using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using MaverickFresh.FlightDynamics.F15;

namespace MaverickFresh.FlightDynamics.Validation
{
    /// <summary>How a time-domain case's equilibrium is obtained.</summary>
    public enum MavF15TimeDomainEquilibriumKind
    {
        /// <summary>A Table VII point, recovered exactly as WP-3D did (WP-3B symmetric / WP-3C turning).</summary>
        Table = 0,

        /// <summary>A symmetric equilibrium at a given V near the pitchfork (WP-3B, seeded from point 165).</summary>
        SymmetricSpeed = 1,

        /// <summary>A turning equilibrium at a given phi near the pitchfork (WP-3C, seeded from 164 / 166).</summary>
        TurningPhi = 2
    }

    /// <summary>How the perturbation response is formed.</summary>
    public enum MavF15PerturbationScheme
    {
        /// <summary>dx = (x+ - x-)/2 from +a d and -a d: linear response plus O(a^3). The default.</summary>
        Antisymmetric = 0,

        /// <summary>dx = x+ - x_ref from +a d alone: keeps the quadratic term (a saddle-node's signature).</summary>
        PlusOnly = 1,

        /// <summary>dx = x- - x_ref from -a d alone.</summary>
        MinusOnly = 2
    }

    /// <summary>One time-domain experiment: an equilibrium, a mode, and the NUMERICAL integration settings.</summary>
    public struct MavF15TimeDomainCase
    {
        public string id;
        public string group;
        public MavF15TimeDomainEquilibriumKind kind;
        public int point;
        public double parameter;

        /// <summary>A WP-3D tracked mode id ("A+", "E", ...) read from the WP-3D dataset, or "critical" (the real eigenvalue nearest 0).</summary>
        public string mode;

        public double dt;
        public double duration;
        public int sampleEvery;
    }

    /// <summary>The result of one experiment at one amplitude and one dt.</summary>
    public struct MavF15TimeDomainMeasurement
    {
        public MavF15TimeDomainCase spec;
        public double amplitude;
        public double dt;
        public bool evaluated;
        public string reason;

        /// <summary>The WP-3D prediction, recomputed at the same equilibrium. Only compared with; never fed to the integrator.</summary>
        public double predictedRe;
        public double predictedIm;

        /// <summary>From the modal coordinate eta(t) = (u . dx(t)) / (u . v), u the left eigenvector: slope of ln|eta| and of unwrapped arg(eta).</summary>
        public double sigmaModal;
        public double omegaModal;

        /// <summary>
        /// Eigen-free, from one physical state perturbation: a real mode by the slope of ln|y|; an oscillatory one
        /// by the spacing of its zero crossings (omega) and the slope of ln|y| at its interior extrema (sigma). NaN
        /// when the window holds too few crossings or extrema.
        /// </summary>
        public double sigmaState;
        public double omegaState;
        public int zeroCrossings;
        public int extrema;
        public string dominantState;

        public double windowEnd;
        public int windowSamples;
        public bool integrationCompleted;

        /// <summary>|eta| at the window end over |eta(0)|.</summary>
        public double modalRatio;

        public double[] time;
        public double[][] perturbation;
        public Complex[] modal;
        public double[] initialPerturbation;
    }

    /// <summary>
    /// WP-3E Goal A: the WP-3D eigenvalues tested against the NONLINEAR source dynamics by direct time
    /// integration of <see cref="MavF15AfitResearchSourceDynamics"/> (unchanged) with
    /// <see cref="MavValidationRk4Integrator"/>, the stabilator held at its equilibrium value.
    ///
    /// Each experiment integrates the equilibrium plus and minus a small perturbation along one WP-3D
    /// eigenvector, and measures the growth or decay and the frequency of the antisymmetric part
    /// dx(t) = (x+(t) - x-(t)) / 2. That cancels the equilibrium's own residual drift and every
    /// even-order nonlinear term (the quadratic one dominates near a fold), leaving the linear response
    /// plus O(a^3).
    ///
    /// PERTURBATION. Real eigenvalue: dx0 = a v / m. Complex pair: dx0 = a Re(v) / m, i.e. (v + conj v)/2,
    /// a real state that excites exactly that pair. v is WP-3D's right eigenvector, scaled so its largest
    /// component is 1; m = max_i |d_i| / s_i with s = (1 rad, 1 rad, 1 rad/s x3, 1 rad, 1 rad, V0). So
    /// the amplitude a is the largest perturbed component in rad, rad/s or dV/V0.
    ///
    /// MEASUREMENT. The eigenvalue is never an input to the integration; it is only the prediction.
    /// Growth, decay and frequency are measured twice: from the modal coordinate (left eigenvector) and
    /// eigen-free from one physical state. Fits use only the local linear window: from t = 0 while the
    /// modal amplitude (in the scaled units above) stays between max(1e-2 a0, 1e-7) and 3e-3.
    ///
    /// FLOAT FLOOR. The coefficient routine takes and returns float, which quantizes x-dot by ~1e-8 /s;
    /// in a perturbation that acts like a bias of order 1e-8 / |lambda|. Slow modes (|lambda| ~ 1e-3)
    /// therefore need amplitudes >= 1e-4, fast ones reach 1e-6. Documented, never tuned away.
    ///
    /// RESEARCH SOURCE MODEL ONLY. Not the real F-15, NASA 836, the FCS or a Rigidbody.
    /// </summary>
    public static class MavF15ResearchTimeDomainStability
    {
        public const int N = MavF15ResearchSourceState.Count;

