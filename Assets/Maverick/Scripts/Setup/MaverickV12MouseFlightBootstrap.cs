using UnityEngine;
using EaglePhysicalAI.Aircraft;
using EaglePhysicalAI.Controls;
using EaglePhysicalAI.UI;
using EaglePhysicalAI.PhysicalAI;

namespace EaglePhysicalAI.Setup
{
    /// <summary>
    /// v1.2 MouseFlight-core bootstrap.
    /// Add this to Maverick_Manager. Assign aircraftObject and mainCamera.
    /// It disables older control/camera scripts and installs MouseFlight-style rig + instructor.
    /// </summary>
    [DefaultExecutionOrder(1200)]
    public class MaverickV12MouseFlightBootstrap : MonoBehaviour
    {
        [Header("Required")]
        public GameObject aircraftObject;
        public Camera mainCamera;

        [Header("Install")]
        public bool installV11Systems = true;
        public bool installMouseFlightRig = true;
        public bool installMouseFlightInstructor = true;
        public bool installMouseFlightHud = true;
        public bool disableOlderInputStacks = true;
        public bool disableOlderHud = true;
        public bool disableBadCameras = true;

        [Header("Created")]
        public MaverickV11WarThunderBootstrap v11;
        public MaverickMouseFlightRigV12 rig;
        public MaverickMouseFlightInstructorV12 instructor;
        public MaverickMouseFlightHudV12 hud;

        private void Awake()
        {
            Setup();
        }

        [ContextMenu("Setup MAVERICK v1.2 MouseFlight Core")]
        public void Setup()
        {
            if (aircraftObject == null) aircraftObject = GameObject.Find("F15E_Player");
            if (mainCamera == null) mainCamera = Camera.main;

            if (aircraftObject == null)
            {
                Debug.LogWarning("MaverickV12MouseFlightBootstrap: aircraftObject missing.");
                return;
            }

            if (installV11Systems)
            {
                v11 = GetOrAdd<MaverickV11WarThunderBootstrap>(gameObject);
                v11.aircraftObject = aircraftObject;
                v11.mainCamera = mainCamera;
                v11.Setup();
            }

            if (disableOlderInputStacks)
                DisableOldInputStacks();

            AircraftPhysicsController aircraft = GetOrAdd<AircraftPhysicsController>(aircraftObject);
            MaverickWTKeybindProfileV11 keys = GetOrAdd<MaverickWTKeybindProfileV11>(aircraftObject);

            if (installMouseFlightRig)
            {
                rig = GetOrAdd<MaverickMouseFlightRigV12>(aircraftObject);
                rig.aircraft = aircraftObject.transform;
                rig.viewCamera = mainCamera;
            }

            if (installMouseFlightInstructor)
            {
                instructor = GetOrAdd<MaverickMouseFlightInstructorV12>(aircraftObject);
                instructor.controller = aircraft;
                instructor.rig = rig;
                instructor.keys = keys;
                instructor.inputEnabled = true;
            }

            PhysicalAIRuntimeAgent ai = aircraftObject.GetComponent<PhysicalAIRuntimeAgent>();
            if (ai != null && instructor != null)
                ai.manualInput = instructor;

            if (mainCamera != null && installMouseFlightHud)
            {
                hud = GetOrAdd<MaverickMouseFlightHudV12>(mainCamera.gameObject);
                hud.viewCamera = mainCamera;
                hud.rig = rig;
                hud.instructor = instructor;
                hud.aircraft = aircraft;
                hud.keys = keys;
            }

            if (disableOlderHud)
                DisableOldHud();

            if (disableBadCameras)
                DisableNonMainScreenCameras();

            if (rig != null)
                rig.CenterAim();
        }

        private void DisableOldInputStacks()
        {
            foreach (var c in aircraftObject.GetComponents<WarThunderMouseAircraftInput>())
                c.enabled = false;

            foreach (var c in aircraftObject.GetComponents<MaverickInstructor>())
                c.enabled = false;

            foreach (var c in aircraftObject.GetComponents<MaverickInstructorV10>())
                c.enabled = false;

            ManualAircraftInput plain = aircraftObject.GetComponent<ManualAircraftInput>();
            if (plain != null && !(plain is MaverickMouseFlightInstructorV12))
                plain.inputEnabled = false;
        }

        private void DisableOldHud()
        {
            foreach (var h in FindObjectsOfType<MaverickWTHudV11>(true))
                h.visible = false;

            foreach (var h in FindObjectsOfType<MaverickFlightHudV10>(true))
                h.visible = false;

            foreach (var h in FindObjectsOfType<MaverickFlightHudMarkers>(true))
                h.showHud = false;

            foreach (var h in FindObjectsOfType<WarThunderControlHud>(true))
                h.enabled = false;
        }

        private void DisableNonMainScreenCameras()
        {
            foreach (Camera cam in FindObjectsOfType<Camera>(true))
            {
                if (cam == null || cam == mainCamera) continue;
                if (cam.targetTexture != null) continue;

                string n = cam.name.ToLowerInvariant();
                if (n.Contains("targeting") || n.Contains("pod") || n.Contains("tgp") || n.Contains("chase"))
                    cam.enabled = false;
            }

            if (mainCamera != null)
            {
                mainCamera.enabled = true;
                mainCamera.depth = 200;
            }
        }

        private static T GetOrAdd<T>(GameObject obj) where T : Component
        {
            T c = obj.GetComponent<T>();
            if (c == null) c = obj.AddComponent<T>();
            return c;
        }
    }
}
