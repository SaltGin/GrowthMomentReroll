using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace GrowthMomentReroll
{
    internal static class RerollService
    {
        private static readonly AccessTools.FieldRef<ChoiceLetter_GrowthMoment, int> PassionCount =
            AccessTools.FieldRefAccess<ChoiceLetter_GrowthMoment, int>("passionChoiceCount");
        private static readonly AccessTools.FieldRef<Dialog_GrowthMomentChoices, List<SkillDef>> ChosenPassions =
            AccessTools.FieldRefAccess<Dialog_GrowthMomentChoices, List<SkillDef>>("chosenPassions");
        private static readonly AccessTools.FieldRef<Dialog_GrowthMomentChoices, Trait> ChosenTrait =
            AccessTools.FieldRefAccess<Dialog_GrowthMomentChoices, Trait>("chosenTrait");

        internal static bool HasUsableQuotas(ChoiceLetter_GrowthMoment letter)
            => !(PassionCount(letter) <= 0 && !letter.passionChoices.NullOrEmpty())
                && !(letter.traitChoiceCount <= 0 && !letter.traitChoices.NullOrEmpty());

        internal static bool TryReroll(Dialog_GrowthMomentChoices dialog, ChoiceLetter_GrowthMoment letter)
        {
            if (letter == null || letter.ArchiveView || !letter.ShowInfoTabs) return false;
            if (!HasUsableQuotas(letter))
            {
                Messages.Message("GMR_QuotaUnavailable".Translate(), null, MessageTypeDefOf.RejectInput, historical: false);
                return false;
            }
            var state = LetterStates.EnsureInitialized(letter);
            if (state.Remaining == 0) return false;

            try
            {
                if (!BgmCompatibility.TryGetSelections(letter, out var bgmPassions, out var bgmTraits))
                {
                    Messages.Message("GMR_BgmUnsupported".Translate(), null, MessageTypeDefOf.RejectInput, historical: false);
                    return false;
                }

                // The flag encodes the one slot removed by vanilla. Never infer quota from list.Count.
                int originalTraitCount = checked(letter.traitChoiceCount + (letter.noTraitOptionShown ? 1 : 0));
                int passionCount = PassionCount(letter);
                List<SkillDef> passions;
                List<Trait> traits;
                bool noTrait;
                int effectiveTraitCount;
                if (GmmsnCompatibility.Active)
                {
                    if (!GmmsnCompatibility.Ready)
                    {
                        Messages.Message("GMR_GmmsnUnsupported".Translate(), null, MessageTypeDefOf.RejectInput, historical: false);
                        return false;
                    }
                    var draft = GmmsnCompatibility.Generate(letter, passionCount, originalTraitCount);
                    passions = draft.passionChoices;
                    traits = draft.traitChoices;
                    noTrait = draft.noTraitOptionShown;
                    effectiveTraitCount = draft.traitChoiceCount;
                    passionCount = GmmsnCompatibility.PassionCount(draft);
                }
                else
                {
                    passions = passionCount > 0
                        ? ChoiceLetter_GrowthMoment.PassionOptions(letter.pawn, passionCount, checkGenes: false) : null;
                    noTrait = originalTraitCount > 2 && Rand.Value < ChoiceLetter_GrowthMoment.ChanceForNoTraitOption;
                    effectiveTraitCount = originalTraitCount - (noTrait ? 1 : 0);
                    traits = effectiveTraitCount > 0
                        ? PawnGenerator.GenerateTraitsFor(letter.pawn, effectiveTraitCount, null, growthMomentTrait: true) : null;
                }
                if ((passionCount > 0 && passions == null) || (effectiveTraitCount > 0 && traits == null))
                    throw new InvalidOperationException("Candidate generator returned null.");

                // Generation is complete. Clear only runtime selections, then commit the new round.
                ChosenPassions(dialog)?.Clear();
                ChosenTrait(dialog) = null;
                bgmPassions?.Clear();
                bgmTraits?.Clear();
                PassionCount(letter) = passionCount;
                letter.passionChoices = passions;
                letter.traitChoiceCount = effectiveTraitCount;
                letter.noTraitOptionShown = noTrait;
                letter.traitChoices = traits;
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
