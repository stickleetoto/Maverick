#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using MaverickFresh.Combat.Legacy;
using MaverickFresh.Combat.Targeting;
using UnityEditor;
using UnityEngine;

namespace MaverickFresh.Combat.EditorTools
{
    /// <summary>
    /// Deterministic validation for radar track/lock R0.
    ///
    /// Lock semantics are entirely about elapsed time, so every case drives the controller with an
    /// explicit clock and explicit step deltas rather than waiting on frames. Nothing here depends on
    /// a scene, on physics, or on a real sensor: tracks are produced by a scripted feed whose
    /// observation times the test controls, which is the only way acquisition and coast timing can be
    /// asserted exactly.
    ///
    /// Deliberately not tested, because none of it exists: missile guidance, seekers, autopilots,
    /// datalink fusion, ECM, clutter, PRF, notching.
    /// </summary>
    public static class MavTrackLockValidation
    {
        /// <summary>
        /// A feed the test drives, with an observation time the test chooses.
        ///
        /// The owner stamps `observedAtTime` from `Time.time`, which does not advance in the editor, so
        /// staleness is produced by moving the CONTROLLER's clock forward instead. That is the same
        /// arithmetic the controller performs at runtime - age is now minus observedAtTime - so the
        /// timing being asserted is the real timing.
        /// </summary>
        private sealed class ScriptedFeed : IMavTargetObservationFeed
        {
            public readonly List<MavTrackObservation> pending = new List<MavTrackObservation>();
            public bool active = true;

            public bool IsFeedActive { get { return active; } }
            public MavTrackSource FeedSource { get { return MavTrackSource.Radar; } }

            public int CollectObservations(List<MavTrackObservation> into)
            {
                for (int i = 0; i < pending.Count; i++)
                    into.Add(pending[i]);
                return pending.Count;
            }

            public void Set(int key, Vector3 position, MavTrackQuality quality, bool hasVelocity)
            {
                pending.Clear();
                Add(key, position, quality, hasVelocity);
            }

            public void Add(int key, Vector3 position, MavTrackQuality quality, bool hasVelocity)
            {
                MavTrackObservation o = new MavTrackObservation();
                o.sourceKey = key;
                o.source = MavTrackSource.Radar;
                o.quality = quality;
                o.position = position;
                o.hasVelocity = hasVelocity;
                o.velocityMps = hasVelocity ? new Vector3(0f, 0f, 200f) : Vector3.zero;
                o.displayName = "TGT" + key;
                o.team = 2;
                o.isAirTarget = true;
                pending.Add(o);
            }
        }

        [MenuItem("Maverick/Combat/Run Track Lock Validation")]
        public static void RunFromMenu()
        {
            int passed, failed;
            string report = RunAll(out passed, out failed);
            Debug.Log(report);
            EditorUtility.DisplayDialog("Track Lock Validation",
                (failed == 0 ? "PASS" : "FAIL") + "  passed=" + passed + " failed=" + failed, "OK");
        }

        public static string RunAll(out int passed, out int failed)
        {
            passed = 0;
            failed = 0;
            StringBuilder report = new StringBuilder();
            report.AppendLine("Maverick Radar Track/Lock Validation R0");
            report.AppendLine("=======================================");

            ValidateVocabulary(report, ref passed, ref failed);
            ValidateAcquisition(report, ref passed, ref failed);
            ValidateMaintenanceAndCoast(report, ref passed, ref failed);
            ValidateLossReasons(report, ref passed, ref failed);
            ValidateSelection(report, ref passed, ref failed);
            ValidateStalledSelection(report, ref passed, ref failed);
            ValidateSelectionOwnership(report, ref passed, ref failed);
            ValidateEffectiveQuality(report, ref passed, ref failed);
            ValidateEngagementPrecedence(report, ref passed, ref failed);
            ValidateMigrationSwitch(report, ref passed, ref failed);
            ValidateLegacyEquivalence(report, ref passed, ref failed);

            report.AppendLine("RESULT: " + (failed == 0 ? "PASS" : "FAIL")
                              + " passed=" + passed + " failed=" + failed);
            return report.ToString();
        }

        // ---- vocabulary -------------------------------------------------------------------------
        private static void ValidateVocabulary(StringBuilder report, ref int passed, ref int failed)
        {
            Record(MavLockState.Idle < MavLockState.Selected
                   && MavLockState.Selected < MavLockState.Acquiring
                   && MavLockState.Acquiring < MavLockState.Locked,
                   "L-001", "lock states order by commitment, so precedence is expressible",
                   report, ref passed, ref failed);

            Record(MavLockLossReason.None == 0,
                   "L-002", "no loss reason is the default, so an unset value cannot look like a real loss",
                   report, ref passed, ref failed);

            Record(MavTrackQuality.Locked > MavTrackQuality.Tracked
                   && MavTrackQuality.Tracked > MavTrackQuality.Coarse,
                   "L-003", "Locked is the strongest track quality",
                   report, ref passed, ref failed);
        }

