#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;

namespace MaverickFresh.EditorTools
{
    /// <summary>One scene's aircraft-identity wiring.</summary>
    public struct MavSceneWiring
    {
        public string sceneName;
        public bool hasProfileApplier;
        public bool hasVisualSwitcher;
        public bool hasAircraftAwareTuning;
        public bool hasInGameBootstrap;
        public bool hasFreshBootstrap;

        /// <summary>
        /// Whether SOMETHING in this scene will apply the player's selected aircraft.
        ///
        /// MavInGameBootstrap does it. MavFreshBootstrap does it through
        /// EnsureAuthoritativeAircraftApplied. A scene with neither has an applier that nobody drives.
        /// </summary>
        public bool HasIdentityOwner
        {
            get { return hasInGameBootstrap || hasFreshBootstrap; }
        }

        /// <summary>
        /// A scene that carries the aircraft machinery but nothing to drive it. The player object has
        /// an applier and a visual switcher, so it LOOKS wired, and at runtime the aircraft is never
        /// applied at all.
        /// </summary>
        public bool IsMissingIdentityOwner
        {
            get { return (hasProfileApplier || hasAircraftAwareTuning) && !HasIdentityOwner; }
        }
    }

    /// <summary>Result of scanning scenes for aircraft-identity wiring.</summary>
    public struct MavSceneWiringScanResult
    {
        public bool sourcesAvailable;
        public int scenesScanned;
        public List<MavSceneWiring> scenes;
        public List<string> violations;
    }

    /// <summary>
    /// Scans SCENES for the aircraft-identity handoff.
    ///
    /// This exists because of a specific failure that every other check in this project missed.
    /// Mav_InGame contains MavFreshBootstrap, MavAircraftProfileApplier, MavAircraftVisualSwitcher
    /// and MavWTFeelPolishController - but NOT MavInGameBootstrap, which was the only component that
    /// applied the session's selected aircraft. So in the scene the game actually loads, nothing
    /// performed the handoff.
    ///
    /// It stayed invisible because MavWTFeelPolishController was reading the applier's serialized
    /// `aircraft` field, default F22A, and applying that. The aircraft was always configured, just
    /// always as the F-22. Removing that read fixed the wrong-aircraft symptom and exposed the
    /// underlying hole: Debug Applied Aircraft = none.
    ///
    /// Every component-level test built its own rig and applied the aircraft by hand, so none of them
    /// could see this. The missing fact was about the SCENE, so this check reads scenes.
    /// </summary>
    public static class MavAircraftSceneWiringScan
    {
        private const string ApplierScript =
            "MaverickFresh/Scripts/Aircraft/MavAircraftProfileApplier.cs";
        private const string SwitcherScript =
            "MaverickFresh/Scripts/Aircraft/MavAircraftVisualSwitcher.cs";
        private const string TuningScript =
            "MaverickFresh/Scripts/MavWTFeelPolishController.cs";
        private const string InGameBootstrapScript =
            "MaverickFresh/Scripts/Modes/MavInGameBootstrap.cs";
        private const string FreshBootstrapScript =
            "MaverickFresh/Scripts/MavFreshBootstrap.cs";

        /// <summary>
        /// Reads a script's GUID from its .meta file rather than hard-coding it, so re-importing or
        /// moving a script cannot turn this scan into a no-op that always passes.
        /// </summary>
        public static string ReadScriptGuid(string assetsRoot, string relativeScriptPath)
        {
            if (string.IsNullOrEmpty(assetsRoot) || string.IsNullOrEmpty(relativeScriptPath))
                return null;

            string meta = Path.Combine(assetsRoot, relativeScriptPath) + ".meta";
            if (!File.Exists(meta))
                return null;

            string[] lines = File.ReadAllLines(meta);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.StartsWith("guid:", System.StringComparison.Ordinal))
                    return line.Substring(5).Trim();
            }

