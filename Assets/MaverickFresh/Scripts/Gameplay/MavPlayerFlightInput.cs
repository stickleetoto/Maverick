using UnityEngine;
using MaverickFresh.FlightDynamics;

namespace MaverickFresh.Gameplay
{
    /// <summary>
    /// The one owner of player flight input: keys -> normalized <see cref="MavPilotCommand"/> -> the aircraft's
    /// single operational command source. Nothing else writes that source, and no key is read in the flight
    /// physics.
    ///
    ///   W/S (Up/Down)     pitch        A/D (Left/Right)  roll        Q/E  yaw
    ///   Shift/Ctrl        throttle REQUEST - shown to the player only. Where the aircraft has fixed thrust the
    ///                     request goes nowhere, and the HUD says so.
    ///
    /// Gating: while <see cref="inputEnabled"/> is false (starting, paused, failed) the command is held exactly
    /// neutral, so resuming always starts from a centred stick. The source keeps its signal throughout: the
    /// aircraft is being flown hands-off, not losing its input path.
    ///
    /// Axis shaping reuses the keyboard source's own rules (<see cref="MavKeyboardPilotCommandSource.Shape"/>),
    /// so the stick feels the same as the validated keyboard rig.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MavPlayerFlightInput : MonoBehaviour
    {
        public MavManualPilotCommandSource source;
        public bool inputEnabled;

        [Header("Shaping (Maverick input choice, same as MavKeyboardPilotCommandSource)")]
        [Min(0.01f)] public float axisRiseRatePerSec = 2.5f;
        [Min(0.01f)] public float axisReturnRatePerSec = 4f;
        [Min(0.01f)] public float throttleRequestRatePerSec = 0.5f;

        [Header("Throttle request (not thrust)")]
        [Range(0f, 1f)] public float requestedThrottle01 = 1f;

        [Tooltip("True when the aircraft's thrust ignores the throttle, so the request is shown but never flown.")]
        public bool throttleHasNoEffect = true;

        [Header("Validation")]
        public bool scriptedOverride;
        public MavPilotCommand scriptedCommand = MavPilotCommand.Neutral;

        [Header("Debug")]
        public MavPilotCommand debugCommand = MavPilotCommand.Neutral;
        public float lastThrottleKeyUnscaledTime = -100f;

        private MavPilotCommand current = MavPilotCommand.Neutral;

        public MavPilotCommand CurrentCommand
        {
            get { return current; }
        }

        /// <summary>Enables or suspends player control. Either way the stick is re-centred at once.</summary>
        public void SetInputEnabled(bool enabled)
        {
            inputEnabled = enabled;
            current = MavPilotCommand.Neutral;
            current.throttle01 = 1f;
            Publish();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (!inputEnabled)
            {
                current = MavPilotCommand.Neutral;
                current.throttle01 = 1f;
                Publish();
                return;
            }

            if (scriptedOverride)
            {
                current = scriptedCommand;
                current.throttle01 = 1f;
                Publish();
                return;
            }

            float pitchTarget = MavKeyboardPilotCommandSource.Axis(Held(KeyCode.W, KeyCode.UpArrow), Held(KeyCode.S, KeyCode.DownArrow));
            float rollTarget = MavKeyboardPilotCommandSource.Axis(Held(KeyCode.D, KeyCode.RightArrow), Held(KeyCode.A, KeyCode.LeftArrow));
            float yawTarget = MavKeyboardPilotCommandSource.Axis(Held(KeyCode.E, KeyCode.None), Held(KeyCode.Q, KeyCode.None));

            current.pitch = MavKeyboardPilotCommandSource.Shape(current.pitch, pitchTarget, axisRiseRatePerSec, axisReturnRatePerSec, dt);
            current.roll = MavKeyboardPilotCommandSource.Shape(current.roll, rollTarget, axisRiseRatePerSec, axisReturnRatePerSec, dt);
            current.yaw = MavKeyboardPilotCommandSource.Shape(current.yaw, yawTarget, axisRiseRatePerSec, axisReturnRatePerSec, dt);

            bool up = Held(KeyCode.LeftShift, KeyCode.RightShift);
            bool down = Held(KeyCode.LeftControl, KeyCode.RightControl);
            if (up)
                requestedThrottle01 = Mathf.Clamp01(requestedThrottle01 + throttleRequestRatePerSec * dt);
            if (down)
                requestedThrottle01 = Mathf.Clamp01(requestedThrottle01 - throttleRequestRatePerSec * dt);
            if (up || down)
                lastThrottleKeyUnscaledTime = Time.unscaledTime;

            // Fixed-thrust aircraft: the flown throttle stays where the validated rig keeps it. The request above
            // is a UI value only.
            current.throttle01 = throttleHasNoEffect ? 1f : requestedThrottle01;
            Publish();
        }

        private void Publish()
        {
            debugCommand = current.Clamped();
            if (source != null)
                source.command = debugCommand;
        }

        private static bool Held(KeyCode a, KeyCode b)
        {
            return (a != KeyCode.None && MavFreshInput.GetKey(a)) || (b != KeyCode.None && MavFreshInput.GetKey(b));
        }
    }
}
