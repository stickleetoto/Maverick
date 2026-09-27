using UnityEngine;

namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>Which pilot-command source the rig wires.</summary>
    public enum MavF15PilotCommandSourceKind
    {
        /// <summary>A human at the keyboard (<see cref="MavKeyboardPilotCommandSource"/>).</summary>
        Keyboard = 0,

        /// <summary>A scripted source (<see cref="MavManualPilotCommandSource"/>), declared operational - tests.</summary>
        Scripted = 1
    }

    /// <summary>
    /// Which Maverick pilot-control law flies the rig. Both are MAVERICK_TUNED_NON_AUTHORITATIVE and neither
    /// is the F-15 FCS; see Docs/Reference/F15_PILOT_CONTROLLED_V1.md and F15_PILOT_CONTROLLED_V2.md.
    /// </summary>
    public enum MavF15PilotControlMode
    {
        /// <summary>V1: direct stick-to-surface gearing about the trim (<see cref="MavF15PilotControlLaw"/>). The regression baseline.</summary>
        DirectV1 = 0,

        /// <summary>V2: closed-loop rate / sideslip augmentation (<see cref="MavF15PilotControlLawV2"/>).</summary>
        AssistedV2 = 1
    }

    /// <summary>
    /// The pilot-controlled F-15 research aircraft (V1) as ONE rig: the component stack, its wiring,
    /// the trim start and the ownership entry, in one place, so the aircraft a human flies and the
    /// aircraft the Play Mode validation flies are built by the same code.
    ///
    /// STACK (all on this GameObject): Rigidbody -> <see cref="MavF15PilotControlledFlightDynamicsProfile"/>
    /// -> <see cref="MavF15PilotControlledAeroModel"/> (six-axis AFIT/Baumann/Davison research, explicit opt-in) -> pilot
    /// command source -> <see cref="MavF15PilotControlLaw"/> -> <see cref="MavF15ControlActuator"/> (travel =
    /// the gameplay envelopes, MaverickTuning, no rate) -> <see cref="MavF15PilotControlledFixedThrust"/> ->
    /// <see cref="MavSixDoFBody"/>. Path: MavPilotCommand -> pilot-control law -> requested F-15 surfaces ->
    /// actuator -> actual surfaces -> research aero -> load set -> six-DoF body. One actual-surface owner,
    /// one aerodynamic model, one thrust, one load application.
    ///
    /// START, on the first physics step (execution order -500, before the ownership authority, the law,
    /// the actuator and the body): the validated trim (<see cref="MavF15PilotTrimStart"/>) is put on the
    /// Rigidbody through the body's own initializer - the only non-physics state write, before arming -
    /// then the ownership authority is attached with the pilot-controlled safety hold released for THIS
    /// rig, and the pilot-controlled owner is entered. Refused anywhere: nothing is armed, and the status
    /// says why.
    ///
    /// CONTROL LAW (<see cref="controlMode"/>): Direct V1 (default - the regression baseline, and the only law
    /// a V1 rig carries) or Assisted V2. Exactly one pilot-control law is enabled and bound to the body; the
    /// other, when present, is disabled, so there is never a second surface requester.
    /// <see cref="TrySetControlMode"/> switches in flight with the stick and pedals centred.
    ///
    /// NOT the frozen research validation rig (untouched), not NASA 836, not the production F-15 FCS.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    public sealed class MavF15PilotControlledRig : MonoBehaviour
    {
        [Header("Rig")]
        public MavF15PilotCommandSourceKind commandSourceKind = MavF15PilotCommandSourceKind.Keyboard;

        [Tooltip("Direct V1 (the regression baseline) or Assisted V2. Switch in flight with TrySetControlMode (HUD: F2).")]
        public MavF15PilotControlMode controlMode = MavF15PilotControlMode.DirectV1;

        [Tooltip("Start from the validated trim and enter the pilot-controlled owner on the first physics step.")]
        public bool startOnFirstPhysicsStep = true;

        [Tooltip("Released ONLY on this rig's own ownership authority, when it is created at start.")]
        public bool releasePilotControlledSafetyHold = true;

        [Header("Wired Stack (filled by EnsureStack)")]
        public Rigidbody body3d;
        public MavF15PilotControlledFlightDynamicsProfile profile;
        public MavF15PilotControlledAeroModel aero;
        public MavPilotCommandSourceBase commandSource;
        public MavF15PilotControlLaw law;
        public MavF15PilotControlLawV2 lawV2;
        public MavF15ControlActuator actuator;
        public MavF15PilotControlledFixedThrust thrust;
        public MavSixDoFBody body;
        public MavFlightPhysicsOwnership ownership;

        [Header("Debug")]
        public bool debugStarted;
        public bool debugStartSucceeded;
        public string debugStartStatus = "not started";
        public MavF15PilotControlledDiagnostics debugDiagnostics;

        /// <summary>
        /// Builds a rig on a new GameObject, configured BEFORE its stack is built (the object is created
        /// inactive, so Awake runs only on activation). Used by validation, which drives the start itself.
        /// </summary>
        public static MavF15PilotControlledRig Create(
            string name, MavF15PilotCommandSourceKind sourceKind, bool startOnFirstPhysicsStep)
        {
            return Create(name, sourceKind, startOnFirstPhysicsStep, MavF15PilotControlMode.DirectV1);
        }

        public static MavF15PilotControlledRig Create(
            string name, MavF15PilotCommandSourceKind sourceKind, bool startOnFirstPhysicsStep, MavF15PilotControlMode mode)
        {
            GameObject go = new GameObject(name);
            go.SetActive(false);
            MavF15PilotControlledRig rig = go.AddComponent<MavF15PilotControlledRig>();
            rig.commandSourceKind = sourceKind;
            rig.startOnFirstPhysicsStep = startOnFirstPhysicsStep;
            rig.controlMode = mode;
            go.SetActive(true);
            return rig;
        }

        /// <summary>
        /// As above, flying the given pilot physics revision: the profile is created first, with its revision set,
        /// so the stack is wired for that revision from the start.
        /// </summary>
        public static MavF15PilotControlledRig Create(
            string name, MavF15PilotCommandSourceKind sourceKind, bool startOnFirstPhysicsStep, MavF15PilotControlMode mode,
            MavF15PilotPhysicsRevision revision)
        {
            GameObject go = new GameObject(name);
            go.SetActive(false);
            MavF15PilotControlledFlightDynamicsProfile profile = go.AddComponent<MavF15PilotControlledFlightDynamicsProfile>();
            profile.physicsRevision = revision;
            MavF15PilotControlledRig rig = go.AddComponent<MavF15PilotControlledRig>();
            rig.commandSourceKind = sourceKind;
            rig.startOnFirstPhysicsStep = startOnFirstPhysicsStep;
            rig.controlMode = mode;
            go.SetActive(true);
            return rig;
        }

        /// <summary>The pilot-control law bound to the body: V1 or V2, per <see cref="controlMode"/>.</summary>
        public MavFlightControlLawBase ActiveLaw
        {
            get { return controlMode == MavF15PilotControlMode.AssistedV2 ? (MavFlightControlLawBase)lawV2 : law; }
        }

        private void Awake()
        {
            EnsureStack();
        }

        private void FixedUpdate()
        {
            if (startOnFirstPhysicsStep && !debugStarted)
                StartFromTrim();

            debugDiagnostics = MavF15PilotControlledDiagnostics.Evaluate(body);
        }

        /// <summary>
        /// Adds whatever part of the stack is missing and wires all of it. Idempotent: a prefab that
        /// already carries the components is only wired.
        /// </summary>
        public void EnsureStack()
        {
            // No interpolation: MavSixDoFBody builds its flight state from the Transform, which must be
            // the physics pose, never an interpolated render pose.
            body3d = Get<Rigidbody>();
            body3d.interpolation = RigidbodyInterpolation.None;
            body3d.collisionDetectionMode = CollisionDetectionMode.Discrete;
            body3d.useGravity = false;

            profile = Get<MavF15PilotControlledFlightDynamicsProfile>();

            aero = Get<MavF15PilotControlledAeroModel>();
            aero.acknowledgeResearchAerodynamics = true;

            commandSource = ResolveCommandSource();

            law = Get<MavF15PilotControlLaw>();

            // A V1 rig carries only the V1 law. V2 is added when selected (or kept when already present).
            lawV2 = controlMode == MavF15PilotControlMode.AssistedV2
                ? Get<MavF15PilotControlLawV2>()
                : GetComponent<MavF15PilotControlLawV2>();
            actuator = Get<MavF15ControlActuator>();
            actuator.limits = profile.gameplayControlAuthority.ToActuatorTravel();
            // The profile's physics revision decides the actuator dynamics: none for R1 (the recorded plant), the
            // research model's own first-order lags for R2. Travel and every other part of the stack are unchanged.
            actuator.dynamics = MavF15PilotPhysics.ActuatorDynamicsFor(profile.physicsRevision);
            thrust = Get<MavF15PilotControlledFixedThrust>();
            body = Get<MavSixDoFBody>();

            body.profileProvider = profile;
            body.aerodynamicModel = aero;
            body.controlSurfaceActuator = actuator;
            body.pilotCommandSource = commandSource;
            body.propulsionModel = thrust;
            body.autoApplyProfileConfiguration = true;
            body.applyMassPropertiesOnEnable = true;
            body.zeroUnityDampingWhenEnabled = true;

            // The research thrust is a non-authoritative research constant; accepting it is the deliberate
            // acknowledgement the readiness gate asks for, not a claim that it is authoritative.
            body.acceptNonAuthoritativePropulsion = true;
            body.requireOperationalReadinessForLoadApplication = true;
            body.allowStructuralOnlyLoadApplication = false;
            body.applyBackendGyroscopicCompensation = true;

            law.configuration = profile;
            law.sixDoFBody = body;
            law.actuator = actuator;
            law.commandSource = commandSource;
            law.driveActuatorInFixedUpdate = true;
            BindControlLaw();
            actuator.sixDoFBody = body;
            aero.surfaceOwner = actuator;

            body.ApplyConfiguredProfile(true);
            body.NotifyOwnershipChanged();
        }

        /// <summary>
        /// Binds the selected pilot-control law and only that: the V2 law is added if V2 is selected and
        /// missing, wired to the same profile, actuator and command source as V1; the selected law is enabled
        /// and becomes the body's law, the other is disabled - never two surface requesters. Touches nothing
        /// else (no mass, profile or physics state), so it is safe in flight.
        /// </summary>
        private void BindControlLaw()
        {
            if (controlMode == MavF15PilotControlMode.AssistedV2 && lawV2 == null)
                lawV2 = Get<MavF15PilotControlLawV2>();

            if (lawV2 != null)
            {
                lawV2.configuration = profile;
                lawV2.sixDoFBody = body;
                lawV2.actuator = actuator;
                lawV2.commandSource = commandSource;
                lawV2.driveActuatorInFixedUpdate = true;
            }

            law.enabled = controlMode == MavF15PilotControlMode.DirectV1;
            if (lawV2 != null)
                lawV2.enabled = controlMode == MavF15PilotControlMode.AssistedV2;
            body.controlLaw = ActiveLaw;
        }

        /// <summary>
        /// Puts the aircraft at the validated trim and hands it to the pilot-controlled owner. Returns
        /// false, arming nothing, when any step is refused; <see cref="debugStartStatus"/> says which.
        /// </summary>
        public bool StartFromTrim()
        {
            debugStarted = true;
            debugStartSucceeded = false;

            if (body == null)
                EnsureStack();

            Vector3 position, velocity, angularVelocity;
            Quaternion rotation;
            string reason;
            MavF15PilotTrimStart trim = profile.trimStart;
            if (!MavF15AfitResearchStateInjection.TryComputeKinematics(
                    trim.ToKinematicState(), out position, out rotation, out velocity, out angularVelocity, out reason))
                return Fail("trim start refused: " + reason);

            if (!body.TryApplyInitialKinematicState(position, rotation, velocity, angularVelocity, out reason))
                return Fail(reason);

            // Surfaces at the trim before the first armed step: the law publishes the neutral-stick
            // request (the trim bias) and the actuator takes it, so the first loads are the trim loads.
            MavPilotCommand neutral = MavPilotCommand.Neutral;
            MavF15PilotControlSolution atTrim = MavF15PilotControlMapping.Solve(
                neutral, profile.gameplayControlAuthority, (float)trim.symmetricStabilatorDeg);
            actuator.SetF15Command(MavF15RequestedSurfaceState.From(atTrim.requested), 0f);
            actuator.SnapToBoundedCommand();
            if (lawV2 != null)
                lawV2.ResetLawState();

            ownership = GetComponent<MavFlightPhysicsOwnership>();
            if (ownership == null)
                ownership = gameObject.AddComponent<MavFlightPhysicsOwnership>();
            ownership.allowF15PilotControlledOwnership = releasePilotControlledSafetyHold;
            body.physicsOwnership = ownership;
            ownership.AttachGovernedBody(body);
            body.NotifyOwnershipChanged();

            if (!ownership.TryEnterF15PilotControlledOwnership(body, MavF15PilotControlledOwnershipGrant.Instance, out reason))
                return Fail(reason);

            debugStartSucceeded = true;
            debugStartStatus = "STARTED at Table VII point " + trim.tableViiPoint + " trim; "
                + MavF15PilotControlledIdentity.ConfigurationId + " is the sole live owner";
            return true;
        }

        /// <summary>Largest stick / pedal deflection at which a law switch is accepted.</summary>
        public const float SwitchCentredTolerance = 0.05f;

        public const string SwitchRefusedNoCommandSource = "law switch refused: no pilot command source is wired";
        public const string SwitchRefusedCommandSourceInactive = "law switch refused: the pilot command source is inactive or disabled";
        public const string SwitchRefusedCommandUnavailable = "law switch refused: the current pilot command is unavailable";
        public const string SwitchRefusedCommandNotFinite = "law switch refused: the current pilot command is not finite";
        public const string SwitchRefusedNotCentred = "law switch refused: centre the stick and pedals first";

        /// <summary>
        /// Switches the pilot-control law - Direct V1 or Assisted V2 - on this rig, in flight or not.
        ///
        /// FAIL-CLOSED: unless <paramref name="mode"/> is already bound and active, the switch proceeds only when
        /// the command source is wired, active and enabled, its current command can be read, and pitch, roll and yaw
        /// are each finite and within <see cref="SwitchCentredTolerance"/>. Any other case is refused and changes
        /// nothing - not the mode, the bound law, which law is enabled, the law state, the surface request or the
        /// ownership. Near the trim both laws request about the trim bias and zero lateral surfaces, so an accepted
        /// switch does not step the surfaces. The newly selected law starts with zero filter state; the other is
        /// disabled, so there is never a second requester. The ownership grant, re-checked every physics step,
        /// accepts either law.
        /// </summary>
        public bool TrySetControlMode(MavF15PilotControlMode mode, out string reason)
        {
            if (IsBoundAndActive(mode))
            {
                reason = "already " + mode;
                return true;
            }

            if (!SwitchPreconditionsHold(out reason))
                return false;

            controlMode = mode;
            if (body == null || law == null)
            {
                EnsureStack();
            }
            else
            {
                BindControlLaw();
                body.NotifyOwnershipChanged();
            }

            if (lawV2 != null)
                lawV2.ResetLawState();

            // Inside a physics step a newly enabled (or newly added) law may not get its own FixedUpdate until the
            // next step, and the body - which re-checks readiness after the change - would then find no command
            // this step and withhold EVERY load, gravity included. So the new law is evaluated here, once, from the
            // same published flight state it would read at its own turn: the surfaces get its request this step
            // and the body keeps flying. V2 then skips its own turn in this same step (no double filter step).
            if (Time.inFixedTimeStep && body != null)
            {
                if (controlMode == MavF15PilotControlMode.AssistedV2)
                    lawV2.StepPilotControlLawForThisPhysicsStep(Time.fixedDeltaTime);
                else
                    law.StepPilotControlLaw(Time.fixedDeltaTime);
            }
            reason = "pilot-control law switched to " + mode + ": " + ActiveLaw.ControlLawName;
            return true;
        }

        /// <summary>
        /// The requested law is the selected one, bound to the body, active and enabled, and the other law is not
        /// enabled: nothing to switch, so no command read is needed.
        /// </summary>
        private bool IsBoundAndActive(MavF15PilotControlMode mode)
        {
            if (mode != controlMode || body == null)
                return false;
            MavFlightControlLawBase active = ActiveLaw;
            MavFlightControlLawBase other = mode == MavF15PilotControlMode.AssistedV2 ? (MavFlightControlLawBase)law : lawV2;
            return active != null && body.controlLaw == active && active.isActiveAndEnabled && (other == null || !other.enabled);
        }

        /// <summary>The switch gate. Reads the command source only; changes nothing.</summary>
        private bool SwitchPreconditionsHold(out string reason)
        {
            if (commandSource == null)
            {
                reason = SwitchRefusedNoCommandSource;
                return false;
            }

            if (!commandSource.isActiveAndEnabled)
            {
                reason = SwitchRefusedCommandSourceInactive + " (" + commandSource.CommandSourceName + ")";
                return false;
            }

            MavPilotCommand c;
            if (!commandSource.TryGetCommand(out c))
            {
                reason = SwitchRefusedCommandUnavailable + " (" + commandSource.CommandSourceName + ")";
                return false;
            }

            if (!IsFinite(c.pitch) || !IsFinite(c.roll) || !IsFinite(c.yaw))
            {
                reason = SwitchRefusedCommandNotFinite;
                return false;
            }

            if (Mathf.Abs(c.pitch) > SwitchCentredTolerance || Mathf.Abs(c.roll) > SwitchCentredTolerance
                || Mathf.Abs(c.yaw) > SwitchCentredTolerance)
            {
                reason = SwitchRefusedNotCentred + " (|pitch|, |roll|, |yaw| <= " + SwitchCentredTolerance + ")";
                return false;
            }

            reason = null;
            return true;
        }

        private static bool IsFinite(float v)
        {
            return !float.IsNaN(v) && !float.IsInfinity(v);
        }

        private bool Fail(string reason)
        {
            debugStartStatus = "START REFUSED (nothing armed): " + reason;
            Debug.LogWarning("[Maverick/F-15 pilot-controlled] " + debugStartStatus, this);
            return false;
        }

        private MavPilotCommandSourceBase ResolveCommandSource()
        {
            if (commandSourceKind == MavF15PilotCommandSourceKind.Scripted)
            {
                MavManualPilotCommandSource scripted = Get<MavManualPilotCommandSource>();
                scripted.treatAsOperationalSource = true;
                scripted.commandAvailable = true;
                return scripted;
            }

            return Get<MavKeyboardPilotCommandSource>();
        }

        private T Get<T>() where T : Component
        {
            T c = GetComponent<T>();
            return c != null ? c : gameObject.AddComponent<T>();
        }
    }
}
