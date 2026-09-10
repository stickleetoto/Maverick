#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MaverickFresh.EditorTools
{
    /// <summary>
    /// Phase 4A validation and live diagnostics for the legacy-to-aero turn ownership migration.
    /// This does not modify the aircraft or add forces; it only checks the migration rule and
    /// reports the currently active turn stack while Play Mode is running.
    /// </summary>
    public static class MavTurnDynamicsMigrationValidation
    {
        [MenuItem("Maverick/Flight Dynamics/Run Phase 4A Turn Ownership Validation")]
        public static void RunValidation()
        {
            int passed = 0;
            int failed = 0;
            StringBuilder report = new StringBuilder(2048);
            report.AppendLine("Maverick Phase 4A TURN OWNERSHIP Validation");
            report.AppendLine("===========================================");

            CheckScale(false, 0.54f, 1f, "aero disabled leaves legacy velocity assist at full ownership", report, ref passed, ref failed);
            CheckScale(true, 0f, 1f, "0% aero -> 100% legacy velocity assist", report, ref passed, ref failed);
            CheckScale(true, 0.25f, 0.75f, "25% aero -> 75% legacy velocity assist", report, ref passed, ref failed);
            CheckScale(true, 0.54f, 0.46f, "F-22 Phase 4A split: 54% aero -> 46% legacy velocity assist", report, ref passed, ref failed);
            CheckScale(true, 1f, 0f, "100% aero -> 0% legacy velocity assist", report, ref passed, ref failed);
            CheckScale(true, -1f, 1f, "negative runtime blend is clamped safely", report, ref passed, ref failed);
            CheckScale(true, 2f, 0f, "over-range runtime blend is clamped safely", report, ref passed, ref failed);

            bool conserved = true;
            for (int i = 0; i <= 20; i++)
            {
                float aero = i / 20f;
                float legacy = MavAeroBody.ComputeLegacyVelocityAssistScale(true, aero);
                if (Mathf.Abs((aero + legacy) - 1f) > 1e-6f)
                {
                    conserved = false;
                    break;
                }
            }
            Record(conserved, "ownership is conserved across the full 0..1 blend: aero + legacy == 1", report, ref passed, ref failed);

            bool monotonic = true;
            float previous = 1f;
            for (int i = 1; i <= 20; i++)
            {
                float scale = MavAeroBody.ComputeLegacyVelocityAssistScale(true, i / 20f);
                if (scale > previous + 1e-6f)
                {
                    monotonic = false;
                    break;
                }
                previous = scale;
            }
            Record(monotonic, "legacy velocity assist never increases as aero ownership rises", report, ref passed, ref failed);

            report.AppendLine();
            report.Append("RESULT: ").Append(failed == 0 ? "PASS" : "FAIL")
                .Append("  passed=").Append(passed).Append(" failed=").Append(failed);

            if (failed == 0)
                Debug.Log(report.ToString());
            else
                Debug.LogError(report.ToString());

            EditorUtility.DisplayDialog(
                "Phase 4A Turn Ownership",
                (failed == 0 ? "PASS" : "FAIL") + "\n\n" + passed + " passed, " + failed + " failed.",
                "OK"
            );
        }

        [MenuItem("Maverick/Flight Dynamics/Report Current Turn Dynamics")]
        public static void ReportCurrentTurnDynamics()
        {
            MavMouseFlightJet jet = Object.FindObjectOfType<MavMouseFlightJet>();
            if (jet == null)
            {
                Debug.LogWarning("[Maverick/Turn] No active MavMouseFlightJet found.");
                return;
            }

            MavAeroBody aero = jet.GetComponent<MavAeroBody>();
            MavAircraftProfileApplier profile = jet.GetComponent<MavAircraftProfileApplier>();
            Rigidbody rb = jet.GetComponent<Rigidbody>();

            float aeroOwnership = aero != null && aero.useAeroBody ? Mathf.Clamp01(aero.aeroBlend) : 0f;
            float legacyScale = aero != null ? aero.GetVelocityAssistScale() : 1f;
            float actualVelocityAssistGate = jet.useVelocityTurnAssist
                ? legacyScale * Mathf.Clamp01(jet.velocityTurnAssistCurrentFactor)
                : 0f;

            Vector3 localRateDeg = rb != null
                ? jet.transform.InverseTransformDirection(rb.angularVelocity) * Mathf.Rad2Deg
                : Vector3.zero;

            StringBuilder report = new StringBuilder(2048);
            report.AppendLine("Maverick CURRENT TURN DYNAMICS");
            report.AppendLine("==============================");
            report.Append("aircraft          : ").AppendLine(profile != null ? profile.lastApplied : "unknown");
            report.Append("speed             : ").Append(jet.speed.ToString("F1")).AppendLine(" m/s");
            report.Append("bank              : ").Append(jet.signedBankAngle.ToString("F1")).AppendLine(" deg");
            report.Append("AoA / AoS         : ").Append(jet.aoaEstimateDeg.ToString("F1")).Append(" / ")
                .Append(jet.aosEstimateDeg.ToString("F1")).AppendLine(" deg");
            report.Append("G estimate        : ").Append(jet.gEstimate.ToString("F2")).AppendLine(" g");
            report.Append("body p/q/r        : ").Append(localRateDeg.x.ToString("F1")).Append(" / ")
                .Append(localRateDeg.y.ToString("F1")).Append(" / ")
                .Append(localRateDeg.z.ToString("F1")).AppendLine(" deg/s");
            report.Append("aero ownership    : ").Append((100f * aeroOwnership).ToString("F1")).AppendLine(" %");
            report.Append("legacy vel scale  : ").Append((100f * legacyScale).ToString("F1")).AppendLine(" %");
            report.Append("assist gate now   : ").Append((100f * actualVelocityAssistGate).ToString("F1")).AppendLine(" %");

            if (aero != null)
            {
                report.Append("aero lift / drag  : ").Append(aero.debugLiftG.ToString("F2")).Append(" / ")
                    .Append(aero.debugDragG.ToString("F2")).AppendLine(" g");
                report.Append("gravity blend     : ").Append(aero.gravityBlend.ToString("F2")).AppendLine(" g multiplier");
                report.Append("ownership sum     : ").Append(aero.debugTurnOwnershipSum.ToString("F3")).AppendLine();
            }

            report.Append("direct torque mode: ").AppendLine(jet.debugTorqueMode);
            report.Append("manual input      : ").AppendLine(jet.debugManualInputActive ? "yes" : "no");
            report.Append("semi-aero active  : ").AppendLine(jet.debugSemiAeroActive ? "yes" : "no");

            Debug.Log(report.ToString(), jet);
        }

        private static void CheckScale(
            bool aeroEnabled,
            float aeroBlend,
            float expected,
            string name,
            StringBuilder report,
            ref int passed,
            ref int failed)
        {
            float actual = MavAeroBody.ComputeLegacyVelocityAssistScale(aeroEnabled, aeroBlend);
            Record(Mathf.Abs(actual - expected) < 1e-6f,
                name + " (actual=" + actual.ToString("F3") + ")",
                report, ref passed, ref failed);
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
#endif
