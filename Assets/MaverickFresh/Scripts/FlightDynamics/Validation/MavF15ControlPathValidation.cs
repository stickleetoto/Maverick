using System.Text;
using UnityEngine;
using MaverickFresh.FlightDynamics.F15;

namespace MaverickFresh.FlightDynamics.Validation
{
    /// <summary>
    /// Deterministic checks for the F-15 control path:
    ///
    ///     pilot command -> MavF15ControlLaw -> requested surface state
    ///                   -> MavF15ControlActuator -> actual surface state -> aero
    ///
    /// Covered:
    ///   [C0] the four-channel surface state addresses every channel independently
    ///   [C1] undeclared surface authority is REFUSED, not passed through
    ///   [C2] half-filled limit declarations are rejected as contradictions
    ///   [C3] declared travel clamps; declared rate rate-limits; no declared rate steps instantly
    ///   [C4] the control law fails closed with no sourced gearing
    ///   [C5] each FCS stage gates on its own gains, independently
    ///   [C6] ARI is fed from COMMANDED roll, not from the augmented aileron position
    ///   [C7] the high-AOA roll-damper washout ramps and saturates correctly
    ///   [C8] the law never claims NASA 836 authority it does not have
    ///   [C9] differential stabilator never leaks into the shared MavControlInput contract
    ///
    /// Every check runs through the real production entry points - the same statics the
    /// MonoBehaviours call - so no GameObject, Rigidbody, scene or play-mode session is created.
    ///
    /// None of this asserts that any F-15 NUMBER is correct. No F-15 control gain has been
    /// recovered for NASA 836; these fixtures use obviously synthetic gains, explicitly marked
    /// MaverickTuning, purely to drive the architecture. What is being validated is ownership and
    /// gating, not aircraft data.
    /// </summary>
    public static class MavF15ControlPathValidation
    {
        private const float Tolerance = 1e-4f;

