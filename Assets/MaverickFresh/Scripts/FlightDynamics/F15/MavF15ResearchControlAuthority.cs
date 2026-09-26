namespace MaverickFresh.FlightDynamics.F15
{
    // Three things that are easy to conflate, kept as three separate types:
    //
    //   A) MavF15ResearchDemonstratedSurfaceRange - deflections the AFIT research SOURCE
    //      demonstrably commanded in its own tabulated solutions. A statement about what the
    //      research model was exercised at. NOT a physical limit of any aircraft.
    //   B) MavF15PhysicalSurfaceHardStops - where the surface physically stops. Exact NASA 836:
    //      unavailable, so zero travel. Research: not adopted, because the public sets conflict.
    //   C) MavF15ActuatorRateLimits - how fast the surface can move. Unavailable everywhere.
    //
    // No conversion exists from A to B or C, or from A to any actuator limit or surface state.
    // Full audit: Docs/Reference/F15_RESEARCH_CONTROL_AUTHORITY_V1.0.md.

    /// <summary>
    /// The deflections of ONE channel that the AFIT research source demonstrably commanded.
    ///
    /// A research-model statement only: "the source evaluated its model at these commanded
    /// values and tabulated the result". It says nothing about where the real surface stops,
    /// how fast it moves, or at what flight condition the values are meaningful.
    /// </summary>
    public struct MavF15ResearchDemonstratedSurfaceRange
    {
        public MavF15SurfaceChannel channel;

        /// <summary>False: nothing demonstrated for this channel, so no value is admitted.</summary>
        public bool declared;

        public float minDeg;
        public float maxDeg;

        /// <summary>What the range is built from, and what it excludes.</summary>
        public string basis;
        public string citation;

        /// <summary>
        /// Always false. This type describes what a research source commanded; it is never a
        /// physical control limit, and the name of the type says so.
        /// </summary>
        public bool IsPhysicalLimit
        {
            get { return false; }
        }

        /// <summary>
        /// True when the value is inside the demonstrated range (inclusive). An undeclared channel
        /// admits nothing, not even zero.
        /// </summary>
        public bool Contains(float deg)
        {
            if (!declared || float.IsNaN(deg) || float.IsInfinity(deg))
                return false;

            return deg >= minDeg && deg <= maxDeg;
        }

        public static MavF15ResearchDemonstratedSurfaceRange Undeclared(
            MavF15SurfaceChannel channel, string why)
        {
            return new MavF15ResearchDemonstratedSurfaceRange
            {
                channel = channel,
                declared = false,
                minDeg = 0f,
                maxDeg = 0f,
                basis = why,
                citation = ""
            };
        }
    }

    /// <summary>
    /// ResearchDemonstratedControlRange for all four channels of the AFIT/Baumann/Davison research
    /// model. See <see cref="MavF15ResearchDemonstratedSurfaceRange"/> for what it is not.
    /// </summary>
    public struct MavF15ResearchDemonstratedControlRange
    {
        /// <summary>
        /// The concept's name. Deliberately not "PhysicalControlLimit": a research source's
        /// tabulated inputs are not the aircraft's hard stops.
        /// </summary>
        public const string Kind = "ResearchDemonstratedControlRange";

        public string configurationId;
        public MavF15ResearchDemonstratedSurfaceRange symmetricStabilator;
        public MavF15ResearchDemonstratedSurfaceRange differentialStabilator;
        public MavF15ResearchDemonstratedSurfaceRange aileron;
        public MavF15ResearchDemonstratedSurfaceRange rudder;

        public MavF15ResearchDemonstratedSurfaceRange Get(MavF15SurfaceChannel channel)
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
        /// The range Baumann's thesis (DTIC ADA217366) demonstrably exercises in its TABULATED
        /// equilibrium solutions, for the research configuration only.
        ///
        /// Symmetric stabilator: -25 to -5 deg. The extremes are the fixed stabilator settings of
        /// Table III (-25 deg, flat spin, PDF p.74) and Table V (-5 deg, flat spin, PDF p.82);
        /// Table VII's 201 continuation points (PDF pp.124-128) lie between them, -17.30722 to
        /// -5.275754 deg. Plot-only continuations are not counted: Baumann fig. 5-1 also sweeps at
        /// -29 deg, and the appendix C sweeps run far past any physical limit, which Davison
        /// (DTIC ADA256613 PDF p.77) says the bifurcation analysis does on purpose.
        ///
        /// Aileron, differential tail and rudder: every Table VII point holds them at exactly
        /// 0 deg, so each is declared as the degenerate range [0, 0]. Baumann's flat-spin and
        /// rudder-sweep tables (III-V, VIII) do tabulate nonzero rudder; that range is recorded in
        /// the audit document and not declared here, because no current use needs it.
        /// </summary>
        public static MavF15ResearchDemonstratedControlRange AfitBaumannTabulatedEquilibria()
        {
            const string cite =
                "Baumann, AFIT/GAE/ENY/89D-01, DTIC ADA217366: Table III (PDF p.74), Table V "
                + "(PDF p.82), Table VII (PDF pp.124-133)";
            const string neutralOnly =
                "tabulated at exactly 0 deg in every Table VII equilibrium; declared as [0, 0]";

            return new MavF15ResearchDemonstratedControlRange
            {
                configurationId = MavF15AfitResearchIdentity.ConfigurationId,
                symmetricStabilator = new MavF15ResearchDemonstratedSurfaceRange
                {
                    channel = MavF15SurfaceChannel.SymmetricStabilator,
                    declared = true,
                    minDeg = -25f,
                    maxDeg = -5f,
                    basis =
                        "envelope of TABULATED equilibrium inputs: Table III -25 deg, Table V -5 deg, "
                        + "Table VII -17.30722..-5.275754 deg. Plot-only sweeps excluded. The "
                        + "tabulated states lie at V = 218.5-699.7 ft/s, so this is not a statement "
                        + "about the M 0.6 source condition",
                    citation = cite
                },
                differentialStabilator = Neutral(MavF15SurfaceChannel.DifferentialStabilator, neutralOnly, cite),
                aileron = Neutral(MavF15SurfaceChannel.Aileron, neutralOnly, cite),
                rudder = Neutral(MavF15SurfaceChannel.Rudder, neutralOnly, cite)
            };
        }

        private static MavF15ResearchDemonstratedSurfaceRange Neutral(
            MavF15SurfaceChannel channel, string basis, string cite)
        {
            return new MavF15ResearchDemonstratedSurfaceRange
            {
                channel = channel,
                declared = true,
                minDeg = 0f,
                maxDeg = 0f,
                basis = basis,
                citation = cite
            };
        }
    }

    /// <summary>One channel's physical travel stop, with where the numbers came from.</summary>
    public struct MavF15HardStop
    {
        public float minDeg;
        public float maxDeg;
        public MavEngineDataProvenance provenance;
        public string sourceNote;

        public bool Declared
        {
            get { return provenance != MavEngineDataProvenance.Unavailable && maxDeg > minDeg; }
        }
    }

    /// <summary>
    /// Where each surface PHYSICALLY stops.
    ///
    /// This is the travel half of the actuator's <see cref="MavF15SurfaceLimits"/>, seen on its
    /// own so it cannot be confused with a research source's commanded range or with a rate
    /// limit. It can only be built from actuator limits; nothing research-scoped converts into it.
    /// </summary>
    public struct MavF15PhysicalSurfaceHardStops
    {
        public MavF15HardStop symmetricStabilator;
        public MavF15HardStop differentialStabilator;
        public MavF15HardStop aileron;
        public MavF15HardStop rudder;

        public MavF15HardStop Get(MavF15SurfaceChannel channel)
        {
            switch (channel)
            {
                case MavF15SurfaceChannel.SymmetricStabilator: return symmetricStabilator;
                case MavF15SurfaceChannel.DifferentialStabilator: return differentialStabilator;
                case MavF15SurfaceChannel.Aileron: return aileron;
                default: return rudder;
            }
        }

        public bool AnyDeclared
        {
            get
            {
                return symmetricStabilator.Declared || differentialStabilator.Declared
                    || aileron.Declared || rudder.Declared;
            }
        }

        public static MavF15PhysicalSurfaceHardStops FromActuatorLimits(MavF15SurfaceLimits limits)
        {
            return new MavF15PhysicalSurfaceHardStops
            {
                symmetricStabilator = Stop(limits.symmetricStabilator),
                differentialStabilator = Stop(limits.differentialStabilator),
                aileron = Stop(limits.aileron),
                rudder = Stop(limits.rudder)
            };
        }

        /// <summary>
        /// Exact NASA 836: unavailable. Zero travel on every channel, from the same fail-closed
        /// default the actuator uses.
        /// </summary>
        public static MavF15PhysicalSurfaceHardStops ExactTarget()
        {
            return FromActuatorLimits(MavF15SurfaceLimits.UnavailableExactTarget());
        }

        private static MavF15HardStop Stop(MavF15SurfaceChannelLimits c)
        {
            return new MavF15HardStop
            {
                minDeg = c.minDeg,
                maxDeg = c.maxDeg,
                provenance = c.travelProvenance,
                sourceNote = c.sourceNote
            };
        }
    }

    /// <summary>One channel's actuator rate limit, with where the number came from.</summary>
    public struct MavF15RateLimit
    {
        public float degPerSec;
        public MavEngineDataProvenance provenance;
        public string sourceNote;

        public bool Declared
        {
            get { return provenance != MavEngineDataProvenance.Unavailable && degPerSec > 0f; }
        }
    }

    /// <summary>
    /// How fast each surface can move - the rate half of the actuator's
    /// <see cref="MavF15SurfaceLimits"/>, on its own.
    ///
    /// Unavailable for the exact target and for the research configuration. The research
    /// source's first-order actuator lags (Davison PDF p.97) are a bandwidth, not a rate limit,
    /// and are not wired - see the audit document.
    /// </summary>
    public struct MavF15ActuatorRateLimits
    {
        public MavF15RateLimit symmetricStabilator;
        public MavF15RateLimit differentialStabilator;
        public MavF15RateLimit aileron;
        public MavF15RateLimit rudder;

        public MavF15RateLimit Get(MavF15SurfaceChannel channel)
        {
            switch (channel)
            {
                case MavF15SurfaceChannel.SymmetricStabilator: return symmetricStabilator;
                case MavF15SurfaceChannel.DifferentialStabilator: return differentialStabilator;
                case MavF15SurfaceChannel.Aileron: return aileron;
                default: return rudder;
            }
        }

        public bool AnyDeclared
        {
            get
            {
                return symmetricStabilator.Declared || differentialStabilator.Declared
                    || aileron.Declared || rudder.Declared;
            }
        }

        public static MavF15ActuatorRateLimits FromActuatorLimits(MavF15SurfaceLimits limits)
        {
            return new MavF15ActuatorRateLimits
            {
                symmetricStabilator = Rate(limits.symmetricStabilator),
                differentialStabilator = Rate(limits.differentialStabilator),
                aileron = Rate(limits.aileron),
                rudder = Rate(limits.rudder)
            };
        }

        public static MavF15ActuatorRateLimits ExactTarget()
        {
            return FromActuatorLimits(MavF15SurfaceLimits.UnavailableExactTarget());
        }

        private static MavF15RateLimit Rate(MavF15SurfaceChannelLimits c)
        {
            return new MavF15RateLimit
            {
                degPerSec = c.rateLimitDegSec,
                provenance = c.rateProvenance,
                sourceNote = c.sourceNote
            };
        }
    }

    /// <summary>
    /// The research model's surface sign convention, as the source states it.
    /// </summary>
    public static class MavF15ResearchControlConventions
    {
        /// <summary>
        /// Baumann's own statement (DTIC ADA217366 PDF p.86, printed p.71): "Horizontal stabilator
        /// surfaces are positively deflected when the leading edges are up (trailing edges are
        /// therefore down.)" Positive stabilator is therefore nose-down, and the transcribed
        /// routine's -0.42629958 * DSTBR term in CMM1 carries the same sign.
        /// </summary>
        public const string SymmetricStabilatorPositive =
            "leading edge up / trailing edge down (nose-down pitching moment)";

        /// <summary>Same page: "Rudders have positive deflection when the trailing edge is to the left".</summary>
        public const string RudderPositive = "trailing edge left";

        /// <summary>Same page: positive when "the right aileron's trailing edge is below the chord line".</summary>
        public const string AileronPositive = "right aileron trailing edge down";

        public const string Citation =
            "Baumann, AFIT/GAE/ENY/89D-01, DTIC ADA217366, Appendix A, PDF p.86 (printed p.71)";
    }

    /// <summary>
    /// A research-model control setting held ONLY to evaluate a published static equilibrium.
    ///
    /// STATIC_EQUILIBRIUM_VALIDATION_ONLY. It is not a surface command, not a surface position,
    /// and not a trim: it cannot become a <see cref="MavF15SurfaceState"/>, a requested or actual
    /// surface state, an actuator limit or a hard stop. It exists so a coefficient-level check can
    /// put the research source's own tabulated inputs into the research coefficient routine
    /// without giving the flying research aircraft any surface travel.
    ///
    /// It admits only values inside the <see cref="MavF15ResearchDemonstratedControlRange"/> it is
    /// created against.
    /// </summary>
    public struct MavF15ResearchStaticControlState
    {
        public const string Label = "STATIC_EQUILIBRIUM_VALIDATION_ONLY";

        public float symmetricStabilatorDeg;
        public float differentialTailDeg;
        public float aileronDeg;
        public float rudderDeg;

        /// <summary>The published equilibrium these inputs reproduce.</summary>
        public string sourceNote;

        public static bool TryCreate(
            MavF15ResearchDemonstratedControlRange range,
            float symmetricStabilatorDeg,
            float differentialTailDeg,
            float aileronDeg,
            float rudderDeg,
            string sourceNote,
            out MavF15ResearchStaticControlState state,
            out string reason)
        {
            state = default(MavF15ResearchStaticControlState);

            if (range.configurationId != MavF15AfitResearchIdentity.ConfigurationId)
            {
                reason = "the demonstrated range does not belong to the research configuration";
                return false;
            }

            if (!Admit(range.symmetricStabilator, symmetricStabilatorDeg, out reason)
                || !Admit(range.differentialStabilator, differentialTailDeg, out reason)
                || !Admit(range.aileron, aileronDeg, out reason)
                || !Admit(range.rudder, rudderDeg, out reason))
            {
                return false;
            }

            state = new MavF15ResearchStaticControlState
            {
                symmetricStabilatorDeg = symmetricStabilatorDeg,
                differentialTailDeg = differentialTailDeg,
                aileronDeg = aileronDeg,
                rudderDeg = rudderDeg,
                sourceNote = sourceNote
            };
            reason = Label;
            return true;
        }

        /// <summary>
        /// The arguments the research coefficient routine takes. The one conversion this type
        /// offers, and it leads only into the research routine.
        /// </summary>
        public MavF15BaumannSurfaceState ToBaumannSurfaceStateForStaticEvaluation()
        {
            return new MavF15BaumannSurfaceState
            {
                symmetricStabilatorDeg = symmetricStabilatorDeg,
                aileronDeg = aileronDeg,
                differentialTailDeg = differentialTailDeg,
                rudderDeg = rudderDeg
            };
        }

        private static bool Admit(
            MavF15ResearchDemonstratedSurfaceRange range, float deg, out string reason)
        {
            if (range.Contains(deg))
            {
                reason = "OK";
                return true;
            }

            reason = range.declared
                ? range.channel + " " + deg.ToString("F3") + " deg is outside the research "
                  + "demonstrated range " + range.minDeg.ToString("F3") + ".."
                  + range.maxDeg.ToString("F3") + " deg"
                : range.channel + " has no research demonstrated range";
            return false;
        }
    }
}
