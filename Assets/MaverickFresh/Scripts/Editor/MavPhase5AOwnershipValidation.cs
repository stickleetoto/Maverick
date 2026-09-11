#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using MaverickFresh.FlightDynamics;

namespace MaverickFresh.EditorTools
{
    /// <summary>
    /// Phase 5A validation: ownership foundation and shadow mode.
    ///
    /// The four gates the issue sets for this phase:
    ///
    ///   - shadow mode produces zero live Rigidbody side effects
    ///   - the ownership scan can prove which components may write force/torque
    ///   - replacement activation is refused if legacy physical writers remain active
    ///   - a failed handover rolls back to Legacy without partial ownership
    ///
    /// Every check here is on the RULES, which are pure static functions, plus a source scan. What it
    /// cannot do is prove the gate is wired into every write site at runtime - that needs Unity, and it
    /// is listed as such rather than implied.
    /// </summary>
    public static class MavPhase5AOwnershipValidation
    {
        [MenuItem("Maverick/Flight Dynamics/Run Phase 5A Ownership Validation")]
        public static void RunValidation()
        {
            int passed = 0;
            int failed = 0;
            StringBuilder report = new StringBuilder(8192);
            report.AppendLine("Maverick PHASE 5A OWNERSHIP + SHADOW Validation");
            report.AppendLine("==============================================");

            ValidateOwnershipRules(report, ref passed, ref failed);
            ValidateReadinessRules(report, ref passed, ref failed);
            ValidateWriterCoverage(report, ref passed, ref failed);
            ValidateWriterCategories(report, ref passed, ref failed);
            ValidateSingleArmingAuthority(report, ref passed, ref failed);
            ValidateRuntimeWiring(report, ref passed, ref failed);
            ValidateDiagnosticText(report, ref passed, ref failed);
            ValidateRegressionSafety(report, ref passed, ref failed);

            report.AppendLine();
            report.Append("RESULT: ").Append(failed == 0 ? "PASS" : "FAIL")
                .Append("  passed=").Append(passed).Append(" failed=").Append(failed);

            if (failed == 0)
                Debug.Log(report.ToString());
            else
                Debug.LogError(report.ToString());
        }

        // ================================================================== [O] rules

        private static void ValidateOwnershipRules(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[O] Ownership rules");

            // Legacy mode must be exactly today's behaviour.
            Record(MavFlightPhysicsOwnership.IsLegacyPhysicsAllowed(MavFlightPhysicsOwner.Legacy),
                "Legacy mode permits legacy physics, so installing the gate changes nothing",
                report, ref passed, ref failed);
            Record(!MavFlightPhysicsOwnership.IsReplacementPhysicsAllowed(MavFlightPhysicsOwner.Legacy),
                "and does not permit the replacement stack to write",
                report, ref passed, ref failed);

            // Shadow: legacy still flies, replacement computes only. This is the phase's core claim.
            Record(MavFlightPhysicsOwnership.IsLegacyPhysicsAllowed(MavFlightPhysicsOwner.Shadow),
                "Shadow mode leaves legacy physics in charge of the live aircraft",
                report, ref passed, ref failed);
            Record(!MavFlightPhysicsOwnership.IsReplacementPhysicsAllowed(MavFlightPhysicsOwner.Shadow),
                "and the replacement stack may NOT write - zero live Rigidbody side effects",
                report, ref passed, ref failed);

            // Replacement: legacy fully gated.
            Record(!MavFlightPhysicsOwnership.IsLegacyPhysicsAllowed(MavFlightPhysicsOwner.F16Replacement),
                "F16Replacement gates every legacy physical writer (FDM-OWN-002)",
                report, ref passed, ref failed);
            Record(MavFlightPhysicsOwnership.IsReplacementPhysicsAllowed(MavFlightPhysicsOwner.F16Replacement),
                "and grants the replacement stack ownership",
                report, ref passed, ref failed);

            // Fault fails closed toward the stack that was flying.
            Record(MavFlightPhysicsOwnership.IsLegacyPhysicsAllowed(MavFlightPhysicsOwner.Fault),
                "Fault leaves legacy flying the aircraft rather than nothing flying it",
                report, ref passed, ref failed);
            Record(!MavFlightPhysicsOwnership.IsReplacementPhysicsAllowed(MavFlightPhysicsOwner.Fault),
                "and holds the replacement stack off",
                report, ref passed, ref failed);

            // FDM-OWN-001: exactly one owner, in every mode, with no exceptions.
            bool exclusive = true;
            string offender = "none";
            MavFlightPhysicsOwner[] modes =
                (MavFlightPhysicsOwner[])System.Enum.GetValues(typeof(MavFlightPhysicsOwner));

            for (int i = 0; i < modes.Length; i++)
            {
                if (MavFlightPhysicsOwnership.ViolatesExclusiveOwnership(modes[i]))
                {
                    exclusive = false;
                    offender = modes[i].ToString();
                    break;
                }
            }

            Record(exclusive,
                "FDM-OWN-001 holds in EVERY mode: legacy and replacement are never both permitted "
                + "(offender: " + offender + ")",
                report, ref passed, ref failed);

            // And no mode leaves nothing able to fly the aircraft.
            bool alwaysSomeone = true;
            for (int i = 0; i < modes.Length; i++)
            {
                if (!MavFlightPhysicsOwnership.IsLegacyPhysicsAllowed(modes[i])
                    && !MavFlightPhysicsOwnership.IsReplacementPhysicsAllowed(modes[i]))
                {
                    alwaysSomeone = false;
                    break;
                }
            }

            Record(alwaysSomeone,
                "and no mode leaves NEITHER owner able to write, which would be an aircraft with no "
                + "physics at all",
                report, ref passed, ref failed);
        }

