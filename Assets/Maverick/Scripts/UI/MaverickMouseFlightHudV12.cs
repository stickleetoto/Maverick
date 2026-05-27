using UnityEngine;
using EaglePhysicalAI.Aircraft;
using EaglePhysicalAI.Controls;

namespace EaglePhysicalAI.UI
{
    /// <summary>
    /// HUD based on MouseFlight-style BoresightPos and MouseAimPos.
    /// </summary>
    public class MaverickMouseFlightHudV12 : MonoBehaviour
    {
        public Camera viewCamera;
        public MaverickMouseFlightRigV12 rig;
        public MaverickMouseFlightInstructorV12 instructor;
        public AircraftPhysicsController aircraft;
        public MaverickWTKeybindProfileV11 keys;

        public bool visible = true;
        public bool showDebugPanel = true;

        private GUIStyle panel;
        private GUIStyle warning;

        private void Awake()
        {
            if (viewCamera == null) viewCamera = Camera.main;
            if (rig == null) rig = FindObjectOfType<MaverickMouseFlightRigV12>();
            if (instructor == null) instructor = FindObjectOfType<MaverickMouseFlightInstructorV12>();
            if (aircraft == null && instructor != null) aircraft = instructor.GetComponent<AircraftPhysicsController>();
            if (keys == null) keys = FindObjectOfType<MaverickWTKeybindProfileV11>();
        }

        private void Update()
        {
            if (keys != null && MaverickInput.GetKeyDown(keys.toggleHUD))
                visible = !visible;
        }

        private void OnGUI()
        {
            if (!visible) return;
            EnsureStyles();

            if (viewCamera == null) viewCamera = Camera.main;

            if (rig != null)
            {
                DrawWorldMarker(rig.MouseAimPos, "◇", 24);
                DrawWorldMarker(rig.BoresightPos, "+", 18);
                DrawWorldMarker(rig.VelocityVectorPos, "○", 16);
            }

            if (showDebugPanel)
                DrawPanel();
        }

        private void DrawWorldMarker(Vector3 world, string label, int size)
        {
            if (viewCamera == null) return;
            Vector3 p = viewCamera.WorldToScreenPoint(world);
            if (p.z <= 0f) return;
            GUI.Box(new Rect(p.x - size * 0.5f, Screen.height - p.y - size * 0.5f, size, size), label);
        }

        private void DrawPanel()
        {
            string text = "MAVERICK v1.2 MouseFlight Core\n";

            if (aircraft != null)
            {
                text += $"SPD {aircraft.Speed:0} ALT {aircraft.Altitude:0} THR {aircraft.throttle:0.00} AoA {aircraft.AngleOfAttack:0.0} G {aircraft.LoadFactorEstimate:0.0}\n";
            }

            if (instructor != null)
            {
                text += $"MODE {instructor.controlMode} STATE {instructor.instructorNote}\n";
                text += $"P/R/Y {instructor.lastPitch:0.00}/{instructor.lastRoll:0.00}/{instructor.lastYaw:0.00}  ANG {instructor.angleOffTarget:0.0}\n";
                text += $"localFlyTarget {instructor.localFlyTarget.x:0.00},{instructor.localFlyTarget.y:0.00},{instructor.localFlyTarget.z:0.00}\n";
                text += $"roll aggr {instructor.aggressiveRoll:0.00} wings {instructor.wingsLevelRoll:0.00} mix {instructor.wingsLevelInfluence:0.00}\n";
            }

            if (rig != null)
            {
                text += $"RIG {rig.rigNote} frozen {rig.isMouseAimFrozen}\n";
            }

            text += "◇ MouseAim  + Boresight  ○ Velocity\n";
            text += "Mouse aim | C/Alt freelook freeze | Mouse2 recenter | A/D roll | Q/E yaw | W WEP | X idle";

            GUI.Box(new Rect(10, 10, 560, 195), text, panel);

            if (aircraft != null && (aircraft.IsStalling || aircraft.StallRisk > 0.72f))
            {
                GUI.Label(new Rect(Screen.width * 0.5f - 80, 75, 220, 35), aircraft.IsStalling ? "STALL" : "STALL RISK", warning);
            }
        }

        private void EnsureStyles()
        {
            if (panel != null) return;
            panel = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 13 };
            warning = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
            warning.normal.textColor = Color.yellow;
        }
    }
}
