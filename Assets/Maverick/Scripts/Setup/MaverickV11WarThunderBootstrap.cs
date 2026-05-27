using UnityEngine;
using EaglePhysicalAI.Aircraft;
using EaglePhysicalAI.CAS;
using EaglePhysicalAI.Controls;
using EaglePhysicalAI.Sensors.Radar;
using EaglePhysicalAI.Sensors.TargetingPod;
using EaglePhysicalAI.UI;

namespace EaglePhysicalAI.Setup
{
    [DefaultExecutionOrder(1100)]
    public class MaverickV11WarThunderBootstrap : MonoBehaviour
    {
        [Header("Required")]
        public GameObject aircraftObject;
        public Camera mainCamera;

        [Header("Install")]
        public bool installV10Base = true;
        public bool installKeybindProfile = true;
        public bool installAircraftSystems = true;
        public bool installRadarHotas = true;
        public bool installPodHotas = true;
        public bool installWeaponSelector = true;
        public bool installHud = true;
        public bool disableOldHud = true;

        [Header("Created")]
        public MaverickV10Bootstrap v10;
        public MaverickWTKeybindProfileV11 keys;
        public MaverickWTAircraftSystemsV11 systems;
        public MaverickWTRadarHotasV11 radarHotas;
        public MaverickWTTargetingPodHotasV11 podHotas;
        public MaverickWTWeaponSelectorV11 weapons;
        public MaverickWTHudV11 hud;

        private void Awake()
        {
            Setup();
        }

        [ContextMenu("Setup MAVERICK v1.1 War Thunder Systems")]
        public void Setup()
        {
            if (aircraftObject == null) aircraftObject = GameObject.Find("F15E_Player");
            if (mainCamera == null) mainCamera = Camera.main;

            if (aircraftObject == null)
            {
                Debug.LogWarning("MaverickV11WarThunderBootstrap: aircraftObject missing.");
                return;
            }

            if (installV10Base)
            {
                v10 = GetOrAdd<MaverickV10Bootstrap>(gameObject);
                v10.aircraftObject = aircraftObject;
                v10.mainCamera = mainCamera;
                v10.Setup();
            }

            if (installKeybindProfile)
            {
                keys = GetOrAdd<MaverickWTKeybindProfileV11>(aircraftObject);
            }

            if (installAircraftSystems)
            {
                systems = GetOrAdd<MaverickWTAircraftSystemsV11>(aircraftObject);
                systems.aircraft = aircraftObject.GetComponent<AircraftPhysicsController>();
                systems.visualGear = aircraftObject.GetComponent<SimpleLandingGearController>();
                systems.keys = keys;
            }

            if (installRadarHotas)
            {
                radarHotas = GetOrAdd<MaverickWTRadarHotasV11>(aircraftObject);
                radarHotas.radar = aircraftObject.GetComponent<F15ERadarSystem>();
                radarHotas.keys = keys;
            }

            if (installPodHotas)
            {
                podHotas = GetOrAdd<MaverickWTTargetingPodHotasV11>(aircraftObject);
                podHotas.pod = aircraftObject.GetComponent<TargetingPodSystem>();
                podHotas.keys = keys;
            }

            if (installWeaponSelector)
            {
                weapons = GetOrAdd<MaverickWTWeaponSelectorV11>(aircraftObject);
                weapons.strikeSystem = aircraftObject.GetComponent<AbstractStrikeSystem>();
                weapons.pod = aircraftObject.GetComponent<TargetingPodSystem>();
                weapons.keys = keys;
            }

            if (installHud && mainCamera != null)
            {
                hud = GetOrAdd<MaverickWTHudV11>(mainCamera.gameObject);
                hud.viewCamera = mainCamera;
                hud.keys = keys;
                hud.aircraft = aircraftObject.GetComponent<AircraftPhysicsController>();
                hud.instructor = aircraftObject.GetComponent<MaverickInstructorV10>();
                hud.aim = aircraftObject.GetComponent<MaverickAimDirectorV10>();
                hud.systems = systems;
                hud.radarHotas = radarHotas;
                hud.podHotas = podHotas;
                hud.weapons = weapons;
            }

            if (disableOldHud)
            {
                foreach (var old in FindObjectsOfType<MaverickFlightHudV10>(true))
                    old.visible = false;
                foreach (var old in FindObjectsOfType<MaverickFlightHudMarkers>(true))
                    old.showHud = false;
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
