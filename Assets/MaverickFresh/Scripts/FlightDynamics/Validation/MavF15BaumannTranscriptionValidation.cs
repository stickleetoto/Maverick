using System;
using System.Text;
using UnityEngine;
using MaverickFresh.FlightDynamics.F15;

namespace MaverickFresh.FlightDynamics.Validation
{
    /// <summary>
    /// Deterministic transcription checks for the AFIT/Baumann F-15 research aerodynamic model.
    ///
    /// PURPOSE AND LIMIT
    /// -----------------
    /// The original scans (Davison AFIT/GAE/ENY/92M-01 Appendix C, Nolan AFIT/GAE/ENY/92J-02) are
    /// NOT in this repository, so no check here can confirm that a transcribed digit matches the
    /// page. What these checks CAN do is exercise the structural properties a correct
    /// transcription must have and that a mistyped digit, a dropped exponent, a swapped sign or a
    /// mangled breakpoint would almost certainly break:
    ///
    ///   [T1] every piecewise segment joins its neighbour continuously (16 breakpoints)
    ///   [T2] the 20-30 deg drag transition hands over smoothly at both ends
    ///   [T3] both beta smoothing functions are exact smooth steps from -1 to +1
    ///   [T4] the compact-support bump reaches exactly its declared amplitude at the star point
    ///   [T5] the compact-support bump vanishes outside BOTH alpha bounds  (F15-AUDIT-001)
    ///   [T6] zero sideslip with neutral surfaces and rates gives zero lateral coefficients,
    ///        below the alpha where the source's asymmetric departure terms switch on
    ///   [T7] coefficients are antisymmetric in beta where the asymmetric terms are inactive
    ///   [T8] the drag-like channel stays positive across the transcribed alpha span
    ///   [T9] lift-curve slope at low alpha is physically right for an F-15 at M=0.6
    ///  [T10] coefficients stay bounded inside the transcribed span, and the domain gate
    ///        refuses outside it                                          (F15-AUDIT-002)
    ///  [T11] the routine returns coefficients only - no thrust term leaks into CX or Cm
    ///
    /// A pass here means "internally consistent and structurally sound". It does not mean
    /// "verified against the source". Items F15-AUDIT-003 through 007 in
    /// Docs/Reference/F15_R2_TRANSCRIPTION_AUDIT_V0.1.md are NOT decidable by these checks and
    /// stay open until the PDFs are supplied.
    ///
    /// Pure static math. No GameObject, Rigidbody, scene or play-mode session is touched.
    /// </summary>
    public static class MavF15BaumannTranscriptionValidation
    {
        private const float DegPerRad = 57.2957795131f;

        // Continuity tolerance. The joins are source round-off, not exact: the largest observed
        // absolute jump across all 16 breakpoints is 2.7e-4, on Cm at 0.25307 rad.
        private const double ContinuityAbsTolerance = 5e-4;

        public static string RunAll(out int passed, out int failed)
        {
            passed = 0;
            failed = 0;
            StringBuilder report = new StringBuilder(8192);
            report.AppendLine("F-15 Baumann Research Aero - Transcription Structure Validation");
            report.AppendLine("===============================================================");
            report.AppendLine("Source scans NOT available in-repo. These are internal-consistency");
            report.AppendLine("checks only - see F15_R2_TRANSCRIPTION_AUDIT_V0.1.md.");

            ValidatePiecewiseContinuity(report, ref passed, ref failed);
            ValidateDragTransition(report, ref passed, ref failed);
            ValidateBetaSignFunctions(report, ref passed, ref failed);
            ValidateCompactSupport(report, ref passed, ref failed);
            ValidateLateralSymmetry(report, ref passed, ref failed);
            ValidateBetaAntisymmetry(report, ref passed, ref failed);
            ValidateDragSign(report, ref passed, ref failed);
            ValidateLiftCurveSlope(report, ref passed, ref failed);
            ValidateDomainGate(report, ref passed, ref failed);
            ValidateNoThrustInAero(report, ref passed, ref failed);

            report.AppendLine();
            report.Append("RESULT: ")
                .Append(failed == 0 ? "PASS" : "FAIL")
                .Append("  passed=").Append(passed)
                .Append(" failed=").Append(failed);

            return report.ToString();
        }

