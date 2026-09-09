using System;
using UnityEngine;

namespace MaverickFresh.FlightDynamics
{
    /// <summary>
    /// Which steady-flight problem the trim solver is being asked to solve.
    ///
    /// The two modes differ in which quantity is an unknown and which is imposed:
    ///
    ///   PoweredSteadyFlight : flight-path angle is IMPOSED, thrust is the UNKNOWN.
    ///                         Alpha and elevator are solved from the normal-force and
    ///                         pitching-moment equations; the axial equation then *defines*
    ///                         the thrust the airframe would need. Whether the propulsion
    ///                         model can actually supply that thrust is reported separately
    ///                         and never assumed.
    ///
    ///   UnpoweredGlide      : thrust is IMPOSED at exactly zero, flight-path angle is the
    ///                         UNKNOWN. All three equations (axial, normal, pitch) are driven
    ///                         to zero simultaneously.
    ///
    /// The distinction exists so that an aircraft without a frozen thrust deck still has a
    /// physically well-posed trim problem, instead of a powered trim being forced to
    /// "converge" against a thrust source that does not exist.
    /// </summary>
    public enum MavTrimMode
    {
        PoweredSteadyFlight = 0,
        UnpoweredGlide = 1
    }

    /// <summary>
    /// Outcome of a trim solve. Every non-converged outcome is a distinct, reportable fact;
    /// none of them are silently mapped onto "converged".
    /// </summary>
    public enum MavTrimStatus
    {
        NotRun = 0,

        /// <summary>All driven residuals reached tolerance and the result is physically usable.</summary>
        Converged = 1,

        /// <summary>
        /// The aerodynamic trim (alpha, elevator) converged, but the axial equation requires more
        /// thrust than the propulsion model can supply. The attitude solution is valid; the
        /// *powered* steady flight condition is not achievable. This is the honest result for an
        /// aircraft whose thrust deck is not frozen.
        /// </summary>
        ConvergedButThrustUnavailable = 2,

        /// <summary>Iteration budget was exhausted before the residual reached tolerance.</summary>
        MaxIterationsExceeded = 3,

        /// <summary>No step could reduce the residual norm: the solver stalled, typically at a bound.</summary>
        Stalled = 4,

        /// <summary>The Jacobian was singular: an unknown has no effect on any residual.</summary>
        SingularJacobian = 5,

        /// <summary>A residual or state value became NaN/Infinity.</summary>
        NonFiniteResidual = 6,

        /// <summary>The requested condition is outside what this solver version models.</summary>
        UnsupportedCondition = 7,

        /// <summary>The plant description is incomplete or invalid.</summary>
        InvalidPlant = 8
    }

    /// <summary>
    /// The steady-flight condition being trimmed for. SI units, angles in degrees at this
    /// boundary because this type is inspector- and report-facing; all internal solver math
    /// works in radians.
    /// </summary>
    [Serializable]
    public struct MavTrimCondition
    {
        public MavTrimMode mode;

        [Tooltip("Geometric altitude in metres. Selects the atmosphere sample.")]
        public float altitudeM;

        [Tooltip("True airspeed in m/s. Held fixed by the solver.")]
        public float trueAirspeedMps;

        [Tooltip("Imposed flight-path angle in degrees for PoweredSteadyFlight. Ignored (solved for) in UnpoweredGlide.")]
        public float flightPathAngleDeg;

        [Tooltip("Bank angle in degrees. v0.1 solves symmetric wings-level trim only; a non-zero value is rejected, not approximated.")]
        public float bankAngleDeg;

        public static MavTrimCondition StraightAndLevel(float altitudeM, float trueAirspeedMps)
        {
            MavTrimCondition condition = new MavTrimCondition();
            condition.mode = MavTrimMode.PoweredSteadyFlight;
            condition.altitudeM = altitudeM;
            condition.trueAirspeedMps = trueAirspeedMps;
            condition.flightPathAngleDeg = 0f;
            condition.bankAngleDeg = 0f;
            return condition;
        }

        public static MavTrimCondition SteadyFlightPath(
            float altitudeM,
            float trueAirspeedMps,
            float flightPathAngleDeg)
        {
            MavTrimCondition condition = StraightAndLevel(altitudeM, trueAirspeedMps);
            condition.flightPathAngleDeg = flightPathAngleDeg;
            return condition;
        }

        public static MavTrimCondition UnpoweredGlide(float altitudeM, float trueAirspeedMps)
        {
            MavTrimCondition condition = new MavTrimCondition();
            condition.mode = MavTrimMode.UnpoweredGlide;
            condition.altitudeM = altitudeM;
            condition.trueAirspeedMps = trueAirspeedMps;
            condition.flightPathAngleDeg = 0f;
            condition.bankAngleDeg = 0f;
            return condition;
        }