        // ================================================================== [R] readiness

        private static void ValidateReadinessRules(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[R] Replacement readiness refuses on every missing condition");

            string error;
            Record(!MavReplacementReadiness.NotReady().IsReady(out error),
                "a default readiness struct is NOT ready: " + error,
                report, ref passed, ref failed);

            Record(FullyReady().IsReady(out error),
                "a fully satisfied readiness struct is ready",
                report, ref passed, ref failed);

            // Knock out one condition at a time. Each must block on its own, or a missing condition
            // could be masked by the others being satisfied.
            MavReplacementReadiness r;

            r = FullyReady(); r.aircraftIdentityAuthoritative = false;
            Record(!r.IsReady(out error),
                "unresolved aircraft identity blocks activation (FDM-OWN-009)",
                report, ref passed, ref failed);

            r = FullyReady(); r.aircraftIsF16C = false;
            Record(!r.IsReady(out error),
                "a non-F-16 authoritative aircraft blocks activation",
                report, ref passed, ref failed);

            r = FullyReady(); r.sixDoFBodyPresent = false;
            Record(!r.IsReady(out error),
                "a missing six-DoF body blocks activation (FDM-OWN-007)",
                report, ref passed, ref failed);

            r = FullyReady(); r.aeroModelReady = false;
            Record(!r.IsReady(out error),
                "an unready aero model blocks activation",
                report, ref passed, ref failed);

            r = FullyReady(); r.controlLawReady = false;
            Record(!r.IsReady(out error),
                "an unready control law blocks activation (FDM-OWN-004)",
                report, ref passed, ref failed);

            r = FullyReady(); r.actuatorReady = false;
            Record(!r.IsReady(out error),
                "an unready actuator blocks activation (FDM-OWN-006)",
                report, ref passed, ref failed);

            r = FullyReady(); r.propulsionAcceptable = false;
            Record(!r.IsReady(out error),
                "unacceptable propulsion blocks activation rather than being fabricated (FDM-OWN-008)",
                report, ref passed, ref failed);

            r = FullyReady(); r.gravityOwnedExactlyOnce = false;
            Record(!r.IsReady(out error),
                "gravity not owned exactly once blocks activation",
                report, ref passed, ref failed);

            r = FullyReady(); r.shadowTelemetryFinite = false;
            Record(!r.IsReady(out error),
                "a shadow path that has not produced finite telemetry blocks activation",
                report, ref passed, ref failed);

            r = FullyReady(); r.shadowRunLongEnough = false;
            Record(!r.IsReady(out error),
                "a shadow path that has not run long enough blocks activation",
                report, ref passed, ref failed);

            Record(FullyReady().Describe().Contains("OwnershipValid=true"),
                "a ready struct describes itself as valid",
                report, ref passed, ref failed);
            Record(MavReplacementReadiness.NotReady().Describe().Contains("OwnershipValid=false"),
                "and an unready one does not claim success",
                report, ref passed, ref failed);
        }

