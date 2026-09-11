#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MaverickFresh.EditorTools
{
    /// <summary>
    /// Validation for the ownership of aoaPitchReduction.
    ///
    /// WHY THIS EXISTS SEPARATELY. The Phase 4B validation checked that the F-16 catalog contained
    /// 0.05 and passed, while the limiter that actually runs used 0.45 for the entire life of the
    /// build. Asserting a constant is in a file proves nothing about the runtime. So every check here
    /// is about the value arriving at MavInstructorController - the component that applies the
    /// limiter - and the probe is built on a real GameObject so GetComponent resolves the way it does
    /// in play mode.
    /// </summary>
    public static class MavAoALimiterOwnershipValidation
    {
        private const float F16Expected = 0.05f;
        private const float GenericDefault = 0.45f;

        [MenuItem("Maverick/Flight Dynamics/Run AoA Limiter Ownership Validation")]
        public static void RunValidation()
        {
            int passed = 0;
            int failed = 0;
            StringBuilder report = new StringBuilder(4096);
            report.AppendLine("Maverick aoaPitchReduction OWNERSHIP Validation");
            report.AppendLine("==============================================");

            ValidateProfileIsSourceOfTruth(report, ref passed, ref failed);
            ValidateEndToEnd(report, ref passed, ref failed);
            ValidateSynchronisationDirection(report, ref passed, ref failed);
            ValidateTelemetry(report, ref passed, ref failed);

            report.AppendLine();
            report.AppendLine("RESULT: " + (failed == 0 ? "PASS" : "FAIL")
                              + "  passed=" + passed + " failed=" + failed);

            if (failed == 0)
                Debug.Log(report.ToString());
            else
                Debug.LogError(report.ToString());
        }

        private static void Record(bool ok, string what, StringBuilder report,
                                   ref int passed, ref int failed)
        {
            if (ok) passed++; else failed++;
            report.Append(ok ? "  PASS  " : "  FAIL  ").AppendLine(what);
        }

        // ----------------------------------------------------------------------------------
        private static void ValidateProfileIsSourceOfTruth(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P] The aircraft profile is the source of truth");

            MavAircraftRuntimeProfile f16 = MavAircraftCatalog.GetBuiltIn(MavAircraftKind.F16C);
            Record(f16 != null, "the F-16C profile resolves", report, ref passed, ref failed);
            if (f16 == null)
                return;

            Record(Mathf.Approximately(f16.aoaPitchReduction, F16Expected),
                "F-16C profile value is 0.05 (got " + f16.aoaPitchReduction.ToString("F3") + ")",
                report, ref passed, ref failed);

            MavAircraftRuntimeProfile f22 = MavAircraftCatalog.GetBuiltIn(MavAircraftKind.F22A);
            Record(f22 != null && Mathf.Approximately(f22.aoaPitchReduction, GenericDefault),
                "and a non-F-16 profile still resolves to the generic 0.45, so this patch is not a "
                + "global handling change",
                report, ref passed, ref failed);
        }

        // ----------------------------------------------------------------------------------
        /// <summary>
        /// The real applier, on a real GameObject, in the editor. This is the check that would have
        /// caught the original defect.
        /// </summary>
        private static void ValidateEndToEnd(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[E] End to end: does the resolved value reach the consumer");

            GameObject probe = new GameObject("MavAoAOwnershipProbe");
            try
            {
                probe.AddComponent<Rigidbody>();
                MavMouseFlightJet jet = probe.AddComponent<MavMouseFlightJet>();
                MavInstructorController instructor = probe.AddComponent<MavInstructorController>();
                MavAircraftProfileApplier applier = probe.AddComponent<MavAircraftProfileApplier>();

                // Generic startup state, as the serialized defaults give it.
                jet.aoaPitchReduction = GenericDefault;
                instructor.aoaPitchReduction = GenericDefault;

                string error;
                bool ok = applier.TryApplyAircraft(MavAircraftKind.F16C, out error);
                Record(ok, "TryApplyAircraft(F16C) succeeds" + (ok ? "" : " (" + error + ")"),
                    report, ref passed, ref failed);

                Record(Mathf.Approximately(instructor.aoaPitchReduction, F16Expected),
                    "MavInstructorController - which APPLIES the limiter - holds 0.05 (got "
                    + instructor.aoaPitchReduction.ToString("F3") + ")",
                    report, ref passed, ref failed);
                Record(Mathf.Approximately(jet.aoaPitchReduction, F16Expected),
                    "and the jet mirror agrees (got " + jet.aoaPitchReduction.ToString("F3") + ")",
                    report, ref passed, ref failed);
                Record(applier.HasAuthoritativeAircraft
                       && applier.AppliedAircraft == MavAircraftKind.F16C,
                    "and the canonical identity from 0851f4f is untouched",
                    report, ref passed, ref failed);

                // The per-FixedUpdate synchronisation must not undo it.
                for (int i = 0; i < 20; i++)
                    instructor.CopyTuningToJet(jet);

                Record(Mathf.Approximately(instructor.aoaPitchReduction, F16Expected),
                    "20 FixedUpdate synchronisations do not restore 0.45 on the authority (got "
                    + instructor.aoaPitchReduction.ToString("F3") + ")",
                    report, ref passed, ref failed);

                // And a late generic push cannot either.
                instructor.CopyTuningFromJet(jet);
                Record(Mathf.Approximately(instructor.aoaPitchReduction, F16Expected),
                    "and a late jet->instructor push cannot either",
                    report, ref passed, ref failed);
            }
            finally
            {
                Object.DestroyImmediate(probe);
            }
        }

        // ----------------------------------------------------------------------------------
        private static void ValidateSynchronisationDirection(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[S] Synchronisation is one-directional: authority -> mirror");

            GameObject probe = new GameObject("MavAoADirectionProbe");
            try
            {
                probe.AddComponent<Rigidbody>();
                MavMouseFlightJet jet = probe.AddComponent<MavMouseFlightJet>();
                MavInstructorController instructor = probe.AddComponent<MavInstructorController>();

                instructor.aoaPitchReduction = F16Expected;   // authority
                jet.aoaPitchReduction = GenericDefault;       // deliberately stale mirror

                instructor.CopyTuningFromJet(jet);
                Record(Mathf.Approximately(instructor.aoaPitchReduction, F16Expected),
                    "a stale mirror cannot flow back into the authority (got "
                    + instructor.aoaPitchReduction.ToString("F3") + ")",
                    report, ref passed, ref failed);

                instructor.CopyTuningToJet(jet);
                Record(Mathf.Approximately(jet.aoaPitchReduction, F16Expected),
                    "and the authority refreshes the mirror (got "
                    + jet.aoaPitchReduction.ToString("F3") + ")",
                    report, ref passed, ref failed);

                // The one setter must reach the consumer.
                jet.SetAoAPitchReductionAuthority(0.21f);
                Record(Mathf.Approximately(instructor.aoaPitchReduction, 0.21f),
                    "SetAoAPitchReductionAuthority writes the consumer, not just the mirror (got "
                    + instructor.aoaPitchReduction.ToString("F3") + ")",
                    report, ref passed, ref failed);

                // Other fields in the jet->instructor copy must still propagate, or this would be a
                // regression wearing a fix's clothes.
                jet.gPitchReduction = 0.19f;
                instructor.CopyTuningFromJet(jet);
                Record(Mathf.Approximately(instructor.gPitchReduction, 0.19f),
                    "while every other field in that copy still propagates",
                    report, ref passed, ref failed);
            }
            finally
            {
                Object.DestroyImmediate(probe);
            }
        }

        // ----------------------------------------------------------------------------------
        private static void ValidateTelemetry(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[T] Limiter telemetry separates base value from bypass");

            GameObject probe = new GameObject("MavAoATelemetryProbe");
            try
            {
                MavInstructorController instructor = probe.AddComponent<MavInstructorController>();

                Record(Mathf.Approximately(instructor.manualEnvelopeBypassFactor, 0.65f),
                    "manualEnvelopeBypassFactor is still 0.65 - this patch changed ownership, not "
                    + "tuning (got " + instructor.manualEnvelopeBypassFactor.ToString("F3") + ")",
                    report, ref passed, ref failed);

                // The three telemetry channels exist and start in a safe, meaningful state.
                Record(Mathf.Approximately(instructor.debugEffectiveLimiterReduction, 1f),
                    "effective limiter reduction starts at 1, i.e. 'no limiting applied yet' rather "
                    + "than 0",
                    report, ref passed, ref failed);
                Record(!instructor.debugAoALimiterEngaged,
                    "and the limiter reports itself disengaged before anything has flown",
                    report, ref passed, ref failed);
            }
            finally
            {
                Object.DestroyImmediate(probe);
            }

            report.AppendLine();
            report.AppendLine("  NOTE: pitchOverride is set only from KEYBOARD pitch input");
            report.AppendLine("  (MavInstructorController line ~1111). Mouse aim never sets it, so the");
            report.AppendLine("  manual bypass does not apply to a normal mouse-flight pull. Watch");
            report.AppendLine("  debugManualBypassFactor in play mode to confirm per control mode.");
        }
    }
}
#endif
