using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;

namespace GrowthMomentReroll
{
    // Better Growth Moments
    internal static class BgmCompatibility
    {
        private static bool checkedAssembly;
        private static Type patchType;
        private static FieldInfo selectedPassions;
        private static FieldInfo selectedTraits;
        private static FieldInfo lastLetter;

        // Resolve only after startup, when all optional mod assemblies have loaded.
        internal static bool TryGetSelections(ChoiceLetter_GrowthMoment letter,
            out List<SkillDef> passions, out List<Trait> traits)
        {
            passions = null;
            traits = null;
            if (!checkedAssembly)
            {
                patchType = AccessTools.TypeByName("BetterGrowthMoments.Patch_Dialog_GrowthMomentChoices_DoWindowContents");
                if (patchType != null)
                {
                    selectedPassions = AccessTools.Field(patchType, "selectedPassions");
                    selectedTraits = AccessTools.Field(patchType, "selectedTraits");
                    lastLetter = AccessTools.Field(patchType, "lastLetter");
                }
                checkedAssembly = true;
            }
            if (patchType == null) return true;
            if (selectedPassions == null || selectedTraits == null || lastLetter == null) return false;
            if (!ReferenceEquals(lastLetter.GetValue(null), letter)) return false;
            passions = selectedPassions.GetValue(null) as List<SkillDef>;
            traits = selectedTraits.GetValue(null) as List<Trait>;
            return passions != null && traits != null;
        }
    }
}
