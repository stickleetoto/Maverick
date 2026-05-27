using UnityEngine;
using EaglePhysicalAI.Aircraft;

namespace EaglePhysicalAI.Controls
{
    /// <summary>
    /// MouseFlight-inspired instructor.
    /// Uses a world-space fly target and the classic localFlyTarget/aggressiveRoll/wingsLevelRoll logic.
    /// Inherits ManualAircraftInput so old logging/PhysicalAI can continue reading lastPitch/Roll/Yaw.
    /// </summary>
    [RequireComponent(typeof(AircraftPhysicsController))]
    public class MaverickMouseFlightInstructorV12 : ManualAircraftInput
    {
        [Header("References")]
        public MaverickMouseFlightRigV12 rig;
        public MaverickWTKeybindProfileV11 keys;

        [Header("Control Mode")]
        public MaverickV10ControlMode controlMode = MaverickV10ControlMode.MouseAim;

        [Header("MouseFlight Core")]
        public float sensitivity = 3.6f;
        public float aggressiveTurnAngle = 12f;
        public float pitchGain = 1.0f;
        public float yawGain = 1.0f;
        public float rollGain = 1.0f;
        public float commandSmoothing = 9.5f;

        [Header("Instructor Assist")]
        public bool useStallProtection = true;
        public bool useGLimiter = true;
        public float stallPitchRelief = 0.32f;
        public float gLimit = 8.5f;
        public float lowSpeedAuthority = 0.45f;
        public float highAoAAuthorityLoss = 0.48f;

        [Header("Keyboard Override")]
        public bool keyboardOverrideEnabled = true;
        public float keyboardPitchWeight = 0.45f;
        public float keyboardRollWeight = 0.75f;
        public float keyboardYawWeight = 0.45f;
        public bool invertKeyboardRoll;
        public bool invertKeyboardPitch;
        public bool invertKeyboardYaw;

        [Header("Output Signs")]
        public float pitchSign = 1f;
        public float rollSign = 1f;
        public float yawSign = 1f;

        [Header("Throttle")]
        public float throttleState = 0.82f;
        public float throttleStepRate = 0.6f;
        public bool holdWForWep = true;

        [Header("Debug")]
        public Vector3 flyTarget;
        public Vector3 localFlyTarget;
        public float angleOffTarget;
        public float aggressiveRoll;
        public float wingsLevelRoll;
        public float wingsLevelInfluence;
        public float authority;
        public string instructorNote = "ready";

        private float smPitch;
        private float smRoll;
        private float smYaw;

        private void Awake()
        {
            if (controller == null) controller = GetComponent<AircraftPhysicsController>();
            if (rig == null) rig = GetComponent<MaverickMouseFlightRigV12>();
            if (keys == null) keys = GetComponent<MaverickWTKeybindProfileV11>();
            if (keys == null) keys = gameObject.AddComponent<MaverickWTKeybindProfileV11>();
            if (controller != null) throttleState = Mathf.Max(controller.targetThrottle, throttleState);
        }

        private void Update()
        {
            strikePressed = false;
            confirmPressed = false;
            abortPressed = false;

            if (!inputEnabled || controller == null)
                return;

            HandleModeKeys();
            HandleThrottle();

            float pitch;
            float roll;
            float yaw;

            if (controlMode == MaverickV10ControlMode.MouseAim)
                BuildMouseFlightCommand(out pitch, out roll, out yaw);
            else if (controlMode == MaverickV10ControlMode.AssistedDirect)
                BuildDirectCommand(out pitch, out roll, out yaw, assisted: true);
            else if (controlMode == MaverickV10ControlMode.RealisticDirect)
                BuildDirectCommand(out pitch, out roll, out yaw, assisted: false);
            else
                return;

            float alpha = 1f - Mathf.Exp(-commandSmoothing * Time.deltaTime);
            smPitch = Mathf.Lerp(smPitch, pitch, alpha);
            smRoll = Mathf.Lerp(smRoll, roll, alpha);
            smYaw = Mathf.Lerp(smYaw, yaw, alpha);

            lastPitch = Mathf.Clamp(smPitch * pitchSign, -1f, 1f);
            lastRoll = Mathf.Clamp(smRoll * rollSign, -1f, 1f);
            lastYaw = Mathf.Clamp(smYaw * yawSign, -1f, 1f);
            lastThrottle = Mathf.Clamp01(throttleState);

            if (MaverickInput.GetKeyDown(keys.firePrimary) || MaverickInput.GetKeyDown(keys.fireSecondary))
                strikePressed = true;

            if (MaverickInput.GetKeyDown(keys.confirm) || MaverickInput.GetKeyDown(KeyCode.F))
                confirmPressed = true;

            if (MaverickInput.GetKeyDown(keys.abort))
                abortPressed = true;

            controller.SetControlInputs(lastPitch, lastRoll, lastYaw, lastThrottle);
        }

        private void HandleModeKeys()
        {
            if (MaverickInput.GetKeyDown(keys.modeMouseAim)) controlMode = MaverickV10ControlMode.MouseAim;
            if (MaverickInput.GetKeyDown(keys.modeAssisted)) controlMode = MaverickV10ControlMode.AssistedDirect;
            if (MaverickInput.GetKeyDown(keys.modeRealistic)) controlMode = MaverickV10ControlMode.RealisticDirect;
            if (MaverickInput.GetKeyDown(keys.modeAI)) controlMode = MaverickV10ControlMode.AIManaged;
        }

