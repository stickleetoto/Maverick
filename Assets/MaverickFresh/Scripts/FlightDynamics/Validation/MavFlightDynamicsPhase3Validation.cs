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
            ValidateAttitudeDirections(report, ref passed, ref failed);
            ValidateLoadFactorRelations(report, ref passed, ref failed);
            ValidateBankedControlLaw(report, ref passed, ref failed);
            ValidateOwnershipInvariant(report, ref passed, ref failed);
            ValidateOwnershipTransitions(report, ref passed, ref failed);
            ValidateOwnershipFailureHandling(report, ref passed, ref failed);

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


        // ================================================================= [B0]

        /// <summary>
        /// Physical-direction tests for the attitude derivation.
        ///
        /// Deliberately NOT round-trip tests. This project has already been bitten by a handedness
        /// error that a round trip could not see, because applying the same wrong sign outbound and
        /// inbound cancels. Every check below states where a vector points and asserts the sign of
        /// the resulting angle, which a matching pair of sign errors cannot survive.
        /// </summary>
        private static void ValidateAttitudeDirections(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[B0] Attitude physical directions");

            // Wings level, nose on the horizon, facing world +Z.
            MavAttitude level = MavAttitudeMath.FromWorldBasis(
                new Vector3(0f, 0f, 1f), new Vector3(0f, 1f, 0f), new Vector3(1f, 0f, 0f),
                new Vector3(0f, 0f, 200f));

            Record(
                level.valid
                && Near(level.PitchAttitudeDeg, 0f, 1e-3f)
                && Near(level.BankAngleDeg, 0f, 1e-3f)
                && Near(level.HeadingDeg, 0f, 1e-3f)
                && Near(level.FlightPathAngleDeg, 0f, 1e-3f),
                "straight and level gives zero pitch, bank, heading and flight-path angle",
                report, ref passed, ref failed);

            // Nose 30 degrees above the horizon.
            float c30 = Mathf.Cos(30f * Mathf.Deg2Rad);
            float s30 = Mathf.Sin(30f * Mathf.Deg2Rad);
            MavAttitude noseUp = MavAttitudeMath.FromWorldBasis(
                new Vector3(0f, s30, c30), new Vector3(0f, c30, -s30), new Vector3(1f, 0f, 0f),
                new Vector3(0f, 0f, 200f));

            Record(
                Near(noseUp.PitchAttitudeDeg, 30f, 1e-2f) && noseUp.PitchAttitudeDeg > 0f,
                "nose ABOVE the horizon gives POSITIVE pitch attitude ("
                + noseUp.PitchAttitudeDeg.ToString("F2") + " deg)",
                report, ref passed, ref failed);

            MavAttitude noseDown = MavAttitudeMath.FromWorldBasis(
                new Vector3(0f, -s30, c30), new Vector3(0f, c30, s30), new Vector3(1f, 0f, 0f),
                new Vector3(0f, 0f, 200f));

            Record(
                Near(noseDown.PitchAttitudeDeg, -30f, 1e-2f),
                "nose BELOW the horizon gives NEGATIVE pitch attitude ("
                + noseDown.PitchAttitudeDeg.ToString("F2") + " deg)",
                report, ref passed, ref failed);

            // Right wing dropped 45 degrees.
            float c45 = Mathf.Cos(45f * Mathf.Deg2Rad);
            MavAttitude rightBank = MavAttitudeMath.FromWorldBasis(
                new Vector3(0f, 0f, 1f), new Vector3(c45, c45, 0f), new Vector3(c45, -c45, 0f),
                new Vector3(0f, 0f, 200f));

            Record(
                Near(rightBank.BankAngleDeg, 45f, 1e-2f) && rightBank.BankAngleDeg > 0f,
                "RIGHT wing down gives POSITIVE bank angle ("
                + rightBank.BankAngleDeg.ToString("F2") + " deg)",
                report, ref passed, ref failed);

            MavAttitude leftBank = MavAttitudeMath.FromWorldBasis(
                new Vector3(0f, 0f, 1f), new Vector3(-c45, c45, 0f), new Vector3(c45, c45, 0f),
                new Vector3(0f, 0f, 200f));

            Record(
                Near(leftBank.BankAngleDeg, -45f, 1e-2f) && leftBank.BankAngleDeg < 0f,
                "LEFT wing down gives NEGATIVE bank angle ("
                + leftBank.BankAngleDeg.ToString("F2") + " deg)",
                report, ref passed, ref failed);

            Record(
                Near(rightBank.PitchAttitudeDeg, 0f, 1e-2f)
                && Near(leftBank.PitchAttitudeDeg, 0f, 1e-2f),
                "a pure bank does not contaminate the pitch attitude",
                report, ref passed, ref failed);

            // Heading: nose toward world +X is a right turn from the +Z reference.
            MavAttitude east = MavAttitudeMath.FromWorldBasis(
                new Vector3(1f, 0f, 0f), new Vector3(0f, 1f, 0f), new Vector3(0f, 0f, -1f),
                new Vector3(200f, 0f, 0f));

            Record(
                Near(east.HeadingDeg, 90f, 1e-2f) && east.HeadingDeg > 0f,
                "turning the nose to the RIGHT increases heading ("
                + east.HeadingDeg.ToString("F2") + " deg)",
                report, ref passed, ref failed);

            MavAttitude west = MavAttitudeMath.FromWorldBasis(
                new Vector3(-1f, 0f, 0f), new Vector3(0f, 1f, 0f), new Vector3(0f, 0f, 1f),
                new Vector3(-200f, 0f, 0f));

            Record(
                Near(west.HeadingDeg, -90f, 1e-2f),
                "and to the LEFT decreases it (" + west.HeadingDeg.ToString("F2") + " deg)",
                report, ref passed, ref failed);

            // Flight path follows the VELOCITY vector, independently of where the nose points.
            MavAttitude climbing = MavAttitudeMath.FromWorldBasis(
                new Vector3(0f, 0f, 1f), new Vector3(0f, 1f, 0f), new Vector3(1f, 0f, 0f),
                new Vector3(0f, 100f, 100f));

            Record(
                climbing.flightPathValid && Near(climbing.FlightPathAngleDeg, 45f, 1e-2f),
                "CLIMBING velocity gives a POSITIVE flight-path angle ("
                + climbing.FlightPathAngleDeg.ToString("F2") + " deg)",
                report, ref passed, ref failed);

            MavAttitude descending = MavAttitudeMath.FromWorldBasis(
                new Vector3(0f, 0f, 1f), new Vector3(0f, 1f, 0f), new Vector3(1f, 0f, 0f),
                new Vector3(0f, -100f, 100f));

            Record(
                Near(descending.FlightPathAngleDeg, -45f, 1e-2f),
                "DESCENDING velocity gives a NEGATIVE one ("
                + descending.FlightPathAngleDeg.ToString("F2") + " deg)",
                report, ref passed, ref failed);

            Record(
                Near(climbing.PitchAttitudeDeg, 0f, 1e-2f),
                "flight-path angle is taken from the velocity vector, not from the nose: a level "
                + "nose with a climbing velocity keeps pitch attitude at zero",
                report, ref passed, ref failed);

            // Degenerate and ill-conditioned cases fail closed rather than returning nonsense.
            MavAttitude degenerate = MavAttitudeMath.FromWorldBasis(
                Vector3.zero, new Vector3(0f, 1f, 0f), new Vector3(1f, 0f, 0f),
                new Vector3(0f, 0f, 200f));

            Record(!degenerate.valid,
                "a degenerate orientation basis yields an INVALID attitude, not a zero one",
                report, ref passed, ref failed);

            MavAttitude vertical = MavAttitudeMath.FromWorldBasis(
                new Vector3(0f, 1f, 0f), new Vector3(0f, 0f, -1f), new Vector3(1f, 0f, 0f),
                new Vector3(0f, 200f, 0f));

            Record(
                vertical.valid && vertical.nearVerticalSingularity,
                "vertical flight is flagged as near-singular, where bank and heading degrade",
                report, ref passed, ref failed);

            MavAttitude stationary = MavAttitudeMath.FromWorldBasis(
                new Vector3(0f, 0f, 1f), new Vector3(0f, 1f, 0f), new Vector3(1f, 0f, 0f),
                Vector3.zero);

            Record(
                stationary.valid && !stationary.flightPathValid,
                "a stationary aircraft has a valid attitude but no meaningful flight-path angle",
                report, ref passed, ref failed);

            // SafeCosBank degrades to the wings-level value rather than to a meaningless number.
            Record(
                Near(MavAttitudeMath.SafeCosBank(MavAttitude.Invalid), 1f, 1e-6f)
                && Near(MavAttitudeMath.SafeCosBank(vertical), 1f, 1e-6f)
                && Near(MavAttitudeMath.SafeCosBank(rightBank), c45, 1e-3f),
                "SafeCosBank returns the true cosine when usable and 1 when not",
                report, ref passed, ref failed);
        }

        // ================================================================= [B1]

        private static void ValidateLoadFactorRelations(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[B1] Bank-aware load-factor relations");

            const float speed = 200f;
            float ratePerG = StandardGravity / speed;

            Record(
                Near(MavAttitudeMath.PitchRateForLoadFactor(1f, 1f, speed, 30f), 0f, 1e-6f),
                "wings level at 1 g requires no pitch rate",
                report, ref passed, ref failed);

            Record(
                Near(MavAttitudeMath.PitchRateForLoadFactor(2f, 1f, speed, 30f), ratePerG, 1e-6f),
                "wings level at 2 g reduces EXACTLY to the Phase 2 relation g*(n-1)/V, so "
                + "wings-level behaviour is unchanged",
                report, ref passed, ref failed);

            // A level 60 degree turn: n = 1/cos(60) = 2, and q = (g/V)(n - cos phi).
            float cos60 = Mathf.Cos(60f * Mathf.Deg2Rad);
            Record(
                Near(MavAttitudeMath.PitchRateForLoadFactor(2f, cos60, speed, 30f),
                     ratePerG * (2f - cos60), 1e-6f),
                "a 60 degree level turn needs q = (g/V)(n - cos phi), which is 1.5 g/V not 1.0 g/V",
                report, ref passed, ref failed);

            Record(
                MavAttitudeMath.PitchRateForLoadFactor(2f, cos60, speed, 30f)
                > MavAttitudeMath.PitchRateForLoadFactor(2f, 1f, speed, 30f),
                "which is MORE pitch rate than the wings-level relation would have commanded: the "
                + "Phase 2 approximation under-commanded in every turn",
                report, ref passed, ref failed);

            // Level-turn load factor n = 1/cos(phi).
            bool applied;
            Record(
                Near(MavAttitudeMath.LevelTurnLoadFactor(1f, 0.2588f, 4f, out applied), 1f, 1e-4f),
                "holding altitude wings level needs 1 g",
                report, ref passed, ref failed);

            Record(
                Near(MavAttitudeMath.LevelTurnLoadFactor(cos60, 0.2588f, 4f, out applied), 2f, 1e-3f)
                && applied,
                "holding altitude in a 60 degree bank needs 2 g",
                report, ref passed, ref failed);

            float cos80 = Mathf.Cos(80f * Mathf.Deg2Rad);
            Record(
                Near(MavAttitudeMath.LevelTurnLoadFactor(cos80, 0.2588f, 4f, out applied), 1f, 1e-4f)
                && !applied,
                "past the knife-edge threshold compensation is withdrawn rather than demanding an "
                + "enormous pull, and says so",
                report, ref passed, ref failed);

            float cos70 = Mathf.Cos(70f * Mathf.Deg2Rad);
            Record(
                MavAttitudeMath.LevelTurnLoadFactor(cos70, 0.2588f, 2.5f, out applied) <= 2.5f + 1e-4f,
                "and is bounded by the configured maximum",
                report, ref passed, ref failed);

            Record(
                Near(MavAttitudeMath.LevelTurnLoadFactor(-0.5f, 0.2588f, 4f, out applied), 1f, 1e-4f)
                && !applied,
                "inverted flight does not produce a negative or runaway compensation demand",
                report, ref passed, ref failed);
        }

        // ================================================================= [B2]

        private static void ValidateBankedControlLaw(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[B2] Control law in banked flight");

            const float speed = 200f;

            // Wings level, trimmed: the Phase 2 result must be preserved exactly.
            MavF16ControlLawDebug levelDebug;
            MavControlInput levelOut = RunLaw(
                MavPilotCommand.Neutral, BuildState(speed, 0f, 1f, true), out levelDebug);

            Record(
                Near(levelOut.elevatorDeg, 0f, 1e-3f)
                && Near(levelDebug.commandedLoadFactorG, 1f, 1e-3f),
                "wings level with neutral stick still commands 1 g and no elevator: the Phase 2 "
                + "behaviour is preserved exactly",
                report, ref passed, ref failed);

            // Moderate bank, neutral stick: the law should now hold altitude by itself.
            MavF16ControlLawDebug bank45;
            MavControlInput out45 = RunLaw(
                MavPilotCommand.Neutral, BuildState(speed, 45f, 1f, true), out bank45);

            Record(
                Near(bank45.commandedLoadFactorG, 1f / Mathf.Cos(45f * Mathf.Deg2Rad), 1e-2f)
                && bank45.turnCompensationApplied,
                "a 45 degree bank with neutral stick commands the altitude-holding load factor ("
                + bank45.commandedLoadFactorG.ToString("F3") + " g)",
                report, ref passed, ref failed);

            Record(
                bank45.limitedPitchRateCommandRadSec > 0f && out45.elevatorDeg < 0f,
                "which becomes a positive pitch-rate demand and nose-up elevator, rather than the "
                + "zero command the Phase 2 law would have produced",
                report, ref passed, ref failed);

            // Steeper bank demands more.
            MavF16ControlLawDebug bank60;
            RunLaw(MavPilotCommand.Neutral, BuildState(speed, 60f, 1f, true), out bank60);

            Record(
                bank60.commandedLoadFactorG > bank45.commandedLoadFactorG
                && Near(bank60.commandedLoadFactorG, 2f, 1e-2f),
                "a 60 degree bank demands 2 g, more than 45 degrees does ("
                + bank60.commandedLoadFactorG.ToString("F3") + " g)",
                report, ref passed, ref failed);

            Record(
                bank60.limitedPitchRateCommandRadSec > bank45.limitedPitchRateCommandRadSec,
                "and correspondingly more pitch rate",
                report, ref passed, ref failed);

            // Very steep bank: compensation withdrawn rather than demanding an enormous pull.
            MavF16ControlLawDebug bank85;
            RunLaw(MavPilotCommand.Neutral, BuildState(speed, 85f, 1f, true), out bank85);

            Record(
                !bank85.turnCompensationApplied && Near(bank85.commandedLoadFactorG, 1f, 1e-2f),
                "an 85 degree bank withdraws turn compensation instead of commanding a huge pull, "
                + "and reports that it did",
                report, ref passed, ref failed);

            // Positive-g turn: aft stick in a bank commands more than neutral does.
            MavF16ControlLawDebug pullingInBank;
            MavControlInput pullOut = RunLaw(
                new MavPilotCommand { pitch = 0.5f }, BuildState(speed, 45f, 1f, true),
                out pullingInBank);

            Record(
                pullingInBank.commandedLoadFactorG > bank45.commandedLoadFactorG,
                "aft stick in a bank commands MORE than the altitude-holding value ("
                + pullingInBank.commandedLoadFactorG.ToString("F3") + " g)",
                report, ref passed, ref failed);

            Record(
                pullOut.elevatorDeg < out45.elevatorDeg,
                "and produces more nose-up elevator, so command direction is right in a turn too",
                report, ref passed, ref failed);

            // Unloading in a bank must still push.
            MavF16ControlLawDebug unloading;
            MavControlInput unloadOut = RunLaw(
                new MavPilotCommand { pitch = -1f }, BuildState(speed, 45f, 1f, true), out unloading);

            Record(
                unloading.commandedLoadFactorG < 1f && unloadOut.elevatorDeg > 0f,
                "full forward stick in a bank commands an unloaded condition and nose-down elevator",
                report, ref passed, ref failed);

            // The g limiter ceiling is bank-aware. Compared against the same law at wings level
            // rather than against the open-loop term alone, because the reported ceiling is the
            // smooth minimum of the open-loop bound and the measured-margin bound, and the latter
            // is correctly bank-independent: dq/dn = g/V whatever the bank angle.
            MavF16ControlLawDebug limitedInBank;
            RunLaw(new MavPilotCommand { pitch = 1f }, BuildState(speed, 60f, 1f, true),
                out limitedInBank);

            MavF16ControlLawDebug limitedLevel;
            RunLaw(new MavPilotCommand { pitch = 1f }, BuildState(speed, 0f, 1f, true),
                out limitedLevel);

            Record(
                limitedInBank.loadFactorPitchRateCeilingRadSec
                > limitedLevel.loadFactorPitchRateCeilingRadSec + 1e-4f,
                "the g-limiter pitch-rate ceiling is higher in a bank than at wings level, so a "
                + "turn is not limited as though it were level flight ("
                + limitedInBank.loadFactorPitchRateCeilingRadSec.ToString("F4") + " vs "
                + limitedLevel.loadFactorPitchRateCeilingRadSec.ToString("F4") + " rad/s)",
                report, ref passed, ref failed);

            Record(
                Near(MavControlLawProtections.MeasuredLoadFactorPitchRateCeilingRadSec(9f, 1f, 1f),
                     8f, 1e-4f),
                "the measured-margin ceiling converts remaining g margin into remaining rate "
                + "authority, which is correctly independent of bank",
                report, ref passed, ref failed);

            Record(
                Near(limitedInBank.commandedLoadFactorG, 9f, 1e-2f),
                "full aft stick still commands the configured maximum load factor in a bank",
                report, ref passed, ref failed);

            // Losing the attitude reference degrades to wings-level behaviour, not to nonsense.
            MavF16ControlLawDebug noAttitude;
            MavControlInput noAttitudeOut = RunLaw(
                MavPilotCommand.Neutral, BuildState(speed, 45f, 1f, false), out noAttitude);

            Record(
                !noAttitude.attitudeValid
                && Near(noAttitude.cosBank, 1f, 1e-6f)
                && Near(noAttitudeOut.elevatorDeg, 0f, 1e-3f),
                "without a valid attitude the law falls back to the wings-level relation rather "
                + "than acting on a meaningless bank angle",
                report, ref passed, ref failed);

            // Turn compensation can be switched off, and the difference is observable.
            MavF16ControlLawGains noCompensation = MavF16ControlLawGains.Default;
            noCompensation.turnCompensationEnabled = false;

            MavF16ControlLawState lawState = MavF16ControlLawState.Zero;
            MavF16ControlLawDebug uncompensated;
            MavF16ControlLawV01.Compute(
                MavPilotCommand.Neutral, BuildState(speed, 45f, 1f, true),
                BuildF16SurfaceLimits(), noCompensation,
                MavAngleOfAttackLimiterSettings.Default, MavLoadFactorLimiterSettings.Default,
                MavRollRateLimiterSettings.Default, ref lawState, 0.02f, out uncompensated);

            Record(
                Near(uncompensated.commandedLoadFactorG, 1f, 1e-3f)
                && !uncompensated.turnCompensationApplied,
                "disabling turn compensation demonstrably restores the flat 1 g demand",
                report, ref passed, ref failed);
        }


        // ================================================================= [C0]

        /// <summary>
        /// The exclusive-ownership invariant, which is the whole reason the ownership controller
        /// exists: two systems applying the same physical effect to one Rigidbody is never
        /// acceptable, not for a frame and not "briefly" during a handover.
        /// </summary>
        private static void ValidateOwnershipInvariant(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[C0] Exclusive physics ownership invariant");

            Record(
                MavPhysicsOwnershipRules.ViolatesExclusiveOwnership(true, true),
                "legacy active AND new FDM armed is recognised as a violation",
                report, ref passed, ref failed);

            Record(
                !MavPhysicsOwnershipRules.ViolatesExclusiveOwnership(true, false)
                && !MavPhysicsOwnershipRules.ViolatesExclusiveOwnership(false, true)
                && !MavPhysicsOwnershipRules.ViolatesExclusiveOwnership(false, false),
                "exactly one owner, or a momentary neither, is not a violation",
                report, ref passed, ref failed);

            // State consistency: the invariant outranks whatever the controller believes.
            string reason;
            MavOwnershipObservation both = BuildObservation(true, true, true, true, true);

            Record(
                !MavPhysicsOwnershipRules.IsStateConsistent(
                    MavPhysicsOwnershipState.NewOwned, both, out reason)
                && reason.Contains("EXCLUSIVE OWNERSHIP VIOLATED"),
                "a double-ownership observation is inconsistent with ANY believed state ("
                + reason + ")",
                report, ref passed, ref failed);

            Record(
                !MavPhysicsOwnershipRules.IsStateConsistent(
                    MavPhysicsOwnershipState.LegacyOwned,
                    BuildObservation(true, true, true, false, true), out reason),
                "believing legacy owns physics while the new FDM is armed is inconsistent",
                report, ref passed, ref failed);

            Record(
                !MavPhysicsOwnershipRules.IsStateConsistent(
                    MavPhysicsOwnershipState.NewOwned,
                    BuildObservation(true, true, true, false, false), out reason),
                "believing the new FDM owns physics while it is disarmed is inconsistent",
                report, ref passed, ref failed);

            Record(
                MavPhysicsOwnershipRules.IsStateConsistent(
                    MavPhysicsOwnershipState.LegacyOwned,
                    BuildObservation(true, true, false, true, false), out reason)
                && MavPhysicsOwnershipRules.IsStateConsistent(
                    MavPhysicsOwnershipState.NewOwned,
                    BuildObservation(true, true, true, false, true), out reason),
                "the two settled configurations are consistent",
                report, ref passed, ref failed);
        }

        // ================================================================= [C1]

        private static void ValidateOwnershipTransitions(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[C1] Ownership handover preconditions and completion");

            string reason;

            // Legacy is still active at this point, and that must NOT block the handover starting -
            // it is precisely what the handover is about to release.
            MavOwnershipObservation readyToHandOver = BuildObservation(true, true, false, true, false);

            Record(
                MavPhysicsOwnershipRules.CanBeginTransitionToNew(readyToHandOver, out reason),
                "a fully prepared stack may begin the handover even though legacy still owns "
                + "physics: that criterion cannot hold until after the release",
                report, ref passed, ref failed);

            Record(
                !MavPhysicsOwnershipRules.CanBeginTransitionToNew(
                    BuildObservation(false, true, false, true, false), out reason),
                "an unprepared stack may not begin a handover (" + reason + ")",
                report, ref passed, ref failed);

            Record(
                !MavPhysicsOwnershipRules.CanBeginTransitionToNew(
                    BuildObservation(true, false, false, true, false), out reason),
                "failing any other operational precondition blocks the handover, so legacy physics "
                + "is never disabled on an aircraft unfit to take over (" + reason + ")",
                report, ref passed, ref failed);

            Record(
                !MavPhysicsOwnershipRules.CanBeginTransitionToNew(
                    BuildObservation(true, true, false, true, true), out reason),
                "a handover is refused if the new FDM is somehow already armed while legacy owns "
                + "physics (" + reason + ")",
                report, ref passed, ref failed);

            // Completion must be confirmed, not assumed.
            Record(
                MavPhysicsOwnershipRules.IsTransitionToNewComplete(
                    BuildObservation(true, true, true, false, true), out reason),
                "a handover completes when legacy is released, readiness holds and the new FDM is armed",
                report, ref passed, ref failed);

            Record(
                !MavPhysicsOwnershipRules.IsTransitionToNewComplete(
                    BuildObservation(true, true, true, true, true), out reason),
                "it does NOT complete while a legacy owner is still active (" + reason + ")",
                report, ref passed, ref failed);

            Record(
                !MavPhysicsOwnershipRules.IsTransitionToNewComplete(
                    BuildObservation(true, true, false, false, true), out reason),
                "nor when the stack is not operationally live-ready after the release ("
                + reason + ")",
                report, ref passed, ref failed);

            Record(
                !MavPhysicsOwnershipRules.IsTransitionToNewComplete(
                    BuildObservation(true, true, true, false, false), out reason),
                "nor when the new FDM failed to arm (" + reason + ")",
                report, ref passed, ref failed);

            // Readiness evaluated as it WOULD be once legacy is released. Without this the
            // handover would deadlock: full readiness can never hold while legacy still owns.
            MavFlightDynamicsReadinessInputs stillLegacy = MavFlightDynamicsReadinessInputs.FullyReady;
            stillLegacy.legacyPhysicsOwnershipClear = false;

            Record(
                !MavFlightDynamicsReadiness.Evaluate(stillLegacy).operationallyLiveReady
                && MavFlightDynamicsReadiness
                    .EvaluateAssumingLegacyOwnershipCleared(stillLegacy).operationallyLiveReady,
                "readiness-assuming-release resolves the chicken-and-egg: full readiness fails "
                + "while legacy owns, but every other condition already holds",
                report, ref passed, ref failed);

            Record(
                !MavFlightDynamicsReadiness
                    .EvaluateAssumingLegacyOwnershipCleared(
                        WithoutCommandSource(stillLegacy)).operationallyLiveReady,
                "and it relaxes ONLY the legacy criterion: any other failure still blocks",
                report, ref passed, ref failed);
        }

        // ================================================================= [C2]

        private static void ValidateOwnershipFailureHandling(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[C2] Ownership failure detection and hand-back");

            string reason;

            Record(
                !MavPhysicsOwnershipRules.ShouldReturnToLegacy(
                    BuildObservation(true, true, true, false, true), out reason),
                "a healthy new-owned aircraft is left alone",
                report, ref passed, ref failed);

            Record(
                MavPhysicsOwnershipRules.ShouldReturnToLegacy(
                    BuildObservation(true, true, true, true, true), out reason)
                && reason.Contains("re-enabled"),
                "a legacy owner re-enabled behind the controller forces a hand-back ("
                + reason + ")",
                report, ref passed, ref failed);

            Record(
                MavPhysicsOwnershipRules.ShouldReturnToLegacy(
                    BuildObservation(true, true, false, false, true), out reason),
                "losing operational live-readiness forces a hand-back (" + reason + ")",
                report, ref passed, ref failed);

            Record(
                MavPhysicsOwnershipRules.ShouldReturnToLegacy(
                    BuildObservation(false, false, false, false, true), out reason),
                "losing structural preparation forces a hand-back (" + reason + ")",
                report, ref passed, ref failed);

            // Each individual cause of a readiness loss must reach the hand-back decision.
            Record(
                CausesHandBack(WithoutCommandSource(MavFlightDynamicsReadinessInputs.FullyReady)),
                "an operational input disappearing hands ownership back",
                report, ref passed, ref failed);

            MavFlightDynamicsReadinessInputs badPropulsion = MavFlightDynamicsReadinessInputs.FullyReady;
            badPropulsion.propulsionAccepted = false;
            Record(
                CausesHandBack(badPropulsion),
                "propulsion becoming unacceptable hands ownership back",
                report, ref passed, ref failed);

            MavFlightDynamicsReadinessInputs twoLaws = MavFlightDynamicsReadinessInputs.FullyReady;
            twoLaws.singleControlLawEnabled = false;
            Record(
                CausesHandBack(twoLaws),
                "a second control law being enabled hands ownership back",
                report, ref passed, ref failed);

            MavFlightDynamicsReadinessInputs mismatched = MavFlightDynamicsReadinessInputs.FullyReady;
            mismatched.controlLawBoundToThisBody = false;
            Record(
                CausesHandBack(mismatched),
                "a mismatched pipeline reference hands ownership back",
                report, ref passed, ref failed);

            // The multiple-control-law criterion, checked directly.
            Record(
                !MavFlightDynamicsReadiness.Evaluate(twoLaws).operationallyLiveReady,
                "two enabled control laws block live-readiness: both run at order -300 and the "
                + "surface command would depend on component order",
                report, ref passed, ref failed);

            // Fault is a terminal, fail-closed state rather than something time heals.
            Record(
                MavPhysicsOwnershipState.Fault != MavPhysicsOwnershipState.LegacyOwned
                && MavPhysicsOwnershipState.Fault != MavPhysicsOwnershipState.NewOwned,
                "Fault is a distinct state, not a flavour of either ownership",
                report, ref passed, ref failed);

            // The controller must never become a physics engine of its own. The ownership source
            // is included in the same scan that guards the rest of the flight-dynamics tree.
            Record(
                !MavFlightDynamicsOwnershipScan.IsOwnershipViolation(
                    "                behaviour.enabled = false;"),
                "disabling a component is not a Rigidbody write: the ownership controller changes "
                + "WHO owns physics, it does not apply physics",
                report, ref passed, ref failed);
        }

        private static bool CausesHandBack(MavFlightDynamicsReadinessInputs inputs)
        {
            MavFlightDynamicsReadinessReport readiness = MavFlightDynamicsReadiness.Evaluate(inputs);

            MavOwnershipObservation observation = BuildObservation(
                readiness.structurallyPrepared,
                true,
                readiness.operationallyLiveReady,
                false,
                true);

            string reason;
            return MavPhysicsOwnershipRules.ShouldReturnToLegacy(observation, out reason);
        }

        private static MavFlightDynamicsReadinessInputs WithoutCommandSource(
            MavFlightDynamicsReadinessInputs inputs)
        {
            inputs.hasValidCommandSource = false;
            return inputs;
        }

        private static MavOwnershipObservation BuildObservation(
            bool structurallyPrepared,
            bool readyExceptLegacyOwnership,
            bool operationallyLiveReady,
            bool legacyOwnerActive,
            bool newFdmArmed)
        {
            MavOwnershipObservation observation = new MavOwnershipObservation();
            observation.structurallyPrepared = structurallyPrepared;
            observation.readyExceptLegacyOwnership = readyExceptLegacyOwnership;
            observation.operationallyLiveReady = operationallyLiveReady;
            observation.legacyOwnerActive = legacyOwnerActive;
            observation.newFdmArmed = newFdmArmed;
            return observation;
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

        /// <summary>
        /// Flight state at a given bank angle, with a measured load factor and an optionally
        /// unavailable attitude reference. Attitude is built through the production
        /// <see cref="MavAttitudeMath"/> path from real basis vectors, so these tests exercise the
        /// derivation rather than hand-setting the angles.
        /// </summary>
        private static MavFlightState BuildState(
            float trueAirspeedMps,
            float bankAngleDeg,
            float loadFactorNz,
            bool attitudeAvailable)
        {
            MavAtmosphereSample atmosphere = MavAtmosphereModel.Sample(0f);

            MavFlightState state = new MavFlightState();
            state.trueAirspeedMps = trueAirspeedMps;
            state.mach = trueAirspeedMps / atmosphere.speedOfSoundMps;
            state.dynamicPressurePa = 0.5f * atmosphere.densityKgM3 * trueAirspeedMps * trueAirspeedMps;
            state.aeroBodyVelocityMps = new Vector3(trueAirspeedMps, 0f, 0f);
            state.aeroBodyRatesRadSec = Vector3.zero;
            state.specificForceAeroBodyG = new Vector3(0f, 0f, -loadFactorNz);
            state.specificForceValid = true;

            if (attitudeAvailable)
            {
                float phi = bankAngleDeg * Mathf.Deg2Rad;
                float cos = Mathf.Cos(phi);
                float sin = Mathf.Sin(phi);

                // Rolled about the world +Z axis: the right wing drops by sin(phi).
                state.attitude = MavAttitudeMath.FromWorldBasis(
                    new Vector3(0f, 0f, 1f),
                    new Vector3(sin, cos, 0f),
                    new Vector3(cos, -sin, 0f),
                    new Vector3(0f, 0f, trueAirspeedMps));
            }
            else
            {
                state.attitude = MavAttitude.Invalid;
            }

            return state;
        }

        private static MavControlInput RunLaw(
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
                out debug);
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
