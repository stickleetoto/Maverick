#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using MaverickFresh.Combat.Targeting;
using UnityEditor;
using UnityEngine;

namespace MaverickFresh.Combat.EditorTools
{
    /// <summary>
    /// Deterministic validation for Consumer Migration R0: the HUD as a read-only consumer of the one
    /// lock authority.
    ///
    /// The presentation rules are asserted through <see cref="MavLockHudPresentation"/>, which is why that
    /// class exists: the HUD builds its panels inside OnGUI and no test can read a GUI.Box. Every case
    /// drives the real controller with an explicit clock, so the lifecycle being presented is the real
    /// lifecycle rather than a mock of it.
    ///
    /// Two of the requirements are about what the HUD must NOT have gained, and those are checked by
    /// reading the HUD source: no new scene search, no sensor dependency, and no call that could write
    /// selection or lock state. A rule of that shape cannot be proven by exercising behavior - the absence
    /// of a call is not observable at runtime - so it is checked where it is actually decided.
    /// </summary>
    public static class MavLockConsumerMigrationValidation
    {
        /// <summary>
        /// Count of scene-wide searches in the HUD at the Consumer Migration R0 base,
        /// 0154e3ef0db7a547f98bc7de36dd59f33d3a10b6. Pinned so a later change that reaches for the scene
        /// again has to move this number deliberately and say why.
        /// </summary>
        private const int HudSceneSearchesAtBase = 53;

        private const string HudPath = "MaverickFresh/Scripts/MavFreshHud.cs";

        /// <summary>
        /// Calls that would make the HUD an author of engagement state rather than a reader of it.
        /// </summary>
        private static readonly string[] ForbiddenHudCalls =
        {
            "RequestSelection",
            "BreakLock",
            "PublishAuthoritativeLock",
            "PublishDesignation",
            "PublishSensorLock",
            "PublishPodLock",
            "PublishLegacyLockStates",
            "RegisterFeed",
            "UnregisterFeed",
            "CollectObservations",
            "SweepNowForTesting",
            "ClearTracksForTesting",
            "EnableLockAuthority",
            "MavTrackLockController",
            "MavRadarSensor",
            "MavF22SensorSuite",
        };

        [MenuItem("Maverick/Combat/Run Lock Consumer Migration Validation")]
        public static void RunFromMenu()
        {
            int passed, failed;
            string report = RunAll(out passed, out failed);
            Debug.Log(report);
            EditorUtility.DisplayDialog("Lock Consumer Migration Validation",
                (failed == 0 ? "PASS" : "FAIL") + "  passed=" + passed + " failed=" + failed, "OK");
        }

        public static string RunAll(out int passed, out int failed)
        {
            passed = 0;
            failed = 0;
            StringBuilder report = new StringBuilder();
            report.AppendLine("Maverick Lock Consumer Migration Validation R0");
            report.AppendLine("=============================================");

            ValidateLifecyclePresentation(report, ref passed, ref failed);
            ValidateFailClosed(report, ref passed, ref failed);
            ValidateGuardWithoutWithdrawal(report, ref passed, ref failed);
            ValidateLegacyPreserved(report, ref passed, ref failed);
            ValidateDisagreementObservable(report, ref passed, ref failed);
            ValidateHudRemainsReadOnly(report, ref passed, ref failed);

            report.AppendLine("RESULT: " + (failed == 0 ? "PASS" : "FAIL")
                              + " passed=" + passed + " failed=" + failed);
            return report.ToString();
        }

        /// <summary>A feed the test drives, so observation timing is the test's to choose.</summary>
        private sealed class ScriptedFeed : IMavTargetObservationFeed
        {
            public readonly List<MavTrackObservation> pending = new List<MavTrackObservation>();
            public bool IsFeedActive { get { return true; } }
            public MavTrackSource FeedSource { get { return MavTrackSource.Radar; } }