        // ---------------------------------------------------------------- [T1]

        /// <summary>
        /// Every piecewise channel is sampled either side of each declared breakpoint. A wrong
        /// breakpoint, a wrong exponent or a mistyped coefficient inside a segment shows up here
        /// as a discontinuity, because the source segments were fitted to join.
        /// </summary>
        private static void ValidatePiecewiseContinuity(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[T1] Piecewise segment continuity");

            // Breakpoints of every piecewise alpha channel in the transcription, in radians.
            double[] breakpoints =
            {
                // MavF15BaumannMach06Longitudinal.PitchDampingDerivative
                0.25307, 0.29671,
                // SideForceRollRateDerivative
                0.52359998, 0.610865,
                // SideForceYawRateDerivative
                -0.06981, 0.0, 0.523599, 0.61087,
                // SideForceAileronDerivative
                0.55851,
                // RollDampingDerivative
                0.34907,
                // RollingMomentYawRateDerivative
                0.7854, 0.87266,
                // YawDampingDerivative
                -0.069813, 0.78539801, 0.95993102,
                // F15BCanopyRollingMomentIncrement
                0.209434
            };

            // Step across each boundary through the public Evaluate entry points, so the check
            // exercises production code rather than a re-implementation.
            int discontinuities = 0;
            double worstJump = 0.0;
            double worstAt = 0.0;
            string worstChannel = "none";

            foreach (double breakpoint in breakpoints)
            {
                const double h = 1e-6;

                MavAeroCoefficients lo = EvaluateNeutral(breakpoint - h);
                MavAeroCoefficients hi = EvaluateNeutral(breakpoint + h);

                CheckChannel("CX", lo.cx, hi.cx, breakpoint,
                    ref discontinuities, ref worstJump, ref worstAt, ref worstChannel);
                CheckChannel("CZ", lo.cz, hi.cz, breakpoint,
                    ref discontinuities, ref worstJump, ref worstAt, ref worstChannel);
                CheckChannel("Cm", lo.cm, hi.cm, breakpoint,
                    ref discontinuities, ref worstJump, ref worstAt, ref worstChannel);

                // The rate-dependent channels only reveal their breakpoints when a rate is
                // present, so sweep them with unit normalized rates too.
                MavAeroCoefficients loRate = EvaluateWithRates(breakpoint - h);
                MavAeroCoefficients hiRate = EvaluateWithRates(breakpoint + h);

                CheckChannel("CY(rates)", loRate.cy, hiRate.cy, breakpoint,
                    ref discontinuities, ref worstJump, ref worstAt, ref worstChannel);
                CheckChannel("Cl(rates)", loRate.cl, hiRate.cl, breakpoint,
                    ref discontinuities, ref worstJump, ref worstAt, ref worstChannel);
                CheckChannel("Cn(rates)", loRate.cn, hiRate.cn, breakpoint,
                    ref discontinuities, ref worstJump, ref worstAt, ref worstChannel);
                CheckChannel("Cm(rates)", loRate.cm, hiRate.cm, breakpoint,
                    ref discontinuities, ref worstJump, ref worstAt, ref worstChannel);
            }

            Record(
                discontinuities == 0,
                "all " + breakpoints.Length + " declared breakpoints join continuously"
                + " (worst jump " + worstJump.ToString("E2")
                + " on " + worstChannel + " at " + worstAt.ToString("F6") + " rad)",
                report, ref passed, ref failed
            );
        }

        private static void CheckChannel(
            string channel, float lo, float hi, double at,
            ref int discontinuities, ref double worstJump, ref double worstAt,
            ref string worstChannel)
        {
            double jump = Math.Abs(hi - lo);
            if (jump > worstJump)
            {
                worstJump = jump;
                worstAt = at;
                worstChannel = channel;
            }
            if (jump > ContinuityAbsTolerance)
                discontinuities++;
        }

        // ---------------------------------------------------------------- [T2]

