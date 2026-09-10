using UnityEngine;

namespace MaverickFresh
{
    /// <summary>
    /// Experimental single-body aerodynamic core.
    /// Keep this on Mav_Player, not on the visible aircraft mesh.
    ///
    /// Phase 4A ownership rule: when this body owns X% of the aerodynamic turn model, the legacy
    /// velocity-turn assist may own at most (1-X)%. Real aero is a replacement for fake centripetal
    /// steering, not an additional layer on top of it.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public class MavAeroBody : MonoBehaviour
    {
        [Header("v0.20.3 Experimental Aero Core")]
        public bool useAeroBody = true;
        [Range(0f, 1f)] public float aeroBlend = 0.45f;
        [Range(0f, 1f)] public float liftBlend = 0.55f;
        [Range(0f, 1f)] public float dragBlend = 0.75f;
        public bool manageRigidbodyGravity = true;
        public bool useCustomGravity = true;
        [Range(0f, 1.25f)] public float gravityBlend = 0.45f;
        public float minAeroSpeed = 20f;

        [Header("Wing / Atmosphere")]
        public float wingArea = 56.5f;
        public float airDensitySeaLevel = 1.225f;
        public float airDensityScaleHeight = 8500f;
        public float densityFloor = 0.18f;

        [Header("v0.20.5 Lift Curve / Stall")]
        public float clSlopePerDeg = 0.070f;
        public float zeroLiftAoADeg = 0f;
        public float stallAoADeg = 18f;
        public float fullStallAoADeg = 32f;
        [Range(0f, 1f)] public float postStallLiftFactor = 0.35f;
        public float maxAbsCl = 1.65f;
        public float postStallDrag = 0.18f;
        public float sideSlipDragPerDeg = 0.00035f;

        [Header("Drag")]
        public float cd0 = 0.028f;
        public float aspectRatio = 3.0f;
        public float oswaldEfficiency = 0.75f;
        public float maxDragG = 2.8f;
        public float maxLiftG = 9.2f;

        [Header("v0.20.4 Velocity Assist Migration")]
        [Tooltip("Serialized compatibility field from the old partial-fade model. Phase 4A no longer uses this value for active aero: velocity assist ownership is exactly 1-aeroBlend. Kept so old scenes/profiles deserialize without data loss.")]
        [Range(0f, 1f)] public float velocityAssistFade = 0.45f;
        [Tooltip("Additional sideslip damping from the aero body. Keep low; MavMouseFlightJet still has yaw/slip dampers.")]
        public float aeroSideSlipDamping = 0.10f;

        [Header("v0.20.6 Dynamic Pressure Control Authority")]
        public bool provideControlAuthority = true;
        public float referenceControlSpeed = 260f;
        public float authorityExponent = 2.0f;
        [Range(0.05f, 1f)] public float minControlAuthority = 0.35f;
        [Range(0.5f, 1.5f)] public float maxControlAuthority = 1.05f;
        public float authorityResponse = 3.5f;

        [Header("Debug")]
        public bool debugDrawForces;
        public float debugSpeed;
        public float debugAoADeg;
        public float debugAoSDeg;
        public float debugCl;
        public float debugCd;
        public float debugLiftN;
        public float debugDragN;
        public float debugLiftG;
        public float debugDragG;
        public float debugStallFactor;
        public float debugAirDensity;
        public float debugDynamicPressure;
        public float debugControlAuthority = 1f;
        public float debugAeroBlendApplied;
        public Vector3 debugLiftForce;
        public Vector3 debugDragForce;
        public Vector3 debugGravityForce;

        [Header("Debug / Phase 4A Turn Ownership")]
        [Tooltip("Fraction of turn ownership assigned to MavAeroBody by aeroBlend.")]
        public float debugAeroTurnOwnership;
        [Tooltip("Remaining fraction available to the legacy direct velocity-turn assist.")]
        public float debugLegacyVelocityAssistScale = 1f;
        [Tooltip("Must remain 1.0: aero ownership + legacy velocity-assist ownership.")]
        public float debugTurnOwnershipSum = 1f;

        private Rigidbody rb;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            ApplyRigidbodyGravityPolicy();
        }

