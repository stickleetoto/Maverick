using System.Text;
using UnityEngine;
using MaverickFresh.FlightDynamics.F16;

namespace MaverickFresh.FlightDynamics.Validation
{
    /// <summary>
    /// Deterministic checks for Phase 3: propulsion architecture (3A), attitude / flight state (3B)
    /// and atomic physics ownership (3C).
    ///
    /// Same discipline as Phase 1 and 2: every check runs the production code through pure static
    /// entry points, with no GameObject, Rigidbody, scene or play-mode session, and no component
    /// state mutated. Phase 1 and 2 suites remain regression requirements and are not replaced.
    /// </summary>
    public static class MavFlightDynamicsPhase3Validation
    {
        private const float Tolerance = 1e-4f;
        private const float StandardGravity = MavControlLawProtections.StandardGravityMps2;

        public static string RunAll(out int passed, out int failed)
        {
            passed = 0;
            failed = 0;
            StringBuilder report = new StringBuilder(8192);
            report.AppendLine("Maverick Flight Dynamics Phase 3 Validation");
            report.AppendLine("==========================================");

            ValidateThrustDataHonesty(report, ref passed, ref failed);
            ValidateDeckInterpolation(report, ref passed, ref failed);
            ValidateEnvelopePolicies(report, ref passed, ref failed);
            ValidateF16ThrustStillUnavailable(report, ref passed, ref failed);
            ValidatePoweredTrimWithDeck(report, ref passed, ref failed);
            ValidateThrottleInversionSafety(report, ref passed, ref failed);

            report.AppendLine();
            report.Append("RESULT: ")
                .Append(failed == 0 ? "PASS" : "FAIL")
                .Append("  passed=").Append(passed)
                .Append(" failed=").Append(failed);

            return report.ToString();
        }

        // ================================================================= [A0]

