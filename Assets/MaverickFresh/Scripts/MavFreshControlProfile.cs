using UnityEngine;

namespace MaverickFresh
{
    public enum MavFreshFlightPreset
    {
        WarThunderMouseAim = 0,
        HeavyJet = 1,
        ArcadeStable = 2,
        DebugDirect = 3
    }

    /// <summary>
    /// Central tuning profile for the fresh branch.
    /// This keeps "feel" tuning in one place instead of manually hunting through Jet/Rig values.
    /// </summary>
    public class MavFreshControlProfile : MonoBehaviour
    {
        public MavFreshFlightPreset preset = MavFreshFlightPreset.WarThunderMouseAim;
        public bool applyOnStart = true;

        public MavMouseFlightJet jet;
        public MavMouseFlightRig rig;
        public MavInstructorController instructor;

        [Header("Runtime")]
        public string lastApplied = "none";

        private void Awake()
        {
            if (jet == null) jet = FindObjectOfType<MavMouseFlightJet>();
            if (rig == null) rig = FindObjectOfType<MavMouseFlightRig>();
            if (instructor == null) instructor = FindObjectOfType<MavInstructorController>();
            if (instructor != null)
            {
                instructor.jet = jet;
                instructor.rig = rig;
            }
        }

        private void Start()
        {
            if (applyOnStart)
                ApplyPreset(preset);
        }

        private void Update()
        {
            if (MavFreshInput.GetKeyDown(KeyCode.Home)) ApplyPreset(MavFreshFlightPreset.WarThunderMouseAim);
            if (MavFreshInput.GetKeyDown(KeyCode.PageUp)) ApplyPreset(MavFreshFlightPreset.ArcadeStable);
            if (MavFreshInput.GetKeyDown(KeyCode.End)) ApplyPreset(MavFreshFlightPreset.HeavyJet);
            if (MavFreshInput.GetKeyDown(KeyCode.PageDown)) ApplyPreset(MavFreshFlightPreset.DebugDirect);
        }