        // ---- acquisition ------------------------------------------------------------------------
        private static void ValidateAcquisition(StringBuilder report, ref int passed, ref int failed)
        {
            GameObject host = new GameObject("MavLockAcquisitionHost");
            try
            {
                MavTargetTrackOwner owner;
                MavTrackLockController lockCtl;
                ScriptedFeed feed;
                Build(host, out owner, out lockCtl, out feed);

                // A good, fresh track: selection happens, then acquisition runs for exactly the
                // configured time before a lock exists.
                feed.Set(1, new Vector3(0f, 0f, 3000f), MavTrackQuality.Tracked, true);
                owner.SweepNowForTesting();
                lockCtl.UseTestClock(Time.time);

                lockCtl.Step(0f);
                Record(lockCtl.SelectedTrackId != 0,
                       "L-010", "a usable track is selected",
                       report, ref passed, ref failed);
                Record(lockCtl.LockState == MavLockState.Acquiring,
                       "L-011", "selection of a fresh, good track begins acquisition",
                       report, ref passed, ref failed);
                Record(lockCtl.LockedTrackId == 0,
                       "L-012", "selection alone is not a lock",
                       report, ref passed, ref failed);

                // Halfway through: still acquiring, progress reported.
                lockCtl.Step(0.5f);
                Record(lockCtl.LockState == MavLockState.Acquiring
                       && lockCtl.AcquisitionProgress01 > 0.3f
                       && lockCtl.AcquisitionProgress01 < 0.6f,
                       "L-013", "acquisition reports partial progress before completing",
                       report, ref passed, ref failed);
                Record(lockCtl.LockedTrackId == 0,
                       "L-013b", "a partially acquired track is not locked",
                       report, ref passed, ref failed);

                // Past the acquisition time: locked.
                lockCtl.Step(0.7f);
                Record(lockCtl.LockState == MavLockState.Locked,
                       "L-014", "holding a good track past the acquisition time establishes the lock",
                       report, ref passed, ref failed);
                Record(lockCtl.LockedTrackId == lockCtl.SelectedTrackId,
                       "L-014b", "the locked track is the selected track",
                       report, ref passed, ref failed);
                Record(Mathf.Approximately(lockCtl.AcquisitionProgress01, 1f),
                       "L-014c", "a locked engagement reports full acquisition progress",
                       report, ref passed, ref failed);

                MavTargetTrackData locked;
                Record(lockCtl.TryGetLockedTrack(out locked) && locked.IsValid,
                       "L-015", "the authority hands out the locked track",
                       report, ref passed, ref failed);
            }
            finally { Object.DestroyImmediate(host); }

            // Interrupted acquisition must restart rather than resume.
            GameObject host2 = new GameObject("MavLockInterruptHost");
            try
            {
                MavTargetTrackOwner owner;
                MavTrackLockController lockCtl;
                ScriptedFeed feed;
                Build(host2, out owner, out lockCtl, out feed);

                feed.Set(1, new Vector3(0f, 0f, 3000f), MavTrackQuality.Tracked, true);
                owner.SweepNowForTesting();
                lockCtl.UseTestClock(Time.time);

                lockCtl.Step(0f);
                lockCtl.Step(0.9f);
                Record(lockCtl.LockState == MavLockState.Acquiring,
                       "L-016", "acquisition is in progress just short of completion",
                       report, ref passed, ref failed);

                // The track goes stale mid-acquisition.
                lockCtl.AdvanceTestClock(2f);
                lockCtl.Step(0.05f);
                Record(lockCtl.LockState == MavLockState.Selected
                       && lockCtl.AcquisitionProgress01 < 0.01f,
                       "L-017", "an interrupted acquisition restarts instead of resuming",
                       report, ref passed, ref failed);

                // Even after the track is fresh again, a full acquisition time is required.
                feed.Set(1, new Vector3(0f, 0f, 3100f), MavTrackQuality.Tracked, true);
                owner.SweepNowForTesting();
                lockCtl.UseTestClock(Time.time);
                lockCtl.Step(0.9f);
                Record(lockCtl.LockState == MavLockState.Acquiring,
                       "L-017b", "a flickering target cannot accumulate a lock from partial attempts",
                       report, ref passed, ref failed);

                // A poor-quality track cannot begin acquisition at all.
                lockCtl.BreakLock(MavLockLossReason.Commanded);
                feed.Set(2, new Vector3(0f, 0f, 3000f), MavTrackQuality.Coarse, false);
                owner.ClearTracksForTesting();
                owner.SweepNowForTesting();
                lockCtl.UseTestClock(Time.time);
                lockCtl.Step(0f);
                lockCtl.Step(5f);
                Record(lockCtl.LockState != MavLockState.Locked,
                       "L-018", "a track below the minimum lock quality never locks, however long it is held",
                       report, ref passed, ref failed);
                Record(lockCtl.LockState == MavLockState.Selected,
                       "L-018b", "such a track is still selected, just not lockable",
                       report, ref passed, ref failed);
            }
            finally { Object.DestroyImmediate(host2); }
        }

        // ---- maintenance and coast ---------------------------------------------------------------
        private static void ValidateMaintenanceAndCoast(StringBuilder report, ref int passed, ref int failed)
        {
            GameObject host = new GameObject("MavLockCoastHost");
            try
            {
                MavTargetTrackOwner owner;
                MavTrackLockController lockCtl;
                ScriptedFeed feed;
                Build(host, out owner, out lockCtl, out feed);

                feed.Set(1, new Vector3(0f, 0f, 3000f), MavTrackQuality.Tracked, true);
                owner.SweepNowForTesting();
                lockCtl.UseTestClock(Time.time);
                lockCtl.Step(0f);
                lockCtl.Step(1.2f);
                Record(lockCtl.LockState == MavLockState.Locked,
                       "L-020", "a lock is established for the coast cases",
                       report, ref passed, ref failed);

                int lockedId = lockCtl.LockedTrackId;
                float inLockBefore = lockCtl.TimeInLockSeconds;

                // Time passes with no new observation: the track goes stale and the lock coasts.
                lockCtl.AdvanceTestClock(1.0f);
                lockCtl.Step(1.0f);
                Record(lockCtl.LockState == MavLockState.Coasting,
                       "L-021", "a stale track puts the lock into coast rather than dropping it",
                       report, ref passed, ref failed);
                Record(lockCtl.LockedTrackId == lockedId,
                       "L-021b", "a coasting lock still reports the same locked track",
                       report, ref passed, ref failed);
                Record(lockCtl.TimeInLockSeconds > inLockBefore,
                       "L-021c", "time in lock keeps accumulating while coasting",
                       report, ref passed, ref failed);

                MavTargetTrackData coasted;
                Record(lockCtl.TryGetLockedTrack(out coasted),
                       "L-021d", "a coasting lock still hands out its track, so an engagement is not dropped on one missed update",
                       report, ref passed, ref failed);

                // A fresh observation resumes the lock without re-acquiring.
                feed.Set(1, new Vector3(0f, 0f, 3200f), MavTrackQuality.Tracked, true);
                owner.SweepNowForTesting();
                lockCtl.UseTestClock(Time.time);
                lockCtl.Step(0.05f);
                Record(lockCtl.LockState == MavLockState.Locked,
                       "L-022", "a fresh observation resumes the lock without re-acquiring",
                       report, ref passed, ref failed);
                Record(lockCtl.LockedTrackId == lockedId,
                       "L-022b", "resuming keeps the same locked track",
                       report, ref passed, ref failed);

                // Coast longer than the allowance: the lock is lost.
                lockCtl.AdvanceTestClock(1.0f);
                lockCtl.Step(1.0f);
                Record(lockCtl.LockState == MavLockState.Coasting,
                       "L-023", "the lock coasts again when the track goes stale",
                       report, ref passed, ref failed);

                lockCtl.AdvanceTestClock(2.5f);
                lockCtl.Step(2.5f);
                Record(lockCtl.LockState != MavLockState.Locked && lockCtl.LockState != MavLockState.Coasting,
                       "L-024", "a coast that outlasts its allowance loses the lock",
                       report, ref passed, ref failed);
                Record(lockCtl.LastLossReason == MavLockLossReason.CoastExpired,
                       "L-024b", "the loss is recorded as CoastExpired rather than being left to inference",
                       report, ref passed, ref failed);
                Record(lockCtl.LockedTrackId == 0,
                       "L-024c", "a lost lock reports no locked track",
                       report, ref passed, ref failed);
                Record(Mathf.Approximately(lockCtl.TimeInLockSeconds, 0f),
                       "L-024d", "time in lock resets once the lock is lost",
                       report, ref passed, ref failed);
                Record(lockCtl.SelectedTrackId != 0,
                       "L-024e", "the selection survives a lost lock, because losing a lock is not losing interest",
                       report, ref passed, ref failed);
            }
            finally { Object.DestroyImmediate(host); }
        }

