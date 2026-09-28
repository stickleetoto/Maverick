#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using MaverickFresh.FlightDynamics.F15;
using Object = UnityEngine.Object;

namespace MaverickFresh.FlightDynamics.Validation
{
    /// <summary>
    /// F-15 PILOT PHYSICS R2 - deterministic Play Mode flight test of the research-model actuator lags.
    ///
    /// Flies <see cref="MavF15PilotControlledRig"/> through real Unity physics from the validated trim with scripted
    /// normalized commands: a neutral hold on R2; 1-s pulses of both signs on every axis flown on R1 and on R2 with the
    /// Assisted V2 law and identical inputs; the pilot sequence (neutral, pitch +/-, roll +/-, yaw +/-, pitch+roll,
    /// neutral) on R2 at dt, dt/2 and dt/4, and on R1 and on R2 with the Direct V1 law at dt.
    ///
    /// Pass criteria: ownership held, one load application per step, finite, surfaces inside the envelope, no research
    /// refusal, the measured lag equal to the source's (1 - e^(-lambda dt)) on the first step, correct signs, decay
    /// after release, first-order dt convergence. R1 vs R2 is REPORTED; neither is scored "better".
    ///
    /// DETERMINISM: Time.captureFramerate = 50, fixed run order, a fresh rig per run built one physics step before it
    /// starts, commands on exact step indices. Runs at -600, before every part of the rig.
    /// </summary>
    [DefaultExecutionOrder(-600)]
    public sealed class MavF15PilotPhysicsR2FlightValidationRunner : MonoBehaviour
    {
        public static bool Finished;
        public static int Passed;
        public static int Failed;
        public static string Report = "";

        /// <summary>The normal V2 prefab, set by the editor driver: [N] checks that placing it flies R2.</summary>
        public static GameObject PrefabToTest;
        public static string PrefabPath = "(none)";

        public const int CaptureFramerate = 50;
        public const float BaseDt = 0.02f;
        public const float PulseAmplitude = 0.5f;
        public const double NeutralTolerance = 1e-5;
        public const float SequenceDurationS = 42f;
        public static readonly float[] SequenceWindows = { 3f, 4f, 8f, 9f, 13f, 14f, 18f, 19f, 23f, 24f, 28f, 29f, 33f, 34f };
        public static readonly string[] SequenceNames = { "pitch +", "pitch -", "roll +", "roll -", "yaw +", "yaw -", "pitch+roll" };
        public static readonly string[] StateNames = { "alpha", "beta", "p", "q", "r", "theta", "phi", "V" };

        private enum Kind { Neutral, Pulse, Sequence }

        private struct Sample
        {
            public double[] x;
            public MavF15SurfaceState surfaces;
            public MavF15SurfaceState requested;
            public bool aeroRefused;
        }

        private sealed class Run
        {
            public string id;
            public Kind kind;
            public MavF15PilotPhysicsRevision revision;
            public MavF15PilotControlMode mode;
            public float dt;
            public int steps;
            public float pitch, roll, yaw;
            public int pulseStart, pulseEnd;
            public readonly List<Sample> samples = new List<Sample>();
            public bool started, completed, ownerHeld = true, oneLaw = true, finite = true, inEnvelope = true;
            public int aeroRefused, loads, duplicates;
            public string failure;
        }

        private readonly List<Run> runs = new List<Run>();
        private readonly StringBuilder sb = new StringBuilder(64 * 1024);
        private int runIndex, phase, stepsDone;
        private MavF15PilotControlledRig rig;
        private MavManualPilotCommandSource source;
        private float savedFixedDeltaTime;
        private int savedCaptureFramerate;
        private bool done;
        private double[] equilibrium;
        private MavF15GameplayControlAuthority authority;
        private bool normalPrefabR2;
        private string normalPrefabNote = "not placed";

        public static void Clear()
        {
            Finished = false;
            Passed = 0;
            Failed = 0;
            Report = "";
        }