        private void HandleThrottle()
        {
            if (MaverickInput.GetKey(keys.throttleUp)) throttleState += throttleStepRate * Time.deltaTime;
            if (MaverickInput.GetKey(keys.throttleDown)) throttleState -= throttleStepRate * Time.deltaTime;
            if (holdWForWep && MaverickInput.GetKey(keys.wep)) throttleState = Mathf.MoveTowards(throttleState, 1f, throttleStepRate * 3f * Time.deltaTime);
            if (MaverickInput.GetKey(keys.idle)) throttleState = Mathf.MoveTowards(throttleState, 0.05f, throttleStepRate * 3f * Time.deltaTime);
            throttleState = Mathf.Clamp01(throttleState);
        }

        private void BuildMouseFlightCommand(out float pitch, out float roll, out float yaw)
        {
            if (rig == null)
            {
                BuildDirectCommand(out pitch, out roll, out yaw, assisted: true);
                return;
            }

            flyTarget = rig.MouseAimPos;

            // MouseFlight-style core.
            localFlyTarget = transform.InverseTransformPoint(flyTarget).normalized * sensitivity;
            angleOffTarget = Vector3.Angle(transform.forward, flyTarget - transform.position);

            yaw = Mathf.Clamp(localFlyTarget.x, -1f, 1f) * yawGain;
            pitch = -Mathf.Clamp(localFlyTarget.y, -1f, 1f) * pitchGain;

            aggressiveRoll = Mathf.Clamp(localFlyTarget.x, -1f, 1f);
            wingsLevelRoll = transform.right.y;
            wingsLevelInfluence = Mathf.InverseLerp(0f, aggressiveTurnAngle, angleOffTarget);
            roll = Mathf.Lerp(wingsLevelRoll, aggressiveRoll, wingsLevelInfluence) * rollGain;

            authority = ComputeAuthority();
            pitch *= authority;
            roll *= authority;
            yaw *= authority;

            if (useStallProtection && controller.StallRisk > 0.58f)
            {
                pitch = Mathf.Lerp(pitch, Mathf.Sign(pitch) * Mathf.Min(Mathf.Abs(pitch), 0.22f), controller.StallRisk);
                pitch += stallPitchRelief * controller.StallRisk;
                instructorNote = "stall_protection";
            }
            else if (useGLimiter && Mathf.Abs(controller.LoadFactorEstimate) > gLimit)
            {
                pitch *= 0.45f;
                roll *= 0.75f;
                instructorNote = "g_limiter";
            }
            else
            {
                instructorNote = "mouseflight_aim";
            }

            float kp, kr, ky;
            ReadKeyboard(out kp, out kr, out ky);
            pitch = Mathf.Clamp(pitch + kp * keyboardPitchWeight, -1f, 1f);
            roll = Mathf.Clamp(roll + kr * keyboardRollWeight, -1f, 1f);
            yaw = Mathf.Clamp(yaw + ky * keyboardYawWeight, -1f, 1f);
        }

        private void BuildDirectCommand(out float pitch, out float roll, out float yaw, bool assisted)
        {
            ReadKeyboard(out pitch, out roll, out yaw);

            if (assisted)
            {
                float bank = SignedBankAngle();
                if (Mathf.Abs(roll) < 0.05f)
                    roll += Mathf.Clamp(-bank / 80f, -0.5f, 0.5f);

                if (useStallProtection && controller.StallRisk > 0.62f)
                    pitch = Mathf.Lerp(pitch, 0.25f, controller.StallRisk);
            }

            instructorNote = assisted ? "assisted_direct" : "realistic_direct";
        }

        private float ComputeAuthority()
        {
            float speedAuthority = Mathf.Lerp(lowSpeedAuthority, 1f, Mathf.InverseLerp(45f, 180f, controller.Speed));
            float aoaLoss = Mathf.Lerp(1f, highAoAAuthorityLoss, Mathf.Clamp01(Mathf.Abs(controller.AngleOfAttack) / Mathf.Max(1f, controller.stallAngleDegrees)));
            return Mathf.Clamp(speedAuthority * aoaLoss, 0.15f, 1.15f);
        }

        private void ReadKeyboard(out float pitch, out float roll, out float yaw)
        {
            pitch = 0f;
            roll = 0f;
            yaw = 0f;

            if (!holdWForWep && MaverickInput.GetKey(keys.wep)) pitch -= 1f;
            if (MaverickInput.GetKey(KeyCode.S)) pitch += 1f;

            if (MaverickInput.GetKey(keys.rollLeft)) roll -= 1f;
            if (MaverickInput.GetKey(keys.rollRight)) roll += 1f;
            if (MaverickInput.GetKey(keys.yawLeft)) yaw -= 1f;
            if (MaverickInput.GetKey(keys.yawRight)) yaw += 1f;

            if (invertKeyboardPitch) pitch = -pitch;
            if (invertKeyboardRoll) roll = -roll;
            if (invertKeyboardYaw) yaw = -yaw;
        }

        private float SignedBankAngle()
        {
            Vector3 projected = Vector3.ProjectOnPlane(transform.up, transform.forward);
            if (projected.sqrMagnitude < 0.0001f) return 0f;
            return Vector3.SignedAngle(Vector3.up, projected.normalized, transform.forward);
        }
    }
}
