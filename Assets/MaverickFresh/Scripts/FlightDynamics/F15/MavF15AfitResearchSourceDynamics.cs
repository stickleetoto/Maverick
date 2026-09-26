using System;

namespace MaverickFresh.FlightDynamics.F15
{
    // WP-3D: the research source's own time-derivative system x-dot = f(x, stabilator), as Baumann's
    // AUTO driver D2ICCV28 evaluates it in subroutine FUNX (DTIC ADA217366 PDF pp.91-96; Davison
    // DTIC ADA256613 App. B carries the same FUNX and the same printed K constants). Research
    // configuration only; never NASA 836.
    //
    // This is the dynamic system whose Jacobian the source's AUTO used for stability. It is NOT the
    // trim residual: the trim solver balances body-axis forces and moments, while FUNX returns the
    // state derivatives alpha-dot, beta-dot and V-dot in wind axes and p-dot, r-dot with the
    // inertia coupling already inverted.
    //
    // Nothing here flies. It evaluates one derivative at one state and holds no state of its own.

    /// <summary>
    /// The source's eight states in the source's order (FUNX U(1)..U(8), PDF p.96-97), in physical
    /// units: angles in radians, rates in rad/s, true airspeed in ft/s.
    ///
    /// The source itself stores alpha, beta, theta and phi in DEGREES and V in THOUSANDS of ft/s
    /// (U(8) = VTRFPS/1000). That is a diagonal rescaling of this state; it changes no eigenvalue.
    /// </summary>
    public struct MavF15ResearchSourceState
    {
        public const int Count = 8;

        public double alphaRad;
        public double betaRad;
        public double pRadSec;
        public double qRadSec;
        public double rRadSec;
        public double thetaRad;
        public double phiRad;
        public double trueAirspeedFtPerSec;

        public double[] ToArray()
        {
            return new[] { alphaRad, betaRad, pRadSec, qRadSec, rRadSec, thetaRad, phiRad, trueAirspeedFtPerSec };
        }

        public static MavF15ResearchSourceState FromArray(double[] x)
        {
            return new MavF15ResearchSourceState
            {
                alphaRad = x[0],
                betaRad = x[1],
                pRadSec = x[2],
                qRadSec = x[3],
                rRadSec = x[4],
                thetaRad = x[5],
                phiRad = x[6],
                trueAirspeedFtPerSec = x[7]
            };
        }
    }

    /// <summary>
    /// The source's derived constants K1..K17 (driver D2ICCV28, PDF pp.91-92), computed here from
    /// the same density, geometry, mass and inertia the source states. The source HARD-CODES the
    /// printed values of the eleven it uses (K1, K5, K7..K10, K12..K17, with K16 = K13); validation
    /// compares these with the printed ones. K2, K3, K4, K6 and K11 are only intermediates.
    /// </summary>
    public struct MavF15ResearchSourceKConstants
    {
        /// <summary>.5*RHO*SREF/RMASS, 1/ft.</summary>
        public double k1;

        /// <summary>(IZ-IY)/IX.</summary>
        public double k2;

        /// <summary>IXZ*IXZ/(IX*IZ).</summary>
        public double k3;

        /// <summary>(IY-IX)/IZ.</summary>
        public double k4;

        /// <summary>IXZ/IX.</summary>
        public double k5;

        /// <summary>.5*RHO*BWING*SREF/IX, 1/ft^2.</summary>
        public double k6;

        /// <summary>IXZ/IZ.</summary>
        public double k7;

        /// <summary>.5*RHO*SREF*CWING/IY, 1/ft^2.</summary>
        public double k8;

        /// <summary>(IZ-IX)/IY.</summary>
        public double k9;

        /// <summary>IXZ/IY.</summary>
        public double k10;

        /// <summary>.5*RHO*SREF*BWING/IZ, 1/ft^2.</summary>
        public double k11;

        /// <summary>(K2+K3)/(1-K3).</summary>
        public double k12;

        /// <summary>(1-K4)*K5/(1-K3). The page image reads "(1.-K4)"; the printed value confirms it.</summary>
        public double k13;

        /// <summary>K6/(1-K3).</summary>
        public double k14;

        /// <summary>(K3-K4)/(1-K3).</summary>
        public double k15;

        /// <summary>(1+K2)*K7/(1-K3). Identically equal to K13; the source prints "K16 = K13".</summary>
        public double k16;

        /// <summary>K11/(1-K3).</summary>
        public double k17;
    }

