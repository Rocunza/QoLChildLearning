#if DEBUG
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace ZCN.QoL.ChildLearning
{
    // EF1-DIAG1 ONLY: no gameplay edits. Compare draft requested state
    // with the actual drafted state while the think tree chooses a job.
    // Prefix and postfix are observational; no job is created or changed here.
    internal static class ChildDraftFoodTrace
    {
        private sealed class Transition
        {
            internal bool Old;
            internal bool Target;
            internal int Tick;
        }

        private static readonly Dictionary<Pawn, Transition> Active =
            new Dictionary<Pawn, Transition>();
        private static FieldInfo pawnField;
        private static bool errorReported;

        internal static void Reset() { Active.Clear(); }

        internal static void Install(Harmony harmony)
        {
            try
            {
                MethodInfo setter = AccessTools.PropertySetter(
                    typeof(Pawn_DraftController), "Drafted");
                pawnField = AccessTools.Field(typeof(Pawn_DraftController), "pawn");
                if (setter == null || pawnField == null)
                {
                    Log.Warning("[ChildLearning][DraftFoodTrace] diagnostic SKIPPED: " +
                        "1.6 setter or controller pawn field not found; " +
                        "EF1 gameplay remains unchanged.");
                    return;
                }

                harmony.Patch(setter,
                    prefix: new HarmonyMethod(typeof(ChildDraftFoodTrace),
                        "BeforeDraftChange"),
                    postfix: new HarmonyMethod(typeof(ChildDraftFoodTrace),
                        "AfterDraftChange"));
                Log.Message("[ChildLearning][DraftFoodTrace] diagnostic hook installed; " +
                    "read-only DEBUG, no job/AI behavior changes.");
            }
            catch (Exception ex)
            {
                Log.Warning("[ChildLearning][DraftFoodTrace] optional diagnostic " +
                    "not installed; EF1 gameplay unchanged. " + ex);
            }
        }

        private static Pawn PawnOf(Pawn_DraftController controller)
        {
            return controller == null || pawnField == null ? null :
                pawnField.GetValue(controller) as Pawn;
        }

        private static bool IsChild(Pawn pawn)
        {
            return pawn != null && !pawn.Destroyed && pawn.Faction == Faction.OfPlayer &&
                pawn.needs != null && pawn.needs.learning != null;
        }

        private static void BeforeDraftChange(Pawn_DraftController __instance, bool __0)
        {
            try
            {
                Pawn pawn = PawnOf(__instance);
                if (!IsChild(pawn)) return;
                int tick = Find.TickManager != null ? Find.TickManager.TicksGame : -1;
                bool old = pawn.Drafted;
                Active[pawn] = new Transition { Old = old, Target = __0, Tick = tick };
                Log.Message("[ChildLearning][DraftFoodTrace][BEGIN] child=" + pawn.LabelShort +
                    "; tick=" + tick + "; from=" + old +
                    "; requested=" + __0 + "; direction=" +
                    (!old && __0 ? "ON" : old && !__0 ? "OFF" : "NO-CHANGE") +
                    "; currentJob=" + JobName(pawn.CurJob) +
                    "; Food=" + FoodLabel(pawn) +
                    "; queue=" + QueueCount(pawn));
            }
            catch (Exception ex) { ReportError(ex); }
        }

        private static void AfterDraftChange(Pawn_DraftController __instance, bool __0)
        {
            try
            {
                Pawn pawn = PawnOf(__instance);
                if (!IsChild(pawn)) return;
                Transition t;
                if (!Active.TryGetValue(pawn, out t)) return;
                Active.Remove(pawn);
                Log.Message("[ChildLearning][DraftFoodTrace][END] child=" + pawn.LabelShort +
                    "; tick=" + CurrentTick() + "; from=" + t.Old +
                    "; requested=" + t.Target + "; finalDrafted=" + pawn.Drafted +
                    "; currentJob=" + JobName(pawn.CurJob) +
                    "; queue=" + QueueCount(pawn));
            }
            catch (Exception ex) { ReportError(ex); }
        }

        // Called from EF1's already-existing selection postfix. The trace
        // fires only while a tracked child is inside the draft setter.
        internal static void ObserveChoice(Pawn pawn, ThinkResult vanillaResult,
            ThinkTreeDef tree)
        {
            if (pawn == null || Active.Count == 0) return;
            Transition t;
            if (!Active.TryGetValue(pawn, out t) || t.Tick != CurrentTick()) return;
            try
            {
                Log.Message("[ChildLearning][DraftFoodTrace][PICK] child=" + pawn.LabelShort +
                    "; tick=" + t.Tick + "; requested=" + t.Target +
                    "; draftedAtPick=" + pawn.Drafted +
                    "; vanillaChoice=" + (vanillaResult.IsValid ?
                        JobName(vanillaResult.Job) : "none") +
                    "; fromQueue=" + vanillaResult.FromQueue +
                    "; constantTree=" + (pawn.thinker != null &&
                        tree != null && tree == pawn.thinker.ConstantThinkTree) +
                    "; currentJob=" + JobName(pawn.CurJob) +
                    "; queue=" + QueueCount(pawn));
            }
            catch (Exception ex) { ReportError(ex); }
        }

        internal static string ContextLabel(Pawn pawn)
        {
            if (pawn == null || Active.Count == 0) return "none";
            Transition t;
            return Active.TryGetValue(pawn, out t) && t.Tick == CurrentTick() ?
                (!t.Old && t.Target ? "ON" : t.Old && !t.Target ? "OFF" : "NO-CHANGE") :
                "none";
        }

        private static int CurrentTick()
        {
            return Find.TickManager != null ? Find.TickManager.TicksGame : -1;
        }

        private static string JobName(Job job)
        {
            return job != null && job.def != null ? job.def.defName : "none";
        }

        private static string QueueCount(Pawn pawn)
        {
            return pawn.jobs != null && pawn.jobs.jobQueue != null ?
                pawn.jobs.jobQueue.Count.ToString() : "n/a";
        }

        private static string FoodLabel(Pawn pawn)
        {
            return pawn.needs.food != null ?
                (pawn.needs.food.CurLevelPercentage * 100f).ToString("0.0") + "%" : "n/a";
        }

        private static void ReportError(Exception ex)
        {
            if (errorReported) return;
            errorReported = true;
            Log.Warning("[ChildLearning][DraftFoodTrace] diagnostic-only error; " +
                "EF1 behavior unchanged. " + ex);
        }
    }
}
#endif
