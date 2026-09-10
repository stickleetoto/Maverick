using System.Text;
using UnityEngine;
using MaverickFresh.FlightDynamics.F16;

namespace MaverickFresh.FlightDynamics.Validation
{
    /// <summary>
    /// Deterministic, side-effect-free checks for the Phase 1 live-FDM infrastructure.
    ///
    /// Every check here exercises the exact production code path through pure static entry
    /// points, so no GameObject, Rigidbody, scene, or play-mode session is created or touched.
    ///
    /// Covered:
    ///   [P0] normalized pilot command clamping
    ///   [P1] C0 control-law surface mapping, signs, limits, and asymmetric ranges
    ///   [P2] load summation (aero + propulsion summed exactly once)
    ///   [P3] duplicate-load prevention
    ///   [P4] null propulsion honesty (zero loads, non-authoritative flag, power-state plumbing)
    ///   [P5] live-FDM readiness gate
    ///   [P6] telemetry CSV header/row column agreement
    ///   [P7] Unity/aero axial-vector direction contract
    ///   [P8] physical damping direction through the whole boundary
    /// </summary>
    public static class MavFlightDynamicsPhase1Validation
    {
        private const float DegTolerance = 1e-4f;
        private const float ForceTolerance = 1e-3f;

        public static string RunAll(out int passed, out int failed)
        {
            passed = 0;
            failed = 0;
            StringBuilder report = new StringBuilder(4096);
            report.AppendLine("Maverick Flight Dynamics Phase 1 Validation");
            report.AppendLine("===========================================");

            ValidatePilotCommand(report, ref passed, ref failed);
            ValidateControlMapping(report, ref passed, ref failed);
            ValidateLoadSummation(report, ref passed, ref failed);
            ValidateDuplicateLoadPrevention(report, ref passed, ref failed);
            ValidateNullPropulsion(report, ref passed, ref failed);
            ValidateReadinessGate(report, ref passed, ref failed);
            ValidateTelemetryCsvShape(report, ref passed, ref failed);
            ValidateAxialVectorDirections(report, ref passed, ref failed);
            ValidateDampingDirections(report, ref passed, ref failed);

            report.AppendLine();
            report.Append("RESULT: ")
                .Append(failed == 0 ? "PASS" : "FAIL")
                .Append("  passed=").Append(passed)
                .Append(" failed=").Append(failed);

            return report.ToString();
        }

        // ---------------------------------------------------------------- [P0]

        private static void ValidatePilotCommand(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P0] Normalized pilot command");

            MavPilotCommand neutral = MavPilotCommand.Neutral;
            Record(
                neutral.pitch == 0f && neutral.roll == 0f && neutral.yaw == 0f && neutral.throttle01 == 0f,
                "Neutral is all-zero (no accidental default demand)",
                report, ref passed, ref failed
            );

            MavPilotCommand wild = new MavPilotCommand
            {
                pitch = 4.5f,
                roll = -9f,
                yaw = 2f,
                throttle01 = 7f
            };
            MavPilotCommand clamped = wild.Clamped();

            Record(
                Near(clamped.pitch, 1f, DegTolerance)
                && Near(clamped.roll, -1f, DegTolerance)
                && Near(clamped.yaw, 1f, DegTolerance)
                && Near(clamped.throttle01, 1f, DegTolerance),
                "out-of-range channels clamp to -1..1 / 0..1",
                report, ref passed, ref failed
            );

            MavPilotCommand negativeThrottle = new MavPilotCommand { throttle01 = -3f };
            Record(
                Near(negativeThrottle.Clamped().throttle01, 0f, DegTolerance),
                "negative throttle clamps to idle, not to reverse thrust",
                report, ref passed, ref failed
            );

            Record(
                MavPilotCommand.Neutral.IsNeutral(1e-6f),
                "IsNeutral detects a neutral command",
                report, ref passed, ref failed
            );
        }

        // ---------------------------------------------------------------- [P1]

        private static void ValidateControlMapping(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P1] C0 direct-surface control-law mapping");

            MavControlSurfaceLimits f16Limits = BuildF16SurfaceLimits();

            // Neutral stick must produce exactly zero deflection: no trim bias hidden in the law.
            MavControlInput neutral = MapWithDefaultSigns(MavPilotCommand.Neutral, f16Limits);
            Record(
                Near(neutral.elevatorDeg, 0f, DegTolerance)
                && Near(neutral.aileronDeg, 0f, DegTolerance)
                && Near(neutral.rudderDeg, 0f, DegTolerance),
                "neutral command -> zero deflection on every axis",
                report, ref passed, ref failed
            );

            // Full nose-up demand. The frozen Morelli convention has positive elevator producing a
            // negative pitching moment, so nose-up must command NEGATIVE elevator deflection.
            MavControlInput noseUp = MapWithDefaultSigns(
                new MavPilotCommand { pitch = 1f }, f16Limits
            );
            Record(
                Near(noseUp.elevatorDeg, MavF16MorelliReference.ElevatorMinDeg, DegTolerance),
                "pitch +1 (nose up) -> elevator at negative limit ("
                + MavF16MorelliReference.ElevatorMinDeg.ToString("F1") + " deg)",
                report, ref passed, ref failed
            );

            MavControlInput noseDown = MapWithDefaultSigns(
                new MavPilotCommand { pitch = -1f }, f16Limits
            );
            Record(
                Near(noseDown.elevatorDeg, MavF16MorelliReference.ElevatorMaxDeg, DegTolerance),
                "pitch -1 (nose down) -> elevator at positive limit",
                report, ref passed, ref failed
            );

            MavControlInput rollRight = MapWithDefaultSigns(
                new MavPilotCommand { roll = 1f }, f16Limits
            );
            Record(
                Near(rollRight.aileronDeg, MavF16MorelliReference.AileronMinDeg, DegTolerance),
                "roll +1 (roll right) -> aileron at negative limit",
                report, ref passed, ref failed
            );

