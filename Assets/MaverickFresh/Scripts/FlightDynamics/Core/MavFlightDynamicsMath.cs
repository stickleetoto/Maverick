using UnityEngine;

namespace MaverickFresh.FlightDynamics
{
    /// <summary>
    /// Boundary helpers between Unity local axes (X right, Y up, Z forward)
    /// and conventional aerodynamic body axes (X forward, Y right, Z down).
    ///
    /// The two bases differ in handedness: the component change of basis has determinant -1.
    /// That distinction matters, and this class keeps the two cases apart deliberately:
    ///
    ///   * A TRUE VECTOR (force, velocity, position) transforms with the basis change alone.
    ///     Use <see cref="UnityLocalVectorToAeroBody"/> / <see cref="AeroBodyVectorToUnityLocal"/>.
    ///
    ///   * An AXIAL VECTOR / pseudo-vector (moment, angular rate) transforms with the basis change
    ///     multiplied by its determinant, i.e. it carries an extra sign on every axis.
    ///     Use <see cref="UnityLocalAngularRateToAeroBody"/> / <see cref="AeroBodyMomentToUnityLocal"/>.
    ///
    /// Physically, in Unity a positive rotation about +X pitches the nose DOWN, about +Y yaws the
    /// nose RIGHT, and about +Z rolls LEFT. In conventional aircraft body axes a positive L rolls
    /// RIGHT, a positive M pitches the nose UP, and a positive N yaws the nose RIGHT. Hence the
    /// roll and pitch channels reverse sign across the boundary while yaw does not.
    ///
    /// Do NOT "simplify" the axial-vector helpers into the true-vector helpers. A round-trip test
    /// cannot catch that mistake, because applying the same wrong sign on the way out and on the
    /// way back in cancels. Directional regressions live in
    /// MavFlightDynamicsPhase1Validation sections [P7] and [P8].
    /// </summary>
    public static class MavFlightDynamicsMath
    {
        public const float MinAirspeedForAnglesMps = 0.5f;

        /// <summary>True-vector conversion: Unity local (right, up, forward) -> aero body (forward, right, down).</summary>
        public static Vector3 UnityLocalVectorToAeroBody(Vector3 unityLocal)
        {
            return new Vector3(unityLocal.z, unityLocal.x, -unityLocal.y);
        }

        /// <summary>True-vector conversion: aero body (forward, right, down) -> Unity local (right, up, forward).</summary>
        public static Vector3 AeroBodyVectorToUnityLocal(Vector3 aeroBody)
        {
            return new Vector3(aeroBody.y, -aeroBody.z, aeroBody.x);
        }

        /// <summary>
        /// Axial-vector conversion: Unity local angular velocity -> conventional body rates p/q/r
        /// about forward/right/down.
        ///
        /// Directions, which are the contract:
        ///   Unity -Z (roll right)  -> p > 0
        ///   Unity -X (nose up)     -> q > 0
        ///   Unity +Y (nose right)  -> r > 0
        /// </summary>
        public static Vector3 UnityLocalAngularRateToAeroBody(Vector3 unityLocalRateRadSec)
        {
            // Basis change composed with determinant -1, so every component is negated relative to
            // the true-vector mapping. See the class remarks.
            return new Vector3(
                -unityLocalRateRadSec.z,
                -unityLocalRateRadSec.x,
                unityLocalRateRadSec.y
            );
        }

        /// <summary>
        /// Axial-vector conversion: conventional body moment L/M/N -> Unity local torque.
        ///
        /// Directions, which are the contract:
        ///   L > 0 (roll right) -> Unity -Z
        ///   M > 0 (nose up)    -> Unity -X
        ///   N > 0 (nose right) -> Unity +Y
        /// </summary>
        public static Vector3 AeroBodyMomentToUnityLocal(Vector3 aeroMomentNm)
        {
            // Basis change composed with determinant -1, so every component is negated relative to
            // the true-vector mapping. See the class remarks.
            return new Vector3(
                -aeroMomentNm.y,
                aeroMomentNm.z,
                -aeroMomentNm.x
            );
        }

        public static float ComputeAlphaRad(Vector3 aeroBodyVelocityMps)
        {
            float u = aeroBodyVelocityMps.x;
            float w = aeroBodyVelocityMps.z;
            if (Mathf.Abs(u) + Mathf.Abs(w) < MinAirspeedForAnglesMps)
                return 0f;

            return Mathf.Atan2(w, u);
        }

        public static float ComputeBetaRad(Vector3 aeroBodyVelocityMps)
        {
            float speed = aeroBodyVelocityMps.magnitude;
            if (speed < MinAirspeedForAnglesMps)
                return 0f;

            float ratio = Mathf.Clamp(aeroBodyVelocityMps.y / speed, -1f, 1f);
            return Mathf.Asin(ratio);
        }

        public static MavAerodynamicLoads Dimensionalize(
            MavAeroCoefficients coefficients,
            MavAeroReferenceGeometry geometry,
            float dynamicPressurePa)
        {
            float q = Mathf.Max(0f, dynamicPressurePa);
            float qS = q * Mathf.Max(0.01f, geometry.wingAreaM2);
            float span = Mathf.Max(0.01f, geometry.wingSpanM);
            float chord = Mathf.Max(0.01f, geometry.meanAerodynamicChordM);

            MavAerodynamicLoads loads = new MavAerodynamicLoads();
            loads.forceAeroBodyN = new Vector3(
                qS * coefficients.cx,
                qS * coefficients.cy,
                qS * coefficients.cz
            );

            loads.momentAeroBodyNm = new Vector3(
                qS * span * coefficients.cl,
                qS * chord * coefficients.cm,
                qS * span * coefficients.cn
            );

            return loads;
        }
    }
}
