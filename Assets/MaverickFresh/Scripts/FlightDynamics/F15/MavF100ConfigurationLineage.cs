using System;
using UnityEngine;

namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// The separate ways two engine configurations can be "the same". Flags, not a boolean,
    /// because they are independent and the difference decides what may be transferred.
    ///
    /// The case that forced this: engine P680063 after its 1980 rebuild carries the F100(3)
    /// DESIGNATION and was explicitly modified to represent the production F100(3) GAS PATH - and
    /// simultaneously ran a DEEC, which NASA TM-84908 says "replaces the functions of the
    /// supervisory electronic engine control and hydromechanical unified fuel control on the
    /// standard F100 engine". Same designation, same gas path, categorically different control.
    /// One boolean cannot say that.
    /// </summary>
    [Flags]
    public enum MavF100EquivalenceDimension
    {
        None = 0,

        /// <summary>The two carry the same model designation. The weakest claim, and the easiest to over-read.</summary>
        SameDesignation = 1,

        /// <summary>Same compressor, combustor, turbine and flowpath hardware.</summary>
        SameGasPath = 2,

        /// <summary>
        /// Same control system and schedules - fuel, nozzle area, vane scheduling against PLA.
        /// This is what makes a thrust-versus-power-lever curve transferable; the gas path sets
        /// what the engine CAN do, the control sets what it does at a given lever angle.
        /// </summary>
        SameControlSchedule = 4,

        /// <summary>
        /// The same performance deck predicts both. The strongest claim, and the only one that
        /// would make a characteristic directly transferable without further argument.
        /// </summary>
        SamePerformanceDeck = 8
    }

    /// <summary>
    /// One configuration phase in an engine's life: what it was, when, what changed, and who says so.
    ///
    /// A serial number is not a configuration. P680063 wore the same serial from 1972 to 1994 and
    /// was four materially different engines in that time, ending more than 27 000 lbf.
    /// </summary>
    public struct MavF100ConfigurationPhase
    {
        public string engineSerial;
        public string designation;

        /// <summary>Year the phase began, as the source states it.</summary>
        public int fromYear;

        public string phaseName;
        public string hardwareAndControlChanges;
        public string source;
        public MavF100SourceClass provenance;
        public MavF100EngineFamily engineFamily;

        /// <summary>
        /// What this phase's measurements may be treated as equivalent TO, relative to the
        /// production F100-PW-100(3). Set only where a source supports it.
        /// </summary>
        public MavF100EquivalenceDimension equivalenceToProductionF100Dash3;
    }

    /// <summary>
    /// Configuration chronologies for the engines the R5 sources actually measured.
    ///
    /// WHY THIS EXISTS
    /// ---------------
    /// The R5 material names two engine serials over and over - P680059 and P680063 - and it is
    /// tempting to treat "P680063 data" as one dataset. It is not. NASA's own retrospective
    /// (Burcham, Conners and Maxwell, NTRS 19990064011) states that P680063 "has flown in four
    /// major configurations", and the performance changed enormously across them: the altitude
    /// calibration this project relies on was taken in 1977 on the 2-7/8 build, while the same
    /// serial in 1993 was an EMD engine producing "more than 27,000 lb of thrust".
    ///
    /// **Configuration date travels with every performance datum.** A serial number staying the
    /// same does not make measurements from different configuration eras interchangeable.
    /// </summary>
    public static class MavF100ConfigurationLineage
    {
        public const string BurchamCitation =
            "Burcham, Conners and Maxwell, 'Flight Research Using F100 Engine P680063 in the "
            + "NASA F-15 Airplane', NTRS 19990064011, public. Retrieved and read.";

        public const string Tm84908Citation =
            "NASA TM-84908, 'Airstart performance of a digital electronic engine control system "
            + "in an F-15 airplane', NTRS 19830013932, public. Retrieved and read.";

        /// <summary>
        /// The four major configurations of P680063, plus the phases between them.
        ///
        /// Fresh array each call: a lineage somebody can edit in place is not a record.
        /// </summary>
        public static MavF100ConfigurationPhase[] Engine063Lineage
        {
            get
            {
                return new MavF100ConfigurationPhase[]
                {
                    Phase("P680063", "F100-PW-100(2)", 1972, "as manufactured",
                        "Built as an F100-PW-100(2) ('mod 2'), serial range P680050-P680084. "
                        + "Flown in the USAF Combined Test Force F-15 evaluation.",
                        BurchamCitation + " Printed p. 2.",
                        MavF100EngineFamily.PrototypeSeries2And7Eighths,
                        MavF100EquivalenceDimension.None),

                    Phase("P680063", "F100-PW-100(2-7/8)", 1974, "rebuilt to 2-7/8",
                        "One of four engines rebuilt in 1974 with the improved fan (bulged inner "
                        + "diameter) and control system; these were called 'F100 two-and-seven-"
                        + "eighths'.",
                        BurchamCitation + " Printed p. 2.",
                        MavF100EngineFamily.PrototypeSeries2And7Eighths,
                        MavF100EquivalenceDimension.None),

                    Phase("P680063", "F100-PW-100(2-7/8)", 1977, "altitude calibration",
                        "Calibrated with P680059 at the NASA Lewis Propulsion System Laboratory. "
                        + "THIS is the configuration behind TP-1069, TP-1228, TP-1373 and "
                        + "TP-1782 - not any later one.",
                        BurchamCitation + " Printed p. 3.",
                        MavF100EngineFamily.PrototypeSeries2And7Eighths,
                        MavF100EquivalenceDimension.None),

                    Phase("P680063", "F100-PW-100(3) w/ DEEC", 1980, "rebuilt to F100(3) gas path",
                        "Pratt & Whitney modified the engine to represent the gas path of the "
                        + "production F100(3): compressor 7th- and 8th-stage disk and blades and "
                        + "13th-stage disk, combustor, fuel nozzles, turbine 1st- and 2nd-stage "
                        + "disks and 3rd- and 4th-stage disk and blades, and the nozzle divergent "
                        + "actuator. Fitted with DEEC hardware and software and new 'partial "
                        + "swirl' augmentor hardware.",
                        BurchamCitation + " Printed p. 4. " + Tm84908Citation
                        + " Printed p. 4: 'It had been updated to an F100(3) production engine "
                        + "configuration prior to the DEEC installation.'",
                        MavF100EngineFamily.Pw100SimulationLineage,
                        MavF100EquivalenceDimension.SameDesignation
                        | MavF100EquivalenceDimension.SameGasPath),

                    Phase("P680063", "F100 EMD", 1985, "fourth configuration",
                        "Updated to the F100 engine model derivative configuration: EMD fan, "
                        + "single-crystal turbine blades and vanes, 16-segment augmentor.",
                        BurchamCitation + " Printed p. 7.",
                        MavF100EngineFamily.Unspecified,
                        MavF100EquivalenceDimension.None),

                    Phase("P680063", "F100 EMD, overhauled", 1990, "increased-life core",
                        "Overhauled before the performance-seeking-control flights with a new "
                        + "increased-life core; 'represented an engine with little deterioration "
                        + "and better-than-average performance'. By the 1993 PCA programme the "
                        + "paper describes 'more than 27,000 lb of thrust'.",
                        BurchamCitation + " Printed pp. 8, 9.",
                        MavF100EngineFamily.Unspecified,
                        MavF100EquivalenceDimension.None)
                };
            }
        }

        /// <summary>
        /// The thrust figure that must never become an F100(3) baseline.
        ///
        /// "The big brute force of engine P680063, with more than 27,000 lb of thrust" belongs to
        /// the 1993 propulsion-controlled-aircraft programme, by which time the engine was in its
        /// EMD configuration with an overhauled increased-life core. It is evidence about an EMD
        /// engine in 1993 and about nothing else - certainly not about the production F100(3)
        /// characteristic that TP-1034 figure 17 describes.
        /// </summary>
        public const float Engine063EmdEraThrustLbf = 27000f;

        public const string Engine063EmdEraThrustWarning =
            "'More than 27,000 lb of thrust' (Burcham et al., printed p. 9) is the 1993 PCA-era "
            + "P680063: F100 EMD configuration, EMD fan, single-crystal turbine blades and vanes, "
            + "16-segment augmentor, overhauled increased-life core. NOT evidence for the "
            + "production F100(3) characteristic and never a baseline PW-100(3) anchor.";

        /// <summary>
        /// Looks up the phase in force for an engine in a given year. Returns false when the year
        /// precedes the first recorded phase - there is no "closest match" here, because guessing
        /// a configuration is the error this whole type exists to prevent.
        /// </summary>
        public static bool TryGetPhase(
            MavF100ConfigurationPhase[] lineage, int year, out MavF100ConfigurationPhase phase)
        {
            phase = new MavF100ConfigurationPhase();

            if (lineage == null || lineage.Length == 0)
                return false;

            bool found = false;
            for (int i = 0; i < lineage.Length; i++)
            {
                if (lineage[i].fromYear <= year)
                {
                    phase = lineage[i];
                    found = true;
                }
            }

            return found;
        }

        private static MavF100ConfigurationPhase Phase(
            string serial, string designation, int fromYear, string phaseName,
            string changes, string source, MavF100EngineFamily family,
            MavF100EquivalenceDimension equivalence)
        {
            MavF100ConfigurationPhase p = new MavF100ConfigurationPhase();
            p.engineSerial = serial;
            p.designation = designation;
            p.fromYear = fromYear;
            p.phaseName = phaseName;
            p.hardwareAndControlChanges = changes;
            p.source = source;
            p.provenance = MavF100SourceClass.CompatibleSupport;
            p.engineFamily = family;
            p.equivalenceToProductionF100Dash3 = equivalence;
            return p;
        }
    }

    /// <summary>
    /// What is publicly known about the engines installed on NASA F-15B 836 itself, and what is
    /// not.
    ///
    /// The headline finding of the bridge pass is a negative one, and it is worth stating plainly:
    /// **no public source has been located that names the sub-configuration of the F100-PW-100
    /// engines installed on tail 836.** Whether they were (1), (2), 2-7/8 or production (3) build
    /// is not established by anything found. Aircraft production year does not settle it, and
    /// inferring from it is expressly not done here.
    ///
    /// The search covered NTRS for tail 836, USAF serial 74-0141, F-15B PFTF and Quiet Spike
    /// aircraft descriptions, the F-15B capability briefings, and the engine-configuration
    /// vocabulary (BOM, production configuration, UFC/EEC, nozzle configuration, engine change).
    /// The F-15B reports describe the airframe and quote a thrust figure; none describes the
    /// engine build.
    /// </summary>
    public static class MavF100Nasa836EngineEvidence
    {
        /// <summary>
        /// Tail 836 is USAF 74-0141, obtained by NASA in 1993 from the Hawaii Air National Guard.
        /// NASA "F-15B 836 Supersonic Research Testbed Capabilities" (NTRS 20160006705).
        /// </summary>
        public const string TailNumberIdentity =
            "NASA F-15B tail 836 = USAF 74-0141, obtained 1993 from the Hawaii ANG. "
            + "NTRS 20160006705, 'F-15B 836 Supersonic Research Testbed Capabilities', public.";

        /// <summary>
        /// The link from the Propulsion Flight Test Fixture reports to tail 836, which
        /// NASA/TM-2005-213670 does NOT itself make.
        /// </summary>
        public const string PftfToTailLink =
            "NTRS 20100001729, 'Analysis of a Channeled Centerbody Supersonic Inlet for F-15B "
            + "Flight Research' (2010), public: the Propulsion Flight Test Fixture is 'available "
            + "for use on the NASA F-15B airplane, tail number 836', and that airplane 'is "
            + "powered by two Pratt & Whitney F100-PW-100 afterburning turbofan engines'.";

        /// <summary>
        /// Year NASA 836 was re-engined, after which its thrust figures describe a DIFFERENT
        /// ENGINE MODEL entirely.
        ///
        /// NTRS 20160006705: "Two F100-PW-220E engines - upgraded in 2014 - 24,000 lb thrust
        /// class - Digital engine control".
        ///
        /// This matters for more than tidiness. A "24,000 lb" figure quoted for tail 836 may
        /// describe the PW-220E rather than the PW-100, and the two are different engines. Any
        /// 836 thrust figure must carry its epoch.
        /// </summary>
        public const int ReEngineToPw220EYear = 2014;

        public const string ReEngineNote =
            "NTRS 20160006705: NASA 836 was upgraded to two F100-PW-220E engines in 2014, "
            + "'24,000 lb thrust class'. Thrust figures for 836 from 2014 onward describe the "
            + "PW-220E, NOT the F100-PW-100 of the frozen target configuration. The target epoch "
            + "is the pre-Quiet-Spike F100-PW-100 baseline, so 2014-and-later figures are out of "
            + "scope for it.";

        /// <summary>
        /// The negative result. False, and expected to stay false until a source turns up.
        /// </summary>
        public static bool SubConfigurationKnown
        {
            get { return false; }
        }

        public const string SubConfigurationSearchResult =
            "NO PUBLIC SOURCE LOCATED connecting NASA 836's installed F100-PW-100 engines to the "
            + "production F100(3) build, or to any other sub-configuration. Searched NTRS for "
            + "tail 836, USAF 74-0141, F-15B PFTF and Quiet Spike aircraft descriptions, the "
            + "F-15B capability briefings, and engine-configuration vocabulary. The F-15B reports "
            + "describe the airframe and quote a thrust figure; none describes the engine build. "
            + "Production year is not evidence of engine sub-configuration and was not used as "
            + "such.";
    }
}
