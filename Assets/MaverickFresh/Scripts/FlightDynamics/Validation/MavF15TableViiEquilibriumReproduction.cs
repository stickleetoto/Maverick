using System;
using MaverickFresh.FlightDynamics.F15;

namespace MaverickFresh.FlightDynamics.Validation
{
    /// <summary>Residuals of one published equilibrium, fed through Maverick's research model.</summary>
    public struct MavF15EquilibriumResidual
    {
        public MavF15TableViiState state;
        public bool evaluated;
        public string reason;

        public double dynamicPressurePsf;

        /// <summary>V / 1036.9 ft/s - what Mach WOULD be at 20,000 ft. The source never computes it.</summary>
        public double impliedMachAt20000Ft;

        /// <summary>Body-axis force residuals divided by weight (source axes: X fwd, Y right, Z down).</summary>
        public double forceXOverWeight;
        public double forceYOverWeight;
        public double forceZOverWeight;

        /// <summary>Moment residuals over q.S.b (roll, yaw) and q.S.cbar (pitch).</summary>
        public double rollOverQSb;
        public double pitchOverQSc;
        public double yawOverQSb;

        /// <summary>Steady-state kinematic residuals, rad/s: theta-dot and phi-dot. No aerodynamics.</summary>
        public double thetaDotRadSec;
        public double phiDotRadSec;

        /// <summary>
        /// Print-precision floor for each residual: its first-order change when every printed field
        /// moves by half a unit in its last printed digit. Context, not a tolerance.
        /// </summary>
        public double floorForceX;
        public double floorForceY;
        public double floorForceZ;
        public double floorRoll;
        public double floorPitch;
        public double floorYaw;
        public double floorThetaDot;
        public double floorPhiDot;
    }

    /// <summary>
    /// Static reproduction of Baumann Table VII: each published equilibrium state is fed into
    /// Maverick's transcription of the research coefficient routine together with the research
    /// mass, inertia and thrust, and the equations of motion are evaluated at that state.
    ///
    /// NOT A TRIM SOLVER. Nothing is iterated or adjusted; the published state goes in, the
    /// residual comes out.
    ///
    /// SOURCE SEMANTICS, from Baumann's driver (DTIC ADA217366 PDF pp.90-98) and COEFF (p.101, p.117):
    /// - density: the fixed RHO = 0.0012673 slug/ft^3 at every state;
    /// - dynamic pressure: q = 0.5*RHO*V^2 with the state's own V;
    /// - gravity: G = 32.174 ft/s^2;
    /// - thrust: THRUST = 8300 lb, entering as CX += THRUST/QBARS and
    ///   CMM += THRUST*(0.25/12)/(QBARS*CWING);
    /// - mass and inertia: RMASS = 37000/32.174 slug; IX 25480, IY 166620, IZ 186930,
    ///   IXZ -1000 slug-ft^2, the same values Davison's driver uses.
    ///
    /// Units are the source's (ft, slug, lbf, s) throughout, to add no conversion error.
    /// The equations are the standard body-axis Newton-Euler equations with the inertia tensor
    /// [Ix 0 -Ixz; 0 Iy 0; -Ixz 0 Iz]. The source's own K-constant form reduces to exactly these
    /// (its K12, K13 and K9 match the standard coefficients; see the audit document).
    ///
    /// Surfaces: the published stabilator enters through a MavF15ResearchStaticControlState
    /// (STATIC_EQUILIBRIUM_VALIDATION_ONLY); aileron, rudder and differential tail are 0, as the
    /// caption states.
    /// </summary>
    public static class MavF15TableViiEquilibriumReproduction
    {
        private const double DegToRad = Math.PI / 180.0;

        /// <summary>
        /// ISA speed of sound at 20,000 ft, used ONLY to report an implied Mach. The source never
        /// computes one.
        /// </summary>
        public const double ImpliedMachSpeedOfSoundFtPerSec = 1036.9;