        private static MavReplacementReadiness FullyReady()
        {
            MavReplacementReadiness r = new MavReplacementReadiness();
            r.aircraftIdentityAuthoritative = true;
            r.aircraftIsF16C = true;
            r.sixDoFBodyPresent = true;
            r.aeroModelReady = true;
            r.controlLawReady = true;
            r.actuatorReady = true;
            r.propulsionAcceptable = true;
            r.gravityOwnedExactlyOnce = true;
            r.shadowTelemetryFinite = true;
            r.shadowRunLongEnough = true;
            return r;
        }

        // ================================================================== [W] writer coverage

        private static void ValidateWriterCoverage(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[W] Every legacy force writer is registered with the gate");

            string reason;
            MavPhysicsWriterScanResult scan = MavPhase5WriterScan.Scan(Application.dataPath, out reason);

            Record(scan.sourcesAvailable,
                "the script sources are readable for scanning (" + reason + ")",
                report, ref passed, ref failed);

            if (!scan.sourcesAvailable)
                return;

            report.Append("        scanned ").Append(scan.filesScanned)
                .Append(" files, ").Append(scan.writerFiles.Count)
                .AppendLine(" carry live Rigidbody writes");

            for (int i = 0; i < scan.writerFiles.Count; i++)
            {
                MavPhysicsWriterFile f = scan.writerFiles[i];
                report.Append("        ").Append(f.gated ? "gated    " : "UNGATED  ")
                    .Append(f.fileName).Append("  writes=").Append(f.writeSites)
                    .Append(" gateChecks=").Append(f.gateChecks)
                    .AppendLine();
            }

            Record(scan.ungatedPlayerWriters.Count == 0,
                "no component that can write player physics does so without consulting the ownership "
                + "gate (" + scan.ungatedPlayerWriters.Count + " ungated)",
                report, ref passed, ref failed);

            for (int i = 0; i < scan.ungatedPlayerWriters.Count; i++)
                report.Append("        UNGATED: ").AppendLine(scan.ungatedPlayerWriters[i]);

            // The scanner must actually detect, or a clean result proves nothing.
            Record(MavPhase5WriterScan.IsLiveWriteSite("            rb.AddForce(f, ForceMode.Force);"),
                "the scanner detects a real AddForce call",
                report, ref passed, ref failed);
            Record(!MavPhase5WriterScan.IsLiveWriteSite("        /// never call AddForce here"),
                "and a documentation comment mentioning AddForce is not a false positive",
                report, ref passed, ref failed);
            Record(MavPhase5WriterScan.IsGateCheck("            if (!LegacyPhysicsAllowed())"),
                "and it recognises a gate check",
                report, ref passed, ref failed);
        }

        // ================================================================== [C] categories

