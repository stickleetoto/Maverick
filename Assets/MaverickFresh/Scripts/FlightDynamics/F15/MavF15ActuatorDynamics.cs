using System;
using UnityEngine;

namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// Which physics revision the PILOT-CONTROLLED F-15 flies. Versioned so that every recorded pilot result stays
    /// reproducible: R1 is exactly the plant the V1 and V2 records were made on.
    /// </summary>
    public enum MavF15PilotPhysicsRevision
    {
        /// <summary>The recorded pilot physics (V1 / V2 records): each surface moves to its bounded command instantly.</summary>
        R1InstantaneousSurfaces = 0,

        /// <summary>
        /// R1 plus the research model's own first-order surface lags (<see cref="MavF15ResearchModelActuatorLags"/>).
        /// Aerodynamics, thrust, mass, environment, travel and control law are unchanged.
        /// </summary>
        R2SourceActuatorLags = 1
    }

    /// <summary>
    /// A first-order actuator lag for one surface channel: x' = lagPerSec * (command - x). Bandwidth, not a rate
    /// limit - a lag slows every change in proportion to its size, a rate limit caps the speed. The two are kept
    /// apart (<see cref="MavF15SurfaceChannelLimits.rateLimitDegSec"/> stays the rate), and a channel may have either,
    /// both or neither.
    /// </summary>
    [Serializable]
    public struct MavF15ChannelLag
    {
        [Tooltip("First-order bandwidth, 1/s. Zero with Unavailable provenance means no lag: the channel follows its bounded command instantly.")]
        public float lagPerSec;

        [Tooltip("Where the bandwidth came from. Unavailable means no lag is modelled, whatever the number says.")]
        public MavEngineDataProvenance provenance;

        [TextArea(1, 3)]
        public string sourceNote;

        /// <summary>True only for a declared, finite, positive bandwidth.</summary>
        public bool HasLag
        {
            get
            {
                return provenance != MavEngineDataProvenance.Unavailable
                       && lagPerSec > 0f && !float.IsNaN(lagPerSec) && !float.IsInfinity(lagPerSec);
            }
        }

        public bool IsSelfConsistent(out string reason)
        {
            if (provenance == MavEngineDataProvenance.Unavailable && lagPerSec != 0f)
            {
                reason = "a non-zero lag with Unavailable provenance is a contradiction";
                return false;
            }

            if (provenance != MavEngineDataProvenance.Unavailable && !HasLag)
            {
                reason = "a declared lag must be finite and positive";
                return false;
            }

            reason = "OK";
            return true;
        }
    }

    /// <summary>First-order lags for the four F-15 surface channels. The default is no lag on any channel.</summary>
    [Serializable]
    public struct MavF15ActuatorDynamics
    {
        public MavF15ChannelLag symmetricStabilator;
        public MavF15ChannelLag differentialStabilator;
        public MavF15ChannelLag aileron;
        public MavF15ChannelLag rudder;

        public static MavF15ActuatorDynamics None
        {
            get { return new MavF15ActuatorDynamics(); }
        }

        public MavF15ChannelLag Get(MavF15SurfaceChannel channel)
        {
            switch (channel)
            {
                case MavF15SurfaceChannel.SymmetricStabilator: return symmetricStabilator;
                case MavF15SurfaceChannel.DifferentialStabilator: return differentialStabilator;
                case MavF15SurfaceChannel.Aileron: return aileron;
                default: return rudder;
            }
        }

        public bool AnyLag
        {
            get
            {
                for (int i = 0; i < 4; i++)
                {
                    if (Get((MavF15SurfaceChannel)i).HasLag)
                        return true;
                }

                return false;
            }
        }
    }

    /// <summary>
    /// The research model's own surface actuator lags, frozen exactly as printed.
    ///
    /// SOURCE. Davison, "Examination of Wing Rock for the F-15", AFIT/GAE/ENY/92M-01, 1992, DTIC ADA256613 (public
    /// release, distribution unlimited): Appendix B, bifurcation driver STATE12, subroutine FUNX, PDF p.97 (printed
    /// p.87), under "Revised 23 Aug 89 - Added differential equations governing control surfaces. Equations assume CAS
    /// off and Aileron-Rudder Interconnect off. Deflections are in degrees.":
    ///     F(9)  = 20.*(CDSTBD-DSTBD)        symmetric stabilator
    ///     F(10) = 28.*(CDRUDD-DRUDD)        rudder
    ///     F(11) = 20.*(CDAILD-DAILD)        aileron
    ///     F(12) = 20.*(.3*CDAILD-DDTD)      differential tail, 0.3 x the COMMANDED aileron, "acting through the
    ///                                       stabilator actuators"
    ///
    /// SCOPE. The same thesis lineage as the aerodynamic coefficients and mass/inertia this aircraft already flies,
    /// so it is version-matched to the model - but the thesis cites no hardware source for 20 / 28 / 20, so it is NOT
    /// production F-15 actuator data. No rate limit and no position limit accompany it in the source; none is added.
    /// The Maverick gameplay command envelopes stay the travel. The frozen research configuration never uses this.
    /// </summary>
    public static class MavF15ResearchModelActuatorLags
    {
        public const float SymmetricStabilatorPerSec = 20f;
        public const float RudderPerSec = 28f;
        public const float AileronPerSec = 20f;
        public const float DifferentialTailPerSec = 20f;

        public const string Label =
            "RESEARCH-MODEL ACTUATOR LAG (Davison 1992 STATE12) - first-order bandwidth of the research model lineage; "
            + "not production F-15 actuator data, not a rate limit";

        public const string Citation =
            "Davison, AFIT/GAE/ENY/92M-01, DTIC ADA256613, App. B STATE12 FUNX, PDF p.97 (printed 87): "
            + "F(9)=20.*(CDSTBD-DSTBD), F(10)=28.*(CDRUDD-DRUDD), F(11)=20.*(CDAILD-DAILD), F(12)=20.*(.3*CDAILD-DDTD); "
            + "CAS off, ARI off, degrees";

        public static MavF15ActuatorDynamics Create()
        {
            return new MavF15ActuatorDynamics
            {
                symmetricStabilator = Lag(SymmetricStabilatorPerSec, "F(9)"),
                differentialStabilator = Lag(DifferentialTailPerSec, "F(12), through the stabilator actuators"),
                aileron = Lag(AileronPerSec, "F(11)"),
                rudder = Lag(RudderPerSec, "F(10)")
            };
        }

        private static MavF15ChannelLag Lag(float perSec, string equation)
        {
            return new MavF15ChannelLag
            {
                lagPerSec = perSec,
                provenance = MavEngineDataProvenance.PublicReference,
                sourceNote = Label + "; " + equation + ", " + perSec + " 1/s; " + Citation
            };
        }
    }

    /// <summary>The pilot-controlled aircraft's physics revisions, and the lag step they share.</summary>
    public static class MavF15PilotPhysics
    {
        public static MavF15ActuatorDynamics ActuatorDynamicsFor(MavF15PilotPhysicsRevision revision)
        {
            return revision == MavF15PilotPhysicsRevision.R2SourceActuatorLags
                ? MavF15ResearchModelActuatorLags.Create()
                : MavF15ActuatorDynamics.None;
        }

        public static string Describe(MavF15PilotPhysicsRevision revision)
        {
            return revision == MavF15PilotPhysicsRevision.R2SourceActuatorLags
                ? "R2: research-model actuator lags stab 20 / aileron 20 / differential 20 / rudder 28 1/s (Davison 1992 STATE12)"
                : "R1: instantaneous surfaces (the V1/V2 record)";
        }

        /// <summary>
        /// One step of x' = lagPerSec * (target - x) with the target held over the step (zero-order hold), solved
        /// exactly: x(t+dt) = target + (x - target) * exp(-lagPerSec * dt). Exact for any dt, so it has no
        /// timestep stability limit and adds no integration error of its own; it never overshoots.
        ///
        /// Fail-safe: a non-finite current position snaps to the (already bounded, finite) target; a zero, negative
        /// or non-finite dt holds the current position, as the instant path does for dt = 0.
        /// </summary>
        public static float StepFirstOrderLag(float current, float target, float lagPerSec, float deltaTime)
        {
            if (float.IsNaN(current) || float.IsInfinity(current))
                return target;
            if (!(deltaTime > 0f) || float.IsInfinity(deltaTime))
                return current;
            float decay = Mathf.Exp(-lagPerSec * deltaTime);
            return target + (current - target) * decay;
        }
    }
}
