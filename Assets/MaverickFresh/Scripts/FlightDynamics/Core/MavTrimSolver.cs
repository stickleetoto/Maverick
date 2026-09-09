using System.Text;
using UnityEngine;

namespace MaverickFresh.FlightDynamics
{
    /// <summary>Residual function for the generic solver: unknowns in, residuals out.</summary>
    public delegate void MavNewtonResidualFunction(float[] unknowns, float[] residuals);

    /// <summary>Termination reason for the generic damped-Newton iteration.</summary>
    public enum MavNewtonOutcome
    {
        Converged = 0,
        MaxIterationsExceeded = 1,
        Stalled = 2,
        SingularJacobian = 3,
        NonFiniteResidual = 4
    }

    /// <summary>
    /// Small, deterministic, bounded damped-Newton solver for square nonlinear systems.
    ///
    /// Deliberately boring:
    ///   - central-difference Jacobian with fixed per-unknown steps (one-sided at a bound)
    ///   - Gaussian elimination with partial pivoting
    ///   - step halving line search that only accepts a strict decrease in the residual norm
    ///   - hard box bounds on every unknown
    ///
    /// There is no randomness, no wall-clock input and no adaptive heuristic, so the same inputs
    /// always produce the same iterates. It touches no Unity object of any kind: it is given
    /// arrays and a function, and returns arrays.
    ///
    /// The residual norm used throughout is the max-abs norm, so the convergence tolerance means
    /// "every residual channel is within tolerance", not "the channels average out".
    /// </summary>
    public static class MavDampedNewtonSolver
    {
        public static MavNewtonOutcome Solve(
            int unknownCount,
            MavNewtonResidualFunction residualFunction,
            float[] unknowns,
            float[] lowerBounds,
            float[] upperBounds,
            float[] perturbations,
            int maxIterations,
            float tolerance,
            int maxLineSearchHalvings,
            out int iterations,
            out float residualNorm)
        {
            iterations = 0;
            residualNorm = float.PositiveInfinity;

            int n = unknownCount;
            float[] residual = new float[n];
            float[] trialResidual = new float[n];
            float[] trialUnknowns = new float[n];
            float[] plusResidual = new float[n];
            float[] minusResidual = new float[n];
            float[] probe = new float[n];
            float[] jacobian = new float[n * n];
            float[] step = new float[n];

            ClampInPlace(unknowns, lowerBounds, upperBounds, n);
            residualFunction(unknowns, residual);
            if (!IsFinite(residual, n))
                return MavNewtonOutcome.NonFiniteResidual;

            residualNorm = MaxAbs(residual, n);

            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                if (residualNorm <= tolerance)
                {
                    iterations = iteration;
                    return MavNewtonOutcome.Converged;
                }

                iterations = iteration + 1;

                // --- Jacobian by finite differences, staying inside the box bounds ---
                for (int column = 0; column < n; column++)
                {
                    float h = Mathf.Abs(perturbations[column]);
                    if (h <= 0f)
                        h = 1e-4f;

                    float x0 = unknowns[column];
                    float xPlus = Mathf.Min(upperBounds[column], x0 + h);
                    float xMinus = Mathf.Max(lowerBounds[column], x0 - h);
                    float span = xPlus - xMinus;

                    if (span <= 0f)
                    {
                        // The unknown is pinned by its own bounds: it cannot influence anything.
                        for (int row = 0; row < n; row++)
                            jacobian[row * n + column] = 0f;
                        continue;
                    }

                    CopyInto(unknowns, probe, n);
                    probe[column] = xPlus;
                    residualFunction(probe, plusResidual);

                    CopyInto(unknowns, probe, n);
                    probe[column] = xMinus;
                    residualFunction(probe, minusResidual);

                    if (!IsFinite(plusResidual, n) || !IsFinite(minusResidual, n))
                        return MavNewtonOutcome.NonFiniteResidual;

                    float inverseSpan = 1f / span;
                    for (int row = 0; row < n; row++)
                        jacobian[row * n + column] = (plusResidual[row] - minusResidual[row]) * inverseSpan;
                }

                // --- solve J * step = -residual ---
                for (int i = 0; i < n; i++)
                    step[i] = -residual[i];

                if (!SolveLinearSystemInPlace(jacobian, step, n))
                    return MavNewtonOutcome.SingularJacobian;

                if (!IsFinite(step, n))
                    return MavNewtonOutcome.NonFiniteResidual;

                // --- damped line search: accept only a strict decrease ---
                bool accepted = false;
                float scale = 1f;
                for (int halving = 0; halving <= maxLineSearchHalvings; halving++)
                {
                    for (int i = 0; i < n; i++)
                        trialUnknowns[i] = unknowns[i] + step[i] * scale;

                    ClampInPlace(trialUnknowns, lowerBounds, upperBounds, n);
                    residualFunction(trialUnknowns, trialResidual);

                    if (IsFinite(trialResidual, n))
                    {
                        float trialNorm = MaxAbs(trialResidual, n);
                        if (trialNorm < residualNorm)
                        {
                            CopyInto(trialUnknowns, unknowns, n);
                            CopyInto(trialResidual, residual, n);
                            residualNorm = trialNorm;
                            accepted = true;
                            break;
                        }
                    }

                    scale *= 0.5f;
                }

                if (!accepted)
                {
                    // Already inside tolerance is a converged stall, not a failure.
                    return residualNorm <= tolerance
                        ? MavNewtonOutcome.Converged
                        : MavNewtonOutcome.Stalled;
                }
            }

