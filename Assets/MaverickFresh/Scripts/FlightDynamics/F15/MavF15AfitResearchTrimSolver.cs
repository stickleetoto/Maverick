using System;
using System.Text;

namespace MaverickFresh.FlightDynamics.F15
{
    // WP-3B: a trim solver for the AFIT/Baumann/Davison RESEARCH model that solves the source's own
    // equilibrium problem. Research configuration only; never NASA 836.
    //
    // Why not MavSteadyFlightTrimSolver (audit: Docs/Reference/F15_RESEARCH_TRIM_SOLVER_V1.0.md §1):
    //   - it samples MavAtmosphereModel density from altitude; the source's RHO is one constant;
    //   - its powered mode imposes the flight-path angle and solves for thrust; the source fixes
    //     thrust at 8,300 lbf and lets the flight-path angle fall out of the equilibrium;
    //   - it refuses any thrust-line moment; the source's thrust has a 0.25-in arm;
    //   - it works in SI with g0 = 9.80665; the source works in ft, slug and lbf with G = 32.174.
    // Fitting the source to it would change the source. This plant wraps the shared
    // MavDampedNewtonSolver instead and duplicates none of its machinery.

    /// <summary>
    /// The research source's own environment, as its driver defines it (Baumann DTIC ADA217366 PDF
    /// pp.91, 95; Davison DTIC ADA256613 PDF pp.91, 124).
    ///
    /// Density is one constant. Nothing here samples an atmosphere model: the source has no
    /// altitude state, and 20,000 ft is only the label of the density it chose. True airspeed is the
    /// one variable, and dynamic pressure follows it alone.
    ///
    /// Isolated to research reproduction. The shared MavAtmosphereModel and MavSixDoFBody are not
    /// changed by, and do not read, this class.
    /// </summary>
    public static class MavF15AfitResearchSourceEnvironment
    {
        /// <summary>RHO = .0012673 slug/ft^3, "AIR DENSITY AT 20000 FT ALTITUDE", for every state.</summary>
        public const double DensitySlugPerFt3 = MavF15SourceExercisedOperatingDomain.SourceAirDensitySlugPerFt3;

        /// <summary>G = 32.174 ft/s^2.</summary>
        public const double GravityFtPerSec2 = MavF15SourceExercisedOperatingDomain.SourceGravityFtPerSec2;

        /// <summary>
        /// A LABEL for the density constant, not an input. No density, speed of sound or Mach is
        /// computed from it.
        /// </summary>
        public const double ReferenceAltitudeLabelFt = MavF15CoefficientFitCondition.PressureAltitudeFt;

        public const string DynamicPressureDefinition = MavF15SourceExercisedOperatingDomain.DynamicPressure;

        /// <summary>QBARS / SREF = 0.5 * RHO * V^2, with the fixed RHO. The only input is V.</summary>
        public static double DynamicPressurePsf(double trueAirspeedFtPerSec)
        {
            return 0.5 * DensitySlugPerFt3 * trueAirspeedFtPerSec * trueAirspeedFtPerSec;
        }
    }

    /// <summary>
    /// The three symmetric equilibrium equations of the research source at one state, with every
    /// term that enters them. Source units: ft, slug, lbf, s; body axes X forward, Z down.
    /// </summary>
    public struct MavF15ResearchSymmetricTrimResidual
    {
        public bool evaluated;
        public string reason;

        public double trueAirspeedFtPerSec;
        public double alphaDeg;
        public double symmetricStabilatorDeg;
        public double pitchAttitudeDeg;

        /// <summary>0.5 * RHO * V^2 from the fixed source density. Never from an atmosphere model.</summary>
        public double dynamicPressurePsf;

        /// <summary>The transcribed routine's own coefficients. The routine carries no thrust term.</summary>
        public double aeroCx;
        public double aeroCz;
        public double aeroCm;

        /// <summary>8,300 lbf total aircraft thrust along body +X, applied here and nowhere else.</summary>
        public double thrustForceLbf;

        /// <summary>8,300 lbf * 0.25/12 ft, nose-up, applied here and nowhere else.</summary>
        public double thrustPitchingMomentFtLbf;

        /// <summary>The thrust-line moment as the source adds it to CMM: THRUST*(0.25/12)/(QBARS*CWING).</summary>
        public double thrustPitchCoefficient;

        /// <summary>(q.S.CX + T - W sin theta) / W. Source F(1)/F(8) with q = r = beta = phi = 0.</summary>
        public double forceXOverWeight;