        public static readonly double[] Amplitudes = { 1e-6, 1e-5, 1e-4 };

        /// <summary>NUMERICAL integration steps, from the dt audit (analysis document §3).</summary>
        public const double FastDt = 0.01;
        public const double MediumDt = 0.05;
        public const double SlowDt = 0.1;

        public const double WindowLowerFraction = 1e-2;
        public const double WindowFloorScaled = 1e-7;
        public const double WindowUpperScaled = 3e-3;

        /// <summary>An unstable experiment stops once the scaled perturbation passes this (≈3°): far outside the fit window.</summary>
        public const double DivergenceStopScaled = 0.05;

        public const int ExportSamples = 120;

        private const double DegToRad = Math.PI / 180.0;
        private const double RadToDeg = 180.0 / Math.PI;

        // ------------------------------------------------------------------ catalogue

        /// <summary>
        /// The experiments, by the brief's groups: A stable symmetric, B CMMQ-unstable symmetric, C stable
        /// turning, D turning on the unstable side of a fold (with its stable neighbours), E limit points,
        /// F symmetric branch near the pitchfork, G turning branches near the pitchfork.
        /// </summary>
        public static List<MavF15TimeDomainCase> Catalogue()
        {
            List<MavF15TimeDomainCase> c = new List<MavF15TimeDomainCase>();

            // A: stable symmetric point 20 (alpha 15.4 deg), every mode.
            c.Add(Table("A20-A", "A", 20, "A+", FastDt, 8.0, 2));
            c.Add(Table("A20-B", "A", 20, "B+", MediumDt, 400.0, 2));
            c.Add(Table("A20-C", "A", 20, "C+", FastDt, 12.0, 2));
            c.Add(Table("A20-D", "A", 20, "D", FastDt, 15.0, 2));
            c.Add(Table("A20-E", "A", 20, "E", FastDt, 40.0, 5));

            // B: the CMMQ-positive band, and its stable neighbour across the Hopf.
            c.Add(Table("B47-A", "B", 47, "A+", FastDt, 20.0, 2));
            c.Add(Table("B5-A", "B", 5, "A+", FastDt, 25.0, 2));
            c.Add(Table("B8-A", "B", 8, "A+", FastDt, 60.0, 2));
            c.Add(Table("B9-A", "B", 9, "A+", FastDt, 60.0, 2));

            // C: stable turning point 150 (phi -21 deg), every mode but the slow E.
            c.Add(Table("C150-A", "C", 150, "A+", FastDt, 12.0, 2));
            c.Add(Table("C150-B", "C", 150, "B+", MediumDt, 400.0, 2));
            c.Add(Table("C150-C", "C", 150, "C+", FastDt, 12.0, 2));
            c.Add(Table("C150-D", "C", 150, "D", FastDt, 15.0, 2));
            c.Add(Table("C150-E", "C", 150, "E", SlowDt, 2000.0, 10));
            c.Add(Table("C117-C", "C", 117, "C+", FastDt, 10.0, 2));

            // D: the fold mode E on both sides of all four folds.
            foreach (int p in new[] { 123, 125, 130, 135, 137, 181, 183, 193, 195 })
                c.Add(Table("D" + p + "-E", "D", p, "E", SlowDt, 2000.0, 10));

            // E: the printed limit points themselves.
            foreach (int p in new[] { 124, 136, 182 })
                c.Add(Table("E" + p + "-E", "E", p, "E", SlowDt, 2000.0, 10));

            // F: the symmetric branch across the pitchfork (V stepped around the printed 377.4).
            c.Add(Special("F372.4", "F", MavF15TimeDomainEquilibriumKind.SymmetricSpeed, 372.4, SlowDt, 1500.0, 10));
            c.Add(Special("F377.3", "F", MavF15TimeDomainEquilibriumKind.SymmetricSpeed, 377.3, SlowDt, 3000.0, 10));
            c.Add(Special("F377.6", "F", MavF15TimeDomainEquilibriumKind.SymmetricSpeed, 377.6, SlowDt, 3000.0, 10));
            c.Add(Special("F382.4", "F", MavF15TimeDomainEquilibriumKind.SymmetricSpeed, 382.4, SlowDt, 1500.0, 10));

            // G: both turning branches near the pitchfork.
            foreach (double phi in new[] { 20.0, -20.0, 2.0, -2.0 })
                c.Add(Special("G" + phi.ToString("+0;-0", CultureInfo.InvariantCulture), "G", MavF15TimeDomainEquilibriumKind.TurningPhi, phi, SlowDt, 3000.0, 10));

            return c;
        }

