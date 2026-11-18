#if UNITY_EDITOR
using System;

namespace MaverickFresh.FlightDynamics.Validation
{
    /// <summary>
    /// INDEPENDENT reference solution for the NASA NESC tumbling-brick check cases 2 and 3.
    ///
    /// Written from the equations, not from Maverick: double precision, classical fourth-order
    /// Runge-Kutta, no UnityEngine type, no Rigidbody, and no call into any production
    /// flight-dynamics routine (not MavGyroscopicMoment, not MavFlightDynamicsMath, not
    /// MavAtmosphereModel). It exists so that the expected answer and the measured answer never come
    /// from the same code.
    ///
    /// State, in conventional aircraft body axes (x forward, y right, z down) and an inertial frame
    /// equal to the local North-East-Down frame frozen at t = 0:
    ///   w  = body rates (p, q, r), rad/s
    ///   e  = attitude quaternion (scalar first) rotating body vectors into the inertial frame
    ///   h  = altitude above the initial reference, m (geometric)
    ///   vd = downward velocity relative to the air, m/s
    ///
    /// Rotational dynamics (Euler, principal axes, Ixz = Ixy = Iyz = 0 for the brick):
    ///   Ixx dp/dt = L + (Iyy - Izz) q r
    ///   Iyy dq/dt = M + (Izz - Ixx) r p
    ///   Izz dr/dt = N + (Ixx - Iyy) p q
    /// Kinematics: de/dt = 1/2 e (x) (0, w).
    ///
    /// Aerodynamic moments follow brick_aero.dml exactly (Cl = Clp p~ + Clr r~, Cm = Cmq q~,
    /// Cn = Cnp p~ + Cnr r~, with p~ = p b / (2 V'), q~ = q c / (2 V'), r~ = r b / (2 V') and
    /// V' = max(V, the file's VRW minValue)), dimensionalised with the dynamic pressure of the TRUE
    /// airspeed V. Treating the DAVE-ML minValue as an input floor of the aero model only, while the
    /// host simulation dimensionalises with the true q-bar, is the reading documented in
    /// Docs/Validation/NASA/CASE3_DAMPING_REPORT.md.
    /// </summary>
    public static class MavNasaRigidBodyReference
    {
        public enum GravityMode
        {
            /// <summary>Constant downward acceleration (what a flat Unity world applies).</summary>
            Constant = 0,
            /// <summary>Radial J2 gravitation at the equator minus the centripetal term of a body co-moving with the rotating Earth (NESC Vol II Table 73 constants).</summary>
            EquatorialJ2Rotating = 1
        }

        public enum AtmosphereMode
        {
            /// <summary>No air: q-bar is zero (used by case 2).</summary>
            None = 0,
            /// <summary>US Standard Atmosphere 1976 troposphere, evaluated at GEOPOTENTIAL altitude (what the NASA tools use).</summary>
            Us76Geopotential = 1,
            /// <summary>The same layer formula evaluated with GEOMETRIC altitude in place of geopotential altitude (the convention Maverick's MavAtmosphereModel uses), implemented independently.</summary>
            StandardLayerAtGeometricAltitude = 2
        }

        public struct Config
        {
            public double ixx, iyy, izz;           // kg m^2 (principal, aero body axes)
            public double areaM2, spanM, chordM;   // reference geometry
            public double clp, clr, cmq, cnp, cnr; // per radian of normalised rate
            public double airspeedFloorMps;        // DAVE-ML VRW minValue, converted
            public bool aerodynamicDampingOn;
            public GravityMode gravityMode;
            public double constantGravityMps2;
            public AtmosphereMode atmosphereMode;
            public double initialAltitudeM;        // geometric, above mean sea level
        }

        public struct State
        {
            public double p, q, r;
            public double e0, e1, e2, e3;
            public double altitudeM;
            public double downVelocityMps;
        }

        // NESC Vol II Table 73, atmospheric check cases.
        public const double EquatorialRadiusM = 6378137.0;
        public const double GravitationalParameterM3S2 = 3.986004418e14;
        public const double J2 = 0.00108262982;
        public const double EarthRotationRadS = 7.292115e-5;
        // US Standard Atmosphere 1976 (troposphere).
        public const double Us76EarthRadiusM = 6356766.0;
        public const double SeaLevelTemperatureK = 288.15;
        public const double SeaLevelPressurePa = 101325.0;
        public const double LapseRateKPerM = -0.0065;
        public const double GasConstant = 287.05287;
        public const double StandardGravity = 9.80665;

