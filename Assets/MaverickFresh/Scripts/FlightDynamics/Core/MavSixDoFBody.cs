using UnityEngine;

namespace MaverickFresh.FlightDynamics
{
    /// <summary>
    /// SI-unit six-DoF flight-dynamics boundary.
    ///
    /// This class is deliberately a thin integrator, not an aircraft brain. Per physics step it:
    ///
    ///   1. samples Rigidbody state
    ///   2. samples atmosphere
    ///   3. builds MavFlightState
    ///   4. evaluates the aerodynamic model
    ///   5. evaluates the propulsion model
    ///   6. sums the dimensional loads exactly once into a MavFlightDynamicsLoadSet
    ///   7. applies the total force and moment exactly once
    ///   8. publishes telemetry
    ///
    /// It does not read input, implement aircraft control laws, hold coefficient tables, align
    /// velocity vectors, or know anything about weapons, sensors, or AI.
    ///
    /// This component is the single final load-application boundary for the new FDM path.
    /// No control law, actuator, instructor, or player script may call Rigidbody.AddForce or
    /// Rigidbody.AddTorque for this path.
    ///
    /// IMPORTANT: simulationEnabled defaults to false so the new engine can coexist with the
    /// legacy Maverick flight stack without double-applying forces.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public class MavSixDoFBody : MonoBehaviour
    {
        [Header("Safety")]
        [Tooltip("Master gate for new-FDM load application. OFF by default; legacy flight keeps ownership until this is deliberately armed.")]
        public bool simulationEnabled = false;
        public bool applyMassPropertiesOnEnable = false;
        public bool zeroUnityDampingWhenEnabled = true;

        [Tooltip("When true (default), loads are applied only at OPERATIONALLY_LIVE_READY. Component presence alone is not enough to take physical ownership of an aircraft.")]
        public bool requireOperationalReadinessForLoadApplication = true;

        [Tooltip("Explicit operator override: apply loads at STRUCTURALLY_PREPARED. Intended for isolated bench testing of the physical path; logs a warning when it is what allows loads through.")]
        public bool allowStructuralOnlyLoadApplication = false;

        [Tooltip("Deliberate acknowledgement that a propulsion model reporting HasAuthoritativeData == false may still be flown. OFF by default: an aircraft with no frozen thrust data is not operationally live-ready.")]
        public bool acceptNonAuthoritativePropulsion = false;

        /// <summary>
        /// Canonical legacy physics owners. Exposed so validation can assert the shipped list
        /// without constructing a component. Cloned into each instance so an inspector edit on one
        /// aircraft cannot mutate the default for every other.
        /// </summary>
        public static readonly string[] DefaultConflictingLegacyPhysicsComponents =
        {
            "MavAeroBody",
            "MavAtmosphericEngine",
            "MavMouseFlightJet",
            "MavInstructorController",
            "MavThrustVectorControl"
        };

        [Tooltip("Component type names that own aircraft physics in the legacy stack. If any of these is present and enabled on this GameObject, the new path is not operationally live-ready, because two systems would own the same physical effect. Matched by type name so the new core takes no compile dependency on the stack it is replacing.")]
        public string[] conflictingLegacyPhysicsComponents =
            (string[])DefaultConflictingLegacyPhysicsComponents.Clone();

        [Header("Physical Profile")]
        public MavFlightDynamicsProfileProvider profileProvider;
        public bool autoApplyProfileConfiguration = true;
        public MavFlightDynamicsProfile activeProfile;

        [Header("Physics Models")]
        public MavAerodynamicModelBase aerodynamicModel;

        [Tooltip("Optional. When absent no propulsive load is contributed at all, which is a valid unpowered-glide configuration.")]
        public MavPropulsionModelBase propulsionModel;

        public MavMassProperties massProperties = new MavMassProperties();

        [Header("Control Path (references only; this component never evaluates a control law)")]
        [Tooltip("Optional. Held for readiness reporting and telemetry. The control law drives the actuator itself, ahead of this component.")]
        public MavFlightControlLawBase controlLaw;

        [Tooltip("Optional. Held for readiness reporting. The actuator publishes actual surface state into controlInput.")]
        public MavControlSurfaceActuatorBase controlSurfaceActuator;

        [Tooltip("Optional. Held for readiness reporting only; the control law reads it directly.")]
        public MavPilotCommandSourceBase pilotCommandSource;

        [Header("Control Input (actual physical surface state, published by the actuator)")]
        public MavControlInput controlInput;

        [Header("Telemetry")]
        [Tooltip("Optional sink. This component pushes one sample per physics step, so telemetry has no execution-order dependency.")]
        public MavFlightDynamicsTelemetry telemetry;

        [Header("Debug / State")]
        public bool debugProfileValid;
        public bool debugInsideProfileEnvelope;
        public string debugProfileStatus = "unconfigured";
        public MavAtmosphereSample debugAtmosphere;
        public MavFlightState debugState;
        public MavAeroCoefficients debugCoefficients;

        [Header("Debug / Loads")]
        [Tooltip("Every dimensional load for the current physics step, with per-source contribution counters.")]
        public MavFlightDynamicsLoadSet debugLoadSet;
        public MavAerodynamicLoads debugLoads;
        public Vector3 debugUnityLocalForceN;
        public Vector3 debugUnityLocalTorqueNm;
        public int debugPhysicsStepIndex;
        public int debugLoadApplications;
        public int debugRejectedDuplicateApplications;
        public int debugRejectedNonFiniteApplications;

        [Header("Debug / Readiness")]
        [Tooltip("Structural preparation only: every part present and wired. This is NOT permission to fly.")]
        public bool debugReadyForLiveFdm;
        public string debugReadinessReason = "not evaluated";

        [Tooltip("Full readiness judgement, split into STRUCTURALLY_PREPARED and OPERATIONALLY_LIVE_READY.")]
        public MavFlightDynamicsReadinessReport debugReadiness;
        public MavFlightDynamicsReadinessInputs debugReadinessInputs;
        public string debugLegacyPhysicsOwner = "none";
        public int debugRejectedNotLiveReadyApplications;

        private Rigidbody rb;
        private bool ownershipInitialized;
        private float lastAppliedFixedTime = float.NegativeInfinity;
        private bool loggedDuplicateRejection;
        private bool loggedNonFiniteRejection;
        private bool loggedNotLiveReadyRejection;
        private bool loggedStructuralOnlyOverride;
        private int lastReadinessMask = -1;
        private bool readinessEvaluatedOnce;

        /// <summary>Physics steps between legacy-ownership component rescans. 25 steps is 0.5 s at the default fixed timestep.</summary>
        private const int LegacyOwnershipScanIntervalSteps = 25;

        private readonly System.Collections.Generic.List<MonoBehaviour> behaviourScratch =
            new System.Collections.Generic.List<MonoBehaviour>(32);
        private int legacyOwnershipScanCountdown;
        private bool cachedLegacyOwnershipConflict;
        private string cachedLegacyOwnerName = "none";

        private void Awake()
        {
            Resolve();
            if (autoApplyProfileConfiguration)
                ApplyConfiguredProfile(false);
        }

        private void OnEnable()
        {
            Resolve();

            if (autoApplyProfileConfiguration)
                ApplyConfiguredProfile(false);

            // InitializePhysicsOwnership writes Rigidbody damping and gravity flags, which is a
            // physical ownership change. It must pass the same readiness gate the per-step load
            // application does, or an armed-but-not-ready stack could still alter the aircraft.
            if (simulationEnabled)
            {
                EvaluateReadinessReport();
                if (IsLoadApplicationPermitted())
                    InitializePhysicsOwnership();
            }
        }

        private void FixedUpdate()
        {
            Resolve();
            debugPhysicsStepIndex++;

            UpdateStateAndAtmosphere();
            UpdateProfileDebug();
            UpdateReadinessDebug();

            debugLoadSet.BeginStep(debugPhysicsStepIndex);

            if (!simulationEnabled || rb == null || aerodynamicModel == null)
            {
                ClearLoadDebug();
                PublishTelemetry(false);
                return;
            }

            // Readiness gate. simulationEnabled says "someone armed this"; readiness says whether
            // arming it is actually correct. Component presence alone must never be sufficient to
            // take physical ownership of an aircraft.
            if (!IsLoadApplicationPermitted())
            {
                ClearLoadDebug();
                PublishTelemetry(false);
                return;
            }

            if (!ownershipInitialized)
                InitializePhysicsOwnership();

            // Defence in depth. The actuator is the primary limiter, but this boundary must not
            // hand the aerodynamic model a deflection the airframe cannot physically reach.
            MavControlInput boundedInput = controlInput;
            if (activeProfile != null && debugProfileValid)
                boundedInput = activeProfile.controlSurfaceLimits.Clamp(controlInput);

            // --- aerodynamic contribution (exactly once) ---
            debugCoefficients = aerodynamicModel.Evaluate(
                debugState,
                boundedInput,
                debugAtmosphere
            );

            debugLoads = MavFlightDynamicsMath.Dimensionalize(
                debugCoefficients,
                aerodynamicModel.referenceGeometry,
                debugState.dynamicPressurePa
            );

            debugLoadSet.AddAerodynamic(debugLoads);

            // --- propulsive contribution (exactly once, and only if a model exists) ---
            if (propulsionModel != null)
            {
                MavPropulsiveLoads propulsive = propulsionModel.Evaluate(
                    debugState,
                    debugAtmosphere,
                    boundedInput.throttle01,
                    Time.fixedDeltaTime
                );

                debugLoadSet.AddPropulsive(propulsive);
            }

            // --- publish the measured specific force (accelerometer channel) ---
            // Done here, from the summed load set, so a control law never has to compute an
            // aerodynamic force itself. The value belongs to the state that produced it, so the
            // snapshot a control law reads next step is internally consistent: its alpha, its body
            // rates and its load factor all come from the same physics step.
            PublishSpecificForce();

            // --- single application boundary ---
            bool applied = TryApplyLoadSet();
            PublishTelemetry(applied);
        }

        public void SetControlInput(MavControlInput input)
        {
            controlInput = input;
        }

        /// <summary>
        /// Applies the accumulated load set to the Rigidbody exactly once for this physics step.
        /// Refuses to apply when the set was already applied, when a source contributed more than
        /// once, or when the total is not finite.
        /// </summary>
        private bool TryApplyLoadSet()
        {
            if (ShouldRejectDuplicateApplication(lastAppliedFixedTime, Time.fixedTime))
            {
                debugRejectedDuplicateApplications++;
                if (!loggedDuplicateRejection)
                {
                    loggedDuplicateRejection = true;
                    Debug.LogError(
                        "[Maverick/FDM] Refused a second load application within one physics step. "
                        + "Something other than MavSixDoFBody.FixedUpdate is driving load application.",
                        this
                    );
                }
                return false;
            }

            if (!debugLoadSet.IsFinite())
            {
                debugRejectedNonFiniteApplications++;
                if (!loggedNonFiniteRejection)
                {
                    loggedNonFiniteRejection = true;
                    Debug.LogError(
                        "[Maverick/FDM] Refused a non-finite load set (NaN/Infinity). "
                        + "Rigidbody state was left untouched.",
                        this
                    );
                }
                ClearUnityLoadDebug();
                return false;
            }

            string reason;
            if (!debugLoadSet.TryMarkApplied(out reason))
            {
                debugRejectedDuplicateApplications++;
                if (!loggedDuplicateRejection)
                {
                    loggedDuplicateRejection = true;
                    Debug.LogError("[Maverick/FDM] Load application refused: " + reason, this);
                }
                ClearUnityLoadDebug();
                return false;
            }

            debugUnityLocalForceN = MavFlightDynamicsMath.AeroBodyVectorToUnityLocal(
                debugLoadSet.totalForceAeroBodyN
            );
            debugUnityLocalTorqueNm = MavFlightDynamicsMath.AeroBodyMomentToUnityLocal(
                debugLoadSet.totalMomentAeroBodyNm
            );

            // ForceMode.Force / Newtons: the load set is already dimensional, so Unity must not
            // reinterpret it as an acceleration.
            rb.AddRelativeForce(debugUnityLocalForceN, ForceMode.Force);
            rb.AddRelativeTorque(debugUnityLocalTorqueNm, ForceMode.Force);

            lastAppliedFixedTime = Time.fixedTime;
            debugLoadApplications++;
            return true;
        }

        /// <summary>
        /// Pure duplicate-application guard. A load application is refused when one already
        /// happened at the same fixed time, which is the signature of two callers believing they
        /// own the load-application boundary.
        /// </summary>
        public static bool ShouldRejectDuplicateApplication(float lastAppliedFixedTime, float currentFixedTime)
        {
            if (float.IsNegativeInfinity(lastAppliedFixedTime))
                return false;

            return lastAppliedFixedTime == currentFixedTime;
        }

        /// <summary>
        /// STRUCTURAL preparation only: is the new FDM path wired completely enough to be a
        /// candidate for physical ownership?
        ///
        /// This is deliberately NOT permission to fly. Use <see cref="IsOperationallyLiveReady"/>
        /// for that question; component presence is not a safety gate.
        /// </summary>
        public bool IsReadyForLiveFdm(out string reason)
        {
            Resolve();

            return EvaluateReadiness(
                rb != null,
                activeProfile != null && debugProfileValid,
                aerodynamicModel != null,
                controlSurfaceActuator != null,
                controlLaw != null,
                propulsionModel != null,
                out reason
            );
        }

        /// <summary>
        /// Pure STRUCTURAL readiness rule, kept static so validation can exercise the exact
        /// production logic without constructing a GameObject.
        ///
        /// Structural readiness is about wiring completeness, not data quality or safety. A
        /// propulsion model that honestly reports zero thrust satisfies it; whether that model may
        /// be flown is an operational question answered by
        /// <see cref="MavFlightDynamicsReadiness.EvaluateOperational"/>.
        ///
        /// The rule itself now lives in <see cref="MavFlightDynamicsReadiness"/>; this overload is
        /// retained because it is the established entry point for existing callers and validation.
        /// </summary>
        public static bool EvaluateReadiness(
            bool hasRigidbody,
            bool hasValidProfile,
            bool hasAerodynamicModel,
            bool hasControlSurfaceActuator,
            bool hasControlLaw,
            bool hasPropulsionModel,
            out string reason)
        {
            MavFlightDynamicsReadinessInputs inputs = new MavFlightDynamicsReadinessInputs();
            inputs.hasRigidbody = hasRigidbody;
            inputs.hasValidProfile = hasValidProfile;
            inputs.hasAerodynamicModel = hasAerodynamicModel;
            inputs.hasControlSurfaceActuator = hasControlSurfaceActuator;
            inputs.hasControlLaw = hasControlLaw;
            inputs.hasPropulsionModel = hasPropulsionModel;

            bool structural = MavFlightDynamicsReadiness.EvaluateStructural(inputs, out reason);
            if (structural)
                reason = "READY";

            return structural;
        }

        /// <summary>
        /// Rebuilds the aircraft's physical profile and applies its geometry/mass configuration
        /// to the flight-dynamics engine. Rigidbody mass/inertia are only changed when applyMassNow
        /// is true, preserving safe coexistence with the legacy flight stack.
        /// </summary>
        public bool ApplyConfiguredProfile(bool applyMassNow)
        {
            Resolve();

            if (profileProvider == null)
            {
                activeProfile = null;
                debugProfileValid = false;
                debugProfileStatus = "No flight-dynamics profile provider.";
                return false;
            }

            activeProfile = profileProvider.BuildProfile();
            if (activeProfile == null)
            {
                debugProfileValid = false;
                debugProfileStatus = "Profile provider returned null.";
                return false;
            }

            string reason;
            debugProfileValid = activeProfile.IsValid(out reason);
            debugProfileStatus = activeProfile.profileId + ": " + reason;
            if (!debugProfileValid)
                return false;

            massProperties = activeProfile.massProperties;

            if (aerodynamicModel != null)
                aerodynamicModel.referenceGeometry = activeProfile.referenceGeometry;

            if (applyMassNow)
                ApplyConfiguredMassProperties();

            return true;
        }

        public void ApplyConfiguredMassProperties()
        {
            Resolve();
            if (rb != null && massProperties != null)
                massProperties.ApplyTo(rb);
        }

        private void Resolve()
        {
            if (rb == null)
                rb = GetComponent<Rigidbody>();

            if (aerodynamicModel == null)
                aerodynamicModel = GetComponent<MavAerodynamicModelBase>();

            if (profileProvider == null)
                profileProvider = GetComponent<MavFlightDynamicsProfileProvider>();

            if (propulsionModel == null)
                propulsionModel = GetComponent<MavPropulsionModelBase>();

            if (controlLaw == null)
                controlLaw = GetComponent<MavFlightControlLawBase>();

            if (controlSurfaceActuator == null)
                controlSurfaceActuator = GetComponent<MavControlSurfaceActuatorBase>();

            if (pilotCommandSource == null)
                pilotCommandSource = GetComponent<MavPilotCommandSourceBase>();

            if (telemetry == null)
                telemetry = GetComponent<MavFlightDynamicsTelemetry>();
        }

        private void InitializePhysicsOwnership()
        {
            if (rb == null)
                return;

            if (autoApplyProfileConfiguration)
                ApplyConfiguredProfile(false);

            if (applyMassPropertiesOnEnable && massProperties != null)
                massProperties.ApplyTo(rb);

            bool zeroLinearDamping = zeroUnityDampingWhenEnabled;
            bool zeroAngularDamping = zeroUnityDampingWhenEnabled;
            bool useGravity = true;

            if (activeProfile != null && debugProfileValid)
            {
                zeroLinearDamping = activeProfile.zeroUnityLinearDamping;
                zeroAngularDamping = activeProfile.zeroUnityAngularDamping;
                useGravity = activeProfile.useGravity;
            }

            if (zeroLinearDamping)
                rb.linearDamping = 0f;
            if (zeroAngularDamping)
                rb.angularDamping = 0f;

            rb.useGravity = useGravity;

            // Give the engine a defined starting power state instead of inheriting whatever the
            // component happened to hold from edit time.
            if (propulsionModel != null)
                propulsionModel.ResetEngineState(controlInput.throttle01);

            ownershipInitialized = true;
        }

        private void UpdateStateAndAtmosphere()
        {
            if (rb == null)
                return;

            float altitudeM = transform.position.y;
            debugAtmosphere = MavAtmosphereModel.Sample(altitudeM);

            Vector3 worldVelocity = rb.linearVelocity;
            Vector3 unityLocalVelocity = transform.InverseTransformDirection(worldVelocity);
            Vector3 aeroBodyVelocity = MavFlightDynamicsMath.UnityLocalVectorToAeroBody(unityLocalVelocity);

            Vector3 unityLocalAngularRate = transform.InverseTransformDirection(rb.angularVelocity);
            Vector3 aeroBodyRates = MavFlightDynamicsMath.UnityLocalAngularRateToAeroBody(unityLocalAngularRate);

            float tas = worldVelocity.magnitude;
            float speedOfSound = Mathf.Max(1f, debugAtmosphere.speedOfSoundMps);
            float q = 0.5f * debugAtmosphere.densityKgM3 * tas * tas;

            MavFlightState state = new MavFlightState();
            state.worldPositionM = transform.position;
            state.worldVelocityMps = worldVelocity;
            state.aeroBodyVelocityMps = aeroBodyVelocity;
            state.aeroBodyRatesRadSec = aeroBodyRates;
            state.trueAirspeedMps = tas;
            state.mach = tas / speedOfSound;
            state.dynamicPressurePa = q;
            state.alphaRad = MavFlightDynamicsMath.ComputeAlphaRad(aeroBodyVelocity);
            state.betaRad = MavFlightDynamicsMath.ComputeBetaRad(aeroBodyVelocity);
            debugState = state;
        }

        private void UpdateProfileDebug()
        {
            if (activeProfile == null || !debugProfileValid)
            {
                debugInsideProfileEnvelope = false;
                return;
            }

            debugInsideProfileEnvelope = activeProfile.envelope.Contains(debugState);
        }

        /// <summary>
        /// Refreshes the readiness debug fields using the already-resolved references, so the
        /// per-step path does not repeat component lookups.
        /// </summary>
        private void UpdateReadinessDebug()
        {
            debugReadinessInputs = BuildReadinessInputs();

            // Evaluate() composes explanatory strings, which must not happen on every physics step.
            // The inputs are twelve booleans, so a bitmask comparison tells us for free whether the
            // judgement can possibly have changed.
            int mask = debugReadinessInputs.ToBitmask();
            if (mask == lastReadinessMask && readinessEvaluatedOnce)
                return;

            lastReadinessMask = mask;
            readinessEvaluatedOnce = true;
            debugReadiness = MavFlightDynamicsReadiness.Evaluate(debugReadinessInputs);

            // Kept for backward compatibility: this field has always meant "structurally prepared".
            debugReadyForLiveFdm = debugReadiness.structurallyPrepared;
            debugReadinessReason = debugReadiness.structurallyPrepared
                ? debugReadiness.operationalReason
                : debugReadiness.structuralReason;
        }

        /// <summary>
        /// Gathers the observable facts about this stack for the readiness rule. Every lookup that
        /// can fail resolves to false, so an unknown answer is treated as not-ready.
        /// </summary>
        public MavFlightDynamicsReadinessInputs BuildReadinessInputs()
        {
            MavFlightDynamicsReadinessInputs inputs = new MavFlightDynamicsReadinessInputs();

            inputs.hasRigidbody = rb != null;
            inputs.hasValidProfile = activeProfile != null && debugProfileValid;
            inputs.hasAerodynamicModel = aerodynamicModel != null;
            inputs.hasControlSurfaceActuator = controlSurfaceActuator != null;
            inputs.hasControlLaw = controlLaw != null;
            inputs.hasPropulsionModel = propulsionModel != null;

            inputs.aerodynamicGeometryMatchesProfile =
                inputs.hasAerodynamicModel
                && inputs.hasValidProfile
                && GeometryMatches(aerodynamicModel.referenceGeometry, activeProfile.referenceGeometry);

            inputs.controlLawEnabledAndDriving =
                controlLaw != null
                && controlLaw.isActiveAndEnabled
                && controlLaw.driveActuatorInFixedUpdate
                && controlLaw.actuator != null
                && controlSurfaceActuator != null
                && IdentityMatches(controlSurfaceActuator, controlLaw.actuator);

            // Identity, not presence. A law wired to this actuator but reading a different body
            // would compute every command from another aircraft's airspeed, alpha and rates, and
            // nothing downstream would look wrong.
            inputs.controlLawBoundToThisBody =
                controlLaw != null
                && controlLaw.sixDoFBody != null
                && IdentityMatches(this, controlLaw.sixDoFBody);

            inputs.actuatorEnabledAndBound =
                controlSurfaceActuator != null
                && controlSurfaceActuator.isActiveAndEnabled
                && controlSurfaceActuator.BoundBody != null
                && IdentityMatches(this, controlSurfaceActuator.BoundBody);

            inputs.propulsionAccepted =
                propulsionModel != null
                && (propulsionModel.HasAuthoritativeData || acceptNonAuthoritativePropulsion);

            // The command path, proved end to end rather than assembled from separate objects.
            //
            // The body inspects one source; the control law reads another field. Those must be the
            // same object, or a declaration taken from source A gets combined with availability
            // observed on source B into a "valid" path that never existed.
            //
            // Availability is the control law's own observation from this step - it runs at -300
            // and this body at -100, so the result is already fresh - which makes it the observed
            // truth rather than a property a source could report incorrectly.
            MavPilotCommandSourceBase bodySource = ResolveCommandSource();
            MavPilotCommandSourceBase lawSource = controlLaw != null ? controlLaw.commandSource : null;

            inputs.commandSourceIdentityMatches =
                bodySource != null
                && lawSource != null
                && IdentityMatches(bodySource, lawSource);

            bool observedSourceSignal =
                controlLaw != null
                && controlLaw.debugCommandResolution
                    == MavFlightControlLawBase.MavCommandResolution.SourceSignal;

            inputs.hasValidCommandSource =
                MavPilotCommandSourceBase.EvaluatesAsLiveCommandPipeline(
                    bodySource != null,
                    controlLaw != null,
                    inputs.commandSourceIdentityMatches,
                    bodySource != null && bodySource.isActiveAndEnabled,
                    bodySource != null && bodySource.IsOperationalCommandSource,
                    observedSourceSignal);

            inputs.legacyPhysicsOwnershipClear = !HasLegacyPhysicsOwnerConflict();
            debugLegacyPhysicsOwner = cachedLegacyOwnerName;

            return inputs;
        }

        /// <summary>
        /// Legacy-ownership detection.
        ///
        /// While load application is armed this rescans EVERY physics step. Caching is a
        /// diagnostics optimization only, and a cached "no conflict" verdict is precisely the
        /// failure this gate exists to prevent: if a legacy owner is enabled mid-flight, a stale
        /// answer would let both systems apply forces to the same Rigidbody until the cache
        /// expired. There is no acceptable length for that window.
        ///
        /// When nothing is armed, the answer is only inspector diagnostics and is throttled.
        /// </summary>
        private bool HasLegacyPhysicsOwnerConflict()
        {
            legacyOwnershipScanCountdown--;

            if (!ShouldRescanLegacyOwnership(simulationEnabled, legacyOwnershipScanCountdown))
                return cachedLegacyOwnershipConflict;

            legacyOwnershipScanCountdown = LegacyOwnershipScanIntervalSteps;
            RefreshLegacyPhysicsOwner();
            return cachedLegacyOwnershipConflict;
        }

        /// <summary>
        /// Whether the legacy-ownership verdict may be answered from cache.
        ///
        /// Kept pure and static so the no-stale-window guarantee is directly testable: when load
        /// application is armed this must return true for every possible countdown value, so no
        /// schedule can ever produce a stale safety answer.
        /// </summary>
        public static bool ShouldRescanLegacyOwnership(bool loadApplicationArmed, int scanCountdown)
        {
            if (loadApplicationArmed)
                return true;

            return scanCountdown <= 0;
        }

        /// <summary>
        /// Forces an immediate legacy-ownership rescan and drops any cached verdict. Call this from
        /// an ownership controller, or after enabling/disabling a physics component, so readiness
        /// reflects the change on the very next evaluation instead of waiting for a scan interval.
        /// </summary>
        public void NotifyOwnershipChanged()
        {
            RefreshLegacyPhysicsOwner();
            legacyOwnershipScanCountdown = 0;

            // Force the readiness judgement itself to be rebuilt too, not just its inputs.
            readinessEvaluatedOnce = false;
            lastReadinessMask = -1;
        }

        /// <summary>Forces an immediate legacy-ownership rescan, for example after wiring changes.</summary>
        public void RefreshLegacyPhysicsOwner()
        {
            string offender;
            cachedLegacyOwnershipConflict = TryFindLegacyPhysicsOwner(out offender);
            cachedLegacyOwnerName = cachedLegacyOwnershipConflict ? offender : "none";
        }

        /// <summary>Full readiness judgement, refreshing component references first.</summary>
        public MavFlightDynamicsReadinessReport EvaluateReadinessReport()
        {
            Resolve();
            if (autoApplyProfileConfiguration && activeProfile == null)
                ApplyConfiguredProfile(false);

            // An explicit query must never be answered from a throttled cache: the caller is
            // typically asking right after changing the wiring.
            RefreshLegacyPhysicsOwner();
            legacyOwnershipScanCountdown = LegacyOwnershipScanIntervalSteps;

            debugReadinessInputs = BuildReadinessInputs();
            debugReadiness = MavFlightDynamicsReadiness.Evaluate(debugReadinessInputs);

            lastReadinessMask = debugReadinessInputs.ToBitmask();
            readinessEvaluatedOnce = true;
            debugReadyForLiveFdm = debugReadiness.structurallyPrepared;
            debugReadinessReason = debugReadiness.structurallyPrepared
                ? debugReadiness.operationalReason
                : debugReadiness.structuralReason;

            return debugReadiness;
        }

        /// <summary>
        /// True when the new path may take physical ownership of the aircraft. Strictly stronger
        /// than <see cref="IsReadyForLiveFdm"/>, which only asks whether the parts are present.
        /// </summary>
        public bool IsOperationallyLiveReady(out string reason)
        {
            MavFlightDynamicsReadinessReport report = EvaluateReadinessReport();
            reason = report.operationallyLiveReady ? report.operationalReason : report.summary;
            return report.operationallyLiveReady;
        }

        /// <summary>
        /// Decides whether this physics step is allowed to apply loads at all, given the readiness
        /// level. Rejections are counted and logged once so a stack that silently never flies is
        /// diagnosable from the inspector.
        /// </summary>
        private bool IsLoadApplicationPermitted()
        {
            if (!requireOperationalReadinessForLoadApplication)
                return true;

            if (debugReadiness.operationallyLiveReady)
                return true;

            if (debugReadiness.structurallyPrepared && allowStructuralOnlyLoadApplication)
            {
                if (!loggedStructuralOnlyOverride)
                {
                    loggedStructuralOnlyOverride = true;
                    Debug.LogWarning(
                        "[Maverick/FDM] Applying loads at STRUCTURALLY_PREPARED because "
                        + "allowStructuralOnlyLoadApplication is set. Operational readiness is not met: "
                        + debugReadiness.operationalReason,
                        this
                    );
                }
                return true;
            }

            debugRejectedNotLiveReadyApplications++;
            if (!loggedNotLiveReadyRejection)
            {
                loggedNotLiveReadyRejection = true;
                Debug.LogError(
                    "[Maverick/FDM] simulationEnabled is set but the stack is not operationally "
                    + "live-ready, so no loads were applied. " + debugReadiness.summary,
                    this
                );
            }
            return false;
        }

        /// <summary>
        /// Publishes the measured non-gravitational specific force in g, in aerodynamic body axes.
        ///
        /// The load set holds only aerodynamic and propulsive force; gravity is applied by the
        /// Rigidbody and never enters it. That is exactly what an accelerometer does not measure,
        /// so the total load divided by mass is already a specific force with no gravity term to
        /// subtract.
        /// </summary>
        private void PublishSpecificForce()
        {
            if (!debugLoadSet.IsFinite())
            {
                debugState.specificForceAeroBodyG = Vector3.zero;
                debugState.specificForceValid = false;
                return;
            }

            float massKg = rb != null
                ? rb.mass
                : (massProperties != null ? massProperties.massKg : 0f);

            debugState.specificForceAeroBodyG = ComputeSpecificForceG(
                debugLoadSet.totalForceAeroBodyN,
                massKg
            );
            debugState.specificForceValid = massKg > 0f;
        }

        /// <summary>
        /// Pure specific-force conversion: N -> g. Kept static so validation can pin the load-factor
        /// sign convention without a Rigidbody. A non-positive mass yields zero rather than an
        /// infinity that would poison a control loop.
        /// </summary>
        public static Vector3 ComputeSpecificForceG(Vector3 totalForceAeroBodyN, float massKg)
        {
            if (massKg <= 0f)
                return Vector3.zero;

            float scale = 1f / (massKg * MavControlLawProtections.StandardGravityMps2);
            return totalForceAeroBodyN * scale;
        }

        /// <summary>
        /// Looks for a legacy physics owner on this GameObject. Matching is by type name so the new
        /// core keeps no compile-time dependency on the legacy stack it is meant to replace, and so
        /// the list stays editable without touching legacy code.
        /// </summary>
        /// <summary>
        /// The deny-list rule, as a pure function: a component conflicts when it is enabled and its
        /// type name is on the list. A disabled component owns nothing, so it does not conflict.
        /// </summary>
        public static bool IsLegacyOwnershipConflict(
            string componentTypeName,
            bool componentEnabled,
            string[] denyList)
        {
            if (!componentEnabled || denyList == null || string.IsNullOrEmpty(componentTypeName))
                return false;

            for (int i = 0; i < denyList.Length; i++)
            {
                if (string.Equals(componentTypeName, denyList[i], System.StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        public bool TryFindLegacyPhysicsOwner(out string offenderName)
        {
            offenderName = "none";
            if (conflictingLegacyPhysicsComponents == null || conflictingLegacyPhysicsComponents.Length == 0)
                return false;

            // The List overload reuses its buffer, so this does not allocate on every scan.
            behaviourScratch.Clear();
            GetComponents(behaviourScratch);

            for (int i = 0; i < behaviourScratch.Count; i++)
            {
                MonoBehaviour behaviour = behaviourScratch[i];
                if (behaviour == null || !behaviour.enabled)
                    continue;

                string typeName = behaviour.GetType().Name;
                if (IsLegacyOwnershipConflict(typeName, behaviour.enabled, conflictingLegacyPhysicsComponents))
                {
                    offenderName = typeName;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Resolves the command source THIS body inspects.
        ///
        /// Deliberately never adopts the control law's field. Doing so would make the identity
        /// check vacuous: the body would silently agree with whatever the law happened to point at,
        /// which is exactly the mismatch the check exists to catch.
        /// </summary>
        private MavPilotCommandSourceBase ResolveCommandSource()
        {
            if (pilotCommandSource == null)
                pilotCommandSource = GetComponent<MavPilotCommandSourceBase>();

            return pilotCommandSource;
        }

        /// <summary>
        /// Reference identity, as a pure predicate: both operands must exist and be the same
        /// object. Two nulls are NOT a match - an unknown wiring state fails closed rather than
        /// being read as agreement.
        ///
        /// Typed as object so validation can exercise the rule with plain instances instead of
        /// needing real Unity components.
        /// </summary>
        public static bool IdentityMatches(object expected, object actual)
        {
            if (expected == null || actual == null)
                return false;

            // Unity overloads == for destroyed objects; ReferenceEquals answers the identity
            // question directly, and the null checks above already handle the destroyed case
            // through the overloaded operator.
            return ReferenceEquals(expected, actual);
        }

        private static bool GeometryMatches(MavAeroReferenceGeometry a, MavAeroReferenceGeometry b)
        {
            const float relativeTolerance = 1e-4f;
            return NearlyEqual(a.wingAreaM2, b.wingAreaM2, relativeTolerance)
                && NearlyEqual(a.wingSpanM, b.wingSpanM, relativeTolerance)
                && NearlyEqual(a.meanAerodynamicChordM, b.meanAerodynamicChordM, relativeTolerance);
        }

        private static bool NearlyEqual(float a, float b, float relativeTolerance)
        {
            float scale = Mathf.Max(1f, Mathf.Max(Mathf.Abs(a), Mathf.Abs(b)));
            return Mathf.Abs(a - b) <= relativeTolerance * scale;
        }

        private void PublishTelemetry(bool loadsApplied)
        {
            if (telemetry == null)
                return;

            MavFlightDynamicsTelemetrySample sample = new MavFlightDynamicsTelemetrySample();
            sample.timeSeconds = Time.fixedTime;
            sample.altitudeM = debugAtmosphere.altitudeM;
            sample.trueAirspeedMps = debugState.trueAirspeedMps;
            sample.mach = debugState.mach;
            sample.dynamicPressurePa = debugState.dynamicPressurePa;
            sample.alphaDeg = debugState.AlphaDeg;
            sample.betaDeg = debugState.BetaDeg;
            sample.loadFactorNz = debugState.specificForceValid ? debugState.LoadFactorNz : 0f;

            sample.rollRateDegSec = debugState.aeroBodyRatesRadSec.x * Mathf.Rad2Deg;
            sample.pitchRateDegSec = debugState.aeroBodyRatesRadSec.y * Mathf.Rad2Deg;
            sample.yawRateDegSec = debugState.aeroBodyRatesRadSec.z * Mathf.Rad2Deg;

            sample.commandedSurfaces = controlLaw != null ? controlLaw.debugLastOutput : controlInput;
            sample.actualSurfaces = controlSurfaceActuator != null
                ? controlSurfaceActuator.ActualSurfaceState
                : controlInput;

            sample.coefficients = debugCoefficients;
            sample.aeroForceAeroBodyN = debugLoadSet.aerodynamic.forceAeroBodyN;
            sample.aeroMomentAeroBodyNm = debugLoadSet.aerodynamic.momentAeroBodyNm;
            sample.propulsionForceAeroBodyN = debugLoadSet.propulsive.forceAeroBodyN;
            sample.propulsionMomentAeroBodyNm = debugLoadSet.propulsive.momentAeroBodyNm;
            sample.totalForceAeroBodyN = debugLoadSet.totalForceAeroBodyN;
            sample.totalMomentAeroBodyNm = debugLoadSet.totalMomentAeroBodyNm;

            sample.profileValid = debugProfileValid;
            sample.insideEnvelope = debugInsideProfileEnvelope;
            sample.loadsApplied = loadsApplied;
            sample.propulsionDataAuthoritative =
                propulsionModel != null && propulsionModel.HasAuthoritativeData;
            sample.aerodynamicContributions = debugLoadSet.aerodynamicContributions;
            sample.propulsiveContributions = debugLoadSet.propulsiveContributions;
            sample.readinessLevel = (int)debugReadiness.level;

            telemetry.Capture(sample);
        }

        private void ClearLoadDebug()
        {
            debugCoefficients = MavAeroCoefficients.Zero;
            debugLoads = MavAerodynamicLoads.Zero;

            // No load set was summed, so there is no measured specific force. Publishing zero with
            // the valid flag cleared is important: a control law must be able to tell "no reading"
            // apart from "a genuine 0 g reading", or a load-factor protection would silently
            // believe the aircraft is unloaded.
            debugState.specificForceAeroBodyG = Vector3.zero;
            debugState.specificForceValid = false;

            ClearUnityLoadDebug();
        }

        private void ClearUnityLoadDebug()
        {
            debugUnityLocalForceN = Vector3.zero;
            debugUnityLocalTorqueNm = Vector3.zero;
        }
    }
}
