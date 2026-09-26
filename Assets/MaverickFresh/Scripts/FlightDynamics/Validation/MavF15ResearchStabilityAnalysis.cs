using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MaverickFresh.FlightDynamics.F15;

namespace MaverickFresh.FlightDynamics.Validation
{
    /// <summary>Where a linearized equilibrium comes from.</summary>
    public enum MavF15ResearchEquilibriumBranch
    {
        /// <summary>Table VII symmetric points 1-91 (less 53, 54), recovered by the WP-3B symmetric solver.</summary>
        Symmetric = 0,

        /// <summary>Table VII turning points 117-199 (less 165, 175, 194), recovered by the WP-3C solver with phi fixed.</summary>
        Turning = 1,

        /// <summary>Table VII point 165, the pitchfork, recovered by the WP-3B symmetric solver.</summary>
        Pitchfork = 2
    }

    /// <summary>
    /// Stability of one eigenvalue or one equilibrium, from the sign of the real part.
    /// NEAR_NEUTRAL means the real part lies inside the NUMERICAL zero band: its sign is not
    /// resolved by the floating-point computation. It is not a physical or source category.
    /// </summary>
    public enum MavF15ResearchStabilityClass
    {
        Stable = 0,
        Unstable = 1,
        NearNeutral = 2,
        NotEvaluated = 3
    }

    /// <summary>One source equilibrium to linearize: the recovered state, with the stabilator held.</summary>
    public struct MavF15ResearchEquilibrium
    {
        public int point;
        public MavF15ResearchEquilibriumBranch branch;
        public MavF15TableViiState published;

        public bool recovered;
        public string note;

        /// <summary>The recovered equilibrium, physical units.</summary>
        public MavF15ResearchSourceState state;

        /// <summary>The recovered stabilator, degrees. Held fixed in the linearization.</summary>
        public double stabilatorDeg;

        /// <summary>The printed parameter the recovery held fixed: V (ft/s) for symmetric and pitchfork, phi (deg) for turning.</summary>
        public double fixedParameter;
    }

    /// <summary>One eigenvalue of one linearization, with its numerical uncertainty and tracked identity.</summary>
    public struct MavF15ResearchMode
    {
        /// <summary>Neutral tracked name, e.g. "A+" / "A-" for a conjugate pair, "D" for a real eigenvalue.</summary>
        public string id;

        public double re;
        public double im;

        /// <summary>|lambda|, rad/s.</summary>
        public double naturalFrequencyRadSec;

        /// <summary>-Re/|lambda| for a complex eigenvalue; NaN for a real one (not meaningful).</summary>
        public double dampingRatio;

        /// <summary>
        /// Eigenvector magnitude per state, commensurable: angles in deg, rates in deg/s, and V as
        /// dV/V0 in degree-equivalent units (x 57.3), scaled to a largest component of 1.
        /// </summary>
        public double[] shape;

        /// <summary>For a real eigenvalue, the signed shape (same scaling); zero for a complex one.</summary>
        public double[] signedShape;

        /// <summary>Fraction of the shape (squared) in beta, p, r, phi: 0 purely longitudinal, 1 purely lateral.</summary>
        public double lateralFraction;

        /// <summary>Largest |delta lambda| to the matching eigenvalue at the plateau neighbour steps.</summary>
        public double stepSpread;

        /// <summary>|delta lambda| to the matching eigenvalue of the independently differenced scaled Jacobian.</summary>
        public double scaledDifference;

        /// <summary>max(stepSpread, scaledDifference) in the real part: the numerical uncertainty of Re.</summary>
        public double realUncertainty;

        /// <summary>NUMERICAL zero band for this eigenvalue's real part. Floating-point only.</summary>
        public double zeroBand;

        /// <summary>|delta lambda| when the printed parameter moves half a unit in its last digit and the state is re-solved.</summary>
        public double printSpread;

        public MavF15ResearchStabilityClass classification;

        /// <summary>|lambda - lambda at the previous point on the branch| for the matched eigenvalue.</summary>
        public double trackingStep;

        /// <summary>
        /// trackingStep / distance to the nearest OTHER eigenvalue at the previous point. Near 1 or
        /// above: identity uncertain (crossing or degeneracy).
        /// </summary>
        public double trackingAmbiguity;

        public double eigenvectorResidual;
    }

    /// <summary>A linearization A = df/dx at one equilibrium, stabilator fixed.</summary>
    public struct MavF15ResearchLinearization
    {
        public MavF15ResearchEquilibrium equilibrium;
        public bool evaluated;
        public string reason;

        /// <summary>x-dot at the equilibrium itself, physical units.</summary>
        public double[] derivative;

        /// <summary>A, physical units (rows d/dt of the state, columns the state).</summary>
        public double[,] jacobian;

        /// <summary>df/d(stabilator), per degree.</summary>
        public double[] stabilatorColumn;

        public MavF15ResearchMode[] modes;
        public MavF15ResearchStabilityClass classification;
        public int unstableCount;
        public int nearNeutralCount;

        /// <summary>|trace(A) - sum(lambda)| / max(1, |trace|), and the same for det/prod.</summary>
        public double traceDefect;
        public double determinantDefect;
        public double largestEigenvectorResidual;
    }

    /// <summary>Eigenvalues of one equilibrium at one step factor, for the step audit.</summary>
    public struct MavF15ResearchStepAuditRow
    {
        public double factor;
        public bool evaluated;
        public double[] re;
        public double[] im;

        /// <summary>Largest |delta lambda| to the matched eigenvalues at the chosen steps.</summary>
        public double largestDifference;

        /// <summary>The same, relative to max(|lambda|, 1e-3).</summary>
        public double largestRelativeDifference;
    }

    /// <summary>A point of the near-pitchfork approach.</summary>
    public struct MavF15ResearchPitchforkApproachPoint
    {
        /// <summary>"symmetric" (V stepped, WP-3B), "phi+" or "phi-" (phi stepped, WP-3C), or "table" (a printed turning point).</summary>
        public string path;
        public double parameter;
        public int tablePoint;
        public bool converged;
        public MavF15ResearchLinearization linearization;

        /// <summary>The real eigenvalue closest to zero, and its lateral content.</summary>
        public double criticalRe;
        public double criticalIm;
        public double criticalLateralFraction;
        public double[] criticalSignedShape;
    }

    /// <summary>A located eigenvalue crossing: the final bisection bracket and the linearization nearer zero.</summary>
    public struct MavF15ResearchCrossing
    {
        public string parameterName;
        public bool bracketed;
        public double parameterA;
        public double parameterB;
        public double largestReA;
        public double largestReB;
        public MavF15ResearchLinearization at;

        /// <summary>|Im| of the crossing eigenvalue (0 for a real crossing), rad/s.</summary>
        public double crossingIm;
        public double crossingLateralFraction;

        /// <summary>Fold only: where the least-squares parabola through the recovered stabilator is extremal.</summary>
        public bool stabilatorExtremumFound;
        public double stabilatorExtremumParameter;
    }

    /// <summary>
    /// WP-3D: local stability of the AFIT/Baumann/Davison RESEARCH SOURCE MODEL at its published
    /// equilibria, from the source's own time-derivative system
    /// (<see cref="MavF15AfitResearchSourceDynamics"/>), never from the trim residual.
    ///
    /// What the source means by stable (audit: Docs/Reference/F15_RESEARCH_STABILITY_ANALYSIS_V1.0.md §1):
    /// Baumann took stability from the eigenvalues of the Jacobian of F(1..8) that AUTO writes to
    /// Fort.9 ("the primary indication whether the equilibrium solutions were stable or not", PDF
    /// p.54), computed by central differences in double precision; the eigenvalues themselves are
    /// never printed. Table VII's caption calls every row a "stable" equilibrium (PDF p.124).
    ///
    /// Everything numerical here is labelled NUMERICAL: the finite-difference steps, the scaling,
    /// and the zero band that separates "sign resolved" from NEAR_NEUTRAL. None is a physical or a
    /// source threshold.
    ///
    /// Research source model only: says nothing about the real F-15, NASA 836, the production FCS,
    /// or a Unity Rigidbody.
    /// </summary>
    public static class MavF15ResearchStabilityAnalysis
    {
        public const int N = MavF15ResearchSourceState.Count;

        private const double DegToRad = Math.PI / 180.0;
        private const double RadToDeg = 180.0 / Math.PI;

        // ------------------------------------------------------------------ numerical settings

