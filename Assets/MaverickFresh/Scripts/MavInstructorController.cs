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
        public float pitchGain = 0.82f;
        public float yawGain = 0.24f;
        public float rollGain = 1.25f;
        public float maxAutoPitch = 0.68f;
        public float noseDownTrim = 0.115f;
        public float inputSmoothing = 5.8f;

        [Header("Mouse Pitch Comfort")]
        public bool useMousePitchComfort = true;
        public float mousePitchDeadzoneY = 0.020f;
        public float mousePitchFullAtY = 0.42f;
        [Range(0.5f, 3.0f)] public float mousePitchExponent = 1.25f;
        public float centerPitchLevelStrength = 0.08f;
        public float noseHighPitchDownAssist = 0.10f;
        public float noseHighAssistStartAngle = 8f;
        public float noseHighAssistFullAngle = 24f;

        [Header("F-15-like Turn Assist")]
        public float bestTurnSpeed = 236f;
        public float turnBandWidth = 95f;
        public float bestTurnPitchBoost = 1.18f;
        public float bestTurnRollBoost = 1.10f;
        public float lowSpeedTurnPenalty = 0.62f;
        public float highSpeedTurnPenalty = 0.72f;
        public float manualPitchBoost = 2.75f;
        public float manualRollBoost = 1.05f;

        [Header("v0.18.9 Manual Control Authority")]
        public bool useManualControlAuthorityBoost = true;
        public float manualPitchAuthorityBoost = 1.35f;
        public float manualRollAuthorityBoost = 1.45f;
        public float manualYawAuthorityBoost = 1.15f;
        public float manualPitchResponseMultiplier = 1.7f;
        public float manualRollResponseMultiplier = 1.8f;
        [Range(0f, 1f)] public float manualLimiterBypassFactor = 0.35f;

        [Header("v0.18.11 Manual Authority Bypass")]
        [Range(0f, 1f)] public float manualDampingReduction = 0.45f;
        [Range(0f, 1f)] public float manualEnvelopeBypassFactor = 0.65f;
        [Range(0f, 1f)] public float manualPitchMinAuthority = 0.78f;
        [Range(0f, 1f)] public float manualRollMinAuthority = 0.82f;
        [Range(0f, 1f)] public float manualYawMinAuthority = 0.60f;

        [Header("v0.18.9 Control Surface Actuator")]
        public float controlSurfaceResponse = 7.5f;
        public float controlSurfaceReleaseResponse = 6.5f;
        public float maxPitchCommandRate = 4.8f;
        public float maxYawCommandRate = 2.8f;
        public float maxRollCommandRate = 5.8f;

        [Header("v0.18.14 Rate-Based Heavy Control")]
        public bool useRateBasedControl = true;
        public float targetPitchRateDeg = 48f;
        public float targetYawRateDeg = 18f;
        public float targetRollRateDeg = 72f;
        public Vector3 rateControlP = new Vector3(0.42f, 0.28f, 0.38f);
        public Vector3 rateControlD = new Vector3(0.10f, 0.12f, 0.09f);
        public Vector3 maxRateControlTorque = new Vector3(32f, 14f, 40f);

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

        [Header("v0.18.9 Flight Envelope Protection")]
        public bool useHighSpeedPitchLimiter = true;
        public float pitchLimiterStartSpeed = 310f;
        public float pitchLimiterFullSpeed = 500f;
        public float highSpeedPitchAuthorityMin = 0.55f;
        public float highSpeedManualPitchAuthorityMin = 0.78f;
        public float highSpeedPitchLimiterSmooth = 6f;
        public bool useAoAAoSSoftGuard = true;
        public float aoaSoftGuardStart = 18f;
        public float aoaHardGuardStart = 30f;
        public float aosSoftGuardStart = 10f;
        public float aosHardGuardStart = 24f;
        public float highSlipGuardStart = 25f;
        public float highSlipGuardHard = 55f;
        public float guardPitchReduction = 0.55f;
        public float guardYawReduction = 0.45f;
        public float guardRollReduction = 0.35f;
        public float highSpeedPitchLimiterFactor = 1f;
        public float aoaAosGuardFactor = 1f;

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
        public float coordinatedYawDamping = 0.28f;
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

        [Header("WT Throttle Axis")]
        public float minThrottlePercent = -5f;
        public float idleThrottlePercent = 0f;
        public float militaryThrottlePercent = 100f;
        public float maxThrottlePercent = 110f;
        public float throttlePercent = 95f;
        public bool engineOn = true;
        public KeyCode engineToggleKey = KeyCode.I;
        public KeyCode throttleUpKey = KeyCode.LeftShift;
        public KeyCode throttleDownKey = KeyCode.LeftControl;
        public KeyCode throttleIdleKey = KeyCode.X;
        public float throttleChangeRatePercentPerSecond = 45f;
        public float throttleWheelStepPercent = 5f;
        public float throttleKeyboardStepPercent = 0f;
        public bool useMouseWheelThrottle = true;
        public bool holdThrottleKeysContinuous = true;
        public float idleThrust01 = 0.04f;
        public float negativeThrottleBrakeDrag = 0.018f;
        public float afterburnerStartPercent = 100f;
        public float afterburnerMaxPercent = 110f;
        public float afterburnerThrustMultiplier = 1.35f;
        public float afterburnerFuelBurnMultiplier = 2.5f;
        public bool useAfterburner = true;
        public bool useNegativeThrottleBrakeDrag = true;
        public float throttleSpoolUpRate = 1.8f;
        public float throttleSpoolDownRate = 2.4f;
        public float effectiveThrottle01;
        public float displayedThrottlePercent = 95f;
        public bool afterburnerActive;
        public bool negativeThrottleActive;

        [Header("Keyboard Override")]
        public bool keyboardOverride = true;
        public bool invertKeyboardRoll;
        public bool invertKeyboardPitch;
        public bool invertKeyboardYaw;
        public float keyboardRollThreshold = 0.10f;
        public float keyboardPitchThreshold = 0.02f;
        [Tooltip("MouseFlight convention here: negative pitch usually means nose-up.")]
        public float pitchUpCommand = -2.85f;
        [Tooltip("MouseFlight convention here: positive pitch usually means nose-down.")]
        public float pitchDownCommand = 2.65f;
        public float keyboardYawAuthority = 0.55f;
        public bool keyboardPitchSuppressesMouseAim = true;

        [Header("WarThunder-like Elevator Override")]
        public bool useWTKeyboardElevatorOverride = true;
        [Range(0f, 1f)] public float keyboardElevatorMouseBlend = 0.03f;
        public float keyboardElevatorResponse = 34f;
        public float keyboardElevatorReleaseBlend = 8.5f;
        public float keyboardElevatorRateDamping = 0.045f;
        public bool keyboardElevatorUsesGLimit = true;

        [Header("Screen Roll Zone Steering")]
        public bool useScreenRollZoneSteering = true;
        public float screenRollZoneStrength = 1.0f;
        public bool suppressAutoRollInNoRollZone = true;
        public float noRollZoneLevelingBoost = 1.35f;

        [Header("Screen Roll Zone Bank Hold")]
        public bool useScreenRollZoneBankHold = true;
        public float maxScreenRollBankAngle = 72f;
        public float bankHoldProportional = 0.048f;
        public float bankHoldRollRateDamping = 0.24f;
        public bool invertScreenRollBankTarget = false;

        [Header("Center Roll Stability")]
        public float rollCommandDeadzone = 0.025f;
        public float bankHoldDeadzoneDeg = 2.5f;
        public float rollCommandSlewRate = 12f;
        public float yawAssistDeadzoneAosDeg = 1.2f;
        public float centerRollStabilizeStrength = 0.12f;

        [Header("v0.18.14 Anti-Wobble")]
        public float mouseRollDeadzone = 0.055f;
        public float mouseYawDeadzone = 0.045f;
        public float rollRateDeadzoneDeg = 3.0f;
        public float aosYawAssistDeadzoneDeg = 2.5f;
        public float rollInputEnterDeadzone = 0.065f;
        public float rollInputExitDeadzone = 0.040f;

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
        public float forwardVelocityAlignment = 1f;
        public float forwardVelocityAngleDeg;
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
        public bool debugAPressed;
        public bool debugDPressed;
        public float debugManualPitchInput;
        public float debugManualRollInput;
        public float debugRawPitchCommand;
        public float debugRawYawCommand;
        public float debugRawRollCommand;
        public float debugFinalPitchCommand;
        public float debugFinalYawCommand;
        public float debugFinalRollCommand;
        public float debugPitchAuthorityFactor = 1f;
        public float debugRollAuthorityFactor = 1f;
        public string debugControlState = "ready";
        public bool debugManualPitchBoostActive;
        public bool debugManualRollBoostActive;
        public bool debugManualYawBoostActive;
        public float debugKeyboardPitchInput;
        public float debugMousePitchInput;
        public float debugMouseYawInput;
        public float debugFinalPitchBeforeSmoothing;
        public float debugFinalPitchAfterSmoothing;
        public float debugFinalYawBeforeSmoothing;
        public float debugFinalYawAfterSmoothing;
        public float debugFinalRollBeforeSmoothing;
        public float debugFinalRollAfterSmoothing;
        public bool debugRollHysteresisActive;
        public bool debugManualPitchActive;
        public bool debugManualRollActive;
        public bool debugManualYawActive;
        public bool debugMouseCenterQuiet;

        private bool rollOverride;
        private bool pitchOverride;
        private bool yawOverride;
        private float smPitch;
        private float smYaw;
        private float smRoll;
        private float smKeyboardPitch;
        private float smHighSpeedPitchLimiterFactor = 1f;
        private float slewedRollCommand;
        private bool rollHysteresisActive;
        private float previousPositiveThrottlePercent = 95f;
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
            float percent = Mathf.Abs(value) > 2f
                ? value
                : value * militaryThrottlePercent;

            SetThrottlePercent(percent);
        }

        public void SetThrottlePercent(float percent)
        {
            throttlePercent = Mathf.Clamp(percent, minThrottlePercent, maxThrottlePercent);
            if (throttlePercent > idleThrottlePercent)
                previousPositiveThrottlePercent = throttlePercent;

            displayedThrottlePercent = throttlePercent;
            throttleIntent = PercentToLegacyThrottle(throttlePercent);

            if (jet != null)
            {
                jet.throttle = throttleIntent;
                jet.throttlePercent = throttlePercent;
                jet.displayedThrottlePercent = displayedThrottlePercent;
            }
        }

        public bool ManualPitchActive { get { return pitchOverride; } }
        public bool ManualRollActive { get { return rollOverride; } }
        public bool ManualYawActive { get { return yawOverride; } }

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
            useManualControlAuthorityBoost = source.useManualControlAuthorityBoost;
            manualPitchAuthorityBoost = source.manualPitchAuthorityBoost;
            manualRollAuthorityBoost = source.manualRollAuthorityBoost;
            manualYawAuthorityBoost = source.manualYawAuthorityBoost;
            manualPitchResponseMultiplier = source.manualPitchResponseMultiplier;
            manualRollResponseMultiplier = source.manualRollResponseMultiplier;
            manualLimiterBypassFactor = source.manualLimiterBypassFactor;
            manualDampingReduction = source.manualDampingReduction;
            manualEnvelopeBypassFactor = source.manualEnvelopeBypassFactor;
            manualPitchMinAuthority = source.manualPitchMinAuthority;
            manualRollMinAuthority = source.manualRollMinAuthority;
            manualYawMinAuthority = source.manualYawMinAuthority;
            controlSurfaceResponse = source.controlSurfaceResponse;
            controlSurfaceReleaseResponse = source.controlSurfaceReleaseResponse;
            maxPitchCommandRate = source.maxPitchCommandRate;
            maxYawCommandRate = source.maxYawCommandRate;
            maxRollCommandRate = source.maxRollCommandRate;

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

            useHighSpeedPitchLimiter = source.useHighSpeedPitchLimiter;
            pitchLimiterStartSpeed = source.pitchLimiterStartSpeed;
            pitchLimiterFullSpeed = source.pitchLimiterFullSpeed;
            highSpeedPitchAuthorityMin = source.highSpeedPitchAuthorityMin;
            highSpeedManualPitchAuthorityMin = source.highSpeedManualPitchAuthorityMin;
            highSpeedPitchLimiterSmooth = source.highSpeedPitchLimiterSmooth;
            useAoAAoSSoftGuard = source.useAoAAoSSoftGuard;
            aoaSoftGuardStart = source.aoaSoftGuardStart;
            aoaHardGuardStart = source.aoaHardGuardStart;
            aosSoftGuardStart = source.aosSoftGuardStart;
            aosHardGuardStart = source.aosHardGuardStart;
            highSlipGuardStart = source.highSlipGuardStart;
            highSlipGuardHard = source.highSlipGuardHard;
            guardPitchReduction = source.guardPitchReduction;
            guardYawReduction = source.guardYawReduction;
            guardRollReduction = source.guardRollReduction;

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

            throttleChangeRate = source.throttleChangeRate;
            maxThrottle = source.maxThrottle;
            minThrottlePercent = source.minThrottlePercent;
            idleThrottlePercent = source.idleThrottlePercent;
            militaryThrottlePercent = source.militaryThrottlePercent;
            maxThrottlePercent = source.maxThrottlePercent;
            throttlePercent = source.throttlePercent;
            displayedThrottlePercent = source.displayedThrottlePercent;
            engineOn = source.engineOn;
            engineToggleKey = source.engineToggleKey;
            throttleUpKey = source.throttleUpKey;
            throttleDownKey = source.throttleDownKey;
            throttleIdleKey = source.throttleIdleKey;
            throttleChangeRatePercentPerSecond = source.throttleChangeRatePercentPerSecond;
            throttleWheelStepPercent = source.throttleWheelStepPercent;
            throttleKeyboardStepPercent = source.throttleKeyboardStepPercent;
            useMouseWheelThrottle = source.useMouseWheelThrottle;
            holdThrottleKeysContinuous = source.holdThrottleKeysContinuous;
            idleThrust01 = source.idleThrust01;
            negativeThrottleBrakeDrag = source.negativeThrottleBrakeDrag;
            afterburnerStartPercent = source.afterburnerStartPercent;
            afterburnerMaxPercent = source.afterburnerMaxPercent;
            afterburnerThrustMultiplier = source.afterburnerThrustMultiplier;
            afterburnerFuelBurnMultiplier = source.afterburnerFuelBurnMultiplier;
            useAfterburner = source.useAfterburner;
            useNegativeThrottleBrakeDrag = source.useNegativeThrottleBrakeDrag;
            throttleSpoolUpRate = source.throttleSpoolUpRate;
            throttleSpoolDownRate = source.throttleSpoolDownRate;
            effectiveThrottle01 = source.effectiveThrottle01;
            afterburnerActive = source.afterburnerActive;
            negativeThrottleActive = source.negativeThrottleActive;
            if (throttlePercent > idleThrottlePercent)
                previousPositiveThrottlePercent = throttlePercent;
            throttleIntent = PercentToLegacyThrottle(throttlePercent);

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

            rollCommandDeadzone = source.rollCommandDeadzone;
            bankHoldDeadzoneDeg = source.bankHoldDeadzoneDeg;
            rollCommandSlewRate = source.rollCommandSlewRate;
            yawAssistDeadzoneAosDeg = source.yawAssistDeadzoneAosDeg;
            centerRollStabilizeStrength = source.centerRollStabilizeStrength;

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
            target.useManualControlAuthorityBoost = useManualControlAuthorityBoost;
            target.manualPitchAuthorityBoost = manualPitchAuthorityBoost;
            target.manualRollAuthorityBoost = manualRollAuthorityBoost;
            target.manualYawAuthorityBoost = manualYawAuthorityBoost;
            target.manualPitchResponseMultiplier = manualPitchResponseMultiplier;
            target.manualRollResponseMultiplier = manualRollResponseMultiplier;
            target.manualLimiterBypassFactor = manualLimiterBypassFactor;
            target.manualDampingReduction = manualDampingReduction;
            target.manualEnvelopeBypassFactor = manualEnvelopeBypassFactor;
            target.manualPitchMinAuthority = manualPitchMinAuthority;
            target.manualRollMinAuthority = manualRollMinAuthority;
            target.manualYawMinAuthority = manualYawMinAuthority;
            target.controlSurfaceResponse = controlSurfaceResponse;
            target.controlSurfaceReleaseResponse = controlSurfaceReleaseResponse;
            target.maxPitchCommandRate = maxPitchCommandRate;
            target.maxYawCommandRate = maxYawCommandRate;
            target.maxRollCommandRate = maxRollCommandRate;

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

            target.useHighSpeedPitchLimiter = useHighSpeedPitchLimiter;
            target.pitchLimiterStartSpeed = pitchLimiterStartSpeed;
            target.pitchLimiterFullSpeed = pitchLimiterFullSpeed;
            target.highSpeedPitchAuthorityMin = highSpeedPitchAuthorityMin;
            target.highSpeedManualPitchAuthorityMin = highSpeedManualPitchAuthorityMin;
            target.highSpeedPitchLimiterSmooth = highSpeedPitchLimiterSmooth;
            target.useAoAAoSSoftGuard = useAoAAoSSoftGuard;
            target.aoaSoftGuardStart = aoaSoftGuardStart;
            target.aoaHardGuardStart = aoaHardGuardStart;
            target.aosSoftGuardStart = aosSoftGuardStart;
            target.aosHardGuardStart = aosHardGuardStart;
            target.highSlipGuardStart = highSlipGuardStart;
            target.highSlipGuardHard = highSlipGuardHard;
            target.guardPitchReduction = guardPitchReduction;
            target.guardYawReduction = guardYawReduction;
            target.guardRollReduction = guardRollReduction;

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
            target.minThrottlePercent = minThrottlePercent;
            target.idleThrottlePercent = idleThrottlePercent;
            target.militaryThrottlePercent = militaryThrottlePercent;
            target.maxThrottlePercent = maxThrottlePercent;
            target.throttlePercent = throttlePercent;
            target.displayedThrottlePercent = displayedThrottlePercent;
            target.engineOn = engineOn;
            target.engineToggleKey = engineToggleKey;
            target.throttleUpKey = throttleUpKey;
            target.throttleDownKey = throttleDownKey;
            target.throttleIdleKey = throttleIdleKey;
            target.throttleChangeRatePercentPerSecond = throttleChangeRatePercentPerSecond;
            target.throttleWheelStepPercent = throttleWheelStepPercent;
            target.throttleKeyboardStepPercent = throttleKeyboardStepPercent;
            target.useMouseWheelThrottle = useMouseWheelThrottle;
            target.holdThrottleKeysContinuous = holdThrottleKeysContinuous;
            target.idleThrust01 = idleThrust01;
            target.negativeThrottleBrakeDrag = negativeThrottleBrakeDrag;
            target.afterburnerStartPercent = afterburnerStartPercent;
            target.afterburnerMaxPercent = afterburnerMaxPercent;
            target.afterburnerThrustMultiplier = afterburnerThrustMultiplier;
            target.afterburnerFuelBurnMultiplier = afterburnerFuelBurnMultiplier;
            target.useAfterburner = useAfterburner;
            target.useNegativeThrottleBrakeDrag = useNegativeThrottleBrakeDrag;
            target.throttleSpoolUpRate = throttleSpoolUpRate;
            target.throttleSpoolDownRate = throttleSpoolDownRate;
            target.effectiveThrottle01 = effectiveThrottle01;
            target.afterburnerActive = afterburnerActive;
            target.negativeThrottleActive = negativeThrottleActive;

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

            target.rollCommandDeadzone = rollCommandDeadzone;
            target.bankHoldDeadzoneDeg = bankHoldDeadzoneDeg;
            target.rollCommandSlewRate = rollCommandSlewRate;
            target.yawAssistDeadzoneAosDeg = yawAssistDeadzoneAosDeg;
            target.centerRollStabilizeStrength = centerRollStabilizeStrength;

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
            jet.SetControlCommand(pitch, yaw, roll, throttleIntent, pitchOverride, rollOverride, yawOverride);
            jet.throttlePercent = throttlePercent;
            jet.displayedThrottlePercent = displayedThrottlePercent;
            jet.engineOn = engineOn;
            jet.effectiveThrottle01 = effectiveThrottle01;
            jet.afterburnerActive = afterburnerActive;
            jet.negativeThrottleActive = negativeThrottleActive;

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
            jet.highSpeedPitchLimiterFactor = highSpeedPitchLimiterFactor;
            jet.aoaAosGuardFactor = aoaAosGuardFactor;
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
            jet.forwardVelocityAlignment = forwardVelocityAlignment;
            jet.forwardVelocityAngleDeg = forwardVelocityAngleDeg;
            jet.debugForwardVelocityAngleDeg = forwardVelocityAngleDeg;
            jet.coordinatedYawAssistOutput = coordinatedYawAssistOutput;
            jet.debugManualPitchBoostActive = debugManualPitchBoostActive;
            jet.debugManualRollBoostActive = debugManualRollBoostActive;
            jet.debugManualYawBoostActive = debugManualYawBoostActive;
            jet.debugManualPitchActive = debugManualPitchActive;
            jet.debugManualRollActive = debugManualRollActive;
            jet.debugManualYawActive = debugManualYawActive;
            jet.debugRollHysteresisActive = debugRollHysteresisActive;
            jet.debugMouseCenterQuiet = debugMouseCenterQuiet;
            jet.debugFinalPitchBeforeSmoothing = debugFinalPitchBeforeSmoothing;
            jet.debugFinalPitchAfterSmoothing = debugFinalPitchAfterSmoothing;
            jet.debugFinalYawBeforeSmoothing = debugFinalYawBeforeSmoothing;
            jet.debugFinalYawAfterSmoothing = debugFinalYawAfterSmoothing;
            jet.debugFinalRollBeforeSmoothing = debugFinalRollBeforeSmoothing;
            jet.debugFinalRollAfterSmoothing = debugFinalRollAfterSmoothing;
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
                forwardVelocityAlignment = 1f;
                forwardVelocityAngleDeg = 0f;
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
                UpdateForwardVelocityTelemetry(worldVel);
                return;
            }

            float forwardSpeed = Mathf.Max(0.1f, localVelocity.z);
            aoaEstimateDeg = Mathf.Atan2(-localVelocity.y, forwardSpeed) * Mathf.Rad2Deg;
            aosEstimateDeg = Mathf.Atan2(localVelocity.x, forwardSpeed) * Mathf.Rad2Deg;
            velocityPitchAngleDeg = aoaEstimateDeg;
            velocityYawAngleDeg = aosEstimateDeg;
            UpdateForwardVelocityTelemetry(worldVel);
        }

        private void UpdateThrottle()
        {
            if (MavFreshInput.GetKeyDown(engineToggleKey) && !IsTargetingPodActive())
                engineOn = !engineOn;

            if (MavFreshInput.GetKeyDown(throttleIdleKey))
            {
                if (throttlePercent > idleThrottlePercent)
                {
                    previousPositiveThrottlePercent = throttlePercent;
                    throttlePercent = idleThrottlePercent;
                }
                else
                {
                    throttlePercent = Mathf.Clamp(
                        previousPositiveThrottlePercent > idleThrottlePercent ? previousPositiveThrottlePercent : 95f,
                        minThrottlePercent,
                        maxThrottlePercent
                    );
                }
            }

            if (holdThrottleKeysContinuous)
            {
                if (MavFreshInput.GetKey(throttleUpKey))
                    throttlePercent += throttleChangeRatePercentPerSecond * Time.deltaTime;

                if (MavFreshInput.GetKey(throttleDownKey))
                    throttlePercent -= throttleChangeRatePercentPerSecond * Time.deltaTime;
            }
            else if (throttleKeyboardStepPercent > 0f)
            {
                if (MavFreshInput.GetKeyDown(throttleUpKey))
                    throttlePercent += throttleKeyboardStepPercent;

                if (MavFreshInput.GetKeyDown(throttleDownKey))
                    throttlePercent -= throttleKeyboardStepPercent;
            }

            if (useMouseWheelThrottle)
            {
                float wheel = MavFreshInput.GetMouseScrollDelta();
                if (Mathf.Abs(wheel) > 0.01f)
                    throttlePercent += Mathf.Sign(wheel) * throttleWheelStepPercent;
            }

            SetThrottlePercent(throttlePercent);
            effectiveThrottle01 = jet != null ? jet.effectiveThrottle01 : PercentToLegacyThrottle(throttlePercent);
            afterburnerActive = jet != null
                ? jet.afterburnerActive
                : engineOn && useAfterburner && throttlePercent > afterburnerStartPercent;
            negativeThrottleActive = jet != null
                ? jet.negativeThrottleActive
                : engineOn && throttlePercent < idleThrottlePercent;
        }

        private float PercentToLegacyThrottle(float percent)
        {
            if (percent <= idleThrottlePercent)
                return Mathf.Lerp(-0.05f, idleThrust01, Mathf.InverseLerp(minThrottlePercent, idleThrottlePercent, percent));

            return Mathf.Clamp(percent / Mathf.Max(1f, militaryThrottlePercent), -0.05f, maxThrottle);
        }

        private bool IsTargetingPodActive()
        {
            MavTargetingPodSystem pod = FindObjectOfType<MavTargetingPodSystem>();
            return pod != null && pod.displayMode != MavTargetingPodDisplayMode.Off;
        }

        private void UpdateControls()
        {
            RefreshAttitudeTelemetry();

            rollOverride = false;
            pitchOverride = false;
            yawOverride = false;
            debugManualPitchBoostActive = false;
            debugManualRollBoostActive = false;
            debugManualYawBoostActive = false;
            debugManualPitchActive = false;
            debugManualRollActive = false;
            debugManualYawActive = false;

            float keyboardRoll = 0f;
            float keyboardPitch = 0f;
            float keyboardYaw = 0f;

            if (keyboardOverride)
            {
                debugAPressed = MavFreshInput.GetKey(KeyCode.A) || MavFreshInput.GetKey(KeyCode.LeftArrow);
                debugDPressed = MavFreshInput.GetKey(KeyCode.D) || MavFreshInput.GetKey(KeyCode.RightArrow);
                if (debugAPressed) keyboardRoll -= 1f;
                if (debugDPressed) keyboardRoll += 1f;
                if (invertKeyboardRoll) keyboardRoll = -keyboardRoll;
                debugManualRollInput = keyboardRoll;
                if (Mathf.Abs(keyboardRoll) > keyboardRollThreshold) rollOverride = true;

                debugWPressed = MavFreshInput.GetKey(KeyCode.W) || MavFreshInput.GetKey(KeyCode.UpArrow);
                debugSPressed = MavFreshInput.GetKey(KeyCode.S) || MavFreshInput.GetKey(KeyCode.DownArrow);

                if (debugWPressed)
                    keyboardPitch = pitchUpCommand;

                if (debugSPressed)
                    keyboardPitch = pitchDownCommand;

                if (invertKeyboardPitch) keyboardPitch = -keyboardPitch;
                debugKeyboardPitchInput = keyboardPitch;
                debugManualPitchInput = keyboardPitch;
                if (Mathf.Abs(keyboardPitch) > keyboardPitchThreshold) pitchOverride = true;

                if (MavFreshInput.GetKey(KeyCode.Q)) keyboardYaw -= 1f;
                if (MavFreshInput.GetKey(KeyCode.E)) keyboardYaw += 1f;
                if (invertKeyboardYaw) keyboardYaw = -keyboardYaw;
                yawOverride = Mathf.Abs(keyboardYaw) > 0.001f;
            }
            else
            {
                debugWPressed = false;
                debugSPressed = false;
                debugAPressed = false;
                debugDPressed = false;
                debugKeyboardPitchInput = 0f;
                debugManualPitchInput = 0f;
                debugManualRollInput = 0f;
            }

            debugManualPitchBoostActive = useManualControlAuthorityBoost && pitchOverride;
            debugManualRollBoostActive = useManualControlAuthorityBoost && rollOverride;
            debugManualYawBoostActive = useManualControlAuthorityBoost && yawOverride;
            debugManualPitchActive = pitchOverride;
            debugManualRollActive = rollOverride;
            debugManualYawActive = yawOverride;

            float autoYaw = 0f;
            float autoPitch = 0f;
            float autoRoll = 0f;
            debugMousePitchInput = 0f;
            debugMouseYawInput = 0f;

            if (enableMouseFlightAutopilot && rig != null)
                RunAutopilot(rig.MouseAimPos, out autoYaw, out autoPitch, out autoRoll);

            float yawAuthority = keyboardYawAuthority;
            if (debugManualYawBoostActive)
                yawAuthority *= Mathf.Max(1f, manualYawAuthorityBoost);

            debugMouseCenterQuiet = IsMouseYawRollNearCenter();

            float targetYaw = yawOverride
                ? keyboardYaw * yawAuthority
                : Mathf.Clamp(autoYaw, -maxAutoYawCommand, maxAutoYawCommand);

            if (yawDamper && rb != null)
            {
                localAngularVelocity = transform.InverseTransformDirection(rb.angularVelocity);
                float yawDampingSignal = -localAngularVelocity.y * yawRateDampingStrength;
                targetYaw += Mathf.Clamp(yawDampingSignal, -0.8f, 0.8f);
            }

            float manualRollCommand = debugManualRollBoostActive
                ? keyboardRoll * Mathf.Max(1f, manualRollAuthorityBoost)
                : keyboardRoll;
            float provisionalRollForYaw = rollOverride ? manualRollCommand : autoRoll;
            if (!yawOverride)
                targetYaw += ComputeCoordinatedYawAssist(provisionalRollForYaw, autoYaw);
            targetYaw = Mathf.Clamp(targetYaw, -1f, 1f);

            float targetPitch = autoPitch;
            float manualKeyboardPitch = debugManualPitchBoostActive
                ? keyboardPitch * Mathf.Max(1f, manualPitchAuthorityBoost)
                : keyboardPitch;

            if (pitchOverride)
            {
                smKeyboardPitch = manualKeyboardPitch;
                targetPitch = Mathf.Clamp(manualKeyboardPitch, -1f, 1f);
            }
            else if (useWTKeyboardElevatorOverride)
            {
                float k = 1f - Mathf.Exp(-keyboardElevatorReleaseBlend * Time.deltaTime);
                smKeyboardPitch = Mathf.Lerp(smKeyboardPitch, 0f, k);
            }
            else
            {
                smKeyboardPitch = 0f;
            }

            if (!pitchOverride)
                targetPitch = autoPitch;

            float targetRoll = rollOverride ? manualRollCommand : autoRoll;
            debugRawPitchCommand = Mathf.Clamp(targetPitch, -1f, 1f);
            debugRawYawCommand = Mathf.Clamp(targetYaw, -1f, 1f);
            debugRawRollCommand = Mathf.Clamp(targetRoll, -1f, 1f);

            ApplyProtectionAssists(ref targetPitch);
            ApplyStabilizer(ref targetPitch, ref targetYaw, ref targetRoll);
            ApplyEnvelopeProtectionGuards(ref targetPitch, ref targetYaw, ref targetRoll);
            debugFinalPitchBeforeSmoothing = Mathf.Clamp(targetPitch, -1f, 1f);
            debugFinalYawBeforeSmoothing = Mathf.Clamp(targetYaw, -1f, 1f);
            debugFinalRollBeforeSmoothing = Mathf.Clamp(targetRoll, -1f, 1f);

            if (keyboardPitchSuppressesMouseAim && pitchOverride)
                smPitch = Mathf.Clamp(targetPitch, -1f, 1f);

            if (inputSmoothing > 0f)
            {
                float pitchResponse = debugManualPitchBoostActive ? Mathf.Max(1f, manualPitchResponseMultiplier) : 1f;
                float yawResponse = debugManualYawBoostActive ? Mathf.Max(1f, manualYawAuthorityBoost) : 1f;
                float rollResponse = debugManualRollBoostActive ? Mathf.Max(1f, manualRollResponseMultiplier) : 1f;
                float pitchK = 1f - Mathf.Exp(-inputSmoothing * pitchResponse * Time.deltaTime);
                float yawK = 1f - Mathf.Exp(-inputSmoothing * yawResponse * Time.deltaTime);
                float rollK = 1f - Mathf.Exp(-inputSmoothing * rollResponse * Time.deltaTime);
                smPitch = pitchOverride ? targetPitch : Mathf.Lerp(smPitch, targetPitch, pitchK);
                smYaw = yawOverride ? targetYaw : Mathf.Lerp(smYaw, targetYaw, yawK);
                smRoll = rollOverride ? targetRoll : Mathf.Lerp(smRoll, targetRoll, rollK);
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
            debugFinalYawAfterSmoothing = yaw;
            debugFinalRollAfterSmoothing = roll;
            debugFinalPitchCommand = pitch;
            debugFinalYawCommand = yaw;
            debugFinalRollCommand = roll;
            debugPitchAuthorityFactor = pitchAuthorityFactor;
            debugRollAuthorityFactor = rollAuthorityFactor;
            state = enableMouseFlightAutopilot ? "wt_instructor" : "direct";
            debugControlState = state + "/" + instructorState;
        }

        private float ComputeCoordinatedYawAssist(float targetRoll, float autoYaw)
        {
            coordinatedYawAssistOutput = 0f;

            if (!enableCoordinatedYawAssist || speed < coordinatedYawSpeedMin)
                return 0f;

            float aosAbs = Mathf.Abs(aosEstimateDeg);
            float assistDeadzone = Mathf.Max(Mathf.Max(0f, yawAssistDeadzoneAosDeg), Mathf.Max(0f, aosYawAssistDeadzoneDeg));
            if (aosAbs < assistDeadzone)
                return 0f;

            float speedT = Mathf.InverseLerp(coordinatedYawSpeedMin, Mathf.Max(coordinatedYawSpeedMin + 0.1f, coordinatedYawFullSpeed), speed);
            float slipT = Mathf.Clamp01(aosAbs / 12f);
            float bankT = Mathf.Clamp01(Mathf.Abs(signedBankAngle) / 75f);
            float turnDemandT = Mathf.Clamp01(Mathf.Max(Mathf.Abs(targetRoll), Mathf.Abs(autoYaw), Mathf.Abs(aggressiveRoll)));

            if (!rollOverride && !yawOverride && debugMouseCenterQuiet && turnDemandT < Mathf.Max(0.001f, rollInputEnterDeadzone))
                return 0f;

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

        private float ApplyManualLimiterBypass(float factor, bool manualOverride)
        {
            if (!useManualControlAuthorityBoost || !manualOverride)
                return factor;

            float bypass = Mathf.Max(manualLimiterBypassFactor, manualEnvelopeBypassFactor);
            return Mathf.Lerp(factor, 1f, Mathf.Clamp01(bypass));
        }

        private float ManualDampingScale(bool manualOverride)
        {
            if (!useManualControlAuthorityBoost || !manualOverride)
                return 1f;

            return Mathf.Clamp01(manualDampingReduction);
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
                reduction = ApplyManualLimiterBypass(reduction, pitchOverride);
                targetPitch *= reduction;
                instructorState = "aoa_limiter";
            }

            if (useGLimiter && gEstimate > sustainedGLimit && targetPitch < 0f)
            {
                float gT = Mathf.InverseLerp(sustainedGLimit, hardGLimit, gEstimate);
                float reduction = ApplyManualLimiterBypass(gPitchReduction, pitchOverride);
                targetPitch = Mathf.Lerp(targetPitch, targetPitch * reduction, gT);
                instructorState = "g_limiter";
            }

            if (useGLimiter && gEstimate > softGLimit && gEstimate <= sustainedGLimit && targetPitch < 0f)
            {
                float gT = Mathf.InverseLerp(softGLimit, hardGLimit, gEstimate);
                float reduction = ApplyManualLimiterBypass(gPitchReduction, pitchOverride);
                targetPitch = Mathf.Lerp(targetPitch, targetPitch * reduction, gT);
                instructorState = "g_limiter";
            }

            if (keyboardElevatorUsesGLimit && pitchOverride && gEstimate > hardGLimit && targetPitch < 0f)
            {
                float reduction = ApplyManualLimiterBypass(0.45f, true);
                targetPitch = Mathf.Min(targetPitch * reduction, -0.05f);
                instructorState = "manual_g_limiter";
            }
        }

        private void ApplyEnvelopeProtectionGuards(ref float targetPitch, ref float targetYaw, ref float targetRoll)
        {
            float targetPitchLimiter = 1f;

            if (useHighSpeedPitchLimiter)
            {
                float speedT = Mathf.InverseLerp(
                    pitchLimiterStartSpeed,
                    Mathf.Max(pitchLimiterStartSpeed + 1f, pitchLimiterFullSpeed),
                    speed
                );
                float minAuthority = pitchOverride
                    ? Mathf.Max(highSpeedManualPitchAuthorityMin, manualPitchMinAuthority)
                    : highSpeedPitchAuthorityMin;
                targetPitchLimiter = Mathf.Lerp(1f, Mathf.Clamp01(minAuthority), Mathf.Clamp01(speedT));
                targetPitchLimiter = ApplyManualLimiterBypass(targetPitchLimiter, pitchOverride);
                if (pitchOverride)
                    targetPitchLimiter = Mathf.Max(targetPitchLimiter, manualPitchMinAuthority);
            }

            float pitchLimiterK = highSpeedPitchLimiterSmooth > 0f
                ? 1f - Mathf.Exp(-highSpeedPitchLimiterSmooth * Time.deltaTime)
                : 1f;
            smHighSpeedPitchLimiterFactor = Mathf.Lerp(smHighSpeedPitchLimiterFactor, targetPitchLimiter, pitchLimiterK);
            highSpeedPitchLimiterFactor = smHighSpeedPitchLimiterFactor;

            if (Mathf.Abs(targetPitch) > 0.0001f)
                targetPitch *= highSpeedPitchLimiterFactor;

            aoaAosGuardFactor = 1f;
            if (!useAoAAoSSoftGuard)
                return;

            float aoaAbs = Mathf.Abs(aoaEstimateDeg);
            float aosAbs = Mathf.Abs(aosEstimateDeg);
            float slipAbs = Mathf.Abs(localVelocity.x);
            float aoaT = Mathf.InverseLerp(aoaSoftGuardStart, Mathf.Max(aoaSoftGuardStart + 0.1f, aoaHardGuardStart), aoaAbs);
            float aosT = Mathf.InverseLerp(aosSoftGuardStart, Mathf.Max(aosSoftGuardStart + 0.1f, aosHardGuardStart), aosAbs);
            float slipT = Mathf.InverseLerp(highSlipGuardStart, Mathf.Max(highSlipGuardStart + 0.1f, highSlipGuardHard), slipAbs);
            float lateralGuardT = Mathf.Clamp01(Mathf.Max(aosT, slipT));

            float pitchGuard = 1f;
            if (targetPitch < 0f)
            {
                pitchGuard = Mathf.Lerp(1f, Mathf.Clamp01(guardPitchReduction), Mathf.Clamp01(aoaT));
                pitchGuard = ApplyManualLimiterBypass(pitchGuard, pitchOverride);
                if (pitchOverride)
                    pitchGuard = Mathf.Max(pitchGuard, manualPitchMinAuthority);
                targetPitch *= pitchGuard;
            }

            float yawGuard = Mathf.Lerp(1f, Mathf.Clamp01(guardYawReduction), lateralGuardT);
            float rollGuard = Mathf.Lerp(1f, Mathf.Clamp01(guardRollReduction), lateralGuardT);
            yawGuard = ApplyManualLimiterBypass(yawGuard, yawOverride);
            rollGuard = ApplyManualLimiterBypass(rollGuard, rollOverride);
            if (yawOverride)
                yawGuard = Mathf.Max(yawGuard, manualYawMinAuthority);
            if (rollOverride)
                rollGuard = Mathf.Max(rollGuard, manualRollMinAuthority);
            targetYaw *= yawGuard;
            targetRoll *= rollGuard;

            aoaAosGuardFactor = Mathf.Min(pitchGuard, Mathf.Min(yawGuard, rollGuard));

            if (Mathf.Max(aoaT, lateralGuardT) > 0.001f)
                instructorState = "envelope_guard";
        }

        private void ApplyStabilizer(ref float targetPitch, ref float targetYaw, ref float targetRoll)
        {
            if (!attitudeStabilizer || rb == null)
                return;

            localAngularVelocity = transform.InverseTransformDirection(rb.angularVelocity);
            bool mouseCenterQuiet = IsMouseYawRollNearCenter();
            debugMouseCenterQuiet = mouseCenterQuiet;

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
                if (mouseCenterQuiet && Mathf.Abs(signedBankAngle) <= bankHoldDeadzoneDeg)
                    rollLevelSignal = 0f;
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
            pitchRateDamping *= ManualDampingScale(pitchOverride);
            float rollRateDamping = rateDampingStrength * ManualDampingScale(rollOverride);
            targetPitch += Mathf.Clamp(-localAngularVelocity.x * pitchRateDamping, -maxStabCommand, maxStabCommand);
            targetRoll += Mathf.Clamp(localAngularVelocity.z * rollRateDamping, -maxStabCommand, maxStabCommand);

            bool centerRoll = !rollOverride
                && mouseCenterQuiet
                && Mathf.Abs(targetRoll) <= Mathf.Max(rollCommandDeadzone, mouseRollDeadzone) * 2f
                && (rig == null || !rig.isInRollZone);
            if (centerRoll)
            {
                float rollRateDeg = localAngularVelocity.z * Mathf.Rad2Deg;
                targetRoll = Mathf.Abs(rollRateDeg) <= rollRateDeadzoneDeg
                    ? 0f
                    : Mathf.Clamp(localAngularVelocity.z * centerRollStabilizeStrength, -maxStabCommand, maxStabCommand);
            }
        }

        private void RunAutopilot(Vector3 target, out float outYaw, out float outPitch, out float outRoll)
        {
            flyTarget = target;

            localFlyTarget = transform.InverseTransformPoint(flyTarget).normalized * sensitivity;
            angleOffTarget = Vector3.Angle(transform.forward, flyTarget - transform.position);

            float rawYawInput = Mathf.Clamp(localFlyTarget.x, -1f, 1f);
            if (Mathf.Abs(rawYawInput) <= mouseYawDeadzone)
                rawYawInput = 0f;

            float rawYaw = rawYawInput * yawGain;
            if (preferBankTurnOverYaw)
                rawYaw *= 0.42f;
            outYaw = Mathf.Clamp(rawYaw, -maxAutoYawCommand, maxAutoYawCommand);
            debugMouseYawInput = outYaw;

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
            debugMousePitchInput = outPitch;

            if (!pitchOverride && signedPitchAngle > noseHighAssistStartAngle)
            {
                float highT = Mathf.InverseLerp(noseHighAssistStartAngle, noseHighAssistFullAngle, signedPitchAngle);
                outPitch = Mathf.Clamp(outPitch + noseHighPitchDownAssist * highT, -maxAutoPitch, maxAutoPitch);
                debugMousePitchInput = outPitch;
            }

            float rawAggressiveRoll = Mathf.Clamp(localFlyTarget.x, -1f, 1f);
            bool hasRollZones = rig != null && rig.useScreenRollZones && useScreenRollZoneSteering;

            if (hasRollZones)
                aggressiveRoll = ApplyRollCommandStability(rig.screenRollCommand * screenRollZoneStrength, rig.isInRollZone);
            else
                aggressiveRoll = ApplyRollCommandStability(rawAggressiveRoll, Mathf.Abs(rawAggressiveRoll) > rollCommandDeadzone);

            wingsLevelRoll = transform.right.y;
            wingsLevelInfluence = Mathf.InverseLerp(0f, aggressiveTurnAngle, angleOffTarget);

            if (rig != null && useContinuousScreenBankHold)
            {
                float continuousCommand = ComputeContinuousScreenBankCommand();
                aggressiveRoll = ApplyRollCommandStability(
                    continuousCommand * screenRollZoneStrength,
                    Mathf.Abs(continuousCommand) > rollCommandDeadzone
                );
                outRoll = ComputeScreenZoneBankHoldRoll(aggressiveRoll, Mathf.Abs(aggressiveRoll) > 0.001f) * rollGain;
            }
            else if (hasRollZones && useScreenRollZoneBankHold)
            {
                outRoll = ComputeScreenZoneBankHoldRoll(aggressiveRoll, rig.isInRollZone) * rollGain;
            }
            else if (hasRollZones && suppressAutoRollInNoRollZone && !rig.isInRollZone)
            {
                outRoll = Mathf.Clamp(wingsLevelRoll * centerRollStabilizeStrength, -1f, 1f) * rollGain;
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

        private float ApplyRollCommandStability(float command, bool active)
        {
            float target = Mathf.Clamp(command, -1f, 1f);
            float enterDeadzone = Mathf.Max(Mathf.Max(rollCommandDeadzone, mouseRollDeadzone), rollInputEnterDeadzone);
            float exitDeadzone = Mathf.Min(enterDeadzone, Mathf.Max(0f, rollInputExitDeadzone));
            float absTarget = Mathf.Abs(target);

            if (!active)
            {
                rollHysteresisActive = false;
            }
            else if (rollHysteresisActive)
            {
                if (absTarget <= exitDeadzone)
                    rollHysteresisActive = false;
            }
            else if (absTarget >= enterDeadzone)
            {
                rollHysteresisActive = true;
            }

            if (!rollHysteresisActive)
                target = 0f;

            if (rollCommandSlewRate > 0f)
            {
                float t = 1f - Mathf.Exp(-rollCommandSlewRate * Time.deltaTime);
                slewedRollCommand = Mathf.Lerp(slewedRollCommand, target, t);
            }
            else
            {
                slewedRollCommand = target;
            }

            if (Mathf.Abs(slewedRollCommand) <= rollCommandDeadzone * 0.5f)
                slewedRollCommand = 0f;

            debugRollHysteresisActive = rollHysteresisActive;
            return Mathf.Clamp(slewedRollCommand, -1f, 1f);
        }

        private float ComputeContinuousScreenBankCommand()
        {
            if (rig == null)
                return 0f;

            float x = rig.cursorOffsetFromCenter.x;
            float absX = Mathf.Abs(x);

            if (absX <= Mathf.Max(continuousBankDeadzone, Mathf.Max(rollCommandDeadzone, mouseRollDeadzone)))
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

            if (!inRollZone || Mathf.Abs(zone) <= Mathf.Max(rollCommandDeadzone, mouseRollDeadzone))
                zone = 0f;

            targetBankAngle = -zone * maxScreenRollBankAngle;
            if (invertScreenRollBankTarget)
                targetBankAngle = -targetBankAngle;

            signedBankAngle = SignedBankAngle();

            Vector3 localAngVel = rb != null
                ? transform.InverseTransformDirection(rb.angularVelocity)
                : Vector3.zero;

            float bankError = signedBankAngle - targetBankAngle;
            float pTerm = Mathf.Abs(bankError) <= bankHoldDeadzoneDeg ? 0f : bankError * bankHoldProportional;
            float dTerm = -localAngVel.z * bankHoldRollRateDamping;
            float rollRateDeg = localAngVel.z * Mathf.Rad2Deg;

            if (Mathf.Abs(targetBankAngle) <= bankHoldDeadzoneDeg
                && Mathf.Abs(bankError) <= bankHoldDeadzoneDeg
                && Mathf.Abs(rollRateDeg) <= rollRateDeadzoneDeg)
            {
                bankHoldRollCommand = 0f;
            }
            else if (Mathf.Abs(targetBankAngle) <= bankHoldDeadzoneDeg && Mathf.Abs(bankError) <= bankHoldDeadzoneDeg)
            {
                bankHoldRollCommand = Mathf.Clamp(dTerm * centerRollStabilizeStrength, -centerRollStabilizeStrength, centerRollStabilizeStrength);
            }
            else
            {
                bankHoldRollCommand = Mathf.Clamp(pTerm + dTerm, -1f, 1f);
            }

            return bankHoldRollCommand;
        }

        private bool IsMouseYawRollNearCenter()
        {
            float rollDeadzone = Mathf.Max(rollCommandDeadzone, mouseRollDeadzone);
            float yawDeadzone = Mathf.Max(0f, mouseYawDeadzone);

            if (rig == null)
                return Mathf.Abs(aggressiveRoll) <= rollDeadzone && Mathf.Abs(debugMouseYawInput) <= yawDeadzone;

            float cursorX = Mathf.Abs(rig.cursorOffsetFromCenter.x);
            float screenRoll = Mathf.Abs(rig.screenRollCommand);
            return cursorX <= yawDeadzone
                && screenRoll <= rollDeadzone
                && Mathf.Abs(aggressiveRoll) <= rollDeadzone
                && Mathf.Abs(debugMouseYawInput) <= yawDeadzone;
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

            if (pitchOverride)
            {
                pitchAuthorityFactor = Mathf.Max(pitchAuthorityFactor, manualPitchMinAuthority);
                pitchTorqueFactor *= manualPitchBoost;
                if (useManualControlAuthorityBoost)
                    pitchTorqueFactor *= Mathf.Max(1f, manualPitchAuthorityBoost);
            }

            if (rollOverride)
            {
                rollAuthorityFactor = Mathf.Max(rollAuthorityFactor, manualRollMinAuthority);
                rollTorqueFactor *= manualRollBoost;
                if (useManualControlAuthorityBoost)
                    rollTorqueFactor *= Mathf.Max(1f, manualRollAuthorityBoost);
            }

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

        private void UpdateForwardVelocityTelemetry(Vector3 worldVel)
        {
            if (worldVel.sqrMagnitude < 0.001f)
            {
                forwardVelocityAlignment = 1f;
                forwardVelocityAngleDeg = 0f;
                return;
            }

            Vector3 velocityDir = worldVel.normalized;
            forwardVelocityAlignment = Mathf.Clamp(Vector3.Dot(transform.forward, velocityDir), -1f, 1f);
            forwardVelocityAngleDeg = Vector3.Angle(transform.forward, velocityDir);
        }
    }
}
