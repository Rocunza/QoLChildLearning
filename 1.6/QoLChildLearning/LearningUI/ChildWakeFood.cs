using System;
using RimWorld;
using Verse;
using Verse.AI;

namespace ZCN.QoL.ChildLearning
{
    // Use vanilla food restriction, inventory, social propriety, forbid,
    // reservation and reachability logic instead of hand-picking a meal.
    // No ThinkTree patch: queried once only when a child truly auto-wakes.
    internal static class ChildWakeFood
    {
        private sealed class VanillaFoodGiver : JobGiver_GetFood
        {
            internal Job Find(Pawn pawn)
            {
                return base.TryGiveJob(pawn);
            }
        }

        private static readonly VanillaFoodGiver FoodGiver = new VanillaFoodGiver();
        private static bool reportedFoodException;

        internal static Job FindIndependentFoodJob(Pawn pawn)
        {
            if (pawn == null || pawn.jobs == null || pawn.needs == null ||
                pawn.needs.food == null || FoodUtility.ShouldBeFedBySomeone(pawn))
                return null;
            if (pawn.jobs.jobQueue != null && pawn.jobs.jobQueue.Count > 0)
                return null;
            try
            {
                Job candidate = FoodGiver.Find(pawn);
                return candidate != null && candidate.def == JobDefOf.Ingest
                    ? candidate : null;
            }
            catch (Exception ex)
            {
                if (!reportedFoodException)
                {
                    reportedFoodException = true;
                    Log.Warning("[QoL: Child Learning][IndependentFood] " +
                        "Vanilla food selection failed; using vanilla AI. " + ex);
                }
                return null;
            }
        }

        internal static Job FindFoodJob(Pawn pawn, ChildLearningSettings settings,
            out string status)
        {
            status = "disabled";
            if (settings == null || !settings.autoEatAfterWake ||
                !ChildGizmoPolicy.FoodEnabled(pawn))
                return null;
            if (settings.autoEatAtJobTransition)
            {
                status = "handled-by-independent-food";
                return null;
            }
            if (pawn == null || pawn.jobs == null || pawn.needs == null ||
                pawn.needs.food == null)
            {
                status = "no-food-need";
                return null;
            }
            if (!ChildWakeRules.FoodBelowThreshold(
                pawn.needs.food.CurLevelPercentage, settings.autoEatFoodTenths))
            {
                status = "above-threshold";
                return null;
            }
            if (pawn.jobs.jobQueue != null && pawn.jobs.jobQueue.Count > 0)
            {
                // All preexisting queued commands get precedence. This also
                // prevents creating a duplicate when Ingest is already queued.
                status = "existing-queue";
                return null;
            }
            if (FoodUtility.ShouldBeFedBySomeone(pawn))
            {
                status = "requires-feeding";
                return null;
            }
            try
            {
                Job job = FoodGiver.Find(pawn);
                if (job == null)
                {
                    status = "no-permitted-food";
                    return null;
                }
                // Do not hijack first action for refilling dispensers,
                // plant harvesting or hunting. Only eating counts as eating.
                if (job.def != JobDefOf.Ingest)
                {
                    status = "vanilla-non-ingest";
                    return null;
                }
                status = "eligible";
                return job;
            }
            catch (Exception e)
            {
                status = "food-search-error";
                if (!reportedFoodException)
                {
                    reportedFoodException = true;
                    Log.Warning("[QoL: Child Learning][WakeFood] Vanilla food selection " +
                        "failed; fallback to normal AI. " + e);
                }
                return null;
            }
        }
    }
}