        public static MavF15EquilibriumResidual Evaluate(MavF15TableViiState state)
        {
            MavF15EquilibriumResidual result = Residuals(state);
            if (!result.evaluated)
                return result;

            // Print-precision floor: perturb each printed field by half a unit in its last printed
            // digit (7 significant digits for stabilator and alpha, 4 for every other column).
            double[] floors = new double[8];
            for (int field = 0; field < 9; field++)
            {
                double h = HalfUnitInLastDigit(Get(state, field), field <= 1 ? 7 : 4);
                if (h == 0.0)
                    continue;

                MavF15TableViiState up = Set(state, field, Get(state, field) + h);
                MavF15TableViiState down = Set(state, field, Get(state, field) - h);
                MavF15EquilibriumResidual a = Residuals(up);
                MavF15EquilibriumResidual b = Residuals(down);
                if (!a.evaluated || !b.evaluated)
                    continue;

                floors[0] += 0.5 * Math.Abs(a.forceXOverWeight - b.forceXOverWeight);
                floors[1] += 0.5 * Math.Abs(a.forceYOverWeight - b.forceYOverWeight);
                floors[2] += 0.5 * Math.Abs(a.forceZOverWeight - b.forceZOverWeight);
                floors[3] += 0.5 * Math.Abs(a.rollOverQSb - b.rollOverQSb);
                floors[4] += 0.5 * Math.Abs(a.pitchOverQSc - b.pitchOverQSc);
                floors[5] += 0.5 * Math.Abs(a.yawOverQSb - b.yawOverQSb);
                floors[6] += 0.5 * Math.Abs(a.thetaDotRadSec - b.thetaDotRadSec);
                floors[7] += 0.5 * Math.Abs(a.phiDotRadSec - b.phiDotRadSec);
            }

            result.floorForceX = floors[0];
            result.floorForceY = floors[1];
            result.floorForceZ = floors[2];
            result.floorRoll = floors[3];
            result.floorPitch = floors[4];
            result.floorYaw = floors[5];
            result.floorThetaDot = floors[6];
            result.floorPhiDot = floors[7];
            return result;
        }

        private static MavF15EquilibriumResidual Residuals(MavF15TableViiState s)
        {
            MavF15EquilibriumResidual r = new MavF15EquilibriumResidual { state = s };

            MavF15ResearchStaticControlState controls;
            string reason;
            if (!MavF15ResearchStaticControlState.TryCreate(
                    MavF15ResearchDemonstratedControlRange.AfitBaumannTabulatedEquilibria(),
                    (float)s.stabilatorDeg, 0f, 0f, 0f, MavF15BaumannTableVii.Citation,
                    out controls, out reason))
            {
                r.reason = reason;
                return r;
            }

            double rho = MavF15SourceExercisedOperatingDomain.SourceAirDensitySlugPerFt3;
            double g = MavF15SourceExercisedOperatingDomain.SourceGravityFtPerSec2;
            double weight = MavF15AfitResearchMassReference.WeightLb;
            double mass = weight / g;
            double ix = MavF15AfitResearchMassReference.IxxSlugFt2;
            double iy = MavF15AfitResearchMassReference.IyySlugFt2;
            double iz = MavF15AfitResearchMassReference.IzzSlugFt2;
            double ixz = MavF15AfitResearchMassReference.IxzSlugFt2;
            double S = MavF15BaumannMach06Reference.WingAreaFt2;
            double b = MavF15BaumannMach06Reference.WingSpanFt;
            double c = MavF15BaumannMach06Reference.MeanAerodynamicChordFt;
            double thrust = MavF15AfitResearchThrustSource.SourceTotalThrustLbf;
            double thrustArmFt = MavF15AfitResearchThrustSource.SourceThrustLineOffsetIn / 12.0;

            double V = s.trueVelocityFtPerSec;
            double alpha = s.alphaDeg * DegToRad;
            double beta = s.betaDeg * DegToRad;
            double theta = s.thetaDeg * DegToRad;
            double phi = s.phiDeg * DegToRad;
            double p = s.pRadSec, q = s.qRadSec, rr = s.rRadSec;

            double qbar = 0.5 * rho * V * V;
            r.dynamicPressurePsf = qbar;
            r.impliedMachAt20000Ft = V / ImpliedMachSpeedOfSoundFtPerSec;

            // Source rate normalization: PB = P*BWING/(2V), QB = Q*CWING/(2V), RB = R*BWING/(2V).
            float pHat = (float)(p * b / (2.0 * V));
            float qHat = (float)(q * c / (2.0 * V));
            float rHat = (float)(rr * b / (2.0 * V));

            MavF15BaumannSurfaceState surfaces = controls.ToBaumannSurfaceStateForStaticEvaluation();
            MavAeroCoefficients lon = MavF15BaumannMach06Longitudinal.Evaluate(
                (float)alpha, surfaces.symmetricStabilatorDeg, qHat);
            MavAeroCoefficients lat = MavF15BaumannMach06LateralDirectional.Evaluate(
                (float)alpha, (float)beta, surfaces, pHat, rHat);

            double qS = qbar * S;
            double X = qS * lon.cx + thrust;
            double Y = qS * lat.cy;
            double Z = qS * lon.cz;
            double L = qS * b * lat.cl;
            double M = qS * c * lon.cm + thrust * thrustArmFt;
            double N = qS * b * lat.cn;

            double u = V * Math.Cos(alpha) * Math.Cos(beta);
            double v = V * Math.Sin(beta);
            double w = V * Math.Sin(alpha) * Math.Cos(beta);

            // Body-axis translational equilibrium: u-dot = v-dot = w-dot = 0.
            r.forceXOverWeight = (X / mass - g * Math.Sin(theta) + rr * v - q * w) / g;
            r.forceYOverWeight = (Y / mass + g * Math.Cos(theta) * Math.Sin(phi) + p * w - rr * u) / g;
            r.forceZOverWeight = (Z / mass + g * Math.Cos(theta) * Math.Cos(phi) + q * u - p * v) / g;

            // Rotational equilibrium: p-dot = q-dot = r-dot = 0.
            r.rollOverQSb = (L + (iy - iz) * q * rr + ixz * p * q) / (qS * b);
            r.pitchOverQSc = (M + (iz - ix) * p * rr + ixz * (rr * rr - p * p)) / (qS * c);
            r.yawOverQSb = (N + (ix - iy) * p * q - ixz * q * rr) / (qS * b);

            // Source kinematics, F(6) and F(7): theta-dot and phi-dot.
            r.thetaDotRadSec = q * Math.Cos(phi) - rr * Math.Sin(phi);
            r.phiDotRadSec = p + (q * Math.Sin(phi) + rr * Math.Cos(phi)) * Math.Tan(theta);

            r.evaluated = IsFinite(r.forceXOverWeight) && IsFinite(r.forceYOverWeight)
                          && IsFinite(r.forceZOverWeight) && IsFinite(r.rollOverQSb)
                          && IsFinite(r.pitchOverQSc) && IsFinite(r.yawOverQSb);
            r.reason = r.evaluated ? "evaluated" : "non-finite residual";
            return r;
        }

