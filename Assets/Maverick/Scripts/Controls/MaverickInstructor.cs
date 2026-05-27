using UnityEngine;
using EaglePhysicalAI.Aircraft;

namespace EaglePhysicalAI.Controls
{
    /// <summary>
    /// War-Thunder-like instructor layer.
    /// Converts mouse aim direction into stabilized pitch/roll/yaw commands.
    /// Inherits ManualAircraftInput so existing data loggers and AI mode systems can still read manual input snapshots.
    /// </summary>
    [RequireComponent(typeof(AircraftPhysicsController))]
    public class MaverickInstructor : ManualAircraftInput
    {
        [Header("v0.9 Instructor")]
        public MaverickControlMode controlMode = MaverickControlMode.MouseAim;
        public MaverickMouseAimDirector aimDirector;
        public bool instructorEnabled = true;

        [Header("Mouse Aim Gains")]
        public float pitchGain = 2.05f;
        public float yawGain = 1.25f;
        public float rollGain = 1.8f;
        public float turnBankGain = 0.95f;
        public float maxAutoBankDegrees = 72f;
        public float autoLevelGain = 0.45f;
        public float commandSmoothing = 8.5f;

        [Header("Safety / Feel")]
        public float stallRiskPitchDown = 0.7f;
        public float gLimit = 8.5f;
        public float gLimitSoftness = 0.45f;
        public float lowSpeedDamping = 0.45f;
        public float highAoADamping = 0.65f;
        public bool useRollToTurn = true;
        public bool useYawToAimAssist = true;

        [Header("Keyboard Overrides")]
        public bool keyboardOverrides = true;
        public bool invertKeyboardRoll = false;
        public bool invertKeyboardPitch = false;
        public bool invertKeyboardYaw = false;
        public float keyboardPitchWeight = 0.72f;
        public float keyboardRollWeight = 0.95f;
        public float keyboardYawWeight = 0.65f;

        [Header("Throttle")]
        public KeyCode throttleUpKey = KeyCode.LeftShift;
        public KeyCode throttleDownKey = KeyCode.LeftControl;
        public KeyCode wepKey = KeyCode.W;
        public KeyCode idleKey = KeyCode.X;
        public float wepThrottle = 1f;
        public float idleThrottle = 0.08f;
        public bool holdWForWep = true;
        public float throttleResponseBoost = 2f;

        [Header("Buttons")]
        public KeyCode primaryFireKey = KeyCode.Mouse0;
        public KeyCode secondaryFireKey = KeyCode.Space;
        public KeyCode confirmKey = KeyCode.F;
        public KeyCode abortKey = KeyCode.Backspace;

        [Header("Debug")]
        public Vector3 desiredDirection;
        public float pitchErrorDeg;
        public float yawErrorDeg;
        public float desiredBankDeg;
        public float currentBankDeg;
        public string instructorNote = "idle";

        private float smoothedPitch;
        private float smoothedRoll;
        private float smoothedYaw;
        private float workingThrottle = 0.72f;

        private void Awake()
        {
            if (controller == null) controller = GetComponent<AircraftPhysicsController>();
            if (aimDirector == null) aimDirector = GetComponent<MaverickMouseAimDirector>();
            if (aimDirector == null) aimDirector = gameObject.AddComponent<MaverickMouseAimDirector>();
            aimDirector.aircraftRoot = transform;
            workingThrottle = controller != null ? Mathf.Clamp01(controller.targetThrottle) : 0.72f;
            lastThrottle = workingThrottle;
        }

        private void Update()
        {
            strikePressed = false;
            confirmPressed = false;
            abortPressed = false;

            if (!inputEnabled || !instructorEnabled || controller == null)
                return;

            UpdateThrottle();

            float pitch, roll, yaw;
            switch (controlMode)
            {
                case MaverickControlMode.RealisticDirect:
                    BuildDirect(out pitch, out roll, out yaw, stabilize: false);
                    break;
                case MaverickControlMode.AssistedDirect:
                    BuildDirect(out pitch, out roll, out yaw, stabilize: true);
                    break;
                case MaverickControlMode.AIManaged:
                    // AI systems own controls. Keep snapshots but do not write.
                    instructorNote = "ai_managed";
                    return;
                default:
                    BuildMouseAim(out pitch, out roll, out yaw);
                    break;
            }

            float alpha = 1f - Mathf.Exp(-commandSmoothing * Time.deltaTime);
            smoothedPitch = Mathf.Lerp(smoothedPitch, pitch, alpha);
            smoothedRoll = Mathf.Lerp(smoothedRoll, roll, alpha);
            smoothedYaw = Mathf.Lerp(smoothedYaw, yaw, alpha);

            lastPitch = Mathf.Clamp(smoothedPitch, -1f, 1f);
            lastRoll = Mathf.Clamp(smoothedRoll, -1f, 1f);
            lastYaw = Mathf.Clamp(smoothedYaw, -1f, 1f);
            lastThrottle = Mathf.Clamp01(workingThrottle);

            strikePressed = MaverickInput.GetKeyDown(primaryFireKey) || MaverickInput.GetKeyDown(secondaryFireKey);
            confirmPressed = MaverickInput.GetKeyDown(confirmKey) || MaverickInput.GetKeyDown(KeyCode.Return);
            abortPressed = MaverickInput.GetKeyDown(abortKey);

            controller.SetControlInputs(lastPitch, lastRoll, lastYaw, lastThrottle);
        }

