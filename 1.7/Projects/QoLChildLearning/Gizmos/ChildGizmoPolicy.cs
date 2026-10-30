using RimWorld;
using Tier_expand;
using Verse;

namespace ZCN.QoL.ChildLearning
{
    // Two-layer permission model: global ModSettings AND per-child GameComponent.
    // Untracked children use configured defaults; old saves are migrated separately.
    internal static class ChildGizmoPolicy
    {
        internal const int Reading = 1;
        internal const int Wake = 2;
        internal const int Food = 4;
        internal const int All = Reading | Wake | Food;
        // Compatibility baseline for children already present in GIZMO2 saves.
        internal const int NewChildDefault = Reading;
        internal static int ConfiguredDefaultMask()
        {
            ChildLearningSettings s = ChildLearningMod.Settings;
            if (s == null) return NewChildDefault;
            return (s.gizmoDefaultReading ? Reading : 0) |
                   (s.gizmoDefaultWake ? Wake : 0) |
                   (s.gizmoDefaultFood ? Food : 0);
        }
        internal const int LegacyChildDefault = All;

        internal static bool IsEligible(Pawn pawn)
        {
            return pawn != null && pawn.Faction == Faction.OfPlayer &&
                pawn.RaceProps != null && pawn.RaceProps.Humanlike &&
                pawn.IsColonist && pawn.needs != null && pawn.needs.learning != null;
        }

        internal static int AvailableMask(ChildLearningSettings s)
        {
            if (s == null) return 0;
            int mask = 0;
            if (s.showReadingIcon || s.playReadingSound ||
                s.showReadingActivationMessage) mask |= Reading;
            if (s.autoWakeChildren) mask |= Wake;
            if (s.autoEatAtJobTransition || (s.autoWakeChildren && s.autoEatAfterWake))
                mask |= Food;
            return mask;
        }

        internal static int RawMask(Pawn pawn)
        {
            if (pawn == null) return ConfiguredDefaultMask();
            ReadingStatusTracker tracker = Current.Game != null
                ? Current.Game.GetComponent<ReadingStatusTracker>() : null;
            return tracker != null ? tracker.GetChildControlMask(pawn) : ConfiguredDefaultMask();
        }

        internal static bool IsOn(Pawn pawn, int bit)
        {
            return (RawMask(pawn) & bit) != 0;
        }

        internal static bool ReadingEnabled(Pawn pawn)
        {
            return IsEligible(pawn) &&
                (AvailableMask(ChildLearningMod.Settings) & Reading) != 0 &&
                IsOn(pawn, Reading);
        }

        internal static bool WakeEnabled(Pawn pawn)
        {
            return IsEligible(pawn) && ChildLearningMod.Settings != null &&
                ChildLearningMod.Settings.autoWakeChildren && IsOn(pawn, Wake);
        }

        internal static bool FoodEnabled(Pawn pawn)
        {
            return IsEligible(pawn) &&
                (AvailableMask(ChildLearningMod.Settings) & Food) != 0 &&
                IsOn(pawn, Food);
        }

        internal static void Set(Pawn pawn, int bit, bool enabled)
        {
            if (!IsEligible(pawn) || (bit != Reading && bit != Wake && bit != Food))
                return;
            ReadingStatusTracker tracker = Current.Game != null
                ? Current.Game.GetComponent<ReadingStatusTracker>() : null;
            if (tracker == null) return;
            int mask = tracker.GetChildControlMask(pawn);
            tracker.SetChildControlMask(pawn,
                enabled ? mask | bit : mask & ~bit);
        }

    }
}
