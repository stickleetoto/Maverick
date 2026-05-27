using UnityEngine;
using EaglePhysicalAI.Aircraft;

namespace EaglePhysicalAI.Scenario
{
    /// <summary>
    /// Abstract danger volume for mission design and reward shaping. It does not model real air defenses;
    /// it is a game/AI-training hazard zone used to teach routing and abort decisions.
    /// </summary>
    public class GroundThreatZone : MonoBehaviour
    {
        public string zoneId = "threat_zone";
        public float radius = 450f;
        public float minimumAltitudeAffected = 0f;
        public float maximumAltitudeAffected = 1200f;
        [Range(0f, 1f)] public float danger = 0.65f;
        public bool active = true;
        public Color gizmoColor = new Color(1f, 0.55f, 0f, 0.18f);

        public float EvaluateRisk(Vector3 aircraftPosition)
        {
            if (!active) return 0f;
            if (aircraftPosition.y < minimumAltitudeAffected || aircraftPosition.y > maximumAltitudeAffected) return 0f;
            float distance = Vector3.Distance(new Vector3(transform.position.x, 0f, transform.position.z), new Vector3(aircraftPosition.x, 0f, aircraftPosition.z));
            if (distance > radius) return 0f;
            return (1f - Mathf.Clamp01(distance / Mathf.Max(1f, radius))) * danger;
        }

        public static float HighestRisk(Vector3 aircraftPosition)
        {
            float risk = 0f;
            var zones = FindObjectsOfType<GroundThreatZone>();
            foreach (var zone in zones)
            {
                if (zone == null) continue;
                risk = Mathf.Max(risk, zone.EvaluateRisk(aircraftPosition));
            }
            return risk;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = gizmoColor;
            Gizmos.DrawSphere(transform.position, radius);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
