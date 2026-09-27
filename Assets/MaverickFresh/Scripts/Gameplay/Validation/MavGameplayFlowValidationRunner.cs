#if UNITY_EDITOR
using System;
using System.Collections;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using MaverickFresh.FlightDynamics;
using MaverickFresh.FlightDynamics.F15;
using Object = UnityEngine.Object;

namespace MaverickFresh.Gameplay.Validation
{
    /// <summary>
    /// Gameplay Integration R1 - Play Mode game-flow test on the REAL scenes, the way a player goes through them:
    ///
    ///   [L] Main Lobby loads with one camera and one listener
    ///   [H] Hangar: F-15 and F-16 cards; a click selects; F-16 cannot fly and does not launch; F-15 FLY launches
    ///   [F] Mav_InGame: exactly one F-15, AssistedV2, V1 off, started from trim, ownership held, camera and HUD bound,
    ///       one operational input source; one camera / listener / input owner / armed body; no legacy aircraft
    ///   [R] neutral, pitch, roll, yaw through the player input path: finite, right signs, no fault, one load per step
    ///   [P] pause stops the clock and the input and resumes the same aircraft
    ///   [S] restart: the old aircraft is gone, exactly one new F-15 V2 at the known start
    ///   [B] return to hangar: nothing from the flight survives the scene change; the selection is kept
    ///   [X] F-16 forced into the flight scene: refused with its reason, nothing spawned, nothing substituted
    ///   [D] development launch: the legacy stack runs alone, labelled, with no pilot-controlled rig beside it
    ///
    /// It proves the WIRING of the game, not the flight physics, which have their own validation. Time.captureFramerate
    /// = 50 gives one physics step per frame.
    /// </summary>
    public sealed class MavGameplayFlowValidationRunner : MonoBehaviour
    {
        public static bool Finished;
        public static int Passed;
        public static int Failed;
        public static string Report = "";

        private readonly StringBuilder sb = new StringBuilder(16 * 1024);
        private int fixedSteps;

        public static void Clear()
        {
            Finished = false;
            Passed = 0;
            Failed = 0;
            Report = "";
        }

        private void Awake()
        {
            Clear();
            DontDestroyOnLoad(gameObject);
        }

        private void FixedUpdate()
        {
            fixedSteps++;
        }

        private IEnumerator Start()
        {
            int savedCapture = Time.captureFramerate;
            int prefAircraft = PlayerPrefs.GetInt("MavSelectedAircraft", -1);
            int prefHas = PlayerPrefs.GetInt("MavHasSelection", -1);
            int prefMode = PlayerPrefs.GetInt("MavSelectedMode", -1);
            Time.captureFramerate = 50;
            sb.AppendLine("MAVERICK GAMEPLAY INTEGRATION R1 - PLAY MODE GAME FLOW");
            sb.AppendLine("Real scenes (Mav_MainLobby, Mav_Hangar, Mav_InGame), captureFramerate 50. Proves game wiring, not flight physics.");

            IEnumerator[] steps = { Lobby(), Hangar(), Flight(), Response(), PauseResume(), Restart(), ReturnToHangar(), F16Refusal(), Development() };
            foreach (IEnumerator step in steps)
            {
                bool threw = false;
                while (true)
                {
                    object current;
                    try
                    {
                        if (!step.MoveNext())
                            break;
                        current = step.Current;
                    }
                    catch (Exception e)
                    {
                        Check(false, "E", "exception: " + e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace);
                        threw = true;
                        break;
                    }

                    yield return current;
                }

                if (threw)
                    break;
            }

            Time.timeScale = 1f;
            Time.captureFramerate = savedCapture;
            RestorePref("MavSelectedAircraft", prefAircraft);
            RestorePref("MavHasSelection", prefHas);
            RestorePref("MavSelectedMode", prefMode);
            PlayerPrefs.Save();

            sb.AppendLine();
            sb.Append("RESULT: ").Append(Failed == 0 ? "PASS" : "FAIL").Append(" passed=").Append(Passed).Append(" failed=").Append(Failed);
            Report = sb.ToString();
            Finished = true;
        }

