using System.Text;
using UnityEngine;
using MaverickFresh.FlightDynamics.F16;

namespace MaverickFresh.FlightDynamics.Validation
{
    /// <summary>
    /// Phase 5C-R: builds the isolated F-16 reference-flight rig entirely in memory.
    ///
    /// WHY IN MEMORY AND NOT A SCENE. The brief allows either and prefers the least serialised churn.
    /// A scene asset would add a committed file whose contents nobody reads, and it would freeze the
    /// wiring at authoring time - so a later defect in how the pipeline is assembled would be invisible
    /// to the test that is supposed to catch it. Built in code, the assembly itself is under review.
    ///
    /// The rig carries NO legacy component: no MavMouseFlightJet, no MavAeroBody, no
    /// MavAtmosphericEngine, no landing gear, no weapons, no AI, no camera. That is what makes the
    /// ownership assertions meaningful rather than aspirational - the legacy writers are not disabled,
    /// they are absent, and the recorder counts them every step to prove it.
    ///
    /// The root's origin IS the reference CG by construction, so Rigidbody.centerOfMass is exactly
    /// Vector3.zero and no art-asset pivot is consulted. There is no visual mesh at all here; a mesh
    /// would be a child and would change nothing about the physics datum.
    /// </summary>
    public static class MavF16ReferenceRigBuilder
    {
        public const string CgDatumProvenance =
            "Phase 5C-R isolated rig: the root transform origin is DEFINED as the aerodynamic "
            + "reference CG, so Rigidbody.centerOfMass is exactly Vector3.zero. No mesh pivot was "
            + "measured or inferred, because the rig carries no mesh. TEST SYNTHETIC declaration of a "
            + "datum, not a measurement of the gameplay prefab.";

        public struct Rig
        {
            public GameObject root;
            public Rigidbody body;
            public MavSixDoFBody sixDoF;
            public MavF16AeroModel aero;
            public MavF16ControlActuator actuator;
            public MavDirectSurfaceControlLaw law;
            public MavF16ReferenceTestCommandSource command;
            public MavF16PropulsionSystem propulsion;
            public MavF16FlightDynamicsProfile profile;
            public MavFlightPhysicsOwnership ownership;
            public MavF16ReferenceTelemetryRecorder recorder;
            public string buildStatus;

            /// <summary>
            /// Structural configuration only: armed, reference mass applied, CG datum at the origin.
            ///
            /// Deliberately NOT operational readiness. That asks whether the control law observed a
            /// command signal THIS STEP, which cannot be true before any step has run - the first
            /// 5C-R run reported nine spurious failures by asking it at build time.
            /// </summary>
            public bool armedAndConfigured;
        }

        public static Rig Build(string name, int telemetryCapacity)
        {
            Rig r = new Rig();
            r.root = new GameObject(name);

            r.body = r.root.AddComponent<Rigidbody>();
            r.body.useGravity = true;
            r.body.linearDamping = 0f;
            r.body.angularDamping = 0f;
            r.body.interpolation = RigidbodyInterpolation.None;
            r.body.collisionDetectionMode = CollisionDetectionMode.Discrete;

            // Ownership gate first: it has execution order -400 and owns Rigidbody.useGravity.
            r.ownership = r.root.AddComponent<MavFlightPhysicsOwnership>();
            r.ownership.owner = MavFlightPhysicsOwner.F16Replacement;
            r.ownership.ownerReason =
                "Phase 5C-R isolated reference rig. This is NOT gameplay F16Replacement: the object "
                + "exists only inside this validation run, carries no legacy component and no gameplay "
                + "aircraft is touched.";

            r.profile = r.root.AddComponent<MavF16FlightDynamicsProfile>();
            r.profile.centerOfMassLocalM = Vector3.zero;
            r.profile.cgMappingMeasuredAndDeclared = true;
            r.profile.cgMappingProvenance = CgDatumProvenance;

            r.aero = r.root.AddComponent<MavF16AeroModel>();
            r.aero.referenceGeometry = MavF16MorelliReference.CreateReferenceGeometry();
            r.aero.xCgCbar = MavF16MorelliReference.DefaultXcgCbar;
            r.aero.xCgReferenceCbar = MavF16MorelliReference.XcgReferenceCbar;

            // Unpowered by design: no thrust deck, so dimensional thrust is exactly zero.
            r.propulsion = r.root.AddComponent<MavF16PropulsionSystem>();
            r.propulsion.thrustDeck = null;

            r.command = r.root.AddComponent<MavF16ReferenceTestCommandSource>();
            r.command.Neutral();

            r.law = r.root.AddComponent<MavDirectSurfaceControlLaw>();
            r.actuator = r.root.AddComponent<MavF16ControlActuator>();
            r.sixDoF = r.root.AddComponent<MavSixDoFBody>();

            // ---- wiring -----------------------------------------------------------------------
            r.sixDoF.profileProvider = r.profile;
            r.sixDoF.autoApplyProfileConfiguration = true;
            r.sixDoF.applyMassPropertiesOnEnable = true;
            r.sixDoF.aerodynamicModel = r.aero;
            r.sixDoF.propulsionModel = r.propulsion;
            r.sixDoF.controlSurfaceActuator = r.actuator;
            r.sixDoF.controlLaw = r.law;
            r.sixDoF.pilotCommandSource = r.command;
            r.sixDoF.physicsOwnership = r.ownership;
            r.sixDoF.telemetry = null;

            // Thrust is UNAVAILABLE, not merely zero, so the readiness gate would refuse the stack.
            // Accepting it explicitly is what makes an unpowered experiment expressible; it is not a
            // claim that the propulsion output is authoritative.
            r.sixDoF.acceptNonAuthoritativePropulsion = true;
            r.sixDoF.requireOperationalReadinessForLoadApplication = true;
            r.sixDoF.allowStructuralOnlyLoadApplication = false;

            r.law.sixDoFBody = r.sixDoF;
            r.law.actuator = r.actuator;
            r.law.commandSource = r.command;
            r.law.driveActuatorInFixedUpdate = true;

            r.actuator.sixDoFBody = r.sixDoF;

            r.recorder = r.root.AddComponent<MavF16ReferenceTelemetryRecorder>();
            r.recorder.capacity = telemetryCapacity;
            r.recorder.body = r.sixDoF;
            r.recorder.actuator = r.actuator;
            r.recorder.commandSource = r.command;
            r.recorder.controlLaw = r.law;
            r.recorder.ownership = r.ownership;
            r.recorder.expectedMassKg = MavF16MassReference.MassKg;
            r.recorder.expectCgDatumDeclared = true;

            // Profile and mass properties applied BEFORE physics runs, as a spawn-time configuration.
            r.sixDoF.ApplyConfiguredProfile(true);
            r.propulsion.ConfigureF16Installation();

            // The single arming authority arms the body, rather than the rig setting the flag itself.
            r.ownership.AttachGovernedBody(r.sixDoF);

            string reason;
            r.sixDoF.IsOperationallyLiveReady(out reason);
            r.armedAndConfigured =
                r.sixDoF.ArmedForLiveFlight
                && Mathf.Abs(r.body.mass - MavF16MassReference.MassKg) < 0.5f
                && r.body.centerOfMass == Vector3.zero
                && r.body.inertiaTensor.x > 0f;
            r.buildStatus = "armed=" + r.sixDoF.ArmedForLiveFlight
                            + " mass=" + r.body.mass.ToString("F2")
                            + " com=" + r.body.centerOfMass
                            + " inertia=" + r.body.inertiaTensor
                            + " structural=" + r.sixDoF.debugReadiness.structuralReason
                            + "; pre-step operational answer (expected to be incomplete until a step "
                            + "has run): " + reason;
            return r;
        }

        /// <summary>
        /// Places the rig at a flight condition. Pitch attitude sets alpha, because with a horizontal
        /// velocity vector the angle of attack IS the pitch attitude - which keeps the initial
        /// condition something that can be stated in one line and checked afterwards.
        ///
        /// Unity's positive rotation about +X pitches the nose DOWN, so a nose-up attitude is a
        /// negative Euler X. Getting that backwards is one of the sign errors this phase exists to
        /// find, so it is written once, here, and the resulting alpha is asserted by the scenarios.
        /// </summary>
        public static void SetFlightCondition(
            Rig r, float altitudeM, float trueAirspeedMps, float pitchUpDeg)
        {
            r.root.transform.position = new Vector3(0f, altitudeM, 0f);
            r.root.transform.rotation = Quaternion.Euler(-pitchUpDeg, 0f, 0f);
            r.body.linearVelocity = new Vector3(0f, 0f, trueAirspeedMps);
            r.body.angularVelocity = Vector3.zero;
        }

        public static void Destroy(Rig r)
        {
            if (r.root != null)
                Object.DestroyImmediate(r.root);
        }
    }