        private void OnEnable()
        {
            if (rb == null) rb = GetComponent<Rigidbody>();
            ApplyRigidbodyGravityPolicy();
        }

        private void FixedUpdate()
        {
            if (rb == null)
                rb = GetComponent<Rigidbody>();
            if (rb == null)
                return;

            ApplyRigidbodyGravityPolicy();
            ResetDebug();
            UpdateTurnOwnershipDebug();

            if (useCustomGravity && gravityBlend > 0f)
            {
                debugGravityForce = Physics.gravity * rb.mass * gravityBlend;
                rb.AddForce(debugGravityForce, ForceMode.Force);
            }

            Vector3 v = rb.linearVelocity;
            float speed = v.magnitude;
            debugSpeed = speed;
            UpdateControlAuthority(speed);

            if (!useAeroBody || aeroBlend <= 0f || speed < minAeroSpeed || v.sqrMagnitude < 0.001f)
                return;

            Vector3 localV = transform.InverseTransformDirection(v);
            float forwardSpeed = Mathf.Max(0.1f, localV.z);
            debugAoADeg = Mathf.Atan2(-localV.y, forwardSpeed) * Mathf.Rad2Deg;
            debugAoSDeg = Mathf.Atan2(localV.x, forwardSpeed) * Mathf.Rad2Deg;
            debugStallFactor = Mathf.InverseLerp(stallAoADeg, Mathf.Max(stallAoADeg + 0.1f, fullStallAoADeg), Mathf.Abs(debugAoADeg));

            float density = AirDensity(transform.position.y);
            debugAirDensity = density;
            float q = 0.5f * density * speed * speed;
            debugDynamicPressure = q;
            float qS = q * Mathf.Max(0.01f, wingArea);

            float cl = LiftCoefficient(debugAoADeg);
            float cd = DragCoefficient(cl, debugAoADeg, debugAoSDeg);
            debugCl = cl;
            debugCd = cd;

            Vector3 vDir = v / speed;
            Vector3 liftDir = Vector3.ProjectOnPlane(transform.up, vDir);
            if (liftDir.sqrMagnitude < 0.0001f)
                liftDir = transform.up;
            else
                liftDir.Normalize();

            Vector3 lift = liftDir * (cl * qS * liftBlend * aeroBlend);
            Vector3 drag = -vDir * (cd * qS * dragBlend * aeroBlend);

            float maxLiftN = Mathf.Max(0.1f, rb.mass * 9.80665f * Mathf.Max(0.1f, maxLiftG));
            float maxDragN = Mathf.Max(0.1f, rb.mass * 9.80665f * Mathf.Max(0.1f, maxDragG));
            lift = Vector3.ClampMagnitude(lift, maxLiftN);
            drag = Vector3.ClampMagnitude(drag, maxDragN);

            if (aeroSideSlipDamping > 0f)
            {
                Vector3 sideAccel = -transform.right * localV.x * aeroSideSlipDamping * aeroBlend;
                rb.AddForce(sideAccel, ForceMode.Acceleration);
            }

            rb.AddForce(lift + drag, ForceMode.Force);
            debugLiftForce = lift;
            debugDragForce = drag;
            debugLiftN = lift.magnitude * Mathf.Sign(cl);
            debugDragN = drag.magnitude;
            debugLiftG = debugLiftN / Mathf.Max(1f, rb.mass * 9.80665f);
            debugDragG = debugDragN / Mathf.Max(1f, rb.mass * 9.80665f);
            debugAeroBlendApplied = aeroBlend;

            if (debugDrawForces)
            {
                Debug.DrawRay(transform.position, lift.normalized * Mathf.Min(25f, lift.magnitude / 50000f), Color.green, Time.fixedDeltaTime);
                Debug.DrawRay(transform.position, drag.normalized * Mathf.Min(25f, drag.magnitude / 50000f), Color.red, Time.fixedDeltaTime);
            }
        }

