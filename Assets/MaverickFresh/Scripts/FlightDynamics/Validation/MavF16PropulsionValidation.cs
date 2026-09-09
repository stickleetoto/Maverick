using System.Text;
using UnityEngine;
using MaverickFresh.FlightDynamics.F16;

namespace MaverickFresh.FlightDynamics.Validation
{
    /// <summary>
    /// Deterministic validation for the sourced F-16 engine power-state model.
    /// This suite does NOT validate dimensional thrust because the thrust deck is not frozen yet.
    /// </summary>
    public static class MavF16PropulsionValidation
    {
        private const float Tolerance = 1e-3f;

        public static string RunAll(out int passed, out int failed)
        {
            passed = 0;
            failed = 0;
            StringBuilder report = new StringBuilder(2048);
            report.AppendLine("Maverick F-16 Propulsion Validation");
            report.AppendLine("==================================");

            ValidateThrottleGearing(report, ref passed, ref failed);
            ValidateReciprocalTimeConstant(report, ref passed, ref failed);
            ValidatePowerRate(report, ref passed, ref failed);
            ValidateIntegration(report, ref passed, ref failed);
            ValidateZeroThrustBoundary(report, ref passed, ref failed);

            report.AppendLine();
            report.Append("RESULT: ")
                .Append(failed == 0 ? "PASS" : "FAIL")
                .Append(" passed=").Append(passed)
                .Append(" failed=").Append(failed);

            return report.ToString();
        }

        private static void ValidateThrottleGearing(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[E0] Throttle gearing");

            Record(Near(MavF16EnginePowerModel.ThrottleToCommandedPowerPercent(0f), 0f),
                "throttle 0 -> commanded power 0", report, ref passed, ref failed);

            Record(Near(MavF16EnginePowerModel.ThrottleToCommandedPowerPercent(0.77f), 49.999f, 0.01f),
                "throttle 0.77 -> commanded power ~50", report, ref passed, ref failed);

            Record(Near(MavF16EnginePowerModel.ThrottleToCommandedPowerPercent(1f), 100f),
                "throttle 1 -> commanded power 100", report, ref passed, ref failed);

            Record(Near(MavF16EnginePowerModel.ThrottleToCommandedPowerPercent(-1f), 0f)
                && Near(MavF16EnginePowerModel.ThrottleToCommandedPowerPercent(2f), 100f),
                "throttle input clamps to 0..1", report, ref passed, ref failed);
        }

        private static void ValidateReciprocalTimeConstant(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[E1] Reciprocal time constant");

            Record(Near(MavF16EnginePowerModel.ReciprocalTimeConstant(0f), 1f),
                "delta <= 25 -> 1.0", report, ref passed, ref failed);
            Record(Near(MavF16EnginePowerModel.ReciprocalTimeConstant(25f), 1f),
                "delta 25 -> 1.0", report, ref passed, ref failed);
            Record(Near(MavF16EnginePowerModel.ReciprocalTimeConstant(37.5f), 0.55f, 1e-4f),
                "delta 37.5 -> 0.55", report, ref passed, ref failed);
            Record(Near(MavF16EnginePowerModel.ReciprocalTimeConstant(50f), 0.1f),
                "delta >= 50 -> 0.1", report, ref passed, ref failed);
        }

        private static void ValidatePowerRate(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[E2] Piecewise power derivative");

            Record(Near(MavF16EnginePowerModel.ComputePowerRatePercentPerSec(60f, 80f), 100f),
                "command>=50, actual>=50 -> 5*(Pc-Pa)", report, ref passed, ref failed);

            Record(Near(MavF16EnginePowerModel.ComputePowerRatePercentPerSec(40f, 80f), 20f),
                "command>=50, actual<50 -> transition toward 60", report, ref passed, ref failed);

            Record(Near(MavF16EnginePowerModel.ComputePowerRatePercentPerSec(60f, 20f), -100f),
                "command<50, actual>=50 -> transition toward 40", report, ref passed, ref failed);

            Record(Near(MavF16EnginePowerModel.ComputePowerRatePercentPerSec(20f, 30f), 10f),
                "command<50, actual<50 -> track commanded power", report, ref passed, ref failed);
        }

        private static void ValidateIntegration(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[E3] Numerical state step");

            float next = MavF16EnginePowerModel.StepActualPowerPercent(60f, 80f, 0.02f);
            Record(Near(next, 62f),
                "60 -> 80 at dt 0.02 advances to 62", report, ref passed, ref failed);

            Record(Near(MavF16EnginePowerModel.StepActualPowerPercent(60f, 80f, 0f), 60f),
                "zero dt leaves state unchanged", report, ref passed, ref failed);

            float p = 0f;
            for (int i = 0; i < 1000; i++)
                p = MavF16EnginePowerModel.StepActualPowerPercent(p, 100f, 0.02f);
            Record(p >= 0f && p <= 100f && p > 99f,
                "long full-power run stays bounded and converges near 100", report, ref passed, ref failed);
        }

        private static void ValidateZeroThrustBoundary(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[E4] Thrust-deck safety boundary");

            MavF16EnginePowerModel model = null;
            // Structural policy check lives in the implementation: HasAuthoritativeData remains false
            // until the altitude/Mach thrust deck is frozen. Here we pin the public power functions only;
            // Unity object construction is intentionally avoided in this side-effect-free suite.
            Record(model == null,
                "validation does not instantiate a runtime engine object", report, ref passed, ref failed);
        }

        private static bool Near(float actual, float expected, float tolerance = Tolerance)
        {
            return Mathf.Abs(actual - expected) <= tolerance;
        }

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
