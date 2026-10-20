using UnityEngine;
using EaglePhysicalAI.Aircraft;

namespace EaglePhysicalAI.FAM
{
    /// <summary>
    /// Conservative heuristic labeler for human flight demonstrations.
    /// Raw controls are always recorded beside the label so downstream FAM tooling
    /// can relabel sessions later without losing the original demonstration.
    /// </summary>
    public class FamBehaviorLabeler : MonoBehaviour
    {
        [Header("Control thresholds")]
        [Range(0.05f, 1f)] public float turnThreshold = 0.35f;
        [Range(0.05f, 1f)] public float pitchThreshold = 0.30f;
        public float throttleDeltaThreshold = 0.04f;

        [Header("Formation thresholds")]
        public float desiredLeaderDistance = 180f;
        public float formationBand = 60f;
        public float rejoinDistance = 420f;
        public float closingRateDeadband = 4f;

        [Header("Safety")]
        [Range(0f, 1f)] public float stabilizeStallRisk = 0.7f;

        public FamBehaviorToken Label(
            AircraftStateSnapshot ego,
            FamControlInput control,
            FamLeaderState leader)
        {
            if (ego.isCrashed || ego.isStalling || ego.stallRisk >= stabilizeStallRisk)
                return FamBehaviorToken.STABILIZE;

            if (leader != null && leader.available)
            {
                if (leader.distance >= rejoinDistance)
                    return FamBehaviorToken.REJOIN;

                float distanceError = leader.distance - desiredLeaderDistance;
                if (distanceError > formationBand && leader.closingRate <= closingRateDeadband)
                    return FamBehaviorToken.CLOSE_DISTANCE;
                if (distanceError < -formationBand && leader.closingRate >= -closingRateDeadband)
                    return FamBehaviorToken.OPEN_DISTANCE;
            }

            if (Mathf.Abs(control.roll) >= turnThreshold)
                return control.roll < 0f ? FamBehaviorToken.TURN_LEFT : FamBehaviorToken.TURN_RIGHT;

            if (Mathf.Abs(control.pitch) >= pitchThreshold)
                return control.pitch > 0f ? FamBehaviorToken.CLIMB : FamBehaviorToken.DESCEND;

            if (control.throttleDelta >= throttleDeltaThreshold)
                return FamBehaviorToken.ACCELERATE;
            if (control.throttleDelta <= -throttleDeltaThreshold)
                return FamBehaviorToken.DECELERATE;

            if (leader != null && leader.available)
            {
                float distanceError = Mathf.Abs(leader.distance - desiredLeaderDistance);
                if (distanceError <= formationBand)
                    return FamBehaviorToken.HOLD_FORMATION;
                return FamBehaviorToken.FOLLOW;
            }

            return FamBehaviorToken.HOLD;
        }
    }
}