        /// <summary>
        /// NUMERICAL central-difference steps in physical units (rad, rad, rad/s x3, rad, rad, ft/s),
        /// each from the plateau of its own column in the step audit (analysis document §4):
        ///   alpha 5e-4 rad  - truncation below, float coefficient noise above; both ~1e-5 relative;
        ///   beta  1e-6 rad  - the routine's |beta| terms make differencing across beta = 0 first
        ///                     order, so the step must be small; float noise stays below ~1e-6;
        ///   p, q, r 1e-2 rad/s - the derivative is at most quadratic in the rates, so a large step
        ///                     costs no truncation and keeps float noise ~1e-7;
        ///   theta, phi 1e-5 rad - they enter only double-precision trigonometry;
        ///   V 1 ft/s        - truncation ~1e-7 against rate-normalization float noise ~5e-7.
        /// Sized to the FLOAT coefficient routine: the source differenced its double-precision routine
        /// with steps of order 1e-9 (PDF p.93), which float coefficients cannot resolve.
        /// </summary>
        public static readonly double[] DirectStep =
        {
            5e-4, 1e-6, 1e-2, 1e-2, 1e-2, 1e-5, 1e-5, 1.0
        };

        /// <summary>
        /// NUMERICAL diagonal scaling D for the independent check, z = D^-1 x: 1 deg for the angles,
        /// 1 deg/s for the rates, 10 ft/s for V. A similarity: the eigenvalues cannot change.
        /// </summary>
        public static readonly double[] ScaleMagnitude =
        {
            DegToRad, DegToRad, DegToRad, DegToRad, DegToRad, DegToRad, DegToRad, 10.0
        };

        /// <summary>
        /// The scaled check differences in z with per-state steps equal to this fraction of
        /// <see cref="DirectStep"/> (converted to z), so it shares no evaluation point with the direct
        /// Jacobian or its plateau neighbours.
        /// </summary>
        public const double ScaledStepFraction = 0.7;

        /// <summary>NUMERICAL step for df/d(stabilator), degrees.</summary>
        public const double StabilatorStepDeg = 1e-2;

        /// <summary>Step multipliers of the audit. The plateau neighbours of the chosen step are 1/3 and 3.</summary>
        public static readonly double[] StepAuditFactors = { 100.0, 30.0, 10.0, 3.0, 1.0, 1.0 / 3.0, 0.1, 0.03, 0.01, 0.003 };

        public static readonly double[] PlateauNeighbourFactors = { 3.0, 1.0 / 3.0 };

        /// <summary>
        /// NUMERICAL zero band: an eigenvalue's real part is sign-resolved only when it exceeds
        /// max(ZeroBandFloorPerSec, ZeroBandUncertaintyMultiple x its own numerical uncertainty).
        /// Floating-point classification only; NOT a source or physical stability margin.
        /// </summary>
        public const double ZeroBandFloorPerSec = 1e-6;

        public const double ZeroBandUncertaintyMultiple = 10.0;

        /// <summary>A tracked eigenvalue whose ambiguity ratio exceeds this is reported as identity-uncertain.</summary>
        public const double TrackingAmbiguityFlag = 0.5;

        /// <summary>Table VII's caption: every row is a "stable" equilibrium (PDF p.124).</summary>
        public const string SourceStabilityLabel = "STABLE (Table VII caption)";

        /// <summary>Symmetric-branch V offsets (ft/s) from the printed pitchfork V, for the approach.</summary>
        public static readonly double[] PitchforkSpeedOffsets =
        {
            -20.0, -10.0, -5.0, -2.0, -1.0, -0.5, -0.2, -0.1, -0.05, 0.0, 0.05, 0.1, 0.2, 0.5, 1.0, 2.0, 5.0, 10.0, 20.0
        };

        /// <summary>|phi| values (deg) for the turning-branch approach, applied with both signs.</summary>
        public static readonly double[] PitchforkPhiDeg = { 20.0, 10.0, 5.0, 2.0, 1.0, 0.5, 0.2, 0.1, 0.05, 0.02, 0.01, 1e-3, 1e-4 };

        // ------------------------------------------------------------------ equilibria

        /// <summary>
        /// All 170 source equilibria: 89 symmetric (printed order), then the turning branch 117-199 in
        /// printed order with the pitchfork 165 in place. Each is re-solved from its printed values by
        /// the WP-3B or WP-3C solver; the printed row is only the start.
        /// </summary>
        public static List<MavF15ResearchEquilibrium> SourceEquilibria()
        {
            List<MavF15ResearchEquilibrium> list = new List<MavF15ResearchEquilibrium>(170);
            foreach (MavF15TableViiState s in MavF15TableViiTrimRecovery.SymmetricStates())
                list.Add(Symmetric(s, s.trueVelocityFtPerSec, MavF15ResearchEquilibriumBranch.Symmetric));

            int fork = MavF15TableViiTurningRecovery.PitchforkPoint();
            foreach (MavF15TableViiState s in MavF15TableViiTurningRecovery.TurningSectionStates())
            {
                list.Add(s.part1Point == fork
                    ? Symmetric(s, s.trueVelocityFtPerSec, MavF15ResearchEquilibriumBranch.Pitchfork)
                    : Turning(s, s.phiDeg));
            }

            return list;
        }

        /// <summary>A symmetric equilibrium at the given V, solved by WP-3B from the printed alpha, stabilator, theta.</summary>
        public static MavF15ResearchEquilibrium Symmetric(MavF15TableViiState s, double speedFtPerSec,
            MavF15ResearchEquilibriumBranch branch)
        {
            MavF15ResearchSymmetricTrimResult r = MavF15AfitResearchTrimSolver.SolveSymmetric(
                MavF15AfitResearchIdentity.ConfigurationId, speedFtPerSec,
                new MavF15ResearchSymmetricTrimGuess((float)s.alphaDeg, (float)s.stabilatorDeg, (float)s.thetaDeg));

            return new MavF15ResearchEquilibrium
            {
                point = s.part1Point,
                branch = branch,
                published = s,
                recovered = r.converged,
                note = MavF15AfitResearchTrimSolver.Describe(r),
                fixedParameter = speedFtPerSec,
                stabilatorDeg = r.symmetricStabilatorDeg,
                state = new MavF15ResearchSourceState
                {
                    alphaRad = r.alphaDeg * DegToRad,
                    thetaRad = r.pitchAttitudeDeg * DegToRad,
                    trueAirspeedFtPerSec = speedFtPerSec
                }
            };
        }

        /// <summary>A turning equilibrium at the given phi, solved by WP-3C from the printed row.</summary>
        public static MavF15ResearchEquilibrium Turning(MavF15TableViiState s, double phiDeg)
        {
            return Turning(s, phiDeg, MavF15TableViiTurningRecovery.Guess(MavF15TableViiTurningRecovery.Published(s)));
        }

        public static MavF15ResearchEquilibrium Turning(MavF15TableViiState s, double phiDeg, MavF15ResearchTurningTrimGuess start)
        {
            MavF15ResearchTurningTrimResult r = MavF15AfitResearchTrimSolver.SolveTurning(
                MavF15AfitResearchIdentity.ConfigurationId, phiDeg, start);
            return FromTurningResult(s, r);
        }

        public static MavF15ResearchEquilibrium FromTurningResult(MavF15TableViiState s, MavF15ResearchTurningTrimResult r)
        {
            MavF15ResearchTurningTrimGuess x = r.solution;
            return new MavF15ResearchEquilibrium
            {
                point = s.part1Point,
                branch = MavF15ResearchEquilibriumBranch.Turning,
                published = s,
                recovered = r.converged,
                note = MavF15AfitResearchTrimSolver.Describe(r),
                fixedParameter = r.phiDeg,
                stabilatorDeg = x.symmetricStabilatorDeg,
                state = new MavF15ResearchSourceState
                {
                    alphaRad = x.alphaDeg * DegToRad,
                    betaRad = x.betaDeg * DegToRad,
                    pRadSec = x.pRadSec,
                    qRadSec = x.qRadSec,
                    rRadSec = x.rRadSec,
                    thetaRad = x.thetaDeg * DegToRad,
                    phiRad = r.phiDeg * DegToRad,
                    trueAirspeedFtPerSec = x.trueAirspeedFtPerSec
                }
            };
        }

