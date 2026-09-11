using UnityEngine;

namespace MaverickFresh.FlightDynamics
{
    /// <summary>
    /// SI-unit six-DoF flight-dynamics boundary and the single final Rigidbody load-application
    /// owner for the new FDM path. simulationEnabled remains OFF by default.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public class MavSixDoFBody : MonoBehaviour, IMavArmableFlightBody
    {
        [Header("Safety")]
        [Tooltip("Master gate for new-FDM load application. OFF by default; legacy flight keeps ownership until this is deliberately armed.")]
        public bool simulationEnabled = false;
        public bool applyMassPropertiesOnEnable = false;
        public bool zeroUnityDampingWhenEnabled = true;

        [Tooltip("When true (default), loads are applied only at OPERATIONALLY_LIVE_READY.")]
        public bool requireOperationalReadinessForLoadApplication = true;

        [Tooltip("Explicit operator override for isolated structural bench testing.")]
        public bool allowStructuralOnlyLoadApplication = false;

        [Tooltip("Deliberate acknowledgement that non-authoritative propulsion may be used. OFF by default.")]
        public bool acceptNonAuthoritativePropulsion = false;

        public static readonly string[] DefaultConflictingLegacyPhysicsComponents =
        {
            "MavAeroBody",
            "MavAtmosphericEngine",
            "MavMouseFlightJet",
            "MavInstructorController",
            "MavThrustVectorControl"
        };

        [Tooltip("Legacy component type names that own aircraft physics.")]
        public string[] conflictingLegacyPhysicsComponents =
            (string[])DefaultConflictingLegacyPhysicsComponents.Clone();

        [Header("Physical Profile")]
        public MavFlightDynamicsProfileProvider profileProvider;
        public bool autoApplyProfileConfiguration = true;
        public MavFlightDynamicsProfile activeProfile;

        [Header("Physics Models")]
        public MavAerodynamicModelBase aerodynamicModel;
        public MavPropulsionModelBase propulsionModel;
        public MavMassProperties massProperties = new MavMassProperties();

        [Header("Control Path")]
        public MavFlightControlLawBase controlLaw;
        public MavControlSurfaceActuatorBase controlSurfaceActuator;
        public MavPilotCommandSourceBase pilotCommandSource;

        [Header("Control Input")]
        public MavControlInput controlInput;

        [Header("Telemetry")]
        public MavFlightDynamicsTelemetry telemetry;

        [Header("Debug / State")]
        public bool debugProfileValid;
        public bool debugInsideProfileEnvelope;
        public string debugProfileStatus = "unconfigured";
        public MavAtmosphereSample debugAtmosphere;
        public MavFlightState debugState;
        public MavAeroCoefficients debugCoefficients;

        [Header("Debug / Loads")]
        public MavFlightDynamicsLoadSet debugLoadSet;
        public MavAerodynamicLoads debugLoads;
        public Vector3 debugUnityLocalForceN;
        public Vector3 debugUnityLocalTorqueNm;
        public int debugPhysicsStepIndex;
        public int debugLoadApplications;
        public int debugRejectedDuplicateApplications;
        public int debugRejectedNonFiniteApplications;

        [Header("Debug / Readiness")]
        public bool debugReadyForLiveFdm;
        public string debugReadinessReason = "not evaluated";
        public MavFlightDynamicsReadinessReport debugReadiness;
        public MavFlightDynamicsReadinessInputs debugReadinessInputs;
        public string debugLegacyPhysicsOwner = "none";
        public int debugRejectedNotLiveReadyApplications;

        /// <summary>
        /// Name for the ownership authority's diagnostics.
        /// </summary>
        public string ArmableBodyName { get { return "MavSixDoFBody"; } }

        /// <summary>
        /// Arming, expressed as the ownership authority's contract.
        ///
        /// simulationEnabled remains the underlying flag - existing scenes, the Phase 2/3 suites and the
        /// readiness gate all still read it - but the SETTER is how the single arming authority moves it.
        /// Nothing else in the runtime sets it true.
        /// </summary>
        public bool ArmedForLiveFlight
        {
            get { return simulationEnabled; }
            set { simulationEnabled = value; }
        }

        [Header("Phase 5A Ownership / Shadow")]
        [Tooltip("The Phase 5 ownership gate. When present it decides whether this body may apply loads. Absent gate falls back to simulationEnabled alone, which is the pre-Phase-5 behaviour.")]
        public MavFlightPhysicsOwnership physicsOwnership;

        [Tooltip("Physics steps this body has computed in shadow mode without applying anything.")]
        public int debugShadowComputeSteps;

        [Tooltip("Load applications refused because the ownership gate did not grant the replacement stack physics ownership.")]
        public int debugRejectedNotOwnerApplications;

        [Tooltip("Whether the most recent shadow computation produced a finite load set. Readiness reads this rather than trusting that the pipeline is merely wired.")]
        public bool debugShadowLoadSetFinite;

        private Rigidbody rb;
        private bool ownershipInitialized;
        private float lastAppliedFixedTime = float.NegativeInfinity;
        private bool loggedDuplicateRejection;
        private bool loggedNonFiniteRejection;
        private bool loggedNotLiveReadyRejection;
        private bool loggedStructuralOnlyOverride;
        private int lastReadinessMask = -1;
        private bool readinessEvaluatedOnce;

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

            if (simulationEnabled)
            {
                EvaluateReadinessReport();
                if (IsLoadApplicationPermitted())
                    InitializePhysicsOwnership();
            }
        }

