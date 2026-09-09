using System.Text;
using UnityEngine;
using MaverickFresh.FlightDynamics.F16;

namespace MaverickFresh.FlightDynamics.Validation
{
    /// <summary>
    /// Deterministic, side-effect-free regression checks for the isolated F-16 reference model.
    /// These checks are intentionally independent of AeroBench runtime code.
    /// Expected coefficient vectors are frozen from the NASA Morelli v0.1 equations.
    /// </summary>
    public static class MavF16ReferenceValidation
    {
        private const float CoefficientTolerance = 5e-6f;
        private const float MassToleranceKg = 0.05f;
        private const float InertiaToleranceKgM2 = 0.25f;
        private const float AngleToleranceDeg = 0.001f;

        public static string RunAll(out int passed, out int failed)
        {
            passed = 0;
            failed = 0;
            StringBuilder report = new StringBuilder(2048);
            report.AppendLine("Maverick F-16 Reference Validation");
            report.AppendLine("================================");

            ValidateCoefficientVectors(report, ref passed, ref failed);
            ValidateReferenceGeometry(report, ref passed, ref failed);
            ValidateMassAndInertia(report, ref passed, ref failed);
            ValidateAxisRoundTrip(report, ref passed, ref failed);

            report.AppendLine();
            report.Append("RESULT: ")
                .Append(failed == 0 ? "PASS" : "FAIL")
                .Append("  passed=").Append(passed)
                .Append(" failed=").Append(failed);

            return report.ToString();
        }

        private static void ValidateCoefficientVectors(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[V0] Morelli coefficient regression vectors");

            ValidateVector(
                "COEFF-000 zero-state",
                0f, 0f, 0f, 0f, 0f,
                0f, 0f, 0f,
                new MavAeroCoefficients
                {
                    cx = -0.019433670f,
                    cy = 0f,
                    cz = -0.137827800f,
                    cl = 0f,
                    cm = -0.034076480f,
                    cn = 0f
                },
                report, ref passed, ref failed
            );

            ValidateVector(
                "COEFF-010 pitch/rate",
                10f, 0f, -5f, 0f, 0f,
                0f, 0.02f, 0f,
                new MavAeroCoefficients
                {
                    cx = 0.075518670f,
                    cy = 0f,
                    cz = -1.356031491f,
                    cl = 0f,
                    cm = -0.215115393f,
                    cn = 0f
                },
                report, ref passed, ref failed
            );

            ValidateVector(
                "COEFF-020 lateral",
                10f, 5f, 0f, 10f, -5f,
                0.03f, 0f, -0.02f,
                new MavAeroCoefficients
                {
                    cx = 0.034331362f,
                    cy = -0.117649055f,
                    cz = -0.768260057f,
                    cl = -0.056381778f,
                    cm = -0.088985246f,
                    cn = 0.036851210f
                },
                report, ref passed, ref failed
            );

            ValidateVector(
                "COEFF-030 high-alpha mixed",
                30f, -8f, 10f, -12f, 15f,
                -0.04f, 0.025f, 0.035f,
                new MavAeroCoefficients
                {
                    cx = 0.176577787f,
                    cy = 0.193763457f,
                    cz = -2.653633497f,
                    cl = 0.066140018f,
                    cm = -0.489592761f,
                    cn = -0.032537919f
                },
                report, ref passed, ref failed
            );
        }

        private static void ValidateVector(
            string name,
            float alphaDeg,
            float betaDeg,
            float elevatorDeg,
            float aileronDeg,
            float rudderDeg,
            float pHat,
            float qHat,
            float rHat,
            MavAeroCoefficients expected,
            StringBuilder report,
            ref int passed,
            ref int failed)
        {
            MavAeroCoefficients actual = MavF16MorelliPolynomial.Evaluate(
                alphaDeg * Mathf.Deg2Rad,
                betaDeg * Mathf.Deg2Rad,
                elevatorDeg * Mathf.Deg2Rad,
                aileronDeg * Mathf.Deg2Rad,
                rudderDeg * Mathf.Deg2Rad,
                pHat,
                qHat,
                rHat,
                MavF16MassReference.XcgCbar,
                MavF16MassReference.XcgReferenceCbar,
                MavF16MorelliReference.MeanAerodynamicChordM / MavF16MorelliReference.WingSpanM
            );

            bool ok =
                Near(actual.cx, expected.cx, CoefficientTolerance)
                && Near(actual.cy, expected.cy, CoefficientTolerance)
                && Near(actual.cz, expected.cz, CoefficientTolerance)
                && Near(actual.cl, expected.cl, CoefficientTolerance)
                && Near(actual.cm, expected.cm, CoefficientTolerance)
                && Near(actual.cn, expected.cn, CoefficientTolerance);

            Record(ok, name, report, ref passed, ref failed);
            if (!ok)
            {
                report.Append("    expected ").Append(Format(expected)).AppendLine();
                report.Append("    actual   ").Append(Format(actual)).AppendLine();
            }
        }

