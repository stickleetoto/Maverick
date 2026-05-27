using UnityEngine;
using EaglePhysicalAI.Aircraft;

namespace EaglePhysicalAI.Controls
{
    /// <summary>
    /// Minimal immediate-mode HUD for mouse aim reticle and War-Thunder-like control debug.
    /// This keeps the project UI-framework-free for quick testing.
    /// </summary>
    public class WarThunderControlHud : MonoBehaviour
    {
        public WarThunderMouseAircraftInput input;
        public AircraftPhysicsController aircraft;
        public bool showHud = true;
        public bool showDebugBox = true;
        public int reticleSize = 18;
        public int flightPathMarkerSize = 14;

        private GUIStyle boxStyle;
        private GUIStyle labelStyle;

        private void Awake()
        {
            if (input == null) input = FindObjectOfType<WarThunderMouseAircraftInput>();
            if (aircraft == null && input != null) aircraft = input.GetComponent<AircraftPhysicsController>();
        }

        private void EnsureGuiStyles()
        {
            if (boxStyle == null) boxStyle = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 12 };
            if (labelStyle == null) labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
        }

        private void OnGUI()
        {
            if (!showHud || input == null) return;
            EnsureGuiStyles();

            DrawReticle();
            DrawFlightPathMarker();
            if (showDebugBox) DrawDebugBox();
        }

        private void DrawReticle()
        {
            Vector2 vp = input.GetReticleViewport();
            float x = vp.x * Screen.width;
            float y = (1f - vp.y) * Screen.height;
            Rect r = new Rect(x - reticleSize * 0.5f, y - reticleSize * 0.5f, reticleSize, reticleSize);
            GUI.Box(r, "+");
        }

        private void DrawFlightPathMarker()
        {
            if (aircraft == null || aircraft.rb == null || aircraft.rb.linearVelocity.sqrMagnitude < 1f || Camera.main == null) return;
            Vector3 world = aircraft.transform.position + aircraft.rb.linearVelocity.normalized * 800f;
            Vector3 screen = Camera.main.WorldToScreenPoint(world);
            if (screen.z <= 0f) return;
            Rect r = new Rect(screen.x - flightPathMarkerSize * 0.5f, Screen.height - screen.y - flightPathMarkerSize * 0.5f, flightPathMarkerSize, flightPathMarkerSize);
            GUI.Box(r, "o");
        }

        private void DrawDebugBox()
        {
            string text = "WT-Like Controls\n" +
                          $"Mode: {input.controlMode}\n" +
                          $"Throttle: {input.lastThrottle:0.00}\n" +
                          $"Pitch/Roll/Yaw: {input.lastPitch:0.00} / {input.lastRoll:0.00} / {input.lastYaw:0.00}\n" +
                          $"Aim Error P/Y: {input.directionPitchError:0.0} / {input.directionYawError:0.0}\n" +
                          $"Desired Bank: {input.desiredBank:0.0}\n" +
                          $"F5 MouseAim | F6 Direct | F7 Stabilized\n" +
                          $"Mouse aim, A/D roll, Q/E rudder, Shift/Ctrl throttle, W WEP, X idle";
            GUI.Box(new Rect(10, Screen.height - 150, 390, 140), text, boxStyle);
        }
    }
}