        private void FixedUpdate()
        {
            StepPhysicsCore(Time.fixedDeltaTime, Time.fixedTime);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only deterministic seam for component integration validation. Player/runtime code
        /// cannot supply its own physics-step token; production load application is owned solely by
        /// FixedUpdate and therefore uses Unity's authoritative Time.fixedTime.
        /// </summary>
        public bool StepPhysicsForValidation(float fixedDeltaTime, float fixedTime)
        {
            return StepPhysicsCore(fixedDeltaTime, fixedTime);
        }
#endif

        private bool StepPhysicsCore(float fixedDeltaTime, float fixedTime)
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
                return false;
            }

            if (!IsLoadApplicationPermitted())
            {
                ClearLoadDebug();
                PublishTelemetry(false);
                return false;
            }

            if (!ownershipInitialized)
                InitializePhysicsOwnership();

            MavControlInput boundedInput = controlInput;
            if (activeProfile != null && debugProfileValid)
                boundedInput = activeProfile.controlSurfaceLimits.Clamp(controlInput);

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

            if (propulsionModel != null)
            {
                MavPropulsiveLoads propulsive = propulsionModel.Evaluate(
                    debugState,
                    debugAtmosphere,
                    boundedInput.throttle01,
                    fixedDeltaTime
                );

                debugLoadSet.AddPropulsive(propulsive);
            }

            PublishSpecificForce();

            // ---- SHADOW: compute everything, apply nothing -------------------------------------
            //
            // Everything above this line is pure computation - state extraction, atmosphere, aero
            // coefficients, dimensionalisation, propulsion, specific force - and touches no Rigidbody.
            // The single write is TryApplyLoadSet below, so shadow mode is implemented by returning
            // before it rather than by disabling parts of the pipeline. That matters: a shadow path
            // that skipped stages would be validating something other than what replacement mode will
            // eventually run.
            //
            // InitializePhysicsOwnership, which writes Rigidbody damping and gravity flags, is also
            // already behind the readiness gate above and is not reached in shadow mode.
            if (IsShadowComputeOnly())
            {
                debugShadowComputeSteps++;
                debugShadowLoadSetFinite = debugLoadSet.IsFinite();
                PublishTelemetry(false);
                return false;
            }

