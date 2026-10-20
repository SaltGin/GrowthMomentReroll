using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using Verse;

[assembly: AssemblyTitle("Growth Moment Reroll")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.7.2")]
[assembly: InternalsVisibleTo("GrowthMomentReroll.Tests")]

namespace GrowthMomentReroll
{
    public sealed class GrowthMomentRerollMod : Mod
    {
        internal const string HarmonyId = "saltgin.growthmomentreroll";
        internal static RerollSettings Settings = new RerollSettings();

        public GrowthMomentRerollMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<RerollSettings>();
            new Harmony(HarmonyId).PatchAll(typeof(GrowthMomentRerollMod).Assembly);
        }

        public override string SettingsCategory() => "GMR_ModName".Translate();

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.Draw(inRect);
        }
    }
}
