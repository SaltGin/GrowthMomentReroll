using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace GrowthMomentReroll
{
    [Flags]
    internal enum RerollParts { None = 0, Traits = 1, Passions = 2, Genes = 4, Vanilla = Traits | Passions }

    internal static class RerollService
    {
        private static readonly AccessTools.FieldRef<ChoiceLetter_GrowthMoment, int> PassionCount =
            AccessTools.FieldRefAccess<ChoiceLetter_GrowthMoment, int>("passionChoiceCount");
        private static readonly AccessTools.FieldRef<Dialog_GrowthMomentChoices, List<SkillDef>> ChosenPassions =
            AccessTools.FieldRefAccess<Dialog_GrowthMomentChoices, List<SkillDef>>("chosenPassions");
        private static readonly AccessTools.FieldRef<Dialog_GrowthMomentChoices, Trait> ChosenTrait =
            AccessTools.FieldRefAccess<Dialog_GrowthMomentChoices, Trait>("chosenTrait");

        internal static RerollParts GetParts(ChoiceLetter_GrowthMoment letter)
        {
            if (letter == null || letter.ArchiveView) return RerollParts.None;
            var settings = GrowthMomentRerollMod.Settings;
            var parts = RerollParts.None;
            if (settings.RerollTraits && (letter.traitChoiceCount > 0 || !letter.traitChoices.NullOrEmpty())) parts |= RerollParts.Traits;
            if (settings.RerollPassions && (PassionCount(letter) > 0 || !letter.passionChoices.NullOrEmpty())) parts |= RerollParts.Passions;
            if (settings.RerollGenes && AgeUpGenesCompatibility.HasChoiceState(letter.pawn)) parts |= RerollParts.Genes;
            return parts;
        }

        internal static bool HasUsableQuotas(ChoiceLetter_GrowthMoment letter, RerollParts parts = RerollParts.Vanilla)
            => !((parts & RerollParts.Passions) != 0 && PassionCount(letter) <= 0 && !letter.passionChoices.NullOrEmpty())
                && !((parts & RerollParts.Traits) != 0 && letter.traitChoiceCount <= 0 && !letter.traitChoices.NullOrEmpty());

        internal static string UnavailableReason(ChoiceLetter_GrowthMoment letter, RerollParts parts)
            => parts == RerollParts.None ? "GMR_NoRerollParts"
                : !HasUsableQuotas(letter, parts) ? "GMR_QuotaUnavailable"
                : (parts & RerollParts.Vanilla) != 0 && !GmmsnCompatibility.Ready ? "GMR_GmmsnUnsupported" : null;

        internal static bool TryReroll(Dialog_GrowthMomentChoices dialog, ChoiceLetter_GrowthMoment letter)
        {
            if (letter == null || letter.ArchiveView) return false;
            try
            {
                var parts = GetParts(letter);
                string reason = UnavailableReason(letter, parts);
                if (reason != null)
                {
                    Messages.Message(reason.Translate(), null, MessageTypeDefOf.RejectInput, historical: false);
                    return false;
                }
                var state = LetterStates.EnsureInitialized(letter);
                if (state.Remaining == 0) return false;
                bool rerollTraits = (parts & RerollParts.Traits) != 0, rerollPassions = (parts & RerollParts.Passions) != 0;
                List<SkillDef> bgmPassions = null;
                List<Trait> bgmTraits = null;
                if ((parts & RerollParts.Vanilla) != 0 && BgmCompatibility.Active
                    && !BgmCompatibility.TryGetSelections(letter, out bgmPassions, out bgmTraits, rerollPassions, rerollTraits))
                {
                    Messages.Message("GMR_BgmUnsupported".Translate(), null, MessageTypeDefOf.RejectInput, historical: false);
                    return false;
                }

                // The flag encodes the one slot removed by vanilla. Never infer quota from list.Count.
                int originalTraitCount = rerollTraits ? checked(letter.traitChoiceCount + (letter.noTraitOptionShown ? 1 : 0)) : 0;
                int passionCount = PassionCount(letter);
                var passions = letter.passionChoices;
                var traits = letter.traitChoices;
                bool noTrait = letter.noTraitOptionShown;
                int effectiveTraitCount = letter.traitChoiceCount;
                if ((parts & RerollParts.Vanilla) != 0 && GmmsnCompatibility.Active)
                {
                    var draft = GmmsnCompatibility.Generate(letter, rerollPassions ? passionCount : 0, originalTraitCount);
                    if (rerollPassions)
                    {
                        passions = draft.passionChoices;
                        passionCount = GmmsnCompatibility.PassionCount(draft);
                    }
                    if (rerollTraits)
                    {
                        traits = draft.traitChoices;
                        noTrait = draft.noTraitOptionShown;
                        effectiveTraitCount = draft.traitChoiceCount;
                    }
                }
                else
                {
                    if (rerollPassions) passions = ChoiceLetter_GrowthMoment.PassionOptions(letter.pawn, passionCount, checkGenes: false);
                    if (rerollTraits)
                    {
                        noTrait = originalTraitCount > 2 && Rand.Value < ChoiceLetter_GrowthMoment.ChanceForNoTraitOption;
                        effectiveTraitCount = originalTraitCount - (noTrait ? 1 : 0);
                        traits = effectiveTraitCount > 0
                            ? PawnGenerator.GenerateTraitsFor(letter.pawn, effectiveTraitCount, null, growthMomentTrait: true) : null;
                    }
                }
                if ((rerollPassions && passions == null) || (rerollTraits && effectiveTraitCount > 0 && traits == null))
                    throw new InvalidOperationException("Candidate generator returned null.");
                var commitGenes = (parts & RerollParts.Genes) != 0 ? AgeUpGenesCompatibility.PrepareReroll(letter.pawn) : null;
                if ((parts & RerollParts.Genes) != 0 && commitGenes == null)
                    throw new InvalidOperationException("The AgeUpGenes growth state changed during generation.");

                // Generation is complete. Clear only runtime selections, then commit the new round.
                commitGenes?.Invoke();
                if (rerollPassions)
                {
                    ChosenPassions(dialog)?.Clear();
                    bgmPassions?.Clear();
                    PassionCount(letter) = passionCount;
                    letter.passionChoices = passions;
                }
                if (rerollTraits)
                {
                    ChosenTrait(dialog) = null;
                    bgmTraits?.Clear();
                    letter.traitChoiceCount = effectiveTraitCount;
                    letter.noTraitOptionShown = noTrait;
                    letter.traitChoices = traits;
                }
                state.Used++;
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("[Growth Moment Reroll] Reroll failed; candidates and budget were retained.\n" + ex);
                Messages.Message("GMR_RerollFailed".Translate(), null, MessageTypeDefOf.RejectInput, historical: false);
                return false;
            }
        }
    }
}
