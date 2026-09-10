#if UNITY_EDITOR
using System.Text;
using UnityEngine;
using MaverickFresh.FlightDynamics;
using MaverickFresh.FlightDynamics.F16;

namespace MaverickFresh.FlightDynamics.EditorTools
{
    /// <summary>
    /// UNITY COMPONENT INTEGRATION validation for the flight-dynamics path.
    ///
    /// Everything in the Phase 1-3 suites is pure and static: it exercises production maths through
    /// parameterised entry points, with no GameObject anywhere. That is fast, deterministic and
    /// completely blind to wiring. It has already missed a real bug for exactly that reason - an
    /// attitude publication that existed only inside a test helper while the runtime path carried
    /// none.
    ///
    /// This suite is the other half. It builds REAL GameObjects with REAL components, wires them the
    /// way the runtime wires them, and drives them in the REAL execution order:
    ///
    ///     control law        -300
    ///     ownership arbiter  -250
    ///     actuator           -200
    ///     six-DoF body       -100
    ///     legacy owners         0
    ///
    /// Nothing here fabricates a MavOwnershipObservation, a MavPipelineSnapshot or a MavFlightState.
    /// Every fact asserted is one the components produced themselves.
    ///
    /// It runs in the editor without play mode, so component Awake/OnEnable are not invoked by
    /// Unity. The rig calls the explicit initialisation the runtime would have done, and every
    /// per-step entry point is a public method rather than FixedUpdate, so stepping is deterministic
    /// and ordered by the suite rather than by Unity.
    ///
    /// Counts from this suite are reported SEPARATELY from the pure/static counts. They measure a
    /// different thing and adding them together would overstate both.
    /// </summary>
    public static class MavFlightDynamicsIntegrationValidation
    {
        private const float FixedDeltaTime = 0.02f;

        public static string RunAll(out int passed, out int failed)
        {
            passed = 0;
            failed = 0;
            StringBuilder report = new StringBuilder(8192);
            report.AppendLine("Maverick Flight Dynamics UNITY INTEGRATION Validation");
            report.AppendLine("=====================================================");
            report.AppendLine("Real components, real wiring, real execution order.");

            ValidateCommandPipeline(report, ref passed, ref failed);
            ValidatePropulsionPipeline(report, ref passed, ref failed);
            ValidateOwnershipHandover(report, ref passed, ref failed);
            ValidateCurrentStepDropout(report, ref passed, ref failed);
            ValidateDestroyedLegacyOwner(report, ref passed, ref failed);
            ValidateBenchRigUnowned(report, ref passed, ref failed);

            report.AppendLine();
            report.Append("RESULT: ")
                .Append(failed == 0 ? "PASS" : "FAIL")
                .Append("  passed=").Append(passed)
                .Append(" failed=").Append(failed);

            return report.ToString();
        }

        // ================================================================= [I0]