        /// <summary>(q.S.CZ + W cos theta) / W.</summary>
        public double forceZOverWeight;

        /// <summary>(q.S.cbar.CM + T.arm) / (q.S.cbar) = CM + thrust coefficient. Source F(4) with p = r = 0.</summary>
        public double pitchOverQSc;

        /// <summary>
        /// The lateral equations at the same symmetric state (beta = p = q = r = phi = 0, aileron,
        /// rudder and differential tail 0). They must vanish for the symmetric reduction to be the
        /// source's own equilibrium; they are reported, not driven.
        /// </summary>
        public double forceYOverWeight;
        public double rollOverQSb;
        public double yawOverQSb;

        public double DrivenNorm
        {
            get
            {
                return Math.Max(Math.Abs(forceXOverWeight),
                    Math.Max(Math.Abs(forceZOverWeight), Math.Abs(pitchOverQSc)));
            }
        }
    }

    /// <summary>A starting point for the symmetric solve. It is only a starting point.</summary>
    public struct MavF15ResearchSymmetricTrimGuess
    {
        public float alphaDeg;
        public float symmetricStabilatorDeg;
        public float pitchAttitudeDeg;

        public MavF15ResearchSymmetricTrimGuess(float alphaDeg, float symmetricStabilatorDeg, float pitchAttitudeDeg)
        {
            this.alphaDeg = alphaDeg;
            this.symmetricStabilatorDeg = symmetricStabilatorDeg;
            this.pitchAttitudeDeg = pitchAttitudeDeg;
        }
    }

    /// <summary>One symmetric research trim, as found. Research only; nothing here is a surface command.</summary>
    public struct MavF15ResearchSymmetricTrimResult
    {
        /// <summary>True when the solve was never attempted. <see cref="refusalReason"/> says why.</summary>
        public bool refused;
        public string refusalReason;

        public MavNewtonOutcome outcome;
        public bool converged;
        public int iterations;

        /// <summary>Largest of |X/W|, |Z/W|, |M/qSc| at the returned point.</summary>
        public float drivenResidualNorm;

        public double trueAirspeedFtPerSec;
        public MavF15ResearchSymmetricTrimGuess initialGuess;

        public float alphaDeg;
        public float symmetricStabilatorDeg;
        public float pitchAttitudeDeg;

        /// <summary>theta - alpha. Falls out of the equilibrium; never imposed.</summary>
        public float FlightPathAngleDeg
        {
            get { return pitchAttitudeDeg - alphaDeg; }
        }

        public MavF15ResearchSymmetricTrimResidual residual;

        /// <summary>
        /// The stabilator reached an edge of the research DEMONSTRATED range. A search bound, not a
        /// physical hard stop: nothing is known about where the surface stops.
        /// </summary>
        public bool stabilatorAtDemonstratedRangeEdge;
        public bool alphaAtSearchBound;
        public bool pitchAttitudeAtSearchBound;
    }

    /// <summary>
    /// Symmetric (wings-level, straight) trim of the AFIT/Baumann/Davison research model, solving the
    /// source's own equilibrium problem.
    ///
    /// SOURCE EQUATIONS (Baumann D2ICCV28 FUNX, DTIC ADA217366 PDF pp.95-96). The source continues
    /// in stabilator PAR(1) and solves all eight states. At a symmetric state (beta = p = r = phi = 0,
    /// aileron = rudder = differential tail = 0) the lateral equations F(2), F(3), F(5), F(7) vanish
    /// identically and F(6), theta-dot = q cos(phi) - r sin(phi), forces q = 0. What remains is
    ///   F(1) alpha-dot = 0 and F(8) V-dot = 0  - the body X and Z force balance, rotated by alpha;
    ///   F(4) q-dot = 0                         - the pitching moment, thrust-line moment included.
    /// Three equations in four quantities (alpha, theta, V, stabilator), so one of them is the
    /// parameter. Here it is V, as published; the unknowns are alpha, stabilator and theta. Theta
    /// is its own unknown: the source never sets theta = alpha, and the published symmetric points
    /// climb or descend (gamma = theta - alpha from +2.4 to -5.3 deg).
    ///
    /// SOURCE ENVIRONMENT: <see cref="MavF15AfitResearchSourceEnvironment"/> (fixed RHO, G = 32.174).
    /// THRUST: 8,300 lbf total along body +X plus 8,300 * 0.25/12 ft-lbf nose-up, both constant,
    /// both applied once, in <see cref="EvaluateSymmetricResidual"/>. No throttle, no required thrust,
    /// no engines, no MavF100 layer.
    /// MASS: 37,000 lb (<see cref="MavF15AfitResearchMassReference"/>). The symmetric equations need
    /// no inertia: with p = q = r = 0 every inertial coupling term is zero.
    /// CONTROLS: the stabilator enters only through a <see cref="MavF15ResearchStaticControlState"/>,
    /// and its search box is the research DEMONSTRATED range, never a hard stop.
    ///
    /// SCOPE: research reproduction only. Every result away from Mach 0.6 applies the Mach 0.6 fit
    /// at a speed the source exercised; that is source-exercised, not aerodynamically validated.
    /// Refused for any configuration id but the research one, and for V outside the
    /// source-exercised span. Turning trim is not solved here (WP-3C candidate).
    /// </summary>
    public static class MavF15AfitResearchTrimSolver
    {
        public const string Scope =
            "RESEARCH SOURCE REPRODUCTION ONLY - AFIT/Baumann/Davison model, fixed source density, "
            + "fixed 8,300 lbf total thrust; the M 0.6 fit applied at the given V (source-exercised, "
            + "NOT aerodynamically validated); NOT NASA 836";

