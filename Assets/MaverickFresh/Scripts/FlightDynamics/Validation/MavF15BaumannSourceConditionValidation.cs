using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using MaverickFresh.FlightDynamics.F15;
using Object = UnityEngine.Object;

namespace MaverickFresh.FlightDynamics.Validation
{
    /// <summary>
    /// WP-3A: what "Mach 0.6 / 20,000 ft" means for the research model, and Baumann's Table VII
    /// reproduced statically.
    ///
    ///   [W1]  coefficient-fit condition, source-exercised domain and physical validity are distinct
    ///   [W2]  Table VII is stored exactly as printed
    ///   [W3]  the two printed halves pair as demonstrated, by kinematics alone
    ///   [W4]  every assembled equilibrium yields finite residuals, reported against print precision
    ///   [W5]  the research stabilator sign stays the source's, across all of Table VII
    ///   [W6]  the static equilibrium control never becomes actuator authority
    ///   [W7]  nothing outside Validation/ can reach Table VII
    ///   [W8]  SourceReproduction is research-only
    ///   [W9]  the strict Mach 0.6 mode still exists and is the default
    ///   [W10] SourceReproduction admits no unrestricted Mach extrapolation
    ///   [W11] 8,300 lbf stays total-aircraft thrust in both modes
    ///
    /// No residual carries a pass threshold. [W4] asserts finiteness and reports the rest.
    /// </summary>
    public static class MavF15BaumannSourceConditionValidation
    {
        private const double FtToM = 0.3048;

        public static string RunAll(out int passed, out int failed)
        {
            passed = 0;
            failed = 0;
            StringBuilder report = new StringBuilder(16384);
            report.AppendLine("F-15 Research Source Condition and Table VII Static Reproduction");
            report.AppendLine("================================================================");
            report.AppendLine("Research configuration only. NOT NASA 836. No residual pass threshold.");

            ValidateThreeConcepts(report, ref passed, ref failed);
            ValidateTableStoredExactly(report, ref passed, ref failed);
            ValidatePairing(report, ref passed, ref failed);
            ValidateResiduals(report, ref passed, ref failed);
            ValidateSign(report, ref passed, ref failed);
            ValidateStaticControlIsNotAuthority(report, ref passed, ref failed);
            ValidateIsolation(report, ref passed, ref failed);
            ValidateResearchOnlyMode(report, ref passed, ref failed);
            ValidateStrictDefault(report, ref passed, ref failed);
            ValidateNoUnrestrictedExtrapolation(report, ref passed, ref failed);
            ValidateTotalThrust(report, ref passed, ref failed);

            report.AppendLine();
            report.Append("RESULT: ")
                .Append(failed == 0 ? "PASS" : "FAIL")
                .Append("  passed=").Append(passed)
                .Append(" failed=").Append(failed);
            return report.ToString();
        }

        // ---------------------------------------------------------------- [W1]

