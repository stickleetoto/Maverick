using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using MaverickFresh.FlightDynamics.Validation;

namespace MaverickFresh.FlightDynamics.EditorTools
{
    /// <summary>
    /// F-15 research runtime closeout driver: runs <see cref="MavF15ResearchRuntimeFlightValidationRunner"/>
    /// inside real Unity physics, in Play Mode.
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
    ///   -executeMethod MaverickFresh.FlightDynamics.EditorTools.MavF15ResearchRuntimeFlightValidation.RunBatch
    ///   -f15rtOut &lt;report path&gt;
    /// </summary>
    [InitializeOnLoad]
    public static class MavF15ResearchRuntimeFlightValidation
    {
        private const string KeyPhase = "Mav.F15RT.Phase";
        private const string KeyBatch = "Mav.F15RT.Batch";
        private const string KeyOut = "Mav.F15RT.Out";
        private const string KeyReport = "Mav.F15RT.Report";
        private const string KeyDeadline = "Mav.F15RT.Deadline";
        private const string KeyPassed = "Mav.F15RT.Passed";
        private const string KeyFailed = "Mav.F15RT.Failed";

        private const int PhaseIdle = 0;
        private const int PhaseRequestPlay = 1;
        private const int PhaseInPlay = 2;
        private const int PhaseLeftPlay = 3;

        static MavF15ResearchRuntimeFlightValidation()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        [MenuItem("Maverick/Flight Dynamics/Run F-15 Research Runtime Closeout (Play Mode)")]
        public static void RunFromMenu()
        {
            SessionState.SetBool(KeyBatch, false);
            SessionState.SetString(KeyOut, string.Empty);
            Begin();
        }

        public static void RunBatch()
        {
            SessionState.SetBool(KeyBatch, true);
            SessionState.SetString(KeyOut, CommandLineValue("-f15rtOut") ?? string.Empty);
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
                       + "\" open. The research closeout runs on an empty scene so no production gameplay starts.", 0, 1);
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

                Finish("FAILED: the research closeout machine exceeded its time budget in phase " + phase
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

            if (!EditorApplication.isPlaying || !MavF15ResearchRuntimeFlightValidationRunner.Finished)
                return;

            // Harvest BEFORE leaving Play Mode: exiting reloads the domain and clears the statics.
            SessionState.SetString(KeyReport, MavF15ResearchRuntimeFlightValidationRunner.Report);
            SessionState.SetInt(KeyPassed, MavF15ResearchRuntimeFlightValidationRunner.Passed);
            SessionState.SetInt(KeyFailed, MavF15ResearchRuntimeFlightValidationRunner.Failed);
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
                    GameObject host = new GameObject("f15rt-runner");
                    host.AddComponent<MavF15ResearchRuntimeFlightValidationRunner>();
                }
                catch (Exception e)
                {
                    SessionState.SetString(KeyReport, "FAILED: starting the research closeout threw "
                        + e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace);
                    SessionState.SetInt(KeyFailed, 1);
                    MavF15ResearchRuntimeFlightValidationRunner.Finished = true;
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
