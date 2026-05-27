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
                rig.cameraFov = 64f;
                rig.cameraFarClip = 24000f;
                rig.showCursorInCursorMode = true;
            }

            if (jet == null)
                return;

            if (p == MavFreshFlightPreset.WarThunderMouseAim)
            {
                jet.thrust = 220f;
                jet.turnTorque = new Vector3(125f, 16f, 150f);
                jet.forceMult = 1000f;
                jet.gravityOff = true;
                jet.linearDamping = 0.021f;
                jet.angularDamping = 1.35f;
                jet.maxAngularVelocity = 4.8f;
                jet.useAccelerationTorqueMode = false;

                jet.sensitivity = 3.85f;
                jet.aggressiveTurnAngle = 9.5f;
                jet.pitchGain = 0.76f;
                jet.yawGain = 0.22f;
                jet.rollGain = 1.18f;
                jet.maxAutoPitch = 0.62f;
                jet.noseDownTrim = 0.115f;
                jet.inputSmoothing = 6.8f;
                jet.maxScreenRollBankAngle = 72f;
                jet.bankHoldProportional = 0.052f;
                jet.bankHoldRollRateDamping = 0.19f;

                jet.autoSpeedAssist = true;
                jet.targetCruiseSpeed = 315f;
                jet.minCombatSpeed = 135f;
                jet.maxCombatSpeed = 530f;
                jet.speedAssistStrength = 0.22f;
                jet.maxThrottle = 1.40f;

                jet.pitchUpCommand = -2.35f;
                jet.pitchDownCommand = 2.15f;
                jet.invertKeyboardPitch = false;
                jet.manualPitchBoost = 2.15f;
                jet.keyboardYawAuthority = 0.55f;
                jet.maxAutoYawCommand = 0.24f;
                jet.keyboardElevatorMouseBlend = 0.05f;
                jet.keyboardElevatorResponse = 22f;
                jet.keyboardElevatorReleaseBlend = 6.5f;
                jet.keyboardElevatorRateDamping = 0.06f;
                jet.useMousePitchComfort = true;
                jet.mousePitchDeadzoneY = 0.025f;
                jet.centerPitchLevelStrength = 0.10f;
                jet.noseHighPitchDownAssist = 0.12f;
                jet.enableCoordinatedYawAssist = true;
                jet.aosYawAssistStrength = 0.060f;
                jet.maxAutoRudderAssist = 0.55f;
                jet.aoaSoftLimitDeg = 24f;
                jet.aoaHardLimitDeg = 34f;
                jet.aoaPitchReduction = 0.45f;
                jet.sustainedGLimit = 8.8f;

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
                jet.thrust = 210f;
                jet.turnTorque = new Vector3(108f, 14f, 128f);
                jet.linearDamping = 0.025f;
                jet.angularDamping = 1.55f;
                jet.maxAngularVelocity = 4.2f;
                jet.useAccelerationTorqueMode = false;

                jet.sensitivity = 2.55f;
                jet.aggressiveTurnAngle = 15f;
                jet.pitchGain = 0.62f;
                jet.yawGain = 0.17f;
                jet.rollGain = 0.92f;
                jet.maxAutoPitch = 0.50f;
                jet.noseDownTrim = 0.15f;
                jet.inputSmoothing = 9.5f;
                jet.maxScreenRollBankAngle = 60f;
                jet.bankHoldProportional = 0.044f;
                jet.bankHoldRollRateDamping = 0.22f;
                jet.pitchUpCommand = -2.35f;
                jet.pitchDownCommand = 2.15f;
                jet.manualPitchBoost = 2.15f;
                jet.keyboardYawAuthority = 0.55f;
                jet.maxAutoYawCommand = 0.24f;
                jet.keyboardElevatorMouseBlend = 0.05f;
                jet.keyboardElevatorResponse = 22f;
                jet.keyboardElevatorReleaseBlend = 6.5f;
                jet.keyboardElevatorRateDamping = 0.06f;
                jet.useMousePitchComfort = true;
                jet.mousePitchDeadzoneY = 0.040f;
                jet.centerPitchLevelStrength = 0.16f;
                jet.noseHighPitchDownAssist = 0.24f;
                jet.enableCoordinatedYawAssist = true;
                jet.aosYawAssistStrength = 0.045f;
                jet.maxAutoRudderAssist = 0.38f;
                jet.aoaSoftLimitDeg = 24f;
                jet.aoaHardLimitDeg = 34f;
                jet.aoaPitchReduction = 0.45f;
                jet.sustainedGLimit = 8.8f;

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
                jet.thrust = 235f;
                jet.turnTorque = new Vector3(145f, 18f, 172f);
                jet.linearDamping = 0.018f;
                jet.angularDamping = 1.20f;
                jet.maxAngularVelocity = 5.4f;
                jet.useAccelerationTorqueMode = false;

                jet.sensitivity = 3.8f;
                jet.aggressiveTurnAngle = 10f;
                jet.pitchGain = 0.95f;
                jet.yawGain = 0.28f;
                jet.rollGain = 1.18f;
                jet.maxAutoPitch = 0.78f;
                jet.noseDownTrim = 0.10f;
                jet.inputSmoothing = 5.2f;
                jet.maxScreenRollBankAngle = 82f;
                jet.bankHoldProportional = 0.060f;
                jet.bankHoldRollRateDamping = 0.17f;
                jet.pitchUpCommand = -2.35f;
                jet.pitchDownCommand = 2.15f;
                jet.manualPitchBoost = 2.15f;
                jet.keyboardYawAuthority = 0.55f;
                jet.maxAutoYawCommand = 0.24f;
                jet.keyboardElevatorMouseBlend = 0.05f;
                jet.keyboardElevatorResponse = 22f;
                jet.keyboardElevatorReleaseBlend = 6.5f;
                jet.keyboardElevatorRateDamping = 0.06f;
                jet.useMousePitchComfort = true;
                jet.mousePitchDeadzoneY = 0.018f;
                jet.centerPitchLevelStrength = 0.07f;
                jet.noseHighPitchDownAssist = 0.16f;
                jet.enableCoordinatedYawAssist = true;
                jet.aosYawAssistStrength = 0.080f;
                jet.maxAutoRudderAssist = 0.65f;
                jet.aoaSoftLimitDeg = 24f;
                jet.aoaHardLimitDeg = 34f;
                jet.aoaPitchReduction = 0.45f;
                jet.sustainedGLimit = 8.8f;

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
                jet.turnTorque = new Vector3(70f, 25f, 75f);
                jet.inputSmoothing = 0f;
            }

            jet.SetupPublicRigidbody();
            jet.PushLegacyTuningToInstructor();
            if (instructor == null)
                instructor = jet.instructor;
            lastApplied = p.ToString();
        }
    }
}