        /// <summary>The printed row itself as a source state (degrees converted, V in ft/s).</summary>
        public static MavF15ResearchSourceState PrintedState(MavF15TableViiState s)
        {
            return new MavF15ResearchSourceState
            {
                alphaRad = s.alphaDeg * DegToRad,
                betaRad = s.betaDeg * DegToRad,
                pRadSec = s.pRadSec,
                qRadSec = s.qRadSec,
                rRadSec = s.rRadSec,
                thetaRad = s.thetaDeg * DegToRad,
                phiRad = s.phiDeg * DegToRad,
                trueAirspeedFtPerSec = s.trueVelocityFtPerSec
            };
        }

        /// <summary>The mirror image: beta, p, r, phi negated; alpha, q, theta, V kept.</summary>
        public static MavF15ResearchSourceState Mirror(MavF15ResearchSourceState x)
        {
            x.betaRad = -x.betaRad;
            x.pRadSec = -x.pRadSec;
            x.rRadSec = -x.rRadSec;
            x.phiRad = -x.phiRad;
            return x;
        }

        // ------------------------------------------------------------------ derivative helpers

        public static double[] Derivative(MavF15ResearchSourceState x, double stabilatorDeg)
        {
            double[] d = new double[N];
            return MavF15AfitResearchSourceDynamics.EvaluateStateDerivative(x.ToArray(), stabilatorDeg, d) ? d : null;
        }

        /// <summary>
        /// The state as the WP-3A evaluator reconstructs it from degrees (angles through deg and back).
        /// Compare <see cref="Derivative"/> at this state with <see cref="IndependentDerivative"/>, so
        /// both hand the float coefficient routine the identical angle.
        /// </summary>
        public static MavF15ResearchSourceState ThroughDegrees(MavF15ResearchSourceState x)
        {
            x.alphaRad = (x.alphaRad * RadToDeg) * DegToRad;
            x.betaRad = (x.betaRad * RadToDeg) * DegToRad;
            x.thetaRad = (x.thetaRad * RadToDeg) * DegToRad;
            x.phiRad = (x.phiRad * RadToDeg) * DegToRad;
            return x;
        }

        /// <summary>
        /// x-dot rebuilt INDEPENDENTLY of FUNX from the WP-3A body-axis Newton-Euler evaluator:
        /// u-dot, v-dot, w-dot from its force residuals (times g), then V-dot = (u u' + v v' + w w')/V,
        /// alpha-dot = (u w' - w u')/(u^2 + w^2), beta-dot = (V v' - v V')/(V^2 cos b); p-dot and r-dot
        /// by inverting [Ix -Ixz; -Ixz Iz] numerically (no K constant); q-dot = M/Iy.
        /// </summary>
        public static double[] IndependentDerivative(MavF15ResearchSourceState x, double stabilatorDeg)
        {
            MavF15TableViiState s = new MavF15TableViiState
            {
                stabilatorDeg = stabilatorDeg,
                alphaDeg = x.alphaRad * RadToDeg,
                betaDeg = x.betaRad * RadToDeg,
                pRadSec = x.pRadSec,
                qRadSec = x.qRadSec,
                rRadSec = x.rRadSec,
                thetaDeg = x.thetaRad * RadToDeg,
                phiDeg = x.phiRad * RadToDeg,
                trueVelocityFtPerSec = x.trueAirspeedFtPerSec
            };

            // Evaluate() also computes print floors; only the residuals are used here.
            MavF15EquilibriumResidual e = MavF15TableViiEquilibriumReproduction.Evaluate(s);
            if (!e.evaluated)
                return null;

            // The evaluator's own angles (see ThroughDegrees), so the transformation sees its state.
            double alpha = s.alphaDeg * DegToRad, beta = s.betaDeg * DegToRad;
            double g = MavF15SourceExercisedOperatingDomain.SourceGravityFtPerSec2;
            double V = x.trueAirspeedFtPerSec;
            double S = MavF15BaumannMach06Reference.WingAreaFt2;
            double b = MavF15BaumannMach06Reference.WingSpanFt;
            double c = MavF15BaumannMach06Reference.MeanAerodynamicChordFt;
            double ix = MavF15AfitResearchMassReference.IxxSlugFt2;
            double iy = MavF15AfitResearchMassReference.IyySlugFt2;
            double iz = MavF15AfitResearchMassReference.IzzSlugFt2;
            double ixz = MavF15AfitResearchMassReference.IxzSlugFt2;

            double u = V * Math.Cos(alpha) * Math.Cos(beta);
            double v = V * Math.Sin(beta);
            double w = V * Math.Sin(alpha) * Math.Cos(beta);
            double uDot = g * e.forceXOverWeight;
            double vDot = g * e.forceYOverWeight;
            double wDot = g * e.forceZOverWeight;

            double vtDot = (u * uDot + v * vDot + w * wDot) / V;
            double alphaDot = (u * wDot - w * uDot) / (u * u + w * w);
            double betaDot = (V * vDot - v * vtDot) / (V * V * Math.Cos(beta));

            double qS = e.dynamicPressurePsf * S;
            double rollTerm = e.rollOverQSb * qS * b;   // = Ix p' - Ixz r'
            double yawTerm = e.yawOverQSb * qS * b;     // = Iz r' - Ixz p'
            double det = ix * iz - ixz * ixz;
            double pDot = (iz * rollTerm + ixz * yawTerm) / det;
            double rDot = (ixz * rollTerm + ix * yawTerm) / det;
            double qDot = e.pitchOverQSc * qS * c / iy;

            return new[] { alphaDot, betaDot, pDot, qDot, rDot, e.thetaDotRadSec, e.phiDotRadSec, vtDot };
        }

        /// <summary>
        /// The largest |x-dot| per state that the recovering solver's own termination epsilon
        /// (NumericalSolverTolerance on each normalized trim residual) allows, mapped through the same
        /// transformation as <see cref="IndependentDerivative"/>. Numerical context, not a threshold
        /// on the source.
        /// </summary>
        public static double[] TerminationBound(MavF15ResearchSourceState x)
        {
            double tol = MavF15AfitResearchTrimSolver.NumericalSolverTolerance;
            double g = MavF15SourceExercisedOperatingDomain.SourceGravityFtPerSec2;
            double V = x.trueAirspeedFtPerSec;
            double S = MavF15BaumannMach06Reference.WingAreaFt2;
            double b = MavF15BaumannMach06Reference.WingSpanFt;
            double c = MavF15BaumannMach06Reference.MeanAerodynamicChordFt;
            double ix = MavF15AfitResearchMassReference.IxxSlugFt2;
            double iy = MavF15AfitResearchMassReference.IyySlugFt2;
            double iz = MavF15AfitResearchMassReference.IzzSlugFt2;
            double ixz = MavF15AfitResearchMassReference.IxzSlugFt2;
            double qS = 0.5 * MavF15SourceExercisedOperatingDomain.SourceAirDensitySlugPerFt3 * V * V * S;

            double u = Math.Abs(V * Math.Cos(x.alphaRad) * Math.Cos(x.betaRad));
            double v = Math.Abs(V * Math.Sin(x.betaRad));
            double w = Math.Abs(V * Math.Sin(x.alphaRad) * Math.Cos(x.betaRad));
            double a = g * tol;
            double vtDot = a * (u + v + w) / V;
            double det = ix * iz - Math.Abs(ixz) * Math.Abs(ixz);
            double m = tol * qS * b;
            return new[]
            {
                a * (u + w) / (u * u + w * w),
                (V * a + v * vtDot) / (V * V * Math.Cos(x.betaRad)),
                (iz + Math.Abs(ixz)) * m / det,
                tol * qS * c / iy,
                (ix + Math.Abs(ixz)) * m / det,
                tol,
                tol,
                vtDot
            };
        }