        /// <summary>
        /// State writers, separated by category.
        ///
        /// "All physics writers are gated" is only an honest sentence if the things it does not cover
        /// are named. A setup-only pose write, a per-step aerodynamic force, a weapon recoil impulse and
        /// a component that mutates another component's drag coefficient are four different problems,
        /// and one combined number would hide three of them.
        /// </summary>
        private static void ValidateWriterCategories(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[C] State writers by category");

            string reason;
            MavPhysicsWriterScanResult scan = MavPhase5WriterScan.Scan(Application.dataPath, out reason);
            if (!scan.sourcesAvailable)
            {
                Record(false, "sources readable for scanning (" + reason + ")",
                    report, ref passed, ref failed);
                return;
            }

            MavWriterCategory[] categories =
                (MavWriterCategory[])System.Enum.GetValues(typeof(MavWriterCategory));

            for (int c = 0; c < categories.Length; c++)
            {
                System.Collections.Generic.List<MavPhysicsWriterFile> files =
                    scan.InCategory(categories[c]);

                report.Append("        ").Append(categories[c].ToString())
                    .Append(": ").Append(files.Count).AppendLine(" file(s)");

                for (int i = 0; i < files.Count; i++)
                {
                    report.Append("            ").Append(files[i].gated ? "gated   " : "ungated ")
                        .Append(files[i].fileName)
                        .Append("  writes=").Append(files[i].writeSites)
                        .AppendLine();
                }
            }

            Record(scan.CountInCategory(MavWriterCategory.PerStepPhysical) == 4,
                "exactly 4 per-step physical player writers - jet, aero body, atmospheric engine, TVC "
                + "(got " + scan.CountInCategory(MavWriterCategory.PerStepPhysical) + ")",
                report, ref passed, ref failed);

            bool allGated = true;
            System.Collections.Generic.List<MavPhysicsWriterFile> perStep =
                scan.InCategory(MavWriterCategory.PerStepPhysical);
            for (int i = 0; i < perStep.Count; i++)
            {
                if (!perStep[i].gated)
                    allGated = false;
            }

            Record(allGated,
                "and every one of them consults the ownership authority",
                report, ref passed, ref failed);

            Record(scan.CountInCategory(MavWriterCategory.TransitionOrSetupPose) == 2,
                "2 transition/setup-only pose writers, reported separately and NOT claimed as gated - "
                + "Phase 5C must convert these into an explicit state handover",
                report, ref passed, ref failed);
            Record(scan.CountInCategory(MavWriterCategory.ScopedExemption) == 2,
                "2 scoped exemptions: weapon recoil and landing-gear drag, both out of scope and both "
                + "named rather than ignored",
                report, ref passed, ref failed);
            Record(scan.CountInCategory(MavWriterCategory.IndirectParameterMutator) == 2,
                "2 indirect parameter mutators, neither writing Rigidbody state: MavCombatFlapSystem "
                + "mutates MavAeroBody coefficients, and MavManeuverDiagnostics substitutes the pilot "
                + "command while a scripted Phase 5B case runs",
                report, ref passed, ref failed);
            Record(scan.CountInCategory(MavWriterCategory.NotPlayerBody) == 3,
                "3 writers that only touch non-player Rigidbodies",
                report, ref passed, ref failed);

            string why;
            Record(MavPhase5WriterScan.ClassifyFile("MavSomethingBrandNew.cs", out why)
                   == MavWriterCategory.PerStepPhysical,
                "an UNCLASSIFIED writer fails closed into the per-step category, so a new writer must "
                + "be gated until somebody deliberately classifies it",
                report, ref passed, ref failed);
        }

        // ================================================================== [S] single authority

