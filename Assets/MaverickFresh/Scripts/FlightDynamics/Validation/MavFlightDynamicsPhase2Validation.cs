using System.Text;
using UnityEngine;
using MaverickFresh.FlightDynamics.F16;

namespace MaverickFresh.FlightDynamics.Validation
{
    /// <summary>
    /// Deterministic, side-effect-free checks for the Phase 2C/2D trim, control-law, protection and
    /// readiness work.
    ///
    /// Every check calls the exact production code through pure static entry points. No GameObject,
    /// Rigidbody, scene or play-mode session is created, and no component state is mutated, so the
    /// suite can be run repeatedly and in any order with identical results.
    ///
    /// Covered:
    ///   [T0] trim residual arithmetic, gravity resolution, and single qbar application
    ///   [T1] trim convergence on a synthetic plant with a known answer
    ///   [T2] non-convergent trim honesty (no thrust, no pitch authority, unreachable alpha)
    ///   [T3] F-16 trim against frozen data, powered and unpowered
    ///   [T4] trim propulsion contract verified at the SOLVED state, not only at a precheck
    ///   [C0] pitch / roll / yaw command direction through the frozen aerodynamic model
    ///   [C1] coordinated-yaw and yaw-damper behaviour
    ///   [L0] load-factor limiter
    ///   [L1] angle-of-attack limiter
    ///   [L2] roll-rate limiter
    ///   [L3] smooth-limiter mathematics
    ///   [L4] integrator anti-windup
    ///   [R0] STRUCTURALLY_PREPARED versus OPERATIONALLY_LIVE_READY
    ///   [R2] legacy-ownership detection has no stale window
    ///   [R3] command-source dropout is fail-closed, with no implicit inspector fallback
    ///   [R4] operational pipeline identity: every miswiring is rejected
    ///   [R1] measured specific force / load-factor sign convention
    ///   [O0] no Rigidbody motion writes outside MavSixDoFBody
    /// </summary>
    public static class MavFlightDynamicsPhase2Validation
    {
        private const float Tolerance = 1e-4f;
        private const float StandardGravity = MavControlLawProtections.StandardGravityMps2;

        public static string RunAll(out int passed, out int failed)
        {
            passed = 0;
            failed = 0;
            StringBuilder report = new StringBuilder(8192);
            report.AppendLine("Maverick Flight Dynamics Phase 2 Validation (trim / control law / protections)");
            report.AppendLine("=============================================================================");

            ValidateTrimResidual(report, ref passed, ref failed);
            ValidateSyntheticTrimConvergence(report, ref passed, ref failed);
            ValidateNonConvergentTrimHonesty(report, ref passed, ref failed);
            ValidateF16Trim(report, ref passed, ref failed);
            ValidateTrimPropulsionContract(report, ref passed, ref failed);
            ValidateControlDirections(report, ref passed, ref failed);
            ValidateCoordinatedYaw(report, ref passed, ref failed);
            ValidateLoadFactorLimiter(report, ref passed, ref failed);
            ValidateAngleOfAttackLimiter(report, ref passed, ref failed);
            ValidateRollRateLimiter(report, ref passed, ref failed);
            ValidateSmoothLimiterMath(report, ref passed, ref failed);
            ValidateIntegratorAntiWindup(report, ref passed, ref failed);
            ValidateReadinessSeparation(report, ref passed, ref failed);
            ValidateLegacyOwnershipFreshness(report, ref passed, ref failed);
            ValidateCommandSourceDropout(report, ref passed, ref failed);
            ValidatePipelineIdentity(report, ref passed, ref failed);
            ValidateSpecificForceConvention(report, ref passed, ref failed);
            ValidateOwnershipScan(report, ref passed, ref failed);

            report.AppendLine();
            report.Append("RESULT: ")
                .Append(failed == 0 ? "PASS" : "FAIL")
                .Append("  passed=").Append(passed)
                .Append(" failed=").Append(failed);

            return report.ToString();
        }

        // ================================================================= [T0]

        private static void ValidateTrimResidual(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[T0] Trim residual arithmetic");

            MavTrimPlant plant = BuildSyntheticPlant(-0.8f, 60000f);

            const float altitudeM = 0f;
            const float airspeedMps = 150f;

            MavAtmosphereSample atmosphere = MavAtmosphereModel.Sample(altitudeM);
            float qbar = 0.5f * atmosphere.densityKgM3 * airspeedMps * airspeedMps;
            float qS = qbar * plant.referenceGeometry.wingAreaM2;
            float weightN = plant.massKg * StandardGravity;

            // --- level, zero alpha, zero elevator, zero thrust ---
            MavTrimResidual flat = MavSteadyFlightTrimSolver.EvaluateResidual(
                plant, altitudeM, airspeedMps, 0f, 0f, 0f, 0f);

            float expectedAxial = qS * SyntheticCx(0f);
            float expectedNormal = weightN;
            float expectedPitch = qS * plant.referenceGeometry.meanAerodynamicChordM * SyntheticCm(0f, 0f, -0.8f);

            Record(
                Near(flat.axialForceN, expectedAxial, Mathf.Abs(expectedAxial) * 1e-4f + 1e-2f),
                "axial residual equals qbar*S*CX with no thrust and no gravity component",
                report, ref passed, ref failed);

            Record(
                Near(flat.normalForceN, expectedNormal, weightN * 1e-5f),
                "normal residual equals the full aircraft weight at zero pitch attitude",
                report, ref passed, ref failed);

            // If dynamic pressure were applied twice this value would be wrong by a factor of qbar,
            // which at 150 m/s at sea level is roughly 13800.
            Record(
                Near(flat.pitchMomentNm, expectedPitch, Mathf.Abs(expectedPitch) * 1e-4f + 1e-2f),
                "pitch residual equals qbar*S*cbar*Cm: dynamic pressure is applied exactly once",
                report, ref passed, ref failed);

            Record(
                Near(flat.normalForceNorm, 1f, 1e-4f)
                && Near(flat.pitchMomentNorm, SyntheticCm(0f, 0f, -0.8f), 1e-5f),
                "normalized residuals are weight fractions and a Cm error",
                report, ref passed, ref failed);

            // --- gravity resolution at a non-zero attitude, with thrust ---
            const float alphaDeg = 4f;
            const float gammaDeg = 10f;
            const float thrustN = 5000f;
            float thetaRad = (alphaDeg + gammaDeg) * Mathf.Deg2Rad;

            MavTrimResidual climbing = MavSteadyFlightTrimSolver.EvaluateResidual(
                plant, altitudeM, airspeedMps, alphaDeg, 0f, gammaDeg, thrustN);

            float expectedClimbAxial =
                qS * SyntheticCx(alphaDeg * Mathf.Deg2Rad) + thrustN - weightN * Mathf.Sin(thetaRad);
            float expectedClimbNormal =
                qS * SyntheticCz(alphaDeg * Mathf.Deg2Rad) + weightN * Mathf.Cos(thetaRad);

            Record(
                Near(climbing.axialForceN, expectedClimbAxial, Mathf.Abs(expectedClimbAxial) * 1e-4f + 1f),
                "axial residual resolves gravity as -W*sin(alpha+gamma) and adds thrust along body X",
                report, ref passed, ref failed);

            Record(
                Near(climbing.normalForceN, expectedClimbNormal, Mathf.Abs(expectedClimbNormal) * 1e-4f + 1f),
                "normal residual resolves gravity as +W*cos(alpha+gamma) along body Z (down)",
                report, ref passed, ref failed);

            Record(
                flat.IsFinite() && climbing.IsFinite(),
                "residuals are finite",
                report, ref passed, ref failed);
        }

        // ================================================================= [T1]

        private static void ValidateSyntheticTrimConvergence(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[T1] Trim convergence on a synthetic plant");

            MavTrimPlant plant = BuildSyntheticPlant(-0.8f, 60000f);
            MavTrimSolverSettings settings = MavTrimSolverSettings.Default;

            MavTrimResult level = MavSteadyFlightTrimSolver.Solve(
                plant, MavTrimCondition.StraightAndLevel(0f, 150f), settings);

            Record(
                level.status == MavTrimStatus.Converged && level.converged,
                "powered straight-and-level trim converges when thrust is available ("
                + level.status + ")",
                report, ref passed, ref failed);

            Record(
                level.residual.drivenNorm <= settings.residualTolerance,
                "driven residual reaches tolerance (" + level.residual.drivenNorm.ToString("E3") + ")",
                report, ref passed, ref failed);

            // Independent physical cross-check of the converged point: lift must balance weight.
            float weightN = plant.massKg * StandardGravity;
            Record(
                Mathf.Abs(level.residual.normalForceN) <= 0.001f * weightN
                && Mathf.Abs(level.residual.pitchMomentNm) <= 1f,
                "at the solution, normal force and pitching moment are physically balanced",
                report, ref passed, ref failed);

            Record(
                level.alphaDeg > 0f && level.alphaDeg < 10f,
                "trim alpha is physically sensible for the synthetic plant ("
                + level.alphaDeg.ToString("F3") + " deg)",
                report, ref passed, ref failed);

            Record(
                level.throttleDetermined && level.throttle01 > 0f && level.throttle01 < 1f,
                "throttle is determined by inverting the thrust curve ("
                + level.throttle01.ToString("F4") + ")",
                report, ref passed, ref failed);

            Record(
                Near(level.requiredThrustN, 60000f * level.throttle01, 60000f * 1e-3f),
                "the solved throttle reproduces the required thrust",
                report, ref passed, ref failed);

            // Determinism: identical inputs must give identical output, iteration count included.
            MavTrimResult repeat = MavSteadyFlightTrimSolver.Solve(
                BuildSyntheticPlant(-0.8f, 60000f),
                MavTrimCondition.StraightAndLevel(0f, 150f),
                settings);

            Record(
                repeat.alphaDeg == level.alphaDeg
                && repeat.elevatorDeg == level.elevatorDeg
                && repeat.iterations == level.iterations,
                "the solver is deterministic: identical inputs give identical iterates",
                report, ref passed, ref failed);

            // Unpowered glide on the same plant: gamma becomes the unknown and must be negative.
            MavTrimResult glide = MavSteadyFlightTrimSolver.Solve(
                plant, MavTrimCondition.UnpoweredGlide(0f, 150f), settings);

            Record(
                glide.status == MavTrimStatus.Converged && glide.converged,
                "unpowered glide trim converges (" + glide.status + ")",
                report, ref passed, ref failed);

            Record(
                glide.flightPathAngleDeg < 0f,
                "an unpowered steady glide has a descending flight path ("
                + glide.flightPathAngleDeg.ToString("F3") + " deg)",
                report, ref passed, ref failed);

            Record(
                Mathf.Abs(glide.residual.axialForceNorm) <= settings.residualTolerance * 2f,
                "the glide solution closes the axial equation with zero thrust",
                report, ref passed, ref failed);
        }

        // ================================================================= [T2]

