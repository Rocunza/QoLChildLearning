using RimWorld;
using Verse;

namespace ZCN.QoL.ChildLearning
{
    public static class ReadingLearningUtility
    {
        /// <summary>
        /// Returns true when Reading is one of the pawn's
        /// currently active learning desires.
        ///
        /// This intentionally does NOT check:
        /// - whether a readable book currently exists
        /// - hunger / rest
        /// - pathing
        /// - whether Reading can start right now
        ///
        /// It represents the learning desire itself.
        /// </summary>
        public static bool HasReadingDesire(Pawn pawn)
        {
            if (pawn == null)
            {
                return false;
            }

            if (!ModsConfig.BiotechActive)
            {
                return false;
            }

            if (!pawn.DevelopmentalStage.Child())
            {
                return false;
            }

            if (pawn.learning == null)
            {
                return false;
            }

            if (pawn.learning.ActiveLearningDesires == null)
            {
                return false;
            }

            return pawn.learning.ActiveLearningDesires.Contains(
                LearningDesireDefOf.Reading
            );
        }
    }
}