        /// <summary>
        /// Print floor on x-dot at a PRINTED row: sum over the printed fields of
        /// |df/dfield| x half a unit in that field's last printed digit (7 digits for stabilator and
        /// alpha, 4 for the rest), with the Jacobian and stabilator column taken at the printed row.
        /// </summary>
        public static double[] PrintFloor(MavF15TableViiState s, double[,] a, double[] stabilatorColumn)
        {
            double[] half =
            {
                MavF15TableViiEquilibriumReproduction.HalfUnitInLastDigit(s.alphaDeg, 7) * DegToRad,
                MavF15TableViiEquilibriumReproduction.HalfUnitInLastDigit(s.betaDeg, 4) * DegToRad,
                MavF15TableViiEquilibriumReproduction.HalfUnitInLastDigit(s.pRadSec, 4),
                MavF15TableViiEquilibriumReproduction.HalfUnitInLastDigit(s.qRadSec, 4),
                MavF15TableViiEquilibriumReproduction.HalfUnitInLastDigit(s.rRadSec, 4),
                MavF15TableViiEquilibriumReproduction.HalfUnitInLastDigit(s.thetaDeg, 4) * DegToRad,
                MavF15TableViiEquilibriumReproduction.HalfUnitInLastDigit(s.phiDeg, 4) * DegToRad,
                1000.0 * MavF15TableViiEquilibriumReproduction.HalfUnitInLastDigit(s.trueVelocityFtPerSec / 1000.0, 4)
            };
            double halfStab = MavF15TableViiEquilibriumReproduction.HalfUnitInLastDigit(s.stabilatorDeg, 7);

            double[] floor = new double[N];
            for (int i = 0; i < N; i++)
            {
                double f = Math.Abs(stabilatorColumn[i]) * halfStab;
                for (int j = 0; j < N; j++)
                    f += Math.Abs(a[i, j]) * half[j];
                floor[i] = f;
            }

            return floor;
        }

        // ------------------------------------------------------------------ Jacobian

        /// <summary>
        /// A = df/dx by central differences in physical coordinates, stabilator fixed. For alpha and
        /// beta - the two states the float coefficient routine takes directly - the perturbed values
        /// are snapped to float and the quotient uses the true spacing, so the routine and the
        /// kinematics see the same perturbation. Deterministic.
        /// </summary>
        public static bool Jacobian(MavF15ResearchSourceState x0, double stabilatorDeg, double[] steps, out double[,] a)
        {
            a = new double[N, N];
            double[] x = x0.ToArray();
            double[] fp = new double[N], fm = new double[N];
            for (int j = 0; j < N; j++)
            {
                double plus, minus;
                Perturb(j, x[j], steps[j], out plus, out minus);
                double[] xp = (double[])x.Clone(), xm = (double[])x.Clone();
                xp[j] = plus;
                xm[j] = minus;
                if (!MavF15AfitResearchSourceDynamics.EvaluateStateDerivative(xp, stabilatorDeg, fp)
                    || !MavF15AfitResearchSourceDynamics.EvaluateStateDerivative(xm, stabilatorDeg, fm))
                    return false;
                double span = plus - minus;
                for (int i = 0; i < N; i++)
                    a[i, j] = (fp[i] - fm[i]) / span;
            }

            return true;
        }

        public static bool Jacobian(MavF15ResearchSourceState x0, double stabilatorDeg, double stepFactor, out double[,] a)
        {
            double[] steps = new double[N];
            for (int j = 0; j < N; j++)
                steps[j] = DirectStep[j] * stepFactor;
            return Jacobian(x0, stabilatorDeg, steps, out a);
        }

        /// <summary>
        /// A_z = D^-1 (df/dx) D differenced DIRECTLY in scaled coordinates z = D^-1 x with one uniform
        /// step: an independent computation whose eigenvalues must equal those of A.
        /// </summary>
        public static bool ScaledJacobian(MavF15ResearchSourceState x0, double stabilatorDeg, out double[,] az)
        {
            az = new double[N, N];
            double[] x = x0.ToArray();
            double[] fp = new double[N], fm = new double[N];
            for (int j = 0; j < N; j++)
            {
                double[] xp = (double[])x.Clone(), xm = (double[])x.Clone();
                double plus, minus;
                double stepZ = ScaledStepFraction * DirectStep[j] / ScaleMagnitude[j];
                Perturb(j, x[j], ScaleMagnitude[j] * stepZ, out plus, out minus);
                xp[j] = plus;
                xm[j] = minus;
                if (!MavF15AfitResearchSourceDynamics.EvaluateStateDerivative(xp, stabilatorDeg, fp)
                    || !MavF15AfitResearchSourceDynamics.EvaluateStateDerivative(xm, stabilatorDeg, fm))
                    return false;
                double spanZ = (plus - minus) / ScaleMagnitude[j];
                for (int i = 0; i < N; i++)
                    az[i, j] = ((fp[i] - fm[i]) / ScaleMagnitude[i]) / spanZ;
            }

            return true;
        }

        public static bool StabilatorColumn(MavF15ResearchSourceState x0, double stabilatorDeg, out double[] column)
        {
            column = new double[N];
            double[] x = x0.ToArray();
            double[] fp = new double[N], fm = new double[N];
            double up = (float)(stabilatorDeg + StabilatorStepDeg), down = (float)(stabilatorDeg - StabilatorStepDeg);
            if (!MavF15AfitResearchSourceDynamics.EvaluateStateDerivative(x, up, fp)
                || !MavF15AfitResearchSourceDynamics.EvaluateStateDerivative(x, down, fm))
                return false;
            for (int i = 0; i < N; i++)
                column[i] = (fp[i] - fm[i]) / (up - down);
            return true;
        }

        private static void Perturb(int index, double value, double step, out double plus, out double minus)
        {
            plus = value + step;
            minus = value - step;
            if (index <= 1)
            {
                plus = (float)plus;
                minus = (float)minus;
            }
        }

        // ------------------------------------------------------------------ linearization

        /// <summary>
        /// Linearizes one equilibrium: A (direct), its eigenvalues and eigenvectors, the independently
        /// differenced scaled A_z, the plateau-neighbour step spread and the print spread.
        /// Refused for any configuration but the research one.
        /// </summary>
        public static MavF15ResearchLinearization Linearize(string configurationId, MavF15ResearchEquilibrium eq)
        {
            MavF15ResearchLinearization lin = Core(configurationId, eq);
            if (!lin.evaluated)
                return lin;

            // Plateau neighbours.
            for (int f = 0; f < PlateauNeighbourFactors.Length; f++)
            {
                double[,] an;
                double[] re, im;
                if (!Jacobian(eq.state, eq.stabilatorDeg, PlateauNeighbourFactors[f], out an)
                    || !MavValidationEigenSolver.Eigenvalues(an, out re, out im))
                    continue;
                int[] match = Match(lin.modes, re, im);
                for (int k = 0; k < N; k++)
                {
                    double d = Distance(lin.modes[k].re, lin.modes[k].im, re[match[k]], im[match[k]]);
                    lin.modes[k].stepSpread = Math.Max(lin.modes[k].stepSpread, d);
                    lin.modes[k].realUncertainty = Math.Max(lin.modes[k].realUncertainty, Math.Abs(lin.modes[k].re - re[match[k]]));
                }
            }

            // Independent scaled differencing.
            double[,] az;
            double[] zr, zi;
            if (ScaledJacobian(eq.state, eq.stabilatorDeg, out az) && MavValidationEigenSolver.Eigenvalues(az, out zr, out zi))
            {
                int[] match = Match(lin.modes, zr, zi);
                for (int k = 0; k < N; k++)
                {
                    lin.modes[k].scaledDifference = Distance(lin.modes[k].re, lin.modes[k].im, zr[match[k]], zi[match[k]]);
                    lin.modes[k].realUncertainty = Math.Max(lin.modes[k].realUncertainty, Math.Abs(lin.modes[k].re - zr[match[k]]));
                }
            }
            else
            {
                for (int k = 0; k < N; k++)
                    lin.modes[k].scaledDifference = double.NaN;
            }

            Classify(ref lin);
            return lin;
        }

