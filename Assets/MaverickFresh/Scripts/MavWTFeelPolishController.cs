using UnityEngine;

namespace MaverickFresh
{
    public enum MavWTFeelPreset
    {
        WarThunderF15Balanced = 0,
        WarThunderF15Smooth = 1,
        WarThunderF15Aggressive = 2,
        DirectDebug = 3
    }

    /// <summary>
    /// v0.15 War-Thunder-like feel preset controller.
    /// F5 = balanced, F6 = smooth, F7 = aggressive, F8 = direct/debug.
    /// </summary>
    [DisallowMultipleComponent]
    public class MavWTFeelPolishController : MonoBehaviour
    {
        [Header("References")]
        public MavMouseFlightJet jet;
        public MavInstructorController instructor;
        public MavMouseFlightRig rig;
        public MavTargetingPodSystem targetingPod;
        public MavCASWeaponSystem weapons;

        [Header("Preset")]
        public MavWTFeelPreset preset = MavWTFeelPreset.WarThunderF15Balanced;
        public bool applyOnStart = true;

        [Header("Hotkeys")]
        public KeyCode balancedKey = KeyCode.F5;
        public KeyCode smoothKey = KeyCode.F6;
        public KeyCode aggressiveKey = KeyCode.F7;
        public KeyCode debugKey = KeyCode.F8;

        [Header("Runtime")]
        public string lastApplied = "none";

        /// <summary>
        /// Latches the one-time "no aircraft yet" warning. The hotkeys re-apply presets freely, and a
        /// warning on every F5 press would bury the one that matters.
        /// </summary>
        private bool loggedMissingAircraftAuthority;

        private void Awake() { Resolve(); }

        private void Start()
        {
            if (applyOnStart)
                ApplyPreset(preset);
        }

        private void Update()
        {
            if (MavFreshInput.GetKeyDown(balancedKey)) ApplyPreset(MavWTFeelPreset.WarThunderF15Balanced);
            if (MavFreshInput.GetKeyDown(smoothKey)) ApplyPreset(MavWTFeelPreset.WarThunderF15Smooth);
            if (MavFreshInput.GetKeyDown(aggressiveKey)) ApplyPreset(MavWTFeelPreset.WarThunderF15Aggressive);
            if (MavFreshInput.GetKeyDown(debugKey)) ApplyPreset(MavWTFeelPreset.DirectDebug);
        }

        public void Resolve()
        {
            if (jet == null) jet = GetComponent<MavMouseFlightJet>();
            if (instructor == null && jet != null) instructor = jet.GetComponent<MavInstructorController>();
            if (instructor == null) instructor = GetComponent<MavInstructorController>();
            if (instructor == null && jet != null) instructor = jet.gameObject.AddComponent<MavInstructorController>();
            if (rig == null) rig = FindObjectOfType<MavMouseFlightRig>();
            if (instructor != null)
            {
                instructor.jet = jet;
                instructor.rig = rig;
                instructor.rb = GetComponent<Rigidbody>();
            }
            if (targetingPod == null) targetingPod = GetComponent<MavTargetingPodSystem>();
            if (weapons == null) weapons = GetComponent<MavCASWeaponSystem>();
        }

        [ContextMenu("Apply Current Preset")]
        public void ApplyCurrentPreset() { ApplyPreset(preset); }

