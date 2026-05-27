using System.Collections.Generic;
using UnityEngine;

namespace MaverickFresh
{
    /// <summary>
    /// Lightweight visual trajectory recorder.
    /// It draws a trail of sampled positions so you can inspect flight paths without training anything yet.
    /// </summary>
    [DisallowMultipleComponent]
    public class MavGhostTrailRecorder : MonoBehaviour
    {
        public bool recordTrail = true;
        public float sampleInterval = 0.25f;
        public int maxPoints = 800;
        public Color trailColor = Color.cyan;
        public float sphereRadius = 4f;

        private readonly List<Vector3> points = new List<Vector3>();
        private float nextSample;

        private void Update()
        {
            if (!recordTrail)
                return;

            if (Time.time >= nextSample)
            {
                points.Add(transform.position);
                while (points.Count > maxPoints)
                    points.RemoveAt(0);

                nextSample = Time.time + sampleInterval;
            }
        }

        [ContextMenu("Clear Trail")]
        public void ClearTrail()
        {
            points.Clear();
        }

        private void OnDrawGizmos()
        {
            if (points == null || points.Count == 0)
                return;

            Color old = Gizmos.color;
            Gizmos.color = trailColor;

            for (int i = 0; i < points.Count; i++)
            {
                Gizmos.DrawSphere(points[i], sphereRadius);

                if (i > 0)
                    Gizmos.DrawLine(points[i - 1], points[i]);
            }

            Gizmos.color = old;
        }
    }
}