        // ---- loss reasons ------------------------------------------------------------------------
        private static void ValidateLossReasons(StringBuilder report, ref int passed, ref int failed)
        {
            // Quality collapse.
            GameObject host = new GameObject("MavLockQualityLossHost");
            try
            {
                MavTargetTrackOwner owner;
                MavTrackLockController lockCtl;
                ScriptedFeed feed;
                Build(host, out owner, out lockCtl, out feed);

                feed.Set(1, new Vector3(0f, 0f, 3000f), MavTrackQuality.Tracked, true);
                owner.SweepNowForTesting();
                lockCtl.UseTestClock(Time.time);
                lockCtl.Step(0f);
                lockCtl.Step(1.2f);
                Record(lockCtl.LockState == MavLockState.Locked,
                       "L-030", "a lock is established for the quality-loss case",
                       report, ref passed, ref failed);

                // The same track, now reported at a lower quality.
                feed.Set(1, new Vector3(0f, 0f, 3000f), MavTrackQuality.Coarse, false);
                owner.SweepNowForTesting();
                lockCtl.UseTestClock(Time.time);
                lockCtl.Step(0.05f);
                Record(lockCtl.LockState != MavLockState.Locked,
                       "L-031", "quality falling below the lock minimum loses the lock",
                       report, ref passed, ref failed);
                Record(lockCtl.LastLossReason == MavLockLossReason.QualityTooLow,
                       "L-031b", "the loss is recorded as QualityTooLow",
                       report, ref passed, ref failed);
            }
            finally { Object.DestroyImmediate(host); }

            // Track dropped entirely.
            GameObject host2 = new GameObject("MavLockDropLossHost");
            try
            {
                MavTargetTrackOwner owner;
                MavTrackLockController lockCtl;
                ScriptedFeed feed;
                Build(host2, out owner, out lockCtl, out feed);

                feed.Set(1, new Vector3(0f, 0f, 3000f), MavTrackQuality.Tracked, true);
                owner.SweepNowForTesting();
                lockCtl.UseTestClock(Time.time);
                lockCtl.Step(0f);
                lockCtl.Step(1.2f);
                Record(lockCtl.LockState == MavLockState.Locked,
                       "L-032", "a lock is established for the track-dropped case",
                       report, ref passed, ref failed);

                owner.ClearTracksForTesting();
                lockCtl.Step(0.05f);
                Record(lockCtl.LockState == MavLockState.Idle,
                       "L-033", "the track disappearing entirely ends the engagement",
                       report, ref passed, ref failed);
                Record(lockCtl.LastLossReason == MavLockLossReason.TrackDropped,
                       "L-033b", "the loss is recorded as TrackDropped",
                       report, ref passed, ref failed);
                Record(lockCtl.SelectedTrackId == 0,
                       "L-033c", "no selection survives a track that no longer exists",
                       report, ref passed, ref failed);
            }
            finally { Object.DestroyImmediate(host2); }

            // Commanded break, and authority disabled.
            GameObject host3 = new GameObject("MavLockCommandedLossHost");
            try
            {
                MavTargetTrackOwner owner;
                MavTrackLockController lockCtl;
                ScriptedFeed feed;
                Build(host3, out owner, out lockCtl, out feed);

                feed.Set(1, new Vector3(0f, 0f, 3000f), MavTrackQuality.Tracked, true);
                owner.SweepNowForTesting();
                lockCtl.UseTestClock(Time.time);
                lockCtl.Step(0f);
                lockCtl.Step(1.2f);

                lockCtl.BreakLock(MavLockLossReason.Commanded);
                Record(lockCtl.LockState == MavLockState.Idle
                       && lockCtl.LastLossReason == MavLockLossReason.Commanded
                       && lockCtl.LockedTrackId == 0,
                       "L-034", "a commanded break clears the engagement and records the reason",
                       report, ref passed, ref failed);

                // Re-acquire, then disable the authority.
                lockCtl.Step(0f);
                lockCtl.Step(1.2f);
                Record(lockCtl.LockState == MavLockState.Locked,
                       "L-035", "the engagement can be re-established after a commanded break",
                       report, ref passed, ref failed);

                lockCtl.enableLockAuthority = false;
                lockCtl.Step(0.05f);
                Record(lockCtl.LockState == MavLockState.Idle
                       && lockCtl.LastLossReason == MavLockLossReason.AuthorityUnavailable
                       && !lockCtl.IsLockAuthorityActive,
                       "L-036", "disabling the authority drops the lock and says why",
                       report, ref passed, ref failed);

                lockCtl.enableLockAuthority = true;
            }
            finally { Object.DestroyImmediate(host3); }
        }

        // ---- selection --------------------------------------------------------------------------
        private static void ValidateSelection(StringBuilder report, ref int passed, ref int failed)
        {
            GameObject host = new GameObject("MavLockSelectionHost");
            try
            {
                MavTargetTrackOwner owner;
                MavTrackLockController lockCtl;
                ScriptedFeed feed;
                Build(host, out owner, out lockCtl, out feed);
                host.transform.position = Vector3.zero;

                // Two tracks: one nearer with lower quality, one further with higher quality.
                feed.pending.Clear();
                feed.Add(1, new Vector3(0f, 0f, 1000f), MavTrackQuality.Coarse, false);
                feed.Add(2, new Vector3(0f, 0f, 8000f), MavTrackQuality.Tracked, true);
                owner.SweepNowForTesting();
                lockCtl.UseTestClock(Time.time);
                lockCtl.Step(0f);

                MavTargetTrackData selected;
                bool haveSelected = owner.TryGetTrackById(lockCtl.SelectedTrackId, out selected);
                Record(haveSelected && selected.quality == MavTrackQuality.Tracked,
                       "L-040", "automatic selection prefers quality over proximity",
                       report, ref passed, ref failed);

                // An explicit request wins over the automatic rule.
                int otherId = 0;
                for (int i = 0; i < owner.TrackCount; i++)
                    if (owner.GetTrack(i).trackId != lockCtl.SelectedTrackId)
                        otherId = owner.GetTrack(i).trackId;

                Record(lockCtl.RequestSelection(otherId) && lockCtl.SelectedTrackId == otherId,
                       "L-041", "an explicit selection request overrides the automatic choice",
                       report, ref passed, ref failed);

                // Re-requesting the same track is a no-op, not a restart.
                feed.Set(2, new Vector3(0f, 0f, 8000f), MavTrackQuality.Tracked, true);
                owner.ClearTracksForTesting();
                owner.SweepNowForTesting();
                lockCtl.UseTestClock(Time.time);
                lockCtl.Step(0f);
                int stableId = lockCtl.SelectedTrackId;
                lockCtl.Step(0.6f);
                float progressBefore = lockCtl.AcquisitionProgress01;
                lockCtl.RequestSelection(stableId);
                lockCtl.Step(0f);
                Record(Mathf.Abs(lockCtl.AcquisitionProgress01 - progressBefore) < 0.01f,
                       "L-042", "re-requesting the selected track does not reset acquisition progress",
                       report, ref passed, ref failed);

                // Selecting a different track while locked abandons the lock for the stated reason.
                lockCtl.Step(1.0f);
                Record(lockCtl.LockState == MavLockState.Locked,
                       "L-043", "a lock is established before the selection-change case",
                       report, ref passed, ref failed);

                feed.pending.Clear();
                feed.Add(2, new Vector3(0f, 0f, 8000f), MavTrackQuality.Tracked, true);
                feed.Add(3, new Vector3(0f, 0f, 5000f), MavTrackQuality.Tracked, true);
                owner.SweepNowForTesting();
                int thirdId = 0;
                for (int i = 0; i < owner.TrackCount; i++)
                    if (owner.GetTrack(i).trackId != lockCtl.LockedTrackId)
                        thirdId = owner.GetTrack(i).trackId;

                Record(thirdId != 0 && lockCtl.RequestSelection(thirdId),
                       "L-044", "a different track can be requested while locked",
                       report, ref passed, ref failed);
                Record(lockCtl.LockState == MavLockState.Selected
                       && lockCtl.LockedTrackId == 0
                       && lockCtl.LastLossReason == MavLockLossReason.SelectionChanged,
                       "L-044b", "changing selection abandons the lock and records SelectionChanged",
                       report, ref passed, ref failed);

                // An unknown track cannot be selected.
                Record(!lockCtl.RequestSelection(999999),
                       "L-045", "a track the owner does not hold cannot be selected",
                       report, ref passed, ref failed);

                // Requesting zero clears.
                Record(lockCtl.RequestSelection(0) && lockCtl.SelectedTrackId == 0,
                       "L-046", "requesting track zero clears the selection",
                       report, ref passed, ref failed);
            }
            finally { Object.DestroyImmediate(host); }
        }

