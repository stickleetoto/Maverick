using UnityEngine;
using EaglePhysicalAI.Aircraft;
using EaglePhysicalAI.CAS;
using EaglePhysicalAI.PhysicalAI;
using EaglePhysicalAI.Controls;

namespace EaglePhysicalAI.Setup
{
    /// <summary>
    /// v0.9 War-Thunder-style control rework bootstrap.
    /// Add this to Maverick_Manager. Assign aircraftObject = F15E_Player and mainCamera = Main Camera.
    /// </summary>
    [DefaultExecutionOrder(250)]
    public class MaverickV09Bootstrap : MonoBehaviour
    {
        [Header("Required")]
        public GameObject aircraftObject;
        public Camera mainCamera;

        [Header("v0.9 Options")]
        public bool installMouseAimDirector = true;
        public bool installInstructor = true;
        public bool installControlModeManager = true;
        public bool installStableCamera = true;
        public bool installRuntimeCameraGuard = true;
        public bool installHudMarkers = true;
        public bool installWeaponSelector = true;
        public bool installAirStart = true;
        public bool disableOldWarThunderInput = true;
        public bool disableOldWarThunderHud = true;

        [Header("Created")]
        public MaverickMouseAimDirector aimDirector;
        public MaverickInstructor instructor;
        public MaverickControlModeManager modeManager;
        public MaverickStableChaseCamera stableCamera;
        public MaverickRuntimeCameraGuard cameraGuard;
        public MaverickFlightHudMarkers hudMarkers;
        public MaverickWeaponSelector weaponSelector;
        public MaverickAirStartConfigurator airStart;

        private void Awake()
        {
            SetupV09();
        }

        [ContextMenu("Setup MAVERICK v0.9")]
        public void SetupV09()
        {
            if (aircraftObject == null) aircraftObject = GameObject.Find("F15E_Player");
            if (mainCamera == null) mainCamera = Camera.main;
            if (aircraftObject == null) { Debug.LogWarning("MaverickV09Bootstrap: aircraftObject missing."); return; }

            AircraftPhysicsController aircraft = GetOrAdd<AircraftPhysicsController>(aircraftObject);
            if (aircraft.rb == null) aircraft.rb = aircraftObject.GetComponent<Rigidbody>();
            aircraft.rollTorqueSign = Mathf.Approximately(aircraft.rollTorqueSign, 0f) ? 1f : aircraft.rollTorqueSign;

            if (disableOldWarThunderInput)
            {
                foreach (var old in aircraftObject.GetComponents<WarThunderMouseAircraftInput>())
                    old.enabled = false;
            }

            if (installMouseAimDirector)
            {
                aimDirector = GetOrAdd<MaverickMouseAimDirector>(aircraftObject);
                aimDirector.aircraftRoot = aircraftObject.transform;
                aimDirector.viewCamera = mainCamera;
            }

            if (installInstructor)
            {
                instructor = GetOrAdd<MaverickInstructor>(aircraftObject);
                instructor.controller = aircraft;
                instructor.aimDirector = aimDirector;
                instructor.inputEnabled = true;
            }

            if (installControlModeManager)
            {
                modeManager = GetOrAdd<MaverickControlModeManager>(aircraftObject);
                modeManager.instructor = instructor;
                PhysicalAIRuntimeAgent physical = aircraftObject.GetComponent<PhysicalAIRuntimeAgent>();
                modeManager.physicalAgent = physical;
                if (physical != null) physical.manualInput = instructor;
            }

            if (installWeaponSelector)
            {
                weaponSelector = GetOrAdd<MaverickWeaponSelector>(aircraftObject);
                if (weaponSelector.strikeSystem == null) weaponSelector.strikeSystem = aircraftObject.GetComponent<AbstractStrikeSystem>();
            }

            if (installAirStart)
            {
                airStart = GetOrAdd<MaverickAirStartConfigurator>(aircraftObject);
            }

            if (mainCamera != null)
            {
                if (installStableCamera)
                {
                    stableCamera = GetOrAdd<MaverickStableChaseCamera>(mainCamera.gameObject);
                    stableCamera.target = aircraftObject.transform;
                    stableCamera.disableOtherScreenCamerasOnStart = true;
                }

                if (installHudMarkers)
                {
                    hudMarkers = GetOrAdd<MaverickFlightHudMarkers>(mainCamera.gameObject);
                    hudMarkers.aimDirector = aimDirector;
                    hudMarkers.instructor = instructor;
                    hudMarkers.aircraft = aircraft;
                    hudMarkers.viewCamera = mainCamera;
                }
            }

            if (installRuntimeCameraGuard)
            {
                cameraGuard = GetOrAdd<MaverickRuntimeCameraGuard>(gameObject);
                cameraGuard.mainCamera = mainCamera;
                cameraGuard.target = aircraftObject.transform;
                cameraGuard.Apply();
            }

            if (disableOldWarThunderHud)
            {
                foreach (var hud in FindObjectsOfType<WarThunderControlHud>(true))
                    hud.enabled = false;
            }

            // Disable pod cameras that render to screen.
            foreach (var cam in FindObjectsOfType<Camera>(true))
            {
                if (cam == null || cam == mainCamera || cam.targetTexture != null) continue;
                string n = cam.name.ToLowerInvariant();
                if (n.Contains("targeting") || n.Contains("pod") || n.Contains("tgp") || n.Contains("chase"))
                    cam.enabled = false;
            }
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T c = target.GetComponent<T>();
            if (c == null) c = target.AddComponent<T>();
            return c;
        }
    }
}
