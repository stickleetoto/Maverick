using UnityEngine;
using EaglePhysicalAI.Aircraft;
using EaglePhysicalAI.Battlefield;
using EaglePhysicalAI.CAS;
using EaglePhysicalAI.Scenario;

namespace EaglePhysicalAI.PhysicalAI
{
    /// <summary>
    /// Shaped game reward for physical AI experiments.
    /// This does not train RL by itself; it provides reward signals for logs, analysis, and future trainers.
    /// </summary>
    public class PhysicalAIRewardTracker : MonoBehaviour
    {
        public AircraftPhysicsController aircraft;
        public CasRequestManager requestManager;
        public AbstractStrikeSystem strikeSystem;
        public CasValidator validator;

        [Header("Reward Weights")]
        public float aliveRewardPerSecond = 0.02f;
        public float targetProgressReward = 0.18f;
        public float stableFlightReward = 0.04f;
        public float strikeAllowedPositionReward = 0.06f;
        public float successfulStrikeReward = 3.0f;
        public float abortPenalty = 0.15f;
        public float friendlyFirePenalty = 5.0f;
        public float crashPenalty = 8.0f;
        public float stallPenaltyPerSecond = 0.15f;
        public float lowAltitudePenaltyPerSecond = 0.1f;
        public float lowAltitude = 120f;
        public float threatZonePenaltyPerSecond = 0.12f;

        [Header("Runtime")]
        public float lastReward;
        public float episodeReturn;
        public int episodeSteps;
        public float lastTargetDistance = -1f;
        public string lastRewardBreakdown = "none";

        private int _lastSuccessfulStrikes;
        private int _lastAbortedStrikes;
        private int _lastFriendlyFire;
        private bool _crashPenaltyApplied;

        private void Awake()
        {
            if (aircraft == null) aircraft = GetComponent<AircraftPhysicsController>();
            if (requestManager == null) requestManager = FindObjectOfType<CasRequestManager>();
            if (strikeSystem == null) strikeSystem = GetComponent<AbstractStrikeSystem>();
            if (validator == null) validator = FindObjectOfType<CasValidator>();
        }

        private void FixedUpdate()
        {
            StepReward(Time.fixedDeltaTime);
        }

        public float StepReward(float dt)
        {
            if (aircraft == null) return 0f;

            float reward = 0f;
            string breakdown = "";

            if (!aircraft.IsCrashed)
            {
                float alive = aliveRewardPerSecond * dt;
                reward += alive;
                breakdown += $"alive:{alive:0.000} ";
            }

            if (!aircraft.IsStalling && aircraft.StallRisk < 0.45f && aircraft.Altitude > lowAltitude)
            {
                float stable = stableFlightReward * dt;
                reward += stable;
                breakdown += $"stable:{stable:0.000} ";
            }
            else
            {
                float stallPenalty = aircraft.StallRisk * stallPenaltyPerSecond * dt;
                reward -= stallPenalty;
                breakdown += $"stall_penalty:{stallPenalty:0.000} ";
            }

            if (aircraft.Altitude < lowAltitude)
            {
                float low = lowAltitudePenaltyPerSecond * dt;
                reward -= low;
                breakdown += $"low_alt:{low:0.000} ";
            }

            float threatRisk = GroundThreatZone.HighestRisk(transform.position);
            if (threatRisk > 0f)
            {
                float threatPenalty = threatRisk * threatZonePenaltyPerSecond * dt;
                reward -= threatPenalty;
                breakdown += $"threat:-{threatPenalty:0.000} ";
            }

            CasRequest request = requestManager != null ? requestManager.activeRequest : null;
            if (request != null && request.target != null && request.active)
            {
                float distance = Vector3.Distance(transform.position, request.target.transform.position);
                if (lastTargetDistance >= 0f)
                {
                    float progress = Mathf.Clamp((lastTargetDistance - distance) / 500f, -1f, 1f) * targetProgressReward;
                    reward += progress;
                    breakdown += $"progress:{progress:0.000} ";
                }
                lastTargetDistance = distance;

                if (validator != null)
                {
                    CasValidationResult result = validator.ValidateStrike(transform, request.target);
                    if (result.allowed)
                    {
                        float allowed = strikeAllowedPositionReward * dt;
                        reward += allowed;
                        breakdown += $"valid_window:{allowed:0.000} ";
                    }
                }
            }
            else
            {
                lastTargetDistance = -1f;
            }

            if (strikeSystem != null)
            {
                if (strikeSystem.successfulStrikes > _lastSuccessfulStrikes)
                {
                    int delta = strikeSystem.successfulStrikes - _lastSuccessfulStrikes;
                    reward += delta * successfulStrikeReward;
                    _lastSuccessfulStrikes = strikeSystem.successfulStrikes;
                    breakdown += $"strike:{delta * successfulStrikeReward:0.000} ";
                }

                if (strikeSystem.abortedStrikes > _lastAbortedStrikes)
                {
                    int delta = strikeSystem.abortedStrikes - _lastAbortedStrikes;
                    reward -= delta * abortPenalty;
                    _lastAbortedStrikes = strikeSystem.abortedStrikes;
                    breakdown += $"abort:-{delta * abortPenalty:0.000} ";
                }

                if (strikeSystem.friendlyFireIncidents > _lastFriendlyFire)
                {
                    int delta = strikeSystem.friendlyFireIncidents - _lastFriendlyFire;
                    reward -= delta * friendlyFirePenalty;
                    _lastFriendlyFire = strikeSystem.friendlyFireIncidents;
                    breakdown += $"ff:-{delta * friendlyFirePenalty:0.000} ";
                }
            }

            if (aircraft.IsCrashed && !_crashPenaltyApplied)
            {
                reward -= crashPenalty;
                _crashPenaltyApplied = true;
                breakdown += $"crash:-{crashPenalty:0.000} ";
            }

            lastReward = reward;
            episodeReturn += reward;
            episodeSteps++;
            lastRewardBreakdown = breakdown;
            return reward;
        }

        [ContextMenu("Reset Episode Reward")]
        public void ResetEpisode()
        {
            lastReward = 0f;
            episodeReturn = 0f;
            episodeSteps = 0;
            lastTargetDistance = -1f;
            lastRewardBreakdown = "reset";
            _crashPenaltyApplied = false;
            if (strikeSystem != null)
            {
                _lastSuccessfulStrikes = strikeSystem.successfulStrikes;
                _lastAbortedStrikes = strikeSystem.abortedStrikes;
                _lastFriendlyFire = strikeSystem.friendlyFireIncidents;
            }
        }
    }
}
