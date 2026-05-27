using UnityEngine;
using EaglePhysicalAI.Aircraft;

namespace EaglePhysicalAI.Controls
{
    /// <summary>
    /// v1.0 instructor-style controller.
    /// Designed to feel closer to Mouse Aim flight:
    /// mouse cursor -> desired direction -> instructor -> aircraft control inputs.
    /// </summary>
    [RequireComponent(typeof(AircraftPhysicsController))]
    public class MaverickInstructorV10 : ManualAircraftInput
    {
        public MaverickV10ControlMode controlMode = MaverickV10ControlMode.MouseAim;
        public MaverickAimDirectorV10 aimDirector;

        [Header("Output Signs")]
        public float pitchOutputSign = 1f;
        public float rollOutputSign = 1f;
        public float yawOutputSign = 1f;

        [Header("Mouse Aim Tuning")]
        public float pitchGain = 1.75f;
        public float yawGain = 0.85f;
        public float rollGain = 2.15f;
        public float turnBankGain = 1.05f;
        public float maxBankDeg = 72f;
        public float maxPitchCommand = 0.95f;
        public float maxRollCommand = 1.0f;
        public float maxYawCommand = 0.55f;
        public float commandSmoothing = 9f;

        [Header("Instructor Protection")]
        public bool useStallProtection = true;
        public bool useGLimiter = true;
        public bool useAutoLevelNearCenter = true;
        public float stallProtectionStrength = 0.55f;
        public float gLimit = 8.5f;
        public float lowSpeedAuthority = 0.42f;
        public float normalSpeedAuthority = 1.0f;
        public float highAoAAuthorityLoss = 0.40f;

        [Header("Keyboard Overrides")]
        public bool keyboardOverrideEnabled = true;
        public float pitchOverrideWeight = 0.55f;
        public float rollOverrideWeight = 0.85f;
        public float yawOverrideWeight = 0.55f;
        public bool invertKeyboardPitch;
        public bool invertKeyboardRoll;
        public bool invertKeyboardYaw;

        [Header("Throttle")]
        public float startingThrottle = 0.82f;
        public float throttleUpRate = 0.55f;
        public float throttleDownRate = 0.65f;
        public KeyCode throttleUpKey = KeyCode.LeftShift;
        public KeyCode throttleDownKey = KeyCode.LeftControl;
        public KeyCode wepKey = KeyCode.W;
        public KeyCode idleKey = KeyCode.X;
        public bool holdWForWep = true;

        [Header("Buttons")]
        public KeyCode primaryFireKey = KeyCode.Mouse0;
        public KeyCode secondaryFireKey = KeyCode.Space;
        public KeyCode confirmKey = KeyCode.Return;
        public KeyCode abortKey = KeyCode.Backspace;

        [Header("Debug")]
        public Vector3 desiredDirection;
        public float pitchErrorDeg;
        public float yawErrorDeg;
        public float currentBankDeg;
        public float desiredBankDeg;
        public float authority;
        public string instructorState = "idle";

        private float smoothPitch;
        private float smoothRoll;
        private float smoothYaw;
        private float throttleState;

        private void Start()
        {
            if (controller == null) controller = GetComponent<AircraftPhysicsController>();
            if (aimDirector == null) aimDirector = GetComponent<MaverickAimDirectorV10>();
            if (aimDirector == null) aimDirector = gameObject.AddComponent<MaverickAimDirectorV10>();
            aimDirector.aircraftRoot = transform;

            throttleState = controller != null ? Mathf.Max(controller.targetThrottle, startingThrottle) : startingThrottle;
            lastThrottle = throttleState;
        }

