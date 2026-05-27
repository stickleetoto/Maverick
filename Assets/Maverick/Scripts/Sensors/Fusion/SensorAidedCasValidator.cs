using UnityEngine;
using EaglePhysicalAI.Battlefield;
using EaglePhysicalAI.CAS;

namespace EaglePhysicalAI.Sensors.Fusion
{
    /// <summary>
    /// Optional validator layer that requires a game-level sensor confirmation before abstract strike.
    /// It wraps the existing CAS validator without changing the original class.
    /// </summary>
    public class SensorAidedCasValidator : MonoBehaviour
    {
        public CasValidator baseValidator;
        public SensorFusionManager fusionManager;
        public float requiredFusionConfidence = 0.42f;
        public float friendlyRiskRadius = 180f;
        public float maxSensorFriendlyRisk = 0.45f;

        [Header("Last Result")]
        public bool lastAllowed;
        public string lastReason = "none";
        public float lastFusionConfidence;
        public float lastFriendlyRisk;

        private void Awake()
        {
            if (baseValidator == null) baseValidator = FindObjectOfType<CasValidator>();
            if (fusionManager == null) fusionManager = GetComponent<SensorFusionManager>();
        }

        public CasValidationResult ValidateWithSensors(Transform aircraft, GroundUnit target)
        {
            CasValidationResult result = baseValidator != null
                ? baseValidator.ValidateStrike(aircraft, target)
                : new CasValidationResult { allowed = target != null, reason = target != null ? "no_base_validator" : "no_target" };

            lastFusionConfidence = fusionManager != null ? fusionManager.fusedConfidence : 0f;
            lastFriendlyRisk = fusionManager != null ? fusionManager.GetFriendlyRiskNearFusedTarget(friendlyRiskRadius) : 0f;

            if (fusionManager == null)
            {
                lastAllowed = result.allowed;
                lastReason = result.reason + "+no_sensor_fusion";
                result.reason = lastReason;
                return result;
            }

            if (target == null || fusionManager.fusedBestGroundUnit != target)
            {
                result.allowed = false;
                result.reason = "sensor_fusion_target_mismatch";
            }
            else if (lastFusionConfidence < requiredFusionConfidence)
            {
                result.allowed = false;
                result.reason = "sensor_confirmation_too_low";
            }
            else if (lastFriendlyRisk > maxSensorFriendlyRisk)
            {
                result.allowed = false;
                result.reason = "sensor_friendly_risk_too_high";
            }
            else
            {
                result.reason = result.reason + "+sensor_confirmed";
            }

            lastAllowed = result.allowed;
            lastReason = result.reason;
            return result;
        }
    }
}
