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
    /// WP-3B: the source-faithful research trim solver, and Baumann's symmetric Table VII
    /// equilibria recovered with it.
    ///
    ///   [R1]  the source environment: fixed density, not ISA; q follows V alone
    ///   [R2]  thrust stays 8,300 lbf total, constant, outside every engine and F100 path
    ///   [R3]  the thrust-line pitching moment enters exactly once
    ///   [R4]  the printed symmetric Table VII states stay near-zero residuals in the trim plant
    ///   [R5]  published -> perturbed start -> solver -> recovered: the Table VII round trip
    ///   [R6]  the solver never reads the answer it is compared against
    ///   [R7]  deterministic starts give bit-identical results
    ///   [R8]  multiple roots and branches: preserved, never steered toward Table VII
    ///   [R9]  the stabilator search box is the demonstrated range, not a hard stop
    ///   [R10] the exact NASA 836 path cannot reach the research trim
    ///   [R11] the MavF100 layer is not used
    ///   [R12] the CFX2 coefficient is unchanged by trim
    ///   [R13] shared atmosphere unchanged; the runtime fixed-density gap is recorded, not hidden
    ///
    /// No source-fidelity pass threshold. Differences from the printed states are reported against
    /// the print-resolution floor, never judged by it. The only tolerances asserted are numerical:
    /// the solver's own termination epsilon, root identity, and double-precision identity between
    /// two implementations of the same equation.
    /// </summary>
    public static class MavF15ResearchTrimSolverValidation
    {
        private const double DegToRad = Math.PI / 180.0;
        private const double FtToM = 0.3048;
        private const double SlugPerFt3ToKgPerM3 = 515.378818;

        /// <summary>
        /// Double-precision identity between two implementations of the same equation. Both round
        /// the same float coefficients; only double arithmetic order differs.
        /// </summary>
        private const double ImplementationIdentity = 1e-12;

        /// <summary>V values probed for extra roots. 622.14 ft/s is Baumann's own Mach 0.6 speed (PDF pp.118-119).</summary>
        private static readonly double[] BranchProbeSpeedsFtPerSec =
            { 218.5, 288.7, 315.1, 342.9, 400.0, 500.0, 622.14, 699.7 };

        /// <summary>Table VII's symmetric state printed inside the turning section: the pitchfork point.</summary>
        private const int PitchforkPoint = 165;

        private static readonly float[] AtmosphereProbeAltitudesM = { 0f, 3000f, 6096f, 11000f };

        public static string RunAll(out int passed, out int failed)
        {
            passed = 0;
            failed = 0;
            StringBuilder report = new StringBuilder(32768);
            report.AppendLine("F-15 Research Trim Solver (WP-3B) and Table VII Symmetric Recovery");
            report.AppendLine("===================================================================");
            report.AppendLine(MavF15AfitResearchTrimSolver.Scope);
            report.AppendLine("No source-fidelity pass threshold: differences from print are reported, not judged.");

            MavAtmosphereSample[] atmosphereBefore = SampleAtmosphere();
            MavAeroCoefficients[] coefficientsBefore = SampleCoefficients();

            List<MavF15TrimRecoveryAttempt> attempts = MavF15TableViiTrimRecovery.RunSymmetric();

            ValidateEnvironment(report, ref passed, ref failed);
            ValidateThrust(attempts, report, ref passed, ref failed);
            ValidateThrustMomentOnce(report, ref passed, ref failed);
            ValidatePrintedStateResiduals(report, ref passed, ref failed);
            ValidateRecovery(attempts, report, ref passed, ref failed);
            ValidateNoAnswerRead(attempts, report, ref passed, ref failed);
            ValidateDeterminism(attempts, report, ref passed, ref failed);
            ValidateBranches(report, ref passed, ref failed);
            ValidateDemonstratedRangeNotHardStop(report, ref passed, ref failed);
            ValidateExactPathCannotReach(report, ref passed, ref failed);
            ValidateNoF100(report, ref passed, ref failed);
            ValidateCfx2Unchanged(coefficientsBefore, report, ref passed, ref failed);
            ValidateSharedAtmosphere(atmosphereBefore, report, ref passed, ref failed);

            report.AppendLine();
            report.Append("RESULT: ")
                .Append(failed == 0 ? "PASS" : "FAIL")
                .Append("  passed=").Append(passed)
                .Append(" failed=").Append(failed);
            return report.ToString();
        }

        // ---------------------------------------------------------------- [R1]

        private static void ValidateEnvironment(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[R1] Source environment: fixed density, not ISA; q follows V alone");

            Record(MavF15AfitResearchSourceEnvironment.DensitySlugPerFt3 == 0.0012673
                   && MavF15AfitResearchSourceEnvironment.GravityFtPerSec2 == 32.174
                   && MavF15AfitResearchSourceEnvironment.ReferenceAltitudeLabelFt == 20000.0,
                "RHO = 0.0012673 slug/ft^3 and G = 32.174 ft/s^2, as the driver prints them; 20,000 ft is a label",
                report, ref passed, ref failed);

            MethodInfo q = typeof(MavF15AfitResearchSourceEnvironment).GetMethod("DynamicPressurePsf");
            double q300 = MavF15AfitResearchSourceEnvironment.DynamicPressurePsf(300.0);
            double q600 = MavF15AfitResearchSourceEnvironment.DynamicPressurePsf(600.0);
            Record(q != null && q.GetParameters().Length == 1
                   && q300 == 0.5 * 0.0012673 * 300.0 * 300.0
                   && Math.Abs(q600 / q300 - 4.0) <= 1e-15,
                "q = 0.5*RHO*V^2 takes V as its only input; doubling V quadruples q",
                report, ref passed, ref failed);

            MavF15ResearchSymmetricTrimResidual a = MavF15AfitResearchTrimSolver.EvaluateSymmetricResidual(315.1, 10.0, -8.0, 5.0);
            MavF15ResearchSymmetricTrimResidual b = MavF15AfitResearchTrimSolver.EvaluateSymmetricResidual(315.1, 18.0, -20.0, 25.0);
            Record(a.evaluated && b.evaluated && a.dynamicPressurePsf == b.dynamicPressurePsf
                   && a.dynamicPressurePsf == MavF15AfitResearchSourceEnvironment.DynamicPressurePsf(315.1),
                "at fixed V the trim plant's q is the same for any alpha, stabilator and theta",
                report, ref passed, ref failed);

            MavAtmosphereSample isa = MavAtmosphereModel.Sample(MavF15CoefficientFitCondition.PressureAltitudeM);
            double isaSlug = isa.densityKgM3 / SlugPerFt3ToKgPerM3;
            double relative = (isaSlug - MavF15AfitResearchSourceEnvironment.DensitySlugPerFt3)
                              / MavF15AfitResearchSourceEnvironment.DensitySlugPerFt3;
            report.Append("    ISA density at 6,096 m = ").Append(isaSlug.ToString("E6"))
                  .Append(" slug/ft^3; relative to the source RHO: ").Append(relative.ToString("E3")).AppendLine();

            string code = SolverCode();
            Record(code != null && isaSlug != MavF15AfitResearchSourceEnvironment.DensitySlugPerFt3
                   && code.IndexOf("MavAtmosphereModel", StringComparison.Ordinal) < 0
                   && code.IndexOf("MavAtmosphereSample", StringComparison.Ordinal) < 0,
                "the trim plant's density is not the ISA density, and its code names no atmosphere model",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [R2]

        private static void ValidateThrust(List<MavF15TrimRecoveryAttempt> attempts,
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[R2] Thrust stays 8,300 lbf total, constant");

            double arm = 8300.0 * (0.25 / 12.0);
            bool constant = attempts.Count > 0;
            for (int i = 0; i < attempts.Count; i++)
            {
                MavF15ResearchSymmetricTrimResidual r = attempts[i].result.residual;
                if (r.thrustForceLbf != 8300.0 || Math.Abs(r.thrustPitchingMomentFtLbf - arm) > 1e-12)
                    constant = false;
            }

            Record(constant && MavF15AfitResearchThrustSource.SourceTotalThrustLbf == 8300f,
                "8,300 lbf and 8,300 x 0.25/12 ft-lbf at every one of " + attempts.Count
                + " recovered states (V " + MinV(attempts).ToString("F1") + "-" + MaxV(attempts).ToString("F1")
                + " ft/s), as ONE total-aircraft force: never per engine, never a coefficient",
                report, ref passed, ref failed);

            MavF15ResearchSymmetricTrimResidual slow = MavF15AfitResearchTrimSolver.EvaluateSymmetricResidual(250.0, 15.0, -12.0, 10.0);
            MavF15ResearchSymmetricTrimResidual fast = MavF15AfitResearchTrimSolver.EvaluateSymmetricResidual(690.0, 15.0, -12.0, 10.0);
            double ratio = slow.thrustPitchCoefficient / fast.thrustPitchCoefficient;
            Record(slow.thrustForceLbf == fast.thrustForceLbf
                   && Math.Abs(ratio / ((690.0 / 250.0) * (690.0 / 250.0)) - 1.0) <= 1e-12,
                "the force is constant in V; its moment coefficient scales with 1/q exactly as the source's "
                + "THRUST*(0.25/12)/(QBARS*CWING)",
                report, ref passed, ref failed);

            string forbidden = ForbiddenMemberName(new[] { "throttle", "requiredthrust", "engine" });
            Record(forbidden == null,
                forbidden == null
                    ? "no solver type has a throttle, required-thrust or engine member: thrust is not solved for"
                    : "UNEXPECTED MEMBER: " + forbidden,
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [R3]

        private static void ValidateThrustMomentOnce(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[R3] The thrust-line pitching moment enters exactly once");

            bool bookkeeping = true;
            bool routineHasNoThrust = true;
            double maxDiffX = 0.0, maxDiffZ = 0.0, maxDiffM = 0.0, minThrustCoeff = double.MaxValue, maxPitchFloor = 0.0;
            int n = 0;
            foreach (MavF15TableViiState s in MavF15TableViiTrimRecovery.SymmetricStates())
            {
                MavF15ResearchSymmetricTrimResidual plant = MavF15AfitResearchTrimSolver.EvaluateSymmetricResidual(
                    s.trueVelocityFtPerSec, s.alphaDeg, s.stabilatorDeg, s.thetaDeg);
                MavF15EquilibriumResidual wp3a = MavF15TableViiEquilibriumReproduction.Evaluate(s);
                n++;

                double expectedCoeff = 8300.0 * (0.25 / 12.0)
                    / (MavF15AfitResearchSourceEnvironment.DynamicPressurePsf(s.trueVelocityFtPerSec)
                       * MavF15BaumannMach06Reference.WingAreaFt2 * MavF15BaumannMach06Reference.MeanAerodynamicChordFt);
                if (Math.Abs(plant.pitchOverQSc - (plant.aeroCm + plant.thrustPitchCoefficient)) > ImplementationIdentity
                    || Math.Abs(plant.thrustPitchCoefficient / expectedCoeff - 1.0) > ImplementationIdentity)
                    bookkeeping = false;

                float routineCm = MavF15BaumannMach06Longitudinal.Evaluate(
                    (float)(s.alphaDeg * DegToRad), (float)s.stabilatorDeg, 0f).cm;
                if (plant.aeroCm != routineCm)
                    routineHasNoThrust = false;

                maxDiffX = Math.Max(maxDiffX, Math.Abs(plant.forceXOverWeight - wp3a.forceXOverWeight));
                maxDiffZ = Math.Max(maxDiffZ, Math.Abs(plant.forceZOverWeight - wp3a.forceZOverWeight));
                maxDiffM = Math.Max(maxDiffM, Math.Abs(plant.pitchOverQSc - wp3a.pitchOverQSc));
                minThrustCoeff = Math.Min(minThrustCoeff, plant.thrustPitchCoefficient);
                maxPitchFloor = Math.Max(maxPitchFloor, wp3a.floorPitch);
            }

            Record(n == 89 && bookkeeping,
                "M/qSc = routine CM + THRUST*(0.25/12)/(q.S.cbar), term by term, at all " + n + " printed symmetric states",
                report, ref passed, ref failed);
            Record(routineHasNoThrust,
                "the CM the plant uses is the transcribed routine's own, which carries no thrust term",
                report, ref passed, ref failed);
            Record(maxDiffX <= ImplementationIdentity && maxDiffZ <= ImplementationIdentity && maxDiffM <= ImplementationIdentity,
                "the plant's X, Z and M residuals equal the independent WP-3A evaluator's at every printed state "
                + "(max differences " + maxDiffX.ToString("E1") + ", " + maxDiffZ.ToString("E1") + ", "
                + maxDiffM.ToString("E1") + ")",
                report, ref passed, ref failed);
            report.Append("    a second thrust-line moment would shift M/qSc by at least ").Append(minThrustCoeff.ToString("E2"))
                  .Append(", ").Append((minThrustCoeff / Math.Max(maxPitchFloor, 1e-30)).ToString("F0"))
                  .AppendLine("x the largest pitch print floor; none is present");
        }

        // ---------------------------------------------------------------- [R4]

        private static void ValidatePrintedStateResiduals(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[R4] The printed symmetric states in the trim plant (static, no solve)");

            double[] maxAbs = new double[3], maxRatio = new double[3];
            int[] above = new int[3];
            bool lateralZero = true, finite = true;
            int n = 0;
            foreach (MavF15TableViiState s in MavF15TableViiTrimRecovery.SymmetricStates())
            {
                MavF15ResearchSymmetricTrimResidual r = MavF15AfitResearchTrimSolver.EvaluateSymmetricResidual(
                    s.trueVelocityFtPerSec, s.alphaDeg, s.stabilatorDeg, s.thetaDeg);
                MavF15EquilibriumResidual f = MavF15TableViiEquilibriumReproduction.Evaluate(s);
                n++;
                finite &= r.evaluated;
                if (r.forceYOverWeight != 0.0 || r.rollOverQSb != 0.0 || r.yawOverQSb != 0.0)
                    lateralZero = false;

                double[] res = { r.forceXOverWeight, r.forceZOverWeight, r.pitchOverQSc };
                double[] floor = { f.floorForceX, f.floorForceZ, f.floorPitch };
                for (int k = 0; k < 3; k++)
                {
                    maxAbs[k] = Math.Max(maxAbs[k], Math.Abs(res[k]));
                    if (floor[k] > 0.0)
                        maxRatio[k] = Math.Max(maxRatio[k], Math.Abs(res[k]) / floor[k]);
                    if (Math.Abs(res[k]) > floor[k])
                        above[k]++;
                }
            }

            string[] names = { "X/W", "Z/W", "M/qSc" };
            for (int k = 0; k < 3; k++)
            {
                report.Append("    ").Append(names[k]).Append(": max |res| ").Append(maxAbs[k].ToString("E2"))
                      .Append(", max res/print floor ").Append(maxRatio[k].ToString("F2"))
                      .Append(", states above floor ").Append(above[k]).AppendLine();
            }

            Record(n == 89 && finite,
                "all 89 printed symmetric states evaluate to finite residuals (reported above against print "
                + "precision; no threshold)",
                report, ref passed, ref failed);
            Record(lateralZero,
                "lateral residuals Y, L and N are exactly 0 at every symmetric state: the three-equation "
                + "reduction is the source's own equilibrium, not an approximation",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [R5]

        private static void ValidateRecovery(List<MavF15TrimRecoveryAttempt> attempts,
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[R5] Table VII round trip: published -> perturbed start -> solver -> recovered");

            int refused = 0, converged = 0, minIt = int.MaxValue, maxIt = 0;
            double sumIt = 0.0;
            bool withinEpsilon = true;
            float minSeedDistance = float.MaxValue;
            double[] maxErr = new double[3], sumErr = new double[3];
            double[] maxIndependent = new double[6];
            bool independentFinite = true;
            Dictionary<MavNewtonOutcome, int> outcomes = new Dictionary<MavNewtonOutcome, int>();

            for (int i = 0; i < attempts.Count; i++)
            {
                MavF15TrimRecoveryAttempt a = attempts[i];
                MavF15ResearchSymmetricTrimResult r = a.result;
                if (r.refused)
                {
                    refused++;
                    continue;
                }

                int count;
                outcomes[r.outcome] = outcomes.TryGetValue(r.outcome, out count) ? count + 1 : 1;
                minSeedDistance = Math.Min(minSeedDistance, Math.Abs(r.initialGuess.alphaDeg - (float)a.published.alphaDeg));
                minSeedDistance = Math.Min(minSeedDistance, Math.Abs(r.initialGuess.symmetricStabilatorDeg - (float)a.published.stabilatorDeg));
                minSeedDistance = Math.Min(minSeedDistance, Math.Abs(r.initialGuess.pitchAttitudeDeg - (float)a.published.thetaDeg));
                if (!r.converged)
                    continue;

                converged++;
                minIt = Math.Min(minIt, r.iterations);
                maxIt = Math.Max(maxIt, r.iterations);
                sumIt += r.iterations;
                if (r.drivenResidualNorm > MavF15AfitResearchTrimSolver.NumericalSolverTolerance)
                    withinEpsilon = false;

                double[] err = { a.alphaErrorDeg, a.stabilatorErrorDeg, a.thetaErrorDeg };
                for (int k = 0; k < 3; k++)
                {
                    maxErr[k] = Math.Max(maxErr[k], Math.Abs(err[k]));
                    sumErr[k] += Math.Abs(err[k]);
                }

                MavF15EquilibriumResidual ind = a.independentResidual;
                independentFinite &= ind.evaluated;
                double[] six = { ind.forceXOverWeight, ind.forceYOverWeight, ind.forceZOverWeight,
                                 ind.rollOverQSb, ind.pitchOverQSc, ind.yawOverQSb };
                for (int k = 0; k < 6; k++)
                    maxIndependent[k] = Math.Max(maxIndependent[k], Math.Abs(six[k]));
            }

            report.Append("    attempts ").Append(attempts.Count).Append(" (")
                  .Append(MavF15TableViiTrimRecovery.SymmetricStates().Count).Append(" states x ")
                  .Append(MavF15TableViiTrimRecovery.Perturbations.Length).Append(" perturbations); outcomes:");
            foreach (KeyValuePair<MavNewtonOutcome, int> kv in outcomes)
                report.Append(' ').Append(kv.Key).Append('=').Append(kv.Value);
            report.AppendLine();
            if (converged > 0)
            {
                report.Append("    iterations: min ").Append(minIt).Append(", mean ")
                      .Append((sumIt / converged).ToString("F2")).Append(", max ").Append(maxIt).AppendLine();
            }

            Record(attempts.Count == 356 && refused == 0,
                "all 356 attempts ran; none refused", report, ref passed, ref failed);
            Record(converged == attempts.Count && withinEpsilon,
                "all " + converged + " converged to the NUMERICAL epsilon "
                + MavF15AfitResearchTrimSolver.NumericalSolverTolerance.ToString("E0")
                + " (a termination criterion, not a source tolerance)",
                report, ref passed, ref failed);
            Record(minSeedDistance >= 0.5f - 1e-5f,
                "no start is the published answer: every start moves every unknown by at least "
                + minSeedDistance.ToString("F3") + " deg",
                report, ref passed, ref failed);

            // Recovered state vs print, per state (first perturbation), with the print-resolution floor.
            report.AppendLine("    recovered minus printed (raw). Floor = |dx/dV|*(V half unit) + x's own half unit:");
            report.AppendLine("     pt     V     alpha: printed  recovered   diff      floor  | stab: printed  recovered   diff      floor  | theta: printed recovered  diff      floor  | gamma");
            double[] maxRatio = new double[3];
            int[] aboveFloor = new int[3];
            int floorsMeasured = 0;
            for (int i = 0; i < attempts.Count; i += MavF15TableViiTrimRecovery.Perturbations.Length)
            {
                MavF15TrimRecoveryAttempt a = attempts[i];
                MavF15RecoveredStateFloor f = MavF15TableViiTrimRecovery.Floor(a.published, a.result);
                double[] err = { a.alphaErrorDeg, a.stabilatorErrorDeg, a.thetaErrorDeg };
                double[] fl = { f.alphaDeg, f.stabilatorDeg, f.thetaDeg };
                if (f.measured)
                {
                    floorsMeasured++;
                    for (int k = 0; k < 3; k++)
                    {
                        maxRatio[k] = Math.Max(maxRatio[k], Math.Abs(err[k]) / fl[k]);
                        if (Math.Abs(err[k]) > fl[k])
                            aboveFloor[k]++;
                    }
                }

                report.AppendFormat("    {0,3} {1,6:F1} | {2,10:F5} {3,10:F5} {4,9:E2} {5,8:E1} | {6,10:F5} {7,10:F5} {8,9:E2} {9,8:E1} | {10,7:F3} {11,9:F5} {12,9:E2} {13,8:E1} | {14,7:F3}\n",
                    a.published.part1Point, a.published.trueVelocityFtPerSec,
                    a.published.alphaDeg, a.result.alphaDeg, err[0], fl[0],
                    a.published.stabilatorDeg, a.result.symmetricStabilatorDeg, err[1], fl[1],
                    a.published.thetaDeg, a.result.pitchAttitudeDeg, err[2], fl[2],
                    a.result.FlightPathAngleDeg);
            }

            string[] names = { "alpha", "stabilator", "theta" };
            for (int k = 0; k < 3; k++)
            {
                report.Append("    ").Append(names[k]).Append(": max |recovered - printed| ")
                      .Append(maxErr[k].ToString("E2")).Append(" deg, mean ")
                      .Append((converged > 0 ? sumErr[k] / converged : 0.0).ToString("E2"))
                      .Append(" deg; max diff/floor ").Append(maxRatio[k].ToString("F2"))
                      .Append("; states above floor ").Append(aboveFloor[k]).Append(" of ").Append(floorsMeasured).AppendLine();
            }

            report.Append("    independent WP-3A evaluator at the recovered states, max |res|: X/W ")
                  .Append(maxIndependent[0].ToString("E1")).Append(", Y/W ").Append(maxIndependent[1].ToString("E1"))
                  .Append(", Z/W ").Append(maxIndependent[2].ToString("E1")).Append(", L/qSb ")
                  .Append(maxIndependent[3].ToString("E1")).Append(", M/qSc ").Append(maxIndependent[4].ToString("E1"))
                  .Append(", N/qSb ").Append(maxIndependent[5].ToString("E1")).AppendLine();
            Record(independentFinite && floorsMeasured == MavF15TableViiTrimRecovery.SymmetricStates().Count,
                "every recovered state was re-evaluated in all six axes by the independent WP-3A evaluator, "
                + "and every print floor was measured",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [R6]

        private static void ValidateNoAnswerRead(List<MavF15TrimRecoveryAttempt> attempts,
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[R6] The solver never reads the answer");

            ParameterInfo[] solve = typeof(MavF15AfitResearchTrimSolver).GetMethod("SolveSymmetric").GetParameters();
            ParameterInfo[] eval = typeof(MavF15AfitResearchTrimSolver).GetMethod("EvaluateSymmetricResidual").GetParameters();
            FieldInfo[] guessFields = typeof(MavF15ResearchSymmetricTrimGuess).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            bool signature = solve.Length == 3 && solve[0].ParameterType == typeof(string)
                             && solve[1].ParameterType == typeof(double)
                             && solve[2].ParameterType == typeof(MavF15ResearchSymmetricTrimGuess)
                             && eval.Length == 4 && guessFields.Length == 3;
            for (int i = 0; i < eval.Length; i++)
                signature &= eval[i].ParameterType == typeof(double);
            for (int i = 0; i < guessFields.Length; i++)
                signature &= guessFields[i].FieldType == typeof(float);

            Record(signature,
                "the solver takes a configuration id, V and a three-float start; the residual takes four doubles. "
                + "There is no parameter a published state could arrive through",
                report, ref passed, ref failed);

            string code = SolverCode();
            Record(code != null && code.IndexOf("TableVii", StringComparison.Ordinal) < 0
                   && code.IndexOf(".Validation", StringComparison.Ordinal) < 0,
                "the solver's code names neither Table VII nor the Validation namespace",
                report, ref passed, ref failed);

            // Start independence: every perturbation and a generic start that knows nothing of Table VII
            // reach the same root at each printed V.
            int stride = MavF15TableViiTrimRecovery.Perturbations.Length;
            bool sameRoot = true;
            float worst = 0f;
            for (int i = 0; i < attempts.Count; i += stride)
            {
                MavF15ResearchSymmetricTrimResult generic = MavF15AfitResearchTrimSolver.SolveSymmetric(
                    MavF15AfitResearchIdentity.ConfigurationId, attempts[i].published.trueVelocityFtPerSec,
                    MavF15TableViiTrimRecovery.GenericStart);
                if (!generic.converged)
                {
                    sameRoot = false;
                    continue;
                }

                for (int k = 0; k < stride; k++)
                {
                    MavF15ResearchSymmetricTrimResult r = attempts[i + k].result;
                    float d = Math.Max(Math.Abs(r.alphaDeg - generic.alphaDeg),
                        Math.Max(Math.Abs(r.symmetricStabilatorDeg - generic.symmetricStabilatorDeg),
                            Math.Abs(r.pitchAttitudeDeg - generic.pitchAttitudeDeg)));
                    worst = Math.Max(worst, d);
                    if (d > MavF15TableViiTrimRecovery.RootIdentityDeg)
                        sameRoot = false;
                }
            }

            Record(sameRoot,
                "all four table-derived starts and the generic start (15, -12, 15) reach the same root at every "
                + "printed V (largest spread " + worst.ToString("E1") + " deg; root identity "
                + MavF15TableViiTrimRecovery.RootIdentityDeg.ToString("E0") + " deg, numerical)",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [R7]

        private static void ValidateDeterminism(List<MavF15TrimRecoveryAttempt> first,
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[R7] Deterministic");

            List<MavF15TrimRecoveryAttempt> second = MavF15TableViiTrimRecovery.RunSymmetric();
            bool identical = first.Count == second.Count;
            for (int i = 0; identical && i < first.Count; i++)
            {
                MavF15ResearchSymmetricTrimResult a = first[i].result, b = second[i].result;
                identical = a.outcome == b.outcome && a.iterations == b.iterations
                            && a.alphaDeg == b.alphaDeg && a.symmetricStabilatorDeg == b.symmetricStabilatorDeg
                            && a.pitchAttitudeDeg == b.pitchAttitudeDeg && a.drivenResidualNorm == b.drivenResidualNorm;
            }

            Record(identical,
                "two full runs of the " + first.Count + " deterministic starts are bit-identical",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [R8]

        private static void ValidateBranches(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[R8] Multiple roots and branches (wide deterministic start grid, "
                              + (MavF15TableViiTrimRecovery.GridAlphaDeg.Length
                                 * MavF15TableViiTrimRecovery.GridStabilatorDeg.Length
                                 * MavF15TableViiTrimRecovery.GridThetaDeg.Length)
                              + " starts per V)");

            bool genuine = true, insideRange = true;
            int probes = 0;
            MavF15ResearchDemonstratedSurfaceRange stab = MavF15AfitResearchTrimSolver.StabilatorSearchRange();
            for (int v = 0; v < BranchProbeSpeedsFtPerSec.Length; v++)
            {
                MavF15TrimBranchProbe p = MavF15TableViiTrimRecovery.ProbeBranches(BranchProbeSpeedsFtPerSec[v]);
                probes++;
                report.Append("    V ").Append(p.trueAirspeedFtPerSec.ToString("F2")).Append(" ft/s: ")
                      .Append(p.converged).Append(" of ").Append(p.starts).Append(" starts converged, ")
                      .Append(p.roots.Count).Append(" distinct root(s); not converged: ")
                      .Append(p.stoppedAtDemonstratedRangeEdge).Append(" at the demonstrated-range edge, ")
                      .Append(p.stoppedAtSearchBound).Append(" at an alpha/theta search bound, ")
                      .Append(p.otherNonConverged).Append(" other")
                      .Append(p.otherNonConverged > 0
                          ? " (smallest residual norm " + p.smallestOtherNorm.ToString("E1") + ": stopped far from any equilibrium)"
                          : "")
                      .AppendLine();
                report.Append("      branch ambiguity: ")
                      .Append(p.roots.Count == 0
                          ? "none - no equilibrium inside the search box at this V"
                          : p.roots.Count == 1
                              ? "none - one root"
                              : "YES - " + p.roots.Count + " distinct roots, all preserved")
                      .AppendLine();

                for (int i = 0; i < p.roots.Count; i++)
                {
                    MavF15TrimRoot root = p.roots[i];
                    report.Append("      root alpha ").Append(root.alphaDeg.ToString("F5"))
                          .Append(" stab ").Append(root.stabilatorDeg.ToString("F5"))
                          .Append(" theta ").Append(root.thetaDeg.ToString("F5"))
                          .Append(" gamma ").Append((root.thetaDeg - root.alphaDeg).ToString("F3"))
                          .Append(" | norm ").Append(root.residualNorm.ToString("E1"))
                          .Append(" | from ").Append(root.startCount).Append(" starts, first (")
                          .Append(root.firstStart.alphaDeg).Append(", ").Append(root.firstStart.symmetricStabilatorDeg)
                          .Append(", ").Append(root.firstStart.pitchAttitudeDeg).Append(") | nearest published: ")
                          .Append(root.nearestPublishedPoint == 0
                              ? "none printed at this V (additional equilibrium, preserved)"
                              : "point " + root.nearestPublishedPoint + ", "
                                + root.distanceToNearestPublishedDeg.ToString("E2") + " deg away")
                          .AppendLine();

                    if (root.residualNorm > MavF15AfitResearchTrimSolver.NumericalSolverTolerance)
                        genuine = false;
                    if (root.stabilatorDeg < stab.minDeg || root.stabilatorDeg > stab.maxDeg)
                        insideRange = false;
                }
            }

            Record(probes == BranchProbeSpeedsFtPerSec.Length && genuine,
                "every preserved root is an equilibrium to the numerical epsilon; roots are kept, never steered "
                + "toward or filtered by Table VII",
                report, ref passed, ref failed);
            Record(insideRange,
                "no root uses a stabilator outside the research demonstrated range; where the equilibrium "
                + "would need one, the solver stops at the range edge and says so",
                report, ref passed, ref failed);

            // Table VII prints one symmetric state inside its turning section: point 165, the
            // pitchfork where the mirror-image turns leave the symmetric branch (phi 1e-6 deg,
            // beta 2e-9 deg as printed). Solved from the generic start, not from the table.
            foreach (MavF15TableViiState s in MavF15BaumannTableVii.PairedStates())
            {
                if (s.part1Point != PitchforkPoint)
                    continue;

                MavF15ResearchSymmetricTrimResult r = MavF15AfitResearchTrimSolver.SolveSymmetric(
                    MavF15AfitResearchIdentity.ConfigurationId, s.trueVelocityFtPerSec, MavF15TableViiTrimRecovery.GenericStart);
                MavF15RecoveredStateFloor f = MavF15TableViiTrimRecovery.Floor(s, r);
                report.Append("    point ").Append(PitchforkPoint).Append(" (V ").Append(s.trueVelocityFtPerSec.ToString("F1"))
                      .Append(" ft/s, printed phi ").Append(s.phiDeg.ToString("E1")).Append(" deg): ")
                      .Append(r.outcome).Append("; recovered minus printed alpha ")
                      .Append((r.alphaDeg - s.alphaDeg).ToString("E2")).Append(" (floor ").Append(f.alphaDeg.ToString("E1"))
                      .Append("), stab ").Append((r.symmetricStabilatorDeg - s.stabilatorDeg).ToString("E2"))
                      .Append(" (floor ").Append(f.stabilatorDeg.ToString("E1")).Append("), theta ")
                      .Append((r.pitchAttitudeDeg - s.thetaDeg).ToString("E2")).Append(" (floor ")
                      .Append(f.thetaDeg.ToString("E1")).AppendLine(")");
            }

            // Where symmetric equilibria exist inside the demonstrated range: continuation in V from a
            // generic start, 0.5 ft/s steps, each step starting from the previous root.
            double low = ContinuationEdge(342.9, -0.5), high = ContinuationEdge(342.9, +0.5);
            report.Append("    continuation from the generic start at 342.9 ft/s, 0.5 ft/s steps: symmetric equilibria "
                          + "inside the demonstrated stabilator range from V = ")
                  .Append(low.ToString("F1")).Append(" to ").Append(high.ToString("F1"))
                  .AppendLine(" ft/s (last converged step each way)");
        }

        private static double ContinuationEdge(double startV, double step)
        {
            MavF15ResearchSymmetricTrimGuess g = MavF15TableViiTrimRecovery.GenericStart;
            double last = double.NaN;
            for (double v = startV;
                 v >= MavF15SourceExercisedOperatingDomain.MinTabulatedTrueAirspeedFtPerSec
                 && v <= MavF15SourceExercisedOperatingDomain.MaxTabulatedTrueAirspeedFtPerSec;
                 v += step)
            {
                MavF15ResearchSymmetricTrimResult r = MavF15AfitResearchTrimSolver.SolveSymmetric(
                    MavF15AfitResearchIdentity.ConfigurationId, v, g);
                if (!r.converged)
                    break;
                last = v;
                g = new MavF15ResearchSymmetricTrimGuess(r.alphaDeg, r.symmetricStabilatorDeg, r.pitchAttitudeDeg);
            }

            return last;
        }

        // ---------------------------------------------------------------- [R9]

        private static void ValidateDemonstratedRangeNotHardStop(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[R9] The stabilator search box is the demonstrated range, not a hard stop");

            MavF15ResearchDemonstratedSurfaceRange box = MavF15AfitResearchTrimSolver.StabilatorSearchRange();
            MavF15ResearchDemonstratedSurfaceRange demonstrated =
                MavF15ResearchDemonstratedControlRange.AfitBaumannTabulatedEquilibria().symmetricStabilator;
            Record(box.declared && box.minDeg == demonstrated.minDeg && box.maxDeg == demonstrated.maxDeg
                   && box.minDeg == -25f && box.maxDeg == -5f && !box.IsPhysicalLimit,
                "search box = ResearchDemonstratedControlRange -25..-5 deg; IsPhysicalLimit is false",
                report, ref passed, ref failed);

            Type[] authority =
            {
                typeof(MavF15SurfaceLimits), typeof(MavF15SurfaceChannelLimits), typeof(MavF15PhysicalSurfaceHardStops),
                typeof(MavF15ActuatorRateLimits), typeof(MavControlSurfaceLimits), typeof(MavF15SurfaceState),
                typeof(MavF15ActualSurfaceState), typeof(MavF15RequestedSurfaceState)
            };
            bool clean = true;
            foreach (Type t in SolverTypes())
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

            GameObject host = new GameObject("MavF15R9Authority");
            host.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                MavF15ControlActuator actuator = host.AddComponent<MavF15ControlActuator>();
                MavF15AfitResearchFlightDynamicsProfile profile = host.AddComponent<MavF15AfitResearchFlightDynamicsProfile>();
                Record(clean
                       && !MavF15PhysicalSurfaceHardStops.FromActuatorLimits(actuator.limits).AnyDeclared
                       && !MavF15ActuatorRateLimits.FromActuatorLimits(actuator.limits).AnyDeclared
                       && profile.BuildProfile().controlSurfaceLimits.Equals(new MavControlSurfaceLimits()),
                    "after every trim run: no solver type holds or returns a limit or surface state; the F-15 "
                    + "actuator declares no hard stop and no rate; the research profile still has zero travel",
                    report, ref passed, ref failed);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        // ---------------------------------------------------------------- [R10]

        private static void ValidateExactPathCannotReach(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[R10] The exact NASA 836 path cannot reach the research trim");

            MavF15ResearchSymmetricTrimGuess g = MavF15TableViiTrimRecovery.GenericStart;
            MavF15ResearchSymmetricTrimResult exact = MavF15AfitResearchTrimSolver.SolveSymmetric(
                MavF15ReferenceData.TargetConfigurationId, 315.1, g);
            MavF15ResearchSymmetricTrimResult empty = MavF15AfitResearchTrimSolver.SolveSymmetric("", 315.1, g);
            Record(exact.refused && empty.refused && !exact.converged,
                "refused under the exact NASA 836 id and under an empty id: " + exact.refusalReason,
                report, ref passed, ref failed);

            MavF15ResearchSymmetricTrimResult slow = MavF15AfitResearchTrimSolver.SolveSymmetric(
                MavF15AfitResearchIdentity.ConfigurationId, 218.4, g);
            MavF15ResearchSymmetricTrimResult fast = MavF15AfitResearchTrimSolver.SolveSymmetric(
                MavF15AfitResearchIdentity.ConfigurationId, 699.8, g);
            MavF15ResearchSymmetricTrimResult nan = MavF15AfitResearchTrimSolver.SolveSymmetric(
                MavF15AfitResearchIdentity.ConfigurationId, double.NaN, g);
            Record(slow.refused && fast.refused && nan.refused,
                "refused outside the source-exercised 218.5-699.7 ft/s: no extrapolation beyond what the source ran",
                report, ref passed, ref failed);

            string root = ResolveFlightDynamicsRoot();
            string validation = root == null ? null : Path.GetFullPath(Path.Combine(root, "Validation")) + Path.DirectorySeparatorChar;
            string solverFile = root == null ? null : Path.GetFullPath(Path.Combine(root, Path.Combine("F15", "MavF15AfitResearchTrimSolver.cs")));
            string offender = null;
            int scanned = 0;
            if (root != null)
            {
                foreach (string f in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    string full = Path.GetFullPath(f);
                    if (full.StartsWith(validation, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(full, solverFile, StringComparison.OrdinalIgnoreCase))
                        continue;
                    scanned++;
                    string text = File.ReadAllText(full);
                    if (text.IndexOf("MavF15AfitResearchTrimSolver", StringComparison.Ordinal) >= 0
                        || text.IndexOf("MavF15AfitResearchSourceEnvironment", StringComparison.Ordinal) >= 0)
                        offender = Path.GetFileName(full);
                }
            }

            Record(root != null && scanned > 0 && offender == null,
                offender == null
                    ? "no other runtime or editor source (" + scanned + " scanned, exact 836 path and MavSixDoFBody included) "
                      + "names the research trim or its environment"
                    : "VIOLATION: " + offender + " names the research trim",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [R11]

        private static void ValidateNoF100(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[R11] The MavF100 layer is not used");

            string code = SolverCode();
            Record(code != null && code.IndexOf("MavF100", StringComparison.Ordinal) < 0
                   && code.IndexOf("MavThrustDeck", StringComparison.Ordinal) < 0
                   && code.IndexOf("MavPropulsionModelBase", StringComparison.Ordinal) < 0
                   && code.IndexOf("MavPropulsiveLoads", StringComparison.Ordinal) < 0
                   && code.IndexOf("throttle", StringComparison.OrdinalIgnoreCase) < 0,
                "the solver's code names no MavF100 type, thrust deck, propulsion model, propulsive loads or throttle",
                report, ref passed, ref failed);

            string root = ResolveFlightDynamicsRoot();
            string offender = null;
            int scanned = 0;
            if (root != null)
            {
                foreach (string f in Directory.GetFiles(root, "MavF100*.cs", SearchOption.AllDirectories))
                {
                    scanned++;
                    string text = File.ReadAllText(f);
                    if (text.IndexOf("MavF15AfitResearchTrimSolver", StringComparison.Ordinal) >= 0
                        || text.IndexOf("MavF15AfitResearchSourceEnvironment", StringComparison.Ordinal) >= 0)
                        offender = Path.GetFileName(f);
                }
            }

            Record(scanned > 0 && offender == null,
                "none of " + scanned + " MavF100 sources names the research trim or its environment",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [R12]

        private static void ValidateCfx2Unchanged(MavAeroCoefficients[] before,
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[R12] The CFX2 coefficient is unchanged by trim");

            // Probed at 40 deg, above the 20-30 deg blend, where CFX is CFX2 alone (same probe as
            // the transcription suite's F15-AUDIT-010 check).
            double a40 = 40.0 * DegToRad;
            MavAeroCoefficients c = MavF15BaumannMach06Longitudinal.Evaluate((float)a40, 0f, 0f);
            double cfx = -((c.cx * Math.Cos(a40)) + (c.cz * Math.Sin(a40)));
            double cfxBase = 0.0267297 - (0.10646919 * a40) + (5.39836337 * a40 * a40)
                             - (5.0086893 * Math.Pow(a40, 3)) + (1.34148193 * Math.Pow(a40, 4));
            double toAppendixC = Math.Abs(cfx - (cfxBase + 0.09833517));
            double toBaumann = Math.Abs(cfx - (cfxBase + 0.09833617));
            Record(toAppendixC < toBaumann,
                "after every trim run CFX at 40 deg still sits on Davison App. C's 0.09833517, not Baumann's "
                + "0.09833617 (distances " + toAppendixC.ToString("E2") + " vs " + toBaumann.ToString("E2")
                + "); the recorded version difference is kept, not resolved by trim",
                report, ref passed, ref failed);

            MavAeroCoefficients[] after = SampleCoefficients();
            bool same = before.Length == after.Length;
            for (int i = 0; same && i < before.Length; i++)
            {
                same = before[i].cx == after[i].cx && before[i].cz == after[i].cz && before[i].cm == after[i].cm
                       && before[i].cy == after[i].cy && before[i].cl == after[i].cl && before[i].cn == after[i].cn;
            }

            bool noMutableStatics = NoMutableStatics(typeof(MavF15BaumannMach06Longitudinal))
                                    && NoMutableStatics(typeof(MavF15BaumannMach06LateralDirectional));
            foreach (Type t in SolverTypes())
                noMutableStatics &= NoMutableStatics(t);

            Record(same && noMutableStatics,
                "the coefficient routine returns bit-identical values before and after all trim runs, and neither "
                + "it nor any solver type has a mutable static a trim could tune",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [R13]

        private static void ValidateSharedAtmosphere(MavAtmosphereSample[] before,
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[R13] Shared atmosphere unchanged; the runtime fixed-density gap is recorded");

            MavAtmosphereSample[] after = SampleAtmosphere();
            bool same = before.Length == after.Length;
            for (int i = 0; same && i < before.Length; i++)
            {
                same = before[i].densityKgM3 == after[i].densityKgM3 && before[i].pressurePa == after[i].pressurePa
                       && before[i].temperatureK == after[i].temperatureK && before[i].speedOfSoundMps == after[i].speedOfSoundMps;
            }

            Record(same,
                "MavAtmosphereModel returns bit-identical samples at 0, 3,000, 6,096 and 11,000 m before and after every trim run",
                report, ref passed, ref failed);

            MavPropulsiveLoads sourceThrust = new MavPropulsiveLoads();
            sourceThrust.forceAeroBodyN = new Vector3(MavF15AfitResearchThrustSource.TotalThrustN, 0f, 0f);
            sourceThrust.momentAeroBodyNm = new Vector3(0f, MavF15AfitResearchThrustSource.ThrustLinePitchingMomentNm, 0f);
            string genericReason;
            Record(!MavSteadyFlightTrimSolver.IsAxialThroughCentreOfGravity(sourceThrust, out genericReason),
                "the generic MavSteadyFlightTrimSolver was not adapted: it still refuses the source's thrust-line moment ("
                + genericReason + ")",
                report, ref passed, ref failed);

            // Section 12 of the brief: the runtime body computes q from ISA density at its altitude.
            // Recorded, not fixed here.
            double vFt = 315.1;
            float vMps = (float)(vFt * FtToM);
            MavAtmosphereSample atm = MavAtmosphereModel.Sample(MavF15CoefficientFitCondition.PressureAltitudeM);
            MavFlightState body = MavSixDoFBody.BuildFlightState(
                new Vector3(0f, MavF15CoefficientFitCondition.PressureAltitudeM, 0f), new Vector3(0f, 0f, vMps),
                new Vector3(0f, 0f, vMps), Vector3.zero, Vector3.forward, Vector3.up, Vector3.right, atm);
            double sourceQPa = 0.5 * MavF15AfitResearchSourceEnvironment.DensitySlugPerFt3 * SlugPerFt3ToKgPerM3
                               * (double)vMps * vMps;
            double relative = (body.dynamicPressurePa - sourceQPa) / sourceQPa;
            report.Append("    MavSixDoFBody q at 6,096 m, ").Append(vFt.ToString("F1")).Append(" ft/s: ")
                  .Append(body.dynamicPressurePa.ToString("F2")).Append(" Pa vs the source's ")
                  .Append(sourceQPa.ToString("F2")).Append(" Pa (relative ").Append(relative.ToString("E2")).AppendLine(")");
            Record(relative != 0.0,
                "RECORDED DISCREPANCY: the runtime body's q comes from ISA density at its altitude, so runtime "
                + "SourceReproduction does NOT reproduce the source's fixed-density q; the trim plant does",
                report, ref passed, ref failed);

            try
            {
                double sourceG = MavF15AfitResearchSourceEnvironment.GravityFtPerSec2 * FtToM;
                report.Append("    runtime gravity |Physics.gravity| = ").Append(Physics.gravity.magnitude.ToString("F5"))
                      .Append(" m/s^2 vs the source's G = ").Append(sourceG.ToString("F5"))
                      .Append(" m/s^2 (relative ").Append(((Physics.gravity.magnitude - sourceG) / sourceG).ToString("E2"))
                      .AppendLine("); recorded for the runtime environment design");
            }
            catch (Exception)
            {
                report.AppendLine("    runtime gravity not readable outside Unity; not reported");
            }
        }

        // ---------------------------------------------------------------- helpers

        private static Type[] SolverTypes()
        {
            return new[]
            {
                typeof(MavF15AfitResearchTrimSolver), typeof(MavF15AfitResearchSourceEnvironment),
                typeof(MavF15ResearchSymmetricTrimResult), typeof(MavF15ResearchSymmetricTrimResidual),
                typeof(MavF15ResearchSymmetricTrimGuess)
            };
        }

        private static string ForbiddenMemberName(string[] words)
        {
            foreach (Type t in SolverTypes())
            {
                foreach (MemberInfo m in t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static
                                                      | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    string n = m.Name.ToLowerInvariant();
                    for (int w = 0; w < words.Length; w++)
                    {
                        if (n.IndexOf(words[w], StringComparison.Ordinal) >= 0)
                            return t.Name + "." + m.Name;
                    }
                }
            }

            return null;
        }

        private static bool NoMutableStatics(Type t)
        {
            foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (!f.IsLiteral && !f.IsInitOnly)
                    return false;
            }

            return true;
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
            float[] alphaDeg = { 5f, 15f, 25f, 40f, 60f };
            float[] stabDeg = { -20f, -10f, -5f };
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

            StringBuilder code = new StringBuilder(16384);
            foreach (string line in File.ReadAllLines(path))
            {
                int comment = line.IndexOf("//", StringComparison.Ordinal);
                code.AppendLine(comment >= 0 ? line.Substring(0, comment) : line);
            }

            return code.ToString();
        }

        private static double MinV(List<MavF15TrimRecoveryAttempt> attempts)
        {
            double v = double.MaxValue;
            for (int i = 0; i < attempts.Count; i++)
                v = Math.Min(v, attempts[i].published.trueVelocityFtPerSec);
            return v;
        }

        private static double MaxV(List<MavF15TrimRecoveryAttempt> attempts)
        {
            double v = 0.0;
            for (int i = 0; i < attempts.Count; i++)
                v = Math.Max(v, attempts[i].published.trueVelocityFtPerSec);
            return v;
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