            return null;
        }

        /// <summary>
        /// Confirms that MavFreshBootstrap really performs the handoff, and performs it EARLY ENOUGH.
        ///
        /// Counting the component in a scene is not enough. MavFreshBootstrap was present in
        /// Mav_InGame the whole time the bug existed - it simply never applied an aircraft, so
        /// treating its presence as "this scene has an identity owner" would call the broken state
        /// clean. Two things are therefore checked in the source:
        ///
        ///   - it resolves the aircraft from MavGameSession, the player's actual selection
        ///   - it applies it BEFORE the aircraft-aware WT polish block, not after
        ///
        /// The ordering half matters as much as the existence half: applying the aircraft after the
        /// aircraft-aware consumers have run leaves them reading an unapplied aircraft, which is the
        /// shape of the original fault.
        /// </summary>
        public static bool VerifyFreshBootstrapAppliesSelection(string assetsRoot, out string reason)
        {
            string path = Path.Combine(assetsRoot, FreshBootstrapScript);
            if (!File.Exists(path))
            {
                reason = "MavFreshBootstrap.cs not found at " + path;
                return false;
            }

            string text = File.ReadAllText(path);

            // The CALL, with its semicolon - not the method definition, which carries the same name
            // and sits further down the file. Matching the definition would report an ordering
            // failure for code that never calls the method at all.
            int applyAt = text.IndexOf(
                "EnsureAuthoritativeAircraftApplied();", System.StringComparison.Ordinal);
            if (applyAt < 0)
            {
                reason = "MavFreshBootstrap does not apply an aircraft at all, so its presence in a "
                         + "scene does not make the selection reach the player";
                return false;
            }

            if (text.IndexOf("MavGameSession.SelectedAircraft", System.StringComparison.Ordinal) < 0)
            {
                reason = "MavFreshBootstrap applies an aircraft without consulting "
                         + "MavGameSession.SelectedAircraft, so it is not honouring the player's "
                         + "selection";
                return false;
            }

            // The BLOCK, not the field declaration: "installWTPolish" as a bare needle matches the
            // public field near the top of the file, which naturally precedes everything and made
            // this check fail against correct code.
            int tuningAt = text.IndexOf("if (installWTPolish)", System.StringComparison.Ordinal);
            if (tuningAt >= 0 && applyAt > tuningAt)
            {
                reason = "MavFreshBootstrap applies the aircraft AFTER its aircraft-aware WT polish "
                         + "block, so that block runs against an unapplied aircraft";
                return false;
            }

            reason = "MavFreshBootstrap applies the session selection before anything aircraft-aware";
            return true;
        }

        public static MavSceneWiringScanResult Scan(string assetsRoot)
        {
            MavSceneWiringScanResult result = new MavSceneWiringScanResult();
            result.scenes = new List<MavSceneWiring>();
            result.violations = new List<string>();

            if (string.IsNullOrEmpty(assetsRoot) || !Directory.Exists(assetsRoot))
            {
                result.sourcesAvailable = false;
                return result;
            }

            string applierGuid = ReadScriptGuid(assetsRoot, ApplierScript);
            string switcherGuid = ReadScriptGuid(assetsRoot, SwitcherScript);
            string tuningGuid = ReadScriptGuid(assetsRoot, TuningScript);
            string inGameGuid = ReadScriptGuid(assetsRoot, InGameBootstrapScript);
            string freshGuid = ReadScriptGuid(assetsRoot, FreshBootstrapScript);

            // Without the GUIDs there is nothing to match on, and a scan that cannot match must say
            // so rather than report every scene clean.
            if (applierGuid == null || inGameGuid == null || freshGuid == null)
            {
                result.sourcesAvailable = false;
                result.violations.Add(
                    "Could not read the script GUIDs needed to scan scenes, so scene wiring was NOT "
                    + "checked. This is a scan failure, not a clean result.");
                return result;
            }

            result.sourcesAvailable = true;
            string[] scenes = Directory.GetFiles(assetsRoot, "*.unity", SearchOption.AllDirectories);

            for (int i = 0; i < scenes.Length; i++)
            {
                string text = File.ReadAllText(scenes[i]);

                MavSceneWiring wiring = new MavSceneWiring();
                wiring.sceneName = Path.GetFileNameWithoutExtension(scenes[i]);
                wiring.hasProfileApplier = text.Contains(applierGuid);
                wiring.hasVisualSwitcher = switcherGuid != null && text.Contains(switcherGuid);
                wiring.hasAircraftAwareTuning = tuningGuid != null && text.Contains(tuningGuid);
                wiring.hasInGameBootstrap = text.Contains(inGameGuid);
                wiring.hasFreshBootstrap = text.Contains(freshGuid);

                result.scenesScanned++;
                result.scenes.Add(wiring);

                if (wiring.IsMissingIdentityOwner)
                {
                    result.violations.Add(
                        wiring.sceneName + ": carries the aircraft machinery (applier="
                        + wiring.hasProfileApplier + ", visual switcher=" + wiring.hasVisualSwitcher
                        + ", aircraft-aware tuning=" + wiring.hasAircraftAwareTuning
                        + ") but NO component that applies the selected aircraft. Nothing will hand "
                        + "the session selection to the player, so the aircraft is never applied.");
                }
            }

            return result;
        }
    }
}
#endif
