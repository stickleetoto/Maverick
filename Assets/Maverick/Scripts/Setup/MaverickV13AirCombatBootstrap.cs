using UnityEngine;
using EaglePhysicalAI.Controls;
using EaglePhysicalAI.Aircraft;

namespace EaglePhysicalAI.Setup
{
    /// <summary>
    /// Air-combat-only bootstrap.
    /// Put on Maverick_Manager. It installs v1.2 MouseFlight controls, then disables landing/ground systems.
    /// </summary>
    [DefaultExecutionOrder(1300)]
    public class MaverickV13AirCombatBootstrap : MonoBehaviour
    {
        [Header("Required")]
        public GameObject aircraftObject;
        public Camera mainCamera;

        [Header("Install")]
        public bool installV12MouseFlight = true;
        public bool installAirCombatOnlyMode = true;
        public bool disableGroundStartObjects = false;

        [Header("Air Combat Tuning")]
        public float startAltitude = 850f;
        public float startSpeed = 260f;
        public float startThrottle = 1.0f;
        public bool disableWheelColliders = true;
        public bool forceGearVisualUp = true;
        public bool disableGroundColliders = false;

        [Header("Created")]
        public MaverickV12MouseFlightBootstrap v12;
        public MaverickAirCombatOnlyModeV13 airCombatOnly;

        private void Awake()
        {
            Setup();
        }

        [ContextMenu("Setup MAVERICK v1.3 Air Combat Only")]
        public void Setup()
        {
            if (aircraftObject == null)
                aircraftObject = GameObject.Find("F15E_Player");

            if (mainCamera == null)
                mainCamera = Camera.main;

            if (aircraftObject == null)
            {
                Debug.LogWarning("MaverickV13AirCombatBootstrap: aircraftObject missing.");
                return;
            }

            if (installV12MouseFlight)
            {
                v12 = GetOrAdd<MaverickV12MouseFlightBootstrap>(gameObject);
                v12.aircraftObject = aircraftObject;
                v12.mainCamera = mainCamera;
                v12.Setup();
            }

            if (installAirCombatOnlyMode)
            {
                airCombatOnly = GetOrAdd<MaverickAirCombatOnlyModeV13>(gameObject);
                airCombatOnly.aircraftObject = aircraftObject;
                airCombatOnly.mainCamera = mainCamera;
                airCombatOnly.startAltitude = startAltitude;
                airCombatOnly.startSpeed = startSpeed;
                airCombatOnly.startThrottle = startThrottle;
                airCombatOnly.disableWheelColliders = disableWheelColliders;
                airCombatOnly.forceGearVisualUp = forceGearVisualUp;
                airCombatOnly.disableGroundColliders = disableGroundColliders;
                airCombatOnly.ApplyAirCombatOnlyMode();
            }

            if (disableGroundStartObjects)
            {
                DisableObjectIfFound("WheelColliders");
            }
        }

        private void DisableObjectIfFound(string name)
        {
            GameObject go = GameObject.Find(name);
            if (go != null)
                go.SetActive(false);
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T c = target.GetComponent<T>();
            if (c == null) c = target.AddComponent<T>();
            return c;
        }
    }
}