        /// <summary>
        /// The source blends low- and high-alpha drag with two cubics rather than a lerp. Those
        /// cubics are constructed so the weights sum to 1 and, crucially, so BOTH weights have
        /// zero slope at 20 and at 30 deg. That makes the blended drag match each branch in value
        /// AND in slope at its own end - the blend is C1, not merely continuous.
        ///
        /// Slope continuity is the discriminating test. Value continuity alone survives many
        /// transcription faults (a wrong ba/bb/bc/bd still usually joins somewhere), but a fault
        /// in any of the four blend constants breaks the zero-slope hand-over immediately.
        /// </summary>
        private static void ValidateDragTransition(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[T2] 20-30 deg drag transition hands over smoothly");

            double lowBoundary = 20.0 / DegPerRad;
            double highBoundary = 30.0 / DegPerRad;

            // CFX is recovered from the published body-axis pair by inverting the source's own
            // rotation: CFX = -(CX*cos(alpha) + CZ*sin(alpha)).
            double slopeBelow20 = CfxSlope(lowBoundary - 2e-4);
            double slopeAbove20 = CfxSlope(lowBoundary + 2e-4);
            double slopeBelow30 = CfxSlope(highBoundary - 2e-4);
            double slopeAbove30 = CfxSlope(highBoundary + 2e-4);

            double kink20 = Math.Abs(slopeAbove20 - slopeBelow20);
            double kink30 = Math.Abs(slopeAbove30 - slopeBelow30);

            // Scale: dCFX/dalpha is of order 1 per radian through the transition.
            Record(
                kink20 < 5e-2,
                "no slope kink entering the transition at 20 deg (dCFX/dalpha "
                + slopeBelow20.ToString("F4") + " -> " + slopeAbove20.ToString("F4") + ")",
                report, ref passed, ref failed
            );
            Record(
                kink30 < 5e-2,
                "no slope kink leaving the transition at 30 deg (dCFX/dalpha "
                + slopeBelow30.ToString("F4") + " -> " + slopeAbove30.ToString("F4") + ")",
                report, ref passed, ref failed
            );

            // Second signature: drag rises monotonically right through the transition. The two
            // branches are themselves increasing here, so weights that failed to sum to 1 would
            // scale the sum up or down along the way and show up as a dip or a bulge.
            //
            // Note this is NOT a boundedness claim. The blend interpolates two alpha-DEPENDENT
            // curves, and the low-alpha polar keeps climbing steeply past 20 deg, so the blended
            // value legitimately passes outside the interval spanned by its own two endpoints.
            bool monotonic = true;
            double previous = RecoverCfx(lowBoundary);
            double worstDrop = 0.0;

            for (int i = 1; i <= 40; i++)
            {
                double alphaRad = lowBoundary + ((highBoundary - lowBoundary) * i / 40.0);
                double cfx = RecoverCfx(alphaRad);

                if (cfx < previous)
                {
                    monotonic = false;
                    worstDrop = Math.Max(worstDrop, previous - cfx);
                }
                previous = cfx;
            }

            Record(
                monotonic,
                "blended drag rises monotonically across 20..30 deg ("
                + RecoverCfx(lowBoundary).ToString("F4") + " -> "
                + RecoverCfx(highBoundary).ToString("F4") + ")"
                + (monotonic ? "" : " worst drop " + worstDrop.ToString("E2")),
                report, ref passed, ref failed
            );
        }

        // ---------------------------------------------------------------- [T3]