            return residualNorm <= tolerance
                ? MavNewtonOutcome.Converged
                : MavNewtonOutcome.MaxIterationsExceeded;
        }

        /// <summary>
        /// Gaussian elimination with partial pivoting. Overwrites both matrix and rhs.
        /// Returns false when the matrix is numerically singular, which for a trim problem means
        /// an unknown has no authority over any residual.
        /// </summary>
        public static bool SolveLinearSystemInPlace(float[] matrix, float[] rhs, int n)
        {
            for (int pivotIndex = 0; pivotIndex < n; pivotIndex++)
            {
                int bestRow = pivotIndex;
                float bestMagnitude = Mathf.Abs(matrix[pivotIndex * n + pivotIndex]);
                for (int row = pivotIndex + 1; row < n; row++)
                {
                    float magnitude = Mathf.Abs(matrix[row * n + pivotIndex]);
                    if (magnitude > bestMagnitude)
                    {
                        bestMagnitude = magnitude;
                        bestRow = row;
                    }
                }

                if (bestMagnitude <= 1e-12f)
                    return false;

                if (bestRow != pivotIndex)
                {
                    for (int column = 0; column < n; column++)
                    {
                        float swap = matrix[pivotIndex * n + column];
                        matrix[pivotIndex * n + column] = matrix[bestRow * n + column];
                        matrix[bestRow * n + column] = swap;
                    }

                    float swapRhs = rhs[pivotIndex];
                    rhs[pivotIndex] = rhs[bestRow];
                    rhs[bestRow] = swapRhs;
                }

                float pivot = matrix[pivotIndex * n + pivotIndex];
                for (int row = pivotIndex + 1; row < n; row++)
                {
                    float factor = matrix[row * n + pivotIndex] / pivot;
                    if (factor == 0f)
                        continue;

                    for (int column = pivotIndex; column < n; column++)
                        matrix[row * n + column] -= factor * matrix[pivotIndex * n + column];

                    rhs[row] -= factor * rhs[pivotIndex];
                }
            }

            for (int row = n - 1; row >= 0; row--)
            {
                float sum = rhs[row];
                for (int column = row + 1; column < n; column++)
                    sum -= matrix[row * n + column] * rhs[column];

                rhs[row] = sum / matrix[row * n + row];
            }

            return true;
        }

        public static float MaxAbs(float[] values, int count)
        {
            float largest = 0f;
            for (int i = 0; i < count; i++)
            {
                float magnitude = Mathf.Abs(values[i]);
                if (magnitude > largest)
                    largest = magnitude;
            }
            return largest;
        }

