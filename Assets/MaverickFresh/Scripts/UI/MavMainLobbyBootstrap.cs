using UnityEngine;
using MaverickFresh.Gameplay;

namespace MaverickFresh
{
    /// <summary>
    /// Main Lobby: title, HANGAR (choose an aircraft), QUICK FLIGHT (straight into the F-15), QUIT.
    /// The quick flight goes through the same launch contract as the hangar's FLY button.
    /// </summary>
    public class MavMainLobbyBootstrap : MonoBehaviour
    {
        public string title = "MAVERICK";
        public string subtitle = "Flight simulator";
        public bool buildEnvironment = true;
        public bool lockCursor = false;

        private float spin;
        private GameObject showcase;
        private string message = "";

        private void Start()
        {
            Time.timeScale = 1f;
            if (buildEnvironment)
                BuildEnvironment();

            Cursor.lockState = lockCursor ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Update()
        {
            spin += Time.deltaTime * 14f;
            if (showcase != null)
                showcase.transform.rotation = Quaternion.Euler(0f, spin, 0f);
        }

        private void OnGUI()
        {
            MavGameplayUiStyle.Ensure();
            float x = 64f, y = 70f, w = 420f;

            GUI.Label(new Rect(x, y, w, 60f), title, MavGameplayUiStyle.Title);
            GUI.Label(new Rect(x + 2f, y + 58f, w, 26f), subtitle.ToUpperInvariant(),
                MavGameplayUiStyle.Tinted(MavGameplayUiStyle.Small, MavGameplayUiStyle.Accent));

            if (GUI.Button(new Rect(x, y + 120f, 320f, 54f), "HANGAR", MavGameplayUiStyle.BigButton))
                MavSceneLoader.LoadSceneSafe(MavSceneNames.Hangar);

            if (GUI.Button(new Rect(x, y + 186f, 320f, 46f), "QUICK FLIGHT - " + MavPlayableAircraftRegistry.F15DisplayName, MavGameplayUiStyle.Button))
            {
                string reason;
                if (!MavFlightLauncher.TryLaunch(MavPlayableAircraftRegistry.DefaultPlayerAircraft, out reason))
                    message = reason;
            }

            if (GUI.Button(new Rect(x, y + 244f, 320f, 42f), "QUIT", MavGameplayUiStyle.Button))
                Application.Quit();

            string error = !string.IsNullOrEmpty(message) ? message : MavGameSession.LastSceneError;
            if (!string.IsNullOrEmpty(error))
                GUI.Label(new Rect(x, Screen.height - 80f, Screen.width - 120f, 60f), error,
                    MavGameplayUiStyle.Tinted(MavGameplayUiStyle.Small, MavGameplayUiStyle.Danger));
        }

        private void BuildEnvironment()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                GameObject camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }

            cam.transform.position = new Vector3(0f, 3.2f, -10f);
            cam.transform.rotation = Quaternion.Euler(13f, 0f, 0f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.06f, 0.08f);

            if (GameObject.Find("Mav_Lobby_KeyLight") == null)
            {
                GameObject lightGo = new GameObject("Mav_Lobby_KeyLight");
                Light l = lightGo.AddComponent<Light>();
                l.type = LightType.Directional;
                l.intensity = 1.2f;
                lightGo.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            }

            showcase = GameObject.Find("Mav_Lobby_RotatingAircraft");
            if (showcase == null)
            {
                showcase = new GameObject("Mav_Lobby_RotatingAircraft");
                showcase.transform.position = new Vector3(2.9f, 1.4f, 0.8f);
                showcase.transform.localScale = Vector3.one * 0.26f;
                MavGameplayVisuals.BuildAircraftModel(MavPlayableAircraftRegistry.DefaultPlayerAircraft, showcase.transform);
            }
        }
    }
}
