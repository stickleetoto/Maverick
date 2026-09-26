using System;
using System.Collections.Generic;
using MaverickFresh.FlightDynamics.F15;

namespace MaverickFresh.FlightDynamics.Validation
{
    /// <summary>How an assembled Table VII turning-section state is treated by WP-3C.</summary>
    public enum MavF15TurningStateClass
    {
        /// <summary>A genuinely non-symmetric (banked, helical) equilibrium: recovered with phi fixed.</summary>
        NonSymmetricTurning = 0,

        /// <summary>
        /// PITCHFORK / SYMMETRIC BRANCH POINT: the state where the printed bank angle changes sign
        /// along the branch. Effectively symmetric; validated with the WP-3B symmetric solver, never
        /// fed to the turning solve.
        /// </summary>
        PitchforkSymmetricBranchPoint = 1
    }

    /// <summary>One deterministic perturbation of all eight solved unknowns (phi is never perturbed).</summary>
    public struct MavF15TurningStartPerturbation
    {
        public float[] delta;

        public MavF15TurningStartPerturbation(params float[] delta)
        {
            this.delta = delta;
        }
    }

    /// <summary>published -> perturbed start -> turning solver (phi fixed) -> recovered.</summary>
    public struct MavF15TurningRecoveryAttempt
    {
        public MavF15TableViiState published;
        public int perturbationIndex;
        public MavF15ResearchTurningTrimResult result;

        /// <summary>Recovered minus printed, in unknown order (alpha, beta, p, q, r, theta, V, stabilator).</summary>
        public double[] error;

        /// <summary>The independent WP-3A evaluator at the recovered state (printed phi). All six axes and both kinematics.</summary>
        public MavF15EquilibriumResidual independentResidual;
    }

    /// <summary>
    /// First-order effect of print resolution on a RECOVERED turning state. Context, not a tolerance.
    /// The solver takes phi as printed (4 significant digits) and returns the other eight, so a model
    /// identical to the source could still differ from print by
    ///   |dx/dphi| * (half a unit in phi's last digit) + (half a unit in x's own last digit).
    /// dx/dphi is measured with the solver itself by central difference over phi's half unit.
    /// </summary>
    public struct MavF15TurningStateFloor
    {
        public bool measured;
        public double halfUnitPhiDeg;
        public double[] floor;
    }

    /// <summary>A distinct turning root at one fixed phi, with the starts that reached it.</summary>
    public struct MavF15TurningRoot
    {
        public MavF15ResearchTurningTrimGuess state;
        public float residualNorm;
        public double headingRateRadSec;
        public int startCount;
        public MavF15ResearchTurningTrimGuess firstStart;

        /// <summary>The published state printed at this phi, or 0 when none is.</summary>
        public int publishedPoint;

        /// <summary>Largest |root - published| over the unknowns, each divided by its root-identity epsilon.</summary>
        public double distanceToPublishedInIdentityUnits;
    }

    public struct MavF15TurningBranchProbe
    {
        public double phiDeg;
        public int starts;
        public int converged;
        public int atSourceSemanticBound;
        public int atNumericalSearchBound;
        public int otherNonConverged;
        public float smallestOtherNorm;
        public List<MavF15TurningRoot> roots;

        /// <summary>The lowest-norm "other" non-converged results (up to three), kept for classification.</summary>
        public List<MavF15ResearchTurningTrimResult> closestNonConverged;
    }

    /// <summary>One solve of the near-pitchfork conditioning probe.</summary>
    public struct MavF15PitchforkProbeStep
    {
        public double phiDeg;
        public int seedPoint;
        public MavF15ResearchTurningTrimResult result;
    }

    /// <summary>
    /// WP-3C round trip for Baumann Table VII's turning states: published state -> fix its printed phi
    /// -> perturb all eight unknowns -> research turning solver -> recovered state.
    ///
    /// The published row is used only to build the start, and afterwards for error reporting,
    /// print-resolution analysis and branch labelling. It never enters the residual: the solver lives
    /// in F15/, cannot name Table VII, and takes only an id, phi and a start.
    ///
    /// Pairing is WP-3A's (MavF15BaumannTableVii.PairedStates): nothing is re-transcribed or
    /// rearranged, and the states with an illegible field (first-half 175, and 194 through
    /// second-half 196) stay unavailable.
    /// </summary>
    public static class MavF15TableViiTurningRecovery
    {
        public static readonly string[] UnknownNames = { "alpha", "beta", "p", "q", "r", "theta", "V", "stabilator" };
        public static readonly string[] UnknownUnits = { "deg", "deg", "rad/s", "rad/s", "rad/s", "deg", "ft/s", "deg" };

