using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ZCN.QoL.ChildLearning
{
    /// <summary>
    /// Adds a small, right-aligned learning-desire panel immediately ABOVE
    /// the vanilla Biotech Gizmo_GrowthTier. The vanilla 190 x 75 growth
    /// gizmo, learning bar, input, and save data remain untouched.
    /// No RimHUD dependency.
    /// </summary>
    [HarmonyPatch(typeof(Gizmo_GrowthTier), "GizmoOnGUI")]
    public static class Patch_GrowthTier_DesiresPanel
    {
        private const float IconSize = 22f;
        private const float Gap = 3f;
        private const float Padding = 5f;
        private const float Separation = 3f;
        private const int MaximumRows = 3;

        // Field is private in the shipped RimWorld 1.6 Gizmo_GrowthTier.
        // Resolve once instead of using Traverse in every OnGUI call.
        private static readonly FieldInfo ChildField =
            AccessTools.Field(typeof(Gizmo_GrowthTier), "child");
        private static bool loggedError;

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(Gizmo_GrowthTier __instance, Vector2 topLeft, float maxWidth)
        {
            try
            {
                Draw(__instance, topLeft, maxWidth);
            }
            catch (Exception e)
            {
                // A rendering problem must not break the vanilla growth gizmo,
                // and it must not spam the log every GUI frame.
                if (!loggedError)
                {
                    loggedError = true;
                    Log.Error("[QoL: Child Learning][Growth desires] Overlay disabled for this draw: " + e);
                }
            }
        }

        private static void Draw(Gizmo_GrowthTier gizmo, Vector2 topLeft, float maxWidth)
        {
            var settings = ChildLearningMod.Settings;
            if (settings == null || !settings.showLearningDesireIcons ||
                !ModsConfig.BiotechActive || gizmo == null || ChildField == null)
                return;

            Pawn child = ChildField.GetValue(gizmo) as Pawn;
            if (child == null || child.learning == null ||
                child.needs == null || child.needs.learning == null ||
                !child.DevelopmentalStage.Child())
                return;

            var active = child.learning.ActiveLearningDesires;
            if (active == null)
                return;

            // Keep original RimWorld (or mod-added) LearningDesireDef icons.
            // Defs without icons cannot be displayed as original icons.
            var desires = new List<LearningDesireDef>();
            foreach (LearningDesireDef desire in active)
            {
                if (desire != null && desire.Icon != null && !desires.Contains(desire))
                    desires.Add(desire);
            }
            if (desires.Count == 0)
                return;

            float gizmoWidth = gizmo.GetWidth(maxWidth);
            if (gizmoWidth < IconSize + Padding * 2f)
                return;

            int columnsLimit = Mathf.Max(1,
                Mathf.FloorToInt((gizmoWidth - Padding * 2f + Gap) / (IconSize + Gap)));
            int capacity = columnsLimit * MaximumRows;
            bool overflow = desires.Count > capacity;
            int iconsShown = overflow ? capacity - 1 : desires.Count;
            int slots = iconsShown + (overflow ? 1 : 0);
            int columns = Mathf.Min(columnsLimit, slots);
            int rows = Mathf.CeilToInt(slots / (float)columnsLimit);

            float boxWidth = Padding * 2f + columns * IconSize + (columns - 1) * Gap;
            float boxHeight = Padding * 2f + rows * IconSize + (rows - 1) * Gap;
            Rect box = new Rect(topLeft.x + gizmoWidth - boxWidth,
                topLeft.y - boxHeight - Separation, boxWidth, boxHeight);

            // We must not cover the vanilla growth meter when a gizmo is
            // unexpectedly positioned at the top edge of the screen.
            if (box.y < 0f)
                return;

            GameFont originalFont = Text.Font;
            TextAnchor originalAnchor = Text.Anchor;
            Color originalColor = GUI.color;
            try
            {
                GUI.color = Color.white;
                Widgets.DrawWindowBackground(box);

                for (int i = 0; i < iconsShown; i++)
                {
                    LearningDesireDef desire = desires[i];
                    int row = i / columnsLimit;
                    int column = i % columnsLimit;
                    Rect iconRect = new Rect(box.x + Padding + column * (IconSize + Gap),
                        box.y + Padding + row * (IconSize + Gap), IconSize, IconSize);
                    Widgets.DrawTextureFitted(iconRect, desire.Icon, 1f);
                    TooltipHandler.TipRegion(iconRect, desire.LabelCap.ToString());
                }

                if (overflow)
                {
                    int index = iconsShown;
                    int row = index / columnsLimit;
                    int column = index % columnsLimit;
                    Rect remainderRect = new Rect(box.x + Padding + column * (IconSize + Gap),
                        box.y + Padding + row * (IconSize + Gap), IconSize, IconSize);
                    Text.Font = GameFont.Tiny;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(remainderRect, "+" + (desires.Count - iconsShown));

                    List<string> hidden = new List<string>();
                    for (int j = iconsShown; j < desires.Count; j++)
                        hidden.Add(desires[j].LabelCap.ToString());
                    TooltipHandler.TipRegion(remainderRect,
                        string.Join(", ", hidden.ToArray()));
                }
            }
            finally
            {
                Text.Font = originalFont;
                Text.Anchor = originalAnchor;
                GUI.color = originalColor;
            }
        }
    }
}
