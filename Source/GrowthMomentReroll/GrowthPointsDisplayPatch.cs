using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace GrowthMomentReroll
{
    [HarmonyPatch(typeof(Gizmo_GrowthTier), "DrawGrowthTier")]
    internal static class GrowthPointsDisplayPatch
    {
        [HarmonyPostfix]
        internal static void Postfix(Rect rect, Pawn ___child,
            Texture2D ___BarTex, Texture2D ___EmptyBarTex, bool __runOriginal)
        {
            if (!__runOriginal || !___child.ageTracker.AtMaxGrowthTier) return;
            Rect barRect = rect;
            barRect.xMin = rect.x + rect.width * 0.55f;
            barRect.yMin += 2f;
            barRect.yMax -= 2f;
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            try
            {
                // Repaint the full bar and border to cover the original label.
                Widgets.FillableBar(barRect, ___child.ageTracker.PercentToNextGrowthTier,
                    ___BarTex, ___EmptyBarTex, doBorder: true);
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(barRect, PointsLabel(___child));
                // Only the repainted area needs its hover highlight restored.
                if (Mouse.IsOver(rect)) Widgets.DrawHighlight(barRect);
            }
            finally
            {
                Text.Font = oldFont;
                Text.Anchor = oldAnchor;
            }
        }

        internal static string PointsLabel(Pawn child) => Mathf.FloorToInt(child.ageTracker.growthPoints)
            + " / " + GrowthUtility.GrowthTiers[GrowthUtility.GrowthTiers.Length - 1].pointsRequirement;
    }
}