        // ---- a selection that cannot reach a lock must not starve one that can -------------------
        /// <summary>
        /// The case that found a real defect.
        ///
        /// Automatic selection used to run only while nothing was selected. The legacy scene-sweep feed
        /// is always live and reports nothing better than Coarse, while a lock needs Tracked - so a
        /// legacy track selected at startup latched forever and the controller sat in Selected, never
        /// acquiring, even once a radar track good enough to lock existed. Nothing failed and nothing
        /// was logged; the selection simply could not progress.
        /// </summary>
        private static void ValidateStalledSelection(StringBuilder report, ref int passed, ref int failed)
        {
            GameObject host = new GameObject("MavLockStalledSelectionHost");
            try
            {
                MavTargetTrackOwner owner;
                MavTrackLockController lockCtl;
                ScriptedFeed feed;
                Build(host, out owner, out lockCtl, out feed);
                host.transform.position = Vector3.zero;

                // Only a Coarse track exists - exactly what the legacy feed produces on its own.
                feed.Set(1, new Vector3(0f, 0f, 2000f), MavTrackQuality.Coarse, false);
                owner.SweepNowForTesting();
                lockCtl.UseTestClock(Time.time);
                lockCtl.Step(0f);

                int coarseId = lockCtl.SelectedTrackId;
                Record(coarseId != 0 && lockCtl.LockState == MavLockState.Selected,
                       "L-047", "a track below the lock threshold is selected but cannot acquire",
                       report, ref passed, ref failed);

                lockCtl.Step(5f);
                Record(lockCtl.LockState == MavLockState.Selected && lockCtl.LockedTrackId == 0,
                       "L-047b", "waiting does not turn an unlockable selection into a lock",
                       report, ref passed, ref failed);

                // A radar-grade track appears. The stalled selection must give way to it.
                feed.pending.Clear();
                feed.Add(1, new Vector3(0f, 0f, 2000f), MavTrackQuality.Coarse, false);
                feed.Add(2, new Vector3(0f, 0f, 9000f), MavTrackQuality.Tracked, true);
                owner.SweepNowForTesting();
                lockCtl.Step(0f);

                Record(lockCtl.SelectedTrackId != 0 && lockCtl.SelectedTrackId != coarseId,
                       "L-047c", "a stalled selection gives way to a track that can actually be locked",
                       report, ref passed, ref failed);

                MavTargetTrackData nowSelected;
                bool have = owner.TryGetTrackById(lockCtl.SelectedTrackId, out nowSelected);
                Record(have && nowSelected.quality >= MavTrackQuality.Tracked,
                       "L-047d", "the replacement selection is the lock-capable track",
                       report, ref passed, ref failed);

                lockCtl.Step(1.0f);
                Record(lockCtl.LockState == MavLockState.Locked,
                       "L-047e", "the lock the stalled selection was blocking is now reachable",
                       report, ref passed, ref failed);

                // Narrowness. Reconsideration must never interrupt acquisition.
                GameObject host2 = new GameObject("MavLockNoInterruptHost");
                try
                {
                    MavTargetTrackOwner owner2;
                    MavTrackLockController ctl2;
                    ScriptedFeed feed2;
                    Build(host2, out owner2, out ctl2, out feed2);
                    host2.transform.position = Vector3.zero;

                    feed2.Set(1, new Vector3(0f, 0f, 9000f), MavTrackQuality.Tracked, true);
                    owner2.SweepNowForTesting();
                    ctl2.UseTestClock(Time.time);
                    ctl2.Step(0f);
                    ctl2.Step(0.5f);
                    int acquiringId = ctl2.SelectedTrackId;
                    float progress = ctl2.AcquisitionProgress01;

                    Record(ctl2.LockState == MavLockState.Acquiring && progress > 0f,
                           "L-048", "acquisition is under way before the interruption case",
                           report, ref passed, ref failed);

                    // A nearer, equally good track appears mid-acquisition.
                    feed2.pending.Clear();
                    feed2.Add(1, new Vector3(0f, 0f, 9000f), MavTrackQuality.Tracked, true);
                    feed2.Add(2, new Vector3(0f, 0f, 500f), MavTrackQuality.Tracked, true);
                    owner2.SweepNowForTesting();
                    ctl2.Step(0f);

                    Record(ctl2.SelectedTrackId == acquiringId
                           && ctl2.AcquisitionProgress01 >= progress - 0.001f,
                           "L-048b", "a better track appearing does not interrupt acquisition",
                           report, ref passed, ref failed);

                    ctl2.Step(0.6f);
                    Record(ctl2.LockState == MavLockState.Locked && ctl2.LockedTrackId == acquiringId,
                           "L-048c", "a lock is established on the track acquisition started on",
                           report, ref passed, ref failed);

                    // Nor may it disturb a held lock.
                    feed2.pending.Clear();
                    feed2.Add(1, new Vector3(0f, 0f, 9000f), MavTrackQuality.Tracked, true);
                    feed2.Add(2, new Vector3(0f, 0f, 100f), MavTrackQuality.Tracked, true);
                    owner2.SweepNowForTesting();
                    ctl2.Step(0.1f);
                    Record(ctl2.LockState == MavLockState.Locked && ctl2.LockedTrackId == acquiringId,
                           "L-048d", "a better track appearing does not steal a held lock",
                           report, ref passed, ref failed);
                }
                finally { Object.DestroyImmediate(host2); }

                // A commanded break with automatic selection on: the selection returns, but acquisition
                // starts over. That is deliberately the same shape as the legacy suite, where ClearLock
                // nulls the selection and the next contact sweep re-selects contacts[0] with the STT
                // timer reset - so a commanded break costs a full re-acquisition rather than nothing.
                GameObject host3 = new GameObject("MavLockCommandedBreakHost");
                try
                {
                    MavTargetTrackOwner owner3;
                    MavTrackLockController ctl3;
                    ScriptedFeed feed3;
                    Build(host3, out owner3, out ctl3, out feed3);

                    feed3.Set(1, new Vector3(0f, 0f, 4000f), MavTrackQuality.Tracked, true);
                    owner3.SweepNowForTesting();
                    ctl3.UseTestClock(Time.time);
                    ctl3.Step(0f);
                    ctl3.Step(1.5f);
                    Record(ctl3.LockState == MavLockState.Locked,
                           "L-049", "a lock is held before the commanded break",
                           report, ref passed, ref failed);

                    ctl3.BreakLock(MavLockLossReason.Commanded);
                    Record(ctl3.LockState == MavLockState.Idle
                           && ctl3.LastLossReason == MavLockLossReason.Commanded,
                           "L-049b", "a commanded break ends the lock and records the reason",
                           report, ref passed, ref failed);

                    ctl3.Step(0f);
                    Record(ctl3.SelectedTrackId != 0
                           && ctl3.LockState == MavLockState.Acquiring
                           && ctl3.LockedTrackId == 0,
                           "L-049c", "automatic selection returns after a commanded break, from zero progress",
                           report, ref passed, ref failed);

                    ctl3.autoSelectBestTrack = false;
                    ctl3.BreakLock(MavLockLossReason.Commanded);
                    ctl3.Step(1.0f);
                    Record(ctl3.LockState == MavLockState.Idle && ctl3.SelectedTrackId == 0,
                           "L-049d", "with automatic selection off a commanded break stays broken",
                           report, ref passed, ref failed);
                }
                finally { Object.DestroyImmediate(host3); }
            }
            finally { Object.DestroyImmediate(host); }
        }

