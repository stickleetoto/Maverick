using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using MaverickFresh.FlightDynamics.F15;

namespace MaverickFresh.FlightDynamics.Validation
{
    /// <summary>
    /// WP-3D: local stability of the AFIT/Baumann/Davison RESEARCH SOURCE MODEL at the 170 Table VII
    /// equilibria recovered in WP-3B/C, from the source's own time-derivative system (FUNX), never
    /// from the trim residual.
    ///
    ///   [S1]  source semantics: state order, units, K constants as printed, what "stable" means
    ///   [S2]  the source RHS evaluates finite and pure
    ///   [S3]  the RHS equals an independent derivation from the WP-3A Newton-Euler evaluator
    ///   [S4]  p-dot / r-dot: the source's K-constant form equals the explicit inertia inverse
    ///   [S5]  x-dot at all 170 equilibria: inside the solver-termination bound (recovered) and the
    ///         print floor (printed)
    ///   [S6]  isolation: NASA 836 refused; no F100, atmosphere or propulsion dependency; runtime
    ///         code never names the eigensolver
    ///   [S7]  the Jacobian and its eigenvalues are deterministic
    ///   [S8]  Jacobian structure: symmetric-state decoupling and the analytic kinematic rows (units)
    ///   [S9]  the eigensolver: known spectra, trace/determinant identities, eigenvector residuals
    ///   [S10] direct and independently differenced scaled Jacobians give the same eigenvalues
    ///   [S11] finite-difference step audit: the chosen steps sit on a plateau
    ///   [S12] classification of all 170, with the NUMERICAL zero band
    ///   [S13] source vs computed stability, and what explains every disagreement
    ///   [S14] the folds: a real eigenvalue crosses zero where the stabilator is extremal
    ///   [S15] the pitchfork, from the symmetric and both turning branches
    ///   [S16] mirrored turning equilibria have matching spectra
    ///   [S17] mode tracking along both branches; crossings and degeneracies
    ///   [S18] Hopf crossings on the symmetric branch vs the Hopf points the sources state
    ///   [S19] coefficients, CFX2, atmosphere and trim results unchanged
    ///
    /// RESEARCH SOURCE MODEL ONLY. Nothing here is the real F-15, NASA 836, the production FCS or a
    /// Unity Rigidbody. The only asserted tolerances are numerical (implementation identity,
    /// eigensolver round-off, the finite-difference plateau); the zero band that decides NEAR_NEUTRAL
    /// is numerical too. No source-fidelity threshold exists: agreement is reported, not judged.
    /// </summary>
    public static class MavF15ResearchStabilityValidation
    {
        private const double DegToRad = Math.PI / 180.0;
        private const double RadToDeg = 180.0 / Math.PI;
        private const int N = MavF15ResearchStabilityAnalysis.N;

        /// <summary>Double-precision identity between two implementations of the same equations.</summary>
        private const double ImplementationIdentity = 1e-12;

        /// <summary>The largest relative gap accepted between a derived K constant and its 10-digit printing.</summary>
        private const double PrintedConstantIdentity = 1e-7;

        /// <summary>Eigensolver round-off accepted on a matrix with a known spectrum, relative to max(1, |lambda|).</summary>
        private const double EigenSolverIdentity = 1e-10;

        /// <summary>
        /// Plateau: the plateau-neighbour steps (x3, x1/3) may move an eigenvalue by at most this
        /// fraction of max(|lambda|, PlateauFloorPerSec). NUMERICAL. The same criterion decides when two
        /// independently computed spectra are numerically indistinguishable.
        /// </summary>
        private const double PlateauRelative = 1e-3;

        /// <summary>
        /// An eigenvalue's ABSOLUTE differencing error scales with the Jacobian's own error, |dA|, not
        /// with |lambda|: a near-zero eigenvalue (a fold, the pitchfork) carries the same absolute error
        /// as its neighbours. The floor is the spectrum's slowest non-trivial scale, the phugoid-like
        /// frequency (~0.1 rad/s). NUMERICAL.
        /// </summary>
        private const double PlateauFloorPerSec = 0.1;

        /// <summary>
        /// Below this, an x-dot component and its print floor are both continuation noise (the symmetric
        /// rows print their lateral states as ~1e-19..1e-21): WP-3A's "numerically zero" case, where a
        /// ratio carries no information.
        /// </summary>
        private const double NumericallyZero = 1e-15;

        private static readonly float[] AtmosphereProbeAltitudesM = { 0f, 3000f, 6096f, 11000f };

        /// <summary>Printed K constants, Baumann D2ICCV28, DTIC ADA217366 PDF p.92 (read on the page image).</summary>
        private static readonly int[] PrintedKIndex = { 1, 5, 7, 8, 9, 10, 12, 13, 14, 15, 17 };

        private static readonly double[] PrintedKValue =
        {
            3.350088890e-04, -3.924646781e-02, -5.349596105e-03, 3.685650971e-05, 0.96897131196,
            -6.001680471e-03, 0.79747314581, -9.615755341e-03, 6.472745847e-04, -0.754990553922, 8.822851558e-05
        };

        /// <summary>The source's own difference steps (PDF p.93): DX0 = 1e-9 times these, in source units.</summary>
        private static readonly double[] SourceDxMultiplier = { 50.0, 10.0, 0.5, 0.25, 0.5, 50.0, 50.0, 0.5 };

        private static readonly int[] StepAuditPoints = { 47, 20, 46, 117, 124, 130, 150, 165, 199 };

        /// <summary>Everything the suite and the dataset export share, computed once.</summary>
        public sealed class Sweep
        {
            public List<MavF15ResearchEquilibrium> equilibria;
            public List<MavF15ResearchLinearization> all;
            public List<MavF15ResearchLinearization> symmetricBranch;
            public List<MavF15ResearchLinearization> turningBranch;
            public List<MavF15ResearchPitchforkApproachPoint> pitchfork;
            public MavF15ResearchCrossing pitchforkCrossing;
            public MavF15ResearchCrossing[] folds;
            public int[][] foldBrackets;
            public MavF15ResearchCrossing hopfShortPeriodTable;
            public MavF15ResearchCrossing hopfShortPeriodFork;
            public MavF15ResearchCrossing hopfLateral;
        }

        public static Sweep RunSweep()
        {
            Sweep s = new Sweep
            {
                equilibria = MavF15ResearchStabilityAnalysis.SourceEquilibria(),
                all = new List<MavF15ResearchLinearization>(170)
            };

            foreach (MavF15ResearchEquilibrium eq in s.equilibria)
            {
                MavF15ResearchLinearization lin = MavF15ResearchStabilityAnalysis.Linearize(MavF15AfitResearchIdentity.ConfigurationId, eq);
                MavF15ResearchStabilityAnalysis.AddPrintSpread(ref lin);
                s.all.Add(lin);
            }

            // Symmetric branch in stabilator order (the continuation parameter); the printed duplicates
            // (points 51-91 repeat 1-41) stay in, adjacent to their twins.
            s.symmetricBranch = new List<MavF15ResearchLinearization>();
            s.turningBranch = new List<MavF15ResearchLinearization>();
            foreach (MavF15ResearchLinearization l in s.all)
            {
                if (l.equilibrium.branch == MavF15ResearchEquilibriumBranch.Symmetric)
                    s.symmetricBranch.Add(l);
                else
                    s.turningBranch.Add(l);
            }

            s.symmetricBranch.Sort((a, b) =>
            {
                int c = b.equilibrium.stabilatorDeg.CompareTo(a.equilibrium.stabilatorDeg);
                return c != 0 ? c : a.equilibrium.point.CompareTo(b.equilibrium.point);
            });
            MavF15ResearchStabilityAnalysis.Track(s.symmetricBranch, 0);

            int forkIndex = 0;
            for (int i = 0; i < s.turningBranch.Count; i++)
            {
                if (s.turningBranch[i].equilibrium.branch == MavF15ResearchEquilibriumBranch.Pitchfork)
                    forkIndex = i;
            }

            MavF15ResearchStabilityAnalysis.Track(s.turningBranch, forkIndex);

            // Write the tracked names back into the all-list (same equilibria, same order of points).
            Dictionary<int, MavF15ResearchLinearization> byPoint = new Dictionary<int, MavF15ResearchLinearization>();
            foreach (MavF15ResearchLinearization l in s.symmetricBranch)
                byPoint[l.equilibrium.point] = l;
            foreach (MavF15ResearchLinearization l in s.turningBranch)
                byPoint[l.equilibrium.point] = l;
            for (int i = 0; i < s.all.Count; i++)
                s.all[i] = byPoint[s.all[i].equilibrium.point];

            s.pitchfork = MavF15ResearchStabilityAnalysis.PitchforkApproach();

            MavF15TableViiState fork = TurningState(MavF15TableViiTurningRecovery.PitchforkPoint());
            s.pitchforkCrossing = MavF15ResearchStabilityAnalysis.LocateSymmetricCrossing(
                fork, fork.trueVelocityFtPerSec - 0.1, fork.trueVelocityFtPerSec + 0.2, true, 30);

            // Folds bracketed by the printed neighbours of the printed stabilator extrema; the start
            // is the printed extremum itself (194 is unavailable, so its bracket starts from 193).
            s.foldBrackets = new[] { new[] { 123, 125, 124 }, new[] { 135, 137, 136 }, new[] { 181, 183, 182 }, new[] { 193, 195, 193 } };
            s.folds = new MavF15ResearchCrossing[s.foldBrackets.Length];
            for (int i = 0; i < s.foldBrackets.Length; i++)
            {
                s.folds[i] = MavF15ResearchStabilityAnalysis.LocateTurningFold(
                    TurningState(s.foldBrackets[i][2]), TurningState(s.foldBrackets[i][0]).phiDeg,
                    TurningState(s.foldBrackets[i][1]).phiDeg, 30);
            }

            // Hopf crossings, each bracketed by the first stable/unstable pair found in the sweep or
            // the approach, and located by WP-3B solves in V (no continuation).
            s.hopfShortPeriodTable = MavF15ResearchStabilityAnalysis.LocateSymmetricCrossing(
                SymmetricState(9), SymmetricState(9).trueVelocityFtPerSec, SymmetricState(8).trueVelocityFtPerSec, false, 30);
            s.hopfShortPeriodFork = MavF15ResearchStabilityAnalysis.LocateSymmetricCrossing(
                fork, fork.trueVelocityFtPerSec - 10.0, fork.trueVelocityFtPerSec - 5.0, false, 30);
            s.hopfLateral = MavF15ResearchStabilityAnalysis.LocateSymmetricCrossing(
                SymmetricState(46), 275.0, 280.0, false, 30);
            return s;
        }

        public static string RunAll(out int passed, out int failed)
        {
            passed = 0;
            failed = 0;
            StringBuilder report = new StringBuilder(131072);
            report.AppendLine("F-15 Research Source Stability (WP-3D): eigenvalues of the source's own x-dot = f(x, stabilator)");
            report.AppendLine("==============================================================================================");
            report.AppendLine(MavF15AfitResearchSourceDynamics.Scope);
            report.AppendLine("SOURCE MODEL stability only: not the real F-15, not NASA 836, not the production FCS, not a Unity Rigidbody.");
            report.AppendLine("No source-fidelity pass threshold: agreement with the source is reported, not judged.");

            MavAtmosphereSample[] atmosphereBefore = SampleAtmosphere();
            MavAeroCoefficients[] coefficientsBefore = SampleCoefficients();
            float[] trimBefore = TrimFingerprint();

            Sweep s = RunSweep();

            ValidateSourceSemantics(report, ref passed, ref failed);
            ValidateRhsFinite(s, report, ref passed, ref failed);
            ValidateRhsIndependent(s, report, ref passed, ref failed);
            ValidateInertiaInversion(report, ref passed, ref failed);
            ValidateEquilibriumDerivatives(s, report, ref passed, ref failed);
            ValidateIsolation(report, ref passed, ref failed);
            ValidateDeterminism(s, report, ref passed, ref failed);
            ValidateJacobianStructure(s, report, ref passed, ref failed);
            ValidateEigenSolver(s, report, ref passed, ref failed);
            ValidateScaledJacobian(s, report, ref passed, ref failed);
            ValidateStepAudit(s, report, ref passed, ref failed);
            ValidateClassification(s, report, ref passed, ref failed);
            ValidateSourceComparison(s, report, ref passed, ref failed);
            ValidateFolds(s, report, ref passed, ref failed);
            ValidatePitchfork(s, report, ref passed, ref failed);
            ValidateMirror(s, report, ref passed, ref failed);
            ValidateModeTracking(s, report, ref passed, ref failed);
            ValidateHopf(s, report, ref passed, ref failed);
            ValidateUnchanged(atmosphereBefore, coefficientsBefore, trimBefore, report, ref passed, ref failed);

            report.AppendLine();
            report.Append("RESULT: ")
                .Append(failed == 0 ? "PASS" : "FAIL")
                .Append("  passed=").Append(passed)
                .Append(" failed=").Append(failed);
            return report.ToString();
        }