        // ------------------------------------------------------------------ [L]

        private IEnumerator Lobby()
        {
            Section("[L] Main Lobby");
            SceneManager.LoadScene(MavSceneNames.MainLobby);
            yield return Frames(3);
            MavPlayerSceneCensus c = MavPlayerSceneCensus.Take();
            Check(Object.FindAnyObjectByType<MavMainLobbyBootstrap>() != null && c.enabledCameras == 1 && c.enabledAudioListeners == 1 && c.pilotRigs == 0,
                "L", "the lobby loads with one camera and one audio listener, and no aircraft (" + c.Describe() + ")");
        }

        // ------------------------------------------------------------------ [H]

        private MavHangarBootstrap hangar;

        private IEnumerator Hangar()
        {
            Section("[H] Hangar selection");
            MavGameSession.HasSelection = false;
            SceneManager.LoadScene(MavSceneNames.Hangar);
            yield return Frames(3);
            hangar = Object.FindAnyObjectByType<MavHangarBootstrap>();
            if (hangar == null)
            {
                Check(false, "H", "the hangar bootstrap exists");
                yield break;
            }

            MavPlayerSceneCensus c = MavPlayerSceneCensus.Take();
            Check(hangar.PlayerAircraft.Count == 2 && hangar.PlayerAircraft[0].displayName == MavPlayableAircraftRegistry.F15DisplayName
                  && hangar.PlayerAircraft[1].displayName == MavPlayableAircraftRegistry.F16DisplayName && c.enabledCameras == 1 && c.enabledAudioListeners == 1,
                "H", "the hangar shows exactly [" + hangar.PlayerAircraft[0].displayName + "] [" + hangar.PlayerAircraft[1].displayName + "], one camera, one listener");
            Check(hangar.Selected != null && hangar.Selected.aircraft == MavAircraftKind.F15E && !MavGameSession.HasSelection,
                "H", "with nothing selected the F-15 card is highlighted and the session is not rewritten");

            hangar.SelectCard(MavAircraftKind.F16C);
            string reason;
            bool f16CanFly = hangar.CanFly(out reason);
            bool f16Flew = hangar.Fly();
            yield return Frames(3);
            Check(!f16CanFly && !f16Flew && SceneManager.GetActiveScene().name == MavSceneNames.Hangar && MavGameSession.SelectedAircraft == MavAircraftKind.F16C
                  && !MavGameSession.HasLaunchRequest,
                "H", "clicking F-16 selects it; FLY is refused (\"" + reason + "\"), the hangar stays, nothing is launched or substituted");

            hangar.SelectCard(MavAircraftKind.F15E);
            bool f15CanFly = hangar.CanFly(out reason);
            Check(f15CanFly && MavGameSession.SelectedAircraft == MavAircraftKind.F15E, "H", "clicking F-15 selects it and enables FLY");
            bool flew = hangar.Fly();
            Check(flew && MavGameSession.HasLaunchRequest && MavGameSession.CurrentLaunch.aircraft == MavAircraftKind.F15E
                  && MavGameSession.CurrentLaunch.kind == MavFlightLaunchKind.Player,
                "H", "FLY launches the F-15 (" + MavGameSession.CurrentLaunch + ")");
        }

        // ------------------------------------------------------------------ [F]

        private MavFlightSessionDirector director;
        private MavF15PilotControlledRig rig;

