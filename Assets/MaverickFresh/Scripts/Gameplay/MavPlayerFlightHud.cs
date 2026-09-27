using UnityEngine;
using MaverickFresh.FlightDynamics;
using MaverickFresh.FlightDynamics.F15;

namespace MaverickFresh.Gameplay
{
    /// <summary>
    /// The player's flight HUD: what a pilot needs, nothing a validation harness needs.
    ///
    ///   left   airspeed (true, knots) and Mach
    ///   right  altitude (feet) and vertical speed
    ///   bottom G (the flight model's own measured load factor, shown only when it is valid), AoA, heading, thrust
    ///   top    aircraft, camera mode, and one warning line when something matters
    ///
    /// Every number comes from the flight model's published state; the HUD computes no physics. The F-15 has fixed
    /// test thrust, so thrust is shown as exactly that, never as a throttle gauge. Developer detail lives in
    /// <see cref="MavFlightDebugHud"/> (F1).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MavPlayerFlightHud : MonoBehaviour
    {
        public const float MpsToKnots = 1.943844f;
        public const float MToFt = 3.280840f;

        public MavFlightSessionDirector director;
        public float controlHintSeconds = 12f;
        public float throttleNoticeSeconds = 3f;

        private GUIStyle warn, danger, value, label, center, small;

        private void OnGUI()
        {
            if (director == null || director.Aircraft == null || director.Aircraft.body == null)
                return;
            MavFlightSessionState st = director.State;
            if (st != MavFlightSessionState.Playing && st != MavFlightSessionState.Paused)
                return;

            MavGameplayUiStyle.Ensure();
            EnsureStyles();
            GUI.depth = 10;

            MavSpawnedAircraft a = director.Aircraft;
            MavFlightState s = a.body.debugState;
            MavF15PilotControlledDiagnostics diag = a.rig != null ? a.rig.debugDiagnostics : default(MavF15PilotControlledDiagnostics);
            float w = Screen.width, h = Screen.height;

            // Top: identity and camera.
            MavGameplayUiStyle.Panel(new Rect(16f, 14f, 360f, 54f));
            GUI.Label(new Rect(28f, 18f, 340f, 28f), director.Definition.displayName, MavGameplayUiStyle.Heading);
            string cameraLabel = director.FlightCamera != null ? MavFlightCameraController.Label(director.FlightCamera.mode) : "";
            GUI.Label(new Rect(28f, 44f, 340f, 20f), "CAMERA " + cameraLabel + "   (V)", small);

            // Left: speed.
            float knots = s.trueAirspeedMps * MpsToKnots;
            DrawBlock(new Rect(24f, h * 0.5f - 60f, 170f, 120f), "TAS KT", knots.ToString("0"), "MACH " + s.mach.ToString("0.00"));

            // Right: altitude.
            float altFt = a.rig.transform.position.y * MToFt;
            float vsFpm = s.worldVelocityMps.y * MToFt * 60f;
            DrawBlock(new Rect(w - 194f, h * 0.5f - 60f, 170f, 120f), "ALT FT", altFt.ToString("0"), "VS " + vsFpm.ToString("+0;-0;0") + " FPM");

            // Bottom strip.
            Rect strip = new Rect(w * 0.5f - 330f, h - 86f, 660f, 66f);
            MavGameplayUiStyle.Panel(strip);
            string g = s.specificForceValid ? s.LoadFactorNz.ToString("0.0") : "--";
            string hdg = s.attitude.valid ? Mathf.Repeat(s.attitude.headingRad * Mathf.Rad2Deg, 360f).ToString("000") : "---";
            Cell(new Rect(strip.x + 12f, strip.y + 6f, 120f, 56f), "G", g);
            Cell(new Rect(strip.x + 132f, strip.y + 6f, 120f, 56f), "AOA", s.AlphaDeg.ToString("0.0") + "°");
            Cell(new Rect(strip.x + 252f, strip.y + 6f, 120f, 56f), "HDG", hdg);
            Cell(new Rect(strip.x + 372f, strip.y + 6f, 276f, 56f), "THRUST", "FIXED TEST THRUST");

            // One warning line, most important first.
            string warning = null;
            GUIStyle warningStyle = warn;
            if (diag.evaluated && !diag.sourceReproductionAdmitted)
            {
                warning = "OUTSIDE FLIGHT MODEL ENVELOPE - NO AERODYNAMIC LIFT";
                warningStyle = danger;
            }
            else if (altFt < 1500f)
            {
                warning = "LOW ALTITUDE - PULL UP";
                warningStyle = danger;
            }
            else if (a.input != null && Time.unscaledTime - a.input.lastThrottleKeyUnscaledTime < throttleNoticeSeconds)
            {
                warning = "THROTTLE HAS NO EFFECT - FIXED TEST THRUST";
            }

            if (warning != null)
                GUI.Label(new Rect(0f, 84f, w, 30f), warning, warningStyle);

            if (st == MavFlightSessionState.Playing && director.SecondsInState < controlHintSeconds)
                GUI.Label(new Rect(0f, h - 118f, w, 24f), director.Definition.controlHint + "   F1 debug", center);

            if (director.FlightCamera != null && director.FlightCamera.mode == MavFlightCameraMode.Nose)
                DrawWaterline(w * 0.5f, h * 0.5f);
        }

        private void DrawBlock(Rect r, string caption, string big, string sub)
        {
            MavGameplayUiStyle.Panel(r);
            GUI.Label(new Rect(r.x + 14f, r.y + 8f, r.width, 18f), caption, label);
            GUI.Label(new Rect(r.x + 14f, r.y + 30f, r.width, 40f), big, value);
            GUI.Label(new Rect(r.x + 14f, r.y + 80f, r.width, 22f), sub, label);
        }

        private void Cell(Rect r, string caption, string text)
        {
            GUI.Label(new Rect(r.x, r.y, r.width, 18f), caption, label);
            GUI.Label(new Rect(r.x, r.y + 18f, r.width, 34f), text, value);
        }

        private static void DrawWaterline(float cx, float cy)
        {
            Color c = MavGameplayUiStyle.Accent;
            MavGameplayUiStyle.Fill(new Rect(cx - 34f, cy - 1f, 22f, 2f), c);
            MavGameplayUiStyle.Fill(new Rect(cx + 12f, cy - 1f, 22f, 2f), c);
            MavGameplayUiStyle.Fill(new Rect(cx - 2f, cy - 2f, 4f, 4f), c);
        }

        private void EnsureStyles()
        {
            if (value != null)
                return;
            value = new GUIStyle(MavGameplayUiStyle.HudValue);
            label = new GUIStyle(MavGameplayUiStyle.HudLabel);
            small = MavGameplayUiStyle.Tinted(MavGameplayUiStyle.Small, MavGameplayUiStyle.Accent);
            warn = new GUIStyle(MavGameplayUiStyle.HudCenter);
            danger = MavGameplayUiStyle.Tinted(MavGameplayUiStyle.HudCenter, MavGameplayUiStyle.Danger);
            center = MavGameplayUiStyle.Tinted(MavGameplayUiStyle.Small, MavGameplayUiStyle.Text);
            center.alignment = TextAnchor.MiddleCenter;
        }
    }
}
