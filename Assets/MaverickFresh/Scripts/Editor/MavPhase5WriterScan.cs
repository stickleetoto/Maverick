#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MaverickFresh.EditorTools
{
    /// <summary>
    /// What KIND of state writer a file is.
    ///
    /// These categories exist because "all physics writers are gated" is a claim that can only be made
    /// honestly if the things it does not cover are named. A setup-only pose write and a per-step
    /// aerodynamic force are both Rigidbody writes and are not the same problem; a component that
    /// mutates another component's drag coefficient is a third thing again, and reporting all three as
    /// one number would hide two of them.
    /// </summary>
    public enum MavWriterCategory
    {
        /// <summary>Applies force/torque to the player every physics step. Must be gated.</summary>
        PerStepPhysical = 0,

        /// <summary>Sets pose or velocity during setup or a transition, not per step. Phase 5C must convert these into an explicit state handover.</summary>
        TransitionOrSetupPose = 1,

        /// <summary>A deliberately scoped exemption - weapon recoil, landing-gear drag - out of scope for this work but named rather than ignored.</summary>
        ScopedExemption = 2,

        /// <summary>Writes to a Rigidbody that is never the player's.</summary>
        NotPlayerBody = 3,

        /// <summary>Writes no Rigidbody state but mutates another component's physical parameters.</summary>
        IndirectParameterMutator = 4,

        /// <summary>The replacement stack's own load-application boundary.</summary>
        ReplacementBoundary = 5
    }

    /// <summary>One source file's live-Rigidbody-write profile.</summary>
    public struct MavPhysicsWriterFile
    {
        public string fileName;
        /// <summary>Path relative to the scripts root. Recorded because classification is keyed
        /// by file NAME, and two files can share one - see duplicateBaseNames.</summary>
        public string relativePath;
        public int writeSites;
        public int gateChecks;
        public bool gated;
        public bool exempt;
        public MavWriterCategory category;
        public string categoryReason;
    }

    /// <summary>Result of scanning for ungated player-physics writers.</summary>
    public struct MavPhysicsWriterScanResult
    {
        public bool sourcesAvailable;
        public int filesScanned;
        public List<MavPhysicsWriterFile> writerFiles;
        public List<string> ungatedPlayerWriters;

        /// <summary>
        /// File names that appear more than once under the scripts root.
        ///
        /// WHY THIS IS TRACKED. Every classification in this scan is keyed by file NAME, so two files
        /// sharing a name are classified as if they were one component. That is not hypothetical:
        /// MavLandingGearSystem.cs exists twice - once in Aircraft/ inside namespace MaverickFresh
        /// with a gear-drag AddForce, and once in Aero/ at global scope with no Rigidbody writes at
        /// all. C# treats them as two different types, so it compiles, and the scan silently reported
        /// one verdict for two components. The one the scene actually references is the one WITHOUT
        /// the force, which means a Phase 5A conclusion about gear drag needing to be gated was drawn
        /// from a file that nothing instantiates.
        /// </summary>
        public List<string> duplicateBaseNames;

        /// <summary>Files in one category.</summary>
        public List<MavPhysicsWriterFile> InCategory(MavWriterCategory category)
        {
            List<MavPhysicsWriterFile> output = new List<MavPhysicsWriterFile>();
            for (int i = 0; i < writerFiles.Count; i++)
            {
                if (writerFiles[i].category == category)
                    output.Add(writerFiles[i]);
            }

            return output;
        }

        public int CountInCategory(MavWriterCategory category)
        {
            return InCategory(category).Count;
        }
    }

    /// <summary>
    /// Proves which components may write force, torque or velocity to the live player Rigidbody, and
    /// that each of them consults the Phase 5 ownership gate before doing so.
    ///
    /// WHY THIS IS A SOURCE SCAN. Phase 5A's whole claim is that ownership is mechanically
    /// trustworthy. A runtime test can show that the writers it happens to exercise are gated; only a
    /// scan of the tree can show that there is no OTHER writer, including one added next month by
    /// someone who never read this file. The issue is explicit that a component being enabled is not
    /// proof of ownership - the corollary is that a passing runtime test is not proof of coverage.
    ///
    /// It is deliberately blunt: any file containing a live Rigidbody write must either consult the
    /// gate or be named in the exemption list with a reason. Blunt is the point; a scan with clever
    /// exceptions is a scan that can be argued around.
    /// </summary>
    public static class MavPhase5WriterScan
    {
        /// <summary>
        /// Calls that write motion state to a Rigidbody. Kept as strings so the scan sees what a
        /// reader of the source would see.
        /// </summary>
        private static readonly string[] WriteCalls =
        {
            "AddForce(",
            "AddTorque(",
            "AddRelativeForce(",
            "AddRelativeTorque(",
            "AddForceAtPosition(",
            "AddExplosionForce(",
            "MovePosition(",
            "MoveRotation(",
            ".linearVelocity =",
            ".angularVelocity =",
            ".velocity ="
        };

        /// <summary>Expressions that count as consulting the ownership gate.</summary>
        private static readonly string[] GateChecks =
        {
            "LegacyPhysicsAllowed",
            "ReplacementOwnershipGranted",
            "ReplacementPhysicsAllowed",
            "IsShadowComputeOnly"
        };

        /// <summary>
        /// Files whose Rigidbody writes are NOT subject to the player ownership gate, each for a
        /// stated reason. Every entry is a deliberate architectural decision.
        ///
        ///   MavSixDoFBody.cs
        ///       The replacement stack's own load-application boundary. It consults the gate through
        ///       ReplacementOwnershipGranted, so it is gated - it appears here only because its writes
        ///       are the replacement side rather than the legacy side.
        ///
        ///   MavEnemyF15Spawner.cs, MavAITargetDrone.cs, MavGunTracerImpactVfx.cs
        ///       Write to OTHER Rigidbodies - AI aircraft, drones, tracer VFX - never the player's.
        ///       Player ownership has nothing to say about them.
        ///
        ///   MavFreshBootstrap.cs, MavInGameBootstrap.cs, MavMouseFlightJet.cs (air start)
        ///       One-shot air-start pose and velocity during setup, not a per-step physical
        ///       contribution. Phase 5C will have to hand state over at the transition instead, and
        ///       that is called out as an open risk rather than silently permitted here.
        ///
        ///   MavCASWeaponSystem.cs
        ///       Gun recoil, an event-driven weapon effect. Weapons are explicitly out of scope for
        ///       this work; listed so the scan does not silently ignore it.
        ///
        ///   MavLandingGearSystem.cs
        ///       Gear drag. Not present on Mav_Player in Mav_InGame today, and gear is out of scope
        ///       for Phase 5A. It MUST be gated before replacement mode goes live.
        /// </summary>
        public static readonly string[] ExemptFileNames =
        {
            "MavSixDoFBody.cs",
            "MavEnemyF15Spawner.cs",
            "MavAITargetDrone.cs",
            "MavGunTracerImpactVfx.cs",
            "MavFreshBootstrap.cs",
            "MavInGameBootstrap.cs",
            "MavCASWeaponSystem.cs",
            "MavLandingGearSystem.cs"
        };

        /// <summary>
        /// The category each known writer falls into, and why.
        ///
        /// Anything not listed defaults to PerStepPhysical - the strictest reading - so a new writer is
        /// treated as needing a gate until somebody deliberately classifies it otherwise. Failing open
        /// here would mean an unclassified writer silently counted as harmless.
        /// </summary>
        public static MavWriterCategory ClassifyFile(string fileName, out string reason)
        {
            switch (fileName)
            {
                case "MavMouseFlightJet.cs":
                    reason = "thrust, drag, assists and control torque every physics step";
                    return MavWriterCategory.PerStepPhysical;

                case "MavAeroBody.cs":
                    reason = "lift, drag, gravity and static stability every physics step";
                    return MavWriterCategory.PerStepPhysical;

                case "MavAtmosphericEngine.cs":
                    reason = "wave/transonic drag every physics step";
                    return MavWriterCategory.PerStepPhysical;

                case "MavThrustVectorControl.cs":
                    reason = "thrust-vectoring torque every physics step";
                    return MavWriterCategory.PerStepPhysical;

                case "MavSixDoFBody.cs":
                    reason = "the replacement stack's single load-application boundary; gated by the "
                             + "ownership authority rather than by the legacy gate";
                    return MavWriterCategory.ReplacementBoundary;

                case "MavRuntimeHandoverTarget.cs":
                    reason = "writes Rigidbody pose, velocities and mass properties ONLY while an "
                             + "ownership handover is executing or rolling back - never per step. It "
                             + "is also a plain class that nothing constructs, so no gameplay path "
                             + "reaches it. Phase 5C-R must confirm the transition write is the only "
                             + "one, the same requirement the bootstraps carry";
                    return MavWriterCategory.TransitionOrSetupPose;

                case "MavFreshBootstrap.cs":
                case "MavInGameBootstrap.cs":
                    reason = "one-shot air-start pose and velocity during setup, not a per-step "
                             + "physical contribution. Phase 5C must replace this with an explicit "
                             + "state handover at the ownership transition";
                    return MavWriterCategory.TransitionOrSetupPose;

                case "MavCASWeaponSystem.cs":
                    reason = "gun recoil impulse, event-driven; weapons are out of scope for this work";
                    return MavWriterCategory.ScopedExemption;

                case "MavLandingGearSystem.cs":
                    reason = "landing-gear drag; not on Mav_Player in Mav_InGame today, and gear is out "
                             + "of scope for Phase 5A. MUST be gated before replacement mode goes live";
                    return MavWriterCategory.ScopedExemption;

                case "MavCombatFlapSystem.cs":
                    reason = "writes no Rigidbody state but mutates MavAeroBody.cd0 and lift "
                             + "multipliers, so it changes what the aero writer applies";
                    return MavWriterCategory.IndirectParameterMutator;

                case "MavManeuverDiagnostics.cs":
                    reason = "Phase 5B recorder. Writes no Rigidbody state and applies no load, but "
                             + "while a scripted maneuver case runs it substitutes the pilot command "
                             + "the existing control path acts on, so it is an indirect mutator "
                             + "rather than a pure observer";
                    return MavWriterCategory.IndirectParameterMutator;

                case "MavEnemyF15Spawner.cs":
                case "MavAITargetDrone.cs":
                case "MavGunTracerImpactVfx.cs":
                    reason = "writes to AI aircraft, drone or tracer Rigidbodies, never the player's";
                    return MavWriterCategory.NotPlayerBody;

                default:
                    reason = "not classified; treated as a per-step player writer until it is";
                    return MavWriterCategory.PerStepPhysical;
            }
        }

        /// <summary>Components that mutate another component's physical parameters without writing Rigidbody state.</summary>
        /// <summary>
        /// Duplicate file names that are KNOWN, TRACKED defects rather than surprises.
        ///
        /// Pinning one does not make it acceptable - defect P5B5-D3 is open, and the entry says so.
        /// It only separates "a duplicate we have written down and understand" from "a duplicate that
        /// appeared since anyone last looked", which is the distinction that lets this scan stay
        /// useful as a gate instead of being permanently red and therefore ignored.
        /// </summary>
        public static readonly string[] KnownDuplicateBaseNames =
        {
            // P5B5-D3. Two different TYPES share this name: Aircraft/ declares it inside namespace
            // MaverickFresh and applies a gear-drag AddForce; Aero/ declares it at global scope and
            // writes no Rigidbody state. C# keeps them distinct, so it compiles. The scene references
            // the Aero/ one, i.e. the one with no force - so the Phase 5A note that gear drag must be
            // gated before replacement mode was drawn from a file nothing instantiates.
            "MavLandingGearSystem.cs"
        };

        public static bool IsKnownDuplicateBaseName(string fileName)
        {
            for (int i = 0; i < KnownDuplicateBaseNames.Length; i++)
            {
                if (KnownDuplicateBaseNames[i] == fileName)
                    return true;
            }

            return false;
        }

        public static readonly string[] IndirectMutatorFileNames =
        {
            "MavCombatFlapSystem.cs",

            // Phase 5B. Applies no force and writes no Rigidbody state, but while a scripted
            // maneuver case is running it substitutes the pilot command the control path acts on.
            // That is an indirect mutation, and a scan whose purpose is to name every writer would
            // be lying by omission if it left this one out.
            "MavManeuverDiagnostics.cs"
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

        /// <summary>Whether this line of executable code writes Rigidbody motion state.</summary>
        public static bool IsLiveWriteSite(string line)
        {
            string code = StripCommentsAndStringLiterals(line);
            if (string.IsNullOrEmpty(code))
                return false;

            for (int i = 0; i < WriteCalls.Length; i++)
            {
                if (code.Contains(WriteCalls[i]))
                    return true;
            }

            return false;
        }

        /// <summary>Whether this line consults the ownership gate.</summary>
        public static bool IsGateCheck(string line)
        {
            string code = StripCommentsAndStringLiterals(line);
            if (string.IsNullOrEmpty(code))
                return false;

            for (int i = 0; i < GateChecks.Length; i++)
            {
                if (code.Contains(GateChecks[i]))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Removes comments and string literals, so the extensive prose about AddForce in this project
        /// does not register as calls to it.
        /// </summary>
        public static string StripCommentsAndStringLiterals(string line)
        {
            if (string.IsNullOrEmpty(line))
                return string.Empty;

            StringBuilder output = new StringBuilder(line.Length);
            bool inString = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (!inString && c == '/' && i + 1 < line.Length && (line[i + 1] == '/' || line[i + 1] == '*'))
                    break;

                if (c == '"' && (i == 0 || line[i - 1] != '\\'))
                {
                    inString = !inString;
                    continue;
                }

                if (!inString)
                    output.Append(c);
            }

            return output.ToString();
        }

        public static bool IsIndirectMutator(string fileName)
        {
            for (int i = 0; i < IndirectMutatorFileNames.Length; i++)
            {
                if (IndirectMutatorFileNames[i] == fileName)
                    return true;
            }

            return false;
        }

        public static MavPhysicsWriterScanResult Scan(string assetsRoot, out string reason)
        {
            MavPhysicsWriterScanResult result = new MavPhysicsWriterScanResult();
            result.writerFiles = new List<MavPhysicsWriterFile>();
            result.ungatedPlayerWriters = new List<string>();
            result.duplicateBaseNames = new List<string>();

            string root = Path.Combine(assetsRoot, "MaverickFresh/Scripts");
            if (!Directory.Exists(root))
            {
                result.sourcesAvailable = false;
                reason = "scripts root not found at " + root;
                return result;
            }

            result.sourcesAvailable = true;
            reason = root;

            string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);

            // Duplicate base names first, because everything below is keyed by name and a duplicate
            // makes those keys ambiguous. Reported rather than silently tolerated.
            Dictionary<string, int> nameCounts = new Dictionary<string, int>();
            for (int i = 0; i < files.Length; i++)
            {
                string n = Path.GetFileName(files[i]);
                nameCounts[n] = nameCounts.ContainsKey(n) ? nameCounts[n] + 1 : 1;
            }
            foreach (KeyValuePair<string, int> kv in nameCounts)
            {
                if (kv.Value > 1)
                    result.duplicateBaseNames.Add(kv.Key + " x" + kv.Value);
            }


            for (int f = 0; f < files.Length; f++)
            {
                string fileName = Path.GetFileName(files[f]);
                result.filesScanned++;

                // Validation and editor-tool files talk ABOUT writes without being runtime writers.
                if (fileName.Contains("Validation") || fileName.Contains("Scan"))
                    continue;

                string[] lines = File.ReadAllLines(files[f]);
                int writes = 0;
                int gates = 0;

                for (int i = 0; i < lines.Length; i++)
                {
                    if (IsLiveWriteSite(lines[i]))
                        writes++;

                    if (IsGateCheck(lines[i]))
                        gates++;
                }

                bool indirect = IsIndirectMutator(fileName);
                if (writes == 0 && !indirect)
                    continue;

                MavPhysicsWriterFile entry = new MavPhysicsWriterFile();
                entry.fileName = fileName;
                entry.relativePath = files[f].Length > root.Length
                    ? files[f].Substring(root.Length).Replace('\\', '/').TrimStart('/')
                    : files[f];
                entry.writeSites = writes;
                entry.gateChecks = gates;
                entry.exempt = IsExemptFile(fileName);
                entry.gated = gates > 0;

                string categoryReason;
                entry.category = ClassifyFile(fileName, out categoryReason);
                entry.categoryReason = categoryReason;
                result.writerFiles.Add(entry);

                // ONLY the per-step physical category is required to consult the gate. The other
                // categories are reported separately and deliberately - see MavWriterCategory.
                if (entry.category == MavWriterCategory.PerStepPhysical && !entry.gated)
                {
                    result.ungatedPlayerWriters.Add(
                        fileName + " has " + writes + " live Rigidbody write site(s) every physics step "
                        + "and never consults the ownership gate. Either gate it, or classify it in "
                        + "ClassifyFile with a reason it cannot affect the player per step.");
                }
            }

            return result;
        }
    }
}
#endif