    /// <summary>
    /// x-dot at one state, in the same physical units as <see cref="MavF15ResearchSourceState"/>:
    /// rad/s for the angles, rad/s^2 for the rates, ft/s^2 for V. With the terms that build it.
    /// </summary>
    public struct MavF15ResearchStateDerivative
    {
        public bool evaluated;
        public string reason;

        public double alphaDotRadSec;
        public double betaDotRadSec;
        public double pDotRadSec2;
        public double qDotRadSec2;
        public double rDotRadSec2;
        public double thetaDotRadSec;
        public double phiDotRadSec;
        public double trueAirspeedDotFtPerSec2;

        /// <summary>0.5 * RHO * V^2 with the fixed source RHO and the state's own V.</summary>
        public double dynamicPressurePsf;

        /// <summary>The transcribed routine's coefficients, thrust NOT included.</summary>
        public double aeroCx;
        public double aeroCy;
        public double aeroCz;
        public double aeroCl;
        public double aeroCm;
        public double aeroCn;

        /// <summary>
        /// The source's COEFF totals as FUNX consumes them: CX includes THRUST/QBARS and CMM includes
        /// THRUST*(0.25/12)/(QBARS*CWING) (PDF p.117). Each thrust term enters here and nowhere else.
        /// </summary>
        public double sourceCx;
        public double sourceCmm;

        public double[] ToArray()
        {
            return new[]
            {
                alphaDotRadSec, betaDotRadSec, pDotRadSec2, qDotRadSec2, rDotRadSec2, thetaDotRadSec, phiDotRadSec,
                trueAirspeedDotFtPerSec2
            };
        }
    }

    /// <summary>
    /// The AFIT/Baumann/Davison research model's time-derivative system, F(1)..F(8) of Baumann's
    /// FUNX (DTIC ADA217366 PDF pp.95-96), with the symmetric stabilator held as the parameter
    /// PAR(1) and aileron, rudder and differential tail at 0.
    ///
    /// FORCES, wind axes, exactly as FUNX writes them with ax = K1 V CX - G sin(theta)/V,
    /// ay = K1 V CY + G cos(theta) sin(phi)/V, az = K1 V CZ + G cos(theta) cos(phi)/V:
    ///   F(1) alpha-dot = Q + (-(ax + R sin b) sin a + (az - P sin b) cos a) / cos b
    ///   F(2) beta-dot  = -(ax sin b + R) cos a + ay cos b - (az sin b - P) sin a
    ///   F(8) V-dot     = V (ax cos a cos b + ay sin b + az sin a cos b)
    /// MOMENTS, with the Ixz coupling already inverted into the K constants:
    ///   F(3) p-dot = -K12 Q R + K13 P Q + K14 (CLM + K7 CNM) V^2
    ///   F(4) q-dot =  K8 V^2 CMM + K9 P R + K10 (R^2 - P^2)
    ///   F(5) r-dot =  K15 P Q - K16 Q R + K17 V^2 (K5 CLM + CNM)
    /// KINEMATICS:
    ///   F(6) theta-dot = Q cos(phi) - R sin(phi)
    ///   F(7) phi-dot   = P + (Q sin(phi) + R cos(phi)) tan(theta)
    /// Psi is not a state (FUNX has none: the equations do not depend on it, Baumann eq. 3.17 is
    /// dropped, PDF p.46), and nothing depends on altitude: RHO is one constant.
    ///
    /// UNITS. FUNX multiplies F(1), F(2), F(6), F(7) by DEGRAD because its angles are in degrees,
    /// and scales F(8) by 1/1000 because U(8) is V/1000. Here every state is physical (rad, rad/s,
    /// ft/s), so neither scaling appears; the two systems differ by a constant diagonal similarity.
    ///
    /// SOURCE ENVIRONMENT, all fixed: RHO 0.0012673 slug/ft^3, G 32.174 ft/s^2, RMASS 37000/G slug,
    /// IX 25480, IY 166620, IZ 186930, IXZ -1000 slug-ft^2, THRUST 8300 lbf total with its 0.25-in
    /// arm. No atmosphere model, no throttle, no engine model. Rates enter the coefficient routine as
    /// the source normalizes them: pHat = P b/2V, qHat = Q cbar/2V, rHat = R b/2V.
    ///
    /// SCOPE: research source reproduction only. It says nothing about the real F-15, NASA 836, the
    /// production flight-control system, or a Unity Rigidbody. The source model has no CAS
    /// (Baumann PDF pp.33-34: "the CAS was not implemented in this model").
    /// </summary>
    public static class MavF15AfitResearchSourceDynamics
    {
        public const string Scope =
            "RESEARCH SOURCE DYNAMICS ONLY - AFIT/Baumann/Davison FUNX, fixed source density, fixed 8,300 lbf "
            + "total thrust, no CAS; the M 0.6 fit applied at the given V (source-exercised, NOT "
            + "aerodynamically validated); NOT NASA 836, NOT the production FCS, NOT a flown Rigidbody";

