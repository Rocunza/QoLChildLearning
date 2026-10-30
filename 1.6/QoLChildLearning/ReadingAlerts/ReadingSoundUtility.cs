using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.Sound;

namespace ZCN.QoL.ChildLearning
{
    // UI labels and stored IDs are independent. Keep the existing Scribe
    // value meanings: 0=Subtle tick, 1=Tiny bell, 2=Clear alert,
    // 7=Positive event, 8=Neutral event. Removed AL3 IDs are not reused.
    internal sealed class ReadingSoundOption
    {
        internal readonly int Id;
        internal readonly string Label;
        internal readonly string DefName;

        internal ReadingSoundOption(int id, string label, string defName)
        {
            Id = id;
            Label = label;
            DefName = defName;
        }
    }

    public static class ReadingSoundUtility
    {
        public const int SubtleTick = 0;
        public const int TinyBell = 1;
        public const int ClearAlert = 2;
        public const int MessagePositive = 7;
        public const int MessageNeutral = 8;
        public const int DefaultChoice = MessagePositive;

        // AL4: Exactly five displayed choices, in the user's requested order.
        private static readonly ReadingSoundOption[] Options =
        {
            new ReadingSoundOption(TinyBell, "ZCNCL_SoundTinyBell", "TinyBell"),
            new ReadingSoundOption(SubtleTick, "ZCNCL_SoundSubtleTick", "Tick_High"),
            new ReadingSoundOption(ClearAlert, "ZCNCL_SoundClearAlert", "LetterArrive_Good"),
            new ReadingSoundOption(MessagePositive, "ZCNCL_SoundPositive", "Message_PositiveEvent"),
            new ReadingSoundOption(MessageNeutral, "ZCNCL_SoundNeutral", "Message_NeutralEvent")
        };

        public static int NormalizeChoice(int choice)
        {
            // Keep all five valid previous selections. Deleted AL3 options
            // (including old Warning alert ID 3) migrate to the new default.
            switch (choice)
            {
                case SubtleTick:
                case TinyBell:
                case ClearAlert:
                case MessagePositive:
                case MessageNeutral:
                    return choice;
                default:
                    return DefaultChoice;
            }
        }

        internal static IEnumerable<ReadingSoundOption> AvailableOptions()
        {
            // All five are known on the user's RimWorld 1.6 test setup.
            // Always display exactly five choices, regardless of load phase;
            // playback itself has an audible fallback if a Def is missing.
            for (int i = 0; i < Options.Length; i++)
                yield return Options[i];
        }

        private static string GetSoundDefName(int choice)
        {
            int normalized = NormalizeChoice(choice);
            for (int i = 0; i < Options.Length; i++)
            {
                if (Options[i].Id == normalized)
                    return Options[i].DefName;
            }
            return "Message_PositiveEvent";
        }

        public static void Play(int choice)
        {
            int normalized = NormalizeChoice(choice);
            if (normalized == TinyBell)
            {
                SoundDefOf.TinyBell.PlayOneShotOnCamera();
                return;
            }
            if (normalized == SubtleTick)
            {
                SoundDefOf.Tick_High.PlayOneShotOnCamera();
                return;
            }

            SoundDef sound = DefDatabase<SoundDef>.GetNamedSilentFail(
                GetSoundDefName(normalized));
            if (sound == null)
                sound = DefDatabase<SoundDef>.GetNamedSilentFail("Message_PositiveEvent");
            if (sound == null)
                sound = DefDatabase<SoundDef>.GetNamedSilentFail("LetterArrive_Good");
            if (sound != null)
                sound.PlayOneShotOnCamera();
            else
                SoundDefOf.TinyBell.PlayOneShotOnCamera();
        }

        public static string GetDebugName(int choice)
        {
            return GetSoundDefName(choice);
        }
    }
}
