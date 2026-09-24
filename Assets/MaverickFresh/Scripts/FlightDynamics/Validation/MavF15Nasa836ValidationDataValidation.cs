using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using UnityEngine;
using MaverickFresh.FlightDynamics.F15;

namespace MaverickFresh.FlightDynamics.Validation
{
    /// <summary>
    /// WP-2 checks for the digitized NASA 836 validation data:
    ///
    ///   [V1] every series carries its full metadata and is OriginalPrimary x Exact836 x ValidationOnly
    ///   [V2] no interpolation outside the digitized domain, across an omitted sample, or between
    ///        scattered estimates
    ///   [V3] validation-only data cannot enter runtime coefficients: no runtime file names it
    ///   [V4] every held figure that was NOT digitized is catalogued with its reason
    ///   [V5] digitized values stay inside the printed axes they were read from
    ///
    /// No agreement between these data and any Maverick model is asserted. They are targets.
    /// </summary>
    public static class MavF15Nasa836ValidationDataValidation
    {
        private static readonly string[] DataTypeNames =
        {
            "MavF15Nasa836ValidationData",
            "MavF15Nasa836ValidationSeries",
            "MavF15ValidationDataKind",
            "MavF15ExcludedValidationFigure"
        };

        public static string RunAll(out int passed, out int failed)
        {
            passed = 0;
            failed = 0;
            StringBuilder report = new StringBuilder(8192);
            report.AppendLine("F-15 NASA 836 Validation Data - Integrity and Isolation");
            report.AppendLine("=======================================================");

            ValidateMetadata(report, ref passed, ref failed);
            ValidateNoExtrapolation(report, ref passed, ref failed);
            ValidateIsolation(report, ref passed, ref failed);
            ValidateExcludedCatalogue(report, ref passed, ref failed);
            ValidatePrintedAxes(report, ref passed, ref failed);

            report.AppendLine();
            report.Append("RESULT: ")
                .Append(failed == 0 ? "PASS" : "FAIL")
                .Append("  passed=").Append(passed)
                .Append(" failed=").Append(failed);
            return report.ToString();
        }

        // ---------------------------------------------------------------- [V1]

