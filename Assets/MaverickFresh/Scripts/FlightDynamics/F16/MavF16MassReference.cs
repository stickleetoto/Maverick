using UnityEngine;

namespace MaverickFresh.FlightDynamics.F16
{
    /// <summary>
    /// Nominal F-16 mass and inertia reference used by the isolated flight-dynamics branch.
    ///
    /// Authoritative reference for this preset:
    /// Eugene A. Morelli, NASA Langley Research Center,
    /// F-16 nonlinear simulation geometry / nominal mass-properties table
    /// (NASA NTRS 20200003104, 2020):
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
