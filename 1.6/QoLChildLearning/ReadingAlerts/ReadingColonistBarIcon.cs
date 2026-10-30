using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ZCN.QoL.ChildLearning
{
    [HarmonyPatch(
        typeof(ColonistBarColonistDrawer),
        "DrawIcons"
    )]
    public static class Patch_ColonistBar_ReadingIcon
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(
            Rect rect,
            Pawn colonist)
        {
            // -----------------------------------------------------
            // Setting disabled
            // -----------------------------------------------------

            if (ChildLearningMod.Settings == null ||
                !ChildLearningMod.Settings.showReadingIcon ||
                !ChildGizmoPolicy.ReadingEnabled(colonist))
            {
                return;
            }


            // -----------------------------------------------------
            // Reading desire not active
            // -----------------------------------------------------

            if (!ReadingLearningUtility.HasReadingDesire(
                    colonist))
            {
                return;
            }


            // -----------------------------------------------------
            // Draw the vanilla Reading learning-desire icon.
            //
            // Existing vanilla activity icons occupy the lower part
            // of the colonist portrait, so this is placed at the
            // upper-right corner to avoid fighting for the same row.
            // -----------------------------------------------------

            float iconSize = ReadingIconSizing.GetSize(
                rect,
                Find.ColonistBar.Scale,
                ChildLearningMod.Settings.largeReadingIcon);
            if (iconSize <= 0f)
            {
                return;
            }

            // UI4: share the exact pre-existing position calculation with
            // the settings preview; the pawn/Reading-desire guards above
            // and the drawn texture/tooltip below are unchanged.
            Rect iconRect = ReadingIconSizing.GetIconRect(
                rect, iconSize, Find.ColonistBar.Scale,
                ChildLearningMod.Settings.readingIconPosition);

            Widgets.DrawTextureFitted(
                iconRect,
                LearningDesireDefOf.Reading.Icon,
                1f
            );


            // -----------------------------------------------------
            // Tooltip
            // -----------------------------------------------------

            TooltipHandler.TipRegion(
                iconRect,
                "RAL_ReadingIconTooltip".Translate().ToString()
            );
        }
    }
}