        /// <summary>
        /// The machine-readable dataset, as delimited CSV blocks for extraction from the batch log:
        /// one row per eigenvalue per equilibrium, the 170 Jacobians, the step audit and the pitchfork
        /// approach. Research source model only.
        /// </summary>
        public static string ExportDataset(out int passed, out int failed)
        {
            Sweep s = RunSweep();
            StringBuilder o = new StringBuilder(1 << 20);
            Block(o, "f15_research_stability_table_vii.csv", MavF15ResearchStabilityAnalysis.DatasetCsv(s.all));
            Block(o, "f15_research_stability_jacobians.csv", MavF15ResearchStabilityAnalysis.JacobianCsv(s.all));
            Block(o, "f15_research_stability_step_audit.csv", StepAuditCsv(s));
            Block(o, "f15_research_stability_pitchfork_approach.csv", PitchforkCsv(s));
            Block(o, "f15_research_stability_crossings.csv", CrossingsCsv(s));

            int evaluated = 0;
            foreach (MavF15ResearchLinearization l in s.all)
            {
                if (l.evaluated)
                    evaluated++;
            }

            passed = evaluated == 170 ? 1 : 0;
            failed = 1 - passed;
            o.Append("RESULT: ").Append(failed == 0 ? "PASS" : "FAIL").Append("  passed=").Append(passed).Append(" failed=").Append(failed);
            return o.ToString();
        }

        // ---------------------------------------------------------------- [S1]