        private void Awake()
        {
            Clear();
            savedFixedDeltaTime = Time.fixedDeltaTime;
            savedCaptureFramerate = Time.captureFramerate;
            Time.captureFramerate = CaptureFramerate;
            MavF15PilotTrimStart t = MavF15PilotTrimStart.TableViiPoint36();
            equilibrium = new[] { t.alphaDeg * Math.PI / 180.0, 0.0, 0.0, 0.0, 0.0, t.thetaDeg * Math.PI / 180.0, 0.0, t.trueAirspeedFtPerSec };
            authority = MavF15GameplayControlAuthority.V1();

            const MavF15PilotPhysicsRevision R1 = MavF15PilotPhysicsRevision.R1InstantaneousSurfaces;
            const MavF15PilotPhysicsRevision R2 = MavF15PilotPhysicsRevision.R2SourceActuatorLags;
            const MavF15PilotControlMode V2 = MavF15PilotControlMode.AssistedV2;
            runs.Add(new Run { id = "NEUTRAL-HOLD R2 V2", kind = Kind.Neutral, revision = R2, mode = V2, dt = BaseDt, steps = Steps(20f, BaseDt) });
            foreach (MavF15PilotPhysicsRevision rev in new[] { R1, R2 })
            {
                AddPulse(rev, "pitch +", PulseAmplitude, 0f, 0f);
                AddPulse(rev, "pitch -", -PulseAmplitude, 0f, 0f);
                AddPulse(rev, "roll +", 0f, PulseAmplitude, 0f);
                AddPulse(rev, "roll -", 0f, -PulseAmplitude, 0f);
                AddPulse(rev, "yaw +", 0f, 0f, PulseAmplitude);
                AddPulse(rev, "yaw -", 0f, 0f, -PulseAmplitude);
            }

            foreach (float dt in new[] { BaseDt, BaseDt / 2f, BaseDt / 4f })
                runs.Add(new Run { id = "SEQUENCE R2 V2 dt " + dt.ToString("F4", CultureInfo.InvariantCulture), kind = Kind.Sequence, revision = R2, mode = V2, dt = dt, steps = Steps(SequenceDurationS, dt) });
            runs.Add(new Run { id = "SEQUENCE R1 V2 dt 0.0200", kind = Kind.Sequence, revision = R1, mode = V2, dt = BaseDt, steps = Steps(SequenceDurationS, BaseDt) });
            runs.Add(new Run { id = "SEQUENCE R2 V1 dt 0.0200", kind = Kind.Sequence, revision = R2, mode = MavF15PilotControlMode.DirectV1, dt = BaseDt, steps = Steps(SequenceDurationS, BaseDt) });
        }

        private void AddPulse(MavF15PilotPhysicsRevision rev, string what, float pitch, float roll, float yaw)
        {
            runs.Add(new Run
            {
                id = "PULSE " + (rev == MavF15PilotPhysicsRevision.R1InstantaneousSurfaces ? "R1 " : "R2 ") + what, kind = Kind.Pulse, revision = rev,
                mode = MavF15PilotControlMode.AssistedV2, dt = BaseDt, steps = Steps(8f, BaseDt), pitch = pitch, roll = roll, yaw = yaw,
                pulseStart = Steps(1f, BaseDt), pulseEnd = Steps(2f, BaseDt)
            });
        }

        private static int Steps(float seconds, float dt)
        {
            return Mathf.RoundToInt(seconds / dt);
        }

        private static bool In(int step, float dt, float from, float to)
        {
            return step >= Steps(from, dt) && step < Steps(to, dt);
        }

        private MavPilotCommand CommandAt(Run run, int step)
        {
            MavPilotCommand c = MavPilotCommand.Neutral;
            if (run.kind == Kind.Pulse && step >= run.pulseStart && step < run.pulseEnd)
            {
                c.pitch = run.pitch;
                c.roll = run.roll;
                c.yaw = run.yaw;
            }
            else if (run.kind == Kind.Sequence)
            {
                float[] w = SequenceWindows;
                if (In(step, run.dt, w[0], w[1])) c.pitch = PulseAmplitude;
                if (In(step, run.dt, w[2], w[3])) c.pitch = -PulseAmplitude;
                if (In(step, run.dt, w[4], w[5])) c.roll = PulseAmplitude;
                if (In(step, run.dt, w[6], w[7])) c.roll = -PulseAmplitude;
                if (In(step, run.dt, w[8], w[9])) c.yaw = PulseAmplitude;
                if (In(step, run.dt, w[10], w[11])) c.yaw = -PulseAmplitude;
                if (In(step, run.dt, w[12], w[13]))
                {
                    c.pitch = 0.3f;
                    c.roll = 0.3f;
                }
            }

            return c;
        }

