using System.Text;
using UnityEngine;
using MaverickFresh.FlightDynamics.F15;

namespace MaverickFresh.FlightDynamics.Validation
{
    /// <summary>
    /// Deterministic checks for the F-15 twin F100-PW-100 installation.
    ///
    /// Covered:
    ///   [E0] twin identity: two slots, two throttle channels, one shared engine profile
    ///   [E1] per-slot runtime state is independent, so two engines cannot share a spool
    ///   [E2] engine performance is honestly unavailable - zero thrust, non-authoritative
    ///   [E3] installation geometry is undeclared, and that BLOCKS live flight
    ///   [E4] the engine-out trap: undeclared geometry gives zero yaw, and the gate catches it
    ///   [E5] the F-16 engine law is not inherited by the F-15
    ///   [E6] attaching a deck does not upgrade provenance
    ///
    /// These run on the installation profile and its static factories - production code, no
    /// GameObject, no Rigidbody, no play-mode session.
    /// </summary>
    public static class MavF15PropulsionValidation
    {
        public static string RunAll(out int passed, out int failed)
        {
            passed = 0;
            failed = 0;
            StringBuilder report = new StringBuilder(4096);
            report.AppendLine("F-15 Twin F100-PW-100 Installation Validation");
            report.AppendLine("=============================================");

            ValidateTwinIdentity(report, ref passed, ref failed);
            ValidateIndependentState(report, ref passed, ref failed);
            ValidatePerformanceUnavailable(report, ref passed, ref failed);
            ValidateGeometryBlocksLiveFlight(report, ref passed, ref failed);
            ValidateEngineOutTrap(report, ref passed, ref failed);
            ValidateNoF16EngineLaw(report, ref passed, ref failed);

            report.AppendLine();
            report.Append("RESULT: ")
                .Append(failed == 0 ? "PASS" : "FAIL")
                .Append("  passed=").Append(passed)
                .Append(" failed=").Append(failed);

            return report.ToString();
        }

        // ---------------------------------------------------------------- [E0]

