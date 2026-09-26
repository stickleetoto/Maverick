using UnityEngine;

namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// Frozen metadata for the AFIT/Baumann F-15 research aerodynamic model.
    ///
    /// THIS IS NOT THE NASA 836 TARGET MODEL.
    ///
    /// Source chain:
    /// - M. T. Davison, "An Examination of Wing Rock for the F-15",
    ///   AFIT/GAE/ENY/92M-01, 1992, Appendix C.
    /// - R. C. Nolan II, "Wing Rock Prediction Method for a High Performance
    ///   Fighter Aircraft", AFIT/GAE/ENY/92J-02, 1992.
    ///
    /// The source states that the polynomial aerodynamics represent the F-15 at
    /// Mach 0.6 and 20,000 ft pressure altitude. The coefficient routine identifies
    /// its primary lineage as McAir baseline-simulator ARO10 / 1988 F-15 aerobase data.
    ///
    /// These constants exist only so a separately tagged CROSS-VALIDATION model can
    /// be reproduced without contaminating the exact NASA 836 profile.
    /// </summary>
    public static class MavF15BaumannMach06Reference
    {
        public const string ModelId =
            "f15-baumann-afit-mach06-20kft-CROSS_VALIDATION";

        public const string SourceIdentity =
            "Davison AFIT/GAE/ENY/92M-01 Appendix C; Nolan AFIT/GAE/ENY/92J-02; "
            + "1988 F-15 aerobase / McAir baseline-simulator lineage.";

        public const float SourceMach = 0.6f;
        public const float SourcePressureAltitudeFt = 20000f;
        public const float SourcePressureAltitudeM = 6096f;

        // Source coefficient-reference geometry used by the research program.
        public const float WingAreaFt2 = 608f;
        public const float WingSpanFt = 42.8f;
        public const float MeanAerodynamicChordFt = 15.94f;

        public const float SquareFootToM2 = 0.09290304f;
        public const float FootToM = 0.3048f;

        public const float WingAreaM2 = WingAreaFt2 * SquareFootToM2;
        public const float WingSpanM = WingSpanFt * FootToM;
        public const float MeanAerodynamicChordM = MeanAerodynamicChordFt * FootToM;

        // ARO10 moment reference center recorded by the reproduced coefficient routine.
        public const float MomentReferenceCgCbar = 0.2565f;

        // These are equality tolerances for reproducing ONE source condition, not a
        // claimed physical validity envelope.
        public const float NumericalMachTolerance = 0.001f;
        public const float NumericalAltitudeToleranceM = 1f;

        public static MavAeroReferenceGeometry CreateReferenceGeometry()
        {
            return new MavAeroReferenceGeometry
            {
                wingAreaM2 = WingAreaM2,
                wingSpanM = WingSpanM,
                meanAerodynamicChordM = MeanAerodynamicChordM
            };
        }

        /// <summary>
        /// True only at the fixed flight condition represented by this research fit.
        ///
        /// MavAtmosphereSample.altitudeM is used as the standard-atmosphere altitude
        /// proxy. No surrounding Mach/altitude envelope is inferred from this equality
        /// check.
        /// </summary>
        public static bool IsAtSourceCondition(
            MavFlightState state,
            MavAtmosphereSample atmosphere,
            out string reason)
        {
            float machError = Mathf.Abs(state.mach - SourceMach);
            if (machError > NumericalMachTolerance)
            {
                reason = "research aero is fixed at M=0.6; current Mach="
                         + state.mach.ToString("F4");
                return false;
            }

            float altitudeError =
                Mathf.Abs(atmosphere.altitudeM - SourcePressureAltitudeM);
            if (altitudeError > NumericalAltitudeToleranceM)
            {
                reason = "research aero is fixed at 20,000 ft pressure-altitude condition; "
                         + "standard-atmosphere altitude proxy="
                         + atmosphere.altitudeM.ToString("F1") + " m";
                return false;
            }

            reason = "M=0.6 / 20,000 ft source condition";
            return true;
        }
    }
}
