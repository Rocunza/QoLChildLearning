using Verse;

namespace ZCN.QoL.ChildLearning
{
    // New independent configuration. Old combined settings are intentionally not read.
    public sealed class ChildLearningSettings : ModSettings
    {
        // Supported ranges for the two child-learning management thresholds.
        internal const int MinWakeRestTenths = 380; // 38.0%
        internal const int MinAutoEatFoodTenths = 340; // 34.0%
        public bool showChildLearningPercent = true;
        public bool showLearningDesireIcons = true; // Vanilla growth-gizmo desire overlay.
        public int childLearningNumberSizePercent = 100;
        public bool highlightActiveChildLearning = true;
        public int activeChildLearningHighlightColor = LearningHighlightPalette.Default;
        public bool showReadingIcon = true;
        public bool largeReadingIcon = false;
        public int readingIconPosition = 0;
        public bool playReadingSound = true;
        public int readingSoundChoice = ReadingSoundUtility.DefaultChoice;
        public bool showReadingActivationMessage = true;
        public bool autoWakeChildren = true;
        public int autoWakeLearningTenths = 899;
        public int autoWakeMinimumRestTenths = 500; // 38.0% to 100.0%.
        public int autoWakeCooldownTenthsHours = 10; // 0 = no time cooldown.
        public bool autoEatAfterWake = false;
        public bool autoEatAtJobTransition = true;
        public int autoEatFoodTenths = 550; // 34.0% to 100.0%, default 55.0%.
        // New/unregistered children use these defaults. Existing children keep
        // their saved per-child gizmo switches; this is not a global override.
        public bool gizmoDefaultReading = true;
        public bool gizmoDefaultWake = false;
        public bool gizmoDefaultFood = false;
        public bool enableDebugLogging = false;

        public override void ExposeData()
        {
            base.ExposeData();
            ChildLearningSettingsData.Expose(this);
            Scribe_Values.Look(ref enableDebugLogging, "enableDebugLogging", false);
        }
    }
}
