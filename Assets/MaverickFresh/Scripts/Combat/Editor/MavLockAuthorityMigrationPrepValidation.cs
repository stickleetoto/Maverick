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
    /// Deterministic validation for Lock Authority Migration Prep R0: the evidence that has to exist
    /// BEFORE authority moves, not after.
    ///
    /// The phase moves nothing. <c>preferAuthoritativeLock</c> stays false and the legacy owners keep
    /// running; what changes is that the one signal used to justify the eventual move -
    /// <see cref="MavEngagementView.legacyDisagreesWithAuthoritative"/> - now actually reports during the
    /// window in which the move is being considered.
    ///
    /// WHAT WAS WRONG. The field was computed only while <c>preferAuthoritativeLock</c> was true. So the
    /// question "do the legacy owners agree with the new authority?" could only be answered after the
    /// switch had already been thrown - the signal could confirm a decision, never inform one. Worse, it
    /// made the retirement criterion vacuous: a flag that is false because nothing computes it looks
    /// exactly like a flag that is false because everything agrees.
    ///
    /// THE DISTINCTION THESE CASES PIN. Precedence and disagreement are different questions.
    /// <see cref="MavEngagementView.PrimaryTrackId"/> answers WHICH claim consumers get, and the switch
    /// still decides that. The disagreement flag answers WHETHER the two available claims differ, and
    /// nothing about precedence should be able to hide it. Both halves are asserted, in both switch
    /// positions, because a test that only checked the corrected half would pass if the gate were moved
    /// from one field to the other.
    ///
    /// Every behavioral case here drives the real <see cref="MavTrackLockController"/> with an explicit
    /// clock where an authoritative lock is needed, so what the view publishes is a real committed lock
    /// rather than a hand-written id. The hand-written cases are kept separate and are about the mapping
    /// only.
    /// </summary>
    public static class MavLockAuthorityMigrationPrepValidation
    {
        private const string ViewPath = "MaverickFresh/Scripts/Combat/Targeting/MavEngagementView.cs";

        [MenuItem("Maverick/Combat/Run Lock Authority Migration Prep Validation")]
        public static void RunFromMenu()
        {
            int passed, failed;
            string report = RunAll(out passed, out failed);
            Debug.Log(report);
            EditorUtility.DisplayDialog("Lock Authority Migration Prep Validation",
                (failed == 0 ? "PASS" : "FAIL") + "  passed=" + passed + " failed=" + failed, "OK");
        }

        public static string RunAll(out int passed, out int failed)
        {
            passed = 0;
            failed = 0;
            StringBuilder report = new StringBuilder();
            report.AppendLine("Maverick Lock Authority Migration Prep Validation R0");
            report.AppendLine("===================================================");

            ValidateDisagreementVisibleWithLegacyDefault(report, ref passed, ref failed);
            ValidateToggleSeparatesPrecedenceFromDisagreement(report, ref passed, ref failed);
            ValidateSignalIsDiagnosticOnly(report, ref passed, ref failed);
            ValidateAgainstTheRealAuthority(report, ref passed, ref failed);
            ValidateGateCannotReturn(report, ref passed, ref failed);

            report.AppendLine("RESULT: " + (failed == 0 ? "PASS" : "FAIL")
                              + " passed=" + passed + " failed=" + failed);
            return report.ToString();
        }

        // ---- the signal, while the default is still legacy ------------------------------------------
        /// <summary>
        /// The corrected semantics, stated as a table: an authoritative committed track versus any
        /// non-zero legacy claim, with the switch OFF throughout - which is the only position this phase
        /// ships.
        ///
        /// These cases set the published ids directly. That is deliberate: they are about the MAPPING
        /// from published claims to the signal, and every combination including ones no real controller
        /// would produce this frame has to be covered. The behavioral half, where a real controller
        /// produces the authoritative claim, is asserted separately below.
        /// </summary>
        private static void ValidateDisagreementVisibleWithLegacyDefault(
            StringBuilder report, ref int passed, ref int failed)
        {
            GameObject host = new GameObject("MavPrepMappingHost");
            try
            {
                MavEngagementView view = host.AddComponent<MavEngagementView>();

                Record(!view.preferAuthoritativeLock,
                       "M-001", "the migration default is still legacy authority",
                       report, ref passed, ref failed);

                // authoritative 10, legacy sensor 11: a conflict, and the switch is off.
                view.PublishAuthoritativeLock(10, MavLockState.Locked, "auth");
                view.PublishSensorLock(11, "stt");
                view.RefreshDisagreement();

                Record(view.legacyDisagreesWithAuthoritative,
                       "M-002", "a legacy claim on another track is reported while the default is legacy",
                       report, ref passed, ref failed);
                Record(view.PrimaryTrackId == 11 && view.PrimarySourceName == "legacy-sensor-stt",
                       "M-003", "and reporting it does not move authority: the legacy claim still wins",
                       report, ref passed, ref failed);

                // authoritative 10, legacy sensor 10: agreement.
                view.PublishSensorLock(10, "stt");
                view.RefreshDisagreement();
                Record(!view.legacyDisagreesWithAuthoritative,
                       "M-004", "agreement on the same track clears the signal",
                       report, ref passed, ref failed);

                // authoritative 10, nothing legacy: absence, not conflict.
                view.PublishSensorLock(0, null);
                view.PublishPodLock(0, null);
                view.PublishDesignation(0, null);
                view.RefreshDisagreement();
                Record(!view.legacyDisagreesWithAuthoritative,
                       "M-005", "legacy authorities claiming nothing is not a disagreement",
                       report, ref passed, ref failed);

                // No authoritative committed track: nothing to disagree WITH.
                view.PublishAuthoritativeLock(0, MavLockState.Idle, null);
                view.PublishSensorLock(11, "stt");
                view.PublishPodLock(12, "pod");
                view.PublishDesignation(13, "cas");
                view.RefreshDisagreement();
                Record(!view.legacyDisagreesWithAuthoritative,
                       "M-006", "with no authoritative committed track there is no disagreement",
                       report, ref passed, ref failed);
                Record(view.authoritiesDisagree && view.claimingAuthorityCount == 3,
                       "M-006b", "though the legacy authorities' own disagreement is still reported",
                       report, ref passed, ref failed);

                // Each legacy owner individually, so one of the three cannot be silently skipped.
                view.PublishAuthoritativeLock(10, MavLockState.Locked, "auth");
                view.PublishSensorLock(0, null);
                view.PublishPodLock(0, null);
                view.PublishDesignation(11, "cas");
                view.RefreshDisagreement();
                Record(view.legacyDisagreesWithAuthoritative,
                       "M-007", "a CAS designation on another track counts",
                       report, ref passed, ref failed);

                view.PublishDesignation(0, null);
                view.PublishPodLock(11, "pod");
                view.RefreshDisagreement();
                Record(view.legacyDisagreesWithAuthoritative,
                       "M-007b", "a pod lock on another track counts",
                       report, ref passed, ref failed);

                view.PublishPodLock(0, null);
                view.PublishSensorLock(11, "stt");
                view.RefreshDisagreement();
                Record(view.legacyDisagreesWithAuthoritative,
                       "M-007c", "a sensor STT lock on another track counts",
                       report, ref passed, ref failed);

                // All three agreeing is the state in which the legacy path could be retired.
                view.PublishSensorLock(10, "stt");
                view.PublishPodLock(10, "pod");
                view.PublishDesignation(10, "cas");
                view.RefreshDisagreement();
                Record(!view.legacyDisagreesWithAuthoritative,
                       "M-008", "all three legacy owners agreeing clears the signal",
                       report, ref passed, ref failed);

                // One dissenter among three is still a dissenter.
                view.PublishPodLock(99, "pod");
                view.RefreshDisagreement();
                Record(view.legacyDisagreesWithAuthoritative,
                       "M-008b", "one legacy owner out of three disagreeing is enough",
                       report, ref passed, ref failed);

                view.ClearAll();
                Record(!view.legacyDisagreesWithAuthoritative,
                       "M-009", "a cleared view reports no disagreement",
                       report, ref passed, ref failed);
            }
            finally { Object.DestroyImmediate(host); }
        }

        // ---- precedence versus disagreement --------------------------------------------------------
        /// <summary>
        /// The two questions, moved apart and checked apart.
        ///
        /// The switch must change WHICH claim wins and must NOT change whether the claims differ. Both
        /// directions are asserted in the same scenario, because the failure being guarded against is
        /// precisely the two being conflated again.
        /// </summary>
        private static void ValidateToggleSeparatesPrecedenceFromDisagreement(
            StringBuilder report, ref int passed, ref int failed)
        {
            GameObject host = new GameObject("MavPrepToggleHost");
            try
            {
                MavEngagementView view = host.AddComponent<MavEngagementView>();

                view.PublishAuthoritativeLock(24, MavLockState.Locked, "auth");
                view.PublishSensorLock(22, "stt");
                view.PublishPodLock(23, "pod");
                view.PublishDesignation(21, "cas");

                view.preferAuthoritativeLock = false;
                view.RefreshDisagreement();
                bool disagreeOff = view.legacyDisagreesWithAuthoritative;
                int primaryOff = view.PrimaryTrackId;
                string sourceOff = view.PrimarySourceName;

                view.preferAuthoritativeLock = true;
                view.RefreshDisagreement();
                bool disagreeOn = view.legacyDisagreesWithAuthoritative;
                int primaryOn = view.PrimaryTrackId;
                string sourceOn = view.PrimarySourceName;

                Record(disagreeOff && disagreeOn,
                       "M-010", "the disagreement is reported in both switch positions",
                       report, ref passed, ref failed);
                Record(disagreeOff == disagreeOn,
                       "M-010b", "toggling the switch does not change the disagreement truth",
                       report, ref passed, ref failed);
                Record(primaryOff == 22 && sourceOff == "legacy-sensor-stt",
                       "M-011", "with the switch off the legacy order still answers",
                       report, ref passed, ref failed);
                Record(primaryOn == 24 && sourceOn == "authoritative-lock",
                       "M-011b", "with the switch on the authoritative lock answers",
                       report, ref passed, ref failed);
                Record(primaryOff != primaryOn,
                       "M-011c", "so the switch does change precedence - the concepts are separate, not merged",
                       report, ref passed, ref failed);

                // The agreeing case must also be switch-independent, so the previous cases cannot pass
                // by the signal simply being stuck on.
                view.PublishSensorLock(24, "stt");
                view.PublishPodLock(0, null);
                view.PublishDesignation(0, null);

                view.preferAuthoritativeLock = false;
                view.RefreshDisagreement();
                bool agreeOff = view.legacyDisagreesWithAuthoritative;
                view.preferAuthoritativeLock = true;
                view.RefreshDisagreement();
                bool agreeOn = view.legacyDisagreesWithAuthoritative;

                Record(!agreeOff && !agreeOn,
                       "M-012", "agreement reads as agreement in both switch positions",
                       report, ref passed, ref failed);

                // And the no-authoritative-lock case.
                view.PublishAuthoritativeLock(0, MavLockState.Idle, null);
                view.PublishSensorLock(22, "stt");

                view.preferAuthoritativeLock = false;
                view.RefreshDisagreement();
                bool noneOff = view.legacyDisagreesWithAuthoritative;
                int noLockPrimaryOff = view.PrimaryTrackId;
                view.preferAuthoritativeLock = true;
                view.RefreshDisagreement();
                bool noneOn = view.legacyDisagreesWithAuthoritative;
                int noLockPrimaryOn = view.PrimaryTrackId;

                Record(!noneOff && !noneOn,
                       "M-013", "no authoritative committed track reads the same in both positions",
                       report, ref passed, ref failed);
                Record(noLockPrimaryOff == 22 && noLockPrimaryOn == 22,
                       "M-013b", "and with nothing authoritative to prefer, precedence is legacy either way",
                       report, ref passed, ref failed);

                // Reversible, and the field ships false.
                view.preferAuthoritativeLock = false;
                Record(!view.preferAuthoritativeLock,
                       "M-014", "the switch returns to legacy, which is the shipped position",
                       report, ref passed, ref failed);
            }
            finally { Object.DestroyImmediate(host); }
        }

        // ---- the signal decides nothing ------------------------------------------------------------
        /// <summary>
        /// A diagnostic that changes the engagement is not a diagnostic. These cases snapshot everything
        /// the view and the authority hold, exercise the signal hard, and require the snapshot back.
        /// </summary>
        private static void ValidateSignalIsDiagnosticOnly(StringBuilder report, ref int passed, ref int failed)
        {
            GameObject host = new GameObject("MavPrepDiagnosticHost");
            try
            {
                MavTargetTrackOwner owner;
                MavTrackLockController ctl;
                MavEngagementView view;
                ScriptedFeed feed;
                Build(host, out owner, out ctl, out view, out feed);

                feed.Set(1, new Vector3(0f, 0f, 4000f), MavTrackQuality.Tracked);
                owner.SweepNowForTesting();
                ctl.UseTestClock(Time.time);
                ctl.Step(0f);
                ctl.Step(1.0f);

                int lockedBefore = ctl.LockedTrackId;
                MavLockState stateBefore = ctl.LockState;
                int selectedBefore = ctl.SelectedTrackId;
                float holdBefore = ctl.TimeInLockSeconds;
                int trackCountBefore = owner.TrackCount;

                view.PublishSensorLock(lockedBefore + 500, "other");
                int primaryBefore = view.PrimaryTrackId;
                int authIdBefore = view.authoritativeLockTrackId;
                MavLockState authStateBefore = view.authoritativeLockState;
                int sensorBefore = view.sensorLockTrackId;
                int podBefore = view.podLockTrackId;
                int desBefore = view.designatedTrackId;

                // Exercise it: recompute repeatedly and read it through the consumer as well.
                view.RefreshDisagreement();
                bool first = view.legacyDisagreesWithAuthoritative;
                view.RefreshDisagreement();
                view.RefreshDisagreement();
                bool third = view.legacyDisagreesWithAuthoritative;
                MavLockHudPresentation.FormatDebugLine(ctl, view);
                MavLockHudPresentation.AuthoritativeDisagreesWithLegacy(ctl, view);

                // Two claims, kept apart: that recomputing is stable, and that the scenario being
                // recomputed is a real disagreement - otherwise stability would be satisfied by a
                // signal that is simply always false.
                Record(first == third,
                       "M-020", "recomputing the signal is idempotent",
                       report, ref passed, ref failed);
                Record(first,
                       "M-020b", "and the case it was stable across is a genuine disagreement",
                       report, ref passed, ref failed);
                Record(view.sensorLockTrackId == sensorBefore
                       && view.podLockTrackId == podBefore
                       && view.designatedTrackId == desBefore,
                       "M-021", "computing it does not alter any legacy claim",
                       report, ref passed, ref failed);
                Record(view.authoritativeLockTrackId == authIdBefore
                       && view.authoritativeLockState == authStateBefore,
                       "M-021b", "and does not alter the authoritative claim",
                       report, ref passed, ref failed);
                Record(view.PrimaryTrackId == primaryBefore,
                       "M-022", "and does not change which authority answers",
                       report, ref passed, ref failed);
                Record(ctl.LockedTrackId == lockedBefore
                       && ctl.LockState == stateBefore
                       && ctl.SelectedTrackId == selectedBefore,
                       "M-023", "the authority's lock and selection are untouched by the diagnostic",
                       report, ref passed, ref failed);
                Record(Mathf.Approximately(ctl.TimeInLockSeconds, holdBefore),
                       "M-023b", "including how long the lock has been held",
                       report, ref passed, ref failed);
                Record(owner.TrackCount == trackCountBefore,
                       "M-024", "and no track was created or dropped to report a disagreement",
                       report, ref passed, ref failed);
            }
            finally { Object.DestroyImmediate(host); }
        }

        // ---- against a real committed lock ---------------------------------------------------------
        /// <summary>
        /// The same semantics, with the authoritative claim produced by the real controller rather than
        /// written into the view. This is what makes the signal migration evidence instead of a unit
        /// test of an if-statement: the id it compares against is whatever the lock lifecycle actually
        /// published this step.
        ///
        /// It also pins the existing consumer. The HUD derives disagreement itself, and its answer must
        /// now match the view's - the workaround and the corrected field cannot be allowed to drift into
        /// two different definitions of the same word.
        /// </summary>
        private static void ValidateAgainstTheRealAuthority(StringBuilder report, ref int passed, ref int failed)
        {
            GameObject host = new GameObject("MavPrepAuthorityHost");
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
                Record(lockedId != 0 && view.authoritativeLockTrackId == lockedId,
                       "M-030", "the real authority holds a lock and the view carries its track",
                       report, ref passed, ref failed);
                Record(!view.preferAuthoritativeLock,
                       "M-030b", "with the legacy default still in force",
                       report, ref passed, ref failed);

                // A legacy owner pointing somewhere else.
                view.PublishSensorLock(lockedId + 500, "other");
                view.RefreshDisagreement();

                Record(view.legacyDisagreesWithAuthoritative,
                       "M-031", "a real committed lock disagreeing with legacy is reported pre-switch",
                       report, ref passed, ref failed);
                Record(view.legacyDisagreesWithAuthoritative
                       == MavLockHudPresentation.AuthoritativeDisagreesWithLegacy(auth, view),
                       "M-032", "and the consumer's own derivation agrees with the view",
                       report, ref passed, ref failed);
                Record(view.PrimaryTrackId == lockedId + 500,
                       "M-032b", "the legacy claim still wins, so nothing about authority moved",
                       report, ref passed, ref failed);

                // Agreement.
                view.PublishSensorLock(lockedId, "same");
                view.RefreshDisagreement();
                Record(!view.legacyDisagreesWithAuthoritative
                       && !MavLockHudPresentation.AuthoritativeDisagreesWithLegacy(auth, view),
                       "M-033", "agreement clears it on both paths",
                       report, ref passed, ref failed);

                // No legacy claim at all.
                view.PublishSensorLock(0, null);
                view.PublishPodLock(0, null);
                view.PublishDesignation(0, null);
                view.RefreshDisagreement();
                Record(!view.legacyDisagreesWithAuthoritative
                       && !MavLockHudPresentation.AuthoritativeDisagreesWithLegacy(auth, view),
                       "M-034", "and no legacy claim is not a disagreement on either path",
                       report, ref passed, ref failed);

                // The HUD's presentation must not start depending on precedence. It reads the authority.
                view.PublishSensorLock(lockedId + 500, "other");
                view.RefreshDisagreement();
                string playerOff = MavLockHudPresentation.FormatPlayerLine(auth, view);
                string debugOff = MavLockHudPresentation.FormatDebugLine(auth, view);

                view.preferAuthoritativeLock = true;
                view.RefreshDisagreement();
                string playerOn = MavLockHudPresentation.FormatPlayerLine(auth, view);
                string debugOn = MavLockHudPresentation.FormatDebugLine(auth, view);
                view.preferAuthoritativeLock = false;
                view.RefreshDisagreement();

                Record(playerOff == playerOn,
                       "M-035", "the HUD player line is unchanged by the migration switch",
                       report, ref passed, ref failed);
                Record(debugOff == debugOn,
                       "M-035b", "and so is the developer line",
                       report, ref passed, ref failed);
                Record(playerOff.StartsWith("LOCK LOCKED"),
                       "M-035c", "which is the committed-lock line the consumer already shipped",
                       report, ref passed, ref failed);
                Record(debugOff.Contains("DISAGREE"),
                       "M-035d", "and the disagreement is still surfaced to the developer panel",
                       report, ref passed, ref failed);

                // An unavailable authority has no committed track, so there is nothing to disagree with,
                // whatever legacy still claims. This is the fail-closed rule reaching the signal.
                ctl.EnableLockAuthority = false;
                view.RefreshDisagreement();

                Record(view.authoritativeLockTrackId == 0,
                       "M-036", "disabling the authority withdraws its published claim",
                       report, ref passed, ref failed);
                Record(!view.legacyDisagreesWithAuthoritative,
                       "M-036b", "an unavailable authority cannot disagree with anything",
                       report, ref passed, ref failed);
                Record(!MavLockHudPresentation.AuthoritativeDisagreesWithLegacy(auth, view),
                       "M-036c", "and the consumer reaches the same answer",
                       report, ref passed, ref failed);
                Record(view.PrimaryTrackId == lockedId + 500
                       && view.PrimarySourceName == "legacy-sensor-stt",
                       "M-036d", "while the legacy answer carries on exactly as before",
                       report, ref passed, ref failed);
            }
            finally { Object.DestroyImmediate(host); }
        }

        // ---- the gate must not come back -----------------------------------------------------------
        /// <summary>
        /// A source-level tripwire, because the defect was one token and re-introducing it would look
        /// like a tidy-up. The behavioral cases above would catch it - but only while someone keeps
        /// asserting both switch positions, and this states the rule where it is actually decided.
        ///
        /// Comment lines are stripped before the check: the code deliberately DISCUSSES the switch in the
        /// comment above the computation, and a naive substring search would forbid explaining itself.
        /// </summary>
        private static void ValidateGateCannotReturn(StringBuilder report, ref int passed, ref int failed)
        {
            string path = Path.Combine(Application.dataPath, ViewPath);
            bool readable = File.Exists(path);
            Record(readable,
                   "M-040", "the engagement view source can be read for the structural check",
                   report, ref passed, ref failed);
            if (!readable)
                return;

            string source = File.ReadAllText(path);
            string refreshBody = StripComments(Between(source, "public void RefreshDisagreement()", "public void ClearAll()"));
            string primaryBody = StripComments(Between(source, "public int PrimaryTrackId", "public string PrimarySourceName"));

            Record(refreshBody.Length > 0 && primaryBody.Length > 0,
                   "M-040b", "both the disagreement computation and the precedence rule were located",
                   report, ref passed, ref failed);
            Record(!refreshBody.Contains("preferAuthoritativeLock"),
                   "M-041", "the disagreement computation does not consult the migration switch",
                   report, ref passed, ref failed);
            Record(refreshBody.Contains("authoritativeLockTrackId != 0"),
                   "M-041b", "it gates on a committed authoritative track instead",
                   report, ref passed, ref failed);
            Record(primaryBody.Contains("preferAuthoritativeLock"),
                   "M-042", "while precedence DOES still consult the switch, so the two stay separate",
                   report, ref passed, ref failed);
            Record(source.Contains("public bool preferAuthoritativeLock = false;"),
                   "M-043", "and the switch is still declared legacy-by-default in source",
                   report, ref passed, ref failed);
        }

        /// <summary>The text between two markers, or empty if either is missing.</summary>
        private static string Between(string source, string startMarker, string endMarker)
        {
            int start = source.IndexOf(startMarker);
            if (start < 0)
                return string.Empty;
            int end = source.IndexOf(endMarker, start);
            if (end < 0)
                return string.Empty;
            return source.Substring(start, end - start);
        }

        /// <summary>Drops whole-line comments, so a rule about code is not defeated by prose about code.</summary>
        private static string StripComments(string source)
        {
            StringBuilder kept = new StringBuilder();
            string[] lines = source.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].TrimStart();
                if (trimmed.StartsWith("//"))
                    continue;
                kept.AppendLine(lines[i]);
            }
            return kept.ToString();
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
