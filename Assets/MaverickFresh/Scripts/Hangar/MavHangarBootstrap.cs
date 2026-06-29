using System.Collections.Generic;
using UnityEngine;

namespace MaverickFresh
{
    public class MavHangarBootstrap : MonoBehaviour
    {
        [Header("Manual aircraft visual slots (optional)")]
        public GameObject f15exVisual;
        public GameObject f16Visual;
        public GameObject f18Visual;
        public GameObject f22Visual;
        public GameObject f35Visual;

        [Header("Legacy slot aliases (kept for old inspector assignments)")]
        public GameObject f15Prefab;
        public GameObject f16Prefab;
        public GameObject fa18Prefab;
        public GameObject f22Prefab;
        public GameObject f35Prefab;

        [Header("Scene Visual Auto Resolve")]
        public bool autoFindSceneAircraftVisuals = true;

        [Header("Hangar")]
        public bool buildOnStart = true;
        public float slideSpacing = 10f;
        public float slideSmooth = 7f;
        public Vector3 aircraftRootPosition = new Vector3(0f, 1.15f, 0f);
        public Vector3 cameraPosition = new Vector3(0f, 3.2f, -12f);
        public Vector3 cameraLookAt = new Vector3(0f, 1.2f, 0f);

        private readonly List<MavAircraftRuntimeProfile> profiles = new List<MavAircraftRuntimeProfile>();
        private readonly List<GameObject> displayRoots = new List<GameObject>();
        private int selectedIndex = 0;
        private Camera cam;
        private GUIStyle titleStyle;
        private GUIStyle normalStyle;
        private GUIStyle buttonStyle;
        private GUIStyle statStyle;
        private bool modePanelOpen;

        private void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (buildOnStart)
                BuildHangar();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Previous();
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Next();
            if (Input.GetKeyDown(KeyCode.Return)) Launch(MavGameMode.FreeFlight);
            if (Input.GetKeyDown(KeyCode.Escape)) MavSceneLoader.LoadSceneSafe(MavSceneNames.MainLobby);

            for (int i = 0; i < displayRoots.Count; i++)
            {
                if (displayRoots[i] == null) continue;
                float offset = i - selectedIndex;
                Vector3 target = aircraftRootPosition + new Vector3(offset * slideSpacing, 0f, Mathf.Abs(offset) * 1.2f);
                displayRoots[i].transform.position = Vector3.Lerp(displayRoots[i].transform.position, target, Time.deltaTime * slideSmooth);
                float targetScale = Mathf.Abs(offset) < 0.1f ? 1f : 0.72f;
                displayRoots[i].transform.localScale = Vector3.Lerp(displayRoots[i].transform.localScale, Vector3.one * targetScale, Time.deltaTime * slideSmooth);
            }
        }

        [ContextMenu("Build Hangar")]
        public void BuildHangar()
        {
            profiles.Clear();
            profiles.AddRange(MavAircraftCatalog.CreateBuiltInProfiles());
            selectedIndex = FindProfileIndex(MavGameSession.HasSelection ? MavGameSession.SelectedAircraft : MavGameSession.DefaultAircraft);
            selectedIndex = Mathf.Clamp(selectedIndex, 0, profiles.Count - 1);

            if (autoFindSceneAircraftVisuals)
                AutoResolveSceneVisuals();

            EnsureEnvironment();
            BuildAircraftDisplays();
            MavGameSession.SelectAircraft(CurrentProfile().aircraft);
        }

        public void Next()
        {
            if (profiles.Count == 0) return;
            selectedIndex = (selectedIndex + 1) % profiles.Count;
            MavGameSession.SelectAircraft(CurrentProfile().aircraft);
        }

        public void Previous()
        {
            if (profiles.Count == 0) return;
            selectedIndex--;
            if (selectedIndex < 0) selectedIndex = profiles.Count - 1;
            MavGameSession.SelectAircraft(CurrentProfile().aircraft);
        }

