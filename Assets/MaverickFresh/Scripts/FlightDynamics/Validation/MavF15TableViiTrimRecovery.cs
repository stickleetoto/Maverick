using System;
using System.Collections.Generic;
using MaverickFresh.FlightDynamics.F15;

namespace MaverickFresh.FlightDynamics.Validation
{
    /// <summary>One deterministic perturbation of the published state, used only to build a start.</summary>
    public struct MavF15TrimStartPerturbation
    {
        public float alphaDeg;
        public float stabilatorDeg;
        public float thetaDeg;

        public MavF15TrimStartPerturbation(float alphaDeg, float stabilatorDeg, float thetaDeg)
        {
            this.alphaDeg = alphaDeg;
            this.stabilatorDeg = stabilatorDeg;
            this.thetaDeg = thetaDeg;
        }
    }

    /// <summary>One recovery attempt: published state -> perturbed start -> solver -> recovered state.</summary>
    public struct MavF15TrimRecoveryAttempt
    {
        public MavF15TableViiState published;
        public int perturbationIndex;
        public MavF15ResearchSymmetricTrimResult result;

        /// <summary>Recovered minus printed, degrees.</summary>
        public double alphaErrorDeg;
        public double stabilatorErrorDeg;
        public double thetaErrorDeg;

        /// <summary>
        /// The same state, evaluated by the independent WP-3A static evaluator at the RECOVERED
        /// alpha, stabilator and theta (beta = p = q = r = phi = 0, V as printed). All six axes.
        /// </summary>
        public MavF15EquilibriumResidual independentResidual;
    }

    /// <summary>
    /// First-order effect of Table VII's print resolution on a RECOVERED state. Context, not a tolerance.
    ///
    /// The solver takes V as printed (4 significant digits) and returns alpha, stabilator and theta.
    /// The printed alpha and stabilator carry 7 digits and theta 4. If the model were the source, the
    /// recovered value could still differ from the printed one by up to
    ///   |dx/dV| * (half a unit in V's last digit) + (half a unit in x's own last digit).
    /// dx/dV is measured with the solver itself, by central difference over V's half unit.
    /// </summary>
    public struct MavF15RecoveredStateFloor
    {
        public bool measured;
        public double halfUnitVFtPerSec;
        public double alphaDeg;
        public double stabilatorDeg;
        public double thetaDeg;
    }

    /// <summary>A distinct equilibrium found from a set of starts, with every start that reached it.</summary>
    public struct MavF15TrimRoot
    {
        public float alphaDeg;
        public float stabilatorDeg;
        public float thetaDeg;
        public float residualNorm;
        public int startCount;
        public MavF15ResearchSymmetricTrimGuess firstStart;

        /// <summary>Nearest published symmetric state (first-half point), or 0 when none is printed at this V.</summary>
        public int nearestPublishedPoint;
        public double distanceToNearestPublishedDeg;
    }

    /// <summary>Everything a set of starts produced at one V: distinct roots and the non-converged outcomes.</summary>
    public struct MavF15TrimBranchProbe
    {
        public double trueAirspeedFtPerSec;
        public int starts;
        public int converged;
        public int stoppedAtDemonstratedRangeEdge;
        public int stoppedAtSearchBound;
        public int otherNonConverged;

        /// <summary>Smallest residual norm among the "other" non-converged starts: how far they stopped from an equilibrium.</summary>
        public float smallestOtherNorm;

        public List<MavF15TrimRoot> roots;
    }

    /// <summary>
    /// WP-3B round trip: published Table VII state -> deterministic perturbation -> research trim
    /// solver -> recovered equilibrium.
    ///
    /// The published row is used for exactly two things: to build a start (published + a fixed
    /// perturbation, so the start is never the answer), and as the target the error is reported
    /// against. It never enters the residual: the solver lives in F15/, cannot name Table VII, and
    /// takes only a configuration id, V and a start.
    /// </summary>
    public static class MavF15TableViiTrimRecovery
    {
        /// <summary>
        /// The deterministic perturbations. Every one moves every unknown, in mixed directions, by up
        /// to 2 deg alpha, 1 deg stabilator and 3 deg theta. The stabilator stays inside the
        /// demonstrated range for every symmetric point (-17.31..-9.43 deg).
        /// </summary>
        public static readonly MavF15TrimStartPerturbation[] Perturbations =
        {
            new MavF15TrimStartPerturbation(+1.0f, +0.5f, -2.0f),
            new MavF15TrimStartPerturbation(-1.0f, -0.5f, +2.0f),
            new MavF15TrimStartPerturbation(+2.0f, -1.0f, +3.0f),
            new MavF15TrimStartPerturbation(-2.0f, +1.0f, -3.0f)
        };

        /// <summary>
        /// NUMERICAL root-identity epsilon: two converged solutions closer than this in every unknown
        /// are the same root. About fifty times the termination error the solver's own epsilon
        /// allows (~2e-5 deg), and far below the spacing of any two distinct equilibria found. It
        /// says nothing about agreement with Table VII.
        /// </summary>
        public const float RootIdentityDeg = 1e-3f;

