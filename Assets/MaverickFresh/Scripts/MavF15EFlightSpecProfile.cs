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
        public Vector3 tunedTurnTorque = new Vector3(125f, 16f, 150f);
        public float tunedForceMult = 1000f;
        public float tunedLinearDamping = 0.021f;
        public float tunedAngularDamping = 1.35f;
        public float tunedMaxAngularVelocity = 4.8f;

        [Header("MouseFlight tuning output")]
        public float tunedSensitivity = 3.85f;
        public float tunedAggressiveTurnAngle = 9.5f;
        public float tunedPitchGain = 0.76f;
        public float tunedYawGain = 0.22f;
        public float tunedRollGain = 1.18f;
        public float tunedMaxAutoPitch = 0.62f;
        public float tunedNoseDownTrim = 0.115f;
        public float tunedInputSmoothing = 6.8f;

        [Header("Camera tuning output")]
        public float tunedCameraFov = 64f;
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
                jet.angularDamping = tunedAngularDamping;
                jet.maxAngularVelocity = tunedMaxAngularVelocity;
                jet.useAccelerationTorqueMode = false;

                jet.sensitivity = tunedSensitivity;
                jet.aggressiveTurnAngle = tunedAggressiveTurnAngle;
                jet.pitchGain = tunedPitchGain;
                jet.yawGain = tunedYawGain;
                jet.rollGain = tunedRollGain;
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
                jet.manualPitchBoost = 2.15f;
                jet.manualRollBoost = 1.05f;

                jet.pitchUpCommand = -2.35f;
                jet.pitchDownCommand = 2.15f;
                jet.keyboardYawAuthority = 0.55f;
                jet.keyboardElevatorMouseBlend = 0.05f;
                jet.keyboardElevatorResponse = 22f;
                jet.keyboardElevatorReleaseBlend = 6.5f;
                jet.keyboardElevatorRateDamping = 0.06f;

                jet.useSpeedAuthorityCurve = true;
                jet.lowSpeedPitchAuthority = 0.58f;
                jet.bestSpeedPitchAuthority = 1.15f;
                jet.highSpeedPitchAuthority = 0.72f;
                jet.lowSpeedRollAuthority = 0.70f;
                jet.bestSpeedRollAuthority = 1.05f;
                jet.highSpeedRollAuthority = 0.82f;

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
                jet.sideSlipDampingStrength = 0.082f;
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
                jet.coordinatedYawDamping = 0.20f;
                jet.aoaSoftLimitDeg = 24f;
                jet.aoaHardLimitDeg = 34f;
                jet.aoaPitchReduction = 0.45f;
                jet.highGShortTermAllowance = 10.8f;
                jet.sustainedGLimit = 8.8f;

                jet.useMousePitchComfort = true;
                jet.mousePitchDeadzoneY = 0.025f;
                jet.mousePitchFullAtY = 0.42f;
                jet.mousePitchExponent = 1.25f;
                jet.centerPitchLevelStrength = 0.10f;
                jet.noseHighPitchDownAssist = 0.12f;
                jet.noseHighAssistStartAngle = 8f;
                jet.noseHighAssistFullAngle = 24f;

                jet.useScreenRollZoneSteering = true;
                jet.screenRollZoneStrength = 1.0f;
                jet.suppressAutoRollInNoRollZone = true;
                jet.noRollZoneLevelingBoost = 1.35f;
                jet.useScreenRollZoneBankHold = true;
                jet.maxScreenRollBankAngle = 72f;
                jet.bankHoldProportional = 0.052f;
                jet.bankHoldRollRateDamping = 0.19f;
                jet.invertScreenRollBankTarget = false;
                jet.useContinuousScreenBankHold = false;
                jet.continuousBankDeadzone = 0.035f;
                jet.continuousBankFullAtX = 0.42f;
                jet.continuousBankExponent = 1.15f;
                jet.useScreenPitchZoneSteering = false;
                jet.screenPitchZoneStrength = 1.0f;
                jet.suppressAutoPitchInNoPitchZone = true;
                jet.noPitchZoneLevelingBoost = 1.20f;

                jet.maxThrottle = 1.40f;
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
