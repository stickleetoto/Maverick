using System.Collections.Generic;
using UnityEngine;
using MaverickFresh.Gameplay;

namespace MaverickFresh
{
    /// <summary>
    /// The hangar: pick an aircraft, see what it is and whether it can fly, press FLY.
    ///
    /// Player-facing aircraft come from <see cref="MavPlayableAircraftRegistry"/> (R1: F-15 and F-16), shown as
    /// clickable cards. Clicking a card selects it in <see cref="MavGameSession"/>, updates the model and the
    /// details, and points FLY at that aircraft's launch path. FLY is enabled only for a launchable aircraft;
    /// otherwise the card says what blocks it. Nothing is ever substituted.
    ///
    /// Unfinished aircraft are not in the player list. They sit behind the DEVELOPMENT toggle, labelled as legacy
    /// flight model, with a separate development launch. Keyboard: Left/Right switch cards, Enter FLY, Esc lobby.
    /// </summary>
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
        public bool autoFindSceneAircraftVisuals = false;

        [Header("Hangar")]
        public bool buildOnStart = true;
        [Tooltip("Unused since R1 (one aircraft is shown at a time); kept for scene compatibility.")]
        public float slideSpacing = 10f;
        public float slideSmooth = 7f;
        public Vector3 aircraftRootPosition = new Vector3(0f, 1.15f, 0f);
        public Vector3 cameraPosition = new Vector3(0f, 3.2f, -12f);
        public Vector3 cameraLookAt = new Vector3(0f, 1.2f, 0f);

        [Header("Presentation")]
        [Tooltip("One scale for every aircraft, so their relative sizes are right.")]
        public float displayScale = 0.33f;
        public float turntableDegreesPerSecond = 12f;

        [Header("Runtime (read-only)")]
        public string lastLaunchMessage = "";

        private readonly List<MavPlayableAircraftDefinition> playerAircraft = new List<MavPlayableAircraftDefinition>();
        private readonly List<MavPlayableAircraftDefinition> developmentAircraft = new List<MavPlayableAircraftDefinition>();
        private int selectedIndex;
        private bool showDevelopment;
        private GameObject displayRoot;
        private GameObject displayModel;
        private float displayYaw = 200f;
        private float transition = 1f;
        private Camera cam;

        public MavPlayableAircraftDefinition Selected
        {
            get { return playerAircraft.Count > 0 ? playerAircraft[Mathf.Clamp(selectedIndex, 0, playerAircraft.Count - 1)] : null; }
        }

        public IList<MavPlayableAircraftDefinition> PlayerAircraft
        {
            get { return playerAircraft; }
        }

