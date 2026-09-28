#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using MaverickFresh.FlightDynamics;
using MaverickFresh.FlightDynamics.F15;
using Object = UnityEngine.Object;

namespace MaverickFresh.FlightDynamics.Validation
{
    /// <summary>
    /// F-15 PILOT PHYSICS R2 - headless validation of the research-model actuator lags (the Play Mode flight test is
    /// MavF15PilotPhysicsR2FlightValidationRunner).
    ///
    ///   [A1] source data: 20 / 28 / 20 / 20 1/s exactly as Davison 1992 STATE12 prints them, labelled, not a rate
    ///   [A2] R1 unchanged: the no-dynamics step is bit-identical to the four-argument step the frozen suites use
    ///   [A3] lag mathematics: exact zero-order-hold step, time constant, no overshoot, unit static gain, dt-independent
    ///   [A4] differential tail: 0.3 x the commanded aileron through the stabilator lag, as F(12)
    ///   [A5] bounds and fail-safes: travel first, NaN / Infinity / dt edge cases, no NaN out
    ///   [A6] rig wiring: R2 is the default; R1 only when named and without lag; R2 lags on the actuator; ownership still granted
    ///   [A7] trim: point 36 is still an equilibrium of the R2 plant - residuals reported, identical to R1
    ///   [A8] response through the real rig (SHADOW): the stabilator follows the lag; moment sign unchanged
    ///   [A9] source scan: the R2 layer writes no physics and names no research-only type
    /// </summary>
    public static class MavF15PilotPhysicsR2Validation
    {
        private const float Dt = 0.02f;

        public static string RunAll(out int passed, out int failed)
        {
            passed = 0;
            failed = 0;
            StringBuilder report = new StringBuilder(16 * 1024);
            report.AppendLine("F-15 PILOT PHYSICS R2 (research-model actuator lags) - headless validation (" + MavF15PilotControlledIdentity.ConfigurationId + ")");
            report.AppendLine("Source: " + MavF15ResearchModelActuatorLags.Citation);
            report.AppendLine("Frozen research aerodynamics unchanged. THIS IS NOT THE F-15 FCS and not production F-15 actuator data.");

            List<Object> created = new List<Object>();
            try
            {
                SourceData(report, ref passed, ref failed);
                R1Unchanged(report, ref passed, ref failed);
                LagMathematics(report, ref passed, ref failed);
                DifferentialTail(report, ref passed, ref failed);
                BoundsAndFailSafes(report, ref passed, ref failed);
                RigWiring(created, report, ref passed, ref failed);
                Trim(created, report, ref passed, ref failed);
                ResponseThroughRig(created, report, ref passed, ref failed);
                SourceScan(report, ref passed, ref failed);
            }
            catch (Exception e)
            {
                failed++;
                report.AppendLine("  FAIL  exception: " + e);
            }
            finally
            {
                for (int i = 0; i < created.Count; i++)
                {
                    if (created[i] != null)
                        Object.DestroyImmediate(created[i]);
                }
            }

            report.AppendLine();
            report.AppendLine("RESULT: " + (failed == 0 ? "PASS" : "FAIL") + "  passed=" + passed + " failed=" + failed);
            return report.ToString();
        }

        // ---------------------------------------------------------------- [A1]