        private static MavF15TimeDomainCase Table(string id, string group, int point, string mode, double dt, double duration, int every)
        {
            return new MavF15TimeDomainCase
            {
                id = id, group = group, kind = MavF15TimeDomainEquilibriumKind.Table, point = point, mode = mode,
                dt = dt, duration = duration, sampleEvery = every
            };
        }

        private static MavF15TimeDomainCase Special(string id, string group, MavF15TimeDomainEquilibriumKind kind, double parameter,
            double dt, double duration, int every)
        {
            return new MavF15TimeDomainCase
            {
                id = id, group = group, kind = kind, parameter = parameter, mode = "critical",
                dt = dt, duration = duration, sampleEvery = every
            };
        }

        // ------------------------------------------------------------------ equilibria and modes

        public static MavF15ResearchEquilibrium Equilibrium(MavF15TimeDomainCase c)
        {
            MavF15TableViiState s;
            switch (c.kind)
            {
                case MavF15TimeDomainEquilibriumKind.SymmetricSpeed:
                    MavF15TableViiTurningRecovery.TryGetState(MavF15TableViiTurningRecovery.PitchforkPoint(), out s);
                    return MavF15ResearchStabilityAnalysis.Symmetric(s, c.parameter, MavF15ResearchEquilibriumBranch.Symmetric);

                case MavF15TimeDomainEquilibriumKind.TurningPhi:
                    int fork = MavF15TableViiTurningRecovery.PitchforkPoint();
                    MavF15TableViiTurningRecovery.TryGetState(fork - 1, out s);
                    if (Math.Sign(s.phiDeg) != Math.Sign(c.parameter))
                        MavF15TableViiTurningRecovery.TryGetState(fork + 1, out s);
                    return MavF15ResearchStabilityAnalysis.Turning(s, c.parameter);

                default:
                    foreach (MavF15TableViiState sym in MavF15TableViiTrimRecovery.SymmetricStates())
                    {
                        if (sym.part1Point == c.point)
                            return MavF15ResearchStabilityAnalysis.Symmetric(sym, sym.trueVelocityFtPerSec, MavF15ResearchEquilibriumBranch.Symmetric);
                    }

                    MavF15TableViiTurningRecovery.TryGetState(c.point, out s);
                    return c.point == MavF15TableViiTurningRecovery.PitchforkPoint()
                        ? MavF15ResearchStabilityAnalysis.Symmetric(s, s.trueVelocityFtPerSec, MavF15ResearchEquilibriumBranch.Pitchfork)
                        : MavF15ResearchStabilityAnalysis.Turning(s, s.phiDeg);
            }
        }

        /// <summary>
        /// The eigenvalue the case names, in a fresh WP-3D linearization of its equilibrium: the one nearest
        /// the WP-3D dataset's value for (point, mode), or the real eigenvalue nearest 0 for "critical".
        /// </summary>
        public static bool SelectMode(MavF15TimeDomainCase c, MavF15ResearchLinearization lin, out double re, out double im, out string reason)
        {
            re = im = double.NaN;
            reason = null;
            if (!lin.evaluated)
            {
                reason = "not linearized: " + lin.reason;
                return false;
            }

            if (c.mode == "critical")
            {
                MavF15ResearchMode m = MavF15ResearchStabilityAnalysis.CriticalMode(lin);
                re = m.re;
                im = 0.0;
                return true;
            }

            double dr, di;
            if (!DatasetEigenvalue(c.point, c.mode, out dr, out di))
            {
                reason = "no WP-3D dataset entry for point " + c.point + " mode " + c.mode;
                return false;
            }

            double best = double.PositiveInfinity;
            foreach (MavF15ResearchMode m in lin.modes)
            {
                double d = MavF15ResearchStabilityAnalysis.Distance(m.re, m.im, dr, di);
                if (d < best)
                {
                    best = d;
                    re = m.re;
                    im = m.im;
                }
            }

            return true;
        }

        // ------------------------------------------------------------------ one experiment

        public static MavValidationDerivative SourceRhs(double stabilatorDeg)
        {
            return delegate (double[] x, double[] xDot)
            {
                return MavF15AfitResearchSourceDynamics.EvaluateStateDerivative(x, stabilatorDeg, xDot);
            };
        }