        /// <summary>A start that knows nothing of Table VII, for the start-independence check.</summary>
        public static readonly MavF15ResearchSymmetricTrimGuess GenericStart =
            new MavF15ResearchSymmetricTrimGuess(15f, -12f, 15f);

        /// <summary>Deterministic wide start grid for the branch probe: 13 x 5 x 7 = 455 starts.</summary>
        public static readonly float[] GridAlphaDeg = { -2f, 5f, 10f, 15f, 20f, 25f, 30f, 40f, 50f, 60f, 70f, 80f, 88f };
        public static readonly float[] GridStabilatorDeg = { -24f, -20f, -15f, -10f, -6f };
        public static readonly float[] GridThetaDeg = { -80f, -45f, -15f, 0f, 15f, 45f, 80f };

        public static List<MavF15TableViiState> SymmetricStates()
        {
            List<MavF15TableViiState> states = new List<MavF15TableViiState>(MavF15BaumannTableVii.LastSymmetricPoint);
            foreach (MavF15TableViiState s in MavF15BaumannTableVii.PairedStates())
            {
                if (s.pairing == MavF15TableViiPairing.SymmetricSameLabel)
                    states.Add(s);
            }

            return states;
        }

        public static MavF15ResearchSymmetricTrimGuess StartFor(MavF15TableViiState s, MavF15TrimStartPerturbation p)
        {
            return new MavF15ResearchSymmetricTrimGuess(
                (float)s.alphaDeg + p.alphaDeg,
                (float)s.stabilatorDeg + p.stabilatorDeg,
                (float)s.thetaDeg + p.thetaDeg);
        }

        public static List<MavF15TrimRecoveryAttempt> RunSymmetric()
        {
            List<MavF15TableViiState> states = SymmetricStates();
            List<MavF15TrimRecoveryAttempt> attempts =
                new List<MavF15TrimRecoveryAttempt>(states.Count * Perturbations.Length);

            for (int i = 0; i < states.Count; i++)
            {
                for (int k = 0; k < Perturbations.Length; k++)
                    attempts.Add(Attempt(states[i], k));
            }

            return attempts;
        }

        public static MavF15TrimRecoveryAttempt Attempt(MavF15TableViiState s, int perturbationIndex)
        {
            MavF15ResearchSymmetricTrimResult r = MavF15AfitResearchTrimSolver.SolveSymmetric(
                MavF15AfitResearchIdentity.ConfigurationId, s.trueVelocityFtPerSec,
                StartFor(s, Perturbations[perturbationIndex]));

            MavF15TrimRecoveryAttempt a = new MavF15TrimRecoveryAttempt
            {
                published = s,
                perturbationIndex = perturbationIndex,
                result = r,
                alphaErrorDeg = r.alphaDeg - s.alphaDeg,
                stabilatorErrorDeg = r.symmetricStabilatorDeg - s.stabilatorDeg,
                thetaErrorDeg = r.pitchAttitudeDeg - s.thetaDeg
            };

            if (!r.refused)
            {
                MavF15TableViiState recovered = s;
                recovered.alphaDeg = r.alphaDeg;
                recovered.stabilatorDeg = r.symmetricStabilatorDeg;
                recovered.thetaDeg = r.pitchAttitudeDeg;
                recovered.betaDeg = 0.0;
                recovered.pRadSec = 0.0;
                recovered.qRadSec = 0.0;
                recovered.rRadSec = 0.0;
                recovered.phiDeg = 0.0;
                a.independentResidual = MavF15TableViiEquilibriumReproduction.Evaluate(recovered);
            }

            return a;
        }

        /// <summary>See <see cref="MavF15RecoveredStateFloor"/>.</summary>
        public static MavF15RecoveredStateFloor Floor(MavF15TableViiState s, MavF15ResearchSymmetricTrimResult recovered)
        {
            MavF15RecoveredStateFloor f = new MavF15RecoveredStateFloor();
            if (!recovered.converged)
                return f;

            double h = 1000.0 * MavF15TableViiEquilibriumReproduction.HalfUnitInLastDigit(
                s.trueVelocityFtPerSec / 1000.0, 4);
            MavF15ResearchSymmetricTrimGuess from = new MavF15ResearchSymmetricTrimGuess(
                recovered.alphaDeg, recovered.symmetricStabilatorDeg, recovered.pitchAttitudeDeg);
            MavF15ResearchSymmetricTrimResult up = MavF15AfitResearchTrimSolver.SolveSymmetric(
                MavF15AfitResearchIdentity.ConfigurationId, s.trueVelocityFtPerSec + h, from);
            MavF15ResearchSymmetricTrimResult down = MavF15AfitResearchTrimSolver.SolveSymmetric(
                MavF15AfitResearchIdentity.ConfigurationId, s.trueVelocityFtPerSec - h, from);
            if (!up.converged || !down.converged)
                return f;

            f.measured = true;
            f.halfUnitVFtPerSec = h;
            f.alphaDeg = 0.5 * Math.Abs(up.alphaDeg - down.alphaDeg)
                         + MavF15TableViiEquilibriumReproduction.HalfUnitInLastDigit(s.alphaDeg, 7);
            f.stabilatorDeg = 0.5 * Math.Abs(up.symmetricStabilatorDeg - down.symmetricStabilatorDeg)
                              + MavF15TableViiEquilibriumReproduction.HalfUnitInLastDigit(s.stabilatorDeg, 7);
            f.thetaDeg = 0.5 * Math.Abs(up.pitchAttitudeDeg - down.pitchAttitudeDeg)
                         + MavF15TableViiEquilibriumReproduction.HalfUnitInLastDigit(s.thetaDeg, 4);
            return f;
        }

