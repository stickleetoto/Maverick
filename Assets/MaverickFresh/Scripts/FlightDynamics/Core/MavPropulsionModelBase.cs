using UnityEngine;

namespace MaverickFresh.FlightDynamics
{
    /// <summary>
    /// Aircraft-independent propulsion model contract:
    ///
    ///   (state, atmosphere, throttle, dt) -> MavPropulsiveLoads
    ///
    /// A propulsion model owns throttle-to-power gearing, engine spool/lag dynamics, Mach and
    /// altitude dependence, and the thrust application point if a thrust-line offset is modeled.
    ///
    /// A propulsion model must NOT:
    ///   - touch Rigidbody (no AddForce / AddRelativeForce)
    ///   - compute aerodynamic loads
    ///   - apply gravity
    ///
    /// Data honesty rule for this branch: if the aircraft's authoritative thrust data is not
    /// frozen in the repository, the implementation returns zero loads and reports
    /// <see cref="HasAuthoritativeData"/> = false. Fabricating a plausible-looking engine curve
    /// and presenting it as reference data is not acceptable.
    /// </summary>
    public abstract class MavPropulsionModelBase : MonoBehaviour
    {
        /// <summary>Human-readable identity of the propulsion model, used by readiness reporting and telemetry.</summary>
        public abstract string PropulsionModelName { get; }

        /// <summary>
        /// False when this model has no frozen source data for the aircraft and is therefore
        /// producing zero thrust on purpose.
        /// </summary>
        public abstract bool HasAuthoritativeData { get; }

        /// <summary>
        /// Advances internal engine state by deltaTime and returns the resulting dimensional
        /// propulsive loads in conventional aircraft body axes.
        /// </summary>
        /// <param name="throttle01">Actual throttle from the actuator, 0..1.</param>
        public abstract MavPropulsiveLoads Evaluate(
            MavFlightState state,
            MavAtmosphereSample atmosphere,
            float throttle01,
            float deltaTime
        );

        /// <summary>Resets engine state to a defined condition. Called when ownership is (re)initialized.</summary>
        public abstract void ResetEngineState(float throttle01);
    }
}
