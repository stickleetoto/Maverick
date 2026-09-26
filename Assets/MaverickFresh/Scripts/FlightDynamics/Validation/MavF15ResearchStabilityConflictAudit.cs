using System;
using System.Collections.Generic;
using MaverickFresh.FlightDynamics.F15;

namespace MaverickFresh.FlightDynamics.Validation
{
    /// <summary>A source-supported hypothesis for the stability conflict, and its verdict.</summary>
    public struct MavF15ConflictHypothesis
    {
        public string name;

        /// <summary>SUPPORTED, RULED OUT or UNRESOLVED.</summary>
        public string verdict;
        public string evidence;
    }

    /// <summary>
    /// One pitch-damping hypothesis tested against Table VII's turning rows: the CMMQ*QB term scaled by k.
    /// k = 1 is the printed source; everything else is an isolated experiment, never a model.
    /// </summary>
    public struct MavF15PitchDampingTest
    {
        public string hypothesis;
        public double factor;
        public int rows;
        public int rowsAboveFloor;
        public double largestRatio;
    }

    /// <summary>A local step ratio along a printed Table VII branch (step over the mean of its neighbours).</summary>
    public struct MavF15TableStepRatio
    {
        public int from;
        public int to;
        public double ratio;
    }

    /// <summary>
    /// WP-3E Goal B: isolated, validation-side experiments for the source's stability conflict (Table VII
    /// and Baumann's figures present regions as stable that the printed equations make unstable).
    ///
    /// NOTHING HERE IS A MODEL CORRECTION. The production transcription is not changed, no coefficient is
    /// replaced, and no alternate coefficient is invented: every hypothesis either rescales a term the
    /// source prints (to see whether the source's own published equilibria could tolerate it) or checks a
    /// structural property.
    /// </summary>
    public static class MavF15ResearchStabilityConflictAudit
    {
        private const double DegToRad = Math.PI / 180.0;

        /// <summary>Scalings of the printed CMMQ*QB term, each a source-supported alternative reading.</summary>
        public static readonly double[] PitchDampingFactors =
        {
            1.0, -1.0, 0.0, 2.0, 0.5, 42.8 / 15.94, 57.2957795131, 1.0 / 57.2957795131
        };

        public static readonly string[] PitchDampingLabels =
        {
            "printed (k = 1)",
            "sign flip of CMMQ*QB (k = -1)",
            "no pitch-rate term (k = 0; e.g. QB never updated)",
            "QB = Q*CWING/VTRFPS, no factor 2 (k = 2)",
            "QB with 4V (k = 1/2)",
            "QB normalized by span, not chord (k = b/c)",
            "Q in deg/s (k = 57.3)",
            "Q in rad/s but CMMQ per degree (k = 1/57.3)"
        };