        /// <summary>Significant digits Table VII prints each unknown with (stabilator and alpha 7, the rest 4).</summary>
        public static readonly int[] PrintedSignificantDigits = { 7, 4, 4, 4, 4, 4, 4, 7 };

        /// <summary>
        /// NUMERICAL root-identity epsilons, per unknown in its own unit: two converged solutions within
        /// all of them are the same root. Far above the spread between starts that reach the same root,
        /// far below the spacing of distinct equilibria. Nothing to do with agreement with Table VII.
        /// </summary>
        public static readonly double[] RootIdentity = { 1e-3, 1e-3, 1e-5, 1e-5, 1e-5, 1e-3, 1e-2, 1e-3 };

        /// <summary>
        /// Four deterministic perturbations, mixed signs, every unknown moved: up to 1 deg alpha, 0.3 deg
        /// beta, 0.003 rad/s p, 0.02 rad/s q and r, 3 deg theta, 20 ft/s V and 0.5 deg stabilator. The
        /// stabilator start stays inside the demonstrated range for every turning point (-6.49..-5.72 deg)
        /// and V inside the source-exercised span (377.4..662.0 ft/s).
        /// </summary>
        public static readonly MavF15TurningStartPerturbation[] Perturbations =
        {
            new MavF15TurningStartPerturbation(+1.0f, +0.2f, +0.002f, -0.01f, +0.01f, -2f, +10f, +0.3f),
            new MavF15TurningStartPerturbation(-1.0f, -0.2f, -0.002f, +0.01f, -0.01f, +2f, -10f, -0.3f),
            new MavF15TurningStartPerturbation(+0.5f, -0.3f, +0.003f, +0.02f, -0.02f, +3f, -20f, -0.5f),
            new MavF15TurningStartPerturbation(-0.5f, +0.3f, -0.003f, -0.02f, +0.02f, -3f, +20f, +0.5f)
        };

        /// <summary>Wide deterministic start grid for the multiple-root probe: 4 x 4 x 3 x 3 x 2 = 288 starts per phi.</summary>
        public static readonly float[] GridAlphaDeg = { 4f, 10f, 20f, 40f };
        public static readonly float[] GridSpeedFtPerSec = { 250f, 400f, 550f, 690f };
        public static readonly float[] GridStabilatorDeg = { -20f, -10f, -6f };
        public static readonly float[] GridThetaDeg = { -30f, 0f, 30f };
        public static readonly float[] GridYawRateRadSec = { -0.05f, 0.05f };
        public const float GridPitchRateRadSec = 0.05f;

        /// <summary>Printed points whose phi the multiple-root probe fixes: both sides, 8 to 65 deg of bank.</summary>
        public static readonly int[] BranchProbePoints = { 117, 129, 147, 159, 171, 178, 190, 199 };

        /// <summary>Bank angles for the near-pitchfork probe, magnitude, deg. Applied with both signs.</summary>
        public static readonly double[] PitchforkProbePhiDeg = { 1.0, 0.5, 0.2, 0.1, 0.05, 0.02, 0.01, 0.005, 0.002, 0.001, 1e-4, 1e-5, 1e-6 };

        // ------------------------------------------------------------------ dataset

        public static List<MavF15TableViiState> TurningSectionStates()
        {
            List<MavF15TableViiState> states = new List<MavF15TableViiState>(81);
            foreach (MavF15TableViiState s in MavF15BaumannTableVii.PairedStates())
            {
                if (s.pairing == MavF15TableViiPairing.TurningDisplacedColumns)
                    states.Add(s);
            }

            return states;
        }