        /// <summary>
        /// NUMERICAL termination epsilon of the Newton iteration, on the largest of |X/W|, |Z/W| and
        /// |M/qSc|. It says when to stop iterating. It is NOT a source-validation tolerance and says
        /// nothing about agreement with any published state.
        ///
        /// Set ten times above the float noise of the residual. The unknowns are float degrees and the
        /// routine returns float coefficients, and converged points sit at 1e-8 to 1e-7; at 1e-7 a
        /// start at V = 293 ft/s stalled on noise at 1.01e-7. At 1e-6 the termination error in the
        /// unknowns is about 2e-5 deg, far below the resolution Table VII is printed at.
        /// </summary>
        public const float NumericalSolverTolerance = 1e-6f;

        public const int MaxIterations = 60;
        public const int MaxLineSearchHalvings = 30;

        /// <summary>Central-difference step for every unknown, degrees.</summary>
        public const float FiniteDifferenceStepDeg = 1e-2f;

        /// <summary>
        /// Search box for theta. The source's Euler kinematics (STHE/CTHE in F(7)) are singular at
        /// +/-90 deg; the box stops short of that. A numerical bound, not a flight limit.
        /// </summary>
        public const float PitchAttitudeSearchLimitDeg = 89f;

        private const double DegToRad = Math.PI / 180.0;

        private const string StaticControlNote = "WP-3B research symmetric trim (static evaluation only)";

        /// <summary>Alpha search box: the transcribed routine's own breakpoint span. Not a validity claim.</summary>
        public static float AlphaSearchMinDeg
        {
            get { return MavF15BaumannMach06Domain.SourceAlphaMinDeg; }
        }

        public static float AlphaSearchMaxDeg
        {
            get { return MavF15BaumannMach06Domain.SourceAlphaMaxDeg; }
        }

        /// <summary>
        /// The stabilator search box: the research DEMONSTRATED range. What the source commanded in
        /// its tabulated solutions - never a physical hard stop, never an actuator limit.
        /// </summary>
        public static MavF15ResearchDemonstratedSurfaceRange StabilatorSearchRange()
        {
            return MavF15ResearchDemonstratedControlRange.AfitBaumannTabulatedEquilibria().symmetricStabilator;
        }