        /// <summary>
        /// The honesty rules for dimensional thrust. These are the checks that stop the propulsion
        /// architecture from becoming a route by which invented numbers acquire authority.
        /// </summary>
        private static void ValidateThrustDataHonesty(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[A0] Thrust data provenance and live-flight acceptance");

            Record(
                MavThrustDeckBase.IsPolicyAcceptableForLiveFlight(
                    MavThrustDataAuthority.Authoritative,
                    MavEnvelopeExcursionPolicy.ClampToValidatedEnvelope),
                "authoritative data with a clamping policy is acceptable for live flight",
                report, ref passed, ref failed);

            Record(
                MavThrustDeckBase.IsPolicyAcceptableForLiveFlight(
                    MavThrustDataAuthority.Authoritative,
                    MavEnvelopeExcursionPolicy.RejectUnsupportedState),
                "authoritative data that rejects unsupported states is acceptable for live flight",
                report, ref passed, ref failed);

            Record(
                !MavThrustDeckBase.IsPolicyAcceptableForLiveFlight(
                    MavThrustDataAuthority.Authoritative,
                    MavEnvelopeExcursionPolicy.MarkNonAuthoritativeExtrapolation),
                "authoritative data configured to EXTRAPOLATE is NOT acceptable for live flight: "
                + "a number outside the validated envelope is not evidence about the aircraft",
                report, ref passed, ref failed);

            Record(
                !MavThrustDeckBase.IsPolicyAcceptableForLiveFlight(
                    MavThrustDataAuthority.SyntheticBench,
                    MavEnvelopeExcursionPolicy.ClampToValidatedEnvelope)
                && !MavThrustDeckBase.IsPolicyAcceptableForLiveFlight(
                    MavThrustDataAuthority.SyntheticBench,
                    MavEnvelopeExcursionPolicy.RejectUnsupportedState),
                "SYNTHETIC BENCH data is never acceptable for live flight, whatever the policy",
                report, ref passed, ref failed);

            Record(
                !MavThrustDeckBase.IsPolicyAcceptableForLiveFlight(
                    MavThrustDataAuthority.Unavailable,
                    MavEnvelopeExcursionPolicy.ClampToValidatedEnvelope),
                "unavailable data is never acceptable for live flight",
                report, ref passed, ref failed);

            // An unconfigured tabulated deck must be honest rather than silently zero-valued.
            MavThrustDeckResult unconfigured = MavTabulatedThrustDeck.EvaluateTables(
                new float[0], new float[0], new float[0], new float[0], new float[0],
                MavThrustDataAuthority.Unavailable,
                MavEnvelopeExcursionPolicy.ClampToValidatedEnvelope,
                MavThrustDeckQuery.Create(0f, 0.5f, 100f));

            Record(
                !unconfigured.valid
                && unconfigured.authority == MavThrustDataAuthority.Unavailable
                && unconfigured.thrustN == 0f,
                "a deck with no data returns no result and says so, rather than returning zero as "
                + "though zero were a measurement",
                report, ref passed, ref failed);

            // Malformed tables must be refused, not interpolated through.
            MavThrustDeckResult malformed = MavTabulatedThrustDeck.EvaluateTables(
                new float[] { 0f, 1000f }, new float[] { 0f, 0.5f },
                new float[] { 1f, 2f },
                new float[] { 1f, 2f, 3f, 4f },
                new float[] { 1f, 2f, 3f, 4f },
                MavThrustDataAuthority.Authoritative,
                MavEnvelopeExcursionPolicy.ClampToValidatedEnvelope,
                MavThrustDeckQuery.Create(500f, 0.25f, 100f));

            Record(
                !malformed.valid,
                "a deck whose table size does not match its axes is refused, not interpolated",
                report, ref passed, ref failed);

            // The sourced Garza/Morelli power blend.
            Record(
                Near(MavThrustDeckMath.BlendByPowerPercent(1000f, 5000f, 9000f, 0f), 1000f)
                && Near(MavThrustDeckMath.BlendByPowerPercent(1000f, 5000f, 9000f, 50f), 5000f)
                && Near(MavThrustDeckMath.BlendByPowerPercent(1000f, 5000f, 9000f, 100f), 9000f),
                "the sourced power blend reproduces idle / military / maximum at 0 / 50 / 100 percent",
                report, ref passed, ref failed);

            Record(
                Near(MavThrustDeckMath.BlendByPowerPercent(1000f, 5000f, 9000f, 25f), 3000f)
                && Near(MavThrustDeckMath.BlendByPowerPercent(1000f, 5000f, 9000f, 75f), 7000f),
                "and interpolates linearly within each region",
                report, ref passed, ref failed);

            Record(
                Near(MavThrustDeckMath.BlendByPowerPercent(1000f, 5000f, 9000f, -50f), 1000f)
                && Near(MavThrustDeckMath.BlendByPowerPercent(1000f, 5000f, 9000f, 500f), 9000f),
                "power percent is clamped, so a bad power state cannot produce thrust off the deck",
                report, ref passed, ref failed);
        }

        // ================================================================= [A1]