        /// <summary>
        /// The pitchfork, found from the data rather than named: the one place along the printed branch
        /// where the bank angle changes sign, taking the state of the bracketing pair with the smaller |phi|.
        /// Returns 0 if the branch never changes sign.
        /// </summary>
        public static int PitchforkPoint()
        {
            List<MavF15TableViiState> states = TurningSectionStates();
            for (int i = 1; i < states.Count; i++)
            {
                if (Math.Sign(states[i - 1].phiDeg) * Math.Sign(states[i].phiDeg) < 0)
                {
                    return Math.Abs(states[i - 1].phiDeg) < Math.Abs(states[i].phiDeg)
                        ? states[i - 1].part1Point
                        : states[i].part1Point;
                }
            }

            return 0;
        }

        public static int SignChangeCount()
        {
            List<MavF15TableViiState> states = TurningSectionStates();
            int changes = 0;
            for (int i = 1; i < states.Count; i++)
            {
                if (Math.Sign(states[i - 1].phiDeg) * Math.Sign(states[i].phiDeg) < 0)
                    changes++;
            }

            return changes;
        }

        public static MavF15TurningStateClass Classify(MavF15TableViiState s)
        {
            return s.part1Point == PitchforkPoint()
                ? MavF15TurningStateClass.PitchforkSymmetricBranchPoint
                : MavF15TurningStateClass.NonSymmetricTurning;
        }

        public static List<MavF15TableViiState> NonSymmetricStates()
        {
            int fork = PitchforkPoint();
            List<MavF15TableViiState> states = new List<MavF15TableViiState>(80);
            foreach (MavF15TableViiState s in TurningSectionStates())
            {
                if (s.part1Point != fork)
                    states.Add(s);
            }

            return states;
        }

        public static bool TryGetState(int part1Point, out MavF15TableViiState state)
        {
            foreach (MavF15TableViiState s in TurningSectionStates())
            {
                if (s.part1Point == part1Point)
                {
                    state = s;
                    return true;
                }
            }

            state = default(MavF15TableViiState);
            return false;
        }

        /// <summary>The printed values of the eight unknowns, in solve order.</summary>
        public static double[] Published(MavF15TableViiState s)
        {
            return new[]
            {
                s.alphaDeg, s.betaDeg, s.pRadSec, s.qRadSec, s.rRadSec, s.thetaDeg, s.trueVelocityFtPerSec,
                s.stabilatorDeg
            };
        }

        public static double[] Values(MavF15ResearchTurningTrimGuess x)
        {
            return new double[]
            {
                x.alphaDeg, x.betaDeg, x.pRadSec, x.qRadSec, x.rRadSec, x.thetaDeg, x.trueAirspeedFtPerSec,
                x.symmetricStabilatorDeg
            };
        }

        public static MavF15ResearchTurningTrimGuess Guess(double[] x)
        {
            return new MavF15ResearchTurningTrimGuess
            {
                alphaDeg = (float)x[0],
                betaDeg = (float)x[1],
                pRadSec = (float)x[2],
                qRadSec = (float)x[3],
                rRadSec = (float)x[4],
                thetaDeg = (float)x[5],
                trueAirspeedFtPerSec = (float)x[6],
                symmetricStabilatorDeg = (float)x[7]
            };
        }

        // ------------------------------------------------------------------ round trip

        public static MavF15ResearchTurningTrimGuess StartFor(MavF15TableViiState s, MavF15TurningStartPerturbation p)
        {
            double[] x = Published(s);
            for (int i = 0; i < 8; i++)
                x[i] = (float)x[i] + p.delta[i];
            return Guess(x);
        }

        public static List<MavF15TurningRecoveryAttempt> RunTurning()
        {
            List<MavF15TableViiState> states = NonSymmetricStates();
            List<MavF15TurningRecoveryAttempt> attempts =
                new List<MavF15TurningRecoveryAttempt>(states.Count * Perturbations.Length);
            for (int i = 0; i < states.Count; i++)
            {
                for (int k = 0; k < Perturbations.Length; k++)
                    attempts.Add(Attempt(states[i], k));
            }

            return attempts;
        }

