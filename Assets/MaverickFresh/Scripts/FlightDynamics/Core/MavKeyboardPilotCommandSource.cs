using UnityEngine;

namespace MaverickFresh.FlightDynamics
{
    /// <summary>
    /// Keyboard pilot-command source for the replacement FDM: a human's keys in, normalized
    /// <see cref="MavPilotCommand"/> out. Aircraft-independent - any control law that reads a
    /// <see cref="MavPilotCommandSourceBase"/> can be flown with it - and it knows nothing about any
    /// airframe, surface or sign convention.
    ///
    /// Default bindings follow Maverick's existing keyboard convention (MavInstructorController):
    /// W / Up = pitch up, S / Down = pitch down, A / Left = roll left, D / Right = roll right,
    /// Q = yaw left, E = yaw right. Keys are read through <see cref="MavFreshInput"/>, so legacy input,
    /// the new Input System or both work.
    ///
    /// INPUT SHAPING. A key is binary; each axis ramps toward its key target at
    /// <see cref="axisRiseRatePerSec"/> and back to centre at <see cref="axisReturnRatePerSec"/>. That
    /// is keyboard-to-stick shaping of this input device, a Maverick input choice - not aircraft data,
    /// not an actuator rate and not a control law.
    ///
    /// It produces intent only: it never touches a Rigidbody, a force, a moment or a surface.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MavKeyboardPilotCommandSource : MavPilotCommandSourceBase
    {
        [Header("Bindings (Maverick keyboard convention)")]
        public KeyCode pitchUpKey = KeyCode.W;
        public KeyCode pitchUpAltKey = KeyCode.UpArrow;
        public KeyCode pitchDownKey = KeyCode.S;
        public KeyCode pitchDownAltKey = KeyCode.DownArrow;
        public KeyCode rollLeftKey = KeyCode.A;
        public KeyCode rollLeftAltKey = KeyCode.LeftArrow;
        public KeyCode rollRightKey = KeyCode.D;
        public KeyCode rollRightAltKey = KeyCode.RightArrow;
        public KeyCode yawLeftKey = KeyCode.Q;
        public KeyCode yawRightKey = KeyCode.E;
        public KeyCode throttleUpKey = KeyCode.LeftShift;
        public KeyCode throttleDownKey = KeyCode.LeftControl;

        [Header("Input shaping (Maverick input choice, not aircraft data)")]
        [Min(0.01f)] public float axisRiseRatePerSec = 2.5f;
        [Min(0.01f)] public float axisReturnRatePerSec = 4f;
        [Min(0.01f)] public float throttleRatePerSec = 0.5f;

        [Tooltip("Largest command each axis can reach from the keyboard.")]
        [Range(0f, 1f)] public float pitchAxisLimit = 1f;
        [Range(0f, 1f)] public float rollAxisLimit = 1f;
        [Range(0f, 1f)] public float yawAxisLimit = 1f;

        [Range(0f, 1f)] public float throttle01 = 1f;

        [Header("Debug")]
        public MavPilotCommand debugCommand = MavPilotCommand.Neutral;

        private MavPilotCommand current = MavPilotCommand.Neutral;

        public override string CommandSourceName
        {
            get { return "Keyboard pilot command source (Maverick keyboard convention)"; }
        }

        /// <summary>A human keyboard is an operational input path by declaration.</summary>
        public override bool IsOperationalCommandSource
        {
            get { return true; }
        }

        public override bool HasCommandSignal
        {
            get { return isActiveAndEnabled; }
        }

        public override MavCommandSignalLossPolicy SignalLossPolicy
        {
            get { return MavCommandSignalLossPolicy.NeutralCommand; }
        }

        private void OnEnable()
        {
            current = MavPilotCommand.Neutral;
            current.throttle01 = throttle01;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float pitchTarget = Axis(Held(pitchUpKey, pitchUpAltKey), Held(pitchDownKey, pitchDownAltKey)) * pitchAxisLimit;
            float rollTarget = Axis(Held(rollRightKey, rollRightAltKey), Held(rollLeftKey, rollLeftAltKey)) * rollAxisLimit;
            float yawTarget = Axis(Held(yawRightKey, KeyCode.None), Held(yawLeftKey, KeyCode.None)) * yawAxisLimit;

            current.pitch = Shape(current.pitch, pitchTarget, axisRiseRatePerSec, axisReturnRatePerSec, dt);
            current.roll = Shape(current.roll, rollTarget, axisRiseRatePerSec, axisReturnRatePerSec, dt);
            current.yaw = Shape(current.yaw, yawTarget, axisRiseRatePerSec, axisReturnRatePerSec, dt);

            if (MavFreshInput.GetKey(throttleUpKey))
                throttle01 = Mathf.Clamp01(throttle01 + throttleRatePerSec * dt);
            if (MavFreshInput.GetKey(throttleDownKey))
                throttle01 = Mathf.Clamp01(throttle01 - throttleRatePerSec * dt);
            current.throttle01 = throttle01;

            debugCommand = current.Clamped();
        }

        public override bool TryGetCommand(out MavPilotCommand command)
        {
            command = current.Clamped();
            return isActiveAndEnabled;
        }

        /// <summary>+1 for the positive key alone, -1 for the negative key alone, 0 for both or neither.</summary>
        public static float Axis(bool positive, bool negative)
        {
            return (positive ? 1f : 0f) - (negative ? 1f : 0f);
        }

        /// <summary>
        /// Moves an axis toward its target: at <paramref name="riseRate"/> when moving away from centre,
        /// at <paramref name="returnRate"/> when moving toward it. Pure, so the shaping is testable.
        /// </summary>
        public static float Shape(float current, float target, float riseRate, float returnRate, float dt)
        {
            float step = Mathf.Abs(target) > Mathf.Abs(current) || Mathf.Sign(target) != Mathf.Sign(current)
                ? riseRate
                : returnRate;
            if (Mathf.Abs(target) < 1e-6f)
                step = returnRate;
            return Mathf.MoveTowards(current, target, Mathf.Max(0f, step) * Mathf.Max(0f, dt));
        }

        private static bool Held(KeyCode a, KeyCode b)
        {
            return (a != KeyCode.None && MavFreshInput.GetKey(a)) || (b != KeyCode.None && MavFreshInput.GetKey(b));
        }
    }
}
