using UnityEngine;

namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// Exact NASA F-15B 836 baseline mass/inertia state used by the R1 profile skeleton.
    ///
    /// Primary target authority:
    /// NASA/TM-2012-215978, table 1, **"Baseline F-15B test airplane"** column, at the table's
    /// stated mid-fuel loading of 8,000 lb.
    ///
    /// Frozen raw source values, selected from <see cref="MavF15Table1MassStates.Baseline"/>:
    ///   weight = 37,426 lb
    ///   CG     = 26.34 % MAC
    ///   Ixx    = 30,345 slug-ft^2
    ///   Iyy    = 198,687 slug-ft^2
    ///   Izz    = 223,214 slug-ft^2
    ///   Ixz    = -5,070 slug-ft^2
    ///
    /// This is one source-defined mass state, not a fuel interpolation model.
    ///
    /// CORRECTION, and why the numbers here moved
    /// ------------------------------------------
    /// Earlier R1 revisions froze table 1's **"Spike extended"** column
    /// (37,152 lb / 26.05 % MAC / 27,953 / 190,777 / 213,957 / -460) while describing it as the
    /// baseline row. The frozen target is
    /// <c>NASA_F15B_836_SN74_0141_PRE_QUIET_SPIKE_BASELINE_F100_PW_100</c> - explicitly the
    /// aircraft BEFORE the Quiet Spike article was fitted - so the baseline column is the only one
    /// that describes it. Two of the three columns even share a weight, so nothing about the
    /// numbers themselves flagged the swap.
    ///
    /// The correction is a source-classification fix, not aircraft tuning. Nothing downstream was
    /// adjusted to compensate for the new mass. Full note in
    /// Docs/Reference/F15_FULL_SCALE_TARGET_FREEZE_V0.1.md.
    ///
    /// Every SI value below is DERIVED from the raw source values at compile time. None is
    /// hand-copied, which is what lets the raw row change without a second, silent error.
    /// </summary>
    public static class MavF15MassReference
    {
        public const float SlugFt2ToKgM2 = 1.3558179483314f;
        public const float PoundMassToKg = 0.45359237f;
        public const float PoundForceToNewton = 4.4482216152605f;

        /// <summary>The table column this reference selects. Baseline, and only baseline.</summary>
        public const MavF15MassStateKind SelectedState = MavF15MassStateKind.Baseline;

        // ---------------------------------------------------------------- raw source values
        // Mirrored from MavF15Table1MassStates.Baseline so they remain compile-time constants.
        // MavF15MassReferenceValidation asserts the two agree, so a divergence cannot survive.

        public const float WeightLb = 37426f;
        public const float XcgPercentMac = 26.34f;

        public const float IxSlugFt2 = 30345f;
        public const float IySlugFt2 = 198687f;
        public const float IzSlugFt2 = 223214f;
        public const float IxzSlugFt2 = -5070f;

        public const float FuelStateLb = 8000f;

        // ---------------------------------------------------------------- derived SI values

        /// <summary>
        /// Mass equivalent of the source weight, treating the table's "Weight, lb" as pounds-mass
        /// in the usual aviation sense. DERIVED; the report publishes no separate mass.
        /// </summary>
        public const float MassKg = WeightLb * PoundMassToKg;

        /// <summary>Weight as a force, for documentation parity. DERIVED.</summary>
        public const float WeightN = WeightLb * PoundForceToNewton;

        public const float FuelStateKg = FuelStateLb * PoundMassToKg;

        /// <summary>CG as a fraction of MAC rather than a percentage.</summary>
        public const float XcgCbar = XcgPercentMac * 0.01f;

        public const float IxKgM2 = IxSlugFt2 * SlugFt2ToKgM2;
        public const float IyKgM2 = IySlugFt2 * SlugFt2ToKgM2;
        public const float IzKgM2 = IzSlugFt2 * SlugFt2ToKgM2;
        public const float IxzKgM2 = IxzSlugFt2 * SlugFt2ToKgM2;

        /// <summary>
        /// Maps the conventional aircraft body-axis inertia tensor into Unity local axes and
        /// diagonalizes the coupled Unity Y/Z block, matching the same common-core convention used
        /// by MavF16MassReference.
        ///
        /// Source body tensor convention (body X forward, Y right, Z down):
        /// [ Ix    0   -Ixz ]
        /// [  0   Iy     0  ]
        /// [ -Ixz  0    Iz  ]
        ///
        /// Unity local axes are X right, Y up, Z forward, so Unity X = body Y, Unity Y = -body Z,
        /// Unity Z = body X. Applying that basis change gives:
        /// [ Iy    0     0  ]
        /// [  0   Iz    Ixz ]
        /// [  0   Ixz   Ix  ]
        ///
        /// The source sign of Ixz is preserved before this basis conversion.
        ///
        /// UNCHANGED BY THE COLUMN CORRECTION. The corrected Ixz is about eleven times the value
        /// it replaced, which rotates the principal axes from roughly -0.14 deg to -1.50 deg about
        /// Unity X. That is the arithmetic responding to a larger product of inertia, not a reason
        /// to revisit the convention: the transformation was re-derived during the correction and
        /// is correct, and the diagonalization preserves both the trace and the determinant of the
        /// coupled block exactly.
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