        /// <summary>
        /// command source -> control law -> actuator -> six-DoF body, through real components.
        /// </summary>
        private static void ValidateCommandPipeline(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[I0] Command source -> control law -> actuator -> body");

            // No ownership controller here: this section is about the command chain, and an
            // arbiter would be making ownership decisions that have nothing to do with it.
            MavIntegrationRig rig = MavIntegrationRig.Build(
                withLegacyOwner: false, withOwnershipController: false);
            try
            {
                rig.SetFlightCondition(altitudeM: 3000f, trueAirspeedMps: 200f);
                rig.commandSource.command = new MavPilotCommand { pitch = 1f, throttle01 = 0.6f };

                rig.Step();

                Record(
                    rig.controlLaw.debugCommandResolution
                        == MavFlightControlLawBase.MavCommandResolution.SourceSignal,
                    "the control law took its command from the source component",
                    report, ref passed, ref failed);

                Record(
                    rig.controlLaw.debugDroveActuator,
                    "the law drove the actuator component",
                    report, ref passed, ref failed);

                Record(
                    rig.actuator.actual.elevatorDeg < 0f,
                    "aft stick reached the ACTUATOR as negative elevator ("
                    + rig.actuator.actual.elevatorDeg.ToString("F3") + " deg)",
                    report, ref passed, ref failed);

                Record(
                    Mathf.Abs(rig.body.controlInput.elevatorDeg - rig.actuator.actual.elevatorDeg)
                        < 1e-4f,
                    "and the actuator published that same state into the six-DoF body",
                    report, ref passed, ref failed);

                Record(
                    Mathf.Abs(rig.actuator.actual.throttle01 - 0.6f) < 1e-4f,
                    "throttle passed through the whole chain unmodified",
                    report, ref passed, ref failed);

                // The body's own state, produced by the real component from the real transform.
                Record(
                    rig.body.debugState.attitude.valid,
                    "the body published a valid attitude from its own transform - the runtime "
                    + "wiring the pure suites cannot see",
                    report, ref passed, ref failed);

                Record(
                    rig.body.debugState.trueAirspeedMps > 100f,
                    "and a flight state with real airspeed ("
                    + rig.body.debugState.trueAirspeedMps.ToString("F1") + " m/s)",
                    report, ref passed, ref failed);

                // Bank the aircraft for real and confirm the law responds.
                rig.transform.rotation = Quaternion.Euler(0f, 0f, -45f);
                rig.commandSource.command = MavPilotCommand.Neutral;
                rig.Step();
                rig.Step();

                // Expectation derived from the aircraft's actual geometry rather than from Unity's
                // Euler convention: whichever way that rotation turned out, the wing that is DOWN
                // determines the sign the body must publish.
                bool rightWingDown = rig.transform.right.y < 0f;
                float publishedBankDeg = rig.body.debugState.attitude.BankAngleDeg;

                Record(
                    Mathf.Abs(publishedBankDeg) > 30f
                    && ((rightWingDown && publishedBankDeg > 0f)
                        || (!rightWingDown && publishedBankDeg < 0f)),
                    "a rolled transform publishes a bank angle whose SIGN matches which wing is "
                    + "down (right wing down = " + rightWingDown + ", published "
                    + publishedBankDeg.ToString("F1") + " deg)",
                    report, ref passed, ref failed);

                Record(
                    rig.controlLaw.debugLaw.turnCompensationApplied
                    && rig.controlLaw.debugLaw.commandedLoadFactorG > 1.1f,
                    "and the control law applies turn compensation from it ("
                    + rig.controlLaw.debugLaw.commandedLoadFactorG.ToString("F3") + " g)",
                    report, ref passed, ref failed);
            }
            finally
            {
                rig.Destroy();
            }
        }

        // ================================================================= [I1]

        private static void ValidatePropulsionPipeline(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[I1] Engine Evaluate -> thrust deck -> loads");

            // No ownership controller: this section deliberately arms the body directly to isolate
            // the propulsion path, which an arbiter would correctly veto.
            MavIntegrationRig rig = MavIntegrationRig.Build(
                withLegacyOwner: false, withOwnershipController: false);
            try
            {
                rig.SetFlightCondition(altitudeM: 0f, trueAirspeedMps: 200f);
                rig.commandSource.command = new MavPilotCommand { throttle01 = 1f };
                rig.body.simulationEnabled = true;
                rig.body.requireOperationalReadinessForLoadApplication = false;

                for (int i = 0; i < 5; i++)
                    rig.Step();

                Record(
                    !rig.engine.HasAuthoritativeData
                    && rig.body.debugLoadSet.propulsive.forceAeroBodyN == Vector3.zero,
                    "with NO thrust deck attached the real engine component contributes exactly "
                    + "zero propulsive force, and says the data is not authoritative",
                    report, ref passed, ref failed);

                Record(
                    rig.engine.actualPowerPercent > 0f,
                    "while the sourced power state still advances ("
                    + rig.engine.actualPowerPercent.ToString("F1") + " %)",
                    report, ref passed, ref failed);

                // Attach a SYNTHETIC deck and confirm the wiring carries dimensional thrust.
                MavSyntheticBenchThrustDeck deck =
                    rig.gameObject.AddComponent<MavSyntheticBenchThrustDeck>();
                rig.engine.thrustDeck = deck;

                rig.Step();

                Record(
                    rig.body.debugLoadSet.propulsive.forceAeroBodyN.x > 0f,
                    "with a deck attached the same component produces dimensional thrust through "
                    + "the real load path ("
                    + rig.body.debugLoadSet.propulsive.forceAeroBodyN.x.ToString("F0") + " N)",
                    report, ref passed, ref failed);

                Record(
                    !rig.body.debugLoadSet.propulsive.hasAuthoritativeData
                    && !rig.engine.IsAcceptableForLiveFlight,
                    "and it is still marked non-authoritative and unacceptable for live flight, "
                    + "because the deck is SYNTHETIC",
                    report, ref passed, ref failed);

                Record(
                    Mathf.Abs(rig.body.debugLoadSet.propulsive.forceAeroBodyN.y) < 1e-3f
                    && Mathf.Abs(rig.body.debugLoadSet.propulsive.forceAeroBodyN.z) < 1e-3f
                    && rig.body.debugLoadSet.propulsive.momentAeroBodyNm == Vector3.zero,
                    "thrust acts along body X through the CG, with no invented moment",
                    report, ref passed, ref failed);

                Record(
                    rig.body.debugLoadSet.aerodynamicContributions == 1
                    && rig.body.debugLoadSet.propulsiveContributions == 1,
                    "each source contributed exactly once to the summed load set",
                    report, ref passed, ref failed);
            }
            finally
            {
                rig.Destroy();
            }
        }