        public static MavF15TurningRecoveryAttempt Attempt(MavF15TableViiState s, int perturbationIndex)
        {
            MavF15ResearchTurningTrimResult r = MavF15AfitResearchTrimSolver.SolveTurning(
                MavF15AfitResearchIdentity.ConfigurationId, s.phiDeg, StartFor(s, Perturbations[perturbationIndex]));

            MavF15TurningRecoveryAttempt a = new MavF15TurningRecoveryAttempt
            {
                published = s,
                perturbationIndex = perturbationIndex,
                result = r,
                error = new double[8]
            };

            if (r.refused)
                return a;

            double[] printed = Published(s);
            double[] recovered = Values(r.solution);
            for (int i = 0; i < 8; i++)
                a.error[i] = recovered[i] - printed[i];

            MavF15TableViiState at = s;
            at.alphaDeg = recovered[0];
            at.betaDeg = recovered[1];
            at.pRadSec = recovered[2];
            at.qRadSec = recovered[3];
            at.rRadSec = recovered[4];
            at.thetaDeg = recovered[5];
            at.trueVelocityFtPerSec = recovered[6];
            at.stabilatorDeg = recovered[7];
            a.independentResidual = MavF15TableViiEquilibriumReproduction.Evaluate(at);
            return a;
        }

        /// <summary>See <see cref="MavF15TurningStateFloor"/>.</summary>
        public static MavF15TurningStateFloor Floor(MavF15TableViiState s, MavF15ResearchTurningTrimResult recovered)
        {
            MavF15TurningStateFloor f = new MavF15TurningStateFloor { floor = new double[8] };
            if (!recovered.converged)
                return f;

            double h = MavF15TableViiEquilibriumReproduction.HalfUnitInLastDigit(s.phiDeg, 4);
            MavF15ResearchTurningTrimResult up = MavF15AfitResearchTrimSolver.SolveTurning(
                MavF15AfitResearchIdentity.ConfigurationId, s.phiDeg + h, recovered.solution);
            MavF15ResearchTurningTrimResult down = MavF15AfitResearchTrimSolver.SolveTurning(
                MavF15AfitResearchIdentity.ConfigurationId, s.phiDeg - h, recovered.solution);
            if (!up.converged || !down.converged)
                return f;

            double[] printed = Published(s);
            double[] a = Values(up.solution);
            double[] b = Values(down.solution);
            f.measured = true;
            f.halfUnitPhiDeg = h;
            for (int i = 0; i < 8; i++)
            {
                double ownHalfUnit = i == 6
                    ? 1000.0 * MavF15TableViiEquilibriumReproduction.HalfUnitInLastDigit(printed[i] / 1000.0, 4)
                    : MavF15TableViiEquilibriumReproduction.HalfUnitInLastDigit(printed[i], PrintedSignificantDigits[i]);
                f.floor[i] = 0.5 * Math.Abs(a[i] - b[i]) + ownHalfUnit;
            }

            return f;
        }

        /// <summary>
        /// NUMERICAL termination uncertainty of a converged root, per unknown: |J^-1 r|, the step one
        /// more Newton iteration would take from where the solver stopped. The solver stops once every
        /// residual is inside NumericalSolverTolerance; this says how far that can leave each unknown.
        /// Kept apart from the print-resolution floor, which is about the source, not the solver.
        ///
        /// J is a central-difference Jacobian with the solver's own steps, solved with the shared
        /// MavDampedNewtonSolver.SolveLinearSystemInPlace. Returns null when J is singular, which is
        /// itself the finding (the pitchfork).
        /// </summary>
        public static double[] NumericalUncertainty(double phiDeg, MavF15ResearchTurningTrimGuess root)
        {
            float[] x = { root.alphaDeg, root.betaDeg, root.pRadSec, root.qRadSec, root.rRadSec, root.thetaDeg,
                          root.trueAirspeedFtPerSec, root.symmetricStabilatorDeg };
            float[] h =
            {
                MavF15AfitResearchTrimSolver.FiniteDifferenceStepDeg, MavF15AfitResearchTrimSolver.FiniteDifferenceStepBetaDeg,
                MavF15AfitResearchTrimSolver.FiniteDifferenceStepRateRadSec, MavF15AfitResearchTrimSolver.FiniteDifferenceStepRateRadSec,
                MavF15AfitResearchTrimSolver.FiniteDifferenceStepRateRadSec, MavF15AfitResearchTrimSolver.FiniteDifferenceStepDeg,
                MavF15AfitResearchTrimSolver.FiniteDifferenceStepSpeedFtPerSec, MavF15AfitResearchTrimSolver.FiniteDifferenceStepDeg
            };

            float[] r0 = ResidualVector(phiDeg, x);
            if (r0 == null)
                return null;

            float[] jacobian = new float[64];
            for (int column = 0; column < 8; column++)
            {
                float[] plus = (float[])x.Clone();
                float[] minus = (float[])x.Clone();
                plus[column] += h[column];
                minus[column] -= h[column];
                float[] rp = ResidualVector(phiDeg, plus);
                float[] rm = ResidualVector(phiDeg, minus);
                if (rp == null || rm == null)
                    return null;
                for (int row = 0; row < 8; row++)
                    jacobian[row * 8 + column] = (rp[row] - rm[row]) / (plus[column] - minus[column]);
            }

            float[] step = new float[8];
            for (int i = 0; i < 8; i++)
                step[i] = -r0[i];
            if (!MavDampedNewtonSolver.SolveLinearSystemInPlace(jacobian, step, 8))
                return null;

            double[] u = new double[8];
            for (int i = 0; i < 8; i++)
                u[i] = Math.Abs(step[i]);
            return u;
        }

