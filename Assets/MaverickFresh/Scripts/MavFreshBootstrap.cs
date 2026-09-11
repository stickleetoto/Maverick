using UnityEngine;

namespace MaverickFresh
{
    /// <summary>
    /// Fresh standalone bootstrap.
    /// Put this on an empty object, assign a generic Mav_Player and Main Camera, then press Play.
    /// This deliberately disables old MAVERICK / EaglePhysicalAI components on the aircraft and camera.
    /// </summary>
    public class MavFreshBootstrap : MonoBehaviour
    {
        [Header("Required")]
        public GameObject aircraftObject;
        public Camera mainCamera;

        [Header("Generic Player Root")]
        public string genericPlayerName = "Mav_Player";
        public bool createGenericPlayerIfMissing = false;
        public bool applyLegacyF15Profile = false;

        [Header("Aircraft Identity")]
        [Tooltip("Applies the aircraft the player actually selected (MavGameSession) to the player object before anything aircraft-aware runs. Turn this off only if a higher-level bootstrap owns aircraft application - it already skips when one has.")]
        public bool applySelectedAircraft = true;

        [Tooltip("What happened on the last aircraft identity pass.")]
        [TextArea(2, 4)] public string aircraftIdentityStatus = "not run";

        [Tooltip("Install the Phase 4B turn-dynamics diagnostic on the player. It is read-only - it applies no force, torque or correction - and exists so turn curvature produced by aerodynamics can be told apart from curvature produced by a legacy assist.")]
        public bool installTurnDynamicsDiagnostics = true;
        public MavTurnDynamicsDiagnostics turnDiagnostics;

        [Header("Fresh Mode")]
        public bool setupOnAwake = true;
        public bool disableOldMaverickComponents = true;
        public bool disableOldCameras = true;
        public bool disableWheelAndLanding = true;
        public bool forceGravityOff = true;
        public bool installPhysicalAIStarter = true;
        public bool installCASStarter = true;
        public bool installWTPolish = true;
        public bool installStateSanityPatch = true;
        public bool installTGPStateManager = true;
        public bool installMountValidator = true;
        public bool installStandaloneControlDebugOverlay = false;
        public bool preserveExistingRadarSystems = true;
        public bool forceRadarOffOnStart = true;
        public bool hideLegacyRadarHudOnStart = true;

        [Header("Air Start")]
        public bool applyAirStart = true;
        public float startAltitude = 1200f;
        public float startSpeed = 260f;
        public Vector3 startEulerAngles = Vector3.zero;

        [Header("Fresh Tuning")]
        public float jetThrust = 220f;
        public Vector3 jetTurnTorque = new Vector3(38f, 16f, 52f);
        public float jetForceMult = 1000f;
        public float mouseSensitivity = 3f;
        public float aimDistance = 600f;
        public MavAimInputMode aimInputMode = MavAimInputMode.CursorPosition;

        [Header("Created")]
        public MavMouseFlightRig rig;
        public MavMouseFlightJet jet;
        public MavInstructorController instructor;
        public MavFreshHud hud;
        public MavFreshControlProfile profile;
        public MavF15EFlightSpecProfile f15eProfile;
        public MavPhysicalAIStarterBootstrap physicalAIStarter;
        public MavPhysicalAIScenarioBootstrap physicalAIScenario;
        public MavCASStarterBootstrap casStarter;
        public MavWTFeelPolishController wtPolish;
        public MavWTQuickHelpOverlay quickHelp;
        public MavTGPStateManager tgpStateManager;
        public MavAircraftMountValidator mountValidator;
        public MavControlDebugOverlay controlDebugOverlay;

        private void Awake()
        {
            if (setupOnAwake)
                SetupFreshMouseFlight();
        }

