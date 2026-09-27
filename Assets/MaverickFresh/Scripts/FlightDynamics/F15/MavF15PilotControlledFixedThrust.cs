using UnityEngine;

namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// Propulsion for the PILOT-CONTROLLED F-15 research aircraft: the research model's own fixed
    /// 8,300 lbf TOTAL thrust and its 0.25-in nose-up thrust-line moment, taken unchanged from
    /// <see cref="MavF15AfitResearchThrustSource"/> (<see cref="MavF15AfitResearchThrustSource.TotalThrustN"/>,
    /// <see cref="MavF15AfitResearchThrustSource.ThrustLinePitchingMomentNm"/>), produced wherever the
    /// research condition gate admits the state in SourceReproduction mode - exactly where the frozen
    /// research thrust would produce it.
    ///
    /// THROTTLE INACTIVE - FIXED RESEARCH THRUST. The pilot's throttle is carried into telemetry and
    /// ignored. There is no F100 deck, no NASA 836 thrust and no invented engine performance behind it.
    ///
    /// Granted only to <see cref="MavF15PilotControlledIdentity.ConfigurationId"/> through
    /// <see cref="MavF15PilotControlledAuthority"/>; on any other body it produces nothing and says why.
    /// The frozen <see cref="MavF15AfitResearchFixedThrust"/> is unchanged and still refuses every id
    /// but the research one.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MavF15PilotControlledFlightDynamicsProfile))]
    public sealed class MavF15PilotControlledFixedThrust : MavPropulsionModelBase
    {
        public const string ThrottleStatus = "THROTTLE INACTIVE - FIXED RESEARCH THRUST";

        public const string ModelName =
            "F-15 pilot-controlled research aircraft: AFIT research fixed TOTAL thrust 8,300 lbf - "
            + ThrottleStatus + "; NOT F100, NOT NASA 836";

        [Header("Debug")]
        public bool debugRefused = true;
        public string debugStatus = "not evaluated";

        [Tooltip("The throttle the pilot supplied on the last step. Shown, never used: " + ThrottleStatus + ".")]
        public float debugThrottleIgnored01;

        public override string PropulsionModelName
        {
            get { return ModelName; }
        }

        public override bool HasAuthoritativeData
        {
            get { return false; }
        }

        public override bool IsAcceptableForLiveFlight
        {
            get { return false; }
        }

        public override string ThrustDataStatus
        {
            get
            {
                return "research-model constant (Davison/Baumann 8,300 lbf total), " + ThrottleStatus
                    + "; not authoritative for any aircraft";
            }
        }

        public override MavPropulsiveLoads Evaluate(
            MavFlightState state,
            MavAtmosphereSample atmosphere,
            float throttle01,
            float deltaTime)
        {
            debugThrottleIgnored01 = throttle01;

            MavPropulsiveLoads loads;
            string reason;
            bool produced = TryEvaluate(GetComponent<MavSixDoFBody>(), state, atmosphere, out loads, out reason);

            debugRefused = !produced;
            debugStatus = (produced ? "" : "REFUSED: ") + reason + " | " + ThrottleStatus;
            return loads;
        }

        public override void ResetEngineState(float throttle01)
        {
            // No engine state exists: the research thrust is a constant.
        }

        /// <summary>The pilot-controlled thrust on <paramref name="body"/>, or a refusal.</summary>
        public static bool TryEvaluate(
            MavSixDoFBody body,
            MavFlightState state,
            MavAtmosphereSample atmosphere,
            out MavPropulsiveLoads loads,
            out string reason)
        {
            loads = MavPropulsiveLoads.Zero;

            if (!MavF15PilotControlledAuthority.TryGrant(body, out reason))
            {
                reason = "pilot-controlled thrust refused: " + reason;
                return false;
            }

            string conditionReason;
            bool insideFitCondition;
            if (!MavF15ResearchConditionGate.Admits(
                    MavF15ResearchConditionMode.SourceReproduction, state, atmosphere,
                    out insideFitCondition, out conditionReason))
            {
                reason = "pilot-controlled thrust refused away from the research source domain: " + conditionReason;
                return false;
            }

            loads.forceAeroBodyN = new Vector3(MavF15AfitResearchThrustSource.TotalThrustN, 0f, 0f);
            loads.momentAeroBodyNm = new Vector3(0f, MavF15AfitResearchThrustSource.ThrustLinePitchingMomentNm, 0f);
            loads.reportedThrustN = MavF15AfitResearchThrustSource.TotalThrustN;
            loads.powerState01 = 0f;
            loads.powerStateSpread01 = 0f;
            loads.contributingEngineCount = 0;
            loads.hasAuthoritativeData = false;

            reason = "research total thrust " + MavF15AfitResearchThrustSource.SourceTotalThrustLbf.ToString("F0")
                + " lbf at " + conditionReason;
            return true;
        }
    }
}