        private static void SourceData(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[A1] Source data");
            MavF15ActuatorDynamics d = MavF15ResearchModelActuatorLags.Create();
            Record(d.symmetricStabilator.lagPerSec == 20f && d.aileron.lagPerSec == 20f && d.differentialStabilator.lagPerSec == 20f
                   && d.rudder.lagPerSec == 28f,
                "stabilator 20, aileron 20, differential tail 20, rudder 28 1/s - Davison STATE12 F(9), F(11), F(12), F(10) as printed",
                report, ref passed, ref failed);

            bool labelled = true;
            for (int i = 0; i < 4; i++)
            {
                MavF15ChannelLag lag = d.Get((MavF15SurfaceChannel)i);
                string r;
                labelled &= lag.HasLag && lag.IsSelfConsistent(out r) && lag.provenance == MavEngineDataProvenance.PublicReference
                            && lag.sourceNote.Contains("ADA256613") && lag.sourceNote.Contains("p.97")
                            && lag.sourceNote.Contains("not production F-15 actuator data") && lag.sourceNote.Contains("not a rate limit");
            }

            Record(labelled, "every channel: PublicReference, cites DTIC ADA256613 p.97, labelled research-model bandwidth - not production F-15 data, not a rate limit",
                report, ref passed, ref failed);

            MavF15SurfaceLimits travel = MavF15GameplayControlAuthority.V1().ToActuatorTravel();
            bool noRate = true;
            for (int i = 0; i < 4; i++)
                noRate &= !travel.Get((MavF15SurfaceChannel)i).HasSourcedRate;
            Record(noRate && !MavF15ActuatorDynamics.None.AnyLag && !MavF15PilotPhysics.ActuatorDynamicsFor(MavF15PilotPhysicsRevision.R1InstantaneousSurfaces).AnyLag
                   && MavF15PilotPhysics.ActuatorDynamicsFor(MavF15PilotPhysicsRevision.R2SourceActuatorLags).AnyLag,
                "no rate limit is added (none is sourced); R1 carries no lag, R2 carries the lags", report, ref passed, ref failed);

            MavF15ChannelLag contradiction = new MavF15ChannelLag { lagPerSec = 5f, provenance = MavEngineDataProvenance.Unavailable };
            MavF15ChannelLag bad = new MavF15ChannelLag { lagPerSec = float.NaN, provenance = MavEngineDataProvenance.PublicReference };
            string r1, r2;
            bool contradictionConsistent = contradiction.IsSelfConsistent(out r1);
            bool badConsistent = bad.IsSelfConsistent(out r2);
            Record(!contradiction.HasLag && !contradictionConsistent && !bad.HasLag && !badConsistent,
                "an undeclared lag is not a lag, and a declared NaN lag is refused (" + r1 + "; " + r2 + ")", report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [A2]

        private static void R1Unchanged(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[A2] R1 is unchanged");
            MavF15SurfaceLimits travel = MavF15GameplayControlAuthority.V1().ToActuatorTravel();
            MavF15SurfaceLimits rated = travel;
            rated.symmetricStabilator.rateLimitDegSec = 30f;
            rated.symmetricStabilator.rateProvenance = MavEngineDataProvenance.MaverickTuning;
            System.Random rng = new System.Random(836);
            bool identical = true;
            int cases = 0;
            foreach (MavF15SurfaceLimits limits in new[] { travel, rated })
            {
                foreach (float dt in new[] { 0f, 0.005f, 0.02f, 0.1f, -1f })
                {
                    for (int k = 0; k < 50; k++)
                    {
                        MavF15SurfaceState from = RandomState(rng), to = RandomState(rng);
                        MavF15SurfaceState a = MavF15ControlActuator.StepChannels(from, to, limits, dt);
                        MavF15SurfaceState b = MavF15ControlActuator.StepChannels(from, to, limits, MavF15ActuatorDynamics.None, dt);
                        for (int c = 0; c < 4; c++)
                            identical &= BitEqual(a.Get((MavF15SurfaceChannel)c), b.Get((MavF15SurfaceChannel)c));
                        cases++;
                    }
                }
            }

            Record(identical, "with no dynamics the new step equals the four-argument step bit for bit (" + cases + " cases, with and without a rate, dt 0 / 0.005 / 0.02 / 0.1 / negative)",
                report, ref passed, ref failed);

            MavF15ActuatorDynamics oneLag = MavF15ActuatorDynamics.None;
            oneLag.rudder = MavF15ResearchModelActuatorLags.Create().rudder;
            MavF15SurfaceState s0 = new MavF15SurfaceState { symmetricStabilatorDeg = -10f, aileronDeg = 0f, differentialStabilatorDeg = 0f, rudderDeg = 0f };
            MavF15SurfaceState s1 = new MavF15SurfaceState { symmetricStabilatorDeg = -20f, aileronDeg = 10f, differentialStabilatorDeg = 3f, rudderDeg = 10f };
            MavF15SurfaceState mixed = MavF15ControlActuator.StepChannels(s0, s1, travel, oneLag, Dt);
            Record(mixed.symmetricStabilatorDeg == -20f && mixed.aileronDeg == 10f && mixed.differentialStabilatorDeg == 3f && mixed.rudderDeg > 0f && mixed.rudderDeg < 10f,
                "the lag is per channel: a lagged rudder beside instant channels leaves the others exactly instant", report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [A3]

        private static void LagMathematics(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[A3] Lag mathematics");
            const float lambda = 20f;
            float one = MavF15PilotPhysics.StepFirstOrderLag(0f, 10f, lambda, Dt);
            float expected = (float)(10.0 * (1.0 - Math.Exp(-lambda * Dt)));
            Record(Math.Abs(one - expected) < 1e-5f,
                "one 0.02 s step toward a 10 deg command reaches " + one.ToString("F5") + " deg = 10(1 - e^-0.4) = " + expected.ToString("F5") + " (exact zero-order-hold solution)",
                report, ref passed, ref failed);

            // Time constant, dt independence: the same physical time reached in 1x, 2x, 4x the steps.
            float[] atTau = new float[3];
            float[] dts = { 0.01f, 0.005f, 0.0025f };
            for (int i = 0; i < 3; i++)
            {
                float x = 0f;
                int n = Mathf.RoundToInt(0.05f / dts[i]);
                for (int k = 0; k < n; k++)
                    x = MavF15PilotPhysics.StepFirstOrderLag(x, 1f, lambda, dts[i]);
                atTau[i] = x;
            }

            float truth = (float)(1.0 - Math.Exp(-1.0));
            Record(Math.Abs(atTau[0] - truth) < 2e-6f && Math.Abs(atTau[1] - truth) < 2e-6f && Math.Abs(atTau[2] - truth) < 2e-6f,
                "at one time constant (0.05 s) the response is 1 - e^-1 = " + truth.ToString("F6") + " at dt 0.01 / 0.005 / 0.0025: "
                + atTau[0].ToString("F6") + " / " + atTau[1].ToString("F6") + " / " + atTau[2].ToString("F6") + " - exact, so no dt error of its own",
                report, ref passed, ref failed);

            bool monotone = true, neverOver = true;
            float y = -5f, previous = -5f;
            for (int k = 0; k < 200; k++)
            {
                y = MavF15PilotPhysics.StepFirstOrderLag(y, 7f, 28f, Dt);
                monotone &= y >= previous;
                neverOver &= y <= 7f;
                previous = y;
            }

            Record(monotone && neverOver && Math.Abs(y - 7f) < 1e-5f,
                "approach is monotonic, never overshoots, and settles on the command (static gain 1: " + y.ToString("F6") + " of 7)", report, ref passed, ref failed);

            float stab95 = TimeTo(0.95f, 20f), rud95 = TimeTo(0.95f, 28f);
            Record(Math.Abs(stab95 - 0.16f) < 0.021f && Math.Abs(rud95 - 0.12f) < 0.021f,
                "95 % of a step at dt 0.02: stabilator/aileron " + stab95.ToString("F2") + " s (3 tau = 0.150), rudder " + rud95.ToString("F2") + " s (3 tau = 0.107)",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [A4]

        private static void DifferentialTail(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[A4] Differential tail through the stabilator lag (Davison F(12))");
            MavF15GameplayControlAuthority authority = MavF15GameplayControlAuthority.V1();
            MavF15SurfaceLimits travel = authority.ToActuatorTravel();
            MavF15ActuatorDynamics lags = MavF15ResearchModelActuatorLags.Create();
            float bias = (float)MavF15PilotTrimStart.TableViiPoint36().symmetricStabilatorDeg;
            MavF15PilotControlSolution sol = MavF15PilotControlMapping.Solve(new MavPilotCommand { roll = 0.5f }, authority, bias);
            MavF15SurfaceState x = MavF15PilotControlMapping.Solve(MavPilotCommand.Neutral, authority, bias).requested;
            float worst = 0f;
            for (int k = 0; k < 25; k++)
            {
                x = MavF15ControlActuator.StepChannels(x, sol.requested, travel, lags, Dt);
                worst = Mathf.Max(worst, Mathf.Abs(x.differentialStabilatorDeg - 0.3f * x.aileronDeg));
            }

            Record(Math.Abs(sol.requested.differentialStabilatorDeg - 0.3f * sol.requested.aileronDeg) < 1e-5f && worst < 1e-5f,
                "command diff = 0.3 x commanded aileron, and with both lagged at 20 1/s the actual diff stays 0.3 x the actual aileron at every step (worst "
                + worst.ToString("E1") + " deg) - F(12)=20.*(.3*CDAILD-DDTD)", report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [A5]

        private static void BoundsAndFailSafes(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[A5] Travel first, then the lag; fail-safe inputs");
            MavF15SurfaceLimits travel = MavF15GameplayControlAuthority.V1().ToActuatorTravel();
            MavF15ActuatorDynamics lags = MavF15ResearchModelActuatorLags.Create();
            bool anyRefused;
            MavF15SurfaceState wild = new MavF15SurfaceState { symmetricStabilatorDeg = 60f, aileronDeg = -90f, differentialStabilatorDeg = 40f, rudderDeg = 45f };
            MavF15SurfaceState bounded = travel.Clamp(wild, out anyRefused);
            MavF15SurfaceState x = new MavF15SurfaceState { symmetricStabilatorDeg = -10f };
            bool inside = true;
            for (int k = 0; k < 100; k++)
            {
                x = MavF15ControlActuator.StepChannels(x, bounded, travel, lags, Dt);
                inside &= x.symmetricStabilatorDeg <= -5f && x.symmetricStabilatorDeg >= -25f && Mathf.Abs(x.aileronDeg) <= 20f
                          && Mathf.Abs(x.differentialStabilatorDeg) <= 6f && Mathf.Abs(x.rudderDeg) <= 15f;
            }

            Record(inside && Math.Abs(x.symmetricStabilatorDeg + 5f) < 1e-4f && Math.Abs(x.rudderDeg - 15f) < 1e-4f,
                "a command far outside the travel is clamped first; the lagged surface approaches the envelope edge and never passes it",
                report, ref passed, ref failed);

            float nanCurrent = MavF15PilotPhysics.StepFirstOrderLag(float.NaN, 3f, 20f, Dt);
            float infCurrent = MavF15PilotPhysics.StepFirstOrderLag(float.PositiveInfinity, 3f, 20f, Dt);
            float zeroDt = MavF15PilotPhysics.StepFirstOrderLag(1f, 3f, 20f, 0f);
            float nanDt = MavF15PilotPhysics.StepFirstOrderLag(1f, 3f, 20f, float.NaN);
            float negDt = MavF15PilotPhysics.StepFirstOrderLag(1f, 3f, 20f, -0.02f);
            float infDt = MavF15PilotPhysics.StepFirstOrderLag(1f, 3f, 20f, float.PositiveInfinity);
            float hugeLambda = MavF15PilotPhysics.StepFirstOrderLag(1f, 3f, 1e9f, Dt);
            Record(nanCurrent == 3f && infCurrent == 3f && zeroDt == 1f && nanDt == 1f && negDt == 1f && infDt == 1f && hugeLambda == 3f,
                "non-finite current snaps to the (bounded) command; zero / NaN / negative / infinite dt holds; an enormous bandwidth lands exactly on the command, never past it",
                report, ref passed, ref failed);

            MavF15ControlActuator actuator = new GameObject("a5-actuator").AddComponent<MavF15ControlActuator>();
            try
            {
                actuator.gameObject.hideFlags = HideFlags.HideAndDontSave;
                actuator.limits = travel;
                actuator.dynamics = lags;
                actuator.requested = MavF15RequestedSurfaceState.From(new MavF15SurfaceState { symmetricStabilatorDeg = float.NaN, aileronDeg = float.PositiveInfinity });
                bool finite = true;
                for (int k = 0; k < 20; k++)
                {
                    actuator.StepActuator(Dt);
                    finite &= actuator.ActualF15SurfaceState.channels.IsFinite();
                }

                Record(finite, "a NaN / Infinity request through the lagged actuator: surfaces stay finite (the request is replaced by neutral, the lag heads there)",
                    report, ref passed, ref failed);
            }
            finally
            {
                Object.DestroyImmediate(actuator.gameObject);
            }
        }

        // ---------------------------------------------------------------- [A6]

        private static void RigWiring(List<Object> created, StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[A6] Rig wiring");
            MavF15PilotControlledRig normal = Rig(created, "a6-default", MavF15PilotControlMode.AssistedV2, null);
            MavF15PilotControlledRig normalV1 = MavF15PilotControlledRig.Create("a6-default-v1", MavF15PilotCommandSourceKind.Scripted, false);
            normalV1.gameObject.hideFlags = HideFlags.HideAndDontSave;
            normalV1.EnsureStack();
            created.Add(normalV1.gameObject);
            MavF15PilotControlledRig r1 = Rig(created, "a6-r1", MavF15PilotControlMode.AssistedV2, MavF15PilotPhysicsRevision.R1InstantaneousSurfaces);
            MavF15PilotControlledRig r2 = Rig(created, "a6-r2", MavF15PilotControlMode.AssistedV2, MavF15PilotPhysicsRevision.R2SourceActuatorLags);
            MavF15PilotControlledRig r2v1 = Rig(created, "a6-r2v1", MavF15PilotControlMode.DirectV1, MavF15PilotPhysicsRevision.R2SourceActuatorLags);

            // The default is R2: a fresh profile, and both Create overloads that name no revision, fly the lags. The two
            // prefabs serialize no revision, so they take the same field default. R1 is reached only by naming it.
            GameObject bare = new GameObject("a6-bare-profile") { hideFlags = HideFlags.HideAndDontSave };
            created.Add(bare);
            MavF15PilotPhysicsRevision fresh = bare.AddComponent<MavF15PilotControlledFlightDynamicsProfile>().physicsRevision;
            const MavF15PilotPhysicsRevision R2 = MavF15PilotPhysicsRevision.R2SourceActuatorLags;
            string prefabNote;
            bool prefabsR2 = PrefabsDefaultToR2(out prefabNote);
            Record(fresh == R2 && normal.profile.physicsRevision == R2 && normal.actuator.dynamics.AnyLag
                   && normalV1.profile.physicsRevision == R2 && normalV1.actuator.dynamics.AnyLag && prefabsR2
                   && r1.profile.physicsRevision == MavF15PilotPhysicsRevision.R1InstantaneousSurfaces && !r1.actuator.dynamics.AnyLag,
                "the default is R2: a fresh profile and the normal rig (V2 and V1 Create with no revision) carry the lags; " + prefabNote
                + "; R1 is explicit: a rig created with R1 has no lag", report, ref passed, ref failed);
            MavF15ActuatorDynamics d = r2.actuator.dynamics;
            Record(d.symmetricStabilator.lagPerSec == 20f && d.aileron.lagPerSec == 20f && d.differentialStabilator.lagPerSec == 20f && d.rudder.lagPerSec == 28f
                   && r2v1.actuator.dynamics.AnyLag && r2.actuator.limits.symmetricStabilator.minDeg == r1.actuator.limits.symmetricStabilator.minDeg,
                "an R2 rig (V2 or V1 law) wires the Davison lags on its actuator; travel is the same as R1", report, ref passed, ref failed);

            string reason;
            Inject(r2.body, out reason);
            string granted;
            Record(MavF15PilotControlledAuthority.TryGrant(r2.body, out granted), "the R2 rig is still granted by the pilot-controlled authority: " + granted,
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [A7]

        private static void Trim(List<Object> created, StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[A7] Trim: Table point 36 on the R2 plant (SHADOW, nothing applied)");
            MavF15PilotTrimStart trim = MavF15PilotTrimStart.TableViiPoint36();
            Residual r1 = TrimResidual(created, MavF15PilotPhysicsRevision.R1InstantaneousSurfaces, 25);
            Residual r2 = TrimResidual(created, MavF15PilotPhysicsRevision.R2SourceActuatorLags, 25);
            report.AppendLine("      V " + trim.trueAirspeedFtPerSec.ToString("F1") + " ft/s, altitude " + trim.altitudeM.ToString("F0") + " m (source fixed density), alpha "
                              + trim.alphaDeg.ToString("F4") + " deg, theta " + trim.thetaDeg.ToString("F4") + " deg, stabilator " + trim.symmetricStabilatorDeg.ToString("F4")
                              + " deg, aileron/differential/rudder 0, thrust 8,300 lbf (fixed research thrust)");
            report.AppendLine("      R2 residual after 25 steps at neutral: force/weight X " + r2.ax.ToString("E2") + " Y " + r2.ay.ToString("E2") + " Z " + r2.az.ToString("E2")
                              + " | moment coefficients L " + r2.cl.ToString("E2") + " M " + r2.cm.ToString("E2") + " N " + r2.cn.ToString("E2")
                              + " | surfaces stab " + r2.stab.ToString("F5") + " lateral max " + r2.lateral.ToString("E1"));
            Record(r1.ok && r2.ok && BitEqual(r1.ax, r2.ax) && BitEqual(r1.ay, r2.ay) && BitEqual(r1.az, r2.az) && BitEqual(r1.cm, r2.cm)
                   && BitEqual(r1.cl, r2.cl) && BitEqual(r1.cn, r2.cn) && BitEqual(r1.stab, r2.stab),
                "R2 and R1 residuals are identical bit for bit: a lag of static gain 1, started at the trim surfaces, leaves the trim surfaces exactly where they are",
                report, ref passed, ref failed);
            Record(r2.ok && Math.Abs(r2.ax) < 2e-3 && Math.Abs(r2.ay) < 1e-6 && Math.Abs(r2.az) < 2e-3 && Math.Abs(r2.cm) < 1e-4 && Math.Abs(r2.cl) < 1e-6 && Math.Abs(r2.cn) < 1e-6
                   && Math.Abs(r2.stab - (float)trim.symmetricStabilatorDeg) < 1e-4f,
                "point 36 stays an equilibrium of the R2 plant to the printed precision of the trim (no residual is hidden by feedback: this is the V1 direct law at neutral, which commands exactly the trim bias)",
                report, ref passed, ref failed);
        }

        private struct Residual
        {
            public bool ok;
            public double ax, ay, az, cl, cm, cn;
            public float stab, lateral;
        }

        private static Residual TrimResidual(List<Object> created, MavF15PilotPhysicsRevision revision, int steps)
        {
            Residual res = new Residual();
            MavF15PilotControlledRig rig = Rig(created, "a7-" + revision, MavF15PilotControlMode.DirectV1, revision);
            string reason;
            if (!Inject(rig.body, out reason))
                return res;
            MavFlightPhysicsOwnership gate = rig.gameObject.AddComponent<MavFlightPhysicsOwnership>();
            rig.body.physicsOwnership = gate;
            gate.AttachGovernedBody(rig.body);
            string e;
            if (!gate.TryEnterShadow(out e))
                return res;
            rig.body.NotifyOwnershipChanged();
            MavManualPilotCommandSource source = (MavManualPilotCommandSource)rig.commandSource;
            source.command = MavPilotCommand.Neutral;
            PrimeSurfacesAtTrim(rig);
            for (int k = 0; k < steps; k++)
            {
                rig.law.StepPilotControlLaw(Dt);
                rig.actuator.StepActuator(Dt);
                rig.body.StepPhysicsForValidation(Dt, Dt * (k + 1));
            }

            MavFlightState s = rig.body.debugState;
            MavFlightDynamicsLoadSet set = rig.body.debugLoadSet;
            double weight = rig.body3d.mass * MavF15AfitResearchRuntimeEnvironment.SourceGravityMps2;
            double theta = s.attitude.pitchAttitudeRad, phi = s.attitude.bankAngleRad;
            Vector3 f = set.totalForceAeroBodyN;
            res.ax = f.x / weight - Math.Sin(theta);
            res.ay = f.y / weight + Math.Cos(theta) * Math.Sin(phi);
            res.az = f.z / weight + Math.Cos(theta) * Math.Cos(phi);
            MavAeroReferenceGeometry geo = MavF15BaumannMach06Reference.CreateReferenceGeometry();
            double qS = s.dynamicPressurePa * geo.wingAreaM2;
            Vector3 m = set.totalMomentAeroBodyNm;
            res.cl = m.x / (qS * geo.wingSpanM);
            res.cm = m.y / (qS * geo.meanAerodynamicChordM);
            res.cn = m.z / (qS * geo.wingSpanM);
            MavF15SurfaceState surf = rig.actuator.ActualF15SurfaceState.channels;
            res.stab = surf.symmetricStabilatorDeg;
            res.lateral = Mathf.Max(Mathf.Abs(surf.aileronDeg), Mathf.Max(Mathf.Abs(surf.differentialStabilatorDeg), Mathf.Abs(surf.rudderDeg)));
            res.ok = set.IsFinite() && qS > 0.0;
            gate.ReturnToLegacy("a7 done");
            return res;
        }

        // ---------------------------------------------------------------- [A8]

        private static void ResponseThroughRig(List<Object> created, StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[A8] Stick step through the real rig (V1 direct law, SHADOW): R1 vs R2");
            float[] stab1, stab2;
            float m1, m2;
            bool ok1 = StickStep(created, MavF15PilotPhysicsRevision.R1InstantaneousSurfaces, out stab1, out m1);
            bool ok2 = StickStep(created, MavF15PilotPhysicsRevision.R2SourceActuatorLags, out stab2, out m2);
            float bias = (float)MavF15PilotTrimStart.TableViiPoint36().symmetricStabilatorDeg;
            float target = stab1[1];
            float step = target - bias;
            float firstFraction = (stab2[1] - bias) / step;
            float expected = (float)(1.0 - Math.Exp(-20.0 * Dt));
            Record(ok1 && ok2 && Math.Abs(stab1[1] - target) < 1e-6f && Math.Abs(firstFraction - expected) < 1e-4f,
                "pitch +0.5 at t=0: R1 stabilator jumps " + step.ToString("F2") + " deg in one step; R2 covers " + (100f * firstFraction).ToString("F1")
                + " % of it (1 - e^-0.4 = " + (100f * expected).ToString("F1") + " %)", report, ref passed, ref failed);
            int n99 = -1;
            for (int k = 1; k < stab2.Length; k++)
            {
                if (Mathf.Abs(stab2[k] - target) <= 0.01f * Mathf.Abs(step))
                {
                    n99 = k;
                    break;
                }
            }

            Record(n99 > 0 && n99 * Dt <= 0.25f + 1e-4f,
                "R2 stabilator within 1 % of the command after " + (n99 * Dt).ToString("F2") + " s (5 tau = 0.25 s)", report, ref passed, ref failed);
            Record(Math.Sign(m1) == Math.Sign(m2) && m1 != 0f && Mathf.Abs(m2) < Mathf.Abs(m1),
                "the pitching-moment change has the same sign under R1 and R2, and is smaller on the first step under R2 (" + m1.ToString("E3") + " vs " + m2.ToString("E3") + " N m)",
                report, ref passed, ref failed);
        }

        private static bool StickStep(List<Object> created, MavF15PilotPhysicsRevision revision, out float[] stab, out float firstMomentChange)
        {
            stab = new float[31];
            firstMomentChange = 0f;
            MavF15PilotControlledRig rig = Rig(created, "a8-" + revision, MavF15PilotControlMode.DirectV1, revision);
            string reason;
            if (!Inject(rig.body, out reason))
                return false;
            MavFlightPhysicsOwnership gate = rig.gameObject.AddComponent<MavFlightPhysicsOwnership>();
            rig.body.physicsOwnership = gate;
            gate.AttachGovernedBody(rig.body);
            string e;
            if (!gate.TryEnterShadow(out e))
                return false;
            rig.body.NotifyOwnershipChanged();
            MavManualPilotCommandSource source = (MavManualPilotCommandSource)rig.commandSource;
            source.command = MavPilotCommand.Neutral;
            PrimeSurfacesAtTrim(rig);
            rig.body.StepPhysicsForValidation(Dt, Dt);
            float m0 = rig.body.debugLoadSet.totalMomentAeroBodyNm.y;
            stab[0] = rig.actuator.ActualF15SurfaceState.channels.symmetricStabilatorDeg;
            source.command = new MavPilotCommand { pitch = 0.5f };
            for (int k = 1; k < stab.Length; k++)
            {
                rig.law.StepPilotControlLaw(Dt);
                rig.actuator.StepActuator(Dt);
                rig.body.StepPhysicsForValidation(Dt, Dt * (k + 1));
                stab[k] = rig.actuator.ActualF15SurfaceState.channels.symmetricStabilatorDeg;
                if (k == 1)
                    firstMomentChange = rig.body.debugLoadSet.totalMomentAeroBodyNm.y - m0;
            }

            gate.ReturnToLegacy("a8 done");
            return true;
        }

        // ---------------------------------------------------------------- [A9]

        private static void SourceScan(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[A9] Source scan");
            string root = Path.Combine(Application.dataPath, MavFlightDynamicsOwnershipScan.FlightDynamicsRelativePath);
            string dynamics = Path.Combine(root, "F15/MavF15ActuatorDynamics.cs");
            string text = File.Exists(dynamics) ? File.ReadAllText(dynamics) : null;
            string[] writes = { "AddForce", "AddTorque", "AddRelativeForce", "AddRelativeTorque", "velocity =", "Velocity =", "MovePosition(", "MoveRotation(",
                                "simulationEnabled =", "ArmedForLiveFlight =", "Rigidbody" };
            string[] forbidden = { "MavF15AfitResearchTrimSolver", "MavF15AfitResearchSourceDynamics", "MavValidation", "MavF15BaumannTableVii", "MavF15TableVii", "MavF100",
                                   "MavF15ResearchRuntimeAuthority", "FromActuator(" };
            string hit = null;
            if (text != null)
            {
                foreach (string w in writes) if (text.Contains(w)) hit = w;
                foreach (string w in forbidden) if (text.Contains(w)) hit = w;
            }

            Record(text != null && hit == null, "MavF15ActuatorDynamics.cs writes no physics, arms nothing, constructs no actual surface state and names no research-only or F100 type"
                + (hit != null ? " - found " + hit : ""), report, ref passed, ref failed);

            string[] frozen = { "F15/MavF15AeroModel.cs", "F15/MavF15AfitResearchFixedThrust.cs", "F15/MavF15AfitResearchStaticSurfaceHold.cs",
                                "F15/MavF15AfitResearchFlightDynamicsProfile.cs", "F15/MavF15BaumannMach06Longitudinal.cs", "F15/MavF15BaumannMach06LateralDirectional.cs" };
            string leak = null;
            foreach (string f in frozen)
            {
                string t = File.ReadAllText(Path.Combine(root, f));
                if (t.Contains("MavF15ActuatorDynamics") || t.Contains("MavF15ResearchModelActuatorLags") || t.Contains("MavF15PilotPhysics"))
                    leak = f;
            }

            Record(leak == null, "no frozen research file references the R2 layer (the research path keeps its static surface hold, no dynamics)"
                + (leak != null ? " - " + leak : ""), report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- helpers

        private static readonly string[] PilotPrefabs =
        {
            "MaverickFresh/Prefabs/F15/F15_PilotControlledResearch_V1.prefab",
            "MaverickFresh/Prefabs/F15/F15_PilotControlledResearch_V2.prefab"
        };

        /// <summary>
        /// True when both pilot prefabs exist and neither pins a revision other than R2. The committed prefabs predate the
        /// field and serialize none, so Unity keeps the field default (R2) on load; a rebuilt prefab would serialize R2
        /// itself. The Play Mode test instantiates the V2 prefab and checks the result directly.
        /// </summary>
        private static bool PrefabsDefaultToR2(out string note)
        {
            string r2Line = "physicsRevision: " + (int)MavF15PilotPhysicsRevision.R2SourceActuatorLags;
            int unpinned = 0;
            foreach (string rel in PilotPrefabs)
            {
                string full = Path.Combine(Application.dataPath, rel);
                if (!File.Exists(full))
                {
                    note = "prefab missing: " + rel;
                    return false;
                }

                bool pinned = false;
                foreach (string line in File.ReadAllLines(full))
                {
                    string t = line.Trim();
                    if (!t.StartsWith("physicsRevision:", StringComparison.Ordinal))
                        continue;
                    pinned = true;
                    if (t != r2Line)
                    {
                        note = "prefab pins a revision other than R2 (" + t + "): " + rel;
                        return false;
                    }
                }

                if (!pinned)
                    unpinned++;
            }

            note = unpinned == PilotPrefabs.Length
                ? "both pilot prefabs serialize no revision, so they load with the R2 default"
                : "neither pilot prefab pins a revision other than R2";
            return true;
        }

        private static MavF15PilotControlledRig Rig(List<Object> created, string name, MavF15PilotControlMode mode, MavF15PilotPhysicsRevision? revision)
        {
            MavF15PilotControlledRig rig = revision.HasValue
                ? MavF15PilotControlledRig.Create(name, MavF15PilotCommandSourceKind.Scripted, false, mode, revision.Value)
                : MavF15PilotControlledRig.Create(name, MavF15PilotCommandSourceKind.Scripted, false, mode);
            rig.gameObject.hideFlags = HideFlags.HideAndDontSave;

            // Edit mode calls no Awake: build the (idempotent) stack explicitly, exactly as Awake does in Play Mode.
            rig.EnsureStack();
            created.Add(rig.gameObject);
            return rig;
        }

        private static bool Inject(MavSixDoFBody body, out string reason)
        {
            Vector3 p, v, w;
            Quaternion q;
            if (!MavF15AfitResearchStateInjection.TryComputeKinematics(MavF15PilotTrimStart.TableViiPoint36().ToKinematicState(), out p, out q, out v, out w, out reason))
                return false;
            if (!body.TryApplyInitialKinematicState(p, q, v, w, out reason))
                return false;
            body.StepPhysicsForValidation(0f, 0f);
            return true;
        }

        /// <summary>As StartFromTrim does: the law's neutral request (the trim bias) snapped onto the actuator.</summary>
        private static void PrimeSurfacesAtTrim(MavF15PilotControlledRig rig)
        {
            MavF15PilotControlSolution atTrim = MavF15PilotControlMapping.Solve(
                MavPilotCommand.Neutral, rig.profile.gameplayControlAuthority, (float)rig.profile.trimStart.symmetricStabilatorDeg);
            rig.actuator.SetF15Command(MavF15RequestedSurfaceState.From(atTrim.requested), 0f);
            rig.actuator.SnapToBoundedCommand();
        }

        private static float TimeTo(float fraction, float lambda)
        {
            float x = 0f;
            for (int k = 1; k < 1000; k++)
            {
                x = MavF15PilotPhysics.StepFirstOrderLag(x, 1f, lambda, Dt);
                if (x >= fraction)
                    return k * Dt;
            }

            return float.NaN;
        }

        private static MavF15SurfaceState RandomState(System.Random rng)
        {
            return new MavF15SurfaceState
            {
                symmetricStabilatorDeg = (float)(rng.NextDouble() * 40.0 - 30.0),
                differentialStabilatorDeg = (float)(rng.NextDouble() * 16.0 - 8.0),
                aileronDeg = (float)(rng.NextDouble() * 50.0 - 25.0),
                rudderDeg = (float)(rng.NextDouble() * 40.0 - 20.0)
            };
        }

        private static bool BitEqual(float a, float b)
        {
            return BitConverter.ToInt32(BitConverter.GetBytes(a), 0) == BitConverter.ToInt32(BitConverter.GetBytes(b), 0);
        }

        private static bool BitEqual(double a, double b)
        {
            return BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);
        }

        private static void Record(bool condition, string label, StringBuilder report, ref int passed, ref int failed)
        {
            if (condition)
                passed++;
            else
                failed++;
            report.Append(condition ? "  PASS  " : "  FAIL  ").AppendLine(label);
        }
    }
}
#endif