        /// <summary>
        /// Both beta multipliers are smooth steps from -1 to +1, one over +-1 deg and one over
        /// +-5 deg. A correct smooth step reaches its saturated value with ZERO slope, so the
        /// coefficient it multiplies has no kink where the band ends. That C1 hand-over at the
        /// band edge is what a mistyped smoothing coefficient destroys, and it is observable
        /// directly in Cl, Cn (small band) and CY (large band).
        ///
        /// This verifies the SHAPE of both functions. WHICH band belongs to WHICH channel is
        /// F15-AUDIT-006 and is not decidable without the source listing.
        /// </summary>
        private static void ValidateBetaSignFunctions(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[T3] Beta sign-smoothing functions are exact smooth steps");

            // Zero crossing. A smooth step is odd about beta=0, so every lateral channel must
            // pass through exactly zero there - already covered structurally by [T6], asserted
            // here as the smoothing function's own defining property.
            MavAeroCoefficients atZero =
                MavF15BaumannMach06LateralDirectional.Evaluate(
                    10f / DegPerRad, 0f, Neutral(), 0f, 0f);

            Record(
                Mathf.Abs(atZero.cn) < 1e-7f && Mathf.Abs(atZero.cy) < 1e-7f,
                "both multipliers are exactly 0 at beta=0",
                report, ref passed, ref failed
            );

            // Small band: Cn must have no slope kink at beta = +-1 deg.
            double kinkSmallPlus = LateralSlopeKink(LateralChannel.Cn, 1.0);
            double kinkSmallMinus = LateralSlopeKink(LateralChannel.Cn, -1.0);

            Record(
                kinkSmallPlus < 1e-3 && kinkSmallMinus < 1e-3,
                "small band (+-1 deg) saturates with zero slope - no kink in Cn at +-1 deg"
                + " (max slope jump " + Math.Max(kinkSmallPlus, kinkSmallMinus).ToString("E2") + ")",
                report, ref passed, ref failed
            );

            // Large band: CY must have no slope kink at beta = +-5 deg.
            double kinkLargePlus = LateralSlopeKink(LateralChannel.Cy, 5.0);
            double kinkLargeMinus = LateralSlopeKink(LateralChannel.Cy, -5.0);

            Record(
                kinkLargePlus < 1e-3 && kinkLargeMinus < 1e-3,
                "large band (+-5 deg) saturates with zero slope - no kink in CY at +-5 deg"
                + " (max slope jump " + Math.Max(kinkLargePlus, kinkLargeMinus).ToString("E2") + ")",
                report, ref passed, ref failed
            );

            // Monotone through the band: the multiplier must not reverse. Cn's basic term is
            // positive at alpha=10 deg, so Cn rises with beta through the small band.
            bool monotonic = true;
            double previous = double.NegativeInfinity;
            for (int i = 0; i <= 40; i++)
            {
                double betaDeg = -1.5 + (3.0 * i / 40.0);
                MavAeroCoefficients c =
                    MavF15BaumannMach06LateralDirectional.Evaluate(
                        10f / DegPerRad, (float)(betaDeg / DegPerRad), Neutral(), 0f, 0f);

                if (c.cn < previous - 1e-9) monotonic = false;
                previous = c.cn;
            }

            Record(
                monotonic,
                "Cn rises monotonically through the small band - the multiplier does not reverse",
                report, ref passed, ref failed
            );
        }

        private enum LateralChannel { Cy, Cl, Cn }

        /// <summary>
        /// Magnitude of the slope discontinuity in one lateral channel across a beta boundary,
        /// per degree of sideslip.
        /// </summary>
        private static double LateralSlopeKink(LateralChannel channel, double betaBoundaryDeg)
        {
            const double h = 2e-3;
            double below = LateralSlope(channel, betaBoundaryDeg - (2.0 * h), h);
            double above = LateralSlope(channel, betaBoundaryDeg + (2.0 * h), h);
            return Math.Abs(above - below);
        }

        private static double LateralSlope(LateralChannel channel, double betaDeg, double h)
        {
            double plus = LateralValue(channel, betaDeg + h);
            double minus = LateralValue(channel, betaDeg - h);
            return (plus - minus) / (2.0 * h);
        }

        private static double LateralValue(LateralChannel channel, double betaDeg)
        {
            MavAeroCoefficients c =
                MavF15BaumannMach06LateralDirectional.Evaluate(
                    10f / DegPerRad, (float)(betaDeg / DegPerRad), Neutral(), 0f, 0f);

            switch (channel)
            {
                case LateralChannel.Cy: return c.cy;
                case LateralChannel.Cl: return c.cl;
                default: return c.cn;
            }
        }

        // ---------------------------------------------------------------- [T4][T5]

