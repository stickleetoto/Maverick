using System;
using UnityEngine;

namespace MaverickFresh.FlightDynamics.F15
{
    // The pilot-controlled F-15 research aircraft keeps three kinds of number strictly apart:
    //
    //   SOURCE PHYSICS - the frozen AFIT/Baumann/Davison research model: coefficients, geometry,
    //     mass, inertia, fixed thrust, source density and gravity. Not touched here.
    //   SOURCED RESEARCH FACTS the control layer uses: the surface sign conventions, the research
    //     model's own differential-tail relation (DDTD = 0.3 x commanded aileron), and the validated
    //     research equilibrium the aircraft starts from.
    //   MAVERICK_TUNED_NON_AUTHORITATIVE values: stick gearing and command envelopes. They are this
    //     project's approximation. They live ONLY in MavF15GameplayControlAuthority - never in
    //     MavF15ResearchDemonstratedControlRange, MavF15PhysicalSurfaceHardStops or
    //     MavF15ActuatorRateLimits - and every one of them is labelled.
    //
    // Full record: Docs/Reference/F15_PILOT_CONTROLLED_V1.md.

    /// <summary>Provenance labels of the pilot-control layer's values.</summary>
    public static class MavF15PilotControlProvenance
    {
        /// <summary>Project tuning: chosen by Maverick, not sourced, not aircraft data.</summary>
        public const string MaverickTunedNonAuthoritative = "MAVERICK_TUNED_NON_AUTHORITATIVE";

        /// <summary>A relation the research model itself states, used unchanged.</summary>
        public const string SourcedResearchModelRelation = "SOURCED_RESEARCH_MODEL_RELATION";

        /// <summary>An equilibrium of the research model, recovered and validated in WP-3B / WP-3D / closeout.</summary>
        public const string ValidatedResearchEquilibrium = "VALIDATED_RESEARCH_EQUILIBRIUM";

        /// <summary>The label every stabilization term carries. Never "F-15 CAS".</summary>
        public const string GameplayResearchAssist = "MAVERICK GAMEPLAY / RESEARCH ASSIST";
    }

    /// <summary>
    /// Which way each surface must move for a positive pilot demand, in the research model's own
    /// surface convention. These are NOT tuning: they follow from the research model's stated
    /// conventions and are checked against its coefficient derivatives by
    /// MavF15PilotControlValidation ([P4]).
    /// </summary>
    public static class MavF15PilotControlConventions
    {
        /// <summary>
        /// Pitch +1 (nose up) moves the symmetric stabilator NEGATIVE: Baumann states positive
        /// stabilator is leading edge up / nose-down (<see cref="MavF15ResearchControlConventions.SymmetricStabilatorPositive"/>),
        /// and WP-2 verified that sign against Table VII.
        /// </summary>
        public const float NoseUpStabilatorSign = -1f;

        /// <summary>
        /// Roll +1 (right) moves the aileron POSITIVE: the transcribed rolling-moment derivative
        /// CL_da is positive across the audited alpha range, so positive aileron rolls right (Cl &gt; 0).
        /// The printed physical convention (<see cref="MavF15ResearchControlConventions.AileronPositive"/>)
        /// is recorded beside the audit; the mapping follows the model that is flown.
        /// </summary>
        public const float RollRightAileronSign = 1f;

        /// <summary>
        /// Yaw +1 (nose right) moves the rudder NEGATIVE: positive rudder is trailing edge left
        /// (<see cref="MavF15ResearchControlConventions.RudderPositive"/>), a nose-left yawing moment,
        /// and the transcribed CN_dr is negative.
        /// </summary>
        public const float YawNoseRightRudderSign = -1f;

        /// <summary>
        /// Differential stabilator per unit COMMANDED aileron, from the research model's own actuator
        /// equations: Davison AFIT/GAE/ENY/92M-01 (DTIC ADA256613) driver, PDF p.97,
        /// <c>F(12) = 20.*(.3*CDAILD-DDTD)</c> - "differential tail = 0.3 x commanded aileron". The
        /// same relation the research aero adapter documents (DTALD = 0.3*DAILD).
        /// </summary>
        public const float DifferentialTailPerCommandedAileron = 0.3f;