        private void Update()
        {
            strikePressed = false;
            confirmPressed = false;
            abortPressed = false;

            if (!inputEnabled || controller == null)
                return;

            UpdateModeKeys();
            UpdateThrottle();

            float pitch;
            float roll;
            float yaw;

            if (controlMode == MaverickV10ControlMode.RealisticDirect)
            {
                ReadKeyboardAxes(out pitch, out roll, out yaw);
                instructorState = "realistic_direct";
            }
            else if (controlMode == MaverickV10ControlMode.AssistedDirect)
            {
                ReadKeyboardAxes(out pitch, out roll, out yaw);
                ApplyAssistedStabilization(ref pitch, ref roll, ref yaw);
                instructorState = "assisted_direct";
            }
            else if (controlMode == MaverickV10ControlMode.AIManaged)
            {
                instructorState = "ai_managed";
                return;
            }
            else
            {
                BuildMouseAimCommand(out pitch, out roll, out yaw);
            }

            float alpha = 1f - Mathf.Exp(-commandSmoothing * Time.deltaTime);
            smoothPitch = Mathf.Lerp(smoothPitch, pitch, alpha);
            smoothRoll = Mathf.Lerp(smoothRoll, roll, alpha);
            smoothYaw = Mathf.Lerp(smoothYaw, yaw, alpha);

            lastPitch = Mathf.Clamp(smoothPitch * pitchOutputSign, -1f, 1f);
            lastRoll = Mathf.Clamp(smoothRoll * rollOutputSign, -1f, 1f);
            lastYaw = Mathf.Clamp(smoothYaw * yawOutputSign, -1f, 1f);
            lastThrottle = Mathf.Clamp01(throttleState);

            strikePressed = MaverickInput.GetKeyDown(primaryFireKey) || MaverickInput.GetKeyDown(secondaryFireKey);
            confirmPressed = MaverickInput.GetKeyDown(confirmKey) || MaverickInput.GetKeyDown(KeyCode.F);
            abortPressed = MaverickInput.GetKeyDown(abortKey);

            controller.SetControlInputs(lastPitch, lastRoll, lastYaw, lastThrottle);
        }

        private void UpdateModeKeys()
        {
            if (MaverickInput.GetKeyDown(KeyCode.F5)) controlMode = MaverickV10ControlMode.MouseAim;
            if (MaverickInput.GetKeyDown(KeyCode.F6)) controlMode = MaverickV10ControlMode.AssistedDirect;
            if (MaverickInput.GetKeyDown(KeyCode.F7)) controlMode = MaverickV10ControlMode.RealisticDirect;
            if (MaverickInput.GetKeyDown(KeyCode.F8)) controlMode = MaverickV10ControlMode.AIManaged;
        }

        private void UpdateThrottle()
        {
            if (MaverickInput.GetKey(throttleUpKey)) throttleState += throttleUpRate * Time.deltaTime;
            if (MaverickInput.GetKey(throttleDownKey)) throttleState -= throttleDownRate * Time.deltaTime;

            if (holdWForWep && MaverickInput.GetKey(wepKey))
                throttleState = Mathf.MoveTowards(throttleState, 1f, throttleUpRate * 3f * Time.deltaTime);

            if (MaverickInput.GetKey(idleKey))
                throttleState = Mathf.MoveTowards(throttleState, 0.05f, throttleDownRate * 3f * Time.deltaTime);

            throttleState = Mathf.Clamp01(throttleState);
        }