        private IEnumerator Flight()
        {
            Section("[F] F-15 flight start");
            yield return WaitForState(MavFlightSessionState.Playing, 10f);
            director = MavFlightSessionDirector.Active;
            if (director == null || director.State != MavFlightSessionState.Playing)
            {
                Check(false, "F", "the flight reaches PLAYING" + (director != null ? " (state " + director.State + ": " + director.failureReason + " | " + director.failureDetail + ")" : " (no director)"));
                yield break;
            }

            MavSpawnedAircraft a = director.Aircraft;
            rig = a.rig;
            Check(director.Definition.aircraft == MavAircraftKind.F15E && director.request.kind == MavFlightLaunchKind.Player && SceneManager.GetActiveScene().name == MavSceneNames.InGame,
                "F", "Mav_InGame flies the requested F-15 (" + director.request + ")");

            string verify;
            bool v2 = MavPlayableAircraftSpawner.VerifyAssistedV2(rig, out verify);
            Check(v2 && rig.controlMode == MavF15PilotControlMode.AssistedV2 && rig.lawV2.enabled && !rig.law.enabled && a.body.controlLaw == rig.lawV2,
                "F", "AssistedV2 active, V2 law bound, DirectV1 inactive" + (v2 ? "" : " (" + verify + ")"));
            Check(rig.debugStartSucceeded && rig.ownership != null && rig.ownership.owner == MavFlightPhysicsOwner.F15PilotControlledResearch && a.body.ArmedForLiveFlight,
                "F", "started from the validated trim; ownership " + (rig.ownership != null ? rig.ownership.owner.ToString() : "none") + ", body armed");

            MavFlightCameraController cam = Camera.main != null ? Camera.main.GetComponent<MavFlightCameraController>() : null;
            Check(cam != null && cam.target == a.renderPose && a.renderPose.hasPose && director.FlightCamera == cam,
                "F", "the scene camera is the flight camera, bound to the aircraft's render pose");
            Check(director.Hud != null && director.Hud.director == director && director.physicsReady && director.playerControlActive,
                "F", "player HUD bound; physics ready; player control active only now");
            Check(a.input.inputEnabled && a.input.source == rig.commandSource && a.commandSource.IsOperationalCommandSource && a.commandSource.HasCommandSignal
                  && a.body.pilotCommandSource == a.commandSource,
                "F", "one operational command source, written by the one player input");

            MavPlayerSceneCensus c = MavPlayerSceneCensus.Take();
            Check(c.pilotRigs == 1 && c.armedBodies == 1 && c.enabledCameras == 1 && c.enabledAudioListeners == 1 && c.operationalSourcesWithSignal == 1
                  && c.playerInputs == 1 && c.flightDirectors == 1 && c.activeLegacyPlayers == 0,
                "F", "G11-G13 exactly one aircraft, camera, listener, input owner, operational source, armed body, director; no legacy aircraft (" + c.Describe() + ")");
        }

        // ------------------------------------------------------------------ [R]

        private IEnumerator Response()
        {
            Section("[R] Controls through the player input path");
            if (!Alive())
            {
                Check(false, "R", "a flight to test");
                yield break;
            }

            MavSpawnedAircraft a = director.Aircraft;
            MavSixDoFBody body = a.body;
            int loads0 = body.debugLoadApplications, steps0 = body.debugPhysicsStepIndex, dup0 = body.debugRejectedDuplicateApplications;
            bool finite = true, owner = true;
            a.input.scriptedOverride = true;

            float[] deltas = new float[3];
            string[] names = { "pitch (q)", "roll (p)", "yaw (r)" };
            a.input.scriptedCommand = MavPilotCommand.Neutral;
            for (int f = 0; f < 50; f++) { yield return null; Track(body, ref finite, ref owner); }
            for (int axis = 0; axis < 3; axis++)
            {
                float before = Rate(body, axis);
                MavPilotCommand cmd = MavPilotCommand.Neutral;
                if (axis == 0) cmd.pitch = 0.5f; else if (axis == 1) cmd.roll = 0.5f; else cmd.yaw = 0.5f;
                a.input.scriptedCommand = cmd;
                for (int f = 0; f < 50; f++) { yield return null; Track(body, ref finite, ref owner); }
                deltas[axis] = Rate(body, axis) - before;
                a.input.scriptedCommand = MavPilotCommand.Neutral;
                for (int f = 0; f < 100; f++) { yield return null; Track(body, ref finite, ref owner); }
            }

            a.input.scriptedOverride = false;
            for (int axis = 0; axis < 3; axis++)
                Check(deltas[axis] > 0.005f, "R", "+0.5 " + names[axis] + " for 1 s: rate change " + deltas[axis].ToString("+0.0000;-0.0000", CultureInfo.InvariantCulture) + " rad/s (positive expected)");

            int loads = body.debugLoadApplications - loads0, steps = body.debugPhysicsStepIndex - steps0;
            Check(finite && owner && director.State == MavFlightSessionState.Playing && loads == steps && steps > 0
                  && body.debugRejectedDuplicateApplications == dup0,
                "R", "every step finite, ownership held, no fault; one load application per physics step (" + loads + "/" + steps + "), no duplicate");
        }

