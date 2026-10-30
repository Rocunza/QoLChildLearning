using HarmonyLib;
using UnityEngine;
using Verse;

namespace ZCN.QoL.ChildLearning
{
    public sealed class ChildLearningMod : Mod
    {
        private static string T(string key)
        {
            return ("ZCNCL_" + key).Translate().ToString();
        }

        public static ChildLearningSettings Settings { get; private set; }
        private int selectedTab; // 0=reading alerts, 1=learning, 2=gizmo defaults
        private Vector2 alertsScroll;
        private Vector2 learningScroll;
        private Vector2 gizmoScroll;
        private string message = string.Empty;

        public ChildLearningMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<ChildLearningSettings>();
            Log.Message("[QoL: Child Learning] BUILD_ID=CHILD-LEARNING-RW16-PREPORT-REFACTOR-R1-20261029; assembly=QoLChildLearning;");
#if DEBUG
            ChildWakeSelfTests.Run();
            ChildIndependentFood.SelfTest();
#endif
            var harmony = new Harmony("zcn.qol.childlearning");
            harmony.PatchAll();
            ChildIndependentFood.Install(harmony);
#if DEBUG
            ChildDraftFoodTrace.Install(harmony);
#endif
            Log.Message("[QoL: Child Learning] Learning, wake, food, reading-desire UI/alert patches installed.");
        }

        public override string SettingsCategory() { return "QoL: Child Learning"; }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            const float tabHeight = 30f;
            const float spacing = 6f;
            float tabWidth = (inRect.width - 2f * spacing) / 3f;
            Rect alertsTab = new Rect(inRect.x, inRect.y, tabWidth, tabHeight);
            Rect learningTab = new Rect(inRect.x + tabWidth + spacing, inRect.y, tabWidth, tabHeight);
            Rect gizmoTab = new Rect(inRect.x + (tabWidth + spacing) * 2f, inRect.y, tabWidth, tabHeight);
            if (selectedTab == 0) Widgets.DrawHighlight(alertsTab);
            else if (selectedTab == 1) Widgets.DrawHighlight(learningTab);
            else Widgets.DrawHighlight(gizmoTab);
            if (Widgets.ButtonText(alertsTab, T("TabReadingAlerts"))) { selectedTab = 0; message = string.Empty; }
            if (Widgets.ButtonText(learningTab, T("TabLearning"))) { selectedTab = 1; message = string.Empty; }
            if (Widgets.ButtonText(gizmoTab, T("TabGizmoDefaults"))) { selectedTab = 2; message = string.Empty; }

            const float footerHeight = 29f;
            float footerY = inRect.yMax - footerHeight;
            float scrollTop = inRect.y + tabHeight + 8f;
            Rect scrollRect = new Rect(inRect.x, scrollTop, inRect.width,
                Mathf.Max(10f, footerY - 40f - scrollTop));
            Rect view = new Rect(0f, 0f, Mathf.Max(10f, scrollRect.width - 20f),
                selectedTab == 0 ? 850f : selectedTab == 1 ? 1430f : 300f);
            Vector2 scroll = selectedTab == 0 ? alertsScroll : selectedTab == 1 ? learningScroll : gizmoScroll;
            Widgets.BeginScrollView(scrollRect, ref scroll, view);
            try
            {
                var listing = new Listing_Standard();
                listing.Begin(view);
                try
                {
                    if (selectedTab == 0) ChildLearningSettingsUI.DrawAlerts(listing, Settings);
                    else if (selectedTab == 1) ChildLearningSettingsUI.DrawLearn(listing, Settings);
                    else ChildLearningSettingsUI.DrawGizmoDefaults(listing, Settings);
                }
                finally { listing.End(); }
            }
            finally { Widgets.EndScrollView(); Text.Font = GameFont.Small; }
            if (selectedTab == 0) alertsScroll = scroll;
            else if (selectedTab == 1) learningScroll = scroll;
            else gizmoScroll = scroll;

            float w = (inRect.width - spacing) * 0.5f;
            if (!string.IsNullOrEmpty(message))
                Widgets.Label(new Rect(inRect.x + 6f, footerY - 32f, inRect.width - 12f, 24f), message);
            Widgets.DrawLineHorizontal(inRect.x, footerY - 7f, inRect.width);
            if (selectedTab == 2)
            {
                // Defaults affect FUTURE children only; do not expose an
                // ambiguous "Disable All" that might imply existing children.
                if (Widgets.ButtonText(new Rect(inRect.x, footerY, inRect.width, footerHeight),
                    T("RestoreGizmoDefaults")))
                {
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        T("ConfirmRestoreGizmoDefaults"), delegate
                        {
                            ChildLearningSettingsPresets.RestoreGizmoDefaults(Settings);
                            WriteSettings(); message = T("GizmoDefaultsRestored");
                        }, false));
                }
            }
            else
            {
                bool alertTab = selectedTab == 0;
                if (Widgets.ButtonText(new Rect(inRect.x, footerY, w, footerHeight),
                    (alertTab ? T("RestoreAlertsDefaults") : T("RestoreLearningDefaults"))))
                {
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        T("ConfirmRestoreDefaults"), delegate
                        {
                            if (alertTab) ChildLearningSettingsPresets.RestoreAlertDefaults(Settings);
                            else ChildLearningSettingsPresets.RestoreLearnDefaults(Settings);
                            WriteSettings(); message = T("DefaultsRestored");
                        }, false));
                }
                if (Widgets.ButtonText(new Rect(inRect.x + w + spacing, footerY, w, footerHeight),
                    (alertTab ? T("DisableAlertsFeatures") : T("DisableLearningFeatures"))))
                {
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        T("ConfirmDisableFeatures"), delegate
                        {
                            if (alertTab) ChildLearningSettingsPresets.DisableAlertFeatures(Settings);
                            else ChildLearningSettingsPresets.DisableLearnFeatures(Settings);
                            WriteSettings(); message = T("FeaturesDisabled");
                        }, false));
                }
            }
            base.DoSettingsWindowContents(inRect);
        }
    }
}
