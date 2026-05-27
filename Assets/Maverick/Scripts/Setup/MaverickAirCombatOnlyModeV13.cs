using UnityEngine;
using EaglePhysicalAI.Aircraft;
using EaglePhysicalAI.Controls;

namespace EaglePhysicalAI.Setup
{
    /// <summary>
    /// Air-combat-only mode for Project MAVERICK.
    /// Disables landing/taxi/wheel systems and starts the aircraft already airborne.
    /// Use this while tuning MouseFlight/War-Thunder-like flight feel.
    /// </summary>
    [DisallowMultipleComponent]
    public class MaverickAirCombatOnlyModeV13 : MonoBehaviour
    {
        [Header("References")]
        public GameObject aircraftObject;
        public Camera mainCamera;

        [Header("Startup Air State")]
        public bool applyOnStart = true;
        public float startAltitude = 850f;
        public float startSpeed = 260f;
        public float startThrottle = 1.0f;
        public Vector3 startEulerAngles = new Vector3(0f, 0f, 0f);
        public bool keepCurrentXZ = true;
        public Vector3 explicitStartPosition = new Vector3(0f, 850f, -500f);

        [Header("Disable Landing/Ground Systems")]
        public bool disableWheelColliders = true;
        public bool disableLandingGearScripts = true;
        public bool forceGearVisualUp = true;
        public bool disableGroundColliders = false;
        public string[] groundObjectNames = new string[] { "Ground_TestRange", "Runway_01" };

        [Header("Air Safety")]
        public bool enforceMinimumAltitude = true;
        public float minimumAltitude = 120f;
        public float recoveryAltitude = 600f;
        public float recoverySpeed = 240f;
        public bool recoverIfTooSlow = true;
        public float minimumSpeed = 85f;

        [Header("Camera")]
        public bool ensureMouseFlightCamera = true;
        public bool disableNonMainScreenCameras = true;

        [Header("Debug")]
        public string lastEvent = "ready";
        public bool isAirCombatOnlyActive;

        private Rigidbody rb;
        private AircraftPhysicsController aircraft;

        private void Awake()
        {
            ResolveReferences();
        }

        private void Start()
        {
            if (applyOnStart)
                ApplyAirCombatOnlyMode();
        }

        private void FixedUpdate()
        {
            if (!isAirCombatOnlyActive || aircraftObject == null)
                return;

            if (enforceMinimumAltitude && aircraftObject.transform.position.y < minimumAltitude)
            {
                RecoverAircraft("altitude_floor");
            }

            if (recoverIfTooSlow && rb != null && rb.linearVelocity.magnitude < minimumSpeed && aircraftObject.transform.position.y < recoveryAltitude)
            {
                RecoverAircraft("low_speed_recovery");
            }
        }

        [ContextMenu("Apply Air Combat Only Mode")]
        public void ApplyAirCombatOnlyMode()
        {
            ResolveReferences();

            if (aircraftObject == null)
            {
                Debug.LogWarning("MaverickAirCombatOnlyModeV13: aircraftObject missing.");
                return;
            }

            DisableLandingSystems();
            ApplyAirStart();
            SetupCamera();

            isAirCombatOnlyActive = true;
            lastEvent = "air_combat_only_applied";
        }

        [ContextMenu("Recover Aircraft Now")]
        public void RecoverNow()
        {
            RecoverAircraft("manual_recover");
        }

        private void ResolveReferences()
        {
            if (aircraftObject == null)
                aircraftObject = GameObject.Find("F15E_Player");

            if (mainCamera == null)
                mainCamera = Camera.main;

            if (aircraftObject != null)
            {
                rb = aircraftObject.GetComponent<Rigidbody>();
                aircraft = aircraftObject.GetComponent<AircraftPhysicsController>();
            }
        }