        private static void ValidateNonConvergentTrimHonesty(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[T2] Non-convergent trim honesty");

            // (a) No thrust source at all: the aerodynamic trim exists, the powered condition does not.
            MavTrimPlant unpowered = BuildSyntheticPlant(-0.8f, 0f);
            MavTrimResult noThrust = MavSteadyFlightTrimSolver.Solve(
                unpowered, MavTrimCondition.StraightAndLevel(0f, 150f));

            Record(
                noThrust.status == MavTrimStatus.ConvergedButThrustUnavailable,
                "a zero-thrust plant reports ConvergedButThrustUnavailable for level flight ("
                + noThrust.status + ")",
                report, ref passed, ref failed);

            Record(
                !noThrust.converged,
                "ConvergedButThrustUnavailable does NOT report converged == true",
                report, ref passed, ref failed);

            Record(
                !noThrust.throttleDetermined && float.IsNaN(noThrust.throttle01),
                "an unachievable powered trim refuses to invent a throttle setting",
                report, ref passed, ref failed);

            Record(
                noThrust.requiredThrustN > 0f && noThrust.availableThrustAtFullPowerN == 0f,
                "the missing thrust is quantified rather than hidden (required "
                + noThrust.requiredThrustN.ToString("F0") + " N, available 0 N)",
                report, ref passed, ref failed);

            // (b) No pitch-control authority: the elevator unknown cannot move any residual.
            MavTrimPlant noPitchAuthority = BuildSyntheticPlant(0f, 60000f);
            MavTrimResult singular = MavSteadyFlightTrimSolver.Solve(
                noPitchAuthority, MavTrimCondition.StraightAndLevel(0f, 150f));

            Record(
                !singular.converged && singular.status != MavTrimStatus.Converged,
                "a plant with no elevator authority does not report a converged trim ("
                + singular.status + ")",
                report, ref passed, ref failed);

            Record(
                Mathf.Abs(singular.residual.pitchMomentNorm) > 1e-3f,
                "the unresolved pitching-moment residual is reported, not zeroed",
                report, ref passed, ref failed);

            // (c) Condition unreachable inside the alpha envelope. The bound is narrowed
            // deliberately: this is the "the trim exists, but outside the range this aerodynamic
            // model is valid for" case, which must not be answered with a number from outside the
            // model's own envelope.
            MavTrimPlant narrowEnvelope = BuildSyntheticPlant(-0.8f, 60000f);
            narrowEnvelope.alphaMaxDeg = 8f;

            MavTrimResult tooSlow = MavSteadyFlightTrimSolver.Solve(
                narrowEnvelope,
                MavTrimCondition.StraightAndLevel(0f, 70f));

            Record(
                !tooSlow.converged && tooSlow.status != MavTrimStatus.Converged,
                "a condition needing more alpha than the model is valid for does not report converged ("
                + tooSlow.status + ")",
                report, ref passed, ref failed);

            Record(
                tooSlow.alphaDeg <= narrowEnvelope.alphaMaxDeg + 1e-3f,
                "and the reported alpha stays inside the model's declared envelope rather than "
                + "extrapolating past it",
                report, ref passed, ref failed);

            Record(
                tooSlow.hitAlphaBound,
                "the solver reports that the alpha unknown was pinned at its bound",
                report, ref passed, ref failed);

            // (d) Unsupported conditions are rejected outright rather than approximated.
            MavTrimCondition banked = MavTrimCondition.StraightAndLevel(0f, 150f);
            banked.bankAngleDeg = 45f;
            MavTrimResult bankedResult = MavSteadyFlightTrimSolver.Solve(
                BuildSyntheticPlant(-0.8f, 60000f), banked);

            Record(
                bankedResult.status == MavTrimStatus.UnsupportedCondition && !bankedResult.converged,
                "a banked trim request is refused as unsupported rather than silently flattened",
                report, ref passed, ref failed);

            MavTrimResult nullPlant = MavSteadyFlightTrimSolver.Solve(
                null, MavTrimCondition.StraightAndLevel(0f, 150f));

            Record(
                nullPlant.status == MavTrimStatus.InvalidPlant && !nullPlant.converged,
                "a null plant is refused as invalid",
                report, ref passed, ref failed);
        }

        // ================================================================= [T3]

        private static void ValidateF16Trim(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[T3] F-16 trim against frozen reference data");

            MavTrimPlant plant = MavF16TrimReference.CreatePlant();
            string plantReason;

            Record(
                plant.IsValid(out plantReason),
                "the F-16 trim plant builds from frozen data (" + plantReason + ")",
                report, ref passed, ref failed);

            Record(
                Near(plant.massKg, MavF16MassReference.MassKg, 1e-3f)
                && Near(plant.referenceGeometry.wingAreaM2, MavF16MorelliReference.WingAreaM2, 1e-4f),
                "the plant uses the frozen mass and reference geometry, not new numbers",
                report, ref passed, ref failed);

            Record(
                !plant.propulsionDataAuthoritative,
                "the plant repeats the propulsion honesty flag: F-16 thrust data is NOT authoritative",
                report, ref passed, ref failed);

            // --- powered straight and level: expected to be honestly unachievable ---
            MavTrimResult level = MavF16TrimReference.SolveStraightAndLevel(0f, 150f);

            Record(
                level.status == MavTrimStatus.ConvergedButThrustUnavailable,
                "F-16 powered straight-and-level reports ConvergedButThrustUnavailable ("
                + level.status + ")",
                report, ref passed, ref failed);

            Record(
                !level.converged && !level.throttleDetermined,
                "the F-16 powered trim is not reported as converged and invents no throttle",
                report, ref passed, ref failed);

            Record(
                level.requiredThrustN > 1000f && level.availableThrustAtFullPowerN == 0f,
                "required thrust is quantified (" + level.requiredThrustN.ToString("F0")
                + " N) against 0 N available",
                report, ref passed, ref failed);

            Record(
                level.alphaDeg > 0f && level.alphaDeg < 15f,
                "the aerodynamic part of the solution is still usable: alpha "
                + level.alphaDeg.ToString("F3") + " deg",
                report, ref passed, ref failed);

            Record(
                level.elevatorDeg >= MavF16MorelliReference.ElevatorMinDeg
                && level.elevatorDeg <= MavF16MorelliReference.ElevatorMaxDeg,
                "trim elevator stays inside the published deflection envelope ("
                + level.elevatorDeg.ToString("F3") + " deg)",
                report, ref passed, ref failed);

            Record(
                Mathf.Abs(level.residual.normalForceN) <= 0.001f * plant.massKg * StandardGravity
                && Mathf.Abs(level.residual.pitchMomentNorm) <= 1e-4f,
                "lift balances weight and the pitching moment is trimmed at the solution",
                report, ref passed, ref failed);

            // --- unpowered glide: the physically well-posed problem for zero thrust ---
            MavTrimResult glide = MavF16TrimReference.SolveUnpoweredGlide(0f, 150f);

            Record(
                glide.status == MavTrimStatus.Converged && glide.converged,
                "F-16 unpowered glide trim converges on frozen aerodynamic data (" + glide.status + ")",
                report, ref passed, ref failed);

            Record(
                glide.flightPathAngleDeg < 0f && glide.flightPathAngleDeg > -30f,
                "the glide solution descends at a plausible angle ("
                + glide.flightPathAngleDeg.ToString("F3") + " deg)",
                report, ref passed, ref failed);

            Record(
                glide.alphaDeg >= MavF16MorelliReference.AlphaMinDeg
                && glide.alphaDeg <= MavF16MorelliReference.AlphaMaxDeg,
                "the glide solution stays inside the published alpha envelope",
                report, ref passed, ref failed);

            // --- consistency: the glide gamma, imposed as a powered condition, needs no thrust ---
            MavTrimResult poweredAtGlideAngle = MavF16TrimReference.SolveSteadyFlightPath(
                0f, 150f, glide.flightPathAngleDeg);

            Record(
                poweredAtGlideAngle.status == MavTrimStatus.Converged,
                "imposing the solved glide angle as a powered condition converges with zero thrust ("
                + poweredAtGlideAngle.status + ")",
                report, ref passed, ref failed);

            Record(
                Mathf.Abs(poweredAtGlideAngle.requiredThrustN) <= 20f,
                "and requires essentially no thrust ("
                + poweredAtGlideAngle.requiredThrustN.ToString("F2") + " N), cross-checking the two modes",
                report, ref passed, ref failed);

            // A trim solve must never disturb live state. The solver has no Rigidbody reference at
            // all, and the F-16 propulsion delegate is a pure steady-state function, so running it
            // twice from different call orders must give the same answer.
            MavTrimResult repeatGlide = MavF16TrimReference.SolveUnpoweredGlide(0f, 150f);
            Record(
                repeatGlide.alphaDeg == glide.alphaDeg
                && repeatGlide.flightPathAngleDeg == glide.flightPathAngleDeg,
                "repeated F-16 trim solves are identical: no hidden state is advanced",
                report, ref passed, ref failed);
        }

        // ================================================================= [T4]

        /// <summary>
        /// Regression for PR #5 review item 3.
        ///
        /// The propulsion contract used to be probed only once, at alpha = the initial guess,
        /// before any iteration. A model whose thrust vector or moment depends on state could pass
        /// that probe and then violate the v0.1 body-X / through-CG assumption at the attitude the
        /// answer is actually built from - producing a quietly wrong trim instead of a refusal.
        ///
        /// Every sample the solver takes is now verified, and the solved state is re-checked.
        /// </summary>
        private static void ValidateTrimPropulsionContract(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[T4] Trim propulsion contract is verified at the solved state");

            const float airspeedMps = 130f;
            const float violationThresholdDeg = 3f;

            // First, establish that this really is the case the old precheck would have missed:
            // clean at the initial guess, violating at the solved attitude.
            MavPropulsiveLoads atGuess = StateDependentMomentLoads(2f, violationThresholdDeg, 60000f, 1f);
            MavPropulsiveLoads atSolution = StateDependentMomentLoads(4.35f, violationThresholdDeg, 60000f, 1f);
            string guessReason;
            string solutionReason;

            Record(
                MavSteadyFlightTrimSolver.IsAxialThroughCentreOfGravity(atGuess, out guessReason),
                "the offending model satisfies the contract at the initial guess, so a precheck "
                + "alone would have passed it",
                report, ref passed, ref failed);

            Record(
                !MavSteadyFlightTrimSolver.IsAxialThroughCentreOfGravity(atSolution, out solutionReason),
                "and violates it at the solved attitude (" + solutionReason + ")",
                report, ref passed, ref failed);

            MavTrimResult momentAtSolution = MavSteadyFlightTrimSolver.Solve(
                BuildStateDependentPropulsionPlant(violationThresholdDeg, false),
                MavTrimCondition.StraightAndLevel(0f, airspeedMps));

            Record(
                momentAtSolution.status == MavTrimStatus.UnsupportedCondition
                && !momentAtSolution.converged,
                "a thrust-line moment appearing only at the solved attitude is refused, not "
                + "silently trimmed (" + momentAtSolution.status + ")",
                report, ref passed, ref failed);

            MavTrimResult offAxisAtSolution = MavSteadyFlightTrimSolver.Solve(
                BuildStateDependentPropulsionPlant(violationThresholdDeg, true),
                MavTrimCondition.StraightAndLevel(0f, airspeedMps));

            Record(
                offAxisAtSolution.status == MavTrimStatus.UnsupportedCondition
                && !offAxisAtSolution.converged,
                "off-axis thrust appearing only at the solved attitude is refused too ("
                + offAxisAtSolution.status + ")",
                report, ref passed, ref failed);

            // An unpowered glide is only meaningful if the engine can actually be commanded to
            // produce nothing. A model with non-zero idle thrust cannot reach the solved condition.
            MavTrimPlant alwaysThrusting = BuildSyntheticPlant(-0.8f, 5000f);
            MavTrimSteadyPropulsionFunction constantThrust = delegate (
                MavFlightState state, MavAtmosphereSample atmosphere, float throttle01)
            {
                MavPropulsiveLoads loads = MavPropulsiveLoads.Zero;
                loads.forceAeroBodyN = new Vector3(5000f, 0f, 0f);
                loads.reportedThrustN = 5000f;
                loads.hasAuthoritativeData = true;
                return loads;
            };
            alwaysThrusting.steadyPropulsionFunction = constantThrust;

            MavTrimResult impossibleGlide = MavSteadyFlightTrimSolver.Solve(
                alwaysThrusting, MavTrimCondition.UnpoweredGlide(0f, airspeedMps));

            Record(
                impossibleGlide.status == MavTrimStatus.UnsupportedCondition
                && !impossibleGlide.converged,
                "an unpowered glide is refused when the model cannot be commanded to zero thrust ("
                + impossibleGlide.status + ")",
                report, ref passed, ref failed);

            // The well-behaved cases must be unaffected by the stricter checking.
            MavTrimResult wellBehaved = MavSteadyFlightTrimSolver.Solve(
                BuildSyntheticPlant(-0.8f, 60000f),
                MavTrimCondition.StraightAndLevel(0f, airspeedMps));

            Record(
                wellBehaved.status == MavTrimStatus.Converged && wellBehaved.converged,
                "a contract-abiding plant still trims normally (" + wellBehaved.status + ")",
                report, ref passed, ref failed);

            Record(
                MavF16TrimReference.SolveUnpoweredGlide(0f, 150f).status == MavTrimStatus.Converged,
                "the F-16 glide trim is unaffected: its propulsion is axial and zero at idle",
                report, ref passed, ref failed);

            Record(
                MavF16TrimReference.SolveStraightAndLevel(0f, 150f).status
                    == MavTrimStatus.ConvergedButThrustUnavailable,
                "and the F-16 powered trim still reports the honest thrust-unavailable result",
                report, ref passed, ref failed);
        }

        // ================================================================= [C0]

