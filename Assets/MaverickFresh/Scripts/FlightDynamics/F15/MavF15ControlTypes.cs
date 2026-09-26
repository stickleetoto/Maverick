using System;
using UnityEngine;

namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// F-15-layer pilot intent.
    ///
    /// The shared <see cref="MavPilotCommand"/> carries normalized pitch/roll/yaw/throttle and is
    /// correctly aircraft-independent. This adds what is specific to this aircraft: the F-15 flies
    /// with a mechanical control path in PARALLEL with a three-axis CAS, and each CAS axis can be
    /// disengaged independently. That is not a Maverick affordance, it is how the aeroplane works,
    /// and modelling it is what makes "CAS off" a state the control law can actually be in rather
    /// than a gain someone zeroed.
    /// </summary>
    [Serializable]
    public struct MavF15PilotCommand
    {
        [Range(-1f, 1f)] public float pitch;
        [Range(-1f, 1f)] public float roll;
        [Range(-1f, 1f)] public float yaw;
        [Range(0f, 1f)] public float throttle01;

        [Tooltip("Pitch CAS engaged. Disengaged leaves the mechanical path alone, which is a real F-15 condition, not a fault.")]
        public bool pitchCasEngaged;

        [Tooltip("Roll CAS engaged.")]
        public bool rollCasEngaged;

        [Tooltip("Yaw CAS engaged.")]
        public bool yawCasEngaged;

        /// <summary>
        /// Mechanical-only: every CAS axis disengaged. The mechanical path still flies the
        /// aircraft, which is the point of having one.
        /// </summary>
        public static MavF15PilotCommand MechanicalOnly
        {
            get { return new MavF15PilotCommand(); }
        }

        /// <summary>All three CAS axes engaged, stick and pedals centred.</summary>
        public static MavF15PilotCommand NeutralFullyAugmented
        {
            get
            {
                return new MavF15PilotCommand
                {
                    pitchCasEngaged = true,
                    rollCasEngaged = true,
                    yawCasEngaged = true
                };
            }
        }

        public static MavF15PilotCommand FromShared(MavPilotCommand shared, bool casEngaged)
        {
            MavPilotCommand c = shared.Clamped();
            return new MavF15PilotCommand
            {
                pitch = c.pitch,
                roll = c.roll,
                yaw = c.yaw,
                throttle01 = c.throttle01,
                pitchCasEngaged = casEngaged,
                rollCasEngaged = casEngaged,
                yawCasEngaged = casEngaged
            };
        }

        public MavF15PilotCommand Clamped()
        {
            MavF15PilotCommand r = this;
            r.pitch = Mathf.Clamp(pitch, -1f, 1f);
            r.roll = Mathf.Clamp(roll, -1f, 1f);
            r.yaw = Mathf.Clamp(yaw, -1f, 1f);
            r.throttle01 = Mathf.Clamp01(throttle01);
            return r;
        }

        public bool AnyCasEngaged
        {
            get { return pitchCasEngaged || rollCasEngaged || yawCasEngaged; }
        }
    }

    /// <summary>
    /// The intermediate the F-15 control path actually has: per-axis surface demand in degrees,
    /// after the mechanical gearing and the ratio changers but BEFORE augmentation.
    ///
    /// It exists as its own type because the ratio changers (PRAD/RRAD) scale mechanical authority
    /// only. Without a named boundary between "mechanical demand" and "augmented demand" it is
    /// very easy to write a ratio changer that also attenuates the damper, which would be wrong
    /// and would not look wrong.
    /// </summary>
    [Serializable]
    public struct MavF15ControlDemand
    {
        public float symmetricStabilatorDeg;
        public float aileronDeg;
        public float differentialStabilatorDeg;
        public float rudderDeg;

        public static MavF15ControlDemand Zero
        {
            get { return new MavF15ControlDemand(); }
        }

        public MavF15SurfaceState ToSurfaceState()
        {
            return new MavF15SurfaceState
            {
                symmetricStabilatorDeg = symmetricStabilatorDeg,
                differentialStabilatorDeg = differentialStabilatorDeg,
                aileronDeg = aileronDeg,
                rudderDeg = rudderDeg
            };
        }
    }

    /// <summary>
    /// What the control law is ASKING the actuator for.
    ///
    /// Deliberately a distinct type from <see cref="MavF15ActualSurfaceState"/> even though both
    /// carry the same four numbers. The ownership rule this project keeps is that only the
    /// actuator may say where a surface actually is; two distinct types make a violation a
    /// compile error at the point where someone would otherwise hand a request to the aerodynamic
    /// model and never notice.
    /// </summary>
    [Serializable]
    public struct MavF15RequestedSurfaceState
    {
        public MavF15SurfaceState channels;

        public static MavF15RequestedSurfaceState Neutral
        {
            get { return new MavF15RequestedSurfaceState { channels = MavF15SurfaceState.Neutral }; }
        }

        public static MavF15RequestedSurfaceState From(MavF15SurfaceState state)
        {
            return new MavF15RequestedSurfaceState { channels = state };
        }

        public bool IsFinite()
        {
            return channels.IsFinite();
        }
    }

    /// <summary>
    /// Where the surfaces ACTUALLY are.
    ///
    /// Only <see cref="MavF15ControlActuator"/> may produce one of these, via
    /// <see cref="FromActuator"/>. Nothing else in the F-15 layer calls that factory, and the
    /// ownership fixtures assert that the aerodynamic model reads this type and not a request.
    /// </summary>
    [Serializable]
    public struct MavF15ActualSurfaceState
    {
        public MavF15SurfaceState channels;

        public static MavF15ActualSurfaceState Neutral
        {
            get { return new MavF15ActualSurfaceState { channels = MavF15SurfaceState.Neutral }; }
        }

        /// <summary>
        /// The only way to construct an actual surface state. The name is the contract: if this
        /// appears anywhere outside the actuator, something other than the actuator is claiming to
        /// know where a surface is.
        /// </summary>
        public static MavF15ActualSurfaceState FromActuator(MavF15SurfaceState ownedState)
        {
            return new MavF15ActualSurfaceState { channels = ownedState };
        }

        public bool IsFinite()
        {
            return channels.IsFinite();
        }
    }

    /// <summary>
    /// What one FCS stage did this step, and why.
    ///
    /// Each stage reports separately so a control path that is 60% unavailable is legible as
    /// exactly which 40% ran, rather than as one aggregate "partially sourced" string. The
    /// provenance travels with the result, so telemetry can never show a contribution without
    /// showing what grade of data produced it.
    /// </summary>
    [Serializable]
    public struct MavF15FcsStageResult
    {
        public MavF15FcsStage stage;

        [Tooltip("True when the stage ran. False means its gains are unavailable or it was disengaged - the reason says which.")]
        public bool applied;

        [Tooltip("Lowest provenance grade among the gains this stage actually used. Unavailable when the stage did not run.")]
        public MavEngineDataProvenance provenance;

        [Tooltip("What this stage contributed, in surface degrees, on its own axis. Zero when it did not run.")]
        public float contributionDeg;

        [TextArea(1, 2)]
        public string reason;

        public static MavF15FcsStageResult NotApplied(MavF15FcsStage stage, string reason)
        {
            return new MavF15FcsStageResult
            {
                stage = stage,
                applied = false,
                provenance = MavEngineDataProvenance.Unavailable,
                contributionDeg = 0f,
                reason = reason
            };
        }

        public static MavF15FcsStageResult Applied(
            MavF15FcsStage stage,
            MavEngineDataProvenance provenance,
            float contributionDeg,
            string reason)
        {
            return new MavF15FcsStageResult
            {
                stage = stage,
                applied = true,
                provenance = provenance,
                contributionDeg = contributionDeg,
                reason = reason
            };
        }
    }
}