        // ================================================================== state machine

        private void FixedUpdate()
        {
            if (done)
                return;

            if (runIndex >= runs.Count)
            {
                done = true;
                PlaceNormalPrefab();
                Complete();
                return;
            }

            Run run = runs[runIndex];
            try
            {
                if (phase == 0)
                {
                    // Build one step BEFORE starting: a component added during a FixedUpdate first steps on the next one.
                    Time.fixedDeltaTime = run.dt;
                    rig = MavF15PilotControlledRig.Create("f15r2-" + run.id, MavF15PilotCommandSourceKind.Scripted, false, run.mode, run.revision);
                    phase = 1;
                    return;
                }

                if (phase == 1)
                {
                    run.started = rig.StartFromTrim();
                    source = (MavManualPilotCommandSource)rig.commandSource;
                    if (!run.started)
                    {
                        run.failure = "start refused: " + rig.debugStartStatus;
                        Finish(run);
                        return;
                    }

                    stepsDone = 0;
                    run.samples.Add(Capture());
                    source.command = CommandAt(run, 0);
                    phase = 2;
                    return;
                }

                stepsDone++;
                Sample s = Capture();
                run.samples.Add(s);
                for (int k = 0; k < 8; k++)
                    run.finite &= !double.IsNaN(s.x[k]) && !double.IsInfinity(s.x[k]);
                run.finite &= s.surfaces.IsFinite();
                run.inEnvelope &= InsideEnvelope(s.surfaces);
                if (s.aeroRefused)
                    run.aeroRefused++;
                if (rig.ownership == null || rig.ownership.owner != MavFlightPhysicsOwner.F15PilotControlledResearch || !rig.body.ArmedForLiveFlight)
                    run.ownerHeld = false;
                int enabled = (rig.law != null && rig.law.enabled ? 1 : 0) + (rig.lawV2 != null && rig.lawV2.enabled ? 1 : 0);
                run.oneLaw &= enabled == 1 && rig.body.controlLaw == rig.ActiveLaw;

                if (stepsDone >= run.steps)
                {
                    Finish(run);
                    return;
                }

                source.command = CommandAt(run, stepsDone);
            }
            catch (Exception e)
            {
                run.failure = "exception: " + e.GetType().Name + ": " + e.Message;
                Finish(run);
            }
        }

        /// <summary>
        /// Places the normal V2 prefab, exactly as the scene menu does, after every run is finished. Its Awake wires the
        /// stack; nothing is changed by the test, and the instance is removed before it ever steps.
        /// </summary>
        private void PlaceNormalPrefab()
        {
            if (PrefabToTest == null)
            {
                normalPrefabNote = "no V2 prefab was handed over (" + PrefabPath + ")";
                return;
            }

            GameObject instance = null;
            try
            {
                instance = Object.Instantiate(PrefabToTest);
                MavF15PilotControlledRig placed = instance.GetComponent<MavF15PilotControlledRig>();
                if (placed == null || placed.profile == null || placed.actuator == null)
                {
                    normalPrefabNote = "the placed prefab has no wired pilot-controlled rig";
                    return;
                }

                MavF15ActuatorDynamics d = placed.actuator.dynamics;
                normalPrefabR2 = placed.controlMode == MavF15PilotControlMode.AssistedV2
                                 && placed.profile.physicsRevision == MavF15PilotPhysicsRevision.R2SourceActuatorLags
                                 && d.symmetricStabilator.lagPerSec == MavF15ResearchModelActuatorLags.SymmetricStabilatorPerSec
                                 && d.aileron.lagPerSec == MavF15ResearchModelActuatorLags.AileronPerSec
                                 && d.differentialStabilator.lagPerSec == MavF15ResearchModelActuatorLags.DifferentialTailPerSec
                                 && d.rudder.lagPerSec == MavF15ResearchModelActuatorLags.RudderPerSec;
                normalPrefabNote = PrefabPath + ": law " + placed.controlMode + ", revision " + placed.profile.physicsRevision
                                   + ", actuator lags stab " + d.symmetricStabilator.lagPerSec.ToString(CultureInfo.InvariantCulture)
                                   + " / aileron " + d.aileron.lagPerSec.ToString(CultureInfo.InvariantCulture)
                                   + " / differential " + d.differentialStabilator.lagPerSec.ToString(CultureInfo.InvariantCulture)
                                   + " / rudder " + d.rudder.lagPerSec.ToString(CultureInfo.InvariantCulture) + " 1/s";
            }
            catch (Exception e)
            {
                normalPrefabNote = "placing the prefab threw " + e.GetType().Name + ": " + e.Message;
            }
            finally
            {
                if (instance != null)
                    Object.DestroyImmediate(instance);
            }
        }

