using UnityEngine;

namespace MaverickFresh.FlightDynamics
{
    /// <summary>
    /// Aircraft-independent flight control law contract:
    ///
    ///   (state, atmosphere, pilotCommand, profile) -> MavControlInput
    ///
    /// A control law shapes normalized pilot intent into physical control-surface demand.
    /// It may implement rate/G/AoA protection and control allocation.
    ///
    /// A control law must NOT:
    ///   - touch Rigidbody (no AddForce / AddTorque / velocity writes)
    ///   - compute aerodynamic forces or moments
    ///   - apply dynamic pressure or dimensionalize coefficients
    ///
    /// Naming policy: a control law may only be described as a real aircraft flight-control
    /// system if it is actually sourced from and validated against that system. Everything
    /// implemented from Maverick-side tuning is a "Maverick control law".
    ///
    /// Execution order: this base drives the pipeline at -300, ahead of the actuator (-200)
    /// and <see cref="MavSixDoFBody"/> (-100), so within one physics step the ordering is
    /// law -> actuator -> aerodynamics -> load application. The state the law reads is the
    /// state sampled by the body during the previous step, i.e. one fixed step of sensor
    /// latency. That is deterministic and intentional, not an accident of ordering.
    /// </summary>
    [DefaultExecutionOrder(-300)]
    public abstract class MavFlightControlLawBase : MonoBehaviour
    {
        [Header("Pipeline")]
        [Tooltip("Source of flight state / atmosphere / physical profile. Resolved from this GameObject when empty.")]
        public MavSixDoFBody sixDoFBody;

        [Tooltip("Actuator that receives the computed physical surface demand. Resolved from this GameObject when empty.")]
        public MavControlSurfaceActuatorBase actuator;

        [Tooltip("When true this component evaluates the law each FixedUpdate and pushes the result to the actuator.")]
        public bool driveActuatorInFixedUpdate = true;

        [Header("Normalized Pilot Intent")]
        [Tooltip("Optional command producer. When present and reporting a command, it overrides the inspector field below. Resolved from this GameObject when empty.")]
        public MavPilotCommandSourceBase commandSource;

        [Tooltip("BENCH INPUT ONLY. Used when no command source is wired, or when a source that does not claim to be an operational path has nothing to say. It is never used to cover the loss of an operational signal - that case applies the source's declared signal-loss policy.")]
        public MavPilotCommand pilotCommand = MavPilotCommand.Neutral;

        [Header("Debug")]
        public MavControlInput debugLastOutput;
        public MavPilotCommand debugLastCommand;
        public bool debugUsingCommandSource;

        [Tooltip("Whether a command source actually produced a command this step.")]
        public bool debugCommandSignalAvailable;

        [Tooltip("Where this step's command came from.")]
        public MavCommandResolution debugCommandResolution = MavCommandResolution.NoSource;

        [Tooltip("Last command an operational source actually supplied. Feeds the signal-loss policy; never sourced from the inspector field.")]
        public MavPilotCommand debugLastValidSourceCommand = MavPilotCommand.Neutral;

        [Tooltip("How many times an operational source has dropped its signal. A dropout must be countable, not merely survivable.")]
        public int debugCommandSignalLossEvents;

        public bool debugDroveActuator;
        public string debugStatus = "idle";

        private bool wasInSignalLoss;

        /// <summary>
        /// Human-readable identity of this control law. Used by readiness reporting and telemetry
        /// so an unsourced test law can never be mistaken for a validated aircraft controller.
        /// </summary>
        public abstract string ControlLawName { get; }

        /// <summary>
        /// Pure evaluation. Implementations must not mutate world state, Rigidbody, or the actuator.
        /// </summary>
        /// <param name="profile">May be null before the physical profile has been built.</param>
        public abstract MavControlInput Evaluate(
            MavFlightState state,
            MavAtmosphereSample atmosphere,
            MavPilotCommand command,
            MavFlightDynamicsProfile profile,
            float deltaTime
        );

        protected virtual void OnEnable()
        {
            ResolvePipeline();

            // A control law must not inherit a command from a previous session. In particular the
            // signal-loss policy must not be able to "hold" a command captured before this enable.
            debugLastValidSourceCommand = MavPilotCommand.Neutral;
            debugCommandResolution = MavCommandResolution.NoSource;
            debugCommandSignalAvailable = false;
            wasInSignalLoss = false;
        }