            MavControlInput yawRight = MapWithDefaultSigns(
                new MavPilotCommand { yaw = 1f }, f16Limits
            );
            Record(
                Near(yawRight.rudderDeg, MavF16MorelliReference.RudderMinDeg, DegTolerance),
                "yaw +1 (nose right) -> rudder at negative limit",
                report, ref passed, ref failed
            );

            // Sign consistency: the mapped deflections must actually produce moments in the
            // demanded direction when fed through the frozen aerodynamic model.
            ValidateMappedSignProducesDemandedMoment(report, ref passed, ref failed, f16Limits);

            // Half demand must be exactly half deflection: the C0 law is linear by definition.
            MavControlInput halfUp = MapWithDefaultSigns(
                new MavPilotCommand { pitch = 0.5f }, f16Limits
            );
            Record(
                Near(halfUp.elevatorDeg, 0.5f * MavF16MorelliReference.ElevatorMinDeg, DegTolerance),
                "pitch 0.5 -> exactly half of the negative elevator limit (linear mapping)",
                report, ref passed, ref failed
            );

            // Out-of-range demand must saturate at the physical limit, never beyond it.
            MavControlInput overDriven = MapWithDefaultSigns(
                new MavPilotCommand { pitch = 12f, roll = -12f, yaw = 12f }, f16Limits
            );
            Record(
                overDriven.elevatorDeg >= MavF16MorelliReference.ElevatorMinDeg - DegTolerance
                && overDriven.elevatorDeg <= MavF16MorelliReference.ElevatorMaxDeg + DegTolerance
                && overDriven.aileronDeg >= MavF16MorelliReference.AileronMinDeg - DegTolerance
                && overDriven.aileronDeg <= MavF16MorelliReference.AileronMaxDeg + DegTolerance
                && overDriven.rudderDeg >= MavF16MorelliReference.RudderMinDeg - DegTolerance
                && overDriven.rudderDeg <= MavF16MorelliReference.RudderMaxDeg + DegTolerance,
                "out-of-range demand saturates inside the physical surface limits",
                report, ref passed, ref failed
            );

            // Asymmetric limits must map each direction to its own limit.
            Record(
                Near(MavDirectSurfaceControlLaw.MapToLimits(1f, -30f, 10f), 10f, DegTolerance)
                && Near(MavDirectSurfaceControlLaw.MapToLimits(-1f, -30f, 10f), -30f, DegTolerance)
                && Near(MavDirectSurfaceControlLaw.MapToLimits(-0.5f, -30f, 10f), -15f, DegTolerance),
                "asymmetric limits map each direction to its own limit",
                report, ref passed, ref failed
            );

            Record(
                Near(MavDirectSurfaceControlLaw.MapToLimits(-1f, 5f, 10f), 0f, DegTolerance),
                "an inverted (positive) minimum yields 0 rather than an illegal deflection",
                report, ref passed, ref failed
            );

            // Authority fraction is test-law tuning and must scale deflection, nothing else.
            MavControlInput halfAuthority = MavDirectSurfaceControlLaw.Map(
                new MavPilotCommand { pitch = 1f }, f16Limits,
                0.5f, 1f, 1f, -1f, -1f, -1f
            );
            Record(
                Near(halfAuthority.elevatorDeg, 0.5f * MavF16MorelliReference.ElevatorMinDeg, DegTolerance),
                "pitch authority 0.5 halves commanded elevator deflection",
                report, ref passed, ref failed
            );

            // Throttle is intent pass-through, and the C0 law never schedules LEF.
            MavControlInput throttled = MapWithDefaultSigns(
                new MavPilotCommand { throttle01 = 0.62f }, f16Limits
            );
            Record(
                Near(throttled.throttle01, 0.62f, DegTolerance),
                "throttle passes through unmodified",
                report, ref passed, ref failed
            );
            Record(
                Near(throttled.leadingEdgeFlapDeg, 0f, DegTolerance),
                "leading-edge flap stays 0 (unsupported by the compact reference model)",
                report, ref passed, ref failed
            );