        public void ApplyPreset(MavWTFeelPreset p)
        {
            preset = p;
            Resolve();
            if (jet == null) return;

            if (TryApplyAircraftAwarePreset(p))
                return;

            if (p == MavWTFeelPreset.WarThunderF15Balanced)
            {
                ApplyCommonWT();
                ApplyThrottleAxis(95f, 45f, 1.8f, 2.4f, 1.35f);
                jet.thrust = 220f;
                jet.turnTorque = new Vector3(20f, 9f, 26f);
                jet.accelerationModeTorque = new Vector3(20f, 9f, 26f);
                jet.forceModeTorque = new Vector3(6200f, 3200f, 8200f);
                jet.maxAppliedTorqueAccelerationMode = new Vector3(42f, 18f, 52f);
                jet.maxAppliedTorqueForceMode = new Vector3(9000f, 4200f, 10500f);
                jet.maxAppliedTorque = jet.maxAppliedTorqueAccelerationMode;
                jet.pitchGain = 0.82f;
                jet.rollGain = 1.25f;
                jet.yawGain = 0.24f;
                jet.maxAutoPitch = 0.68f;
                jet.noseDownTrim = 0.115f;
                jet.inputSmoothing = 5.8f;
                jet.pitchUpCommand = -2.85f;
                jet.pitchDownCommand = 2.65f;
                jet.manualPitchBoost = 2.75f;
                jet.maxScreenRollBankAngle = 72f;
                jet.bankHoldProportional = 0.048f;
                jet.bankHoldRollRateDamping = 0.24f;
                jet.keyboardElevatorResponse = 34f;
                jet.keyboardElevatorReleaseBlend = 8.5f;
                jet.keyboardElevatorRateDamping = 0.045f;
                jet.rollCommandDeadzone = 0.025f;
                jet.rollCommandSlewRate = 12f;
                jet.angularDamping = 1.75f;
                jet.maxAngularVelocity = 6f;
                jet.controlSurfaceResponse = 7.5f;
                jet.controlSurfaceReleaseResponse = 6.5f;
                jet.maxPitchCommandRate = 4.8f;
                jet.maxYawCommandRate = 2.8f;
                jet.maxRollCommandRate = 5.8f;
                jet.useDirectManualTorqueAssist = false;
                jet.directManualPitchAssist = 9f;
                jet.directManualRollAssist = 12f;
                jet.directManualYawAssist = 3.5f;
                jet.velocityTurnAssistStrength = 0.018f;
                jet.velocityTurnAssistMaxAccel = 14f;
                jet.velocityTurnAssistInputFactor = 0.58f;
                jet.sideSlipDamping = 0.12f;
                jet.sideSlipDampingHighAoS = 0.24f;
                jet.aoaDragStrength = 0.018f;
                jet.aosDragStrength = 0.026f;
                jet.highSpeedTurnDragStrength = 0.014f;
                jet.angularRateDampingPitch = 0.09f;
                jet.angularRateDampingYaw = 0.12f;
                jet.angularRateDampingRoll = 0.075f;
                jet.mousePitchDeadzoneY = 0.020f;
                jet.centerPitchLevelStrength = 0.08f;
                jet.noseHighPitchDownAssist = 0.10f;
                jet.enableCoordinatedYawAssist = true;
                jet.aosYawAssistStrength = 0.060f;
                jet.maxAutoRudderAssist = 0.55f;
                jet.maxThrottle = 1.40f;
                jet.targetCruiseSpeed = 315f;
                jet.minCombatSpeed = 135f;
                jet.maxCombatSpeed = 530f;
                jet.lowSpeedThrustBoost = 0.55f;
                jet.overspeedDrag = 0.055f;
                jet.speedAssistStrength = 0.22f;
                ApplyRateBasedControl(
                    48f,
                    18f,
                    72f,
                    new Vector3(0.42f, 0.28f, 0.38f),
                    new Vector3(0.10f, 0.12f, 0.09f),
                    new Vector3(32f, 14f, 40f),
                    false,
                    0f,
                    0f,
                    0f
                );
                jet.highSpeedTurnDragStrength = 0.018f;
                jet.sideSlipDamping = 0.14f;
                jet.sideSlipDampingHighAoS = 0.30f;
                if (rig != null)
                {
                    rig.cameraRollFollowStrength = 0.28f;
                    rig.cameraLocalPosition = new Vector3(0f, 5.3f, -17.2f);
                    rig.cameraFov = 68f;
                    rig.zoomFov = 38f;
                    rig.useMouseAimCameraFollow = true;
                    rig.mouseAimCameraFollowStrength = 0.55f;
                    rig.mouseAimCameraFollowSmooth = 12.0f;
                    rig.mouseAimCameraMaxAngle = 36f;
                }
            }
            else if (p == MavWTFeelPreset.WarThunderF15Smooth)
            {
                ApplyCommonWT();
                ApplyThrottleAxis(90f, 35f, 1.3f, 1.8f, 1.25f);
                jet.thrust = 210f;
                jet.turnTorque = new Vector3(16f, 7f, 21f);
                jet.accelerationModeTorque = new Vector3(16f, 7f, 21f);
                jet.forceModeTorque = new Vector3(5400f, 2800f, 7000f);
                jet.maxAppliedTorqueAccelerationMode = new Vector3(34f, 14f, 42f);
                jet.maxAppliedTorqueForceMode = new Vector3(8200f, 3800f, 9200f);
                jet.maxAppliedTorque = jet.maxAppliedTorqueAccelerationMode;
                jet.angularDamping = 2.0f;
                jet.maxAngularVelocity = 5.2f;
                jet.pitchGain = 0.66f;
                jet.rollGain = 1.08f;
                jet.yawGain = 0.18f;
                jet.maxAutoPitch = 0.54f;
                jet.noseDownTrim = 0.14f;
                jet.inputSmoothing = 8.5f;
                jet.pitchUpCommand = -2.45f;
                jet.pitchDownCommand = 2.25f;
                jet.manualPitchBoost = 2.35f;
                jet.maxScreenRollBankAngle = 62f;
                jet.bankHoldProportional = 0.038f;
                jet.bankHoldRollRateDamping = 0.29f;
                jet.keyboardElevatorResponse = 26f;
                jet.keyboardElevatorReleaseBlend = 7.5f;
                jet.keyboardElevatorRateDamping = 0.05f;
                jet.rollCommandDeadzone = 0.035f;
                jet.rollCommandSlewRate = 9f;
                jet.controlSurfaceResponse = 5.8f;
                jet.controlSurfaceReleaseResponse = 5.5f;
                jet.finalTorqueSmoothing = 9.0f;
                jet.maxPitchCommandRate = 3.8f;
                jet.maxYawCommandRate = 2.2f;
                jet.maxRollCommandRate = 4.6f;
                jet.useDirectManualTorqueAssist = false;
                jet.directManualPitchAssist = 5f;
                jet.directManualRollAssist = 7f;
                jet.directManualYawAssist = 2f;
                jet.velocityTurnAssistStrength = 0.032f;
                jet.velocityTurnAssistMaxAccel = 11f;
                jet.velocityTurnAssistInputFactor = 0.58f;
                jet.sideSlipDamping = 0.14f;
                jet.sideSlipDampingHighAoS = 0.28f;
                jet.highSpeedTurnDragStrength = 0.016f;
                jet.angularRateDampingPitch = 0.12f;
                jet.angularRateDampingYaw = 0.15f;
                jet.angularRateDampingRoll = 0.095f;
                jet.mousePitchDeadzoneY = 0.035f;
                jet.centerPitchLevelStrength = 0.14f;
                jet.noseHighPitchDownAssist = 0.24f;
                jet.enableCoordinatedYawAssist = true;
                jet.aosYawAssistStrength = 0.045f;
                jet.maxAutoRudderAssist = 0.38f;
                jet.maxThrottle = 1.32f;
                jet.targetCruiseSpeed = 285f;
                jet.minCombatSpeed = 135f;
                jet.maxCombatSpeed = 470f;
                jet.lowSpeedThrustBoost = 0.48f;
                ApplyRateBasedControl(
                    36f,
                    14f,
                    55f,
                    new Vector3(0.34f, 0.23f, 0.30f),
                    new Vector3(0.13f, 0.15f, 0.12f),
                    new Vector3(24f, 10f, 30f),
                    false,
                    0f,
                    0f,
                    0f
                );
                if (rig != null)
                {
                    rig.cameraRollFollowStrength = 0.20f;
                    rig.cameraLocalPosition = new Vector3(0f, 5.6f, -18.8f);
                    rig.cameraFov = 62f;
                    rig.zoomFov = 40f;
                    rig.useMouseAimCameraFollow = true;
                    rig.mouseAimCameraFollowStrength = 0.38f;
                    rig.mouseAimCameraFollowSmooth = 9.0f;
                    rig.mouseAimCameraMaxAngle = 26f;
                }
            }
            else if (p == MavWTFeelPreset.WarThunderF15Aggressive)
            {
                ApplyCommonWT();
                ApplyThrottleAxis(100f, 60f, 2.4f, 3.0f, 1.45f);
                jet.thrust = 235f;
                jet.turnTorque = new Vector3(28f, 12f, 36f);
                jet.accelerationModeTorque = new Vector3(28f, 12f, 36f);
                jet.forceModeTorque = new Vector3(7600f, 3600f, 10400f);
                jet.maxAppliedTorqueAccelerationMode = new Vector3(58f, 24f, 72f);
                jet.maxAppliedTorqueForceMode = new Vector3(11000f, 5000f, 12500f);
                jet.maxAppliedTorque = jet.maxAppliedTorqueAccelerationMode;
                jet.angularDamping = 1.35f;
                jet.maxAngularVelocity = 7.2f;
                jet.pitchGain = 1.02f;
                jet.rollGain = 1.45f;
                jet.yawGain = 0.30f;
                jet.maxAutoPitch = 0.82f;
                jet.noseDownTrim = 0.095f;
                jet.inputSmoothing = 4.6f;
                jet.pitchUpCommand = -3.25f;
                jet.pitchDownCommand = 3.05f;
                jet.manualPitchBoost = 3.15f;
                jet.maxScreenRollBankAngle = 84f;
                jet.bankHoldProportional = 0.058f;
                jet.bankHoldRollRateDamping = 0.20f;
                jet.keyboardElevatorResponse = 42f;
                jet.keyboardElevatorReleaseBlend = 10f;
                jet.keyboardElevatorRateDamping = 0.04f;
                jet.rollCommandDeadzone = 0.018f;
                jet.rollCommandSlewRate = 15f;
                jet.controlSurfaceResponse = 10f;
                jet.controlSurfaceReleaseResponse = 7.5f;
                jet.finalTorqueSmoothing = 12.0f;
                jet.maxPitchCommandRate = 7f;
                jet.maxYawCommandRate = 3.8f;
                jet.maxRollCommandRate = 8.5f;
                jet.useDirectManualTorqueAssist = true;
                jet.directManualPitchAssist = 9f;
                jet.directManualRollAssist = 12f;
                jet.directManualYawAssist = 3.5f;
                jet.velocityTurnAssistStrength = 0.052f;
                jet.velocityTurnAssistMaxAccel = 17f;
                jet.velocityTurnAssistInputFactor = 0.58f;
                jet.sideSlipDamping = 0.10f;
                jet.sideSlipDampingHighAoS = 0.22f;
                jet.highSpeedTurnDragStrength = 0.012f;
                jet.angularRateDampingPitch = 0.075f;
                jet.angularRateDampingYaw = 0.10f;
                jet.angularRateDampingRoll = 0.060f;
                jet.mousePitchDeadzoneY = 0.012f;
                jet.centerPitchLevelStrength = 0.05f;
                jet.noseHighPitchDownAssist = 0.14f;
                jet.enableCoordinatedYawAssist = true;
                jet.aosYawAssistStrength = 0.080f;
                jet.maxAutoRudderAssist = 0.65f;
                jet.maxThrottle = 1.48f;
                jet.targetCruiseSpeed = 335f;
                jet.minCombatSpeed = 135f;
                jet.maxCombatSpeed = 550f;
                jet.lowSpeedThrustBoost = 0.62f;
                jet.overspeedDrag = 0.045f;
                ApplyRateBasedControl(
                    62f,
                    24f,
                    95f,
                    new Vector3(0.52f, 0.34f, 0.48f),
                    new Vector3(0.08f, 0.10f, 0.07f),
                    new Vector3(44f, 18f, 56f),
                    true,
                    8f,
                    10f,
                    3f
                );
                if (rig != null)
                {
                    rig.cameraRollFollowStrength = 0.36f;
                    rig.cameraLocalPosition = new Vector3(0f, 5.1f, -15.5f);
                    rig.cameraFov = 65f;
                    rig.zoomFov = 36f;
                    rig.useMouseAimCameraFollow = true;
                    rig.mouseAimCameraFollowStrength = 0.70f;
                    rig.mouseAimCameraFollowSmooth = 14.0f;
                    rig.mouseAimCameraMaxAngle = 44f;
                }
            }
            else
            {
                jet.enableMouseFlightAutopilot = false;
                jet.attitudeStabilizer = false;
                jet.yawDamper = false;
                jet.useGLimiter = false;
                jet.useMousePitchComfort = false;
                jet.useWTKeyboardElevatorOverride = false;
                jet.useRateBasedControl = false;
                jet.state = "direct_debug";
                if (rig != null)
                {
                    rig.stabilizeCameraHorizon = false;
                    rig.cameraRollFollowStrength = 1f;
                }
            }

            jet.SetupPublicRigidbody();
            jet.PushLegacyTuningToInstructor();
            instructor = jet.instructor;

            if (targetingPod != null)
            {
                targetingPod.fov = Mathf.Clamp(targetingPod.fov, targetingPod.minFov, targetingPod.maxFov);
            }

            lastApplied = p.ToString();
        }

