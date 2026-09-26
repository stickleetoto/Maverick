using UnityEngine;

namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// Which engine build a piece of F100 evidence describes.
    ///
    /// The R5 sources are not one engine. They are three, and an earlier revision of this work
    /// described the research path as "F100-PW-100(3), prototype series 2 7/8 schedules" - which
    /// silently married two builds whose differences the sources themselves spell out. This enum
    /// exists so that marriage cannot be performed by accident again.
    /// </summary>
    public enum MavF100EngineFamily
    {
        /// <summary>Not stated. Never valid for data that is actually used.</summary>
        Unspecified = 0,

        /// <summary>
        /// The Pratt &amp; Whitney simulation lineage: F100-PW-100(1) in TM X-3261 and
        /// F100-PW-100(3) in TP-1034 and TP-1056, all patterned after P&amp;W digital decks
        /// (CCD 1015, CCD 1103-1.0).
        ///
        /// Carries the figure 17 normalized characteristic, the printed cycle equations, the Y12
        /// machine scale and the derived bound on the normalizer.
        ///
        /// NOTE the sub-distinction inside this family: TM X-3261 is the **(1)** build and
        /// TP-1034/TP-1056 the **(3)**. TP-1034 printed p. 2 says the (3) "features improved fan
        /// performance over the earlier F100-PW-100(1) version" and regenerated every component
        /// map, so (1) and (3) are not identical either. Items carry their own build string.
        /// </summary>
        Pw100SimulationLineage = 1,

        /// <summary>
        /// The prototype series 2 7/8 test engines P680059 and P680063: TP-1373, TP-1782, and
        /// TP-1069 / TP-1228 when supplied.
        ///
        /// Carries the altitude calibrations, the prototype control schedules and nozzle facts,
        /// and the F-15 flight cross-validation.
        ///
        /// TP-1373 printed p. 4 states these engines have series 2 cores, a series 3 fan,
        /// "control schedule differences from both the series 2 and 3 engines", and series 2
        /// actuated divergent nozzles where series 3 engines have free-floating ones.
        /// </summary>
        PrototypeSeries2And7Eighths = 2,

        /// <summary>
        /// The exact target: two F100-PW-100 as installed on NASA F-15B tail number 836.
        /// Only NASA-836 sources may populate this family.
        /// </summary>
        Nasa836Target = 3
    }

    /// <summary>
    /// How precisely a value is stated by its source. An approximation that forgets it was
    /// approximate becomes a specification the next time somebody reads it.
    /// </summary>
    public enum MavF100ValuePrecision
    {
        /// <summary>Not stated. Treat as unusable.</summary>
        Unspecified = 0,

        /// <summary>
        /// The source qualifies the value - "approximately", "about", a rating rather than a
        /// measurement. It may not be repeated without the qualifier.
        /// </summary>
        Approximate = 1,

        /// <summary>The source states the value without qualification, at stated conditions.</summary>
        Stated = 2,

        /// <summary>A calibrated measurement with a stated uncertainty. Nothing here is this.</summary>
        CalibratedMeasurement = 3
    }

    /// <summary>
    /// Rules about which engine families may be combined, and the scope of family-specific results.
    ///
    /// The separation is not a ranking. Prototype 2 7/8 calibration data is excellent data; it is
    /// simply data about a different engine than the one TP-1034's characteristic describes, which
    /// is in turn a different engine from the one bolted to NASA 836. Being separately valid does
    /// not make them jointly valid.
    /// </summary>
    public static class MavF100EngineFamilies
    {
        /// <summary>
        /// Whether evidence from two families may be combined into one derived number.
        ///
        /// Same family: yes. Different families: only with a named source proving build
        /// equivalence. No source in this repository proves any cross-family equivalence.
        /// </summary>
        public static bool MayCombine(
            MavF100EngineFamily a,
            MavF100EngineFamily b,
            MavF100ConfigurationEquivalence equivalence)
        {
            return MayCombine(a, b, equivalence, RequiredForCharacteristicTransfer);
        }

        /// <summary>
        /// The equivalence dimensions needed before a thrust-versus-power-lever characteristic may
        /// be carried from one configuration to another.
        ///
        /// Gas path AND control schedule. The gas path sets what the engine can do; the control
        /// sets what it actually does at a given lever angle, which is precisely what figure 17
        /// plots. Designation alone is not enough, and the P680063 lineage is the proof: NASA
        /// TM-84908 records it "updated to an F100(3) production engine configuration" while
        /// running a DEEC that replaced the production control entirely.
        ///
        /// SamePerformanceDeck is not required, only because requiring it would be requiring the
        /// answer. Where it IS proven, nothing further need be argued.
        /// </summary>
        public const MavF100EquivalenceDimension RequiredForCharacteristicTransfer =
            MavF100EquivalenceDimension.SameGasPath
            | MavF100EquivalenceDimension.SameControlSchedule;

        public static bool MayCombine(
            MavF100EngineFamily a,
            MavF100EngineFamily b,
            MavF100ConfigurationEquivalence equivalence,
            MavF100EquivalenceDimension required)
        {
            if (a == MavF100EngineFamily.Unspecified || b == MavF100EngineFamily.Unspecified)
                return false;

            if (a == b)
                return true;

            return equivalence.Covers(required);
        }

        /// <summary>
        /// Whether the derived upper bound on the figure 17 normalizer applies to a family.
        ///
        /// Only to <see cref="MavF100EngineFamily.Pw100SimulationLineage"/>. The bound comes from
        /// a scaled-fraction ceiling inside one simulation, applied to that simulation's own
        /// normalizing constant. Carrying it across to NASA 836 would assert that the target
        /// aircraft's engines cannot exceed a limit derived from a different engine's analog
        /// computer scaling - which is not a physical statement about anything.
        ///
        /// The arithmetic makes the point by itself: NASA reports put 836's engines at about
        /// 23 500 lbf, which is ABOVE the roughly 22 422 lbf bound. Applied across families the
        /// bound would "prove" a published NASA figure impossible.
        /// </summary>
        public static bool BoundAppliesTo(MavF100EngineFamily family)
        {
            return family == MavF100EngineFamily.Pw100SimulationLineage;
        }

        public static string Describe(MavF100EngineFamily family)
        {
            switch (family)
            {
                case MavF100EngineFamily.Pw100SimulationLineage:
                    return "F100-PW-100 simulation lineage ((1) TM X-3261, (3) TP-1034/TP-1056)";
                case MavF100EngineFamily.PrototypeSeries2And7Eighths:
                    return "F100 prototype series 2 7/8 (P680059, P680063)";
                case MavF100EngineFamily.Nasa836Target:
                    return "F100-PW-100 as installed on NASA F-15B 836";
                default:
                    return "unspecified engine family";
            }
        }
    }
}