            // The profile's Clamp must be a no-op on the law's own output: mapping already respects limits.
            MavControlInput full = MapWithDefaultSigns(
                new MavPilotCommand { pitch = 1f, roll = -1f, yaw = 1f, throttle01 = 1f }, f16Limits
            );
            MavControlInput reclamped = f16Limits.Clamp(full);
            Record(
                Near(full.elevatorDeg, reclamped.elevatorDeg, DegTolerance)
                && Near(full.aileronDeg, reclamped.aileronDeg, DegTolerance)
                && Near(full.rudderDeg, reclamped.rudderDeg, DegTolerance),
                "profile re-clamping the law output changes nothing (limits already honoured)",
                report, ref passed, ref failed
            );
        }

        /// <summary>
        /// Confirms the C0 mapping is not merely sign-consistent with a comment but actually drives
        /// the frozen aerodynamic model in the demanded direction. This is the check that catches an
        /// inverted control axis before it reaches a Rigidbody.
        /// </summary>
        private static void ValidateMappedSignProducesDemandedMoment(
            StringBuilder report,
            ref int passed,
            ref int failed,
            MavControlSurfaceLimits limits)
        {
            float chordOverSpan = MavF16MorelliReference.MeanAerodynamicChordM
                                  / MavF16MorelliReference.WingSpanM;

            MavControlInput noseUp = MapWithDefaultSigns(new MavPilotCommand { pitch = 1f }, limits);
            MavAeroCoefficients pitchUp = EvaluateAt(noseUp, chordOverSpan);
            MavAeroCoefficients pitchNeutral = EvaluateAt(
                MapWithDefaultSigns(MavPilotCommand.Neutral, limits), chordOverSpan
            );

            // Cm positive = nose up in the conventional body-axis convention.
            Record(
                pitchUp.cm > pitchNeutral.cm,
                "nose-up demand increases Cm (pitch-up moment) in the frozen aero model",
                report, ref passed, ref failed
            );

            MavControlInput rollRight = MapWithDefaultSigns(new MavPilotCommand { roll = 1f }, limits);
            MavAeroCoefficients rollCoefficients = EvaluateAt(rollRight, chordOverSpan);

            // Cl positive = right wing down in the conventional body-axis convention.
            Record(
                rollCoefficients.cl > 0f,
                "roll-right demand produces positive Cl (right-wing-down moment)",
                report, ref passed, ref failed
            );

            MavControlInput yawRight = MapWithDefaultSigns(new MavPilotCommand { yaw = 1f }, limits);
            MavAeroCoefficients yawCoefficients = EvaluateAt(yawRight, chordOverSpan);

            // Cn positive = nose right in the conventional body-axis convention.
            Record(
                yawCoefficients.cn > 0f,
                "nose-right demand produces positive Cn (nose-right moment)",
                report, ref passed, ref failed
            );
        }

        // ---------------------------------------------------------------- [P2]

        private static void ValidateLoadSummation(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P2] Load summation");

            MavAerodynamicLoads aero = new MavAerodynamicLoads
            {
                forceAeroBodyN = new Vector3(-1200f, 300f, -45000f),
                momentAeroBodyNm = new Vector3(5000f, -22000f, 1500f)
            };

            MavPropulsiveLoads propulsion = new MavPropulsiveLoads
            {
                forceAeroBodyN = new Vector3(60000f, 0f, 0f),
                momentAeroBodyNm = new Vector3(0f, -800f, 0f),
                reportedThrustN = 60000f,
                powerState01 = 1f,
                hasAuthoritativeData = true
            };

            MavFlightDynamicsLoadSet set = new MavFlightDynamicsLoadSet();
            set.BeginStep(1);

            Record(
                set.totalForceAeroBodyN == Vector3.zero
                && set.totalMomentAeroBodyNm == Vector3.zero
                && !set.HasAerodynamic && !set.HasPropulsive && !set.applied,
                "BeginStep produces a fully cleared accumulator",
                report, ref passed, ref failed
            );

            Record(set.AddAerodynamic(aero), "first aerodynamic contribution accepted", report, ref passed, ref failed);
            Record(set.AddPropulsive(propulsion), "first propulsive contribution accepted", report, ref passed, ref failed);

            Vector3 expectedForce = aero.forceAeroBodyN + propulsion.forceAeroBodyN;
            Vector3 expectedMoment = aero.momentAeroBodyNm + propulsion.momentAeroBodyNm;

            Record(
                NearVector(set.totalForceAeroBodyN, expectedForce, ForceTolerance),
                "total force = aero + propulsion, summed once",
                report, ref passed, ref failed
            );
            Record(
                NearVector(set.totalMomentAeroBodyNm, expectedMoment, ForceTolerance),
                "total moment = aero + propulsion, summed once",
                report, ref passed, ref failed
            );
            Record(
                set.aerodynamicContributions == 1 && set.propulsiveContributions == 1,
                "one contribution recorded per physical source",
                report, ref passed, ref failed
            );
            Record(set.HasSingleOwnerPerSource, "single owner per source", report, ref passed, ref failed);

            // Aero-only: an aircraft with no propulsion model must total exactly the aero load.
            MavFlightDynamicsLoadSet aeroOnly = new MavFlightDynamicsLoadSet();
            aeroOnly.BeginStep(2);
            aeroOnly.AddAerodynamic(aero);
            Record(
                NearVector(aeroOnly.totalForceAeroBodyN, aero.forceAeroBodyN, ForceTolerance)
                && NearVector(aeroOnly.totalMomentAeroBodyNm, aero.momentAeroBodyNm, ForceTolerance)
                && !aeroOnly.HasPropulsive,
                "with no propulsion contribution the total equals the aerodynamic load exactly",
                report, ref passed, ref failed
            );

            // A new step must not inherit the previous step's total.
            aeroOnly.BeginStep(3);
            Record(
                aeroOnly.totalForceAeroBodyN == Vector3.zero
                && aeroOnly.totalMomentAeroBodyNm == Vector3.zero,
                "a new step never inherits the previous step's total",
                report, ref passed, ref failed
            );

            // NaN must be caught before it can reach a Rigidbody.
            MavFlightDynamicsLoadSet poisoned = new MavFlightDynamicsLoadSet();
            poisoned.BeginStep(4);
            poisoned.AddAerodynamic(new MavAerodynamicLoads
            {
                forceAeroBodyN = new Vector3(float.NaN, 0f, 0f),
                momentAeroBodyNm = Vector3.zero
            });
            Record(!poisoned.IsFinite(), "a NaN load is reported as non-finite", report, ref passed, ref failed);

            MavFlightDynamicsLoadSet infinite = new MavFlightDynamicsLoadSet();
            infinite.BeginStep(5);
            infinite.AddAerodynamic(new MavAerodynamicLoads
            {
                forceAeroBodyN = Vector3.zero,
                momentAeroBodyNm = new Vector3(0f, float.PositiveInfinity, 0f)
            });
            Record(!infinite.IsFinite(), "an infinite moment is reported as non-finite", report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [P3]

        private static void ValidateDuplicateLoadPrevention(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P3] Duplicate load prevention");

            MavAerodynamicLoads aero = new MavAerodynamicLoads
            {
                forceAeroBodyN = new Vector3(10f, 20f, 30f),
                momentAeroBodyNm = new Vector3(1f, 2f, 3f)
            };
            MavPropulsiveLoads propulsion = new MavPropulsiveLoads
            {
                forceAeroBodyN = new Vector3(100f, 0f, 0f),
                momentAeroBodyNm = Vector3.zero
            };

            MavFlightDynamicsLoadSet set = new MavFlightDynamicsLoadSet();
            set.BeginStep(1);
            set.AddAerodynamic(aero);
            set.AddPropulsive(propulsion);

            Vector3 totalAfterFirst = set.totalForceAeroBodyN;

            Record(
                !set.AddAerodynamic(aero),
                "a second aerodynamic contribution in the same step is refused",
                report, ref passed, ref failed
            );
            Record(
                !set.AddPropulsive(propulsion),
                "a second propulsive contribution in the same step is refused",
                report, ref passed, ref failed
            );
            Record(
                NearVector(set.totalForceAeroBodyN, totalAfterFirst, ForceTolerance),
                "refused duplicates do not change the accumulated total",
                report, ref passed, ref failed
            );
            Record(
                !set.HasSingleOwnerPerSource,
                "duplicate attempts are recorded so the ownership bug is visible",
                report, ref passed, ref failed
            );

            string reason;
            Record(
                !set.TryMarkApplied(out reason),
                "application is refused while duplicate contributions are present: " + reason,
                report, ref passed, ref failed
            );

            // A clean set applies exactly once.
            MavFlightDynamicsLoadSet clean = new MavFlightDynamicsLoadSet();
            clean.BeginStep(2);
            clean.AddAerodynamic(aero);
            clean.AddPropulsive(propulsion);

            Record(clean.TryMarkApplied(out reason), "a clean load set applies once", report, ref passed, ref failed);
            Record(
                !clean.TryMarkApplied(out reason),
                "the same load set cannot be applied twice: " + reason,
                report, ref passed, ref failed
            );
            Record(
                !clean.AddAerodynamic(aero) && !clean.AddPropulsive(propulsion),
                "an already-applied set accepts no further contributions",
                report, ref passed, ref failed
            );

            // The body-level guard: two applications at the same fixed time must be refused.
            Record(
                !MavSixDoFBody.ShouldRejectDuplicateApplication(float.NegativeInfinity, 0f),
                "the first application of a session is allowed",
                report, ref passed, ref failed
            );
            Record(
                MavSixDoFBody.ShouldRejectDuplicateApplication(1.25f, 1.25f),
                "a second application at the same fixed time is refused",
                report, ref passed, ref failed
            );
            Record(
                !MavSixDoFBody.ShouldRejectDuplicateApplication(1.25f, 1.27f),
                "application in the next physics step is allowed",
                report, ref passed, ref failed
            );
        }

        // ---------------------------------------------------------------- [P4]

        private static void ValidateNullPropulsion(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P4] Null propulsion honesty");

            MavPropulsiveLoads idle = MavNullPropulsionModel.EvaluateZeroThrust(0f);
            MavPropulsiveLoads full = MavNullPropulsionModel.EvaluateZeroThrust(1f);

            Record(
                idle.forceAeroBodyN == Vector3.zero && idle.momentAeroBodyNm == Vector3.zero,
                "zero thrust at idle power",
                report, ref passed, ref failed
            );
            Record(
                full.forceAeroBodyN == Vector3.zero && full.momentAeroBodyNm == Vector3.zero,
                "zero thrust at full power (no invented thrust map)",
                report, ref passed, ref failed
            );
            Record(
                Near(full.reportedThrustN, 0f, ForceTolerance),
                "reported thrust is 0 N",
                report, ref passed, ref failed
            );
            Record(
                !idle.hasAuthoritativeData && !full.hasAuthoritativeData,
                "null propulsion reports hasAuthoritativeData = false",
                report, ref passed, ref failed
            );
            Record(
                Near(full.powerState01, 1f, DegTolerance),
                "power state is still tracked and reported for inspection",
                report, ref passed, ref failed
            );

            // Contributing a null propulsion load must not change the total in any way.
            MavAerodynamicLoads aero = new MavAerodynamicLoads
            {
                forceAeroBodyN = new Vector3(-500f, 25f, -30000f),
                momentAeroBodyNm = new Vector3(120f, -4000f, 60f)
            };

            MavFlightDynamicsLoadSet withNull = new MavFlightDynamicsLoadSet();
            withNull.BeginStep(1);
            withNull.AddAerodynamic(aero);
            withNull.AddPropulsive(MavNullPropulsionModel.EvaluateZeroThrust(1f));

            Record(
                NearVector(withNull.totalForceAeroBodyN, aero.forceAeroBodyN, ForceTolerance)
                && NearVector(withNull.totalMomentAeroBodyNm, aero.momentAeroBodyNm, ForceTolerance),
                "a null propulsion contribution leaves the total identical to aero-only",
                report, ref passed, ref failed
            );
            Record(
                withNull.HasPropulsive && withNull.propulsiveContributions == 1,
                "the null contribution is still counted, so its ownership stays visible",
                report, ref passed, ref failed
            );

            // Power-state plumbing.
            Record(
                Near(MavNullPropulsionModel.StepPowerState(0f, 1f, 0f, 0.02f), 1f, DegTolerance),
                "zero lag snaps power state to the commanded value",
                report, ref passed, ref failed
            );
            Record(
                Near(MavNullPropulsionModel.StepPowerState(0f, 1f, 1f, 0f), 1f, DegTolerance),
                "zero dt snaps power state instead of dividing by zero",
                report, ref passed, ref failed
            );

            float lagged = MavNullPropulsionModel.StepPowerState(0f, 1f, 1f, 0.02f);
            Record(
                lagged > 0f && lagged < 1f,
                "a positive lag produces a bounded intermediate power state",
                report, ref passed, ref failed
            );
            Record(
                Near(MavNullPropulsionModel.StepPowerState(0.5f, 5f, 1f, 0.02f), 0.51f, 0.02f),
                "commanded power state is clamped into 0..1 before blending",
                report, ref passed, ref failed
            );
        }

        // ---------------------------------------------------------------- [P5]

        private static void ValidateReadinessGate(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P5] Live-FDM readiness gate");

            string reason;

            Record(
                MavSixDoFBody.EvaluateReadiness(true, true, true, true, true, true, out reason)
                && reason == "READY",
                "a fully wired stack reports READY",
                report, ref passed, ref failed
            );

            Record(
                !MavSixDoFBody.EvaluateReadiness(false, true, true, true, true, true, out reason),
                "missing Rigidbody blocks readiness (" + reason + ")",
                report, ref passed, ref failed
            );
            Record(
                !MavSixDoFBody.EvaluateReadiness(true, false, true, true, true, true, out reason),
                "invalid physical profile blocks readiness (" + reason + ")",
                report, ref passed, ref failed
            );
            Record(
                !MavSixDoFBody.EvaluateReadiness(true, true, false, true, true, true, out reason),
                "missing aerodynamic model blocks readiness (" + reason + ")",
                report, ref passed, ref failed
            );
            Record(
                !MavSixDoFBody.EvaluateReadiness(true, true, true, false, true, true, out reason),
                "missing actuator blocks readiness (" + reason + ")",
                report, ref passed, ref failed
            );
            Record(
                !MavSixDoFBody.EvaluateReadiness(true, true, true, true, false, true, out reason),
                "missing control law blocks readiness (" + reason + ")",
                report, ref passed, ref failed
            );
            Record(
                !MavSixDoFBody.EvaluateReadiness(true, true, true, true, true, false, out reason),
                "missing propulsion model blocks readiness (" + reason + ")",
                report, ref passed, ref failed
            );
        }

        // ---------------------------------------------------------------- [P6]

        private static void ValidateTelemetryCsvShape(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P6] Telemetry CSV shape");

            // A real sample, with a distinct value in every field so a mis-ordered column shows up
            // as a wrong value rather than merely a wrong count.
            MavFlightDynamicsTelemetrySample sample = new MavFlightDynamicsTelemetrySample();
            sample.timeSeconds = 1f;
            sample.altitudeM = 2f;
            sample.trueAirspeedMps = 3f;
            sample.mach = 4f;
            sample.dynamicPressurePa = 5f;
            sample.alphaDeg = 6f;
            sample.betaDeg = 7f;
            sample.rollRateDegSec = 8f;
            sample.pitchRateDegSec = 9f;
            sample.yawRateDegSec = 10f;
            sample.commandedSurfaces = new MavControlInput
            {
                elevatorDeg = 11f, aileronDeg = 12f, rudderDeg = 13f
            };
            sample.actualSurfaces = new MavControlInput
            {
                elevatorDeg = 14f, aileronDeg = 15f, rudderDeg = 16f, throttle01 = 17f
            };
            sample.coefficients = new MavAeroCoefficients
            {
                cx = 18f, cy = 19f, cz = 20f, cl = 21f, cm = 22f, cn = 23f
            };
            sample.aeroForceAeroBodyN = new Vector3(24f, 25f, 26f);
            sample.aeroMomentAeroBodyNm = new Vector3(27f, 28f, 29f);
            sample.propulsionForceAeroBodyN = new Vector3(30f, 0f, 0f);
            sample.totalForceAeroBodyN = new Vector3(31f, 32f, 33f);
            sample.loadFactorNz = 34f;
            sample.profileValid = true;
            sample.insideEnvelope = false;
            sample.loadsApplied = true;
            sample.propulsionDataAuthoritative = false;
            sample.aerodynamicContributions = 1;
            sample.propulsiveContributions = 1;
            sample.readinessLevel = 2;

            // Serialize through the SAME path that writes rows to disk.
            string[] headerColumns = MavFlightDynamicsTelemetry.CsvHeader().Split(',');
            string[] rowColumns = MavFlightDynamicsTelemetry.BuildCsvRow(sample).Split(',');

            Record(
                headerColumns.Length == rowColumns.Length,
                "an ACTUAL serialized row has the same column count as the header ("
                + rowColumns.Length + " vs " + headerColumns.Length + ")",
                report, ref passed, ref failed);

            Record(
                headerColumns.Length == MavFlightDynamicsTelemetry.CsvNumericColumnCount
                                        + MavFlightDynamicsTelemetry.CsvFlagColumnCount,
                "and both agree with the declared column constants",
                report, ref passed, ref failed);

            bool allNamed = true;
            for (int i = 0; i < headerColumns.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(headerColumns[i]))
                {
                    allNamed = false;
                    break;
                }
            }
            Record(allNamed, "every CSV column has a name", report, ref passed, ref failed);

            // Column ORDER, checked by name. The values above were chosen so each column's expected
            // content is unambiguous; a swapped pair changes a value, not just a count.
            Record(
                ColumnValueMatches(headerColumns, rowColumns, "t_s", 1f)
                && ColumnValueMatches(headerColumns, rowColumns, "alt_m", 2f)
                && ColumnValueMatches(headerColumns, rowColumns, "alpha_deg", 6f)
                && ColumnValueMatches(headerColumns, rowColumns, "beta_deg", 7f),
                "leading state columns carry the values their headers name",
                report, ref passed, ref failed);

            Record(
                ColumnValueMatches(headerColumns, rowColumns, "cmd_elev_deg", 11f)
                && ColumnValueMatches(headerColumns, rowColumns, "act_elev_deg", 14f)
                && ColumnValueMatches(headerColumns, rowColumns, "act_throttle01", 17f),
                "commanded and actual surface columns are not transposed",
                report, ref passed, ref failed);

            Record(
                ColumnValueMatches(headerColumns, rowColumns, "CX", 18f)
                && ColumnValueMatches(headerColumns, rowColumns, "Cn", 23f)
                && ColumnValueMatches(headerColumns, rowColumns, "aeroFx_n", 24f)
                && ColumnValueMatches(headerColumns, rowColumns, "aeroN_nm", 29f),
                "coefficient and aerodynamic-load columns are in header order",
                report, ref passed, ref failed);

            Record(
                ColumnValueMatches(headerColumns, rowColumns, "propFx_n", 30f)
                && ColumnValueMatches(headerColumns, rowColumns, "totFx_n", 31f)
                && ColumnValueMatches(headerColumns, rowColumns, "totFz_n", 33f)
                && ColumnValueMatches(headerColumns, rowColumns, "nz_g", 34f),
                "propulsion, total-load and load-factor columns are in header order",
                report, ref passed, ref failed);

            Record(
                ColumnValueMatches(headerColumns, rowColumns, "profileValid", 1f)
                && ColumnValueMatches(headerColumns, rowColumns, "insideEnvelope", 0f)
                && ColumnValueMatches(headerColumns, rowColumns, "loadsApplied", 1f)
                && ColumnValueMatches(headerColumns, rowColumns, "propulsionSourced", 0f)
                && ColumnValueMatches(headerColumns, rowColumns, "readinessLevel", 2f),
                "flag columns follow the numeric ones, in header order, with the right values",
                report, ref passed, ref failed);

            Record(
                IndexOfColumn(headerColumns, "nz_g")
                    < IndexOfColumn(headerColumns, "profileValid"),
                "every numeric column precedes every flag column, as the writer assumes",
                report, ref passed, ref failed);
        }

        private static int IndexOfColumn(string[] headerColumns, string name)
        {
            for (int i = 0; i < headerColumns.Length; i++)
            {
                if (headerColumns[i] == name)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Looks up a column by header name and compares the serialized row's value at that index.
        /// This is what makes the check an ORDER check rather than a count check.
        /// </summary>
        private static bool ColumnValueMatches(
            string[] headerColumns,
            string[] rowColumns,
            string columnName,
            float expected)
        {
            int index = IndexOfColumn(headerColumns, columnName);
            if (index < 0 || index >= rowColumns.Length)
                return false;

            float actual;
            if (!float.TryParse(
                    rowColumns[index],
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out actual))
            {
                return false;
            }

            return Mathf.Abs(actual - expected) <= 1e-4f;
        }

        // ---------------------------------------------------------------- [P7]

        /// <summary>
        /// Directional contract for the Unity / aerodynamic axis boundary.
        ///
        /// The two bases differ in handedness, so the component change of basis has determinant -1.
        /// A true vector (force, velocity, position) transforms with the basis change alone, while an
        /// axial vector (moment, angular rate) transforms with the basis change multiplied by that
        /// determinant, picking up an extra sign on every axis.
        ///
        /// These checks assert physical directions, not algebraic shape. A round-trip check is kept
        /// below, but deliberately not relied on as proof: applying the same wrong sign on the way
        /// out and on the way back in cancels, so a round trip cannot detect a shared handedness
        /// error. That is exactly the failure this section exists to catch.
        /// </summary>
        private static void ValidateAxialVectorDirections(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P7] Unity/aero axial-vector direction contract");

            // Images of the Unity basis vectors in aerodynamic body axes.
            Vector3 ex = MavFlightDynamicsMath.UnityLocalVectorToAeroBody(new Vector3(1f, 0f, 0f));
            Vector3 ey = MavFlightDynamicsMath.UnityLocalVectorToAeroBody(new Vector3(0f, 1f, 0f));
            Vector3 ez = MavFlightDynamicsMath.UnityLocalVectorToAeroBody(new Vector3(0f, 0f, 1f));

            float determinant =
                ex.x * (ey.y * ez.z - ey.z * ez.y)
                - ex.y * (ey.x * ez.z - ey.z * ez.x)
                + ex.z * (ey.x * ez.y - ey.y * ez.x);

            Record(
                Near(determinant, -1f, 1e-5f),
                "Unity <-> aero basis change has determinant -1 (the two frames differ in handedness)",
                report, ref passed, ref failed
            );

            // --- true vectors keep the plain basis change ---
            Record(
                NearVector(
                    MavFlightDynamicsMath.AeroBodyVectorToUnityLocal(new Vector3(1f, 0f, 0f)),
                    new Vector3(0f, 0f, 1f), 1e-5f)
                && NearVector(
                    MavFlightDynamicsMath.AeroBodyVectorToUnityLocal(new Vector3(0f, 1f, 0f)),
                    new Vector3(1f, 0f, 0f), 1e-5f)
                && NearVector(
                    MavFlightDynamicsMath.AeroBodyVectorToUnityLocal(new Vector3(0f, 0f, 1f)),
                    new Vector3(0f, -1f, 0f), 1e-5f),
                "aerodynamic force axes map as a true vector (forward/right/down -> forward/right/-up)",
                report, ref passed, ref failed
            );

            // --- moment out: L/M/N -> Unity local torque, asserted as physical directions ---
            // In Unity a positive rotation about +X pitches the nose DOWN, about +Y yaws the nose
            // RIGHT, and about +Z rolls LEFT. So roll-right and nose-up must map to NEGATIVE Unity
            // torque on their axes, and nose-right must map to POSITIVE Unity Y torque.
            Record(
                NearVector(
                    MavFlightDynamicsMath.AeroBodyMomentToUnityLocal(new Vector3(1f, 0f, 0f)),
                    new Vector3(0f, 0f, -1f), 1e-5f),
                "L +1 (roll right) -> Unity local torque -Z (roll right in Unity)",
                report, ref passed, ref failed
            );
            Record(
                NearVector(
                    MavFlightDynamicsMath.AeroBodyMomentToUnityLocal(new Vector3(0f, 1f, 0f)),
                    new Vector3(-1f, 0f, 0f), 1e-5f),
                "M +1 (nose up) -> Unity local torque -X (nose up in Unity)",
                report, ref passed, ref failed
            );
            Record(
                NearVector(
                    MavFlightDynamicsMath.AeroBodyMomentToUnityLocal(new Vector3(0f, 0f, 1f)),
                    new Vector3(0f, 1f, 0f), 1e-5f),
                "N +1 (nose right) -> Unity local torque +Y (nose right in Unity)",
                report, ref passed, ref failed
            );

            // --- angular rate in: Unity local angular velocity -> p/q/r, asserted as directions ---
            Record(
                NearVector(
                    MavFlightDynamicsMath.UnityLocalAngularRateToAeroBody(new Vector3(0f, 0f, -1f)),
                    new Vector3(1f, 0f, 0f), 1e-5f),
                "Unity -Z rate (rolling right) -> p +1",
                report, ref passed, ref failed
            );
            Record(
                NearVector(
                    MavFlightDynamicsMath.UnityLocalAngularRateToAeroBody(new Vector3(-1f, 0f, 0f)),
                    new Vector3(0f, 1f, 0f), 1e-5f),
                "Unity -X rate (pitching nose up) -> q +1",
                report, ref passed, ref failed
            );
            Record(
                NearVector(
                    MavFlightDynamicsMath.UnityLocalAngularRateToAeroBody(new Vector3(0f, 1f, 0f)),
                    new Vector3(0f, 0f, 1f), 1e-5f),
                "Unity +Y rate (yawing nose right) -> r +1",
                report, ref passed, ref failed
            );

            // --- the axial helpers must carry the determinant sign, i.e. differ from the true-vector
            //     mapping on every axis. This is the specific regression that guards against someone
            //     collapsing the axial helpers back into the true-vector helpers.
            Vector3 probe = new Vector3(1f, 2f, 3f);
            Vector3 trueVectorMapped = MavFlightDynamicsMath.AeroBodyVectorToUnityLocal(probe);
            Vector3 axialMapped = MavFlightDynamicsMath.AeroBodyMomentToUnityLocal(probe);
            Record(
                NearVector(axialMapped, trueVectorMapped * -1f, 1e-5f),
                "the moment mapping carries the determinant sign (it is the negated true-vector mapping)",
                report, ref passed, ref failed
            );

            // --- necessary but NOT sufficient: kept so a one-sided edit is still caught ---
            Vector3 aeroMoment = new Vector3(2f, -3f, 4f);
            Vector3 unityMoment = MavFlightDynamicsMath.AeroBodyMomentToUnityLocal(aeroMoment);
            Vector3 recovered = MavFlightDynamicsMath.UnityLocalAngularRateToAeroBody(unityMoment);
            Record(
                NearVector(recovered, aeroMoment, 1e-5f),
                "moment-out and rate-in remain mutual inverses (necessary, not sufficient: see above)",
                report, ref passed, ref failed
            );
        }

        // ---------------------------------------------------------------- [P8]

        /// <summary>
        /// End-to-end physical regression across the whole boundary:
        ///
        ///   Unity angular velocity -> body rates -> frozen Morelli damping derivatives
        ///     -> aerodynamic moment -> Unity local torque
        ///
        /// For each axis, a body rotation must produce a Unity torque that OPPOSES that rotation.
        /// The published Morelli damping derivatives are negative at zero alpha (Clp, Cmq, Cnr), so
        /// aerodynamic damping is the physically expected outcome.
        ///
        /// This section is the reason a round-trip check is not enough. Under the previous
        /// true-vector mapping, the inbound rate sign error and the outbound moment sign error
        /// cancelled around exactly this loop, so damping still looked correct while static and
        /// control-derived moments were inverted. These checks assert the damping direction AND the
        /// static/control direction, so neither can be wrong in isolation or in tandem.
        /// </summary>
        private static void ValidateDampingDirections(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P8] Physical damping direction through the whole boundary");

            const float speedMps = 200f;
            const float rateRadSec = 0.2f;

            // Rolling right in Unity is a NEGATIVE Unity Z angular velocity.
            Vector3 rollRightTorque = UnityTorqueForUnityRate(new Vector3(0f, 0f, -rateRadSec), speedMps);
            Record(
                rollRightTorque.z > 0f,
                "rolling right produces a Unity +Z (roll-left) torque: roll damping opposes the motion",
                report, ref passed, ref failed
            );

            // Pitching nose up in Unity is a NEGATIVE Unity X angular velocity.
            Vector3 noseUpTorque = UnityTorqueForUnityRate(new Vector3(-rateRadSec, 0f, 0f), speedMps);
            Record(
                noseUpTorque.x > 0f,
                "pitching nose up produces a Unity +X (nose-down) torque: pitch damping opposes the motion",
                report, ref passed, ref failed
            );

            // Yawing nose right in Unity is a POSITIVE Unity Y angular velocity.
            Vector3 noseRightTorque = UnityTorqueForUnityRate(new Vector3(0f, rateRadSec, 0f), speedMps);
            Record(
                noseRightTorque.y < 0f,
                "yawing nose right produces a Unity -Y (nose-left) torque: yaw damping opposes the motion",
                report, ref passed, ref failed
            );

            // Static and control-derived moments must also reach Unity in the demanded direction.
            // These do NOT benefit from any cancellation, which is why they are checked here too.
            MavControlSurfaceLimits limits = BuildF16SurfaceLimits();

            Vector3 noseUpCommandTorque = UnityTorqueForSurfaces(
                MapWithDefaultSigns(new MavPilotCommand { pitch = 1f }, limits), speedMps
            );
            Record(
                noseUpCommandTorque.x < 0f,
                "a nose-up pilot command reaches the Rigidbody as a Unity -X (nose-up) torque",
                report, ref passed, ref failed
            );

            Vector3 rollRightCommandTorque = UnityTorqueForSurfaces(
                MapWithDefaultSigns(new MavPilotCommand { roll = 1f }, limits), speedMps
            );
            Record(
                rollRightCommandTorque.z < 0f,
                "a roll-right pilot command reaches the Rigidbody as a Unity -Z (roll-right) torque",
                report, ref passed, ref failed
            );

            Vector3 noseRightCommandTorque = UnityTorqueForSurfaces(
                MapWithDefaultSigns(new MavPilotCommand { yaw = 1f }, limits), speedMps
            );
            Record(
                noseRightCommandTorque.y > 0f,
                "a nose-right pilot command reaches the Rigidbody as a Unity +Y (nose-right) torque",
                report, ref passed, ref failed
            );

            // Static longitudinal restoring moment: at positive alpha with neutral controls the
            // aerodynamic pitching moment must push the nose back down, not further up.
            Vector3 alphaTorque = UnityTorqueForState(
                8f * Mathf.Deg2Rad, 0f, new MavControlInput(), Vector3.zero, speedMps
            );
            Record(
                alphaTorque.x > 0f,
                "positive alpha with neutral controls yields a Unity +X (nose-down) restoring torque",
                report, ref passed, ref failed
            );
        }

        /// <summary>
        /// Runs a Unity-local angular velocity through the real boundary and returns the resulting
        /// Unity-local torque contributed by the rate terms alone (the zero-rate moment is
        /// subtracted so only the damping contribution remains).
        /// </summary>
        private static Vector3 UnityTorqueForUnityRate(Vector3 unityLocalRateRadSec, float speedMps)
        {
            Vector3 bodyRates = MavFlightDynamicsMath.UnityLocalAngularRateToAeroBody(unityLocalRateRadSec);

            Vector3 withRate = UnityTorqueForState(0f, 0f, new MavControlInput(), bodyRates, speedMps);
            Vector3 withoutRate = UnityTorqueForState(0f, 0f, new MavControlInput(), Vector3.zero, speedMps);
            return withRate - withoutRate;
        }

        private static Vector3 UnityTorqueForSurfaces(MavControlInput surfaces, float speedMps)
        {
            return UnityTorqueForState(0f, 0f, surfaces, Vector3.zero, speedMps);
        }

        /// <summary>
        /// Full boundary evaluation: state + surfaces -> frozen Morelli coefficients ->
        /// dimensionalization -> Unity-local torque. Mirrors what MavSixDoFBody does per step.
        /// </summary>
        private static Vector3 UnityTorqueForState(
            float alphaRad,
            float betaRad,
            MavControlInput surfaces,
            Vector3 bodyRatesRadSec,
            float speedMps)
        {
            MavAeroReferenceGeometry geometry = MavF16MorelliReference.CreateReferenceGeometry();
            float halfInverseSpeed = 0.5f / Mathf.Max(0.1f, speedMps);

            float pHat = bodyRatesRadSec.x * geometry.wingSpanM * halfInverseSpeed;
            float qHat = bodyRatesRadSec.y * geometry.meanAerodynamicChordM * halfInverseSpeed;
            float rHat = bodyRatesRadSec.z * geometry.wingSpanM * halfInverseSpeed;

            MavAeroCoefficients coefficients = MavF16MorelliPolynomial.Evaluate(
                alphaRad,
                betaRad,
                surfaces.elevatorDeg * Mathf.Deg2Rad,
                surfaces.aileronDeg * Mathf.Deg2Rad,
                surfaces.rudderDeg * Mathf.Deg2Rad,
                pHat,
                qHat,
                rHat,
                MavF16MassReference.XcgCbar,
                MavF16MassReference.XcgReferenceCbar,
                geometry.meanAerodynamicChordM / geometry.wingSpanM
            );

            // Representative sea-level dynamic pressure for the reference speed.
            MavAtmosphereSample atmosphere = MavAtmosphereModel.Sample(0f);
            float dynamicPressurePa = 0.5f * atmosphere.densityKgM3 * speedMps * speedMps;

            MavAerodynamicLoads loads = MavFlightDynamicsMath.Dimensionalize(
                coefficients, geometry, dynamicPressurePa
            );

            return MavFlightDynamicsMath.AeroBodyMomentToUnityLocal(loads.momentAeroBodyNm);
        }

        // ---------------------------------------------------------------- helpers

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

        private static MavControlInput MapWithDefaultSigns(
            MavPilotCommand command,
            MavControlSurfaceLimits limits)
        {
            return MavDirectSurfaceControlLaw.Map(command, limits, 1f, 1f, 1f, -1f, -1f, -1f);
        }

        /// <summary>
        /// Evaluates the frozen aerodynamic polynomial at zero alpha/beta/rates with only the
        /// commanded surface deflections applied, so a coefficient change is attributable to the
        /// control mapping and nothing else.
        /// </summary>
        private static MavAeroCoefficients EvaluateAt(MavControlInput input, float chordOverSpan)
        {
            return MavF16MorelliPolynomial.Evaluate(
                0f,
                0f,
                input.elevatorDeg * Mathf.Deg2Rad,
                input.aileronDeg * Mathf.Deg2Rad,
                input.rudderDeg * Mathf.Deg2Rad,
                0f,
                0f,
                0f,
                MavF16MassReference.XcgCbar,
                MavF16MassReference.XcgReferenceCbar,
                chordOverSpan
            );
        }

        private static bool Near(float actual, float expected, float tolerance)
        {
            return Mathf.Abs(actual - expected) <= tolerance;
        }

        private static bool NearVector(Vector3 actual, Vector3 expected, float tolerance)
        {
            return Mathf.Abs(actual.x - expected.x) <= tolerance
                && Mathf.Abs(actual.y - expected.y) <= tolerance
                && Mathf.Abs(actual.z - expected.z) <= tolerance;
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
