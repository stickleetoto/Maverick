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

        [Header("Dimensional Thrust Data (separate from the sourced power dynamics)")]
        [Tooltip("Optional altitude/Mach/power thrust deck. NULL or Unavailable means dimensional thrust stays exactly zero, which is the current F-16 condition. Attaching a deck does not make its numbers authoritative - the deck declares that itself.")]
        public MavThrustDeckBase thrustDeck;

        [Header("Debug")]
        [Range(0f, 1f)] public float debugThrottle01;
        [Range(0f, 100f)] public float debugCommandedPowerPercent;
        public float debugPowerRatePercentPerSec;
        public bool debugThrustDeckAvailable = false;
        public MavThrustDeckResult debugLastThrustResult;
        public string debugThrustDataStatus = "no thrust deck: dimensional thrust unavailable";

        public override string PropulsionModelName
        {
            get
            {
                return thrustDeck != null
                    ? "F-16 Garza/Morelli power dynamics + " + thrustDeck.DeckName
                    : "F-16 Garza/Morelli power dynamics (thrust deck pending)";
            }
        }

        /// <summary>
        /// Reflects the DIMENSIONAL THRUST data only. The power-state dynamics are sourced
        /// independently and are reported by <see cref="HasAuthoritativePowerDynamics"/>.
        ///
        /// With no deck attached, or a deck that declares its data unavailable, this stays false and
        /// thrust stays exactly zero - the current F-16 condition on this branch.
        /// </summary>
        public override bool HasAuthoritativeData
        {
            get
            {
                return thrustDeck != null
                    && thrustDeck.Authority == MavThrustDataAuthority.Authoritative;
            }
        }

        /// <summary>
        /// Stricter than <see cref="HasAuthoritativeData"/>: also requires an envelope policy that
        /// keeps every returned number backed by the data. A deck configured to extrapolate is not
        /// acceptable for live flight even when its tables are authoritative.
        /// </summary>
        public override bool IsAcceptableForLiveFlight
        {
            get { return thrustDeck != null && thrustDeck.IsAcceptableForLiveFlight; }
        }

        public override string ThrustDataStatus
        {
            get { return debugThrustDataStatus; }
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

            return EvaluateDimensionalThrust(state, atmosphere, actualPowerPercent);
        }

        /// <summary>
        /// Turns the current power state into dimensional loads via the thrust deck.
        ///
        /// The split is deliberate: the power state above is sourced Garza/Morelli, the thrust below
        /// is whatever the deck can honestly supply. With no deck, or an unusable one, the result is
        /// exactly zero thrust reported as non-authoritative - never a plausible-looking guess.
        /// </summary>
        public MavPropulsiveLoads EvaluateDimensionalThrust(
            MavFlightState state,
            MavAtmosphereSample atmosphere,
            float powerPercent)
        {
            debugThrustDeckAvailable = thrustDeck != null
                                       && thrustDeck.Authority != MavThrustDataAuthority.Unavailable;

            if (!debugThrustDeckAvailable)
            {
                debugLastThrustResult = MavThrustDeckResult.Unavailable(
                    "no dimensional thrust deck: F-16 thrust remains unavailable");
                debugThrustDataStatus = debugLastThrustResult.statusReason;
                return BuildZeroThrustLoads(powerPercent);
            }

            debugLastThrustResult = thrustDeck.Evaluate(
                MavThrustDeckQuery.Create(state.worldPositionM.y, state.mach, powerPercent));

            debugThrustDataStatus = thrustDeck.Authority + " / " + debugLastThrustResult.statusReason;

            if (!debugLastThrustResult.valid)
                return BuildZeroThrustLoads(powerPercent);

            return BuildAxialThrustLoads(
                debugLastThrustResult.thrustN,
                powerPercent,
                debugLastThrustResult.authority == MavThrustDataAuthority.Authoritative);
        }

        /// <summary>
        /// Dimensional propulsive loads for a thrust acting along body X through the CG.
        ///
        /// v0.1 models no thrust-line offset, so the moment is exactly zero rather than an
        /// approximation. The authority flag rides along with the loads so downstream layers cannot
        /// lose track of where the number came from.
        /// </summary>
        public static MavPropulsiveLoads BuildAxialThrustLoads(
            float thrustN,
            float actualPowerPercent,
            bool authoritative)
        {
            MavPropulsiveLoads loads = MavPropulsiveLoads.Zero;
            loads.forceAeroBodyN = new Vector3(thrustN, 0f, 0f);
            loads.momentAeroBodyNm = Vector3.zero;
            loads.reportedThrustN = thrustN;
            loads.powerState01 = Mathf.Clamp01(actualPowerPercent * 0.01f);
            loads.hasAuthoritativeData = authoritative;
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

        /// <summary>
        /// Current safety boundary while the Morelli thrust deck is unavailable to this repository.
        /// Power dynamics advance, but dimensional thrust remains exactly zero and is explicitly
        /// marked non-authoritative.
        /// </summary>
        public static MavPropulsiveLoads BuildZeroThrustLoads(float actualPowerPercent)
        {
            MavPropulsiveLoads loads = MavPropulsiveLoads.Zero;
            loads.forceAeroBodyN = Vector3.zero;
            loads.momentAeroBodyNm = Vector3.zero;
            loads.reportedThrustN = 0f;
            loads.powerState01 = Mathf.Clamp01(actualPowerPercent * 0.01f);
            loads.hasAuthoritativeData = false;
            return loads;
        }
    }
}