        private static void ValidateControlDirections(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[C0] Control-law command directions");

            const float speedMps = 200f;
            MavFlightState level = BuildState(speedMps, 0f, 0f, Vector3.zero, 1f, true);

            MavControlInput neutral = RunLaw(new MavPilotCommand { throttle01 = 0.5f }, level);

            Record(
                Near(neutral.elevatorDeg, 0f, 1e-3f)
                && Near(neutral.aileronDeg, 0f, 1e-3f)
                && Near(neutral.rudderDeg, 0f, 1e-3f),
                "neutral stick in trimmed 1 g flight produces no surface bias",
                report, ref passed, ref failed);

            Record(
                Near(neutral.throttle01, 0.5f, 1e-6f),
                "throttle passes through the control law untouched",
                report, ref passed, ref failed);

            Record(
                Near(neutral.leadingEdgeFlapDeg, 0f, 1e-6f),
                "the law never commands leading-edge flap, which the compact model does not consume",
                report, ref passed, ref failed);

            // --- pitch ---
            MavControlInput noseUp = RunLaw(new MavPilotCommand { pitch = 1f }, level);
            MavAeroCoefficients noseUpCoefficients = EvaluateCoefficients(noseUp, 0f, 0f);
            MavAeroCoefficients neutralCoefficients = EvaluateCoefficients(neutral, 0f, 0f);

            Record(
                noseUp.elevatorDeg < 0f,
                "aft stick commands negative elevator, matching the frozen sign convention ("
                + noseUp.elevatorDeg.ToString("F3") + " deg)",
                report, ref passed, ref failed);

            Record(
                noseUpCoefficients.cm > neutralCoefficients.cm,
                "aft stick increases Cm: the aircraft actually pitches nose up",
                report, ref passed, ref failed);

            // Relative, not absolute: an augmented law commands only the increment needed to reach
            // the demanded rate, so at an untrimmed condition the total moment can still be
            // nose-down. What must be true is that aft stick moves the Rigidbody torque in the
            // nose-up direction, which in Unity local axes is -X.
            Vector3 noseUpTorque = UnityTorqueForSurfaces(noseUp, speedMps);
            Vector3 neutralTorque = UnityTorqueForSurfaces(neutral, speedMps);
            Record(
                noseUpTorque.x < neutralTorque.x,
                "aft stick moves the Rigidbody torque toward Unity -X (nose-up)",
                report, ref passed, ref failed);

            Record(
                Mathf.Abs(noseUpTorque.y) < Mathf.Abs(noseUpTorque.x) * 1e-3f + 1f
                && Mathf.Abs(noseUpTorque.z) < Mathf.Abs(noseUpTorque.x) * 1e-3f + 1f,
                "a pure pitch command produces no roll or yaw cross-coupling at zero sideslip",
                report, ref passed, ref failed);

            MavControlInput noseDown = RunLaw(new MavPilotCommand { pitch = -1f }, level);
            Record(
                noseDown.elevatorDeg > 0f && noseDown.elevatorDeg > noseUp.elevatorDeg,
                "forward stick commands the opposite elevator direction",
                report, ref passed, ref failed);

            // --- roll ---
            MavControlInput rollRight = RunLaw(new MavPilotCommand { roll = 1f }, level);
            MavAeroCoefficients rollCoefficients = EvaluateCoefficients(rollRight, 0f, 0f);

            Record(
                rollRight.aileronDeg < 0f,
                "right stick commands negative aileron, matching the frozen sign convention",
                report, ref passed, ref failed);

            Record(
                rollCoefficients.cl > 0f,
                "right stick produces positive Cl (right wing down)",
                report, ref passed, ref failed);

            Vector3 rollTorque = UnityTorqueForSurfaces(rollRight, speedMps);
            Record(
                rollTorque.z < 0f,
                "right stick reaches the Rigidbody boundary as a Unity -Z (roll-right) torque",
                report, ref passed, ref failed);

            // --- yaw ---
            MavControlInput yawRight = RunLaw(new MavPilotCommand { yaw = 1f }, level);
            MavAeroCoefficients yawCoefficients = EvaluateCoefficients(yawRight, 0f, 0f);

            Record(
                yawRight.rudderDeg < 0f,
                "right pedal commands negative rudder, matching the frozen sign convention",
                report, ref passed, ref failed);

            Record(
                yawCoefficients.cn > 0f,
                "right pedal produces positive Cn (nose right)",
                report, ref passed, ref failed);

            Vector3 yawTorque = UnityTorqueForSurfaces(yawRight, speedMps);
            Record(
                yawTorque.y > 0f,
                "right pedal reaches the Rigidbody boundary as a Unity +Y (nose-right) torque",
                report, ref passed, ref failed);

            // --- rate feedback opposes the motion, through physical surfaces only ---
            MavFlightState pitchingUp = BuildState(speedMps, 0f, 0f, new Vector3(0f, 0.2f, 0f), 1f, true);
            MavControlInput pitchDamping = RunLaw(MavPilotCommand.Neutral, pitchingUp);
            Record(
                pitchDamping.elevatorDeg > 0f,
                "an uncommanded nose-up rate is opposed by a nose-down elevator command",
                report, ref passed, ref failed);

            MavFlightState rollingRight = BuildState(speedMps, 0f, 0f, new Vector3(1f, 0f, 0f), 1f, true);
            MavControlInput rollDamping = RunLaw(MavPilotCommand.Neutral, rollingRight);
            Record(
                rollDamping.aileronDeg > 0f,
                "an uncommanded roll-right rate is opposed by a roll-left aileron command",
                report, ref passed, ref failed);

            // --- determinism ---
            MavControlInput first = RunLaw(new MavPilotCommand { pitch = 0.4f, roll = -0.3f, yaw = 0.2f }, level);
            MavControlInput second = RunLaw(new MavPilotCommand { pitch = 0.4f, roll = -0.3f, yaw = 0.2f }, level);
            Record(
                first.elevatorDeg == second.elevatorDeg
                && first.aileronDeg == second.aileronDeg
                && first.rudderDeg == second.rudderDeg,
                "the control law is deterministic for identical inputs and initial state",
                report, ref passed, ref failed);
        }

        // ================================================================= [C1]

        private static void ValidateCoordinatedYaw(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[C1] Coordinated yaw and yaw damper");

            const float speedMps = 200f;

            // Positive beta means the relative wind comes from the right and the nose sits left of
            // the velocity vector. Coordinating means yawing the nose right to zero it.
            MavFlightState sideslipRight = BuildState(speedMps, 0f, 5f, Vector3.zero, 1f, true);
            MavControlInput coordinating = RunLaw(MavPilotCommand.Neutral, sideslipRight);
            MavAeroCoefficients coordinatingCoefficients = EvaluateCoefficients(coordinating, 0f, 5f);

            Record(
                coordinating.rudderDeg < 0f,
                "positive sideslip with no pedal commands nose-right rudder to coordinate",
                report, ref passed, ref failed);

            MavAeroCoefficients uncontrolled = EvaluateCoefficients(new MavControlInput(), 0f, 5f);
            Record(
                coordinatingCoefficients.cn > uncontrolled.cn,
                "the coordinating rudder adds nose-right yawing moment on top of natural weathercock",
                report, ref passed, ref failed);

            MavFlightState sideslipLeft = BuildState(speedMps, 0f, -5f, Vector3.zero, 1f, true);
            MavControlInput coordinatingLeft = RunLaw(MavPilotCommand.Neutral, sideslipLeft);
            Record(
                coordinatingLeft.rudderDeg > 0f,
                "negative sideslip commands the mirrored rudder direction",
                report, ref passed, ref failed);

            // Manual yaw bias: right pedal commands a nose-right yaw, i.e. a NEGATIVE sideslip.
            MavF16ControlLawDebug debug;
            RunLawWithDebug(new MavPilotCommand { yaw = 1f }, BuildState(speedMps, 0f, 0f, Vector3.zero, 1f, true), out debug);

            Record(
                debug.commandedSideslipDeg < 0f,
                "right pedal commands a negative sideslip target ("
                + debug.commandedSideslipDeg.ToString("F2") + " deg), which is a nose-right yaw",
                report, ref passed, ref failed);

            Record(
                Near(debug.commandedSideslipDeg,
                     -MavF16ControlLawGains.Default.commandedSideslipAtFullPedalDeg, 1e-3f),
                "full pedal commands the configured maximum manual sideslip",
                report, ref passed, ref failed);

            // Yaw damper: the washout must remove a steady yaw rate so the damper does not fight a
            // coordinated turn, while still opposing a fresh oscillatory rate.
            MavF16ControlLawState lawState = MavF16ControlLawState.Zero;
            MavF16ControlLawGains gains = MavF16ControlLawGains.Default;
            MavFlightState steadyTurn = BuildState(speedMps, 0f, 0f, new Vector3(0f, 0f, 0.15f), 1f, true);

            MavF16ControlLawDebug firstStep;
            MavF16ControlLawV01.Compute(
                MavPilotCommand.Neutral, steadyTurn, BuildF16SurfaceLimits(), gains,
                MavAngleOfAttackLimiterSettings.Default, MavLoadFactorLimiterSettings.Default,
                MavRollRateLimiterSettings.Default, ref lawState, 0.02f, out firstStep);

            float initialWashout = Mathf.Abs(firstStep.washedOutYawRateRadSec);

            MavF16ControlLawDebug lateStep = firstStep;
            for (int i = 0; i < 1000; i++)
            {
                MavF16ControlLawV01.Compute(
                    MavPilotCommand.Neutral, steadyTurn, BuildF16SurfaceLimits(), gains,
                    MavAngleOfAttackLimiterSettings.Default, MavLoadFactorLimiterSettings.Default,
                    MavRollRateLimiterSettings.Default, ref lawState, 0.02f, out lateStep);
            }

            Record(
                Mathf.Abs(lateStep.washedOutYawRateRadSec) < 0.05f * initialWashout,
                "a sustained yaw rate washes out, so the damper stops fighting a steady turn",
                report, ref passed, ref failed);

            // With the washout state settled on a steady rate, a sudden extra yaw rate must still be
            // opposed: the damper responds to the change, not to the trim value.
            MavFlightState disturbed = BuildState(speedMps, 0f, 0f, new Vector3(0f, 0f, 0.45f), 1f, true);
            MavF16ControlLawDebug disturbedStep;
            MavF16ControlLawV01.Compute(
                MavPilotCommand.Neutral, disturbed, BuildF16SurfaceLimits(), gains,
                MavAngleOfAttackLimiterSettings.Default, MavLoadFactorLimiterSettings.Default,
                MavRollRateLimiterSettings.Default, ref lawState, 0.02f, out disturbedStep);

            Record(
                disturbedStep.washedOutYawRateRadSec > 0.2f,
                "a fresh yaw-rate disturbance still passes the washout and reaches the damper",
                report, ref passed, ref failed);

            // Aileron-rudder interconnect: a commanded roll adds coordinating rudder feed-forward.
            MavF16ControlLawDebug rollDebug;
            MavControlInput rolling = RunLawWithDebug(
                new MavPilotCommand { roll = 1f },
                BuildState(speedMps, 0f, 0f, Vector3.zero, 1f, true),
                out rollDebug);

            Record(
                rolling.rudderDeg < 0f,
                "a commanded right roll adds nose-right interconnect rudder against adverse yaw",
                report, ref passed, ref failed);
        }

        // ================================================================= [L0]

        private static void ValidateLoadFactorLimiter(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[L0] Load-factor (G) limiter");

            const float speedMps = 200f;
            MavLoadFactorLimiterSettings limiter = MavLoadFactorLimiterSettings.Default;

            MavF16ControlLawDebug atOneG;
            RunLawWithDebug(new MavPilotCommand { pitch = 1f },
                BuildState(speedMps, 0f, 0f, Vector3.zero, 1f, true), out atOneG);

            Record(
                Near(atOneG.commandedLoadFactorG, limiter.maxLoadFactorG, 1e-3f),
                "full aft stick commands the configured maximum load factor ("
                + atOneG.commandedLoadFactorG.ToString("F2") + " g)",
                report, ref passed, ref failed);

            float openLoopCeiling = MavControlLawProtections.LoadFactorPitchRateCeilingRadSec(
                limiter.maxLoadFactorG, speedMps, 30f);

            Record(
                atOneG.limitedPitchRateCommandRadSec <= openLoopCeiling + 1e-4f,
                "the commanded pitch rate never exceeds the rate implied by the g limit",
                report, ref passed, ref failed);

            Record(
                atOneG.rawPitchRateCommandRadSec > atOneG.limitedPitchRateCommandRadSec,
                "the raw command is genuinely reduced by the limiter, not merely passed through",
                report, ref passed, ref failed);

            // Monotone tightening as measured load factor approaches the limit.
            float previous = float.PositiveInfinity;
            bool monotone = true;
            for (float nz = 1f; nz <= 9f; nz += 1f)
            {
                MavF16ControlLawDebug debug;
                RunLawWithDebug(new MavPilotCommand { pitch = 1f },
                    BuildState(speedMps, 0f, 0f, Vector3.zero, nz, true), out debug);

                if (debug.limitedPitchRateCommandRadSec > previous + 1e-5f)
                    monotone = false;
                previous = debug.limitedPitchRateCommandRadSec;
            }

            Record(monotone,
                "allowed pitch rate falls monotonically as the measured load factor rises",
                report, ref passed, ref failed);

            MavF16ControlLawDebug atLimit;
            RunLawWithDebug(new MavPilotCommand { pitch = 1f },
                BuildState(speedMps, 0f, 0f, Vector3.zero, limiter.maxLoadFactorG, true), out atLimit);

            Record(
                atLimit.limitedPitchRateCommandRadSec <= 1e-3f,
                "at the g limit the nose-up rate command is exhausted ("
                + atLimit.limitedPitchRateCommandRadSec.ToString("F4") + " rad/s)",
                report, ref passed, ref failed);

            MavF16ControlLawDebug overLimit;
            MavControlInput overLimitOutput = RunLawWithDebug(new MavPilotCommand { pitch = 1f },
                BuildState(speedMps, 0f, 0f, Vector3.zero, limiter.maxLoadFactorG + 1.5f, true), out overLimit);

            Record(
                overLimit.limitedPitchRateCommandRadSec < 0f,
                "beyond the g limit the law commands a nose-down recovery rate, not merely zero",
                report, ref passed, ref failed);

            Record(
                overLimitOutput.elevatorDeg > 0f,
                "and that recovery reaches the surface as nose-down elevator",
                report, ref passed, ref failed);

            // Negative-g side.
            MavF16ControlLawDebug atNegativeLimit;
            RunLawWithDebug(new MavPilotCommand { pitch = -1f },
                BuildState(speedMps, 0f, 0f, Vector3.zero, limiter.minLoadFactorG, true), out atNegativeLimit);

            Record(
                atNegativeLimit.limitedPitchRateCommandRadSec >= -1e-3f,
                "at the negative g limit the nose-down rate command is exhausted",
                report, ref passed, ref failed);

            // With no measurement the open-loop bound must still apply.
            MavF16ControlLawDebug unmeasured;
            RunLawWithDebug(new MavPilotCommand { pitch = 1f },
                BuildState(speedMps, 0f, 0f, Vector3.zero, 1f, false), out unmeasured);

            Record(
                !unmeasured.loadFactorMeasurementValid
                && unmeasured.limitedPitchRateCommandRadSec <= openLoopCeiling + 1e-4f,
                "without a measured load factor the open-loop g bound still limits the command",
                report, ref passed, ref failed);

            // Disabling the limiter must be observable, not silent.
            MavLoadFactorLimiterSettings disabled = limiter;
            disabled.enabled = false;
            MavF16ControlLawState lawState = MavF16ControlLawState.Zero;
            MavF16ControlLawDebug unlimited;
            MavF16ControlLawV01.Compute(
                new MavPilotCommand { pitch = 1f },
                BuildState(speedMps, 0f, 0f, Vector3.zero, 9f, true),
                BuildF16SurfaceLimits(), MavF16ControlLawGains.Default,
                MavAngleOfAttackLimiterSettings.Default, disabled,
                MavRollRateLimiterSettings.Default, ref lawState, 0.02f, out unlimited);

            Record(
                unlimited.limitedPitchRateCommandRadSec > atLimit.limitedPitchRateCommandRadSec,
                "disabling the g limiter demonstrably removes the restriction",
                report, ref passed, ref failed);
        }

