using UnityEngine;

namespace MaverickFresh.Gameplay
{
    /// <summary>
    /// The flight session's screens: the start fade, the pause menu and the failure screen. Presentation only -
    /// every button calls the director, which owns the state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MavFlightSessionUi : MonoBehaviour
    {
        public MavFlightSessionDirector director;
        public float fadeInSeconds = 0.6f;

        private void OnGUI()
        {
            if (director == null)
                return;

            MavGameplayUiStyle.Ensure();
            GUI.depth = 0;
            float w = Screen.width, h = Screen.height;

            switch (director.State)
            {
                case MavFlightSessionState.Initializing:
                    MavGameplayUiStyle.Fade(1f);
                    GUI.Label(new Rect(0f, h * 0.5f - 20f, w, 40f),
                        "PREPARING " + (director.Definition != null ? director.Definition.displayName : "AIRCRAFT"),
                        Centered(MavGameplayUiStyle.Heading));
                    break;

                case MavFlightSessionState.Playing:
                    MavGameplayUiStyle.Fade(1f - director.SecondsInState / fadeInSeconds);
                    if (director.legacyDevelopmentFlight)
                        GUI.Label(new Rect(0f, h - 30f, w, 24f),
                            "DEVELOPMENT FLIGHT - LEGACY FLIGHT MODEL (not a validated flight model)   Esc: menu",
                            Centered(MavGameplayUiStyle.Tinted(MavGameplayUiStyle.Small, MavGameplayUiStyle.Warning)));
                    break;

                case MavFlightSessionState.Paused:
                    DrawPause(w, h);
                    break;

                case MavFlightSessionState.Failed:
                    DrawFailure(w, h);
                    break;

                case MavFlightSessionState.Returning:
                    MavGameplayUiStyle.Fade(0.85f);
                    break;
            }
        }

        private void DrawPause(float w, float h)
        {
            MavGameplayUiStyle.Fade(0.45f);
            Rect r = new Rect(w * 0.5f - 180f, h * 0.5f - 150f, 360f, 300f);
            MavGameplayUiStyle.Panel(r, true);
            GUI.Label(new Rect(r.x, r.y + 18f, r.width, 44f), "PAUSED", Centered(MavGameplayUiStyle.Title));
            if (GUI.Button(new Rect(r.x + 40f, r.y + 90f, r.width - 80f, 46f), "RESUME", MavGameplayUiStyle.Button))
                director.Resume();
            if (GUI.Button(new Rect(r.x + 40f, r.y + 146f, r.width - 80f, 46f), "RESTART FLIGHT", MavGameplayUiStyle.Button))
                director.RestartFlight();
            if (GUI.Button(new Rect(r.x + 40f, r.y + 202f, r.width - 80f, 46f), "RETURN TO HANGAR", MavGameplayUiStyle.Button))
                director.ReturnToHangar();
            GUI.Label(new Rect(r.x, r.y + 258f, r.width, 24f), "Esc resume   R restart", Centered(MavGameplayUiStyle.Small));
        }

        private void DrawFailure(float w, float h)
        {
            MavGameplayUiStyle.Fade(0.6f);
            Rect r = new Rect(w * 0.5f - 280f, h * 0.5f - 150f, 560f, 300f);
            MavGameplayUiStyle.Panel(r, true);
            GUI.Label(new Rect(r.x, r.y + 18f, r.width, 40f), director.failureTitle,
                Centered(MavGameplayUiStyle.Tinted(MavGameplayUiStyle.Heading, MavGameplayUiStyle.Danger)));
            GUI.Label(new Rect(r.x + 30f, r.y + 70f, r.width - 60f, 90f), director.failureReason, Centered(MavGameplayUiStyle.Body));
            GUI.Label(new Rect(r.x + 30f, r.y + 160f, r.width - 60f, 40f), "F1 shows the technical detail.", Centered(MavGameplayUiStyle.Small));

            bool canRetry = director.Definition != null && director.Definition.IsLaunchable || director.legacyDevelopmentFlight;
            GUI.enabled = canRetry;
            if (GUI.Button(new Rect(r.x + 40f, r.y + 220f, 220f, 48f), "RETRY", MavGameplayUiStyle.Button))
                director.RestartFlight();
            GUI.enabled = true;
            if (GUI.Button(new Rect(r.x + r.width - 260f, r.y + 220f, 220f, 48f), "RETURN TO HANGAR", MavGameplayUiStyle.Button))
                director.ReturnToHangar();
        }

        private static GUIStyle Centered(GUIStyle style)
        {
            GUIStyle s = new GUIStyle(style);
            s.alignment = TextAnchor.MiddleCenter;
            s.wordWrap = true;
            return s;
        }
    }
}