        public void ApplyPreset(MavFreshFlightPreset p)
        {
            preset = p;
            if (jet == null) jet = FindObjectOfType<MavMouseFlightJet>();
            if (rig == null) rig = FindObjectOfType<MavMouseFlightRig>();
            if (instructor == null && jet != null) instructor = jet.GetComponent<MavInstructorController>();
            if (instructor == null) instructor = FindObjectOfType<MavInstructorController>();

            if (rig != null)
            {
                rig.aimInputMode = MavAimInputMode.CursorPosition;
                rig.cameraLooksAtAimWithoutFreeLook = false;
                rig.stabilizeCameraHorizon = true;
                rig.cameraRollFollowStrength = 0.30f;
                rig.useScreenRollZones = true;
                rig.rollNoRollHalfWidth = 0.22f;
                rig.rollFullAtHalfWidth = 0.42f;
                rig.rollZoneExponent = 1.25f;
                rig.useScreenPitchZones = false;
                rig.pitchNoPitchHalfHeight = 0.16f;
                rig.pitchFullAtHalfHeight = 0.40f;
                rig.pitchZoneExponent = 1.20f;
                rig.cursorAimLimit = 0.39f;
                rig.cameraFov = 68f;
                rig.cameraFarClip = 24000f;
                rig.showCursorInCursorMode = true;
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

            if (jet == null)
                return;

            if (p == MavFreshFlightPreset.WarThunderMouseAim)
            {
                ApplyThrottleAxis(95f, 45f, 1.8f, 2.4f, 1.35f);
                jet.thrust = 220f;
                jet.turnTorque = new Vector3(20f, 9f, 26f);
                jet.accelerationModeTorque = new Vector3(20f, 9f, 26f);
                jet.forceModeTorque = new Vector3(6200f, 3200f, 8200f);
                jet.maxAppliedTorqueAccelerationMode = new Vector3(42f, 18f, 52f);
                jet.maxAppliedTorqueForceMode = new Vector3(9000f, 4200f, 10500f);
                jet.maxAppliedTorque = jet.maxAppliedTorqueAccelerationMode;
                jet.forceMult = 1000f;
                // Gravity/aero policy is owned by MavAeroBody when present.
                jet.linearDamping = 0.006f;
                jet.angularDamping = 1.75f;
                jet.maxAngularVelocity = 6f;
                jet.useAccelerationTorqueMode = true;

                jet.sensitivity = 3.85f;
                jet.aggressiveTurnAngle = 9.5f;
                jet.pitchGain = 0.82f;
                jet.yawGain = 0.24f;
                jet.rollGain = 1.25f;
                jet.maxAutoPitch = 0.68f;
                jet.noseDownTrim = 0.115f;
                jet.inputSmoothing = 5.8f;
                jet.maxScreenRollBankAngle = 72f;
                jet.bankHoldProportional = 0.048f;
                jet.bankHoldRollRateDamping = 0.24f;

                jet.autoSpeedAssist = true;
                jet.targetCruiseSpeed = 315f;
                jet.minCombatSpeed = 135f;
                jet.maxCombatSpeed = 530f;
                jet.speedAssistStrength = 0.22f;
                jet.maxThrottle = 1.40f;

                jet.pitchUpCommand = -2.85f;
                jet.pitchDownCommand = 2.65f;
                jet.invertKeyboardPitch = false;
                jet.manualPitchBoost = 2.75f;
                jet.keyboardYawAuthority = 0.55f;
                jet.maxAutoYawCommand = 0.24f;
                jet.keyboardElevatorMouseBlend = 0.03f;
                jet.keyboardElevatorResponse = 34f;
                jet.keyboardElevatorReleaseBlend = 8.5f;
                jet.keyboardElevatorRateDamping = 0.045f;
                jet.useMousePitchComfort = true;
                jet.mousePitchDeadzoneY = 0.020f;
                jet.centerPitchLevelStrength = 0.08f;
                jet.noseHighPitchDownAssist = 0.10f;
                jet.enableCoordinatedYawAssist = true;
                jet.aosYawAssistStrength = 0.060f;
                jet.maxAutoRudderAssist = 0.55f;
                jet.coordinatedYawDamping = 0.28f;
                jet.useVelocityTurnAssist = true;
                jet.velocityTurnAssistStrength = 0.018f;
                jet.velocityTurnAssistMaxAccel = 14f;
                jet.velocityTurnAssistMinSpeed = 80f;
                jet.velocityTurnAssistFullSpeed = 230f;
                jet.velocityTurnAssistInputFactor = 0.58f;
                jet.velocityTurnAssistAoSLimit = 45f;
                jet.rollCommandDeadzone = 0.025f;
                jet.bankHoldDeadzoneDeg = 4.0f;
                jet.rollCommandSlewRate = 12f;
                jet.yawAssistDeadzoneAosDeg = 2.5f;
                jet.centerRollStabilizeStrength = 0.12f;
                jet.SetAoASoftLimitAuthority(24f);
                jet.SetAoAHardLimitAuthority(34f);
                jet.SetAoAPitchReductionAuthority(0.45f);
                jet.sustainedGLimit = 8.8f;
                jet.pitchLimiterStartSpeed = 310f;
                jet.pitchLimiterFullSpeed = 500f;
                jet.highSpeedPitchAuthorityMin = 0.55f;
                jet.highSpeedManualPitchAuthorityMin = 0.78f;
                jet.finalTorqueSmoothing = 10.5f;
                jet.controlSurfaceResponse = 7.5f;
                jet.controlSurfaceReleaseResponse = 6.5f;
                jet.maxPitchCommandRate = 4.8f;
                jet.maxYawCommandRate = 2.8f;
                jet.maxRollCommandRate = 5.8f;
                jet.sideSlipDamping = 0.14f;
                jet.sideSlipDampingHighAoS = 0.30f;
                jet.aoaDragStrength = 0.018f;
                jet.aosDragStrength = 0.026f;
                jet.highSpeedTurnDragStrength = 0.018f;
                jet.angularRateDampingPitch = 0.09f;
                jet.angularRateDampingYaw = 0.12f;
                jet.angularRateDampingRoll = 0.075f;
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

                if (rig != null)
                {
                    rig.mouseSensitivity = 2.25f;
                    rig.cursorAimSmooth = 22f;
                    rig.camSmoothSpeed = 10f;
                    rig.useMouseAimCameraFollow = true;
                    rig.mouseAimCameraFollowStrength = 0.55f;
                    rig.mouseAimCameraFollowSmooth = 12.0f;
                    rig.mouseAimCameraMaxAngle = 36f;
                    rig.cameraLocalPosition = new Vector3(0f, 5.4f, -16.5f);
                }
            }
            else if (p == MavFreshFlightPreset.HeavyJet)
            {
                ApplyThrottleAxis(90f, 35f, 1.3f, 1.8f, 1.25f);
                jet.thrust = 210f;
                jet.turnTorque = new Vector3(16f, 7f, 21f);
                jet.accelerationModeTorque = new Vector3(16f, 7f, 21f);
                jet.forceModeTorque = new Vector3(5400f, 2800f, 7000f);
                jet.maxAppliedTorqueAccelerationMode = new Vector3(34f, 14f, 42f);
                jet.maxAppliedTorqueForceMode = new Vector3(8200f, 3800f, 9200f);
                jet.maxAppliedTorque = jet.maxAppliedTorqueAccelerationMode;
                jet.linearDamping = 0.025f;
                jet.angularDamping = 2.0f;
                jet.maxAngularVelocity = 5.2f;
                jet.useAccelerationTorqueMode = true;

                jet.sensitivity = 2.55f;
                jet.aggressiveTurnAngle = 15f;
                jet.pitchGain = 0.66f;
                jet.yawGain = 0.18f;
                jet.rollGain = 1.08f;
                jet.maxAutoPitch = 0.54f;
                jet.noseDownTrim = 0.15f;
                jet.inputSmoothing = 8.5f;
                jet.maxScreenRollBankAngle = 62f;
                jet.bankHoldProportional = 0.038f;
                jet.bankHoldRollRateDamping = 0.29f;
                jet.pitchUpCommand = -2.45f;
                jet.pitchDownCommand = 2.25f;
                jet.manualPitchBoost = 2.35f;
                jet.keyboardYawAuthority = 0.55f;
                jet.maxAutoYawCommand = 0.24f;
                jet.keyboardElevatorMouseBlend = 0.03f;
                jet.keyboardElevatorResponse = 26f;
                jet.keyboardElevatorReleaseBlend = 7.5f;
                jet.keyboardElevatorRateDamping = 0.05f;
                jet.useMousePitchComfort = true;
                jet.mousePitchDeadzoneY = 0.035f;
                jet.centerPitchLevelStrength = 0.14f;
                jet.noseHighPitchDownAssist = 0.24f;
                jet.enableCoordinatedYawAssist = true;
                jet.aosYawAssistStrength = 0.045f;
                jet.maxAutoRudderAssist = 0.38f;
                jet.coordinatedYawDamping = 0.28f;
                jet.useVelocityTurnAssist = true;
                jet.velocityTurnAssistStrength = 0.032f;
                jet.velocityTurnAssistMaxAccel = 11f;
                jet.velocityTurnAssistMinSpeed = 80f;
                jet.velocityTurnAssistFullSpeed = 230f;
                jet.velocityTurnAssistInputFactor = 0.58f;
                jet.velocityTurnAssistAoSLimit = 45f;
                jet.rollCommandDeadzone = 0.035f;
                jet.bankHoldDeadzoneDeg = 4.0f;
                jet.rollCommandSlewRate = 9f;
                jet.yawAssistDeadzoneAosDeg = 2.5f;
                jet.centerRollStabilizeStrength = 0.12f;
                jet.SetAoASoftLimitAuthority(24f);
                jet.SetAoAHardLimitAuthority(34f);
                jet.SetAoAPitchReductionAuthority(0.45f);
                jet.sustainedGLimit = 8.8f;
                jet.pitchLimiterStartSpeed = 310f;
                jet.pitchLimiterFullSpeed = 500f;
                jet.highSpeedPitchAuthorityMin = 0.55f;
                jet.highSpeedManualPitchAuthorityMin = 0.78f;
                jet.finalTorqueSmoothing = 9.0f;
                jet.controlSurfaceResponse = 5.8f;
                jet.controlSurfaceReleaseResponse = 5.5f;
                jet.maxPitchCommandRate = 3.8f;
                jet.maxYawCommandRate = 2.2f;
                jet.maxRollCommandRate = 4.6f;
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
                jet.directManualPitchAssist = 5f;
                jet.directManualRollAssist = 7f;
                jet.directManualYawAssist = 2f;
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
                jet.sideSlipDamping = 0.14f;
                jet.sideSlipDampingHighAoS = 0.28f;
                jet.highSpeedTurnDragStrength = 0.016f;
                jet.angularRateDampingPitch = 0.12f;
                jet.angularRateDampingYaw = 0.15f;
                jet.angularRateDampingRoll = 0.095f;

                jet.autoSpeedAssist = true;
                jet.targetCruiseSpeed = 285f;
                jet.minCombatSpeed = 130f;
                jet.maxCombatSpeed = 470f;
                jet.speedAssistStrength = 0.28f;
                jet.maxThrottle = 1.32f;

                if (rig != null)
                {
                    rig.mouseSensitivity = 1.9f;
                    rig.cursorAimSmooth = 18f;
                    rig.camSmoothSpeed = 9f;
                    rig.useMouseAimCameraFollow = true;
                    rig.mouseAimCameraFollowStrength = 0.38f;
                    rig.mouseAimCameraFollowSmooth = 9.0f;
                    rig.mouseAimCameraMaxAngle = 26f;
                    rig.cameraLocalPosition = new Vector3(0f, 7f, -27f);
                }
            }
            else if (p == MavFreshFlightPreset.ArcadeStable)
            {
                ApplyThrottleAxis(100f, 60f, 2.4f, 3.0f, 1.45f);
                jet.thrust = 235f;
                jet.turnTorque = new Vector3(28f, 12f, 36f);
                jet.accelerationModeTorque = new Vector3(28f, 12f, 36f);
                jet.forceModeTorque = new Vector3(7600f, 3600f, 10400f);
                jet.maxAppliedTorqueAccelerationMode = new Vector3(58f, 24f, 72f);
                jet.maxAppliedTorqueForceMode = new Vector3(11000f, 5000f, 12500f);
                jet.maxAppliedTorque = jet.maxAppliedTorqueAccelerationMode;
                jet.linearDamping = 0.018f;
                jet.angularDamping = 1.35f;
                jet.maxAngularVelocity = 7.2f;
                jet.useAccelerationTorqueMode = true;

                jet.sensitivity = 3.8f;
                jet.aggressiveTurnAngle = 10f;
                jet.pitchGain = 1.02f;
                jet.yawGain = 0.30f;
                jet.rollGain = 1.45f;
                jet.maxAutoPitch = 0.82f;
                jet.noseDownTrim = 0.10f;
                jet.inputSmoothing = 4.6f;
                jet.maxScreenRollBankAngle = 84f;
                jet.bankHoldProportional = 0.058f;
                jet.bankHoldRollRateDamping = 0.20f;
                jet.pitchUpCommand = -3.25f;
                jet.pitchDownCommand = 3.05f;
                jet.manualPitchBoost = 3.15f;
                jet.keyboardYawAuthority = 0.55f;
                jet.maxAutoYawCommand = 0.24f;
                jet.keyboardElevatorMouseBlend = 0.03f;
                jet.keyboardElevatorResponse = 42f;
                jet.keyboardElevatorReleaseBlend = 10f;
                jet.keyboardElevatorRateDamping = 0.04f;
                jet.useMousePitchComfort = true;
                jet.mousePitchDeadzoneY = 0.012f;
                jet.centerPitchLevelStrength = 0.05f;
                jet.noseHighPitchDownAssist = 0.16f;
                jet.enableCoordinatedYawAssist = true;
                jet.aosYawAssistStrength = 0.080f;
                jet.maxAutoRudderAssist = 0.65f;
                jet.coordinatedYawDamping = 0.28f;
                jet.useVelocityTurnAssist = true;
                jet.velocityTurnAssistStrength = 0.052f;
                jet.velocityTurnAssistMaxAccel = 17f;
                jet.velocityTurnAssistMinSpeed = 80f;
                jet.velocityTurnAssistFullSpeed = 230f;
                jet.velocityTurnAssistInputFactor = 0.58f;
                jet.velocityTurnAssistAoSLimit = 45f;
                jet.rollCommandDeadzone = 0.018f;
                jet.bankHoldDeadzoneDeg = 4.0f;
                jet.rollCommandSlewRate = 15f;
                jet.yawAssistDeadzoneAosDeg = 2.5f;
                jet.centerRollStabilizeStrength = 0.12f;
                jet.SetAoASoftLimitAuthority(24f);
                jet.SetAoAHardLimitAuthority(34f);
                jet.SetAoAPitchReductionAuthority(0.45f);
                jet.sustainedGLimit = 8.8f;
                jet.pitchLimiterStartSpeed = 310f;
                jet.pitchLimiterFullSpeed = 500f;
                jet.highSpeedPitchAuthorityMin = 0.55f;
                jet.highSpeedManualPitchAuthorityMin = 0.78f;
                jet.finalTorqueSmoothing = 12.0f;
                jet.controlSurfaceResponse = 10f;
                jet.controlSurfaceReleaseResponse = 7.5f;
                jet.maxPitchCommandRate = 7f;
                jet.maxYawCommandRate = 3.8f;
                jet.maxRollCommandRate = 8.5f;
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
                jet.useDirectManualTorqueAssist = true;
                jet.directManualPitchAssist = 8f;
                jet.directManualRollAssist = 10f;
                jet.directManualYawAssist = 3f;
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
                jet.sideSlipDamping = 0.10f;
                jet.sideSlipDampingHighAoS = 0.22f;
                jet.highSpeedTurnDragStrength = 0.012f;
                jet.angularRateDampingPitch = 0.075f;
                jet.angularRateDampingYaw = 0.10f;
                jet.angularRateDampingRoll = 0.060f;

                jet.autoSpeedAssist = true;
                jet.targetCruiseSpeed = 335f;
                jet.minCombatSpeed = 115f;
                jet.maxCombatSpeed = 550f;
                jet.speedAssistStrength = 0.45f;
                jet.maxThrottle = 1.48f;

                if (rig != null)
                {
                    rig.mouseSensitivity = 2.7f;
                    rig.cursorAimSmooth = 24f;
                    rig.camSmoothSpeed = 12f;
                    rig.useMouseAimCameraFollow = true;
                    rig.mouseAimCameraFollowStrength = 0.70f;
                    rig.mouseAimCameraFollowSmooth = 14.0f;
                    rig.mouseAimCameraMaxAngle = 44f;
                    rig.cameraLocalPosition = new Vector3(0f, 6.0f, -22f);
                }
            }
            else
            {
                jet.enableMouseFlightAutopilot = false;
                jet.autoSpeedAssist = false;
                jet.thrust = 150f;
                jet.turnTorque = new Vector3(55f, 24f, 70f);
                jet.accelerationModeTorque = new Vector3(55f, 24f, 70f);
                jet.forceModeTorque = new Vector3(8200f, 4200f, 11000f);
                jet.maxAppliedTorqueAccelerationMode = new Vector3(120f, 48f, 145f);
                jet.maxAppliedTorqueForceMode = new Vector3(12000f, 5600f, 14000f);
                jet.maxAppliedTorque = jet.maxAppliedTorqueAccelerationMode;
                jet.useAccelerationTorqueMode = true;
                jet.useRateBasedControl = false;
                jet.angularDamping = 0.9f;
                jet.maxAngularVelocity = 10f;
                jet.controlSurfaceResponse = 24f;
                jet.controlSurfaceReleaseResponse = 14f;
                jet.maxPitchCommandRate = 18f;
                jet.maxYawCommandRate = 9f;
                jet.maxRollCommandRate = 21f;
                jet.manualDampingReduction = 0.35f;
                jet.manualEnvelopeBypassFactor = 0.75f;
                jet.manualPitchMinAuthority = 0.90f;
                jet.manualRollMinAuthority = 0.90f;
                jet.manualYawMinAuthority = 0.75f;
                jet.useDirectManualTorqueAssist = true;
                jet.inputSmoothing = 0f;
            }

            jet.SetupPublicRigidbody();
            jet.PushLegacyTuningToInstructor();
            if (instructor == null)
                instructor = jet.instructor;
            lastApplied = p.ToString();
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
            if (jet == null)
                return;

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