        /// <summary>
        /// Exactly one component may arm MavSixDoFBody.
        ///
        /// This is Blocker 2. Two independent authorities for one fact is the bug class that produced
        /// the F-22-instead-of-F-16 failure, and an ownership-foundation phase is the wrong place to
        /// leave one standing.
        /// </summary>
        private static void ValidateSingleArmingAuthority(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[S] Exactly one arming authority");

            string root = global::System.IO.Path.Combine(Application.dataPath, "MaverickFresh/Scripts");
            if (!global::System.IO.Directory.Exists(root))
            {
                Record(false, "scripts root readable", report, ref passed, ref failed);
                return;
            }

            // Both spellings: the authority uses the ArmedForLiveFlight contract, while scenes and the
            // Phase 2/3 suites still read the underlying simulationEnabled flag. Checking only one of
            // them is how this check went vacuous once and passed by finding nothing.
            string[] armingForms = { "simulationEnabled = true", "ArmedForLiveFlight = " };

            string[] files = global::System.IO.Directory.GetFiles(root, "*.cs", global::System.IO.SearchOption.AllDirectories);
            System.Collections.Generic.List<string> granters =
                new System.Collections.Generic.List<string>();

            for (int f = 0; f < files.Length; f++)
            {
                string name = global::System.IO.Path.GetFileName(files[f]);
                if (name.Contains("Validation") || name.Contains("Scan"))
                    continue;

                // MavSixDoFBody declares the property; its own setter is the mechanism, not a rival.
                if (name == "MavSixDoFBody.cs")
                    continue;

                string[] lines = global::System.IO.File.ReadAllLines(files[f]);
                for (int i = 0; i < lines.Length; i++)
                {
                    string code = MavPhase5WriterScan.StripCommentsAndStringLiterals(lines[i]);
                    for (int k = 0; k < armingForms.Length; k++)
                    {
                        if (code.Contains(armingForms[k]) && !granters.Contains(name))
                            granters.Add(name);
                    }
                }
            }

            report.Append("        files able to arm the replacement body: ")
                .AppendLine(granters.Count == 0 ? "none" : string.Join(", ", granters.ToArray()));

            Record(granters.Count == 1,
                "EXACTLY one file can arm the replacement body - not zero, which would mean this check "
                + "found nothing, and not two, which is the blocker (" + granters.Count + ")",
                report, ref passed, ref failed);
            Record(granters.Count == 1 && granters[0] == "MavFlightPhysicsOwnership.cs",
                "and it is MavFlightPhysicsOwnership, the declared authority",
                report, ref passed, ref failed);

            string ctrlPath = global::System.IO.Path.Combine(root, "FlightDynamics/Core/MavPhysicsOwnershipController.cs");
            if (global::System.IO.File.Exists(ctrlPath))
            {
                string ctrl = global::System.IO.File.ReadAllText(ctrlPath);
                Record(ctrl.Contains("TryRequestReplacementArming"),
                    "MavPhysicsOwnershipController requests arming instead of deciding it",
                    report, ref passed, ref failed);
                Record(ctrl.Contains("armingAuthority == null"),
                    "and fails closed when no authority is present",
                    report, ref passed, ref failed);
                Record(ctrl.Contains("r.shadowTelemetryFinite = false"),
                    "and reports shadow readiness as false, which it cannot observe - so it cannot "
                    + "satisfy the gate on its own",
                    report, ref passed, ref failed);
            }
        }

        // ================================================================== [T] runtime wiring

        /// <summary>
        /// The authority is installed by the real startup path, not by hand.
        ///
        /// This is Blocker 1. Infrastructure that has to be added manually is not part of the runtime;
        /// it is a thing somebody might remember.
        /// </summary>
        private static void ValidateRuntimeWiring(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[T] Runtime wiring on the real startup path");

            string bootPath = global::System.IO.Path.Combine(
                Application.dataPath, "MaverickFresh/Scripts/MavFreshBootstrap.cs");

            if (!global::System.IO.File.Exists(bootPath))
            {
                Record(false, "MavFreshBootstrap.cs readable", report, ref passed, ref failed);
                return;
            }

            string boot = global::System.IO.File.ReadAllText(bootPath);

            Record(boot.Contains("EnsureFlightPhysicsOwnership();"),
                "MavFreshBootstrap installs the ownership authority during SetupFreshMouseFlight",
                report, ref passed, ref failed);

            int atIdentity = boot.IndexOf(
                "EnsureAuthoritativeAircraftApplied();", System.StringComparison.Ordinal);
            int atOwnership = boot.IndexOf(
                "EnsureFlightPhysicsOwnership();", System.StringComparison.Ordinal);
            int atPolish = boot.IndexOf("if (installWTPolish)", System.StringComparison.Ordinal);

            Record(atIdentity >= 0 && atOwnership > atIdentity,
                "after the aircraft identity is authoritative, so the readiness contract has an "
                + "aircraft to ask about (0851f4f ordering preserved)",
                report, ref passed, ref failed);
            Record(atPolish < 0 || atOwnership < atPolish,
                "and before the aircraft-aware WT polish block",
                report, ref passed, ref failed);
            Record(boot.Contains("existing.Length > 1"),
                "and it detects more than one authority and faults rather than picking one",
                report, ref passed, ref failed);
            Record(boot.Contains("AttachGovernedBody("),
                "and hands the authority the body it governs at runtime rather than via a serialized "
                + "reference that can go stale",
                report, ref passed, ref failed);
        }