        private Sample Capture()
        {
            // Read back from the Rigidbody, as the pilot V1/V2 flight tests do: valid from the injected start onward, whereas
            // the body's published state only exists once it has stepped.
            MavFlightState s = MavF15AfitResearchStateInjection.ReadBack(rig.body);
            return new Sample
            {
                x = new double[]
                {
                    s.alphaRad, s.betaRad, s.aeroBodyRatesRadSec.x, s.aeroBodyRatesRadSec.y, s.aeroBodyRatesRadSec.z,
                    s.attitude.pitchAttitudeRad, s.attitude.bankAngleRad, s.trueAirspeedMps / MavF15BaumannMach06Reference.FootToM
                },
                surfaces = rig.actuator.ActualF15SurfaceState.channels,
                requested = rig.actuator.requested.channels,
                aeroRefused = rig.aero != null && rig.aero.debugRefused
            };
        }

        private void Finish(Run run)
        {
            if (rig != null && rig.body != null)
            {
                run.loads = rig.body.debugLoadApplications;
                run.duplicates = rig.body.debugRejectedDuplicateApplications;
            }

            run.completed = run.failure == null && stepsDone >= run.steps;
            if (rig != null && rig.ownership != null && rig.ownership.owner != MavFlightPhysicsOwner.Fault)
                rig.ownership.ReturnToLegacy("R2 flight run complete");
            if (rig != null)
                Object.DestroyImmediate(rig.gameObject);
            rig = null;
            source = null;
            runIndex++;
            phase = 0;
        }

        private bool InsideEnvelope(MavF15SurfaceState s)
        {
            const float eps = 1e-4f;
            return s.symmetricStabilatorDeg >= authority.symmetricStabilator.minDeg - eps && s.symmetricStabilatorDeg <= authority.symmetricStabilator.maxDeg + eps
                   && s.aileronDeg >= authority.aileron.minDeg - eps && s.aileronDeg <= authority.aileron.maxDeg + eps
                   && s.differentialStabilatorDeg >= authority.differentialStabilator.minDeg - eps && s.differentialStabilatorDeg <= authority.differentialStabilator.maxDeg + eps
                   && s.rudderDeg >= authority.rudder.minDeg - eps && s.rudderDeg <= authority.rudder.maxDeg + eps;
        }

        // ================================================================== evaluation