        private void DisableLandingSystems()
        {
            if (aircraftObject == null)
                return;

            if (disableWheelColliders)
            {
                foreach (WheelCollider wheel in aircraftObject.GetComponentsInChildren<WheelCollider>(true))
                    wheel.enabled = false;
            }

            if (disableLandingGearScripts)
            {
                foreach (var gear in aircraftObject.GetComponents<MaverickLandingGearPhysics>())
                    gear.enabled = false;

                foreach (var wheelFollow in aircraftObject.GetComponentsInChildren<MaverickWheelVisualFollower>(true))
                    wheelFollow.enabled = false;
            }

            if (forceGearVisualUp)
            {
                SimpleLandingGearController simpleGear = aircraftObject.GetComponent<SimpleLandingGearController>();
                if (simpleGear != null)
                    simpleGear.SetGearDown(false);

                // Heuristic: hide objects that look like gear-down visuals.
                foreach (Transform t in aircraftObject.GetComponentsInChildren<Transform>(true))
                {
                    string n = t.name.ToLowerInvariant();
                    if (n.Contains("landingon") || n.Contains("landing_on") || n.Contains("gear_down"))
                        t.gameObject.SetActive(false);
                    if (n.Contains("landingoff") || n.Contains("landing_off") || n.Contains("gear_up"))
                        t.gameObject.SetActive(true);
                }
            }

            if (disableGroundColliders && groundObjectNames != null)
            {
                foreach (string objName in groundObjectNames)
                {
                    GameObject go = GameObject.Find(objName);
                    if (go == null) continue;
                    foreach (Collider col in go.GetComponentsInChildren<Collider>(true))
                        col.enabled = false;
                }
            }
        }

        private void ApplyAirStart()
        {
            if (aircraftObject == null)
                return;

            Vector3 pos = keepCurrentXZ ? aircraftObject.transform.position : explicitStartPosition;
            pos.y = startAltitude;
            aircraftObject.transform.position = pos;
            aircraftObject.transform.rotation = Quaternion.Euler(startEulerAngles);

            if (rb == null)
                rb = aircraftObject.GetComponent<Rigidbody>();

            if (rb != null)
            {
                rb.linearVelocity = aircraftObject.transform.forward * startSpeed;
                rb.angularVelocity = Vector3.zero;
                rb.WakeUp();
            }

            if (aircraft == null)
                aircraft = aircraftObject.GetComponent<AircraftPhysicsController>();

            if (aircraft != null)
            {
                aircraft.targetThrottle = Mathf.Clamp01(startThrottle);
                aircraft.throttle = Mathf.Clamp01(startThrottle);
                aircraft.SetCrashed(false);
            }
        }

        private void SetupCamera()
        {
            if (!ensureMouseFlightCamera || mainCamera == null || aircraftObject == null)
                return;

            MaverickMouseFlightRigV12 rig = aircraftObject.GetComponent<MaverickMouseFlightRigV12>();
            if (rig != null)
            {
                rig.aircraft = aircraftObject.transform;
                rig.viewCamera = mainCamera;
                rig.CenterAim();
            }

            if (disableNonMainScreenCameras)
            {
                foreach (Camera cam in FindObjectsOfType<Camera>(true))
                {
                    if (cam == null || cam == mainCamera) continue;
                    if (cam.targetTexture != null) continue;

                    string n = cam.name.ToLowerInvariant();
                    if (n.Contains("targeting") || n.Contains("pod") || n.Contains("tgp") || n.Contains("chase"))
                        cam.enabled = false;
                }

                mainCamera.enabled = true;
                mainCamera.depth = 250;
            }
        }

        private void RecoverAircraft(string reason)
        {
            if (aircraftObject == null)
                return;

            Vector3 pos = aircraftObject.transform.position;
            pos.y = Mathf.Max(recoveryAltitude, minimumAltitude + 200f);
            aircraftObject.transform.position = pos;

            aircraftObject.transform.rotation = Quaternion.Euler(startEulerAngles);

            if (rb == null)
                rb = aircraftObject.GetComponent<Rigidbody>();

            if (rb != null)
            {
                rb.linearVelocity = aircraftObject.transform.forward * recoverySpeed;
                rb.angularVelocity = Vector3.zero;
            }

            if (aircraft == null)
                aircraft = aircraftObject.GetComponent<AircraftPhysicsController>();

            if (aircraft != null)
            {
                aircraft.SetCrashed(false);
                aircraft.targetThrottle = Mathf.Clamp01(startThrottle);
                aircraft.throttle = Mathf.Clamp01(startThrottle);
            }

            lastEvent = reason;
        }
    }
}
