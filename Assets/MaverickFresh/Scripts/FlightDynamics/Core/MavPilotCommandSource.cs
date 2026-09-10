using UnityEngine;

namespace MaverickFresh.FlightDynamics
{
    /// <summary>
    /// What a control law does when an operational command source stops producing commands.
    ///
    /// The policy is declared by the source, not chosen by the law, because only the source knows
    /// what its own dropouts mean. A device that briefly misses a poll wants the last command held;
    /// a link that has genuinely gone away wants the controls centred.
    ///
    /// Neither policy is "keep flying on whatever is in the inspector". That fallback is a bench
    /// affordance and must never become an implicit live-flight behaviour.
    /// </summary>
    public enum MavCommandSignalLossPolicy
    {
        /// <summary>
        /// Centre pitch, roll and yaw; hold the last known throttle.
        ///
        /// Throttle is held rather than zeroed deliberately: chopping to idle is a far larger
        /// disturbance than centring the stick, and this control-law layer does not own thrust
        /// management. An engine-side response to signal loss belongs to the propulsion path.
        /// </summary>
        NeutralCommand = 0,

        /// <summary>Hold the entire last valid command, unchanged.</summary>
        HoldLastCommand = 1
    }

    /// <summary>
    /// Contract for whatever produces normalized pilot intent for the new FDM path.
    ///
    /// This is the socket a real command producer plugs into: a device binding, an autopilot, an
    /// AI pilot, or - later, and explicitly not in this phase - a War-Thunder-style mouse
    /// instructor. It exists because operational live-readiness has to be able to ask "is there a
    /// valid command source, and is it producing right now?", and a public inspector field on a
    /// control law is not an answer to either question.
    ///
    /// A command source produces intent only. It must never touch a Rigidbody, compute a force or
    /// moment, or write a control-surface deflection.
    ///
    /// The two readiness questions are deliberately separate properties:
    ///
    ///   <see cref="IsOperationalCommandSource"/> is a DECLARATION about the kind of path this is.
    ///   It does not change from step to step.
    ///
    ///   <see cref="HasCommandSignal"/> is LIVE STATE: is a command actually arriving this step?
    ///
    /// Collapsing the two lets a source that is nominally operational drop its signal while
    /// readiness still reports the stack fit to fly. Operational live-readiness requires both.
    /// </summary>
    public abstract class MavPilotCommandSourceBase : MonoBehaviour
    {
        /// <summary>Human-readable identity, used by readiness reporting so a test rig is never mistaken for a real input path.</summary>
        public abstract string CommandSourceName { get; }

        /// <summary>
        /// Returns the current pilot intent. Returning false means "no command available right
        /// now", and implementations MUST keep that consistent with <see cref="HasCommandSignal"/>:
        /// if this returns false, that property must be false too.
        /// </summary>
        public abstract bool TryGetCommand(out MavPilotCommand command);

        /// <summary>
        /// Whether this source is, by declaration, fit to fly the aircraft operationally.
        ///
        /// Defaults to false on purpose. A source has to claim this deliberately; a debug or test
        /// source that simply exposes a struct in the inspector is not a valid operational command
        /// source, and operational live-readiness must not be satisfiable by adding a component.
        ///
        /// This is a statement about the KIND of path, not about whether a command arrived this
        /// step. Moment-to-moment availability is <see cref="HasCommandSignal"/>.
        /// </summary>
        public virtual bool IsOperationalCommandSource
        {
            get { return false; }
        }

        /// <summary>
        /// Whether a command is actually arriving right now.
        ///
        /// Defaults to false so a source that does not answer the question fails closed: an
        /// unanswered "am I producing?" must never be read as yes.
        /// </summary>
        public virtual bool HasCommandSignal
        {
            get { return false; }
        }

        /// <summary>
        /// What the control law should do if this source stops producing. Declared by the source;
        /// the safest option is the default.
        /// </summary>
        public virtual MavCommandSignalLossPolicy SignalLossPolicy
        {
            get { return MavCommandSignalLossPolicy.NeutralCommand; }
        }

        /// <summary>
        /// The readiness rule for a command path, in one place: a source counts toward operational
        /// live-readiness only when it declares itself an operational path AND is currently
        /// producing commands. Pure, so validation can exercise it without a component.
        /// </summary>
        public static bool EvaluatesAsLiveCommandPath(bool isOperationalPath, bool hasSignal)
        {
            return isOperationalPath && hasSignal;
        }

        /// <summary>
        /// The whole command-path rule, including identity.
        ///
        /// Every clause is required, and the identity clause is the one that is easy to omit: the
        /// declaration is read off one object and the observed availability off another, so without
        /// proving they are the SAME object a "valid" path can be assembled from two halves that
        /// never met - source A declares itself operational while the control law happily flies on
        /// source B. Unknown or mismatched states fail closed.
        ///
        /// Pure, so validation can enumerate every miswiring without a scene.
        /// </summary>
        public static bool EvaluatesAsLiveCommandPipeline(
            bool bodySourceExists,
            bool controlLawExists,
            bool sourceIdentityMatches,
            bool sourceEnabled,
            bool declaresOperationalCapability,
            bool observedSourceSignalThisStep)
        {
            return bodySourceExists
                && controlLawExists
                && sourceIdentityMatches
                && sourceEnabled
                && EvaluatesAsLiveCommandPath(
                    declaresOperationalCapability,
                    observedSourceSignalThisStep);
        }

        /// <summary>
        /// The command a control law must use when an operational source drops its signal.
        /// Pure and policy-driven; note that no branch of this returns an inspector value.
        /// </summary>
        public static MavPilotCommand ResolveOnSignalLoss(
            MavCommandSignalLossPolicy policy,
            MavPilotCommand lastValidCommand)
        {
            MavPilotCommand last = lastValidCommand.Clamped();

            if (policy == MavCommandSignalLossPolicy.HoldLastCommand)
                return last;

            MavPilotCommand neutral = MavPilotCommand.Neutral;
            neutral.throttle01 = last.throttle01;
            return neutral;
        }
    }

    /// <summary>
    /// Inspector- and script-driven command source for development and deterministic testing.
    ///
    /// This is the Phase 2 stand-in for a real input path. It reports itself as an operational path
    /// only when <see cref="treatAsOperationalSource"/> is explicitly set, which is a deliberate
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
        [Tooltip("Live signal state. When false, TryGetCommand returns false and HasCommandSignal is false, so an operational stack immediately loses live-readiness. Used by validation to simulate a dropout.")]
        public bool commandAvailable = true;

        [Tooltip("Deliberate operator acknowledgement that this manual source is an operational input path. OFF by default: a test rig is not an operational input path. This is a declaration about the kind of path, not about whether a command arrived this step.")]
        public bool treatAsOperationalSource = DefaultTreatAsOperationalSource;

        [Tooltip("What a control law should do if this source stops producing commands.")]
        public MavCommandSignalLossPolicy signalLossPolicy = MavCommandSignalLossPolicy.NeutralCommand;

        public override string CommandSourceName
        {
            get { return "Manual / test pilot command source"; }
        }

        public override bool IsOperationalCommandSource
        {
            get { return treatAsOperationalSource; }
        }

        public override bool HasCommandSignal
        {
            get { return commandAvailable; }
        }

        public override MavCommandSignalLossPolicy SignalLossPolicy
        {
            get { return signalLossPolicy; }
        }

        public override bool TryGetCommand(out MavPilotCommand pilotCommand)
        {
            pilotCommand = command.Clamped();
            return commandAvailable;
        }
    }
}