        public const string DifferentialTailCitation =
            "Davison AFIT/GAE/ENY/92M-01 (DTIC ADA256613) driver PDF p.97: F(12) = 20.*(.3*CDAILD-DDTD)";
    }

    /// <summary>One channel's gameplay command envelope. A COMMAND bound, never a physical stop.</summary>
    [Serializable]
    public struct MavF15GameplayChannelEnvelope
    {
        public float minDeg;
        public float maxDeg;

        [TextArea(1, 3)]
        public string basis;

        public bool IsValid
        {
            get { return !float.IsNaN(minDeg) && !float.IsNaN(maxDeg) && maxDeg > minDeg; }
        }

        public float Clamp(float deg)
        {
            return Mathf.Clamp(deg, minDeg, maxDeg);
        }
    }

    /// <summary>
    /// The pilot-controlled aircraft's gameplay control authority: stick gearing and command
    /// envelopes. EVERY value here is <see cref="MavF15PilotControlProvenance.MaverickTunedNonAuthoritative"/>.
    ///
    /// Not the F-15's mechanical gearing, not its FCS gains, not its hard stops, not its actuator
    /// rates - none of those is sourced. These are chosen so the research model stays inside the
    /// input region its coefficient fits were exercised at or audited as numerically sane
    /// (Docs/Reference/F15_PILOT_CONTROLLED_V1.md section 4). They are converted into actuator
    /// travel only as <see cref="MavEngineDataProvenance.MaverickTuning"/>.
    /// </summary>
    [Serializable]
    public struct MavF15GameplayControlAuthority
    {
        public const string Provenance = MavF15PilotControlProvenance.MaverickTunedNonAuthoritative;

        [Header("Stick gearing (deg per unit command) - MAVERICK_TUNED_NON_AUTHORITATIVE")]
        [Tooltip("Symmetric-stabilator travel per unit pitch command, about the trim bias. Sign comes from MavF15PilotControlConventions.")]
        public float pitchStabilatorDegPerUnit;

        [Tooltip("Aileron per unit roll command. Differential stabilator follows the research model's 0.3 x commanded aileron.")]
        public float rollAileronDegPerUnit;

        [Tooltip("Rudder per unit yaw command.")]
        public float yawRudderDegPerUnit;

        [Header("Command envelopes (absolute deg) - MAVERICK_TUNED_NON_AUTHORITATIVE, NOT hard stops")]
        public MavF15GameplayChannelEnvelope symmetricStabilator;
        public MavF15GameplayChannelEnvelope differentialStabilator;
        public MavF15GameplayChannelEnvelope aileron;
        public MavF15GameplayChannelEnvelope rudder;

        public MavF15GameplayChannelEnvelope Get(MavF15SurfaceChannel channel)
        {
            switch (channel)
            {
                case MavF15SurfaceChannel.SymmetricStabilator: return symmetricStabilator;
                case MavF15SurfaceChannel.DifferentialStabilator: return differentialStabilator;
                case MavF15SurfaceChannel.Aileron: return aileron;
                default: return rudder;
            }
        }

        /// <summary>
        /// The V1 values. Each is a Maverick choice; the basis strings say what it was chosen to
        /// respect. See Docs/Reference/F15_PILOT_CONTROLLED_V1.md section 4 for the coefficient audit.
        /// </summary>
        public static MavF15GameplayControlAuthority V1()
        {
            const string tag = MavF15PilotControlProvenance.MaverickTunedNonAuthoritative + ": ";
            return new MavF15GameplayControlAuthority
            {
                pitchStabilatorDegPerUnit = 10f,
                rollAileronDegPerUnit = 20f,
                yawRudderDegPerUnit = 15f,
                symmetricStabilator = new MavF15GameplayChannelEnvelope
                {
                    minDeg = -25f,
                    maxDeg = -5f,
                    basis = tag + "gameplay command envelope chosen to keep the symmetric stabilator inside the "
                            + "setting region the research source exercised in its tabulated equilibria. It is a "
                            + "command bound of this gameplay layer - NOT a physical hard stop, NOT a sourced travel, "
                            + "and NOT MavF15ResearchDemonstratedControlRange (which stays a separate research fact)"
                },
                differentialStabilator = new MavF15GameplayChannelEnvelope
                {
                    minDeg = -6f,
                    maxDeg = 6f,
                    basis = tag + "0.3 x the aileron envelope (the research model's own differential-tail relation); "
                            + "no sourced differential-tail travel"
                },
                aileron = new MavF15GameplayChannelEnvelope
                {
                    minDeg = -20f,
                    maxDeg = 20f,
                    basis = tag + "gameplay aileron command envelope inside the region audited as monotonic and finite "
                            + "in the research rolling-moment fit; Table VII exercised 0 deg only"
                },
                rudder = new MavF15GameplayChannelEnvelope
                {
                    minDeg = -15f,
                    maxDeg = 15f,
                    basis = tag + "gameplay rudder command envelope inside the region audited as monotonic and finite "
                            + "in the research yawing-moment fit; Table VII exercised 0 deg only (flat-spin tables "
                            + "about -3 to +14 deg)"
                }
            };
        }

