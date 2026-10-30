using Verse;

namespace ZCN.QoL.ChildLearning
{
    internal static class ChildLearningSettingsData
    {
        internal static void Expose(ChildLearningSettings s)
        {
            Scribe_Values.Look(ref s.showChildLearningPercent, "showChildLearningPercent", true);
            Scribe_Values.Look(ref s.showLearningDesireIcons, "showLearningDesireIconsV1", true);
            // UI10: new setting key prevents old 125% users from being
            // silently reinterpreted as the new 125% (which is larger).
            // Old 125% physically maps to the new 100% default.
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                int newPercent = -1;
                Scribe_Values.Look(ref newPercent, "childLearningNumberSizePercentV2", -1);
                if (newPercent >= 0)
                {
                    s.childLearningNumberSizePercent =
                        LearningColonistBarScale.Normalize(newPercent);
                }
                else
                {
                    int oldPercent = 125;
                    Scribe_Values.Look(ref oldPercent, "childLearningNumberSizePercent", 125);
                    s.childLearningNumberSizePercent =
                        LearningColonistBarScale.FromLegacyPercent(oldPercent);
                }
            }
            else if (Scribe.mode == LoadSaveMode.Saving)
            {
                s.childLearningNumberSizePercent =
                    LearningColonistBarScale.Normalize(s.childLearningNumberSizePercent);
                Scribe_Values.Look(ref s.childLearningNumberSizePercent,
                    "childLearningNumberSizePercentV2", 100);
            }
            Scribe_Values.Look(ref s.highlightActiveChildLearning, "highlightActiveChildLearning", true);
            Scribe_Values.Look(ref s.activeChildLearningHighlightColor, "activeChildLearningHighlightColor", LearningHighlightPalette.Default);
            s.activeChildLearningHighlightColor = LearningHighlightPalette.Normalize(s.activeChildLearningHighlightColor);
            s.childLearningNumberSizePercent = LearningColonistBarScale.Normalize(s.childLearningNumberSizePercent);

            Scribe_Values.Look(ref s.autoWakeChildren, "autoWakeChildren", true);
            Scribe_Values.Look(ref s.autoWakeLearningTenths, "autoWakeLearningTenths", 899);
            // GIZMO7: migrate out-of-range settings while retaining the same Scribe keys.
            // Normalize BEFORE saving so no obsolete 0% values get written to XML.
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                s.autoWakeMinimumRestTenths = System.Math.Max(ChildLearningSettings.MinWakeRestTenths,
                    ChildWakeRules.LimitTenths(s.autoWakeMinimumRestTenths, 1000));
                s.autoEatFoodTenths = s.autoEatFoodTenths == 0 ? 500 :
                    System.Math.Max(ChildLearningSettings.MinAutoEatFoodTenths,
                        ChildWakeRules.LimitTenths(s.autoEatFoodTenths, 1000));
            }
            Scribe_Values.Look(ref s.autoWakeMinimumRestTenths, "autoWakeMinimumRestTenths", 500);
            Scribe_Values.Look(ref s.autoWakeCooldownTenthsHours, "autoWakeCooldownTenthsHours", 10);
            s.autoWakeLearningTenths = ChildWakeRules.LimitTenths(s.autoWakeLearningTenths, 1000);
            s.autoWakeMinimumRestTenths = System.Math.Max(ChildLearningSettings.MinWakeRestTenths,
                ChildWakeRules.LimitTenths(s.autoWakeMinimumRestTenths, 1000));
            s.autoWakeCooldownTenthsHours = ChildWakeRules.LimitTenths(s.autoWakeCooldownTenthsHours, 240);
            Scribe_Values.Look(ref s.autoEatAfterWake, "autoEatAfterWake", false);
            Scribe_Values.Look(ref s.autoEatAtJobTransition, "autoEatAtJobTransition", true);
            Scribe_Values.Look(ref s.autoEatFoodTenths, "autoEatFoodTenths", 550);
            // Older EF1 versions interpreted 0% as 50%; preserve that effective
            // threshold for existing users. Legacy eat-after-wake 0% (unconditional)
            // is intentionally retired under the new minimum of 34%.
            s.autoEatFoodTenths = s.autoEatFoodTenths == 0 ? 500 :
                System.Math.Max(ChildLearningSettings.MinAutoEatFoodTenths,
                    ChildWakeRules.LimitTenths(s.autoEatFoodTenths, 1000));

            // GIZMO3: configurable defaults for newly enrolled children only.
            Scribe_Values.Look(ref s.gizmoDefaultReading, "gizmoDefaultReadingV1", true);
            Scribe_Values.Look(ref s.gizmoDefaultWake, "gizmoDefaultWakeV1", false);
            Scribe_Values.Look(ref s.gizmoDefaultFood, "gizmoDefaultFoodV1", false);

            Scribe_Values.Look(ref s.showReadingIcon, "showReadingIcon", true);
            Scribe_Values.Look(ref s.readingIconPosition, "readingIconPosition", 0);
            Scribe_Values.Look(ref s.largeReadingIcon, "largeReadingIcon", false);
            if (s.readingIconPosition < 0 || s.readingIconPosition > 5) s.readingIconPosition = 0;
            Scribe_Values.Look(ref s.playReadingSound, "playReadingSound", true);
            Scribe_Values.Look(ref s.readingSoundChoice, "readingSoundChoice", ReadingSoundUtility.DefaultChoice);
            s.readingSoundChoice = ReadingSoundUtility.NormalizeChoice(s.readingSoundChoice);
            Scribe_Values.Look(ref s.showReadingActivationMessage, "showReadingActivationMessage", true);
        }
    }
}