        // ================================================================== [D] diagnostics

        private static void ValidateDiagnosticText(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[D] Ownership diagnostic enumerates owner and active writers");

            MavWriterReport[] reports = new MavWriterReport[2];
            reports[0].name = "MavAeroBody";
            reports[0].kind = MavLegacyWriterKind.Aerodynamic;
            reports[0].wroteLastStep = true;
            reports[0].lastStepIndex = 41;
            reports[1].name = "MavMouseFlightJet";
            reports[1].kind = MavLegacyWriterKind.Other;
            reports[1].wroteLastStep = false;
            reports[1].lastStepIndex = 12;

            string legacyText = MavFlightPhysicsOwnership.DescribeOwnership(
                MavFlightPhysicsOwner.Legacy, "default", reports, 41);

            Record(legacyText.Contains("CurrentOwner=Legacy"),
                "the diagnostic names the current owner",
                report, ref passed, ref failed);
            Record(legacyText.Contains("LegacyForceWriters=1"),
                "and counts the writers that actually wrote, not the ones that exist",
                report, ref passed, ref failed);
            Record(legacyText.Contains("ACTIVE") && legacyText.Contains("MavAeroBody"),
                "and names them",
                report, ref passed, ref failed);
            Record(legacyText.Contains("ExclusiveOwnershipHeld=true"),
                "and states whether the exclusivity invariant holds",
                report, ref passed, ref failed);

            MavWriterReport[] none = new MavWriterReport[0];
            string replacementText = MavFlightPhysicsOwnership.DescribeOwnership(
                MavFlightPhysicsOwner.F16Replacement, "handover complete", none, 100);

            Record(replacementText.Contains("LegacyForceWriters=0")
                   && replacementText.Contains("ReplacementPhysicsAllowed=true"),
                "and in replacement mode reports LegacyForceWriters=0 with replacement permitted",
                report, ref passed, ref failed);
        }

        // ================================================================== [REG]

        private static void ValidateRegressionSafety(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[REG] 0851f4f identity and Phase 4B tuning preserved");

            MavAircraftRuntimeProfile f16 = MavAircraftCatalog.GetBuiltIn(MavAircraftKind.F16C);
            Record(f16 != null && f16.aircraft == MavAircraftKind.F16C && f16.aircraftId == "f16c",
                "F-16 canonical identity intact",
                report, ref passed, ref failed);
            Record(f16 != null && Mathf.Abs(f16.mass - 9800f) < 0.5f
                   && Mathf.Abs(f16.wingArea - 27.9f) < 0.05f && !f16.useThrustVectorControl,
                "and its identifying figures untouched (9800 kg, 27.9 m^2, no TVC)",
                report, ref passed, ref failed);

            Record(f16 != null && f16.usePhase4BTurnDynamics
                   && Mathf.Abs(f16.aeroBlend - 0.95f) < 1e-4f
                   && Mathf.Abs(f16.liftBlend - 0.92f) < 1e-4f
                   && Mathf.Abs(f16.gravityBlend - 1.0f) < 1e-4f,
                "Phase 4B turn tuning preserved exactly (aeroBlend 0.95, liftBlend 0.92, gravity 1.0)",
                report, ref passed, ref failed);
            Record(f16 != null && f16.useAeroStaticStability
                   && Mathf.Abs(f16.releaseRateNullingScale - 0.35f) < 1e-4f,
                "and so are static stability and the release rate-nulling scale",
                report, ref passed, ref failed);

            Record(Mathf.Abs(MavAeroBody.ComputeLegacyVelocityAssistScale(true, 0.54f) - 0.46f) < 1e-4f,
                "the Phase 4A ownership rule still answers as it did",
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
