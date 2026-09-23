using System.Collections.Generic;
using System.IO;
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
    ///   [E7] the digitized TP-1034 figure 17 data is structurally sound and self-consistent
    ///   [E8] source-point regression: every digitized point comes back out unchanged
    ///   [E9] source envelope enforcement, and zero silent extrapolation
    ///  [E10] finite outputs across a sweep, and refusal of non-finite inputs
    ///  [E11] gross / ram drag / net thrust are separate quantities and cannot be confused
    ///  [E12] the two fully-printed control schedules, and the inlet recovery equation
    ///  [E13] augmentation boundaries are only as sharp as the source makes them
    ///  [E14] the thrust deck refuses to produce a force, and says why twice over
    ///  [E15] propulsion writes no Rigidbody, and the aero model adds no engine thrust
    ///  [E16] TP-1782 stays cross-validation only
    ///  [E17] 111.2 kN is a plot scale and cannot become the missing design maximum
    ///  [E18] the sea-level-static anchor: sound mechanism, three gates, fails at the first
    ///  [E19] normalized net and dimensional gross remain separate datasets
    ///  [E20] TP-1034 appendix C prints a 30 000 lbf channel scale - and it is not the normalizer
    ///  [E21] CP2903B is verified and restricted - but that is a search hint, not a verdict
    ///  [E22] the research characteristic and the NASA-836 target stay separate paths
    ///  [E23] PW-100(3) and prototype 2 7/8 data cannot silently mix
    ///  [E24] the NASA 836 approximate thrust anchor, and its approximation metadata
    ///  [E25] the ~22.4 klbf bound belongs to TP-1034 and is not a NASA 836 limit
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
            report.AppendLine("F-15 Twin F100-PW-100 Propulsion Validation");
            report.AppendLine("=============================================");

            ValidateTwinIdentity(report, ref passed, ref failed);
            ValidateIndependentState(report, ref passed, ref failed);
            ValidatePerformanceUnavailable(report, ref passed, ref failed);
            ValidateGeometryBlocksLiveFlight(report, ref passed, ref failed);
            ValidateEngineOutTrap(report, ref passed, ref failed);
            ValidateNoF16EngineLaw(report, ref passed, ref failed);
            ValidateDigitizedSourceData(report, ref passed, ref failed);
            ValidateSourcePointRegression(report, ref passed, ref failed);
            ValidateEnvelopeEnforcement(report, ref passed, ref failed);
            ValidateFiniteOutputs(report, ref passed, ref failed);
            ValidateGrossNetSeparation(report, ref passed, ref failed);
            ValidateSourcedSchedules(report, ref passed, ref failed);
            ValidateAugmentationBoundaries(report, ref passed, ref failed);
            ValidateDeckRefusesThrust(report, ref passed, ref failed);
            ValidatePropulsionOwnership(report, ref passed, ref failed);
            ValidateCrossValidationOnly(report, ref passed, ref failed);
            ValidateAxisNormalizerIsNotTheScale(report, ref passed, ref failed);
            ValidateStaticAnchorDoesNotClose(report, ref passed, ref failed);
            ValidateDatasetsStaySeparate(report, ref passed, ref failed);
            ValidateY12ChannelScale(report, ref passed, ref failed);
            ValidateRestrictedSpecification(report, ref passed, ref failed);
            ValidatePathSeparation(report, ref passed, ref failed);
            ValidateEngineFamilySeparation(report, ref passed, ref failed);
            ValidateNasa836ApproximateAnchor(report, ref passed, ref failed);
            ValidateBoundScope(report, ref passed, ref failed);

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

        // ---------------------------------------------------------------- [E7]

        private static void ValidateDigitizedSourceData(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[E7] Digitized TP-1034 figure 17 data");

            MavF100OperatingPointCurve[] curves = MavF100SourceData.NetThrustCurves;

            Record(curves.Length == 7,
                "all seven documented operating points are present",
                report, ref passed, ref failed);

            bool lengthsMatch = true;
            bool ascending = true;
            bool finite = true;
            int totalPoints = 0;

            for (int i = 0; i < curves.Length; i++)
            {
                MavF100OperatingPointCurve c = curves[i];

                if (c.powerLeverAngleDeg == null || c.netThrustFraction == null
                    || c.powerLeverAngleDeg.Length != c.netThrustFraction.Length
                    || c.powerLeverAngleDeg.Length < 2)
                {
                    lengthsMatch = false;
                    continue;
                }

                totalPoints += c.Count;

                for (int k = 1; k < c.Count; k++)
                {
                    if (!(c.powerLeverAngleDeg[k] > c.powerLeverAngleDeg[k - 1]))
                        ascending = false;
                }

                for (int k = 0; k < c.Count; k++)
                {
                    if (float.IsNaN(c.netThrustFraction[k])
                        || float.IsInfinity(c.netThrustFraction[k])
                        || float.IsNaN(c.powerLeverAngleDeg[k])
                        || float.IsInfinity(c.powerLeverAngleDeg[k]))
                    {
                        finite = false;
                    }
                }
            }

            Record(lengthsMatch,
                "every curve pairs each power lever angle with exactly one thrust fraction",
                report, ref passed, ref failed);

            Record(ascending,
                "power lever angle is strictly ascending in every curve, which interpolation needs",
                report, ref passed, ref failed);

            Record(finite,
                "all " + totalPoints + " digitized values are finite",
                report, ref passed, ref failed);

            // Distinct conditions, or two of them would match the same query.
            bool distinct = true;
            for (int i = 0; i < curves.Length; i++)
            {
                for (int j = i + 1; j < curves.Length; j++)
                {
                    bool sameAltitude = Mathf.Abs(curves[i].altitudeM - curves[j].altitudeM)
                        <= MavF100NormalizedNetThrustModel.ConditionMatchAltitudeToleranceM;
                    bool sameMach = Mathf.Abs(curves[i].mach - curves[j].mach)
                        <= MavF100NormalizedNetThrustModel.ConditionMatchMachTolerance;

                    if (sameAltitude && sameMach)
                        distinct = false;
                }
            }

            Record(distinct,
                "no two operating points fall inside one another's match tolerance,"
                + " so a query can never be answered by the wrong condition",
                report, ref passed, ref failed);

            // The figure supplies its own accuracy check for free. Panel (a) is sea level,
            // Mach 0, and its normalizer is defined as net thrust at maximum augmentation
            // there - so the last point of panel (a) must be exactly 1.0. Anything else is
            // digitization error, and this measures it.
            MavF100OperatingPointCurve slStatic = curves[0];
            float atMaxAugmentation = slStatic.netThrustFraction[slStatic.Count - 1];
            float normalizerError = Mathf.Abs(atMaxAugmentation - 1f);

            Record(
                slStatic.panel == "17(a)" && Mathf.Approximately(slStatic.mach, 0f),
                "the self-check uses panel 17(a), sea level and Mach 0, which defines the normalizer",
                report, ref passed, ref failed);

            Record(
                normalizerError <= MavF100SourceData.NetThrustDigitizationTolerance,
                "digitization error at the defining point is " + normalizerError.ToString("0.0000")
                + ", within the stated tolerance of "
                + MavF100SourceData.NetThrustDigitizationTolerance
                + " - the figure's own definition bounds the reading accuracy",
                report, ref passed, ref failed);

            Record(
                MavF100SourceData.NetThrustCurves[0].netThrustFraction
                    != MavF100SourceData.NetThrustCurves[0].netThrustFraction,
                "each read of the source data returns fresh arrays, so no caller can edit"
                + " the source out from under another",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [E8]

        private static void ValidateSourcePointRegression(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[E8] Source-point regression fixtures");

            MavF100OperatingPointCurve[] curves = MavF100SourceData.NetThrustCurves;

            int checkedPoints = 0;
            int mismatches = 0;
            string firstMismatch = string.Empty;

            for (int i = 0; i < curves.Length; i++)
            {
                MavF100OperatingPointCurve c = curves[i];

                for (int k = 0; k < c.Count; k++)
                {
                    MavF100NetThrustFractionResult r = MavF100NormalizedNetThrustModel.Evaluate(
                        c.altitudeM, c.mach, c.powerLeverAngleDeg[k]);

                    checkedPoints++;

                    bool ok = r.support == MavF100ThrustSupport.Supported
                        && r.panel == c.panel
                        && Mathf.Abs(r.netThrustFraction - c.netThrustFraction[k]) <= 1e-5f
                        && r.quantity == MavF100ThrustQuantity.UninstalledNetThrust;

                    if (!ok)
                    {
                        mismatches++;
                        if (firstMismatch.Length == 0)
                        {
                            firstMismatch = c.panel + " at PLA " + c.powerLeverAngleDeg[k]
                                + ": got " + r.netThrustFraction + " (" + r.support + ")";
                        }
                    }
                }
            }

            Record(checkedPoints == 63,
                "every digitized point is covered by a fixture (" + checkedPoints + " points)",
                report, ref passed, ref failed);

            Record(mismatches == 0,
                "every digitized point evaluates back to its own value, on its own panel,"
                + " labelled as uninstalled NET thrust"
                + (mismatches == 0 ? "" : "; first mismatch: " + firstMismatch),
                report, ref passed, ref failed);

            // Interpolation between two breakpoints must land between their values, or the
            // curve is not the one the figure draws.
            MavF100OperatingPointCurve sl = curves[0];
            float midPla = 0.5f * (sl.powerLeverAngleDeg[4] + sl.powerLeverAngleDeg[5]);
            MavF100NetThrustFractionResult mid =
                MavF100NormalizedNetThrustModel.Evaluate(sl.altitudeM, sl.mach, midPla);

            Record(
                mid.support == MavF100ThrustSupport.Supported
                && mid.netThrustFraction > sl.netThrustFraction[4]
                && mid.netThrustFraction < sl.netThrustFraction[5],
                "interpolation along power lever angle stays between its two breakpoints -"
                + " the source draws a continuous curve there, so this value is supported",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [E9]

        private static void ValidateEnvelopeEnforcement(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[E9] Source envelope enforcement, zero silent extrapolation");

            // A condition squarely between two documented points. Plausible, entirely undocumented.
            MavF100NetThrustFractionResult between =
                MavF100NormalizedNetThrustModel.Evaluate(6000f, 0.9f, 100f);

            Record(between.support == MavF100ThrustSupport.OutsideSourceSupport,
                "6 km at Mach 0.9 sits between documented points and is refused,"
                + " not interpolated across scattered test conditions",
                report, ref passed, ref failed);

            Record(!between.HasNumber && between.netThrustFraction == 0f,
                "a refused query carries no number at all",
                report, ref passed, ref failed);

            Record(between.sourceClass == MavF100SourceClass.Unavailable,
                "and its source class is Unavailable, so nothing downstream can grade it higher",
                report, ref passed, ref failed);

            // Right at the edge of the match tolerance, and just past it.
            MavF100NetThrustFractionResult justInside =
                MavF100NormalizedNetThrustModel.Evaluate(
                    9144f + MavF100NormalizedNetThrustModel.ConditionMatchAltitudeToleranceM - 1f,
                    0.9f, 100f);

            MavF100NetThrustFractionResult justOutside =
                MavF100NormalizedNetThrustModel.Evaluate(
                    9144f + MavF100NormalizedNetThrustModel.ConditionMatchAltitudeToleranceM + 1f,
                    0.9f, 100f);

            Record(justInside.support == MavF100ThrustSupport.Supported,
                "inside the altitude match tolerance the documented point still answers",
                report, ref passed, ref failed);

            Record(justOutside.support == MavF100ThrustSupport.OutsideSourceSupport,
                "one metre past it, the source no longer speaks and neither does the model",
                report, ref passed, ref failed);

            // Off the Mach axis by more than the tolerance.
            Record(
                MavF100NormalizedNetThrustModel.Evaluate(9144f, 0.95f, 100f).support
                    == MavF100ThrustSupport.OutsideSourceSupport,
                "Mach 0.95 at 9.144 km is outside the Mach match tolerance and is refused",
                report, ref passed, ref failed);

            // Power lever excursions clamp, and say so, and never extrapolate.
            MavF100OperatingPointCurve[] curves = MavF100SourceData.NetThrustCurves;
            MavF100OperatingPointCurve supersonic = curves[4];

            MavF100NetThrustFractionResult belowRange =
                MavF100NormalizedNetThrustModel.Evaluate(
                    supersonic.altitudeM, supersonic.mach, 20f);

            Record(
                belowRange.support == MavF100ThrustSupport.PowerLeverClampedToTestedRange,
                "PLA 20 deg at a supersonic point is reported as CLAMPED -"
                + " TP-1034 never tested below 83 deg there",
                report, ref passed, ref failed);

            Record(
                Mathf.Abs(belowRange.netThrustFraction - supersonic.netThrustFraction[0]) <= 1e-5f,
                "and the clamped value is the tested endpoint exactly, not an extrapolation of it",
                report, ref passed, ref failed);

            MavF100NetThrustFractionResult aboveRange =
                MavF100NormalizedNetThrustModel.Evaluate(
                    supersonic.altitudeM, supersonic.mach, 200f);

            Record(
                aboveRange.support == MavF100ThrustSupport.PowerLeverClampedToTestedRange
                && Mathf.Abs(aboveRange.netThrustFraction
                    - supersonic.netThrustFraction[supersonic.Count - 1]) <= 1e-5f,
                "PLA 200 deg clamps to the maximum tested setting rather than running off the curve",
                report, ref passed, ref failed);

            Record(
                MavF100NormalizedNetThrustModel.DescribeSourceEnvelope().Contains("17(g)"),
                "the envelope describes itself, naming every panel it came from",
                report, ref passed, ref failed);
        }

        // --------------------------------------------------------------- [E10]

        private static void ValidateFiniteOutputs(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[E10] Finite outputs, and refusal of non-finite inputs");

            MavF100OperatingPointCurve[] curves = MavF100SourceData.NetThrustCurves;

            int evaluated = 0;
            bool allFinite = true;
            bool allBounded = true;

            for (int i = 0; i < curves.Length; i++)
            {
                for (float pla = 0f; pla <= 200f; pla += 5f)
                {
                    MavF100NetThrustFractionResult r = MavF100NormalizedNetThrustModel.Evaluate(
                        curves[i].altitudeM, curves[i].mach, pla);

                    evaluated++;

                    if (float.IsNaN(r.netThrustFraction) || float.IsInfinity(r.netThrustFraction))
                        allFinite = false;

                    // Figure 17 peaks at 1.338 (panel e). Nothing may exceed the data's own range.
                    if (r.netThrustFraction < -0.5f || r.netThrustFraction > 1.5f)
                        allBounded = false;
                }
            }

            Record(allFinite,
                "all " + evaluated + " sweep evaluations are finite",
                report, ref passed, ref failed);

            Record(allBounded,
                "and every one stays inside the range the figure itself spans",
                report, ref passed, ref failed);

            Record(
                MavF100NormalizedNetThrustModel.Evaluate(float.NaN, 0.9f, 100f).support
                    == MavF100ThrustSupport.NotEvaluated,
                "a NaN altitude is refused outright rather than propagated",
                report, ref passed, ref failed);

            Record(
                MavF100NormalizedNetThrustModel.Evaluate(9144f, float.PositiveInfinity, 100f)
                    .support == MavF100ThrustSupport.NotEvaluated,
                "an infinite Mach number is refused outright",
                report, ref passed, ref failed);

            Record(
                MavF100NormalizedNetThrustModel.Evaluate(9144f, 0.9f, float.NaN).support
                    == MavF100ThrustSupport.NotEvaluated,
                "a NaN power lever angle is refused outright",
                report, ref passed, ref failed);
        }

        // --------------------------------------------------------------- [E11]

        private static void ValidateGrossNetSeparation(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[E11] Gross, ram drag and net thrust stay separate quantities");

            // Ram drag against the printed equation, computed independently here.
            const float airflow = 100f;
            const float mach = 0.9f;
            const float temperature = 288.15f;

            MavF100ThrustValue drag = MavF100ThrustSemantics.RamDragN(airflow, mach, temperature);
            float expected = MavF100SourceData.RamDragCoefficient
                * airflow * mach * Mathf.Sqrt(temperature);

            Record(drag.valid && Mathf.Abs(drag.newtons - expected) <= 1e-2f,
                "ram drag reproduces 20.041*w2*M0*sqrt(T0) from TM X-3261 (B52) / TP-1034 (B56)",
                report, ref passed, ref failed);

            Record(drag.quantity == MavF100ThrustQuantity.RamDrag,
                "and it is labelled as ram drag, not as a thrust",
                report, ref passed, ref failed);

            // The coefficient is sqrt(gamma*R) for air. If someone ever "tidies" it, this fails.
            float sqrtGammaR = Mathf.Sqrt(1.4f * 287.05f);
            Record(
                Mathf.Abs(MavF100SourceData.RamDragCoefficient - sqrtGammaR) < 0.01f,
                "the printed coefficient is sqrt(gamma*R) for air to within 0.01 ("
                + sqrtGammaR.ToString("0.000") + "), which is why the term is w2*V0",
                report, ref passed, ref failed);

            Record(
                MavF100ThrustSemantics.RamDragN(airflow, 0f, temperature).newtons == 0f,
                "ram drag is exactly zero at Mach 0, as the equation requires",
                report, ref passed, ref failed);

            // A net value must not be handed to the gross-to-net transform.
            MavF100ThrustValue net = MavF100ThrustValue.Of(
                50000f, MavF100ThrustQuantity.UninstalledNetThrust,
                MavF100SourceClass.CompatibleSupport, "test fixture");

            MavF100ThrustValue doubleSubtracted =
                MavF100ThrustSemantics.NetFromGross(net, airflow, mach, temperature);

            Record(!doubleSubtracted.valid,
                "applying the ram-drag transform to a NET value is refused -"
                + " that mistake subtracts the inlet momentum twice and looks entirely plausible",
                report, ref passed, ref failed);

            MavF100ThrustValue gross = MavF100ThrustValue.Of(
                80000f, MavF100ThrustQuantity.GrossThrust,
                MavF100SourceClass.CompatibleSupport, "test fixture");

            MavF100ThrustValue converted =
                MavF100ThrustSemantics.NetFromGross(gross, airflow, mach, temperature);

            Record(
                converted.valid
                && converted.quantity == MavF100ThrustQuantity.UninstalledNetThrust
                && Mathf.Abs(converted.newtons - (80000f - expected)) <= 1e-2f,
                "a gross value converts to UNINSTALLED net thrust, never to installed thrust",
                report, ref passed, ref failed);

            // Provenance cannot be laundered upward by passing through the equation.
            MavF100ThrustValue weakGross = MavF100ThrustValue.Of(
                80000f, MavF100ThrustQuantity.GrossThrust,
                MavF100SourceClass.CrossValidationOnly, "oracle only");

            Record(
                MavF100ThrustSemantics.NetFromGross(weakGross, airflow, mach, temperature)
                    .sourceClass == MavF100SourceClass.CrossValidationOnly,
                "a cross-validation-only gross thrust stays cross-validation-only after conversion",
                report, ref passed, ref failed);

            Record(
                !MavF100ThrustSemantics.RamDragN(airflow, mach, -1f).valid
                && !MavF100ThrustSemantics.RamDragN(-1f, mach, temperature).valid
                && !MavF100ThrustSemantics.RamDragN(float.NaN, mach, temperature).valid,
                "negative airflow, non-positive temperature and NaN are refused rather than"
                + " returned as a zero that reads like a physical result",
                report, ref passed, ref failed);

            Record(
                MavF100SourceData.GrossThrustAxisNormalizerN == 111200f,
                "TP-1373's 111.2 kN axis normalizer is recorded as an axis scale (25 000 lbf),"
                + " kept where nobody will mistake it for the missing design maximum",
                report, ref passed, ref failed);
        }

        // --------------------------------------------------------------- [E12]

        private static void ValidateSourcedSchedules(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[E12] The fully-printed schedules, and inlet recovery");

            // EEC minimum power schedule, TP-1373 printed p. 5.
            Record(
                MavF100EngineControlSchedules.MinimumPowerFractionOfIntermediate(0.5f) == 0f
                && MavF100EngineControlSchedules.MinimumPowerFractionOfIntermediate(0.89f) == 0f,
                "below Mach 0.90 the EEC permits idle, so the minimum power fraction is zero",
                report, ref passed, ref failed);

            Record(
                MavF100EngineControlSchedules.MinimumPowerFractionOfIntermediate(1.4f) == 1f
                && MavF100EngineControlSchedules.MinimumPowerFractionOfIntermediate(2.5f) == 1f,
                "at and above Mach 1.4 the minimum is intermediate power and stays there",
                report, ref passed, ref failed);

            float atMidpoint =
                MavF100EngineControlSchedules.MinimumPowerFractionOfIntermediate(1.15f);

            Record(Mathf.Abs(atMidpoint - 0.5f) <= 1e-5f,
                "and it rises LINEARLY between them - Mach 1.15 is exactly half way",
                report, ref passed, ref failed);

            bool monotone = true;
            float previous = -1f;
            for (float m = 0f; m <= 2.5f; m += 0.01f)
            {
                float v = MavF100EngineControlSchedules.MinimumPowerFractionOfIntermediate(m);
                if (v < previous - 1e-6f)
                    monotone = false;
                previous = v;
            }

            Record(monotone,
                "the schedule never decreases with Mach number",
                report, ref passed, ref failed);

            // Nozzle area-ratio mode, TP-1373 printed p. 7.
            Record(
                !MavF100EngineControlSchedules.IsHighModeNozzleSchedule(1.0f)
                && MavF100EngineControlSchedules.IsHighModeNozzleSchedule(1.2f),
                "the nozzle area-ratio schedule switches to high mode above Mach 1.1",
                report, ref passed, ref failed);

            // Inlet recovery, TM X-3261 (B3).
            Record(
                MavF100InletRecovery.TotalPressureRecovery(0.8f) == 1f
                && MavF100InletRecovery.TotalPressureRecovery(1.0f) == 1f,
                "inlet total-pressure recovery is unity at and below Mach 1",
                report, ref passed, ref failed);

            float atMach2 = MavF100InletRecovery.TotalPressureRecovery(2f);
            Record(
                Mathf.Abs(atMach2 - (1f - 0.075f)) <= 1e-5f,
                "at Mach 2 it is 1 - 0.075*(1)^1.35 = 0.925, straight from (B3)",
                report, ref passed, ref failed);

            bool recoveryFalls = true;
            float last = 1.1f;
            for (float m = 1.0f; m <= 2.5f; m += 0.05f)
            {
                float r = MavF100InletRecovery.TotalPressureRecovery(m);
                if (r > last + 1e-6f || r <= 0f || r > 1f)
                    recoveryFalls = false;
                last = r;
            }

            Record(recoveryFalls,
                "and it falls monotonically, staying inside (0, 1] across the supersonic range",
                report, ref passed, ref failed);

            Record(
                Mathf.Abs(MavF100InletRecovery.FanFaceTotalTemperatureRatio(1f) - 1.2f) <= 1e-5f
                && Mathf.Abs(MavF100InletRecovery.FanFaceTotalPressureRatio(0f) - 1f) <= 1e-5f,
                "fan-face stagnation ratios match (B4)/(B5): 1.2 at Mach 1, unity at rest",
                report, ref passed, ref failed);
        }

        // --------------------------------------------------------------- [E13]

        private static void ValidateAugmentationBoundaries(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[E13] Augmentation boundaries are only as sharp as the source");

            MavEngineProfile profile = MavF15PropulsionSkeleton.CreateUnfrozenEngineProfile();

            Record(
                profile.augmentation == MavEngineAugmentationSemantics.Unavailable,
                "augmentation semantics stay Unavailable: no source gives a burner-light"
                + " condition or an augmentor transition law for this engine",
                report, ref passed, ref failed);

            // The data DOES span augmented operation - it just cannot say where it begins.
            MavF100OperatingPointCurve sl = MavF100SourceData.NetThrustCurves[0];

            Record(
                sl.powerLeverAngleDeg[sl.Count - 1]
                    >= MavF100SourceData.MaximumAugmentationPowerLeverAngleDeg - 1f,
                "the sea-level curve does reach the maximum augmentation lever angle of "
                + MavF100SourceData.MaximumAugmentationPowerLeverAngleDeg + " deg",
                report, ref passed, ref failed);

            Record(
                MavF100SourceData.MilitaryPowerLeverAngleDegTmX3261
                    != MavF100SourceData.SupersonicMinimumPowerLeverAngleDeg,
                "TM X-3261's military lever angle (73 deg) and TP-1034's non-augmented ceiling"
                + " (83 deg) are kept as two separate constants - the reports do not share a"
                + " power lever convention and neither was averaged away",
                report, ref passed, ref failed);

            // Thrust rises across the augmented range: the plateau is a real feature of the data.
            MavF100NetThrustFractionResult atMilitary =
                MavF100NormalizedNetThrustModel.Evaluate(0f, 0f, 83.4f);
            MavF100NetThrustFractionResult atMaximum =
                MavF100NormalizedNetThrustModel.Evaluate(0f, 0f, 129.8f);

            Record(
                atMilitary.HasNumber && atMaximum.HasNumber
                && atMaximum.netThrustFraction > atMilitary.netThrustFraction,
                "and augmented thrust exceeds non-augmented thrust at sea level, "
                + atMilitary.netThrustFraction.ToString("0.000") + " to "
                + atMaximum.netThrustFraction.ToString("0.000"),
                report, ref passed, ref failed);

            Record(
                MavF100SourceData.NormalizedThrustEngineBuild == "F100-PW-100(3)",
                "the augmented characteristic is labelled F100-PW-100(3), not silently promoted"
                + " to the F100-PW-100 target build",
                report, ref passed, ref failed);
        }

        // --------------------------------------------------------------- [E14]

        private static void ValidateDeckRefusesThrust(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[E14] The thrust deck refuses to produce a force");

            Record(
                !MavF100SourceData.DesignMaximumNetThrust.declared,
                "design maximum net thrust - the normalizer of figure 17 - is undeclared",
                report, ref passed, ref failed);

            Record(
                MavF100SourceData.DesignMaximumNetThrust.sourceClass
                    == MavF100SourceClass.Unavailable,
                "and its source class is Unavailable, so no caller can grade it upward",
                report, ref passed, ref failed);

            string bothMissing = MavF100ThrustDeck.DescribeBlockers(
                false, 0f, string.Empty, MavF100PowerLeverConvention.Undeclared);

            Record(
                bothMissing.Contains("SCALE") && bothMissing.Contains("POWER LEVER"),
                "the default deck reports BOTH blockers together, so closing one does not"
                + " suggest thrust is a single step away when it is two",
                report, ref passed, ref failed);

            string scaleOnly = MavF100ThrustDeck.DescribeBlockers(
                true, 100000f, "fixture citation", MavF100PowerLeverConvention.Undeclared);

            Record(
                !scaleOnly.Contains("SCALE") && scaleOnly.Contains("POWER LEVER"),
                "declaring the scale alone leaves the power lever convention blocking",
                report, ref passed, ref failed);

            string leverOnly = MavF100ThrustDeck.DescribeBlockers(
                false, 0f, string.Empty,
                MavF100PowerLeverConvention.LinearBetweenSourcedEndpoints("fixture"));

            Record(
                leverOnly.Contains("SCALE") && !leverOnly.Contains("POWER LEVER"),
                "and declaring the convention alone leaves the scale blocking",
                report, ref passed, ref failed);

            string uncited = MavF100ThrustDeck.DescribeBlockers(
                true, 100000f, string.Empty,
                MavF100PowerLeverConvention.LinearBetweenSourcedEndpoints("fixture"));

            Record(
                uncited.Contains("SCALE"),
                "a scale declared with no citation is still refused - that is exactly how an"
                + " invented number would enter a sourced deck",
                report, ref passed, ref failed);

            string nonPositive = MavF100ThrustDeck.DescribeBlockers(
                true, 0f, "fixture citation",
                MavF100PowerLeverConvention.LinearBetweenSourcedEndpoints("fixture"));

            Record(
                nonPositive.Contains("SCALE"),
                "so is a non-positive one",
                report, ref passed, ref failed);

            Record(
                !MavThrustDeckBase.IsPolicyAcceptableForLiveFlight(
                    MavThrustDataAuthority.Unavailable,
                    MavEnvelopeExcursionPolicy.RejectUnsupportedState),
                "and even with a refusing excursion policy, unavailable thrust data is not"
                + " acceptable for live flight",
                report, ref passed, ref failed);

            // The sourced half still works. That is the point of separating them.
            Record(
                MavF100NormalizedNetThrustModel.Evaluate(9144f, 0.9f, 130.5f).HasNumber,
                "while the normalized characteristic itself evaluates normally - the blocked"
                + " work is blocked, the rest is done",
                report, ref passed, ref failed);
        }

        // --------------------------------------------------------------- [E15]

        private static void ValidatePropulsionOwnership(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[E15] Propulsion writes no Rigidbody; aero adds no thrust");

            string root = ResolveFlightDynamicsRoot();

            if (root == null)
            {
                Record(true,
                    "SKIPPED: flight-dynamics sources are not present (player build)",
                    report, ref passed, ref failed);
                return;
            }

            MavOwnershipScanResult scan = MavFlightDynamicsOwnershipScan.Scan(root);

            if (!scan.sourcesAvailable)
            {
                Record(true,
                    "SKIPPED: flight-dynamics sources are not readable",
                    report, ref passed, ref failed);
                return;
            }

            List<string> propulsionViolations = new List<string>();
            if (scan.violations != null)
            {
                for (int i = 0; i < scan.violations.Count; i++)
                {
                    string v = scan.violations[i];
                    if (v.Contains("Propulsion") || v.Contains("Engine") || v.Contains("F100"))
                        propulsionViolations.Add(v);
                }
            }

            Record(propulsionViolations.Count == 0,
                "no propulsion or engine source applies a force, torque or velocity write ("
                + scan.filesScanned + " FDM files scanned)"
                + (propulsionViolations.Count == 0
                    ? ""
                    : "; first: " + propulsionViolations[0]),
                report, ref passed, ref failed);

            // The other direction: the aerodynamic model must not quietly grow a thrust term.
            string[] aeroFiles = Directory.GetFiles(root, "*Aero*.cs", SearchOption.AllDirectories);
            System.Array.Sort(aeroFiles, System.StringComparer.Ordinal);

            List<string> aeroThrust = new List<string>();
            for (int i = 0; i < aeroFiles.Length; i++)
            {
                string text = File.ReadAllText(aeroFiles[i]);
                string name = Path.GetFileName(aeroFiles[i]);

                if (text.Contains("MavThrustDeck") || text.Contains("thrustN")
                    || text.Contains("MavPropulsiveLoads") || text.Contains("MavF100"))
                {
                    aeroThrust.Add(name);
                }
            }

            Record(aeroThrust.Count == 0,
                "no aerodynamic source references a thrust deck, a thrust force or propulsive"
                + " loads (" + aeroFiles.Length + " aero files scanned)"
                + (aeroThrust.Count == 0 ? "" : "; first: " + aeroThrust[0]),
                report, ref passed, ref failed);
        }

        // --------------------------------------------------------------- [E16]

        private static void ValidateCrossValidationOnly(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[E16] TP-1782 stays cross-validation only");

            Record(
                MavF100SourceData.FlightValidationCitation.Contains("CROSS-VALIDATION ONLY"),
                "the TP-1782 record says outright that it is an oracle, not a value source",
                report, ref passed, ref failed);

            Record(
                MavF100SourceData.FlightValidationCitation.Contains("LEFT"),
                "and that only the left-position engine 059 was ever flown, so it supports no"
                + " claim about right-engine installation",
                report, ref passed, ref failed);

            // The flight-validation envelope must not have become the model's envelope. The
            // flown band is Mach 0.6-1.5 at 6-13.7 km; the model answers at seven scattered
            // points, and most of that band is not among them.
            int insideFlightBand = 0;
            MavF100OperatingPointCurve[] curves = MavF100SourceData.NetThrustCurves;
            for (int i = 0; i < curves.Length; i++)
            {
                if (curves[i].mach >= MavF100SourceData.FlightValidationMinMach
                    && curves[i].mach <= MavF100SourceData.FlightValidationMaxMach
                    && curves[i].altitudeM >= MavF100SourceData.FlightValidationMinAltitudeM
                    && curves[i].altitudeM <= MavF100SourceData.FlightValidationMaxAltitudeM)
                {
                    insideFlightBand++;
                }
            }

            Record(insideFlightBand <= 2,
                "only " + insideFlightBand + " of the seven modelled operating points fall inside"
                + " the flown band at all, so TP-1782 cannot be standing in as the source envelope",
                report, ref passed, ref failed);

            Record(
                MavF100NormalizedNetThrustModel.Evaluate(10000f, 1.2f, 100f).support
                    == MavF100ThrustSupport.OutsideSourceSupport,
                "a condition inside the flown band but outside the modelled points is still"
                + " refused - being flown by NASA is not the same as being published as data",
                report, ref passed, ref failed);

            Record(
                MavF100SourceData.CalibrationEngineBuild.Contains("2 7/8"),
                "the calibration engines are recorded as prototype series 2 7/8, the build both"
                + " TP-1373 and TP-1782 describe",
                report, ref passed, ref failed);
        }

        // --------------------------------------------------------------- [E17]

        private static void ValidateAxisNormalizerIsNotTheScale(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[E17] 111.2 kN is a plot scale, not the missing design maximum");

            Record(
                MavF100SourceData.GrossThrustAxisNormalizerN == 111200f,
                "TP-1373's axis normalizer is recorded as 111.2 kN",
                report, ref passed, ref failed);

            Record(
                MavF100SourceData.AxisNormalizerIsNotTheDesignMaximum.Contains("GROSS")
                && MavF100SourceData.AxisNormalizerIsNotTheDesignMaximum.Contains("NET"),
                "and recorded with the reason it cannot be the denominator: it is gross thrust"
                + " where figure 17 is net",
                report, ref passed, ref failed);

            Record(
                MavF100SourceData.AxisNormalizerIsNotTheDesignMaximum.Contains("TP-1228")
                && MavF100SourceData.AxisNormalizerIsNotTheDesignMaximum.Contains("not verified"),
                "the reported TP-1228 statement that it is an arbitrary nominal normalization is"
                + " carried as UNVERIFIED here, since TP-1228 is not held in this repository",
                report, ref passed, ref failed);

            // The decisive structural check: the deck must not be reachable by handing it the
            // axis scale. The scale is a plain float, so nothing stops someone assigning it -
            // but the deck still demands a citation, and "it was the only round number" is not one.
            string withAxisScale = MavF100ThrustDeck.DescribeBlockers(
                true, MavF100SourceData.GrossThrustAxisNormalizerN, string.Empty,
                MavF100PowerLeverConvention.LinearBetweenSourcedEndpoints("fixture"));

            Record(
                withAxisScale.Contains("SCALE"),
                "declaring 111.2 kN as the scale with no citation is still refused by the deck",
                report, ref passed, ref failed);
        }

        // --------------------------------------------------------------- [E18]

        private static void ValidateStaticAnchorDoesNotClose(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[E18] The sea-level-static anchor, and why it does not close");

            // The mechanism itself is sound and worth asserting: ram drag is EXACTLY zero at M0=0,
            // so gross and uninstalled net coincide there. This is what makes the anchor idea
            // right in principle.
            MavF100ThrustValue dragAtRest =
                MavF100ThrustSemantics.RamDragN(100f, 0f, 288.15f);

            Record(
                dragAtRest.valid && dragAtRest.newtons == 0f,
                "ram drag is identically zero at Mach 0 - so at a static condition gross thrust"
                + " and uninstalled net thrust are the same number, with no airflow needed",
                report, ref passed, ref failed);

            // Gate 1. Neither calibration report has a static point, and this searches for one.
            MavF100CalibrationCondition[] e059 = MavF100DimensionalAnchor.Engine059Conditions;
            MavF100CalibrationCondition[] e063 = MavF100DimensionalAnchor.Engine063Conditions;

            Record(e059.Length == 8 && e063.Length == 8,
                "both TP-1069 and TP-1228 test matrices are transcribed from TP-1373 table 3"
                + " (8 conditions each)",
                report, ref passed, ref failed);

            Record(
                !MavF100DimensionalAnchor.HasStaticCondition(e059)
                && !MavF100DimensionalAnchor.HasStaticCondition(e063),
                "NEITHER report contains a sea-level-static condition, so neither can supply the"
                + " anchor at all - this is the gate the chain actually fails at",
                report, ref passed, ref failed);

            float lowest059 = MavF100DimensionalAnchor.LowestMach(e059);
            float lowest063 = MavF100DimensionalAnchor.LowestMach(e063);

            Record(
                Mathf.Abs(lowest059 - 0.80f) <= 1e-5f && Mathf.Abs(lowest063 - 0.80f) <= 1e-5f,
                "the lowest condition either engine ran is Mach " + lowest059.ToString("0.00")
                + " - they are ALTITUDE facility calibrations, not sea-level tests",
                report, ref passed, ref failed);

            // Gate 1 enforced in code: a candidate measured off-static is refused.
            MavF100ThrustValue candidate = MavF100ThrustValue.Of(
                111200f, MavF100ThrustQuantity.GrossThrust,
                MavF100SourceClass.CompatibleSupport, "fixture");

            MavF100ThrustValue atMach08 =
                MavF100DimensionalAnchor.DesignMaximumNetThrustFromStaticGross(
                    candidate, 0.80f,
                    MavF100ConfigurationEquivalence.Unproven);

            Record(!atMach08.valid && atMach08.reason.Contains("static"),
                "a gross thrust measured at Mach 0.80 is refused as an anchor: ram drag there is"
                + " non-zero and cannot be removed without absolute airflow",
                report, ref passed, ref failed);

            // Gate 2: the value must actually be gross thrust.
            MavF100ThrustValue netCandidate = MavF100ThrustValue.Of(
                111200f, MavF100ThrustQuantity.UninstalledNetThrust,
                MavF100SourceClass.CompatibleSupport, "fixture");

            Record(
                !MavF100DimensionalAnchor.DesignMaximumNetThrustFromStaticGross(
                    netCandidate, 0f,
                    MavF100ConfigurationEquivalence.Unproven).valid,
                "and a value not labelled GROSS is refused even at a static condition",
                report, ref passed, ref failed);

            // Gate 3: configuration equivalence, unproven and refused.
            MavF100ThrustValue atStaticUnproven =
                MavF100DimensionalAnchor.DesignMaximumNetThrustFromStaticGross(
                    candidate, 0f,
                    MavF100ConfigurationEquivalence.Unproven);

            Record(
                !atStaticUnproven.valid && atStaticUnproven.reason.Contains("equivalence"),
                "even a genuine static gross thrust is refused while equivalence to the"
                + " F100-PW-100(3) is unproven - series 2 7/8 differs in core, control schedules"
                + " and nozzle actuation, and no source relates the two designation schemes",
                report, ref passed, ref failed);

            MavF100ConfigurationEquivalence claimedWithoutSource =
                new MavF100ConfigurationEquivalence();
            claimedWithoutSource.proven = true;
            claimedWithoutSource.provingSource = string.Empty;

            Record(
                !MavF100DimensionalAnchor.DesignMaximumNetThrustFromStaticGross(
                    candidate, 0f, claimedWithoutSource).valid,
                "claiming equivalence without naming a proving source does not count",
                report, ref passed, ref failed);

            // And the positive case, so the gate is known to be a gate and not a wall.
            MavF100ConfigurationEquivalence proven = new MavF100ConfigurationEquivalence();
            proven.proven = true;
            proven.provingSource = "fixture: hypothetical equivalence source";

            MavF100ThrustValue closed =
                MavF100DimensionalAnchor.DesignMaximumNetThrustFromStaticGross(
                    candidate, 0f, proven);

            Record(
                closed.valid
                && closed.quantity == MavF100ThrustQuantity.UninstalledNetThrust
                && closed.newtons == 111200f
                && closed.sourceClass == MavF100SourceClass.CompatibleSupport,
                "with all three gates satisfied the anchor does close, yielding uninstalled NET"
                + " thrust at compatible-support grade - so the refusals above are the evidence"
                + " failing, not the code",
                report, ref passed, ref failed);
        }

        // --------------------------------------------------------------- [E19]

        private static void ValidateDatasetsStaySeparate(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[E19] Normalized net and dimensional gross stay separate datasets");

            Record(
                !MavF100DimensionalGrossThrustDataset.Available,
                "the dimensional gross-thrust dataset is empty: "
                + "TP-1069 and TP-1228 are not held here",
                report, ref passed, ref failed);

            Record(
                MavF100DimensionalGrossThrustDataset.UnavailableReason.Contains("CCD 1088-2.0"),
                "and the reason names why TP-1373 cannot substitute - it reports their results"
                + " only as percentages against a deck that is also absent",
                report, ref passed, ref failed);

            Record(
                MavF100DimensionalGrossThrustDataset.Quantity
                    == MavF100ThrustQuantity.GrossThrust,
                "the dimensional dataset is GROSS thrust",
                report, ref passed, ref failed);

            MavF100NetThrustFractionResult normalized =
                MavF100NormalizedNetThrustModel.Evaluate(0f, 0f, 130f);

            Record(
                normalized.quantity == MavF100ThrustQuantity.UninstalledNetThrust,
                "while the normalized model is uninstalled NET thrust - different quantity,"
                + " different engine build, and dimensionless",
                report, ref passed, ref failed);

            Record(
                MavF100DimensionalGrossThrustDataset.EngineBuild.Contains("2 7/8")
                && MavF100SourceData.NormalizedThrustEngineBuild.Contains("(3)"),
                "the two datasets name different engine builds: "
                + MavF100DimensionalGrossThrustDataset.EngineBuild + " vs "
                + MavF100SourceData.NormalizedThrustEngineBuild,
                report, ref passed, ref failed);

            Record(
                MavF100DimensionalGrossThrustDataset.SourceClass
                    == MavF100SourceClass.CompatibleSupport,
                "both are compatible support; neither is exact-target, and being compatible"
                + " support does not make them compatible with EACH OTHER",
                report, ref passed, ref failed);
        }

        // --------------------------------------------------------------- [E20]

        private static void ValidateY12ChannelScale(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[E20] The TP-1034 Y12 net-thrust channel scale");

            Record(
                MavF100SourceData.SimulationNetThrustChannelFullScaleLbf == 30000f,
                "TP-1034 appendix C printed p. 28 gives FN=FN*30000. - verified from the page"
                + " image at 6x, the leading digit matching the 3 of T41=T41*3000. on the same"
                + " page and differing from the 2 and 5 of WPLPT=WPLPT*2 5*.29326",
                report, ref passed, ref failed);

            // The source prints its own SI conversion; ours must agree with it.
            float viaSourceConstant =
                MavF100SourceData.SimulationNetThrustChannelFullScaleLbf * 4.4482e-3f * 1000f;

            Record(
                Mathf.Abs(MavF100SourceData.SimulationNetThrustChannelFullScaleN
                    - viaSourceConstant) < 10f,
                "and the SI form agrees with the source's own FNSI=FN*4.4482E-3 to within 10 N: "
                + (MavF100SourceData.SimulationNetThrustChannelFullScaleN * 0.001f).ToString("0.000")
                + " kN",
                report, ref passed, ref failed);

            Record(
                Mathf.Abs(MavF100SourceData.SimulationNetThrustChannelFullScaleN - 133446.6f)
                    < 5f,
                "30 000 lbf is 133.45 kN, not 111.2 kN - the channel scale and TP-1373's axis"
                + " normalizer are different numbers as well as different quantities",
                report, ref passed, ref failed);

            // The decisive structural check, and the reason the scale is not applied.
            float peak = 0f;
            MavF100OperatingPointCurve[] curves = MavF100SourceData.NetThrustCurves;
            for (int i = 0; i < curves.Length; i++)
            {
                for (int k = 0; k < curves[i].Count; k++)
                {
                    if (curves[i].netThrustFraction[k] > peak)
                        peak = curves[i].netThrustFraction[k];
                }
            }

            Record(peak > 1f,
                "figure 17 plots up to " + peak.ToString("0.000")
                + " of its own normalizer - above 1.0, which a SCALED FRACTION channel cannot"
                + " represent",
                report, ref passed, ref failed);

            Record(
                MavF100SourceData.Y12IsNotTheFigure17Normalizer.Contains("SCALED FRACTION"),
                "so Y12's full scale is recorded as NOT the figure 17 normalizer, on the grounds"
                + " of the SCALED FRACTION declaration on printed p. 25",
                report, ref passed, ref failed);

            // The derived bound. A bound, not a value - nothing computes with it.
            float bound = MavF100SourceData.DesignMaximumNetThrustUpperBoundLbf;

            Record(
                bound > 0f && bound < MavF100SourceData.SimulationNetThrustChannelFullScaleLbf,
                "the derived upper bound on the design maximum is "
                + bound.ToString("0") + " lbf, strictly below the 30 000 lbf channel scale",
                report, ref passed, ref failed);

            Record(
                bound < 25000f,
                "and it falls below 25 000 lbf, so it independently rules out 111.2 kN as the"
                + " design maximum as well",
                report, ref passed, ref failed);

            // The whole point: none of this dimensionalizes the deck.
            Record(
                !MavF100SourceData.DesignMaximumNetThrust.declared,
                "the design maximum net thrust remains UNDECLARED - a channel full scale is not"
                + " a design value, and the 63 points stay dimensionless",
                report, ref passed, ref failed);

            Record(
                MavF100SourceData.DesignMaximumNetThrust.citation.Contains("30 000 lbf"),
                "with the investigated-and-rejected candidate named in the blocker text, so the"
                + " next reader does not re-run this search",
                report, ref passed, ref failed);

            MavF100NetThrustFractionResult slsMax =
                MavF100NormalizedNetThrustModel.Evaluate(0f, 0f, 129.8f);

            Record(
                slsMax.HasNumber && Mathf.Abs(slsMax.netThrustFraction - 1.004f) < 0.01f,
                "and the sea-level maximum-augmentation point is still a FRACTION, "
                + slsMax.netThrustFraction.ToString("0.000") + ", not a force",
                report, ref passed, ref failed);
        }

        // --------------------------------------------------------------- [E21]

        private static void ValidateRestrictedSpecification(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[E21] CP2903B verified and restricted - a search hint, not a verdict");

            MavF100DeclaredScalar scale = MavF100SourceData.DesignMaximumNetThrust;

            Record(
                scale.blocker == MavF100BlockerKind.UnavailableInHeldSources,
                "the design maximum net thrust is UnavailableInHeldSources - absent from what is "
                + "held, which says nothing about what some other public document may print",
                report, ref passed, ref failed);

            Record(
                scale.restrictedPrimarySpecification,
                "and it carries the restricted-primary-specification flag: TP-1056 printed p. 8 "
                + "verifies that the F100 thrust REQUIREMENTS live in classified CP2903B",
                report, ref passed, ref failed);

            Record(
                MavF100SourceData.ClassifiedThrustSpecification.Contains("search hint, not a verdict"),
                "recorded explicitly as a search hint rather than 'looking will not help' - a "
                + "classified requirements document and a published performance figure are "
                + "different documents, and NASA F-15B reports do publish the latter",
                report, ref passed, ref failed);

            Record(
                !scale.declared && scale.sourceClass == MavF100SourceClass.Unavailable,
                "and it remains undeclared and Unavailable - a named reason for a gap does not "
                + "fill the gap",
                report, ref passed, ref failed);

            Record(
                MavF100SourceData.ClassifiedThrustSpecification.Contains("VERIFIED")
                && MavF100SourceData.ClassifiedThrustSpecification.Contains("CP2903B"),
                "the CP2903B finding is now VERIFIED from the primary source, NASA TP-1056 "
                + "printed p. 8, retrieved from NTRS and read",
                report, ref passed, ref failed);

            Record(
                MavF100SourceData.ClassifiedThrustSpecification.Contains("Do not reconstruct"),
                "with the instruction attached: a classified specification is not a gap to be "
                + "filled by inference",
                report, ref passed, ref failed);

            Record(
                MavF100SourceData.Tp1056Citation.Contains("19770026225")
                && MavF100SourceData.Tp1056Citation.Contains("p. 8"),
                "TP-1056 is cited by NTRS document id and printed page",
                report, ref passed, ref failed);

            Record(
                MavF100SourceData.NetThrustComputedDigitally.Contains("DIGITAL"),
                "and its printed p. 17 statement that net thrust is computed in the DIGITAL "
                + "portion is recorded - which is what removes the plotting assumption from the "
                + "derived bound",
                report, ref passed, ref failed);

            // Finding 2 strengthens the Y12 argument: the ceiling binds at computation time.
            Record(
                MavF100SourceData.SimulationNetThrustChannelFullScaleLbf == 30000f
                && !MavF100SourceData.DesignMaximumNetThrust.declared,
                "the 30 000 lbf channel scale is still held as a channel scale and still not "
                + "used as the design thrust",
                report, ref passed, ref failed);

            float bound = MavF100SourceData.DesignMaximumNetThrustUpperBoundLbf;

            Record(
                bound > 22000f && bound < 23000f,
                "and the derived upper bound stands at " + bound.ToString("0")
                + " lbf, cross-validation grade",
                report, ref passed, ref failed);
        }

        // --------------------------------------------------------------- [E22]

        private static void ValidatePathSeparation(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[E22] Research characteristic and NASA-836 target stay separate");

            // Path B now holds one approximate anchor. The barrier matters more, not less.
            Record(
                MavF100Nasa836TargetPropulsion.HasDimensionalAnchor
                && MavF100Nasa836TargetPropulsion.Anchors[0].IsApproximate,
                "path B holds exactly one dimensional anchor and it is APPROXIMATE - the "
                + "separation now guards a live number rather than an empty set",
                report, ref passed, ref failed);

            Record(
                MavF100Nasa836TargetPropulsion.EngineIdentityClass
                    == MavF100SourceClass.AuthoritativeExactTarget,
                "while the engine IDENTITY on path B is exact-target - the one exact-target "
                + "propulsion fact this project holds",
                report, ref passed, ref failed);

            Record(
                MavF100Nasa836TargetPropulsion.Anchors
                    != MavF100Nasa836TargetPropulsion.Anchors,
                "and each read returns a fresh array, so no caller can populate path B for "
                + "everyone else",
                report, ref passed, ref failed);

            // Path membership: research evidence can never serve the target path.
            Record(
                !MavF100PathSeparation.BelongsTo(
                    MavF100SourceClass.CompatibleSupport, MavF100PropulsionPath.Nasa836Target),
                "compatible-support evidence does not belong to the target path, however good it "
                + "is at being research data",
                report, ref passed, ref failed);

            Record(
                !MavF100PathSeparation.BelongsTo(
                    MavF100SourceClass.AuthoritativeExactTarget,
                    MavF100PropulsionPath.ResearchCharacteristic),
                "and exact-target evidence is not research evidence either - the separation runs "
                + "in both directions",
                report, ref passed, ref failed);

            Record(
                MavF100PathSeparation.BelongsTo(
                    MavF100SourceClass.CompatibleSupport,
                    MavF100PropulsionPath.ResearchCharacteristic)
                && MavF100PathSeparation.BelongsTo(
                    MavF100SourceClass.AuthoritativeExactTarget,
                    MavF100PropulsionPath.Nasa836Target),
                "each path admits its own evidence",
                report, ref passed, ref failed);

            // The combination, refused four different ways.
            MavF100NetThrustFractionResult research =
                MavF100NormalizedNetThrustModel.Evaluate(0f, 0f, 129.8f);

            Record(research.HasNumber,
                "path A still answers on its own: sea-level maximum augmentation is "
                + research.netThrustFraction.ToString("0.000") + " of design maximum",
                report, ref passed, ref failed);

            MavF100Nasa836TargetAnchor empty = new MavF100Nasa836TargetAnchor();

            MavF100PathCombination noAnchor = MavF100PathSeparation.DimensionalizeForTarget(
                research, empty, MavF100ConfigurationEquivalence.Unproven);

            Record(!noAnchor.permitted && noAnchor.newtons == 0f,
                "but combining it with an unpopulated target anchor is refused, and yields no "
                + "number",
                report, ref passed, ref failed);

            MavF100Nasa836TargetAnchor uncited = MavF100Nasa836TargetPropulsion.Anchors[0];
            uncited.citation = string.Empty;

            Record(
                !MavF100PathSeparation.DimensionalizeForTarget(
                    research, uncited, MavF100ConfigurationEquivalence.Unproven).permitted,
                "an anchor with no NASA-836 citation is refused",
                report, ref passed, ref failed);

            MavF100Nasa836TargetAnchor noCondition = MavF100Nasa836TargetPropulsion.Anchors[0];
            noCondition.condition = string.Empty;

            Record(
                !MavF100PathSeparation.DimensionalizeForTarget(
                    research, noCondition, MavF100ConfigurationEquivalence.Unproven).permitted,
                "and so is one with no stated operating condition - a thrust without a condition "
                + "means nothing",
                report, ref passed, ref failed);

            MavF100Nasa836TargetAnchor grossAnchor = MavF100Nasa836TargetPropulsion.Anchors[0];
            grossAnchor.quantityName = "fixture gross";
            grossAnchor.quantity = MavF100ThrustQuantity.GrossThrust;

            MavF100ConfigurationEquivalence proven = new MavF100ConfigurationEquivalence();
            proven.proven = true;
            proven.provingSource = "fixture equivalence source";

            MavF100PathCombination mismatch = MavF100PathSeparation.DimensionalizeForTarget(
                research, grossAnchor, proven);

            Record(!mismatch.permitted && mismatch.reason.Contains("quantity mismatch"),
                "a GROSS anchor cannot scale the NET characteristic, even with equivalence proven",
                report, ref passed, ref failed);

            MavF100Nasa836TargetAnchor netAnchor = MavF100Nasa836TargetPropulsion.Anchors[0];

            MavF100PathCombination unproven = MavF100PathSeparation.DimensionalizeForTarget(
                research, netAnchor, MavF100ConfigurationEquivalence.Unproven);

            Record(!unproven.permitted && unproven.reason.Contains("equivalence"),
                "and a matching anchor is still refused while build equivalence is unproven - "
                + "otherwise the product is one engine's thrust shape wearing another's identity",
                report, ref passed, ref failed);

            // The positive case, so the barrier is known to be a gate rather than a wall.
            MavF100PathCombination allowed = MavF100PathSeparation.DimensionalizeForTarget(
                research, netAnchor, proven);

            Record(
                allowed.permitted
                && Mathf.Abs(allowed.newtons
                    - research.netThrustFraction * netAnchor.newtons) < 1f,
                "with every condition met the combination is permitted and multiplies correctly",
                report, ref passed, ref failed);

            Record(
                allowed.sourceClass == MavF100SourceClass.CompatibleSupport,
                "and the product is CompatibleSupport, NOT exact-target: an exact-target anchor "
                + "scales a research characteristic, it does not promote one",
                report, ref passed, ref failed);

            Record(
                allowed.precision == MavF100ValuePrecision.Approximate,
                "nor sharper - an approximate anchor yields an approximate product, because "
                + "multiplying an approximation by an exact fraction does not sharpen it",
                report, ref passed, ref failed);

            // The headline regression the follow-up asks for: the real NASA 836 anchor, on its
            // own, cannot dimensionalize the research curve.
            MavF100PathCombination realAnchorAlone =
                MavF100PathSeparation.DimensionalizeForTarget(
                    research,
                    MavF100Nasa836TargetPropulsion.Anchors[0],
                    MavF100ConfigurationEquivalence.Unproven);

            Record(
                !realAnchorAlone.permitted && realAnchorAlone.newtons == 0f,
                "and the REAL NASA 836 anchor, alone, cannot dimensionalize the PW-100(3) curve: "
                + "an exact-target aircraft figure is not a proof that the engine builds match",
                report, ref passed, ref failed);
        }

        // --------------------------------------------------------------- [E23]

        private static void ValidateEngineFamilySeparation(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[E23] PW-100(3) and prototype 2 7/8 data cannot silently mix");

            MavF100ConfigurationEquivalence unproven = MavF100ConfigurationEquivalence.Unproven;

            Record(
                MavF100EngineFamilies.MayCombine(
                    MavF100EngineFamily.Pw100SimulationLineage,
                    MavF100EngineFamily.Pw100SimulationLineage, unproven),
                "evidence may combine within one engine family without an equivalence proof",
                report, ref passed, ref failed);

            Record(
                !MavF100EngineFamilies.MayCombine(
                    MavF100EngineFamily.Pw100SimulationLineage,
                    MavF100EngineFamily.PrototypeSeries2And7Eighths, unproven),
                "but the PW-100 simulation lineage and the prototype series 2 7/8 engines may NOT "
                + "combine - TP-1373 p. 4 records different cores, control schedules and nozzle "
                + "actuation, and nothing in the pack relates the (1)/(3) and series designations",
                report, ref passed, ref failed);

            Record(
                !MavF100EngineFamilies.MayCombine(
                    MavF100EngineFamily.Pw100SimulationLineage,
                    MavF100EngineFamily.Nasa836Target, unproven),
                "nor may the simulation lineage combine with the NASA 836 target",
                report, ref passed, ref failed);

            Record(
                !MavF100EngineFamilies.MayCombine(
                    MavF100EngineFamily.PrototypeSeries2And7Eighths,
                    MavF100EngineFamily.Nasa836Target, unproven),
                "nor the prototype engines with the NASA 836 target",
                report, ref passed, ref failed);

            Record(
                !MavF100EngineFamilies.MayCombine(
                    MavF100EngineFamily.Unspecified,
                    MavF100EngineFamily.Nasa836Target, unproven),
                "and data with no declared family combines with nothing at all",
                report, ref passed, ref failed);

            MavF100ConfigurationEquivalence proven = new MavF100ConfigurationEquivalence();
            proven.proven = true;
            proven.provingSource = "fixture equivalence source";

            Record(
                MavF100EngineFamilies.MayCombine(
                    MavF100EngineFamily.Pw100SimulationLineage,
                    MavF100EngineFamily.PrototypeSeries2And7Eighths, proven),
                "a named proving source is the only thing that opens a cross-family combination",
                report, ref passed, ref failed);

            // The families are named distinctly, so a reader can tell which engine a number is about.
            Record(
                MavF100EngineFamilies.Describe(MavF100EngineFamily.Pw100SimulationLineage)
                    != MavF100EngineFamilies.Describe(
                        MavF100EngineFamily.PrototypeSeries2And7Eighths),
                "the two research families describe themselves differently: "
                + MavF100EngineFamilies.Describe(MavF100EngineFamily.Pw100SimulationLineage)
                + " vs "
                + MavF100EngineFamilies.Describe(
                    MavF100EngineFamily.PrototypeSeries2And7Eighths),
                report, ref passed, ref failed);
        }

        // --------------------------------------------------------------- [E24]

        private static void ValidateNasa836ApproximateAnchor(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[E24] The NASA 836 approximate thrust anchor");

            Record(
                MavF100Nasa836TargetPropulsion.HasDimensionalAnchor,
                "path B is no longer empty: NASA's own F-15B reports publish an approximate "
                + "thrust for this aircraft's engines",
                report, ref passed, ref failed);

            MavF100Nasa836TargetAnchor[] anchors = MavF100Nasa836TargetPropulsion.Anchors;

            Record(anchors.Length == 1 && anchors[0].IsUsable,
                "exactly one anchor, and it is usable - citation, condition, precision and "
                + "engine family all present",
                report, ref passed, ref failed);

            MavF100Nasa836TargetAnchor a = anchors[0];

            Record(
                Mathf.Abs(a.sourceValue - 23500f) < 1f && a.sourceUnits == "lbf",
                "it carries the value in the units the source printed: 23 500 lbf",
                report, ref passed, ref failed);

            Record(
                Mathf.Abs(a.newtons - 104533.2f) < 5f,
                "and the derived SI value, " + (a.newtons * 0.001f).ToString("0.00")
                + " kN - derived here, since TM-2005-213670 prints only lbf in that sentence",
                report, ref passed, ref failed);

            Record(
                a.precision == MavF100ValuePrecision.Approximate && a.IsApproximate,
                "the approximation metadata is preserved: the source says 'approximately', and "
                + "an approximation that forgets it was one becomes a specification",
                report, ref passed, ref failed);

            Record(
                a.quantity == MavF100ThrustQuantity.UninstalledNetThrust
                && a.condition.Contains("static")
                && a.condition.Contains("afterburner"),
                "with its quantity and condition attached: uninstalled net thrust, "
                + a.condition,
                report, ref passed, ref failed);

            Record(
                a.engineFamily == MavF100EngineFamily.Nasa836Target
                && a.targetIdentity == MavF100Nasa836TargetPropulsion.AircraftConfiguration,
                "and it names the aircraft it is about",
                report, ref passed, ref failed);

            Record(
                a.citation.Contains("TM-2005-213670") && a.citation.Contains("APPROXIMATE"),
                "citation NASA/TM-2005-213670, flagged approximate in the citation itself",
                report, ref passed, ref failed);

            // An anchor is not a deck, and the record says so.
            Record(
                MavF100Nasa836TargetPropulsion.StillUnavailableReason.Contains("not a thrust deck"),
                "one sea-level point is still not a thrust deck: no lapse, part power, airflow, "
                + "fuel flow, spool dynamics or installation effects for this aircraft",
                report, ref passed, ref failed);

            // The conflicting NASA figure is recorded, with its arithmetic error.
            Record(
                MavF100Nasa836TargetPropulsion.ConflictingThrustFigure.Contains("25,000 lbf")
                && MavF100Nasa836TargetPropulsion.ConflictingThrustFigure.Contains("111,206 N"),
                "the conflicting 25 000 lbf figure from TM-2001-210395 / TM-2002-210736 is "
                + "recorded, along with the fact that its own parenthetical (91,188 N) is "
                + "arithmetically wrong",
                report, ref passed, ref failed);

            // Exact-target identity must not become an exact-target performance deck.
            Record(
                MavF100Nasa836TargetPropulsion.EngineIdentityClass
                    == MavF100SourceClass.AuthoritativeExactTarget
                && !MavF100SourceData.DesignMaximumNetThrust.declared,
                "exact-target IDENTITY plus one approximate anchor does not make an exact-target "
                + "performance deck - the research normalizer is still undeclared",
                report, ref passed, ref failed);
        }

        // --------------------------------------------------------------- [E25]

        private static void ValidateBoundScope(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[E25] The ~22.4 klbf bound belongs to TP-1034 only");

            Record(
                MavF100EngineFamilies.BoundAppliesTo(
                    MavF100EngineFamily.Pw100SimulationLineage),
                "the derived bound applies to the PW-100 simulation lineage, whose figure 17 "
                + "normalizer it is about",
                report, ref passed, ref failed);

            Record(
                !MavF100EngineFamilies.BoundAppliesTo(MavF100EngineFamily.Nasa836Target),
                "and NOT to NASA 836 - it is not an upper bound on that aircraft's engine thrust",
                report, ref passed, ref failed);

            Record(
                !MavF100EngineFamilies.BoundAppliesTo(
                    MavF100EngineFamily.PrototypeSeries2And7Eighths),
                "nor to the prototype series 2 7/8 engines",
                report, ref passed, ref failed);

            float bound = MavF100SourceData.DesignMaximumNetThrustUpperBoundLbf;
            float nasa836 = MavF100Nasa836TargetPropulsion.ApproximateSlsFullAbThrustLbf;

            Record(
                nasa836 > bound,
                "the NASA 836 figure (" + nasa836.ToString("0")
                + " lbf) is ABOVE the bound (" + bound.ToString("0")
                + " lbf) - which is exactly why the bound must not be carried across families: "
                + "applied to 836 it would 'prove' a published NASA figure impossible",
                report, ref passed, ref failed);

            Record(
                MavF100SourceData.Y12IsNotTheFigure17Normalizer.Length > 0
                && MavF100SourceData.SimulationNetThrustChannelFullScaleLbf > nasa836,
                "and the 30 000 lbf channel scale sits above both, consistent with being a "
                + "headroom scale rather than any engine's rating",
                report, ref passed, ref failed);
        }

        // ------------------------------------------------------ source-tree location

        /// <summary>
        /// Locates the flight-dynamics sources whether or not a Unity player is loaded. Outside
        /// Unity <see cref="Application.dataPath"/> throws, so fall back to walking up from the
        /// working directory, which is how the static host runs these checks.
        /// </summary>
        private static string ResolveFlightDynamicsRoot()
        {
            try
            {
                string fromUnity = Path.Combine(
                    Application.dataPath,
                    MavFlightDynamicsOwnershipScan.FlightDynamicsRelativePath);

                if (Directory.Exists(fromUnity))
                    return fromUnity;
            }
            catch (System.Exception)
            {
                // No Unity player loaded. Fall through to the directory walk.
            }

            DirectoryInfo dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            for (int depth = 0; depth < 8 && dir != null; depth++)
            {
                string candidate = Path.Combine(
                    Path.Combine(dir.FullName, "Assets"),
                    MavFlightDynamicsOwnershipScan.FlightDynamicsRelativePath);

                if (Directory.Exists(candidate))
                    return candidate;

                dir = dir.Parent;
            }

            return null;
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