        // ================================================================= [I2]

        private static void ValidateOwnershipHandover(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[I2] Ownership handover and rollback, on real components");

            MavIntegrationRig rig = MavIntegrationRig.Build(withLegacyOwner: true);
            try
            {
                rig.SetFlightCondition(altitudeM: 3000f, trueAirspeedMps: 200f);
                rig.MakeOperationallyLiveReady();
                rig.Step();

                Record(
                    rig.legacyOwner.enabled && !rig.body.simulationEnabled,
                    "before the handover: legacy is enabled and the new FDM is disarmed",
                    report, ref passed, ref failed);

                rig.ownership.RequestTransitionToNewFdm();
                rig.Step();

                Record(
                    rig.ownership.state == MavPhysicsOwnershipState.NewOwned,
                    "after an explicit request the controller reaches NewOwned ("
                    + rig.ownership.state + ": " + rig.ownership.stateReason + ")",
                    report, ref passed, ref failed);

                Record(
                    !rig.legacyOwner.enabled && rig.body.simulationEnabled,
                    "the real legacy component was disabled and the new FDM armed",
                    report, ref passed, ref failed);

                Record(
                    !(rig.legacyOwner.enabled && rig.body.simulationEnabled),
                    "and the exclusive-ownership invariant holds on the real components",
                    report, ref passed, ref failed);

                int legacyStepsDuringNewOwnership = rig.legacyOwner.debugStepCount;
                rig.Step();

                Record(
                    rig.legacyOwner.debugStepCount == legacyStepsDuringNewOwnership,
                    "the legacy owner does not run while the new FDM owns physics",
                    report, ref passed, ref failed);

                // Hand back.
                rig.ownership.RequestReturnToLegacy();
                rig.Step();

                Record(
                    rig.ownership.state == MavPhysicsOwnershipState.LegacyOwned,
                    "an explicit return hands ownership back to legacy ("
                    + rig.ownership.state + ")",
                    report, ref passed, ref failed);

                Record(
                    rig.legacyOwner.enabled && !rig.body.simulationEnabled,
                    "the real legacy component was re-enabled and the new FDM disarmed",
                    report, ref passed, ref failed);

                // Rollback: make the stack unfit, then request a handover.
                rig.commandSource.treatAsOperationalSource = false;
                rig.ownership.RequestTransitionToNewFdm();
                rig.Step();

                Record(
                    rig.ownership.state == MavPhysicsOwnershipState.LegacyOwned,
                    "a handover requested on an unfit stack rolls back to legacy ("
                    + rig.ownership.stateReason + ")",
                    report, ref passed, ref failed);

                Record(
                    rig.legacyOwner.enabled && !rig.body.simulationEnabled,
                    "and leaves legacy owning physics, with the new FDM disarmed",
                    report, ref passed, ref failed);
            }
            finally
            {
                rig.Destroy();
            }
        }

        // ================================================================= [I3]