        // ================================================================= [L1]

        private static void ValidateAngleOfAttackLimiter(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[L1] Angle-of-attack limiter");

            const float speedMps = 200f;
            MavAngleOfAttackLimiterSettings limiter = MavAngleOfAttackLimiterSettings.Default;

            MavF16ControlLawDebug lowAlpha;
            RunLawWithDebug(new MavPilotCommand { pitch = 1f },
                BuildState(speedMps, 2f, 0f, Vector3.zero, 1f, true), out lowAlpha);

            MavF16ControlLawDebug nearLimit;
            RunLawWithDebug(new MavPilotCommand { pitch = 1f },
                BuildState(speedMps, limiter.maxAlphaDeg - 2f, 0f, Vector3.zero, 1f, true), out nearLimit);

            Record(
                nearLimit.limitedPitchRateCommandRadSec < lowAlpha.limitedPitchRateCommandRadSec,
                "nose-up authority is reduced as alpha approaches the limit",
                report, ref passed, ref failed);

            MavF16ControlLawDebug atLimit;
            RunLawWithDebug(new MavPilotCommand { pitch = 1f },
                BuildState(speedMps, limiter.maxAlphaDeg, 0f, Vector3.zero, 1f, true), out atLimit);

            Record(
                atLimit.limitedPitchRateCommandRadSec <= 1e-3f,
                "at the alpha limit the nose-up rate command is exhausted ("
                + atLimit.limitedPitchRateCommandRadSec.ToString("F4") + " rad/s)",
                report, ref passed, ref failed);

            MavF16ControlLawDebug beyondLimit;
            MavControlInput beyondOutput = RunLawWithDebug(new MavPilotCommand { pitch = 1f },
                BuildState(speedMps, limiter.maxAlphaDeg + 5f, 0f, Vector3.zero, 1f, true), out beyondLimit);

            Record(
                beyondLimit.limitedPitchRateCommandRadSec < 0f,
                "beyond the alpha limit the law commands a nose-down recovery rate",
                report, ref passed, ref failed);

            Record(
                beyondOutput.elevatorDeg > 0f,
                "and that recovery reaches the surface as nose-down elevator",
                report, ref passed, ref failed);

            // The limiter must never block a nose-down command at high alpha: that would trap the
            // aircraft above its own limit.
            MavControlInput escaping = RunLaw(new MavPilotCommand { pitch = -1f },
                BuildState(speedMps, limiter.maxAlphaDeg + 5f, 0f, Vector3.zero, 1f, true));

            Record(
                escaping.elevatorDeg > 0f,
                "full forward stick above the alpha limit still commands nose-down elevator",
                report, ref passed, ref failed);

            // Low-alpha side.
            MavF16ControlLawDebug belowMinimum;
            RunLawWithDebug(new MavPilotCommand { pitch = -1f },
                BuildState(speedMps, limiter.minAlphaDeg - 5f, 0f, Vector3.zero, 1f, true), out belowMinimum);

            Record(
                belowMinimum.limitedPitchRateCommandRadSec > 0f,
                "below the minimum alpha the law commands a nose-up recovery rate",
                report, ref passed, ref failed);

            // Smoothness: no step change in commanded rate across the limiter knee.
            float previous = float.NaN;
            float largestStep = 0f;
            for (float alphaDeg = 0f; alphaDeg <= 35f; alphaDeg += 0.25f)
            {
                MavF16ControlLawDebug debug;
                RunLawWithDebug(new MavPilotCommand { pitch = 1f },
                    BuildState(speedMps, alphaDeg, 0f, Vector3.zero, 1f, true), out debug);

                if (!float.IsNaN(previous))
                    largestStep = Mathf.Max(largestStep,
                        Mathf.Abs(debug.limitedPitchRateCommandRadSec - previous));

                previous = debug.limitedPitchRateCommandRadSec;
            }

            Record(
                largestStep < 0.05f,
                "the alpha limiter engages smoothly: largest command step over a 0.25 deg sweep is "
                + largestStep.ToString("F4") + " rad/s",
                report, ref passed, ref failed);
        }

        // ================================================================= [L2]

        private static void ValidateRollRateLimiter(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[L2] Roll-rate limiter");

            const float speedMps = 200f;
            MavRollRateLimiterSettings limiter = MavRollRateLimiterSettings.Default;
            float limitRadSec = limiter.maxRollRateDegSec * Mathf.Deg2Rad;

            bool alwaysBounded = true;
            for (float stick = -1f; stick <= 1f; stick += 0.05f)
            {
                MavF16ControlLawDebug debug;
                RunLawWithDebug(new MavPilotCommand { roll = stick },
                    BuildState(speedMps, 0f, 0f, Vector3.zero, 1f, true), out debug);

                if (Mathf.Abs(debug.limitedRollRateCommandRadSec) > limitRadSec + 1e-4f)
                    alwaysBounded = false;
            }

            Record(alwaysBounded,
                "commanded roll rate never exceeds the configured limit across the full stick range",
                report, ref passed, ref failed);

            MavF16ControlLawDebug lowAlphaRoll;
            RunLawWithDebug(new MavPilotCommand { roll = 1f },
                BuildState(speedMps, 0f, 0f, Vector3.zero, 1f, true), out lowAlphaRoll);

            MavF16ControlLawDebug highAlphaRoll;
            RunLawWithDebug(new MavPilotCommand { roll = 1f },
                BuildState(speedMps, limiter.authorityFadeEndAlphaDeg + 5f, 0f, Vector3.zero, 1f, true),
                out highAlphaRoll);

            Record(
                Near(lowAlphaRoll.rollAuthorityFactor, 1f, 1e-3f),
                "roll authority is unrestricted at low alpha",
                report, ref passed, ref failed);

            Record(
                Near(highAlphaRoll.rollAuthorityFactor, limiter.minimumAuthorityFactor, 1e-3f),
                "roll authority falls to the configured floor beyond the fade band",
                report, ref passed, ref failed);

            Record(
                Mathf.Abs(highAlphaRoll.limitedRollRateCommandRadSec)
                < Mathf.Abs(lowAlphaRoll.limitedRollRateCommandRadSec),
                "high-alpha roll commands are correspondingly reduced",
                report, ref passed, ref failed);

            // Smoothness of the authority fade.
            float previous = float.NaN;
            float largestStep = 0f;
            for (float alphaDeg = 0f; alphaDeg <= 40f; alphaDeg += 0.25f)
            {
                float factor = MavControlLawProtections.RollAuthorityFactorAtAlpha(
                    alphaDeg,
                    limiter.authorityFadeStartAlphaDeg,
                    limiter.authorityFadeEndAlphaDeg,
                    limiter.minimumAuthorityFactor);

                if (!float.IsNaN(previous))
                    largestStep = Mathf.Max(largestStep, Mathf.Abs(factor - previous));

                previous = factor;
            }

            Record(largestStep < 0.05f,
                "the roll-authority fade is smooth: largest step is " + largestStep.ToString("F4"),
                report, ref passed, ref failed);
        }

        // ================================================================= [L3]

        private static void ValidateSmoothLimiterMath(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[L3] Smooth limiter mathematics");

            const float limit = 2f;
            const float band = 0.4f;

            Record(
                Near(MavControlLawProtections.SoftSaturate(0.5f, limit, band), 0.5f, 1e-6f)
                && Near(MavControlLawProtections.SoftSaturate(-1.2f, limit, band), -1.2f, 1e-6f),
                "SoftSaturate is exactly the identity below the knee: small commands are undistorted",
                report, ref passed, ref failed);

            bool bounded = true;
            bool monotone = true;
            float previousValue = float.NegativeInfinity;
            float largestStep = 0f;
            float previousStepValue = float.NaN;

            for (float x = -6f; x <= 6f; x += 0.01f)
            {
                float y = MavControlLawProtections.SoftSaturate(x, limit, band);

                if (Mathf.Abs(y) > limit + 1e-5f)
                    bounded = false;
                if (y < previousValue - 1e-6f)
                    monotone = false;
                if (!float.IsNaN(previousStepValue))
                    largestStep = Mathf.Max(largestStep, Mathf.Abs(y - previousStepValue));

                previousValue = y;
                previousStepValue = y;
            }

            Record(bounded, "SoftSaturate never exceeds the limit", report, ref passed, ref failed);
            Record(monotone, "SoftSaturate is monotone", report, ref passed, ref failed);
            Record(largestStep <= 0.011f,
                "SoftSaturate is continuous: largest output step over a 0.01 input step is "
                + largestStep.ToString("F5"),
                report, ref passed, ref failed);

            Record(
                Near(MavControlLawProtections.SoftSaturate(limit + band, limit, band), limit, 1e-5f)
                && Near(MavControlLawProtections.SoftSaturate(100f, limit, band), limit, 1e-5f),
                "SoftSaturate reaches the limit exactly at the end of the band and holds it beyond",
                report, ref passed, ref failed);

            Record(
                MavControlLawProtections.SoftSaturate(limit, limit, band) < limit,
                "inside the band the command is eased below the limit rather than clipped to it",
                report, ref passed, ref failed);

            // Conservatism: a smoothed protection may be tighter than the hard one, never looser.
            bool smoothMinConservative = true;
            bool smoothMaxConservative = true;
            for (float a = -3f; a <= 3f; a += 0.1f)
            {
                for (float b = -3f; b <= 3f; b += 0.1f)
                {
                    if (MavControlLawProtections.SmoothMin(a, b, 0.2f) > Mathf.Min(a, b) + 1e-5f)
                        smoothMinConservative = false;
                    if (MavControlLawProtections.SmoothMax(a, b, 0.2f) < Mathf.Max(a, b) - 1e-5f)
                        smoothMaxConservative = false;
                }
            }

            Record(smoothMinConservative,
                "SmoothMin never returns more than the true minimum",
                report, ref passed, ref failed);
            Record(smoothMaxConservative,
                "SmoothMax never returns less than the true maximum",
                report, ref passed, ref failed);

            Record(
                Near(MavControlLawProtections.SmoothMin(5f, -2f, 1e-4f), -2f, 1e-3f),
                "SmoothMin converges to the hard minimum as the band shrinks",
                report, ref passed, ref failed);

            // Exactness outside the band is what keeps chained protections from leaving a permanent
            // offset on a command that is nowhere near any limit.
            Record(
                MavControlLawProtections.SmoothMin(0f, 5f, 0.2f) == 0f
                && MavControlLawProtections.SmoothMax(0f, -5f, 0.2f) == 0f,
                "SmoothMin/SmoothMax are exactly the identity when no limit is close: no hidden bias",
                report, ref passed, ref failed);

            Record(
                Near(MavControlLawProtections.SmoothMin(1f, 2f, 0f), 1f, 1e-6f)
                && Near(MavControlLawProtections.SmoothMax(1f, 2f, 0f), 2f, 1e-6f),
                "a zero band degrades to the exact hard min/max",
                report, ref passed, ref failed);

            // Gain scheduling must stay bounded even at a degenerate dynamic pressure.
            float scaleAtZeroQ = MavControlLawProtections.DynamicPressureGainScale(0f, 24000f, 0.25f, 4f);
            Record(
                scaleAtZeroQ <= 4f + 1e-5f && scaleAtZeroQ >= 0.25f - 1e-5f,
                "dynamic-pressure gain scheduling stays clamped at zero airspeed ("
                + scaleAtZeroQ.ToString("F3") + ")",
                report, ref passed, ref failed);

            Record(
                Near(MavControlLawProtections.DynamicPressureGainScale(24000f, 24000f, 0.25f, 4f), 1f, 1e-4f),
                "gain scale is unity at the reference dynamic pressure",
                report, ref passed, ref failed);
        }

