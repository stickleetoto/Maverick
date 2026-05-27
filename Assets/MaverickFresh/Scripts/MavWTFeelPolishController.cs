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

            if (p == MavWTFeelPreset.WarThunderF15Balanced)
            {
                ApplyCommonWT();
                jet.thrust = 220f;
                jet.turnTorque = new Vector3(125f, 16f, 150f);
                jet.pitchGain = 0.76f;
                jet.rollGain = 1.18f;
                jet.yawGain = 0.22f;
                jet.maxAutoPitch = 0.62f;
                jet.noseDownTrim = 0.115f;
                jet.inputSmoothing = 6.8f;
                jet.pitchUpCommand = -2.35f;
                jet.pitchDownCommand = 2.15f;
                jet.manualPitchBoost = 2.15f;
                jet.maxScreenRollBankAngle = 72f;
                jet.bankHoldProportional = 0.052f;
                jet.bankHoldRollRateDamping = 0.19f;
                jet.mousePitchDeadzoneY = 0.025f;
                jet.centerPitchLevelStrength = 0.10f;
                jet.noseHighPitchDownAssist = 0.12f;
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
                if (rig != null)
                {
                    rig.cameraRollFollowStrength = 0.28f;
                    rig.cameraLocalPosition = new Vector3(0f, 5.3f, -17.2f);
                    rig.cameraFov = 63f;
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
                jet.thrust = 210f;
                jet.turnTorque = new Vector3(108f, 14f, 128f);
                jet.angularDamping = 1.55f;
                jet.maxAngularVelocity = 4.2f;
                jet.pitchGain = 0.62f;
                jet.rollGain = 0.92f;
                jet.yawGain = 0.17f;
                jet.maxAutoPitch = 0.50f;
                jet.noseDownTrim = 0.14f;
                jet.inputSmoothing = 9.5f;
                jet.pitchUpCommand = -2.35f;
                jet.pitchDownCommand = 2.15f;
                jet.manualPitchBoost = 2.15f;
                jet.maxScreenRollBankAngle = 60f;
                jet.bankHoldProportional = 0.044f;
                jet.bankHoldRollRateDamping = 0.22f;
                jet.mousePitchDeadzoneY = 0.040f;
                jet.centerPitchLevelStrength = 0.16f;
                jet.noseHighPitchDownAssist = 0.24f;
                jet.enableCoordinatedYawAssist = true;
                jet.aosYawAssistStrength = 0.045f;
                jet.maxAutoRudderAssist = 0.38f;
                jet.maxThrottle = 1.32f;
                jet.targetCruiseSpeed = 285f;
                jet.minCombatSpeed = 135f;
                jet.maxCombatSpeed = 470f;
                jet.lowSpeedThrustBoost = 0.48f;
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
                jet.thrust = 235f;
                jet.turnTorque = new Vector3(145f, 18f, 172f);
                jet.angularDamping = 1.20f;
                jet.maxAngularVelocity = 5.4f;
                jet.pitchGain = 0.95f;
                jet.rollGain = 1.18f;
                jet.yawGain = 0.28f;
                jet.maxAutoPitch = 0.78f;
                jet.noseDownTrim = 0.095f;
                jet.inputSmoothing = 5.2f;
                jet.pitchUpCommand = -2.35f;
                jet.pitchDownCommand = 2.15f;
                jet.manualPitchBoost = 2.15f;
                jet.maxScreenRollBankAngle = 82f;
                jet.bankHoldProportional = 0.060f;
                jet.bankHoldRollRateDamping = 0.17f;
                jet.mousePitchDeadzoneY = 0.018f;
                jet.centerPitchLevelStrength = 0.07f;
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
                targetingPod.displayMode = MavTargetingPodDisplayMode.PictureInPicture;
                targetingPod.fov = Mathf.Clamp(targetingPod.fov, targetingPod.minFov, targetingPod.maxFov);
            }

            lastApplied = p.ToString();
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
            jet.gravityOff = true;
            jet.linearDamping = 0.021f;
            jet.angularDamping = 1.35f;
            jet.maxAngularVelocity = 4.8f;
            jet.useAccelerationTorqueMode = false;
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
            jet.enableCoordinatedYawAssist = true;
            jet.coordinatedYawSpeedMin = 85f;
            jet.coordinatedYawFullSpeed = 210f;
            jet.coordinatedYawBankFactor = 0.35f;
            jet.coordinatedYawTurnDemandFactor = 0.35f;
            jet.coordinatedYawDamping = 0.20f;
            jet.yawRateDampingStrength = 0.42f;
            jet.sideSlipDampingStrength = 0.082f;
            jet.maxAutoYawCommand = 0.24f;
            jet.keyboardYawAuthority = 0.55f;
            jet.preferBankTurnOverYaw = true;
            jet.useScreenRollZoneSteering = true;
            jet.useScreenRollZoneBankHold = true;
            jet.useScreenPitchZoneSteering = false;
            jet.useContinuousScreenBankHold = false;
            jet.mousePitchDeadzoneY = 0.045f;
            jet.mousePitchFullAtY = 0.42f;
            jet.mousePitchExponent = 1.25f;
            jet.keyboardElevatorMouseBlend = 0.05f;
            jet.keyboardElevatorResponse = 22f;
            jet.keyboardElevatorReleaseBlend = 6.5f;
            jet.keyboardElevatorRateDamping = 0.06f;

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
            }
        }
    }
}
