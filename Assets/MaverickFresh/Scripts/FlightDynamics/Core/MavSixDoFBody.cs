using UnityEngine;

namespace MaverickFresh.FlightDynamics
{
    /// <summary>
    /// Isolated SI-unit six-DoF aerodynamic integration boundary.
    ///
    /// IMPORTANT: simulationEnabled defaults to false so this component can coexist
    /// with the legacy Maverick flight stack without double-applying forces.
    /// Do not enable it on Mav_Player until the legacy ownership migration begins.
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

        [Header("Model")]
        public MavAerodynamicModelBase aerodynamicModel;
        public MavMassProperties massProperties = new MavMassProperties();

        [Header("Control Input")]
        public MavControlInput controlInput;

        [Header("Debug / Telemetry")]
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
        }

        private void OnEnable()
        {
            Resolve();

            if (simulationEnabled)
                InitializePhysicsOwnership();
        }

        private void FixedUpdate()
        {
            Resolve();
            UpdateStateAndAtmosphere();

            if (!simulationEnabled || rb == null || aerodynamicModel == null)
            {
                ClearLoadDebug();
                return;
            }

            if (!ownershipInitialized)
                InitializePhysicsOwnership();

            debugCoefficients = aerodynamicModel.Evaluate(
                debugState,
                controlInput,
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
        }

        private void InitializePhysicsOwnership()
        {
            if (rb == null)
                return;

            if (applyMassPropertiesOnEnable && massProperties != null)
                massProperties.ApplyTo(rb);

            if (zeroUnityDampingWhenEnabled)
            {
                rb.linearDamping = 0f;
                rb.angularDamping = 0f;
            }

            rb.useGravity = true;
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

        private void ClearLoadDebug()
        {
            debugCoefficients = MavAeroCoefficients.Zero;
            debugLoads = new MavAerodynamicLoads();
            debugUnityLocalForceN = Vector3.zero;
            debugUnityLocalTorqueNm = Vector3.zero;
        }
    }
}