        // ================================================================= [L4]

        private static void ValidateIntegratorAntiWindup(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[L4] Integrator anti-windup");

            float state = 0f;
            for (int i = 0; i < 1000; i++)
            {
                state = MavControlLawProtections.StepIntegratorWithAntiWindup(
                    state, 10f, 5f, 0.02f, 8f, false, false);
            }

            Record(
                Near(state, 8f, 1e-4f),
                "an unsaturated integrator is hard-clamped at its configured limit",
                report, ref passed, ref failed);

            float saturatedState = 3f;
            for (int i = 0; i < 1000; i++)
            {
                saturatedState = MavControlLawProtections.StepIntegratorWithAntiWindup(
                    saturatedState, 10f, 5f, 0.02f, 8f, true, false);
            }

            Record(
                Near(saturatedState, 3f, 1e-5f),
                "a positive error does not accumulate while the surface is saturated positive",
                report, ref passed, ref failed);

            float unwinding = 3f;
            unwinding = MavControlLawProtections.StepIntegratorWithAntiWindup(
                unwinding, -10f, 5f, 0.02f, 8f, true, false);

            Record(
                unwinding < 3f,
                "the integrator can still unwind out of saturation in the opposite direction",
                report, ref passed, ref failed);

            // End to end through the law: hold a large rate error against a saturated elevator and
            // confirm the stored command stays bounded rather than winding up.
            MavF16ControlLawGains aggressive = MavF16ControlLawGains.Default;
            aggressive.pitchRateGainDegPerRadSec = 200f;
            aggressive.pitchRateIntegralGainDegPerRadSecPerSec = 500f;
            aggressive.pitchIntegralLimitDeg = 400f;
            aggressive.scheduleGainsWithDynamicPressure = false;

            MavF16ControlLawState lawState = MavF16ControlLawState.Zero;
            MavControlSurfaceLimits limits = BuildF16SurfaceLimits();
            MavFlightState stuck = BuildState(200f, 0f, 0f, Vector3.zero, 1f, true);

            MavControlInput output = new MavControlInput();
            for (int i = 0; i < 500; i++)
            {
                MavF16ControlLawDebug debug;
                output = MavF16ControlLawV01.Compute(
                    new MavPilotCommand { pitch = 1f }, stuck, limits, aggressive,
                    MavAngleOfAttackLimiterSettings.Default, MavLoadFactorLimiterSettings.Default,
                    MavRollRateLimiterSettings.Default, ref lawState, 0.02f, out debug);
            }

            Record(
                Mathf.Abs(lawState.pitchIntegralDeg) <= aggressive.pitchIntegralLimitDeg + 1e-3f,
                "the law's integral state stays inside its configured bound under sustained saturation",
                report, ref passed, ref failed);

            Record(
                output.elevatorDeg >= limits.elevatorMinDeg - 1e-4f
                && output.elevatorDeg <= limits.elevatorMaxDeg + 1e-4f,
                "the commanded elevator stays inside the physical surface limits",
                report, ref passed, ref failed);

            Record(
                !float.IsNaN(lawState.pitchIntegralDeg) && !float.IsInfinity(lawState.pitchIntegralDeg)
                && !float.IsNaN(output.elevatorDeg) && !float.IsInfinity(output.elevatorDeg),
                "no integrator divergence: state and output remain finite over 500 steps",
                report, ref passed, ref failed);

            // A control law must never start a flight with a stored command from the last one.
            Record(
                MavF16ControlLawState.Zero.pitchIntegralDeg == 0f
                && MavF16ControlLawState.Zero.yawRateLowPassRadSec == 0f,
                "the reset law state is fully zeroed",
                report, ref passed, ref failed);
        }

        // ================================================================= [R0]

        private static void ValidateReadinessSeparation(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[R0] STRUCTURALLY_PREPARED versus OPERATIONALLY_LIVE_READY");

            MavFlightDynamicsReadinessInputs full = MavFlightDynamicsReadinessInputs.FullyReady;
            MavFlightDynamicsReadinessReport ready = MavFlightDynamicsReadiness.Evaluate(full);

            Record(
                ready.level == MavFlightDynamicsReadinessLevel.OperationallyLiveReady
                && ready.structurallyPrepared && ready.operationallyLiveReady,
                "a fully satisfied stack reaches OPERATIONALLY_LIVE_READY",
                report, ref passed, ref failed);

            // The Phase 1 question - are the parts present? - must no longer be sufficient.
            MavFlightDynamicsReadinessInputs presentOnly = new MavFlightDynamicsReadinessInputs();
            presentOnly.hasRigidbody = true;
            presentOnly.hasValidProfile = true;
            presentOnly.hasAerodynamicModel = true;
            presentOnly.hasControlSurfaceActuator = true;
            presentOnly.hasControlLaw = true;
            presentOnly.hasPropulsionModel = true;

            MavFlightDynamicsReadinessReport structuralOnly =
                MavFlightDynamicsReadiness.Evaluate(presentOnly);

            Record(
                structuralOnly.structurallyPrepared && !structuralOnly.operationallyLiveReady,
                "component presence alone reaches STRUCTURALLY_PREPARED but NOT live-ready",
                report, ref passed, ref failed);

            Record(
                structuralOnly.level == MavFlightDynamicsReadinessLevel.StructurallyPrepared,
                "and the reported level distinguishes the two states",
                report, ref passed, ref failed);

            // Every structural criterion must block on its own.
            RecordBlocks(report, ref passed, ref failed, full, "hasRigidbody", true);
            RecordBlocks(report, ref passed, ref failed, full, "hasValidProfile", true);
            RecordBlocks(report, ref passed, ref failed, full, "hasAerodynamicModel", true);
            RecordBlocks(report, ref passed, ref failed, full, "hasControlSurfaceActuator", true);
            RecordBlocks(report, ref passed, ref failed, full, "hasControlLaw", true);
            RecordBlocks(report, ref passed, ref failed, full, "hasPropulsionModel", true);

            // Every operational criterion must block live-readiness while remaining structural.
            RecordBlocks(report, ref passed, ref failed, full, "aerodynamicGeometryMatchesProfile", false);
            RecordBlocks(report, ref passed, ref failed, full, "controlLawEnabledAndDriving", false);
            RecordBlocks(report, ref passed, ref failed, full, "actuatorEnabledAndBound", false);
            RecordBlocks(report, ref passed, ref failed, full, "propulsionAccepted", false);
            RecordBlocks(report, ref passed, ref failed, full, "hasValidCommandSource", false);
            RecordBlocks(report, ref passed, ref failed, full, "legacyPhysicsOwnershipClear", false);

            // The current F-16 configuration must be honestly reported as not live-ready: its
            // propulsion model has no frozen thrust deck and is not accepted by default.
            MavFlightDynamicsReadinessInputs f16Today = MavFlightDynamicsReadinessInputs.FullyReady;
            f16Today.propulsionAccepted = false;
            f16Today.hasValidCommandSource = false;
            MavFlightDynamicsReadinessReport f16Report = MavFlightDynamicsReadiness.Evaluate(f16Today);

            Record(
                f16Report.structurallyPrepared && !f16Report.operationallyLiveReady,
                "the branch's actual F-16 configuration reports prepared-but-not-live-ready",
                report, ref passed, ref failed);

            // Backward compatibility: the Phase 1 static entry point still answers the structural
            // question and nothing more.
            string legacyReason;
            Record(
                MavSixDoFBody.EvaluateReadiness(true, true, true, true, true, true, out legacyReason)
                && legacyReason == "READY",
                "the Phase 1 structural readiness entry point is unchanged for existing callers",
                report, ref passed, ref failed);

            // A manual/test command source must not satisfy the operational criterion by default.
            Record(
                !MavPilotCommandSourceBase.EvaluatesAsLiveCommandPath(
                    MavManualPilotCommandSource.DefaultTreatAsOperationalSource, true),
                "a default manual command source does not claim to be an operational input path",
                report, ref passed, ref failed);
        }

        /// <summary>
        /// Clears one readiness input by name and asserts what it blocks. Structural criteria must
        /// drop the level to NotPrepared; operational criteria must leave it at
        /// STRUCTURALLY_PREPARED while denying live-readiness.
        /// </summary>
        private static void RecordBlocks(
            StringBuilder report,
            ref int passed,
            ref int failed,
            MavFlightDynamicsReadinessInputs template,
            string fieldName,
            bool structural)
        {
            MavFlightDynamicsReadinessInputs inputs = template;
            switch (fieldName)
            {
                case "hasRigidbody": inputs.hasRigidbody = false; break;
                case "hasValidProfile": inputs.hasValidProfile = false; break;
                case "hasAerodynamicModel": inputs.hasAerodynamicModel = false; break;
                case "hasControlSurfaceActuator": inputs.hasControlSurfaceActuator = false; break;
                case "hasControlLaw": inputs.hasControlLaw = false; break;
                case "hasPropulsionModel": inputs.hasPropulsionModel = false; break;
                case "aerodynamicGeometryMatchesProfile": inputs.aerodynamicGeometryMatchesProfile = false; break;
                case "controlLawEnabledAndDriving": inputs.controlLawEnabledAndDriving = false; break;
                case "actuatorEnabledAndBound": inputs.actuatorEnabledAndBound = false; break;
                case "propulsionAccepted": inputs.propulsionAccepted = false; break;
                case "hasValidCommandSource": inputs.hasValidCommandSource = false; break;
                case "legacyPhysicsOwnershipClear": inputs.legacyPhysicsOwnershipClear = false; break;
                default:
                    Record(false, "unknown readiness field " + fieldName, report, ref passed, ref failed);
                    return;
            }

            MavFlightDynamicsReadinessReport result = MavFlightDynamicsReadiness.Evaluate(inputs);

            bool ok = structural
                ? (!result.structurallyPrepared
                   && result.level == MavFlightDynamicsReadinessLevel.NotPrepared)
                : (result.structurallyPrepared
                   && !result.operationallyLiveReady
                   && result.level == MavFlightDynamicsReadinessLevel.StructurallyPrepared);

            Record(
                ok,
                (structural ? "structural" : "operational") + " criterion '" + fieldName
                + "' blocks as expected (" + result.summary + ")",
                report, ref passed, ref failed);
        }

        // ================================================================= [R2]

