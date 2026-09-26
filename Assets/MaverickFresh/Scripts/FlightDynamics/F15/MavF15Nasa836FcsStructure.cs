namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// How NASA F-15B 836's own control-system block diagrams bear on one element of the R3
    /// control-law architecture.
    ///
    /// This grades STRUCTURE only - whether a path exists, what feeds it, where it goes. The 836
    /// diagrams print no gain, schedule, limit or rate, so no grade here can make a stage run.
    /// </summary>
    public enum MavF15FcsStructureClass
    {
        /// <summary>
        /// Not drawn on the 836 simplified diagrams, and no F-15-family or research source in hand
        /// describes it either. The fail-closed default for anything not graded.
        /// </summary>
        NotShownFor836 = 0,

        /// <summary>Drawn on the 836 simplified diagrams: the path exists on this aircraft.</summary>
        Exact836StructureConfirmed = 1,

        /// <summary>
        /// Not drawn on the 836 diagrams, but described for the F-15 family by a source in hand.
        /// Absence from a SIMPLIFIED diagram is not proof of absence, so this is not a contradiction.
        /// </summary>
        F15FamilyStructureOnly = 2,

        /// <summary>Described only by the AFIT research lineage.</summary>
        AfitResearchOnly = 3,

        /// <summary>The 836 diagrams draw something incompatible with the R3 element.</summary>
        Contradicted = 4
    }

    /// <summary>
    /// How much of the diagram's drawing of a stage the R3 implementation represents. Separate
    /// from <see cref="MavF15FcsStructureClass"/>: a stage can exist on 836 and still be modelled
    /// by R3 as a simplified subset of what the diagram draws.
    /// </summary>
    public enum MavF15R3TopologyMatch
    {
        NotApplicable = 0,

        /// <summary>R3 carries the stage at the level of detail the diagram draws.</summary>
        Matches = 1,

        /// <summary>
        /// R3 has the stage's sensors and output surface, but the diagram draws further elements
        /// (filters, compensation, integrators, command paths, schedules) that R3 does not model.
        /// </summary>
        SimplifiedSubset = 2,

        /// <summary>R3 feeds or routes the stage differently from the diagram.</summary>
        RoutingDiffers = 3
    }

    /// <summary>
    /// One graded element of the control-law architecture against the 836 diagrams.
    /// </summary>
    public struct MavF15FcsStructureFact
    {
        /// <summary>False for the actuator path, which is not a <see cref="MavF15FcsStage"/>.</summary>
        public bool isStage;
        public MavF15FcsStage stage;
        public string element;
        public MavF15FcsStructureClass classification;
        public MavF15R3TopologyMatch r3Topology;

        /// <summary>What the 836 diagram draws for this element, in the diagram's own words.</summary>
        public string diagramElement;
        public string citation;

        /// <summary>Elements drawn for 836 that R3 does not model, or how R3 routes differently.</summary>
        public string r3Difference;

        public bool IsExact836Structure
        {
            get { return classification == MavF15FcsStructureClass.Exact836StructureConfirmed; }
        }
    }

    /// <summary>
    /// A Mach-number switch printed on an 836 block diagram: above the threshold the switch
    /// selects its "0" contact and the stage's path is open.
    ///
    /// It is STRUCTURAL LOGIC. It can only take a stage out; it never supplies a gain and it can
    /// never make an unavailable stage run.
    /// </summary>
    public struct MavF15FcsMachSwitch
    {
        public MavF15FcsStage stage;

        /// <summary>
        /// The printed threshold. The diagram reads "Mach &gt; 1.5" / "Mach &gt; 1.0", so the switch
        /// is open strictly above it and closed at exactly the threshold.
        /// </summary>
        public float switchedOutAboveMach;

        /// <summary>Which of the stage's inputs the diagram routes through the switch.</summary>
        public string switchedSignals;
        public string citation;

        /// <summary>
        /// True only when the switch and its label were read on the rendered source page, not
        /// from extracted text. A switch that is not verified is never applied.
        /// </summary>
        public bool verifiedOnRenderedPage;

        public bool IsSwitchedOut(float mach)
        {
            return verifiedOnRenderedPage && mach > switchedOutAboveMach;
        }
    }

    /// <summary>
    /// The EXACT NASA F-15B 836 control-system STRUCTURE, from the aircraft's own simplified
    /// pitch, roll and yaw control models.
    ///
    /// SOURCES (both public NASA documents, both read on rendered pages):
    ///   NASA/TM-2009-214651, McWherter, Moua, Gera, Cox, "Stability and Control Analysis of the
    ///     F-15B Quiet Spike Aircraft", Aug 2009: figs. 3-4 PDF p.30 / printed p.26 (art 090121,
    ///     090122); fig. 5 PDF p.31 / printed p.27 (art 090123).
    ///   NASA/TM-2012-215978, Moua, McWherter, Cox, Gera, "Flight Test Results on the Stability and
    ///     Control of the F-15 Quiet Spike Aircraft", 2012: figs. 3-4 PDF p.22 / printed p.18 (art
    ///     110195, 110196); fig. 5 PDF p.23 / printed p.19 (art 110197). Same diagrams, redrawn.
    ///
    /// Both reports caption them as the control model of "the NASA Dryden Flight Research Center
    /// F-15B test airplane" - 836. The reports concern its Quiet Spike configuration; neither
    /// reports any modification of the control system for the spike, so the structure is taken
    /// as the airplane's own.
    ///
    /// WHAT IS NOT HERE: every box on those diagrams is labelled "Gain", "Filter",
    /// "Compensation", "Stall inhibitor", "Roll ratio adjust device" and so on. No number, curve,
    /// limit or rate is printed. <see cref="AnyNumericFcsDataShown"/> is false and stays false;
    /// this class can gate stages OUT but can never supply a value that makes one run.
    ///
    /// Full cross-check: Docs/Reference/F15_836_FCS_STRUCTURE_V1.0.md.
    /// </summary>
    public static class MavF15Nasa836FcsStructure
    {
        public const string PitchDiagram =
            "NASA/TM-2009-214651 fig. 3 (PDF p.30, printed p.26, art 090121); repeated as "
            + "NASA/TM-2012-215978 fig. 3 (PDF p.22, printed p.18, art 110195)";

        public const string RollDiagram =
            "NASA/TM-2009-214651 fig. 4 (PDF p.30, printed p.26, art 090122); repeated as "
            + "NASA/TM-2012-215978 fig. 4 (PDF p.22, printed p.18, art 110196)";

        public const string YawDiagram =
            "NASA/TM-2009-214651 fig. 5 (PDF p.31, printed p.27, art 090123); repeated as "
            + "NASA/TM-2012-215978 fig. 5 (PDF p.23, printed p.19, art 110197)";

        /// <summary>The diagrams print no gain, schedule, limit or rate anywhere.</summary>
        public const bool AnyNumericFcsDataShown = false;

        /// <summary>
        /// Mechanical aileron-rudder interconnect: both multiplier inputs - the symmetric
        /// stabilator mechanical system through its gain and filter, and the lateral stick through
        /// its filter - pass through switches labelled "Mach &gt; 1.5" that select 0.
        /// </summary>
        public static readonly MavF15FcsMachSwitch AileronRudderInterconnectSwitch =
            new MavF15FcsMachSwitch
            {
                stage = MavF15FcsStage.AileronRudderInterconnect,
                switchedOutAboveMach = 1.5f,
                switchedSignals =
                    "both multiplier inputs: symmetric-stabilator mechanical system (gain, filter) "
                    + "and lateral stick deflection (filter)",
                citation = YawDiagram,
                verifiedOnRenderedPage = true
            };

        /// <summary>
        /// Roll-yaw crossfeed: the angle-of-attack input to the crossfeed multiplier passes through
        /// a switch labelled "Mach &gt; 1.0" that selects 0, so the product with roll rate is zero.
        /// </summary>
        public static readonly MavF15FcsMachSwitch RollYawCrossfeedSwitch =
            new MavF15FcsMachSwitch
            {
                stage = MavF15FcsStage.RollToYawCrossfeed,
                switchedOutAboveMach = 1.0f,
                switchedSignals = "angle-of-attack input to the angle-of-attack x roll-rate multiplier",
                citation = YawDiagram,
                verifiedOnRenderedPage = true
            };

        private static readonly MavF15FcsStructureFact[] StageFacts = BuildStageFacts();

        /// <summary>
        /// The actuator path, graded separately because it is not a control-law stage.
        /// </summary>
        public static readonly MavF15FcsStructureFact ActuatorPath = new MavF15FcsStructureFact
        {
            isStage = false,
            element = "actuator path",
            classification = MavF15FcsStructureClass.Exact836StructureConfirmed,
            r3Topology = MavF15R3TopologyMatch.SimplifiedSubset,
            diagramElement =
                "four surface outputs: symmetric stabilator position (boost actuator, power "
                + "cylinder), aileron deflection (power cylinder), differential tail deflection "
                + "(CAS servo summed with the mechanical roll path into a power cylinder), rudder "
                + "deflection (power cylinder); CAS servos and a pitch CAS interconnect servo",
            citation = PitchDiagram + "; " + RollDiagram + "; " + YawDiagram,
            r3Difference =
                "R3's four actuator channels are exactly the diagram's four surface outputs. R3 "
                + "models no boost actuator, power cylinder or servo dynamics, and the diagrams "
                + "print none; travel and rate stay unavailable"
        };

        public static MavF15FcsStructureFact Get(MavF15FcsStage stage)
        {
            int i = (int)stage;
            if (i >= 0 && i < StageFacts.Length && StageFacts[i].isStage)
                return StageFacts[i];

            return new MavF15FcsStructureFact
            {
                isStage = true,
                stage = stage,
                element = stage.ToString(),
                classification = MavF15FcsStructureClass.NotShownFor836,
                r3Topology = MavF15R3TopologyMatch.NotApplicable,
                diagramElement = "not graded",
                citation = "",
                r3Difference = ""
            };
        }

        public static bool TryGetMachSwitch(MavF15FcsStage stage, out MavF15FcsMachSwitch machSwitch)
        {
            if (stage == MavF15FcsStage.AileronRudderInterconnect)
            {
                machSwitch = AileronRudderInterconnectSwitch;
                return true;
            }

            if (stage == MavF15FcsStage.RollToYawCrossfeed)
            {
                machSwitch = RollYawCrossfeedSwitch;
                return true;
            }

            machSwitch = default(MavF15FcsMachSwitch);
            return false;
        }

        /// <summary>
        /// The exact-structure switch gate for one stage at one Mach number.
        ///
        /// True when the 836 diagram switches the stage out at this Mach. Also true when the
        /// stage HAS a switch and the Mach number is not finite: the switch position is then
        /// unknown, and an unknown switch is treated as open. False for a stage with no switch.
        ///
        /// This only ever removes a stage. Nothing here can make a stage run.
        /// </summary>
        public static bool IsSwitchedOutAt(MavF15FcsStage stage, float mach, out string reason)
        {
            MavF15FcsMachSwitch machSwitch;
            if (!TryGetMachSwitch(stage, out machSwitch) || !machSwitch.verifiedOnRenderedPage)
            {
                reason = "";
                return false;
            }

            if (float.IsNaN(mach) || float.IsInfinity(mach))
            {
                reason = "Mach unknown, so the exact 836 Mach > "
                         + machSwitch.switchedOutAboveMach.ToString("F1")
                         + " switch position is undetermined; stage held out";
                return true;
            }

            if (machSwitch.IsSwitchedOut(mach))
            {
                reason = "switched out: exact 836 structure opens this path above Mach "
                         + machSwitch.switchedOutAboveMach.ToString("F1") + " (Mach "
                         + mach.ToString("F3") + ")";
                return true;
            }

            reason = "";
            return false;
        }

        private static MavF15FcsStructureFact[] BuildStageFacts()
        {
            MavF15FcsStructureFact[] facts =
                new MavF15FcsStructureFact[(int)MavF15FcsStage.RollToYawCrossfeed + 1];

            facts[(int)MavF15FcsStage.MechanicalPath] = Fact(
                MavF15FcsStage.MechanicalPath,
                MavF15FcsStructureClass.Exact836StructureConfirmed,
                MavF15R3TopologyMatch.SimplifiedSubset,
                "pitch stick -> (x PRAD) -> boost actuator -> power cylinder -> symmetric "
                + "stabilator; lateral stick -> (x RRAD) -> gain -> power cylinder -> aileron, and "
                + "the same product into the differential-tail power cylinder; pedal -> gain -> "
                + "mechanical system -> power cylinder -> rudder",
                PitchDiagram + "; " + RollDiagram + "; " + YawDiagram,
                "the diagram's mechanical pitch path also carries a normal-acceleration gain into "
                + "a mechanical integrator ahead of the boost actuator; R3 has only the stick "
                + "gearing");

            facts[(int)MavF15FcsStage.PitchRatioChanger] = Fact(
                MavF15FcsStage.PitchRatioChanger,
                MavF15FcsStructureClass.Exact836StructureConfirmed,
                MavF15R3TopologyMatch.Matches,
                "pitch ratio adjust device fed by Pt/Ps and Pt - Ps, multiplying pitch stick "
                + "deflection inside the mechanical system, upstream of the CAS stick-gradient branch",
                PitchDiagram,
                "R3 scales mechanical pitch demand only, as drawn; the device's two air-data inputs "
                + "are not modelled because no schedule exists");

            facts[(int)MavF15FcsStage.RollRatioChanger] = Fact(
                MavF15FcsStage.RollRatioChanger,
                MavF15FcsStructureClass.Exact836StructureConfirmed,
                MavF15R3TopologyMatch.Matches,
                "roll ratio adjust device fed by the symmetric stabilator mechanical system and "
                + "calibrated airspeed, multiplying lateral stick; the product drives BOTH the "
                + "aileron path and the differential-tail path",
                RollDiagram,
                "R3 scales both roll effectors, as drawn; the device's inputs are not modelled "
                + "because no schedule exists");

            facts[(int)MavF15FcsStage.PitchCas] = Fact(
                MavF15FcsStage.PitchCas,
                MavF15FcsStructureClass.Exact836StructureConfirmed,
                MavF15R3TopologyMatch.SimplifiedSubset,
                "pitch rate through a washout filter plus normal acceleration, gear-switched "
                + "compensation, summed with a stick-gradient/prefilter command, structural "
                + "filter plus integrator, CAS servo and CAS interconnect servo",
                PitchDiagram,
                "R3 has proportional pitch-rate and load-factor feedback only: no stick command "
                + "path, washout, gear switch, compensation, structural filter, integrator or "
                + "interconnect servo");

            facts[(int)MavF15FcsStage.RollCas] = Fact(
                MavF15FcsStage.RollCas,
                MavF15FcsStructureClass.Exact836StructureConfirmed,
                MavF15R3TopologyMatch.SimplifiedSubset,
                "roll rate x gain summed with a stick-gradient/filter command, through a limiter "
                + "scheduled on angle of attack and calibrated airspeed, CAS servo, onto the "
                + "differential tail",
                RollDiagram,
                "R3 feeds roll rate to the differential stabilator, as drawn; it has no stick "
                + "command path and no AoA/airspeed-scheduled limiter");

            facts[(int)MavF15FcsStage.YawCas] = Fact(
                MavF15FcsStage.YawCas,
                MavF15FcsStructureClass.Exact836StructureConfirmed,
                MavF15R3TopologyMatch.SimplifiedSubset,
                "yaw rate through a structural filter and compensation, plus lateral acceleration "
                + "(sensor location) through gain and structural filter, proportional plus "
                + "integral, summed with the pedal command and the crossfeed, CAS servo",
                YawDiagram,
                "R3 has proportional yaw-rate feedback only: no lateral-acceleration feedback, "
                + "pedal command path, proportional-plus-integral or filters");

            facts[(int)MavF15FcsStage.AileronRudderInterconnect] = Fact(
                MavF15FcsStage.AileronRudderInterconnect,
                MavF15FcsStructureClass.Exact836StructureConfirmed,
                MavF15R3TopologyMatch.SimplifiedSubset,
                "MECHANICAL aileron-rudder interconnect inside the mechanical system: (symmetric "
                + "stabilator mechanical system -> gain -> filter) x (lateral stick deflection -> "
                + "filter) into the rudder power cylinder; both inputs switched to 0 above Mach 1.5",
                YawDiagram,
                "R3 feeds the ARI from roll COMMAND and does not gate it on yaw CAS, both as drawn; "
                + "the lateral-stick input is taken ahead of the roll ratio changer, which settles "
                + "R3's open RRAD-ordering question for 836. R3 uses a constant gain where the "
                + "diagram multiplies by a stabilator-dependent signal, and has no filters");

            facts[(int)MavF15FcsStage.HighAoaRollDamperWashout] = Fact(
                MavF15FcsStage.HighAoaRollDamperWashout,
                MavF15FcsStructureClass.F15FamilyStructureOnly,
                MavF15R3TopologyMatch.NotApplicable,
                "no gain washout is drawn; the roll CAS's angle-of-attack dependence is drawn as "
                + "a limiter scheduled on angle of attack and calibrated airspeed",
                RollDiagram,
                "R3 scales roll-rate feedback authority with AoA (Davison fig. 27, a public "
                + "reproduction of a production-F-15 schedule). Not contradicted: the diagram is "
                + "simplified and does draw an AoA-scheduled roll-CAS element, but not this one");

            facts[(int)MavF15FcsStage.StallInhibitor] = Fact(
                MavF15FcsStage.StallInhibitor,
                MavF15FcsStructureClass.Exact836StructureConfirmed,
                MavF15R3TopologyMatch.RoutingDiffers,
                "stall inhibitor fed by angle of attack AND pitch rate, summed into the pitch CAS "
                + "ahead of the structural filter, so it acts through the CAS servo",
                PitchDiagram,
                "R3 drives the stall inhibitor from angle of attack alone and adds it to the "
                + "stabilator independently of pitch-CAS engagement; the diagram feeds it pitch "
                + "rate too and routes it through the pitch CAS. No numeric consequence today: "
                + "the stage has no sourced threshold or gradient");

            facts[(int)MavF15FcsStage.TurnCoordination] = Fact(
                MavF15FcsStage.TurnCoordination,
                MavF15FcsStructureClass.NotShownFor836,
                MavF15R3TopologyMatch.NotApplicable,
                "no turn-coordination element is drawn; coordination on the yaw axis is drawn as "
                + "lateral-acceleration feedback in the yaw CAS",
                YawDiagram,
                "R3's g.tan(phi)/V yaw-rate-error term appears on no 836 diagram and in no family "
                + "or research source in hand");

            facts[(int)MavF15FcsStage.RollToYawCrossfeed] = Fact(
                MavF15FcsStage.RollToYawCrossfeed,
                MavF15FcsStructureClass.Exact836StructureConfirmed,
                MavF15R3TopologyMatch.SimplifiedSubset,
                "roll-yaw crossfeed: angle of attack x roll rate, compensation, into the yaw CAS "
                + "summing junction; the angle-of-attack input is switched to 0 above Mach 1.0",
                YawDiagram,
                "R3 feeds roll rate into the rudder through the yaw CAS, as drawn; it uses a "
                + "constant gain where the diagram multiplies by angle of attack, and has no "
                + "compensation");

            return facts;
        }

        private static MavF15FcsStructureFact Fact(
            MavF15FcsStage stage,
            MavF15FcsStructureClass classification,
            MavF15R3TopologyMatch topology,
            string diagramElement,
            string citation,
            string r3Difference)
        {
            return new MavF15FcsStructureFact
            {
                isStage = true,
                stage = stage,
                element = stage.ToString(),
                classification = classification,
                r3Topology = topology,
                diagramElement = diagramElement,
                citation = citation,
                r3Difference = r3Difference
            };
        }
    }
}
