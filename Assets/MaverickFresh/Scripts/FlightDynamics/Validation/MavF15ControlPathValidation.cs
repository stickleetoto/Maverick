using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using MaverickFresh.FlightDynamics.F15;

namespace MaverickFresh.FlightDynamics.Validation
{
    /// <summary>
    /// Deterministic checks for the F-15 control path:
    ///
    ///     pilot command -> MavF15ControlLaw -> MavF15RequestedSurfaceState
    ///                   -> MavF15ControlActuator -> MavF15ActualSurfaceState -> aero
    ///
    /// Covered:
    ///   [C0]  the four-channel surface state addresses every channel independently
    ///   [C1]  undeclared surface authority is REFUSED, not passed through
    ///   [C2]  half-filled limit declarations are rejected as contradictions
    ///   [C3]  declared travel clamps; declared rate rate-limits; dt=0 cannot teleport
    ///   [C4]  the control law fails closed with no sourced gearing
    ///   [C5]  each FCS stage gates on its own gains, independently
    ///   [C6]  ARI is fed from COMMANDED roll, and only when its gain is declared
    ///   [C7]  the roll-damper washout schedule: monotonic, reaches zero at its declared point
    ///   [C8]  the law never claims NASA 836 authority it does not have
    ///   [C9]  differential stabilator never leaks into the shared MavControlInput contract
    ///   [C10] axis routing: pitch/roll/yaw each reach exactly their intended surfaces
    ///   [C11] CAS disengaged produces exactly zero augmentation increment
    ///   [C12] FCS mode provenance floor refuses mixed-provenance configurations
    ///   [C13] no F-15 FCS source writes Rigidbody forces or torques
    ///   [C14] every output is finite across a wide state sweep
    ///   [C15] the actuator is the sole owner of actual surface state
    ///
    /// Every check runs through the real production entry points - the same statics the
    /// MonoBehaviours call - so no GameObject, Rigidbody, scene or play-mode session is created.
    ///
    /// None of this asserts that any F-15 NUMBER is correct. No F-15 control gain has been
    /// recovered for NASA 836; these fixtures use obviously synthetic gains, explicitly marked,
    /// purely to drive the architecture. What is validated is ownership, routing and gating.
    /// </summary>
    public static class MavF15ControlPathValidation
    {
        private const float Tolerance = 1e-4f;

        public static string RunAll(out int passed, out int failed)
        {
            passed = 0;
            failed = 0;
            StringBuilder report = new StringBuilder(16384);
            report.AppendLine("F-15 Control Path - Ownership, Routing and Gating Validation");
            report.AppendLine("============================================================");
            report.AppendLine("Synthetic gains only. No F-15 aircraft number is asserted here.");

            ValidateSurfaceStateAddressing(report, ref passed, ref failed);
            ValidateUndeclaredAuthorityRefused(report, ref passed, ref failed);
            ValidateLimitSelfConsistency(report, ref passed, ref failed);
            ValidateActuatorTravelAndRate(report, ref passed, ref failed);
            ValidateControlLawFailsClosed(report, ref passed, ref failed);
            ValidateStageGating(report, ref passed, ref failed);
            ValidateAriSource(report, ref passed, ref failed);
            ValidateWashoutSchedule(report, ref passed, ref failed);
            ValidateAuthorityClaims(report, ref passed, ref failed);
            ValidateDifferentialStabilatorContainment(report, ref passed, ref failed);
            ValidateAxisRouting(report, ref passed, ref failed);
            ValidateCasDisengagement(report, ref passed, ref failed);
            ValidateModeProvenanceFloor(report, ref passed, ref failed);
            ValidateNoRigidbodyWrites(report, ref passed, ref failed);
            ValidateFiniteOutputs(report, ref passed, ref failed);
            ValidateActualStateOwnership(report, ref passed, ref failed);

            report.AppendLine();
            report.Append("RESULT: ")
                .Append(failed == 0 ? "PASS" : "FAIL")
                .Append("  passed=").Append(passed)
                .Append(" failed=").Append(failed);

            return report.ToString();
        }

        // ---------------------------------------------------------------- [C0]

