using UnityEngine;

namespace EaglePhysicalAI.Controls
{
    /// <summary>
    /// Central War-Thunder-inspired keybind profile.
    /// This is intentionally editable in Inspector so we can tune without hunting through scripts.
    /// </summary>
    public class MaverickWTKeybindProfileV11 : MonoBehaviour
    {
        [Header("Flight")]
        public KeyCode recenterAim = KeyCode.Mouse2;
        public KeyCode freeLook = KeyCode.C;
        public KeyCode freeLookAlt = KeyCode.LeftAlt;

        public KeyCode throttleUp = KeyCode.LeftShift;
        public KeyCode throttleDown = KeyCode.LeftControl;
        public KeyCode wep = KeyCode.W;
        public KeyCode idle = KeyCode.X;

        public KeyCode pitchDownOverride = KeyCode.S;
        public KeyCode rollLeft = KeyCode.A;
        public KeyCode rollRight = KeyCode.D;
        public KeyCode yawLeft = KeyCode.Q;
        public KeyCode yawRight = KeyCode.E;

        [Header("Aircraft Systems")]
        public KeyCode landingGear = KeyCode.G;
        public KeyCode flapsToggle = KeyCode.F;
        public KeyCode flapsUp = KeyCode.LeftBracket;
        public KeyCode flapsDown = KeyCode.RightBracket;
        public KeyCode airbrake = KeyCode.B;
        public KeyCode wheelBrake = KeyCode.B;
        public KeyCode parkingBrake = KeyCode.P;

        [Header("Control Modes")]
        public KeyCode modeMouseAim = KeyCode.F5;
        public KeyCode modeAssisted = KeyCode.F6;
        public KeyCode modeRealistic = KeyCode.F7;
        public KeyCode modeAI = KeyCode.F8;

        [Header("Radar")]
        public KeyCode radarPower = KeyCode.BackQuote;
        public KeyCode radarCycleMode = KeyCode.R;
        public KeyCode radarNextTrack = KeyCode.T;
        public KeyCode radarLock = KeyCode.O;
        public KeyCode radarUnlock = KeyCode.U;
        public KeyCode radarAutoSelect = KeyCode.LeftAlt;

        [Header("Targeting Pod")]
        public KeyCode podCycleMode = KeyCode.P;
        public KeyCode podSlaveToRadar = KeyCode.Y;
        public KeyCode podDesignate = KeyCode.Return;
        public KeyCode podClear = KeyCode.Backspace;

        [Header("Weapons")]
        public KeyCode firePrimary = KeyCode.Mouse0;
        public KeyCode fireSecondary = KeyCode.Space;
        public KeyCode switchPrimary = KeyCode.Alpha1;
        public KeyCode switchSecondary = KeyCode.Alpha2;
        public KeyCode confirm = KeyCode.Return;
        public KeyCode abort = KeyCode.Backspace;

        [Header("HUD / Tuning")]
        public KeyCode toggleHUD = KeyCode.F12;
        public KeyCode presetHeavy = KeyCode.Home;
        public KeyCode presetArcade = KeyCode.PageUp;
        public KeyCode presetRealistic = KeyCode.End;
    }
}