        private void Complete()
        {
            Time.fixedDeltaTime = savedFixedDeltaTime;
            Time.captureFramerate = savedCaptureFramerate;
            MavF15PilotTrimStart t = MavF15PilotTrimStart.TableViiPoint36();
            sb.AppendLine("F-15 PILOT PHYSICS R2 - PLAY MODE FLIGHT TEST (" + MavF15PilotControlledIdentity.ConfigurationId + ")");
            sb.AppendLine("==========================================================================");
            sb.AppendLine("Frozen research aerodynamics + R2 research-model actuator lags (" + MavF15ResearchModelActuatorLags.Citation + ").");
            sb.AppendLine("Control laws unchanged: Assisted V2 and Direct V1, MAVERICK_TUNED_NON_AUTHORITATIVE. THIS IS NOT THE F-15 FCS.");
            sb.AppendLine("Trim start: stab " + t.symmetricStabilatorDeg.ToString("F5", CultureInfo.InvariantCulture) + " deg, alpha "
                          + t.alphaDeg.ToString("F4", CultureInfo.InvariantCulture) + " deg, V " + t.trueAirspeedFtPerSec.ToString("F1", CultureInfo.InvariantCulture)
                          + " ft/s; pulses " + PulseAmplitude + " for 1 s (1-2 s) then centred to 8 s.");

            Mechanics();
            Neutral();
            LagInFlight();
            Pulses();
            Comparison();
            Sequence();
            Convergence();
            NormalAircraft();

            sb.AppendLine();
            sb.Append("RESULT: ").Append(Failed == 0 ? "PASS" : "FAIL").Append("  passed=").Append(Passed).Append(" failed=").Append(Failed);
            Report = sb.ToString();
            Finished = true;
        }

        private void Mechanics()
        {
            sb.AppendLine();
            sb.AppendLine("[M] Runtime mechanics of every run");
            foreach (Run r in runs)
            {
                bool ok = r.completed && r.ownerHeld && r.oneLaw && r.finite && r.inEnvelope && r.aeroRefused == 0 && r.duplicates == 0 && r.loads == r.steps;
                Check(ok, "M", r.id.PadRight(26) + " " + r.steps + " steps, loads " + r.loads + ", duplicates " + r.duplicates + ", owner held " + r.ownerHeld
                               + ", one law " + r.oneLaw + ", finite " + r.finite + ", surfaces in envelope " + r.inEnvelope + ", aero refused " + r.aeroRefused
                               + (r.failure != null ? " - " + r.failure : ""));
            }
        }

        private void Neutral()
        {
            sb.AppendLine();
            sb.AppendLine("[H] Neutral hold on R2 (V2, 20 s)");
            Run r = runs.Find(x => x.kind == Kind.Neutral);
            if (!Usable(r, "H"))
                return;
            double[] d = Drift(r);
            Check(Max(d) <= NeutralTolerance, "H", "every state within " + Max(d).ToString("E1") + " of the trim (tolerance " + NeutralTolerance.ToString("E0")
                                                     + "): the lag leaves the equilibrium an equilibrium - " + DescribeStates(d));
        }

        private void LagInFlight()
        {
            sb.AppendLine();
            sb.AppendLine("[L] The lag, measured in flight (pulse onset, first step after the command changes)");
            foreach (string what in new[] { "pitch +", "roll +", "yaw +" })
            {
                Run r2 = runs.Find(x => x.id == "PULSE R2 " + what);
                Run r1 = runs.Find(x => x.id == "PULSE R1 " + what);
                if (!Usable(r2, "L") || !Usable(r1, "L"))
                    continue;
                MavF15SurfaceChannel channel = what.StartsWith("pitch") ? MavF15SurfaceChannel.SymmetricStabilator
                    : what.StartsWith("roll") ? MavF15SurfaceChannel.Aileron : MavF15SurfaceChannel.Rudder;
                float lambda = MavF15ResearchModelActuatorLags.Create().Get(channel).lagPerSec;
                int k = r2.pulseStart + 1;
                float before = r2.samples[k - 1].surfaces.Get(channel);
                float cmd = r2.samples[k].requested.Get(channel);
                float after = r2.samples[k].surfaces.Get(channel);
                float fraction = (after - before) / (cmd - before);
                float expected = 1f - Mathf.Exp(-lambda * r2.dt);
                float r1Fraction = (r1.samples[k].surfaces.Get(channel) - r1.samples[k - 1].surfaces.Get(channel))
                                   / (r1.samples[k].requested.Get(channel) - r1.samples[k - 1].surfaces.Get(channel));
                float trailMax = 0f;
                for (int i = r2.pulseStart; i < r2.pulseEnd; i++)
                    trailMax = Mathf.Max(trailMax, Mathf.Abs(r2.samples[i].requested.Get(channel) - r2.samples[i].surfaces.Get(channel)));
                Check(Mathf.Abs(fraction - expected) < 2e-3f && Mathf.Abs(r1Fraction - 1f) < 1e-4f,
                    "L", what + " (" + channel + ", " + lambda + " 1/s): R2 moves " + (100f * fraction).ToString("F1") + " % of the step on the first step (source 1 - e^-lambda dt = "
                         + (100f * expected).ToString("F1") + " %); R1 moves " + (100f * r1Fraction).ToString("F1") + " %; R2 trails the command by up to "
                         + trailMax.ToString("F2") + " deg during the pulse");
            }
        }

