using UnityEngine;
using EaglePhysicalAI.Aircraft;

namespace EaglePhysicalAI.Setup
{
    [DefaultExecutionOrder(500)]
    public class MaverickAirStartConfigurator : MonoBehaviour
    {
        public bool applyOnStart = true;
        public bool applyOnlyOnce = true;

        [Header("Air Start")]
        public float startAltitude = 220f;
        public float startSpeed = 190f;
        public float startThrottle = 0.82f;
        public bool keepCurrentXZ = true;
        public Vector3 explicitStartPosition = new Vector3(0f, 220f, -250f);
        public Vector3 startEulerAngles = new Vector3(0f, 0f, 0f);

        [Header("Gear")]
        public bool hideVisualLandingGear = true;
        public bool disableWheelCollidersInAir = true;

        private bool applied;

        private void Start()
        {
            if (applyOnStart) ApplyAirStart();
        }

        [ContextMenu("Apply Air Start Now")]
        public void ApplyAirStart()
        {
            if (applyOnlyOnce && applied) return;
            applied = true;

            Vector3 pos = keepCurrentXZ ? transform.position : explicitStartPosition;
            pos.y = startAltitude;
            transform.position = pos;
            transform.rotation = Quaternion.Euler(startEulerAngles);

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = transform.forward * startSpeed;
                rb.angularVelocity = Vector3.zero;
                rb.WakeUp();
            }

            AircraftPhysicsController aircraft = GetComponent<AircraftPhysicsController>();
            if (aircraft != null)
            {
                aircraft.targetThrottle = Mathf.Clamp01(startThrottle);
                aircraft.throttle = Mathf.Clamp01(startThrottle);
                aircraft.SetCrashed(false);
            }

            SimpleLandingGearController simpleGear = GetComponent<SimpleLandingGearController>();
            if (simpleGear != null && hideVisualLandingGear)
                simpleGear.SetGearDown(false);

            if (disableWheelCollidersInAir)
            {
                foreach (WheelCollider wheel in GetComponentsInChildren<WheelCollider>(true))
                    wheel.enabled = false;
            }
        }
    }
}
