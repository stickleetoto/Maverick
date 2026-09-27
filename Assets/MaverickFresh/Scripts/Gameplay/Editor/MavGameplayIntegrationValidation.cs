#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using MaverickFresh.EditorTools;
using MaverickFresh.FlightDynamics;
using MaverickFresh.FlightDynamics.F15;
using Object = UnityEngine.Object;

namespace MaverickFresh.Gameplay.EditorTools
{
    /// <summary>
    /// Gameplay Integration R1 - headless game-flow validation (the Play Mode flow is MavGameplayFlowValidationRunner).
    ///
    ///   [G1]  the player-facing list is exactly F-15, F-16
    ///   [G2]  the F-15 selection maps to the pilot-controlled F-15 identity, under a neutral player name
    ///   [G3]  a normal F-15 spawn is AssistedV2: V2 enabled and bound, V1 not requesting
    ///   [G4]  normal gameplay has no route to DirectV1 (spawn result, API surface, source scan)
    ///   [G5]  selecting the F-15 launches the F-15 and nothing else
    ///   [G6]  selecting the F-16 launches nothing and substitutes nothing
    ///   [G7]  unfinished aircraft are never reported playable
    ///   [G8]  a refused launch keeps the player's selection and leaves no flight pending
    ///   [G9]  restart keeps the aircraft identity
    ///   [G10] return-to-hangar keeps the selection
    ///   [G14] the F-16 status is the code's truth (hold, bench input, undeclared engine envelope)
    ///   [G15] scenes: one camera, one listener, one director, legacy stack saved inactive, no missing scripts,
    ///         the official scene-wiring scan still clean
    ///   [G16] no gameplay runtime file writes Rigidbody motion state (the official writer-scan rule)
    ///   [G17] player-facing text carries no prototype / wrong-variant / harness wording
    /// [G11]-[G13] (exactly one aircraft, camera, input owner) need a running flight: Play Mode runner.
    /// </summary>
    public static class MavGameplayIntegrationValidation
    {
        private const string GameplayRelative = "MaverickFresh/Scripts/Gameplay";

        public static string RunAll(out int passed, out int failed)
        {
            passed = 0;
            failed = 0;
            StringBuilder report = new StringBuilder(16 * 1024);
            report.AppendLine("MAVERICK GAMEPLAY INTEGRATION R1 - headless game-flow validation");

            MavAircraftKind savedAircraft = MavGameSession.SelectedAircraft;
            MavGameMode savedMode = MavGameSession.SelectedMode;
            bool savedHasSelection = MavGameSession.HasSelection;
            MavFlightLaunchRequest savedLaunch = MavGameSession.CurrentLaunch;
            bool savedHasLaunch = MavGameSession.HasLaunchRequest;
            int prefAircraft = PlayerPrefs.GetInt("MavSelectedAircraft", -1);
            int prefHas = PlayerPrefs.GetInt("MavHasSelection", -1);
            int prefMode = PlayerPrefs.GetInt("MavSelectedMode", -1);

            try
            {
                Catalog(report, ref passed, ref failed);
                Spawn(report, ref passed, ref failed);
                NoDirectV1(report, ref passed, ref failed);
                Launches(report, ref passed, ref failed);
                F16Truth(report, ref passed, ref failed);
                Scenes(report, ref passed, ref failed);
                WriterScan(report, ref passed, ref failed);
                Wording(report, ref passed, ref failed);
            }
            catch (Exception e)
            {
                failed++;
                report.AppendLine("  FAIL  exception: " + e);
            }
            finally
            {
                MavGameSession.SelectedAircraft = savedAircraft;
                MavGameSession.SelectedMode = savedMode;
                MavGameSession.HasSelection = savedHasSelection;
                MavGameSession.CurrentLaunch = savedLaunch;
                MavGameSession.HasLaunchRequest = savedHasLaunch;
                RestorePref("MavSelectedAircraft", prefAircraft);
                RestorePref("MavHasSelection", prefHas);
                RestorePref("MavSelectedMode", prefMode);
                PlayerPrefs.Save();
            }

            report.AppendLine();
            report.AppendLine("RESULT: " + (failed == 0 ? "PASS" : "FAIL") + "  passed=" + passed + " failed=" + failed);
            return report.ToString();
        }

        // ---------------------------------------------------------------- [G1] [G2] [G7]