        private void Pulses()
        {
            sb.AppendLine();
            sb.AppendLine("[S] R2 pulses under V2: sign, decay after release");
            string[] names = { "pitch +", "pitch -", "roll +", "roll -", "yaw +", "yaw -" };
            foreach (string what in names)
            {
                Run r = runs.Find(x => x.id == "PULSE R2 " + what);
                if (!Usable(r, "S"))
                    continue;
                int idx = what.StartsWith("pitch") ? 3 : what.StartsWith("roll") ? 2 : 4;
                int sign = what.EndsWith("+") ? 1 : -1;
                PulseStats p = Stats(r, idx);
                Check(Math.Sign(p.delta) == sign && Math.Abs(p.delta) > 1e-3 && p.lastSecond < 0.5 * p.peak,
                    "S", what.PadRight(8) + " delta over pulse " + p.delta.ToString("+0.0000;-0.0000") + " rad/s, peak " + p.peak.ToString("F4")
                         + ", last second " + p.lastSecond.ToString("F4") + " (< 0.5 peak), settle " + Settle(p.settle));
            }
        }

        private void Comparison()
        {
            sb.AppendLine();
            sb.AppendLine("[C] R1 vs R2, identical inputs, Assisted V2 (reported; R2 = the same law on a plant with the source's actuator lag)");
            sb.AppendLine("      input     rev  delta(rad/s)  peak     release swing  settle(<10% peak)  last 1 s   |beta|max deg");
            string[] names = { "pitch +", "pitch -", "roll +", "roll -", "yaw +", "yaw -" };
            foreach (string what in names)
            {
                Run r1 = runs.Find(x => x.id == "PULSE R1 " + what);
                Run r2 = runs.Find(x => x.id == "PULSE R2 " + what);
                if (!Usable(r1, "C") || !Usable(r2, "C"))
                    continue;
                int idx = what.StartsWith("pitch") ? 3 : what.StartsWith("roll") ? 2 : 4;
                foreach (Run r in new[] { r1, r2 })
                {
                    PulseStats p = Stats(r, idx);
                    sb.AppendLine("      " + what.PadRight(9) + " " + (r == r1 ? "R1" : "R2") + "   " + p.delta.ToString("+0.0000;-0.0000").PadRight(12) + "  "
                                  + p.peak.ToString("F4").PadRight(7) + "  " + p.swing.ToString("F2").PadRight(13) + "  " + Settle(p.settle).PadRight(17) + "  "
                                  + p.lastSecond.ToString("F4").PadRight(9) + "  " + (p.betaMax * 180.0 / Math.PI).ToString("F2"));
                }

                PulseStats a = Stats(r1, idx), b = Stats(r2, idx);
                Check(Math.Sign(a.delta) == Math.Sign(b.delta) && Math.Abs(b.delta) > 0.5 * Math.Abs(a.delta) && b.lastSecond < 0.5 * b.peak,
                    "C", what + ": same response sense on both plants; R2 keeps " + (100.0 * b.delta / a.delta).ToString("F0")
                         + " % of the R1 rate change and still decays after release - the V2 loop stays closed and damped with the lag in it");
            }
        }

