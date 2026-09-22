using System;

namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// Transcription of the LONGITUDINAL subset of the AFIT/Baumann F-15
    /// coefficient routine at Mach 0.6 / 20,000 ft.
    ///
    /// Implemented source channels:
    /// - CFZ / lift-side stability-axis force fit
    /// - CFX low/high-AOA drag fit and the source 20..30 deg smooth transition
    /// - CMM1 basic pitching moment
    /// - CMMQ pitch-rate damping
    /// - final body-axis CX/CZ conversion
    ///
    /// Deliberately NOT implemented here:
    /// - CY, Cl, Cn lateral/directional equations
    /// - differential-tail/aileron/rudder effects
    /// - speedbrake/store increments
    /// - thrust contribution or thrust-line pitching moment
    ///
    /// The original routine adds thrust inside CX and Cm. Maverick does not:
    /// propulsion is a separate load owner, so importing those terms here would double
    /// apply thrust once the F100 model exists.
    ///
    /// This model is CROSS-VALIDATION / RESEARCH only. It is not the exact NASA 836
    /// pre-Quiet-Spike baseline aerodynamic database.
    /// </summary>
    public static class MavF15BaumannMach06Longitudinal
    {
        private const double DegPerRad = 57.2957795131;
        private const double LiftFitArtifactDivisor = 57.29578;

        public static MavAeroCoefficients Evaluate(
            float alphaRad,
            float symmetricStabilatorDeg,
            float qHat)
        {
            double ral = alphaRad;
            double dstbr = symmetricStabilatorDeg / DegPerRad;

            double cfz =
                -0.00369376
                + (3.78028702 * ral)
                + (0.6921459 * ral * ral)
                - (5.0005867 * Pow(ral, 3))
                + (1.94478199 * Pow(ral, 4))
                + (0.40781955 * dstbr)
                + (0.10114579 * dstbr * dstbr);

            // The source explicitly calls this conversion a curve-fitting artifact.
            //
            // F15-AUDIT-008 (CLOSED - SOURCE-CONFIRMED). The divisor looks like a stray
            // rad->deg conversion. It is not. Davison Appendix C, printed page 130, has the
            // statement verbatim as "CL=CFZ1/57.29578", followed by the source's own
            // explanation: the curve fit took every independent variable in radians, and for
            // CFX1 one of those variables was not an angle but a dimensionless coefficient.
            // The divisor is that artifact, and the polar coefficients below only produce sane
            // drag for an argument of this scale.
            double clArtifact = cfz / LiftFitArtifactDivisor;

            double cfxLow =
                0.01806821
                + (0.01556573 * clArtifact)
                + (498.96208868 * Pow(clArtifact, 2))
                - (14451.56518396 * Pow(clArtifact, 3))
                + (2132344.6184755 * Pow(clArtifact, 4));

            double cfxHigh =
                0.0267297
                - (0.10646919 * ral)
                + (5.39836337 * ral * ral)
                - (5.0086893 * Pow(ral, 3))
                + (1.34148193 * Pow(ral, 4))
                + (0.20978902 * dstbr)
                + (0.30604211 * dstbr * dstbr)
                // F15-AUDIT-005 (CLOSED) + F15-AUDIT-010 (CORRECTED).
                //
                // The second bare constant is real: Davison Appendix C, printed page 130, ends
                // the CFX2 statement with a trailing "+0.09833517" after the DSTBR**2 term. So
                // the duplicated-constant shape was NOT an OCR artifact.
                //
                // The digit was wrong, though: the source reads 0.0983 *5* 17, not 0.0983 *6* 17.
                // Confirmed at 12x on the page image - the glyph has the flat top bar and open
                // upper-left of a 5, where the adjacent 3s and a 6 are plainly different.
                + 0.09833517;

            double cfx = BlendLowHighAoaDrag(ral, cfxLow, cfxHigh);

            double cmm1 =
                0.00501496
                - (0.08004901 * ral)
                - (1.03486675 * ral * ral)
                - (0.68580677 * Pow(ral, 3))
                + (6.46858488 * Pow(ral, 4))
                - (10.15574108 * Pow(ral, 5))
                + (6.44350808 * Pow(ral, 6))
                - (1.46175188 * Pow(ral, 7))
                + (0.24050902 * ral * dstbr)
                - (0.42629958 * dstbr)
                - (0.03337449 * dstbr * dstbr)
                - (0.53951733 * Pow(dstbr, 3));

            double cmmq = PitchDampingDerivative(ral);
            double cmm = cmm1 + (cmmq * qHat);

            double sinAlpha = Math.Sin(ral);
            double cosAlpha = Math.Cos(ral);

            // Original source:
            //   CX = CFZ*sin(alpha) - CFX*cos(alpha) + thrust/qS
            //   CZ = -(CFZ*cos(alpha) + CFX*sin(alpha))
            //   Cm = CMM + thrust-line moment
            // Propulsion terms are intentionally omitted in Maverick.
            MavAeroCoefficients result = MavAeroCoefficients.Zero;
            result.cx = (float)((cfz * sinAlpha) - (cfx * cosAlpha));
            result.cz = (float)(-(cfz * cosAlpha + cfx * sinAlpha));
            result.cm = (float)cmm;

            // Longitudinal slice only. Lateral channels remain visibly zero rather than
            // being invented from another F-15 configuration.
            result.cy = 0f;
            result.cl = 0f;
            result.cn = 0f;
            return result;
        }

        private static double BlendLowHighAoaDrag(
            double ral,
            double cfxLow,
            double cfxHigh)
        {
            double a1 = 20.0 / DegPerRad;
            double a2 = 30.0 / DegPerRad;

            if (ral < a1)
                return cfxLow;
            if (ral > a2)
                return cfxHigh;

            // Source smooth transition polynomial, preserved instead of replacing it
            // with a generic lerp.
            double a12 = a1 + a2;
            double ba =
                2.0
                / (-Pow(a1, 3)
                   + (3.0 * a1 * a2 * (a1 - a2))
                   + Pow(a2, 3));
            double bb = -3.0 * ba * (a1 + a2) / 2.0;
            double bc = 3.0 * ba * a1 * a2;
            double bd = ba * a2 * a2 * (a2 - (3.0 * a1)) / 2.0;

            double f1 =
                (ba * Pow(ral, 3))
                + (bb * ral * ral)
                + (bc * ral)
                + bd;

            double f2 =
                (-ba * Pow(ral, 3))
                + (((3.0 * a12 * ba) + bb) * ral * ral)
                - ((bc + (2.0 * a12 * bb) + (3.0 * a12 * a12 * ba)) * ral)
                + bd
                + (a12 * bc)
                + (a12 * a12 * bb)
                + (Pow(a12, 3) * ba);

            return (cfxLow * f1) + (cfxHigh * f2);
        }

        private static double PitchDampingDerivative(double ral)
        {
            if (ral <= 0.25307)
            {
                return
                    -3.8386262
                    + (13.54661297 * ral)
                    + (402.53011559 * ral * ral)
                    - (6660.95327122 * Pow(ral, 3))
                    - (62257.89908743 * Pow(ral, 4))
                    + (261526.10242329 * Pow(ral, 5))
                    + (2177190.33155227 * Pow(ral, 6))
                    - (703575.13709062 * Pow(ral, 7))
                    - (20725000.34643054 * Pow(ral, 8))
                    - (27829700.53333649 * Pow(ral, 9));
            }

            if (ral < 0.29671)
            {
                double d = ral - 0.2530699968;
                return
                    -8.4926528931
                    - (2705.3000488281 * d)
                    + (123801.5 * d * d)
                    - (1414377.0 * Pow(d, 3));
            }

            return
                47.24676075
                - (709.60757056 * ral)
                + (3359.08807193 * ral * ral)
                - (7565.32017266 * Pow(ral, 3))
                + (8695.1858091 * Pow(ral, 4))
                - (4891.77183313 * Pow(ral, 5))
                + (1061.55915089 * Pow(ral, 6));
        }

        private static double Pow(double value, int exponent)
        {
            return Math.Pow(value, exponent);
        }
    }
}
