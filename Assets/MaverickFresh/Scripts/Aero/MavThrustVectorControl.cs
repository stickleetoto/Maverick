using UnityEngine;

namespace MaverickFresh
{
    /// <summary>
    /// v0.20.8 F-22 primary aircraft assist.
    /// Keep this on Mav_Player. It is profile-driven and disabled for non-TVC aircraft.
    /// This is not a full nozzle simulation; it adds a small pitch-dominant control moment
    /// at high AoA and low speed so the F-22 keeps its high-alpha identity without making
    /// every aircraft use the same arcade turn assist.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public class MavThrustVectorControl : MonoBehaviour,
        MaverickFresh.FlightDynamics.IMavLegacyPhysicsWriter
    {
        // ================= Phase 5A legacy physics ownership gate =========================
        //
        // This component can write force or torque to the live player Rigidbody, so it asks the
        // ownership gate before every such write and reports afterwards what it actually did.
        //
        // The gate is CONSULTED rather than the component being switched off from outside. Disabling
        // the component would also stop its command generation and telemetry, which must survive into
        // replacement mode; and a disabled component proves nothing about what it contributed, which is
        // exactly the claim replacement activation has to be able to make. In Legacy mode the gate
        // allows everything, so this costs one boolean read per step and changes no behaviour.

        [Header("Phase 5A Ownership")]
        [Tooltip("Gate deciding whether this component may write physics. Resolved from this GameObject; an absent gate means legacy is allowed, which is the pre-Phase-5 behaviour.")]
        public MaverickFresh.FlightDynamics.MavFlightPhysicsOwnership physicsOwnership;

        [Tooltip("Whether this component applied force or torque to the live Rigidbody on its most recent physics step. OBSERVED, not configured.")]
        public bool debugWroteLiveForceLastStep;
        public int debugWriterLastStepIndex;

        public string LegacyWriterName { get { return "MavThrustVectorControl"; } }

        public MaverickFresh.FlightDynamics.MavLegacyWriterKind LegacyWriterKind
        {
            get { return MaverickFresh.FlightDynamics.MavLegacyWriterKind.ControlTorque; }
        }

        public bool WroteLiveForceLastStep { get { return debugWroteLiveForceLastStep; } }
        public int LegacyWriterLastStepIndex { get { return debugWriterLastStepIndex; } }

        /// <summary>
        /// Whether this component may write physics this step. An absent gate means legacy ownership,
        /// which is the pre-Phase-5 behaviour and the safe default.
        /// </summary>
        private bool LegacyPhysicsAllowed()
        {
            ResolvePhysicsOwnership();
            return physicsOwnership == null || physicsOwnership.LegacyPhysicsAllowed;
        }

        /// <summary>Records that a live write happened, for the gate's writer enumeration.</summary>
        private void MarkLiveForceWritten()
        {
            debugWroteLiveForceLastStep = true;
            if (physicsOwnership != null)
                debugWriterLastStepIndex = physicsOwnership.CurrentStepIndex;
        }

        /// <summary>Called at the top of each physics step, before any write decision.</summary>
        private void BeginLegacyWriterStep()
        {
            ResolvePhysicsOwnership();
            debugWroteLiveForceLastStep = false;
        }

        private void ResolvePhysicsOwnership()
        {
            if (physicsOwnership != null)
                return;

            physicsOwnership = GetComponent<MaverickFresh.FlightDynamics.MavFlightPhysicsOwnership>();
            if (physicsOwnership != null)
                physicsOwnership.RegisterLegacyWriter(this);
        }

        [Header("v0.20.8 Thrust Vector Control") ]
        public bool useThrustVectorControl = false;
        public float pitchAuthority = 0f;
        public float rollAuthority = 0f;
        public float yawAuthority = 0f;
        public float activationAoADeg = 18f;
        public float fullAoADeg = 45f;
        public float lowSpeedFull = 145f;
        public float lowSpeedFadeOut = 360f;
        public float maxTorque = 8f;
        public float response = 8f;

        [Header("Safety") ]
        public bool requireEngineOn = true;
        public bool pitchOnlyWhenLowSpeedOrHighAoA = true;
        public float minimumThrottle01 = 0.08f;

        [Header("Debug") ]
        public bool debugActive;
        public float debugAoAFactor;
        public float debugSpeedFactor;
        public float debugThrottleFactor;
        public Vector3 debugTorque;

        private Rigidbody rb;
        private MavMouseFlightJet jet;
        private Vector3 smoothedTorque;

        private void Awake()
        {
            Resolve();
        }

        private void FixedUpdate()
        {
            Resolve();
            debugActive = false;
            debugTorque = Vector3.zero;

            if (!useThrustVectorControl || rb == null || jet == null)
                return;
            if (requireEngineOn && !jet.engineOn)
                return;

            float throttleFactor = Mathf.Clamp01(jet.effectiveThrottle01);
            if (throttleFactor < minimumThrottle01)
                return;

            float aoaAbs = Mathf.Abs(jet.aoaEstimateDeg);
            debugAoAFactor = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(activationAoADeg, Mathf.Max(activationAoADeg + 0.1f, fullAoADeg), aoaAbs));

            float speed = rb.linearVelocity.magnitude;
            debugSpeedFactor = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(lowSpeedFull, Mathf.Max(lowSpeedFull + 0.1f, lowSpeedFadeOut), speed));
            float blend = Mathf.Clamp01(Mathf.Max(debugAoAFactor, pitchOnlyWhenLowSpeedOrHighAoA ? debugSpeedFactor : 0f));
            debugThrottleFactor = throttleFactor;

            if (blend <= 0.001f)
                return;

            Vector3 commanded = new Vector3(
                jet.pitch * pitchAuthority,
                jet.yaw * yawAuthority,
                -jet.roll * rollAuthority
            ) * blend * Mathf.Lerp(0.35f, 1f, throttleFactor);

            commanded = Vector3.ClampMagnitude(commanded, Mathf.Max(0.01f, maxTorque));
            float k = 1f - Mathf.Exp(-Mathf.Max(0.01f, response) * Time.fixedDeltaTime);
            smoothedTorque = Vector3.Lerp(smoothedTorque, commanded, k);

            if (smoothedTorque.sqrMagnitude > 0.000001f && LegacyPhysicsAllowed())
            {
                rb.AddRelativeTorque(smoothedTorque, ForceMode.Acceleration);
                MarkLiveForceWritten();
                debugActive = true;
                debugTorque = smoothedTorque;
            }
        }

        private void Resolve()
        {
            if (rb == null) rb = GetComponent<Rigidbody>();
            if (jet == null) jet = GetComponent<MavMouseFlightJet>();
        }
    }
}
