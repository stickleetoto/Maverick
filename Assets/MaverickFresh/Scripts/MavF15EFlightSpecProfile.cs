using UnityEngine;

namespace MaverickFresh
{
    /// <summary>
    /// Game-friendly F-15E-inspired flight/spec profile.
    /// 
    /// This is not a real flight model. It uses public high-level characteristics
    /// to shape the arcade/sim-lite feel:
    /// - twin-engine heavy fighter
    /// - high thrust
    /// - high top speed
    /// - large wing area / heavy mass
    /// - strong roll-driven turning
    /// 
    /// Values are intentionally clamped and tuned for Unity gameplay.
    /// </summary>
    [DisallowMultipleComponent]
    public class MavF15EFlightSpecProfile : MonoBehaviour
    {
        [Header("Real-world reference values, public high-level")]
        public float realLengthMeters = 19.44f;
        public float realWingspanMeters = 13.0f;
        public float realHeightMeters = 5.6f;
        public float realEmptyMassKg = 17010f;
        public float realMaxTakeoffMassKg = 36450f;
        public float realMaxThrustTotalNewtons = 258000f; // Approx 58,000 lbf total
        public float realMaxSpeedMach = 2.5f;
        public float realCeilingMeters = 18288f;

        [Header("Unity/game model scaling")]
        [Tooltip("Do not make the rigidbody mass equal to full real mass unless your force scale is tuned for it.")]
        public float gameplayMassKg = 14500f;
        public float normalCombatSpeed = 315f;
        public float highCombatSpeed = 530f;
        public float emergencySpeed = 580f;
        public float lowSpeedWarning = 135f;

        [Header("Fresh branch tuning output")]
        public float tunedThrust = 220f;
        public Vector3 tunedTurnTorque = new Vector3(20f, 9f, 26f);
        public Vector3 tunedForceModeTorque = new Vector3(6200f, 3200f, 8200f);
        public Vector3 tunedAccelerationTorqueClamp = new Vector3(42f, 18f, 52f);
        public Vector3 tunedForceTorqueClamp = new Vector3(9000f, 4200f, 10500f);
        public float tunedForceMult = 1000f;
        public float tunedLinearDamping = 0.006f;
        public float tunedAngularDamping = 1.6f;
        public float tunedMaxAngularVelocity = 6f;

        [Header("MouseFlight tuning output")]
        public float tunedSensitivity = 3.85f;
        public float tunedAggressiveTurnAngle = 9.5f;
        public float tunedPitchGain = 0.82f;
        public float tunedYawGain = 0.24f;
        public float tunedRollGain = 1.25f;
        public float tunedMaxAutoPitch = 0.68f;
        public float tunedNoseDownTrim = 0.115f;
        public float tunedInputSmoothing = 5.8f;

        [Header("Throttle tuning output")]
        public float tunedThrottlePercent = 95f;
        public float tunedThrottleChangeRatePercentPerSecond = 45f;
        public float tunedThrottleSpoolUpRate = 1.8f;
        public float tunedThrottleSpoolDownRate = 2.4f;
        public float tunedAfterburnerThrustMultiplier = 1.35f;

        [Header("Camera tuning output")]
        public float tunedCameraFov = 68f;
        public float tunedZoomFov = 42f;
        public float tunedCameraFarClip = 24000f;
        [Range(0f, 1f)] public float tunedCameraRollFollowStrength = 0.30f;
        public Vector3 tunedCameraLocalPosition = new Vector3(0f, 5.4f, -16.5f);
        public float tunedCursorAimLimit = 0.38f;
        public float tunedCursorAimSmooth = 20f;

        [Header("Runtime")]
        public bool applyOnStart = true;
        public MavMouseFlightJet jet;
        public MavMouseFlightRig rig;
        public MavInstructorController instructor;
        public Rigidbody rb;
        public string lastApplied = "not_applied";

        private void Awake()
        {
            Resolve();
        }

        private void Start()
        {
            if (applyOnStart)
                ApplyF15EProfile();
        }