        // ---- selection ownership: automatic may yield, commanded may not -------------------------
        /// <summary>
        /// The second defect this phase found, and the one that mattered more.
        ///
        /// <c>ReconsiderStalledSelection</c> claimed it never overrode an explicit request, on the
        /// reasoning that a commanded selection sits in Selected only until its track becomes lockable.
        /// That is false whenever the commanded track simply stays Coarse - and nothing recorded WHICH
        /// kind of selection was held, so a player, AI or FAM command naming a Coarse track was
        /// silently replaceable by the automatic rule.
        ///
        /// Every case here steps the controller AFTER the request. Checking the immediate return of
        /// RequestSelection proves nothing: the override happened on the following step, which is
        /// exactly why reading the state machine did not reveal it.
        /// </summary>
        private static void ValidateSelectionOwnership(StringBuilder report, ref int passed, ref int failed)
        {
            // ---- 1. an AUTOMATIC stalled selection still yields (L-047 behavior, via the origin) ----
            GameObject hostA = new GameObject("MavLockOwnershipAutoHost");
            try
            {
                MavTargetTrackOwner owner;
                MavTrackLockController ctl;
                ScriptedFeed feed;
                Build(hostA, out owner, out ctl, out feed);
                hostA.transform.position = Vector3.zero;

                feed.Set(1, new Vector3(0f, 0f, 2000f), MavTrackQuality.Coarse, false);
                owner.SweepNowForTesting();
                ctl.UseTestClock(Time.time);
                ctl.Step(0f);

                int autoCoarseId = ctl.SelectedTrackId;
                Record(autoCoarseId != 0 && !ctl.SelectionIsExplicit
                       && ctl.LockState == MavLockState.Selected,
                       "L-090", "an automatically picked selection reports itself as not commanded",
                       report, ref passed, ref failed);

                feed.pending.Clear();
                feed.Add(1, new Vector3(0f, 0f, 2000f), MavTrackQuality.Coarse, false);
                feed.Add(2, new Vector3(0f, 0f, 9000f), MavTrackQuality.Tracked, true);
                owner.SweepNowForTesting();
                ctl.Step(0f);

                Record(ctl.SelectedTrackId != 0 && ctl.SelectedTrackId != autoCoarseId
                       && !ctl.SelectionIsExplicit,
                       "L-090b", "an automatic stalled selection still yields to a lock-capable track",
                       report, ref passed, ref failed);
            }
            finally { Object.DestroyImmediate(hostA); }

            // ---- 2..5. a COMMANDED selection is held -----------------------------------------------
            GameObject hostB = new GameObject("MavLockOwnershipExplicitHost");
            try
            {
                MavTargetTrackOwner owner;
                MavTrackLockController ctl;
                ScriptedFeed feed;
                Build(hostB, out owner, out ctl, out feed);
                hostB.transform.position = Vector3.zero;

                // A Coarse track AND a better Tracked track exist. The automatic rule would take the
                // Tracked one, so commanding the Coarse one is the case that used to be overridden.
                feed.pending.Clear();
                feed.Add(1, new Vector3(0f, 0f, 2000f), MavTrackQuality.Coarse, false);
                feed.Add(2, new Vector3(0f, 0f, 9000f), MavTrackQuality.Tracked, true);
                owner.SweepNowForTesting();
                ctl.UseTestClock(Time.time);

                int coarseId = 0, trackedId = 0;
                for (int i = 0; i < owner.TrackCount; i++)
                {
                    MavTargetTrackData t = owner.GetTrack(i);
                    if (t.quality == MavTrackQuality.Coarse) coarseId = t.trackId;
                    else if (t.quality == MavTrackQuality.Tracked) trackedId = t.trackId;
                }

                Record(coarseId != 0 && trackedId != 0 && ctl.RequestSelection(coarseId)
                       && ctl.SelectionIsExplicit,
                       "L-091", "a commanded selection of a Coarse track is accepted and marked commanded",
                       report, ref passed, ref failed);

                // THE BLOCKER. One step later the automatic rule must not have taken it.
                ctl.Step(0f);
                Record(ctl.SelectedTrackId == coarseId
                       && ctl.LockState == MavLockState.Selected
                       && ctl.SelectionIsExplicit,
                       "L-091b", "one step after the command the commanded track is still selected",
                       report, ref passed, ref failed);

                // And it does not drift away over time either.
                ctl.Step(0.5f);
                ctl.Step(0.5f);
                ctl.Step(1.0f);
                Record(ctl.SelectedTrackId == coarseId
                       && ctl.LockState == MavLockState.Selected
                       && ctl.LockedTrackId == 0,
                       "L-091c", "a commanded low-quality selection is held indefinitely, not replaced",
                       report, ref passed, ref failed);

                // 3. When the commanded track's own quality improves, acquisition begins on THAT track.
                feed.pending.Clear();
                feed.Add(1, new Vector3(0f, 0f, 2000f), MavTrackQuality.Tracked, true);
                feed.Add(2, new Vector3(0f, 0f, 9000f), MavTrackQuality.Tracked, true);
                owner.SweepNowForTesting();
                ctl.UseTestClock(Time.time);
                ctl.Step(0f);

                Record(ctl.SelectedTrackId == coarseId && ctl.LockState == MavLockState.Acquiring,
                       "L-092", "acquisition begins on the commanded track once its quality improves",
                       report, ref passed, ref failed);

                ctl.Step(1.0f);
                Record(ctl.LockState == MavLockState.Locked && ctl.LockedTrackId == coarseId,
                       "L-092b", "the lock is established on the commanded track, not the automatic pick",
                       report, ref passed, ref failed);

                // 4. A second command replaces the first.
                Record(ctl.RequestSelection(trackedId)
                       && ctl.SelectedTrackId == trackedId
                       && ctl.SelectionIsExplicit,
                       "L-093", "a second commanded selection replaces the first",
                       report, ref passed, ref failed);
                ctl.Step(0f);
                Record(ctl.SelectedTrackId == trackedId && ctl.SelectionIsExplicit,
                       "L-093b", "the replacement command survives the following step",
                       report, ref passed, ref failed);

                // 5. Breaking releases ownership, so automatic selection may resume.
                //
                // Let the commanded selection finish acquiring first. BreakLock records a loss reason
                // only when a lock was actually held, which is right - there is nothing to lose from
                // Acquiring - so breaking mid-acquisition would leave LastLossReason reading the
                // earlier SelectionChanged. An earlier version of this case asserted Commanded after
                // breaking from Acquiring and was simply wrong about the code.
                ctl.Step(1.0f);
                Record(ctl.LockState == MavLockState.Locked && ctl.LockedTrackId == trackedId,
                       "L-093c", "the replacement command acquires its own lock",
                       report, ref passed, ref failed);

                ctl.BreakLock(MavLockLossReason.Commanded);
                Record(ctl.SelectedTrackId == 0 && !ctl.SelectionIsExplicit
                       && ctl.LastLossReason == MavLockLossReason.Commanded,
                       "L-094", "a commanded break releases selection ownership",
                       report, ref passed, ref failed);

                ctl.Step(0f);
                Record(ctl.SelectedTrackId != 0 && !ctl.SelectionIsExplicit,
                       "L-094b", "automatic selection resumes after a commanded break, as automatic",
                       report, ref passed, ref failed);

                // Requesting zero is also a release, not a silent no-op.
                Record(ctl.RequestSelection(coarseId) && ctl.SelectionIsExplicit
                       && ctl.RequestSelection(0) && !ctl.SelectionIsExplicit,
                       "L-095", "commanding track zero releases ownership too",
                       report, ref passed, ref failed);
            }
            finally { Object.DestroyImmediate(hostB); }

            // ---- a commanded track that disappears cannot still be honoured -------------------------
            GameObject hostC = new GameObject("MavLockOwnershipDropHost");
            try
            {
                MavTargetTrackOwner owner;
                MavTrackLockController ctl;
                ScriptedFeed feed;
                Build(hostC, out owner, out ctl, out feed);

                feed.Set(1, new Vector3(0f, 0f, 2000f), MavTrackQuality.Coarse, false);
                owner.SweepNowForTesting();
                ctl.UseTestClock(Time.time);
                int onlyId = owner.GetTrack(0).trackId;
                ctl.RequestSelection(onlyId);
                ctl.Step(0f);

                feed.pending.Clear();
                owner.ClearTracksForTesting();
                owner.SweepNowForTesting();
                ctl.Step(0f);

                Record(ctl.SelectedTrackId == 0 && !ctl.SelectionIsExplicit
                       && ctl.LockState == MavLockState.Idle,
                       "L-096", "a commanded selection whose track is dropped releases ownership",
                       report, ref passed, ref failed);
            }
            finally { Object.DestroyImmediate(hostC); }

            // ---- 6. automatic selection cannot interrupt a coast -----------------------------------
            // Acquiring and Locked are pinned by L-048b and L-048d. Coasting is the remaining state,
            // and it is the one where the selected track is deliberately NOT being refreshed - so it is
            // the state most likely to look like a stalled selection to a careless rule.
            GameObject hostD = new GameObject("MavLockOwnershipCoastHost");
            try
            {
                MavTargetTrackOwner owner;
                MavTrackLockController ctl;
                ScriptedFeed feed;
                Build(hostD, out owner, out ctl, out feed);
                hostD.transform.position = Vector3.zero;

                feed.Set(1, new Vector3(0f, 0f, 9000f), MavTrackQuality.Tracked, true);
                owner.SweepNowForTesting();
                ctl.UseTestClock(Time.time);
                ctl.Step(0f);
                ctl.Step(1.0f);
                int lockedId = ctl.LockedTrackId;
                Record(ctl.LockState == MavLockState.Locked && lockedId != 0,
                       "L-097", "an automatic lock is held before the coast case",
                       report, ref passed, ref failed);

                // Let the locked track go stale, then offer a nearer, fresher, equally good track.
                ctl.AdvanceTestClock(1.0f);
                ctl.Step(0.1f);
                Record(ctl.LockState == MavLockState.Coasting && ctl.LockedTrackId == lockedId,
                       "L-097b", "the stale locked track coasts rather than dropping",
                       report, ref passed, ref failed);

                feed.pending.Clear();
                feed.Add(2, new Vector3(0f, 0f, 200f), MavTrackQuality.Tracked, true);
                owner.SweepNowForTesting();
                ctl.Step(0.1f);

                Record(ctl.LockState == MavLockState.Coasting && ctl.LockedTrackId == lockedId,
                       "L-097c", "automatic selection cannot interrupt a coast for a better track",
                       report, ref passed, ref failed);
            }
            finally { Object.DestroyImmediate(hostD); }
        }