        /// <summary>Runs one case at one amplitude with the case's dt, or an override (0 = the case's).</summary>
        public static MavF15TimeDomainMeasurement Run(string configurationId, MavF15TimeDomainCase c, double amplitude, double dtOverride)
        {
            return Run(configurationId, c, amplitude, dtOverride, MavF15PerturbationScheme.Antisymmetric);
        }

        public static MavF15TimeDomainMeasurement Run(string configurationId, MavF15TimeDomainCase c, double amplitude, double dtOverride,
            MavF15PerturbationScheme scheme)
        {
            MavF15TimeDomainMeasurement r = new MavF15TimeDomainMeasurement
            {
                spec = c,
                amplitude = amplitude,
                dt = dtOverride > 0.0 ? dtOverride : c.dt,
                sigmaModal = double.NaN,
                omegaModal = double.NaN,
                sigmaState = double.NaN,
                omegaState = double.NaN
            };

            if (configurationId != MavF15AfitResearchIdentity.ConfigurationId)
            {
                r.reason = "research time-domain stability is for " + MavF15AfitResearchIdentity.ConfigurationId + " only; '"
                           + configurationId + "' has no research source dynamics";
                return r;
            }

            MavF15ResearchEquilibrium eq = Equilibrium(c);
            if (!eq.recovered)
            {
                r.reason = "equilibrium not recovered: " + eq.note;
                return r;
            }

            MavF15ResearchLinearization lin = MavF15ResearchStabilityAnalysis.Core(configurationId, eq);
            double re, im;
            string why;
            if (!SelectMode(c, lin, out re, out im, out why))
            {
                r.reason = why;
                return r;
            }

            r.predictedRe = re;
            r.predictedIm = im;

            // Right eigenvector v of A, left eigenvector as the right eigenvector of A^T (same lambda).
            double[] vr, vi, ur, ui;
            double res1, res2;
            double[,] at = Transpose(lin.jacobian);
            if (!MavValidationEigenSolver.Eigenvector(lin.jacobian, re, im, out vr, out vi, out res1)
                || !MavValidationEigenSolver.Eigenvector(at, re, im, out ur, out ui, out res2))
            {
                r.reason = "eigenvector not found";
                return r;
            }

            Complex[] v = new Complex[N], u = new Complex[N];
            for (int i = 0; i < N; i++)
            {
                v[i] = new Complex(vr[i], im != 0.0 ? vi[i] : 0.0);
                u[i] = new Complex(ur[i], im != 0.0 ? ui[i] : 0.0);
            }

            Complex uv = Dot(u, v);
            double[] x0 = eq.state.ToArray();
            double[] scale = Scale(x0[7]);
            double[] d = new double[N];
            double m = 0.0;
            int dominant = 0;
            for (int i = 0; i < N; i++)
            {
                d[i] = v[i].Real;
                double s = Math.Abs(d[i]) / scale[i];
                if (s > m)
                {
                    m = s;
                    dominant = i;
                }
            }

            double[] dx0 = new double[N];
            double[] xp = new double[N], xm = new double[N];
            for (int i = 0; i < N; i++)
            {
                dx0[i] = amplitude * d[i] / m;
                xp[i] = x0[i] + dx0[i];
                xm[i] = x0[i] - dx0[i];
            }

            r.initialPerturbation = dx0;
            r.dominantState = MavF15AfitResearchSourceDynamics.StateNames[dominant];

            double dt = r.dt;
            int steps = (int)Math.Round(c.duration / dt);
            int every = Math.Max(1, (int)Math.Round(c.sampleEvery * c.dt / dt));
            MavValidationDerivative f = SourceRhs(eq.stabilatorDeg);
            MavValidationTrajectory plus, minus;
            if (scheme == MavF15PerturbationScheme.Antisymmetric)
            {
                plus = IntegrateUntilDivergence(f, xp, x0, dt, steps, every, scale);
                minus = IntegrateUntilDivergence(f, xm, x0, dt, steps, every, scale);
            }
            else
            {
                // One-sided: the response is x_s - x_ref, written as (plus - minus)/2 with plus = x_s,
                // minus = 2 x_ref - x_s, so the same measurement code applies.
                MavValidationTrajectory side = IntegrateUntilDivergence(f, scheme == MavF15PerturbationScheme.PlusOnly ? xp : xm,
                    x0, dt, steps, every, scale);
                MavValidationTrajectory reference = MavValidationRk4Integrator.Integrate(f, x0, dt, steps, every);
                int count = Math.Min(side.time.Length, reference.time.Length);
                plus = new MavValidationTrajectory { completed = side.completed, reason = side.reason, time = new double[count], state = new double[count][] };
                minus = new MavValidationTrajectory { completed = reference.completed, reason = reference.reason, time = new double[count], state = new double[count][] };
                for (int k = 0; k < count; k++)
                {
                    plus.time[k] = minus.time[k] = side.time[k];
                    plus.state[k] = side.state[k];
                    minus.state[k] = new double[N];
                    for (int i = 0; i < N; i++)
                        minus.state[k][i] = 2.0 * reference.state[k][i] - side.state[k][i];
                }
            }
            r.integrationCompleted = plus.completed && minus.completed;
            int samples = Math.Min(plus.time.Length, minus.time.Length);
            if (samples < 3)
            {
                r.reason = "trajectory too short: " + plus.reason + " / " + minus.reason;
                return r;
            }

            r.time = new double[samples];
            r.perturbation = new double[samples][];
            r.modal = new Complex[samples];
            for (int k = 0; k < samples; k++)
            {
                r.time[k] = plus.time[k];
                double[] dx = new double[N];
                for (int i = 0; i < N; i++)
                    dx[i] = 0.5 * (plus.state[k][i] - minus.state[k][i]);
                r.perturbation[k] = dx;
                r.modal[k] = Dot(u, dx) / uv;
            }

            Measure(ref r, scale, dominant, amplitude);
            r.evaluated = true;
            r.reason = "evaluated";
            return r;
        }

