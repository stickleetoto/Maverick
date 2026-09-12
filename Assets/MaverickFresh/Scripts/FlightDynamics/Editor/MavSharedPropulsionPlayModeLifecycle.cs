using System;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using MaverickFresh.FlightDynamics;
using MaverickFresh.FlightDynamics.F16;
using MaverickFresh.FlightDynamics.Validation;

namespace MaverickFresh.FlightDynamics.EditorTools
{
    /// <summary>
    /// Phase P0.2b: the shared-propulsion checks that require ACTUAL Play Mode, across an
    /// enter / exit / re-enter cycle, under both domain-reload settings.
    ///
    /// WHY THIS EXISTS SEPARATELY FROM MavSharedPropulsionUnityValidation.
    ///
    /// The P0.2 suite runs synchronously inside one edit-mode call. That is enough for everything
    /// that is a function of the loaded assemblies, but it cannot answer three questions:
    ///
    ///   1. Does the engine-law registry come up correctly in Play Mode, and does it survive an
    ///      exit and a re-entry without accumulating duplicate registrations?
    ///   2. Does per-engine command authority hold on EVERY frame of a real Play Mode session,
    ///      including the frame on which the command source disappears?
    ///   3. What does the propulsion hot path allocate inside a real player loop, measured with
    ///      Unity's own GC.Alloc counter rather than an editor-wide heap total?
    ///
    /// None of those can be answered synchronously, because entering Play Mode may reload the
    /// managed domain and destroy the call stack asking the question. So this is a STATE MACHINE
    /// driven by EditorApplication callbacks, with its progress parked in SessionState - the only
    /// store that survives a domain reload within one editor session.
    ///
    /// ISOLATION. This enters Play Mode on whatever empty, unsaved scene the editor already has. It
    /// refuses to run if a saved scene is open, so it can never start a production scene's gameplay.
    /// It does not enable F16Replacement, does not touch a production prefab, and creates every
    /// object it needs in memory, labelled SYNTHETIC_VALIDATION_ONLY. Entering Play Mode on an empty
    /// scene is not the same thing as enabling the gameplay FDM, and nothing here does the latter.
    ///
    /// PROJECT SETTINGS. Enter Play Mode Options are read, driven through both states, and restored
    /// to the values found on entry. The restore runs on the normal path and on the watchdog path.
    ///
    /// Headless:
    ///   -executeMethod MaverickFresh.FlightDynamics.EditorTools.MavSharedPropulsionPlayModeLifecycle.RunBatch
    /// Do NOT pass -quit: this machine needs the editor to keep ticking, and exits by itself.
    /// </summary>
    [InitializeOnLoad]
    public static class MavSharedPropulsionPlayModeLifecycle
    {
        private const string SyntheticTag = "SYNTHETIC_VALIDATION_ONLY";
        private const float SynthLateralOffsetM = 3f;
        private const float SynthThrustN = 50000f;
        private const int SpoolSteps = 900;

        private const string KeyPhase = "Mav.P02b.Phase";
        private const string KeyMode = "Mav.P02b.Mode";
        private const string KeyReport = "Mav.P02b.Report";
        private const string KeyPassed = "Mav.P02b.Passed";
        private const string KeyFailed = "Mav.P02b.Failed";
        private const string KeyBatch = "Mav.P02b.Batch";
        private const string KeyOrigEnabled = "Mav.P02b.OrigEnabled";
        private const string KeyOrigOptions = "Mav.P02b.OrigOptions";
        private const string KeySettingsSaved = "Mav.P02b.SettingsSaved";
        private const string KeyFingerprint = "Mav.P02b.Fingerprint";
        private const string KeyPlayFingerprint = "Mav.P02b.PlayFingerprint";
        private const string KeyCountBeforeClear = "Mav.P02b.CountBeforeClear";
        private const string KeySession1Left = "Mav.P02b.S1Left";
        private const string KeySession1Right = "Mav.P02b.S1Right";
        private const string KeyDeadline = "Mav.P02b.Deadline";
        private const string KeyOutPath = "Mav.P02b.OutPath";

        private const int PhaseIdle = 0;
        private const int PhaseRequestPlay1 = 1;
        private const int PhaseInPlay1 = 2;
        private const int PhaseLeftPlay1 = 3;
        private const int PhaseRequestPlay2 = 4;
        private const int PhaseInPlay2 = 5;
        private const int PhaseLeftPlay2 = 6;

        // Two runs: index 0 with domain reload ON, index 1 with it OFF.
        private const int ModeCount = 2;

        /// <summary>
        /// Regenerated every time this assembly's domain is loaded. Comparing it across a Play Mode
        /// transition is how this suite OBSERVES whether a domain reload actually happened, rather
        /// than trusting the project setting to mean what it says.
        /// </summary>
        private static readonly string DomainFingerprint = Guid.NewGuid().ToString("N");

        /// <summary>
        /// Deliberate static state, set in Play Mode session 1 and read after it.
        ///
        /// This is the POSITIVE CONTROL for the state-leak checks. The production propulsion code
        /// holds no static engine state, so a leak test run against it would pass whether or not the
        /// test was capable of detecting leakage. This field CAN leak, so its behaviour reports what
        /// the experiment can actually see: cleared under domain reload, carried over without it. A
        /// leak check that survives this control is measuring something.
        /// </summary>
        private static float leakCanaryPowerPercent = -1f;

        private static StringBuilder sb;
        private static int passed;
        private static int failed;

