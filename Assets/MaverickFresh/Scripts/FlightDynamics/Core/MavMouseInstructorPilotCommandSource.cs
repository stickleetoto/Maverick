using UnityEngine;

namespace MaverickFresh.FlightDynamics
{
    /// <summary>
    /// Operational command bridge from Maverick's existing War-Thunder-like mouse instructor into
    /// the replacement FDM's aircraft-independent pilot-command contract.
    ///
    /// This component produces intent only. It never touches the Rigidbody, never applies a force or
    /// moment, and never owns a control surface. That keeps the existing instructor useful after the
    /// physical writer is gated off by replacement ownership.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MavMouseInstructorPilotCommandSource : MavPilotCommandSourceBase
    {
        [Tooltip("The existing Maverick mouse-flight instructor whose normalized output is bridged into the replacement FDM.")]
        public global::MaverickFresh.MavInstructorController instructor;

        [Header("Debug")]
        public MavPilotCommand debugLastCommand = MavPilotCommand.Neutral;
        public bool debugSignalAvailable;

        public override string CommandSourceName
        {
            get { return "Maverick mouse-flight instructor bridge"; }
        }

        public override bool IsOperationalCommandSource
        {
            get { return instructor != null && instructor.isActiveAndEnabled; }
        }

        public override bool HasCommandSignal
        {
            get { return instructor != null && instructor.isActiveAndEnabled; }
        }

        public override MavCommandSignalLossPolicy SignalLossPolicy
        {
            get { return MavCommandSignalLossPolicy.NeutralCommand; }
        }

        public override bool TryGetCommand(out MavPilotCommand command)
        {
            command = MavPilotCommand.Neutral;
            debugSignalAvailable = HasCommandSignal;

            if (!debugSignalAvailable)
            {
                debugLastCommand = command;
                return false;
            }

            // MouseFlight pitch uses the legacy convention where negative is normally nose-up.
            // MavPilotCommand uses the aircraft-conventional convention where +1 means nose-up.
            command.pitch = -instructor.pitch;
            command.roll = instructor.roll;
            command.yaw = instructor.yaw;
            command.throttle01 = ConvertThrottlePercentToFdm(instructor.throttlePercent);
            command = command.Clamped();

            debugLastCommand = command;
            return true;
        }

        /// <summary>
        /// Maps the legacy WT-style throttle axis onto the Garza/Morelli normalized engine command.
        /// Legacy 100% is military power; the sourced gearing reaches the military breakpoint near
        /// normalized throttle 0.77. Legacy 110% maps to the maximum normalized command.
        /// </summary>
        public static float ConvertThrottlePercentToFdm(float throttlePercent)
        {
            if (throttlePercent <= 0f)
                return 0f;

            if (throttlePercent <= 100f)
                return Mathf.Lerp(0f, 0.77f, Mathf.Clamp01(throttlePercent / 100f));

            return Mathf.Lerp(
                0.77f,
                1f,
                Mathf.InverseLerp(100f, 110f, throttlePercent));
        }
    }
}
