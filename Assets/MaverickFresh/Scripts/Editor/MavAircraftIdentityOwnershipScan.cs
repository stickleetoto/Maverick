#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MaverickFresh.EditorTools
{
    /// <summary>Result of scanning the sources for illegal reads of the applier's request field.</summary>
    public struct MavIdentityOwnershipScanResult
    {
        public bool sourcesAvailable;
        public int filesScanned;
        public List<string> violations;
    }

    /// <summary>
    /// Static scan enforcing WHO MAY DECIDE AIRCRAFT IDENTITY.
    ///
    /// MavAircraftProfileApplier carries two different things that look alike:
    ///
    ///   aircraft              a serialized REQUEST, default F22A, meaningless until applied
    ///   AppliedAircraft       the identity that has actually been applied
    ///
    /// Reading the first as though it were the second is what put an F-22 on a player who had
    /// selected the F-16. MavWTFeelPolishController did exactly that, during Awake, before any
    /// selection had been applied - and no amount of correctness in the catalog could help, because
    /// the wrong question was being asked.
    ///
    /// A comment cannot prevent that from being written again, so this scan does. Any read of
    /// `something.aircraft` where the receiver looks like an applier is a violation unless the file
    /// is named below. It is a text scan, not a type-checked one, which makes it slightly blunt and
    /// very hard to accidentally defeat.
    /// </summary>
    public static class MavAircraftIdentityOwnershipScan
    {
        /// <summary>
        /// Files permitted to read the request field, each for a stated reason.
        ///
        ///   MavAircraftProfileApplier.cs
        ///       Declares it. ApplySelected() and applyOnStart are precisely the authoring paths the
        ///       field exists for - an explicit request to apply that aircraft.
        ///
        ///   MavAircraftIdentityValidation.cs
        ///   MavAircraftStartupOrderValidation.cs
        ///       Validation. They read and write it deliberately, including setting it to the WRONG
        ///       aircraft, to prove nothing downstream is influenced by it.
        /// </summary>
        public static readonly string[] ExemptFileNames =
        {
            "MavAircraftProfileApplier.cs",
            "MavAircraftIdentityValidation.cs",
            "MavAircraftStartupOrderValidation.cs"
        };

        /// <summary>Receiver names that mean "this is an aircraft profile applier".</summary>
        private static readonly string[] ApplierReceivers =
        {
            "applier",
            "profileApplier",
            "aircraftProfile",
            "profileApplierComponent"
        };

        public static bool IsExemptFile(string fileName)
        {
            for (int i = 0; i < ExemptFileNames.Length; i++)
            {
                if (ExemptFileNames[i] == fileName)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Whether this line reads an applier's request field. Comments and string literals are
        /// stripped first, so the explanatory comments that quote the old broken line - including the
        /// one in MavWTFeelPolishController - do not register as violations.
        /// </summary>
        public static bool IsIdentityOwnershipViolation(string codeOnlyLine)
        {
            if (string.IsNullOrEmpty(codeOnlyLine))
                return false;

            for (int i = 0; i < ApplierReceivers.Length; i++)
            {
                string needle = ApplierReceivers[i] + ".aircraft";
                int at = codeOnlyLine.IndexOf(needle, System.StringComparison.Ordinal);

                while (at >= 0)
                {
                    // ".aircraftId", ".aircraftKind" and friends are different members.
                    int after = at + needle.Length;
                    bool wholeMember = after >= codeOnlyLine.Length
                                       || !IsIdentifierChar(codeOnlyLine[after]);

                    // The receiver must not itself be the tail of a longer identifier, so that
                    // "myApplier.aircraft" counts but "xapplier" inside a word does not mislead.
                    bool receiverStartsCleanly = at == 0 || !IsIdentifierChar(codeOnlyLine[at - 1]);

                    if (wholeMember && receiverStartsCleanly)
                        return true;

                    at = codeOnlyLine.IndexOf(needle, at + 1, System.StringComparison.Ordinal);
                }
            }

            return false;
        }

        private static bool IsIdentifierChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
        }

        /// <summary>Removes comments and string literals so only executable text is judged.</summary>
        public static string StripCommentsAndStringLiterals(string line)
        {
            if (string.IsNullOrEmpty(line))
                return string.Empty;

            StringBuilder output = new StringBuilder(line.Length);
            bool inString = false;
            bool inChar = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (!inString && !inChar && c == '/' && i + 1 < line.Length && line[i + 1] == '/')
                    break;

                if (!inString && !inChar && c == '/' && i + 1 < line.Length && line[i + 1] == '*')
                    break;

                if (!inChar && c == '"' && (i == 0 || line[i - 1] != '\\'))
                {
                    inString = !inString;
                    continue;
                }

                if (!inString && c == '\'' && (i == 0 || line[i - 1] != '\\'))
                {
                    inChar = !inChar;
                    continue;
                }

                if (!inString && !inChar)
                    output.Append(c);
            }

            return output.ToString();
        }

        public static MavIdentityOwnershipScanResult Scan(string scriptsRoot)
        {
            MavIdentityOwnershipScanResult result = new MavIdentityOwnershipScanResult();
            result.violations = new List<string>();

            if (string.IsNullOrEmpty(scriptsRoot) || !Directory.Exists(scriptsRoot))
            {
                result.sourcesAvailable = false;
                return result;
            }

            result.sourcesAvailable = true;
            string[] files = Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories);

            for (int f = 0; f < files.Length; f++)
            {
                string fileName = Path.GetFileName(files[f]);
                result.filesScanned++;

                if (IsExemptFile(fileName))
                    continue;

                string[] lines = File.ReadAllLines(files[f]);
                for (int i = 0; i < lines.Length; i++)
                {
                    string code = StripCommentsAndStringLiterals(lines[i]);
                    if (!IsIdentityOwnershipViolation(code))
                        continue;

                    result.violations.Add(
                        fileName + ":" + (i + 1) + "  " + lines[i].Trim()
                        + "   <- reads the applier's REQUEST field; use AppliedAircraft together "
                        + "with HasAuthoritativeAircraft instead");
                }
            }

            return result;
        }
    }
}
#endif
