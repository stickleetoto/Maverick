using UnityEngine;

namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// Alpha/beta admissibility gate for the AFIT/Baumann research aerodynamic transcription.
    ///
    /// WHAT THIS IS
    /// ------------
    /// The span of the alpha and beta BREAKPOINTS that the transcribed routine itself declares.
    /// Every bound below is read off the transcription, and the method that declares it is named.
    ///
    /// WHAT THIS IS NOT
    /// ----------------
    /// It is NOT an aerodynamic validity envelope for the F-15, and it is not a NASA 836 claim.
    /// Neither Davison nor Nolan publishes an alpha/beta validity statement for the coefficient
    /// routine, and Davison explicitly warns against assuming the M=0.6 fit transfers elsewhere.
    /// No such statement is invented here. This gate only answers a narrower, checkable question:
    /// "is the routine still inside the region its own piecewise structure was written for?"
    ///
    /// WHY IT EXISTS (F15-AUDIT-002)
    /// -----------------------------
    /// The model already fails closed off-Mach and off-altitude, but nothing bounded alpha or
    /// beta. The base fits are 6th- to 9th-order polynomials, so outside the fitted region they
    /// do not degrade gracefully - they diverge. Measured with the rest of the model at source
    /// condition, surfaces and rates neutral:
    ///
    ///     inside  this span : max |coefficient| = 2.27   (alpha 50.7 deg, beta -20 deg)
    ///     alpha = 120 deg   : Cm = -10.4,  CY =  -4.4
    ///     alpha = 150 deg   : Cm = -122,   CY =  -40
    ///     alpha = 180 deg   : Cm = -730,   CY = -190
    ///
    /// MavF15AeroModel's finiteness check does not catch any of that: -730 is a perfectly finite
    /// float. Dimensionalized at the source condition it is a wholly fictitious pitching moment
    /// presented as research data. Refusing is the same fail-closed discipline the Mach and
    /// altitude gates already apply, and it invents nothing.
    /// </summary>
    public static class MavF15BaumannMach06Domain
    {
        /// <summary>
        /// Lowest alpha breakpoint anywhere in the transcription: the constant extensions in
        /// SideForceYawRateDerivative (-0.06981) and YawDampingDerivative (-0.069813). Below it
        /// every remaining channel - CFY1, CML1, CMN1, CMMQ, CLP, CNP - is an unextended
        /// polynomial running backwards out of its fit. Cm is 0.006 at -4 deg, 0.90 at -30 deg
        /// and 7.04 at -45 deg.
        /// </summary>
        public const float SourceAlphaMinDeg = -4.0f;

        /// <summary>
        /// Highest alpha breakpoint anywhere in the transcription: the compact-support alphaMax
        /// shared by both high-alpha asymmetric terms. Nothing in the routine declares behaviour
        /// above it.
        /// </summary>
        public const float SourceAlphaMaxDeg = 90.0f;

        /// <summary>
        /// Largest beta MAGNITUDE appearing in any transcribed breakpoint: the +0.34906 rad
        /// (20 deg) upper support bound of HighAlphaAsymmetricYawingMoment. The routine's beta
        /// breakpoints are not symmetric (the asymmetric-departure terms are deliberately
        /// one-sided), so the magnitude hull is used rather than reproducing a lopsided window
        /// that would make the gate itself asymmetric for a symmetric aircraft.
        /// </summary>
        public const float SourceAbsBetaMaxDeg = 20.0f;

        /// <summary>
        /// True when alpha and beta are both inside the transcribed breakpoint span.
        /// <paramref name="reason"/> always describes the outcome, refused or not.
        /// </summary>
        public static bool IsInsideTranscribedSpan(
            float alphaRad,
            float betaRad,
            out string reason)
        {
            float alphaDeg = alphaRad * Mathf.Rad2Deg;
            float betaDeg = betaRad * Mathf.Rad2Deg;

            if (float.IsNaN(alphaDeg) || float.IsNaN(betaDeg))
            {
                reason = "alpha/beta are not finite";
                return false;
            }

            if (alphaDeg < SourceAlphaMinDeg || alphaDeg > SourceAlphaMaxDeg)
            {
                reason =
                    "alpha=" + alphaDeg.ToString("F2")
                    + " deg is outside the transcribed breakpoint span ["
                    + SourceAlphaMinDeg.ToString("F1") + ", "
                    + SourceAlphaMaxDeg.ToString("F1")
                    + "] deg; the research polynomials diverge there";
                return false;
            }

            if (Mathf.Abs(betaDeg) > SourceAbsBetaMaxDeg)
            {
                reason =
                    "|beta|=" + Mathf.Abs(betaDeg).ToString("F2")
                    + " deg exceeds the largest transcribed beta breakpoint "
                    + SourceAbsBetaMaxDeg.ToString("F1")
                    + " deg; the research polynomials are extrapolating there";
                return false;
            }

            reason =
                "alpha=" + alphaDeg.ToString("F2")
                + " deg / beta=" + betaDeg.ToString("F2")
                + " deg inside transcribed span";
            return true;
        }
    }
}