        private static bool IsFinite(float[] values, int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (float.IsNaN(values[i]) || float.IsInfinity(values[i]))
                    return false;
            }
            return true;
        }

        private static void CopyInto(float[] source, float[] destination, int count)
        {
            for (int i = 0; i < count; i++)
                destination[i] = source[i];
        }

        private static void ClampInPlace(float[] values, float[] lower, float[] upper, int count)
        {
            for (int i = 0; i < count; i++)
                values[i] = Mathf.Clamp(values[i], lower[i], upper[i]);
        }
    }

    /// <summary>
    /// Aircraft-independent steady symmetric flight trim solver.
    ///
    /// Physical formulation, conventional aircraft body axes (X forward, Y right, Z down),
    /// wings level, zero sideslip, zero body rates, constant true airspeed and constant
    /// flight-path angle. Pitch attitude is therefore theta = alpha + gamma, and gravity resolves
    /// into body axes as (-m*g*sin(theta), 0, +m*g*cos(theta)).
    ///
    ///   axial  : qbar*S*CX + T - m*g*sin(theta) = 0
    ///   normal : qbar*S*CZ     + m*g*cos(theta) = 0
    ///   pitch  : qbar*S*cbar*Cm                 = 0
    ///
    /// Dynamic pressure is applied exactly once, inside MavFlightDynamicsMath.Dimensionalize.
    /// Nothing in this class multiplies by qbar a second time.
    ///
    /// This solver never touches a Rigidbody, a Transform, a component, or Time. It is handed a
    /// MavTrimPlant of frozen numbers and pure functions, and returns a MavTrimResult.
    ///
    /// v0.1 models thrust as acting along body X through the centre of gravity. A propulsion
    /// model that reports an off-axis force or a thrust-line moment is refused with
    /// MavTrimStatus.UnsupportedCondition rather than silently approximated.
    /// </summary>
    public static class MavSteadyFlightTrimSolver
    {
        public const float StandardGravityMps2 = 9.80665f;

        /// <summary>Widest flight-path angle the glide solve is allowed to search, degrees.</summary>
        public const float FlightPathAngleSearchLimitDeg = 89f;

        public static MavTrimResult Solve(MavTrimPlant plant, MavTrimCondition condition)
        {
            return Solve(plant, condition, MavTrimSolverSettings.Default);
        }

        public static MavTrimResult Solve(
            MavTrimPlant plant,
            MavTrimCondition condition,
            MavTrimSolverSettings settings)
        {
            if (plant == null)
                return MavTrimResult.Failed(MavTrimStatus.InvalidPlant, condition, "trim plant is null");

            string plantReason;
            if (!plant.IsValid(out plantReason))
                return MavTrimResult.Failed(MavTrimStatus.InvalidPlant, condition, plantReason);

            string conditionReason;
            if (!condition.IsSupported(out conditionReason))
                return MavTrimResult.Failed(MavTrimStatus.UnsupportedCondition, condition, conditionReason);

            MavAtmosphereSample atmosphere = MavAtmosphereModel.Sample(condition.altitudeM);
            float weightN = plant.massKg * StandardGravityMps2;

            string propulsionReason;
            if (!ThrustModelIsSupported(plant, condition, atmosphere, out propulsionReason))
                return MavTrimResult.Failed(MavTrimStatus.UnsupportedCondition, condition, propulsionReason);

            bool glideMode = condition.mode == MavTrimMode.UnpoweredGlide;
            int unknownCount = glideMode ? 3 : 2;

            float[] unknowns = new float[unknownCount];
            float[] lower = new float[unknownCount];
            float[] upper = new float[unknownCount];
            float[] perturbations = new float[unknownCount];

            unknowns[0] = Mathf.Clamp(settings.initialAlphaDeg, plant.alphaMinDeg, plant.alphaMaxDeg);
            lower[0] = plant.alphaMinDeg;
            upper[0] = plant.alphaMaxDeg;
            perturbations[0] = settings.alphaPerturbationDeg;

            unknowns[1] = Mathf.Clamp(
                settings.initialElevatorDeg,
                plant.controlSurfaceLimits.elevatorMinDeg,
                plant.controlSurfaceLimits.elevatorMaxDeg
            );
            lower[1] = plant.controlSurfaceLimits.elevatorMinDeg;
            upper[1] = plant.controlSurfaceLimits.elevatorMaxDeg;
            perturbations[1] = settings.elevatorPerturbationDeg;

            if (glideMode)
            {
                unknowns[2] = Mathf.Clamp(
                    settings.initialFlightPathAngleDeg,
                    -FlightPathAngleSearchLimitDeg,
                    FlightPathAngleSearchLimitDeg
                );
                lower[2] = -FlightPathAngleSearchLimitDeg;
                upper[2] = FlightPathAngleSearchLimitDeg;
                perturbations[2] = settings.flightPathPerturbationDeg;
            }

            float imposedFlightPathDeg = condition.flightPathAngleDeg;

            MavNewtonResidualFunction residualFunction = delegate (float[] x, float[] r)
            {
                float flightPathDeg = glideMode ? x[2] : imposedFlightPathDeg;
                MavTrimResidual sample = EvaluateResidual(
                    plant,
                    condition.altitudeM,
                    condition.trueAirspeedMps,
                    x[0],
                    x[1],
                    flightPathDeg,
                    0f
                );

                if (glideMode)
                {
                    // Unpowered: thrust is pinned at zero, so all three equations must close.
                    r[0] = sample.axialForceNorm;
                    r[1] = sample.normalForceNorm;
                    r[2] = sample.pitchMomentNorm;
                }
                else
                {
                    // Powered: the axial equation defines the required thrust rather than
                    // constraining alpha/elevator, so only normal force and pitch are driven.
                    r[0] = sample.normalForceNorm;
                    r[1] = sample.pitchMomentNorm;
                }
            };

            int iterations;
            float residualNorm;
            MavNewtonOutcome outcome = MavDampedNewtonSolver.Solve(
                unknownCount,
                residualFunction,
                unknowns,
                lower,
                upper,
                perturbations,
                Mathf.Max(1, settings.maxIterations),
                Mathf.Max(0f, settings.residualTolerance),
                Mathf.Max(0, settings.maxLineSearchHalvings),
                out iterations,
                out residualNorm
            );

            float solvedAlphaDeg = unknowns[0];
            float solvedElevatorDeg = unknowns[1];
            float solvedFlightPathDeg = glideMode ? unknowns[2] : imposedFlightPathDeg;

            MavTrimResidual residual = EvaluateResidual(
                plant,
                condition.altitudeM,
                condition.trueAirspeedMps,
                solvedAlphaDeg,
                solvedElevatorDeg,
                solvedFlightPathDeg,
                0f
            );
            residual.drivenNorm = residualNorm;

            MavTrimResult result = new MavTrimResult();
            result.condition = condition;
            result.alphaDeg = solvedAlphaDeg;
            result.elevatorDeg = solvedElevatorDeg;
            result.flightPathAngleDeg = solvedFlightPathDeg;
            result.pitchAttitudeDeg = solvedAlphaDeg + solvedFlightPathDeg;
            result.iterations = iterations;
            result.residual = residual;
            result.propulsionDataAuthoritative = plant.propulsionDataAuthoritative;
            result.hitAlphaBound = AtBound(solvedAlphaDeg, lower[0], upper[0]);
            result.hitElevatorBound = AtBound(solvedElevatorDeg, lower[1], upper[1]);

            // The axial equation with zero thrust tells us what thrust is missing.
            // axial residual at T=0 is  qbar*S*CX - W*sin(theta); trim needs it plus T to vanish.
            result.requiredThrustN = -residual.axialForceN;

            if (outcome != MavNewtonOutcome.Converged)
            {
                result.status = MapOutcome(outcome);
                result.converged = false;
                result.throttle01 = float.NaN;
                result.throttleDetermined = false;
                result.availableThrustAtFullPowerN = SampleThrust(plant, condition, atmosphere,
                    solvedAlphaDeg, solvedFlightPathDeg, 1f);
                result.report = BuildReport(plant, result, weightN);
                return result;
            }

            if (glideMode)
            {
                result.status = MavTrimStatus.Converged;
                result.converged = true;
                result.throttle01 = 0f;
                result.throttleDetermined = true;
                result.requiredThrustN = 0f;
                result.availableThrustAtFullPowerN = SampleThrust(plant, condition, atmosphere,
                    solvedAlphaDeg, solvedFlightPathDeg, 1f);
                result.report = BuildReport(plant, result, weightN);
                return result;
            }

            ResolveThrottleForPoweredTrim(plant, condition, atmosphere, settings, weightN, ref result);
            result.report = BuildReport(plant, result, weightN);
            return result;
        }

        /// <summary>
        /// Evaluates the three steady-flight equations at an arbitrary (alpha, elevator, gamma,
        /// thrust) point. Public so validation can pin the residual arithmetic against a hand
        /// calculation instead of only checking that the solver agrees with itself.
        /// </summary>
        public static MavTrimResidual EvaluateResidual(
            MavTrimPlant plant,
            float altitudeM,
            float trueAirspeedMps,
            float alphaDeg,
            float elevatorDeg,
            float flightPathAngleDeg,
            float thrustN)
        {
            MavTrimResidual residual = new MavTrimResidual();
            if (plant == null || plant.aeroFunction == null)
                return residual;

            MavAtmosphereSample atmosphere = MavAtmosphereModel.Sample(altitudeM);
            float dynamicPressurePa = 0.5f * atmosphere.densityKgM3 * trueAirspeedMps * trueAirspeedMps;

            float alphaRad = alphaDeg * Mathf.Deg2Rad;
            float thetaRad = (alphaDeg + flightPathAngleDeg) * Mathf.Deg2Rad;

            MavControlInput surfaces = new MavControlInput();
            surfaces.elevatorDeg = elevatorDeg;
            surfaces.aileronDeg = 0f;
            surfaces.rudderDeg = 0f;
            surfaces.leadingEdgeFlapDeg = 0f;
            surfaces.throttle01 = 0f;
            surfaces = plant.controlSurfaceLimits.Clamp(surfaces);

            MavAeroCoefficients coefficients = plant.aeroFunction(alphaRad, 0f, surfaces);

            // Single point of dimensionalization: qbar is applied here and nowhere else.
            MavAerodynamicLoads loads = MavFlightDynamicsMath.Dimensionalize(
                coefficients,
                plant.referenceGeometry,
                dynamicPressurePa
            );

            float weightN = plant.massKg * StandardGravityMps2;

            residual.axialForceN = loads.forceAeroBodyN.x + thrustN - weightN * Mathf.Sin(thetaRad);
            residual.normalForceN = loads.forceAeroBodyN.z + weightN * Mathf.Cos(thetaRad);
            residual.pitchMomentNm = loads.momentAeroBodyNm.y;

            float forceScale = Mathf.Max(1e-6f, weightN);
            float momentScale = Mathf.Max(
                1e-6f,
                dynamicPressurePa
                * plant.referenceGeometry.wingAreaM2
                * plant.referenceGeometry.meanAerodynamicChordM
            );

            residual.axialForceNorm = residual.axialForceN / forceScale;
            residual.normalForceNorm = residual.normalForceN / forceScale;
            residual.pitchMomentNorm = residual.pitchMomentNm / momentScale;
            residual.drivenNorm = Mathf.Max(
                Mathf.Abs(residual.normalForceNorm),
                Mathf.Abs(residual.pitchMomentNorm)
            );

            return residual;
        }

        /// <summary>
        /// Builds the steady flight state a propulsion model would see at a trim point.
        /// Body rates are exactly zero because a trim is, by definition, unaccelerated.
        /// </summary>
        public static MavFlightState BuildTrimFlightState(
            float altitudeM,
            float trueAirspeedMps,
            float alphaDeg,
            float flightPathAngleDeg)
        {
            MavAtmosphereSample atmosphere = MavAtmosphereModel.Sample(altitudeM);
            float alphaRad = alphaDeg * Mathf.Deg2Rad;
            float gammaRad = flightPathAngleDeg * Mathf.Deg2Rad;

            MavFlightState state = new MavFlightState();
            state.worldPositionM = new Vector3(0f, altitudeM, 0f);
            state.worldVelocityMps = new Vector3(
                0f,
                trueAirspeedMps * Mathf.Sin(gammaRad),
                trueAirspeedMps * Mathf.Cos(gammaRad)
            );
            state.aeroBodyVelocityMps = new Vector3(
                trueAirspeedMps * Mathf.Cos(alphaRad),
                0f,
                trueAirspeedMps * Mathf.Sin(alphaRad)
            );
            state.aeroBodyRatesRadSec = Vector3.zero;
            state.trueAirspeedMps = trueAirspeedMps;
            state.mach = trueAirspeedMps / Mathf.Max(1f, atmosphere.speedOfSoundMps);
            state.dynamicPressurePa = 0.5f * atmosphere.densityKgM3 * trueAirspeedMps * trueAirspeedMps;
            state.alphaRad = alphaRad;
            state.betaRad = 0f;
            state.specificForceAeroBodyG = Vector3.zero;
            state.specificForceValid = false;
            return state;
        }

        private static void ResolveThrottleForPoweredTrim(
            MavTrimPlant plant,
            MavTrimCondition condition,
            MavAtmosphereSample atmosphere,
            MavTrimSolverSettings settings,
            float weightN,
            ref MavTrimResult result)
        {
            float requiredThrustN = result.requiredThrustN;
            float toleranceN = Mathf.Max(1f, 1e-4f * weightN);

            if (plant.steadyPropulsionFunction == null)
            {
                result.availableThrustAtFullPowerN = 0f;
                result.throttle01 = float.NaN;
                result.throttleDetermined = false;
                result.converged = Mathf.Abs(requiredThrustN) <= toleranceN;
                result.status = result.converged
                    ? MavTrimStatus.Converged
                    : MavTrimStatus.ConvergedButThrustUnavailable;
                return;
            }

            float idleThrustN = SampleThrust(plant, condition, atmosphere,
                result.alphaDeg, result.flightPathAngleDeg, 0f);
            float fullThrustN = SampleThrust(plant, condition, atmosphere,
                result.alphaDeg, result.flightPathAngleDeg, 1f);

            result.availableThrustAtFullPowerN = fullThrustN;

            float minimumThrustN = Mathf.Min(idleThrustN, fullThrustN);
            float maximumThrustN = Mathf.Max(idleThrustN, fullThrustN);

            if (requiredThrustN > maximumThrustN + toleranceN
                || requiredThrustN < minimumThrustN - toleranceN)
            {
                // The aerodynamic trim is real; the powered condition is not achievable.
                result.throttle01 = float.NaN;
                result.throttleDetermined = false;
                result.converged = false;
                result.status = MavTrimStatus.ConvergedButThrustUnavailable;
                return;
            }

            if (Mathf.Abs(maximumThrustN - minimumThrustN) <= toleranceN)
            {
                // Thrust does not vary with throttle (typically a zero-thrust placeholder) but the
                // required thrust already matches it, so any throttle satisfies the condition.
                result.throttle01 = 0f;
                result.throttleDetermined = true;
                result.converged = true;
                result.status = MavTrimStatus.Converged;
                return;
            }

            float low = 0f;
            float high = 1f;
            int steps = Mathf.Max(1, settings.throttleBisectionSteps);
            for (int i = 0; i < steps; i++)
            {
                float mid = 0.5f * (low + high);
                float midThrustN = SampleThrust(plant, condition, atmosphere,
                    result.alphaDeg, result.flightPathAngleDeg, mid);

                if ((midThrustN - requiredThrustN) * (idleThrustN - requiredThrustN) > 0f)
                    low = mid;
                else
                    high = mid;
            }

            result.throttle01 = Mathf.Clamp01(0.5f * (low + high));
            result.throttleDetermined = true;
            result.converged = true;
            result.status = MavTrimStatus.Converged;
        }

        private static float SampleThrust(
            MavTrimPlant plant,
            MavTrimCondition condition,
            MavAtmosphereSample atmosphere,
            float alphaDeg,
            float flightPathAngleDeg,
            float throttle01)
        {
            if (plant.steadyPropulsionFunction == null)
                return 0f;

            MavFlightState state = BuildTrimFlightState(
                condition.altitudeM,
                condition.trueAirspeedMps,
                alphaDeg,
                flightPathAngleDeg
            );

            MavPropulsiveLoads loads = plant.steadyPropulsionFunction(
                state,
                atmosphere,
                Mathf.Clamp01(throttle01)
            );

            return loads.forceAeroBodyN.x;
        }

        /// <summary>
        /// v0.1 assumes thrust acts along body X through the CG. Rather than approximate an
        /// off-axis or offset thrust line, the solver refuses the problem and says why.
        /// </summary>
        private static bool ThrustModelIsSupported(
            MavTrimPlant plant,
            MavTrimCondition condition,
            MavAtmosphereSample atmosphere,
            out string reason)
        {
            reason = "OK";
            if (plant.steadyPropulsionFunction == null)
                return true;

            float[] probes = { 0f, 0.5f, 1f };
            for (int i = 0; i < probes.Length; i++)
            {
                MavFlightState state = BuildTrimFlightState(
                    condition.altitudeM,
                    condition.trueAirspeedMps,
                    0f,
                    condition.flightPathAngleDeg
                );

                MavPropulsiveLoads loads = plant.steadyPropulsionFunction(state, atmosphere, probes[i]);

                if (Mathf.Abs(loads.forceAeroBodyN.y) > 1e-3f
                    || Mathf.Abs(loads.forceAeroBodyN.z) > 1e-3f)
                {
                    reason = "propulsion model reports off-axis thrust; v0.1 trim models body-X thrust only";
                    return false;
                }

                if (loads.momentAeroBodyNm.sqrMagnitude > 1e-6f)
                {
                    reason = "propulsion model reports a thrust-line moment; v0.1 trim models thrust through the CG only";
                    return false;
                }
            }

            return true;
        }

        private static MavTrimStatus MapOutcome(MavNewtonOutcome outcome)
        {
            switch (outcome)
            {
                case MavNewtonOutcome.Converged: return MavTrimStatus.Converged;
                case MavNewtonOutcome.MaxIterationsExceeded: return MavTrimStatus.MaxIterationsExceeded;
                case MavNewtonOutcome.Stalled: return MavTrimStatus.Stalled;
                case MavNewtonOutcome.SingularJacobian: return MavTrimStatus.SingularJacobian;
                default: return MavTrimStatus.NonFiniteResidual;
            }
        }

        private static bool AtBound(float value, float lower, float upper)
        {
            const float epsilon = 1e-4f;
            return value <= lower + epsilon || value >= upper - epsilon;
        }

        public static string BuildReport(MavTrimPlant plant, MavTrimResult result, float weightN)
        {
            StringBuilder report = new StringBuilder(1024);
            report.Append("Trim: ").Append(plant != null ? plant.plantId : "null-plant")
                  .Append(" | mode=").Append(result.condition.mode)
                  .Append(" | status=").Append(result.status).AppendLine();

            report.Append("  condition : alt=").Append(result.condition.altitudeM.ToString("F0"))
                  .Append(" m, TAS=").Append(result.condition.trueAirspeedMps.ToString("F1"))
                  .Append(" m/s").AppendLine();

            report.Append("  solution  : alpha=").Append(result.alphaDeg.ToString("F3"))
                  .Append(" deg, elevator=").Append(result.elevatorDeg.ToString("F3"))
                  .Append(" deg, gamma=").Append(result.flightPathAngleDeg.ToString("F3"))
                  .Append(" deg, theta=").Append(result.pitchAttitudeDeg.ToString("F3"))
                  .Append(" deg").AppendLine();

            report.Append("  residual  : X=").Append(result.residual.axialForceN.ToString("F3"))
                  .Append(" N, Z=").Append(result.residual.normalForceN.ToString("F3"))
                  .Append(" N, M=").Append(result.residual.pitchMomentNm.ToString("F3"))
                  .Append(" Nm | normalized driven=").Append(result.residual.drivenNorm.ToString("E3"))
                  .AppendLine();

            report.Append("  iterations: ").Append(result.iterations)
                  .Append(" | alphaAtBound=").Append(result.hitAlphaBound)
                  .Append(" | elevatorAtBound=").Append(result.hitElevatorBound).AppendLine();

            report.Append("  thrust    : required=").Append(result.requiredThrustN.ToString("F1"))
                  .Append(" N (").Append((weightN > 0f ? result.requiredThrustN / weightN : 0f).ToString("F4"))
                  .Append(" W), availableAtFullPower=")
                  .Append(result.availableThrustAtFullPowerN.ToString("F1")).Append(" N").AppendLine();

            report.Append("  throttle  : ")
                  .Append(result.throttleDetermined ? result.throttle01.ToString("F4") : "UNDETERMINED")
                  .Append(" | propulsionDataAuthoritative=").Append(result.propulsionDataAuthoritative)
                  .AppendLine();

            if (result.status == MavTrimStatus.ConvergedButThrustUnavailable)
            {
                report.AppendLine(
                    "  NOTE: the aerodynamic trim (alpha/elevator) is valid, but the propulsion model "
                    + "cannot supply the required axial force. This condition is NOT a converged powered "
                    + "trim and must not be reported as one.");
            }

            return report.ToString();
        }
    }
}
