using UnityEngine;

namespace MaverickFresh
{
    public class MavInGameMenuOverlay : MonoBehaviour
    {
        public MavAircraftRuntimeProfile profile;
        public MavGameMode mode;
        public bool showOverlay = true;
        private GUIStyle small;
        private GUIStyle button;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.H))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                MavSceneLoader.LoadSceneSafe(MavSceneNames.Hangar);
            }

            if (Input.GetKeyDown(KeyCode.F1))
                showOverlay = !showOverlay;
        }

        private void OnGUI()
        {
            if (!showOverlay)
                return;

            EnsureStyles();
            string aircraft = profile != null ? profile.shortName : MavGameSession.SelectedAircraft.ToString();
            GUI.Label(new Rect(14f, Screen.height - 66f, 620f, 24f), "MAVERICK GAME LOOP  |  " + aircraft + "  |  " + mode.ToString(), small);
            GUI.Label(new Rect(14f, Screen.height - 40f, 760f, 24f), "H/Esc: Hangar   F1: hide this overlay   F5/F6/F7: flight feel preset   Tab/Space: weapons", small);

            if (GUI.Button(new Rect(Screen.width - 150f, 14f, 132f, 34f), "HANGAR", button))
                MavSceneLoader.LoadSceneSafe(MavSceneNames.Hangar);
        }

        private void EnsureStyles()
        {
            if (small != null) return;
            small = new GUIStyle(GUI.skin.label);
            small.fontSize = 14;
            small.normal.textColor = Color.white;
            small.fontStyle = FontStyle.Bold;

            button = new GUIStyle(GUI.skin.button);
            button.fontSize = 14;
            button.fontStyle = FontStyle.Bold;
        }
    }
}
