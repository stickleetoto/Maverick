#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using MaverickFresh.FlightDynamics.Validation;

namespace MaverickFresh.FlightDynamics.EditorTools
{
    /// <summary>
    /// Driver for <see cref="MavF15PilotPhysicsR2FlightValidationRunner"/>: enters Play Mode on the empty unsaved batch
    /// scene, lets the runner fly its temporary rigs, harvests the report and exits. The same machinery as the pilot V1/V2
    /// flight-test drivers, with its own session keys. It refuses to run with a saved scene open, so no production scene,
    /// gameplay or aircraft starts. Editor-only, so it lives in an Editor folder like the V1/V2 drivers: in a runtime folder
    /// its UnityEditor references would enter Assembly-CSharp, which the shared propulsion gate (U-003g) forbids.
    ///
    /// Headless (do NOT pass -quit; the editor exits by itself):
    ///   -executeMethod MaverickFresh.FlightDynamics.EditorTools.MavF15PilotPhysicsR2FlightValidation.RunBatch -f15r2Out &lt;path&gt;
    /// </summary>
    [InitializeOnLoad]
    public static class MavF15PilotPhysicsR2FlightValidation
    {
        private const string KeyPhase = "Mav.F15R2.Phase";
        private const string KeyBatch = "Mav.F15R2.Batch";
        private const string KeyOut = "Mav.F15R2.Out";
        private const string KeyReport = "Mav.F15R2.Report";
        private const string KeyDeadline = "Mav.F15R2.Deadline";
        private const string KeyPassed = "Mav.F15R2.Passed";
        private const string KeyFailed = "Mav.F15R2.Failed";

        private const int PhaseIdle = 0;
        private const int PhaseRequestPlay = 1;
        private const int PhaseInPlay = 2;
        private const int PhaseLeftPlay = 3;

        static MavF15PilotPhysicsR2FlightValidation()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        [MenuItem("Maverick/F-15/Run Pilot Physics R2 Flight Test (Play Mode)")]
        public static void RunFromMenu()
        {
            SessionState.SetBool(KeyBatch, false);
            SessionState.SetString(KeyOut, string.Empty);
            Begin();
        }

        public static void RunBatch()
        {
            SessionState.SetBool(KeyBatch, true);
            SessionState.SetString(KeyOut, CommandLineValue("-f15r2Out") ?? string.Empty);
            Begin();
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

        private static void Begin()
        {
            SessionState.SetString(KeyReport, string.Empty);
            SessionState.SetInt(KeyPassed, 0);
            SessionState.SetInt(KeyFailed, 0);
            SessionState.SetFloat(KeyDeadline, (float)EditorApplication.timeSinceStartup + 1500f);

            string scenePath = SceneManager.GetActiveScene().path;
            if (!string.IsNullOrEmpty(scenePath))
            {
                Finish("ABORTED: refusing to enter Play Mode with the saved scene \"" + scenePath
                       + "\" open. The R2 flight test runs on an empty scene so no production gameplay starts.", 0, 1);
                return;
            }

            SessionState.SetInt(KeyPhase, PhaseRequestPlay);
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
                Finish("FAILED: the R2 flight test exceeded its time budget in phase " + phase + ". Recorded as a failure, never as a pass by timeout.", 0, 1);
                return;
            }

            if (phase == PhaseRequestPlay)
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                    EditorApplication.isPlaying = true;
                return;
            }

            if (phase != PhaseInPlay || !EditorApplication.isPlaying || !MavF15PilotPhysicsR2FlightValidationRunner.Finished)
                return;

            // Harvest BEFORE leaving Play Mode: exiting reloads the domain and clears the statics.
            SessionState.SetString(KeyReport, MavF15PilotPhysicsR2FlightValidationRunner.Report);
            SessionState.SetInt(KeyPassed, MavF15PilotPhysicsR2FlightValidationRunner.Passed);
            SessionState.SetInt(KeyFailed, MavF15PilotPhysicsR2FlightValidationRunner.Failed);
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
                    GameObject host = new GameObject("f15r2-runner");
                    host.AddComponent<MavF15PilotPhysicsR2FlightValidationRunner>();
                }
                catch (Exception e)
                {
                    SessionState.SetString(KeyReport, "FAILED: starting the R2 flight test threw " + e.GetType().Name + ": " + e.Message);
                    SessionState.SetInt(KeyFailed, 1);
                    MavF15PilotPhysicsR2FlightValidationRunner.Finished = true;
                }

                return;
            }

            if (change == PlayModeStateChange.EnteredEditMode && phase == PhaseLeftPlay)
            {
                SessionState.SetInt(KeyPhase, PhaseIdle);
                Finish(SessionState.GetString(KeyReport, "(no report)"), SessionState.GetInt(KeyPassed, 0), SessionState.GetInt(KeyFailed, 0));
            }
        }

        private static void Finish(string report, int passed, int failed)
        {
            string text = report;
            int existing = text.LastIndexOf("RESULT:", StringComparison.Ordinal);
            if (existing >= 0)
                text = text.Substring(0, existing);
            text += Environment.NewLine + "RESULT: " + (failed == 0 && passed > 0 ? "PASS" : "FAIL") + " passed=" + passed + " failed=" + failed;

            if (failed == 0 && passed > 0)
                Debug.Log(text);
            else
                Debug.LogError(text);

            string outPath = SessionState.GetString(KeyOut, string.Empty);
            if (!string.IsNullOrEmpty(outPath))
            {
                try
                {
                    File.WriteAllText(outPath, text);
                }
                catch (Exception e)
                {
                    Debug.LogError("could not write " + outPath + ": " + e.Message);
                }
            }

            if (SessionState.GetBool(KeyBatch, false))
                EditorApplication.Exit(failed == 0 && passed > 0 ? 0 : 1);
        }
    }
}
#endif
