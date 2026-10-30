using UnityEngine;
using System;
using System.Globalization;
using Verse;

namespace ZCN.QoL.ChildLearning
{
    /// <summary>
    /// Stage 4S-2H0: presentation-only split. Do not rename Scribe keys.
    /// Read owns book, reading and notifications; Learn owns child Learning UI.
    /// </summary>
    internal static class ChildLearningSettingsUI
    {
        // Keyed labels are evaluated when the settings window is drawn,
        // so switching languages after a game restart does not require a DLL update.
        private static string T(string key)
        {
            return ("ZCNCL_" + key).Translate().ToString();
        }

        // UI buffers are intentionally non-persistent. Values remain in the existing
        // ModSettings XML. Incomplete edits (e.g. "90.") are not overwritten on repaint.
        private static string learningFieldBuffer;
        private static string restFieldBuffer;
        private static string cooldownFieldBuffer;
        private static string foodFieldBuffer;

        internal static void ResetLearnInputBuffers()
        {
            learningFieldBuffer = null;
            restFieldBuffer = null;
            cooldownFieldBuffer = null;
            foodFieldBuffer = null;
        }

        private static void DrawTenthsField(Listing_Standard listing, string title,
            ref int tenths, int maxTenths, ref string inputBuffer,
            string tooltipKey = null, int minTenths = 0)
        {
            // Keep exceptional rules available on hover without adding full
            // paragraphs to the settings page.
            Rect labelRect = listing.GetRect(Mathf.Max(26f, Text.LineHeight + 2f));
            Widgets.Label(labelRect, title);
            if (!string.IsNullOrEmpty(tooltipKey))
                TooltipHandler.TipRegion(labelRect, T(tooltipKey));
            if (inputBuffer == null)
                inputBuffer = (tenths / 10m).ToString("0.0", CultureInfo.InvariantCulture);
            Rect fieldRect = listing.GetRect(30f);
            string entered = Widgets.TextField(fieldRect, inputBuffer);
            if (entered != inputBuffer)
            {
                inputBuffer = entered;
                decimal parsed;
                if ((decimal.TryParse(entered, NumberStyles.Float,
                         CultureInfo.InvariantCulture, out parsed) ||
                     decimal.TryParse(entered, NumberStyles.Float,
                         CultureInfo.CurrentCulture, out parsed)) &&
                    parsed >= minTenths / 10m && parsed <= maxTenths / 10m)
                {
                    tenths = Math.Max(minTenths, ChildWakeRules.LimitTenths(
                        (int)Math.Round(parsed * 10m, 0, MidpointRounding.AwayFromZero),
                        maxTenths));
                }
            }
            // In normal use the text field already shows its value. Surface
            // the last accepted value only if an invalid entry is typed.
            decimal checkedValue;
            bool validBuffer = (decimal.TryParse(inputBuffer, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out checkedValue) ||
                decimal.TryParse(inputBuffer, NumberStyles.Float,
                    CultureInfo.CurrentCulture, out checkedValue)) &&
                checkedValue >= minTenths / 10m && checkedValue <= maxTenths / 10m;
            if (!validBuffer)
                SettingsTypography.Description(listing, T("InvalidNumber") +
                    (tenths / 10m).ToString("0.0", CultureInfo.InvariantCulture));
        }

        // Summaries use the last accepted settings values, not the temporary
        // text-field buffers. This keeps examples truthful during incomplete edits.
        private static string TenthsPercent(int tenths)
        {
            return (tenths / 10m).ToString("0.0", CultureInfo.InvariantCulture);
        }

        // User-facing summary text uses the same font size as the settings labels.
        // Each sentence owns a Listing row, so wrapped translations never overlap.
        private static void DrawSummaryLine(Listing_Standard listing, string value)
        {
            GameFont oldFont = Text.Font;
            try
            {
                Text.Font = GameFont.Small;
                listing.Label(value);
            }
            finally
            {
                Text.Font = oldFont;
            }
        }

        private static string FormatSummary(string key, params object[] arguments)
        {
            return string.Format(CultureInfo.InvariantCulture, T(key), arguments);
        }

        private static void DrawWakeSummary(Listing_Standard listing, ChildLearningSettings settings)
        {
            DrawSummaryLine(listing, FormatSummary("WakeSummaryLearning",
                TenthsPercent(settings.autoWakeLearningTenths)));
            listing.Gap(3f);
            DrawSummaryLine(listing, FormatSummary("WakeSummaryRest",
                TenthsPercent(settings.autoWakeMinimumRestTenths)));
            listing.Gap(3f);
            if (settings.autoWakeCooldownTenthsHours == 0)
                DrawSummaryLine(listing, T("WakeSummaryCooldownZero"));
            else
                DrawSummaryLine(listing, FormatSummary("WakeSummaryCooldown",
                    TenthsPercent(settings.autoWakeCooldownTenthsHours)));
        }