        /// <summary>
        /// Evaluates the WP-3A pitching-moment residual at every assembled turning Table VII row (where
        /// q is nonzero) with the CMMQ*QB term scaled by each factor, against WP-3A's print floor. The
        /// term's own value comes from the unchanged coefficient routine: cm(qHat) - cm(0).
        /// </summary>
        public static List<MavF15PitchDampingTest> PitchDampingHypotheses(out double alphaMinDeg, out double alphaMaxDeg,
            out double largestTermCoefficient)
        {
            List<MavF15PitchDampingTest> tests = new List<MavF15PitchDampingTest>();
            alphaMinDeg = double.PositiveInfinity;
            alphaMaxDeg = double.NegativeInfinity;
            largestTermCoefficient = 0.0;
            List<MavF15TableViiState> rows = MavF15TableViiTurningRecovery.NonSymmetricStates();
            double c = MavF15BaumannMach06Reference.MeanAerodynamicChordFt;

            double[] residual = new double[rows.Count], floor = new double[rows.Count], term = new double[rows.Count];
            for (int i = 0; i < rows.Count; i++)
            {
                MavF15TableViiState s = rows[i];
                MavF15EquilibriumResidual e = MavF15TableViiEquilibriumReproduction.Evaluate(s);
                float alpha = (float)(s.alphaDeg * DegToRad);
                float qHat = (float)(s.qRadSec * c / (2.0 * s.trueVelocityFtPerSec));
                MavAeroCoefficients with = MavF15BaumannMach06Longitudinal.Evaluate(alpha, (float)s.stabilatorDeg, qHat);
                MavAeroCoefficients without = MavF15BaumannMach06Longitudinal.Evaluate(alpha, (float)s.stabilatorDeg, 0f);
                residual[i] = e.pitchOverQSc;
                floor[i] = e.floorPitch;
                term[i] = (double)with.cm - without.cm;
                alphaMinDeg = Math.Min(alphaMinDeg, s.alphaDeg);
                alphaMaxDeg = Math.Max(alphaMaxDeg, s.alphaDeg);
                largestTermCoefficient = Math.Max(largestTermCoefficient, Math.Abs(term[i]));
            }

            for (int h = 0; h < PitchDampingFactors.Length; h++)
            {
                MavF15PitchDampingTest t = new MavF15PitchDampingTest
                {
                    hypothesis = PitchDampingLabels[h],
                    factor = PitchDampingFactors[h],
                    rows = rows.Count
                };
                for (int i = 0; i < rows.Count; i++)
                {
                    double r = residual[i] + (PitchDampingFactors[h] - 1.0) * term[i];
                    double ratio = floor[i] > 0.0 ? Math.Abs(r) / floor[i] : double.PositiveInfinity;
                    t.largestRatio = Math.Max(t.largestRatio, ratio);
                    if (Math.Abs(r) > floor[i])
                        t.rowsAboveFloor++;
                }

                tests.Add(t);
            }

            return tests;
        }

        /// <summary>
        /// CMMQ as the routine returns it for a given alpha argument, per unit qHat: (cm(qHat) - cm(-qHat)) / (2 qHat).
        /// Fed alpha in degrees instead of radians, it shows what a degree-argument reading would mean.
        /// </summary>
        public static double CmqFromRoutine(double alphaArgument, double stabilatorDeg)
        {
            const float qHat = 1e-3f;
            MavAeroCoefficients up = MavF15BaumannMach06Longitudinal.Evaluate((float)alphaArgument, (float)stabilatorDeg, qHat);
            MavAeroCoefficients down = MavF15BaumannMach06Longitudinal.Evaluate((float)alphaArgument, (float)stabilatorDeg, -qHat);
            return ((double)up.cm - down.cm) / (2.0 * qHat);
        }

        /// <summary>
        /// Davison's 12-state model adds first-order surface states x' = K (cmd - x) that feed the airframe
        /// and are fed by nothing. With the stabilator as such a state (K = 20 /s, Davison PDF p.97), the
        /// 9x9 Jacobian is [[A, b], [0, -K]]: block triangular, so its spectrum is the WP-3D one plus -K.
        /// Returns the largest distance of the 9x9 spectrum's first eight eigenvalues (matched) from A's.
        /// </summary>
        public static bool ActuatorStateSpectrum(MavF15ResearchLinearization lin, double gain, out double largestShift, out double extraRe)
        {
            largestShift = double.NaN;
            extraRe = double.NaN;
            if (!lin.evaluated || lin.stabilatorColumn == null)
                return false;

            const int n = MavF15ResearchSourceState.Count;
            double[,] big = new double[n + 1, n + 1];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                    big[i, j] = lin.jacobian[i, j];
                big[i, n] = lin.stabilatorColumn[i];
            }

            big[n, n] = -gain;
            double[] re, im;
            if (!MavValidationEigenSolver.Eigenvalues(big, out re, out im))
                return false;

            // The eigenvalue nearest -gain is the actuator's; match the rest to A's.
            int actuator = 0;
            for (int k = 1; k <= n; k++)
            {
                if (MavF15ResearchStabilityAnalysis.Distance(re[k], im[k], -gain, 0.0)
                    < MavF15ResearchStabilityAnalysis.Distance(re[actuator], im[actuator], -gain, 0.0))
                    actuator = k;
            }

