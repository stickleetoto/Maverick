using UnityEngine;
using EaglePhysicalAI.Aircraft;
using EaglePhysicalAI.Controls;
using EaglePhysicalAI.CAS;
using EaglePhysicalAI.PhysicalAI;

namespace EaglePhysicalAI.Setup
{
    [DefaultExecutionOrder(900)]
    public class MaverickV10Bootstrap : MonoBehaviour
    {
        [Header("Required")]
        public GameObject aircraftObject;
        public Camera mainCamera;

        [Header("Install")]
        public bool installSceneDoctor = true;
        public bool installV10Instructor = true;
        public bool installFlightFeelTuner = true;
        public bool installStableCamera = true;
        public bool installHud = true;
        public bool installWeaponSelector = true;
        public bool installAirStart = true;
        public bool disableOldInputs = true;
        public bool disableOldHud = true;

        [Header("Startup")]
        public MaverickFlightFeelPreset defaultPreset = MaverickFlightFeelPreset.HeavyFighter;
        public bool applyStartupFixes = true;

        public MaverickSceneDoctor sceneDoctor;
        public MaverickInstructorV10 instructor;
        public MaverickAimDirectorV10 aimDirector;
        public MaverickFlightFeelTuner feelTuner;
        public MaverickStableChaseCameraV10 stableCamera;
        public MaverickFlightHudV10 hud;
        public MaverickWeaponSelector weaponSelector;
        public MaverickAirStartV10 airStart;

        private void Awake()
        {
            Setup();
        }

        [ContextMenu("Setup MAVERICK v1.0")]
        public void Setup()
        {
            if (aircraftObject == null) aircraftObject = GameObject.Find("F15E_Player");
            if (mainCamera == null) mainCamera = Camera.main;

            if (aircraftObject == null)
            {
                Debug.LogWarning("MaverickV10Bootstrap: aircraftObject missing.");
                return;
            }

            AircraftPhysicsController aircraft = GetOrAdd<AircraftPhysicsController>(aircraftObject);
            if (aircraft.rb == null) aircraft.rb = aircraftObject.GetComponent<Rigidbody>();

            if (installV10Instructor)
            {
                aimDirector = GetOrAdd<MaverickAimDirectorV10>(aircraftObject);
                aimDirector.aircraftRoot = aircraftObject.transform;
                aimDirector.viewCamera = mainCamera;

                instructor = GetOrAdd<MaverickInstructorV10>(aircraftObject);
                instructor.controller = aircraft;
                instructor.aimDirector = aimDirector;
                instructor.inputEnabled = true;
            }

            if (installFlightFeelTuner)
            {
                feelTuner = GetOrAdd<MaverickFlightFeelTuner>(aircraftObject);
                feelTuner.aircraft = aircraft;
                feelTuner.instructor = instructor;
                feelTuner.ApplyPreset(defaultPreset);
            }

            if (installWeaponSelector)
            {
                weaponSelector = GetOrAdd<MaverickWeaponSelector>(aircraftObject);
                if (weaponSelector.strikeSystem == null)
                    weaponSelector.strikeSystem = aircraftObject.GetComponent<AbstractStrikeSystem>();
            }

            if (installAirStart)
            {
                airStart = GetOrAdd<MaverickAirStartV10>(aircraftObject);
            }

            if (disableOldInputs)
            {
                foreach (var old in aircraftObject.GetComponents<WarThunderMouseAircraftInput>())
                    old.enabled = false;

                foreach (var old in aircraftObject.GetComponents<MaverickInstructor>())
                    old.enabled = false;

                ManualAircraftInput plain = aircraftObject.GetComponent<ManualAircraftInput>();
                if (plain != null && !(plain is MaverickInstructorV10))
                    plain.inputEnabled = false;
            }

            PhysicalAIRuntimeAgent ai = aircraftObject.GetComponent<PhysicalAIRuntimeAgent>();
            if (ai != null && instructor != null)
                ai.manualInput = instructor;

            if (mainCamera != null)
            {
                if (installStableCamera)
                {
                    stableCamera = GetOrAdd<MaverickStableChaseCameraV10>(mainCamera.gameObject);
                    stableCamera.target = aircraftObject.transform;
                }

                if (installHud)
                {
                    hud = GetOrAdd<MaverickFlightHudV10>(mainCamera.gameObject);
                    hud.viewCamera = mainCamera;
                    hud.aim = aimDirector;
                    hud.instructor = instructor;
                    hud.aircraft = aircraft;
                }
            }

            if (disableOldHud)
            {
                foreach (var old in FindObjectsOfType<WarThunderControlHud>(true))
                    old.enabled = false;

                foreach (var old in FindObjectsOfType<MaverickFlightHudMarkers>(true))
                    old.enabled = false;
            }

            if (installSceneDoctor)
            {
                sceneDoctor = GetOrAdd<MaverickSceneDoctor>(gameObject);
                sceneDoctor.aircraftObject = aircraftObject;
                sceneDoctor.mainCamera = mainCamera;
                if (applyStartupFixes)
                    sceneDoctor.RunDiagnosisAndFix();
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