        /// <summary>Only A, eigenvalues and eigenvectors: no neighbours, no scaled check, no classification.</summary>
        public static MavF15ResearchLinearization Core(string configurationId, MavF15ResearchEquilibrium eq)
        {
            MavF15ResearchLinearization lin = new MavF15ResearchLinearization
            {
                equilibrium = eq,
                classification = MavF15ResearchStabilityClass.NotEvaluated
            };

            if (configurationId != MavF15AfitResearchIdentity.ConfigurationId)
            {
                lin.reason = "research stability is for " + MavF15AfitResearchIdentity.ConfigurationId
                             + " only; '" + configurationId + "' has no research source dynamics";
                return lin;
            }

            if (!eq.recovered)
            {
                lin.reason = "equilibrium not recovered: " + eq.note;
                return lin;
            }

            lin.derivative = Derivative(eq.state, eq.stabilatorDeg);
            double[,] a;
            double[] column;
            double[] re, im;
            if (lin.derivative == null || !Jacobian(eq.state, eq.stabilatorDeg, 1.0, out a)
                || !StabilatorColumn(eq.state, eq.stabilatorDeg, out column))
            {
                lin.reason = "the source derivative could not be evaluated";
                return lin;
            }

            if (!MavValidationEigenSolver.Eigenvalues(a, out re, out im))
            {
                lin.reason = "eigenvalues did not converge";
                return lin;
            }

            lin.jacobian = a;
            lin.stabilatorColumn = column;
            lin.modes = new MavF15ResearchMode[N];
            double sumRe = 0.0, sumIm = 0.0;
            double prodRe = 1.0, prodIm = 0.0;
            for (int k = 0; k < N; k++)
            {
                MavF15ResearchMode m = new MavF15ResearchMode
                {
                    id = "",
                    re = re[k],
                    im = im[k],
                    naturalFrequencyRadSec = Math.Sqrt(re[k] * re[k] + im[k] * im[k]),
                    dampingRatio = im[k] != 0.0 ? -re[k] / Math.Sqrt(re[k] * re[k] + im[k] * im[k]) : double.NaN,
                    classification = MavF15ResearchStabilityClass.NotEvaluated
                };
                Shape(a, eq.state.trueAirspeedFtPerSec, ref m);
                lin.largestEigenvectorResidual = Math.Max(lin.largestEigenvectorResidual, m.eigenvectorResidual);
                lin.modes[k] = m;

                sumRe += re[k];
                sumIm += im[k];
                double pr = prodRe * re[k] - prodIm * im[k];
                prodIm = prodRe * im[k] + prodIm * re[k];
                prodRe = pr;
            }

            double trace = MavValidationEigenSolver.Trace(a);
            double det = MavValidationEigenSolver.Determinant(a);
            lin.traceDefect = Math.Sqrt((trace - sumRe) * (trace - sumRe) + sumIm * sumIm) / Math.Max(1.0, Math.Abs(trace));
            lin.determinantDefect = Math.Sqrt((det - prodRe) * (det - prodRe) + prodIm * prodIm) / Math.Max(1e-300, Math.Abs(det));
            lin.evaluated = true;
            lin.reason = "evaluated";
            return lin;
        }

        /// <summary>Eigenvalues of A at the given step factor (for the audit and for determinism checks).</summary>
        public static bool EigenvaluesAt(MavF15ResearchEquilibrium eq, double stepFactor, out double[] re, out double[] im)
        {
            double[,] a;
            re = im = null;
            return Jacobian(eq.state, eq.stabilatorDeg, stepFactor, out a) && MavValidationEigenSolver.Eigenvalues(a, out re, out im);
        }

        private static void Shape(double[,] a, double speedFtPerSec, ref MavF15ResearchMode m)
        {
            double[] vr, vi;
            double residual;
            m.shape = new double[N];
            m.signedShape = new double[N];
            if (!MavValidationEigenSolver.Eigenvector(a, m.re, m.im, out vr, out vi, out residual))
            {
                m.eigenvectorResidual = double.PositiveInfinity;
                return;
            }

            m.eigenvectorResidual = residual;

            // Commensurable units: angles in deg, rates in deg/s, V as dV/V0 in degree-equivalents.
            double[] unit = { RadToDeg, RadToDeg, RadToDeg, RadToDeg, RadToDeg, RadToDeg, RadToDeg, RadToDeg / speedFtPerSec };
            double largest = 0.0;
            int largestIndex = 0;
            for (int i = 0; i < N; i++)
            {
                m.shape[i] = Math.Sqrt(vr[i] * vr[i] + vi[i] * vi[i]) * unit[i];
                if (m.shape[i] > largest)
                {
                    largest = m.shape[i];
                    largestIndex = i;
                }
            }

            double lateral = 0.0, total = 0.0;
            for (int i = 0; i < N; i++)
            {
                m.shape[i] = largest > 0.0 ? m.shape[i] / largest : 0.0;
                total += m.shape[i] * m.shape[i];
                if (i == 1 || i == 2 || i == 4 || i == 6)
                    lateral += m.shape[i] * m.shape[i];
            }

            m.lateralFraction = total > 0.0 ? lateral / total : 0.0;

            if (m.im == 0.0 && largest > 0.0)
            {
                double sign = vr[largestIndex] * unit[largestIndex] >= 0.0 ? 1.0 : -1.0;
                for (int i = 0; i < N; i++)
                    m.signedShape[i] = sign * vr[i] * unit[i] / largest;
            }
        }

        private static void Classify(ref MavF15ResearchLinearization lin)
        {
            lin.unstableCount = 0;
            lin.nearNeutralCount = 0;
            for (int k = 0; k < N; k++)
            {
                MavF15ResearchMode m = lin.modes[k];
                m.zeroBand = Math.Max(ZeroBandFloorPerSec, ZeroBandUncertaintyMultiple * m.realUncertainty);
                if (m.re > m.zeroBand)
                {
                    m.classification = MavF15ResearchStabilityClass.Unstable;
                    lin.unstableCount++;
                }
                else if (m.re < -m.zeroBand)
                {
                    m.classification = MavF15ResearchStabilityClass.Stable;
                }
                else
                {
                    m.classification = MavF15ResearchStabilityClass.NearNeutral;
                    lin.nearNeutralCount++;
                }

                lin.modes[k] = m;
            }

            lin.classification = lin.unstableCount > 0
                ? MavF15ResearchStabilityClass.Unstable
                : lin.nearNeutralCount > 0 ? MavF15ResearchStabilityClass.NearNeutral : MavF15ResearchStabilityClass.Stable;
        }

        /// <summary>
        /// Adds the print spread: re-solves the equilibrium with its printed parameter moved by half a
        /// unit in the last printed digit (V for symmetric/pitchfork, phi for turning), both ways, and
        /// records the largest eigenvalue change. Source-resolution context, not numerical uncertainty.
        /// </summary>
        public static void AddPrintSpread(ref MavF15ResearchLinearization lin)
        {
            if (!lin.evaluated)
                return;

            MavF15ResearchEquilibrium eq = lin.equilibrium;
            for (int side = -1; side <= 1; side += 2)
            {
                MavF15ResearchEquilibrium moved;
                if (eq.branch == MavF15ResearchEquilibriumBranch.Turning)
                {
                    double h = MavF15TableViiEquilibriumReproduction.HalfUnitInLastDigit(eq.published.phiDeg, 4);
                    MavF15ResearchTurningTrimGuess start = new MavF15ResearchTurningTrimGuess
                    {
                        alphaDeg = (float)(eq.state.alphaRad * RadToDeg),
                        betaDeg = (float)(eq.state.betaRad * RadToDeg),
                        pRadSec = (float)eq.state.pRadSec,
                        qRadSec = (float)eq.state.qRadSec,
                        rRadSec = (float)eq.state.rRadSec,
                        thetaDeg = (float)(eq.state.thetaRad * RadToDeg),
                        trueAirspeedFtPerSec = (float)eq.state.trueAirspeedFtPerSec,
                        symmetricStabilatorDeg = (float)eq.stabilatorDeg
                    };
                    moved = Turning(eq.published, eq.published.phiDeg + side * h, start);
                }
                else
                {
                    double h = 1000.0 * MavF15TableViiEquilibriumReproduction.HalfUnitInLastDigit(
                        eq.published.trueVelocityFtPerSec / 1000.0, 4);
                    moved = Symmetric(eq.published, eq.published.trueVelocityFtPerSec + side * h, eq.branch);
                }

                double[] re, im;
                if (!moved.recovered || !EigenvaluesAt(moved, 1.0, out re, out im))
                {
                    for (int k = 0; k < N; k++)
                        lin.modes[k].printSpread = double.NaN;
                    return;
                }

                int[] match = Match(lin.modes, re, im);
                for (int k = 0; k < N; k++)
                {
                    lin.modes[k].printSpread = Math.Max(lin.modes[k].printSpread,
                        Distance(lin.modes[k].re, lin.modes[k].im, re[match[k]], im[match[k]]));
                }
            }
        }

        // ------------------------------------------------------------------ matching and tracking

        /// <summary>
        /// The assignment of the given eigenvalues to the modes that minimizes the summed distance,
        /// over all permutations (8! = 40,320; exact, deterministic). match[k] is the index of the
        /// eigenvalue assigned to modes[k].
        /// </summary>
        public static int[] Match(MavF15ResearchMode[] modes, double[] re, double[] im)
        {
            int n = modes.Length;
            double[,] cost = new double[n, n];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                    cost[i, j] = Distance(modes[i].re, modes[i].im, re[j], im[j]);
            }

