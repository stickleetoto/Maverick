#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace MaverickFresh.FlightDynamics.Validation
{
    /// <summary>
    /// NASA NESC 6-DoF atmospheric check cases 2 (dragless tumbling brick) and 3 (tumbling brick
    /// with aerodynamic damping), run through the ACTUAL Maverick backend: a production
    /// <see cref="MavSixDoFBody"/> on a real Rigidbody, stepped with its editor seam
    /// <c>StepPhysicsForValidation</c> and integrated by PhysX through <c>Physics.Simulate</c>
    /// (script simulation mode, restored afterwards). No production file is modified and no
    /// production setting is changed except on the temporary body this suite creates and destroys.
    ///
    /// Expected values come from <see cref="MavNasaRigidBodyReference"/> (independent, double,
    /// RK4) and, for the reference itself, from the published NASA tool outputs in
    /// <see cref="MavNasaBrickReferenceData"/>. The two are never computed by the same code as the
    /// measured result.
    ///
    ///   [B0] source identity: DAVE-ML constants equal the Vol II report tables
    ///   [B1] case 2 reference vs NASA tools
    ///   [B2] case 2 Maverick backend: mechanics, accuracy, convergence, invariants, gyro control
    ///   [B3] case 3 reference vs NASA tools
    ///   [B4] case 3 Maverick backend: mechanics, damping moments, atmosphere, convergence, dissipation
    ///
    /// Run: MavFdmValidationBatchAdapter -fdmMode sync -fdmType MavNasaBrickCheckCaseValidation -fdmMethod RunAll
    /// </summary>
    public static class MavNasaBrickCheckCaseValidation
    {
        // Unit conversions (exact definitions).
        private const double Ft = 0.3048;
        private const double SlugKg = 14.59390293720636;
        private const double SlugFt2KgM2 = SlugKg * Ft * Ft;
        private const double Deg = Math.PI / 180.0;

        private static readonly float[] TimeSteps = { 0.02f, 0.01f, 0.005f, 0.0025f };
        private const int SampleCount = 16; // t = 0, 2, ..., 30 s
        private const double SampleStep = 2.0;
        private const int ReferenceSubsteps = 2000; // h = 1 ms
        private const int Sim01 = 0, Sim02 = 1, Sim04 = 2, Sim05 = 3, Sim06 = 4;

        private static int passed;
        private static int failed;
        private static StringBuilder report;

        public static string RunAll(out int passedCount, out int failedCount)
        {
            passed = 0;
            failed = 0;
            report = new StringBuilder(32768);
            report.AppendLine("NASA NESC 6-DoF check cases 2 and 3 - tumbling brick through MavSixDoFBody + PhysX (editor seam + Physics.Simulate)");
            report.AppendLine("Unity " + Application.unityVersion + "; source checkcases zip sha256 " + MavNasaBrickReferenceData.CheckcasesZipSha256
                + "; models zip sha256 " + MavNasaBrickReferenceData.ModelsZipSha256);

            SimulationMode savedMode = Physics.simulationMode;
            Vector3 savedGravity = Physics.gravity;
            try
            {
                SourceIdentity();
                Case2();
                Case3();
            }
            catch (Exception ex)
            {
                Check(false, "EXC", "unexpected exception: " + ex);
            }
            finally
            {
                Physics.simulationMode = savedMode;
                Physics.gravity = savedGravity;
            }

            report.AppendLine();
            report.AppendLine("RESULT: " + (failed == 0 ? "PASS" : "FAIL") + " passed=" + passed + " failed=" + failed);
            passedCount = passed;
            failedCount = failed;
            return report.ToString();
        }

        // ------------------------------------------------------------------ [B0]

        private static void SourceIdentity()
        {
            report.AppendLine();
            report.AppendLine("[B0] Source identity: DAVE-ML model values vs NESC Vol II Tables 4-5 (typed from the report, PDF pp. 17-18)");
            // Hand-typed from the REPORT, deliberately independent of the generated DAVE-ML extract.
            Check(Math.Abs(MavNasaBrickReferenceData.MassSlug - 0.155404754) < 1e-12, "B0", "mass 0.155404754 slug");
            Check(Math.Abs(MavNasaBrickReferenceData.IxxSlugFt2 - 0.001894220) < 1e-12, "B0", "Ixx 0.001894220 slug ft^2");
            Check(Math.Abs(MavNasaBrickReferenceData.IyySlugFt2 - 0.006211019) < 1e-12, "B0", "Iyy 0.006211019 slug ft^2");
            Check(Math.Abs(MavNasaBrickReferenceData.IzzSlugFt2 - 0.007194665) < 1e-12, "B0", "Izz 0.007194665 slug ft^2");
            Check(MavNasaBrickReferenceData.IzxSlugFt2 == 0.0 && MavNasaBrickReferenceData.IxySlugFt2 == 0.0 && MavNasaBrickReferenceData.IyzSlugFt2 == 0.0,
                "B0", "products of inertia zero (principal axes = body axes)");
            Check(Math.Abs(MavNasaBrickReferenceData.ReferenceAreaFt2 - 0.22222) < 1e-12
                  && Math.Abs(MavNasaBrickReferenceData.ReferenceSpanFt - 0.33333) < 1e-12
                  && Math.Abs(MavNasaBrickReferenceData.ReferenceChordFt - 0.66667) < 1e-12, "B0", "S 0.22222 ft^2, b 0.33333 ft, c 0.66667 ft");
            Check(MavNasaBrickReferenceData.Clp == -1.0 && MavNasaBrickReferenceData.Cmq == -1.0 && MavNasaBrickReferenceData.Cnr == -1.0
                  && MavNasaBrickReferenceData.Clr == 0.0 && MavNasaBrickReferenceData.Cnp == 0.0, "B0", "Clp = Cmq = Cnr = -1, Clr = Cnp = 0");
            Check(MavNasaBrickReferenceData.TrueAirspeedFloorFtS == 0.5, "B0",
                "brick_aero.dml VRW minValue 0.5 ft/s (rate normalisation floor; not stated in the report tables)");
        }

        private static MavNasaRigidBodyReference.Config BrickConfig(bool damping)
        {
            return new MavNasaRigidBodyReference.Config
            {
                ixx = MavNasaBrickReferenceData.IxxSlugFt2 * SlugFt2KgM2,
                iyy = MavNasaBrickReferenceData.IyySlugFt2 * SlugFt2KgM2,
                izz = MavNasaBrickReferenceData.IzzSlugFt2 * SlugFt2KgM2,
                areaM2 = MavNasaBrickReferenceData.ReferenceAreaFt2 * Ft * Ft,
                spanM = MavNasaBrickReferenceData.ReferenceSpanFt * Ft,
                chordM = MavNasaBrickReferenceData.ReferenceChordFt * Ft,
                clp = MavNasaBrickReferenceData.Clp,
                clr = MavNasaBrickReferenceData.Clr,
                cmq = MavNasaBrickReferenceData.Cmq,
                cnp = MavNasaBrickReferenceData.Cnp,
                cnr = MavNasaBrickReferenceData.Cnr,
                airspeedFloorMps = MavNasaBrickReferenceData.TrueAirspeedFloorFtS * Ft,
                aerodynamicDampingOn = damping,
                initialAltitudeM = 30000.0 * Ft
            };
        }

        private static MavNasaRigidBodyReference.State BrickInitial()
        {
            // Vol II Table 26/27: inertial body rates 10, 20, 30 deg/s; body x north, z down; 30,000 ft.
            return MavNasaRigidBodyReference.Initial(10.0 * Deg, 20.0 * Deg, 30.0 * Deg, 30000.0 * Ft);
        }

        // ------------------------------------------------------------------ case 2

        private static void Case2()
        {
            MavNasaRigidBodyReference.Config cfg = BrickConfig(false);
            cfg.atmosphereMode = MavNasaRigidBodyReference.AtmosphereMode.None;
            cfg.gravityMode = MavNasaRigidBodyReference.GravityMode.Constant;
            cfg.constantGravityMps2 = NasaInitialGravityMps2(2);

            MavNasaRigidBodyReference.State[] reference = MavNasaRigidBodyReference.Solve(cfg, BrickInitial(), SampleStep, SampleCount, ReferenceSubsteps);
            MavNasaRigidBodyReference.State[] referenceFine = MavNasaRigidBodyReference.Solve(cfg, BrickInitial(), SampleStep, SampleCount, 2 * ReferenceSubsteps);

            report.AppendLine();
            report.AppendLine("[B1] Case 2 independent reference (RK4, h 1 ms, double) vs NASA tools (inertial body rates; attitude relative to the t = 0 NED frame)");
            double refSelf = 0.0;
            for (int k = 0; k < SampleCount; k++)
                refSelf = Math.Max(refSelf, RateDiffDeg(reference[k], referenceFine[k]));
            report.AppendLine("  reference self-consistency (h vs h/2), max |dw| " + E(refSelf) + " deg/s");
            Check(refSelf < 1e-9, "B1", "reference converged: h vs h/2 max |dw| " + E(refSelf) + " deg/s < 1e-9");
            CompareReferenceToNasa(2, reference, true);

            report.AppendLine();
            report.AppendLine("[B2] Case 2 Maverick backend (MavSixDoFBody gyroscopic compensation ON) vs independent reference");
            report.AppendLine("  dt       max|dw| deg/s   max att deg     |H| rel drift   T rel drift");
            double[] rateErr = new double[TimeSteps.Length];
            double[] attErr = new double[TimeSteps.Length];
            double[] hDrift = new double[TimeSteps.Length];
            double[] tDrift = new double[TimeSteps.Length];
            for (int i = 0; i < TimeSteps.Length; i++)
            {
                RunResult run = RunBackend(2, TimeSteps[i], true, cfg.constantGravityMps2);
                MechanicsChecks("B2", run, TimeSteps[i], true);
                Errors(run, reference, cfg, out rateErr[i], out attErr[i], out hDrift[i], out tDrift[i]);
                report.AppendLine("  " + TimeSteps[i].ToString("F4", CultureInfo.InvariantCulture) + "   " + E(rateErr[i]) + "    " + E(attErr[i])
                    + "    " + E(hDrift[i]) + "    " + E(tDrift[i]));
            }
            ConvergenceChecks("B2", "body rates", rateErr);
            ConvergenceChecks("B2", "attitude", attErr);
            ConvergenceChecks("B2", "|H| drift", hDrift);
            ConvergenceChecks("B2", "T drift", tDrift);

            // Sensitivity / single-application control: switch the compensation OFF at one step size.
            RunResult naive = RunBackend(2, 0.005f, false, cfg.constantGravityMps2);
            double naiveRate, naiveAtt, naiveH, naiveT;
            Errors(naive, reference, cfg, out naiveRate, out naiveAtt, out naiveH, out naiveT);
            report.AppendLine("  compensation OFF at dt 0.005: max|dw| " + E(naiveRate) + " deg/s, max att " + E(naiveAtt) + " deg");
            Check(naiveRate > 10.0 * rateErr[2], "B2",
                "control run: without MavSixDoFBody's compensation the error is " + E(naiveRate) + " deg/s, > 10x the compensated " + E(rateErr[2])
                + " - PhysX alone omits w x (I w), so the compensation is required and is not duplicated by the backend");
            Check(naive.maxRateChangeDegS < 1e-3, "B2",
                "control run: with no applied torque PhysX holds the body rates constant (max change " + E(naive.maxRateChangeDegS) + " deg/s)");
        }

        // ------------------------------------------------------------------ case 3

        private static void Case3()
        {
            MavNasaRigidBodyReference.Config nasaEq = BrickConfig(true);
            nasaEq.atmosphereMode = MavNasaRigidBodyReference.AtmosphereMode.Us76Geopotential;
            nasaEq.gravityMode = MavNasaRigidBodyReference.GravityMode.EquatorialJ2Rotating;

            MavNasaRigidBodyReference.State[] nasaRef = MavNasaRigidBodyReference.Solve(nasaEq, BrickInitial(), SampleStep, SampleCount, ReferenceSubsteps);
            MavNasaRigidBodyReference.State[] nasaRefFine = MavNasaRigidBodyReference.Solve(nasaEq, BrickInitial(), SampleStep, SampleCount, 2 * ReferenceSubsteps);

            report.AppendLine();
            report.AppendLine("[B3] Case 3 NASA-equivalent reference (US76 at geopotential altitude; equatorial J2 gravitation minus centripetal term) vs NASA tools");
            double refSelf = 0.0;
            for (int k = 0; k < SampleCount; k++)
                refSelf = Math.Max(refSelf, RateDiffDeg(nasaRef[k], nasaRefFine[k]));
            // Gate basis: the reference's own truncation error (RK4, ~ h vs h/2 difference) must be at
            // least 100x below the smallest difference it is used to judge in case 3 (reference vs
            // SIM 04, ~3e-4 deg/s; Maverick errors >= 1e-2 deg/s). The first R1 run gated this at an
            // unjustified 1e-8 and failed at 1.96e-8; the basis is recorded in NUMERICAL_CONVERGENCE.md.
            Check(refSelf < 1e-6, "B3", "reference converged: h vs h/2 max |dw| " + E(refSelf) + " deg/s < 1e-6 (>= 100x below the smallest compared difference)");
            CompareReferenceToNasa(3, nasaRef, false);
            double maxAltFt = 0.0, maxTasFtS = 0.0;
            for (int k = 0; k < SampleCount; k++)
            {
                double[] row = MavNasaBrickReferenceData.Case3[Sim04][k];
                maxAltFt = Math.Max(maxAltFt, Math.Abs(nasaRef[k].altitudeM / Ft - row[13]));
                maxTasFtS = Math.Max(maxTasFtS, Math.Abs(nasaRef[k].downVelocityMps / Ft - row[14]));
            }
            report.AppendLine("  translational agreement with sim 04: max |dalt| " + E(maxAltFt) + " ft, max |dTAS| " + E(maxTasFtS) + " ft/s (reported)");

            // Maverick-equivalent reference: the assumptions a flat Unity world makes, written independently.
            double g = NasaInitialGravityMps2(3);
            MavNasaRigidBodyReference.Config mavEq = BrickConfig(true);
            mavEq.atmosphereMode = MavNasaRigidBodyReference.AtmosphereMode.StandardLayerAtGeometricAltitude;
            mavEq.gravityMode = MavNasaRigidBodyReference.GravityMode.Constant;
            mavEq.constantGravityMps2 = g;
            MavNasaRigidBodyReference.State[] mavRef = MavNasaRigidBodyReference.Solve(mavEq, BrickInitial(), SampleStep, SampleCount, ReferenceSubsteps);

            report.AppendLine();
            report.AppendLine("[B4] Case 3 Maverick backend vs the Maverick-equivalent reference (constant g " + g.ToString("F6", CultureInfo.InvariantCulture)
                + " m/s^2 = NASA local gravity at t = 0; standard layer formula at geometric altitude)");
            report.AppendLine("  dt       max|dw| deg/s   max att deg     max|dh| m");
            double[] rateErr = new double[TimeSteps.Length];
            double[] attErr = new double[TimeSteps.Length];
            double[] altErr = new double[TimeSteps.Length];
            RunResult finest = null;
            for (int i = 0; i < TimeSteps.Length; i++)
            {
                RunResult run = RunBackend(3, TimeSteps[i], true, g);
                MechanicsChecks("B4", run, TimeSteps[i], true);
                double hd, td;
                Errors(run, mavRef, mavEq, out rateErr[i], out attErr[i], out hd, out td);
                for (int k = 0; k < SampleCount; k++)
                    altErr[i] = Math.Max(altErr[i], Math.Abs(run.samples[k].altitudeM - mavRef[k].altitudeM));
                report.AppendLine("  " + TimeSteps[i].ToString("F4", CultureInfo.InvariantCulture) + "   " + E(rateErr[i]) + "    " + E(attErr[i]) + "    " + E(altErr[i]));

                bool dissipative = true;
                for (int k = 1; k < SampleCount; k++)
                    dissipative &= run.samples[k].kineticEnergy <= run.samples[k - 1].kineticEnergy * (1.0 + 1e-6);
                Check(dissipative, "B4", "dt " + F(TimeSteps[i]) + ": rotational kinetic energy never increases between samples (damping is dissipative)");
                Check(run.firstStepAeroMomentMagnitude == 0.0 && run.firstStepFinite, "B4",
                    "dt " + F(TimeSteps[i]) + ": first step at V = 0 gives a finite, exactly zero aerodynamic moment (q-bar = 0); |M| = " + E(run.firstStepAeroMomentMagnitude));
                if (i == TimeSteps.Length - 1)
                    finest = run;
            }
            ConvergenceChecks("B4", "body rates", rateErr);
            ConvergenceChecks("B4", "attitude", attErr);

            report.AppendLine("  production chain checks on the dt " + F(finest.dt) + " run, sample t = 2 s:");
            Check(finest.momentFormulaRelError < 1e-4, "B4",
                "body's aerodynamic moment equals 1/2 rho V^2 S b Cl(p b/2V') computed here from the body's own state: rel err " + E(finest.momentFormulaRelError));
            Check(finest.maxDensityRelErrGeometric < 1e-5, "B4",
                "MavAtmosphereModel density at the sampled altitudes equals the independent standard-layer formula at GEOMETRIC altitude: max rel err " + E(finest.maxDensityRelErrGeometric));
            report.AppendLine("  FINDING: MavAtmosphereModel vs US76 at GEOPOTENTIAL altitude (the NASA convention): max rel density difference "
                + E(finest.maxDensityRelErrUs76) + " over 30,000 -> " + (finest.samples[SampleCount - 1].altitudeM / Ft).ToString("F0", CultureInfo.InvariantCulture) + " ft");
            Check(finest.maxDensityRelErrUs76 > 1e-4 && finest.maxDensityRelErrUs76 < 5e-3, "B4",
                "the geometric-vs-geopotential altitude convention difference is present and small (expected O(h/R) ~ 1e-3): " + E(finest.maxDensityRelErrUs76));

            // Model-difference budget against NASA (reported, not gated: constant vs altitude-varying gravity, atmosphere convention).
            double rateVsNasa = 0.0, attVsNasa = 0.0;
            for (int k = 0; k < SampleCount; k++)
            {
                double[] row = MavNasaBrickReferenceData.Case3[Sim04][k];
                rateVsNasa = Math.Max(rateVsNasa, Math.Sqrt(Sq(finest.samples[k].pDeg - row[1]) + Sq(finest.samples[k].qDeg - row[2]) + Sq(finest.samples[k].rDeg - row[3])));
                attVsNasa = Math.Max(attVsNasa, MavNasaRigidBodyReference.AttitudeDifferenceRad(finest.samples[k].c, Slice(row, 4, 9)) / Deg);
            }
            report.AppendLine("  Maverick (dt " + F(finest.dt) + ") vs NASA sim 04 directly: max |dw| " + E(rateVsNasa) + " deg/s, max att " + E(attVsNasa)
                + " deg (includes the model differences above; reported, not gated)");
        }

        // ------------------------------------------------------------------ reference vs NASA

        private static void CompareReferenceToNasa(int caseId, MavNasaRigidBodyReference.State[] reference, bool strictPair)
        {
            double[][][] data = caseId == 2 ? MavNasaBrickReferenceData.Case2 : MavNasaBrickReferenceData.Case3;
            string tag = caseId == 2 ? "B1" : "B3";
            report.AppendLine("  sim   max|dw| deg/s   max att deg   (reference minus tool)");
            for (int s = 0; s < data.Length; s++)
            {
                double rate = 0.0, att = 0.0;
                for (int k = 0; k < SampleCount; k++)
                {
                    rate = Math.Max(rate, RateDiffDeg(reference[k], data[s][k]));
                    att = Math.Max(att, MavNasaRigidBodyReference.AttitudeDifferenceRad(MavNasaRigidBodyReference.BodyFromInertial(reference[k]), Slice(data[s][k], 4, 9)) / Deg);
                }
                report.AppendLine("  " + MavNasaBrickReferenceData.Sims[s] + "    " + E(rate) + "      " + E(att));
            }

            if (strictPair)
            {
                // Sims 01 and 04 agree with each other to ~1e-10 deg/s in this case; the reference must sit with them.
                double worst = 0.0, worstAtt = 0.0, pairSpread = 0.0;
                for (int k = 0; k < SampleCount; k++)
                {
                    worst = Math.Max(worst, Math.Max(RateDiffDeg(reference[k], data[Sim01][k]), RateDiffDeg(reference[k], data[Sim04][k])));
                    worstAtt = Math.Max(worstAtt, Math.Max(
                        MavNasaRigidBodyReference.AttitudeDifferenceRad(MavNasaRigidBodyReference.BodyFromInertial(reference[k]), Slice(data[Sim01][k], 4, 9)),
                        MavNasaRigidBodyReference.AttitudeDifferenceRad(MavNasaRigidBodyReference.BodyFromInertial(reference[k]), Slice(data[Sim04][k], 4, 9))));
                    pairSpread = Math.Max(pairSpread, Math.Sqrt(Sq(data[Sim01][k][1] - data[Sim04][k][1]) + Sq(data[Sim01][k][2] - data[Sim04][k][2]) + Sq(data[Sim01][k][3] - data[Sim04][k][3])));
                }
                report.AppendLine("  sims 01 vs 04 spread " + E(pairSpread) + " deg/s");
                Check(worst < 1e-6, tag, "reference agrees with sims 01 and 04 (which agree with each other to " + E(pairSpread) + "): max |dw| " + E(worst) + " deg/s < 1e-6");
                Check(worstAtt < 1e-6, tag, "reference attitude agrees with sims 01 and 04: max " + E(worstAtt) + " rad < 1e-6 (confirms the inertial-rate and rotating-NED interpretation)");
            }

            // Envelope rule: the reference may be no farther from sim 04 than the NASA tools are from each other.
            // Sim 02 attitude is excluded, as NASA excluded SIM 2 Euler angles (Vol II §E.1.2).
            bool rateInside = true, attInside = true;
            double worstRateRatio = 0.0, worstAttRatio = 0.0;
            for (int k = 0; k < SampleCount; k++)
            {
                double rateSpread = 0.0, attSpread = 0.0;
                for (int s = 0; s < data.Length; s++)
                {
                    if (s == Sim04) continue;
                    rateSpread = Math.Max(rateSpread, RateDiffRows(data[s][k], data[Sim04][k]));
                    if (s != Sim02)
                        attSpread = Math.Max(attSpread, MavNasaRigidBodyReference.AttitudeDifferenceRad(Slice(data[s][k], 4, 9), Slice(data[Sim04][k], 4, 9)));
                }
                double rd = RateDiffDeg(reference[k], data[Sim04][k]);
                double ad = MavNasaRigidBodyReference.AttitudeDifferenceRad(MavNasaRigidBodyReference.BodyFromInertial(reference[k]), Slice(data[Sim04][k], 4, 9));
                rateInside &= rd <= rateSpread + 1e-9;
                attInside &= ad <= attSpread + 1e-9;
                worstRateRatio = Math.Max(worstRateRatio, rd / (rateSpread + 1e-9));
                worstAttRatio = Math.Max(worstAttRatio, ad / (attSpread + 1e-9));
            }
            Check(rateInside, tag, "body rates: reference-to-sim-04 distance within the NASA inter-tool spread at every sample (worst ratio " + worstRateRatio.ToString("F3", CultureInfo.InvariantCulture) + ")");
            Check(attInside, tag, "attitude: reference-to-sim-04 distance within the NASA inter-tool spread at every sample, SIM 2 excluded as in Vol II E.1.2 (worst ratio " + worstAttRatio.ToString("F3", CultureInfo.InvariantCulture) + ")");
        }

        // ------------------------------------------------------------------ backend run

        private sealed class Sample
        {
            public double pDeg, qDeg, rDeg;
            public double[] c;
            public double altitudeM;
            public double kineticEnergy;
            public double hx, hy, hz;
        }

        private sealed class RunResult
        {
            public float dt;
            public Sample[] samples = new Sample[SampleCount];
            public int steps;
            public int loadApplications;
            public int gyroAppliedSteps;
            public int rejected;
            public bool allFinite = true;
            public bool dampingZero;
            public bool inertiaMatches;
            public bool massMatches;
            public float maxAngularVelocity;
            public double maxRateChangeDegS;
            public double firstStepAeroMomentMagnitude = -1.0;
            public bool firstStepFinite;
            public double momentFormulaRelError = double.NaN;
            public double maxDensityRelErrGeometric;
            public double maxDensityRelErrUs76;
        }

        private static RunResult RunBackend(int caseId, float dt, bool gyroscopicCompensation, double gravityMps2)
        {
            MavNasaRigidBodyReference.Config cfg = BrickConfig(caseId == 3);
            RunResult result = new RunResult { dt = dt };
            GameObject go = new GameObject("nasa-brick-case" + caseId);
            go.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                Physics.simulationMode = SimulationMode.Script;
                Physics.gravity = new Vector3(0f, -(float)gravityMps2, 0f);

                go.transform.position = new Vector3(0f, (float)cfg.initialAltitudeM, 0f);
                go.transform.rotation = Quaternion.identity; // Unity z = north, x = east, y = up => body x north, y east, z down

                Rigidbody rb = go.AddComponent<Rigidbody>();
                rb.interpolation = RigidbodyInterpolation.None;
                rb.sleepThreshold = 0f;          // a test-rig setting: the slowly tumbling brick must never be put to sleep
                rb.automaticCenterOfMass = false;
                rb.automaticInertiaTensor = false;

                MavNasaBrickAeroModel aero = go.AddComponent<MavNasaBrickAeroModel>();
                aero.referenceGeometry = new MavAeroReferenceGeometry
                {
                    wingAreaM2 = (float)cfg.areaM2,
                    wingSpanM = (float)cfg.spanM,
                    meanAerodynamicChordM = (float)cfg.chordM
                };
                aero.dampingOn = caseId == 3;
                aero.clp = (float)cfg.clp; aero.clr = (float)cfg.clr; aero.cmq = (float)cfg.cmq;
                aero.cnp = (float)cfg.cnp; aero.cnr = (float)cfg.cnr;
                aero.airspeedFloorMps = (float)cfg.airspeedFloorMps;

                MavSixDoFBody body = go.AddComponent<MavSixDoFBody>();
                body.aerodynamicModel = aero;
                // Mass properties through the production path. Unity local axes: x = body y, y = -body z, z = body x.
                body.massProperties = new MavMassProperties
                {
                    massKg = (float)(MavNasaBrickReferenceData.MassSlug * SlugKg),
                    centerOfMassLocalM = Vector3.zero,
                    inertiaTensorKgM2 = new Vector3((float)cfg.iyy, (float)cfg.izz, (float)cfg.ixx),
                    inertiaTensorRotationEulerDeg = Vector3.zero
                };
                body.applyMassPropertiesOnEnable = true;
                body.simulationEnabled = true;
                body.requireOperationalReadinessForLoadApplication = false; // bench body: no aircraft readiness applies
                body.applyBackendGyroscopicCompensation = gyroscopicCompensation;
                body.acknowledgeIncompleteAngularDynamicsForTesting = !gyroscopicCompensation;

                // Initial inertial body rates; axial-vector mapping written here independently of
                // MavFlightDynamicsMath: p -> -Unity z, q -> -Unity x, r -> +Unity y.
                MavNasaRigidBodyReference.State init = BrickInitial();
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = new Vector3((float)-init.q, (float)init.r, (float)-init.p);
                Physics.SyncTransforms();

                int stepsPerSample = Mathf.RoundToInt((float)(SampleStep / dt));
                int totalSteps = stepsPerSample * (SampleCount - 1);
                result.samples[0] = Measure(go, rb, cfg);
                double p0 = result.samples[0].pDeg, q0 = result.samples[0].qDeg, r0 = result.samples[0].rDeg;

                for (int step = 1; step <= totalSteps; step++)
                {
                    float time = step * dt;
                    int appliedBefore = body.debugLoadApplications;
                    body.StepPhysicsForValidation(dt, time);
                    if (step == 1)
                    {
                        result.massMatches = Math.Abs(rb.mass - body.massProperties.massKg) < 1e-6f;
                        Vector3 it = rb.inertiaTensor;
                        result.inertiaMatches = Math.Abs(it.x - cfg.iyy) < 1e-7 * Math.Max(1.0, cfg.iyy) + 1e-9
                                                && Math.Abs(it.y - cfg.izz) < 1e-7 * Math.Max(1.0, cfg.izz) + 1e-9
                                                && Math.Abs(it.z - cfg.ixx) < 1e-7 * Math.Max(1.0, cfg.ixx) + 1e-9;
                        result.dampingZero = rb.angularDamping == 0f && rb.linearDamping == 0f;
                        result.maxAngularVelocity = rb.maxAngularVelocity;
                        Vector3 m = body.debugExternalMomentAeroBodyNm;
                        result.firstStepAeroMomentMagnitude = m.magnitude;
                        result.firstStepFinite = Finite(m) && Finite(body.debugFinalMomentAeroBodyNm);
                    }
                    if (body.debugLoadApplications == appliedBefore + 1 && body.debugGyroscopicApplied)
                        result.gyroAppliedSteps++;
                    if (!Finite(body.debugFinalMomentAeroBodyNm) || !Finite(rb.angularVelocity) || !Finite(go.transform.position))
                        result.allFinite = false;

                    if (caseId == 3 && step == stepsPerSample)
                        result.momentFormulaRelError = MomentFormulaError(body, cfg);

                    Physics.Simulate(dt);
                    result.steps++;

                    if (step % stepsPerSample == 0)
                    {
                        Sample s = Measure(go, rb, cfg);
                        result.samples[step / stepsPerSample] = s;
                        result.maxRateChangeDegS = Math.Max(result.maxRateChangeDegS,
                            Math.Sqrt(Sq(s.pDeg - p0) + Sq(s.qDeg - q0) + Sq(s.rDeg - r0)));
                        if (caseId == 3)
                        {
                            MavAtmosphereSample a = MavAtmosphereModel.Sample(go.transform.position.y);
                            double geo = MavNasaRigidBodyReference.Density(MavNasaRigidBodyReference.AtmosphereMode.StandardLayerAtGeometricAltitude, go.transform.position.y);
                            double us76 = MavNasaRigidBodyReference.Density(MavNasaRigidBodyReference.AtmosphereMode.Us76Geopotential, go.transform.position.y);
                            result.maxDensityRelErrGeometric = Math.Max(result.maxDensityRelErrGeometric, Math.Abs(a.densityKgM3 - geo) / geo);
                            result.maxDensityRelErrUs76 = Math.Max(result.maxDensityRelErrUs76, Math.Abs(a.densityKgM3 - us76) / us76);
                        }
                    }
                }
                result.loadApplications = body.debugLoadApplications;
                result.rejected = body.debugRejectedDuplicateApplications + body.debugRejectedNonFiniteApplications
                                  + body.debugRejectedNotLiveReadyApplications + body.debugRejectedEnvironmentSteps
                                  + body.debugRejectedGravityOwnershipSteps + body.debugRejectedNotOwnerApplications;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
            return result;
        }

        private static Sample Measure(GameObject go, Rigidbody rb, MavNasaRigidBodyReference.Config cfg)
        {
            Transform t = go.transform;
            Vector3 local = t.InverseTransformDirection(rb.angularVelocity);
            Sample s = new Sample
            {
                pDeg = -local.z / Deg,
                qDeg = -local.x / Deg,
                rDeg = local.y / Deg,
                altitudeM = t.position.y
            };
            Vector3 f = t.forward, r = t.right, u = t.up;
            // C(body <- NED0), row i = body axis i on (N, E, D); Unity N = +z, E = +x, D = -y.
            s.c = new double[] { f.z, f.x, -f.y, r.z, r.x, -r.y, -u.z, -u.x, u.y };
            double p = s.pDeg * Deg, q = s.qDeg * Deg, rr = s.rDeg * Deg;
            s.kineticEnergy = MavNasaRigidBodyReference.RotationalKineticEnergy(cfg.ixx, cfg.iyy, cfg.izz, p, q, rr);
            MavNasaRigidBodyReference.InertialAngularMomentum(s.c, cfg.ixx, cfg.iyy, cfg.izz, p, q, rr, out s.hx, out s.hy, out s.hz);
            return s;
        }

        private static double MomentFormulaError(MavSixDoFBody body, MavNasaRigidBodyReference.Config cfg)
        {
            MavFlightState st = body.debugState;
            double v = st.trueAirspeedMps;
            double rho = body.debugAtmosphere.densityKgM3;
            double vn = Math.Max(v, cfg.airspeedFloorMps);
            double qbar = 0.5 * rho * v * v;
            Vector3 w = st.aeroBodyRatesRadSec;
            double l = qbar * cfg.areaM2 * cfg.spanM * (cfg.clp * w.x * cfg.spanM / (2 * vn) + cfg.clr * w.z * cfg.spanM / (2 * vn));
            double m = qbar * cfg.areaM2 * cfg.chordM * (cfg.cmq * w.y * cfg.chordM / (2 * vn));
            double n = qbar * cfg.areaM2 * cfg.spanM * (cfg.cnp * w.x * cfg.spanM / (2 * vn) + cfg.cnr * w.z * cfg.spanM / (2 * vn));
            Vector3 got = body.debugExternalMomentAeroBodyNm;
            double norm = Math.Sqrt(l * l + m * m + n * n);
            double diff = Math.Sqrt(Sq(got.x - l) + Sq(got.y - m) + Sq(got.z - n));
            return norm > 0 ? diff / norm : diff;
        }

        private static void Errors(RunResult run, MavNasaRigidBodyReference.State[] reference, MavNasaRigidBodyReference.Config cfg,
            out double rateErr, out double attErr, out double hDrift, out double tDrift)
        {
            rateErr = attErr = hDrift = tDrift = 0.0;
            Sample s0 = run.samples[0];
            double h0 = Math.Sqrt(s0.hx * s0.hx + s0.hy * s0.hy + s0.hz * s0.hz);
            for (int k = 1; k < SampleCount; k++)
            {
                Sample s = run.samples[k];
                MavNasaRigidBodyReference.State r = reference[k];
                rateErr = Math.Max(rateErr, Math.Sqrt(Sq(s.pDeg - r.p / Deg) + Sq(s.qDeg - r.q / Deg) + Sq(s.rDeg - r.r / Deg)));
                attErr = Math.Max(attErr, MavNasaRigidBodyReference.AttitudeDifferenceRad(s.c, MavNasaRigidBodyReference.BodyFromInertial(r)) / Deg);
                double dh = Math.Sqrt(Sq(s.hx - s0.hx) + Sq(s.hy - s0.hy) + Sq(s.hz - s0.hz));
                hDrift = Math.Max(hDrift, dh / h0);
                tDrift = Math.Max(tDrift, Math.Abs(s.kineticEnergy - s0.kineticEnergy) / s0.kineticEnergy);
            }
        }

        private static void MechanicsChecks(string tag, RunResult run, float dt, bool gyro)
        {
            string p = "dt " + F(dt) + ": ";
            Check(run.loadApplications == run.steps && run.rejected == 0, tag, p + "one load application per step (" + run.loadApplications + "/" + run.steps + "), no refusals");
            Check(!gyro || run.gyroAppliedSteps == run.steps, tag, p + "gyroscopic compensation applied exactly once in every step (" + run.gyroAppliedSteps + "/" + run.steps + ")");
            Check(run.allFinite, tag, p + "all moments, rates and positions finite");
            Check(run.dampingZero, tag, p + "Unity angular and linear damping are zero (no general-purpose damping)");
            Check(run.massMatches && run.inertiaMatches, tag, p + "Rigidbody mass and principal inertia equal the NASA values after the production MavMassProperties path (no floor binding)");
            Check(run.maxAngularVelocity >= 7f, tag, p + "Rigidbody.maxAngularVelocity " + run.maxAngularVelocity.ToString("F1", CultureInfo.InvariantCulture)
                + " rad/s is far above the brick's ~0.65 rad/s");
        }

        /// <summary>
        /// First-order convergence gate. PhysX integrates applied torque with a first-order explicit
        /// (semi-implicit Euler) step, so halving dt should halve the error: observed order
        /// p = log2(e(dt)/e(dt/2)) must lie in [0.7, 1.3] for the 0.02->0.01 and 0.01->0.005 pairs
        /// and the error must decrease monotonically. The finest pair is reported only, because
        /// float32 rounding in PhysX can flatten it.
        /// </summary>
        private static void ConvergenceChecks(string tag, string what, double[] errors)
        {
            StringBuilder orders = new StringBuilder();
            bool monotone = true;
            bool ordersOk = true;
            for (int i = 0; i + 1 < errors.Length; i++)
            {
                double p = Math.Log(errors[i] / errors[i + 1], 2.0);
                orders.Append((i > 0 ? ", " : "") + TimeSteps[i].ToString("G3", CultureInfo.InvariantCulture) + "->"
                              + TimeSteps[i + 1].ToString("G3", CultureInfo.InvariantCulture) + ": " + p.ToString("F3", CultureInfo.InvariantCulture));
                if (i < 2)
                {
                    monotone &= errors[i + 1] < errors[i];
                    ordersOk &= p >= 0.7 && p <= 1.3;
                }
            }
            Check(monotone && ordersOk, tag, what + ": first-order convergence (observed order " + orders + ")");
        }

        // ------------------------------------------------------------------ helpers

        private static double NasaInitialGravityMps2(int caseId)
        {
            double[][][] data = caseId == 2 ? MavNasaBrickReferenceData.Case2 : MavNasaBrickReferenceData.Case3;
            return data[Sim04][0][16] * Ft;
        }

        private static double RateDiffDeg(MavNasaRigidBodyReference.State s, double[] row)
        {
            return Math.Sqrt(Sq(s.p / Deg - row[1]) + Sq(s.q / Deg - row[2]) + Sq(s.r / Deg - row[3]));
        }

        private static double RateDiffDeg(MavNasaRigidBodyReference.State a, MavNasaRigidBodyReference.State b)
        {
            return Math.Sqrt(Sq((a.p - b.p) / Deg) + Sq((a.q - b.q) / Deg) + Sq((a.r - b.r) / Deg));
        }

        private static double RateDiffRows(double[] a, double[] b)
        {
            return Math.Sqrt(Sq(a[1] - b[1]) + Sq(a[2] - b[2]) + Sq(a[3] - b[3]));
        }

        private static double[] Slice(double[] a, int start, int count)
        {
            double[] o = new double[count];
            Array.Copy(a, start, o, 0, count);
            return o;
        }

        private static double Sq(double x) { return x * x; }

        private static bool Finite(Vector3 v)
        {
            return !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
        }

        private static string E(double x) { return x.ToString("E3", CultureInfo.InvariantCulture); }
        private static string F(float x) { return x.ToString("G4", CultureInfo.InvariantCulture); }

        private static void Check(bool ok, string tag, string message)
        {
            if (ok) passed++; else failed++;
            report.AppendLine("  " + (ok ? "PASS" : "FAIL") + " [" + tag + "] " + message);
        }
    }
}
#endif