        private static void ValidateMetadata(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[V1] Metadata, lineage and scope on every series");

            ReadOnlyCollection<MavF15Nasa836ValidationSeries> all = MavF15Nasa836ValidationData.All;
            int samples = 0;
            bool metadata = true;
            bool provenance = true;
            bool numeric = true;
            bool ids = true;

            for (int i = 0; i < all.Count; i++)
            {
                MavF15Nasa836ValidationSeries s = all[i];
                samples += s.Count;

                if (Empty(s.id) || Empty(s.report) || Empty(s.figure) || Empty(s.pageReference)
                    || Empty(s.configuration) || Empty(s.flightCondition) || Empty(s.xQuantity)
                    || Empty(s.xUnits) || Empty(s.yQuantity) || Empty(s.yUnits) || Empty(s.digitization))
                    metadata = false;

                if (s.Lineage != MavF15SourceLineage.OriginalPrimary
                    || s.Scope != MavF15ConfigurationScope.Exact836 || !s.ValidationOnly
                    || (s.report != "NASA/TM-2008-214634" && s.report != "NASA/TM-2012-215978"))
                    provenance = false;

                if (s.Count < 1 || !s.IsStrictlyIncreasing() || !Finite(s.xUncertainty)
                    || !Finite(s.yUncertainty) || s.xUncertainty <= 0.0 || s.yUncertainty <= 0.0)
                    numeric = false;

                for (int k = 0; k < s.Count; k++)
                {
                    if (!Finite(s.X(k)) || !Finite(s.Y(k)))
                        numeric = false;
                }

                if (MavF15Nasa836ValidationData.Find(s.id) != s)
                    ids = false;
            }

            report.Append("  ").Append(all.Count).Append(" series, ").Append(samples).AppendLine(" samples");

            Record(all.Count > 0 && metadata,
                "every series names report, figure, page, configuration, flight condition, axes, "
                + "units and its digitization method",
                report, ref passed, ref failed);
            Record(provenance,
                "every series is OriginalPrimary x Exact836 x ValidationOnly, from the two NASA reports",
                report, ref passed, ref failed);
            Record(numeric,
                "every value and uncertainty is finite; every uncertainty is stated and positive; "
                + "x strictly increases",
                report, ref passed, ref failed);
            Record(ids, "series ids are unique", report, ref passed, ref failed);

            bool kinds = true;
            for (int i = 0; i < all.Count; i++)
            {
                MavF15Nasa836ValidationSeries s = all[i];
                bool sim = s.kind == MavF15ValidationDataKind.SimulationTimeHistory;
                bool isSimId = s.id.IndexOf("_SIM") >= 0;
                if (sim != isSimId)
                    kinds = false;
                if (s.report == "NASA/TM-2012-215978" && !sim)
                    kinds = false;
            }

            Record(kinds,
                "simulation series are labelled simulation; every TM-2012 series is baseline simulation",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [V2]

        private static void ValidateNoExtrapolation(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[V2] No interpolation outside the plotted domain");

            ReadOnlyCollection<MavF15Nasa836ValidationSeries> all = MavF15Nasa836ValidationData.All;
            bool outside = true;
            bool scatter = true;
            bool atSample = true;
            bool gapSeen = false;
            bool gapRefused = true;
            string reason;
            double y;

            for (int i = 0; i < all.Count; i++)
            {
                MavF15Nasa836ValidationSeries s = all[i];
                double span = s.DomainMax - s.DomainMin;
                double eps = span > 0.0 ? span * 1e-6 : 1e-6;

                if (s.TryInterpolate(s.DomainMin - eps, out y, out reason)
                    || s.TryInterpolate(s.DomainMax + eps, out y, out reason)
                    || s.TryInterpolate(double.NaN, out y, out reason))
                    outside = false;

                if (s.kind == MavF15ValidationDataKind.FlightParameterEstimate)
                {
                    if (s.TryInterpolate(s.X(0), out y, out reason) || s.maxInterpolationSpan != 0.0)
                        scatter = false;
                    continue;
                }

                for (int k = 0; k < s.Count; k++)
                {
                    if (!s.TryInterpolate(s.X(k), out y, out reason) || y != s.Y(k))
                        atSample = false;
                }

                for (int k = 1; k < s.Count; k++)
                {
                    if (s.X(k) - s.X(k - 1) > s.maxInterpolationSpan)
                    {
                        gapSeen = true;
                        if (s.TryInterpolate(0.5 * (s.X(k) + s.X(k - 1)), out y, out reason))
                            gapRefused = false;
                    }
                }
            }

            Record(outside,
                "every series refuses below its first sample, above its last, and at NaN",
                report, ref passed, ref failed);
            Record(scatter,
                "scattered flight estimates are never interpolated, even inside their Mach range",
                report, ref passed, ref failed);
            Record(atSample,
                "at a digitized sample, a line-type series returns exactly that sample",
                report, ref passed, ref failed);
            Record(gapSeen && gapRefused,
                "an omitted sample is never filled: interpolation across the gap is refused",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [V3]

        private static void ValidateIsolation(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[V3] Validation-only data cannot enter runtime coefficients");

            Record(typeof(MavF15Nasa836ValidationSeries).Namespace == "MaverickFresh.FlightDynamics.Validation"
                   && typeof(MavF15Nasa836ValidationData).Namespace == "MaverickFresh.FlightDynamics.Validation",
                "the data types live in the Validation namespace",
                report, ref passed, ref failed);

            string root = ResolveFlightDynamicsRoot();
            if (root == null)
            {
                Record(false, "flight-dynamics source root not found; isolation scan could not run",
                    report, ref passed, ref failed);
                return;
            }

            string validationDir = Path.Combine(root, "Validation");
            string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
            int scanned = 0;
            string offender = null;

            for (int i = 0; i < files.Length; i++)
            {
                string full = Path.GetFullPath(files[i]);
                if (full.StartsWith(Path.GetFullPath(validationDir) + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                scanned++;
                string text = File.ReadAllText(full);
                for (int t = 0; t < DataTypeNames.Length; t++)
                {
                    if (text.IndexOf(DataTypeNames[t], StringComparison.Ordinal) >= 0)
                        offender = Path.GetFileName(full) + " names " + DataTypeNames[t];
                }
            }

            report.Append("  scanned ").Append(scanned).AppendLine(" non-validation sources (F15/, Core/, F16/, Editor/, ...)");
            Record(scanned > 0 && offender == null,
                offender == null
                    ? "no runtime or editor source outside Validation/ names the validation data"
                    : "VIOLATION: " + offender,
                report, ref passed, ref failed);

            // The exact aerodynamic path still has nothing to put the data into.
            Record(MavF15ReferenceData.CreateExactTargetGeometry().wingAreaM2 == 0f
                   && MavF15ReferenceData.CreateExactTargetGeometry().meanAerodynamicChordM == 0f
                   && MavF15ReferenceData.CreateExactTargetGeometry().wingSpanM == 0f,
                "exact S, cbar and b are still 0: the flight-estimated Cn_beta and Cm_alpha cannot "
                + "be dimensionalized for 836 and were not used to close them",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [V4]

        private static void ValidateExcludedCatalogue(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[V4] Held-but-excluded figures are catalogued with reasons");

            ReadOnlyCollection<MavF15ExcludedValidationFigure> ex = MavF15Nasa836ValidationData.Excluded;
            bool reasons = ex.Count > 0;
            bool unscaled = false;
            bool spike3033 = false;
            for (int i = 0; i < ex.Count; i++)
            {
                if (Empty(ex[i].report) || Empty(ex[i].figure) || Empty(ex[i].reason))
                    reasons = false;
                if (ex[i].figure.IndexOf("15-18") >= 0 && ex[i].reason.IndexOf("UNSCALED") >= 0)
                    unscaled = true;
                if (ex[i].figure.IndexOf("30-33") >= 0 && ex[i].reason.IndexOf("SPIKE-EXTENDED") >= 0)
                    spike3033 = true;
            }

            Record(reasons, "every excluded figure states report, figure and reason",
                report, ref passed, ref failed);
            Record(unscaled && spike3033,
                "TM-2012-215978 derivative borders (unscaled ordinate) and CAS-off Dutch-roll / "
                + "short-period figures (spike-extended airplane) are excluded, not stored as 836",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [V5]

        private static void ValidatePrintedAxes(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[V5] Digitized values lie inside the printed axes");

            ReadOnlyCollection<MavF15Nasa836ValidationSeries> all = MavF15Nasa836ValidationData.All;
            bool derivatives = true;
            bool times = true;

            for (int i = 0; i < all.Count; i++)
            {
                MavF15Nasa836ValidationSeries s = all[i];
                if (s.xQuantity == "Mach")
                {
                    bool cnb = s.yQuantity == "Cn_beta";
                    for (int k = 0; k < s.Count; k++)
                    {
                        if (s.X(k) < 0.4 || s.X(k) > 2.0)
                            derivatives = false;
                        if (cnb && (s.Y(k) < 0.0005 || s.Y(k) > 0.0045))
                            derivatives = false;
                        if (!cnb && (s.Y(k) < -0.03 || s.Y(k) > -0.005))
                            derivatives = false;
                    }
                }
                else
                {
                    double tMax = s.report == "NASA/TM-2012-215978" ? 10.0 : 9.0;
                    for (int k = 0; k < s.Count; k++)
                    {
                        if (s.X(k) < 0.0 || s.X(k) > tMax)
                            times = false;
                    }
                }
            }

            Record(derivatives,
                "TM-2008 figs. 12-13: Mach 0.4-2.0; Cn_beta 0.0005-0.0045 /deg; Cm_alpha -0.03 to -0.005 /deg",
                report, ref passed, ref failed);
            Record(times, "time histories stay inside their printed time axes",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- helpers

        private static string ResolveFlightDynamicsRoot()
        {
            try
            {
                string fromUnity = Path.Combine(
                    Application.dataPath, MavFlightDynamicsOwnershipScan.FlightDynamicsRelativePath);
                if (Directory.Exists(fromUnity))
                    return fromUnity;
            }
            catch (Exception)
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

        private static bool Empty(string s)
        {
            return string.IsNullOrEmpty(s);
        }

        private static bool Finite(double v)
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
