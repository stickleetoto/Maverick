using UnityEngine;

namespace MaverickFresh.FlightDynamics
{
    /// <summary>
    /// Zero-thrust propulsion model.
    ///
    /// This is the deliberate placeholder for any aircraft whose authoritative propulsion data
    /// is not yet frozen in the repository. It implements the full propulsion contract and the
    /// throttle/power-state plumbing so the rest of the pipeline can be wired, validated, and
    /// inspected, but it always returns zero force and zero moment.
    ///
    /// It reports <see cref="HasAuthoritativeData"/> = false so readiness reporting and telemetry
    /// state plainly that the aircraft currently has no thrust source. No F-16 thrust map,
    /// spool time constant, or installed-thrust figure is invented here.
    ///
    /// The optional first-order power-state lag is bookkeeping only: it never scales an output
    /// force, because the output force is structurally zero. Its time constant defaults to 0
    /// (instant) precisely because no sourced value exists.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MavNullPropulsionModel : MavPropulsionModelBase
    {
        [Header("Power-State Bookkeeping (no sourced data; output is always zero thrust)")]
        [Tooltip("First-order power-state lag in seconds. 0 = instant. This is unsourced plumbing, not an F-16 engine time constant.")]
        [Min(0f)] public float provisionalPowerLagSeconds = 0f;

        [Header("Debug")]
        public float debugCommandedThrottle01;
        public float debugPowerState01;

        public override string PropulsionModelName
        {
            get { return "Null propulsion (zero thrust, no frozen source data)"; }
        }

        public override bool HasAuthoritativeData
        {
            get { return false; }
        }

        public override MavPropulsiveLoads Evaluate(
            MavFlightState state,
            MavAtmosphereSample atmosphere,
            float throttle01,
            float deltaTime)
        {
            debugCommandedThrottle01 = Mathf.Clamp01(throttle01);
            debugPowerState01 = StepPowerState(
                debugPowerState01,
                debugCommandedThrottle01,
                provisionalPowerLagSeconds,
                deltaTime
            );

            return EvaluateZeroThrust(debugPowerState01);
        }

        public override void ResetEngineState(float throttle01)
        {
            debugCommandedThrottle01 = Mathf.Clamp01(throttle01);
            debugPowerState01 = debugCommandedThrottle01;
        }

        /// <summary>
        /// Pure zero-thrust load construction. Kept static so validation can assert that a null
        /// propulsion model contributes literally nothing at any power state, without needing a
        /// GameObject.
        /// </summary>
        public static MavPropulsiveLoads EvaluateZeroThrust(float powerState01)
        {
            MavPropulsiveLoads loads = MavPropulsiveLoads.Zero;
            loads.forceAeroBodyN = Vector3.zero;
            loads.momentAeroBodyNm = Vector3.zero;
            loads.reportedThrustN = 0f;
            loads.powerState01 = Mathf.Clamp01(powerState01);
            loads.hasAuthoritativeData = false;
            return loads;
        }

        /// <summary>
        /// Pure first-order power-state transition. Exposed for validation of the state plumbing.
        /// A non-positive time constant or non-positive dt snaps to the commanded value.
        /// </summary>
        public static float StepPowerState(float current, float commanded01, float lagSeconds, float deltaTime)
        {
            float target = Mathf.Clamp01(commanded01);
            if (lagSeconds <= 0f || deltaTime <= 0f)
                return target;

            float blend = Mathf.Clamp01(deltaTime / lagSeconds);
            return Mathf.Clamp01(Mathf.Lerp(current, target, blend));
        }
    }
}