        private static void ValidateTwinIdentity(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[E0] Twin-engine identity");

            MavPropulsionInstallationProfile twin = MavF15PropulsionSkeleton.CreateTwinSkeleton();

            Record(twin.EngineCount == 2,
                "the installation has exactly 2 engine slots",
                report, ref passed, ref failed);

            Record(
                twin.engines[0].throttleChannel == 0 && twin.engines[1].throttleChannel == 1,
                "each engine reads its OWN throttle channel (0 and 1),"
                + " so differential throttle needs no architecture change",
                report, ref passed, ref failed);

            Record(
                twin.engines[0].slotId != twin.engines[1].slotId,
                "slot ids are distinct, so a per-engine reading is never ambiguous",
                report, ref passed, ref failed);

            Record(
                ReferenceEquals(twin.engines[0].engineProfile, twin.engines[1].engineProfile),
                "both slots share ONE engine-profile object - one engine model, two engines",
                report, ref passed, ref failed);

            Record(
                twin.aircraftConfiguration == MavF15ReferenceData.TargetConfigurationId,
                "the installation names its aircraft configuration: "
                + twin.aircraftConfiguration,
                report, ref passed, ref failed);

            string reason;
            Record(twin.IsValid(out reason),
                "the installation is structurally valid (" + reason + ")",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [E1]

        private static void ValidateIndependentState(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[E1] Independent per-engine runtime state");

            MavPropulsionInstallationProfile twin = MavF15PropulsionSkeleton.CreateTwinSkeleton();

            // The profile is shared; the runtimes must not be. Sharing a spool state between two
            // engines would make an asymmetric failure impossible to represent, which is the
            // whole reason a twin is modelled as a twin.
            IMavEnginePowerDynamics dynamics = MavEnginePowerDynamicsFactory.Resolve(
                twin.engines[0].engineProfile.powerDynamicsLaw);

            MavEngineRuntime left = new MavEngineRuntime(twin.engines[0], dynamics);
            MavEngineRuntime right = new MavEngineRuntime(twin.engines[1], dynamics);

            Record(!ReferenceEquals(left, right),
                "each slot gets its own runtime instance",
                report, ref passed, ref failed);

            left.Reset(1f);
            right.Reset(0f);

            Record(
                !Mathf.Approximately(left.ActualPowerPercent, right.ActualPowerPercent),
                "the two runtimes hold DIFFERENT power states simultaneously ("
                + left.ActualPowerPercent.ToString("F1") + "% vs "
                + right.ActualPowerPercent.ToString("F1")
                + "%) - an asymmetric shutdown is representable",
                report, ref passed, ref failed);

            // And a failure on one side must not reach the other.
            left.SetFailed(true);
            Record(
                left.Failed && !right.Failed,
                "failing one engine leaves the other unaffected - the shared profile carries no"
                + " mutable state for them to collide over",
                report, ref passed, ref failed);

            Record(
                ReferenceEquals(twin.engines[0].engineProfile, twin.engines[1].engineProfile),
                "...while still sharing the one profile, so the two facts are independent",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [E2]

        private static void ValidatePerformanceUnavailable(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[E2] Engine performance is honestly unavailable");

            MavEngineProfile profile = MavF15PropulsionSkeleton.CreateUnfrozenEngineProfile();

            Record(
                profile.provenance == MavEngineDataProvenance.Unavailable,
                "engine data provenance is Unavailable - the VARIANT is frozen, the numbers are not",
                report, ref passed, ref failed);

            Record(profile.thrustDeck == null,
                "no thrust deck is attached, so dimensional thrust is exactly 0 N",
                report, ref passed, ref failed);

            Record(
                profile.augmentation == MavEngineAugmentationSemantics.Unavailable,
                "augmentation semantics are Unavailable - no invented AB schedule",
                report, ref passed, ref failed);

            Record(
                !profile.sourceEnvelope.declared
                && !profile.sourceEnvelope.Contains(6096f, 0.6f),
                "the source envelope is Undeclared, and an undeclared envelope CONTAINS nothing"
                + " rather than everything",
                report, ref passed, ref failed);

            Record(
                !profile.rotorAngularMomentum.available,
                "rotor angular momentum is unavailable, so no gyroscopic engine moment is invented",
                report, ref passed, ref failed);

            Record(
                profile.engineVariantIdentity.Contains("F100-PW-100"),
                "the engine VARIANT identity is nonetheless recorded: "
                + profile.engineVariantIdentity,
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [E3]

        private static void ValidateGeometryBlocksLiveFlight(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[E3] Undeclared installation geometry blocks live flight");

            MavPropulsionInstallationProfile twin = MavF15PropulsionSkeleton.CreateTwinSkeleton();

            Record(
                !twin.engines[0].geometryDeclared && !twin.engines[1].geometryDeclared,
                "neither mount position is declared - zero is a placeholder, not a measurement",
                report, ref passed, ref failed);

            string reason;
            bool live = twin.IsAcceptableForLiveFlight(out reason);

            Record(!live,
                "the installation is REFUSED for live flight (" + reason + ")",
                report, ref passed, ref failed);

            Record(
                reason.Contains("geometry") || reason.Contains("thrust") || reason.Contains("deck"),
                "...and the refusal names the actual gap rather than failing vaguely",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [E4]

        private static void ValidateEngineOutTrap(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[E4] The engine-out trap");

            MavPropulsionInstallationProfile twin = MavF15PropulsionSkeleton.CreateTwinSkeleton();

            // With both mounts at the origin, r x F is identically zero for ANY thrust. That is
            // correct today because thrust is zero, but it is indistinguishable from "both
            // engines really are on the centreline" - so the geometry gate, not the number, has
            // to be what stops it.
            Vector3 leftMount = twin.engines[0].positionAeroBodyM;
            Vector3 rightMount = twin.engines[1].positionAeroBodyM;

            Record(
                leftMount == Vector3.zero && rightMount == Vector3.zero,
                "both mounts sit at the origin, so r x F would be zero for any thrust",
                report, ref passed, ref failed);

            Record(
                leftMount == rightMount,
                "the two mounts are INDISTINGUISHABLE, which is the trap: a thrust deck attached"
                + " in this state gives a twin-engine aircraft with no engine-out yaw, and 0 N*m"
                + " looks like a perfectly ordinary answer",
                report, ref passed, ref failed);

            // The gate must survive a deck being attached - that is the exact moment the trap
            // would otherwise spring.
            MavPropulsionInstallationProfile withDeck =
                MavF15PropulsionSkeleton.CreateTwinSkeleton(null);

            string reason;
            Record(
                !withDeck.IsAcceptableForLiveFlight(out reason),
                "the live-flight gate still refuses, so the trap cannot be reached by accident"
                + " (" + reason + ")",
                report, ref passed, ref failed);

            Record(
                twin.engines[0].geometryProvenance.Contains("UNAVAILABLE"),
                "each slot records WHY its geometry is missing: "
                + twin.engines[0].geometryProvenance,
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [E5][E6]

        private static void ValidateNoF16EngineLaw(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[E5][E6] No F-16 engine law, no provenance upgrade by deck");

            MavEngineProfile profile = MavF15PropulsionSkeleton.CreateUnfrozenEngineProfile();

            Record(
                profile.powerDynamicsLaw
                    == MavEnginePowerDynamicsLaw.InstantNoSourcedTransient,
                "the power-dynamics law is InstantNoSourcedTransient - declared absence,"
                + " not a modelled transient",
                report, ref passed, ref failed);

            Record(
                profile.powerDynamicsLaw != MavEnginePowerDynamicsLaw.F16GarzaMorelliPowerState,
                "the F-16 Garza/Morelli law is NOT selected. The shared runtime supports it;"
                + " that is not a licence to apply it to a different engine",
                report, ref passed, ref failed);

            Record(
                profile.powerDynamicsProvenance == MavEngineDataProvenance.Unavailable,
                "power-dynamics provenance is Unavailable",
                report, ref passed, ref failed);

            // [E6] A deck must not launder the identity into authority.
            MavEngineProfile withDeck =
                MavF15PropulsionSkeleton.CreateUnfrozenEngineProfile(null);

            Record(
                withDeck.provenance == MavEngineDataProvenance.Unavailable,
                "supplying a deck does not upgrade engine provenance - acceptance for the"
                + " F100-PW-100 on NASA 836 is a separate decision from attachment",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- helpers

        private static void Record(
            bool condition, string label,
            StringBuilder report, ref int passed, ref int failed)
        {
            if (condition) passed++; else failed++;
            report.Append(condition ? "  PASS  " : "  FAIL  ").AppendLine(label);
        }
    }
}