            int[] best = new int[n];
            int[] current = new int[n];
            bool[] used = new bool[n];
            double bestCost = double.PositiveInfinity;
            Search(0, n, cost, current, used, 0.0, ref bestCost, best);
            return best;
        }

        private static void Search(int row, int n, double[,] cost, int[] current, bool[] used, double sum,
            ref double bestCost, int[] best)
        {
            if (sum >= bestCost)
                return;
            if (row == n)
            {
                bestCost = sum;
                Array.Copy(current, best, n);
                return;
            }

            for (int j = 0; j < n; j++)
            {
                if (used[j])
                    continue;
                used[j] = true;
                current[row] = j;
                Search(row + 1, n, cost, current, used, sum + cost[row, j], ref bestCost, best);
                used[j] = false;
            }
        }

        public static double Distance(double re1, double im1, double re2, double im2)
        {
            double dr = re1 - re2, di = im1 - im2;
            return Math.Sqrt(dr * dr + di * di);
        }

        /// <summary>
        /// Tracks modes along an ordered branch from a seed index outward in both directions, by the
        /// minimum-distance assignment between neighbours (never by sorting each point on its own).
        /// The seed gets neutral names; each later point's modes are reordered to follow their
        /// predecessors, and every mode records its tracking step and ambiguity.
        /// </summary>
        public static void Track(List<MavF15ResearchLinearization> branch, int seed)
        {
            if (branch.Count == 0 || !branch[seed].evaluated)
                return;

            MavF15ResearchLinearization s = branch[seed];
            NameSeed(ref s);
            branch[seed] = s;

            for (int direction = -1; direction <= 1; direction += 2)
            {
                int previous = seed;
                for (int i = seed + direction; i >= 0 && i < branch.Count; i += direction)
                {
                    if (!branch[i].evaluated)
                        continue;
                    MavF15ResearchLinearization current = branch[i];
                    Follow(branch[previous].modes, ref current);
                    branch[i] = current;
                    previous = i;
                }
            }
        }

        private static void Follow(MavF15ResearchMode[] previous, ref MavF15ResearchLinearization current)
        {
            double[] re = new double[N], im = new double[N];
            for (int k = 0; k < N; k++)
            {
                re[k] = current.modes[k].re;
                im[k] = current.modes[k].im;
            }

            int[] match = Match(previous, re, im);
            MavF15ResearchMode[] ordered = new MavF15ResearchMode[N];
            for (int k = 0; k < N; k++)
            {
                MavF15ResearchMode m = current.modes[match[k]];
                m.id = previous[k].id;
                m.trackingStep = Distance(previous[k].re, previous[k].im, m.re, m.im);
                double nearestOther = double.PositiveInfinity;
                for (int j = 0; j < N; j++)
                {
                    if (j != k)
                        nearestOther = Math.Min(nearestOther, Distance(previous[k].re, previous[k].im, previous[j].re, previous[j].im));
                }

                m.trackingAmbiguity = nearestOther > 0.0 ? m.trackingStep / nearestOther : double.PositiveInfinity;
                ordered[k] = m;
            }

            current.modes = ordered;
        }

        /// <summary>
        /// Neutral names at a seed: longitudinal-dominated eigenvalues first, then lateral, each group
        /// by descending |lambda|; a conjugate pair shares a letter ("A+", "A-").
        /// </summary>
        private static void NameSeed(ref MavF15ResearchLinearization lin)
        {
            MavF15ResearchMode[] m = lin.modes;
            int[] order = new int[N];
            for (int k = 0; k < N; k++)
                order[k] = k;

            Array.Sort(order, (x, y) =>
            {
                bool lx = m[x].lateralFraction > 0.5, ly = m[y].lateralFraction > 0.5;
                if (lx != ly)
                    return lx ? 1 : -1;
                int c = m[y].naturalFrequencyRadSec.CompareTo(m[x].naturalFrequencyRadSec);
                if (c != 0)
                    return c;
                return m[y].im.CompareTo(m[x].im);
            });

            MavF15ResearchMode[] named = new MavF15ResearchMode[N];
            char letter = 'A';
            int filled = 0;
            bool[] done = new bool[N];
            for (int o = 0; o < N; o++)
            {
                int k = order[o];
                if (done[k])
                    continue;
                done[k] = true;
                if (m[k].im != 0.0)
                {
                    int partner = -1;
                    for (int j = 0; j < N; j++)
                    {
                        if (!done[j] && m[j].im == -m[k].im && m[j].re == m[k].re)
                            partner = j;
                    }

                    MavF15ResearchMode a = m[k];
                    a.id = letter + (a.im > 0.0 ? "+" : "-");
                    named[filled++] = a;
                    if (partner >= 0)
                    {
                        done[partner] = true;
                        MavF15ResearchMode b = m[partner];
                        b.id = letter + (b.im > 0.0 ? "+" : "-");
                        named[filled++] = b;
                    }
                }
                else
                {
                    MavF15ResearchMode a = m[k];
                    a.id = letter.ToString();
                    named[filled++] = a;
                }

                letter++;
            }

            lin.modes = named;
        }

        // ------------------------------------------------------------------ step audit

        /// <summary>
        /// Eigenvalues at every audit factor of <see cref="DirectStep"/>, each matched to the chosen
        /// (factor 1) spectrum. NUMERICAL: the plateau says where differencing noise and truncation
        /// both stay small.
        /// </summary>
        public static List<MavF15ResearchStepAuditRow> StepAudit(MavF15ResearchEquilibrium eq)
        {
            List<MavF15ResearchStepAuditRow> rows = new List<MavF15ResearchStepAuditRow>(StepAuditFactors.Length);
            MavF15ResearchLinearization reference = Core(MavF15AfitResearchIdentity.ConfigurationId, eq);
            for (int f = 0; f < StepAuditFactors.Length; f++)
            {
                MavF15ResearchStepAuditRow row = new MavF15ResearchStepAuditRow { factor = StepAuditFactors[f] };
                double[] re = null, im = null;
                row.evaluated = reference.evaluated && EigenvaluesAt(eq, StepAuditFactors[f], out re, out im);
                if (row.evaluated)
                {
                    int[] match = Match(reference.modes, re, im);
                    row.re = new double[N];
                    row.im = new double[N];
                    for (int k = 0; k < N; k++)
                    {
                        row.re[k] = re[match[k]];
                        row.im[k] = im[match[k]];
                        double d = Distance(reference.modes[k].re, reference.modes[k].im, row.re[k], row.im[k]);
                        row.largestDifference = Math.Max(row.largestDifference, d);
                        row.largestRelativeDifference = Math.Max(row.largestRelativeDifference,
                            d / Math.Max(reference.modes[k].naturalFrequencyRadSec, 1e-3));
                    }
                }

                rows.Add(row);
            }

            return rows;
        }

        // ------------------------------------------------------------------ pitchfork

        /// <summary>
        /// The approach to the pitchfork from all three branches, every point re-solved by the WP-3B
        /// or WP-3C solver (no continuation): the symmetric branch with V stepped about the printed
        /// pitchfork V, and the left and right turning branches with phi stepped toward 0 from the
        /// printed neighbours 164 / 166. Plus the printed turning points nearest the fork.
        /// </summary>
        public static List<MavF15ResearchPitchforkApproachPoint> PitchforkApproach()
        {
            List<MavF15ResearchPitchforkApproachPoint> points = new List<MavF15ResearchPitchforkApproachPoint>(64);
            int fork = MavF15TableViiTurningRecovery.PitchforkPoint();
            MavF15TableViiState forkState;
            MavF15TableViiTurningRecovery.TryGetState(fork, out forkState);

            for (int i = 0; i < PitchforkSpeedOffsets.Length; i++)
            {
                double v = forkState.trueVelocityFtPerSec + PitchforkSpeedOffsets[i];
                MavF15ResearchEquilibrium eq = Symmetric(forkState, v, MavF15ResearchEquilibriumBranch.Pitchfork);
                points.Add(ApproachPoint("symmetric", v, 0, eq));
            }

            for (int side = 1; side >= -1; side -= 2)
            {
                MavF15TableViiState seed;
                if (!MavF15TableViiTurningRecovery.TryGetState(fork - side, out seed))
                    continue;
                for (int i = 0; i < PitchforkPhiDeg.Length; i++)
                {
                    double phi = Math.Sign(seed.phiDeg) * PitchforkPhiDeg[i];
                    MavF15ResearchEquilibrium eq = Turning(seed, phi);
                    points.Add(ApproachPoint(phi > 0.0 ? "phi+" : "phi-", phi, 0, eq));
                }
            }

            foreach (MavF15TableViiState s in MavF15TableViiTurningRecovery.TurningSectionStates())
            {
                if (Math.Abs(s.part1Point - fork) > 6)
                    continue;
                MavF15ResearchEquilibrium eq = s.part1Point == fork
                    ? Symmetric(s, s.trueVelocityFtPerSec, MavF15ResearchEquilibriumBranch.Pitchfork)
                    : Turning(s, s.phiDeg);
                points.Add(ApproachPoint("table", s.part1Point == fork ? s.trueVelocityFtPerSec : s.phiDeg, s.part1Point, eq));
            }

            return points;
        }