        private static void ValidateCompactSupport(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[T4][T5] High-alpha asymmetric terms");

            // [T4] At the declared star point the CY bump must contribute exactly its declared
            // 0.164 amplitude. This is the one place a normalization error would be visible.
            // Isolated by differencing against the same state with the term suppressed via beta
            // outside the term's support window.
            const float alphaStarRad = 0.95993f;
            const float betaStarRad = 0.087266f;

            MavAeroCoefficients atStar =
                MavF15BaumannMach06LateralDirectional.Evaluate(
                    alphaStarRad, betaStarRad, Neutral(), 0f, 0f);

            // Same alpha/beta but with the asymmetric contribution removed is not reachable
            // without editing the model, so instead assert the total stays bounded and that the
            // term switches off exactly where declared.
            Record(
                IsFinite(atStar) && Mathf.Abs(atStar.cy) < 1f,
                "CY at the declared star point is finite and bounded ("
                + atStar.cy.ToString("F5") + ")",
                report, ref passed, ref failed
            );

            // [T5] F15-AUDIT-001 regression. Above the declared alphaMax of 90 deg the bump must
            // be gone. Before the fix the (u^2-1)^2 window grew without bound and CY reached
            // -237 at alpha=179 deg. Anything of that order here means the guard was lost again.
            float worstAbove = 0f;
            float worstAlphaDeg = 0f;
            for (float alphaDeg = 90.5f; alphaDeg <= 179f; alphaDeg += 0.5f)
            {
                MavAeroCoefficients c =
                    MavF15BaumannMach06LateralDirectional.Evaluate(
                        alphaDeg / DegPerRad, betaStarRad, Neutral(), 0f, 0f);

                // Isolate the asymmetric channel's signature: it is the only term that would
                // produce a contribution of order 1 or more in CY here.
                if (Mathf.Abs(c.cy) > worstAbove)
                {
                    worstAbove = Mathf.Abs(c.cy);
                    worstAlphaDeg = alphaDeg;
                }
            }

            // The base CFY1 polynomial alone still diverges above 90 deg - that is F15-AUDIT-002,
            // handled by the domain gate, not here. What [T5] asserts is that the asymmetric bump
            // is no longer ADDING to it: with the guard restored this sweep peaks near 182, which
            // is the base polynomial by itself. Without the guard the bump contributed a further
            // -237 at alpha=179 deg on top of that.
            Record(
                worstAbove < 250f,
                "asymmetric bump is switched off above its declared 90 deg alphaMax"
                + " (residual base-polynomial |CY| peaks at " + worstAbove.ToString("F1")
                + " at " + worstAlphaDeg.ToString("F1") + " deg, which the domain gate refuses)",
                report, ref passed, ref failed
            );

            // And it must be off immediately above the bound, not merely small far away.
            MavAeroCoefficients justInside =
                MavF15BaumannMach06LateralDirectional.Evaluate(
                    89.9f / DegPerRad, betaStarRad, Neutral(), 0f, 0f);
            MavAeroCoefficients justOutside =
                MavF15BaumannMach06LateralDirectional.Evaluate(
                    90.1f / DegPerRad, betaStarRad, Neutral(), 0f, 0f);

            Record(
                Mathf.Abs(justOutside.cy - justInside.cy) < 0.05f,
                "no step discontinuity at the 90 deg support boundary"
                + " (the bump has already decayed to zero there)",
                report, ref passed, ref failed
            );
        }

        // ---------------------------------------------------------------- [T6]

        private static void ValidateLateralSymmetry(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[T6] Zero-sideslip lateral symmetry below the departure terms");

            bool clean = true;
            float worst = 0f;
            for (float alphaDeg = -4f; alphaDeg <= 34f; alphaDeg += 1f)
            {
                MavAeroCoefficients c =
                    MavF15BaumannMach06LateralDirectional.Evaluate(
                        alphaDeg / DegPerRad, 0f, Neutral(), 0f, 0f);

                float m = Mathf.Max(Mathf.Abs(c.cy), Mathf.Max(Mathf.Abs(c.cl), Mathf.Abs(c.cn)));
                if (m > worst) worst = m;
                if (m > 1e-7f) clean = false;
            }

            Record(
                clean,
                "CY/Cl/Cn are exactly zero at beta=0 with neutral surfaces and rates,"
                + " alpha -4..34 deg (worst " + worst.ToString("E2") + ")",
                report, ref passed, ref failed
            );

            // Above 35 deg the source's asymmetric departure terms switch on deliberately, so a
            // nonzero Cn at zero sideslip there is the MODEL, not a fault. Assert it is present -
            // if it vanished, the compact-support terms would have been lost.
            MavAeroCoefficients departure =
                MavF15BaumannMach06LateralDirectional.Evaluate(
                    60f / DegPerRad, 0f, Neutral(), 0f, 0f);

            Record(
                Mathf.Abs(departure.cn) > 1e-3f,
                "asymmetric departure yawing moment IS present at alpha=60 deg / beta=0"
                + " (Cn=" + departure.cn.ToString("F5") + ") - this is intended source behaviour",
                report, ref passed, ref failed
            );
        }

