using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace GrowthMomentReroll
{
    [StaticConstructorOnStartup]
    internal static class AgeUpGenesCompatibility
    {
        private static readonly PropertyInfo ChoiceLookup;
        private static readonly MethodInfo GenerateChoices;
        private static readonly ConstructorInfo NewChoices;
        private static readonly FieldInfo ShouldDoChoice, GeneChoices, SelectedGene, GeneType;
        internal static bool Active { get; }
        internal static bool Ready { get; }

        static AgeUpGenesCompatibility()
        {
            Active = ModsConfig.IsActive("keyz182.AgeUpGenes");
            if (!Active) { Ready = true; return; }
            try
            {
                var patch = Type.GetType("AgeUpGenes.HarmonyPatches.Dialog_GrowthMomentChoices_Patch, AgeUpGenes", false);
                var choices = Type.GetType("AgeUpGenes.Choices, AgeUpGenes", false);
                if (patch == null || choices == null) throw new InvalidOperationException("AgeUpGenes choice types were not found.");
                ChoiceLookup = AccessTools.Property(patch, "ChoiceLookup");
                ShouldDoChoice = AccessTools.Field(choices, "ShouldDoChoice");
                GeneChoices = AccessTools.Field(choices, "geneChoices");
                SelectedGene = AccessTools.Field(choices, "selectedGene");
                GeneType = AccessTools.Field(choices, "type");
                if (ChoiceLookup?.GetGetMethod() == null || ShouldDoChoice?.FieldType != typeof(bool)
                    || GeneChoices == null || SelectedGene == null || GeneType == null)
                    throw new InvalidOperationException("AgeUpGenes choice fields differ.");
                GenerateChoices = AccessTools.Method(patch, "GeneChoicesForPawn",
                    new[] { typeof(Pawn), GeneType.FieldType.MakeByRefType() });
                if (GenerateChoices == null || GenerateChoices.ReturnType != GeneChoices.FieldType)
                    throw new InvalidOperationException("AgeUpGenes generation entry point differs.");
                NewChoices = AccessTools.Constructor(choices)
                    ?? throw new InvalidOperationException("AgeUpGenes choice constructor was not found.");
                Ready = true;
            }
            catch (Exception ex)
            {
                Log.Error("[Growth Moment Reroll] AgeUpGenes compatibility unavailable.\n" + ex);
            }
        }

        internal static bool HasChoiceState(Pawn pawn)
            => Active && Ready && pawn != null && ChoiceFor(pawn, out _) != null;

        private static object ChoiceFor(Pawn pawn, out IDictionary lookup)
        {
            lookup = ChoiceLookup.GetValue(null, null) as IDictionary
                ?? throw new InvalidOperationException("AgeUpGenes choice lookup is unavailable.");
            return lookup[pawn];
        }

        internal static Action PrepareReroll(Pawn pawn)
        {
            if (!Active) return null;
            if (!Ready) throw new InvalidOperationException("AgeUpGenes compatibility was not initialized.");
            if (ChoiceFor(pawn, out var lookup) == null) return null;
            // The upstream constructor rolls GeneEventChance once and starts with empty selections.
            // Publish only after every generator has succeeded; upstream saves this lookup.
            var draft = NewChoices.Invoke(null);
            if ((bool)ShouldDoChoice.GetValue(draft))
            {
                var args = new object[] { pawn, null };
                var pool = GenerateChoices.Invoke(null, args) as IList;
                if (pool == null || pool.Count == 0)
                    throw new InvalidOperationException("AgeUpGenes returned no gene candidates.");
                GeneChoices.SetValue(draft, pool);
                GeneType.SetValue(draft, args[1]);
            }
            return () => lookup[pawn] = draft;
        }
    }
}