        /// <summary>
        /// Remaining ownership available to MavMouseFlightJet's direct velocity-turn assist.
        ///
        /// Phase 4A deliberately ignores the old per-aircraft partial fade while aero is active.
        /// If aeroBlend is 0.54, aero owns 54% and the fake velocity steering owns 46% - never
        /// 54% real aero plus 68.7% fake steering as the old F-22 profile produced.
        /// </summary>
        public float GetVelocityAssistScale()
        {
            return ComputeLegacyVelocityAssistScale(useAeroBody, aeroBlend);
        }

        /// <summary>Pure ownership migration rule used by production and validation.</summary>
        public static float ComputeLegacyVelocityAssistScale(bool aeroEnabled, float requestedAeroBlend)
        {
            if (!aeroEnabled)
                return 1f;

            return 1f - Mathf.Clamp01(requestedAeroBlend);
        }

        private void UpdateTurnOwnershipDebug()
        {
            debugAeroTurnOwnership = useAeroBody ? Mathf.Clamp01(aeroBlend) : 0f;
            debugLegacyVelocityAssistScale = ComputeLegacyVelocityAssistScale(useAeroBody, aeroBlend);
            debugTurnOwnershipSum = debugAeroTurnOwnership + debugLegacyVelocityAssistScale;
        }

        private void ApplyRigidbodyGravityPolicy()
        {
            if (rb != null && manageRigidbodyGravity)
                rb.useGravity = false;
        }

        private void ResetDebug()
        {
            debugLiftForce = Vector3.zero;
            debugDragForce = Vector3.zero;
            debugGravityForce = Vector3.zero;
            debugLiftN = 0f;
            debugDragN = 0f;
            debugLiftG = 0f;
            debugDragG = 0f;
            debugAeroBlendApplied = useAeroBody ? aeroBlend : 0f;
        }

        private float AirDensity(float altitudeMeters)
        {
            float alt = Mathf.Max(0f, altitudeMeters);
            float density = airDensitySeaLevel * Mathf.Exp(-alt / Mathf.Max(100f, airDensityScaleHeight));
            return Mathf.Max(densityFloor, density);
        }

        private float LiftCoefficient(float aoaDeg)
        {
            float aoa = aoaDeg - zeroLiftAoADeg;
            float sign = Mathf.Sign(aoa == 0f ? 1f : aoa);
            float abs = Mathf.Abs(aoa);
            float stall = Mathf.Max(0.1f, stallAoADeg);
            float full = Mathf.Max(stall + 0.1f, fullStallAoADeg);
            float clAtStall = clSlopePerDeg * stall;

            float clAbs;
            if (abs <= stall)
            {
                clAbs = clSlopePerDeg * abs;
            }
            else
            {
                float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(stall, full, abs));
                clAbs = Mathf.Lerp(clAtStall, clAtStall * Mathf.Clamp01(postStallLiftFactor), t);
            }

            return Mathf.Clamp(sign * clAbs, -Mathf.Abs(maxAbsCl), Mathf.Abs(maxAbsCl));
        }

        private float DragCoefficient(float cl, float aoaDeg, float aosDeg)
        {
            float ar = Mathf.Max(0.1f, aspectRatio);
            float e = Mathf.Clamp(oswaldEfficiency, 0.1f, 1.0f);
            float induced = (cl * cl) / (Mathf.PI * ar * e);
            float stallExtra = postStallDrag * Mathf.Clamp01(debugStallFactor);
            float slipExtra = sideSlipDragPerDeg * Mathf.Abs(aosDeg);
            return Mathf.Max(0f, cd0 + induced + stallExtra + slipExtra);
        }

        private void UpdateControlAuthority(float speed)
        {
            if (!provideControlAuthority)
            {
                debugControlAuthority = Mathf.MoveTowards(debugControlAuthority, 1f, authorityResponse * Time.fixedDeltaTime);
                return;
            }

            float refSpeed = Mathf.Max(1f, referenceControlSpeed);
            float raw = Mathf.Pow(Mathf.Clamp(speed / refSpeed, 0f, 2.0f), Mathf.Max(0.1f, authorityExponent));
            float target = Mathf.Clamp(raw, minControlAuthority, maxControlAuthority);
            float step = Mathf.Max(0.01f, authorityResponse) * Time.fixedDeltaTime;
            debugControlAuthority = Mathf.MoveTowards(debugControlAuthority, target, step);
        }
    }
}