        /// <summary>Samples-long RK4 that stops once the perturbation from the reference state x0 is far outside the linear window.</summary>
        private static MavValidationTrajectory IntegrateUntilDivergence(MavValidationDerivative f, double[] xp, double[] x0, double dt,
            int steps, int every, double[] scale)
        {
            // Integrate in blocks of one sample so the divergence test sees every sample; still pure RK4.
            List<double> times = new List<double>();
            List<double[]> states = new List<double[]>();
            times.Add(0.0);
            states.Add((double[])xp.Clone());
            double[] x = (double[])xp.Clone();
            bool completed = true;
            string reason = "completed";
            for (int k = 1; k * every <= steps; k++)
            {
                MavValidationTrajectory block = MavValidationRk4Integrator.Integrate(f, x, dt, every, every);
                if (!block.completed)
                {
                    completed = false;
                    reason = block.reason;
                    break;
                }

                x = block.state[block.state.Length - 1];
                times.Add(k * every * dt);
                states.Add((double[])x.Clone());
                double far = 0.0;
                for (int i = 0; i < N; i++)
                    far = Math.Max(far, Math.Abs(x[i] - x0[i]) / scale[i]);
                if (far > DivergenceStopScaled)
                {
                    reason = "stopped: perturbation passed " + DivergenceStopScaled.ToString("G2") + " (scaled) at t = " + (k * every * dt).ToString("G5") + " s";
                    break;
                }
            }

            return new MavValidationTrajectory
            {
                completed = completed,
                reason = reason,
                dt = dt,
                steps = steps,
                sampleEvery = every,
                time = times.ToArray(),
                state = states.ToArray()
            };
        }

