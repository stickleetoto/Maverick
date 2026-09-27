using System;
using UnityEngine;

namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// Every tunable number of the Maverick F-15 pilot-control law V2
    /// (<see cref="MavF15PilotControlLawV2"/>).
    ///
    /// SOURCE STATUS: ALL OF IT IS <see cref="MavF15PilotControlProvenance.MaverickTunedNonAuthoritative"/>.
    /// Not one command, gain, schedule value or limit here comes from an F-15 source, and none of it
    /// describes the F-15 CAS, FLCS, SAS or NASA 836 control law. Nor is any value copied from the
    /// Maverick F-16 control law (<c>MavF16ControlLawGains</c>): V2 reuses that law's ARCHITECTURE only,
    /// and every number was chosen for the frozen AFIT/Baumann/Davison research model at its validated
    /// trim (Table VII point 36) from deterministic closed-loop sweeps - see
    /// Docs/Reference/F15_PILOT_CONTROLLED_V2.md section 5 and <see cref="Entries"/>.
    ///
    /// The surface command envelopes are NOT here: V2 flies inside the same
    /// <see cref="MavF15GameplayControlAuthority"/> envelopes as V1, and the actuator travel is unchanged.
    /// </summary>
    [Serializable]
    public struct MavF15PilotControlGainsV2
    {
        public const string Provenance = MavF15PilotControlProvenance.MaverickTunedNonAuthoritative;

        [Header("Pitch: stick -> pitch-rate demand -> q feedback -> symmetric stabilator (MAVERICK_TUNED_NON_AUTHORITATIVE)")]
        [Tooltip("Body pitch rate commanded at full stick, deg/s.")]
        public float commandedPitchRateAtFullStickDegSec;

        [Tooltip("Feed-forward: nose-up stabilator demand per rad/s of commanded pitch rate, deg per rad/s. Sized from the research model's own short-term pitch response so the command, not the error, does most of the work.")]
        public float pitchRateFeedForwardDegPerRadSec;

        [Tooltip("Proportional pitch-rate feedback, deg of nose-up stabilator demand per rad/s of rate error.")]
        public float pitchRateGainDegPerRadSec;

        [Header("Roll: stick -> stability-axis roll-rate demand -> feedback -> aileron (+0.3 differential tail) (MAVERICK_TUNED_NON_AUTHORITATIVE)")]
        [Tooltip("Stability-axis roll rate (roll about the velocity vector) commanded at full stick, deg/s.")]
        public float commandedRollRateAtFullStickDegSec;

        [Tooltip("Feed-forward: roll-right aileron demand per rad/s of commanded roll rate, deg per rad/s.")]
        public float rollRateFeedForwardDegPerRadSec;

        [Tooltip("Proportional stability-axis roll-rate feedback, deg of aileron demand per rad/s of rate error.")]
        public float rollRateGainDegPerRadSec;

        [Header("Yaw: pedal -> sideslip demand -> beta feedback -> rudder, plus aileron-rudder interconnect (MAVERICK_TUNED_NON_AUTHORITATIVE)")]
        [Tooltip("Sideslip commanded at full pedal, deg. Right pedal (nose right) commands NEGATIVE beta in the beta = asin(v/V) convention.")]
        public float commandedSideslipAtFullPedalDeg;

        [Tooltip("Feed-forward: nose-right rudder demand per deg of commanded (nose-right, i.e. negative) sideslip, deg per deg.")]
        public float sideslipFeedForwardDegPerDeg;

        [Tooltip("Sideslip feedback, deg of nose-right rudder demand per deg of sideslip error.")]
        public float sideslipGainDegPerDeg;

        [Tooltip("Aileron-rudder interconnect: nose-right rudder demand per rad/s of commanded roll rate, deg per rad/s. Feed-forward of the yaw a roll about the velocity vector needs at this angle of attack.")]
        public float aileronRudderInterconnectDegPerRadSec;

        [Header("Washed-out stability-axis yaw-rate term - " + MavF15PilotControlProvenance.GameplayResearchAssist + " (MAVERICK_TUNED_NON_AUTHORITATIVE)")]
        [Tooltip("Nose-right rudder demand per rad/s of washed-out STABILITY-AXIS yaw rate (r cos alpha - p sin alpha), opposing it, deg per rad/s. Zero in a roll about the velocity vector, so it coordinates body-axis rolling and damps the Dutch roll. NOT an F-15 yaw damper, SAS or CAS.")]
        public float stabilityAxisYawRateGainDegPerRadSec;

        [Tooltip("Washout time constant of that yaw-rate term, s: removes the steady yaw rate of a sustained turn so the term does not fight it. <= 0 disables the washout.")]
        public float yawRateWashoutTimeConstantSeconds;

        [Header("Dynamic-pressure gain scheduling (MAVERICK_TUNED_NON_AUTHORITATIVE)")]
        [Tooltip("Scale every surface-demand gain by qbarRef / qbar, clamped. Surfaces only - never a load.")]
        public bool scheduleGainsWithDynamicPressure;

        [Tooltip("Reference dynamic pressure, Pa: the research source's own qbar at the point-36 trim (fixed source density, 300.8 ft/s), so the schedule is exactly 1 at the trim the gains were tuned at.")]
        public float referenceDynamicPressurePa;

        public float minimumGainScale;
        public float maximumGainScale;

        /// <summary>
        /// The V2 defaults. Why each value was chosen is in <see cref="Entries"/> and in
        /// Docs/Reference/F15_PILOT_CONTROLLED_V2.md section 5.
        /// </summary>
        public static MavF15PilotControlGainsV2 V2()
        {
            return new MavF15PilotControlGainsV2
            {
                commandedPitchRateAtFullStickDegSec = 7f,
                pitchRateFeedForwardDegPerRadSec = 60f,
                pitchRateGainDegPerRadSec = 30f,

                commandedRollRateAtFullStickDegSec = 15f,
                rollRateFeedForwardDegPerRadSec = 20f,
                rollRateGainDegPerRadSec = 50f,

                commandedSideslipAtFullPedalDeg = 1.5f,
                sideslipFeedForwardDegPerDeg = 4f,
                sideslipGainDegPerDeg = 4f,
                aileronRudderInterconnectDegPerRadSec = 40f,
                stabilityAxisYawRateGainDegPerRadSec = 40f,
                yawRateWashoutTimeConstantSeconds = 1.5f,

                scheduleGainsWithDynamicPressure = true,
                referenceDynamicPressurePa = ReferenceDynamicPressureAtTrimPa(),
                minimumGainScale = 0.5f,
                maximumGainScale = 2f
            };
        }

        /// <summary>
        /// qbar of the research source at the point-36 trim speed, in Pa: 0.5 x the source's fixed
        /// density x (300.8 ft/s)^2. Derived from the validated trim and the frozen source density - the
        /// one schedule value that is not a free choice.
        /// </summary>
        public static float ReferenceDynamicPressureAtTrimPa()
        {
            const double PsfToPa = 47.88025898033584;
            double v = MavF15PilotTrimStart.TableViiPoint36().trueAirspeedFtPerSec;
            double rho = MavF15SourceExercisedOperatingDomain.SourceAirDensitySlugPerFt3;
            return (float)(0.5 * rho * v * v * PsfToPa);
        }

        public bool IsSelfConsistent(out string reason)
        {
            float[] nonNegative =
            {
                commandedPitchRateAtFullStickDegSec, pitchRateFeedForwardDegPerRadSec, pitchRateGainDegPerRadSec,
                commandedRollRateAtFullStickDegSec, rollRateFeedForwardDegPerRadSec, rollRateGainDegPerRadSec,
                commandedSideslipAtFullPedalDeg, sideslipFeedForwardDegPerDeg, sideslipGainDegPerDeg, aileronRudderInterconnectDegPerRadSec,
                stabilityAxisYawRateGainDegPerRadSec, yawRateWashoutTimeConstantSeconds
            };

            for (int i = 0; i < nonNegative.Length; i++)
            {
                if (!(nonNegative[i] >= 0f) || float.IsInfinity(nonNegative[i]))
                {
                    reason = "every command and gain must be finite and non-negative; direction comes from MavF15PilotControlConventions";
                    return false;
                }
            }

            if (scheduleGainsWithDynamicPressure
                && (!(referenceDynamicPressurePa > 0f) || !(minimumGainScale > 0f) || !(maximumGainScale >= minimumGainScale)
                    || float.IsInfinity(referenceDynamicPressurePa) || float.IsInfinity(maximumGainScale)))
            {
                reason = "the dynamic-pressure schedule needs a positive reference and 0 < min <= max, all finite";
                return false;
            }

            reason = "OK";
            return true;
        }

        /// <summary>Every default with its unit and the reason it was chosen - the audit trail for the V2 tuning.</summary>
        public static MavF15TunedValue[] Entries()
        {
            MavF15PilotControlGainsV2 g = V2();
            return new[]
            {
                Entry("commandedPitchRateAtFullStickDegSec", g.commandedPitchRateAtFullStickDegSec, "deg/s",
                    "the point-36 trim is slow (300.8 ft/s) and already at alpha 17.5; with the gains below, full stick then asks for about the same "
                    + "stabilator travel as V1 full stick (~10 deg), i.e. up to the nose-up side of the gameplay envelope (9.9 deg from the trim bias)"),
                Entry("pitchRateFeedForwardDegPerRadSec", g.pitchRateFeedForwardDegPerRadSec, "deg per rad/s",
                    "inverse of the research model's own 1-s pitch-rate response per degree of stabilator (V1 pulse: ~0.015 rad/s per deg): with the "
                    + "feedback below, a 1-s half-stick pulse reaches ~0.9 of the commanded rate (40 -> 0.69, 80 -> 1.08 overshooting)"),
                Entry("pitchRateGainDegPerRadSec", g.pitchRateGainDegPerRadSec, "deg per rad/s",
                    "short-period damping 0.37 -> ~0.6 with the one-step law delay included; 10-20 left a longer post-release bobble, 40 buys no "
                    + "tracking and lowers phugoid damping further (0.15 open loop -> 0.115 at 30)"),
                Entry("commandedRollRateAtFullStickDegSec", g.commandedRollRateAtFullStickDegSec, "deg/s",
                    "full-stick roll about the velocity vector that keeps the coordinating rudder (interconnect + yaw-rate term) clear of its "
                    + "+-15 deg envelope (~11-12 deg at full stick); at 18 it reached ~14 deg, and pinned at 15 deg for 1-3 s with a stronger interconnect"),
                Entry("rollRateFeedForwardDegPerRadSec", g.rollRateFeedForwardDegPerRadSec, "deg per rad/s",
                    "about the inverse of the coordinated steady roll rate per degree of aileron at the trim (|L_p| / L_da, differential tail included)"),
                Entry("rollRateGainDegPerRadSec", g.rollRateGainDegPerRadSec, "deg per rad/s",
                    "stability-axis roll-rate feedback; at this alpha the research Dutch roll is mostly rolling, so this loop is also what damps it "
                    + "(40 -> zeta 0.27, 50 -> 0.31); higher saturates the aileron at half stick"),
                Entry("commandedSideslipAtFullPedalDeg", g.commandedSideslipAtFullPedalDeg, "deg",
                    "small on purpose: at alpha 17.5 half a degree of sideslip already makes a rolling moment of the order of half the aileron's; "
                    + "larger demands just roll the aircraft and saturate the rudder"),
                Entry("sideslipFeedForwardDegPerDeg", g.sideslipFeedForwardDegPerDeg, "deg per deg",
                    "feed-forward rudder for the pedal sideslip demand; with the feedback below, half pedal reaches ~60 % of the commanded sideslip in 1 s "
                    + "(the rest is lost to the dihedral roll the roll loop resists); 6 overshoots at full pedal"),
                Entry("sideslipGainDegPerDeg", g.sideslipGainDegPerDeg, "deg per deg",
                    "sideslip feedback; keeps |beta| to a few tenths of a degree in a commanded roll; 6-8 saturates the rudder at full pedal"),
                Entry("aileronRudderInterconnectDegPerRadSec", g.aileronRudderInterconnectDegPerRadSec, "deg per rad/s",
                    "feed-forward yaw for a roll about the velocity vector (r = p tan alpha): the transient needs far more rudder than the steady turn, "
                    + "so this is what makes roll entry fast (0 -> half-stick ps at 1 s ~0.3 of the command, 40 -> ~0.8); 50-60 overshoot the command"),
                Entry("stabilityAxisYawRateGainDegPerRadSec", g.stabilityAxisYawRateGainDegPerRadSec, "deg per rad/s",
                    MavF15PilotControlProvenance.GameplayResearchAssist + ": washed-out stability-axis yaw rate, opposed; zero in a coordinated roll. "
                    + "Adds Dutch-roll damping on top of the roll loop (zeta 0.25 -> 0.31 at 40) and trims the full-stick roll overshoot"),
                Entry("yawRateWashoutTimeConstantSeconds", g.yawRateWashoutTimeConstantSeconds, "s",
                    "passes the Dutch roll (period ~2 s) and removes a sustained turn's steady yaw rate within a few seconds; 1 and 2 s behaved alike"),
                Entry("scheduleGainsWithDynamicPressure", g.scheduleGainsWithDynamicPressure ? 1f : 0f, "bool",
                    "evaluated at research trims 265-400 ft/s: scheduling keeps the half-stick roll rate within ~0.4-1.2 of the command "
                    + "(unscheduled 0.3-1.5) and removes a 2.3x pitch overshoot at 400 ft/s; Dutch-roll damping stays >= 0.16 either way"),
                Entry("referenceDynamicPressurePa", g.referenceDynamicPressurePa, "Pa",
                    "DERIVED, not a free choice: the source's fixed density x the point-36 trim speed, so the schedule is exactly 1 where the gains were tuned"),
                Entry("minimumGainScale", g.minimumGainScale, "-",
                    "lower clamp of the schedule; never binds in the trimmable range (qbarRef/qbar >= 0.57 at 400 ft/s, where the trim stabilator "
                    + "reaches the -5 deg gameplay envelope edge)"),
                Entry("maximumGainScale", g.maximumGainScale, "-",
                    "upper clamp: never more than twice the tuned gains toward the low-speed edge of the research domain (218.5 ft/s gives 1.9)")
            };
        }

        private static MavF15TunedValue Entry(string name, float value, string unit, string rationale)
        {
            return new MavF15TunedValue
            {
                name = name,
                value = value,
                unit = unit,
                provenance = Provenance,
                rationale = rationale
            };
        }
    }

    /// <summary>One labelled, non-authoritative control-law value and why it was chosen.</summary>
    [Serializable]
    public struct MavF15TunedValue
    {
        public string name;
        public float value;
        public string unit;
        public string provenance;
        public string rationale;
    }
}
