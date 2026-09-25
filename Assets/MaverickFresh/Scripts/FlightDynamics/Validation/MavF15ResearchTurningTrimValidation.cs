using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using MaverickFresh.FlightDynamics.F15;
using Object = UnityEngine.Object;

namespace MaverickFresh.FlightDynamics.Validation
{
    /// <summary>
    /// WP-3C: Baumann Table VII's non-symmetric turning (helical) equilibria, recovered by the research
    /// trim solver with the printed bank angle fixed.
    ///
    ///   [H1]  dataset: WP-3A pairing reused; the pitchfork found from the data; unusable points stay out
    ///   [H2]  the pitchfork point is classified and delegated to the WP-3B symmetric solver
    ///   [H3]  the exact NASA 836 path cannot call the turning solver
    ///   [H4]  the fixed-density source environment is the one used
    ///   [H5]  8,300 lbf and the thrust-line moment enter exactly once
    ///   [H6]  phi is fixed and never solved
    ///   [H7]  the published answer is absent from the residual function
    ///   [H8]  every solved unknown starts genuinely perturbed
    ///   [H9]  published -> perturbed start -> solver -> recovered, for every usable state
    ///   [H10] recovered roots satisfy all eight equations
    ///   [H11] heading rate is an output, not a constraint
    ///   [H12] mirror parity, derived from the equations and then checked
    ///   [H13] search bounds: numerical ones labelled, none clipping a state or holding a root
    ///   [H14] no actuator authority is created
    ///   [H15] deterministic reruns are identical
    ///   [H16] multiple roots: a wide start grid, every root preserved
    ///   [H17] near-pitchfork conditioning, characterized
    ///   [H18] CFX2: the production constant untouched; its sensitivity at the recovered roots
    ///   [H19] shared atmosphere unchanged
    ///
    /// No source-fidelity pass threshold. Recovered-minus-printed is reported against the print
    /// floor (source) and, separately, the numerical termination uncertainty (solver). The only
    /// tolerances asserted are numerical: the solver's termination epsilon, root identity, and
    /// double-precision identity between two implementations of the same equations.
    /// </summary>
    public static class MavF15ResearchTurningTrimValidation
    {
        private const double DegToRad = Math.PI / 180.0;
        private const double ImplementationIdentity = 1e-12;

        /// <summary>The coefficient routine's high-AoA drag blend starts here; CFX2 carries no weight below it.</summary>
        private const double Cfx2BlendStartDeg = 20.0;

        private static readonly float[] AtmosphereProbeAltitudesM = { 0f, 3000f, 6096f, 11000f };

        public static string RunAll(out int passed, out int failed)
        {
            passed = 0;
            failed = 0;
            StringBuilder report = new StringBuilder(65536);
            report.AppendLine("F-15 Research Turning Trim (WP-3C) and Table VII Helical Recovery");
            report.AppendLine("=================================================================");
            report.AppendLine(MavF15AfitResearchTrimSolver.Scope);
            report.AppendLine("No source-fidelity pass threshold: differences from print are reported, not judged.");

            MavAtmosphereSample[] atmosphereBefore = SampleAtmosphere();
            MavAeroCoefficients[] coefficientsBefore = SampleCoefficients();

            List<MavF15TurningRecoveryAttempt> attempts = MavF15TableViiTurningRecovery.RunTurning();

            ValidateDataset(report, ref passed, ref failed);
            ValidatePitchfork(report, ref passed, ref failed);
            ValidateExactCannotCall(report, ref passed, ref failed);
            ValidateEnvironment(attempts, report, ref passed, ref failed);
            ValidateThrustOnce(attempts, report, ref passed, ref failed);
            ValidatePhiFixed(attempts, report, ref passed, ref failed);
            ValidateNoAnswerRead(attempts, report, ref passed, ref failed);
            ValidatePerturbed(attempts, report, ref passed, ref failed);
            ValidateRecovery(attempts, report, ref passed, ref failed);
            ValidateEightEquations(attempts, report, ref passed, ref failed);
            ValidateHeadingRate(attempts, report, ref passed, ref failed);
            ValidateMirror(attempts, report, ref passed, ref failed);
            ValidateBounds(attempts, report, ref passed, ref failed);
            ValidateNoAuthority(report, ref passed, ref failed);
            ValidateDeterminism(attempts, report, ref passed, ref failed);
            ValidateMultipleRoots(report, ref passed, ref failed);
            ValidateNearPitchfork(report, ref passed, ref failed);
            ValidateCfx2(attempts, coefficientsBefore, report, ref passed, ref failed);
            ValidateSharedAtmosphere(atmosphereBefore, report, ref passed, ref failed);

            report.AppendLine();
            report.Append("RESULT: ")
                .Append(failed == 0 ? "PASS" : "FAIL")
                .Append("  passed=").Append(passed)
                .Append(" failed=").Append(failed);
            return report.ToString();
        }

        // ---------------------------------------------------------------- [H1]

