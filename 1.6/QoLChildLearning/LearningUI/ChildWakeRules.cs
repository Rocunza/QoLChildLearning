using System;
using Verse;

namespace ZCN.QoL.ChildLearning
{
    // Pure 4S-2H1 rules: integer tenths avoid UI rounding ambiguity.
    // No allocations, pathfinding, food searches or Harmony patches in this path.
    internal static class ChildWakeRules
    {
        internal const int TicksPerTenthGameHour = 250;

        internal static int LimitTenths(int value, int max)
        {
            return Math.Max(0, Math.Min(max, value));
        }

        internal static bool LearningBelowThreshold(float needLevel01, int thresholdTenths)
        {
            if (float.IsNaN(needLevel01) || float.IsInfinity(needLevel01))
                return false;
            return (double)needLevel01 * 1000.0 <= thresholdTenths + 0.0001;
        }

        internal static bool MeetsMinimumRest(float needLevel01, int minTenths)
        {
            if (minTenths == 0) return true;
            if (float.IsNaN(needLevel01) || float.IsInfinity(needLevel01))
                return false;
            return (double)needLevel01 * 1000.0 >= minTenths - 0.0001;
        }

        internal static bool FoodBelowThreshold(float foodLevel01, int maxTenths)
        {
            // Unlike threshold 0.0%, zero means unconditional (user specification).
            if (maxTenths == 0) return true;
            if (float.IsNaN(foodLevel01) || float.IsInfinity(foodLevel01))
                return false;
            return (double)foodLevel01 * 1000.0 <= maxTenths + 0.0001;
        }

        // If vanilla immediately selects LayDown again, do not spam wake attempts.
        // An early failed wake (>89.9% Learning) may be retried as soon as
        // Learning reaches the usual vanilla learning-start region.
        internal const int VanillaLearningRetryTenths = 899;
        internal const int FailedWakeRetryTicks = 2500; // 1 game hour
        internal const int WakeConfirmationTicks = 250; // 0.1 game hour

        internal static bool CanRetryFailedWake(float currentLearning,
            bool failedAboveVanillaLearning, int now, int lastFailedTick)
        {
            if (lastFailedTick < 0) return true;
            bool learningReady = LearningBelowThreshold(currentLearning,
                VanillaLearningRetryTenths);
            if (failedAboveVanillaLearning && !learningReady) return false;
            // A transition from the early-wake region to the vanilla learning
            // region is meaningful even before the fallback timer elapses.
            if (failedAboveVanillaLearning && learningReady) return true;
            return (long)now - lastFailedTick >= FailedWakeRetryTicks;
        }

        internal static bool ConfirmationElapsed(int now, int attemptedAt)
        {
            return (long)now - attemptedAt >= WakeConfirmationTicks;
        }

        internal static bool CooldownPassed(int tickNow, int tickLast, int hoursTenths)
        {
            if (hoursTenths <= 0 || tickLast < 0) return true;
            // Use long to handle tick arithmetic without overflow.
            return (long)tickNow - tickLast >= (long)hoursTenths * TicksPerTenthGameHour;
        }
    }

#if DEBUG
    // Automated deterministic tests run once on DEBUG mod start (without developer mode).
    internal static class ChildWakeSelfTests
    {
        private static void Assert(bool result, string name)
        {
            if (!result) throw new InvalidOperationException("Auto-wake rule failed: " + name);
        }

        internal static void Run()
        {
            try
            {
                Assert(ChildWakeRules.LimitTenths(-8, 1000) == 0, "clamp-low");
                Assert(ChildWakeRules.LimitTenths(1250, 1000) == 1000, "clamp-high");
                Assert(ChildWakeRules.LimitTenths(241, 240) == 240, "hour-limit");
                Assert(ChildWakeRules.LearningBelowThreshold(0.903f, 903), "90.3-boundary");
                Assert(!ChildWakeRules.LearningBelowThreshold(0.904f, 903), "90.4-above");
                Assert(!ChildWakeRules.LearningBelowThreshold(0.901f, 900), "90.1-above");
                Assert(ChildWakeRules.MeetsMinimumRest(0.01f, 0), "rest-off");
                Assert(ChildWakeRules.MeetsMinimumRest(0.50f, 500), "rest-50-boundary");
                Assert(!ChildWakeRules.MeetsMinimumRest(0.499f, 500), "rest-below");
                Assert(ChildWakeRules.CooldownPassed(1001, 1000, 0), "cooldown-off");
                Assert(!ChildWakeRules.CooldownPassed(1249, 1000, 1), "cooldown-0.1-not-yet");
                Assert(ChildWakeRules.CooldownPassed(1250, 1000, 1), "cooldown-0.1-exact");
                Assert(!ChildWakeRules.CooldownPassed(60999, 1000, 240), "cooldown-24-not-yet");
                Assert(ChildWakeRules.CooldownPassed(61000, 1000, 240), "cooldown-24-exact");
                Assert(ChildWakeRules.FoodBelowThreshold(0.70f, 0), "food-zero-unconditional");
                Assert(ChildWakeRules.FoodBelowThreshold(1.00f, 0), "food-zero-even-full");
                Assert(ChildWakeRules.FoodBelowThreshold(0.60f, 600), "food-60-boundary");
                Assert(!ChildWakeRules.FoodBelowThreshold(0.601f, 600), "food-60-above");
                Assert(ChildWakeRules.FoodBelowThreshold(0.903f, 903), "food-90.3-boundary");
                Assert(!ChildWakeRules.FoodBelowThreshold(0.904f, 903), "food-90.4-above");
                Assert(!ChildWakeRules.FoodBelowThreshold(float.NaN, 600), "food-nan-safe");
                Assert(!ChildWakeRules.CanRetryFailedWake(0.905f, true,
                    10100, 10000), "early-failure-no-immediate-retry");
                Assert(ChildWakeRules.CanRetryFailedWake(0.899f, true,
                    10100, 10000), "early-failure-retry-after-learning-drop");
                Assert(!ChildWakeRules.CanRetryFailedWake(0.899f, false,
                    12499, 10000), "low-learning-backoff-before-hour");
                Assert(ChildWakeRules.CanRetryFailedWake(0.899f, false,
                    12500, 10000), "low-learning-backoff-after-hour");
                Assert(ChildWakeRules.CanRetryFailedWake(0.97f, false,
                    12500, 10000), "failed-wake-backoff-no-stuck-guard");
                Assert(!ChildWakeRules.CanRetryFailedWake(float.NaN, true,
                    14000, 10000), "early-failure-nan-safe");
                Assert(ChildWakeRules.CanRetryFailedWake(0.5f, true,
                    14000, -1), "no-failure-record");
                Assert(!ChildWakeRules.ConfirmationElapsed(249, 0),
                    "confirmation-too-early");
                Assert(ChildWakeRules.ConfirmationElapsed(250, 0),
                    "confirmation-exact-boundary");
                Assert(ChildWakeRules.ConfirmationElapsed(100500, 100000),
                    "confirmation-after-boundary");
                Log.Message("[QoL: Child Learning] 4S-2H3B-WF1 wake/food rule self-tests PASS (31/31).");
            }
            catch (Exception ex)
            {
                Log.Error("[QoL: Child Learning] 4S-2H1 auto-wake rule self-tests FAILED: " + ex);
            }
        }
    }
#endif
}