        /// <summary>
        /// The symmetric equilibrium equations at one state. Public so validation can compare them
        /// with the independent WP-3A static evaluator and check the thrust bookkeeping directly.
        /// </summary>
        public static MavF15ResearchSymmetricTrimResidual EvaluateSymmetricResidual(
            double trueAirspeedFtPerSec,
            double alphaDeg,
            double symmetricStabilatorDeg,
            double pitchAttitudeDeg)
        {
            MavF15ResearchSymmetricTrimResidual r = new MavF15ResearchSymmetricTrimResidual
            {
                trueAirspeedFtPerSec = trueAirspeedFtPerSec,
                alphaDeg = alphaDeg,
                symmetricStabilatorDeg = symmetricStabilatorDeg,
                pitchAttitudeDeg = pitchAttitudeDeg
            };

            MavF15ResearchStaticControlState controls;
            string reason;
            if (!MavF15ResearchStaticControlState.TryCreate(
                    MavF15ResearchDemonstratedControlRange.AfitBaumannTabulatedEquilibria(),
                    (float)symmetricStabilatorDeg, 0f, 0f, 0f, StaticControlNote,
                    out controls, out reason))
            {
                r.reason = reason;
                return r;
            }

            double S = MavF15BaumannMach06Reference.WingAreaFt2;
            double b = MavF15BaumannMach06Reference.WingSpanFt;
            double c = MavF15BaumannMach06Reference.MeanAerodynamicChordFt;
            double weight = MavF15AfitResearchMassReference.WeightLb;
            double thrust = MavF15AfitResearchThrustSource.SourceTotalThrustLbf;
            double thrustArmFt = MavF15AfitResearchThrustSource.SourceThrustLineOffsetIn / 12.0;

            double qbar = MavF15AfitResearchSourceEnvironment.DynamicPressurePsf(trueAirspeedFtPerSec);
            double qS = qbar * S;
            double alphaRad = alphaDeg * DegToRad;
            double theta = pitchAttitudeDeg * DegToRad;

            // Symmetric state: all body rates zero, so every rate-normalized term is zero too.
            MavF15BaumannSurfaceState surfaces = controls.ToBaumannSurfaceStateForStaticEvaluation();
            MavAeroCoefficients lon = MavF15BaumannMach06Longitudinal.Evaluate(
                (float)alphaRad, surfaces.symmetricStabilatorDeg, 0f);
            MavAeroCoefficients lat = MavF15BaumannMach06LateralDirectional.Evaluate(
                (float)alphaRad, 0f, surfaces, 0f, 0f);

            r.dynamicPressurePsf = qbar;
            r.aeroCx = lon.cx;
            r.aeroCz = lon.cz;
            r.aeroCm = lon.cm;
            r.thrustForceLbf = thrust;
            r.thrustPitchingMomentFtLbf = thrust * thrustArmFt;
            r.thrustPitchCoefficient = r.thrustPitchingMomentFtLbf / (qS * c);

            r.forceXOverWeight = (qS * lon.cx + r.thrustForceLbf - weight * Math.Sin(theta)) / weight;
            r.forceZOverWeight = (qS * lon.cz + weight * Math.Cos(theta)) / weight;
            r.pitchOverQSc = (qS * c * lon.cm + r.thrustPitchingMomentFtLbf) / (qS * c);

            r.forceYOverWeight = qS * lat.cy / weight;
            r.rollOverQSb = lat.cl;
            r.yawOverQSb = lat.cn;

            r.evaluated = IsFinite(r.forceXOverWeight) && IsFinite(r.forceZOverWeight) && IsFinite(r.pitchOverQSc);
            r.reason = r.evaluated ? "evaluated" : "non-finite residual";
            return r;
        }

        /// <summary>
        /// Solves the symmetric research equilibrium at a fixed true airspeed from one starting guess.
        ///
        /// Deterministic: the same arguments always give the same iterates. The answer comes from
        /// the equations alone; nothing here reads or accepts a published state.
        /// </summary>
        public static MavF15ResearchSymmetricTrimResult SolveSymmetric(
            string configurationId,
            double trueAirspeedFtPerSec,
            MavF15ResearchSymmetricTrimGuess initialGuess)
        {
            MavF15ResearchSymmetricTrimResult result = new MavF15ResearchSymmetricTrimResult
            {
                trueAirspeedFtPerSec = trueAirspeedFtPerSec,
                initialGuess = initialGuess,
                outcome = MavNewtonOutcome.NonFiniteResidual,
                alphaDeg = float.NaN,
                symmetricStabilatorDeg = float.NaN,
                pitchAttitudeDeg = float.NaN,
                drivenResidualNorm = float.PositiveInfinity
            };

            string refusal = Refusal(configurationId, trueAirspeedFtPerSec, initialGuess);
            if (refusal != null)
            {
                result.refused = true;
                result.refusalReason = refusal;
                return result;
            }

            MavF15ResearchDemonstratedSurfaceRange stabilator = StabilatorSearchRange();

            float[] unknowns = { initialGuess.alphaDeg, initialGuess.symmetricStabilatorDeg, initialGuess.pitchAttitudeDeg };
            float[] lower = { AlphaSearchMinDeg, stabilator.minDeg, -PitchAttitudeSearchLimitDeg };
            float[] upper = { AlphaSearchMaxDeg, stabilator.maxDeg, PitchAttitudeSearchLimitDeg };
            float[] steps = { FiniteDifferenceStepDeg, FiniteDifferenceStepDeg, FiniteDifferenceStepDeg };

            MavNewtonResidualFunction residualFunction = delegate (float[] x, float[] res)
            {
                MavF15ResearchSymmetricTrimResidual e =
                    EvaluateSymmetricResidual(trueAirspeedFtPerSec, x[0], x[1], x[2]);
                if (!e.evaluated)
                {
                    res[0] = res[1] = res[2] = float.NaN;
                    return;
                }

                res[0] = (float)e.forceXOverWeight;
                res[1] = (float)e.forceZOverWeight;
                res[2] = (float)e.pitchOverQSc;
            };

            int iterations;
            float norm;
            result.outcome = MavDampedNewtonSolver.Solve(
                3, residualFunction, unknowns, lower, upper, steps,
                MaxIterations, NumericalSolverTolerance, MaxLineSearchHalvings,
                out iterations, out norm);

            result.converged = result.outcome == MavNewtonOutcome.Converged;
            result.iterations = iterations;
            result.drivenResidualNorm = norm;
            result.alphaDeg = unknowns[0];
            result.symmetricStabilatorDeg = unknowns[1];
            result.pitchAttitudeDeg = unknowns[2];
            result.residual = EvaluateSymmetricResidual(trueAirspeedFtPerSec, unknowns[0], unknowns[1], unknowns[2]);
            result.alphaAtSearchBound = AtBound(unknowns[0], lower[0], upper[0]);
            result.stabilatorAtDemonstratedRangeEdge = AtBound(unknowns[1], lower[1], upper[1]);
            result.pitchAttitudeAtSearchBound = AtBound(unknowns[2], lower[2], upper[2]);
            return result;
        }