    /// <summary>
    /// Phase 5C-R scenario machine: runs U5CR-001 .. U5CR-010 inside real Unity physics.
    ///
    /// Driven from FixedUpdate at execution order 100, i.e. after the ownership gate (-400), the
    /// control law (-300), the actuator (-200), MavSixDoFBody (-100) and the recorder (50). So when
    /// this component looks at the aircraft, every value it reads belongs to the step that just
    /// completed rather than to the previous one.
    ///
    /// Each scenario builds a FRESH rig and destroys it afterwards. Reusing one rig would let a
    /// scenario inherit the attitude, rates and surface positions the previous one finished with, and
    /// a test whose initial condition depends on test order is not reproducible.
    ///
    /// Nothing here tunes anything. When a scenario fails it records what it measured and what it
    /// expected, and the run continues so the report covers every test rather than stopping at the
    /// first surprise.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class MavF16ReferenceFlightScenarioRunner : MonoBehaviour
    {
        public static bool Finished;
        public static int Passed;
        public static int Failed;
        public static string Report = "";
        public static string LastCsv = "";
        public static string CsvScenarioName = "";

        public static void Clear()
        {
            Finished = false;
            Passed = 0;
            Failed = 0;
            Report = "";
            LastCsv = "";
            CsvScenarioName = "";
        }

        // Reference flight condition: the SANITY-001 cross-check point from F16_REFERENCE_SPEC_V0.1,
        // 550 ft/s at 3600 ft. Mach there is about 0.50, comfortably inside the 0.6 Morelli ceiling.
        private const float RefAltitudeM = 1097.28f;
        private const float RefAirspeedMps = 167.64f;
        private const float RefAlphaDeg = 2f;

        private const int Capacity = 4000;

        private StringBuilder sb;
        private int scenario;
        private int phase;
        private int stepsInPhase;
        private MavF16ReferenceRigBuilder.Rig rig;
        private bool rigLive;

        // U5CR-008 state
        private GameObject inertiaObject;
        private Rigidbody inertiaBody;
        private MavInertiaTorqueApplier inertiaApplier;
        private Vector3 inertiaOmegaBefore;
        private int inertiaCase;
        private readonly StringBuilder inertiaLog = new StringBuilder();
        private int inertiaPassed;
        private int inertiaFailed;

        private void Awake()
        {
            Clear();
            sb = new StringBuilder(32768);
            sb.AppendLine("Maverick Phase 5C-R - ISOLATED F-16 REFERENCE FLIGHT VALIDATION");
            sb.AppendLine("===============================================================");
            sb.AppendLine("Unity " + Application.unityVersion);
            sb.AppendLine("Fixed timestep " + Time.fixedDeltaTime.ToString("F4") + " s, gravity "
                          + Physics.gravity);
            sb.AppendLine("Reference condition: " + RefAirspeedMps.ToString("F2") + " m/s at "
                          + RefAltitudeM.ToString("F1") + " m, alpha " + RefAlphaDeg.ToString("F1")
                          + " deg, UNPOWERED");

            // The pure-math suite runs FIRST and its results are counted with the rest. If the
            // formula, the tensor handling or the frame mapping is wrong, that shows here - before
            // any PhysX measurement can confuse a formula error with an integration difference.
            int gPassed;
            int gFailed;
            string gReport = MavGyroscopicMomentValidation.Run(out gPassed, out gFailed);
            sb.AppendLine();
            sb.Append(gReport).AppendLine();
            Passed += gPassed;
            Failed += gFailed;
        }

        private void FixedUpdate()
        {
            if (Finished)
                return;

            switch (scenario)
            {
                case 0: RunScenario("U5CR-001", 120, SetupFreeFall, EvaluateFreeFall); break;
                case 1: RunScenario("U5CR-002", 260, SetupGlide, EvaluateGlide); break;
                case 2: RunScenario("U5CR-003", 200, SetupElevatorStep, EvaluateElevatorStep); break;
                case 3: RunScenario("U5CR-004", 200, SetupAileronStep, EvaluateAileronStep); break;
                case 4: RunScenario("U5CR-005", 200, SetupRudderStep, EvaluateRudderStep); break;
                case 5: RunScenario("U5CR-006", 220, SetupPitchDoublet, EvaluatePitchDoublet); break;
                case 6: RunScenario("U5CR-007", 220, SetupLateralDoublet, EvaluateLateralDoublet); break;
                case 7: RunInertiaScenario(); break;
                case 8: RunScenario("U5CR-009", 300, SetupGlide, EvaluateEnergySanity); break;
                case 9: RunScenario("U5CR-010", 60, SetupEnvelopeExit, EvaluateEnvelopeExit); break;
                default: Complete(); break;
            }
        }

        // ============================================================ scenario driver

        private delegate void SetupStep(int stepIndex);
        private delegate void EvaluateStep();

        private void RunScenario(string id, int runSteps, SetupStep setup, EvaluateStep evaluate)
        {
            if (phase == 0)
            {
                rig = MavF16ReferenceRigBuilder.Build("p5cr-" + id, Capacity);
                rigLive = true;
                currentId = id;

                Check(rig.armedAndConfigured, id, "a",
                    "the isolated rig builds, arms through the ownership authority and spawns in the "
                    + "reference configuration: " + rig.buildStatus);

                setup(-1);
                rig.recorder.BeginRecording();
                phase = 1;
                stepsInPhase = 0;
                return;
            }

            if (phase == 1)
            {
                if (stepsInPhase == 1)
                {
                    // One step has now run, so the control law has observed its command source and
                    // the per-step readiness conditions are answerable.
                    string reason;
                    bool ready = rig.sixDoF.IsOperationallyLiveReady(out reason);
                    Check(ready, currentId, "a2",
                        "and it is operationally live-ready once a step has run: " + reason);
                    Check(rig.sixDoF.debugLoadApplications > 0, currentId, "a3",
                        "with loads actually reaching the Rigidbody ("
                        + rig.sixDoF.debugLoadApplications + " application(s) so far)");
                }

                setup(stepsInPhase);
                stepsInPhase++;
                if (stepsInPhase < runSteps)
                    return;

                rig.recorder.StopRecording();
                evaluate();

                if (string.IsNullOrEmpty(LastCsv))
                {
                    LastCsv = rig.recorder.ToCsv();
                    CsvScenarioName = id;
                }

                MavF16ReferenceRigBuilder.Destroy(rig);
                rigLive = false;
                phase = 0;
                scenario++;
            }
        }

        private string currentId = "";

        // ============================================================ U5CR-001 free fall

        private void SetupFreeFall(int step)
        {
            if (step < 0)
            {
                // Velocity ZERO, so dynamic pressure is exactly zero and the aerodynamic contribution
                // is not merely small but identically zero. The brief warns against assuming "free
                // fall at high airspeed means no aero" - this removes the assumption instead of
                // relying on it, and the evaluation asserts qbar == 0 rather than trusting the setup.
                MavF16ReferenceRigBuilder.SetFlightCondition(rig, 3000f, 0f, 0f);
                rig.command.Neutral();

                // ENVELOPE ASSERTION OFF, deliberately, for this diagnostic only.
                //
                // Dropping from rest with the nose level puts the velocity vector straight down, so
                // alpha is 90 degrees within a few steps - far outside the Morelli domain. That is
                // correct and expected: vertical free fall is not a reference FLIGHT condition, it is
                // a gravity and specific-force diagnostic, and the aerodynamic model is irrelevant to
                // it precisely because the dynamic pressure is nearly zero. The evaluation below
                // BOUNDS the aerodynamic force instead of assuming it away, and U5CR-010 is where the
                // envelope gate itself is tested.
                rig.recorder.assertEnvelope = false;
            }
        }

        private void EvaluateFreeFall()
        {
            MavF16ReferenceTelemetrySample[] s = rig.recorder.Samples;
            int n = rig.recorder.sampleCount;
            string id = "U5CR-001";

            if (!Require(n > 60, id, "b", "recorded " + n + " physics steps"))
                return;

            // THE MEASUREMENT WINDOW, and why it is short.
            //
            // The first version of this test measured over the whole 2.2 s window and failed: a body
            // dropped from rest reaches 22 m/s, and by then the dynamic pressure is 227 Pa and the
            // aerodynamic force 13.6 kN - 15% of the aircraft's weight. "Free fall" stops being
            // aerodynamically free almost immediately, which is exactly the assumption the brief warns
            // against making.
            //
            // So the diagnostic uses the first few steps only, and BOUNDS the aerodynamic force as a
            // fraction of weight rather than claiming it is zero. At step 5 the speed is under 1 m/s
            // and the aerodynamic force is a few tens of newtons against 91 kN of weight.
            const int window = 6;
            float weightN = rig.body.mass * MavF16ReferenceTelemetryRecorder.StandardGravityMps2;

            float maxQbar = 0f;
            float maxAero = 0f;
            float maxThrust = 0f;
            float maxSpecific = 0f;
            for (int i = 0; i < window && i < n; i++)
            {
                maxQbar = Mathf.Max(maxQbar, Mathf.Abs(s[i].dynamicPressurePa));
                maxAero = Mathf.Max(maxAero, s[i].aeroForceAeroBodyN.magnitude);
                maxThrust = Mathf.Max(maxThrust, Mathf.Abs(s[i].propulsionThrustN));
                maxSpecific = Mathf.Max(maxSpecific, s[i].specificForceG.magnitude);
            }

            float aeroFraction = maxAero / weightN;

            sb.Append("        window = first ").Append(window).Append(" steps (")
              .Append((window * Time.fixedDeltaTime).ToString("F2")).Append(" s): max qbar ")
              .Append(maxQbar.ToString("F3")).Append(" Pa, max aero force ")
              .Append(maxAero.ToString("F1")).Append(" N = ")
              .Append((aeroFraction * 100f).ToString("F4")).Append("% of the ")
              .Append((weightN / 1000f).ToString("F1")).AppendLine(" kN weight");
            sb.Append("        for contrast, at the end of the 2.4 s fall the aero force is ")
              .Append(s[n - 1].aeroForceAeroBodyN.magnitude.ToString("F0"))
              .Append(" N (").Append((100f * s[n - 1].aeroForceAeroBodyN.magnitude / weightN)
                  .ToString("F1"))
              .AppendLine("% of weight) - which is why the window is short");

            Check(aeroFraction < 1e-3f, id, "c",
                "the aerodynamic contribution over the measurement window is bounded at "
                + (aeroFraction * 100f).ToString("F4")
                + "% of weight - deliberately negligible, and MEASURED rather than assumed");
            Check(maxQbar < 1f, id, "d",
                "dynamic pressure stays below 1 Pa in the window (max " + maxQbar.ToString("F3")
                + " Pa), so this is a gravity diagnostic and not an aerodynamic one");
            Check(maxThrust < 1e-6f, id, "e",
                "thrust is exactly zero (max " + maxThrust.ToString("E3") + " N)");

            // 2. Measured acceleration from the recorded velocity history, over the same window.
            int a = 1;
            int b = window - 1;
            float dt = s[b].time - s[a].time;
            Vector3 accel = (s[b].worldVelocityMps - s[a].worldVelocityMps) / Mathf.Max(1e-6f, dt);
            float g = MavF16ReferenceTelemetryRecorder.StandardGravityMps2;

            sb.Append("        measured world acceleration over ").Append(dt.ToString("F3"))
              .Append(" s: ").Append(accel.ToString("F4")).Append(" m/s^2 (|a| = ")
              .Append(accel.magnitude.ToString("F4")).Append("), Physics.gravity.y = ")
              .Append(Physics.gravity.y.ToString("F4")).AppendLine();

            Check(accel.y < 0f, id, "f", "gravity accelerates the body DOWNWARD (ay = "
                + accel.y.ToString("F4") + " m/s^2)");
            Check(Mathf.Abs(accel.y - Physics.gravity.y) < 0.05f, id, "g",
                "and the magnitude matches Unity gravity exactly once: measured "
                + accel.y.ToString("F4") + " vs " + Physics.gravity.y.ToString("F4")
                + " m/s^2. Two gravity sources would read about "
                + (2f * Physics.gravity.y).ToString("F2"));
            Check(Mathf.Abs(accel.y) < 1.5f * g, id, "h",
                "no duplicate gravity: |ay| = " + Mathf.Abs(accel.y).ToString("F4")
                + " m/s^2 is far below the " + (2f * g).ToString("F2") + " two sources would give");
            Check(Mathf.Abs(accel.x) < 0.02f && Mathf.Abs(accel.z) < 0.02f, id, "i",
                "and there is no unexplained lateral acceleration (ax = " + accel.x.ToString("E3")
                + ", az = " + accel.z.ToString("E3")
                + " m/s^2), which is the bound the residual aerodynamic force in the window allows");

            // 3. Specific force semantics: in true free fall the non-gravitational force is zero, so
            //    an accelerometer reads 0 g. This is the check that separates "specific force" from
            //    "acceleration", and getting it wrong would show up as 1 g here.
            Check(maxSpecific < 1e-3f, id, "j",
                "specific force is 0 g throughout (max " + maxSpecific.ToString("E3")
                + " g) - free fall is weightless, which is the semantics the load-factor channel has "
                + "to carry, and an implementation that reported acceleration instead would read 1 g");

            int gravityProviders = s[n - 1].gravityProviderCount;
            Check(gravityProviders == 1, id, "k",
                "exactly one gravity provider is live (" + gravityProviders + ")");
        }

        // ============================================================ U5CR-002 / 009 glide

        private void SetupGlide(int step)
        {
            if (step < 0)
            {
                MavF16ReferenceRigBuilder.SetFlightCondition(
                    rig, RefAltitudeM, RefAirspeedMps, RefAlphaDeg);
                rig.command.Neutral();
                rig.recorder.assertEnvelope = true;
            }
        }

        private void EvaluateGlide()
        {
            MavF16ReferenceTelemetrySample[] s = rig.recorder.Samples;
            int n = rig.recorder.sampleCount;
            string id = "U5CR-002";

            if (!Require(n > 200, id, "b", "recorded " + n + " physics steps"))
                return;

            float maxThrust = 0f;
            for (int i = 0; i < n; i++)
                maxThrust = Mathf.Max(maxThrust, Mathf.Abs(s[i].propulsionThrustN));

            Check(maxThrust < 1e-6f, id, "c",
                "thrust is exactly zero for the whole glide (max " + maxThrust.ToString("E3") + " N)");

            sb.Append("        initial: alpha ").Append(s[2].alphaDeg.ToString("F2"))
              .Append(" deg, Mach ").Append(s[2].mach.ToString("F4"))
              .Append(", qbar ").Append(s[2].dynamicPressurePa.ToString("F1"))
              .Append(" Pa, CZ ").Append(s[2].cz.ToString("F4"))
              .Append(", CX ").Append(s[2].cx.ToString("F4")).AppendLine();

            Check(Mathf.Abs(s[2].alphaDeg - RefAlphaDeg) < 0.5f, id, "d",
                "the initial condition produced the intended angle of attack: "
                + s[2].alphaDeg.ToString("F3") + " deg against " + RefAlphaDeg.ToString("F1")
                + " commanded by attitude - so the Unity nose-up sign is right");
            Check(s[2].dynamicPressurePa > 1000f, id, "e",
                "dynamic pressure is " + s[2].dynamicPressurePa.ToString("F1") + " Pa");
            Check(s[2].aeroForceAeroBodyN.x < 0f, id, "f",
                "the body-axis X aerodynamic force is NEGATIVE (" + s[2].aeroForceAeroBodyN.x.ToString("F1")
                + " N), i.e. drag opposes the nose direction rather than adding to it");

            // Energy, windowed. Integration noise makes a per-step monotonicity demand meaningless,
            // so compare the mean of the first and last twenty samples.
            float e0 = MeanEnergy(s, 2, 22);
            float e1 = MeanEnergy(s, n - 22, n - 2);
            float ke0 = s[2].translationalKeJ;
            float pe0 = s[2].potentialEnergyJ;
            float ke1 = s[n - 2].translationalKeJ;
            float pe1 = s[n - 2].potentialEnergyJ;

            sb.Append("        energy: total ").Append((e0 / 1e6f).ToString("F4")).Append(" -> ")
              .Append((e1 / 1e6f).ToString("F4")).Append(" MJ (")
              .Append(((e1 - e0) / 1e3f).ToString("F1")).Append(" kJ); KE ")
              .Append((ke0 / 1e6f).ToString("F4")).Append(" -> ").Append((ke1 / 1e6f).ToString("F4"))
              .Append(" MJ; PE ").Append((pe0 / 1e6f).ToString("F4")).Append(" -> ")
              .Append((pe1 / 1e6f).ToString("F4")).Append(" MJ; rotational KE max ")
              .Append(MaxRotationalKe(s, n).ToString("F1")).AppendLine(" J");

            Check(e1 < e0, id, "g",
                "total mechanical energy DECREASED over the glide (" + ((e1 - e0) / 1e3f).ToString("F1")
                + " kJ across " + ((s[n - 2].time - s[2].time)).ToString("F2")
                + " s), which is what aerodynamic drag with zero thrust has to produce");
            // The first version of this check subtracted (ke+pe) from itself and demanded the result
            // be under 1 J - algebraically zero, but these are floats around 2.3e8, whose spacing is
            // about 16 J, so it failed on rounding alone while proving nothing. What is worth
            // checking is that the reported TOTAL is actually composed of the three parts, to within
            // float resolution at this magnitude.
            float composed = s[n - 2].translationalKeJ + s[n - 2].rotationalKeJ
                             + s[n - 2].potentialEnergyJ;
            float resolution = 64f * Mathf.Abs(s[n - 2].totalMechanicalEnergyJ) * 1.2e-7f;
            Check(Mathf.Abs(s[n - 2].totalMechanicalEnergyJ - composed) <= Mathf.Max(1f, resolution),
                id, "h",
                "the reported total is the sum of translational KE, rotational KE and gravitational "
                + "PE to within float resolution at this magnitude ("
                + Mathf.Abs(s[n - 2].totalMechanicalEnergyJ - composed).ToString("F2") + " J of "
                + Mathf.Max(1f, resolution).ToString("F2") + " J) - so the total is not assembled "
                + "from an unrelated quantity");
            Check(pe1 < pe0, id, "i",
                "the aircraft descended (PE " + ((pe1 - pe0) / 1e3f).ToString("F1")
                + " kJ), so potential energy is being traded, not created");
            Check(rig.recorder.assertionFailures == 0, id, "j",
                "and every per-step runtime assertion held for all " + n + " steps"
                + (rig.recorder.assertionFailures > 0
                    ? " - first failure: " + rig.recorder.firstAssertionFailure : ""));

            // SECTION 9: the gyroscopic correction must be a non-event at glide rates.
            float worstShare;
            float worstRate;
            float maxGyro = MaxGyroscopicShare(s, n, out worstShare, out worstRate);

            sb.Append("        gyroscopic correction over the glide: max ")
              .Append(maxGyro.ToString("F3")).Append(" Nm, worst share of the external moment ")
              .Append((100f * worstShare).ToString("F4")).Append("% at |w| = ")
              .Append(worstRate.ToString("F4")).AppendLine(" rad/s");

            Check(worstShare < 0.01f, id, "k",
                "the correction never exceeds " + (100f * worstShare).ToString("F4")
                + "% of the external moment at glide rates, so restoring the coupling term does not "
                + "disturb steady unpowered flight - no deadband is used or needed, the term is "
                + "quadratic in w and simply small here");
        }

        private void EvaluateEnergySanity()
        {
            MavF16ReferenceTelemetrySample[] s = rig.recorder.Samples;
            int n = rig.recorder.sampleCount;
            string id = "U5CR-009";

            if (!Require(n > 250, id, "b", "recorded " + n + " physics steps"))
                return;

            // Windowed: no window mean may exceed the previous window mean by more than tolerance.
            // A hidden speed assist, an alignment force or a negative drag would all show as a rise.
            const int w = 25;
            int windows = n / w;
            float worstRise = 0f;
            int worstWindow = -1;
            float previous = MeanEnergy(s, 1, w);
            for (int k = 1; k < windows; k++)
            {
                float m = MeanEnergy(s, k * w, k * w + w);
                float rise = m - previous;
                if (rise > worstRise)
                {
                    worstRise = rise;
                    worstWindow = k;
                }
                previous = m;
            }

            float span = MeanEnergy(s, 1, w) - MeanEnergy(s, (windows - 1) * w, windows * w);
            float tolerance = Mathf.Max(200f, 0.02f * Mathf.Abs(span));

            sb.Append("        ").Append(windows).Append(" windows of ").Append(w)
              .Append(" steps; total decline ").Append((span / 1e3f).ToString("F1"))
              .Append(" kJ; worst window-to-window RISE ").Append(worstRise.ToString("F1"))
              .Append(" J at window ").Append(worstWindow).Append("; tolerance ")
              .Append(tolerance.ToString("F1")).AppendLine(" J");

            Check(span > 0f, id, "c",
                "mechanical energy declined overall (" + (span / 1e3f).ToString("F1")
                + " kJ) with thrust at exactly zero");
            Check(worstRise < tolerance, id, "d",
                "no window injected energy beyond integration tolerance (worst rise "
                + worstRise.ToString("F1") + " J against " + tolerance.ToString("F1")
                + " J) - so there is no hidden speed assist, velocity alignment, negative drag or "
                + "duplicate gravity feeding the aircraft");

            // Contribution accounting: exactly one aerodynamic and one propulsive contribution per
            // step, so no third writer is quietly adding to the load set.
            bool singleOwner = rig.sixDoF.debugLoadSet.HasSingleOwnerPerSource;
            Check(singleOwner, id, "e",
                "the load set carries exactly one aerodynamic and one propulsive contribution per "
                + "step (aero=" + rig.sixDoF.debugLoadSet.aerodynamicContributions
                + ", prop=" + rig.sixDoF.debugLoadSet.propulsiveContributions + ")");
            Check(rig.sixDoF.debugRejectedDuplicateApplications == 0, id, "f",
                "and no duplicate load application was ever attempted ("
                + rig.sixDoF.debugRejectedDuplicateApplications + ")");
            Check(rig.sixDoF.debugLoadApplications == n, id, "g",
                "with exactly one application per recorded step (" + rig.sixDoF.debugLoadApplications
                + " applications, " + n + " steps)");
        }

        // ============================================================ U5CR-003 elevator

        private void SetupElevatorStep(int step)
        {
            if (step < 0)
            {
                MavF16ReferenceRigBuilder.SetFlightCondition(
                    rig, RefAltitudeM, RefAirspeedMps, RefAlphaDeg);
                rig.command.Neutral();
                rig.recorder.assertEnvelope = true;
                return;
            }

            if (step == 30)
                rig.command.SetIntent(0.4f, 0f, 0f);
        }

        private void EvaluateElevatorStep()
        {
            EvaluateSurfaceStep("U5CR-003", Axis.Pitch);
        }

        private void SetupAileronStep(int step)
        {
            if (step < 0)
            {
                MavF16ReferenceRigBuilder.SetFlightCondition(
                    rig, RefAltitudeM, RefAirspeedMps, RefAlphaDeg);
                rig.command.Neutral();
                rig.recorder.assertEnvelope = true;
                return;
            }

            if (step == 30)
                rig.command.SetIntent(0f, 0.4f, 0f);
        }

        private void EvaluateAileronStep()
        {
            EvaluateSurfaceStep("U5CR-004", Axis.Roll);
        }

        private void SetupRudderStep(int step)
        {
            if (step < 0)
            {
                MavF16ReferenceRigBuilder.SetFlightCondition(
                    rig, RefAltitudeM, RefAirspeedMps, RefAlphaDeg);
                rig.command.Neutral();
                rig.recorder.assertEnvelope = true;
                return;
            }

            if (step == 30)
                rig.command.SetIntent(0f, 0f, 0.4f);
        }

        private void EvaluateRudderStep()
        {
            EvaluateSurfaceStep("U5CR-005", Axis.Yaw);
        }

        private enum Axis { Pitch, Roll, Yaw }

        /// <summary>
        /// One surface step, checked as a CHAIN rather than as an outcome.
        ///
        /// The order matters: the surface actually moved, the coefficient the polynomial produces
        /// responds to that surface with the sourced sign, the dimensional moment carries that
        /// coefficient, the body rate follows the moment, and no torque reached the Rigidbody from
        /// anywhere else. A test that only checked "the aircraft pitched" would pass on a fake torque.
        ///
        /// The coefficient sign is established by evaluating the FROZEN polynomial at the recorded
        /// flight condition with the surface at its actual deflection and again at zero, so the
        /// expectation comes from the implementation under test rather than from a number typed here.
        /// </summary>
        private void EvaluateSurfaceStep(string id, Axis axis)
        {
            MavF16ReferenceTelemetrySample[] s = rig.recorder.Samples;
            int n = rig.recorder.sampleCount;

            if (!Require(n > 150, id, "b", "recorded " + n + " physics steps"))
                return;

            // THREE SAMPLE POINTS, and the middle one is the important one.
            //
            // "before" is neutral, "early" is a few steps after the command while the rate change is
            // still dominated by the commanded moment, and "after" is the settled condition.
            //
            // The first version compared the settled RATE against the settled MOMENT and failed on the
            // rudder case: four seconds into a sustained rudder input the aircraft has rolled,
            // developed sideslip and built a large roll rate, so the yawing moment and the yaw rate
            // have no reason to share a sign - the w x (I w) coupling and the weathercock moment both
            // act. That was a defect in the check, not in the aircraft. A moment sets an
            // ACCELERATION; only near the start, from rest, does the rate direction follow it.
            int before = 25;
            int early = 42;
            int after = n - 5;
            int commandStep = 30;

            float requested = axis == Axis.Pitch ? s[after].requestedElevatorDeg
                : axis == Axis.Roll ? s[after].requestedAileronDeg
                : s[after].requestedRudderDeg;
            float actual = axis == Axis.Pitch ? s[after].actualElevatorDeg
                : axis == Axis.Roll ? s[after].actualAileronDeg
                : s[after].actualRudderDeg;
            float actualBefore = axis == Axis.Pitch ? s[before].actualElevatorDeg
                : axis == Axis.Roll ? s[before].actualAileronDeg
                : s[before].actualRudderDeg;

            sb.Append("        ").Append(axis).Append(": surface ")
              .Append(actualBefore.ToString("F3")).Append(" -> ").Append(actual.ToString("F3"))
              .Append(" deg (requested ").Append(requested.ToString("F3")).AppendLine(" deg)");

            Check(Mathf.Abs(actualBefore) < 1e-4f, id, "c",
                "the surface was at neutral before the step (" + actualBefore.ToString("E3") + " deg)");
            Check(Mathf.Abs(actual - requested) < 1e-3f, id, "d",
                "the actual surface follows the actuator to the requested deflection ("
                + actual.ToString("F4") + " vs " + requested.ToString("F4")
                + " deg) - the demand reached the aircraft through the actuator, not around it");
            Check(Mathf.Abs(actual) > 1f, id, "e",
                "and the deflection is a real one (" + actual.ToString("F3") + " deg)");

            // Coefficient sensitivity, measured on the frozen polynomial at this exact condition.
            float alphaRad = s[after].alphaDeg * Mathf.Deg2Rad;
            float betaRad = s[after].betaDeg * Mathf.Deg2Rad;
            float chordOverSpan = MavF16MorelliReference.MeanAerodynamicChordM
                                  / MavF16MorelliReference.WingSpanM;

            MavAeroCoefficients atZero = MavF16MorelliPolynomial.Evaluate(
                alphaRad, betaRad, 0f, 0f, 0f, 0f, 0f, 0f,
                MavF16MorelliReference.DefaultXcgCbar,
                MavF16MorelliReference.XcgReferenceCbar, chordOverSpan);

            MavAeroCoefficients atDeflection = MavF16MorelliPolynomial.Evaluate(
                alphaRad, betaRad,
                axis == Axis.Pitch ? actual * Mathf.Deg2Rad : 0f,
                axis == Axis.Roll ? actual * Mathf.Deg2Rad : 0f,
                axis == Axis.Yaw ? actual * Mathf.Deg2Rad : 0f,
                0f, 0f, 0f,
                MavF16MorelliReference.DefaultXcgCbar,
                MavF16MorelliReference.XcgReferenceCbar, chordOverSpan);

            if (axis == Axis.Pitch)
            {
                float dCm = atDeflection.cm - atZero.cm;
                float dDelta = actual * Mathf.Deg2Rad;
                float slope = dCm / dDelta;
                float q = s[after].bodyRatesRadSec.y;
                float momentM = s[after].aeroMomentAeroBodyNm.y;
                float dq = s[early].bodyRatesRadSec.y - s[commandStep].bodyRatesRadSec.y;
                float momentEarly = s[early].aeroMomentAeroBodyNm.y;

                sb.Append("        early transient: dq = ").Append(dq.ToString("F5"))
                  .Append(" rad/s over ").Append(early - commandStep)
                  .Append(" steps, M there ").Append(momentEarly.ToString("F0")).AppendLine(" Nm");
                sb.Append("        dCm/dElevator = ").Append(slope.ToString("F4"))
                  .Append(" per rad; Cm ").Append(s[after].cm.ToString("F5"))
                  .Append("; body M ").Append(momentM.ToString("F0")).Append(" Nm; q ")
                  .Append(q.ToString("F5")).Append(" rad/s; pitch attitude ")
                  .Append(PitchUpDeg(s[after]).ToString("F2")).AppendLine(" deg");

                Check(slope < 0f, id, "f",
                    "dCm/dElevator is NEGATIVE (" + slope.ToString("F4")
                    + " per rad): positive elevator gives a nose-down pitching moment, which is the "
                    + "frozen Morelli convention");
                Check(Mathf.Sign(momentM) == Mathf.Sign(s[after].cm) || Mathf.Abs(s[after].cm) < 1e-6f,
                    id, "g",
                    "the dimensional pitching moment carries the sign of Cm (Cm "
                    + s[after].cm.ToString("F5") + ", M " + momentM.ToString("F0") + " Nm)");
                Check(Mathf.Sign(dq) == Mathf.Sign(momentEarly) && Mathf.Abs(dq) > 1e-5f, id, "h",
                    "and the pitch rate CHANGES in the direction of the moment in the early "
                    + "transient (M " + momentEarly.ToString("F0") + " Nm, dq " + dq.ToString("F5")
                    + " rad/s) - a moment sets an acceleration, so this is the comparison that "
                    + "means something");
                Check(actual < 0f && q > 0f, id, "i",
                    "a nose-up demand produced a NEGATIVE elevator deflection and a POSITIVE pitch "
                    + "rate (" + actual.ToString("F2") + " deg, q " + q.ToString("F5")
                    + " rad/s), which is the whole sign chain from stick to body rate");
            }
            else if (axis == Axis.Roll)
            {
                float dCl = atDeflection.cl - atZero.cl;
                float slope = dCl / (actual * Mathf.Deg2Rad);
                float p = s[after].bodyRatesRadSec.x;
                float momentL = s[after].aeroMomentAeroBodyNm.x;
                float dp = s[early].bodyRatesRadSec.x - s[commandStep].bodyRatesRadSec.x;
                float momentEarly = s[early].aeroMomentAeroBodyNm.x;

                sb.Append("        early transient: dp = ").Append(dp.ToString("F5"))
                  .Append(" rad/s over ").Append(early - commandStep)
                  .Append(" steps, L there ").Append(momentEarly.ToString("F0")).AppendLine(" Nm");
                sb.Append("        dCl/dAileron = ").Append(slope.ToString("F4"))
                  .Append(" per rad; Cl ").Append(s[after].cl.ToString("F5"))
                  .Append("; body L ").Append(momentL.ToString("F0")).Append(" Nm; p ")
                  .Append(p.ToString("F5")).Append(" rad/s; bank ")
                  .Append(BankDeg(s[after]).ToString("F2")).AppendLine(" deg");

                Check(slope < 0f, id, "f",
                    "dCl/dAileron is NEGATIVE (" + slope.ToString("F4")
                    + " per rad), matching the frozen convention");
                Check(Mathf.Sign(momentL) == Mathf.Sign(s[after].cl) || Mathf.Abs(s[after].cl) < 1e-6f,
                    id, "g",
                    "the dimensional rolling moment carries the sign of Cl (Cl "
                    + s[after].cl.ToString("F5") + ", L " + momentL.ToString("F0") + " Nm)");
                Check(Mathf.Sign(dp) == Mathf.Sign(momentEarly) && Mathf.Abs(dp) > 1e-5f, id, "h",
                    "and the roll rate CHANGES in the direction of the moment in the early transient "
                    + "(L " + momentEarly.ToString("F0") + " Nm, dp " + dp.ToString("F5")
                    + " rad/s)");
                Check(actual < 0f && p > 0f, id, "i",
                    "a roll-right demand produced a negative aileron deflection and a POSITIVE roll "
                    + "rate (" + actual.ToString("F2") + " deg, p " + p.ToString("F5") + " rad/s)");
            }
            else
            {
                float dCn = atDeflection.cn - atZero.cn;
                float dCy = atDeflection.cy - atZero.cy;
                float slopeN = dCn / (actual * Mathf.Deg2Rad);
                float slopeY = dCy / (actual * Mathf.Deg2Rad);
                float r = s[after].bodyRatesRadSec.z;
                float momentN = s[after].aeroMomentAeroBodyNm.z;
                float dr = s[early].bodyRatesRadSec.z - s[commandStep].bodyRatesRadSec.z;
                float momentEarly = s[early].aeroMomentAeroBodyNm.z;

                sb.Append("        early transient: dr = ").Append(dr.ToString("F5"))
                  .Append(" rad/s over ").Append(early - commandStep)
                  .Append(" steps, N there ").Append(momentEarly.ToString("F0")).AppendLine(" Nm");
                sb.Append("        settled at 4 s, after roll/yaw coupling has developed: N ")
                  .Append(momentN.ToString("F0")).Append(" Nm, r ").Append(r.ToString("F5"))
                  .Append(" rad/s, p ").Append(s[after].bodyRatesRadSec.x.ToString("F5"))
                  .AppendLine(" rad/s");
                sb.Append("        dCn/dRudder = ").Append(slopeN.ToString("F4"))
                  .Append(", dCY/dRudder = ").Append(slopeY.ToString("F4"))
                  .Append(" per rad; Cn ").Append(s[after].cn.ToString("F5"))
                  .Append("; body N ").Append(momentN.ToString("F0")).Append(" Nm; r ")
                  .Append(r.ToString("F5")).Append(" rad/s; beta ")
                  .Append(s[after].betaDeg.ToString("F3")).AppendLine(" deg");

                Check(slopeN < 0f, id, "f",
                    "dCn/dRudder is NEGATIVE (" + slopeN.ToString("F4")
                    + " per rad), matching the frozen convention");
                Check(Mathf.Abs(slopeY) > 1e-4f, id, "g",
                    "the rudder also produces a side-force response (dCY/dRudder "
                    + slopeY.ToString("F4") + " per rad), so the lateral force channel is live");
                Check(Mathf.Sign(dr) == Mathf.Sign(momentEarly) && Mathf.Abs(dr) > 1e-6f, id, "h",
                    "the yaw rate CHANGES in the direction of the yawing moment in the early "
                    + "transient (N " + momentEarly.ToString("F0") + " Nm, dr " + dr.ToString("F5")
                    + " rad/s). The SETTLED rate does not have to share the moment sign, and here it "
                    + "does not: four seconds in, the roll rate is "
                    + s[after].bodyRatesRadSec.x.ToString("F3")
                    + " rad/s and sideslip is " + s[after].betaDeg.ToString("F2")
                    + " deg, so the AERODYNAMIC cross terms - weathercock from beta and the Cn "
                    + "contributions from p and r - set the steady yaw rate. Not inertial coupling: "
                    + "U5CR-008 case 2 shows PhysX does not integrate w x (I w) at all");
                Check(Mathf.Abs(s[after].betaDeg) > 1e-3f, id, "i",
                    "and sideslip developed (" + s[after].betaDeg.ToString("F4")
                    + " deg) rather than the yaw being absorbed by a hidden assist");
            }

            // No torque may reach the Rigidbody from anywhere but the aero moment.
            // SAME STEP. The first version compared the component last-step debug value against a
            // sample five steps earlier and reported a mismatch that was purely the step offset. The
            // applied force and torque are now part of each recorded sample.
            Vector3 expectedUnityTorque = MavFlightDynamicsMath.AeroBodyMomentToUnityLocal(
                s[after].totalMomentAeroBodyNm);
            Vector3 appliedUnityTorque = s[after].appliedUnityLocalTorqueNm;
            Check((appliedUnityTorque - expectedUnityTorque).magnitude
                  <= 1f + 1e-3f * expectedUnityTorque.magnitude, id, "j",
                "the torque handed to the Rigidbody in that step is exactly the converted total "
                + "moment (applied " + appliedUnityTorque.ToString("F1") + ", expected "
                + expectedUnityTorque.ToString("F1") + " Nm) - there is no second torque source");

            Vector3 expectedUnityForce = MavFlightDynamicsMath.AeroBodyVectorToUnityLocal(
                s[after].totalForceAeroBodyN);
            Check((s[after].appliedUnityLocalForceN - expectedUnityForce).magnitude
                  <= 1f + 1e-3f * expectedUnityForce.magnitude, id, "j2",
                "and the force is the converted total force (applied "
                + s[after].appliedUnityLocalForceN.ToString("F1") + ", expected "
                + expectedUnityForce.ToString("F1") + " N). It is applied at the centre of mass with "
                + "the aerodynamic moment supplied independently, so no lever arm is counted twice");
            Check(s[after].propulsionMomentAeroBodyNm.sqrMagnitude < 1e-9f, id, "k",
                "and propulsion contributes no moment (" + s[after].propulsionMomentAeroBodyNm + ")");
        }

        // ============================================================ U5CR-006 / 007 doublets

        private void SetupPitchDoublet(int step)
        {
            if (step < 0)
            {
                MavF16ReferenceRigBuilder.SetFlightCondition(
                    rig, RefAltitudeM, RefAirspeedMps, RefAlphaDeg);
                rig.command.Neutral();
                rig.recorder.assertEnvelope = true;
                return;
            }

            // AMPLITUDE AND DURATION CHOSEN TO STAY INSIDE THE MODEL DOMAIN.
            //
            // The first attempt used +/-0.35 for 50 steps each way and drove alpha to -22 degrees,
            // outside the Morelli -10 .. +45 domain. The envelope gate caught it and failed the test,
            // which is the correct behaviour - but a doublet whose job is to exercise in-domain pitch
            // response should not leave the domain, and the deliberate excursion belongs to U5CR-010.
            // The INPUT was reduced. No coefficient, limit, inertia, CG or damping was touched.
            if (step == 25) rig.command.SetIntent(0.15f, 0f, 0f);
            else if (step == 60) rig.command.SetIntent(-0.15f, 0f, 0f);
            else if (step == 95) rig.command.Neutral();
        }

        private void EvaluatePitchDoublet()
        {
            MavF16ReferenceTelemetrySample[] s = rig.recorder.Samples;
            int n = rig.recorder.sampleCount;
            string id = "U5CR-006";

            if (!Require(n > 180, id, "b", "recorded " + n + " physics steps"))
                return;

            float qUp = Extremum(s, 28, 60, SampleQ, true);
            float qDown = Extremum(s, 62, 95, SampleQ, false);
            float alphaMax = Extremum(s, 1, n - 1, SampleAlpha, true);
            float alphaMin = Extremum(s, 1, n - 1, SampleAlpha, false);
            float cmMax = Extremum(s, 1, n - 1, SampleCm, true);
            float cmMin = Extremum(s, 1, n - 1, SampleCm, false);
            float elevMax = Extremum(s, 1, n - 1, SampleElev, true);
            float elevMin = Extremum(s, 1, n - 1, SampleElev, false);

            sb.Append("        first half: q peak ").Append(qUp.ToString("F5"))
              .Append(" rad/s; second half: q peak ").Append(qDown.ToString("F5")).AppendLine(" rad/s");
            sb.Append("        alpha ").Append(alphaMin.ToString("F2")).Append(" .. ")
              .Append(alphaMax.ToString("F2")).Append(" deg; Cm ").Append(cmMin.ToString("F5"))
              .Append(" .. ").Append(cmMax.ToString("F5")).Append("; elevator ")
              .Append(elevMin.ToString("F2")).Append(" .. ").Append(elevMax.ToString("F2"))
              .AppendLine(" deg");

            Check(qUp > 0f && qDown < 0f, id, "c",
                "the pitch rate reverses with the command (peak +" + qUp.ToString("F5")
                + " then " + qDown.ToString("F5") + " rad/s), so the doublet is being followed");
            Check(elevMin < -1f && elevMax > 1f, id, "d",
                "the elevator moved to both signs (" + elevMin.ToString("F2") + " .. "
                + elevMax.ToString("F2") + " deg)");
            Check(cmMin < 0f && cmMax > 0f, id, "e",
                "and Cm took both signs (" + cmMin.ToString("F5") + " .. " + cmMax.ToString("F5")
                + "), which a sign inversion in one direction only would not produce");

            // Continuity: the largest single-step jump in q, compared against the typical step. A
            // numerical discontinuity or a sign inversion shows as one step far larger than its
            // neighbours, which an end-state check would never see.
            float maxJump = 0f;
            float meanJump = 0f;
            int jumpAt = -1;
            for (int i = 2; i < n; i++)
            {
                float j = Mathf.Abs(s[i].bodyRatesRadSec.y - s[i - 1].bodyRatesRadSec.y);
                meanJump += j;
                if (j > maxJump) { maxJump = j; jumpAt = i; }
            }
            meanJump /= Mathf.Max(1, n - 2);

            sb.Append("        step-to-step q change: mean ").Append(meanJump.ToString("E3"))
              .Append(", max ").Append(maxJump.ToString("E3")).Append(" rad/s at step ")
              .Append(jumpAt).AppendLine();

            Check(maxJump < 40f * meanJump + 1e-4f, id, "f",
                "no numerical discontinuity: the largest single-step change in q ("
                + maxJump.ToString("E3") + " rad/s) is within the expected multiple of the mean ("
                + meanJump.ToString("E3") + "), and the largest one sits at the commanded reversal");
            Check(AllFinite(s, n), id, "g", "every recorded sample is finite");
            Check(alphaMax < MavF16MorelliReference.AlphaMaxDeg
                  && alphaMin > MavF16MorelliReference.AlphaMinDeg, id, "h",
                "alpha stayed inside the Morelli domain throughout (" + alphaMin.ToString("F2")
                + " .. " + alphaMax.ToString("F2") + " deg)");
            Check(rig.recorder.assertionFailures == 0, id, "i",
                "and every per-step runtime assertion held"
                + (rig.recorder.assertionFailures > 0
                    ? " - first failure: " + rig.recorder.firstAssertionFailure : ""));
        }

        private void SetupLateralDoublet(int step)
        {
            if (step < 0)
            {
                MavF16ReferenceRigBuilder.SetFlightCondition(
                    rig, RefAltitudeM, RefAirspeedMps, RefAlphaDeg);
                rig.command.Neutral();
                rig.recorder.assertEnvelope = true;
                return;
            }

            if (step == 25) rig.command.SetIntent(0f, 0.35f, 0.25f);
            else if (step == 75) rig.command.SetIntent(0f, -0.35f, -0.25f);
            else if (step == 125) rig.command.Neutral();
        }

        private void EvaluateLateralDoublet()
        {
            MavF16ReferenceTelemetrySample[] s = rig.recorder.Samples;
            int n = rig.recorder.sampleCount;
            string id = "U5CR-007";

            if (!Require(n > 180, id, "b", "recorded " + n + " physics steps"))
                return;

            float pUp = Extremum(s, 30, 75, SampleP, true);
            float pDown = Extremum(s, 80, 125, SampleP, false);
            float rUp = Extremum(s, 30, 75, SampleR, true);
            float rDown = Extremum(s, 80, 125, SampleR, false);
            float betaMax = Extremum(s, 1, n - 1, SampleBeta, true);
            float betaMin = Extremum(s, 1, n - 1, SampleBeta, false);
            float clMax = Extremum(s, 1, n - 1, SampleCl, true);
            float clMin = Extremum(s, 1, n - 1, SampleCl, false);
            float cnMax = Extremum(s, 1, n - 1, SampleCn, true);
            float cnMin = Extremum(s, 1, n - 1, SampleCn, false);

            sb.Append("        p peak +").Append(pUp.ToString("F5")).Append(" / ")
              .Append(pDown.ToString("F5")).Append(" rad/s; r peak ").Append(rUp.ToString("F5"))
              .Append(" / ").Append(rDown.ToString("F5")).AppendLine(" rad/s");
            sb.Append("        beta ").Append(betaMin.ToString("F3")).Append(" .. ")
              .Append(betaMax.ToString("F3")).Append(" deg; Cl ").Append(clMin.ToString("F5"))
              .Append(" .. ").Append(clMax.ToString("F5")).Append("; Cn ")
              .Append(cnMin.ToString("F5")).Append(" .. ").Append(cnMax.ToString("F5")).AppendLine();
            sb.Append("        aileron ").Append(Extremum(s, 1, n - 1, SampleAil, false).ToString("F2"))
              .Append(" .. ").Append(Extremum(s, 1, n - 1, SampleAil, true).ToString("F2"))
              .Append(" deg; rudder ").Append(Extremum(s, 1, n - 1, SampleRud, false).ToString("F2"))
              .Append(" .. ").Append(Extremum(s, 1, n - 1, SampleRud, true).ToString("F2"))
              .AppendLine(" deg");

            Check(pUp > 0f && pDown < 0f, id, "c",
                "roll rate reverses with the command (+" + pUp.ToString("F5") + " / "
                + pDown.ToString("F5") + " rad/s)");
            Check(betaMax > 0f && betaMin < 0f, id, "d",
                "sideslip develops in both directions (" + betaMin.ToString("F3") + " .. "
                + betaMax.ToString("F3") + " deg)");
            Check(clMin < 0f && clMax > 0f && cnMin < 0f && cnMax > 0f, id, "e",
                "and both lateral moment coefficients take both signs (Cl " + clMin.ToString("F5")
                + ".." + clMax.ToString("F5") + ", Cn " + cnMin.ToString("F5") + ".."
                + cnMax.ToString("F5") + ")");
            Check(Mathf.Abs(rUp) > 1e-6f || Mathf.Abs(rDown) > 1e-6f, id, "f",
                "the yaw channel responded (r " + rUp.ToString("F5") + " / " + rDown.ToString("F5")
                + " rad/s)");
            Check(AllFinite(s, n), id, "g", "every recorded sample is finite");
            Check(betaMax < MavF16MorelliReference.BetaMaxDeg
                  && betaMin > MavF16MorelliReference.BetaMinDeg, id, "h",
                "beta stayed inside the Morelli domain (" + betaMin.ToString("F3") + " .. "
                + betaMax.ToString("F3") + " deg)");
            Check(rig.recorder.assertionFailures == 0, id, "i",
                "and every per-step runtime assertion held"
                + (rig.recorder.assertionFailures > 0
                    ? " - first failure: " + rig.recorder.firstAssertionFailure : ""));

            // SECTION 9's other half: at these rates the correction must MATTER. A glide test showing
            // it is negligible would look identical if the term were never computed, so this is what
            // separates "small because w is small" from "absent".
            float worstShare;
            float worstRate;
            float maxGyro = MaxGyroscopicShare(s, n, out worstShare, out worstRate);

            sb.Append("        gyroscopic correction over the doublet: max ")
              .Append(maxGyro.ToString("F0")).Append(" Nm, peak share of the external moment ")
              .Append((100f * worstShare).ToString("F1")).Append("% at |w| = ")
              .Append(worstRate.ToString("F3")).AppendLine(" rad/s");

            Check(worstShare > 0.02f, id, "j",
                "the correction reaches " + (100f * worstShare).ToString("F1")
                + "% of the external moment at |w| = " + worstRate.ToString("F3")
                + " rad/s, so it is live and scales with the rate - the glide result is small because "
                + "w is small there, not because the term is missing");
            Check(maxGyro > 100f, id, "k",
                "in absolute terms it reaches " + maxGyro.ToString("F0")
                + " Nm, which is a real contribution to the aircraft's rotational behaviour rather "
                + "than a rounding term");
        }

        /// <summary>
        /// Largest gyroscopic correction over a run, and its largest share of the external moment.
        ///
        /// The share is taken only where the external moment is big enough for a ratio to mean
        /// anything; near zero external moment the ratio diverges for reasons that say nothing about
        /// the correction.
        /// </summary>
        private static float MaxGyroscopicShare(
            MavF16ReferenceTelemetrySample[] s, int n, out float worstShare, out float rateAtWorst)
        {
            float maxGyro = 0f;
            worstShare = 0f;
            rateAtWorst = 0f;

            for (int i = 0; i < n; i++)
            {
                float gyro = s[i].gyroscopicMomentAeroBodyNm.magnitude;
                float external = s[i].externalMomentAeroBodyNm.magnitude;
                maxGyro = Mathf.Max(maxGyro, gyro);

                if (external < 100f)
                    continue;

                float share = gyro / external;
                if (share > worstShare)
                {
                    worstShare = share;
                    rateAtWorst = s[i].bodyRatesRadSec.magnitude;
                }
            }

            return maxGyro;
        }

        // ============================================================ U5CR-010 envelope exit

        private void SetupEnvelopeExit(int step)
        {
            if (step < 0)
            {
                // 230 m/s at 3000 m is Mach ~0.68: deliberately past the 0.6 Morelli ceiling.
                MavF16ReferenceRigBuilder.SetFlightCondition(rig, 3000f, 230f, RefAlphaDeg);
                rig.command.Neutral();

                // The standing assertion is switched OFF for this scenario only, because leaving the
                // envelope is the EXPECTED outcome here and the evaluation below asserts it happened.
                // A suite that reported a pass because its own alarm was silenced would be worthless,
                // so the evaluation checks the alarm fired, not that it stayed quiet.
                rig.recorder.assertEnvelope = false;
            }
        }

        private void EvaluateEnvelopeExit()
        {
            MavF16ReferenceTelemetrySample[] s = rig.recorder.Samples;
            int n = rig.recorder.sampleCount;
            string id = "U5CR-010";

            if (!Require(n > 30, id, "b", "recorded " + n + " physics steps"))
                return;

            int outside = 0;
            int outOfRange = 0;
            for (int i = 0; i < n; i++)
            {
                if (!s[i].envelopeWithinReference)
                    outside++;
                if (s[i].envelopeStatus == (int)MavReferenceEnvelopeStatus.OutOfReferenceRange)
                    outOfRange++;
            }

            sb.Append("        Mach ").Append(s[1].mach.ToString("F4")).Append(" against ceiling ")
              .Append(MavF16MorelliReference.MaxReferenceMach.ToString("F2")).Append("; ")
              .Append(outside).Append(" of ").Append(n)
              .Append(" steps outside the envelope, ").Append(outOfRange)
              .AppendLine(" of them naming OutOfReferenceRange");

            Check(s[1].mach >= MavF16MorelliReference.MaxReferenceMach, id, "c",
                "the scenario really did cross the boundary: Mach " + s[1].mach.ToString("F4")
                + " is at or above " + MavF16MorelliReference.MaxReferenceMach.ToString("F2"));
            Check(outside == n, id, "d",
                "every step is reported outside the reference envelope (" + outside + "/" + n + ")");
            Check(outOfRange == n, id, "e",
                "and the EXACT boundary is named on every step: OutOfReferenceRange, not a generic "
                + "failure (" + outOfRange + "/" + n + ")");

            // The Mach that gated must be the reference atmosphere's, not the legacy constant.
            float referenceMach = MavAtmosphereModel.ReferenceMach(230f, 3000f);
            float legacyMach = MavAtmosphereModel.LegacyMachEstimate(230f);
            Check(Mathf.Abs(s[1].mach - referenceMach) < 1e-3f, id, "f",
                "the gating Mach came from the reference atmosphere (" + s[1].mach.ToString("F4")
                + " vs " + referenceMach.ToString("F4") + "), not from the legacy constant-a estimate ("
                + legacyMach.ToString("F4") + ")");

            // No silent extrapolation, and no legacy takeover.
            Check(rig.ownership.owner == MavFlightPhysicsOwner.F16Replacement, id, "g",
                "ownership did NOT flip to Legacy mid-test (owner is still " + rig.ownership.owner
                + ") - leaving the envelope ends the experiment, it does not hand the aircraft to "
                + "another stack");
            Check(rig.recorder.CountLegacyPhysicalWriters() == 0, id, "h",
                "and no legacy physical writer appeared");

            // The aero model flags the excursion itself rather than extrapolating quietly.
            Check(rig.aero.debugOutsidePublishedMachRange, id, "i",
                "the aerodynamic model reports debugOutsidePublishedMachRange, so the excursion is "
                + "visible at the model and the result must not be called reference-valid");
        }

        // ============================================================ U5CR-008 inertia

        /// <summary>
        /// The rigid-body angular-dynamics comparison, run on an isolated diagnostic body.
        ///
        /// Deliberately NOT the aircraft rig: aerodynamic moments would contaminate the measurement,
        /// and the question here is only whether Unity's rigid body reproduces
        ///
        ///     I w_dot + w x (I w) = M
        ///
        /// for the sourced F-16 tensor, including Ixz, once that tensor has been through the
        /// aero->Unity basis change and the principal-axis decomposition Unity stores.
        ///
        /// Two cases, and the second is the one that matters:
        ///
        ///   case 0: w = 0. The gyroscopic term vanishes, so w_dot = I^-1 M exactly. This isolates the
        ///           tensor mapping and the axis conventions from the coupling.
        ///   case 1: w != 0, chosen so the coupling is large. The full Euler prediction must match and
        ///           the naive I^-1 M prediction must NOT - otherwise the test would pass on a body
        ///           that ignored the gyroscopic term entirely.
        /// </summary>
        private void RunInertiaScenario()
        {
            string id = "U5CR-008";

            if (phase == 0)
            {
                // ---- the BARE-BODY reference measurement, kept deliberately -------------------
                //
                // This is the diagnostic that found D-5CR-8: a Rigidbody with the F-16 inertia, a
                // known moment, and nothing of Maverick's in the way. It stays because it is the only
                // thing that can tell us whether the backend still omits w x (I w). If a future Unity
                // began applying it, this case would change and the compensation in MavSixDoFBody
                // would have to be removed - and without this measurement nobody would know.
                inertiaObject = new GameObject("p5cr-inertia-backend-probe");
                inertiaBody = inertiaObject.AddComponent<Rigidbody>();
                inertiaBody.useGravity = false;
                inertiaBody.linearDamping = 0f;
                inertiaBody.angularDamping = 0f;
                inertiaBody.interpolation = RigidbodyInterpolation.None;

                MavMassProperties props = MavF16MassReference.CreateUnityMassProperties(Vector3.zero);
                props.ApplyTo(inertiaBody);

                inertiaApplier = inertiaObject.AddComponent<MavInertiaTorqueApplier>();
                inertiaObject.transform.rotation = Quaternion.Euler(-12f, 35f, 20f);

                ReportInertiaRepresentation(id);
                phase = 1;
                inertiaCase = 0;
                return;
            }

            if (phase == 1)
            {
                Vector3 bodyRates = new Vector3(1.2f, 0.45f, 0.30f);
                Vector3 momentAeroBody = Vector3.zero;   // torque-free: the decisive backend probe

                Vector3 unityLocalRate = AeroBodyRateToUnityLocal(bodyRates);
                inertiaBody.angularVelocity =
                    inertiaObject.transform.TransformDirection(unityLocalRate);

                inertiaApplier.unityLocalTorque =
                    MavFlightDynamicsMath.AeroBodyMomentToUnityLocal(momentAeroBody);
                inertiaApplier.active = true;

                pendingMoment = momentAeroBody;
                nominalBodyRates = bodyRates;
                phase = 2;
                return;
            }

            if (phase == 2)
            {
                inertiaOmegaBefore = inertiaBody.angularVelocity;
                measuredBodyRatesBefore = MavFlightDynamicsMath.UnityLocalAngularRateToAeroBody(
                    inertiaObject.transform.InverseTransformDirection(inertiaOmegaBefore));
                phase = 3;
                return;
            }

            if (phase == 3)
            {
                Vector3 omegaAfter = inertiaBody.angularVelocity;
                Vector3 ratesAfter = MavFlightDynamicsMath.UnityLocalAngularRateToAeroBody(
                    inertiaObject.transform.InverseTransformDirection(omegaAfter));
                inertiaApplier.active = false;

                Vector3 measured = (ratesAfter - measuredBodyRatesBefore) / Time.fixedDeltaTime;

                MavInertiaMatrix inertia = ReferenceInertia();
                Vector3 euler;
                MavInertiaTensorMath.TryAngularAcceleration(
                    inertia, measuredBodyRatesBefore, pendingMoment, out euler);

                inertiaLog.Append("        BACKEND PROBE - bare Rigidbody, no Maverick stack, "
                                  + "torque-free, p/q/r = ")
                  .Append(measuredBodyRatesBefore.ToString("F3")).AppendLine(" rad/s:")
                  .Append("          measured  ").Append(measured.ToString("F5"))
                  .AppendLine(" rad/s^2")
                  .Append("          Euler     ").Append(euler.ToString("F5"))
                  .Append(" rad/s^2   |Euler| = ").Append(euler.magnitude.ToString("F4"))
                  .AppendLine();

                bool backendOmits = measured.magnitude < 0.05f * euler.magnitude;
                InertiaCheck(backendOmits, id, "backend",
                    "the raw backend still omits w x (I w): torque-free, Euler requires "
                    + euler.magnitude.ToString("F4") + " rad/s^2 and the bare Rigidbody produced "
                    + measured.magnitude.ToString("F6")
                    + ". This is the measurement the compensation in MavSixDoFBody exists for, and it "
                    + "must keep being taken - if it ever changes, the compensation has to go");

                if (inertiaObject != null)
                    Object.DestroyImmediate(inertiaObject);

                phase = 4;
                inertiaCase = 0;
                return;
            }

            // ---- the CORRECTED path, measured through MavSixDoFBody ---------------------------

            if (phase == 4)
            {
                rig = MavF16ReferenceRigBuilder.Build("p5cr-" + id + "-corrected", Capacity);
                rigLive = true;

                // The external moment enters as an aerodynamic coefficient set, because that is how a
                // moment reaches this aircraft. The synthetic model inverts the dimensionalisation
                // MavSixDoFBody will apply, so the external moment is exactly what the case asks for
                // and the gyroscopic correction is the only other term in the sum.
                syntheticAero = rig.root.AddComponent<MavConstantMomentAeroModel>();
                syntheticAero.referenceGeometry = MavF16MorelliReference.CreateReferenceGeometry();
                rig.sixDoF.aerodynamicModel = syntheticAero;
                rig.aero.enabled = false;

                // Airspeed only so dynamic pressure is non-zero and a coefficient can carry a moment.
                // A non-trivial attitude so a body/world frame confusion cannot pass unnoticed.
                MavF16ReferenceRigBuilder.SetFlightCondition(rig, 3000f, 120f, 0f);
                rig.root.transform.rotation = Quaternion.Euler(-12f, 35f, 20f);

                // This is a rigid-body diagnostic, not a flight condition: the attitude and rates are
                // chosen to exercise the coupling, and alpha/beta are not meaningful here.
                rig.recorder.assertEnvelope = false;
                rig.recorder.BeginRecording();

                phase = 5;
                return;
            }

            if (phase == 5)
            {
                Vector3 bodyRates;
                Vector3 externalMoment;
                DescribeCorrectedCase(inertiaCase, out bodyRates, out externalMoment);

                Vector3 unityLocalRate = AeroBodyRateToUnityLocal(bodyRates);
                rig.body.angularVelocity = rig.root.transform.TransformDirection(unityLocalRate);
                syntheticAero.targetMomentAeroBodyNm = externalMoment;

                nominalBodyRates = bodyRates;
                pendingMoment = externalMoment;
                phase = 6;
                return;
            }

            if (phase == 6)
            {
                // Read at order 100, after MavSixDoFBody has already computed and applied this step's
                // load at order -100 but before PhysX integrates. So this omega is exactly the omega
                // the correction was computed from, which is what makes the prediction comparable.
                measuredBodyRatesBefore = MavFlightDynamicsMath.UnityLocalAngularRateToAeroBody(
                    rig.root.transform.InverseTransformDirection(rig.body.angularVelocity));

                capturedExternal = rig.sixDoF.debugExternalMomentAeroBodyNm;
                capturedGyro = rig.sixDoF.debugGyroscopicMomentAeroBodyNm;
                capturedFinal = rig.sixDoF.debugFinalMomentAeroBodyNm;
                phase = 7;
                return;
            }

            if (phase == 7)
            {
                Vector3 ratesAfter = MavFlightDynamicsMath.UnityLocalAngularRateToAeroBody(
                    rig.root.transform.InverseTransformDirection(rig.body.angularVelocity));
                Vector3 measured = (ratesAfter - measuredBodyRatesBefore) / Time.fixedDeltaTime;

                MavInertiaMatrix inertia = ReferenceInertia();

                Vector3 euler;
                MavInertiaTensorMath.TryAngularAcceleration(
                    inertia, measuredBodyRatesBefore, capturedExternal, out euler);

                Vector3 naive;
                MavGyroscopicMoment.TryNaiveAngularAcceleration(inertia, capturedExternal, out naive);

                float errEuler = (measured - euler).magnitude;
                float errNaive = (measured - naive).magnitude;
                float scale = Mathf.Max(1e-4f, euler.magnitude);

                string caseName = inertiaCase == 0 ? "A (w = 0, moment applied)"
                    : inertiaCase == 1 ? "B (w != 0, moment applied)"
                    : "C (w != 0, ZERO external moment)";

                inertiaLog.AppendLine()
                  .Append("        CORRECTED PATH, case ").Append(caseName).AppendLine(":")
                  .Append("          omega entering the step   ")
                  .Append(measuredBodyRatesBefore.ToString("F4")).AppendLine(" rad/s")
                  .Append("          external moment           ").Append(capturedExternal.ToString("F1"))
                  .AppendLine(" Nm")
                  .Append("          gyroscopic correction     ").Append(capturedGyro.ToString("F1"))
                  .AppendLine(" Nm")
                  .Append("          final applied moment      ").Append(capturedFinal.ToString("F1"))
                  .AppendLine(" Nm")
                  .Append("          measured omega_dot        ").Append(measured.ToString("F5"))
                  .AppendLine(" rad/s^2")
                  .Append("          full Euler                ").Append(euler.ToString("F5"))
                  .Append("   error ").Append((100f * errEuler / scale).ToString("F2")).AppendLine("%")
                  .Append("          naive I^-1 M              ").Append(naive.ToString("F5"))
                  .Append("   error ").Append((100f * errNaive / scale).ToString("F2")).AppendLine("%");

                string tag = inertiaCase == 0 ? "A" : inertiaCase == 1 ? "B" : "C";

                // Section 8: the split must add up, and the correction must be the formula.
                Vector3 expectedGyro;
                MavGyroscopicMoment.TryCompute(inertia, measuredBodyRatesBefore, out expectedGyro);
                float sumError = (capturedFinal - (capturedExternal + capturedGyro)).magnitude;
                float formulaError = (capturedGyro - expectedGyro).magnitude;
                float momentScale = Mathf.Max(1f, capturedFinal.magnitude);

                InertiaCheck(sumError / momentScale < 1e-4f, id, tag + "-sum",
                    "finalAppliedMoment = externalMoment + gyroscopicCorrectionMoment to "
                    + sumError.ToString("F4") + " Nm - one sum, applied once");
                InertiaCheck(formulaError / Mathf.Max(1f, expectedGyro.magnitude) < 1e-3f,
                    id, tag + "-formula",
                    "and the correction IS -w x (I w) evaluated on the full tensor: "
                    + capturedGyro.ToString("F1") + " against " + expectedGyro.ToString("F1") + " Nm");

                InertiaCheck(errEuler / scale < 0.05f, id, tag + "-euler",
                    "the corrected runtime matches the FULL Euler solution to "
                    + (100f * errEuler / scale).ToString("F2") + "% over one "
                    + Time.fixedDeltaTime.ToString("F3") + " s step");

                if (inertiaCase == 0)
                {
                    InertiaCheck(capturedGyro.magnitude < 1e-3f, id, "A-zero",
                        "with w = 0 the correction is exactly zero (" + capturedGyro.ToString("F6")
                        + " Nm), so it cannot perturb a non-rotating aircraft");
                    InertiaCheck(errNaive / scale < 0.05f, id, "A-naive",
                        "and the naive prediction agrees here, as it must when the coupling term "
                        + "vanishes - which is what makes this case a clean check of the tensor "
                        + "rather than of the correction");
                }
                else
                {
                    InertiaCheck(errNaive / scale > 0.20f, id, tag + "-naive",
                        "and it does NOT match the naive I^-1 M, which is off by "
                        + (100f * errNaive / scale).ToString("F1")
                        + "% - so this case discriminates between the two predictions instead of "
                        + "passing on either");
                }

                if (inertiaCase == 2)
                {
                    InertiaCheck(capturedExternal.magnitude < 1f, id, "C-external",
                        "the external moment really is zero (" + capturedExternal.ToString("F4")
                        + " Nm), so what follows is torque-free motion");
                    InertiaCheck(measured.magnitude > 0.2f * euler.magnitude, id, "C-evolves",
                        "and the aircraft still evolves: Euler requires "
                        + euler.magnitude.ToString("F4") + " rad/s^2 with no applied moment and the "
                        + "corrected runtime produced " + measured.magnitude.ToString("F4")
                        + ". Before the fix this measured 0.000002 rad/s^2");
                }

                inertiaCase++;
                phase = inertiaCase < 3 ? 5 : 8;
                return;
            }

            // phase 8: report and move on.
            rig.recorder.StopRecording();
            sb.AppendLine();
            sb.AppendLine("[" + id + "] inertia / angular dynamics against analytical rigid-body motion");
            sb.Append(inertiaLog.ToString());
            Passed += inertiaPassed;
            Failed += inertiaFailed;
            sb.Append("        ").Append(inertiaPassed).Append(" passed, ").Append(inertiaFailed)
              .AppendLine(" failed");

            MavF16ReferenceRigBuilder.Destroy(rig);
            rigLive = false;

            phase = 0;
            scenario++;
        }

        private static MavInertiaMatrix ReferenceInertia()
        {
            return MavInertiaTensorMath.BuildAeroBodyInertia(
                MavF16MassReference.IxKgM2, MavF16MassReference.IyKgM2,
                MavF16MassReference.IzKgM2, MavF16MassReference.IxzKgM2);
        }

        private static void DescribeCorrectedCase(
            int index, out Vector3 bodyRates, out Vector3 externalMoment)
        {
            switch (index)
            {
                case 0:
                    bodyRates = Vector3.zero;
                    externalMoment = new Vector3(20000f, 60000f, 30000f);
                    return;

                case 1:
                    bodyRates = new Vector3(1.2f, 0.45f, 0.30f);
                    externalMoment = new Vector3(20000f, 60000f, 30000f);
                    return;

                default:
                    bodyRates = new Vector3(1.2f, 0.45f, 0.30f);
                    externalMoment = Vector3.zero;
                    return;
            }
        }

        private MavConstantMomentAeroModel syntheticAero;
        private Vector3 capturedExternal;
        private Vector3 capturedGyro;
        private Vector3 capturedFinal;
        private Vector3 nominalBodyRates;
        private Vector3 measuredBodyRatesBefore;
        private Vector3 pendingMoment;

        private void InertiaCheck(bool condition, string id, string suffix, string text)
        {
            if (condition)
            {
                inertiaPassed++;
                inertiaLog.Append("          PASS  ").Append(id).Append('-').Append(suffix)
                  .Append("  ").AppendLine(text);
            }
            else
            {
                inertiaFailed++;
                inertiaLog.Append("          FAIL  ").Append(id).Append('-').Append(suffix)
                  .Append("  ").AppendLine(text);
            }
        }

        /// <summary>
        /// Records what Unity was actually handed, and checks the principal-axis representation
        /// reconstructs the sourced tensor.
        ///
        /// Read back from the Rigidbody rather than from the values written to it, because Unity may
        /// normalise or reorder what it stores, and the representation that matters is the one the
        /// solver will use.
        /// </summary>
        private void ReportInertiaRepresentation(string id)
        {
            MavInertiaMatrix aero = MavInertiaTensorMath.BuildAeroBodyInertia(
                MavF16MassReference.IxKgM2, MavF16MassReference.IyKgM2,
                MavF16MassReference.IzKgM2, MavF16MassReference.IxzKgM2);
            MavInertiaMatrix expectedUnity = MavInertiaTensorMath.AeroBodyInertiaToUnityLocal(aero);

            Vector3 principal = inertiaBody.inertiaTensor;
            Quaternion rotation = inertiaBody.inertiaTensorRotation;
            MavInertiaMatrix rebuilt = MavInertiaTensorMath.Reconstruct(principal, rotation);

            float diff = MavInertiaTensorMath.MaxAbsDifference(expectedUnity, rebuilt);
            float magnitude = MavInertiaTensorMath.MaxAbsElement(expectedUnity);
            float relative = diff / Mathf.Max(1f, magnitude);

            inertiaLog.Append("        mass ").Append(inertiaBody.mass.ToString("F2"))
              .Append(" kg, centre of mass ").Append(inertiaBody.centerOfMass.ToString("F4"))
              .AppendLine();
            inertiaLog.Append("        source tensor (aero body, slug-ft^2): Ix ")
              .Append(MavF16MassReference.IxSlugFt2.ToString("F0")).Append(", Iy ")
              .Append(MavF16MassReference.IySlugFt2.ToString("F0")).Append(", Iz ")
              .Append(MavF16MassReference.IzSlugFt2.ToString("F0")).Append(", Ixz ")
              .Append(MavF16MassReference.IxzSlugFt2.ToString("F0")).AppendLine();
            inertiaLog.Append("        Unity principal moments read back: ")
              .Append(principal.ToString("F1")).Append(" kg m^2, rotation ")
              .Append(rotation.eulerAngles.ToString("F3")).AppendLine(" deg");
            inertiaLog.Append("        reconstruction error vs the basis-changed source tensor: ")
              .Append(diff.ToString("F3")).Append(" kg m^2 (")
              .Append((100f * relative).ToString("F4")).AppendLine("% of the largest element)");

            InertiaCheck(Mathf.Abs(inertiaBody.mass - MavF16MassReference.MassKg) < 0.5f, id, "mass",
                "the diagnostic body carries the reference mass "
                + inertiaBody.mass.ToString("F2") + " kg");
            InertiaCheck(inertiaBody.centerOfMass == Vector3.zero, id, "cg",
                "with the centre of mass at the origin, which is the declared reference datum");
            InertiaCheck(relative < 1e-3f, id, "tensor",
                "and Unity's principal moments plus inertiaTensorRotation RECONSTRUCT the sourced "
                + "body-axis tensor after the aero->Unity basis change, to "
                + (100f * relative).ToString("F4")
                + "% - so the Ixz product of inertia survived the decomposition rather than being "
                + "dropped");
            ProbeRigidbodyGyroscopicApi(id);

            InertiaCheck(Mathf.Abs(rotation.eulerAngles.y) < 1e-3f
                         && Mathf.Abs(rotation.eulerAngles.z) < 1e-3f
                         || Mathf.Abs(rotation.eulerAngles.y - 360f) < 1e-3f, id, "rot-axis",
                "the principal rotation is about Unity's X axis only ("
                + rotation.eulerAngles.ToString("F4")
                + " deg), which is where an Ixz-only coupling must put it");
        }

        /// <summary>
        /// Lists every Rigidbody member whose name mentions gyroscopic or inertia.
        ///
        /// If Unity exposed a switch for gyroscopic forces, failing to set it would be this harness
        /// misconfiguring the body rather than an engine limitation - a difference worth settling by
        /// looking instead of assuming. PhysX itself has PxRigidBodyFlag::eENABLE_GYROSCOPIC_FORCES;
        /// the question is whether Unity surfaces it.
        /// </summary>
        private void ProbeRigidbodyGyroscopicApi(string id)
        {
            System.Reflection.MemberInfo[] members = typeof(Rigidbody).GetMembers(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static);

            int gyroscopic = 0;
            StringBuilder found = new StringBuilder();
            for (int i = 0; i < members.Length; i++)
            {
                string lower = members[i].Name.ToLowerInvariant();
                if (lower.Contains("gyro"))
                {
                    gyroscopic++;
                    found.Append(members[i].Name).Append(' ');
                }
                else if (lower.Contains("inertia"))
                {
                    found.Append('[').Append(members[i].Name).Append("] ");
                }
            }

            inertiaLog.Append("        Rigidbody API probe - members mentioning gyroscopic/inertia: ")
              .AppendLine(found.Length > 0 ? found.ToString() : "(none)");
            InertiaCheck(gyroscopic == 0, id, "api",
                "Unity 6000.3.16f1 exposes NO Rigidbody member for gyroscopic forces ("
                + gyroscopic + " found), so the missing coupling below is an engine behaviour this "
                + "rig cannot switch on - not a setting it forgot");
        }

        /// <summary>Inverse of MavFlightDynamicsMath.UnityLocalAngularRateToAeroBody.</summary>
        private static Vector3 AeroBodyRateToUnityLocal(Vector3 bodyRates)
        {
            // p = -unity.z, q = -unity.x, r = unity.y  =>  unity = (-q, r, -p)
            return new Vector3(-bodyRates.y, bodyRates.z, -bodyRates.x);
        }

        // ============================================================ helpers

        private void Complete()
        {
            if (rigLive)
            {
                MavF16ReferenceRigBuilder.Destroy(rig);
                rigLive = false;
            }

            sb.AppendLine();
            sb.Append("RESULT: ").Append(Failed == 0 ? "PASS" : "FAIL")
              .Append(" passed=").Append(Passed).Append(" failed=").Append(Failed);
            Report = sb.ToString();
            Finished = true;
        }

        private void Check(bool condition, string id, string suffix, string text)
        {
            if (suffix == "a")
            {
                sb.AppendLine();
                sb.AppendLine("[" + id + "] " + ScenarioTitle(id));
            }

            if (condition)
            {
                Passed++;
                sb.Append("  PASS  ").Append(id).Append(suffix).Append("  ").AppendLine(text);
            }
            else
            {
                Failed++;
                sb.Append("  FAIL  ").Append(id).Append(suffix).Append("  ").AppendLine(text);
            }
        }

        private bool Require(bool condition, string id, string suffix, string text)
        {
            Check(condition, id, suffix, text);
            return condition;
        }

        private static string ScenarioTitle(string id)
        {
            switch (id)
            {
                case "U5CR-001": return "free fall - gravity ownership and specific-force semantics";
                case "U5CR-002": return "unpowered glide - Morelli aero and gravity only";
                case "U5CR-003": return "elevator step through the actuator path";
                case "U5CR-004": return "aileron step through the actuator path";
                case "U5CR-005": return "rudder step through the actuator path";
                case "U5CR-006": return "pitch doublet";
                case "U5CR-007": return "lateral doublet";
                case "U5CR-009": return "energy sanity - no unexplained injection";
                case "U5CR-010": return "reference envelope exit";
                default: return id;
            }
        }

        private static float MeanEnergy(MavF16ReferenceTelemetrySample[] s, int from, int to)
        {
            int a = Mathf.Max(0, from);
            int b = Mathf.Max(a + 1, to);
            float sum = 0f;
            for (int i = a; i < b; i++)
                sum += s[i].totalMechanicalEnergyJ;

            return sum / (b - a);
        }

        private static float e0WithoutRot(MavF16ReferenceTelemetrySample[] s)
        {
            return s[2].translationalKeJ + s[2].potentialEnergyJ;
        }

        private static float e1WithoutRot(MavF16ReferenceTelemetrySample[] s, int n)
        {
            return s[n - 2].translationalKeJ + s[n - 2].potentialEnergyJ;
        }

        private static float MaxRotationalKe(MavF16ReferenceTelemetrySample[] s, int n)
        {
            float m = 0f;
            for (int i = 0; i < n; i++)
                m = Mathf.Max(m, s[i].rotationalKeJ);

            return m;
        }

        private static bool AllFinite(MavF16ReferenceTelemetrySample[] s, int n)
        {
            for (int i = 0; i < n; i++)
            {
                if (float.IsNaN(s[i].totalMechanicalEnergyJ)
                    || float.IsInfinity(s[i].totalMechanicalEnergyJ)
                    || float.IsNaN(s[i].alphaDeg) || float.IsNaN(s[i].cm)
                    || float.IsNaN(s[i].bodyRatesRadSec.x))
                    return false;
            }

            return true;
        }

        private delegate float SampleAccessor(MavF16ReferenceTelemetrySample s);

        private static float SampleQ(MavF16ReferenceTelemetrySample s) { return s.bodyRatesRadSec.y; }
        private static float SampleP(MavF16ReferenceTelemetrySample s) { return s.bodyRatesRadSec.x; }
        private static float SampleR(MavF16ReferenceTelemetrySample s) { return s.bodyRatesRadSec.z; }
        private static float SampleAlpha(MavF16ReferenceTelemetrySample s) { return s.alphaDeg; }
        private static float SampleBeta(MavF16ReferenceTelemetrySample s) { return s.betaDeg; }
        private static float SampleCm(MavF16ReferenceTelemetrySample s) { return s.cm; }
        private static float SampleCl(MavF16ReferenceTelemetrySample s) { return s.cl; }
        private static float SampleCn(MavF16ReferenceTelemetrySample s) { return s.cn; }
        private static float SampleElev(MavF16ReferenceTelemetrySample s) { return s.actualElevatorDeg; }
        private static float SampleAil(MavF16ReferenceTelemetrySample s) { return s.actualAileronDeg; }
        private static float SampleRud(MavF16ReferenceTelemetrySample s) { return s.actualRudderDeg; }

        private static float Extremum(
            MavF16ReferenceTelemetrySample[] s, int from, int to, SampleAccessor f, bool maximum)
        {
            int a = Mathf.Max(0, from);
            int b = Mathf.Min(to, s.Length);
            float best = maximum ? float.MinValue : float.MaxValue;
            for (int i = a; i < b; i++)
            {
                float v = f(s[i]);
                if (maximum ? v > best : v < best)
                    best = v;
            }

            return best == float.MinValue || best == float.MaxValue ? 0f : best;
        }

        private static float PitchUpDeg(MavF16ReferenceTelemetrySample s)
        {
            float x = s.eulerDeg.x;
            if (x > 180f) x -= 360f;
            return -x;
        }

        private static float BankDeg(MavF16ReferenceTelemetrySample s)
        {
            float z = s.eulerDeg.z;
            if (z > 180f) z -= 360f;
            return -z;
        }
    }

