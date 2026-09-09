using UnityEngine;

namespace MaverickFresh.FlightDynamics.F16
{
    /// <summary>
    /// Reference geometry and validity limits for the compact nonlinear F-16
    /// aerodynamic model described by Eugene A. Morelli.
    ///
    /// Mathematical source: NASA NTRS 20040110310 / ACC 1998 paper,
    /// "Global Nonlinear Parametric Modeling with Application to F-16 Aerodynamics".
    ///
    /// The compact aerodynamic fit is based on subsonic wind-tunnel data for a
    /// clean F-16 configuration (gear retracted, no external stores) at Mach < 0.6.
    /// </summary>
    public static class MavF16MorelliReference
    {
        public const float WingAreaM2 = 27.870912f;          // 300 ft^2
        public const float WingSpanM = 9.144f;               // 30 ft
        public const float MeanAerodynamicChordM = 3.450336f; // 11.32 ft

        public const float XcgReferenceCbar = 0.35f;
        public const float DefaultXcgCbar = 0.25f;

        public const float AlphaMinDeg = -10f;
        public const float AlphaMaxDeg = 45f;
        public const float BetaMinDeg = -30f;
        public const float BetaMaxDeg = 30f;
        public const float ElevatorMinDeg = -25f;
        public const float ElevatorMaxDeg = 25f;
        public const float AileronMinDeg = -21.5f;
        public const float AileronMaxDeg = 21.5f;
        public const float RudderMinDeg = -30f;
        public const float RudderMaxDeg = 30f;
        public const float MaxReferenceMach = 0.6f;

        public static MavAeroReferenceGeometry CreateReferenceGeometry()
        {
            MavAeroReferenceGeometry geometry = new MavAeroReferenceGeometry();
            geometry.wingAreaM2 = WingAreaM2;
            geometry.wingSpanM = WingSpanM;
            geometry.meanAerodynamicChordM = MeanAerodynamicChordM;
            return geometry;
        }

        public static float ClampAlphaRad(float alphaRad)
        {
            return Mathf.Clamp(alphaRad, AlphaMinDeg * Mathf.Deg2Rad, AlphaMaxDeg * Mathf.Deg2Rad);
        }

        public static float ClampBetaRad(float betaRad)
        {
            return Mathf.Clamp(betaRad, BetaMinDeg * Mathf.Deg2Rad, BetaMaxDeg * Mathf.Deg2Rad);
        }

        public static float ClampElevatorRad(float elevatorRad)
        {
            return Mathf.Clamp(elevatorRad, ElevatorMinDeg * Mathf.Deg2Rad, ElevatorMaxDeg * Mathf.Deg2Rad);
        }

        public static float ClampAileronRad(float aileronRad)
        {
            return Mathf.Clamp(aileronRad, AileronMinDeg * Mathf.Deg2Rad, AileronMaxDeg * Mathf.Deg2Rad);
        }

        public static float ClampRudderRad(float rudderRad)
        {
            return Mathf.Clamp(rudderRad, RudderMinDeg * Mathf.Deg2Rad, RudderMaxDeg * Mathf.Deg2Rad);
        }
    }
}
