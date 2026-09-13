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
    ///
    /// TRANSITIONAL as of the shared-propulsion migration (P0).
    ///
    /// The equations no longer live here. They are in
    /// <see cref="MavF16GarzaMorelliEngineDynamics"/>, which is the canonical implementation and the
    /// strategy the shared <see cref="MavEngineRuntime"/> calls; the static methods below forward to
    /// it so the sourced constants exist exactly once in the repository. That matters more than the
    /// small indirection: two copies of a gearing breakpoint is how one path gets fixed and the other
    /// does not.
    ///
    /// This component is retained because several existing suites and the F-16 auto-setup reference it
    /// directly, and because it is a single-engine aircraft where the two designs are numerically
    /// identical. The replacement path is
    /// <see cref="MavPropulsionSystem"/> + <see cref="MavF16PropulsionInstallation"/>, which is what a
    /// multi-engine aircraft must use. Deleting this class is a later, separate step.
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
            return MavF16GarzaMorelliEngineDynamics.ThrottleToCommandedPowerPercent(throttle01);
        }

        /// <summary>
        /// Reciprocal engine time constant from the Garza/Morelli F-16 model.
        /// Input is the positive power-state delta selected by the transition logic.
        /// </summary>
        public static float ReciprocalTimeConstant(float deltaPowerPercent)
        {
            return MavF16GarzaMorelliEngineDynamics.ReciprocalTimeConstant(deltaPowerPercent);
        }

        /// <summary>
        /// NASA Garza/Morelli piecewise actual-power derivative, percent-power per second.
        /// </summary>
        public static float ComputePowerRatePercentPerSec(
            float actualPowerPercent,
            float commandedPowerPercent)
        {
            return MavF16GarzaMorelliEngineDynamics.ComputePowerRatePercentPerSec(
                actualPowerPercent, commandedPowerPercent);
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
            return MavF16GarzaMorelliEngineDynamics.StepActualPower(
                actualPowerPercent, commandedPowerPercent, deltaTime);
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