            public int CollectObservations(List<MavTrackObservation> into)
            {
                for (int i = 0; i < pending.Count; i++)
                    into.Add(pending[i]);
                return pending.Count;
            }

            public void Set(int key, Vector3 position, MavTrackQuality quality)
            {
                pending.Clear();
                MavTrackObservation o = new MavTrackObservation();
                o.sourceKey = key;
                o.source = MavTrackSource.Radar;
                o.quality = quality;
                o.position = position;
                o.hasVelocity = true;
                o.velocityMps = new Vector3(0f, 0f, 200f);
                o.displayName = "BANDIT" + key;
                o.team = 2;
                o.isAirTarget = true;
                pending.Add(o);
            }
        }

        private static void Build(GameObject host, out MavTargetTrackOwner owner,
                                 out MavTrackLockController ctl, out MavEngagementView view,
                                 out ScriptedFeed feed)
        {
            owner = host.AddComponent<MavTargetTrackOwner>();
            owner.trackDropSeconds = 600f;
            feed = new ScriptedFeed();
            owner.RegisterFeed(feed);

            view = host.AddComponent<MavEngagementView>();

            ctl = host.AddComponent<MavTrackLockController>();
            ctl.owner = owner;
            ctl.engagementView = view;
            ctl.acquisitionSeconds = 1.0f;
            ctl.staleTrackSeconds = 0.75f;
            ctl.coastSeconds = 2.0f;
            ctl.minimumLockQuality = MavTrackQuality.Tracked;
            ctl.minimumSelectableQuality = MavTrackQuality.Coarse;
            ctl.autoSelectBestTrack = true;
        }