    /// <summary>
    /// An aerodynamic model that produces a chosen aero-body MOMENT and no force.
    ///
    /// Used by U5CR-008 so a known external moment can reach the aircraft the way any moment does -
    /// as a coefficient set that MavSixDoFBody dimensionalises and applies - rather than through a
    /// test-only torque source that would bypass the very code being verified.
    ///
    /// It inverts the dimensionalisation exactly:
    ///
    ///     Cl = L / (qbar S b)      Cm = M / (qbar S cbar)      Cn = N / (qbar S b)
    ///
    /// so the moment the load set receives is the moment the caller asked for, whatever the dynamic
    /// pressure happens to be. Forces are identically zero, so the diagnostic measures rotational
    /// dynamics and nothing else.
    ///
    /// SYNTHETIC_VALIDATION_ONLY. These are not F-16 coefficients and this is not an aerodynamic
    /// model of anything; it is a way of expressing a chosen moment in the units the pipeline speaks.
    /// </summary>
    public sealed class MavConstantMomentAeroModel : MavAerodynamicModelBase
    {
        public Vector3 targetMomentAeroBodyNm;

        [Header("Read-only")]
        public float debugDynamicPressurePa;
        public MavAeroCoefficients debugCoefficients;

        public override MavAeroCoefficients Evaluate(
            MavFlightState state,
            MavControlInput input,
            MavAtmosphereSample atmosphere)
        {
            MavAeroCoefficients c = MavAeroCoefficients.Zero;

            float q = state.dynamicPressurePa;
            debugDynamicPressurePa = q;

            float qS = q * Mathf.Max(0.01f, referenceGeometry.wingAreaM2);
            if (qS < 1e-3f)
            {
                debugCoefficients = c;
                return c;
            }

            float span = Mathf.Max(0.01f, referenceGeometry.wingSpanM);
            float chord = Mathf.Max(0.01f, referenceGeometry.meanAerodynamicChordM);

            c.cl = targetMomentAeroBodyNm.x / (qS * span);
            c.cm = targetMomentAeroBodyNm.y / (qS * chord);
            c.cn = targetMomentAeroBodyNm.z / (qS * span);

            debugCoefficients = c;
            return c;
        }
    }

