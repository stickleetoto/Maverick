using System;
using System.Reflection;
using System.Text;
using UnityEngine;
using MaverickFresh.FlightDynamics.F15;
using Object = UnityEngine.Object;

namespace MaverickFresh.FlightDynamics.Validation
{
    /// <summary>
    /// Cross-module guards that keep the F-15 RESEARCH path from contaminating the exact NASA 836
    /// path.
    ///
    /// Written BEFORE the research profile existed, deliberately. Sections [X1]-[X5] exercise
    /// only gates that were already in the code; they passed then and must keep passing now that
    /// a research profile, research mass state and research thrust exist beside them. [X6] adds
    /// the guards that can only be written against the research types themselves.
    ///
    /// Covered:
    ///   [X1] research geometry cannot reach the exact coefficient reference set
    ///   [X2] research mass cannot replace or impersonate the exact mass state
    ///   [X3] research thrust cannot reach the F100 target path, its anchor, or its gate
    ///   [X4] research aerodynamics need an explicit opt-in; the exact default stays zero
    ///   [X5] family / research FCS modes can never satisfy the exact-836 provenance floor
    ///   [X6] the research identity, mass state and thrust are separate by construction
    /// </summary>
    public static class MavF15ResearchContaminationValidation
    {
        public static string RunAll(out int passed, out int failed)
        {
            passed = 0;
            failed = 0;
            StringBuilder report = new StringBuilder(4096);
            report.AppendLine("F-15 Research / Exact-836 Contamination Guards");
            report.AppendLine("==============================================");

            ValidateGeometry(report, ref passed, ref failed);
            ValidateMass(report, ref passed, ref failed);
            ValidateThrust(report, ref passed, ref failed);
            ValidateAero(report, ref passed, ref failed);
            ValidateFcs(report, ref passed, ref failed);
            ValidateResearchIdentity(report, ref passed, ref failed);

            report.AppendLine();
            report.Append("RESULT: ")
                .Append(failed == 0 ? "PASS" : "FAIL")
                .Append("  passed=").Append(passed)
                .Append(" failed=").Append(failed);

            return report.ToString();
        }

        // ---------------------------------------------------------------- [X1]