        public bool IsSelfConsistent(out string reason)
        {
            if (!(pitchStabilatorDegPerUnit >= 0f) || !(rollAileronDegPerUnit >= 0f) || !(yawRudderDegPerUnit >= 0f))
            {
                reason = "gearing must be finite and non-negative; direction comes from the conventions";
                return false;
            }

            for (int i = 0; i < 4; i++)
            {
                if (!Get((MavF15SurfaceChannel)i).IsValid)
                {
                    reason = ((MavF15SurfaceChannel)i) + " command envelope is empty, inverted or not finite";
                    return false;
                }
            }

            reason = "OK";
            return true;
        }

        /// <summary>
        /// The actuator travel this gameplay layer needs to move the surfaces at all: each envelope,
        /// declared as <see cref="MavEngineDataProvenance.MaverickTuning"/> and labelled. The rate is
        /// deliberately left Unavailable - no actuator dynamics are sourced, so the channels step to
        /// the command, which the actuator reports rather than hides.
        /// </summary>
        public MavF15SurfaceLimits ToActuatorTravel()
        {
            return new MavF15SurfaceLimits
            {
                symmetricStabilator = Travel(symmetricStabilator),
                differentialStabilator = Travel(differentialStabilator),
                aileron = Travel(aileron),
                rudder = Travel(rudder)
            };
        }

        private static MavF15SurfaceChannelLimits Travel(MavF15GameplayChannelEnvelope e)
        {
            return new MavF15SurfaceChannelLimits
            {
                minDeg = e.minDeg,
                maxDeg = e.maxDeg,
                rateLimitDegSec = 0f,
                travelProvenance = MavEngineDataProvenance.MaverickTuning,
                rateProvenance = MavEngineDataProvenance.Unavailable,
                sourceNote = MavF15PilotControlProvenance.MaverickTunedNonAuthoritative
                    + ": gameplay command envelope of " + MavF15PilotControlledIdentity.ConfigurationId
                    + " - not a physical hard stop, not sourced travel; no sourced actuator rate"
            };
        }
    }

    /// <summary>
    /// The validated research equilibrium the pilot-controlled aircraft starts from.
    ///
    /// Table VII point 36 - a symmetric, wings-level equilibrium of the research model, recovered by
    /// the WP-3B trim solver, classified STABLE in WP-3D (every mode decays; slowest -0.021 /s), and
    /// held to float precision on the Rigidbody in the final research closeout. Its stabilator,
    /// -15.145 deg, sits at the centre of the source-exercised region. MavF15PilotControlValidation
    /// ([P6]) re-solves it with the trim solver and checks these stored values against it.
    ///
    /// The stabilator value is also the pilot-neutral BIAS: with the stick centred the symmetric
    /// stabilator sits at the trim setting. That is trim, not a physical neutral surface position.
    /// </summary>
    [Serializable]
    public struct MavF15PilotTrimStart
    {
        public const string Provenance = MavF15PilotControlProvenance.ValidatedResearchEquilibrium;

