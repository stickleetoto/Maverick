using System.Collections.Generic;
using System.Text;
using UnityEngine;
using MaverickFresh.FlightDynamics.F15;

namespace MaverickFresh.FlightDynamics.Validation
{
    /// <summary>
    /// WP-2 checks for the EXACT NASA 836 control-system STRUCTURE:
    ///
    ///   [S1] every R3 stage and the actuator path is graded against 836's block diagrams
    ///   [S2] the Mach 1.5 ARI switch is stored, and only because it was verified on the page
    ///   [S3] the Mach 1.0 roll-yaw crossfeed switch is stored, likewise
    ///   [S4] no other stage has a Mach switch
    ///   [S5] switch facts alone never activate an unavailable gain
    ///   [S6] the switches gate the exact law exactly as drawn, and only the exact law
    ///   [S7] the exact 836 FCS stays neutral with its gains unavailable, at every Mach
    ///
    /// [S6] needs a stage that COULD run, so it uses obviously synthetic gains, explicitly marked,
    /// the same way MavF15ControlPathValidation does. No F-15 number is asserted anywhere here.
    /// </summary>
    public static class MavF15Nasa836FcsStructureValidation
    {
        private const float Tolerance = 1e-5f;

        public static string RunAll(out int passed, out int failed)
        {
            passed = 0;
            failed = 0;
            StringBuilder report = new StringBuilder(8192);
            report.AppendLine("F-15 NASA 836 FCS Structure Validation");
            report.AppendLine("======================================");
            report.AppendLine("Structure only. Synthetic gains in [S6] only. No F-15 gain is asserted.");

            ValidateCatalogue(report, ref passed, ref failed);
            ValidateAriSwitch(report, ref passed, ref failed);
            ValidateCrossfeedSwitch(report, ref passed, ref failed);
            ValidateNoOtherSwitches(report, ref passed, ref failed);
            ValidateSwitchesNeverActivate(report, ref passed, ref failed);
            ValidateSwitchGating(report, ref passed, ref failed);
            ValidateExactNeutral(report, ref passed, ref failed);

            report.AppendLine();
            report.Append("RESULT: ")
                .Append(failed == 0 ? "PASS" : "FAIL")
                .Append("  passed=").Append(passed)
                .Append(" failed=").Append(failed);
            return report.ToString();
        }

        // ---------------------------------------------------------------- [S1]

