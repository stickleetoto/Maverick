#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MaverickFresh.EditorTools
{
    /// <summary>
    /// Phase 5B.0b validation: ownership of the four envelope-protection settings.
    ///
    ///     aoaSoftLimitDeg, aoaHardLimitDeg, softGLimit, hardGLimit
    ///
    /// WHAT COUNTS AS EVIDENCE. Not that the catalog contains a number - that test already existed for
    /// aoaPitchReduction and passed for the whole life of a build in which the limiter used a different
    /// value. Every check here asks whether MavInstructorController, the component that enforces the
    /// envelope, ends up holding the resolved aircraft value, and it asks that through the real
    /// profile applier on a real GameObject.
    ///
    /// ONE HONEST NOTE CARRIED IN THE OUTPUT. The F-16 catalog does not override softGLimit or
    /// hardGLimit, so those resolve to the shared profile defaults, which happen to equal the
    /// instructor defaults. Fixing their ownership therefore changes no behaviour today. It removes a
    /// latent defect, and the checks below say so rather than implying a behavioural win.
    /// </summary>
    public static class MavEnvelopeProtectionOwnershipValidation
    {
        private const float F16AoASoft = 22f;
        private const float F16AoAHard = 30f;
        private const float GenericAoASoft = 24f;
        private const float GenericAoAHard = 34f;

        [MenuItem("Maverick/Flight Dynamics/Run Envelope Protection Ownership Validation")]
        public static void RunValidation()
        {
            int passed = 0;
            int failed = 0;
            StringBuilder report = new StringBuilder(6144);
            report.AppendLine("Maverick ENVELOPE PROTECTION OWNERSHIP Validation");
            report.AppendLine("================================================");

            ValidateProfileValues(report, ref passed, ref failed);
            ValidateEndToEnd(report, ref passed, ref failed);
            ValidateInitialisationOrder(report, ref passed, ref failed);
            ValidateBackChannelsRemoved(report, ref passed, ref failed);
            ValidateMirrorConsumers(report, ref passed, ref failed);

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

        private static string N(float v)
        {
            return v.ToString("F2");
        }

        // ------------------------------------------------------------------ [P]
        private static void ValidateProfileValues(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P] The F-16C profile values, recorded exactly as they are");

            MavAircraftRuntimeProfile f16 = MavAircraftCatalog.GetBuiltIn(MavAircraftKind.F16C);
            Record(f16 != null, "the F-16C profile resolves", report, ref passed, ref failed);
            if (f16 == null)
                return;

            report.AppendLine("        aoaSoftLimitDeg = " + N(f16.aoaSoftLimitDeg)
                              + "   aoaHardLimitDeg = " + N(f16.aoaHardLimitDeg));
            report.AppendLine("        softGLimit      = " + N(f16.softGLimit)
                              + "    hardGLimit      = " + N(f16.hardGLimit));

            Record(Mathf.Approximately(f16.aoaSoftLimitDeg, F16AoASoft),
                "aoaSoftLimitDeg is 22 and was not adjusted by this patch",
                report, ref passed, ref failed);
            Record(Mathf.Approximately(f16.aoaHardLimitDeg, F16AoAHard),
                "aoaHardLimitDeg is 30 and was not adjusted by this patch",
                report, ref passed, ref failed);

            // Honest about what the G limits are: profile defaults, not an F-16 decision.
            Record(Mathf.Approximately(f16.softGLimit, 8.8f)
                   && Mathf.Approximately(f16.hardGLimit, 11.2f),
                "softGLimit/hardGLimit are the shared profile defaults 8.8/11.2 - the F-16 block does "
                + "NOT override them, so their ownership fix is latent-defect removal, not a "
                + "behaviour change",
                report, ref passed, ref failed);
        }

        // ------------------------------------------------------------------ [E]
        private static void ValidateEndToEnd(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[E] End to end through the real profile applier");

            GameObject probe = new GameObject("MavEnvelopeOwnershipProbe");
            try
            {
                probe.AddComponent<Rigidbody>();
                MavMouseFlightJet jet = probe.AddComponent<MavMouseFlightJet>();
                MavInstructorController instructor = probe.AddComponent<MavInstructorController>();
                MavAircraftProfileApplier applier = probe.AddComponent<MavAircraftProfileApplier>();

                // Generic startup state.
                instructor.aoaSoftLimitDeg = GenericAoASoft;
                instructor.aoaHardLimitDeg = GenericAoAHard;
                instructor.softGLimit = 8.8f;
                instructor.hardGLimit = 11.2f;
                jet.aoaSoftLimitDeg = GenericAoASoft;
                jet.aoaHardLimitDeg = GenericAoAHard;

                string error;
                bool ok = applier.TryApplyAircraft(MavAircraftKind.F16C, out error);
                Record(ok, "TryApplyAircraft(F16C) succeeds" + (ok ? "" : " (" + error + ")"),
                    report, ref passed, ref failed);

                MavAircraftRuntimeProfile f16 =
                    MavAircraftCatalog.GetBuiltIn(MavAircraftKind.F16C);

                Record(Mathf.Approximately(instructor.aoaSoftLimitDeg, F16AoASoft),
                    "instructor aoaSoftLimitDeg = 22, was 24 (got "
                    + N(instructor.aoaSoftLimitDeg) + ")", report, ref passed, ref failed);
                Record(Mathf.Approximately(instructor.aoaHardLimitDeg, F16AoAHard),
                    "instructor aoaHardLimitDeg = 30, was 34 (got "
                    + N(instructor.aoaHardLimitDeg) + ")", report, ref passed, ref failed);
                Record(Mathf.Approximately(instructor.softGLimit, f16.softGLimit),
                    "instructor softGLimit matches the profile exactly (got "
                    + N(instructor.softGLimit) + ")", report, ref passed, ref failed);
                Record(Mathf.Approximately(instructor.hardGLimit, f16.hardGLimit),
                    "instructor hardGLimit matches the profile exactly (got "
                    + N(instructor.hardGLimit) + ")", report, ref passed, ref failed);

                Record(Mathf.Approximately(jet.aoaSoftLimitDeg, F16AoASoft)
                       && Mathf.Approximately(jet.aoaHardLimitDeg, F16AoAHard)
                       && Mathf.Approximately(jet.softGLimit, f16.softGLimit)
                       && Mathf.Approximately(jet.hardGLimit, f16.hardGLimit),
                    "and all four jet mirrors agree with the authority",
                    report, ref passed, ref failed);

                Record(applier.HasAuthoritativeAircraft
                       && applier.AppliedAircraft == MavAircraftKind.F16C,
                    "and the canonical identity from 0851f4f is untouched",
                    report, ref passed, ref failed);

                // The G limits need a DISTINGUISHABLE value to be tested behaviourally.
                //
                // Every aircraft's softGLimit/hardGLimit equal the instructor defaults, so a check
                // that just applied a stock profile would pass whether the propagation worked or not -
                // it would be measuring a coincidence. Applying a profile whose G limits differ makes
                // the propagation observable. TryGetBuiltIn builds fresh profiles per call, so
                // mutating this copy cannot leak into another check.
                MavAircraftRuntimeProfile tweaked =
                    MavAircraftCatalog.GetBuiltIn(MavAircraftKind.F16C);
                tweaked.softGLimit = 6.5f;
                tweaked.hardGLimit = 9.5f;

                string applyError;
                bool applied = applier.TryApplyProfile(tweaked, out applyError);
                Record(applied,
                    "a profile with distinguishable G limits applies"
                    + (applied ? "" : " (" + applyError + ")"),
                    report, ref passed, ref failed);
                Record(Mathf.Approximately(instructor.softGLimit, 6.5f)
                       && Mathf.Approximately(instructor.hardGLimit, 9.5f),
                    "and the consumer receives them, so G-limit propagation is verified by behaviour "
                    + "and not only by reading the source (got " + N(instructor.softGLimit) + "/"
                    + N(instructor.hardGLimit) + ")",
                    report, ref passed, ref failed);
                Record(Mathf.Approximately(jet.softGLimit, 6.5f)
                       && Mathf.Approximately(jet.hardGLimit, 9.5f),
                    "and the mirrors follow",
                    report, ref passed, ref failed);
            }
            finally
            {
                Object.DestroyImmediate(probe);
            }
        }

        // ------------------------------------------------------------------ [O]
        private static void ValidateInitialisationOrder(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[O] Initialisation order: generic -> authoritative -> steady state");

            GameObject probe = new GameObject("MavEnvelopeOrderProbe");
            try
            {
                probe.AddComponent<Rigidbody>();
                MavMouseFlightJet jet = probe.AddComponent<MavMouseFlightJet>();
                MavInstructorController instructor = probe.AddComponent<MavInstructorController>();
                MavAircraftProfileApplier applier = probe.AddComponent<MavAircraftProfileApplier>();

                // (a) generic defaults, and the pre-profile jet->instructor push at bootstrap :194.
                jet.aoaSoftLimitDeg = GenericAoASoft;
                jet.aoaHardLimitDeg = GenericAoAHard;
                instructor.CopyTuningFromJet(jet);
                Record(Mathf.Approximately(instructor.aoaSoftLimitDeg, GenericAoASoft),
                    "generic defaults may legitimately start at 24/34",
                    report, ref passed, ref failed);

                // (b) authoritative apply.
                string error;
                applier.TryApplyAircraft(MavAircraftKind.F16C, out error);
                Record(Mathf.Approximately(instructor.aoaSoftLimitDeg, F16AoASoft)
                       && Mathf.Approximately(instructor.aoaHardLimitDeg, F16AoAHard),
                    "after authoritative F-16 apply the consumer holds 22/30",
                    report, ref passed, ref failed);

                // (c) repeated per-step synchronisation.
                for (int i = 0; i < 50; i++)
                    instructor.CopyTuningToJet(jet);
                Record(Mathf.Approximately(instructor.aoaSoftLimitDeg, F16AoASoft)
                       && Mathf.Approximately(instructor.aoaHardLimitDeg, F16AoAHard),
                    "50 FixedUpdate synchronisations do not restore 24/34 (got "
                    + N(instructor.aoaSoftLimitDeg) + "/" + N(instructor.aoaHardLimitDeg) + ")",
                    report, ref passed, ref failed);

                // (d) a LATE generic push, e.g. a control profile pressed at runtime, with the mirror
                //     deliberately stale. This is the case the back-channel removal exists for.
                jet.aoaSoftLimitDeg = GenericAoASoft;
                jet.aoaHardLimitDeg = GenericAoAHard;
                jet.softGLimit = 1f;
                jet.hardGLimit = 2f;
                instructor.CopyTuningFromJet(jet);
                Record(Mathf.Approximately(instructor.aoaSoftLimitDeg, F16AoASoft)
                       && Mathf.Approximately(instructor.aoaHardLimitDeg, F16AoAHard),
                    "a late jet->instructor push with a stale mirror cannot restore 24/34 (got "
                    + N(instructor.aoaSoftLimitDeg) + "/" + N(instructor.aoaHardLimitDeg) + ")",
                    report, ref passed, ref failed);
                Record(Mathf.Approximately(instructor.softGLimit, 8.8f)
                       && Mathf.Approximately(instructor.hardGLimit, 11.2f),
                    "and cannot inject nonsense G limits either (got "
                    + N(instructor.softGLimit) + "/" + N(instructor.hardGLimit) + ")",
                    report, ref passed, ref failed);
            }
            finally
            {
                Object.DestroyImmediate(probe);
            }
        }

        // ------------------------------------------------------------------ [B]
        private static void ValidateBackChannelsRemoved(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[B] Back-channels removed, mirror direction retained");

            string path = global::System.IO.Path.Combine(
                Application.dataPath, "MaverickFresh/Scripts/MavInstructorController.cs");
            if (!global::System.IO.File.Exists(path))
            {
                Record(false, "MavInstructorController.cs readable", report, ref passed, ref failed);
                return;
            }

            string src = global::System.IO.File.ReadAllText(path);
            string[] fields = { "aoaSoftLimitDeg", "aoaHardLimitDeg", "softGLimit", "hardGLimit" };

            for (int i = 0; i < fields.Length; i++)
            {
                Record(!src.Contains(fields[i] + " = source." + fields[i] + ";"),
                    fields[i] + ": jet->instructor back-channel is gone",
                    report, ref passed, ref failed);
                Record(src.Contains("target." + fields[i] + " = " + fields[i] + ";"),
                    "    and authority->mirror still flows",
                    report, ref passed, ref failed);
            }

            // Every other field in that copy must still work, or this is a regression in disguise.
            Record(src.Contains("gPitchReduction = source.gPitchReduction;"),
                "and an untouched field such as gPitchReduction still copies both ways, so only the "
                + "four named settings changed direction",
                report, ref passed, ref failed);

            string applier = global::System.IO.File.ReadAllText(global::System.IO.Path.Combine(
                Application.dataPath, "MaverickFresh/Scripts/Aircraft/MavAircraftProfileApplier.cs"));
            for (int i = 0; i < fields.Length; i++)
            {
                Record(applier.Contains("Set(instructor, \"" + fields[i] + "\""),
                    fields[i] + ": the profile applier writes the consumer",
                    report, ref passed, ref failed);
            }
        }

        // ------------------------------------------------------------------ [M]
        private static void ValidateMirrorConsumers(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[M] Mirrors that are genuinely consumed keep working");

            GameObject probe = new GameObject("MavEnvelopeMirrorProbe");
            try
            {
                probe.AddComponent<Rigidbody>();
                MavMouseFlightJet jet = probe.AddComponent<MavMouseFlightJet>();
                MavInstructorController instructor = probe.AddComponent<MavInstructorController>();

                // MavPhysicalAIRewardLogger reads jet.hardGLimit, so that mirror is not decorative.
                instructor.hardGLimit = 9.5f;
                instructor.CopyTuningToJet(jet);
                Record(Mathf.Approximately(jet.hardGLimit, 9.5f),
                    "jet.hardGLimit tracks the authority, which matters because "
                    + "MavPhysicalAIRewardLogger reads it (got " + N(jet.hardGLimit) + ")",
                    report, ref passed, ref failed);

                // Each setter must reach the consumer, not just the mirror.
                jet.SetAoASoftLimitAuthority(17f);
                jet.SetAoAHardLimitAuthority(27f);
                jet.SetSoftGLimitAuthority(6f);
                jet.SetHardGLimitAuthority(10f);
                Record(Mathf.Approximately(instructor.aoaSoftLimitDeg, 17f)
                       && Mathf.Approximately(instructor.aoaHardLimitDeg, 27f)
                       && Mathf.Approximately(instructor.softGLimit, 6f)
                       && Mathf.Approximately(instructor.hardGLimit, 10f),
                    "all four setters write the consumer rather than only the mirror",
                    report, ref passed, ref failed);
            }
            finally
            {
                Object.DestroyImmediate(probe);
            }

            report.AppendLine();
            report.AppendLine("  NOTE: softGLimit (8.8) equals sustainedGLimit (8.8), so the");
            report.AppendLine("  soft-G branch in ApplyProtectionAssists - guarded by");
            report.AppendLine("  'g > softGLimit && g <= sustainedGLimit' - is unreachable as");
            report.AppendLine("  configured. Reported, not changed: altering it is tuning.");
            report.AppendLine("  sustainedGLimit is also not written by any aircraft profile at all.");
        }
    }
}
#endif
