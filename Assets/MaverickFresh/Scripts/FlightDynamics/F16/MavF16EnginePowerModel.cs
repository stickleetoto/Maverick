using UnityEngine;

namespace MaverickFresh.FlightDynamics.F16
{
    /// <summary>
    /// NASA Garza/Morelli F-16 engine POWER-DYNAMICS model.
    ///
    /// Authoritative here:
    ///   - throttle gearing: throttle 0..1 -> commanded power 0..100
    ///   - commanded/actual power transition logic
    ///   - reciprocal time-constant schedule
    ///
    /// Not authoritative here:
    ///   - thrust deck T_idle/T_mil/T_max versus altitude and Mach.
    ///
    /// Until that thrust deck is frozen from an approved source, this component returns
    /// exactly zero force/moment while still advancing and exposing the sourced power state.
    /// That keeps the engine-state implementation useful without fabricating thrust.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MavF16EnginePowerModel : MavPropulsionModelBase
    {
        [Header("NASA Garza/Morelli engine state")]
        [Range(0f, 100f)] public float actualPowerPercent;

        [Header("Debug")]
        [Range(0f, 1f)] public float debugThrottle01;
        [Range(0f, 100f)] public float debugCommandedPowerPercent;
        public float debugPowerRatePercentPerSec;
        public bool debugThrustDeckAvailable = false;

        public override string PropulsionModelName
        {
            get { return "F-16 Garza/Morelli power dynamics (thrust deck pending)"; }
        }

        /// <summary>
        /// False until the altitude/Mach idle-military-maximum thrust tables are frozen.
        /// The power-state dynamics themselves are sourced; the dimensional thrust output is not.
        /// </summary>
        public override bool HasAuthoritativeData
        {
            get { return false; }
        }

        public bool HasAuthoritativePowerDynamics
        {
            get { return true; }
        }

        public override MavPropulsiveLoads Evaluate(
            MavFlightState state,
            MavAtmosphereSample atmosphere,
            float throttle01,
            float deltaTime)
        {
            debugThrottle01 = Mathf.Clamp01(throttle01);
            debugCommandedPowerPercent = ThrottleToCommandedPowerPercent(debugThrottle01);
            debugPowerRatePercentPerSec = ComputePowerRatePercentPerSec(
                actualPowerPercent,
                debugCommandedPowerPercent
            );

            actualPowerPercent = StepActualPowerPercent(
                actualPowerPercent,
                debugCommandedPowerPercent,
                deltaTime
            );

            // Deliberately zero until a sourced thrust deck is frozen.
            MavPropulsiveLoads loads = MavPropulsiveLoads.Zero;
            loads.forceAeroBodyN = Vector3.zero;
            loads.momentAeroBodyNm = Vector3.zero;
            loads.reportedThrustN = 0f;
            loads.powerState01 = Mathf.Clamp01(actualPowerPercent * 0.01f);
            loads.hasAuthoritativeData = false;
            return loads;
        }

        public override void ResetEngineState(float throttle01)
        {
            debugThrottle01 = Mathf.Clamp01(throttle01);
            debugCommandedPowerPercent = ThrottleToCommandedPowerPercent(debugThrottle01);
            actualPowerPercent = debugCommandedPowerPercent;
            debugPowerRatePercentPerSec = 0f;
        }

        /// <summary>
        /// NASA/TM-2003-212145 throttle gearing.
        /// Military-power breakpoint is approximately throttle=0.77 -> commanded power=50.
        /// </summary>
        public static float ThrottleToCommandedPowerPercent(float throttle01)
        {
            float throttle = Mathf.Clamp01(throttle01);
            float command = throttle <= 0.77f
                ? 64.94f * throttle
                : 217.38f * throttle - 117.38f;
            return Mathf.Clamp(command, 0f, 100f);
        }

        /// <summary>
        /// Reciprocal engine time constant from the Garza/Morelli F-16 model.
        /// Input is the positive power-state delta selected by the transition logic.
        /// </summary>
        public static float ReciprocalTimeConstant(float deltaPowerPercent)
        {
            float dp = deltaPowerPercent;
            if (dp <= 25f)
                return 1f;
            if (dp >= 50f)
                return 0.1f;
            return 1.9f - 0.036f * dp;
        }

        /// <summary>
        /// NASA Garza/Morelli piecewise actual-power derivative, percent-power per second.
        /// </summary>
        public static float ComputePowerRatePercentPerSec(
            float actualPowerPercent,
            float commandedPowerPercent)
        {
            float actual = Mathf.Clamp(actualPowerPercent, 0f, 100f);
            float commanded = Mathf.Clamp(commandedPowerPercent, 0f, 100f);

            float target;
            float rateFactor;

            if (commanded >= 50f)
            {
                if (actual >= 50f)
                {
                    target = commanded;
                    rateFactor = 5f;
                }
                else
                {
                    target = 60f;
                    rateFactor = ReciprocalTimeConstant(target - actual);
                }
            }
            else
            {
                if (actual >= 50f)
                {
                    target = 40f;
                    rateFactor = 5f;
                }
                else
                {
                    target = commanded;
                    rateFactor = ReciprocalTimeConstant(target - actual);
                }
            }

            return rateFactor * (target - actual);
        }

        /// <summary>
        /// Deterministic explicit-Euler state step. The derivative is the sourced model;
        /// the numerical integration method is Maverick infrastructure and is intentionally
        /// kept separate from any claim about the original MATLAB integrator.
        /// </summary>
        public static float StepActualPowerPercent(
            float actualPowerPercent,
            float commandedPowerPercent,
            float deltaTime)
        {
            float actual = Mathf.Clamp(actualPowerPercent, 0f, 100f);
            if (deltaTime <= 0f)
                return actual;

            float rate = ComputePowerRatePercentPerSec(actual, commandedPowerPercent);
            return Mathf.Clamp(actual + rate * deltaTime, 0f, 100f);
        }
    }
}