        private static float[] ResidualVector(double phiDeg, float[] x)
        {
            MavF15ResearchTurningTrimResidual e = MavF15AfitResearchTrimSolver.EvaluateTurningResidual(
                phiDeg, x[0], x[1], x[2], x[3], x[4], x[5], x[6], x[7]);
            if (!e.evaluated)
                return null;

            return new[]
            {
                (float)e.forceXOverWeight, (float)e.forceYOverWeight, (float)e.forceZOverWeight, (float)e.rollOverQSb,
                (float)e.pitchOverQSc, (float)e.yawOverQSb, (float)e.thetaDotRadSec, (float)e.phiDotRadSec
            };
        }

        // ------------------------------------------------------------------ mirror

        /// <summary>
        /// The mirror image under the model's left-right symmetry: alpha, q, theta, V and the stabilator
        /// kept; beta, p and r negated (phi is negated by the caller).
        /// </summary>
        public static MavF15ResearchTurningTrimGuess Mirror(MavF15ResearchTurningTrimGuess x)
        {
            x.betaDeg = -x.betaDeg;
            x.pRadSec = -x.pRadSec;
            x.rRadSec = -x.rRadSec;
            return x;
        }

        /// <summary>
        /// Solve at -phi from the start of the +phi attempt, either mirrored or as it is. The unmirrored
        /// start is the harder test: the solver must travel to the other side on its own.
        /// </summary>
        public static MavF15ResearchTurningTrimResult SolveMirror(MavF15TurningRecoveryAttempt a, bool mirrorStart)
        {
            MavF15ResearchTurningTrimGuess start = mirrorStart ? Mirror(a.result.initialGuess) : a.result.initialGuess;
            return MavF15AfitResearchTrimSolver.SolveTurning(
                MavF15AfitResearchIdentity.ConfigurationId, -a.published.phiDeg, start);
        }

        // ------------------------------------------------------------------ multiple roots