        /// <summary>
        /// Layers WT feel tuning on top of whichever aircraft is ALREADY in force.
        ///
        /// This controller is a tuning layer and nothing else. It must never decide which aircraft
        /// the player is flying, and it used to do exactly that:
        ///
        ///     MavAircraftRuntimeProfile profile = MavAircraftCatalog.GetBuiltIn(applier.aircraft);
        ///     applier.ApplyProfile(profile);
        ///
        /// `applier.aircraft` is a SERIALIZED inspector field whose default is F22A. During bootstrap
        /// - MavFreshBootstrap.Awake -> SetupFreshMouseFlight -> ApplyPreset -> here - no selection
        /// has been applied yet, so that read returned F22A and this line applied the entire F-22
        /// profile to Mav_Player. A player who had selected the F-16 got "applied F-22A RAPTOR to
        /// Mav_Player" in the log, and F-22 mass, thrust, wing area and TVC on the aircraft. The
        /// catalog was answering correctly; the caller was asking the wrong question.
        ///
        /// So identity now comes only from the applier's authoritative state. With no aircraft yet
        /// applied, the aircraft-aware pass is SKIPPED - returning false, which falls through to the
        /// plain feel preset that touches the jet's handling and no aircraft identity at all. Waiting
        /// is correct; guessing is not.
        /// </summary>
        private bool TryApplyAircraftAwarePreset(MavWTFeelPreset p)
        {
            if (p == MavWTFeelPreset.DirectDebug)
                return false;

            MavAircraftProfileApplier applier = jet != null ? jet.GetComponent<MavAircraftProfileApplier>() : null;
            if (applier == null)
                return false;

            // The one question this controller is allowed to ask about identity: has one been
            // established? Never "which one should it be?".
            if (!applier.HasAuthoritativeAircraft)
            {
                lastApplied = "no aircraft applied yet: WT feel skipped its aircraft-aware pass "
                              + "rather than defaulting to one";

                // Waiting is correct, but a silent wait is hard to diagnose. Said once, because the
                // preset hotkeys can re-enter this freely.
                if (!loggedMissingAircraftAuthority)
                {
                    loggedMissingAircraftAuthority = true;
                    Debug.LogWarning(
                        "[Maverick/Aircraft] WT feel ran before any aircraft was applied to "
                        + applier.name + ", so it tuned handling only and left aircraft identity "
                        + "alone. If the aircraft is never applied, check that the in-game bootstrap "
                        + "applies the session selection.", this);
                }

                return false;
            }

            applier.applyOnStart = false;
            applier.renameObject = false;

            // Re-state the identity already in force so the tuning below lands on a clean baseline.
            // This cannot change which aircraft this is.
            string reapplyError;
            if (!applier.TryReapplyAppliedProfile(out reapplyError))
            {
                lastApplied = "WT feel could not refresh the applied aircraft: " + reapplyError;
                return false;
            }

            MavAircraftRuntimeProfile profile = applier.AppliedProfile;
            if (profile == null)
                return false;

            Resolve();

            float response = 1f;
            float damping = 1f;
            float camera = 1f;
            float throttle = 1f;
            if (p == MavWTFeelPreset.WarThunderF15Smooth)
            {
                response = 0.78f;
                damping = 1.18f;
                camera = 0.90f;
                throttle = 0.94f;
            }
            else if (p == MavWTFeelPreset.WarThunderF15Aggressive)
            {
                response = 1.16f;
                damping = 0.88f;
                camera = 1.08f;
                throttle = 1.06f;
            }

            jet.pitchGain *= response;
            jet.rollGain *= response;
            jet.yawGain *= Mathf.Lerp(1f, response, 0.45f);
            jet.targetPitchRateDeg *= response;
            jet.targetRollRateDeg *= response;
            jet.targetYawRateDeg *= Mathf.Lerp(1f, response, 0.50f);
            jet.angularDamping *= damping;
            jet.controlSurfaceResponse *= response;
            jet.controlSurfaceReleaseResponse *= Mathf.Lerp(1f, response, 0.50f);
            jet.maxPitchCommandRate *= response;
            jet.maxRollCommandRate *= response;
            jet.maxYawCommandRate *= Mathf.Lerp(1f, response, 0.50f);
            jet.throttleChangeRatePercentPerSecond *= throttle;

            if (rig != null)
            {
                rig.cameraRollFollowStrength = Mathf.Clamp01(rig.cameraRollFollowStrength * camera);
                rig.mouseAimCameraFollowStrength = Mathf.Clamp01(rig.mouseAimCameraFollowStrength * camera);
                rig.cameraFov = Mathf.Clamp(rig.cameraFov * Mathf.Lerp(1f, camera, 0.35f), 58f, 82f);
            }

            lastApplied = "aircraft-aware " + profile.shortName + " " + p;
            return true;
        }

