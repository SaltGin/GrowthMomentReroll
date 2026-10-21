using System;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace GrowthMomentReroll
{
    public sealed class RerollSettings : ModSettings
    {
        public int[] BaseRerolls = NewDefaults();
        public int OverflowStep = 5;
        public bool RerollTraits = true, RerollPassions = true, RerollGenes = true;

        private readonly string[] tierBuffers = new string[9];
        private string stepBuffer;
        private Vector2 scrollPosition;
        private float contentHeight = 600f;

        private static int[] NewDefaults() => new[] { 0, 0, 0, 0, 1, 1, 1, 1, 2 };

        public override void ExposeData()
        {
            for (int i = 0; i < BaseRerolls.Length; i++)
            {
                int defaultValue = i == 8 ? 2 : (i >= 4 ? 1 : 0);
                Scribe_Values.Look(ref BaseRerolls[i], "tier" + i, defaultValue);
            }
            Scribe_Values.Look(ref OverflowStep, "overflowStep", 5);
            Scribe_Values.Look(ref RerollTraits, "rerollTraits", true);
            Scribe_Values.Look(ref RerollPassions, "rerollPassions", true);
            Scribe_Values.Look(ref RerollGenes, "rerollGenes", true);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                for (int i = 0; i < BaseRerolls.Length; i++)
                    BaseRerolls[i] = Math.Max(0, BaseRerolls[i]);
                OverflowStep = Math.Max(1, OverflowStep);
            }
        }

        internal void Draw(Rect inRect)
        {
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            bool oldWrap = Text.WordWrap;
            try
            {
                Text.Anchor = TextAnchor.UpperLeft;
                Text.WordWrap = true;
                var viewRect = new Rect(0f, 0f, inRect.width - 20f, Mathf.Max(inRect.height, contentHeight));
                Widgets.BeginScrollView(inRect, ref scrollPosition, viewRect);
                var listing = new Listing_Standard { maxOneColumn = true };
                listing.Begin(viewRect);
                DrawSectionHeading(listing, "GMR_RerollParts".Translate());
                listing.CheckboxLabeled("GMR_RerollTraits".Translate(), ref RerollTraits);
                listing.CheckboxLabeled("GMR_RerollPassions".Translate(), ref RerollPassions);
                if (AgeUpGenesCompatibility.Active)
                    listing.CheckboxLabeled("GMR_RerollGenes".Translate(), ref RerollGenes);
                listing.Label("GMR_RerollPartsTiming".Translate());
                listing.Gap(16f);
                DrawSectionHeading(listing, "GMR_RerollBudget".Translate());
                listing.Label("GMR_SettingsExplanation".Translate());
                for (int i = 0; i < BaseRerolls.Length; i++)
                    DrawAdjustRow(listing, "GMR_TierBudget".Translate(i), ref BaseRerolls[i], ref tierBuffers[i], 0);
                listing.GapLine();
                DrawAdjustRow(listing, "GMR_OverflowStep".Translate(), ref OverflowStep, ref stepBuffer, 1);
                listing.Gap();
                listing.Label("GMR_SettingsTiming".Translate());
                listing.Gap();
                Rect resetRect = listing.GetRect(30f);
                resetRect.width = Mathf.Min(220f, resetRect.width);
                if (Widgets.ButtonText(resetRect, "GMR_ResetDefaults".Translate()))
                {
                    BaseRerolls = NewDefaults();
                    OverflowStep = 5;
                    RerollTraits = RerollPassions = RerollGenes = true;
                    Array.Clear(tierBuffers, 0, tierBuffers.Length);
                    stepBuffer = null;
                    GUI.FocusControl(null);
                    SoundDefOf.Click.PlayOneShotOnCamera();
                }
                listing.Gap();
                contentHeight = listing.CurHeight;
                listing.End();
                Widgets.EndScrollView();
            }
            finally
            {
                Text.Font = oldFont;
                Text.Anchor = oldAnchor;
                Text.WordWrap = oldWrap;
            }
        }

        private static void DrawSectionHeading(Listing_Standard listing, string label)
        {
            GameFont oldFont = Text.Font;
            Text.Font = GameFont.Medium;
            listing.Label(label);
            Text.Font = oldFont;
            listing.GapLine();
        }

        private static void DrawAdjustRow(Listing_Standard listing, string label, ref int value, ref string buffer, int min)
        {
            const float buttonWidth = 32f, fieldWidth = 112f, gap = 6f, controlHeight = 30f;
            const float controlsWidth = buttonWidth * 2f + fieldWidth + gap * 2f;
            float labelWidth = Mathf.Max(1f, listing.ColumnWidth - controlsWidth - 16f);
            Rect row = listing.GetRect(Mathf.Max(controlHeight, Text.CalcHeight(label, labelWidth)));
            TextAnchor oldAnchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(row.x, row.y, labelWidth, row.height), label);
            Text.Anchor = oldAnchor;
            var left = new Rect(row.xMax - controlsWidth, row.y + (row.height - controlHeight) / 2f, buttonWidth, controlHeight);
            var field = new Rect(left.xMax + gap, left.y, fieldWidth, controlHeight);
            var right = new Rect(field.xMax + gap, left.y, buttonWidth, controlHeight);
            Color oldColor = GUI.color;
            bool decrease = GUI.enabled && value > min;
            GUI.color = decrease ? oldColor : oldColor * Color.gray;
            if (Widgets.ButtonText(left, "<", doMouseoverSound: decrease, active: decrease))
            {
                value--;
                buffer = value.ToString();
                SoundDefOf.Checkbox_TurnedOff.PlayOneShotOnCamera();
            }
            GUI.color = oldColor;
            // Parse integers directly: the game's numeric widget clamps through float.
            string edited = Widgets.TextField(field, buffer ?? value.ToString());
            if (edited.Length == 0)
                buffer = edited;
            else if (int.TryParse(edited, out int entered))
            {
                value = Math.Max(min, entered);
                buffer = value.ToString();
            }
            bool increase = GUI.enabled && value < int.MaxValue;
            GUI.color = increase ? oldColor : oldColor * Color.gray;
            if (Widgets.ButtonText(right, ">", doMouseoverSound: increase, active: increase))
            {
                value++;
                buffer = value.ToString();
                SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera();
            }
            GUI.color = oldColor;
            listing.Gap(4f);
        }
    }
}
