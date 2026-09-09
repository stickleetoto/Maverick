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
        public bool debugReadyForLiveFdm;
        public string debugReadinessReason = "not evaluated";

        private Rigidbody rb;
        private bool ownershipInitialized;
        private float lastAppliedFixedTime = float.NegativeInfinity;
        private bool loggedDuplicateRejection;
        private bool loggedNonFiniteRejection;

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
                InitializePhysicsOwnership();
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
        /// Reports whether the new FDM path is wired completely enough to take physical ownership.
        /// This is the gate described by the architecture readiness state machine; it never enables
        /// anything by itself.
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
        /// Pure readiness rule, kept static so validation can exercise the exact production logic
        /// without constructing a GameObject.
        ///
        /// Note: readiness is about wiring completeness, not data quality. A propulsion model that
        /// honestly reports zero thrust satisfies readiness; whether its data is authoritative is a
        /// separate, separately reported fact.
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
            if (!hasRigidbody)
            {
                reason = "no Rigidbody";
                return false;
            }

            if (!hasValidProfile)
            {
                reason = "no valid physical flight-dynamics profile";
                return false;
            }

            if (!hasAerodynamicModel)
            {
                reason = "no aerodynamic model";
                return false;
            }

            if (!hasControlSurfaceActuator)
            {
                reason = "no control-surface actuator";
                return false;
            }

            if (!hasControlLaw)
            {
                reason = "no flight control law";
                return false;
            }

            if (!hasPropulsionModel)
            {
                reason = "no propulsion model";
                return false;
            }

            reason = "READY";
            return true;
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
            string reason;
            debugReadyForLiveFdm = EvaluateReadiness(
                rb != null,
                activeProfile != null && debugProfileValid,
                aerodynamicModel != null,
                controlSurfaceActuator != null,
                controlLaw != null,
                propulsionModel != null,
                out reason
            );
            debugReadinessReason = reason;
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

            telemetry.Capture(sample);
        }

        private void ClearLoadDebug()
        {
            debugCoefficients = MavAeroCoefficients.Zero;
            debugLoads = MavAerodynamicLoads.Zero;
            ClearUnityLoadDebug();
        }

        private void ClearUnityLoadDebug()
        {
            debugUnityLocalForceN = Vector3.zero;
            debugUnityLocalTorqueNm = Vector3.zero;
        }
    }
}