        public static readonly string[] StateNames = { "alpha", "beta", "p", "q", "r", "theta", "phi", "V" };
        public static readonly string[] StateUnits = { "rad", "rad", "rad/s", "rad/s", "rad/s", "rad", "rad", "ft/s" };

        private const string LinearizationControlNote =
            "WP-3D research source dynamics: stabilator held at an equilibrium value (static evaluation only)";

        /// <summary>K1..K17 from the source's density, geometry, mass and inertia.</summary>
        public static MavF15ResearchSourceKConstants KConstants()
        {
            double rho = MavF15SourceExercisedOperatingDomain.SourceAirDensitySlugPerFt3;
            double g = MavF15SourceExercisedOperatingDomain.SourceGravityFtPerSec2;
            double mass = MavF15AfitResearchMassReference.WeightLb / g;
            double S = MavF15BaumannMach06Reference.WingAreaFt2;
            double b = MavF15BaumannMach06Reference.WingSpanFt;
            double c = MavF15BaumannMach06Reference.MeanAerodynamicChordFt;
            double ix = MavF15AfitResearchMassReference.IxxSlugFt2;
            double iy = MavF15AfitResearchMassReference.IyySlugFt2;
            double iz = MavF15AfitResearchMassReference.IzzSlugFt2;
            double ixz = MavF15AfitResearchMassReference.IxzSlugFt2;

            MavF15ResearchSourceKConstants k = new MavF15ResearchSourceKConstants
            {
                k1 = 0.5 * rho * S / mass,
                k2 = (iz - iy) / ix,
                k3 = ixz * ixz / (ix * iz),
                k4 = (iy - ix) / iz,
                k5 = ixz / ix,
                k6 = 0.5 * rho * b * S / ix,
                k7 = ixz / iz,
                k8 = 0.5 * rho * S * c / iy,
                k9 = (iz - ix) / iy,
                k10 = ixz / iy,
                k11 = 0.5 * rho * S * b / iz
            };
            k.k12 = (k.k2 + k.k3) / (1.0 - k.k3);
            k.k13 = (1.0 - k.k4) * k.k5 / (1.0 - k.k3);
            k.k14 = k.k6 / (1.0 - k.k3);
            k.k15 = (k.k3 - k.k4) / (1.0 - k.k3);
            k.k16 = (1.0 + k.k2) * k.k7 / (1.0 - k.k3);
            k.k17 = k.k11 / (1.0 - k.k3);
            return k;
        }

