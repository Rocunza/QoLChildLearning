using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using ZCN.QoL.ChildLearning; // Current Child Learning services; namespace preserved for save compatibility.


namespace Tier_expand
{
    // Keep the fully-qualified legacy name used in saves from the old combined mod.
    // This is the ONLY tracker GameComponent; do not add a second alias subclass.
    public class ReadingStatusTracker : GameComponent
    {
        private const int ScanIntervalTicks = 250;

        private readonly Dictionary<Pawn, bool> readingStates =
            new Dictionary<Pawn, bool>();

        private readonly HashSet<Pawn> seenPawns =
            new HashSet<Pawn>();

        // Saved on the EXISTING legacy tracker component. Avoid a second
        // GameComponent type so pre-existing saves can be loaded unchanged.
        private Dictionary<Pawn, int> lastWakeTick = new Dictionary<Pawn, int>();
        // Scribe requires persistent working lists when dictionary keys use
        // LookMode.Reference. A local list in ExposeData would not survive
        // LoadingVars -> ResolvingCrossRefs; the 4-argument overload logs
        // "You need to provide working lists" and cannot restore cooldowns.
        private List<Pawn> wakeKeysWorkingList;
        private List<int> wakeValuesWorkingList;
        // V1: per-child policies stored on this same legacy component.
        // Missing entries use the configurable defaults; migration preserves older child states.
        private Dictionary<Pawn, int> childControls = new Dictionary<Pawn, int>();
        private List<Pawn> childControlsKeysWorkingList;
        private List<int> childControlsValuesWorkingList;
        private bool childControlsMigrated;
        // Preserve GIZMO2's implicit ON/OFF/OFF for children already in a save.
        // Otherwise changing new defaults before loading an old save would
        // unexpectedly change those children's behavior.
        private bool gizmo3ExistingChildrenSnapshotted;
        // Runtime-only one-shot guard: prevents a 0-cooldown wake/sleep loop.
        private readonly HashSet<Pawn> wakeDisarmed = new HashSet<Pawn>();
        private bool initialized;


        public ReadingStatusTracker(Game game)
        {
        }


        internal int GetChildControlMask(Pawn pawn)
        {
            int flags;
            if (pawn == null) return ChildGizmoPolicy.ConfiguredDefaultMask();
            if (childControls == null) childControls = new Dictionary<Pawn, int>();
            if (childControls.TryGetValue(pawn, out flags))
                return flags & ChildGizmoPolicy.All;
            int configured = ChildGizmoPolicy.ConfiguredDefaultMask();
            // Snapshot on first encounter: later changes to default settings
            // must not flip an already enrolled child's personal switches.
            if (ChildGizmoPolicy.IsEligible(pawn))
                childControls[pawn] = configured;
            return configured;
        }

        internal void SetChildControlMask(Pawn pawn, int flags)
        {
            if (pawn == null) return;
            if (childControls == null) childControls = new Dictionary<Pawn, int>();
            int oldMask = GetChildControlMask(pawn);
            int newMask = flags & ChildGizmoPolicy.All;
            childControls[pawn] = newMask;
            // Turning the assisted behavior off must release any one-shot
            // runtime state without erasing the persisted wake cooldown.
            if ((oldMask & ChildGizmoPolicy.Wake) != 0 &&
                (newMask & ChildGizmoPolicy.Wake) == 0)
            {
                wakeDisarmed.Remove(pawn);
                ChildWakeOutcome.Forget(pawn);
            }
            if ((oldMask & ChildGizmoPolicy.Food) != 0 &&
                (newMask & ChildGizmoPolicy.Food) == 0)
                ChildIndependentFood.Forget(pawn);
        }