        private void Launch(MavGameMode mode)
        {
            MavAircraftRuntimeProfile p = CurrentProfile();
            MavGameSession.Launch(p.aircraft, mode);
        }

        private int FindProfileIndex(MavAircraftKind aircraft)
        {
            for (int i = 0; i < profiles.Count; i++)
            {
                if (profiles[i] != null && profiles[i].aircraft == aircraft)
                    return i;
            }
            for (int i = 0; i < profiles.Count; i++)
            {
                if (profiles[i] != null && profiles[i].aircraft == MavAircraftKind.F22A)
                    return i;
            }
            return 0;
        }

        private MavAircraftRuntimeProfile CurrentProfile()
        {
            if (profiles.Count == 0)
                return MavAircraftCatalog.GetBuiltIn(MavAircraftKind.F22A);
            return profiles[Mathf.Clamp(selectedIndex, 0, profiles.Count - 1)];
        }

        private void OnGUI()
        {
            EnsureStyles();
            MavAircraftRuntimeProfile p = CurrentProfile();

            GUI.Label(new Rect(42f, 32f, 600f, 48f), "HANGAR", titleStyle);
            GUI.Label(new Rect(44f, 84f, 760f, 28f), "F-22A is the primary aircraft. A/D or Arrow Keys: switch aircraft   Enter: test flight   Esc: lobby", normalStyle);

            float panelW = 430f;
            float x = Screen.width - panelW - 42f;
            float y = 62f;
            GUI.Box(new Rect(x - 18f, y - 18f, panelW + 36f, 510f), "");
            GUI.Label(new Rect(x, y, panelW, 36f), p.displayName, titleStyle);
            GUI.Label(new Rect(x, y + 48f, panelW, 26f), p.role, normalStyle);
            GUI.Label(new Rect(x, y + 86f, panelW, 70f), p.description, normalStyle);

            DrawStat(x, y + 168f, "SPEED", p.statSpeed);
            DrawStat(x, y + 204f, "TURN", p.statTurn);
            DrawStat(x, y + 240f, "STABILITY", p.statStability);
            DrawStat(x, y + 276f, "PAYLOAD", p.statPayload);
            DrawStat(x, y + 312f, "DIFFICULTY", p.statDifficulty);

            if (GUI.Button(new Rect(x, y + 365f, 190f, 42f), "TEST FLIGHT", buttonStyle))
                Launch(MavGameMode.FreeFlight);

            if (GUI.Button(new Rect(x + 205f, y + 365f, 190f, 42f), "MODE SELECT", buttonStyle))
                modePanelOpen = !modePanelOpen;

            if (GUI.Button(new Rect(x, y + 420f, 190f, 38f), "< PREV", buttonStyle))
                Previous();

            if (GUI.Button(new Rect(x + 205f, y + 420f, 190f, 38f), "NEXT >", buttonStyle))
                Next();

            if (modePanelOpen)
                DrawModePanel(x - 260f, y + 365f);

            if (!string.IsNullOrEmpty(MavGameSession.LastSceneError))
                GUI.Label(new Rect(40f, Screen.height - 70f, Screen.width - 80f, 50f), MavGameSession.LastSceneError, normalStyle);
        }

        private void DrawModePanel(float x, float y)
        {
            GUI.Box(new Rect(x, y - 12f, 240f, 252f), "");
            GUI.Label(new Rect(x + 14f, y, 210f, 26f), "SELECT MODE", normalStyle);
            if (GUI.Button(new Rect(x + 14f, y + 34f, 210f, 34f), "FREE FLIGHT", buttonStyle)) Launch(MavGameMode.FreeFlight);
            if (GUI.Button(new Rect(x + 14f, y + 74f, 210f, 34f), "TEST RANGE", buttonStyle)) Launch(MavGameMode.TestRange);
            if (GUI.Button(new Rect(x + 14f, y + 114f, 210f, 34f), "DOGFIGHT", buttonStyle)) Launch(MavGameMode.Dogfight);
            if (GUI.Button(new Rect(x + 14f, y + 154f, 210f, 34f), "GROUND ATTACK", buttonStyle)) Launch(MavGameMode.GroundAttack);
            if (GUI.Button(new Rect(x + 14f, y + 194f, 210f, 34f), "CARRIER TEST", buttonStyle)) Launch(MavGameMode.CarrierTest);
        }

