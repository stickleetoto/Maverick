using UnityEngine;

namespace MaverickFresh
{
    /// <summary>
    /// One sample of turn dynamics. Everything a Phase 4B question needs, and nothing that requires
    /// the diagnostic to touch physics.
    /// </summary>
    [System.Serializable]
    public struct MavTurnDynamicsSample
    {
        public float timeSeconds;
        public float trueAirspeed;
        public float bankAngleDeg;
        public float angleOfAttackDeg;
        public float loadFactorG;
        public float turnRateDegPerSec;
        public Vector3 angularVelocityDegPerSec;
        public float turnRadiusMeters;

        [Tooltip("Curvature acceleration from aerodynamic lift, m/s^2.")]
        public float aeroCurvatureAccel;
        [Tooltip("Curvature acceleration applied directly by the legacy velocity-turn assist, m/s^2.")]
        public float legacyCurvatureAccel;
        [Tooltip("Magnitude of the nose-onto-velocity alignment torque, rad/s^2.")]
        public float alignmentAssistTorque;

        [Tooltip("Share of this step's curvature produced by aerodynamics: aero / (aero + legacy). 1 = aerodynamics did all of it.")]
        public float aeroCurvatureShare;
        [Tooltip("Nominal ownership the configuration claims, for comparison against the measured share.")]
        public float configuredAeroOwnership;
        [Tooltip("Nominal legacy velocity-assist ownership.")]
        public float configuredLegacyOwnership;

        public float thrustBoostAllowance;
        public float rateNullingScale;
        public bool phase4BActive;
    }

    /// <summary>
    /// Phase 4B turn-dynamics diagnostic.
    ///
    /// READ-ONLY BY CONSTRUCTION. It applies no force, no torque and no correction, and it writes
    /// nothing to the jet or the aero body. That matters more than usual here: the whole subject of
    /// Phase 4B is which component owns trajectory curvature, and a diagnostic that nudged the
    /// aircraft would be measuring itself.
    ///
    /// The central number is aeroCurvatureShare. Phase 4A reported a nominal ownership sum of exactly
    /// 1.00 while the F-16 was flying on 28% aerodynamic lift and 53% direct velocity steering,
    /// because it multiplied aeroBlend by nothing and never measured what was applied. This samples
    /// the accelerations actually added to the Rigidbody, so configured-versus-measured disagreement
    /// is visible rather than hidden.
    /// </summary>
    [DisallowMultipleComponent]
    public class MavTurnDynamicsDiagnostics : MonoBehaviour
    {
        [Header("Sources")]
        public MavMouseFlightJet jet;
        public MavAeroBody aeroBody;
        public Rigidbody body;

        [Header("Sampling")]
        [Tooltip("Rolling history length. 250 samples is 5 s at the default fixed timestep.")]
        public int historyLength = 250;

        [Header("Latest")]
        public MavTurnDynamicsSample latest;

        [Header("Sustained-turn energy")]
        [Tooltip("Speed when the current sustained turn began, m/s.")]
        public float turnEntrySpeed;
        [Tooltip("Speed lost since the current sustained turn began, m/s. Negative means the aircraft gained energy.")]
        public float speedLostInTurn;
        [Tooltip("Load factor above which the aircraft counts as being in a sustained turn.")]
        public float sustainedTurnEntryG = 2.0f;

        [Header("Report")]
        [TextArea(10, 28)] public string report = "not sampled";

        private MavTurnDynamicsSample[] history;
        private int historyCount;
        private int historyHead;
        private bool inSustainedTurn;
        private float elapsed;

        private void Awake()
        {
            Resolve();
            EnsureHistory();
        }

        private void Resolve()
        {
            if (jet == null) jet = GetComponent<MavMouseFlightJet>();
            if (aeroBody == null) aeroBody = GetComponent<MavAeroBody>();
            if (body == null) body = GetComponent<Rigidbody>();
        }

        private void EnsureHistory()
        {
            int want = Mathf.Max(8, historyLength);
            if (history == null || history.Length != want)
            {
                history = new MavTurnDynamicsSample[want];
                historyCount = 0;
                historyHead = 0;
            }
        }

        private void FixedUpdate()
        {
            Resolve();
            EnsureHistory();

            if (body == null)
                return;

            elapsed += Time.fixedDeltaTime;
            latest = Sample(elapsed);
            Push(latest);
            UpdateSustainedTurnEnergy(latest);
        }