        private static void ValidateCatalogue(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[S1] Every R3 stage graded against the 836 diagrams");

            bool allGraded = true;
            bool allCited = true;
            for (int i = 0; i <= (int)MavF15FcsStage.RollToYawCrossfeed; i++)
            {
                MavF15FcsStructureFact f = MavF15Nasa836FcsStructure.Get((MavF15FcsStage)i);
                if (!f.isStage || f.stage != (MavF15FcsStage)i || f.diagramElement == "not graded")
                    allGraded = false;
                if (string.IsNullOrEmpty(f.citation) || f.citation.IndexOf("TM-2009-214651") < 0
                    || f.citation.IndexOf("TM-2012-215978") < 0 || string.IsNullOrEmpty(f.r3Difference))
                    allCited = false;
            }

            Record(allGraded, "all 11 stages carry a grade from the catalogue", report, ref passed, ref failed);
            Record(allCited,
                "every grade cites both reports' diagrams and states how R3 differs",
                report, ref passed, ref failed);

            MavF15FcsStage[] confirmed =
            {
                MavF15FcsStage.MechanicalPath, MavF15FcsStage.PitchRatioChanger,
                MavF15FcsStage.RollRatioChanger, MavF15FcsStage.PitchCas, MavF15FcsStage.RollCas,
                MavF15FcsStage.YawCas, MavF15FcsStage.AileronRudderInterconnect,
                MavF15FcsStage.StallInhibitor, MavF15FcsStage.RollToYawCrossfeed
            };
            bool confirmedOk = true;
            for (int i = 0; i < confirmed.Length; i++)
            {
                if (!MavF15Nasa836FcsStructure.Get(confirmed[i]).IsExact836Structure)
                    confirmedOk = false;
            }

            Record(confirmedOk,
                "nine stages are drawn on 836's diagrams: mechanical path, PRAD, RRAD, pitch/roll/yaw "
                + "CAS, ARI, stall inhibitor, roll-yaw crossfeed",
                report, ref passed, ref failed);

            Record(MavF15Nasa836FcsStructure.Get(MavF15FcsStage.HighAoaRollDamperWashout).classification
                       == MavF15FcsStructureClass.F15FamilyStructureOnly
                   && MavF15Nasa836FcsStructure.Get(MavF15FcsStage.TurnCoordination).classification
                       == MavF15FcsStructureClass.NotShownFor836,
                "the roll-damper washout is family-only and turn coordination is not shown for 836",
                report, ref passed, ref failed);

            bool noneContradicted = true;
            for (int i = 0; i <= (int)MavF15FcsStage.RollToYawCrossfeed; i++)
            {
                if (MavF15Nasa836FcsStructure.Get((MavF15FcsStage)i).classification
                    == MavF15FcsStructureClass.Contradicted)
                    noneContradicted = false;
            }

            Record(noneContradicted
                   && MavF15Nasa836FcsStructure.Get(MavF15FcsStage.StallInhibitor).r3Topology
                      == MavF15R3TopologyMatch.RoutingDiffers,
                "no stage is contradicted; the stall inhibitor's R3 ROUTING differs (it bypasses the "
                + "pitch CAS the diagram routes it through) and is recorded as such",
                report, ref passed, ref failed);

            MavF15FcsStructureFact act = MavF15Nasa836FcsStructure.ActuatorPath;
            Record(!act.isStage && act.IsExact836Structure
                   && act.r3Topology == MavF15R3TopologyMatch.SimplifiedSubset,
                "the actuator path is graded separately: four surface outputs confirmed, dynamics "
                + "not modelled",
                report, ref passed, ref failed);

            Record(!MavF15Nasa836FcsStructure.AnyNumericFcsDataShown,
                "the diagrams print no gain, schedule, limit or rate",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [S2] [S3] [S4]

        private static void ValidateAriSwitch(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[S2] Exact Mach 1.5 ARI switch - stored only because verified");

            MavF15FcsMachSwitch sw;
            bool found = MavF15Nasa836FcsStructure.TryGetMachSwitch(
                MavF15FcsStage.AileronRudderInterconnect, out sw);

            Record(found && sw.stage == MavF15FcsStage.AileronRudderInterconnect
                   && sw.switchedOutAboveMach == 1.5f && sw.verifiedOnRenderedPage
                   && sw.citation.IndexOf("fig. 5") >= 0 && sw.citation.IndexOf("TM-2009-214651") >= 0
                   && sw.citation.IndexOf("TM-2012-215978") >= 0,
                "ARI switch: Mach 1.5, verified on the rendered page, cites fig. 5 of both reports",
                report, ref passed, ref failed);

            Record(!sw.IsSwitchedOut(1.4f) && !sw.IsSwitchedOut(1.5f) && sw.IsSwitchedOut(1.5001f)
                   && sw.IsSwitchedOut(2.0f),
                "the diagram prints \"Mach > 1.5\": closed at 1.4 and at exactly 1.5, open above",
                report, ref passed, ref failed);

            MavF15FcsMachSwitch unverified = sw;
            unverified.verifiedOnRenderedPage = false;
            Record(!unverified.IsSwitchedOut(2.0f),
                "a switch that is not verified on the page is never applied",
                report, ref passed, ref failed);
        }

        private static void ValidateCrossfeedSwitch(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[S3] Exact Mach 1.0 roll-yaw crossfeed switch - stored only because verified");

            MavF15FcsMachSwitch sw;
            bool found = MavF15Nasa836FcsStructure.TryGetMachSwitch(
                MavF15FcsStage.RollToYawCrossfeed, out sw);

            Record(found && sw.stage == MavF15FcsStage.RollToYawCrossfeed
                   && sw.switchedOutAboveMach == 1.0f && sw.verifiedOnRenderedPage
                   && sw.citation.IndexOf("fig. 5") >= 0
                   && sw.switchedSignals.IndexOf("angle-of-attack") >= 0,
                "crossfeed switch: Mach 1.0 on the angle-of-attack input, verified, cites fig. 5",
                report, ref passed, ref failed);

            Record(!sw.IsSwitchedOut(0.95f) && !sw.IsSwitchedOut(1.0f) && sw.IsSwitchedOut(1.0001f),
                "\"Mach > 1.0\": closed at 0.95 and at exactly 1.0, open above",
                report, ref passed, ref failed);
        }

        private static void ValidateNoOtherSwitches(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[S4] No other stage has a Mach switch");

            bool onlyTwo = true;
            for (int i = 0; i <= (int)MavF15FcsStage.RollToYawCrossfeed; i++)
            {
                MavF15FcsStage stage = (MavF15FcsStage)i;
                MavF15FcsMachSwitch sw;
                bool has = MavF15Nasa836FcsStructure.TryGetMachSwitch(stage, out sw);
                bool expected = stage == MavF15FcsStage.AileronRudderInterconnect
                                || stage == MavF15FcsStage.RollToYawCrossfeed;
                string reason;
                if (has != expected
                    || (!expected && MavF15Nasa836FcsStructure.IsSwitchedOutAt(stage, 1.9f, out reason)))
                    onlyTwo = false;
            }

            Record(onlyTwo,
                "exactly two switches exist, and no other stage is ever switched out",
                report, ref passed, ref failed);

            string r;
            Record(MavF15Nasa836FcsStructure.IsSwitchedOutAt(
                       MavF15FcsStage.AileronRudderInterconnect, float.NaN, out r)
                   && MavF15Nasa836FcsStructure.IsSwitchedOutAt(
                       MavF15FcsStage.RollToYawCrossfeed, float.PositiveInfinity, out r),
                "an unknown Mach leaves a switch position undetermined, and the stage is held out",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [S5]

        private static void ValidateSwitchesNeverActivate(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[S5] Switch facts alone never activate an unavailable gain");

            float[] machs = { 0.3f, 0.9f, 1.0f, 1.2f, 1.5f, 1.8f };
            bool neutral = true;
            for (int i = 0; i < machs.Length; i++)
            {
                MavF15FcsSolution s = Solve(MavF15FcsMode.ExactNasa836Unavailable,
                    MavF15ControlLawSchedules.Unavailable(), FullStick(), State(machs[i], 0.3f));
                if (!IsNeutral(s.requested) || AppliedCount(s) != 0
                    || s.EffectiveProvenance != MavEngineDataProvenance.Unavailable)
                    neutral = false;
            }

            Record(neutral,
                "exact mode, no gains: neutral and no stage applied on both sides of both switches",
                report, ref passed, ref failed);

            // Only the two switched stages' gains declared - no mechanical gearing, so nothing
            // may run, switch or no switch.
            MavF15ControlLawSchedules onlySwitched = MavF15ControlLawSchedules.Unavailable();
            onlySwitched.ariRudderPerAileron = SyntheticAuthoritative(1f);
            onlySwitched.rollRateToYawCrossfeedDegPerRadSec = SyntheticAuthoritative(1f);
            bool stillNeutral = true;
            for (int i = 0; i < machs.Length; i++)
            {
                MavF15FcsSolution s = Solve(MavF15FcsMode.ExactNasa836Unavailable, onlySwitched,
                    FullStick(), State(machs[i], 0.3f));
                if (!IsNeutral(s.requested) || AppliedCount(s) != 0)
                    stillNeutral = false;
            }

            Record(stillNeutral,
                "with only the switched stages' gains declared, the law still forms no demand",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [S6]

        private static void ValidateSwitchGating(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[S6] Switch gating in the exact law (SYNTHETIC gains)");

            MavF15ControlLawSchedules all = SyntheticAll(MavEngineDataProvenance.Authoritative);

            Record(Applied(MavF15FcsMode.ExactNasa836Unavailable, all, 1.4f, MavF15FcsStage.AileronRudderInterconnect)
                   && Applied(MavF15FcsMode.ExactNasa836Unavailable, all, 1.5f, MavF15FcsStage.AileronRudderInterconnect)
                   && !Applied(MavF15FcsMode.ExactNasa836Unavailable, all, 1.6f, MavF15FcsStage.AileronRudderInterconnect),
                "ARI runs at Mach 1.4 and 1.5 and is held out at 1.6",
                report, ref passed, ref failed);

            Record(Applied(MavF15FcsMode.ExactNasa836Unavailable, all, 0.9f, MavF15FcsStage.RollToYawCrossfeed)
                   && Applied(MavF15FcsMode.ExactNasa836Unavailable, all, 1.0f, MavF15FcsStage.RollToYawCrossfeed)
                   && !Applied(MavF15FcsMode.ExactNasa836Unavailable, all, 1.1f, MavF15FcsStage.RollToYawCrossfeed),
                "roll-yaw crossfeed runs at Mach 0.9 and 1.0 and is held out at 1.1",
                report, ref passed, ref failed);

            Record(!Applied(MavF15FcsMode.ExactNasa836Unavailable, all, float.NaN, MavF15FcsStage.AileronRudderInterconnect)
                   && !Applied(MavF15FcsMode.ExactNasa836Unavailable, all, float.NaN, MavF15FcsStage.RollToYawCrossfeed),
                "an unknown Mach holds both switched stages out",
                report, ref passed, ref failed);

            // A switched-out stage contributes nothing: the rudder difference across the ARI
            // switch equals the ARI's own contribution just below it.
            MavF15FcsSolution below = Solve(MavF15FcsMode.ExactNasa836Unavailable, all, RollOnly(), State(1.5f, 0f));
            MavF15FcsSolution above = Solve(MavF15FcsMode.ExactNasa836Unavailable, all, RollOnly(), State(1.5001f, 0f));
            float ariBelow = Contribution(below, MavF15FcsStage.AileronRudderInterconnect);
            Record(Mathf.Abs(ariBelow) > Tolerance
                   && Mathf.Abs((below.requested.channels.rudderDeg - above.requested.channels.rudderDeg) - ariBelow) < Tolerance
                   && Contribution(above, MavF15FcsStage.AileronRudderInterconnect) == 0f,
                "across the switch the rudder loses exactly the ARI contribution, nothing more",
                report, ref passed, ref failed);

            // Switches only remove: never more stages above a switch than below it.
            bool onlyRemove = true;
            for (float m = 0.1f; m <= 2.0f; m += 0.1f)
            {
                if (AppliedCount(Solve(MavF15FcsMode.ExactNasa836Unavailable, all, FullStick(), State(m, 0.2f)))
                    > AppliedCount(Solve(MavF15FcsMode.ExactNasa836Unavailable, all, FullStick(), State(0.5f, 0.2f))))
                    onlyRemove = false;
            }

            Record(onlyRemove, "across Mach 0.1-2.0 the switches never add a stage",
                report, ref passed, ref failed);

            MavF15ControlLawSchedules research = SyntheticAll(MavEngineDataProvenance.CrossValidationOnly);
            Record(Applied(MavF15FcsMode.AFITResearch, research, 1.8f, MavF15FcsStage.AileronRudderInterconnect)
                   && Applied(MavF15FcsMode.AFITResearch, research, 1.8f, MavF15FcsStage.RollToYawCrossfeed),
                "the 836 switches do not gate the research mode, which draws on other sources",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [S7]

        private static void ValidateExactNeutral(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[S7] The exact 836 FCS stays neutral with its gains unavailable");

            bool neutral = true;
            List<MavF15FcsStageResult> buffer = new List<MavF15FcsStageResult>(12);
            for (float m = 0f; m <= 2.0f; m += 0.05f)
            {
                for (float alpha = -5f; alpha <= 30f; alpha += 7f)
                {
                    MavF15FcsSolution s = MavF15ControlLaw.Solve(
                        MavF15FcsMode.ExactNasa836Unavailable, MavF15ControlLawSchedules.Unavailable(),
                        FullStick(), State(m, alpha * Mathf.Deg2Rad), buffer);
                    if (!IsNeutral(s.requested) || AppliedCount(s) != 0
                        || s.stages.Count != (int)MavF15FcsStage.RollToYawCrossfeed + 1)
                        neutral = false;
                }
            }

            Record(neutral,
                "Mach 0-2 x alpha -5..30 deg at full stick: neutral, 0 of 11 stages applied, "
                + "every stage reported",
                report, ref passed, ref failed);

            Record(!MavF15ControlLawSchedules.Unavailable().MechanicalPathAvailable
                   && MavF15Nasa836FcsStructure.Get(MavF15FcsStage.MechanicalPath).IsExact836Structure,
                "the mechanical path is confirmed as structure and still has no gearing - "
                + "structure is not a gain",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- helpers

        private static MavF15FcsSolution Solve(
            MavF15FcsMode mode, MavF15ControlLawSchedules schedules,
            MavF15PilotCommand command, MavFlightState state)
        {
            return MavF15ControlLaw.Solve(mode, schedules, command, state, null);
        }

        private static bool Applied(MavF15FcsMode mode, MavF15ControlLawSchedules s, float mach, MavF15FcsStage stage)
        {
            MavF15FcsSolution sol = Solve(mode, s, FullStick(), State(mach, 0.2f));
            for (int i = 0; i < sol.stages.Count; i++)
            {
                if (sol.stages[i].stage == stage)
                    return sol.stages[i].applied;
            }

            return false;
        }

        private static float Contribution(MavF15FcsSolution sol, MavF15FcsStage stage)
        {
            for (int i = 0; i < sol.stages.Count; i++)
            {
                if (sol.stages[i].stage == stage && sol.stages[i].applied)
                    return sol.stages[i].contributionDeg;
            }

            return 0f;
        }

        private static int AppliedCount(MavF15FcsSolution s)
        {
            int n = 0;
            for (int i = 0; i < s.stages.Count; i++)
            {
                if (s.stages[i].applied)
                    n++;
            }

            return n;
        }

        private static bool IsNeutral(MavF15RequestedSurfaceState r)
        {
            return r.channels.symmetricStabilatorDeg == 0f && r.channels.differentialStabilatorDeg == 0f
                && r.channels.aileronDeg == 0f && r.channels.rudderDeg == 0f;
        }

        private static MavF15PilotCommand FullStick()
        {
            return new MavF15PilotCommand
            {
                pitch = 1f, roll = 1f, yaw = 1f, throttle01 = 0f,
                pitchCasEngaged = true, rollCasEngaged = true, yawCasEngaged = true
            };
        }

        private static MavF15PilotCommand RollOnly()
        {
            MavF15PilotCommand c = FullStick();
            c.pitch = 0f;
            c.yaw = 0f;
            return c;
        }

        private static MavFlightState State(float mach, float rollRate)
        {
            MavFlightState state = new MavFlightState();
            state.alphaRad = 5f * Mathf.Deg2Rad;
            state.trueAirspeedMps = 250f;
            state.mach = mach;
            state.aeroBodyRatesRadSec = new Vector3(rollRate, 0f, 0f);
            state.specificForceValid = false;
            return state;
        }

        private static MavF15ControlGain SyntheticAuthoritative(float value)
        {
            return new MavF15ControlGain
            {
                value = value,
                provenance = MavEngineDataProvenance.Authoritative,
                sourceNote = "SYNTHETIC validation value. Not F-15 data."
            };
        }

        private static MavF15ControlLawSchedules SyntheticAll(MavEngineDataProvenance provenance)
        {
            MavF15ControlGain g = new MavF15ControlGain
            {
                value = 1f,
                provenance = provenance,
                sourceNote = "SYNTHETIC validation value. Not F-15 data."
            };

            return new MavF15ControlLawSchedules
            {
                pitchStickToStabilatorDegPerUnit = g,
                rollStickToAileronDegPerUnit = g,
                rollStickToDifferentialStabilatorDegPerUnit = g,
                pedalToRudderDegPerUnit = g,
                pitchRatioChanger = g,
                rollRatioChanger = g,
                pitchRateFeedbackDegPerRadSec = g,
                normalLoadFactorFeedbackDegPerG = g,
                rollRateFeedbackDegPerRadSec = g,
                yawRateFeedbackDegPerRadSec = g,
                ariRudderPerAileron = g,
                stallInhibitorAlphaThresholdDeg = g,
                stallInhibitorDegPerDegAlpha = g,
                turnCoordinationDegPerRadSec = g,
                rollRateToYawCrossfeedDegPerRadSec = g,
                rollDamperWashout = new MavF15RollDamperSchedule
                {
                    fullAuthorityAlphaDeg = 10f,
                    zeroAuthorityAlphaDeg = 20f,
                    shape = MavF15RollDamperWashoutShape.Linear,
                    provenance = provenance,
                    sourceNote = "SYNTHETIC validation value. Not F-15 data."
                }
            };
        }

        private static void Record(
            bool condition, string label,
            StringBuilder report, ref int passed, ref int failed)
        {
            if (condition) passed++; else failed++;
            report.Append(condition ? "  PASS  " : "  FAIL  ").AppendLine(label);
        }
    }
}