        private static void Measure(ref MavF15TimeDomainMeasurement r, double[] scale, int dominant, double amplitude)
        {
            int n = r.time.Length;
            double eta0 = r.modal[0].Magnitude;
            if (eta0 == 0.0)
                return;

            // Modal amplitude in the scaled units of the perturbation: equals the amplitude at t = 0.
            double k = amplitude / eta0;
            double lower = Math.Max(WindowLowerFraction * amplitude, WindowFloorScaled);
            int end = 0;
            for (int i = 0; i < n; i++)
            {
                double a = r.modal[i].Magnitude * k;
                if (a < lower || a > WindowUpperScaled)
                    break;
                end = i;
            }

            r.windowSamples = end + 1;
            r.windowEnd = r.time[end];
            r.modalRatio = r.modal[end].Magnitude / eta0;
            if (end < 3)
                return;

            // Modal fits.
            double[] t = new double[end + 1], ln = new double[end + 1], ph = new double[end + 1];
            double previous = 0.0, unwrap = 0.0;
            for (int i = 0; i <= end; i++)
            {
                t[i] = r.time[i];
                ln[i] = Math.Log(r.modal[i].Magnitude);
                double phase = Math.Atan2(r.modal[i].Imaginary, r.modal[i].Real);
                if (i > 0)
                {
                    double step = phase - previous;
                    while (step > Math.PI) step -= 2.0 * Math.PI;
                    while (step < -Math.PI) step += 2.0 * Math.PI;
                    unwrap += step;
                }
                else
                {
                    unwrap = phase;
                }

                previous = phase;
                ph[i] = unwrap;
            }

            r.sigmaModal = Slope(t, ln, end + 1);
            r.omegaModal = r.predictedIm != 0.0 ? Slope(t, ph, end + 1) : 0.0;

            // Eigen-free: one physical state perturbation.
            double[] y = new double[end + 1];
            for (int i = 0; i <= end; i++)
                y[i] = r.perturbation[i][dominant] / scale[dominant];

            if (r.predictedIm == 0.0)
            {
                int last = end;
                for (int i = 1; i <= end; i++)
                {
                    if (Math.Sign(y[i]) != Math.Sign(y[0]))
                    {
                        last = i - 1;
                        break;
                    }
                }

                double[] ly = new double[last + 1];
                for (int i = 0; i <= last; i++)
                    ly[i] = Math.Log(Math.Abs(y[i]));
                r.sigmaState = last >= 3 ? Slope(t, ly, last + 1) : double.NaN;
                r.omegaState = 0.0;
                return;
            }

            List<double> crossings = new List<double>();
            for (int i = 1; i <= end; i++)
            {
                if ((y[i - 1] < 0.0 && y[i] >= 0.0) || (y[i - 1] > 0.0 && y[i] <= 0.0))
                {
                    double f = y[i - 1] / (y[i - 1] - y[i]);
                    crossings.Add(t[i - 1] + f * (t[i] - t[i - 1]));
                }
            }

            r.zeroCrossings = crossings.Count;
            if (crossings.Count >= 2)
                r.omegaState = Math.PI * (crossings.Count - 1) / (crossings[crossings.Count - 1] - crossings[0]);

            // Interior extrema of |y| (never at a window edge), refined by a parabola through three samples.
            List<double> te = new List<double>(), le = new List<double>();
            for (int i = 1; i < end; i++)
            {
                double a = Math.Abs(y[i - 1]), b = Math.Abs(y[i]), cc = Math.Abs(y[i + 1]);
                if (b > a && b >= cc && Math.Sign(y[i - 1]) == Math.Sign(y[i]) && Math.Sign(y[i + 1]) == Math.Sign(y[i]))
                {
                    double denominator = a - 2.0 * b + cc;
                    double offset = denominator != 0.0 ? 0.5 * (a - cc) / denominator : 0.0;
                    double h = t[i] - t[i - 1];
                    te.Add(t[i] + offset * h);
                    le.Add(Math.Log(b - 0.25 * (a - cc) * offset));
                }
            }

            r.extrema = te.Count;
            if (te.Count >= 2)
                r.sigmaState = Slope(te.ToArray(), le.ToArray(), te.Count);
        }

        // ------------------------------------------------------------------ symmetry breaking

        /// <summary>One nonlinear run from a symmetric equilibrium perturbed along its symmetry-breaking mode.</summary>
        public struct SymmetryBreakingRun
        {
            public double sign;
            public bool completed;
            public string reason;
            public double[] time;
            public double[][] state;

            /// <summary>At the first sample with |phi| >= 1 deg: time, phi, beta, r and heading rate.</summary>
            public double departureTime;
            public double departurePhiDeg;
            public double departureBetaDeg;
            public double departureRRadSec;
            public double departureHeadingRateRadSec;

            /// <summary>At the last sample: phi, V, alpha and the largest scaled |x-dot| (small = settling on an equilibrium).</summary>
            public double finalPhiDeg;
            public double finalSpeedFtPerSec;
            public double finalAlphaDeg;
            public double finalHeadingRateRadSec;
            public double finalDerivativeScaled;
        }

        public const double SymmetryBreakingSpeedFtPerSec = 382.4;
        public const double SymmetryBreakingAmplitude = 1e-4;
        public const double SymmetryBreakingDuration = 6000.0;
        public const double SymmetryBreakingDt = 0.05;