        private void Sequence()
        {
            sb.AppendLine();
            sb.AppendLine("[Q] The pilot sequence on R2 (V2 and V1) and on R1 (V2), 42 s");
            foreach (string id in new[] { "SEQUENCE R2 V2 dt 0.0200", "SEQUENCE R1 V2 dt 0.0200", "SEQUENCE R2 V1 dt 0.0200" })
            {
                Run r = runs.Find(x => x.id == id);
                if (!Usable(r, "Q"))
                    continue;
                bool signs = true;
                int[] axis = { 3, 3, 2, 2, 4, 4, 3 };
                int[] sgn = { 1, -1, 1, -1, 1, -1, 1 };
                for (int w = 0; w < 7; w++)
                {
                    int a0 = Steps(SequenceWindows[2 * w], r.dt), a1 = Steps(SequenceWindows[2 * w + 1], r.dt);
                    double delta = r.samples[a1].x[axis[w]] - r.samples[a0].x[axis[w]];
                    signs &= Math.Sign(delta) == sgn[w];
                }

                double alphaMin = double.MaxValue, alphaMax = double.MinValue, betaMax = 0, vMin = double.MaxValue, vMax = double.MinValue, phiMax = 0, endRates = 0;
                int tail = Steps(SequenceDurationS - 2f, r.dt);
                for (int i = 0; i < r.samples.Count; i++)
                {
                    double[] x = r.samples[i].x;
                    alphaMin = Math.Min(alphaMin, x[0]);
                    alphaMax = Math.Max(alphaMax, x[0]);
                    betaMax = Math.Max(betaMax, Math.Abs(x[1]));
                    vMin = Math.Min(vMin, x[7]);
                    vMax = Math.Max(vMax, x[7]);
                    phiMax = Math.Max(phiMax, Math.Abs(x[6]));
                    if (i >= tail)
                        endRates = Math.Max(endRates, Math.Max(Math.Abs(x[2]), Math.Max(Math.Abs(x[3]), Math.Abs(x[4]))));
                }

                const double r2d = 180.0 / Math.PI;
                // Direct V1 has no rate feedback and keeps rolling after release by design (its recorded behaviour), so
                // the settled-rates criterion applies to the closed-loop V2 runs only.
                bool settledRequired = r.mode == MavF15PilotControlMode.AssistedV2;
                Check(signs && r.aeroRefused == 0 && (!settledRequired || endRates < 0.02),
                    "Q", id + ": every window has the commanded sign; no research refusal; alpha " + (alphaMin * r2d).ToString("F2") + "-" + (alphaMax * r2d).ToString("F2")
                         + " deg, |beta| <= " + (betaMax * r2d).ToString("F2") + " deg, V " + vMin.ToString("F1") + "-" + vMax.ToString("F1") + " ft/s, |phi| <= "
                         + (phiMax * r2d).ToString("F2") + " deg; body rates <= " + endRates.ToString("F4") + " rad/s in the final 2 s");
            }
        }

        private void Convergence()
        {
            sb.AppendLine();
            sb.AppendLine("[D] Timestep refinement on R2 (V2 sequence at dt, dt/2, dt/4, compared at common times)");
            Run a = runs.Find(x => x.id == "SEQUENCE R2 V2 dt 0.0200");
            Run b = runs.Find(x => x.id == "SEQUENCE R2 V2 dt 0.0100");
            Run c = runs.Find(x => x.id == "SEQUENCE R2 V2 dt 0.0050");
            if (!Usable(a, "D") || !Usable(b, "D") || !Usable(c, "D"))
                return;
            double[] e1 = new double[8], e2 = new double[8];
            for (int i = 0; i < a.samples.Count; i++)
            {
                int j = 2 * i, k = 4 * i;
                if (j >= b.samples.Count || k >= c.samples.Count)
                    break;
                for (int s = 0; s < 8; s++)
                {
                    double scale = s == 7 ? equilibrium[7] : 1.0;
                    e1[s] = Math.Max(e1[s], Math.Abs(a.samples[i].x[s] - c.samples[k].x[s]) / scale);
                    e2[s] = Math.Max(e2[s], Math.Abs(b.samples[j].x[s] - c.samples[k].x[s]) / scale);
                }
            }

            int dom = 0;
            for (int s = 1; s < 8; s++)
                if (e1[s] > e1[dom]) dom = s;
            double ratio = e1[dom] / e2[dom];
            Check(e2[dom] < e1[dom] && ratio > 2.0 && ratio < 4.5,
                "D", "the dominant difference (" + StateNames[dom] + ") shrinks with dt, ratio " + ratio.ToString("F2") + " (3 for first order against dt/4); the lag itself is exact, "
                     + "so the order is the law's one-step delay and the held surfaces, as on R1");
        }

