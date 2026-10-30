using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ZCN.QoL.ChildLearning
{
    /// <summary>
    /// UI1A-3: Only a CHILD's optional Learning number is deferred until
    /// the end of the entire colonist bar repaint. In compact multi-row bars,
    /// a later portrait would otherwise cover the enlarged number of a pawn
    /// in an earlier row. Adult names and all names with the option OFF use
    /// the unmodified vanilla paint order.
    /// </summary>
    internal static class LearningColonistBarFront
    {
        private struct LabelDraw
        {
            internal Pawn pawn;
            internal Vector2 pos;
            internal float alpha;
            internal float truncateToWidth;
            internal Dictionary<string, string> labelsCache;
            internal GameFont font;
            internal bool alwaysDrawBg;
            internal bool alignCenter;
            internal Matrix4x4 originalMatrix;
        }

        [ThreadStatic] private static bool buffering;
        [ThreadStatic] private static List<LabelDraw> pending;

        internal static void Begin()
        {
            // Unity IMGUI draws the bar only in Repaint, so no extra label
            // layout work is done on input events.
            buffering = Event.current != null && Event.current.type == EventType.Repaint;
            if (pending != null)
                pending.Clear();
        }

        internal static bool TryQueue(
            Pawn pawn,
            Vector2 pos,
            float alpha,
            float truncateToWidth,
            Dictionary<string, string> truncatedLabelsCache,
            GameFont font,
            bool alwaysDrawBg,
            bool alignCenter)
        {
            if (!buffering || !LearningColonistBarLabelScope.IsTarget(pawn))
                return false;

            if (pending == null)
                pending = new List<LabelDraw>(16);

            pending.Add(new LabelDraw
            {
                pawn = pawn,
                pos = pos,
                alpha = alpha,
                truncateToWidth = truncateToWidth,
                labelsCache = truncatedLabelsCache,
                font = font,
                alwaysDrawBg = alwaysDrawBg,
                alignCenter = alignCenter,
                originalMatrix = GUI.matrix
            });
            return true;
        }

        internal static void EndAndDraw()
        {
            // Switch off interception BEFORE we call vanilla DrawPawnLabel.
            // Otherwise the deferred draw would enqueue itself recursively.
            bool wasBuffering = buffering;
            buffering = false;
            if (!wasBuffering || pending == null || pending.Count == 0)
                return;

            Matrix4x4 savedMatrix = GUI.matrix;
            Color savedColor = GUI.color;
            GameFont savedFont = Text.Font;
            TextAnchor savedAnchor = Text.Anchor;
            bool savedWordWrap = Text.WordWrap;

            try
            {
                for (int i = 0; i < pending.Count; ++i)
                {
                    LabelDraw item = pending[i];
                    Pawn priorScope = LearningColonistBarLabelScope.PushDeferredScope(item.pawn);
                    try
                    {
                        // Preserve any outer GUI transform used by the bar;
                        // the existing UI1A-2 75-150% scale patch still runs.
                        GUI.matrix = item.originalMatrix;
                        GenMapUI.DrawPawnLabel(
                            item.pawn,
                            item.pos,
                            item.alpha,
                            item.truncateToWidth,
                            item.labelsCache,
                            item.font,
                            item.alwaysDrawBg,
                            item.alignCenter);
                    }
                    catch (Exception e)
                    {
                        // Never let an optional overlay break the entire UI.
                        Log.ErrorOnce("[QoL: Child Learning] child numeric foreground rendering failed: " + e,
                            194816307);
                    }
                    finally
                    {
                        LearningColonistBarLabelScope.RestoreDeferredScope(priorScope);
                        GUI.matrix = savedMatrix;
                        GUI.color = savedColor;
                        Text.Font = savedFont;
                        Text.Anchor = savedAnchor;
                        Text.WordWrap = savedWordWrap;
                    }
                }
            }
            finally
            {
                pending.Clear();
                GUI.matrix = savedMatrix;
                GUI.color = savedColor;
                Text.Font = savedFont;
                Text.Anchor = savedAnchor;
                Text.WordWrap = savedWordWrap;
            }
        }

        internal static void Abort()
        {
            buffering = false;
            if (pending != null)
                pending.Clear();
        }
    }

    [HarmonyPatch(typeof(ColonistBar), "ColonistBarOnGUI")]
    internal static class Patch_ColonistBar_LearningNumberForegroundFlush
    {
        [HarmonyPrefix]
        private static void Prefix()
        {
            LearningColonistBarFront.Begin();
        }

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix()
        {
            LearningColonistBarFront.EndAndDraw();
        }

        [HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception)
        {
            // Handles failed original draws as well as successful postfixed ones.
            LearningColonistBarFront.Abort();
            return __exception;
        }
    }

    // The bar calls the Vector2 overload for names. Intercept only if the
    // child numeric setting is enabled and the exact pawn is being drawn
    // from ColonistBarColonistDrawer.DrawColonist. Standard names bypass it.
    [HarmonyPatch(typeof(GenMapUI), "DrawPawnLabel", new Type[]
    {
        typeof(Pawn),
        typeof(Vector2),
        typeof(float),
        typeof(float),
        typeof(Dictionary<string, string>),
        typeof(GameFont),
        typeof(bool),
        typeof(bool)
    })]
    internal static class Patch_ColonistBar_DeferredChildNumericLabel
    {
        [HarmonyPrefix]
        private static bool Prefix(
            Pawn pawn,
            Vector2 pos,
            float alpha,
            float truncateToWidth,
            Dictionary<string, string> truncatedLabelsCache,
            GameFont font,
            bool alwaysDrawBg,
            bool alignCenter)
        {
            return !LearningColonistBarFront.TryQueue(
                pawn, pos, alpha, truncateToWidth, truncatedLabelsCache,
                font, alwaysDrawBg, alignCenter);
        }
    }
}
