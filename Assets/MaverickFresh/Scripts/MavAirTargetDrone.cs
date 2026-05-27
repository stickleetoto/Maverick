using UnityEngine;

namespace MaverickFresh
{
    public class MavAirTargetDrone : MonoBehaviour
    {
        public Transform orbitCenter;
        public float orbitRadius = 450f;
        public float orbitSpeedDeg = 15f;
        public float altitude = 850f;
        public float bobAmplitude = 25f;
        public float bobFrequency = 0.4f;
        public bool faceVelocity = true;

        private float angle;
        private Vector3 lastPosition;

        private void Start()
        {
            if (orbitCenter == null)
            {
                GameObject center = GameObject.Find("MavFresh_AirCombatCenter");
                if (center == null)
                {
                    center = new GameObject("MavFresh_AirCombatCenter");
                    center.transform.position = new Vector3(0f, altitude, 600f);
                }

                orbitCenter = center.transform;
            }

            lastPosition = transform.position;
        }

        private void Update()
        {
            if (orbitCenter == null)
                return;

            angle += orbitSpeedDeg * Time.deltaTime;
            float r = angle * Mathf.Deg2Rad;

            Vector3 pos = orbitCenter.position;
            pos.x += Mathf.Cos(r) * orbitRadius;
            pos.z += Mathf.Sin(r) * orbitRadius;
            pos.y = altitude + Mathf.Sin(Time.time * bobFrequency) * bobAmplitude;

            transform.position = pos;

            Vector3 velocity = transform.position - lastPosition;
            if (faceVelocity && velocity.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(velocity.normalized, Vector3.up);

            lastPosition = transform.position;
        }
    }
}