        [ContextMenu("Apply F-15E Inspired Profile")]
        public void ApplyF15EProfile()
        {
            Resolve();

            if (rb != null)
            {
                rb.mass = gameplayMassKg;
                rb.useGravity = false;
                rb.linearDamping = tunedLinearDamping;
                rb.angularDamping = tunedAngularDamping;
                rb.maxAngularVelocity = tunedMaxAngularVelocity;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }

            if (jet != null)
            {
                jet.thrust = tunedThrust;
                jet.turnTorque = tunedTurnTorque;
                jet.forceMult = tunedForceMult;
                jet.gravityOff = true;
                jet.linearDamping = tunedLinearDamping;
                jet.angularDamping = 1.75f;
                jet.maxAngularVelocity = 6.0f;
                jet.useAccelerationTorqueMode = true;
                jet.useRateBasedControl = true;
                jet.targetPitchRateDeg = 48f;
                jet.targetYawRateDeg = 18f;
                jet.targetRollRateDeg = 72f;
                jet.rateControlP = new Vector3(0.42f, 0.28f, 0.38f);
                jet.rateControlD = new Vector3(0.10f, 0.12f, 0.09f);
                jet.maxRateControlTorque = new Vector3(32f, 14f, 40f);
                jet.accelerationModeTorque = tunedTurnTorque;
                jet.forceModeTorque = tunedForceModeTorque;
                jet.maxAppliedTorqueAccelerationMode = tunedAccelerationTorqueClamp;
                jet.maxAppliedTorqueForceMode = tunedForceTorqueClamp;
                jet.maxAppliedTorque = jet.maxAppliedTorqueAccelerationMode;

                jet.sensitivity = tunedSensitivity;
                jet.aggressiveTurnAngle = tunedAggressiveTurnAngle;
                jet.pitchGain = tunedPitchGain;
                jet.yawGain = tunedYawGain;
                jet.rollGain = 1.25f;
                jet.maxAutoPitch = tunedMaxAutoPitch;
                jet.noseDownTrim = tunedNoseDownTrim;
                jet.inputSmoothing = tunedInputSmoothing;

                jet.autoSpeedAssist = true;
                jet.targetCruiseSpeed = normalCombatSpeed;
                jet.minCombatSpeed = lowSpeedWarning;
                jet.maxCombatSpeed = highCombatSpeed;
                jet.speedAssistStrength = 0.22f;
                jet.overspeedDrag = 0.055f;
                jet.lowSpeedThrustBoost = 0.55f;

                jet.bestTurnSpeed = 236f;
                jet.turnBandWidth = 95f;
                jet.bestTurnPitchBoost = 1.28f;
                jet.bestTurnRollBoost = 1.12f;
                jet.manualPitchBoost = 2.75f;
                jet.manualRollBoost = 1.05f;

                jet.pitchUpCommand = -2.85f;
                jet.pitchDownCommand = 2.65f;
                jet.keyboardYawAuthority = 0.55f;
                jet.keyboardElevatorMouseBlend = 0.03f;
                jet.keyboardElevatorResponse = 34f;
                jet.keyboardElevatorReleaseBlend = 8.5f;
                jet.keyboardElevatorRateDamping = 0.045f;

                jet.useSpeedAuthorityCurve = true;
                jet.lowSpeedPitchAuthority = 0.58f;
                jet.bestSpeedPitchAuthority = 1.15f;
                jet.highSpeedPitchAuthority = 0.72f;
                jet.lowSpeedRollAuthority = 0.70f;
                jet.bestSpeedRollAuthority = 1.05f;
                jet.highSpeedRollAuthority = 0.82f;
                jet.pitchLimiterStartSpeed = 310f;
                jet.pitchLimiterFullSpeed = 500f;
                jet.highSpeedPitchAuthorityMin = 0.55f;
                jet.highSpeedManualPitchAuthorityMin = 0.78f;

                jet.useGLimiter = true;
                jet.softGLimit = 8.8f;
                jet.hardGLimit = 11.2f;
                jet.useLowSpeedNoseDownAssist = true;

                jet.attitudeStabilizer = true;
                jet.centerAimAngleForLeveling = 7f;
                jet.activeTurnLevelingMultiplier = 0.22f;
                jet.rollLevelStrength = 0.52f;
                jet.rateDampingStrength = 0.125f;

                jet.yawDamper = true;
                jet.yawRateDampingStrength = 0.42f;
                jet.sideSlipDampingStrength = 0.55f;
                jet.maxAutoYawCommand = 0.24f;
                jet.keyboardYawAuthority = 0.55f;
                jet.preferBankTurnOverYaw = true;
                jet.enableCoordinatedYawAssist = true;
                jet.aosYawAssistStrength = 0.060f;
                jet.maxAutoRudderAssist = 0.55f;
                jet.coordinatedYawSpeedMin = 85f;
                jet.coordinatedYawFullSpeed = 210f;
                jet.coordinatedYawBankFactor = 0.35f;
                jet.coordinatedYawTurnDemandFactor = 0.35f;
                jet.coordinatedYawDamping = 0.28f;
                jet.useVelocityTurnAssist = true;
                jet.velocityTurnAssistStrength = 0.018f;
                jet.velocityTurnAssistMaxAccel = 14f;
                jet.velocityTurnAssistMinSpeed = 80f;
                jet.velocityTurnAssistFullSpeed = 230f;
                jet.velocityTurnAssistInputFactor = 0.58f;
                jet.velocityTurnAssistAoSLimit = 45f;
                jet.aoaSoftLimitDeg = 24f;
                jet.aoaHardLimitDeg = 34f;
                jet.aoaPitchReduction = 0.45f;
                jet.highGShortTermAllowance = 10.8f;
                jet.sustainedGLimit = 8.8f;
                jet.finalTorqueSmoothing = 10.5f;
                jet.controlSurfaceResponse = 7.5f;
                jet.controlSurfaceReleaseResponse = 6.5f;
                jet.maxPitchCommandRate = 4.8f;
                jet.maxYawCommandRate = 2.8f;
                jet.maxRollCommandRate = 5.8f;
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
                jet.directManualPitchAssist = 0f;
                jet.directManualRollAssist = 0f;
                jet.directManualYawAssist = 0f;
                jet.sideSlipDamping = 0.14f;
                jet.sideSlipDampingHighAoS = 0.30f;
                jet.aoaDragStrength = 0.018f;
                jet.aosDragStrength = 0.026f;
                jet.highSpeedTurnDragStrength = 0.018f;
                jet.angularRateDampingPitch = 0.09f;
                jet.angularRateDampingYaw = 0.12f;
                jet.angularRateDampingRoll = 0.075f;

                jet.useMousePitchComfort = true;
                jet.mousePitchDeadzoneY = 0.020f;
                jet.mousePitchFullAtY = 0.42f;
                jet.mousePitchExponent = 1.25f;
                jet.centerPitchLevelStrength = 0.08f;
                jet.noseHighPitchDownAssist = 0.10f;
                jet.noseHighAssistStartAngle = 8f;
                jet.noseHighAssistFullAngle = 24f;

                jet.useScreenRollZoneSteering = true;
                jet.screenRollZoneStrength = 1.0f;
                jet.suppressAutoRollInNoRollZone = true;
                jet.noRollZoneLevelingBoost = 1.35f;
                jet.useScreenRollZoneBankHold = true;
                jet.maxScreenRollBankAngle = 72f;
                jet.bankHoldProportional = 0.048f;
                jet.bankHoldRollRateDamping = 0.24f;
                jet.invertScreenRollBankTarget = false;
                jet.rollCommandDeadzone = 0.055f;
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
                jet.useContinuousScreenBankHold = false;
                jet.continuousBankDeadzone = 0.035f;
                jet.continuousBankFullAtX = 0.42f;
                jet.continuousBankExponent = 1.15f;
                jet.useScreenPitchZoneSteering = false;
                jet.screenPitchZoneStrength = 1.0f;
                jet.suppressAutoPitchInNoPitchZone = true;
                jet.noPitchZoneLevelingBoost = 1.20f;

                jet.maxThrottle = 1.40f;
                jet.minThrottlePercent = -5f;
                jet.idleThrottlePercent = 0f;
                jet.militaryThrottlePercent = 100f;
                jet.maxThrottlePercent = 110f;
                jet.throttlePercent = tunedThrottlePercent;
                jet.displayedThrottlePercent = tunedThrottlePercent;
                jet.throttle = Mathf.Clamp(tunedThrottlePercent / 100f, -0.05f, jet.maxThrottle);
                jet.engineOn = true;
                jet.engineToggleKey = KeyCode.I;
                jet.throttleUpKey = KeyCode.LeftShift;
                jet.throttleDownKey = KeyCode.LeftControl;
                jet.throttleIdleKey = KeyCode.X;
                jet.throttleChangeRatePercentPerSecond = tunedThrottleChangeRatePercentPerSecond;
                jet.throttleWheelStepPercent = 5f;
                jet.throttleKeyboardStepPercent = 0f;
                jet.useMouseWheelThrottle = true;
                jet.holdThrottleKeysContinuous = true;
                jet.idleThrust01 = 0.04f;
                jet.negativeThrottleBrakeDrag = 0.018f;
                jet.afterburnerStartPercent = 100f;
                jet.afterburnerMaxPercent = 110f;
                jet.afterburnerThrustMultiplier = tunedAfterburnerThrustMultiplier;
                jet.afterburnerFuelBurnMultiplier = 2.5f;
                jet.useAfterburner = true;
                jet.useNegativeThrottleBrakeDrag = true;
                jet.throttleSpoolUpRate = tunedThrottleSpoolUpRate;
                jet.throttleSpoolDownRate = tunedThrottleSpoolDownRate;
                jet.SetupPublicRigidbody();
                jet.PushLegacyTuningToInstructor();
                if (instructor == null)
                    instructor = jet.instructor;
            }

            if (rig != null)
            {
                rig.aimInputMode = MavAimInputMode.CursorPosition;
                rig.cameraLooksAtAimWithoutFreeLook = false;
                rig.cursorAimLimit = tunedCursorAimLimit;
                rig.cursorAimSmooth = tunedCursorAimSmooth;
                rig.cameraFov = tunedCameraFov;
                rig.zoomFov = tunedZoomFov;
                rig.cameraFarClip = tunedCameraFarClip;
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
                rig.useMouseAimCameraFollow = true;
                rig.mouseAimCameraFollowStrength = 0.55f;
                rig.mouseAimCameraFollowSmooth = 12.0f;
                rig.mouseAimCameraMaxAngle = 36f;
                rig.cameraRollFollowStrength = tunedCameraRollFollowStrength;
                rig.stabilizeCameraHorizon = true;
                rig.useScreenRollZones = true;
                rig.rollNoRollHalfWidth = 0.22f;
                rig.rollFullAtHalfWidth = 0.42f;
                rig.rollZoneExponent = 1.25f;
                rig.useScreenPitchZones = false;
                rig.pitchNoPitchHalfHeight = 0.16f;
                rig.pitchFullAtHalfHeight = 0.40f;
                rig.pitchZoneExponent = 1.20f;
                rig.cameraLocalPosition = tunedCameraLocalPosition;
                rig.camSmoothSpeed = 9.5f;
                rig.showCursorInCursorMode = true;

                if (rig.playerCamera != null)
                {
                    rig.playerCamera.fieldOfView = tunedCameraFov;
                    rig.playerCamera.transform.localPosition = tunedCameraLocalPosition;
                }
            }

            lastApplied = "f15e_profile_applied";
        }

        public void Resolve()
        {
            if (jet == null) jet = GetComponent<MavMouseFlightJet>();
            if (rig == null) rig = FindObjectOfType<MavMouseFlightRig>();
            if (instructor == null && jet != null) instructor = jet.GetComponent<MavInstructorController>();
            if (instructor == null) instructor = GetComponent<MavInstructorController>();
            if (rb == null) rb = GetComponent<Rigidbody>();
            if (instructor != null)
            {
                instructor.jet = jet;
                instructor.rig = rig;
                instructor.rb = rb;
            }
        }
    }
}