        // ---- the migration switch ----------------------------------------------------------------
        /// <summary>
        /// Moving authority has to be a decision that can be undone at one field, which is the whole
        /// reason <c>preferAuthoritativeLock</c> exists. These cases pin both positions of it.
        /// </summary>
        private static void ValidateMigrationSwitch(StringBuilder report, ref int passed, ref int failed)
        {
            GameObject host = new GameObject("MavLockSwitchHost");
            try
            {
                MavEngagementView view = host.AddComponent<MavEngagementView>();

                // THE DEFAULT. Legacy authority, because consumers have not migrated yet.
                Record(!view.preferAuthoritativeLock,
                       "L-065", "the migration switch defaults to legacy authority",
                       report, ref passed, ref failed);

                view.PublishDesignation(21, "cas");
                view.PublishSensorLock(22, "stt");
                view.PublishPodLock(23, "pod");
                view.PublishAuthoritativeLock(24, MavLockState.Locked, "authoritative");
                view.RefreshDisagreement();

                Record(view.PrimaryTrackId == 22 && view.PrimarySourceName == "legacy-sensor-stt",
                       "L-065b", "by default the answer is the pre-migration legacy order, even with an authoritative lock held",
                       report, ref passed, ref failed);
                Record(view.authoritativeLockTrackId == 24
                       && view.authoritativeLockState == MavLockState.Locked,
                       "L-065c", "the default withholds the authoritative lock from consumers without erasing it",
                       report, ref passed, ref failed);
                Record(!view.legacyDisagreesWithAuthoritative,
                       "L-065d", "with the migration not in effect there is no disagreement to report",
                       report, ref passed, ref failed);

                // THE OPT-IN. Deliberate, and it is the whole of the change when it happens.
                view.preferAuthoritativeLock = true;
                view.RefreshDisagreement();

                Record(view.PrimaryTrackId == 24 && view.PrimarySourceName == "authoritative-lock",
                       "L-066", "opting in makes the authoritative lock outrank all three legacy authorities",
                       report, ref passed, ref failed);
                Record(view.legacyDisagreesWithAuthoritative,
                       "L-066b", "the migration signal becomes meaningful only once the move is in effect",
                       report, ref passed, ref failed);

                // AND BACK. Reversible at one field, not by reverting code.
                view.preferAuthoritativeLock = false;
                view.RefreshDisagreement();

                Record(view.PrimaryTrackId == 22 && view.PrimarySourceName == "legacy-sensor-stt",
                       "L-066c", "turning the switch back off restores legacy behavior exactly",
                       report, ref passed, ref failed);
                Record(!view.legacyDisagreesWithAuthoritative && view.authoritativeLockTrackId == 24,
                       "L-066d", "the switch is reversible in both directions and loses nothing either way",
                       report, ref passed, ref failed);
            }
            finally { Object.DestroyImmediate(host); }
        }