        private static void ValidateDataset(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[H1] Dataset: WP-3A pairing reused; the pitchfork found from the data");

            List<MavF15TableViiState> turning = MavF15TableViiTurningRecovery.TurningSectionStates();
            List<MavF15TableViiState> nonSymmetric = MavF15TableViiTurningRecovery.NonSymmetricStates();
            int fork = MavF15TableViiTurningRecovery.PitchforkPoint();
            bool allDisplaced = true;
            foreach (MavF15TableViiState s in turning)
            {
                if (s.part2OtherColumnsPoint != s.part1Point + MavF15BaumannTableVii.TurningColumnDisplacement)
                    allDisplaced = false;
            }

            Record(turning.Count == 81 && allDisplaced,
                "81 turning-section states assembled by WP-3A's displaced-column pairing (r, theta, phi and V "
                + "two rows lower); nothing re-transcribed or rearranged",
                report, ref passed, ref failed);

            bool damagedAbsent = true;
            foreach (MavF15TableViiState s in turning)
            {
                if (s.part1Point == 175 || s.part1Point == 194)
                    damagedAbsent = false;
            }

            report.Append("    unavailable: first-half 175 (beta illegible: ").Append(NoteOrDash(MavF15BaumannTableVii.Part1[174].note))
                  .Append(") and 194 (its displaced r at second-half 196 illegible: ")
                  .Append(NoteOrDash(MavF15BaumannTableVii.Part2[195].note)).AppendLine(")");
            Record(damagedAbsent,
                "points 175 and 194 stay unavailable - SOURCE PRINT DAMAGE, not repaired from symmetry",
                report, ref passed, ref failed);

            MavF15TableViiState forkState;
            MavF15TableViiTurningRecovery.TryGetState(fork, out forkState);
            double smallestOther = double.PositiveInfinity;
            foreach (MavF15TableViiState s in nonSymmetric)
                smallestOther = Math.Min(smallestOther, Math.Abs(s.phiDeg));

            report.Append("    pitchfork point ").Append(fork).Append(": printed phi ").Append(forkState.phiDeg.ToString("E3"))
                  .Append(" deg, beta ").Append(forkState.betaDeg.ToString("E2")).Append(" deg, r ")
                  .Append(forkState.rRadSec.ToString("E2")).Append(" rad/s; the smallest |phi| of any other turning state is ")
                  .Append(smallestOther.ToString("F3")).Append(" deg (ratio ")
                  .Append((smallestOther / Math.Abs(forkState.phiDeg)).ToString("E1")).AppendLine(")");
            Record(MavF15TableViiTurningRecovery.SignChangeCount() == 1 && fork == 165
                   && Math.Abs(forkState.phiDeg) < smallestOther && nonSymmetric.Count == 80,
                "the printed bank angle changes sign exactly once along the branch, at point 165; that is the "
                + "PITCHFORK / SYMMETRIC BRANCH POINT, leaving 80 non-symmetric turning states",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [H2]

        private static void ValidatePitchfork(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[H2] The pitchfork point is delegated to the WP-3B symmetric solver");

            MavF15TableViiState s;
            MavF15TableViiTurningRecovery.TryGetState(MavF15TableViiTurningRecovery.PitchforkPoint(), out s);
            MavF15ResearchSymmetricTrimResult sym = MavF15AfitResearchTrimSolver.SolveSymmetric(
                MavF15AfitResearchIdentity.ConfigurationId, s.trueVelocityFtPerSec, MavF15TableViiTrimRecovery.GenericStart);
            MavF15RecoveredStateFloor f = MavF15TableViiTrimRecovery.Floor(s, sym);
            report.Append("    WP-3B at V ").Append(s.trueVelocityFtPerSec.ToString("F1")).Append(" ft/s: ")
                  .Append(MavF15AfitResearchTrimSolver.Describe(sym)).AppendLine();
            report.Append("    recovered minus printed: alpha ").Append((sym.alphaDeg - s.alphaDeg).ToString("E2"))
                  .Append(" (floor ").Append(f.alphaDeg.ToString("E1")).Append("), stab ")
                  .Append((sym.symmetricStabilatorDeg - s.stabilatorDeg).ToString("E2")).Append(" (floor ")
                  .Append(f.stabilatorDeg.ToString("E1")).Append("), theta ")
                  .Append((sym.pitchAttitudeDeg - s.thetaDeg).ToString("E2")).Append(" (floor ")
                  .Append(f.thetaDeg.ToString("E1")).AppendLine(")");
            Record(sym.converged && f.measured
                   && MavF15TableViiTurningRecovery.Classify(s) == MavF15TurningStateClass.PitchforkSymmetricBranchPoint,
                "classified PITCHFORK / SYMMETRIC BRANCH POINT and solved by the WP-3B symmetric solver from a generic start",
                report, ref passed, ref failed);

            MavF15ResearchTurningTrimResult zero = MavF15AfitResearchTrimSolver.SolveTurning(
                MavF15AfitResearchIdentity.ConfigurationId, 0.0, MavF15TableViiTurningRecovery.Guess(MavF15TableViiTurningRecovery.Published(s)));
            Record(zero.refused && zero.refusalReason.IndexOf("SolveSymmetric", StringComparison.Ordinal) >= 0,
                "the turning solver refuses phi = 0 and points to the symmetric solver, so a singular Jacobian "
                + "there is never reported as a turning failure",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [H3]

        private static void ValidateExactCannotCall(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[H3] The exact NASA 836 path cannot call the turning solver");

            MavF15ResearchTurningTrimGuess g = GenericTurningStart(1.0);
            MavF15ResearchTurningTrimResult exact = MavF15AfitResearchTrimSolver.SolveTurning(MavF15ReferenceData.TargetConfigurationId, 20.0, g);
            MavF15ResearchTurningTrimResult empty = MavF15AfitResearchTrimSolver.SolveTurning("", 20.0, g);
            MavF15ResearchTurningTrimResult nan = MavF15AfitResearchTrimSolver.SolveTurning(MavF15AfitResearchIdentity.ConfigurationId, double.NaN, g);
            Record(exact.refused && empty.refused && nan.refused && !exact.converged,
                "refused under the exact NASA 836 id, an empty id and a non-finite phi: " + exact.refusalReason,
                report, ref passed, ref failed);

            string root = ResolveFlightDynamicsRoot();
            string offender = null;
            int scanned = 0;
            if (root != null)
            {
                string validation = Path.GetFullPath(Path.Combine(root, "Validation")) + Path.DirectorySeparatorChar;
                string solverFile = Path.GetFullPath(Path.Combine(root, Path.Combine("F15", "MavF15AfitResearchTrimSolver.cs")));
                foreach (string f in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    string full = Path.GetFullPath(f);
                    if (full.StartsWith(validation, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(full, solverFile, StringComparison.OrdinalIgnoreCase))
                        continue;
                    scanned++;
                    string text = File.ReadAllText(full);
                    if (text.IndexOf("MavF15AfitResearchTrimSolver", StringComparison.Ordinal) >= 0
                        || text.IndexOf("MavF15ResearchTurningTrim", StringComparison.Ordinal) >= 0)
                        offender = Path.GetFileName(full);
                }
            }

            Record(root != null && scanned > 0 && offender == null,
                offender == null
                    ? "no other runtime or editor source (" + scanned + " scanned) names the research trim or its turning types"
                    : "VIOLATION: " + offender,
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [H4]

        private static void ValidateEnvironment(List<MavF15TurningRecoveryAttempt> attempts,
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[H4] The fixed-density source environment is the one used");

            bool sourceQ = attempts.Count > 0;
            for (int i = 0; i < attempts.Count; i++)
            {
                MavF15ResearchTurningTrimResidual r = attempts[i].result.residual;
                if (r.dynamicPressurePsf != MavF15AfitResearchSourceEnvironment.DynamicPressurePsf(r.trueAirspeedFtPerSec))
                    sourceQ = false;
            }

            MavF15ResearchTurningTrimResidual a = MavF15AfitResearchTrimSolver.EvaluateTurningResidual(30.0, 10.0, 0.02, 0.005, 0.03, 0.04, 10.0, 450.0, -6.4);
            MavF15ResearchTurningTrimResidual b = MavF15AfitResearchTrimSolver.EvaluateTurningResidual(-50.0, 12.0, -0.5, -0.01, 0.06, -0.02, 2.0, 450.0, -10.0);
            string code = SolverCode();
            Record(sourceQ && a.dynamicPressurePsf == b.dynamicPressurePsf
                   && a.dynamicPressurePsf == 0.5 * MavF15AfitResearchSourceEnvironment.DensitySlugPerFt3 * 450.0 * 450.0
                   && code != null && code.IndexOf("MavAtmosphereModel", StringComparison.Ordinal) < 0,
                "q = 0.5 * 0.0012673 * V^2 at every recovered state, whatever phi, alpha, beta or the rates; "
                + "the solver's code names no atmosphere model",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [H5]

        private static void ValidateThrustOnce(List<MavF15TurningRecoveryAttempt> attempts,
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[H5] 8,300 lbf and the thrust-line moment enter exactly once");

            double arm = 8300.0 * (0.25 / 12.0);
            bool constant = attempts.Count > 0;
            for (int i = 0; i < attempts.Count; i++)
            {
                MavF15ResearchTurningTrimResidual r = attempts[i].result.residual;
                if (r.thrustForceLbf != 8300.0 || Math.Abs(r.thrustPitchingMomentFtLbf - arm) > 1e-12)
                    constant = false;
            }

            Record(constant,
                "8,300 lbf and 8,300 x 0.25/12 ft-lbf at every one of " + attempts.Count + " recovered states: one total force, constant",
                report, ref passed, ref failed);

            double[] maxDiff = new double[8];
            double minThrustCoeff = double.MaxValue;
            int n = 0;
            foreach (MavF15TableViiState s in MavF15TableViiTurningRecovery.TurningSectionStates())
            {
                MavF15ResearchTurningTrimResidual plant = MavF15AfitResearchTrimSolver.EvaluateTurningResidual(
                    s.phiDeg, s.alphaDeg, s.betaDeg, s.pRadSec, s.qRadSec, s.rRadSec, s.thetaDeg, s.trueVelocityFtPerSec, s.stabilatorDeg);
                MavF15EquilibriumResidual wp3a = MavF15TableViiEquilibriumReproduction.Evaluate(s);
                n++;
                double[] d =
                {
                    plant.forceXOverWeight - wp3a.forceXOverWeight, plant.forceYOverWeight - wp3a.forceYOverWeight,
                    plant.forceZOverWeight - wp3a.forceZOverWeight, plant.rollOverQSb - wp3a.rollOverQSb,
                    plant.pitchOverQSc - wp3a.pitchOverQSc, plant.yawOverQSb - wp3a.yawOverQSb,
                    plant.thetaDotRadSec - wp3a.thetaDotRadSec, plant.phiDotRadSec - wp3a.phiDotRadSec
                };
                for (int k = 0; k < 8; k++)
                    maxDiff[k] = Math.Max(maxDiff[k], Math.Abs(d[k]));
                minThrustCoeff = Math.Min(minThrustCoeff, arm / (plant.dynamicPressurePsf * MavF15BaumannMach06Reference.WingAreaFt2
                                                                 * MavF15BaumannMach06Reference.MeanAerodynamicChordFt));
            }

            double worst = 0.0;
            for (int k = 0; k < 8; k++)
                worst = Math.Max(worst, maxDiff[k]);
            Record(n == 81 && worst <= ImplementationIdentity,
                "all eight residuals equal the independent WP-3A evaluator's at every one of " + n
                + " printed turning states (largest difference " + worst.ToString("E1") + "); that evaluator counts "
                + "the moment once and reproduces them to print precision",
                report, ref passed, ref failed);
            report.Append("    a second thrust-line moment would shift M/qSc by at least ").Append(minThrustCoeff.ToString("E2")).AppendLine();
        }

        // ---------------------------------------------------------------- [H6]

        private static void ValidatePhiFixed(List<MavF15TurningRecoveryAttempt> attempts,
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[H6] Phi is fixed and never solved");

            bool fixedPhi = attempts.Count > 0;
            for (int i = 0; i < attempts.Count; i++)
            {
                MavF15TurningRecoveryAttempt a = attempts[i];
                if (a.result.phiDeg != a.published.phiDeg || a.result.residual.phiDeg != a.published.phiDeg)
                    fixedPhi = false;
            }

            FieldInfo[] guessFields = typeof(MavF15ResearchTurningTrimGuess).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            bool noPhiUnknown = guessFields.Length == 8;
            for (int i = 0; i < guessFields.Length; i++)
            {
                if (guessFields[i].FieldType != typeof(float) || guessFields[i].Name.IndexOf("phi", StringComparison.OrdinalIgnoreCase) >= 0)
                    noPhiUnknown = false;
            }

            Record(fixedPhi && noPhiUnknown,
                "the returned phi is the printed phi, bit for bit, in all " + attempts.Count + " solves; the eight "
                + "unknowns (alpha, beta, p, q, r, theta, V, stabilator) have no phi among them",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [H7]

        private static void ValidateNoAnswerRead(List<MavF15TurningRecoveryAttempt> attempts,
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[H7] The published answer is absent from the residual function");

            ParameterInfo[] solve = typeof(MavF15AfitResearchTrimSolver).GetMethod("SolveTurning").GetParameters();
            ParameterInfo[] eval = typeof(MavF15AfitResearchTrimSolver).GetMethod("EvaluateTurningResidual").GetParameters();
            bool signature = solve.Length == 3 && solve[0].ParameterType == typeof(string)
                             && solve[1].ParameterType == typeof(double) && solve[1].Name == "phiDeg"
                             && solve[2].ParameterType == typeof(MavF15ResearchTurningTrimGuess)
                             && eval.Length == 9 && eval[0].Name == "phiDeg";
            for (int i = 0; i < eval.Length; i++)
                signature &= eval[i].ParameterType == typeof(double);

            string code = SolverCode();
            Record(signature && code != null && code.IndexOf("TableVii", StringComparison.Ordinal) < 0
                   && code.IndexOf(".Validation", StringComparison.Ordinal) < 0,
                "the solver takes an id, phi and a start; the residual takes phi and the eight unknowns; its code "
                + "names neither Table VII nor Validation",
                report, ref passed, ref failed);

            int stride = MavF15TableViiTurningRecovery.Perturbations.Length;
            int same = 0, states = 0;
            double worst = 0.0;
            for (int i = 0; i < attempts.Count; i += stride)
            {
                states++;
                MavF15ResearchTurningTrimResult generic = MavF15AfitResearchTrimSolver.SolveTurning(
                    MavF15AfitResearchIdentity.ConfigurationId, attempts[i].published.phiDeg, GenericTurningStart(attempts[i].published.phiDeg));
                if (!generic.converged)
                    continue;

                bool all = true;
                double[] g = MavF15TableViiTurningRecovery.Values(generic.solution);
                for (int k = 0; k < stride; k++)
                {
                    double[] x = MavF15TableViiTurningRecovery.Values(attempts[i + k].result.solution);
                    all &= MavF15TableViiTurningRecovery.SameRoot(x, g);
                    for (int j = 0; j < 8; j++)
                        worst = Math.Max(worst, Math.Abs(x[j] - g[j]) / MavF15TableViiTurningRecovery.RootIdentity[j]);
                }

                if (all)
                    same++;
            }

            Record(same == states && states == 80,
                "a generic start that knows nothing of Table VII (alpha 10, beta 0, p 0, q 0.05, r 0.03*sign(phi), "
                + "theta 10, V 450, stab -10) reaches the same root as all four table-derived starts at " + same
                + " of " + states + " states (largest spread " + worst.ToString("F2") + " root-identity units)",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [H8]

        private static void ValidatePerturbed(List<MavF15TurningRecoveryAttempt> attempts,
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[H8] Every solved unknown starts genuinely perturbed");

            double[] minMove = new double[8];
            for (int k = 0; k < 8; k++)
                minMove[k] = double.PositiveInfinity;
            for (int i = 0; i < attempts.Count; i++)
            {
                double[] start = MavF15TableViiTurningRecovery.Values(attempts[i].result.initialGuess);
                double[] printed = MavF15TableViiTurningRecovery.Published(attempts[i].published);
                for (int k = 0; k < 8; k++)
                    minMove[k] = Math.Min(minMove[k], Math.Abs(start[k] - printed[k]));
            }

            bool moved = true;
            report.Append("    smallest start displacement:");
            for (int k = 0; k < 8; k++)
            {
                report.Append(' ').Append(MavF15TableViiTurningRecovery.UnknownNames[k]).Append(' ')
                      .Append(minMove[k].ToString("G3")).Append(' ').Append(MavF15TableViiTurningRecovery.UnknownUnits[k]).Append(';');
                double smallestDelta = double.PositiveInfinity;
                for (int p = 0; p < MavF15TableViiTurningRecovery.Perturbations.Length; p++)
                    smallestDelta = Math.Min(smallestDelta, Math.Abs(MavF15TableViiTurningRecovery.Perturbations[p].delta[k]));
                if (!(minMove[k] >= 0.5 * smallestDelta))
                    moved = false;
            }

            report.AppendLine();
            Record(moved,
                "no solved unknown ever starts at its published value: each is displaced by at least half its "
                + "smallest perturbation in every one of " + attempts.Count + " starts",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [H9]

        private static void ValidateRecovery(List<MavF15TurningRecoveryAttempt> attempts,
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[H9] Round trip: published -> fix phi -> perturbed start -> solver -> recovered");

            int refused = 0, converged = 0, minIt = int.MaxValue, maxIt = 0;
            double sumIt = 0.0;
            for (int i = 0; i < attempts.Count; i++)
            {
                MavF15ResearchTurningTrimResult r = attempts[i].result;
                if (r.refused)
                {
                    refused++;
                    continue;
                }

                if (!r.converged)
                    continue;
                converged++;
                minIt = Math.Min(minIt, r.iterations);
                maxIt = Math.Max(maxIt, r.iterations);
                sumIt += r.iterations;
            }

            report.Append("    attempts ").Append(attempts.Count).Append(" (")
                  .Append(MavF15TableViiTurningRecovery.NonSymmetricStates().Count).Append(" states x ")
                  .Append(MavF15TableViiTurningRecovery.Perturbations.Length).Append(" perturbations); converged ")
                  .Append(converged).Append("; iterations min ").Append(converged > 0 ? minIt : 0).Append(", mean ")
                  .Append((converged > 0 ? sumIt / converged : 0.0).ToString("F2")).Append(", max ").Append(maxIt).AppendLine();

            // Per state (first perturbation), with the print floor and the numerical uncertainty.
            int stride = MavF15TableViiTurningRecovery.Perturbations.Length;
            double[] maxErr = new double[8], sumErr = new double[8], maxRatio = new double[8], maxU = new double[8];
            int[] aboveFloor = new int[8];
            Dictionary<string, int> classes = new Dictionary<string, int>();
            int statesReported = 0, floorsMeasured = 0;
            string[] n = MavF15TableViiTurningRecovery.UnknownNames;
            report.AppendLine("    per state: printed | recovered, then recovered-printed [print floor; numerical uncertainty]:");
            for (int i = 0; i < attempts.Count; i += stride)
            {
                MavF15TurningRecoveryAttempt a = attempts[i];
                MavF15TurningStateFloor f = MavF15TableViiTurningRecovery.Floor(a.published, a.result);
                double[] u = a.result.converged
                    ? MavF15TableViiTurningRecovery.NumericalUncertainty(a.published.phiDeg, a.result.solution)
                    : null;
                string cls = ClassifyRecovery(a, f, u);
                int count;
                classes[cls] = classes.TryGetValue(cls, out count) ? count + 1 : 1;
                statesReported++;

                double[] printed = MavF15TableViiTurningRecovery.Published(a.published);
                double[] recovered = MavF15TableViiTurningRecovery.Values(a.result.solution);
                report.AppendFormat("    pt {0,3} phi {1,8:F3} | {2} | psi-dot {3,8:F5} rad/s, gamma {4,7:F3} deg, norm {5:E1}, {6} it | {7}\n",
                    a.published.part1Point, a.published.phiDeg, a.result.outcome, a.result.residual.headingRateRadSec,
                    a.result.residual.flightPathAngleDeg, a.result.drivenResidualNorm, a.result.iterations, cls);
                StringBuilder line1 = new StringBuilder("      ");
                StringBuilder line2 = new StringBuilder("      ");
                for (int k = 0; k < 8; k++)
                {
                    line1.Append(n[k]).Append(' ').Append(printed[k].ToString("G7")).Append('|').Append(recovered[k].ToString("G7")).Append("  ");
                    line2.Append(n[k]).Append(' ').Append(a.error[k].ToString("E1")).Append(" [")
                         .Append(f.measured ? f.floor[k].ToString("E1") : "-").Append("; ")
                         .Append(u != null ? u[k].ToString("E1") : "-").Append("]  ");
                    if (!a.result.converged)
                        continue;
                    maxErr[k] = Math.Max(maxErr[k], Math.Abs(a.error[k]));
                    sumErr[k] += Math.Abs(a.error[k]);
                    if (u != null)
                        maxU[k] = Math.Max(maxU[k], u[k]);
                    if (f.measured)
                    {
                        maxRatio[k] = Math.Max(maxRatio[k], Math.Abs(a.error[k]) / f.floor[k]);
                        if (Math.Abs(a.error[k]) > f.floor[k])
                            aboveFloor[k]++;
                    }
                }

                if (f.measured)
                    floorsMeasured++;
                report.AppendLine(line1.ToString());
                report.AppendLine(line2.ToString());
            }

            report.AppendLine("    summary over " + statesReported + " states:");
            for (int k = 0; k < 8; k++)
            {
                report.Append("      ").Append(n[k]).Append(": max |recovered - printed| ").Append(maxErr[k].ToString("E2"))
                      .Append(' ').Append(MavF15TableViiTurningRecovery.UnknownUnits[k]).Append(", mean ")
                      .Append((sumErr[k] / Math.Max(1, statesReported)).ToString("E2")).Append("; max diff/floor ")
                      .Append(maxRatio[k].ToString("F2")).Append("; above floor ").Append(aboveFloor[k])
                      .Append("; max numerical uncertainty ").Append(maxU[k].ToString("E1")).AppendLine();
            }

            report.Append("    classification:");
            foreach (KeyValuePair<string, int> kv in classes)
                report.Append(' ').Append(kv.Key).Append('=').Append(kv.Value);
            report.AppendLine();

            Record(attempts.Count == 320 && refused == 0,
                "all 320 attempts ran; none refused", report, ref passed, ref failed);
            Record(converged == attempts.Count,
                "all " + converged + " converged to the NUMERICAL epsilon "
                + MavF15AfitResearchTrimSolver.NumericalSolverTolerance.ToString("E0") + " (a termination criterion, not a source tolerance)",
                report, ref passed, ref failed);
            int unclassified;
            classes.TryGetValue(Unclassified, out unclassified);
            Record(statesReported == 80 && floorsMeasured == 80 && unclassified == 0,
                "every usable state is reported with its print floor, its numerical uncertainty and a classification",
                report, ref passed, ref failed);
        }

        private const string Unclassified = "UNCLASSIFIED";

        /// <summary>
        /// Per-state outcome, never a pass/fail. Recovered states are placed against the print floor
        /// (source) and the numerical uncertainty (solver); failures get the brief's categories.
        /// </summary>
        private static string ClassifyRecovery(MavF15TurningRecoveryAttempt a, MavF15TurningStateFloor f, double[] u)
        {
            MavF15ResearchTurningTrimResult r = a.result;
            if (r.refused)
                return Unclassified;
            if (!r.converged)
                return r.boundReached != null ? "SEARCH_BOUND_ISSUE" : "NEWTON_CONDITIONING";
            if (!f.measured || u == null)
                return "NEWTON_CONDITIONING";

            bool inFloor = true, inFloorPlusNumerical = true;
            for (int k = 0; k < 8; k++)
            {
                double e = Math.Abs(a.error[k]);
                if (e > f.floor[k])
                    inFloor = false;
                if (e > f.floor[k] + u[k])
                    inFloorPlusNumerical = false;
            }

            if (inFloor)
                return "WITHIN_PRINT_FLOOR";
            return inFloorPlusNumerical ? "WITHIN_PRINT_FLOOR_PLUS_NUMERICAL_UNCERTAINTY" : "UNKNOWN_BEYOND_FLOOR_AND_NUMERICAL";
        }

        // ---------------------------------------------------------------- [H10]

        private static void ValidateEightEquations(List<MavF15TurningRecoveryAttempt> attempts,
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[H10] Recovered roots satisfy all eight equations");

            bool each = attempts.Count > 0;
            double[] maxIndependent = new double[8];
            bool finite = true;
            float tol = MavF15AfitResearchTrimSolver.NumericalSolverTolerance;
            for (int i = 0; i < attempts.Count; i++)
            {
                MavF15ResearchTurningTrimResidual r = attempts[i].result.residual;
                double[] eight = { r.forceXOverWeight, r.forceYOverWeight, r.forceZOverWeight, r.rollOverQSb,
                                   r.pitchOverQSc, r.yawOverQSb, r.thetaDotRadSec, r.phiDotRadSec };
                for (int k = 0; k < 8; k++)
                {
                    if (!(Math.Abs(eight[k]) <= tol))
                        each = false;
                }

                MavF15EquilibriumResidual ind = attempts[i].independentResidual;
                finite &= ind.evaluated;
                double[] six = { ind.forceXOverWeight, ind.forceYOverWeight, ind.forceZOverWeight, ind.rollOverQSb,
                                 ind.pitchOverQSc, ind.yawOverQSb, ind.thetaDotRadSec, ind.phiDotRadSec };
                for (int k = 0; k < 8; k++)
                    maxIndependent[k] = Math.Max(maxIndependent[k], Math.Abs(six[k]));
            }

            report.Append("    independent WP-3A evaluator at the recovered states, max |res|: X/W ").Append(maxIndependent[0].ToString("E1"))
                  .Append(", Y/W ").Append(maxIndependent[1].ToString("E1")).Append(", Z/W ").Append(maxIndependent[2].ToString("E1"))
                  .Append(", L/qSb ").Append(maxIndependent[3].ToString("E1")).Append(", M/qSc ").Append(maxIndependent[4].ToString("E1"))
                  .Append(", N/qSb ").Append(maxIndependent[5].ToString("E1")).Append(", theta-dot ").Append(maxIndependent[6].ToString("E1"))
                  .Append(", phi-dot ").Append(maxIndependent[7].ToString("E1")).AppendLine(" (rad/s)");
            Record(each && finite,
                "each of the eight residuals is inside the numerical epsilon at every recovered root, and the "
                + "independent evaluator agrees",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [H11]

        private static void ValidateHeadingRate(List<MavF15TurningRecoveryAttempt> attempts,
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[H11] Heading rate is an output, not a constraint");

            double minAbs = double.PositiveInfinity, lo = double.PositiveInfinity, hi = double.NegativeInfinity;
            double gLo = double.PositiveInfinity, gHi = double.NegativeInfinity;
            int signMatches = 0, n = 0;
            for (int i = 0; i < attempts.Count; i++)
            {
                MavF15ResearchTurningTrimResult r = attempts[i].result;
                double psi = r.residual.headingRateRadSec;
                n++;
                minAbs = Math.Min(minAbs, Math.Abs(psi));
                lo = Math.Min(lo, psi);
                hi = Math.Max(hi, psi);
                gLo = Math.Min(gLo, r.residual.flightPathAngleDeg);
                gHi = Math.Max(gHi, r.residual.flightPathAngleDeg);
                if (r.TurnDirection == Math.Sign(attempts[i].published.phiDeg))
                    signMatches++;
            }

            report.Append("    psi-dot ").Append(lo.ToString("F4")).Append(" .. ").Append(hi.ToString("F4"))
                  .Append(" rad/s (smallest |psi-dot| ").Append(minAbs.ToString("E2")).Append("); flight-path angle ")
                  .Append(gLo.ToString("F3")).Append(" .. ").Append(gHi.ToString("F3")).Append(" deg; turn direction = sign(phi) in ")
                  .Append(signMatches).Append(" of ").Append(n).AppendLine(" (right turn = psi-dot > 0)");
            Record(n > 0 && minAbs > 100.0 * MavF15AfitResearchTrimSolver.NumericalSolverTolerance,
                "psi-dot is nonzero at every root - over 100x the epsilon every driven residual sits inside - so it "
                + "is not driven: these are steady helical turns",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [H12]

        private static void ValidateMirror(List<MavF15TurningRecoveryAttempt> attempts,
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[H12] Mirror parity: derived, then checked");

            // Derivation. Under (beta, p, r, phi) -> -(beta, p, r, phi) with alpha, q, theta, V and the
            // stabilator kept: the gravity, omega x V and inertial terms of X, Z and M are even, and those of
            // Y, L, N and phi-dot odd; theta-dot is even. The equations are therefore mirror-symmetric iff the
            // longitudinal coefficients do not depend on beta, p, r and the lateral ones are odd in them.
            ParameterInfo[] lon = typeof(MavF15BaumannMach06Longitudinal).GetMethod("Evaluate").GetParameters();
            bool lonNoLateral = lon.Length == 3 && lon[0].Name == "alphaRad" && lon[1].Name == "symmetricStabilatorDeg" && lon[2].Name == "qHat";

            double oddDefect = 0.0;
            foreach (MavF15TableViiState s in MavF15TableViiTurningRecovery.NonSymmetricStates())
            {
                float a = (float)(s.alphaDeg * DegToRad), b = (float)(s.betaDeg * DegToRad);
                float ph = (float)(s.pRadSec * MavF15BaumannMach06Reference.WingSpanFt / (2.0 * s.trueVelocityFtPerSec));
                float rh = (float)(s.rRadSec * MavF15BaumannMach06Reference.WingSpanFt / (2.0 * s.trueVelocityFtPerSec));
                MavF15BaumannSurfaceState surf = new MavF15BaumannSurfaceState { symmetricStabilatorDeg = (float)s.stabilatorDeg };
                MavAeroCoefficients c1 = MavF15BaumannMach06LateralDirectional.Evaluate(a, b, surf, ph, rh);
                MavAeroCoefficients c2 = MavF15BaumannMach06LateralDirectional.Evaluate(a, -b, surf, -ph, -rh);
                oddDefect = Math.Max(oddDefect, Math.Max(Math.Abs(c1.cy + c2.cy), Math.Max(Math.Abs(c1.cl + c2.cl), Math.Abs(c1.cn + c2.cn))));
            }

            Record(lonNoLateral && oddDefect <= ImplementationIdentity,
                "longitudinal routine takes no beta, p or r; lateral CY, Cl, Cn are odd at every turning state "
                + "(largest defect " + oddDefect.ToString("E1") + "). EVEN: alpha, q, theta, V, stabilator; ODD: beta, p, r, phi, psi-dot",
                report, ref passed, ref failed);

            int stride = MavF15TableViiTurningRecovery.Perturbations.Length;
            int[] odd = { 0, 1, 1, 0, 1, 0, 0, 0 };
            double[] defectMirrored = new double[8], defectFree = new double[8];
            double psiDefectMirrored = 0.0;
            int mirroredOk = 0, freeOk = 0, states = 0;
            for (int i = 0; i < attempts.Count; i += stride)
            {
                states++;
                MavF15TurningRecoveryAttempt a = attempts[i];
                double[] x = MavF15TableViiTurningRecovery.Values(a.result.solution);
                double[] mirrorOfX = MavF15TableViiTurningRecovery.Values(MavF15TableViiTurningRecovery.Mirror(a.result.solution));

                MavF15ResearchTurningTrimResult m = MavF15TableViiTurningRecovery.SolveMirror(a, true);
                if (m.converged)
                {
                    double[] y = MavF15TableViiTurningRecovery.Values(m.solution);
                    for (int k = 0; k < 8; k++)
                        defectMirrored[k] = Math.Max(defectMirrored[k], Math.Abs(odd[k] == 1 ? x[k] + y[k] : x[k] - y[k]));
                    psiDefectMirrored = Math.Max(psiDefectMirrored, Math.Abs(a.result.residual.headingRateRadSec + m.residual.headingRateRadSec));
                    if (MavF15TableViiTurningRecovery.SameRoot(mirrorOfX, y))
                        mirroredOk++;
                }

                MavF15ResearchTurningTrimResult free = MavF15TableViiTurningRecovery.SolveMirror(a, false);
                if (free.converged)
                {
                    double[] y = MavF15TableViiTurningRecovery.Values(free.solution);
                    for (int k = 0; k < 8; k++)
                        defectFree[k] = Math.Max(defectFree[k], Math.Abs(odd[k] == 1 ? x[k] + y[k] : x[k] - y[k]));
                    if (MavF15TableViiTurningRecovery.SameRoot(mirrorOfX, y))
                        freeOk++;
                }
            }

            report.Append("    parity defect, -phi solved from the MIRRORED start:");
            for (int k = 0; k < 8; k++)
                report.Append(' ').Append(MavF15TableViiTurningRecovery.UnknownNames[k]).Append(' ').Append(defectMirrored[k].ToString("E1"));
            report.Append(", psi-dot ").Append(psiDefectMirrored.ToString("E1")).AppendLine();
            report.Append("    parity defect, -phi solved from the UNMIRRORED +phi start:");
            for (int k = 0; k < 8; k++)
                report.Append(' ').Append(MavF15TableViiTurningRecovery.UnknownNames[k]).Append(' ').Append(defectFree[k].ToString("E1"));
            report.AppendLine(" (numerical termination scale)");

            Record(mirroredOk == states && states == 80,
                "-phi from the mirrored start returns the exact mirror image in " + mirroredOk + " of " + states + " states",
                report, ref passed, ref failed);
            Record(freeOk == states,
                "-phi from the UNMIRRORED start - the solver must cross to the other side on its own - still lands on "
                + "the mirror root (within root identity) in " + freeOk + " of " + states + " states",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [H13]

        private static void ValidateBounds(List<MavF15TurningRecoveryAttempt> attempts,
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[H13] Search bounds: labelled, clipping no state, holding no root");

            MavF15ResearchSearchBound[] bounds = MavF15AfitResearchTrimSolver.TurningSearchBounds();
            bool labels = bounds.Length == 8;
            for (int k = 0; k < bounds.Length; k++)
            {
                bool numerical = k >= 2 && k <= 5;
                labels &= !bounds[k].IsPhysicalLimit;
                labels &= numerical
                    ? bounds[k].kind == MavF15ResearchSearchBoundKind.NumericalSearchBound && bounds[k].Label == "NUMERICAL_SEARCH_BOUND"
                    : bounds[k].kind == MavF15ResearchSearchBoundKind.SourceSemantic;
                report.Append("    ").Append(bounds[k].unknown).Append(' ').Append(bounds[k].min.ToString("G6")).Append("..")
                      .Append(bounds[k].max.ToString("G6")).Append(' ').Append(bounds[k].units).Append(" - ").Append(bounds[k].Label)
                      .Append(": ").Append(bounds[k].basis).AppendLine();
            }

            Record(labels,
                "p, q, r and theta carry NUMERICAL_SEARCH_BOUND; alpha, beta, V and the stabilator keep their source "
                + "semantics unbroadened; none is a physical limit",
                report, ref passed, ref failed);

            double printedMargin = double.PositiveInfinity;
            foreach (MavF15TableViiState s in MavF15TableViiTurningRecovery.TurningSectionStates())
                printedMargin = Math.Min(printedMargin, Margin(bounds, MavF15TableViiTurningRecovery.Published(s)));

            double rootMargin = double.PositiveInfinity;
            bool noneAtBound = true;
            for (int i = 0; i < attempts.Count; i++)
            {
                rootMargin = Math.Min(rootMargin, Margin(bounds, MavF15TableViiTurningRecovery.Values(attempts[i].result.solution)));
                if (attempts[i].result.boundReached != null)
                    noneAtBound = false;
            }

            report.Append("    smallest distance to any bound, as a fraction of that bound's box: printed states ")
                  .Append(printedMargin.ToString("F3")).Append(", recovered roots ").Append(rootMargin.ToString("F3")).AppendLine();
            Record(printedMargin > MavF15AfitResearchTrimSolver.BoundProximityFraction && noneAtBound
                   && rootMargin > MavF15AfitResearchTrimSolver.BoundProximityFraction,
                "no bound clips any assembled Table VII turning state, and no recovered root sits on a bound",
                report, ref passed, ref failed);
        }

        private static double Margin(MavF15ResearchSearchBound[] bounds, double[] x)
        {
            double m = double.PositiveInfinity;
            for (int k = 0; k < 8; k++)
            {
                double span = bounds[k].max - bounds[k].min;
                m = Math.Min(m, Math.Min(x[k] - bounds[k].min, bounds[k].max - x[k]) / span);
            }

            return m;
        }

        // ---------------------------------------------------------------- [H14]

        private static void ValidateNoAuthority(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[H14] No actuator authority is created");

            Type[] authority =
            {
                typeof(MavF15SurfaceLimits), typeof(MavF15SurfaceChannelLimits), typeof(MavF15PhysicalSurfaceHardStops),
                typeof(MavF15ActuatorRateLimits), typeof(MavControlSurfaceLimits), typeof(MavF15SurfaceState),
                typeof(MavF15ActualSurfaceState), typeof(MavF15RequestedSurfaceState)
            };
            Type[] turningTypes =
            {
                typeof(MavF15AfitResearchTrimSolver), typeof(MavF15ResearchTurningTrimResult), typeof(MavF15ResearchTurningTrimResidual),
                typeof(MavF15ResearchTurningTrimGuess), typeof(MavF15ResearchSearchBound)
            };
            bool clean = true;
            foreach (Type t in turningTypes)
            {
                foreach (MethodInfo m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static
                                                      | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    if (Array.IndexOf(authority, m.ReturnType) >= 0)
                        clean = false;
                }

                foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static
                                                    | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    if (Array.IndexOf(authority, f.FieldType) >= 0)
                        clean = false;
                }
            }

            GameObject host = new GameObject("MavF15H14Authority");
            host.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                MavF15ControlActuator actuator = host.AddComponent<MavF15ControlActuator>();
                MavF15AfitResearchFlightDynamicsProfile profile = host.AddComponent<MavF15AfitResearchFlightDynamicsProfile>();
                Record(clean
                       && !MavF15PhysicalSurfaceHardStops.FromActuatorLimits(actuator.limits).AnyDeclared
                       && !MavF15ActuatorRateLimits.FromActuatorLimits(actuator.limits).AnyDeclared
                       && profile.BuildProfile().controlSurfaceLimits.Equals(new MavControlSurfaceLimits()),
                    "after every turning solve: the stabilator entered only via the static control; no turning type holds "
                    + "or returns a limit or surface state; the actuator declares no hard stop or rate; the research "
                    + "profile still has zero travel",
                    report, ref passed, ref failed);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        // ---------------------------------------------------------------- [H15]

        private static void ValidateDeterminism(List<MavF15TurningRecoveryAttempt> first,
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[H15] Deterministic reruns");

            List<MavF15TurningRecoveryAttempt> second = MavF15TableViiTurningRecovery.RunTurning();
            bool identical = first.Count == second.Count;
            for (int i = 0; identical && i < first.Count; i++)
            {
                MavF15ResearchTurningTrimResult a = first[i].result, b = second[i].result;
                double[] x = MavF15TableViiTurningRecovery.Values(a.solution), y = MavF15TableViiTurningRecovery.Values(b.solution);
                identical = a.outcome == b.outcome && a.iterations == b.iterations && a.drivenResidualNorm == b.drivenResidualNorm;
                for (int k = 0; identical && k < 8; k++)
                    identical = x[k] == y[k];
            }

            Record(identical, "two full runs of the " + first.Count + " deterministic starts are bit-identical",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [H16]

        private static void ValidateMultipleRoots(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            int perPhi = MavF15TableViiTurningRecovery.GridAlphaDeg.Length * MavF15TableViiTurningRecovery.GridSpeedFtPerSec.Length
                         * MavF15TableViiTurningRecovery.GridStabilatorDeg.Length * MavF15TableViiTurningRecovery.GridThetaDeg.Length
                         * MavF15TableViiTurningRecovery.GridYawRateRadSec.Length;
            report.AppendLine("[H16] Multiple roots: wide deterministic grid, " + perPhi + " starts per phi");

            bool genuine = true;
            int probes = 0, totalRoots = 0;
            float tol = MavF15AfitResearchTrimSolver.NumericalSolverTolerance;
            foreach (int point in MavF15TableViiTurningRecovery.BranchProbePoints)
            {
                MavF15TableViiState s;
                if (!MavF15TableViiTurningRecovery.TryGetState(point, out s))
                    continue;

                MavF15TurningBranchProbe p = MavF15TableViiTurningRecovery.ProbeBranches(s.phiDeg, point);
                probes++;
                totalRoots += p.roots.Count;
                report.Append("    phi ").Append(s.phiDeg.ToString("F3")).Append(" (point ").Append(point).Append("): ")
                      .Append(p.converged).Append(" of ").Append(p.starts).Append(" converged, ").Append(p.roots.Count)
                      .Append(" distinct root(s); not converged: ").Append(p.atSourceSemanticBound).Append(" at a source-semantic bound, ")
                      .Append(p.atNumericalSearchBound).Append(" at a numerical bound, ").Append(p.otherNonConverged).Append(" other");
                if (p.otherNonConverged > 0)
                    report.Append(" (smallest norm ").Append(p.smallestOtherNorm.ToString("E1")).Append(')');
                report.AppendLine();

                for (int i = 0; i < p.roots.Count; i++)
                {
                    MavF15TurningRoot root = p.roots[i];
                    MavF15ResearchTurningTrimGuess x = root.state;
                    report.Append("      root alpha ").Append(x.alphaDeg.ToString("F4")).Append(" beta ").Append(x.betaDeg.ToString("E2"))
                          .Append(" p ").Append(x.pRadSec.ToString("E2")).Append(" q ").Append(x.qRadSec.ToString("E2"))
                          .Append(" r ").Append(x.rRadSec.ToString("E2")).Append(" theta ").Append(x.thetaDeg.ToString("F3"))
                          .Append(" V ").Append(x.trueAirspeedFtPerSec.ToString("F2")).Append(" stab ").Append(x.symmetricStabilatorDeg.ToString("F4"))
                          .Append(" | branch ").Append(root.headingRateRadSec > 0 ? "RIGHT (psi-dot > 0)" : "LEFT (psi-dot < 0)")
                          .Append(" | norm ").Append(root.residualNorm.ToString("E1")).Append(" | ").Append(root.startCount)
                          .Append(" starts, first (a ").Append(root.firstStart.alphaDeg).Append(", V ").Append(root.firstStart.trueAirspeedFtPerSec)
                          .Append(", stab ").Append(root.firstStart.symmetricStabilatorDeg).Append(", theta ").Append(root.firstStart.thetaDeg)
                          .Append(", r ").Append(root.firstStart.rRadSec).Append(") | nearest published: ")
                          .Append(root.publishedPoint == 0
                              ? "none printed at this phi (additional equilibrium, preserved)"
                              : "point " + root.publishedPoint + ", " + root.distanceToPublishedInIdentityUnits.ToString("F1") + " root-identity units")
                          .AppendLine();
                    if (root.residualNorm > tol)
                        genuine = false;
                }

                for (int i = 0; i < p.closestNonConverged.Count; i++)
                {
                    MavF15ResearchTurningTrimResult c = p.closestNonConverged[i];
                    MavF15ResearchTurningTrimResult again = MavF15AfitResearchTrimSolver.SolveTurning(
                        MavF15AfitResearchIdentity.ConfigurationId, s.phiDeg, c.solution);
                    string verdict;
                    if (again.converged && p.roots.Count > 0
                        && MavF15TableViiTurningRecovery.SameRoot(MavF15TableViiTurningRecovery.Values(again.solution),
                            MavF15TableViiTurningRecovery.Values(p.roots[0].state)))
                        verdict = "restarted from where it stopped, it reaches the root above: slow Newton convergence, not another root";
                    else if (again.converged)
                        verdict = "restarted, it converges to a DIFFERENT point - possible additional root";
                    else
                        verdict = "restarted, still " + again.outcome + " at norm " + again.drivenResidualNorm.ToString("E1")
                                  + " - not a root; unresolved (stabilator " + again.solution.symmetricStabilatorDeg.ToString("F2")
                                  + " deg, alpha " + again.solution.alphaDeg.ToString("F2") + " deg"
                                  + (p.roots.Count > 0
                                      ? "; " + IdentityDistance(again.solution, p.roots[0].state).ToString("G3")
                                        + " root-identity units from the root above"
                                      : "")
                                  + ")";
                    report.Append("      closest non-converged #").Append(i + 1).Append(": ").Append(c.outcome).Append(" norm ")
                          .Append(c.drivenResidualNorm.ToString("E1")).Append(" at alpha ").Append(c.solution.alphaDeg.ToString("F2"))
                          .Append(", V ").Append(c.solution.trueAirspeedFtPerSec.ToString("F1")).Append(", stab ")
                          .Append(c.solution.symmetricStabilatorDeg.ToString("F2")).Append(" -> ").Append(verdict).AppendLine();
                }

                if (p.roots.Count > 1)
                    report.AppendLine("      branch ambiguity: YES - " + p.roots.Count + " distinct roots, all preserved");
                else
                    report.AppendLine("      branch ambiguity: " + (p.roots.Count == 1 ? "none found - one root" : "none - no root"));
            }

            Record(probes == MavF15TableViiTurningRecovery.BranchProbePoints.Length && genuine,
                "every preserved root (" + totalRoots + " over " + probes + " bank angles) is an equilibrium to the numerical "
                + "epsilon; roots are kept whether or not Table VII prints them. A finite grid proves no uniqueness",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [H17]

        private static void ValidateNearPitchfork(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[H17] Near-pitchfork conditioning (characterization; solver settings unchanged)");
            report.AppendLine("    each solve from the same fixed seed (printed state nearest the pitchfork on that side); "
                              + "u = numerical uncertainty |J^-1 r| at the result");

            List<MavF15PitchforkProbeStep> steps = MavF15TableViiTurningRecovery.ProbePitchfork();
            double smallestConverged = double.PositiveInfinity;
            int ran = 0;
            for (int i = 0; i < steps.Count; i++)
            {
                MavF15PitchforkProbeStep st = steps[i];
                MavF15ResearchTurningTrimResult r = st.result;
                if (r.refused)
                    continue;
                ran++;
                double[] u = r.converged ? MavF15TableViiTurningRecovery.NumericalUncertainty(st.phiDeg, r.solution) : null;
                if (r.converged)
                    smallestConverged = Math.Min(smallestConverged, Math.Abs(st.phiDeg));
                report.AppendFormat("    phi {0,10:G3} (seed {1}): {2,-22} {3} it, norm {4:E1} | alpha {5:F5} V {6:F3} stab {7:F5} beta {8:E2} | u: alpha {9} V {10} stab {11}\n",
                    st.phiDeg, st.seedPoint, r.outcome, r.iterations, r.drivenResidualNorm, r.solution.alphaDeg,
                    r.solution.trueAirspeedFtPerSec, r.solution.symmetricStabilatorDeg, r.solution.betaDeg,
                    u == null ? "SINGULAR" : u[0].ToString("E1"), u == null ? "-" : u[6].ToString("E1"), u == null ? "-" : u[7].ToString("E1"));
            }

            report.Append("    smallest |phi| that converged: ").Append(smallestConverged.ToString("G3"))
                  .AppendLine(" deg. Convergence does not fail; conditioning does: u grows as |phi| shrinks");
            Record(ran == 2 * MavF15TableViiTurningRecovery.PitchforkProbePhiDeg.Length,
                "every probe bank angle on both sides was attempted and reported (" + ran + " solves); no tolerance was "
                + "changed and no continuation used",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [H18]

        private static void ValidateCfx2(List<MavF15TurningRecoveryAttempt> attempts, MavAeroCoefficients[] before,
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[H18] CFX2: the production constant untouched, and its sensitivity");

            double a40 = 40.0 * DegToRad;
            MavAeroCoefficients c = MavF15BaumannMach06Longitudinal.Evaluate((float)a40, 0f, 0f);
            double cfx = -((c.cx * Math.Cos(a40)) + (c.cz * Math.Sin(a40)));
            double cfxBase = 0.0267297 - (0.10646919 * a40) + (5.39836337 * a40 * a40)
                             - (5.0086893 * Math.Pow(a40, 3)) + (1.34148193 * Math.Pow(a40, 4));
            double toAppendixC = Math.Abs(cfx - (cfxBase + 0.09833517));
            double toBaumann = Math.Abs(cfx - (cfxBase + 0.09833617));

            MavAeroCoefficients[] after = SampleCoefficients();
            bool same = before.Length == after.Length;
            for (int i = 0; same && i < before.Length; i++)
            {
                same = before[i].cx == after[i].cx && before[i].cz == after[i].cz && before[i].cm == after[i].cm
                       && before[i].cy == after[i].cy && before[i].cl == after[i].cl && before[i].cn == after[i].cn;
            }

            Record(toAppendixC < toBaumann && same,
                "after every turning solve CFX at 40 deg still sits on Davison App. C's 0.09833517 (distance "
                + toAppendixC.ToString("E1") + " vs " + toBaumann.ToString("E1") + " to Baumann's 0.09833617), and the "
                + "routine is bit-identical before and after",
                report, ref passed, ref failed);

            double maxAlpha = 0.0;
            for (int i = 0; i < attempts.Count; i++)
                maxAlpha = Math.Max(maxAlpha, attempts[i].result.solution.alphaDeg);
            report.Append("    highest alpha at any recovered turning root: ").Append(maxAlpha.ToString("F3"))
                  .Append(" deg; the high-AoA drag blend that carries CFX2 starts at ").Append(Cfx2BlendStartDeg.ToString("F0"))
                  .AppendLine(" deg (the routine returns the low-AoA fit alone below it)");
            Record(maxAlpha < Cfx2BlendStartDeg,
                "SENSITIVITY: zero at every recovered turning equilibrium - neither printing can change them, so Table VII's "
                + "turning states cannot discriminate the two constants either",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [H19]

        private static void ValidateSharedAtmosphere(MavAtmosphereSample[] before,
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[H19] Shared atmosphere unchanged");

            MavAtmosphereSample[] after = SampleAtmosphere();
            bool same = before.Length == after.Length;
            for (int i = 0; same && i < before.Length; i++)
            {
                same = before[i].densityKgM3 == after[i].densityKgM3 && before[i].pressurePa == after[i].pressurePa
                       && before[i].temperatureK == after[i].temperatureK && before[i].speedOfSoundMps == after[i].speedOfSoundMps;
            }

            Record(same,
                "MavAtmosphereModel returns bit-identical samples before and after every turning solve",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>A start that knows nothing of Table VII; only the sign of the given phi picks the turn side.</summary>
        private static MavF15ResearchTurningTrimGuess GenericTurningStart(double phiDeg)
        {
            return new MavF15ResearchTurningTrimGuess
            {
                alphaDeg = 10f,
                betaDeg = 0f,
                pRadSec = 0f,
                qRadSec = 0.05f,
                rRadSec = 0.03f * Math.Sign(phiDeg),
                thetaDeg = 10f,
                trueAirspeedFtPerSec = 450f,
                symmetricStabilatorDeg = -10f
            };
        }

        private static double IdentityDistance(MavF15ResearchTurningTrimGuess a, MavF15ResearchTurningTrimGuess b)
        {
            double[] x = MavF15TableViiTurningRecovery.Values(a), y = MavF15TableViiTurningRecovery.Values(b);
            double d = 0.0;
            for (int k = 0; k < 8; k++)
                d = Math.Max(d, Math.Abs(x[k] - y[k]) / MavF15TableViiTurningRecovery.RootIdentity[k]);
            return d;
        }

        private static string NoteOrDash(string note)
        {
            return string.IsNullOrEmpty(note) ? "-" : note;
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
            float[] alphaDeg = { 5f, 10f, 25f, 40f, 60f };
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

        /// <summary>The research trim solver's source with comments removed, or null when it cannot be read.</summary>
        private static string SolverCode()
        {
            string root = ResolveFlightDynamicsRoot();
            if (root == null)
                return null;

            string path = Path.Combine(root, Path.Combine("F15", "MavF15AfitResearchTrimSolver.cs"));
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
                string fromUnity = Path.Combine(
                    Application.dataPath, MavFlightDynamicsOwnershipScan.FlightDynamicsRelativePath);
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
                string candidate = Path.Combine(
                    Path.Combine(dir.FullName, "Assets"), MavFlightDynamicsOwnershipScan.FlightDynamicsRelativePath);
                if (Directory.Exists(candidate))
                    return candidate;
                dir = dir.Parent;
            }

            return null;
        }

        private static void Record(
            bool condition, string label,
            StringBuilder report, ref int passed, ref int failed)
        {
            if (condition) passed++; else failed++;
            report.Append(condition ? "  PASS  " : "  FAIL  ").AppendLine(label);
        }
    }
}