        // ------------------------------------------------------------------ [P]

        private IEnumerator PauseResume()
        {
            Section("[P] Pause / resume");
            if (!Alive())
            {
                Check(false, "P", "a flight to pause");
                yield break;
            }

            MavSpawnedAircraft a = director.Aircraft;
            int rigId = rig.GetInstanceID();
            director.Pause();
            int stepsAtPause = fixedSteps, loadsAtPause = a.body.debugLoadApplications;
            a.input.scriptedOverride = true;
            a.input.scriptedCommand = new MavPilotCommand { pitch = 1f, roll = 1f };
            yield return Frames(20);
            bool frozen = fixedSteps == stepsAtPause && a.body.debugLoadApplications == loadsAtPause;
            bool neutral = a.commandSource.command.pitch == 0f && a.commandSource.command.roll == 0f && !a.input.inputEnabled;
            Check(director.State == MavFlightSessionState.Paused && Time.timeScale == 0f && frozen && neutral,
                "P", "paused: clock stopped (0 physics steps in 20 frames), input suspended (stick held neutral against a full deflection)");
            a.input.scriptedOverride = false;

            director.Resume();
            int stepsAtResume = fixedSteps, loadsAtResume = a.body.debugLoadApplications;
            yield return Frames(20);
            int advanced = fixedSteps - stepsAtResume;
            Check(director.State == MavFlightSessionState.Playing && Time.timeScale == 1f && a.input.inputEnabled && advanced > 0
                  && a.body.debugLoadApplications - loadsAtResume == advanced && rig != null && rig.GetInstanceID() == rigId
                  && rig.ownership.owner == MavFlightPhysicsOwner.F15PilotControlledResearch,
                "P", "resumed: the same aircraft flies on (" + advanced + " steps, one load each), input restored, ownership held");
        }

        // ------------------------------------------------------------------ [S]

        private IEnumerator Restart()
        {
            Section("[S] Restart");
            if (!Alive())
            {
                Check(false, "S", "a flight to restart");
                yield break;
            }

            MavFlightSessionDirector oldDirector = director;
            MavF15PilotControlledRig oldRig = rig;
            int attempt = director.request.attempt;
            director.Pause();
            director.RestartFlight();
            float deadline = Time.realtimeSinceStartup + 10f;
            while ((MavFlightSessionDirector.Active == null || MavFlightSessionDirector.Active == oldDirector
                    || MavFlightSessionDirector.Active.State != MavFlightSessionState.Playing) && Time.realtimeSinceStartup < deadline)
                yield return null;

            director = MavFlightSessionDirector.Active;
            if (director == null || director == oldDirector || director.State != MavFlightSessionState.Playing)
            {
                Check(false, "S", "the restarted flight reaches PLAYING");
                yield break;
            }

            rig = director.Aircraft.rig;
            MavFlightState s = director.Aircraft.body.debugState;
            MavF15PilotTrimStart trim = MavF15PilotTrimStart.TableViiPoint36();
            float tasFt = s.trueAirspeedMps / 0.3048f;
            string verify;
            Check(oldRig == null && oldDirector == null && Time.timeScale == 1f, "S", "the old aircraft and session are destroyed; the clock runs again");
            Check(MavPlayableAircraftSpawner.VerifyAssistedV2(rig, out verify) && director.request.aircraft == MavAircraftKind.F15E && director.request.attempt == attempt + 1,
                "S", "a new F-15 in AssistedV2, same aircraft, attempt " + director.request.attempt);
            Check(Mathf.Abs(tasFt - (float)trim.trueAirspeedFtPerSec) < 0.5f && Mathf.Abs(s.AlphaDeg - (float)trim.alphaDeg) < 0.1f
                  && s.aeroBodyRatesRadSec.magnitude < 1e-3f && Mathf.Abs(rig.transform.position.y - (float)trim.altitudeM) < 5f,
                "S", "at the known start: TAS " + tasFt.ToString("F2") + " ft/s, alpha " + s.AlphaDeg.ToString("F3") + " deg, |rates| "
                     + s.aeroBodyRatesRadSec.magnitude.ToString("E1") + ", altitude " + rig.transform.position.y.ToString("F1") + " m");
            MavPlayerSceneCensus c = MavPlayerSceneCensus.Take();
            Check(c.pilotRigs == 1 && c.armedBodies == 1 && c.enabledCameras == 1 && c.enabledAudioListeners == 1 && c.operationalSourcesWithSignal == 1
                  && c.playerInputs == 1 && c.flightDirectors == 1 && c.activeLegacyPlayers == 0,
                "S", "exactly one of each after restart (" + c.Describe() + ")");
        }

