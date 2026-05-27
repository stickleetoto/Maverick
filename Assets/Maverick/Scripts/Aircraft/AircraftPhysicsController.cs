using UnityEngine;

namespace EaglePhysicalAI.Aircraft
{
    /// <summary>
    /// Simplified non-realistic aircraft physics for game/AI experiments.
    /// Unity handles transforms/collisions; this script provides abstract lift, drag, thrust, and control torque.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class AircraftPhysicsController : MonoBehaviour
    {
        [Header("References")]
        public Rigidbody rb;

        [Header("Mass / Engine")]
        public float maxThrust = 85000f;
        public float throttleResponse = 0.8f;
        [Range(0f, 1f)] public float throttle;

        [Header("Aerodynamics - Abstract")]
        public float liftCoefficient = 0.85f;
        public float dragCoefficient = 0.035f;
        public float inducedDragCoefficient = 0.015f;
        public float stallAngleDegrees = 28f;
        public float minLiftSpeed = 35f;

        [Header("Control Authority")]
        public float pitchTorque = 22000f;
        public float rollTorque = 28000f;
        public float yawTorque = 9000f;
        public float angularDamping = 0.55f;

        [Header("v0.9 Axis / Feel Tuning")]
        [Tooltip("Use this to fix imported-model roll direction without rewriting controls. If A/D is reversed, flip this between 1 and -1.")]
        public float rollTorqueSign = 1f;
        [Tooltip("Use this to fix pitch direction if needed.")]
        public float pitchTorqueSign = 1f;
        [Tooltip("Use this to fix rudder/yaw direction if needed.")]
        public float yawTorqueSign = 1f;
        [Tooltip("Soft cap for angular velocity. Higher = twitchier, lower = heavier jet feeling.")]
        public float maxAngularVelocity = 3.2f;
        [Tooltip("How strongly control authority fades at extreme low speed.")]
        public float lowSpeedControlFloor = 0.22f;
        [Tooltip("How much authority can increase around normal maneuvering speed.")]
        public float highSpeedControlCeiling = 1.15f;

        [Header("Limits / Warnings")]
        public float crashAltitude = -10f;
        public float overspeedWarning = 420f;
        public float stallWarningSpeed = 55f;

        [Header("Input State")]
        [Range(-1f, 1f)] public float pitchInput;
        [Range(-1f, 1f)] public float rollInput;
        [Range(-1f, 1f)] public float yawInput;
        [Range(0f, 1f)] public float targetThrottle;

        public float Speed { get; private set; }
        public float ForwardSpeed { get; private set; }
        public float Altitude => transform.position.y;
        public float AngleOfAttack { get; private set; }
        public float StallRisk { get; private set; }
        public float LoadFactorEstimate { get; private set; }
        public Vector3 LocalVelocity { get; private set; }
        public bool IsStalling { get; private set; }
        public bool IsCrashed { get; private set; }

        private Vector3 _lastVelocity;

        private void Reset()
        {
            rb = GetComponent<Rigidbody>();
            rb.mass = 14500f;
            rb.useGravity = true;
            rb.linearDamping = 0f;
            rb.angularDamping = 0.15f;
            targetThrottle = 0.65f;
            throttle = 0.65f;
        }

        private void Awake()
        {
            if (rb == null) rb = GetComponent<Rigidbody>();
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.maxAngularVelocity = maxAngularVelocity;
            _lastVelocity = rb.linearVelocity;
        }

        private void FixedUpdate()
        {
            if (IsCrashed) return;

            UpdateState();
            ApplyThrottleResponse();
            ApplyForces();
            ApplyControlTorques();
            ApplyAngularDamping();
            CheckCrashState();
        }

        public void SetControlInputs(float pitch, float roll, float yaw, float throttle01)
        {
            pitchInput = Mathf.Clamp(pitch, -1f, 1f);
            rollInput = Mathf.Clamp(roll, -1f, 1f);
            yawInput = Mathf.Clamp(yaw, -1f, 1f);
            targetThrottle = Mathf.Clamp01(throttle01);
        }

        public void SetCrashed(bool crashed)
        {
            IsCrashed = crashed;
        }

        private void UpdateState()
        {
            Speed = rb.linearVelocity.magnitude;
            ForwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
            LocalVelocity = transform.InverseTransformDirection(rb.linearVelocity);

            Vector3 acceleration = (rb.linearVelocity - _lastVelocity) / Mathf.Max(Time.fixedDeltaTime, 0.0001f);
            LoadFactorEstimate = Vector3.Dot(acceleration + Physics.gravity, transform.up) / 9.80665f;
            _lastVelocity = rb.linearVelocity;

            if (Speed > 1f)
            {
                AngleOfAttack = Vector3.SignedAngle(transform.forward, rb.linearVelocity.normalized, transform.right);
            }
            else
            {
                AngleOfAttack = 0f;
            }

            float aoaRatio = Mathf.Clamp01(Mathf.Abs(AngleOfAttack) / Mathf.Max(1f, stallAngleDegrees));
            float speedRisk = Mathf.InverseLerp(stallWarningSpeed, minLiftSpeed, Mathf.Abs(ForwardSpeed));
            StallRisk = Mathf.Clamp01(Mathf.Max(aoaRatio, speedRisk));
            IsStalling = Mathf.Abs(AngleOfAttack) > stallAngleDegrees || Mathf.Abs(ForwardSpeed) < minLiftSpeed;
        }

        private void ApplyThrottleResponse()
        {
            throttle = Mathf.MoveTowards(throttle, targetThrottle, throttleResponse * Time.fixedDeltaTime);
        }

        private void ApplyForces()
        {
            Vector3 thrust = transform.forward * (throttle * maxThrust);
            rb.AddForce(thrust, ForceMode.Force);

            float forwardSpeedAbs = Mathf.Max(0f, ForwardSpeed);
            float aoaPenalty = Mathf.Clamp01(1f - Mathf.Abs(AngleOfAttack) / Mathf.Max(1f, stallAngleDegrees));
            float stallPenalty = IsStalling ? 0.35f : 1f;
            float liftMagnitude = liftCoefficient * forwardSpeedAbs * forwardSpeedAbs * aoaPenalty * stallPenalty;
            rb.AddForce(transform.up * liftMagnitude, ForceMode.Force);

            if (Speed > 0.1f)
            {
                float dragMagnitude = dragCoefficient * Speed * Speed;
                float inducedDrag = inducedDragCoefficient * Mathf.Abs(pitchInput) * Speed * Speed;
                rb.AddForce(-rb.linearVelocity.normalized * (dragMagnitude + inducedDrag), ForceMode.Force);
            }
        }

        private void ApplyControlTorques()
        {
            float authority = Mathf.InverseLerp(20f, 140f, Mathf.Abs(ForwardSpeed));
            authority = Mathf.Clamp(authority, lowSpeedControlFloor, highSpeedControlCeiling);

            Vector3 torque = Vector3.zero;
            torque += transform.right * (pitchInput * pitchTorque * authority * pitchTorqueSign);
            torque += transform.forward * (rollInput * rollTorque * authority * rollTorqueSign);
            torque += transform.up * (yawInput * yawTorque * authority * yawTorqueSign);
            rb.AddTorque(torque, ForceMode.Force);
        }

        private void ApplyAngularDamping()
        {
            rb.AddTorque(-rb.angularVelocity * angularDamping, ForceMode.Acceleration);
        }

        private void CheckCrashState()
        {
            if (transform.position.y < crashAltitude)
            {
                IsCrashed = true;
                rb.linearVelocity *= 0.15f;
                rb.angularVelocity *= 0.15f;
            }
        }
    }
}