            extraRe = re[actuator];
            double[] r8 = new double[n], i8 = new double[n];
            int m = 0;
            for (int k = 0; k <= n; k++)
            {
                if (k == actuator)
                    continue;
                r8[m] = re[k];
                i8[m] = im[k];
                m++;
            }

            int[] match = MavF15ResearchStabilityAnalysis.Match(lin.modes, r8, i8);
            largestShift = 0.0;
            for (int k = 0; k < n; k++)
            {
                largestShift = Math.Max(largestShift,
                    MavF15ResearchStabilityAnalysis.Distance(lin.modes[k].re, lin.modes[k].im, r8[match[k]], i8[match[k]]));
            }

            return true;
        }

        /// <summary>
        /// At a symmetric state the short-period-like pair lives in the longitudinal 4x4 block (alpha, q,
        /// theta, V), which contains no inertia product: returns that block's eigenvalues, and the pitch
        /// damping entry dq'/dq against K8 V (cbar/2) CMMQ from the routine.
        /// </summary>
        public static bool LongitudinalBlock(MavF15ResearchLinearization lin, out double[] re, out double[] im,
            out double pitchDamping, out double pitchDampingFromCmmq)
        {
            re = im = null;
            pitchDamping = pitchDampingFromCmmq = double.NaN;
            if (!lin.evaluated)
                return false;

            int[] idx = { 0, 3, 5, 7 };
            double[,] b = new double[4, 4];
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                    b[i, j] = lin.jacobian[idx[i], idx[j]];
            }

            if (!MavValidationEigenSolver.Eigenvalues(b, out re, out im))
                return false;

