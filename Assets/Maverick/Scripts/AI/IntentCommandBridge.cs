using UnityEngine;
using EaglePhysicalAI.Aircraft;

namespace EaglePhysicalAI.AI
{
    /// <summary>
    /// Minimal bridge from abstract intent labels to safe controller/autopilot modes.
    /// This is the place to later connect LLM/Bio Overlay intent output.
    /// </summary>
    public class IntentCommandBridge : MonoBehaviour
    {
        public WaypointAutopilot autopilot;
        public AircraftPhysicsController aircraft;

        private void Awake()
        {
            if (autopilot == null) autopilot = GetComponent<WaypointAutopilot>();
            if (aircraft == null) aircraft = GetComponent<AircraftPhysicsController>();
        }

        public void ApplyIntent(AircraftIntent intent, Transform target = null)
        {
            if (aircraft == null || autopilot == null) return;

            switch (intent)
            {
                case AircraftIntent.Manual:
                    autopilot.autopilotEnabled = false;
                    break;
                case AircraftIntent.StabilizeAircraft:
                    autopilot.autopilotEnabled = true;
                    autopilot.targetAltitude = Mathf.Max(aircraft.Altitude + 200f, 400f);
                    autopilot.targetSpeed = 220f;
                    break;
                case AircraftIntent.ApproachCasZone:
                case AircraftIntent.ConfirmTarget:
                case AircraftIntent.StrikeOrAbort:
                    if (target != null) autopilot.SetDirectTarget(target, true);
                    break;
                case AircraftIntent.OrbitCasZone:
                    if (target != null) autopilot.SetOrbitTarget(target, 900f, 1, true);
                    break;
                case AircraftIntent.BreakAway:
                    autopilot.autopilotEnabled = true;
                    autopilot.directTarget = null;
                    autopilot.targetAltitude = Mathf.Max(aircraft.Altitude + 350f, 800f);
                    autopilot.targetSpeed = 260f;
                    break;
            }
        }
    }
}
