using UnityEngine;
using EaglePhysicalAI.Aircraft;

namespace EaglePhysicalAI.Controls
{
    /// <summary>
    /// Immediate-mode HUD for v0.9: mouse aim cursor, nose marker, velocity vector, and flight info.
    /// </summary>
    public class MaverickFlightHudMarkers : MonoBehaviour
    {
        public MaverickMouseAimDirector aimDirector;
        public MaverickInstructor instructor;
        public AircraftPhysicsController aircraft;
        public Camera viewCamera;
        public bool showHud = true;
        public bool showDebugPanel = true;

        [Header("Marker Sizes")]
        public int aimSize = 22;
        public int noseSize = 18;
        public int velocitySize = 16;

        private GUIStyle boxStyle;
        private GUIStyle labelStyle;
        private GUIStyle warningStyle;

        private void Awake()
        {
            if (viewCamera == null) viewCamera = Camera.main;
            if (instructor == null) instructor = FindObjectOfType<MaverickInstructor>();
            if (aimDirector == null) aimDirector = FindObjectOfType<MaverickMouseAimDirector>();
            if (aircraft == null && instructor != null) aircraft = instructor.GetComponent<AircraftPhysicsController>();
        }

        private void EnsureStyles()
        {
            if (boxStyle != null) return;
            boxStyle = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 13 };
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            warningStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
            warningStyle.normal.textColor = Color.yellow;
        }

        private void OnGUI()
        {
            if (!showHud) return;
            EnsureStyles();
            if (viewCamera == null) viewCamera = Camera.main;

            DrawAimCursor();
            DrawNoseMarker();
            DrawVelocityVector();
            if (showDebugPanel) DrawInfo();
        }

        private void DrawAimCursor()
        {
            if (aimDirector == null) return;
            Vector2 p = aimDirector.GetScreenPoint();
            Rect r = new Rect(p.x - aimSize * 0.5f, p.y - aimSize * 0.5f, aimSize, aimSize);
            GUI.Box(r, "◇");
        }

        private void DrawNoseMarker()
        {
            if (aircraft == null || viewCamera == null) return;
            Vector3 world = aircraft.transform.position + aircraft.transform.forward * 1200f;
            Vector3 s = viewCamera.WorldToScreenPoint(world);
            if (s.z <= 0f) return;
            Rect r = new Rect(s.x - noseSize * 0.5f, Screen.height - s.y - noseSize * 0.5f, noseSize, noseSize);
            GUI.Box(r, "+");
        }

        private void DrawVelocityVector()
        {
            if (aircraft == null || aircraft.rb == null || viewCamera == null || aircraft.rb.linearVelocity.sqrMagnitude < 4f) return;
            Vector3 world = aircraft.transform.position + aircraft.rb.linearVelocity.normalized * 1200f;
            Vector3 s = viewCamera.WorldToScreenPoint(world);
            if (s.z <= 0f) return;
            Rect r = new Rect(s.x - velocitySize * 0.5f, Screen.height - s.y - velocitySize * 0.5f, velocitySize, velocitySize);
            GUI.Box(r, "○");
        }

        private void DrawInfo()
        {
            if (aircraft == null || instructor == null) return;
            string warn = aircraft.IsStalling ? "STALL" : aircraft.StallRisk > 0.65f ? "STALL RISK" : "";
            string text =
                "MAVERICK v0.9 Mouse Aim\n" +
                $"Mode: {instructor.controlMode}\n" +
                $"Speed: {aircraft.Speed:0} m/s   Alt: {aircraft.Altitude:0} m\n" +
                $"Throttle: {aircraft.throttle:0.00}   G est: {aircraft.LoadFactorEstimate:0.0}\n" +
                $"AoA: {aircraft.AngleOfAttack:0.0}   StallRisk: {aircraft.StallRisk:0.00}\n" +
                $"Pitch/Roll/Yaw: {instructor.lastPitch:0.00}/{instructor.lastRoll:0.00}/{instructor.lastYaw:0.00}\n" +
                $"Aim Err P/Y: {instructor.pitchErrorDeg:0.0}/{instructor.yawErrorDeg:0.0}\n" +
                $"Bank: {instructor.currentBankDeg:0.0} -> {instructor.desiredBankDeg:0.0}\n" +
                $"Note: {instructor.instructorNote}\n" +
                "F5 MouseAim | F6 Assisted | F7 Direct | F8 AI\n" +
                "Mouse aim | A/D roll | Q/E rudder | Shift/Ctrl throttle | W WEP | X idle";
            GUI.Box(new Rect(10, 10, 430, 250), text, boxStyle);

            if (!string.IsNullOrEmpty(warn))
                GUI.Label(new Rect(Screen.width * 0.5f - 80, 80, 220, 40), warn, warningStyle);
        }
    }
}