        private void ApplyCommonWT()
        {
            jet.enableMouseFlightAutopilot = true;
            jet.attitudeStabilizer = true;
            jet.yawDamper = true;
            jet.useGLimiter = true;
            jet.useLowSpeedNoseDownAssist = true;
            jet.useSpeedAuthorityCurve = true;
            jet.useMousePitchComfort = true;
            jet.useWTKeyboardElevatorOverride = true;
            jet.keyboardPitchSuppressesMouseAim = true;
            // Keep aero/gravity policy owned by MavAeroBody/profile. Do not reset it here.
            jet.linearDamping = 0.006f;
            jet.angularDamping = 1.75f;
            jet.maxAngularVelocity = 6f;
            jet.useAccelerationTorqueMode = true;
            jet.useRateBasedControl = true;
            jet.accelerationModeTorque = new Vector3(20f, 9f, 26f);
            jet.forceModeTorque = new Vector3(6200f, 3200f, 8200f);
            jet.maxAppliedTorqueAccelerationMode = new Vector3(42f, 18f, 52f);
            jet.maxAppliedTorqueForceMode = new Vector3(9000f, 4200f, 10500f);
            jet.bestTurnSpeed = 236f;
            jet.turnBandWidth = 95f;
            jet.targetCruiseSpeed = 315f;
            jet.minCombatSpeed = 135f;
            jet.maxCombatSpeed = 530f;
            jet.softGLimit = 8.8f;
            jet.hardGLimit = 11.2f;
            jet.gPitchReduction = 0.30f;
            jet.aoaSoftLimitDeg = 24f;
            jet.aoaHardLimitDeg = 34f;
            jet.aoaPitchReduction = 0.45f;
            jet.highGShortTermAllowance = 10.8f;
            jet.sustainedGLimit = 8.8f;
            jet.useHighSpeedPitchLimiter = true;
            jet.pitchLimiterStartSpeed = 310f;
            jet.pitchLimiterFullSpeed = 500f;
            jet.highSpeedPitchAuthorityMin = 0.55f;
            jet.highSpeedManualPitchAuthorityMin = 0.78f;
            jet.highSpeedPitchLimiterSmooth = 6f;
            jet.useAoAAoSSoftGuard = true;
            jet.aoaSoftGuardStart = 18f;
            jet.aoaHardGuardStart = 30f;
            jet.aosSoftGuardStart = 10f;
            jet.aosHardGuardStart = 24f;
            jet.highSlipGuardStart = 25f;
            jet.highSlipGuardHard = 55f;
            jet.guardPitchReduction = 0.55f;
            jet.guardYawReduction = 0.45f;
            jet.guardRollReduction = 0.35f;
            jet.highSpeedTurnDragStrength = 0.014f;
            jet.highAoADragStrength = 0.026f;
            jet.highAoSDragStrength = 0.034f;
            jet.maxEnvelopeDragAccel = 28f;
            jet.useFinalTorqueClamp = true;
            jet.maxAppliedTorque = jet.maxAppliedTorqueAccelerationMode;
            jet.finalTorqueSmoothing = 10.5f;
            jet.controlSurfaceResponse = 7.5f;
            jet.controlSurfaceReleaseResponse = 6.5f;
            jet.maxPitchCommandRate = 4.8f;
            jet.maxYawCommandRate = 2.8f;
            jet.maxRollCommandRate = 5.8f;
            jet.enableCoordinatedYawAssist = true;
            jet.coordinatedYawSpeedMin = 85f;
            jet.coordinatedYawFullSpeed = 210f;
            jet.coordinatedYawBankFactor = 0.35f;
            jet.coordinatedYawTurnDemandFactor = 0.35f;
            jet.coordinatedYawDamping = 0.28f;
            jet.yawRateDampingStrength = 0.42f;
            jet.sideSlipDampingStrength = 0.55f;
            jet.maxAutoYawCommand = 0.24f;
            jet.keyboardYawAuthority = 0.55f;
            jet.preferBankTurnOverYaw = true;
            jet.useVelocityTurnAssist = true;
            jet.velocityTurnAssistStrength = 0.018f;
            jet.velocityTurnAssistMaxAccel = 14f;
            jet.velocityTurnAssistMinSpeed = 80f;
            jet.velocityTurnAssistFullSpeed = 230f;
            jet.velocityTurnAssistInputFactor = 0.58f;
            jet.velocityTurnAssistAoSLimit = 45f;
            jet.velocityTurnAssistSlipFadeStart = 20f;
            jet.velocityTurnAssistSlipFadeEnd = 55f;
            jet.useScreenRollZoneSteering = true;
            jet.useScreenRollZoneBankHold = true;
            jet.rollCommandDeadzone = 0.025f;
            jet.bankHoldDeadzoneDeg = 4.0f;
            jet.rollCommandSlewRate = 12f;
            jet.yawAssistDeadzoneAosDeg = 2.5f;
            jet.centerRollStabilizeStrength = 0.12f;
            jet.mouseRollDeadzone = 0.055f;
            jet.mouseYawDeadzone = 0.045f;
            jet.rollRateDeadzoneDeg = 3.0f;
            jet.aosYawAssistDeadzoneDeg = 2.5f;
            jet.rollInputEnterDeadzone = 0.065f;
            jet.rollInputExitDeadzone = 0.040f;
            jet.useScreenPitchZoneSteering = false;
            jet.useContinuousScreenBankHold = false;
            jet.mousePitchDeadzoneY = 0.045f;
            jet.mousePitchFullAtY = 0.42f;
            jet.mousePitchExponent = 1.25f;
            jet.keyboardElevatorMouseBlend = 0.03f;
            jet.keyboardElevatorResponse = 34f;
            jet.keyboardElevatorReleaseBlend = 8.5f;
            jet.keyboardElevatorRateDamping = 0.045f;
            jet.useManualControlAuthorityBoost = true;
            jet.manualPitchAuthorityBoost = 1.35f;
            jet.manualRollAuthorityBoost = 1.45f;
            jet.manualYawAuthorityBoost = 1.15f;
            jet.manualPitchResponseMultiplier = 1.7f;
            jet.manualRollResponseMultiplier = 1.8f;
            jet.manualLimiterBypassFactor = 0.35f;
            jet.manualDampingReduction = 0.45f;
            jet.manualEnvelopeBypassFactor = 0.65f;
            jet.manualPitchMinAuthority = 0.78f;
            jet.manualRollMinAuthority = 0.82f;
            jet.manualYawMinAuthority = 0.60f;
            jet.useDirectManualTorqueAssist = false;
            jet.directManualPitchAssist = 9f;
            jet.directManualRollAssist = 12f;
            jet.directManualYawAssist = 3.5f;
            jet.sideSlipDamping = 0.12f;
            jet.sideSlipDampingHighAoS = 0.24f;
            jet.aoaDragStrength = 0.018f;
            jet.aosDragStrength = 0.026f;
            jet.angularRateDampingPitch = 0.09f;
            jet.angularRateDampingYaw = 0.12f;
            jet.angularRateDampingRoll = 0.075f;

            if (rig != null)
            {
                rig.stabilizeCameraHorizon = true;
                rig.cameraLooksAtAimWithoutFreeLook = false;
                rig.useScreenRollZones = true;
                rig.useScreenPitchZones = false;
                rig.rollNoRollHalfWidth = 0.22f;
                rig.rollFullAtHalfWidth = 0.42f;
                rig.rollZoneExponent = 1.25f;
                rig.cameraFarClip = 24000f;
                rig.useCameraLag = true;
                rig.cameraPositionLag = 7f;
                rig.cameraRotationLag = 8f;
                rig.hardManeuverCameraLagMultiplier = 0.65f;
                rig.maxCameraLagDistance = 6f;
                rig.useSpeedBasedFov = true;
                rig.minSpeedFov = 60f;
                rig.cruiseSpeedFov = 68f;
                rig.maxSpeedFov = 78f;
                rig.fovSmooth = 4f;
                rig.fovSpeedMin = 80f;
                rig.fovSpeedMax = 360f;
                rig.useCameraCollisionAvoidance = true;
                rig.cameraMinDistance = 9f;
                rig.cameraMaxDistance = 18f;
                rig.diveCameraPullback = 4f;
                rig.steepDivePitchThreshold = -55f;
                rig.cameraCollisionRadius = 0.8f;
            }
        }

