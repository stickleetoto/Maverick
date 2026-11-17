#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using MaverickFresh.FlightDynamics.F16;

namespace MaverickFresh.FlightDynamics.Validation
{
    /// <summary>
    /// Static aerodynamic comparison of Maverick's F-16 (Morelli 1998 compact polynomial,
    /// configuration F16-CFG-SIM-MORELLI1998) against NASA's NESC check-case F-16 tables
    /// (F16_aero.dml, configuration F16-CFG-NESC-CHECKCASE). The two share research lineage (both
    /// descend from the Stevens &amp; Lewis simplified version of the TP-1538 database) but are not
    /// the same representation, so magnitude differences are CLASSIFIED and reported, never gated.
    ///
    /// The NASA file is NOT committed. It is read at run time from the path in the environment
    /// variable MAVERICK_NESC_F16_AERO_DML or the command-line argument -nescF16AeroDml, and its
    /// SHA-256 must equal the value pinned here (NESC F16_package, see
    /// Docs/Validation/NASA/NASA_6DOF_R1_PLAN.md). If the file is absent the suite reports NOT RUN as
    /// a failure so it can never pass silently.
    ///
    ///   [F0] source identity
    ///   [F1] the independent DAVE-ML evaluator reproduces all NASA static check-shots
    ///   [F2] Maverick geometry / units / dynamic-pressure dimensionalisation
    ///   [F3] production MavF16AeroModel path equals the polynomial at matched inputs
    ///   [F4] Morelli vs NESC at the NASA check-shot inputs (reported)
    ///   [F5] static sweep over the NESC domain: statistics and classification (reported) + finiteness gate
    ///   [F6] derivative signs: stability and damping (gated where NESC is significant), controls (reported)
    ///   [F8] attribution: Maverick's polynomial equals Morelli Table 3 typed from the paper at the points of largest difference
    ///   [F7] case 11 trim feasibility at SIM 04's published trimmed state (static; trajectory not run)
    /// </summary>
    public static class MavNasaF16StaticAeroValidation
    {
        public const string PinnedAeroDmlSha256 = "82796577b989b5f3becb7d13430ff6c619b3ecd2e642a6427b743658fb177e51";
        public const string EnvironmentVariable = "MAVERICK_NESC_F16_AERO_DML";
        public const string CommandLineArgument = "-nescF16AeroDml";

        private const double Ft = 0.3048;
        private const double Lbf = 4.4482216152605;
        private const double Deg = Math.PI / 180.0;
        private const double SpanFt = 30.0, ChordFt = 11.32, AreaFt2 = 300.0;

        private static readonly string[] Outputs =
        {
            "aeroBodyForceCoefficient_X", "aeroBodyForceCoefficient_Y", "aeroBodyForceCoefficient_Z",
            "aeroBodyMomentCoefficient_Roll", "aeroBodyMomentCoefficient_Pitch", "aeroBodyMomentCoefficient_Yaw"
        };
        private static readonly string[] Short = { "CX", "CY", "CZ", "Cl", "Cm", "Cn" };

        private static int passed, failed;
        private static StringBuilder report;

        public static string RunAll(out int passedCount, out int failedCount)
        {
            passed = failed = 0;
            report = new StringBuilder(32768);
            report.AppendLine("NASA NESC F-16 static aerodynamics (DAVE-ML tables) vs Maverick Morelli 1998 polynomial - configurations kept separate");
            try
            {
                string path = ResolvePath();
                if (path == null || !File.Exists(path))
                {
                    Check(false, "F0", "NOT RUN: NASA F16_aero.dml not available (set " + EnvironmentVariable + " or " + CommandLineArgument
                        + "); this suite cannot pass without its source data");
                }
                else
                {
                    byte[] bytes = File.ReadAllBytes(path);
                    string sha = Sha256(bytes);
                    Check(sha == PinnedAeroDmlSha256, "F0", "F16_aero.dml SHA-256 " + sha + (sha == PinnedAeroDmlSha256 ? " matches the pinned NESC package value" : " DOES NOT match " + PinnedAeroDmlSha256 + " - refusing to compare"));
                    if (sha == PinnedAeroDmlSha256)
                    {
                        MavDaveMlEvaluator nesc = MavDaveMlEvaluator.Load(Encoding.UTF8.GetString(bytes));
                        CheckShots(nesc);
                        UnitsAndDimensionalisation();
                        ComponentPath();
                        ShotsComparison(nesc);
                        Sweep(nesc);
                        Derivatives(nesc);
                        MorelliTableThreeAttribution();
                        Case11Trim(nesc, path);
                    }
                }
            }
            catch (Exception ex)
            {
                Check(false, "EXC", "unexpected exception: " + ex);
            }
            report.AppendLine();
            report.AppendLine("RESULT: " + (failed == 0 ? "PASS" : "FAIL") + " passed=" + passed + " failed=" + failed);
            passedCount = passed;
            failedCount = failed;
            return report.ToString();
        }

