using UnityEngine;

namespace MaverickFresh.FlightDynamics
{
    /// <summary>
    /// Contract for whatever produces normalized pilot intent for the new FDM path.
    ///
    /// This is the socket a real command producer plugs into: a device binding, an autopilot, an
    /// AI pilot, or - later, and explicitly not in this phase - a War-Thunder-style mouse
    /// instructor. It exists now because operational live-readiness has to be able to ask
    /// "is there a valid command source?", and a public inspector field on a control law is not
    /// an answer to that question.
    ///
    /// A command source produces intent only. It must never touch a Rigidbody, compute a force or
    /// moment, or write a control-surface deflection.
    /// </summary>
    public abstract class MavPilotCommandSourceBase : MonoBehaviour
    {
        /// <summary>Human-readable identity, used by readiness reporting so a test rig is never mistaken for a real input path.</summary>
        public abstract string CommandSourceName { get; }

        /// <summary>
        /// Returns the current pilot intent. Returning false means "no command available right
        /// now", which a control law must treat as a reason to hold its own fallback, not as a
        /// neutral command.
        /// </summary>
        public abstract bool TryGetCommand(out MavPilotCommand command);

        /// <summary>
        /// Whether this source is fit to fly the aircraft operationally.
        ///
        /// Defaults to false on purpose. A source has to claim this deliberately; a debug or test
        /// source that simply exposes a struct in the inspector is not a valid operational command
        /// source, and operational live-readiness must not be satisfiable by adding a component.
        /// </summary>
        public virtual bool IsOperationalCommandSource
        {
            get { return false; }
        }
    }

    /// <summary>
    /// Inspector- and script-driven command source for development and deterministic testing.
    ///
    /// This is the Phase 2 stand-in for a real input path. It reports itself as operational only
    /// when <see cref="treatAsOperationalSource"/> is explicitly set, which is a deliberate
    /// operator action, not a default.
    ///
    /// It is NOT the War-Thunder mouse instructor and contains no aiming, no target-direction
    /// logic and no velocity-vector alignment.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MavManualPilotCommandSource : MavPilotCommandSourceBase
    {
        /// <summary>
        /// The shipped default for <see cref="treatAsOperationalSource"/>. Exposed as a constant so
        /// validation can assert the default without instantiating a MonoBehaviour.
        /// </summary>
        public const bool DefaultTreatAsOperationalSource = false;

        [Header("Normalized Pilot Intent")]
        public MavPilotCommand command = MavPilotCommand.Neutral;

        [Header("Availability")]
        [Tooltip("When false, TryGetCommand returns false and the control law falls back to its own inspector command.")]
        public bool commandAvailable = true;

        [Tooltip("Deliberate operator acknowledgement that this manual source may satisfy the operational command-source readiness criterion. OFF by default: a test rig is not an operational input path.")]
        public bool treatAsOperationalSource = DefaultTreatAsOperationalSource;

        public override string CommandSourceName
        {
            get { return "Manual / test pilot command source"; }
        }

        public override bool IsOperationalCommandSource
        {
            get { return EvaluatesAsOperationalSource(treatAsOperationalSource, commandAvailable); }
        }

        /// <summary>
        /// The operational-source rule, as a pure function. A manual source counts as operational
        /// only when an operator has said so AND it is actually producing commands.
        /// </summary>
        public static bool EvaluatesAsOperationalSource(bool treatAsOperational, bool commandAvailable)
        {
            return treatAsOperational && commandAvailable;
        }

        public override bool TryGetCommand(out MavPilotCommand pilotCommand)
        {
            pilotCommand = command.Clamped();
            return commandAvailable;
        }
    }
}
