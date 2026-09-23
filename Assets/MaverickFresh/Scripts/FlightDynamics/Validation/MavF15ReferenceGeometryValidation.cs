using System.Text;
using UnityEngine;
using MaverickFresh.FlightDynamics.F15;

namespace MaverickFresh.FlightDynamics.Validation
{
    /// <summary>
    /// Deterministic checks on the exact-target NASA F-15B 836 aerodynamic reference geometry.
    ///
    /// The audit behind this suite found that no held primary source for NASA 836 prints S,
    /// cbar, a coefficient reference span or a moment reference. The suite therefore mostly
    /// proves that things are REFUSED - and that the refusals are for the right reasons, so the
    /// gate opens for a properly sourced reference set and for nothing else.
    ///
    /// Covered:
    ///   [G0] every source value converts to SI through one constant per dimension
    ///   [G1] the exact-target reference set is unavailable, and the profile fails closed on it
    ///   [G2] reference area / chord / span become positive only under accepted authority
    ///   [G3] the familiar 608 ft^2 / 15.94 ft cannot silently satisfy the exact target
    ///   [G4] the span in the reference geometry is a reference span, never the physical span
    ///   [G5] a moment reference is never a centre of gravity
    ///   [G6] coefficients are dimensionalized by one coherent reference set, never a mixture
    ///   [G7] valid geometry alone would not enable flight - the other gates stay closed
    /// </summary>
    public static class MavF15ReferenceGeometryValidation
    {
        private const string SyntheticSet = "SYNTHETIC_TEST_SET_A";
        private const string OtherSyntheticSet = "SYNTHETIC_TEST_SET_B";

        // Deliberately unlike any F-15 value, so no synthetic fixture can be mistaken for data.
        private const float SyntheticAreaFt2 = 100f;
        private const float SyntheticChordFt = 10f;
        private const float SyntheticSpanFt = 20f;

        public static string RunAll(out int passed, out int failed)
        {
            passed = 0;
            failed = 0;
            StringBuilder report = new StringBuilder(4096);
            report.AppendLine("NASA F-15B 836 Aerodynamic Reference Geometry Validation");
            report.AppendLine("========================================================");

            ValidateSiConversion(report, ref passed, ref failed);
            ValidateExactTargetUnavailable(report, ref passed, ref failed);
            ValidateAuthorityGate(report, ref passed, ref failed);
            ValidateFamilyValuesCannotSatisfyTarget(report, ref passed, ref failed);
            ValidateSpanDefinition(report, ref passed, ref failed);
            ValidateMomentReferenceIsNotCg(report, ref passed, ref failed);
            ValidateCoherentReferenceSet(report, ref passed, ref failed);
            ValidateOtherGatesStayClosed(report, ref passed, ref failed);

            report.AppendLine();
            report.Append("RESULT: ")
                .Append(failed == 0 ? "PASS" : "FAIL")
                .Append("  passed=").Append(passed)
                .Append(" failed=").Append(failed);

            return report.ToString();
        }

        // ---------------------------------------------------------------- [G0]

