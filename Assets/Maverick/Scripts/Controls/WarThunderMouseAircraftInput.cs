using UnityEngine;
using EaglePhysicalAI.Aircraft;

namespace EaglePhysicalAI.Controls
{
    /// <summary>
    /// War-Thunder-like mouse aim input layer for the simplified aircraft physics controller.
    ///
    /// Design goal:
    /// - Mouse controls a desired flight direction / aim reticle, not raw torque directly.
    /// - The script converts direction error into pitch/yaw/roll inputs.
    /// - Keyboard still provides throttle, rudder, roll override, pitch override, brakes/airbrake-like assists.
    /// - It inherits ManualAircraftInput so existing telemetry, behavior-cloning, and runtime-agent scripts
    ///   that read ManualAircraftInput continue to work without refactoring.
    ///
    /// This is a game-feel controller, not an aircraft avionics simulation.
    /// </summary>
    [RequireComponent(typeof(AircraftPhysicsController))]
    public class WarThunderMouseAircraftInput : ManualAircraftInput
    {
        [Header("War Thunder Style Control")]
        public WarThunderControlMode controlMode = WarThunderControlMode.MouseAim;
        public Camera viewCamera;
        public bool lockCursorOnPlay = true;
        public KeyCode toggleMouseAimKey = KeyCode.F5;
        public KeyCode toggleKeyboardDirectKey = KeyCode.F6;
        public KeyCode toggleStabilizedKeyboardKey = KeyCode.F7;
        public KeyCode freeLookKey = KeyCode.LeftAlt;

        [Header("Mouse Aim Reticle")]
        [Tooltip("Normalized viewport position. (0.5, 0.5) is screen center.")]
        public Vector2 aimViewport = new Vector2(0.5f, 0.5f);
        public float mouseSensitivity = 0.0018f;
        public float reticleLimit = 0.42f;
        public float reticleReturnSpeed = 0.15f;
        public bool returnReticleWhenFreeLook = false;

        [Header("Flight Director Gains")]
        public float pitchGain = 2.25f;
        public float yawGain = 1.65f;
        public float rollGain = 1.35f;
        public float autoLevelGain = 0.55f;
        public float turnBankGain = 0.85f;
        public float maxAutoBankDegrees = 68f;
        public float lowSpeedDamping = 0.45f;
        public float stallPitchDownAssist = 0.55f;
        public float smoothing = 7.5f;

        [Header("Keyboard Assist")]
        public float keyboardRollWeight = 0.9f;
        public float keyboardPitchWeight = 0.75f;
        public float keyboardYawWeight = 0.8f;
        public bool useWasdForPitchRoll = true;
        public bool useQEForRudder = true;

        [Header("Throttle / Engine Feel")]
        public KeyCode throttleUpKey = KeyCode.LeftShift;
        public KeyCode throttleDownKey = KeyCode.LeftControl;
        public KeyCode wepKey = KeyCode.W;
        public KeyCode idleKey = KeyCode.X;
        public float wepThrottle = 1.0f;
        public float idleThrottle = 0.08f;
        public bool holdWForWep = true;
        public bool holdXForIdle = true;

        [Header("Combat Buttons")]
        public KeyCode primaryFireKey = KeyCode.Mouse0;
        public KeyCode secondaryFireKey = KeyCode.Space;
        public KeyCode confirmKey = KeyCode.F;
        public KeyCode abortKey = KeyCode.Backspace;

        [Header("Debug")]
        public Vector3 desiredWorldDirection;
        public float directionPitchError;
        public float directionYawError;
        public float desiredBank;
        public bool freeLookActive;
        public bool mouseAimEnabled = true;

        private float smoothedPitch;
        private float smoothedRoll;
        private float smoothedYaw;
        private float workingThrottle;

        private void Awake()
        {
            if (controller == null) controller = GetComponent<AircraftPhysicsController>();
            if (viewCamera == null) viewCamera = Camera.main;
            workingThrottle = controller != null ? controller.targetThrottle : 0.65f;
            lastThrottle = workingThrottle;
        }

