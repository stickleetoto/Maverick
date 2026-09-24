using UnityEngine;

namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// The thrust the AFIT / Baumann / Davison research model uses, exactly as its source prints it,
    /// and nowhere else.
    ///
    /// THIS IS NOT an F100 thrust deck, NOT NASA 836 thrust, NOT installed F100 performance, and NOT
    /// a function of Mach, altitude, throttle or time. It is one constant force belonging to one
    /// research model at one flight condition. It lives outside the MavF100* layer on purpose, and
    /// it can only be evaluated under the research identity.
    ///
    /// SOURCE SEMANTIC - verified in Davison AFIT/GAE/ENY/92M-01 (DTIC ADA256613):
    ///   driver, PDF p.91 (printed p.81): "THRUST - TOTAL A/C THRUST, LBS", <c>THRUST=8300.</c>
    ///     -> TOTAL AIRCRAFT thrust, not per engine.
    ///   coefficient routine, PDF p.120 and p.150: <c>CX = CFZ*SIN(RAL) - CFX*COS(RAL) + THRUST/QBARS</c>
    ///     -> acts along body +X (CX is "+ forward").
    ///   same routine: <c>CMM = CMM + THRUST*(0.25/12.0)/(QBARS*CWING)</c>, "THE (0.25/12.0) IS THE
    ///     OFFSET OF THE THRUST VECTOR FROM THE CG" -> a nose-up pitching moment of
    ///     THRUST x 0.25 in. It is reproduced here, because the transcribed coefficient routine
    ///     deliberately omits both thrust terms (see MavF15BaumannMach06Longitudinal).
    ///   Baumann AFIT/GAE/ENY/89D-01 (DTIC ADA217366) PDF p.34: 8,300 lbs is "military power",
    ///     "the thrust setting for trim conditions (steady, level flight) at 0.6 Mach and 20,000
    ///     feet"; PDF p.124: "thrust is fixed constant at 8300 lbs (Mil Power)".
    ///
    /// THROTTLE. The source holds thrust constant; nothing in it varies thrust with a throttle. A
    /// cockpit throttle is therefore IGNORED by this model, and that is reported, not hidden.
    /// </summary>
    public static class MavF15AfitResearchThrustSource
    {
        /// <summary>Total aircraft thrust, as printed. Not per engine.</summary>
        public const float SourceTotalThrustLbf = 8300f;

        /// <summary>Offset of the thrust vector from the CG, as printed (0.25/12.0 ft).</summary>
        public const float SourceThrustLineOffsetIn = 0.25f;

        public const float PoundForceToNewton = MavF15MassReference.PoundForceToNewton;
        public const float InchToM = 0.0254f;

        public const float TotalThrustN = SourceTotalThrustLbf * PoundForceToNewton;
        public const float ThrustLineOffsetM = SourceThrustLineOffsetIn * InchToM;

        /// <summary>Nose-up (+ body Y) pitching moment the source adds to Cm.</summary>
        public const float ThrustLinePitchingMomentNm = TotalThrustN * ThrustLineOffsetM;

        public const string Citation =
            "Davison AFIT/GAE/ENY/92M-01 (DTIC ADA256613): THRUST=8300. 'TOTAL A/C THRUST, LBS' "
            + "(driver PDF p.91); CX += THRUST/QBARS and CMM += THRUST*(0.25/12.0)/(QBARS*CWING) "
            + "(PDF p.120, p.150). Baumann AFIT/GAE/ENY/89D-01 (DTIC ADA217366) PDF p.34, p.124: "
            + "military power, fixed constant, at M 0.6 / 20,000 ft.";

        public const string ModelName =
            "F-15 AFIT research fixed TOTAL thrust 8,300 lbf @ "
            + MavF15AfitResearchIdentity.SourceConditionLabel
            + " - research model only; NOT F100, NOT NASA 836";

        /// <summary>
        /// The research thrust loads, or a refusal.
        ///
        /// Refuses unless <paramref name="profileId"/> is the research configuration id, and unless
        /// the state is at the research source condition. Asked for under any other identity -
        /// including the exact NASA 836 id - it returns zero and says why.
        /// </summary>
        public static bool TryEvaluate(
            string profileId,
            MavFlightState state,
            MavAtmosphereSample atmosphere,
            out MavPropulsiveLoads loads,
            out string reason)
        {
            loads = MavPropulsiveLoads.Zero;

            if (profileId != MavF15AfitResearchIdentity.ConfigurationId)
            {
                reason = "research thrust refused: profile '" + (profileId ?? "(none)")
                    + "' is not the research configuration "
                    + MavF15AfitResearchIdentity.ConfigurationId;
                return false;
            }

            string conditionReason;
            if (!MavF15BaumannMach06Reference.IsAtSourceCondition(
                    state, atmosphere, out conditionReason))
            {
                reason = "research thrust refused away from its source condition: "
                    + conditionReason;
                return false;
            }

            loads.forceAeroBodyN = new Vector3(TotalThrustN, 0f, 0f);
            loads.momentAeroBodyNm = new Vector3(0f, ThrustLinePitchingMomentNm, 0f);
            loads.reportedThrustN = TotalThrustN;

            // The source models no engines - one total-aircraft force - so no per-engine power
            // state or engine count is reported.
            loads.powerState01 = 0f;
            loads.powerStateSpread01 = 0f;
            loads.contributingEngineCount = 0;

            // A research-model constant is not authoritative data for any aircraft.
            loads.hasAuthoritativeData = false;

            reason = "research total thrust " + SourceTotalThrustLbf.ToString("F0")
                + " lbf at " + conditionReason;
            return true;
        }
    }

    /// <summary>
    /// Propulsion component for the F-15 RESEARCH configuration only. Produces
    /// <see cref="MavF15AfitResearchThrustSource"/>'s constant force when, and only when, the body
    /// it drives is flying the research profile at the research source condition.
    ///
    /// Requires the research profile on the same GameObject, and additionally refuses if the
    /// six-DoF body there is configured with any other profile provider - so dropping this
    /// component onto an aircraft running the exact NASA 836 profile yields zero thrust.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MavF15AfitResearchFlightDynamicsProfile))]
    public sealed class MavF15AfitResearchFixedThrust : MavPropulsionModelBase
    {
        [Header("Debug")]
        public bool debugRefused = true;
        public string debugStatus = "not evaluated";

        [Tooltip("The throttle the pipeline supplied on the last step. IGNORED: the research source holds thrust constant.")]
        public float debugThrottleIgnored01;

        public override string PropulsionModelName
        {
            get { return MavF15AfitResearchThrustSource.ModelName; }
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
                return "research-model constant (Davison/Baumann 8,300 lbf total at "
                    + MavF15AfitResearchIdentity.SourceConditionLabel
                    + "); not authoritative for any aircraft";
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
            bool produced = MavF15AfitResearchThrustSource.TryEvaluate(
                ResolveProfileId(), state, atmosphere, out loads, out reason);

            debugRefused = !produced;
            debugStatus = (produced ? "" : "REFUSED: ") + reason
                + " | throttle ignored (source thrust is constant)";
            return loads;
        }

        public override void ResetEngineState(float throttle01)
        {
            // No engine state exists: the source thrust is a constant.
        }

        /// <summary>
        /// The profile id this component is actually flying under. The six-DoF body's provider
        /// wins; with no body present, the sibling research profile answers.
        /// </summary>
        private string ResolveProfileId()
        {
            MavSixDoFBody body = GetComponent<MavSixDoFBody>();
            if (body != null && body.profileProvider != null)
            {
                return body.profileProvider is MavF15AfitResearchFlightDynamicsProfile
                    ? MavF15AfitResearchIdentity.ConfigurationId
                    : body.profileProvider.GetType().Name;
            }

            return GetComponent<MavF15AfitResearchFlightDynamicsProfile>() != null
                ? MavF15AfitResearchIdentity.ConfigurationId
                : null;
        }
    }
}
