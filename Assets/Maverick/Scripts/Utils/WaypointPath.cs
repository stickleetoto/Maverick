using System.Collections.Generic;
using UnityEngine;

namespace EaglePhysicalAI.Utils
{
    public class WaypointPath : MonoBehaviour
    {
        public List<Transform> waypoints = new List<Transform>();
        public bool loop = true;
        public float reachDistance = 120f;

        public Transform GetWaypoint(int index)
        {
            if (waypoints == null || waypoints.Count == 0) return null;
            int safeIndex = Mathf.Clamp(index, 0, waypoints.Count - 1);
            return waypoints[safeIndex];
        }

        public int GetNextIndex(int current)
        {
            if (waypoints == null || waypoints.Count == 0) return 0;
            int next = current + 1;
            if (next >= waypoints.Count) return loop ? 0 : waypoints.Count - 1;
            return next;
        }

        private void OnDrawGizmos()
        {
            if (waypoints == null || waypoints.Count == 0) return;
            Gizmos.color = Color.cyan;
            for (int i = 0; i < waypoints.Count; i++)
            {
                if (waypoints[i] == null) continue;
                Gizmos.DrawWireSphere(waypoints[i].position, reachDistance * 0.1f);
                Transform next = waypoints[GetNextIndex(i)];
                if (next != null && (loop || i < waypoints.Count - 1))
                {
                    Gizmos.DrawLine(waypoints[i].position, next.position);
                }
            }
        }
    }
}
