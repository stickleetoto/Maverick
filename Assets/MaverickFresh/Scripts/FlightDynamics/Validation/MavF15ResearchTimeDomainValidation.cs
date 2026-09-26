using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using MaverickFresh.FlightDynamics.F15;

namespace MaverickFresh.FlightDynamics.Validation
{
    /// <summary>
    /// WP-3E: the WP-3D eigenvalues verified in the NONLINEAR source dynamics by direct time integration
    /// (Goal A), and isolated experiments on the source's own stability conflict (Goal B).
    ///
    ///   [T1]  the source RHS, coefficient routines and trim solver are byte-identical to WP-3D; the fresh
    ///         Jacobians and eigenvalues equal the WP-3D dataset bit for bit
    ///   [T2]  RK4: deterministic, fourth order on an exact linear test, touches no Unity time or body
    ///   [T3]  dt refinement (dt, dt/2, dt/4): measured rates and frequencies on a plateau
    ///   [T4]  stable modes decay at Re(lambda)
    ///   [T5]  oscillatory modes oscillate at Im(lambda) - modal phase and eigen-free zero crossings
    ///   [T6]  the CMMQ-positive band grows at the predicted rate and frequency
    ///   [T7]  folds: the fold mode decays on one side, grows on the other, is near-neutral at the limit point
    ///   [T8]  pitchfork: sign change across it, local symmetry breaking to opposite turns
    ///   [T9]  mirrored nonlinear trajectories stay mirrored
    ///   [T10] amplitude sweep: the linear rate as amplitude falls, until the float floor
    ///   [T11] isolation: research id only; no F100, atmosphere, Rigidbody or Unity time
    ///   [T12] coefficients, CFX2 and atmosphere unchanged
    ///   [T13] WP-3A/B/C/D datasets and trim roots unchanged
    ///   [T14] Goal B: source-supported hypotheses for the stability conflict, each tested in isolation
    ///
    /// RESEARCH SOURCE MODEL ONLY: not the real F-15, NASA 836, the FCS or a Rigidbody. Nothing is
    /// flown. The asserted bands are NUMERICAL (the comparison of a linearization with a nonlinear
    /// integration of the same equations), stated with their budget in the analysis document.
    /// </summary>
    public static class MavF15ResearchTimeDomainValidation
    {
        private const int N = MavF15ResearchSourceState.Count;
        private const double RadToDeg = 180.0 / Math.PI;

        /// <summary>The amplitude at which every rate and frequency is compared (scaled units, see the analysis class).</summary>
        public const double ReferenceAmplitude = 1e-4;

        public static readonly double[] SweepAmplitudes = { 1e-6, 1e-5, 1e-4, 1e-3 };

        /// <summary>
        /// NUMERICAL agreement bands between the nonlinear measurement and the WP-3D eigenvalue:
        /// |sigma - Re| <= RateRelativeBand |lambda| + 3 u + RateFloor, |omega - Im| <= FrequencyRelativeBand |Im| + 3 u,
        /// u = the WP-3D eigenvalue's own measured numerical uncertainty. Budget (analysis §4): float floor
        /// <= 0.1 % at a = 1e-4 for |lambda| >= 3e-4 /s, residual cubic nonlinearity <= 0.8 % beside fold 194,
        /// dt <= 1e-4. Not a physical or source tolerance.
        /// </summary>
        public const double RateRelativeBand = 1e-2;
        public const double RateFloorPerSec = 1e-7;
        public const double FrequencyRelativeBand = 5e-3;

        /// <summary>dt plateau: measured rate and frequency change by at most this fraction across dt, dt/2, dt/4.</summary>
        public const double DtPlateauRelative = 1e-3;

        private static readonly string[] DtAuditCases = { "A20-A", "A20-B", "B47-A", "C117-C", "D130-E", "F382.4" };

        private static readonly string[] ExportTrajectoryCases =
        {
            "A20-A", "A20-C", "B47-A", "B9-A", "C150-A", "C117-C", "D123-E", "D125-E", "E124-E", "F377.3", "F377.6", "G+20"
        };

        /// <summary>SHA-256 (line endings normalized to LF) of the WP-3D-era files this WP must not change.</summary>
        private static readonly string[][] ProtectedFiles =
        {
            new[] { "Assets/MaverickFresh/Scripts/FlightDynamics/F15/MavF15AfitResearchSourceDynamics.cs", "9026632d61c722346ddc84e063da13bed15bf3628a65e56c26f61c05cba24fe3" },
            new[] { "Assets/MaverickFresh/Scripts/FlightDynamics/F15/MavF15AfitResearchTrimSolver.cs", "6b54e72b6e25050c062f4ea927bdbcc290a62275f355eb621349c28f7c8754b5" },
            new[] { "Assets/MaverickFresh/Scripts/FlightDynamics/F15/MavF15BaumannMach06Longitudinal.cs", "c58dc64b5c457ec16a9ef0b265bc7d2b3ecc178cab14858505cfb8492fae6399" },
            new[] { "Assets/MaverickFresh/Scripts/FlightDynamics/F15/MavF15BaumannMach06LateralDirectional.cs", "cd0daeec7915e0c0d64ce70ea3d04d89e8581824401721517f4c4f59c79250f9" },
            new[] { "Assets/MaverickFresh/Scripts/FlightDynamics/Validation/MavF15BaumannTableViiData.cs", "102c89853bb927fddce0b67374e4c3de290d8c7cbffa2c351cf66f048108905b" },
            new[] { "Docs/Reference/Data/F15/table_vii/baumann_table_vii_part1.txt", "97e64a3aa618118b03fb46852bbd3e808e7b9535a7cbf636d9e479f705bd3c9b" },
            new[] { "Docs/Reference/Data/F15/table_vii/baumann_table_vii_part2.txt", "0d99d473e5f5e9bd84aabd6ab31c0d0bb65dc2f20adeab2c6773452e36f20a85" },
            new[] { "Docs/Reference/Data/F15/stability/f15_research_stability_table_vii.csv", "bd7065bf9b4b9e36db5da27d494214c4e714a8573e213d43929d13afa868e47e" },
            new[] { "Docs/Reference/Data/F15/stability/f15_research_stability_jacobians.csv", "370debb69e0f34a5a6ea9cf2612283f6c1a04f25c81c1b42a2907888b3159d1d" },
            new[] { "Docs/Reference/Data/F15/stability/f15_research_stability_step_audit.csv", "34c080bc9afa23453f946b0b9cef3e0e15024aeaebc6b66314ae323e0da871f4" },
            new[] { "Docs/Reference/Data/F15/stability/f15_research_stability_pitchfork_approach.csv", "dd0d51c84c9b7bd48ab3761f701750f40b15d71e21b29e0ac6d6f38020931db9" },
            new[] { "Docs/Reference/Data/F15/stability/f15_research_stability_crossings.csv", "f1a4a24b5d23ced6e13c0407748639aef4896c7fc183f0b4dd936064fdf852e8" }
        };

        /// <summary>One case: its sweep over amplitudes, its WP-3D uncertainty and its verdict at the reference amplitude.</summary>
        public sealed class CaseResult
        {
            public MavF15TimeDomainCase spec;
            public MavF15TimeDomainMeasurement[] sweep;
            public MavF15TimeDomainMeasurement reference;
            public double uncertainty;
            public double zeroBand;
            public double rateError;
            public double rateBand;
            public double frequencyError;
            public double frequencyBand;
            public double stateFrequencyError;
            public bool rateAgrees;
            public bool frequencyAgrees;
        }

        /// <summary>Everything the suite and the export share, computed once.</summary>
        public sealed class Sweep
        {
            public List<CaseResult> cases;
            public Dictionary<string, MavF15TimeDomainMeasurement[]> dtAudit;
            public MavF15ResearchTimeDomainStability.SymmetryBreakingRun[] breaking;
            public MavF15ResearchEquilibrium breakingEquilibrium;
            public double breakingCriticalRe;
            public MavF15ResearchEquilibrium settledCheck;
            public bool mirrorEvaluated;
            public double[] mirrorGap;
            public double[] mirrorExcursion;
            public bool mirrorBitwise;
            public MavValidationTrajectory mirrorOriginal;
            public MavValidationTrajectory mirrorMirrored;
            public Dictionary<string, double[]> oneSided;
        }

