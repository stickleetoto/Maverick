using UnityEngine;
using MaverickFresh.FlightDynamics;

namespace MaverickFresh.Gameplay
{
    public enum MavFlightSessionState
    {
        /// <summary>Aircraft being built and started. Player input is OFF.</summary>
        Initializing = 0,
        Playing = 1,
        Paused = 2,

        /// <summary>The flight cannot continue or never started. The reason is shown; physics stays fail-closed.</summary>
        Failed = 3,

        /// <summary>Leaving: restart or return in progress.</summary>
        Returning = 4
    }

    /// <summary>
    /// The flight session in Mav_InGame: the one spawn authority and the one state machine for a flight.
    ///
    /// It reads the launch request from <see cref="MavGameSession"/>, resolves the aircraft's
    /// <see cref="MavPlayableAircraftDefinition"/>, spawns it through <see cref="MavPlayableAircraftSpawner"/>, owns
    /// the one camera, the player HUD and the pause / restart / return loop, and says clearly when a flight
    /// cannot start or continue.
    ///
    /// It never touches aircraft physics. It reads the rig's start status and its ownership state, gates the
    /// player's input, and stops the clock (Time.timeScale) to pause - which halts every FixedUpdate at once, so
    /// the flight law, actuator, body and ownership all stop together and resume together.
    ///
    /// The legacy Mav_Player stack in this scene is saved INACTIVE. It is switched on only for an explicit
    /// development launch of a legacy aircraft, so a player flight never shares the scene with a second player
    /// aircraft, camera rig, input owner or physics owner.
    /// </summary>
    [DefaultExecutionOrder(-20000)]
    [DisallowMultipleComponent]
    public sealed class MavFlightSessionDirector : MonoBehaviour
    {
        public static MavFlightSessionDirector Active { get; private set; }

        [Header("Legacy development stack (saved inactive, activated in this order for a development launch)")]
        public GameObject[] legacyObjects = new GameObject[0];

        [Header("Session")]
        public float startTimeoutSeconds = 6f;
        public float groundImpactMarginM = 3f;
        public bool buildEnvironment = true;
        public KeyCode pauseKey = KeyCode.Escape;
        public KeyCode restartKeyWhilePaused = KeyCode.R;

        [Header("Runtime (read-only)")]
        public MavFlightSessionState state = MavFlightSessionState.Initializing;
        public MavFlightLaunchRequest request;
        public string failureTitle = "";
        public string failureReason = "";
        [TextArea(2, 6)] public string failureDetail = "";
        public bool physicsReady;
        public bool playerControlActive;
        public bool legacyDevelopmentFlight;
        public float stateEnteredUnscaledTime;

        public MavPlayableAircraftDefinition Definition { get; private set; }
        public MavSpawnedAircraft Aircraft { get; private set; }
        public MavFlightCameraController FlightCamera { get; private set; }
        public MavPlayerFlightHud Hud { get; private set; }
        public MavFlightDebugHud DebugHud { get; private set; }
        public MavFlightSessionUi Ui { get; private set; }

        private bool clockStoppedByUs;

        public MavFlightSessionState State
        {
            get { return state; }
        }

        public float SecondsInState
        {
            get { return Time.unscaledTime - stateEnteredUnscaledTime; }
        }

        private void Awake()
        {
            if (Active != null && Active != this)
            {
                Debug.LogError("[Maverick/Flight] A second flight session director was found and removed. Exactly one may exist.", this);
                Destroy(this);
                return;
            }

            Active = this;
            Time.timeScale = 1f;
            stateEnteredUnscaledTime = Time.unscaledTime;

            Ui = gameObject.AddComponent<MavFlightSessionUi>();
            Ui.director = this;
            DebugHud = gameObject.AddComponent<MavFlightDebugHud>();
            DebugHud.director = this;

            ResolveRequest();
            Camera cam = ResolveCamera();

            if (request.kind == MavFlightLaunchKind.DevelopmentLegacy)
            {
                StartLegacyDevelopment();
                return;
            }

            MavPlayableAircraftDefinition definition;
            string reason;
            if (!MavPlayableAircraftRegistry.CanLaunchAsPlayer(request.aircraft, out definition, out reason))
            {
                Definition = definition;
                Fail("AIRCRAFT NOT AVAILABLE", reason, definition != null ? definition.physicsPathDescription : "", false);
                return;
            }

            Definition = definition;
            if (buildEnvironment)
                MavFreeFlightEnvironment.Build();

            MavSpawnedAircraft spawned;
            if (!MavPlayableAircraftSpawner.TrySpawn(definition, out spawned, out reason))
            {
                Fail("AIRCRAFT START FAILED", reason, definition.physicsPathDescription, false);
                return;
            }

            Aircraft = spawned;
            FlightCamera = cam.GetComponent<MavFlightCameraController>();
            if (FlightCamera == null)
                FlightCamera = cam.gameObject.AddComponent<MavFlightCameraController>();
            FlightCamera.Bind(spawned.renderPose, spawned.body, definition.defaultCamera);

            Hud = gameObject.AddComponent<MavPlayerFlightHud>();
            Hud.director = this;

            Debug.Log("[Maverick/Flight] Launch " + request + ": " + definition.displayName + " (" + definition.physicsConfigurationId + ")", this);
            EnterState(MavFlightSessionState.Initializing);
        }