        private static void DrawFoodSummary(Listing_Standard listing, ChildLearningSettings settings)
        {
            DrawSummaryLine(listing, FormatSummary("FoodSummaryStart",
                TenthsPercent(settings.autoEatFoodTenths)));
            listing.Gap(3f);
            DrawSummaryLine(listing, T("FoodSummaryNoInterrupt"));
            listing.Gap(3f);
            DrawSummaryLine(listing, T("FoodSummaryQueuedCommands"));
            listing.Gap(3f);
            DrawSummaryLine(listing, T("FoodSummaryPolicy"));
        }

        internal static void DrawGizmoDefaults(Listing_Standard listing, ChildLearningSettings settings)
        {
            // The third tab already names the feature; show the one scope rule
            // once, then only the three settings and a safety clarification.
            SettingsTypography.Description(listing, T("GizmoDefaultsScope"));
            listing.Gap(16f);
            SettingsTypography.FeatureToggle(listing, T("GizmoDefaultReading"),
                ref settings.gizmoDefaultReading);
            listing.Gap(8f);
            SettingsTypography.FeatureToggle(listing, T("GizmoDefaultWake"),
                ref settings.gizmoDefaultWake);
            listing.Gap(8f);
            SettingsTypography.FeatureToggle(listing, T("GizmoDefaultFood"),
                ref settings.gizmoDefaultFood);
            SettingsTypography.Description(listing, T("GizmoDefaultFoodHint"));
        }

        internal static void DrawAlerts(Listing_Standard listing, ChildLearningSettings Settings)
        {
            // The tab already says 'Reading alerts'. Avoid repeating the
            // tab title and global-policy documentation above every option.

            // Stage 3J-2: treat visibility and position as distinct settings.
            // Preserve the last-selected position when hiding the icon.
            // These are existing persisted fields: no save migration needed.
            Settings.readingIconPosition = Mathf.Clamp(
                Settings.readingIconPosition, 0, 5);

            SettingsTypography.FeatureToggle(
                listing,
                T("ShowReadingIcon"),
                ref Settings.showReadingIcon
            );

            // UI6: OFF hides the entire preview card (not only its icon),
            // and no preview-sized empty space is reserved in the listing.
            if (Settings.showReadingIcon)
            {
                // The preview belongs to the icon position/size group and
                // scrolls with it. The rest of the layout is unchanged.
                Rect iconBlockStart = listing.GetRect(0f);
                float previewWidth = Mathf.Min(226f,
                    Mathf.Max(182f, iconBlockStart.width * 0.27f));
                const float minimumSideWidth = 580f;
                bool sidePreview = iconBlockStart.width >= minimumSideWidth;

                // UI3: placement first, size second; persisted fields unchanged.
                listing.Gap(10f);
                listing.Label(T("ReadingIconPosition"));
                listing.Gap(2f);

                if (SettingsTypography.RadioChoice(listing, T("PositionTopRight"),
                    Settings.readingIconPosition == 0))
                    Settings.readingIconPosition = 0;
                if (SettingsTypography.RadioChoice(listing, T("PositionTopLeft"),
                    Settings.readingIconPosition == 1))
                    Settings.readingIconPosition = 1;
                if (SettingsTypography.RadioChoice(listing, T("PositionBottomRight"),
                    Settings.readingIconPosition == 2))
                    Settings.readingIconPosition = 2;
                if (SettingsTypography.RadioChoice(listing, T("PositionBottomLeft"),
                    Settings.readingIconPosition == 3))
                    Settings.readingIconPosition = 3;
                if (SettingsTypography.RadioChoice(listing, T("PositionCenter"),
                    Settings.readingIconPosition == 4))
                    Settings.readingIconPosition = 4;
                if (SettingsTypography.RadioChoice(listing, T("PositionLowerCenter"),
                    Settings.readingIconPosition == 5))
                    Settings.readingIconPosition = 5;

                listing.Gap(10f);
                listing.Label(T("LargeReadingIcon"));
                listing.Gap(2f);
                if (SettingsTypography.RadioChoice(listing, T("ReadingIconSizeNormal"),
                    !Settings.largeReadingIcon))
                    Settings.largeReadingIcon = false;
                if (SettingsTypography.RadioChoice(listing, T("ReadingIconSizeLarge"),
                    Settings.largeReadingIcon))
                    Settings.largeReadingIcon = true;

                if (sidePreview)
                {
                    Rect iconBlockEnd = listing.GetRect(0f);
                    float consumed = iconBlockEnd.y - iconBlockStart.y;
                    float needed = ReadingIconSettingsPreview.PanelHeight + 14f;
                    if (consumed < needed)
                        listing.Gap(needed - consumed);

                    float panelX = Mathf.Min(iconBlockStart.x + 395f,
                        iconBlockStart.xMax - previewWidth - 8f);
                    Rect rightPanel = new Rect(panelX, iconBlockStart.y + 10f,
                        previewWidth, ReadingIconSettingsPreview.PanelHeight);
                    ReadingIconSettingsPreview.Draw(rightPanel, Settings);
                }
                else
                {
                    listing.Gap(10f);
                    Rect slot = listing.GetRect(ReadingIconSettingsPreview.PanelHeight);
                    float width = Mathf.Min(previewWidth, slot.width);
                    Rect compactPanel = new Rect(slot.center.x - width * 0.5f,
                        slot.y, width, ReadingIconSettingsPreview.PanelHeight);
                    ReadingIconSettingsPreview.Draw(compactPanel, Settings);
                }
            }

            SettingsTypography.GroupDivider(listing);
            SettingsTypography.FeatureToggle(
                listing,
                T("PlayReadingSound"),
                ref Settings.playReadingSound
            );

            if (Settings.playReadingSound)
            {
                Settings.readingSoundChoice =
                    ReadingSoundUtility.NormalizeChoice(
                        Settings.readingSoundChoice
                    );

                listing.Gap(10f);
                listing.Label(T("NotificationSound"));
                listing.Gap(2f);

                // AL4: five choices in fixed display order; persisted IDs unchanged.
                foreach (ReadingSoundOption option in ReadingSoundUtility.AvailableOptions())
                {
                    if (SettingsTypography.RadioChoice(listing,
                        option.Label.Translate().ToString(),
                        Settings.readingSoundChoice == option.Id))
                    {
                        Settings.readingSoundChoice = option.Id;
                    }
                }

                listing.Gap(8f);

                if (listing.ButtonText(
                        T("PreviewSound"),
                        widthPct: 0.35f))
                {
                    ReadingSoundUtility.Play(
                        Settings.readingSoundChoice
                    );
                }

            }

            SettingsTypography.GroupDivider(listing);
            SettingsTypography.FeatureToggle(
                listing,
                T("ShowReadingMessage"),
                ref Settings.showReadingActivationMessage
            );

        }

