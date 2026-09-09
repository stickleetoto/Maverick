using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace MaverickFresh.FlightDynamics.Validation
{
    /// <summary>Outcome of a physical-ownership source scan.</summary>
    public struct MavOwnershipScanResult
    {
        /// <summary>False when the flight-dynamics sources are not present (for example in a player build).</summary>
        public bool sourcesAvailable;

        public int filesScanned;

        /// <summary>One entry per offending line: "relative/path.cs:line: text".</summary>
        public List<string> violations;

        public bool IsClean
        {
            get { return sourcesAvailable && (violations == null || violations.Count == 0); }
        }
    }

    /// <summary>
    /// Structural check that <see cref="MavSixDoFBody"/> really is the only place in the new
    /// flight-dynamics path that touches Rigidbody motion state.
    ///
    /// Why a source scan rather than a runtime assertion: the rule being enforced is "no other
    /// component may EVER apply a force", and no runtime test can prove a negative about code paths
    /// it happens not to execute. Reading the sources can. The check is deterministic - the same
    /// tree always produces the same result - and it fails loudly if someone adds an AddForce call
    /// to a control law, an actuator, a propulsion model, or an instructor.
    ///
    /// The scan strips line comments and string literals before matching, so documentation that
    /// mentions AddForce (there is a lot of it, deliberately) and validation code that carries the
    /// token list as data are not false positives.
    /// </summary>
    public static class MavFlightDynamicsOwnershipScan
    {
        /// <summary>Path of the flight-dynamics sources relative to the Assets folder.</summary>
        public const string FlightDynamicsRelativePath = "MaverickFresh/Scripts/FlightDynamics";

        /// <summary>
        /// The one file allowed to move a Rigidbody: the single load-application boundary.
        /// Adding anything to this list is a deliberate change of ownership architecture.
        /// </summary>
        public static readonly string[] ExemptFileNames =
        {
            "MavSixDoFBody.cs"
        };

        /// <summary>
        /// Tokens that indicate a component is writing rigid-body motion state directly.
        ///
        /// Velocity and rotation writes are included alongside the force calls because they are the
        /// other way a control layer can cheat physics: assigning a velocity is "arcade velocity
        /// alignment", and it bypasses the load path just as completely as AddTorque does.
        /// </summary>
        public static readonly string[] ForbiddenTokens =
        {
            "AddForce",
            "AddTorque",
            "AddRelativeForce",
            "AddRelativeTorque",
            "AddForceAtPosition",
            "AddExplosionForce",
            "linearVelocity =",
            "angularVelocity =",
            "velocity =",
            "MovePosition(",
            "MoveRotation("
        };

        /// <summary>
        /// Pure line classifier. Returns true when this source line writes rigid-body motion state.
        ///
        /// Kept separate from the file walk so the matching rule itself is deterministically
        /// testable: a comment mentioning AddForce must not trip it, and an actual call must.
        /// </summary>
        public static bool IsOwnershipViolation(string sourceLine)
        {
            if (string.IsNullOrEmpty(sourceLine))
                return false;

            string code = StripCommentsAndStringLiterals(sourceLine);
            if (string.IsNullOrEmpty(code))
                return false;

            for (int i = 0; i < ForbiddenTokens.Length; i++)
            {
                if (code.Contains(ForbiddenTokens[i]))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Removes double-quoted string literals and a trailing line comment, so only executable
        /// text is matched. Deliberately simple and deterministic; it does not attempt to parse C#.
        /// Block comments are not used anywhere in this folder and are not handled.
        /// </summary>
        public static string StripCommentsAndStringLiterals(string sourceLine)
        {
            StringBuilder code = new StringBuilder(sourceLine.Length);
            bool inString = false;
            bool escaped = false;

            for (int i = 0; i < sourceLine.Length; i++)
            {
                char current = sourceLine[i];

                if (inString)
                {
                    if (escaped)
                        escaped = false;
                    else if (current == '\\')
                        escaped = true;
                    else if (current == '"')
                        inString = false;
                    continue;
                }

                if (current == '"')
                {
                    inString = true;
                    continue;
                }

                if (current == '/' && i + 1 < sourceLine.Length && sourceLine[i + 1] == '/')
                    break;

                code.Append(current);
            }

            return code.ToString();
        }

        public static bool IsExemptFile(string fileName)
        {
            for (int i = 0; i < ExemptFileNames.Length; i++)
            {
                if (string.Equals(fileName, ExemptFileNames[i], System.StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Walks the flight-dynamics sources and reports every rigid-body motion write outside the
        /// exempt load-application boundary.
        /// </summary>
        public static MavOwnershipScanResult Scan()
        {
            return Scan(Path.Combine(Application.dataPath, FlightDynamicsRelativePath));
        }

        public static MavOwnershipScanResult Scan(string rootDirectory)
        {
            MavOwnershipScanResult result = new MavOwnershipScanResult();
            result.violations = new List<string>();

            if (string.IsNullOrEmpty(rootDirectory) || !Directory.Exists(rootDirectory))
            {
                result.sourcesAvailable = false;
                return result;
            }

            result.sourcesAvailable = true;

            string[] files = Directory.GetFiles(rootDirectory, "*.cs", SearchOption.AllDirectories);
            System.Array.Sort(files, System.StringComparer.Ordinal);

            for (int f = 0; f < files.Length; f++)
            {
                string file = files[f];
                string fileName = Path.GetFileName(file);
                result.filesScanned++;

                if (IsExemptFile(fileName))
                    continue;

                string[] lines;
                try
                {
                    lines = File.ReadAllLines(file);
                }
                catch (System.Exception e)
                {
                    result.violations.Add(fileName + ": unreadable (" + e.Message + ")");
                    continue;
                }

                for (int i = 0; i < lines.Length; i++)
                {
                    if (IsOwnershipViolation(lines[i]))
                        result.violations.Add(fileName + ":" + (i + 1) + ": " + lines[i].Trim());
                }
            }

            return result;
        }
    }
}