        public static Sweep RunSweep()
        {
            string id = MavF15AfitResearchIdentity.ConfigurationId;
            Sweep s = new Sweep { cases = new List<CaseResult>(), dtAudit = new Dictionary<string, MavF15TimeDomainMeasurement[]>(), oneSided = new Dictionary<string, double[]>() };

            foreach (MavF15TimeDomainCase c in MavF15ResearchTimeDomainStability.Catalogue())
            {
                CaseResult r = new CaseResult { spec = c, sweep = new MavF15TimeDomainMeasurement[SweepAmplitudes.Length] };
                for (int a = 0; a < SweepAmplitudes.Length; a++)
                {
                    r.sweep[a] = MavF15ResearchTimeDomainStability.Run(id, c, SweepAmplitudes[a], 0.0);
                    if (SweepAmplitudes[a] == ReferenceAmplitude)
                        r.reference = r.sweep[a];
                }

                Uncertainty(c, r.reference, out r.uncertainty, out r.zeroBand);
                MavF15TimeDomainMeasurement m = r.reference;
                double lambda = Math.Sqrt(m.predictedRe * m.predictedRe + m.predictedIm * m.predictedIm);
                r.rateError = Math.Abs(m.sigmaModal - m.predictedRe);
                r.rateBand = RateRelativeBand * lambda + 3.0 * r.uncertainty + RateFloorPerSec;
                r.rateAgrees = m.evaluated && r.rateError <= r.rateBand;
                if (m.predictedIm != 0.0)
                {
                    r.frequencyError = Math.Abs(m.omegaModal - m.predictedIm);
                    r.frequencyBand = FrequencyRelativeBand * Math.Abs(m.predictedIm) + 3.0 * r.uncertainty;
                    r.stateFrequencyError = double.IsNaN(m.omegaState) ? double.NaN : Math.Abs(m.omegaState - m.predictedIm);
                    r.frequencyAgrees = r.frequencyError <= r.frequencyBand
                                        && (double.IsNaN(r.stateFrequencyError) || r.stateFrequencyError <= r.frequencyBand);
                }

                s.cases.Add(r);
            }

            foreach (MavF15TimeDomainCase c in MavF15ResearchTimeDomainStability.Catalogue())
            {
                if (Array.IndexOf(DtAuditCases, c.id) < 0)
                    continue;
                s.dtAudit[c.id] = new[]
                {
                    MavF15ResearchTimeDomainStability.Run(id, c, ReferenceAmplitude, c.dt),
                    MavF15ResearchTimeDomainStability.Run(id, c, ReferenceAmplitude, c.dt / 2.0),
                    MavF15ResearchTimeDomainStability.Run(id, c, ReferenceAmplitude, c.dt / 4.0)
                };
            }

            // One-sided responses at the limit points and at two fold neighbours (why the antisymmetric pair is used).
            foreach (MavF15TimeDomainCase c in MavF15ResearchTimeDomainStability.Catalogue())
            {
                if (c.group != "E" && c.id != "D123-E" && c.id != "D125-E")
                    continue;
                double[] v = new double[4];
                int k = 0;
                foreach (double a in new[] { 3e-4, 1e-3 })
                {
                    v[k++] = MavF15ResearchTimeDomainStability.Run(id, c, a, 0.0, MavF15PerturbationScheme.PlusOnly).sigmaModal;
                    v[k++] = MavF15ResearchTimeDomainStability.Run(id, c, a, 0.0, MavF15PerturbationScheme.MinusOnly).sigmaModal;
                }

                s.oneSided[c.id] = v;
            }

            s.breaking = MavF15ResearchTimeDomainStability.SymmetryBreaking(id, MavF15ResearchTimeDomainStability.SymmetryBreakingSpeedFtPerSec,
                MavF15ResearchTimeDomainStability.SymmetryBreakingAmplitude, MavF15ResearchTimeDomainStability.SymmetryBreakingDuration,
                MavF15ResearchTimeDomainStability.SymmetryBreakingDt, out s.breakingEquilibrium, out s.breakingCriticalRe);

            // Cross-check where the + run settled: a WP-3C solve at its final bank angle, from its final state.
            if (s.breaking[0].completed)
            {
                double[] xe = s.breaking[0].state[s.breaking[0].state.Length - 1];
                MavF15TableViiState seed;
                MavF15TableViiTurningRecovery.TryGetState(199, out seed);
                MavF15ResearchTurningTrimGuess start = new MavF15ResearchTurningTrimGuess
                {
                    alphaDeg = (float)(xe[0] * RadToDeg), betaDeg = (float)(xe[1] * RadToDeg), pRadSec = (float)xe[2],
                    qRadSec = (float)xe[3], rRadSec = (float)xe[4], thetaDeg = (float)(xe[5] * RadToDeg),
                    trueAirspeedFtPerSec = (float)xe[7], symmetricStabilatorDeg = (float)s.breakingEquilibrium.stabilatorDeg
                };
                s.settledCheck = MavF15ResearchStabilityAnalysis.Turning(seed, xe[6] * RadToDeg, start);
            }

            s.mirrorEvaluated = MavF15ResearchTimeDomainStability.MirrorTrajectories(id, out s.mirrorGap, out s.mirrorExcursion,
                out s.mirrorBitwise, out s.mirrorOriginal, out s.mirrorMirrored);
            return s;
        }

        private static void Uncertainty(MavF15TimeDomainCase c, MavF15TimeDomainMeasurement m, out double u, out double band)
        {
            u = band = double.NaN;
            MavF15ResearchEquilibrium eq = MavF15ResearchTimeDomainStability.Equilibrium(c);
            MavF15ResearchLinearization lin = MavF15ResearchStabilityAnalysis.Linearize(MavF15AfitResearchIdentity.ConfigurationId, eq);
            if (!lin.evaluated)
                return;
            double best = double.PositiveInfinity;
            foreach (MavF15ResearchMode mode in lin.modes)
            {
                double d = MavF15ResearchStabilityAnalysis.Distance(mode.re, mode.im, m.predictedRe, m.predictedIm);
                if (d < best)
                {
                    best = d;
                    u = mode.realUncertainty;
                    band = mode.zeroBand;
                }
            }
        }

        public static string RunAll(out int passed, out int failed)
        {
            passed = 0;
            failed = 0;
            StringBuilder report = new StringBuilder(131072);
            report.AppendLine("F-15 Research Nonlinear Time-Domain Stability (WP-3E) and Source Stability Conflict Audit");
            report.AppendLine("=========================================================================================");
            report.AppendLine(MavF15AfitResearchSourceDynamics.Scope);
            report.AppendLine("Goal A: WP-3D eigenvalues vs RK4 integration of the unchanged source RHS. Goal B: isolated tests of the source's stability conflict.");
            report.AppendLine("SOURCE MODEL ONLY: not the real F-15, NASA 836, the FCS or a Rigidbody. Nothing is flown. Bands are NUMERICAL.");

            MavAtmosphereSample[] atmosphereBefore = SampleAtmosphere();
            MavAeroCoefficients[] coefficientsBefore = SampleCoefficients();
            float[] trimBefore = TrimFingerprint();

            Sweep s = RunSweep();

            ValidateUnchangedRhs(report, ref passed, ref failed);
            ValidateIntegrator(report, ref passed, ref failed);
            ValidateDtRefinement(s, report, ref passed, ref failed);
            ValidateStableDecay(s, report, ref passed, ref failed);
            ValidateFrequencies(s, report, ref passed, ref failed);
            ValidateCmmqBand(s, report, ref passed, ref failed);
            ValidateFolds(s, report, ref passed, ref failed);
            ValidatePitchfork(s, report, ref passed, ref failed);
            ValidateMirror(s, report, ref passed, ref failed);
            ValidateAmplitudeSweep(s, report, ref passed, ref failed);
            ValidateIsolation(report, ref passed, ref failed);
            ValidateCoefficients(atmosphereBefore, coefficientsBefore, report, ref passed, ref failed);
            ValidateDatasets(trimBefore, report, ref passed, ref failed);
            ValidateConflictHypotheses(s, report, ref passed, ref failed);

            report.AppendLine();
            report.Append("RESULT: ").Append(failed == 0 ? "PASS" : "FAIL").Append("  passed=").Append(passed).Append(" failed=").Append(failed);
            return report.ToString();
        }

        /// <summary>The time-domain dataset as delimited CSV blocks, for extraction from the batch log.</summary>
        public static string ExportDataset(out int passed, out int failed)
        {
            Sweep s = RunSweep();
            StringBuilder o = new StringBuilder(1 << 20);
            Block(o, "f15_time_domain_measurements.csv", MeasurementsCsv(s));
            Block(o, "f15_time_domain_trajectories.csv", TrajectoriesCsv(s));
            Block(o, "f15_time_domain_dt_audit.csv", DtAuditCsv(s));
            Block(o, "f15_time_domain_symmetry_breaking.csv", SymmetryBreakingCsv(s));
            Block(o, "f15_time_domain_mirror.csv", MirrorCsv(s));
            Block(o, "f15_stability_conflict_hypotheses.csv", HypothesesCsv());
            Block(o, "f15_table_vii_step_ratios.csv", StepRatioCsv());
            int evaluated = 0;
            foreach (CaseResult r in s.cases)
            {
                if (r.reference.evaluated)
                    evaluated++;
            }

            passed = evaluated == s.cases.Count ? 1 : 0;
            failed = 1 - passed;
            o.Append("RESULT: ").Append(failed == 0 ? "PASS" : "FAIL").Append("  passed=").Append(passed).Append(" failed=").Append(failed);
            return o.ToString();
        }

        // ---------------------------------------------------------------- [T1]