        /// <summary>
        /// Regression for PR #5 review item 1.
        ///
        /// The legacy-ownership verdict used to be answered from a cache refreshed every 25 physics
        /// steps. If a legacy owner was enabled while the new FDM was live, operational readiness
        /// could stay true and loads could keep being applied for up to ~0.5 s - two systems owning
        /// the same physical effect, which is the exact failure this gate exists to prevent.
        ///
        /// The rule is now: while load application is armed, always rescan. These checks pin that
        /// there is no schedule, and no countdown value, that can produce a stale safety answer.
        /// </summary>
        private static void ValidateLegacyOwnershipFreshness(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[R2] Legacy-ownership detection has no stale window");

            bool alwaysRescansWhenArmed = true;
            for (int countdown = -5; countdown <= 40; countdown++)
            {
                if (!MavSixDoFBody.ShouldRescanLegacyOwnership(true, countdown))
                    alwaysRescansWhenArmed = false;
            }

            Record(
                alwaysRescansWhenArmed,
                "while load application is armed, the legacy-ownership scan runs on EVERY step for "
                + "every possible countdown value: no stale safety verdict is reachable",
                report, ref passed, ref failed);

            Record(
                !MavSixDoFBody.ShouldRescanLegacyOwnership(false, 25)
                && !MavSixDoFBody.ShouldRescanLegacyOwnership(false, 1)
                && MavSixDoFBody.ShouldRescanLegacyOwnership(false, 0)
                && MavSixDoFBody.ShouldRescanLegacyOwnership(false, -3),
                "while nothing is armed the verdict is diagnostics only, and stays throttled",
                report, ref passed, ref failed);

            // The detection rule itself.
            string[] denyList = MavSixDoFBody.DefaultConflictingLegacyPhysicsComponents;

            Record(
                MavSixDoFBody.IsLegacyOwnershipConflict("MavAeroBody", true, denyList)
                && MavSixDoFBody.IsLegacyOwnershipConflict("MavMouseFlightJet", true, denyList)
                && MavSixDoFBody.IsLegacyOwnershipConflict("MavAtmosphericEngine", true, denyList)
                && MavSixDoFBody.IsLegacyOwnershipConflict("MavInstructorController", true, denyList),
                "every legacy physics owner named in the project constraints is detected",
                report, ref passed, ref failed);

            Record(
                !MavSixDoFBody.IsLegacyOwnershipConflict("MavAeroBody", false, denyList),
                "a DISABLED legacy component owns nothing and is not a conflict",
                report, ref passed, ref failed);

            Record(
                !MavSixDoFBody.IsLegacyOwnershipConflict("MavF16AeroModel", true, denyList)
                && !MavSixDoFBody.IsLegacyOwnershipConflict("MavSixDoFBody", true, denyList),
                "new-path components are not mistaken for legacy owners",
                report, ref passed, ref failed);

            Record(
                !MavSixDoFBody.IsLegacyOwnershipConflict("mavaerobody", true, denyList),
                "matching is ordinal, so a near-miss name is not silently treated as a match",
                report, ref passed, ref failed);

            Record(
                !MavSixDoFBody.IsLegacyOwnershipConflict("MavAeroBody", true, null)
                && !MavSixDoFBody.IsLegacyOwnershipConflict(null, true, denyList)
                && !MavSixDoFBody.IsLegacyOwnershipConflict("MavAeroBody", true, new string[0]),
                "a null or empty deny-list and a null type name are handled without throwing",
                report, ref passed, ref failed);

            // And the consequence: a detected conflict must remove live-readiness immediately.
            MavFlightDynamicsReadinessInputs conflicted = MavFlightDynamicsReadinessInputs.FullyReady;
            conflicted.legacyPhysicsOwnershipClear = false;
            MavFlightDynamicsReadinessReport conflictedReport =
                MavFlightDynamicsReadiness.Evaluate(conflicted);

            Record(
                !conflictedReport.operationallyLiveReady
                && conflictedReport.level == MavFlightDynamicsReadinessLevel.StructurallyPrepared,
                "an activated legacy owner drops the stack out of live-ready in the same evaluation",
                report, ref passed, ref failed);
        }

        // ================================================================= [R3]

        /// <summary>
        /// Regression for PR #5 review item 2.
        ///
        /// Readiness used to check only IsOperationalCommandSource, which is a declaration about
        /// the kind of path and says nothing about whether a command arrived this step. A source
        /// could therefore stay nominally operational, stop producing, and the aircraft would keep
        /// flying on whatever was in the control law's inspector field while the readiness gate
        /// still reported it fit to fly.
        ///
        /// Availability is now part of readiness, and signal loss resolves through the source's
        /// declared policy. No branch of that path can return an inspector value.
        /// </summary>
        private static void ValidateCommandSourceDropout(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[R3] Command-source dropout is fail-closed");

            Record(
                MavPilotCommandSourceBase.EvaluatesAsLiveCommandPath(true, true),
                "an operational path that is producing commands is a live command path",
                report, ref passed, ref failed);

            Record(
                !MavPilotCommandSourceBase.EvaluatesAsLiveCommandPath(true, false),
                "an operational path with NO SIGNAL is not a live command path: availability is "
                + "part of readiness, not merely the declaration",
                report, ref passed, ref failed);

            Record(
                !MavPilotCommandSourceBase.EvaluatesAsLiveCommandPath(false, true),
                "a bench source that is producing commands is still not a live command path",
                report, ref passed, ref failed);

            // The readiness consequence of a dropout.
            MavFlightDynamicsReadinessInputs droppedOut = MavFlightDynamicsReadinessInputs.FullyReady;
            droppedOut.hasValidCommandSource = false;
            MavFlightDynamicsReadinessReport droppedReport =
                MavFlightDynamicsReadiness.Evaluate(droppedOut);

            Record(
                !droppedReport.operationallyLiveReady
                && droppedReport.level == MavFlightDynamicsReadinessLevel.StructurallyPrepared,
                "a dropout drops the stack out of live-ready rather than leaving the gate open",
                report, ref passed, ref failed);

            // The command-resolution decision. This is the blocker in one line.
            Record(
                MavFlightControlLawBase.ClassifyCommandResolution(true, true, false, true)
                    == MavFlightControlLawBase.MavCommandResolution.SignalLossPolicy,
                "an OPERATIONAL source with no signal resolves to its declared signal-loss policy, "
                + "NOT to inspector input",
                report, ref passed, ref failed);

            Record(
                MavFlightControlLawBase.ClassifyCommandResolution(true, true, false, false)
                    == MavFlightControlLawBase.MavCommandResolution.BenchInspectorFallback,
                "a bench source with no signal may still use inspector input: the fallback survives "
                + "where it is legitimate",
                report, ref passed, ref failed);

            Record(
                MavFlightControlLawBase.ClassifyCommandResolution(true, true, true, true)
                    == MavFlightControlLawBase.MavCommandResolution.SourceSignal
                && MavFlightControlLawBase.ClassifyCommandResolution(true, true, true, false)
                    == MavFlightControlLawBase.MavCommandResolution.SourceSignal,
                "a source that is producing commands is always used",
                report, ref passed, ref failed);

            Record(
                MavFlightControlLawBase.ClassifyCommandResolution(false, false, false, false)
                    == MavFlightControlLawBase.MavCommandResolution.NoSource
                && MavFlightControlLawBase.ClassifyCommandResolution(true, false, false, true)
                    == MavFlightControlLawBase.MavCommandResolution.NoSource,
                "no source, or a disabled source, is bench mode",
                report, ref passed, ref failed);

            // The declared policies.
            MavPilotCommand lastGood = new MavPilotCommand
            {
                pitch = 0.7f,
                roll = -0.4f,
                yaw = 0.25f,
                throttle01 = 0.8f
            };

            MavPilotCommand neutralPolicy = MavPilotCommandSourceBase.ResolveOnSignalLoss(
                MavCommandSignalLossPolicy.NeutralCommand, lastGood);

            Record(
                Near(neutralPolicy.pitch, 0f, 1e-6f)
                && Near(neutralPolicy.roll, 0f, 1e-6f)
                && Near(neutralPolicy.yaw, 0f, 1e-6f),
                "NeutralCommand centres pitch, roll and yaw",
                report, ref passed, ref failed);

            Record(
                Near(neutralPolicy.throttle01, lastGood.throttle01, 1e-6f),
                "NeutralCommand holds the last throttle: chopping to idle is a larger disturbance "
                + "than centring the stick, and this layer does not own thrust",
                report, ref passed, ref failed);

            MavPilotCommand holdPolicy = MavPilotCommandSourceBase.ResolveOnSignalLoss(
                MavCommandSignalLossPolicy.HoldLastCommand, lastGood);

            Record(
                Near(holdPolicy.pitch, lastGood.pitch, 1e-6f)
                && Near(holdPolicy.roll, lastGood.roll, 1e-6f)
                && Near(holdPolicy.yaw, lastGood.yaw, 1e-6f)
                && Near(holdPolicy.throttle01, lastGood.throttle01, 1e-6f),
                "HoldLastCommand holds the entire last valid command",
                report, ref passed, ref failed);

            // With no valid command ever received, the policy must produce neutral, not garbage.
            MavPilotCommand neverReceived = MavPilotCommandSourceBase.ResolveOnSignalLoss(
                MavCommandSignalLossPolicy.HoldLastCommand, MavPilotCommand.Neutral);

            Record(
                neverReceived.IsNeutral(1e-6f) && Near(neverReceived.throttle01, 0f, 1e-6f),
                "a dropout before any command was ever received yields a neutral command",
                report, ref passed, ref failed);

            Record(
                MavPilotCommandSourceBase.ResolveOnSignalLoss(
                    MavCommandSignalLossPolicy.HoldLastCommand,
                    new MavPilotCommand { pitch = 5f, throttle01 = 9f }).pitch <= 1f,
                "the signal-loss command is clamped like any other pilot command",
                report, ref passed, ref failed);
        }

        // ================================================================= [R4]

        /// <summary>
        /// One wiring configuration of the operational pipeline, expressed as object references.
        ///
        /// Deliberately typed as <c>object</c>: the flags the production rule consumes are derived
        /// from reference identity, and testing that derivation needs distinct instances, not Unity
        /// components. Using the SAME predicates the body uses is what keeps this honest - the test
        /// re-derives the flags rather than asserting hand-written ones.
        /// </summary>
        private struct MavWiringScenario
        {
            public object body;
            public object controlLawReadsBody;
            public object actuator;
            public object controlLawDrivesActuator;
            public object actuatorBoundToBody;
            public object bodyCommandSource;
            public object controlLawCommandSource;
            public bool sourceEnabled;
            public bool sourceDeclaresOperational;
            public bool observedSourceSignalThisStep;
        }

        /// <summary>
        /// Turns a wiring configuration into readiness inputs by running the PRODUCTION mapping,
        /// <see cref="MavFlightDynamicsReadiness.BuildInputs"/>.
        ///
        /// An earlier revision re-derived the mapping here instead. That made the test agree
        /// perfectly with itself while never executing the derivation the runtime actually uses -
        /// so a bug in that derivation would have been invisible. Now the only thing this method
        /// does is describe the wiring; every judgement about what it means comes from production
        /// code.
        /// </summary>
        private static MavFlightDynamicsReadinessInputs BuildInputsFromWiring(MavWiringScenario wiring)
        {
            MavPipelineSnapshot snapshot = new MavPipelineSnapshot();

            snapshot.body = wiring.body;
            snapshot.hasRigidbody = true;
            snapshot.hasValidProfile = true;
            snapshot.aerodynamicGeometryMatchesProfile = true;
            snapshot.aerodynamicModel = AnyPresentObject;
            snapshot.propulsionModel = AnyPresentObject;
            snapshot.propulsionAcceptableForLiveFlight = true;

            snapshot.controlLaw = AnyPresentObject;
            snapshot.controlLawEnabled = true;
            snapshot.controlLawDrivesActuatorEachStep = true;
            snapshot.controlLawActuator = wiring.controlLawDrivesActuator;
            snapshot.controlLawBody = wiring.controlLawReadsBody;
            snapshot.controlLawCommandSource = wiring.controlLawCommandSource;
            snapshot.observedSourceSignalThisStep = wiring.observedSourceSignalThisStep;
            snapshot.enabledControlLawCount = 1;

            snapshot.actuator = wiring.actuator;
            snapshot.actuatorEnabled = true;
            snapshot.actuatorBoundBody = wiring.actuatorBoundToBody;

            snapshot.bodyCommandSource = wiring.bodyCommandSource;
            snapshot.commandSourceEnabled = wiring.sourceEnabled;
            snapshot.commandSourceDeclaresOperational = wiring.sourceDeclaresOperational;

            snapshot.legacyOwnerActive = false;

            return MavFlightDynamicsReadiness.BuildInputs(snapshot);
        }

        /// <summary>Stand-in for a component whose identity is not what a given scenario is about.</summary>
        private static readonly object AnyPresentObject = new object();

        /// <summary>A correctly wired pipeline: one body, one actuator, one source, all agreeing.</summary>
        private static MavWiringScenario BuildCorrectWiring()
        {
            object body = new object();
            object actuator = new object();
            object source = new object();

            MavWiringScenario wiring = new MavWiringScenario();
            wiring.body = body;
            wiring.controlLawReadsBody = body;
            wiring.actuator = actuator;
            wiring.controlLawDrivesActuator = actuator;
            wiring.actuatorBoundToBody = body;
            wiring.bodyCommandSource = source;
            wiring.controlLawCommandSource = source;
            wiring.sourceEnabled = true;
            wiring.sourceDeclaresOperational = true;
            wiring.observedSourceSignalThisStep = true;
            return wiring;
        }