        private void UpdateThrottle()
        {
            if (MaverickInput.GetKey(throttleUpKey)) workingThrottle += throttleChangePerSecond * Time.deltaTime;
            if (MaverickInput.GetKey(throttleDownKey)) workingThrottle -= throttleChangePerSecond * Time.deltaTime;

            if (holdWForWep && MaverickInput.GetKey(wepKey))
                workingThrottle = Mathf.MoveTowards(workingThrottle, wepThrottle, throttleChangePerSecond * throttleResponseBoost * Time.deltaTime);

            if (MaverickInput.GetKey(idleKey))
                workingThrottle = Mathf.MoveTowards(workingThrottle, idleThrottle, throttleChangePerSecond * throttleResponseBoost * Time.deltaTime);

            workingThrottle = Mathf.Clamp01(workingThrottle);
        }

        private void BuildMouseAim(out float pitch, out float roll, out float yaw)
        {
            if (aimDirector == null)
            {
                BuildDirect(out pitch, out roll, out yaw, stabilize: true);
                return;
            }

            desiredDirection = aimDirector.desiredWorldDirection.sqrMagnitude > 0.01f ? aimDirector.desiredWorldDirection.normalized : transform.forward;
            Vector3 localDesired = transform.InverseTransformDirection(desiredDirection).normalized;

            yawErrorDeg = Mathf.Atan2(localDesired.x, Mathf.Max(0.01f, localDesired.z)) * Mathf.Rad2Deg;
            pitchErrorDeg = -Mathf.Atan2(localDesired.y, Mathf.Max(0.01f, localDesired.z)) * Mathf.Rad2Deg;

            float speedAuthority = Mathf.InverseLerp(45f, 180f, controller.Speed);
            float aoaDamping = Mathf.Lerp(1f, highAoADamping, Mathf.Clamp01(Mathf.Abs(controller.AngleOfAttack) / Mathf.Max(1f, controller.stallAngleDegrees)));
            float authority = Mathf.Lerp(lowSpeedDamping, 1f, speedAuthority) * aoaDamping;

            float autoPitch = Mathf.Clamp(pitchErrorDeg / 36f * pitchGain, -1f, 1f) * authority;
            float autoYaw = useYawToAimAssist ? Mathf.Clamp(yawErrorDeg / 50f * yawGain, -1f, 1f) * authority : 0f;

            currentBankDeg = GetSignedBankAngle();
            desiredBankDeg = useRollToTurn ? Mathf.Clamp(yawErrorDeg * turnBankGain, -maxAutoBankDegrees, maxAutoBankDegrees) : 0f;
            float bankError = Mathf.DeltaAngle(currentBankDeg, desiredBankDeg);
            float autoRoll = Mathf.Clamp(bankError / 65f * rollGain, -1f, 1f);

            if (controller.StallRisk > 0.62f)
            {
                // Positive pitch command here means "nose down" for the current abstract model.
                autoPitch = Mathf.Lerp(autoPitch, Mathf.Abs(autoPitch) + 0.12f, controller.StallRisk * stallRiskPitchDown);
                instructorNote = "stall_protection";
            }
            else if (Mathf.Abs(controller.LoadFactorEstimate) > gLimit)
            {
                autoPitch *= gLimitSoftness;
                instructorNote = "g_limiter";
            }
            else
            {
                instructorNote = "mouse_aim";
            }

            float keyPitch, keyRoll, keyYaw;
            ReadKeyboardOverrideAxes(out keyPitch, out keyRoll, out keyYaw);

            pitch = Mathf.Clamp(autoPitch + keyPitch * keyboardPitchWeight, -1f, 1f);
            roll = Mathf.Clamp(autoRoll + keyRoll * keyboardRollWeight, -1f, 1f);
            yaw = Mathf.Clamp(autoYaw + keyYaw * keyboardYawWeight, -1f, 1f);
        }

        private void BuildDirect(out float pitch, out float roll, out float yaw, bool stabilize)
        {
            ReadKeyboardOverrideAxes(out pitch, out roll, out yaw);

            if (stabilize && Mathf.Abs(roll) < 0.05f)
            {
                currentBankDeg = GetSignedBankAngle();
                roll = Mathf.Clamp(-currentBankDeg / 75f * autoLevelGain, -0.75f, 0.75f);
            }

            if (stabilize && controller.StallRisk > 0.68f)
            {
                pitch = Mathf.Lerp(pitch, 0.35f, controller.StallRisk * stallRiskPitchDown);
                instructorNote = "assisted_stall_protection";
            }
            else
            {
                instructorNote = stabilize ? "assisted_direct" : "realistic_direct";
            }
        }

        private void ReadKeyboardOverrideAxes(out float pitch, out float roll, out float yaw)
        {
            pitch = 0f;
            roll = 0f;
            yaw = 0f;

            // War Thunder-like: W usually power/WEP here. S is elevator-down override.
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

        private float GetSignedBankAngle()
        {
            Vector3 projectedUp = Vector3.ProjectOnPlane(transform.up, transform.forward);
            if (projectedUp.sqrMagnitude < 0.0001f) return 0f;
            return Vector3.SignedAngle(Vector3.up, projectedUp.normalized, transform.forward);
        }
    }
}
