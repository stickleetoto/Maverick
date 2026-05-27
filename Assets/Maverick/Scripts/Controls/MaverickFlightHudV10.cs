using UnityEngine;
using EaglePhysicalAI.Aircraft;

namespace EaglePhysicalAI.Controls
{
    public class MaverickFlightHudV10 : MonoBehaviour
    {
        public Camera viewCamera;
        public MaverickAimDirectorV10 aim;
        public MaverickInstructorV10 instructor;
        public AircraftPhysicsController aircraft;

        public bool visible = true;
        public KeyCode toggleKey = KeyCode.F12;

        public int aimMarkerSize = 24;
        public int noseMarkerSize = 18;
        public int velocityMarkerSize = 16;

        private GUIStyle boxStyle;
        private GUIStyle labelStyle;
        private GUIStyle warnStyle;

        private void Awake()
        {
            if (viewCamera == null) viewCamera = Camera.main;
            if (instructor == null) instructor = FindObjectOfType<MaverickInstructorV10>();
            if (aim == null) aim = FindObjectOfType<MaverickAimDirectorV10>();
            if (aircraft == null && instructor != null) aircraft = instructor.GetComponent<AircraftPhysicsController>();
        }

        private void Update()
        {
            if (MaverickInput.GetKeyDown(toggleKey)) visible = !visible;
        }

        private void OnGUI()
        {
            if (!visible) return;
            EnsureStyles();

            if (viewCamera == null) viewCamera = Camera.main;

            DrawAimMarker();
            DrawNoseMarker();
            DrawVelocityMarker();
            DrawInfoPanel();
        }

        private void DrawAimMarker()
        {
            if (aim == null) return;
            Vector2 p = aim.ScreenPoint();
            GUI.Box(new Rect(p.x - aimMarkerSize * 0.5f, p.y - aimMarkerSize * 0.5f, aimMarkerSize, aimMarkerSize), "◇");
        }

        private void DrawNoseMarker()
        {
            if (aircraft == null || viewCamera == null) return;
            Vector3 world = aircraft.transform.position + aircraft.transform.forward * 1600f;
            DrawWorldMarker(world, "+", noseMarkerSize);
        }

        private void DrawVelocityMarker()
        {
            if (aircraft == null || aircraft.rb == null || aircraft.rb.linearVelocity.sqrMagnitude < 9f) return;
            Vector3 world = aircraft.transform.position + aircraft.rb.linearVelocity.normalized * 1600f;
            DrawWorldMarker(world, "○", velocityMarkerSize);
        }

        private void DrawWorldMarker(Vector3 world, string text, int size)
        {
            Vector3 s = viewCamera.WorldToScreenPoint(world);
            if (s.z <= 0f) return;
            GUI.Box(new Rect(s.x - size * 0.5f, Screen.height - s.y - size * 0.5f, size, size), text);
        }

        private void DrawInfoPanel()
        {
            if (aircraft == null || instructor == null) return;

            string txt =
                "MAVERICK v1.0\n" +
                $"Mode: {instructor.controlMode} | State: {instructor.instructorState}\n" +
                $"Speed: {aircraft.Speed:0} m/s | Alt: {aircraft.Altitude:0} m | Thr: {aircraft.throttle:0.00}\n" +
                $"AoA: {aircraft.AngleOfAttack:0.0} | Stall: {aircraft.StallRisk:0.00} | G: {aircraft.LoadFactorEstimate:0.0}\n" +
                $"Input P/R/Y: {instructor.lastPitch:0.00}/{instructor.lastRoll:0.00}/{instructor.lastYaw:0.00}\n" +
                $"Err P/Y: {instructor.pitchErrorDeg:0.0}/{instructor.yawErrorDeg:0.0} | Bank {instructor.currentBankDeg:0.0}->{instructor.desiredBankDeg:0.0}\n" +
                "F5 MouseAim  F6 Assist  F7 Direct  F8 AI\n" +
                "Mouse aim | A/D roll | Q/E yaw | S pitch | Shift/Ctrl throttle | W WEP | X idle";

            GUI.Box(new Rect(10, 10, 455, 205), txt, boxStyle);

            if (aircraft.IsStalling || aircraft.StallRisk > 0.72f)
                GUI.Label(new Rect(Screen.width * 0.5f - 70, 72, 180, 32), aircraft.IsStalling ? "STALL" : "STALL RISK", warnStyle);
        }

        private void EnsureStyles()
        {
            if (boxStyle != null) return;
            boxStyle = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 13 };
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            warnStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            warnStyle.normal.textColor = Color.yellow;
        }
    }
}