        private static void ValidateDeckInterpolation(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[A1] Deterministic bounded interpolation");

            float[] altitudes = { 0f, 5000f, 10000f };
            float[] machs = { 0f, 0.5f, 1f };

            // Value = altitudeIndex * 100 + machIndex * 10, so every sample is hand-checkable.
            float[] table =
            {
                0f, 10f, 20f,
                100f, 110f, 120f,
                200f, 210f, 220f
            };

            Record(
                MavThrustDeckMath.IsStrictlyAscending(altitudes)
                && !MavThrustDeckMath.IsStrictlyAscending(new float[] { 0f, 0f, 1f })
                && !MavThrustDeckMath.IsStrictlyAscending(new float[] { 1f, 0f })
                && !MavThrustDeckMath.IsStrictlyAscending(null),
                "axis validation rejects duplicate, descending and missing axes",
                report, ref passed, ref failed);

            float value;
            bool inside;

            MavThrustDeckMath.TryBilinearSample(altitudes, machs, table, 0f, 0f, out value, out inside);
            Record(Near(value, 0f) && inside, "exact grid corner sample", report, ref passed, ref failed);

            MavThrustDeckMath.TryBilinearSample(altitudes, machs, table, 10000f, 1f, out value, out inside);
            Record(Near(value, 220f) && inside, "opposite grid corner sample", report, ref passed, ref failed);

            MavThrustDeckMath.TryBilinearSample(altitudes, machs, table, 2500f, 0.25f, out value, out inside);
            Record(Near(value, 55f) && inside,
                "bilinear midpoint is the average of the four surrounding nodes ("
                + value.ToString("F3") + ")",
                report, ref passed, ref failed);

            // Out of range without extrapolation: clamped to the edge, and reported as an excursion.
            MavThrustDeckMath.TryBilinearSample(altitudes, machs, table, 20000f, 0.5f, out value, out inside);
            Record(
                Near(value, 210f) && !inside,
                "beyond the axis the sample is clamped to the edge and flagged as outside the envelope",
                report, ref passed, ref failed);

            MavThrustDeckMath.TryBilinearSample(altitudes, machs, table, -5000f, 0f, out value, out inside);
            Record(
                Near(value, 0f) && !inside,
                "below the axis likewise clamps and flags",
                report, ref passed, ref failed);

            // With extrapolation the value genuinely leaves the table, but is bounded to one cell.
            float extrapolated;
            MavThrustDeckMath.TryBilinearSample(
                altitudes, machs, table, 15000f, 0f, true, out extrapolated, out inside);
            Record(
                Near(extrapolated, 300f) && !inside,
                "the extrapolation policy genuinely extrapolates rather than quietly clamping ("
                + extrapolated.ToString("F1") + ")",
                report, ref passed, ref failed);

            float farExtrapolated;
            MavThrustDeckMath.TryBilinearSample(
                altitudes, machs, table, 500000f, 0f, true, out farExtrapolated, out inside);
            Record(
                Near(farExtrapolated, 300f),
                "and is bounded to one grid cell beyond the edge, so it cannot run away ("
                + farExtrapolated.ToString("F1") + ")",
                report, ref passed, ref failed);

            // Determinism.
            float first, second;
            bool insideA, insideB;
            MavThrustDeckMath.TryBilinearSample(altitudes, machs, table, 3721f, 0.37f, out first, out insideA);
            MavThrustDeckMath.TryBilinearSample(altitudes, machs, table, 3721f, 0.37f, out second, out insideB);
            Record(first == second && insideA == insideB,
                "repeated lookups are bit-identical",
                report, ref passed, ref failed);
        }

        // ================================================================= [A2]

        private static void ValidateEnvelopePolicies(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[A2] Envelope excursion policies");

            float[] altitudes = { 0f, 10000f };
            float[] machs = { 0f, 1f };
            float[] idle = { 1000f, 1000f, 500f, 500f };
            float[] military = { 50000f, 55000f, 25000f, 27000f };
            float[] maximum = { 90000f, 100000f, 45000f, 50000f };

            MavThrustDeckQuery outside = MavThrustDeckQuery.Create(20000f, 0.5f, 100f);

            MavThrustDeckResult clamped = MavTabulatedThrustDeck.EvaluateTables(
                altitudes, machs, idle, military, maximum,
                MavThrustDataAuthority.Authoritative,
                MavEnvelopeExcursionPolicy.ClampToValidatedEnvelope, outside);

            Record(
                clamped.valid && !clamped.insideEnvelope
                && clamped.authority == MavThrustDataAuthority.Authoritative,
                "clamping keeps the result authoritative, because the edge value IS supported data, "
                + "and still reports the excursion",
                report, ref passed, ref failed);

            MavThrustDeckResult rejected = MavTabulatedThrustDeck.EvaluateTables(
                altitudes, machs, idle, military, maximum,
                MavThrustDataAuthority.Authoritative,
                MavEnvelopeExcursionPolicy.RejectUnsupportedState, outside);

            Record(
                !rejected.valid,
                "the rejecting policy returns no result at all outside the envelope",
                report, ref passed, ref failed);

            MavThrustDeckResult extrapolated = MavTabulatedThrustDeck.EvaluateTables(
                altitudes, machs, idle, military, maximum,
                MavThrustDataAuthority.Authoritative,
                MavEnvelopeExcursionPolicy.MarkNonAuthoritativeExtrapolation, outside);

            Record(
                extrapolated.valid
                && extrapolated.authority != MavThrustDataAuthority.Authoritative,
                "extrapolation DOWNGRADES authority even on an authoritative deck: the number is no "
                + "longer backed by data",
                report, ref passed, ref failed);

            MavThrustDeckResult insideResult = MavTabulatedThrustDeck.EvaluateTables(
                altitudes, machs, idle, military, maximum,
                MavThrustDataAuthority.Authoritative,
                MavEnvelopeExcursionPolicy.ClampToValidatedEnvelope,
                MavThrustDeckQuery.Create(5000f, 0.5f, 100f));

            Record(
                insideResult.valid && insideResult.insideEnvelope
                && insideResult.authority == MavThrustDataAuthority.Authoritative,
                "inside the envelope the result is authoritative and marked as such",
                report, ref passed, ref failed);

            Record(
                Near(insideResult.thrustN, 71250f, 1f),
                "and the interpolated maximum-power value matches a hand computation ("
                + insideResult.thrustN.ToString("F0") + " N)",
                report, ref passed, ref failed);
        }

