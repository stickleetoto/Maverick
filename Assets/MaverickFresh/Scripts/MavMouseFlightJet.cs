using UnityEngine;

namespace MaverickFresh
{
    /// <summary>
    /// v0.17 physical actuator for the Fresh MouseFlight jet.
    /// High-level War-Thunder-like interpretation lives in MavInstructorController.
    /// This class owns Rigidbody setup, thrust, side-slip damping, and final torque.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class MavMouseFlightJet : MonoBehaviour
    {
        [Header("Components")]
        public MavMouseFlightRig controller;
        public MavInstructorController instructor;
        [Tooltip("Enemy/AI aircraft should keep this off so they do not accidentally bind to the player mouse-flight rig.")]
        public bool allowGlobalRigLookup = true;

        [Header("Physics")]
        public float thrust = 220f;
        public Vector3 turnTorque = new Vector3(20f, 9f, 26f);
        public float forceMult = 1000f;
        public bool gravityOff = true;
        public float linearDamping = 0.006f;
        public float angularDamping = 1.6f;
        public float maxAngularVelocity = 6f;
        public bool useAccelerationTorqueMode = true;

        [Header("v0.18.11 Control Torque Authority")]
        public Vector3 accelerationModeTorque = new Vector3(20f, 9f, 26f);
        public Vector3 forceModeTorque = new Vector3(6200f, 3200f, 8200f);
        public Vector3 maxAppliedTorqueAccelerationMode = new Vector3(42f, 18f, 52f);
        public Vector3 maxAppliedTorqueForceMode = new Vector3(9000f, 4200f, 10500f);

        [Header("v0.18.14 Rate-Based Heavy Control")]
        public bool useRateBasedControl = true;
        public float targetPitchRateDeg = 48f;
        public float targetYawRateDeg = 18f;
        public float targetRollRateDeg = 72f;
        public Vector3 targetAngularRateDeg = Vector3.zero;
        public Vector3 rateControlP = new Vector3(0.42f, 0.28f, 0.38f);
        public Vector3 rateControlD = new Vector3(0.10f, 0.12f, 0.09f);
        public Vector3 maxRateControlTorque = new Vector3(32f, 14f, 40f);

        [Header("v0.18.9 Torque Safety")]
        public bool useFinalTorqueClamp = true;
        public Vector3 maxAppliedTorque = new Vector3(42f, 18f, 52f);
        public float finalTorqueSmoothing = 10f;

        [Header("v0.18.9 Control Surface Actuator")]
        public float controlSurfaceResponse = 7.5f;
        public float controlSurfaceReleaseResponse = 6.5f;
        public float maxPitchCommandRate = 4.8f;
        public float maxYawCommandRate = 2.8f;
        public float maxRollCommandRate = 5.8f;

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

        [Header("v0.11 Mouse Pitch Comfort")]
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

        [Header("v0.18.11 Direct Manual Torque Assist")]
        public bool useDirectManualTorqueAssist = false;
        public float directManualPitchAssist = 9f;
        public float directManualRollAssist = 12f;
        public float directManualYawAssist = 3.5f;

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
        public float highSpeedTurnDragStrength = 0.014f;
        public float highAoADragStrength = 0.026f;
        public float highAoSDragStrength = 0.034f;
        public float maxEnvelopeDragAccel = 28f;

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

        [Header("Yaw / Side-Slip Damper")]
        public bool yawDamper = true;
        public float yawRateDampingStrength = 0.42f;
        public float sideSlipDampingStrength = 0.55f;
        public float maxAutoYawCommand = 0.24f;
        public bool preferBankTurnOverYaw = true;

        [Header("v0.18.4 Semi-Aero Stabilizer")]
        public bool useSemiAeroStabilizer = true;
        public float sideSlipDamping = 0.12f;
        public float sideSlipDampingHighAoS = 0.24f;
        public float aoaDragStrength = 0.018f;
        public float aosDragStrength = 0.026f;
        public float forwardAlignmentAssist = 0.008f;
        public float forwardAlignmentMaxTorque = 0.35f;
        public float angularRateDampingPitch = 0.09f;
        public float angularRateDampingYaw = 0.12f;
        public float angularRateDampingRoll = 0.075f;
        public float highAoADragStart = 18f;
        public float highAoDampingStart = 12f;

        [Header("v0.18.6 Velocity Turn Assist")]
        public bool useVelocityTurnAssist = true;
        public float velocityTurnAssistStrength = 0.018f;
        public float velocityTurnAssistMaxAccel = 14f;
        public float velocityTurnAssistMinSpeed = 80f;
        public float velocityTurnAssistFullSpeed = 230f;
        public float velocityTurnAssistInputFactor = 0.58f;
        public float velocityTurnAssistAoSLimit = 45f;
        public float velocityTurnAssistSlipFadeStart = 20f;
        public float velocityTurnAssistSlipFadeEnd = 55f;

        [Header("v0.20.3~0.20.6 Aero Body Integration")]
        public MavAeroBody aeroBody;
        public bool useAeroBodyAuthority = true;
        [Range(0f, 1f)] public float aeroAuthorityBlend = 0.75f;
        public bool allowAeroBodyToFadeVelocityAssist = true;
        public float debugAeroBodyAuthority = 1f;
        public bool debugAeroBodyActive;



[Header("v0.20.7 Atmospheric Engine Integration")]
public MavAtmosphericEngine atmosphericEngine;
public bool useAtmosphericEngineScale = true;
public float debugEngineThrustScale = 1f;

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
        [Range(0f, 1.6f)] public float throttle = 0.95f;
        public float throttleChangeRate = 0.65f;
        public float maxThrottle = 1.40f;

        [Header("v0.18.8 WT Throttle Axis")]
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
        public float pitchUpCommand = -2.85f;
        public float pitchDownCommand = 2.65f;
        public float keyboardYawAuthority = 0.55f;
        public bool keyboardPitchSuppressesMouseAim = true;

        [Header("v0.12 WarThunder-like Elevator Override")]
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

        [Header("v0.18.6 Center Roll Stability")]
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

        [Header("Debug")]
        public Vector3 flyTarget;
        public Vector3 localFlyTarget;
        public float angleOffTarget;
        public float aggressiveRoll;
        public float wingsLevelRoll;
        public float wingsLevelInfluence;
        public float autoPitchRaw;
        public float speed;
        public float effectiveThrottle;
        public float machEstimate;
        public float gEstimate;
        public float aoaEstimateDeg;
        public float aosEstimateDeg;
        public float verticalSpeed;
        public Vector3 localVelocity;
        public float velocityPitchAngleDeg;
        public float velocityYawAngleDeg;
        public float forwardVelocityAlignment = 1f;
        public float forwardVelocityAngleDeg;
        public string speedRegime = "normal";
        public float turnBandFactor;
        public float pitchTorqueFactor = 1f;
        public float rollTorqueFactor = 1f;
        public float pitchAuthorityFactor = 1f;
        public float rollAuthorityFactor = 1f;
        public float signedBankAngle;
        public float targetBankAngle;
        public float bankHoldRollCommand;
        public float signedPitchAngle;
        public Vector3 localAngularVelocity;
        public Vector3 debugAppliedTorque;
        public float debugForwardVelocityAngleDeg;
        public float debugSideSlipAmount;
        public bool debugSemiAeroActive;
        public float highSpeedPitchLimiterFactor = 1f;
        public float aoaAosGuardFactor = 1f;
        public bool debugFinalTorqueClampActive;
        public float velocityTurnAssistCurrentFactor = 1f;
        public float envelopeDragAmount;
        public Vector3 debugControlTorqueBeforeClamp;
        public Vector3 debugControlTorqueAfterClamp;
        public Vector3 debugRawControlCommand;
        public Vector3 debugActuatedControlCommand;
        public float debugControlSurfaceResponseActive;
        public float debugReceivedPitchCommand;
        public float debugReceivedYawCommand;
        public float debugReceivedRollCommand;
        public float debugActuatedPitchCommand;
        public float debugActuatedYawCommand;
        public float debugActuatedRollCommand;
        public Vector3 debugFinalTorque;
        public string debugTorqueMode = "Acceleration";
        public bool debugManualInputActive;
        public float debugAngularDragEffective;
        public Vector3 debugAngularVelocity;
        public float debugAngularResponseMagnitude;
        public string debugActuatorState = "ready";
        public RigidbodyConstraints debugRigidbodyConstraints;
        public bool debugDirectManualAssistActive;
        public Vector3 debugDirectManualAssistTorque;
        public bool debugManualPitchBoostActive;
        public bool debugManualRollBoostActive;
        public bool debugManualYawBoostActive;
        public Vector3 debugTargetAngularRateDeg;
        public Vector3 debugCurrentAngularRateDeg;
        public Vector3 debugRateErrorDeg;
        public Vector3 debugRateControlTorque;
        public bool debugRollHysteresisActive;
        public bool debugManualPitchActive;
        public bool debugManualRollActive;
        public bool debugManualYawActive;
        public bool debugMouseCenterQuiet;
        public float debugFinalPitchBeforeSmoothing;
        public float debugFinalPitchAfterSmoothing;
        public float debugFinalYawBeforeSmoothing;
        public float debugFinalYawAfterSmoothing;
        public float debugFinalRollBeforeSmoothing;
        public float debugFinalRollAfterSmoothing;
        public string instructorState = "ready";
        public string state = "ready";

        private Rigidbody rigid;
        private Vector3 lastVelocity;
        private bool hasVelocitySample;
        private Vector3 smoothedControlTorque;
        private Vector3 actuatedControlCommand;
        private bool receivedManualPitchCommand;
        private bool receivedManualRollCommand;
        private bool receivedManualYawCommand;

        private void Awake()
        {
            ResolveComponents(true);
            SetupPublicRigidbody();
            if (rigid != null)
            {
                lastVelocity = rigid.linearVelocity;
                hasVelocitySample = true;
            }
        }

        private void Start()
        {
            ResolveComponents(true);

            if (rigid != null && rigid.linearVelocity.magnitude < 20f)
                rigid.linearVelocity = transform.forward * 255f;

            if (instructor != null)
            {
                instructor.SetThrottlePercent(throttlePercent);
                instructor.MirrorRuntimeToJet();
            }
        }

        private void Update()
        {
            ResolveComponents(false);
        }

        private void FixedUpdate()
        {
            if (rigid == null)
                return;

            ResolveComponents(false);
            UpdatePhysicsTelemetry();
            debugAppliedTorque = Vector3.zero;
            debugSemiAeroActive = false;
            debugFinalTorqueClampActive = false;
            velocityTurnAssistCurrentFactor = useVelocityTurnAssist ? 1f : 0f;
            debugAeroBodyActive = aeroBody != null && aeroBody.useAeroBody;
            debugAeroBodyAuthority = GetAeroControlAuthority();
            envelopeDragAmount = 0f;
            debugControlTorqueBeforeClamp = Vector3.zero;
            debugControlTorqueAfterClamp = Vector3.zero;
            debugRawControlCommand = Vector3.zero;
            debugActuatedControlCommand = actuatedControlCommand;
            debugControlSurfaceResponseActive = 0f;
            debugFinalTorque = Vector3.zero;
            debugDirectManualAssistActive = false;
            debugDirectManualAssistTorque = Vector3.zero;
            debugTargetAngularRateDeg = Vector3.zero;
            debugCurrentAngularRateDeg = Vector3.zero;
            debugRateErrorDeg = Vector3.zero;
            debugRateControlTorque = Vector3.zero;

            if (instructor != null)
            {
                instructor.RefreshPhysicsTelemetry(Time.fixedDeltaTime);
                MirrorRuntimeFromInstructor();
            }

            effectiveThrottle = ComputeEffectiveThrottle();
            debugEngineThrustScale = GetAtmosphericEngineScale();
            rigid.AddRelativeForce(Vector3.forward * thrust * effectiveThrottle * debugEngineThrustScale * forceMult, ForceMode.Force);

            if (negativeThrottleActive && useNegativeThrottleBrakeDrag && speed > 1f)
                rigid.AddForce(-rigid.linearVelocity.normalized * negativeThrottleBrakeDrag * forceMult * speed, ForceMode.Force);

            if (autoSpeedAssist && speed > maxCombatSpeed)
            {
                Vector3 antiVelocity = -rigid.linearVelocity.normalized * overspeedDrag * forceMult * (speed - maxCombatSpeed);
                rigid.AddForce(antiVelocity, ForceMode.Force);
            }

            ApplySideSlipDamping();
            ApplySemiAeroStabilizer();
            ApplyControlTorque();
        }

        public void SetupPublicRigidbody()
        {
            if (rigid == null)
                rigid = GetComponent<Rigidbody>();

            if (rigid == null)
                return;

            rigid.useGravity = !gravityOff;
            rigid.linearDamping = linearDamping;
            float effectiveAngularDamping = debugManualInputActive
                ? angularDamping * Mathf.Clamp01(manualDampingReduction)
                : angularDamping;
            rigid.angularDamping = effectiveAngularDamping;
            maxAngularVelocity = Mathf.Max(maxAngularVelocity, 5f);
            rigid.maxAngularVelocity = maxAngularVelocity;
            rigid.isKinematic = false;

            RigidbodyConstraints constraints = rigid.constraints;
            constraints &= ~RigidbodyConstraints.FreezeRotationX;
            constraints &= ~RigidbodyConstraints.FreezeRotationY;
            constraints &= ~RigidbodyConstraints.FreezeRotationZ;
            rigid.constraints = constraints;

            rigid.interpolation = RigidbodyInterpolation.Interpolate;
            rigid.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            debugAngularDragEffective = effectiveAngularDamping;
            debugRigidbodyConstraints = rigid.constraints;
            debugAngularVelocity = transform.InverseTransformDirection(rigid.angularVelocity);
            debugAngularResponseMagnitude = rigid.angularVelocity.magnitude;
        }

        public void ResetAirborne(Vector3 position, float startSpeed)
        {
            transform.position = position;
            transform.rotation = Quaternion.identity;

            if (rigid == null)
                rigid = GetComponent<Rigidbody>();

            if (rigid == null)
                return;

            rigid.linearVelocity = transform.forward * startSpeed;
            rigid.angularVelocity = Vector3.zero;
            lastVelocity = rigid.linearVelocity;
            hasVelocitySample = true;
            smoothedControlTorque = Vector3.zero;
            actuatedControlCommand = Vector3.zero;

            if (controller != null)
                controller.CenterAim();

            if (instructor != null)
                instructor.RefreshPhysicsTelemetry(Time.fixedDeltaTime);
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
            displayedThrottlePercent = throttlePercent;
            throttle = PercentToLegacyThrottle(throttlePercent);
        }

        public void SetControlCommand(float pitchCommand, float yawCommand, float rollCommand, float throttleCommand, bool manualPitch, bool manualRoll, bool manualYaw)
        {
            pitch = Mathf.Clamp(pitchCommand, -1f, 1f);
            yaw = Mathf.Clamp(yawCommand, -1f, 1f);
            roll = Mathf.Clamp(rollCommand, -1f, 1f);
            throttle = throttleCommand;

            receivedManualPitchCommand = manualPitch;
            receivedManualRollCommand = manualRoll;
            receivedManualYawCommand = manualYaw;
            debugManualInputActive = manualPitch || manualRoll || manualYaw;
            debugReceivedPitchCommand = pitch;
            debugReceivedYawCommand = yaw;
            debugReceivedRollCommand = roll;
        }

        public void PushLegacyTuningToInstructor()
        {
            ResolveComponents(true);
            if (instructor != null)
                instructor.CopyTuningFromJet(this);
        }

        public void PullInstructorTuningToLegacy()
        {
            ResolveComponents(false);
            if (instructor != null)
                instructor.CopyTuningToJet(this);
        }

        private void ResolveComponents(bool createInstructor)
        {
            if (rigid == null)
                rigid = GetComponent<Rigidbody>();

            if (controller == null && allowGlobalRigLookup)
                controller = FindObjectOfType<MavMouseFlightRig>();

            if (instructor == null)
                instructor = GetComponent<MavInstructorController>();

            if (instructor == null && createInstructor)
                instructor = gameObject.AddComponent<MavInstructorController>();

            if (aeroBody == null)
                aeroBody = GetComponent<MavAeroBody>();

            if (atmosphericEngine == null)
                atmosphericEngine = GetComponent<MavAtmosphericEngine>();

            if (instructor != null)
            {
                instructor.jet = this;
                instructor.rb = rigid;
                if (instructor.rig == null)
                    instructor.rig = controller;
            }
        }

        private void UpdatePhysicsTelemetry()
        {
            speed = rigid.linearVelocity.magnitude;
            machEstimate = speed / 343f;

            if (!hasVelocitySample)
            {
                lastVelocity = rigid.linearVelocity;
                hasVelocitySample = true;
            }

            Vector3 acceleration = (rigid.linearVelocity - lastVelocity) / Mathf.Max(Time.fixedDeltaTime, 0.0001f);
            gEstimate = Vector3.Dot(acceleration, transform.up) / 9.80665f;
            lastVelocity = rigid.linearVelocity;

            UpdateAeroTelemetry();
            signedBankAngle = SignedBankAngle();
            signedPitchAngle = SignedPitchAngle();
            localAngularVelocity = transform.InverseTransformDirection(rigid.angularVelocity);

            if (speed < minCombatSpeed) speedRegime = "LOW";
            else if (speed > maxCombatSpeed) speedRegime = "FAST";
            else if (Mathf.Abs(speed - bestTurnSpeed) < turnBandWidth) speedRegime = "BEST TURN";
            else if (speed > targetCruiseSpeed) speedRegime = "HIGH";
            else speedRegime = "NORMAL";
        }

        private void UpdateAeroTelemetry()
        {
            if (rigid == null)
                return;

            Vector3 worldVel = rigid.linearVelocity;
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

        private void MirrorRuntimeFromInstructor()
        {
            if (instructor == null)
                return;

            instructor.CopyTuningToJet(this);

            SetControlCommand(
                instructor.pitch,
                instructor.yaw,
                instructor.roll,
                instructor.throttleIntent,
                instructor.ManualPitchActive,
                instructor.ManualRollActive,
                instructor.ManualYawActive
            );

            flyTarget = instructor.flyTarget;
            localFlyTarget = instructor.localFlyTarget;
            angleOffTarget = instructor.angleOffTarget;
            aggressiveRoll = instructor.aggressiveRoll;
            wingsLevelRoll = instructor.wingsLevelRoll;
            wingsLevelInfluence = instructor.wingsLevelInfluence;
            autoPitchRaw = instructor.autoPitchRaw;
            turnBandFactor = instructor.turnBandFactor;
            pitchTorqueFactor = instructor.pitchTorqueFactor;
            rollTorqueFactor = instructor.rollTorqueFactor;
            pitchAuthorityFactor = instructor.pitchAuthorityFactor;
            rollAuthorityFactor = instructor.rollAuthorityFactor;
            highSpeedPitchLimiterFactor = instructor.highSpeedPitchLimiterFactor;
            aoaAosGuardFactor = instructor.aoaAosGuardFactor;
            signedBankAngle = instructor.signedBankAngle;
            targetBankAngle = instructor.targetBankAngle;
            bankHoldRollCommand = instructor.bankHoldRollCommand;
            signedPitchAngle = instructor.signedPitchAngle;
            localAngularVelocity = instructor.localAngularVelocity;
            instructorState = instructor.instructorState;
            state = instructor.state;
            throttle = instructor.throttleIntent;
            throttlePercent = instructor.throttlePercent;
            displayedThrottlePercent = instructor.displayedThrottlePercent;
            engineOn = instructor.engineOn;
            effectiveThrottle01 = instructor.effectiveThrottle01;
            afterburnerActive = instructor.afterburnerActive;
            negativeThrottleActive = instructor.negativeThrottleActive;
            speedRegime = instructor.speedRegime;
            aoaEstimateDeg = instructor.aoaEstimateDeg;
            aosEstimateDeg = instructor.aosEstimateDeg;
            verticalSpeed = instructor.verticalSpeed;
            localVelocity = instructor.localVelocity;
            velocityPitchAngleDeg = instructor.velocityPitchAngleDeg;
            velocityYawAngleDeg = instructor.velocityYawAngleDeg;
            forwardVelocityAlignment = instructor.forwardVelocityAlignment;
            forwardVelocityAngleDeg = instructor.forwardVelocityAngleDeg;
            debugForwardVelocityAngleDeg = forwardVelocityAngleDeg;
            coordinatedYawAssistOutput = instructor.coordinatedYawAssistOutput;
            debugManualPitchBoostActive = instructor.debugManualPitchBoostActive;
            debugManualRollBoostActive = instructor.debugManualRollBoostActive;
            debugManualYawBoostActive = instructor.debugManualYawBoostActive;
            debugManualPitchActive = instructor.debugManualPitchActive;
            debugManualRollActive = instructor.debugManualRollActive;
            debugManualYawActive = instructor.debugManualYawActive;
            debugRollHysteresisActive = instructor.debugRollHysteresisActive;
            debugMouseCenterQuiet = instructor.debugMouseCenterQuiet;
            debugFinalPitchBeforeSmoothing = instructor.debugFinalPitchBeforeSmoothing;
            debugFinalPitchAfterSmoothing = instructor.debugFinalPitchAfterSmoothing;
            debugFinalYawBeforeSmoothing = instructor.debugFinalYawBeforeSmoothing;
            debugFinalYawAfterSmoothing = instructor.debugFinalYawAfterSmoothing;
            debugFinalRollBeforeSmoothing = instructor.debugFinalRollBeforeSmoothing;
            debugFinalRollAfterSmoothing = instructor.debugFinalRollAfterSmoothing;
        }

        private float ComputeEffectiveThrottle()
        {
            float commandedPercent = instructor != null ? instructor.throttlePercent : throttlePercent;
            bool engineRunning = instructor != null ? instructor.engineOn : engineOn;
            displayedThrottlePercent = Mathf.Clamp(commandedPercent, minThrottlePercent, maxThrottlePercent);
            throttlePercent = displayedThrottlePercent;
            throttle = PercentToLegacyThrottle(throttlePercent);

            float targetThrottle01 = engineRunning ? MapThrottlePercentToThrust01(throttlePercent) : 0f;

            if (engineRunning && autoSpeedAssist && throttlePercent > idleThrottlePercent)
            {
                if (speed < minCombatSpeed)
                    targetThrottle01 += lowSpeedThrustBoost;

                if (speed > maxCombatSpeed)
                    targetThrottle01 *= Mathf.Lerp(1f, 0.70f, Mathf.InverseLerp(maxCombatSpeed, maxCombatSpeed + 190f, speed));
            }

            float spoolRate = targetThrottle01 > effectiveThrottle01 ? throttleSpoolUpRate : throttleSpoolDownRate;
            float k = 1f - Mathf.Exp(-spoolRate * Time.fixedDeltaTime);
            effectiveThrottle01 = Mathf.Lerp(effectiveThrottle01, targetThrottle01, k);

            if (!engineRunning)
                effectiveThrottle01 = 0f;

            afterburnerActive = engineRunning && useAfterburner && throttlePercent > afterburnerStartPercent && effectiveThrottle01 > 1.01f;
            negativeThrottleActive = engineRunning && throttlePercent < idleThrottlePercent;

            return Mathf.Clamp(effectiveThrottle01, -0.08f, maxThrottle + lowSpeedThrustBoost + afterburnerThrustMultiplier);
        }

        private float MapThrottlePercentToThrust01(float percent)
        {
            float clamped = Mathf.Clamp(percent, minThrottlePercent, maxThrottlePercent);

            if (clamped <= idleThrottlePercent)
            {
                float t = Mathf.InverseLerp(minThrottlePercent, idleThrottlePercent, clamped);
                return Mathf.Lerp(-0.05f, idleThrust01, t);
            }

            if (!useAfterburner || clamped <= afterburnerStartPercent)
            {
                float t = Mathf.InverseLerp(idleThrottlePercent, militaryThrottlePercent, clamped);
                return Mathf.Lerp(idleThrust01, 1f, t);
            }

            float abT = Mathf.InverseLerp(
                afterburnerStartPercent,
                Mathf.Max(afterburnerStartPercent + 0.1f, afterburnerMaxPercent),
                clamped
            );
            return Mathf.Lerp(1f, afterburnerThrustMultiplier, Mathf.Clamp01(abT));
        }

        private float PercentToLegacyThrottle(float percent)
        {
            if (percent <= idleThrottlePercent)
                return Mathf.Lerp(-0.05f, idleThrust01, Mathf.InverseLerp(minThrottlePercent, idleThrottlePercent, percent));

            return Mathf.Clamp(percent / Mathf.Max(1f, militaryThrottlePercent), -0.05f, maxThrottle);
        }

        private void ApplySideSlipDamping()
        {
            Vector3 localVelocity = transform.InverseTransformDirection(rigid.linearVelocity);
            debugSideSlipAmount = localVelocity.x;
            Vector3 sideAccel = -transform.right * localVelocity.x * sideSlipDampingStrength * ManualPhysicsDampingScale();
            rigid.AddForce(sideAccel, ForceMode.Acceleration);
        }

        private void ApplySemiAeroStabilizer()
        {
            if (!useSemiAeroStabilizer || rigid == null)
                return;

            Vector3 worldVel = rigid.linearVelocity;
            float speedNow = worldVel.magnitude;
            if (speedNow < 1f)
                return;

            Vector3 localVel = transform.InverseTransformDirection(worldVel);
            float speedFactor = Mathf.InverseLerp(35f, Mathf.Max(36f, targetCruiseSpeed), speedNow);
            float aoaAbs = Mathf.Abs(aoaEstimateDeg);
            float aosAbs = Mathf.Abs(aosEstimateDeg);
            float highAoAT = Mathf.InverseLerp(highAoADragStart, highAoADragStart + 30f, aoaAbs);
            float highAoST = Mathf.InverseLerp(highAoDampingStart, highAoDampingStart + 25f, aosAbs);
            float highAngleT = Mathf.Clamp01(Mathf.Max(highAoAT, highAoST));
            float highSlipT = useAoAAoSSoftGuard
                ? Mathf.InverseLerp(highSlipGuardStart, Mathf.Max(highSlipGuardStart + 0.1f, highSlipGuardHard), Mathf.Abs(localVel.x))
                : 0f;

            float manualDampingScale = ManualPhysicsDampingScale();
            float sideDamping = Mathf.Lerp(sideSlipDamping, sideSlipDampingHighAoS, Mathf.Clamp01(highAoST));
            sideDamping *= Mathf.Lerp(1f, 1.75f, Mathf.Clamp01(highSlipT));
            sideDamping *= manualDampingScale;
            Vector3 sideAccel = -transform.right * localVel.x * sideDamping * Mathf.Lerp(0.35f, 1f, speedFactor);
            rigid.AddForce(sideAccel, ForceMode.Acceleration);

            float angleDrag = (aoaAbs * aoaDragStrength + aosAbs * aosDragStrength)
                * Mathf.Lerp(0.35f, 1f, highAngleT)
                * manualDampingScale;
            if (angleDrag > 0.001f)
                rigid.AddForce(-worldVel.normalized * angleDrag * Mathf.Lerp(0.35f, 1f, speedFactor), ForceMode.Acceleration);

            ApplyEnvelopeDrag(worldVel, speedNow, aoaAbs, aosAbs, highSlipT);
            ApplyVelocityTurnAssist(worldVel, speedNow, speedFactor);

            Vector3 localTorque = Vector3.zero;

            if (forwardVelocityAngleDeg > 3f)
            {
                Vector3 velocityDir = worldVel.normalized;
                Vector3 worldAlignAxis = Vector3.Cross(transform.forward, velocityDir);

                if (worldAlignAxis.sqrMagnitude > 0.0001f)
                {
                    float angleT = Mathf.InverseLerp(8f, 75f, forwardVelocityAngleDeg);
                    Vector3 alignTorque = transform.InverseTransformDirection(worldAlignAxis.normalized)
                        * forwardAlignmentAssist
                        * Mathf.Clamp01(angleT)
                        * Mathf.Lerp(0.25f, 1f, speedFactor);

                    localTorque += Vector3.ClampMagnitude(alignTorque, Mathf.Max(0.01f, forwardAlignmentMaxTorque)) * manualDampingScale;
                }
            }

            Vector3 localRate = transform.InverseTransformDirection(rigid.angularVelocity);
            Vector3 rateDamping = new Vector3(
                -localRate.x * angularRateDampingPitch,
                -localRate.y * angularRateDampingYaw,
                -localRate.z * angularRateDampingRoll
            );

            localTorque += rateDamping * Mathf.Lerp(0.45f, 1.35f, highAngleT) * Mathf.Lerp(0.35f, 1f, speedFactor) * manualDampingScale;

            if (localTorque.sqrMagnitude > 0.000001f)
            {
                rigid.AddRelativeTorque(localTorque, ForceMode.Acceleration);
                debugAppliedTorque += localTorque;
            }

            debugSideSlipAmount = localVel.x;
            debugSemiAeroActive = true;
        }

        private void ApplyEnvelopeDrag(Vector3 worldVel, float speedNow, float aoaAbs, float aosAbs, float highSlipT)
        {
            envelopeDragAmount = 0f;
            if (!useAoAAoSSoftGuard || worldVel.sqrMagnitude < 1f)
                return;

            float highSpeedT = Mathf.InverseLerp(
                pitchLimiterStartSpeed,
                Mathf.Max(pitchLimiterStartSpeed + 1f, pitchLimiterFullSpeed),
                speedNow
            );
            float turnDemand = Mathf.Clamp01(Mathf.Max(Mathf.Abs(pitch), Mathf.Abs(roll)));
            float highSpeedTurnDrag = speedNow * highSpeedTurnDragStrength * Mathf.Clamp01(highSpeedT) * turnDemand;
            float aoaDrag = Mathf.Max(0f, aoaAbs - aoaSoftGuardStart) * highAoADragStrength;
            float aosDrag = Mathf.Max(0f, aosAbs - aosSoftGuardStart) * highAoSDragStrength;
            float slipDrag = maxEnvelopeDragAccel * 0.35f * Mathf.Clamp01(highSlipT);

            envelopeDragAmount = Mathf.Min(
                Mathf.Max(0f, maxEnvelopeDragAccel),
                highSpeedTurnDrag + aoaDrag + aosDrag + slipDrag
            );
            envelopeDragAmount *= ManualPhysicsDampingScale();

            if (envelopeDragAmount > 0.001f)
                rigid.AddForce(-worldVel.normalized * envelopeDragAmount, ForceMode.Acceleration);
        }

        private void ApplyVelocityTurnAssist(Vector3 worldVel, float speedNow, float speedFactor)
        {
            velocityTurnAssistCurrentFactor = 0f;
            if (!useVelocityTurnAssist || rigid == null || speedNow < velocityTurnAssistMinSpeed || worldVel.sqrMagnitude < 1f)
                return;

            if (forwardVelocityAngleDeg < 1f)
                return;

            // 구심력 방식: 속도 방향에 수직인 평면에서 기수 방향 성분만 추출
            // 속력은 유지하면서 방향만 바꿈 (실제 전투기 선회 원리)
            Vector3 velDir = worldVel.normalized;
            Vector3 forwardInVelPlane = Vector3.ProjectOnPlane(transform.forward, velDir);
            if (forwardInVelPlane.sqrMagnitude < 0.001f)
                return;
            forwardInVelPlane.Normalize();
            float sinAngle = Mathf.Sin(forwardVelocityAngleDeg * Mathf.Deg2Rad);
            float centripetalNeeded = speedNow * sinAngle;

            float speedT = Mathf.InverseLerp(
                velocityTurnAssistMinSpeed,
                Mathf.Max(velocityTurnAssistMinSpeed + 1f, velocityTurnAssistFullSpeed),
                speedNow
            );
            float turnDemand = Mathf.Clamp01(Mathf.Max(Mathf.Abs(pitch), Mathf.Abs(yaw), Mathf.Abs(roll)));
            float inputT = Mathf.Clamp01(turnDemand / Mathf.Max(0.01f, velocityTurnAssistInputFactor));
            float inputFactor = Mathf.Lerp(0.30f, 1f, inputT);
            float angleT = Mathf.InverseLerp(4f, 70f, forwardVelocityAngleDeg);
            float aosOrForwardAngle = Mathf.Max(Mathf.Abs(aosEstimateDeg), forwardVelocityAngleDeg);
            float overLimitT = Mathf.InverseLerp(
                velocityTurnAssistAoSLimit,
                Mathf.Max(velocityTurnAssistAoSLimit + 1f, velocityTurnAssistAoSLimit + 35f),
                aosOrForwardAngle
            );
            float snapAvoidance = Mathf.Lerp(1f, 0.25f, overLimitT);
            float slipMetric = Mathf.Max(Mathf.Abs(aosEstimateDeg), Mathf.Abs(debugSideSlipAmount));
            float slipFadeT = Mathf.InverseLerp(
                velocityTurnAssistSlipFadeStart,
                Mathf.Max(velocityTurnAssistSlipFadeStart + 0.1f, velocityTurnAssistSlipFadeEnd),
                slipMetric
            );
            float slipAssistFade = Mathf.Lerp(1f, 0.10f, Mathf.Clamp01(slipFadeT));
            velocityTurnAssistCurrentFactor = Mathf.Clamp01(snapAvoidance * slipAssistFade);

            Vector3 alignAccel = forwardInVelPlane * centripetalNeeded
                * velocityTurnAssistStrength
                * GetVelocityAssistAeroScale()
                * Mathf.Clamp01(speedT)
                * Mathf.Clamp01(angleT)
                * inputFactor
                * velocityTurnAssistCurrentFactor;

            alignAccel = Vector3.ClampMagnitude(alignAccel, Mathf.Max(0.1f, velocityTurnAssistMaxAccel));
            if (alignAccel.sqrMagnitude > 0.0001f)
                rigid.AddForce(alignAccel, ForceMode.Acceleration);

            float extremeSlipT = Mathf.Clamp01(Mathf.Max(overLimitT, slipFadeT));
            if (extremeSlipT > 0.001f)
            {
                float dampingAccel = velocityTurnAssistMaxAccel * 0.30f * extremeSlipT * Mathf.Lerp(0.35f, 1f, speedFactor);
                rigid.AddForce(-worldVel.normalized * dampingAccel, ForceMode.Acceleration);
            }
        }

        private float GetAeroControlAuthority()
        {
            if (!useAeroBodyAuthority || aeroBody == null || !aeroBody.useAeroBody)
                return 1f;

            float target = Mathf.Clamp(aeroBody.debugControlAuthority, 0.05f, 1.5f);
            return Mathf.Lerp(1f, target, Mathf.Clamp01(aeroAuthorityBlend));
        }

        private float GetVelocityAssistAeroScale()
        {
            if (!allowAeroBodyToFadeVelocityAssist || aeroBody == null || !aeroBody.useAeroBody)
                return 1f;
            return aeroBody.GetVelocityAssistScale();
        }


private float GetAtmosphericEngineScale()
{
    if (!useAtmosphericEngineScale || atmosphericEngine == null || !atmosphericEngine.useAtmosphericEngine)
        return 1f;
    return Mathf.Max(0.05f, atmosphericEngine.GetThrustScale(afterburnerActive));
}

        private void ApplyControlTorque()
        {
            Vector3 targetCommand = new Vector3(pitch, yaw, roll);
            Vector3 controlCommand = useRateBasedControl
                ? UpdateImmediateRateCommandDebug(targetCommand)
                : UpdateActuatedControlCommand(targetCommand);

            if (useRateBasedControl)
            {
                ApplyRateBasedControlTorque(controlCommand);
                ApplyDirectManualTorqueAssist();
                return;
            }

            Vector3 torqueScale = useAccelerationTorqueMode ? accelerationModeTorque : forceModeTorque;
            float pitchAuthority = receivedManualPitchCommand
                ? Mathf.Max(pitchAuthorityFactor, manualPitchMinAuthority)
                : pitchAuthorityFactor;
            float rollAuthority = receivedManualRollCommand
                ? Mathf.Max(rollAuthorityFactor, manualRollMinAuthority)
                : rollAuthorityFactor;
            float yawAuthority = receivedManualYawCommand ? Mathf.Max(1f, manualYawMinAuthority) : 1f;
            float aeroAuthority = GetAeroControlAuthority();
            pitchAuthority *= aeroAuthority;
            rollAuthority *= aeroAuthority;
            yawAuthority = Mathf.Lerp(yawAuthority, yawAuthority * aeroAuthority, 0.55f);

            Vector3 torqueCommand = new Vector3(
                torqueScale.x * controlCommand.x * pitchTorqueFactor * pitchAuthority,
                torqueScale.y * controlCommand.y * yawAuthority,
                -torqueScale.z * controlCommand.z * rollTorqueFactor * rollAuthority
            );

            ForceMode torqueMode = useAccelerationTorqueMode ? ForceMode.Acceleration : ForceMode.Force;
            Vector3 controlTorque = torqueCommand;
            debugControlTorqueBeforeClamp = controlTorque;
            debugTorqueMode = torqueMode.ToString();

            if (useFinalTorqueClamp)
                controlTorque = ClampAppliedTorque(controlTorque);

            if (finalTorqueSmoothing > 0f)
            {
                float k = 1f - Mathf.Exp(-finalTorqueSmoothing * Time.fixedDeltaTime);
                smoothedControlTorque = Vector3.Lerp(smoothedControlTorque, controlTorque, k);
                controlTorque = smoothedControlTorque;
            }
            else
            {
                smoothedControlTorque = controlTorque;
            }

            debugControlTorqueAfterClamp = controlTorque;
            debugFinalTorque = controlTorque;
            debugActuatorState = debugManualInputActive ? "manual_control" : "instructor_control";
            rigid.AddRelativeTorque(controlTorque, torqueMode);
            debugAppliedTorque += controlTorque;
            ApplyDirectManualTorqueAssist();
        }

        private void ApplyRateBasedControlTorque(Vector3 controlCommand)
        {
            Vector3 localAngularVelDeg = transform.InverseTransformDirection(rigid.angularVelocity) * Mathf.Rad2Deg;

            float pitchAuthority = receivedManualPitchCommand
                ? Mathf.Max(pitchAuthorityFactor, manualPitchMinAuthority)
                : pitchAuthorityFactor;
            float rollAuthority = receivedManualRollCommand
                ? Mathf.Max(rollAuthorityFactor, manualRollMinAuthority)
                : rollAuthorityFactor;
            float yawAuthority = receivedManualYawCommand ? Mathf.Max(1f, manualYawMinAuthority) : 1f;
            float aeroAuthority = GetAeroControlAuthority();
            pitchAuthority *= aeroAuthority;
            rollAuthority *= aeroAuthority;
            yawAuthority = Mathf.Lerp(yawAuthority, yawAuthority * aeroAuthority, 0.55f);

            targetAngularRateDeg = new Vector3(
                controlCommand.x * targetPitchRateDeg * pitchAuthority,
                controlCommand.y * targetYawRateDeg * yawAuthority,
                -controlCommand.z * targetRollRateDeg * rollAuthority
            );

            debugTargetAngularRateDeg = targetAngularRateDeg;
            debugCurrentAngularRateDeg = localAngularVelDeg;
            debugRateErrorDeg = targetAngularRateDeg - localAngularVelDeg;

            Vector3 rateTorque = new Vector3(
                debugRateErrorDeg.x * rateControlP.x - localAngularVelDeg.x * rateControlD.x,
                debugRateErrorDeg.y * rateControlP.y - localAngularVelDeg.y * rateControlD.y,
                debugRateErrorDeg.z * rateControlP.z - localAngularVelDeg.z * rateControlD.z
            );

            debugControlTorqueBeforeClamp = rateTorque;
            rateTorque = ClampRateControlTorque(rateTorque);
            debugRateControlTorque = rateTorque;
            debugTorqueMode = "RateAcceleration";

            if (finalTorqueSmoothing > 0f)
            {
                float k = 1f - Mathf.Exp(-finalTorqueSmoothing * Time.fixedDeltaTime);
                smoothedControlTorque = Vector3.Lerp(smoothedControlTorque, rateTorque, k);
                rateTorque = smoothedControlTorque;
            }
            else
            {
                smoothedControlTorque = rateTorque;
            }

            debugControlTorqueAfterClamp = rateTorque;
            debugFinalTorque = rateTorque;
            debugActuatorState = debugManualInputActive ? "manual_rate" : "instructor_rate";
            rigid.AddRelativeTorque(rateTorque, ForceMode.Acceleration);
            debugAppliedTorque += rateTorque;
        }

        private Vector3 UpdateImmediateRateCommandDebug(Vector3 targetCommand)
        {
            debugRawControlCommand = targetCommand;
            actuatedControlCommand = new Vector3(
                Mathf.Clamp(targetCommand.x, -1f, 1f),
                Mathf.Clamp(targetCommand.y, -1f, 1f),
                Mathf.Clamp(targetCommand.z, -1f, 1f)
            );
            debugControlSurfaceResponseActive = 0f;
            debugActuatedControlCommand = actuatedControlCommand;
            debugActuatedPitchCommand = actuatedControlCommand.x;
            debugActuatedYawCommand = actuatedControlCommand.y;
            debugActuatedRollCommand = actuatedControlCommand.z;
            return actuatedControlCommand;
        }

        private Vector3 UpdateActuatedControlCommand(Vector3 targetCommand)
        {
            debugRawControlCommand = targetCommand;

            float pitchMultiplier = debugManualPitchBoostActive && useManualControlAuthorityBoost
                ? Mathf.Max(1f, manualPitchResponseMultiplier)
                : 1f;
            float yawMultiplier = debugManualYawBoostActive && useManualControlAuthorityBoost
                ? Mathf.Max(1f, manualYawAuthorityBoost)
                : 1f;
            float rollMultiplier = debugManualRollBoostActive && useManualControlAuthorityBoost
                ? Mathf.Max(1f, manualRollResponseMultiplier)
                : 1f;

            float pitchResponse = AxisResponse(actuatedControlCommand.x, targetCommand.x) * pitchMultiplier;
            float yawResponse = AxisResponse(actuatedControlCommand.y, targetCommand.y) * yawMultiplier;
            float rollResponse = AxisResponse(actuatedControlCommand.z, targetCommand.z) * rollMultiplier;

            actuatedControlCommand.x = MoveActuatorAxis(
                actuatedControlCommand.x,
                targetCommand.x,
                pitchResponse,
                maxPitchCommandRate * pitchMultiplier
            );
            actuatedControlCommand.y = MoveActuatorAxis(
                actuatedControlCommand.y,
                targetCommand.y,
                yawResponse,
                maxYawCommandRate * yawMultiplier
            );
            actuatedControlCommand.z = MoveActuatorAxis(
                actuatedControlCommand.z,
                targetCommand.z,
                rollResponse,
                maxRollCommandRate * rollMultiplier
            );

            debugControlSurfaceResponseActive = Mathf.Max(pitchResponse, Mathf.Max(yawResponse, rollResponse));
            debugActuatedControlCommand = actuatedControlCommand;
            debugActuatedPitchCommand = actuatedControlCommand.x;
            debugActuatedYawCommand = actuatedControlCommand.y;
            debugActuatedRollCommand = actuatedControlCommand.z;
            return actuatedControlCommand;
        }

        private void ApplyDirectManualTorqueAssist()
        {
            debugDirectManualAssistActive = false;
            debugDirectManualAssistTorque = Vector3.zero;

            if (!useDirectManualTorqueAssist || rigid == null || !debugManualInputActive)
                return;

            Vector3 assist = new Vector3(
                receivedManualPitchCommand ? pitch * directManualPitchAssist : 0f,
                receivedManualYawCommand ? yaw * directManualYawAssist : 0f,
                receivedManualRollCommand ? -roll * directManualRollAssist : 0f
            );

            if (assist.sqrMagnitude <= 0.000001f)
                return;

            rigid.AddRelativeTorque(assist, ForceMode.Acceleration);
            debugAppliedTorque += assist;
            debugDirectManualAssistTorque = assist;
            debugDirectManualAssistActive = true;
            debugActuatorState = "manual_assist";
        }

        private float ManualPhysicsDampingScale()
        {
            return debugManualInputActive ? Mathf.Clamp01(manualDampingReduction) : 1f;
        }

        private float AxisResponse(float current, float target)
        {
            bool releasing = Mathf.Abs(target) < Mathf.Abs(current);
            return Mathf.Max(0f, releasing ? controlSurfaceReleaseResponse : controlSurfaceResponse);
        }

        private float MoveActuatorAxis(float current, float target, float response, float maxRate)
        {
            if (response <= 0f)
                return Mathf.Clamp(target, -1f, 1f);

            float k = 1f - Mathf.Exp(-response * Time.fixedDeltaTime);
            float desired = Mathf.Lerp(current, Mathf.Clamp(target, -1f, 1f), k);

            if (maxRate > 0f)
                return Mathf.MoveTowards(current, desired, maxRate * Time.fixedDeltaTime);

            return desired;
        }

        private Vector3 ClampAppliedTorque(Vector3 torque)
        {
            Vector3 activeLimit = useAccelerationTorqueMode ? maxAppliedTorqueAccelerationMode : maxAppliedTorqueForceMode;
            if (activeLimit.sqrMagnitude <= 0.000001f)
                activeLimit = maxAppliedTorque;

            Vector3 limit = new Vector3(
                Mathf.Abs(activeLimit.x),
                Mathf.Abs(activeLimit.y),
                Mathf.Abs(activeLimit.z)
            );

            Vector3 clamped = new Vector3(
                limit.x > 0f ? Mathf.Clamp(torque.x, -limit.x, limit.x) : torque.x,
                limit.y > 0f ? Mathf.Clamp(torque.y, -limit.y, limit.y) : torque.y,
                limit.z > 0f ? Mathf.Clamp(torque.z, -limit.z, limit.z) : torque.z
            );

            debugFinalTorqueClampActive = !Mathf.Approximately(torque.x, clamped.x)
                || !Mathf.Approximately(torque.y, clamped.y)
                || !Mathf.Approximately(torque.z, clamped.z);

            return clamped;
        }

        private Vector3 ClampRateControlTorque(Vector3 torque)
        {
            Vector3 limit = new Vector3(
                Mathf.Abs(maxRateControlTorque.x),
                Mathf.Abs(maxRateControlTorque.y),
                Mathf.Abs(maxRateControlTorque.z)
            );

            Vector3 clamped = new Vector3(
                limit.x > 0f ? Mathf.Clamp(torque.x, -limit.x, limit.x) : torque.x,
                limit.y > 0f ? Mathf.Clamp(torque.y, -limit.y, limit.y) : torque.y,
                limit.z > 0f ? Mathf.Clamp(torque.z, -limit.z, limit.z) : torque.z
            );

            debugFinalTorqueClampActive = !Mathf.Approximately(torque.x, clamped.x)
                || !Mathf.Approximately(torque.y, clamped.y)
                || !Mathf.Approximately(torque.z, clamped.z);

            return clamped;
        }

        private void UpdateForwardVelocityTelemetry(Vector3 worldVel)
        {
            if (worldVel.sqrMagnitude < 0.001f)
            {
                forwardVelocityAlignment = 1f;
                forwardVelocityAngleDeg = 0f;
                debugForwardVelocityAngleDeg = 0f;
                return;
            }

            Vector3 velocityDir = worldVel.normalized;
            forwardVelocityAlignment = Mathf.Clamp(Vector3.Dot(transform.forward, velocityDir), -1f, 1f);
            forwardVelocityAngleDeg = Vector3.Angle(transform.forward, velocityDir);
            debugForwardVelocityAngleDeg = forwardVelocityAngleDeg;
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