        private void DrawStat(float x, float y, string label, float value)
        {
            GUI.Label(new Rect(x, y, 110f, 24f), label, statStyle);
            GUI.Box(new Rect(x + 120f, y + 5f, 220f, 14f), "");
            GUI.Box(new Rect(x + 120f, y + 5f, Mathf.Clamp01(value / 10f) * 220f, 14f), "");
            GUI.Label(new Rect(x + 350f, y, 60f, 24f), value.ToString("0.0"), statStyle);
        }

        private void EnsureEnvironment()
        {
            if (Camera.main == null)
            {
                GameObject camGo = new GameObject("Main Camera");
                cam = camGo.AddComponent<Camera>();
                cam.tag = "MainCamera";
            }
            else cam = Camera.main;

            cam.transform.position = cameraPosition;
            cam.transform.LookAt(cameraLookAt);
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 48f;

            if (GameObject.Find("Mav_Hangar_KeyLight") == null)
            {
                GameObject lightGo = new GameObject("Mav_Hangar_KeyLight");
                Light l = lightGo.AddComponent<Light>();
                l.type = LightType.Directional;
                l.intensity = 1.2f;
                lightGo.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            }

            if (GameObject.Find("Mav_Hangar_FillLight") == null)
            {
                GameObject lightGo = new GameObject("Mav_Hangar_FillLight");
                Light l = lightGo.AddComponent<Light>();
                l.type = LightType.Point;
                l.intensity = 3.0f;
                l.range = 30f;
                lightGo.transform.position = new Vector3(-4f, 5.5f, -6f);
            }

            if (GameObject.Find("Mav_Hangar_Platform") == null)
            {
                GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                platform.name = "Mav_Hangar_Platform";
                platform.transform.position = new Vector3(0f, 0f, 0f);
                platform.transform.localScale = new Vector3(4.8f, 0.15f, 4.8f);
            }

            if (GameObject.Find("Mav_Hangar_Floor") == null)
            {
                GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "Mav_Hangar_Floor";
                floor.transform.position = new Vector3(0f, -0.12f, 0f);
                floor.transform.localScale = new Vector3(34f, 0.1f, 24f);
            }
        }

        private void BuildAircraftDisplays()
        {
            GameObject oldRoot = GameObject.Find("Mav_Hangar_AircraftDisplays");
            if (oldRoot != null)
                Destroy(oldRoot);

            GameObject root = new GameObject("Mav_Hangar_AircraftDisplays");
            displayRoots.Clear();

            for (int i = 0; i < profiles.Count; i++)
            {
                GameObject slot = new GameObject("HangarSlot_" + profiles[i].shortName);
                slot.transform.SetParent(root.transform, true);
                slot.transform.position = aircraftRootPosition + new Vector3((i - selectedIndex) * slideSpacing, 0f, 0f);
                slot.transform.localScale = Vector3.one * (i == selectedIndex ? 1f : 0.72f);
                MavAircraftVisualFactory.CreateDisplayVisual(profiles[i], slot.transform, GetPrefab(profiles[i].aircraft), true);
                displayRoots.Add(slot);
            }
        }

        private GameObject GetPrefab(MavAircraftKind kind)
        {
            if (kind == MavAircraftKind.F15E) return FirstNonNull(f15exVisual, f15Prefab);
            if (kind == MavAircraftKind.F16C) return FirstNonNull(f16Visual, f16Prefab);
            if (kind == MavAircraftKind.FA18E) return FirstNonNull(f18Visual, fa18Prefab);
            if (kind == MavAircraftKind.F22A) return FirstNonNull(f22Visual, f22Prefab);
            if (kind == MavAircraftKind.F35A) return FirstNonNull(f35Visual, f35Prefab);
            return null;
        }

