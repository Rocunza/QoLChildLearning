using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace ZCN.QoL.ChildLearning
{
    // EF1: one opt-in job-SELECTION postfix, not a time-based job interrupter.
    // Never EndCurrentJob(), enqueue, start a job, alter the timetable, or
    // force the RimWorld think-tree to run while a pawn is working or sleeping.
    internal static class ChildIndependentFood
    {
        // A food job is offered at most once in a short window, and the
        // hunger threshold must be exited before a new meal is offered.
        // If one tiny meal leaves the pawn hungry, vanilla hunger handling
        // remains free to feed them; after six game hours EF1 can try again.
        internal const int RetryTicks = 15000; // six game hours
        private static readonly Dictionary<Pawn, int> LastMealOffers =
            new Dictionary<Pawn, int>();
        private static readonly List<Pawn> Scratch = new List<Pawn>();
        private static bool installationFailed;
        private static bool postfixErrorLogged;

        internal static void Reset()
        {
            LastMealOffers.Clear();
            Scratch.Clear();
#if DEBUG
            ChildDraftFoodTrace.Reset();
#endif
        }
        internal static void Forget(Pawn pawn) { if (pawn != null) LastMealOffers.Remove(pawn); }

        // Invoked only by the existing 250-tick GameComponent scan.
        internal static void Reconcile(List<Pawn> currentPawns)
        {
            if (LastMealOffers.Count == 0) return;
            Scratch.Clear();
            foreach (KeyValuePair<Pawn, int> pair in LastMealOffers)
            {
                Pawn pawn = pair.Key;
                if (pawn == null || pawn.Destroyed || pawn.needs == null ||
                    pawn.needs.food == null || !currentPawns.Contains(pawn))
                {
                    Scratch.Add(pawn);
                    continue;
                }
                ChildLearningSettings s = ChildLearningMod.Settings;
                if (s == null || !s.autoEatAtJobTransition ||
                    !ChildGizmoPolicy.FoodEnabled(pawn) ||
                    (long)Find.TickManager.TicksGame - pair.Value >= RetryTicks)
                    Scratch.Add(pawn);
            }
            foreach (Pawn pawn in Scratch) LastMealOffers.Remove(pawn);
        }

        internal static bool BelowIndependentThreshold(float food01, int configuredTenths)
        {
            if (float.IsNaN(food01) || float.IsInfinity(food01)) return false;
            // Independent mode never orders food when full. Legacy wake-only
            // 0=unconditional remains unchanged when independent mode is OFF.
            int threshold = configuredTenths == 0 ? 500 :
                ChildWakeRules.LimitTenths(configuredTenths, 1000);
            return food01 < 0.999f &&
                ChildWakeRules.FoodBelowThreshold(food01, threshold);
        }

        internal static bool CanOfferAgain(int now, int previous)
        {
            return (long)now - previous >= RetryTicks;
        }

        // Fail-open: versions without the correct 1.6 hook simply retain
        // vanilla food AI. No startup failure for disabled/unsupported hook.
        internal static void Install(Harmony harmony)
        {
            try
            {
                MethodInfo target = null;
                foreach (MethodInfo method in AccessTools.GetDeclaredMethods(
                    typeof(Pawn_JobTracker)))
                {
                    if (method.Name != "DetermineNextJob" ||
                        method.ReturnType != typeof(ThinkResult)) continue;
                    ParameterInfo[] args = method.GetParameters();
                    if (args.Length >= 1 && args[0].ParameterType ==
                        typeof(ThinkTreeDef).MakeByRefType())
                    {
                        target = method;
                        break;
                    }
                }
                if (target == null)
                {
                    installationFailed = true;
                    Log.Warning("[QoL: Child Learning][EF1] No compatible " +
                        "DetermineNextJob found; independent food disabled.");
                    return;
                }
                MethodInfo postfix = AccessTools.Method(typeof(ChildIndependentFood),
                    "OnDetermineNextJob");
                harmony.Patch(target, postfix: new HarmonyMethod(postfix));
                Log.Message("[QoL: Child Learning][EF1] Independent food " +
                    "job-selection hook installed (configurable, default ON; " +
                    "no active job interruption).");
            }
            catch (Exception ex)
            {
                installationFailed = true;
                Log.Warning("[QoL: Child Learning][EF1] Failed to install optional " +
                    "food job hook. Vanilla AI preserved. " + ex);
            }
        }

        private static void OnDetermineNextJob(Pawn ___pawn,
            ref ThinkResult __result, ref ThinkTreeDef __0)
        {
#if DEBUG
            // Observe job decisions inside the draft setter before any EF1 guard.
            ChildDraftFoodTrace.ObserveChoice(___pawn, __result, __0);
#endif
            ChildLearningSettings settings = ChildLearningMod.Settings;
            if (installationFailed || settings == null ||
                !settings.autoEatAtJobTransition ||
                !ChildGizmoPolicy.FoodEnabled(___pawn)) return;

            try
            {
                Pawn pawn = ___pawn;
                if (pawn == null || !pawn.Spawned || pawn.Dead ||
                    pawn.Downed || pawn.Drafted || pawn.InMentalState ||
                    pawn.Faction != Faction.OfPlayer || !pawn.IsColonist ||
                    !pawn.RaceProps.Humanlike || pawn.jobs == null ||
                    pawn.CurJob != null || pawn.jobs.startingNewJob ||
                    pawn.InBed() || pawn.needs == null ||
                    pawn.needs.learning == null || pawn.needs.food == null)
                    return;

                if (pawn.jobs.jobQueue != null && pawn.jobs.jobQueue.Count > 0)
                    return;
                if (pawn.mindState == null || pawn.thinker == null ||
                    pawn.mindState.priorityWork.IsPrioritized ||
                    (pawn.thinker.ConstantThinkTree != null &&
                        __0 == pawn.thinker.ConstantThinkTree))
                    return;
                if (!__result.IsValid || __result.FromQueue ||
                    __result.Job.playerForced ||
                    __result.Job.def == JobDefOf.LayDown ||
                    __result.Job.def == JobDefOf.Ingest)
                    return;

                // Guard categories that should never be superseded by a meal.
                JobDef next = __result.Job.def;
                switch (next.defName)
                {
                    case "Wait_Combat":
                    case "Flee":
                    case "FleeAndCower":
                    case "GetMedicalRest":
                    case "TendPatient":
                    case "Rescue":
                    case "ExtinguishSelf":
                    case "BeatFire":
                    case "Firefight":
                    case "AttackMelee":
                    case "AttackStatic":
                        return;
                }

                float food = pawn.needs.food.CurLevelPercentage;
                if (!BelowIndependentThreshold(food, settings.autoEatFoodTenths))
                {
                    LastMealOffers.Remove(pawn);
                    return;
                }
                int now = Find.TickManager.TicksGame;
                int previous;
                if (LastMealOffers.TryGetValue(pawn, out previous) &&
                    !CanOfferAgain(now, previous)) return;

                Job meal = ChildWakeFood.FindIndependentFoodJob(pawn);
                if (meal == null) return;

                // Change ONLY the pending think-tree choice. Never cancel,
                // queue or modify the job which just completed.
#if DEBUG
                string vanillaChoice = next.defName;
#endif
                __result = new ThinkResult(meal, null, null, false);
                __0 = null;
                LastMealOffers[pawn] = now;
#if DEBUG
                Log.Message("[ChildLearning][IndependentFood] selected-Ingest; child=" +
                    pawn.LabelShort + "; tick=" + now + "; Food=" +
                    (food * 100f).ToString("0.0") + "% ; threshold=" +
                    ((settings.autoEatFoodTenths == 0 ? 500 : settings.autoEatFoodTenths) / 10f).ToString("0.0") +
                    "% ; previous-job-ended=True; queued=False" +
                    "; vanillaChoice=" + vanillaChoice +
                    "; draftedAtOffer=" + pawn.Drafted +
                    "; insideDraftToggle=" + ChildDraftFoodTrace.ContextLabel(pawn));
#endif
            }
            catch (Exception ex)
            {
                if (!postfixErrorLogged)
                {
                    postfixErrorLogged = true;
                    Log.Warning("[QoL: Child Learning][EF1] Safe fallback " +
                        "to vanilla next job after food hook exception. " + ex);
                }
            }
        }

#if DEBUG
        internal static void SelfTest()
        {
            bool ok = !BelowIndependentThreshold(1f, 1000) &&
                BelowIndependentThreshold(0.60f, 600) &&
                !BelowIndependentThreshold(0.601f, 600) &&
                !BelowIndependentThreshold(0.90f, 0) &&
                BelowIndependentThreshold(0.50f, 0) &&
                !CanOfferAgain(15000, 1) && CanOfferAgain(15001, 1);
            if (ok) Log.Message("[QoL: Child Learning][EF1] independent food rules PASS (7/7).");
            else Log.Error("[QoL: Child Learning][EF1] independent food rules FAILED.");
        }
#endif
    }
}
