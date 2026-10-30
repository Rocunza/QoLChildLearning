using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ZCN.QoL.ChildLearning
{
    // H2: optional food-first job, only at a genuine wake event.
    // Intentionally no global sleep/learning think-tree Harmony modification.
    internal static class ChildAutoWake
    {
        internal static void TryWake(Pawn pawn,
            HashSet<Pawn> disarmed, Dictionary<Pawn, int> lastWakeTick)
        {
            ChildLearningSettings options = ChildLearningMod.Settings;
            if (options == null || !options.autoWakeChildren ||
                !ChildGizmoPolicy.WakeEnabled(pawn) || pawn == null ||
                pawn.needs == null || pawn.needs.learning == null ||
                pawn.jobs == null)
                return;

            Job job = pawn.CurJob;
            bool sleeping = job != null && job.def == JobDefOf.LayDown;
            // Once the child is observed awake, a new sleep episode is eligible.
            // If the child immediately goes back to bed, do not spam EndCurrentJob.
            if (!sleeping)
            {
                bool wasDisarmed = disarmed.Remove(pawn);
                ChildWakeOutcome.ObservedAwake(pawn);
#if DEBUG
                ChildWakeTrace.Decision(pawn, wasDisarmed ? "awake-reset-disarm" : "awake",
                    disarmed, lastWakeTick);
#endif
                return;
            }

            // Keep behavior identical to 4S-2H3A; DEBUG only reports which
            // preexisting gate rejected this sleeping child.
            if (disarmed.Contains(pawn))
            {
#if DEBUG
                ChildWakeTrace.Decision(pawn, "blocked-disarmed", disarmed, lastWakeTick);
#endif
                return;
            }
            if (job.playerForced || pawn.Downed || pawn.Drafted ||
                pawn.InMentalState || pawn.Faction != Faction.OfPlayer ||
                !pawn.RaceProps.Humanlike)
            {
#if DEBUG
                string reason = job.playerForced ? "blocked-player-forced" :
                    pawn.Downed ? "blocked-downed" :
                    pawn.Drafted ? "blocked-drafted" :
                    pawn.InMentalState ? "blocked-mental" :
                    pawn.Faction != Faction.OfPlayer ? "blocked-not-player" :
                    "blocked-not-humanlike";
                ChildWakeTrace.Decision(pawn, reason, disarmed, lastWakeTick);
#endif
                return;
            }

            // Never interrupt medical bed rest or incapacitated pawns.
            Building_Bed bed = pawn.CurrentBed();
            if (bed != null && bed.Medical)
            {
#if DEBUG
                ChildWakeTrace.Decision(pawn, "blocked-medical-bed", disarmed, lastWakeTick);
#endif
                return;
            }
            if (!ChildWakeRules.LearningBelowThreshold(
                    pawn.needs.learning.CurLevel, options.autoWakeLearningTenths))
            {
#if DEBUG
                ChildWakeTrace.Decision(pawn, "blocked-learning-above", disarmed, lastWakeTick);
#endif
                return;
            }
            if (pawn.needs.rest == null ||
                !ChildWakeRules.MeetsMinimumRest(
                    pawn.needs.rest.CurLevel, options.autoWakeMinimumRestTenths))
            {
#if DEBUG
                ChildWakeTrace.Decision(pawn, "blocked-rest-below", disarmed, lastWakeTick);
#endif
                return;
            }
            int now = Find.TickManager.TicksGame;
            int previous;
            if (lastWakeTick != null && lastWakeTick.TryGetValue(pawn, out previous) &&
                !ChildWakeRules.CooldownPassed(now, previous,
                    options.autoWakeCooldownTenthsHours))
            {
#if DEBUG
                ChildWakeTrace.Decision(pawn, "blocked-cooldown", disarmed, lastWakeTick);
#endif
                return;
            }
            string retryBlockReason;
            if (!ChildWakeOutcome.CanAttempt(pawn, now, out retryBlockReason))
            {
#if DEBUG
                ChildWakeTrace.Decision(pawn, retryBlockReason, disarmed, lastWakeTick);
#endif
                return;
            }
#if DEBUG
            ChildWakeTrace.Decision(pawn, "eligible", disarmed, lastWakeTick);
            ChildWakeTrace.BeforeWake(pawn, disarmed, lastWakeTick);
#endif

            // Find food only once on the wake event, not during 250-tick scans.
            // Do not modify existing queued player commands (including queued eating).
            string foodStatus = "disabled";
            Job foodJob = ChildWakeFood.FindFoodJob(pawn, options, out foodStatus);

            // An attempt is NOT a success. ChildWakeOutcome checks for an
            // immediate LayDown and confirms a real awake spell after 250 ticks.
            if (foodJob != null)
            {
                // Suppress the normal post-sleep job selection for one action.
                // No jobs are queued or displaced; vanilla StartJob reserves food.
                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, false);
                pawn.jobs.StartJob(foodJob, JobCondition.None);
                foodStatus = pawn.CurJob == foodJob ? "started-Ingest" : "not-started";
            }
            else
            {
                // The tested H1 behavior is unchanged when no meal is eligible.
                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }

            bool pendingConfirmation = ChildWakeOutcome.Begin(pawn, job, now, disarmed);
#if DEBUG
            ChildWakeTrace.AfterWake(pawn, disarmed, lastWakeTick, foodStatus);
#endif
            bool logWake = ChildLearningDebug.Enabled;
#if DEBUG
            logWake = true; // Automated evidence in test builds; at most once per wake.
#endif
            if (logWake)
                Log.Message("[QoL: Child Learning][Wake] child=" + pawn.LabelShort +
                    "; Learning=" + (pawn.needs.learning.CurLevel * 100f).ToString("0.0") +
                    "%; Rest=" + (pawn.needs.rest.CurLevel * 100f).ToString("0.0") +
                    "%; tick=" + now + "; nextJob=" +
                    (pawn.CurJob != null ? pawn.CurJob.def.defName : "none") +
                    "; food=" + foodStatus +
                    "; outcome=" + (pendingConfirmation ? "pending-250t-confirmation" :
                        "failed-immediate-resleep") + ".");
        }
    }
}
