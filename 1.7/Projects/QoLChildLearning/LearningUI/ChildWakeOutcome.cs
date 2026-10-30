using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ZCN.QoL.ChildLearning
{
    // Actual wake completion, not the EndCurrentJob call, is the success event.
    // No permanent timetable change and no ThinkTree / global job Harmony hook.
    // Runtime-only state, reset on save load and pruned when a pawn leaves maps.
    internal static class ChildWakeOutcome
    {
        private sealed class FailedWake
        {
            internal int Tick;
            internal bool WasAboveVanillaLearning;
        }

        private sealed class PendingWake
        {
            internal int StartTick;
        }

        private static readonly Dictionary<Pawn, FailedWake> Failures =
            new Dictionary<Pawn, FailedWake>();
        private static readonly Dictionary<Pawn, PendingWake> Pending =
            new Dictionary<Pawn, PendingWake>();
        private static readonly List<Pawn> RemoveList = new List<Pawn>();

        internal static void Reset()
        {
            Failures.Clear();
            Pending.Clear();
            RemoveList.Clear();
        }

        internal static void Forget(Pawn pawn)
        {
            Failures.Remove(pawn);
            Pending.Remove(pawn);
        }

        internal static void ObservedAwake(Pawn pawn)
        {
            // A truly awake spell ends the old failed sleep episode.
            Failures.Remove(pawn);
        }

        internal static bool CanAttempt(Pawn pawn, int now, out string reason)
        {
            reason = null;
            if (Pending.ContainsKey(pawn))
            {
                reason = "blocked-wake-confirmation";
                return false;
            }
            FailedWake previous;
            if (!Failures.TryGetValue(pawn, out previous)) return true;
            float learning = pawn.needs.learning.CurLevel;
            if (!ChildWakeRules.CanRetryFailedWake(learning,
                previous.WasAboveVanillaLearning, now, previous.Tick))
            {
                reason = previous.WasAboveVanillaLearning &&
                    !ChildWakeRules.LearningBelowThreshold(learning,
                        ChildWakeRules.VanillaLearningRetryTenths) ?
                    "blocked-wait-learning-below-90" : "blocked-retry-one-hour";
                return false;
            }
            return true;
        }

        private static void RecordFailure(Pawn pawn, int now,
            HashSet<Pawn> disarmed, string result)
        {
            if (pawn.needs == null || pawn.needs.learning == null) return;
            disarmed.Remove(pawn);
            Failures[pawn] = new FailedWake
            {
                Tick = now,
                WasAboveVanillaLearning = !ChildWakeRules.LearningBelowThreshold(
                    pawn.needs.learning.CurLevel, ChildWakeRules.VanillaLearningRetryTenths)
            };
#if DEBUG
            Log.Message("[ChildLearning][WakeOutcome] status=" + result +
                "; child=" + pawn.LabelShort + "; tick=" + now +
                "; Learning=" + (pawn.needs.learning.CurLevel * 100f).ToString("0.000") +
                "% ; cooldownNotCharged=True");
#endif
        }

        // Called AFTER EndCurrentJob/StartJob, before the old one-shot guard
        // would otherwise count the attempt as successful.
        internal static bool Begin(Pawn pawn, Job beforeJob, int now,
            HashSet<Pawn> disarmed)
        {
            Job next = pawn.CurJob;
            if (next != null && next.def == JobDefOf.LayDown)
            {
#if DEBUG
                Log.Message("[ChildLearning][WakeOutcome] sameTickSleep; sameJobObject=" +
                    object.ReferenceEquals(beforeJob, next) + "; child=" + pawn.LabelShort);
#endif
                RecordFailure(pawn, now, disarmed, "reslept-same-tick");
                return false;
            }
            // If EndCurrentJob briefly leaves no job, only declare a success
            // after the game has had a chance to select a genuine next job.
            Pending[pawn] = new PendingWake { StartTick = now };
            disarmed.Add(pawn);
#if DEBUG
            Log.Message("[ChildLearning][WakeOutcome] status=pending-confirmation; child=" +
                pawn.LabelShort + "; tick=" + now + "; job=" +
                (next != null && next.def != null ? next.def.defName : "none"));
#endif
            return true;
        }

        // Invoked by the existing game component every tick. Only pending
        // children (usually 0, occasionally 1-2) are enumerated. No food search.
        internal static void Tick(HashSet<Pawn> disarmed,
            Dictionary<Pawn, int> lastWakeTick)
        {
            if (Pending.Count == 0) return;
            int now = Find.TickManager.TicksGame;
            RemoveList.Clear();
            foreach (KeyValuePair<Pawn, PendingWake> pair in Pending)
            {
                Pawn pawn = pair.Key;
                if (pawn == null || pawn.Destroyed || pawn.jobs == null ||
                    pawn.needs == null || pawn.needs.learning == null ||
                    pawn.Faction != Faction.OfPlayer)
                {
                    disarmed.Remove(pawn);
                    RemoveList.Add(pawn);
                    continue;
                }
                Job job = pawn.CurJob;
                if (job != null && job.def == JobDefOf.LayDown)
                {
                    RecordFailure(pawn, now, disarmed, "returned-to-sleep-before-confirmation");
                    RemoveList.Add(pawn);
                    continue;
                }
                if (ChildWakeRules.ConfirmationElapsed(now, pair.Value.StartTick))
                {
                    // Commit the successful wake only after 250 ticks out of bed.
                    if (lastWakeTick != null) lastWakeTick[pawn] = pair.Value.StartTick;
                    Failures.Remove(pawn);
#if DEBUG
                    Log.Message("[ChildLearning][WakeOutcome] status=confirmed-awake; child=" +
                        pawn.LabelShort + "; tick=" + now +
                        "; wakeTick=" + pair.Value.StartTick +
                        "; cooldownCharged=True");
#endif
                    RemoveList.Add(pawn);
                }
            }
            for (int i = 0; i < RemoveList.Count; i++) Pending.Remove(RemoveList[i]);
        }
    }
}
