using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ZCN.QoL.ChildLearning
{
    /// <summary>
    /// UI3-Fix1: A child-only, session-only number/name switch for the colonist bar.
    /// The clickable rectangles are captured from the labels actually painted
    /// by GenMapUI. We never intercept the portrait itself and do not change
    /// Pawn.Name, pawn selection, saved games or existing mod-settings keys.
    /// </summary>
    internal static class ChildLearningLabelToggle
    {
        private struct HitRegion
        {
            internal Pawn pawn;
            internal Rect screenRect;
        }

        private static Game trackedGame;
        private static readonly HashSet<Pawn> NameModePawns = new HashSet<Pawn>();
        private static readonly List<HitRegion> Regions = new List<HitRegion>(16);
        private static bool diagnosticInstallationLogged;
        private static bool diagnosticInputHookLogged;
        private static int debugInputCount;

        private static void EnsureCurrentGame()
        {
            Game game = Current.Game;
            if (ReferenceEquals(trackedGame, game))
                return;

            // Switching/loading a game resets temporary per-child choices.
            // A session toggle is not part of the save or global mod settings.
            trackedGame = game;
            NameModePawns.Clear();
            Regions.Clear();
        }

        internal static bool Eligible(Pawn pawn)
        {
            ChildLearningSettings settings = ChildLearningMod.Settings;
            return settings != null && settings.showChildLearningPercent &&
                   ModsConfig.BiotechActive && Current.Game != null &&
                   pawn != null && !pawn.Dead &&
                   pawn.DevelopmentalStage.Child() &&
                   pawn.needs != null && pawn.needs.learning != null;
        }

        internal static bool ShowName(Pawn pawn)
        {
            EnsureCurrentGame();
            return pawn != null && NameModePawns.Contains(pawn);
        }

        // Called from the normal colonist-bar UI entry point on Repaint only.
        internal static void BeginRepaint()
        {
            EnsureCurrentGame();
            Regions.Clear();
            if (!diagnosticInputHookLogged && ChildLearningMod.Settings != null &&
                ChildLearningMod.Settings.enableDebugLogging)
            {
                diagnosticInputHookLogged = true;
                Log.Message("[QoL: Child Learning] UI3-Fix1 INPUT HOOK installed " +
                    "(ColonistBar.ColonistBarOnGUI).");
            }
        }

        /// <summary>
        /// Record a label in *screen-space* using the same conversion Unity
        /// uses for GUI input. Do NOT multiply GUI.matrix manually and then
        /// compare against raw Event.mousePosition: those are different
        /// coordinate spaces at non-default UI scales and 75%-150% labels.
        /// </summary>
        internal static void RememberPaintedLabel(Pawn pawn, Rect bgRect)
        {
            if (!Eligible(pawn))
                return;

            EnsureCurrentGame();
            Rect padded = new Rect(bgRect.x - 1f, bgRect.y - 2f,
                                   bgRect.width + 2f, bgRect.height + 4f);

            // Respect nested IMGUI transforms (UI scale + optional number
            // scale). The click point will be converted with this same API.
            Vector2 a = GUIUtility.GUIToScreenPoint(new Vector2(padded.xMin, padded.yMin));
            Vector2 b = GUIUtility.GUIToScreenPoint(new Vector2(padded.xMax, padded.yMin));
            Vector2 c = GUIUtility.GUIToScreenPoint(new Vector2(padded.xMin, padded.yMax));
            Vector2 d = GUIUtility.GUIToScreenPoint(new Vector2(padded.xMax, padded.yMax));
            float xMin = Mathf.Min(Mathf.Min(a.x, b.x), Mathf.Min(c.x, d.x));
            float xMax = Mathf.Max(Mathf.Max(a.x, b.x), Mathf.Max(c.x, d.x));
            float yMin = Mathf.Min(Mathf.Min(a.y, b.y), Mathf.Min(c.y, d.y));
            float yMax = Mathf.Max(Mathf.Max(a.y, b.y), Mathf.Max(c.y, d.y));
            if (float.IsNaN(xMin) || float.IsNaN(yMin) ||
                float.IsInfinity(xMin) || float.IsInfinity(yMin) ||
                float.IsNaN(xMax) || float.IsNaN(yMax) ||
                xMax <= xMin || yMax <= yMin)
                return;

            Regions.Add(new HitRegion
            {
                pawn = pawn,
                screenRect = Rect.MinMaxRect(xMin, yMin, xMax, yMax)
            });

            if (!diagnosticInstallationLogged && ChildLearningMod.Settings != null &&
                ChildLearningMod.Settings.enableDebugLogging)
            {
                diagnosticInstallationLogged = true;
                Log.Message("[QoL: Child Learning] UI3-Fix1 label-click patch ACTIVE; " +
                    "numeric/name hotspot captured in screen coordinates.");
            }
        }

        /// <summary>
        /// Called before the bar handles MouseDown. Only a *single left click*
        /// on a child label is consumed. The entire portrait, drag, right
        /// click, double click, and non-child bar behavior are vanilla.
        /// </summary>
        internal static bool TryConsumeLabelClick()
        {
            Event evt = Event.current;
            if (evt == null || evt.type != EventType.MouseDown ||
                evt.button != 0 || evt.clickCount != 1)
                return false;

            EnsureCurrentGame();
            if (Regions.Count == 0 || Mouse.IsInputBlockedNow)
                return false;

            // The rectangles and the mouse point are now BOTH in the same
            // Unity screen coordinates. In particular do not compare this
            // screenRect directly to evt.mousePosition (GUI coordinates).
            Vector2 clickScreenPoint = GUIUtility.GUIToScreenPoint(evt.mousePosition);
            for (int i = Regions.Count - 1; i >= 0; --i)
            {
                HitRegion candidate = Regions[i];
                if (!candidate.screenRect.Contains(clickScreenPoint) ||
                    !Eligible(candidate.pawn))
                    continue;

                bool switchedToName = NameModePawns.Add(candidate.pawn);
                if (!switchedToName)
                    NameModePawns.Remove(candidate.pawn);

                if (ChildLearningMod.Settings != null &&
                    ChildLearningMod.Settings.enableDebugLogging)
                {
                    Log.Message("[QoL: Child Learning] UI3-Fix1 CLICK: pawn=" +
                        candidate.pawn.LabelShortCap +
                        ", mode=" + (switchedToName ? "name" : "number") +
                        ", labelRegions=" + Regions.Count);
                }

                evt.Use();
                return true;
            }

            // Bounded trace to distinguish an input dispatch failure from
            // a coordinate mismatch. Only log clicks close to bar labels.
            if (ChildLearningMod.Settings != null &&
                ChildLearningMod.Settings.enableDebugLogging && debugInputCount < 8)
            {
                for (int i = 0; i < Regions.Count; ++i)
                {
                    Rect area = Regions[i].screenRect;
                    if (clickScreenPoint.y >= area.yMin - 15f &&
                        clickScreenPoint.y <= area.yMax + 15f)
                    {
                        ++debugInputCount;
                        Log.Message("[QoL: Child Learning] UI3-Fix1 MISS: " +
                            "labelRegions=" + Regions.Count +
                            ", mouseScreen=" + clickScreenPoint);
                        break;
                    }
                }
            }
            return false;
        }

    }

    /// <summary>
    /// Capture the label background rectangle at the exact moment the vanilla
    /// Rect overload draws it. Priority.Last sees the temporary scale matrix
    /// after UI1A-2, while the pawn scope excludes labels in other windows.
    /// </summary>
    [HarmonyPatch(typeof(GenMapUI), "DrawPawnLabel", new Type[]
    {
        typeof(Pawn), typeof(Rect), typeof(float), typeof(float),
        typeof(Dictionary<string, string>), typeof(GameFont),
        typeof(bool), typeof(bool)
    })]
    internal static class Patch_ChildLearningToggle_LabelHitRegion
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Last)]
        private static void Prefix(Pawn pawn, Rect bgRect)
        {
            if (Event.current != null &&
                Event.current.type == EventType.Repaint &&
                LearningColonistBarLabelScope.InEligibleScope(pawn))
            {
                ChildLearningLabelToggle.RememberPaintedLabel(pawn, bgRect);
            }
        }
    }

    /// <summary>
    /// The vanilla bar receives mouse events in a separate IMGUI pass from
    /// Repaint. Check only the last painted label hotspots, before the bar's
    /// built-in portrait click handling. Skip only this consumed MouseDown.
    /// </summary>
    [HarmonyPatch(typeof(ColonistBar), "ColonistBarOnGUI")]
    internal static class Patch_ChildLearningToggle_ColonistBarMouse
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix()
        {
            Event evt = Event.current;
            if (evt != null && evt.type == EventType.Repaint)
                ChildLearningLabelToggle.BeginRepaint();
            return !ChildLearningLabelToggle.TryConsumeLabelClick();
        }
    }
}