        public int tableViiPoint;
        public double symmetricStabilatorDeg;
        public double alphaDeg;
        public double thetaDeg;
        public double trueAirspeedFtPerSec;
        public float altitudeM;
        public double headingDeg;
        public string sourceNote;

        public static MavF15PilotTrimStart TableViiPoint36()
        {
            return new MavF15PilotTrimStart
            {
                tableViiPoint = 36,
                symmetricStabilatorDeg = -15.145285606384277,
                alphaDeg = 17.4864559173584,
                thetaDeg = 14.546023368835447,
                trueAirspeedFtPerSec = 300.8,
                altitudeM = MavF15CoefficientFitCondition.PressureAltitudeM,
                headingDeg = 0.0,
                sourceNote = Provenance + ": Baumann Table VII point 36, recovered by the WP-3B symmetric trim "
                             + "solver (stabilator, alpha, theta at V = 300.8 ft/s; beta = p = q = r = phi = 0), "
                             + "STABLE in WP-3D, held on the Rigidbody in the research closeout"
            };
        }

        /// <summary>The same state in the research kinematics' input form.</summary>
        public MavF15ResearchRuntimeInitialState ToKinematicState()
        {
            const double degToRad = Math.PI / 180.0;
            return new MavF15ResearchRuntimeInitialState
            {
                alphaRad = alphaDeg * degToRad,
                betaRad = 0.0,
                pRadSec = 0.0,
                qRadSec = 0.0,
                rRadSec = 0.0,
                thetaRad = thetaDeg * degToRad,
                phiRad = 0.0,
                trueAirspeedFtPerSec = trueAirspeedFtPerSec,
                symmetricStabilatorDeg = symmetricStabilatorDeg,
                headingRad = headingDeg * degToRad,
                altitudeM = altitudeM,
                sourceNote = sourceNote
            };
        }
    }

    /// <summary>What the mapping produced for one command.</summary>
    public struct MavF15PilotControlSolution
    {
        /// <summary>The clamped normalized command the mapping used.</summary>
        public MavPilotCommand command;

        /// <summary>The requested surfaces, inside the gameplay envelopes.</summary>
        public MavF15SurfaceState requested;

        /// <summary>Before the envelopes.</summary>
        public MavF15SurfaceState unbounded;

        /// <summary>Which channels the envelope limited this step.</summary>
        public bool symmetricLimited, differentialLimited, aileronLimited, rudderLimited;

        public bool AnyLimited
        {
            get { return symmetricLimited || differentialLimited || aileronLimited || rudderLimited; }
        }
    }

    /// <summary>
    /// The direct stick-to-surface mapping, as a pure function. No state, no feedback, no protection:
    ///
    ///   symmetric stabilator = trim bias + NoseUpStabilatorSign x pitch x gearing
    ///   aileron              = RollRightAileronSign x roll x gearing
    ///   differential stab.   = 0.3 x commanded aileron          (research model relation)
    ///   rudder               = YawNoseRightRudderSign x yaw x gearing
    ///
    /// each clamped to its gameplay envelope. Neutral stick therefore returns exactly the trim bias
    /// and zero lateral surfaces. Continuous and monotonic in every input.
    /// </summary>
    public static class MavF15PilotControlMapping
    {
        public static MavF15PilotControlSolution Solve(
            MavPilotCommand command,
            MavF15GameplayControlAuthority authority,
            float trimStabilatorBiasDeg)
        {
            MavF15PilotControlSolution s = new MavF15PilotControlSolution();
            MavPilotCommand c = Finite(command).Clamped();
            s.command = c;

            float bias = IsFinite(trimStabilatorBiasDeg) ? trimStabilatorBiasDeg : 0f;
            float aileronCommand = MavF15PilotControlConventions.RollRightAileronSign * c.roll * authority.rollAileronDegPerUnit;

            s.unbounded.symmetricStabilatorDeg =
                bias + MavF15PilotControlConventions.NoseUpStabilatorSign * c.pitch * authority.pitchStabilatorDegPerUnit;
            s.unbounded.aileronDeg = aileronCommand;
            s.unbounded.differentialStabilatorDeg =
                MavF15PilotControlConventions.DifferentialTailPerCommandedAileron * aileronCommand;
            s.unbounded.rudderDeg =
                MavF15PilotControlConventions.YawNoseRightRudderSign * c.yaw * authority.yawRudderDegPerUnit;

            Bound(ref s, authority);
            return s;
        }