        internal static void DrawLearn(Listing_Standard listing, ChildLearningSettings Settings)
        {
            // The second tab already says 'Child Learning'.

            // Stage 4P-UI1A: opt-in child learning percentage in the
            // colonist-bar name slot. Does not alter the pawn's saved name.
            // Can be used with or without the Reading desire icon.
            SettingsTypography.FeatureToggle(
                listing,
                T("ShowLearningPercent"),
                ref Settings.showChildLearningPercent
            );

            // UI10: 100/125/150 display sizes; the new 100% is the former
            // 125% physical size. Keep the control behind the main toggle.
            if (Settings.showChildLearningPercent)
            {
                Settings.childLearningNumberSizePercent =
                    LearningColonistBarScale.Normalize(
                        Settings.childLearningNumberSizePercent);

                listing.Gap(10f);
                listing.Label(
                    T("LearningNumberSize") +
                    Settings.childLearningNumberSizePercent + "%" +
                    (Settings.childLearningNumberSizePercent == 100 ? " " + T("DefaultSuffix") : "")
                );
                float sizeSliderValue = listing.Slider(
                    Settings.childLearningNumberSizePercent,
                    LearningColonistBarScale.MinPercent,
                    LearningColonistBarScale.MaxPercent
                );
                Settings.childLearningNumberSizePercent =
                    LearningColonistBarScale.Normalize(
                        Mathf.RoundToInt(sizeSliderValue));

                // UI1B/UI3-Fix2: tint only the child numeric label on real
                // Learning gain or an active positive Learn() call at the cap.
                listing.Gap(12f);
                SettingsTypography.FeatureToggle(
                    listing,
                    T("HighlightLearning"),
                    ref Settings.highlightActiveChildLearning
                );

                // UI1B-Palette: name-labeled choices, no color perception
                // required to configure the setting. Preserve selection
                // even when highlighting is switched OFF.
                if (Settings.highlightActiveChildLearning)
                {
                    Settings.activeChildLearningHighlightColor =
                        LearningHighlightPalette.Normalize(
                            Settings.activeChildLearningHighlightColor);
                    // UI9: anchor the sample next to the palette, like
                    // the book-icon preview next to its position options.
                    Rect highlightBlockStart = listing.GetRect(0f);
                    float previewWidth = Mathf.Min(192f,
                        Mathf.Max(160f, highlightBlockStart.width * 0.23f));
                    bool sideHighlightPreview = highlightBlockStart.width >= 580f;

                    listing.Gap(10f);
                    listing.Label(T("HighlightColor"));
                    listing.Gap(2f);

                    if (SettingsTypography.RadioChoice(listing, T("ColorYellow"),
                            Settings.activeChildLearningHighlightColor == LearningHighlightPalette.Yellow))
                        Settings.activeChildLearningHighlightColor = LearningHighlightPalette.Yellow;
                    if (SettingsTypography.RadioChoice(listing, T("ColorCyan"), 
                            Settings.activeChildLearningHighlightColor == LearningHighlightPalette.Cyan))
                        Settings.activeChildLearningHighlightColor = LearningHighlightPalette.Cyan;
                    if (SettingsTypography.RadioChoice(listing, T("ColorMagenta"), 
                            Settings.activeChildLearningHighlightColor == LearningHighlightPalette.Magenta))
                        Settings.activeChildLearningHighlightColor = LearningHighlightPalette.Magenta;
                    if (SettingsTypography.RadioChoice(listing, T("ColorOrange"), 
                            Settings.activeChildLearningHighlightColor == LearningHighlightPalette.Orange))
                        Settings.activeChildLearningHighlightColor = LearningHighlightPalette.Orange;
                    if (SettingsTypography.RadioChoice(listing, T("ColorBlue"), 
                            Settings.activeChildLearningHighlightColor == LearningHighlightPalette.Blue))
                        Settings.activeChildLearningHighlightColor = LearningHighlightPalette.Blue;

                    if (sideHighlightPreview)
                    {
                        Rect blockEnd = listing.GetRect(0f);
                        float consumed = blockEnd.y - highlightBlockStart.y;
                        float needed = LearningHighlightSettingsPreview.PanelHeight + 12f;
                        if (consumed < needed)
                            listing.Gap(needed - consumed);

                        float panelX = Mathf.Min(highlightBlockStart.x + 395f,
                            highlightBlockStart.xMax - previewWidth - 8f);
                        Rect preview = new Rect(panelX, highlightBlockStart.y + 10f,
                            previewWidth, LearningHighlightSettingsPreview.PanelHeight);
                        LearningHighlightSettingsPreview.Draw(preview, Settings);
                    }
                    else
                    {
                        listing.Gap(8f);
                        Rect slot = listing.GetRect(LearningHighlightSettingsPreview.PanelHeight);
                        float width = Mathf.Min(previewWidth, slot.width);
                        Rect preview = new Rect(slot.center.x - width * 0.5f,
                            slot.y, width, LearningHighlightSettingsPreview.PanelHeight);
                        LearningHighlightSettingsPreview.Draw(preview, Settings);
                    }
                }
            }
            listing.Gap(8f);
            SettingsTypography.FeatureToggle(listing,
                T("GrowthGizmoLearningDesires"),
                ref Settings.showLearningDesireIcons);
            SettingsTypography.GroupDivider(listing);
            SettingsTypography.FeatureToggle(listing,
                T("AutoWakeToggle"),
                ref Settings.autoWakeChildren);
            if (Settings.autoWakeChildren)
            {
                listing.Gap(10f);
                DrawTenthsField(listing, T("WakeLearningThreshold"),
                    ref Settings.autoWakeLearningTenths, 1000, ref learningFieldBuffer,
                    "AutoWakeDescription");
                listing.Gap(8f);
                DrawTenthsField(listing, T("MinimumRestThreshold"),
                    ref Settings.autoWakeMinimumRestTenths, 1000, ref restFieldBuffer,
                    "MinimumRestDescription", ChildLearningSettings.MinWakeRestTenths);
                listing.Gap(8f);
                DrawTenthsField(listing, T("WakeCooldown"),
                    ref Settings.autoWakeCooldownTenthsHours, 240, ref cooldownFieldBuffer,
                    "WakeCooldownDescription");
                listing.Gap(10f);
                DrawWakeSummary(listing, Settings);
            }

            SettingsTypography.GroupDivider(listing);
            SettingsTypography.FeatureToggle(listing,
                T("AutoEatToggle"),
                ref Settings.autoEatAtJobTransition);
            if (Settings.autoEatAtJobTransition ||
                (Settings.autoWakeChildren && Settings.autoEatAfterWake))
            {
                listing.Gap(10f);
                DrawTenthsField(listing, T("EatFoodThreshold"),
                    ref Settings.autoEatFoodTenths, 1000, ref foodFieldBuffer,
                    minTenths: ChildLearningSettings.MinAutoEatFoodTenths);
                if (Settings.autoEatAtJobTransition)
                {
                    listing.Gap(10f);
                    DrawFoodSummary(listing, Settings);
                }
            }
            if (Settings.autoWakeChildren && !Settings.autoEatAtJobTransition)
            {
                listing.Gap(6f);
                SettingsTypography.FeatureToggle(listing,
                    T("LegacyEatToggle"),
                    ref Settings.autoEatAfterWake);
            }
            SettingsTypography.GroupDivider(listing);
            SettingsTypography.FeatureToggle(listing, T("DebugLogging"), ref Settings.enableDebugLogging);
        }
    }
}
