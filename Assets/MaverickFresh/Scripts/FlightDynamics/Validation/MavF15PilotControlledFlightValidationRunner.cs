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
    /// F-15 PILOT-CONTROLLED research aircraft V1 - deterministic Play Mode flight test.
    ///
    /// Flies <see cref="MavF15PilotControlledRig"/> - the same rig a human flies - through real Unity physics
    /// from the validated trim, with scripted normalized pilot commands (no keyboard, camera or UI):
    ///   neutral hold (and the frozen research path flown side by side: they must be the same trajectory),
    ///   the human prefab / keyboard rig holding trim with no input, individual pitch / roll / yaw pulses of
    ///   both signs and a combined pitch + roll, the scripted sequence neutral - pitch - neutral - roll -
    ///   neutral - yaw - neutral at dt, dt/2 and dt/4, and an ownership-loss fault.
    ///
    /// Pass criteria are sign, finiteness, continuity (no teleport), ownership, single load application,
    /// release-to-trim and convergence - never "looks right". RESEARCH MODEL + MAVERICK CONTROL
    /// APPROXIMATION; not NASA 836, not the production F-15 FCS.
    ///
    /// DETERMINISM: Time.captureFramerate = 50, a fixed run order, a fresh rig per run; commands switch on
    /// exact step indices, so every dt sees the same command timeline. Runs at -600, before the rig (-500),
    /// the ownership authority (-400), the law (-300), the actuator (-200) and the body (-100).
    /// </summary>
    [DefaultExecutionOrder(-600)]
    public sealed class MavF15PilotControlledFlightValidationRunner : MonoBehaviour
    {
        public static bool Finished;
        public static int Passed;
        public static int Failed;
        public static string Report = "";

        /// <summary>The human prefab to fly, set by the editor driver. Null: a keyboard rig is built instead.</summary>
        public static GameObject PrefabToTest;
        public static string PrefabPath = "(none)";

        public const int CaptureFramerate = 50;
        public const float BaseDt = 0.02f;
        public const float PulseAmplitude = 0.5f;
        public const double FtToM = 0.3048;
        public const double RadToDeg = 180.0 / Math.PI;

        /// <summary>A float32 state at ~1e-7 relative resolution: the floor below which a drift is round-off.</summary>
        public const double FloatFloor = 2e-6;

        public static readonly string[] StateNames = { "alpha", "beta", "p", "q", "r", "theta", "phi", "V" };

        private enum Kind
        {
            NeutralPilot,
            NeutralResearch,
            HumanRig,
            Pulse,
            Sequence,
            OwnershipLoss
        }

        private struct Sample
        {
            public double t;
            public double[] x;
            public Vector3 position;
            public Vector3 velocity;
            public MavF15SurfaceState surfaces;
            public bool aeroRefused;
        }

        private sealed class Run
        {
            public string id;
            public Kind kind;
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
            public bool unityGravityOffEveryStep = true;
            public bool dtAsConfigured = true;
            public bool finiteEveryStep = true;
            public int aeroRefusedSteps;
            public double maxKinematicResidualM;
            public double maxVelocityStepMps;
            public bool faultedNextStep;
            public bool loadsStoppedAfterFault;
            public bool disarmedAfterFault;
            public string faultReason;
            public bool hasHud;
            public bool hasCamera;
            public float fixedDeltaTimeUsed;
            public string physics;
        }

        private readonly List<Run> runs = new List<Run>();
        private readonly StringBuilder sb = new StringBuilder(64 * 1024);
        private int runIndex;
        private int phase;
        private int stepsDone;
        private int waitSteps;
        private MavF15PilotControlledRig rig;
        private GameObject researchRoot;
        private MavSixDoFBody activeBody;
        private MavManualPilotCommandSource activeSource;
        private MavFlightPhysicsOwnership activeGate;
        private float savedFixedDeltaTime;
        private int savedCaptureFramerate;
        private bool completeDone;
        private float trimBias;
        private double[] equilibrium;

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
            BuildRuns();
        }

        private void BuildRuns()
        {
            runs.Add(new Run { id = "NEUTRAL-HOLD pilot", kind = Kind.NeutralPilot, dt = BaseDt, steps = Steps(20f, BaseDt) });
            runs.Add(new Run { id = "NEUTRAL-HOLD frozen research path", kind = Kind.NeutralResearch, dt = BaseDt, steps = Steps(20f, BaseDt) });
            runs.Add(new Run { id = PrefabToTest != null ? "HUMAN PREFAB hold" : "HUMAN KEYBOARD RIG hold", kind = Kind.HumanRig, dt = BaseDt, steps = Steps(5f, BaseDt) });

            AddPulse("PULSE pitch +", PulseAmplitude, 0f, 0f);
            AddPulse("PULSE pitch -", -PulseAmplitude, 0f, 0f);
            AddPulse("PULSE roll +", 0f, PulseAmplitude, 0f);
            AddPulse("PULSE roll -", 0f, -PulseAmplitude, 0f);
            AddPulse("PULSE yaw +", 0f, 0f, PulseAmplitude);
            AddPulse("PULSE yaw -", 0f, 0f, -PulseAmplitude);
            AddPulse("PULSE pitch+roll", 0.3f, 0.3f, 0f);

            foreach (float dt in new[] { BaseDt, BaseDt / 2f, BaseDt / 4f })
                runs.Add(new Run { id = "SEQUENCE dt " + dt.ToString("F4", CultureInfo.InvariantCulture), kind = Kind.Sequence, dt = dt, steps = Steps(SequenceDurationS, dt) });

            runs.Add(new Run { id = "OWNERSHIP-LOSS", kind = Kind.OwnershipLoss, dt = BaseDt, steps = 100, lossStep = 25 });
        }

        private void AddPulse(string id, float pitch, float roll, float yaw)
        {
            runs.Add(new Run
            {
                id = id, kind = Kind.Pulse, dt = BaseDt, steps = Steps(4f, BaseDt), pitch = pitch, roll = roll, yaw = yaw,
                pulseStartStep = Steps(1f, BaseDt), pulseEndStep = Steps(2f, BaseDt)
            });
        }

        private static int Steps(float seconds, float dt)
        {
            return Mathf.RoundToInt(seconds / dt);
        }

        // Sequence: neutral 0-3 s, pitch +0.5 3-4 s, neutral 4-9 s, roll +0.5 9-10 s, neutral 10-15 s,
        // yaw +0.5 15-16 s, neutral 16-24 s.
        public const float SequenceDurationS = 24f;
        public static readonly float[] SequenceWindows = { 3f, 4f, 9f, 10f, 15f, 16f };

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
                if (In(stepIndex, run.dt, SequenceWindows[0], SequenceWindows[1])) c.pitch = PulseAmplitude;
                if (In(stepIndex, run.dt, SequenceWindows[2], SequenceWindows[3])) c.roll = PulseAmplitude;
                if (In(stepIndex, run.dt, SequenceWindows[4], SequenceWindows[5])) c.yaw = PulseAmplitude;
            }

            return c;
        }

        private static bool In(int stepIndex, float dt, float from, float to)
        {
            return stepIndex >= Steps(from, dt) && stepIndex < Steps(to, dt);
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
                    // next physics step, so a rig built and started in the same step would miss its first load.
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
            if (run.kind == Kind.NeutralResearch)
            {
                BuildResearchRig();
            }
            else if (run.kind == Kind.HumanRig)
            {
                if (PrefabToTest != null)
                {
                    GameObject instance = Object.Instantiate(PrefabToTest);
                    instance.name = "f15pc-human-prefab";
                    rig = instance.GetComponent<MavF15PilotControlledRig>();
                    // This record was flown on R1. The prefab serializes no revision and now loads with the R2
                    // default, so R1 is named before the rig first steps.
                    string revisionNote;
                    if (rig != null && !rig.TrySelectPhysicsRevision(MavF15PilotPhysicsRevision.R1InstantaneousSurfaces, out revisionNote))
                        throw new InvalidOperationException(revisionNote);
                }
                else
                {
                    rig = MavF15PilotControlledRig.Create("f15pc-human-keyboard-rig", MavF15PilotCommandSourceKind.Keyboard, true, MavF15PilotControlMode.DirectV1, MavF15PilotPhysicsRevision.R1InstantaneousSurfaces);
                }
            }
            else
            {
                rig = MavF15PilotControlledRig.Create("f15pc-" + run.id, MavF15PilotCommandSourceKind.Scripted, false, MavF15PilotControlMode.DirectV1, MavF15PilotPhysicsRevision.R1InstantaneousSurfaces);
            }
        }

        private void Begin(Run run)
        {
            stepsDone = 0;
            waitSteps = 0;

            if (run.kind == Kind.NeutralResearch)
            {
                string reason;
                run.started = StartResearch(out reason);
                run.startStatus = reason;
            }
            else if (run.kind == Kind.HumanRig)
            {
                run.hasHud = rig != null && rig.GetComponent<MavF15PilotControlledHud>() != null;
                run.hasCamera = rig != null && rig.GetComponentInChildren<Camera>() != null;
                activeBody = rig != null ? rig.body : null;
                activeSource = null;

                // The human rig starts ITSELF on its first physics step, exactly as it does for a person.
                phase = 2;
                return;
            }
            else
            {
                run.started = rig.StartFromTrim();
                run.startStatus = rig.debugStartStatus;
                activeBody = rig.body;
                activeSource = (MavManualPilotCommandSource)rig.commandSource;
                activeGate = rig.ownership;
            }

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
                // Wait for the prefab / keyboard rig to start itself.
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

            if (stepsDone == 1)
                run.physics = DescribePhysics(body.GetComponent<Rigidbody>());

            Sample s = Capture(stepsDone * (double)run.fixedDeltaTimeUsed);
            Sample prev = run.samples[run.samples.Count - 1];
            run.samples.Add(s);

            bool finite = true;
            for (int k = 0; k < 8; k++)
                finite &= !double.IsNaN(s.x[k]) && !double.IsInfinity(s.x[k]);
            finite &= s.surfaces.IsFinite() && IsFinite(s.position) && IsFinite(s.velocity);
            if (!finite)
                run.finiteEveryStep = false;

            // No teleport: PhysX's semi-implicit Euler moves the position by the NEW velocity times dt.
            double residual = (s.position - prev.position - s.velocity * run.fixedDeltaTimeUsed).magnitude;
            run.maxKinematicResidualM = Math.Max(run.maxKinematicResidualM, residual);
            run.maxVelocityStepMps = Math.Max(run.maxVelocityStepMps, (s.velocity - prev.velocity).magnitude);
            if (s.aeroRefused)
                run.aeroRefusedSteps++;

            bool afterLoss = run.lossStep >= 0 && stepsDone > run.lossStep;
            MavFlightPhysicsOwner expected = run.kind == Kind.NeutralResearch
                ? MavFlightPhysicsOwner.F15AfitResearch
                : MavFlightPhysicsOwner.F15PilotControlledResearch;
            if (!afterLoss)
            {
                if (activeGate == null || activeGate.owner != expected || !body.ArmedForLiveFlight)
                    run.ownerHeldEveryStep = false;
                if (CountArmedBodies() != 1)
                    run.exactlyOneArmedEveryStep = false;
                Rigidbody rb = body.GetComponent<Rigidbody>();
                if (rb == null || rb.useGravity)
                    run.unityGravityOffEveryStep = false;
            }

            if (run.lossStep >= 0 && stepsDone == run.lossStep)
            {
                run.loadApplicationsAtEnd = body.debugLoadApplications;

                // The pilot profile stops being the body's provider: the source environment is gone.
                body.profileProvider = null;
            }

            if (run.lossStep >= 0 && stepsDone == run.lossStep + 1)
            {
                run.faultedNextStep = activeGate.owner == MavFlightPhysicsOwner.Fault;
                run.disarmedAfterFault = !body.ArmedForLiveFlight;
                run.faultReason = activeGate.ownerReason;
            }

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

        private void Finish(Run run)
        {
            if (activeBody != null)
            {
                if (run.lossStep < 0)
                    run.loadApplicationsAtEnd = activeBody.debugLoadApplications;
                run.duplicateRejections = activeBody.debugRejectedDuplicateApplications;
                run.initialStateApplications = activeBody.debugInitialStateApplications;
            }

            run.completed = run.failure == null && stepsDone >= run.steps;

            if (activeGate != null && activeGate.owner != MavFlightPhysicsOwner.Fault)
                activeGate.ReturnToLegacy("pilot-controlled validation run complete");

            if (rig != null)
                Object.DestroyImmediate(rig.gameObject);
            if (researchRoot != null)
                Object.DestroyImmediate(researchRoot);

            rig = null;
            researchRoot = null;
            activeBody = null;
            activeSource = null;
            activeGate = null;
            runIndex++;
            phase = 0;
        }

        private Sample Capture(double t)
        {
            MavFlightState s = MavF15AfitResearchStateInjection.ReadBack(activeBody);
            Rigidbody rb = activeBody.GetComponent<Rigidbody>();
            MavF15ControlActuator actuator = activeBody.controlSurfaceActuator as MavF15ControlActuator;
            bool refused = false;
            MavF15PilotControlledAeroModel pilotAero = activeBody.aerodynamicModel as MavF15PilotControlledAeroModel;
            MavF15AeroModel researchAero = activeBody.aerodynamicModel as MavF15AeroModel;
            if (pilotAero != null) refused = pilotAero.debugRefused && t > 0.0;
            if (researchAero != null) refused = researchAero.debugRefused && t > 0.0;
            return new Sample
            {
                t = t,
                x = new double[]
                {
                    s.alphaRad, s.betaRad, s.aeroBodyRatesRadSec.x, s.aeroBodyRatesRadSec.y, s.aeroBodyRatesRadSec.z,
                    s.attitude.pitchAttitudeRad, s.attitude.bankAngleRad, s.trueAirspeedMps / FtToM
                },
                position = rb.position,
                velocity = rb.linearVelocity,
                surfaces = actuator != null ? actuator.ActualF15SurfaceState.channels : MavF15SurfaceState.Neutral,
                aeroRefused = refused
            };
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

        /// <summary>The frozen research path at the same trim: research profile, MavF15AeroModel, static hold, research owner.</summary>
        private void BuildResearchRig()
        {
            researchRoot = new GameObject("f15pc-frozen-research-path");
            Rigidbody rb = researchRoot.AddComponent<Rigidbody>();
            rb.interpolation = RigidbodyInterpolation.None;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            MavF15AfitResearchFlightDynamicsProfile profile = researchRoot.AddComponent<MavF15AfitResearchFlightDynamicsProfile>();
            profile.conditionMode = MavF15ResearchConditionMode.SourceReproduction;
            profile.densityPolicy = MavF15ResearchDensityPolicy.SourceFixedDensity;
            profile.gravityPolicy = MavF15ResearchGravityPolicy.SourceGravity;
            MavF15AeroModel aero = researchRoot.AddComponent<MavF15AeroModel>();
            aero.sourceMode = MavF15AeroSourceMode.BaumannMach06SixAxisResearch;
            aero.allowCrossValidationResearchModel = true;
            MavManualPilotCommandSource command = researchRoot.AddComponent<MavManualPilotCommandSource>();
            command.treatAsOperationalSource = true;
            MavF15ControlLaw law = researchRoot.AddComponent<MavF15ControlLaw>();
            law.mode = MavF15FcsMode.AFITResearch;
            MavF15ControlActuator actuator = researchRoot.AddComponent<MavF15ControlActuator>();
            MavF15AfitResearchFixedThrust thrust = researchRoot.AddComponent<MavF15AfitResearchFixedThrust>();
            MavF15AfitResearchStaticSurfaceHold hold = researchRoot.AddComponent<MavF15AfitResearchStaticSurfaceHold>();
            MavSixDoFBody body = researchRoot.AddComponent<MavSixDoFBody>();
            body.profileProvider = profile;
            body.aerodynamicModel = aero;
            body.controlSurfaceActuator = actuator;
            body.controlLaw = law;
            body.pilotCommandSource = command;
            body.propulsionModel = thrust;
            body.autoApplyProfileConfiguration = true;
            body.applyMassPropertiesOnEnable = true;
            body.acceptNonAuthoritativePropulsion = true;
            body.requireOperationalReadinessForLoadApplication = true;
            body.allowStructuralOnlyLoadApplication = false;
            law.sixDoFBody = body;
            law.actuator = actuator;
            law.commandSource = command;
            law.driveActuatorInFixedUpdate = true;
            actuator.sixDoFBody = body;
            hold.sixDoFBody = body;
            body.ApplyConfiguredProfile(true);
            body.NotifyOwnershipChanged();
        }

        private bool StartResearch(out string reason)
        {
            MavSixDoFBody body = researchRoot.GetComponent<MavSixDoFBody>();
            MavF15ResearchInitializationReport init = MavF15AfitResearchStateInjection.TryInitialize(
                body, MavF15PilotTrimStart.TableViiPoint36().ToKinematicState());
            if (!init.initialized)
            {
                reason = init.reason;
                return false;
            }

            MavFlightPhysicsOwnership gate = researchRoot.AddComponent<MavFlightPhysicsOwnership>();
            gate.allowResearchValidationOwnership = true;
            body.physicsOwnership = gate;
            gate.AttachGovernedBody(body);
            body.NotifyOwnershipChanged();
            if (!gate.TryEnterF15AfitResearchOwnership(body, MavF15ResearchOwnershipGrant.Instance, out reason))
                return false;

            activeBody = body;
            activeGate = gate;
            activeSource = null;
            reason = "frozen research path started at the same trim";
            return true;
        }

        private static string DescribePhysics(Rigidbody rb)
        {
            return "fixedDeltaTime " + Time.fixedDeltaTime.ToString("R", CultureInfo.InvariantCulture)
                + " s, captureFramerate " + Time.captureFramerate + ", simulationMode " + Physics.simulationMode
                + ", Physics.gravity " + Physics.gravity.ToString("F3") + " (NOT used: useGravity off)"
                + " | rigidbody: mass " + rb.mass.ToString("R", CultureInfo.InvariantCulture)
                + " kg, inertia " + rb.inertiaTensor.ToString("F1") + " rot " + rb.inertiaTensorRotation.eulerAngles.ToString("F4")
                + ", useGravity " + rb.useGravity + ", damping " + rb.linearDamping.ToString("R", CultureInfo.InvariantCulture)
                + "/" + rb.angularDamping.ToString("R", CultureInfo.InvariantCulture) + ", interpolation " + rb.interpolation
                + ", colliders " + rb.GetComponents<Collider>().Length;
        }

        // ================================================================== evaluation

        private void Complete()
        {
            Time.fixedDeltaTime = savedFixedDeltaTime;
            Time.captureFramerate = savedCaptureFramerate;

            sb.AppendLine("F-15 PILOT-CONTROLLED RESEARCH AIRCRAFT V1 - PLAY MODE FLIGHT TEST (" + MavF15PilotControlledIdentity.ConfigurationId + ")");
            sb.AppendLine("=====================================================================================");
            sb.AppendLine("Research aerodynamics (frozen AFIT/Baumann/Davison baseline) + Maverick control approximation. NOT NASA 836, NOT the production F-15 FCS.");
            sb.AppendLine("Scripted normalized pilot commands; temporary rigs on the empty batch scene; human prefab: " + PrefabPath);
            MavF15PilotTrimStart t = MavF15PilotTrimStart.TableViiPoint36();
            sb.AppendLine("Trim start: Table VII point " + t.tableViiPoint + " - stab " + t.symmetricStabilatorDeg.ToString("F5", CultureInfo.InvariantCulture)
                          + " deg, alpha " + t.alphaDeg.ToString("F4", CultureInfo.InvariantCulture) + ", theta " + t.thetaDeg.ToString("F4", CultureInfo.InvariantCulture)
                          + ", V " + t.trueAirspeedFtPerSec.ToString("F1", CultureInfo.InvariantCulture) + " ft/s; pulse amplitude " + PulseAmplitude + " (pulses 1.0-2.0 s)");

            EvaluateMechanics();
            EvaluateNeutral();
            EvaluateHumanRig();
            EvaluatePulses();
            EvaluateSequence();
            EvaluateConvergence();
            EvaluateOwnershipLoss();

            sb.AppendLine();
            sb.Append("RESULT: ").Append(Failed == 0 ? "PASS" : "FAIL").Append("  passed=").Append(Passed).Append(" failed=").Append(Failed);
            Report = sb.ToString();
            Finished = true;
        }

        private void EvaluateMechanics()
        {
            sb.AppendLine();
            sb.AppendLine("[M] Runtime mechanics of every run (owner, one armed body, one load application per step, no teleport, finite)");
            foreach (Run r in runs)
            {
                int expectedLoads = r.kind == Kind.OwnershipLoss ? r.lossStep : r.steps;
                bool ok = r.completed && r.failure == null && r.finiteEveryStep && r.ownerHeldEveryStep && r.exactlyOneArmedEveryStep
                          && r.unityGravityOffEveryStep && r.dtAsConfigured && r.duplicateRejections == 0 && r.initialStateApplications == 1
                          && r.maxKinematicResidualM < 2e-3 && r.maxVelocityStepMps < 2.0
                          && (r.kind == Kind.OwnershipLoss || r.kind == Kind.HumanRig ? r.loadApplicationsAtEnd >= expectedLoads - 1 : r.loadApplicationsAtEnd == expectedLoads);
                Check(ok, "M", r.id.PadRight(34) + " " + r.steps + " steps, loads " + r.loadApplicationsAtEnd + ", duplicate refusals " + r.duplicateRejections
                    + ", initial-state writes " + r.initialStateApplications + ", owner held " + r.ownerHeldEveryStep + ", one armed body " + r.exactlyOneArmedEveryStep
                    + ", Unity gravity off " + r.unityGravityOffEveryStep + ", finite " + r.finiteEveryStep + ", max |dx - v dt| "
                    + r.maxKinematicResidualM.ToString("E1") + " m, max |dv| " + r.maxVelocityStepMps.ToString("F3") + " m/s, aero refused "
                    + r.aeroRefusedSteps + " steps, dt " + r.fixedDeltaTimeUsed.ToString("R", CultureInfo.InvariantCulture)
                    + (r.failure == null ? "" : " - " + r.failure));
            }

            Run first = runs[0];
            if (first.physics != null)
                sb.AppendLine("    physics (first run): " + first.physics);
        }

        private void EvaluateNeutral()
        {
            sb.AppendLine();
            sb.AppendLine("[H] Neutral hold - the pilot layer at centred stick flies the frozen research equilibrium");
            Run pilot = Find(Kind.NeutralPilot), research = Find(Kind.NeutralResearch);
            if (pilot == null || research == null || !pilot.completed || !research.completed)
            {
                Check(false, "H", "both neutral runs completed");
                return;
            }

            double[] drift = Drift(pilot);
            bool surfacesAtTrim = true;
            foreach (Sample s in pilot.samples)
                surfacesAtTrim &= s.surfaces.symmetricStabilatorDeg == trimBias && s.surfaces.aileronDeg == 0f
                                  && s.surfaces.differentialStabilatorDeg == 0f && s.surfaces.rudderDeg == 0f;
            double worst = 0;
            for (int k = 0; k < 8; k++)
                worst = Math.Max(worst, drift[k]);
            sb.AppendLine("    pilot 20 s: max |x - trim| " + DescribeStates(drift));
            Check(surfacesAtTrim && worst <= FloatFloor && pilot.aeroRefusedSteps == 0,
                "H", "centred stick holds the trim for 20 s: every state within " + worst.ToString("E1") + " (float floor " + FloatFloor.ToString("E0")
                     + "), surfaces exactly at the trim bias (" + trimBias.ToString("R", CultureInfo.InvariantCulture) + " deg) and zero lateral");

            bool identical = pilot.samples.Count == research.samples.Count;
            double maxDiff = 0;
            for (int i = 0; identical && i < pilot.samples.Count; i++)
            {
                for (int k = 0; k < 8; k++)
                    maxDiff = Math.Max(maxDiff, Math.Abs(pilot.samples[i].x[k] - research.samples[i].x[k]));
                if (pilot.samples[i].position != research.samples[i].position || pilot.samples[i].velocity != research.samples[i].velocity)
                    identical = false;
            }

            Check(identical && maxDiff == 0.0,
                "H", "the pilot-controlled aircraft and the frozen research path (research profile, MavF15AeroModel, static hold, research owner) fly the SAME trajectory "
                     + "bit for bit over 20 s (" + pilot.samples.Count + " samples, max state difference " + maxDiff.ToString("E1")
                     + "): the control layer adds nothing at neutral stick");
        }

        private void EvaluateHumanRig()
        {
            sb.AppendLine();
            sb.AppendLine("[K] The human-flown rig (" + (PrefabToTest != null ? "prefab " + PrefabPath : "keyboard rig") + "): starts itself, holds trim with no input");
            Run r = Find(Kind.HumanRig);
            if (r == null || !r.completed)
            {
                Check(false, "K", "the human rig run completed" + (r != null && r.failure != null ? " - " + r.failure : ""));
                return;
            }

            double[] drift = Drift(r);
            double worst = 0;
            for (int k = 0; k < 8; k++)
                worst = Math.Max(worst, drift[k]);
            Check(r.started && worst <= FloatFloor && r.ownerHeldEveryStep,
                "K", "started itself on its first physics step (" + r.startStatus + "), keyboard source with no key held = neutral, held the trim for 5 s within "
                     + worst.ToString("E1"));
            if (PrefabToTest != null)
                Check(r.hasHud && r.hasCamera, "K", "the prefab carries its HUD (diagnostics, THROTTLE INACTIVE) and its camera");
        }

        private void EvaluatePulses()
        {
            sb.AppendLine();
            sb.AppendLine("[S] Sign response in Play Mode - 1 s pulses from trim (commanded rate change at pulse end, then release)");
            foreach (Run r in runs)
            {
                if (r.kind != Kind.Pulse)
                    continue;
                if (!r.completed)
                {
                    Check(false, "S", r.id + " completed");
                    continue;
                }

                double[] d = Delta(r, r.pulseStartStep, r.pulseEndStep);
                bool released = SurfacesAtTrimFrom(r, r.pulseEndStep + 1);
                bool moved = SurfacesMovedDuring(r);
                bool ok;
                string what;
                if (r.pitch != 0f && r.roll != 0f)
                {
                    ok = d[3] > 0 && d[2] > 0;
                    what = "q " + d[3].ToString("+0.0000;-0.0000") + " and p " + d[2].ToString("+0.0000;-0.0000") + " rad/s (both + expected)";
                }
                else if (r.pitch != 0f)
                {
                    ok = Math.Sign(d[3]) == Math.Sign(r.pitch) && Math.Sign(d[5]) == Math.Sign(r.pitch);
                    what = "q " + d[3].ToString("+0.0000;-0.0000") + " rad/s, theta " + (d[5] * RadToDeg).ToString("+0.000;-0.000") + " deg";
                }
                else if (r.roll != 0f)
                {
                    ok = Math.Sign(d[2]) == Math.Sign(r.roll) && Math.Sign(d[6]) == Math.Sign(r.roll);
                    what = "p " + d[2].ToString("+0.0000;-0.0000") + " rad/s, phi " + (d[6] * RadToDeg).ToString("+0.000;-0.000") + " deg";
                }
                else
                {
                    ok = Math.Sign(d[4]) == Math.Sign(r.yaw);
                    what = "r " + d[4].ToString("+0.0000;-0.0000") + " rad/s, beta " + (d[1] * RadToDeg).ToString("+0.000;-0.000") + " deg";
                }

                Check(ok && moved && released && r.aeroRefusedSteps == 0,
                    "S", r.id.PadRight(18) + " (" + Commanded(r) + "): " + what + " over the pulse; surfaces moved with the stick and returned EXACTLY to trim on release");
            }
        }

        private void EvaluateSequence()
        {
            sb.AppendLine();
            sb.AppendLine("[Q] Scripted sequence neutral - pitch - neutral - roll - neutral - yaw - neutral (dt 0.02)");
            Run r = runs.Find(x => x.kind == Kind.Sequence && Math.Abs(x.dt - BaseDt) < 1e-6f);
            if (r == null || !r.completed)
            {
                Check(false, "Q", "the sequence completed" + (r != null && r.failure != null ? " - " + r.failure : ""));
                return;
            }

            string[] names = { "pitch", "roll", "yaw" };
            int[] axis = { 3, 2, 4 };
            for (int w = 0; w < 3; w++)
            {
                int start = Steps(SequenceWindows[2 * w], r.dt), end = Steps(SequenceWindows[2 * w + 1], r.dt);
                int next = w < 2 ? Steps(SequenceWindows[2 * w + 2], r.dt) : r.steps;
                double[] d = Delta(r, start, end);
                double early = PeakAbs(r, axis[w], end, end + Steps(1f, r.dt));
                double late = PeakAbs(r, axis[w], next - Steps(1f, r.dt), next);
                bool released = SurfacesAtTrimFrom(r, end + 1, next);
                Check(d[axis[w]] > 0 && released && late < early,
                    "Q", names[w] + " +" + PulseAmplitude + " at " + SequenceWindows[2 * w] + "-" + SequenceWindows[2 * w + 1] + " s: "
                         + StateNames[axis[w]] + " " + d[axis[w]].ToString("+0.0000;-0.0000") + " rad/s over the pulse; after release surfaces at trim and the "
                         + StateNames[axis[w]] + " peak decays " + early.ToString("F4") + " -> " + late.ToString("F4") + " rad/s (research-model natural dynamics)");
            }

            double[] env = Envelope(r);
            Check(r.aeroRefusedSteps == 0 && r.finiteEveryStep,
                "Q", "the whole sequence stays inside the research domain (no aerodynamic refusal): alpha " + (env[0] * RadToDeg).ToString("F2") + ".." + (env[1] * RadToDeg).ToString("F2")
                     + " deg, |beta| <= " + (env[2] * RadToDeg).ToString("F2") + " deg, V " + env[3].ToString("F1") + ".." + env[4].ToString("F1") + " ft/s, |phi| <= "
                     + (env[5] * RadToDeg).ToString("F2") + " deg");
        }

        private void EvaluateConvergence()
        {
            sb.AppendLine();
            sb.AppendLine("[D] Convergence of the sequence: dt, dt/2 against dt/4 (max over the common 0.02 s grid; rad, rad/s; V ft/s)");
            List<Run> seq = runs.FindAll(x => x.kind == Kind.Sequence);
            if (seq.Count != 3 || !seq[0].completed || !seq[1].completed || !seq[2].completed)
            {
                Check(false, "D", "the three sequence runs completed");
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
                     + " (3 for first order against dt/4): the piloted trajectory converges like the integration error of a fixed model, not like a fault");
        }

        private void EvaluateOwnershipLoss()
        {
            sb.AppendLine();
            sb.AppendLine("[N] Fail-closed ownership");
            Run r = Find(Kind.OwnershipLoss);
            if (r == null || !r.completed)
            {
                Check(false, "N", "the ownership-loss run completed");
                return;
            }

            Check(r.faultedNextStep && r.disarmedAfterFault && r.loadsStoppedAfterFault,
                "N", "the pilot profile removed as the body's provider at step " + r.lossStep + ": before the body's next step the pilot-controlled owner FAULTS, the body is disarmed and "
                     + "no further load is applied - never a fallback to the standard atmosphere or Unity gravity (" + r.faultReason + ")");
        }

        // ================================================================== helpers

        private Run Find(Kind kind)
        {
            return runs.Find(x => x.kind == kind);
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

        private static double[] Delta(Run r, int from, int to)
        {
            double[] d = new double[8];
            if (to >= r.samples.Count || from >= r.samples.Count)
                return d;
            for (int k = 0; k < 8; k++)
                d[k] = r.samples[to].x[k] - r.samples[from].x[k];
            return d;
        }

        private static double PeakAbs(Run r, int k, int from, int to)
        {
            double peak = 0;
            for (int i = Math.Max(0, from); i < Math.Min(to, r.samples.Count); i++)
                peak = Math.Max(peak, Math.Abs(r.samples[i].x[k]));
            return peak;
        }

        private bool SurfacesAtTrimFrom(Run r, int from)
        {
            return SurfacesAtTrimFrom(r, from, r.samples.Count);
        }

        private bool SurfacesAtTrimFrom(Run r, int from, int to)
        {
            for (int i = from; i < Math.Min(to, r.samples.Count); i++)
            {
                MavF15SurfaceState s = r.samples[i].surfaces;
                if (s.symmetricStabilatorDeg != trimBias || s.aileronDeg != 0f || s.differentialStabilatorDeg != 0f || s.rudderDeg != 0f)
                    return false;
            }

            return from < r.samples.Count;
        }

        private bool SurfacesMovedDuring(Run r)
        {
            int i = Math.Min(r.pulseStartStep + 1, r.samples.Count - 1);
            MavF15SurfaceState s = r.samples[i].surfaces;
            bool pitchOk = r.pitch == 0f ? s.symmetricStabilatorDeg == trimBias : Math.Sign(trimBias - s.symmetricStabilatorDeg) == Math.Sign(r.pitch);
            bool rollOk = r.roll == 0f ? s.aileronDeg == 0f : Math.Sign(s.aileronDeg) == Math.Sign(r.roll);
            bool yawOk = r.yaw == 0f ? s.rudderDeg == 0f : Math.Sign(s.rudderDeg) == -Math.Sign(r.yaw);
            return pitchOk && rollOk && yawOk;
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

        private static string Commanded(Run r)
        {
            return "pitch " + r.pitch.ToString("+0.0;-0.0;0") + ", roll " + r.roll.ToString("+0.0;-0.0;0") + ", yaw " + r.yaw.ToString("+0.0;-0.0;0");
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
