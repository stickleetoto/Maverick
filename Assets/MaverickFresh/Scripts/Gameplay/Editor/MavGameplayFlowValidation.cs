#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using MaverickFresh.Gameplay.Validation;

namespace MaverickFresh.Gameplay.EditorTools
{
    /// <summary>
    /// Runs <see cref="MavGameplayFlowValidationRunner"/> in Play Mode. The runner loads the real game scenes itself
    /// (Mav_MainLobby, Mav_Hangar, Mav_InGame, all in the build settings); it never saves them.
    ///
    /// Same shape as the flight-test drivers: progress lives in SessionState across the domain reload into Play Mode.
    ///
    /// Headless (do NOT pass -quit; the editor exits by itself):
    ///   -executeMethod MaverickFresh.Gameplay.EditorTools.MavGameplayFlowValidation.RunBatch -gameplayOut &lt;report path&gt;
    /// </summary>
    [InitializeOnLoad]
    public static class MavGameplayFlowValidation
    {
        private const string KeyPhase = "Mav.GameplayR1.Phase";
        private const string KeyBatch = "Mav.GameplayR1.Batch";
        private const string KeyOut = "Mav.GameplayR1.Out";
        private const string KeyReport = "Mav.GameplayR1.Report";
        private const string KeyDeadline = "Mav.GameplayR1.Deadline";
        private const string KeyPassed = "Mav.GameplayR1.Passed";
        private const string KeyFailed = "Mav.GameplayR1.Failed";

        private const int PhaseIdle = 0;
        private const int PhaseRequestPlay = 1;
        private const int PhaseInPlay = 2;
        private const int PhaseLeftPlay = 3;

        static MavGameplayFlowValidation()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        [MenuItem("Maverick/Gameplay/Run Game-Flow Validation (Play Mode)")]
        public static void RunFromMenu()
        {
            SessionState.SetBool(KeyBatch, false);
            SessionState.SetString(KeyOut, string.Empty);
            Begin();
        }

        public static void RunBatch()
        {
            SessionState.SetBool(KeyBatch, true);
            SessionState.SetString(KeyOut, CommandLineValue("-gameplayOut") ?? string.Empty);
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
            SessionState.SetFloat(KeyDeadline, (float)EditorApplication.timeSinceStartup + 600f);

            if (SceneManager.GetActiveScene().isDirty)
            {
                Finish("ABORTED: the open scene has unsaved changes; the game-flow test loads other scenes and would discard them.", 0, 1);
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
                Finish("FAILED: the game-flow test exceeded its time budget in phase " + phase + ". Recorded as a failure, never as a pass.", 0, 1);
                return;
            }

            if (phase == PhaseRequestPlay)
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                    EditorApplication.isPlaying = true;
                return;
            }

            if (phase != PhaseInPlay || !EditorApplication.isPlaying || !MavGameplayFlowValidationRunner.Finished)
                return;

            // Harvest BEFORE leaving Play Mode: exiting reloads the domain and clears the statics.
            SessionState.SetString(KeyReport, MavGameplayFlowValidationRunner.Report);
            SessionState.SetInt(KeyPassed, MavGameplayFlowValidationRunner.Passed);
            SessionState.SetInt(KeyFailed, MavGameplayFlowValidationRunner.Failed);
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
                    GameObject host = new GameObject("gameplay-r1-runner");
                    host.AddComponent<MavGameplayFlowValidationRunner>();
                }
                catch (Exception e)
                {
                    SessionState.SetString(KeyReport, "FAILED: starting the game-flow test threw " + e.GetType().Name + ": " + e.Message);
                    SessionState.SetInt(KeyFailed, 1);
                    MavGameplayFlowValidationRunner.Finished = true;
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
