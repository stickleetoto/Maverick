using System;
using System.Text;
using UnityEngine;
using MaverickFresh.FlightDynamics.F16;

namespace MaverickFresh.FlightDynamics.Validation
{
    /// <summary>
    /// Phase 5D validation for the frozen NASA TP-1538 Table VI F-16 thrust runtime.
    ///
    /// This suite is intentionally independent of the table component's private arrays: the 90
    /// published SI values are repeated here as the validation oracle. It exercises the pure frozen
    /// evaluator and then the real MavEngineRuntime -> MavPropulsionSystem path on an in-memory
    /// single-engine F-16 installation. It never enables gameplay F16Replacement.
    /// </summary>
    public static class MavF16Tp1538RuntimeValidation
    {
        private const float ToleranceN = 0.05f;

        private static readonly float[] AltitudesM =
        {
            0f, 3048f, 6096f, 9144f, 12192f, 15240f
        };

        private static readonly float[] Machs =
        {
            0.2f, 0.4f, 0.6f, 0.8f, 1.0f
        };

        // Independent oracle, row-major [altitude,mach], copied from the frozen PUBLISHED SI table.
        private static readonly float[] IdleN =
        {
             2824f,   267f, -4537f, -12010f, -16013f,
             1890f,   111f, -3158f,  -8451f,  -6227f,
             3069f,  1535f, -1334f,  -5782f,  -2647f,
             4492f,  3358f,  1557f,  -1099f,  -1521f,
             5916f,  5026f,  4048f,   2669f,   -890f,
             7562f,  6783f,  6049f,   4893f,   3114f
        };

        private static readonly float[] MilitaryN =
        {
            56401f, 56089f, 56223f, 55111f, 51953f,
            40699f, 41420f, 43764f, 45263f, 43804f,
            28080f, 29401f, 31536f, 34472f, 35806f,
            17970f, 19082f, 20728f, 23663f, 27133f,
            10987f, 11565f, 12632f, 14456f, 16902f,
             6227f,  6939f,  7384f,  8585f, 10275f
        };

        private static readonly float[] MaximumN =
        {
             95276f, 100970f, 107820f, 115959f, 128485f,
             69834f,  74993f,  84112f,  93742f, 103723f,
             49929f,  54488f,  61204f,  71057f,  81398f,
             32573f,  36269f,  41300f,  49440f,  59977f,
             19727f,  22240f,  25354f,  30513f,  38440f,
             11565f,  12610f,  14300f,  17570f,  22494f
        };

        public static string RunAll(out int passed, out int failed)
        {
            passed = 0;
            failed = 0;
            StringBuilder report = new StringBuilder(8192);
            report.AppendLine("Maverick F-16 TP-1538 Phase 5D Runtime Validation");
            report.AppendLine("=================================================");

            ValidateAllGridPoints(report, ref passed, ref failed);
            ValidatePowerStateBoundaries(report, ref passed, ref failed);
            ValidateInterpolation(report, ref passed, ref failed);
            ValidateRefusalAndStaleThrust(report, ref passed, ref failed);
            ValidatePoweredReferenceIntegration(report, ref passed, ref failed);

            report.AppendLine();
            report.Append("RESULT: ")
                .Append(failed == 0 ? "PASS" : "FAIL")
                .Append(" passed=").Append(passed)
                .Append(" failed=").Append(failed);
            return report.ToString();
        }

        private static void ValidateAllGridPoints(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[D0] All 90 published SI thrust cells");
            int negativeIdle = 0;

            for (int h = 0; h < AltitudesM.Length; h++)
            {
                for (int m = 0; m < Machs.Length; m++)
                {
                    int i = h * Machs.Length + m;
                    CheckExact(AltitudesM[h], Machs[m], 0f, IdleN[i], "Tidle", report, ref passed, ref failed);
                    CheckExact(AltitudesM[h], Machs[m], 50f, MilitaryN[i], "Tmil", report, ref passed, ref failed);
                    CheckExact(AltitudesM[h], Machs[m], 100f, MaximumN[i], "Tmax", report, ref passed, ref failed);

                    if (IdleN[i] < 0f)
                    {
                        negativeIdle++;
                        MavThrustDeckResult r = MavF16Tp1538ThrustDeck.EvaluateFrozen(
                            MavThrustDeckQuery.Create(AltitudesM[h], Machs[m], 0f));
                        Record(r.valid && r.thrustN < 0f,
                            "negative Tidle preserved h=" + AltitudesM[h] + " M=" + Machs[m],
                            report, ref passed, ref failed);
                    }
                }
            }

            Record(negativeIdle == 12,
                "source oracle contains exactly 12 negative idle coordinates",
                report, ref passed, ref failed);
        }

