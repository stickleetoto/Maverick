using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using MaverickFresh.FlightDynamics;
using MaverickFresh.FlightDynamics.Validation;

namespace MaverickFresh.FlightDynamics.EditorTools
{
    /// <summary>
    /// Phase 5C-R driver: runs the isolated F-16 reference-flight scenarios inside real Unity physics.
    ///
    /// The scenarios need PhysX, so they need Play Mode, so they cannot run inside one synchronous
    /// editor call. This is the same shape as the P0.2b lifecycle machine: progress lives in
    /// SessionState because entering Play Mode reloads the managed domain, and an
    /// <c>[InitializeOnLoad]</c> static constructor re-hooks the callbacks on every domain load.
    ///
    /// ISOLATION. Play Mode is entered on the empty unsaved scene batchmode already has, and the
    /// driver refuses to run if a saved scene is open - so no production scene's gameplay can start.
    /// Everything the experiment needs is built in memory by MavF16ReferenceRigBuilder and destroyed
    /// afterwards. No scene, prefab, material or model is touched, and gameplay F16Replacement is not
    /// enabled: the ownership component that grants physics to the reference stack exists only on the
    /// temporary rig object.
    ///
    /// Headless:
    ///   -executeMethod MaverickFresh.FlightDynamics.EditorTools.MavF16ReferenceFlightValidation.RunBatch
    /// Do NOT pass -quit: the machine needs the editor to keep ticking and exits by itself.
    /// </summary>
    [InitializeOnLoad]
    public static class MavF16ReferenceFlightValidation
    {
        private const string KeyPhase = "Mav.P5CR.Phase";
        private const string KeyBatch = "Mav.P5CR.Batch";
        private const string KeyOut = "Mav.P5CR.Out";
        private const string KeyCsv = "Mav.P5CR.Csv";
        private const string KeyReport = "Mav.P5CR.Report";
        private const string KeyPreamble = "Mav.P5CR.Preamble";
        private const string KeyPrePassed = "Mav.P5CR.PrePassed";
        private const string KeyPreFailed = "Mav.P5CR.PreFailed";
        private const string KeyDeadline = "Mav.P5CR.Deadline";
        private const string KeyPassed = "Mav.P5CR.Passed";
        private const string KeyFailed = "Mav.P5CR.Failed";

        private const int PhaseIdle = 0;
        private const int PhaseRequestPlay = 1;
        private const int PhaseInPlay = 2;
        private const int PhaseLeftPlay = 3;

        static MavF16ReferenceFlightValidation()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        [MenuItem("Maverick/Flight Dynamics/Run Phase 5C-R Reference Flight Validation")]
        public static void RunFromMenu()
        {
            SessionState.SetBool(KeyBatch, false);
            SessionState.SetString(KeyOut, string.Empty);
            SessionState.SetString(KeyCsv, string.Empty);
            Start();
        }

        public static void RunBatch()
        {
            SessionState.SetBool(KeyBatch, true);
            SessionState.SetString(KeyOut, CommandLineValue("-p5crOut") ?? string.Empty);
            SessionState.SetString(KeyCsv, CommandLineValue("-p5crCsv") ?? string.Empty);
            Start();
        }