        private void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 1f;
            if (buildOnStart)
                BuildHangar();
        }

        [ContextMenu("Build Hangar")]
        public void BuildHangar()
        {
            playerAircraft.Clear();
            playerAircraft.AddRange(MavPlayableAircraftRegistry.PlayerFacing());
            developmentAircraft.Clear();
            developmentAircraft.AddRange(MavPlayableAircraftRegistry.Development());

            // Show the session's selection when it is a player aircraft. Otherwise the F-15 card is highlighted, but
            // the session is NOT rewritten: only the player's own click or FLY changes the selection.
            int index = MavGameSession.HasSelection ? IndexOf(MavGameSession.SelectedAircraft) : -1;
            if (index < 0)
                index = Mathf.Max(0, IndexOf(MavPlayableAircraftRegistry.DefaultPlayerAircraft));
            selectedIndex = index;

            EnsureEnvironment();
            ShowSelectedModel(true);
        }

        /// <summary>A click on a card: select it, record it, show it. FLY now points at this aircraft.</summary>
        public void SelectCard(MavAircraftKind aircraft)
        {
            int index = IndexOf(aircraft);
            if (index < 0)
                return;
            bool changed = index != selectedIndex;
            selectedIndex = index;
            MavGameSession.SelectAircraft(aircraft);
            lastLaunchMessage = "";
            if (changed || displayModel == null)
                ShowSelectedModel(false);
        }

        public bool CanFly(out string reason)
        {
            MavPlayableAircraftDefinition d = Selected;
            if (d == null)
            {
                reason = "No aircraft selected";
                return false;
            }

            MavPlayableAircraftDefinition checkedDefinition;
            return MavPlayableAircraftRegistry.CanLaunchAsPlayer(d.aircraft, out checkedDefinition, out reason);
        }

        /// <summary>FLY: launch the selected aircraft, or refuse with the reason and change nothing else.</summary>
        public bool Fly()
        {
            MavPlayableAircraftDefinition d = Selected;
            if (d == null)
                return false;

            string reason;
            bool ok = MavFlightLauncher.TryLaunch(d.aircraft, out reason);
            lastLaunchMessage = ok ? "" : reason;
            return ok;
        }

        private void Update()
        {
            if (MavFreshInput.GetKeyDown(KeyCode.LeftArrow) || MavFreshInput.GetKeyDown(KeyCode.A))
                Step(-1);
            if (MavFreshInput.GetKeyDown(KeyCode.RightArrow) || MavFreshInput.GetKeyDown(KeyCode.D))
                Step(1);
            if (MavFreshInput.GetKeyDown(KeyCode.Return))
            {
                string reason;
                if (CanFly(out reason))
                    Fly();
                else
                    lastLaunchMessage = reason;
            }

            if (MavFreshInput.GetKeyDown(KeyCode.Escape))
                MavSceneLoader.LoadSceneSafe(MavSceneNames.MainLobby);

            if (displayRoot != null)
            {
                displayYaw += turntableDegreesPerSecond * Time.deltaTime;
                transition = Mathf.MoveTowards(transition, 1f, Time.deltaTime * 2.5f);
                float eased = 1f - (1f - transition) * (1f - transition);
                displayRoot.transform.position = aircraftRootPosition + new Vector3(0f, 0.6f * (1f - eased), 0f);
                displayRoot.transform.rotation = Quaternion.Euler(0f, displayYaw, 0f);
                displayRoot.transform.localScale = Vector3.one * (displayScale * Mathf.Lerp(0.85f, 1f, eased));
            }
        }

        private void Step(int delta)
        {
            if (playerAircraft.Count == 0)
                return;
            int next = (selectedIndex + delta + playerAircraft.Count) % playerAircraft.Count;
            SelectCard(playerAircraft[next].aircraft);
        }

        private int IndexOf(MavAircraftKind aircraft)
        {
            for (int i = 0; i < playerAircraft.Count; i++)
            {
                if (playerAircraft[i].aircraft == aircraft)
                    return i;
            }

            return -1;
        }

        // ------------------------------------------------------------------ presentation

        private void OnGUI()
        {
            MavGameplayUiStyle.Ensure();
            float w = Screen.width, h = Screen.height;

            GUI.Label(new Rect(40f, 26f, 700f, 50f), "SELECT AIRCRAFT", MavGameplayUiStyle.Title);
            GUI.Label(new Rect(44f, 76f, 700f, 24f), "FREE FLIGHT", MavGameplayUiStyle.Tinted(MavGameplayUiStyle.Small, MavGameplayUiStyle.Accent));

            MavPlayableAircraftDefinition d = Selected;
            if (d == null)
            {
                GUI.Label(new Rect(44f, 110f, 700f, 30f), "No aircraft is available.", MavGameplayUiStyle.Body);
                return;
            }

            DrawCards(w, h);
            DrawDetails(d, w, h);

            if (GUI.Button(new Rect(40f, h - 64f, 150f, 42f), "BACK", MavGameplayUiStyle.Button))
                MavSceneLoader.LoadSceneSafe(MavSceneNames.MainLobby);

            string devLabel = showDevelopment ? "HIDE DEVELOPMENT" : "DEVELOPMENT";
            if (GUI.Button(new Rect(200f, h - 58f, 170f, 32f), devLabel, MavGameplayUiStyle.Button))
                showDevelopment = !showDevelopment;
            if (showDevelopment)
                DrawDevelopment(h);

            if (!string.IsNullOrEmpty(MavGameSession.LastSceneError))
                GUI.Label(new Rect(390f, h - 58f, w - 800f, 40f), MavGameSession.LastSceneError,
                    MavGameplayUiStyle.Tinted(MavGameplayUiStyle.Small, MavGameplayUiStyle.Danger));
        }

        private void DrawCards(float w, float h)
        {
            const float cardW = 300f, cardH = 92f, gap = 20f;
            float total = playerAircraft.Count * cardW + (playerAircraft.Count - 1) * gap;
            float x = Mathf.Max(40f, (w - 480f - total) * 0.5f);
            float y = h - cardH - 90f;
            for (int i = 0; i < playerAircraft.Count; i++)
            {
                MavPlayableAircraftDefinition card = playerAircraft[i];
                bool selected = i == selectedIndex;
                Rect r = new Rect(x + i * (cardW + gap), y, cardW, cardH);
                string label = card.displayName + "\n" + card.statusText;
                if (GUI.Button(r, label, selected ? MavGameplayUiStyle.CardSelected : MavGameplayUiStyle.Card))
                    SelectCard(card.aircraft);
                if (selected)
                    MavGameplayUiStyle.Fill(new Rect(r.x, r.y + r.height - 4f, r.width, 4f), MavGameplayUiStyle.Accent);
            }
        }

        private void DrawDetails(MavPlayableAircraftDefinition d, float w, float h)
        {
            float panelW = 440f;
            Rect p = new Rect(w - panelW - 32f, 30f, panelW, h - 60f);
            MavGameplayUiStyle.Panel(p);
            float x = p.x + 22f, y = p.y + 18f, cw = panelW - 44f;

            GUI.Label(new Rect(x, y, cw, 34f), d.displayName, MavGameplayUiStyle.Heading);
            y += 34f;
            GUI.Label(new Rect(x, y, cw, 22f), d.role, MavGameplayUiStyle.Small);
            y += 30f;

            bool ready = d.IsLaunchable;
            Color badge = ready ? MavGameplayUiStyle.Accent : MavGameplayUiStyle.Warning;
            MavGameplayUiStyle.Fill(new Rect(x, y + 2f, 10f, 20f), badge);
            GUI.Label(new Rect(x + 18f, y, cw, 24f), d.statusText + "   " + d.flightModelLabel,
                MavGameplayUiStyle.Tinted(MavGameplayUiStyle.Body, badge));
            y += 34f;

            GUI.Label(new Rect(x, y, cw, 64f), d.description, MavGameplayUiStyle.Body);
            y += 72f;

            for (int i = 0; i < d.facts.Length; i++)
            {
                string[] kv = d.facts[i].Split('|');
                GUI.Label(new Rect(x, y, 150f, 22f), kv[0].ToUpperInvariant(), MavGameplayUiStyle.Small);
                GUI.Label(new Rect(x + 150f, y - 2f, cw - 150f, 22f), kv.Length > 1 ? kv[1] : "", MavGameplayUiStyle.Body);
                y += 26f;
            }

            if (d.blockers.Length > 0)
            {
                y += 8f;
                GUI.Label(new Rect(x, y, cw, 22f), "WHY IT CANNOT FLY YET", MavGameplayUiStyle.Tinted(MavGameplayUiStyle.Small, MavGameplayUiStyle.Warning));
                y += 24f;
                for (int i = 0; i < d.blockers.Length; i++)
                {
                    GUI.Label(new Rect(x, y, cw, 22f), "-  " + d.blockers[i], MavGameplayUiStyle.Body);
                    y += 24f;
                }
            }

            y += 10f;
            GUI.Label(new Rect(x, y, cw, 40f), "CONTROLS   " + d.controlHint, MavGameplayUiStyle.Small);

            string reason;
            bool canFly = CanFly(out reason);
            GUI.enabled = canFly;
            if (GUI.Button(new Rect(x, p.yMax - 96f, cw, 60f), canFly ? "FLY" : "NOT AVAILABLE", MavGameplayUiStyle.BigButton))
                Fly();
            GUI.enabled = true;
            string note = !string.IsNullOrEmpty(lastLaunchMessage) ? lastLaunchMessage : (canFly ? "Enter" : "");
            GUI.Label(new Rect(x, p.yMax - 32f, cw, 24f), note, MavGameplayUiStyle.Small);
        }

        private void DrawDevelopment(float h)
        {
            Rect p = new Rect(40f, 120f, 420f, 70f + developmentAircraft.Count * 44f);
            MavGameplayUiStyle.Panel(p, true);
            GUI.Label(new Rect(p.x + 16f, p.y + 10f, p.width - 32f, 40f),
                "DEVELOPMENT - legacy flight model, not validated, not part of the player release",
                MavGameplayUiStyle.Tinted(MavGameplayUiStyle.Small, MavGameplayUiStyle.Warning));
            for (int i = 0; i < developmentAircraft.Count; i++)
            {
                MavPlayableAircraftDefinition dev = developmentAircraft[i];
                float y = p.y + 56f + i * 44f;
                GUI.Label(new Rect(p.x + 16f, y + 6f, 250f, 24f), dev.displayName, MavGameplayUiStyle.Small);
                if (GUI.Button(new Rect(p.x + p.width - 150f, y, 134f, 34f), "DEV LAUNCH", MavGameplayUiStyle.Button))
                {
                    string reason;
                    if (!MavFlightLauncher.TryDevelopmentLaunch(dev.aircraft, out reason))
                        lastLaunchMessage = reason;
                }
            }
        }

        // ------------------------------------------------------------------ 3D

        private void ShowSelectedModel(bool instant)
        {
            if (displayRoot == null)
                displayRoot = new GameObject("Mav_Hangar_AircraftDisplay");
            if (displayModel != null)
                Destroy(displayModel);

            MavPlayableAircraftDefinition d = Selected;
            if (d == null)
                return;

            GameObject manual = GetPrefab(d.aircraft);
            if (manual != null)
            {
                displayModel = Instantiate(manual, displayRoot.transform);
                displayModel.transform.localPosition = Vector3.zero;
                displayModel.transform.localRotation = Quaternion.identity;
                MavGameplayVisuals.RemoveColliders(displayModel);
            }
            else
            {
                displayModel = MavGameplayVisuals.BuildAircraftModel(d.aircraft, displayRoot.transform);
            }

            transition = instant ? 1f : 0f;
        }

        private void EnsureEnvironment()
        {
            cam = Camera.main;
            if (cam == null)
            {
                GameObject camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }

            // Framed on the aircraft, which sits a little left of centre: the details panel covers the right side.
            cam.transform.position = cameraPosition + new Vector3(3.2f, 0.6f, 1.5f);
            cam.transform.LookAt(cameraLookAt + new Vector3(2.6f, 0.2f, 0f));
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.07f, 0.09f);
            cam.fieldOfView = 42f;

            if (GameObject.Find("Mav_Hangar_KeyLight") == null)
            {
                GameObject lightGo = new GameObject("Mav_Hangar_KeyLight");
                Light l = lightGo.AddComponent<Light>();
                l.type = LightType.Directional;
                l.intensity = 1.25f;
                l.shadows = LightShadows.Soft;
                lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            if (GameObject.Find("Mav_Hangar_FillLight") == null)
            {
                GameObject lightGo = new GameObject("Mav_Hangar_FillLight");
                Light l = lightGo.AddComponent<Light>();
                l.type = LightType.Point;
                l.intensity = 4.0f;
                l.range = 30f;
                l.color = new Color(0.75f, 0.85f, 1f);
                lightGo.transform.position = new Vector3(-4f, 5.5f, -6f);
            }

            if (GameObject.Find("Mav_Hangar_Platform") == null)
                MavGameplayVisuals.Primitive(PrimitiveType.Cylinder, "Mav_Hangar_Platform", null, new Vector3(0f, 0.05f, 0f),
                    new Vector3(8.5f, 0.1f, 8.5f), MavGameplayVisuals.NewMaterial(new Color(0.20f, 0.22f, 0.25f), 0.6f));

            if (GameObject.Find("Mav_Hangar_Floor") == null)
                MavGameplayVisuals.Primitive(PrimitiveType.Cube, "Mav_Hangar_Floor", null, new Vector3(0f, -0.12f, 0f),
                    new Vector3(60f, 0.1f, 60f), MavGameplayVisuals.NewMaterial(new Color(0.11f, 0.12f, 0.14f), 0.25f));

            if (GameObject.Find("Mav_Hangar_Backdrop") == null)
                MavGameplayVisuals.Primitive(PrimitiveType.Cube, "Mav_Hangar_Backdrop", null, new Vector3(0f, 8f, 14f),
                    new Vector3(80f, 20f, 0.5f), MavGameplayVisuals.NewMaterial(new Color(0.09f, 0.10f, 0.12f), 0.1f));
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
    }
}
