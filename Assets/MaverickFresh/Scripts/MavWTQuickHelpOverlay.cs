using UnityEngine;

namespace MaverickFresh
{
    /// <summary>H toggles a War-Thunder-like quick controls card.</summary>
    [DisallowMultipleComponent]
    public class MavWTQuickHelpOverlay : MonoBehaviour
    {
        public bool visible;
        public KeyCode toggleKey = KeyCode.H;
        public int fontSize = 13;
        public Rect rect = new Rect(18f, 520f, 560f, 230f);

        private GUIStyle style;
        private GUIStyle box;

        private void Update()
        {
            if (MavFreshInput.GetKeyDown(toggleKey))
                visible = !visible;
        }

        private void OnGUI()
        {
            if (!visible) return;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label);
                style.normal.textColor = Color.white;
                style.fontSize = fontSize;
                style.richText = true;
                style.wordWrap = true;
            }
            if (box == null)
            {
                box = new GUIStyle(GUI.skin.box);
                box.alignment = TextAnchor.UpperLeft;
            }

            GUI.Box(rect, "", box);
            GUI.Label(new Rect(rect.x + 10f, rect.y + 8f, rect.width - 20f, rect.height - 16f),
                "<b>MAVERICK WT-LIKE QUICK HELP</b>\n" +
                "F5 Balanced | F6 Smooth | F7 Aggressive | F8 Direct Debug\n" +
                "Mouse Aim = desired flight attitude | W/S = elevator override | A/D = roll | Q/E = rudder\n" +
                "Shift/Ctrl = throttle | C/Alt hold = free look | RMB = zoom\n" +
                "CAS: F designate | Tab cycle target | 1/2 secondary | 3 missile | 4 bomb | Mouse0 gun | Space secondary\n" +
                "TGP: T display | O mode | IJKL slew | 5/6 zoom | R lock | G push designation\n" +
                "Physical AI: F11 on/off | F3 mode | F4 reward log | F9 flight CSV\n" +
                "H = hide this help", style);
        }
    }
}
