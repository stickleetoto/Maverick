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
    /// REALISTIC MOUSE INSTRUCTOR V2 - deterministic Play Mode flight test through real Unity physics.
    ///
    /// Flies the pilot-controlled F-15 research rig with the opt-in mouse-aim source (Assisted V2 law, unchanged) from the
    /// validated trim: neutral aim hold; small / large turns; a 180-deg reversal; climb and descent; a deterministic moving
    /// target; a sustained climb request into low speed (degraded authority); release to neutral; the same mouse trace at
    /// 25 / 50 / 100 rendered fps against the fixed 0.02-s step; and the two input-loss policies.
    ///
    /// Pass criteria: ownership, one law, one load application per step, no pose write by the input path, finiteness,
    /// bounded and rate-limited commands, surfaces in the gameplay envelope, the sign of the response, the TREND of the
    /// angular error, no energy appearing from nowhere, and the visibility of saturation / soft caps. No physical
    /// performance target is invented: turn rates, times and speeds are REPORTED, not scored.
    ///
    /// DETERMINISM: Time.captureFramerate pinned per run (50 unless the run says otherwise), fixed dt 0.02, a fresh rig per
    /// run built one physics step before it starts, events on exact step indices. Runs at -600, before the rig (-500), the
    /// ownership authority (-400), the source (-350), the law (-300), the actuator (-200) and the body (-100).
    /// RESEARCH MODEL + MAVERICK CONTROL LAW + MAVERICK GUIDANCE. NOT the F-15 FCS. NOT War Thunder's instructor.
    /// </summary>
    [DefaultExecutionOrder(-600)]
    public sealed class MavMouseAimInstructorV2FlightValidationRunner : MonoBehaviour
    {
        public static bool Finished;
        public static int Passed;
        public static int Failed;
        public static string Report = "";

        public const float Dt = 0.02f;
        public const int DefaultCaptureFramerate = 50;

        private enum Kind
        {
            NeutralHold,
            FixedTarget,
            MovingTarget,
            LowSpeed,
            Release,
            MouseTrace,
            LossNeutral,
            LossDrop
        }

        private sealed class Run
        {
            public string id;
            public Kind kind;
            public int steps;
            public float targetAzDeg, targetElDeg;
            public int captureFramerate = DefaultCaptureFramerate;
            public int eventStep = -1;

            public bool started;
            public string failure;
            public bool completed;
            public float az0, el0;
            public float mouseDegPerUnit;

            // mechanics
            public bool ownerHeld = true, oneArmed = true, oneLaw = true, finite = true, surfacesInEnvelope = true, noPoseWrite = true;
            public int loadsAtEnd, loadsAtEvent = -1, duplicateRejections, aeroRefusedSteps, withheldAtEnd;
            public double maxResidualM, maxVelocityStepMps, maxCommandStep, maxAbsYaw;
            public MavAimGuidanceLimiter limiterUnion;
            public int pullCapSteps, pitchSaturatedSteps, rollSaturatedSteps, pushLimitedSteps, lawEnvelopeLimitedSteps;
            public float elevationStart, elevationMax = float.MinValue, elevationMin = float.MaxValue;
            public int firstPullCapStep = -1;

            // flight
            public readonly List<float> errorDeg = new List<float>();
            public readonly List<float> bankDeg = new List<float>();
            public readonly List<MavPilotCommand> commands = new List<MavPilotCommand>();
            public readonly List<float> aimAzDeg = new List<float>();
            public readonly List<Vector3> positions = new List<Vector3>();
            public float tasMin = float.MaxValue, tasMax, alphaMin = float.MaxValue, alphaMax = float.MinValue, betaAbsMax, nzMin = float.MaxValue, nzMax = float.MinValue;
            public float energyStart, energyEnd, altitudeStart, altitudeEnd;
            public double rateMagAtEvent, rateMagLast2s;
            public int stepsToNeutralAfterEvent = -1;
            public MavFlightControlLawBase.MavCommandResolution resolutionAtEnd;
            public MavFlightPhysicsOwner ownerAtEnd;
        }

        private readonly List<Run> runs = new List<Run>();
        private readonly StringBuilder sb = new StringBuilder(64 * 1024);
        private int runIndex, phase, stepsDone;
        private bool completeDone;
        private float savedFixedDeltaTime;
        private int savedCaptureFramerate;
        private MavF15PilotControlledRig rig;
        private MavMouseAimPilotCommandSource source;
        private MavF15GameplayControlAuthority authority;
        private Vector3 lastPos, lastVel;
        private bool havePrev;
        private MavPilotCommand lastCmd;
        private float traceStartTime;
        private double tracePrevT;

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
            Time.captureFramerate = DefaultCaptureFramerate;
            Time.fixedDeltaTime = Dt;
            authority = MavF15GameplayControlAuthority.V1();

            Add("NEUTRAL AIM HOLD", Kind.NeutralHold, 20f, 0f, 0f);
            Add("SMALL TURN 15 R", Kind.FixedTarget, 25f, 15f, 0f);
            Add("SMALL TURN 15 L", Kind.FixedTarget, 25f, -15f, 0f);
            Add("LARGE TURN 90 R", Kind.FixedTarget, 40f, 90f, 0f);
            Add("REVERSAL 180", Kind.FixedTarget, 60f, 180f, 0f);
            Add("CLIMB 8", Kind.FixedTarget, 20f, 0f, 8f);
            Add("DESCENT 8", Kind.FixedTarget, 20f, 0f, -8f);
            Add("MOVING TARGET 2 deg/s", Kind.MovingTarget, 40f, 0f, 0f);
            Add("LOW-SPEED CLIMB 40 (degraded)", Kind.LowSpeed, 40f, 0f, 40f);
            Run release = Add("RELEASE TO NEUTRAL", Kind.Release, 16f, 30f, 0f);
            release.eventStep = Steps(8f);
            foreach (int fps in new[] { 25, 50, 100 })
            {
                Run trace = Add("MOUSE TRACE @" + fps + " fps", Kind.MouseTrace, 16f, 0f, 0f);
                trace.captureFramerate = fps;
            }

            Run lossN = Add("INPUT LOSS (default NeutralStick)", Kind.LossNeutral, 8f, 25f, 0f);
            lossN.eventStep = Steps(3f);
            Run lossD = Add("INPUT LOSS (DropCommandSignal)", Kind.LossDrop, 6f, 25f, 0f);
            lossD.eventStep = Steps(3f);
        }

        private Run Add(string id, Kind kind, float seconds, float az, float el)
        {
            Run r = new Run { id = id, kind = kind, steps = Steps(seconds), targetAzDeg = az, targetElDeg = el };
            runs.Add(r);
            return r;
        }

        private static int Steps(float seconds)
        {
            return Mathf.RoundToInt(seconds / Dt);
        }

        // ================================================================== mouse trace (frame input)

        public static double TraceX(double t)
        {
            // A smooth 25-unit right sweep over 1-5 s, then back 17 units over 9-13 s (aim +30 deg, then +9.6 deg at 1.2 deg/unit).
            return 25.0 * Ramp(t, 1.0, 5.0) - 17.0 * Ramp(t, 9.0, 13.0);
        }

        public static double TraceY(double t)
        {
            return 3.0 * Ramp(t, 2.0, 4.0) - 3.0 * Ramp(t, 10.0, 12.0);
        }

        private static double Ramp(double t, double a, double b)
        {
            if (t <= a) return 0.0;
            if (t >= b) return 1.0;
            double u = (t - a) / (b - a);
            return 0.5 - 0.5 * Math.Cos(Math.PI * u);
        }

        /// <summary>Largest |dTraceX/dt| (units/s): 25 * pi / (2 * 4).</summary>
        public const double TraceSpeedMax = 25.0 * Math.PI / 8.0;

        private void Update()
        {
            if (completeDone || runIndex >= runs.Count || phase != 2 || source == null)
                return;
            Run run = runs[runIndex];
            if (run.kind != Kind.MouseTrace)
                return;

            double t = Math.Max(0.0, Time.time - traceStartTime);
            source.ProcessFrameInput((float)(TraceX(t) - TraceX(tracePrevT)), (float)(TraceY(t) - TraceY(tracePrevT)), false, true);
            tracePrevT = t;
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
                    Time.fixedDeltaTime = Dt;
                    Time.captureFramerate = run.captureFramerate;
                    rig = MavF15PilotControlledRig.Create("mi2-" + run.id, MavF15PilotCommandSourceKind.MouseAim, false, MavF15PilotControlMode.AssistedV2);
                    source = (MavMouseAimPilotCommandSource)rig.commandSource;
                    source.readDeviceInput = false;
                    source.requireAimCamera = false;
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

        private void Begin(Run run)
        {
            stepsDone = 0;
            havePrev = false;
            run.started = rig.StartFromTrim();
            if (!run.started)
            {
                run.failure = "start refused: " + rig.debugStartStatus;
                Finish(run);
                return;
            }

            Vector3 v0 = rig.body3d.linearVelocity;
            MavMouseAimAngles a0;
            MavMouseAimInput.TryAnglesFromDirection(v0, 89.9f, out a0);
            run.az0 = a0.azimuthDeg;
            run.el0 = a0.elevationDeg;
            if (run.kind == Kind.FixedTarget || run.kind == Kind.LowSpeed || run.kind == Kind.Release || run.kind == Kind.LossNeutral || run.kind == Kind.LossDrop)
                source.SetScriptedTargetDirection(MavMouseAimInput.DirectionFromAngles(run.az0 + run.targetAzDeg, run.el0 + run.targetElDeg));

            run.mouseDegPerUnit = source.degreesPerMouseUnit;
            traceStartTime = Time.time;
            tracePrevT = 0.0;
            lastCmd = MavPilotCommand.Neutral;
            run.energyStart = Energy(rig.body3d);
            run.altitudeStart = rig.body3d.position.y;
            phase = 2;
        }

        private void Continue(Run run)
        {
            stepsDone++;
            MavSixDoFBody body = rig.body;
            Rigidbody rb = rig.body3d;
            Vector3 pos = rb.position, vel = rb.linearVelocity;

            // No pose write between the previous step's start and the body's own state build: what the body published last
            // step is exactly the pose this runner saw at the start of that step.
            if (havePrev)
            {
                MavFlightState published = body.debugState;
                if ((published.worldPositionM - lastPos).magnitude > 1e-4f || (published.worldVelocityMps - lastVel).magnitude > 1e-5f)
                    run.noPoseWrite = false;
                run.maxResidualM = Math.Max(run.maxResidualM, (pos - lastPos - vel * Dt).magnitude);
                run.maxVelocityStepMps = Math.Max(run.maxVelocityStepMps, (vel - lastVel).magnitude);
            }

            lastPos = pos;
            lastVel = vel;
            havePrev = true;

            bool afterDrop = run.kind == Kind.LossDrop && run.eventStep >= 0 && stepsDone > run.eventStep;
            if (!afterDrop)
            {
                if (rig.ownership == null || rig.ownership.owner != MavFlightPhysicsOwner.F15PilotControlledResearch || !body.ArmedForLiveFlight)
                    run.ownerHeld = false;
                if (CountArmedBodies() != 1)
                    run.oneArmed = false;
            }

            int enabled = (rig.law.enabled ? 1 : 0) + (rig.lawV2 != null && rig.lawV2.enabled ? 1 : 0);
            if (enabled != 1 || body.controlLaw != rig.lawV2)
                run.oneLaw = false;

            MavF15PilotControlledAeroModel aero = body.aerodynamicModel as MavF15PilotControlledAeroModel;
            if (aero != null && aero.debugRefused)
                run.aeroRefusedSteps++;
            MavF15SurfaceState surfaces = rig.actuator.ActualF15SurfaceState.channels;
            if (!InsideEnvelope(surfaces))
                run.surfacesInEnvelope = false;

            MavFlightState s = body.debugState;
            MavPilotCommand cmd = source.debugCommand;
            MavMouseAimGuidanceOutput g = source.debugGuidance;
            bool finite = IsFinite(pos) && IsFinite(vel) && surfaces.IsFinite() && MavMouseAimGuidance.IsFinite(cmd.pitch)
                          && MavMouseAimGuidance.IsFinite(cmd.roll) && MavMouseAimGuidance.IsFinite(cmd.yaw) && body.debugLoadSet.IsFinite();
            if (!finite)
                run.finite = false;

            // The source's own command is rate-bounded while it produces one; after a deliberate signal drop it publishes nothing usable.
            if (source.HasCommandSignal)
                run.maxCommandStep = Math.Max(run.maxCommandStep, Math.Max(Math.Abs(cmd.pitch - lastCmd.pitch), Math.Max(Math.Abs(cmd.roll - lastCmd.roll), Math.Abs(cmd.yaw - lastCmd.yaw))));
            if (rig.lawV2 != null && rig.lawV2.debugEnvelopeLimited)
                run.lawEnvelopeLimitedSteps++;
            if (vel.sqrMagnitude > 1f)
            {
                float el = Mathf.Asin(Mathf.Clamp(vel.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
                if (stepsDone == 1) run.elevationStart = el;
                run.elevationMax = Mathf.Max(run.elevationMax, el);
                run.elevationMin = Mathf.Min(run.elevationMin, el);
            }
            run.maxAbsYaw = Math.Max(run.maxAbsYaw, Math.Abs(cmd.yaw));
            lastCmd = cmd;
            run.limiterUnion |= g.limiters;
            if ((g.limiters & MavAimGuidanceLimiter.LowSpeedPullCap) != 0)
            {
                run.pullCapSteps++;
                if (run.firstPullCapStep < 0)
                    run.firstPullCapStep = stepsDone;
            }

            if ((g.limiters & MavAimGuidanceLimiter.PitchStickSaturated) != 0) run.pitchSaturatedSteps++;
            if ((g.limiters & MavAimGuidanceLimiter.RollStickSaturated) != 0) run.rollSaturatedSteps++;
            if ((g.limiters & MavAimGuidanceLimiter.PushLimited) != 0) run.pushLimitedSteps++;

            Vector3 target = TargetNow(run);
            run.errorDeg.Add(vel.sqrMagnitude > 1f ? Vector3.Angle(vel, target) : 0f);
            run.bankDeg.Add(s.attitude.valid ? s.attitude.BankAngleDeg : 0f);
            run.commands.Add(cmd);
            run.aimAzDeg.Add(source.debugAim.azimuthDeg);
            run.positions.Add(pos);
            float tas = vel.magnitude;
            run.tasMin = Mathf.Min(run.tasMin, tas);
            run.tasMax = Mathf.Max(run.tasMax, tas);
            run.alphaMin = Mathf.Min(run.alphaMin, s.AlphaDeg);
            run.alphaMax = Mathf.Max(run.alphaMax, s.AlphaDeg);
            run.betaAbsMax = Mathf.Max(run.betaAbsMax, Mathf.Abs(s.BetaDeg));
            if (s.specificForceValid)
            {
                run.nzMin = Mathf.Min(run.nzMin, s.LoadFactorNz);
                run.nzMax = Mathf.Max(run.nzMax, s.LoadFactorNz);
            }

            double rateMag = s.aeroBodyRatesRadSec.magnitude;
            if (stepsDone >= run.steps - Steps(2f))
                run.rateMagLast2s = Math.Max(run.rateMagLast2s, rateMag);

            // events
            if (run.kind == Kind.MovingTarget)
                source.SetScriptedTargetDirection(target);
            if (stepsDone == run.eventStep)
            {
                run.loadsAtEvent = body.debugLoadApplications;
                run.rateMagAtEvent = rateMag;
                if (run.kind == Kind.Release)
                    source.SetHandsOff(true);
                else if (run.kind == Kind.LossNeutral)
                    source.requireAimCamera = true;
                else if (run.kind == Kind.LossDrop)
                {
                    source.inputLossPolicy = MavMouseAimInputLossPolicy.DropCommandSignal;
                    source.requireAimCamera = true;
                }
            }

            if (run.eventStep >= 0 && stepsDone > run.eventStep && run.stepsToNeutralAfterEvent < 0 && cmd.IsNeutral(1e-3f))
                run.stepsToNeutralAfterEvent = stepsDone - run.eventStep;

            if (stepsDone >= run.steps)
                Finish(run);
        }

        private Vector3 TargetNow(Run run)
        {
            float t = stepsDone * Dt;
            switch (run.kind)
            {
                case Kind.MovingTarget:
                    return MavMouseAimInput.DirectionFromAngles(run.az0 + 2f * Mathf.Max(0f, t - 2f), run.el0);
                case Kind.NeutralHold:
                case Kind.MouseTrace:
                    return source.AimDirectionWorld;
                default:
                    return MavMouseAimInput.DirectionFromAngles(run.az0 + run.targetAzDeg, run.el0 + run.targetElDeg);
            }
        }

        private void Finish(Run run)
        {
            if (rig != null)
            {
                run.loadsAtEnd = rig.body.debugLoadApplications;
                run.withheldAtEnd = rig.body.debugRejectedNotLiveReadyApplications;
                run.duplicateRejections = rig.body.debugRejectedDuplicateApplications;
                run.resolutionAtEnd = rig.lawV2 != null ? rig.lawV2.debugCommandResolution : MavFlightControlLawBase.MavCommandResolution.NoSource;
                run.ownerAtEnd = rig.ownership != null ? rig.ownership.owner : MavFlightPhysicsOwner.Legacy;
                run.energyEnd = Energy(rig.body3d);
                run.altitudeEnd = rig.body3d.position.y;
            }

            run.completed = run.failure == null && stepsDone >= run.steps;
            if (rig != null && rig.ownership != null && rig.ownership.owner != MavFlightPhysicsOwner.Fault)
                rig.ownership.ReturnToLegacy("mouse instructor v2 validation run complete");
            if (rig != null)
                Object.DestroyImmediate(rig.gameObject);
            rig = null;
            source = null;
            runIndex++;
            phase = 0;
        }

        // ================================================================== evaluation

        private void Complete()
        {
            Time.fixedDeltaTime = savedFixedDeltaTime;
            Time.captureFramerate = savedCaptureFramerate;

            MavMouseAimGuidanceConfig cfg = MavMouseAimGuidanceConfig.Default();
            sb.AppendLine("REALISTIC MOUSE INSTRUCTOR V2 - PLAY MODE FLIGHT TEST (" + MavF15PilotControlledIdentity.ConfigurationId + ", Assisted V2 law unchanged)");
            sb.AppendLine("================================================================================================");
            sb.AppendLine(MavMouseAimProvenance.Identity);
            sb.AppendLine("Research aerodynamics (frozen) + fixed research thrust (THROTTLE INACTIVE) + Maverick V2 law + Maverick aim guidance. Scripted aim targets / mouse traces.");
            sb.AppendLine("guidance: K " + cfg.flightPathRateGainPerSec + "/s, bank tau " + cfg.bankTimeConstantSeconds + " s, path-rate cap = V2 full-stick pitch rate "
                          + MavF15PilotControlGainsV2.V2().commandedPitchRateAtFullStickDegSec + " deg/s, command rate " + cfg.commandRatePerSec + "/s; dt " + Dt);

            sb.AppendLine();
            sb.AppendLine("[RM] Runtime mechanics (owner, one armed body, one law, one load per step, no pose write by the input path, finite, bounded rate-limited commands, surfaces in envelope)");
            double maxCmdStep = cfg.commandRatePerSec * Dt + 1e-5;
            foreach (Run r in runs)
            {
                bool drop = r.kind == Kind.LossDrop;
                int expectedLoads = drop ? r.loadsAtEvent : r.steps;
                bool loadsOk = drop ? r.loadsAtEnd == r.loadsAtEvent : r.loadsAtEnd == expectedLoads;
                bool ok = r.completed && r.failure == null && r.ownerHeld && r.oneArmed && r.oneLaw && r.finite && r.noPoseWrite && r.duplicateRejections == 0
                          && r.surfacesInEnvelope && r.maxCommandStep <= maxCmdStep && r.maxAbsYaw <= cfg.yawStickLimit + 1e-6
                          && r.maxResidualM < 5e-3 && r.maxVelocityStepMps < 2.0 && loadsOk && (r.kind == Kind.LowSpeed || r.aeroRefusedSteps == 0);
                Check(ok, "RM", r.id.PadRight(34) + " steps " + r.steps + ", loads " + r.loadsAtEnd + (drop ? " (frozen at " + r.loadsAtEvent + ")" : "")
                    + ", dup " + r.duplicateRejections + ", owner " + r.ownerHeld + ", 1 armed " + r.oneArmed + ", 1 law " + r.oneLaw + ", no pose write " + r.noPoseWrite
                    + ", finite " + r.finite + ", max cmd step " + r.maxCommandStep.ToString("F3") + ", max |yaw| " + r.maxAbsYaw.ToString("F3")
                    + ", surfaces in envelope " + r.surfacesInEnvelope + ", |dx-v dt| " + r.maxResidualM.ToString("E1") + " m, max |dv| " + r.maxVelocityStepMps.ToString("F3")
                    + " m/s/step, aero refused " + r.aeroRefusedSteps + (r.failure == null ? "" : " - " + r.failure));
            }

            sb.AppendLine();
            sb.AppendLine("[RF] Flight record (reported, not scored): error deg at 0/25/50/75/100 %, bank range, TAS, alpha, |beta|, Nz, energy height change, limiters");
            foreach (Run r in runs)
                sb.AppendLine("  " + r.id.PadRight(34) + " err " + Trend(r.errorDeg) + " | bank " + Min(r.bankDeg).ToString("F0") + ".." + Max(r.bankDeg).ToString("F0")
                              + " | TAS " + r.tasMin.ToString("F1") + ".." + r.tasMax.ToString("F1") + " m/s | alpha " + r.alphaMin.ToString("F1") + ".." + r.alphaMax.ToString("F1")
                              + " | beta " + r.betaAbsMax.ToString("F2") + " | Nz " + (r.nzMax >= r.nzMin ? r.nzMin.ToString("F2") + ".." + r.nzMax.ToString("F2") : "n/a")
                              + " | dE " + (r.energyEnd - r.energyStart).ToString("F0") + " m, dh " + (r.altitudeEnd - r.altitudeStart).ToString("F0") + " m | limiters "
                              + r.limiterUnion + " (pitch sat " + r.pitchSaturatedSteps + ", roll sat " + r.rollSaturatedSteps + ", push lim " + r.pushLimitedSteps
                              + ", low-speed cap " + r.pullCapSteps + ", V2 gameplay-envelope limited " + r.lawEnvelopeLimitedSteps + ")"
                              + " | path elevation " + r.elevationStart.ToString("F1") + " (" + r.elevationMin.ToString("F1") + ".." + r.elevationMax.ToString("F1") + ")");

            sb.AppendLine();
            sb.AppendLine("[RT] Tracking behaviour (sign of response, error trend, banked turns - no performance target)");
            foreach (Run r in runs)
                EvaluateTracking(r);

            EvaluateTrace();
            EvaluateLoss();

            sb.AppendLine();
            sb.Append("RESULT: ").Append(Failed == 0 ? "PASS" : "FAIL").Append("  passed=").Append(Passed).Append(" failed=").Append(Failed);
            Report = sb.ToString();
            Finished = true;
        }

        private void EvaluateTracking(Run r)
        {
            if (r.errorDeg.Count == 0)
                return;
            float e0 = r.errorDeg[0], eEnd = r.errorDeg[r.errorDeg.Count - 1], eMin = Min(r.errorDeg);
            int firstWindow = Math.Min(r.bankDeg.Count, Steps(6f));
            float earlyBankMax = Max(r.bankDeg.GetRange(0, firstWindow)), earlyBankMin = Min(r.bankDeg.GetRange(0, firstWindow));
            switch (r.kind)
            {
                case Kind.NeutralHold:
                    Check(Max(r.errorDeg) < 1f && Mathf.Max(Mathf.Abs(Min(r.bankDeg)), Mathf.Abs(Max(r.bankDeg))) < 1f,
                        "RT", r.id + ": untouched mouse holds the trimmed flight path: max error " + Max(r.errorDeg).ToString("F3") + " deg, |bank| <= "
                        + Mathf.Max(Mathf.Abs(Min(r.bankDeg)), Mathf.Abs(Max(r.bankDeg))).ToString("F3") + " deg over 20 s");
                    break;
                case Kind.FixedTarget when r.targetElDeg > 0f:
                    // A sustained climb above the trim path is energy limited at the fixed research thrust (the trim itself is a
                    // shallow fixed-thrust descent): scored on the response sign, the approach and the energy book, not on capture.
                    Check(r.elevationMax - r.elevationStart > 1f && r.energyEnd < r.energyStart,
                        "RT", r.id + ": the flight path rises " + (r.elevationMax - r.elevationStart).ToString("F1") + " deg (" + r.elevationStart.ToString("F1") + " -> max "
                        + r.elevationMax.ToString("F1") + "), error " + e0.ToString("F1") + " -> min " + eMin.ToString("F1") + " -> end " + eEnd.ToString("F1")
                        + " deg; energy height " + r.energyStart.ToString("F0") + " -> " + r.energyEnd.ToString("F0") + " m - the climb is paid for with speed, never created");
                    break;
                case Kind.FixedTarget when r.targetElDeg < 0f:
                    // V2 has no flight-path hold: as the dive builds speed its speed stability brings the path back toward
                    // the trim path, and the guidance will not push beyond maxPushStick. Scored on the approach; the hold is reported.
                    Check(r.elevationMin < r.elevationStart - 1f && eMin < 0.5f * e0 && r.errorDeg.TrueForAll(MavMouseAimGuidance.IsFinite),
                        "RT", r.id + ": the flight path drops " + (r.elevationStart - r.elevationMin).ToString("F1") + " deg (" + r.elevationStart.ToString("F1") + " -> min "
                        + r.elevationMin.ToString("F1") + "), error " + e0.ToString("F1") + " -> min " + eMin.ToString("F1") + "; NOT HELD: ends " + eEnd.ToString("F1")
                        + " deg as the speed builds (TAS max " + r.tasMax.ToString("F1") + " m/s) - known limitation, see report");
                    break;
                case Kind.FixedTarget:
                    bool lateral = Mathf.Abs(r.targetAzDeg) > 1f;
                    bool rightTurn = r.targetAzDeg > 0f;
                    bool sign = !lateral || (rightTurn ? earlyBankMax > 5f : earlyBankMin < -5f);
                    bool trend = r.targetAzDeg >= 179f ? eMin < 0.5f * e0 : eEnd < 0.5f * e0;
                    Check(sign && trend && r.errorDeg.TrueForAll(MavMouseAimGuidance.IsFinite),
                        "RT", r.id + ": " + (lateral ? "banks " + (rightTurn ? "RIGHT (max " + earlyBankMax.ToString("F0") : "LEFT (min " + earlyBankMin.ToString("F0")) + " deg in 6 s), " : "")
                        + "error " + e0.ToString("F1") + " -> " + eEnd.ToString("F1") + " deg (min " + eMin.ToString("F1") + ")"
                        + (r.targetAzDeg >= 179f ? " - reversal scored on its minimum: the fixed-thrust research aircraft is energy limited" : ""));
                    break;
                case Kind.MovingTarget:
                {
                    List<float> last = r.errorDeg.GetRange(r.errorDeg.Count - Steps(10f), Steps(10f));
                    Check(Max(last) - Min(last) < 3f && Max(last) < 20f,
                        "RT", r.id + ": steady tracking lag over the last 10 s " + Min(last).ToString("F2") + ".." + Max(last).ToString("F2")
                        + " deg (bounded, not growing); bank " + Min(r.bankDeg.GetRange(r.bankDeg.Count - Steps(10f), Steps(10f))).ToString("F1") + ".."
                        + Max(r.bankDeg.GetRange(r.bankDeg.Count - Steps(10f), Steps(10f))).ToString("F1") + " deg");
                    break;
                }
                case Kind.LowSpeed:
                    Check(r.firstPullCapStep > 0 && r.energyEnd < r.energyStart && eEnd > 5f,
                        "RT", r.id + ": no forced convergence: error " + e0.ToString("F1") + " -> " + eEnd.ToString("F1") + " deg; energy height "
                        + r.energyStart.ToString("F0") + " -> " + r.energyEnd.ToString("F0") + " m (fixed thrust: nothing is added); low-speed pull cap from t "
                        + (r.firstPullCapStep * Dt).ToString("F1") + " s (" + r.pullCapSteps + " steps), TAS min " + r.tasMin.ToString("F1")
                        + " m/s (research floor 66.60), aero refused " + r.aeroRefusedSteps + " steps - VISIBLE, not hidden");
                    break;
                case Kind.Release:
                    Check(r.stepsToNeutralAfterEvent > 0 && r.stepsToNeutralAfterEvent * Dt <= 1f && r.finite,
                        "RT", r.id + ": hands-off at 8 s -> neutral command after " + (r.stepsToNeutralAfterEvent * Dt).ToString("F2") + " s (rate-limited ramp, no step); body-rate magnitude "
                        + r.rateMagAtEvent.ToString("F4") + " rad/s at release, max " + r.rateMagLast2s.ToString("F4") + " in the last 2 s (reported: V2 has no attitude hold, the aircraft keeps its bank)");
                    break;
            }
        }

        private void EvaluateTrace()
        {
            sb.AppendLine();
            sb.AppendLine("[RI] Mouse trace at 25 / 50 / 100 rendered fps vs the fixed 0.02-s step (production ProcessFrameInput in Update, snapshot in FixedUpdate)");
            List<Run> traces = runs.FindAll(r => r.kind == Kind.MouseTrace);
            bool allBound = true;
            float worstLag = 0f;
            foreach (Run r in traces)
            {
                float k = r.mouseDegPerUnit;
                float frame = 1f / r.captureFramerate;
                float bound = (float)(TraceSpeedMax * k * frame) + 1e-3f;
                for (int i = 0; i < r.aimAzDeg.Count; i++)
                {
                    float ideal = r.az0 + (float)(TraceX((i + 1) * Dt) * k);
                    float lag = Mathf.Abs(Mathf.DeltaAngle(ideal, r.aimAzDeg[i]));
                    worstLag = Mathf.Max(worstLag, lag);
                    allBound &= lag <= bound + (float)(TraceSpeedMax * k * Dt);
                }
            }

            Check(traces.Count == 3 && traces.TrueForAll(r => r.completed) && allBound,
                "RI", "every physics-step aim snapshot is within one render frame (+ one step) of the continuous trace at every fps (worst " + worstLag.ToString("F3") + " deg)");

            if (traces.Count == 3)
            {
                float a25 = traces[0].aimAzDeg[traces[0].aimAzDeg.Count - 1], a50 = traces[1].aimAzDeg[traces[1].aimAzDeg.Count - 1], a100 = traces[2].aimAzDeg[traces[2].aimAzDeg.Count - 1];
                float finalSpread = Mathf.Max(Mathf.Abs(Mathf.DeltaAngle(a25, a50)), Mathf.Abs(Mathf.DeltaAngle(a100, a50)));
                double posDiff = 0;
                for (int i = 0; i < Math.Min(traces[0].positions.Count, Math.Min(traces[1].positions.Count, traces[2].positions.Count)); i++)
                    posDiff = Math.Max(posDiff, Math.Max((traces[0].positions[i] - traces[1].positions[i]).magnitude, (traces[2].positions[i] - traces[1].positions[i]).magnitude));
                Check(finalSpread < 5e-3f, "RI", "final aim identical across 25 / 50 / 100 fps (spread " + finalSpread.ToString("E1") + " deg); trajectories differ by at most "
                    + posDiff.ToString("F2") + " m over 16 s - the unavoidable part: a physics step sees input up to the last rendered frame");
            }
        }

        private void EvaluateLoss()
        {
            sb.AppendLine();
            sb.AppendLine("[RL] Input loss (aim camera lost at 3 s)");
            Run n = runs.Find(r => r.kind == Kind.LossNeutral);
            Run d = runs.Find(r => r.kind == Kind.LossDrop);
            if (n != null)
                Check(n.completed && n.stepsToNeutralAfterEvent > 0 && n.stepsToNeutralAfterEvent * Dt <= 1f && n.loadsAtEnd == n.steps
                      && n.resolutionAtEnd == MavFlightControlLawBase.MavCommandResolution.SourceSignal,
                    "RL", n.id + ": the source ramps to neutral stick in " + (n.stepsToNeutralAfterEvent * Dt).ToString("F2") + " s and keeps producing it; "
                    + n.loadsAtEnd + " / " + n.steps + " loads - the aircraft keeps flying its physics hands-off");
            if (d != null)
                Check(d.completed && d.finite && d.loadsAtEnd == d.loadsAtEvent && d.withheldAtEnd > 0
                      && d.resolutionAtEnd == MavFlightControlLawBase.MavCommandResolution.SignalLossPolicy,
                    "RL", d.id + ": signal dropped -> law resolution " + d.resolutionAtEnd + " (declared NeutralCommand, never the inspector), loads frozen at "
                    + d.loadsAtEvent + ", " + d.withheldAtEnd + " withheld by live-readiness, owner at end " + d.ownerAtEnd + " - fail closed, counted");
        }

        // ================================================================== helpers

        private bool InsideEnvelope(MavF15SurfaceState s)
        {
            return s.symmetricStabilatorDeg >= authority.symmetricStabilator.minDeg && s.symmetricStabilatorDeg <= authority.symmetricStabilator.maxDeg
                   && s.aileronDeg >= authority.aileron.minDeg && s.aileronDeg <= authority.aileron.maxDeg
                   && s.differentialStabilatorDeg >= authority.differentialStabilator.minDeg && s.differentialStabilatorDeg <= authority.differentialStabilator.maxDeg
                   && s.rudderDeg >= authority.rudder.minDeg && s.rudderDeg <= authority.rudder.maxDeg;
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

        private static float Energy(Rigidbody rb)
        {
            float v = rb.linearVelocity.magnitude;
            return rb.position.y + v * v / (2f * 9.80665f);
        }

        private static string Trend(List<float> e)
        {
            if (e.Count == 0)
                return "-";
            int n = e.Count - 1;
            return e[0].ToString("F1") + " / " + e[n / 4].ToString("F1") + " / " + e[n / 2].ToString("F1") + " / " + e[3 * n / 4].ToString("F1") + " / " + e[n].ToString("F1");
        }

        private static float Min(List<float> v)
        {
            float m = float.MaxValue;
            foreach (float x in v) m = Mathf.Min(m, x);
            return v.Count > 0 ? m : 0f;
        }

        private static float Max(List<float> v)
        {
            float m = float.MinValue;
            foreach (float x in v) m = Mathf.Max(m, x);
            return v.Count > 0 ? m : 0f;
        }

        private static bool IsFinite(Vector3 v)
        {
            return MavMouseAimGuidance.IsFinite(v);
        }

        private void Check(bool condition, string tag, string label)
        {
            if (condition) Passed++;
            else Failed++;
            sb.AppendLine("  " + (condition ? "PASS" : "FAIL") + "  [" + tag + "] " + label);
        }
    }
}
