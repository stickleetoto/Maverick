using UnityEngine;

namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// Which of the two deliberately separate propulsion bodies of evidence a quantity belongs to.
    ///
    /// R5 ended with two things that look adjacent and must not be combined:
    ///
    ///   A. a complete, dimensionless F100-PW-100(3) RESEARCH CHARACTERISTIC, and
    ///   B. a NASA F-15B 836 TARGET whose engine identity is exact and which has no dimensional
    ///      performance values at all.
    ///
    /// Multiplying A by a future anchor from B would produce dimensional thrust for the target
    /// aircraft. It would also be wrong unless the two engine builds are the same, which no source
    /// in hand establishes - and the result would carry the target's exact identity while being
    /// built from a different engine's characteristic.
    /// </summary>
    public enum MavF100PropulsionPath
    {
        /// <summary>
        /// Path A. The F100-PW-100(3) research characteristic: TP-1034 figure 17's 63 normalized
        /// points, the printed equations, the prototype control schedules. Dimensionless
        /// throughout, gated to the seven documented source conditions, capped at
        /// <see cref="MavF100SourceClass.CompatibleSupport"/>.
        /// </summary>
        ResearchCharacteristic = 0,

        /// <summary>
        /// Path B. NASA F-15B 836 target propulsion. Engine IDENTITY is exact-target and frozen.
        /// Only dimensional values directly supported by NASA-836 sources may live here, and today
        /// there are none - see <see cref="MavF100Nasa836TargetPropulsion"/>.
        /// </summary>
        Nasa836Target = 1
    }

    /// <summary>
    /// A dimensional propulsion value supported directly by a NASA-836 source. Path B only.
    /// </summary>
    public struct MavF100Nasa836TargetAnchor
    {
        public string quantityName;
        public MavF100ThrustQuantity quantity;
        public float newtons;

        /// <summary>The NASA-836 source. Required non-empty; an anchor without one is not an anchor.</summary>
        public string citation;

        public bool IsUsable
        {
            get
            {
                return !string.IsNullOrEmpty(citation)
                    && !float.IsNaN(newtons)
                    && !float.IsInfinity(newtons);
            }
        }
    }

    /// <summary>
    /// PATH B: NASA F-15B 836 target propulsion.
    ///
    /// The engine identity is exact and was frozen in an earlier pass: two Pratt &amp; Whitney
    /// F100-PW-100 on the pre-Quiet-Spike NASA F-15B 836 configuration. That is the ONLY
    /// exact-target propulsion fact this project holds.
    ///
    /// <see cref="Anchors"/> is empty. Not "small", not "approximate" - empty. No NASA-836 source
    /// in this repository publishes a dimensional thrust, airflow, fuel flow or spool time
    /// constant for that aircraft's engines.
    ///
    /// The barrier around this path is built now precisely BECAUSE it is empty. The moment a first
    /// anchor arrives, the obvious next move is to scale it by path A's 63-point characteristic
    /// and call the result NASA 836 thrust. That would be a different engine build's shape wearing
    /// the target's exact identity, and it would look entirely sourced from every call site.
    /// <see cref="MavF100PathSeparation"/> is where that move is refused.
    /// </summary>
    public static class MavF100Nasa836TargetPropulsion
    {
        public const string AircraftConfiguration =
            "NASA_F15B_836_SN74_0141_PRE_QUIET_SPIKE_BASELINE_F100_PW_100";

        /// <summary>The one exact-target propulsion fact: which engine it is.</summary>
        public const MavF100SourceClass EngineIdentityClass =
            MavF100SourceClass.AuthoritativeExactTarget;

        /// <summary>
        /// Dimensional values directly supported by NASA-836 sources. Empty, and a fresh array
        /// each call so no caller can populate it for everyone else.
        /// </summary>
        public static MavF100Nasa836TargetAnchor[] Anchors
        {
            get { return new MavF100Nasa836TargetAnchor[0]; }
        }

        public static bool HasDimensionalAnchor
        {
            get { return Anchors.Length > 0; }
        }

        public const string UnavailableReason =
            "No NASA-836 source in this repository publishes a dimensional thrust, airflow, fuel "
            + "flow or spool time constant for the F100-PW-100 as installed on F-15B 836. The R5 "
            + "pack is F100-family research and calibration material on other engine builds; it "
            + "establishes what this engine family's thrust characteristic looks like, not how "
            + "large it is on this aircraft.";
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
        public string reason;

        public static MavF100PathCombination Refused(string reason)
        {
            MavF100PathCombination c = new MavF100PathCombination();
            c.permitted = false;
            c.newtons = 0f;
            c.quantity = MavF100ThrustQuantity.Unspecified;
            c.sourceClass = MavF100SourceClass.Unavailable;
            c.reason = reason;
            return c;
        }
    }

    /// <summary>
    /// The one place path A and path B may meet, and the gate that stops them meeting by accident.
    ///
    /// The combination is arithmetically trivial - a fraction times a force - which is exactly the
    /// problem. Written as a bare multiplication somewhere in a call site it would be invisible.
    /// Written here it has to pass four conditions, and it currently passes none of them, because
    /// path B is empty.
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
        /// Refuses unless all four hold:
        ///   1. the normalized result actually carries a number;
        ///   2. the anchor is usable, and cites a NASA-836 source;
        ///   3. the anchor is the same thrust QUANTITY as the characteristic - a gross anchor
        ///      cannot scale a net characteristic;
        ///   4. configuration equivalence between the research build and the target build is
        ///      proven by a named source.
        ///
        /// Today nothing reaches condition 2, because
        /// <see cref="MavF100Nasa836TargetPropulsion.Anchors"/> is empty.
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
                    + MavF100Nasa836TargetPropulsion.UnavailableReason);
            }

            if (targetAnchor.quantity != researchCharacteristic.quantity)
            {
                return MavF100PathCombination.Refused(
                    "quantity mismatch: the research characteristic is "
                    + researchCharacteristic.quantity + " and the target anchor is "
                    + targetAnchor.quantity
                    + "; scaling one by the other would silently change what the number means");
            }

            if (!equivalence.IsUsable)
            {
                return MavF100PathCombination.Refused(
                    "configuration equivalence between the "
                    + MavF100SourceData.NormalizedThrustEngineBuild
                    + " research build and the NASA 836 target build is not proven by a named "
                    + "source. Without it this product would be one engine's thrust shape "
                    + "wearing another's exact identity.");
            }

            MavF100PathCombination result = new MavF100PathCombination();
            result.permitted = true;
            result.newtons = researchCharacteristic.netThrustFraction * targetAnchor.newtons;
            result.quantity = targetAnchor.quantity;

            // The product is never stronger than the research shape it came from. An exact-target
            // anchor does not promote a compatible-support characteristic; it only scales it.
            result.sourceClass = MavF100SourceClass.CompatibleSupport;

            result.reason = "path A characteristic (" + researchCharacteristic.panel
                + ") scaled by path B anchor '" + targetAnchor.quantityName
                + "' [" + targetAnchor.citation + "]; equivalence per: "
                + equivalence.provingSource;
            return result;
        }
    }
}