        private static void ValidateReferenceGeometry(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[V1] Reference geometry");

            Record(Near(MavF16MorelliReference.WingAreaM2, 27.870912f, 1e-6f), "S = 27.870912 m^2", report, ref passed, ref failed);
            Record(Near(MavF16MorelliReference.WingSpanM, 9.144f, 1e-6f), "b = 9.144 m", report, ref passed, ref failed);
            Record(Near(MavF16MorelliReference.MeanAerodynamicChordM, 3.450336f, 1e-6f), "cbar = 3.450336 m", report, ref passed, ref failed);
            Record(Near(MavF16MassReference.XcgCbar, 0.25f, 1e-7f), "xcg = 0.25 cbar", report, ref passed, ref failed);
            Record(Near(MavF16MassReference.XcgReferenceCbar, 0.35f, 1e-7f), "xcgRef = 0.35 cbar", report, ref passed, ref failed);
        }

        private static void ValidateMassAndInertia(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[V1] Mass / inertia transform");

            MavMassProperties properties = MavF16MassReference.CreateUnityMassProperties(Vector3.zero);

            Record(Near(properties.massKg, 9298.6512f, MassToleranceKg), "mass conversion", report, ref passed, ref failed);
            Record(Near(properties.inertiaTensorKgM2.x, 75673.6231f, InertiaToleranceKgM2), "principal Ix(Unity X)", report, ref passed, ref failed);
            Record(Near(properties.inertiaTensorKgM2.y, 85576.4953f, InertiaToleranceKgM2), "principal Iy", report, ref passed, ref failed);
            Record(Near(properties.inertiaTensorKgM2.z, 12850.4646f, InertiaToleranceKgM2), "principal Iz", report, ref passed, ref failed);
            Record(Near(properties.inertiaTensorRotationEulerDeg.x, 1.0491624f, AngleToleranceDeg), "principal-axis rotation", report, ref passed, ref failed);

            float sourceTrace = MavF16MassReference.IxKgM2 + MavF16MassReference.IyKgM2 + MavF16MassReference.IzKgM2;
            float principalTrace = properties.inertiaTensorKgM2.x + properties.inertiaTensorKgM2.y + properties.inertiaTensorKgM2.z;
            Record(Near(sourceTrace, principalTrace, 0.5f), "inertia trace preserved", report, ref passed, ref failed);
        }

        private static void ValidateAxisRoundTrip(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[V1] Axis conversion round-trip");

            Vector3 unity = new Vector3(3.25f, -7.5f, 11.75f);
            Vector3 aero = MavFlightDynamicsMath.UnityLocalVectorToAeroBody(unity);
            Vector3 roundTrip = MavFlightDynamicsMath.AeroBodyVectorToUnityLocal(aero);
            Record((roundTrip - unity).sqrMagnitude <= 1e-10f, "Unity -> aero -> Unity vector", report, ref passed, ref failed);

            Vector3 aeroMoment = new Vector3(2f, -3f, 4f);
            Vector3 unityMoment = MavFlightDynamicsMath.AeroBodyMomentToUnityLocal(aeroMoment);
            Vector3 recovered = MavFlightDynamicsMath.UnityLocalAngularRateToAeroBody(unityMoment);
            Record((recovered - aeroMoment).sqrMagnitude <= 1e-10f, "aero moment axis mapping round-trip", report, ref passed, ref failed);
        }

        private static bool Near(float actual, float expected, float tolerance)
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

        private static string Format(MavAeroCoefficients c)
        {
            return string.Format(
                "CX={0:F9} CY={1:F9} CZ={2:F9} Cl={3:F9} Cm={4:F9} Cn={5:F9}",
                c.cx, c.cy, c.cz, c.cl, c.cm, c.cn
            );
        }
    }
}