        // ------------------------------------------------------------------ [B]

        private IEnumerator ReturnToHangar()
        {
            Section("[B] Return to hangar");
            if (!Alive())
            {
                Check(false, "B", "a flight to leave");
                yield break;
            }

            director.Pause();
            director.ReturnToHangar();
            float deadline = Time.realtimeSinceStartup + 10f;
            while (Object.FindAnyObjectByType<MavHangarBootstrap>() == null && Time.realtimeSinceStartup < deadline)
                yield return null;
            yield return Frames(3);
            hangar = Object.FindAnyObjectByType<MavHangarBootstrap>();
            MavPlayerSceneCensus c = MavPlayerSceneCensus.Take();
            Check(hangar != null && SceneManager.GetActiveScene().name == MavSceneNames.Hangar && c.pilotRigs == 0 && c.armedBodies == 0 && c.playerInputs == 0
                  && c.flightDirectors == 0 && c.enabledCameras == 1 && c.enabledAudioListeners == 1 && Object.FindAnyObjectByType<MavSixDoFBody>() == null
                  && Time.timeScale == 1f,
                "B", "back in the hangar: nothing from the flight survived the scene change (" + c.Describe() + ")");
            Check(MavGameSession.SelectedAircraft == MavAircraftKind.F15E && !MavGameSession.HasLaunchRequest && hangar != null && hangar.Selected.aircraft == MavAircraftKind.F15E,
                "B", "the F-15 is still selected and shown; no flight is pending");
        }

        // ------------------------------------------------------------------ [X]

        private IEnumerator F16Refusal()
        {
            Section("[X] F-16: current truth");
            MavPlayableAircraftDefinition f16 = MavPlayableAircraftRegistry.F16();
            Check(f16.status == MavPlayableAircraftStatus.NotReady && !f16.IsLaunchable && f16.blockers.Length > 0,
                "X", "F-16 is NOT READY: " + string.Join("; ", f16.blockers));

            // Forced past the hangar: the flight session must refuse it on its own.
            MavGameSession.SelectAircraft(MavAircraftKind.F16C);
            MavGameSession.SetLaunchRequest(new MavFlightLaunchRequest
            {
                aircraft = MavAircraftKind.F16C, mode = MavGameMode.FreeFlight, kind = MavFlightLaunchKind.Player, attempt = 1
            });
            SceneManager.LoadScene(MavSceneNames.InGame);
            yield return WaitForState(MavFlightSessionState.Failed, 10f);
            director = MavFlightSessionDirector.Active;
            yield return Frames(5);
            MavPlayerSceneCensus c = MavPlayerSceneCensus.Take();
            Check(director != null && director.State == MavFlightSessionState.Failed && director.failureTitle == "AIRCRAFT NOT AVAILABLE"
                  && director.Aircraft == null,
                "X", "a forced F-16 launch is refused in the flight scene: " + (director != null ? director.failureTitle + " - " + director.failureReason : "no director"));
            Check(c.pilotRigs == 0 && c.armedBodies == 0 && c.activeLegacyPlayers == 0 && c.playerInputs == 0 && MavGameSession.SelectedAircraft == MavAircraftKind.F16C,
                "X", "nothing was spawned in its place - no F-15, no legacy aircraft - and the selection is still the F-16 (" + c.Describe() + ")");

            if (director != null)
                director.ReturnToHangar();
            float deadline = Time.realtimeSinceStartup + 10f;
            while (Object.FindAnyObjectByType<MavHangarBootstrap>() == null && Time.realtimeSinceStartup < deadline)
                yield return null;
            yield return Frames(2);
        }

