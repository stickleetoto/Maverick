using UnityEngine;
using EaglePhysicalAI.Aircraft;

namespace EaglePhysicalAI.Setup
{
    public class MaverickAirStartV10 : MonoBehaviour
    {
        public bool applyOnStart = true;
        public bool applyOnlyOnce = true;
        public float altitude = 260f;
        public float speed = 205f;
        public float throttle = 0.84f;
        public Vector3 startEulerAngles;
        public bool keepCurrentXZ = true;
        public Vector3 explicitPosition = new Vector3(0f, 260f, -300f);
        public bool disableWheelColliders = true;
        public bool setGearUpVisual = true;

        private bool applied;

        private void Start()
        {
            if (applyOnStart) Apply();
        }

        [ContextMenu("Apply Air Start")]
        public void Apply()
        {
            if (applyOnlyOnce && applied) return;
            applied = true;

            Vector3 pos = keepCurrentXZ ? transform.position : explicitPosition;
            pos.y = altitude;
            transform.position = pos;
            transform.rotation = Quaternion.Euler(startEulerAngles);

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = transform.forward * speed;
                rb.angularVelocity = Vector3.zero;
                rb.WakeUp();
            }

            AircraftPhysicsController aircraft = GetComponent<AircraftPhysicsController>();
            if (aircraft != null)
            {
                aircraft.targetThrottle = Mathf.Clamp01(throttle);
                aircraft.throttle = Mathf.Clamp01(throttle);
                aircraft.SetCrashed(false);
            }

            if (disableWheelColliders)
            {
                foreach (WheelCollider wc in GetComponentsInChildren<WheelCollider>(true))
                    wc.enabled = false;
            }

            if (setGearUpVisual)
            {
                SimpleLandingGearController gear = GetComponent<SimpleLandingGearController>();
                if (gear != null) gear.SetGearDown(false);
            }
        }
    }
}