        static MavSharedPropulsionPlayModeLifecycle()
        {
            // Re-hooked on every domain load, which is exactly why the machine survives one.
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        // ============================================================== entry points

        [MenuItem("Maverick/Flight Dynamics/Run P0.2b Play Mode Lifecycle Validation")]
        public static void RunFromMenu()
        {
            SessionState.SetBool(KeyBatch, false);
            SessionState.SetString(KeyOutPath, string.Empty);
            Start();
        }

        /// <summary>Headless entry. Exits the editor itself when the machine finishes.</summary>
        public static void RunBatch()
        {
            SessionState.SetBool(KeyBatch, true);
            string path = CommandLineValue("-p02bOut");
            SessionState.SetString(KeyOutPath, path ?? string.Empty);
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
            SessionState.SetBool(KeySettingsSaved, false);
            SessionState.SetInt(KeyMode, 0);
            SessionState.SetFloat(KeyDeadline, (float)EditorApplication.timeSinceStartup + 1200f);

            Open();
            sb.AppendLine("Maverick Shared Propulsion - P0.2b PLAY MODE LIFECYCLE");
            sb.AppendLine("======================================================");
            sb.AppendLine("Unity " + Application.unityVersion);
            sb.AppendLine("Every synthetic value below is " + SyntheticTag);

            // SAFETY GATE. Entering Play Mode is only harmless because the scene is empty. If a
            // saved scene were open, its Awake/Start would run real gameplay, so refuse instead.
            string scenePath = SceneManager.GetActiveScene().path;
            bool sceneIsUnsaved = string.IsNullOrEmpty(scenePath);
            Section("[L-000] isolation gate");
            Check(sceneIsUnsaved, "L-000",
                "active scene is unsaved/empty (path=\"" + scenePath + "\"), so entering Play Mode "
                + "starts no production gameplay and no production prefab awakens");

            if (!sceneIsUnsaved)
            {
                sb.AppendLine("ABORTED: refusing to enter Play Mode with a saved scene open.");
                Close();
                Finish();
                return;
            }

            SessionState.SetBool(KeyOrigEnabled, EditorSettings.enterPlayModeOptionsEnabled);
            SessionState.SetInt(KeyOrigOptions, (int)EditorSettings.enterPlayModeOptions);
            SessionState.SetBool(KeySettingsSaved, true);

            sb.AppendLine();
            sb.AppendLine("PROJECT SETTING AS FOUND (ProjectSettings/EditorSettings.asset):");
            sb.AppendLine("  m_EnterPlayModeOptionsEnabled : "
                          + EditorSettings.enterPlayModeOptionsEnabled);
            sb.AppendLine("  m_EnterPlayModeOptions        : "
                          + EditorSettings.enterPlayModeOptions
                          + "   (None => domain AND scene reload both still happen)");
            Close();

            BeginMode(0);
        }

        // ============================================================== mode driver

        private static void BeginMode(int mode)
        {
            SessionState.SetInt(KeyMode, mode);

            bool domainReload = mode == 0;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = domainReload
                ? EnterPlayModeOptions.None
                : EnterPlayModeOptions.DisableDomainReload;

            Open();
            sb.AppendLine();
            sb.AppendLine("################################################################");
            sb.AppendLine("# MODE " + mode + ": domain reload "
                          + (domainReload ? "ENABLED" : "DISABLED")
                          + "  (EnterPlayModeOptions=" + EditorSettings.enterPlayModeOptions + ")");
            sb.AppendLine("################################################################");

            EditModeChecksBeforePlay(mode);
            Close();

            SessionState.SetInt(KeyPhase, PhaseRequestPlay1);
        }

        private static void OnEditorUpdate()
        {
            int phase = SessionState.GetInt(KeyPhase, PhaseIdle);
            if (phase == PhaseIdle)
                return;

            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(KeyDeadline, 0f))
            {
                Open();
                Section("[L-WATCHDOG] machine did not complete");
                Check(false, "L-WATCHDOG",
                    "the lifecycle machine exceeded its time budget while in phase " + phase
                    + " - recorded as a FAILURE, never as a pass by timeout");
                Close();
                SessionState.SetInt(KeyPhase, PhaseIdle);
                RestoreSettings();
                Finish();
                return;
            }

            if (phase == PhaseRequestPlay1 || phase == PhaseRequestPlay2)
            {
                // Requested from an editor update rather than from executeMethod or from inside a
                // playModeStateChanged callback: Unity refuses the transition if asked from there.
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                    EditorApplication.isPlaying = true;

                return;
            }

            if (phase != PhaseInPlay1 && phase != PhaseInPlay2)
                return;

            if (!EditorApplication.isPlaying || !MavPropulsionPlayModeProbe.Finished)
                return;

            Open();
            HarvestProbe(phase == PhaseInPlay1 ? 1 : 2);
            Close();

            MavPropulsionPlayModeProbe.Clear();
            SessionState.SetInt(KeyPhase, phase == PhaseInPlay1 ? PhaseLeftPlay1 : PhaseLeftPlay2);
            EditorApplication.isPlaying = false;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            int phase = SessionState.GetInt(KeyPhase, PhaseIdle);
            if (phase == PhaseIdle)
                return;

            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                if (phase == PhaseRequestPlay1)
                {
                    SessionState.SetInt(KeyPhase, PhaseInPlay1);
                    RunPlaySession(1);
                }
                else if (phase == PhaseRequestPlay2)
                {
                    SessionState.SetInt(KeyPhase, PhaseInPlay2);
                    RunPlaySession(2);
                }

                return;
            }

            if (change != PlayModeStateChange.EnteredEditMode)
                return;

            if (phase == PhaseLeftPlay1)
            {
                Open();
                try
                {
                    EditModeChecksBetweenSessions();
                }
                catch (Exception e)
                {
                    Check(false, "L-150-CRASH", "the between-sessions checks threw: " + Describe(e));
                }

                Close();
                SessionState.SetInt(KeyPhase, PhaseRequestPlay2);
                return;
            }

            if (phase != PhaseLeftPlay2)
                return;

            Open();
            try
            {
                EditModeChecksAfterSecondSession();
            }
            catch (Exception e)
            {
                Check(false, "L-160-CRASH", "the post-cycle checks threw: " + Describe(e));
            }

            Close();

            int mode = SessionState.GetInt(KeyMode, 0);
            if (mode + 1 < ModeCount)
            {
                BeginMode(mode + 1);
                return;
            }

            SessionState.SetInt(KeyPhase, PhaseIdle);
            RestoreSettings();
            Finish();
        }

        /// <summary>
        /// Runs one Play Mode session's checks with its exceptions CONTAINED.
        ///
        /// An exception thrown inside a playModeStateChanged callback would otherwise abandon the
        /// machine mid-session, leaving the editor-side loop waiting on the probe forever and the
        /// run ending at the watchdog with no diagnosis. A crash is recorded as a FAILURE and the
        /// machine is unblocked so it can finish and report - never swallowed, and never allowed to
        /// look like "no failures found".
        /// </summary>
        private static void RunPlaySession(int session)
        {
            Open();
            try
            {
                PlaySessionChecks(session);
            }
            catch (Exception e)
            {
                Check(false, "L-1" + session + "-CRASH",
                    "the Play Mode session " + session + " checks threw, which is a failure of this "
                    + "phase and not an absence of findings: " + Describe(e));
                MavPropulsionPlayModeProbe.ForceFinish();
            }

            Close();
        }

        private static string Describe(Exception e)
        {
            return e.GetType().Name + ": " + e.Message;
        }

        private static void RestoreSettings()
        {
            if (!SessionState.GetBool(KeySettingsSaved, false))
                return;

            bool wantEnabled = SessionState.GetBool(KeyOrigEnabled, true);
            int wantOptions = SessionState.GetInt(KeyOrigOptions, 0);

            EditorSettings.enterPlayModeOptionsEnabled = wantEnabled;
            EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)wantOptions;

