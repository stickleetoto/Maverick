using UnityEngine;

namespace EaglePhysicalAI.Mission
{
    public class MissionWaypoint : MonoBehaviour
    {
        public string waypointId = "waypoint";
        public float radius = 220f;
        public bool reached;
        public Color gizmoColor = new Color(0.2f, 0.8f, 1f, 0.2f);

        public bool IsReachedBy(Vector3 position)
        {
            reached = Vector3.Distance(position, transform.position) <= radius;
            return reached;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = gizmoColor;
            Gizmos.DrawSphere(transform.position, radius);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