        private static void ValidateSiConversion(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[G0] Source value -> SI conversion");

            Record(
                MavF15ReferenceGeometrySources.FootToM == 0.3048f
                && Mathf.Abs(MavF15ReferenceGeometrySources.SquareFootToM2
                    - MavF15ReferenceGeometrySources.FootToM
                        * MavF15ReferenceGeometrySources.FootToM) < 1e-9f,
                "one exact constant per dimension: 1 ft = 0.3048 m, and 1 ft^2 is its square",
                report, ref passed, ref failed);

            MavF15GeometryCandidate[] all = MavF15ReferenceGeometrySources.All;
            bool allConvert = true;
            int converted = 0;
            for (int i = 0; i < all.Length; i++)
            {
                MavF15GeometryCandidate c = all[i];
                if (c.siValue == 0f)
                    continue;

                float factor;
                if (c.sourceUnits == "ft^2")
                    factor = MavF15ReferenceGeometrySources.SquareFootToM2;
                else if (c.sourceUnits == "ft")
                    factor = MavF15ReferenceGeometrySources.FootToM;
                else
                {
                    allConvert = false;
                    continue;
                }

                converted++;
                if (Mathf.Abs(c.siValue - c.sourceValue * factor) > 1e-5f * c.siValue)
                    allConvert = false;
            }

            Record(allConvert && converted > 0,
                "every candidate carrying an SI value derives it from its printed value ("
                + converted + " candidates)",
                report, ref passed, ref failed);

            Record(
                Mathf.Abs(MavF15ReferenceData.PhysicalWingSpanM - 13.04544f) < 1e-4f
                && Mathf.Abs(MavF15ReferenceData.AircraftLengthM - 19.41576f) < 1e-4f
                && Mathf.Abs(MavF15ReferenceData.AircraftHeightM - 5.69976f) < 1e-4f,
                "the physical dimensions now derive from the printed 42.8 / 63.7 / 18.7 ft and "
                + "reproduce the previously frozen metres exactly - the VALUES did not change, "
                + "only the span's role",
                report, ref passed, ref failed);

            MavF15GeometryCandidate area = Find(all, "NF15B837_S");
            MavF15GeometryCandidate chord = Find(all, "NF15B837_CBAR");
            Record(
                Mathf.Abs(area.siValue - 56.48505f) < 1e-3f
                && Mathf.Abs(chord.siValue - 4.858512f) < 1e-5f,
                "608 ft^2 -> " + area.siValue.ToString("0.00000") + " m^2 and 15.94 ft -> "
                + chord.siValue.ToString("0.000000") + " m (converted for the record; graded "
                + "Incompatible below)",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [G1]

        private static void ValidateExactTargetUnavailable(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[G1] The exact-target coefficient reference set is UNAVAILABLE");

            MavF15CoefficientReferenceSet set;
            string reason;
            bool selected =
                MavF15ReferenceGeometrySources.TrySelectExactTargetReferenceSet(
                    out set, out reason);

            Record(!selected && !set.IsComplete,
                "the held sources close no reference set: " + reason,
                report, ref passed, ref failed);

            MavAeroReferenceGeometry geometry = MavF15ReferenceData.CreateExactTargetGeometry();
            Record(
                geometry.wingAreaM2 == 0f && geometry.meanAerodynamicChordM == 0f
                && geometry.wingSpanM == 0f,
                "CreateExactTargetGeometry returns S = 0, cbar = 0 and reference b = 0",
                report, ref passed, ref failed);

            MavF15GeometryCandidate[] all = MavF15ReferenceGeometrySources.All;
            string[] missing =
            {
                "NASA836_S", "NASA836_CBAR", "NASA836_B_REFERENCE", "NASA836_MOMENT_REFERENCE"
            };
            bool allUnavailable = true;
            for (int i = 0; i < missing.Length; i++)
            {
                MavF15GeometryCandidate c = Find(all, missing[i]);
                if (c.id != missing[i]
                    || c.authority != MavF15GeometryAuthority.Unavailable
                    || c.siValue != 0f)
                    allUnavailable = false;
            }

            Record(allUnavailable,
                "NASA 836 S, cbar, reference b and moment reference are recorded as "
                + "Unavailable, with no value",
                report, ref passed, ref failed);

            MavFlightDynamicsProfile profile = new MavFlightDynamicsProfile();
            profile.profileId = "f15-geometry-validation";
            profile.referenceGeometry = geometry;
            profile.massProperties = MavF15MassReference.CreateUnityMassProperties(Vector3.zero);

            string profileReason;
            bool valid = profile.IsValid(out profileReason);
            Record(!valid && profileReason == "reference geometry is invalid",
                "a profile built on it fails closed on geometry: " + profileReason,
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [G2]

        private static void ValidateAuthorityGate(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[G2] Reference geometry becomes positive only under accepted authority");

            MavF15GeometryAuthority[] grades =
            {
                MavF15GeometryAuthority.Unavailable,
                MavF15GeometryAuthority.Incompatible,
                MavF15GeometryAuthority.CrossValidationOnly,
                MavF15GeometryAuthority.F15FamilySupport,
                MavF15GeometryAuthority.ExplicitProductionReferenceUsedBy836Model,
                MavF15GeometryAuthority.DirectExact836
            };

            for (int g = 0; g < grades.Length; g++)
            {
                MavF15GeometryCandidate[] fixture = SyntheticSet3(grades[g], SyntheticSet);

                MavF15CoefficientReferenceSet set;
                string reason;
                bool selected =
                    MavF15ReferenceGeometrySources.TrySelectReferenceSet(
                        fixture, out set, out reason);

                bool shouldAccept =
                    grades[g] == MavF15GeometryAuthority.DirectExact836
                    || grades[g]
                        == MavF15GeometryAuthority.ExplicitProductionReferenceUsedBy836Model;

                bool correct;
                if (shouldAccept)
                {
                    correct = selected
                        && set.areaM2 > 0f && set.chordM > 0f && set.spanM > 0f
                        && Mathf.Abs(set.areaM2
                            - SyntheticAreaFt2 * MavF15ReferenceGeometrySources.SquareFootToM2)
                            < 1e-4f
                        && Mathf.Abs(set.chordM
                            - SyntheticChordFt * MavF15ReferenceGeometrySources.FootToM) < 1e-5f
                        && Mathf.Abs(set.spanM
                            - SyntheticSpanFt * MavF15ReferenceGeometrySources.FootToM) < 1e-5f;
                }
                else
                {
                    correct = !selected
                        && set.areaM2 == 0f && set.chordM == 0f && set.spanM == 0f;
                }

                Record(correct,
                    grades[g] + ": a complete synthetic set is "
                    + (shouldAccept ? "ACCEPTED, S/cbar/b > 0 and equal to the printed values "
                        + "converted" : "REFUSED, S/cbar/b stay zero"),
                    report, ref passed, ref failed);
            }
        }

        // ---------------------------------------------------------------- [G3]

        private static void ValidateFamilyValuesCannotSatisfyTarget(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[G3] 608 ft^2 / 15.94 ft cannot silently satisfy the exact target");

            MavF15GeometryCandidate[] all = MavF15ReferenceGeometrySources.All;

            int familiar = 0;
            bool noneAccepted = true;
            for (int i = 0; i < all.Length; i++)
            {
                bool isFamiliar =
                    (all[i].quantity == MavF15GeometryQuantity.CoefficientReferenceArea
                        && Mathf.Approximately(all[i].sourceValue, 608f))
                    || (all[i].quantity == MavF15GeometryQuantity.CoefficientReferenceChord
                        && Mathf.Approximately(all[i].sourceValue, 15.94f));

                if (!isFamiliar)
                    continue;

                familiar++;
                if (all[i].MayDimensionalizeExactTargetCoefficients
                    || all[i].AuthorityAcceptedForExactTarget)
                    noneAccepted = false;
            }

            Record(familiar == 4 && noneAccepted,
                "all " + familiar + " occurrences of 608 ft^2 or 15.94 ft in the audit are held "
                + "WITHOUT exact-target authority",
                report, ref passed, ref failed);

            Record(
                Find(all, "NF15B837_S").authority == MavF15GeometryAuthority.Incompatible
                && Find(all, "NF15B837_CBAR").authority == MavF15GeometryAuthority.Incompatible,
                "the NASA table that prints them is NF-15B 837's - preproduction, canards, "
                + "F100-PW-229 - and is graded Incompatible, not family support",
                report, ref passed, ref failed);

            Record(
                Find(all, "ARO10_S").authority == MavF15GeometryAuthority.F15FamilySupport
                && Find(all, "ARO10_CBAR").authority == MavF15GeometryAuthority.F15FamilySupport,
                "the production-aerobase occurrence is F15FamilySupport - genuine family data, "
                + "no chain to NASA 836",
                report, ref passed, ref failed);

            MavF15CoefficientReferenceSet set;
            string reason;
            bool nf15b = MavF15ReferenceGeometrySources.TrySelectReferenceSet(
                Filter(all, MavF15ReferenceGeometrySources.Nf15b837ReferenceSetId),
                out set, out reason);
            bool aro10 = MavF15ReferenceGeometrySources.TrySelectReferenceSet(
                Filter(all, MavF15ReferenceGeometrySources.Aro10ReferenceSetId),
                out set, out reason);

            Record(!nf15b && !aro10,
                "each family set, offered alone and complete, is refused by the selector",
                report, ref passed, ref failed);

            MavAeroReferenceGeometry exact = MavF15ReferenceData.CreateExactTargetGeometry();
            MavAeroReferenceGeometry research =
                MavF15BaumannMach06Reference.CreateReferenceGeometry();
            Record(
                exact.wingAreaM2 != research.wingAreaM2
                && exact.meanAerodynamicChordM != research.meanAerodynamicChordM,
                "and the exact target has not adopted the cross-validation research model's "
                + "geometry",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [G4]

        private static void ValidateSpanDefinition(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[G4] The reference span is a reference span, never the physical span");

            MavF15GeometryCandidate[] all = MavF15ReferenceGeometrySources.All;
            MavF15GeometryCandidate physical = Find(all, "NASA836_PHYSICAL_SPAN");

            Record(
                physical.quantity == MavF15GeometryQuantity.PhysicalWingSpan
                && physical.authority == MavF15GeometryAuthority.DirectExact836
                && !physical.MayDimensionalizeExactTargetCoefficients,
                "836's 42.8 ft is exact PHYSICAL geometry - and for that very reason may not "
                + "dimensionalize a coefficient",
                report, ref passed, ref failed);

            // Accepted S and cbar, plus the physical span in the span slot: still refused.
            MavF15GeometryCandidate[] fixture =
            {
                Synthetic(MavF15GeometryQuantity.CoefficientReferenceArea, SyntheticAreaFt2,
                    "ft^2", MavF15GeometryAuthority.DirectExact836, SyntheticSet),
                Synthetic(MavF15GeometryQuantity.CoefficientReferenceChord, SyntheticChordFt,
                    "ft", MavF15GeometryAuthority.DirectExact836, SyntheticSet),
                Synthetic(MavF15GeometryQuantity.PhysicalWingSpan,
                    MavF15ReferenceGeometrySources.Nasa836PhysicalWingSpanFt,
                    "ft", MavF15GeometryAuthority.DirectExact836, SyntheticSet)
            };

            MavF15CoefficientReferenceSet set;
            string reason;
            bool selected = MavF15ReferenceGeometrySources.TrySelectReferenceSet(
                fixture, out set, out reason);

            Record(!selected,
                "even beside an accepted S and cbar, a physical span cannot fill the reference-"
                + "span slot: " + reason,
                report, ref passed, ref failed);

            MavAeroReferenceGeometry geometry = MavF15ReferenceData.CreateExactTargetGeometry();
            Record(
                geometry.wingSpanM == 0f
                && MavF15ReferenceData.PhysicalWingSpanM > 13f,
                "the exact-target geometry's span field is 0 while the physical span stays "
                + "available, correctly named, as PhysicalWingSpanM = "
                + MavF15ReferenceData.PhysicalWingSpanM.ToString("0.00000") + " m",
                report, ref passed, ref failed);

            // The distinction is not pedantry: sources print different values for the two.
            Record(
                !Mathf.Approximately(
                    MavF15ReferenceGeometrySources.Nf15b837PhysicalWingSpanFt,
                    MavF15ReferenceGeometrySources.Nf15b837ReferenceSpanFt),
                "one report, one airplane (NF-15B 837): three-view span 42.83 ft, reference "
                + "span 42.7 ft - physical and reference span are different quantities",
                report, ref passed, ref failed);

            Record(
                !Mathf.Approximately(
                    MavF15ReferenceGeometrySources.Aro10ReferenceSpanFt,
                    MavF15ReferenceGeometrySources.Nf15b837ReferenceSpanFt),
                "and two F-15 aerodynamic models use different reference spans (ARO10 42.8 ft, "
                + "NF-15B 837 42.7 ft) - so 836's cannot be assumed from either",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [G5]

        private static void ValidateMomentReferenceIsNotCg(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[G5] A moment reference is never a centre of gravity");

            MavF15GeometryCandidate[] all = MavF15ReferenceGeometrySources.All;

            MavF15GeometryCandidate momentReference;
            string reason;
            Record(
                !MavF15ReferenceGeometrySources.TrySelectExactTargetMomentReference(
                    out momentReference, out reason),
                "no exact-target moment reference is available: " + reason,
                report, ref passed, ref failed);

            // The 836 CG statements are exact - and must still be refused as a moment reference.
            MavF15GeometryCandidate table1Cg = Find(all, "NASA836_TABLE1_CG");
            MavF15GeometryCandidate pftfCg = Find(all, "NASA836_PFTF_CG");
            MavF15GeometryCandidate[] cgOnly = { table1Cg, pftfCg };

            Record(
                table1Cg.quantity == MavF15GeometryQuantity.CenterOfGravityLocation
                && pftfCg.quantity == MavF15GeometryQuantity.CenterOfGravityLocation
                && table1Cg.AuthorityAcceptedForExactTarget
                && pftfCg.AuthorityAcceptedForExactTarget
                && !MavF15ReferenceGeometrySources.TrySelectMomentReference(
                    cgOnly, out momentReference, out reason),
                "836's exact CG statements (26.34 % MAC; 28 % MAC = FS 561.7) are refused as a "
                + "moment reference despite their authority",
                report, ref passed, ref failed);

            Record(
                Mathf.Abs(table1Cg.sourceValue - MavF15MassReference.XcgPercentMac) < 1e-4f,
                "the table-1 CG candidate is the frozen mass state's CG, not a second copy",
                report, ref passed, ref failed);

            Record(
                MavF15ReferenceGeometrySources.Nf15b837MomentReferenceFsIn
                    != MavF15ReferenceGeometrySources.Nf15b837CgFsIn,
                "where a NASA table prints both, they differ: NF-15B 837 moment reference "
                + "FS 557.2 against CG FS 560.40",
                report, ref passed, ref failed);

            MavF15GeometryCandidate[] synthetic =
            {
                Synthetic(MavF15GeometryQuantity.MomentReferenceLocation, 1f, "in",
                    MavF15GeometryAuthority.DirectExact836, SyntheticSet)
            };
            MavF15GeometryCandidate accepted;
            Record(
                MavF15ReferenceGeometrySources.TrySelectMomentReference(
                    synthetic, out accepted, out reason)
                && accepted.quantity == MavF15GeometryQuantity.MomentReferenceLocation,
                "the gate does open for a genuine moment reference with exact-target authority",
                report, ref passed, ref failed);

            // The only cross-check between 836 and the family geometry, recorded as such.
            const float inchesPerFoot = 12f;
            float familyChordIn =
                MavF15ReferenceGeometrySources.Nf15b837ReferenceChordFt * inchesPerFoot;
            float leadingEdgeFrom837 =
                MavF15ReferenceGeometrySources.Nf15b837MomentReferenceFsIn
                - MavF15ReferenceGeometrySources.Aro10MomentReferenceFractionCbar * familyChordIn;
            float leadingEdgeFrom836 =
                MavF15ReferenceGeometrySources.Nasa836PftfAnalysisCgFuselageStationIn
                - 0.01f * MavF15ReferenceGeometrySources.Nasa836PftfAnalysisCgPercentMac
                    * familyChordIn;

            Record(
                Mathf.Abs(leadingEdgeFrom837 - leadingEdgeFrom836) < 0.05f
                && Find(all, "NASA836_PFTF_CG_FAMILY_CONSISTENCY").authority
                    == MavF15GeometryAuthority.CrossValidationOnly,
                "cross-check only: IF the family chord and ARO10's 25.65 % reference applied, "
                + "837's FS 557.2 and 836's 28 % MAC at FS 561.7 would put the leading-edge MAC "
                + "at FS " + leadingEdgeFrom837.ToString("0.00") + " and FS "
                + leadingEdgeFrom836.ToString("0.00") + " - consistent, and graded "
                + "CrossValidationOnly",
                report, ref passed, ref failed);

            // Why it cannot be promoted: 836's statement is one equation in two unknowns (the
            // leading-edge MAC station and cbar), so it is held as a CG statement and nothing
            // derived from it is recorded as a chord.
            bool chordFromPftf = false;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].id.StartsWith("NASA836_PFTF")
                    && (all[i].IsCoefficientReference || all[i].siValue != 0f))
                    chordFromPftf = true;
            }

            Record(!chordFromPftf,
                "and no chord is derived from it: 836's statement is one equation in two "
                + "unknowns, satisfied by any cbar with a suitable leading edge",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [G6]

        private static void ValidateCoherentReferenceSet(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[G6] One coherent reference set, never a mixture");

            MavF15GeometryCandidate[] mixed =
            {
                Synthetic(MavF15GeometryQuantity.CoefficientReferenceArea, SyntheticAreaFt2,
                    "ft^2", MavF15GeometryAuthority.DirectExact836, SyntheticSet),
                Synthetic(MavF15GeometryQuantity.CoefficientReferenceChord, SyntheticChordFt,
                    "ft", MavF15GeometryAuthority.DirectExact836, SyntheticSet),
                Synthetic(MavF15GeometryQuantity.CoefficientReferenceSpan, SyntheticSpanFt,
                    "ft", MavF15GeometryAuthority.DirectExact836, OtherSyntheticSet)
            };

            MavF15CoefficientReferenceSet set;
            string reason;
            Record(
                !MavF15ReferenceGeometrySources.TrySelectReferenceSet(mixed, out set, out reason),
                "S and cbar from one set with b from another is refused, even with every value "
                + "accepted: " + reason,
                report, ref passed, ref failed);

            MavF15GeometryCandidate[] duplicated =
            {
                Synthetic(MavF15GeometryQuantity.CoefficientReferenceArea, SyntheticAreaFt2,
                    "ft^2", MavF15GeometryAuthority.DirectExact836, SyntheticSet),
                Synthetic(MavF15GeometryQuantity.CoefficientReferenceArea, 2f * SyntheticAreaFt2,
                    "ft^2", MavF15GeometryAuthority.DirectExact836, SyntheticSet),
                Synthetic(MavF15GeometryQuantity.CoefficientReferenceChord, SyntheticChordFt,
                    "ft", MavF15GeometryAuthority.DirectExact836, SyntheticSet),
                Synthetic(MavF15GeometryQuantity.CoefficientReferenceSpan, SyntheticSpanFt,
                    "ft", MavF15GeometryAuthority.DirectExact836, SyntheticSet)
            };
            Record(
                !MavF15ReferenceGeometrySources.TrySelectReferenceSet(
                    duplicated, out set, out reason),
                "a set offering two different areas is refused rather than picking one",
                report, ref passed, ref failed);

            MavF15GeometryCandidate[] incomplete =
            {
                Synthetic(MavF15GeometryQuantity.CoefficientReferenceArea, SyntheticAreaFt2,
                    "ft^2", MavF15GeometryAuthority.DirectExact836, SyntheticSet),
                Synthetic(MavF15GeometryQuantity.CoefficientReferenceChord, SyntheticChordFt,
                    "ft", MavF15GeometryAuthority.DirectExact836, SyntheticSet)
            };
            Record(
                !MavF15ReferenceGeometrySources.TrySelectReferenceSet(
                    incomplete, out set, out reason),
                "S and cbar without a reference b is refused, not completed from elsewhere",
                report, ref passed, ref failed);

            Record(
                MavF15ReferenceGeometrySources.TrySelectReferenceSet(
                    SyntheticSet3(MavF15GeometryAuthority.DirectExact836, SyntheticSet),
                    out set, out reason)
                && set.referenceSetId == SyntheticSet && set.IsComplete,
                "a complete, single-source accepted set is selected and keeps its source id",
                report, ref passed, ref failed);

            // The cross-validation research model is itself one coherent set: ARO10's.
            MavF15GeometryCandidate[] all = MavF15ReferenceGeometrySources.All;
            Record(
                Mathf.Approximately(MavF15BaumannMach06Reference.WingAreaFt2,
                    Find(all, "ARO10_S").sourceValue)
                && Mathf.Approximately(MavF15BaumannMach06Reference.MeanAerodynamicChordFt,
                    Find(all, "ARO10_CBAR").sourceValue)
                && Mathf.Approximately(MavF15BaumannMach06Reference.WingSpanFt,
                    Find(all, "ARO10_B_REFERENCE").sourceValue),
                "the AFIT research model dimensionalizes with one set, ARO10's, throughout",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [G7]

        private static void ValidateOtherGatesStayClosed(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[G7] Valid geometry alone would not enable flight");

            // Suppose the geometry gate DID close, with a synthetic accepted set.
            MavF15CoefficientReferenceSet set;
            string reason;
            MavF15ReferenceGeometrySources.TrySelectReferenceSet(
                SyntheticSet3(MavF15GeometryAuthority.DirectExact836, SyntheticSet),
                out set, out reason);

            MavFlightDynamicsProfile profile = new MavFlightDynamicsProfile();
            profile.profileId = "f15-geometry-validation-hypothetical";
            profile.referenceGeometry = new MavAeroReferenceGeometry
            {
                wingAreaM2 = set.areaM2,
                wingSpanM = set.spanM,
                meanAerodynamicChordM = set.chordM
            };
            profile.massProperties = MavF15MassReference.CreateUnityMassProperties(Vector3.zero);
            profile.envelope = MavF15ReferenceData.CreateUnavailableAerodynamicEnvelope();
            profile.controlSurfaceLimits = MavF15ReferenceData.CreateUnavailableControlLimits();

            string profileReason;
            Record(profile.IsValid(out profileReason),
                "with hypothetical accepted geometry the core profile check passes - geometry "
                + "was the gate it tests",
                report, ref passed, ref failed);

            bool anyInside = false;
            for (float mach = 0.1f; mach <= 2.51f; mach += 0.2f)
            {
                for (float alphaDeg = -10f; alphaDeg <= 40.1f; alphaDeg += 5f)
                {
                    MavFlightState state = new MavFlightState();
                    state.mach = mach;
                    state.alphaRad = alphaDeg * Mathf.Deg2Rad;
                    state.betaRad = 0f;
                    if (profile.envelope.Contains(state))
                        anyInside = true;
                }
            }

            Record(!anyInside,
                "but the exact aerodynamic validity envelope still contains no flight state",
                report, ref passed, ref failed);

            MavControlSurfaceLimits limits = profile.controlSurfaceLimits;
            Record(
                limits.elevatorMinDeg == 0f && limits.elevatorMaxDeg == 0f
                && limits.aileronMinDeg == 0f && limits.aileronMaxDeg == 0f
                && limits.rudderMinDeg == 0f && limits.rudderMaxDeg == 0f,
                "the exact control hard stops still allow zero travel",
                report, ref passed, ref failed);

            Record(
                default(MavF15AeroSourceMode) == MavF15AeroSourceMode.ExactNasa836Unavailable,
                "the exact NASA 836 coefficient source is still the unavailable default",
                report, ref passed, ref failed);

            MavEngineProfile engine = MavF15PropulsionSkeleton.CreateUnfrozenEngineProfile();
            Record(engine.provenance == MavEngineDataProvenance.Unavailable,
                "and the propulsion data provenance is still Unavailable",
                report, ref passed, ref failed);
            UnityEngine.Object.DestroyImmediate(engine);
        }

        // ---------------------------------------------------------------- helpers

        private static MavF15GeometryCandidate[] SyntheticSet3(
            MavF15GeometryAuthority authority, string setId)
        {
            return new MavF15GeometryCandidate[]
            {
                Synthetic(MavF15GeometryQuantity.CoefficientReferenceArea, SyntheticAreaFt2,
                    "ft^2", authority, setId),
                Synthetic(MavF15GeometryQuantity.CoefficientReferenceChord, SyntheticChordFt,
                    "ft", authority, setId),
                Synthetic(MavF15GeometryQuantity.CoefficientReferenceSpan, SyntheticSpanFt,
                    "ft", authority, setId)
            };
        }

        private static MavF15GeometryCandidate Synthetic(
            MavF15GeometryQuantity quantity, float value, string units,
            MavF15GeometryAuthority authority, string setId)
        {
            MavF15GeometryCandidate c = new MavF15GeometryCandidate();
            c.id = "SYNTHETIC_" + quantity + "_" + setId;
            c.quantity = quantity;
            c.sourceValue = value;
            c.sourceUnits = units;
            c.siValue = value * (units == "ft^2"
                ? MavF15ReferenceGeometrySources.SquareFootToM2
                : units == "ft" ? MavF15ReferenceGeometrySources.FootToM : 0.0254f);
            c.referenceSetId = setId;
            c.airframe = "SYNTHETIC TEST FIXTURE - not an aircraft";
            c.citation = "none - test fixture";
            c.authority = authority;
            c.reason = "test fixture";
            return c;
        }

        private static MavF15GeometryCandidate Find(MavF15GeometryCandidate[] all, string id)
        {
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].id == id)
                    return all[i];
            }

            return new MavF15GeometryCandidate();
        }

        private static MavF15GeometryCandidate[] Filter(
            MavF15GeometryCandidate[] all, string referenceSetId)
        {
            int count = 0;
            for (int i = 0; i < all.Length; i++)
                if (all[i].referenceSetId == referenceSetId) count++;

            MavF15GeometryCandidate[] result = new MavF15GeometryCandidate[count];
            int k = 0;
            for (int i = 0; i < all.Length; i++)
                if (all[i].referenceSetId == referenceSetId) result[k++] = all[i];

            return result;
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