        /// <summary>Half a unit in the last printed digit of a value printed with this many significant digits.</summary>
        public static double HalfUnitInLastDigit(double value, int significantDigits)
        {
            if (value == 0.0 || double.IsNaN(value))
                return 0.0;
            int exponent = (int)Math.Floor(Math.Log10(Math.Abs(value)));
            return 0.5 * Math.Pow(10.0, exponent - (significantDigits - 1));
        }

        // 0 stabilator, 1 alpha, 2 beta, 3 p, 4 q, 5 r, 6 theta, 7 phi, 8 V
        private static double Get(MavF15TableViiState s, int field)
        {
            switch (field)
            {
                case 0: return s.stabilatorDeg;
                case 1: return s.alphaDeg;
                case 2: return s.betaDeg;
                case 3: return s.pRadSec;
                case 4: return s.qRadSec;
                case 5: return s.rRadSec;
                case 6: return s.thetaDeg;
                case 7: return s.phiDeg;
                default: return s.trueVelocityFtPerSec / 1000.0;
            }
        }

        private static MavF15TableViiState Set(MavF15TableViiState s, int field, double value)
        {
            switch (field)
            {
                case 0: s.stabilatorDeg = value; break;
                case 1: s.alphaDeg = value; break;
                case 2: s.betaDeg = value; break;
                case 3: s.pRadSec = value; break;
                case 4: s.qRadSec = value; break;
                case 5: s.rRadSec = value; break;
                case 6: s.thetaDeg = value; break;
                case 7: s.phiDeg = value; break;
                default: s.trueVelocityFtPerSec = value * 1000.0; break;
            }

            return s;
        }

        private static bool IsFinite(double v)
        {
            return !double.IsNaN(v) && !double.IsInfinity(v);
        }
    }
}