        /// <summary>
        /// Operational pipeline identity: every reference in the control path must point at the
        /// object it claims to.
        ///
        /// Presence checks cannot catch a swap. A control law wired to the right actuator but
        /// reading a different body computes every command from another aircraft's airspeed, alpha
        /// and rates - the surfaces still move, the aircraft still flies, and nothing looks broken.
        /// The same is true of a command path assembled from a declaration on one source and an
        /// observed signal on another.
        /// </summary>
        private static void ValidatePipelineIdentity(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[R4] Operational pipeline identity / miswiring rejection");

            // The identity predicate itself, before anything built on it means much. The wiring
            // scenarios below no longer call it directly - they go through the production mapping -
            // so it is pinned here on its own.
            object a = new object();
            object b = new object();

            Record(
                MavSixDoFBody.IdentityMatches(a, a) && !MavSixDoFBody.IdentityMatches(a, b),
                "IdentityMatches distinguishes the same object from a different one",
                report, ref passed, ref failed);

            Record(
                !MavSixDoFBody.IdentityMatches(null, null)
                && !MavSixDoFBody.IdentityMatches(a, null)
                && !MavSixDoFBody.IdentityMatches(null, a),
                "two nulls are NOT a match: an unknown wiring state fails closed rather than "
                + "being read as agreement",
                report, ref passed, ref failed);

            // 1. Correct wiring passes.
            MavWiringScenario correct = BuildCorrectWiring();
            MavFlightDynamicsReadinessReport correctReport =
                MavFlightDynamicsReadiness.Evaluate(BuildInputsFromWiring(correct));

            Record(
                correctReport.operationallyLiveReady,
                "1. a correctly wired body + law + actuator + source reaches OPERATIONALLY_LIVE_READY",
                report, ref passed, ref failed);

            // 2. Control law reads a different six-DoF body.
            MavWiringScenario wrongBody = BuildCorrectWiring();
            wrongBody.controlLawReadsBody = new object();
            RecordRejected(
                wrongBody,
                "2. control law reading a DIFFERENT six-DoF body",
                report, ref passed, ref failed);

            // 3. Control law drives a different actuator.
            MavWiringScenario wrongActuator = BuildCorrectWiring();
            wrongActuator.controlLawDrivesActuator = new object();
            RecordRejected(
                wrongActuator,
                "3. control law driving a DIFFERENT actuator",
                report, ref passed, ref failed);

            // 4. Actuator publishes into a different body.
            MavWiringScenario wrongActuatorBinding = BuildCorrectWiring();
            wrongActuatorBinding.actuatorBoundToBody = new object();
            RecordRejected(
                wrongActuatorBinding,
                "4. actuator bound to a DIFFERENT body",
                report, ref passed, ref failed);

            // 5. Body inspects source A while the control law reads source B. This is the case a
            //    presence check cannot see at all: both halves exist and both look healthy.
            MavWiringScenario splitSource = BuildCorrectWiring();
            splitSource.controlLawCommandSource = new object();
            RecordRejected(
                splitSource,
                "5. body inspecting source A while the control law reads source B",
                report, ref passed, ref failed);

            MavFlightDynamicsReadinessInputs splitInputs = BuildInputsFromWiring(splitSource);
            Record(
                !splitInputs.commandSourceIdentityMatches && !splitInputs.hasValidCommandSource,
                "   and the split path is rejected as a whole, not patched together from a "
                + "declaration on one object and availability on another",
                report, ref passed, ref failed);

            // 6. Operational source present and matching, but disabled.
            MavWiringScenario disabledSource = BuildCorrectWiring();
            disabledSource.sourceEnabled = false;
            RecordRejected(
                disabledSource,
                "6. operational command source disabled",
                report, ref passed, ref failed);

            // 7. Operational source enabled, but no signal observed this step.
            MavWiringScenario noSignal = BuildCorrectWiring();
            noSignal.observedSourceSignalThisStep = false;
            RecordRejected(
                noSignal,
                "7. operational command source present but producing no signal",
                report, ref passed, ref failed);

            // 8. A bench source that IS producing commands still is not an operational path.
            MavWiringScenario benchSource = BuildCorrectWiring();
            benchSource.sourceDeclaresOperational = false;
            RecordRejected(
                benchSource,
                "8. bench source producing a signal is still not operationally live-ready",
                report, ref passed, ref failed);

            // 9. Restoring every reference recovers readiness deterministically.
            MavWiringScenario repaired = BuildCorrectWiring();
            MavFlightDynamicsReadinessReport firstRun =
                MavFlightDynamicsReadiness.Evaluate(BuildInputsFromWiring(repaired));
            MavFlightDynamicsReadinessReport secondRun =
                MavFlightDynamicsReadiness.Evaluate(BuildInputsFromWiring(repaired));

            Record(
                firstRun.operationallyLiveReady
                && secondRun.operationallyLiveReady
                && firstRun.level == secondRun.level
                && firstRun.summary == secondRun.summary,
                "9. once every reference is restored, readiness recovers and is deterministic",
                report, ref passed, ref failed);

            // Each miswiring must remain STRUCTURALLY_PREPARED: the parts are all present, which is
            // exactly why presence is not a sufficient gate.
            MavFlightDynamicsReadinessReport wrongBodyReport =
                MavFlightDynamicsReadiness.Evaluate(BuildInputsFromWiring(wrongBody));

            Record(
                wrongBodyReport.structurallyPrepared
                && wrongBodyReport.level == MavFlightDynamicsReadinessLevel.StructurallyPrepared,
                "a miswired pipeline is still STRUCTURALLY_PREPARED, which is why presence alone "
                + "cannot be the safety gate",
                report, ref passed, ref failed);

            // The full pipeline predicate, clause by clause.
            Record(
                MavPilotCommandSourceBase.EvaluatesAsLiveCommandPipeline(true, true, true, true, true, true),
                "the command pipeline predicate accepts a fully satisfied path",
                report, ref passed, ref failed);

            // The mapping itself, exercised on facts rather than on identities: presence and flag
            // handling that the scenarios above do not vary.
            MavPipelineSnapshot bare = new MavPipelineSnapshot();
            MavFlightDynamicsReadinessInputs bareInputs = MavFlightDynamicsReadiness.BuildInputs(bare);

            Record(
                !bareInputs.hasRigidbody && !bareInputs.hasAerodynamicModel
                && !bareInputs.hasControlSurfaceActuator && !bareInputs.hasControlLaw
                && !bareInputs.hasPropulsionModel && !bareInputs.hasValidCommandSource,
                "the production mapping reports an empty pipeline as entirely absent",
                report, ref passed, ref failed);

            MavPipelineSnapshot twoLaws = BuildCorrectSnapshot();
            twoLaws.enabledControlLawCount = 2;

            Record(
                !MavFlightDynamicsReadiness.BuildInputs(twoLaws).singleControlLawEnabled,
                "the production mapping turns two enabled control laws into a failed criterion",
                report, ref passed, ref failed);

            MavPipelineSnapshot legacyActive = BuildCorrectSnapshot();
            legacyActive.legacyOwnerActive = true;

            Record(
                !MavFlightDynamicsReadiness.BuildInputs(legacyActive).legacyPhysicsOwnershipClear,
                "and an active legacy owner into a failed legacy-ownership criterion",
                report, ref passed, ref failed);

            Record(
                MavFlightDynamicsReadiness.Evaluate(
                    MavFlightDynamicsReadiness.BuildInputs(BuildCorrectSnapshot()))
                    .operationallyLiveReady,
                "a fully wired snapshot reaches live-ready through the production mapping",
                report, ref passed, ref failed);

            Record(
                !MavPilotCommandSourceBase.EvaluatesAsLiveCommandPipeline(false, true, true, true, true, true)
                && !MavPilotCommandSourceBase.EvaluatesAsLiveCommandPipeline(true, false, true, true, true, true)
                && !MavPilotCommandSourceBase.EvaluatesAsLiveCommandPipeline(true, true, false, true, true, true)
                && !MavPilotCommandSourceBase.EvaluatesAsLiveCommandPipeline(true, true, true, false, true, true)
                && !MavPilotCommandSourceBase.EvaluatesAsLiveCommandPipeline(true, true, true, true, false, true)
                && !MavPilotCommandSourceBase.EvaluatesAsLiveCommandPipeline(true, true, true, true, true, false),
                "every clause of the command pipeline predicate is individually required",
                report, ref passed, ref failed);
        }

        /// <summary>A fully satisfied snapshot, for exercising the mapping on non-identity facts.</summary>
        private static MavPipelineSnapshot BuildCorrectSnapshot()
        {
            object body = new object();
            object actuator = new object();
            object source = new object();

            MavPipelineSnapshot snapshot = new MavPipelineSnapshot();
            snapshot.body = body;
            snapshot.hasRigidbody = true;
            snapshot.hasValidProfile = true;
            snapshot.aerodynamicGeometryMatchesProfile = true;
            snapshot.aerodynamicModel = AnyPresentObject;
            snapshot.propulsionModel = AnyPresentObject;
            snapshot.propulsionAcceptableForLiveFlight = true;
            snapshot.controlLaw = AnyPresentObject;
            snapshot.controlLawEnabled = true;
            snapshot.controlLawDrivesActuatorEachStep = true;
            snapshot.controlLawActuator = actuator;
            snapshot.controlLawBody = body;
            snapshot.controlLawCommandSource = source;
            snapshot.observedSourceSignalThisStep = true;
            snapshot.enabledControlLawCount = 1;
            snapshot.actuator = actuator;
            snapshot.actuatorEnabled = true;
            snapshot.actuatorBoundBody = body;
            snapshot.bodyCommandSource = source;
            snapshot.commandSourceEnabled = true;
            snapshot.commandSourceDeclaresOperational = true;
            snapshot.legacyOwnerActive = false;
            return snapshot;
        }

        private static void RecordRejected(
            MavWiringScenario wiring,
            string description,
            StringBuilder report,
            ref int passed,
            ref int failed)
        {
            MavFlightDynamicsReadinessReport result =
                MavFlightDynamicsReadiness.Evaluate(BuildInputsFromWiring(wiring));

            Record(
                !result.operationallyLiveReady,
                description + " is rejected (" + result.operationalReason + ")",
                report, ref passed, ref failed);
        }

        // ================================================================= [R1]

        private static void ValidateSpecificForceConvention(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[R1] Measured specific force and load-factor sign");

            const float massKg = 9298.65f;
            float weightN = massKg * StandardGravity;

            // A lift force of one weight acts UP, which is negative body Z.
            Vector3 oneG = MavSixDoFBody.ComputeSpecificForceG(new Vector3(0f, 0f, -weightN), massKg);

            Record(
                Near(oneG.z, -1f, 1e-4f),
                "an upward aerodynamic force of one weight gives -1 g along body Z (down positive)",
                report, ref passed, ref failed);

            MavFlightState state = new MavFlightState();
            state.specificForceAeroBodyG = oneG;
            state.specificForceValid = true;

            Record(
                Near(state.LoadFactorNz, 1f, 1e-4f),
                "and that reads as Nz = +1 g in the conventional pulling-g sense",
                report, ref passed, ref failed);

            Vector3 nineG = MavSixDoFBody.ComputeSpecificForceG(new Vector3(0f, 0f, -9f * weightN), massKg);
            state.specificForceAeroBodyG = nineG;
            Record(
                Near(state.LoadFactorNz, 9f, 1e-3f),
                "a nine-weight upward force reads as Nz = +9 g",
                report, ref passed, ref failed);

            Vector3 negative = MavSixDoFBody.ComputeSpecificForceG(new Vector3(0f, 0f, 2f * weightN), massKg);
            state.specificForceAeroBodyG = negative;
            Record(
                Near(state.LoadFactorNz, -2f, 1e-3f),
                "a downward force reads as negative Nz (pushing over)",
                report, ref passed, ref failed);

            Record(
                MavSixDoFBody.ComputeSpecificForceG(new Vector3(1000f, 0f, 0f), 0f) == Vector3.zero,
                "a non-positive mass yields zero rather than an infinity that would poison a control loop",
                report, ref passed, ref failed);

            MavFlightState unmeasured = new MavFlightState();
            Record(
                !unmeasured.specificForceValid,
                "a freshly built flight state reports no specific-force measurement, so 'no reading' "
                + "is distinguishable from a genuine 0 g reading",
                report, ref passed, ref failed);
        }

        // ================================================================= [O0]