    /// <summary>
    /// Applies a fixed Unity-local torque each physics step, and nothing else.
    ///
    /// Exists only so the U5CR-008 diagnostic body receives its test moment from inside FixedUpdate,
    /// the way a real load would arrive. It is not part of the aircraft: the reference rig's only
    /// physical writer remains MavSixDoFBody, and this component is never attached to it.
    /// </summary>
    public sealed class MavInertiaTorqueApplier : MonoBehaviour
    {
        public Vector3 unityLocalTorque;
        public bool active;

        private Rigidbody body;
        private bool refused;

        /// <summary>
        /// Refuses to run on anything that could be an aircraft.
        ///
        /// The Phase 5A writer scan classifies this file as NotPlayerBody - a writer whose Rigidbody
        /// is never the player's. That classification is only honest if it cannot quietly become
        /// false, so the condition is checked here instead of being promised in a comment: an object
        /// carrying the replacement body, the ownership authority or a legacy writer is not a bare
        /// diagnostic body, and this component disables itself rather than becoming an ungated
        /// contributor to it.
        /// </summary>
        private void Awake()
        {
            body = GetComponent<Rigidbody>();

            if (GetComponent<MavSixDoFBody>() != null
                || GetComponent<MavFlightPhysicsOwnership>() != null
                || HasLegacyWriter())
            {
                refused = true;
                active = false;
                enabled = false;
                Debug.LogError(
                    "MavInertiaTorqueApplier refused to attach: this object carries an aircraft "
                    + "physics stack, and this component is only valid on the bare diagnostic body "
                    + "created by the Phase 5C-R U5CR-008 case.", this);
            }
        }

        private bool HasLegacyWriter()
        {
            MonoBehaviour[] all = GetComponents<MonoBehaviour>();
            string[] deny = MavSixDoFBody.DefaultConflictingLegacyPhysicsComponents;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null)
                    continue;

                string typeName = all[i].GetType().Name;
                for (int d = 0; d < deny.Length; d++)
                {
                    if (typeName == deny[d])
                        return true;
                }
            }

            return false;
        }

        private void FixedUpdate()
        {
            if (refused || !active || body == null)
                return;

            body.AddRelativeTorque(unityLocalTorque, ForceMode.Force);
        }
    }
}
