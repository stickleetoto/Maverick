using UnityEngine;

namespace EaglePhysicalAI.Scenario
{
    /// <summary>
    /// Simple airborne drone target for flight/combat-feel testing.
    /// Not a real AI aircraft yet; it is a moving visual/sensor target.
    /// </summary>
    public class MaverickAirTargetDroneV13 : MonoBehaviour
    {
        public Transform orbitCenter;
        public float orbitRadius = 450f;
        public float orbitSpeedDeg = 12f;
        public float altitude = 900f;
        public float bobAmplitude = 40f;
        public float bobFrequency = 0.25f;
        public bool faceVelocity = true;

        private float angle;
        private Vector3 lastPos;

        private void Start()
        {
            if (orbitCenter == null)
            {
                GameObject center = GameObject.Find("AirCombat_Center");
                if (center == null) center = new GameObject("AirCombat_Center");
                orbitCenter = center.transform;
            }

            lastPos = transform.position;
        }

        private void Update()
        {
            if (orbitCenter == null) return;

            angle += orbitSpeedDeg * Time.deltaTime;
            float rad = angle * Mathf.Deg2Rad;

            Vector3 pos = orbitCenter.position;
            pos.x += Mathf.Cos(rad) * orbitRadius;
            pos.z += Mathf.Sin(rad) * orbitRadius;
            pos.y = altitude + Mathf.Sin(Time.time * bobFrequency) * bobAmplitude;

            transform.position = pos;

            Vector3 vel = transform.position - lastPos;
            if (faceVelocity && vel.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(vel.normalized, Vector3.up);

            lastPos = transform.position;
        }
    }
}
