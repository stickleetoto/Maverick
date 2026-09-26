using UnityEngine;

namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// Mass and inertia of the AFIT / Baumann / Davison RESEARCH model, version-matched to the
    /// aerodynamic routine Maverick transcribes.
    ///
    /// THIS IS NOT THE NASA 836 MASS STATE. That is <see cref="MavF15MassReference"/> (Table 1,
    /// Baseline column), and nothing here reads from or writes to it.
    ///
    /// SOURCE - Davison, AFIT/GAE/ENY/92M-01, DTIC ADA256613 (Internet Archive DTIC mirror):
    ///   driver program, PDF p.91 (printed p.81): <c>RMASS=37000./32.174</c>, i.e. 37,000 lb;
    ///   PDF p.92 (printed p.82): <c>IX= 25480. IY= 166620. IZ= 186930. IXZ= -1000.</c>, slug-ft^2.
    ///
    /// A detail that matters: on p.92 those inertia lines are COMMENTED OUT. The active code uses
    /// precomputed constants K1..K17. The constants reproduce exactly from the commented values
    /// (K9 = (IZ-IX)/IY = 0.96897131, K5 = IXZ/IX = -0.03924647, K7 = IXZ/IZ = -0.00534960,
    /// K10 = IXZ/IY = -0.00600168, K1 = 0.5*RHO*SREF/RMASS = 3.3500889e-4), so these ARE the values
    /// the running model used. The research validation recomputes each one.
    ///
    /// The same numbers appear in the AFIT physical-characteristics table (McDonnell PDF p.84,
    /// Davison PDF p.88), captioned there as 50 % fuel, gear up.
    ///
    /// SIGN. The model's rotational equations couple roll and yaw through +IXZ/IZ and +IXZ/IX,
    /// which is the conventional tensor [ Ix 0 -Ixz ; 0 Iy 0 ; -Ixz 0 Iz ]. That is the convention
    /// <see cref="MavF15InertiaBasis"/> takes, so the source sign is preserved as printed.
    /// </summary>
    public static class MavF15AfitResearchMassReference
    {
        /// <summary>Research mass-state id. Carries no exact-target token.</summary>
        public const string ResearchMassStateId = "F15_AFIT_DAVISON_DRIVER_37000LB_RESEARCH_MASS_STATE";

        public const string Citation =
            "Davison AFIT/GAE/ENY/92M-01 (DTIC ADA256613) driver program: RMASS=37000./32.174 "
            + "(PDF p.91, printed p.81); IX=25480. IY=166620. IZ=186930. IXZ=-1000. slug-ft^2 "
            + "(PDF p.92, printed p.82; commented, reproduced by the active K constants).";

        // ---------------------------------------------------------------- raw source values

        public const float WeightLb = 37000f;
        public const float IxxSlugFt2 = 25480f;
        public const float IyySlugFt2 = 166620f;
        public const float IzzSlugFt2 = 186930f;
        public const float IxzSlugFt2 = -1000f;

        /// <summary>
        /// The research model's CG, as a fraction of its 15.94-ft MAC. The ARO10 routine states the
        /// aerodynamic data were referenced to this point and that "the moments of inertia and other
        /// aircraft data are for a clean configuration test aircraft with a CG at the same CG. As a
        /// result, there is no 'CG offset' to be computed" (Davison listing printed p.123). CG and
        /// moment reference therefore coincide IN THIS MODEL, by construction.
        /// </summary>
        public const float CgAndMomentReferenceFractionCbar =
            MavF15BaumannMach06Reference.MomentReferenceCgCbar;

        // ---------------------------------------------------------------- derived SI

        public const float MassKg = WeightLb * MavF15MassReference.PoundMassToKg;
        public const float IxKgM2 = IxxSlugFt2 * MavF15MassReference.SlugFt2ToKgM2;
        public const float IyKgM2 = IyySlugFt2 * MavF15MassReference.SlugFt2ToKgM2;
        public const float IzKgM2 = IzzSlugFt2 * MavF15MassReference.SlugFt2ToKgM2;
        public const float IxzKgM2 = IxzSlugFt2 * MavF15MassReference.SlugFt2ToKgM2;

        /// <summary>
        /// Unity mass properties for the research profile.
        ///
        /// CENTER OF MASS - A SIMULATION REFERENCE CHOICE, NOT SOURCE DATA. The research model gives
        /// no absolute CG position that a Unity asset could be measured against. Its aerodynamic
        /// moments are referenced to its own CG (above), so placing the Rigidbody center of mass at
        /// the aircraft's local origin, and applying the model's moments about it, introduces no
        /// offset the model itself does not have. Callers pass that local point explicitly; the
        /// research profile passes Vector3.zero and says so.
        /// </summary>
        public static MavMassProperties CreateUnityMassProperties(Vector3 centerOfMassLocalM)
        {
            return MavF15InertiaBasis.CreateUnityMassProperties(
                MassKg, IxKgM2, IyKgM2, IzKgM2, IxzKgM2, centerOfMassLocalM);
        }
    }
}