        /// <summary>
        /// FUNX at one state with the symmetric stabilator held fixed (degrees, the source's PAR(1)).
        /// Aileron, rudder and differential tail are 0. Pure: no state is kept between calls.
        /// </summary>
        public static MavF15ResearchStateDerivative EvaluateStateDerivative(
            MavF15ResearchSourceState state,
            double symmetricStabilatorDeg)
        {
            MavF15ResearchStateDerivative d = new MavF15ResearchStateDerivative();

            double V = state.trueAirspeedFtPerSec;
            if (!(V > 0.0) || !IsFinite(V))
            {
                d.reason = "true airspeed must be positive and finite";
                return d;
            }

            if (!IsFinite(state.alphaRad) || !IsFinite(state.betaRad) || !IsFinite(state.pRadSec)
                || !IsFinite(state.qRadSec) || !IsFinite(state.rRadSec) || !IsFinite(state.thetaRad)
                || !IsFinite(state.phiRad) || !IsFinite(symmetricStabilatorDeg))
            {
                d.reason = "state or stabilator is not finite";
                return d;
            }

            MavF15ResearchStaticControlState controls;
            string reason;
            if (!MavF15ResearchStaticControlState.TryCreate(
                    MavF15ResearchDemonstratedControlRange.AfitBaumannTabulatedEquilibria(),
                    (float)symmetricStabilatorDeg, 0f, 0f, 0f, LinearizationControlNote,
                    out controls, out reason))
            {
                d.reason = reason;
                return d;
            }

            MavF15ResearchSourceKConstants k = KConstants();
            double g = MavF15SourceExercisedOperatingDomain.SourceGravityFtPerSec2;
            double rho = MavF15SourceExercisedOperatingDomain.SourceAirDensitySlugPerFt3;
            double S = MavF15BaumannMach06Reference.WingAreaFt2;
            double b = MavF15BaumannMach06Reference.WingSpanFt;
            double c = MavF15BaumannMach06Reference.MeanAerodynamicChordFt;
            double thrust = MavF15AfitResearchThrustSource.SourceTotalThrustLbf;
            double thrustArmFt = MavF15AfitResearchThrustSource.SourceThrustLineOffsetIn / 12.0;

            double P = state.pRadSec, Q = state.qRadSec, R = state.rRadSec;
            double ca = Math.Cos(state.alphaRad), sa = Math.Sin(state.alphaRad);
            double cb = Math.Cos(state.betaRad), sb = Math.Sin(state.betaRad);
            double cthe = Math.Cos(state.thetaRad), sthe = Math.Sin(state.thetaRad);
            double cphi = Math.Cos(state.phiRad), sphi = Math.Sin(state.phiRad);

            double qbar = 0.5 * rho * V * V;
            double qbarS = qbar * S;

            // COEFF: the routine's coefficients at the source's normalized rates.
            float pHat = (float)(P * b / (2.0 * V));
            float qHat = (float)(Q * c / (2.0 * V));
            float rHat = (float)(R * b / (2.0 * V));
            MavF15BaumannSurfaceState surfaces = controls.ToBaumannSurfaceStateForStaticEvaluation();
            MavAeroCoefficients lon = MavF15BaumannMach06Longitudinal.Evaluate(
                (float)state.alphaRad, surfaces.symmetricStabilatorDeg, qHat);
            MavAeroCoefficients lat = MavF15BaumannMach06LateralDirectional.Evaluate(
                (float)state.alphaRad, (float)state.betaRad, surfaces, pHat, rHat);

            // COEFF adds the thrust to CX and its line moment to CMM, once (PDF p.117).
            double cx = lon.cx + thrust / qbarS;
            double cmm = lon.cm + thrust * thrustArmFt / (qbarS * c);
            double cy = lat.cy, cz = lon.cz, clm = lat.cl, cnm = lat.cn;

            double ax = k.k1 * V * cx - g * sthe / V;
            double ay = k.k1 * V * cy + g * cthe * sphi / V;
            double az = k.k1 * V * cz + g * cthe * cphi / V;
            double v2 = V * V;

            d.alphaDotRadSec = Q + (-(ax + R * sb) * sa + (az - P * sb) * ca) / cb;
            d.betaDotRadSec = -(ax * sb + R) * ca + ay * cb - (az * sb - P) * sa;
            d.pDotRadSec2 = -k.k12 * Q * R + k.k13 * P * Q + k.k14 * (clm + k.k7 * cnm) * v2;
            d.qDotRadSec2 = k.k8 * v2 * cmm + k.k9 * P * R + k.k10 * (R * R - P * P);
            d.rDotRadSec2 = k.k15 * P * Q - k.k16 * Q * R + k.k17 * v2 * (k.k5 * clm + cnm);
            d.thetaDotRadSec = Q * cphi - R * sphi;
            d.phiDotRadSec = P + Q * (sthe / cthe) * sphi + R * (sthe / cthe) * cphi;
            d.trueAirspeedDotFtPerSec2 = V * (ax * ca * cb + ay * sb + az * sa * cb);

            d.dynamicPressurePsf = qbar;
            d.aeroCx = lon.cx;
            d.aeroCy = lat.cy;
            d.aeroCz = lon.cz;
            d.aeroCl = lat.cl;
            d.aeroCm = lon.cm;
            d.aeroCn = lat.cn;
            d.sourceCx = cx;
            d.sourceCmm = cmm;

            double[] all = d.ToArray();
            d.evaluated = true;
            for (int i = 0; i < all.Length; i++)
            {
                if (!IsFinite(all[i]))
                    d.evaluated = false;
            }

            d.reason = d.evaluated ? "evaluated" : "non-finite derivative";
            return d;
        }

        /// <summary>
        /// Array form of <see cref="EvaluateStateDerivative(MavF15ResearchSourceState, double)"/>:
        /// x and xDot are in the physical state order and units. Returns false when not evaluated.
        /// </summary>
        public static bool EvaluateStateDerivative(double[] x, double symmetricStabilatorDeg, double[] xDot)
        {
            MavF15ResearchStateDerivative d =
                EvaluateStateDerivative(MavF15ResearchSourceState.FromArray(x), symmetricStabilatorDeg);
            double[] v = d.ToArray();
            for (int i = 0; i < MavF15ResearchSourceState.Count; i++)
                xDot[i] = d.evaluated ? v[i] : double.NaN;
            return d.evaluated;
        }

        private static bool IsFinite(double v)
        {
            return !double.IsNaN(v) && !double.IsInfinity(v);
        }
    }
}