        [ContextMenu("Setup Fresh MouseFlight")]
        public void SetupFreshMouseFlight()
        {
            // Resolve the aircraft player object generically so non-F-15 aircraft work too.
            if (aircraftObject == null && !string.IsNullOrEmpty(genericPlayerName))
                aircraftObject = GameObject.Find(genericPlayerName);

            if (aircraftObject == null)
                aircraftObject = FindObjectOfType<MavMouseFlightJet>()?.gameObject;

            if (aircraftObject == null && createGenericPlayerIfMissing)
            {
                aircraftObject = new GameObject(string.IsNullOrEmpty(genericPlayerName) ? "Mav_Player" : genericPlayerName);
                aircraftObject.transform.position = new Vector3(0f, startAltitude, 0f);
                aircraftObject.transform.rotation = Quaternion.Euler(startEulerAngles);
            }

            if (mainCamera == null)
                mainCamera = Camera.main;

            if (aircraftObject == null)
            {
                Debug.LogWarning("MavFreshBootstrap: aircraftObject missing.");
                return;
            }

            if (disableOldMaverickComponents)
                DisableOldComponents();

            if (disableWheelAndLanding)
                DisableWheelAndLandingSystems();

            Rigidbody rb = aircraftObject.GetComponent<Rigidbody>();
            if (rb == null)
                rb = aircraftObject.AddComponent<Rigidbody>();

            rb.useGravity = !forceGravityOff;
            rb.mass = 12000f;
            rb.linearDamping = 0.015f;
            rb.angularDamping = 1.2f;
            rb.maxAngularVelocity = Mathf.Max(rb.maxAngularVelocity, 8f);
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            GameObject rigGo = GameObject.Find("MaverickFresh_MouseFlightRig");
            if (rigGo == null)
                rigGo = new GameObject("MaverickFresh_MouseFlightRig");

            rig = rigGo.GetComponent<MavMouseFlightRig>();
            if (rig == null)
                rig = rigGo.AddComponent<MavMouseFlightRig>();

            rig.aircraft = aircraftObject.transform;
            rig.playerCamera = mainCamera;
            rig.mouseSensitivity = mouseSensitivity;
            rig.aimDistance = aimDistance;
            rig.aimInputMode = aimInputMode;
            rig.showCursorInCursorMode = true;
            rig.cameraLooksAtAimWithoutFreeLook = false;

            jet = aircraftObject.GetComponent<MavMouseFlightJet>();
            if (jet == null)
                jet = aircraftObject.AddComponent<MavMouseFlightJet>();

            if (aircraftObject.GetComponent<MavAeroBody>() == null)
                aircraftObject.AddComponent<MavAeroBody>();

            jet.controller = rig;
            jet.thrust = jetThrust;
            jet.turnTorque = jetTurnTorque;
            jet.useAccelerationTorqueMode = true;
            jet.accelerationModeTorque = jetTurnTorque;
            jet.forceModeTorque = new Vector3(6200f, 3200f, 8200f);
            jet.maxAppliedTorqueAccelerationMode = new Vector3(80f, 34f, 105f);
            jet.maxAppliedTorqueForceMode = new Vector3(9000f, 4200f, 10500f);
            jet.maxAppliedTorque = jet.maxAppliedTorqueAccelerationMode;
            jet.angularDamping = 1.2f;
            jet.maxAngularVelocity = 8f;
            jet.controlSurfaceResponse = 16f;
            jet.controlSurfaceReleaseResponse = 9f;
            jet.maxPitchCommandRate = 11f;
            jet.maxYawCommandRate = 5.5f;
            jet.maxRollCommandRate = 13f;
            jet.manualDampingReduction = 0.45f;
            jet.manualEnvelopeBypassFactor = 0.65f;
            jet.manualPitchMinAuthority = 0.78f;
            jet.manualRollMinAuthority = 0.82f;
            jet.manualYawMinAuthority = 0.60f;
            jet.useDirectManualTorqueAssist = true;
            jet.forceMult = jetForceMult;
            jet.gravityOff = forceGravityOff;

            instructor = aircraftObject.GetComponent<MavInstructorController>();
            if (instructor == null)
                instructor = aircraftObject.AddComponent<MavInstructorController>();

            instructor.rig = rig;
            instructor.jet = jet;
            instructor.rb = rb;
            jet.instructor = instructor;
            jet.PushLegacyTuningToInstructor();

            if (mainCamera != null)
            {
                hud = mainCamera.GetComponent<MavFreshHud>();
                if (hud == null)
                    hud = mainCamera.gameObject.AddComponent<MavFreshHud>();

                hud.mouseFlight = rig;
                hud.jet = jet;
                hud.playerCam = mainCamera;
            }

            profile = aircraftObject.GetComponent<MavFreshControlProfile>();
            if (profile == null)
                profile = aircraftObject.AddComponent<MavFreshControlProfile>();

            profile.jet = jet;
            profile.rig = rig;
            profile.instructor = instructor;
            profile.preset = MavFreshFlightPreset.WarThunderMouseAim;
            profile.ApplyPreset(profile.preset);

            f15eProfile = aircraftObject.GetComponent<MavF15EFlightSpecProfile>();
            if (applyLegacyF15Profile)
            {
                if (f15eProfile == null)
                    f15eProfile = aircraftObject.AddComponent<MavF15EFlightSpecProfile>();

                f15eProfile.jet = jet;
                f15eProfile.rig = rig;
                f15eProfile.instructor = instructor;
                f15eProfile.rb = rb;
                f15eProfile.applyOnStart = false;
                f15eProfile.ApplyF15EProfile();
            }
            else if (f15eProfile != null)
            {
                // The generic multi-aircraft player must not be forced back into F-15 tuning.
                f15eProfile.applyOnStart = false;
            }

            if (disableOldCameras)
                DisableNonMainScreenCameras();

            if (applyAirStart)
                ApplyAirStart(rb);

            if (installCASStarter)
            {
                casStarter = GetComponent<MavCASStarterBootstrap>();
                if (casStarter == null)
                    casStarter = gameObject.AddComponent<MavCASStarterBootstrap>();

                casStarter.aircraftObject = aircraftObject;
                casStarter.Setup();
            }

            if (installPhysicalAIStarter)
            {
                physicalAIStarter = GetComponent<MavPhysicalAIStarterBootstrap>();
                if (physicalAIStarter == null)
                    physicalAIStarter = gameObject.AddComponent<MavPhysicalAIStarterBootstrap>();

                physicalAIStarter.aircraftObject = aircraftObject;
                physicalAIStarter.Setup();
            }

            // ---- AUTHORITATIVE AIRCRAFT IDENTITY ---------------------------------------------
            // This must run before the WT polish block below, because that block is aircraft-aware.
            //
            // MavInGameBootstrap also applies the selection, and does so before calling this method -
            // but it is not present in every scene. Mav_InGame contains only this bootstrap, the
            // applier, the visual switcher and WT polish, so with the application living solely in
            // MavInGameBootstrap the selection reached the player in that scene through nothing at
            // all. What used to hide that was WT polish reading the applier's serialized default and
            // applying the F-22; once that stopped, the aircraft was simply never applied, and
            // Debug Applied Aircraft stayed "none".
            //
            // So the startup component that actually runs establishes identity. It defers to a
            // higher-level bootstrap that has already done so rather than competing with it.
            EnsureAuthoritativeAircraftApplied();

            // Read-only observer. Installed after the aircraft is applied so it samples the real
            // configuration, and it writes nothing back to the jet or the aero body.
            if (installTurnDynamicsDiagnostics && aircraftObject != null)
            {
                turnDiagnostics = aircraftObject.GetComponent<MavTurnDynamicsDiagnostics>();
                if (turnDiagnostics == null)
                    turnDiagnostics = aircraftObject.AddComponent<MavTurnDynamicsDiagnostics>();
            }

            if (installWTPolish)
            {
                wtPolish = aircraftObject.GetComponent<MavWTFeelPolishController>();
                if (wtPolish == null)
                    wtPolish = aircraftObject.AddComponent<MavWTFeelPolishController>();

                wtPolish.jet = jet;
                wtPolish.instructor = instructor;
                wtPolish.rig = rig;
                wtPolish.targetingPod = aircraftObject.GetComponent<MavTargetingPodSystem>();
                wtPolish.weapons = aircraftObject.GetComponent<MavCASWeaponSystem>();
                // v0.19+: keep hotkeys alive, but do not let Start re-apply the F-15 preset after aircraft selection.
                wtPolish.applyOnStart = false;
                wtPolish.ApplyPreset(MavWTFeelPreset.WarThunderF15Balanced);

                quickHelp = aircraftObject.GetComponent<MavWTQuickHelpOverlay>();
                if (quickHelp == null)
                    quickHelp = aircraftObject.AddComponent<MavWTQuickHelpOverlay>();
            }

            SetupStateSanityPatch();
            ConfigureRadarStartupState();
            rig.CenterAim();
        }