        private static MavF15ResearchPitchforkApproachPoint ApproachPoint(string path, double parameter, int tablePoint,
            MavF15ResearchEquilibrium eq)
        {
            MavF15ResearchPitchforkApproachPoint p = new MavF15ResearchPitchforkApproachPoint
            {
                path = path,
                parameter = parameter,
                tablePoint = tablePoint,
                converged = eq.recovered,
                criticalRe = double.NaN,
                criticalIm = double.NaN
            };

            if (!eq.recovered)
                return p;

            p.linearization = Linearize(MavF15AfitResearchIdentity.ConfigurationId, eq);
            if (!p.linearization.evaluated)
                return p;

            int critical = -1;
            for (int k = 0; k < N; k++)
            {
                MavF15ResearchMode m = p.linearization.modes[k];
                if (m.im != 0.0)
                    continue;
                if (critical < 0 || Math.Abs(m.re) < Math.Abs(p.linearization.modes[critical].re))
                    critical = k;
            }

            if (critical >= 0)
            {
                MavF15ResearchMode m = p.linearization.modes[critical];
                p.criticalRe = m.re;
                p.criticalIm = m.im;
                p.criticalLateralFraction = m.lateralFraction;
                p.criticalSignedShape = m.signedShape;
            }

            return p;
        }

        // ------------------------------------------------------------------ crossings

        /// <summary>Solves in each fold fit, across the eigenvalue bracket.</summary>
        public const int FoldFitPoints = 9;

        /// <summary>
        /// Locates, by bisection in V on the symmetric branch, where the largest real part of the
        /// eigenvalues (all of them, or only the real ones when <paramref name="realOnly"/>) changes
        /// sign between <paramref name="vA"/> and <paramref name="vB"/>. Every point is an independent
        /// WP-3B solve from the same printed seed: no continuation. Returns the bracket it ends with.
        /// </summary>
        public static MavF15ResearchCrossing LocateSymmetricCrossing(MavF15TableViiState seed, double vA, double vB,
            bool realOnly, int iterations)
        {
            MavF15ResearchCrossing c = new MavF15ResearchCrossing { parameterName = "V_ft_s" };
            MavF15ResearchLinearization la = CoreAtSpeed(seed, vA), lb = CoreAtSpeed(seed, vB);
            double ra = Largest(la, realOnly), rb = Largest(lb, realOnly);
            c.bracketed = la.evaluated && lb.evaluated && Math.Sign(ra) * Math.Sign(rb) < 0;
            if (!c.bracketed)
                return c;

            for (int i = 0; i < iterations; i++)
            {
                double vm = 0.5 * (vA + vB);
                MavF15ResearchLinearization lm = CoreAtSpeed(seed, vm);
                if (!lm.evaluated)
                    break;
                double rm = Largest(lm, realOnly);
                if (Math.Sign(rm) == Math.Sign(ra))
                {
                    vA = vm;
                    ra = rm;
                    la = lm;
                }
                else
                {
                    vB = vm;
                    rb = rm;
                    lb = lm;
                }
            }

            c.parameterA = vA;
            c.parameterB = vB;
            c.largestReA = ra;
            c.largestReB = rb;
            c.at = Math.Abs(ra) <= Math.Abs(rb) ? la : lb;
            MavF15ResearchMode m = CrossingMode(c.at, realOnly);
            c.crossingIm = Math.Abs(m.im);
            c.crossingLateralFraction = m.lateralFraction;
            return c;
        }

        /// <summary>
        /// Locates, by bisection in phi on the turning branch, where the real eigenvalue nearest zero
        /// changes sign between two bank angles, each point an independent WP-3C solve from the same
        /// printed start. Also fits a parabola to the recovered stabilator over the bracket: at a fold
        /// the stabilator is extremal exactly where the Jacobian is singular.
        /// </summary>
        public static MavF15ResearchCrossing LocateTurningFold(MavF15TableViiState start, double phiA, double phiB, int iterations)
        {
            MavF15ResearchCrossing c = new MavF15ResearchCrossing { parameterName = "phi_deg" };
            MavF15ResearchLinearization la = CoreAtPhi(start, phiA), lb = CoreAtPhi(start, phiB);
            double ra = CriticalReal(la), rb = CriticalReal(lb);
            c.bracketed = la.evaluated && lb.evaluated && Math.Sign(ra) * Math.Sign(rb) < 0;
            if (!c.bracketed)
                return c;

            double a0 = phiA, b0 = phiB;
            for (int i = 0; i < iterations; i++)
            {
                double pm = 0.5 * (phiA + phiB);
                MavF15ResearchLinearization lm = CoreAtPhi(start, pm);
                if (!lm.evaluated)
                    break;
                double rm = CriticalReal(lm);
                if (Math.Sign(rm) == Math.Sign(ra))
                {
                    phiA = pm;
                    ra = rm;
                    la = lm;
                }
                else
                {
                    phiB = pm;
                    rb = rm;
                    lb = lm;
                }
            }

            c.parameterA = phiA;
            c.parameterB = phiB;
            c.largestReA = ra;
            c.largestReB = rb;
            c.at = Math.Abs(ra) <= Math.Abs(rb) ? la : lb;
            c.crossingLateralFraction = CriticalMode(c.at).lateralFraction;

            // Stabilator extremum: least-squares parabola through FoldFitPoints solves across the bracket.
            int n = FoldFitPoints;
            double[] x = new double[n], y = new double[n];
            int used = 0;
            for (int i = 0; i < n; i++)
            {
                double phi = a0 + (b0 - a0) * i / (n - 1);
                MavF15ResearchEquilibrium eq = Turning(start, phi);
                if (!eq.recovered)
                    continue;
                x[used] = phi;
                y[used] = eq.stabilatorDeg;
                used++;
            }

            double vertex = double.NaN;
            c.stabilatorExtremumFound = used >= 3 && ParabolaVertex(x, y, used, out vertex);
            c.stabilatorExtremumParameter = c.stabilatorExtremumFound ? vertex : double.NaN;
            return c;
        }

        private static MavF15ResearchLinearization CoreAtSpeed(MavF15TableViiState seed, double v)
        {
            MavF15ResearchEquilibrium eq = Symmetric(seed, v, MavF15ResearchEquilibriumBranch.Symmetric);
            return Core(MavF15AfitResearchIdentity.ConfigurationId, eq);
        }

        private static MavF15ResearchLinearization CoreAtPhi(MavF15TableViiState start, double phi)
        {
            return Core(MavF15AfitResearchIdentity.ConfigurationId, Turning(start, phi));
        }

        private static double Largest(MavF15ResearchLinearization lin, bool realOnly)
        {
            if (!lin.evaluated)
                return double.NaN;
            double r = double.NegativeInfinity;
            for (int k = 0; k < N; k++)
            {
                if (realOnly && lin.modes[k].im != 0.0)
                    continue;
                r = Math.Max(r, lin.modes[k].re);
            }

            return r;
        }

        private static MavF15ResearchMode CrossingMode(MavF15ResearchLinearization lin, bool realOnly)
        {
            int best = 0;
            double r = double.NegativeInfinity;
            for (int k = 0; k < N; k++)
            {
                if (realOnly && lin.modes[k].im != 0.0)
                    continue;
                if (lin.modes[k].re > r)
                {
                    r = lin.modes[k].re;
                    best = k;
                }
            }

            return lin.modes[best];
        }

