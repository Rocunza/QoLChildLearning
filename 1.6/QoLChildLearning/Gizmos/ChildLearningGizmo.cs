using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ZCN.QoL.ChildLearning
{
    // Three direct per-child toggles. Global settings still decide whether each
    // feature is available at all; unavailable features do not appear.
    [HarmonyPatch(typeof(Pawn), "GetGizmos")]
    internal static class ChildLearningGizmo
    {
        private static string T(string key)
        {
            return ("ZCNCL_" + key).Translate().ToString();
        }

        [HarmonyPostfix]
        private static void Postfix(Pawn __instance, ref IEnumerable<Gizmo> __result)
        {
            if (!ChildGizmoPolicy.IsEligible(__instance))
                return;

            int allowed = ChildGizmoPolicy.AvailableMask(ChildLearningMod.Settings);
            if (allowed == 0)
                return;

            IEnumerable<Gizmo> original = __result;
            __result = Append(original, __instance, allowed);
        }

        private static IEnumerable<Gizmo> Append(IEnumerable<Gizmo> original, Pawn pawn, int allowed)
        {
            if (original != null)
                foreach (Gizmo gizmo in original)
                    yield return gizmo;

            if ((allowed & ChildGizmoPolicy.Reading) != 0)
                yield return MakeToggle(pawn, ChildGizmoPolicy.Reading,
                    "GizmoReadingLabel", "GizmoReadingDescription",
                    ChildGizmoTextures.Reading);

            if ((allowed & ChildGizmoPolicy.Wake) != 0)
                yield return MakeToggle(pawn, ChildGizmoPolicy.Wake,
                    "GizmoWakeLabel", "GizmoWakeDescription",
                    ChildGizmoTextures.Wake);

            if ((allowed & ChildGizmoPolicy.Food) != 0)
                yield return MakeToggle(pawn, ChildGizmoPolicy.Food,
                    "GizmoFoodLabel", "GizmoFoodDescription",
                    ChildGizmoTextures.Food);
        }

        // The threshold shown in each gizmo tooltip follows the current mod settings.
        // Mod settings store percentages in tenths (899 -> 89.9%).
        private static string ThresholdText(int tenths)
        {
            return (tenths / 10f).ToString("0.0", CultureInfo.InvariantCulture);
        }

        private static string TooltipDescription(int flag, string descKey)
        {
            string translated = T(descKey);
            ChildLearningSettings settings = ChildLearningMod.Settings;
            if (flag == ChildGizmoPolicy.Wake)
            {
                int tenths = settings != null ? settings.autoWakeLearningTenths : 899;
                return string.Format(CultureInfo.InvariantCulture, translated,
                    ThresholdText(tenths));
            }
            if (flag == ChildGizmoPolicy.Food)
            {
                int tenths = settings != null ? settings.autoEatFoodTenths : 550;
                return string.Format(CultureInfo.InvariantCulture, translated,
                    ThresholdText(tenths));
            }
            return translated;
        }

        private static Command_Toggle MakeToggle(Pawn pawn, int flag,
            string labelKey, string descKey, Texture2D icon)
        {
            return new Command_Toggle
            {
                defaultLabel = T(labelKey),
                defaultDesc = TooltipDescription(flag, descKey),
                icon = icon,
                isActive = delegate { return ChildGizmoPolicy.IsOn(pawn, flag); },
                toggleAction = delegate
                {
                    bool isOn = ChildGizmoPolicy.IsOn(pawn, flag);
                    ChildGizmoPolicy.Set(pawn, flag, !isOn);
                }
            };
        }
    }

    [StaticConstructorOnStartup]
    internal static class ChildGizmoTextures
    {
        internal static readonly Texture2D Reading = LearningDesireDefOf.Reading.Icon;
        internal static readonly Texture2D Wake =
            ContentFinder<Texture2D>.Get("QoLChildLearning/GizmoWake", true);
        internal static readonly Texture2D Food =
            ContentFinder<Texture2D>.Get("QoLChildLearning/GizmoFood", true);
    }
}