        private void SetupStateSanityPatch()
        {
            if (!installStateSanityPatch || aircraftObject == null)
                return;

            if (installTGPStateManager)
            {
                tgpStateManager = aircraftObject.GetComponent<MavTGPStateManager>();
                if (tgpStateManager == null)
                    tgpStateManager = aircraftObject.AddComponent<MavTGPStateManager>();

                tgpStateManager.targetingPod = aircraftObject.GetComponent<MavTargetingPodSystem>();
                tgpStateManager.forceOffOnStart = true;
                tgpStateManager.startHidden = true;
                tgpStateManager.SetOff();
            }

            if (installMountValidator)
            {
                mountValidator = aircraftObject.GetComponent<MavAircraftMountValidator>();
                if (mountValidator == null)
                    mountValidator = aircraftObject.AddComponent<MavAircraftMountValidator>();

                mountValidator.ordnanceAssets = aircraftObject.GetComponent<MavCASOrdnanceAssets>();
                mountValidator.targetingPod = aircraftObject.GetComponent<MavTargetingPodSystem>();
                mountValidator.validateOnStart = true;
            }

            if (installStandaloneControlDebugOverlay && mainCamera != null)
            {
                controlDebugOverlay = mainCamera.GetComponent<MavControlDebugOverlay>();
                if (controlDebugOverlay == null)
                    controlDebugOverlay = mainCamera.gameObject.AddComponent<MavControlDebugOverlay>();

                controlDebugOverlay.rig = rig;
                controlDebugOverlay.jet = jet;
                controlDebugOverlay.instructor = instructor;
                controlDebugOverlay.targetingPod = aircraftObject.GetComponent<MavTargetingPodSystem>();
            }
        }