        private static string ResolvePath()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == CommandLineArgument)
                    return args[i + 1];
            return Environment.GetEnvironmentVariable(EnvironmentVariable);
        }

        private static string Sha256(byte[] bytes)
        {
            using (SHA256 h = SHA256.Create())
            {
                byte[] d = h.ComputeHash(bytes);
                StringBuilder sb = new StringBuilder(64);
                foreach (byte b in d) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        // ------------------------------------------------------------------ models at matched inputs

        private struct Inputs
        {
            public double vtFtS, alphaDeg, betaDeg, p, q, r, elDeg, ailDeg, rdrDeg;
        }

        private static double[] Nesc(MavDaveMlEvaluator e, Inputs x)
        {
            e.SetInputs(new Dictionary<string, double>
            {
                { "vt", x.vtFtS }, { "alpha", x.alphaDeg }, { "beta", x.betaDeg },
                { "p", x.p }, { "q", x.q }, { "r", x.r },
                { "el", x.elDeg }, { "ail", x.ailDeg }, { "rdr", x.rdrDeg }
            });
            double[] o = new double[6];
            for (int i = 0; i < 6; i++) o[i] = e.Get(Outputs[i]);
            return o;
        }

        /// <summary>Morelli polynomial at the same physical inputs, CG at the 0.35 c reference (where NESC reports its coefficients).</summary>
        private static double[] Morelli(Inputs x)
        {
            double v = x.vtFtS;
            MavAeroCoefficients c = MavF16MorelliPolynomial.Evaluate(
                (float)(x.alphaDeg * Deg), (float)(x.betaDeg * Deg),
                (float)(x.elDeg * Deg), (float)(x.ailDeg * Deg), (float)(x.rdrDeg * Deg),
                (float)(x.p * SpanFt / (2 * v)), (float)(x.q * ChordFt / (2 * v)), (float)(x.r * SpanFt / (2 * v)),
                0.35f, 0.35f, (float)(ChordFt / SpanFt));
            return new double[] { c.cx, c.cy, c.cz, c.cl, c.cm, c.cn };
        }

        // ------------------------------------------------------------------ [F1]

        private static void CheckShots(MavDaveMlEvaluator nesc)
        {
            report.AppendLine();
            report.AppendLine("[F1] Independent DAVE-ML evaluator vs NASA's own static check-shots in F16_aero.dml");
            Check(nesc.StaticShots.Count == 16, "F1", "16 static check-shots present (found " + nesc.StaticShots.Count + ")");
            foreach (MavDaveMlEvaluator.StaticShot shot in nesc.StaticShots)
            {
                Dictionary<string, double> inputs = new Dictionary<string, double>();
                foreach (MavDaveMlEvaluator.CheckSignal s in shot.inputs) inputs[s.name] = s.value;
                nesc.SetInputs(inputs);
                double worst = 0.0;
                bool ok = shot.outputs.Count > 0;
                foreach (MavDaveMlEvaluator.CheckSignal s in shot.outputs)
                {
                    double got = nesc.Get(s.name);
                    double err = Math.Abs(got - s.value);
                    worst = Math.Max(worst, err);
                    ok &= err <= s.tol;
                }
                Check(ok, "F1", "check-shot '" + shot.name + "': " + shot.outputs.Count + " outputs within their tolerance (worst |err| " + E(worst) + ")");
            }
        }

        // ------------------------------------------------------------------ [F2]

        private static void UnitsAndDimensionalisation()
        {
            report.AppendLine();
            report.AppendLine("[F2] Geometry, units and dynamic-pressure dimensionalisation (production MavFlightDynamicsMath)");
            MavAeroReferenceGeometry g = MavF16MorelliReference.CreateReferenceGeometry();
            Check(Math.Abs(g.wingAreaM2 - AreaFt2 * Ft * Ft) < 1e-4 && Math.Abs(g.wingSpanM - SpanFt * Ft) < 1e-5 && Math.Abs(g.meanAerodynamicChordM - ChordFt * Ft) < 1e-5,
                "F2", "Maverick reference geometry equals the NESC 300 ft^2 / 30 ft / 11.32 ft converted exactly (" + g.wingAreaM2 + " m^2, " + g.wingSpanM + " m, " + g.meanAerodynamicChordM + " m)");

            // Same coefficients, dimensionalised two ways: Maverick SI vs an English-unit computation converted here.
            MavAeroCoefficients c = new MavAeroCoefficients { cx = -0.004f, cy = 0.012f, cz = -0.416f, cl = -0.0021f, cm = 0.0153f, cn = 0.0047f };
            double qbarPa = 0.5 * 0.9 * 150.0 * 150.0;
            MavAerodynamicLoads loads = MavFlightDynamicsMath.Dimensionalize(c, g, (float)qbarPa);
            double qbarPsf = qbarPa / (Lbf / (Ft * Ft));
            double fxLbf = qbarPsf * AreaFt2 * c.cx, fzLbf = qbarPsf * AreaFt2 * c.cz;
            double lFtLbf = qbarPsf * AreaFt2 * SpanFt * c.cl, mFtLbf = qbarPsf * AreaFt2 * ChordFt * c.cm, nFtLbf = qbarPsf * AreaFt2 * SpanFt * c.cn;
            double rel = Math.Max(Math.Max(Rel(loads.forceAeroBodyN.x, fxLbf * Lbf), Rel(loads.forceAeroBodyN.z, fzLbf * Lbf)),
                Math.Max(Rel(loads.momentAeroBodyNm.x, lFtLbf * Lbf * Ft), Math.Max(Rel(loads.momentAeroBodyNm.y, mFtLbf * Lbf * Ft), Rel(loads.momentAeroBodyNm.z, nFtLbf * Lbf * Ft))));
            Check(rel < 2e-6, "F2", "force = q S C and moment = q S (b|c) C agree with an English-unit computation to " + E(rel) + " (float precision)");

            MavAtmosphereSample atm = new MavAtmosphereSample { densityKgM3 = 0.9f, speedOfSoundMps = 330f };
            MavFlightState st = MavSixDoFBody.BuildFlightState(Vector3.zero, new Vector3(0f, 0f, 150f), new Vector3(0f, 0f, 150f), Vector3.zero,
                Vector3.forward, Vector3.up, Vector3.right, atm);
            Check(Math.Abs(st.dynamicPressurePa - qbarPa) / qbarPa < 1e-6, "F2", "flight state q-bar = 1/2 rho V^2 (" + st.dynamicPressurePa.ToString("F3", CultureInfo.InvariantCulture) + " Pa)");
        }

        // ------------------------------------------------------------------ [F3]

        private static void ComponentPath()
        {
            report.AppendLine();
            report.AppendLine("[F3] Production MavF16AeroModel component (deg -> rad, rate normalisation) equals the polynomial at matched inputs");
            GameObject go = new GameObject("nasa-f16-aero-probe") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                MavF16AeroModel model = go.AddComponent<MavF16AeroModel>();
                model.xCgCbar = 0.35f;
                model.xCgReferenceCbar = 0.35f;
                double worst = 0.0;
                foreach (double a in new[] { -5.0, 5.0, 17.0, 33.0 })
                {
                    Inputs x = new Inputs { vtFtS = 500.0, alphaDeg = a, betaDeg = 4.0, p = 0.3, q = -0.1, r = 0.2, elDeg = -6.0, ailDeg = 7.0, rdrDeg = -9.0 };
                    MavFlightState st = new MavFlightState
                    {
                        trueAirspeedMps = (float)(x.vtFtS * Ft),
                        alphaRad = (float)(x.alphaDeg * Deg),
                        betaRad = (float)(x.betaDeg * Deg),
                        aeroBodyRatesRadSec = new Vector3((float)x.p, (float)x.q, (float)x.r),
                        mach = 0.4f
                    };
                    MavControlInput ci = new MavControlInput { elevatorDeg = (float)x.elDeg, aileronDeg = (float)x.ailDeg, rudderDeg = (float)x.rdrDeg };
                    MavAeroCoefficients c = model.Evaluate(st, ci, new MavAtmosphereSample());
                    double[] direct = Morelli(x);
                    double[] comp = { c.cx, c.cy, c.cz, c.cl, c.cm, c.cn };
                    for (int i = 0; i < 6; i++) worst = Math.Max(worst, Math.Abs(comp[i] - direct[i]));
                }
                Check(worst < 1e-5, "F3", "component vs direct polynomial: max |dC| " + E(worst) + " < 1e-5 (float rounding of the rate normalisation)");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        // ------------------------------------------------------------------ [F4]

        private static void ShotsComparison(MavDaveMlEvaluator nesc)
        {
            report.AppendLine();
            report.AppendLine("[F4] Morelli polynomial vs NESC tables at the NASA check-shot inputs (difference = Morelli - NESC; reported, not gated)");
            report.AppendLine("  shot                    dCX        dCY        dCZ        dCl        dCm        dCn");
            foreach (MavDaveMlEvaluator.StaticShot shot in nesc.StaticShots)
            {
                Dictionary<string, double> m = new Dictionary<string, double>();
                foreach (MavDaveMlEvaluator.CheckSignal s in shot.inputs) m[s.name] = s.value;
                Inputs x = new Inputs
                {
                    vtFtS = m["trueAirspeed"], alphaDeg = m["angleOfAttack"], betaDeg = m["angleOfSideslip"],
                    p = m["bodyAngularRate_Roll"], q = m["bodyAngularRate_Pitch"], r = m["bodyAngularRate_Yaw"],
                    elDeg = m["elevatorDeflection"], ailDeg = m["aileronDeflection"], rdrDeg = m["rudderDeflection"]
                };
                double[] a = Morelli(x), b = Nesc(nesc, x);
                StringBuilder line = new StringBuilder("  " + shot.name.PadRight(22));
                for (int i = 0; i < 6; i++) line.Append(" " + (a[i] - b[i]).ToString("+0.00000;-0.00000", CultureInfo.InvariantCulture).PadLeft(10));
                report.AppendLine(line.ToString());
            }
        }

        // ------------------------------------------------------------------ [F5]

        private static void Sweep(MavDaveMlEvaluator nesc)
        {
            report.AppendLine();
            report.AppendLine("[F5] Static sweep over the shared domain: alpha -10..45 step 2.5, beta {-30,-20,-10,-5,0,5,10,20,30}, elevator {-24,-12,0,12,24} deg; aileron = rudder = 0; rates 0");
            int n = 0;
            double[] maxAbs = new double[6], sumSq = new double[6], minN = new double[6], maxN = new double[6];
            double[] sx = new double[6], sy = new double[6], sxx = new double[6], syy = new double[6], sxy = new double[6];
            string[] worstAt = new string[6];
            for (int i = 0; i < 6; i++) { minN[i] = double.MaxValue; maxN[i] = double.MinValue; }
            bool finite = true;
            foreach (double a in Range(-10.0, 45.0, 2.5))
                foreach (double be in new[] { -30.0, -20.0, -10.0, -5.0, 0.0, 5.0, 10.0, 20.0, 30.0 })
                    foreach (double el in new[] { -24.0, -12.0, 0.0, 12.0, 24.0 })
                    {
                        Inputs x = new Inputs { vtFtS = 500.0, alphaDeg = a, betaDeg = be, elDeg = el };
                        double[] mo = Morelli(x), ne = Nesc(nesc, x);
                        n++;
                        for (int i = 0; i < 6; i++)
                        {
                            finite &= !(double.IsNaN(mo[i]) || double.IsInfinity(mo[i]));
                            double d = mo[i] - ne[i];
                            if (Math.Abs(d) > maxAbs[i]) { maxAbs[i] = Math.Abs(d); worstAt[i] = "a " + a + " b " + be + " el " + el; }
                            sumSq[i] += d * d;
                            minN[i] = Math.Min(minN[i], ne[i]); maxN[i] = Math.Max(maxN[i], ne[i]);
                            sx[i] += mo[i]; sy[i] += ne[i]; sxx[i] += mo[i] * mo[i]; syy[i] += ne[i] * ne[i]; sxy[i] += mo[i] * ne[i];
                        }
                    }
            Check(finite, "F5", "Morelli polynomial finite at all " + n + " sweep points");
            report.AppendLine("  coef  max|d|      rms d       NESC range  max|d|/range  corr     worst at                     classification");
            for (int i = 0; i < 6; i++)
            {
                double range = maxN[i] - minN[i];
                double corr = (n * sxy[i] - sx[i] * sy[i]) / Math.Sqrt(Math.Max(1e-300, (n * sxx[i] - sx[i] * sx[i]) * (n * syy[i] - sy[i] * sy[i])));
                double rel = range > 0 ? maxAbs[i] / range : 0.0;
                string cls = corr >= 0.98 && rel <= 0.25 ? "FIT/REPRESENTATION DIFFERENCE" : "UNRESOLVED (see report)";
                report.AppendLine("  " + Short[i].PadRight(5) + E(maxAbs[i]) + "   " + E(Math.Sqrt(sumSq[i] / n)) + "   " + E(range) + "   "
                    + rel.ToString("F3", CultureInfo.InvariantCulture).PadLeft(10) + "   " + corr.ToString("F4", CultureInfo.InvariantCulture) + "   "
                    + (worstAt[i] ?? "-").PadRight(28) + " " + cls);
            }
            report.AppendLine("  classification rule (reporting only): correlation >= 0.98 and max|d| <= 25 % of the NESC range => fit/representation difference;");
            report.AppendLine("  otherwise unresolved. Both models are fits/tables of the same simplified dataset, so a difference is not by itself a defect.");
        }

        // ------------------------------------------------------------------ [F6]

        private static void Derivatives(MavDaveMlEvaluator nesc)
        {
            report.AppendLine();
            report.AppendLine("[F6] Derivative signs, central differences at beta = 0, V 500 ft/s (Morelli | NESC)");
            double[] alphas = { -5.0, 0.0, 5.0, 10.0, 15.0, 20.0, 25.0, 30.0, 35.0, 40.0 };
            // name, coefficient index, input perturbed, step
            string[] names = { "Cl_beta", "Cn_beta", "Cm_alpha", "Cl_p", "Cm_q", "Cn_r", "Cm_de", "Cl_da", "Cn_da", "Cl_dr", "Cn_dr" };
            int[] coef = { 3, 5, 4, 3, 4, 5, 4, 3, 5, 3, 5 };
            bool[] gated = { true, true, false, true, true, true, false, false, false, false, false };
            report.AppendLine("  deriv     " + string.Join("  ", Array.ConvertAll(alphas, a => ("a" + a).PadLeft(17))));
            for (int d = 0; d < names.Length; d++)
            {
                double[] dm = new double[alphas.Length], dn = new double[alphas.Length];
                double maxMagN = 0.0;
                for (int k = 0; k < alphas.Length; k++)
                {
                    dm[k] = Derivative(names[d], coef[d], alphas[k], true, null);
                    dn[k] = Derivative(names[d], coef[d], alphas[k], false, nesc);
                    maxMagN = Math.Max(maxMagN, Math.Abs(dn[k]));
                }
                StringBuilder line = new StringBuilder("  " + names[d].PadRight(8));
                int disagree = 0, considered = 0;
                for (int k = 0; k < alphas.Length; k++)
                {
                    line.Append("  " + (dm[k].ToString("+0.0000;-0.0000", CultureInfo.InvariantCulture) + "|" + dn[k].ToString("+0.0000;-0.0000", CultureInfo.InvariantCulture)).PadLeft(17));
                    if (Math.Abs(dn[k]) >= 0.1 * maxMagN && Math.Abs(dm[k]) >= 0.1 * maxMagN)
                    {
                        considered++;
                        if (Math.Sign(dm[k]) != Math.Sign(dn[k])) disagree++;
                    }
                }
                report.AppendLine(line.ToString());
                string verdict = names[d] + ": sign agreement at " + (considered - disagree) + "/" + considered + " alphas where both magnitudes are >= 10 % of the NESC maximum";
                if (gated[d])
                    Check(disagree == 0, "F6", verdict);
                else
                    report.AppendLine("  INFO [F6] " + verdict + (disagree > 0 ? "  -> classified: CONVENTION/REPRESENTATION, see F16_STATIC_AERO_REPORT.md" : ""));
            }
        }

        private static double Derivative(string name, int coefIndex, double alphaDeg, bool morelli, MavDaveMlEvaluator nesc)
        {
            Inputs lo = new Inputs { vtFtS = 500.0, alphaDeg = alphaDeg };
            Inputs hi = lo;
            double step;
            switch (name)
            {
                case "Cl_beta": case "Cn_beta": step = 1.0; lo.betaDeg = -step; hi.betaDeg = step; break;
                case "Cm_alpha": step = 1.0; lo.alphaDeg = alphaDeg - step; hi.alphaDeg = alphaDeg + step; break;
                case "Cl_p": step = 0.1; lo.p = -step; hi.p = step; break;
                case "Cm_q": step = 0.1; lo.q = -step; hi.q = step; break;
                case "Cn_r": step = 0.1; lo.r = -step; hi.r = step; break;
                case "Cm_de": step = 2.0; lo.elDeg = -step; hi.elDeg = step; break;
                case "Cl_da": case "Cn_da": step = 2.0; lo.ailDeg = -step; hi.ailDeg = step; break;
                default: step = 2.0; lo.rdrDeg = -step; hi.rdrDeg = step; break;
            }
            double[] a = morelli ? Morelli(hi) : Nesc(nesc, hi);
            double[] b = morelli ? Morelli(lo) : Nesc(nesc, lo);
            return (a[coefIndex] - b[coefIndex]) / (2.0 * step);
        }

        // ------------------------------------------------------------------ [F8] attribution

        /// <summary>
        /// Attribution check for the largest differences found in F4-F7: is the difference in
        /// Morelli's PUBLISHED polynomial, or in Maverick's implementation of it? The coefficients
        /// below are typed from Morelli (1998), Table 3 (NTRS 20040110310, p. 5), independently of the
        /// repository's frozen constants, and evaluated here in double precision.
        /// </summary>
        private static void MorelliTableThreeAttribution()
        {
            report.AppendLine();
            report.AppendLine("[F8] Attribution: Maverick's polynomial vs Morelli Table 3 typed from the paper (independent evaluation)");
            double a = 5.0 * Deg;
            double cx = -1.943367e-02 + 2.136104e-01 * a + 6.988016e-01 * a * a - 9.035381e-01 * a * a * a;                 // a0 + a1 a + a5 a^2 + a6 a^3 (de = 0)
            double cz = -1.378278e-01 - 4.211369e+00 * a + 4.775187e+00 * a * a - 1.026225e+01 * a * a * a + 8.399763e+00 * a * a * a * a; // beta = 0, de = 0
            double a30 = 30.0 * Deg;
            double cnBetaPerRad = 2.993363e-01 + 6.594004e-02 * a30 - 2.107885e+00 * a30 * a30 + 8.476901e-01 * a30 * a30 * a30;   // o0 + o1 a + o4 a^2 + o6 a^3
            double[] m5 = Morelli(new Inputs { vtFtS = 500.0, alphaDeg = 5.0 });
            double cnBetaMaverickPerDeg = (Morelli(new Inputs { vtFtS = 500.0, alphaDeg = 30.0, betaDeg = 1.0 })[5]
                                           - Morelli(new Inputs { vtFtS = 500.0, alphaDeg = 30.0, betaDeg = -1.0 })[5]) / 2.0;
            report.AppendLine("  Table 3: CX(5 deg) " + cx.ToString("F6", CultureInfo.InvariantCulture) + ", CZ(5 deg) " + cz.ToString("F6", CultureInfo.InvariantCulture)
                + ", Cn_beta(30 deg) " + (cnBetaPerRad * Deg).ToString("F6", CultureInfo.InvariantCulture) + " /deg");
            Check(Math.Abs(m5[0] - cx) < 1e-6, "F8", "CX at alpha 5 deg: Maverick " + m5[0].ToString("F6", CultureInfo.InvariantCulture) + " = Table 3 (the sign difference from the NESC table is in the published polynomial)");
            Check(Math.Abs(m5[2] - cz) < 1e-6, "F8", "CZ at alpha 5 deg: Maverick " + m5[2].ToString("F6", CultureInfo.InvariantCulture) + " = Table 3 (the lift difference from the NESC table is in the published polynomial)");
            Check(Math.Abs(cnBetaMaverickPerDeg - cnBetaPerRad * Deg) < 1e-5, "F8", "Cn_beta at alpha 30 deg: Maverick " + cnBetaMaverickPerDeg.ToString("F6", CultureInfo.InvariantCulture)
                + " /deg = Table 3 (the sign difference from the NESC table is in the published polynomial)");
        }

        // ------------------------------------------------------------------ [F7] case 11 trim feasibility

        public const string PinnedPropDmlSha256 = "e5fd7772e5c05cdac666c94febbe4cb50139418b26045affd92dbd198f2215c6";
        public const string PinnedInertiaDmlSha256 = "e11d4eba5b158d24db7034bd85682ad0b896dd2f3172f007ca3d6d6d4e693c4a";

        // NESC atmospheric check case 11, SIM 04, first data row (t = 0, the trimmed state):
        // Atmospheric_checkcases/Atmos_11_TrimCheckSubsonicF16/Atmos_11_sim_04.csv
        // sha256 c6b8c1210c31fa440d271297ad219b5ad89264f4bb8a25f636c14f53d9b04a07 (inside the pinned checkcases zip).
        private const double Sim04ThetaDeg = 2.63872639635;      // eulerAngle_deg_Pitch (= alpha: wings level, zero flight-path angle)
        private const double Sim04QbarPsf = 280.787827751;       // dynamicPressure_lbf_ft2
        private const double Sim04Mach = 0.525083366639;         // mach
        private const double Sim04AltitudeFt = 10013.0;          // altitudeMsl_ft
        private const double Sim04GravityFtS2 = 32.188575449212834; // localGravity_ft_s2
        private const double Sim04VelocityNorthFtS = 400.010306117; // feVelocity_ft_s_X
        private const double Sim04VelocityEastFtS = 400.010306117;  // feVelocity_ft_s_Y
        private const double Sim04AeroForceXLbf = -1420.44198427;   // aero_bodyForce_lbf_X
        private const double Sim04AeroForceZLbf = -20401.3025899;   // aero_bodyForce_lbf_Z
        private const double TrimThrottle = 0.1390191130965607;     // F16_control.dml trimmedPilotControl_throttle (line 226)
        private const double CgFraction = 0.25, MrcFraction = 0.35;

        private static void Case11Trim(MavDaveMlEvaluator aero, string aeroPath)
        {
            report.AppendLine();
            report.AppendLine("[F7] Case 11 trim feasibility (static, at SIM 04's published t = 0 state; no trajectory)");
            string dir = Path.GetDirectoryName(aeroPath);
            string propPath = Path.Combine(dir, "F16_prop.dml"), inertiaPath = Path.Combine(dir, "F16_inertia.dml");
            if (!File.Exists(propPath) || !File.Exists(inertiaPath))
            {
                Check(false, "F7", "NOT RUN: F16_prop.dml / F16_inertia.dml not found next to F16_aero.dml");
                return;
            }
            byte[] propBytes = File.ReadAllBytes(propPath), inertiaBytes = File.ReadAllBytes(inertiaPath);
            bool idOk = Sha256(propBytes) == PinnedPropDmlSha256 && Sha256(inertiaBytes) == PinnedInertiaDmlSha256;
            Check(idOk, "F7", "F16_prop.dml and F16_inertia.dml SHA-256 match the pinned NESC package values");
            if (!idOk) return;
            MavDaveMlEvaluator prop = MavDaveMlEvaluator.Load(Encoding.UTF8.GetString(propBytes));
            MavDaveMlEvaluator inertia = MavDaveMlEvaluator.Load(Encoding.UTF8.GetString(inertiaBytes));

            int propOk = 0;
            foreach (MavDaveMlEvaluator.StaticShot shot in prop.StaticShots)
            {
                Dictionary<string, double> m = new Dictionary<string, double>();
                foreach (MavDaveMlEvaluator.CheckSignal s in shot.inputs) m[s.name] = s.value;
                prop.SetInputs(m);
                bool ok = true;
                foreach (MavDaveMlEvaluator.CheckSignal s in shot.outputs) ok &= Math.Abs(prop.Get(s.name) - s.value) <= Math.Max(s.tol, 0.0);
                if (ok) propOk++;
            }
            Check(prop.StaticShots.Count > 0 && propOk == prop.StaticShots.Count, "F7", "evaluator reproduces all " + prop.StaticShots.Count + " NASA propulsion check-shots (" + propOk + " ok)");

            inertia.SetInputs(new Dictionary<string, double>());
            double massSlug = inertia.Get("XMASS");
            double vt = Math.Sqrt(Sim04VelocityNorthFtS * Sim04VelocityNorthFtS + Sim04VelocityEastFtS * Sim04VelocityEastFtS);
            report.AppendLine("  state: alpha = theta " + Sim04ThetaDeg + " deg, q-bar " + Sim04QbarPsf + " psf, V " + vt.ToString("F4", CultureInfo.InvariantCulture)
                + " ft/s, Mach " + Sim04Mach + ", g " + Sim04GravityFtS2 + " ft/s^2, mass " + massSlug + " slug (DAVE-ML), CG 0.25 c, MRC 0.35 c");

            // NESC tables: elevator for zero pitching moment about the CG, then the forces.
            Func<double, double, double[]> nescAt = (alpha, el) => Nesc(aero, new Inputs { vtFtS = vt, alphaDeg = alpha, elDeg = el });
            double elNesc = SolveElevator(x => { double[] c = nescAt(Sim04ThetaDeg, x); return c[4] + c[2] * (MrcFraction - CgFraction); });
            double[] cn = nescAt(Sim04ThetaDeg, elNesc);
            double fxN = cn[0] * Sim04QbarPsf * AreaFt2, fzN = cn[2] * Sim04QbarPsf * AreaFt2;
            report.AppendLine("  NESC tables: trim elevator " + elNesc.ToString("F4", CultureInfo.InvariantCulture) + " deg; Fx " + fxN.ToString("F2", CultureInfo.InvariantCulture)
                + " lbf (SIM 04 " + Sim04AeroForceXLbf + "), Fz " + fzN.ToString("F2", CultureInfo.InvariantCulture) + " lbf (SIM 04 " + Sim04AeroForceZLbf + ")");
            // Neglected: SIM 04's trimmed pitch rate (-0.0039 deg/s) through CZq/CXq, |q c/2V| ~ 7e-7 => < 1e-4 relative.
            Check(Rel(fzN, Sim04AeroForceZLbf) < 5e-4, "F7", "NESC-table Z force at the published trim reproduces SIM 04 to " + E(Rel(fzN, Sim04AeroForceZLbf)) + " (< 5e-4: 5x the neglected pitch-rate term)");
            Check(Rel(fxN, Sim04AeroForceXLbf) < 5e-3, "F7", "NESC-table X force reproduces SIM 04 to " + E(Rel(fxN, Sim04AeroForceXLbf)) + " (< 5e-3; X is the small, drag-dominated component)");

            // Thrust: required along body X vs the NESC deck at the published trim throttle.
            double tReq = massSlug * Sim04GravityFtS2 * Math.Sin(Sim04ThetaDeg * Deg) - Sim04AeroForceXLbf;
            prop.SetInputs(new Dictionary<string, double> { { "PWR", TrimThrottle * 100.0 }, { "RMACH", Sim04Mach }, { "ALT", Sim04AltitudeFt } });
            double tTable = prop.Get("FEX");
            report.AppendLine("  thrust required " + tReq.ToString("F2", CultureInfo.InvariantCulture) + " lbf vs NESC deck at throttle " + TrimThrottle.ToString("F6", CultureInfo.InvariantCulture)
                + " " + tTable.ToString("F2", CultureInfo.InvariantCulture) + " lbf");
            Check(Rel(tReq, tTable) < 0.01, "F7", "required thrust matches the NESC deck at the published trim throttle to " + E(Rel(tReq, tTable))
                + " (< 1 %: rotating-Earth and curved-path terms neglected here are < 0.5 % of weight x sin(theta) ~ 5 lbf)");

            // Maverick Morelli polynomial at the same state, CG 0.25, reference 0.35 (Morelli's own transfer term).
            Func<double, double, double[]> morAt = (alpha, el) =>
            {
                MavAeroCoefficients c = MavF16MorelliPolynomial.Evaluate((float)(alpha * Deg), 0f, (float)(el * Deg), 0f, 0f, 0f, 0f, 0f,
                    (float)CgFraction, (float)MrcFraction, (float)(ChordFt / SpanFt));
                return new double[] { c.cx, c.cy, c.cz, c.cl, c.cm, c.cn };
            };
            double elMorSameAlpha = SolveElevator(x => morAt(Sim04ThetaDeg, x)[4]);
            double[] cm1 = morAt(Sim04ThetaDeg, elMorSameAlpha);
            report.AppendLine("  Morelli at the same alpha: trim elevator " + elMorSameAlpha.ToString("F4", CultureInfo.InvariantCulture) + " deg, Fx "
                + (cm1[0] * Sim04QbarPsf * AreaFt2).ToString("F2", CultureInfo.InvariantCulture) + " lbf, Fz " + (cm1[2] * Sim04QbarPsf * AreaFt2).ToString("F2", CultureInfo.InvariantCulture)
                + " lbf (representation difference at fixed alpha)");

            // Morelli re-trimmed to the SAME lift and zero pitching moment: isolates the aero representation.
            double alphaMor = Sim04ThetaDeg, elMor = elMorSameAlpha;
            for (int it = 0; it < 60; it++)
            {
                double a0 = alphaMor;
                Func<double, double> fzAt = a =>
                {
                    double e = SolveElevator(x => morAt(a, x)[4]);
                    return morAt(a, e)[2] * Sim04QbarPsf * AreaFt2 - Sim04AeroForceZLbf;
                };
                double f0 = fzAt(a0), f1 = fzAt(a0 + 1e-3);
                alphaMor = a0 - f0 * 1e-3 / (f1 - f0);
                elMor = SolveElevator(x => morAt(alphaMor, x)[4]);
                if (Math.Abs(alphaMor - a0) < 1e-9) break;
            }
            double[] cm2 = morAt(alphaMor, elMor);
            double tReqMor = massSlug * Sim04GravityFtS2 * Math.Sin(alphaMor * Deg) - cm2[0] * Sim04QbarPsf * AreaFt2;
            bool morOk = !double.IsNaN(alphaMor) && Math.Abs(cm2[2] * Sim04QbarPsf * AreaFt2 - Sim04AeroForceZLbf) < 0.01 && Math.Abs(cm2[4]) < 1e-6;
            Check(morOk, "F7", "Morelli model trims to the same lift with zero pitching moment (converged)");
            report.AppendLine("  Morelli trim at equal lift: alpha " + alphaMor.ToString("F4", CultureInfo.InvariantCulture) + " deg (NESC/SIM 04 " + Sim04ThetaDeg.ToString("F4", CultureInfo.InvariantCulture)
                + ", d " + (alphaMor - Sim04ThetaDeg).ToString("+0.0000;-0.0000", CultureInfo.InvariantCulture) + "), elevator " + elMor.ToString("F4", CultureInfo.InvariantCulture)
                + " deg (NESC " + elNesc.ToString("F4", CultureInfo.InvariantCulture) + ", d " + (elMor - elNesc).ToString("+0.0000;-0.0000", CultureInfo.InvariantCulture)
                + "), thrust required " + tReqMor.ToString("F1", CultureInfo.InvariantCulture) + " lbf (NESC " + tReq.ToString("F1", CultureInfo.InvariantCulture) + ")");
            report.AppendLine("  classification: FIT/REPRESENTATION DIFFERENCE (Morelli polynomial vs the tables it was fitted to); not a defect by itself");

            // Production trim solver at the nominal case 11 condition (its own ISA-at-geometric-altitude and standard gravity).
            MavTrimResult prod = MavF16TrimReference.SolveStraightAndLevel((float)(Sim04AltitudeFt * Ft), (float)(565.6854 * Ft));
            report.AppendLine("  production MavF16TrimReference.SolveStraightAndLevel(10013 ft, 565.6854 ft/s): status " + prod.status + ", alpha "
                + prod.alphaDeg.ToString("F4", CultureInfo.InvariantCulture) + " deg, elevator " + prod.elevatorDeg.ToString("F4", CultureInfo.InvariantCulture)
                + " deg, required thrust " + (prod.requiredThrustN / Lbf).ToString("F1", CultureInfo.InvariantCulture) + " lbf (includes g 9.80665 and Maverick atmosphere; reported)");
            Check(!float.IsNaN(prod.alphaDeg) && !float.IsNaN(prod.elevatorDeg), "F7", "production trim solver returns a finite alpha/elevator at the case 11 condition (" + prod.status + ")");
            report.AppendLine("  NOT RUN: case 11 trajectory (180 s). Reasons: rotating WGS-84 Earth vs flat Unity world; no NESC-table aero or NESC thrust profile");
            report.AppendLine("  in Maverick; check-case controller not ported. See VALIDATION_LIMITATIONS.md.");
        }

        /// <summary>Bisection on elevator (deg) over the shared table range for f(el) = 0.</summary>
        private static double SolveElevator(Func<double, double> f)
        {
            double lo = -24.0, hi = 24.0, flo = f(lo), fhi = f(hi);
            if (Math.Sign(flo) == Math.Sign(fhi)) return double.NaN;
            for (int i = 0; i < 80; i++)
            {
                double mid = 0.5 * (lo + hi), fm = f(mid);
                if (Math.Sign(fm) == Math.Sign(flo)) { lo = mid; flo = fm; } else { hi = mid; fhi = fm; }
            }
            return 0.5 * (lo + hi);
        }

        // ------------------------------------------------------------------ helpers

        private static IEnumerable<double> Range(double a, double b, double step)
        {
            for (double x = a; x <= b + 1e-9; x += step) yield return x;
        }

        private static double Rel(double got, double expected)
        {
            return Math.Abs(got - expected) / Math.Max(1e-12, Math.Abs(expected));
        }

        private static string E(double x) { return x.ToString("E3", CultureInfo.InvariantCulture); }

        private static void Check(bool ok, string tag, string message)
        {
            if (ok) passed++; else failed++;
            report.AppendLine("  " + (ok ? "PASS" : "FAIL") + " [" + tag + "] " + message);
        }
    }
}
#endif
