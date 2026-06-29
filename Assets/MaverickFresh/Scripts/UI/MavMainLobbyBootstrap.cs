using UnityEngine;

namespace MaverickFresh
{
    public class MavMainLobbyBootstrap : MonoBehaviour
    {
        public string title = "MAVERICK";
        public string subtitle = "F-22 Primary Jet Combat Sandbox Prototype";
        public bool buildEnvironment = true;
        public bool lockCursor = false;

        private Camera cam;
        private GUIStyle titleStyle;
        private GUIStyle normalStyle;
        private GUIStyle buttonStyle;
        private float spin;

        private void Start()
        {
            if (buildEnvironment)
                BuildEnvironment();

            Cursor.lockState = lockCursor ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Update()
        {
            spin += Time.deltaTime * 18f;
            GameObject logo = GameObject.Find("Mav_Lobby_RotatingAircraft");
            if (logo != null)
                logo.transform.rotation = Quaternion.Euler(0f, spin, 0f);
        }

        private void OnGUI()
        {
            EnsureStyles();

            float w = Mathf.Min(520f, Screen.width * 0.42f);
            float x = 56f;
            float y = 60f;

            GUI.Label(new Rect(x, y, w, 54f), title, titleStyle);
            GUI.Label(new Rect(x, y + 58f, w, 32f), subtitle, normalStyle);
            GUI.Label(new Rect(x, y + 100f, w, 72f), "F-22A is the primary aircraft. Main Lobby -> 3D Hangar -> In-Game Flight", normalStyle);

            if (GUI.Button(new Rect(x, y + 190f, 280f, 46f), "ENTER HANGAR", buttonStyle))
                MavSceneLoader.LoadSceneSafe(MavSceneNames.Hangar);

            if (GUI.Button(new Rect(x, y + 248f, 280f, 42f), "QUICK F-22 FREE FLIGHT", buttonStyle))
                MavGameSession.Launch(MavAircraftKind.F22A, MavGameMode.FreeFlight);

            if (GUI.Button(new Rect(x, y + 300f, 280f, 42f), "QUICK F-16 DOGFIGHT", buttonStyle))
                MavGameSession.Launch(MavAircraftKind.F16C, MavGameMode.Dogfight);

            if (!string.IsNullOrEmpty(MavGameSession.LastSceneError))
                GUI.Label(new Rect(x, Screen.height - 80f, Screen.width - 120f, 60f), MavGameSession.LastSceneError, normalStyle);
        }

        private void BuildEnvironment()
        {
            if (Camera.main == null)
            {
                GameObject camGo = new GameObject("Main Camera");
                cam = camGo.AddComponent<Camera>();
                cam.tag = "MainCamera";
                cam.transform.position = new Vector3(0f, 3.2f, -10f);
                cam.transform.rotation = Quaternion.Euler(13f, 0f, 0f);
                cam.clearFlags = CameraClearFlags.Skybox;
            }
            else
            {
                cam = Camera.main;
            }

            if (GameObject.Find("Mav_Lobby_KeyLight") == null)
            {
                GameObject lightGo = new GameObject("Mav_Lobby_KeyLight");
                Light l = lightGo.AddComponent<Light>();
                l.type = LightType.Directional;
                l.intensity = 1.1f;
                lightGo.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            }

            if (GameObject.Find("Mav_Lobby_RotatingAircraft") == null)
            {
                MavAircraftRuntimeProfile p = MavAircraftCatalog.GetBuiltIn(MavAircraftKind.F22A);
                GameObject logo = new GameObject("Mav_Lobby_RotatingAircraft");
                logo.transform.position = new Vector3(2.7f, 1.7f, 0.7f);
                logo.transform.localScale = Vector3.one * 0.65f;
                MavAircraftVisualFactory.CreateDisplayVisual(p, logo.transform, null, true);
            }
        }

        private void EnsureStyles()
        {
            if (titleStyle != null)
                return;

            titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontSize = 44;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.normal.textColor = Color.white;

            normalStyle = new GUIStyle(GUI.skin.label);
            normalStyle.fontSize = 17;
            normalStyle.normal.textColor = new Color(0.88f, 0.92f, 0.96f);
            normalStyle.wordWrap = true;

            buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fontSize = 18;
            buttonStyle.fontStyle = FontStyle.Bold;
        }
    }
}