        // ---- the lifecycle, as the HUD presents it -------------------------------------------------
        private static void ValidateLifecyclePresentation(StringBuilder report, ref int passed, ref int failed)
        {
            GameObject host = new GameObject("MavConsumerLifecycleHost");
            try
            {
                MavTargetTrackOwner owner;
                MavTrackLockController ctl;
                MavEngagementView view;
                ScriptedFeed feed;
                Build(host, out owner, out ctl, out view, out feed);
                IMavTrackLockAuthority auth = ctl;

                // 1. Idle: available, committed to nothing.
                ctl.autoSelectBestTrack = false;
                ctl.UseTestClock(Time.time);
                ctl.Step(0f);
                Record(auth.LockState == MavLockState.Idle
                       && MavLockHudPresentation.FormatPlayerLine(auth, view) == MavLockHudPresentation.NoLockText
                       && !MavLockHudPresentation.ShowsLockCommitment(auth),
                       "C-001", "an idle authority shows no authoritative lock",
                       report, ref passed, ref failed);

                // 2. Selected must not read as Locked. A Coarse track can be selected but never locked,
                //    which is the cleanest way to hold the state still at Selected.
                feed.Set(1, new Vector3(0f, 0f, 3000f), MavTrackQuality.Coarse);
                owner.SweepNowForTesting();
                ctl.UseTestClock(Time.time);
                int coarseId = owner.GetTrack(0).trackId;
                ctl.RequestSelection(coarseId);
                ctl.Step(0f);

                string selectedLine = MavLockHudPresentation.FormatPlayerLine(auth, view);
                Record(auth.LockState == MavLockState.Selected,
                       "C-002", "a selected low-quality track stays Selected",
                       report, ref passed, ref failed);
                Record(selectedLine == "LOCK SEL",
                       "C-002b", "Selected is presented as SEL",
                       report, ref passed, ref failed);
                Record(!selectedLine.Contains("LOCKED") && !MavLockHudPresentation.ShowsLockCommitment(auth),
                       "C-002c", "Selected is never presented as a lock",
                       report, ref passed, ref failed);

                // 3. Acquiring: state and progress both represented.
                feed.Set(2, new Vector3(0f, 0f, 4000f), MavTrackQuality.Tracked);
                owner.ClearTracksForTesting();
                owner.SweepNowForTesting();
                ctl.UseTestClock(Time.time);
                int goodId = owner.GetTrack(0).trackId;
                ctl.RequestSelection(goodId);
                ctl.Step(0f);
                ctl.Step(0.5f);

                string acquiringLine = MavLockHudPresentation.FormatPlayerLine(auth, view);
                Record(auth.LockState == MavLockState.Acquiring,
                       "C-003", "a good track begins acquiring",
                       report, ref passed, ref failed);
                Record(acquiringLine == "LOCK ACQ 50%",
                       "C-003b", "Acquiring shows the acquisition percentage, exactly",
                       report, ref passed, ref failed);
                Record(!MavLockHudPresentation.ShowsLockCommitment(auth),
                       "C-003c", "Acquiring is not yet a lock commitment",
                       report, ref passed, ref failed);

                // 4. Locked: track id, name and state.
                ctl.Step(0.6f);
                string lockedLine = MavLockHudPresentation.FormatPlayerLine(auth, view);
                string lockedDebug = MavLockHudPresentation.FormatDebugLine(auth, view);
                Record(auth.LockState == MavLockState.Locked && ctl.LockedTrackId == goodId,
                       "C-004", "acquisition completes into a lock on the selected track",
                       report, ref passed, ref failed);
                Record(lockedLine == "LOCK LOCKED BANDIT2",
                       "C-004b", "Locked shows the locked track name",
                       report, ref passed, ref failed);
                Record(lockedDebug.Contains("AUTH LOCKED")
                       && lockedDebug.Contains("trk " + goodId)
                       && lockedDebug.Contains("BANDIT2"),
                       "C-004c", "the debug line carries state, track id and name",
                       report, ref passed, ref failed);
                Record(MavLockHudPresentation.ShowsLockCommitment(auth),
                       "C-004d", "Locked is a lock commitment",
                       report, ref passed, ref failed);

                // 5. Coasting: commitment kept, but distinguishable from fresh Locked.
                ctl.AdvanceTestClock(1.0f);
                ctl.Step(0.1f);
                string coastLine = MavLockHudPresentation.FormatPlayerLine(auth, view);
                Record(auth.LockState == MavLockState.Coasting && ctl.LockedTrackId == goodId,
                       "C-005", "a stale locked track coasts rather than dropping",
                       report, ref passed, ref failed);
                Record(MavLockHudPresentation.ShowsLockCommitment(auth),
                       "C-005b", "Coasting is still a lock commitment",
                       report, ref passed, ref failed);
                Record(coastLine == "LOCK COAST BANDIT2" && coastLine != lockedLine,
                       "C-005c", "Coasting is presented differently from fresh Locked",
                       report, ref passed, ref failed);
                Record(MavLockHudPresentation.StateLabel(MavLockState.Coasting)
                       != MavLockHudPresentation.StateLabel(MavLockState.Locked),
                       "C-005d", "the two commitment states never share a label",
                       report, ref passed, ref failed);
            }
            finally { Object.DestroyImmediate(host); }
        }