        private static void Catalog(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[G1][G2][G7] The player-facing aircraft");
            List<MavPlayableAircraftDefinition> player = MavPlayableAircraftRegistry.PlayerFacing();
            Record(player.Count == 2 && player[0].aircraft == MavAircraftKind.F15E && player[1].aircraft == MavAircraftKind.F16C,
                "G1  the player-facing list is exactly [F-15, F-16] (" + Names(player) + ")", report, ref passed, ref failed);

            MavPlayableAircraftDefinition f15 = player[0];
            string upper = f15.displayName.ToUpperInvariant();
            Record(f15.aircraft == MavAircraftKind.F15E && f15.displayName == "F-15 EAGLE" && !upper.Contains("EX") && !upper.Contains("836")
                   && !upper.Contains("NASA") && f15.physicsConfigurationId == MavF15PilotControlledIdentity.ConfigurationId
                   && f15.spawnStrategy == MavAircraftSpawnStrategy.PilotControlledF15AssistedV2 && f15.status == MavPlayableAircraftStatus.Playable
                   && f15.IsLaunchable,
                "G2  F-15: serialized id " + f15.aircraft + " (catalog id '" + MavAircraftCatalog.CanonicalAircraftId(f15.aircraft)
                + "'), player name '" + f15.displayName + "', physics '" + f15.physicsConfigurationId + "', strategy " + f15.spawnStrategy
                + ", " + f15.status, report, ref passed, ref failed);

            List<MavPlayableAircraftDefinition> dev = MavPlayableAircraftRegistry.Development();
            bool devClean = dev.Count >= 3;
            foreach (MavPlayableAircraftDefinition d in dev)
            {
                MavPlayableAircraftDefinition p;
                string r;
                devClean &= d.status == MavPlayableAircraftStatus.Development && !d.IsLaunchable
                            && d.spawnStrategy == MavAircraftSpawnStrategy.LegacyMouseFlightDevelopment
                            && d.displayName.Contains("LEGACY");
                if (d.aircraft != MavAircraftKind.F16C)
                    devClean &= !MavPlayableAircraftRegistry.CanLaunchAsPlayer(d.aircraft, out p, out r)
                                && !MavPlayableAircraftRegistry.TryGetPlayerFacing(d.aircraft, out p);
            }

            MavPlayableAircraftDefinition any;
            string reason;
            bool f22Refused = !MavPlayableAircraftRegistry.CanLaunchAsPlayer(MavAircraftKind.F22A, out any, out reason);
            Record(devClean && f22Refused,
                "G7  F/A-18, F-22, F-35 (and the legacy F-16) are Development: not launchable, legacy-labelled, not in the player list (" + reason + ")",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [G3]

        private static void Spawn(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[G3] A normal F-15 spawn");
            MavSpawnedAircraft a;
            string reason;
            bool ok = MavPlayableAircraftSpawner.TrySpawn(MavPlayableAircraftRegistry.F15(), out a, out reason);
            try
            {
                string verify = "";
                bool v2 = ok && MavPlayableAircraftSpawner.VerifyAssistedV2(a.rig, out verify);
                Record(ok && v2 && a.rig.controlMode == MavF15PilotControlMode.AssistedV2 && a.rig.lawV2.enabled && a.body.controlLaw == a.rig.lawV2
                       && a.rig.law != null && !a.rig.law.enabled,
                    "G3  spawned in AssistedV2: V2 law enabled and bound, V1 law present but disabled (" + reason + (v2 ? "" : "; " + verify) + ")",
                    report, ref passed, ref failed);

                bool noCamera = ok && a.rig.GetComponentInChildren<Camera>(true) == null && a.gameplayRoot.GetComponentInChildren<Camera>(true) == null
                                && a.rig.GetComponentInChildren<AudioListener>(true) == null && a.gameplayRoot.GetComponentInChildren<AudioListener>(true) == null;
                bool noRendererOnPhysics = ok && a.rig.GetComponentInChildren<Renderer>(true) == null;
                bool noColliders = ok && a.gameplayRoot.GetComponentInChildren<Collider>(true) == null && a.rig.GetComponentInChildren<Collider>(true) == null;
                Record(noCamera && noRendererOnPhysics && noColliders && ok && a.rig.GetComponent<MavF15PilotControlledHud>() == null,
                    "G3  the player F-15 brings no camera, audio listener, developer HUD or collider; the physics root carries no renderer",
                    report, ref passed, ref failed);

                Record(ok && a.input.source == a.commandSource && a.commandSource == a.rig.commandSource && a.body.pilotCommandSource == a.commandSource
                       && a.commandSource.IsOperationalCommandSource && !a.input.inputEnabled && a.renderPose.physicsTarget == a.rig.transform,
                    "G3  one input owner writes the rig's one operational source; input is OFF until the flight starts; the render pose follows the rig",
                    report, ref passed, ref failed);
            }
            finally
            {
                if (a != null)
                {
                    if (a.rig != null) Object.DestroyImmediate(a.rig.gameObject);
                    if (a.gameplayRoot != null) Object.DestroyImmediate(a.gameplayRoot);
                }
            }

            int rigsBefore = Object.FindObjectsByType<MavF15PilotControlledRig>(FindObjectsSortMode.None).Length;
            MavSpawnedAircraft none;
            bool f16Refused = !MavPlayableAircraftSpawner.TrySpawn(MavPlayableAircraftRegistry.F16(), out none, out reason);
            int rigsAfter = Object.FindObjectsByType<MavF15PilotControlledRig>(FindObjectsSortMode.None).Length;
            Record(f16Refused && none == null && rigsAfter == rigsBefore,
                "G6  the spawner refuses the F-16 and builds nothing in its place (" + reason + ")", report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [G4]

        private static void NoDirectV1(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[G4] Normal gameplay cannot select DirectV1");

            // API surface: nothing in the gameplay layer takes, stores or exposes a control mode.
            List<string> offenders = new List<string>();
            foreach (Type t in typeof(MavFlightSessionDirector).Assembly.GetTypes())
            {
                if (t.Namespace != "MaverickFresh.Gameplay")
                    continue;
                const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
                foreach (FieldInfo f in t.GetFields(all))
                    if (f.FieldType == typeof(MavF15PilotControlMode)) offenders.Add(t.Name + "." + f.Name);
                foreach (PropertyInfo p in t.GetProperties(all))
                    if (p.PropertyType == typeof(MavF15PilotControlMode)) offenders.Add(t.Name + "." + p.Name);
                foreach (MethodInfo m in t.GetMethods(all))
                    foreach (ParameterInfo pi in m.GetParameters())
                        if (pi.ParameterType == typeof(MavF15PilotControlMode)) offenders.Add(t.Name + "." + m.Name + "(" + pi.Name + ")");
            }

            Record(offenders.Count == 0,
                "G4  no gameplay type has a control-mode field, property or parameter" + (offenders.Count > 0 ? ": " + string.Join(", ", offenders.ToArray()) : ""),
                report, ref passed, ref failed);

            // Source: the gameplay runtime never names V1, never switches laws, never adds the law-switching HUD.
            string[] banned = { "DirectV1", "TrySetControlMode", "lawSelectKey", "AddComponent<MavF15PilotControlledHud>", "MavF15PilotControlledPrefabBuilder" };
            List<string> hits = new List<string>();
            int scanned = 0;
            foreach (string file in RuntimeGameplaySources())
            {
                scanned++;
                // Code only: prose in comments may explain what the layer does NOT do.
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    string code = CodeWithoutComment(lines[i]);
                    foreach (string ban in banned)
                        if (code.Contains(ban)) hits.Add(Path.GetFileName(file) + ":" + (i + 1) + ":" + ban);
                }
            }

            Record(scanned > 0 && hits.Count == 0,
                "G4  none of " + scanned + " gameplay runtime sources names DirectV1, a law switch, the law-switching HUD or the prefab builder"
                + (hits.Count > 0 ? ": " + string.Join(", ", hits.ToArray()) : ""), report, ref passed, ref failed);

            MavPlayableAircraftDefinition f15 = MavPlayableAircraftRegistry.F15();
            Record(f15.spawnStrategy == MavAircraftSpawnStrategy.PilotControlledF15AssistedV2 && f15.physicsPathDescription.Contains("AssistedV2"),
                "G4  the F-15 definition's only strategy is " + f15.spawnStrategy, report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [G5] [G6] [G8] [G9] [G10]

        private static void Launches(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[G5][G6][G8][G9][G10] Launch, refusal, restart, return");

            MavGameSession.ClearLaunchRequest();
            MavFlightLaunchRequest r15;
            string reason;
            bool f15 = MavFlightLauncher.TryPrepareLaunch(MavAircraftKind.F15E, out r15, out reason);
            Record(f15 && r15.aircraft == MavAircraftKind.F15E && r15.kind == MavFlightLaunchKind.Player && r15.mode == MavGameMode.FreeFlight
                   && r15.attempt == 1 && MavGameSession.HasLaunchRequest && MavGameSession.CurrentLaunch.aircraft == MavAircraftKind.F15E
                   && MavGameSession.SelectedAircraft == MavAircraftKind.F15E,
                "G5  F-15 selected -> a Player launch of the F-15 (" + r15 + ")", report, ref passed, ref failed);

            MavFlightLaunchRequest r16;
            bool f16 = MavFlightLauncher.TryPrepareLaunch(MavAircraftKind.F16C, out r16, out reason);
            Record(!f16 && MavGameSession.SelectedAircraft == MavAircraftKind.F16C && !MavGameSession.HasLaunchRequest && r16.attempt == 0
                   && reason.Contains("NOT READY"),
                "G6/G8  F-16 selected -> refused (\"" + reason + "\"); the selection stays F-16, nothing is pending, nothing substituted",
                report, ref passed, ref failed);

            MavFlightLaunchRequest r22;
            bool f22 = MavFlightLauncher.TryPrepareLaunch(MavAircraftKind.F22A, out r22, out reason);
            Record(!f22 && MavGameSession.SelectedAircraft == MavAircraftKind.F22A && !MavGameSession.HasLaunchRequest,
                "G7/G8  F-22 as a player launch -> refused (\"" + reason + "\"), selection kept, nothing pending", report, ref passed, ref failed);

            MavFlightLauncher.TryPrepareLaunch(MavAircraftKind.F15E, out r15, out reason);
            MavFlightLaunchRequest again = MavFlightLauncher.PrepareRestart(r15);
            Record(again.aircraft == r15.aircraft && again.mode == r15.mode && again.kind == r15.kind && again.attempt == r15.attempt + 1
                   && MavGameSession.CurrentLaunch.attempt == again.attempt && MavGameSession.CurrentLaunch.aircraft == MavAircraftKind.F15E,
                "G9  restart keeps aircraft, mode and launch kind (" + again + ")", report, ref passed, ref failed);

            MavFlightLauncher.PrepareReturnToHangar();
            Record(!MavGameSession.HasLaunchRequest && MavGameSession.HasSelection && MavGameSession.SelectedAircraft == MavAircraftKind.F15E,
                "G10 return to hangar ends the flight request and keeps the F-15 selected", report, ref passed, ref failed);

            MavFlightLaunchRequest dev;
            bool devOk = MavFlightLauncher.TryPrepareDevelopmentLaunch(MavAircraftKind.F22A, out dev, out reason);
            bool devF15 = MavFlightLauncher.TryPrepareDevelopmentLaunch(MavAircraftKind.F15E, out dev, out reason);
            Record(devOk && !devF15, "G7  a development launch exists only for the legacy development aircraft (F-22 yes, F-15 no)",
                report, ref passed, ref failed);
            MavGameSession.ClearLaunchRequest();
        }

        // ---------------------------------------------------------------- [G14]

        private static void F16Truth(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[G14] The F-16 status is the code's truth");
            MavF16GameplayReadinessReport r = MavF16GameplayReadiness.Evaluate();
            string path = Path.Combine(Application.dataPath, "MaverickFresh/Scripts/FlightDynamics/F16/MavF16SelectionAutoSetup.cs");
            string text = File.Exists(path) ? File.ReadAllText(path) : "";
            int reconcile = text.IndexOf("public void ReconcileNow()", StringComparison.Ordinal);
            int holdComment = text.IndexOf("// Safety hold remains mandatory", StringComparison.Ordinal);
            int hold = holdComment >= 0 ? text.IndexOf("sixDoFBody.simulationEnabled = false;", holdComment, StringComparison.Ordinal) : -1;
            bool holdUnconditional = reconcile >= 0 && hold > reconcile
                                     && text.Substring(holdComment, hold - holdComment).Contains("if (sixDoFBody != null)")
                                     && !text.Substring(holdComment, hold - holdComment).Contains("else");
            bool bench = text.Contains("pilotCommandSource.treatAsOperationalSource = false;");
            Record(holdUnconditional == MavF16GameplayReadiness.BindingKeepsSafetyHold,
                "G14 MavF16SelectionBinding still holds simulationEnabled = false unconditionally (" + holdUnconditional + ") - matches the stated blocker",
                report, ref passed, ref failed);
            Record(bench == MavF16GameplayReadiness.BindingCommandSourceIsBench,
                "G14 its pilot command source is still wired non-operational (" + bench + ") - matches the stated blocker", report, ref passed, ref failed);
            string detail;
            bool declared = MavF16GameplayReadiness.EngineSourceEnvelopeDeclared(out detail);
            Record(!r.liveReady && !declared && r.playerBlockers.Length == 3 && MavPlayableAircraftRegistry.F16().status == MavPlayableAircraftStatus.NotReady,
                "G14 F-16 = NOT READY with 3 blockers (" + string.Join("; ", r.playerBlockers) + "); engine: " + detail, report, ref passed, ref failed);
            report.AppendLine("      " + r.summary);
        }

        // ---------------------------------------------------------------- [G15]

        private static void Scenes(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[G15] Scenes");
            string scenes = Path.Combine(Application.dataPath, "MaverickFresh/Scenes");
            string inGame = File.ReadAllText(Path.Combine(scenes, "Mav_InGame.unity"));
            string directorGuid = AssetDatabase.AssetPathToGUID("Assets/" + GameplayRelative + "/MavFlightSessionDirector.cs");
            Record(Count(inGame, "--- !u!20 ") == 1 && Count(inGame, "--- !u!81 ") == 1 && !string.IsNullOrEmpty(directorGuid) && Count(inGame, directorGuid) == 1,
                "G15 Mav_InGame: exactly one Camera, one AudioListener and one flight-session director are saved", report, ref passed, ref failed);

            bool allInactive = true;
            foreach (string name in MavGameplaySceneSetup.LegacyObjectNames)
            {
                Match m = Regex.Match(inGame, "m_Name: " + Regex.Escape(name) + "\\r?\\n(?:.*\\r?\\n){0,5}?\\s*m_IsActive: (\\d)");
                allInactive &= m.Success && m.Groups[1].Value == "0";
            }

            Record(allInactive, "G15 the legacy stack (" + string.Join(", ", MavGameplaySceneSetup.LegacyObjectNames) + ") is saved inactive",
                report, ref passed, ref failed);

            int missing = 0, scripts = 0;
            List<string> missingDetail = new List<string>();
            foreach (string scene in new[] { "Mav_MainLobby.unity", "Mav_Hangar.unity", "Mav_InGame.unity" })
            {
                string text = File.ReadAllText(Path.Combine(scenes, scene));
                foreach (Match m in Regex.Matches(text, "m_Script: \\{fileID: \\d+, guid: ([0-9a-f]{32})"))
                {
                    scripts++;
                    if (string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(m.Groups[1].Value)))
                    {
                        missing++;
                        missingDetail.Add(scene + ":" + m.Groups[1].Value);
                    }
                }
            }

            Record(scripts > 0 && missing == 0, "G15 no missing script in the three game scenes (" + scripts + " script references)"
                + (missing > 0 ? ": " + string.Join(", ", missingDetail.ToArray()) : ""), report, ref passed, ref failed);

            string lobby = File.ReadAllText(Path.Combine(scenes, "Mav_MainLobby.unity"));
            string hangar = File.ReadAllText(Path.Combine(scenes, "Mav_Hangar.unity"));
            Record(Count(lobby, "--- !u!20 ") == 0 && Count(hangar, "--- !u!20 ") == 0,
                "G15 Lobby and Hangar save no camera (each bootstrap creates exactly one when none exists)", report, ref passed, ref failed);

            MavSceneWiringScanResult wiring = MavAircraftSceneWiringScan.Scan(Application.dataPath);
            Record(wiring.sourcesAvailable && wiring.violations.Count == 0,
                "G15 the official scene-wiring scan is still clean (" + wiring.scenesScanned + " scenes, " + wiring.violations.Count + " violations)",
                report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- [G16] [G17]

        private static void WriterScan(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[G16] Gameplay writes no physics");
            List<string> sites = new List<string>();
            int files = 0;
            foreach (string file in RuntimeGameplaySources())
            {
                files++;
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                    if (MavPhase5WriterScan.IsLiveWriteSite(lines[i])) sites.Add(Path.GetFileName(file) + ":" + (i + 1));
            }

            Record(files > 0 && sites.Count == 0, "G16 no Rigidbody write site in " + files + " gameplay runtime sources (official writer-scan rule)"
                + (sites.Count > 0 ? ": " + string.Join(", ", sites.ToArray()) : ""), report, ref passed, ref failed);
        }

        private static void Wording(StringBuilder report, ref int passed, ref int failed)
        {
            report.AppendLine();
            report.AppendLine("[G17] Player-facing text");
            string[] bad = { "prototype", "primary aircraft", "primary jet", "structurally_prepared", "reference stack", "research owner", "f-15ex", "nasa 836", "sandbox" };
            List<string> texts = new List<string>();
            foreach (MavPlayableAircraftDefinition d in MavPlayableAircraftRegistry.PlayerFacing())
            {
                texts.Add(d.displayName); texts.Add(d.role); texts.Add(d.description); texts.Add(d.statusText); texts.Add(d.flightModelLabel); texts.Add(d.controlHint);
                texts.AddRange(d.blockers);
                texts.AddRange(d.facts);
            }

            string scripts = Path.Combine(Application.dataPath, "MaverickFresh/Scripts");
            foreach (string rel in new[] { "Hangar/MavHangarBootstrap.cs", "UI/MavMainLobbyBootstrap.cs", "Gameplay/MavPlayerFlightHud.cs", "Gameplay/MavFlightSessionUi.cs" })
                foreach (Match m in Regex.Matches(File.ReadAllText(Path.Combine(scripts, rel)), "\"((?:[^\"\\\\]|\\\\.)*)\""))
                    texts.Add(m.Groups[1].Value);
            string lobbyScene = File.ReadAllText(Path.Combine(Application.dataPath, "MaverickFresh/Scenes/Mav_MainLobby.unity"));
            Match sub = Regex.Match(lobbyScene, "subtitle: (.*)");
            if (sub.Success) texts.Add(sub.Groups[1].Value);

            List<string> hits = new List<string>();
            foreach (string t in texts)
            {
                string low = (t ?? "").ToLowerInvariant();
                foreach (string b in bad)
                    if (low.Contains(b)) hits.Add("'" + t + "'");
            }

            Record(hits.Count == 0, "G17 " + texts.Count + " player-facing strings carry no prototype, wrong-variant or harness wording"
                + (hits.Count > 0 ? ": " + string.Join(", ", hits.ToArray()) : ""), report, ref passed, ref failed);
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>Gameplay runtime sources: the Gameplay folder without its Editor and Validation subfolders.</summary>
        private static List<string> RuntimeGameplaySources()
        {
            List<string> list = new List<string>();
            string root = Path.Combine(Application.dataPath, GameplayRelative);
            if (!Directory.Exists(root))
                return list;
            foreach (string f in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string n = f.Replace('\\', '/');
                if (n.Contains("/Editor/") || n.Contains("/Validation/"))
                    continue;
                list.Add(f);
            }

            return list;
        }

        /// <summary>The line up to a // comment (a /// doc line becomes empty). String literals are kept.</summary>
        private static string CodeWithoutComment(string line)
        {
            bool inString = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"' && (i == 0 || line[i - 1] != '\\'))
                    inString = !inString;
                if (!inString && c == '/' && i + 1 < line.Length && line[i + 1] == '/')
                    return line.Substring(0, i);
            }

            return line;
        }

        private static string Names(List<MavPlayableAircraftDefinition> list)
        {
            List<string> n = new List<string>();
            foreach (MavPlayableAircraftDefinition d in list) n.Add(d.displayName);
            return string.Join(", ", n.ToArray());
        }

        private static int Count(string text, string needle)
        {
            int n = 0, at = 0;
            while ((at = text.IndexOf(needle, at, StringComparison.Ordinal)) >= 0) { n++; at += needle.Length; }
            return n;
        }

        private static void RestorePref(string key, int value)
        {
            if (value < 0) PlayerPrefs.DeleteKey(key);
            else PlayerPrefs.SetInt(key, value);
        }

        private static void Record(bool condition, string label, StringBuilder report, ref int passed, ref int failed)
        {
            if (condition) passed++;
            else failed++;
            report.Append(condition ? "  PASS  " : "  FAIL  ").AppendLine(label);
        }
    }
}
#endif