        public static string RunAll(out int passed, out int failed)
        {
            passed = 0;
            failed = 0;
            StringBuilder report = new StringBuilder(8192);
            report.AppendLine("F-15 Control Path - Ownership and Gating Validation");
            report.AppendLine("===================================================");
            report.AppendLine("Synthetic gains only. No F-15 aircraft number is asserted here.");

            ValidateSurfaceStateAddressing(report, ref passed, ref failed);
            ValidateUndeclaredAuthorityRefused(report, ref passed, ref failed);
            ValidateLimitSelfConsistency(report, ref passed, ref failed);
            ValidateActuatorTravelAndRate(report, ref passed, ref failed);
            ValidateControlLawFailsClosed(report, ref passed, ref failed);
            ValidateStageGating(report, ref passed, ref failed);
            ValidateAriSource(report, ref passed, ref failed);
            ValidateWashout(report, ref passed, ref failed);
            ValidateAuthorityClaims(report, ref passed, ref failed);
            ValidateDifferentialStabilatorContainment(report, ref passed, ref failed);

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

            // Each channel must be independently addressable, or a control law writing one
            // channel would silently disturb another.
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

            // A channel declared as Unavailable but with a non-empty interval left over from an
            // edit must still be refused: the provenance is what decides, not the numbers.
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
            sourceWithoutRate.rateLimitDegSec = 0f;
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

            // No sourced rate: the channel must reach the target in one step, and that must be
            // visible as "no actuator dynamics" rather than look like an instantaneous surface.
            MavF15SurfaceState afterInstant = MavF15ControlActuator.StepChannels(
                MavF15SurfaceState.Neutral, bounded, limits, 0.02f);

            Record(
                Near(afterInstant.symmetricStabilatorDeg, 15f, Tolerance),
                "with NO sourced rate the channel steps instantly (declared, not hidden)",
                report, ref passed, ref failed);

            // Sourced rate: 60 deg/s for 0.02 s is 1.2 deg, not the full 15.
            MavF15SurfaceLimits rated = SyntheticLimits(rateDegSec: 60f);
            MavF15SurfaceState afterRated = MavF15ControlActuator.StepChannels(
                MavF15SurfaceState.Neutral, bounded, rated, 0.02f);

            Record(
                Near(afterRated.symmetricStabilatorDeg, 1.2f, Tolerance),
                "with a sourced 60 deg/s rate, one 20 ms step moves 1.2 deg, not the full 15 deg"
                + " (actual " + afterRated.symmetricStabilatorDeg.ToString("F4") + ")",
                report, ref passed, ref failed);

            // And it must converge rather than oscillate or overshoot.
            MavF15SurfaceState converging = MavF15SurfaceState.Neutral;
            for (int i = 0; i < 100; i++)
                converging = MavF15ControlActuator.StepChannels(converging, bounded, rated, 0.02f);

            Record(
                Near(converging.symmetricStabilatorDeg, 15f, Tolerance)
                && Near(converging.rudderDeg, -30f, Tolerance),
                "a rate-limited channel converges on the target without overshoot",
                report, ref passed, ref failed);

            // Zero dt must not move a surface, or a paused physics step would still fly.
            MavF15SurfaceState frozen = MavF15ControlActuator.StepChannels(
                MavF15SurfaceState.Neutral, bounded, rated, 0f);
            Record(
                frozen.symmetricStabilatorDeg == 0f,
                "a zero timestep moves a rate-limited surface not at all",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [C4]

        private static void ValidateControlLawFailsClosed(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[C4] Control law fails closed without sourced gearing");

            MavF15ControlLawSchedules none = MavF15ControlLawSchedules.Unavailable();
            float washout;

            MavF15SurfaceState result = MavF15ControlLaw.Solve(
                none, FullDeflectionCommand(), FlightState(alphaDeg: 5f), out washout, null);

            Record(!none.MechanicalPathAvailable,
                "the default schedule set declares no mechanical gearing",
                report, ref passed, ref failed);

            Record(
                result.symmetricStabilatorDeg == 0f && result.differentialStabilatorDeg == 0f
                && result.aileronDeg == 0f && result.rudderDeg == 0f,
                "FULL stick, full pedal, non-zero rates -> every surface stays neutral."
                + " The aircraft is honestly unflyable rather than flying on invented gains",
                report, ref passed, ref failed);

            // Partially sourced gearing is still not gearing: all four are needed to form a demand.
            MavF15ControlLawSchedules partial = MavF15ControlLawSchedules.Unavailable();
            partial.pitchStickToStabilatorDegPerUnit = Gain(-15f);

            MavF15SurfaceState partialResult = MavF15ControlLaw.Solve(
                partial, FullDeflectionCommand(), FlightState(alphaDeg: 5f), out washout, null);

            Record(
                !partial.MechanicalPathAvailable && partialResult.symmetricStabilatorDeg == 0f,
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
            float washout;

            // Mechanical only, with a pitch rate present. No CAS is sourced, so the pitch rate
            // must have no effect whatsoever.
            MavF15SurfaceState noCas = MavF15ControlLaw.Solve(
                mech, PitchCommand(1f), FlightState(alphaDeg: 5f, q: 0.5f), out washout, null);

            Record(
                Near(noCas.symmetricStabilatorDeg, -15f, Tolerance),
                "with no sourced pitch CAS, a 0.5 rad/s pitch rate changes nothing"
                + " (stabilator " + noCas.symmetricStabilatorDeg.ToString("F3") + " = pure gearing)",
                report, ref passed, ref failed);

            // Add pitch rate feedback only.
            MavF15ControlLawSchedules withPitchCas = mech;
            withPitchCas.pitchRateFeedbackDegPerRadSec = Gain(4f);

            MavF15SurfaceState casOn = MavF15ControlLaw.Solve(
                withPitchCas, PitchCommand(1f), FlightState(alphaDeg: 5f, q: 0.5f),
                out washout, null);

            Record(
                Near(casOn.symmetricStabilatorDeg, -15f - (0.5f * 4f), Tolerance),
                "sourcing ONLY pitch-rate feedback switches on ONLY that term"
                + " (" + casOn.symmetricStabilatorDeg.ToString("F3") + " = -15 - 2.0)",
                report, ref passed, ref failed);

            Record(
                Near(casOn.rudderDeg, 0f, Tolerance) && Near(casOn.aileronDeg, 0f, Tolerance),
                "switching on pitch CAS does not switch on yaw or roll augmentation",
                report, ref passed, ref failed);

            // Load-factor feedback must be skipped when the accelerometer is not valid: a zero
            // reading from an unflown body is not a real 0 g.
            MavF15ControlLawSchedules withNz = mech;
            withNz.normalLoadFactorFeedbackDegPerG = Gain(2f);

            MavFlightState invalidNz = FlightState(alphaDeg: 5f);
            invalidNz.specificForceValid = false;
            invalidNz.specificForceAeroBodyG = new Vector3(0f, 0f, -4f);

            MavF15SurfaceState nzSkipped = MavF15ControlLaw.Solve(
                withNz, PitchCommand(1f), invalidNz, out washout, null);

            Record(
                Near(nzSkipped.symmetricStabilatorDeg, -15f, Tolerance),
                "load-factor feedback is SKIPPED while the accelerometer reads invalid,"
                + " rather than closing a loop on a phantom 4 g",
                report, ref passed, ref failed);

            MavFlightState validNz = invalidNz;
            validNz.specificForceValid = true;

            MavF15SurfaceState nzApplied = MavF15ControlLaw.Solve(
                withNz, PitchCommand(1f), validNz, out washout, null);

            Record(
                Near(nzApplied.symmetricStabilatorDeg, -15f - (4f * 2f), Tolerance),
                "the same reading marked valid IS used ("
                + nzApplied.symmetricStabilatorDeg.ToString("F3") + " = -15 - 8)",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [C6]

        private static void ValidateAriSource(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[C6] ARI is fed from commanded roll, not augmented aileron");

            // Roll CAS acts on the differential stabilator. If the ARI were instead fed from a
            // post-augmentation roll quantity, a roll RATE with the stick centred would produce
            // rudder. It must not.
            MavF15ControlLawSchedules s = MechanicalOnly();
            s.rollRateFeedbackDegPerRadSec = Gain(3f);
            s.ariRudderPerAileron = Gain(0.25f);

            float washout;
            MavF15SurfaceState stickCentred = MavF15ControlLaw.Solve(
                s, MavPilotCommand.Neutral, FlightState(alphaDeg: 5f, p: 1.0f), out washout, null);

            Record(
                Near(stickCentred.rudderDeg, 0f, Tolerance),
                "a 1 rad/s roll rate with the stick centred produces NO rudder"
                + " (rudder " + stickCentred.rudderDeg.ToString("F4") + ")"
                + " - the ARI is not closing a loop through the roll damper",
                report, ref passed, ref failed);

            Record(
                !Near(stickCentred.differentialStabilatorDeg, 0f, Tolerance),
                "...while the roll damper DOES respond to that rate"
                + " (differential stabilator "
                + stickCentred.differentialStabilatorDeg.ToString("F3") + ")",
                report, ref passed, ref failed);

            // Stick deflection, no rate: ARI contributes commandedAileron * gain = 20 * 0.25 = 5.
            MavF15SurfaceState stickRolled = MavF15ControlLaw.Solve(
                s, RollCommand(1f), FlightState(alphaDeg: 5f), out washout, null);

            Record(
                Near(stickRolled.rudderDeg, 5f, Tolerance),
                "full roll stick crossfeeds 20 deg aileron * 0.25 = 5 deg rudder"
                + " (actual " + stickRolled.rudderDeg.ToString("F3") + ")",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [C7]

        private static void ValidateWashout(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[C7] High-AOA roll-damper washout");

            MavF15ControlLawSchedules s = MechanicalOnly();
            s.rollRateFeedbackDegPerRadSec = Gain(3f);

            float washout;

            // No washout schedule: the damper must stay at full authority rather than fade by
            // some invented amount.
            MavF15ControlLaw.Solve(s, MavPilotCommand.Neutral,
                FlightState(alphaDeg: 40f, p: 1f), out washout, null);
            Record(Near(washout, 1f, Tolerance),
                "with no sourced washout schedule the damper stays at FULL authority at 40 deg"
                + " alpha - no invented fade",
                report, ref passed, ref failed);

            s.rollDamperWashoutStartAlphaDeg = Gain(20f);
            s.rollDamperWashoutEndAlphaDeg = Gain(30f);

            MavF15ControlLaw.Solve(s, MavPilotCommand.Neutral,
                FlightState(alphaDeg: 10f, p: 1f), out washout, null);
            Record(Near(washout, 1f, Tolerance),
                "below the start alpha the washout is 1.0 (damper unaffected)",
                report, ref passed, ref failed);

            MavF15ControlLaw.Solve(s, MavPilotCommand.Neutral,
                FlightState(alphaDeg: 25f, p: 1f), out washout, null);
            Record(Near(washout, 0.5f, Tolerance),
                "halfway up the ramp the washout is 0.5 (actual " + washout.ToString("F3") + ")",
                report, ref passed, ref failed);

            MavF15SurfaceState fullyWashed = MavF15ControlLaw.Solve(
                s, MavPilotCommand.Neutral, FlightState(alphaDeg: 45f, p: 1f), out washout, null);
            Record(
                Near(washout, 0f, Tolerance)
                && Near(fullyWashed.differentialStabilatorDeg, 0f, Tolerance),
                "above the end alpha the washout saturates at 0 and the damper contributes"
                + " nothing - it does not go negative and start driving the roll",
                report, ref passed, ref failed);

            // An inverted or degenerate ramp must disable the stage, not divide by zero.
            MavF15ControlLawSchedules degenerate = s;
            degenerate.rollDamperWashoutEndAlphaDeg = Gain(20f);
            MavF15ControlLaw.Solve(degenerate, MavPilotCommand.Neutral,
                FlightState(alphaDeg: 25f, p: 1f), out washout, null);
            Record(Near(washout, 1f, Tolerance) && !float.IsNaN(washout),
                "a zero-width ramp disables the stage instead of dividing by zero",
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

            // Every gain sourced, but at PublicReference - the TM-72861 case, where the evidence
            // is strong but the configuration is preproduction F-15 No. 8, not NASA 836.
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

            // One downgrade anywhere must revoke the claim.
            MavF15ControlLawSchedules oneDowngraded = authoritative;
            oneDowngraded.ariRudderPerAileron = Gain(0.25f);
            Record(
                !oneDowngraded.IsFullyExactTargetAuthoritative,
                "downgrading a SINGLE gain revokes the exact-target claim for the whole law",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [C9]

        private static void ValidateDifferentialStabilatorContainment(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[C9] Differential stabilator stays out of the shared contract");

            // The shared MavControlInput has no differential-tail field. The risk is that someone
            // folds it into the aileron field, which would hand every downstream reader two
            // different physical surfaces added together.
            MavF15SurfaceState state = new MavF15SurfaceState
            {
                symmetricStabilatorDeg = -8f,
                differentialStabilatorDeg = 6f,
                aileronDeg = 12f,
                rudderDeg = -4f
            };

            MavF15BaumannSurfaceState research =
                MavF15BaumannSurfaceState.FromPhysicalSurfaceState(state);

            Record(
                Near(research.differentialTailDeg, 6f, Tolerance)
                && Near(research.aileronDeg, 12f, Tolerance),
                "the research adapter carries the OWNED differential tail through unchanged,"
                + " without re-deriving it from aileron",
                report, ref passed, ref failed);

            // The fallback path is the only place the research relation may appear, and it must
            // be reachable only when nothing owns the channel.
            MavControlInput shared = new MavControlInput { aileronDeg = 12f };
            MavF15BaumannSurfaceState fallback =
                MavF15BaumannSurfaceState.FromCommonInput(shared, false, 0f);

            Record(
                Near(fallback.differentialTailDeg, 12f * 0.3f, Tolerance),
                "the fallback adapter applies the AFIT research relation DTALD = 0.3*DAILD"
                + " (" + fallback.differentialTailDeg.ToString("F3") + ")",
                report, ref passed, ref failed);

            Record(
                !Near(research.differentialTailDeg, fallback.differentialTailDeg, Tolerance),
                "the owned path and the research fallback give DIFFERENT answers, so a rig"
                + " silently falling back to the research relation is detectable",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- helpers

        private static MavF15ControlGain Gain(float value)
        {
            return new MavF15ControlGain
            {
                value = value,
                provenance = MavEngineDataProvenance.MaverickTuning,
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
                rollDamperWashoutStartAlphaDeg = new MavF15ControlGain
                {
                    value = 20f, provenance = provenance, sourceNote = "SYNTHETIC"
                },
                rollDamperWashoutEndAlphaDeg = new MavF15ControlGain
                {
                    value = 30f, provenance = provenance, sourceNote = "SYNTHETIC"
                }
            };
        }

        private static MavFlightState FlightState(float alphaDeg, float p = 0f, float q = 0f, float r = 0f)
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

        private static MavPilotCommand FullDeflectionCommand()
        {
            return new MavPilotCommand { pitch = 1f, roll = 1f, yaw = 1f, throttle01 = 1f };
        }

        private static MavPilotCommand PitchCommand(float pitch)
        {
            return new MavPilotCommand { pitch = pitch };
        }

        private static MavPilotCommand RollCommand(float roll)
        {
            return new MavPilotCommand { roll = roll };
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