        // ================================================================= [A3]

        private static void ValidateF16ThrustStillUnavailable(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[A3] F-16 dimensional thrust remains unavailable");

            // The sourced power dynamics are untouched by the new architecture.
            Record(
                Near(MavF16EnginePowerModel.ThrottleToCommandedPowerPercent(0f), 0f)
                && Near(MavF16EnginePowerModel.ThrottleToCommandedPowerPercent(1f), 100f),
                "the sourced Garza/Morelli throttle gearing is unchanged",
                report, ref passed, ref failed);

            // With no deck the model still produces exactly zero, reported as non-authoritative.
            MavPropulsiveLoads zero = MavF16EnginePowerModel.BuildZeroThrustLoads(100f);
            Record(
                zero.forceAeroBodyN == Vector3.zero
                && zero.momentAeroBodyNm == Vector3.zero
                && !zero.hasAuthoritativeData,
                "with no thrust deck, full power still produces exactly zero thrust, marked "
                + "non-authoritative",
                report, ref passed, ref failed);

            // The shipped F-16 trim plant still reports the honest result.
            MavTrimResult level = MavF16TrimReference.SolveStraightAndLevel(0f, 150f);
            Record(
                level.status == MavTrimStatus.ConvergedButThrustUnavailable
                && !level.converged
                && !level.propulsionDataAuthoritative,
                "the shipped F-16 powered trim is unchanged: ConvergedButThrustUnavailable ("
                + level.status + ")",
                report, ref passed, ref failed);

            Record(
                MavF16TrimReference.SolveUnpoweredGlide(0f, 150f).status == MavTrimStatus.Converged,
                "and the F-16 unpowered glide still converges",
                report, ref passed, ref failed);

            // The synthetic bench deck can never claim authority, by construction.
            MavThrustDeckResult synthetic = MavSyntheticBenchThrustDeck.EvaluateSynthetic(
                MavThrustDeckQuery.Create(0f, 0.3f, 100f),
                5000f, 60000f, 110000f, 0.25f, 15000f, 1.6f,
                MavEnvelopeExcursionPolicy.ClampToValidatedEnvelope);

            Record(
                synthetic.valid && synthetic.authority == MavThrustDataAuthority.SyntheticBench,
                "the synthetic bench deck produces usable numbers that are permanently labelled "
                + "SyntheticBench",
                report, ref passed, ref failed);

            Record(
                !MavThrustDeckBase.IsPolicyAcceptableForLiveFlight(
                    synthetic.authority, MavEnvelopeExcursionPolicy.ClampToValidatedEnvelope),
                "so a stack using it can never satisfy propulsion acceptance for live flight",
                report, ref passed, ref failed);

            Record(
                synthetic.thrustN > 0f,
                "the synthetic deck does produce thrust, which is what makes it useful for "
                + "exercising the powered path (" + synthetic.thrustN.ToString("F0") + " N)",
                report, ref passed, ref failed);

            // Thrust falls with altitude and rises with power: enough structure to test against.
            MavThrustDeckResult high = MavSyntheticBenchThrustDeck.EvaluateSynthetic(
                MavThrustDeckQuery.Create(10000f, 0.3f, 100f),
                5000f, 60000f, 110000f, 0.25f, 15000f, 1.6f,
                MavEnvelopeExcursionPolicy.ClampToValidatedEnvelope);

            Record(
                high.thrustN < synthetic.thrustN,
                "synthetic thrust falls with altitude, so altitude dependence is exercised",
                report, ref passed, ref failed);
        }

        // ================================================================= [A4]