            Open();
            Section("[L-099] project settings restored");
            Check(EditorSettings.enterPlayModeOptionsEnabled == wantEnabled, "L-099",
                "m_EnterPlayModeOptionsEnabled restored to "
                + EditorSettings.enterPlayModeOptionsEnabled);
            Check((int)EditorSettings.enterPlayModeOptions == wantOptions, "L-099b",
                "m_EnterPlayModeOptions restored to " + EditorSettings.enterPlayModeOptions
                + " - the project setting is left exactly as it was found");
            Close();
        }

        private static void Finish()
        {
            string text = SessionState.GetString(KeyReport, string.Empty);
            int p = SessionState.GetInt(KeyPassed, 0);
            int f = SessionState.GetInt(KeyFailed, 0);
            text += Environment.NewLine + Environment.NewLine
                    + "RESULT: " + (f == 0 ? "PASS" : "FAIL")
                    + " passed=" + p + " failed=" + f;

            Debug.Log(text);
            Console.WriteLine(text);

            string outPath = SessionState.GetString(KeyOutPath, string.Empty);
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
                EditorApplication.Exit(f == 0 ? 0 : 1);
        }

        // ============================================================== edit mode, before play

        private static void EditModeChecksBeforePlay(int mode)
        {
            Section("[L-001] fresh edit-mode state before entering Play Mode");

            bool registeredInEditor = MavF16EngineLawRegistrar.IsRegistered;
            int countInEditor = MavEnginePowerDynamicsFactory.F16RegistrationCount;

            Check(registeredInEditor, "L-001",
                "in edit mode the F-16 law is already registered, with no Play Mode required - "
                + "route 2, the editor-assembly InitializeOnLoadMethod, works on its own");
            Check(countInEditor >= 1, "L-001b",
                "registration count in this domain is " + countInEditor);

            // ISOLATING EXPERIMENT. Clear the registration immediately before the transition.
            //
            // With domain reload DISABLED the editor hook does not run again on entering Play Mode,
            // so anything that re-registers has to be the runtime route. That makes mode 1 a direct
            // test of [RuntimeInitializeOnLoadMethod], rather than an inference from its presence in
            // compiled metadata, which is all P0.2 could offer.
            SessionState.SetInt(KeyCountBeforeClear, countInEditor);
            MavEnginePowerDynamicsFactory.RegisterF16GarzaMorelli(null);
            Check(!MavF16EngineLawRegistrar.IsRegistered, "L-001c",
                "registration deliberately cleared before the transition, so a registered state "
                + "seen in Play Mode must have been established by a Play Mode hook"
                + (mode == 1
                    ? " - and with domain reload DISABLED the editor hook cannot be that hook"
                    : " - under domain reload either the editor or the runtime hook may supply it"));

            SessionState.SetString(KeyFingerprint, DomainFingerprint);

            leakCanaryPowerPercent = -1f;
        }

        // ============================================================== play mode

        private static void PlaySessionChecks(int session)
        {
            string s = session.ToString();
            Section("[L-1" + s + "0] PLAY MODE session " + s + ": registry state on entry");

            bool reloaded = DomainFingerprint != SessionState.GetString(KeyFingerprint, string.Empty);
            bool expectReload = SessionState.GetInt(KeyMode, 0) == 0;

            sb.Append("        per-domain fingerprint ")
              .Append(reloaded ? "CHANGED" : "unchanged")
              .Append(" across the transition; the project setting asks for a reload: ")
              .Append(expectReload).AppendLine();

            Check(reloaded == expectReload, "L-1" + s + "0a",
                "the domain reload OBSERVED (" + reloaded + ") matches the one the project setting "
                + "asks for (" + expectReload + ") - measured with a per-domain fingerprint rather "
                + "than assumed from the setting");

            // THE registration check. Nothing has built an engine profile yet in this domain, so
            // route 3 (EnsureRegistered from profile construction) cannot be responsible: what is
            // observed here is a lifecycle hook or nothing.
            bool registered = MavF16EngineLawRegistrar.IsRegistered;
            int count = MavEnginePowerDynamicsFactory.F16RegistrationCount;

            Check(registered, "L-1" + s + "0b",
                "the F-16 Garza/Morelli law is available in Play Mode after being deliberately "
                + "cleared in edit mode"
                + (expectReload
                    ? ""
                    : ", and WITHOUT a domain reload - which only [RuntimeInitializeOnLoadMethod] "
                      + "can have done, so the player-build route is directly demonstrated"));
            // Bounded RELATIVE to the pre-clear count. This suite deliberately clears the
            // registration (one change) and a lifecycle hook re-establishes it (a second), so at most
            // two increments are legitimate per session. An absolute bound would only hold under
            // domain reload, where the counter is reset - and mode 1 is precisely the case where it
            // is not.
            int beforeClear = SessionState.GetInt(KeyCountBeforeClear, 0);
            Check(count >= 1 && count <= beforeClear + 2, "L-1" + s + "0c",
                "registration count is " + count + ", at most two more than the " + beforeClear
                + " before this suite's deliberate clear: one for the clear, one for the "
                + "re-registration. Not one per overlapping hook");

            IMavEnginePowerDynamics resolved = MavEnginePowerDynamicsFactory.Resolve(
                MavEnginePowerDynamicsLaw.F16GarzaMorelliPowerState);
            Check(ReferenceEquals(resolved, MavF16GarzaMorelliEngineDynamics.Instance),
                "L-1" + s + "0d",
                "and what resolves IS the sourced Garza/Morelli singleton, not a substitute: "
                + (resolved != null ? resolved.DynamicsName : "null"));

            // Idempotency under a real lifecycle: ask again, as a second overlapping hook would.
            int before = MavEnginePowerDynamicsFactory.F16RegistrationCount;
            MavF16EngineLawRegistrar.EnsureRegistered();
            MavF16EngineLawRegistrar.RegisterOnGameStart();
            Check(MavEnginePowerDynamicsFactory.F16RegistrationCount == before, "L-1" + s + "0e",
                "calling both registration entry points again changes nothing (count stays "
                + before + ") - duplicate-registration corruption is not reachable");

            // ---------------------------------------------------------- command authority

            Section("[L-1" + s + "1] PLAY MODE session " + s + ": per-engine authority, every frame");

            MavEngineProfile shared = MakeProfile(
                "l1" + s + "1-shared-" + SyntheticTag, null,
                MavEnginePowerDynamicsLaw.F16GarzaMorelliPowerState);

            GameObject go = new GameObject("p02b-authority-" + SyntheticTag);
            MavPropulsionSystem sys = go.AddComponent<MavPropulsionSystem>();
            sys.installation = MakeTwin(shared);
            sys.autoResolveCommandSource = false;
            string err;
            bool builtOk = sys.Build(true, out err);
            Check(builtOk, "L-1" + s + "1a",
                "a synthetic twin installation builds in Play Mode: " + (builtOk ? "built" : err));

            MavScriptedPropulsionCommandSource src =
                go.AddComponent<MavScriptedPropulsionCommandSource>();
            src.Resize(2);
            sys.commandSource = src;
            src.SetEngineThrottle(0, 1.00f);
            src.SetEngineThrottle(1, 0.25f);
            sys.ResetEngineState(0f);

            int blended = 0;
            int perEngineFrames = 0;
            int linkedFrames = 0;
            for (int i = 0; i < SpoolSteps; i++)
            {
                sys.Evaluate(LevelState(), Atmosphere(), 0.5f, 0.02f);
                if (!AuthorityIsUnambiguous(sys, ref perEngineFrames, ref linkedFrames))
                    blended++;
            }

            float left = sys.GetRuntime(0).ActualPowerPercent;
            float right = sys.GetRuntime(1).ActualPowerPercent;

            Check(Mathf.Abs(left - 100f) < 0.05f, "L-1" + s + "1b",
                "left engine follows its own command in Play Mode: 217.38*1.00 - 117.38 = 100.000, "
                + "got " + F(left));
            Check(Mathf.Abs(right - 16.235f) < 0.05f, "L-1" + s + "1c",
                "right engine follows its own: 64.94*0.25 = 16.235, got " + F(right));
            Check(Mathf.Abs(left - 32.47f) > 1f && Mathf.Abs(right - 32.47f) > 1f, "L-1" + s + "1d",
                "and neither landed on 32.47, which the 0.5 pipeline scalar would have produced - "
                + "so the scalar is not what reached the engines");
            Check(sys.debugCommandAuthority == MavPropulsionSystem.AuthorityPerEngineSource,
                "L-1" + s + "1e",
                "authority reads " + sys.debugCommandAuthority + " with "
                + sys.debugAddressedEngineCount + "/" + sys.Command.EngineCount
                + " engines addressed");
            Check(blended == 0, "L-1" + s + "1f",
                "across " + SpoolSteps + " Play Mode steps there was NO frame with a blended "
                + "authority: " + perEngineFrames + " fully per-engine, " + linkedFrames
                + " fully linked, " + blended + " ambiguous");

            // ---------------------------------------------------------- source disappears

            Section("[L-1" + s + "2] PLAY MODE session " + s + ": command source destroyed mid-session");

            UnityEngine.Object.DestroyImmediate(src);
            Check(sys.commandSource == null, "L-1" + s + "2a",
                "the destroyed source compares == null through Unity Object semantics, so the "
                + "propulsion system can tell that it is gone");

            int blendedAfter = 0;
            int perEngineAfter = 0;
            int linkedAfter = 0;
            for (int i = 0; i < SpoolSteps; i++)
            {
                sys.Evaluate(LevelState(), Atmosphere(), 0.78262f, 0.02f);
                if (!AuthorityIsUnambiguous(sys, ref perEngineAfter, ref linkedAfter))
                    blendedAfter++;
            }

            float lf = sys.GetRuntime(0).ActualPowerPercent;
            float rf = sys.GetRuntime(1).ActualPowerPercent;
            Check(sys.debugCommandAuthority == MavPropulsionSystem.AuthorityLinkedScalar,
                "L-1" + s + "2b",
                "authority falls back to " + sys.debugCommandAuthority
                + " with the source gone - no exception, and no stale asymmetric command");
            Check(Mathf.Abs(lf - rf) < 1e-3f, "L-1" + s + "2c",
                "and both engines converge on the same state (" + F(lf) + " vs " + F(rf)
                + "), so the previous asymmetry did not persist past the source");
            Check(blendedAfter == 0 && perEngineAfter == 0, "L-1" + s + "2d",
                "every one of those frames was unambiguously linked (" + linkedAfter + " linked, "
                + perEngineAfter + " per-engine, " + blendedAfter + " ambiguous) - including the "
                + "frame on which the source vanished");

            // ---------------------------------------------------------- partial coverage

            Section("[L-1" + s + "3] PLAY MODE session " + s + ": partial coverage refused");

            MavScriptedPropulsionCommandSource partial =
                go.AddComponent<MavScriptedPropulsionCommandSource>();
            partial.Resize(2);
            partial.SetEngineThrottle(0, 1.0f);
            partial.SetEngineThrottle(1, 0.25f);
            partial.SetPartialCoverage(1);
            sys.commandSource = partial;
            sys.ResetEngineState(0.5f);

            for (int i = 0; i < 300; i++)
                sys.Evaluate(LevelState(), Atmosphere(), 0.5f, 0.02f);

            Check(sys.debugCommandAuthority == MavPropulsionSystem.AuthorityLinkedScalar,
                "L-1" + s + "3a",
                "a source addressing 1 of 2 engines does not receive per-engine authority: "
                + sys.debugCommandAuthority);
            Check(sys.debugPartialCommandRefused, "L-1" + s + "3b",
                "and the refusal is REPORTED rather than silent (debugPartialCommandRefused)");
            Check(Mathf.Abs(sys.GetRuntime(0).ActualPowerPercent
                            - sys.GetRuntime(1).ActualPowerPercent) < 1e-3f, "L-1" + s + "3c",
                "with both engines on the scalar, so the un-addressed engine was not left silently "
                + "asymmetric: " + F(sys.GetRuntime(0).ActualPowerPercent) + " vs "
                + F(sys.GetRuntime(1).ActualPowerPercent));

            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(shared);

            // ---------------------------------------------------------- fake-null profile

            PlayModeDestroyedProfile(session);

            // ---------------------------------------------------------- state-leak record

            if (session == 1)
            {
                SessionState.SetFloat(KeySession1Left, left);
                SessionState.SetFloat(KeySession1Right, right);
                leakCanaryPowerPercent = 77.5f;
                sb.AppendLine("        recorded the session-1 spool result (" + F(left) + ", "
                              + F(right) + ") and armed the leak canary at 77.5");
            }
            else
            {
                StateLeakChecks(left, right);
            }

            SessionState.SetString(KeyPlayFingerprint, DomainFingerprint);

            // The allocation probe needs real player-loop frames, so it runs as a component and the
            // editor-side machine waits for it before leaving Play Mode.
            GameObject probeHost = new GameObject("p02b-allocprobe-" + SyntheticTag);
            MavPropulsionPlayModeProbe probe = probeHost.AddComponent<MavPropulsionPlayModeProbe>();

            // AddComponent returns NULL rather than throwing when Unity refuses the type - which is
            // how the first attempt failed, with the probe in the Editor folder: "Can't add script
            // behaviour because it is an editor script". Checked explicitly so that failure is a
            // reported result instead of a NullReferenceException in a callback.
            if (probe == null)
            {
                Check(false, "L-1" + s + "5-SETUP",
                    "the allocation probe could not be attached, so no allocation measurement was "
                    + "taken - reported as a setup FAILURE, never as a pass");
                MavPropulsionPlayModeProbe.ForceFinish();
                return;
            }

            probe.Configure(SyntheticTag, SynthThrustN, SynthLateralOffsetM);
        }

        private static void StateLeakChecks(float left, float right)
        {
            Section("[L-140] no stale engine state carried from session 1 into session 2");

            float s1l = SessionState.GetFloat(KeySession1Left, -1f);
            float s1r = SessionState.GetFloat(KeySession1Right, -1f);
            Check(Mathf.Abs(left - s1l) < 1e-4f && Mathf.Abs(right - s1r) < 1e-4f, "L-140",
                "session 2 reproduces session 1's spool exactly (" + F(left) + "/" + F(right)
                + " vs " + F(s1l) + "/" + F(s1r) + "), so the second session began from the same "
                + "initial condition as the first");

            // FALSIFIABILITY. Prove that comparison would have noticed carried-over state, by
            // continuing from a different initial condition instead of a fresh one.
            MavEngineProfile p2 = MakeProfile("l140-" + SyntheticTag, null,
                MavEnginePowerDynamicsLaw.F16GarzaMorelliPowerState);
            GameObject g2 = new GameObject("p02b-leakcontrol-" + SyntheticTag);
            MavPropulsionSystem s2 = g2.AddComponent<MavPropulsionSystem>();
            s2.installation = MakeTwin(p2);
            s2.autoResolveCommandSource = false;
            string e2;
            s2.Build(true, out e2);

            MavScriptedPropulsionCommandSource src2 =
                g2.AddComponent<MavScriptedPropulsionCommandSource>();
            src2.Resize(2);
            src2.SetEngineThrottle(0, 1.00f);
            src2.SetEngineThrottle(1, 0.25f);
            s2.commandSource = src2;

            s2.ResetEngineState(1f);            // a DIFFERENT initial condition
            for (int i = 0; i < 3; i++)         // only a few steps, so it cannot converge
                s2.Evaluate(LevelState(), Atmosphere(), 0.5f, 0.02f);

            float dirtyRight = s2.GetRuntime(1).ActualPowerPercent;
            Check(Mathf.Abs(dirtyRight - s1r) > 1f, "L-140b",
                "and the same comparison DOES separate a carried-over initial condition from a "
                + "fresh one (" + F(dirtyRight) + " vs " + F(s1r) + "), so L-140 is a check that "
                + "could have failed");

            UnityEngine.Object.DestroyImmediate(g2);
            UnityEngine.Object.DestroyImmediate(p2);
        }

        /// <summary>
        /// Whether this frame's command authority is exactly one owner, with the throttles the
        /// engines will actually read agreeing with the owner it claims.
        ///
        /// The point is not that the diagnostic STRING holds one of two values - that is trivially
        /// true - but that the string and the per-engine throttles cannot disagree. A blend looks
        /// like LINKED_SCALAR with the engines reading different values, or PER_ENGINE_SOURCE with
        /// fewer engines addressed than are installed.
        /// </summary>
        private static bool AuthorityIsUnambiguous(
            MavPropulsionSystem sys, ref int perEngineFrames, ref int linkedFrames)
        {
            MavPropulsionCommand c = sys.Command;
            int n = c.EngineCount;

            if (sys.debugCommandAuthority == MavPropulsionSystem.AuthorityPerEngineSource)
            {
                perEngineFrames++;
                return c.Authority == MavPropulsionCommandAuthority.PerEngineSource
                       && c.AddressedEngineCount == n
                       && n > 0;
            }

            if (sys.debugCommandAuthority == MavPropulsionSystem.AuthorityLinkedScalar)
            {
                linkedFrames++;
                if (c.Authority != MavPropulsionCommandAuthority.LinkedScalar)
                    return false;

                for (int i = 0; i < n; i++)
                {
                    if (Mathf.Abs(c.ThrottleForEngine(i) - c.LinkedThrottle01) > 1e-6f)
                        return false;
                }

                return true;
            }

            return false;
        }

        private static void PlayModeDestroyedProfile(int session)
        {
            string s = session.ToString();
            Section("[L-1" + s + "4] PLAY MODE session " + s
                    + ": destroyed MavEngineProfile (Unity fake-null)");

            // Declared Authoritative so the aggregate reports authoritative data BEFORE the
            // profile is destroyed - otherwise the before/after contrast this check exists for is
            // false on both sides for an unrelated reason. Still SYNTHETIC_VALIDATION_ONLY: the
            // label is what the test needs to exercise the gate, not a claim about the number.
            MavFixedSyntheticDeck deck = MakeDeck(SynthThrustN, MavThrustDataAuthority.Authoritative);
            MavEngineProfile profile = MakeProfile(
                "l1" + s + "4-" + SyntheticTag, deck,
                MavEnginePowerDynamicsLaw.F16GarzaMorelliPowerState);

            GameObject go = new GameObject("p02b-fakenull-" + SyntheticTag);
            MavPropulsionSystem sys = go.AddComponent<MavPropulsionSystem>();
            sys.installation = MakeSingle(profile);
            sys.autoResolveCommandSource = false;
            string err;
            sys.Build(true, out err);
            sys.ResetEngineState(1f);

            MavPropulsiveLoads healthyLoads = MavPropulsiveLoads.Zero;
            for (int i = 0; i < 400; i++)
                healthyLoads = sys.Evaluate(LevelState(), Atmosphere(), 1f, 0.02f);

            float healthyThrust = sys.debugTotalThrustN;
            Check(healthyThrust > 1f, "L-1" + s + "4a",
                "with a live profile the engine produces thrust from its deck: " + F(healthyThrust)
                + " N");

            // TWO DIFFERENT QUESTIONS, and both have to be asked.
            //
            // sys.HasAuthoritativeData is the CONFIGURATION gate: it walks the slots and asks whether
            // every installed engine's profile declares sourced data. loads.hasAuthoritativeData is
            // the DATA AUTHORITY carried on this step's load set, composed from what the engines
            // actually contributed - and it is the one the everySlotHasProfile guard protects.
            //
            // Only the first was checked at first, and a mutation probe showed why that was not
            // enough: deleting the guard left this suite entirely green, because a null profile also
            // fails the configuration gate for its own separate reason. The load set was never
            // inspected, so the defect P0.2 fixed had no Play Mode coverage at all.
            Check(sys.HasAuthoritativeData, "L-1" + s + "4b",
                "CONFIGURATION: every installed engine's profile declares sourced thrust data while "
                + "the profile is intact");
            Check(healthyLoads.hasAuthoritativeData, "L-1" + s + "4b2",
                "DATA AUTHORITY: and this step's load set carries authoritative data too, which is "
                + "the state the next step has to take away");

            UnityEngine.Object.DestroyImmediate(profile);
            Check(sys.installation.engines[0].engineProfile == null, "L-1" + s + "4c",
                "after DestroyImmediate the slot's profile is Unity fake-null: the C# reference "
                + "survives while == null is true, which is the state a plain class cannot reach");

            bool threw = false;
            string ex = string.Empty;
            MavPropulsiveLoads lostLoads = MavPropulsiveLoads.Zero;
            try
            {
                for (int i = 0; i < 50; i++)
                    lostLoads = sys.Evaluate(LevelState(), Atmosphere(), 1f, 0.02f);
            }
            catch (Exception e)
            {
                threw = true;
                ex = e.GetType().Name + ": " + e.Message;
            }

            Check(!threw, "L-1" + s + "4d",
                "evaluating with a destroyed profile throws nothing"
                + (threw ? " - but it did: " + ex : ""));
            Check(!sys.HasAuthoritativeData, "L-1" + s + "4e",
                "CONFIGURATION now fails: a slot whose profile is gone cannot declare sourced data");
            Check(!lostLoads.hasAuthoritativeData, "L-1" + s + "4e2",
                "DATA AUTHORITY now fails on the LOAD SET itself - the vacuous-truth defect P0.2 "
                + "found stays fixed in Play Mode. With no engine contributing, the "
                + "per-contribution unanimity is trivially true, so this can only hold because the "
                + "aggregate separately requires every slot to still have a profile");
            Check(lostLoads.hasAuthoritativeData != healthyLoads.hasAuthoritativeData,
                "L-1" + s + "4e3",
                "and the load set's authority actually CHANGED across the destruction ("
                + healthyLoads.hasAuthoritativeData + " -> " + lostLoads.hasAuthoritativeData
                + "), so this pair is a transition rather than two readings that agree by accident");
            Check(!sys.IsAcceptableForLiveFlight, "L-1" + s + "4f",
                "readiness fails, with the reason: " + sys.ThrustDataStatus);
            Check(Mathf.Abs(sys.debugTotalThrustN) < 1e-6f, "L-1" + s + "4g",
                "and thrust is exactly zero (" + F(sys.debugTotalThrustN)
                + " N) rather than a last-known value");

            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(deck.gameObject);
        }

        private static void HarvestProbe(int session)
        {
            string s = session.ToString();
            Section("[L-1" + s + "5] PLAY MODE session " + s
                    + ": GC allocation measured in the real player loop");

            int per = MavPropulsionPlayModeProbe.StepsPerFrameUsed;
            int known = MavPropulsionPlayModeProbe.SensitivityBytesPerStep;
            string instrument = MavPropulsionPlayModeProbe.InstrumentUsed;

            sb.AppendLine("        instruments considered:");
            sb.Append("          \"GC Allocated In Frame\" counter : ")
              .Append(MavPropulsionPlayModeProbe.RecorderValid ? "started" : "not available")
              .Append("  idle ").Append(MavPropulsionPlayModeProbe.MinFrameBytesIdle)
              .Append(" / evaluating ").Append(MavPropulsionPlayModeProbe.MinFrameBytesEvaluating)
              .Append(" / sensitivity ").Append(MavPropulsionPlayModeProbe.MinFrameBytesSensitivity)
              .AppendLine(" bytes/frame");
            sb.Append("          GC.Alloc marker, per frame     : ")
              .Append(MavPropulsionPlayModeProbe.MarkerRecorderValid ? "started" : "not available")
              .Append("  idle ").Append(MavPropulsionPlayModeProbe.MarkerBytesIdle)
              .Append(" / evaluating ").Append(MavPropulsionPlayModeProbe.MarkerBytesEvaluating)
              .Append(" / sensitivity ").Append(MavPropulsionPlayModeProbe.MarkerBytesSensitivity)
              .AppendLine(" bytes/frame");
            sb.Append("          profiler enabled by this probe : ")
              .Append(MavPropulsionPlayModeProbe.ProfilerEnabledForRun)
              .Append(" (was ").Append(MavPropulsionPlayModeProbe.ProfilerWasEnabled)
              .AppendLine(" before, and restored afterwards)");
            sb.AppendLine("          GC.GetTotalMemory delta        : a LEVEL, not a counter - it "
                          + "reads 0 for a deliberate 40 bytes/step, so it cannot see short-lived "
                          + "garbage. Corroboration only.");
            sb.AppendLine("          GC.CollectionCount(0)          : Boehm's trigger threshold "
                          + "scales with heap size - 38 MB of deliberate garbage moved it by zero "
                          + "here. Corroboration only.");
            sb.AppendLine("          GC.GetTotalAllocatedBytes      : UNAVAILABLE - needs .NET "
                          + "Standard 2.1 and this project is set to 2.0, which P0.2b does not "
                          + "change to suit its own measurement.");
            sb.Append("        INSTRUMENT USED: ").AppendLine(instrument);

            bool counterBites = instrument == "GC Allocated In Frame counter";
            bool markerBites = instrument == "GC.Alloc marker, summed per frame";

            long busy = counterBites
                ? MavPropulsionPlayModeProbe.MinFrameBytesEvaluating
                : MavPropulsionPlayModeProbe.MarkerBytesEvaluating;
            long idle = counterBites
                ? MavPropulsionPlayModeProbe.MinFrameBytesIdle
                : MavPropulsionPlayModeProbe.MarkerBytesIdle;
            long sens = counterBites
                ? MavPropulsionPlayModeProbe.MinFrameBytesSensitivity
                : MavPropulsionPlayModeProbe.MarkerBytesSensitivity;

            double propulsionPerStep = per > 0 ? (double)(busy - idle) / per : double.NaN;
            double sensitivityPerStep = per > 0 ? (double)(sens - idle) / per : double.NaN;

            // ORDER MATTERS. The sensitivity control decides whether the propulsion figure means
            // anything at all, so it is judged first. A flat instrument hands out a free pass: it
            // reports zero for everything, including for the 1600 bytes per frame this phase
            // deliberately allocates - which is exactly what the first two P0.2b runs measured.
            if (counterBites || markerBites)
            {
                Check(true, "L-1" + s + "5b",
                    "the chosen instrument SEES a known allocation: "
                    + sensitivityPerStep.ToString("0.###") + " bytes/step measured against a "
                    + "deliberate " + known + " bytes/step (" + per + " per frame). The P0.2 "
                    + "heap-delta instrument reported 0 for this same control, which is why the "
                    + "method changed");
                Check(propulsionPerStep < 1d, "L-1" + s + "5a",
                    "and on that instrument the propulsion hot path costs "
                    + propulsionPerStep.ToString("0.###") + " bytes/step - a DIFFERENTIAL against "
                    + "an idle frame, so whatever the rest of the player loop allocates appears in "
                    + "both terms and cancels");
                Check(sens > busy && busy >= idle, "L-1" + s + "5c",
                    "with the three phases ordering correctly: idle " + idle + " <= evaluating "
                    + busy + " < sensitivity " + sens + " bytes/frame");
            }
            else
            {
                // Per the P0.2b brief: if no direct Unity allocation counter is usable here, say so
                // and do not dress the fallback up as a proof.
                Check(false, "L-1" + s + "5a",
                    "NEITHER Unity allocation instrument passed its sensitivity control in this "
                    + "context, so NO allocation claim is made from Play Mode. The counter read "
                    + "idle " + MavPropulsionPlayModeProbe.MinFrameBytesIdle + " / sensitivity "
                    + MavPropulsionPlayModeProbe.MinFrameBytesSensitivity + " and the marker read "
                    + "idle " + MavPropulsionPlayModeProbe.MarkerBytesIdle + " / sensitivity "
                    + MavPropulsionPlayModeProbe.MarkerBytesSensitivity
                    + " bytes/frame - flat across a phase allocating " + (per * known)
                    + " bytes/frame. What stands is the edit-mode bound in U-009: NO OBSERVED "
                    + "STEADY PER-STEP ALLOCATION, which is not a formal zero-allocation proof");
            }

            // ---- corroborating observations, deliberately NOT assertions ------------------------
            int steps = MavPropulsionPlayModeProbe.LongLoopSteps;
            sb.Append("        corroboration (NOT load-bearing) - ").Append(steps)
              .Append(" steps in one real FixedUpdate, ")
              .Append(MavPropulsionPlayModeProbe.LongLoopMilliseconds.ToString("0"))
              .AppendLine(" ms:");
            sb.Append("          heap delta ")
              .Append(MavPropulsionPlayModeProbe.LongLoopHeapBytesPerStep.ToString("0.###"))
              .AppendLine(" bytes/step - includes background editor allocation over that window");
            sb.Append("          gen-0 collections ")
              .Append(MavPropulsionPlayModeProbe.Gen0CollectionsEvaluating)
              .Append(", and ").Append(MavPropulsionPlayModeProbe.Gen0CollectionsAllocControl)
              .Append(" for a control allocating ~")
              .Append((long)steps * MavPropulsionPlayModeProbe.SensitivityBytesPerStep / 1048576L)
              .AppendLine(" MB - a zero there is what disqualified this counter as primary");

            Check(steps >= 1000000, "L-1" + s + "5e",
                "the corroborating long loop did run " + steps + " steps");

            sb.Append("        frames measured: ").Append(MavPropulsionPlayModeProbe.FramesEvaluating)
              .Append(" evaluating, ").Append(MavPropulsionPlayModeProbe.FramesIdle)
              .Append(" idle, ").Append(MavPropulsionPlayModeProbe.FramesSensitivity)
              .Append(" sensitivity; FixedUpdate calls ")
              .Append(MavPropulsionPlayModeProbe.FixedUpdates).AppendLine();

            Check(MavPropulsionPlayModeProbe.FramesEvaluating > 30
                  && MavPropulsionPlayModeProbe.FramesIdle > 30
                  && MavPropulsionPlayModeProbe.FramesSensitivity > 30, "L-1" + s + "5f",
                "all three phases were measured across many real player-loop frames rather than "
                + "from a single sample");
            Check(MavPropulsionPlayModeProbe.FixedUpdates > 10, "L-1" + s + "5g",
                "and the same code path also ran under a real fixed timestep ("
                + MavPropulsionPlayModeProbe.FixedUpdates + " FixedUpdate calls), which is where a "
                + "flight model actually runs");
        }

        // ============================================================== edit mode, between

        private static void EditModeChecksBetweenSessions()
        {
            Section("[L-150] edit mode AFTER exiting Play Mode session 1");

            bool expectReload = SessionState.GetInt(KeyMode, 0) == 0;

            Check(MavF16EngineLawRegistrar.IsRegistered, "L-150",
                "the engine-law registry is intact after leaving Play Mode - exiting did not leave "
                + "the editor unable to resolve the law");

            int count = MavEnginePowerDynamicsFactory.F16RegistrationCount;
            Check(count >= 1 && count <= 3, "L-150b",
                "registration count after one full enter/exit cycle is " + count
                + " - bounded, not one more per transition");

            float canary = leakCanaryPowerPercent;
            bool reloadedOnExit =
                DomainFingerprint != SessionState.GetString(KeyPlayFingerprint, string.Empty);

            sb.Append("        leak canary after exit: ").Append(F(canary))
              .Append(" (armed at 77.5 inside Play Mode); domain reload OBSERVED on exit: ")
              .Append(reloadedOnExit).AppendLine();

            // POSITIVE CONTROL for the state-leak experiment - what it is capable of seeing - and a
            // measurement of Unity's own lifecycle rather than an assumption about it.
            //
            // The first version of this check assumed that "domain reload enabled" meant a reload on
            // BOTH the enter and the exit transition, and asserted the canary was cleared after
            // exiting. It failed: the fingerprint shows Unity reloads the domain when ENTERING Play
            // Mode and not when leaving it, so a static armed during a session is still there in edit
            // mode afterwards, and is cleared by the NEXT entry instead. The check now ties the two
            // observations together, which holds under either setting and would catch a change in
            // Unity's behaviour rather than encoding one belief about it.
            Check(reloadedOnExit == (canary < 0f), "L-150c",
                "the leaky static's fate matches the reload actually observed on this transition: "
                + "reload " + reloadedOnExit + ", canary cleared " + (canary < 0f)
                + ". Unity reloads the domain on ENTERING Play Mode, not on leaving it, so a static "
                + "armed in a session survives into edit mode and is cleared by the next entry - "
                + "which is what L-140 relies on");
            Check(canary > 70f || reloadedOnExit, "L-150d",
                "and the canary can in fact carry state across a transition (value " + F(canary)
                + "), so the experiment is able to observe leakage at all - the production types "
                + "have nothing to carry, which L-151 checks separately");

            Section("[L-151] there is no static engine state available to leak");

            // With domain reload disabled, anything static in the propulsion types persists across
            // sessions. The architecture's answer is that there is nothing static to persist, and
            // that is checkable rather than merely asserted.
            Type[] types = new Type[]
            {
                typeof(MavPropulsionSystem),
                typeof(MavEngineRuntime),
                typeof(MavPropulsionCommand),
                typeof(MavEngineProfile),
                typeof(MavEngineInstallation),
                typeof(MavPropulsionInstallationProfile)
            };

            int mutableStatics = 0;
            StringBuilder found = new StringBuilder();
            for (int t = 0; t < types.Length; t++)
            {
                FieldInfo[] fields = types[t].GetFields(
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                for (int f = 0; f < fields.Length; f++)
                {
                    if (fields[f].IsLiteral || fields[f].IsInitOnly)
                        continue;

                    mutableStatics++;
                    found.Append(types[t].Name).Append('.').Append(fields[f].Name).Append(' ');
                }
            }

            Check(mutableStatics == 0, "L-151",
                "the six propulsion types hold ZERO mutable static fields, so no engine state can "
                + "survive a Play Mode session even with domain reload disabled"
                + (mutableStatics > 0 ? " - found: " + found : string.Empty));

            // Positive control: the same scan must be able to find one when one exists.
            FieldInfo canaryField = typeof(MavSharedPropulsionPlayModeLifecycle).GetField(
                "leakCanaryPowerPercent", BindingFlags.Static | BindingFlags.NonPublic);
            Check(canaryField != null && !canaryField.IsLiteral && !canaryField.IsInitOnly,
                "L-151b",
                "and the same scan does find a mutable static when there is one - it found this "
                + "suite's own leak canary, so L-151 is not passing by looking in the wrong place");

            // The one static that SHOULD exist, named explicitly rather than silently excluded.
            FieldInfo[] factoryFields = typeof(MavEnginePowerDynamicsFactory).GetFields(
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            int factoryStatics = 0;
            for (int f = 0; f < factoryFields.Length; f++)
            {
                if (!factoryFields[f].IsLiteral && !factoryFields[f].IsInitOnly)
                    factoryStatics++;
            }

            Check(factoryStatics >= 1, "L-151c",
                "the ONE deliberate static is the law registry in MavEnginePowerDynamicsFactory ("
                + factoryStatics + " field(s)): a reference to a stateless strategy, not engine "
                + "state - and L-150b is what proves it does not accumulate");
        }

        private static void EditModeChecksAfterSecondSession()
        {
            Section("[L-160] edit mode after the full enter / exit / re-enter / exit cycle");

            Check(MavF16EngineLawRegistrar.IsRegistered, "L-160",
                "the law is still resolvable after two complete Play Mode sessions");

            int count = MavEnginePowerDynamicsFactory.F16RegistrationCount;
            Check(count >= 1 && count <= 4, "L-160b",
                "final registration count is " + count + " after two enter/exit cycles and two "
                + "deliberate clears - bounded, not one per transition");

            // Leave the editor with the law registered, as this suite found it.
            MavF16EngineLawRegistrar.EnsureRegistered();
            Check(MavF16EngineLawRegistrar.IsRegistered, "L-160c",
                "and the editor is left with the law registered, the state this suite started from");
        }

        // ============================================================== helpers

        private static void Open()
        {
            sb = new StringBuilder(SessionState.GetString(KeyReport, string.Empty));
            passed = SessionState.GetInt(KeyPassed, 0);
            failed = SessionState.GetInt(KeyFailed, 0);
        }

        private static void Close()
        {
            SessionState.SetString(KeyReport, sb.ToString());
            SessionState.SetInt(KeyPassed, passed);
            SessionState.SetInt(KeyFailed, failed);
        }

        private static void Section(string title)
        {
            sb.AppendLine();
            sb.AppendLine(title);
        }

        private static void Check(bool condition, string id, string description)
        {
            if (condition)
            {
                passed++;
                sb.Append("  PASS  ").Append(id).Append("  ").AppendLine(description);
            }
            else
            {
                failed++;
                sb.Append("  FAIL  ").Append(id).Append("  ").AppendLine(description);
            }
        }

        private static string F(float v)
        {
            return v.ToString("0.####");
        }

        private static MavEngineProfile MakeProfile(
            string id, MavThrustDeckBase deck, MavEnginePowerDynamicsLaw law)
        {
            MavEngineProfile p = MavEngineProfile.CreateInMemory(id);
            p.displayName = id;
            p.engineVariantIdentity = SyntheticTag + " - not aircraft data";
            p.sourceIdentity = SyntheticTag;
            p.provenance = MavEngineDataProvenance.CrossValidationOnly;
            p.powerDynamicsLaw = law;
            p.powerDynamicsProvenance = law == MavEnginePowerDynamicsLaw.InstantNoSourcedTransient
                ? MavEngineDataProvenance.Unavailable
                : MavEngineDataProvenance.PublicReference;
            p.thrustDeck = deck;
            return p;
        }

        private static MavEngineInstallation MakeSlot(
            int id, string name, MavEngineProfile profile, Vector3 pos, int channel)
        {
            MavEngineInstallation slot = new MavEngineInstallation();
            slot.slotId = id;
            slot.slotName = name;
            slot.engineProfile = profile;
            slot.positionAeroBodyM = pos;
            slot.thrustDirectionAeroBody = new Vector3(1f, 0f, 0f);
            slot.geometryDeclared = true;
            slot.geometryProvenance = SyntheticTag + " - not aircraft geometry";
            slot.throttleChannel = channel;
            slot.enabled = true;
            return slot;
        }

        private static MavPropulsionInstallationProfile MakeTwin(MavEngineProfile shared)
        {
            MavPropulsionInstallationProfile i = new MavPropulsionInstallationProfile();
            i.installationId = "p02b-twin-" + SyntheticTag;
            i.aircraftConfiguration = SyntheticTag;
            i.engines = new MavEngineInstallation[]
            {
                MakeSlot(0, "left", shared, new Vector3(0f, -SynthLateralOffsetM, 0f), 0),
                MakeSlot(1, "right", shared, new Vector3(0f, SynthLateralOffsetM, 0f), 1)
            };
            return i;
        }

        private static MavPropulsionInstallationProfile MakeSingle(MavEngineProfile profile)
        {
            MavPropulsionInstallationProfile i = new MavPropulsionInstallationProfile();
            i.installationId = "p02b-single-" + SyntheticTag;
            i.aircraftConfiguration = SyntheticTag;
            i.engines = new MavEngineInstallation[]
            {
                MakeSlot(0, "single", profile, Vector3.zero, 0)
            };
            return i;
        }

        /// <summary>
        /// A RUNTIME synthetic deck, not the editor-assembly one the P0.2 suite uses.
        ///
        /// Unity refuses to attach an editor-assembly MonoBehaviour to a GameObject in Play Mode,
        /// so every component this suite creates has to come from the runtime validation assembly.
        /// SYNTHETIC_VALIDATION_ONLY - never aircraft data.
        /// </summary>
        private static MavFixedSyntheticDeck MakeDeck(float thrustN, MavThrustDataAuthority authority)
        {
            GameObject host = new GameObject("p02b-deck-" + SyntheticTag);
            MavFixedSyntheticDeck d = host.AddComponent<MavFixedSyntheticDeck>();
            d.fixedThrustN = thrustN;
            d.declaredAuthority = authority;
            return d;
        }

        private static MavFlightState LevelState()
        {
            MavFlightState s = new MavFlightState();
            s.worldPositionM = new Vector3(0f, 3000f, 0f);
            s.mach = 0.5f;
            s.trueAirspeedMps = 160f;
            return s;
        }

        private static MavAtmosphereSample Atmosphere()
        {
            MavAtmosphereSample a = new MavAtmosphereSample();
            a.altitudeM = 3000f;
            a.densityKgM3 = 0.9093f;
            a.speedOfSoundMps = 328.6f;
            return a;
        }
    }
}