        private void BuildMouseAimCommand(out float pitch, out float roll, out float yaw)
        {
            if (aimDirector == null)
            {
                ReadKeyboardAxes(out pitch, out roll, out yaw);
                return;
            }

            desiredDirection = aimDirector.desiredWorldDirection.sqrMagnitude > 0.001f
                ? aimDirector.desiredWorldDirection.normalized
                : transform.forward;

            Vector3 local = transform.InverseTransformDirection(desiredDirection).normalized;

            yawErrorDeg = Mathf.Atan2(local.x, Mathf.Max(0.001f, local.z)) * Mathf.Rad2Deg;
            pitchErrorDeg = -Mathf.Atan2(local.y, Mathf.Max(0.001f, local.z)) * Mathf.Rad2Deg;

            authority = ComputeAuthority();

            float autoPitch = Mathf.Clamp(pitchErrorDeg / 36f * pitchGain, -maxPitchCommand, maxPitchCommand) * authority;
            float autoYaw = Mathf.Clamp(yawErrorDeg / 55f * yawGain, -maxYawCommand, maxYawCommand) * authority;

            currentBankDeg = SignedBankAngle();
            desiredBankDeg = Mathf.Clamp(yawErrorDeg * turnBankGain, -maxBankDeg, maxBankDeg);
            float bankError = Mathf.DeltaAngle(currentBankDeg, desiredBankDeg);
            float autoRoll = Mathf.Clamp(bankError / 70f * rollGain, -maxRollCommand, maxRollCommand) * authority;

            if (useAutoLevelNearCenter && Mathf.Abs(yawErrorDeg) < 2.5f)
                autoRoll += Mathf.Clamp(-currentBankDeg / 90f, -0.25f, 0.25f);

            if (useStallProtection && controller.StallRisk > 0.55f)
            {
                autoPitch = Mathf.Lerp(autoPitch, Mathf.Sign(autoPitch) * Mathf.Min(Mathf.Abs(autoPitch), 0.25f), controller.StallRisk * stallProtectionStrength);
                autoPitch += 0.18f * controller.StallRisk;
                instructorState = "stall_protection";
            }
            else if (useGLimiter && Mathf.Abs(controller.LoadFactorEstimate) > gLimit)
            {
                autoPitch *= 0.45f;
                autoRoll *= 0.80f;
                instructorState = "g_limiter";
            }
            else
            {
                instructorState = "mouse_aim";
            }

            float keyPitch;
            float keyRoll;
            float keyYaw;
            ReadKeyboardAxes(out keyPitch, out keyRoll, out keyYaw);

            pitch = Mathf.Clamp(autoPitch + keyPitch * pitchOverrideWeight, -1f, 1f);
            roll = Mathf.Clamp(autoRoll + keyRoll * rollOverrideWeight, -1f, 1f);
            yaw = Mathf.Clamp(autoYaw + keyYaw * yawOverrideWeight, -1f, 1f);
        }

        private float ComputeAuthority()
        {
            float speedA = Mathf.Lerp(lowSpeedAuthority, normalSpeedAuthority, Mathf.InverseLerp(45f, 175f, controller.Speed));
            float aoaLoss = Mathf.Lerp(1f, highAoAAuthorityLoss, Mathf.Clamp01(Mathf.Abs(controller.AngleOfAttack) / Mathf.Max(1f, controller.stallAngleDegrees)));
            return Mathf.Clamp(speedA * aoaLoss, 0.15f, 1.15f);
        }

        private void ApplyAssistedStabilization(ref float pitch, ref float roll, ref float yaw)
        {
            if (Mathf.Abs(roll) < 0.05f)
                roll += Mathf.Clamp(-SignedBankAngle() / 80f, -0.6f, 0.6f);

            if (useStallProtection && controller.StallRisk > 0.62f)
                pitch = Mathf.Lerp(pitch, 0.25f, controller.StallRisk * stallProtectionStrength);
        }

        private void ReadKeyboardAxes(out float pitch, out float roll, out float yaw)
        {
            pitch = 0f;
            roll = 0f;
            yaw = 0f;

            if (!holdWForWep && MaverickInput.GetKey(KeyCode.W)) pitch -= 1f;
            if (MaverickInput.GetKey(KeyCode.S)) pitch += 1f;

            if (MaverickInput.GetKey(KeyCode.A)) roll -= 1f;
            if (MaverickInput.GetKey(KeyCode.D)) roll += 1f;
            if (MaverickInput.GetKey(KeyCode.Q)) yaw -= 1f;
            if (MaverickInput.GetKey(KeyCode.E)) yaw += 1f;

            if (invertKeyboardPitch) pitch = -pitch;
            if (invertKeyboardRoll) roll = -roll;
            if (invertKeyboardYaw) yaw = -yaw;
        }

        private float SignedBankAngle()
        {
            Vector3 upProjected = Vector3.ProjectOnPlane(transform.up, transform.forward);
            if (upProjected.sqrMagnitude < 0.0001f) return 0f;
            return Vector3.SignedAngle(Vector3.up, upProjected.normalized, transform.forward);
        }
    }
}
