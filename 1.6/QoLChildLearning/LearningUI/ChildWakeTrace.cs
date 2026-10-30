#if DEBUG
using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ZCN.QoL.ChildLearning
{
    // Diagnostic-only flight recorder. NO job changes, NO new Harmony hooks.
    // One decision record on a reason change; short-lived per-tick sampling
    // applies ONLY to pawns whom R&L actually tried to wake.
    internal static class ChildWakeTrace
    {
        private sealed class DecisionStamp
        {
            internal string Signature;
            internal int LastLoggedTick;
        }

        private sealed class WakeWatch
        {
            internal Pawn Pawn;
            internal int StartTick;
            internal string LastJob;
            internal bool SawNonSleep;
            internal bool SawResleep;
            internal int NextSample;
        }

        private static readonly int[] SampleOffsets = { 1, 10, 30, 60, 120, 250 };
        private static readonly Dictionary<Pawn, DecisionStamp> Decisions =
            new Dictionary<Pawn, DecisionStamp>();
        private static readonly Dictionary<Pawn, WakeWatch> Watching =
            new Dictionary<Pawn, WakeWatch>();
        private static readonly List<Pawn> RemoveList = new List<Pawn>();

        internal static void Reset()
        {
            Decisions.Clear();
            Watching.Clear();
            RemoveList.Clear();
            Log.Message("[ChildLearning][WakeTrace] RESET (new game/load); diagnostic only; " +
                "scan=250t, post-wake samples=1/10/30/60/120/250t.");
        }

        internal static void Forget(Pawn pawn)
        {
            Decisions.Remove(pawn);
            Watching.Remove(pawn);
        }

        private static string JobName(Pawn pawn)
        {
            Job job = pawn != null ? pawn.CurJob : null;
            return job == null || job.def == null ? "none" : job.def.defName;
        }

        private static string Values(Pawn pawn, HashSet<Pawn> disarmed,
            Dictionary<Pawn, int> lastWakeTick, int now)
        {
            ChildLearningSettings settings = ChildLearningMod.Settings;
            int last = 0;
            bool hasLast = lastWakeTick != null &&
                lastWakeTick.TryGetValue(pawn, out last);
            int delta = hasLast ? now - last : -1;
            Job job = pawn.CurJob;
            return "child=" + pawn.LabelShort +
                "; id=" + pawn.GetUniqueLoadID() +
                "; tick=" + now +
                "; Learning=" + (pawn.needs.learning.CurLevel * 100f).ToString("0.000") +
                "%/limit=" + (settings.autoWakeLearningTenths / 10f).ToString("0.0") +
                "; Rest=" + (pawn.needs.rest != null ?
                    (pawn.needs.rest.CurLevel * 100f).ToString("0.000") : "null") +
                "%/minimum=" + (settings.autoWakeMinimumRestTenths / 10f).ToString("0.0") +
                "; cooldownH=" + (settings.autoWakeCooldownTenthsHours / 10f).ToString("0.0") +
                "; sinceLastWakeT=" + (hasLast ? delta.ToString() : "none") +
                "; disarmed=" + disarmed.Contains(pawn) +
                "; job=" + JobName(pawn) +
                "; forced=" + (job != null && job.playerForced) +
                "; queue=" + (pawn.jobs != null && pawn.jobs.jobQueue != null ?
                    pawn.jobs.jobQueue.Count : 0) +
                "; drafted=" + pawn.Drafted +
                "; downed=" + pawn.Downed;
        }

        internal static void Decision(Pawn pawn, string reason,
            HashSet<Pawn> disarmed, Dictionary<Pawn, int> lastWakeTick)
        {
            if (pawn == null || pawn.needs == null || pawn.needs.learning == null ||
                pawn.jobs == null || ChildLearningMod.Settings == null) return;
            int now = Find.TickManager.TicksGame;
            // Awake job types change often and are irrelevant unless a wake is being watched.
            string signature = reason + "|" + disarmed.Contains(pawn) +
                (reason == "awake" ? "" : "|" + JobName(pawn));
            DecisionStamp old;
            bool known = Decisions.TryGetValue(pawn, out old);
            bool changed = !known || old.Signature != signature;
            // If permanently disarmed while sleeping, a 5,000-tick heartbeat
            // distinguishes a short pause from an indefinitely stuck guard.
            bool stuckHeartbeat = !changed && reason == "blocked-disarmed" &&
                now - old.LastLoggedTick >= 5000;
            if (!changed && !stuckHeartbeat) return;
            if (!known)
            {
                old = new DecisionStamp();
                Decisions[pawn] = old;
            }
            old.Signature = signature;
            old.LastLoggedTick = now;
            Log.Message("[ChildLearning][WakeTrace][SCAN] reason=" + reason + "; " +
                Values(pawn, disarmed, lastWakeTick, now));
        }

        internal static void BeforeWake(Pawn pawn, HashSet<Pawn> disarmed,
            Dictionary<Pawn, int> lastWakeTick)
        {
            int now = Find.TickManager.TicksGame;
            Log.Message("[ChildLearning][WakeTrace][ATTEMPT] before-EndCurrentJob; " +
                Values(pawn, disarmed, lastWakeTick, now));
        }

        internal static void AfterWake(Pawn pawn, HashSet<Pawn> disarmed,
            Dictionary<Pawn, int> lastWakeTick, string foodStatus)
        {
            int now = Find.TickManager.TicksGame;
            string currentJob = JobName(pawn);
            Log.Message("[ChildLearning][WakeTrace][RESULT] after-EndCurrentJob; food=" +
                foodStatus + "; " + Values(pawn, disarmed, lastWakeTick, now));
            Watching[pawn] = new WakeWatch
            {
                Pawn = pawn,
                StartTick = now,
                LastJob = currentJob,
                SawNonSleep = currentJob != JobDefOf.LayDown.defName,
                SawResleep = false,
                NextSample = 0
            };
        }

        // Called in GameComponentTick every tick; does NOTHING if nobody was woken.
        // Does not enumerate all pawns or look for food.
        internal static void Tick()
        {
            if (Watching.Count == 0) return;
            int now = Find.TickManager.TicksGame;
            RemoveList.Clear();
            foreach (KeyValuePair<Pawn, WakeWatch> pair in Watching)
            {
                WakeWatch watch = pair.Value;
                Pawn pawn = watch.Pawn;
                int elapsed = now - watch.StartTick;
                if (pawn == null || pawn.Destroyed || elapsed > 250)
                {
                    RemoveList.Add(pair.Key);
                    continue;
                }
                string job = JobName(pawn);
                bool sleeping = job == JobDefOf.LayDown.defName;
                if (!sleeping) watch.SawNonSleep = true;
                if (sleeping && watch.SawNonSleep) watch.SawResleep = true;
                if (job != watch.LastJob)
                {
                    Log.Message("[ChildLearning][WakeTrace][JOB-CHANGE] child=" + pawn.LabelShort +
                        "; tick=" + now + "; afterT=" + elapsed +
                        "; from=" + watch.LastJob + "; to=" + job +
                        "; sawAwake=" + watch.SawNonSleep +
                        "; reSlept=" + watch.SawResleep);
                    watch.LastJob = job;
                }
                if (watch.NextSample < SampleOffsets.Length &&
                    elapsed >= SampleOffsets[watch.NextSample])
                {
                    Log.Message("[ChildLearning][WakeTrace][SAMPLE] child=" + pawn.LabelShort +
                        "; afterT=" + elapsed + "; job=" + job +
                        "; sawAwake=" + watch.SawNonSleep +
                        "; reSlept=" + watch.SawResleep);
                    watch.NextSample++;
                }
                if (elapsed == 250)
                {
                    Log.Message("[ChildLearning][WakeTrace][SUMMARY] child=" + pawn.LabelShort +
                        "; tick=" + now + "; job=" + job +
                        "; sawNonSleep=" + watch.SawNonSleep +
                        "; returnedToSleep=" + watch.SawResleep + ".");
                    RemoveList.Add(pair.Key);
                }
            }
            for (int i = 0; i < RemoveList.Count; i++)
                Watching.Remove(RemoveList[i]);
        }
    }
}
#endif
