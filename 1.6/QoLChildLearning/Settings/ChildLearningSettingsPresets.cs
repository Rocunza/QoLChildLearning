namespace ZCN.QoL.ChildLearning
{
    internal static class ChildLearningSettingsPresets
    {
        internal static void RestoreGizmoDefaults(ChildLearningSettings s)
        {
            if (s == null) return;
            s.gizmoDefaultReading = true;
            s.gizmoDefaultWake = false;
            s.gizmoDefaultFood = false;
            // Deliberately does not modify any pawn's saved toggle state.
        }

        internal static void RestoreAlertDefaults(ChildLearningSettings s)
        {
            if (s == null) return;
            var d = new ChildLearningSettings();
            s.showReadingIcon = d.showReadingIcon;
            s.largeReadingIcon = d.largeReadingIcon;
            s.readingIconPosition = d.readingIconPosition;
            s.playReadingSound = d.playReadingSound;
            s.readingSoundChoice = d.readingSoundChoice;
            s.showReadingActivationMessage = d.showReadingActivationMessage;
        }
        internal static void DisableAlertFeatures(ChildLearningSettings s)
        {
            if (s == null) return;
            s.showReadingIcon = false;
            s.playReadingSound = false;
            s.showReadingActivationMessage = false;
        }
        internal static void RestoreLearnDefaults(ChildLearningSettings s)
        {
            if (s == null) return;
            var d = new ChildLearningSettings();
            s.showChildLearningPercent = d.showChildLearningPercent;
            s.showLearningDesireIcons = d.showLearningDesireIcons;
            s.childLearningNumberSizePercent = d.childLearningNumberSizePercent;
            s.highlightActiveChildLearning = d.highlightActiveChildLearning;
            s.activeChildLearningHighlightColor = d.activeChildLearningHighlightColor;
            s.autoWakeChildren = d.autoWakeChildren;
            s.autoWakeLearningTenths = d.autoWakeLearningTenths;
            s.autoWakeMinimumRestTenths = d.autoWakeMinimumRestTenths;
            s.autoWakeCooldownTenthsHours = d.autoWakeCooldownTenthsHours;
            s.autoEatAfterWake = d.autoEatAfterWake;
            s.autoEatAtJobTransition = d.autoEatAtJobTransition;
            s.autoEatFoodTenths = d.autoEatFoodTenths;
            ChildLearningSettingsUI.ResetLearnInputBuffers();
        }
        internal static void DisableLearnFeatures(ChildLearningSettings s)
        {
            if (s == null) return;
            s.showChildLearningPercent = false;
            s.showLearningDesireIcons = false;
            s.highlightActiveChildLearning = false;
            s.autoWakeChildren = false;
            s.autoEatAfterWake = false;
            s.autoEatAtJobTransition = false;
        }
    }
}
