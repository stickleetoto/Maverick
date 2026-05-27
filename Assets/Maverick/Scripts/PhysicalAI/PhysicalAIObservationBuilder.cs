using UnityEngine;
using EaglePhysicalAI.Aircraft;
using EaglePhysicalAI.Battlefield;
using EaglePhysicalAI.CAS;

namespace EaglePhysicalAI.PhysicalAI
{
    /// <summary>
    /// Converts the current Unity scene state into a fixed-size, normalized vector.
    /// This is the core contract for physical AI, logging, behavior cloning, and future RL.
    /// </summary>
    public class PhysicalAIObservationBuilder : MonoBehaviour
    {
        [Header("References")]
        public AircraftPhysicsController aircraft;
        public CasRequestManager requestManager;
        public CasValidator validator;
        public AbstractStrikeSystem strikeSystem;

        [Header("Normalization")]
        public float maxSpeed = 420f;
        public float maxAltitude = 2200f;
        public float maxVerticalSpeed = 160f;
        public float maxTargetDistance = 6500f;
        public float lowAltitude = 120f;
        public float maxStrikeCountForNorm = 20f;

        public PhysicalAIObservation lastObservation = new PhysicalAIObservation();
        public string lastValidationReason = "none";

        private void Awake()
        {
            if (aircraft == null) aircraft = GetComponent<AircraftPhysicsController>();
            if (requestManager == null) requestManager = FindObjectOfType<CasRequestManager>();
            if (validator == null) validator = FindObjectOfType<CasValidator>();
            if (strikeSystem == null) strikeSystem = GetComponent<AbstractStrikeSystem>();
        }

        public PhysicalAIObservation Build()
        {
            if (aircraft == null) return new PhysicalAIObservation();

            var obs = new PhysicalAIObservation();
            Rigidbody rb = aircraft.rb;
            CasRequest request = requestManager != null ? requestManager.activeRequest : null;
            GroundUnit target = request != null ? request.target : null;

            obs[0] = SafeDiv(aircraft.Speed, maxSpeed);
            obs[1] = Mathf.Clamp(SafeDiv(aircraft.ForwardSpeed, maxSpeed), -1f, 1f);
            obs[2] = SafeDiv(aircraft.Altitude, maxAltitude);
            obs[3] = rb != null ? Mathf.Clamp(SafeDiv(rb.linearVelocity.y, maxVerticalSpeed), -1f, 1f) : 0f;
            obs[4] = aircraft.throttle;
            obs[5] = aircraft.pitchInput;
            obs[6] = aircraft.rollInput;
            obs[7] = aircraft.yawInput;
            obs[8] = aircraft.StallRisk;
            obs[9] = aircraft.IsStalling ? 1f : 0f;
            obs[10] = aircraft.IsCrashed ? 1f : 0f;
            obs[11] = Mathf.Clamp(Mathf.DeltaAngle(0f, transform.eulerAngles.x) / 90f, -1f, 1f);
            obs[12] = Mathf.Clamp(Mathf.DeltaAngle(0f, transform.eulerAngles.z) / 180f, -1f, 1f);

            bool hasActiveRequest = request != null && request.active && target != null;
            obs[13] = hasActiveRequest ? 1f : 0f;

            if (hasActiveRequest)
            {
                Vector3 toTargetWorld = target.transform.position - transform.position;
                Vector3 localTarget = transform.InverseTransformDirection(toTargetWorld.normalized);
                float distance = toTargetWorld.magnitude;

                obs[14] = Mathf.Clamp01(distance / Mathf.Max(1f, maxTargetDistance));
                obs[15] = Mathf.Clamp(localTarget.x, -1f, 1f);
                obs[16] = Mathf.Clamp(localTarget.y, -1f, 1f);
                obs[17] = Mathf.Clamp(Vector3.Dot(transform.forward, toTargetWorld.normalized), -1f, 1f);
                obs[25] = target.isAlive ? 1f : 0f;
                obs[26] = Mathf.Clamp((transform.position.y - target.transform.position.y) / Mathf.Max(1f, maxAltitude), -1f, 1f);
                obs[27] = Mathf.Clamp01(request.priority);
                obs[30] = localTarget.x >= 0f ? 1f : -1f;
                obs[31] = localTarget.y >= 0f ? 1f : -1f;

                if (validator != null)
                {
                    CasValidationResult result = validator.ValidateStrike(transform, target);
                    obs[18] = result.allowed ? 1f : 0f;
                    obs[19] = Mathf.Clamp01(result.friendlyRisk);
                    obs[20] = Mathf.Clamp01(result.geometryScore);
                    obs[29] = result.allowed ? 0f : 1f;
                    lastValidationReason = result.reason;
                }
            }
            else
            {
                obs[14] = 1f;
                obs[15] = 0f;
                obs[16] = 0f;
                obs[17] = 0f;
                obs[18] = 0f;
                obs[19] = 0f;
                obs[20] = 0f;
                obs[25] = 0f;
                obs[26] = 0f;
                obs[27] = 0f;
                obs[29] = 1f;
                obs[30] = 0f;
                obs[31] = 0f;
                lastValidationReason = "no_active_request";
            }

            if (strikeSystem != null)
            {
                obs[21] = strikeSystem.CanAttemptStrike ? 1f : 0f;
                obs[22] = Mathf.Clamp01(strikeSystem.successfulStrikes / maxStrikeCountForNorm);
                obs[23] = Mathf.Clamp01(strikeSystem.abortedStrikes / maxStrikeCountForNorm);
                obs[24] = Mathf.Clamp01(strikeSystem.friendlyFireIncidents / maxStrikeCountForNorm);
            }

            obs[28] = aircraft.Altitude < lowAltitude ? 1f : 0f;
            obs.ClampAll();
            lastObservation = obs;
            return obs;
        }

        private static float SafeDiv(float value, float divisor)
        {
            if (Mathf.Abs(divisor) < 0.0001f) return 0f;
            return value / divisor;
        }
    }
}