        /// <summary>
        /// Builds one sample. Public and parameterised so a test can sample a rig it is stepping
        /// itself, rather than waiting on FixedUpdate.
        /// </summary>
        public MavTurnDynamicsSample Sample(float timeSeconds)
        {
            MavTurnDynamicsSample s = new MavTurnDynamicsSample();
            s.timeSeconds = timeSeconds;

            if (body == null)
                return s;

            Vector3 v = body.linearVelocity;
            float speed = v.magnitude;
            s.trueAirspeed = speed;

            s.bankAngleDeg = MavTurnDynamicsRules.ComputeBankAngleDeg(transform.right, transform.up);
            s.angularVelocityDegPerSec =
                transform.InverseTransformDirection(body.angularVelocity) * Mathf.Rad2Deg;

            if (aeroBody != null)
            {
                s.angleOfAttackDeg = aeroBody.debugAoADeg;
                s.aeroCurvatureAccel = aeroBody.debugAeroCurvatureAccel;
                s.loadFactorG = aeroBody.debugAeroCurvatureG;
                s.configuredAeroOwnership = aeroBody.debugAeroTurnOwnership;
                s.configuredLegacyOwnership = aeroBody.debugLegacyVelocityAssistScale;
            }

            if (jet != null)
            {
                s.legacyCurvatureAccel = jet.debugLegacyCurvatureAccel;
                s.alignmentAssistTorque = jet.debugAlignmentAssistTorque;
                s.aeroCurvatureShare = jet.debugMeasuredAeroCurvatureShare;
                s.thrustBoostAllowance = jet.debugThrustBoostAllowance;
                s.rateNullingScale = jet.debugRateNullingScale;
                s.phase4BActive = jet.usePhase4BTurnDynamics;
            }

            // Turn rate from the actual rotation of the velocity vector, not from body rates. Body
            // pitch rate is not turn rate: an aircraft can pitch while its trajectory barely bends,
            // and that gap is exactly what a rail-like assist hides.
            float totalCurvature = s.aeroCurvatureAccel + s.legacyCurvatureAccel;
            s.turnRateDegPerSec = MavTurnDynamicsRules.ComputeTurnRateDegPerSec(totalCurvature, speed);
            s.turnRadiusMeters = MavTurnDynamicsRules.ComputeTurnRadiusMeters(totalCurvature, speed);

            return s;
        }

        private void UpdateSustainedTurnEnergy(MavTurnDynamicsSample s)
        {
            bool turning = Mathf.Abs(s.loadFactorG) >= sustainedTurnEntryG;

            if (turning && !inSustainedTurn)
            {
                inSustainedTurn = true;
                turnEntrySpeed = s.trueAirspeed;
            }
            else if (!turning)
            {
                inSustainedTurn = false;
            }

            speedLostInTurn = inSustainedTurn ? turnEntrySpeed - s.trueAirspeed : 0f;
        }

        private void Push(MavTurnDynamicsSample s)
        {
            history[historyHead] = s;
            historyHead = (historyHead + 1) % history.Length;
            if (historyCount < history.Length)
                historyCount++;
        }

        /// <summary>Samples in chronological order, oldest first.</summary>
        public MavTurnDynamicsSample[] GetHistory()
        {
            MavTurnDynamicsSample[] output = new MavTurnDynamicsSample[historyCount];
            int start = (historyHead - historyCount + history.Length) % history.Length;

            for (int i = 0; i < historyCount; i++)
                output[i] = history[(start + i) % history.Length];

            return output;
        }

        public void ResetHistory()
        {
            historyCount = 0;
            historyHead = 0;
            elapsed = 0f;
            inSustainedTurn = false;
            speedLostInTurn = 0f;
        }

        [ContextMenu("Build Turn Dynamics Report")]
        public void BuildReport()
        {
            report = DescribeSample(latest, speedLostInTurn);
            Debug.Log("[Maverick/Phase4B] " + report, this);
        }

        public static string DescribeSample(MavTurnDynamicsSample s, float speedLost)
        {
            System.Text.StringBuilder text = new System.Text.StringBuilder(768);
            text.AppendLine("Phase 4B turn dynamics");
            text.Append("  phase4B active      ").AppendLine(s.phase4BActive ? "yes" : "no (Phase 4A behaviour)");
            text.Append("  true airspeed       ").Append(s.trueAirspeed.ToString("F1")).AppendLine(" m/s");
            text.Append("  bank angle          ").Append(s.bankAngleDeg.ToString("F1")).AppendLine(" deg");
            text.Append("  angle of attack     ").Append(s.angleOfAttackDeg.ToString("F2")).AppendLine(" deg");
            text.Append("  load factor         ").Append(s.loadFactorG.ToString("F2")).AppendLine(" g");
            text.Append("  turn rate           ").Append(s.turnRateDegPerSec.ToString("F2")).AppendLine(" deg/s");
            text.Append("  turn radius         ").Append(s.turnRadiusMeters.ToString("F0")).AppendLine(" m");
            text.Append("  body rates p/q/r    ")
                .Append(s.angularVelocityDegPerSec.z.ToString("F1")).Append(" / ")
                .Append(s.angularVelocityDegPerSec.x.ToString("F1")).Append(" / ")
                .Append(s.angularVelocityDegPerSec.y.ToString("F1")).AppendLine(" deg/s");
            text.AppendLine("  --- curvature ownership ---");
            text.Append("  aero lift           ").Append(s.aeroCurvatureAccel.ToString("F2")).AppendLine(" m/s^2");
            text.Append("  legacy assist       ").Append(s.legacyCurvatureAccel.ToString("F2")).AppendLine(" m/s^2");
            text.Append("  alignment torque    ").Append(s.alignmentAssistTorque.ToString("F3")).AppendLine(" rad/s^2");
            text.Append("  MEASURED aero share ").Append((s.aeroCurvatureShare * 100f).ToString("F1")).AppendLine(" %");
            text.Append("  configured aero     ").Append((s.configuredAeroOwnership * 100f).ToString("F1")).AppendLine(" %");
            text.Append("  configured legacy   ").Append((s.configuredLegacyOwnership * 100f).ToString("F1")).AppendLine(" %");
            text.AppendLine("  --- energy ---");
            text.Append("  speed lost in turn  ").Append(speedLost.ToString("F1")).AppendLine(" m/s");
            text.Append("  thrust boost allow  ").Append((s.thrustBoostAllowance * 100f).ToString("F0")).AppendLine(" %");
            text.Append("  rate nulling scale  ").Append(s.rateNullingScale.ToString("F2")).AppendLine();
            return text.ToString();
        }
    }
}
