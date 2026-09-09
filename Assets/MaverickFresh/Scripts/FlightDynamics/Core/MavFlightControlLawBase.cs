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
        [Tooltip("Phase 1 has no input binding. This is set by test code, the inspector, or a later instructor layer.")]
        public MavPilotCommand pilotCommand = MavPilotCommand.Neutral;

        [Header("Debug")]
        public MavControlInput debugLastOutput;
        public bool debugDroveActuator;
        public string debugStatus = "idle";

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

            debugLastOutput = Evaluate(
                sixDoFBody.debugState,
                sixDoFBody.debugAtmosphere,
                pilotCommand.Clamped(),
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
            debugStatus = ControlLawName + " driving actuator";
        }

        protected void ResolvePipeline()
        {
            if (sixDoFBody == null)
                sixDoFBody = GetComponent<MavSixDoFBody>();

            if (actuator == null)
                actuator = GetComponent<MavControlSurfaceActuatorBase>();
        }
    }
}