        private static void ValidatePoweredTrimWithDeck(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[A4] Powered trim through a dimensional thrust deck");

            MavTrimPlant syntheticPlant = MavF16TrimReference.CreatePlant(
                BuildSyntheticSteadyPropulsion(5000f, 60000f, 110000f));

            MavTrimResult powered = MavSteadyFlightTrimSolver.Solve(
                syntheticPlant, MavTrimCondition.StraightAndLevel(0f, 150f));

            Record(
                powered.status == MavTrimStatus.ConvergedWithNonAuthoritativeThrust,
                "a powered trim that needs real thrust from NON-AUTHORITATIVE data is reported as "
                + "ConvergedWithNonAuthoritativeThrust, not as a plain Converged reference result ("
                + powered.status + ")",
                report, ref passed, ref failed);

            Record(
                powered.throttleDetermined
                && powered.throttle01 > 0f && powered.throttle01 < 1f,
                "the throttle is determined by inverting the deck ("
                + powered.throttle01.ToString("F4") + ")",
                report, ref passed, ref failed);

            // The solved throttle must actually produce the required thrust.
            MavTrimSteadyPropulsionFunction propulsion =
                BuildSyntheticSteadyPropulsion(5000f, 60000f, 110000f);
            MavFlightState trimState = MavSteadyFlightTrimSolver.BuildTrimFlightState(
                0f, 150f, powered.alphaDeg, powered.flightPathAngleDeg);
            MavPropulsiveLoads atSolvedThrottle = propulsion(
                trimState, MavAtmosphereModel.Sample(0f), powered.throttle01);

            Record(
                Near(atSolvedThrottle.forceAeroBodyN.x, powered.requiredThrustN,
                     Mathf.Max(10f, 0.001f * Mathf.Abs(powered.requiredThrustN))),
                "and the deck at that throttle really does produce the required thrust ("
                + atSolvedThrottle.forceAeroBodyN.x.ToString("F0") + " N vs "
                + powered.requiredThrustN.ToString("F0") + " N)",
                report, ref passed, ref failed);

            Record(
                !powered.propulsionDataAuthoritative,
                "the result carries the non-authoritative flag through to the caller",
                report, ref passed, ref failed);

            // The same architecture with data declared authoritative yields a plain Converged.
            MavTrimPlant authoritativePlant = MavF16TrimReference.CreatePlant(
                BuildSyntheticSteadyPropulsion(5000f, 60000f, 110000f));
            authoritativePlant.propulsionDataAuthoritative = true;

            MavTrimResult authoritative = MavSteadyFlightTrimSolver.Solve(
                authoritativePlant, MavTrimCondition.StraightAndLevel(0f, 150f));

            Record(
                authoritative.status == MavTrimStatus.Converged && authoritative.converged,
                "the same path with ACCEPTED data yields a true powered Converged result, so the "
                + "architecture is ready for a real deck (" + authoritative.status + ")",
                report, ref passed, ref failed);

            Record(
                Near(authoritative.throttle01, powered.throttle01, 1e-4f),
                "and the solved throttle is identical: only the provenance differs",
                report, ref passed, ref failed);

            // A condition needing no thrust is not downgraded, because it needed no thrust data.
            MavTrimResult glide = MavSteadyFlightTrimSolver.Solve(
                MavF16TrimReference.CreatePlant(),
                MavTrimCondition.UnpoweredGlide(0f, 150f));

            Record(
                glide.status == MavTrimStatus.Converged,
                "a condition requiring no thrust is not downgraded: it needed no thrust data",
                report, ref passed, ref failed);

            // Insufficient thrust remains honestly reported even with a deck attached.
            MavTrimPlant weakPlant = MavF16TrimReference.CreatePlant(
                BuildSyntheticSteadyPropulsion(10f, 50f, 100f));

            MavTrimResult weak = MavSteadyFlightTrimSolver.Solve(
                weakPlant, MavTrimCondition.StraightAndLevel(0f, 150f));

            Record(
                weak.status == MavTrimStatus.ConvergedButThrustUnavailable && !weak.converged,
                "a deck that cannot supply the required thrust still reports it honestly ("
                + weak.status + ")",
                report, ref passed, ref failed);
        }

        // ================================================================= [A5]