        public static State Initial(double pRadS, double qRadS, double rRadS, double altitudeM)
        {
            return new State { p = pRadS, q = qRadS, r = rRadS, e0 = 1.0, altitudeM = altitudeM };
        }

        /// <summary>Air density, kg/m^3, at geometric altitude per the chosen convention (troposphere only; the brick never leaves it).</summary>
        public static double Density(AtmosphereMode mode, double geometricAltitudeM)
        {
            if (mode == AtmosphereMode.None)
                return 0.0;
            double h = mode == AtmosphereMode.Us76Geopotential
                ? Us76EarthRadiusM * geometricAltitudeM / (Us76EarthRadiusM + geometricAltitudeM)
                : geometricAltitudeM;
            double t = SeaLevelTemperatureK + LapseRateKPerM * h;
            double p = SeaLevelPressurePa * Math.Pow(t / SeaLevelTemperatureK, -StandardGravity / (LapseRateKPerM * GasConstant));
            return p / (GasConstant * t);
        }

        public static double DownwardAcceleration(Config c, double geometricAltitudeM)
        {
            if (c.gravityMode == GravityMode.Constant)
                return c.constantGravityMps2;
            double radius = EquatorialRadiusM + geometricAltitudeM;
            double ratio = EquatorialRadiusM / radius;
            double gravitation = GravitationalParameterM3S2 / (radius * radius) * (1.0 + 1.5 * J2 * ratio * ratio);
            return gravitation - EarthRotationRadS * EarthRotationRadS * radius;
        }

        /// <summary>Aerodynamic moment (L, M, N) in body axes, N m, per brick_aero.dml.</summary>
        public static void AeroMoment(Config c, State s, out double l, out double m, out double n)
        {
            l = m = n = 0.0;
            if (!c.aerodynamicDampingOn)
                return;
            double v = Math.Abs(s.downVelocityMps);
            double rho = Density(c.atmosphereMode, s.altitudeM);
            double qbar = 0.5 * rho * v * v;
            double vn = Math.Max(v, c.airspeedFloorMps);
            double ph = s.p * c.spanM / (2.0 * vn);
            double qh = s.q * c.chordM / (2.0 * vn);
            double rh = s.r * c.spanM / (2.0 * vn);
            l = qbar * c.areaM2 * c.spanM * (c.clp * ph + c.clr * rh);
            m = qbar * c.areaM2 * c.chordM * (c.cmq * qh);
            n = qbar * c.areaM2 * c.spanM * (c.cnp * ph + c.cnr * rh);
        }

        private static State Derivative(Config c, State s)
        {
            double l, m, n;
            AeroMoment(c, s, out l, out m, out n);
            State d = new State();
            d.p = (l + (c.iyy - c.izz) * s.q * s.r) / c.ixx;
            d.q = (m + (c.izz - c.ixx) * s.r * s.p) / c.iyy;
            d.r = (n + (c.ixx - c.iyy) * s.p * s.q) / c.izz;
            d.e0 = 0.5 * (-s.e1 * s.p - s.e2 * s.q - s.e3 * s.r);
            d.e1 = 0.5 * (s.e0 * s.p + s.e2 * s.r - s.e3 * s.q);
            d.e2 = 0.5 * (s.e0 * s.q + s.e3 * s.p - s.e1 * s.r);
            d.e3 = 0.5 * (s.e0 * s.r + s.e1 * s.q - s.e2 * s.p);
            d.altitudeM = -s.downVelocityMps;
            d.downVelocityMps = DownwardAcceleration(c, s.altitudeM);
            return d;
        }

        private static State Add(State a, State d, double h)
        {
            return new State
            {
                p = a.p + h * d.p, q = a.q + h * d.q, r = a.r + h * d.r,
                e0 = a.e0 + h * d.e0, e1 = a.e1 + h * d.e1, e2 = a.e2 + h * d.e2, e3 = a.e3 + h * d.e3,
                altitudeM = a.altitudeM + h * d.altitudeM,
                downVelocityMps = a.downVelocityMps + h * d.downVelocityMps
            };
        }