        public static string Describe(MavF15ResearchSymmetricTrimResult r)
        {
            StringBuilder s = new StringBuilder(256);
            s.Append("research symmetric trim V=").Append(r.trueAirspeedFtPerSec.ToString("F1")).Append(" ft/s: ");
            if (r.refused)
                return s.Append("REFUSED - ").Append(r.refusalReason).ToString();

            s.Append(r.outcome)
             .Append(" in ").Append(r.iterations).Append(" it | alpha ").Append(r.alphaDeg.ToString("F5"))
             .Append(" stab ").Append(r.symmetricStabilatorDeg.ToString("F5"))
             .Append(" theta ").Append(r.pitchAttitudeDeg.ToString("F5"))
             .Append(" gamma ").Append(r.FlightPathAngleDeg.ToString("F4"))
             .Append(" deg | norm ").Append(r.drivenResidualNorm.ToString("E2"));
            if (r.stabilatorAtDemonstratedRangeEdge)
                s.Append(" | stabilator at demonstrated-range edge (NOT a hard stop)");
            if (r.alphaAtSearchBound || r.pitchAttitudeAtSearchBound)
                s.Append(" | at search bound");
            return s.ToString();
        }

        private static string Refusal(
            string configurationId, double trueAirspeedFtPerSec, MavF15ResearchSymmetricTrimGuess guess)
        {
            if (configurationId != MavF15AfitResearchIdentity.ConfigurationId)
            {
                return "research trim is for " + MavF15AfitResearchIdentity.ConfigurationId
                       + " only; '" + configurationId + "' has no research trim";
            }

            if (!IsFinite(trueAirspeedFtPerSec)
                || trueAirspeedFtPerSec < MavF15SourceExercisedOperatingDomain.MinTabulatedTrueAirspeedFtPerSec
                || trueAirspeedFtPerSec > MavF15SourceExercisedOperatingDomain.MaxTabulatedTrueAirspeedFtPerSec)
            {
                return "V " + trueAirspeedFtPerSec.ToString("F1") + " ft/s is outside the source-exercised "
                       + MavF15SourceExercisedOperatingDomain.MinTabulatedTrueAirspeedFtPerSec.ToString("F1") + "-"
                       + MavF15SourceExercisedOperatingDomain.MaxTabulatedTrueAirspeedFtPerSec.ToString("F1")
                       + " ft/s; no extrapolation beyond what the source ran";
            }

            if (!IsFinite(guess.alphaDeg) || !IsFinite(guess.symmetricStabilatorDeg) || !IsFinite(guess.pitchAttitudeDeg))
                return "initial guess is not finite";

            if (!StabilatorSearchRange().declared)
                return "no research demonstrated stabilator range is declared";

            return null;
        }

        private static bool AtBound(float value, float lower, float upper)
        {
            const float epsilon = 1e-4f;
            return value <= lower + epsilon || value >= upper - epsilon;
        }

        private static bool IsFinite(double v)
        {
            return !double.IsNaN(v) && !double.IsInfinity(v);
        }
    }
}
