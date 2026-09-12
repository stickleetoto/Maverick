using UnityEngine;

namespace MaverickFresh.FlightDynamics.F16
{
    /// <summary>
    /// NASA/TM-2003-212145 (Garza &amp; Morelli) F-16 engine POWER-STATE dynamics, as the shared
    /// engine architecture's power-law strategy.
    ///
    /// SOURCED AIRCRAFT DATA in this file:
    ///   - throttle gearing, throttle 0..1 -> commanded power 0..100, military breakpoint at 0.77
    ///   - the piecewise actual-power derivative and its 50% power-state branches
    ///   - the reciprocal time-constant (RTAU) schedule
    ///
    /// MAVERICK INFRASTRUCTURE in this file:
    ///   - the explicit-Euler integration step. The sourced part of the model is the derivative; the
    ///     numerical method is ours and is not claimed to match the original MATLAB integrator.
    ///
    /// NOT established by this file:
    ///   - dimensional thrust. That comes from a thrust deck, and the F-16's is not frozen yet.
    ///   - anything about any other engine. This is F-16 REFERENCE-ENGINE behaviour. An F-15's
    ///     F100-family engine is a different engine with different data, and selecting this law for it
    ///     would be a claim nobody has proven. See ENG-006.
    ///
    /// This is the canonical implementation of these equations in the repository.
    /// <see cref="MavF16EnginePowerModel"/> forwards its static methods here rather than keeping a
    /// second copy, so the sourced constants exist exactly once and a change cannot be made to one
    /// path and missed on the other.
    ///
    /// Stateless, as <see cref="IMavEnginePowerDynamics"/> requires: the mutable power value belongs
    /// to <see cref="MavEngineRuntime"/>, which is what allows two engines sharing this law to hold
    /// independent power states.
    /// </summary>
    public sealed class MavF16GarzaMorelliEngineDynamics : IMavEnginePowerDynamics
    {
        public static readonly MavF16GarzaMorelliEngineDynamics Instance =
            new MavF16GarzaMorelliEngineDynamics();

        /// <summary>Throttle at which commanded power reaches the military-power breakpoint.</summary>
        public const float MilitaryPowerThrottleBreakpoint = 0.77f;

        public string DynamicsName
        {
            get { return "NASA/TM-2003-212145 Garza/Morelli F-16 power-state dynamics"; }
        }

        public float CommandedPowerPercent(float throttle01)
        {
            return ThrottleToCommandedPowerPercent(throttle01);
        }

        public float PowerRatePercentPerSec(float actualPowerPercent, float commandedPowerPercent)
        {
            return ComputePowerRatePercentPerSec(actualPowerPercent, commandedPowerPercent);
        }

        public float StepActualPowerPercent(
            float actualPowerPercent,
            float commandedPowerPercent,
            float deltaTime)
        {
            return StepActualPower(actualPowerPercent, commandedPowerPercent, deltaTime);
        }

        // ================================================================== sourced equations
        //
        // Static so the existing F-16 propulsion validation can exercise them directly, and so
        // MavF16EnginePowerModel can forward to them without instantiating anything.

        /// <summary>
        /// NASA/TM-2003-212145 throttle gearing.
        /// Military power is approximately throttle = 0.77 -> commanded power = 50.
        /// </summary>
        public static float ThrottleToCommandedPowerPercent(float throttle01)
        {
            float throttle = Mathf.Clamp01(throttle01);
            float command = throttle <= MilitaryPowerThrottleBreakpoint
                ? 64.94f * throttle
                : 217.38f * throttle - 117.38f;
            return Mathf.Clamp(command, 0f, 100f);
        }

        /// <summary>
        /// Reciprocal engine time constant (RTAU) from the Garza/Morelli F-16 model.
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
        /// The piecewise actual-power derivative, percent-power per second.
        ///
        /// The four branches are the sourced structure: whether commanded and actual power are each
        /// above or below the 50% military-power state selects both the target the engine moves
        /// toward and the rate factor it uses. The 60/40 targets are the model's own, not a smoothing
        /// choice of ours.
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
        /// Deterministic explicit-Euler state step. The derivative above is sourced; this integration
        /// is Maverick infrastructure, kept separate from any claim about the original integrator.
        /// </summary>
        public static float StepActualPower(
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