        private static void CheckExact(
            float altitudeM, float mach, float power, float expectedN, string state,
            StringBuilder report, ref int passed, ref int failed)
        {
            MavThrustDeckResult r = MavF16Tp1538ThrustDeck.EvaluateFrozen(
                MavThrustDeckQuery.Create(altitudeM, mach, power));
            Record(r.valid
                   && r.insideEnvelope
                   && r.authority == MavThrustDataAuthority.Authoritative
                   && Near(r.thrustN, expectedN),
                state + " exact h=" + altitudeM + " M=" + mach + " -> " + expectedN + " N",
                report, ref passed, ref failed);
        }

        private static void ValidatePowerStateBoundaries(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[D1] Garza/Morelli actual-power thrust semantics");

            const float h = 0f;
            const float m = 0.2f;
            const float idle = 2824f;
            const float military = 56401f;
            const float maximum = 95276f;

            CheckPower(h, m, 0f, idle, "Pa=0 -> Tidle", report, ref passed, ref failed);
            CheckPower(h, m, 25f, idle + 0.5f * (military - idle),
                "Pa=25 -> halfway Tidle/Tmil", report, ref passed, ref failed);
            CheckPower(h, m, 50f, military, "Pa=50 -> Tmil", report, ref passed, ref failed);
            CheckPower(h, m, 75f, military + 0.5f * (maximum - military),
                "Pa=75 -> halfway Tmil/Tmax", report, ref passed, ref failed);
            CheckPower(h, m, 100f, maximum, "Pa=100 -> Tmax", report, ref passed, ref failed);
        }

        private static void CheckPower(
            float altitudeM, float mach, float power, float expectedN, string name,
            StringBuilder report, ref int passed, ref int failed)
        {
            MavThrustDeckResult r = MavF16Tp1538ThrustDeck.EvaluateFrozen(
                MavThrustDeckQuery.Create(altitudeM, mach, power));
            Record(r.valid && Near(r.thrustN, expectedN), name,
                report, ref passed, ref failed);
        }

        private static void ValidateInterpolation(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[D2] Source-authorized linear interpolation");

            // Mach midpoint at sea level, military power: average 56401 and 56089.
            MavThrustDeckResult machMid = MavF16Tp1538ThrustDeck.EvaluateFrozen(
                MavThrustDeckQuery.Create(0f, 0.3f, 50f));
            Record(machMid.valid && Near(machMid.thrustN, 56245f),
                "Mach interpolation M=0.3, h=0, Pa=50 -> 56245 N",
                report, ref passed, ref failed);

            // Altitude midpoint between 0 and 3048 m at M=.4, maximum power.
            MavThrustDeckResult altitudeMid = MavF16Tp1538ThrustDeck.EvaluateFrozen(
                MavThrustDeckQuery.Create(1524f, 0.4f, 100f));
            Record(altitudeMid.valid && Near(altitudeMid.thrustN, 87981.5f),
                "altitude interpolation h=1524, M=.4, Pa=100 -> 87981.5 N",
                report, ref passed, ref failed);

            // Interior 2D point. Oracle is calculated explicitly from the four published Tmil
            // corners, then power-blended with the corresponding Tidle plane at Pa=25.
            float fh = 1000f / 3048f;
            float fm = (0.35f - 0.2f) / 0.2f;
            float idle00 = Lerp(2824f, 267f, fm);
            float idle10 = Lerp(1890f, 111f, fm);
            float idle2d = Lerp(idle00, idle10, fh);
            float mil00 = Lerp(56401f, 56089f, fm);
            float mil10 = Lerp(40699f, 41420f, fm);
            float mil2d = Lerp(mil00, mil10, fh);
            float expected = Lerp(idle2d, mil2d, 0.5f);

            MavThrustDeckResult interior = MavF16Tp1538ThrustDeck.EvaluateFrozen(
                MavThrustDeckQuery.Create(1000f, 0.35f, 25f));
            Record(interior.valid && Near(interior.thrustN, expected, 0.1f),
                "interior h/M bilinear interpolation + Pa blend", report, ref passed, ref failed);
        }