        /// <summary>
        /// The ordering bug, demonstrated on real components.
        ///
        /// If ownership is arbitrated BEFORE the control law has polled, a source that drops out on
        /// the current step still looks healthy to the arbiter. It releases legacy and arms the new
        /// FDM; the law then observes the dropout; the body refuses loads for lack of a valid
        /// command path; and legacy is already disabled. That is a physics step with NO owner.
        /// </summary>
        private static void ValidateCurrentStepDropout(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[I3] Command dropout on the handover step");

            // --- production order: law first, then ownership ---
            MavIntegrationRig rig = MavIntegrationRig.Build(withLegacyOwner: true);
            try
            {
                rig.SetFlightCondition(altitudeM: 3000f, trueAirspeedMps: 200f);
                rig.MakeOperationallyLiveReady();
                rig.Step();

                Record(
                    rig.body.debugReadiness.operationallyLiveReady,
                    "the rig is genuinely operationally live-ready before the dropout",
                    report, ref passed, ref failed);

                // The source drops out on the SAME step the handover is requested.
                rig.commandSource.commandAvailable = false;
                rig.ownership.RequestTransitionToNewFdm();
                rig.Step();

                Record(
                    rig.ownership.state != MavPhysicsOwnershipState.NewOwned,
                    "the handover does NOT complete when the command signal drops on that step ("
                    + rig.ownership.state + ")",
                    report, ref passed, ref failed);

                Record(
                    rig.legacyOwner.enabled,
                    "legacy still owns physics: there is no step with nothing flying the aircraft",
                    report, ref passed, ref failed);

                Record(
                    !rig.body.simulationEnabled,
                    "and the new FDM was never armed against a dropped signal",
                    report, ref passed, ref failed);

                Record(
                    rig.controlLaw.debugCommandResolution
                        == MavFlightControlLawBase.MavCommandResolution.SignalLossPolicy,
                    "the law meanwhile applied its declared signal-loss policy",
                    report, ref passed, ref failed);
            }
            finally
            {
                rig.Destroy();
            }

            // --- ordering rationale: arbitrate BEFORE the law and the decision goes wrong ---
            MavIntegrationRig stale = MavIntegrationRig.Build(withLegacyOwner: true);
            try
            {
                stale.SetFlightCondition(altitudeM: 3000f, trueAirspeedMps: 200f);
                stale.MakeOperationallyLiveReady();
                stale.Step();

                stale.commandSource.commandAvailable = false;
                stale.ownership.RequestTransitionToNewFdm();

                // Deliberately the OLD ordering: ownership arbitrated ahead of the control law, so
                // it reads last step's command resolution.
                stale.StepWithOwnershipBeforeControlLaw();

                Record(
                    stale.ownership.state == MavPhysicsOwnershipState.NewOwned
                    || !stale.legacyOwner.enabled,
                    "arbitrating BEFORE the law acts on a stale SourceSignal and releases legacy - "
                    + "which is exactly why the arbiter runs at -250 and not ahead of everything ("
                    + stale.ownership.state + ")",
                    report, ref passed, ref failed);

                Record(
                    !stale.body.StepPhysics(FixedDeltaTime, 99f),
                    "and the body then refuses loads for the dropped command path, leaving that "
                    + "step with no owner at all",
                    report, ref passed, ref failed);
            }
            finally
            {
                stale.Destroy();
            }
        }

        // ================================================================= [I4]

        private static void ValidateDestroyedLegacyOwner(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[I4] Destroyed legacy owner cannot settle as Unowned");

            MavIntegrationRig rig = MavIntegrationRig.Build(withLegacyOwner: true);
            try
            {
                rig.SetFlightCondition(altitudeM: 3000f, trueAirspeedMps: 200f);
                rig.MakeOperationallyLiveReady();
                rig.Step();

                rig.ownership.RequestTransitionToNewFdm();
                rig.Step();

                Record(
                    rig.ownership.state == MavPhysicsOwnershipState.NewOwned
                    && !rig.legacyOwner.enabled,
                    "handover completed and the legacy owner is disabled",
                    report, ref passed, ref failed);

                // Destroy the disabled legacy owner behind the controller's back, then hand back.
                Object.DestroyImmediate(rig.legacyOwner);
                rig.legacyOwner = null;

                rig.ownership.RequestReturnToLegacy();
                rig.Step();

                Record(
                    rig.ownership.state == MavPhysicsOwnershipState.Fault,
                    "a legacy owner destroyed while disabled makes the hand-back a FAULT, not "
                    + "Unowned (" + rig.ownership.state + ": " + rig.ownership.stateReason + ")",
                    report, ref passed, ref failed);

                Record(
                    !rig.body.simulationEnabled,
                    "and the new FDM is held disarmed in the fault state",
                    report, ref passed, ref failed);

                rig.Step();

                Record(
                    rig.ownership.state == MavPhysicsOwnershipState.Fault
                    && !rig.body.simulationEnabled,
                    "the fault persists rather than healing on its own",
                    report, ref passed, ref failed);
            }
            finally
            {
                rig.Destroy();
            }
        }

