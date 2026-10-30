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
    /// UI1A-2: Scale only the existing child learning-label draw call.
    /// Keep the vanilla label background, health bar, and drafted underline;
    /// never change the pawn name or any shared GUIStyle/Font settings.
    /// </summary>
    internal static class LearningColonistBarScale
    {
        internal const int DefaultPercent = 100;
        internal const int MinPercent = 100;
        internal const int MaxPercent = 150;
        internal const int StepPercent = 25;

        // UI10: three DISPLAY choices: 100, 125, 150.
        // 100% intentionally retains the old 125% on-screen size.
        // The original 100% size was found too small for readability.
        internal static int Normalize(int value)
        {
            int step = Mathf.Clamp(
                Mathf.RoundToInt((value - MinPercent) / (float)StepPercent),
                0,
                (MaxPercent - MinPercent) / StepPercent);
            return MinPercent + step * StepPercent;
        }

        // Physical scale relative to vanilla label drawing.
        // Display 100% must match UI9's physical 125% default.
        internal static float GuiScale(int displayedPercent)
        {
            return 1.25f * Normalize(displayedPercent) / 100f;
        }

        // Legacy UI9 stored 75/100/125/150 in the old settings key.
        // The smallest new choice is old 125%, so earlier small sizes
        // cannot be preserved without retaining a fourth choice.
        internal static int FromLegacyPercent(int legacyPercent)
        {
            legacyPercent = Mathf.Clamp(legacyPercent, 75, 150);
            return legacyPercent >= 150 ? 125 : 100;
        }
    }

    /// <summary>
    /// The colonist bar calls the Rect overload of GenMapUI.DrawPawnLabel
    /// inside ColonistBarColonistDrawer.DrawColonist.  Scope is set by UI1A.
    /// Apply a temporary IMGUI transform ONLY for that label and restore it
    /// in both normal and exceptional paths. UI10 maps the new 100% to
    /// the previous on-screen 125% default, intentionally.
    /// </summary>
    [HarmonyPatch]
    internal static class Patch_ColonistBar_LearningLabelScale
    {
        private struct ScaleState
        {
            internal bool applied;
            internal Matrix4x4 previousMatrix;
        }

        [HarmonyTargetMethod]
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(GenMapUI),
                "DrawPawnLabel",
                new Type[]
                {
                    typeof(Pawn),
                    typeof(Rect),
                    typeof(float),
                    typeof(float),
                    typeof(Dictionary<string, string>),
                    typeof(GameFont),
                    typeof(bool),
                    typeof(bool)
                });
        }

        [HarmonyPrefix]
        private static void Prefix(Pawn pawn, Rect bgRect, out ScaleState __state)
        {
            __state = default(ScaleState);

            // IsTarget also checks the setting, child stage, alive state,
            // and that UI1A is drawing this exact colonist-bar pawn.
            if (!LearningColonistBarLabelScope.IsTarget(pawn))
            {
                return;
            }

            ChildLearningSettings settings = ChildLearningMod.Settings;
            int percent = LearningColonistBarScale.Normalize(
                settings.childLearningNumberSizePercent);

            float scale = LearningColonistBarScale.GuiScale(percent);
            if (Mathf.Approximately(scale, 1f))
                return;
            __state.previousMatrix = GUI.matrix;
            __state.applied = true;

            // Center horizontally and retain the label's top anchor.
            // This scales only the existing label/background as one group;
            // it does not touch the pawn portrait, UI input or game logic.
            GUIUtility.ScaleAroundPivot(
                new Vector2(scale, scale),
                new Vector2(bgRect.center.x, bgRect.y));
        }

        [HarmonyPostfix]
        private static void Postfix(ScaleState __state)
        {
            Restore(__state);
        }

        [HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception, ScaleState __state)
        {
            Restore(__state);
            return __exception;
        }

        private static void Restore(ScaleState state)
        {
            if (state.applied)
            {
                GUI.matrix = state.previousMatrix;
            }
        }
    }
}
