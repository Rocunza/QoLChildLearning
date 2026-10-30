using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ZCN.QoL.ChildLearning
{
    /// <summary>
    /// Limit our label change to the duration of one colonist-bar pawn draw.
    /// We do not modify pawn names, the normal label renderer, or other UI.
    /// </summary>
    [HarmonyPatch(typeof(ColonistBarColonistDrawer), "DrawColonist")]
    internal static class LearningColonistBarLabelScope
    {
        // Unity IMGUI is drawn on the UI thread. ThreadStatic additionally
        // prevents a future non-UI caller from inheriting the drawing scope.
        [ThreadStatic]
        private static Pawn drawingPawn;

        // Keep both numeric and temporary vanilla-name draws scoped to the
        // exact child bar entry. The click switch never changes pawn.Name.
        internal static bool InEligibleScope(Pawn pawn)
        {
            return pawn != null && ReferenceEquals(drawingPawn, pawn) &&
                   ChildLearningLabelToggle.Eligible(pawn);
        }

        internal static bool IsTarget(Pawn pawn)
        {
            return InEligibleScope(pawn) &&
                   !ChildLearningLabelToggle.ShowName(pawn);
        }

        // UI1A-3: allow the deferred label renderer to retain precisely
        // the same child-only numeric-label scope as DrawColonist.
        // No pawn name, selection state, or other UI is changed.
        internal static Pawn PushDeferredScope(Pawn pawn)
        {
            Pawn previous = drawingPawn;
            drawingPawn = pawn;
            return previous;
        }

        internal static void RestoreDeferredScope(Pawn previous)
        {
            drawingPawn = previous;
        }

        [HarmonyPrefix]
        private static void Prefix(Pawn colonist, out Pawn __state)
        {
            // Keep the old scope if a mod nests colonist-bar rendering.
            __state = drawingPawn;
            drawingPawn = colonist;
        }

        [HarmonyPostfix]
        private static void Postfix(Pawn __state)
        {
            drawingPawn = __state;
        }

        [HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception, Pawn __state)
        {
            // Also restore on exceptions before the normal postfix completes.
            drawingPawn = __state;
            return __exception;
        }
    }

    /// <summary>
    /// The game computes both the label's width and its text with this private
    /// helper. Replace the returned string only inside our bar-drawing scope;
    /// vanilla keeps drawing its own background, health bar, and label.
    /// </summary>
    [HarmonyPatch]
    internal static class Patch_ColonistBar_LearningPercentLabel
    {
        [HarmonyTargetMethod]
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(GenMapUI),
                "GetPawnLabel",
                new Type[]
                {
                    typeof(Pawn),
                    typeof(float),
                    typeof(Dictionary<string, string>),
                    typeof(GameFont)
                });
        }

        [HarmonyPrefix]
        private static bool Prefix(Pawn pawn, ref string __result)
        {
            if (!LearningColonistBarLabelScope.IsTarget(pawn))
            {
                return true;
            }

            float fraction = pawn.needs.learning.CurLevelPercentage;
            if (float.IsNaN(fraction) || float.IsInfinity(fraction))
            {
                return true;
            }

            // Preserve floor rounding at ordinary learning values: 89.9%
            // must NOT appear as 90% (vanilla auto-learning starts below 90%).
            // Exception at the top: vanilla considers Learning satisfied at
            // >= 99.9%. Its NeedInterval periodically drops 100% to ~99.955%
            // even during uninterrupted, forced reading; flooring that value
            // would visibly flash 99 / 100 every interval. Snap only that
            // final 0.1% band to a displayed 100. Never change CurLevel.
            int percent = fraction >= 0.999f
                ? 100
                : Mathf.Clamp(
                    Mathf.FloorToInt(Mathf.Clamp01(fraction) * 100f),
                    0,
                    100);

            __result = percent.ToString(CultureInfo.InvariantCulture);
            return false;
        }
    }
}
