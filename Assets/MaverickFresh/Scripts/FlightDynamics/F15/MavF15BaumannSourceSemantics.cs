using UnityEngine;

namespace MaverickFresh.FlightDynamics.F15
{
    // What "Mach 0.6 / 20,000 ft" means for the AFIT/Baumann/Davison research model, read from the
    // source code itself (Baumann DTIC ADA217366 App. B driver PDF pp.90-98 and COEFF pp.98-117;
    // Davison DTIC ADA256613 App. B/C). Three concepts, kept apart:
    //
    //   MavF15CoefficientFitCondition        - where the coefficient DATA came from: the 1988 F-15
    //                                          aerobase at 0.6 Mach, 20,000 ft. Mach is not an input
    //                                          to the coefficient routine; it is a property of the fit.
    //   MavF15SourceExercisedOperatingDomain - what the source CODE actually ran: true velocity is a
    //                                          state and varies; density is a fixed 20,000-ft constant;
    //                                          no Mach is ever computed.
    //   MavF15PhysicalValidityEnvelope       - what the AUTHORS claim is physically valid: the fit
    //                                          condition, and no more.
    //
    // Source-exercised is NOT aerodynamically validated. Full audit:
    // Docs/Reference/F15_BAUMANN_SOURCE_CONDITION_AUDIT_V1.0.md.

    /// <summary>
    /// How the research runtime treats a state away from the coefficient-fit condition.
    /// </summary>
    public enum MavF15ResearchConditionMode
    {
        /// <summary>
        /// Refuse everything but the fit condition (Mach 0.6 +/- 0.001, 6,096 +/- 1 m). The default,
        /// and the only mode that claims the coefficients' own validity.
        /// </summary>
        StrictFitCondition = 0,

        /// <summary>
        /// Reproduce what the source code does: admit true airspeeds the source demonstrably
        /// exercised, at the source's fixed-density altitude, using the Mach 0.6 fit at every one.
        ///
        /// RESEARCH ONLY. Everything evaluated away from Mach 0.6 is reported as extrapolated from
        /// the Mach 0.6 fit: source-exercised, not aerodynamically validated. It is never NASA 836
        /// authority, and it only takes effect through the research profile.
        /// </summary>
        SourceReproduction = 1
    }

    /// <summary>The condition the research coefficients were identified / fitted at.</summary>
    public static class MavF15CoefficientFitCondition
    {
        public const float Mach = MavF15BaumannMach06Reference.SourceMach;
        public const float PressureAltitudeFt = MavF15BaumannMach06Reference.SourcePressureAltitudeFt;
        public const float PressureAltitudeM = MavF15BaumannMach06Reference.SourcePressureAltitudeM;

        public const string Provenance =
            "coefficient identification / fitting condition. Baumann: 'Two parameters not listed in "
            + "the force and moment coefficient expressions are Mach number and altitude, which were "
            + "fixed at 0.6 and 20,000 feet respectively' (PDF p.36). COEFF comment: coefficients "
            + "'TAKEN DIRECTLY FROM THE 1988 F15 AEROBASE (0.6 MACH, 20000 FEET)' (Baumann PDF p.101; "
            + "Davison PDF pp.103, 133)";

        /// <summary>True at the fit condition, to the numerical equality tolerances of the strict gate.</summary>
        public static bool Contains(float mach, float altitudeM)
        {
            return Mathf.Abs(mach - Mach) <= MavF15BaumannMach06Reference.NumericalMachTolerance
                && Mathf.Abs(altitudeM - PressureAltitudeM)
                    <= MavF15BaumannMach06Reference.NumericalAltitudeToleranceM;
        }
    }

    /// <summary>
    /// What the research source's own code ran. A statement about the source program, not about
    /// the aircraft.
    /// </summary>
    public static class MavF15SourceExercisedOperatingDomain
    {
        /// <summary>
        /// "RHO - AIR DENSITY AT 20000 FT ALTITUDE, SLUG/FT^3", RHO = .0012673, one constant for
        /// every state (Baumann PDF p.91; Davison PDF pp.91, 124).
        /// </summary>
        public const double SourceAirDensitySlugPerFt3 = 0.0012673;

        /// <summary>The source's G = 32.174 ft/s^2 (Baumann PDF p.95).</summary>
        public const double SourceGravityFtPerSec2 = 32.174;

        /// <summary>
        /// Lowest true velocity in the tabulated equilibria: Baumann Table V point 3, 0.2185 kft/s
        /// (PDF p.82).
        /// </summary>
        public const float MinTabulatedTrueAirspeedFtPerSec = 218.5f;

        /// <summary>
        /// Highest true velocity in the tabulated equilibria: Table VII second-half point 118,
        /// 0.6997 kft/s (PDF p.131).
        /// </summary>
        public const float MaxTabulatedTrueAirspeedFtPerSec = 699.7f;

