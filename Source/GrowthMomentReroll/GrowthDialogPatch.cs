using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace GrowthMomentReroll
{
    [HarmonyPatch(typeof(Dialog_GrowthMomentChoices), nameof(Dialog_GrowthMomentChoices.DoWindowContents))]
    internal static class GrowthDialogPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Dialog_GrowthMomentChoices __instance, Rect inRect, ChoiceLetter_GrowthMoment ___letter)
        {
            var letter = ___letter;
            if (!__instance.IsOpen || letter == null || letter.ArchiveView || !letter.ShowInfoTabs) return;
            var state = LetterStates.EnsureInitialized(letter);
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            bool oldWrap = Text.WordWrap;
            Color oldColor = GUI.color;
            bool oldEnabled = GUI.enabled;
            try
            {
                float height = Window.CloseButSize.y;
                float x = Window.CloseButSize.x + 12f;
                float y = inRect.yMax - height;
                Text.Font = GameFont.Small;
                string caption = "GMR_Reroll".Translate();
                float width = Mathf.Max(84f, Text.CalcSize(caption).x + 24f);
                var button = new Rect(x, y, width, height);
                bool usableQuotas = RerollService.HasUsableQuotas(letter);
                bool canReroll = oldEnabled && state.Remaining > 0 && GmmsnCompatibility.Ready && usableQuotas;
                GUI.enabled = canReroll;
                if (!canReroll) GUI.color = oldColor * Color.gray;
                // The 1.7 GUI port does not consistently enforce GUI.enabled on buttons.
                if (Widgets.ButtonText(button, caption, doMouseoverSound: canReroll, active: canReroll) &&
                    RerollService.TryReroll(__instance, letter))
                    SoundDef.Named("Message_PositiveEvent").PlayOneShotOnCamera();
                GUI.enabled = oldEnabled;
                GUI.color = oldColor;
                if (oldEnabled && !canReroll && Widgets.ButtonInvisible(button, doMouseoverSound: false))
                    SoundDefOf.ClickReject.PlayOneShotOnCamera();
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleLeft;
                Text.WordWrap = false;
                var counts = new Rect(button.xMax + 8f, y, 160f, height);
                Widgets.Label(counts, "GMR_Counts".Translate(state.Remaining, state.Total));
                string tooltipKey = !GmmsnCompatibility.Ready ? "GMR_GmmsnUnsupported"
                    : !usableQuotas ? "GMR_QuotaUnavailable" : "GMR_RerollTooltip";
                TooltipHandler.TipRegion(button, tooltipKey.Translate());
            }
            finally
            {
                Text.Font = oldFont;
                Text.Anchor = oldAnchor;
                Text.WordWrap = oldWrap;
                GUI.color = oldColor;
                GUI.enabled = oldEnabled;
            }
        }
    }
}
