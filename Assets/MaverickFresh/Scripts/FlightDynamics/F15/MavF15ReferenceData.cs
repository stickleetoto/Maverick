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
    /// The exact NASA 836 coefficient-reference wing area and mean aerodynamic chord are still
    /// CONFIG_MATCH_PENDING. This class therefore returns ZERO for those fields instead of silently
    /// borrowing the familiar F-15-family 608 ft^2 / 191.3 in values. A MavFlightDynamicsProfile
    /// built from this geometry is intentionally invalid and fails closed until the gap is resolved.
    /// </summary>
    public static class MavF15ReferenceData
    {
        public const string TargetConfigurationId =
            "NASA_F15B_836_SN74_0141_PRE_QUIET_SPIKE_BASELINE_F100_PW_100";

        public const string TargetMassStateId =
            "NASA_F15B_836_BASELINE_8K_FUEL_MASS_STATE";

        public const int EngineCount = 2;
        public const string EngineVariant = "Pratt & Whitney F100-PW-100";

        // Exact-target full-scale external dimensions frozen in
        // Docs/Reference/F15_FULL_SCALE_TARGET_FREEZE_V0.1.md.
        public const float AircraftLengthM = 19.41576f;
        public const float WingSpanM = 13.04544f;
        public const float AircraftHeightM = 5.69976f;

        /// <summary>
        /// Creates only the geometry that is exact-target frozen today.
        ///
        /// wingAreaM2 and meanAerodynamicChordM deliberately remain zero. MavFlightDynamicsProfile
        /// treats that as invalid geometry, so the NASA 836 profile cannot become live merely because
        /// a plausible F-15-family number exists elsewhere.
        /// </summary>
        public static MavAeroReferenceGeometry CreateExactTargetGeometry()
        {
            return new MavAeroReferenceGeometry
            {
                wingAreaM2 = 0f,                  // CONFIG_MATCH_PENDING for exact NASA 836
                wingSpanM = WingSpanM,            // FROZEN_DIRECT
                meanAerodynamicChordM = 0f        // CONFIG_MATCH_PENDING for exact NASA 836
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