        private static void ValidateRefusalAndStaleThrust(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[D3] Fail-closed domain and stale-value rejection");

            float[] badAltitudes = { -1f, 15240.1f };
            for (int i = 0; i < badAltitudes.Length; i++)
            {
                MavThrustDeckResult r = MavF16Tp1538ThrustDeck.EvaluateFrozen(
                    MavThrustDeckQuery.Create(badAltitudes[i], 0.4f, 50f));
                Record(!r.valid && r.thrustN == 0f && !r.insideEnvelope,
                    "reject altitude " + badAltitudes[i], report, ref passed, ref failed);
            }

            float[] badMach = { 0.199f, 1.001f };
            for (int i = 0; i < badMach.Length; i++)
            {
                MavThrustDeckResult r = MavF16Tp1538ThrustDeck.EvaluateFrozen(
                    MavThrustDeckQuery.Create(3048f, badMach[i], 50f));
                Record(!r.valid && r.thrustN == 0f,
                    "reject Mach " + badMach[i], report, ref passed, ref failed);
            }

            float[] badPower = { -0.01f, 100.01f };
            for (int i = 0; i < badPower.Length; i++)
            {
                MavThrustDeckResult r = MavF16Tp1538ThrustDeck.EvaluateFrozen(
                    MavThrustDeckQuery.Create(3048f, 0.4f, badPower[i]));
                Record(!r.valid && r.thrustN == 0f,
                    "reject actual power " + badPower[i], report, ref passed, ref failed);
            }

            float[] nonFinite =
            {
                float.NaN, float.PositiveInfinity, float.NegativeInfinity
            };
            for (int i = 0; i < nonFinite.Length; i++)
            {
                MavThrustDeckResult a = MavF16Tp1538ThrustDeck.EvaluateFrozen(
                    MavThrustDeckQuery.Create(nonFinite[i], 0.4f, 50f));
                MavThrustDeckResult m = MavF16Tp1538ThrustDeck.EvaluateFrozen(
                    MavThrustDeckQuery.Create(3048f, nonFinite[i], 50f));
                MavThrustDeckResult p = MavF16Tp1538ThrustDeck.EvaluateFrozen(
                    MavThrustDeckQuery.Create(3048f, 0.4f, nonFinite[i]));
                Record(!a.valid && a.thrustN == 0f, "reject non-finite altitude", report, ref passed, ref failed);
                Record(!m.valid && m.thrustN == 0f, "reject non-finite Mach", report, ref passed, ref failed);
                Record(!p.valid && p.thrustN == 0f, "reject non-finite power", report, ref passed, ref failed);
            }

            MavThrustDeckResult valid = MavF16Tp1538ThrustDeck.EvaluateFrozen(
                MavThrustDeckQuery.Create(3048f, 0.4f, 100f));
            MavThrustDeckResult invalid = MavF16Tp1538ThrustDeck.EvaluateFrozen(
                MavThrustDeckQuery.Create(3048f, 1.1f, 100f));
            Record(valid.valid && valid.thrustN == 74993f
                   && !invalid.valid && invalid.thrustN == 0f,
                "valid->invalid query cannot retain last-known thrust",
                report, ref passed, ref failed);
        }

