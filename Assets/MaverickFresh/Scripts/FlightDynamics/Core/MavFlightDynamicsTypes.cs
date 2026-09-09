using System;
using UnityEngine;

namespace MaverickFresh.FlightDynamics
{
    /// <summary>
    /// Standard-atmosphere sample in SI units.
    /// </summary>
    [Serializable]
    public struct MavAtmosphereSample
    {
        public float altitudeM;
        public float temperatureK;
        public float pressurePa;
        public float densityKgM3;
        public float speedOfSoundMps;
    }

    /// <summary>
    /// Snapshot consumed by aerodynamic models.
    ///
    /// Aerodynamic body axes follow the conventional aircraft definition:
    /// X = forward, Y = right, Z = down.
    /// Angular rates are p, q, r in rad/s around those axes.
    ///
    /// Unity local axes remain X = right, Y = up, Z = forward and are converted
    /// at the flight-dynamics boundary so aerodynamic code does not need to know
    /// about Unity's axis convention.
    /// </summary>
    [Serializable]
    public struct MavFlightState
    {
        public Vector3 worldPositionM;
        public Vector3 worldVelocityMps;
        public Vector3 aeroBodyVelocityMps;
        public Vector3 aeroBodyRatesRadSec;

        public float trueAirspeedMps;
        public float mach;
        public float dynamicPressurePa;
        public float alphaRad;
        public float betaRad;

        public float AlphaDeg
        {
            get { return alphaRad * Mathf.Rad2Deg; }
        }

        public float BetaDeg
        {
            get { return betaRad * Mathf.Rad2Deg; }
        }
    }

    /// <summary>
    /// Physical control-surface demand. Values are deliberately expressed in
    /// surface deflection degrees rather than game-normalized torque commands.
    /// Sign convention is owned by each aircraft aerodynamic model.
    /// </summary>
    [Serializable]
    public struct MavControlInput
    {
        [Range(0f, 1f)] public float throttle01;
        public float elevatorDeg;
        public float aileronDeg;
        public float rudderDeg;
        public float leadingEdgeFlapDeg;
    }

    /// <summary>
    /// Dimensionless aerodynamic coefficients in conventional aircraft body axes.
    /// CX/CY/CZ are force coefficients. Cl/Cm/Cn are roll/pitch/yaw moment coefficients.
    /// </summary>
    [Serializable]
    public struct MavAeroCoefficients
    {
        public float cx;
        public float cy;
        public float cz;
        public float cl;
        public float cm;
        public float cn;

        public static MavAeroCoefficients Zero
        {
            get { return new MavAeroCoefficients(); }
        }
    }

    /// <summary>
    /// Reference geometry used to dimensionalize aerodynamic coefficients.
    /// </summary>
    [Serializable]
    public struct MavAeroReferenceGeometry
    {
        [Min(0.01f)] public float wingAreaM2;
        [Min(0.01f)] public float wingSpanM;
        [Min(0.01f)] public float meanAerodynamicChordM;
    }

    /// <summary>
    /// Dimensional aerodynamic loads in conventional aircraft body axes.
    /// Force is Newtons, moment is Newton-metres.
    /// </summary>
    [Serializable]
    public struct MavAerodynamicLoads
    {
        public Vector3 forceAeroBodyN;
        public Vector3 momentAeroBodyNm;
    }
}