        public bool IsSupported(out string reason)
        {
            if (float.IsNaN(trueAirspeedMps) || float.IsInfinity(trueAirspeedMps) || trueAirspeedMps <= 1f)
            {
                reason = "true airspeed must be finite and greater than 1 m/s";
                return false;
            }

            if (Mathf.Abs(bankAngleDeg) > 1e-3f)
            {
                reason = "v0.1 trims symmetric wings-level flight only; banked/turning trim is not implemented";
                return false;
            }

            if (float.IsNaN(altitudeM) || float.IsInfinity(altitudeM))
            {
                reason = "altitude is not finite";
                return false;
            }

            if (mode == MavTrimMode.PoweredSteadyFlight
                && (float.IsNaN(flightPathAngleDeg) || float.IsInfinity(flightPathAngleDeg)))
            {
                reason = "imposed flight-path angle is not finite";
                return false;
            }

            reason = "OK";
            return true;
        }
    }

    /// <summary>
    /// Trim equation residuals, in conventional aircraft body axes.
    ///
    /// Raw residuals are dimensional so they can be reasoned about physically. Normalized
    /// residuals are what the solver actually drives to zero, because a Newton iteration on
    /// mixed N and N*m quantities is otherwise dominated by whichever channel happens to have
    /// the larger numerical magnitude.
    ///
    /// Normalization:
    ///   force  residuals / (m * g0)              -> "fraction of aircraft weight"
    ///   moment residual  / (qbar * S * cbar)     -> back to a pitching-moment coefficient
    /// </summary>
    [Serializable]
    public struct MavTrimResidual
    {
        [Tooltip("Sum of body-X forces (aero + thrust + gravity component), N. Zero at trim.")]
        public float axialForceN;

        [Tooltip("Sum of body-Z forces (aero + gravity component), N. Zero at trim.")]
        public float normalForceN;

        [Tooltip("Sum of body pitching moments, N*m. Zero at trim.")]
        public float pitchMomentNm;

        [Tooltip("axialForceN / (m*g0): residual as a fraction of aircraft weight.")]
        public float axialForceNorm;

        [Tooltip("normalForceN / (m*g0): residual as a fraction of aircraft weight.")]
        public float normalForceNorm;

        [Tooltip("pitchMomentNm / (qbar*S*cbar): residual expressed back as a Cm error.")]
        public float pitchMomentNorm;

        [Tooltip("Largest absolute normalized residual among the channels the solver is actually driving.")]
        public float drivenNorm;

        public bool IsFinite()
        {
            return IsFiniteScalar(axialForceN)
                && IsFiniteScalar(normalForceN)
                && IsFiniteScalar(pitchMomentNm)
                && IsFiniteScalar(axialForceNorm)
                && IsFiniteScalar(normalForceNorm)
                && IsFiniteScalar(pitchMomentNorm);
        }

        private static bool IsFiniteScalar(float v)
        {
            return !float.IsNaN(v) && !float.IsInfinity(v);
        }
    }

    /// <summary>
    /// Full result of a trim solve. Deliberately verbose: a trim that actually worked and a trim
    /// that merely stopped iterating must be distinguishable from the result value alone,
    /// without reading a console log.
    /// </summary>
    [Serializable]
    public struct MavTrimResult
    {
        public MavTrimStatus status;

        [Tooltip("True only for MavTrimStatus.Converged. ConvergedButThrustUnavailable is NOT a converged powered trim.")]
        public bool converged;

        public MavTrimCondition condition;

        [Header("Solution")]
        public float alphaDeg;
        public float elevatorDeg;
        public float flightPathAngleDeg;
        public float pitchAttitudeDeg;

        [Header("Propulsion")]
        [Tooltip("Axial thrust the airframe needs to hold this condition, N. Positive means thrust is required.")]
        public float requiredThrustN;

        [Tooltip("Steady axial thrust the propulsion model reports at full throttle, N.")]
        public float availableThrustAtFullPowerN;

        [Tooltip("Throttle that supplies requiredThrustN. Only meaningful when throttleDetermined is true.")]
        public float throttle01;

        [Tooltip("False when the propulsion model cannot supply the required thrust, or has no thrust map at all.")]
        public bool throttleDetermined;

        [Tooltip("Mirrors MavPropulsionModelBase.HasAuthoritativeData for the model used in this solve.")]
        public bool propulsionDataAuthoritative;

        [Header("Convergence")]
        public int iterations;
        public MavTrimResidual residual;
        public bool hitAlphaBound;
        public bool hitElevatorBound;

        [TextArea(4, 20)] public string report;

        public static MavTrimResult Failed(MavTrimStatus status, MavTrimCondition condition, string report)
        {
            MavTrimResult result = new MavTrimResult();
            result.status = status;
            result.converged = false;
            result.condition = condition;
            result.throttle01 = float.NaN;
            result.throttleDetermined = false;
            result.report = report;
            return result;
        }
    }

    /// <summary>
    /// Deterministic solver configuration. Every field is a fixed number, not a heuristic:
    /// two runs with the same plant, condition and settings produce identical results.
    /// </summary>
    [Serializable]
    public struct MavTrimSolverSettings
    {
        [Min(1)] public int maxIterations;

        [Tooltip("Convergence threshold on the largest normalized residual.")]
        public float residualTolerance;