        // ---- legacy equivalence ------------------------------------------------------------------
        /// <summary>
        /// The step the migration order puts before moving authority: the three legacy authorities'
        /// semantics, expressed in the new vocabulary and checked.
        ///
        /// These cases assert the MAPPING, not the legacy components. The projection is pure, so every
        /// legacy state can be asserted without building a scene or instantiating components that read
        /// input and search the scene in their own Update.
        ///
        /// Where the new authority deliberately diverges, the divergence is asserted too, so it stays a
        /// decision on the record rather than drift nobody noticed.
        /// </summary>
        private static void ValidateLegacyEquivalence(StringBuilder report, ref int passed, ref int failed)
        {
            // MavF22SensorSuite: the only legacy authority with an acquisition phase.
            Record(MavLegacyLockProjection.ProjectSensorSuite(false, 0f, false) == MavLockState.Idle,
                   "L-070", "no sensor selection projects to Idle",
                   report, ref passed, ref failed);
            Record(MavLegacyLockProjection.ProjectSensorSuite(true, 0f, false) == MavLockState.Selected,
                   "L-071", "a sensor selection with no STT progress projects to Selected",
                   report, ref passed, ref failed);
            Record(MavLegacyLockProjection.ProjectSensorSuite(true, 0.6f, false) == MavLockState.Acquiring,
                   "L-072", "partial STT progress projects to Acquiring",
                   report, ref passed, ref failed);
            Record(MavLegacyLockProjection.ProjectSensorSuite(true, 1f, true) == MavLockState.Locked,
                   "L-073", "a completed STT lock projects to Locked",
                   report, ref passed, ref failed);

            bool sensorNeverCoasts = true;
            for (int i = 0; i <= 20; i++)
            {
                float progress = i / 20f;
                if (MavLegacyLockProjection.ProjectSensorSuite(true, progress, progress >= 1f) == MavLockState.Coasting)
                    sensorNeverCoasts = false;
                if (MavLegacyLockProjection.ProjectSensorSuite(false, progress, false) != MavLockState.Idle)
                    sensorNeverCoasts = false;
            }
            Record(sensorNeverCoasts,
                   "L-074", "no STT progress projects to Coasting, because the legacy suite has no coast",
                   report, ref passed, ref failed);

            // MavTargetingPodSystem: instantaneous, and it can lock a bare point.
            Record(MavLegacyLockProjection.ProjectTargetingPod(true, true, true) == MavLockState.Locked,
                   "L-075", "a pod lock onto a target projects to Locked",
                   report, ref passed, ref failed);
            Record(MavLegacyLockProjection.ProjectTargetingPod(true, false, false) == MavLockState.Idle,
                   "L-076", "a pod point lock projects to Idle: a patch of ground is not a track",
                   report, ref passed, ref failed);
            Record(MavLegacyLockProjection.ProjectTargetingPod(true, true, false) == MavLockState.Idle,
                   "L-077", "a pod lock whose target is gone projects to Idle",
                   report, ref passed, ref failed);
            Record(MavLegacyLockProjection.ProjectTargetingPod(false, false, false) == MavLockState.Idle,
                   "L-077b", "an unlocked pod projects to Idle",
                   report, ref passed, ref failed);

            // MavCASTargetingSystem: instantaneous, never expires.
            Record(MavLegacyLockProjection.ProjectCasDesignation(true, true) == MavLockState.Locked,
                   "L-078", "a designation of a live target projects to Locked",
                   report, ref passed, ref failed);
            Record(MavLegacyLockProjection.ProjectCasDesignation(true, false) == MavLockState.Idle,
                   "L-079", "a designation of a dead target projects to Idle",
                   report, ref passed, ref failed);
            Record(MavLegacyLockProjection.ProjectCasDesignation(false, false) == MavLockState.Idle,
                   "L-080", "a bare point designation projects to Idle",
                   report, ref passed, ref failed);

            // The SHAPE of each authority, not just individual cases.
            Record(MavLegacyLockProjection.IsReachableByLegacy(MavLegacyLockAuthorityKind.SensorSuiteStt, MavLockState.Acquiring)
                   && !MavLegacyLockProjection.IsReachableByLegacy(MavLegacyLockAuthorityKind.TargetingPod, MavLockState.Acquiring)
                   && !MavLegacyLockProjection.IsReachableByLegacy(MavLegacyLockAuthorityKind.CasDesignation, MavLockState.Acquiring),
                   "L-081", "only the sensor suite has an acquisition phase to represent",
                   report, ref passed, ref failed);

            Record(!MavLegacyLockProjection.IsReachableByLegacy(MavLegacyLockAuthorityKind.SensorSuiteStt, MavLockState.Coasting)
                   && !MavLegacyLockProjection.IsReachableByLegacy(MavLegacyLockAuthorityKind.TargetingPod, MavLockState.Coasting)
                   && !MavLegacyLockProjection.IsReachableByLegacy(MavLegacyLockAuthorityKind.CasDesignation, MavLockState.Coasting),
                   "L-082", "not one legacy authority holds a lock through a gap, so Coasting is new behavior",
                   report, ref passed, ref failed);

            // Timing equivalence that IS preserved: the new acquisition time is the legacy STT time.
            GameObject host = new GameObject("MavLockDefaultsHost");
            try
            {
                MavTrackLockController defaults = host.AddComponent<MavTrackLockController>();
                Record(Mathf.Abs(defaults.acquisitionSeconds - 1.15f) < 0.0001f,
                       "L-083", "the new acquisition time defaults to the legacy STT lock time",
                       report, ref passed, ref failed);
                Record(defaults.coastSeconds > 0f,
                       "L-085", "the new authority coasts, which no legacy authority did",
                       report, ref passed, ref failed);
                Record(defaults.minimumLockQuality == MavTrackQuality.Tracked,
                       "L-085b", "a lock needs a Tracked-grade observation, which the legacy feed never reports",
                       report, ref passed, ref failed);
            }
            finally { Object.DestroyImmediate(host); }

            // Guard against a silent retune on the legacy side. If someone changes sttLockTime, the
            // equivalence claim above stops being true, and a source check is the only way to notice
            // without instantiating a component that reads input in its own Update.
            string suitePath = Path.Combine(Application.dataPath,
                "MaverickFresh/Scripts/Sensors/MavF22SensorSuite.cs");
            bool suiteReadable = File.Exists(suitePath);
            bool suiteStillDeclares = suiteReadable
                                      && File.ReadAllText(suitePath).Contains("sttLockTime = 1.15f");
            Record(suiteReadable,
                   "L-084", "the legacy sensor suite source can be read for the equivalence check",
                   report, ref passed, ref failed);
            Record(suiteStillDeclares,
                   "L-084b", "the legacy STT lock time is still 1.15s, so the equivalence still holds",
                   report, ref passed, ref failed);
        }