        public const float MinTabulatedTrueAirspeedMps =
            MinTabulatedTrueAirspeedFtPerSec * MavF15BaumannMach06Reference.FootToM;
        public const float MaxTabulatedTrueAirspeedMps =
            MaxTabulatedTrueAirspeedFtPerSec * MavF15BaumannMach06Reference.FootToM;

        /// <summary>V is state U(8) of the driver, "TRUE VELOCITY, IN THOUSANDS OF FT/SEC".</summary>
        public const bool TrueAirspeedIsAState = true;

        /// <summary>The 8-state model has no altitude state; the density constant stands in for it.</summary>
        public const bool AltitudeIsAState = false;

        /// <summary>No source line computes Mach or a speed of sound.</summary>
        public const bool MachIsComputed = false;

        /// <summary>QBARS = .5*RHO*VTRFPS*VTRFPS*SREF with the varying V and the fixed RHO (Baumann PDF p.101).</summary>
        public const string DynamicPressure = "q = 0.5 * RHO_20000ft * V^2, V varying, RHO fixed";

        public static bool ContainsTrueAirspeed(float trueAirspeedMps)
        {
            return trueAirspeedMps >= MinTabulatedTrueAirspeedMps
                && trueAirspeedMps <= MaxTabulatedTrueAirspeedMps;
        }
    }

    /// <summary>
    /// What the authors claim is physically valid. Deliberately the narrowest of the three.
    /// </summary>
    public static class MavF15PhysicalValidityEnvelope
    {
        public const string AuthorClaims =
            "Davison: 'The model aerodynamics were only valid at M=0.6' (PDF p.44). Baumann: 'Flight at "
            + "Mach 0.6 can be assumed to be in the incompressible flow region' (PDF p.36), and "
            + "'Velocities much higher than [622.14 ft/sec] are in the compressible flow region and "
            + "invalidate the assumptions made in deriving the model' (PDF pp.118-119)";

        /// <summary>
        /// True only at the coefficient-fit condition. Nothing the source exercised elsewhere is
        /// counted as validated.
        /// </summary>
        public static bool IsAerodynamicallyValidated(float mach, float altitudeM)
        {
            return MavF15CoefficientFitCondition.Contains(mach, altitudeM);
        }
    }

    /// <summary>The one research-condition gate the aero model, research thrust and profile share.</summary>
    public static class MavF15ResearchConditionGate
    {
        /// <summary>
        /// True when the state is admissible in this mode.
        ///
        /// <paramref name="insideFitCondition"/> says whether the coefficients are being used at
        /// their own fit condition. In SourceReproduction mode, a false value means every
        /// coefficient is an extrapolation of the Mach 0.6 fit, and <paramref name="reason"/> says so.
        /// </summary>
        public static bool Admits(
            MavF15ResearchConditionMode mode,
            MavFlightState state,
            MavAtmosphereSample atmosphere,
            out bool insideFitCondition,
            out string reason)
        {
            insideFitCondition = MavF15CoefficientFitCondition.Contains(state.mach, atmosphere.altitudeM);

            if (mode != MavF15ResearchConditionMode.SourceReproduction)
                return MavF15BaumannMach06Reference.IsAtSourceCondition(state, atmosphere, out reason);

            // The source has no altitude state: its density is the 20,000-ft constant. Maverick's
            // density follows altitude, so the source semantics hold only at that altitude.
            float altitudeError = Mathf.Abs(atmosphere.altitudeM - MavF15CoefficientFitCondition.PressureAltitudeM);
            if (altitudeError > MavF15BaumannMach06Reference.NumericalAltitudeToleranceM)
            {
                reason = "SOURCE REPRODUCTION refused: the source's density is the fixed 20,000-ft value "
                         + "(no altitude state); altitude proxy "
                         + atmosphere.altitudeM.ToString("F1") + " m";
                return false;
            }

            if (!MavF15SourceExercisedOperatingDomain.ContainsTrueAirspeed(state.trueAirspeedMps))
            {
                reason = "SOURCE REPRODUCTION refused: true airspeed "
                         + (state.trueAirspeedMps / MavF15BaumannMach06Reference.FootToM).ToString("F1")
                         + " ft/s is outside the source-exercised "
                         + MavF15SourceExercisedOperatingDomain.MinTabulatedTrueAirspeedFtPerSec.ToString("F1")
                         + "-"
                         + MavF15SourceExercisedOperatingDomain.MaxTabulatedTrueAirspeedFtPerSec.ToString("F1")
                         + " ft/s; no extrapolation beyond what the source ran";
                return false;
            }

            reason = insideFitCondition
                ? "SOURCE REPRODUCTION at the M=0.6 / 20,000 ft coefficient-fit condition"
                : "SOURCE REPRODUCTION: Mach " + state.mach.ToString("F3")
                  + " is outside the M=0.6 coefficient-fit condition; coefficients EXTRAPOLATED from "
                  + "the M=0.6 fit (source-exercised, NOT aerodynamically validated)";
            return true;
        }
    }
}