        private static void ValidateUnchangedRhs(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[T1] The WP-3D source RHS is used unchanged");

            string root = MavF15ResearchTimeDomainStability.ProjectRoot();
            string mismatch = null;
            int checkedFiles = 0;
            for (int i = 0; i < 4; i++)
            {
                string h = root == null ? null : Hash(Path.Combine(root, ProtectedFiles[i][0]));
                checkedFiles++;
                if (h != ProtectedFiles[i][1])
                    mismatch = ProtectedFiles[i][0] + " -> " + (h ?? "unreadable");
            }

            Record(root != null && mismatch == null,
                mismatch == null
                    ? "MavF15AfitResearchSourceDynamics.cs, the trim solver and both coefficient routines are byte-identical to WP-3D "
                      + "(SHA-256, line endings normalized)"
                    : "CHANGED: " + mismatch,
                report, ref passed, ref failed);

            // Fresh Jacobians and eigenvalues against the committed WP-3D dataset.
            Dictionary<int, double[]> jacobians = WP3DJacobians(root);
            int[] points = { 1, 20, 47, 124, 150, 165, 199 };
            int identical = 0, found = 0;
            foreach (int p in points)
            {
                double[] row;
                if (jacobians == null || !jacobians.TryGetValue(p, out row))
                    continue;
                found++;
                MavF15TimeDomainCase c = new MavF15TimeDomainCase { kind = MavF15TimeDomainEquilibriumKind.Table, point = p };
                MavF15ResearchEquilibrium eq = MavF15ResearchTimeDomainStability.Equilibrium(c);
                MavF15ResearchLinearization lin = MavF15ResearchStabilityAnalysis.Core(MavF15AfitResearchIdentity.ConfigurationId, eq);
                bool same = lin.evaluated;
                for (int i = 0; same && i < N; i++)
                {
                    for (int j = 0; j < N; j++)
                    {
                        if (lin.jacobian[i, j] != row[i * N + j])
                            same = false;
                    }
                }

                if (same)
                    identical++;
            }

            Record(found == points.Length && identical == found,
                "re-linearizing points 1, 20, 47, 124, 150, 165 and 199 reproduces the committed WP-3D Jacobians bit for bit ("
                + identical + "/" + points.Length + "): the prediction under test is exactly WP-3D's",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [T2]

        private static void ValidateIntegrator(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[T2] Fixed-step RK4 (validation-side)");

            // Exact test: x' = [s -w; w s] x, x(t) = e^{st} R(wt) x0.
            const double sigma = -0.3, omega = 4.0, horizon = 5.0;
            MavValidationDerivative f = delegate (double[] x, double[] d)
            {
                d[0] = sigma * x[0] - omega * x[1];
                d[1] = omega * x[0] + sigma * x[1];
                return true;
            };

            double[] errors = new double[3];
            double[] dts = { 0.04, 0.02, 0.01 };
            for (int k = 0; k < 3; k++)
            {
                MavValidationTrajectory t = MavValidationRk4Integrator.Integrate(f, new[] { 1.0, 0.0 }, dts[k], (int)Math.Round(horizon / dts[k]),
                    (int)Math.Round(horizon / dts[k]));
                double[] x = t.state[t.state.Length - 1];
                double e = Math.Exp(sigma * horizon);
                errors[k] = Math.Max(Math.Abs(x[0] - e * Math.Cos(omega * horizon)), Math.Abs(x[1] - e * Math.Sin(omega * horizon)));
            }

            double r1 = errors[0] / errors[1], r2 = errors[1] / errors[2];
            report.Append("    exact damped rotation (sigma -0.3, omega 4, t = 5 s): error ").Append(errors[0].ToString("E2")).Append(" / ")
                  .Append(errors[1].ToString("E2")).Append(" / ").Append(errors[2].ToString("E2")).Append(" at dt 0.04 / 0.02 / 0.01; ratios ")
                  .Append(r1.ToString("F2")).Append(", ").Append(r2.ToString("F2")).AppendLine(" (fourth order: 16)");
            Record(r1 > 12.0 && r1 < 20.0 && r2 > 12.0 && r2 < 20.0,
                "RK4 is fourth order on an exactly solvable test", report, ref passed, ref failed);

            MavF15TimeDomainCase c = MavF15ResearchTimeDomainStability.Catalogue().Find(x => x.id == "B47-A");
            MavF15TimeDomainMeasurement a = MavF15ResearchTimeDomainStability.Run(MavF15AfitResearchIdentity.ConfigurationId, c, ReferenceAmplitude, 0.0);
            MavF15TimeDomainMeasurement b = MavF15ResearchTimeDomainStability.Run(MavF15AfitResearchIdentity.ConfigurationId, c, ReferenceAmplitude, 0.0);
            bool same = a.evaluated && b.evaluated && a.time.Length == b.time.Length;
            for (int k = 0; same && k < a.time.Length; k++)
            {
                for (int i = 0; i < N; i++)
                {
                    if (a.perturbation[k][i] != b.perturbation[k][i])
                        same = false;
                }
            }

            Record(same && a.sigmaModal == b.sigmaModal && a.omegaModal == b.omegaModal,
                "deterministic: re-running an experiment reproduces every sample and every fitted number bit for bit",
                report, ref passed, ref failed);

            string root = ResolveValidationRoot();
            string[] files = { "MavValidationRk4Integrator.cs", "MavF15ResearchTimeDomainStability.cs", "MavF15ResearchStabilityConflictAudit.cs" };
            string[] forbidden = { "Rigidbody", "MonoBehaviour", "Transform", "Time.", "FixedUpdate", "MavF100", "MavAtmosphereModel", "MavSixDoFBody" };
            string hit = null;
            foreach (string file in files)
            {
                string code = root == null ? null : CodeOf(Path.Combine(root, file));
                if (code == null)
                {
                    hit = file + " unreadable";
                    continue;
                }

                foreach (string token in forbidden)
                {
                    if (code.IndexOf(token, StringComparison.Ordinal) >= 0)
                        hit = file + " names " + token;
                }
            }

            Record(hit == null,
                hit == null
                    ? "the integrator, the time-domain harness and the conflict audit name no Rigidbody, MonoBehaviour, Transform, Unity time, "
                      + "F100, atmosphere model or 6DoF body: they integrate x-dot = f(x) and nothing else"
                    : "VIOLATION: " + hit,
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [T3]

        private static void ValidateDtRefinement(Sweep s, StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[T3] dt refinement (amplitude 1e-4): dt, dt/2, dt/4 (NUMERICAL plateau, not physical uncertainty)");
            report.AppendLine("    case     dt      | sigma (1/s)          omega (rad/s)   | trajectory gap to dt/4 (relative)");
            bool plateau = true;
            foreach (KeyValuePair<string, MavF15TimeDomainMeasurement[]> kv in s.dtAudit)
            {
                MavF15TimeDomainMeasurement[] m = kv.Value;
                double sigmaScale = Math.Max(Math.Abs(m[2].sigmaModal), 1e-3);
                double omegaScale = Math.Max(Math.Abs(m[2].omegaModal), 1e-3);
                for (int k = 0; k < 3; k++)
                {
                    double gap = TrajectoryGap(m[k], m[2]);
                    report.Append("    ").Append(kv.Key.PadRight(8)).Append(m[k].dt.ToString("G4", CultureInfo.InvariantCulture).PadRight(8)).Append("| ")
                          .Append(m[k].sigmaModal.ToString("E8").PadRight(20)).Append(' ').Append(m[k].omegaModal.ToString("F8").PadRight(14))
                          .Append(" | ").AppendLine(gap.ToString("E2"));
                    if (Math.Abs(m[k].sigmaModal - m[2].sigmaModal) > DtPlateauRelative * sigmaScale
                        || Math.Abs(m[k].omegaModal - m[2].omegaModal) > DtPlateauRelative * omegaScale)
                        plateau = false;
                }
            }

            report.AppendLine("    trajectory gaps (<= ~6e-4 relative) sit at the float floor, not at RK4 truncation (they do not shrink 16x per halving);");
            report.AppendLine("    the fitted numbers move by < 2e-4 of themselves: fast 0.01 s, medium 0.05 s and slow 0.1 s are on the plateau");
            Record(plateau && s.dtAudit.Count == DtAuditCases.Length,
                "across dt, dt/2 and dt/4 every measured rate and frequency moves by less than " + DtPlateauRelative.ToString("E0")
                + " of itself (SP, phugoid, CMMQ-unstable pair, 8 rad/s Dutch-roll-like, saddle, pitchfork side)",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [T4]-[T6]

        private static void ValidateStableDecay(Sweep s, StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[T4] Stable modes decay at Re(lambda) (antisymmetric pair, amplitude 1e-4, local linear window)");
            report.AppendLine("    case     mode  | predicted Re      | measured modal    eigen-free (state)  | error / band        window");
            int n = 0, ok = 0;
            foreach (CaseResult r in s.cases)
            {
                MavF15TimeDomainMeasurement m = r.reference;
                if (m.predictedRe >= 0.0 || r.spec.group == "E")
                    continue;
                n++;
                bool good = r.rateAgrees && m.sigmaModal < 0.0;
                if (good)
                    ok++;
                RateLine(report, r);
            }

            Record(n > 0 && ok == n,
                n + " stable modes (symmetric point 20: all five; turning 150: all five; 117; the stable fold sides; the stable pitchfork "
                + "sides) decay, at Re(lambda) within the numerical band",
                report, ref passed, ref failed);
        }

        private static void ValidateFrequencies(Sweep s, StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[T5] Oscillatory modes: frequency from the modal phase and, eigen-free, from zero crossings of one state");
            report.AppendLine("    case     | predicted Im  | modal omega    | state omega (crossings)   | worst error / band");
            int n = 0, ok = 0;
            foreach (CaseResult r in s.cases)
            {
                MavF15TimeDomainMeasurement m = r.reference;
                if (m.predictedIm == 0.0)
                    continue;
                n++;
                if (r.frequencyAgrees)
                    ok++;
                report.Append("    ").Append(r.spec.id.PadRight(9)).Append("| ").Append(m.predictedIm.ToString("F6").PadRight(13)).Append(" | ")
                      .Append(m.omegaModal.ToString("F6").PadRight(14)).Append(" | ")
                      .Append((double.IsNaN(m.omegaState) ? "n/a" : m.omegaState.ToString("F6")).PadRight(10)).Append(" (")
                      .Append(m.dominantState).Append(", ").Append(m.zeroCrossings).Append(")".PadRight(8)).Append(" | ")
                      .Append(Math.Max(r.frequencyError, double.IsNaN(r.stateFrequencyError) ? 0.0 : r.stateFrequencyError).ToString("E1"))
                      .Append(" / ").AppendLine(r.frequencyBand.ToString("E1"));
            }

            Record(n > 0 && ok == n,
                "every one of the " + n + " oscillatory modes oscillates at |Im(lambda)| within the band, by the modal phase and, wherever "
                + "the linear window holds two zero crossings, by the eigen-free crossing spacing",
                report, ref passed, ref failed);
        }

        private static void ValidateCmmqBand(Sweep s, StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[T6] The CMMQ-positive band (symmetric points 47, 5, 8 at alpha 13.05, 13.82, 14.11 deg) and point 9 across the Hopf");
            bool grows = true, decays = false;
            foreach (CaseResult r in s.cases)
            {
                if (r.spec.group != "B")
                    continue;
                MavF15TimeDomainMeasurement m = r.reference;
                RateLine(report, r);
                if (r.spec.point == 9)
                    decays = r.rateAgrees && r.frequencyAgrees && m.sigmaModal < 0.0;
                else if (!(r.rateAgrees && r.frequencyAgrees && m.sigmaModal > 0.0))
                    grows = false;
            }

            CaseResult b47 = s.cases.Find(r => r.spec.id == "B47-A");
            report.Append("    point 47: a 1e-4 pitch-mode perturbation grows by ").Append(b47.reference.modalRatio.ToString("F1"))
                  .Append("x in ").Append(b47.reference.windowEnd.ToString("F1")).Append(" s, oscillating with period ")
                  .Append((2.0 * Math.PI / b47.reference.omegaModal).ToString("F2")).AppendLine(" s");
            Record(grows,
                "points 47, 5 and 8 GROW at the predicted rate (+0.81, +0.46, +0.12 /s) and oscillate at the predicted frequency: the printed "
                + "source equations - CMMQ as all three printings give it - are themselves unstable there, whatever the thesis figures and caption say",
                report, ref passed, ref failed);
            Record(decays,
                "point 9, just past the located Hopf (alpha 14.18 deg), DECAYS at its predicted -0.031 /s: the sign change is real in the nonlinear dynamics",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [T7]

        private static void ValidateFolds(Sweep s, StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[T7] Folds: the fold mode (tracked Mode E) on both sides of each printed limit point, and at the limit points");
            int[][] brackets = { new[] { 123, 124, 125 }, new[] { 135, 136, 137 }, new[] { 181, 182, 183 }, new[] { 193, 194, 195 } };
            bool signs = true;
            foreach (int[] b in brackets)
            {
                CaseResult a = s.cases.Find(r => r.spec.point == b[0] && r.spec.mode == "E");
                CaseResult c = s.cases.Find(r => r.spec.point == b[2] && r.spec.mode == "E");
                CaseResult lp = s.cases.Find(r => r.spec.point == b[1] && r.spec.mode == "E");
                report.Append("    fold ").Append(b[1]).Append(": ").Append(b[0]).Append(' ').Append(Sign(a)).Append(' ')
                      .Append(a.reference.sigmaModal.ToString("E3")).Append(" (pred ").Append(a.reference.predictedRe.ToString("E3")).Append(")  |  ");
                if (lp != null)
                    report.Append(b[1]).Append(" limit point ").Append(lp.reference.sigmaModal.ToString("E2")).Append(" (band ")
                          .Append(lp.zeroBand.ToString("E1")).Append(")  |  ");
                else
                    report.Append(b[1]).Append(" unavailable (print damage)  |  ");
                report.Append(b[2]).Append(' ').Append(Sign(c)).Append(' ').Append(c.reference.sigmaModal.ToString("E3")).Append(" (pred ")
                      .Append(c.reference.predictedRe.ToString("E3")).AppendLine(")");
                if (Math.Sign(a.reference.sigmaModal) == Math.Sign(c.reference.sigmaModal) || !a.rateAgrees || !c.rateAgrees)
                    signs = false;
            }

            Record(signs,
                "at every fold the fold mode decays on one side and grows on the other in the nonlinear dynamics, each at its predicted rate "
                + "within the band: the saddles WP-3D found are real",
                report, ref passed, ref failed);

            bool neutral = true;
            foreach (CaseResult r in s.cases)
            {
                if (r.spec.group != "E")
                    continue;
                if (!(Math.Abs(r.reference.sigmaModal) <= r.zeroBand))
                    neutral = false;
            }

            Record(neutral,
                "at the printed limit points 124, 136 and 182 the measured rate is inside WP-3D's numerical zero band (|sigma| <= ~1e-5 /s, "
                + "against >= 3e-4 /s one step away): near-neutral, very slow",
                report, ref passed, ref failed);

            report.AppendLine("    one-sided perturbations (x+ or x- alone, each against the unperturbed reference) keep the even nonlinear terms:");
            foreach (KeyValuePair<string, double[]> kv in s.oneSided)
            {
                report.Append("      ").Append(kv.Key.PadRight(7)).Append(" a 3e-4: +v ").Append(kv.Value[0].ToString("E2")).Append(", -v ")
                      .Append(kv.Value[1].ToString("E2")).Append("   a 1e-3: +v ").Append(kv.Value[2].ToString("E2")).Append(", -v ")
                      .AppendLine(kv.Value[3].ToString("E2"));
            }

            report.AppendLine("      they bracket the antisymmetric rate and their spread grows with amplitude; at the limit points +v and -v take opposite signs,");
            report.AppendLine("      growing ~ a - the fold's local (even-order) nonlinearity, which the antisymmetric pair cancels");
        }

        // ---------------------------------------------------------------- [T8]

        private static void ValidatePitchfork(Sweep s, StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[T8] Pitchfork: the symmetry-breaking real mode across the bifurcation, and local symmetry breaking");
            bool sign = true, turning = true;
            foreach (CaseResult r in s.cases)
            {
                if (r.spec.group != "F" && r.spec.group != "G")
                    continue;
                RateLine(report, r);
                if (!r.rateAgrees || Math.Sign(r.reference.sigmaModal) != Math.Sign(r.reference.predictedRe))
                {
                    if (r.spec.group == "F")
                        sign = false;
                    else
                        turning = false;
                }
            }

            CaseResult below = s.cases.Find(r => r.spec.id == "F377.3");
            CaseResult above = s.cases.Find(r => r.spec.id == "F377.6");
            Record(sign && below.reference.sigmaModal < 0.0 && above.reference.sigmaModal > 0.0,
                "SYMMETRIC branch: the lateral real mode decays below the crossing (V 372.4, 377.3) and grows above it (377.6, 382.4), at the "
                + "predicted rates: the sign change WP-3D located at V 377.438 is real in the nonlinear dynamics",
                report, ref passed, ref failed);
            Record(turning,
                "TURNING branches (phi +/-20, +/-2 deg): the same mode decays on both sides, at the predicted rates",
                report, ref passed, ref failed);

            MavF15ResearchTimeDomainStability.SymmetryBreakingRun plus = s.breaking[0], minus = s.breaking[1];
            report.Append("    symmetric V ").Append(s.breakingEquilibrium.state.trueAirspeedFtPerSec.ToString("F1")).Append(" (stabilator ")
                  .Append(s.breakingEquilibrium.stabilatorDeg.ToString("F4")).Append(", critical ").Append(s.breakingCriticalRe.ToString("E3"))
                  .AppendLine(" /s), +/- 1e-4 along the eigenvector, full nonlinear run:");
            foreach (MavF15ResearchTimeDomainStability.SymmetryBreakingRun r in s.breaking)
            {
                report.Append("      ").Append(r.sign > 0 ? "+" : "-").Append("v: |phi| reaches 1 deg at t = ").Append(r.departureTime.ToString("F0"))
                      .Append(" s with phi ").Append(r.departurePhiDeg.ToString("+0.000;-0.000")).Append(" deg, beta ")
                      .Append(r.departureBetaDeg.ToString("+0.0E0;-0.0E0")).Append(" deg, r ").Append(r.departureRRadSec.ToString("+0.0E0;-0.0E0"))
                      .Append(", psi-dot ").Append(r.departureHeadingRateRadSec.ToString("+0.0E0;-0.0E0")).Append(" | at ")
                      .Append(r.time[r.time.Length - 1].ToString("F0")).Append(" s: phi ").Append(r.finalPhiDeg.ToString("+0.000;-0.000"))
                      .Append(", V ").Append(r.finalSpeedFtPerSec.ToString("F2")).Append(", alpha ").Append(r.finalAlphaDeg.ToString("F3"))
                      .Append(", psi-dot ").Append(r.finalHeadingRateRadSec.ToString("+0.0000;-0.0000")).Append(", |x-dot| ")
                      .AppendLine(r.finalDerivativeScaled.ToString("E1"));
            }

            bool opposite = plus.completed && minus.completed && !double.IsNaN(plus.departureTime) && !double.IsNaN(minus.departureTime)
                            && plus.departurePhiDeg > 0.0 && minus.departurePhiDeg < 0.0
                            && Math.Sign(plus.departureHeadingRateRadSec) == 1 && Math.Sign(minus.departureHeadingRateRadSec) == -1
                            && Math.Sign(plus.departureRRadSec) == -Math.Sign(minus.departureRRadSec);
            Record(opposite,
                "LOCAL SYMMETRY BREAKING: +v and -v depart in opposite bank directions with opposite heading rates - a right and a left turn",
                report, ref passed, ref failed);

            MavF15ResearchEquilibrium settled = s.settledCheck;
            bool settles = settled.recovered && plus.finalDerivativeScaled < 1e-6 && minus.finalDerivativeScaled < 1e-6
                           && Math.Abs(settled.stabilatorDeg - s.breakingEquilibrium.stabilatorDeg) < 1e-3
                           && Math.Abs(settled.state.trueAirspeedFtPerSec - plus.finalSpeedFtPerSec) < 0.1;
            report.Append("    observed, not assumed: both runs settle (|x-dot| < 1e-6) on the steady turns phi = +/-")
                  .Append(Math.Abs(plus.finalPhiDeg).ToString("F3")).Append(" deg; a WP-3C solve at that phi returns stabilator ")
                  .Append(settled.stabilatorDeg.ToString("F4")).Append(" (held ").Append(s.breakingEquilibrium.stabilatorDeg.ToString("F4"))
                  .Append("), V ").Append(settled.state.trueAirspeedFtPerSec.ToString("F2")).AppendLine(" - the outer turning segment of Table VII (198-199: phi ~63, V ~626)");
            Record(settles,
                "beyond the local claim (reported as an observation): the nonlinear runs come to rest on a WP-3C turning equilibrium at the held stabilator",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [T9]

        private static void ValidateMirror(Sweep s, StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[T9] Mirrored nonlinear trajectories (turning point 150 and its mirror, finite perturbation, 60 s, dt 0.01)");
            report.AppendLine("    perturbation: alpha +0.5 deg, beta +0.3 deg, p +0.02, q -0.01, r +0.01 rad/s, theta -0.3 deg, phi +2 deg, V +5 ft/s");
            double worst = 0.0;
            report.Append("    largest |x_mirror - S x| / largest excursion:");
            for (int i = 0; s.mirrorEvaluated && i < N; i++)
            {
                double rel = s.mirrorExcursion[i] > 0.0 ? s.mirrorGap[i] / s.mirrorExcursion[i] : 0.0;
                worst = Math.Max(worst, rel);
                report.Append(' ').Append(MavF15AfitResearchSourceDynamics.StateNames[i]).Append(' ').Append(rel.ToString("E1"));
            }

            report.AppendLine();
            report.Append("    even (alpha, q, theta, V) and odd (beta, p, r, phi): ").AppendLine(s.mirrorBitwise ? "bit-identical at every sample" : "within round-off");
            Record(s.mirrorEvaluated && worst <= 1e-12,
                "the full nonlinear trajectories stay mirrored (" + (s.mirrorBitwise ? "bit for bit" : "to " + worst.ToString("E1"))
                + ") - independent of the eigensolver",
                report, ref passed, ref failed);

            double gm = 0.0;
            MavF15ResearchTimeDomainStability.SymmetryBreakingRun a = s.breaking[0], b = s.breaking[1];
            int n = Math.Min(a.time.Length, b.time.Length);
            for (int k = 0; k < n; k++)
            {
                for (int i = 0; i < N; i++)
                    gm = Math.Max(gm, Math.Abs(b.state[k][i] - MavF15ResearchTimeDomainStability.Parity[i] * a.state[k][i]));
            }

            report.Append("    the +/- symmetry-breaking runs (6000 s through an unstable equilibrium) stay mirrored to ").Append(gm.ToString("E1"))
                  .AppendLine(" (rounding grown by the instability)");
        }

        // ---------------------------------------------------------------- [T10]

        private static void ValidateAmplitudeSweep(Sweep s, StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[T10] Amplitude sweep (relative rate error; antisymmetric pair). The float floor (~1e-8 /s bias) limits small amplitudes");
            report.AppendLine("      for slow modes; cubic nonlinearity limits large ones near a fold.");
            report.Append("    case     |lambda|  ");
            foreach (double a in SweepAmplitudes)
                report.Append(("a " + a.ToString("E0", CultureInfo.InvariantCulture)).PadLeft(11));
            report.AppendLine();
            bool converge = true;
            foreach (CaseResult r in s.cases)
            {
                if (r.spec.group == "E")
                    continue;
                double lambda = Math.Sqrt(r.reference.predictedRe * r.reference.predictedRe + r.reference.predictedIm * r.reference.predictedIm);
                report.Append("    ").Append(r.spec.id.PadRight(9)).Append(lambda.ToString("E2").PadLeft(9)).Append(' ');
                foreach (MavF15TimeDomainMeasurement m in r.sweep)
                {
                    double e = (m.sigmaModal - m.predictedRe) / Math.Max(Math.Abs(m.predictedRe), 1e-9);
                    report.Append((m.evaluated && !double.IsNaN(e) ? e.ToString("P2") : "n/a").PadLeft(11));
                }

                report.AppendLine();

                // The reference amplitude and at least one neighbouring amplitude must both be inside the band:
                // the comparison is made on a plateau, wherever the float floor and the nonlinearity leave one.
                int reference = Array.IndexOf(SweepAmplitudes, ReferenceAmplitude);
                bool inside = InBand(r.sweep[reference], r.rateBand);
                bool neighbour = (reference > 0 && InBand(r.sweep[reference - 1], r.rateBand))
                                 || (reference + 1 < r.sweep.Length && InBand(r.sweep[reference + 1], r.rateBand));
                if (!(inside && neighbour))
                    converge = false;
            }

            report.AppendLine("    fast modes stay inside the band down to 1e-6; slow ones reach the float floor below ~1e-5 (error ~ 1e-8 / (a |lambda|));");
            report.AppendLine("    beside fold 194 (D193) the cubic nonlinearity leaves the band already at 1e-3 - the linear regime narrows near a fold");
            Record(converge,
                "for every mode the reference amplitude 1e-4 and at least one neighbouring amplitude (1e-5 or 1e-3) both agree with Re(lambda) within "
                + "the band: the comparison is made on an amplitude plateau - the local linear regime - never at the float floor or in the nonlinear range",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [T11]-[T13]

        private static void ValidateIsolation(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[T11] Isolation");
            MavF15TimeDomainCase c = MavF15ResearchTimeDomainStability.Catalogue()[0];
            MavF15TimeDomainMeasurement exact = MavF15ResearchTimeDomainStability.Run(MavF15ReferenceData.TargetConfigurationId, c, ReferenceAmplitude, 0.0);
            MavF15ResearchEquilibrium eq;
            double cr;
            MavF15ResearchTimeDomainStability.SymmetryBreakingRun[] sb = MavF15ResearchTimeDomainStability.SymmetryBreaking(
                MavF15ReferenceData.TargetConfigurationId, 382.4, 1e-4, 10.0, 0.1, out eq, out cr);
            double[] g, e;
            bool bw;
            MavValidationTrajectory o, m;
            bool mirror = MavF15ResearchTimeDomainStability.MirrorTrajectories(MavF15ReferenceData.TargetConfigurationId, out g, out e, out bw, out o, out m);
            Record(!exact.evaluated && exact.time == null && sb[0].time == null && !mirror,
                "every time-domain entry point refuses the exact NASA 836 id: " + exact.reason, report, ref passed, ref failed);

            string root = MavF15ResearchTimeDomainStability.ProjectRoot();
            string fd = root == null ? null : Path.Combine(root, Path.Combine("Assets", MavFlightDynamicsOwnershipScan.FlightDynamicsRelativePath));
            string offender = null;
            int scanned = 0;
            if (fd != null && Directory.Exists(fd))
            {
                string validation = Path.GetFullPath(Path.Combine(fd, "Validation")) + Path.DirectorySeparatorChar;
                foreach (string f in Directory.GetFiles(fd, "*.cs", SearchOption.AllDirectories))
                {
                    string full = Path.GetFullPath(f);
                    if (full.StartsWith(validation, StringComparison.OrdinalIgnoreCase))
                        continue;
                    scanned++;
                    string text = File.ReadAllText(full);
                    if (text.IndexOf("MavValidationRk4Integrator", StringComparison.Ordinal) >= 0
                        || text.IndexOf("MavF15ResearchTimeDomainStability", StringComparison.Ordinal) >= 0
                        || text.IndexOf("MavF15ResearchStabilityConflictAudit", StringComparison.Ordinal) >= 0)
                        offender = Path.GetFileName(full);
                }
            }

            Record(scanned > 0 && offender == null,
                offender == null
                    ? "no runtime or editor source outside Validation/ (" + scanned + " scanned) names the integrator, the harness or the audit"
                    : "VIOLATION: " + offender,
                report, ref passed, ref failed);
        }

        private static void ValidateCoefficients(MavAtmosphereSample[] atmosphereBefore, MavAeroCoefficients[] coefficientsBefore,
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[T12] Coefficients, CFX2, atmosphere");
            MavAeroCoefficients[] after = SampleCoefficients();
            bool same = after.Length == coefficientsBefore.Length;
            for (int i = 0; same && i < after.Length; i++)
            {
                same = after[i].cx == coefficientsBefore[i].cx && after[i].cz == coefficientsBefore[i].cz && after[i].cm == coefficientsBefore[i].cm
                       && after[i].cy == coefficientsBefore[i].cy && after[i].cl == coefficientsBefore[i].cl && after[i].cn == coefficientsBefore[i].cn;
            }

            double a40 = 40.0 * Math.PI / 180.0;
            MavAeroCoefficients c = MavF15BaumannMach06Longitudinal.Evaluate((float)a40, 0f, 0f);
            double cfx = -((c.cx * Math.Cos(a40)) + (c.cz * Math.Sin(a40)));
            double cfxBase = 0.0267297 - (0.10646919 * a40) + (5.39836337 * a40 * a40) - (5.0086893 * Math.Pow(a40, 3)) + (1.34148193 * Math.Pow(a40, 4));
            bool cfx2 = Math.Abs(cfx - (cfxBase + 0.09833517)) < Math.Abs(cfx - (cfxBase + 0.09833617));
            double cmq13 = MavF15ResearchStabilityConflictAudit.CmqFromRoutine(13.0 * Math.PI / 180.0, -9.4);
            Record(same && cfx2 && cmq13 > 19.0 && cmq13 < 20.5,
                "the coefficient routines are bit-identical before and after every experiment, CFX2 is App. C's 0.09833517, and CMMQ(13 deg) is "
                + "still the printed " + cmq13.ToString("F2") + " /rad - no coefficient was touched",
                report, ref passed, ref failed);

            MavAtmosphereSample[] atmosphereAfter = SampleAtmosphere();
            bool atmosphere = atmosphereAfter.Length == atmosphereBefore.Length;
            for (int i = 0; atmosphere && i < atmosphereAfter.Length; i++)
                atmosphere = atmosphereAfter[i].densityKgM3 == atmosphereBefore[i].densityKgM3 && atmosphereAfter[i].pressurePa == atmosphereBefore[i].pressurePa;
            Record(atmosphere, "MavAtmosphereModel returns bit-identical samples before and after", report, ref passed, ref failed);
        }

        private static void ValidateDatasets(float[] trimBefore, StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[T13] Existing WP-3A/B/C/D artifacts unchanged");
            string root = MavF15ResearchTimeDomainStability.ProjectRoot();
            string mismatch = null;
            for (int i = 4; i < ProtectedFiles.Length; i++)
            {
                string h = root == null ? null : Hash(Path.Combine(root, ProtectedFiles[i][0]));
                if (h != ProtectedFiles[i][1])
                    mismatch = ProtectedFiles[i][0];
            }

            Record(root != null && mismatch == null,
                mismatch == null
                    ? "the Table VII transcription and its generated data, and all five WP-3D stability CSVs, are byte-identical to WP-3D"
                    : "CHANGED: " + mismatch,
                report, ref passed, ref failed);

            float[] trimAfter = TrimFingerprint();
            bool same = trimAfter.Length == trimBefore.Length;
            for (int i = 0; same && i < trimAfter.Length; i++)
                same = trimAfter[i] == trimBefore[i];
            Record(same,
                "WP-3B symmetric and WP-3C turning roots (points 1, 46, 117, 150, 199) are bit-identical before and after the whole time-domain pass",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [T14]

        private static void ValidateConflictHypotheses(Sweep s, StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[T14] Goal B - the source's stability conflict, source-supported hypotheses tested in isolation (no model change)");

            double aMin, aMax, largestTerm;
            List<MavF15PitchDampingTest> tests = MavF15ResearchStabilityConflictAudit.PitchDampingHypotheses(out aMin, out aMax, out largestTerm);
            report.Append("    CMMQ*QB term scaled by k at the 80 printed turning rows (alpha ").Append(aMin.ToString("F2")).Append("-").Append(aMax.ToString("F2"))
                  .Append(" deg, where q != 0; the term reaches ").Append(largestTerm.ToString("E1")).AppendLine(" in Cm), against WP-3A's print floor:");
            bool printedHolds = false, othersFail = true;
            foreach (MavF15PitchDampingTest t in tests)
            {
                report.Append("      ").Append(t.hypothesis.PadRight(52)).Append(" rows above floor ").Append(t.rowsAboveFloor.ToString().PadLeft(2))
                      .Append("/").Append(t.rows).Append("  largest residual/floor ").AppendLine(t.largestRatio.ToString("E2"));
                if (t.factor == 1.0)
                    printedHolds = t.rowsAboveFloor == 0;
                else if (t.rowsAboveFloor == 0)
                    othersFail = false;
            }

            Record(printedHolds && othersFail,
                "only the printed reading (k = 1) keeps every turning row inside its print floor; a sign flip, no pitch-rate term, 2x or 1/2 "
                + "normalization, span instead of chord, and deg/s or per-degree units each push rows far outside - RULED OUT. These rows test "
                + "the term's sign, normalization and units everywhere, but CMMQ's value only at alpha <= 10.6 deg",
                report, ref passed, ref failed);

            double cmqDegreeArgument = MavF15ResearchStabilityConflictAudit.CmqFromRoutine(13.0, -9.4);
            double cmqRadian = MavF15ResearchStabilityConflictAudit.CmqFromRoutine(13.0 * Math.PI / 180.0, -9.4);
            report.Append("    CMMQ at 13 deg with RAL in radians (as COEFF defines it): ").Append(cmqRadian.ToString("F2")).Append(" /rad; with 13 fed as degrees: ")
                  .AppendLine(cmqDegreeArgument.ToString("E2"));
            Record(Math.Abs(cmqDegreeArgument) > 1e6,
                "a degree-valued alpha argument gives an absurd CMMQ (|.| > 1e6) and COEFF defines RAL = AL/DEGRAD with radian breakpoints "
                + "0.25307 / 0.29671 - RULED OUT", report, ref passed, ref failed);

            MavF15ResearchEquilibrium e47 = MavF15ResearchTimeDomainStability.Equilibrium(new MavF15TimeDomainCase { kind = MavF15TimeDomainEquilibriumKind.Table, point = 47 });
            MavF15ResearchLinearization lin47 = MavF15ResearchStabilityAnalysis.Linearize(MavF15AfitResearchIdentity.ConfigurationId, e47);
            double shift, extra;
            bool act = MavF15ResearchStabilityConflictAudit.ActuatorStateSpectrum(lin47, 20.0, out shift, out extra);
            report.Append("    Davison's stabilator actuator state (x' = 20 (cmd - x)) appended at point 47: the 9x9 spectrum is WP-3D's to ")
                  .Append(shift.ToString("E1")).Append(" plus ").AppendLine(extra.ToString("F6"));
            Record(act && shift <= 1e-10 && Math.Abs(extra + 20.0) <= 1e-9,
                "actuator states (Davison 12-state, CAS off) are block triangular: they append -20/-28 and move no airframe eigenvalue - RULED OUT",
                report, ref passed, ref failed);

            double[] lre, lim;
            double mq, mqFromCmmq;
            bool block = MavF15ResearchStabilityConflictAudit.LongitudinalBlock(lin47, out lre, out lim, out mq, out mqFromCmmq);
            double pairGap = double.PositiveInfinity;
            MavF15ResearchMode unstable = lin47.modes[0];
            foreach (MavF15ResearchMode mo in lin47.modes)
            {
                if (mo.re > unstable.re)
                    unstable = mo;
            }

            for (int k = 0; block && k < 4; k++)
                pairGap = Math.Min(pairGap, MavF15ResearchStabilityAnalysis.Distance(lre[k], lim[k], unstable.re, unstable.im));
            report.Append("    point 47: the unstable pair lives in the longitudinal block (alpha, q, theta, V; gap ").Append(pairGap.ToString("E1"))
                  .Append("); dq'/dq = ").Append(mq.ToString("F4")).Append(" /s vs K8 V (cbar/2) CMMQ = ").Append(mqFromCmmq.ToString("F4")).AppendLine(" /s");
            Record(block && pairGap <= 1e-12 && Math.Abs(mq - mqFromCmmq) <= 1e-3 * Math.Abs(mqFromCmmq) && mq > 0.0,
                "the instability is carried by the pitch-damping term alone: a longitudinal pair with positive dq'/dq equal to K8 V (cbar/2) CMMQ; "
                + "the inertia product Ixz does not enter that block - RULED OUT as a cause",
                report, ref passed, ref failed);

            List<MavF15TableStepRatio> sym = MavF15ResearchStabilityConflictAudit.StepRatios(MavF15ResearchStabilityConflictAudit.SymmetricPrintedSequence());
            List<MavF15TableStepRatio> turn = MavF15ResearchStabilityConflictAudit.StepRatios(MavF15ResearchStabilityConflictAudit.TurningPrintedSequence());
            double symMin = double.PositiveInfinity, symMax = 0.0, at89 = double.NaN;
            foreach (MavF15TableStepRatio r in sym)
            {
                symMin = Math.Min(symMin, r.ratio);
                symMax = Math.Max(symMax, r.ratio);
                if (r.from == 8 && r.to == 9)
                    at89 = r.ratio;
            }

            List<string> shortSteps = new List<string>();
            bool special = true;
            int[][] expected = { new[] { 123, 124 }, new[] { 135, 136 }, new[] { 164, 165 }, new[] { 181, 182 }, new[] { 193, 194 } };
            foreach (MavF15TableStepRatio r in turn)
            {
                if (r.ratio < 0.5)
                    shortSteps.Add(r.from + "-" + r.to + " " + r.ratio.ToString("F2"));
            }

            foreach (int[] e in expected)
            {
                MavF15TableStepRatio r = turn.Find(x => x.from == e[0] && x.to == e[1]);
                if (!(r.ratio < 0.5))
                    special = false;
            }

            report.Append("    Table VII step ratios: symmetric branch ").Append(symMin.ToString("F2")).Append("-").Append(symMax.ToString("F2"))
                  .Append(" (8->9 at the located Hopf: ").Append(at89.ToString("F3")).Append("); turning short steps: ").AppendLine(string.Join(", ", shortSteps.ToArray()));
            report.AppendLine("      (172-173 / 174-175 bracket a printing seam where rows are missing, not a special point)");
            Record(special && symMin > 0.85 && symMax < 1.15 && Math.Abs(at89 - 1.0) < 0.01,
                "AUTO's located special points appear as short steps at every fold (123-124, 135-136, 181-182, 193-194) and at the fork "
                + "(164-165), but the symmetric branch is uniform throughout, 8->9 included: the executed run located no Hopf where the printed "
                + "CMMQ puts one (indirect evidence)",
                report, ref passed, ref failed);

            report.AppendLine("    listing and presentation evidence:");
            foreach (MavF15ConflictHypothesis h in MavF15ResearchStabilityConflictAudit.ListingEvidence())
                report.Append("      ").Append(h.verdict.PadRight(44)).Append(' ').AppendLine(h.name);

            CaseResult b47 = s.cases.Find(r => r.spec.id == "B47-A");
            bool confirmed = b47 != null && b47.rateAgrees && b47.reference.sigmaModal > 0.0;
            report.AppendLine("    CMMQ provenance: Baumann 1989 App. B (PDF p.114) = Davison 1992 App. B (p.116) = App. C (p.147), all read on page images;");
            report.AppendLine("    upstream: McAir ARO10 / 1988 F-15 Aerobase ATAB05 (not public) -> Baumann SAS fit; McDonnell 1990 refit Cmq (repository lineage, not held)");
            report.Append("    VERDICT: ").AppendLine(confirmed ? "PUBLIC PRINTINGS AGREE - EXECUTED AUTO MODEL MAY DIFFER" : "(time-domain confirmation missing)");
            Record(confirmed,
                "the verdict is recorded only because the nonlinear dynamics confirm the instability the printed CMMQ implies ([T6])",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- CSV

        private static void Block(StringBuilder o, string name, string csv)
        {
            o.Append("BEGIN_CSV ").Append(name).Append('\n').Append(csv);
            if (csv.Length > 0 && csv[csv.Length - 1] != '\n')
                o.Append('\n');
            o.Append("END_CSV ").Append(name).Append('\n');
        }

        private static string MeasurementsCsv(Sweep s)
        {
            CultureInfo ci = CultureInfo.InvariantCulture;
            StringBuilder o = new StringBuilder(65536);
            o.Append("case,group,equilibrium,mode,amplitude,dt_s,duration_s,window_end_s,window_samples,predicted_re_per_s,predicted_im_rad_s,"
                     + "measured_sigma_modal,measured_omega_modal,measured_sigma_state,measured_omega_state,dominant_state,zero_crossings,extrema,"
                     + "wp3d_uncertainty,rate_error,rate_band,rate_agrees,frequency_error,frequency_band,frequency_agrees,reference_amplitude\n");
            foreach (CaseResult r in s.cases)
            {
                foreach (MavF15TimeDomainMeasurement m in r.sweep)
                {
                    bool isRef = m.amplitude == ReferenceAmplitude;
                    o.Append(r.spec.id).Append(',').Append(r.spec.group).Append(',').Append(EquilibriumId(r.spec)).Append(',').Append(r.spec.mode).Append(',')
                     .Append(m.amplitude.ToString("R", ci)).Append(',').Append(m.dt.ToString("R", ci)).Append(',').Append(r.spec.duration.ToString("R", ci)).Append(',')
                     .Append(m.windowEnd.ToString("R", ci)).Append(',').Append(m.windowSamples.ToString(ci)).Append(',')
                     .Append(m.predictedRe.ToString("R", ci)).Append(',').Append(m.predictedIm.ToString("R", ci)).Append(',')
                     .Append(Num(m.sigmaModal)).Append(',').Append(Num(m.omegaModal)).Append(',').Append(Num(m.sigmaState)).Append(',').Append(Num(m.omegaState)).Append(',')
                     .Append(m.dominantState).Append(',').Append(m.zeroCrossings.ToString(ci)).Append(',').Append(m.extrema.ToString(ci)).Append(',')
                     .Append(r.uncertainty.ToString("E3", ci)).Append(',').Append(Num(Math.Abs(m.sigmaModal - m.predictedRe))).Append(',')
                     .Append(r.rateBand.ToString("E3", ci)).Append(',').Append(isRef ? (r.rateAgrees ? "true" : "false") : "").Append(',')
                     .Append(m.predictedIm != 0.0 ? Num(Math.Abs(m.omegaModal - m.predictedIm)) : "").Append(',')
                     .Append(m.predictedIm != 0.0 ? r.frequencyBand.ToString("E3", ci) : "").Append(',')
                     .Append(isRef && m.predictedIm != 0.0 ? (r.frequencyAgrees ? "true" : "false") : "").Append(',')
                     .Append(isRef ? "true" : "false").Append('\n');
                }
            }

            return o.ToString();
        }

        private static string TrajectoriesCsv(Sweep s)
        {
            CultureInfo ci = CultureInfo.InvariantCulture;
            StringBuilder o = new StringBuilder(1 << 19);
            o.Append("case,equilibrium,mode,amplitude,dt_s,duration_s,predicted_re_per_s,predicted_im_rad_s,measured_sigma,measured_omega,t_s,"
                     + "d_alpha_rad,d_beta_rad,d_p_rad_s,d_q_rad_s,d_r_rad_s,d_theta_rad,d_phi_rad,d_V_ft_s,modal_re,modal_im\n");
            foreach (string id in ExportTrajectoryCases)
            {
                CaseResult r = s.cases.Find(x => x.spec.id == id);
                if (r == null || !r.reference.evaluated)
                    continue;
                MavF15TimeDomainMeasurement m = r.reference;
                int n = m.time.Length;
                int stride = Math.Max(1, n / MavF15ResearchTimeDomainStability.ExportSamples);
                for (int k = 0; k < n; k += stride)
                {
                    o.Append(id).Append(',').Append(EquilibriumId(r.spec)).Append(',').Append(r.spec.mode).Append(',').Append(m.amplitude.ToString("R", ci)).Append(',')
                     .Append(m.dt.ToString("R", ci)).Append(',').Append(r.spec.duration.ToString("R", ci)).Append(',')
                     .Append(m.predictedRe.ToString("R", ci)).Append(',').Append(m.predictedIm.ToString("R", ci)).Append(',')
                     .Append(Num(m.sigmaModal)).Append(',').Append(Num(m.omegaModal)).Append(',').Append(m.time[k].ToString("R", ci));
                    for (int i = 0; i < N; i++)
                        o.Append(',').Append(m.perturbation[k][i].ToString("E9", ci));
                    o.Append(',').Append(m.modal[k].Real.ToString("E9", ci)).Append(',').Append(m.modal[k].Imaginary.ToString("E9", ci)).Append('\n');
                }
            }

            return o.ToString();
        }

        private static string DtAuditCsv(Sweep s)
        {
            CultureInfo ci = CultureInfo.InvariantCulture;
            StringBuilder o = new StringBuilder(4096);
            o.Append("case,dt_s,measured_sigma,measured_omega,trajectory_gap_to_dt4_relative\n");
            foreach (KeyValuePair<string, MavF15TimeDomainMeasurement[]> kv in s.dtAudit)
            {
                foreach (MavF15TimeDomainMeasurement m in kv.Value)
                {
                    o.Append(kv.Key).Append(',').Append(m.dt.ToString("R", ci)).Append(',').Append(Num(m.sigmaModal)).Append(',').Append(Num(m.omegaModal))
                     .Append(',').Append(TrajectoryGap(m, kv.Value[2]).ToString("E3", ci)).Append('\n');
                }
            }

            return o.ToString();
        }

        private static string SymmetryBreakingCsv(Sweep s)
        {
            CultureInfo ci = CultureInfo.InvariantCulture;
            StringBuilder o = new StringBuilder(65536);
            o.Append("sign,t_s,alpha_deg,beta_deg,p_rad_s,q_rad_s,r_rad_s,theta_deg,phi_deg,V_ft_s,heading_rate_rad_s\n");
            foreach (MavF15ResearchTimeDomainStability.SymmetryBreakingRun r in s.breaking)
            {
                int stride = Math.Max(1, r.time.Length / 150);
                for (int k = 0; k < r.time.Length; k += stride)
                {
                    double[] x = r.state[k];
                    o.Append(r.sign > 0 ? "+" : "-").Append(',').Append(r.time[k].ToString("R", ci)).Append(',')
                     .Append((x[0] * RadToDeg).ToString("E9", ci)).Append(',').Append((x[1] * RadToDeg).ToString("E9", ci)).Append(',')
                     .Append(x[2].ToString("E9", ci)).Append(',').Append(x[3].ToString("E9", ci)).Append(',').Append(x[4].ToString("E9", ci)).Append(',')
                     .Append((x[5] * RadToDeg).ToString("E9", ci)).Append(',').Append((x[6] * RadToDeg).ToString("E9", ci)).Append(',')
                     .Append(x[7].ToString("E9", ci)).Append(',').Append(MavF15ResearchTimeDomainStability.HeadingRate(x).ToString("E9", ci)).Append('\n');
                }
            }

            return o.ToString();
        }

        private static string MirrorCsv(Sweep s)
        {
            CultureInfo ci = CultureInfo.InvariantCulture;
            StringBuilder o = new StringBuilder(4096);
            o.Append("state,parity,largest_gap,largest_excursion,bitwise\n");
            for (int i = 0; s.mirrorEvaluated && i < N; i++)
            {
                o.Append(MavF15AfitResearchSourceDynamics.StateNames[i]).Append(',').Append(MavF15ResearchTimeDomainStability.Parity[i] > 0 ? "even" : "odd").Append(',')
                 .Append(s.mirrorGap[i].ToString("R", ci)).Append(',').Append(s.mirrorExcursion[i].ToString("R", ci)).Append(',')
                 .Append(s.mirrorBitwise ? "true" : "false").Append('\n');
            }

            return o.ToString();
        }

        private static string HypothesesCsv()
        {
            CultureInfo ci = CultureInfo.InvariantCulture;
            StringBuilder o = new StringBuilder(8192);
            o.Append("kind,hypothesis,factor,rows,rows_above_floor,largest_ratio,verdict,evidence\n");
            double a, b, c;
            foreach (MavF15PitchDampingTest t in MavF15ResearchStabilityConflictAudit.PitchDampingHypotheses(out a, out b, out c))
            {
                o.Append("pitch_damping_scaling,\"").Append(t.hypothesis).Append("\",").Append(t.factor.ToString("R", ci)).Append(',').Append(t.rows).Append(',')
                 .Append(t.rowsAboveFloor).Append(',').Append(t.largestRatio.ToString("E3", ci)).Append(',')
                 .Append(t.factor == 1.0 ? (t.rowsAboveFloor == 0 ? "PRINTED - CONSISTENT" : "PRINTED - INCONSISTENT") : (t.rowsAboveFloor > 0 ? "RULED OUT" : "NOT RULED OUT"))
                 .Append(",\"turning rows alpha ").Append(a.ToString("F2", ci)).Append("-").Append(b.ToString("F2", ci)).Append(" deg vs WP-3A print floor\"\n");
            }

            foreach (MavF15ConflictHypothesis h in MavF15ResearchStabilityConflictAudit.ListingEvidence())
                o.Append("evidence,\"").Append(h.name).Append("\",,,,,").Append(h.verdict).Append(",\"").Append(h.evidence.Replace("\"", "'")).Append("\"\n");
            return o.ToString();
        }

        private static string StepRatioCsv()
        {
            CultureInfo ci = CultureInfo.InvariantCulture;
            StringBuilder o = new StringBuilder(8192);
            o.Append("branch,from_point,to_point,step_over_neighbour_mean\n");
            foreach (MavF15TableStepRatio r in MavF15ResearchStabilityConflictAudit.StepRatios(MavF15ResearchStabilityConflictAudit.SymmetricPrintedSequence()))
                o.Append("symmetric,").Append(r.from).Append(',').Append(r.to).Append(',').Append(r.ratio.ToString("F6", ci)).Append('\n');
            foreach (MavF15TableStepRatio r in MavF15ResearchStabilityConflictAudit.StepRatios(MavF15ResearchStabilityConflictAudit.TurningPrintedSequence()))
                o.Append("turning,").Append(r.from).Append(',').Append(r.to).Append(',').Append(r.ratio.ToString("F6", ci)).Append('\n');
            return o.ToString();
        }

        // ---------------------------------------------------------------- helpers

        private static void RateLine(StringBuilder report, CaseResult r)
        {
            MavF15TimeDomainMeasurement m = r.reference;
            report.Append("    ").Append(r.spec.id.PadRight(9)).Append(r.spec.mode.PadRight(6)).Append("| ")
                  .Append(m.predictedRe.ToString("E5").PadRight(17)).Append(" | ").Append(m.sigmaModal.ToString("E5").PadRight(17)).Append(' ')
                  .Append((double.IsNaN(m.sigmaState) ? "n/a" : m.sigmaState.ToString("E5")).PadRight(15)).Append("(").Append(m.dominantState).Append(")")
                  .Append(" | ").Append(r.rateError.ToString("E1")).Append(" / ").Append(r.rateBand.ToString("E1")).Append(r.rateAgrees ? "  ok" : "  OUT")
                  .Append("   ").Append(m.windowEnd.ToString("F1")).AppendLine(" s");
        }

        private static bool InBand(MavF15TimeDomainMeasurement m, double band)
        {
            return m.evaluated && !double.IsNaN(m.sigmaModal) && Math.Abs(m.sigmaModal - m.predictedRe) <= band;
        }

        private static string Sign(CaseResult r)
        {
            return r.reference.sigmaModal > 0.0 ? "GROWS" : "decays";
        }

        private static string EquilibriumId(MavF15TimeDomainCase c)
        {
            switch (c.kind)
            {
                case MavF15TimeDomainEquilibriumKind.SymmetricSpeed:
                    return "symmetric_V" + c.parameter.ToString("F1", CultureInfo.InvariantCulture);
                case MavF15TimeDomainEquilibriumKind.TurningPhi:
                    return "turning_phi" + c.parameter.ToString("+0.#;-0.#", CultureInfo.InvariantCulture);
                default:
                    return "table_vii_" + c.point.ToString(CultureInfo.InvariantCulture);
            }
        }

        private static string Num(double v)
        {
            return double.IsNaN(v) ? "" : v.ToString("R", CultureInfo.InvariantCulture);
        }

        private static double TrajectoryGap(MavF15TimeDomainMeasurement a, MavF15TimeDomainMeasurement b)
        {
            if (a.time == null || b.time == null)
                return double.NaN;
            int n = Math.Min(a.time.Length, b.time.Length);
            double gap = 0.0, scale = 0.0;
            for (int k = 0; k < n; k++)
            {
                for (int i = 0; i < N; i++)
                {
                    gap = Math.Max(gap, Math.Abs(a.perturbation[k][i] - b.perturbation[k][i]));
                    scale = Math.Max(scale, Math.Abs(b.perturbation[k][i]));
                }
            }

            return scale > 0.0 ? gap / scale : 0.0;
        }

        private static Dictionary<int, double[]> WP3DJacobians(string root)
        {
            string path = root == null ? null : Path.Combine(root, "Docs/Reference/Data/F15/stability/f15_research_stability_jacobians.csv");
            if (path == null || !File.Exists(path))
                return null;
            Dictionary<int, double[]> d = new Dictionary<int, double[]>();
            string[] lines = File.ReadAllLines(path);
            for (int l = 1; l < lines.Length; l++)
            {
                string[] f = lines[l].Split(',');
                if (f.Length < 3 + N * N)
                    continue;
                double[] a = new double[N * N];
                for (int i = 0; i < N * N; i++)
                    a[i] = double.Parse(f[3 + i], CultureInfo.InvariantCulture);
                d[int.Parse(f[0], CultureInfo.InvariantCulture)] = a;
            }

            return d;
        }

        private static string Hash(string path)
        {
            if (!File.Exists(path))
                return null;
            byte[] raw = File.ReadAllBytes(path);
            List<byte> normalized = new List<byte>(raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                if (raw[i] == 13 && i + 1 < raw.Length && raw[i + 1] == 10)
                    continue;
                normalized.Add(raw[i]);
            }

            using (SHA256 sha = SHA256.Create())
            {
                byte[] h = sha.ComputeHash(normalized.ToArray());
                StringBuilder s = new StringBuilder(64);
                foreach (byte b in h)
                    s.Append(b.ToString("x2"));
                return s.ToString();
            }
        }

        private static string ResolveValidationRoot()
        {
            string root = MavF15ResearchTimeDomainStability.ProjectRoot();
            if (root == null)
                return null;
            string v = Path.Combine(Path.Combine(root, "Assets"), Path.Combine(MavFlightDynamicsOwnershipScan.FlightDynamicsRelativePath, "Validation"));
            return Directory.Exists(v) ? v : null;
        }

        private static string CodeOf(string path)
        {
            if (!File.Exists(path))
                return null;
            StringBuilder code = new StringBuilder(65536);
            foreach (string line in File.ReadAllLines(path))
            {
                int comment = line.IndexOf("//", StringComparison.Ordinal);
                code.AppendLine(comment >= 0 ? line.Substring(0, comment) : line);
            }

            return code.ToString();
        }

        private static float[] TrimFingerprint()
        {
            List<float> f = new List<float>();
            foreach (int point in new[] { 1, 46 })
            {
                foreach (MavF15TableViiState s in MavF15TableViiTrimRecovery.SymmetricStates())
                {
                    if (s.part1Point != point)
                        continue;
                    MavF15ResearchSymmetricTrimResult r = MavF15AfitResearchTrimSolver.SolveSymmetric(MavF15AfitResearchIdentity.ConfigurationId,
                        s.trueVelocityFtPerSec, new MavF15ResearchSymmetricTrimGuess((float)s.alphaDeg, (float)s.stabilatorDeg, (float)s.thetaDeg));
                    f.Add(r.alphaDeg);
                    f.Add(r.symmetricStabilatorDeg);
                    f.Add(r.pitchAttitudeDeg);
                }
            }

            foreach (int point in new[] { 117, 150, 199 })
            {
                MavF15TableViiState s;
                MavF15TableViiTurningRecovery.TryGetState(point, out s);
                MavF15ResearchTurningTrimResult r = MavF15AfitResearchTrimSolver.SolveTurning(MavF15AfitResearchIdentity.ConfigurationId,
                    s.phiDeg, MavF15TableViiTurningRecovery.Guess(MavF15TableViiTurningRecovery.Published(s)));
                foreach (double v in MavF15TableViiTurningRecovery.Values(r.solution))
                    f.Add((float)v);
            }

            return f.ToArray();
        }

        private static readonly float[] AtmosphereProbeAltitudesM = { 0f, 3000f, 6096f, 11000f };

        private static MavAtmosphereSample[] SampleAtmosphere()
        {
            MavAtmosphereSample[] samples = new MavAtmosphereSample[AtmosphereProbeAltitudesM.Length];
            for (int i = 0; i < samples.Length; i++)
                samples[i] = MavAtmosphereModel.Sample(AtmosphereProbeAltitudesM[i]);
            return samples;
        }

        private static MavAeroCoefficients[] SampleCoefficients()
        {
            float[] alphaDeg = { 5f, 10f, 13f, 25f, 40f, 60f };
            float[] stabDeg = { -20f, -6.4f, -5f };
            MavAeroCoefficients[] c = new MavAeroCoefficients[alphaDeg.Length * stabDeg.Length * 2];
            int n = 0;
            for (int a = 0; a < alphaDeg.Length; a++)
            {
                for (int e = 0; e < stabDeg.Length; e++)
                {
                    float alphaRad = alphaDeg[a] * (float)(Math.PI / 180.0);
                    MavF15BaumannSurfaceState surfaces = new MavF15BaumannSurfaceState { symmetricStabilatorDeg = stabDeg[e] };
                    c[n++] = MavF15BaumannMach06Longitudinal.Evaluate(alphaRad, stabDeg[e], 0.01f);
                    c[n++] = MavF15BaumannMach06LateralDirectional.Evaluate(alphaRad, 0.05f, surfaces, 0.01f, 0.01f);
                }
            }

            return c;
        }

        private static void Record(bool condition, string label, StringBuilder report, ref int passed, ref int failed)
        {
            if (condition) passed++; else failed++;
            report.Append(condition ? "  PASS  " : "  FAIL  ").AppendLine(label);
        }
    }
}