        private static void ValidateSurfaceStateAddressing(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[C0] Four-channel surface state");

            MavF15SurfaceState s = MavF15SurfaceState.Neutral;
            Record(
                s.symmetricStabilatorDeg == 0f && s.differentialStabilatorDeg == 0f
                && s.aileronDeg == 0f && s.rudderDeg == 0f,
                "Neutral is all-zero (no accidental default deflection)",
                report, ref passed, ref failed);

            bool independent = true;
            for (int i = 0; i < 4; i++)
            {
                MavF15SurfaceState probe = MavF15SurfaceState.Neutral;
                probe.Set((MavF15SurfaceChannel)i, 7f);

                for (int j = 0; j < 4; j++)
                {
                    float expected = i == j ? 7f : 0f;
                    if (!Near(probe.Get((MavF15SurfaceChannel)j), expected, Tolerance))
                        independent = false;
                }
            }

            Record(independent,
                "Get/Set address all four channels independently, with no crosstalk",
                report, ref passed, ref failed);

            MavF15SurfaceState bad = MavF15SurfaceState.Neutral;
            bad.aileronDeg = float.NaN;
            Record(!bad.IsFinite() && MavF15SurfaceState.Neutral.IsFinite(),
                "IsFinite detects a non-finite channel",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [C1]

        private static void ValidateUndeclaredAuthorityRefused(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[C1] Undeclared surface authority is refused");

            MavF15SurfaceLimits unavailable = MavF15SurfaceLimits.UnavailableExactTarget();

            Record(unavailable.AvailableChannelCount == 0,
                "the exact-target default declares zero channels - NASA 836 travel is not frozen",
                report, ref passed, ref failed);

            MavF15SurfaceState big = new MavF15SurfaceState
            {
                symmetricStabilatorDeg = 30f,
                differentialStabilatorDeg = 20f,
                aileronDeg = 25f,
                rudderDeg = 30f
            };

            bool refused;
            MavF15SurfaceState bounded = unavailable.Clamp(big, out refused);

            Record(refused,
                "clamping against undeclared authority reports a refusal",
                report, ref passed, ref failed);

            Record(
                bounded.symmetricStabilatorDeg == 0f && bounded.differentialStabilatorDeg == 0f
                && bounded.aileronDeg == 0f && bounded.rudderDeg == 0f,
                "every undeclared channel is driven to ZERO, not passed through"
                + " - unknown authority must never become unlimited authority",
                report, ref passed, ref failed);

            MavF15SurfaceLimits sneaky = MavF15SurfaceLimits.UnavailableExactTarget();
            sneaky.aileron.minDeg = -20f;
            sneaky.aileron.maxDeg = 20f;

            Record(!sneaky.aileron.Available && sneaky.AvailableChannelCount == 0,
                "a channel with travel numbers but Unavailable provenance is still refused",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [C2]

        private static void ValidateLimitSelfConsistency(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[C2] Half-filled limit declarations are contradictions");

            string reason;

            MavF15SurfaceChannelLimits rateWithoutSource = Declared(-20f, 20f);
            rateWithoutSource.rateLimitDegSec = 40f;
            rateWithoutSource.rateProvenance = MavEngineDataProvenance.Unavailable;
            Record(!rateWithoutSource.IsSelfConsistent(out reason),
                "a finite rate with Unavailable rate provenance is rejected (" + reason + ")",
                report, ref passed, ref failed);

            MavF15SurfaceChannelLimits sourceWithoutRate = Declared(-20f, 20f);
            sourceWithoutRate.rateProvenance = MavEngineDataProvenance.MaverickTuning;
            Record(!sourceWithoutRate.IsSelfConsistent(out reason),
                "a declared rate source with no positive rate is rejected (" + reason + ")",
                report, ref passed, ref failed);

            MavF15SurfaceChannelLimits inverted = Declared(20f, -20f);
            Record(!inverted.IsSelfConsistent(out reason),
                "an inverted travel interval is rejected (" + reason + ")",
                report, ref passed, ref failed);

            MavF15SurfaceChannelLimits honest = Declared(-20f, 20f);
            Record(honest.IsSelfConsistent(out reason) && honest.Available && !honest.HasSourcedRate,
                "declared travel with no sourced rate is CONSISTENT - 'no actuator dynamics'"
                + " is an honest state, not a contradiction",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [C3]

        private static void ValidateActuatorTravelAndRate(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[C3] Actuator travel clamping and rate limiting");

            MavF15SurfaceLimits limits = SyntheticLimits(rateDegSec: 0f);

            bool refused;
            MavF15SurfaceState bounded = limits.Clamp(
                new MavF15SurfaceState
                {
                    symmetricStabilatorDeg = 99f,
                    differentialStabilatorDeg = -99f,
                    aileronDeg = 99f,
                    rudderDeg = -99f
                },
                out refused);

            Record(!refused && limits.AvailableChannelCount == 4,
                "all four synthetic channels are declared, so nothing is refused",
                report, ref passed, ref failed);

            Record(
                Near(bounded.symmetricStabilatorDeg, 15f, Tolerance)
                && Near(bounded.differentialStabilatorDeg, -10f, Tolerance)
                && Near(bounded.aileronDeg, 20f, Tolerance)
                && Near(bounded.rudderDeg, -30f, Tolerance),
                "each channel clamps to its OWN declared travel, including asymmetric stops",
                report, ref passed, ref failed);

            MavF15SurfaceState afterInstant = MavF15ControlActuator.StepChannels(
                MavF15SurfaceState.Neutral, bounded, limits, 0.02f);

            Record(
                Near(afterInstant.symmetricStabilatorDeg, 15f, Tolerance),
                "with NO sourced rate the channel steps instantly (declared, not hidden)",
                report, ref passed, ref failed);

            MavF15SurfaceLimits rated = SyntheticLimits(rateDegSec: 60f);
            MavF15SurfaceState afterRated = MavF15ControlActuator.StepChannels(
                MavF15SurfaceState.Neutral, bounded, rated, 0.02f);

            Record(
                Near(afterRated.symmetricStabilatorDeg, 1.2f, Tolerance),
                "a rate-limited surface CANNOT teleport: 60 deg/s over 20 ms moves 1.2 deg,"
                + " not the full 15 (actual " + afterRated.symmetricStabilatorDeg.ToString("F4") + ")",
                report, ref passed, ref failed);

            MavF15SurfaceState converging = MavF15SurfaceState.Neutral;
            for (int i = 0; i < 100; i++)
                converging = MavF15ControlActuator.StepChannels(converging, bounded, rated, 0.02f);

            Record(
                Near(converging.symmetricStabilatorDeg, 15f, Tolerance)
                && Near(converging.rudderDeg, -30f, Tolerance),
                "a rate-limited channel converges on the target without overshoot",
                report, ref passed, ref failed);

            MavF15SurfaceState frozen = MavF15ControlActuator.StepChannels(
                MavF15SurfaceState.Neutral, bounded, rated, 0f);
            Record(
                frozen.symmetricStabilatorDeg == 0f && frozen.rudderDeg == 0f,
                "dt=0 moves a rate-limited surface NOT AT ALL - a paused physics step"
                + " cannot be used to teleport a surface to its target",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [C4]

        private static void ValidateControlLawFailsClosed(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[C4] Control law fails closed without sourced gearing");

            MavF15ControlLawSchedules none = MavF15ControlLawSchedules.Unavailable();

            MavF15FcsSolution neutralCmd = Solve(
                MavF15FcsMode.AFITResearch, none, Command(0f, 0f, 0f), FlightState(alphaDeg: 5f));

            Record(IsNeutral(neutralCmd.requested),
                "neutral command gives neutral requested surfaces",
                report, ref passed, ref failed);

            MavF15FcsSolution full = Solve(
                MavF15FcsMode.AFITResearch, none, Command(1f, 1f, 1f),
                FlightState(alphaDeg: 5f, p: 1f, q: 1f, r: 1f));

            Record(!none.MechanicalPathAvailable,
                "the default schedule set declares no mechanical gearing",
                report, ref passed, ref failed);

            Record(IsNeutral(full.requested),
                "FULL stick, full pedal, non-zero rates -> every surface stays neutral."
                + " An unavailable gain cannot create control authority",
                report, ref passed, ref failed);

            Record(
                full.EffectiveProvenance == MavEngineDataProvenance.Unavailable,
                "and the solution reports Unavailable effective provenance, not a grade it lacks",
                report, ref passed, ref failed);

            MavF15ControlLawSchedules partial = MavF15ControlLawSchedules.Unavailable();
            partial.pitchStickToStabilatorDegPerUnit = Gain(-15f);

            MavF15FcsSolution partialResult = Solve(
                MavF15FcsMode.AFITResearch, partial, Command(1f, 1f, 1f), FlightState(alphaDeg: 5f));

            Record(
                !partial.MechanicalPathAvailable && IsNeutral(partialResult.requested),
                "one sourced gearing out of four is still refused - a partial mechanical path"
                + " cannot produce a trustworthy demand on any axis",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [C5]

        private static void ValidateStageGating(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[C5] Each FCS stage gates on its own gains");

            MavF15ControlLawSchedules mech = MechanicalOnly();

            MavF15FcsSolution noCas = Solve(
                MavF15FcsMode.AFITResearch, mech, Command(1f, 0f, 0f),
                FlightState(alphaDeg: 5f, q: 0.5f));

            Record(
                Near(noCas.requested.channels.symmetricStabilatorDeg, -15f, Tolerance),
                "with no sourced pitch CAS, a 0.5 rad/s pitch rate changes nothing"
                + " (stabilator " + noCas.requested.channels.symmetricStabilatorDeg.ToString("F3")
                + " = pure gearing)",
                report, ref passed, ref failed);

            MavF15ControlLawSchedules withPitchCas = mech;
            withPitchCas.pitchRateFeedbackDegPerRadSec = Gain(4f);

            MavF15FcsSolution casOn = Solve(
                MavF15FcsMode.AFITResearch, withPitchCas, Command(1f, 0f, 0f),
                FlightState(alphaDeg: 5f, q: 0.5f));

            Record(
                Near(casOn.requested.channels.symmetricStabilatorDeg, -15f - 2f, Tolerance),
                "sourcing ONLY pitch-rate feedback switches on ONLY that term"
                + " (" + casOn.requested.channels.symmetricStabilatorDeg.ToString("F3")
                + " = -15 - 2.0)",
                report, ref passed, ref failed);

            Record(
                Near(casOn.requested.channels.rudderDeg, 0f, Tolerance)
                && Near(casOn.requested.channels.differentialStabilatorDeg, 0f, Tolerance),
                "switching on pitch CAS does not switch on yaw or roll augmentation",
                report, ref passed, ref failed);

            // Load-factor feedback must be skipped when the accelerometer is not valid.
            MavF15ControlLawSchedules withNz = mech;
            withNz.normalLoadFactorFeedbackDegPerG = Gain(2f);

            MavFlightState invalidNz = FlightState(alphaDeg: 5f);
            invalidNz.specificForceValid = false;
            invalidNz.specificForceAeroBodyG = new Vector3(0f, 0f, -4f);

            MavF15FcsSolution nzSkipped = Solve(
                MavF15FcsMode.AFITResearch, withNz, Command(1f, 0f, 0f), invalidNz);

            Record(
                Near(nzSkipped.requested.channels.symmetricStabilatorDeg, -15f, Tolerance),
                "load-factor feedback is SKIPPED while the accelerometer reads invalid,"
                + " rather than closing a loop on a phantom 4 g",
                report, ref passed, ref failed);

            MavFlightState validNz = invalidNz;
            validNz.specificForceValid = true;

            MavF15FcsSolution nzApplied = Solve(
                MavF15FcsMode.AFITResearch, withNz, Command(1f, 0f, 0f), validNz);

            Record(
                Near(nzApplied.requested.channels.symmetricStabilatorDeg, -15f - 8f, Tolerance),
                "the same reading marked valid IS used ("
                + nzApplied.requested.channels.symmetricStabilatorDeg.ToString("F3") + " = -15 - 8)",
                report, ref passed, ref failed);

            // Stall inhibitor needs BOTH threshold and gradient.
            MavF15ControlLawSchedules halfInhibitor = mech;
            halfInhibitor.stallInhibitorAlphaThresholdDeg = Gain(20f);

            Record(
                !halfInhibitor.StageAvailable(MavF15FcsStage.StallInhibitor),
                "a stall-inhibitor threshold with no authority gradient does not enable the stage",
                report, ref passed, ref failed);

            MavF15ControlLawSchedules inhibitor = halfInhibitor;
            inhibitor.stallInhibitorDegPerDegAlpha = Gain(0.5f);

            MavF15FcsSolution below = Solve(
                MavF15FcsMode.AFITResearch, inhibitor, Command(0f, 0f, 0f),
                FlightState(alphaDeg: 10f));
            MavF15FcsSolution above = Solve(
                MavF15FcsMode.AFITResearch, inhibitor, Command(0f, 0f, 0f),
                FlightState(alphaDeg: 30f));

            Record(
                Near(below.requested.channels.symmetricStabilatorDeg, 0f, Tolerance)
                && Near(above.requested.channels.symmetricStabilatorDeg, 5f, Tolerance),
                "the stall inhibitor is inert below its threshold and acts above it"
                + " (10 deg -> 0.0, 30 deg -> "
                + above.requested.channels.symmetricStabilatorDeg.ToString("F2") + ")",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [C6]

        private static void ValidateAriSource(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[C6] ARI is fed from commanded roll, and only when declared");

            MavF15ControlLawSchedules noAri = MechanicalOnly();
            noAri.rollRateFeedbackDegPerRadSec = Gain(3f);

            MavF15FcsSolution ariOff = Solve(
                MavF15FcsMode.AFITResearch, noAri, Command(0f, 1f, 0f), FlightState(alphaDeg: 5f));

            Record(
                Near(ariOff.requested.channels.rudderDeg, 0f, Tolerance),
                "with the ARI gain Unavailable, full roll stick produces NO rudder"
                + " - the stage cannot contribute without its own declared gain",
                report, ref passed, ref failed);

            MavF15ControlLawSchedules s = noAri;
            s.ariRudderPerAileron = Gain(0.25f);

            MavF15FcsSolution stickCentred = Solve(
                MavF15FcsMode.AFITResearch, s, Command(0f, 0f, 0f),
                FlightState(alphaDeg: 5f, p: 1f));

            Record(
                Near(stickCentred.requested.channels.rudderDeg, 0f, Tolerance),
                "a 1 rad/s roll rate with the stick centred produces NO rudder"
                + " (rudder " + stickCentred.requested.channels.rudderDeg.ToString("F4") + ")"
                + " - the ARI is not closing a loop through the roll damper",
                report, ref passed, ref failed);

            Record(
                !Near(stickCentred.requested.channels.differentialStabilatorDeg, 0f, Tolerance),
                "...while the roll damper DOES respond to that rate (differential stabilator "
                + stickCentred.requested.channels.differentialStabilatorDeg.ToString("F3") + ")",
                report, ref passed, ref failed);

            MavF15FcsSolution stickRolled = Solve(
                MavF15FcsMode.AFITResearch, s, Command(0f, 1f, 0f), FlightState(alphaDeg: 5f));

            Record(
                Near(stickRolled.requested.channels.rudderDeg, 5f, Tolerance),
                "full roll stick crossfeeds 20 deg aileron * 0.25 = 5 deg rudder (actual "
                + stickRolled.requested.channels.rudderDeg.ToString("F3") + ")",
                report, ref passed, ref failed);

            // The roll-RATE crossfeed is a separate stage from the ARI and must gate separately.
            MavF15ControlLawSchedules withCrossfeed = s;
            withCrossfeed.rollRateToYawCrossfeedDegPerRadSec = Gain(2f);

            MavF15FcsSolution crossfed = Solve(
                MavF15FcsMode.AFITResearch, withCrossfeed, Command(0f, 0f, 0f),
                FlightState(alphaDeg: 5f, p: 1f));

            Record(
                Near(crossfed.requested.channels.rudderDeg, 2f, Tolerance),
                "the roll-RATE to yaw crossfeed is a DIFFERENT stage: with the stick centred and"
                + " 1 rad/s roll rate it gives 2 deg rudder where the ARI gave 0",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [C7]

        private static void ValidateWashoutSchedule(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[C7] Roll-damper washout schedule");

            string reason;

            MavF15RollDamperSchedule unavailable = MavF15RollDamperSchedule.Unavailable();
            Record(
                !unavailable.Available
                && Near(unavailable.AuthorityAt(40f, out reason), 1f, Tolerance),
                "an unavailable schedule leaves the damper at FULL authority at 40 deg alpha"
                + " - no invented fade",
                report, ref passed, ref failed);

            // A schedule carrying only the reported endpoint must NOT run: one endpoint does not
            // determine the curve. This is the specific trap this type exists to prevent.
            MavF15RollDamperSchedule endpointOnly = MavF15RollDamperSchedule.Unavailable();
            endpointOnly.zeroAuthorityAlphaDeg =
                MavF15RollDamperSchedule.ReportedAfitZeroAuthorityAlphaDeg;

            Record(
                !endpointOnly.Available,
                "a schedule carrying ONLY the reported 20.2 deg zero point is NOT available"
                + " - an endpoint is not a schedule",
                report, ref passed, ref failed);

            // Fully declared AFIT research schedule.
            MavF15RollDamperSchedule afit = MavF15RollDamperSchedule.AfitResearch(
                0f, MavF15RollDamperWashoutShape.Linear, "synthetic validation start alpha");

            Record(
                afit.Available
                && afit.provenance == MavEngineDataProvenance.CrossValidationOnly,
                "the AFIT factory produces an available schedule pinned to CrossValidationOnly"
                + " - it cannot be raised to exact-target authority through that path",
                report, ref passed, ref failed);

            Record(
                Near(afit.zeroAuthorityAlphaDeg, 20.2f, Tolerance),
                "its zero-authority alpha is the reported 20.2 deg",
                report, ref passed, ref failed);

            // Monotonic non-increasing across the whole range.
            bool monotonic = true;
            float previous = float.MaxValue;
            float worstAlpha = 0f;
            for (float a = -5f; a <= 40f; a += 0.25f)
            {
                float authority = afit.AuthorityAt(a, out reason);
                if (authority > previous + 1e-6f)
                {
                    monotonic = false;
                    worstAlpha = a;
                }
                previous = authority;
            }

            Record(monotonic,
                "authority decreases monotonically with alpha"
                + (monotonic ? "" : " - rose at " + worstAlpha.ToString("F2") + " deg"),
                report, ref passed, ref failed);

            Record(
                Near(afit.AuthorityAt(0f, out reason), 1f, Tolerance),
                "full authority at the declared start alpha",
                report, ref passed, ref failed);

            Record(
                Near(afit.AuthorityAt(10.1f, out reason), 0.5f, Tolerance),
                "half authority halfway up a Linear ramp (actual "
                + afit.AuthorityAt(10.1f, out reason).ToString("F4") + ")",
                report, ref passed, ref failed);

            Record(
                Near(afit.AuthorityAt(20.2f, out reason), 0f, Tolerance),
                "authority reaches EXACTLY zero at the source-defined 20.2 deg point",
                report, ref passed, ref failed);

            Record(
                Near(afit.AuthorityAt(60f, out reason), 0f, Tolerance),
                "and saturates at zero beyond it - a washed-out damper contributes nothing,"
                + " it does not go negative and start driving the roll",
                report, ref passed, ref failed);

            // The washout must actually scale the roll CAS contribution.
            MavF15ControlLawSchedules s = MechanicalOnly();
            s.rollRateFeedbackDegPerRadSec = Gain(4f);
            s.rollDamperWashout = afit;

            MavF15FcsSolution low = Solve(
                MavF15FcsMode.AFITResearch, s, Command(0f, 0f, 0f),
                FlightState(alphaDeg: 0f, p: 1f));
            MavF15FcsSolution mid = Solve(
                MavF15FcsMode.AFITResearch, s, Command(0f, 0f, 0f),
                FlightState(alphaDeg: 10.1f, p: 1f));
            MavF15FcsSolution washed = Solve(
                MavF15FcsMode.AFITResearch, s, Command(0f, 0f, 0f),
                FlightState(alphaDeg: 25f, p: 1f));

            Record(
                Near(low.requested.channels.differentialStabilatorDeg, -4f, Tolerance)
                && Near(mid.requested.channels.differentialStabilatorDeg, -2f, Tolerance)
                && Near(washed.requested.channels.differentialStabilatorDeg, 0f, Tolerance),
                "the schedule scales the roll CAS contribution: -4.0 at 0 deg, -2.0 at 10.1 deg,"
                + " 0.0 at 25 deg",
                report, ref passed, ref failed);

            // Half-filled: shape declared with no source, or source with no shape.
            MavF15RollDamperSchedule shapeNoSource = afit;
            shapeNoSource.provenance = MavEngineDataProvenance.Unavailable;
            Record(!shapeNoSource.IsSelfConsistent(out reason),
                "a declared shape with Unavailable provenance is rejected (" + reason + ")",
                report, ref passed, ref failed);

            MavF15RollDamperSchedule sourceNoShape = afit;
            sourceNoShape.shape = MavF15RollDamperWashoutShape.Unavailable;
            Record(!sourceNoShape.IsSelfConsistent(out reason),
                "a declared source with Unavailable shape is rejected (" + reason + ")",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [C8]

        private static void ValidateAuthorityClaims(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[C8] The law does not claim authority it lacks");

            Record(
                !MavF15ControlLawSchedules.Unavailable().IsFullyExactTargetAuthoritative,
                "the default schedule set does not claim exact-target authority",
                report, ref passed, ref failed);

            MavF15ControlLawSchedules publicRef = AllGainsAt(MavEngineDataProvenance.PublicReference);
            Record(
                !publicRef.IsFullyExactTargetAuthoritative,
                "a FULLY populated schedule set at PublicReference still does not claim"
                + " exact-target authority - strong public evidence is not configuration proof",
                report, ref passed, ref failed);

            Record(
                publicRef.MechanicalPathAvailable && publicRef.StageAvailable(MavF15FcsStage.YawCas),
                "...while still being usable: available and authoritative are separate questions",
                report, ref passed, ref failed);

            MavF15ControlLawSchedules authoritative =
                AllGainsAt(MavEngineDataProvenance.Authoritative);
            Record(
                authoritative.IsFullyExactTargetAuthoritative,
                "only an all-Authoritative set claims exact-target authority",
                report, ref passed, ref failed);

            MavF15ControlLawSchedules oneDowngraded = authoritative;
            oneDowngraded.ariRudderPerAileron = Gain(0.25f);
            Record(
                !oneDowngraded.IsFullyExactTargetAuthoritative,
                "downgrading a SINGLE gain revokes the exact-target claim for the whole law",
                report, ref passed, ref failed);

            // The washout schedule counts too.
            MavF15ControlLawSchedules washoutDowngraded = authoritative;
            washoutDowngraded.rollDamperWashout = MavF15RollDamperSchedule.AfitResearch(
                0f, MavF15RollDamperWashoutShape.Linear, "synthetic");
            Record(
                !washoutDowngraded.IsFullyExactTargetAuthoritative,
                "a research-grade washout schedule also revokes the exact-target claim",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [C9]

        private static void ValidateDifferentialStabilatorContainment(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[C9] Differential stabilator stays out of the shared contract");

            MavF15ActualSurfaceState actual = MavF15ActualSurfaceState.FromActuator(
                new MavF15SurfaceState
                {
                    symmetricStabilatorDeg = -8f,
                    differentialStabilatorDeg = 6f,
                    aileronDeg = 12f,
                    rudderDeg = -4f
                });

            MavF15BaumannSurfaceState research =
                MavF15BaumannSurfaceState.FromPhysicalSurfaceState(actual);

            Record(
                Near(research.differentialTailDeg, 6f, Tolerance)
                && Near(research.aileronDeg, 12f, Tolerance),
                "the research adapter carries the OWNED differential tail through unchanged,"
                + " without re-deriving it from aileron",
                report, ref passed, ref failed);

            MavControlInput shared = new MavControlInput { aileronDeg = 12f };
            MavF15BaumannSurfaceState fallback =
                MavF15BaumannSurfaceState.FromCommonInput(shared, false, 0f);

            Record(
                Near(fallback.differentialTailDeg, 12f * 0.3f, Tolerance),
                "the fallback adapter applies the AFIT research relation DTALD = 0.3*DAILD ("
                + fallback.differentialTailDeg.ToString("F3") + ")",
                report, ref passed, ref failed);

            Record(
                !Near(research.differentialTailDeg, fallback.differentialTailDeg, Tolerance),
                "the owned path and the research fallback give DIFFERENT answers, so a rig"
                + " silently falling back to the research relation is detectable",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [C10]

        private static void ValidateAxisRouting(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[C10] Axis routing: each command reaches exactly its own surfaces");

            MavF15ControlLawSchedules mech = MechanicalOnly();
            MavFlightState still = FlightState(alphaDeg: 0f);

            // Pitch: symmetric stabilator only.
            MavF15SurfaceState pitch =
                Solve(MavF15FcsMode.AFITResearch, mech, Command(1f, 0f, 0f), still)
                    .requested.channels;

            Record(
                Near(pitch.symmetricStabilatorDeg, -15f, Tolerance)
                && Near(pitch.aileronDeg, 0f, Tolerance)
                && Near(pitch.differentialStabilatorDeg, 0f, Tolerance)
                && Near(pitch.rudderDeg, 0f, Tolerance),
                "a pitch command reaches the symmetric stabilator ONLY"
                + " (stab " + pitch.symmetricStabilatorDeg.ToString("F2")
                + ", ail/difftail/rudder all 0)",
                report, ref passed, ref failed);

            // Roll: aileron AND differential stabilator, nothing else.
            MavF15SurfaceState roll =
                Solve(MavF15FcsMode.AFITResearch, mech, Command(0f, 1f, 0f), still)
                    .requested.channels;

            Record(
                Near(roll.aileronDeg, 20f, Tolerance)
                && Near(roll.differentialStabilatorDeg, 10f, Tolerance),
                "a roll command reaches BOTH the aileron (" + roll.aileronDeg.ToString("F2")
                + ") and the differential stabilator ("
                + roll.differentialStabilatorDeg.ToString("F2") + ")",
                report, ref passed, ref failed);

            Record(
                Near(roll.symmetricStabilatorDeg, 0f, Tolerance)
                && Near(roll.rudderDeg, 0f, Tolerance),
                "...and does not disturb the symmetric stabilator or the rudder",
                report, ref passed, ref failed);

            // Yaw: rudder only.
            MavF15SurfaceState yaw =
                Solve(MavF15FcsMode.AFITResearch, mech, Command(0f, 0f, 1f), still)
                    .requested.channels;

            Record(
                Near(yaw.rudderDeg, 30f, Tolerance)
                && Near(yaw.symmetricStabilatorDeg, 0f, Tolerance)
                && Near(yaw.aileronDeg, 0f, Tolerance)
                && Near(yaw.differentialStabilatorDeg, 0f, Tolerance),
                "a yaw command reaches the rudder ONLY (rudder " + yaw.rudderDeg.ToString("F2") + ")",
                report, ref passed, ref failed);

            // The mechanical demand is exposed separately from the augmented request.
            MavF15FcsSolution withCas = Solve(
                MavF15FcsMode.AFITResearch, WithAllCas(mech), Command(1f, 0f, 0f),
                FlightState(alphaDeg: 0f, q: 1f));

            Record(
                Near(withCas.mechanicalDemand.symmetricStabilatorDeg, -15f, Tolerance)
                && !Near(withCas.requested.channels.symmetricStabilatorDeg, -15f, Tolerance),
                "the mechanical demand is reported separately from the augmented request"
                + " (mechanical " + withCas.mechanicalDemand.symmetricStabilatorDeg.ToString("F2")
                + " vs requested "
                + withCas.requested.channels.symmetricStabilatorDeg.ToString("F2") + ")",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [C11]

        private static void ValidateCasDisengagement(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[C11] CAS disengaged produces exactly zero augmentation");

            MavF15ControlLawSchedules s = WithAllCas(MechanicalOnly());
            MavFlightState moving = FlightState(alphaDeg: 5f, p: 1f, q: 1f, r: 1f);

            MavF15PilotCommand engaged = Command(0f, 0f, 0f);
            MavF15PilotCommand disengaged = Command(0f, 0f, 0f);
            disengaged.pitchCasEngaged = false;
            disengaged.rollCasEngaged = false;
            disengaged.yawCasEngaged = false;

            MavF15SurfaceState on =
                Solve(MavF15FcsMode.AFITResearch, s, engaged, moving).requested.channels;
            MavF15SurfaceState off =
                Solve(MavF15FcsMode.AFITResearch, s, disengaged, moving).requested.channels;

            Record(
                !Near(on.symmetricStabilatorDeg, 0f, Tolerance)
                && !Near(on.differentialStabilatorDeg, 0f, Tolerance)
                && !Near(on.rudderDeg, 0f, Tolerance),
                "with CAS engaged and body rates present, all three axes are augmented",
                report, ref passed, ref failed);

            Record(
                Near(off.symmetricStabilatorDeg, 0f, Tolerance)
                && Near(off.differentialStabilatorDeg, 0f, Tolerance)
                && Near(off.rudderDeg, 0f, Tolerance),
                "with all CAS axes disengaged the SAME state produces exactly zero increment"
                + " - the mechanical path alone, which is stick-centred neutral",
                report, ref passed, ref failed);

            // Per-axis disengagement must be independent.
            MavF15PilotCommand pitchOnlyOff = Command(0f, 0f, 0f);
            pitchOnlyOff.pitchCasEngaged = false;

            MavF15SurfaceState mixed =
                Solve(MavF15FcsMode.AFITResearch, s, pitchOnlyOff, moving).requested.channels;

            Record(
                Near(mixed.symmetricStabilatorDeg, 0f, Tolerance)
                && !Near(mixed.differentialStabilatorDeg, 0f, Tolerance)
                && !Near(mixed.rudderDeg, 0f, Tolerance),
                "disengaging ONLY pitch CAS leaves roll and yaw augmentation running",
                report, ref passed, ref failed);

            // Disengaging CAS must not disable the mechanical path.
            MavF15PilotCommand stickWithCasOff = Command(1f, 0f, 0f);
            stickWithCasOff.pitchCasEngaged = false;

            MavF15SurfaceState mechanicalStillFlies =
                Solve(MavF15FcsMode.AFITResearch, s, stickWithCasOff, moving).requested.channels;

            Record(
                Near(mechanicalStillFlies.symmetricStabilatorDeg, -15f, Tolerance),
                "with pitch CAS off the MECHANICAL path still flies the aircraft"
                + " (stabilator " + mechanicalStillFlies.symmetricStabilatorDeg.ToString("F2")
                + ") - that is the point of having one",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [C12]

        private static void ValidateModeProvenanceFloor(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[C12] FCS mode provenance floor refuses mixed configurations");

            string reason;

            Record(
                MavF15FcsModes.ProvenanceFloor(MavF15FcsMode.ExactNasa836Unavailable)
                    == MavEngineDataProvenance.Authoritative
                && MavF15FcsModes.ProvenanceFloor(MavF15FcsMode.F15FamilyReference)
                    == MavEngineDataProvenance.PublicReference
                && MavF15FcsModes.ProvenanceFloor(MavF15FcsMode.AFITResearch)
                    == MavEngineDataProvenance.CrossValidationOnly,
                "each mode declares its own provenance floor",
                report, ref passed, ref failed);

            Record(
                !MavF15FcsModes.Admits(
                    MavF15FcsMode.AFITResearch, MavEngineDataProvenance.Unavailable),
                "Unavailable is admitted by NO mode - an absent number is not a low-grade one",
                report, ref passed, ref failed);

            // A research-grade gain must be refused in the exact-target mode.
            MavF15ControlLawSchedules research = MechanicalOnly();
            Record(
                !research.IsConsistentWith(MavF15FcsMode.ExactNasa836Unavailable, out reason),
                "research-grade gains are REFUSED in ExactNasa836Unavailable (" + reason + ")",
                report, ref passed, ref failed);

            MavF15FcsSolution refusedSolution = Solve(
                MavF15FcsMode.ExactNasa836Unavailable, research, Command(1f, 1f, 1f),
                FlightState(alphaDeg: 5f));

            Record(
                IsNeutral(refusedSolution.requested) && !refusedSolution.modeConsistent,
                "and the law outputs NEUTRAL rather than running them anyway",
                report, ref passed, ref failed);

            // The specific mixing case: one authoritative gain alongside preproduction ones.
            MavF15ControlLawSchedules mixed = AllGainsAt(MavEngineDataProvenance.PublicReference);
            mixed.ariRudderPerAileron = new MavF15ControlGain
            {
                value = 0.25f,
                provenance = MavEngineDataProvenance.CrossValidationOnly,
                sourceNote = "SYNTHETIC research-grade gain"
            };

            Record(
                !mixed.IsConsistentWith(MavF15FcsMode.F15FamilyReference, out reason),
                "a research-grade gain inside an otherwise F15FamilyReference set is refused ("
                + reason + ") - this is the anti-mixing check",
                report, ref passed, ref failed);

            Record(
                mixed.IsConsistentWith(MavF15FcsMode.AFITResearch, out reason),
                "...and the same set IS consistent once the mode is lowered to AFITResearch",
                report, ref passed, ref failed);

            // Unavailable gains never make a set inconsistent - they just do not run.
            Record(
                MavF15ControlLawSchedules.Unavailable()
                    .IsConsistentWith(MavF15FcsMode.ExactNasa836Unavailable, out reason),
                "an all-Unavailable set is consistent with every mode - nothing is mixed",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [C13]

        private static void ValidateNoRigidbodyWrites(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[C13] No F-15 FCS source writes Rigidbody motion state");

            MavOwnershipScanResult scan =
                MavFlightDynamicsOwnershipScan.Scan(ResolveFlightDynamicsRoot());

            if (!scan.sourcesAvailable)
            {
                Record(true,
                    "SKIPPED: flight-dynamics sources are not present (player build)",
                    report, ref passed, ref failed);
                return;
            }

            // The shared scan covers the whole FDM tree, which includes every F-15 FCS file.
            // Re-checking the F-15 subset explicitly keeps the failure message specific.
            List<string> f15Violations = new List<string>();
            if (scan.violations != null)
            {
                for (int i = 0; i < scan.violations.Count; i++)
                {
                    if (scan.violations[i].Contains("MavF15"))
                        f15Violations.Add(scan.violations[i]);
                }
            }

            Record(
                f15Violations.Count == 0,
                "no MavF15* source applies a force, torque or velocity write ("
                + scan.filesScanned + " FDM files scanned)"
                + (f15Violations.Count == 0 ? "" : "; first: " + f15Violations[0]),
                report, ref passed, ref failed);

            Record(
                scan.IsClean,
                "and the whole flight-dynamics tree is still clean"
                + (scan.IsClean
                    ? ""
                    : "; " + scan.violations.Count + " violation(s), first: " + scan.violations[0]),
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [C14]

        private static void ValidateFiniteOutputs(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[C14] All outputs finite across a wide state sweep");

            MavF15ControlLawSchedules s = WithAllCas(MechanicalOnly());
            s.pitchRatioChanger = Gain(0.6f);
            s.rollRatioChanger = Gain(0.7f);
            s.ariRudderPerAileron = Gain(0.25f);
            s.stallInhibitorAlphaThresholdDeg = Gain(20f);
            s.stallInhibitorDegPerDegAlpha = Gain(0.5f);
            s.turnCoordinationDegPerRadSec = Gain(3f);
            s.rollRateToYawCrossfeedDegPerRadSec = Gain(2f);
            s.rollDamperWashout = MavF15RollDamperSchedule.AfitResearch(
                0f, MavF15RollDamperWashoutShape.Linear, "synthetic");

            bool allFinite = true;
            float worst = 0f;
            string worstAt = "none";
            int samples = 0;

            float[] commands = { -1f, 0f, 1f };
            float[] alphas = { -10f, 0f, 10f, 20.2f, 30f, 60f };
            float[] rates = { -2f, 0f, 2f };

            foreach (float cmd in commands)
            foreach (float alpha in alphas)
            foreach (float rate in rates)
            {
                MavFlightState state = FlightState(alpha, p: rate, q: rate, r: rate);
                state.specificForceValid = true;
                state.specificForceAeroBodyG = new Vector3(0f, 0f, -rate);

                MavF15SurfaceState o =
                    Solve(MavF15FcsMode.AFITResearch, s, Command(cmd, cmd, cmd), state)
                        .requested.channels;
                samples++;

                if (!o.IsFinite())
                {
                    allFinite = false;
                    worstAt = "alpha=" + alpha + " rate=" + rate + " cmd=" + cmd;
                    break;
                }

                float m = Mathf.Max(
                    Mathf.Abs(o.symmetricStabilatorDeg),
                    Mathf.Max(
                        Mathf.Abs(o.differentialStabilatorDeg),
                        Mathf.Max(Mathf.Abs(o.aileronDeg), Mathf.Abs(o.rudderDeg))));

                if (m > worst)
                {
                    worst = m;
                    worstAt = "alpha=" + alpha + " rate=" + rate + " cmd=" + cmd;
                }
            }

            Record(allFinite,
                "every requested surface is finite across " + samples + " states"
                + (allFinite ? " (worst magnitude " + worst.ToString("F2") + " deg at " + worstAt + ")"
                             : " - non-finite at " + worstAt),
                report, ref passed, ref failed);

            // A non-finite flight state must not propagate into the request.
            MavFlightState broken = FlightState(alphaDeg: 5f);
            broken.aeroBodyRatesRadSec = new Vector3(float.NaN, 0f, 0f);

            MavF15SurfaceState fromBroken =
                Solve(MavF15FcsMode.AFITResearch, MechanicalOnly(), Command(0f, 0f, 0f), broken)
                    .requested.channels;

            Record(
                fromBroken.IsFinite(),
                "a NaN body rate does not reach the request when the roll CAS gain is"
                + " unavailable - an unavailable stage cannot propagate a bad input",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [C15]

        private static void ValidateActualStateOwnership(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[C15] The actuator is the sole owner of actual surface state");

            // Requested and actual are distinct TYPES. The aerodynamic adapter accepts only the
            // actual one, so handing it a request does not compile - which is the invariant.
            MavF15RequestedSurfaceState requested = MavF15RequestedSurfaceState.From(
                new MavF15SurfaceState { aileronDeg = 12f, differentialStabilatorDeg = 6f });

            MavF15ActualSurfaceState actual = MavF15ActualSurfaceState.FromActuator(
                new MavF15SurfaceState { aileronDeg = 3f, differentialStabilatorDeg = 1f });

            Record(
                typeof(MavF15RequestedSurfaceState) != typeof(MavF15ActualSurfaceState),
                "requested and actual surface state are DISTINCT types, so the aerodynamic"
                + " model cannot be handed a request by mistake",
                report, ref passed, ref failed);

            Record(
                !Near(requested.channels.aileronDeg, actual.channels.aileronDeg, Tolerance),
                "they carry independent values - a request is not a position",
                report, ref passed, ref failed);

            // The actuator is what turns one into the other, through its declared authority.
            MavF15SurfaceLimits limits = SyntheticLimits(rateDegSec: 0f);
            bool refusedFlag;
            MavF15SurfaceState bounded = limits.Clamp(requested.channels, out refusedFlag);
            MavF15ActualSurfaceState produced = MavF15ActualSurfaceState.FromActuator(
                MavF15ControlActuator.StepChannels(
                    MavF15SurfaceState.Neutral, bounded, limits, 0.02f));

            Record(
                Near(produced.channels.aileronDeg, 12f, Tolerance)
                && Near(produced.channels.differentialStabilatorDeg, 6f, Tolerance),
                "the actuator converts a bounded request into an actual state",
                report, ref passed, ref failed);

            // With authority undeclared the same request produces zero actual deflection.
            MavF15SurfaceLimits none = MavF15SurfaceLimits.UnavailableExactTarget();
            MavF15SurfaceState boundedNone = none.Clamp(requested.channels, out refusedFlag);
            MavF15ActualSurfaceState refused = MavF15ActualSurfaceState.FromActuator(
                MavF15ControlActuator.StepChannels(
                    MavF15SurfaceState.Neutral, boundedNone, none, 0.02f));

            Record(
                Near(refused.channels.aileronDeg, 0f, Tolerance)
                && Near(refused.channels.differentialStabilatorDeg, 0f, Tolerance),
                "with undeclared authority the SAME request yields zero actual deflection"
                + " - the request never becomes a position on its own",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>
        /// Directory holding the flight-dynamics sources.
        ///
        /// Inside Unity this is Application.dataPath. Outside it - the fast compile-and-run loop
        /// used during development invokes these fixtures from a plain .NET host - that property
        /// throws, so the path is recovered by walking up from the working directory instead. The
        /// scan itself is identical either way; only the way the root is found differs, and the
        /// authoritative run is always the one inside Unity.
        /// </summary>
        private static string ResolveFlightDynamicsRoot()
        {
            try
            {
                string fromUnity = Path.Combine(
                    Application.dataPath,
                    MavFlightDynamicsOwnershipScan.FlightDynamicsRelativePath);

                if (Directory.Exists(fromUnity))
                    return fromUnity;
            }
            catch (System.Exception)
            {
                // No Unity player loaded. Fall through to the directory walk.
            }

            DirectoryInfo dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            for (int depth = 0; depth < 8 && dir != null; depth++)
            {
                string candidate = Path.Combine(
                    Path.Combine(dir.FullName, "Assets"),
                    MavFlightDynamicsOwnershipScan.FlightDynamicsRelativePath);

                if (Directory.Exists(candidate))
                    return candidate;

                dir = dir.Parent;
            }

            return null;
        }

        private static MavF15FcsSolution Solve(
            MavF15FcsMode mode,
            MavF15ControlLawSchedules schedules,
            MavF15PilotCommand command,
            MavFlightState state)
        {
            return MavF15ControlLaw.Solve(mode, schedules, command, state, null);
        }

        private static bool IsNeutral(MavF15RequestedSurfaceState r)
        {
            return Near(r.channels.symmetricStabilatorDeg, 0f, Tolerance)
                && Near(r.channels.differentialStabilatorDeg, 0f, Tolerance)
                && Near(r.channels.aileronDeg, 0f, Tolerance)
                && Near(r.channels.rudderDeg, 0f, Tolerance);
        }

        private static MavF15PilotCommand Command(float pitch, float roll, float yaw)
        {
            return new MavF15PilotCommand
            {
                pitch = pitch,
                roll = roll,
                yaw = yaw,
                throttle01 = 0f,
                pitchCasEngaged = true,
                rollCasEngaged = true,
                yawCasEngaged = true
            };
        }

        private static MavF15ControlGain Gain(float value)
        {
            return new MavF15ControlGain
            {
                value = value,
                provenance = MavEngineDataProvenance.CrossValidationOnly,
                sourceNote = "SYNTHETIC validation value. Not F-15 data."
            };
        }

        private static MavF15SurfaceChannelLimits Declared(float minDeg, float maxDeg)
        {
            return new MavF15SurfaceChannelLimits
            {
                minDeg = minDeg,
                maxDeg = maxDeg,
                rateLimitDegSec = 0f,
                travelProvenance = MavEngineDataProvenance.MaverickTuning,
                rateProvenance = MavEngineDataProvenance.Unavailable,
                sourceNote = "SYNTHETIC validation value. Not F-15 data."
            };
        }

        private static MavF15SurfaceLimits SyntheticLimits(float rateDegSec)
        {
            MavF15SurfaceChannelLimits stab = Declared(-25f, 15f);
            MavF15SurfaceChannelLimits diff = Declared(-10f, 10f);
            MavF15SurfaceChannelLimits ail = Declared(-20f, 20f);
            MavF15SurfaceChannelLimits rud = Declared(-30f, 30f);

            if (rateDegSec > 0f)
            {
                stab.rateLimitDegSec = rateDegSec;
                stab.rateProvenance = MavEngineDataProvenance.MaverickTuning;
                diff.rateLimitDegSec = rateDegSec;
                diff.rateProvenance = MavEngineDataProvenance.MaverickTuning;
                ail.rateLimitDegSec = rateDegSec;
                ail.rateProvenance = MavEngineDataProvenance.MaverickTuning;
                rud.rateLimitDegSec = rateDegSec;
                rud.rateProvenance = MavEngineDataProvenance.MaverickTuning;
            }

            return new MavF15SurfaceLimits
            {
                symmetricStabilator = stab,
                differentialStabilator = diff,
                aileron = ail,
                rudder = rud
            };
        }

        /// <summary>Mechanical gearing only - every augmentation stage still unavailable.</summary>
        private static MavF15ControlLawSchedules MechanicalOnly()
        {
            MavF15ControlLawSchedules s = MavF15ControlLawSchedules.Unavailable();
            s.pitchStickToStabilatorDegPerUnit = Gain(-15f);
            s.rollStickToAileronDegPerUnit = Gain(20f);
            s.rollStickToDifferentialStabilatorDegPerUnit = Gain(10f);
            s.pedalToRudderDegPerUnit = Gain(30f);
            return s;
        }

        /// <summary>Mechanical gearing plus all three CAS axes.</summary>
        private static MavF15ControlLawSchedules WithAllCas(MavF15ControlLawSchedules s)
        {
            s.pitchRateFeedbackDegPerRadSec = Gain(4f);
            s.rollRateFeedbackDegPerRadSec = Gain(3f);
            s.yawRateFeedbackDegPerRadSec = Gain(5f);
            return s;
        }

        private static MavF15ControlLawSchedules AllGainsAt(MavEngineDataProvenance provenance)
        {
            MavF15ControlGain g = new MavF15ControlGain
            {
                value = 1f,
                provenance = provenance,
                sourceNote = "SYNTHETIC validation value. Not F-15 data."
            };

            return new MavF15ControlLawSchedules
            {
                pitchStickToStabilatorDegPerUnit = g,
                rollStickToAileronDegPerUnit = g,
                rollStickToDifferentialStabilatorDegPerUnit = g,
                pedalToRudderDegPerUnit = g,
                pitchRatioChanger = g,
                rollRatioChanger = g,
                pitchRateFeedbackDegPerRadSec = g,
                normalLoadFactorFeedbackDegPerG = g,
                rollRateFeedbackDegPerRadSec = g,
                yawRateFeedbackDegPerRadSec = g,
                ariRudderPerAileron = g,
                stallInhibitorAlphaThresholdDeg = g,
                stallInhibitorDegPerDegAlpha = g,
                turnCoordinationDegPerRadSec = g,
                rollRateToYawCrossfeedDegPerRadSec = g,
                rollDamperWashout = new MavF15RollDamperSchedule
                {
                    fullAuthorityAlphaDeg = 10f,
                    zeroAuthorityAlphaDeg = 20f,
                    shape = MavF15RollDamperWashoutShape.Linear,
                    provenance = provenance,
                    sourceNote = "SYNTHETIC validation value. Not F-15 data."
                }
            };
        }

        private static MavFlightState FlightState(
            float alphaDeg, float p = 0f, float q = 0f, float r = 0f)
        {
            MavFlightState state = new MavFlightState();
            state.alphaRad = alphaDeg * Mathf.Deg2Rad;
            state.betaRad = 0f;
            state.trueAirspeedMps = 200f;
            state.mach = 0.6f;
            state.aeroBodyRatesRadSec = new Vector3(p, q, r);
            state.specificForceValid = false;
            return state;
        }

        private static bool Near(float a, float b, float tolerance)
        {
            return Mathf.Abs(a - b) <= tolerance;
        }

        private static void Record(
            bool condition, string label,
            StringBuilder report, ref int passed, ref int failed)
        {
            if (condition) passed++; else failed++;
            report.Append(condition ? "  PASS  " : "  FAIL  ").AppendLine(label);
        }
    }
}
