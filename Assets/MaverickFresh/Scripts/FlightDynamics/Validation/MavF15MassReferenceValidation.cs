using System.Text;
using UnityEngine;
using MaverickFresh.FlightDynamics.F15;

namespace MaverickFresh.FlightDynamics.Validation
{
    /// <summary>
    /// Deterministic checks on the exact-target NASA F-15B 836 mass/inertia reference state.
    ///
    /// This suite exists because of a real defect. Earlier R1 revisions froze NASA/TM-2012-215978
    /// table 1's "Spike extended" column into <see cref="MavF15MassReference"/> while every comment
    /// and document around it said "baseline F-15B test airplane". Six anonymous constants carry no
    /// column identity, two of the three columns share a weight, and all eighteen numbers look
    /// equally plausible - so nothing could notice.
    ///
    /// Covered:
    ///   [M0] all three table-1 columns match the source exactly
    ///   [M1] the frozen target selects BASELINE and cannot be satisfied by either spike state
    ///   [M2] SI values derive from the raw source values rather than being hand-copied
    ///   [M3] the source inertia tensor is physically admissible - positive definite, and its
    ///        PRINCIPAL moments (eigenvalues of the complete tensor, derived independently in the
    ///        suite) satisfy the triangle inequalities
    ///   [M4] the Unity conversion is numerically consistent with the source tensor
    ///   [M5] the specific mislabelled tuple is pinned to QuietSpikeExtended forever
    ///
    /// [M3] was itself corrected once. It originally applied the triangle inequalities to the
    /// body-axis diagonal and called that a principal-moment test, which it is only when Ixz is
    /// zero. The baseline's Ixz is -5,070 slug-ft^2.
    /// </summary>
    public static class MavF15MassReferenceValidation
    {
        public static string RunAll(out int passed, out int failed)
        {
            passed = 0;
            failed = 0;
            StringBuilder report = new StringBuilder(4096);
            report.AppendLine("NASA F-15B 836 Mass / Inertia Reference Validation");
            report.AppendLine("=================================================");

            ValidateTableRows(report, ref passed, ref failed);
            ValidateTargetSelectsBaseline(report, ref passed, ref failed);
            ValidateDerivedSi(report, ref passed, ref failed);
            ValidatePhysicalAdmissibility(report, ref passed, ref failed);
            ValidateUnityConversion(report, ref passed, ref failed);
            ValidateMislabelledTuplePinned(report, ref passed, ref failed);

            report.AppendLine();
            report.Append("RESULT: ")
                .Append(failed == 0 ? "PASS" : "FAIL")
                .Append("  passed=").Append(passed)
                .Append(" failed=").Append(failed);

            return report.ToString();
        }

        // ---------------------------------------------------------------- [M0]