        private static void ValidateThreeConcepts(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[W1] Fit condition, source-exercised domain and physical validity are distinct");

            Record(MavF15CoefficientFitCondition.Mach == 0.6f
                   && MavF15CoefficientFitCondition.PressureAltitudeFt == 20000f
                   && MavF15CoefficientFitCondition.Provenance.IndexOf("fitting condition") >= 0,
                "coefficient-fit condition: Mach 0.6 / 20,000 ft, labelled as the fitting condition",
                report, ref passed, ref failed);

            Record(MavF15SourceExercisedOperatingDomain.TrueAirspeedIsAState
                   && !MavF15SourceExercisedOperatingDomain.AltitudeIsAState
                   && !MavF15SourceExercisedOperatingDomain.MachIsComputed
                   && MavF15SourceExercisedOperatingDomain.SourceAirDensitySlugPerFt3 == 0.0012673,
                "source-exercised domain: V varies as a state; density is the fixed 20,000-ft constant; "
                + "no altitude state; Mach never computed",
                report, ref passed, ref failed);

            float a = MavAtmosphereModel.Sample(MavF15CoefficientFitCondition.PressureAltitudeM).speedOfSoundMps;
            float exercisedMach = (float)(400.0 * FtToM) / a;
            Record(MavF15SourceExercisedOperatingDomain.ContainsTrueAirspeed((float)(400.0 * FtToM))
                   && !MavF15PhysicalValidityEnvelope.IsAerodynamicallyValidated(
                       exercisedMach, MavF15CoefficientFitCondition.PressureAltitudeM)
                   && MavF15PhysicalValidityEnvelope.IsAerodynamicallyValidated(
                       0.6f, MavF15CoefficientFitCondition.PressureAltitudeM),
                "400 ft/s (Mach " + exercisedMach.ToString("F3") + ") is source-exercised but NOT "
                + "aerodynamically validated; only the fit condition is",
                report, ref passed, ref failed);

            Record(MavF15PhysicalValidityEnvelope.AuthorClaims.IndexOf("only valid at M=0.6") >= 0,
                "the physical-validity envelope carries Davison's 'only valid at M=0.6' and Baumann's "
                + "incompressibility claim",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [W2]

        private static void ValidateTableStoredExactly(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[W2] Table VII stored exactly as printed");

            ReadOnlyCollection<MavF15TableViiPart1Row> p1 = MavF15BaumannTableVii.Part1;
            ReadOnlyCollection<MavF15TableViiPart2Row> p2 = MavF15BaumannTableVii.Part2;

            bool numbering = p1.Count == 201 && p2.Count == 201;
            for (int i = 0; i < 201 && numbering; i++)
                numbering = p1[i].point == i + 1 && p2[i].point == i + 1;
            Record(numbering, "201 printed points in each half, numbered 1-201 in order",
                report, ref passed, ref failed);

            // Printed values read off the rendered pages (PDF pp.124, 125, 126, 128, 129, 131, 133).
            Record(p1[0].stabilatorDeg == -9.923545 && p1[0].alphaDeg == 13.43360
                   && p1[0].betaDeg == -3.286e-19 && p1[0].rollRateRadSec == -1.873e-21
                   && p1[45].stabilatorDeg == -17.30722 && p1[45].alphaDeg == 19.18532
                   && p1[116].stabilatorDeg == -5.717336 && p1[116].alphaDeg == 8.300828
                   && p1[200].stabilatorDeg == -5.275754 && p1[200].alphaDeg == 7.875753
                   && p1[200].betaDeg == 1.759e-02 && p1[200].rollRateRadSec == -5.239e-04,
                "first half: points 1, 46, 117 and 201 hold their printed values",
                report, ref passed, ref failed);

            Record(p2[0].pitchAngleDeg == 15.38 && p2[0].trueVelocityKftPerSec == 0.3384
                   && p2[91].pitchRateRadSec == 4.025e-02 && p2[91].yawRateRadSec == 4.649e-02
                   && p2[91].bankAngleDeg == 41.50 && p2[117].trueVelocityKftPerSec == 0.6997
                   && p2[200].pitchRateRadSec == 9.330e-02 && p2[200].bankAngleDeg == 63.02,
                "second half: points 1, 92, 118 and 201 hold their printed values",
                report, ref passed, ref failed);

            int nan = 0;
            bool nanNoted = true;
            for (int i = 0; i < 201; i++)
            {
                bool n1 = double.IsNaN(p1[i].stabilatorDeg) || double.IsNaN(p1[i].alphaDeg)
                          || double.IsNaN(p1[i].betaDeg) || double.IsNaN(p1[i].rollRateRadSec);
                bool n2 = double.IsNaN(p2[i].pitchRateRadSec) || double.IsNaN(p2[i].yawRateRadSec)
                          || double.IsNaN(p2[i].pitchAngleDeg) || double.IsNaN(p2[i].bankAngleDeg)
                          || double.IsNaN(p2[i].trueVelocityKftPerSec);
                if (n1) { nan++; if (p1[i].note.IndexOf("illegible") < 0) nanNoted = false; }
                if (n2) { nan++; if (p2[i].note.IndexOf("illegible") < 0) nanNoted = false; }
            }

            Record(nan == 4 && nanNoted
                   && double.IsNaN(p1[52].rollRateRadSec) && double.IsNaN(p1[53].rollRateRadSec)
                   && double.IsNaN(p1[174].betaDeg) && double.IsNaN(p2[195].yawRateRadSec),
                "exactly four fields are illegible in the scan (points 53, 54, 175 first half; 196 "
                + "second half); each is NaN with a note, never guessed",
                report, ref passed, ref failed);

            Record(p1[39].alphaDeg == p1[89].alphaDeg && p1[39].note.IndexOf("row 90") >= 0,
                "point 40's scan-damaged alpha is the value point 90 prints for the same equilibrium, "
                + "and the note says so",
                report, ref passed, ref failed);

            double vMin = double.MaxValue, vMax = 0, dMin = 0, dMax = -100;
            for (int i = 0; i < 201; i++)
            {
                vMin = Math.Min(vMin, p2[i].trueVelocityKftPerSec * 1000.0);
                vMax = Math.Max(vMax, p2[i].trueVelocityKftPerSec * 1000.0);
                dMin = Math.Min(dMin, p1[i].stabilatorDeg);
                dMax = Math.Max(dMax, p1[i].stabilatorDeg);
            }

            report.Append("    printed V ").Append(vMin.ToString("F1")).Append("-").Append(vMax.ToString("F1"))
                  .Append(" ft/s; stabilator ").Append(dMin.ToString("F5")).Append(" to ")
                  .Append(dMax.ToString("F6")).AppendLine(" deg");
            Record(Math.Abs(vMin - 288.7) < 1e-9 && Math.Abs(vMax - 699.7) < 1e-9
                   && dMin == -17.30722 && dMax == -5.275754
                   && MavF15BaumannTableVii.CaptionThrustLbf == 8300.0
                   && MavF15BaumannTableVii.CaptionAltitudeFt == 20000.0,
                "ranges as printed (V 288.7-699.7 ft/s, stabilator -17.30722 to -5.275754 deg); caption "
                + "thrust 8,300 lb, altitude 20,000 ft",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [W3]

        private static void ValidatePairing(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[W3] The two printed halves pair as demonstrated (kinematics only)");

            List<MavF15TableViiState> states = MavF15BaumannTableVii.PairedStates();
            int sym = 0, turn = 0;
            for (int i = 0; i < states.Count; i++)
            {
                if (states[i].pairing == MavF15TableViiPairing.SymmetricSameLabel) sym++; else turn++;
            }

            report.Append("    assembled ").Append(states.Count).Append(" states: ").Append(sym)
                  .Append(" symmetric, ").Append(turn).AppendLine(" turning");
            Record(sym == 89 && turn == 81,
                "89 symmetric states (points 1-91 less 53, 54) and 81 turning states (117-199 less "
                + "175 and 194, whose displaced columns include an illegible field)",
                report, ref passed, ref failed);

            int better = 0, compared = 0;
            for (int i = 0; i < states.Count; i++)
            {
                MavF15TableViiState s = states[i];
                if (s.pairing != MavF15TableViiPairing.TurningDisplacedColumns)
                    continue;

                MavF15TableViiPart2Row same = MavF15BaumannTableVii.Part2[s.part1Point - 1];
                if (double.IsNaN(same.yawRateRadSec))
                    continue;

                double chosen = PhiDot(s.pRadSec, s.qRadSec, s.rRadSec, s.thetaDeg, s.phiDeg);
                double alt = PhiDot(s.pRadSec, s.qRadSec, same.yawRateRadSec, same.pitchAngleDeg, same.bankAngleDeg);
                compared++;
                if (Math.Abs(chosen) < Math.Abs(alt))
                    better++;
            }

            report.Append("    displaced-column pairing closes phi-dot better than same-label pairing in ")
                  .Append(better).Append(" of ").Append(compared).AppendLine(" turning states");
            Record(compared > 0 && better == compared,
                "for every turning state, the demonstrated pairing satisfies phi-dot = 0 better than "
                + "same-number pairing - no aerodynamics involved",
                report, ref passed, ref failed);

            bool reasons = true;
            for (int i = 92; i <= 116; i++)
                if (MavF15BaumannTableVii.UnassembledReason(i) == null) reasons = false;
            Record(reasons && MavF15BaumannTableVii.UnassembledReason(200) != null
                   && MavF15BaumannTableVii.UnassembledReason(201) != null
                   && MavF15BaumannTableVii.UnassembledReason(1) == null,
                "points 92-116, 200 and 201 are printed but not assembled, each with its reason",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [W4]

        private static void ValidateResiduals(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[W4] Static equilibrium residuals (reported; no threshold)");

            string[] names = { "X/W", "Y/W", "Z/W", "L/qSb", "M/qScbar", "N/qSb", "theta-dot", "phi-dot" };
            List<MavF15TableViiState> states = MavF15BaumannTableVii.PairedStates();
            bool allFinite = true;
            int evaluated = 0;

            foreach (MavF15TableViiPairing pairing in new[] {
                         MavF15TableViiPairing.SymmetricSameLabel, MavF15TableViiPairing.TurningDisplacedColumns })
            {
                double[] maxAbs = new double[8], maxRatio = new double[8];
                int[] over = new int[8];
                int n = 0;
                double mMin = double.MaxValue, mMax = 0;

                for (int i = 0; i < states.Count; i++)
                {
                    if (states[i].pairing != pairing)
                        continue;

                    MavF15EquilibriumResidual r = MavF15TableViiEquilibriumReproduction.Evaluate(states[i]);
                    if (!r.evaluated)
                    {
                        allFinite = false;
                        continue;
                    }

                    n++;
                    evaluated++;
                    mMin = Math.Min(mMin, r.impliedMachAt20000Ft);
                    mMax = Math.Max(mMax, r.impliedMachAt20000Ft);
                    double[] res = { r.forceXOverWeight, r.forceYOverWeight, r.forceZOverWeight, r.rollOverQSb,
                                     r.pitchOverQSc, r.yawOverQSb, r.thetaDotRadSec, r.phiDotRadSec };
                    double[] floor = { r.floorForceX, r.floorForceY, r.floorForceZ, r.floorRoll,
                                       r.floorPitch, r.floorYaw, r.floorThetaDot, r.floorPhiDot };
                    for (int k = 0; k < 8; k++)
                    {
                        if (double.IsNaN(res[k]) || double.IsInfinity(res[k]))
                            allFinite = false;
                        maxAbs[k] = Math.Max(maxAbs[k], Math.Abs(res[k]));
                        if (floor[k] > 0)
                            maxRatio[k] = Math.Max(maxRatio[k], Math.Abs(res[k]) / floor[k]);
                        if (Math.Abs(res[k]) > floor[k])
                            over[k]++;
                    }
                }

                report.Append("    ").Append(pairing).Append(": ").Append(n).Append(" states, implied Mach ")
                      .Append(mMin.ToString("F3")).Append("-").Append(mMax.ToString("F3")).AppendLine();
                for (int k = 0; k < 8; k++)
                {
                    report.Append("      ").Append(names[k].PadRight(10)).Append(" max|res| ")
                          .Append(maxAbs[k].ToString("E2")).Append("  max res/print-floor ")
                          .Append(maxRatio[k].ToString("F2")).Append("  states above floor ")
                          .Append(over[k]).AppendLine();
                }

                if (pairing == MavF15TableViiPairing.SymmetricSameLabel)
                {
                    report.AppendLine("      (symmetric states: Y, L, N and theta-dot are ~1e-22 or smaller - numerically "
                                      + "zero. Their floors are smaller still because the printed lateral values are "
                                      + "themselves ~1e-19 continuation noise, so those ratios carry no information.)");
                }
            }

            Record(evaluated == states.Count && allFinite,
                "all " + evaluated + " assembled equilibria evaluated with finite residuals in six axes "
                + "and both kinematic equations",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [W5]

        private static void ValidateSign(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[W5] Research stabilator sign across all of Table VII");

            List<MavF15TableViiState> states = MavF15BaumannTableVii.PairedStates();
            int better = 0, n = 0;
            for (int i = 0; i < states.Count; i++)
            {
                MavF15TableViiState s = states[i];
                float alphaRad = (float)(s.alphaDeg * Math.PI / 180.0);
                double qHat = s.qRadSec * MavF15BaumannMach06Reference.MeanAerodynamicChordFt
                              / (2.0 * s.trueVelocityFtPerSec);
                double source = MavF15BaumannMach06Longitudinal.Evaluate(alphaRad, (float)s.stabilatorDeg, (float)qHat).cm;
                double flipped = MavF15BaumannMach06Longitudinal.Evaluate(alphaRad, (float)-s.stabilatorDeg, (float)qHat).cm;
                MavF15EquilibriumResidual r = MavF15TableViiEquilibriumReproduction.Evaluate(s);
                double withFlip = r.pitchOverQSc + (flipped - source);
                n++;
                if (Math.Abs(r.pitchOverQSc) < Math.Abs(withFlip))
                    better++;
            }

            Record(n > 0 && better == n,
                "flipping the stabilator sign worsens the pitch-moment residual at all " + n + " states",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [W6]

        private static void ValidateStaticControlIsNotAuthority(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[W6] The static equilibrium control never becomes actuator authority");

            Type[] authority =
            {
                typeof(MavF15SurfaceLimits), typeof(MavF15SurfaceChannelLimits), typeof(MavF15PhysicalSurfaceHardStops),
                typeof(MavF15ActuatorRateLimits), typeof(MavControlSurfaceLimits), typeof(MavF15SurfaceState),
                typeof(MavF15ActualSurfaceState), typeof(MavF15RequestedSurfaceState)
            };
            bool clean = true;
            foreach (MethodInfo m in typeof(MavF15ResearchStaticControlState).GetMethods(
                         BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static
                         | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (Array.IndexOf(authority, m.ReturnType) >= 0)
                    clean = false;
            }

            GameObject host = new GameObject("MavF15W6Profile");
            host.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                MavF15AfitResearchFlightDynamicsProfile profile =
                    host.AddComponent<MavF15AfitResearchFlightDynamicsProfile>();
                profile.conditionMode = MavF15ResearchConditionMode.SourceReproduction;
                MavFlightDynamicsProfile built = profile.BuildProfile();
                Record(clean && built.controlSurfaceLimits.Equals(new MavControlSurfaceLimits())
                       && MavF15PhysicalSurfaceHardStops.ExactTarget().AnyDeclared == false,
                    "the static control converts to no limit or surface state; the research profile "
                    + "in SourceReproduction mode still declares zero surface travel",
                    report, ref passed, ref failed);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        // ---------------------------------------------------------------- [W7]

        private static void ValidateIsolation(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[W7] Nothing outside Validation/ can reach Table VII");

            string[] names =
            {
                "MavF15BaumannTableVii", "MavF15TableViiState", "MavF15TableViiPart",
                "MavF15TableViiEquilibriumReproduction", "MavF15EquilibriumResidual"
            };
            string root = ResolveFlightDynamicsRoot();
            if (root == null)
            {
                Record(false, "flight-dynamics source root not found", report, ref passed, ref failed);
                return;
            }

            string validation = Path.GetFullPath(Path.Combine(root, "Validation")) + Path.DirectorySeparatorChar;
            string offender = null;
            int scanned = 0;
            foreach (string f in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string full = Path.GetFullPath(f);
                if (full.StartsWith(validation, StringComparison.OrdinalIgnoreCase))
                    continue;
                scanned++;
                string text = File.ReadAllText(full);
                for (int i = 0; i < names.Length; i++)
                    if (text.IndexOf(names[i], StringComparison.Ordinal) >= 0)
                        offender = Path.GetFileName(full) + " names " + names[i];
            }

            Record(scanned > 0 && offender == null,
                offender == null
                    ? "none of " + scanned + " non-Validation sources (exact 836 path included) names Table VII"
                    : "VIOLATION: " + offender,
                report, ref passed, ref failed);

            Record(MavF15ReferenceData.CreateExactTargetGeometry().wingAreaM2 == 0f
                   && ExactAeroRefusesInSourceReproduction(),
                "the exact NASA 836 path is unchanged: S = 0, and exact aero refuses in every mode",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [W8]

        private static void ValidateResearchOnlyMode(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[W8] SourceReproduction is research-only");

            MavFlightState state = State(400.0, 5f);
            MavAtmosphereSample atm = MavAtmosphereModel.Sample(MavF15CoefficientFitCondition.PressureAltitudeM);

            GameObject a = new GameObject("MavF15W8NoProfile");
            GameObject b = new GameObject("MavF15W8ExactBody");
            GameObject c = new GameObject("MavF15W8ResearchBody");
            a.hideFlags = b.hideFlags = c.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                MavF15AeroModel aeroA = ResearchAero(a);
                aeroA.Evaluate(state, default(MavControlInput), atm);
                Record(aeroA.debugRefused && aeroA.debugConditionMode == MavF15ResearchConditionMode.StrictFitCondition,
                    "no research profile: strict, and 400 ft/s is refused",
                    report, ref passed, ref failed);

                MavF15AfitResearchFlightDynamicsProfile rb = b.AddComponent<MavF15AfitResearchFlightDynamicsProfile>();
                rb.conditionMode = MavF15ResearchConditionMode.SourceReproduction;
                MavF15FlightDynamicsProfile exact = b.AddComponent<MavF15FlightDynamicsProfile>();
                MavSixDoFBody bodyB = b.AddComponent<MavSixDoFBody>();
                bodyB.profileProvider = exact;
                MavF15AeroModel aeroB = ResearchAero(b);
                aeroB.Evaluate(state, default(MavControlInput), atm);
                Record(aeroB.debugRefused && aeroB.debugConditionMode == MavF15ResearchConditionMode.StrictFitCondition,
                    "research profile asks for SourceReproduction but the body flies the exact 836 profile: "
                    + "strict, refused",
                    report, ref passed, ref failed);

                MavF15AfitResearchFlightDynamicsProfile rc = c.AddComponent<MavF15AfitResearchFlightDynamicsProfile>();
                rc.conditionMode = MavF15ResearchConditionMode.SourceReproduction;
                MavSixDoFBody bodyC = c.AddComponent<MavSixDoFBody>();
                bodyC.profileProvider = rc;
                MavF15AeroModel aeroC = ResearchAero(c);
                MavAeroCoefficients coeffs = aeroC.Evaluate(state, default(MavControlInput), atm);
                Record(!aeroC.debugRefused
                       && aeroC.debugConditionMode == MavF15ResearchConditionMode.SourceReproduction
                       && aeroC.debugExtrapolatedFromFitCondition && !aeroC.debugAtFixedSourceCondition
                       && aeroC.debugStatus.IndexOf("EXTRAPOLATED") >= 0
                       && aeroC.debugStatus.IndexOf("NOT aerodynamically validated") >= 0
                       && !float.IsNaN(coeffs.cm),
                    "research profile flown by the body: 400 ft/s admitted, flagged EXTRAPOLATED from the "
                    + "Mach 0.6 fit and NOT aerodynamically validated",
                    report, ref passed, ref failed);

                report.Append("    status: ").AppendLine(aeroC.debugStatus);
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
                Object.DestroyImmediate(c);
            }
        }

        // ---------------------------------------------------------------- [W9]

        private static void ValidateStrictDefault(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[W9] Strict Mach 0.6 mode exists and is the default");

            MavAtmosphereSample atm = MavAtmosphereModel.Sample(MavF15CoefficientFitCondition.PressureAltitudeM);
            bool inFit;
            string reason;
            bool strictAtFit = MavF15ResearchConditionGate.Admits(
                MavF15ResearchConditionMode.StrictFitCondition, State(0.6 * atm.speedOfSoundMps / FtToM, 5f, 0.6f),
                atm, out inFit, out reason);
            bool strictOff = MavF15ResearchConditionGate.Admits(
                MavF15ResearchConditionMode.StrictFitCondition, State(400.0, 5f), atm, out inFit, out reason);

            GameObject host = new GameObject("MavF15W9");
            host.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                MavF15AfitResearchFlightDynamicsProfile p = host.AddComponent<MavF15AfitResearchFlightDynamicsProfile>();
                MavFlightDynamicsEnvelope env = MavF15AfitResearchFlightDynamicsProfile.CreateResearchEnvelope();
                Record(p.conditionMode == MavF15ResearchConditionMode.StrictFitCondition
                       && strictAtFit && !strictOff
                       && Mathf.Abs(env.minMach - 0.599f) < 1e-6f && Mathf.Abs(env.maxMach - 0.601f) < 1e-6f
                       && MavF15BaumannMach06Reference.NumericalMachTolerance == 0.001f,
                    "a new research profile is strict; strict admits Mach 0.6 and refuses 400 ft/s; the "
                    + "default envelope is still Mach 0.599-0.601",
                    report, ref passed, ref failed);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        // ---------------------------------------------------------------- [W10]

        private static void ValidateNoUnrestrictedExtrapolation(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[W10] No unrestricted Mach extrapolation");

            MavAtmosphereSample atm = MavAtmosphereModel.Sample(MavF15CoefficientFitCondition.PressureAltitudeM);
            MavAtmosphereSample high = MavAtmosphereModel.Sample(7000f);
            MavF15ResearchConditionMode src = MavF15ResearchConditionMode.SourceReproduction;
            bool inFit;
            string reason;

            bool lowRefused = !MavF15ResearchConditionGate.Admits(src, State(200.0, 5f), atm, out inFit, out reason);
            bool highRefused = !MavF15ResearchConditionGate.Admits(src, State(720.0, 5f), atm, out inFit, out reason);
            bool altRefused = !MavF15ResearchConditionGate.Admits(src, State(400.0, 5f), high, out inFit, out reason);
            bool minAdmitted = MavF15ResearchConditionGate.Admits(src, State(218.6, 5f), atm, out inFit, out reason);
            bool maxAdmitted = MavF15ResearchConditionGate.Admits(src, State(699.6, 5f), atm, out inFit, out reason);

            Record(lowRefused && highRefused && altRefused && minAdmitted && maxAdmitted,
                "SourceReproduction admits 218.5-699.7 ft/s at the fixed-density altitude only; 200 ft/s, "
                + "720 ft/s and 7,000 m are refused",
                report, ref passed, ref failed);

            MavFlightDynamicsEnvelope env = MavF15AfitResearchFlightDynamicsProfile.CreateResearchEnvelope(src);
            float a = atm.speedOfSoundMps;
            Record(Mathf.Abs(env.minMach - MavF15SourceExercisedOperatingDomain.MinTabulatedTrueAirspeedMps / a) < 1e-6f
                   && Mathf.Abs(env.maxMach - MavF15SourceExercisedOperatingDomain.MaxTabulatedTrueAirspeedMps / a) < 1e-6f
                   && env.alphaMinDeg == MavF15BaumannMach06Domain.SourceAlphaMinDeg
                   && env.alphaMaxDeg == MavF15BaumannMach06Domain.SourceAlphaMaxDeg,
                "the SourceReproduction envelope widens only Mach, to Mach " + env.minMach.ToString("F4")
                + "-" + env.maxMach.ToString("F4") + " (the source-exercised speeds); alpha/beta unchanged",
                report, ref passed, ref failed);

            double sourceRhoKgM3 = MavF15SourceExercisedOperatingDomain.SourceAirDensitySlugPerFt3 * 515.378818;
            report.Append("    density at 6,096 m: Maverick ").Append(atm.densityKgM3.ToString("F5"))
                  .Append(" kg/m^3, source RHO ").Append(sourceRhoKgM3.ToString("F5"))
                  .Append(" kg/m^3 (relative difference ")
                  .Append(((atm.densityKgM3 - sourceRhoKgM3) / sourceRhoKgM3).ToString("E2")).AppendLine(")");
            Record(!float.IsNaN(atm.densityKgM3) && atm.densityKgM3 > 0f,
                "Maverick's density at the fixed altitude is finite and reported against the source constant",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [W11]

        private static void ValidateTotalThrust(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[W11] 8,300 lbf stays total-aircraft thrust");

            MavAtmosphereSample atm = MavAtmosphereModel.Sample(MavF15CoefficientFitCondition.PressureAltitudeM);
            MavPropulsiveLoads strict, src, refused, exact;
            string reason;
            bool okStrict = MavF15AfitResearchThrustSource.TryEvaluate(MavF15AfitResearchIdentity.ConfigurationId,
                MavF15ResearchConditionMode.StrictFitCondition, State(0.6 * atm.speedOfSoundMps / FtToM, 5f, 0.6f),
                atm, out strict, out reason);
            bool okSrc = MavF15AfitResearchThrustSource.TryEvaluate(MavF15AfitResearchIdentity.ConfigurationId,
                MavF15ResearchConditionMode.SourceReproduction, State(400.0, 5f), atm, out src, out reason);
            bool noOutside = !MavF15AfitResearchThrustSource.TryEvaluate(MavF15AfitResearchIdentity.ConfigurationId,
                MavF15ResearchConditionMode.SourceReproduction, State(720.0, 5f), atm, out refused, out reason);
            bool noExact = !MavF15AfitResearchThrustSource.TryEvaluate(MavF15ReferenceData.TargetConfigurationId,
                MavF15ResearchConditionMode.SourceReproduction, State(400.0, 5f), atm, out exact, out reason);

            Record(okStrict && okSrc && noOutside && noExact
                   && MavF15AfitResearchThrustSource.SourceTotalThrustLbf == 8300f
                   && src.forceAeroBodyN == strict.forceAeroBodyN && src.momentAeroBodyNm == strict.momentAeroBodyNm
                   && src.contributingEngineCount == 0 && !src.hasAuthoritativeData
                   && Mathf.Abs(src.forceAeroBodyN.x - MavF15AfitResearchThrustSource.TotalThrustN) < 1e-3f,
                "one 8,300 lbf total force (no engines) with the same thrust-line moment in both modes; "
                + "refused outside the domain and under the exact 836 id",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>
        /// The exact source mode refuses even on a body flying the research profile in
        /// SourceReproduction mode: the condition mode never reaches the exact path.
        /// </summary>
        private static bool ExactAeroRefusesInSourceReproduction()
        {
            GameObject g = new GameObject("MavF15W7Exact");
            g.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                MavF15AfitResearchFlightDynamicsProfile rp = g.AddComponent<MavF15AfitResearchFlightDynamicsProfile>();
                rp.conditionMode = MavF15ResearchConditionMode.SourceReproduction;
                MavSixDoFBody body = g.AddComponent<MavSixDoFBody>();
                body.profileProvider = rp;
                MavF15AeroModel aero = g.AddComponent<MavF15AeroModel>();
                aero.sourceMode = MavF15AeroSourceMode.ExactNasa836Unavailable;
                aero.allowCrossValidationResearchModel = true;
                MavAeroCoefficients c = aero.Evaluate(State(400.0, 5f),
                    default(MavControlInput), MavAtmosphereModel.Sample(MavF15CoefficientFitCondition.PressureAltitudeM));
                return aero.debugRefused && c.cx == 0f && c.cm == 0f;
            }
            finally
            {
                Object.DestroyImmediate(g);
            }
        }

        private static MavF15AeroModel ResearchAero(GameObject g)
        {
            MavF15AeroModel aero = g.AddComponent<MavF15AeroModel>();
            aero.sourceMode = MavF15AeroSourceMode.BaumannMach06SixAxisResearch;
            aero.allowCrossValidationResearchModel = true;
            return aero;
        }

        private static MavFlightState State(double tasFtPerSec, float alphaDeg, float machOverride = -1f)
        {
            MavAtmosphereSample atm = MavAtmosphereModel.Sample(MavF15CoefficientFitCondition.PressureAltitudeM);
            MavFlightState s = new MavFlightState();
            s.trueAirspeedMps = (float)(tasFtPerSec * FtToM);
            s.mach = machOverride > 0f ? machOverride : s.trueAirspeedMps / atm.speedOfSoundMps;
            s.alphaRad = alphaDeg * Mathf.Deg2Rad;
            s.betaRad = 0f;
            return s;
        }

        private static double PhiDot(double p, double q, double r, double thetaDeg, double phiDeg)
        {
            double th = thetaDeg * Math.PI / 180.0, ph = phiDeg * Math.PI / 180.0;
            return p + (q * Math.Sin(ph) + r * Math.Cos(ph)) * Math.Tan(th);
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
