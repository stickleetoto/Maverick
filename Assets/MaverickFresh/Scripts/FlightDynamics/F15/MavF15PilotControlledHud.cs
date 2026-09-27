using UnityEngine;

namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// On-screen readout for a human flying the pilot-controlled F-15 research aircraft: the rig's
    /// <see cref="MavF15PilotControlledDiagnostics"/> (speed, alpha, beta, altitude, research-domain
    /// status, surfaces, THROTTLE INACTIVE) plus its start / ownership status and the keys. Display
    /// only - it reads the rig and writes nothing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MavF15PilotControlledHud : MonoBehaviour
    {
        public MavF15PilotControlledRig rig;
        public bool show = true;
        public KeyCode toggleKey = KeyCode.F1;

        private GUIStyle style;

        private void Update()
        {
            if (MavFreshInput.GetKeyDown(toggleKey))
                show = !show;
        }

        private void OnGUI()
        {
            if (!show)
                return;

            if (rig == null)
                rig = GetComponent<MavF15PilotControlledRig>();
            if (rig == null)
                return;

            if (style == null)
            {
                style = new GUIStyle(GUI.skin.box);
                style.alignment = TextAnchor.UpperLeft;
                style.fontSize = 13;
                style.wordWrap = true;
            }

            string owner = rig.ownership != null ? rig.ownership.owner.ToString() : "no authority";
            string text = rig.debugDiagnostics.Format()
                + "owner: " + owner + " | " + rig.debugStartStatus + "\n"
                + "keys: W/S pitch, A/D roll, Q/E yaw (arrows too), Shift/Ctrl throttle (inactive), F1 hide";
            GUI.Box(new Rect(10f, 10f, 620f, 260f), text, style);
        }
    }
}
