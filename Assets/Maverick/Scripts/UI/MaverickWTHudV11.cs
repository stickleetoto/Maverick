using UnityEngine;
using EaglePhysicalAI.Aircraft;
using EaglePhysicalAI.Controls;
using EaglePhysicalAI.CAS;
using EaglePhysicalAI.Sensors.Radar;
using EaglePhysicalAI.Sensors.TargetingPod;

namespace EaglePhysicalAI.UI
{
    public class MaverickWTHudV11 : MonoBehaviour
    {
        public bool visible = true;
        public Camera viewCamera;
        public AircraftPhysicsController aircraft;
        public MaverickInstructorV10 instructor;
        public MaverickAimDirectorV10 aim;
        public MaverickWTAircraftSystemsV11 systems;
        public MaverickWTRadarHotasV11 radarHotas;
        public MaverickWTTargetingPodHotasV11 podHotas;
        public MaverickWTWeaponSelectorV11 weapons;
        public MaverickWTKeybindProfileV11 keys;

        private GUIStyle panel;
        private GUIStyle warn;

        private void Awake()
        {
            if (viewCamera == null) viewCamera = Camera.main;
            if (aircraft == null) aircraft = FindObjectOfType<AircraftPhysicsController>();
            if (instructor == null) instructor = FindObjectOfType<MaverickInstructorV10>();
            if (aim == null) aim = FindObjectOfType<MaverickAimDirectorV10>();
            if (systems == null) systems = FindObjectOfType<MaverickWTAircraftSystemsV11>();
            if (radarHotas == null) radarHotas = FindObjectOfType<MaverickWTRadarHotasV11>();
            if (podHotas == null) podHotas = FindObjectOfType<MaverickWTTargetingPodHotasV11>();
            if (weapons == null) weapons = FindObjectOfType<MaverickWTWeaponSelectorV11>();
            if (keys == null) keys = FindObjectOfType<MaverickWTKeybindProfileV11>();
        }

        private void Update()
        {
            if (keys != null && MaverickInput.GetKeyDown(keys.toggleHUD)) visible = !visible;
        }

        private void OnGUI()
        {
            if (!visible) return;
            EnsureStyle();

            DrawMarkers();
            DrawPanel();
        }

        private void DrawMarkers()
        {
            if (viewCamera == null) viewCamera = Camera.main;

            if (aim != null)
            {
                Vector2 p = aim.ScreenPoint();
                GUI.Box(new Rect(p.x - 12, p.y - 12, 24, 24), "◇");
            }

            if (aircraft != null && viewCamera != null)
            {
                DrawWorldMarker(aircraft.transform.position + aircraft.transform.forward * 1600f, "+", 18);
                if (aircraft.rb != null && aircraft.rb.linearVelocity.sqrMagnitude > 9f)
                    DrawWorldMarker(aircraft.transform.position + aircraft.rb.linearVelocity.normalized * 1600f, "○", 16);
            }
        }

        private void DrawWorldMarker(Vector3 world, string label, int size)
        {
            Vector3 screen = viewCamera.WorldToScreenPoint(world);
            if (screen.z <= 0f) return;
            GUI.Box(new Rect(screen.x - size / 2, Screen.height - screen.y - size / 2, size, size), label);
        }

        private void DrawPanel()
        {
            string mode = instructor != null ? instructor.controlMode.ToString() : "none";
            string state = instructor != null ? instructor.instructorState : "none";
            string flight =
                aircraft != null
                    ? $"SPD {aircraft.Speed:0}  ALT {aircraft.Altitude:0}  THR {aircraft.throttle:0.00}  AoA {aircraft.AngleOfAttack:0.0}  G {aircraft.LoadFactorEstimate:0.0}"
                    : "no aircraft";

            string sys = systems != null
                ? $"GEAR {(systems.gearDown ? "DOWN" : "UP")}  FLAPS {systems.flaps}  AIRBRAKE {(systems.airbrakeDeployed ? "ON" : "OFF")}  SYS {systems.lastSystemEvent}"
                : "no systems";

            string radar = radarHotas != null && radarHotas.radar != null
                ? $"RADAR {(radarHotas.radarMasterOn ? radarHotas.radar.mode.ToString() : "OFF")}  TRK {radarHotas.radar.tracks.Count}  EVT {radarHotas.lastRadarEvent}"
                : "no radar";

            string pod = podHotas != null && podHotas.pod != null
                ? $"TGP {podHotas.pod.mode}  ID {podHotas.pod.identificationConfidence:0.00}  EVT {podHotas.lastPodEvent}"
                : "no pod";

            string wpn = weapons != null
                ? $"PRI {weapons.primary}  SEC {weapons.secondary}  EVT {weapons.lastWeaponEvent}"
                : "no weapon selector";

            string text =
                "MAVERICK v1.1 WT SYSTEMS\n" +
                $"MODE {mode}  STATE {state}\n" +
                flight + "\n" +
                sys + "\n" +
                radar + "\n" +
                pod + "\n" +
                wpn + "\n" +
                "Mouse Aim | A/D Roll | Q/E Rudder | S Pitch | W WEP | Shift/Ctrl Throttle\n" +
                "G Gear | F Flaps | B Airbrake/Brake | R Radar | T Track | O Lock | P Pod | Y Slave | LMB/Space Fire";

            GUI.Box(new Rect(10, 10, 650, 190), text, panel);

            if (aircraft != null && (aircraft.IsStalling || aircraft.StallRisk > 0.72f))
            {
                GUI.Label(new Rect(Screen.width / 2 - 80, 80, 220, 40), aircraft.IsStalling ? "STALL" : "STALL RISK", warn);
            }
        }

        private void EnsureStyle()
        {
            if (panel != null) return;
            panel = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 13 };
            warn = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
            warn.normal.textColor = Color.yellow;
        }
    }
}