        // ---------------------------------------------------------
        // Do not treat already-active Reading desires as "new"
        // when loading a save or starting a game.
        // ---------------------------------------------------------

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref lastWakeTick, "autoWakeLastTicks",
                LookMode.Reference, LookMode.Value,
                ref wakeKeysWorkingList, ref wakeValuesWorkingList);
            Scribe_Collections.Look(ref childControls, "zcnChildControlsV1",
                LookMode.Reference, LookMode.Value,
                ref childControlsKeysWorkingList, ref childControlsValuesWorkingList);
            Scribe_Values.Look(ref childControlsMigrated, "zcnChildControlsMigratedV1", false);
            Scribe_Values.Look(ref gizmo3ExistingChildrenSnapshotted,
                "zcnGizmo3ExistingChildrenSnapshottedV1", false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (lastWakeTick == null) lastWakeTick = new Dictionary<Pawn, int>();
                if (childControls == null) childControls = new Dictionary<Pawn, int>();
            }
        }

        public override void StartedNewGame()
        {
            if (lastWakeTick == null) lastWakeTick = new Dictionary<Pawn, int>();
            lastWakeTick.Clear();
            if (childControls == null) childControls = new Dictionary<Pawn, int>();
            childControls.Clear();
            childControlsMigrated = true;
            gizmo3ExistingChildrenSnapshotted = true;
            wakeDisarmed.Clear();
            ChildWakeOutcome.Reset();
            ChildIndependentFood.Reset();
#if DEBUG
            ChildWakeTrace.Reset();
#endif
            SeedCurrentStates();
        }

        public override void LoadedGame()
        {
            if (lastWakeTick == null) lastWakeTick = new Dictionary<Pawn, int>();
            if (childControls == null) childControls = new Dictionary<Pawn, int>();
            if (!childControlsMigrated)
            {
                // Pre-gizmo saves: honor the previous game behavior for
                // already-present colonist children. New arrivals get ON/OFF/OFF.
                foreach (Pawn pawn in PawnsFinder.AllMaps_FreeColonists)
                    if (ChildGizmoPolicy.IsEligible(pawn) && !childControls.ContainsKey(pawn))
                        childControls[pawn] = ChildGizmoPolicy.LegacyChildDefault;
                childControlsMigrated = true;
            }
            if (!gizmo3ExistingChildrenSnapshotted)
            {
                // One-time GIZMO2 -> GIZMO3 migration. GIZMO2 used an implicit
                // ON/OFF/OFF value for children without explicit entries.
                foreach (Pawn pawn in PawnsFinder.AllMaps_FreeColonists)
                    if (ChildGizmoPolicy.IsEligible(pawn) && !childControls.ContainsKey(pawn))
                        childControls[pawn] = ChildGizmoPolicy.NewChildDefault;
                gizmo3ExistingChildrenSnapshotted = true;
            }
            wakeDisarmed.Clear();
            ChildWakeOutcome.Reset();
            ChildIndependentFood.Reset();
#if DEBUG
            ChildWakeTrace.Reset();
#endif
            SeedCurrentStates();
#if DEBUG
            // One-time load check for the user's next save/reload experiment.
            Log.Message("[QoL: Child Learning][WakeSave] restoredCooldownEntries=" +
                lastWakeTick.Count + ".");
#endif
        }


        public override void GameComponentTick()
        {
            ChildWakeOutcome.Tick(wakeDisarmed, lastWakeTick);
#if DEBUG
            ChildWakeTrace.Tick();
#endif
            if (!initialized)
            {
                SeedCurrentStates();
                return;
            }

            if (Find.TickManager.TicksGame %
                ScanIntervalTicks != 0)
            {
                return;
            }

            ScanForChanges();
        }


        // =========================================================
        // Initial snapshot
        // =========================================================

        private void SeedCurrentStates()
        {
            readingStates.Clear();

            List<Pawn> pawns =
                PawnsFinder.AllMaps_FreeColonists;

            int readingActiveCount = 0;

            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];

                if (pawn == null)
                {
                    continue;
                }

                if (ChildGizmoPolicy.IsEligible(pawn))
                    GetChildControlMask(pawn); // persist defaults for every child

                bool readingActive =
                    ReadingLearningUtility.HasReadingDesire(
                        pawn
                    );

                readingStates[pawn] = readingActive;

                if (readingActive)
                {
                    readingActiveCount++;
                }
            }

            initialized = true;

            if (ChildLearningDebug.Enabled)
            {
                Log.Message(
                    "[QoL: Child Learning] DEBUG reading status " +
                    "tracker initialized. " +
                    "Tracked pawns = " +
                    readingStates.Count +
                    ", Reading active = " +
                    readingActiveCount +
                    "."
                );
            }
        }


        // =========================================================
        // Periodic transition scan
        // =========================================================

        private void ScanForChanges()
        {
            List<Pawn> pawns =
                PawnsFinder.AllMaps_FreeColonists;

            seenPawns.Clear();
            ChildIndependentFood.Reconcile(pawns);
            // Lazily allocate only when a NEW Reading desire actually activates.
            List<Pawn> newlyActiveReaders = null;

            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];

                if (pawn == null)
                {
                    continue;
                }

                seenPawns.Add(pawn);
                if (ChildGizmoPolicy.IsEligible(pawn))
                    GetChildControlMask(pawn); // new arrivals receive current defaults once

                // Gate is shared with the existing 250-tick scan; OFF = no
                // extra map loop, food search, or job manipulation.
                ChildAutoWake.TryWake(pawn, wakeDisarmed, lastWakeTick);

                bool current =
                    ReadingLearningUtility.HasReadingDesire(
                        pawn
                    );

                bool previous;

                // Newly encountered pawn:
                // establish baseline, but do NOT fire an activation.
                if (!readingStates.TryGetValue(
                        pawn,
                        out previous))
                {
                    readingStates[pawn] = current;
                    continue;
                }

                if (previous == current)
                {
                    continue;
                }

                readingStates[pawn] = current;

                if (ChildLearningDebug.Enabled)
                {
                    Log.Message(
                        "[QoL: Child Learning] DEBUG reading state " +
                        "transition: pawn=" +
                        pawn.LabelShort +
                        ", " +
                        previous +
                        " -> " +
                        current +
                        "."
                    );
                }

                if (!previous && current && ChildGizmoPolicy.ReadingEnabled(pawn))
                {
                    if (newlyActiveReaders == null)
                        newlyActiveReaders = new List<Pawn>(2);
                    newlyActiveReaders.Add(pawn);
                }
            }


            // One notification per 250-tick batch; never a sound per child.
            if (newlyActiveReaders != null)
                NotifyReadingActivated(newlyActiveReaders);

            // Remove pawns that are no longer free colonists.
            // Prevents stale references from accumulating forever.

            List<Pawn> removeList = null;

            foreach (KeyValuePair<Pawn, bool> pair
                in readingStates)
            {
                if (seenPawns.Contains(pair.Key))
                {
                    continue;
                }

                if (removeList == null)
                {
                    removeList = new List<Pawn>();
                }

                removeList.Add(pair.Key);
            }

            if (removeList != null)
            {
                for (int i = 0;
                    i < removeList.Count;
                    i++)
                {
                    readingStates.Remove(removeList[i]);
                    wakeDisarmed.Remove(removeList[i]);
                    ChildWakeOutcome.Forget(removeList[i]);
                    ChildIndependentFood.Forget(removeList[i]);
#if DEBUG
                    ChildWakeTrace.Forget(removeList[i]);
#endif
                    if (lastWakeTick != null) lastWakeTick.Remove(removeList[i]);
                }
            }
        }


        // =========================================================
        // Notification for genuine false -> true transitions only.
        // Saved/load baseline and newborn-pawn baseline do not notify.
        // =========================================================


        // A missing Keyed/ReadingAlerts.xml was observed to render a
        // pseudo-localized key (RAL_Reà...), dropping the pawn's name.
        // We normally use the game's translation system, but never let an
        // unresolved key reach the player. Keep all messages concise.
        private static string BuildReadingMessage(List<Pawn> pawns, out bool usedFallback)
        {
            string first = pawns[0].LabelShort;
            string translated;
            if (pawns.Count == 1)
                translated = "RAL_ReadingActiveOne".Translate(first).ToString();
            else if (pawns.Count == 2)
                translated = "RAL_ReadingActiveTwo".Translate(
                    first, pawns[1].LabelShort).ToString();
            else
                translated = "RAL_ReadingActiveMany".Translate(
                    first, (pawns.Count - 1).ToString()).ToString();

            // RimWorld intentionally pseudo-accentuates an unknown keyed
            // translation, while preserving its initial RAL_ prefix.
            // A valid translation must also contain the actual first pawn name.
            usedFallback = string.IsNullOrEmpty(translated) ||
                translated.StartsWith("RAL_", StringComparison.OrdinalIgnoreCase) ||
                translated.IndexOf(first, StringComparison.Ordinal) < 0 ||
                translated.IndexOf("{0}", StringComparison.Ordinal) >= 0;
            if (!usedFallback)
                return translated;

            bool korean = LanguageDatabase.activeLanguage != null &&
                string.Equals(LanguageDatabase.activeLanguage.folderName,
                    "Korean", StringComparison.OrdinalIgnoreCase);
            string prefix = korean ? "독서 가능: " : "Reading available: ";
            if (pawns.Count == 1)
                return prefix + first;
            if (pawns.Count == 2)
                return prefix + first + ", " + pawns[1].LabelShort;
            return korean
                ? prefix + first + " 외 " + (pawns.Count - 1) + "명"
                : prefix + first + " and " + (pawns.Count - 1) + " more";
        }

        private static void NotifyReadingActivated(List<Pawn> pawns)
        {
            if (pawns == null || pawns.Count == 0)
                return;

            ChildLearningSettings settings = ChildLearningMod.Settings;
            if (settings == null)
                return;

            // Defend against changes between detection and dispatch.
            pawns.RemoveAll(p => !ChildGizmoPolicy.ReadingEnabled(p));
            if (pawns.Count == 0) return;

            bool soundEnabled = settings.playReadingSound;
            bool messageEnabled = settings.showReadingActivationMessage;

            string displayedMessage = null;
            bool usedFallback = false;
            if (messageEnabled)
            {
                // SilentInput deliberately has no additional notification
                // sound: the selected one is played just once below.
                // Click target is the first newly eligible child.
                displayedMessage = BuildReadingMessage(pawns, out usedFallback);
                Messages.Message(displayedMessage, pawns[0],
                    MessageTypeDefOf.SilentInput, historical: false);
            }

            if (soundEnabled)
                ReadingSoundUtility.Play(settings.readingSoundChoice);

            if (ChildLearningDebug.Enabled)
            {
                Log.Message(
                    "[QoL: Child Learning] DEBUG Reading ACTIVATED: " +
                    "pawns=" + pawns.Count + ", first=" + pawns[0].LabelShort +
                    ", message=" + messageEnabled +
                    ", messageText=" + (displayedMessage ?? "(disabled)") +
                    ", translationFallback=" + usedFallback +
                    ", sound=" + soundEnabled +
                    ", choice=" + ReadingSoundUtility.GetDebugName(
                        settings.readingSoundChoice) + ".");
            }
        }
    }
}
