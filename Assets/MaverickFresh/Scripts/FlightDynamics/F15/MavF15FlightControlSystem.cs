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
        HighAoaRollDamperWashout = 7
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

        [Header("High-AOA roll-damper washout")]
        [Tooltip("Alpha at which roll-rate feedback begins washing out, degrees.")]
        public MavF15ControlGain rollDamperWashoutStartAlphaDeg;

        [Tooltip("Alpha at which roll-rate feedback is fully washed out, degrees.")]
        public MavF15ControlGain rollDamperWashoutEndAlphaDeg;

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
                default:
                    return rollDamperWashoutStartAlphaDeg;
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
                    // A washout needs both ends of its ramp, and they must be ordered.
                    return rollDamperWashoutStartAlphaDeg.Available
                        && rollDamperWashoutEndAlphaDeg.Available
                        && rollDamperWashoutEndAlphaDeg.value
                           > rollDamperWashoutStartAlphaDeg.value;

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
                    && rollDamperWashoutStartAlphaDeg.IsExactTargetAuthority
                    && rollDamperWashoutEndAlphaDeg.IsExactTargetAuthority;
            }
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
                rollDamperWashoutStartAlphaDeg = MavF15ControlGain.Unavailable(
                    "UNAVAILABLE: high-AOA roll-damper washout schedule not recovered for NASA 836."),
                rollDamperWashoutEndAlphaDeg = MavF15ControlGain.Unavailable(
                    "UNAVAILABLE: high-AOA roll-damper washout schedule not recovered for NASA 836.")
            };
        }
    }
}
