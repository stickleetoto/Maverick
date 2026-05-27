using UnityEngine;
using EaglePhysicalAI.Aircraft;
using EaglePhysicalAI.Battlefield;
using EaglePhysicalAI.CAS;

namespace EaglePhysicalAI.PhysicalAI
{
    /// <summary>
    /// Safety clamp for AI actions. This is a game safety layer, not a real-world flight law.
    /// It prevents obviously bad learned outputs from immediately destroying the test aircraft.
    /// </summary>
    public class PhysicalActionValidator : MonoBehaviour
    {
        public AircraftPhysicsController aircraft;
        public CasRequestManager requestManager;
        public CasValidator casValidator;

        [Header("Flight Limits")]
        public float hardDeckAltitude = 80f;
        public float stallRiskThrottleOverride = 0.68f;
        public float stallRiskMaxPitchUp = 0.05f;
        public float lowAltitudeMaxPitchDown = -0.05f;
        public float maxRollAtLowAltitude = 0.45f;
        public float lowAltitudeRollClampHeight = 160f;

        [Header("Strike Gate")]
        public bool requireCasValidatorForStrike = true;
        public float strikeThreshold = 0.65f;

        public string lastSafetyNote = "none";

        private void Awake()
        {
            if (aircraft == null) aircraft = GetComponent<AircraftPhysicsController>();
            if (requestManager == null) requestManager = FindObjectOfType<CasRequestManager>();
            if (casValidator == null) casValidator = FindObjectOfType<CasValidator>();
        }

        public PhysicalAIAction Validate(PhysicalAIAction action)
        {
            if (action == null) action = new PhysicalAIAction();
            action.Clamp();
            lastSafetyNote = "ok";

            if (aircraft == null) return action;

            if (aircraft.IsCrashed)
            {
                action.pitch = 0f;
                action.roll = 0f;
                action.yaw = 0f;
                action.throttle = 0f;
                action.strike = 0f;
                lastSafetyNote = "crashed_lockout";
                return action;
            }

            if (aircraft.StallRisk >= stallRiskThrottleOverride)
            {
                action.throttle = 1f;
                action.pitch = Mathf.Min(action.pitch, stallRiskMaxPitchUp);
                lastSafetyNote = "stall_recovery_override";
            }

            if (aircraft.Altitude <= hardDeckAltitude)
            {
                action.pitch = Mathf.Max(action.pitch, lowAltitudeMaxPitchDown);
                action.throttle = Mathf.Max(action.throttle, 0.75f);
                action.strike = 0f;
                lastSafetyNote = "hard_deck_recovery";
            }

            if (aircraft.Altitude <= lowAltitudeRollClampHeight)
            {
                action.roll = Mathf.Clamp(action.roll, -maxRollAtLowAltitude, maxRollAtLowAltitude);
            }

            if (requireCasValidatorForStrike && action.strike >= strikeThreshold)
            {
                CasRequest request = requestManager != null ? requestManager.activeRequest : null;
                GroundUnit target = request != null ? request.target : null;
                CasValidationResult result = casValidator != null
                    ? casValidator.ValidateStrike(transform, target)
                    : CasValidationResult.Deny("no_cas_validator");

                if (!result.allowed)
                {
                    action.strike = 0f;
                    action.abort = 1f;
                    lastSafetyNote = "strike_blocked_" + result.reason;
                }
            }

            return action.Clamp();
        }
    }
}