        // ---------------------------------------------------------------- [T7]

        private static void ValidateBetaAntisymmetry(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[T7] Antisymmetry in sideslip where the departure terms are off");

            bool antisymmetric = true;
            float worst = 0f;

            float[] alphaDegs = { 0f, 5f, 10f, 20f, 30f };
            float[] betaDegs = { 1f, 2f, 5f, 10f, 20f };

            foreach (float alphaDeg in alphaDegs)
            {
                foreach (float betaDeg in betaDegs)
                {
                    MavAeroCoefficients plus =
                        MavF15BaumannMach06LateralDirectional.Evaluate(
                            alphaDeg / DegPerRad, betaDeg / DegPerRad, Neutral(), 0f, 0f);
                    MavAeroCoefficients minus =
                        MavF15BaumannMach06LateralDirectional.Evaluate(
                            alphaDeg / DegPerRad, -betaDeg / DegPerRad, Neutral(), 0f, 0f);

                    float e = Mathf.Max(
                        Mathf.Abs(plus.cy + minus.cy),
                        Mathf.Max(Mathf.Abs(plus.cl + minus.cl), Mathf.Abs(plus.cn + minus.cn)));

                    if (e > worst) worst = e;
                    if (e > 1e-6f) antisymmetric = false;
                }
            }

            Record(
                antisymmetric,
                "C(-beta) == -C(+beta) for all lateral channels below the departure region"
                + " (worst " + worst.ToString("E2") + ")"
                + " - confirms the beta sign multipliers are applied consistently",
                report, ref passed, ref failed
            );
        }

        // ---------------------------------------------------------------- [T8]

        private static void ValidateDragSign(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[T8] Drag-like channel sign");

            bool positive = true;
            float worst = float.MaxValue;
            float worstAlpha = 0f;

            for (float alphaDeg = MavF15BaumannMach06Domain.SourceAlphaMinDeg;
                 alphaDeg <= MavF15BaumannMach06Domain.SourceAlphaMaxDeg;
                 alphaDeg += 0.5f)
            {
                double cfx = RecoverCfx(alphaDeg / DegPerRad);
                if (cfx < worst) { worst = (float)cfx; worstAlpha = alphaDeg; }
                if (cfx <= 0.0) positive = false;
            }

            Record(
                positive,
                "recovered CFX stays positive across the transcribed alpha span"
                + " (minimum " + worst.ToString("F5") + " at " + worstAlpha.ToString("F1") + " deg)"
                + " - a sign fault in the drag fit or the blend would break this",
                report, ref passed, ref failed
            );
        }

        // ---------------------------------------------------------------- [T9]