        public static MavF15TurningBranchProbe ProbeBranches(double phiDeg, int publishedPoint)
        {
            MavF15TurningBranchProbe probe = new MavF15TurningBranchProbe
            {
                phiDeg = phiDeg,
                smallestOtherNorm = float.PositiveInfinity,
                roots = new List<MavF15TurningRoot>(),
                closestNonConverged = new List<MavF15ResearchTurningTrimResult>()
            };

            MavF15TableViiState published;
            bool hasPublished = TryGetState(publishedPoint, out published) && published.phiDeg == phiDeg;

            for (int a = 0; a < GridAlphaDeg.Length; a++)
            for (int v = 0; v < GridSpeedFtPerSec.Length; v++)
            for (int e = 0; e < GridStabilatorDeg.Length; e++)
            for (int t = 0; t < GridThetaDeg.Length; t++)
            for (int y = 0; y < GridYawRateRadSec.Length; y++)
            {
                MavF15ResearchTurningTrimGuess start = new MavF15ResearchTurningTrimGuess
                {
                    alphaDeg = GridAlphaDeg[a],
                    betaDeg = 0f,
                    pRadSec = 0f,
                    qRadSec = GridPitchRateRadSec,
                    rRadSec = GridYawRateRadSec[y],
                    thetaDeg = GridThetaDeg[t],
                    trueAirspeedFtPerSec = GridSpeedFtPerSec[v],
                    symmetricStabilatorDeg = GridStabilatorDeg[e]
                };
                MavF15ResearchTurningTrimResult r = MavF15AfitResearchTrimSolver.SolveTurning(
                    MavF15AfitResearchIdentity.ConfigurationId, phiDeg, start);
                probe.starts++;

                if (!r.converged)
                {
                    if (r.boundReached != null && r.boundReachedKind == MavF15ResearchSearchBoundKind.SourceSemantic)
                        probe.atSourceSemanticBound++;
                    else if (r.boundReached != null)
                        probe.atNumericalSearchBound++;
                    else
                    {
                        probe.otherNonConverged++;
                        probe.smallestOtherNorm = Math.Min(probe.smallestOtherNorm, r.drivenResidualNorm);
                        KeepClosest(probe.closestNonConverged, r);
                    }
                    continue;
                }

                probe.converged++;
                double[] x = Values(r.solution);
                int match = -1;
                for (int i = 0; i < probe.roots.Count && match < 0; i++)
                {
                    if (SameRoot(Values(probe.roots[i].state), x))
                        match = i;
                }

                if (match >= 0)
                {
                    MavF15TurningRoot known = probe.roots[match];
                    known.startCount++;
                    probe.roots[match] = known;
                    continue;
                }

                MavF15TurningRoot root = new MavF15TurningRoot
                {
                    state = r.solution,
                    residualNorm = r.drivenResidualNorm,
                    headingRateRadSec = r.residual.headingRateRadSec,
                    startCount = 1,
                    firstStart = start,
                    publishedPoint = hasPublished ? publishedPoint : 0,
                    distanceToPublishedInIdentityUnits = double.PositiveInfinity
                };

                if (hasPublished)
                {
                    double[] printed = Published(published);
                    double d = 0.0;
                    for (int i = 0; i < 8; i++)
                        d = Math.Max(d, Math.Abs(x[i] - printed[i]) / RootIdentity[i]);
                    root.distanceToPublishedInIdentityUnits = d;
                }

                probe.roots.Add(root);
            }

            return probe;
        }

        private static void KeepClosest(List<MavF15ResearchTurningTrimResult> kept, MavF15ResearchTurningTrimResult r)
        {
            kept.Add(r);
            kept.Sort((x, y) => x.drivenResidualNorm.CompareTo(y.drivenResidualNorm));
            if (kept.Count > 3)
                kept.RemoveAt(kept.Count - 1);
        }

        public static bool SameRoot(double[] a, double[] b)
        {
            for (int i = 0; i < 8; i++)
            {
                if (Math.Abs(a[i] - b[i]) > RootIdentity[i])
                    return false;
            }

            return true;
        }

        // ------------------------------------------------------------------ near the pitchfork

        /// <summary>
        /// Solves at successively smaller |phi| on each side, every solve from the same fixed seed: the
        /// printed state nearest the pitchfork on that side. Characterization only: tolerances are the
        /// solver's own, and no continuation is done.
        /// </summary>
        public static List<MavF15PitchforkProbeStep> ProbePitchfork()
        {
            List<MavF15PitchforkProbeStep> steps = new List<MavF15PitchforkProbeStep>(2 * PitchforkProbePhiDeg.Length);
            int fork = PitchforkPoint();
            for (int side = -1; side <= 1; side += 2)
            {
                MavF15TableViiState seed;
                if (!NearestOnSide(fork, side, out seed))
                    continue;

                for (int i = 0; i < PitchforkProbePhiDeg.Length; i++)
                {
                    double phi = side * PitchforkProbePhiDeg[i];
                    steps.Add(new MavF15PitchforkProbeStep
                    {
                        phiDeg = phi,
                        seedPoint = seed.part1Point,
                        result = MavF15AfitResearchTrimSolver.SolveTurning(
                            MavF15AfitResearchIdentity.ConfigurationId, phi, Guess(Published(seed)))
                    });
                }
            }

            return steps;
        }

        private static bool NearestOnSide(int fork, int side, out MavF15TableViiState nearest)
        {
            nearest = default(MavF15TableViiState);
            double best = double.PositiveInfinity;
            foreach (MavF15TableViiState s in NonSymmetricStates())
            {
                if (Math.Sign(s.phiDeg) != side || Math.Abs(s.phiDeg) >= best)
                    continue;
                best = Math.Abs(s.phiDeg);
                nearest = s;
            }

            return !double.IsPositiveInfinity(best);
        }
    }
}