        private void ApplyRateBasedControl(
            float pitchRateDeg,
            float yawRateDeg,
            float rollRateDeg,
            Vector3 pGain,
            Vector3 dGain,
            Vector3 maxTorque,
            bool directAssist,
            float pitchAssist,
            float rollAssist,
            float yawAssist)
        {
            jet.useRateBasedControl = true;
            jet.targetPitchRateDeg = pitchRateDeg;
            jet.targetYawRateDeg = yawRateDeg;
            jet.targetRollRateDeg = rollRateDeg;
            jet.rateControlP = pGain;
            jet.rateControlD = dGain;
            jet.maxRateControlTorque = maxTorque;
            jet.mouseRollDeadzone = 0.055f;
            jet.mouseYawDeadzone = 0.045f;
            jet.bankHoldDeadzoneDeg = 4.0f;
            jet.rollRateDeadzoneDeg = 3.0f;
            jet.aosYawAssistDeadzoneDeg = 2.5f;
            jet.yawAssistDeadzoneAosDeg = 2.5f;
            jet.rollInputEnterDeadzone = 0.065f;
            jet.rollInputExitDeadzone = 0.040f;
            jet.rollCommandDeadzone = Mathf.Max(jet.rollCommandDeadzone, jet.mouseRollDeadzone);
            jet.useDirectManualTorqueAssist = directAssist;
            jet.directManualPitchAssist = pitchAssist;
            jet.directManualRollAssist = rollAssist;
            jet.directManualYawAssist = yawAssist;
        }