        private void OnDestroy()
        {
            if (Active == this)
                Active = null;
            if (clockStoppedByUs)
                Time.timeScale = 1f;
        }

        private void ResolveRequest()
        {
            if (MavGameSession.HasLaunchRequest)
            {
                request = MavGameSession.CurrentLaunch;
                return;
            }

            // No launch request: the scene was opened directly. Fly the player's selection - or, when nothing was
            // ever selected, the documented default. A selection that is not a player aircraft is NOT replaced;
            // it is refused below with its reason.
            request = new MavFlightLaunchRequest
            {
                aircraft = MavGameSession.HasSelection ? MavGameSession.SelectedAircraft : MavPlayableAircraftRegistry.DefaultPlayerAircraft,
                mode = MavGameMode.FreeFlight,
                kind = MavFlightLaunchKind.Player,
                attempt = 1
            };
            MavGameSession.SetLaunchRequest(request);
        }

        private static Camera ResolveCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                GameObject go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }

            cam.enabled = true;
            return cam;
        }

        // ------------------------------------------------------------------ legacy development launch

        private void StartLegacyDevelopment()
        {
            legacyDevelopmentFlight = true;
            MavPlayableAircraftDefinition definition;
            if (!MavPlayableAircraftRegistry.TryGetDevelopment(request.aircraft, out definition))
            {
                Fail("AIRCRAFT NOT AVAILABLE", request.aircraft + " has no development launch.", "", false);
                return;
            }

            Definition = definition;
            if (legacyObjects == null || legacyObjects.Length == 0)
            {
                Fail("AIRCRAFT START FAILED", "The legacy development stack is not wired in this scene.", "", false);
                return;
            }

            // In the saved scene order: the legacy bootstrap finds Mav_Player and the mouse-flight rig when it wakes.
            for (int i = 0; i < legacyObjects.Length; i++)
            {
                if (legacyObjects[i] != null)
                    legacyObjects[i].SetActive(true);
            }

            physicsReady = true;
            playerControlActive = true;
            Debug.Log("[Maverick/Flight] DEVELOPMENT launch " + request + " on the LEGACY flight model.", this);
            EnterState(MavFlightSessionState.Playing);
        }

        // ------------------------------------------------------------------ state machine

        private void Update()
        {
            switch (state)
            {
                case MavFlightSessionState.Initializing:
                    UpdateInitializing();
                    break;
                case MavFlightSessionState.Playing:
                    if (MavFreshInput.GetKeyDown(pauseKey))
                        Pause();
                    else
                        MonitorFlight();
                    break;
                case MavFlightSessionState.Paused:
                    if (MavFreshInput.GetKeyDown(pauseKey))
                        Resume();
                    else if (MavFreshInput.GetKeyDown(restartKeyWhilePaused))
                        RestartFlight();
                    break;
            }
        }

        private void UpdateInitializing()
        {
            if (Aircraft == null || Aircraft.rig == null)
                return;

            MavF15StartProbe probe = MavF15StartProbe.Read(Aircraft);
            if (probe.started)
            {
                if (probe.succeeded)
                {
                    physicsReady = true;
                    Aircraft.renderPose.Snap();
                    SetPlayerControl(true);
                    Debug.Log("[Maverick/Flight] " + Definition.displayName + " started: " + probe.status, this);
                    EnterState(MavFlightSessionState.Playing);
                }
                else
                {
                    Fail("AIRCRAFT START FAILED", probe.status, probe.ownerDetail, false);
                }

                return;
            }

            if (SecondsInState > startTimeoutSeconds)
                Fail("AIRCRAFT START FAILED", "The aircraft did not start within " + startTimeoutSeconds + " s.", probe.status, false);
        }

        /// <summary>Stops the flight when its physics stops being valid, instead of flying on silently.</summary>
        private void MonitorFlight()
        {
            if (legacyDevelopmentFlight || Aircraft == null || Aircraft.body == null)
                return;

            MavF15StartProbe probe = MavF15StartProbe.Read(Aircraft);
            if (!probe.ownerHeld)
            {
                Fail("FLIGHT MODEL STOPPED", "The flight model left its validated operating state and stopped applying loads.", probe.ownerDetail, true);
                return;
            }

            Vector3 p = Aircraft.rig.transform.position;
            if (!(p.y > MavFreeFlightEnvironment.GroundHeightM + groundImpactMarginM))
            {
                Fail("GROUND IMPACT", "The aircraft hit the ground.", "altitude " + p.y.ToString("F1") + " m", true);
            }
        }

        public void Pause()
        {
            if (state != MavFlightSessionState.Playing)
                return;
            SetPlayerControl(false);
            StopClock();
            EnterState(MavFlightSessionState.Paused);
        }

        public void Resume()
        {
            if (state != MavFlightSessionState.Paused)
                return;
            StartClock();
            SetPlayerControl(true);
            EnterState(MavFlightSessionState.Playing);
        }

        public void RestartFlight()
        {
            if (state == MavFlightSessionState.Returning)
                return;
            SetPlayerControl(false);
            EnterState(MavFlightSessionState.Returning);
            string reason;
            MavFlightLauncher.Restart(request, out reason);
        }

        public void ReturnToHangar()
        {
            if (state == MavFlightSessionState.Returning)
                return;
            SetPlayerControl(false);
            EnterState(MavFlightSessionState.Returning);
            string reason;
            MavFlightLauncher.ReturnToHangar(out reason);
        }

        private void Fail(string title, string reason, string detail, bool stopClock)
        {
            failureTitle = title;
            failureReason = reason;
            failureDetail = detail ?? "";
            SetPlayerControl(false);
            if (stopClock)
                StopClock();
            Debug.LogWarning("[Maverick/Flight] " + title + ": " + reason + (string.IsNullOrEmpty(failureDetail) ? "" : " | " + failureDetail), this);
            EnterState(MavFlightSessionState.Failed);
        }

        private void SetPlayerControl(bool active)
        {
            playerControlActive = active;
            if (Aircraft != null && Aircraft.input != null)
                Aircraft.input.SetInputEnabled(active);
            if (FlightCamera != null)
                FlightCamera.acceptInput = active;

            if (legacyDevelopmentFlight)
                return;
            Cursor.lockState = active ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !active;
        }

        private void StopClock()
        {
            Time.timeScale = 0f;
            clockStoppedByUs = true;
        }

        private void StartClock()
        {
            Time.timeScale = 1f;
            clockStoppedByUs = false;
        }

        private void EnterState(MavFlightSessionState next)
        {
            state = next;
            stateEnteredUnscaledTime = Time.unscaledTime;
            if (next == MavFlightSessionState.Paused || next == MavFlightSessionState.Failed)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }

    /// <summary>What the session may read about a spawned F-15's start and ownership. Reads only.</summary>
    public struct MavF15StartProbe
    {
        public bool started;
        public bool succeeded;
        public bool ownerHeld;
        public string status;
        public string ownerDetail;

        public static MavF15StartProbe Read(MavSpawnedAircraft aircraft)
        {
            MavF15StartProbe p = new MavF15StartProbe();
            if (aircraft == null || aircraft.rig == null)
            {
                p.status = "no aircraft";
                return p;
            }

            p.started = aircraft.rig.debugStarted;
            p.status = aircraft.rig.debugStartStatus;
            MavFlightPhysicsOwnership gate = aircraft.rig.ownership;
            p.ownerHeld = gate != null && gate.owner == MavFlightPhysicsOwner.F15PilotControlledResearch
                          && aircraft.body != null && aircraft.body.ArmedForLiveFlight;
            p.succeeded = p.started && aircraft.rig.debugStartSucceeded && p.ownerHeld;
            p.ownerDetail = gate != null ? "owner " + gate.owner + ": " + gate.ownerReason : "no ownership authority";
            return p;
        }
    }
}