        // ---- Locked as a concrete quality --------------------------------------------------------
        private static void ValidateEffectiveQuality(StringBuilder report, ref int passed, ref int failed)
        {
            GameObject host = new GameObject("MavLockQualityHost");
            try
            {
                MavTargetTrackOwner owner;
                MavTrackLockController lockCtl;
                ScriptedFeed feed;
                Build(host, out owner, out lockCtl, out feed);

                feed.pending.Clear();
                feed.Add(1, new Vector3(0f, 0f, 3000f), MavTrackQuality.Tracked, true);
                feed.Add(2, new Vector3(0f, 0f, 6000f), MavTrackQuality.Tracked, true);
                owner.SweepNowForTesting();
                lockCtl.UseTestClock(Time.time);
                lockCtl.Step(0f);
                lockCtl.Step(1.2f);

                int lockedId = lockCtl.LockedTrackId;
                Record(lockedId != 0, "L-050", "a lock is established for the quality case",
                       report, ref passed, ref failed);

                Record(lockCtl.EffectiveQualityOf(lockedId) == MavTrackQuality.Locked,
                       "L-051", "the locked track reports Locked as its effective quality",
                       report, ref passed, ref failed);

                int otherId = 0;
                for (int i = 0; i < owner.TrackCount; i++)
                    if (owner.GetTrack(i).trackId != lockedId)
                        otherId = owner.GetTrack(i).trackId;
                Record(otherId != 0 && lockCtl.EffectiveQualityOf(otherId) == MavTrackQuality.Tracked,
                       "L-052", "an unlocked track reports exactly what its producer measured",
                       report, ref passed, ref failed);

                // The owner's stored measurement is NOT rewritten: the lock is layered over it.
                MavTargetTrackData stored;
                Record(owner.TryGetTrackById(lockedId, out stored) && stored.quality == MavTrackQuality.Tracked,
                       "L-053", "the owner's stored quality is untouched, so no measurement is rewritten",
                       report, ref passed, ref failed);

                Record(lockCtl.EffectiveQualityOf(0) == MavTrackQuality.None
                       && lockCtl.EffectiveQualityOf(999999) == MavTrackQuality.None,
                       "L-054", "an unknown track has no effective quality",
                       report, ref passed, ref failed);

                // After a commanded break, nothing reports Locked any more.
                lockCtl.BreakLock(MavLockLossReason.Commanded);
                Record(lockCtl.EffectiveQualityOf(lockedId) == MavTrackQuality.Tracked,
                       "L-055", "after the lock ends the track reverts to its measured quality",
                       report, ref passed, ref failed);
            }
            finally { Object.DestroyImmediate(host); }
        }

        // ---- engagement precedence and the legacy migration signal -------------------------------
        private static void ValidateEngagementPrecedence(StringBuilder report, ref int passed, ref int failed)
        {
            GameObject host = new GameObject("MavLockPrecedenceHost");
            try
            {
                MavEngagementView view = host.AddComponent<MavEngagementView>();

                // This section is about the ORDER of precedence once the authoritative lock is allowed
                // to compete, so it opts in explicitly. The DEFAULT is asserted in the migration-switch
                // section instead - keeping the two apart is what stops a precedence case from silently
                // becoming the thing that documents the default.
                view.preferAuthoritativeLock = true;

                view.PublishDesignation(11, "cas");
                view.PublishSensorLock(12, "stt");
                view.PublishPodLock(13, "pod");
                view.PublishAuthoritativeLock(14, MavLockState.Locked, "authoritative");
                view.RefreshDisagreement();

                Record(view.PrimaryTrackId == 14,
                       "L-060", "the authoritative lock outranks all three legacy authorities",
                       report, ref passed, ref failed);
                Record(view.PrimarySourceName == "authoritative-lock",
                       "L-060b", "the view names which authority the primary answer came from",
                       report, ref passed, ref failed);
                Record(view.legacyDisagreesWithAuthoritative,
                       "L-061", "legacy authorities claiming a different track raises the migration signal",
                       report, ref passed, ref failed);

                // Legacy agreeing with the authority lowers the signal - the state in which the legacy
                // path could be retired safely.
                view.PublishDesignation(14, "cas");
                view.PublishSensorLock(14, "stt");
                view.PublishPodLock(0, null);
                view.RefreshDisagreement();
                Record(!view.legacyDisagreesWithAuthoritative,
                       "L-062", "legacy authorities agreeing with the authority clears the migration signal",
                       report, ref passed, ref failed);

                // Without an authoritative lock the legacy order still applies, unchanged.
                view.PublishAuthoritativeLock(0, MavLockState.Idle, null);
                view.PublishDesignation(11, "cas");
                view.PublishSensorLock(12, "stt");
                view.PublishPodLock(13, "pod");
                view.RefreshDisagreement();
                Record(view.PrimaryTrackId == 12 && view.PrimarySourceName == "legacy-sensor-stt",
                       "L-063", "with no authoritative lock the legacy precedence is preserved exactly",
                       report, ref passed, ref failed);

                view.PublishSensorLock(0, null);
                Record(view.PrimaryTrackId == 13 && view.PrimarySourceName == "legacy-pod-lock",
                       "L-063b", "legacy pod lock still outranks designation",
                       report, ref passed, ref failed);

                view.PublishPodLock(0, null);
                Record(view.PrimaryTrackId == 11 && view.PrimarySourceName == "legacy-cas-designation",
                       "L-063c", "legacy designation is still the last resort",
                       report, ref passed, ref failed);

                view.ClearAll();
                Record(view.PrimaryTrackId == 0
                       && view.PrimarySourceName == "none"
                       && !view.legacyDisagreesWithAuthoritative,
                       "L-064", "a cleared view claims nothing from any authority",
                       report, ref passed, ref failed);
            }
            finally { Object.DestroyImmediate(host); }
        }

        private static void Build(GameObject host, out MavTargetTrackOwner owner,
                                 out MavTrackLockController lockCtl, out ScriptedFeed feed)
        {
            owner = host.AddComponent<MavTargetTrackOwner>();
            owner.trackDropSeconds = 600f;   // aging is the controller's business in these cases
            feed = new ScriptedFeed();
            owner.RegisterFeed(feed);

            lockCtl = host.AddComponent<MavTrackLockController>();
            lockCtl.owner = owner;
            lockCtl.acquisitionSeconds = 1.0f;
            lockCtl.staleTrackSeconds = 0.75f;
            lockCtl.coastSeconds = 2.0f;
            lockCtl.minimumLockQuality = MavTrackQuality.Tracked;
            lockCtl.minimumSelectableQuality = MavTrackQuality.Coarse;
            lockCtl.autoSelectBestTrack = true;
        }

        private static void Record(bool condition, string id, string description,
                                   StringBuilder report, ref int passed, ref int failed)
        {
            if (condition)
            {
                passed++;
                report.AppendLine("  PASS  " + id + "  " + description);
            }
            else
            {
                failed++;
                report.AppendLine("  FAIL  " + id + "  " + description);
            }
        }
    }
}
#endif
