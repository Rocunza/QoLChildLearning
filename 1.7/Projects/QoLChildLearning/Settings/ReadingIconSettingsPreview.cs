using RimWorld;
using UnityEngine;
using Verse;

namespace ZCN.QoL.ChildLearning
{
    /// <summary>
    /// UI8: settings-only visual preview. Reuses the existing portrait texture,
    /// vanilla Reading icon and shared size/position geometry.
    /// Does not mutate actual learning desires or save data.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class ReadingIconSettingsPreview
    {
        internal const float PanelHeight = 118f;
        private const float PreviewPortraitSide = 48f;
        private const string PortraitTexturePath = "QoLChildLearning/PreviewChild";

        // The dedicated mod texture is only requested after game textures
        // are loaded. No static constructor references a Unity asset.
        private static Texture2D exampleChild;
        private static bool portraitLookupDone;

        private static string T(string key)
        {
            return ("ZCNCL_" + key).Translate().ToString();
        }

        private static Texture2D ExamplePortrait
        {
            get
            {
                if (!portraitLookupDone)
                {
                    portraitLookupDone = true;
                    exampleChild = ContentFinder<Texture2D>.Get(
                        PortraitTexturePath, false);
                    if (exampleChild != null)
                        exampleChild.filterMode = FilterMode.Point;
                }
                return exampleChild;
            }
        }

        // UI9: the learning-color preview uses this same portrait texture,
        // fallback drawing and border without loading another Unity asset.
        internal static void DrawExamplePortrait(Rect portrait)
        {
            Widgets.DrawBoxSolid(portrait,
                new Color(0.20f, 0.24f, 0.29f, 1f));
            Texture2D avatar = ExamplePortrait;
            if (avatar != null)
                Widgets.DrawTextureFitted(portrait, avatar, 1f);
            else
            {
                float side = portrait.width;
                Widgets.DrawBoxSolid(new Rect(portrait.center.x - side * 0.17f,
                    portrait.y + side * 0.20f, side * 0.34f, side * 0.37f),
                    new Color(0.56f, 0.60f, 0.64f, 1f));
                Widgets.DrawBoxSolid(new Rect(portrait.center.x - side * 0.33f,
                    portrait.y + side * 0.66f, side * 0.66f, side * 0.30f),
                    new Color(0.47f, 0.53f, 0.59f, 1f));
            }
            Widgets.DrawBox(portrait);
        }

        internal static void Draw(Rect rect, ChildLearningSettings settings)
        {
            // A disabled parent toggle hides the ENTIRE card, and the
            // caller avoids reserving its space in the settings listing.
            if (settings == null || !settings.showReadingIcon ||
                rect.width < 125f || rect.height < 104f)
                return;

            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            Color oldColor = GUI.color;
            try
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(new Rect(rect.x + 6f, rect.y + 8f,
                    rect.width - 12f, 26f), T("IconPreviewTitle"));

                // UI8: The portrait is kept at the verified 48 GUI-unit size.
                // The real on-bar icon is based on fixed-size GUI units
                // (18 / 36 at bar scale 1), not a fraction of a 72-unit
                // reference portrait. The old extra 48/72 zoom shrank just
                // the preview book even though the preview pawn was 1:1.
                // Keep the icon geometry in the same GUI coordinate space.
                Rect portrait = new Rect(
                    rect.center.x - PreviewPortraitSide * 0.5f,
                    rect.y + 43f,
                    PreviewPortraitSide, PreviewPortraitSide);

                DrawExamplePortrait(portrait);

                if (ModsConfig.BiotechActive &&
                    LearningDesireDefOf.Reading != null &&
                    LearningDesireDefOf.Reading.Icon != null)
                {
                    // Use the shared REAL icon sizing and location formulas
                    // directly with the on-screen preview portrait.
                    // 1f matches the reference colonist-bar scale shown in
                    // the comparison screenshots. At other bar scales this
                    // remains a reference example, not a live pawn readout.
                    float size = ReadingIconSizing.GetSize(
                        portrait, 1f, settings.largeReadingIcon);
                    if (size > 0f)
                    {
                        Rect iconRect = ReadingIconSizing.GetIconRect(
                            portrait, size, 1f,
                            settings.readingIconPosition);
                        Widgets.DrawTextureFitted(iconRect,
                            LearningDesireDefOf.Reading.Icon, 1f);
                    }
                }


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
