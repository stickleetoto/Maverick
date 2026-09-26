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
    /// F-15 research runtime closeout: the AFIT/Baumann/Davison research configuration flown through the
    /// real Unity runtime path - MavSixDoFBody, PhysX, the ownership authority - in PLAY MODE, and compared
    /// with the WP-3E source RHS / RK4 reference.
    ///
    /// RESEARCH SOURCE MODEL ONLY. Not NASA 836, not the production FCS, not gameplay: every object is a
    /// temporary validation rig built in memory on the empty batch scene and destroyed afterwards. No pilot
    /// input, no throttle, no F100; the stabilator is the source-defined static hold.
    ///
    /// DETERMINISM. Time.captureFramerate = 50 pins frame pacing; each run sets Time.fixedDeltaTime one step
    /// before it starts; every rig is fresh; the order of runs is fixed.
    ///
    /// ORDER. This runs at execution order -500, before the ownership authority (-400), the control law,
    /// the actuator (-200) and the body (-100). A run is initialized and handed to the research owner here,
    /// so the body's first load computation of the run is at the injected state, before PhysX integrates.
    /// Each later call reads the state PhysX just produced and the loads the body computed one step earlier.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class MavF15ResearchRuntimeFlightValidationRunner : MonoBehaviour
    {
        public static bool Finished;
        public static int Passed;
        public static int Failed;
        public static string Report = "";

        public static void Clear()
        {
            Finished = false;
            Passed = 0;
            Failed = 0;
            Report = "";
        }

        // ------------------------------------------------------------------ fixed choices

        /// <summary>Table VII symmetric point 36: alpha 17.49 deg, stabilator -15.145 deg (centre of -25..-5).</summary>
        public const int SymmetricPoint = 36;

        /// <summary>Table VII turning point 150: phi -21.04 deg, all five modes stable (WP-3D/WP-3E).</summary>
        public const int TurningPoint = 150;

        public const float BaseDt = 0.02f;
        public const int CaptureFramerate = 50;
        public const float HoldDurationS = 30f;
        public const float PerturbedDurationS = 10f;

        /// <summary>Reference RK4 step for every comparison (WP-3E showed its plateau well above this).</summary>
        public const double ReferenceRk4Step = 0.0025;

        /// <summary>
        /// Below this (rad, rad/s, or relative V) a Unity-vs-reference difference is at the float floor of the
        /// float32 runtime and its dt trend is not interpreted. NUMERICAL.
        /// </summary>
        public const double FloatFloor = 2e-6;

        private const double RadToDeg = 180.0 / Math.PI;
        private const double FtToM = 0.3048;

        private static readonly float[] Dts = { BaseDt, BaseDt / 2f, BaseDt / 4f };
        private static readonly string[] StateNames = { "alpha", "beta", "p", "q", "r", "theta", "phi", "V" };

        private enum CaseKind { SymmetricHold, TurningHold, SymmetricPerturbed, TurningPerturbed, TurningHoldGyroOff, EnvironmentLoss }

        private struct Sample
        {
            public double t;
            public double[] x;          // alpha, beta, p, q, r, theta, phi (rad, rad/s), V (ft/s)
            public double heading;
            public double gamma;
            public Vector3 position;
            public Vector3 velocity;
            public Vector3 angularVelocityWorld;
            public double linearResidualG;
            public double angularResidual;
            public bool hasResidual;
        }

        private sealed class Run
        {
            public string id;
            public CaseKind kind;
            public float dt;
            public int steps;
            public MavF15ResearchRuntimeInitialState initial;
            public double stabilatorDeg;
            public double headingRatePredicted;
            public double gammaPredictedRad;
            public double[] equilibrium;

            public readonly List<Sample> samples = new List<Sample>(6200);
            public bool completed;
            public string failure;
            public int loadApplications;
            public bool ownerHeldEveryStep = true;
            public bool exactlyOneArmedEveryStep = true;
            public bool unityGravityOffEveryStep = true;
            public bool dtAsSpecifiedEveryStep = true;
            public float fixedDeltaTimeUsed;
            public int refusals;
            public Quaternion rotation0;
            public Vector3 velocity0;
            public Vector3 angularVelocity0;
            public Vector3 appliedForceLocal0;
            public Vector3 appliedTorqueLocal0;
            public Vector3 externalMomentAero0;
            public Vector3 omegaAero0;
            public float mass;
            public Vector3 inertiaPrincipal;
            public Quaternion inertiaRotation;
            public Vector3 velocity1;
            public Vector3 angularVelocity1;
            public Vector3 position0;
            public Vector3 position1;
            public string physicsSettings;
            public bool faultObserved;
            public int applicationsAfterLoss;

            // reference comparison
            public double[] maxAbsDiff = new double[8];
            public double[] finalDiff = new double[8];
            public double[] unityDrift = new double[8];
            public double[] referenceDrift = new double[8];
            public double headingRateMeasured;
            public double gammaMeanMeasured;
        }

        private readonly List<Run> runs = new List<Run>();
        private StringBuilder sb;
        private int runIndex;
        private int phase;
        private int stepsDone;
        private Rig rig;
        private float savedFixedDeltaTime;
        private int savedCaptureFramerate;
        private MavF15ResearchEquilibrium symmetric;
        private MavF15ResearchEquilibrium turning;
        private MavF15ResearchTurningTrimResidual turningResidual;

        // ================================================================== setup

        private void Awake()
        {
            Clear();
            sb = new StringBuilder(65536);
            savedFixedDeltaTime = Time.fixedDeltaTime;
            savedCaptureFramerate = Time.captureFramerate;
            Time.captureFramerate = CaptureFramerate;

            sb.AppendLine("F-15 RESEARCH RUNTIME CLOSEOUT - PLAY MODE (" + MavF15AfitResearchIdentity.ConfigurationId + ")");
            sb.AppendLine("=====================================================================================");
            sb.AppendLine("RESEARCH SOURCE MODEL through the Unity runtime path. Not NASA 836, not the production FCS, not gameplay.");
            sb.AppendLine("Unity " + Application.unityVersion + "; temporary in-memory rigs on the empty batch scene.");

            try
            {
                PrepareEquilibria();
                ValidateOwnershipRules();
                BuildRunList();
            }
            catch (Exception e)
            {
                Check(false, "SETUP", "setup threw " + e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace);
                runs.Clear();
            }
        }

        private void PrepareEquilibria()
        {
            MavF15TableViiState sym = default(MavF15TableViiState);
            foreach (MavF15TableViiState s in MavF15TableViiTrimRecovery.SymmetricStates())
            {
                if (s.part1Point == SymmetricPoint)
                    sym = s;
            }

            MavF15TableViiState turn = default(MavF15TableViiState);
            foreach (MavF15TableViiState s in MavF15TableViiTurningRecovery.TurningSectionStates())
            {
                if (s.part1Point == TurningPoint)
                    turn = s;
            }

            symmetric = MavF15ResearchStabilityAnalysis.Symmetric(sym, sym.trueVelocityFtPerSec, MavF15ResearchEquilibriumBranch.Symmetric);
            MavF15ResearchTurningTrimResult tr = MavF15AfitResearchTrimSolver.SolveTurning(
                MavF15AfitResearchIdentity.ConfigurationId, turn.phiDeg,
                MavF15TableViiTurningRecovery.Guess(MavF15TableViiTurningRecovery.Published(turn)));
            turning = MavF15ResearchStabilityAnalysis.FromTurningResult(turn, tr);
            turningResidual = tr.residual;

            sb.AppendLine();
            sb.AppendLine("Representative equilibria (WP-3B / WP-3C solves; physical units):");
            AppendEquilibrium("symmetric", symmetric);
            AppendEquilibrium("turning  ", turning);
            sb.Append("  turning WP-3C prediction: heading rate ").Append(turningResidual.headingRateRadSec.ToString("F6", CultureInfo.InvariantCulture))
              .Append(" rad/s, flight-path angle ").Append(turningResidual.flightPathAngleDeg.ToString("F4", CultureInfo.InvariantCulture))
              .AppendLine(" deg");
            Check(symmetric.recovered && turning.recovered, "EQ", "both representative equilibria re-solve from the printed rows (WP-3B / WP-3C)");
        }

        private void AppendEquilibrium(string label, MavF15ResearchEquilibrium eq)
        {
            MavF15ResearchSourceState x = eq.state;
            sb.Append("  ").Append(label).Append(" point ").Append(eq.point)
              .Append(": stab ").Append(eq.stabilatorDeg.ToString("F5", CultureInfo.InvariantCulture))
              .Append(" deg, alpha ").Append((x.alphaRad * RadToDeg).ToString("F4", CultureInfo.InvariantCulture))
              .Append(", beta ").Append((x.betaRad * RadToDeg).ToString("F5", CultureInfo.InvariantCulture))
              .Append(", p ").Append(x.pRadSec.ToString("F6", CultureInfo.InvariantCulture))
              .Append(", q ").Append(x.qRadSec.ToString("F6", CultureInfo.InvariantCulture))
              .Append(", r ").Append(x.rRadSec.ToString("F6", CultureInfo.InvariantCulture))
              .Append(", theta ").Append((x.thetaRad * RadToDeg).ToString("F4", CultureInfo.InvariantCulture))
              .Append(", phi ").Append((x.phiRad * RadToDeg).ToString("F3", CultureInfo.InvariantCulture))
              .Append(", V ").Append(x.trueAirspeedFtPerSec.ToString("F3", CultureInfo.InvariantCulture)).AppendLine(" ft/s");
        }

        private void BuildRunList()
        {
            for (int i = 0; i < Dts.Length; i++)
                runs.Add(MakeRun("S-HOLD", CaseKind.SymmetricHold, Dts[i], HoldDurationS));
            for (int i = 0; i < Dts.Length; i++)
                runs.Add(MakeRun("T-HOLD", CaseKind.TurningHold, Dts[i], HoldDurationS));
            for (int i = 0; i < Dts.Length; i++)
                runs.Add(MakeRun("S-PERT", CaseKind.SymmetricPerturbed, Dts[i], PerturbedDurationS));
            for (int i = 0; i < Dts.Length; i++)
                runs.Add(MakeRun("T-PERT", CaseKind.TurningPerturbed, Dts[i], PerturbedDurationS));
            runs.Add(MakeRun("T-HOLD-GYRO-OFF", CaseKind.TurningHoldGyroOff, BaseDt, HoldDurationS));
            runs.Add(MakeRun("ENV-LOSS", CaseKind.EnvironmentLoss, BaseDt, 1f));
        }

        private Run MakeRun(string id, CaseKind kind, float dt, float duration)
        {
            bool isTurning = kind == CaseKind.TurningHold || kind == CaseKind.TurningPerturbed
                             || kind == CaseKind.TurningHoldGyroOff || kind == CaseKind.EnvironmentLoss;
            MavF15ResearchEquilibrium eq = isTurning ? turning : symmetric;
            MavF15ResearchSourceState x = eq.state;

            Run r = new Run
            {
                id = id + " dt " + dt.ToString("F4", CultureInfo.InvariantCulture),
                kind = kind,
                dt = dt,
                steps = Mathf.RoundToInt(duration / dt),
                stabilatorDeg = eq.stabilatorDeg,
                equilibrium = x.ToArray(),
                headingRatePredicted = isTurning ? turningResidual.headingRateRadSec : 0.0,
                gammaPredictedRad = isTurning
                    ? turningResidual.flightPathAngleDeg / RadToDeg
                    : x.thetaRad - x.alphaRad
            };

            r.initial = new MavF15ResearchRuntimeInitialState
            {
                alphaRad = x.alphaRad,
                betaRad = x.betaRad,
                pRadSec = x.pRadSec,
                qRadSec = x.qRadSec,
                rRadSec = x.rRadSec,
                thetaRad = x.thetaRad,
                phiRad = x.phiRad,
                trueAirspeedFtPerSec = x.trueAirspeedFtPerSec,
                symmetricStabilatorDeg = eq.stabilatorDeg,
                headingRad = 0.0,
                altitudeM = MavF15ResearchRuntimeInitialState.DefaultAltitudeM,
                sourceNote = "Baumann Table VII point " + eq.point + " (" + eq.branch + ", WP-3B/WP-3C solve)"
            };

            // Deterministic excitations for the dynamic comparisons: longitudinal for the symmetric case,
            // lateral (roll/yaw, where Ixz couples) for the turning case. Not part of the equilibria.
            if (kind == CaseKind.SymmetricPerturbed)
                r.initial.alphaRad += 0.5 / RadToDeg;
            if (kind == CaseKind.TurningPerturbed)
            {
                r.initial.betaRad += 0.5 / RadToDeg;
                r.initial.pRadSec += 0.02;
            }

            return r;
        }

        // ================================================================== ownership rules (item 1)

        private void ValidateOwnershipRules()
        {
            sb.AppendLine();
            sb.AppendLine("[O] Research ownership state (minimum): rules, defaults, refusals");

            MavFlightPhysicsOwner r = MavFlightPhysicsOwner.F15AfitResearch;
            Check(MavFlightPhysicsOwnership.IsReplacementPhysicsAllowed(r) && !MavFlightPhysicsOwnership.IsLegacyPhysicsAllowed(r)
                  && MavFlightPhysicsOwnership.ResolveGravityProvider(r) == MavGravityProvider.ReplacementLoadSet
                  && MavFlightPhysicsOwnership.HasExactlyOneGravitySource(r) && !MavFlightPhysicsOwnership.ViolatesExclusiveOwnership(r)
                  && MavFlightPhysicsOwnership.F15AfitResearchConfigurationId == MavF15AfitResearchIdentity.ConfigurationId,
                "O1", "F15AfitResearch: replacement writes, legacy gated off, gravity from the replacement load set only, exclusive");

            bool unchanged =
                MavFlightPhysicsOwnership.IsLegacyPhysicsAllowed(MavFlightPhysicsOwner.Legacy)
                && !MavFlightPhysicsOwnership.IsReplacementPhysicsAllowed(MavFlightPhysicsOwner.Legacy)
                && MavFlightPhysicsOwnership.IsLegacyPhysicsAllowed(MavFlightPhysicsOwner.Shadow)
                && !MavFlightPhysicsOwnership.IsReplacementPhysicsAllowed(MavFlightPhysicsOwner.Shadow)
                && !MavFlightPhysicsOwnership.IsLegacyPhysicsAllowed(MavFlightPhysicsOwner.F16Replacement)
                && MavFlightPhysicsOwnership.IsReplacementPhysicsAllowed(MavFlightPhysicsOwner.F16Replacement)
                && MavFlightPhysicsOwnership.IsLegacyPhysicsAllowed(MavFlightPhysicsOwner.Fault)
                && !MavFlightPhysicsOwnership.IsReplacementPhysicsAllowed(MavFlightPhysicsOwner.Fault)
                && MavFlightPhysicsOwnership.ResolveGravityProvider(MavFlightPhysicsOwner.F16Replacement) == MavGravityProvider.UnityRigidbody
                && MavFlightPhysicsOwnership.ResolveGravityProvider(MavFlightPhysicsOwner.Legacy) == MavGravityProvider.LegacyAeroCustomGravity
                && MavFlightPhysicsOwnership.ResolveGravityProvider(MavFlightPhysicsOwner.Shadow) == MavGravityProvider.LegacyAeroCustomGravity
                && MavFlightPhysicsOwnership.ResolveGravityProvider(MavFlightPhysicsOwner.Fault) == MavGravityProvider.LegacyAeroCustomGravity;
            bool everyModeExclusive = true;
            foreach (MavFlightPhysicsOwner m in (MavFlightPhysicsOwner[])Enum.GetValues(typeof(MavFlightPhysicsOwner)))
                everyModeExclusive &= !MavFlightPhysicsOwnership.ViolatesExclusiveOwnership(m) && MavFlightPhysicsOwnership.HasExactlyOneGravitySource(m);
            Check(unchanged && everyModeExclusive, "O2",
                "Legacy / Shadow / F16Replacement / Fault rules and gravity providers are exactly as before; every mode is exclusive with one gravity source");

            Rig probe = Rig.Build("f15rt-ownership-probe", MavF15ResearchDensityPolicy.SourceFixedDensity, MavF15ResearchGravityPolicy.SourceGravity, false);
            GameObject other = null;
            try
            {
                // Initial state first: the body's initializer refuses while an authority that has not granted
                // the replacement stack is attached, so the authority joins only after it.
                string injected;
                bool injectedOk = probe.Inject(InitialFrom(symmetric), out injected);
                MavFlightPhysicsOwnership gate = probe.AttachGate(false);
                Check(injectedOk && gate.owner == MavFlightPhysicsOwner.Legacy && !gate.allowResearchValidationOwnership
                      && !probe.body.ArmedForLiveFlight, "O3",
                    "default ownership unchanged: a new authority is Legacy with the research safety hold ON, and the body stays unarmed");
                string e1, e2, e3, e4, e5, e6, e7;
                bool holdRefused = !gate.TryEnterF15AfitResearchOwnership(probe.body, MavF15ResearchOwnershipGrant.Instance, out e1);
                gate.allowResearchValidationOwnership = true;
                bool nullGrant = !gate.TryEnterF15AfitResearchOwnership(probe.body, null, out e2);
                bool wrongGrant = !gate.TryEnterF15AfitResearchOwnership(probe.body, new WrongIdGrant(), out e3);

                string original = probe.body.activeProfile.profileId;
                string[] impostors = { MavF15ReferenceData.TargetConfigurationId, original + " ", original.ToLowerInvariant(), original + "_EXACT", "f16-morelli-clean-subsonic-v0.1" };
                int refusedIds = 0;
                probe.body.autoApplyProfileConfiguration = false;
                foreach (string id in impostors)
                {
                    probe.body.activeProfile.profileId = id;
                    string e;
                    if (!gate.TryEnterF15AfitResearchOwnership(probe.body, MavF15ResearchOwnershipGrant.Instance, out e))
                        refusedIds++;
                }
                probe.body.activeProfile.profileId = original;

                probe.profile.densityPolicy = MavF15ResearchDensityPolicy.StandardAtmosphere;
                bool noDensity = !gate.TryEnterF15AfitResearchOwnership(probe.body, MavF15ResearchOwnershipGrant.Instance, out e4);
                probe.profile.densityPolicy = MavF15ResearchDensityPolicy.SourceFixedDensity;
                probe.profile.gravityPolicy = MavF15ResearchGravityPolicy.UnityProjectGravity;
                bool noGravity = !gate.TryEnterF15AfitResearchOwnership(probe.body, MavF15ResearchOwnershipGrant.Instance, out e5);
                probe.profile.gravityPolicy = MavF15ResearchGravityPolicy.SourceGravity;

                other = new GameObject("f15rt-other-armed-body");
                MavSixDoFBody otherBody = other.AddComponent<MavSixDoFBody>();
                otherBody.simulationEnabled = true;
                bool anotherArmed = !gate.TryEnterF15AfitResearchOwnership(probe.body, MavF15ResearchOwnershipGrant.Instance, out e6);
                otherBody.simulationEnabled = false;

                MavFlightPhysicsOwner saved = gate.owner;
                gate.owner = MavFlightPhysicsOwner.F16Replacement;
                bool f16Held = !gate.TryEnterF15AfitResearchOwnership(probe.body, MavF15ResearchOwnershipGrant.Instance, out e7);
                gate.owner = saved;

                bool noStateChanged = gate.owner == MavFlightPhysicsOwner.Legacy && !probe.body.ArmedForLiveFlight;
                sb.Append("      e.g. ").AppendLine(e1);
                sb.Append("      e.g. ").AppendLine(e4);
                Check(holdRefused && nullGrant && wrongGrant && refusedIds == impostors.Length && noDensity && noGravity
                      && anotherArmed && f16Held && noStateChanged, "O4",
                    "refused: safety hold on; no / wrong-id grant; NASA 836, trailing-space, lower-case, suffixed and F-16 ids ("
                    + refusedIds + "/" + impostors.Length + "); standard density; Unity gravity; another armed body; F16Replacement "
                    + "holding physics. Every refusal left the owner Legacy and the body unarmed");

                string granted;
                bool enter = gate.TryEnterF15AfitResearchOwnership(probe.body, MavF15ResearchOwnershipGrant.Instance, out granted);
                bool armed = probe.body.ArmedForLiveFlight && gate.owner == MavFlightPhysicsOwner.F15AfitResearch;
                gate.ReturnToLegacy("probe complete");
                Check(enter && armed && !probe.body.ArmedForLiveFlight && gate.owner == MavFlightPhysicsOwner.Legacy, "O5",
                    "the exact research body with the source environment is granted - and armed by the authority itself - and "
                    + "returning to Legacy disarms it");
            }
            finally
            {
                if (other != null)
                    Object.DestroyImmediate(other);
                probe.Destroy();
            }
        }

        private sealed class WrongIdGrant : IMavResearchOwnershipGrant
        {
            public string ResearchConfigurationId
            {
                get { return MavF15ReferenceData.TargetConfigurationId; }
            }

            public bool TryGrantResearchOwnership(MavSixDoFBody body, out string reason)
            {
                reason = "a grant for the wrong configuration always says yes";
                return true;
            }
        }

        // ================================================================== the play-mode machine

        private void FixedUpdate()
        {
            if (Finished)
                return;

            try
            {
                if (runIndex >= runs.Count)
                {
                    Complete();
                    return;
                }

                Run run = runs[runIndex];
                if (phase == 0)
                    SetUp(run);
                else if (phase == 1)
                    BeginRun(run);
                else
                    Continue(run);
            }
            catch (Exception e)
            {
                Check(false, "CRASH", "the play-mode machine threw " + e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace);
                if (rig != null)
                    rig.Destroy();
                rig = null;
                runIndex++;
                phase = 0;
            }
        }

        private void SetUp(Run run)
        {
            Time.fixedDeltaTime = run.dt;
            rig = Rig.Build("f15rt-" + run.id, MavF15ResearchDensityPolicy.SourceFixedDensity, MavF15ResearchGravityPolicy.SourceGravity,
                run.kind == CaseKind.TurningHoldGyroOff);
            phase = 1;
        }

        private void BeginRun(Run run)
        {
            string reason;
            if (!rig.Inject(run.initial, out reason))
            {
                Abort(run, "initialization refused: " + reason);
                return;
            }

            MavFlightPhysicsOwnership gate = rig.AttachGate(true);
            if (!gate.TryEnterF15AfitResearchOwnership(rig.body, MavF15ResearchOwnershipGrant.Instance, out reason))
            {
                Abort(run, reason);
                return;
            }

            run.rotation0 = rig.root.transform.rotation;
            run.position0 = rig.rigidbody.position;
            run.velocity0 = rig.rigidbody.linearVelocity;
            run.angularVelocity0 = rig.rigidbody.angularVelocity;
            run.mass = rig.rigidbody.mass;
            run.inertiaPrincipal = rig.rigidbody.inertiaTensor;
            run.inertiaRotation = rig.rigidbody.inertiaTensorRotation;
            run.samples.Add(Capture(0.0));

            stepsDone = 0;
            phase = 2;
        }

        private void Continue(Run run)
        {
            stepsDone++;
            MavSixDoFBody body = rig.body;

            // Unity quantizes the stored timestep (0.02 -> 0.0199999921, 0.005 -> 0.004999993, ~1.4e-6 relative);
            // the check is that the configured dt is the one in use, not a bit-exact float compare.
            if (Mathf.Abs(Time.fixedDeltaTime - run.dt) > 1e-5f * run.dt)
                run.dtAsSpecifiedEveryStep = false;
            run.fixedDeltaTimeUsed = Time.fixedDeltaTime;

            // Loads the body computed one step ago, at the previous sample's state.
            Sample previous = run.samples[run.samples.Count - 1];
            if (body.debugLoadApplications == stepsDone)
            {
                ComputeResidual(body, ref previous);
                run.samples[run.samples.Count - 1] = previous;
            }

            if (stepsDone == 1)
            {
                run.physicsSettings = DescribePhysics(rig);
                run.appliedForceLocal0 = body.debugUnityLocalForceN;
                run.appliedTorqueLocal0 = body.debugUnityLocalTorqueNm;
                run.externalMomentAero0 = body.debugLoadSet.ExternalMomentAeroBodyNm;
                run.omegaAero0 = body.debugState.aeroBodyRatesRadSec;
                run.velocity1 = rig.rigidbody.linearVelocity;
                run.angularVelocity1 = rig.rigidbody.angularVelocity;
                run.position1 = rig.rigidbody.position;
            }

            if (run.kind == CaseKind.EnvironmentLoss)
            {
                ContinueEnvironmentLoss(run, body);
                return;
            }

            run.loadApplications = body.debugLoadApplications;
            run.refusals = body.debugRejectedEnvironmentSteps + body.debugRejectedGravityOwnershipSteps
                           + body.debugRejectedNotLiveReadyApplications + body.debugRejectedNotOwnerApplications;
            run.ownerHeldEveryStep &= rig.gate.owner == MavFlightPhysicsOwner.F15AfitResearch && body.ArmedForLiveFlight;
            run.unityGravityOffEveryStep &= !rig.rigidbody.useGravity;
            run.exactlyOneArmedEveryStep &= CountArmedBodies() == 1;

            run.samples.Add(Capture(stepsDone * (double)run.fixedDeltaTimeUsed));

            if (stepsDone >= run.steps)
            {
                run.completed = true;
                Finish(run);
            }
        }

        private void ContinueEnvironmentLoss(Run run, MavSixDoFBody body)
        {
            if (stepsDone == 10)
            {
                run.loadApplications = body.debugLoadApplications;
                rig.profile.densityPolicy = MavF15ResearchDensityPolicy.StandardAtmosphere;
            }

            if (stepsDone == 11)
                run.faultObserved = rig.gate.owner == MavFlightPhysicsOwner.Fault && !body.ArmedForLiveFlight;

            if (stepsDone > 11)
                run.applicationsAfterLoss = body.debugLoadApplications - run.loadApplications;

            if (stepsDone >= run.steps)
            {
                run.completed = true;
                Finish(run);
            }
        }

        private void Finish(Run run)
        {
            if (rig != null)
            {
                if (rig.gate != null)
                    rig.gate.ReturnToLegacy("run complete");
                rig.Destroy();
            }

            rig = null;
            runIndex++;
            phase = 0;
        }

        private void Abort(Run run, string why)
        {
            run.failure = why;
            Finish(run);
        }

        private Sample Capture(double t)
        {
            MavFlightState s = MavF15AfitResearchStateInjection.ReadBack(rig.body);
            Rigidbody rb = rig.rigidbody;
            return new Sample
            {
                t = t,
                x = StateOf(s),
                heading = s.attitude.headingRad,
                gamma = s.attitude.flightPathAngleRad,
                position = rb.position,
                velocity = rb.linearVelocity,
                angularVelocityWorld = rb.angularVelocity
            };
        }

        private static double[] StateOf(MavFlightState s)
        {
            return new double[]
            {
                s.alphaRad, s.betaRad, s.aeroBodyRatesRadSec.x, s.aeroBodyRatesRadSec.y, s.aeroBodyRatesRadSec.z,
                s.attitude.pitchAttitudeRad, s.attitude.bankAngleRad, s.trueAirspeedMps / FtToM
            };
        }

        private void ComputeResidual(MavSixDoFBody body, ref Sample sample)
        {
            MavFlightDynamicsLoadSet set = body.debugLoadSet;
            double m = rig.rigidbody.mass;
            Vector3 f = set.AppliedForceAeroBodyN;
            Vector3 w = body.debugState.aeroBodyRatesRadSec;
            Vector3 v = body.debugState.aeroBodyVelocityMps;
            double cx = (double)w.y * v.z - (double)w.z * v.y;
            double cy = (double)w.z * v.x - (double)w.x * v.z;
            double cz = (double)w.x * v.y - (double)w.y * v.x;
            double ax = f.x / m - cx, ay = f.y / m - cy, az = f.z / m - cz;
            sample.linearResidualG = Math.Sqrt(ax * ax + ay * ay + az * az) / MavF15AfitResearchRuntimeEnvironment.SourceGravityMps2;

            MavAeroReferenceGeometry g = MavF15BaumannMach06Reference.CreateReferenceGeometry();
            double qsc = (double)body.debugState.dynamicPressurePa * g.wingAreaM2 * g.meanAerodynamicChordM;
            sample.angularResidual = set.totalMomentAeroBodyNm.magnitude / qsc;
            sample.hasResidual = true;
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

        private static string DescribePhysics(Rig rig)
        {
            Rigidbody rb = rig.rigidbody;
            return "fixedDeltaTime " + Time.fixedDeltaTime.ToString("R", CultureInfo.InvariantCulture)
                + " s, captureFramerate " + Time.captureFramerate + ", maximumDeltaTime " + Time.maximumDeltaTime.ToString("R", CultureInfo.InvariantCulture)
                + ", simulationMode " + Physics.simulationMode + ", autoSyncTransforms " + Physics.autoSyncTransforms
                + ", Physics.gravity " + Physics.gravity.ToString("F3") + " (NOT used: useGravity off)"
                + ", solverIterations " + Physics.defaultSolverIterations + "/" + Physics.defaultSolverVelocityIterations
                + ", sleepThreshold " + Physics.sleepThreshold.ToString("R", CultureInfo.InvariantCulture)
                + ", defaultMaxAngularSpeed " + Physics.defaultMaxAngularSpeed.ToString("R", CultureInfo.InvariantCulture)
                + " | rigidbody: mass " + rb.mass.ToString("R", CultureInfo.InvariantCulture)
                + " kg, inertia " + rb.inertiaTensor.ToString("F1") + " rot " + rb.inertiaTensorRotation.eulerAngles.ToString("F4")
                + ", centerOfMass " + rb.centerOfMass.ToString("F3") + ", useGravity " + rb.useGravity
                + ", damping " + rb.linearDamping.ToString("R", CultureInfo.InvariantCulture) + "/" + rb.angularDamping.ToString("R", CultureInfo.InvariantCulture)
                + ", interpolation " + rb.interpolation + ", collision " + rb.collisionDetectionMode
                + ", maxAngularVelocity " + rb.maxAngularVelocity.ToString("R", CultureInfo.InvariantCulture)
                + ", sleepThreshold " + rb.sleepThreshold.ToString("R", CultureInfo.InvariantCulture)
                + ", isKinematic " + rb.isKinematic + ", colliders " + rb.GetComponents<Collider>().Length;
        }

        // ================================================================== evaluation

        private void Complete()
        {
            Time.fixedDeltaTime = savedFixedDeltaTime;
            Time.captureFramerate = savedCaptureFramerate;

            foreach (Run run in runs)
            {
                if (run.completed && run.samples.Count > 1)
                    CompareWithReference(run);
            }

            ReportMechanics();
            ReportFrameSignAudit();
            ReportHold(CaseKind.SymmetricHold, "[S] Symmetric trim hold - Table VII point " + SymmetricPoint);
            ReportHold(CaseKind.TurningHold, "[T] Turning trim hold - Table VII point " + TurningPoint + " (also the dynamic Ixz / gyro-sign case)");
            ReportConvergence();
            ReportReference();
            ReportNegativeControlAndEnvironment();

            sb.AppendLine();
            sb.Append("RESULT: ").Append(Failed == 0 ? "PASS" : "FAIL").Append("  passed=").Append(Passed).Append(" failed=").Append(Failed);
            Report = sb.ToString();
            Finished = true;
        }

        private void CompareWithReference(Run run)
        {
            double[] x0 = run.samples[0].x;
            // Integrate on the timestep Unity actually used (its stored float), so A and B share one time grid.
            double stepUsed = run.fixedDeltaTimeUsed > 0f ? run.fixedDeltaTimeUsed : run.dt;
            int every = Math.Max(1, (int)Math.Round(stepUsed / ReferenceRk4Step));
            double h = stepUsed / every;
            double stab = run.stabilatorDeg;
            MavValidationTrajectory reference = MavValidationRk4Integrator.Integrate(
                (x, xDot) => MavF15AfitResearchSourceDynamics.EvaluateStateDerivative(x, stab, xDot),
                x0, h, run.steps * every, every);

            int n = Math.Min(reference.state.Length, run.samples.Count);
            for (int i = 0; i < n; i++)
            {
                double[] a = reference.state[i];
                double[] b = run.samples[i].x;
                for (int k = 0; k < 8; k++)
                {
                    double scale = k == 7 ? x0[7] : 1.0;
                    double d = Math.Abs(b[k] - a[k]) / scale;
                    run.maxAbsDiff[k] = Math.Max(run.maxAbsDiff[k], d);
                    run.unityDrift[k] = Math.Max(run.unityDrift[k], Math.Abs(b[k] - run.equilibrium[k]) / scale);
                    run.referenceDrift[k] = Math.Max(run.referenceDrift[k], Math.Abs(a[k] - run.equilibrium[k]) / scale);
                    if (i == n - 1)
                        run.finalDiff[k] = d;
                }
            }

            // Heading rate: least-squares slope of the unwrapped nose heading; flight-path angle: mean.
            double sumT = 0, sumH = 0, sumTT = 0, sumTH = 0, gammaSum = 0;
            double unwrap = 0, last = run.samples[0].heading;
            for (int i = 0; i < run.samples.Count; i++)
            {
                double hd = run.samples[i].heading;
                double step = hd - last;
                if (step > Math.PI) unwrap -= 2 * Math.PI;
                if (step < -Math.PI) unwrap += 2 * Math.PI;
                last = hd;
                double hu = hd + unwrap;
                double t = run.samples[i].t;
                sumT += t; sumH += hu; sumTT += t * t; sumTH += t * hu;
                gammaSum += run.samples[i].gamma;
            }

            int count = run.samples.Count;
            run.headingRateMeasured = (count * sumTH - sumT * sumH) / (count * sumTT - sumT * sumT);
            run.gammaMeanMeasured = gammaSum / count;
        }

        private IEnumerable<Run> Of(CaseKind kind)
        {
            foreach (Run r in runs)
            {
                if (r.kind == kind)
                    yield return r;
            }
        }

        private void ReportMechanics()
        {
            sb.AppendLine();
            sb.AppendLine("[M] Runtime mechanics of every run (research owner, one armed body, loads every step, source environment)");
            foreach (Run run in runs)
            {
                if (run.kind == CaseKind.EnvironmentLoss)
                    continue;

                bool ok = run.completed && run.failure == null && run.loadApplications == run.steps && run.refusals == 0
                          && run.ownerHeldEveryStep && run.exactlyOneArmedEveryStep && run.unityGravityOffEveryStep && run.dtAsSpecifiedEveryStep;
                Check(ok, "M", run.id.PadRight(26) + " " + run.steps + " steps, " + run.loadApplications + " load applications, refusals "
                    + run.refusals + ", research owner + armed every step " + run.ownerHeldEveryStep + ", exactly one armed body "
                    + run.exactlyOneArmedEveryStep + ", Unity gravity off " + run.unityGravityOffEveryStep
                    + ", fixedDeltaTime " + run.fixedDeltaTimeUsed.ToString("R", CultureInfo.InvariantCulture)
                    + (run.dtAsSpecifiedEveryStep ? "" : " NOT the configured dt")
                    + (run.failure == null ? "" : " - " + run.failure));
            }

            Run first = runs.Count > 0 ? runs[0] : null;
            if (first != null && first.physicsSettings != null)
            {
                sb.AppendLine("    physics settings (first run; the dt series changes fixedDeltaTime only):");
                sb.Append("      ").AppendLine(first.physicsSettings);
            }
        }

        private void ReportFrameSignAudit()
        {
            sb.AppendLine();
            sb.AppendLine("[F] Frame / sign audit on the first PhysX step (source body axes X fwd, Y right, Z down vs Unity X right, Y up, Z fwd)");

            foreach (Run run in Of(CaseKind.TurningPerturbed))
            {
                if (!run.completed || run.samples.Count < 2)
                    continue;

                double dt = run.dt;

                // Force mapping + gravity direction: PhysX semi-implicit Euler gives dv = R0 F_local / m dt exactly,
                // where F_local already carries the research gravity; Unity's own gravity is off.
                Vector3 predictedDv = run.rotation0 * run.appliedForceLocal0 / run.mass * (float)dt;
                Vector3 measuredDv = run.velocity1 - run.velocity0;
                double forceError = (measuredDv - predictedDv).magnitude / Math.Max(1e-9, predictedDv.magnitude);

                // Position integration uses the updated velocity (semi-implicit): dx = v1 dt.
                double positionError = ((run.position1 - run.position0) - run.velocity1 * (float)dt).magnitude
                                       / Math.Max(1e-9, (run.velocity1 * (float)dt).magnitude);

                // Moment mapping: dw_world = I_world^-1 (R0 tau_local) dt, from the Rigidbody's own tensor.
                Quaternion inertiaWorld = run.rotation0 * run.inertiaRotation;
                Vector3 tauWorld = run.rotation0 * run.appliedTorqueLocal0;
                Vector3 tauPrincipal = Quaternion.Inverse(inertiaWorld) * tauWorld;
                Vector3 alphaPrincipal = new Vector3(tauPrincipal.x / run.inertiaPrincipal.x,
                    tauPrincipal.y / run.inertiaPrincipal.y, tauPrincipal.z / run.inertiaPrincipal.z);
                Vector3 predictedDw = inertiaWorld * alphaPrincipal * (float)dt;
                Vector3 measuredDw = run.angularVelocity1 - run.angularVelocity0;
                double momentError = (measuredDw - predictedDw).magnitude / Math.Max(1e-12, predictedDw.magnitude);

                // Against the SOURCE: the body's measured angular acceleration in aero body axes vs the source
                // RHS p-dot, q-dot, r-dot at the same (Unity t = 0) state. Ixz enters the source's K constants.
                Vector3 dwLocal = Quaternion.Inverse(run.rotation0) * measuredDw;
                Vector3 measuredAero = MavFlightDynamicsMath.UnityLocalAngularRateToAeroBody(dwLocal) / (float)dt;
                double[] xDot = new double[8];
                MavF15AfitResearchSourceDynamics.EvaluateStateDerivative(run.samples[0].x, run.stabilatorDeg, xDot);
                Vector3 source = new Vector3((float)xDot[2], (float)xDot[3], (float)xDot[4]);
                double sourceError = (measuredAero - source).magnitude / Math.Max(1e-12, source.magnitude);

                // Sensitivity: the same external moment through the inertia with the Ixz sign FLIPPED.
                Vector3 flipped = EulerRates(run.externalMomentAero0, run.omegaAero0, -1.0);
                Vector3 correct = EulerRates(run.externalMomentAero0, run.omegaAero0, +1.0);
                double flipSeparation = (flipped - correct).magnitude / Math.Max(1e-12, correct.magnitude);

                sb.Append("    ").Append(run.id).Append(": |dv - R F/m dt| / |dv| ").Append(forceError.ToString("E2"))
                  .Append("; |dx - v1 dt| ").Append(positionError.ToString("E2"))
                  .Append("; |dw - I^-1 tau dt| / |dw| ").Append(momentError.ToString("E2")).AppendLine();
                sb.Append("      measured (p,q,r)-dot ").Append(Fmt(measuredAero)).Append(" vs source RHS ").Append(Fmt(source))
                  .Append(": ").Append(sourceError.ToString("E2")).Append(" relative; Ixz-flipped inertia would give ")
                  .Append(Fmt(flipped)).Append(" (").Append(flipSeparation.ToString("E2")).AppendLine(" away)");

                Check(forceError < 1e-3 && positionError < 1e-3, "F1",
                    run.id + ": force mapping and gravity direction - PhysX's velocity change is exactly R F_local / m dt, with the "
                    + "research gravity inside F_local and no Unity gravity; position integrates the new velocity");
                Check(momentError < 1e-3, "F2",
                    run.id + ": moment mapping - PhysX's angular-velocity change is the Rigidbody inertia's I^-1 R tau dt");
                Check(sourceError < 2e-3 && flipSeparation > 10.0 * sourceError, "F3",
                    run.id + ": Ixz sign / gyroscopic term - the measured (p,q,r)-dot matches the SOURCE RHS to "
                    + sourceError.ToString("E1") + ", while a flipped Ixz would be " + flipSeparation.ToString("E1") + " away");
            }

            Run s = null;
            foreach (Run r in Of(CaseKind.TurningHold))
            {
                if (s == null) s = r;
            }

            if (s != null && s.completed)
            {
                double[] x = s.samples[0].x;
                double[] eq = s.equilibrium;
                double worst = 0;
                for (int k = 0; k < 7; k++)
                    worst = Math.Max(worst, Math.Abs(x[k] - eq[k]));
                worst = Math.Max(worst, Math.Abs(x[7] / eq[7] - 1.0));
                Check(worst < 2e-6 && Math.Abs(s.samples[0].heading) < 1e-6, "F4",
                    "linear-velocity, angular-velocity and Euler-attitude initialization: the body reads back the WP-3C state "
                    + "(alpha, beta, p, q, r, theta, phi, V) to " + worst.ToString("E1") + " and heading 0 at t = 0");
            }
        }

        /// <summary>
        /// w-dot = I^-1 (M - w x I w) in aero body axes, with the research inertia; ixzSign -1 flips Ixz.
        /// Validation-side mirror of the source's inertia coupling, used only to show the sensitivity.
        /// </summary>
        private static Vector3 EulerRates(Vector3 moment, Vector3 w, double ixzSign)
        {
            double k = MavF15MassReference.SlugFt2ToKgM2;
            double ix = MavF15AfitResearchMassReference.IxxSlugFt2 * k;
            double iy = MavF15AfitResearchMassReference.IyySlugFt2 * k;
            double iz = MavF15AfitResearchMassReference.IzzSlugFt2 * k;
            double ixz = MavF15AfitResearchMassReference.IxzSlugFt2 * k * ixzSign;

            // I = [ix 0 -ixz; 0 iy 0; -ixz 0 iz]
            double hx = ix * w.x - ixz * w.z, hy = iy * w.y, hz = -ixz * w.x + iz * w.z;
            double gx = w.y * hz - w.z * hy, gy = w.z * hx - w.x * hz, gz = w.x * hy - w.y * hx;
            double mx = moment.x - gx, my = moment.y - gy, mz = moment.z - gz;
            double det = ix * iz - ixz * ixz;
            return new Vector3((float)((iz * mx + ixz * mz) / det), (float)(my / iy), (float)((ixz * mx + ix * mz) / det));
        }

        private void ReportHold(CaseKind kind, string title)
        {
            sb.AppendLine();
            sb.AppendLine(title);
            foreach (Run run in Of(kind))
            {
                if (!run.completed || run.samples.Count < 2)
                {
                    Check(false, "H", run.id + " did not complete: " + run.failure);
                    continue;
                }

                Sample last = run.samples[run.samples.Count - 1];
                double maxLinear = 0, maxAngular = 0, maxOmegaHorizontal = 0, maxVerticalRateError = 0;
                double vSinGamma = run.equilibrium[7] * FtToM * Math.Sin(run.gammaPredictedRad);
                foreach (Sample smp in run.samples)
                {
                    if (smp.hasResidual)
                    {
                        maxLinear = Math.Max(maxLinear, smp.linearResidualG);
                        maxAngular = Math.Max(maxAngular, smp.angularResidual);
                    }

                    maxOmegaHorizontal = Math.Max(maxOmegaHorizontal,
                        new Vector2(smp.angularVelocityWorld.x, smp.angularVelocityWorld.z).magnitude);
                    maxVerticalRateError = Math.Max(maxVerticalRateError, Math.Abs(smp.velocity.y - vSinGamma));
                }

                sb.Append("    ").Append(run.id).Append(" over ").Append((run.steps * run.dt).ToString("F0")).AppendLine(" s:");
                sb.Append("      max |x - equilibrium|: ").AppendLine(Deviation(run.unityDrift));
                sb.Append("      final state:           ").AppendLine(StateText(last.x));
                sb.Append("      heading rate ").Append(run.headingRateMeasured.ToString("F6", CultureInfo.InvariantCulture))
                  .Append(" rad/s (WP-3C ").Append(run.headingRatePredicted.ToString("F6", CultureInfo.InvariantCulture))
                  .Append("); flight-path angle ").Append((run.gammaMeanMeasured * RadToDeg).ToString("F5", CultureInfo.InvariantCulture))
                  .Append(" deg (predicted ").Append((run.gammaPredictedRad * RadToDeg).ToString("F5", CultureInfo.InvariantCulture)).AppendLine(")");
                sb.Append("      world: vertical speed error max ").Append(maxVerticalRateError.ToString("E2"))
                  .Append(" m/s; horizontal angular velocity max ").Append(maxOmegaHorizontal.ToString("E2"))
                  .Append(" rad/s (steady turn: vertical axis only); final position ").Append(last.position.ToString("F1"))
                  .Append(" m; final velocity ").Append(last.velocity.ToString("F3")).Append(" m/s; final angular velocity ")
                  .Append(last.angularVelocityWorld.ToString("F5")).AppendLine(" rad/s");
                sb.Append("      load residuals: linear max ").Append(maxLinear.ToString("E2")).Append(" g, angular max ")
                  .Append(maxAngular.ToString("E2")).AppendLine(" qScbar; fixedDeltaTime used "
                      + run.fixedDeltaTimeUsed.ToString("R", CultureInfo.InvariantCulture) + " s");

                if (kind == CaseKind.SymmetricHold)
                {
                    double worst = 0;
                    for (int k = 0; k < 8; k++)
                        worst = Math.Max(worst, run.unityDrift[k]);
                    Check(worst <= FloatFloor && Math.Abs(run.headingRateMeasured) <= FloatFloor, "S",
                        run.id + ": the symmetric equilibrium is HELD - every state within " + worst.ToString("E1")
                        + " of it for 30 s (float floor " + FloatFloor.ToString("E0") + "), no heading change");
                }
            }

            if (kind != CaseKind.TurningHold)
                return;

            List<Run> t = new List<Run>(Of(CaseKind.TurningHold));
            if (t.Count != 3 || !t[0].completed || !t[1].completed || !t[2].completed)
                return;

            double[] headingError = new double[3], gammaError = new double[3], drift = new double[3];
            for (int i = 0; i < 3; i++)
            {
                headingError[i] = Math.Abs(t[i].headingRateMeasured - t[i].headingRatePredicted);
                gammaError[i] = Math.Abs(t[i].gammaMeanMeasured - t[i].gammaPredictedRad);
                for (int k = 0; k < 8; k++)
                    drift[i] = Math.Max(drift[i], t[i].unityDrift[k]);
            }

            sb.Append("    heading-rate error vs WP-3C ").Append(headingError[0].ToString("E2")).Append(", ").Append(headingError[1].ToString("E2"))
              .Append(", ").Append(headingError[2].ToString("E2")).Append(" rad/s; flight-path error ")
              .Append((gammaError[0] * RadToDeg).ToString("E2")).Append(", ").Append((gammaError[1] * RadToDeg).ToString("E2"))
              .Append(", ").Append((gammaError[2] * RadToDeg).ToString("E2")).Append(" deg; largest state drift ")
              .Append(drift[0].ToString("E2")).Append(", ").Append(drift[1].ToString("E2")).Append(", ").Append(drift[2].ToString("E2"))
              .AppendLine(" (dt, dt/2, dt/4)");

            Check(headingError[1] < headingError[0] && headingError[2] < headingError[1]
                  && gammaError[1] < gammaError[0] && gammaError[2] < gammaError[1]
                  && drift[1] < drift[0] && drift[2] < drift[1], "T",
                "the steady turn is HELD up to integration error: its heading rate, flight-path angle and largest state drift from the "
                + "WP-3C equilibrium all shrink at each halving of dt (phi, V, p/q/r stay on the predicted turn; world rotation about "
                + "the vertical axis only)");
        }

        private void ReportConvergence()
        {
            sb.AppendLine();
            sb.AppendLine("[D] Timestep refinement dt, dt/2, dt/4 - max over time of |Unity - source RK4| (rad, rad/s; V relative)");
            sb.AppendLine("    Judged per state: at the float floor (" + FloatFloor.ToString("E0") + ") at every dt, or shrinking at each halving "
                          + "(integration error). No target value is set; the observed order is reported.");

            foreach (CaseKind kind in new[] { CaseKind.SymmetricHold, CaseKind.TurningHold, CaseKind.SymmetricPerturbed, CaseKind.TurningPerturbed })
            {
                List<Run> series = new List<Run>(Of(kind));
                if (series.Count != 3 || !series[0].completed || !series[1].completed || !series[2].completed)
                {
                    Check(false, "D", kind + ": dt series incomplete");
                    continue;
                }

                int dominant = 0;
                for (int k = 1; k < 8; k++)
                {
                    if (series[0].maxAbsDiff[k] > series[0].maxAbsDiff[dominant])
                        dominant = k;
                }

                bool dominantMonotone = series[1].maxAbsDiff[dominant] < series[0].maxAbsDiff[dominant]
                                        && series[2].maxAbsDiff[dominant] < series[1].maxAbsDiff[dominant];
                bool allFloor = series[0].maxAbsDiff[dominant] <= FloatFloor;
                bool allConverge = dominantMonotone || allFloor;
                StringBuilder line = new StringBuilder();
                for (int k = 0; k < 8; k++)
                {
                    double e1 = series[0].maxAbsDiff[k], e2 = series[1].maxAbsDiff[k], e3 = series[2].maxAbsDiff[k];
                    bool floor = e1 <= FloatFloor && e2 <= FloatFloor && e3 <= FloatFloor;
                    bool monotone = e2 < e1 && e3 < e2;
                    bool shrinking = monotone || e3 < e1;
                    string order = e2 > 0 && e3 > 0
                        ? (Math.Log(e1 / e2, 2)).ToString("F2", CultureInfo.InvariantCulture) + "/" + (Math.Log(e2 / e3, 2)).ToString("F2", CultureInfo.InvariantCulture)
                        : "-";
                    allConverge &= floor || shrinking;
                    line.Append("      ").Append(StateNames[k].PadRight(6)).Append(e1.ToString("E2")).Append("  ").Append(e2.ToString("E2"))
                        .Append("  ").Append(e3.ToString("E2")).Append("  order ").Append(order)
                        .AppendLine((k == dominant ? "  <- dominant" : "")
                                    + (floor ? "  (float floor)" : monotone ? "" : shrinking ? "  (net decrease; non-monotone at the float32 level)" : "  NOT SHRINKING"));
                }

                sb.Append("    ").AppendLine(kind.ToString());
                sb.Append(line);
                Check(allConverge, "D", kind + ": the dominant difference (" + StateNames[dominant] + ") "
                                        + (allFloor ? "is at the float floor" : "shrinks at each halving")
                                        + " and every other state shrinks net over dt -> dt/4 or sits at the float floor - it behaves like "
                                        + "numerical integration error, not a model or runtime mismatch");
            }
        }

        private void ReportReference()
        {
            sb.AppendLine();
            sb.AppendLine("[R] WP-3E reference: A = source RHS / RK4 from Unity's own t = 0 state, B = Unity runtime, C = B - A (finest dt)");
            foreach (CaseKind kind in new[] { CaseKind.SymmetricHold, CaseKind.TurningHold, CaseKind.SymmetricPerturbed, CaseKind.TurningPerturbed })
            {
                Run run = null;
                foreach (Run r in Of(kind))
                    run = r;
                if (run == null || !run.completed)
                    continue;

                sb.Append("    ").Append(run.id).AppendLine(":");
                sb.Append("      A drift from equilibrium: ").AppendLine(Deviation(run.referenceDrift));
                sb.Append("      B drift from equilibrium: ").AppendLine(Deviation(run.unityDrift));
                sb.Append("      C = max |B - A|:          ").AppendLine(Deviation(run.maxAbsDiff));
            }
        }

        private void ReportNegativeControlAndEnvironment()
        {
            sb.AppendLine();
            sb.AppendLine("[N] Negative control and fail-closed environment");

            Run off = null, on = null;
            foreach (Run r in Of(CaseKind.TurningHoldGyroOff))
                off = r;
            foreach (Run r in Of(CaseKind.TurningHold))
            {
                if (on == null)
                    on = r;
            }

            if (off != null && on != null && off.completed && on.completed)
            {
                double onMax = Max(on.maxAbsDiff, 2, 5);
                double offMax = Max(off.maxAbsDiff, 2, 5);
                sb.Append("    turning hold at dt ").Append(on.dt.ToString("F3")).Append(", max |p,q,r - source|: gyroscopic compensation ON ")
                  .Append(onMax.ToString("E2")).Append(" rad/s, OFF ").Append(offMax.ToString("E2")).AppendLine(" rad/s");
                Check(offMax > 10.0 * onMax, "N1",
                    "without the w x (I w) compensation the steady turn departs from the source (" + (offMax / Math.Max(1e-12, onMax)).ToString("F0")
                    + "x the compensated difference): the runtime check is sensitive to the gyroscopic term, and with it the body follows the source");
            }
            else
            {
                Check(false, "N1", "negative control incomplete");
            }

            Run loss = null;
            foreach (Run r in Of(CaseKind.EnvironmentLoss))
                loss = r;
            Check(loss != null && loss.completed && loss.loadApplications == 10 && loss.faultObserved && loss.applicationsAfterLoss == 0, "N2",
                "the source density switched off mid-run: the next step the research owner faults, the body is disarmed and NO further "
                + "load is applied - never a fallback to the standard atmosphere or Unity gravity");
        }

        // ================================================================== helpers

        private static double Max(double[] v, int from, int to)
        {
            double m = 0;
            for (int i = from; i < to; i++)
                m = Math.Max(m, v[i]);
            return m;
        }

        private static string Deviation(double[] d)
        {
            StringBuilder s = new StringBuilder();
            for (int k = 0; k < 8; k++)
                s.Append(StateNames[k]).Append(' ').Append(d[k].ToString("E1")).Append(k < 7 ? ", " : "");
            return s.ToString();
        }

        private static string StateText(double[] x)
        {
            return "alpha " + (x[0] * RadToDeg).ToString("F5", CultureInfo.InvariantCulture)
                + ", beta " + (x[1] * RadToDeg).ToString("F5", CultureInfo.InvariantCulture)
                + ", p " + x[2].ToString("F6", CultureInfo.InvariantCulture) + ", q " + x[3].ToString("F6", CultureInfo.InvariantCulture)
                + ", r " + x[4].ToString("F6", CultureInfo.InvariantCulture)
                + ", theta " + (x[5] * RadToDeg).ToString("F5", CultureInfo.InvariantCulture)
                + ", phi " + (x[6] * RadToDeg).ToString("F4", CultureInfo.InvariantCulture)
                + ", V " + x[7].ToString("F4", CultureInfo.InvariantCulture) + " ft/s";
        }

        private static string Fmt(Vector3 v)
        {
            return "(" + v.x.ToString("E3") + ", " + v.y.ToString("E3") + ", " + v.z.ToString("E3") + ")";
        }

        private static MavF15ResearchRuntimeInitialState InitialFrom(MavF15ResearchEquilibrium eq)
        {
            MavF15ResearchSourceState x = eq.state;
            return new MavF15ResearchRuntimeInitialState
            {
                alphaRad = x.alphaRad, betaRad = x.betaRad, pRadSec = x.pRadSec, qRadSec = x.qRadSec, rRadSec = x.rRadSec,
                thetaRad = x.thetaRad, phiRad = x.phiRad, trueAirspeedFtPerSec = x.trueAirspeedFtPerSec,
                symmetricStabilatorDeg = eq.stabilatorDeg, altitudeM = MavF15ResearchRuntimeInitialState.DefaultAltitudeM,
                sourceNote = "Baumann Table VII point " + eq.point + " (ownership probe)"
            };
        }

        private void Check(bool condition, string id, string text)
        {
            if (condition) Passed++; else Failed++;
            sb.Append(condition ? "  PASS  " : "  FAIL  ").Append(id).Append("  ").AppendLine(text);
        }

        // ================================================================== rig

        /// <summary>
        /// A temporary research aircraft: research profile (source density + source gravity), research aero,
        /// F-15 actuator with the static hold, a neutral validation command source (no pilot), the AFIT-research
        /// control law, the research fixed thrust, and MavSixDoFBody. The ownership authority is attached only
        /// after the initial state is set, and is the only thing that arms the body.
        /// </summary>
        private sealed class Rig
        {
            public GameObject root;
            public Rigidbody rigidbody;
            public MavSixDoFBody body;
            public MavF15AfitResearchFlightDynamicsProfile profile;
            public MavF15AeroModel aero;
            public MavF15ControlActuator actuator;
            public MavManualPilotCommandSource command;
            public MavF15ControlLaw law;
            public MavF15AfitResearchFixedThrust thrust;
            public MavF15AfitResearchStaticSurfaceHold hold;
            public MavFlightPhysicsOwnership gate;

            public static Rig Build(string name, MavF15ResearchDensityPolicy density, MavF15ResearchGravityPolicy gravity, bool gyroOff)
            {
                Rig r = new Rig();
                r.root = new GameObject(name);
                r.rigidbody = r.root.AddComponent<Rigidbody>();
                r.rigidbody.interpolation = RigidbodyInterpolation.None;
                r.rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;

                r.profile = r.root.AddComponent<MavF15AfitResearchFlightDynamicsProfile>();
                r.profile.conditionMode = MavF15ResearchConditionMode.SourceReproduction;
                r.profile.densityPolicy = density;
                r.profile.gravityPolicy = gravity;

                r.aero = r.root.AddComponent<MavF15AeroModel>();
                r.aero.sourceMode = MavF15AeroSourceMode.BaumannMach06SixAxisResearch;
                r.aero.allowCrossValidationResearchModel = true;

                r.command = r.root.AddComponent<MavManualPilotCommandSource>();
                r.command.command = MavPilotCommand.Neutral;
                r.command.commandAvailable = true;
                r.command.treatAsOperationalSource = true;

                r.law = r.root.AddComponent<MavF15ControlLaw>();
                r.law.mode = MavF15FcsMode.AFITResearch;
                r.actuator = r.root.AddComponent<MavF15ControlActuator>();
                r.thrust = r.root.AddComponent<MavF15AfitResearchFixedThrust>();
                r.hold = r.root.AddComponent<MavF15AfitResearchStaticSurfaceHold>();
                r.body = r.root.AddComponent<MavSixDoFBody>();

                r.body.profileProvider = r.profile;
                r.body.aerodynamicModel = r.aero;
                r.body.controlSurfaceActuator = r.actuator;
                r.body.controlLaw = r.law;
                r.body.pilotCommandSource = r.command;
                r.body.propulsionModel = r.thrust;
                r.body.telemetry = null;
                r.body.autoApplyProfileConfiguration = true;
                r.body.applyMassPropertiesOnEnable = true;

                // The research thrust is a non-authoritative research constant; accepting it is the deliberate
                // acknowledgement the readiness gate asks for, not a claim that it is authoritative.
                r.body.acceptNonAuthoritativePropulsion = true;
                r.body.requireOperationalReadinessForLoadApplication = true;
                r.body.allowStructuralOnlyLoadApplication = false;
                if (gyroOff)
                {
                    // Negative control only: the body's own A/B switch, with its explicit acknowledgement.
                    r.body.applyBackendGyroscopicCompensation = false;
                    r.body.acknowledgeIncompleteAngularDynamicsForTesting = true;
                }

                r.law.sixDoFBody = r.body;
                r.law.actuator = r.actuator;
                r.law.commandSource = r.command;
                r.law.driveActuatorInFixedUpdate = true;
                r.actuator.sixDoFBody = r.body;
                r.hold.sixDoFBody = r.body;

                r.body.ApplyConfiguredProfile(true);
                r.body.NotifyOwnershipChanged();
                return r;
            }

            public bool Inject(MavF15ResearchRuntimeInitialState s, out string reason)
            {
                MavF15ResearchInitializationReport report = MavF15AfitResearchStateInjection.TryInitialize(body, s);
                reason = report.reason;
                return report.initialized && report.readbackWithinTolerance;
            }

            public MavFlightPhysicsOwnership AttachGate(bool allowResearch)
            {
                gate = root.AddComponent<MavFlightPhysicsOwnership>();
                gate.allowResearchValidationOwnership = allowResearch;
                body.physicsOwnership = gate;
                gate.AttachGovernedBody(body);
                body.NotifyOwnershipChanged();
                return gate;
            }

            public void Destroy()
            {
                if (root != null)
                    Object.DestroyImmediate(root);
                root = null;
            }
        }
    }
}
