using UnityEngine;

namespace MaverickFresh.FlightDynamics.F16
{
    /// <summary>
    /// Nominal F-16 mass and inertia reference used by the isolated flight-dynamics branch.
    ///
    /// Authoritative reference for this preset, VERIFIED against the primary source in Phase 5B.5:
    ///
    ///   L. T. Nguyen, M. E. Ogburn, W. P. Gilbert, K. S. Kibler, P. W. Brown, P. L. Deal,
    ///   "Simulator Study of Stall/Post-Stall Characteristics of a Fighter Airplane With Relaxed
    ///   Longitudinal Static Stability", NASA Technical Paper 1538, December 1979,
    ///   TABLE I - MASS AND DIMENSIONAL CHARACTERISTICS USED IN SIMULATION.
    ///
    /// Reproduced as Table 1 of F. R. Garza and E. A. Morelli, "A Collection of Nonlinear Aircraft
    /// Simulations in MATLAB", NASA TM-2003-212145, 2003.
    ///
    /// This citation was corrected in Phase 5B.5 (defect P5B5-D6). It previously named
    /// NASA NTRS 20200003104, which is a 2020 paper on real-time aerodynamic modelling and is not
    /// where this table comes from. The NUMBERS were and are correct - TP-1538 Table I gives
    /// weight 91 188 N (20 500 lb), IX 12 875 (9 496), Iy 75 674 (55 814), IZ 85 552 (63 100),
    /// IXz 1 331 (982) kg-m^2 (slug-ft^2), reference c.g. 0.35 cbar - so this was a provenance
    /// defect rather than a physics defect. It still mattered: a citation that cannot be checked is
    /// the one part of a reference model nobody can audit.
    ///
    /// NOTE (defect P5B5-D2, OPEN): the mass below is 9 298.65 kg, while MavAircraftCatalog's F-16C
    /// legacy profile flies 9 800 kg. Those are two different aircraft masses for one aircraft.
    ///
    /// Values as published:
    ///   m   = 637.16 slug
    ///   Ix  = 9,496 slug-ft^2
    ///   Iy  = 55,814 slug-ft^2
    ///   Iz  = 63,100 slug-ft^2
    ///   Ixz = 982 slug-ft^2
    ///   xcg = 0.25 cbar, reference station = 0.35 cbar
    ///
    /// The source body axes are conventional aircraft axes (X forward, Y right, Z down).
    /// Unity local axes are X right, Y up, Z forward, so the inertia tensor is transformed
    /// before being represented by Rigidbody.inertiaTensor + inertiaTensorRotation.
    /// </summary>
    public static class MavF16MassReference
    {
        public const float SlugToKg = 14.59390294f;
        public const float FootToM = 0.3048f;
        public const float SlugFt2ToKgM2 = 1.35581795f;

        public const float MassSlug = 637.16f;
        public const float IxSlugFt2 = 9496f;
        public const float IySlugFt2 = 55814f;
        public const float IzSlugFt2 = 63100f;
        public const float IxzSlugFt2 = 982f;

        public const float MassKg = MassSlug * SlugToKg;
        public const float IxKgM2 = IxSlugFt2 * SlugFt2ToKgM2;
        public const float IyKgM2 = IySlugFt2 * SlugFt2ToKgM2;
        public const float IzKgM2 = IzSlugFt2 * SlugFt2ToKgM2;
        public const float IxzKgM2 = IxzSlugFt2 * SlugFt2ToKgM2;

        public const float XcgCbar = 0.25f;
        public const float XcgReferenceCbar = 0.35f;

        /// <summary>
        /// Creates Unity Rigidbody mass properties from the published body-axis inertia.
        /// The center-of-mass position is intentionally supplied by the caller because the
        /// local origin of a Unity aircraft prefab is an asset convention, not an aerodynamic
        /// reference point. We must not silently guess that mapping.
        /// </summary>
        public static MavMassProperties CreateUnityMassProperties(Vector3 centerOfMassLocalM)
        {
            // Body-axis tensor convention used here:
            // [ Ix    0   -Ixz ]
            // [  0   Iy     0  ]
            // [ -Ixz  0    Iz  ]
            //
            // After body->Unity axis conversion (Xright, Yup, Zforward):
            // [ Iy    0     0  ]
            // [  0   Iz    Ixz ]
            // [  0   Ixz   Ix  ]
            // The Y/Z block is diagonalized into Unity principal moments.

            float a = IzKgM2;   // Unity local Y/Y
            float d = IxKgM2;   // Unity local Z/Z
            float b = IxzKgM2;  // Unity local Y/Z

            float halfTrace = 0.5f * (a + d);
            float halfDifference = 0.5f * (a - d);
            float root = Mathf.Sqrt(halfDifference * halfDifference + b * b);

            float principalY = halfTrace + root;
            float principalZ = halfTrace - root;
            float rotationAboutUnityXRad = 0.5f * Mathf.Atan2(2f * b, a - d);

            MavMassProperties properties = new MavMassProperties();
            properties.massKg = MassKg;
            properties.centerOfMassLocalM = centerOfMassLocalM;
            properties.inertiaTensorKgM2 = new Vector3(
                IyKgM2,
                principalY,
                principalZ
            );
            properties.inertiaTensorRotationEulerDeg = new Vector3(
                rotationAboutUnityXRad * Mathf.Rad2Deg,
                0f,
                0f
            );
            return properties;
        }
    }
}
