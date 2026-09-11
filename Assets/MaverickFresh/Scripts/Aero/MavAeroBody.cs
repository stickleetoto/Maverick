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
    ///
    /// PHASE 4B correction. Phase 4A measured aero ownership as aeroBlend alone, but the lift force
    /// this body actually applies is scaled by aeroBlend * liftBlend. On the F-16 that was
    /// 0.47 * 0.60 = 0.282, while the legacy velocity-turn assist was given 1 - 0.47 = 0.53. The
    /// conservation debug reported a tidy 1.00 and the aircraft was in fact turning on 28% real lift
    /// and 53% direct velocity steering - which is what "rail-like" felt like. Curvature authority
    /// is now measured as the product, so raising liftBlend genuinely buys ownership and genuinely
    /// pays for it out of the assist.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public class MavAeroBody : MonoBehaviour,
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

        public string LegacyWriterName { get { return "MavAeroBody"; } }

        public MaverickFresh.FlightDynamics.MavLegacyWriterKind LegacyWriterKind
        {
            get { return MaverickFresh.FlightDynamics.MavLegacyWriterKind.Aerodynamic; }
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

        [Header("Phase 4B Aerodynamic Static Stability")]
        [Tooltip("Aerodynamic weathercock stability: a restoring moment proportional to angle of attack and dynamic pressure. This is the REPLACEMENT for the legacy forward-alignment assist, not an addition to it - see the class summary.")]
        public bool useAeroStaticStability = false;

        [Tooltip("MAVERICK TUNING, not a sourced Cm_alpha. Pitch restoring angular acceleration per degree of AoA at reference dynamic pressure, rad/s^2/deg.")]
        public float pitchStabilityStrength = 0.055f;

        [Tooltip("MAVERICK TUNING. Yaw restoring angular acceleration per degree of sideslip at reference dynamic pressure, rad/s^2/deg.")]
        public float yawStabilityStrength = 0.030f;

        [Tooltip("Dynamic pressure at which the stability strengths above apply in full. Below it stability fades with q, as real aerodynamic stability does.")]
        public float stabilityReferenceDynamicPressure = 30000f;

        [Tooltip("Ceiling on the restoring angular acceleration, rad/s^2. Stops a high-q excursion producing a snap.")]
        public float maxStabilityAngularAccel = 3.5f;

        [Header("Phase 4B Turn Authority Migration")]
        [Tooltip("Measure aero turn ownership as aeroBlend * liftBlend (the authority actually applied) instead of aeroBlend alone. OFF reproduces Phase 4A exactly, including its accounting error, for aircraft that have not been re-tuned.")]
        public bool usePhase4BTurnAuthority = false;

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

        [Header("Debug / Turn Ownership")]
        [Tooltip("Fraction of turn ownership assigned to MavAeroBody. Phase 4B: aeroBlend * liftBlend, the authority actually applied.")]
        public float debugAeroTurnOwnership;
        [Tooltip("Remaining fraction available to the legacy direct velocity-turn assist.")]
        public float debugLegacyVelocityAssistScale = 1f;
        [Tooltip("Must remain 1.0: aero ownership + legacy velocity-assist ownership.")]
        public float debugTurnOwnershipSum = 1f;

        [Header("Debug / Phase 4B Measured Curvature")]
        [Tooltip("Acceleration perpendicular to the velocity vector produced by aerodynamic lift, m/s^2. This is the curvature aero is actually paying for - measured, not nominal.")]
        public float debugAeroCurvatureAccel;
        [Tooltip("Aero curvature as a load factor, g.")]
        public float debugAeroCurvatureG;
        [Tooltip("Restoring angular acceleration from aerodynamic static stability, rad/s^2. Replaces the legacy alignment assist's stabilising role.")]
        public Vector3 debugStaticStabilityAccel;

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

            BeginLegacyWriterStep();
            ApplyRigidbodyGravityPolicy();
            ResetDebug();
            UpdateTurnOwnershipDebug();

            if (useCustomGravity && gravityBlend > 0f && LegacyPhysicsAllowed())
            {
                debugGravityForce = Physics.gravity * rb.mass * gravityBlend;
                rb.AddForce(debugGravityForce, ForceMode.Force);
                MarkLiveForceWritten();
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

            ApplyStaticStability(q);

            if (aeroSideSlipDamping > 0f && LegacyPhysicsAllowed())
            {
                Vector3 sideAccel = -transform.right * localV.x * aeroSideSlipDamping * aeroBlend;
                rb.AddForce(sideAccel, ForceMode.Acceleration);
                MarkLiveForceWritten();
            }

            if (!LegacyPhysicsAllowed())
                return;

            rb.AddForce(lift + drag, ForceMode.Force);
            MarkLiveForceWritten();

            // Lift is built perpendicular to the velocity vector by construction (liftDir is
            // transform.up projected onto the plane normal to vDir), so all of it curves the
            // trajectory. Recording it here is what lets the Phase 4B diagnostic separate curvature
            // that aerodynamics produced from curvature an assist produced directly.
            debugAeroCurvatureAccel = lift.magnitude / Mathf.Max(1f, rb.mass);
            debugAeroCurvatureG = debugAeroCurvatureAccel / 9.80665f;

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
        /// Aerodynamic static stability: a restoring moment that pulls the nose back toward the
        /// velocity vector, growing with angle of attack and with dynamic pressure.
        ///
        /// THIS IS A REPLACEMENT, NOT AN ADDITION. MavMouseFlightJet's forwardAlignmentAssist was
        /// doing this job, and doing it as a fixed torque that did not care about airspeed. Phase 4B
        /// migrates that assist down, and the aircraft would have nothing holding the nose near the
        /// velocity vector if this did not take over: angle of attack would integrate without bound
        /// under a held pitch command, because the legacy aero core applies forces only and models no
        /// pitching moment at all.
        ///
        /// Scaling with q is the substantive difference. The assist was equally strong at 100 m/s and
        /// 400 m/s; real stability is weak when there is little air to work with, which is what makes
        /// low-speed handling feel different from high-speed handling rather than merely slower.
        ///
        /// The strengths are MAVERICK TUNING. They are not a sourced Cm_alpha or Cn_beta, and they are
        /// deliberately expressed as angular acceleration rather than as a moment coefficient so they
        /// do not pretend to a rigour they do not have.
        /// </summary>
        private void ApplyStaticStability(float dynamicPressure)
        {
            if (!useAeroStaticStability || rb == null)
                return;

            float qRatio = dynamicPressure / Mathf.Max(1f, stabilityReferenceDynamicPressure);

            float pitchAccel = ComputeStabilityAccel(
                debugAoADeg, pitchStabilityStrength, qRatio, aeroBlend, maxStabilityAngularAccel);
            float yawAccel = ComputeStabilityAccel(
                debugAoSDeg, yawStabilityStrength, qRatio, aeroBlend, maxStabilityAngularAccel);

            // Positive AoA means the nose sits above the velocity vector, and a positive torque about
            // the aircraft's local X axis pitches the nose down - so the restoring sign is positive.
            // Positive sideslip means the nose is left of the velocity vector, restored by a positive
            // yaw torque about local Y.
            if (!LegacyPhysicsAllowed())
                return;

            debugStaticStabilityAccel = new Vector3(pitchAccel, yawAccel, 0f);
            rb.AddRelativeTorque(debugStaticStabilityAccel, ForceMode.Acceleration);
            MarkLiveForceWritten();
        }

        /// <summary>
        /// Restoring angular acceleration for one axis. Pure, so validation exercises the production
        /// rule rather than a copy of it.
        /// </summary>
        public static float ComputeStabilityAccel(
            float angleDeg,
            float strengthPerDeg,
            float dynamicPressureRatio,
            float authority,
            float maxAccel)
        {
            float accel = angleDeg * strengthPerDeg
                          * Mathf.Max(0f, dynamicPressureRatio)
                          * Mathf.Clamp01(authority);

            return Mathf.Clamp(accel, -Mathf.Abs(maxAccel), Mathf.Abs(maxAccel));
        }

        /// <summary>
        /// The aero turn ownership this body is actually exercising.
        ///
        /// Phase 4B: aeroBlend * liftBlend, because that is the factor the lift force is multiplied
        /// by. Phase 4A: aeroBlend alone, preserved for aircraft that have not been re-tuned, so
        /// enabling Phase 4B is a per-aircraft decision rather than a silent global retune.
        /// </summary>
        public float GetAeroTurnAuthority()
        {
            return ComputeAeroTurnAuthority(useAeroBody, aeroBlend, liftBlend, usePhase4BTurnAuthority);
        }

        /// <summary>
        /// Remaining ownership available to MavMouseFlightJet's direct velocity-turn assist.
        ///
        /// Aero ownership plus assist ownership is always exactly 1: real aero is a replacement for
        /// fake centripetal steering, never an additional layer on top of it.
        /// </summary>
        public float GetVelocityAssistScale()
        {
            return 1f - GetAeroTurnAuthority();
        }

        /// <summary>Pure ownership rule: how much turn authority aerodynamics is exercising.</summary>
        public static float ComputeAeroTurnAuthority(
            bool aeroEnabled,
            float requestedAeroBlend,
            float requestedLiftBlend,
            bool phase4B)
        {
            if (!aeroEnabled)
                return 0f;

            float blend = Mathf.Clamp01(requestedAeroBlend);
            if (!phase4B)
                return blend;

            return blend * Mathf.Clamp01(requestedLiftBlend);
        }

        /// <summary>
        /// Pure ownership migration rule used by production and validation.
        ///
        /// Phase 4A signature, kept so the Phase 4A validation keeps testing exactly what it tested.
        /// It answers for the aeroBlend-only accounting; Phase 4B callers use the four-argument form.
        /// </summary>
        public static float ComputeLegacyVelocityAssistScale(bool aeroEnabled, float requestedAeroBlend)
        {
            if (!aeroEnabled)
                return 1f;

            return 1f - Mathf.Clamp01(requestedAeroBlend);
        }

        /// <summary>Phase 4B form: the assist gets whatever aerodynamics is not carrying.</summary>
        public static float ComputeLegacyVelocityAssistScale(
            bool aeroEnabled,
            float requestedAeroBlend,
            float requestedLiftBlend,
            bool phase4B)
        {
            if (!aeroEnabled)
                return 1f;

            return 1f - ComputeAeroTurnAuthority(
                aeroEnabled, requestedAeroBlend, requestedLiftBlend, phase4B);
        }

        private void UpdateTurnOwnershipDebug()
        {
            debugAeroTurnOwnership = GetAeroTurnAuthority();
            debugLegacyVelocityAssistScale = GetVelocityAssistScale();
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
            debugAeroCurvatureAccel = 0f;
            debugAeroCurvatureG = 0f;
            debugStaticStabilityAccel = Vector3.zero;
            debugAeroBlendApplied = useAeroBody ? aeroBlend : 0f;
        }

        private float AirDensity(float altitudeMeters)
        {
            return ComputeAirDensity(
                altitudeMeters, airDensitySeaLevel, airDensityScaleHeight, densityFloor);
        }

        /// <summary>
        /// Exponential atmosphere, as a pure function.
        ///
        /// The coefficient maths below is exposed this way so validation can integrate a turn using
        /// the SAME curve production flies. A validation suite that re-implements the lift curve is
        /// only testing its own copy, and would happily pass while the real aircraft flew a different
        /// one.
        /// </summary>
        public static float ComputeAirDensity(
            float altitudeMeters,
            float seaLevelDensity,
            float scaleHeight,
            float floor)
        {
            float alt = Mathf.Max(0f, altitudeMeters);
            float density = seaLevelDensity * Mathf.Exp(-alt / Mathf.Max(100f, scaleHeight));
            return Mathf.Max(floor, density);
        }

        /// <summary>Lift coefficient including the post-stall roll-off, as a pure function.</summary>
        public static float ComputeLiftCoefficient(
            float aoaDeg,
            float zeroLiftAoADeg,
            float clSlopePerDeg,
            float stallAoADeg,
            float fullStallAoADeg,
            float postStallLiftFactor,
            float maxAbsCl)
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

        /// <summary>Drag coefficient including induced drag, as a pure function.</summary>
        public static float ComputeDragCoefficient(
            float cl,
            float cd0,
            float aspectRatio,
            float oswaldEfficiency,
            float postStallDrag,
            float stallFactor,
            float sideSlipDragPerDeg,
            float aosDeg)
        {
            float ar = Mathf.Max(0.1f, aspectRatio);
            float e = Mathf.Clamp(oswaldEfficiency, 0.1f, 1.0f);
            float induced = (cl * cl) / (Mathf.PI * ar * e);
            float stallExtra = postStallDrag * Mathf.Clamp01(stallFactor);
            float slipExtra = sideSlipDragPerDeg * Mathf.Abs(aosDeg);
            return Mathf.Max(0f, cd0 + induced + stallExtra + slipExtra);
        }

        private float LiftCoefficient(float aoaDeg)
        {
            return ComputeLiftCoefficient(
                aoaDeg, zeroLiftAoADeg, clSlopePerDeg, stallAoADeg, fullStallAoADeg,
                postStallLiftFactor, maxAbsCl);
        }

        private float DragCoefficient(float cl, float aoaDeg, float aosDeg)
        {
            return ComputeDragCoefficient(
                cl, cd0, aspectRatio, oswaldEfficiency, postStallDrag, debugStallFactor,
                sideSlipDragPerDeg, aosDeg);
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
