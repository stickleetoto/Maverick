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
    /// F-15 PILOT CONTROL V2 - deterministic Play Mode flight test, with the V1 vs V2 comparison.
    ///
    /// Flies <see cref="MavF15PilotControlledRig"/> - the rig a human flies - through real Unity physics from the
    /// validated trim, with scripted normalized pilot commands (no keyboard, camera or UI):
    ///   neutral hold under V1 and under V2; the human V2 prefab holding trim with no input; 1-s pulses of both
    ///   signs on every axis and a combined pitch + roll, flown by V1 AND by V2 with identical inputs; the
    ///   sequence neutral - pitch+ - pitch- - roll+ - roll- - yaw+ - yaw- - pitch+roll - neutral under V2 at dt,
    ///   dt/2 and dt/4 (and under V1 at dt, for comparison); an in-flight law switch V1 -> V2 -> V1 (and a switch
    ///   refused with the stick deflected); a fail-closed switch run (refused with the stick deflected, the command
    ///   unavailable, the command source missing and the command source disabled, then V1 -> V2 -> V1 centred); an
    ///   ownership loss; and a second surface requester enabled in flight.
    ///
    /// Pass criteria are sign, finiteness, continuity, ownership, a single load application per step, bounded
    /// surfaces, no research-domain refusal, decay after release and convergence - never "looks right". The
    /// V1-vs-V2 table is REPORTED with its tradeoffs; "faster" is never scored as "better" by itself.
    ///
    /// RESEARCH MODEL + MAVERICK CONTROL LAWS. THIS IS NOT THE F-15 FCS; not NASA 836.
    ///
    /// DETERMINISM: Time.captureFramerate = 50, a fixed run order, a fresh rig per run built one physics step
    /// before it starts; commands switch on exact step indices, so every dt sees the same command timeline.
    /// Runs at -600, before the rig (-500), the ownership authority (-400), the laws (-300), the actuator
    /// (-200) and the body (-100).
    /// </summary>
    [DefaultExecutionOrder(-600)]
    public sealed class MavF15PilotControlledV2FlightValidationRunner : MonoBehaviour
    {
        public static bool Finished;
        public static int Passed;
        public static int Failed;
        public static string Report = "";

        /// <summary>The human V2 prefab to fly, set by the editor driver. Null: a V2 keyboard rig is built instead.</summary>
        public static GameObject PrefabToTest;
        public static string PrefabPath = "(none)";

        public const int CaptureFramerate = 50;
        public const float BaseDt = 0.02f;
        public const float PulseAmplitude = 0.5f;
        public const double RadToDeg = 180.0 / Math.PI;

        /// <summary>Neutral-hold tolerance: float round-off on the state, amplified at most slightly by the V2 feedback.</summary>
        public const double NeutralTolerance = 1e-5;

        public static readonly string[] StateNames = { "alpha", "beta", "p", "q", "r", "theta", "phi", "V" };

        // Sequence (s): neutral 0-3, pitch+ 3-4, neutral, pitch- 8-9, neutral, roll+ 13-14, neutral, roll- 18-19,
        // neutral, yaw+ 23-24, neutral, yaw- 28-29, neutral, pitch+roll (0.3/0.3) 33-34, neutral to 42.
        public const float SequenceDurationS = 42f;
        public static readonly float[] SequenceWindows = { 3f, 4f, 8f, 9f, 13f, 14f, 18f, 19f, 23f, 24f, 28f, 29f, 33f, 34f };
        public static readonly string[] SequenceNames = { "pitch +", "pitch -", "roll +", "roll -", "yaw +", "yaw -", "pitch+roll" };

        private enum Kind
        {
            Neutral,
            HumanRig,
            Pulse,
            Sequence,
            Switch,
            SwitchFailClosed,
            OwnershipLoss,
            SecondRequester
        }

        private struct Sample
        {
            public double t;
            public double[] x;
            public Vector3 position;
            public Vector3 velocity;
            public MavF15SurfaceState surfaces;
            public bool aeroRefused;
            public bool limited;
        }

        private sealed class Run
        {
            public string id;
            public Kind kind;
            public MavF15PilotControlMode mode;
            public float dt;
            public int steps;
            public float pitch, roll, yaw;
            public int pulseStartStep, pulseEndStep;
            public int lossStep = -1;

            public readonly List<Sample> samples = new List<Sample>();
            public bool started;
            public string startStatus = "not started";
            public string failure;
            public bool completed;
            public int loadApplicationsAtEnd;
            public int duplicateRejections;
            public int initialStateApplications;
            public bool ownerHeldEveryStep = true;
            public bool exactlyOneArmedEveryStep = true;
            public bool exactlyOneLawEveryStep = true;
            public bool unityGravityOffEveryStep = true;
            public bool dtAsConfigured = true;
            public bool finiteEveryStep = true;
            public bool surfacesInsideEnvelopeEveryStep = true;
            public int aeroRefusedSteps;
            public double maxKinematicResidualM;
            public double maxVelocityStepMps;
            public bool faultedNextStep;
            public bool loadsStoppedAfterFault;
            public bool disarmedAfterFault;
            public string faultReason;
            public bool hasHud;
            public bool hasCamera;
            public bool prefabCarriesBothLaws;
            public float fixedDeltaTimeUsed;

            // Law switch run.
            public readonly List<string> switchLog = new List<string>();
            public bool switchToV2Accepted, switchRefusedWhileDeflected, refusalChangedNothing, switchToV1Accepted;
            public bool v2BoundAfterSwitch, v1BoundAfterSwitchBack;
            public double maxSurfaceStepAtSwitchDeg;
            public bool v1GearingAfterSwitchBack;

            // Fail-closed switch run.
            public double trimDriftBeforeAttempts = double.NaN;
            public int refusalsAttempted, refusalsOk;
        }

        /// <summary>Everything a refused law switch must leave untouched.</summary>
        private struct SwitchSnapshot
        {
            public MavF15PilotControlMode mode;
            public bool v1Enabled, v2Enabled;
            public MavFlightControlLawBase bound;
            public MavFlightPhysicsOwner owner;
            public string ownerReason;
            public bool armed;
            public MavF15SurfaceState actuatorRequest, actualSurfaces, v1Request, v2Request;

            public static SwitchSnapshot Of(MavF15PilotControlledRig rig, MavFlightPhysicsOwnership gate)
            {
                return new SwitchSnapshot
                {
                    mode = rig.controlMode,
                    v1Enabled = rig.law != null && rig.law.enabled,
                    v2Enabled = rig.lawV2 != null && rig.lawV2.enabled,
                    bound = rig.body.controlLaw,
                    owner = gate.owner,
                    ownerReason = gate.ownerReason,
                    armed = rig.body.ArmedForLiveFlight,
                    actuatorRequest = rig.actuator.requested.channels,
                    actualSurfaces = rig.actuator.ActualF15SurfaceState.channels,
                    v1Request = rig.law.debugRequested.channels,
                    v2Request = rig.lawV2 != null ? rig.lawV2.debugRequested.channels : MavF15SurfaceState.Neutral
                };
            }

            public bool SameAs(SwitchSnapshot o)
            {
                if (mode != o.mode || v1Enabled != o.v1Enabled || v2Enabled != o.v2Enabled || bound != o.bound || owner != o.owner
                    || ownerReason != o.ownerReason || armed != o.armed)
                    return false;
                for (int c = 0; c < 4; c++)
                {
                    MavF15SurfaceChannel ch = (MavF15SurfaceChannel)c;
                    if (!actuatorRequest.Get(ch).Equals(o.actuatorRequest.Get(ch)) || !actualSurfaces.Get(ch).Equals(o.actualSurfaces.Get(ch))
                        || !v1Request.Get(ch).Equals(o.v1Request.Get(ch)) || !v2Request.Get(ch).Equals(o.v2Request.Get(ch)))
                        return false;
                }

                return true;
            }
        }

        private readonly List<Run> runs = new List<Run>();
        private readonly StringBuilder sb = new StringBuilder(96 * 1024);
        private int runIndex;
        private int phase;
        private int stepsDone;
        private int waitSteps;
        private MavF15PilotControlledRig rig;
        private MavSixDoFBody activeBody;
        private MavManualPilotCommandSource activeSource;
        private MavFlightPhysicsOwnership activeGate;
        private float savedFixedDeltaTime;
        private int savedCaptureFramerate;
        private bool completeDone;
        private float trimBias;
        private double[] equilibrium;
        private MavF15GameplayControlAuthority authority;

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
            trimBias = (float)t.symmetricStabilatorDeg;
            equilibrium = new[] { t.alphaDeg / RadToDeg, 0.0, 0.0, 0.0, 0.0, t.thetaDeg / RadToDeg, 0.0, t.trueAirspeedFtPerSec };
            authority = MavF15GameplayControlAuthority.V1();
            BuildRuns();
        }

        private void BuildRuns()
        {
            runs.Add(new Run { id = "NEUTRAL-HOLD V1", kind = Kind.Neutral, mode = MavF15PilotControlMode.DirectV1, dt = BaseDt, steps = Steps(20f, BaseDt) });
            runs.Add(new Run { id = "NEUTRAL-HOLD V2", kind = Kind.Neutral, mode = MavF15PilotControlMode.AssistedV2, dt = BaseDt, steps = Steps(20f, BaseDt) });
            runs.Add(new Run
            {
                id = PrefabToTest != null ? "HUMAN V2 PREFAB hold" : "HUMAN V2 KEYBOARD RIG hold", kind = Kind.HumanRig,
                mode = MavF15PilotControlMode.AssistedV2, dt = BaseDt, steps = Steps(5f, BaseDt)
            });

            foreach (MavF15PilotControlMode m in new[] { MavF15PilotControlMode.DirectV1, MavF15PilotControlMode.AssistedV2 })
            {
                AddPulse(m, "pitch +", PulseAmplitude, 0f, 0f);
                AddPulse(m, "pitch -", -PulseAmplitude, 0f, 0f);
                AddPulse(m, "roll +", 0f, PulseAmplitude, 0f);
                AddPulse(m, "roll -", 0f, -PulseAmplitude, 0f);
                AddPulse(m, "yaw +", 0f, 0f, PulseAmplitude);
                AddPulse(m, "yaw -", 0f, 0f, -PulseAmplitude);
                AddPulse(m, "pitch+roll", 0.3f, 0.3f, 0f);
            }

            foreach (float dt in new[] { BaseDt, BaseDt / 2f, BaseDt / 4f })
            {
                runs.Add(new Run
                {
                    id = "SEQUENCE V2 dt " + dt.ToString("F4", CultureInfo.InvariantCulture), kind = Kind.Sequence,
                    mode = MavF15PilotControlMode.AssistedV2, dt = dt, steps = Steps(SequenceDurationS, dt)
                });
            }

            runs.Add(new Run { id = "SEQUENCE V1 dt 0.0200", kind = Kind.Sequence, mode = MavF15PilotControlMode.DirectV1, dt = BaseDt, steps = Steps(SequenceDurationS, BaseDt) });
            runs.Add(new Run { id = "LAW SWITCH V1->V2->V1", kind = Kind.Switch, mode = MavF15PilotControlMode.DirectV1, dt = BaseDt, steps = Steps(12f, BaseDt) });
            runs.Add(new Run { id = "SWITCH FAIL-CLOSED V1->V2->V1", kind = Kind.SwitchFailClosed, mode = MavF15PilotControlMode.DirectV1, dt = BaseDt, steps = Steps(8f, BaseDt) });
            runs.Add(new Run { id = "OWNERSHIP-LOSS V2", kind = Kind.OwnershipLoss, mode = MavF15PilotControlMode.AssistedV2, dt = BaseDt, steps = 100, lossStep = 25 });
            runs.Add(new Run { id = "SECOND REQUESTER V2", kind = Kind.SecondRequester, mode = MavF15PilotControlMode.AssistedV2, dt = BaseDt, steps = 100, lossStep = 25 });
        }

        private void AddPulse(MavF15PilotControlMode mode, string what, float pitch, float roll, float yaw)
        {
            runs.Add(new Run
            {
                id = "PULSE " + (mode == MavF15PilotControlMode.DirectV1 ? "V1 " : "V2 ") + what, kind = Kind.Pulse, mode = mode, dt = BaseDt,
                steps = Steps(8f, BaseDt), pitch = pitch, roll = roll, yaw = yaw,
                pulseStartStep = Steps(1f, BaseDt), pulseEndStep = Steps(2f, BaseDt)
            });
        }

        private static int Steps(float seconds, float dt)
        {
            return Mathf.RoundToInt(seconds / dt);
        }

        private static bool In(int stepIndex, float dt, float from, float to)
        {
            return stepIndex >= Steps(from, dt) && stepIndex < Steps(to, dt);
        }

        private MavPilotCommand CommandAt(Run run, int stepIndex)
        {
            MavPilotCommand c = MavPilotCommand.Neutral;
            if (run.kind == Kind.Pulse)
            {
                if (stepIndex >= run.pulseStartStep && stepIndex < run.pulseEndStep)
                {
                    c.pitch = run.pitch;
                    c.roll = run.roll;
                    c.yaw = run.yaw;
                }
            }
            else if (run.kind == Kind.Sequence)
            {
                float[] w = SequenceWindows;
                if (In(stepIndex, run.dt, w[0], w[1])) c.pitch = PulseAmplitude;
                if (In(stepIndex, run.dt, w[2], w[3])) c.pitch = -PulseAmplitude;
                if (In(stepIndex, run.dt, w[4], w[5])) c.roll = PulseAmplitude;
                if (In(stepIndex, run.dt, w[6], w[7])) c.roll = -PulseAmplitude;
                if (In(stepIndex, run.dt, w[8], w[9])) c.yaw = PulseAmplitude;
                if (In(stepIndex, run.dt, w[10], w[11])) c.yaw = -PulseAmplitude;
                if (In(stepIndex, run.dt, w[12], w[13]))
                {
                    c.pitch = 0.3f;
                    c.roll = 0.3f;
                }
            }
            else if (run.kind == Kind.Switch)
            {
                // Roll +0.5 at 3-4 s (under V2, with a refused switch attempt inside it) and 9-10 s (under V1 again).
                if (In(stepIndex, run.dt, 3f, 4f) || In(stepIndex, run.dt, 9f, 10f))
                    c.roll = PulseAmplitude;
            }

            return c;
        }

        // ================================================================== state machine

        private void FixedUpdate()
        {
            if (completeDone)
                return;

            if (runIndex >= runs.Count)
            {
                completeDone = true;
                Complete();
                return;
            }

            Run run = runs[runIndex];
            try
            {
                if (phase == 0)
                {
                    // Build one step BEFORE starting: a component added during a FixedUpdate first steps on the
                    // next physics step.
                    Time.fixedDeltaTime = run.dt;
                    Build(run);
                    phase = 1;
                    return;
                }

                if (phase == 1)
                {
                    Begin(run);
                    return;
                }

                Continue(run);
            }
            catch (Exception e)
            {
                run.failure = "exception: " + e.GetType().Name + ": " + e.Message;
                Finish(run);
            }
        }

        private void Build(Run run)
        {
            if (run.kind == Kind.HumanRig)
            {
                if (PrefabToTest != null)
                {
                    GameObject instance = Object.Instantiate(PrefabToTest);
                    instance.name = "f15pc2-human-prefab";
                    rig = instance.GetComponent<MavF15PilotControlledRig>();
                }
                else
                {
                    rig = MavF15PilotControlledRig.Create("f15pc2-human-keyboard-rig", MavF15PilotCommandSourceKind.Keyboard, true, MavF15PilotControlMode.AssistedV2);
                }

                return;
            }

            rig = MavF15PilotControlledRig.Create("f15pc2-" + run.id, MavF15PilotCommandSourceKind.Scripted, false, run.mode);
        }

        private void Begin(Run run)
        {
            stepsDone = 0;
            waitSteps = 0;

            if (run.kind == Kind.HumanRig)
            {
                run.hasHud = rig != null && rig.GetComponent<MavF15PilotControlledHud>() != null;
                run.hasCamera = rig != null && rig.GetComponentInChildren<Camera>() != null;
                run.prefabCarriesBothLaws = rig != null && rig.GetComponent<MavF15PilotControlLaw>() != null && rig.GetComponent<MavF15PilotControlLawV2>() != null;
                activeBody = rig != null ? rig.body : null;
                activeSource = null;
                phase = 2;
                return;
            }

            run.started = rig.StartFromTrim();
            run.startStatus = rig.debugStartStatus;
            activeBody = rig.body;
            activeSource = (MavManualPilotCommandSource)rig.commandSource;
            activeGate = rig.ownership;

            if (!run.started)
            {
                run.failure = "start refused: " + run.startStatus;
                Finish(run);
                return;
            }

            run.samples.Add(Capture(0.0));
            if (activeSource != null)
                activeSource.command = CommandAt(run, 0);
            phase = 2;
        }

        private void Continue(Run run)
        {
            if (run.kind == Kind.HumanRig && !run.started)
            {
                if (rig != null && rig.debugStarted)
                {
                    run.started = rig.debugStartSucceeded;
                    run.startStatus = rig.debugStartStatus;
                    activeBody = rig.body;
                    activeGate = rig.ownership;
                    if (!run.started)
                    {
                        run.failure = "the human rig refused to start: " + run.startStatus;
                        Finish(run);
                        return;
                    }

                    run.samples.Add(Capture(0.0));
                    return;
                }

                if (++waitSteps > 10)
                {
                    run.failure = "the human rig did not start itself within 10 physics steps";
                    Finish(run);
                }

                return;
            }

            stepsDone++;
            MavSixDoFBody body = activeBody;

            if (Mathf.Abs(Time.fixedDeltaTime - run.dt) > 1e-5f * run.dt)
                run.dtAsConfigured = false;
            run.fixedDeltaTimeUsed = Time.fixedDeltaTime;

            Sample s = Capture(stepsDone * (double)run.fixedDeltaTimeUsed);
            Sample prev = run.samples[run.samples.Count - 1];
            run.samples.Add(s);

            bool finite = true;
            for (int k = 0; k < 8; k++)
                finite &= !double.IsNaN(s.x[k]) && !double.IsInfinity(s.x[k]);
            finite &= s.surfaces.IsFinite() && IsFinite(s.position) && IsFinite(s.velocity);
            if (!finite)
                run.finiteEveryStep = false;
            if (!InsideEnvelope(s.surfaces))
                run.surfacesInsideEnvelopeEveryStep = false;

            double residual = (s.position - prev.position - s.velocity * run.fixedDeltaTimeUsed).magnitude;
            run.maxKinematicResidualM = Math.Max(run.maxKinematicResidualM, residual);
            run.maxVelocityStepMps = Math.Max(run.maxVelocityStepMps, (s.velocity - prev.velocity).magnitude);
            if (s.aeroRefused)
                run.aeroRefusedSteps++;

            bool afterLoss = run.lossStep >= 0 && stepsDone > run.lossStep;
            if (!afterLoss && !(run.lossStep >= 0 && stepsDone == run.lossStep))
            {
                if (activeGate == null || activeGate.owner != MavFlightPhysicsOwner.F15PilotControlledResearch || !body.ArmedForLiveFlight)
                    run.ownerHeldEveryStep = false;
                if (CountArmedBodies() != 1)
                    run.exactlyOneArmedEveryStep = false;
                if (!ExactlyOneLaw(rig))
                    run.exactlyOneLawEveryStep = false;
                Rigidbody rb = body.GetComponent<Rigidbody>();
                if (rb == null || rb.useGravity)
                    run.unityGravityOffEveryStep = false;
            }

            if (run.lossStep >= 0 && stepsDone == run.lossStep)
            {
                run.loadApplicationsAtEnd = body.debugLoadApplications;
                if (run.kind == Kind.OwnershipLoss)
                    body.profileProvider = null;          // the source environment is gone
                else
                    rig.law.enabled = true;               // a second surface requester appears next to the bound V2 law
            }

            if (run.lossStep >= 0 && stepsDone == run.lossStep + 1)
            {
                run.faultedNextStep = activeGate.owner == MavFlightPhysicsOwner.Fault;
                run.disarmedAfterFault = !body.ArmedForLiveFlight;
                run.faultReason = activeGate.ownerReason;
            }

            if (run.kind == Kind.Switch)
                StepSwitch(run);
            else if (run.kind == Kind.SwitchFailClosed)
                StepSwitchFailClosed(run);

            if (stepsDone >= run.steps)
            {
                if (run.lossStep >= 0)
                    run.loadsStoppedAfterFault = body.debugLoadApplications == run.loadApplicationsAtEnd;
                Finish(run);
                return;
            }

            if (activeSource != null)
                activeSource.command = CommandAt(run, stepsDone);
        }

        /// <summary>The law-switch script: V1 until 2 s, V2 (a refused attempt at 3.5 s, stick deflected), V1 again from 8 s.</summary>
        private void StepSwitch(Run run)
        {
            string reason;
            if (stepsDone == Steps(2f, run.dt))
            {
                MavF15SurfaceState before = rig.actuator.ActualF15SurfaceState.channels;
                run.switchToV2Accepted = rig.TrySetControlMode(MavF15PilotControlMode.AssistedV2, out reason);
                run.switchLog.Add("t 2.0 s: " + reason);
                run.v2BoundAfterSwitch = rig.body.controlLaw == rig.lawV2 && rig.lawV2 != null && rig.lawV2.enabled && !rig.law.enabled;
                switchMark = run.samples.Count - 1;
                switchBefore = before;
            }
            else if (stepsDone == Steps(3.5f, run.dt))
            {
                bool refused = !rig.TrySetControlMode(MavF15PilotControlMode.DirectV1, out reason);
                run.switchLog.Add("t 3.5 s (roll +0.5 held): " + reason);
                run.switchRefusedWhileDeflected = refused;
                run.refusalChangedNothing = rig.controlMode == MavF15PilotControlMode.AssistedV2 && rig.body.controlLaw == rig.lawV2
                                            && rig.lawV2.enabled && !rig.law.enabled;
            }
            else if (stepsDone == Steps(8f, run.dt))
            {
                run.switchToV1Accepted = rig.TrySetControlMode(MavF15PilotControlMode.DirectV1, out reason);
                run.switchLog.Add("t 8.0 s: " + reason);
                run.v1BoundAfterSwitchBack = rig.body.controlLaw == rig.law && rig.law.enabled && !rig.lawV2.enabled;
                switchMark2 = run.samples.Count - 1;
            }
            else if (stepsDone == Steps(9.5f, run.dt))
            {
                // Under V1 again the roll pulse is pure V1 gearing: aileron = 0.5 x 20 deg, no interconnect rudder.
                MavF15SurfaceState s = rig.actuator.ActualF15SurfaceState.channels;
                run.v1GearingAfterSwitchBack = s.aileronDeg == 0.5f * authority.rollAileronDegPerUnit && s.rudderDeg == 0f
                                               && s.symmetricStabilatorDeg == trimBias;
            }
        }

        /// <summary>
        /// The fail-closed switch script, V1 at centred stick throughout: the trim is checked at 1 s; switch attempts are refused
        /// at 1.5 s (pitch 0.3 on the stick at the attempt), 2.0 s (command unavailable), 2.5 s (command source missing) and
        /// 2.8 s (command source disabled) - each condition is restored before the laws step, so the aircraft never sees it;
        /// then V1 -> V2 at 3 s and V2 -> V1 at 6 s, centred with the source restored.
        /// </summary>
        private void StepSwitchFailClosed(Run run)
        {
            string reason;
            if (stepsDone == Steps(1f, run.dt))
            {
                run.trimDriftBeforeAttempts = Max(Drift(run));
            }
            else if (stepsDone == Steps(1.5f, run.dt))
            {
                activeSource.command = new MavPilotCommand { pitch = 0.3f };
                AttemptRefused(run, "t 1.5 s, pitch 0.3 on the stick", MavF15PilotControlledRig.SwitchRefusedNotCentred);
                activeSource.command = MavPilotCommand.Neutral;
            }
            else if (stepsDone == Steps(2f, run.dt))
            {
                activeSource.commandAvailable = false;
                AttemptRefused(run, "t 2.0 s, command unavailable", MavF15PilotControlledRig.SwitchRefusedCommandUnavailable);
                activeSource.commandAvailable = true;
            }
            else if (stepsDone == Steps(2.5f, run.dt))
            {
                MavPilotCommandSourceBase wired = rig.commandSource;
                rig.commandSource = null;
                AttemptRefused(run, "t 2.5 s, command source missing", MavF15PilotControlledRig.SwitchRefusedNoCommandSource);
                rig.commandSource = wired;
            }
            else if (stepsDone == Steps(2.8f, run.dt))
            {
                activeSource.enabled = false;
                AttemptRefused(run, "t 2.8 s, command source disabled", MavF15PilotControlledRig.SwitchRefusedCommandSourceInactive);
                activeSource.enabled = true;
            }
            else if (stepsDone == Steps(3f, run.dt))
            {
                run.switchToV2Accepted = rig.TrySetControlMode(MavF15PilotControlMode.AssistedV2, out reason);
                run.switchLog.Add("t 3.0 s, centred, source restored: " + reason);
                run.v2BoundAfterSwitch = rig.body.controlLaw == rig.lawV2 && rig.lawV2 != null && rig.lawV2.enabled && !rig.law.enabled;
                switchMark = run.samples.Count - 1;
            }
            else if (stepsDone == Steps(6f, run.dt))
            {
                run.switchToV1Accepted = rig.TrySetControlMode(MavF15PilotControlMode.DirectV1, out reason);
                run.switchLog.Add("t 6.0 s, centred: " + reason);
                run.v1BoundAfterSwitchBack = rig.body.controlLaw == rig.law && rig.law.enabled && !rig.lawV2.enabled;
                switchMark2 = run.samples.Count - 1;
            }
        }

        private void AttemptRefused(Run run, string label, string expectedReasonPrefix)
        {
            SwitchSnapshot before = SwitchSnapshot.Of(rig, activeGate);
            string reason;
            bool refused = !rig.TrySetControlMode(MavF15PilotControlMode.AssistedV2, out reason);
            bool unchanged = SwitchSnapshot.Of(rig, activeGate).SameAs(before);
            bool expected = reason != null && reason.StartsWith(expectedReasonPrefix, StringComparison.Ordinal);
            run.refusalsAttempted++;
            if (refused && unchanged && expected)
                run.refusalsOk++;
            run.switchLog.Add(label + ": " + (refused ? "refused" : "ACCEPTED") + (unchanged ? ", nothing changed" : ", STATE CHANGED") + " - " + reason);
        }

        private int switchMark = -1, switchMark2 = -1;
        private MavF15SurfaceState switchBefore;

        private void Finish(Run run)
        {
            if (activeBody != null)
            {
                if (run.lossStep < 0)
                    run.loadApplicationsAtEnd = activeBody.debugLoadApplications;
                run.duplicateRejections = activeBody.debugRejectedDuplicateApplications;
                run.initialStateApplications = activeBody.debugInitialStateApplications;
            }

            if (run.kind == Kind.Switch || run.kind == Kind.SwitchFailClosed)
            {
                run.maxSurfaceStepAtSwitchDeg = Math.Max(SurfaceStepAround(run, switchMark), SurfaceStepAround(run, switchMark2));
                switchMark = switchMark2 = -1;
            }

            run.completed = run.failure == null && stepsDone >= run.steps;

            if (activeGate != null && activeGate.owner != MavFlightPhysicsOwner.Fault)
                activeGate.ReturnToLegacy("pilot-control V2 validation run complete");

            if (rig != null)
                Object.DestroyImmediate(rig.gameObject);

            rig = null;
            activeBody = null;
            activeSource = null;
            activeGate = null;
            runIndex++;
            phase = 0;
        }

        /// <summary>Largest single-step surface change in the 3 steps around a switch.</summary>
        private static double SurfaceStepAround(Run r, int mark)
        {
            if (mark < 1)
                return double.NaN;
            double worst = 0;
            for (int i = Math.Max(1, mark - 1); i <= Math.Min(r.samples.Count - 1, mark + 2); i++)
            {
                MavF15SurfaceState a = r.samples[i - 1].surfaces, b = r.samples[i].surfaces;
                for (int c = 0; c < 4; c++)
                    worst = Math.Max(worst, Math.Abs(b.Get((MavF15SurfaceChannel)c) - a.Get((MavF15SurfaceChannel)c)));
            }

            return worst;
        }

        private Sample Capture(double t)
        {
            MavFlightState s = MavF15AfitResearchStateInjection.ReadBack(activeBody);
            Rigidbody rb = activeBody.GetComponent<Rigidbody>();
            MavF15ControlActuator actuator = activeBody.controlSurfaceActuator as MavF15ControlActuator;
            MavF15PilotControlledAeroModel aero = activeBody.aerodynamicModel as MavF15PilotControlledAeroModel;
            MavF15PilotControlLaw v1 = activeBody.controlLaw as MavF15PilotControlLaw;
            MavF15PilotControlLawV2 v2 = activeBody.controlLaw as MavF15PilotControlLawV2;
            return new Sample
            {
                t = t,
                x = new double[]
                {
                    s.alphaRad, s.betaRad, s.aeroBodyRatesRadSec.x, s.aeroBodyRatesRadSec.y, s.aeroBodyRatesRadSec.z,
                    s.attitude.pitchAttitudeRad, s.attitude.bankAngleRad, s.trueAirspeedMps / MavF15BaumannMach06Reference.FootToM
                },
                position = rb.position,
                velocity = rb.linearVelocity,
                surfaces = actuator != null ? actuator.ActualF15SurfaceState.channels : MavF15SurfaceState.Neutral,
                aeroRefused = aero != null && aero.debugRefused && t > 0.0,
                limited = (v1 != null && v1.debugEnvelopeLimited) || (v2 != null && v2.debugEnvelopeLimited)
            };
        }

        private bool InsideEnvelope(MavF15SurfaceState s)
        {
            return s.symmetricStabilatorDeg >= authority.symmetricStabilator.minDeg && s.symmetricStabilatorDeg <= authority.symmetricStabilator.maxDeg
                   && s.aileronDeg >= authority.aileron.minDeg && s.aileronDeg <= authority.aileron.maxDeg
                   && s.differentialStabilatorDeg >= authority.differentialStabilator.minDeg && s.differentialStabilatorDeg <= authority.differentialStabilator.maxDeg
                   && s.rudderDeg >= authority.rudder.minDeg && s.rudderDeg <= authority.rudder.maxDeg;
        }

        private static bool ExactlyOneLaw(MavF15PilotControlledRig r)
        {
            if (r == null)
                return false;
            int enabled = (r.law != null && r.law.enabled ? 1 : 0) + (r.lawV2 != null && r.lawV2.enabled ? 1 : 0);
            return enabled == 1 && r.body.controlLaw == r.ActiveLaw && r.ActiveLaw.enabled;
        }

        private static int CountArmedBodies()
        {
            int n = 0;
            foreach (MavSixDoFBody b in Object.FindObjectsByType<MavSixDoFBody>(FindObjectsSortMode.None))
            {
                if (b != null && b.ArmedForLiveFlight)
                    n++;
            }

            return n;
        }

        // ================================================================== evaluation

        private void Complete()
        {
            Time.fixedDeltaTime = savedFixedDeltaTime;
            Time.captureFramerate = savedCaptureFramerate;

            sb.AppendLine("F-15 PILOT CONTROL V2 - PLAY MODE FLIGHT TEST + V1 vs V2 COMPARISON (" + MavF15PilotControlledIdentity.ConfigurationId + ")");
            sb.AppendLine("=====================================================================================");
            sb.AppendLine("Research aerodynamics (frozen AFIT/Baumann/Davison baseline) + Maverick pilot-control laws V1 (direct) and V2 (closed-loop).");
            sb.AppendLine("THIS IS NOT THE F-15 FCS. NOT NASA 836. Every control value is " + MavF15PilotControlProvenance.MaverickTunedNonAuthoritative + ".");
            sb.AppendLine("Scripted normalized pilot commands; temporary rigs on the empty batch scene; human V2 prefab: " + PrefabPath);
            MavF15PilotTrimStart t = MavF15PilotTrimStart.TableViiPoint36();
            MavF15PilotControlGainsV2 g = MavF15PilotControlGainsV2.V2();
            sb.AppendLine("Trim start: Table VII point " + t.tableViiPoint + " - stab " + t.symmetricStabilatorDeg.ToString("F5", CultureInfo.InvariantCulture)
                          + " deg, alpha " + t.alphaDeg.ToString("F4", CultureInfo.InvariantCulture) + ", V " + t.trueAirspeedFtPerSec.ToString("F1", CultureInfo.InvariantCulture)
                          + " ft/s; pulse amplitude " + PulseAmplitude + " (1.0-2.0 s); V2 full-stick commands: q " + g.commandedPitchRateAtFullStickDegSec
                          + " deg/s, p_s " + g.commandedRollRateAtFullStickDegSec + " deg/s, beta " + g.commandedSideslipAtFullPedalDeg + " deg");

            EvaluateMechanics();
            EvaluateNeutral();
            EvaluateHumanRig();
            EvaluatePulses();
            EvaluateComparison();
            EvaluateSequence();
            EvaluateConvergence();
            EvaluateSwitch();
            EvaluateSwitchFailClosed();
            EvaluateFailClosed();

            sb.AppendLine();
            sb.Append("RESULT: ").Append(Failed == 0 ? "PASS" : "FAIL").Append("  passed=").Append(Passed).Append(" failed=").Append(Failed);
            Report = sb.ToString();
            Finished = true;
        }

        private void EvaluateMechanics()
        {
            sb.AppendLine();
            sb.AppendLine("[M] Runtime mechanics of every run (owner, one armed body, one enabled law, one load application per step, no teleport, finite, surfaces in envelope)");
            foreach (Run r in runs)
            {
                bool failClosed = r.kind == Kind.OwnershipLoss || r.kind == Kind.SecondRequester;
                int expectedLoads = failClosed ? r.lossStep : r.steps;
                bool ok = r.completed && r.failure == null && r.finiteEveryStep && r.ownerHeldEveryStep && r.exactlyOneArmedEveryStep
                          && r.exactlyOneLawEveryStep && r.unityGravityOffEveryStep && r.dtAsConfigured && r.duplicateRejections == 0
                          && r.initialStateApplications == 1 && r.surfacesInsideEnvelopeEveryStep
                          && r.maxKinematicResidualM < 2e-3 && r.maxVelocityStepMps < 2.0
                          && (failClosed || r.kind == Kind.HumanRig ? r.loadApplicationsAtEnd >= expectedLoads - 1 : r.loadApplicationsAtEnd == expectedLoads);
                Check(ok, "M", r.id.PadRight(26) + " " + r.steps + " steps, loads " + r.loadApplicationsAtEnd + ", duplicate refusals " + r.duplicateRejections
                    + ", initial-state writes " + r.initialStateApplications + ", owner held " + r.ownerHeldEveryStep + ", one armed body " + r.exactlyOneArmedEveryStep
                    + ", one law " + r.exactlyOneLawEveryStep + ", surfaces in envelope " + r.surfacesInsideEnvelopeEveryStep + ", finite " + r.finiteEveryStep
                    + ", max |dx - v dt| " + r.maxKinematicResidualM.ToString("E1") + " m, aero refused " + r.aeroRefusedSteps + " steps, dt "
                    + r.fixedDeltaTimeUsed.ToString("R", CultureInfo.InvariantCulture) + (r.failure == null ? "" : " - " + r.failure));
            }
        }

        private void EvaluateNeutral()
        {
            sb.AppendLine();
            sb.AppendLine("[H] Neutral hold - centred stick holds the validated trim under both laws");
            Run v1 = runs.Find(x => x.kind == Kind.Neutral && x.mode == MavF15PilotControlMode.DirectV1);
            Run v2 = runs.Find(x => x.kind == Kind.Neutral && x.mode == MavF15PilotControlMode.AssistedV2);
            if (v1 == null || v2 == null || !v1.completed || !v2.completed)
            {
                Check(false, "H", "both neutral runs completed");
                return;
            }

            double w1 = Max(Drift(v1)), w2 = Max(Drift(v2));
            double surf = 0;
            foreach (Sample s in v2.samples)
                surf = Math.Max(surf, SurfaceDeviation(s.surfaces));
            bool v1Exact = true;
            foreach (Sample s in v1.samples)
                v1Exact &= SurfaceDeviation(s.surfaces) == 0.0;
            double diff = 0;
            for (int i = 0; i < Math.Min(v1.samples.Count, v2.samples.Count); i++)
                for (int k = 0; k < 8; k++)
                    diff = Math.Max(diff, Math.Abs(v1.samples[i].x[k] - v2.samples[i].x[k]) / (k == 7 ? equilibrium[7] : 1.0));
            sb.AppendLine("    V1 20 s: max |x - trim| " + DescribeStates(Drift(v1)));
            sb.AppendLine("    V2 20 s: max |x - trim| " + DescribeStates(Drift(v2)));
            Check(v1Exact && w1 <= NeutralTolerance && v1.aeroRefusedSteps == 0,
                "H", "V1 at centred stick: surfaces exactly at the trim bias for 20 s, every state within " + w1.ToString("E1") + " of the trim (V1 unchanged)");
            Check(w2 <= NeutralTolerance && surf < 1e-3 && v2.aeroRefusedSteps == 0,
                "H", "V2 at centred stick: every state within " + w2.ToString("E1") + " of the trim for 20 s (tolerance " + NeutralTolerance.ToString("E0")
                     + "), surfaces within " + surf.ToString("E1") + " deg of the trim bias / zero - the feedback acts only on round-off; V1 and V2 trajectories differ by at most "
                     + diff.ToString("E1"));
        }

        private void EvaluateHumanRig()
        {
            sb.AppendLine();
            sb.AppendLine("[K] The human-flown V2 rig (" + (PrefabToTest != null ? "prefab " + PrefabPath : "keyboard rig") + "): starts itself in Assisted V2, holds trim with no input");
            Run r = runs.Find(x => x.kind == Kind.HumanRig);
            if (r == null || !r.completed)
            {
                Check(false, "K", "the human rig run completed" + (r != null && r.failure != null ? " - " + r.failure : ""));
                return;
            }

            double worst = Max(Drift(r));
            Check(r.started && worst <= NeutralTolerance && r.ownerHeldEveryStep && r.exactlyOneLawEveryStep,
                "K", "started itself (" + r.startStatus + ") under the V2 law, keyboard source with no key held = neutral, held the trim for 5 s within "
                     + worst.ToString("E1"));
            if (PrefabToTest != null)
                Check(r.hasHud && r.hasCamera && r.prefabCarriesBothLaws,
                    "K", "the V2 prefab carries its HUD (F2 selects Direct V1 / Assisted V2), its camera, and both laws (V1 disabled)");
        }

        private sealed class PulseMetrics
        {
            public int axis;
            public double delta, commanded, peakDuring, postPeak, overshoot, settleS, residual, betaMaxDeg, alphaMinDeg, alphaMaxDeg;
            public double stabDev, ailMax, rudMax, pAtEnd;
            public int refused;
        }

        /// <summary>Axis: 3 = q, 8 = stability-axis roll rate, 4 = r.</summary>
        private static double Axis(Sample s, int axis)
        {
            return axis == 8 ? s.x[2] * Math.Cos(s.x[0]) + s.x[4] * Math.Sin(s.x[0]) : s.x[axis];
        }

        private PulseMetrics Measure(Run r)
        {
            PulseMetrics m = new PulseMetrics();
            m.axis = r.pitch != 0f ? 3 : r.roll != 0f ? 8 : 4;
            int s0 = r.pulseStartStep, s1 = Math.Min(r.pulseEndStep, r.samples.Count - 1);
            m.delta = Axis(r.samples[s1], m.axis) - Axis(r.samples[s0], m.axis);
            m.pAtEnd = r.samples[s1].x[2];
            MavF15PilotControlGainsV2 g = MavF15PilotControlGainsV2.V2();
            m.commanded = m.axis == 3 ? r.pitch * g.commandedPitchRateAtFullStickDegSec / RadToDeg
                : m.axis == 8 ? r.roll * g.commandedRollRateAtFullStickDegSec / RadToDeg : double.NaN;
            int sign = Math.Sign(m.delta);
            for (int i = s0; i <= s1; i++)
                m.peakDuring = Math.Max(m.peakDuring, Math.Abs(Axis(r.samples[i], m.axis)));
            double opposite = 0;
            for (int i = s1 + 1; i < r.samples.Count; i++)
            {
                double v = Axis(r.samples[i], m.axis);
                m.postPeak = Math.Max(m.postPeak, Math.Abs(v));
                opposite = Math.Max(opposite, -sign * v);
            }

            m.overshoot = m.peakDuring > 0 ? opposite / m.peakDuring : 0;
            m.settleS = double.NaN;
            for (int i = r.samples.Count - 1; i > s1; i--)
            {
                if (Math.Abs(Axis(r.samples[i], m.axis)) > 0.1 * m.peakDuring)
                {
                    m.settleS = i < r.samples.Count - 1 ? (i + 1 - s1) * r.dt : double.PositiveInfinity;
                    break;
                }
            }

            if (double.IsNaN(m.settleS))
                m.settleS = r.dt;
            for (int i = r.samples.Count - Steps(1f, r.dt); i < r.samples.Count; i++)
                m.residual = Math.Max(m.residual, Math.Abs(Axis(r.samples[i], m.axis)));

            m.alphaMinDeg = double.MaxValue;
            m.alphaMaxDeg = double.MinValue;
            foreach (Sample s in r.samples)
            {
                m.betaMaxDeg = Math.Max(m.betaMaxDeg, Math.Abs(s.x[1]) * RadToDeg);
                m.alphaMinDeg = Math.Min(m.alphaMinDeg, s.x[0] * RadToDeg);
                m.alphaMaxDeg = Math.Max(m.alphaMaxDeg, s.x[0] * RadToDeg);
                m.stabDev = Math.Max(m.stabDev, Math.Abs(s.surfaces.symmetricStabilatorDeg - trimBias));
                m.ailMax = Math.Max(m.ailMax, Math.Abs(s.surfaces.aileronDeg));
                m.rudMax = Math.Max(m.rudMax, Math.Abs(s.surfaces.rudderDeg));
            }

            m.refused = r.aeroRefusedSteps;
            return m;
        }

        private void EvaluatePulses()
        {
            sb.AppendLine();
            sb.AppendLine("[S] V2 sign response in Play Mode - 1 s pulses from trim, then 6 s centred");
            foreach (Run r in runs)
            {
                if (r.kind != Kind.Pulse || r.mode != MavF15PilotControlMode.AssistedV2)
                    continue;
                if (!r.completed)
                {
                    Check(false, "S", r.id + " completed");
                    continue;
                }

                PulseMetrics m = Measure(r);
                int s0 = r.pulseStartStep, s1 = r.pulseEndStep;
                double[] d = Delta(r, s0, s1);
                bool ok;
                string what;
                if (r.pitch != 0f && r.roll != 0f)
                {
                    ok = d[3] > 0 && Axis(r.samples[s1], 8) > 0;
                    what = "q " + d[3].ToString("+0.0000;-0.0000") + " and p_s " + Axis(r.samples[s1], 8).ToString("+0.0000;-0.0000") + " rad/s (both + expected)";
                }
                else if (r.pitch != 0f)
                {
                    ok = Math.Sign(d[3]) == Math.Sign(r.pitch) && Math.Sign(d[5]) == Math.Sign(r.pitch);
                    what = "q " + d[3].ToString("+0.0000;-0.0000") + " rad/s (commanded " + m.commanded.ToString("+0.0000;-0.0000") + "), theta "
                           + (d[5] * RadToDeg).ToString("+0.000;-0.000") + " deg";
                }
                else if (r.roll != 0f)
                {
                    ok = Math.Sign(m.delta) == Math.Sign(r.roll) && Math.Sign(d[6]) == Math.Sign(r.roll);
                    what = "p_s " + m.delta.ToString("+0.0000;-0.0000") + " rad/s (commanded " + m.commanded.ToString("+0.0000;-0.0000") + "), phi "
                           + (d[6] * RadToDeg).ToString("+0.000;-0.000") + " deg";
                }
                else
                {
                    ok = Math.Sign(d[4]) == Math.Sign(r.yaw) && Math.Sign(d[1]) == -Math.Sign(r.yaw);
                    what = "r " + d[4].ToString("+0.0000;-0.0000") + " rad/s, beta " + (d[1] * RadToDeg).ToString("+0.000;-0.000") + " deg (nose right = beta < 0)";
                }

                // Release: the surfaces go back toward the trim as the rates decay (V2 keeps feeding back what remains).
                double during = 0, late = 0;
                for (int i = s0; i <= s1; i++)
                    during = Math.Max(during, SurfaceDeviation(r.samples[i].surfaces));
                for (int i = r.samples.Count - Steps(1f, r.dt); i < r.samples.Count; i++)
                    late = Math.Max(late, SurfaceDeviation(r.samples[i].surfaces));
                bool moved = SurfacesMovedWithStick(r);
                // A pedal pulse leaves the aircraft in a gentle banked turn (V2 holds no attitude), so its yaw rate settles
                // to the turn rate rather than to zero: "decays" means below half the pulse peak, not zero.
                bool decays = m.residual < 0.5 * m.peakDuring && late < 0.25 * during;
                Check(ok && moved && decays && m.refused == 0,
                    "S", r.id.PadRight(20) + ": " + what + "; surfaces moved in the commanded sense; after release the response decays ("
                         + m.peakDuring.ToString("F4") + " -> " + m.residual.ToString("F4") + " in the last second) and the surfaces return toward trim ("
                         + during.ToString("F2") + " -> " + late.ToString("F2") + " deg)");
            }
        }

        private void EvaluateComparison()
        {
            sb.AppendLine();
            sb.AppendLine("[C] V1 vs V2 under IDENTICAL scripted inputs (1 s pulse from trim, 6 s centred). Axis: q (pitch), p_s = p cos a + r sin a (roll), r (yaw).");
            sb.AppendLine("    delta: change over the pulse; peak: largest |axis| during the pulse; post: largest |axis| after release; overshoot: opposite-sign swing after");
            sb.AppendLine("    release / peak; settle: time after release until |axis| stays < 10 % of the peak; last 1 s: largest |axis| in the final second.");
            sb.AppendLine("    input       law  delta     peak    post    overshoot settle   last1s  |beta|max alpha range     stab dev ail max rud max refused");
            string[] inputs = { "pitch +", "pitch -", "roll +", "roll -", "yaw +", "yaw -", "pitch+roll" };
            Dictionary<string, PulseMetrics[]> table = new Dictionary<string, PulseMetrics[]>();
            foreach (string what in inputs)
            {
                Run a = runs.Find(x => x.kind == Kind.Pulse && x.id == "PULSE V1 " + what);
                Run b = runs.Find(x => x.kind == Kind.Pulse && x.id == "PULSE V2 " + what);
                if (a == null || b == null || !a.completed || !b.completed)
                {
                    Check(false, "C", what + ": both pulse runs completed");
                    continue;
                }

                PulseMetrics ma = Measure(a), mb = Measure(b);
                table[what] = new[] { ma, mb };
                Row(what, "V1", ma);
                Row(what, "V2", mb);
            }

            if (table.Count != inputs.Length)
                return;

            // What V2 was built to change, stated as facts - each with its cost reported beside it.
            PulseMetrics[] rp = table["roll +"], rn = table["roll -"], pp = table["pitch +"], pn = table["pitch -"], yp = table["yaw +"], yn = table["yaw -"];
            Check(Math.Abs(rp[1].delta) > 2.0 * Math.Abs(rp[0].delta) && Math.Abs(rn[1].delta) > 2.0 * Math.Abs(rn[0].delta)
                  && rp[1].residual < rp[0].residual && rp[1].betaMaxDeg < rp[0].betaMaxDeg && rp[1].refused == 0,
                "C", "ROLL: V2 delivers " + (rp[1].delta / rp[0].delta).ToString("F1") + "x V1's roll rate over the same half-stick second ("
                     + rp[1].delta.ToString("F4") + " vs " + rp[0].delta.ToString("F4") + " rad/s, " + (rp[1].delta / rp[1].commanded * 100).ToString("F0")
                     + " % of the commanded " + rp[1].commanded.ToString("F4") + "), with less sideslip (" + rp[1].betaMaxDeg.ToString("F2") + " vs "
                     + rp[0].betaMaxDeg.ToString("F2") + " deg) and it STOPS rolling after release (last second " + rp[1].residual.ToString("F4") + " vs V1 "
                     + rp[0].residual.ToString("F4") + " rad/s). COST: aileron " + rp[1].ailMax.ToString("F1") + " vs " + rp[0].ailMax.ToString("F1")
                     + " deg and the rudder works too (" + rp[1].rudMax.ToString("F1") + " deg, V1 0)");
            double pr = pp[1].delta / pp[1].commanded;
            Check(pr > 0.7 && pr < 1.3 && Math.Sign(pn[1].delta) < 0 && pp[1].refused == 0 && pn[1].refused == 0,
                "C", "PITCH: V2 is a pitch-rate command - half stick gives " + pp[1].delta.ToString("F4") + " rad/s against " + pp[1].commanded.ToString("F4")
                     + " commanded (" + (pr * 100).ToString("F0") + " %); release swing " + pp[1].overshoot.ToString("F2") + " vs V1 " + pp[0].overshoot.ToString("F2")
                     + ", settle " + Settle(pp[1]) + " vs " + Settle(pp[0]) + " s. COST: less raw pitch rate than V1's direct "
                     + "gearing at half stick (" + pp[1].delta.ToString("F4") + " vs " + pp[0].delta.ToString("F4") + " rad/s) - by design, not a gain");
            Check(Math.Sign(yp[1].delta) > 0 && Math.Sign(yn[1].delta) < 0 && yp[1].betaMaxDeg < yp[0].betaMaxDeg * 1.5 && yp[1].refused == 0,
                "C", "YAW: pedal gives yaw rate and sideslip of the commanded sense under both laws (V2 r " + yp[1].delta.ToString("+0.0000;-0.0000") + " / "
                     + yn[1].delta.ToString("+0.0000;-0.0000") + ", V1 " + yp[0].delta.ToString("+0.0000;-0.0000") + " / " + yn[0].delta.ToString("+0.0000;-0.0000")
                     + " rad/s); V2's roll loop resists the dihedral roll the sideslip makes, so V2 yaws LESS per pedal than V1 - a tradeoff, not an improvement");
        }

        private void Row(string what, string law, PulseMetrics m)
        {
            sb.AppendLine("    " + what.PadRight(11) + " " + law + "  " + m.delta.ToString("+0.0000;-0.0000") + "  " + m.peakDuring.ToString("F4") + "  " + m.postPeak.ToString("F4")
                          + "  " + m.overshoot.ToString("F2").PadLeft(6) + "    " + Settle(m).PadLeft(5)
                          + "s  " + m.residual.ToString("F4") + "  " + m.betaMaxDeg.ToString("F2").PadLeft(6) + "    " + m.alphaMinDeg.ToString("F2") + ".."
                          + m.alphaMaxDeg.ToString("F2") + "  " + m.stabDev.ToString("F2").PadLeft(6) + "  " + m.ailMax.ToString("F2").PadLeft(6) + "  "
                          + m.rudMax.ToString("F2").PadLeft(6) + "  " + m.refused);
        }

        private static string Settle(PulseMetrics m)
        {
            return double.IsInfinity(m.settleS) ? ">6.00" : m.settleS.ToString("F2");
        }

        private void EvaluateSequence()
        {
            sb.AppendLine();
            sb.AppendLine("[Q] V2 scripted sequence (dt 0.02): neutral - pitch+ - neutral - pitch- - neutral - roll+ - neutral - roll- - neutral - yaw+ - neutral - yaw- - neutral - pitch+roll - neutral");
            Run r = runs.Find(x => x.kind == Kind.Sequence && x.mode == MavF15PilotControlMode.AssistedV2 && Math.Abs(x.dt - BaseDt) < 1e-6f);
            Run v1 = runs.Find(x => x.kind == Kind.Sequence && x.mode == MavF15PilotControlMode.DirectV1);
            if (r == null || !r.completed)
            {
                Check(false, "Q", "the V2 sequence completed" + (r != null && r.failure != null ? " - " + r.failure : ""));
                return;
            }

            int[] axis = { 3, 3, 8, 8, 4, 4, 3 };
            int[] sign = { +1, -1, +1, -1, +1, -1, +1 };
            for (int w = 0; w < 7; w++)
            {
                int start = Steps(SequenceWindows[2 * w], r.dt), end = Steps(SequenceWindows[2 * w + 1], r.dt);
                int next = w < 6 ? Steps(SequenceWindows[2 * w + 2], r.dt) : r.steps;
                double delta = Axis(r.samples[end], axis[w]) - Axis(r.samples[start], axis[w]);
                double early = PeakAbs(r, axis[w], end, end + Steps(1f, r.dt));
                double late = PeakAbs(r, axis[w], next - Steps(1f, r.dt), next);
                string extra = w == 6 ? ", p_s " + Axis(r.samples[end], 8).ToString("+0.0000;-0.0000") : "";
                bool combined = w != 6 || Axis(r.samples[end], 8) > 0;
                double v1Late = v1 != null && v1.completed ? PeakAbs(v1, axis[w], next - Steps(1f, r.dt), next) : double.NaN;
                Check(Math.Sign(delta) == sign[w] && combined && late < early,
                    "Q", SequenceNames[w].PadRight(10) + " at " + SequenceWindows[2 * w] + "-" + SequenceWindows[2 * w + 1] + " s: " + (axis[w] == 8 ? "p_s" : StateNames[axis[w]])
                         + " " + delta.ToString("+0.0000;-0.0000") + " rad/s over the pulse" + extra + "; after release the peak decays " + early.ToString("F4") + " -> "
                         + late.ToString("F4") + " rad/s (V1 at the same point: " + v1Late.ToString("F4") + ")");
            }

            double[] env = Envelope(r);
            double[] envV1 = v1 != null && v1.completed ? Envelope(v1) : new double[6];
            Check(r.aeroRefusedSteps == 0 && r.finiteEveryStep && r.surfacesInsideEnvelopeEveryStep,
                "Q", "the whole V2 sequence stays inside the research domain (no aerodynamic refusal) with every surface inside its gameplay envelope: alpha "
                     + (env[0] * RadToDeg).ToString("F2") + ".." + (env[1] * RadToDeg).ToString("F2") + " deg, |beta| <= " + (env[2] * RadToDeg).ToString("F2")
                     + " deg, V " + env[3].ToString("F1") + ".." + env[4].ToString("F1") + " ft/s, |phi| <= " + (env[5] * RadToDeg).ToString("F2")
                     + " deg (V1: alpha " + (envV1[0] * RadToDeg).ToString("F2") + ".." + (envV1[1] * RadToDeg).ToString("F2") + ", |beta| <= "
                     + (envV1[2] * RadToDeg).ToString("F2") + ", |phi| <= " + (envV1[5] * RadToDeg).ToString("F2") + ")");

            // The final 8 s are centred: the aircraft settles back toward level, unaccelerated flight.
            double lastRates = 0;
            for (int i = r.samples.Count - Steps(2f, r.dt); i < r.samples.Count; i++)
                for (int k = 2; k <= 4; k++)
                    lastRates = Math.Max(lastRates, Math.Abs(r.samples[i].x[k]));
            Check(lastRates < 0.02,
                "Q", "centred for the last 8 s: every body rate below " + lastRates.ToString("F4") + " rad/s in the final 2 s (sensible decay, no residual oscillation)");
        }

        private void EvaluateConvergence()
        {
            sb.AppendLine();
            sb.AppendLine("[D] Convergence of the V2 sequence: dt, dt/2 against dt/4 (max over the common 0.02 s grid; rad, rad/s; V ft/s)");
            List<Run> seq = runs.FindAll(x => x.kind == Kind.Sequence && x.mode == MavF15PilotControlMode.AssistedV2);
            if (seq.Count != 3 || !seq[0].completed || !seq[1].completed || !seq[2].completed)
            {
                Check(false, "D", "the three V2 sequence runs completed");
                return;
            }

            double[] e1 = new double[8], e2 = new double[8];
            int n = seq[0].samples.Count;
            for (int i = 0; i < n; i++)
            {
                if (2 * i >= seq[1].samples.Count || 4 * i >= seq[2].samples.Count)
                    break;
                for (int k = 0; k < 8; k++)
                {
                    double fine = seq[2].samples[4 * i].x[k];
                    e1[k] = Math.Max(e1[k], Math.Abs(seq[0].samples[i].x[k] - fine));
                    e2[k] = Math.Max(e2[k], Math.Abs(seq[1].samples[2 * i].x[k] - fine));
                }
            }

            int dominant = 0;
            for (int k = 1; k < 8; k++)
                if (e1[k] > e1[dominant]) dominant = k;
            for (int k = 0; k < 8; k++)
            {
                double ratio = e2[k] > 0 ? e1[k] / e2[k] : double.NaN;
                sb.AppendLine("      " + StateNames[k].PadRight(6) + e1[k].ToString("E2") + "  " + e2[k].ToString("E2") + "  ratio " + ratio.ToString("F2")
                              + (k == dominant ? "  <- dominant" : ""));
            }

            double r = e1[dominant] / e2[dominant];
            Check(e2[dominant] < e1[dominant] && r > 2.0 && r < 4.5,
                "D", "the dominant difference (" + StateNames[dominant] + ") shrinks with dt, ratio " + r.ToString("F2")
                     + " (3 for first order against dt/4): the closed loop (law one step behind the body, surfaces held over the step) converges like the integration error, not like a fault");
        }

        private void EvaluateSwitch()
        {
            sb.AppendLine();
            sb.AppendLine("[W] Human law selection in flight: V1 -> V2 at 2 s (centred), refused at 3.5 s (roll held), V2 -> V1 at 8 s (centred)");
            Run r = runs.Find(x => x.kind == Kind.Switch);
            if (r == null || !r.completed)
            {
                Check(false, "W", "the switch run completed" + (r != null && r.failure != null ? " - " + r.failure : ""));
                return;
            }

            foreach (string line in r.switchLog)
                sb.AppendLine("      " + line);
            Check(r.switchToV2Accepted && r.v2BoundAfterSwitch && r.switchRefusedWhileDeflected && r.refusalChangedNothing
                  && r.switchToV1Accepted && r.v1BoundAfterSwitchBack && r.v1GearingAfterSwitchBack,
                "W", "accepted centred (V2 bound, V1 disabled), refused with the stick deflected (nothing changed), accepted back to V1 (V1 bound, V2 disabled) - "
                     + "and V1 then flies its own direct gearing again (roll +0.5 -> aileron exactly 10 deg, no rudder)");
            Check(r.ownerHeldEveryStep && r.exactlyOneLawEveryStep && r.exactlyOneArmedEveryStep && r.duplicateRejections == 0
                  && r.loadApplicationsAtEnd == r.steps && r.maxSurfaceStepAtSwitchDeg < 1.0 && r.aeroRefusedSteps == 0,
                "W", "through both switches: the pilot-controlled owner held every step, exactly one law enabled and bound, one load application per step ("
                     + r.loadApplicationsAtEnd + "/" + r.steps + "), and no surface stepped more than " + r.maxSurfaceStepAtSwitchDeg.ToString("F3")
                     + " deg across a switch (the residual V2 feedback on the decaying rates; V1 at centred stick is exactly the trim)");
        }

        private void EvaluateSwitchFailClosed()
        {
            sb.AppendLine();
            sb.AppendLine("[X] Fail-closed law switch: V1 at the trim; refused with the stick deflected, the command unavailable, the source missing and the source disabled; V1 -> V2 at 3 s and V2 -> V1 at 6 s centred");
            Run r = runs.Find(x => x.kind == Kind.SwitchFailClosed);
            if (r == null || !r.completed)
            {
                Check(false, "X", "the fail-closed switch run completed" + (r != null && r.failure != null ? " - " + r.failure : ""));
                return;
            }

            foreach (string line in r.switchLog)
                sb.AppendLine("      " + line);
            Check(r.trimDriftBeforeAttempts <= NeutralTolerance,
                "X", "V1 holds the trim before any attempt: every state within " + r.trimDriftBeforeAttempts.ToString("E1") + " of the trim over the first second");
            Check(r.refusalsAttempted == 4 && r.refusalsOk == 4,
                "X", r.refusalsOk + " of " + r.refusalsAttempted + " attempts refused with their own reason, each leaving the mode, the bound law, both laws' enabled states, "
                     + "the actuator request, the surfaces, both laws' requests, the owner, its reason and the arming unchanged");
            Check(r.switchToV2Accepted && r.v2BoundAfterSwitch && r.switchToV1Accepted && r.v1BoundAfterSwitchBack,
                "X", "with the source restored and the controls centred: V1 -> V2 accepted (V2 bound, V1 disabled), V2 -> V1 accepted (V1 bound, V2 disabled)");
            Check(r.ownerHeldEveryStep && r.exactlyOneLawEveryStep && r.exactlyOneArmedEveryStep && r.duplicateRejections == 0
                  && r.loadApplicationsAtEnd == r.steps && r.finiteEveryStep && r.aeroRefusedSteps == 0 && r.maxSurfaceStepAtSwitchDeg < 1.0,
                "X", "through the refusals and both switches: no FAULT (the pilot-controlled owner held every step), exactly one law enabled and bound every step, "
                     + "one load application per step (" + r.loadApplicationsAtEnd + "/" + r.steps + ", no gap, no duplicate), finite, and no surface stepped more than "
                     + r.maxSurfaceStepAtSwitchDeg.ToString("F3") + " deg across a switch");
        }

        private void EvaluateFailClosed()
        {
            sb.AppendLine();
            sb.AppendLine("[N] Fail-closed ownership under V2");
            Run loss = runs.Find(x => x.kind == Kind.OwnershipLoss);
            Run second = runs.Find(x => x.kind == Kind.SecondRequester);
            if (loss == null || !loss.completed || second == null || !second.completed)
            {
                Check(false, "N", "both fail-closed runs completed");
                return;
            }

            Check(loss.faultedNextStep && loss.disarmedAfterFault && loss.loadsStoppedAfterFault,
                "N", "the pilot profile removed at step " + loss.lossStep + ": the owner FAULTS, the body is disarmed and no further load is applied ("
                     + loss.faultReason + ")");
            Check(second.faultedNextStep && second.disarmedAfterFault && second.loadsStoppedAfterFault,
                "N", "the V1 law enabled next to the bound V2 law at step " + second.lossStep + " (a second surface requester): the owner FAULTS, the body is disarmed, "
                     + "no further load (" + second.faultReason + ")");
        }

        // ================================================================== helpers

        private double SurfaceDeviation(MavF15SurfaceState s)
        {
            return Math.Max(Math.Max(Math.Abs(s.symmetricStabilatorDeg - trimBias), Math.Abs(s.aileronDeg)),
                Math.Max(Math.Abs(s.differentialStabilatorDeg), Math.Abs(s.rudderDeg)));
        }

        private bool SurfacesMovedWithStick(Run r)
        {
            int i = Math.Min(r.pulseStartStep + 1, r.samples.Count - 1);
            MavF15SurfaceState s = r.samples[i].surfaces;
            bool pitchOk = r.pitch == 0f || Math.Sign(trimBias - s.symmetricStabilatorDeg) == Math.Sign(r.pitch);
            bool rollOk = r.roll == 0f || Math.Sign(s.aileronDeg) == Math.Sign(r.roll);
            bool yawOk = r.yaw == 0f || Math.Sign(s.rudderDeg) == -Math.Sign(r.yaw);
            return pitchOk && rollOk && yawOk;
        }

        private double[] Drift(Run r)
        {
            double[] d = new double[8];
            foreach (Sample s in r.samples)
            {
                for (int k = 0; k < 8; k++)
                {
                    double scale = k == 7 ? equilibrium[7] : 1.0;
                    d[k] = Math.Max(d[k], Math.Abs(s.x[k] - equilibrium[k]) / scale);
                }
            }

            return d;
        }

        private static double Max(double[] d)
        {
            double m = 0;
            foreach (double v in d)
                m = Math.Max(m, v);
            return m;
        }

        private static double[] Delta(Run r, int from, int to)
        {
            double[] d = new double[8];
            if (to >= r.samples.Count || from >= r.samples.Count)
                return d;
            for (int k = 0; k < 8; k++)
                d[k] = r.samples[to].x[k] - r.samples[from].x[k];
            return d;
        }

        private static double PeakAbs(Run r, int axis, int from, int to)
        {
            double peak = 0;
            for (int i = Math.Max(0, from); i < Math.Min(to, r.samples.Count); i++)
                peak = Math.Max(peak, Math.Abs(Axis(r.samples[i], axis)));
            return peak;
        }

        private static double[] Envelope(Run r)
        {
            double aMin = double.MaxValue, aMax = double.MinValue, b = 0, vMin = double.MaxValue, vMax = double.MinValue, phi = 0;
            foreach (Sample s in r.samples)
            {
                aMin = Math.Min(aMin, s.x[0]);
                aMax = Math.Max(aMax, s.x[0]);
                b = Math.Max(b, Math.Abs(s.x[1]));
                vMin = Math.Min(vMin, s.x[7]);
                vMax = Math.Max(vMax, s.x[7]);
                phi = Math.Max(phi, Math.Abs(s.x[6]));
            }

            return new[] { aMin, aMax, b, vMin, vMax, phi };
        }

        private static string DescribeStates(double[] d)
        {
            StringBuilder s = new StringBuilder();
            for (int k = 0; k < 8; k++)
                s.Append(StateNames[k]).Append(' ').Append(d[k].ToString("E1")).Append(k < 7 ? ", " : "");
            return s.ToString();
        }

        private static bool IsFinite(Vector3 v)
        {
            return !float.IsNaN(v.x) && !float.IsInfinity(v.x) && !float.IsNaN(v.y) && !float.IsInfinity(v.y)
                   && !float.IsNaN(v.z) && !float.IsInfinity(v.z);
        }

        private void Check(bool condition, string tag, string label)
        {
            if (condition)
                Passed++;
            else
                Failed++;
            sb.Append(condition ? "  PASS  " : "  FAIL  ").Append(tag).Append("  ").AppendLine(label);
        }
    }
}
