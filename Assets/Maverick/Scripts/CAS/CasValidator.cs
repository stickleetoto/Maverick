using UnityEngine;
using EaglePhysicalAI.Battlefield;
using EaglePhysicalAI.Scenario;

namespace EaglePhysicalAI.CAS
{
    /// <summary>
    /// Game validator for abstract CAS actions. This is not a real targeting model.
    /// It only prevents bad game actions such as striking with no target or near friendlies.
    /// </summary>
    public class CasValidator : MonoBehaviour
    {
        public CasRequestManager requestManager;
        public float minFriendlyDistanceFromTarget = 120f;
        public float maxAircraftDistanceFromTarget = 5500f;
        public float minAircraftAltitude = 80f;
        public float maxOffBoresightAngle = 70f;
        public LayerMask groundUnitMask = ~0;
        public bool respectNoStrikeZones = true;

        public CasValidationResult ValidateStrike(Transform aircraft, GroundUnit targetOverride = null)
        {
            if (aircraft == null) return CasValidationResult.Deny("no_aircraft");

            CasRequest request = requestManager != null ? requestManager.activeRequest : null;
            GroundUnit target = targetOverride != null ? targetOverride : request?.target;

            if (target == null) return CasValidationResult.Deny("no_target_selected");
            if (!target.isAlive) return CasValidationResult.Deny("target_already_inactive");
            if (target.team != GroundTeam.Hostile) return CasValidationResult.Deny("target_not_hostile", 0.1f, 1f, 0f);
            if (aircraft.position.y < minAircraftAltitude) return CasValidationResult.Deny("aircraft_too_low", 0.7f, 0.5f, 0.2f);

            float distance = Vector3.Distance(aircraft.position, target.transform.position);
            if (distance > maxAircraftDistanceFromTarget)
            {
                return CasValidationResult.Deny("target_too_far", 0.55f, 0.2f, 0.1f);
            }

            Vector3 toTarget = (target.transform.position - aircraft.position).normalized;
            float angle = Vector3.Angle(aircraft.forward, toTarget);
            float geometryScore = 1f - Mathf.Clamp01(angle / maxOffBoresightAngle);
            if (angle > maxOffBoresightAngle)
            {
                return CasValidationResult.Deny("target_outside_abstract_attack_cone", 0.65f, 0.2f, geometryScore);
            }

            if (respectNoStrikeZones && IsInsideNoStrikeZone(target.transform.position))
            {
                return CasValidationResult.Deny("inside_no_strike_zone", 0.75f, 1f, geometryScore);
            }

            float friendlyRisk = ComputeFriendlyRisk(target.transform.position);
            if (friendlyRisk >= 0.95f)
            {
                return CasValidationResult.Deny("friendly_too_close", 0.8f, friendlyRisk, geometryScore);
            }

            return CasValidationResult.Allow("strike_allowed_by_game_validator", 0.85f, friendlyRisk, geometryScore);
        }

        public bool IsInsideNoStrikeZone(Vector3 targetPoint)
        {
            var zones = FindObjectsOfType<NoStrikeZone>();
            foreach (var zone in zones)
            {
                if (zone != null && zone.Contains(targetPoint)) return true;
            }
            return false;
        }

        public float ComputeFriendlyRisk(Vector3 targetPoint)
        {
            Collider[] hits = Physics.OverlapSphere(targetPoint, minFriendlyDistanceFromTarget, groundUnitMask);
            float highestRisk = 0f;
            foreach (var hit in hits)
            {
                GroundUnit unit = hit.GetComponentInParent<GroundUnit>();
                if (unit == null || !unit.isAlive) continue;
                if (unit.team == GroundTeam.Friendly || unit.team == GroundTeam.Neutral)
                {
                    float distance = Vector3.Distance(targetPoint, unit.transform.position);
                    float risk = 1f - Mathf.Clamp01(distance / minFriendlyDistanceFromTarget);
                    highestRisk = Mathf.Max(highestRisk, risk);
                }
            }
            return highestRisk;
        }
    }
}