        private void ApplyAirStart(Rigidbody rb)
        {
            Vector3 pos = aircraftObject.transform.position;
            pos.y = startAltitude;
            aircraftObject.transform.position = pos;
            aircraftObject.transform.rotation = Quaternion.Euler(startEulerAngles);

            rb.linearVelocity = aircraftObject.transform.forward * startSpeed;
            rb.angularVelocity = Vector3.zero;
        }

        /// <summary>
        /// Makes the player's aircraft identity authoritative from the SESSION SELECTION, if nothing
        /// else has already done it.
        ///
        /// The identity source is MavGameSession - what the player chose in the hangar. It is never
        /// the applier's serialized `aircraft` field: that field is a request with an F-22A default,
        /// and reading it during startup is the exact bug this replaces.
        ///
        /// Returns true when an aircraft is authoritative afterwards, whether this call established
        /// it or found it already established.
        /// </summary>
        public bool EnsureAuthoritativeAircraftApplied()
        {
            if (!applySelectedAircraft)
            {
                aircraftIdentityStatus = "disabled: applySelectedAircraft is off, so this bootstrap "
                                         + "did not apply an aircraft";
                return false;
            }

            if (aircraftObject == null)
            {
                aircraftIdentityStatus = "no player object to apply an aircraft to";
                return false;
            }

            MavAircraftProfileApplier applier =
                aircraftObject.GetComponent<MavAircraftProfileApplier>();
            if (applier == null)
                applier = aircraftObject.AddComponent<MavAircraftProfileApplier>();

            // Somebody with more context - MavInGameBootstrap, or a mode-specific bootstrap - has
            // already decided. That decision stands: this method re-states it, and never re-decides.
            //
            // Re-stating is not busywork here. SetupFreshMouseFlight has just written its own
            // generic startup values over the aircraft - rb.mass = 12000f, and a page of jet
            // defaults - which would otherwise silently outrank the aircraft that was applied before
            // it ran. Restoring the applied profile puts the aircraft's own numbers back on top.
            if (applier.HasAuthoritativeAircraft)
            {
                string refreshError;
                if (!applier.TryReapplyAppliedProfile(out refreshError))
                {
                    aircraftIdentityStatus = "could not restate the already-applied "
                                             + applier.AppliedAircraft + ": " + refreshError;
                    Debug.LogError("MavFreshBootstrap: " + aircraftIdentityStatus, this);
                    return false;
                }

                aircraftIdentityStatus = "restated " + applier.AppliedAircraft
                                         + ", which was applied elsewhere, over this bootstrap's "
                                         + "generic startup values";
                return true;
            }

            // The session is the authority. With no selection at all, the session's own declared
            // default applies - a deliberate, logged decision, and still not the applier's field.
            bool hasSelection = MavGameSession.HasSelection;
            MavAircraftKind selected = hasSelection
                ? MavGameSession.SelectedAircraft
                : MavGameSession.DefaultAircraft;

            MavAircraftRuntimeProfile profile;
            string error;
            if (!MavAircraftCatalog.TryGetBuiltIn(selected, out profile, out error))
            {
                aircraftIdentityStatus = "cannot resolve the selected aircraft: " + error;
                Debug.LogError("MavFreshBootstrap: " + aircraftIdentityStatus, this);
                return false;
            }

            applier.applyOnStart = false;
            applier.renameObject = false;

            if (!applier.TryApplyProfile(profile, out error))
            {
                aircraftIdentityStatus = "could not apply " + profile.displayName + ": " + error;
                Debug.LogError("MavFreshBootstrap: " + aircraftIdentityStatus, this);
                return false;
            }

            aircraftIdentityStatus = "applied " + profile.displayName + " from "
                                     + (hasSelection
                                        ? "the player's session selection"
                                        : "the session default, because nothing was selected");

            if (!hasSelection)
            {
                Debug.LogWarning(
                    "MavFreshBootstrap: no aircraft had been selected, so the session default "
                    + profile.displayName + " was applied. Enter through the hangar to choose one.",
                    this);
            }

            return true;
        }

