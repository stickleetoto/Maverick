using System;
using UnityEngine;

namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// The four physical control-surface channels of the F-15.
    ///
    /// The shared <see cref="MavControlInput"/> carries elevator / aileron / rudder / LEF, which
    /// fits the F-16 but cannot express this aircraft: the F-15 drives its stabilators both
    /// symmetrically (pitch) and differentially (roll), and the differential half is an
    /// independent physical state, not a function of aileron.
    ///
    /// Forcing differential tail into a generic field would give that field two different
    /// meanings depending on which aircraft owned it, so it stays out of the shared core. The
    /// core keeps consuming coefficients and loads, which really are aircraft-independent.
    /// </summary>
    public enum MavF15SurfaceChannel
    {
        SymmetricStabilator = 0,
        DifferentialStabilator = 1,
        Aileron = 2,
        Rudder = 3
    }

    /// <summary>
    /// A complete F-15 control-surface position set, in physical deflection degrees.
    ///
    /// Used for BOTH the requested and the actual state. They are the same physical quantity;
    /// what differs is who owns them. The control law owns requested, the actuator owns actual,
    /// and only the actuator's copy may reach the aerodynamic model - see
    /// <see cref="MavF15ControlActuator"/>.
    ///
    /// Sign convention is owned by the aerodynamic model that consumes this, exactly as the
    /// shared <see cref="MavControlInput"/> contract states. No sign convention is asserted here,
    /// because the exact NASA 836 sign authority is not frozen.
    /// </summary>
    [Serializable]
    public struct MavF15SurfaceState
    {
        public float symmetricStabilatorDeg;
        public float differentialStabilatorDeg;
        public float aileronDeg;
        public float rudderDeg;

        public static MavF15SurfaceState Neutral
        {
            get { return new MavF15SurfaceState(); }
        }

        public float Get(MavF15SurfaceChannel channel)
        {
            switch (channel)
            {
                case MavF15SurfaceChannel.SymmetricStabilator: return symmetricStabilatorDeg;
                case MavF15SurfaceChannel.DifferentialStabilator: return differentialStabilatorDeg;
                case MavF15SurfaceChannel.Aileron: return aileronDeg;
                default: return rudderDeg;
            }
        }

        public void Set(MavF15SurfaceChannel channel, float valueDeg)
        {
            switch (channel)
            {
                case MavF15SurfaceChannel.SymmetricStabilator:
                    symmetricStabilatorDeg = valueDeg; break;
                case MavF15SurfaceChannel.DifferentialStabilator:
                    differentialStabilatorDeg = valueDeg; break;
                case MavF15SurfaceChannel.Aileron:
                    aileronDeg = valueDeg; break;
                default:
                    rudderDeg = valueDeg; break;
            }
        }

        public bool IsFinite()
        {
            return Finite(symmetricStabilatorDeg)
                && Finite(differentialStabilatorDeg)
                && Finite(aileronDeg)
                && Finite(rudderDeg);
        }

        private static bool Finite(float v)
        {
            return !float.IsNaN(v) && !float.IsInfinity(v);
        }
    }

    /// <summary>
    /// Travel and rate authority for ONE F-15 surface channel, with the provenance of those
    /// numbers attached to them.
    ///
    /// The provenance is not decoration. The handoff requires that where configurations disagree
    /// - NASA 836 versus F-15 No. 8 preproduction versus the AFIT research model - both paths are
    /// preserved rather than one being silently chosen. A limit that does not know where it came
    /// from cannot satisfy that, so the source is carried with the number and
    /// <see cref="Available"/> is false until someone declares it.
    ///
    /// <see cref="MavEngineDataProvenance"/> is reused rather than duplicated: despite the name
    /// its vocabulary is aircraft-data-generic, and its middle grades are exactly the distinctions
    /// this needs. A travel limit read from NASA TM-72861 is PublicReference - a strong public
    /// source whose configuration match to 836 is still unproven - and must not be promoted to
    /// Authoritative without that proof.
    /// </summary>
    [Serializable]
    public struct MavF15SurfaceChannelLimits
    {
        public float minDeg;
        public float maxDeg;

        [Tooltip("Actuator rate limit in deg/s. Zero or negative means NO sourced finite rate, and the channel moves instantly - which is declared by rateProvenance, not inferred from the number.")]
        public float rateLimitDegSec;

        [Tooltip("Where the travel limits came from. Unavailable means the channel is refused, not that it is free.")]
        public MavEngineDataProvenance travelProvenance;

        [Tooltip("Where the rate limit came from. Unavailable with a zero rate means 'no sourced actuator dynamics', which is honest. Unavailable with a non-zero rate is a contradiction and is refused.")]
        public MavEngineDataProvenance rateProvenance;

        [TextArea(1, 3)]
        public string sourceNote;

        /// <summary>
        /// True only when travel authority has actually been declared AND the interval is sane.
        /// An undeclared channel is refused: zero travel is the fail-closed state, and it must not
        /// be reachable by leaving a struct default and hoping.
        /// </summary>
        public bool Available
        {
            get
            {
                return travelProvenance != MavEngineDataProvenance.Unavailable
                    && maxDeg > minDeg;
            }
        }

        /// <summary>
        /// True when a finite actuator rate has been declared. A zero/negative rate with
        /// Unavailable provenance is the honest "no sourced actuator dynamics" state and means
        /// the channel steps instantly; that is visible rather than hidden.
        /// </summary>
        public bool HasSourcedRate
        {
            get
            {
                return rateProvenance != MavEngineDataProvenance.Unavailable
                    && rateLimitDegSec > 0f;
            }
        }

        /// <summary>
        /// A declared rate with no number, or a number with no declared source, is a contradiction
        /// - someone half-filled the struct. Refusing is safer than guessing which half was meant.
        /// </summary>
        public bool IsSelfConsistent(out string reason)
        {
            if (rateProvenance != MavEngineDataProvenance.Unavailable && rateLimitDegSec <= 0f)
            {
                reason = "a rate source is declared but the rate is not positive";
                return false;
            }

            if (rateProvenance == MavEngineDataProvenance.Unavailable && rateLimitDegSec > 0f)
            {
                reason = "a finite rate is set but its source is Unavailable";
                return false;
            }

            if (travelProvenance != MavEngineDataProvenance.Unavailable && maxDeg <= minDeg)
            {
                reason = "a travel source is declared but the travel interval is empty or inverted";
                return false;
            }

            reason = "OK";
            return true;
        }

        public static MavF15SurfaceChannelLimits Unavailable(string why)
        {
            return new MavF15SurfaceChannelLimits
            {
                minDeg = 0f,
                maxDeg = 0f,
                rateLimitDegSec = 0f,
                travelProvenance = MavEngineDataProvenance.Unavailable,
                rateProvenance = MavEngineDataProvenance.Unavailable,
                sourceNote = why
            };
        }
    }

    /// <summary>
    /// Travel and rate authority for all four F-15 channels.
    /// </summary>
    [Serializable]
    public struct MavF15SurfaceLimits
    {
        public MavF15SurfaceChannelLimits symmetricStabilator;
        public MavF15SurfaceChannelLimits differentialStabilator;
        public MavF15SurfaceChannelLimits aileron;
        public MavF15SurfaceChannelLimits rudder;

        public MavF15SurfaceChannelLimits Get(MavF15SurfaceChannel channel)
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
        /// Every channel whose travel authority is declared and self-consistent.
        /// </summary>
        public int AvailableChannelCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < 4; i++)
                {
                    MavF15SurfaceChannelLimits c = Get((MavF15SurfaceChannel)i);
                    string ignored;
                    if (c.Available && c.IsSelfConsistent(out ignored))
                        n++;
                }
                return n;
            }
        }

        /// <summary>
        /// Clamps each channel to its declared travel. A channel with no declared travel is driven
        /// to ZERO, not passed through: an undeclared limit means the aircraft's authority is
        /// unknown, and unknown authority must not become unlimited authority.
        /// </summary>
        public MavF15SurfaceState Clamp(MavF15SurfaceState requested, out bool anyRefused)
        {
            anyRefused = false;
            MavF15SurfaceState bounded = MavF15SurfaceState.Neutral;

            for (int i = 0; i < 4; i++)
            {
                MavF15SurfaceChannel channel = (MavF15SurfaceChannel)i;
                MavF15SurfaceChannelLimits limits = Get(channel);

                string ignored;
                if (!limits.Available || !limits.IsSelfConsistent(out ignored))
                {
                    anyRefused = true;
                    bounded.Set(channel, 0f);
                    continue;
                }

                bounded.Set(
                    channel,
                    Mathf.Clamp(requested.Get(channel), limits.minDeg, limits.maxDeg)
                );
            }

            return bounded;
        }

        /// <summary>
        /// The fail-closed default: nothing declared, so nothing moves.
        ///
        /// This is the honest exact-target state today. The frozen NASA 836 control travel, sign
        /// authority, actuator rates and servo dynamics have not been recovered, and the handoff
        /// forbids substituting generic F-15-family or preproduction numbers to make the aircraft
        /// controllable. See Docs/Reference/F15_FULL_SCALE_DATA_GAPS_V0.2.md.
        /// </summary>
        public static MavF15SurfaceLimits UnavailableExactTarget()
        {
            const string why =
                "UNAVAILABLE for NASA F-15B 836 pre-Quiet-Spike: exact control travel, sign "
                + "authority and actuator dynamics are not frozen. MDC A4172 Part II and "
                + "DN-1180.01-238-458 Rev. D would close this.";

            return new MavF15SurfaceLimits
            {
                symmetricStabilator = MavF15SurfaceChannelLimits.Unavailable(why),
                differentialStabilator = MavF15SurfaceChannelLimits.Unavailable(why),
                aileron = MavF15SurfaceChannelLimits.Unavailable(why),
                rudder = MavF15SurfaceChannelLimits.Unavailable(why)
            };
        }
    }
}