        /// <summary>
        /// Perturbs the symmetric equilibrium at <paramref name="speedFtPerSec"/> (the unstable side of the fork)
        /// by +a and -a along its critical real (lateral) eigenvector and integrates the full nonlinear source
        /// dynamics. Reports only what the trajectories do; no convergence is assumed.
        /// </summary>
        public static SymmetryBreakingRun[] SymmetryBreaking(string configurationId, double speedFtPerSec, double amplitude,
            double duration, double dt, out MavF15ResearchEquilibrium eq, out double criticalRe)
        {
            MavF15TimeDomainCase c = Special("SB", "F", MavF15TimeDomainEquilibriumKind.SymmetricSpeed, speedFtPerSec, dt, duration, 20);
            eq = Equilibrium(c);
            criticalRe = double.NaN;
            SymmetryBreakingRun[] runs = new SymmetryBreakingRun[2];
            if (configurationId != MavF15AfitResearchIdentity.ConfigurationId || !eq.recovered)
                return runs;

            MavF15ResearchLinearization lin = MavF15ResearchStabilityAnalysis.Core(configurationId, eq);
            MavF15ResearchMode m = MavF15ResearchStabilityAnalysis.CriticalMode(lin);
            criticalRe = m.re;
            double[] vr, vi;
            double residual;
            MavValidationEigenSolver.Eigenvector(lin.jacobian, m.re, 0.0, out vr, out vi, out residual);
            double[] x0 = eq.state.ToArray();
            double[] scale = Scale(x0[7]);
            double mx = 0.0;
            for (int i = 0; i < N; i++)
                mx = Math.Max(mx, Math.Abs(vr[i]) / scale[i]);

            // Orient the eigenvector so +a gives positive phi, whatever sign the solver returned.
            double orient = vr[6] >= 0.0 ? 1.0 : -1.0;
            MavValidationDerivative f = SourceRhs(eq.stabilatorDeg);
            int steps = (int)Math.Round(duration / dt);
            for (int s = 0; s < 2; s++)
            {
                double sign = s == 0 ? 1.0 : -1.0;
                double[] xs = new double[N];
                for (int i = 0; i < N; i++)
                    xs[i] = x0[i] + sign * orient * amplitude * vr[i] / mx;
                MavValidationTrajectory t = MavValidationRk4Integrator.Integrate(f, xs, dt, steps, 20);
                SymmetryBreakingRun r = new SymmetryBreakingRun
                {
                    sign = sign, completed = t.completed, reason = t.reason, time = t.time, state = t.state,
                    departureTime = double.NaN
                };

                for (int k = 0; k < t.time.Length; k++)
                {
                    double[] x = t.state[k];
                    if (Math.Abs(x[6]) * RadToDeg >= 1.0)
                    {
                        r.departureTime = t.time[k];
                        r.departurePhiDeg = x[6] * RadToDeg;
                        r.departureBetaDeg = x[1] * RadToDeg;
                        r.departureRRadSec = x[4];
                        r.departureHeadingRateRadSec = HeadingRate(x);
                        break;
                    }
                }

                double[] xe = t.state[t.state.Length - 1];
                double[] xd = new double[N];
                f(xe, xd);
                double far = 0.0;
                double[] se = Scale(xe[7]);
                for (int i = 0; i < N; i++)
                    far = Math.Max(far, Math.Abs(xd[i]) / se[i]);
                r.finalPhiDeg = xe[6] * RadToDeg;
                r.finalSpeedFtPerSec = xe[7];
                r.finalAlphaDeg = xe[0] * RadToDeg;
                r.finalHeadingRateRadSec = HeadingRate(xe);
                r.finalDerivativeScaled = far;
                runs[s] = r;
            }

            return runs;
        }

        public static double HeadingRate(double[] x)
        {
            return (x[3] * Math.Sin(x[6]) + x[4] * Math.Cos(x[6])) / Math.Cos(x[5]);
        }

        // ------------------------------------------------------------------ nonlinear mirror

        public const int MirrorPoint = 150;
        public const double MirrorDuration = 60.0;
        public const double MirrorDt = 0.01;

        /// <summary>
        /// A deliberately finite (nonlinear) perturbation, in physical units: alpha +0.5 deg, beta +0.3 deg,
        /// p +0.02, q -0.01, r +0.01 rad/s, theta -0.3 deg, phi +2 deg, V +5 ft/s.
        /// </summary>
        public static double[] MirrorPerturbation()
        {
            return new[] { 0.5 * DegToRad, 0.3 * DegToRad, 0.02, -0.01, 0.01, -0.3 * DegToRad, 2.0 * DegToRad, 5.0 };
        }

        /// <summary>+1 for the even states (alpha, q, theta, V), -1 for the odd ones (beta, p, r, phi).</summary>
        public static readonly double[] Parity = { 1.0, -1.0, -1.0, 1.0, -1.0, 1.0, -1.0, 1.0 };

