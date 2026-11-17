using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using MaverickFresh.FlightDynamics.Validation;

namespace MaverickFresh.FlightDynamics.EditorTools
{
    /// <summary>
    /// REALISTIC MOUSE INSTRUCTOR V2 flight-test driver: runs <see cref="MavMouseAimInstructorV2FlightValidationRunner"/> inside
    /// real Unity physics, in Play Mode, and the headless suite <see cref="MavMouseAimInstructorV2Validation"/> from the menu.
    /// The same shape as the pilot-control V2 driver (its own session keys; that driver is untouched): progress lives in
    /// SessionState because entering Play Mode reloads the domain.
    ///
    /// ISOLATION. Play Mode is entered on the empty unsaved scene batch mode starts with; the driver refuses to run with a
    /// saved scene open. Every rig is built in memory and destroyed. No scene, prefab, material or model is touched.
    ///
    /// Headless (do NOT pass -quit; the driver exits by itself):
    ///   -executeMethod MaverickFresh.FlightDynamics.EditorTools.MavMouseAimInstructorV2FlightValidation.RunBatch -mi2Out &lt;report path&gt;
    /// </summary>
    [InitializeOnLoad]
    public static class MavMouseAimInstructorV2FlightValidation
    {
        private const string KeyPhase = "Mav.MI2.Phase";
        private const string KeyBatch = "Mav.MI2.Batch";
        private const string KeyOut = "Mav.MI2.Out";
        private const string KeyReport = "Mav.MI2.Report";
        private const string KeyDeadline = "Mav.MI2.Deadline";
        private const string KeyPassed = "Mav.MI2.Passed";
        private const string KeyFailed = "Mav.MI2.Failed";

        private const int PhaseIdle = 0;
        private const int PhaseRequestPlay = 1;
        private const int PhaseInPlay = 2;
        private const int PhaseLeftPlay = 3;

        static MavMouseAimInstructorV2FlightValidation()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        [MenuItem("Maverick/F-15/Mouse Instructor V2 (EXPERIMENTAL)/Run Headless Validation")]
        public static void RunHeadlessFromMenu()
        {
            int passed, failed;
            string report = MavMouseAimInstructorV2Validation.RunAll(out passed, out failed);
            Debug.Log(report);
            EditorUtility.DisplayDialog("Mouse Instructor V2 - headless validation", "passed " + passed + ", failed " + failed + "\n(full report in the Console)", "OK");
        }

        [MenuItem("Maverick/F-15/Mouse Instructor V2 (EXPERIMENTAL)/Run Flight Test (Play Mode, empty scene)")]
        public static void RunFromMenu()
        {
            SessionState.SetBool(KeyBatch, false);
            SessionState.SetString(KeyOut, string.Empty);
            Begin();
        }

        public static void RunBatch()
        {
            SessionState.SetBool(KeyBatch, true);
            SessionState.SetString(KeyOut, CommandLineValue("-mi2Out") ?? string.Empty);
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
                       + "\" open. The mouse instructor flight test runs on an empty scene so no production gameplay starts.", 0, 1);
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
                Finish("FAILED: the mouse instructor flight test exceeded its time budget in phase " + phase
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

            if (!EditorApplication.isPlaying || !MavMouseAimInstructorV2FlightValidationRunner.Finished)
                return;

            // Harvest BEFORE leaving Play Mode: exiting reloads the domain and clears the statics.
            SessionState.SetString(KeyReport, MavMouseAimInstructorV2FlightValidationRunner.Report);
            SessionState.SetInt(KeyPassed, MavMouseAimInstructorV2FlightValidationRunner.Passed);
            SessionState.SetInt(KeyFailed, MavMouseAimInstructorV2FlightValidationRunner.Failed);
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
                    GameObject host = new GameObject("mi2-runner");
                    host.AddComponent<MavMouseAimInstructorV2FlightValidationRunner>();
                }
                catch (Exception e)
                {
                    SessionState.SetString(KeyReport, "FAILED: starting the mouse instructor flight test threw "
                        + e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace);
                    SessionState.SetInt(KeyFailed, 1);
                    MavMouseAimInstructorV2FlightValidationRunner.Finished = true;
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
