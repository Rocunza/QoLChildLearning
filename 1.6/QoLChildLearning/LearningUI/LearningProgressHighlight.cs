using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ZCN.QoL.ChildLearning
{
    /// <summary>
    /// UI1B/UI3-Fix2: records ACTUAL Learning need gains, plus positive
    /// Learn() calls at the exact 100% cap. This is evidence of a learning
    /// action still running even though no higher value can be earned.
    /// Merely scheduling a job or walking to a learning location is ignored.
    /// Uses weak keys, so old saves / dead pawns need no manual cleanup.
    /// No periodic game-tick scanning, no changes to learning rate or jobs.
    /// </summary>
    internal static class LearningProgressHighlight
    {
        // Keep the tint visible between vanilla activity updates and avoid
        // one-frame flashes. At normal simulation speed 120 ticks is ~2s.
        private const int RecentGainWindowTicks = 120;

        private sealed class GainStamp
        {
            // Last actual gain OR attempted positive Learn() at the hard cap.
            internal int lastLearningTick = -1;
        }

        private static readonly ConditionalWeakTable<Need_Learning, GainStamp> LastGains =
            new ConditionalWeakTable<Need_Learning, GainStamp>();

        private static readonly ConditionalWeakTable<Need_Learning, GainStamp>.CreateValueCallback StampFactory =
            delegate(Need_Learning need) { return new GainStamp(); };

        internal static bool Enabled
        {
            get
            {
                ChildLearningSettings settings = ChildLearningMod.Settings;
                return settings != null && settings.showChildLearningPercent &&
                       settings.highlightActiveChildLearning;
            }
        }

        internal static void RecordLearningActivity(Need_Learning need)
        {
            if (!Enabled || need == null || Find.TickManager == null)
                return;

            LastGains.GetValue(need, StampFactory).lastLearningTick =
                Find.TickManager.TicksGame;
        }

        internal static bool IsRecentlyIncreasing(Pawn pawn)
        {
            if (!Enabled || pawn == null || pawn.needs == null ||
                pawn.needs.learning == null || Find.TickManager == null)
                return false;

            Need_Learning need = pawn.needs.learning;
            // Do not drop the highlight just because Learning reached 100%.
            // A forced learning activity can keep invoking Learn() at the cap.
            GainStamp stamp;
            if (!LastGains.TryGetValue(need, out stamp) || stamp.lastLearningTick < 0)
                return false;

            int age = Find.TickManager.TicksGame - stamp.lastLearningTick;
            return age >= 0 && age <= RecentGainWindowTicks;
        }

        internal static Color LearningTextColor(Color original)
        {
            ChildLearningSettings settings = ChildLearningMod.Settings;
            int choice = settings != null
                ? settings.activeChildLearningHighlightColor
                : LearningHighlightPalette.Default;
            return LearningHighlightPalette.TextColor(choice, original.a);
        }
    }

    /// <summary>
    /// Read-only observation of the official Learning need gain method.
    /// No original execution is skipped; other mods' learning rates remain
    /// intact. At full capacity a positive Learn() attempt is remembered as
    /// ongoing learning activity, not as an actual numerical gain.
    /// </summary>
    [HarmonyPatch(typeof(Need_Learning), "Learn", new Type[] { typeof(float) })]
    internal static class Patch_ActualLearningGain_Track
    {
        [HarmonyPrefix]
        private static void Prefix(Need_Learning __instance, out float __state)
        {
            __state = float.NaN;
            if (LearningProgressHighlight.Enabled && __instance != null)
                __state = __instance.CurLevel;
        }

        [HarmonyPostfix]
        private static void Postfix(Need_Learning __instance, float amount, float __state)
        {
            if (float.IsNaN(__state) || __instance == null)
                return;

            bool actualGain = __instance.CurLevel > __state + 0.0000001f;
            // At exactly 100%, RimWorld clamps Learn(amount) to zero.
            // The positive call still establishes that a learning activity
            // is actively running (e.g. forced book reading at full need).
            // Only accept positive calls at the hard cap: zero/negative calls
            // and ordinary joy-reading must not light up the label.
            bool ongoingAtCap = amount > 0f && __state >= 0.999999f &&
                __instance.CurLevel >= 0.999999f;
            if (actualGain || ongoingAtCap)
                LearningProgressHighlight.RecordLearningActivity(__instance);
        }
    }

    /// <summary>
    /// GenMapUI.DrawPawnLabel requests its foreground text color through
    /// PawnNameColorUtility.PawnNameColorOf. Change only the numeric label
    /// in the existing UI1A/UI1A-3 child-only render scope; vanilla names,
    /// adult labels, other windows and portrait colors stay unmodified.
    /// </summary>
    [HarmonyPatch(typeof(PawnNameColorUtility), "PawnNameColorOf", new Type[] { typeof(Pawn) })]
    internal static class Patch_ChildNumber_ColorWhileLearning
    {
        [HarmonyPostfix]
        private static void Postfix(Pawn pawn, ref Color __result)
        {
            if (!LearningColonistBarLabelScope.IsTarget(pawn))
                return;

            if (LearningProgressHighlight.IsRecentlyIncreasing(pawn))
                __result = LearningProgressHighlight.LearningTextColor(__result);
        }
    }
}
