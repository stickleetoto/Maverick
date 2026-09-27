using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using MaverickFresh.FlightDynamics.Validation;

namespace MaverickFresh.FlightDynamics.EditorTools
{
    /// <summary>
    /// F-15 PILOT CONTROL V2 flight-test driver: runs <see cref="MavF15PilotControlledV2FlightValidationRunner"/>
    /// (V2 flight test + V1 vs V2 comparison) inside real Unity physics, in Play Mode, and hands it the human V2
    /// prefab (<see cref="MavF15PilotControlledPrefabBuilder.PrefabPathV2"/>) to fly as well - instantiated in
    /// memory, never modified. A copy of the V1 driver's machinery with its own session keys, so the V1 test is
    /// untouched.
    ///
    /// Same shape as the Phase 5C-R F-16 driver: progress lives in SessionState because entering Play Mode
    /// reloads the managed domain, and an [InitializeOnLoad] static constructor re-hooks the callbacks.
    ///
    /// ISOLATION. Play Mode is entered on the empty unsaved scene batch mode starts with; the driver refuses
    /// to run with a saved scene open, so no production scene, gameplay or aircraft starts. Every rig is built
    /// in memory and destroyed. No scene, prefab, material or model is touched; gameplay F15Replacement does
    /// not exist and is not created.
    ///
    /// Headless (do NOT pass -quit; the machine exits by itself):
    ///   -executeMethod MaverickFresh.FlightDynamics.EditorTools.MavF15PilotControlledV2FlightValidation.RunBatch
    ///   -f15pc2Out &lt;report path&gt;
    /// </summary>
    [InitializeOnLoad]
    public static class MavF15PilotControlledV2FlightValidation
    {
        private const string KeyPhase = "Mav.F15PC2.Phase";
        private const string KeyBatch = "Mav.F15PC2.Batch";
        private const string KeyOut = "Mav.F15PC2.Out";
        private const string KeyReport = "Mav.F15PC2.Report";
        private const string KeyDeadline = "Mav.F15PC2.Deadline";
        private const string KeyPassed = "Mav.F15PC2.Passed";
        private const string KeyFailed = "Mav.F15PC2.Failed";

        private const int PhaseIdle = 0;
        private const int PhaseRequestPlay = 1;
        private const int PhaseInPlay = 2;
        private const int PhaseLeftPlay = 3;

        static MavF15PilotControlledV2FlightValidation()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        [MenuItem("Maverick/F-15/Run Pilot-Control V2 Flight Test + V1 Comparison (Play Mode)")]
        public static void RunFromMenu()
        {
            SessionState.SetBool(KeyBatch, false);
            SessionState.SetString(KeyOut, string.Empty);
            Begin();
        }

        public static void RunBatch()
        {
            SessionState.SetBool(KeyBatch, true);
            SessionState.SetString(KeyOut, CommandLineValue("-f15pc2Out") ?? string.Empty);
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
                       + "\" open. The pilot-controlled flight test runs on an empty scene so no production gameplay starts.", 0, 1);
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

                Finish("FAILED: the pilot-control V2 flight test exceeded its time budget in phase " + phase
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

            if (!EditorApplication.isPlaying || !MavF15PilotControlledV2FlightValidationRunner.Finished)
                return;

            // Harvest BEFORE leaving Play Mode: exiting reloads the domain and clears the statics.
            SessionState.SetString(KeyReport, MavF15PilotControlledV2FlightValidationRunner.Report);
            SessionState.SetInt(KeyPassed, MavF15PilotControlledV2FlightValidationRunner.Passed);
            SessionState.SetInt(KeyFailed, MavF15PilotControlledV2FlightValidationRunner.Failed);
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
                    // Static fields do not survive the domain reload into Play Mode: the prefab is handed over here.
                    MavF15PilotControlledV2FlightValidationRunner.PrefabToTest =
                        AssetDatabase.LoadAssetAtPath<GameObject>(MavF15PilotControlledPrefabBuilder.PrefabPathV2);
                    MavF15PilotControlledV2FlightValidationRunner.PrefabPath =
                        MavF15PilotControlledV2FlightValidationRunner.PrefabToTest != null
                            ? MavF15PilotControlledPrefabBuilder.PrefabPathV2
                            : "(V2 prefab not found; a V2 keyboard rig is flown instead)";
                    GameObject host = new GameObject("f15pc2-runner");
                    host.AddComponent<MavF15PilotControlledV2FlightValidationRunner>();
                }
                catch (Exception e)
                {
                    SessionState.SetString(KeyReport, "FAILED: starting the pilot-control V2 flight test threw "
                        + e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace);
                    SessionState.SetInt(KeyFailed, 1);
                    MavF15PilotControlledV2FlightValidationRunner.Finished = true;
                }

                return;
            }

            if (change == PlayModeStateChange.EnteredEditMode && phase == PhaseLeftPlay)
            {
                SessionState.SetInt(KeyPhase, PhaseIdle);
                Finish(SessionState.GetString(KeyReport, "(no report)"),
                    SessionState.GetInt(KeyPassed, 0), SessionState.GetInt(KeyFailed, 0));
            }
        }

        private static void Finish(string report, int passed, int failed)
        {
            string text = report;
            int existing = text.LastIndexOf("RESULT:", StringComparison.Ordinal);
            if (existing >= 0)
                text = text.Substring(0, existing);
            text += Environment.NewLine + "RESULT: " + (failed == 0 && passed > 0 ? "PASS" : "FAIL")
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
                EditorApplication.Exit(failed == 0 && passed > 0 ? 0 : 1);
        }
    }
}