        private void ApplyThrottleAxis(float defaultPercent, float changeRatePercent, float spoolUp, float spoolDown, float afterburnerMultiplier)
        {
            jet.minThrottlePercent = -5f;
            jet.idleThrottlePercent = 0f;
            jet.militaryThrottlePercent = 100f;
            jet.maxThrottlePercent = 110f;
            jet.throttlePercent = defaultPercent;
            jet.displayedThrottlePercent = defaultPercent;
            jet.throttle = Mathf.Clamp(defaultPercent / 100f, -0.05f, jet.maxThrottle);
            jet.engineOn = true;
            jet.engineToggleKey = KeyCode.I;
            jet.throttleUpKey = KeyCode.LeftShift;
            jet.throttleDownKey = KeyCode.LeftControl;
            jet.throttleIdleKey = KeyCode.X;
            jet.throttleChangeRatePercentPerSecond = changeRatePercent;
            jet.throttleWheelStepPercent = 5f;
            jet.throttleKeyboardStepPercent = 0f;
            jet.useMouseWheelThrottle = true;
            jet.holdThrottleKeysContinuous = true;
            jet.idleThrust01 = 0.04f;
            jet.negativeThrottleBrakeDrag = 0.018f;
            jet.afterburnerStartPercent = 100f;
            jet.afterburnerMaxPercent = 110f;
            jet.afterburnerThrustMultiplier = afterburnerMultiplier;
            jet.afterburnerFuelBurnMultiplier = 2.5f;
            jet.useAfterburner = true;
            jet.useNegativeThrottleBrakeDrag = true;
            jet.throttleSpoolUpRate = spoolUp;
            jet.throttleSpoolDownRate = spoolDown;
        }
    }
}
