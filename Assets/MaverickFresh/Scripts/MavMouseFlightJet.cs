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

        [Header("Physics")]
        public float thrust = 220f;
        public Vector3 turnTorque = new Vector3(125f, 16f, 150f);
        public float forceMult = 1000f;
        public bool gravityOff = true;
        public float linearDamping = 0.021f;
        public float angularDamping = 1.35f;
        public float maxAngularVelocity = 4.8f;
        public bool useAccelerationTorqueMode = false;

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

        [Header("v0.11 Mouse Pitch Comfort")]
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

        [Header("Yaw / Side-Slip Damper")]
        public bool yawDamper = true;
        public float yawRateDampingStrength = 0.42f;
        public float sideSlipDampingStrength = 0.082f;
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
        [Range(0f, 1.6f)] public float throttle = 0.95f;
        public float throttleChangeRate = 0.65f;
        public float maxThrottle = 1.40f;

        [Header("Keyboard Override")]
        public bool keyboardOverride = true;
        public bool invertKeyboardRoll;
        public bool invertKeyboardPitch;
        public bool invertKeyboardYaw;
        public float keyboardRollThreshold = 0.10f;
        public float keyboardPitchThreshold = 0.02f;
        public float pitchUpCommand = -2.35f;
        public float pitchDownCommand = 2.15f;
        public float keyboardYawAuthority = 0.55f;
        public bool keyboardPitchSuppressesMouseAim = true;

        [Header("v0.12 WarThunder-like Elevator Override")]
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
        public string instructorState = "ready";
        public string state = "ready";

        private Rigidbody rigid;
        private Vector3 lastVelocity;
        private bool hasVelocitySample;

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
                instructor.SetThrottleIntent(throttle);
                instructor.MirrorRuntimeToJet();
            }
        }

        private void Update()
        {
            ResolveComponents(false);
            SetupPublicRigidbody();
        }

        private void FixedUpdate()
        {
            if (rigid == null)
                return;

            ResolveComponents(false);
            SetupPublicRigidbody();
            UpdatePhysicsTelemetry();

            if (instructor != null)
            {
                instructor.RefreshPhysicsTelemetry(Time.fixedDeltaTime);
                MirrorRuntimeFromInstructor();
            }

            effectiveThrottle = ComputeEffectiveThrottle();
            rigid.AddRelativeForce(Vector3.forward * thrust * effectiveThrottle * forceMult, ForceMode.Force);

            if (autoSpeedAssist && speed > maxCombatSpeed)
            {
                Vector3 antiVelocity = -rigid.linearVelocity.normalized * overspeedDrag * forceMult * (speed - maxCombatSpeed);
                rigid.AddForce(antiVelocity, ForceMode.Force);
            }

            ApplySideSlipDamping();
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
            rigid.angularDamping = angularDamping;
            rigid.maxAngularVelocity = maxAngularVelocity;
            rigid.interpolation = RigidbodyInterpolation.Interpolate;
            rigid.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
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

            if (controller != null)
                controller.CenterAim();

            if (instructor != null)
                instructor.RefreshPhysicsTelemetry(Time.fixedDeltaTime);
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

            if (controller == null)
                controller = FindObjectOfType<MavMouseFlightRig>();

            if (instructor == null)
                instructor = GetComponent<MavInstructorController>();

            if (instructor == null && createInstructor)
                instructor = gameObject.AddComponent<MavInstructorController>();

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
                return;
            }

            float forwardSpeed = Mathf.Max(0.1f, Mathf.Abs(localVelocity.z));
            aoaEstimateDeg = Mathf.Atan2(-localVelocity.y, forwardSpeed) * Mathf.Rad2Deg;
            aosEstimateDeg = Mathf.Atan2(localVelocity.x, forwardSpeed) * Mathf.Rad2Deg;
            velocityPitchAngleDeg = aoaEstimateDeg;
            velocityYawAngleDeg = aosEstimateDeg;
        }

        private void MirrorRuntimeFromInstructor()
        {
            if (instructor == null)
                return;

            instructor.CopyTuningToJet(this);

            pitch = instructor.pitch;
            yaw = instructor.yaw;
            roll = instructor.roll;
            throttle = instructor.throttleIntent;

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
            signedBankAngle = instructor.signedBankAngle;
            targetBankAngle = instructor.targetBankAngle;
            bankHoldRollCommand = instructor.bankHoldRollCommand;
            signedPitchAngle = instructor.signedPitchAngle;
            localAngularVelocity = instructor.localAngularVelocity;
            instructorState = instructor.instructorState;
            state = instructor.state;
            speedRegime = instructor.speedRegime;
            aoaEstimateDeg = instructor.aoaEstimateDeg;
            aosEstimateDeg = instructor.aosEstimateDeg;
            verticalSpeed = instructor.verticalSpeed;
            localVelocity = instructor.localVelocity;
            velocityPitchAngleDeg = instructor.velocityPitchAngleDeg;
            velocityYawAngleDeg = instructor.velocityYawAngleDeg;
            coordinatedYawAssistOutput = instructor.coordinatedYawAssistOutput;
        }

        private float ComputeEffectiveThrottle()
        {
            float t = instructor != null ? instructor.throttleIntent : throttle;

            if (autoSpeedAssist)
            {
                if (speed < minCombatSpeed)
                    t += lowSpeedThrustBoost;

                if (speed > maxCombatSpeed)
                    t *= Mathf.Lerp(1f, 0.70f, Mathf.InverseLerp(maxCombatSpeed, maxCombatSpeed + 190f, speed));

                if (speed > targetCruiseSpeed)
                {
                    float reduce = Mathf.InverseLerp(targetCruiseSpeed, maxCombatSpeed, speed) * speedAssistStrength;
                    t *= Mathf.Clamp01(1f - reduce);
                }
            }

            return Mathf.Clamp(t, 0f, maxThrottle + lowSpeedThrustBoost);
        }

        private void ApplySideSlipDamping()
        {
            Vector3 localVelocity = transform.InverseTransformDirection(rigid.linearVelocity);
            Vector3 sideSlipDamping = -transform.right * localVelocity.x * sideSlipDampingStrength * forceMult;
            rigid.AddForce(sideSlipDamping, ForceMode.Force);
        }

        private void ApplyControlTorque()
        {
            Vector3 torqueCommand = new Vector3(
                turnTorque.x * pitch * pitchTorqueFactor * pitchAuthorityFactor,
                turnTorque.y * yaw,
                -turnTorque.z * roll * rollTorqueFactor * rollAuthorityFactor
            );

            ForceMode torqueMode = useAccelerationTorqueMode ? ForceMode.Acceleration : ForceMode.Force;
            Vector3 controlTorque = useAccelerationTorqueMode ? torqueCommand : torqueCommand * forceMult;
            rigid.AddRelativeTorque(controlTorque, torqueMode);
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
