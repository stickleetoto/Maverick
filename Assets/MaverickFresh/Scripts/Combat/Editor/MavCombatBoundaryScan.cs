#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MaverickFresh.Combat.EditorTools
{
    public struct MavCombatBoundaryResult
    {
        public bool sourcesAvailable;
        public int filesScanned;
        public List<string> violations;
    }

    /// <summary>
    /// Static architecture scan for the combat boundary, in the same spirit as the flight-dynamics
    /// ownership and writer scans: a rule that is checked rather than promised in a document.
    ///
    /// It deliberately lives under Scripts/Combat/Editor and NOT under Scripts/Editor. The latter is
    /// a declared authority root of the FDM validation baseline, so a new file there would be an
    /// unlisted validation surface and would force a change to the frozen FDM manifest. Weapon
    /// separation must not touch that manifest, so the scan is placed where it does not.
    ///
    /// Rules enforced today, all of them about DIRECTION of dependency, not about behavior:
    ///
    ///   C-1  Combat/Core must not reference flight-dynamics implementation types. The new combat
    ///        architecture has to be buildable without the FDM, and an aircraft must be able to
    ///        change its physics without touching combat.
    ///   C-2  Combat/Core must not write to a Rigidbody. Aircraft physics has exactly one writer and
    ///        combat is not it.
    ///   C-3  Combat/Core must not search the scene. Its whole purpose is to end the pattern where
    ///        every consumer finds its own targets with FindObjectsOfType.
    ///   C-4  Combat/Core must not read input. Input requests actions; it does not simulate them.
    ///   C-5  No radar, seeker, guidance or missile-profile implementation may appear in Combat/,
    ///        because those are later phases and this one is separation only.
    /// </summary>
    public static class MavCombatBoundaryScan
    {
        private const string CombatCoreRoot = "Assets/MaverickFresh/Scripts/Combat/Core";
        private const string CombatRoot = "Assets/MaverickFresh/Scripts/Combat";

        private static readonly string[] FlightDynamicsImplementationTypes =
        {
            "MavSixDoFBody",
            "MavFlightPhysicsOwnership",
            "MavPhysicsOwnershipController",
            "MavAeroBody",
            "MavPropulsionSystem",
            "MavFlightControlLawBase",
            "MavAtomicHandover",
            "MavRuntimeHandoverTarget",
            "MavThrustDeck",
            "MavEngineInstallation",
        };

        private static readonly string[] RigidbodyWriteTokens =
        {
            "AddForce(",
            "AddTorque(",
            "AddRelativeForce(",
            "AddRelativeTorque(",
            "AddForceAtPosition(",
            "linearVelocity =",
            "angularVelocity =",
        };

        private static readonly string[] SceneSearchTokens =
        {
            "FindObjectsOfType",
            "FindObjectOfType",
            "FindObjectsByType",
            "FindGameObjectsWithTag",
            "FindWithTag",
        };

        private static readonly string[] InputTokens =
        {
            "Input.GetKey",
            "Input.GetMouse",
            "Input.GetAxis",
            "MavFreshInput.",
            "Keyboard.current",
            "Mouse.current",
        };

        /// <summary>
        /// Type names that would mean a later phase had started early. Matched as declarations only,
        /// so a comment or a contract that merely NAMES radar - which the track contract does, in its
        /// source enum - is not a false positive.
        /// </summary>
        private static readonly string[] PrematureImplementationDeclarations =
        {
            "class MavRadarSystem",
            "class MavRadarScan",
            "class MavSeeker",
            "class MavMissileSeeker",
            "class MavMissileGuidance",
            "class MavGuidanceLaw",
            "class MavProportionalNavigation",
            "class MavAim120",
            "class MavAIM120",
            "class MavAim9",
            "class MavAIM9",
            "class MavAmraam",
            "class MavMissileAutopilot",
        };

        [MenuItem("Maverick/Combat/Run Combat Boundary Scan")]
        public static void RunFromMenu()
        {
            int passed, failed;
            string report = RunAll(out passed, out failed);
            Debug.Log(report);
            EditorUtility.DisplayDialog(
                "Combat Boundary Scan",
                (failed == 0 ? "PASS" : "FAIL") + "  passed=" + passed + " failed=" + failed,
                "OK");
        }

        /// <summary>
        /// Result-producing entry point, matching the shape the other Maverick validators use so a
        /// batch runner can invoke it without special handling.
        /// </summary>
        public static string RunAll(out int passed, out int failed)
        {
            passed = 0;
            failed = 0;
            StringBuilder report = new StringBuilder();
            report.AppendLine("Maverick Combat Boundary Scan R0");
            report.AppendLine("================================");

            MavCombatBoundaryResult result = Scan();

            if (!result.sourcesAvailable)
            {
                failed++;
                report.AppendLine("FAIL  the combat source tree could not be read, so nothing was checked");
                report.AppendLine("RESULT: FAIL passed=" + passed + " failed=" + failed);
                return report.ToString();
            }

            report.AppendLine("files scanned: " + result.filesScanned);

            if (result.violations.Count == 0)
            {
                passed++;
                report.AppendLine("PASS  C-1..C-5  no combat boundary violation found");
            }
            else
            {
                failed++;
                report.AppendLine("FAIL  " + result.violations.Count + " combat boundary violation(s):");
                for (int i = 0; i < result.violations.Count; i++)
                    report.AppendLine("      " + result.violations[i]);
            }

            // The scan must be able to fail, or a green result means nothing.
            if (IsViolatingLine("            rb.AddForce(f, ForceMode.Force);", RigidbodyWriteTokens))
                passed++;
            else
            {
                failed++;
                report.AppendLine("FAIL  self-test: a real Rigidbody write was not detected");
            }

            if (!IsViolatingLine("        // never call AddForce from combat code", RigidbodyWriteTokens))
                passed++;
            else
            {
                failed++;
                report.AppendLine("FAIL  self-test: a comment mentioning AddForce was treated as a violation");
            }

            report.AppendLine("RESULT: " + (failed == 0 ? "PASS" : "FAIL")
                              + " passed=" + passed + " failed=" + failed);
            return report.ToString();
        }

        public static MavCombatBoundaryResult Scan()
        {
            MavCombatBoundaryResult result = new MavCombatBoundaryResult();
            result.violations = new List<string>();

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string coreRoot = Path.Combine(projectRoot, CombatCoreRoot.Replace('/', Path.DirectorySeparatorChar));
            string combatRoot = Path.Combine(projectRoot, CombatRoot.Replace('/', Path.DirectorySeparatorChar));

            if (!Directory.Exists(combatRoot))
            {
                // No combat tree yet is a legitimate state, not a failure.
                result.sourcesAvailable = true;
                return result;
            }

            result.sourcesAvailable = true;

            if (Directory.Exists(coreRoot))
            {
                string[] coreFiles = Directory.GetFiles(coreRoot, "*.cs", SearchOption.AllDirectories);
                for (int i = 0; i < coreFiles.Length; i++)
                {
                    result.filesScanned++;
                    string name = Path.GetFileName(coreFiles[i]);
                    string[] lines;
                    try { lines = File.ReadAllLines(coreFiles[i]); }
                    catch (IOException) { result.violations.Add(name + ": could not be read"); continue; }

                    for (int l = 0; l < lines.Length; l++)
                    {
                        string code = StripCommentsAndStringLiterals(lines[l]);
                        if (code.Trim().Length == 0)
                            continue;

                        CheckTokens(result, name, l + 1, code, FlightDynamicsImplementationTypes,
                            "C-1 references a flight-dynamics implementation type");
                        CheckTokens(result, name, l + 1, code, RigidbodyWriteTokens,
                            "C-2 writes to a Rigidbody");
                        CheckTokens(result, name, l + 1, code, SceneSearchTokens,
                            "C-3 searches the scene");
                        CheckTokens(result, name, l + 1, code, InputTokens,
                            "C-4 reads input directly");
                    }
                }
            }

            string[] allCombatFiles = Directory.GetFiles(combatRoot, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < allCombatFiles.Length; i++)
            {
                string name = Path.GetFileName(allCombatFiles[i]);
                string[] lines;
                try { lines = File.ReadAllLines(allCombatFiles[i]); }
                catch (IOException) { continue; }

                for (int l = 0; l < lines.Length; l++)
                {
                    string code = StripCommentsAndStringLiterals(lines[l]);
                    if (code.Trim().Length == 0)
                        continue;

                    CheckTokens(result, name, l + 1, code, PrematureImplementationDeclarations,
                        "C-5 declares a later-phase implementation type in a separation-only branch");
                }
            }

            return result;
        }

        private static void CheckTokens(
            MavCombatBoundaryResult result, string file, int line, string code, string[] tokens, string rule)
        {
            for (int i = 0; i < tokens.Length; i++)
            {
                if (code.Contains(tokens[i]))
                {
                    result.violations.Add(file + ":" + line + ": " + rule + " (" + tokens[i] + ")");
                    return;
                }
            }
        }

        public static bool IsViolatingLine(string sourceLine, string[] tokens)
        {
            string code = StripCommentsAndStringLiterals(sourceLine);
            for (int i = 0; i < tokens.Length; i++)
            {
                if (code.Contains(tokens[i]))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Removes line comments and string literals so the extensive prose in this project - which
        /// names the very tokens being searched for - cannot trip the scan. Block comments are not
        /// used in this folder and are not handled.
        /// </summary>
        public static string StripCommentsAndStringLiterals(string sourceLine)
        {
            StringBuilder code = new StringBuilder(sourceLine.Length);
            bool inString = false;
            bool inChar = false;

            for (int i = 0; i < sourceLine.Length; i++)
            {
                char c = sourceLine[i];

                if (!inString && !inChar && c == '/' && i + 1 < sourceLine.Length && sourceLine[i + 1] == '/')
                    break;

                if (!inChar && c == '"' && (i == 0 || sourceLine[i - 1] != '\\'))
                {
                    inString = !inString;
                    continue;
                }

                if (!inString && c == '\'' && (i == 0 || sourceLine[i - 1] != '\\'))
                {
                    inChar = !inChar;
                    continue;
                }

                if (inString || inChar)
                    continue;

                code.Append(c);
            }

            return code.ToString();
        }
    }
}
#endif