        [Tooltip("Central-difference step for the alpha unknown, degrees.")]
        public float alphaPerturbationDeg;

        [Tooltip("Central-difference step for the elevator unknown, degrees.")]
        public float elevatorPerturbationDeg;

        [Tooltip("Central-difference step for the flight-path-angle unknown, degrees.")]
        public float flightPathPerturbationDeg;

        [Tooltip("Maximum step halvings per iteration before the solver declares a stall.")]
        [Min(0)] public int maxLineSearchHalvings;

        [Header("Initial Guess")]
        public float initialAlphaDeg;
        public float initialElevatorDeg;
        public float initialFlightPathAngleDeg;

        [Header("Throttle Search")]
        [Tooltip("Bisection steps used to invert a monotone thrust-versus-throttle curve.")]
        [Min(1)] public int throttleBisectionSteps;

        public static MavTrimSolverSettings Default
        {
            get
            {
                MavTrimSolverSettings settings = new MavTrimSolverSettings();
                settings.maxIterations = 80;

                // 1e-5 normalized means "force residual within 1e-5 of the aircraft weight, and Cm
                // residual within 1e-5". On a 9-tonne airframe that is well under a Newton. It is
                // deliberately not tighter: the residuals are differences of ~1e5-magnitude
                // quantities in float, whose cancellation noise floor sits near 1e-7 normalized, so
                // a tighter tolerance would chase arithmetic noise and stall instead of converging.
                settings.residualTolerance = 1e-5f;

                // Central-difference steps, chosen well above float noise and well inside the range
                // where the second-order truncation error is negligible.
                settings.alphaPerturbationDeg = 1e-2f;
                settings.elevatorPerturbationDeg = 1e-2f;
                settings.flightPathPerturbationDeg = 1e-2f;
                settings.maxLineSearchHalvings = 20;
                settings.initialAlphaDeg = 2f;
                settings.initialElevatorDeg = 0f;
                settings.initialFlightPathAngleDeg = 0f;
                settings.throttleBisectionSteps = 60;
                return settings;
            }
        }
    }

    /// <summary>
    /// Pure aerodynamic evaluation for trim: (alpha, beta, surfaces) -> coefficients, at zero
    /// body rates. A trim is a steady state, so rate-dependent terms are evaluated at zero rate
    /// by construction and must not be smuggled in through this delegate.
    /// </summary>
    public delegate MavAeroCoefficients MavTrimAeroFunction(
        float alphaRad,
        float betaRad,
        MavControlInput surfaces
    );

    /// <summary>
    /// Pure STEADY-STATE propulsion evaluation for trim: (state, atmosphere, throttle) -> loads.
    ///
    /// This deliberately takes no deltaTime and must not advance engine spool state. A trim is a
    /// converged condition, so the engine sits at its settled power for the commanded throttle.
    /// Feeding a live component's transient spool state into a trim solve would make the answer
    /// depend on when it was run.
    /// </summary>
    public delegate MavPropulsiveLoads MavTrimSteadyPropulsionFunction(
        MavFlightState state,
        MavAtmosphereSample atmosphere,
        float throttle01
    );

    /// <summary>
    /// Everything the trim solver needs to know about an aircraft, with no Unity component,
    /// GameObject or Rigidbody involved.
    ///
    /// This is what makes the solver aircraft-independent: an aircraft supplies frozen data plus
    /// two pure functions and gets a trim back. It is also the structural reason the solver
    /// cannot modify a Rigidbody while solving - it holds no reference to one.
    /// </summary>
    public sealed class MavTrimPlant
    {
        public string plantId = "unconfigured";

        public MavAeroReferenceGeometry referenceGeometry;
        public float massKg;
        public MavControlSurfaceLimits controlSurfaceLimits;

        [Tooltip("Search bounds for the alpha unknown. Normally the published validity envelope of the aerodynamic model.")]
        public float alphaMinDeg = -10f;
        public float alphaMaxDeg = 45f;

        public MavTrimAeroFunction aeroFunction;

        [Tooltip("Optional. Null means a structurally unpowered airframe, which is a different statement from a powered airframe reporting zero thrust.")]
        public MavTrimSteadyPropulsionFunction steadyPropulsionFunction;

        [Tooltip("Mirrors the propulsion model's honesty flag so the trim report can repeat it.")]
        public bool propulsionDataAuthoritative;

        public bool IsValid(out string reason)
        {
            if (aeroFunction == null)
            {
                reason = "plant has no aerodynamic function";
                return false;
            }

            if (referenceGeometry.wingAreaM2 <= 0f
                || referenceGeometry.wingSpanM <= 0f
                || referenceGeometry.meanAerodynamicChordM <= 0f)
            {
                reason = "plant reference geometry is invalid";
                return false;
            }

            if (massKg <= 0f || float.IsNaN(massKg) || float.IsInfinity(massKg))
            {
                reason = "plant mass is invalid";
                return false;
            }

            if (alphaMaxDeg <= alphaMinDeg)
            {
                reason = "plant alpha search bounds are inverted";
                return false;
            }

            reason = "OK";
            return true;
        }
    }
}
