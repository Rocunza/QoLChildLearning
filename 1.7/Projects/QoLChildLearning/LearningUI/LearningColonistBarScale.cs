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
    /// RW17-UI2: keep the vanilla colonist-bar label coordinates unchanged.
    /// RimWorld 1.7's Waylith IMGUI compatibility layer can shift child label
    /// positions when GUIUtility.ScaleAroundPivot changes GUI.matrix.
    /// Instead enlarge ONLY the Learning number's font at the moment vanilla
    /// GenMapUI.DrawPawnLabel calls Widgets.Label. The pawn portrait, gray
    /// background, health bar, drafted underline, other text, and hit testing
    /// remain in the original GUI coordinate space.
    /// </summary>
    internal static class LearningColonistBarScale
    {
        internal const int DefaultPercent = 100;
        internal const int MinPercent = 100;
        internal const int MaxPercent = 150;
        internal const int StepPercent = 25;

        internal static int Normalize(int value)
        {
            int step = Mathf.Clamp(
                Mathf.RoundToInt((value - MinPercent) / (float)StepPercent),
                0,
                (MaxPercent - MinPercent) / StepPercent);
            return MinPercent + step * StepPercent;
        }

        // Display 100/125/150 maps to the 1.6 final physical 125/156/187%.
        internal static float GuiScale(int displayedPercent)
        {
            return 1.25f * Normalize(displayedPercent) / 100f;
        }

        // Keep the existing V2 settings migration (saves use this method).
        internal static int FromLegacyPercent(int legacyPercent)
        {
            legacyPercent = Mathf.Clamp(legacyPercent, 75, 150);
            return legacyPercent >= 150 ? 125 : 100;
        }
    }

    /// <summary>
    /// Scope a style override to one child colonist-bar DrawPawnLabel(Rect).
    /// Retain the newer 1.6 displayed choices while avoiding GUI.matrix
    /// and shared Text.CurFontStyle mutations.
    /// </summary>
    [HarmonyPatch]
    internal static class Patch_ColonistBar_LearningLabelScale
    {
        [ThreadStatic] private static int activePercent;
        [ThreadStatic] private static bool firstStyledDrawLogged;

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
        private static void Prefix(Pawn pawn, out int __state)
        {
            // Restore even on exceptions, and support nested label rendering.
            __state = activePercent;
            activePercent = 0;
            if (!LearningColonistBarLabelScope.IsTarget(pawn))
                return;

            ChildLearningSettings settings = ChildLearningMod.Settings;
            if (settings == null)
                return;

            int percent = LearningColonistBarScale.Normalize(
                settings.childLearningNumberSizePercent);
            // 1.6 final UI10 makes displayed 100% physically 125%.
            // Carry that design forward with font-only enlargement.
            activePercent = percent;
        }

        [HarmonyPostfix]
        private static void Postfix(int __state)
        {
            activePercent = __state;
        }

        [HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception, int __state)
        {
            activePercent = __state;
            return __exception;
        }

        internal static bool TryDrawLearningNumber(Rect rect, string label)
        {
            int percent = activePercent;
            if (percent == 0 || string.IsNullOrEmpty(label) || label.Length > 3)
                return false;

            // No unrelated name or localized UI string must be redrawn.
            for (int i = 0; i < label.Length; ++i)
            {
                if (label[i] < '0' || label[i] > '9')
                    return false;
            }

            // Work with a private copy: don't change Text.CurFontStyle.fontSize
            // globally (some render backends defer style consumption).
            GUIStyle original = Text.CurFontStyle;
            if (original == null)
                return false;

            GUIStyle numberStyle = new GUIStyle(original);
            int baseSize = original.fontSize;
            if (baseSize <= 0)
                baseSize = Text.Font == GameFont.Tiny ? 12 : 14;
            float ratio = LearningColonistBarScale.GuiScale(percent);
            numberStyle.fontSize = Mathf.Max(1, Mathf.RoundToInt(baseSize * ratio));

            // Keep the original label's exact center. A wider text-only Rect
            // prevents 100 (3 digits) from being clipped at 150% font size.
            // We never scale GUI coordinates or shift neighboring portraits.
            float newWidth = rect.width * Mathf.Max(1f, ratio);
            Rect numberRect = new Rect(rect.center.x - newWidth / 2f,
                rect.y, newWidth, rect.height);
            GUI.Label(numberRect, label, numberStyle);

            if (!firstStyledDrawLogged && Event.current != null &&
                Event.current.type == EventType.Repaint)
            {
                firstStyledDrawLogged = true;
                Log.Message("[QoL: Child Learning][RW17-UI2] Learning number " +
                    "uses font-size scaling without GUI.matrix; percent=" +
                    percent + "; baseFontSize=" + baseSize +
                    "; adjustedFontSize=" + numberStyle.fontSize + ".");
            }
            return true;
        }
    }

    /// <summary>
    /// Widgets.Label is the leaf call used by the vanilla pawn label renderer.
    /// Outside the scoped child number paint, this prefix is a no-op.
    /// Returning false skips ONLY that one numeric label, while preserving
    /// vanilla background/health/draft indicator rendering around it.
    /// </summary>
    [HarmonyPatch(typeof(Widgets), "Label", new Type[] { typeof(Rect), typeof(string) })]
    internal static class Patch_ColonistBar_LearningNumberFontOnly
    {
        [HarmonyPrefix]
        private static bool Prefix(Rect rect, string label)
        {
            return !Patch_ColonistBar_LearningLabelScale.TryDrawLearningNumber(rect, label);
        }
    }
}
