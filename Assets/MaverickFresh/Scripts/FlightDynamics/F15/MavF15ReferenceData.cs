using UnityEngine;

namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// Source-frozen identity and exact-target facts for Maverick's first full-scale F-15.
    ///
    /// Target:
    /// NASA F-15B 836 / USAF S/N 74-0141, pre-Quiet-Spike baseline,
    /// two Pratt & Whitney F100-PW-100 engines.
    ///
    /// IMPORTANT:
    /// The exact NASA 836 coefficient reference set - wing area S, mean aerodynamic chord cbar
    /// and reference span b - is UNAVAILABLE in the held primary sources. This class therefore
    /// returns ZERO for all three instead of silently borrowing the familiar F-15-family
    /// 608 ft^2 / 15.94 ft values. A MavFlightDynamicsProfile built from this geometry is
    /// intentionally invalid and fails closed until the gap is resolved.
    ///
    /// The source audit, with every candidate graded, is
    /// <see cref="MavF15ReferenceGeometrySources"/> and
    /// Docs/Reference/F15_NASA836_REFERENCE_GEOMETRY_AUDIT_V0.1.md.
    /// </summary>
    public static class MavF15ReferenceData
    {
        public const string TargetConfigurationId =
            "NASA_F15B_836_SN74_0141_PRE_QUIET_SPIKE_BASELINE_F100_PW_100";

        public const string TargetMassStateId =
            "NASA_F15B_836_BASELINE_8K_FUEL_MASS_STATE";

        public const int EngineCount = 2;
        public const string EngineVariant = "Pratt & Whitney F100-PW-100";

        // Exact-target full-scale PHYSICAL external dimensions, frozen in
        // Docs/Reference/F15_FULL_SCALE_TARGET_FREEZE_V0.1.md. SI derived from the raw feet.
        //
        // These describe the airframe. None of them is an aerodynamic coefficient reference
        // quantity, and none may dimensionalize a coefficient.
        public const float AircraftLengthM =
            MavF15ReferenceGeometrySources.Nasa836PhysicalLengthFt
            * MavF15ReferenceGeometrySources.FootToM;

        /// <summary>
        /// Physical tip-to-tip wingspan, 42.8 ft. NOT the coefficient reference span.
        ///
        /// Earlier revisions named this WingSpanM and placed it in the coefficient reference
        /// geometry's span field. No 836 source calls it a reference span, and the one NASA
        /// report that prints both for an F-15 shows the two differing (42.83 ft three-view,
        /// 42.7 ft reference, NF-15B 837).
        /// </summary>
        public const float PhysicalWingSpanM =
            MavF15ReferenceGeometrySources.Nasa836PhysicalWingSpanFt
            * MavF15ReferenceGeometrySources.FootToM;

        public const float AircraftHeightM =
            MavF15ReferenceGeometrySources.Nasa836PhysicalHeightFt
            * MavF15ReferenceGeometrySources.FootToM;

        /// <summary>
        /// The exact-target aerodynamic COEFFICIENT reference geometry: S, cbar and b, drawn
        /// together from one source-accepted reference set, or not at all.
        ///
        /// Today the held sources close none of the three, so all three are zero and
        /// MavFlightDynamicsProfile treats the geometry as invalid - the NASA 836 profile cannot
        /// become live merely because a plausible F-15-family number exists elsewhere.
        ///
        /// CORRECTION. Earlier revisions returned the physical wingspan (13.04544 m) in wingSpanM
        /// while S and cbar were zero. That field is the span Cl, Cn, p-hat and r-hat are
        /// normalized by, so it must come from the same reference set as S and cbar. A physical
        /// dimension beside two unavailable reference quantities is not a reference set. The
        /// physical span is kept, correctly named, as <see cref="PhysicalWingSpanM"/>.
        /// </summary>
        public static MavAeroReferenceGeometry CreateExactTargetGeometry()
        {
            MavF15CoefficientReferenceSet set;
            string reason;
            if (MavF15ReferenceGeometrySources.TrySelectExactTargetReferenceSet(
                    out set, out reason))
            {
                return new MavAeroReferenceGeometry
                {
                    wingAreaM2 = set.areaM2,
                    wingSpanM = set.spanM,
                    meanAerodynamicChordM = set.chordM
                };
            }

            return new MavAeroReferenceGeometry
            {
                wingAreaM2 = 0f,             // UNAVAILABLE for exact NASA 836
                wingSpanM = 0f,              // UNAVAILABLE - reference b, not the physical span
                meanAerodynamicChordM = 0f   // UNAVAILABLE for exact NASA 836
            };
        }

        /// <summary>
        /// No exact-target aerodynamic validity envelope is frozen yet.
        /// An inverted interval is used so Contains(...) is false for every finite flight state.
        /// </summary>
        public static MavFlightDynamicsEnvelope CreateUnavailableAerodynamicEnvelope()
        {
            return new MavFlightDynamicsEnvelope
            {
                alphaMinDeg = 1f,
                alphaMaxDeg = 0f,
                betaMinDeg = 1f,
                betaMaxDeg = 0f,
                minMach = 1f,
                maxMach = 0f
            };
        }

        /// <summary>
        /// Exact NASA 836 hard stops/sign convention are not frozen. Zero travel is safer than
        /// promoting preproduction/family limits into target authority.
        /// </summary>
        public static MavControlSurfaceLimits CreateUnavailableControlLimits()
        {
            return new MavControlSurfaceLimits
            {
                elevatorMinDeg = 0f,
                elevatorMaxDeg = 0f,
                aileronMinDeg = 0f,
                aileronMaxDeg = 0f,
                rudderMinDeg = 0f,
                rudderMaxDeg = 0f,
                leadingEdgeFlapMinDeg = 0f,
                leadingEdgeFlapMaxDeg = 0f
            };
        }
    }
}
