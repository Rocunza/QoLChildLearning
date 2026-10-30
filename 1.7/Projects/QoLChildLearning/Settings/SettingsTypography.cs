using UnityEngine;
using Verse;

namespace ZCN.QoL.ChildLearning
{
    /// <summary>
    /// UI2: limit text emphasis to top-level feature toggles. Restore
    /// global IMGUI Text.Font and GUI.color after every element so that
    /// unrelated settings and other mods keep their original styles.
    /// </summary>
    internal static class SettingsTypography
    {
        internal static void FeatureToggle(Listing_Standard listing, string label, ref bool enabled)
        {
            GameFont oldFont = Text.Font;
            try
            {
                Text.Font = GameFont.Medium;
                listing.CheckboxLabeled(label, ref enabled);
            }
            finally
            {
                Text.Font = oldFont;
            }
        }

        // UI2: a divider only between independently controlled feature groups.
        internal static void GroupDivider(Listing_Standard listing)
        {
            listing.Gap(10f);
            listing.GapLine();
            listing.Gap(12f);
        }

        // UI2: an indented, left-aligned radio column is easier to scan than
        // full-width radios at the extreme right edge of the settings window.
        // The label stays 10 px from the circle in both English and Korean.
        internal static bool RadioChoice(Listing_Standard listing, string label, bool selected)
        {
            float height = Mathf.Max(28f, Text.LineHeight + 2f);
            Rect row = listing.GetRect(height);
            const float indent = 20f;
            const float radioSize = 24f;
            const float textGap = 10f;
            float radioX = row.x + indent;
            float radioY = row.y + (height - radioSize) * 0.5f;
            bool clicked = Widgets.RadioButton(radioX, radioY, selected);

            float textX = radioX + radioSize + textGap;
            float availableWidth = Mathf.Max(20f, row.xMax - textX);
            float desiredWidth = Text.CalcSize(label).x + 8f;
            float textWidth = Mathf.Min(availableWidth, Mathf.Min(420f, desiredWidth));
            Rect textRect = new Rect(textX, row.y, textWidth, height);
            TextAnchor previousAnchor = Text.Anchor;
            try
            {
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(textRect, label);
            }
            finally
            {
                Text.Anchor = previousAnchor;
            }
            if (desiredWidth > textWidth)
                TooltipHandler.TipRegion(textRect, label);
            if (Widgets.ButtonInvisible(textRect, false))
                clicked = true;
            listing.Gap(2f);
            return clicked;
        }

        internal static void Description(Listing_Standard listing, string description)
        {
            GameFont oldFont = Text.Font;
            Color oldColor = GUI.color;
            try
            {
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.78f, 0.78f, 0.78f, oldColor.a);
                listing.Label(description);
            }
            finally
            {
                GUI.color = oldColor;
                Text.Font = oldFont;
            }
        }
    }
}