            bool applied = TryApplyLoadSet(fixedTime);
            PublishTelemetry(applied);
            return applied;
        }

        /// <summary>
        /// Whether this step must compute without applying anything.
        ///
        /// Answered from the ownership gate, not from a local flag, so there is one authority for the
        /// question. With no gate present the answer is false and the body behaves exactly as it did
        /// before Phase 5A.
        /// </summary>
        public bool IsShadowComputeOnly()
        {
            ResolvePhysicsOwnership();
            return physicsOwnership != null && physicsOwnership.ReplacementShadowComputeRequested;
        }

        /// <summary>
        /// Whether the ownership gate grants the replacement stack permission to write physics.
        /// An absent gate means the pre-Phase-5 behaviour: simulationEnabled and the readiness report
        /// are the only authorities.
        /// </summary>
        public bool ReplacementOwnershipGranted()
        {
            ResolvePhysicsOwnership();
            return physicsOwnership == null || physicsOwnership.ReplacementPhysicsAllowed;
        }

        private void ResolvePhysicsOwnership()
        {
            if (physicsOwnership == null)
                physicsOwnership = GetComponent<MavFlightPhysicsOwnership>();
        }

        public void SetControlInput(MavControlInput input)
        {
            controlInput = input;
        }

        private bool TryApplyLoadSet(float fixedTime)
        {
            // The gate has the final word on whether this stack owns physics. Checked here, at the one
            // place that actually writes, so no future caller can reach the Rigidbody around it.
            if (!ReplacementOwnershipGranted())
            {
                debugRejectedNotOwnerApplications++;
                ClearUnityLoadDebug();
                return false;
            }

            if (ShouldRejectDuplicateApplication(lastAppliedFixedTime, fixedTime))
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
                        "[Maverick/FDM] Refused a non-finite load set (NaN/Infinity). Rigidbody state was left untouched.",
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

            rb.AddRelativeForce(debugUnityLocalForceN, ForceMode.Force);
            rb.AddRelativeTorque(debugUnityLocalTorqueNm, ForceMode.Force);

            lastAppliedFixedTime = fixedTime;
            debugLoadApplications++;
            return true;
        }

        public static bool ShouldRejectDuplicateApplication(float lastAppliedFixedTime, float currentFixedTime)
        {
            if (float.IsNegativeInfinity(lastAppliedFixedTime))
                return false;

            return lastAppliedFixedTime == currentFixedTime;
        }

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
            Vector3 unityLocalAngularRate = transform.InverseTransformDirection(rb.angularVelocity);

            debugState = BuildFlightState(
                transform.position,
                worldVelocity,
                unityLocalVelocity,
                unityLocalAngularRate,
                transform.forward,
                transform.up,
                transform.right,
                debugAtmosphere
            );
        }

        public static MavFlightState BuildFlightState(
            Vector3 worldPositionM,
            Vector3 worldVelocityMps,
            Vector3 unityLocalVelocityMps,
            Vector3 unityLocalAngularRateRadSec,
            Vector3 forwardWorld,
            Vector3 upWorld,
            Vector3 rightWorld,
            MavAtmosphereSample atmosphere)
        {
            Vector3 aeroBodyVelocity =
                MavFlightDynamicsMath.UnityLocalVectorToAeroBody(unityLocalVelocityMps);
            Vector3 aeroBodyRates =
                MavFlightDynamicsMath.UnityLocalAngularRateToAeroBody(unityLocalAngularRateRadSec);

            float tas = worldVelocityMps.magnitude;
            float speedOfSound = Mathf.Max(1f, atmosphere.speedOfSoundMps);
            float q = 0.5f * atmosphere.densityKgM3 * tas * tas;

            MavFlightState state = new MavFlightState();
            state.worldPositionM = worldPositionM;
            state.worldVelocityMps = worldVelocityMps;
            state.aeroBodyVelocityMps = aeroBodyVelocity;
            state.aeroBodyRatesRadSec = aeroBodyRates;
            state.trueAirspeedMps = tas;
            state.mach = tas / speedOfSound;
            state.dynamicPressurePa = q;
            state.alphaRad = MavFlightDynamicsMath.ComputeAlphaRad(aeroBodyVelocity);
            state.betaRad = MavFlightDynamicsMath.ComputeBetaRad(aeroBodyVelocity);
            state.attitude = MavAttitudeMath.FromWorldBasis(
                forwardWorld, upWorld, rightWorld, worldVelocityMps);
            state.specificForceAeroBodyG = Vector3.zero;
            state.specificForceValid = false;
            return state;
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

        private void UpdateReadinessDebug()
        {
            debugReadinessInputs = BuildReadinessInputs();

            int mask = debugReadinessInputs.ToBitmask();
            if (mask == lastReadinessMask && readinessEvaluatedOnce)
                return;

            lastReadinessMask = mask;
            readinessEvaluatedOnce = true;
            debugReadiness = MavFlightDynamicsReadiness.Evaluate(debugReadinessInputs);
            debugReadyForLiveFdm = debugReadiness.structurallyPrepared;
            debugReadinessReason = debugReadiness.structurallyPrepared
                ? debugReadiness.operationalReason
                : debugReadiness.structuralReason;
        }

        public MavFlightDynamicsReadinessInputs BuildReadinessInputs()
        {
            return MavFlightDynamicsReadiness.BuildInputs(CapturePipelineSnapshot());
        }

        public MavPipelineSnapshot CapturePipelineSnapshot()
        {
            MavPipelineSnapshot snapshot = new MavPipelineSnapshot();

            snapshot.body = this;
            snapshot.hasRigidbody = rb != null;
            snapshot.hasValidProfile = activeProfile != null && debugProfileValid;
            snapshot.aerodynamicModel = aerodynamicModel != null ? aerodynamicModel : null;
            snapshot.propulsionModel = propulsionModel != null ? propulsionModel : null;

            snapshot.aerodynamicGeometryMatchesProfile =
                aerodynamicModel != null
                && activeProfile != null
                && debugProfileValid
                && GeometryMatches(aerodynamicModel.referenceGeometry, activeProfile.referenceGeometry);

            snapshot.controlLaw = controlLaw != null ? controlLaw : null;
            snapshot.controlLawEnabled = controlLaw != null && controlLaw.isActiveAndEnabled;
            snapshot.controlLawDrivesActuatorEachStep =
                controlLaw != null && controlLaw.driveActuatorInFixedUpdate;
            snapshot.controlLawActuator =
                controlLaw != null && controlLaw.actuator != null ? controlLaw.actuator : null;
            snapshot.controlLawBody =
                controlLaw != null && controlLaw.sixDoFBody != null ? controlLaw.sixDoFBody : null;
            snapshot.controlLawCommandSource =
                controlLaw != null && controlLaw.commandSource != null ? controlLaw.commandSource : null;
            snapshot.observedSourceSignalThisStep =
                controlLaw != null
                && controlLaw.debugCommandResolution
                    == MavFlightControlLawBase.MavCommandResolution.SourceSignal;
            snapshot.enabledControlLawCount = CountEnabledControlLaws();

            snapshot.actuator = controlSurfaceActuator != null ? controlSurfaceActuator : null;
            snapshot.actuatorEnabled =
                controlSurfaceActuator != null && controlSurfaceActuator.isActiveAndEnabled;
            snapshot.actuatorBoundBody =
                controlSurfaceActuator != null && controlSurfaceActuator.BoundBody != null
                    ? controlSurfaceActuator.BoundBody
                    : null;

            MavPilotCommandSourceBase bodySource = ResolveCommandSource();
            snapshot.bodyCommandSource = bodySource != null ? bodySource : null;
            snapshot.commandSourceEnabled = bodySource != null && bodySource.isActiveAndEnabled;
            snapshot.commandSourceDeclaresOperational =
                bodySource != null && bodySource.IsOperationalCommandSource;

            snapshot.propulsionAcceptableForLiveFlight =
                propulsionModel != null
                && (propulsionModel.IsAcceptableForLiveFlight || acceptNonAuthoritativePropulsion);

            snapshot.legacyOwnerActive = HasLegacyPhysicsOwnerConflict();
            debugLegacyPhysicsOwner = cachedLegacyOwnerName;

            return snapshot;
        }

        public int CountEnabledControlLaws()
        {
            behaviourScratch.Clear();
            GetComponents(behaviourScratch);

            int count = 0;
            for (int i = 0; i < behaviourScratch.Count; i++)
            {
                MavFlightControlLawBase law = behaviourScratch[i] as MavFlightControlLawBase;
                if (law != null && law.enabled)
                    count++;
            }

            return count;
        }

        private bool HasLegacyPhysicsOwnerConflict()
        {
            legacyOwnershipScanCountdown--;

            if (!ShouldRescanLegacyOwnership(simulationEnabled, legacyOwnershipScanCountdown))
                return cachedLegacyOwnershipConflict;

            legacyOwnershipScanCountdown = LegacyOwnershipScanIntervalSteps;
            RefreshLegacyPhysicsOwner();
            return cachedLegacyOwnershipConflict;
        }

        public static bool ShouldRescanLegacyOwnership(bool loadApplicationArmed, int scanCountdown)
        {
            if (loadApplicationArmed)
                return true;

            return scanCountdown <= 0;
        }

        public void NotifyOwnershipChanged()
        {
            RefreshLegacyPhysicsOwner();
            legacyOwnershipScanCountdown = 0;
            readinessEvaluatedOnce = false;
            lastReadinessMask = -1;
        }

        public void RefreshLegacyPhysicsOwner()
        {
            string offender;
            cachedLegacyOwnershipConflict = TryFindLegacyPhysicsOwner(out offender);
            cachedLegacyOwnerName = cachedLegacyOwnershipConflict ? offender : "none";
        }

        public MavFlightDynamicsReadinessReport EvaluateReadinessReport()
        {
            Resolve();
            if (autoApplyProfileConfiguration && activeProfile == null)
                ApplyConfiguredProfile(false);

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

        public bool IsOperationallyLiveReady(out string reason)
        {
            MavFlightDynamicsReadinessReport report = EvaluateReadinessReport();
            reason = report.operationallyLiveReady ? report.operationalReason : report.summary;
            return report.operationallyLiveReady;
        }

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

        private void PublishSpecificForce()
        {
            float massKg = rb != null
                ? rb.mass
                : (massProperties != null ? massProperties.massKg : 0f);

            debugState = PublishSpecificForce(debugState, debugLoadSet, massKg);
        }

        public static MavFlightState PublishSpecificForce(
            MavFlightState state,
            MavFlightDynamicsLoadSet loadSet,
            float massKg)
        {
            if (!loadSet.IsFinite() || massKg <= 0f)
            {
                state.specificForceAeroBodyG = Vector3.zero;
                state.specificForceValid = false;
                return state;
            }

            state.specificForceAeroBodyG = ComputeSpecificForceG(loadSet.totalForceAeroBodyN, massKg);
            state.specificForceValid = true;
            return state;
        }

        public static Vector3 ComputeSpecificForceG(Vector3 totalForceAeroBodyN, float massKg)
        {
            if (massKg <= 0f)
                return Vector3.zero;

            float scale = 1f / (massKg * MavControlLawProtections.StandardGravityMps2);
            return totalForceAeroBodyN * scale;
        }

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

        private MavPilotCommandSourceBase ResolveCommandSource()
        {
            if (pilotCommandSource == null)
                pilotCommandSource = GetComponent<MavPilotCommandSourceBase>();

            return pilotCommandSource;
        }

        public static bool IdentityMatches(object expected, object actual)
        {
            if (expected == null || actual == null)
                return false;

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