        private static void ValidateOwnershipScan(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[O0] Rigidbody ownership: MavSixDoFBody is the only load-application boundary");

            // The classifier itself must be right before its verdict on the tree means anything.
            Record(
                MavFlightDynamicsOwnershipScan.IsOwnershipViolation("            rb.AddRelativeForce(f, ForceMode.Force);"),
                "the scanner detects a real AddRelativeForce call",
                report, ref passed, ref failed);

            Record(
                MavFlightDynamicsOwnershipScan.IsOwnershipViolation("        rb.linearVelocity = Vector3.zero;"),
                "the scanner detects a direct velocity write (arcade velocity alignment)",
                report, ref passed, ref failed);

            Record(
                !MavFlightDynamicsOwnershipScan.IsOwnershipViolation("        /// A control law must never call AddForce or AddTorque."),
                "a documentation comment mentioning AddForce is not a false positive",
                report, ref passed, ref failed);

            Record(
                !MavFlightDynamicsOwnershipScan.IsOwnershipViolation("        float lift = 0f; // AddForce would be wrong here"),
                "a trailing comment mentioning AddForce is not a false positive",
                report, ref passed, ref failed);

            Record(
                !MavFlightDynamicsOwnershipScan.IsOwnershipViolation("        string token = \"AddTorque\";"),
                "a string literal containing a forbidden token is not a false positive",
                report, ref passed, ref failed);

            Record(
                !MavFlightDynamicsOwnershipScan.IsOwnershipViolation("        Vector3 worldVelocity = rb.linearVelocity;"),
                "reading rigid-body velocity is not an ownership violation",
                report, ref passed, ref failed);

            // The exemption list is the one thing that could quietly hollow out this check, so it
            // is pinned. A new entry has to be a deliberate edit here as well as there.
            Record(
                MavFlightDynamicsOwnershipScan.ExemptFileNames.Length == 2
                && MavFlightDynamicsOwnershipScan.IsExemptFile("MavSixDoFBody.cs")
                && MavFlightDynamicsOwnershipScan.IsExemptFile(
                    "MavFlightDynamicsIntegrationValidation.cs"),
                "the ownership-scan exemption list contains only the load-application boundary and "
                + "the editor-only integration rig",
                report, ref passed, ref failed);

            Record(
                !MavFlightDynamicsOwnershipScan.IsExemptFile("MavF16ControlLawV01.cs")
                && !MavFlightDynamicsOwnershipScan.IsExemptFile("MavF16ControlActuator.cs")
                && !MavFlightDynamicsOwnershipScan.IsExemptFile("MavPhysicsOwnershipController.cs"),
                "no control law, actuator or ownership controller is exempt",
                report, ref passed, ref failed);

            MavOwnershipScanResult scan = MavFlightDynamicsOwnershipScan.Scan();

            if (!scan.sourcesAvailable)
            {
                report.AppendLine("  SKIP  flight-dynamics sources unavailable (not an editor context)");
                return;
            }

            Record(
                scan.filesScanned > 0,
                "the scan found flight-dynamics sources (" + scan.filesScanned + " files)",
                report, ref passed, ref failed);

            if (!scan.IsClean)
            {
                for (int i = 0; i < scan.violations.Count; i++)
                    report.Append("        offender: ").AppendLine(scan.violations[i]);
            }

            Record(
                scan.IsClean,
                "no component outside MavSixDoFBody writes Rigidbody motion state ("
                + (scan.violations != null ? scan.violations.Count : 0) + " violations)",
                report, ref passed, ref failed);
        }

        // ================================================================= helpers

        private static float SyntheticCx(float alphaRad)
        {
            return -0.02f - 0.1f * alphaRad * alphaRad;
        }

        private static float SyntheticCz(float alphaRad)
        {
            return -4f * alphaRad;
        }

        private static float SyntheticCm(float alphaRad, float elevatorRad, float cmElevatorPerRad)
        {
            return 0.02f - 0.4f * alphaRad + cmElevatorPerRad * elevatorRad;
        }

        /// <summary>
        /// A deliberately simple linear plant with a hand-checkable trim. Using a synthetic plant
        /// as well as the F-16 matters: it separates "the solver is correct" from "the F-16 data is
        /// correct", so a failure points at one or the other rather than at both.
        /// </summary>
        private static MavTrimPlant BuildSyntheticPlant(float cmElevatorPerRad, float thrustAtFullThrottleN)
        {
            MavTrimPlant plant = new MavTrimPlant();
            plant.plantId = "synthetic-linear-test-plant";
            plant.massKg = 9000f;
            plant.alphaMinDeg = -10f;
            plant.alphaMaxDeg = 45f;

            plant.referenceGeometry = new MavAeroReferenceGeometry
            {
                wingAreaM2 = 28f,
                wingSpanM = 9f,
                meanAerodynamicChordM = 3.5f
            };

            plant.controlSurfaceLimits = new MavControlSurfaceLimits
            {
                elevatorMinDeg = -25f,
                elevatorMaxDeg = 25f,
                aileronMinDeg = -20f,
                aileronMaxDeg = 20f,
                rudderMinDeg = -30f,
                rudderMaxDeg = 30f,
                leadingEdgeFlapMinDeg = 0f,
                leadingEdgeFlapMaxDeg = 0f
            };

            float cmDe = cmElevatorPerRad;
            plant.aeroFunction = delegate (float alphaRad, float betaRad, MavControlInput surfaces)
            {
                MavAeroCoefficients coefficients = new MavAeroCoefficients();
                coefficients.cx = SyntheticCx(alphaRad);
                coefficients.cz = SyntheticCz(alphaRad);
                coefficients.cm = SyntheticCm(alphaRad, surfaces.elevatorDeg * Mathf.Deg2Rad, cmDe);
                return coefficients;
            };

            float fullThrust = thrustAtFullThrottleN;
            plant.steadyPropulsionFunction = delegate (
                MavFlightState state,
                MavAtmosphereSample atmosphere,
                float throttle01)
            {
                MavPropulsiveLoads loads = MavPropulsiveLoads.Zero;
                float thrust = fullThrust * Mathf.Clamp01(throttle01);
                loads.forceAeroBodyN = new Vector3(thrust, 0f, 0f);
                loads.reportedThrustN = thrust;
                loads.powerState01 = Mathf.Clamp01(throttle01);
                loads.hasAuthoritativeData = true;
                return loads;
            };

            plant.propulsionDataAuthoritative = true;
            return plant;
        }

        /// <summary>
        /// Propulsive loads that satisfy the v0.1 axial/through-CG contract below a threshold alpha
        /// and violate it above one. This is the shape of model the old single-probe precheck could
        /// not catch: well-behaved where it was sampled, misbehaving where the answer is built.
        /// </summary>
        private static MavPropulsiveLoads StateDependentMomentLoads(
            float alphaDeg,
            float violationThresholdDeg,
            float fullThrustN,
            float throttle01)
        {
            float thrust = fullThrustN * Mathf.Clamp01(throttle01);

            MavPropulsiveLoads loads = MavPropulsiveLoads.Zero;
            loads.forceAeroBodyN = new Vector3(thrust, 0f, 0f);
            loads.reportedThrustN = thrust;
            loads.powerState01 = Mathf.Clamp01(throttle01);
            loads.hasAuthoritativeData = true;

            if (alphaDeg > violationThresholdDeg)
                loads.momentAeroBodyNm = new Vector3(0f, 5000f, 0f);

            return loads;
        }

        private static MavTrimPlant BuildStateDependentPropulsionPlant(
            float violationThresholdDeg,
            bool offAxisInsteadOfMoment)
        {
            MavTrimPlant plant = BuildSyntheticPlant(-0.8f, 60000f);
            float threshold = violationThresholdDeg;
            bool offAxis = offAxisInsteadOfMoment;

            plant.steadyPropulsionFunction = delegate (
                MavFlightState state,
                MavAtmosphereSample atmosphere,
                float throttle01)
            {
                float alphaDeg = state.AlphaDeg;

                if (!offAxis)
                    return StateDependentMomentLoads(alphaDeg, threshold, 60000f, throttle01);

                float thrust = 60000f * Mathf.Clamp01(throttle01);
                MavPropulsiveLoads loads = MavPropulsiveLoads.Zero;
                loads.forceAeroBodyN = alphaDeg > threshold
                    ? new Vector3(thrust, 0f, 0.05f * thrust)
                    : new Vector3(thrust, 0f, 0f);
                loads.reportedThrustN = thrust;
                loads.powerState01 = Mathf.Clamp01(throttle01);
                loads.hasAuthoritativeData = true;
                return loads;
            };

            return plant;
        }

        private static MavControlSurfaceLimits BuildF16SurfaceLimits()
        {
            return new MavControlSurfaceLimits
            {
                elevatorMinDeg = MavF16MorelliReference.ElevatorMinDeg,
                elevatorMaxDeg = MavF16MorelliReference.ElevatorMaxDeg,
                aileronMinDeg = MavF16MorelliReference.AileronMinDeg,
                aileronMaxDeg = MavF16MorelliReference.AileronMaxDeg,
                rudderMinDeg = MavF16MorelliReference.RudderMinDeg,
                rudderMaxDeg = MavF16MorelliReference.RudderMaxDeg,
                leadingEdgeFlapMinDeg = 0f,
                leadingEdgeFlapMaxDeg = 0f
            };
        }

        private static MavFlightState BuildState(
            float trueAirspeedMps,
            float alphaDeg,
            float betaDeg,
            Vector3 bodyRatesRadSec,
            float loadFactorNz,
            bool loadFactorValid)
        {
            MavAtmosphereSample atmosphere = MavAtmosphereModel.Sample(0f);
            float alphaRad = alphaDeg * Mathf.Deg2Rad;
            float betaRad = betaDeg * Mathf.Deg2Rad;

            MavFlightState state = new MavFlightState();
            state.trueAirspeedMps = trueAirspeedMps;
            state.mach = trueAirspeedMps / atmosphere.speedOfSoundMps;
            state.dynamicPressurePa = 0.5f * atmosphere.densityKgM3 * trueAirspeedMps * trueAirspeedMps;
            state.alphaRad = alphaRad;
            state.betaRad = betaRad;
            state.aeroBodyRatesRadSec = bodyRatesRadSec;
            state.aeroBodyVelocityMps = new Vector3(
                trueAirspeedMps * Mathf.Cos(alphaRad) * Mathf.Cos(betaRad),
                trueAirspeedMps * Mathf.Sin(betaRad),
                trueAirspeedMps * Mathf.Sin(alphaRad) * Mathf.Cos(betaRad)
            );

            // Body +Z is down, so an upward specific force is negative Z; Nz is its negation.
            state.specificForceAeroBodyG = new Vector3(0f, 0f, -loadFactorNz);
            state.specificForceValid = loadFactorValid;
            return state;
        }

        private static MavControlInput RunLaw(MavPilotCommand command, MavFlightState state)
        {
            MavF16ControlLawDebug debug;
            return RunLawWithDebug(command, state, out debug);
        }

        /// <summary>
        /// One control-law evaluation from a freshly zeroed law state, so each directional check is
        /// independent of every other and no integrator history leaks between assertions.
        /// </summary>
        private static MavControlInput RunLawWithDebug(
            MavPilotCommand command,
            MavFlightState state,
            out MavF16ControlLawDebug debug)
        {
            MavF16ControlLawState lawState = MavF16ControlLawState.Zero;
            return MavF16ControlLawV01.Compute(
                command,
                state,
                BuildF16SurfaceLimits(),
                MavF16ControlLawGains.Default,
                MavAngleOfAttackLimiterSettings.Default,
                MavLoadFactorLimiterSettings.Default,
                MavRollRateLimiterSettings.Default,
                ref lawState,
                0.02f,
                out debug
            );
        }

        private static MavAeroCoefficients EvaluateCoefficients(
            MavControlInput surfaces,
            float alphaDeg,
            float betaDeg)
        {
            MavAeroReferenceGeometry geometry = MavF16MorelliReference.CreateReferenceGeometry();
            return MavF16MorelliPolynomial.Evaluate(
                alphaDeg * Mathf.Deg2Rad,
                betaDeg * Mathf.Deg2Rad,
                surfaces.elevatorDeg * Mathf.Deg2Rad,
                surfaces.aileronDeg * Mathf.Deg2Rad,
                surfaces.rudderDeg * Mathf.Deg2Rad,
                0f,
                0f,
                0f,
                MavF16MassReference.XcgCbar,
                MavF16MassReference.XcgReferenceCbar,
                geometry.meanAerodynamicChordM / geometry.wingSpanM
            );
        }

        /// <summary>
        /// Full boundary evaluation: surfaces -> frozen Morelli coefficients -> dimensionalization
        /// -> Unity-local torque, mirroring exactly what MavSixDoFBody does per physics step. This
        /// is what turns "the sign in the law looks right" into "the aircraft actually moves that
        /// way".
        /// </summary>
        private static Vector3 UnityTorqueForSurfaces(MavControlInput surfaces, float speedMps)
        {
            MavAeroReferenceGeometry geometry = MavF16MorelliReference.CreateReferenceGeometry();
            MavAeroCoefficients coefficients = EvaluateCoefficients(surfaces, 0f, 0f);

            MavAtmosphereSample atmosphere = MavAtmosphereModel.Sample(0f);
            float dynamicPressurePa = 0.5f * atmosphere.densityKgM3 * speedMps * speedMps;

            MavAerodynamicLoads loads = MavFlightDynamicsMath.Dimensionalize(
                coefficients, geometry, dynamicPressurePa);

            return MavFlightDynamicsMath.AeroBodyMomentToUnityLocal(loads.momentAeroBodyNm);
        }

        private static bool Near(float actual, float expected, float tolerance = Tolerance)
        {
            return Mathf.Abs(actual - expected) <= tolerance;
        }

        private static void Record(bool ok, string name, StringBuilder report, ref int passed, ref int failed)
        {
            if (ok)
            {
                passed++;
                report.Append("  PASS  ").AppendLine(name);
            }
            else
            {
                failed++;
                report.Append("  FAIL  ").AppendLine(name);
            }
        }
    }
}
