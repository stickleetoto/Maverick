#if UNITY_EDITOR
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MaverickFresh.EditorTools
{
    /// <summary>
    /// Phase 4B validation: turn entry, curvature ownership, rotational inertia, sustained-turn energy.
    ///
    /// Scenarios A-F run through MavTurnDynamicsModel, which integrates the pitch axis using the
    /// production lift and drag curves from MavAeroBody and the production rules from
    /// MavTurnDynamicsRules. Nothing here re-implements the aerodynamics.
    ///
    /// WHY A MODEL AND NOT JUST FIELD CHECKS. Every Phase 4B requirement is about a TRANSIENT - turn
    /// rate must not arrive instantly, angular motion must decay over a finite time, a sustained turn
    /// must cost energy. None of that is visible in configuration values. Phase 4A's own conservation
    /// check inspected settings, reported a tidy 1.00 ownership sum, and completely missed that the
    /// aircraft was flying on 28% aerodynamic lift because it multiplied aeroBlend by nothing.
    ///
    /// WHAT THE MODEL IS NOT. Pitch-plane only: it assumes bank is established and asks what the pitch
    /// axis does. It does not model roll-in, cross-axis coupling, the instructor's full command
    /// shaping, or the visual feel of the result. It is a mechanism check. Feel still has to be flown.
    /// </summary>
    public static class MavTurnDynamicsPhase4BValidation
    {
        [MenuItem("Maverick/Flight Dynamics/Run Phase 4B Turn Dynamics Validation")]
        public static void RunValidation()
        {
            int passed = 0;
            int failed = 0;
            StringBuilder report = new StringBuilder(8192);
            report.AppendLine("Maverick PHASE 4B TURN ENTRY & INERTIA Validation");
            report.AppendLine("================================================");

            MavTurnModelConfig a4 = MavTurnModelConfig.F16(false);
            MavTurnModelConfig b4 = MavTurnModelConfig.F16(true);

            ValidateOwnership(a4, b4, report, ref passed, ref failed);
            ValidateProfileWiring(report, ref passed, ref failed);
            ValidateScenarioA(b4, report, ref passed, ref failed);
            ValidateScenarioB(a4, b4, report, ref passed, ref failed);
            ValidateScenarioC(a4, b4, report, ref passed, ref failed);
            ValidateScenarioD(a4, b4, report, ref passed, ref failed);
            ValidateScenarioEF(b4, report, ref passed, ref failed);
            ValidateRegressionSafety(report, ref passed, ref failed);

            report.AppendLine();
            report.Append("RESULT: ").Append(failed == 0 ? "PASS" : "FAIL")
                .Append("  passed=").Append(passed).Append(" failed=").Append(failed);

            if (failed == 0)
                Debug.Log(report.ToString());
            else
                Debug.LogError(report.ToString());
        }

        // ================================================================== ownership

        private static void ValidateOwnership(
            MavTurnModelConfig a4, MavTurnModelConfig b4,
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[OWN] Curvature ownership conservation");

            float aeroA = MavTurnDynamicsModel.AeroAuthority(a4);
            float legA = MavTurnDynamicsModel.LegacyAuthority(a4);
            float aeroB = MavTurnDynamicsModel.AeroAuthority(b4);
            float legB = MavTurnDynamicsModel.LegacyAuthority(b4);

            report.Append("        Phase 4A: aero ").Append(Pct(aeroA))
                .Append("  legacy assist ").AppendLine(Pct(legA));
            report.Append("        Phase 4B: aero ").Append(Pct(aeroB))
                .Append("  legacy assist ").AppendLine(Pct(legB));

            Record(Mathf.Abs(aeroA + legA - 1f) < 1e-4f,
                "Phase 4A ownership sums to 1", report, ref passed, ref failed);
            Record(Mathf.Abs(aeroB + legB - 1f) < 1e-4f,
                "Phase 4B ownership sums to 1 - nothing was added without being paid for",
                report, ref passed, ref failed);
            Record(aeroB > aeroA + 0.4f && legB < legA * 0.35f,
                "ownership genuinely moved: aero " + Pct(aeroA) + " -> " + Pct(aeroB)
                + ", assist " + Pct(legA) + " -> " + Pct(legB),
                report, ref passed, ref failed);

            // The Phase 4A accounting error, as a check rather than a claim.
            float nominal = MavAeroBody.ComputeAeroTurnAuthority(true, a4.aeroBlend, a4.liftBlend, false);
            float actual = MavAeroBody.ComputeAeroTurnAuthority(true, a4.aeroBlend, a4.liftBlend, true);
            Record(nominal > actual + 0.15f,
                "Phase 4A's aeroBlend-only accounting overstated real aero authority by "
                + Pct(nominal - actual) + " (" + Pct(nominal) + " claimed, " + Pct(actual) + " applied)",
                report, ref passed, ref failed);

            // The alignment assist is the other migrated mechanism.
            float alignA = MavTurnDynamicsRules.ComputeAlignmentAssistScale(legA, 0.15f);
            float alignB = MavTurnDynamicsRules.ComputeAlignmentAssistScale(legB, 0.15f);
            report.Append("        alignment assist scale: Phase 4A ").Append(Pct(alignA))
                .Append(" -> Phase 4B ").AppendLine(Pct(alignB));
            Record(alignB < alignA,
                "the nose-onto-velocity alignment assist was migrated too, not left at full strength",
                report, ref passed, ref failed);
            Record(alignB > 0.01f,
                "but not to zero, because it is the low-speed floor under aerodynamic stability",
                report, ref passed, ref failed);
        }

        // ================================================================== wiring

        /// <summary>
        /// The applier writes several Phase 4B fields by NAME through reflection, and its Set helper
        /// swallows failures. A typo would silently configure nothing - the same shape of bug as an
        /// aircraft identity that never got applied - so the field names are checked to exist.
        /// </summary>
        private static void ValidateProfileWiring(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[WIRE] Reflection-set field names exist");

            string[] jetFields =
            {
                "usePhase4BTurnDynamics",
                "thrustBoostSuppressionG",
                "releaseRateNullingScale",
                "alignmentAssistFloorAtFullAero"
            };

            for (int i = 0; i < jetFields.Length; i++)
            {
                FieldInfo f = typeof(MavMouseFlightJet).GetField(
                    jetFields[i], BindingFlags.Instance | BindingFlags.Public);
                Record(f != null,
                    "MavMouseFlightJet." + jetFields[i] + " exists, so the applier's reflection write "
                    + "is not a silent no-op",
                    report, ref passed, ref failed);
            }

            MavAircraftRuntimeProfile f16 = MavAircraftCatalog.GetBuiltIn(MavAircraftKind.F16C);
            Record(f16 != null, "the F-16 profile resolves", report, ref passed, ref failed);

            if (f16 != null)
            {
                Record(f16.usePhase4BTurnDynamics,
                    "the F-16 profile has Phase 4B turn dynamics enabled",
                    report, ref passed, ref failed);
                Record(f16.useAeroStaticStability,
                    "and aerodynamic static stability enabled, which the alignment-assist migration "
                    + "depends on",
                    report, ref passed, ref failed);

                MavAircraftRuntimeProfile f22 = MavAircraftCatalog.GetBuiltIn(MavAircraftKind.F22A);
                Record(f22 != null && !f22.usePhase4BTurnDynamics,
                    "and the F-22 is NOT migrated, so aircraft that have not been re-flown keep "
                    + "Phase 4A behaviour exactly",
                    report, ref passed, ref failed);
            }
        }

        // ================================================================== A

        private static void ValidateScenarioA(
            MavTurnModelConfig b4, StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[A] Straight flight baseline");

            MavTurnModelState[] run = MavTurnDynamicsModel.Simulate(b4, 245f, 0f, 0f, 4f, 0.02f);
            MavTurnModelState end = run[run.Length - 1];

            Record(Mathf.Abs(end.aoaDeg) < 1f && Mathf.Abs(end.turnRateDegPerSec) < 1f
                   && Mathf.Abs(end.pitchRateDegPerSec) < 1f,
                "no command leaves no AoA, no turn rate and no residual pitch rate (AoA "
                + F(end.aoaDeg) + " deg, turn " + F(end.turnRateDegPerSec) + " deg/s)",
                report, ref passed, ref failed);

            float liftG = MavTurnDynamicsModel.AeroCurvatureAccel(b4, 2f, 245f) / 9.80665f;
            report.Append("        lift at 2 deg AoA, 245 m/s, 3000 m: ").Append(F(liftG)).AppendLine(" g");
            Record(liftG > 0.7f && liftG < 1.8f,
                "trim sits near 1 g at a small AoA, which is why gravityBlend goes to 1.0 alongside "
                + "the lift increase",
                report, ref passed, ref failed);
        }

        // ================================================================== B

        private static void ValidateScenarioB(
            MavTurnModelConfig a4, MavTurnModelConfig b4,
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[B] Abrupt full turn command - finite turn-entry transient");

            MavTurnModelState[] runA = MavTurnDynamicsModel.Simulate(a4, 245f, 1f, 6f, 6f, 0.02f);
            MavTurnModelState[] runB = MavTurnDynamicsModel.Simulate(b4, 245f, 1f, 6f, 6f, 0.02f);

            float t90A = MavTurnDynamicsModel.TimeToFractionOfPeakTurnRate(runA, 0.9f);
            float t90B = MavTurnDynamicsModel.TimeToFractionOfPeakTurnRate(runB, 0.9f);

            report.Append("        Phase 4A peak ").Append(F(MavTurnDynamicsModel.PeakTurnRate(runA)))
                .Append(" deg/s, 90% at ").Append(F(t90A)).AppendLine(" s");
            report.Append("        Phase 4B peak ").Append(F(MavTurnDynamicsModel.PeakTurnRate(runB)))
                .Append(" deg/s, 90% at ").Append(F(t90B)).AppendLine(" s");

            Record(t90B > 0.25f,
                "final turn rate is NOT reached instantly: 90% at t=" + F(t90B) + " s",
                report, ref passed, ref failed);
            Record(t90B > t90A,
                "and entry takes longer than Phase 4A (" + F(t90A) + " -> " + F(t90B) + " s)",
                report, ref passed, ref failed);

            // The required sequence, by onset against fixed thresholds.
            float tRate = FirstTimeAbove(runB, 1, 1.0f);
            float tAoA = FirstTimeAbove(runB, 0, 0.5f);
            float tTurn = FirstTimeAbove(runB, 2, 0.5f);
            report.Append("        onset: pitch rate ").Append(F(tRate))
                .Append(" s -> AoA ").Append(F(tAoA))
                .Append(" s -> curvature ").Append(F(tTurn)).AppendLine(" s");

            Record(tRate >= 0f && tAoA >= 0f && tTurn >= 0f,
                "all three signals appear", report, ref passed, ref failed);
            Record(tRate <= tAoA + 1e-3f && tAoA <= tTurn + 1e-3f,
                "and in the required order: rate response, then AoA, then trajectory curvature",
                report, ref passed, ref failed);

            MavTurnModelState midB = MavTurnDynamicsModel.At(runB, 1f);
            MavTurnModelState midA = MavTurnDynamicsModel.At(runA, 1f);
            Record(midB.aeroCurvatureShare > 0.85f && midB.aeroCurvatureShare > midA.aeroCurvatureShare,
                "the turn is produced aerodynamically: measured aero share "
                + Pct(midB.aeroCurvatureShare) + " vs Phase 4A's " + Pct(midA.aeroCurvatureShare),
                report, ref passed, ref failed);

            float peakAoAB = PeakAbs(runB, 0);
            Record(peakAoAB > 2f,
                "AoA opens to " + F(peakAoAB) + " deg, so lift has something to build the turn from",
                report, ref passed, ref failed);
            Record(peakAoAB < b4.aoaHardLimitDeg + 2f,
                "and the AoA limiter HOLDS it (" + F(peakAoAB) + " deg vs hard limit "
                + F(b4.aoaHardLimitDeg) + ") rather than letting a held pull depart",
                report, ref passed, ref failed);
        }

        // ================================================================== C

        private static void ValidateScenarioC(
            MavTurnModelConfig a4, MavTurnModelConfig b4,
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[C] Sustained high-load turn - energy must be spent");

            MavTurnModelConfig cruiseB = b4;
            cruiseB.throttle01 = 0.60f;
            MavTurnModelState[] runB = MavTurnDynamicsModel.Simulate(cruiseB, 260f, 1f, 12f, 12f, 0.02f);

            MavTurnModelConfig cruiseA = a4;
            cruiseA.throttle01 = 0.60f;
            MavTurnModelState[] runA = MavTurnDynamicsModel.Simulate(cruiseA, 260f, 1f, 12f, 12f, 0.02f);

            float lostB = runB[0].speed - runB[runB.Length - 1].speed;
            float lostA = runA[0].speed - runA[runA.Length - 1].speed;
            report.Append("        60% throttle, 12 s hard turn: Phase 4A lost ").Append(F(lostA))
                .Append(" m/s, Phase 4B lost ").Append(F(lostB)).AppendLine(" m/s");

            Record(PeakAbs(runB, 3) > 2.5f,
                "the turn loads the aircraft (" + F(PeakAbs(runB, 3)) + " g)",
                report, ref passed, ref failed);
            Record(lostB > 10f,
                "and it costs real energy: " + F(lostB) + " m/s lost",
                report, ref passed, ref failed);
            Record(lostB > lostA + 10f,
                "far more than Phase 4A, which lost " + F(lostA)
                + " m/s - it was effectively a free turn",
                report, ref passed, ref failed);

            float dragLow = MavTurnDynamicsModel.AeroDragDecel(b4, 2f, 260f);
            float dragHigh = MavTurnDynamicsModel.AeroDragDecel(b4, 10f, 260f);
            Record(dragHigh > dragLow * 2.5f,
                "induced drag rises steeply with load (" + F(dragLow) + " -> " + F(dragHigh)
                + " m/s^2 from 2 to 10 deg AoA)",
                report, ref passed, ref failed);

            Record(MavTurnDynamicsRules.ComputeThrustBoostAllowance(1f, 3f) > 0.99f,
                "the low-speed thrust boost still rescues slow level flight at 1 g",
                report, ref passed, ref failed);
            Record(MavTurnDynamicsRules.ComputeThrustBoostAllowance(3f, 3f) < 0.01f
                   && MavTurnDynamicsRules.ComputeThrustBoostAllowance(5f, 3f) < 0.01f,
                "and is fully suppressed under load, so it cannot silently pay the turn's energy bill",
                report, ref passed, ref failed);
        }

        // ================================================================== D

        private static void ValidateScenarioD(
            MavTurnModelConfig a4, MavTurnModelConfig b4,
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[D] Release to neutral - finite decay without oscillation");

            // Gentle input, so AoA is small at release. Released from a hard pull the new static
            // stability moment dominates and would be measured instead of rotational inertia.
            MavTurnModelState[] runA = MavTurnDynamicsModel.Simulate(a4, 245f, 0.25f, 0.6f, 4f, 0.02f);
            MavTurnModelState[] runB = MavTurnDynamicsModel.Simulate(b4, 245f, 0.25f, 0.6f, 4f, 0.02f);

            float decayA = DecayTime(runA, 0.6f, 0.1f);
            float decayB = DecayTime(runB, 0.6f, 0.1f);
            report.Append("        pitch rate to 10% of release value: Phase 4A ").Append(F(decayA))
                .Append(" s, Phase 4B ").Append(F(decayB)).AppendLine(" s");

            Record(decayB > 0.03f,
                "angular motion decays over a finite time (" + F(decayB) + " s)",
                report, ref passed, ref failed);
            Record(decayB > decayA,
                "with more rotational inertia than Phase 4A (" + F(decayA) + " -> " + F(decayB) + " s)",
                report, ref passed, ref failed);

            int revB = MavTurnDynamicsModel.CountRateSignReversalsAfter(runB, 0.6f);
            Record(revB <= 1,
                "and no oscillation: " + revB + " pitch-rate sign reversals after release",
                report, ref passed, ref failed);

            MavTurnModelState[] hardB = MavTurnDynamicsModel.Simulate(b4, 245f, 1f, 2f, 6f, 0.02f);
            int revHard = MavTurnDynamicsModel.CountRateSignReversalsAfter(hardB, 2f);
            Record(revHard <= 1,
                "released from a hard pull the nose is restored without hunting (" + revHard
                + " reversals)",
                report, ref passed, ref failed);

            Record(Mathf.Abs(MavTurnDynamicsRules.ComputeRateNullingScale(1f, 0.06f, 0.35f) - 1f) < 1e-4f,
                "a commanded rate still gets the full controller, so the aircraft is not sluggish",
                report, ref passed, ref failed);
            float half = MavTurnDynamicsRules.ComputeRateNullingScale(0.03f, 0.06f, 0.35f);
            Record(half > 0.35f && half < 1f,
                "and the transition is blended, with no torque step at the threshold",
                report, ref passed, ref failed);
        }

        // ================================================================== E / F

        private static void ValidateScenarioEF(
            MavTurnModelConfig b4, StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[E/F] Low-speed and high-speed turn behaviour");

            MavTurnModelState[] slow = MavTurnDynamicsModel.Simulate(b4, 130f, 1f, 6f, 6f, 0.02f);
            MavTurnModelState[] fast = MavTurnDynamicsModel.Simulate(b4, 380f, 1f, 6f, 6f, 0.02f);

            float gSlow = PeakAbs(slow, 3);
            float gFast = PeakAbs(fast, 3);

            MavTurnModelState slowAt = MavTurnDynamicsModel.At(slow, 1f);
            MavTurnModelState fastAt = MavTurnDynamicsModel.At(fast, 1f);

            report.Append("        entry 130 m/s: peak ").Append(F(gSlow))
                .Append(" g, at t=1 s AoA ").Append(F(slowAt.aoaDeg)).AppendLine(" deg");
            report.Append("        entry 380 m/s: peak ").Append(F(gFast))
                .Append(" g, at t=1 s AoA ").Append(F(fastAt.aoaDeg)).AppendLine(" deg");

            Record(gFast > gSlow,
                "the same command reaches higher load at higher speed (" + F(gSlow) + " -> "
                + F(gFast) + " g), because load is lift and lift needs dynamic pressure",
                report, ref passed, ref failed);
            Record(PeakAbs(slow, 0) > 4f,
                "the low-speed case needs real AoA to turn at all (" + F(PeakAbs(slow, 0)) + " deg)",
                report, ref passed, ref failed);
        }

        // ================================================================== regression

        private static void ValidateRegressionSafety(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[REG] Phase 4A behaviour and aircraft identity preserved");

            // The Phase 4A rule must still answer exactly as it did, or the F-22 and the rest change
            // behaviour without anyone having chosen that.
            Record(Mathf.Abs(MavAeroBody.ComputeLegacyVelocityAssistScale(true, 0.54f) - 0.46f) < 1e-4f,
                "the Phase 4A two-argument ownership rule is unchanged (0.54 -> 0.46)",
                report, ref passed, ref failed);
            Record(Mathf.Abs(MavAeroBody.ComputeLegacyVelocityAssistScale(false, 0.54f) - 1f) < 1e-4f,
                "and still gives the assist everything when aero is off",
                report, ref passed, ref failed);

            MavAircraftRuntimeProfile f16 = MavAircraftCatalog.GetBuiltIn(MavAircraftKind.F16C);
            Record(f16 != null && f16.aircraft == MavAircraftKind.F16C && f16.aircraftId == "f16c",
                "F-16 canonical identity is intact",
                report, ref passed, ref failed);
            Record(f16 != null && Mathf.Abs(f16.mass - 9800f) < 0.5f
                   && Mathf.Abs(f16.wingArea - 27.9f) < 0.05f && !f16.useThrustVectorControl,
                "and its identifying figures are untouched by Phase 4B tuning (9800 kg, 27.9 m^2, no TVC)",
                report, ref passed, ref failed);
        }

        // ================================================================== helpers

        // 0 = AoA, 1 = pitch rate, 2 = turn rate, 3 = load factor.
        private static float Pick(MavTurnModelState s, int which)
        {
            if (which == 0) return Mathf.Abs(s.aoaDeg);
            if (which == 1) return Mathf.Abs(s.pitchRateDegPerSec);
            if (which == 2) return Mathf.Abs(s.turnRateDegPerSec);
            return Mathf.Abs(s.loadFactorG);
        }

        private static float PeakAbs(MavTurnModelState[] run, int which)
        {
            float peak = 0f;
            for (int i = 0; i < run.Length; i++)
                peak = Mathf.Max(peak, Pick(run[i], which));

            return peak;
        }

        private static float FirstTimeAbove(MavTurnModelState[] run, int which, float threshold)
        {
            for (int i = 0; i < run.Length; i++)
            {
                if (Pick(run[i], which) >= threshold)
                    return run[i].timeSeconds;
            }

            return -1f;
        }

        private static float DecayTime(MavTurnModelState[] run, float releaseTime, float fraction)
        {
            float atRelease = 0f;
            for (int i = 0; i < run.Length; i++)
            {
                if (run[i].timeSeconds >= releaseTime)
                {
                    atRelease = Mathf.Abs(run[i].pitchRateDegPerSec);
                    break;
                }
            }

            if (atRelease < 1e-4f)
                return 0f;

            for (int i = 0; i < run.Length; i++)
            {
                if (run[i].timeSeconds < releaseTime)
                    continue;

                if (Mathf.Abs(run[i].pitchRateDegPerSec) <= atRelease * fraction)
                    return run[i].timeSeconds - releaseTime;
            }

            return -1f;
        }

        private static string F(float v) { return v.ToString("F2"); }
        private static string Pct(float v) { return (v * 100f).ToString("F1") + "%"; }

        private static void Record(bool ok, string name, StringBuilder report, ref int passed, ref int failed)
        {
            if (ok)
            {
                passed++;
                report.Append("  PASS  ").AppendLine(name);
            }
            else
            {
                failed++;
                report.Append("  FAIL  ").AppendLine(name);
            }
        }
    }
}
#endif