        public static State StepRk4(Config c, State s, double h)
        {
            State k1 = Derivative(c, s);
            State k2 = Derivative(c, Add(s, k1, 0.5 * h));
            State k3 = Derivative(c, Add(s, k2, 0.5 * h));
            State k4 = Derivative(c, Add(s, k3, h));
            State o = new State
            {
                p = s.p + h / 6.0 * (k1.p + 2.0 * k2.p + 2.0 * k3.p + k4.p),
                q = s.q + h / 6.0 * (k1.q + 2.0 * k2.q + 2.0 * k3.q + k4.q),
                r = s.r + h / 6.0 * (k1.r + 2.0 * k2.r + 2.0 * k3.r + k4.r),
                e0 = s.e0 + h / 6.0 * (k1.e0 + 2.0 * k2.e0 + 2.0 * k3.e0 + k4.e0),
                e1 = s.e1 + h / 6.0 * (k1.e1 + 2.0 * k2.e1 + 2.0 * k3.e1 + k4.e1),
                e2 = s.e2 + h / 6.0 * (k1.e2 + 2.0 * k2.e2 + 2.0 * k3.e2 + k4.e2),
                e3 = s.e3 + h / 6.0 * (k1.e3 + 2.0 * k2.e3 + 2.0 * k3.e3 + k4.e3),
                altitudeM = s.altitudeM + h / 6.0 * (k1.altitudeM + 2.0 * k2.altitudeM + 2.0 * k3.altitudeM + k4.altitudeM),
                downVelocityMps = s.downVelocityMps + h / 6.0 * (k1.downVelocityMps + 2.0 * k2.downVelocityMps + 2.0 * k3.downVelocityMps + k4.downVelocityMps)
            };
            double norm = Math.Sqrt(o.e0 * o.e0 + o.e1 * o.e1 + o.e2 * o.e2 + o.e3 * o.e3);
            o.e0 /= norm; o.e1 /= norm; o.e2 /= norm; o.e3 /= norm;
            return o;
        }

        /// <summary>Integrates to each sample time; returns the states at t = 0, step, 2 step, ...</summary>
        public static State[] Solve(Config c, State initial, double sampleStepS, int sampleCount, int substepsPerSample)
        {
            State[] samples = new State[sampleCount];
            State s = initial;
            samples[0] = s;
            double h = sampleStepS / substepsPerSample;
            for (int k = 1; k < sampleCount; k++)
            {
                for (int i = 0; i < substepsPerSample; i++)
                    s = StepRk4(c, s, h);
                samples[k] = s;
            }
            return samples;
        }

        /// <summary>Direction cosines C(body &lt;- inertial), row-major: row i = body axis i in N, E, D components.</summary>
        public static double[] BodyFromInertial(State s)
        {
            double q0 = s.e0, q1 = s.e1, q2 = s.e2, q3 = s.e3;
            // R = rotation body -> inertial; C(body <- inertial) = R^T.
            double r00 = 1 - 2 * (q2 * q2 + q3 * q3), r01 = 2 * (q1 * q2 - q0 * q3), r02 = 2 * (q1 * q3 + q0 * q2);
            double r10 = 2 * (q1 * q2 + q0 * q3), r11 = 1 - 2 * (q1 * q1 + q3 * q3), r12 = 2 * (q2 * q3 - q0 * q1);
            double r20 = 2 * (q1 * q3 - q0 * q2), r21 = 2 * (q2 * q3 + q0 * q1), r22 = 1 - 2 * (q1 * q1 + q2 * q2);
            return new[] { r00, r10, r20, r01, r11, r21, r02, r12, r22 };
        }

        /// <summary>Rotation angle (rad) between two direction-cosine matrices, accurate for small angles: 2 asin(||C1 - C2||_F / (2 sqrt 2)).</summary>
        public static double AttitudeDifferenceRad(double[] c1, double[] c2)
        {
            double sum = 0.0;
            for (int i = 0; i < 9; i++)
            {
                double d = c1[i] - c2[i];
                sum += d * d;
            }
            double x = Math.Sqrt(sum) / (2.0 * Math.Sqrt(2.0));
            return 2.0 * Math.Asin(Math.Min(1.0, x));
        }

        /// <summary>Angular momentum in the inertial frame, kg m^2/s: H = C^T (I w).</summary>
        public static void InertialAngularMomentum(double[] cBodyFromInertial, double ixx, double iyy, double izz,
            double p, double q, double r, out double hx, out double hy, out double hz)
        {
            double bx = ixx * p, by = iyy * q, bz = izz * r;
            hx = cBodyFromInertial[0] * bx + cBodyFromInertial[3] * by + cBodyFromInertial[6] * bz;
            hy = cBodyFromInertial[1] * bx + cBodyFromInertial[4] * by + cBodyFromInertial[7] * bz;
            hz = cBodyFromInertial[2] * bx + cBodyFromInertial[5] * by + cBodyFromInertial[8] * bz;
        }

        public static double RotationalKineticEnergy(double ixx, double iyy, double izz, double p, double q, double r)
        {
            return 0.5 * (ixx * p * p + iyy * q * q + izz * r * r);
        }
    }
}
#endif