        private static void ValidateThrottleInversionSafety(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[A5] Throttle inversion refuses non-monotonic thrust");

            Record(
                MavThrustDeckMath.IsMonotonic(new float[] { 1f, 2f, 3f, 4f }, 1e-3f)
                && MavThrustDeckMath.IsMonotonic(new float[] { 4f, 3f, 2f, 1f }, 1e-3f)
                && MavThrustDeckMath.IsMonotonic(new float[] { 1f, 1f, 1f }, 1e-3f),
                "monotonicity accepts ascending, descending and flat sequences",
                report, ref passed, ref failed);

            Record(
                !MavThrustDeckMath.IsMonotonic(new float[] { 1f, 5f, 2f, 6f }, 1e-3f),
                "and rejects a sequence that reverses in the middle - the exact shape whose "
                + "endpoints differ while a bisection on it is meaningless",
                report, ref passed, ref failed);

            Record(
                MavThrustDeckMath.IsMonotonic(new float[] { 1f, 1.0005f, 1f }, 1e-2f),
                "small non-monotonic noise within tolerance is accepted",
                report, ref passed, ref failed);

            // A deck whose thrust rises, falls and rises again across the throttle range. Its
            // endpoints differ, so an endpoint-only check would happily proceed to bisect.
            MavTrimPlant foldedPlant = MavF16TrimReference.CreatePlant(
                delegate (MavFlightState state, MavAtmosphereSample atmosphere, float throttle01)
                {
                    float t = Mathf.Clamp01(throttle01);

                    // 0 -> 40 kN, 0.5 -> 5 kN, 1 -> 60 kN.
                    float thrust = 40000f
                                   - 140000f * t
                                   + 160000f * t * t;

                    return MavF16EnginePowerModel.BuildAxialThrustLoads(thrust, t * 100f, true);
                });
            foldedPlant.propulsionDataAuthoritative = true;

            MavTrimResult folded = MavSteadyFlightTrimSolver.Solve(
                foldedPlant, MavTrimCondition.StraightAndLevel(0f, 150f));

            Record(
                folded.status == MavTrimStatus.ThrustNotMonotonic,
                "a non-monotonic thrust curve is refused rather than bisected (" + folded.status + ")",
                report, ref passed, ref failed);

            Record(
                !folded.throttleDetermined && float.IsNaN(folded.throttle01),
                "and no throttle is invented for it",
                report, ref passed, ref failed);

            Record(
                !folded.converged,
                "such a result is not reported as converged",
                report, ref passed, ref failed);

            // A monotone deck at the same condition still resolves, so the guard is not blanket.
            MavTrimPlant monotonePlant = MavF16TrimReference.CreatePlant(
                BuildSyntheticSteadyPropulsion(5000f, 60000f, 110000f));
            monotonePlant.propulsionDataAuthoritative = true;

            MavTrimResult monotone = MavSteadyFlightTrimSolver.Solve(
                monotonePlant, MavTrimCondition.StraightAndLevel(0f, 150f));

            Record(
                monotone.status == MavTrimStatus.Converged && monotone.throttleDetermined,
                "a monotone deck at the same condition still resolves normally",
                report, ref passed, ref failed);
        }

        // ================================================================= helpers

        /// <summary>
        /// Steady propulsion built from the SYNTHETIC bench deck, applying the sourced Garza/Morelli
        /// throttle gearing first. Used only to exercise the architecture; the numbers are invented
        /// and are never presented as aircraft data.
        /// </summary>
        private static MavTrimSteadyPropulsionFunction BuildSyntheticSteadyPropulsion(
            float idleN,
            float militaryN,
            float maximumN)
        {
            return delegate (MavFlightState state, MavAtmosphereSample atmosphere, float throttle01)
            {
                float powerPercent =
                    MavF16EnginePowerModel.ThrottleToCommandedPowerPercent(throttle01);

                MavThrustDeckResult deckResult = MavSyntheticBenchThrustDeck.EvaluateSynthetic(
                    MavThrustDeckQuery.Create(state.worldPositionM.y, state.mach, powerPercent),
                    idleN, militaryN, maximumN, 0.25f, 15000f, 1.6f,
                    MavEnvelopeExcursionPolicy.ClampToValidatedEnvelope);

                if (!deckResult.valid)
                    return MavF16EnginePowerModel.BuildZeroThrustLoads(powerPercent);

                return MavF16EnginePowerModel.BuildAxialThrustLoads(
                    deckResult.thrustN,
                    powerPercent,
                    deckResult.authority == MavThrustDataAuthority.Authoritative);
            };
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