        // ================================================================= [I5]

        private static void ValidateBenchRigUnowned(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[I5] Bench rig with no legacy owner");

            MavIntegrationRig rig = MavIntegrationRig.Build(withLegacyOwner: false);
            try
            {
                rig.SetFlightCondition(altitudeM: 3000f, trueAirspeedMps: 200f);
                rig.MakeOperationallyLiveReady();

                // Nothing owns physics and nothing ever did: the controller should say so rather
                // than claiming a legacy owner exists.
                rig.ownership.RequestReturnToLegacy();
                rig.Step();

                Record(
                    rig.ownership.state == MavPhysicsOwnershipState.LegacyOwned
                    || rig.ownership.state == MavPhysicsOwnershipState.Unowned,
                    "a rig with no legacy owner settles honestly rather than faulting ("
                    + rig.ownership.state + ": " + rig.ownership.stateReason + ")",
                    report, ref passed, ref failed);

                rig.ownership.RequestTransitionToNewFdm();
                rig.Step();

                Record(
                    rig.ownership.state == MavPhysicsOwnershipState.NewOwned,
                    "and it can still hand over to the new FDM (" + rig.ownership.state + ")",
                    report, ref passed, ref failed);

                Record(
                    rig.ownership.debugDisabledLegacyOwnerCount == 0,
                    "having disabled nothing, because there was nothing to disable",
                    report, ref passed, ref failed);

                rig.ownership.RequestReturnToLegacy();
                rig.Step();

                Record(
                    rig.ownership.state == MavPhysicsOwnershipState.Unowned,
                    "returning from that leaves the rig explicitly Unowned - not a pretended "
                    + "LegacyOwned (" + rig.ownership.state + ": " + rig.ownership.stateReason + ")",
                    report, ref passed, ref failed);

                Record(
                    !rig.body.simulationEnabled,
                    "with the new FDM disarmed",
                    report, ref passed, ref failed);
            }
            finally
            {
                rig.Destroy();
            }
        }

        // ================================================================= rig

        /// <summary>
        /// A real GameObject with the real components, wired the way the runtime wires them and
        /// stepped in the real execution order.
        /// </summary>
        private sealed class MavIntegrationRig
        {
            public GameObject gameObject;
            public Transform transform;
            public Rigidbody rigidbody;
            public MavSixDoFBody body;
            public MavF16FlightDynamicsProfile profile;
            public MavF16AeroModel aeroModel;
            public MavF16ControlActuator actuator;
            public MavF16ControlLawV01 controlLaw;
            public MavManualPilotCommandSource commandSource;
            public MavF16EnginePowerModel engine;
            public MavPhysicsOwnershipController ownership;
            public MavIntegrationTestLegacyOwner legacyOwner;

            private float stepTime;

            public static MavIntegrationRig Build(bool withLegacyOwner)
            {
                return Build(withLegacyOwner, withOwnershipController: true);
            }