        private static string CommandLineValue(string flag)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == flag)
                    return args[i + 1];
            }

            return null;
        }

        private static void Start()
        {
            SessionState.SetString(KeyReport, string.Empty);
            SessionState.SetInt(KeyPassed, 0);
            SessionState.SetInt(KeyFailed, 0);
            SessionState.SetFloat(KeyDeadline, (float)EditorApplication.timeSinceStartup + 900f);

            string scenePath = SceneManager.GetActiveScene().path;
            if (!string.IsNullOrEmpty(scenePath))
            {
                Finish("ABORTED: refusing to enter Play Mode with the saved scene \"" + scenePath
                       + "\" open. Phase 5C-R runs on an empty scene so no production gameplay starts.",
                    0, 1);
                return;
            }

            RunSerializationAudit();
            SessionState.SetInt(KeyPhase, PhaseRequestPlay);
        }

        /// <summary>
        /// S-001: can a serialized MavSixDoFBody silently run with the Euler correction disabled?
        ///
        /// Two ways that could happen, and both are checked:
        ///
        ///   1. A component serialized BEFORE the field existed. Unity constructs the object - running
        ///      field initialisers - and then applies whatever the serialized data contains, so a key
        ///      that is not in the data keeps its initialiser. That should give true. Checked by
        ///      round-tripping through Unity's own serializer with the key removed.
        ///
        ///   2. A component serialized WITH the field false. That one is real, and the initialiser
        ///      cannot help: readiness has to refuse it. Checked here end to end on a live component,
        ///      because G-009 checks the rule and this checks that the component actually feeds it.
        ///
        /// Runs in edit mode before Play Mode is requested, so a failure is reported without spending
        /// a physics run on it.
        /// </summary>
        private static void RunSerializationAudit()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder(2048);
            int passed = 0;
            int failed = 0;

            sb.AppendLine();
            sb.AppendLine("[S-001] gyroscopic compensation cannot be disabled silently");

            GameObject host = new GameObject("p5cr-serialization-audit");
            try
            {
                MavSixDoFBody body = host.AddComponent<MavSixDoFBody>();

                Audit(body.applyBackendGyroscopicCompensation, "S-001",
                    "a freshly constructed MavSixDoFBody has the compensation ON by default",
                    sb, ref passed, ref failed);

                // 1. Serialize, strip the key, deserialize - i.e. data written before the field
                //    existed. Unity's own serializer, not a stand-in.
                string json = EditorJsonUtility.ToJson(body);
                bool keyPresent = json.Contains("applyBackendGyroscopicCompensation");
                string stripped = json
                    .Replace("\"applyBackendGyroscopicCompensation\":true,", string.Empty)
                    .Replace("\"applyBackendGyroscopicCompensation\":false,", string.Empty);

                Audit(keyPresent, "S-001b",
                    "the field is serialized by Unity at all, so removing it from the payload is a "
                    + "real simulation of older data rather than a no-op",
                    sb, ref passed, ref failed);

                // Overwritten onto a FRESH component, which is what Unity actually does: it
                // constructs the managed object - running field initialisers - and then applies the
                // serialized data over it.
                //
                // The first version of this check set the field to false on an EXISTING component and
                // then overwrote it, and failed. That was the test being wrong, not the engine:
                // FromJsonOverwrite leaves a key it does not find at whatever the target currently
                // holds, so it was measuring a value the test itself had just written. Modelling the
                // real path means starting from a freshly constructed component.
                GameObject freshHost = new GameObject("p5cr-serialization-audit-fresh");
                MavSixDoFBody fresh = freshHost.AddComponent<MavSixDoFBody>();
                EditorJsonUtility.FromJsonOverwrite(stripped, fresh);
                bool absentKeyKeepsDefault = fresh.applyBackendGyroscopicCompensation;

                // And the key IS honoured when present, or the check above would pass for the wrong
                // reason - a serializer that ignored the field entirely would also leave it true.
                string explicitlyOff = json
                    .Replace("\"applyBackendGyroscopicCompensation\":true",
                             "\"applyBackendGyroscopicCompensation\":false");
                GameObject offHost = new GameObject("p5cr-serialization-audit-off");
                MavSixDoFBody offBody = offHost.AddComponent<MavSixDoFBody>();
                EditorJsonUtility.FromJsonOverwrite(explicitlyOff, offBody);
                bool presentKeyApplied = !offBody.applyBackendGyroscopicCompensation;

                UnityEngine.Object.DestroyImmediate(freshHost);
                UnityEngine.Object.DestroyImmediate(offHost);

                Audit(absentKeyKeepsDefault, "S-001c",
                    "data written BEFORE the field existed deserializes with the correction ON - "
                    + "Unity constructs the component, running the field initialiser, then applies "
                    + "only the keys the payload contains, so an existing serialized MavSixDoFBody "
                    + "cannot come back with it silently off",
                    sb, ref passed, ref failed);
                Audit(presentKeyApplied, "S-001c2",
                    "while a payload that DOES carry the key still sets it, so the check above is not "
                    + "passing because the serializer ignores the field",
                    sb, ref passed, ref failed);

                // 2. Explicitly serialized false. The initialiser cannot save this one; readiness must.
                body.applyBackendGyroscopicCompensation = false;
                body.acknowledgeIncompleteAngularDynamicsForTesting = false;
                Audit(!body.AngularDynamicsAcceptable, "S-001d",
                    "a component with the compensation explicitly OFF and nothing acknowledged reports "
                    + "its angular dynamics as unacceptable",
                    sb, ref passed, ref failed);

                MavFlightDynamicsReadinessInputs inputs = body.BuildReadinessInputs();
                Audit(!inputs.angularDynamicsAccepted, "S-001e",
                    "the component feeds that through to the readiness inputs, so the rule G-009 "
                    + "checks is actually reached from a live component",
                    sb, ref passed, ref failed);

                body.acknowledgeIncompleteAngularDynamicsForTesting = true;
                Audit(body.AngularDynamicsAcceptable
                      && body.BuildReadinessInputs().angularDynamicsAccepted, "S-001f",
                    "while the explicit testing acknowledgement restores acceptance, which is the "
                    + "only route to the naive dynamics and it takes a deliberate second field",
                    sb, ref passed, ref failed);
            }
            catch (Exception e)
            {
                failed++;
                sb.Append("  FAIL  S-001-CRASH  the serialization audit threw ")
                  .Append(e.GetType().Name).Append(": ").AppendLine(e.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }

            SessionState.SetString(KeyPreamble, sb.ToString());
            SessionState.SetInt(KeyPrePassed, passed);
            SessionState.SetInt(KeyPreFailed, failed);
        }

        private static void Audit(
            bool condition, string id, string text,
            System.Text.StringBuilder sb, ref int passed, ref int failed)
        {
            if (condition)
            {
                passed++;
                sb.Append("  PASS  ").Append(id).Append("  ").AppendLine(text);
            }
            else
            {
                failed++;
                sb.Append("  FAIL  ").Append(id).Append("  ").AppendLine(text);
            }
        }

        private static void OnEditorUpdate()
        {
            int phase = SessionState.GetInt(KeyPhase, PhaseIdle);
            if (phase == PhaseIdle)
                return;

            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(KeyDeadline, 0f))
            {
                SessionState.SetInt(KeyPhase, PhaseIdle);
                if (EditorApplication.isPlaying)
                    EditorApplication.isPlaying = false;

                Finish("FAILED: the Phase 5C-R machine exceeded its time budget in phase " + phase
                       + ". Recorded as a failure, never as a pass by timeout.", 0, 1);
                return;
            }

            if (phase == PhaseRequestPlay)
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                    EditorApplication.isPlaying = true;

                return;
            }

            if (phase != PhaseInPlay)
                return;

            if (!EditorApplication.isPlaying || !MavF16ReferenceFlightScenarioRunner.Finished)
                return;

            // Harvest BEFORE leaving Play Mode: exiting reloads the domain and clears the statics.
            SessionState.SetString(KeyReport, MavF16ReferenceFlightScenarioRunner.Report);
            SessionState.SetInt(KeyPassed, MavF16ReferenceFlightScenarioRunner.Passed);
            SessionState.SetInt(KeyFailed, MavF16ReferenceFlightScenarioRunner.Failed);

            string csvPath = SessionState.GetString(KeyCsv, string.Empty);
            if (!string.IsNullOrEmpty(csvPath)
                && !string.IsNullOrEmpty(MavF16ReferenceFlightScenarioRunner.LastCsv))
            {
                try
                {
                    System.IO.File.WriteAllText(
                        csvPath, MavF16ReferenceFlightScenarioRunner.LastCsv);
                    Console.WriteLine("wrote telemetry CSV for "
                                      + MavF16ReferenceFlightScenarioRunner.CsvScenarioName
                                      + " to " + csvPath);
                }
                catch (Exception e)
                {
                    Console.WriteLine("could not write " + csvPath + ": " + e.Message);
                }
            }

            SessionState.SetInt(KeyPhase, PhaseLeftPlay);
            EditorApplication.isPlaying = false;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            int phase = SessionState.GetInt(KeyPhase, PhaseIdle);
            if (phase == PhaseIdle)
                return;

            if (change == PlayModeStateChange.EnteredPlayMode && phase == PhaseRequestPlay)
            {
                SessionState.SetInt(KeyPhase, PhaseInPlay);

                try
                {
                    GameObject host = new GameObject("p5cr-scenario-runner");
                    MavF16ReferenceFlightScenarioRunner runner =
                        host.AddComponent<MavF16ReferenceFlightScenarioRunner>();

                    if (runner == null)
                    {
                        SessionState.SetString(KeyReport,
                            "FAILED: the scenario runner could not be attached.");
                        SessionState.SetInt(KeyFailed, 1);
                        MavF16ReferenceFlightScenarioRunner.Finished = true;
                    }
                }
                catch (Exception e)
                {
                    SessionState.SetString(KeyReport,
                        "FAILED: building the Phase 5C-R rig threw " + e.GetType().Name + ": "
                        + e.Message + "\n" + e.StackTrace);
                    SessionState.SetInt(KeyFailed, 1);
                    MavF16ReferenceFlightScenarioRunner.Finished = true;
                }

                return;
            }

            if (change == PlayModeStateChange.EnteredEditMode && phase == PhaseLeftPlay)
            {
                SessionState.SetInt(KeyPhase, PhaseIdle);
                Finish(
                    SessionState.GetString(KeyReport, "(no report)"),
                    SessionState.GetInt(KeyPassed, 0),
                    SessionState.GetInt(KeyFailed, 0));
            }
        }

        private static void Finish(string report, int passed, int failed)
        {
            // The edit-mode audit runs before Play Mode, so its results are prepended and its counts
            // folded in - a suite that reported only what happened in Play Mode would drop them.
            string preamble = SessionState.GetString(KeyPreamble, string.Empty);
            passed += SessionState.GetInt(KeyPrePassed, 0);
            failed += SessionState.GetInt(KeyPreFailed, 0);

            string text = report;
            if (!string.IsNullOrEmpty(preamble))
            {
                int marker = text.IndexOf("RESULT:", StringComparison.Ordinal);
                text = marker > 0
                    ? text.Substring(0, marker) + preamble + Environment.NewLine
                      + text.Substring(marker)
                    : text + preamble;
            }
            // The runner's own RESULT line predates the audit counts, so it is replaced rather than
            // left to contradict the totals below it.
            int existing = text.LastIndexOf("RESULT:", StringComparison.Ordinal);
            if (existing >= 0)
                text = text.Substring(0, existing);

            text += Environment.NewLine
                    + "RESULT: " + (failed == 0 ? "PASS" : "FAIL")
                    + " passed=" + passed + " failed=" + failed;

            Debug.Log(text);
            Console.WriteLine(text);

            string outPath = SessionState.GetString(KeyOut, string.Empty);
            if (!string.IsNullOrEmpty(outPath))
            {
                try
                {
                    System.IO.File.WriteAllText(outPath, text);
                }
                catch (Exception e)
                {
                    Console.WriteLine("could not write " + outPath + ": " + e.Message);
                }
            }

            if (SessionState.GetBool(KeyBatch, false))
                EditorApplication.Exit(failed == 0 ? 0 : 1);
        }
    }
}
