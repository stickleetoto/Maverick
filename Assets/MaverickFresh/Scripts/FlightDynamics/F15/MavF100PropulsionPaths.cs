using UnityEngine;

namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// Which of the two deliberately separate propulsion bodies of evidence a quantity belongs to.
    ///
    /// R5 ended with two things that look adjacent and must not be combined:
    ///
    ///   A. a complete, dimensionless F100-PW-100(3) RESEARCH CHARACTERISTIC, and
    ///   B. a NASA F-15B 836 TARGET whose engine identity is exact and which now holds one
    ///      approximate dimensional anchor.
    ///
    /// Multiplying A by a future anchor from B would produce dimensional thrust for the target
    /// aircraft. It would also be wrong unless the two engine builds are the same, which no source
    /// in hand establishes - and the result would carry the target's exact identity while being
    /// built from a different engine's characteristic.
    /// </summary>
    public enum MavF100PropulsionPath
    {
        /// <summary>
        /// Path A. The F100-PW-100 simulation lineage: TP-1034 figure 17's 63 normalized points,
        /// the printed cycle equations, the Y12 machine scale and the derived bound on the
        /// normalizer. Dimensionless throughout, gated to the seven documented source conditions,
        /// capped at <see cref="MavF100SourceClass.CompatibleSupport"/>.
        ///
        /// It does NOT include the prototype series 2 7/8 control schedules. Those are a separate
        /// engine family - see <see cref="MavF100EngineFamily"/>.
        /// </summary>
        ResearchCharacteristic = 0,

        /// <summary>
        /// Path B. NASA F-15B 836 target propulsion. Engine IDENTITY is exact-target and frozen.
        /// Only dimensional values directly supported by NASA-836 sources may live here: today
        /// that is one approximate SLS full-afterburner thrust figure - see
        /// <see cref="MavF100Nasa836TargetPropulsion"/>.
        /// </summary>
        Nasa836Target = 1
    }

    /// <summary>
    /// A dimensional propulsion value supported directly by a NASA-836 source.
    ///
    /// Carries everything needed to repeat the claim honestly: what the number is, which thrust
    /// quantity, at what condition, how precisely the source states it, who said it, which
    /// aircraft it describes, which engine family, and what grade the evidence is. A bare float
    /// would lose all of that at the first assignment, and an approximation that forgets it was
    /// approximate becomes a specification the next time somebody reads it.
    /// </summary>
    public struct MavF100Nasa836TargetAnchor
    {
        public string quantityName;
        public MavF100ThrustQuantity quantity;
        public float newtons;

        /// <summary>The same value in the units the source actually printed.</summary>
        public float sourceValue;
        public string sourceUnits;

        /// <summary>The operating condition it belongs to. A thrust without one means nothing.</summary>
        public string condition;

        /// <summary>How precisely the source states it. Approximate values stay approximate.</summary>
        public MavF100ValuePrecision precision;

        /// <summary>The NASA-836 source. Required non-empty; an anchor without one is not an anchor.</summary>
        public string citation;

        /// <summary>Which aircraft the source is describing.</summary>
        public string targetIdentity;

        public MavF100EngineFamily engineFamily;
        public MavF100SourceClass sourceClass;

        public bool IsUsable
        {
            get
            {
                return !string.IsNullOrEmpty(citation)
                    && !string.IsNullOrEmpty(condition)
                    && precision != MavF100ValuePrecision.Unspecified
                    && engineFamily != MavF100EngineFamily.Unspecified
                    && !float.IsNaN(newtons)
                    && !float.IsInfinity(newtons);
            }
        }

        public bool IsApproximate
        {
            get { return precision == MavF100ValuePrecision.Approximate; }
        }
    }

    /// <summary>
    /// PATH B: NASA F-15B 836 target propulsion.
    ///
    /// The engine identity is exact and was frozen in an earlier pass: two Pratt &amp; Whitney
    /// F100-PW-100 on the pre-Quiet-Spike NASA F-15B 836 configuration. That is the ONLY
    /// exact-target propulsion fact this project holds.
    ///
    /// <see cref="Anchors"/> is NO LONGER EMPTY. NASA's own F-15B reports publish an approximate
    /// sea-level-static full-afterburner thrust for this aircraft's engines, and that is a
    /// genuine exact-target-aircraft datum even though it is an approximation rather than a deck.
    ///
    /// The barrier around this path still stands, and matters more now than when the path was
    /// empty. With an anchor present, the obvious next move is to scale it by path A's 63-point
    /// characteristic and call the result NASA 836 thrust - a different engine build's shape
    /// wearing the target's exact identity, looking entirely sourced from every call site.
    /// <see cref="MavF100PathSeparation"/> is where that move is refused.
    /// </summary>
    public static class MavF100Nasa836TargetPropulsion
    {
        public const string AircraftConfiguration =
            "NASA_F15B_836_SN74_0141_PRE_QUIET_SPIKE_BASELINE_F100_PW_100";

        /// <summary>The first exact-target propulsion fact: which engine it is.</summary>
        public const MavF100SourceClass EngineIdentityClass =
            MavF100SourceClass.AuthoritativeExactTarget;

        /// <summary>
        /// Approximate uninstalled sea-level-static full-afterburner thrust per engine, in lbf.
        ///
        /// NASA/TM-2005-213670, "Local Flow Conditions for Propulsion Experiments on the NASA
        /// F-15B Propulsion Flight Test Fixture" (NTRS 20050241960, public), verified here:
        ///
        ///   "The F-15B airplane is powered by two Pratt &amp; Whitney (West Palm Beach, Florida)
        ///    F100-PW-100 turbofan engines that each produce an uninstalled, sea level static
        ///    thrust of approximately 23,500 lbf in full afterburner."
        ///
        /// APPROXIMATE. The source says "approximately", and it is a descriptive figure in an
        /// airplane-description section, not a calibrated engine deck. It must never be presented
        /// as one.
        /// </summary>
        public const float ApproximateSlsFullAbThrustLbf = 23500f;

        /// <summary>
        /// The same figure in newtons, 104 533 N. DERIVED here - TM-2005-213670 prints only lbf
        /// in that sentence.
        /// </summary>
        public const float ApproximateSlsFullAbThrustN =
            (float)(ApproximateSlsFullAbThrustLbf * MavF100SourceData.PoundForceToNewton);

        /// <summary>
        /// A second, conflicting NASA figure for the same aircraft, recorded rather than
        /// discarded.
        ///
        /// NASA/TM-2001-210395 (AIAA 2001-3303) and NASA/TM-2002-210736 both state: "Each engine
        /// has an uninstalled, sea-level static thrust rating of approximately 25,000 lbf
        /// (91,188 N)."
        ///
        /// That sentence is INTERNALLY INCONSISTENT: 25 000 lbf is 111 206 N, and 91 188 N is
        /// 20 500 lbf. The two halves of the parenthetical disagree by more than 20 percent, so
        /// neither number in it can be relied on. It is also a "rating" without a stated power
        /// setting, where TM-2005-213670 says "in full afterburner".
        ///
        /// The preferred anchor is therefore TM-2005-213670: internally consistent, and
        /// condition-specific. This conflict is recorded so nobody rediscovers the 25 000 lbf
        /// figure and assumes it was overlooked - and because 25 000 lbf happens to equal
        /// TP-1373's 111.2 kN axis scale, which would be a coincidence worth being suspicious of.
        /// </summary>
        public const string ConflictingThrustFigure =
            "NASA/TM-2001-210395 and NASA/TM-2002-210736 give 'approximately 25,000 lbf "
            + "(91,188 N)' for the same aircraft. That parenthetical is arithmetically wrong - "
            + "25,000 lbf is 111,206 N and 91,188 N is 20,500 lbf - so neither half is reliable, "
            + "and it states no power setting. NASA/TM-2005-213670's 23,500 lbf in full "
            + "afterburner is preferred: internally consistent and condition-specific.";

        public const string ThrustAnchorCitation =
            "NASA/TM-2005-213670 (H-2625), 'Local Flow Conditions for Propulsion Experiments on "
            + "the NASA F-15B Propulsion Flight Test Fixture', NTRS 20050241960, public. "
            + "Verified from the report. APPROXIMATE, airplane-description figure, not a deck.";

        /// <summary>
        /// Dimensional values directly supported by NASA-836 sources. A fresh array each call so
        /// no caller can populate it for everyone else.
        /// </summary>
        public static MavF100Nasa836TargetAnchor[] Anchors
        {
            get
            {
                MavF100Nasa836TargetAnchor sls = new MavF100Nasa836TargetAnchor();
                sls.quantityName = "uninstalled SLS full-afterburner thrust, per engine";
                sls.quantity = MavF100ThrustQuantity.UninstalledNetThrust;
                sls.newtons = ApproximateSlsFullAbThrustN;
                sls.sourceValue = ApproximateSlsFullAbThrustLbf;
                sls.sourceUnits = "lbf";
                sls.condition = "sea level, static (Mach 0), full afterburner";
                sls.precision = MavF100ValuePrecision.Approximate;
                sls.citation = ThrustAnchorCitation;
                sls.targetIdentity = AircraftConfiguration;
                sls.engineFamily = MavF100EngineFamily.Nasa836Target;
                sls.sourceClass = MavF100SourceClass.AuthoritativeExactTarget;

                return new MavF100Nasa836TargetAnchor[] { sls };
            }
        }

        public static bool HasDimensionalAnchor
        {
            get { return Anchors.Length > 0; }
        }

        /// <summary>
        /// Why one approximate anchor is not an engine model. Holding a single SLS number is a
        /// long way from a thrust deck: nothing here gives altitude or Mach lapse, part-power
        /// behaviour, airflow, fuel flow, spool dynamics or installation effects for this
        /// aircraft.
        /// </summary>
        public const string StillUnavailableReason =
            "One approximate sea-level-static point is not a thrust deck. No NASA-836 source here "
            + "gives altitude or Mach lapse, part-power behaviour, airflow, fuel flow, spool "
            + "dynamics or installation effects for 836's engines. The R5 pack supplies a "
            + "characteristic shape, but for OTHER engine builds - which is why it may not simply "
            + "be scaled by this anchor.";
    }

    /// <summary>
    /// Result of asking the two paths to be combined.
    /// </summary>
    public struct MavF100PathCombination
    {
        public bool permitted;
        public float newtons;
        public MavF100ThrustQuantity quantity;
        public MavF100SourceClass sourceClass;

        /// <summary>
        /// Precision of the product. An approximate anchor yields an approximate result, always -
        /// multiplying an approximation by an exact fraction does not sharpen it.
        /// </summary>
        public MavF100ValuePrecision precision;

        public string reason;

        public static MavF100PathCombination Refused(string reason)
        {
            MavF100PathCombination c = new MavF100PathCombination();
            c.permitted = false;
            c.newtons = 0f;
            c.quantity = MavF100ThrustQuantity.Unspecified;
            c.sourceClass = MavF100SourceClass.Unavailable;
            c.precision = MavF100ValuePrecision.Unspecified;
            c.reason = reason;
            return c;
        }
    }

    /// <summary>
    /// The one place path A and path B may meet, and the gate that stops them meeting by accident.
    ///
    /// The combination is arithmetically trivial - a fraction times a force - which is exactly the
    /// problem. Written as a bare multiplication somewhere in a call site it would be invisible.
    /// Written here it has to pass every check in <see cref="DimensionalizeForTarget"/>, and today
    /// it does not: path B now holds an approximate NASA-836 anchor, but no named source proves
    /// build equivalence between the PW-100(3) research build and 836's engines, and 836's
    /// installed sub-configuration is unknown. (An earlier revision of this comment said the gate
    /// failed because path B was empty; that stopped being true when the anchor was added.)
    ///
    /// This generalises the barrier already standing between the normalized net characteristic and
    /// <see cref="MavF100DimensionalGrossThrustDataset"/>. Same rule, one rung up: research
    /// characteristic and target anchors are different bodies of evidence, and being separately
    /// valid does not make them jointly valid.
    /// </summary>
    public static class MavF100PathSeparation
    {
        /// <summary>
        /// Which path a source class may serve. Exact-target evidence belongs to the target path;
        /// research and calibration evidence never does, whatever its grade.
        /// </summary>
        public static bool BelongsTo(MavF100SourceClass sourceClass, MavF100PropulsionPath path)
        {
            if (path == MavF100PropulsionPath.Nasa836Target)
                return sourceClass == MavF100SourceClass.AuthoritativeExactTarget;

            return sourceClass == MavF100SourceClass.CompatibleSupport
                || sourceClass == MavF100SourceClass.CrossValidationOnly;
        }

        /// <summary>
        /// Turns a path-A normalized fraction into newtons using a path-B dimensional anchor.
        ///
        /// Refuses unless all of these hold, checked in this order:
        ///   1. the normalized result actually carries a number;
        ///   2. the anchor is usable, and cites a NASA-836 source;
        ///   3. the anchor is the same thrust QUANTITY as the characteristic - a gross anchor
        ///      cannot scale a net characteristic;
        ///   4. the engine families may be combined under the supplied equivalence;
        ///   5. that equivalence covers SameGasPath and SameControlSchedule, proven by a named
        ///      source;
        ///   6. NASA 836's installed engine sub-configuration is known.
        ///
        /// Today <see cref="MavF100Nasa836TargetPropulsion.Anchors"/> holds an approximate anchor,
        /// so condition 2 can pass. No source proves the equivalence needed for 4 and 5, and 6 is
        /// false by record (<see cref="MavF100Nasa836EngineEvidence.SubConfigurationKnown"/>). An
        /// earlier revision of this comment said condition 2 could not be reached because the
        /// anchor list was empty; that is no longer the case.
        /// </summary>
        public static MavF100PathCombination DimensionalizeForTarget(
            MavF100NetThrustFractionResult researchCharacteristic,
            MavF100Nasa836TargetAnchor targetAnchor,
            MavF100ConfigurationEquivalence equivalence)
        {
            if (!researchCharacteristic.HasNumber)
            {
                return MavF100PathCombination.Refused(
                    "path A produced no number: " + researchCharacteristic.reason);
            }

            if (!targetAnchor.IsUsable)
            {
                return MavF100PathCombination.Refused(
                    "path B supplied no usable NASA-836 anchor. "
                    + MavF100Nasa836TargetPropulsion.StillUnavailableReason);
            }

            if (targetAnchor.quantity != researchCharacteristic.quantity)
            {
                return MavF100PathCombination.Refused(
                    "quantity mismatch: the research characteristic is "
                    + researchCharacteristic.quantity + " and the target anchor is "
                    + targetAnchor.quantity
                    + "; scaling one by the other would silently change what the number means");
            }

            // Engine-family separation, checked explicitly rather than implied by the
            // equivalence flag alone. The research characteristic is the PW-100 simulation
            // lineage; the anchor is the NASA 836 target. Different families.
            if (!MavF100EngineFamilies.MayCombine(
                    MavF100EngineFamily.Pw100SimulationLineage,
                    targetAnchor.engineFamily,
                    equivalence))
            {
                return MavF100PathCombination.Refused(
                    "engine families may not be combined: the characteristic is "
                    + MavF100EngineFamilies.Describe(MavF100EngineFamily.Pw100SimulationLineage)
                    + " and the anchor is "
                    + MavF100EngineFamilies.Describe(targetAnchor.engineFamily)
                    + ", and no named source proves build equivalence. Without it this product "
                    + "would be one engine's thrust shape wearing another's exact identity.");
            }

            if (!equivalence.Covers(
                    MavF100EngineFamilies.RequiredForCharacteristicTransfer))
            {
                return MavF100PathCombination.Refused(
                    "configuration equivalence between the "
                    + MavF100SourceData.NormalizedThrustEngineBuild
                    + " research build and the NASA 836 target build is not established for the "
                    + "quantity being transferred - "
                    + equivalence.DescribeShortfall(
                        MavF100EngineFamilies.RequiredForCharacteristicTransfer));
            }

            // Even with equivalence claimed, the target's own installed sub-configuration has to
            // be known, or there is nothing for the equivalence to be an equivalence TO.
            if (!MavF100Nasa836EngineEvidence.SubConfigurationKnown)
            {
                return MavF100PathCombination.Refused(
                    "NASA 836's installed engine sub-configuration is unknown. "
                    + MavF100Nasa836EngineEvidence.SubConfigurationSearchResult);
            }

            MavF100PathCombination result = new MavF100PathCombination();
            result.permitted = true;
            result.newtons = researchCharacteristic.netThrustFraction * targetAnchor.newtons;
            result.quantity = targetAnchor.quantity;

            // The product is never stronger than the research shape it came from. An exact-target
            // anchor does not promote a compatible-support characteristic; it only scales it.
            result.sourceClass = MavF100SourceClass.CompatibleSupport;

            // Nor sharper. An approximate anchor yields an approximate product.
            result.precision = targetAnchor.IsApproximate
                ? MavF100ValuePrecision.Approximate
                : targetAnchor.precision;

            result.reason = "path A characteristic (" + researchCharacteristic.panel
                + ") scaled by path B anchor '" + targetAnchor.quantityName
                + "' [" + targetAnchor.citation + "]; equivalence per: "
                + equivalence.provingSource;
            return result;
        }
    }
}