        // ================================================================== helpers

        private struct PulseStats
        {
            public double delta, peak, swing, lastSecond, settle, betaMax;
        }

        private PulseStats Stats(Run r, int idx)
        {
            PulseStats p = new PulseStats();
            double x0 = r.samples[r.pulseStart].x[idx];
            p.delta = r.samples[r.pulseEnd].x[idx] - x0;
            for (int i = r.pulseStart; i <= r.pulseEnd; i++)
                p.peak = Math.Max(p.peak, Math.Abs(r.samples[i].x[idx] - x0));
            double swingMin = double.MaxValue, swingMax = double.MinValue;
            for (int i = r.pulseEnd; i < r.samples.Count; i++)
            {
                double v = r.samples[i].x[idx] - x0;
                swingMin = Math.Min(swingMin, v);
                swingMax = Math.Max(swingMax, v);
                p.betaMax = Math.Max(p.betaMax, Math.Abs(r.samples[i].x[1]));
            }

            for (int i = r.pulseStart; i < r.pulseEnd; i++)
                p.betaMax = Math.Max(p.betaMax, Math.Abs(r.samples[i].x[1]));
            p.swing = p.peak > 0 ? (swingMax - swingMin) / p.peak : 0;
            int last = r.samples.Count - Steps(1f, r.dt);
            for (int i = last; i < r.samples.Count; i++)
                p.lastSecond = Math.Max(p.lastSecond, Math.Abs(r.samples[i].x[idx]));
            p.settle = double.PositiveInfinity;
            for (int i = r.samples.Count - 1; i >= r.pulseEnd; i--)
            {
                if (Math.Abs(r.samples[i].x[idx]) > 0.1 * p.peak)
                {
                    p.settle = i + 1 < r.samples.Count ? (i + 1 - r.pulseEnd) * r.dt : double.PositiveInfinity;
                    break;
                }

                if (i == r.pulseEnd)
                    p.settle = 0;
            }

            return p;
        }

        private static string Settle(double s)
        {
            return double.IsInfinity(s) ? ">6.00 s" : s.ToString("F2") + " s";
        }

        private double[] Drift(Run r)
        {
            double[] d = new double[8];
            foreach (Sample s in r.samples)
                for (int k = 0; k < 8; k++)
                    d[k] = Math.Max(d[k], Math.Abs(s.x[k] - equilibrium[k]) / (k == 7 ? equilibrium[7] : 1.0));
            return d;
        }

        private static double Max(double[] d)
        {
            double m = 0;
            foreach (double v in d) m = Math.Max(m, v);
            return m;
        }

        private void NormalAircraft()
        {
            sb.AppendLine();
            sb.AppendLine("[N] The normal pilot-controlled F-15 (the V2 prefab, placed with no settings changed) flies R2");
            Check(normalPrefabR2, "N", normalPrefabNote);
        }

        private static string DescribeStates(double[] d)
        {
            StringBuilder s = new StringBuilder();
            for (int k = 0; k < 8; k++)
                s.Append(StateNames[k]).Append(' ').Append(d[k].ToString("E1")).Append(k < 7 ? ", " : "");
            return s.ToString();
        }

        private bool Usable(Run r, string tag)
        {
            if (r != null && r.completed)
                return true;
            Check(false, tag, "run " + (r != null ? r.id : "(missing)") + " completed" + (r != null && r.failure != null ? " - " + r.failure : ""));
            return false;
        }

        private void Check(bool condition, string tag, string label)
        {
            if (condition) Passed++;
            else Failed++;
            sb.Append(condition ? "  PASS  " : "  FAIL  ").Append(tag).Append("  ").AppendLine(label);
        }
    }
}
#endif
