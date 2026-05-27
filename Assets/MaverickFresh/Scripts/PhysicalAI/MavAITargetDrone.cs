using UnityEngine;

namespace MaverickFresh
{
    /// <summary>
    /// Simple physical/non-physical target drone for AI testing.
    /// Creates a moving target so the Physical AI has something to chase.
    /// </summary>
    [DisallowMultipleComponent]
    public class MavAITargetDrone : MonoBehaviour
    {
        public enum DronePattern
        {
            Circle,
            FigureEight,
            StraightLine
        }

        public DronePattern pattern = DronePattern.Circle;
        public Vector3 center = new Vector3(0f, 900f, 2400f);
        public float radius = 850f;
        public float speedDeg = 18f;
        public float verticalWave = 90f;
        public float straightSpeed = 180f;
        public bool addRigidbodyVelocity = true;
        public Vector3 currentVelocity;

        private float t;
        private Rigidbody rb;
        private Vector3 lastPos;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null)
                rb = gameObject.AddComponent<Rigidbody>();

            rb.useGravity = false;
            rb.linearDamping = 0f;
            rb.angularDamping = 0.2f;
            rb.isKinematic = true;

            lastPos = transform.position;
        }

        private void Start()
        {
            if (transform.position == Vector3.zero)
                transform.position = center + new Vector3(radius, 0f, 0f);
        }

        private void Update()
        {
            t += Time.deltaTime;

            Vector3 pos = transform.position;

            if (pattern == DronePattern.Circle)
            {
                float a = t * speedDeg * Mathf.Deg2Rad;
                pos = center + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a * 0.7f) * verticalWave, Mathf.Sin(a) * radius);
            }
            else if (pattern == DronePattern.FigureEight)
            {
                float a = t * speedDeg * Mathf.Deg2Rad;
                pos = center + new Vector3(Mathf.Sin(a) * radius, Mathf.Sin(a * 0.8f) * verticalWave, Mathf.Sin(a * 2f) * radius * 0.55f);
            }
            else
            {
                pos = center + transform.forward * Mathf.Sin(t * 0.2f) * radius + Vector3.up * Mathf.Sin(t * 0.5f) * verticalWave;
            }

            Vector3 velocity = (pos - lastPos) / Mathf.Max(Time.deltaTime, 0.0001f);
            currentVelocity = velocity;

            transform.position = pos;
            if (velocity.sqrMagnitude > 1f)
                transform.rotation = Quaternion.LookRotation(velocity.normalized, Vector3.up);

            if (rb != null && addRigidbodyVelocity)
            {
                rb.position = pos;
                rb.rotation = transform.rotation;
                rb.linearVelocity = velocity;
            }

            lastPos = pos;
        }
    }
}