        private static void ValidateSourceSemantics(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[S1] Source semantics (audit: F15_RESEARCH_STABILITY_ANALYSIS_V1.0.md §1)");
            report.AppendLine("    state (FUNX U(1..8), Baumann PDF p.96-97): alpha deg, beta deg, p q r rad/s, theta deg, phi deg, V/1000 ft/s;");
            report.AppendLine("    here the same order in rad, rad/s, ft/s - a constant diagonal similarity of the source's own coordinates");
            report.AppendLine("    stable (Baumann PDF p.25, 32, 54): all eigenvalues of the F(1..8) Jacobian in the left half plane, read from");
            report.AppendLine("    AUTO's Fort.9; eigenvalues are NOT printed anywhere in either thesis; Table VII's caption (PDF p.124) calls");
            report.AppendLine("    every row a 'stable low alpha equilibrium'; Davison (ADA256613 PDF p.19): stable if the eigenvalues are");
            report.AppendLine("    'negative or zero'; a limit point is a real eigenvalue crossing, a change of stability (p.21)");

            string[] names = MavF15AfitResearchSourceDynamics.StateNames;
            Record(names.Length == 8 && names[0] == "alpha" && names[1] == "beta" && names[2] == "p" && names[3] == "q"
                   && names[4] == "r" && names[5] == "theta" && names[6] == "phi" && names[7] == "V",
                "state order is FUNX's: alpha, beta, p, q, r, theta, phi, V; psi is not a state (Baumann eq. 3.17 dropped)",
                report, ref passed, ref failed);

            MavF15ResearchSourceKConstants k = MavF15AfitResearchSourceDynamics.KConstants();
            double[] derived = KArray(k);
            double worst = 0.0;
            int worstIndex = 0;
            for (int i = 0; i < PrintedKIndex.Length; i++)
            {
                double rel = Math.Abs(derived[PrintedKIndex[i]] - PrintedKValue[i]) / Math.Abs(PrintedKValue[i]);
                if (rel > worst)
                {
                    worst = rel;
                    worstIndex = PrintedKIndex[i];
                }
            }

            double k16 = Math.Abs(k.k16 - k.k13) / Math.Abs(k.k13);
            report.Append("    K constants from RHO .0012673, S 608, b 42.8, c 15.94, 37000/32.174 slug, IX 25480, IY 166620, IZ 186930, IXZ -1000:")
                  .Append(" largest gap to the printed value K").Append(worstIndex).Append(" ").Append(worst.ToString("E2"))
                  .Append(" relative; K16 - K13 ").Append(k16.ToString("E1")).AppendLine();
            Record(worst <= PrintedConstantIdentity && k16 <= 1e-15,
                "all 11 printed K constants (K1 K5 K7-K10 K12-K15 K17) reproduce from the source's own inputs, and K16 = K13 "
                + "identically as the listing prints; the RHS uses the derived values, so it and the trim equations share every input",
                report, ref passed, ref failed);

            report.Append("    source Jacobian: central differences (PDF p.49 eq. 4.1), DX0 = 1e-9 times");
            for (int i = 0; i < N; i++)
                report.Append(' ').Append(SourceDxMultiplier[i].ToString("G3", CultureInfo.InvariantCulture));
            report.AppendLine(" in source units, double precision throughout; not reproducible with the float coefficient routine");
            report.AppendLine("    NUMERICAL steps used here (physical units): " + Join(MavF15ResearchStabilityAnalysis.DirectStep, "G3"));
            Record(MavF15ResearchStabilityAnalysis.ZeroBandFloorPerSec > 0.0 && MavF15ResearchStabilityAnalysis.ZeroBandUncertaintyMultiple >= 1.0,
                "the zero band is NUMERICAL: max(" + MavF15ResearchStabilityAnalysis.ZeroBandFloorPerSec.ToString("E0")
                + " 1/s, " + MavF15ResearchStabilityAnalysis.ZeroBandUncertaintyMultiple.ToString("F0")
                + " x the eigenvalue's own measured uncertainty); no source stability threshold exists or is invented",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [S2]

        private static void ValidateRhsFinite(Sweep s, StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[S2] The source RHS evaluates finite, and it is pure");

            int recovered = 0, finite = 0;
            foreach (MavF15ResearchEquilibrium eq in s.equilibria)
            {
                if (!eq.recovered)
                    continue;
                recovered++;
                double[] d = MavF15ResearchStabilityAnalysis.Derivative(eq.state, eq.stabilatorDeg);
                if (d != null && AllFinite(d))
                    finite++;
            }

            List<MavF15ResearchSourceState> probes = ProbeStates();
            int probeFinite = 0;
            bool pure = true;
            foreach (MavF15ResearchSourceState x in probes)
            {
                double[] a = MavF15ResearchStabilityAnalysis.Derivative(x, -8.0);
                double[] b = MavF15ResearchStabilityAnalysis.Derivative(x, -8.0);
                if (a != null && AllFinite(a))
                    probeFinite++;
                for (int i = 0; a != null && b != null && i < N; i++)
                {
                    if (a[i] != b[i])
                        pure = false;
                }
            }

            Record(recovered == 170 && finite == 170,
                "all 170 source equilibria recovered (89 symmetric + 80 turning + the pitchfork) and x-dot finite at every one",
                report, ref passed, ref failed);
            Record(probeFinite == probes.Count && pure,
                "finite and bit-identical on repeat at " + probes.Count + " off-equilibrium probe states (alpha 0-25 deg, "
                + "|beta| to 5 deg, rates to 0.5 rad/s, theta/phi to +/-60 deg, V 250-650 ft/s): no hidden state",
                report, ref passed, ref failed);

            MavF15ResearchStateDerivative bad = MavF15AfitResearchSourceDynamics.EvaluateStateDerivative(
                new MavF15ResearchSourceState { alphaRad = 0.1, trueAirspeedFtPerSec = 0.0 }, -8.0);
            MavF15ResearchStateDerivative outside = MavF15AfitResearchSourceDynamics.EvaluateStateDerivative(
                new MavF15ResearchSourceState { alphaRad = 0.1, trueAirspeedFtPerSec = 400.0 }, -60.0);
            Record(!bad.evaluated && !outside.evaluated,
                "refuses V = 0 and a stabilator outside the research demonstrated range: " + outside.reason,
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [S3]

        private static void ValidateRhsIndependent(Sweep s, StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[S3] FUNX equals an independent derivation from the WP-3A Newton-Euler evaluator");
            report.AppendLine("    independent: u', v', w' from WP-3A's force residuals x g; V' = (u u' + v v' + w w')/V; alpha' = (u w' - w u')/(u^2+w^2);");
            report.AppendLine("    beta' = (V v' - v V')/(V^2 cos b); p', r' by inverting [Ix -Ixz; -Ixz Iz]; q' = M/Iy; theta', phi' as printed");

            double worst = 0.0;
            int n = 0;
            string worstWhere = "";
            foreach (MavF15ResearchEquilibrium eq in s.equilibria)
            {
                Compare(MavF15ResearchStabilityAnalysis.ThroughDegrees(eq.state), eq.stabilatorDeg, "recovered " + eq.point, ref worst, ref worstWhere, ref n);
                Compare(MavF15ResearchStabilityAnalysis.PrintedState(eq.published), eq.published.stabilatorDeg, "printed " + eq.point, ref worst, ref worstWhere, ref n);
            }

            foreach (MavF15ResearchSourceState x in ProbeStates())
                Compare(MavF15ResearchStabilityAnalysis.ThroughDegrees(x), -8.0, "probe", ref worst, ref worstWhere, ref n);

            report.Append("    ").Append(n).Append(" states: largest |FUNX - independent| / (1 + |x-dot|) = ").Append(worst.ToString("E2"))
                  .Append(" (").Append(worstWhere).AppendLine(")");
            Record(n == 340 + ProbeStates().Count && worst <= ImplementationIdentity,
                "identical to double precision at the 170 recovered equilibria, the 170 printed rows and every off-equilibrium "
                + "probe: the wind-axis FUNX form and the body-axis Newton-Euler form are the same dynamics",
                report, ref passed, ref failed);
        }

        private static void Compare(MavF15ResearchSourceState x, double stab, string where, ref double worst, ref string worstWhere, ref int n)
        {
            double[] a = MavF15ResearchStabilityAnalysis.Derivative(x, stab);
            double[] b = MavF15ResearchStabilityAnalysis.IndependentDerivative(x, stab);
            if (a == null || b == null)
            {
                worst = double.PositiveInfinity;
                worstWhere = where + " not evaluated";
                return;
            }

            n++;
            for (int i = 0; i < N; i++)
            {
                double d = Math.Abs(a[i] - b[i]) / (1.0 + Math.Abs(a[i]));
                if (d > worst)
                {
                    worst = d;
                    worstWhere = where + " " + MavF15AfitResearchSourceDynamics.StateNames[i] + "-dot";
                }
            }
        }

        // ---------------------------------------------------------------- [S4]

        private static void ValidateInertiaInversion(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[S4] p-dot and r-dot: the source's K-constant form equals the explicit inverse of [Ix -Ixz; -Ixz Iz]");

            MavF15ResearchSourceKConstants k = MavF15AfitResearchSourceDynamics.KConstants();
            double ix = MavF15AfitResearchMassReference.IxxSlugFt2, iy = MavF15AfitResearchMassReference.IyySlugFt2;
            double iz = MavF15AfitResearchMassReference.IzzSlugFt2, ixz = MavF15AfitResearchMassReference.IxzSlugFt2;
            double gamma = ix * iz - ixz * ixz;
            double[] forms =
            {
                k.k12 - (iz * (iz - iy) + ixz * ixz) / gamma,
                k.k13 - ixz * (ix - iy + iz) / gamma,
                k.k15 - (ix * (ix - iy) + ixz * ixz) / gamma,
                k.k16 - ixz * (ix - iy + iz) / gamma,
                k.k14 - k.k6 * ix * iz / gamma,
                k.k17 - k.k11 * ix * iz / gamma,
                k.k9 - (iz - ix) / iy,
                k.k10 - ixz / iy
            };
            double worst = 0.0;
            foreach (double f in forms)
                worst = Math.Max(worst, Math.Abs(f));
            report.Append("    K12 = (Iz(Iz-Iy)+Ixz^2)/G, K13 = K16 = Ixz(Ix-Iy+Iz)/G, K15 = (Ix(Ix-Iy)+Ixz^2)/G, K14 = K6 IxIz/G, ")
                  .Append("K17 = K11 IxIz/G, G = IxIz - Ixz^2: largest gap ").Append(worst.ToString("E1")).AppendLine();
            Record(worst <= 1e-15,
                "every inertia K constant is the corresponding entry of the standard inverted Newton-Euler moment equations; "
                + "the '(1.-K4)' read on the page image is the only form consistent with the printed K13",
                report, ref passed, ref failed);

            double worstRate = 0.0;
            int n = 0;
            foreach (MavF15ResearchSourceState x0 in ProbeStates())
            {
                MavF15ResearchSourceState x = MavF15ResearchStabilityAnalysis.ThroughDegrees(x0);
                double[] a = MavF15ResearchStabilityAnalysis.Derivative(x, -8.0);
                double[] b = MavF15ResearchStabilityAnalysis.IndependentDerivative(x, -8.0);
                if (a == null || b == null)
                    continue;
                n++;
                worstRate = Math.Max(worstRate, Math.Abs(a[2] - b[2]) / (1.0 + Math.Abs(a[2])));
                worstRate = Math.Max(worstRate, Math.Abs(a[4] - b[4]) / (1.0 + Math.Abs(a[4])));
            }

            Record(n == ProbeStates().Count && worstRate <= ImplementationIdentity,
                "at " + n + " probe states with p, q, r all nonzero, FUNX's p-dot and r-dot equal the explicit inverse to "
                + worstRate.ToString("E1") + " - the Ixz coupling is inverted once, correctly",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [S5]

        private static void ValidateEquilibriumDerivatives(Sweep s, StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[S5] x-dot at all 170 source equilibria");

            double[] maxRecovered = new double[N], maxRatio = new double[N];
            double[] maxPrinted = new double[N], maxPrintedRatio = new double[N];
            string[] classes = { "symmetric", "turning", "pitchfork" };
            double[,] byClass = new double[3, N];
            int insideRecovered = 0, insidePrinted = 0, printedEvaluated = 0, numericallyZero = 0;
            foreach (MavF15ResearchLinearization lin in s.all)
            {
                MavF15ResearchEquilibrium eq = lin.equilibrium;
                double[] d = lin.derivative;
                double[] bound = MavF15ResearchStabilityAnalysis.TerminationBound(eq.state);
                bool inside = true;
                for (int i = 0; i < N; i++)
                {
                    maxRecovered[i] = Math.Max(maxRecovered[i], Math.Abs(d[i]));
                    maxRatio[i] = Math.Max(maxRatio[i], Math.Abs(d[i]) / bound[i]);
                    byClass[(int)eq.branch, i] = Math.Max(byClass[(int)eq.branch, i], Math.Abs(d[i]));
                    if (Math.Abs(d[i]) > bound[i])
                        inside = false;
                }

                if (inside)
                    insideRecovered++;

                MavF15ResearchSourceState printed = MavF15ResearchStabilityAnalysis.PrintedState(eq.published);
                double[,] a;
                double[] column;
                double[] dp = MavF15ResearchStabilityAnalysis.Derivative(printed, eq.published.stabilatorDeg);
                if (dp == null || !MavF15ResearchStabilityAnalysis.Jacobian(printed, eq.published.stabilatorDeg, 1.0, out a)
                    || !MavF15ResearchStabilityAnalysis.StabilatorColumn(printed, eq.published.stabilatorDeg, out column))
                    continue;
                printedEvaluated++;
                double[] floor = MavF15ResearchStabilityAnalysis.PrintFloor(eq.published, a, column);
                bool insideFloor = true;
                for (int i = 0; i < N; i++)
                {
                    maxPrinted[i] = Math.Max(maxPrinted[i], Math.Abs(dp[i]));
                    if (Math.Abs(dp[i]) <= NumericallyZero && floor[i] <= NumericallyZero)
                    {
                        numericallyZero++;
                        continue;
                    }

                    if (floor[i] > 0.0)
                        maxPrintedRatio[i] = Math.Max(maxPrintedRatio[i], Math.Abs(dp[i]) / floor[i]);
                    if (Math.Abs(dp[i]) > floor[i])
                        insideFloor = false;
                }

                if (insideFloor)
                    insidePrinted++;
            }

            string[] units = { "rad/s", "rad/s", "rad/s^2", "rad/s^2", "rad/s^2", "rad/s", "rad/s", "ft/s^2" };
            report.AppendLine("    state-dot     | recovered: max |x-dot|  sym / turn / fork       max/bound | printed row: max |x-dot|  max/floor");
            for (int i = 0; i < N; i++)
            {
                report.Append("    ").Append((MavF15AfitResearchSourceDynamics.StateNames[i] + "-dot " + units[i]).PadRight(14))
                      .Append(" ").Append(maxRecovered[i].ToString("E2")).Append("   ")
                      .Append(byClass[0, i].ToString("E1")).Append(" / ").Append(byClass[1, i].ToString("E1")).Append(" / ")
                      .Append(byClass[2, i].ToString("E1")).Append("   ").Append(maxRatio[i].ToString("F3"))
                      .Append("  | ").Append(maxPrinted[i].ToString("E2")).Append("   ").Append(maxPrintedRatio[i].ToString("F3")).AppendLine();
            }

            report.AppendLine("    bound = the x-dot the recovering solver's termination epsilon (" + MavF15AfitResearchTrimSolver.NumericalSolverTolerance.ToString("E0")
                              + " on each normalized trim residual) allows; floor = sum |df/dfield| x half a unit in each printed field's last digit");
            report.Append("    printed max/floor excludes ").Append(numericallyZero).Append(" components where x-dot and floor are both below ")
                  .Append(NumericallyZero.ToString("E0")).AppendLine(": the symmetric rows' lateral states and q are printed as ~1e-19..1e-31 "
                  + "continuation noise (WP-3A's 'numerically zero'), so a ratio there carries no information");
            Record(insideRecovered == 170,
                "at all 170 recovered equilibria every component of x-dot is inside the solver-termination bound (" + insideRecovered
                + "/170): they are equilibria of the SOURCE DYNAMICS, not just of the trim equations",
                report, ref passed, ref failed);
            Record(printedEvaluated == 170 && insidePrinted == 170,
                "at all 170 PRINTED rows every component of x-dot that is not numerically zero is inside its print floor (" + insidePrinted
                + "/" + printedEvaluated + "): the published digits are equilibria of the source dynamics to print precision; nothing was tuned",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [S6]

        private static void ValidateIsolation(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[S6] Isolation: the research stability path only, no F100, no atmosphere model, no eigensolver in runtime code");

            MavF15ResearchEquilibrium eq = MavF15ResearchStabilityAnalysis.Symmetric(SymmetricState(20), SymmetricState(20).trueVelocityFtPerSec,
                MavF15ResearchEquilibriumBranch.Symmetric);
            MavF15ResearchLinearization exact = MavF15ResearchStabilityAnalysis.Linearize(MavF15ReferenceData.TargetConfigurationId, eq);
            MavF15ResearchLinearization empty = MavF15ResearchStabilityAnalysis.Linearize("", eq);
            Record(!exact.evaluated && !empty.evaluated && exact.modes == null,
                "refused under the exact NASA 836 id and an empty id: " + exact.reason,
                report, ref passed, ref failed);

            string root = ResolveFlightDynamicsRoot();
            string rhs = root == null ? null : CodeOf(Path.Combine(root, Path.Combine("F15", "MavF15AfitResearchSourceDynamics.cs")));
            string[] forbidden = { "MavF100", "MavThrustDeck", "MavPropulsionModelBase", "MavPropulsiveLoads", "MavAtmosphereModel",
                                   "MavAtmosphereSample", "MavSixDoFBody", "UnityEngine", "MavValidationEigenSolver", "MavF15ResearchStabilityAnalysis" };
            string hit = null;
            for (int i = 0; rhs != null && i < forbidden.Length; i++)
            {
                if (rhs.IndexOf(forbidden[i], StringComparison.Ordinal) >= 0)
                    hit = forbidden[i];
            }

            Record(rhs != null && hit == null,
                hit == null
                    ? "the source RHS names no F100, thrust deck, propulsion model, atmosphere model, 6DoF body, UnityEngine or eigensolver"
                    : "VIOLATION: the RHS names " + hit,
                report, ref passed, ref failed);

            string offender = null;
            int scanned = 0;
            if (root != null)
            {
                string validation = Path.GetFullPath(Path.Combine(root, "Validation")) + Path.DirectorySeparatorChar;
                foreach (string f in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    string full = Path.GetFullPath(f);
                    if (full.StartsWith(validation, StringComparison.OrdinalIgnoreCase))
                        continue;
                    scanned++;
                    string text = File.ReadAllText(full);
                    if (text.IndexOf("MavValidationEigenSolver", StringComparison.Ordinal) >= 0
                        || text.IndexOf("MavF15ResearchStabilityAnalysis", StringComparison.Ordinal) >= 0)
                        offender = Path.GetFileName(full);
                }
            }

            Record(root != null && scanned > 0 && offender == null,
                offender == null
                    ? "no runtime or editor source outside Validation/ (" + scanned + " scanned) names the eigensolver or the stability analysis"
                    : "VIOLATION: " + offender,
                report, ref passed, ref failed);

            string callers = null;
            if (root != null)
            {
                string validation = Path.GetFullPath(Path.Combine(root, "Validation")) + Path.DirectorySeparatorChar;
                string self = Path.GetFullPath(Path.Combine(root, Path.Combine("F15", "MavF15AfitResearchSourceDynamics.cs")));
                foreach (string f in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    string full = Path.GetFullPath(f);
                    if (full.StartsWith(validation, StringComparison.OrdinalIgnoreCase) || string.Equals(full, self, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (File.ReadAllText(full).IndexOf("MavF15AfitResearchSourceDynamics", StringComparison.Ordinal) >= 0)
                        callers = Path.GetFileName(full);
                }
            }

            Record(root != null && callers == null,
                callers == null
                    ? "nothing outside Validation/ calls the source RHS: no flight path, profile or body can reach it"
                    : "VIOLATION: " + callers + " calls the source RHS",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [S7]

        private static void ValidateDeterminism(Sweep s, StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[S7] The Jacobian and its eigenvalues are deterministic");

            int same = 0, checkedCount = 0;
            foreach (MavF15ResearchLinearization lin in s.all)
            {
                if (!lin.evaluated)
                    continue;
                checkedCount++;
                double[,] a;
                double[] re, im;
                if (!MavF15ResearchStabilityAnalysis.Jacobian(lin.equilibrium.state, lin.equilibrium.stabilatorDeg, 1.0, out a)
                    || !MavValidationEigenSolver.Eigenvalues(a, out re, out im))
                    continue;
                bool identical = true;
                for (int i = 0; i < N; i++)
                {
                    for (int j = 0; j < N; j++)
                    {
                        if (a[i, j] != lin.jacobian[i, j])
                            identical = false;
                    }
                }

                double[] r0, i0;
                MavValidationEigenSolver.Eigenvalues(lin.jacobian, out r0, out i0);
                for (int k = 0; k < N; k++)
                {
                    if (re[k] != r0[k] || im[k] != i0[k])
                        identical = false;
                }

                if (identical)
                    same++;
            }

            MavF15ResearchEquilibrium again = MavF15ResearchStabilityAnalysis.Turning(TurningState(150), TurningState(150).phiDeg);
            MavF15ResearchLinearization first = s.all.Find(l => l.equilibrium.point == 150);
            MavF15ResearchLinearization second = MavF15ResearchStabilityAnalysis.Linearize(MavF15AfitResearchIdentity.ConfigurationId, again);
            bool rerun = first.evaluated && second.evaluated;
            for (int k = 0; rerun && k < N; k++)
                rerun = first.modes[k].re == second.modes[Index(second, first.modes[k])].re;

            Record(checkedCount == 170 && same == 170 && rerun,
                "re-differencing reproduces A bit for bit and the eigensolver returns bit-identical eigenvalues at all 170; "
                + "re-solving and re-linearizing point 150 from scratch reproduces its spectrum exactly",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [S8]

        private static void ValidateJacobianStructure(Sweep s, StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[S8] Jacobian structure: symmetric-state decoupling, and the analytic kinematic rows (units check)");

            int[] longitudinal = { 0, 3, 5, 7 };
            int[] lateral = { 1, 2, 4, 6 };
            int symmetric = 0, decoupled = 0;
            double worstKinematic = 0.0;
            foreach (MavF15ResearchLinearization lin in s.all)
            {
                double[,] a = lin.jacobian;
                MavF15ResearchSourceState x = lin.equilibrium.state;
                if (lin.equilibrium.branch != MavF15ResearchEquilibriumBranch.Turning)
                {
                    symmetric++;
                    bool zero = true;
                    foreach (int i in longitudinal)
                    {
                        foreach (int j in lateral)
                        {
                            if (a[i, j] != 0.0 || a[j, i] != 0.0)
                                zero = false;
                        }
                    }

                    if (zero)
                        decoupled++;
                }

                // Rows F(6), F(7) are pure kinematics: their derivatives are known in closed form.
                double sp = Math.Sin(x.phiRad), cp = Math.Cos(x.phiRad), tt = Math.Tan(x.thetaRad), ct = Math.Cos(x.thetaRad);
                double[] analytic =
                {
                    a[5, 3] - cp, a[5, 4] + sp, a[5, 6] - (-x.qRadSec * sp - x.rRadSec * cp),
                    a[6, 2] - 1.0, a[6, 3] - sp * tt, a[6, 4] - cp * tt,
                    a[6, 5] - (x.qRadSec * sp + x.rRadSec * cp) / (ct * ct),
                    a[6, 6] - (x.qRadSec * cp - x.rRadSec * sp) * tt,
                    a[5, 0], a[5, 1], a[5, 2], a[5, 5], a[5, 7], a[6, 0], a[6, 1], a[6, 7],
                    a[0, 3] - 1.0
                };
                foreach (double d in analytic)
                    worstKinematic = Math.Max(worstKinematic, Math.Abs(d));
            }

            Record(symmetric == 90 && decoupled == 90,
                "at all 90 symmetric states (89 + the pitchfork) the longitudinal (alpha, q, theta, V) and lateral (beta, p, r, phi) "
                + "blocks are exactly decoupled - every cross entry is 0.0, as the model's left-right parity requires",
                report, ref passed, ref failed);
            report.Append("    kinematic rows vs closed form (d theta-dot/dq = cos phi, d phi-dot/dp = 1, d phi-dot/d theta = (q sin phi + ")
                  .Append("r cos phi)/cos^2 theta, ... and d alpha-dot/dq = 1): largest gap ").Append(worstKinematic.ToString("E1")).AppendLine();
            Record(worstKinematic <= 1e-8,
                "the differenced kinematic rows match their closed forms at all 170: angles are in radians and rates in rad/s "
                + "throughout (a degree slip would show as a factor 57.3)",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [S9]

        private static void ValidateEigenSolver(Sweep s, StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[S9] The eigensolver (validation-side; balancing, Householder Hessenberg, complex shifted QR, inverse iteration)");

            // Known spectra, dressed by a fixed dense similarity and by the Jacobians' own scale spread.
            double[][] spectra =
            {
                new[] { -0.2, 4.0, -0.2, -4.0, -1.4, 1.0, -1.4, -1.0, -0.73, 0.0, -3e-5, 0.0, -7e-3, 0.118, -7e-3, -0.118 },
                new[] { 0.81, 0.87, 0.81, -0.87, -0.04, 0.155, -0.04, -0.155, -0.06, 0.0, -0.2, 4.1, -0.2, -4.1, -0.47, 0.0 },
                new[] { 1e-6, 0.0, -1e-6, 0.0, -2.0, 0.0, -2.0 + 1e-3, 0.0, -0.5, 2.5, -0.5, -2.5, 3.0, 0.0, -30.0, 0.0 }
            };
            double[] scales = { 1.0, 1e-3, 10.0, 1e2, 1.0, 1e-2, 1.0, 1e3 };
            double worstKnown = 0.0;
            int known = 0;
            foreach (double[] spectrum in spectra)
            {
                double[,] m = KnownMatrix(spectrum, scales);
                double[] re, im;
                if (!MavValidationEigenSolver.Eigenvalues(m, out re, out im))
                {
                    worstKnown = double.PositiveInfinity;
                    continue;
                }

                known++;
                MavF15ResearchMode[] expected = new MavF15ResearchMode[N];
                for (int k = 0; k < N; k++)
                    expected[k] = new MavF15ResearchMode { re = spectrum[2 * k], im = spectrum[2 * k + 1] };
                int[] match = MavF15ResearchStabilityAnalysis.Match(expected, re, im);
                for (int k = 0; k < N; k++)
                {
                    double d = MavF15ResearchStabilityAnalysis.Distance(expected[k].re, expected[k].im, re[match[k]], im[match[k]]);
                    worstKnown = Math.Max(worstKnown, d / Math.Max(1.0, Math.Sqrt(expected[k].re * expected[k].re + expected[k].im * expected[k].im)));
                }
            }

            Record(known == spectra.Length && worstKnown <= EigenSolverIdentity,
                "three matrices with known spectra (conjugate pairs, a 1e-6 +/- pair straddling zero, a near-double real pair) behind a "
                + "dense similarity with a 1e-3..1e3 scale spread: largest error " + worstKnown.ToString("E1") + " relative",
                report, ref passed, ref failed);

            double worstTrace = 0.0, worstDet = 0.0, worstVector = 0.0;
            foreach (MavF15ResearchLinearization lin in s.all)
            {
                worstTrace = Math.Max(worstTrace, lin.traceDefect);
                worstDet = Math.Max(worstDet, lin.determinantDefect);
                worstVector = Math.Max(worstVector, lin.largestEigenvectorResidual);
            }

            report.Append("    at the 170 Jacobians: |trace - sum(lambda)| ").Append(worstTrace.ToString("E1"))
                  .Append(", |det - prod(lambda)|/|det| ").Append(worstDet.ToString("E1"))
                  .Append(", ||A v - lambda v|| / ||A|| ").Append(worstVector.ToString("E1")).AppendLine();
            Record(worstTrace <= 1e-12 && worstDet <= 1e-6 && worstVector <= 1e-10,
                "every one of the 1,360 eigenpairs satisfies A v = lambda v to round-off, and the spectra reproduce trace and determinant "
                + "(the determinant defect is relative, and largest where one eigenvalue is ~1e-6)",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [S10]

        private static void ValidateScaledJacobian(Sweep s, StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[S10] Direct vs scaled Jacobian: z = D^-1 x, D = diag(1 deg x5... 10 ft/s), differenced independently in z");

            double worstScaled = 0.0, worstRelative = 0.0, worstAnalytic = 0.0;
            int within = 0, total = 0;
            foreach (MavF15ResearchLinearization lin in s.all)
            {
                for (int k = 0; k < N; k++)
                {
                    MavF15ResearchMode m = lin.modes[k];
                    total++;
                    worstScaled = Math.Max(worstScaled, m.scaledDifference);
                    worstRelative = Math.Max(worstRelative, m.scaledDifference / Math.Max(m.naturalFrequencyRadSec, PlateauFloorPerSec));
                    if (m.scaledDifference <= Math.Max(m.stepSpread, 1e-12))
                        within++;
                }

                // Pure eigensolver invariance: the analytic similarity D^-1 A D.
                double[,] az = new double[N, N];
                for (int i = 0; i < N; i++)
                {
                    for (int j = 0; j < N; j++)
                        az[i, j] = lin.jacobian[i, j] * MavF15ResearchStabilityAnalysis.ScaleMagnitude[j] / MavF15ResearchStabilityAnalysis.ScaleMagnitude[i];
                }

                double[] re, im;
                if (!MavValidationEigenSolver.Eigenvalues(az, out re, out im))
                {
                    worstAnalytic = double.PositiveInfinity;
                    continue;
                }

                int[] match = MavF15ResearchStabilityAnalysis.Match(lin.modes, re, im);
                for (int k = 0; k < N; k++)
                {
                    double d = MavF15ResearchStabilityAnalysis.Distance(lin.modes[k].re, lin.modes[k].im, re[match[k]], im[match[k]]);
                    worstAnalytic = Math.Max(worstAnalytic, d / Math.Max(1.0, lin.modes[k].naturalFrequencyRadSec));
                }
            }

            report.Append("    independently differenced scaled A_z vs A: largest |d lambda| ").Append(worstScaled.ToString("E2"))
                  .Append(" (").Append(worstRelative.ToString("E1")).Append(" of max(|lambda|, 0.1)); inside the eigenvalue's own plateau spread: ")
                  .Append(within).Append('/').Append(total).AppendLine();
            Record(worstAnalytic <= EigenSolverIdentity,
                "the analytic similarity D^-1 A D gives the same eigenvalues to " + worstAnalytic.ToString("E1")
                + " at all 170: the eigensolver is insensitive to the physical units' scale spread",
                report, ref passed, ref failed);
            Record(worstRelative <= PlateauRelative,
                "the independently differenced scaled system reproduces every one of the 1,360 eigenvalues within the plateau criterion ("
                + PlateauRelative.ToString("E0") + " of max(|lambda|, " + PlateauFloorPerSec.ToString("F1") + "/s)); where it differs by more "
                + "than the x3/x1/3 spread, the difference enters that eigenvalue's numerical uncertainty and zero band",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [S11]

        private static void ValidateStepAudit(Sweep s, StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[S11] Finite-difference step audit (NUMERICAL settings, not physical uncertainty)");
            report.Append("    all eight steps scaled together by the factor; largest |d lambda| to the chosen steps / max(|lambda|, 0.1):")
                  .AppendLine();
            report.Append("    point ");
            foreach (double f in MavF15ResearchStabilityAnalysis.StepAuditFactors)
                report.Append(f.ToString("G3", CultureInfo.InvariantCulture).PadLeft(9));
            report.AppendLine();

            bool plateau = true, resolves = true;
            foreach (int point in StepAuditPoints)
            {
                MavF15ResearchLinearization lin = s.all.Find(l => l.equilibrium.point == point);
                MavF15ResearchLinearization core = MavF15ResearchStabilityAnalysis.Core(MavF15AfitResearchIdentity.ConfigurationId, lin.equilibrium);
                List<MavF15ResearchStepAuditRow> rows = MavF15ResearchStabilityAnalysis.StepAudit(lin.equilibrium);
                report.Append("    ").Append(point.ToString().PadLeft(5)).Append(' ');
                foreach (MavF15ResearchStepAuditRow row in rows)
                {
                    double rel = 0.0;
                    for (int k = 0; row.evaluated && k < N; k++)
                    {
                        MavF15ResearchMode m = FindMode(lin, core, k);
                        rel = Math.Max(rel, MavF15ResearchStabilityAnalysis.Distance(m.re, m.im, row.re[k], row.im[k])
                                            / Math.Max(m.naturalFrequencyRadSec, PlateauFloorPerSec));
                    }

                    report.Append((row.evaluated ? rel.ToString("E1") : "n/a").PadLeft(9));
                    if ((row.factor == 3.0 || Math.Abs(row.factor - 1.0 / 3.0) < 1e-12) && (!row.evaluated || rel > PlateauRelative))
                        plateau = false;
                    if (row.factor == 100.0 && row.evaluated && rel <= PlateauRelative)
                        resolves = false;
                }

                report.AppendLine();
            }

            report.AppendLine("    per-column audit (analysis document §4): alpha plateau 3e-4..1e-3 rad (float coefficient noise below, truncation above);");
            report.AppendLine("    beta first-order across beta = 0 (the routine's |beta| terms), plateau at <= 3e-6 rad off zero; p, q, r linear-to-");
            report.AppendLine("    quadratic, flat 3e-3..3e-2 rad/s; theta, phi double trigonometry, flat 1e-6..1e-4 rad; V truncation vs normalization noise, 0.5..1.5 ft/s");
            Record(plateau,
                "at every audited equilibrium (symmetric start/middle/end, both folds' neighbourhoods, the saddle, the pitchfork, both "
                + "turning ends) the x3 and x1/3 steps move no eigenvalue by more than " + PlateauRelative.ToString("E0")
                + " of max(|lambda|, 0.1/s): the chosen steps sit on a plateau",
                report, ref passed, ref failed);
            Record(resolves,
                "and the audit has resolving power: 100x the chosen steps moves every audited spectrum outside that band",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [S12]

        private static void ValidateClassification(Sweep s, StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[S12] Classification (STABLE: every Re < -band; UNSTABLE: any Re > +band; NEAR_NEUTRAL: an Re inside the NUMERICAL band)");

            int[,] counts = new int[3, 4];
            foreach (MavF15ResearchLinearization lin in s.all)
                counts[(int)lin.equilibrium.branch, (int)lin.classification]++;

            string[] branch = { "symmetric", "turning  ", "pitchfork" };
            for (int b = 0; b < 3; b++)
            {
                report.Append("    ").Append(branch[b]).Append(": STABLE ").Append(counts[b, 0]).Append(", UNSTABLE ").Append(counts[b, 1])
                      .Append(", NEAR_NEUTRAL ").Append(counts[b, 2]).Append(", not evaluated ").Append(counts[b, 3]).AppendLine();
            }

            int stable = counts[0, 0] + counts[1, 0] + counts[2, 0];
            int unstable = counts[0, 1] + counts[1, 1] + counts[2, 1];
            int neutral = counts[0, 2] + counts[1, 2] + counts[2, 2];
            int notEvaluated = counts[0, 3] + counts[1, 3] + counts[2, 3];
            report.Append("    total: STABLE ").Append(stable).Append(", UNSTABLE ").Append(unstable).Append(", NEAR_NEUTRAL ").Append(neutral).AppendLine();

            report.AppendLine("    point branch    stab_deg   alpha_deg  V_ft_s  phi_deg | class        | eigenvalue with the largest real part  (band)");
            foreach (MavF15ResearchLinearization lin in s.all)
            {
                MavF15ResearchMode top = lin.modes[0];
                for (int k = 1; k < N; k++)
                {
                    if (lin.modes[k].re > top.re)
                        top = lin.modes[k];
                }

                MavF15ResearchEquilibrium eq = lin.equilibrium;
                report.Append("    ").Append(eq.point.ToString().PadLeft(5)).Append(' ')
                      .Append(MavF15ResearchStabilityAnalysis.BranchLabel(eq.branch).PadRight(9))
                      .Append(eq.stabilatorDeg.ToString("F5").PadLeft(10)).Append(' ')
                      .Append((eq.state.alphaRad * RadToDeg).ToString("F4").PadLeft(9)).Append(' ')
                      .Append(eq.state.trueAirspeedFtPerSec.ToString("F2").PadLeft(7)).Append(' ')
                      .Append((eq.state.phiRad * RadToDeg).ToString("F3").PadLeft(8)).Append(" | ")
                      .Append(MavF15ResearchStabilityAnalysis.ClassLabel(lin.classification).PadRight(12)).Append(" | ")
                      .Append(top.id.PadRight(3)).Append(top.re.ToString("E4").PadLeft(12))
                      .Append(top.im >= 0.0 ? " +" : " -").Append(Math.Abs(top.im).ToString("F4")).Append("i  (")
                      .Append(top.zeroBand.ToString("E1")).AppendLine(")");
            }

            Record(notEvaluated == 0 && stable + unstable + neutral == 170,
                "all 170 classified; every raw eigenvalue is kept (dataset: Docs/Reference/Data/F15/stability/)",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [S13]

        private static void ValidateSourceComparison(Sweep s, StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[S13] Source vs computed stability");
            report.AppendLine("    source: Table VII's caption labels every row stable; no eigenvalue, unstable-root count or per-row flag is printed.");
            report.AppendLine("    The printed stabilator extrema (124, 136, 182, 194) and the phi sign change (165) are the only special points evident.");

            int agree = 0, disagree = 0, unresolved = 0;
            int explainedFold = 0, explainedPair = 0, unexplained = 0, neutralAtFold = 0;
            double hopfAlpha = s.hopfShortPeriodTable.bracketed ? s.hopfShortPeriodTable.at.equilibrium.state.alphaRad * RadToDeg : double.NaN;
            List<int> disagreePoints = new List<int>();
            foreach (MavF15ResearchLinearization lin in s.all)
            {
                string a = MavF15ResearchStabilityAnalysis.Agreement(lin);
                if (a == "AGREE")
                {
                    agree++;
                    continue;
                }

                MavF15ResearchEquilibrium eq = lin.equilibrium;
                if (a == "UNRESOLVED")
                {
                    unresolved++;
                    if (IsPrintedFold(eq.point))
                        neutralAtFold++;
                    continue;
                }

                disagree++;
                disagreePoints.Add(eq.point);
                bool fold = false, pair = false;
                for (int k = 0; k < N; k++)
                {
                    MavF15ResearchMode m = lin.modes[k];
                    if (m.classification != MavF15ResearchStabilityClass.Unstable)
                        continue;
                    if (eq.branch == MavF15ResearchEquilibriumBranch.Turning && m.im == 0.0 && BetweenFolds(eq.point))
                        fold = true;
                    else if (eq.branch == MavF15ResearchEquilibriumBranch.Symmetric && m.im != 0.0 && m.lateralFraction < 0.01
                             && eq.state.alphaRad * RadToDeg < hopfAlpha)
                        pair = true;
                }

                if (fold)
                    explainedFold++;
                else if (pair)
                    explainedPair++;
                else
                    unexplained++;
            }

            report.Append("    AGREE ").Append(agree).Append(", DISAGREE ").Append(disagree).Append(", UNRESOLVED (numerical zero band) ")
                  .Append(unresolved).AppendLine();
            report.Append("    DISAGREE points: ").AppendLine(Ranges(disagreePoints));
            report.Append("      ").Append(explainedFold).AppendLine(" turning states strictly between two printed stabilator extrema: one REAL eigenvalue > 0 (a saddle).");
            report.AppendLine("        Structural: a stabilator extremum along a branch is a limit point, where a real eigenvalue must cross zero");
            report.AppendLine("        (Davison p.21); whatever the coefficients, the segment between 124 and 136 (and 182-194) is on the far side.");
            report.Append("      ").Append(explainedPair).Append(" symmetric states with alpha below the located Hopf (").Append(hopfAlpha.ToString("F3"))
                  .AppendLine(" deg): a LONGITUDINAL complex pair with Re > 0.");
            report.AppendLine("        Cause: the source's own pitch-damping fit CMMQ (all three printings agree, transcription re-read) is POSITIVE for");
            report.AppendLine("        alpha ~10.6-14.3 deg (+19.8/rad at 13 deg). Table VII cannot test CMMQ there: the symmetric rows have q = 0.");
            report.Append("      unexplained: ").Append(unexplained).AppendLine();
            report.Append("    UNRESOLVED: ").Append(unresolved).Append(" (").Append(neutralAtFold).AppendLine(" of them the printed limit points themselves, where one eigenvalue is ~0 by definition)");
            report.AppendLine("    Baumann's own plotted stability (App. C, 'stable ... solid, unstable dashed'; Figure C-7, phi vs stabilator, PDF p.122,");
            report.AppendLine("    read on the page image): symmetric branch DASHED for stabilator ~ -6.5..0 and below ~ -20, SOLID between; the turning");
            report.AppendLine("    loop SOLID throughout. Against it: AGREE - symmetric unstable on the turning side of the fork (the positive lateral real");
            report.AppendLine("    eigenvalue of [S15]) and unstable beyond the lateral Hopf ([S18]); DISAGREE - the longitudinal pair unstable at alpha");
            report.AppendLine("    11.0-14.2 deg (stabilator -6.96..-10.88, drawn solid) and the fold-bounded saddles (drawn solid, though a stabilator");
            report.AppendLine("    extremum forces a real eigenvalue through zero). The first says Baumann's executed model had pitch damping the printed");
            report.AppendLine("    CMMQ does not; the second cannot be reconciled with any smooth 8-state model at the figure's resolution. Neither is tuned.");
            Record(agree + disagree + unresolved == 170 && unexplained == 0 && neutralAtFold == unresolved,
                "every one of the 170 compared; every disagreement is one of two located mechanisms and every unresolved point is a "
                + "printed limit point - nothing is tuned, and no source eigenvalue is manufactured",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [S14]

        private static void ValidateFolds(Sweep s, StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[S14] Folds: a real eigenvalue crosses zero where the stabilator is extremal along the branch");
            report.AppendLine("    bracket  | phi where lambda = 0 (bisection) | phi of stabilator extremum (parabola, 9 solves) | stabilator at lambda = 0 vs printed extremum");

            bool all = true;
            for (int i = 0; i < s.folds.Length; i++)
            {
                MavF15ResearchCrossing c = s.folds[i];
                int[] br = s.foldBrackets[i];
                double printedStab = TurningState(br[2] == 193 ? 124 : br[2]).stabilatorDeg;
                double phiZero = 0.5 * (c.parameterA + c.parameterB);
                double stabAtZero = c.at.equilibrium.stabilatorDeg;
                report.Append("    ").Append(br[0]).Append('-').Append(br[1]).Append("  | ").Append(phiZero.ToString("F5").PadLeft(10))
                      .Append(" (Re ").Append(c.largestReA.ToString("E1")).Append('/').Append(c.largestReB.ToString("E1")).Append(")")
                      .Append(" | ").Append(c.stabilatorExtremumParameter.ToString("F4").PadLeft(10))
                      .Append(" (gap ").Append(Math.Abs(c.stabilatorExtremumParameter - phiZero).ToString("F4")).Append(" deg)")
                      .Append(" | ").Append(stabAtZero.ToString("F6")).Append(" vs ").Append(printedStab.ToString("F6"))
                      .Append(br[2] == 193 ? " (194 is unavailable; its printed stabilator equals 124's)" : "").AppendLine();
                if (!c.bracketed || !c.stabilatorExtremumFound
                    || Math.Abs(c.stabilatorExtremumParameter - phiZero) > 0.1 * Math.Abs(TurningState(br[1]).phiDeg - TurningState(br[0]).phiDeg)
                    || Math.Abs(stabAtZero - printedStab) > 1e-4)
                    all = false;
            }

            Record(all,
                "all four printed stabilator extrema are located as real-eigenvalue zero crossings; the stabilator extremum of the "
                + "recovered branch falls inside a tenth of the bracket, and the stabilator at lambda = 0 matches the printed extremum "
                + "to < 1e-4 deg - the dynamic Jacobian is singular exactly where the source's branch folds",
                report, ref passed, ref failed);

            double left = 0.5 * (s.folds[1].parameterA + s.folds[1].parameterB), right = 0.5 * (s.folds[2].parameterA + s.folds[2].parameterB);
            double left2 = 0.5 * (s.folds[0].parameterA + s.folds[0].parameterB), right2 = 0.5 * (s.folds[3].parameterA + s.folds[3].parameterB);
            report.Append("    mirror: phi at the fold pairs ").Append(left.ToString("F5")).Append(" / ").Append(right.ToString("F5")).Append(" and ")
                  .Append(left2.ToString("F5")).Append(" / ").Append(right2.ToString("F5")).AppendLine();
            Record(Math.Abs(left + right) < 1e-3 && Math.Abs(left2 + right2) < 1e-3,
                "the left and right folds sit at mirror bank angles, located independently",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [S15]

        private static void ValidatePitchfork(Sweep s, StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[S15] The pitchfork (point 165), approached along the symmetric branch and both turning branches");
            report.AppendLine("    path      parameter    stab_deg     alpha_deg  V_ft_s   | critical real eigenvalue  lateral | shape (alpha beta p q r theta phi V; deg, deg/s, dV/V0 x57.3)");

            bool symmetricChanges = false, turningKeepsSign = true, lateral = true;
            double symmetricBelow = double.NaN, symmetricAbove = double.NaN;
            foreach (MavF15ResearchPitchforkApproachPoint p in s.pitchfork)
            {
                if (!p.converged)
                {
                    report.Append("    ").Append(p.path.PadRight(9)).Append(p.parameter.ToString("G6").PadLeft(10)).AppendLine("  not recovered");
                    continue;
                }

                MavF15ResearchEquilibrium eq = p.linearization.equilibrium;
                report.Append("    ").Append(p.path.PadRight(9)).Append((p.tablePoint > 0 ? "pt " + p.tablePoint + " " : "") .PadLeft(0))
                      .Append(p.parameter.ToString("G6").PadLeft(10)).Append(' ')
                      .Append(eq.stabilatorDeg.ToString("F6").PadLeft(11)).Append(' ')
                      .Append((eq.state.alphaRad * RadToDeg).ToString("F5").PadLeft(10)).Append(' ')
                      .Append(eq.state.trueAirspeedFtPerSec.ToString("F3").PadLeft(8)).Append(" | ")
                      .Append(p.criticalRe.ToString("E3").PadLeft(11)).Append(' ')
                      .Append(ClassOfCritical(p.linearization, p.criticalRe).PadRight(12))
                      .Append(p.criticalLateralFraction.ToString("F3").PadLeft(6)).Append(" |");
                if (p.criticalSignedShape != null)
                {
                    foreach (double v in p.criticalSignedShape)
                        report.Append(' ').Append(v.ToString("+0.000;-0.000"));
                }

                report.AppendLine();

                if (p.path == "symmetric")
                {
                    if (p.criticalLateralFraction < 0.999)
                        lateral = false;
                    if (p.parameter < 377.3)
                        symmetricBelow = double.IsNaN(symmetricBelow) ? p.criticalRe : symmetricBelow;
                    if (p.parameter > 377.6)
                        symmetricAbove = p.criticalRe;
                }
                else if (p.path == "phi+" || p.path == "phi-")
                {
                    MavF15ResearchMode m = CriticalOf(p.linearization, p.criticalRe);
                    if (p.criticalRe > m.zeroBand)
                        turningKeepsSign = false;
                }
            }

            symmetricChanges = symmetricBelow < 0.0 && symmetricAbove > 0.0;
            MavF15ResearchCrossing c = s.pitchforkCrossing;
            MavF15ResearchEquilibrium at = c.at.equilibrium;
            MavF15TableViiState printed = TurningState(MavF15TableViiTurningRecovery.PitchforkPoint());
            double turningLimitStab = double.NaN, turningLimitV = double.NaN;
            foreach (MavF15ResearchPitchforkApproachPoint p in s.pitchfork)
            {
                if ((p.path == "phi+" || p.path == "phi-") && Math.Abs(p.parameter) == 0.1 && p.converged)
                {
                    turningLimitStab = p.linearization.equilibrium.stabilatorDeg;
                    turningLimitV = p.linearization.equilibrium.state.trueAirspeedFtPerSec;
                }
            }

            report.Append("    symmetric zero crossing (bisection in V): V ").Append(c.parameterA.ToString("F5")).Append("..").Append(c.parameterB.ToString("F5"))
                  .Append(", stabilator ").Append(at.stabilatorDeg.ToString("F6")).Append(", alpha ").Append((at.state.alphaRad * RadToDeg).ToString("F5")).AppendLine();
            report.Append("    turning branches at |phi| = 0.1 deg: V ").Append(turningLimitV.ToString("F4")).Append(", stabilator ").Append(turningLimitStab.ToString("F6"))
                  .Append(";  printed 165: V ").Append(printed.trueVelocityFtPerSec.ToString("F1")).Append(" (4 digits), stabilator ")
                  .Append(printed.stabilatorDeg.ToString("F6")).AppendLine();
            Record(c.bracketed && symmetricChanges && lateral,
                "SYMMETRIC branch: one REAL eigenvalue changes sign - negative for V below the fork (stabilator more negative), positive "
                + "above - and its eigenvector is purely lateral (beta, p, r, phi; lateral fraction 1.000): the symmetry-breaking mode",
                report, ref passed, ref failed);
            Record(turningKeepsSign,
                "TURNING branches: the same eigenvalue stays negative (or inside the numerical band) on both sides and shrinks toward 0 as "
                + "|phi| -> 0 - it touches zero at the fork without crossing: a supercritical pitchfork, turning pair stable near it",
                report, ref passed, ref failed);
            // How well the zero crossing is located: the critical eigenvalue's own numerical uncertainty,
            // mapped to V through its slope on the symmetric branch, and to the stabilator through dstab/dV.
            MavF15ResearchLinearization atFull = MavF15ResearchStabilityAnalysis.Linearize(MavF15AfitResearchIdentity.ConfigurationId, at);
            double muUncertainty = MavF15ResearchStabilityAnalysis.CriticalMode(atFull).realUncertainty;
            MavF15ResearchPitchforkApproachPoint lo = s.pitchfork.Find(p => p.path == "symmetric" && Math.Abs(p.parameter - (printed.trueVelocityFtPerSec - 0.05)) < 1e-6);
            MavF15ResearchPitchforkApproachPoint hi = s.pitchfork.Find(p => p.path == "symmetric" && Math.Abs(p.parameter - (printed.trueVelocityFtPerSec + 0.1)) < 1e-6);
            double dMuDv = (hi.criticalRe - lo.criticalRe) / (hi.parameter - lo.parameter);
            double dStabDv = (hi.linearization.equilibrium.stabilatorDeg - lo.linearization.equilibrium.stabilatorDeg) / (hi.parameter - lo.parameter);
            double uV = muUncertainty / Math.Abs(dMuDv);
            double uStab = uV * Math.Abs(dStabDv);
            double vZero = 0.5 * (c.parameterA + c.parameterB);
            report.Append("    location uncertainty (critical-eigenvalue uncertainty ").Append(muUncertainty.ToString("E1")).Append(" / slope ")
                  .Append(dMuDv.ToString("E2")).Append(" per ft/s): V +/-").Append(uV.ToString("E1")).Append(" ft/s, stabilator +/-")
                  .Append(uStab.ToString("E1")).Append(" deg;  gaps: printed ").Append(Math.Abs(at.stabilatorDeg - printed.stabilatorDeg).ToString("E1"))
                  .Append(" deg, turning limit ").Append(Math.Abs(at.stabilatorDeg - turningLimitStab).ToString("E1")).AppendLine(" deg");
            Record(Math.Abs(at.stabilatorDeg - printed.stabilatorDeg) <= 3.0 * uStab && Math.Abs(at.stabilatorDeg - turningLimitStab) <= 3.0 * uStab
                   && Math.Abs(vZero - printed.trueVelocityFtPerSec) <= 0.05 + 3.0 * uV,
                "the eigenvalue zero, the turning branches' phi -> 0 limit and the printed pitchfork agree within 3x the crossing's own "
                + "numerical location uncertainty in stabilator, and in V within the printed half unit (0.05 ft/s)",
                report, ref passed, ref failed);

            MavF15ResearchLinearization fork = s.all.Find(l => l.equilibrium.branch == MavF15ResearchEquilibriumBranch.Pitchfork);
            MavF15ResearchMode critical = MavF15ResearchStabilityAnalysis.CriticalMode(fork);
            report.Append("    at the recovered 165 (V = 377.4 as printed): critical ").Append(critical.re.ToString("E3"))
                  .Append(", numerical band ").Append(critical.zeroBand.ToString("E1")).Append(", print spread ").Append(critical.printSpread.ToString("E1"))
                  .AppendLine(" -> sign not resolved by the printed V: consistent with the zero the fork requires");
            Record(Math.Abs(critical.re) <= critical.printSpread,
                "point 165's critical eigenvalue lies inside its own print spread (V moved by its half unit re-solved both ways)",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [S16]

        private static void ValidateMirror(Sweep s, StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[S16] Mirror spectra: spectrum(+phi) = spectrum(-phi)");

            double worstExact = 0.0, worstSolver = 0.0, worstSolverOverSpread = 0.0, worstSolverRelative = 0.0, worstEntry = 0.0;
            int n = 0, solved = 0, bitwise = 0;
            double[] parity = { 1.0, -1.0, -1.0, 1.0, -1.0, 1.0, -1.0, 1.0 };
            foreach (MavF15ResearchLinearization lin in s.turningBranch)
            {
                if (lin.equilibrium.branch != MavF15ResearchEquilibriumBranch.Turning)
                    continue;
                n++;
                MavF15ResearchSourceState mirror = MavF15ResearchStabilityAnalysis.Mirror(lin.equilibrium.state);
                double[,] a;
                double[] re, im;
                if (!MavF15ResearchStabilityAnalysis.Jacobian(mirror, lin.equilibrium.stabilatorDeg, 1.0, out a)
                    || !MavValidationEigenSolver.Eigenvalues(a, out re, out im))
                {
                    worstExact = double.PositiveInfinity;
                    continue;
                }

                bool same = true;
                double norm = 0.0, diff = 0.0;
                for (int i = 0; i < N; i++)
                {
                    for (int j = 0; j < N; j++)
                    {
                        double mirrored = parity[i] * lin.jacobian[i, j] * parity[j];
                        if (a[i, j] != mirrored)
                            same = false;
                        diff = Math.Max(diff, Math.Abs(a[i, j] - mirrored));
                        norm = Math.Max(norm, Math.Abs(lin.jacobian[i, j]));
                    }
                }

                worstEntry = Math.Max(worstEntry, diff / norm);

                if (same)
                    bitwise++;

                int[] match = MavF15ResearchStabilityAnalysis.Match(lin.modes, re, im);
                for (int k = 0; k < N; k++)
                {
                    double d = MavF15ResearchStabilityAnalysis.Distance(lin.modes[k].re, lin.modes[k].im, re[match[k]], im[match[k]]);
                    worstExact = Math.Max(worstExact, d / Math.Max(1.0, lin.modes[k].naturalFrequencyRadSec));
                }

                // The WP-3C solver at -phi from the UNMIRRORED start: it must travel to the other side itself.
                MavF15ResearchTurningTrimGuess start = MavF15TableViiTurningRecovery.Guess(MavF15TableViiTurningRecovery.Published(lin.equilibrium.published));
                MavF15ResearchEquilibrium other = MavF15ResearchStabilityAnalysis.Turning(lin.equilibrium.published, -lin.equilibrium.published.phiDeg, start);
                if (!other.recovered || !MavF15ResearchStabilityAnalysis.EigenvaluesAt(other, 1.0, out re, out im))
                    continue;
                solved++;
                match = MavF15ResearchStabilityAnalysis.Match(lin.modes, re, im);
                for (int k = 0; k < N; k++)
                {
                    MavF15ResearchMode m = lin.modes[k];
                    double d = MavF15ResearchStabilityAnalysis.Distance(m.re, m.im, re[match[k]], im[match[k]]);
                    worstSolver = Math.Max(worstSolver, d);
                    worstSolverOverSpread = Math.Max(worstSolverOverSpread, d / Math.Max(m.stepSpread + m.printSpread, 1e-12));
                    worstSolverRelative = Math.Max(worstSolverRelative, d / Math.Max(m.naturalFrequencyRadSec, PlateauFloorPerSec));
                }
            }

            report.Append("    exact mirror state (beta, p, r, phi negated): A(mirror) = S A S bit for bit at ").Append(bitwise).Append("/").Append(n)
                  .Append(", S = diag(+1 -1 -1 +1 -1 +1 -1 +1); elsewhere the largest entry gap is ").Append(worstEntry.ToString("E1"))
                  .AppendLine(" of max|A| (the routine's beta sign blends EPA02S/L are odd in exact arithmetic, not bit for bit in floating point)");
            report.Append("    spectra differ by at most ").Append(worstExact.ToString("E1"))
                  .AppendLine(" of max(1, |lambda|): the eigensolver's own path through a sign-flipped, identical-spectrum matrix");
            Record(n == 80 && worstEntry <= ImplementationIdentity && worstExact <= PlateauRelative * 1e-3,
                "at all 80 turning equilibria the differenced Jacobian of the mirrored state is S A S to round-off: the parity derived in "
                + "WP-3C holds for the DYNAMICS, so spectrum(+phi) = spectrum(-phi), reproduced by the eigensolver to " + worstExact.ToString("E1"),
                report, ref passed, ref failed);
            report.Append("    solver-found mirror root from the unmirrored start (80 independent WP-3C solves at -phi): largest |d lambda| ")
                  .Append(worstSolver.ToString("E1")).Append(" (").Append(worstSolverRelative.ToString("E1")).Append(" of max(|lambda|, 0.1); ")
                  .Append(worstSolverOverSpread.ToString("F1")).AppendLine(" x step + print spread) - the solver's own root defect at -phi (WP-3C: alpha 8.9e-5 deg, V 2.2e-3 ft/s)");
            Record(solved == 80 && worstSolverRelative <= PlateauRelative,
                "the 80 independently solved mirror roots reproduce every spectrum within the plateau criterion: numerically indistinguishable",
                report, ref passed, ref failed);

            MavF15ResearchLinearization a136 = s.all.Find(l => l.equilibrium.point == 136);
            MavF15ResearchLinearization a182 = s.all.Find(l => l.equilibrium.point == 182);
            double[] r182 = new double[N], i182 = new double[N];
            for (int k = 0; k < N; k++)
            {
                r182[k] = a182.modes[k].re;
                i182[k] = a182.modes[k].im;
            }

            int[] m136 = MavF15ResearchStabilityAnalysis.Match(a136.modes, r182, i182);
            double worstPair = 0.0, worstPairBand = 0.0;
            for (int k = 0; k < N; k++)
            {
                double d = MavF15ResearchStabilityAnalysis.Distance(a136.modes[k].re, a136.modes[k].im, r182[m136[k]], i182[m136[k]]);
                worstPair = Math.Max(worstPair, d);
                worstPairBand = Math.Max(worstPairBand, d / Math.Max(a136.modes[k].stepSpread + a136.modes[k].printSpread, 1e-12));
            }

            report.Append("    Table VII's own printed mirror pair 136 / 182 (identical printed stabilator and alpha, opposite beta and p), each ")
                  .Append("recovered from its own row: largest |d lambda| ").Append(worstPair.ToString("E1")).AppendLine();
            Record(worstPairBand <= 1.0,
                "the source's printed mirror pair has matching spectra within their numerical + print spread",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [S17]

        private static void ValidateModeTracking(Sweep s, StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[S17] Mode tracking (minimum-distance assignment between neighbours; never per-point sorting)");

            bool consistent = true;
            foreach (List<MavF15ResearchLinearization> branch in new[] { s.symmetricBranch, s.turningBranch })
            {
                string name = branch == s.symmetricBranch ? "symmetric (seed: point " + branch[0].equilibrium.point + ", stabilator order)"
                    : "turning (seed: the pitchfork 165, both ways)";
                MavF15ResearchLinearization seed = branch == s.symmetricBranch ? branch[0] : branch.Find(l => l.equilibrium.branch == MavF15ResearchEquilibriumBranch.Pitchfork);
                report.Append("    ").Append(name).AppendLine(":");
                foreach (MavF15ResearchMode m in seed.modes)
                {
                    if (m.id.EndsWith("-"))
                        continue;
                    report.Append("      Mode ").Append(m.id.TrimEnd('+').PadRight(2)).Append(m.re.ToString("E3").PadLeft(11))
                          .Append(m.im != 0.0 ? " +/-" + Math.Abs(m.im).ToString("F4") + "i" : "          ")
                          .Append("  lateral ").Append(m.lateralFraction.ToString("F3"))
                          .Append("  shape").Append(ShapeText(m.shape)).Append("  -> ").AppendLine(Interpretation(m));
                }

                List<string> events = new List<string>();
                double worstAmbiguity = 0.0;
                string worstAt = "";
                for (int i = 1; i < branch.Count; i++)
                {
                    MavF15ResearchLinearization prev = branch[i - 1], cur = branch[i];
                    for (int k = 0; k < N; k++)
                    {
                        MavF15ResearchMode m = cur.modes[k];
                        if (m.trackingAmbiguity > worstAmbiguity && !double.IsInfinity(m.trackingAmbiguity))
                        {
                            worstAmbiguity = m.trackingAmbiguity;
                            worstAt = m.id + " between " + prev.equilibrium.point + " and " + cur.equilibrium.point;
                        }

                        if (m.trackingAmbiguity > MavF15ResearchStabilityAnalysis.TrackingAmbiguityFlag)
                            events.Add("identity uncertain: " + m.id + " " + prev.equilibrium.point + "->" + cur.equilibrium.point
                                       + " (ratio " + m.trackingAmbiguity.ToString("F2") + ")");
                        MavF15ResearchMode before = FindById(prev, m.id);
                        if ((before.im == 0.0) != (m.im == 0.0))
                            events.Add("coalescence: " + m.id + (m.im == 0.0 ? " complex -> real" : " real -> complex") + " between "
                                       + prev.equilibrium.point + " and " + cur.equilibrium.point);
                        if (Math.Sign(before.re) != Math.Sign(m.re) && m.id.IndexOf('-') < 0)
                            events.Add("Re sign change: " + m.id.TrimEnd('+') + " between " + prev.equilibrium.point + " and " + cur.equilibrium.point
                                       + " (" + before.re.ToString("E2") + " -> " + m.re.ToString("E2") + ")");
                    }

                    // Conjugate names must stay paired.
                    for (int k = 0; k < N; k++)
                    {
                        MavF15ResearchMode m = cur.modes[k];
                        if (m.im == 0.0 || !m.id.EndsWith("+"))
                            continue;
                        MavF15ResearchMode partner = FindById(cur, m.id.TrimEnd('+') + "-");
                        if (partner.re != m.re || partner.im != -m.im)
                            consistent = false;
                    }
                }

                report.Append("      largest tracking ambiguity ").Append(worstAmbiguity.ToString("F3")).Append(" (").Append(worstAt).AppendLine(")");
                foreach (string e in Distinct(events))
                    report.Append("      ").AppendLine(e);
            }

            Record(consistent,
                "every conjugate pair keeps one name along both branches, and every sign change, coalescence and ambiguous step is listed "
                + "above; conventional labels are attached only where block membership, frequency and eigenvector agree, and are "
                + "interpretations of the SOURCE MODEL's modes, not claims about the aircraft",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [S18]

        private static void ValidateHopf(Sweep s, StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[S18] Hopf crossings on the symmetric branch (WP-3B solves bisected in V; no continuation) vs the sources' statements");
            report.AppendLine("    Baumann App. C (PDF p.118): 'The branch loses its stability at -19.73 deg through a Hopf bifurcation and is unstable for");
            report.AppendLine("    delta_e < -19.73'; Baumann p.60: 'a Hopf bifurcation to periodic motion at 19 deg angle of attack'; Davison p.42 (12-state");
            report.AppendLine("    model, CAS off, same airframe equations - its first-order actuator states add eigenvalues -20, -28 but move none of these):");
            report.AppendLine("    'At 20 degrees (30 units) AOA, a Hopf bifurcation point was detected, signalling the onset of wing rock'");

            MavF15ResearchCrossing[] all = { s.hopfShortPeriodFork, s.hopfShortPeriodTable, s.hopfLateral };
            string[] label = { "longitudinal pair, near the fork (untabulated)", "longitudinal pair, inside Table VII (8/9)", "lateral pair, beyond Table VII" };
            bool located = true;
            for (int i = 0; i < all.Length; i++)
            {
                MavF15ResearchCrossing c = all[i];
                MavF15ResearchEquilibrium eq = c.at.equilibrium;
                report.Append("    ").Append(label[i].PadRight(48)).Append(": V ").Append(c.parameterA.ToString("F4")).Append(", stabilator ")
                      .Append(eq.stabilatorDeg.ToString("F4")).Append(" deg, alpha ").Append((eq.state.alphaRad * RadToDeg).ToString("F3"))
                      .Append(" deg, omega ").Append(c.crossingIm.ToString("F4")).Append(" rad/s (period ")
                      .Append((2.0 * Math.PI / c.crossingIm).ToString("F2")).Append(" s), lateral fraction ").Append(c.crossingLateralFraction.ToString("F3")).AppendLine();
                if (!c.bracketed || c.crossingIm < 0.5)
                    located = false;
            }

            MavF15ResearchEquilibrium lat = s.hopfLateral.at.equilibrium;
            report.Append("    comparison: the lateral Hopf is at stabilator ").Append(lat.stabilatorDeg.ToString("F2")).Append(" deg vs Baumann's -19.73 (")
                  .Append((lat.stabilatorDeg + 19.73).ToString("+0.00;-0.00")).Append(" deg), alpha ").Append((lat.state.alphaRad * RadToDeg).ToString("F1"))
                  .AppendLine(" deg vs Baumann's 19 and Davison's 20; lateral oscillatory, i.e. the wing-rock type Davison names");
            report.AppendLine("    it is unstable for more negative stabilator, as Baumann states; V there (278 ft/s) is beyond Table VII's rows but inside");
            report.AppendLine("    the source-exercised speed span; alpha 20.8 deg is inside the CFX 20-30 deg blend, where the high-alpha fit carries weight");
            report.AppendLine("    ~0.02 (3t^2 - 2t^3, t = 0.08), so the two CFX2 printings (1e-6 apart) differ there by ~2e-8 in CFX - below float resolution");
            report.AppendLine("    neither source mentions the two LONGITUDINAL crossings; Table VII prints no special point near either");
            Record(located && s.hopfLateral.crossingLateralFraction > 0.99 && s.hopfShortPeriodTable.crossingLateralFraction < 0.01
                   && s.hopfShortPeriodFork.crossingLateralFraction < 0.01,
                "three Hopf crossings located as complex pairs crossing the imaginary axis: two longitudinal (the positive-CMMQ band) and "
                + "one lateral; agreement with the sources is reported above, not asserted",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [S19]

        private static void ValidateUnchanged(MavAtmosphereSample[] atmosphereBefore, MavAeroCoefficients[] coefficientsBefore,
            float[] trimBefore, StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[S19] Nothing shared moved: coefficients, CFX2, atmosphere, trim results");

            MavAeroCoefficients[] after = SampleCoefficients();
            bool same = coefficientsBefore.Length == after.Length;
            for (int i = 0; same && i < after.Length; i++)
            {
                same = coefficientsBefore[i].cx == after[i].cx && coefficientsBefore[i].cz == after[i].cz && coefficientsBefore[i].cm == after[i].cm
                       && coefficientsBefore[i].cy == after[i].cy && coefficientsBefore[i].cl == after[i].cl && coefficientsBefore[i].cn == after[i].cn;
            }

            double a40 = 40.0 * DegToRad;
            MavAeroCoefficients c = MavF15BaumannMach06Longitudinal.Evaluate((float)a40, 0f, 0f);
            double cfx = -((c.cx * Math.Cos(a40)) + (c.cz * Math.Sin(a40)));
            double cfxBase = 0.0267297 - (0.10646919 * a40) + (5.39836337 * a40 * a40) - (5.0086893 * Math.Pow(a40, 3)) + (1.34148193 * Math.Pow(a40, 4));
            bool cfx2 = Math.Abs(cfx - (cfxBase + 0.09833517)) < Math.Abs(cfx - (cfxBase + 0.09833617));
            Record(same && cfx2,
                "the coefficient routines are bit-identical before and after the whole analysis, and CFX2 is still App. C's 0.09833517",
                report, ref passed, ref failed);

            MavAtmosphereSample[] atmosphereAfter = SampleAtmosphere();
            bool atmosphere = atmosphereBefore.Length == atmosphereAfter.Length;
            for (int i = 0; atmosphere && i < atmosphereAfter.Length; i++)
            {
                atmosphere = atmosphereBefore[i].densityKgM3 == atmosphereAfter[i].densityKgM3 && atmosphereBefore[i].pressurePa == atmosphereAfter[i].pressurePa
                             && atmosphereBefore[i].temperatureK == atmosphereAfter[i].temperatureK;
            }

            Record(atmosphere, "MavAtmosphereModel returns bit-identical samples before and after", report, ref passed, ref failed);

            float[] trimAfter = TrimFingerprint();
            bool trim = trimBefore.Length == trimAfter.Length;
            for (int i = 0; trim && i < trimAfter.Length; i++)
                trim = trimBefore[i] == trimAfter[i];
            Record(trim,
                "WP-3B symmetric and WP-3C turning trims (points 1, 46, 117, 150, 199 from their printed starts) return bit-identical "
                + "roots before and after: the stability pass reads the trim solver and changes nothing in it",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- CSV blocks

        private static void Block(StringBuilder o, string name, string csv)
        {
            o.Append("BEGIN_CSV ").Append(name).Append('\n').Append(csv);
            if (csv.Length > 0 && csv[csv.Length - 1] != '\n')
                o.Append('\n');
            o.Append("END_CSV ").Append(name).Append('\n');
        }

        private static string StepAuditCsv(Sweep s)
        {
            StringBuilder o = new StringBuilder(32768);
            CultureInfo ci = CultureInfo.InvariantCulture;
            o.Append("point,factor,mode,real_per_s,imag_rad_s,abs_difference_to_chosen\n");
            foreach (int point in StepAuditPoints)
            {
                MavF15ResearchLinearization lin = s.all.Find(l => l.equilibrium.point == point);
                MavF15ResearchLinearization core = MavF15ResearchStabilityAnalysis.Core(MavF15AfitResearchIdentity.ConfigurationId, lin.equilibrium);
                foreach (MavF15ResearchStepAuditRow row in MavF15ResearchStabilityAnalysis.StepAudit(lin.equilibrium))
                {
                    for (int k = 0; row.evaluated && k < N; k++)
                    {
                        MavF15ResearchMode m = FindMode(lin, core, k);
                        o.Append(point.ToString(ci)).Append(',').Append(row.factor.ToString("R", ci)).Append(',').Append(m.id).Append(',')
                         .Append(row.re[k].ToString("R", ci)).Append(',').Append(row.im[k].ToString("R", ci)).Append(',')
                         .Append(MavF15ResearchStabilityAnalysis.Distance(m.re, m.im, row.re[k], row.im[k]).ToString("E3", ci)).Append('\n');
                    }
                }
            }

            return o.ToString();
        }

        private static string PitchforkCsv(Sweep s)
        {
            StringBuilder o = new StringBuilder(16384);
            CultureInfo ci = CultureInfo.InvariantCulture;
            o.Append("path,parameter,table_point,converged,stabilator_deg,alpha_deg,V_ft_s,phi_deg,critical_real_per_s,critical_lateral_fraction,"
                     + "shape_alpha,shape_beta,shape_p,shape_q,shape_r,shape_theta,shape_phi,shape_V\n");
            foreach (MavF15ResearchPitchforkApproachPoint p in s.pitchfork)
            {
                MavF15ResearchEquilibrium eq = p.linearization.equilibrium;
                o.Append(p.path).Append(',').Append(p.parameter.ToString("R", ci)).Append(',').Append(p.tablePoint.ToString(ci)).Append(',')
                 .Append(p.converged ? "true" : "false").Append(',').Append(eq.stabilatorDeg.ToString("R", ci)).Append(',')
                 .Append((eq.state.alphaRad * RadToDeg).ToString("R", ci)).Append(',').Append(eq.state.trueAirspeedFtPerSec.ToString("R", ci)).Append(',')
                 .Append((eq.state.phiRad * RadToDeg).ToString("R", ci)).Append(',').Append(p.criticalRe.ToString("R", ci)).Append(',')
                 .Append(p.criticalLateralFraction.ToString("F6", ci));
                for (int i = 0; i < N; i++)
                    o.Append(',').Append(p.criticalSignedShape == null ? "" : p.criticalSignedShape[i].ToString("F6", ci));
                o.Append('\n');
            }

            return o.ToString();
        }

        private static string CrossingsCsv(Sweep s)
        {
            StringBuilder o = new StringBuilder(4096);
            CultureInfo ci = CultureInfo.InvariantCulture;
            o.Append("crossing,parameter,bracket_a,bracket_b,re_a,re_b,stabilator_deg,alpha_deg,V_ft_s,phi_deg,crossing_imag_rad_s,lateral_fraction,"
                     + "stabilator_extremum_phi_deg\n");
            List<KeyValuePair<string, MavF15ResearchCrossing>> rows = new List<KeyValuePair<string, MavF15ResearchCrossing>>
            {
                new KeyValuePair<string, MavF15ResearchCrossing>("pitchfork_symmetric_real", s.pitchforkCrossing),
                new KeyValuePair<string, MavF15ResearchCrossing>("hopf_longitudinal_near_fork", s.hopfShortPeriodFork),
                new KeyValuePair<string, MavF15ResearchCrossing>("hopf_longitudinal_points_8_9", s.hopfShortPeriodTable),
                new KeyValuePair<string, MavF15ResearchCrossing>("hopf_lateral_beyond_table", s.hopfLateral)
            };
            for (int i = 0; i < s.folds.Length; i++)
            {
                rows.Add(new KeyValuePair<string, MavF15ResearchCrossing>(
                    "fold_" + s.foldBrackets[i][0] + "_" + s.foldBrackets[i][1], s.folds[i]));
            }

            foreach (KeyValuePair<string, MavF15ResearchCrossing> r in rows)
            {
                MavF15ResearchCrossing c = r.Value;
                MavF15ResearchEquilibrium eq = c.at.equilibrium;
                o.Append(r.Key).Append(',').Append(c.parameterName).Append(',').Append(c.parameterA.ToString("R", ci)).Append(',')
                 .Append(c.parameterB.ToString("R", ci)).Append(',').Append(c.largestReA.ToString("R", ci)).Append(',')
                 .Append(c.largestReB.ToString("R", ci)).Append(',').Append(eq.stabilatorDeg.ToString("R", ci)).Append(',')
                 .Append((eq.state.alphaRad * RadToDeg).ToString("R", ci)).Append(',').Append(eq.state.trueAirspeedFtPerSec.ToString("R", ci)).Append(',')
                 .Append((eq.state.phiRad * RadToDeg).ToString("R", ci)).Append(',').Append(c.crossingIm.ToString("R", ci)).Append(',')
                 .Append(c.crossingLateralFraction.ToString("F6", ci)).Append(',')
                 .Append(c.stabilatorExtremumFound ? c.stabilatorExtremumParameter.ToString("R", ci) : "").Append('\n');
            }

            return o.ToString();
        }

        // ---------------------------------------------------------------- helpers

        private static MavF15TableViiState SymmetricState(int point)
        {
            foreach (MavF15TableViiState s in MavF15TableViiTrimRecovery.SymmetricStates())
            {
                if (s.part1Point == point)
                    return s;
            }

            throw new InvalidOperationException("no symmetric Table VII state " + point);
        }

        private static MavF15TableViiState TurningState(int point)
        {
            MavF15TableViiState s;
            if (!MavF15TableViiTurningRecovery.TryGetState(point, out s))
                throw new InvalidOperationException("no turning Table VII state " + point);
            return s;
        }

        private static bool IsPrintedFold(int point)
        {
            return point == 124 || point == 136 || point == 182 || point == 194;
        }

        private static bool BetweenFolds(int point)
        {
            return (point > 124 && point < 136) || (point > 182 && point < 194);
        }

        private static List<MavF15ResearchSourceState> ProbeStates()
        {
            List<MavF15ResearchSourceState> list = new List<MavF15ResearchSourceState>();
            double[] alpha = { 0.0, 8.0, 14.0, 25.0 };
            double[] beta = { -5.0, 0.0, 2.0 };
            double[] v = { 250.0, 450.0, 650.0 };
            int n = 0;
            foreach (double a in alpha)
            {
                foreach (double b in beta)
                {
                    foreach (double speed in v)
                    {
                        n++;
                        list.Add(new MavF15ResearchSourceState
                        {
                            alphaRad = a * DegToRad,
                            betaRad = b * DegToRad,
                            pRadSec = 0.5 * Math.Sin(n),
                            qRadSec = 0.2 * Math.Cos(1.3 * n),
                            rRadSec = 0.3 * Math.Sin(0.7 * n + 1.0),
                            thetaRad = 60.0 * Math.Sin(0.9 * n) * DegToRad,
                            phiRad = 60.0 * Math.Cos(1.1 * n) * DegToRad,
                            trueAirspeedFtPerSec = speed
                        });
                    }
                }
            }

            return list;
        }

        /// <summary>P D P^-1 with a block-diagonal D holding the given spectrum, then S^-1 (.) S by the scale vector.</summary>
        private static double[,] KnownMatrix(double[] spectrum, double[] scales)
        {
            double[,] d = new double[N, N];
            for (int k = 0; k < N; k++)
            {
                double re = spectrum[2 * k], im = spectrum[2 * k + 1];
                if (im > 0.0 && k + 1 < N)
                {
                    d[k, k] = re;
                    d[k, k + 1] = im;
                    d[k + 1, k] = -im;
                    d[k + 1, k + 1] = re;
                    k++;
                }
                else
                {
                    d[k, k] = re;
                }
            }

            double[,] p = new double[N, N];
            for (int i = 0; i < N; i++)
            {
                for (int j = 0; j < N; j++)
                    p[i, j] = (i == j ? 1.0 : 0.0) + 0.3 * Math.Sin(1.7 * i + 0.9 * j + 0.4);
            }

            double[,] pinv = Inverse(p);
            double[,] m = Multiply(Multiply(p, d), pinv);
            for (int i = 0; i < N; i++)
            {
                for (int j = 0; j < N; j++)
                    m[i, j] = m[i, j] * scales[j] / scales[i];
            }

            return m;
        }

        private static double[,] Multiply(double[,] a, double[,] b)
        {
            double[,] c = new double[N, N];
            for (int i = 0; i < N; i++)
            {
                for (int j = 0; j < N; j++)
                {
                    double s = 0.0;
                    for (int k = 0; k < N; k++)
                        s += a[i, k] * b[k, j];
                    c[i, j] = s;
                }
            }

            return c;
        }

        private static double[,] Inverse(double[,] a)
        {
            double[,] m = (double[,])a.Clone();
            double[,] inv = new double[N, N];
            for (int i = 0; i < N; i++)
                inv[i, i] = 1.0;
            for (int k = 0; k < N; k++)
            {
                int p = k;
                for (int i = k + 1; i < N; i++)
                {
                    if (Math.Abs(m[i, k]) > Math.Abs(m[p, k]))
                        p = i;
                }

                for (int j = 0; j < N; j++)
                {
                    double t = m[k, j];
                    m[k, j] = m[p, j];
                    m[p, j] = t;
                    t = inv[k, j];
                    inv[k, j] = inv[p, j];
                    inv[p, j] = t;
                }

                double pivot = m[k, k];
                for (int j = 0; j < N; j++)
                {
                    m[k, j] /= pivot;
                    inv[k, j] /= pivot;
                }

                for (int i = 0; i < N; i++)
                {
                    if (i == k)
                        continue;
                    double f = m[i, k];
                    for (int j = 0; j < N; j++)
                    {
                        m[i, j] -= f * m[k, j];
                        inv[i, j] -= f * inv[k, j];
                    }
                }
            }

            return inv;
        }

        private static double[] KArray(MavF15ResearchSourceKConstants k)
        {
            return new[] { 0.0, k.k1, k.k2, k.k3, k.k4, k.k5, k.k6, k.k7, k.k8, k.k9, k.k10, k.k11, k.k12, k.k13, k.k14, k.k15, k.k16, k.k17 };
        }

        private static int Index(MavF15ResearchLinearization lin, MavF15ResearchMode m)
        {
            int best = 0;
            double d = double.PositiveInfinity;
            for (int k = 0; k < N; k++)
            {
                double e = MavF15ResearchStabilityAnalysis.Distance(lin.modes[k].re, lin.modes[k].im, m.re, m.im);
                if (e < d)
                {
                    d = e;
                    best = k;
                }
            }

            return best;
        }

        /// <summary>The tracked mode of lin matching entry k of the untracked Core linearization (audit rows follow Core's order).</summary>
        private static MavF15ResearchMode FindMode(MavF15ResearchLinearization lin, MavF15ResearchLinearization core, int k)
        {
            return lin.modes[Index(lin, core.modes[k])];
        }

        private static MavF15ResearchMode FindById(MavF15ResearchLinearization lin, string id)
        {
            foreach (MavF15ResearchMode m in lin.modes)
            {
                if (m.id == id)
                    return m;
            }

            return default(MavF15ResearchMode);
        }

        private static MavF15ResearchMode CriticalOf(MavF15ResearchLinearization lin, double re)
        {
            foreach (MavF15ResearchMode m in lin.modes)
            {
                if (m.re == re && m.im == 0.0)
                    return m;
            }

            return default(MavF15ResearchMode);
        }

        private static string ClassOfCritical(MavF15ResearchLinearization lin, double re)
        {
            return MavF15ResearchStabilityAnalysis.ClassLabel(CriticalOf(lin, re).classification);
        }

        private static string ShapeText(double[] shape)
        {
            StringBuilder s = new StringBuilder();
            for (int i = 0; shape != null && i < N; i++)
                s.Append(' ').Append(MavF15AfitResearchSourceDynamics.StateNames[i]).Append(' ').Append(shape[i].ToString("F2", CultureInfo.InvariantCulture));
            return s.ToString();
        }

        private static string Dominant(MavF15ResearchMode m)
        {
            string[] names = MavF15AfitResearchSourceDynamics.StateNames;
            StringBuilder s = new StringBuilder();
            for (int i = 0; i < N; i++)
            {
                if (m.shape != null && m.shape[i] >= 0.3)
                    s.Append(s.Length > 0 ? "," : "").Append(names[i]);
            }

            return s.ToString();
        }

        /// <summary>
        /// A conventional name only where the evidence supports it: block membership (exact at a
        /// symmetric seed), frequency and the dominant eigenvector components. Otherwise none.
        /// </summary>
        private static string Interpretation(MavF15ResearchMode m)
        {
            bool lateral = m.lateralFraction > 0.99, longitudinal = m.lateralFraction < 0.01;
            double[] v = m.shape;
            if (v == null)
                return "no conventional label";
            if (longitudinal && m.im != 0.0 && m.naturalFrequencyRadSec > 0.5 && v[0] > 0.3 && v[3] >= 0.3 && v[7] < 1.0)
                return "short-period-like (alpha, q; caveat: source model, no CAS)";
            if (longitudinal && m.im != 0.0 && m.naturalFrequencyRadSec < 0.5 && v[7] >= 0.3)
                return "phugoid-like (V, theta; caveat: source model)";
            if (lateral && m.im != 0.0 && m.naturalFrequencyRadSec > 1.0 && v[1] > 0.05 && v[4] > 0.05)
                return "Dutch-roll-like: the only lateral oscillatory mode, beta and r both participate, roll-rate dominated (|phi/beta| "
                       + (v[6] / v[1]).ToString("F1", CultureInfo.InvariantCulture) + "; caveat: source model)";
            if (lateral && m.im == 0.0 && m.naturalFrequencyRadSec > 0.2 && v[2] >= 0.3)
                return "roll-subsidence-like (p, phi; caveat: source model)";
            if (lateral && m.im == 0.0 && m.naturalFrequencyRadSec < 0.2 && v[6] >= 0.99 && v[4] < 0.3)
                return "spiral-like (phi with small r; caveat: source model)";
            return "no conventional label";
        }

        private static string Ranges(List<int> points)
        {
            points.Sort();
            StringBuilder s = new StringBuilder();
            for (int i = 0; i < points.Count; i++)
            {
                int start = points[i];
                while (i + 1 < points.Count && points[i + 1] == points[i] + 1)
                    i++;
                if (s.Length > 0)
                    s.Append(", ");
                s.Append(start);
                if (points[i] != start)
                    s.Append('-').Append(points[i]);
            }

            return s.ToString();
        }

        private static List<string> Distinct(List<string> items)
        {
            List<string> o = new List<string>();
            foreach (string s in items)
            {
                if (!o.Contains(s))
                    o.Add(s);
            }

            return o;
        }

        private static string Join(double[] v, string format)
        {
            StringBuilder s = new StringBuilder();
            for (int i = 0; i < v.Length; i++)
                s.Append(i > 0 ? " " : "").Append(MavF15AfitResearchSourceDynamics.StateNames[i]).Append(' ').Append(v[i].ToString(format, CultureInfo.InvariantCulture));
            return s.ToString();
        }

        private static bool AllFinite(double[] v)
        {
            foreach (double x in v)
            {
                if (double.IsNaN(x) || double.IsInfinity(x))
                    return false;
            }

            return true;
        }

        private static float[] TrimFingerprint()
        {
            List<float> f = new List<float>();
            foreach (int point in new[] { 1, 46 })
            {
                MavF15TableViiState s = SymmetricState(point);
                MavF15ResearchSymmetricTrimResult r = MavF15AfitResearchTrimSolver.SolveSymmetric(MavF15AfitResearchIdentity.ConfigurationId,
                    s.trueVelocityFtPerSec, new MavF15ResearchSymmetricTrimGuess((float)s.alphaDeg, (float)s.stabilatorDeg, (float)s.thetaDeg));
                f.Add(r.alphaDeg);
                f.Add(r.symmetricStabilatorDeg);
                f.Add(r.pitchAttitudeDeg);
                f.Add(r.drivenResidualNorm);
            }

            foreach (int point in new[] { 117, 150, 199 })
            {
                MavF15TableViiState s = TurningState(point);
                MavF15ResearchTurningTrimResult r = MavF15AfitResearchTrimSolver.SolveTurning(MavF15AfitResearchIdentity.ConfigurationId,
                    s.phiDeg, MavF15TableViiTurningRecovery.Guess(MavF15TableViiTurningRecovery.Published(s)));
                double[] x = MavF15TableViiTurningRecovery.Values(r.solution);
                foreach (double v in x)
                    f.Add((float)v);
                f.Add(r.drivenResidualNorm);
            }

            return f.ToArray();
        }

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
                    float alphaRad = alphaDeg[a] * Mathf.Deg2Rad;
                    MavF15BaumannSurfaceState surfaces = new MavF15BaumannSurfaceState { symmetricStabilatorDeg = stabDeg[e] };
                    c[n++] = MavF15BaumannMach06Longitudinal.Evaluate(alphaRad, stabDeg[e], 0.01f);
                    c[n++] = MavF15BaumannMach06LateralDirectional.Evaluate(alphaRad, 0.05f, surfaces, 0.01f, 0.01f);
                }
            }

            return c;
        }

        /// <summary>The file's source with line comments removed, or null.</summary>
        private static string CodeOf(string path)
        {
            if (!File.Exists(path))
                return null;
            StringBuilder code = new StringBuilder(32768);
            foreach (string line in File.ReadAllLines(path))
            {
                int comment = line.IndexOf("//", StringComparison.Ordinal);
                code.AppendLine(comment >= 0 ? line.Substring(0, comment) : line);
            }

            return code.ToString();
        }

        private static string ResolveFlightDynamicsRoot()
        {
            try
            {
                string fromUnity = Path.Combine(Application.dataPath, MavFlightDynamicsOwnershipScan.FlightDynamicsRelativePath);
                if (Directory.Exists(fromUnity))
                    return fromUnity;
            }
            catch (Exception)
            {
                // No Unity player loaded. Fall through to the directory walk.
            }

            DirectoryInfo dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            for (int depth = 0; depth < 8 && dir != null; depth++)
            {
                string candidate = Path.Combine(Path.Combine(dir.FullName, "Assets"), MavFlightDynamicsOwnershipScan.FlightDynamicsRelativePath);
                if (Directory.Exists(candidate))
                    return candidate;
                dir = dir.Parent;
            }

            return null;
        }

        private static void Record(bool condition, string label, StringBuilder report, ref int passed, ref int failed)
        {
            if (condition) passed++; else failed++;
            report.Append(condition ? "  PASS  " : "  FAIL  ").AppendLine(label);
        }
    }
}