        private static GameObject FirstNonNull(GameObject preferred, GameObject legacy)
        {
            return preferred != null ? preferred : legacy;
        }

        private void AutoResolveSceneVisuals()
        {
            if (f15exVisual == null && f15Prefab == null) f15exVisual = FindSceneVisual(MavAircraftKind.F15E);
            if (f16Visual == null && f16Prefab == null) f16Visual = FindSceneVisual(MavAircraftKind.F16C);
            if (f18Visual == null && fa18Prefab == null) f18Visual = FindSceneVisual(MavAircraftKind.FA18E);
            if (f22Visual == null && f22Prefab == null) f22Visual = FindSceneVisual(MavAircraftKind.F22A);
            if (f35Visual == null && f35Prefab == null) f35Visual = FindSceneVisual(MavAircraftKind.F35A);
        }

        private GameObject FindSceneVisual(MavAircraftKind kind)
        {
            string[] tokens = TokensFor(kind);
            Transform[] all = FindObjectsOfType<Transform>(true);
            Transform best = null;
            int bestScore = -1;

            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null) continue;
                GameObject go = t.gameObject;
                if (IsInsideGeneratedHangarDisplay(t)) continue;
                if (go.GetComponent<Camera>() != null || go.GetComponent<Light>() != null) continue;
                if (go.GetComponent<MavHangarBootstrap>() != null || go.GetComponent<MavInGameBootstrap>() != null) continue;
                if (go.name.StartsWith("Mav_") || go.name.StartsWith("MaverickFresh_")) continue;
                if (go.GetComponentInChildren<Renderer>(true) == null) continue;

                int score = ScoreName(t.name.ToLowerInvariant(), tokens);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = t;
                }
            }

            return best != null && bestScore > 0 ? best.gameObject : null;
        }


        private bool IsInsideGeneratedHangarDisplay(Transform t)
        {
            while (t != null)
            {
                if (t.name == "Mav_Hangar_AircraftDisplays" || t.name.StartsWith("HangarSlot_"))
                    return true;
                t = t.parent;
            }
            return false;
        }

        private static string[] TokensFor(MavAircraftKind kind)
        {
            switch (kind)
            {
                case MavAircraftKind.F15E: return new[] { "f15ex", "f-15ex", "f15_ex", "f15 ex", "f15", "f-15", "f15e" };
                case MavAircraftKind.F16C: return new[] { "f16", "f-16", "f16c", "f_16" };
                case MavAircraftKind.FA18E: return new[] { "f18", "f-18", "fa18", "fa-18", "f/a-18", "f_18" };
                case MavAircraftKind.F22A: return new[] { "f22", "f-22", "f22a", "f_22" };
                case MavAircraftKind.F35A: return new[] { "f35", "f-35", "f35a", "f_35" };
                default: return new string[0];
            }
        }

        private static int ScoreName(string lowerName, string[] tokens)
        {
            if (string.IsNullOrEmpty(lowerName) || tokens == null) return -1;
            int score = -1;
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (string.IsNullOrEmpty(token)) continue;
                if (lowerName == token) score = Mathf.Max(score, 100 + token.Length);
                else if (lowerName.Contains(token)) score = Mathf.Max(score, 10 + token.Length);
            }
            return score;
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontSize = 28;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.normal.textColor = Color.white;

            normalStyle = new GUIStyle(GUI.skin.label);
            normalStyle.fontSize = 16;
            normalStyle.normal.textColor = new Color(0.88f, 0.92f, 0.96f);
            normalStyle.wordWrap = true;

            statStyle = new GUIStyle(GUI.skin.label);
            statStyle.fontSize = 14;
            statStyle.fontStyle = FontStyle.Bold;
            statStyle.normal.textColor = Color.white;

            buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fontSize = 15;
            buttonStyle.fontStyle = FontStyle.Bold;
        }
    }
}