        /// <summary>
        /// The same mapping from a closed-loop law's surface DEMANDS instead of stick gearing - used by
        /// <see cref="MavF15PilotControlLawV2"/>, so V1 and V2 share one owner of the sign conventions,
        /// the research differential-tail relation and the gameplay envelopes:
        ///
        ///   symmetric stabilator = trim bias + NoseUpStabilatorSign x nose-up demand
        ///   aileron              = RollRightAileronSign x roll-right demand
        ///   differential stab.   = 0.3 x commanded aileron          (research model relation)
        ///   rudder               = YawNoseRightRudderSign x nose-right demand
        ///
        /// each clamped to its gameplay envelope. Zero demands return exactly the trim bias and zero
        /// lateral surfaces. A non-finite demand is treated as zero (a fault upstream is centred, never
        /// passed on). <paramref name="command"/> is only carried through for throttle and telemetry.
        /// </summary>
        public static MavF15PilotControlSolution SolveFromDemands(
            MavPilotCommand command,
            float noseUpDemandDeg,
            float rollRightDemandDeg,
            float noseRightDemandDeg,
            MavF15GameplayControlAuthority authority,
            float trimStabilatorBiasDeg)
        {
            MavF15PilotControlSolution s = new MavF15PilotControlSolution();
            s.command = Finite(command).Clamped();

            float bias = IsFinite(trimStabilatorBiasDeg) ? trimStabilatorBiasDeg : 0f;
            float pitch = IsFinite(noseUpDemandDeg) ? noseUpDemandDeg : 0f;
            float roll = IsFinite(rollRightDemandDeg) ? rollRightDemandDeg : 0f;
            float yaw = IsFinite(noseRightDemandDeg) ? noseRightDemandDeg : 0f;
            float aileronCommand = MavF15PilotControlConventions.RollRightAileronSign * roll;

            s.unbounded.symmetricStabilatorDeg = bias + MavF15PilotControlConventions.NoseUpStabilatorSign * pitch;
            s.unbounded.aileronDeg = aileronCommand;
            s.unbounded.differentialStabilatorDeg =
                MavF15PilotControlConventions.DifferentialTailPerCommandedAileron * aileronCommand;
            s.unbounded.rudderDeg = MavF15PilotControlConventions.YawNoseRightRudderSign * yaw;

            Bound(ref s, authority);
            return s;
        }

        private static void Bound(ref MavF15PilotControlSolution s, MavF15GameplayControlAuthority authority)
        {
            s.requested.symmetricStabilatorDeg = authority.symmetricStabilator.Clamp(s.unbounded.symmetricStabilatorDeg);
            s.requested.aileronDeg = authority.aileron.Clamp(s.unbounded.aileronDeg);
            s.requested.differentialStabilatorDeg = authority.differentialStabilator.Clamp(s.unbounded.differentialStabilatorDeg);
            s.requested.rudderDeg = authority.rudder.Clamp(s.unbounded.rudderDeg);

            s.symmetricLimited = s.requested.symmetricStabilatorDeg != s.unbounded.symmetricStabilatorDeg;
            s.aileronLimited = s.requested.aileronDeg != s.unbounded.aileronDeg;
            s.differentialLimited = s.requested.differentialStabilatorDeg != s.unbounded.differentialStabilatorDeg;
            s.rudderLimited = s.requested.rudderDeg != s.unbounded.rudderDeg;
        }

        private static MavPilotCommand Finite(MavPilotCommand c)
        {
            // A non-finite channel is a fault upstream: it is centred, never passed on.
            if (!IsFinite(c.pitch)) c.pitch = 0f;
            if (!IsFinite(c.roll)) c.roll = 0f;
            if (!IsFinite(c.yaw)) c.yaw = 0f;
            if (!IsFinite(c.throttle01)) c.throttle01 = 0f;
            return c;
        }

        private static bool IsFinite(float v)
        {
            return !float.IsNaN(v) && !float.IsInfinity(v);
        }
    }
}
