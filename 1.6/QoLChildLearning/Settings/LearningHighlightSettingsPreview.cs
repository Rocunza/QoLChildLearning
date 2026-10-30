using UnityEngine;
using Verse;

namespace ZCN.QoL.ChildLearning
{
    /// <summary>
    /// UI10: settings-only demonstration of the numeric label color/size.
    /// The number 49 is a fixed sample, never a pawn/Need_Learning value.
    /// Reuses the reading preview's picture, with no duplicate asset cache.
    /// </summary>
    internal static class LearningHighlightSettingsPreview
    {
        internal const float PanelHeight = 132f;
        private const float PortraitSide = 48f;

        internal static void Draw(Rect rect, ChildLearningSettings settings)
        {
            if (settings == null || !settings.showChildLearningPercent ||
                !settings.highlightActiveChildLearning ||
                rect.width < 125f || rect.height < 114f)
                return;

            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            Color oldColor = GUI.color;
            try
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(new Rect(rect.x + 5f, rect.y + 8f,
                    rect.width - 10f, 24f),
                    "ZCNCL_HighlightPreviewTitle".Translate().ToString());

                Rect portrait = new Rect(rect.center.x - PortraitSide * 0.5f,
                    rect.y + 39f, PortraitSide, PortraitSide);
                ReadingIconSettingsPreview.DrawExamplePortrait(portrait);

                // UI10: centered name-slot number directly beneath the
                // portrait, in the same coordinate system as the portrait.
                // Avoid GUIUtility.ScaleAroundPivot here: the IMGUI matrix
                // causes the text to drift inside a scrolled Listing_Standard.
                float zoom = LearningColonistBarScale.GuiScale(
                    settings.childLearningNumberSizePercent);
                float labelWidth = portrait.width + 38f;
                Rect labelRect = new Rect(
                    portrait.center.x - labelWidth * 0.5f,
                    portrait.yMax + 1f, labelWidth, 29f);
                GUIStyle numberStyle = new GUIStyle(Text.CurFontStyle);
                numberStyle.alignment = TextAnchor.UpperCenter;
                numberStyle.padding = new RectOffset(0, 0, 0, 0);
                int baseFontSize = numberStyle.fontSize > 0
                    ? numberStyle.fontSize : 14;
                numberStyle.fontSize = Mathf.Max(1,
                    Mathf.RoundToInt(baseFontSize * zoom));
                numberStyle.normal.textColor = LearningHighlightPalette.TextColor(
                    settings.activeChildLearningHighlightColor, oldColor.a);
                GUI.color = Color.white;
                GUI.Label(labelRect, "49", numberStyle);
            }
            finally
            {
                GUI.color = oldColor;
                Text.Anchor = oldAnchor;
                Text.Font = oldFont;
            }
        }
    }
}