            public static MavIntegrationRig Build(bool withLegacyOwner, bool withOwnershipController)
            {
                MavIntegrationRig rig = new MavIntegrationRig();

                rig.gameObject = new GameObject("MavIntegrationRig");
                rig.gameObject.hideFlags = HideFlags.HideAndDontSave;
                rig.transform = rig.gameObject.transform;

                // RequireComponent adds the Rigidbody with the body.
                rig.body = rig.gameObject.AddComponent<MavSixDoFBody>();
                rig.rigidbody = rig.gameObject.GetComponent<Rigidbody>();
                rig.rigidbody.useGravity = false;

                rig.profile = rig.gameObject.AddComponent<MavF16FlightDynamicsProfile>();
                rig.aeroModel = rig.gameObject.AddComponent<MavF16AeroModel>();
                rig.actuator = rig.gameObject.AddComponent<MavF16ControlActuator>();
                rig.commandSource = rig.gameObject.AddComponent<MavManualPilotCommandSource>();
                rig.controlLaw = rig.gameObject.AddComponent<MavF16ControlLawV01>();
                rig.engine = rig.gameObject.AddComponent<MavF16EnginePowerModel>();

                if (withOwnershipController)
                    rig.ownership = rig.gameObject.AddComponent<MavPhysicsOwnershipController>();

                if (withLegacyOwner)
                    rig.legacyOwner = rig.gameObject.AddComponent<MavIntegrationTestLegacyOwner>();

                // Wire exactly as the runtime auto-setup does.
                rig.body.profileProvider = rig.profile;
                rig.body.aerodynamicModel = rig.aeroModel;
                rig.body.controlSurfaceActuator = rig.actuator;
                rig.body.controlLaw = rig.controlLaw;
                rig.body.pilotCommandSource = rig.commandSource;
                rig.body.propulsionModel = rig.engine;
                rig.body.simulationEnabled = false;

                rig.actuator.sixDoFBody = rig.body;
                rig.controlLaw.sixDoFBody = rig.body;
                rig.controlLaw.actuator = rig.actuator;
                rig.controlLaw.commandSource = rig.commandSource;
                rig.controlLaw.driveActuatorInFixedUpdate = true;
                rig.controlLaw.ResetLawState();

                // Point ownership detection at the test stand-in, so no real legacy component is
                // ever touched by these tests.
                rig.body.conflictingLegacyPhysicsComponents =
                    new string[] { MavIntegrationTestLegacyOwner.TypeName };

                if (rig.ownership != null)
                {
                    rig.ownership.sixDoFBody = rig.body;
                    rig.ownership.autoTransitionToNewFdm = false;
                    rig.ownership.legacyPhysicsOwners =
                        new string[] { MavIntegrationTestLegacyOwner.TypeName };

                    // Start the controller in the state that is actually true for this rig, rather
                    // than defaulting to a legacy owner that may not exist.
                    rig.ownership.state = withLegacyOwner
                        ? MavPhysicsOwnershipState.LegacyOwned
                        : MavPhysicsOwnershipState.Unowned;
                }

                // Awake/OnEnable do not run in edit mode, so do explicitly what they would have.
                rig.body.ApplyConfiguredProfile(true);
                rig.body.NotifyOwnershipChanged();

                return rig;
            }

            /// <summary>Makes every operational readiness criterion satisfiable.</summary>
            public void MakeOperationallyLiveReady()
            {
                commandSource.treatAsOperationalSource = true;
                commandSource.commandAvailable = true;
                commandSource.command = MavPilotCommand.Neutral;

                // No thrust deck exists, so the operator must explicitly accept non-authoritative
                // propulsion. This is the deliberate acknowledgement, not a default.
                body.acceptNonAuthoritativePropulsion = true;
            }

            public void SetFlightCondition(float altitudeM, float trueAirspeedMps)
            {
                transform.position = new Vector3(0f, altitudeM, 0f);
                transform.rotation = Quaternion.identity;
                rigidbody.linearVelocity = new Vector3(0f, 0f, trueAirspeedMps);
            }

            /// <summary>One step, in production execution order.</summary>
            public void Step()
            {
                stepTime += FixedDeltaTime;

                controlLaw.StepControlLaw(FixedDeltaTime);

                if (ownership != null)
                    ownership.StepOwnership();

                actuator.StepActuator(FixedDeltaTime);
                body.StepPhysics(FixedDeltaTime, stepTime);

                if (legacyOwner != null)
                    legacyOwner.StepLegacyOwner();
            }

            /// <summary>
            /// One step with ownership arbitrated BEFORE the control law, reproducing the ordering
            /// that caused the no-owner gap. Used only to demonstrate why the order matters.
            /// </summary>
            public void StepWithOwnershipBeforeControlLaw()
            {
                stepTime += FixedDeltaTime;

                ownership.StepOwnership();
                controlLaw.StepControlLaw(FixedDeltaTime);
                actuator.StepActuator(FixedDeltaTime);
            }

            public void Destroy()
            {
                if (gameObject != null)
                    Object.DestroyImmediate(gameObject);

                gameObject = null;
            }
        }

        private static void Record(bool ok, string name, StringBuilder report, ref int passed, ref int failed)
        {
            if (ok)
            {
                passed++;
                report.Append("  PASS  ").AppendLine(name);
            }
            else
            {
                failed++;
                report.Append("  FAIL  ").AppendLine(name);
            }
        }
    }
}
#endif
