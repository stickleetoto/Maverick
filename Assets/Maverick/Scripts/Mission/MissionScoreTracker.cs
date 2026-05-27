using UnityEngine;
using EaglePhysicalAI.Aircraft;
using EaglePhysicalAI.CAS;

namespace EaglePhysicalAI.Mission
{
    public class MissionScoreTracker : MonoBehaviour
    {
        public AircraftPhysicsController aircraft;
        public AbstractStrikeSystem strikeSystem;

        [Header("Score")]
        public float score;
        public float survivalPointsPerSecond = 0.1f;
        public float successfulStrikePoints = 100f;
        public float abortedStrikePenalty = 5f;
        public float friendlyFirePenalty = 250f;
        public float crashPenalty = 500f;

        private int _lastSuccessfulStrikes;
        private int _lastAbortedStrikes;
        private int _lastFriendlyFire;
        private bool _crashPenaltyApplied;

        private void Awake()
        {
            if (aircraft == null) aircraft = FindObjectOfType<AircraftPhysicsController>();
            if (strikeSystem == null) strikeSystem = FindObjectOfType<AbstractStrikeSystem>();
        }

        private void Update()
        {
            if (aircraft != null && !aircraft.IsCrashed)
            {
                score += survivalPointsPerSecond * Time.deltaTime;
            }

            if (strikeSystem != null)
            {
                if (strikeSystem.successfulStrikes > _lastSuccessfulStrikes)
                {
                    score += (strikeSystem.successfulStrikes - _lastSuccessfulStrikes) * successfulStrikePoints;
                    _lastSuccessfulStrikes = strikeSystem.successfulStrikes;
                }

                if (strikeSystem.abortedStrikes > _lastAbortedStrikes)
                {
                    score -= (strikeSystem.abortedStrikes - _lastAbortedStrikes) * abortedStrikePenalty;
                    _lastAbortedStrikes = strikeSystem.abortedStrikes;
                }

                if (strikeSystem.friendlyFireIncidents > _lastFriendlyFire)
                {
                    score -= (strikeSystem.friendlyFireIncidents - _lastFriendlyFire) * friendlyFirePenalty;
                    _lastFriendlyFire = strikeSystem.friendlyFireIncidents;
                }
            }

            if (aircraft != null && aircraft.IsCrashed && !_crashPenaltyApplied)
            {
                score -= crashPenalty;
                _crashPenaltyApplied = true;
            }
        }
    }
}