        /// <summary>
        /// Integrates a turning equilibrium plus a finite perturbation, and its mirror image plus the mirrored
        /// perturbation, and returns the largest |x_mirror(t) - S x(t)| per state over the run (physical units),
        /// with the largest excursion of each state from its equilibrium for scale.
        /// </summary>
        public static bool MirrorTrajectories(string configurationId, out double[] largestGap, out double[] largestExcursion,
            out bool bitwise, out MavValidationTrajectory original, out MavValidationTrajectory mirrored)
        {
            largestGap = new double[N];
            largestExcursion = new double[N];
            bitwise = false;
            original = mirrored = null;
            if (configurationId != MavF15AfitResearchIdentity.ConfigurationId)
                return false;

            MavF15ResearchEquilibrium eq = Equilibrium(Table("M", "M", MirrorPoint, "E", MirrorDt, MirrorDuration, 10));
            if (!eq.recovered)
                return false;

            double[] x0 = eq.state.ToArray();
            double[] dp = MirrorPerturbation();
            double[] xa = new double[N], xb = new double[N];
            for (int i = 0; i < N; i++)
            {
                xa[i] = x0[i] + dp[i];
                xb[i] = Parity[i] * x0[i] + Parity[i] * dp[i];
            }

            MavValidationDerivative f = SourceRhs(eq.stabilatorDeg);
            int steps = (int)Math.Round(MirrorDuration / MirrorDt);
            original = MavValidationRk4Integrator.Integrate(f, xa, MirrorDt, steps, 10);
            mirrored = MavValidationRk4Integrator.Integrate(f, xb, MirrorDt, steps, 10);
            if (!original.completed || !mirrored.completed)
                return false;

            bitwise = true;
            for (int k = 0; k < original.time.Length; k++)
            {
                for (int i = 0; i < N; i++)
                {
                    double gap = Math.Abs(mirrored.state[k][i] - Parity[i] * original.state[k][i]);
                    if (gap != 0.0)
                        bitwise = false;
                    largestGap[i] = Math.Max(largestGap[i], gap);
                    largestExcursion[i] = Math.Max(largestExcursion[i], Math.Abs(original.state[k][i] - x0[i]));
                }
            }

            return true;
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>1 rad, 1 rad, 1 rad/s x3, 1 rad, 1 rad, V0: the units the amplitude is stated in.</summary>
        public static double[] Scale(double speedFtPerSec)
        {
            return new[] { 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, speedFtPerSec };
        }

        public static double Slope(double[] x, double[] y, int n)
        {
            double mx = 0.0, my = 0.0;
            for (int i = 0; i < n; i++)
            {
                mx += x[i];
                my += y[i];
            }

            mx /= n;
            my /= n;
            double sxy = 0.0, sxx = 0.0;
            for (int i = 0; i < n; i++)
            {
                sxy += (x[i] - mx) * (y[i] - my);
                sxx += (x[i] - mx) * (x[i] - mx);
            }

            return sxx > 0.0 ? sxy / sxx : double.NaN;
        }

        private static Complex Dot(Complex[] a, Complex[] b)
        {
            Complex s = Complex.Zero;
            for (int i = 0; i < a.Length; i++)
                s += a[i] * b[i];
            return s;
        }

        private static Complex Dot(Complex[] a, double[] b)
        {
            Complex s = Complex.Zero;
            for (int i = 0; i < a.Length; i++)
                s += a[i] * b[i];
            return s;
        }

        public static double[,] Transpose(double[,] a)
        {
            int n = a.GetLength(0);
            double[,] t = new double[n, n];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                    t[j, i] = a[i, j];
            }

            return t;
        }

        // ------------------------------------------------------------------ WP-3D dataset

        private static Dictionary<string, double[]> dataset;

        /// <summary>The WP-3D dataset's eigenvalue for (point, mode), read from the committed CSV.</summary>
        public static bool DatasetEigenvalue(int point, string mode, out double re, out double im)
        {
            re = im = double.NaN;
            Dictionary<string, double[]> d = LoadDataset();
            double[] v;
            if (d == null || !d.TryGetValue(point + "/" + mode, out v))
                return false;
            re = v[0];
            im = v[1];
            return true;
        }

        public static string DatasetPath()
        {
            string root = ProjectRoot();
            return root == null ? null : Path.Combine(root, Path.Combine("Docs", Path.Combine("Reference", Path.Combine("Data",
                Path.Combine("F15", Path.Combine("stability", "f15_research_stability_table_vii.csv"))))));
        }

        private static Dictionary<string, double[]> LoadDataset()
        {
            if (dataset != null)
                return dataset;
            string path = DatasetPath();
            if (path == null || !File.Exists(path))
                return null;

            Dictionary<string, double[]> d = new Dictionary<string, double[]>();
            string[] lines = File.ReadAllLines(path);
            string[] header = lines[0].Split(',');
            int cp = Array.IndexOf(header, "point"), cm = Array.IndexOf(header, "mode");
            int cr = Array.IndexOf(header, "real_per_s"), ci = Array.IndexOf(header, "imag_rad_s");
            for (int i = 1; i < lines.Length; i++)
            {
                string[] f = lines[i].Split(',');
                if (f.Length <= Math.Max(Math.Max(cp, cm), Math.Max(cr, ci)))
                    continue;
                d[f[cp] + "/" + f[cm]] = new[]
                {
                    double.Parse(f[cr], CultureInfo.InvariantCulture), double.Parse(f[ci], CultureInfo.InvariantCulture)
                };
            }

            dataset = d;
            return d;
        }

        /// <summary>The project root (the folder holding Assets/ and Docs/), or null.</summary>
        public static string ProjectRoot()
        {
            try
            {
                string fromUnity = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
                if (Directory.Exists(Path.Combine(fromUnity, "Docs")))
                    return fromUnity;
            }
            catch (Exception)
            {
                // No Unity player loaded. Fall through to the directory walk.
            }

            DirectoryInfo dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            for (int depth = 0; depth < 8 && dir != null; depth++)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "Assets")) && Directory.Exists(Path.Combine(dir.FullName, "Docs")))
                    return dir.FullName;
                dir = dir.Parent;
            }

            return null;
        }
    }
}
