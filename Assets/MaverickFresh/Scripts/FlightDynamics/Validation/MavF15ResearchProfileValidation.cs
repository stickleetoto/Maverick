using System.Text;
using UnityEngine;
using MaverickFresh.FlightDynamics.F15;
using Object = UnityEngine.Object;

namespace MaverickFresh.FlightDynamics.Validation
{
    /// <summary>
    /// The F-15 RESEARCH configuration <see cref="MavF15AfitResearchIdentity.ConfigurationId"/>:
    /// that it carries only research-source data, that it is valid only at its source condition,
    /// and that the exact NASA 836 path is unchanged beside it.
    ///
    /// No flight-performance tolerance is asserted anywhere: none is sourced yet.
    ///
    /// Covered:
    ///   [P0] identity - unique, no exact-target token, the source condition in every label
    ///   [P1] reference geometry is the audited ARO10 set, reused rather than duplicated
    ///   [P2] mass / inertia are Davison's, reproduce his active constants, and convert soundly
    ///   [P3] the envelope is fixed to M 0.6 / 20,000 ft; research aero refuses outside it
    ///   [P4] the 8,300 lbf thrust keeps its source semantic and ignores the throttle
    ///   [P5] the exact NASA 836 path did not change
    ///   [P6] the research profile reaches a six-DoF-compatible load path and generic structural
    ///        readiness, without exact authority
    /// </summary>
    public static class MavF15ResearchProfileValidation
    {
        public static string RunAll(out int passed, out int failed)
        {
            passed = 0;
            failed = 0;
            StringBuilder report = new StringBuilder(4096);
            report.AppendLine("F-15 AFIT Research Profile Validation - "
                + MavF15AfitResearchIdentity.ConfigurationId
                + " @ " + MavF15AfitResearchIdentity.SourceConditionLabel);
            report.AppendLine("==========================================================");

            GameObject host = new GameObject("MavF15ResearchProfileValidationHost");
            host.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                MavF15AfitResearchFlightDynamicsProfile provider =
                    host.AddComponent<MavF15AfitResearchFlightDynamicsProfile>();
                MavFlightDynamicsProfile profile = provider.BuildProfile();

                ValidateIdentity(provider, profile, report, ref passed, ref failed);
                ValidateGeometry(profile, report, ref passed, ref failed);
                ValidateMass(profile, report, ref passed, ref failed);
                ValidateEnvelope(profile, report, ref passed, ref failed);
                ValidateThrust(report, ref passed, ref failed);
                ValidateExactPathUnchanged(report, ref passed, ref failed);
                ValidateLoadPathAndReadiness(profile, report, ref passed, ref failed);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }

            report.AppendLine();
            report.Append("RESULT: ")
                .Append(failed == 0 ? "PASS" : "FAIL")
                .Append("  passed=").Append(passed)
                .Append(" failed=").Append(failed);

            return report.ToString();
        }

        // ---------------------------------------------------------------- [P0]

        private static void ValidateIdentity(
            MavF15AfitResearchFlightDynamicsProfile provider, MavFlightDynamicsProfile profile,
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P0] Identity");

            Record(profile.profileId == MavF15AfitResearchIdentity.ConfigurationId
                    && profile.profileId == "F15_AFIT_BAUMANN_DAVISON_MACH06_20K_RESEARCH",
                "profile id is " + profile.profileId,
                report, ref passed, ref failed);

            Record(MavF15AfitResearchIdentity.CarriesNoExactTargetToken(profile.profileId)
                    && MavF15AfitResearchIdentity.CarriesNoExactTargetToken(
                        MavF15AfitResearchMassReference.ResearchMassStateId),
                "neither the profile id nor the research mass-state id carries NASA_F15B_836, "
                + "74-0141, PRE_QUIET_SPIKE or EXACT",
                report, ref passed, ref failed);