        private static void ValidateTableRows(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[M0] All three TM-2012-215978 table 1 columns");

            MavF15MassStateReference baseline = MavF15Table1MassStates.Baseline;
            MavF15MassStateReference retracted = MavF15Table1MassStates.QuietSpikeRetracted;
            MavF15MassStateReference extended = MavF15Table1MassStates.QuietSpikeExtended;

            Record(
                baseline.Matches(37426f, 26.34f, 30345f, 198687f, 223214f, -5070f),
                "Baseline F-15B test airplane: 37,426 lb / 26.34 % MAC / "
                + "30,345 / 198,687 / 223,214 / -5,070 slug-ft^2",
                report, ref passed, ref failed);

            Record(
                retracted.Matches(37152f, 26.13f, 27947f, 189456f, 212746f, -466f),
                "Spike retracted: 37,152 lb / 26.13 % MAC / "
                + "27,947 / 189,456 / 212,746 / -466 slug-ft^2",
                report, ref passed, ref failed);

            Record(
                extended.Matches(37152f, 26.05f, 27953f, 190777f, 213957f, -460f),
                "Spike extended: 37,152 lb / 26.05 % MAC / "
                + "27,953 / 190,777 / 213,957 / -460 slug-ft^2",
                report, ref passed, ref failed);

            Record(
                baseline.fuelStateLb == 8000f
                && retracted.fuelStateLb == 8000f
                && extended.fuelStateLb == 8000f,
                "all three columns are at the table's stated mid-fuel loading of 8,000 lb",
                report, ref passed, ref failed);

            // The trap that made the original error invisible.
            Record(
                Mathf.Approximately(retracted.weightLb, extended.weightLb)
                && !Mathf.Approximately(baseline.weightLb, extended.weightLb),
                "the two spike columns SHARE a weight (37,152 lb) while baseline differs - which "
                + "is why weight alone can never identify the column",
                report, ref passed, ref failed);

            Record(
                baseline.columnLabel != retracted.columnLabel
                && retracted.columnLabel != extended.columnLabel,
                "each state carries its printed column heading, so the column travels with the "
                + "numbers instead of living in a comment",
                report, ref passed, ref failed);

            // Ixz is where the correction bites hardest.
            Record(
                baseline.ixzSlugFt2 < 0f && retracted.ixzSlugFt2 < 0f
                && extended.ixzSlugFt2 < 0f,
                "the source Ixz sign is negative in all three columns and is preserved as printed",
                report, ref passed, ref failed);

            Record(
                Mathf.Abs(baseline.ixzSlugFt2) > 10f * Mathf.Abs(extended.ixzSlugFt2),
                "baseline Ixz is more than ten times the spike-extended value ("
                + baseline.ixzSlugFt2.ToString("0") + " vs "
                + extended.ixzSlugFt2.ToString("0")
                + ") - the single largest consequence of the column correction",
                report, ref passed, ref failed);

            MavF15MassStateReference fetched;
            Record(
                MavF15Table1MassStates.TryGet(MavF15MassStateKind.Baseline, out fetched)
                && fetched.kind == MavF15MassStateKind.Baseline,
                "lookup by kind returns the matching column",
                report, ref passed, ref failed);

            Record(
                MavF15Table1MassStates.All != MavF15Table1MassStates.All,
                "and each read returns a fresh array, so no caller can edit the table for everyone",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [M1]

        private static void ValidateTargetSelectsBaseline(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[M1] The frozen target selects BASELINE");

            Record(
                MavF15MassReference.SelectedState == MavF15MassStateKind.Baseline,
                "MavF15MassReference declares which column it selects, and it is Baseline",
                report, ref passed, ref failed);

            MavF15MassStateReference baseline = MavF15Table1MassStates.Baseline;

            Record(
                Mathf.Approximately(MavF15MassReference.WeightLb, baseline.weightLb)
                && Mathf.Approximately(
                    MavF15MassReference.XcgPercentMac, baseline.xcgPercentMac)
                && Mathf.Approximately(MavF15MassReference.IxSlugFt2, baseline.ixxSlugFt2)
                && Mathf.Approximately(MavF15MassReference.IySlugFt2, baseline.iyySlugFt2)
                && Mathf.Approximately(MavF15MassReference.IzSlugFt2, baseline.izzSlugFt2)
                && Mathf.Approximately(MavF15MassReference.IxzSlugFt2, baseline.ixzSlugFt2),
                "and every frozen constant agrees with the Baseline column - the mirrored "
                + "compile-time constants cannot drift from the table",
                report, ref passed, ref failed);

            // The target id says pre-Quiet-Spike; the selected state must be consistent with it.
            Record(
                MavF15ReferenceData.TargetConfigurationId.Contains("PRE_QUIET_SPIKE"),
                "the frozen target id is explicitly PRE_QUIET_SPIKE: "
                + MavF15ReferenceData.TargetConfigurationId,
                report, ref passed, ref failed);

            Record(
                MavF15ReferenceData.TargetMassStateId.Contains("BASELINE"),
                "and the target mass-state id names BASELINE: "
                + MavF15ReferenceData.TargetMassStateId,
                report, ref passed, ref failed);

            Record(
                MavF15MassReference.FuelStateLb == 8000f,
                "fuel state remains the table's 8,000 lb",
                report, ref passed, ref failed);

            // Neither spike state may satisfy the frozen target.
            MavF15MassStateReference retracted = MavF15Table1MassStates.QuietSpikeRetracted;
            MavF15MassStateReference extended = MavF15Table1MassStates.QuietSpikeExtended;

            Record(
                !retracted.Matches(
                    MavF15MassReference.WeightLb, MavF15MassReference.XcgPercentMac,
                    MavF15MassReference.IxSlugFt2, MavF15MassReference.IySlugFt2,
                    MavF15MassReference.IzSlugFt2, MavF15MassReference.IxzSlugFt2),
                "the spike-RETRACTED column does not satisfy the frozen target state",
                report, ref passed, ref failed);

            Record(
                !extended.Matches(
                    MavF15MassReference.WeightLb, MavF15MassReference.XcgPercentMac,
                    MavF15MassReference.IxSlugFt2, MavF15MassReference.IySlugFt2,
                    MavF15MassReference.IzSlugFt2, MavF15MassReference.IxzSlugFt2),
                "and neither does the spike-EXTENDED column, which earlier revisions used",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [M2]

        private static void ValidateDerivedSi(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[M2] SI values derive from the raw source values");

            Record(
                Mathf.Abs(MavF15MassReference.MassKg
                    - MavF15MassReference.WeightLb * MavF15MassReference.PoundMassToKg) < 1e-3f,
                "mass " + MavF15MassReference.MassKg.ToString("0.000")
                + " kg derives from " + MavF15MassReference.WeightLb.ToString("0")
                + " lb - not hand-copied from a document",
                report, ref passed, ref failed);

            // The old stale constant, pinned so it cannot creep back.
            const float staleMassKg = 16851.86373f;
            Record(
                Mathf.Abs(MavF15MassReference.MassKg - staleMassKg) > 100f,
                "and it is no longer the stale 16,851.86373 kg, which was the spike-extended "
                + "weight converted",
                report, ref passed, ref failed);

            Record(
                Mathf.Abs(MavF15MassReference.IxKgM2
                    - MavF15MassReference.IxSlugFt2 * MavF15MassReference.SlugFt2ToKgM2) < 1e-2f
                && Mathf.Abs(MavF15MassReference.IyKgM2
                    - MavF15MassReference.IySlugFt2 * MavF15MassReference.SlugFt2ToKgM2) < 1e-2f
                && Mathf.Abs(MavF15MassReference.IzKgM2
                    - MavF15MassReference.IzSlugFt2 * MavF15MassReference.SlugFt2ToKgM2) < 1e-2f,
                "all three moments of inertia derive through the single conversion constant",
                report, ref passed, ref failed);

            Record(
                MavF15MassReference.IxzKgM2 < 0f
                && Mathf.Abs(MavF15MassReference.IxzKgM2
                    - MavF15MassReference.IxzSlugFt2
                        * MavF15MassReference.SlugFt2ToKgM2) < 1e-2f,
                "and the product of inertia derives with its source sign intact: "
                + MavF15MassReference.IxzKgM2.ToString("0.000") + " kg m^2",
                report, ref passed, ref failed);

            Record(
                Mathf.Abs(MavF15MassReference.XcgCbar - 0.2634f) < 1e-5f,
                "CG as a fraction of MAC is "
                + MavF15MassReference.XcgCbar.ToString("0.0000")
                + ", derived from 26.34 % MAC",
                report, ref passed, ref failed);

            Record(
                Mathf.Abs(MavF15MassReference.FuelStateKg
                    - 8000f * MavF15MassReference.PoundMassToKg) < 1e-3f,
                "fuel state converts consistently",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [M3]

        private static void ValidatePhysicalAdmissibility(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[M3] The source inertia tensor is physically admissible");

            MavF15MassStateReference[] all = MavF15Table1MassStates.All;

            bool allPositiveDefinite = true;
            bool allTriangle = true;
            bool principalNeverLooser = true;
            for (int i = 0; i < all.Length; i++)
            {
                if (!all[i].IsPositiveDefinite)
                    allPositiveDefinite = false;
                if (!all[i].SatisfiesPrincipalTriangleInequalities)
                    allTriangle = false;

                // The smallest eigenvalue of the second-moment matrix can never exceed any of its
                // diagonal entries, so the principal margin is never looser than the body-axis one.
                if (all[i].TightestPrincipalTriangleMarginSlugFt2
                    > BodyAxisTriangleMargin(all[i]) + 1e-6)
                    principalNeverLooser = false;
            }

            Record(allPositiveDefinite,
                "every table column yields a positive-definite body tensor "
                + "(Ixx > 0, Iyy > 0, Ixx*Izz > Ixz^2)",
                report, ref passed, ref failed);

            Record(allTriangle,
                "and every column's PRINCIPAL moments satisfy the rigid-body triangle "
                + "inequalities - evaluated on the eigenvalues of the complete tensor, not on the "
                + "body-axis diagonal",
                report, ref passed, ref failed);

            Record(principalNeverLooser,
                "for every column the principal margin is no looser than the body-axis margin, "
                + "as the mathematics requires - which is why the body-axis figure could only ever "
                + "overstate the margin",
                report, ref passed, ref failed);

            MavF15MassStateReference baseline = MavF15Table1MassStates.Baseline;

            // Derive the baseline's principal moments INDEPENDENTLY of the struct: a general
            // symmetric 3x3 eigen-solver on the full tensor, not the sparsity-aware closed form.
            double[,] tensor = BodyTensor(baseline);
            double[] independent = SymmetricEigenvaluesAscending(tensor);

            double smallest, middle, largest;
            baseline.GetPrincipalMomentsSlugFt2(out smallest, out middle, out largest);

            bool eigenAgree =
                RelativeClose(independent[0], smallest, 1e-9)
                && RelativeClose(independent[1], middle, 1e-9)
                && RelativeClose(independent[2], largest, 1e-9);

            Record(eigenAgree,
                "baseline principal moments, derived two independent ways from the source tensor: "
                + independent[0].ToString("0.00") + " / " + independent[1].ToString("0.00")
                + " / " + independent[2].ToString("0.00") + " slug-ft^2",
                report, ref passed, ref failed);

            // The eigenvalues must reproduce the tensor's three invariants.
            double trace = tensor[0, 0] + tensor[1, 1] + tensor[2, 2];
            double minors =
                tensor[0, 0] * tensor[1, 1] + tensor[1, 1] * tensor[2, 2]
                + tensor[0, 0] * tensor[2, 2] - tensor[0, 2] * tensor[0, 2];
            double determinant = Determinant3(tensor);

            bool invariantsHold =
                RelativeClose(independent[0] + independent[1] + independent[2], trace, 1e-12)
                && RelativeClose(
                    independent[0] * independent[1] + independent[1] * independent[2]
                    + independent[0] * independent[2], minors, 1e-9)
                && RelativeClose(
                    independent[0] * independent[1] * independent[2], determinant, 1e-9);

            Record(invariantsHold,
                "and they reproduce the tensor's trace, sum of principal minors and determinant",
                report, ref passed, ref failed);

            // Body Y is decoupled, so Iyy must survive as a principal moment untouched.
            Record(RelativeClose(middle, baseline.iyySlugFt2, 1e-12),
                "the decoupled body-Y moment Iyy is itself principal",
                report, ref passed, ref failed);

            double principalMargin = baseline.TightestPrincipalTriangleMarginSlugFt2;
            double bodyAxisMargin = BodyAxisTriangleMargin(baseline);

            Record(principalMargin > 0.0
                && RelativeClose(
                    principalMargin, independent[0] + independent[1] - independent[2], 1e-9),
                "the baseline's tightest PRINCIPAL triangle margin is "
                + principalMargin.ToString("0.00") + " slug-ft^2 (I1 + I2 - I3), positive as "
                + "required",
                report, ref passed, ref failed);

            Record(principalMargin < bodyAxisMargin,
                "which is SMALLER than the " + bodyAxisMargin.ToString("0")
                + " slug-ft^2 the old body-axis check reported (Ixx + Iyy - Izz) - the body-axis "
                + "diagonal is not the principal set when Ixz = "
                + baseline.ixzSlugFt2.ToString("0"),
                report, ref passed, ref failed);

            // A tensor that the OLD check accepts and the correct one rejects. Positive definite,
            // every body-axis triangle inequality satisfied, and still no physical body has it.
            MavF15MassStateReference counterexample = new MavF15MassStateReference();
            counterexample.ixxSlugFt2 = 10f;
            counterexample.iyySlugFt2 = 20f;
            counterexample.izzSlugFt2 = 25f;
            counterexample.ixzSlugFt2 = -7f;

            Record(
                counterexample.IsPositiveDefinite
                && BodyAxisTriangleMargin(counterexample) > 0.0
                && !counterexample.SatisfiesPrincipalTriangleInequalities,
                "pinned counterexample (10, 20, 25, Ixz -7): positive definite and passes every "
                + "body-axis triangle inequality, yet its principal margin is "
                + counterexample.TightestPrincipalTriangleMarginSlugFt2.ToString("0.000")
                + " - the old check would have accepted a tensor no rigid body can have",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [M4]

        private static void ValidateUnityConversion(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[M4] Unity conversion is consistent with the source tensor");

            MavMassProperties properties =
                MavF15MassReference.CreateUnityMassProperties(Vector3.zero);

            Vector3 inertia = properties.inertiaTensorKgM2;

            Record(
                !float.IsNaN(inertia.x) && !float.IsNaN(inertia.y) && !float.IsNaN(inertia.z)
                && !float.IsInfinity(inertia.x) && !float.IsInfinity(inertia.y)
                && !float.IsInfinity(inertia.z)
                && !float.IsNaN(properties.massKg),
                "all values are finite",
                report, ref passed, ref failed);

            Record(
                inertia.x > 0f && inertia.y > 0f && inertia.z > 0f,
                "all three principal moments are positive: "
                + inertia.x.ToString("0") + ", " + inertia.y.ToString("0") + ", "
                + inertia.z.ToString("0") + " kg m^2",
                report, ref passed, ref failed);

            // Unity X carries Iy untouched by the coupled block.
            Record(
                Mathf.Abs(inertia.x - MavF15MassReference.IyKgM2) < 1f,
                "Unity X takes the body pitch inertia Iyy directly, as the basis change requires",
                report, ref passed, ref failed);

            // A similarity transform preserves trace and determinant of the coupled 2x2 block.
            float a = MavF15MassReference.IzKgM2;
            float d = MavF15MassReference.IxKgM2;
            float b = MavF15MassReference.IxzKgM2;

            float traceSource = a + d;
            float traceUnity = inertia.y + inertia.z;

            Record(
                Mathf.Abs(traceSource - traceUnity) / traceSource < 1e-5f,
                "the coupled block's TRACE is preserved by the diagonalization ("
                + traceUnity.ToString("0") + " vs " + traceSource.ToString("0") + ")",
                report, ref passed, ref failed);

            double detSource = (double)a * d - (double)b * b;
            double detUnity = (double)inertia.y * inertia.z;

            Record(
                System.Math.Abs(detSource - detUnity) / detSource < 1e-4,
                "and so is its DETERMINANT - the two invariants that a correct similarity "
                + "transform must leave alone",
                report, ref passed, ref failed);

            // The rotation is about Unity X only, and small.
            Vector3 euler = properties.inertiaTensorRotationEulerDeg;
            Record(
                Mathf.Approximately(euler.y, 0f) && Mathf.Approximately(euler.z, 0f),
                "the principal-axis rotation is about Unity X alone, as the sparsity requires",
                report, ref passed, ref failed);

            Record(
                Mathf.Abs(euler.x) > 1.0f && Mathf.Abs(euler.x) < 5f,
                "and it is " + euler.x.ToString("0.000")
                + " deg - about ten times the spike-extended value, which is the corrected Ixz "
                + "showing up in the arithmetic rather than a convention change",
                report, ref passed, ref failed);

            Record(
                Mathf.Abs(properties.massKg - MavF15MassReference.MassKg) < 1e-3f,
                "and the mass carried into Unity is the derived baseline mass",
                report, ref passed, ref failed);

            // The Unity principal moments are the source principal moments, converted - so the
            // triangle test in [M3] is a test of exactly what Unity receives.
            double[] unitySorted = SortedAscending(
                inertia.x / (double)MavF15MassReference.SlugFt2ToKgM2,
                inertia.y / (double)MavF15MassReference.SlugFt2ToKgM2,
                inertia.z / (double)MavF15MassReference.SlugFt2ToKgM2);

            double smallest, middle, largest;
            MavF15Table1MassStates.Baseline.GetPrincipalMomentsSlugFt2(
                out smallest, out middle, out largest);

            Record(
                RelativeClose(unitySorted[0], smallest, 1e-4)
                && RelativeClose(unitySorted[1], middle, 1e-4)
                && RelativeClose(unitySorted[2], largest, 1e-4),
                "the three moments Unity receives are the source tensor's principal moments, "
                + "converted to kg m^2",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [M5]

        private static void ValidateMislabelledTuplePinned(
            StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[M5] The mislabelled tuple is pinned to QuietSpikeExtended");

            // The exact tuple that earlier R1 revisions froze as "baseline".
            const float w = 37152f;
            const float cg = 26.05f;
            const float ixx = 27953f;
            const float iyy = 190777f;
            const float izz = 213957f;
            const float ixz = -460f;

            MavF15MassStateReference extended = MavF15Table1MassStates.QuietSpikeExtended;

            Record(
                extended.Matches(w, cg, ixx, iyy, izz, ixz)
                && extended.kind == MavF15MassStateKind.QuietSpikeExtended,
                "(37152, 26.05, 27953, 190777, 213957, -460) IS the QuietSpikeExtended column",
                report, ref passed, ref failed);

            Record(
                !MavF15Table1MassStates.Baseline.Matches(w, cg, ixx, iyy, izz, ixz),
                "and it is NOT the Baseline column",
                report, ref passed, ref failed);

            Record(
                !MavF15Table1MassStates.QuietSpikeRetracted.Matches(w, cg, ixx, iyy, izz, ixz),
                "nor the QuietSpikeRetracted column",
                report, ref passed, ref failed);

            // The load-bearing assertion: it must not satisfy the frozen target state.
            bool satisfiesTarget =
                Mathf.Approximately(MavF15MassReference.WeightLb, w)
                && Mathf.Approximately(MavF15MassReference.XcgPercentMac, cg)
                && Mathf.Approximately(MavF15MassReference.IxSlugFt2, ixx)
                && Mathf.Approximately(MavF15MassReference.IySlugFt2, iyy)
                && Mathf.Approximately(MavF15MassReference.IzSlugFt2, izz)
                && Mathf.Approximately(MavF15MassReference.IxzSlugFt2, ixz);

            Record(!satisfiesTarget,
                "and it MUST NOT satisfy the frozen baseline target state - this is the exact "
                + "regression that the original error would have to pass through to return",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>
        /// Ixx + Iyy - Izz on the BODY-AXIS diagonal: the check earlier revisions mislabelled as a
        /// principal-moment test. Kept only so the suite can show how it differs.
        /// </summary>
        private static double BodyAxisTriangleMargin(MavF15MassStateReference s)
        {
            return (double)s.ixxSlugFt2 + s.iyySlugFt2 - s.izzSlugFt2;
        }

        /// <summary>The conventional aircraft body tensor [ Ixx 0 -Ixz ; 0 Iyy 0 ; -Ixz 0 Izz ].</summary>
        private static double[,] BodyTensor(MavF15MassStateReference s)
        {
            double[,] m = new double[3, 3];
            m[0, 0] = s.ixxSlugFt2;
            m[1, 1] = s.iyySlugFt2;
            m[2, 2] = s.izzSlugFt2;
            m[0, 2] = -s.ixzSlugFt2;
            m[2, 0] = -s.ixzSlugFt2;
            return m;
        }

        private static double Determinant3(double[,] m)
        {
            return m[0, 0] * (m[1, 1] * m[2, 2] - m[1, 2] * m[2, 1])
                - m[0, 1] * (m[1, 0] * m[2, 2] - m[1, 2] * m[2, 0])
                + m[0, 2] * (m[1, 0] * m[2, 1] - m[1, 1] * m[2, 0]);
        }

        /// <summary>
        /// Eigenvalues of a general real symmetric 3x3 matrix by the trigonometric closed form
        /// (Smith, 1961). Deliberately makes no use of the inertia tensor's sparsity, so it is an
        /// independent derivation rather than the struct's own arithmetic run twice.
        /// </summary>
        private static double[] SymmetricEigenvaluesAscending(double[,] a)
        {
            double p1 = a[0, 1] * a[0, 1] + a[0, 2] * a[0, 2] + a[1, 2] * a[1, 2];
            double q = (a[0, 0] + a[1, 1] + a[2, 2]) / 3.0;
            double p2 =
                (a[0, 0] - q) * (a[0, 0] - q)
                + (a[1, 1] - q) * (a[1, 1] - q)
                + (a[2, 2] - q) * (a[2, 2] - q)
                + 2.0 * p1;
            double p = System.Math.Sqrt(p2 / 6.0);

            double[,] b = new double[3, 3];
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    b[i, j] = (a[i, j] - (i == j ? q : 0.0)) / p;

            double r = Determinant3(b) / 2.0;
            if (r < -1.0) r = -1.0;
            if (r > 1.0) r = 1.0;
            double phi = System.Math.Acos(r) / 3.0;

            double largest = q + 2.0 * p * System.Math.Cos(phi);
            double smallest = q + 2.0 * p * System.Math.Cos(phi + 2.0 * System.Math.PI / 3.0);
            double middle = 3.0 * q - largest - smallest;

            return SortedAscending(smallest, middle, largest);
        }

        private static double[] SortedAscending(double x, double y, double z)
        {
            double[] v = new double[] { x, y, z };
            System.Array.Sort(v);
            return v;
        }

        private static bool RelativeClose(double actual, double expected, double relativeTolerance)
        {
            double scale = System.Math.Max(1.0, System.Math.Abs(expected));
            return System.Math.Abs(actual - expected) <= relativeTolerance * scale;
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
