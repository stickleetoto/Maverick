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
    /// NOT the frozen research validation rig (untouched), not NASA 836, not the production F-15 FCS.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    public sealed class MavF15PilotControlledRig : MonoBehaviour
    {
        [Header("Rig")]
        public MavF15PilotCommandSourceKind commandSourceKind = MavF15PilotCommandSourceKind.Keyboard;

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
            GameObject go = new GameObject(name);
            go.SetActive(false);
            MavF15PilotControlledRig rig = go.AddComponent<MavF15PilotControlledRig>();
            rig.commandSourceKind = sourceKind;
            rig.startOnFirstPhysicsStep = startOnFirstPhysicsStep;
            go.SetActive(true);
            return rig;
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
            actuator = Get<MavF15ControlActuator>();
            actuator.limits = profile.gameplayControlAuthority.ToActuatorTravel();
            thrust = Get<MavF15PilotControlledFixedThrust>();
            body = Get<MavSixDoFBody>();

            body.profileProvider = profile;
            body.aerodynamicModel = aero;
            body.controlSurfaceActuator = actuator;
            body.controlLaw = law;
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
            actuator.sixDoFBody = body;
            aero.surfaceOwner = actuator;

            body.ApplyConfiguredProfile(true);
            body.NotifyOwnershipChanged();
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
