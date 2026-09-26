using UnityEngine;

namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// Exact NASA F-15B 836 baseline mass/inertia state used by the R1 profile skeleton.
    ///
    /// Primary target authority:
    /// NASA/TM-2012-215978, Table 1, baseline F-15B test airplane at 8,000 lb fuel.
    ///
    /// Frozen raw source values:
    ///   weight = 37,152 lb
    ///   CG     = 26.05 % MAC
    ///   Ixx    = 27,953 slug-ft^2
    ///   Iyy    = 190,777 slug-ft^2
    ///   Izz    = 213,957 slug-ft^2
    ///   Ixz    = -460 slug-ft^2
    ///
    /// This is one source-defined mass state, not a fuel interpolation model.
    /// </summary>
    public static class MavF15MassReference
    {
        public const float SlugFt2ToKgM2 = 1.3558179483314f;
        public const float PoundMassToKg = 0.45359237f;

        public const float WeightLb = 37152f;
        public const float MassKg = 16851.86373f;
        public const float FuelStateLb = 8000f;
        public const float FuelStateKg = FuelStateLb * PoundMassToKg;

        public const float XcgCbar = 0.2605f;

        public const float IxSlugFt2 = 27953f;
        public const float IySlugFt2 = 190777f;
        public const float IzSlugFt2 = 213957f;
        public const float IxzSlugFt2 = -460f;

        public const float IxKgM2 = IxSlugFt2 * SlugFt2ToKgM2;
        public const float IyKgM2 = IySlugFt2 * SlugFt2ToKgM2;
        public const float IzKgM2 = IzSlugFt2 * SlugFt2ToKgM2;
        public const float IxzKgM2 = IxzSlugFt2 * SlugFt2ToKgM2;

        /// <summary>
        /// Maps the conventional aircraft body-axis inertia tensor into Unity local axes and
        /// diagonalizes the coupled Unity Y/Z block, matching the same common-core convention used
        /// by MavF16MassReference.
        ///
        /// Source body tensor convention:
        /// [ Ix    0   -Ixz ]
        /// [  0   Iy     0  ]
        /// [ -Ixz  0    Iz  ]
        ///
        /// Unity local axes are X right, Y up, Z forward. After basis conversion:
        /// [ Iy    0     0  ]
        /// [  0   Iz    Ixz ]
        /// [  0   Ixz   Ix  ]
        ///
        /// The source sign of Ixz is preserved before this basis conversion.
        /// </summary>
        public static MavMassProperties CreateUnityMassProperties(Vector3 centerOfMassLocalM)
        {
            float a = IzKgM2;
            float d = IxKgM2;
            float b = IxzKgM2;

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