        private void OnEnable()
        {
            if (lockCursorOnPlay)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void OnDisable()
        {
            if (lockCursorOnPlay)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void Update()
        {
            strikePressed = false;
            confirmPressed = false;
            abortPressed = false;

            if (MaverickInput.GetKeyDown(toggleMouseAimKey)) controlMode = WarThunderControlMode.MouseAim;
            if (MaverickInput.GetKeyDown(toggleKeyboardDirectKey)) controlMode = WarThunderControlMode.KeyboardDirect;
            if (MaverickInput.GetKeyDown(toggleStabilizedKeyboardKey)) controlMode = WarThunderControlMode.StabilizedKeyboard;

            if (!inputEnabled || controller == null) return;
            if (viewCamera == null) viewCamera = Camera.main;

            freeLookActive = MaverickInput.GetKey(freeLookKey);

            UpdateThrottle();
            UpdateReticle();

            float pitch;
            float roll;
            float yaw;

            switch (controlMode)
            {
                case WarThunderControlMode.KeyboardDirect:
                    BuildKeyboardDirect(out pitch, out roll, out yaw);
                    break;
                case WarThunderControlMode.StabilizedKeyboard:
                    BuildStabilizedKeyboard(out pitch, out roll, out yaw);
                    break;
                default:
                    BuildMouseAim(out pitch, out roll, out yaw);
                    break;
            }

            smoothedPitch = Mathf.Lerp(smoothedPitch, pitch, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
            smoothedRoll = Mathf.Lerp(smoothedRoll, roll, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
            smoothedYaw = Mathf.Lerp(smoothedYaw, yaw, 1f - Mathf.Exp(-smoothing * Time.deltaTime));

            lastPitch = Mathf.Clamp(smoothedPitch, -1f, 1f);
            lastRoll = Mathf.Clamp(smoothedRoll, -1f, 1f);
            lastYaw = Mathf.Clamp(smoothedYaw, -1f, 1f);
            lastThrottle = workingThrottle;

            strikePressed = MaverickInput.GetKeyDown(primaryFireKey) || MaverickInput.GetKeyDown(secondaryFireKey);
            confirmPressed = MaverickInput.GetKeyDown(confirmKey);
            abortPressed = MaverickInput.GetKeyDown(abortKey);

            controller.SetControlInputs(lastPitch, lastRoll, lastYaw, lastThrottle);
        }

        private void UpdateThrottle()
        {
            if (MaverickInput.GetKey(throttleUpKey)) workingThrottle += throttleChangePerSecond * Time.deltaTime;
            if (MaverickInput.GetKey(throttleDownKey)) workingThrottle -= throttleChangePerSecond * Time.deltaTime;

            if (holdWForWep && MaverickInput.GetKey(wepKey))
            {
                workingThrottle = Mathf.MoveTowards(workingThrottle, wepThrottle, throttleChangePerSecond * 2.0f * Time.deltaTime);
            }

            if (holdXForIdle && MaverickInput.GetKey(idleKey))
            {
                workingThrottle = Mathf.MoveTowards(workingThrottle, idleThrottle, throttleChangePerSecond * 2.0f * Time.deltaTime);
            }

            workingThrottle = Mathf.Clamp01(workingThrottle);
        }

        private void UpdateReticle()
        {
            if (controlMode != WarThunderControlMode.MouseAim)
            {
                aimViewport = Vector2.Lerp(aimViewport, new Vector2(0.5f, 0.5f), reticleReturnSpeed * Time.deltaTime);
                return;
            }

            bool shouldMoveReticle = !freeLookActive;
            if (shouldMoveReticle)
            {
                Vector2 delta = new Vector2(MaverickInput.GetAxisRaw("Mouse X"), MaverickInput.GetAxisRaw("Mouse Y"));
                aimViewport += delta * mouseSensitivity;
            }
            else if (returnReticleWhenFreeLook)
            {
                aimViewport = Vector2.Lerp(aimViewport, new Vector2(0.5f, 0.5f), reticleReturnSpeed * Time.deltaTime);
            }

            Vector2 offset = aimViewport - new Vector2(0.5f, 0.5f);
            if (offset.magnitude > reticleLimit)
            {
                offset = offset.normalized * reticleLimit;
                aimViewport = new Vector2(0.5f, 0.5f) + offset;
            }
        }

        private void BuildMouseAim(out float pitch, out float roll, out float yaw)
        {
            if (viewCamera == null)
            {
                BuildStabilizedKeyboard(out pitch, out roll, out yaw);
                return;
            }

            Ray aimRay = viewCamera.ViewportPointToRay(new Vector3(aimViewport.x, aimViewport.y, 0f));
            desiredWorldDirection = aimRay.direction.normalized;
            Vector3 localDesired = transform.InverseTransformDirection(desiredWorldDirection).normalized;

            directionYawError = Mathf.Atan2(localDesired.x, Mathf.Max(0.01f, localDesired.z)) * Mathf.Rad2Deg;
            directionPitchError = -Mathf.Atan2(localDesired.y, Mathf.Max(0.01f, localDesired.z)) * Mathf.Rad2Deg;

            float speedAuthority = Mathf.InverseLerp(35f, 140f, controller.Speed);
            float authorityDamping = Mathf.Lerp(lowSpeedDamping, 1f, speedAuthority);

            float autoPitch = Mathf.Clamp(directionPitchError / 35f * pitchGain, -1f, 1f) * authorityDamping;
            float autoYaw = Mathf.Clamp(directionYawError / 45f * yawGain, -1f, 1f) * authorityDamping;

            float targetBank = Mathf.Clamp(directionYawError * turnBankGain, -maxAutoBankDegrees, maxAutoBankDegrees);
            desiredBank = targetBank;
            float currentBank = GetSignedBankAngle();
            float bankError = Mathf.DeltaAngle(currentBank, targetBank);
            float autoRoll = Mathf.Clamp(bankError / 65f * rollGain, -1f, 1f);

            // Keep the aircraft from pulling itself deeper into a stall.
            if (controller.StallRisk > 0.65f)
            {
                autoPitch = Mathf.Lerp(autoPitch, Mathf.Abs(autoPitch) < 0.15f ? 0.15f : Mathf.Abs(autoPitch), stallPitchDownAssist * controller.StallRisk);
            }

            float keyPitch = 0f;
            float keyRoll = 0f;
            float keyYaw = 0f;
            ReadKeyboardAxes(out keyPitch, out keyRoll, out keyYaw);

            pitch = Mathf.Clamp(autoPitch + keyPitch * keyboardPitchWeight, -1f, 1f);
            roll = Mathf.Clamp(autoRoll + keyRoll * keyboardRollWeight, -1f, 1f);
            yaw = Mathf.Clamp(autoYaw + keyYaw * keyboardYawWeight, -1f, 1f);
        }

        private void BuildKeyboardDirect(out float pitch, out float roll, out float yaw)
        {
            ReadKeyboardAxes(out pitch, out roll, out yaw);
        }

        private void BuildStabilizedKeyboard(out float pitch, out float roll, out float yaw)
        {
            float keyPitch;
            float keyRoll;
            float keyYaw;
            ReadKeyboardAxes(out keyPitch, out keyRoll, out keyYaw);

            float currentBank = GetSignedBankAngle();
            float levelRoll = Mathf.Clamp(-currentBank / 70f * autoLevelGain, -0.8f, 0.8f);

            // If no roll key is pressed, gently level the plane. If a roll key is pressed, let the player bank.
            roll = Mathf.Abs(keyRoll) > 0.05f ? keyRoll : levelRoll;
            pitch = keyPitch;
            yaw = keyYaw;
        }

        private void ReadKeyboardAxes(out float pitch, out float roll, out float yaw)
        {
            pitch = 0f;
            roll = 0f;
            yaw = 0f;

            if (useWasdForPitchRoll)
            {
                if (MaverickInput.GetKey(KeyCode.S)) pitch += 1f;
                if (MaverickInput.GetKey(KeyCode.W) && !holdWForWep) pitch -= 1f;
                if (MaverickInput.GetKey(KeyCode.A)) roll -= 1f;
                if (MaverickInput.GetKey(KeyCode.D)) roll += 1f;
            }
            else
            {
                pitch = invertPitch ? -MaverickInput.GetAxis("Vertical") : MaverickInput.GetAxis("Vertical");
                roll = MaverickInput.GetAxis("Horizontal");
            }

            if (useQEForRudder)
            {
                if (MaverickInput.GetKey(KeyCode.Q)) yaw -= 1f;
                if (MaverickInput.GetKey(KeyCode.E)) yaw += 1f;
            }
        }

        private float GetSignedBankAngle()
        {
            Vector3 projectedUp = Vector3.ProjectOnPlane(transform.up, transform.forward).normalized;
            if (projectedUp.sqrMagnitude < 0.0001f) return 0f;
            return Vector3.SignedAngle(Vector3.up, projectedUp, transform.forward);
        }

        public Vector2 GetReticleViewport()
        {
            return aimViewport;
        }
    }
}
