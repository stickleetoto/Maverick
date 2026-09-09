using UnityEngine;

namespace MaverickFresh.FlightDynamics
{
    /// <summary>
    /// Boundary helpers between Unity local axes (X right, Y up, Z forward)
    /// and conventional aerodynamic body axes (X forward, Y right, Z down).
    /// </summary>
    public static class MavFlightDynamicsMath
    {
        public const float MinAirspeedForAnglesMps = 0.5f;

        public static Vector3 UnityLocalVectorToAeroBody(Vector3 unityLocal)
        {
            return new Vector3(unityLocal.z, unityLocal.x, -unityLocal.y);
        }

        public static Vector3 AeroBodyVectorToUnityLocal(Vector3 aeroBody)
        {
            return new Vector3(aeroBody.y, -aeroBody.z, aeroBody.x);
        }

        public static Vector3 UnityLocalAngularRateToAeroBody(Vector3 unityLocalRateRadSec)
        {
            // Unity local angular velocity components are rotations about right/up/forward.
            // Conventional p/q/r are rotations about forward/right/down respectively.
            return new Vector3(
                unityLocalRateRadSec.z,
                unityLocalRateRadSec.x,
                -unityLocalRateRadSec.y
            );
        }

        public static Vector3 AeroBodyMomentToUnityLocal(Vector3 aeroMomentNm)
        {
            // L/M/N -> Unity pitch/yaw/roll torque axes.
            return new Vector3(
                aeroMomentNm.y,
                -aeroMomentNm.z,
                aeroMomentNm.x
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
