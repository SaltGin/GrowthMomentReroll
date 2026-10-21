using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace GrowthMomentReroll
{
    // Better Growth Moments
    [StaticConstructorOnStartup]
    internal static class BgmCompatibility
    {
        internal static bool Active { get; }
        private static FieldInfo selectedPassions;
        private static FieldInfo selectedTraits;
        private static FieldInfo lastLetter;

        static BgmCompatibility()
        {
            Active = ModsConfig.IsActive("Arkymn.BetterGrowthMoments");
            if (!Active) return;
            var patchType = Type.GetType("BetterGrowthMoments.Patch_Dialog_GrowthMomentChoices_DoWindowContents, BetterGrowthMoments", throwOnError: false);
            if (patchType == null) return;
            selectedPassions = AccessTools.Field(patchType, "selectedPassions");
            selectedTraits = AccessTools.Field(patchType, "selectedTraits");
            lastLetter = AccessTools.Field(patchType, "lastLetter");
        }

        internal static bool TryGetSelections(ChoiceLetter_GrowthMoment letter,
            out List<SkillDef> passions, out List<Trait> traits, bool needPassions = true, bool needTraits = true)
        {
            passions = null;
            traits = null;
            if (lastLetter == null) return false;
            if (!ReferenceEquals(lastLetter.GetValue(null), letter)) return false;
            if (needPassions) passions = selectedPassions?.GetValue(null) as List<SkillDef>;
            if (needTraits) traits = selectedTraits?.GetValue(null) as List<Trait>;
            return (!needPassions || passions != null) && (!needTraits || traits != null);
        }
    }
}
