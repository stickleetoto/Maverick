#if UNITY_EDITOR
using UnityEngine;

namespace MaverickFresh.FlightDynamics.Validation
{
    /// <summary>
    /// VALIDATION-ONLY aerodynamic model of the NASA NESC "fictitious U.S. standard face brick"
    /// (brick_aero.dml, Vol II Table 5), used to drive the production <see cref="MavSixDoFBody"/>
    /// in check cases 2 and 3. It is never attached to an aircraft.
    ///
    /// Outputs body-axis coefficients only; the body dimensionalises them with its own dynamic
    /// pressure and reference geometry, exactly as for an aircraft. Force coefficients are zero
    /// (both cases are dragless). Moment coefficients, when damping is on:
    ///   Cl = Clp p~ + Clr r~,  Cm = Cmq q~,  Cn = Cnp p~ + Cnr r~
    ///   p~ = p b/(2V'), q~ = q c/(2V'), r~ = r b/(2V'),  V' = max(V, VRW minValue)
    /// Case 2 sets every coefficient to zero (Vol II Table 26 note).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MavNasaBrickAeroModel : MavAerodynamicModelBase
    {
        public bool dampingOn;
        public float clp, clr, cmq, cnp, cnr;
        public float airspeedFloorMps;

        [Header("Debug")]
        public float debugNormalizationSpeedMps;
        public MavAeroCoefficients debugLast;

        public override MavAeroCoefficients Evaluate(MavFlightState state, MavControlInput input, MavAtmosphereSample atmosphere)
        {
            MavAeroCoefficients c = new MavAeroCoefficients();
            if (!dampingOn)
            {
                debugLast = c;
                return c;
            }

            float v = Mathf.Max(state.trueAirspeedMps, airspeedFloorMps);
            debugNormalizationSpeedMps = v;
            Vector3 w = state.aeroBodyRatesRadSec;
            float ph = w.x * referenceGeometry.wingSpanM / (2f * v);
            float qh = w.y * referenceGeometry.meanAerodynamicChordM / (2f * v);
            float rh = w.z * referenceGeometry.wingSpanM / (2f * v);
            c.cl = clp * ph + clr * rh;
            c.cm = cmq * qh;
            c.cn = cnp * ph + cnr * rh;
            debugLast = c;
            return c;
        }
    }
}
#endif