        /// <summary>
        /// Solves at one V from every start of the wide grid and keeps every distinct converged root,
        /// with the nearest published symmetric state. Nothing is steered toward Table VII: the grid
        /// does not depend on it, and a root no published row matches is preserved and reported.
        /// </summary>
        public static MavF15TrimBranchProbe ProbeBranches(double trueAirspeedFtPerSec)
        {
            MavF15TrimBranchProbe probe = new MavF15TrimBranchProbe
            {
                trueAirspeedFtPerSec = trueAirspeedFtPerSec,
                smallestOtherNorm = float.PositiveInfinity,
                roots = new List<MavF15TrimRoot>()
            };

            for (int a = 0; a < GridAlphaDeg.Length; a++)
            for (int e = 0; e < GridStabilatorDeg.Length; e++)
            for (int t = 0; t < GridThetaDeg.Length; t++)
            {
                MavF15ResearchSymmetricTrimGuess start = new MavF15ResearchSymmetricTrimGuess(
                    GridAlphaDeg[a], GridStabilatorDeg[e], GridThetaDeg[t]);
                MavF15ResearchSymmetricTrimResult r = MavF15AfitResearchTrimSolver.SolveSymmetric(
                    MavF15AfitResearchIdentity.ConfigurationId, trueAirspeedFtPerSec, start);
                probe.starts++;

                if (!r.converged)
                {
                    if (r.stabilatorAtDemonstratedRangeEdge)
                        probe.stoppedAtDemonstratedRangeEdge++;
                    else if (r.alphaAtSearchBound || r.pitchAttitudeAtSearchBound)
                        probe.stoppedAtSearchBound++;
                    else
                    {
                        probe.otherNonConverged++;
                        probe.smallestOtherNorm = Math.Min(probe.smallestOtherNorm, r.drivenResidualNorm);
                    }
                    continue;
                }

                probe.converged++;
                int match = -1;
                for (int i = 0; i < probe.roots.Count; i++)
                {
                    if (Math.Abs(probe.roots[i].alphaDeg - r.alphaDeg) <= RootIdentityDeg
                        && Math.Abs(probe.roots[i].stabilatorDeg - r.symmetricStabilatorDeg) <= RootIdentityDeg
                        && Math.Abs(probe.roots[i].thetaDeg - r.pitchAttitudeDeg) <= RootIdentityDeg)
                    {
                        match = i;
                    }
                }

                if (match >= 0)
                {
                    MavF15TrimRoot known = probe.roots[match];
                    known.startCount++;
                    probe.roots[match] = known;
                    continue;
                }

                MavF15TrimRoot root = new MavF15TrimRoot
                {
                    alphaDeg = r.alphaDeg,
                    stabilatorDeg = r.symmetricStabilatorDeg,
                    thetaDeg = r.pitchAttitudeDeg,
                    residualNorm = r.drivenResidualNorm,
                    startCount = 1,
                    firstStart = start
                };
                Nearest(trueAirspeedFtPerSec, ref root);
                probe.roots.Add(root);
            }

            return probe;
        }

        /// <summary>The nearest published symmetric state printed at this V, by largest-unknown distance.</summary>
        private static void Nearest(double trueAirspeedFtPerSec, ref MavF15TrimRoot root)
        {
            root.nearestPublishedPoint = 0;
            root.distanceToNearestPublishedDeg = double.PositiveInfinity;
            foreach (MavF15TableViiState s in SymmetricStates())
            {
                if (Math.Abs(s.trueVelocityFtPerSec - trueAirspeedFtPerSec) > 1e-6)
                    continue;

                double d = Math.Max(Math.Abs(s.alphaDeg - root.alphaDeg),
                    Math.Max(Math.Abs(s.stabilatorDeg - root.stabilatorDeg), Math.Abs(s.thetaDeg - root.thetaDeg)));
                if (d < root.distanceToNearestPublishedDeg)
                {
                    root.distanceToNearestPublishedDeg = d;
                    root.nearestPublishedPoint = s.part1Point;
                }
            }
        }
    }
}