        // ---- fail closed ---------------------------------------------------------------------------
        private static void ValidateFailClosed(StringBuilder report, ref int passed, ref int failed)
        {
            GameObject host = new GameObject("MavConsumerFailClosedHost");
            try
            {
                MavTargetTrackOwner owner;
                MavTrackLockController ctl;
                MavEngagementView view;
                ScriptedFeed feed;
                Build(host, out owner, out ctl, out view, out feed);
                IMavTrackLockAuthority auth = ctl;

                feed.Set(1, new Vector3(0f, 0f, 4000f), MavTrackQuality.Tracked);
                owner.SweepNowForTesting();
                ctl.UseTestClock(Time.time);
                ctl.Step(0f);
                ctl.Step(1.0f);

                Record(MavLockHudPresentation.FormatPlayerLine(auth, view) == "LOCK LOCKED BANDIT1",
                       "C-006", "a lock is presented before the authority is disabled",
                       report, ref passed, ref failed);

                // No step, no callback. The presentation must already be gone.
                ctl.EnableLockAuthority = false;

                Record(MavLockHudPresentation.FormatPlayerLine(auth, view) == string.Empty,
                       "C-006b", "a disabled authority presents nothing at all, immediately",
                       report, ref passed, ref failed);
                Record(!MavLockHudPresentation.ShowsLockCommitment(auth),
                       "C-006c", "a disabled authority is not a lock commitment",
                       report, ref passed, ref failed);
                Record(MavLockHudPresentation.FormatDebugLine(auth, view)
                           .Contains(MavLockHudPresentation.OfflineText),
                       "C-006d", "the debug line says the authority is offline rather than showing a lock",
                       report, ref passed, ref failed);

                // The view may still be holding the last published name. It must not resurface.
                string debug = MavLockHudPresentation.FormatDebugLine(auth, view);
                Record(!debug.Contains("LOCKED") && !debug.Contains("COAST"),
                       "C-006e", "no stale locked presentation survives the authority going away",
                       report, ref passed, ref failed);

                // A null authority is the same answer, not an exception.
                Record(MavLockHudPresentation.FormatPlayerLine(null, view) == string.Empty
                       && !MavLockHudPresentation.ShowsLockCommitment(null)
                       && MavLockHudPresentation.FormatDebugLine(null, view)
                              .Contains(MavLockHudPresentation.OfflineText),
                       "C-006f", "a missing authority fails closed rather than throwing",
                       report, ref passed, ref failed);

                // Re-enabling must not bring the old lock back into the presentation.
                ctl.EnableLockAuthority = true;
                ctl.UseTestClock(Time.time);
                ctl.Step(0f);
                Record(MavLockHudPresentation.FormatPlayerLine(auth, view) != "LOCK LOCKED BANDIT1"
                       && !MavLockHudPresentation.ShowsLockCommitment(auth),
                       "C-007", "re-enabling the authority does not restore the old lock presentation",
                       report, ref passed, ref failed);
            }
            finally { Object.DestroyImmediate(host); }
        }

        /// <summary>
        /// The presentation guard, exercised where it is the ONLY thing standing between a consumer and a
        /// stale lock.
        ///
        /// Turning the enable switch off makes the controller withdraw internally, so the state is already
        /// Idle and those cases pass even if the presentation forgot to check availability at all - proven
        /// by deliberately removing the check and watching them still pass. Losing the track source
        /// performs no withdrawal and raises no callback, so here the guard carries the case alone.
        ///
        /// Its own host, because restoring the wiring afterwards leaves a lock that was never withdrawn -
        /// the documented owner-reassignment residual - and that must not leak into a case about something
        /// else.
        /// </summary>
        private static void ValidateGuardWithoutWithdrawal(StringBuilder report, ref int passed, ref int failed)
        {
            GameObject host = new GameObject("MavConsumerGuardHost");
            try
            {
                MavTargetTrackOwner owner;
                MavTrackLockController ctl;
                MavEngagementView view;
                ScriptedFeed feed;
                Build(host, out owner, out ctl, out view, out feed);
                IMavTrackLockAuthority auth = ctl;

                feed.Set(1, new Vector3(0f, 0f, 4000f), MavTrackQuality.Tracked);
                owner.SweepNowForTesting();
                ctl.UseTestClock(Time.time);
                ctl.Step(0f);
                ctl.Step(1.0f);
                Record(ctl.LockState == MavLockState.Locked
                       && MavLockHudPresentation.ShowsLockCommitment(auth),
                       "C-008", "a lock is held and presented before the wiring is removed",
                       report, ref passed, ref failed);

                // No withdrawal, no callback, no step. Only the guard can catch this.
                ctl.owner = null;

                Record(MavLockHudPresentation.FormatPlayerLine(auth, view) == string.Empty,
                       "C-008b", "losing the track source presents nothing, with no withdrawal to help",
                       report, ref passed, ref failed);
                Record(!MavLockHudPresentation.ShowsLockCommitment(auth),
                       "C-008c", "and it is not treated as a lock commitment either",
                       report, ref passed, ref failed);
                Record(MavLockHudPresentation.FormatDebugLine(auth, view)
                           .Contains(MavLockHudPresentation.OfflineText),
                       "C-008d", "the debug line reports the authority offline",
                       report, ref passed, ref failed);
                Record(!MavLockHudPresentation.AuthoritativeDisagreesWithLegacy(auth, view),
                       "C-008e", "an unavailable authority cannot disagree with anything",
                       report, ref passed, ref failed);
            }
            finally { Object.DestroyImmediate(host); }
        }