            string condition = MavF15AfitResearchIdentity.SourceConditionLabel;
            Record(profile.displayName.Contains(condition) && profile.displayName.Contains("NOT NASA 836")
                    && profile.description.Contains(condition)
                    && provider.debugProfileStatus.Contains(condition)
                    && MavF15AfitResearchThrustSource.ModelName.Contains(condition),
                "the source condition (" + condition + ") is in the display name, description, "
                + "debug status and thrust-model name; the display name says NOT NASA 836",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [P1]

        private static void ValidateGeometry(
            MavFlightDynamicsProfile profile, StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P1] Research reference geometry is the audited ARO10 set");

            MavAeroReferenceGeometry g = profile.referenceGeometry;
            MavAeroReferenceGeometry baumann = MavF15BaumannMach06Reference.CreateReferenceGeometry();

            Record(g.wingAreaM2 == baumann.wingAreaM2 && g.wingSpanM == baumann.wingSpanM
                    && g.meanAerodynamicChordM == baumann.meanAerodynamicChordM,
                "the profile reuses MavF15BaumannMach06Reference's geometry rather than new constants",
                report, ref passed, ref failed);

            Record(Mathf.Abs(g.wingAreaM2 - 608f * 0.09290304f) < 1e-3f
                    && Mathf.Abs(g.wingSpanM - 42.8f * 0.3048f) < 1e-4f
                    && Mathf.Abs(g.meanAerodynamicChordM - 15.94f * 0.3048f) < 1e-4f,
                "S = 608 ft^2 (" + g.wingAreaM2.ToString("F4") + " m^2), b = 42.8 ft ("
                + g.wingSpanM.ToString("F5") + " m), cbar = 15.94 ft ("
                + g.meanAerodynamicChordM.ToString("F6") + " m)",
                report, ref passed, ref failed);

            MavF15GeometryCandidate[] all = MavF15ReferenceGeometrySources.All;
            MavF15GeometryCandidate s = Find(all, "ARO10_S");
            MavF15GeometryCandidate c = Find(all, "ARO10_CBAR");
            MavF15GeometryCandidate b = Find(all, "ARO10_B_REFERENCE");
            Record(Mathf.Abs(s.siValue - g.wingAreaM2) < 1e-4f
                    && Mathf.Abs(c.siValue - g.meanAerodynamicChordM) < 1e-5f
                    && Mathf.Abs(b.siValue - g.wingSpanM) < 1e-5f
                    && s.authority == MavF15GeometryAuthority.F15FamilySupport,
                "and it is the same set the geometry audit graded F15_FAMILY_SUPPORT (ARO10 "
                + "driver constants), not exact-target authority",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [P2]

        private static void ValidateMass(
            MavFlightDynamicsProfile profile, StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P2] Research mass / inertia are Davison's");

            Record(MavF15AfitResearchMassReference.WeightLb == 37000f
                    && MavF15AfitResearchMassReference.IxxSlugFt2 == 25480f
                    && MavF15AfitResearchMassReference.IyySlugFt2 == 166620f
                    && MavF15AfitResearchMassReference.IzzSlugFt2 == 186930f
                    && MavF15AfitResearchMassReference.IxzSlugFt2 == -1000f,
                "raw values 37,000 lb / Ixx 25,480 / Iyy 166,620 / Izz 186,930 / Ixz -1,000 slug-ft^2",
                report, ref passed, ref failed);

            // The driver's inertia lines are commented out; its ACTIVE constants must reproduce
            // from them, or these would not be the values the running model used.
            double ix = MavF15AfitResearchMassReference.IxxSlugFt2;
            double iy = MavF15AfitResearchMassReference.IyySlugFt2;
            double iz = MavF15AfitResearchMassReference.IzzSlugFt2;
            double ixz = MavF15AfitResearchMassReference.IxzSlugFt2;
            double rmass = MavF15AfitResearchMassReference.WeightLb / 32.174;
            double k1 = 0.5 * 0.0012673 * 608.0 / rmass;

            Record(RelativeClose(k1, 3.350088890e-4, 1e-7)
                    && RelativeClose(ixz / ix, -3.924646781e-2, 1e-8)
                    && RelativeClose(ixz / iz, -5.349596105e-3, 1e-8)
                    && RelativeClose((iz - ix) / iy, 0.96897131196, 1e-9)
                    && RelativeClose(ixz / iy, -6.001680471e-3, 1e-8),
                "Davison's active constants K1, K5, K7, K9, K10 (PDF p.92) reproduce from these "
                + "values - they are the ones the running model used",
                report, ref passed, ref failed);

            Record(Mathf.Abs(MavF15AfitResearchMassReference.MassKg - 37000f * 0.45359237f) < 1e-2f
                    && Mathf.Abs(profile.massProperties.massKg - MavF15AfitResearchMassReference.MassKg) < 1e-3f,
                "mass " + profile.massProperties.massKg.ToString("F3") + " kg, derived from 37,000 lb",
                report, ref passed, ref failed);

            Record(MavF15InertiaBasis.IsPositiveDefinite(ix, iy, iz, ixz),
                "the research body tensor is positive definite",
                report, ref passed, ref failed);

            double p1, p2, p3;
            MavF15InertiaBasis.GetPrincipalMoments(ix, iy, iz, ixz, out p1, out p2, out p3);
            Record(IsFinite(p1) && IsFinite(p2) && IsFinite(p3) && p1 > 0 && p2 > 0 && p3 > 0
                    && p1 + p2 - p3 > 0,
                "principal moments finite and positive (" + p1.ToString("F2") + " / "
                + p2.ToString("F2") + " / " + p3.ToString("F2")
                + " slug-ft^2) and satisfy the rigid-body triangle inequality (margin "
                + (p1 + p2 - p3).ToString("F2") + ")",
                report, ref passed, ref failed);

            // The shared math is the audited exact math: same inputs, identical output.
            MavMassProperties exact = MavF15MassReference.CreateUnityMassProperties(Vector3.zero);
            MavMassProperties viaHelper = MavF15InertiaBasis.CreateUnityMassProperties(
                MavF15MassReference.MassKg, MavF15MassReference.IxKgM2, MavF15MassReference.IyKgM2,
                MavF15MassReference.IzKgM2, MavF15MassReference.IxzKgM2, Vector3.zero);
            Record(exact.massKg == viaHelper.massKg
                    && exact.inertiaTensorKgM2 == viaHelper.inertiaTensorKgM2
                    && exact.inertiaTensorRotationEulerDeg == viaHelper.inertiaTensorRotationEulerDeg,
                "MavF15InertiaBasis reproduces the audited exact conversion bit-for-bit on the exact "
                + "inputs - the research state uses the same math, not a new one",
                report, ref passed, ref failed);

            Vector3 inertia = profile.massProperties.inertiaTensorKgM2;
            double a = MavF15AfitResearchMassReference.IzKgM2;
            double d = MavF15AfitResearchMassReference.IxKgM2;
            double bb = MavF15AfitResearchMassReference.IxzKgM2;
            Record(Mathf.Abs(inertia.x - MavF15AfitResearchMassReference.IyKgM2) < 1f
                    && RelativeClose(inertia.y + (double)inertia.z, a + d, 1e-5)
                    && RelativeClose((double)inertia.y * inertia.z, a * d - bb * bb, 1e-4)
                    && profile.massProperties.inertiaTensorRotationEulerDeg.y == 0f
                    && profile.massProperties.inertiaTensorRotationEulerDeg.z == 0f,
                "Unity X takes Iyy; the coupled block keeps its trace and determinant; the rotation "
                + "is about Unity X only ("
                + profile.massProperties.inertiaTensorRotationEulerDeg.x.ToString("F4") + " deg)",
                report, ref passed, ref failed);

            Record(profile.massProperties.centerOfMassLocalM == Vector3.zero,
                "center of mass at the local origin - a declared simulation reference choice (the "
                + "model's CG is its own moment reference), not a sourced position",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [P3]

        private static void ValidateEnvelope(
            MavFlightDynamicsProfile profile, StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P3] Envelope fixed to " + MavF15AfitResearchIdentity.SourceConditionLabel);

            MavFlightDynamicsEnvelope e = profile.envelope;
            Record(Mathf.Abs(e.minMach - 0.599f) < 1e-5f && Mathf.Abs(e.maxMach - 0.601f) < 1e-5f
                    && e.alphaMinDeg == MavF15BaumannMach06Domain.SourceAlphaMinDeg
                    && e.alphaMaxDeg == MavF15BaumannMach06Domain.SourceAlphaMaxDeg
                    && e.betaMaxDeg == MavF15BaumannMach06Domain.SourceAbsBetaMaxDeg
                    && e.betaMinDeg == -MavF15BaumannMach06Domain.SourceAbsBetaMaxDeg,
                "Mach [" + e.minMach.ToString("F3") + ", " + e.maxMach.ToString("F3")
                + "], alpha [" + e.alphaMinDeg.ToString("F1") + ", " + e.alphaMaxDeg.ToString("F1")
                + "] deg, beta +/-" + e.betaMaxDeg.ToString("F1")
                + " deg - the declared transcription span, not broadened",
                report, ref passed, ref failed);

            MavFlightState at;
            MavAtmosphereSample atmosphere;
            MavF15ResearchContaminationValidation.SourceCondition(5f, out at, out atmosphere);

            MavFlightState slow = at; slow.mach = 0.5f;
            MavFlightState fast = at; fast.mach = 0.7f;
            MavFlightState highAlpha = at; highAlpha.alphaRad = 95f * Mathf.Deg2Rad;
            MavFlightState highBeta = at; highBeta.betaRad = 25f * Mathf.Deg2Rad;
            Record(e.Contains(at) && !e.Contains(slow) && !e.Contains(fast)
                    && !e.Contains(highAlpha) && !e.Contains(highBeta),
                "the envelope contains the source condition and excludes M 0.5, M 0.7, alpha 95 "
                + "and beta 25",
                report, ref passed, ref failed);

            GameObject host = new GameObject("MavF15ResearchEnvelopeAeroHost");
            host.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                MavF15AeroModel aero = host.AddComponent<MavF15AeroModel>();
                aero.sourceMode = MavF15AeroSourceMode.BaumannMach06SixAxisResearch;
                aero.allowCrossValidationResearchModel = true;

                aero.Evaluate(at, new MavControlInput(), atmosphere);
                bool atOk = !aero.debugRefused;

                MavAtmosphereSample low = MavAtmosphereModel.Sample(5000f);
                MavFlightState lowState = at;
                aero.Evaluate(lowState, new MavControlInput(), low);
                bool lowRefused = aero.debugRefused;
                string lowWhy = aero.debugStatus;

                aero.Evaluate(fast, new MavControlInput(), atmosphere);
                bool fastRefused = aero.debugRefused;

                aero.Evaluate(highAlpha, new MavControlInput(), atmosphere);
                bool alphaRefused = aero.debugRefused;

                aero.Evaluate(highBeta, new MavControlInput(), atmosphere);
                bool betaRefused = aero.debugRefused;

                Record(atOk && lowRefused && fastRefused && alphaRefused && betaRefused,
                    "the research aero evaluates at the source condition and refuses at 5,000 m, "
                    + "M 0.7, alpha 95 and beta 25 (" + lowWhy + ")",
                    report, ref passed, ref failed);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        // ---------------------------------------------------------------- [P4]

        private static void ValidateThrust(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P4] Research thrust keeps its source semantic");

            Record(MavF15AfitResearchThrustSource.SourceTotalThrustLbf == 8300f
                    && Mathf.Abs(MavF15AfitResearchThrustSource.TotalThrustN
                        - 8300f * 4.4482216152605f) < 1e-2f
                    && Mathf.Abs(MavF15AfitResearchThrustSource.ThrustLineOffsetM - 0.00635f) < 1e-7f,
                "8,300 lbf TOTAL aircraft thrust = " + MavF15AfitResearchThrustSource.TotalThrustN.ToString("F2")
                + " N; thrust-line offset 0.25 in = 0.00635 m",
                report, ref passed, ref failed);

            MavFlightState at;
            MavAtmosphereSample atmosphere;
            MavF15ResearchContaminationValidation.SourceCondition(5f, out at, out atmosphere);

            MavPropulsiveLoads loads;
            string reason;
            bool ok = MavF15AfitResearchThrustSource.TryEvaluate(
                MavF15AfitResearchIdentity.ConfigurationId, at, atmosphere, out loads, out reason);

            float expectedMoment = MavF15AfitResearchThrustSource.TotalThrustN
                * MavF15AfitResearchThrustSource.ThrustLineOffsetM;
            Record(ok
                    && loads.forceAeroBodyN.x == MavF15AfitResearchThrustSource.TotalThrustN
                    && loads.forceAeroBodyN.y == 0f && loads.forceAeroBodyN.z == 0f,
                "at the source condition: one total force along body +X (CX += THRUST/QBARS)",
                report, ref passed, ref failed);

            Record(ok && loads.momentAeroBodyNm.x == 0f && loads.momentAeroBodyNm.z == 0f
                    && Mathf.Abs(loads.momentAeroBodyNm.y - expectedMoment) < 1e-3f
                    && loads.momentAeroBodyNm.y > 0f,
                "and the source's nose-up thrust-line moment THRUST x 0.25 in = "
                + loads.momentAeroBodyNm.y.ToString("F3") + " N m about +body Y (CMM += "
                + "THRUST*(0.25/12)/(QBARS*CWING))",
                report, ref passed, ref failed);

            Record(ok && !loads.hasAuthoritativeData && loads.contributingEngineCount == 0
                    && loads.reportedThrustN == MavF15AfitResearchThrustSource.TotalThrustN,
                "not authoritative data for any aircraft; no engine count invented (the source "
                + "models one total force)",
                report, ref passed, ref failed);

            MavAtmosphereSample low = MavAtmosphereModel.Sample(5000f);
            MavPropulsiveLoads lowLoads;
            bool lowOk = MavF15AfitResearchThrustSource.TryEvaluate(
                MavF15AfitResearchIdentity.ConfigurationId, at, low, out lowLoads, out reason);
            MavFlightState fast = at; fast.mach = 0.7f;
            MavPropulsiveLoads fastLoads;
            bool fastOk = MavF15AfitResearchThrustSource.TryEvaluate(
                MavF15AfitResearchIdentity.ConfigurationId, fast, atmosphere, out fastLoads, out reason);
            Record(!lowOk && lowLoads.forceAeroBodyN == Vector3.zero
                    && !fastOk && fastLoads.forceAeroBodyN == Vector3.zero,
                "no thrust at 5,000 m or at M 0.7 - not a function of altitude or Mach, and not "
                + "extrapolated",
                report, ref passed, ref failed);

            GameObject host = new GameObject("MavF15ResearchThrustHost");
            host.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                MavF15AfitResearchFixedThrust thrust = host.AddComponent<MavF15AfitResearchFixedThrust>();
                Record(host.GetComponent<MavF15AfitResearchFlightDynamicsProfile>() != null,
                    "the thrust component brings the research profile with it (RequireComponent)",
                    report, ref passed, ref failed);

                MavPropulsiveLoads idle = thrust.Evaluate(at, atmosphere, 0f, 0.02f);
                MavPropulsiveLoads full = thrust.Evaluate(at, atmosphere, 1f, 0.02f);
                Record(idle.forceAeroBodyN == full.forceAeroBodyN
                        && idle.forceAeroBodyN.x == MavF15AfitResearchThrustSource.TotalThrustN
                        && thrust.debugStatus.Contains("throttle ignored"),
                    "throttle 0 and throttle 1 give the same force - the throttle is ignored and "
                    + "that is reported: " + thrust.debugStatus,
                    report, ref passed, ref failed);

                Record(!thrust.HasAuthoritativeData && !thrust.IsAcceptableForLiveFlight
                        && thrust.PropulsionModelName.Contains("NOT F100")
                        && thrust.PropulsionModelName.Contains("NOT NASA 836"),
                    "the component is non-authoritative and not acceptable for live flight: "
                    + thrust.PropulsionModelName,
                    report, ref passed, ref failed);

                // Dropped onto a body flying the EXACT profile, it must produce nothing.
                MavSixDoFBody body = host.AddComponent<MavSixDoFBody>();
                MavF15FlightDynamicsProfile exactProvider = host.AddComponent<MavF15FlightDynamicsProfile>();
                body.profileProvider = exactProvider;
                MavPropulsiveLoads underExact = thrust.Evaluate(at, atmosphere, 1f, 0.02f);
                Record(underExact.forceAeroBodyN == Vector3.zero && thrust.debugRefused,
                    "on a body using the exact NASA 836 profile it refuses: " + thrust.debugStatus,
                    report, ref passed, ref failed);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        // ---------------------------------------------------------------- [P5]

        private static void ValidateExactPathUnchanged(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P5] The exact NASA 836 path did not change");

            GameObject host = new GameObject("MavF15ExactUnchangedHost");
            host.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                MavFlightDynamicsProfile exact =
                    host.AddComponent<MavF15FlightDynamicsProfile>().BuildProfile();
                string reason;
                Record(!exact.IsValid(out reason) && reason == "reference geometry is invalid"
                        && exact.profileId != MavF15AfitResearchIdentity.ConfigurationId,
                    "the exact profile is still invalid: " + reason,
                    report, ref passed, ref failed);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }

            MavAeroReferenceGeometry g = MavF15ReferenceData.CreateExactTargetGeometry();
            Record(g.wingAreaM2 == 0f && g.wingSpanM == 0f && g.meanAerodynamicChordM == 0f,
                "exact S / cbar / reference b are still zero",
                report, ref passed, ref failed);

            Record(Mathf.Abs(MavF15MassReference.MassKg - 37426f * 0.45359237f) < 1e-2f
                    && MavF15MassReference.IxzSlugFt2 == -5070f,
                "the exact mass state is still Table 1 Baseline (37,426 lb, Ixz -5,070)",
                report, ref passed, ref failed);

            MavF100Nasa836TargetAnchor anchor = MavF100Nasa836TargetPropulsion.Anchors[0];
            MavF100NetThrustFractionResult shape = new MavF100NetThrustFractionResult();
            shape.support = MavF100ThrustSupport.Supported;
            shape.netThrustFraction = 1f;
            shape.quantity = MavF100ThrustQuantity.UninstalledNetThrust;
            MavF100PathCombination gate = MavF100PathSeparation.DimensionalizeForTarget(
                shape, anchor, MavF100ConfigurationEquivalence.Unproven);
            Record(anchor.sourceValue == 23500f && !gate.permitted,
                "the NASA 836 anchor is still 23,500 lbf and the dimensionalizing gate still "
                + "refuses: " + gate.reason,
                report, ref passed, ref failed);

            MavEngineProfile engine = MavF15PropulsionSkeleton.CreateUnfrozenEngineProfile();
            Record(engine.provenance == MavEngineDataProvenance.Unavailable,
                "the exact F100 engine profile's provenance is still Unavailable",
                report, ref passed, ref failed);
            Object.DestroyImmediate(engine);
        }

        // ---------------------------------------------------------------- [P6]

        private static void ValidateLoadPathAndReadiness(
            MavFlightDynamicsProfile profile, StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[P6] Six-DoF-compatible load path and structural readiness");

            MavFlightState at;
            MavAtmosphereSample atmosphere;
            MavF15ResearchContaminationValidation.SourceCondition(5f, out at, out atmosphere);

            GameObject host = new GameObject("MavF15ResearchLoadPathHost");
            host.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                MavF15AeroModel aero = host.AddComponent<MavF15AeroModel>();
                aero.sourceMode = MavF15AeroSourceMode.BaumannMach06SixAxisResearch;
                aero.allowCrossValidationResearchModel = true;
                MavAeroCoefficients coefficients = aero.Evaluate(at, new MavControlInput(), atmosphere);

                // The same two calls MavSixDoFBody makes: dimensionalize with the profile's
                // geometry, then sum aero and propulsion into one load set.
                MavAerodynamicLoads aeroLoads = MavFlightDynamicsMath.Dimensionalize(
                    coefficients, profile.referenceGeometry, at.dynamicPressurePa);

                MavPropulsiveLoads thrust;
                string reason;
                MavF15AfitResearchThrustSource.TryEvaluate(
                    profile.profileId, at, atmosphere, out thrust, out reason);

                MavFlightDynamicsLoadSet set = new MavFlightDynamicsLoadSet();
                set.BeginStep(1);
                bool aeroAdded = set.AddAerodynamic(aeroLoads);
                bool thrustAdded = set.AddPropulsive(thrust);

                Record(!aero.debugRefused && aeroAdded && thrustAdded
                        && set.aerodynamicContributions == 1 && set.propulsiveContributions == 1,
                    "research aero and research thrust each contribute exactly once",
                    report, ref passed, ref failed);

                Vector3 expectedForce = aeroLoads.forceAeroBodyN + thrust.forceAeroBodyN;
                Vector3 expectedMoment = aeroLoads.momentAeroBodyNm + thrust.momentAeroBodyNm;
                Record(set.IsFinite()
                        && (set.totalForceAeroBodyN - expectedForce).sqrMagnitude < 1e-6f
                        && (set.totalMomentAeroBodyNm - expectedMoment).sqrMagnitude < 1e-6f,
                    "the summed load set is finite and equals aero + thrust: force "
                    + set.totalForceAeroBodyN.ToString("F1") + " N, moment "
                    + set.totalMomentAeroBodyNm.ToString("F1") + " N m (no tolerance on the "
                    + "values themselves is claimed)",
                    report, ref passed, ref failed);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }

            // The generic structural rule, applied to each profile with every part present.
            string researchReason, exactReason;
            MavFlightDynamicsReadinessInputs inputs = new MavFlightDynamicsReadinessInputs();
            inputs.hasRigidbody = true;
            inputs.hasAerodynamicModel = true;
            inputs.hasControlSurfaceActuator = true;
            inputs.hasControlLaw = true;
            inputs.hasPropulsionModel = true;

            inputs.hasValidProfile = profile.IsValid(out researchReason);
            MavFlightDynamicsReadinessReport research = MavFlightDynamicsReadiness.Evaluate(inputs);

            GameObject exactHost = new GameObject("MavF15ExactReadinessHost");
            exactHost.hideFlags = HideFlags.HideAndDontSave;
            MavFlightDynamicsReadinessReport exact;
            try
            {
                inputs.hasValidProfile = exactHost.AddComponent<MavF15FlightDynamicsProfile>()
                    .BuildProfile().IsValid(out exactReason);
                exact = MavFlightDynamicsReadiness.Evaluate(inputs);
            }
            finally
            {
                Object.DestroyImmediate(exactHost);
            }

            Record(research.structurallyPrepared && !research.operationallyLiveReady,
                "with every part present, the research profile reaches STRUCTURALLY_PREPARED and "
                + "not live-ready: " + research.summary,
                report, ref passed, ref failed);

            Record(!exact.structurallyPrepared,
                "the same parts around the exact profile stay NOT_PREPARED: " + exact.summary,
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- helpers

        private static MavF15GeometryCandidate Find(MavF15GeometryCandidate[] all, string id)
        {
            for (int i = 0; i < all.Length; i++)
                if (all[i].id == id) return all[i];
            return new MavF15GeometryCandidate();
        }

        private static bool RelativeClose(double actual, double expected, double tolerance)
        {
            double scale = System.Math.Max(1e-30, System.Math.Abs(expected));
            return System.Math.Abs(actual - expected) <= tolerance * scale;
        }

        private static bool IsFinite(double v)
        {
            return !double.IsNaN(v) && !double.IsInfinity(v);
        }

        private static void Record(
            bool condition, string label,
            StringBuilder report, ref int passed, ref int failed)
        {
            if (condition) passed++; else failed++;
            report.Append(condition ? "  PASS  " : "  FAIL  ").AppendLine(label);
        }
    }
}