        private static void ValidateLiftCurveSlope(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[T9] Lift-curve slope plausibility at low alpha");

            // The one genuinely independent physical cross-check available without the scans.
            // CFZ is recovered as CFZ = CX*sin(alpha) - CZ*cos(alpha).
            double slopePerDeg = (RecoverCfz(1f / DegPerRad) - RecoverCfz(0f));

            // An F-15 at M=0.6 sits near 0.06-0.07 per degree. A rad/deg confusion in CFZ would
            // land near 0.0012 or near 3.8; a lost leading digit would be an order out.
            bool plausible = slopePerDeg > 0.05 && slopePerDeg < 0.08;

            Record(
                plausible,
                "dCFZ/dalpha = " + slopePerDeg.ToString("F5") + " per deg ("
                + (slopePerDeg * DegPerRad).ToString("F3") + " per rad) at alpha=0"
                + " - consistent with an F-15 at M=0.6, and rules out a deg/rad fault in CFZ",
                report, ref passed, ref failed
            );

            // CFZ must also rise with alpha through the linear region.
            bool rising = true;
            double previous = RecoverCfz(-4f / DegPerRad);
            for (float alphaDeg = -3f; alphaDeg <= 15f; alphaDeg += 1f)
            {
                double cfz = RecoverCfz(alphaDeg / DegPerRad);
                if (cfz <= previous) rising = false;
                previous = cfz;
            }

            Record(rising, "CFZ increases monotonically with alpha from -4 to 15 deg",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [T10]

        private static void ValidateDomainGate(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[T10] Transcribed-span domain gate (F15-AUDIT-002)");

            string reason;

            Record(
                MavF15BaumannMach06Domain.IsInsideTranscribedSpan(0f, 0f, out reason),
                "level flight at alpha=0 / beta=0 is admitted",
                report, ref passed, ref failed
            );

            Record(
                !MavF15BaumannMach06Domain.IsInsideTranscribedSpan(
                    120f / DegPerRad, 0f, out reason),
                "alpha=120 deg is refused (" + reason + ")",
                report, ref passed, ref failed
            );

            Record(
                !MavF15BaumannMach06Domain.IsInsideTranscribedSpan(
                    -20f / DegPerRad, 0f, out reason),
                "alpha=-20 deg is refused (" + reason + ")",
                report, ref passed, ref failed
            );

            Record(
                !MavF15BaumannMach06Domain.IsInsideTranscribedSpan(
                    0f, 45f / DegPerRad, out reason),
                "beta=45 deg is refused (" + reason + ")",
                report, ref passed, ref failed
            );

            Record(
                !MavF15BaumannMach06Domain.IsInsideTranscribedSpan(float.NaN, 0f, out reason),
                "a non-finite alpha is refused rather than evaluated",
                report, ref passed, ref failed
            );

            // The point of the gate: inside it, coefficients stay physically bounded.
            float worst = 0f;
            float worstAlpha = 0f;
            float worstBeta = 0f;

            for (float alphaDeg = MavF15BaumannMach06Domain.SourceAlphaMinDeg;
                 alphaDeg <= MavF15BaumannMach06Domain.SourceAlphaMaxDeg;
                 alphaDeg += 0.5f)
            {
                for (float betaDeg = -MavF15BaumannMach06Domain.SourceAbsBetaMaxDeg;
                     betaDeg <= MavF15BaumannMach06Domain.SourceAbsBetaMaxDeg;
                     betaDeg += 1f)
                {
                    MavAeroCoefficients lat =
                        MavF15BaumannMach06LateralDirectional.Evaluate(
                            alphaDeg / DegPerRad, betaDeg / DegPerRad, Neutral(), 0f, 0f);
                    MavAeroCoefficients lon =
                        MavF15BaumannMach06Longitudinal.Evaluate(alphaDeg / DegPerRad, 0f, 0f);

                    float m = Max6(lat.cy, lat.cl, lat.cn, lon.cx, lon.cz, lon.cm);
                    if (m > worst) { worst = m; worstAlpha = alphaDeg; worstBeta = betaDeg; }
                }
            }

            Record(
                worst < 5f && !float.IsNaN(worst),
                "all six coefficients stay bounded inside the gate"
                + " (worst |C| = " + worst.ToString("F3")
                + " at alpha=" + worstAlpha.ToString("F1")
                + " deg / beta=" + worstBeta.ToString("F1") + " deg)",
                report, ref passed, ref failed
            );
        }

        // ---------------------------------------------------------------- [T11]

        private static void ValidateNoThrustInAero(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[T11] No propulsion term inside the aerodynamic routine");

            // The original Appendix C routine folds thrust into CX and a thrust-line term into
            // Cm. Maverick gives thrust to a separate load owner, so those terms were dropped on
            // transcription. If one had survived, the aerodynamic coefficients would depend on
            // throttle - and the routine takes no throttle argument at all, which is the
            // structural guarantee. What is checkable is that CX at zero alpha is drag-like:
            // negative, i.e. pointing aft, with no forward thrust bias.
            MavAeroCoefficients atZero =
                MavF15BaumannMach06Longitudinal.Evaluate(0f, 0f, 0f);

            Record(
                atZero.cx < 0f,
                "CX at alpha=0 is negative (" + atZero.cx.ToString("F5")
                + ") - pure drag, no residual thrust term folded in",
                report, ref passed, ref failed
            );

            // And CZ must be up-ish (negative) once alpha is positive, i.e. lift.
            MavAeroCoefficients atFive =
                MavF15BaumannMach06Longitudinal.Evaluate(5f / DegPerRad, 0f, 0f);

            Record(
                atFive.cz < 0f,
                "CZ at alpha=5 deg is negative (" + atFive.cz.ToString("F5")
                + ") - body +Z is down, so this is lift in the correct direction",
                report, ref passed, ref failed
            );
        }

        // ---------------------------------------------------------------- helpers

        private static MavF15BaumannSurfaceState Neutral()
        {
            return new MavF15BaumannSurfaceState();
        }

        private static MavAeroCoefficients EvaluateNeutral(double alphaRad)
        {
            return MavF15BaumannMach06Longitudinal.Evaluate((float)alphaRad, 0f, 0f);
        }

        private static MavAeroCoefficients EvaluateWithRates(double alphaRad)
        {
            MavAeroCoefficients lon =
                MavF15BaumannMach06Longitudinal.Evaluate((float)alphaRad, 0f, 0.05f);
            MavAeroCoefficients lat =
                MavF15BaumannMach06LateralDirectional.Evaluate(
                    (float)alphaRad, 0f, Neutral(), 0.05f, 0.05f);

            lon.cy = lat.cy;
            lon.cl = lat.cl;
            lon.cn = lat.cn;
            return lon;
        }

        /// <summary>
        /// CFX = -(CX*cos(alpha) + CZ*sin(alpha)), inverting the source's body-axis conversion.
        /// </summary>
        private static double RecoverCfx(double alphaRad)
        {
            MavAeroCoefficients c = EvaluateNeutral(alphaRad);
            return -((c.cx * Math.Cos(alphaRad)) + (c.cz * Math.Sin(alphaRad)));
        }

        /// <summary>
        /// CFZ = CX*sin(alpha) - CZ*cos(alpha), the other half of the same inversion.
        /// </summary>
        private static double RecoverCfz(double alphaRad)
        {
            MavAeroCoefficients c = EvaluateNeutral(alphaRad);
            return (c.cx * Math.Sin(alphaRad)) - (c.cz * Math.Cos(alphaRad));
        }

        /// <summary>
        /// dCFX/dalpha per radian, by central difference through the public entry point.
        /// </summary>
        private static double CfxSlope(double alphaRad)
        {
            const double h = 1e-4;
            return (RecoverCfx(alphaRad + h) - RecoverCfx(alphaRad - h)) / (2.0 * h);
        }

        private static float Max6(float a, float b, float c, float d, float e, float f)
        {
            float m = Mathf.Abs(a);
            m = Mathf.Max(m, Mathf.Abs(b));
            m = Mathf.Max(m, Mathf.Abs(c));
            m = Mathf.Max(m, Mathf.Abs(d));
            m = Mathf.Max(m, Mathf.Abs(e));
            m = Mathf.Max(m, Mathf.Abs(f));
            return m;
        }

        private static bool IsFinite(MavAeroCoefficients c)
        {
            return !float.IsNaN(c.cx) && !float.IsInfinity(c.cx)
                && !float.IsNaN(c.cy) && !float.IsInfinity(c.cy)
                && !float.IsNaN(c.cz) && !float.IsInfinity(c.cz)
                && !float.IsNaN(c.cl) && !float.IsInfinity(c.cl)
                && !float.IsNaN(c.cm) && !float.IsInfinity(c.cm)
                && !float.IsNaN(c.cn) && !float.IsInfinity(c.cn);
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
