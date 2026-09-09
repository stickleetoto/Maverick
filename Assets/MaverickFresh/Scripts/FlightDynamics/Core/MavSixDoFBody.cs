using UnityEngine;

namespace MaverickFresh.FlightDynamics
{
    /// <summary>
    /// SI-unit six-DoF flight-dynamics engine boundary.
    ///
    /// Aircraft-specific code supplies dimensionless aerodynamic coefficients and an optional
    /// MavFlightDynamicsProfileProvider supplies physical geometry / mass / envelope data.
    /// This class owns atmosphere sampling, state extraction, coefficient dimensionalization,
    /// and final Rigidbody force/moment application.
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
        public bool simulationEnabled = false;
        public bool applyMassPropertiesOnEnable = false;
        public bool zeroUnityDampingWhenEnabled = true;

        [Header("Physical Profile")]
        public MavFlightDynamicsProfileProvider profileProvider;
        public bool autoApplyProfileConfiguration = true;
        public MavFlightDynamicsProfile activeProfile;

        [Header("Aerodynamic Model")]
        public MavAerodynamicModelBase aerodynamicModel;
        public MavMassProperties massProperties = new MavMassProperties();

        [Header("Control Input")]
        public MavControlInput controlInput;

        [Header("Debug / Telemetry")]
        public bool debugProfileValid;
        public bool debugInsideProfileEnvelope;
        public string debugProfileStatus = "unconfigured";
        public MavAtmosphereSample debugAtmosphere;
        public MavFlightState debugState;
        public MavAeroCoefficients debugCoefficients;
        public MavAerodynamicLoads debugLoads;
        public Vector3 debugUnityLocalForceN;
        public Vector3 debugUnityLocalTorqueNm;

        private Rigidbody rb;
        private bool ownershipInitialized;

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
            UpdateStateAndAtmosphere();
            UpdateProfileDebug();

            if (!simulationEnabled || rb == null || aerodynamicModel == null)
            {
                ClearLoadDebug();
                return;
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

            debugUnityLocalForceN = MavFlightDynamicsMath.AeroBodyVectorToUnityLocal(
                debugLoads.forceAeroBodyN
            );
            debugUnityLocalTorqueNm = MavFlightDynamicsMath.AeroBodyMomentToUnityLocal(
                debugLoads.momentAeroBodyNm
            );

            rb.AddRelativeForce(debugUnityLocalForceN, ForceMode.Force);
            rb.AddRelativeTorque(debugUnityLocalTorqueNm, ForceMode.Force);
        }

        public void SetControlInput(MavControlInput input)
        {
            controlInput = input;
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

        private void ClearLoadDebug()
        {
            debugCoefficients = MavAeroCoefficients.Zero;
            debugLoads = new MavAerodynamicLoads();
            debugUnityLocalForceN = Vector3.zero;
            debugUnityLocalTorqueNm = Vector3.zero;
        }
    }
}
