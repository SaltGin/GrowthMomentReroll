using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Verse;

namespace GrowthMomentReroll
{
    internal sealed class RerollState
    {
        internal bool Initialized;
        internal float PointsSnapshot;
        internal int Total;
        internal int Used;
        internal int Remaining => Math.Max(0, Total - Used);

        internal void Initialize(float points, int tier, float threshold, RerollSettings settings)
        {
            if (Initialized) return;
            PointsSnapshot = points;
            int index = Math.Max(0, Math.Min(tier, settings.BaseRerolls.Length - 1));
            int baseCount = Math.Max(0, settings.BaseRerolls[index]);
            double extra = Math.Floor(Math.Max(0d, (double)points - threshold) / Math.Max(1, settings.OverflowStep));
            if (double.IsNaN(extra)) extra = 0;
            Total = (int)Math.Min(int.MaxValue, baseCount + extra);
            Used = 0;
            Initialized = true;
        }

        internal void ExposeData()
        {
            Scribe_Values.Look(ref Initialized, "gmr_initialized", false);
            Scribe_Values.Look(ref PointsSnapshot, "gmr_pointsSnapshot", 0f);
            Scribe_Values.Look(ref Total, "gmr_total", 0);
            Scribe_Values.Look(ref Used, "gmr_used", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                Total = Math.Max(0, Total);
                Used = Math.Max(0, Math.Min(Used, Total));
            }
        }
    }

    internal static class LetterStates
    {
        private static readonly ConditionalWeakTable<ChoiceLetter_GrowthMoment, RerollState> States =
            new ConditionalWeakTable<ChoiceLetter_GrowthMoment, RerollState>();

        internal static RerollState Get(ChoiceLetter_GrowthMoment letter) => States.GetValue(letter, _ => new RerollState());

        internal static RerollState EnsureInitialized(ChoiceLetter_GrowthMoment letter)
        {
            var state = Get(letter);
            if (!state.Initialized && !letter.ArchiveView && letter.pawn.ageTracker != null)
            {
                var tiers = GrowthUtility.GrowthTiers;
                state.Initialize(letter.pawn.ageTracker.growthPoints, letter.growthTier,
                    tiers[tiers.Length - 1].pointsRequirement, GrowthMomentRerollMod.Settings);
            }
            return state;
        }
    }

    [HarmonyPatch(typeof(ChoiceLetter_GrowthMoment), nameof(ChoiceLetter_GrowthMoment.ConfigureGrowthLetter))]
    internal static class ConfigureLetterPatch
    {
        [HarmonyPostfix, HarmonyPriority(Priority.Last)]
        private static void Postfix(ChoiceLetter_GrowthMoment __instance) => LetterStates.EnsureInitialized(__instance);
    }

    [HarmonyPatch(typeof(ChoiceLetter_GrowthMoment), nameof(ChoiceLetter_GrowthMoment.ExposeData))]
    internal static class ExposeLetterPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ChoiceLetter_GrowthMoment __instance) => LetterStates.Get(__instance).ExposeData();
    }
}
