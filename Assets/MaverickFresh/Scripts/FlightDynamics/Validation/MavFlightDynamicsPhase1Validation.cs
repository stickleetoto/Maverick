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
    ///   [P7] Unity/aero axis handedness bookkeeping
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
            ValidateAxisHandedness(report, ref passed, ref failed);

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

            string[] columns = MavFlightDynamicsTelemetry.CsvHeader().Split(',');
            int expected = MavFlightDynamicsTelemetry.CsvNumericColumnCount
                           + MavFlightDynamicsTelemetry.CsvFlagColumnCount;

            Record(
                columns.Length == expected,
                "CSV header column count matches the emitted row width ("
                + columns.Length + " vs " + expected + ")",
                report, ref passed, ref failed
            );

            bool allNamed = true;
            for (int i = 0; i < columns.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(columns[i]))
                {
                    allNamed = false;
                    break;
                }
            }
            Record(allNamed, "every CSV column has a name", report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [P7]

        /// <summary>
        /// Records the relationship between the two axis conventions so that any future change to
        /// the conversion helpers is a deliberate, reviewed act rather than an accident.
        ///
        /// Facts these checks pin down:
        ///
        ///  * The Unity-local basis (X right, Y up, Z forward) and the aerodynamic body basis
        ///    (X forward, Y right, Z down) differ in handedness. The component change of basis has
        ///    determinant -1.
        ///
        ///  * Under a basis change of determinant -1, a true vector (force, velocity, position)
        ///    transforms with the basis change, but a pseudo-vector (moment, angular rate)
        ///    transforms with an extra factor of the determinant, i.e. an extra sign on every axis.
        ///
        ///  * MavFlightDynamicsMath.AeroBodyMomentToUnityLocal currently applies the true-vector
        ///    mapping and therefore does NOT carry that extra sign. The check named below asserts
        ///    exactly that, so the current behaviour is pinned and visible.
        ///
        /// This is recorded as an OPEN question rather than silently changed here: altering it
        /// changes the physical meaning of every applied moment, which the architecture document
        /// gates behind the golden-trace procedure. See the Flight Dynamics README, section
        /// "Open issue: moment/rate axis handedness".
        /// </summary>
        private static void ValidateAxisHandedness(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P7] Unity/aero axis handedness bookkeeping");

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

            // Force (true vector) mapping: right stays right, up is minus down, forward stays forward.
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
                "aerodynamic force axes map to Unity axes as a true vector (forward/right/down -> forward/right/-up)",
                report, ref passed, ref failed
            );

            // PINNED CURRENT BEHAVIOUR: the moment mapping is identical to the force mapping, so it
            // carries no handedness sign. Recorded as an open issue, not endorsed as correct.
            Record(
                NearVector(
                    MavFlightDynamicsMath.AeroBodyMomentToUnityLocal(new Vector3(1f, 2f, 3f)),
                    MavFlightDynamicsMath.AeroBodyVectorToUnityLocal(new Vector3(1f, 2f, 3f)),
                    1e-5f),
                "PINNED (open issue): the moment mapping equals the force mapping and carries no handedness sign",
                report, ref passed, ref failed
            );

            // Self-consistency invariant that must hold regardless of how the open issue is
            // resolved: converting a moment to Unity and an angular rate back must round-trip.
            // This is why the aerodynamic rate-damping terms remain self-consistent today.
            Vector3 aeroMoment = new Vector3(2f, -3f, 4f);
            Vector3 unityMoment = MavFlightDynamicsMath.AeroBodyMomentToUnityLocal(aeroMoment);
            Vector3 recovered = MavFlightDynamicsMath.UnityLocalAngularRateToAeroBody(unityMoment);
            Record(
                NearVector(recovered, aeroMoment, 1e-5f),
                "moment-out and rate-in conversions are mutual inverses (rate-damping loop stays self-consistent)",
                report, ref passed, ref failed
            );
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
