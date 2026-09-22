using System;
using UnityEngine;

namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// One scalar gain, gearing or schedule value in the F-15 control system, carrying the
    /// provenance of the number with the number.
    ///
    /// A float on its own cannot answer the question this project keeps having to ask: is this a
    /// measured F-15 value, a strong public figure whose configuration match is unproven, or
    /// something somebody typed in to make the aircraft fly? A gain that cannot answer must not
    /// be used, so <see cref="Available"/> is false until the answer is supplied.
    ///
    /// NASA TM-72861 is the obvious near-term source for most of these, and it is genuinely good
    /// evidence - but its test aircraft was F-15 No. 8, a preproduction airframe later modified
    /// toward production standard. A gain taken from it is therefore
    /// <see cref="MavEngineDataProvenance.PublicReference"/>, never Authoritative, until
    /// equivalence with NASA 836 is actually demonstrated. The type makes that distinction
    /// survivable across edits; a comment would not.
    /// </summary>
    [Serializable]
    public struct MavF15ControlGain
    {
        public float value;

        [Tooltip("Where this number came from. Unavailable means the stage that uses it is refused, not that the gain is zero.")]
        public MavEngineDataProvenance provenance;

        [TextArea(1, 3)]
        [Tooltip("Document, table/figure and configuration. A provenance grade without a citation is not provenance.")]
        public string sourceNote;

        public bool Available
        {
            get { return provenance != MavEngineDataProvenance.Unavailable; }
        }

        /// <summary>
        /// True when this gain may be presented as exact NASA 836 authority. Deliberately narrow:
        /// only an explicitly Authoritative grade qualifies.
        /// </summary>
        public bool IsExactTargetAuthority
        {
            get { return provenance == MavEngineDataProvenance.Authoritative; }
        }

        public static MavF15ControlGain Unavailable(string why)
        {
            return new MavF15ControlGain
            {
                value = 0f,
                provenance = MavEngineDataProvenance.Unavailable,
                sourceNote = why
            };
        }
    }

    /// <summary>
    /// The stages of the F-15 control path, in the order the aircraft applies them.
    ///
    /// Named individually so readiness reporting can say WHICH part of the system is missing
    /// rather than "the FCS is unavailable". Each stage is separately gated: the architecture
    /// exists in full today, and stages switch on one at a time as their gains are sourced.
    /// </summary>
    public enum MavF15FcsStage
    {
        /// <summary>Stick and pedal through the mechanical linkage to surface demand. Gearing.</summary>
        MechanicalPath = 0,

        /// <summary>Pitch ratio changer: scales mechanical pitch authority with flight condition.</summary>
        PitchRatioChanger = 1,

        /// <summary>Roll ratio changer: scales mechanical roll authority with flight condition.</summary>
        RollRatioChanger = 2,

        /// <summary>Pitch control augmentation: rate and load-factor feedback.</summary>
        PitchCas = 3,

        /// <summary>Roll control augmentation: roll-rate feedback.</summary>
        RollCas = 4,

        /// <summary>Yaw control augmentation: yaw damper.</summary>
        YawCas = 5,

        /// <summary>Aileron-rudder interconnect: roll command crossfed to rudder.</summary>
        AileronRudderInterconnect = 6,

        /// <summary>High-AOA roll-damper washout.</summary>
        HighAoaRollDamperWashout = 7,

        /// <summary>Stall inhibitor / AoA limiter acting on the pitch axis.</summary>
        StallInhibitor = 8,

        /// <summary>Turn coordination: rudder from the kinematically required yaw rate.</summary>
        TurnCoordination = 9,

        /// <summary>Roll-rate to yaw crossfeed. Distinct from ARI, which is fed by roll COMMAND.</summary>
        RollToYawCrossfeed = 10
    }

    /// <summary>
    /// Which body of evidence an F-15 control-law configuration is drawing on.
    ///
    /// The mode is not decoration and it is not a preset. It declares a PROVENANCE FLOOR, and the
    /// law refuses to run any gain that sits below the floor its mode declares. That is what stops
    /// a preproduction F-15 No. 8 schedule and an exact NASA 836 value being averaged, mixed, or
    /// quietly co-resident in one configuration - which is the specific failure this project keeps
    /// guarding against.
    /// </summary>
    public enum MavF15FcsMode
    {
        /// <summary>
        /// The exact NASA 836 control system is not available. Only Authoritative gains may run,
        /// and none exist, so the law outputs neutral. This is the default and the current state.
        /// </summary>
        ExactNasa836Unavailable = 0,

        /// <summary>
        /// F-15-family evidence, principally NASA TM-72861. Accepts PublicReference and above.
        ///
        /// TM-72861's test aircraft was F-15 No. 8, a preproduction airframe later modified toward
        /// production standard, so its numbers are strong F-15-family evidence whose configuration
        /// match to NASA 836 is unproven. A law in this mode must never be described as the
        /// NASA 836 FCS.
        /// </summary>
        F15FamilyReference = 1,

        /// <summary>
        /// AFIT research lineage. Accepts CrossValidationOnly and above, which is what pairs with
        /// the Baumann M=0.6 research aerodynamic model. Research only.
        /// </summary>
        AFITResearch = 2
    }

    public static class MavF15FcsModes
    {
        /// <summary>
        /// The lowest provenance grade a gain may carry and still be allowed to run in this mode.
        /// </summary>
        public static MavEngineDataProvenance ProvenanceFloor(MavF15FcsMode mode)
        {
            switch (mode)
            {
                case MavF15FcsMode.ExactNasa836Unavailable:
                    return MavEngineDataProvenance.Authoritative;
                case MavF15FcsMode.F15FamilyReference:
                    return MavEngineDataProvenance.PublicReference;
                default:
                    return MavEngineDataProvenance.CrossValidationOnly;
            }
        }

        /// <summary>
        /// True when a gain of this provenance is admissible in this mode. Unavailable is never
        /// admissible: it is the absence of a number, not a low grade of one.
        /// </summary>
        public static bool Admits(MavF15FcsMode mode, MavEngineDataProvenance provenance)
        {
            if (provenance == MavEngineDataProvenance.Unavailable)
                return false;

            return provenance >= ProvenanceFloor(mode);
        }

        public static string Describe(MavF15FcsMode mode)
        {
            switch (mode)
            {
                case MavF15FcsMode.ExactNasa836Unavailable:
                    return "exact NASA 836 FCS unavailable; only Authoritative gains may run";
                case MavF15FcsMode.F15FamilyReference:
                    return "F-15-family reference (TM-72861 lineage); NOT NASA 836 authority";
                default:
                    return "AFIT research; cross-validation only";
            }
        }
    }

    /// <summary>
    /// Gains and schedules for the whole F-15 control path.
    ///
    /// EVERY value defaults to Unavailable. That is the honest state: no F-15 FCS gain has been
    /// recovered for NASA 836, and the handoff forbids substituting F-16 FLCS gains or
    /// reconstructing a schedule to make the aircraft controllable.
    ///
    /// The architecture is nonetheless implemented in full, because the ownership boundary is
    /// worth having before the numbers arrive: with this in place, sourcing a gain is a data
    /// change, not a redesign.
    /// </summary>
    [Serializable]
    public struct MavF15ControlLawSchedules
    {
        [Header("Mechanical path (stick/pedal gearing to surface degrees)")]
        public MavF15ControlGain pitchStickToStabilatorDegPerUnit;
        public MavF15ControlGain rollStickToAileronDegPerUnit;
        public MavF15ControlGain rollStickToDifferentialStabilatorDegPerUnit;
        public MavF15ControlGain pedalToRudderDegPerUnit;

        [Header("Ratio changers (PRAD / RRAD)")]
        [Tooltip("Fraction of mechanical pitch authority passed at the current condition, 0..1.")]
        public MavF15ControlGain pitchRatioChanger;

        [Tooltip("Fraction of mechanical roll authority passed at the current condition, 0..1.")]
        public MavF15ControlGain rollRatioChanger;

        [Header("Control augmentation (CAS)")]
        [Tooltip("Stabilator degrees per rad/s of pitch rate. Sign convention is the aerodynamic model's.")]
        public MavF15ControlGain pitchRateFeedbackDegPerRadSec;

        [Tooltip("Stabilator degrees per g of normal load factor error.")]
        public MavF15ControlGain normalLoadFactorFeedbackDegPerG;

        [Tooltip("Differential stabilator degrees per rad/s of roll rate.")]
        public MavF15ControlGain rollRateFeedbackDegPerRadSec;

        [Tooltip("Rudder degrees per rad/s of yaw rate - the yaw damper.")]
        public MavF15ControlGain yawRateFeedbackDegPerRadSec;

        [Header("Aileron-rudder interconnect")]
        [Tooltip("Rudder degrees per degree of commanded aileron.")]
        public MavF15ControlGain ariRudderPerAileron;

        [Header("Stall inhibitor / AoA limiter")]
        [Tooltip("Alpha beyond which the inhibitor commands nose-down, degrees.")]
        public MavF15ControlGain stallInhibitorAlphaThresholdDeg;

        [Tooltip("Stabilator degrees of nose-down authority per degree of alpha beyond the threshold.")]
        public MavF15ControlGain stallInhibitorDegPerDegAlpha;

        [Header("Turn coordination and crossfeeds")]
        [Tooltip("Rudder degrees per rad/s of yaw-rate error against the coordinated-turn value.")]
        public MavF15ControlGain turnCoordinationDegPerRadSec;

        [Tooltip("Rudder degrees per rad/s of ROLL RATE. Distinct from the ARI, which is fed by roll command.")]
        public MavF15ControlGain rollRateToYawCrossfeedDegPerRadSec;

        [Header("High-AOA roll-damper washout")]
        [Tooltip("Washout schedule with its own provenance and declared curve shape. See MavF15RollDamperSchedule - an endpoint alone is not a schedule.")]
        public MavF15RollDamperSchedule rollDamperWashout;

        public MavF15ControlGain Get(MavF15FcsStage stage)
        {
            switch (stage)
            {
                case MavF15FcsStage.MechanicalPath:
                    return pitchStickToStabilatorDegPerUnit;
                case MavF15FcsStage.PitchRatioChanger:
                    return pitchRatioChanger;
                case MavF15FcsStage.RollRatioChanger:
                    return rollRatioChanger;
                case MavF15FcsStage.PitchCas:
                    return pitchRateFeedbackDegPerRadSec;
                case MavF15FcsStage.RollCas:
                    return rollRateFeedbackDegPerRadSec;
                case MavF15FcsStage.YawCas:
                    return yawRateFeedbackDegPerRadSec;
                case MavF15FcsStage.AileronRudderInterconnect:
                    return ariRudderPerAileron;
                case MavF15FcsStage.StallInhibitor:
                    return stallInhibitorDegPerDegAlpha;
                case MavF15FcsStage.TurnCoordination:
                    return turnCoordinationDegPerRadSec;
                case MavF15FcsStage.RollToYawCrossfeed:
                    return rollRateToYawCrossfeedDegPerRadSec;
                default:
                    // HighAoaRollDamperWashout is a schedule, not a scalar gain. It has no
                    // MavF15ControlGain to return, so callers must ask StageAvailable instead.
                    return MavF15ControlGain.Unavailable(
                        "the washout is a MavF15RollDamperSchedule, not a scalar gain");
            }
        }

        /// <summary>
        /// The mechanical path needs all four gearings before it can produce any surface demand at
        /// all. Reported separately because it is the one stage whose absence disables everything
        /// downstream - augmentation has nothing to augment.
        /// </summary>
        public bool MechanicalPathAvailable
        {
            get
            {
                return pitchStickToStabilatorDegPerUnit.Available
                    && rollStickToAileronDegPerUnit.Available
                    && rollStickToDifferentialStabilatorDegPerUnit.Available
                    && pedalToRudderDegPerUnit.Available;
            }
        }

        public bool StageAvailable(MavF15FcsStage stage)
        {
            switch (stage)
            {
                case MavF15FcsStage.MechanicalPath:
                    return MechanicalPathAvailable;

                case MavF15FcsStage.PitchCas:
                    // Either feedback path on its own is a usable pitch CAS.
                    return pitchRateFeedbackDegPerRadSec.Available
                        || normalLoadFactorFeedbackDegPerG.Available;

                case MavF15FcsStage.HighAoaRollDamperWashout:
                    return rollDamperWashout.Available;

                case MavF15FcsStage.StallInhibitor:
                    // The inhibitor needs a threshold AND an authority gradient; either alone
                    // cannot produce a command.
                    return stallInhibitorAlphaThresholdDeg.Available
                        && stallInhibitorDegPerDegAlpha.Available;

                default:
                    return Get(stage).Available;
            }
        }

        /// <summary>
        /// True only if EVERY gain in the whole path is Authoritative for the exact target. Used
        /// to stop a partially-sourced control law ever being described as the F-15 FLCS.
        /// </summary>
        public bool IsFullyExactTargetAuthoritative
        {
            get
            {
                return pitchStickToStabilatorDegPerUnit.IsExactTargetAuthority
                    && rollStickToAileronDegPerUnit.IsExactTargetAuthority
                    && rollStickToDifferentialStabilatorDegPerUnit.IsExactTargetAuthority
                    && pedalToRudderDegPerUnit.IsExactTargetAuthority
                    && pitchRatioChanger.IsExactTargetAuthority
                    && rollRatioChanger.IsExactTargetAuthority
                    && pitchRateFeedbackDegPerRadSec.IsExactTargetAuthority
                    && normalLoadFactorFeedbackDegPerG.IsExactTargetAuthority
                    && rollRateFeedbackDegPerRadSec.IsExactTargetAuthority
                    && yawRateFeedbackDegPerRadSec.IsExactTargetAuthority
                    && ariRudderPerAileron.IsExactTargetAuthority
                    && stallInhibitorAlphaThresholdDeg.IsExactTargetAuthority
                    && stallInhibitorDegPerDegAlpha.IsExactTargetAuthority
                    && turnCoordinationDegPerRadSec.IsExactTargetAuthority
                    && rollRateToYawCrossfeedDegPerRadSec.IsExactTargetAuthority
                    && rollDamperWashout.provenance == MavEngineDataProvenance.Authoritative;
            }
        }

        /// <summary>
        /// Every scalar gain in the set, for whole-set checks.
        /// </summary>
        public MavF15ControlGain[] AllGains()
        {
            return new[]
            {
                pitchStickToStabilatorDegPerUnit,
                rollStickToAileronDegPerUnit,
                rollStickToDifferentialStabilatorDegPerUnit,
                pedalToRudderDegPerUnit,
                pitchRatioChanger,
                rollRatioChanger,
                pitchRateFeedbackDegPerRadSec,
                normalLoadFactorFeedbackDegPerG,
                rollRateFeedbackDegPerRadSec,
                yawRateFeedbackDegPerRadSec,
                ariRudderPerAileron,
                stallInhibitorAlphaThresholdDeg,
                stallInhibitorDegPerDegAlpha,
                turnCoordinationDegPerRadSec,
                rollRateToYawCrossfeedDegPerRadSec
            };
        }

        /// <summary>
        /// Whether every DECLARED gain is admissible in this mode.
        ///
        /// This is the anti-mixing check. A configuration that pairs an Authoritative NASA 836
        /// gain with a preproduction TM-72861 one is not "mostly sourced", it is two aircraft
        /// averaged together, and the mode floor makes that refusable rather than merely
        /// regrettable. Unavailable gains are ignored here - an absent number is not a mixed one,
        /// it simply means its stage will not run.
        /// </summary>
        public bool IsConsistentWith(MavF15FcsMode mode, out string reason)
        {
            MavF15ControlGain[] gains = AllGains();
            for (int i = 0; i < gains.Length; i++)
            {
                if (gains[i].provenance == MavEngineDataProvenance.Unavailable)
                    continue;

                if (!MavF15FcsModes.Admits(mode, gains[i].provenance))
                {
                    reason = "a gain graded " + gains[i].provenance
                             + " is below the " + MavF15FcsModes.ProvenanceFloor(mode)
                             + " floor declared by mode " + mode;
                    return false;
                }
            }

            if (rollDamperWashout.provenance != MavEngineDataProvenance.Unavailable
                && !MavF15FcsModes.Admits(mode, rollDamperWashout.provenance))
            {
                reason = "the roll-damper washout schedule is graded "
                         + rollDamperWashout.provenance + ", below the "
                         + MavF15FcsModes.ProvenanceFloor(mode) + " floor for mode " + mode;
                return false;
            }

            reason = "OK";
            return true;
        }

        /// <summary>
        /// The fail-closed default. Nothing sourced, so the control law produces neutral surfaces
        /// and says which stages are missing.
        /// </summary>
        public static MavF15ControlLawSchedules Unavailable()
        {
            const string mech =
                "UNAVAILABLE: F-15 mechanical control gearing not recovered for NASA 836. "
                + "DN-1180.01-238-458 Rev. D (F-15 Flight Control System Description) would close this.";
            const string cas =
                "UNAVAILABLE: F-15 CAS gain not recovered for NASA 836. NASA TM-72861 carries "
                + "F-15-family CAS architecture and gains, but for preproduction F-15 No. 8 - "
                + "PublicReference at best until equivalence with 836 is demonstrated.";

            return new MavF15ControlLawSchedules
            {
                pitchStickToStabilatorDegPerUnit = MavF15ControlGain.Unavailable(mech),
                rollStickToAileronDegPerUnit = MavF15ControlGain.Unavailable(mech),
                rollStickToDifferentialStabilatorDegPerUnit = MavF15ControlGain.Unavailable(mech),
                pedalToRudderDegPerUnit = MavF15ControlGain.Unavailable(mech),
                pitchRatioChanger = MavF15ControlGain.Unavailable(
                    "UNAVAILABLE: PRAD schedule not recovered for NASA 836."),
                rollRatioChanger = MavF15ControlGain.Unavailable(
                    "UNAVAILABLE: RRAD schedule not recovered for NASA 836."),
                pitchRateFeedbackDegPerRadSec = MavF15ControlGain.Unavailable(cas),
                normalLoadFactorFeedbackDegPerG = MavF15ControlGain.Unavailable(cas),
                rollRateFeedbackDegPerRadSec = MavF15ControlGain.Unavailable(cas),
                yawRateFeedbackDegPerRadSec = MavF15ControlGain.Unavailable(cas),
                ariRudderPerAileron = MavF15ControlGain.Unavailable(
                    "UNAVAILABLE: ARI schedule not recovered for NASA 836."),
                stallInhibitorAlphaThresholdDeg = MavF15ControlGain.Unavailable(
                    "UNAVAILABLE: stall-inhibitor alpha threshold not recovered for NASA 836."),
                stallInhibitorDegPerDegAlpha = MavF15ControlGain.Unavailable(
                    "UNAVAILABLE: stall-inhibitor authority gradient not recovered for NASA 836."),
                turnCoordinationDegPerRadSec = MavF15ControlGain.Unavailable(
                    "UNAVAILABLE: turn-coordination gain not recovered for NASA 836."),
                rollRateToYawCrossfeedDegPerRadSec = MavF15ControlGain.Unavailable(
                    "UNAVAILABLE: roll-to-yaw crossfeed gain not recovered for NASA 836."),
                rollDamperWashout = MavF15RollDamperSchedule.Unavailable()
            };
        }
    }
}
