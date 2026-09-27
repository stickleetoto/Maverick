using UnityEngine;

namespace MaverickFresh.FlightDynamics.F15
{
    /// <summary>
    /// On-screen readout for a human flying the pilot-controlled F-15 research aircraft: the rig's
    /// <see cref="MavF15PilotControlledDiagnostics"/> (speed, alpha, beta, altitude, research-domain
    /// status, surfaces, THROTTLE INACTIVE) plus its start / ownership status and the keys.
    ///
    /// It writes nothing to the aircraft except one human request: the law-select key asks the rig to
    /// switch between Direct V1 and Assisted V2 (<see cref="MavF15PilotControlledRig.TrySetControlMode"/>,
    /// refused unless stick and pedals are centred). No key is read inside the flight physics.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MavF15PilotControlledHud : MonoBehaviour
    {
        public MavF15PilotControlledRig rig;
        public bool show = true;
        public KeyCode toggleKey = KeyCode.F1;

        [Tooltip("Switches the pilot-control law: Direct V1 <-> Assisted V2 (stick and pedals centred).")]
        public KeyCode lawSelectKey = KeyCode.F2;

        public string debugLastLawSwitch = "";

        private GUIStyle style;

        private void Update()
        {
            if (MavFreshInput.GetKeyDown(toggleKey))
                show = !show;

            if (MavFreshInput.GetKeyDown(lawSelectKey))
            {
                if (rig == null)
                    rig = GetComponent<MavF15PilotControlledRig>();
                if (rig != null)
                {
                    MavF15PilotControlMode next = rig.controlMode == MavF15PilotControlMode.DirectV1
                        ? MavF15PilotControlMode.AssistedV2
                        : MavF15PilotControlMode.DirectV1;
                    rig.TrySetControlMode(next, out debugLastLawSwitch);
                }
            }
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
                + (debugLastLawSwitch.Length > 0 ? debugLastLawSwitch + "\n" : "")
                + "keys: W/S pitch, A/D roll, Q/E yaw (arrows too), Shift/Ctrl throttle (inactive), F2 law V1/V2, F1 hide";
            GUI.Box(new Rect(10f, 10f, 640f, 300f), text, style);
        }
    }
}
