using UnityEngine;

namespace MaverickFresh.Gameplay
{
    /// <summary>
    /// One look for the R1 player UI (hangar, lobby, flight HUD, menus). IMGUI for R1; presentation only, so a
    /// later move to Canvas/UI Toolkit replaces this file and the OnGUI bodies, not the game logic.
    /// Styles are built lazily from inside OnGUI, where GUI.skin exists.
    /// </summary>
    public static class MavGameplayUiStyle
    {
        public static readonly Color Accent = new Color(0.36f, 0.85f, 0.55f);
        public static readonly Color Warning = new Color(1f, 0.72f, 0.25f);
        public static readonly Color Danger = new Color(1f, 0.38f, 0.32f);
        public static readonly Color Text = new Color(0.92f, 0.95f, 0.97f);
        public static readonly Color Muted = new Color(0.65f, 0.71f, 0.76f);

        public static GUIStyle Title, Heading, Body, Small, Mono, Button, BigButton, Card, CardSelected, HudValue, HudLabel, HudCenter;

        private static Texture2D panel, panelStrong, cardTex, cardSelectedTex, buttonTex, buttonHoverTex, black, white;

        public static void Ensure()
        {
            if (Title != null && panel != null)
                return;

            panel = MavGameplayVisuals.SolidTexture(new Color(0.05f, 0.07f, 0.09f, 0.78f));
            panelStrong = MavGameplayVisuals.SolidTexture(new Color(0.03f, 0.04f, 0.05f, 0.92f));
            cardTex = MavGameplayVisuals.SolidTexture(new Color(0.10f, 0.13f, 0.16f, 0.90f));
            cardSelectedTex = MavGameplayVisuals.SolidTexture(new Color(0.12f, 0.30f, 0.20f, 0.95f));
            buttonTex = MavGameplayVisuals.SolidTexture(new Color(0.16f, 0.20f, 0.24f, 0.95f));
            buttonHoverTex = MavGameplayVisuals.SolidTexture(new Color(0.22f, 0.42f, 0.30f, 0.98f));
            black = MavGameplayVisuals.SolidTexture(Color.black);
            white = MavGameplayVisuals.SolidTexture(Color.white);

            Title = Label(40, FontStyle.Bold, Text);
            Heading = Label(22, FontStyle.Bold, Text);
            Body = Label(16, FontStyle.Normal, Text);
            Body.wordWrap = true;
            Small = Label(13, FontStyle.Normal, Muted);
            Small.wordWrap = true;
            Mono = Label(13, FontStyle.Normal, Text);
            Mono.font = Font.CreateDynamicFontFromOSFont("Consolas", 13);
            Mono.wordWrap = false;

            Button = new GUIStyle(GUI.skin.button);
            Button.fontSize = 16;
            Button.fontStyle = FontStyle.Bold;
            Button.normal.background = buttonTex;
            Button.hover.background = buttonHoverTex;
            Button.active.background = buttonHoverTex;
            Button.normal.textColor = Text;
            Button.hover.textColor = Color.white;
            Button.active.textColor = Color.white;
            Button.alignment = TextAnchor.MiddleCenter;

            BigButton = new GUIStyle(Button);
            BigButton.fontSize = 22;

            Card = new GUIStyle(GUI.skin.button);
            Card.normal.background = cardTex;
            Card.hover.background = buttonHoverTex;
            Card.active.background = cardSelectedTex;
            Card.normal.textColor = Text;
            Card.hover.textColor = Color.white;
            Card.fontSize = 18;
            Card.fontStyle = FontStyle.Bold;
            Card.alignment = TextAnchor.MiddleCenter;
            Card.wordWrap = true;

            CardSelected = new GUIStyle(Card);
            CardSelected.normal.background = cardSelectedTex;
            CardSelected.normal.textColor = Color.white;

            HudValue = Label(28, FontStyle.Bold, Accent);
            HudLabel = Label(13, FontStyle.Bold, Accent);
            HudCenter = Label(18, FontStyle.Bold, Warning);
            HudCenter.alignment = TextAnchor.MiddleCenter;
        }

        public static void Panel(Rect r, bool strong = false)
        {
            Ensure();
            GUI.DrawTexture(r, strong ? panelStrong : panel);
        }

        public static void Fill(Rect r, Color c)
        {
            Ensure();
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, white);
            GUI.color = old;
        }

        public static void Fade(float alpha)
        {
            if (alpha <= 0f)
                return;
            Ensure();
            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, Mathf.Clamp01(alpha));
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), black);
            GUI.color = old;
        }

        public static GUIStyle Tinted(GUIStyle style, Color color)
        {
            GUIStyle s = new GUIStyle(style);
            s.normal.textColor = color;
            return s;
        }

        private static GUIStyle Label(int size, FontStyle fontStyle, Color color)
        {
            GUIStyle s = new GUIStyle(GUI.skin.label);
            s.fontSize = size;
            s.fontStyle = fontStyle;
            s.normal.textColor = color;
            return s;
        }
    }
}
