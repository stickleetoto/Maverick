using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace MaverickFresh.FlightDynamics.F16
{
    /// <summary>One power setting's grid, newtons, indexed [machIndex, altitudeIndex].</summary>
    public class MavThrustGrid
    {
        public string powerSetting = "";
        public float[,] valuesN;
        public float[,] valuesLbf;

        public bool HasBoth
        {
            get { return valuesN != null && valuesLbf != null; }
        }
    }

    /// <summary>Result of parsing a raw Table VI transcription.</summary>
    public class MavThrustTranscriptionResult
    {
        public bool parsed;
        public string sourceDocument = "";
        public string sourceTable = "";
        public string sourcePages = "";
        public string transcribedBy = "";
        public string transcribedOn = "";
        public string method = "";

        public float[] altitudesM;
        public float[] machs;
        public readonly List<MavThrustGrid> grids = new List<MavThrustGrid>();
        public readonly List<string> issues = new List<string>();

        public bool IsEmptyTemplate
        {
            get { return grids.Count == 0; }
        }
    }

    /// <summary>
    /// Transcription workflow for NASA TP-1538 Table VI, "THRUST VALUES USED IN SIMULATION".
    ///
    /// WHY A WORKFLOW AND NOT JUST A TABLE. The data is public and permitted - TP-1538 is an
    /// unrestricted NASA Technical Paper - but the OCR of that particular table is column-interleaved:
    /// the idle, military and maximum rows are shuffled together with the altitude headers, so the
    /// extracted text cannot be trusted. Transcribing it anyway would be exactly the invented data this
    /// programme forbids. So the numbers must be read visually from the rendered page by a person, and
    /// this class is the machinery that makes such a transcription checkable afterwards:
    ///
    ///   * BOTH unit systems are required, and cross-checked against each other. A single transcription
    ///     error is overwhelmingly likely to break the 4.44822 N/lbf relationship, which is a far
    ///     stronger check than proof-reading.
    ///   * idle &lt; military &lt; maximum at every grid point. Physically required, and it catches a
    ///     row read from the wrong power block - the specific failure mode the interleaved OCR invites.
    ///   * thrust decreases with altitude at fixed Mach and power. Physically expected as density falls.
    ///   * thrust vs MACH is deliberately NOT checked for monotonicity. Ram recovery can raise thrust
    ///     with Mach before it falls again, so asserting monotonicity there would reject correct data.
    ///   * negative idle values are ALLOWED. TP-1538's idle figures go negative at high Mach and
    ///     altitude, which is the model representing net installed idle thrust, not a typo.
    ///   * a provenance hash over the normalized SI table, so a frozen transcription can be verified
    ///     to be the same one that was reviewed.
    ///
    /// NOTHING HERE IS CONNECTED TO THE LIVE AIRCRAFT. This class parses and validates a file. It does
    /// not register a thrust deck, and the reference propulsion model keeps reporting no authoritative
    /// data until a verified transcription is deliberately wired in as a separate step.
    /// </summary>
    public static class MavF16ThrustTranscription
    {
        public const string ExpectedSourceDocument = "NASA TP-1538";
        public const string ExpectedSourceTable = "VI";
        public const float NewtonsPerPoundForce = 4.4482216152605f;

        /// <summary>Tolerance on the SI/US cross-check, as a fraction. Transcriptions are rounded.</summary>
        public const float UnitCrossCheckTolerance = 0.01f;

        /// <summary>
        /// Parse the line-oriented transcription format. Deliberately strict: an unrecognised line is
        /// an issue, not something to skip, because a silently ignored line is a silently missing row.
        /// </summary>
        public static MavThrustTranscriptionResult Parse(string text)
        {
            MavThrustTranscriptionResult r = new MavThrustTranscriptionResult();
            if (string.IsNullOrEmpty(text))
            {
                r.issues.Add("transcription file is empty");
                return r;
            }

            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            MavThrustGrid current = null;
            string pendingUnits = null;
            List<float[]> pendingRows = new List<float[]>();

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                    continue;

                int colon = line.IndexOf(':');
                if (colon <= 0)
                {
                    r.issues.Add("line " + (i + 1) + " is not 'KEY: value' and is not a comment: " + line);
                    continue;
                }

                string key = line.Substring(0, colon).Trim().ToUpperInvariant();
                string value = line.Substring(colon + 1).Trim();

                switch (key)
                {
                    case "SOURCE": r.sourceDocument = value; break;
                    case "TABLE": r.sourceTable = value; break;
                    case "PAGES": r.sourcePages = value; break;
                    case "TRANSCRIBED_BY": r.transcribedBy = value; break;
                    case "TRANSCRIBED_ON": r.transcribedOn = value; break;
                    case "METHOD": r.method = value; break;

                    case "ALTITUDES_M": r.altitudesM = ParseFloats(value, r, i + 1); break;
                    case "MACHS": r.machs = ParseFloats(value, r, i + 1); break;

                    case "POWER":
                        FlushGrid(r, ref current, ref pendingUnits, pendingRows, i + 1);
                        current = new MavThrustGrid();
                        current.powerSetting = value.ToLowerInvariant();
                        r.grids.Add(current);
                        break;

                    case "UNITS":
                        FlushRows(r, current, pendingUnits, pendingRows, i + 1);
                        pendingUnits = value.ToUpperInvariant();
                        break;

                    case "ROW":
                        float[] row = ParseFloats(value, r, i + 1);
                        if (row != null)
                            pendingRows.Add(row);
                        break;

                    default:
                        r.issues.Add("line " + (i + 1) + " has unrecognised key '" + key + "'");
                        break;
                }
            }

            FlushGrid(r, ref current, ref pendingUnits, pendingRows, lines.Length);
            r.parsed = true;
            return r;
        }

        private static void FlushRows(
            MavThrustTranscriptionResult r, MavThrustGrid grid, string units,
            List<float[]> rows, int line)
        {
            if (units == null || rows.Count == 0)
            {
                rows.Clear();
                return;
            }

            if (grid == null)
            {
                r.issues.Add("line " + line + ": rows given before any POWER block was opened");
                rows.Clear();
                return;
            }

            int cols = rows[0].Length;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Length != cols)
                {
                    r.issues.Add("power '" + grid.powerSetting + "' " + units
                                 + " block has ragged rows: row 1 has " + cols + " values, row "
                                 + (i + 1) + " has " + rows[i].Length);
                    rows.Clear();
                    return;
                }
            }

            float[,] table = new float[rows.Count, cols];
            for (int i = 0; i < rows.Count; i++)
                for (int j = 0; j < cols; j++)
                    table[i, j] = rows[i][j];

            if (units == "SI" || units == "N")
                grid.valuesN = table;
            else if (units == "US" || units == "LBF" || units == "LB")
                grid.valuesLbf = table;
            else
                r.issues.Add("power '" + grid.powerSetting + "' has unknown UNITS '" + units + "'");

            rows.Clear();
        }

        private static void FlushGrid(
            MavThrustTranscriptionResult r, ref MavThrustGrid grid, ref string units,
            List<float[]> rows, int line)
        {
            FlushRows(r, grid, units, rows, line);
            units = null;
        }

        private static float[] ParseFloats(string value, MavThrustTranscriptionResult r, int line)
        {
            string[] parts = value.Split(',');
            List<float> got = new List<float>();
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim();
                if (p.Length == 0)
                    continue;

                float f;
                if (!float.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out f))
                {
                    r.issues.Add("line " + line + ": '" + p + "' is not a number");
                    return null;
                }
                got.Add(f);
            }
            return got.ToArray();
        }

        /// <summary>
        /// Validate a parsed transcription. Returns true only when it is complete, self-consistent and
        /// physically ordered. An empty template validates as FALSE with a clear reason, so an
        /// unfilled file can never be mistaken for verified data.
        /// </summary>
        public static bool Validate(MavThrustTranscriptionResult r, out string summary)
        {
            List<string> problems = new List<string>(r.issues);

            if (r.IsEmptyTemplate)
            {
                summary = "NOT TRANSCRIBED: the template contains no POWER blocks. The reference "
                          + "propulsion model stays non-authoritative until a person reads Table VI "
                          + "from the rendered page and fills this in.";
                return false;
            }

            if (r.sourceDocument != ExpectedSourceDocument)
                problems.Add("SOURCE must be '" + ExpectedSourceDocument + "', got '"
                             + r.sourceDocument + "'");
            if (r.sourceTable != ExpectedSourceTable)
                problems.Add("TABLE must be '" + ExpectedSourceTable + "', got '" + r.sourceTable + "'");
            if (string.IsNullOrEmpty(r.sourcePages))
                problems.Add("PAGES is required: a transcription without a page reference cannot be checked");
            if (string.IsNullOrEmpty(r.transcribedBy))
                problems.Add("TRANSCRIBED_BY is required");
            if (string.IsNullOrEmpty(r.method))
                problems.Add("METHOD is required and must say the values were read visually, not OCR'd");

            if (r.altitudesM == null || r.altitudesM.Length == 0)
                problems.Add("ALTITUDES_M is required");
            if (r.machs == null || r.machs.Length == 0)
                problems.Add("MACHS is required");

            // The three power settings TP-1538 publishes.
            bool hasIdle = false, hasMil = false, hasMax = false;
            for (int i = 0; i < r.grids.Count; i++)
            {
                string p = r.grids[i].powerSetting;
                if (p == "idle") hasIdle = true;
                else if (p == "mil" || p == "military") hasMil = true;
                else if (p == "max" || p == "maximum") hasMax = true;
                else problems.Add("unknown power setting '" + p + "'");

                if (!r.grids[i].HasBoth)
                    problems.Add("power '" + p + "' is missing one of its unit blocks; BOTH SI and US "
                                 + "are required so they can be cross-checked");
            }

            if (!hasIdle) problems.Add("idle grid missing");
            if (!hasMil) problems.Add("military grid missing");
            if (!hasMax) problems.Add("maximum grid missing");

            if (r.machs != null && r.altitudesM != null)
            {
                for (int i = 0; i < r.grids.Count; i++)
                {
                    MavThrustGrid g = r.grids[i];
                    if (g.valuesN == null)
                        continue;

                    if (g.valuesN.GetLength(0) != r.machs.Length
                        || g.valuesN.GetLength(1) != r.altitudesM.Length)
                    {
                        problems.Add("power '" + g.powerSetting + "' SI grid is "
                                     + g.valuesN.GetLength(0) + "x" + g.valuesN.GetLength(1)
                                     + " but the axes declare " + r.machs.Length + "x"
                                     + r.altitudesM.Length);
                    }
                }
            }

            CrossCheckUnits(r, problems);
            CheckPowerOrdering(r, problems);
            CheckAltitudeTrend(r, problems);
            CheckFinite(r, problems);

            if (problems.Count == 0)
            {
                summary = "VERIFIED: " + r.grids.Count + " power grids, "
                          + (r.machs != null ? r.machs.Length : 0) + " Mach x "
                          + (r.altitudesM != null ? r.altitudesM.Length : 0)
                          + " altitude, SI/US cross-checked, power ordering and altitude trend hold. "
                          + "hash=" + ComputeHash(r);
                return true;
            }

            StringBuilder sb = new StringBuilder();
            sb.Append("REJECTED with ").Append(problems.Count).AppendLine(" problem(s):");
            for (int i = 0; i < problems.Count; i++)
                sb.Append("  - ").AppendLine(problems[i]);
            summary = sb.ToString();
            return false;
        }

        private static void CrossCheckUnits(MavThrustTranscriptionResult r, List<string> problems)
        {
            for (int i = 0; i < r.grids.Count; i++)
            {
                MavThrustGrid g = r.grids[i];
                if (!g.HasBoth)
                    continue;

                if (g.valuesN.GetLength(0) != g.valuesLbf.GetLength(0)
                    || g.valuesN.GetLength(1) != g.valuesLbf.GetLength(1))
                {
                    problems.Add("power '" + g.powerSetting + "' SI and US grids differ in shape");
                    continue;
                }

                for (int m = 0; m < g.valuesN.GetLength(0); m++)
                {
                    for (int h = 0; h < g.valuesN.GetLength(1); h++)
                    {
                        float si = g.valuesN[m, h];
                        float us = g.valuesLbf[m, h] * NewtonsPerPoundForce;
                        float scale = Mathf.Max(1f, Mathf.Abs(si));
                        float rel = Mathf.Abs(si - us) / scale;
                        if (rel > UnitCrossCheckTolerance)
                        {
                            problems.Add(string.Format(CultureInfo.InvariantCulture,
                                "power '{0}' at mach index {1}, altitude index {2}: SI {3:F0} N does "
                                + "not match US {4:F0} lbf = {5:F0} N ({6:P2} apart)",
                                g.powerSetting, m, h, si, g.valuesLbf[m, h], us, rel));
                        }
                    }
                }
            }
        }

        private static void CheckPowerOrdering(MavThrustTranscriptionResult r, List<string> problems)
        {
            MavThrustGrid idle = FindGrid(r, "idle");
            MavThrustGrid mil = FindGrid(r, "mil", "military");
            MavThrustGrid max = FindGrid(r, "max", "maximum");
            if (idle == null || mil == null || max == null
                || idle.valuesN == null || mil.valuesN == null || max.valuesN == null)
                return;

            int mCount = Mathf.Min(idle.valuesN.GetLength(0),
                Mathf.Min(mil.valuesN.GetLength(0), max.valuesN.GetLength(0)));
            int hCount = Mathf.Min(idle.valuesN.GetLength(1),
                Mathf.Min(mil.valuesN.GetLength(1), max.valuesN.GetLength(1)));

            for (int m = 0; m < mCount; m++)
            {
                for (int h = 0; h < hCount; h++)
                {
                    // Negative idle is legitimate; ORDER is what must hold.
                    if (!(idle.valuesN[m, h] < mil.valuesN[m, h]))
                    {
                        problems.Add(string.Format(CultureInfo.InvariantCulture,
                            "idle >= military at mach index {0}, altitude index {1}: {2:F0} vs {3:F0} N"
                            + " - the classic symptom of a row read from the wrong power block",
                            m, h, idle.valuesN[m, h], mil.valuesN[m, h]));
                    }
                    if (!(mil.valuesN[m, h] < max.valuesN[m, h]))
                    {
                        problems.Add(string.Format(CultureInfo.InvariantCulture,
                            "military >= maximum at mach index {0}, altitude index {1}: {2:F0} vs "
                            + "{3:F0} N", m, h, mil.valuesN[m, h], max.valuesN[m, h]));
                    }
                }
            }
        }

        private static void CheckAltitudeTrend(MavThrustTranscriptionResult r, List<string> problems)
        {
            for (int i = 0; i < r.grids.Count; i++)
            {
                MavThrustGrid g = r.grids[i];
                if (g.valuesN == null)
                    continue;

                // Idle can behave oddly as it goes negative; the trend check is applied to the powered
                // settings, where falling density must reduce thrust.
                if (g.powerSetting == "idle")
                    continue;

                for (int m = 0; m < g.valuesN.GetLength(0); m++)
                {
                    for (int h = 1; h < g.valuesN.GetLength(1); h++)
                    {
                        if (g.valuesN[m, h] > g.valuesN[m, h - 1])
                        {
                            problems.Add(string.Format(CultureInfo.InvariantCulture,
                                "power '{0}' thrust RISES with altitude at mach index {1}: {2:F0} -> "
                                + "{3:F0} N between altitude index {4} and {5}",
                                g.powerSetting, m, g.valuesN[m, h - 1], g.valuesN[m, h], h - 1, h));
                        }
                    }
                }
            }
        }

        private static void CheckFinite(MavThrustTranscriptionResult r, List<string> problems)
        {
            for (int i = 0; i < r.grids.Count; i++)
            {
                MavThrustGrid g = r.grids[i];
                if (g.valuesN == null)
                    continue;

                for (int m = 0; m < g.valuesN.GetLength(0); m++)
                {
                    for (int h = 0; h < g.valuesN.GetLength(1); h++)
                    {
                        float v = g.valuesN[m, h];
                        if (float.IsNaN(v) || float.IsInfinity(v))
                            problems.Add("power '" + g.powerSetting + "' has a non-finite value");
                    }
                }
            }
        }

        public static MavThrustGrid FindGrid(
            MavThrustTranscriptionResult r, string name, string alternate = null)
        {
            for (int i = 0; i < r.grids.Count; i++)
            {
                string p = r.grids[i].powerSetting;
                if (p == name || (alternate != null && p == alternate))
                    return r.grids[i];
            }
            return null;
        }

        /// <summary>
        /// Bilinear interpolation on the normalized SI table. Clamps at the grid edges rather than
        /// extrapolating: past the published envelope there is no data, and inventing some is the
        /// thing this whole class exists to prevent.
        /// </summary>
        public static bool TryInterpolateN(
            MavThrustTranscriptionResult r, string powerSetting,
            float mach, float altitudeM, out float thrustN, out bool clamped)
        {
            thrustN = 0f;
            clamped = false;

            MavThrustGrid g = FindGrid(r, powerSetting,
                powerSetting == "mil" ? "military" : (powerSetting == "max" ? "maximum" : null));
            if (g == null || g.valuesN == null || r.machs == null || r.altitudesM == null)
                return false;

            int mi = LowerIndex(r.machs, mach, ref clamped);
            int hi = LowerIndex(r.altitudesM, altitudeM, ref clamped);
            int mi2 = Mathf.Min(mi + 1, r.machs.Length - 1);
            int hi2 = Mathf.Min(hi + 1, r.altitudesM.Length - 1);

            float mt = mi2 == mi ? 0f
                : Mathf.Clamp01((mach - r.machs[mi]) / (r.machs[mi2] - r.machs[mi]));
            float ht = hi2 == hi ? 0f
                : Mathf.Clamp01((altitudeM - r.altitudesM[hi]) / (r.altitudesM[hi2] - r.altitudesM[hi]));

            float a = Mathf.Lerp(g.valuesN[mi, hi], g.valuesN[mi2, hi], mt);
            float b = Mathf.Lerp(g.valuesN[mi, hi2], g.valuesN[mi2, hi2], mt);
            thrustN = Mathf.Lerp(a, b, ht);
            return true;
        }

        private static int LowerIndex(float[] axis, float v, ref bool clamped)
        {
            if (v <= axis[0]) { if (v < axis[0]) clamped = true; return 0; }
            if (v >= axis[axis.Length - 1])
            {
                if (v > axis[axis.Length - 1]) clamped = true;
                return axis.Length - 1;
            }

            for (int i = 0; i < axis.Length - 1; i++)
            {
                if (v >= axis[i] && v <= axis[i + 1])
                    return i;
            }
            return 0;
        }

        /// <summary>
        /// Stable hash over the normalized SI table and its declared axes, so a reviewed transcription
        /// can be frozen and later proven to be the same one.
        /// </summary>
        public static string ComputeHash(MavThrustTranscriptionResult r)
        {
            StringBuilder sb = new StringBuilder(4096);
            sb.Append(r.sourceDocument).Append('|').Append(r.sourceTable).Append('|');
            AppendAxis(sb, r.altitudesM);
            AppendAxis(sb, r.machs);

            for (int i = 0; i < r.grids.Count; i++)
            {
                MavThrustGrid g = r.grids[i];
                sb.Append(g.powerSetting).Append(':');
                if (g.valuesN == null)
                    continue;

                for (int m = 0; m < g.valuesN.GetLength(0); m++)
                    for (int h = 0; h < g.valuesN.GetLength(1); h++)
                        sb.Append(g.valuesN[m, h].ToString("F1", CultureInfo.InvariantCulture))
                          .Append(',');
                sb.Append(';');
            }

            unchecked
            {
                ulong hash = 1469598103934665603UL;
                string s = sb.ToString();
                for (int i = 0; i < s.Length; i++)
                {
                    hash ^= s[i];
                    hash *= 1099511628211UL;
                }
                return hash.ToString("X16", CultureInfo.InvariantCulture);
            }
        }

        private static void AppendAxis(StringBuilder sb, float[] axis)
        {
            if (axis == null)
            {
                sb.Append("none|");
                return;
            }

            for (int i = 0; i < axis.Length; i++)
                sb.Append(axis[i].ToString("F1", CultureInfo.InvariantCulture)).Append(',');
            sb.Append('|');
        }
    }
}
