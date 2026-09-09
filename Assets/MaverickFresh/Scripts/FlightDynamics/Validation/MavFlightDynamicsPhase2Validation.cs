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
    ///   [C0] pitch / roll / yaw command direction through the frozen aerodynamic model
    ///   [C1] coordinated-yaw and yaw-damper behaviour
    ///   [L0] load-factor limiter
    ///   [L1] angle-of-attack limiter
    ///   [L2] roll-rate limiter
    ///   [L3] smooth-limiter mathematics
    ///   [L4] integrator anti-windup
    ///   [R0] STRUCTURALLY_PREPARED versus OPERATIONALLY_LIVE_READY
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
            ValidateControlDirections(report, ref passed, ref failed);
            ValidateCoordinatedYaw(report, ref passed, ref failed);
            ValidateLoadFactorLimiter(report, ref passed, ref failed);
            ValidateAngleOfAttackLimiter(report, ref passed, ref failed);
            ValidateRollRateLimiter(report, ref passed, ref failed);
            ValidateSmoothLimiterMath(report, ref passed, ref failed);
            ValidateIntegratorAntiWindup(report, ref passed, ref failed);
            ValidateReadinessSeparation(report, ref passed, ref failed);
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
                !MavManualPilotCommandSource.EvaluatesAsOperationalSource(
                    MavManualPilotCommandSource.DefaultTreatAsOperationalSource, true),
                "a default manual command source does not claim to be an operational input path",
                report, ref passed, ref failed);

            Record(
                !MavManualPilotCommandSource.EvaluatesAsOperationalSource(true, false),
                "an acknowledged manual source still fails while it is producing no commands",
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