            MavF15ResearchSourceState x = lin.equilibrium.state;
            MavF15ResearchSourceKConstants k = MavF15AfitResearchSourceDynamics.KConstants();
            double c = MavF15BaumannMach06Reference.MeanAerodynamicChordFt;
            pitchDamping = lin.jacobian[3, 3];
            pitchDampingFromCmmq = k.k8 * x.trueAirspeedFtPerSec * (c / 2.0) * CmqFromRoutine(x.alphaRad, lin.equilibrium.stabilatorDeg);
            return true;
        }

        /// <summary>
        /// Local step ratios along a printed branch: each step (in the stabilator-alpha plane, degrees)
        /// over the mean of its two neighbours. AUTO's located special points show up as short steps.
        /// </summary>
        public static List<MavF15TableStepRatio> StepRatios(int[] sequence)
        {
            Dictionary<int, MavF15TableViiPart1Row> rows = new Dictionary<int, MavF15TableViiPart1Row>();
            foreach (MavF15TableViiPart1Row r in MavF15BaumannTableVii.Part1)
                rows[r.point] = r;

            List<MavF15TableStepRatio> ratios = new List<MavF15TableStepRatio>();
            for (int i = 1; i + 2 < sequence.Length; i++)
            {
                double s0 = Step(rows[sequence[i - 1]], rows[sequence[i]]);
                double s1 = Step(rows[sequence[i]], rows[sequence[i + 1]]);
                double s2 = Step(rows[sequence[i + 1]], rows[sequence[i + 2]]);
                ratios.Add(new MavF15TableStepRatio { from = sequence[i], to = sequence[i + 1], ratio = s1 / (0.5 * (s0 + s2)) });
            }

            return ratios;
        }

        public static int[] SymmetricPrintedSequence()
        {
            List<int> s = new List<int> { 47, 48, 49, 50 };
            for (int p = 1; p <= 46; p++)
                s.Add(p);
            return s.ToArray();
        }

        public static int[] TurningPrintedSequence()
        {
            List<int> s = new List<int>();
            for (int p = 117; p <= 201; p++)
                s.Add(p);
            return s.ToArray();
        }

        private static double Step(MavF15TableViiPart1Row a, MavF15TableViiPart1Row b)
        {
            double ds = b.stabilatorDeg - a.stabilatorDeg, da = b.alphaDeg - a.alphaDeg;
            return Math.Sqrt(ds * ds + da * da);
        }

        /// <summary>
        /// The hypotheses and verdicts, with their evidence. Verdicts that depend on a measurement are
        /// filled in by the validation suite from the experiments above; the rest are listing evidence.
        /// </summary>
        public static List<MavF15ConflictHypothesis> ListingEvidence()
        {
            return new List<MavF15ConflictHypothesis>
            {
                new MavF15ConflictHypothesis
                {
                    name = "AUTO's Jacobian froze the coefficients (no aerodynamic rate derivatives)",
                    verdict = "RULED OUT",
                    evidence = "Baumann FUNC (PDF pp.93-94) calls COEFF before FUNX inside the DFDU loop for every +/-DX; Davison "
                               + "'Revised 13 Aug 89 - Moved all calls to subroutine COEFF to the start of subroutine FUNX' (PDF p.91): "
                               + "both printed drivers re-evaluate CMM, CMMQ*QB included, at every perturbed state"
                },
                new MavF15ConflictHypothesis
                {
                    name = "COMMON /SEIZE/ storage slip (CMM never reaching FUNX)",
                    verdict = "RULED OUT",
                    evidence = "the text layer reads 'CLM,CCMM,CNM'; the page image (Baumann PDF p.99) reads COMMON /SEIZE/ "
                               + "CX,CY,CZ,CLM,CMM,CNM - an OCR artifact; the pitching moment balances at every printed equilibrium (WP-3A)"
                },
                new MavF15ConflictHypothesis
                {
                    name = "Different state indexing inside COEFF (QB built from another state)",
                    verdict = "RULED OUT",
                    evidence = "COEFF sets AL=U(1), P=U(3), Q=U(4), R=U(5), VTRFPS=U(8)*1000 (Baumann PDF p.100) and "
                               + "QB=(Q*CWING)/(2*VTRFPS) (p.106); RAL=AL/DEGRAD in both listings"
                },
                new MavF15ConflictHypothesis
                {
                    name = "Different flight condition in Davison's diagram",
                    verdict = "RULED OUT",
                    evidence = "Davison Figure 7 (12-state, CAS off; PDF p.43) read on the page image: alpha 10.0 at elevator -5.9 deg, "
                               + "~17.4 at -15.2, Hopf ~21 at -19.8 - the same equilibria as Table VII and WP-3B"
                },
                new MavF15ConflictHypothesis
                {
                    name = "AUTO executed a coefficient routine other than the printed one",
                    verdict = "UNRESOLVED (supported by indirect evidence)",
                    evidence = "every public printing of CMMQ agrees (Baumann PDF p.114, Davison App. B p.116, App. C p.147) and the "
                               + "nonlinear dynamics confirm the instability they imply; yet both executed stability presentations - "
                               + "Baumann Figures C-1..C-8 and Davison Figure 7 ('up to 20 degrees ... can be flown with impunity') - draw "
                               + "alpha 11-14 deg stable, and Table VII shows AUTO special points at every fold and the fork but none at the "
                               + "located Hopf between 8 and 9. Davison's listing notes 'a merger of a later subroutine to an earlier version' "
                               + "(PDF p.101), and McDonnell 1990 refit Cmq against the aero database (repository lineage record). The executed "
                               + "CMMQ itself is not public"
                },
                new MavF15ConflictHypothesis
                {
                    name = "Table VII's caption is a loose label for the printed branch segments",
                    verdict = "SUPPORTED",
                    evidence = "Table VII prints the fold-bounded segments 125-135 and 183-193, whose saddle instability is forced by the "
                               + "stabilator extrema whatever the coefficients (WP-3D; confirmed nonlinearly here), and the neutral pitchfork 165"
                },
                new MavF15ConflictHypothesis
                {
                    name = "Baumann's figures draw the fold saddles solid",
                    verdict = "UNRESOLVED",
                    evidence = "Figure C-7's turning loop is solid throughout; a stabilator extremum along a smooth equilibrium branch forces a "
                               + "real eigenvalue through zero, so one side of each fold must be unstable in ANY smooth model with these "
                               + "equilibria. No source statement explains the line style there"
                }
            };
        }
    }
}