        protected virtual void FixedUpdate()
        {
            ResolvePipeline();
            debugDroveActuator = false;

            if (!driveActuatorInFixedUpdate)
            {
                debugStatus = "manual mode: not driving actuator";
                return;
            }

            if (sixDoFBody == null)
            {
                debugStatus = "no MavSixDoFBody: cannot read flight state";
                return;
            }

            debugLastCommand = ResolveCommand();

            debugLastOutput = Evaluate(
                sixDoFBody.debugState,
                sixDoFBody.debugAtmosphere,
                debugLastCommand,
                sixDoFBody.debugProfileValid ? sixDoFBody.activeProfile : null,
                Time.fixedDeltaTime
            );

            if (actuator == null)
            {
                debugStatus = "no actuator: surface demand computed but not published";
                return;
            }

            actuator.SetCommand(debugLastOutput);
            debugDroveActuator = true;
            debugStatus = ControlLawName + " driving actuator | command=" + debugCommandResolution
                + (debugCommandResolution == MavCommandResolution.SignalLossPolicy
                    ? " (" + commandSource.SignalLossPolicy + ")"
                    : string.Empty);
        }

        protected void ResolvePipeline()
        {
            if (sixDoFBody == null)
                sixDoFBody = GetComponent<MavSixDoFBody>();

            if (actuator == null)
                actuator = GetComponent<MavControlSurfaceActuatorBase>();

            if (commandSource == null)
                commandSource = GetComponent<MavPilotCommandSourceBase>();
        }

        /// <summary>
        /// How this step's pilot intent was obtained. Reported so a dropout is visible rather than
        /// inferred from the aircraft's behaviour.
        /// </summary>
        public enum MavCommandResolution
        {
            /// <summary>No command source is wired: bench mode, using the inspector field.</summary>
            NoSource = 0,

            /// <summary>A source supplied a command this step.</summary>
            SourceSignal = 1,

            /// <summary>An OPERATIONAL source produced nothing, so its declared signal-loss policy applied.</summary>
            SignalLossPolicy = 2,

            /// <summary>A non-operational (bench) source produced nothing, so the inspector field applied.</summary>
            BenchInspectorFallback = 3
        }

        /// <summary>
        /// Decides where this step's command comes from. Pure, so the rule can be exercised
        /// exhaustively without a component.
        ///
        /// The case that matters is an operational path with no signal. That must resolve to the
        /// declared signal-loss policy, NEVER to the inspector field: an inspector value silently
        /// becoming the live command during a dropout is a control-path failure disguised as an
        /// aircraft that still seems to fly.
        /// </summary>
        public static MavCommandResolution ClassifyCommandResolution(
            bool hasSource,
            bool sourceEnabled,
            bool gotSignal,
            bool sourceIsOperationalPath)
        {
            if (!hasSource || !sourceEnabled)
                return MavCommandResolution.NoSource;

            if (gotSignal)
                return MavCommandResolution.SourceSignal;

            return sourceIsOperationalPath
                ? MavCommandResolution.SignalLossPolicy
                : MavCommandResolution.BenchInspectorFallback;
        }

        /// <summary>
        /// Picks this step's normalized pilot intent.
        ///
        /// Inspector input is a BENCH affordance. It is used when there is no command source at
        /// all, or when a source that does not claim to be an operational path has nothing to say.
        /// It is never used to paper over the loss of an operational signal - that case applies the
        /// source's declared policy and is counted, so the dropout is observable.
        /// </summary>
        protected MavPilotCommand ResolveCommand()
        {
            bool hasSource = commandSource != null;
            bool sourceEnabled = hasSource && commandSource.isActiveAndEnabled;

            MavPilotCommand sourced = MavPilotCommand.Neutral;
            bool gotSignal = sourceEnabled && commandSource.TryGetCommand(out sourced);
            bool operationalPath = sourceEnabled && commandSource.IsOperationalCommandSource;

            debugCommandResolution = ClassifyCommandResolution(
                hasSource, sourceEnabled, gotSignal, operationalPath);

            debugUsingCommandSource = debugCommandResolution == MavCommandResolution.SourceSignal;
            debugCommandSignalAvailable = gotSignal;

            switch (debugCommandResolution)
            {
                case MavCommandResolution.SourceSignal:
                    debugLastValidSourceCommand = sourced.Clamped();
                    wasInSignalLoss = false;
                    return debugLastValidSourceCommand;

                case MavCommandResolution.SignalLossPolicy:
                    if (!wasInSignalLoss)
                    {
                        wasInSignalLoss = true;
                        debugCommandSignalLossEvents++;
                        Debug.LogWarning(
                            "[Maverick/FDM] Operational command source '"
                            + commandSource.CommandSourceName
                            + "' stopped producing commands; applying its declared "
                            + commandSource.SignalLossPolicy
                            + " policy. Inspector input is NOT used as a live fallback.",
                            this);
                    }
                    return MavPilotCommandSourceBase.ResolveOnSignalLoss(
                        commandSource.SignalLossPolicy, debugLastValidSourceCommand);

                default:
                    wasInSignalLoss = false;
                    return pilotCommand.Clamped();
            }
        }
    }
}