        private static void ValidateGeometry(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[X1] Research geometry cannot reach the exact reference set");

            MavAeroReferenceGeometry research = MavF15BaumannMach06Reference.CreateReferenceGeometry();
            MavAeroReferenceGeometry exact = MavF15ReferenceData.CreateExactTargetGeometry();

            Record(research.wingAreaM2 > 0f && research.meanAerodynamicChordM > 0f
                    && research.wingSpanM > 0f,
                "the research reference set is populated (608 ft^2 / 15.94 ft / 42.8 ft)",
                report, ref passed, ref failed);

            Record(exact.wingAreaM2 == 0f && exact.meanAerodynamicChordM == 0f
                    && exact.wingSpanM == 0f,
                "while the exact NASA 836 S, cbar and reference b are all still zero",
                report, ref passed, ref failed);

            MavF15CoefficientReferenceSet set;
            string reason;
            Record(!MavF15ReferenceGeometrySources.TrySelectExactTargetReferenceSet(out set, out reason),
                "the exact-target selector still closes no reference set: " + reason,
                report, ref passed, ref failed);

            // Offer the research set to the exact selector as a complete, single-source set.
            MavF15GeometryCandidate[] all = MavF15ReferenceGeometrySources.All;
            int aro10 = 0;
            for (int i = 0; i < all.Length; i++)
                if (all[i].referenceSetId == MavF15ReferenceGeometrySources.Aro10ReferenceSetId) aro10++;

            MavF15GeometryCandidate[] researchOnly = new MavF15GeometryCandidate[aro10];
            int k = 0;
            for (int i = 0; i < all.Length; i++)
                if (all[i].referenceSetId == MavF15ReferenceGeometrySources.Aro10ReferenceSetId)
                    researchOnly[k++] = all[i];

            Record(aro10 >= 3
                    && !MavF15ReferenceGeometrySources.TrySelectReferenceSet(
                        researchOnly, out set, out reason),
                "the research (ARO10) set, offered alone and complete, is refused by the exact "
                + "selector",
                report, ref passed, ref failed);

            Record(MavF15ReferenceData.TargetConfigurationId.Contains("NASA_F15B_836")
                    && !MavF15BaumannMach06Reference.ModelId.Contains("NASA_F15B_836"),
                "the research model id does not carry the NASA 836 target identity",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [X2]

        private static void ValidateMass(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[X2] Research mass cannot replace the exact mass state");

            Record(MavF15MassReference.SelectedState == MavF15MassStateKind.Baseline
                    && MavF15Table1MassStates.Baseline.Matches(
                        MavF15MassReference.WeightLb, MavF15MassReference.XcgPercentMac,
                        MavF15MassReference.IxSlugFt2, MavF15MassReference.IySlugFt2,
                        MavF15MassReference.IzSlugFt2, MavF15MassReference.IxzSlugFt2),
                "the exact mass reference is still Table 1's Baseline column",
                report, ref passed, ref failed);

            Record(MavF15ReferenceData.TargetMassStateId == "NASA_F15B_836_BASELINE_8K_FUEL_MASS_STATE",
                "and its mass-state id is unchanged",
                report, ref passed, ref failed);

            MavMassProperties exactProps = MavF15MassReference.CreateUnityMassProperties(Vector3.zero);
            Record(Mathf.Abs(exactProps.massKg - MavF15MassReference.MassKg) < 1e-3f,
                "the exact Unity mass properties still derive from the exact mass reference",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [X3]

        private static void ValidateThrust(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[X3] Research thrust cannot reach the F100 target path");

            MavF100Nasa836TargetAnchor[] anchors = MavF100Nasa836TargetPropulsion.Anchors;
            Record(anchors.Length == 1
                    && anchors[0].sourceValue == 23500f
                    && anchors[0].precision == MavF100ValuePrecision.Approximate
                    && anchors[0].engineFamily == MavF100EngineFamily.Nasa836Target,
                "the NASA 836 anchor list is exactly the one approximate 23,500 lbf anchor",
                report, ref passed, ref failed);

            Record(!MavF100Nasa836EngineEvidence.SubConfigurationKnown,
                "836's engine sub-configuration is still recorded as unknown",
                report, ref passed, ref failed);

            // A research-scale thrust dressed up as an anchor, in every engine family, with a
            // forged all-dimension equivalence: the gate must still refuse.
            MavF100NetThrustFractionResult characteristic = new MavF100NetThrustFractionResult();
            characteristic.support = MavF100ThrustSupport.Supported;
            characteristic.netThrustFraction = 1f;
            characteristic.quantity = MavF100ThrustQuantity.UninstalledNetThrust;
            characteristic.panel = "forged";

            MavF100ConfigurationEquivalence forged = MavF100ConfigurationEquivalence.Proving(
                MavF100EquivalenceDimension.SameDesignation
                | MavF100EquivalenceDimension.SameGasPath
                | MavF100EquivalenceDimension.SameControlSchedule
                | MavF100EquivalenceDimension.SamePerformanceDeck,
                "forged by the contamination guard");

            bool allRefused = true;
            foreach (MavF100EngineFamily family in Enum.GetValues(typeof(MavF100EngineFamily)))
            {
                MavF100Nasa836TargetAnchor anchor = new MavF100Nasa836TargetAnchor();
                anchor.quantityName = "research fixed total thrust (forged)";
                anchor.quantity = MavF100ThrustQuantity.UninstalledNetThrust;
                anchor.newtons = 8300f * 4.4482216152605f;
                anchor.sourceValue = 8300f;
                anchor.sourceUnits = "lbf";
                anchor.condition = "M 0.6 / 20,000 ft research source condition";
                anchor.precision = MavF100ValuePrecision.Stated;
                anchor.citation = "Davison AFIT/GAE/ENY/92M-01 driver (research model)";
                anchor.targetIdentity = "F15_AFIT_RESEARCH";
                anchor.engineFamily = family;
                anchor.sourceClass = MavF100SourceClass.CrossValidationOnly;

                MavF100PathCombination result = MavF100PathSeparation.DimensionalizeForTarget(
                    characteristic, anchor, forged);
                if (result.permitted)
                    allRefused = false;
            }

            Record(allRefused,
                "a research thrust forged as a target anchor, in every engine family and with a "
                + "forged full equivalence, is refused by DimensionalizeForTarget",
                report, ref passed, ref failed);

            // No F100 type may carry the research figure as a number.
            string carrier = FindMavF100Constant(8300f, 8300f * 4.4482216152605f);
            Record(carrier == null,
                "no MavF100* type carries 8,300 lbf (or its newton equivalent) as a constant"
                + (carrier == null ? "" : " - found in " + carrier),
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [X4]

        private static void ValidateAero(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[X4] Research aero needs an explicit opt-in; the exact default stays zero");

            Record(default(MavF15AeroSourceMode) == MavF15AeroSourceMode.ExactNasa836Unavailable,
                "the default aero source mode is ExactNasa836Unavailable",
                report, ref passed, ref failed);

            GameObject host = new GameObject("MavF15ContaminationAeroHost");
            host.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                MavF15AeroModel aero = host.AddComponent<MavF15AeroModel>();
                Record(aero.sourceMode == MavF15AeroSourceMode.ExactNasa836Unavailable
                        && !aero.allowCrossValidationResearchModel,
                    "a freshly added MavF15AeroModel starts in the exact-unavailable mode, opt-in "
                    + "off",
                    report, ref passed, ref failed);

                MavFlightState state;
                MavAtmosphereSample atmosphere;
                SourceCondition(5f, out state, out atmosphere);

                MavAeroCoefficients exactCoeffs = aero.Evaluate(state, new MavControlInput(), atmosphere);
                Record(aero.debugRefused && IsZero(exactCoeffs),
                    "in the exact mode, even AT the research source condition, coefficients are "
                    + "zero: " + aero.debugStatus,
                    report, ref passed, ref failed);

                aero.sourceMode = MavF15AeroSourceMode.BaumannMach06SixAxisResearch;
                aero.allowCrossValidationResearchModel = false;
                MavAeroCoefficients noOptIn = aero.Evaluate(state, new MavControlInput(), atmosphere);
                Record(aero.debugRefused && IsZero(noOptIn),
                    "a research mode WITHOUT the explicit opt-in is refused: " + aero.debugStatus,
                    report, ref passed, ref failed);

                aero.allowCrossValidationResearchModel = true;
                MavAeroCoefficients withOptIn = aero.Evaluate(state, new MavControlInput(), atmosphere);
                Record(!aero.debugRefused && !IsZero(withOptIn)
                        && aero.debugStatus.Contains(MavF15BaumannMach06Reference.ModelId),
                    "with the opt-in, at the source condition, it evaluates - and labels itself "
                    + "with the CROSS_VALIDATION model id: " + aero.debugStatus,
                    report, ref passed, ref failed);

                Record(MavF15BaumannMach06Reference.ModelId.Contains("CROSS_VALIDATION"),
                    "the research aero model id is permanently tagged CROSS_VALIDATION",
                    report, ref passed, ref failed);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        // ---------------------------------------------------------------- [X5]

        private static void ValidateFcs(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[X5] Family / research FCS can never satisfy the exact-836 floor");

            Record(MavF15FcsModes.ProvenanceFloor(MavF15FcsMode.ExactNasa836Unavailable)
                    == MavEngineDataProvenance.Authoritative,
                "the exact-836 FCS mode admits only Authoritative gains",
                report, ref passed, ref failed);

            Record(MavF15FcsModes.ProvenanceFloor(MavF15FcsMode.F15FamilyReference)
                        < MavEngineDataProvenance.Authoritative
                    && MavF15FcsModes.ProvenanceFloor(MavF15FcsMode.AFITResearch)
                        < MavEngineDataProvenance.Authoritative,
                "the family and research modes sit below that floor",
                report, ref passed, ref failed);

            Record(!MavF15FcsModes.Admits(MavF15FcsMode.ExactNasa836Unavailable,
                        MavEngineDataProvenance.PublicReference)
                    && !MavF15FcsModes.Admits(MavF15FcsMode.ExactNasa836Unavailable,
                        MavEngineDataProvenance.CrossValidationOnly),
                "family-grade (PublicReference) and research-grade (CrossValidationOnly) gains are "
                + "never admitted by the exact mode",
                report, ref passed, ref failed);

            MavF15ControlGain family = new MavF15ControlGain
            {
                value = 1f,
                provenance = MavEngineDataProvenance.PublicReference,
                sourceNote = "contamination-guard fixture"
            };
            MavF15ControlGain research = new MavF15ControlGain
            {
                value = 1f,
                provenance = MavEngineDataProvenance.CrossValidationOnly,
                sourceNote = "contamination-guard fixture"
            };
            Record(!family.IsExactTargetAuthority && !research.IsExactTargetAuthority,
                "neither a family nor a research gain can present itself as exact-target authority",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [X6]

        private static void ValidateResearchIdentity(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[X6] Research identity, mass state and thrust are separate by construction");

            string id = MavF15AfitResearchIdentity.ConfigurationId;
            Record(!string.IsNullOrEmpty(id)
                    && id != MavF15ReferenceData.TargetConfigurationId
                    && MavF15AfitResearchIdentity.CarriesNoExactTargetToken(id),
                "the research configuration id '" + id + "' is not the exact id and carries none "
                + "of NASA_F15B_836 / 74-0141 / PRE_QUIET_SPIKE / EXACT",
                report, ref passed, ref failed);

            Record(!MavF15AfitResearchIdentity.CarriesNoExactTargetToken(
                        MavF15ReferenceData.TargetConfigurationId),
                "and the token check genuinely detects the exact id",
                report, ref passed, ref failed);

            Record(MavF15AfitResearchMassReference.ResearchMassStateId
                        != MavF15ReferenceData.TargetMassStateId
                    && MavF15AfitResearchIdentity.CarriesNoExactTargetToken(
                        MavF15AfitResearchMassReference.ResearchMassStateId),
                "the research mass state has its own id, free of exact-target tokens",
                report, ref passed, ref failed);

            Record(!Mathf.Approximately(MavF15AfitResearchMassReference.WeightLb,
                        MavF15MassReference.WeightLb)
                    && !Mathf.Approximately(MavF15AfitResearchMassReference.IxxSlugFt2,
                        MavF15MassReference.IxSlugFt2),
                "the research raw mass values are not the exact ones",
                report, ref passed, ref failed);

            // Build both profiles; building the research one must leave the exact one untouched.
            GameObject host = new GameObject("MavF15ContaminationProfileHost");
            host.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                MavF15AfitResearchFlightDynamicsProfile researchProvider =
                    host.AddComponent<MavF15AfitResearchFlightDynamicsProfile>();
                MavF15FlightDynamicsProfile exactProvider =
                    host.AddComponent<MavF15FlightDynamicsProfile>();

                MavFlightDynamicsProfile researchProfile = researchProvider.BuildProfile();
                MavFlightDynamicsProfile exactProfile = exactProvider.BuildProfile();

                string researchReason, exactReason;
                bool researchValid = researchProfile.IsValid(out researchReason);
                bool exactValid = exactProfile.IsValid(out exactReason);

                Record(researchValid && !exactValid
                        && exactReason == "reference geometry is invalid",
                    "side by side: the research profile is valid, the exact profile still fails "
                    + "closed on geometry",
                    report, ref passed, ref failed);

                Record(researchProfile.profileId != exactProfile.profileId
                        && researchProfile.profileId == MavF15AfitResearchIdentity.ConfigurationId
                        && MavF15AfitResearchIdentity.CarriesNoExactTargetToken(
                            researchProfile.profileId),
                    "the two profile ids cannot alias: '" + researchProfile.profileId + "' vs '"
                    + exactProfile.profileId + "'",
                    report, ref passed, ref failed);

                Record(Mathf.Abs(exactProfile.massProperties.massKg - MavF15MassReference.MassKg) < 1e-3f
                        && Mathf.Abs(researchProfile.massProperties.massKg
                            - MavF15AfitResearchMassReference.MassKg) < 1e-3f,
                    "each profile carries its own mass; the exact provider never selects the "
                    + "research state",
                    report, ref passed, ref failed);

                MavAeroReferenceGeometry exactAfter = MavF15ReferenceData.CreateExactTargetGeometry();
                Record(exactAfter.wingAreaM2 == 0f && exactAfter.meanAerodynamicChordM == 0f
                        && exactAfter.wingSpanM == 0f
                        && exactProfile.referenceGeometry.wingAreaM2 == 0f,
                    "after the research profile is built, the exact geometry is still zero",
                    report, ref passed, ref failed);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }

            // The research thrust lives outside the F100 layer and cannot be asked for under any
            // identity but the research one.
            Record(!typeof(MavF15AfitResearchFixedThrust).Name.StartsWith("MavF100")
                    && !typeof(MavF15AfitResearchThrustSource).Name.StartsWith("MavF100"),
                "the research thrust types are not MavF100* types",
                report, ref passed, ref failed);

            MavFlightState state;
            MavAtmosphereSample atmosphere;
            SourceCondition(5f, out state, out atmosphere);

            MavPropulsiveLoads loads;
            string thrustReason;
            bool underExact = MavF15AfitResearchThrustSource.TryEvaluate(
                MavF15ReferenceData.TargetConfigurationId, state, atmosphere,
                out loads, out thrustReason);
            Record(!underExact && loads.forceAeroBodyN == Vector3.zero,
                "asked for under the exact NASA 836 identity, the research thrust refuses: "
                + thrustReason,
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>The research source condition, from the project's own atmosphere model.</summary>
        public static void SourceCondition(
            float alphaDeg, out MavFlightState state, out MavAtmosphereSample atmosphere)
        {
            atmosphere = MavAtmosphereModel.Sample(MavF15BaumannMach06Reference.SourcePressureAltitudeM);
            float speed = MavF15BaumannMach06Reference.SourceMach * atmosphere.speedOfSoundMps;

            state = new MavFlightState();
            state.mach = MavF15BaumannMach06Reference.SourceMach;
            state.trueAirspeedMps = speed;
            state.alphaRad = alphaDeg * Mathf.Deg2Rad;
            state.betaRad = 0f;
            state.dynamicPressurePa = 0.5f * atmosphere.densityKgM3 * speed * speed;
            state.aeroBodyVelocityMps = new Vector3(
                speed * Mathf.Cos(state.alphaRad), 0f, speed * Mathf.Sin(state.alphaRad));
        }

        private static bool IsZero(MavAeroCoefficients c)
        {
            return c.cx == 0f && c.cy == 0f && c.cz == 0f && c.cl == 0f && c.cm == 0f && c.cn == 0f;
        }

        /// <summary>
        /// Scans every public constant and static float field of every MavF100* type for either
        /// value. Returns the first carrier found, or null.
        /// </summary>
        private static string FindMavF100Constant(float a, float b)
        {
            Assembly assembly = typeof(MavF100SourceData).Assembly;
            foreach (Type type in assembly.GetTypes())
            {
                if (!type.Name.StartsWith("MavF100"))
                    continue;

                FieldInfo[] fields = type.GetFields(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                for (int i = 0; i < fields.Length; i++)
                {
                    if (fields[i].FieldType != typeof(float))
                        continue;

                    float value = (float)fields[i].GetValue(null);
                    if (Mathf.Abs(value - a) < 0.5f || Mathf.Abs(value - b) < 0.5f)
                        return type.Name + "." + fields[i].Name;
                }
            }

            return null;
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