        /// <summary>The real eigenvalue with the smallest magnitude, NaN when there is none.</summary>
        public static double CriticalReal(MavF15ResearchLinearization lin)
        {
            MavF15ResearchMode m = CriticalMode(lin);
            return m.shape == null ? double.NaN : m.re;
        }

        /// <summary>The real mode with the smallest |eigenvalue|; default when there is none.</summary>
        public static MavF15ResearchMode CriticalMode(MavF15ResearchLinearization lin)
        {
            MavF15ResearchMode best = default(MavF15ResearchMode);
            if (!lin.evaluated)
                return best;
            bool found = false;
            for (int k = 0; k < N; k++)
            {
                if (lin.modes[k].im != 0.0)
                    continue;
                if (!found || Math.Abs(lin.modes[k].re) < Math.Abs(best.re))
                {
                    best = lin.modes[k];
                    found = true;
                }
            }

            return best;
        }

        private static bool ParabolaVertex(double[] x, double[] y, int n, out double vertex)
        {
            // Least squares y = c0 + c1 t + c2 t^2, t = x - mean(x); 3x3 normal equations by Cramer's rule.
            double mean = 0.0;
            for (int i = 0; i < n; i++)
                mean += x[i];
            mean /= n;

            double s0 = n, s1 = 0.0, s2 = 0.0, s3 = 0.0, s4 = 0.0, t0 = 0.0, t1 = 0.0, t2 = 0.0;
            for (int i = 0; i < n; i++)
            {
                double t = x[i] - mean;
                s1 += t;
                s2 += t * t;
                s3 += t * t * t;
                s4 += t * t * t * t;
                t0 += y[i];
                t1 += y[i] * t;
                t2 += y[i] * t * t;
            }

            vertex = double.NaN;
            double det = s0 * (s2 * s4 - s3 * s3) - s1 * (s1 * s4 - s2 * s3) + s2 * (s1 * s3 - s2 * s2);
            if (det == 0.0)
                return false;
            double c1 = (s0 * (t1 * s4 - s3 * t2) - t0 * (s1 * s4 - s2 * s3) + s2 * (s1 * t2 - t1 * s2)) / det;
            double c2 = (s0 * (s2 * t2 - t1 * s3) - s1 * (s1 * t2 - t1 * s2) + t0 * (s1 * s3 - s2 * s2)) / det;
            if (c2 == 0.0)
                return false;
            vertex = mean - c1 / (2.0 * c2);
            return true;
        }

        // ------------------------------------------------------------------ labels and export

        public static string ClassLabel(MavF15ResearchStabilityClass c)
        {
            switch (c)
            {
                case MavF15ResearchStabilityClass.Stable: return "STABLE";
                case MavF15ResearchStabilityClass.Unstable: return "UNSTABLE";
                case MavF15ResearchStabilityClass.NearNeutral: return "NEAR_NEUTRAL";
                default: return "NOT_EVALUATED";
            }
        }

        public static string BranchLabel(MavF15ResearchEquilibriumBranch b)
        {
            switch (b)
            {
                case MavF15ResearchEquilibriumBranch.Symmetric: return "symmetric";
                case MavF15ResearchEquilibriumBranch.Turning: return "turning";
                default: return "pitchfork";
            }
        }

        /// <summary>
        /// Source-vs-computed: the source labels every Table VII row stable. AGREE when computed
        /// STABLE, DISAGREE when computed UNSTABLE, UNRESOLVED when an eigenvalue is NEAR_NEUTRAL.
        /// </summary>
        public static string Agreement(MavF15ResearchLinearization lin)
        {
            switch (lin.classification)
            {
                case MavF15ResearchStabilityClass.Stable: return "AGREE";
                case MavF15ResearchStabilityClass.Unstable: return "DISAGREE";
                case MavF15ResearchStabilityClass.NearNeutral: return "UNRESOLVED";
                default: return "NOT_EVALUATED";
            }
        }

        public const string DatasetHeader =
            "point,branch,stabilator_deg,V_ft_s,alpha_deg,beta_deg,p_rad_s,q_rad_s,r_rad_s,theta_deg,phi_deg,"
            + "mode,real_per_s,imag_rad_s,natural_frequency_rad_s,damping_ratio,lateral_fraction,"
            + "mode_classification,equilibrium_classification,unstable_count,source_classification,agreement,"
            + "fd_step_spread,fd_scaled_difference,zero_band,print_spread,tracking_ambiguity";

        /// <summary>One CSV row per eigenvalue, per equilibrium, in branch order.</summary>
        public static string DatasetCsv(List<MavF15ResearchLinearization> all)
        {
            StringBuilder s = new StringBuilder(all.Count * N * 320);
            s.Append(DatasetHeader).Append('\n');
            CultureInfo c = CultureInfo.InvariantCulture;
            foreach (MavF15ResearchLinearization lin in all)
            {
                MavF15ResearchEquilibrium eq = lin.equilibrium;
                MavF15ResearchSourceState x = eq.state;
                for (int k = 0; k < (lin.evaluated ? N : 0); k++)
                {
                    MavF15ResearchMode m = lin.modes[k];
                    s.Append(eq.point.ToString(c)).Append(',')
                     .Append(BranchLabel(eq.branch)).Append(',')
                     .Append(eq.stabilatorDeg.ToString("R", c)).Append(',')
                     .Append(x.trueAirspeedFtPerSec.ToString("R", c)).Append(',')
                     .Append((x.alphaRad * RadToDeg).ToString("R", c)).Append(',')
                     .Append((x.betaRad * RadToDeg).ToString("R", c)).Append(',')
                     .Append(x.pRadSec.ToString("R", c)).Append(',')
                     .Append(x.qRadSec.ToString("R", c)).Append(',')
                     .Append(x.rRadSec.ToString("R", c)).Append(',')
                     .Append((x.thetaRad * RadToDeg).ToString("R", c)).Append(',')
                     .Append((x.phiRad * RadToDeg).ToString("R", c)).Append(',')
                     .Append(m.id).Append(',')
                     .Append(m.re.ToString("R", c)).Append(',')
                     .Append(m.im.ToString("R", c)).Append(',')
                     .Append(m.naturalFrequencyRadSec.ToString("R", c)).Append(',')
                     .Append(double.IsNaN(m.dampingRatio) ? "" : m.dampingRatio.ToString("R", c)).Append(',')
                     .Append(m.lateralFraction.ToString("F6", c)).Append(',')
                     .Append(ClassLabel(m.classification)).Append(',')
                     .Append(ClassLabel(lin.classification)).Append(',')
                     .Append(lin.unstableCount.ToString(c)).Append(',')
                     .Append(SourceStabilityLabel).Append(',')
                     .Append(Agreement(lin)).Append(',')
                     .Append(m.stepSpread.ToString("E3", c)).Append(',')
                     .Append(m.scaledDifference.ToString("E3", c)).Append(',')
                     .Append(m.zeroBand.ToString("E3", c)).Append(',')
                     .Append(double.IsNaN(m.printSpread) ? "" : m.printSpread.ToString("E3", c)).Append(',')
                     .Append(m.trackingAmbiguity.ToString("F4", c)).Append('\n');
                }
            }

            return s.ToString();
        }

        /// <summary>One CSV row per equilibrium: the 64 entries of A, row-major, physical units.</summary>
        public static string JacobianCsv(List<MavF15ResearchLinearization> all)
        {
            StringBuilder s = new StringBuilder(all.Count * 1600);
            CultureInfo c = CultureInfo.InvariantCulture;
            s.Append("point,branch,stabilator_deg");
            for (int i = 0; i < N; i++)
            {
                for (int j = 0; j < N; j++)
                    s.Append(",A_").Append(MavF15AfitResearchSourceDynamics.StateNames[i]).Append("dot_")
                     .Append(MavF15AfitResearchSourceDynamics.StateNames[j]);
            }

            s.Append('\n');
            foreach (MavF15ResearchLinearization lin in all)
            {
                if (!lin.evaluated)
                    continue;
                s.Append(lin.equilibrium.point.ToString(c)).Append(',').Append(BranchLabel(lin.equilibrium.branch))
                 .Append(',').Append(lin.equilibrium.stabilatorDeg.ToString("R", c));
                for (int i = 0; i < N; i++)
                {
                    for (int j = 0; j < N; j++)
                        s.Append(',').Append(lin.jacobian[i, j].ToString("R", c));
                }

                s.Append('\n');
            }

            return s.ToString();
        }
    }
}