        // ------------------------------------------------------------------ [D]

        private IEnumerator Development()
        {
            Section("[D] Development launch (legacy flight model)");
            string reason;
            bool ok = MavFlightLauncher.TryDevelopmentLaunch(MavAircraftKind.F22A, out reason);
            float deadline = Time.realtimeSinceStartup + 10f;
            while ((MavFlightSessionDirector.Active == null || MavFlightSessionDirector.Active.State != MavFlightSessionState.Playing)
                   && Time.realtimeSinceStartup < deadline)
                yield return null;
            yield return Frames(30);
            director = MavFlightSessionDirector.Active;
            MavPlayerSceneCensus c = MavPlayerSceneCensus.Take();
            Check(ok && director != null && director.legacyDevelopmentFlight && director.State == MavFlightSessionState.Playing && c.pilotRigs == 0
                  && c.activeLegacyPlayers == 1 && c.flightDirectors == 1 && c.enabledAudioListeners == 1,
                "D", "the legacy stack flies alone, labelled development: one legacy aircraft, no pilot-controlled rig (" + c.Describe() + ")");
            if (director != null)
                director.ReturnToHangar();
            deadline = Time.realtimeSinceStartup + 10f;
            while (Object.FindAnyObjectByType<MavHangarBootstrap>() == null && Time.realtimeSinceStartup < deadline)
                yield return null;
            yield return Frames(2);
        }

        // ------------------------------------------------------------------ helpers

        private bool Alive()
        {
            return director != null && director.Aircraft != null && rig != null && director.State == MavFlightSessionState.Playing;
        }

        private static float Rate(MavSixDoFBody body, int axis)
        {
            Vector3 w = body.debugState.aeroBodyRatesRadSec;
            return axis == 0 ? w.y : axis == 1 ? w.x : w.z;
        }

        private void Track(MavSixDoFBody body, ref bool finite, ref bool owner)
        {
            Vector3 w = body.debugState.aeroBodyRatesRadSec;
            finite &= IsFinite(w.x) && IsFinite(w.y) && IsFinite(w.z) && IsFinite(body.debugState.trueAirspeedMps) && body.debugLoadSet.IsFinite();
            owner &= rig != null && rig.ownership != null && rig.ownership.owner == MavFlightPhysicsOwner.F15PilotControlledResearch && body.ArmedForLiveFlight;
        }

        private static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++)
                yield return null;
        }

        private static IEnumerator WaitForState(MavFlightSessionState target, float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                MavFlightSessionDirector d = MavFlightSessionDirector.Active;
                if (d != null && (d.State == target || d.State == MavFlightSessionState.Failed))
                    yield break;
                yield return null;
            }
        }

        private static bool IsFinite(float v)
        {
            return !float.IsNaN(v) && !float.IsInfinity(v);
        }

        private static void RestorePref(string key, int value)
        {
            if (value < 0) PlayerPrefs.DeleteKey(key);
            else PlayerPrefs.SetInt(key, value);
        }

        private void Section(string title)
        {
            sb.AppendLine();
            sb.AppendLine(title);
        }

        private void Check(bool ok, string tag, string label)
        {
            if (ok) Passed++;
            else Failed++;
            sb.Append(ok ? "  PASS  " : "  FAIL  ").Append(tag).Append("  ").AppendLine(label);
        }
    }
}
#endif
