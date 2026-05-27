using UnityEngine;

namespace EaglePhysicalAI.Scenario
{
    /// <summary>
    /// Simple editor-visible volume for game CAS safety. If a target point is inside this radius,
    /// CAS validators can reject abstract strikes. Use for civilian areas, friendly bases, or tutorial no-fire areas.
    /// </summary>
    public class NoStrikeZone : MonoBehaviour
    {
        public string zoneId = "no_strike_zone";
        public float radius = 150f;
        public bool active = true;
        public Color gizmoColor = new Color(1f, 0.2f, 0.1f, 0.22f);

        public bool Contains(Vector3 point)
        {
            if (!active) return false;
            return Vector3.Distance(transform.position, point) <= radius;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = gizmoColor;
            Gizmos.DrawSphere(transform.position, radius);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
