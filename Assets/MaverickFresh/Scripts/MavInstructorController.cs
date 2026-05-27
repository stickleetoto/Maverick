using UnityEngine;

namespace MaverickFresh
{
    /// <summary>
    /// v0.17 War-Thunder-like Instructor.
    /// Owns control interpretation and produces normalized pitch/yaw/roll commands.
    /// MavMouseFlightJet remains the physical Rigidbody actuator.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public class MavInstructorController : MonoBehaviour
    {
        [Header("Components")]
        public MavMouseFlightRig rig;
        public MavMouseFlightJet jet;
        public Rigidbody rb;

        [Header("Mouse Aim Autopilot")]
        public float sensitivity = 3.85f;
        public float aggressiveTurnAngle = 9.5f;
        public bool enableMouseFlightAutopilot = true;
        public float pitchGain = 0.76f;
        public float yawGain = 0.22f;
        public float rollGain = 1.18f;
        public float maxAutoPitch = 0.62f;
        public float noseDownTrim = 0.115f;
        public float inputSmoothing = 6.8f;

        [Header("Mouse Pitch Comfort")]
        public bool useMousePitchComfort = true;
        public float mousePitchDeadzoneY = 0.025f;
        public float mousePitchFullAtY = 0.42f;
        [Range(0.5f, 3.0f)] public float mousePitchExponent = 1.25f;
        public float centerPitchLevelStrength = 0.10f;
        public float noseHighPitchDownAssist = 0.12f;
        public float noseHighAssistStartAngle = 8f;
        public float noseHighAssistFullAngle = 24f;

        [Header("F-15-like Turn Assist")]
        public float bestTurnSpeed = 236f;
        public float turnBandWidth = 95f;
        public float bestTurnPitchBoost = 1.18f;
        public float bestTurnRollBoost = 1.10f;
        public float lowSpeedTurnPenalty = 0.62f;
        public float highSpeedTurnPenalty = 0.72f;
        public float manualPitchBoost = 2.15f;
        public float manualRollBoost = 1.05f;

        [Header("WarThunder-like Authority Curve")]
        public bool useSpeedAuthorityCurve = true;
        public float lowSpeedPitchAuthority = 0.55f;
        public float bestSpeedPitchAuthority = 1.06f;
        public float highSpeedPitchAuthority = 0.62f;
        public float lowSpeedRollAuthority = 0.72f;
        public float bestSpeedRollAuthority = 1.04f;
        public float highSpeedRollAuthority = 0.80f;

        [Header("G / Stall Assist")]
        public bool useGLimiter = true;
        public float softGLimit = 8.8f;
        public float hardGLimit = 11.2f;
        public float gPitchReduction = 0.30f;
        public bool useLowSpeedNoseDownAssist = true;
        public float stallAssistSpeed = 102f;
        public float stallNoseDownAssist = 0.34f;
        public float stallAssistMaxPitchClamp = 0.22f;
        [Header("AoA / High-G Protection")]
        public float aoaSoftLimitDeg = 24f;
        public float aoaHardLimitDeg = 34f;
        public float aoaPitchReduction = 0.45f;
        public float highGShortTermAllowance = 10.8f;
        public float sustainedGLimit = 8.8f;

        [Header("Instructor Stabilizer")]
        public bool attitudeStabilizer = true;
        public float rateDampingStrength = 0.145f;
        public float rollLevelStrength = 0.48f;
        public float pitchRecoveryStrength = 0.135f;
        public float pilotInputSuppress = 0.96f;
        public float maxStabilizerTorque = 160f;

        [Header("Conditional Stabilizer Behavior")]
        public float centerAimAngleForLeveling = 8.5f;
        public float activeTurnLevelingMultiplier = 0.18f;
        public float centerLevelingMultiplier = 1.0f;
        public float freeLookLevelingMultiplier = 0.55f;

        [Header("Yaw Damper")]
        public bool yawDamper = true;
        public float yawRateDampingStrength = 0.42f;
        public float maxAutoYawCommand = 0.24f;
        public bool preferBankTurnOverYaw = true;

        [Header("Coordinated Yaw Assist")]
        public bool enableCoordinatedYawAssist = true;
        public float aosYawAssistStrength = 0.060f;
        public float maxAutoRudderAssist = 0.55f;
        public float coordinatedYawSpeedMin = 85f;
        public float coordinatedYawFullSpeed = 210f;
        public float coordinatedYawBankFactor = 0.35f;
        public float coordinatedYawTurnDemandFactor = 0.35f;
        public float coordinatedYawDamping = 0.20f;
        public float coordinatedYawAssistOutput;

        [Header("Speed Assist")]
        public bool autoSpeedAssist = true;
        public float targetCruiseSpeed = 315f;
        public float minCombatSpeed = 135f;
        public float maxCombatSpeed = 530f;
        public float speedAssistStrength = 0.22f;
        public float overspeedDrag = 0.055f;
        public float lowSpeedThrustBoost = 0.55f;

        [Header("Throttle")]
        [Range(0f, 1.6f)] public float throttleIntent = 0.95f;
        public float throttleChangeRate = 0.65f;
        public float maxThrottle = 1.40f;

        [Header("Keyboard Override")]
        public bool keyboardOverride = true;
        public bool invertKeyboardRoll;
        public bool invertKeyboardPitch;
        public bool invertKeyboardYaw;
        public float keyboardRollThreshold = 0.10f;
        public float keyboardPitchThreshold = 0.02f;
        [Tooltip("MouseFlight convention here: negative pitch usually means nose-up.")]
        public float pitchUpCommand = -2.35f;
        [Tooltip("MouseFlight convention here: positive pitch usually means nose-down.")]
        public float pitchDownCommand = 2.15f;
        public float keyboardYawAuthority = 0.55f;
        public bool keyboardPitchSuppressesMouseAim = true;

        [Header("WarThunder-like Elevator Override")]
        public bool useWTKeyboardElevatorOverride = true;
        [Range(0f, 1f)] public float keyboardElevatorMouseBlend = 0.05f;
        public float keyboardElevatorResponse = 22f;
        public float keyboardElevatorReleaseBlend = 6.5f;
        public float keyboardElevatorRateDamping = 0.06f;
        public bool keyboardElevatorUsesGLimit = true;

        [Header("Screen Roll Zone Steering")]
        public bool useScreenRollZoneSteering = true;
        public float screenRollZoneStrength = 1.0f;
        public bool suppressAutoRollInNoRollZone = true;
        public float noRollZoneLevelingBoost = 1.35f;

        [Header("Screen Roll Zone Bank Hold")]
        public bool useScreenRollZoneBankHold = true;
        public float maxScreenRollBankAngle = 72f;
        public float bankHoldProportional = 0.052f;
        public float bankHoldRollRateDamping = 0.19f;
        public bool invertScreenRollBankTarget = false;

        [Header("Continuous Screen Bank Hold")]
        public bool useContinuousScreenBankHold = false;
        public float continuousBankDeadzone = 0.035f;
        public float continuousBankFullAtX = 0.42f;
        [Range(0.5f, 3.0f)] public float continuousBankExponent = 1.15f;

        [Header("Screen Pitch Zone Steering")]
        public bool useScreenPitchZoneSteering = false;
        public float screenPitchZoneStrength = 1.0f;
        public bool suppressAutoPitchInNoPitchZone = true;
        public float noPitchZoneLevelingBoost = 1.20f;

        [Header("Output")]
        [Range(-1f, 1f)] public float pitch;
        [Range(-1f, 1f)] public float yaw;
        [Range(-1f, 1f)] public float roll;

        [Header("Telemetry")]
        public Vector3 flyTarget;
        public Vector3 localFlyTarget;
        public float angleOffTarget;
        public float signedBankAngle;
        public float targetBankAngle;
        public float bankHoldRollCommand;
        public float signedPitchAngle;
        public float speed;
        public float gEstimate;
        public float aoaEstimateDeg;
        public float aosEstimateDeg;
        public float verticalSpeed;
        public Vector3 localVelocity;
        public float velocityPitchAngleDeg;
        public float velocityYawAngleDeg;
        public float turnBandFactor;
        public float pitchAuthorityFactor = 1f;
        public float rollAuthorityFactor = 1f;
        public string instructorState = "ready";

        [Header("Compatibility Debug")]
        public float aggressiveRoll;
        public float wingsLevelRoll;
        public float wingsLevelInfluence;
        public float autoPitchRaw;
        public float pitchTorqueFactor = 1f;
        public float rollTorqueFactor = 1f;
        public Vector3 localAngularVelocity;
        public string speedRegime = "normal";
        public string state = "ready";
        public bool debugWPressed;
        public bool debugSPressed;
        public float debugKeyboardPitchInput;
        public float debugFinalPitchBeforeSmoothing;
        public float debugFinalPitchAfterSmoothing;

        private bool rollOverride;
        private bool pitchOverride;
        private float smPitch;
        private float smYaw;
        private float smRoll;
        private float smKeyboardPitch;
        private Vector3 lastVelocity;
        private bool hasVelocitySample;

        private void Awake()
        {
            Resolve();
            if (jet != null)
                CopyTuningFromJet(jet);
            if (rb != null)
            {
                lastVelocity = rb.linearVelocity;
                hasVelocitySample = true;
            }
        }

        private void OnEnable()
        {
            Resolve();
            if (rb != null)
            {
                lastVelocity = rb.linearVelocity;
                hasVelocitySample = true;
            }
        }

        private void Update()
        {
            Resolve();
            RefreshAttitudeTelemetry();
            UpdateThrottle();
            UpdateControls();
            MirrorRuntimeToJet();
        }

        public void Resolve()
        {
            if (rb == null) rb = GetComponent<Rigidbody>();
            if (jet == null) jet = GetComponent<MavMouseFlightJet>();

            if (rig == null && jet != null)
                rig = jet.controller;

            if (rig == null)
                rig = FindObjectOfType<MavMouseFlightRig>();

            if (jet != null)
            {
                if (jet.instructor == null)
                    jet.instructor = this;

                if (jet.controller == null)
                    jet.controller = rig;
            }
        }

        public void SetThrottleIntent(float value)
        {
            throttleIntent = Mathf.Clamp(value, 0f, maxThrottle);
            if (jet != null)
                jet.throttle = throttleIntent;
        }

        public void RefreshPhysicsTelemetry(float fixedDeltaTime)
        {
            Resolve();

            if (rb == null)
                return;

            speed = rb.linearVelocity.magnitude;

            if (!hasVelocitySample)
            {
                lastVelocity = rb.linearVelocity;
                hasVelocitySample = true;
            }

            Vector3 acceleration = (rb.linearVelocity - lastVelocity) / Mathf.Max(fixedDeltaTime, 0.0001f);
            gEstimate = Vector3.Dot(acceleration, transform.up) / 9.80665f;
            lastVelocity = rb.linearVelocity;

            RefreshAttitudeTelemetry();
            ComputeTurnBandFactors();
        }

        public void CopyTuningFromJet(MavMouseFlightJet source)
        {
            if (source == null)
                return;

            sensitivity = source.sensitivity;
            aggressiveTurnAngle = source.aggressiveTurnAngle;
            enableMouseFlightAutopilot = source.enableMouseFlightAutopilot;
            pitchGain = source.pitchGain;
            yawGain = source.yawGain;
            rollGain = source.rollGain;
            maxAutoPitch = source.maxAutoPitch;
            noseDownTrim = source.noseDownTrim;
            inputSmoothing = source.inputSmoothing;

            useMousePitchComfort = source.useMousePitchComfort;
            mousePitchDeadzoneY = source.mousePitchDeadzoneY;
            mousePitchFullAtY = source.mousePitchFullAtY;
            mousePitchExponent = source.mousePitchExponent;
            centerPitchLevelStrength = source.centerPitchLevelStrength;
            noseHighPitchDownAssist = source.noseHighPitchDownAssist;
            noseHighAssistStartAngle = source.noseHighAssistStartAngle;
            noseHighAssistFullAngle = source.noseHighAssistFullAngle;

            bestTurnSpeed = source.bestTurnSpeed;
            turnBandWidth = source.turnBandWidth;
            bestTurnPitchBoost = source.bestTurnPitchBoost;
            bestTurnRollBoost = source.bestTurnRollBoost;
            lowSpeedTurnPenalty = source.lowSpeedTurnPenalty;
            highSpeedTurnPenalty = source.highSpeedTurnPenalty;
            manualPitchBoost = source.manualPitchBoost;
            manualRollBoost = source.manualRollBoost;

            useSpeedAuthorityCurve = source.useSpeedAuthorityCurve;
            lowSpeedPitchAuthority = source.lowSpeedPitchAuthority;
            bestSpeedPitchAuthority = source.bestSpeedPitchAuthority;
            highSpeedPitchAuthority = source.highSpeedPitchAuthority;
            lowSpeedRollAuthority = source.lowSpeedRollAuthority;
            bestSpeedRollAuthority = source.bestSpeedRollAuthority;
            highSpeedRollAuthority = source.highSpeedRollAuthority;

            useGLimiter = source.useGLimiter;
            softGLimit = source.softGLimit;
            hardGLimit = source.hardGLimit;
            gPitchReduction = source.gPitchReduction;
            useLowSpeedNoseDownAssist = source.useLowSpeedNoseDownAssist;
            stallAssistSpeed = source.stallAssistSpeed;
            stallNoseDownAssist = source.stallNoseDownAssist;
            stallAssistMaxPitchClamp = source.stallAssistMaxPitchClamp;
            aoaSoftLimitDeg = source.aoaSoftLimitDeg;
            aoaHardLimitDeg = source.aoaHardLimitDeg;
            aoaPitchReduction = source.aoaPitchReduction;
            highGShortTermAllowance = source.highGShortTermAllowance;
            sustainedGLimit = source.sustainedGLimit;

            attitudeStabilizer = source.attitudeStabilizer;
            rateDampingStrength = source.rateDampingStrength;
            rollLevelStrength = source.rollLevelStrength;
            pitchRecoveryStrength = source.pitchRecoveryStrength;
            pilotInputSuppress = source.pilotInputSuppress;
            maxStabilizerTorque = source.maxStabilizerTorque;

            centerAimAngleForLeveling = source.centerAimAngleForLeveling;
            activeTurnLevelingMultiplier = source.activeTurnLevelingMultiplier;
            centerLevelingMultiplier = source.centerLevelingMultiplier;
            freeLookLevelingMultiplier = source.freeLookLevelingMultiplier;

            yawDamper = source.yawDamper;
            yawRateDampingStrength = source.yawRateDampingStrength;
            maxAutoYawCommand = source.maxAutoYawCommand;
            preferBankTurnOverYaw = source.preferBankTurnOverYaw;

            enableCoordinatedYawAssist = source.enableCoordinatedYawAssist;
            aosYawAssistStrength = source.aosYawAssistStrength;
            maxAutoRudderAssist = source.maxAutoRudderAssist;
            coordinatedYawSpeedMin = source.coordinatedYawSpeedMin;
            coordinatedYawFullSpeed = source.coordinatedYawFullSpeed;
            coordinatedYawBankFactor = source.coordinatedYawBankFactor;
            coordinatedYawTurnDemandFactor = source.coordinatedYawTurnDemandFactor;
            coordinatedYawDamping = source.coordinatedYawDamping;

            autoSpeedAssist = source.autoSpeedAssist;
            targetCruiseSpeed = source.targetCruiseSpeed;
            minCombatSpeed = source.minCombatSpeed;
            maxCombatSpeed = source.maxCombatSpeed;
            speedAssistStrength = source.speedAssistStrength;
            overspeedDrag = source.overspeedDrag;
            lowSpeedThrustBoost = source.lowSpeedThrustBoost;

            throttleIntent = Mathf.Clamp(source.throttle, 0f, source.maxThrottle);
            throttleChangeRate = source.throttleChangeRate;
            maxThrottle = source.maxThrottle;

            keyboardOverride = source.keyboardOverride;
            invertKeyboardRoll = source.invertKeyboardRoll;
            invertKeyboardPitch = source.invertKeyboardPitch;
            invertKeyboardYaw = source.invertKeyboardYaw;
            keyboardRollThreshold = source.keyboardRollThreshold;
            keyboardPitchThreshold = source.keyboardPitchThreshold;
            pitchUpCommand = source.pitchUpCommand;
            pitchDownCommand = source.pitchDownCommand;
            keyboardYawAuthority = source.keyboardYawAuthority;
            keyboardPitchSuppressesMouseAim = source.keyboardPitchSuppressesMouseAim;

            useWTKeyboardElevatorOverride = source.useWTKeyboardElevatorOverride;
            keyboardElevatorMouseBlend = source.keyboardElevatorMouseBlend;
            keyboardElevatorResponse = source.keyboardElevatorResponse;
            keyboardElevatorReleaseBlend = source.keyboardElevatorReleaseBlend;
            keyboardElevatorRateDamping = source.keyboardElevatorRateDamping;
            keyboardElevatorUsesGLimit = source.keyboardElevatorUsesGLimit;

            useScreenRollZoneSteering = source.useScreenRollZoneSteering;
            screenRollZoneStrength = source.screenRollZoneStrength;
            suppressAutoRollInNoRollZone = source.suppressAutoRollInNoRollZone;
            noRollZoneLevelingBoost = source.noRollZoneLevelingBoost;

            useScreenRollZoneBankHold = source.useScreenRollZoneBankHold;
            maxScreenRollBankAngle = source.maxScreenRollBankAngle;
            bankHoldProportional = source.bankHoldProportional;
            bankHoldRollRateDamping = source.bankHoldRollRateDamping;
            invertScreenRollBankTarget = source.invertScreenRollBankTarget;

            useContinuousScreenBankHold = source.useContinuousScreenBankHold;
            continuousBankDeadzone = source.continuousBankDeadzone;
            continuousBankFullAtX = source.continuousBankFullAtX;
            continuousBankExponent = source.continuousBankExponent;

            useScreenPitchZoneSteering = source.useScreenPitchZoneSteering;
            screenPitchZoneStrength = source.screenPitchZoneStrength;
            suppressAutoPitchInNoPitchZone = source.suppressAutoPitchInNoPitchZone;
            noPitchZoneLevelingBoost = source.noPitchZoneLevelingBoost;
        }

        public void CopyTuningToJet(MavMouseFlightJet target)
        {
            if (target == null)
                return;

            target.sensitivity = sensitivity;
            target.aggressiveTurnAngle = aggressiveTurnAngle;
            target.enableMouseFlightAutopilot = enableMouseFlightAutopilot;
            target.pitchGain = pitchGain;
            target.yawGain = yawGain;
            target.rollGain = rollGain;
            target.maxAutoPitch = maxAutoPitch;
            target.noseDownTrim = noseDownTrim;
            target.inputSmoothing = inputSmoothing;

            target.useMousePitchComfort = useMousePitchComfort;
            target.mousePitchDeadzoneY = mousePitchDeadzoneY;
            target.mousePitchFullAtY = mousePitchFullAtY;
            target.mousePitchExponent = mousePitchExponent;
            target.centerPitchLevelStrength = centerPitchLevelStrength;
            target.noseHighPitchDownAssist = noseHighPitchDownAssist;
            target.noseHighAssistStartAngle = noseHighAssistStartAngle;
            target.noseHighAssistFullAngle = noseHighAssistFullAngle;

            target.bestTurnSpeed = bestTurnSpeed;
            target.turnBandWidth = turnBandWidth;
            target.bestTurnPitchBoost = bestTurnPitchBoost;
            target.bestTurnRollBoost = bestTurnRollBoost;
            target.lowSpeedTurnPenalty = lowSpeedTurnPenalty;
            target.highSpeedTurnPenalty = highSpeedTurnPenalty;
            target.manualPitchBoost = manualPitchBoost;
            target.manualRollBoost = manualRollBoost;

            target.useSpeedAuthorityCurve = useSpeedAuthorityCurve;
            target.lowSpeedPitchAuthority = lowSpeedPitchAuthority;
            target.bestSpeedPitchAuthority = bestSpeedPitchAuthority;
            target.highSpeedPitchAuthority = highSpeedPitchAuthority;
            target.lowSpeedRollAuthority = lowSpeedRollAuthority;
            target.bestSpeedRollAuthority = bestSpeedRollAuthority;
            target.highSpeedRollAuthority = highSpeedRollAuthority;

            target.useGLimiter = useGLimiter;
            target.softGLimit = softGLimit;
            target.hardGLimit = hardGLimit;
            target.gPitchReduction = gPitchReduction;
            target.useLowSpeedNoseDownAssist = useLowSpeedNoseDownAssist;
            target.stallAssistSpeed = stallAssistSpeed;
            target.stallNoseDownAssist = stallNoseDownAssist;
            target.stallAssistMaxPitchClamp = stallAssistMaxPitchClamp;
            target.aoaSoftLimitDeg = aoaSoftLimitDeg;
            target.aoaHardLimitDeg = aoaHardLimitDeg;
            target.aoaPitchReduction = aoaPitchReduction;
            target.highGShortTermAllowance = highGShortTermAllowance;
            target.sustainedGLimit = sustainedGLimit;

            target.attitudeStabilizer = attitudeStabilizer;
            target.rateDampingStrength = rateDampingStrength;
            target.rollLevelStrength = rollLevelStrength;
            target.pitchRecoveryStrength = pitchRecoveryStrength;
            target.pilotInputSuppress = pilotInputSuppress;
            target.maxStabilizerTorque = maxStabilizerTorque;

            target.centerAimAngleForLeveling = centerAimAngleForLeveling;
            target.activeTurnLevelingMultiplier = activeTurnLevelingMultiplier;
            target.centerLevelingMultiplier = centerLevelingMultiplier;
            target.freeLookLevelingMultiplier = freeLookLevelingMultiplier;

            target.yawDamper = yawDamper;
            target.yawRateDampingStrength = yawRateDampingStrength;
            target.maxAutoYawCommand = maxAutoYawCommand;
            target.preferBankTurnOverYaw = preferBankTurnOverYaw;

            target.enableCoordinatedYawAssist = enableCoordinatedYawAssist;
            target.aosYawAssistStrength = aosYawAssistStrength;
            target.maxAutoRudderAssist = maxAutoRudderAssist;
            target.coordinatedYawSpeedMin = coordinatedYawSpeedMin;
            target.coordinatedYawFullSpeed = coordinatedYawFullSpeed;
            target.coordinatedYawBankFactor = coordinatedYawBankFactor;
            target.coordinatedYawTurnDemandFactor = coordinatedYawTurnDemandFactor;
            target.coordinatedYawDamping = coordinatedYawDamping;

            target.autoSpeedAssist = autoSpeedAssist;
            target.targetCruiseSpeed = targetCruiseSpeed;
            target.minCombatSpeed = minCombatSpeed;
            target.maxCombatSpeed = maxCombatSpeed;
            target.speedAssistStrength = speedAssistStrength;
            target.overspeedDrag = overspeedDrag;
            target.lowSpeedThrustBoost = lowSpeedThrustBoost;

            target.throttle = throttleIntent;
            target.throttleChangeRate = throttleChangeRate;
            target.maxThrottle = maxThrottle;

            target.keyboardOverride = keyboardOverride;
            target.invertKeyboardRoll = invertKeyboardRoll;
            target.invertKeyboardPitch = invertKeyboardPitch;
            target.invertKeyboardYaw = invertKeyboardYaw;
            target.keyboardRollThreshold = keyboardRollThreshold;
            target.keyboardPitchThreshold = keyboardPitchThreshold;
            target.pitchUpCommand = pitchUpCommand;
            target.pitchDownCommand = pitchDownCommand;
            target.keyboardYawAuthority = keyboardYawAuthority;
            target.keyboardPitchSuppressesMouseAim = keyboardPitchSuppressesMouseAim;

            target.useWTKeyboardElevatorOverride = useWTKeyboardElevatorOverride;
            target.keyboardElevatorMouseBlend = keyboardElevatorMouseBlend;
            target.keyboardElevatorResponse = keyboardElevatorResponse;
            target.keyboardElevatorReleaseBlend = keyboardElevatorReleaseBlend;
            target.keyboardElevatorRateDamping = keyboardElevatorRateDamping;
            target.keyboardElevatorUsesGLimit = keyboardElevatorUsesGLimit;

            target.useScreenRollZoneSteering = useScreenRollZoneSteering;
            target.screenRollZoneStrength = screenRollZoneStrength;
            target.suppressAutoRollInNoRollZone = suppressAutoRollInNoRollZone;
            target.noRollZoneLevelingBoost = noRollZoneLevelingBoost;

            target.useScreenRollZoneBankHold = useScreenRollZoneBankHold;
            target.maxScreenRollBankAngle = maxScreenRollBankAngle;
            target.bankHoldProportional = bankHoldProportional;
            target.bankHoldRollRateDamping = bankHoldRollRateDamping;
            target.invertScreenRollBankTarget = invertScreenRollBankTarget;

            target.useContinuousScreenBankHold = useContinuousScreenBankHold;
            target.continuousBankDeadzone = continuousBankDeadzone;
            target.continuousBankFullAtX = continuousBankFullAtX;
            target.continuousBankExponent = continuousBankExponent;

            target.useScreenPitchZoneSteering = useScreenPitchZoneSteering;
            target.screenPitchZoneStrength = screenPitchZoneStrength;
            target.suppressAutoPitchInNoPitchZone = suppressAutoPitchInNoPitchZone;
            target.noPitchZoneLevelingBoost = noPitchZoneLevelingBoost;
        }

        public void MirrorRuntimeToJet()
        {
            if (jet == null)
                return;

            jet.pitch = pitch;
            jet.yaw = yaw;
            jet.roll = roll;
            jet.throttle = throttleIntent;

            jet.flyTarget = flyTarget;
            jet.localFlyTarget = localFlyTarget;
            jet.angleOffTarget = angleOffTarget;
            jet.aggressiveRoll = aggressiveRoll;
            jet.wingsLevelRoll = wingsLevelRoll;
            jet.wingsLevelInfluence = wingsLevelInfluence;
            jet.autoPitchRaw = autoPitchRaw;
            jet.turnBandFactor = turnBandFactor;
            jet.pitchTorqueFactor = pitchTorqueFactor;
            jet.rollTorqueFactor = rollTorqueFactor;
            jet.pitchAuthorityFactor = pitchAuthorityFactor;
            jet.rollAuthorityFactor = rollAuthorityFactor;
            jet.signedBankAngle = signedBankAngle;
            jet.targetBankAngle = targetBankAngle;
            jet.bankHoldRollCommand = bankHoldRollCommand;
            jet.signedPitchAngle = signedPitchAngle;
            jet.localAngularVelocity = localAngularVelocity;
            jet.instructorState = instructorState;
            jet.state = state;
            jet.speedRegime = speedRegime;
            jet.aoaEstimateDeg = aoaEstimateDeg;
            jet.aosEstimateDeg = aosEstimateDeg;
            jet.verticalSpeed = verticalSpeed;
            jet.localVelocity = localVelocity;
            jet.velocityPitchAngleDeg = velocityPitchAngleDeg;
            jet.velocityYawAngleDeg = velocityYawAngleDeg;
            jet.coordinatedYawAssistOutput = coordinatedYawAssistOutput;
        }

        private void RefreshAttitudeTelemetry()
        {
            if (rb != null)
                speed = rb.linearVelocity.magnitude;

            UpdateAeroTelemetry();

            signedBankAngle = SignedBankAngle();
            signedPitchAngle = SignedPitchAngle();

            if (rb != null)
                localAngularVelocity = transform.InverseTransformDirection(rb.angularVelocity);
        }

        private void UpdateAeroTelemetry()
        {
            if (rb == null)
            {
                localVelocity = Vector3.zero;
                verticalSpeed = 0f;
                aoaEstimateDeg = 0f;
                aosEstimateDeg = 0f;
                velocityPitchAngleDeg = 0f;
                velocityYawAngleDeg = 0f;
                return;
            }

            Vector3 worldVel = rb.linearVelocity;
            verticalSpeed = worldVel.y;
            localVelocity = transform.InverseTransformDirection(worldVel);

            if (localVelocity.sqrMagnitude < 0.001f)
            {
                aoaEstimateDeg = 0f;
                aosEstimateDeg = 0f;
                velocityPitchAngleDeg = 0f;
                velocityYawAngleDeg = 0f;
                return;
            }

            float forwardSpeed = Mathf.Max(0.1f, Mathf.Abs(localVelocity.z));
            aoaEstimateDeg = Mathf.Atan2(-localVelocity.y, forwardSpeed) * Mathf.Rad2Deg;
            aosEstimateDeg = Mathf.Atan2(localVelocity.x, forwardSpeed) * Mathf.Rad2Deg;
            velocityPitchAngleDeg = aoaEstimateDeg;
            velocityYawAngleDeg = aosEstimateDeg;
        }

        private void UpdateThrottle()
        {
            bool throttleKeyHeld =
                MavFreshInput.GetKey(KeyCode.LeftShift) ||
                MavFreshInput.GetKey(KeyCode.LeftControl) ||
                MavFreshInput.GetKey(KeyCode.X);

            if (!throttleKeyHeld && jet != null && Mathf.Abs(jet.throttle - throttleIntent) > 0.001f)
                throttleIntent = Mathf.Clamp(jet.throttle, 0f, maxThrottle);

            if (MavFreshInput.GetKey(KeyCode.LeftShift))
                throttleIntent += throttleChangeRate * Time.deltaTime;

            if (MavFreshInput.GetKey(KeyCode.LeftControl))
                throttleIntent -= throttleChangeRate * Time.deltaTime;

            if (MavFreshInput.GetKey(KeyCode.X))
                throttleIntent = Mathf.MoveTowards(throttleIntent, 0.05f, throttleChangeRate * 3f * Time.deltaTime);

            throttleIntent = Mathf.Clamp(throttleIntent, 0f, maxThrottle);
        }

        private void UpdateControls()
        {
            RefreshAttitudeTelemetry();

            rollOverride = false;
            pitchOverride = false;

            float keyboardRoll = 0f;
            float keyboardPitch = 0f;
            float keyboardYaw = 0f;

            if (keyboardOverride)
            {
                if (MavFreshInput.GetKey(KeyCode.A) || MavFreshInput.GetKey(KeyCode.LeftArrow)) keyboardRoll -= 1f;
                if (MavFreshInput.GetKey(KeyCode.D) || MavFreshInput.GetKey(KeyCode.RightArrow)) keyboardRoll += 1f;
                if (invertKeyboardRoll) keyboardRoll = -keyboardRoll;
                if (Mathf.Abs(keyboardRoll) > keyboardRollThreshold) rollOverride = true;

                debugWPressed = MavFreshInput.GetKey(KeyCode.W) || MavFreshInput.GetKey(KeyCode.UpArrow);
                debugSPressed = MavFreshInput.GetKey(KeyCode.S) || MavFreshInput.GetKey(KeyCode.DownArrow);

                if (debugWPressed)
                    keyboardPitch = pitchUpCommand;

                if (debugSPressed)
                    keyboardPitch = pitchDownCommand;

                if (invertKeyboardPitch) keyboardPitch = -keyboardPitch;
                debugKeyboardPitchInput = keyboardPitch;
                if (Mathf.Abs(keyboardPitch) > keyboardPitchThreshold) pitchOverride = true;

                if (MavFreshInput.GetKey(KeyCode.Q)) keyboardYaw -= 1f;
                if (MavFreshInput.GetKey(KeyCode.E)) keyboardYaw += 1f;
                if (invertKeyboardYaw) keyboardYaw = -keyboardYaw;
            }
            else
            {
                debugWPressed = false;
                debugSPressed = false;
                debugKeyboardPitchInput = 0f;
            }

            float autoYaw = 0f;
            float autoPitch = 0f;
            float autoRoll = 0f;

            if (enableMouseFlightAutopilot && rig != null)
                RunAutopilot(rig.MouseAimPos, out autoYaw, out autoPitch, out autoRoll);

            float targetYaw = Mathf.Clamp(autoYaw, -maxAutoYawCommand, maxAutoYawCommand) + keyboardYaw * keyboardYawAuthority;

            if (yawDamper && rb != null)
            {
                localAngularVelocity = transform.InverseTransformDirection(rb.angularVelocity);
                float yawDampingSignal = -localAngularVelocity.y * yawRateDampingStrength;
                targetYaw += Mathf.Clamp(yawDampingSignal, -0.8f, 0.8f);
            }

            float provisionalRollForYaw = rollOverride ? keyboardRoll : autoRoll;
            targetYaw += ComputeCoordinatedYawAssist(provisionalRollForYaw, autoYaw);
            targetYaw = Mathf.Clamp(targetYaw, -1f, 1f);

            float targetPitch = autoPitch;

            if (useWTKeyboardElevatorOverride)
            {
                float response = pitchOverride ? keyboardElevatorResponse : keyboardElevatorReleaseBlend;
                float k = 1f - Mathf.Exp(-response * Time.deltaTime);
                smKeyboardPitch = Mathf.Lerp(smKeyboardPitch, pitchOverride ? keyboardPitch : 0f, k);

                if (pitchOverride)
                    targetPitch = Mathf.Clamp(smKeyboardPitch + autoPitch * keyboardElevatorMouseBlend, -1f, 1f);
            }
            else
            {
                targetPitch = pitchOverride ? keyboardPitch : autoPitch;
            }

            if (!pitchOverride)
                targetPitch = autoPitch;

            float targetRoll = rollOverride ? keyboardRoll : autoRoll;

            ApplyProtectionAssists(ref targetPitch);
            ApplyStabilizer(ref targetPitch, ref targetYaw, ref targetRoll);
            debugFinalPitchBeforeSmoothing = Mathf.Clamp(targetPitch, -1f, 1f);

            if (keyboardPitchSuppressesMouseAim && pitchOverride)
            {
                float k = 1f - Mathf.Exp(-inputSmoothing * 2.15f * Time.deltaTime);
                smPitch = Mathf.Lerp(smPitch, targetPitch, k);
            }

            if (inputSmoothing > 0f)
            {
                float a = 1f - Mathf.Exp(-inputSmoothing * Time.deltaTime);
                smPitch = Mathf.Lerp(smPitch, targetPitch, a);
                smYaw = Mathf.Lerp(smYaw, targetYaw, a);
                smRoll = Mathf.Lerp(smRoll, targetRoll, a);
                pitch = Mathf.Clamp(smPitch, -1f, 1f);
                yaw = Mathf.Clamp(smYaw, -1f, 1f);
                roll = Mathf.Clamp(smRoll, -1f, 1f);
            }
            else
            {
                pitch = Mathf.Clamp(targetPitch, -1f, 1f);
                yaw = Mathf.Clamp(targetYaw, -1f, 1f);
                roll = Mathf.Clamp(targetRoll, -1f, 1f);
            }

            debugFinalPitchAfterSmoothing = pitch;
            state = enableMouseFlightAutopilot ? "wt_instructor" : "direct";
        }

        private float ComputeCoordinatedYawAssist(float targetRoll, float autoYaw)
        {
            coordinatedYawAssistOutput = 0f;

            if (!enableCoordinatedYawAssist || speed < coordinatedYawSpeedMin)
                return 0f;

            float aosAbs = Mathf.Abs(aosEstimateDeg);
            if (aosAbs < 0.15f)
                return 0f;

            float speedT = Mathf.InverseLerp(coordinatedYawSpeedMin, Mathf.Max(coordinatedYawSpeedMin + 0.1f, coordinatedYawFullSpeed), speed);
            float slipT = Mathf.Clamp01(aosAbs / 12f);
            float bankT = Mathf.Clamp01(Mathf.Abs(signedBankAngle) / 75f);
            float turnDemandT = Mathf.Clamp01(Mathf.Max(Mathf.Abs(targetRoll), Mathf.Abs(autoYaw), Mathf.Abs(aggressiveRoll)));

            float context = Mathf.Clamp01(0.35f + bankT * coordinatedYawBankFactor + turnDemandT * coordinatedYawTurnDemandFactor);

            if (pitchOverride && turnDemandT < 0.08f)
                context *= 0.35f;

            float desired = -aosEstimateDeg * aosYawAssistStrength;
            desired *= speedT * Mathf.Lerp(0.35f, 1f, slipT) * context;

            if (rb != null)
            {
                Vector3 localAV = transform.InverseTransformDirection(rb.angularVelocity);
                desired += -localAV.y * coordinatedYawDamping * 0.15f;
            }

            coordinatedYawAssistOutput = Mathf.Clamp(desired, -maxAutoRudderAssist, maxAutoRudderAssist);
            return coordinatedYawAssistOutput;
        }

        private void ApplyProtectionAssists(ref float targetPitch)
        {
            instructorState = "normal";

            if (useLowSpeedNoseDownAssist && speed < stallAssistSpeed && !pitchOverride)
            {
                float stallT = 1f - Mathf.Clamp01(speed / Mathf.Max(1f, stallAssistSpeed));
                targetPitch = Mathf.Clamp(targetPitch + stallNoseDownAssist * stallT, -stallAssistMaxPitchClamp, stallAssistMaxPitchClamp);
                instructorState = "stall_assist";
            }

            float aoaAbs = Mathf.Abs(aoaEstimateDeg);
            if (targetPitch < 0f && aoaAbs > aoaSoftLimitDeg)
            {
                float aoaT = Mathf.InverseLerp(aoaSoftLimitDeg, Mathf.Max(aoaSoftLimitDeg + 0.1f, aoaHardLimitDeg), aoaAbs);
                float reduction = Mathf.Lerp(1f, aoaPitchReduction, Mathf.Clamp01(aoaT));
                targetPitch *= reduction;
                instructorState = "aoa_limiter";
            }

            if (useGLimiter && gEstimate > sustainedGLimit && targetPitch < 0f)
            {
                float gT = Mathf.InverseLerp(sustainedGLimit, hardGLimit, gEstimate);
                targetPitch = Mathf.Lerp(targetPitch, targetPitch * gPitchReduction, gT);
                instructorState = "g_limiter";
            }

            if (useGLimiter && gEstimate > softGLimit && gEstimate <= sustainedGLimit && targetPitch < 0f)
            {
                float gT = Mathf.InverseLerp(softGLimit, hardGLimit, gEstimate);
                targetPitch = Mathf.Lerp(targetPitch, targetPitch * gPitchReduction, gT);
                instructorState = "g_limiter";
            }

            if (keyboardElevatorUsesGLimit && pitchOverride && gEstimate > hardGLimit && targetPitch < 0f)
            {
                targetPitch = Mathf.Min(targetPitch * 0.45f, -0.05f);
                instructorState = "manual_g_limiter";
            }
        }

        private void ApplyStabilizer(ref float targetPitch, ref float targetYaw, ref float targetRoll)
        {
            if (!attitudeStabilizer || rb == null)
                return;

            localAngularVelocity = transform.InverseTransformDirection(rb.angularVelocity);

            float aimCenterFactor = 1f - Mathf.Clamp01(angleOffTarget / Mathf.Max(0.1f, centerAimAngleForLeveling));

            bool hasRollZones = rig != null && rig.useScreenRollZones && useScreenRollZoneSteering;
            if (hasRollZones)
                aimCenterFactor = rig.isInRollZone ? 0f : 1f;

            float turnMultiplier = Mathf.Lerp(activeTurnLevelingMultiplier, centerLevelingMultiplier, aimCenterFactor);

            if (hasRollZones && !rig.isInRollZone)
                turnMultiplier *= noRollZoneLevelingBoost;

            if (rig != null && rig.freeLookHeld)
                turnMultiplier *= freeLookLevelingMultiplier;

            float pilotRollSuppression = Mathf.Clamp01(Mathf.Abs(targetRoll) * pilotInputSuppress);
            float levelFactor = Mathf.Clamp01((1f - pilotRollSuppression) * turnMultiplier);

            if (!rollOverride)
            {
                float rollLevelSignal = Mathf.Clamp(transform.right.y, -1f, 1f) * rollLevelStrength * levelFactor;
                targetRoll += rollLevelSignal;
            }

            if (!pitchOverride)
            {
                float pitchFactor = Mathf.Lerp(0.55f, 1f, aimCenterFactor);
                float pitchRecoverySignal = Mathf.Clamp(-signedPitchAngle / 55f, -1f, 1f) * pitchRecoveryStrength * pitchFactor;

                if (signedPitchAngle > noseHighAssistStartAngle)
                {
                    float highT = Mathf.InverseLerp(noseHighAssistStartAngle, noseHighAssistFullAngle, signedPitchAngle);
                    pitchRecoverySignal += noseHighPitchDownAssist * highT;
                }

                targetPitch += pitchRecoverySignal;
            }

            float maxStabCommand = Mathf.Clamp(maxStabilizerTorque / 160f, 0.05f, 1.5f);
            float pitchRateDamping = pitchOverride ? keyboardElevatorRateDamping : rateDampingStrength;
            targetPitch += Mathf.Clamp(-localAngularVelocity.x * pitchRateDamping, -maxStabCommand, maxStabCommand);
            targetRoll += Mathf.Clamp(localAngularVelocity.z * rateDampingStrength, -maxStabCommand, maxStabCommand);
        }

        private void RunAutopilot(Vector3 target, out float outYaw, out float outPitch, out float outRoll)
        {
            flyTarget = target;

            localFlyTarget = transform.InverseTransformPoint(flyTarget).normalized * sensitivity;
            angleOffTarget = Vector3.Angle(transform.forward, flyTarget - transform.position);

            float rawYaw = Mathf.Clamp(localFlyTarget.x, -1f, 1f) * yawGain;
            if (preferBankTurnOverYaw)
                rawYaw *= 0.42f;
            outYaw = Mathf.Clamp(rawYaw, -maxAutoYawCommand, maxAutoYawCommand);

            autoPitchRaw = -Mathf.Clamp(localFlyTarget.y, -1f, 1f) * pitchGain;

            if (useMousePitchComfort && rig != null)
            {
                float yOffset = rig.cursorViewport.y - 0.5f;
                float absY = Mathf.Abs(yOffset);

                if (absY <= mousePitchDeadzoneY)
                {
                    autoPitchRaw = Mathf.Lerp(autoPitchRaw, 0f, centerPitchLevelStrength);
                }
                else
                {
                    float t = Mathf.InverseLerp(mousePitchDeadzoneY, Mathf.Max(mousePitchDeadzoneY + 0.001f, mousePitchFullAtY), absY);
                    t = Mathf.Clamp01(t);
                    t = Mathf.Pow(t, mousePitchExponent);
                    float zonePitch = -Mathf.Sign(yOffset) * t * pitchGain;
                    autoPitchRaw = Mathf.Lerp(autoPitchRaw, zonePitch, 0.72f);
                }
            }

            outPitch = Mathf.Clamp(autoPitchRaw + noseDownTrim, -maxAutoPitch, maxAutoPitch);

            if (!pitchOverride && signedPitchAngle > noseHighAssistStartAngle)
            {
                float highT = Mathf.InverseLerp(noseHighAssistStartAngle, noseHighAssistFullAngle, signedPitchAngle);
                outPitch = Mathf.Clamp(outPitch + noseHighPitchDownAssist * highT, -maxAutoPitch, maxAutoPitch);
            }

            float rawAggressiveRoll = Mathf.Clamp(localFlyTarget.x, -1f, 1f);
            bool hasRollZones = rig != null && rig.useScreenRollZones && useScreenRollZoneSteering;

            if (hasRollZones)
                aggressiveRoll = rig.screenRollCommand * screenRollZoneStrength;
            else
                aggressiveRoll = rawAggressiveRoll;

            wingsLevelRoll = transform.right.y;
            wingsLevelInfluence = Mathf.InverseLerp(0f, aggressiveTurnAngle, angleOffTarget);

            if (rig != null && useContinuousScreenBankHold)
            {
                aggressiveRoll = ComputeContinuousScreenBankCommand() * screenRollZoneStrength;
                outRoll = ComputeScreenZoneBankHoldRoll(aggressiveRoll, Mathf.Abs(aggressiveRoll) > 0.001f) * rollGain;
            }
            else if (hasRollZones && useScreenRollZoneBankHold)
            {
                outRoll = ComputeScreenZoneBankHoldRoll(aggressiveRoll, rig.isInRollZone) * rollGain;
            }
            else if (hasRollZones && suppressAutoRollInNoRollZone && !rig.isInRollZone)
            {
                outRoll = wingsLevelRoll * 0.82f * rollGain;
                targetBankAngle = 0f;
                bankHoldRollCommand = outRoll;
            }
            else
            {
                outRoll = Mathf.Lerp(wingsLevelRoll, aggressiveRoll, wingsLevelInfluence) * rollGain;
                targetBankAngle = 0f;
                bankHoldRollCommand = outRoll;
            }
        }

        private float ComputeContinuousScreenBankCommand()
        {
            if (rig == null)
                return 0f;

            float x = rig.cursorOffsetFromCenter.x;
            float absX = Mathf.Abs(x);

            if (absX <= continuousBankDeadzone)
                return 0f;

            float t = Mathf.InverseLerp(
                continuousBankDeadzone,
                Mathf.Max(continuousBankDeadzone + 0.001f, continuousBankFullAtX),
                absX
            );

            t = Mathf.Clamp01(t);
            t = Mathf.Pow(t, continuousBankExponent);

            return Mathf.Sign(x) * t;
        }

        private float ComputeScreenZoneBankHoldRoll(float zoneRollCommand, bool inRollZone)
        {
            float zone = Mathf.Clamp(zoneRollCommand, -1f, 1f);

            if (!inRollZone)
                zone = 0f;

            targetBankAngle = -zone * maxScreenRollBankAngle;
            if (invertScreenRollBankTarget)
                targetBankAngle = -targetBankAngle;

            signedBankAngle = SignedBankAngle();

            Vector3 localAngVel = rb != null
                ? transform.InverseTransformDirection(rb.angularVelocity)
                : Vector3.zero;

            float bankError = signedBankAngle - targetBankAngle;
            float pTerm = bankError * bankHoldProportional;
            float dTerm = -localAngVel.z * bankHoldRollRateDamping;

            bankHoldRollCommand = Mathf.Clamp(pTerm + dTerm, -1f, 1f);
            return bankHoldRollCommand;
        }

        private void ComputeTurnBandFactors()
        {
            float distanceFromBest = Mathf.Abs(speed - bestTurnSpeed);
            turnBandFactor = 1f - Mathf.Clamp01(distanceFromBest / Mathf.Max(1f, turnBandWidth));

            pitchTorqueFactor = Mathf.Lerp(1f, bestTurnPitchBoost, turnBandFactor);
            rollTorqueFactor = Mathf.Lerp(1f, bestTurnRollBoost, turnBandFactor);

            if (useSpeedAuthorityCurve)
            {
                if (speed <= bestTurnSpeed)
                {
                    float t = Mathf.InverseLerp(45f, bestTurnSpeed, speed);
                    pitchAuthorityFactor = Mathf.Lerp(lowSpeedPitchAuthority, bestSpeedPitchAuthority, t);
                    rollAuthorityFactor = Mathf.Lerp(lowSpeedRollAuthority, bestSpeedRollAuthority, t);
                }
                else
                {
                    float t = Mathf.InverseLerp(bestTurnSpeed, maxCombatSpeed + 160f, speed);
                    pitchAuthorityFactor = Mathf.Lerp(bestSpeedPitchAuthority, highSpeedPitchAuthority, t);
                    rollAuthorityFactor = Mathf.Lerp(bestSpeedRollAuthority, highSpeedRollAuthority, t);
                }
            }
            else
            {
                pitchAuthorityFactor = 1f;
                rollAuthorityFactor = 1f;
            }

            if (speed < minCombatSpeed)
            {
                float slow = Mathf.InverseLerp(45f, minCombatSpeed, speed);
                pitchTorqueFactor *= Mathf.Lerp(lowSpeedTurnPenalty, 1f, slow);
                rollTorqueFactor *= Mathf.Lerp(lowSpeedTurnPenalty, 1f, slow);
            }

            if (speed > maxCombatSpeed)
            {
                float fast = Mathf.InverseLerp(maxCombatSpeed, maxCombatSpeed + 220f, speed);
                pitchTorqueFactor *= Mathf.Lerp(1f, highSpeedTurnPenalty, fast);
                rollTorqueFactor *= Mathf.Lerp(1f, highSpeedTurnPenalty, fast);
            }

            if (pitchOverride) pitchTorqueFactor *= manualPitchBoost;
            if (rollOverride) rollTorqueFactor *= manualRollBoost;

            if (speed < minCombatSpeed) speedRegime = "LOW";
            else if (speed > maxCombatSpeed) speedRegime = "FAST";
            else if (Mathf.Abs(speed - bestTurnSpeed) < turnBandWidth) speedRegime = "BEST TURN";
            else if (speed > targetCruiseSpeed) speedRegime = "HIGH";
            else speedRegime = "NORMAL";
        }

        private float SignedBankAngle()
        {
            Vector3 projectedUp = Vector3.ProjectOnPlane(transform.up, transform.forward);
            if (projectedUp.sqrMagnitude < 0.0001f) return 0f;
            return Vector3.SignedAngle(Vector3.up, projectedUp.normalized, transform.forward);
        }

        private float SignedPitchAngle()
        {
            Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (flatForward.sqrMagnitude < 0.0001f) return 0f;
            return Vector3.SignedAngle(flatForward.normalized, transform.forward, transform.right);
        }
    }
}
