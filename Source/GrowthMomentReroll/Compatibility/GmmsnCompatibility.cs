using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Verse;

namespace GrowthMomentReroll
{
    // Growth Moments Make Sense Now
    [StaticConstructorOnStartup]
    internal static class GmmsnCompatibility
    {
        private const string Owner = GrowthMomentRerollMod.HarmonyId + ".gmmsn";
        private static readonly MethodInfo SetChoices;
        private static readonly Func<Pawn, SkillDef, bool, bool> IsValidPassion;
        internal static readonly AccessTools.FieldRef<ChoiceLetter_GrowthMoment, int> PassionCount;

        internal static bool Active { get; }
        internal static bool Ready { get; }

        private struct Quotas
        {
            internal bool Restore, NewTraits;
            internal int Passions, Traits;
        }

        static GmmsnCompatibility()
        {
            Active = ModsConfig.IsActive("Jdalt.GMMSN");
            if (!Active) { Ready = true; return; }
            var harmony = new Harmony(Owner);
            MethodInfo target = null;
            try
            {
                var type = Type.GetType("Growth_Moments_Make_Sense_Now.HarmonyPatches, GMMSN", throwOnError: false)
                    ?? throw new InvalidOperationException("GMMSN compatibility type was not found.");
                SetChoices = AccessTools.Method(typeof(ChoiceLetter_GrowthMoment), "TrySetChoices");
                PassionCount = AccessTools.FieldRefAccess<ChoiceLetter_GrowthMoment, int>("passionChoiceCount");
                // GMMSN also initializes on startup. Force its registration first,
                // independent of which static constructor the game visits first.
                RuntimeHelpers.RunClassConstructor(type.TypeHandle);
                target = AccessTools.Method(type, "GMMSN_TrySetChoices", new[] {
                    typeof(ChoiceLetter_GrowthMoment), typeof(int).MakeByRefType(), typeof(int).MakeByRefType() });
                var predicate = AccessTools.Field(type, "IsValidPassion")?.GetValue(null) as Delegate;
                if (target == null || predicate == null
                    || Harmony.GetPatchInfo(SetChoices)?.Owners.Contains("Jdalt.RimWorld.GMMSN.Main") != true)
                    throw new InvalidOperationException("GMMSN generation entry point differs.");
                IsValidPassion = (Func<Pawn, SkillDef, bool, bool>)Delegate.CreateDelegate(
                    typeof(Func<Pawn, SkillDef, bool, bool>), predicate.Target, predicate.Method);
                harmony.Patch(target,
                    prefix: new HarmonyMethod(typeof(GmmsnCompatibility), nameof(PrepareChoices)),
                    finalizer: new HarmonyMethod(typeof(GmmsnCompatibility), nameof(RestoreQuotas)));
                Ready = true;
            }
            catch (Exception ex)
            {
                if (target != null)
                {
                    try { harmony.Unpatch(target, HarmonyPatchType.All, Owner); }
                    catch (Exception rollback) { Log.Error("[Growth Moment Reroll] Could not remove failed GMMSN patch.\n" + rollback); }
                }
                Log.Error("[Growth Moment Reroll] GMMSN compatibility unavailable; rerolls are disabled.\n" + ex);
            }
        }

        private static bool PrepareChoices(ChoiceLetter_GrowthMoment __0, int __1, int __2,
            ref bool __result, out Quotas __state)
        {
            __state = default;
            if (__0 == null) return true;
            bool newPassions = __1 > 0 && __0.passionChoices == null;
            bool newTraits = __2 > 0 && __0.traitChoices == null;
            if (!newPassions && !newTraits)
            {
                // Preserve cached pools without repeating inheritance or quota clamping.
                __result = true;
                return false;
            }
            if (__0.choiceMade || __0.pawn.DestroyedOrNull()) return true;
            __state = new Quotas { Restore = true, NewTraits = newTraits, Passions = __1, Traits = __2 };
            // Match GMMSN's clamp using its actual validity predicate.
            __state.Passions = Math.Min(__1,
                __0.pawn.skills.skills.Count(skill => IsValidPassion(__0.pawn, skill.def, false)));
            return true;
        }

        private static void RestoreQuotas(ref int __1, ref int __2, ChoiceLetter_GrowthMoment __0,
            Quotas __state, bool __runOriginal)
        {
            if (!__state.Restore || !__runOriginal) return;
            __1 = __state.Passions;
            __2 = __state.Traits - (__state.NewTraits && __0.noTraitOptionShown ? 1 : 0);
        }

        internal static ChoiceLetter_GrowthMoment Generate(ChoiceLetter_GrowthMoment letter, int passionCount, int traitCount)
        {
            if (!Ready) throw new InvalidOperationException("GMMSN compatibility was not initialized.");
            var draft = new ChoiceLetter_GrowthMoment {
                pawn = letter.pawn, growthTier = letter.growthTier,
                passionGainsCount = letter.passionGainsCount, traitChoiceCount = traitCount
            };
            PassionCount(draft) = passionCount;
            SetChoices.Invoke(draft, null);
            if (draft.traitChoiceCount + (draft.noTraitOptionShown ? 1 : 0) != traitCount)
                throw new InvalidOperationException("GMMSN changed the original trait quota unexpectedly.");
            return draft;
        }
    }
}