        // ---- legacy default and legacy-only information --------------------------------------------
        private static void ValidateLegacyPreserved(StringBuilder report, ref int passed, ref int failed)
        {
            GameObject host = new GameObject("MavConsumerLegacyHost");
            try
            {
                MavEngagementView view = host.AddComponent<MavEngagementView>();

                Record(!view.preferAuthoritativeLock,
                       "C-010", "the migration default is still legacy authority",
                       report, ref passed, ref failed);

                view.PublishDesignation(11, "cas");
                view.PublishSensorLock(12, "stt");
                view.PublishPodLock(13, "pod");
                view.PublishAuthoritativeLock(14, MavLockState.Locked, "authoritative");
                view.RefreshDisagreement();

                Record(view.PrimaryTrackId == 12,
                       "C-010b", "migrating a consumer did not move global authority",
                       report, ref passed, ref failed);

                // Legacy-only lifecycle states stay visible in the debug presentation. A pod locked onto
                // a bare ground point projects to Idle, and that is information the authoritative lock
                // cannot carry at all.
                view.PublishLegacyLockStates(MavLockState.Locked, MavLockState.Acquiring, MavLockState.Idle);
                string debug = MavLockHudPresentation.FormatDebugLine(null, view);

                Record(debug.Contains("LEG des LOCKED"),
                       "C-011", "a legacy CAS designation stays visible",
                       report, ref passed, ref failed);
                Record(debug.Contains("stt ACQ"),
                       "C-011b", "a legacy STT part-way through acquisition stays visible",
                       report, ref passed, ref failed);
                Record(debug.Contains("pod IDLE"),
                       "C-011c", "a pod point lock still reads Idle, and is still shown",
                       report, ref passed, ref failed);
                Record(view.legacyDesignationLockState == MavLockState.Locked
                       && view.legacySensorLockState == MavLockState.Acquiring,
                       "C-011d", "the presentation read the legacy states without altering them",
                       report, ref passed, ref failed);
            }
            finally { Object.DestroyImmediate(host); }
        }

