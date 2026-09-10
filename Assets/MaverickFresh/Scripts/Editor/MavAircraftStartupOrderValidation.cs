#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MaverickFresh.EditorTools
{
    /// <summary>
    /// Regression validation for AIRCRAFT IDENTITY OWNERSHIP DURING STARTUP.
    ///
    /// The bug this reproduces, from the Unity log that found it:
    ///
    ///     MavFreshBootstrap.Awake
    ///       -> SetupFreshMouseFlight
    ///         -> MavWTFeelPolishController.ApplyPreset
    ///           -> TryApplyAircraftAwarePreset
    ///             -> MavAircraftProfileApplier.ApplyProfile
    ///     "MavAircraftProfileApplier applied F-22A RAPTOR to Mav_Player"
    ///
    /// with F16C selected. The catalog was answering correctly by then - the earlier identity work
    /// had removed every silent F-22A substitution from it. The remaining fault was OWNERSHIP: a
    /// tuning controller was deciding aircraft identity, and it derived that identity from
    /// `applier.aircraft`, a SERIALIZED inspector field whose default is F22A. During bootstrap no
    /// selection had been applied yet, so the read returned F22A and the whole F-22 profile went onto
    /// the aircraft.
    ///
    /// Why the previous suite passed while this was broken: every check there applied an aircraft
    /// first and then asserted the result. None of them ran the startup ORDER, where the defining
    /// property is that nothing has been applied yet. A component that reads a default is invisible
    /// to a test that never lets it see one.
    ///
    /// So these checks assert on the sequence rather than the outcome, and they watch Unity's log,
    /// because "F-22 was applied at some point during startup" is a statement about what happened on
    /// the way - not about where things ended up.
    /// </summary>
    public static class MavAircraftStartupOrderValidation
    {
        [MenuItem("Maverick/Aircraft/Run Aircraft Startup Order Validation")]
        public static void RunValidation()
        {
            int passed = 0;
            int failed = 0;
            StringBuilder report = new StringBuilder(4096);
            report.AppendLine("Maverick AIRCRAFT STARTUP ORDER Validation");
            report.AppendLine("==========================================");

            // The session is global mutable state, so it is saved and restored around the run.
            MavAircraftKind savedAircraft = MavGameSession.SelectedAircraft;
            MavGameMode savedMode = MavGameSession.SelectedMode;
            bool savedHasSelection = MavGameSession.HasSelection;

            try
            {
                ValidateWtFeelNeverChoosesIdentity(report, ref passed, ref failed);
                ValidateF16Startup(report, ref passed, ref failed);
                ValidateF22Startup(report, ref passed, ref failed);
                ValidateNoSelectionStartup(report, ref passed, ref failed);
                ValidateReapplyUsesAppliedProfile(report, ref passed, ref failed);
                ValidateNobodyElseReadsTheRequestField(report, ref passed, ref failed);
                ValidateSceneHandoff(report, ref passed, ref failed);
                ValidateFreshBootstrapHandoff(report, ref passed, ref failed);
            }
            finally
            {
                MavGameSession.SelectedAircraft = savedAircraft;
                MavGameSession.SelectedMode = savedMode;
                MavGameSession.HasSelection = savedHasSelection;
            }

            report.AppendLine();
            report.Append("RESULT: ").Append(failed == 0 ? "PASS" : "FAIL")
                .Append("  passed=").Append(passed).Append(" failed=").Append(failed);

            if (failed == 0)
                Debug.Log(report.ToString());
            else
                Debug.LogError(report.ToString());
        }

        // ================================================================== [S0]

        /// <summary>
        /// The exact reported call, in the exact reported condition: WT feel invoked while no
        /// aircraft has been applied, on an applier whose serialized field says F22A.
        /// </summary>
        private static void ValidateWtFeelNeverChoosesIdentity(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[S0] WT feel never chooses aircraft identity");

            MavGameSession.SelectedAircraft = MavAircraftKind.F16C;
            MavGameSession.HasSelection = true;

            MavStartupRig rig = MavStartupRig.Build();
            try
            {
                // The condition that produced the bug: the inspector field says F-22, and nothing has
                // been applied.
                rig.applier.aircraft = MavAircraftKind.F22A;

                Record(!rig.applier.HasAuthoritativeAircraft,
                    "a freshly built player has no authoritative aircraft, even with F22A sitting in "
                    + "the applier's serialized field",
                    report, ref passed, ref failed);

                float massBefore = rig.rigidbody.mass;

                // This is the call MavFreshBootstrap makes at the end of SetupFreshMouseFlight,
                // with the same preset argument.
                MavLogWatch watch = MavLogWatch.Begin();
                rig.wtPolish.ApplyPreset(MavWTFeelPreset.WarThunderF15Balanced);
                watch.End();

                Record(!rig.applier.HasAuthoritativeAircraft,
                    "WT feel did not make any aircraft authoritative",
                    report, ref passed, ref failed);
                Record(rig.applier.AppliedRevision == 0,
                    "WT feel applied NO aircraft profile at all (revision "
                    + rig.applier.AppliedRevision + ")",
                    report, ref passed, ref failed);
                Record(!watch.SawAircraftApplied(),
                    "and nothing logged an aircraft application: " + watch.Describe(),
                    report, ref passed, ref failed);
                Record(!watch.SawAircraftApplied("F-22A"),
                    "in particular, F-22A RAPTOR was never applied - the exact line the bug report "
                    + "showed",
                    report, ref passed, ref failed);
                Record(Mathf.Abs(rig.rigidbody.mass - massBefore) < 0.001f,
                    "and the Rigidbody mass is untouched, so no aircraft's physics was installed ("
                    + rig.rigidbody.mass.ToString("F0") + " kg)",
                    report, ref passed, ref failed);
                Record(!rig.visualSwitcher.HasCommittedVisual,
                    "and no aircraft visual was committed",
                    report, ref passed, ref failed);

                // Skipping must be a visible decision, not a silent no-op.
                Record(rig.wtPolish.lastApplied != null
                       && rig.wtPolish.lastApplied.Contains("no aircraft applied yet"),
                    "WT feel records WHY it skipped: " + rig.wtPolish.lastApplied,
                    report, ref passed, ref failed);
            }
            finally
            {
                rig.Destroy();
            }
        }

        // ================================================================== [S1] / [S2] / [S3]

        private static void ValidateF16Startup(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[S1] Session selects F16C -> startup ends on the F-16");

            RunStartupSequence(
                "F-16 selection", MavAircraftKind.F16C, true,
                MavAircraftKind.F16C, 9800f, false,
                report, ref passed, ref failed);
        }

        private static void ValidateF22Startup(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[S2] Session selects F22A -> startup ends on the F-22");

            // The aircraft that used to be the silent substitute must still work when it is the one
            // actually chosen, or the fix would have broken the default aircraft.
            RunStartupSequence(
                "F-22 selection", MavAircraftKind.F22A, true,
                MavAircraftKind.F22A, 16500f, true,
                report, ref passed, ref failed);
        }

        private static void ValidateNoSelectionStartup(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[S3] No selection -> nothing is applied until the bootstrap decides");

            MavGameSession.HasSelection = false;
            MavGameSession.SelectedAircraft = MavGameSession.DefaultAircraft;

            MavStartupRig rig = MavStartupRig.Build();
            try
            {
                rig.applier.aircraft = MavAircraftKind.F22A;

                MavLogWatch watch = MavLogWatch.Begin();
                rig.wtPolish.ApplyPreset(MavWTFeelPreset.WarThunderF15Balanced);
                watch.End();

                Record(!rig.applier.HasAuthoritativeAircraft && rig.applier.AppliedRevision == 0,
                    "with no selection at all, WT feel still applies no aircraft rather than taking "
                    + "the default",
                    report, ref passed, ref failed);
                Record(!watch.SawAircraftApplied(),
                    "and nothing was applied on the way: " + watch.Describe(),
                    report, ref passed, ref failed);

                // The bootstrap's own documented fallback is a separate, deliberate decision. It is
                // allowed to pick the default aircraft - what it must not do is arrive there by
                // accident.
                MavAircraftKind bootstrapChoice = MavGameSession.HasSelection
                    ? MavGameSession.SelectedAircraft
                    : MavAircraftKind.F22A;

                string error;
                Record(rig.applier.TryApplyAircraft(bootstrapChoice, out error),
                    "the bootstrap can then apply its declared fallback explicitly (" + error + ")",
                    report, ref passed, ref failed);
                Record(rig.applier.HasAuthoritativeAircraft
                       && rig.applier.AppliedAircraft == bootstrapChoice,
                    "and only THAT makes an aircraft authoritative",
                    report, ref passed, ref failed);
            }
            finally
            {
                rig.Destroy();
            }
        }

        /// <summary>
        /// The full ordered sequence: session selection, identity applied, then the fresh-bootstrap
        /// WT preset pass that used to overwrite it.
        /// </summary>
        private static void RunStartupSequence(
            string label,
            MavAircraftKind selected,
            bool hasSelection,
            MavAircraftKind expected,
            float expectedMass,
            bool expectTvc,
            StringBuilder report,
            ref int passed,
            ref int failed)
        {
            MavGameSession.SelectedAircraft = selected;
            MavGameSession.HasSelection = hasSelection;

            MavStartupRig rig = MavStartupRig.Build();
            try
            {
                // A serialized field pointing at the WRONG aircraft, so that anything reading it
                // instead of the selection shows up as a failure rather than a coincidence.
                rig.applier.aircraft = selected == MavAircraftKind.F22A
                    ? MavAircraftKind.F16C
                    : MavAircraftKind.F22A;

                MavLogWatch watch = MavLogWatch.Begin();

                // --- step 1: the bootstrap resolves the session selection -----------------------
                MavAircraftRuntimeProfile profile;
                string error;
                bool resolved = MavAircraftCatalog.TryGetBuiltIn(
                    MavGameSession.SelectedAircraft, out profile, out error);

                Record(resolved && profile != null && profile.aircraft == expected,
                    label + ": the session selection resolves to " + expected + " (" + error + ")",
                    report, ref passed, ref failed);

                if (!resolved)
                {
                    watch.End();
                    return;
                }

                // --- step 2: identity becomes authoritative BEFORE anything aircraft-aware -------
                bool applied = rig.applier.TryApplyProfile(profile, out error);
                Record(applied, label + ": the selection is applied atomically (" + error + ")",
                    report, ref passed, ref failed);
                Record(rig.applier.HasAuthoritativeAircraft
                       && rig.applier.AppliedAircraft == expected,
                    label + ": identity is authoritative and is " + expected,
                    report, ref passed, ref failed);

                int revisionAfterApply = rig.applier.AppliedRevision;

                // --- step 3: SetupFreshMouseFlight's WT preset pass -----------------------------
                rig.wtPolish.ApplyPreset(MavWTFeelPreset.WarThunderF15Balanced);
                watch.End();

                Record(rig.applier.AppliedRevision > revisionAfterApply,
                    label + ": WT feel refreshed the aircraft rather than skipping it, now that an "
                    + "identity exists",
                    report, ref passed, ref failed);

                // --- the claims that matter -----------------------------------------------------
                Record(rig.applier.AppliedAircraft == expected,
                    label + ": the applier still reports " + expected + " after the WT pass (got "
                    + rig.applier.AppliedAircraft + ")",
                    report, ref passed, ref failed);
                Record(rig.visualSwitcher.activeAircraft == expected,
                    label + ": the visual switcher reports " + expected + " (got "
                    + rig.visualSwitcher.activeAircraft + ")",
                    report, ref passed, ref failed);
                Record(Mathf.Abs(rig.rigidbody.mass - expectedMass) < 0.5f,
                    label + ": the Rigidbody carries " + expectedMass.ToString("F0") + " kg (got "
                    + rig.rigidbody.mass.ToString("F0") + ")",
                    report, ref passed, ref failed);

                MavThrustVectorControl tvc = rig.gameObject.GetComponent<MavThrustVectorControl>();
                if (tvc != null)
                {
                    Record(tvc.useThrustVectorControl == expectTvc,
                        label + ": thrust vectoring is " + (expectTvc ? "on" : "off")
                        + ", which is an aircraft-specific tell",
                        report, ref passed, ref failed);
                }

                // The heart of it: no OTHER aircraft may appear anywhere in the sequence.
                MavAircraftKind[] kinds =
                    (MavAircraftKind[])System.Enum.GetValues(typeof(MavAircraftKind));
                bool onlyExpectedApplied = true;
                string offender = "none";

                for (int i = 0; i < kinds.Length; i++)
                {
                    if (kinds[i] == expected)
                        continue;

                    MavAircraftRuntimeProfile other = MavAircraftCatalog.GetBuiltIn(kinds[i]);
                    if (other == null)
                        continue;

                    if (watch.SawAircraftApplied(other.displayName))
                    {
                        onlyExpectedApplied = false;
                        offender = other.displayName;
                        break;
                    }
                }

                Record(onlyExpectedApplied,
                    label + ": no other aircraft was applied at any point during the sequence, "
                    + "intermediate or final (offender: " + offender + ")",
                    report, ref passed, ref failed);
                Record(watch.SawAircraftApplied(profile.displayName),
                    label + ": and the one that WAS applied is " + profile.displayName,
                    report, ref passed, ref failed);
            }
            finally
            {
                rig.Destroy();
            }
        }

        // ================================================================== [S4]

        private static void ValidateReapplyUsesAppliedProfile(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[S4] A later re-apply restates the applied aircraft, not the field");

            MavGameSession.SelectedAircraft = MavAircraftKind.F16C;
            MavGameSession.HasSelection = true;

            MavStartupRig rig = MavStartupRig.Build();
            try
            {
                string error;
                Record(rig.applier.TryApplyAircraft(MavAircraftKind.F16C, out error),
                    "setup: the F-16 is applied (" + error + ")",
                    report, ref passed, ref failed);

                // Something writes the serialized field afterwards - a stale prefab value, an
                // inspector edit, an editor tool. A re-apply must ignore it.
                rig.applier.aircraft = MavAircraftKind.F22A;

                MavLogWatch watch = MavLogWatch.Begin();
                bool reapplied = rig.applier.TryReapplyAppliedProfile(out error);
                watch.End();

                Record(reapplied, "the applied profile can be re-applied (" + error + ")",
                    report, ref passed, ref failed);
                Record(rig.applier.AppliedAircraft == MavAircraftKind.F16C,
                    "and it re-applied the F-16, not the F22A now sitting in the serialized field "
                    + "(got " + rig.applier.AppliedAircraft + ")",
                    report, ref passed, ref failed);
                Record(!watch.SawAircraftApplied("F-22A"),
                    "with no F-22A application logged",
                    report, ref passed, ref failed);
                Record(Mathf.Abs(rig.rigidbody.mass - 9800f) < 0.5f,
                    "and F-16 mass is still in force (" + rig.rigidbody.mass.ToString("F0") + " kg)",
                    report, ref passed, ref failed);

                // A repeated WT pass must be equally immune.
                watch = MavLogWatch.Begin();
                rig.wtPolish.ApplyPreset(MavWTFeelPreset.WarThunderF15Aggressive);
                watch.End();

                Record(rig.applier.AppliedAircraft == MavAircraftKind.F16C
                       && !watch.SawAircraftApplied("F-22A"),
                    "a second WT preset pass also keeps the F-16",
                    report, ref passed, ref failed);
            }
            finally
            {
                rig.Destroy();
            }
        }

        // ================================================================== [S5]

        /// <summary>
        /// The rule as an invariant over the sources, not just over this rig.
        ///
        /// The component checks above prove that WT feel behaves. They cannot prove that the NEXT
        /// aircraft-aware component to be written will, and the bug was a one-line read that looked
        /// entirely reasonable in isolation. So the ban is enforced against the whole script tree.
        /// </summary>
        private static void ValidateNobodyElseReadsTheRequestField(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[S5] No component reads the applier's request field as identity");

            string root = Path.Combine(Application.dataPath, "MaverickFresh/Scripts");
            MavIdentityOwnershipScanResult scan = MavAircraftIdentityOwnershipScan.Scan(root);

            Record(scan.sourcesAvailable,
                "the script sources are readable for scanning (" + root + ")",
                report, ref passed, ref failed);

            if (!scan.sourcesAvailable)
                return;

            Record(scan.violations.Count == 0,
                "no file outside the exemption list reads applier.aircraft as identity ("
                + scan.filesScanned + " files scanned, " + scan.violations.Count + " violations)",
                report, ref passed, ref failed);

            for (int i = 0; i < scan.violations.Count; i++)
                report.Append("        ").AppendLine(scan.violations[i]);

            // A scan whose detector is broken reports a clean tree forever, so prove it detects.
            Record(MavAircraftIdentityOwnershipScan.IsIdentityOwnershipViolation(
                       "var p = MavAircraftCatalog.GetBuiltIn(applier.aircraft);"),
                "the detector flags the exact line that caused the bug",
                report, ref passed, ref failed);
            Record(!MavAircraftIdentityOwnershipScan.IsIdentityOwnershipViolation(
                       "var k = applier.AppliedAircraft;"),
                "and accepts the authoritative property",
                report, ref passed, ref failed);

            // And that the exemption list has not quietly grown.
            Record(MavAircraftIdentityOwnershipScan.ExemptFileNames.Length == 3
                   && !MavAircraftIdentityOwnershipScan.IsExemptFile("MavWTFeelPolishController.cs"),
                "the exemption list still holds only the declaring file and the two validation "
                + "suites, and WT feel is not among them",
                report, ref passed, ref failed);
        }

        // ================================================================== [S6]

        /// <summary>
        /// Every scene carrying the aircraft machinery must contain something that applies the
        /// selection.
        ///
        /// This is the check that was missing when the bug survived a 60-assertion suite.
        /// Mav_InGame has the applier, the visual switcher and the aircraft-aware tuning controller,
        /// but not MavInGameBootstrap - which was the only thing that applied the session selection.
        /// Nothing in the scene performed the handoff, and every existing test built its own rig and
        /// applied the aircraft by hand, so none of them could notice.
        /// </summary>
        private static void ValidateSceneHandoff(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[S6] Every scene with the aircraft machinery can apply a selection");

            string assets = Application.dataPath;
            MavSceneWiringScanResult scan = MavAircraftSceneWiringScan.Scan(assets);

            Record(scan.sourcesAvailable,
                "scenes and script GUIDs are readable for scanning",
                report, ref passed, ref failed);

            if (!scan.sourcesAvailable)
            {
                for (int i = 0; i < scan.violations.Count; i++)
                    report.Append("        ").AppendLine(scan.violations[i]);

                return;
            }

            Record(scan.scenesScanned > 0,
                "scanned " + scan.scenesScanned + " scenes",
                report, ref passed, ref failed);

            for (int i = 0; i < scan.scenes.Count; i++)
            {
                MavSceneWiring wiring = scan.scenes[i];
                report.Append("        ").Append(wiring.sceneName)
                    .Append("  applier=").Append(wiring.hasProfileApplier)
                    .Append(" switcher=").Append(wiring.hasVisualSwitcher)
                    .Append(" tuning=").Append(wiring.hasAircraftAwareTuning)
                    .Append(" inGameBootstrap=").Append(wiring.hasInGameBootstrap)
                    .Append(" freshBootstrap=").Append(wiring.hasFreshBootstrap)
                    .Append(" -> identityOwner=").Append(wiring.HasIdentityOwner)
                    .AppendLine();
            }

            Record(scan.violations.Count == 0,
                "no scene carries an applier that nothing drives (" + scan.violations.Count
                + " violations)",
                report, ref passed, ref failed);

            for (int i = 0; i < scan.violations.Count; i++)
                report.Append("        ").AppendLine(scan.violations[i]);
        }

        // ================================================================== [S7]

        /// <summary>
        /// Presence of an owner is not enough - it has to do the job, early enough.
        ///
        /// MavFreshBootstrap sat in Mav_InGame throughout the bug without applying any aircraft, so
        /// counting the component would have called the broken scene clean.
        /// </summary>
        private static void ValidateFreshBootstrapHandoff(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[S7] The scene's startup owner really performs the handoff");

            string reason;
            Record(MavAircraftSceneWiringScan.VerifyFreshBootstrapAppliesSelection(
                       Application.dataPath, out reason),
                "MavFreshBootstrap applies the session selection before anything aircraft-aware: "
                + reason,
                report, ref passed, ref failed);

            // And the component-level proof of the same thing, on a real rig with no higher-level
            // bootstrap present - which is exactly the Mav_InGame situation.
            MavGameSession.SelectedAircraft = MavAircraftKind.F16C;
            MavGameSession.HasSelection = true;

            MavStartupRig rig = MavStartupRig.Build();
            GameObject host = new GameObject("MavFreshBootstrap_TestHost");
            host.hideFlags = HideFlags.HideAndDontSave;

            try
            {
                MavFreshBootstrap fresh = host.AddComponent<MavFreshBootstrap>();
                fresh.setupOnAwake = false;
                fresh.aircraftObject = rig.gameObject;
                fresh.applySelectedAircraft = true;

                Record(!rig.applier.HasAuthoritativeAircraft,
                    "the player starts with no authoritative aircraft",
                    report, ref passed, ref failed);

                MavLogWatch watch = MavLogWatch.Begin();
                bool ok = fresh.EnsureAuthoritativeAircraftApplied();
                watch.End();

                Record(ok, "the handoff reports success: " + fresh.aircraftIdentityStatus,
                    report, ref passed, ref failed);
                Record(rig.applier.HasAuthoritativeAircraft,
                    "the aircraft is now authoritative - Debug Applied Aircraft is no longer 'none'",
                    report, ref passed, ref failed);
                Record(rig.applier.AppliedAircraft == MavAircraftKind.F16C,
                    "and it is the selected F16C (got " + rig.applier.AppliedAircraft + ")",
                    report, ref passed, ref failed);
                Record(rig.visualSwitcher.activeAircraft == MavAircraftKind.F16C,
                    "the visual switcher reports F16C rather than its F22A default (got "
                    + rig.visualSwitcher.activeAircraft + ")",
                    report, ref passed, ref failed);
                Record(rig.visualSwitcher.HasCommittedVisual,
                    "Active Visual is no longer None",
                    report, ref passed, ref failed);
                Record(Mathf.Abs(rig.rigidbody.mass - 9800f) < 0.5f,
                    "and F-16 mass is in force (" + rig.rigidbody.mass.ToString("F0") + " kg)",
                    report, ref passed, ref failed);
                Record(!watch.SawAircraftApplied("F-22A"),
                    "with no F-22A applied at any point",
                    report, ref passed, ref failed);

                // The applier's serialized field must have had no influence.
                rig.applier.aircraft = MavAircraftKind.F35A;
                watch = MavLogWatch.Begin();
                fresh.EnsureAuthoritativeAircraftApplied();
                watch.End();

                Record(rig.applier.AppliedAircraft == MavAircraftKind.F16C,
                    "a second pass restates the F-16 and ignores the serialized field, now set to "
                    + "F35A (got " + rig.applier.AppliedAircraft + ")",
                    report, ref passed, ref failed);
                Record(!watch.SawAircraftApplied("F-35A"),
                    "and no F-35A was applied",
                    report, ref passed, ref failed);
            }
            finally
            {
                Object.DestroyImmediate(host);
                rig.Destroy();
            }
        }

        // ================================================================== log watch

        /// <summary>
        /// Captures Unity log messages for the span of a sequence.
        ///
        /// This is here because the requirement is about what happens ON THE WAY, not just where the
        /// aircraft ends up. Asserting the final identity cannot distinguish "the F-16 was applied"
        /// from "the F-22 was applied and then overwritten by the F-16" - and the second is what the
        /// bug report showed. The applier logs every application, so the log is the record of the
        /// whole sequence.
        /// </summary>
        private sealed class MavLogWatch
        {
            private const string AppliedPrefix = "MavAircraftProfileApplier applied ";

            private readonly List<string> messages = new List<string>(16);
            private bool listening;

            public static MavLogWatch Begin()
            {
                MavLogWatch watch = new MavLogWatch();
                watch.listening = true;
                Application.logMessageReceived += watch.OnLog;
                return watch;
            }

            public void End()
            {
                if (!listening)
                    return;

                Application.logMessageReceived -= OnLog;
                listening = false;
            }

            private void OnLog(string condition, string stackTrace, LogType type)
            {
                if (!string.IsNullOrEmpty(condition) && condition.Contains(AppliedPrefix))
                    messages.Add(condition);
            }

            /// <summary>Whether any aircraft application was logged during the span.</summary>
            public bool SawAircraftApplied()
            {
                return messages.Count > 0;
            }

            /// <summary>Whether an aircraft whose name contains this text was applied.</summary>
            public bool SawAircraftApplied(string displayNameFragment)
            {
                if (string.IsNullOrEmpty(displayNameFragment))
                    return false;

                for (int i = 0; i < messages.Count; i++)
                {
                    if (messages[i].Contains(displayNameFragment))
                        return true;
                }

                return false;
            }

            public string Describe()
            {
                if (messages.Count == 0)
                    return "no aircraft applications logged";

                StringBuilder text = new StringBuilder(128);
                for (int i = 0; i < messages.Count; i++)
                {
                    if (i > 0)
                        text.Append(" | ");

                    text.Append(messages[i]);
                }

                return text.ToString();
            }
        }

        // ================================================================== rig

        /// <summary>
        /// A Mav_Player-shaped object carrying the components the reported stack actually touches:
        /// the jet WT feel tunes, the WT controller itself, the applier, and the visual switcher.
        ///
        /// It reproduces the sequence from SetupFreshMouseFlight's WT preset call onwards, which is
        /// where the fault was. It deliberately does NOT run MavFreshBootstrap.SetupFreshMouseFlight
        /// itself: that builds cameras, starters and environment into the open scene, and an editor
        /// validation run must not edit the scene the user has open.
        ///
        /// Every global lookup is pre-satisfied with a component on this rig - jet rig lookup,
        /// instructor, mouse-flight rig - so nothing in the open scene is read or written. The WT
        /// aircraft-aware path multiplies rig camera fields, and without a local rig assigned it
        /// would find and mutate the scene's.
        /// </summary>
        private sealed class MavStartupRig
        {
            public GameObject gameObject;
            public Rigidbody rigidbody;
            public MavAircraftProfileApplier applier;
            public MavAircraftVisualSwitcher visualSwitcher;
            public MavMouseFlightJet jet;
            public MavWTFeelPolishController wtPolish;
            public MavMouseFlightRig mouseRig;

            public static MavStartupRig Build()
            {
                MavStartupRig rig = new MavStartupRig();

                rig.gameObject = new GameObject("Mav_Player_StartupTestRig");
                rig.gameObject.hideFlags = HideFlags.HideAndDontSave;

                // MavMouseFlightJet requires a Rigidbody, so this brings one.
                rig.jet = rig.gameObject.AddComponent<MavMouseFlightJet>();
                rig.rigidbody = rig.gameObject.GetComponent<Rigidbody>();
                rig.rigidbody.useGravity = false;

                // Keep the jet from reaching into the open scene for a rig.
                rig.jet.allowGlobalRigLookup = false;

                rig.gameObject.AddComponent<MavAeroBody>();
                rig.gameObject.AddComponent<MavAtmosphericEngine>();
                rig.gameObject.AddComponent<MavThrustVectorControl>();

                rig.applier = rig.gameObject.AddComponent<MavAircraftProfileApplier>();
                rig.applier.applyOnStart = false;
                rig.applier.renameObject = false;
                rig.applier.allowGlobalRigLookup = false;

                rig.visualSwitcher = rig.gameObject.AddComponent<MavAircraftVisualSwitcher>();
                rig.visualSwitcher.autoFindSceneVisuals = false;
                rig.visualSwitcher.claimLooseSceneVisuals = false;
                rig.visualSwitcher.preclaimAllResolvedVisuals = false;
                rig.visualSwitcher.createPlaceholderIfMissing = true;

                // A local mouse-flight rig, so WT feel's camera multipliers never touch the scene's.
                rig.mouseRig = rig.gameObject.AddComponent<MavMouseFlightRig>();

                rig.wtPolish = rig.gameObject.AddComponent<MavWTFeelPolishController>();
                rig.wtPolish.applyOnStart = false;
                rig.wtPolish.jet = rig.jet;
                rig.wtPolish.rig = rig.mouseRig;

                return rig;
            }

            public void Destroy()
            {
                if (gameObject != null)
                    Object.DestroyImmediate(gameObject);

                gameObject = null;
            }
        }

        private static void Record(bool ok, string name, StringBuilder report, ref int passed, ref int failed)
        {
            if (ok)
            {
                passed++;
                report.Append("  PASS  ").AppendLine(name);
            }
            else
            {
                failed++;
                report.Append("  FAIL  ").AppendLine(name);
            }
        }
    }
}
#endif