        private static void ValidatePoweredReferenceIntegration(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[D4] Separate powered-reference rig / shared propulsion integration");

            // Reuse the existing in-memory structural builder without changing its historical 5C-R
            // semantics. Build() must first produce the old null-deck/unpowered configuration; this
            // NEW validation scenario then opts into TP-1538 explicitly for its own lifetime only.
            MavF16ReferenceRigBuilder.Rig rig =
                MavF16ReferenceRigBuilder.Build("Mav_F16_TP1538_POWERED_REFERENCE_Validation", 32);
            try
            {
                bool startedHistoricalNullDeck = rig.propulsion != null
                    && rig.propulsion.thrustDeck == null;
                bool configured = rig.propulsion.ConfigureTp1538SourcedInstallation();

                Record(startedHistoricalNullDeck,
                    "historical 5C-R builder remains null-deck before powered-reference opt-in",
                    report, ref passed, ref failed);

                rig.propulsion.ResetEngineState(1f);

                MavFlightState state = new MavFlightState();
                state.worldPositionM = new Vector3(0f, 3048f, 0f);
                state.mach = 0.4f;
                MavAtmosphereSample atmosphere = new MavAtmosphereSample();

                MavThrustDeckResult direct = MavF16Tp1538ThrustDeck.EvaluateFrozen(
                    MavThrustDeckQuery.Create(3048f, 0.4f, 100f));
                MavPropulsiveLoads loads = rig.propulsion.Evaluate(state, atmosphere, 1f, 0f);

                Record(configured && rig.propulsion.RuntimeCount == 1,
                    "one F-16 engine runtime configured", report, ref passed, ref failed);
                Record(rig.sixDoF != null
                       && ReferenceEquals(rig.sixDoF.propulsionModel, rig.propulsion)
                       && rig.recorder.CountLegacyPhysicalWriters() == 0,
                    "isolated rig keeps MavSixDoFBody as the sole reference physical writer",
                    report, ref passed, ref failed);
                Record(direct.valid && Near(direct.thrustN, 74993f),
                    "direct sourced evaluator returns reference thrust", report, ref passed, ref failed);
                Record(loads.contributingEngineCount == 1
                       && Near(loads.reportedThrustN, direct.thrustN)
                       && Near(loads.forceAeroBodyN.x, direct.thrustN)
                       && Near(loads.forceAeroBodyN.y, 0f)
                       && Near(loads.forceAeroBodyN.z, 0f),
                    "shared runtime reports exactly the sourced axial thrust once",
                    report, ref passed, ref failed);
                Record(loads.momentAeroBodyNm == Vector3.zero,
                    "single centerline F-16 installation creates no fake asymmetric yaw moment",
                    report, ref passed, ref failed);
                Record(loads.hasAuthoritativeData && rig.propulsion.HasAuthoritativeData,
                    "source authority survives into propulsion telemetry",
                    report, ref passed, ref failed);
                Record(!rig.propulsion.IsAcceptableForLiveFlight,
                    "sourced thrust alone does not bypass remaining full-aircraft live-readiness gates",
                    report, ref passed, ref failed);

                MavFlightDynamicsLoadSet set = new MavFlightDynamicsLoadSet();
                set.BeginStep(1);
                bool first = set.AddPropulsive(loads);
                bool second = set.AddPropulsive(loads);
                Record(first && !second && set.propulsiveContributions == 2
                       && Near(set.totalForceAeroBodyN.x, loads.forceAeroBodyN.x),
                    "load set applies one propulsive contribution and rejects a duplicate",
                    report, ref passed, ref failed);

                state.mach = 1.1f;
                MavPropulsiveLoads invalid = rig.propulsion.Evaluate(state, atmosphere, 1f, 0f);
                Record(invalid.reportedThrustN == 0f
                       && invalid.forceAeroBodyN == Vector3.zero
                       && !invalid.hasAuthoritativeData,
                    "out-of-envelope runtime clears thrust and cannot masquerade as valid",
                    report, ref passed, ref failed);
            }
            finally
            {
                MavF16ReferenceRigBuilder.Destroy(rig);
            }
        }

        private static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }

        private static bool Near(float actual, float expected, float tolerance = ToleranceN)
        {
            return Mathf.Abs(actual - expected) <= tolerance;
        }

        private static void Record(
            bool ok, string name, StringBuilder report, ref int passed, ref int failed)
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