        private void DisableOldComponents()
        {
            MonoBehaviour[] behaviours = aircraftObject.GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour mb in behaviours)
            {
                if (mb == null)
                    continue;

                string typeName = mb.GetType().FullName;

                if (typeName.StartsWith("MaverickFresh."))
                    continue;

                if (preserveExistingRadarSystems && IsRadarSafetyComponent(typeName))
                    continue;

                if (typeName.StartsWith("EaglePhysicalAI.") || typeName.Contains("Maverick") || typeName.Contains("Eagle"))
                    mb.enabled = false;
            }

            if (mainCamera != null)
            {
                foreach (MonoBehaviour mb in mainCamera.GetComponents<MonoBehaviour>())
                {
                    if (mb == null)
                        continue;

                    string typeName = mb.GetType().FullName;
                    if (typeName.StartsWith("MaverickFresh."))
                        continue;

                    if (preserveExistingRadarSystems && IsRadarSafetyComponent(typeName))
                        continue;

                    if (typeName.StartsWith("EaglePhysicalAI.") || typeName.Contains("Maverick") || typeName.Contains("WarThunder"))
                        mb.enabled = false;
                }
            }
        }

        private void DisableWheelAndLandingSystems()
        {
            foreach (WheelCollider wc in aircraftObject.GetComponentsInChildren<WheelCollider>(true))
                wc.enabled = false;

            foreach (Transform t in aircraftObject.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (n.Contains("landingon") || n.Contains("landing_on") || n.Contains("gear_down"))
                    t.gameObject.SetActive(false);
                if (n.Contains("landingoff") || n.Contains("landing_off") || n.Contains("gear_up"))
                    t.gameObject.SetActive(true);
            }
        }

        private void DisableNonMainScreenCameras()
        {
            foreach (Camera cam in FindObjectsOfType<Camera>())
            {
                if (cam == null || cam == mainCamera)
                    continue;

                if (cam.targetTexture != null)
                    continue;

                string n = cam.name.ToLowerInvariant();
                if (n.Contains("targeting") || n.Contains("pod") || n.Contains("tgp") || n.Contains("chase"))
                    cam.enabled = false;
            }

            if (mainCamera != null)
            {
                mainCamera.enabled = true;
                mainCamera.depth = 100;
            }
        }

        private void ConfigureRadarStartupState()
        {
            if (!preserveExistingRadarSystems || aircraftObject == null)
                return;

            MonoBehaviour[] behaviours = aircraftObject.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (MonoBehaviour mb in behaviours)
            {
                if (mb == null)
                    continue;

                string typeName = mb.GetType().FullName;
                if (!IsRadarSafetyComponent(typeName))
                    continue;

                mb.enabled = true;

                if (forceRadarOffOnStart)
                {
                    SetBoolField(mb, "radarOn", false);
                    SetBoolField(mb, "radarMasterOn", false);
                }

                if (hideLegacyRadarHudOnStart)
                {
                    SetBoolField(mb, "show", false);
                    SetBoolField(mb, "showRadarHud", false);
                }
            }
        }

        private bool IsRadarSafetyComponent(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return false;

            return typeName.Contains(".Sensors.Radar.") ||
                typeName.EndsWith(".Sensors.UI.RadarHud") ||
                typeName.Contains("RadarHotas");
        }

        private void SetBoolField(MonoBehaviour target, string fieldName, bool value)
        {
            if (target == null)
                return;

            System.Reflection.FieldInfo field = target.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
            );

            if (field != null && field.FieldType == typeof(bool))
                field.SetValue(target, value);
        }
    }
}