        // ---- disagreement stays observable while the default is legacy ------------------------------
        private static void ValidateDisagreementObservable(StringBuilder report, ref int passed, ref int failed)
        {
            GameObject host = new GameObject("MavConsumerDisagreeHost");
            try
            {
                MavTargetTrackOwner owner;
                MavTrackLockController ctl;
                MavEngagementView view;
                ScriptedFeed feed;
                Build(host, out owner, out ctl, out view, out feed);
                IMavTrackLockAuthority auth = ctl;

                feed.Set(1, new Vector3(0f, 0f, 4000f), MavTrackQuality.Tracked);
                owner.SweepNowForTesting();
                ctl.UseTestClock(Time.time);
                ctl.Step(0f);
                ctl.Step(1.0f);
                int lockedId = ctl.LockedTrackId;

                // A legacy authority claiming a different track.
                view.PublishSensorLock(lockedId + 500, "other");
                view.RefreshDisagreement();

                Record(MavLockHudPresentation.AuthoritativeDisagreesWithLegacy(auth, view),
                       "C-012", "the consumer can see authoritative and legacy disagree",
                       report, ref passed, ref failed);
                Record(MavLockHudPresentation.FormatDebugLine(auth, view).Contains("DISAGREE"),
                       "C-012b", "disagreement is surfaced in the developer presentation",
                       report, ref passed, ref failed);
                Record(!view.legacyDisagreesWithAuthoritative,
                       "C-012c", "and it is visible even though the view's own flag stays off with the legacy default",
                       report, ref passed, ref failed);

                // Agreement clears it.
                view.PublishSensorLock(lockedId, "same");
                Record(!MavLockHudPresentation.AuthoritativeDisagreesWithLegacy(auth, view)
                       && !MavLockHudPresentation.FormatDebugLine(auth, view).Contains("DISAGREE"),
                       "C-013", "agreement clears the disagreement signal",
                       report, ref passed, ref failed);

                // A legacy claim the owner never saw is id 0, which is not a disagreement.
                view.PublishSensorLock(0, null);
                view.PublishPodLock(0, null);
                view.PublishDesignation(0, null);
                Record(!MavLockHudPresentation.AuthoritativeDisagreesWithLegacy(auth, view),
                       "C-013b", "a legacy authority claiming nothing is not a disagreement",
                       report, ref passed, ref failed);
            }
            finally { Object.DestroyImmediate(host); }
        }

        // ---- what the HUD must not have gained ------------------------------------------------------
        private static void ValidateHudRemainsReadOnly(StringBuilder report, ref int passed, ref int failed)
        {
            string path = Path.Combine(Application.dataPath, HudPath);
            bool readable = File.Exists(path);
            Record(readable,
                   "C-020", "the HUD source can be read for the read-only check",
                   report, ref passed, ref failed);
            if (!readable)
                return;

            string source = File.ReadAllText(path);

            // Scene searches: pinned, not merely "not many".
            int searches = CountOccurrences(source, "FindObjectOfType");
            Record(searches == HudSceneSearchesAtBase,
                   "C-021", "the HUD gained no new scene search (" + searches + " vs "
                            + HudSceneSearchesAtBase + " at base)",
                   report, ref passed, ref failed);

            for (int i = 0; i < ForbiddenHudCalls.Length; i++)
            {
                string token = ForbiddenHudCalls[i];
                Record(!source.Contains(token),
                       "C-022." + token, "the HUD does not reference " + token,
                       report, ref passed, ref failed);
            }

            // It must read the authority through the interface, and hold no target GameObject.
            Record(source.Contains("IMavTrackLockAuthority"),
                   "C-023", "the HUD reads the lock authority through its interface",
                   report, ref passed, ref failed);
            Record(source.Contains("MavLockHudPresentation"),
                   "C-023b", "presentation goes through the pure formatter",
                   report, ref passed, ref failed);

            // The formatter itself must hold no state, so nothing can cache a lock between frames.
            string presenterPath = Path.Combine(Application.dataPath,
                "MaverickFresh/Scripts/HUD/MavLockHudPresentation.cs");
            bool presenterReadable = File.Exists(presenterPath);
            Record(presenterReadable,
                   "C-024", "the presentation source can be read",
                   report, ref passed, ref failed);
            if (presenterReadable)
            {
                string presenter = File.ReadAllText(presenterPath);
                Record(presenter.Contains("public static class"),
                       "C-024b", "the presentation is static, so it cannot cache a lock",
                       report, ref passed, ref failed);
                Record(CountOccurrences(presenter, "FindObjectOfType") == 0
                       && !presenter.Contains("MavRadarSensor")
                       && !presenter.Contains("MavF22SensorSuite"),
                       "C-024c", "the presentation searches no scene and knows no sensor",
                       report, ref passed, ref failed);
            }
        }

        private static int CountOccurrences(string text, string token)
        {
            int n = 0;
            int at = text.IndexOf(token);
            while (at >= 0)
            {
                n++;
                at = text.IndexOf(token, at + token.Length);
            }
            return n;
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
