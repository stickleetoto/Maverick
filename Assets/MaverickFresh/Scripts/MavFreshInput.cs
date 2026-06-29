using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace MaverickFresh
{
    /// <summary>
    /// Tiny input wrapper so this fresh prototype works with Legacy Input, New Input System, or Both.
    /// </summary>
    public static class MavFreshInput
    {
        public static bool GetKey(KeyCode key)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(key);
#elif ENABLE_INPUT_SYSTEM
            ButtonControl c = ResolveKey(key);
            return c != null && c.isPressed;
#else
            return false;
#endif
        }

        public static bool GetKeyDown(KeyCode key)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(key);
#elif ENABLE_INPUT_SYSTEM
            ButtonControl c = ResolveKey(key);
            return c != null && c.wasPressedThisFrame;
#else
            return false;
#endif
        }

        public static bool GetKeyUp(KeyCode key)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyUp(key);
#elif ENABLE_INPUT_SYSTEM
            ButtonControl c = ResolveKey(key);
            return c != null && c.wasReleasedThisFrame;
#else
            return false;
#endif
        }

        public static float MouseX()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetAxisRaw("Mouse X");
#elif ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.delta.ReadValue().x * 0.05f : 0f;
#else
            return 0f;
#endif
        }

        public static float MouseY()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetAxisRaw("Mouse Y");
#elif ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.delta.ReadValue().y * 0.05f : 0f;
#else
            return 0f;
#endif
        }

        public static Vector2 MousePosition()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.mousePosition;
#elif ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
#else
            return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
#endif
        }

        public static float GetMouseScrollDelta()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.mouseScrollDelta.y;
#elif ENABLE_INPUT_SYSTEM
            if (Mouse.current == null)
                return 0f;

            float y = Mouse.current.scroll.ReadValue().y;
            return Mathf.Abs(y) > 10f ? y / 120f : y;
#else
            return 0f;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static ButtonControl ResolveKey(KeyCode key)
        {
            Keyboard k = Keyboard.current;
            Mouse m = Mouse.current;

            if (key == KeyCode.Mouse0) return m != null ? m.leftButton : null;
            if (key == KeyCode.Mouse1) return m != null ? m.rightButton : null;
            if (key == KeyCode.Mouse2) return m != null ? m.middleButton : null;

            if (k == null) return null;

            switch (key)
            {
                case KeyCode.A: return k.aKey;
                case KeyCode.B: return k.bKey;
                case KeyCode.C: return k.cKey;
                case KeyCode.D: return k.dKey;
                case KeyCode.E: return k.eKey;
                case KeyCode.F: return k.fKey;
                case KeyCode.G: return k.gKey;
                case KeyCode.H: return k.hKey;
                case KeyCode.Z: return k.zKey;
                case KeyCode.V: return k.vKey;
                case KeyCode.P: return k.pKey;
                case KeyCode.O: return k.oKey;
                case KeyCode.L: return k.lKey;
                case KeyCode.K: return k.kKey;
                case KeyCode.J: return k.jKey;
                case KeyCode.I: return k.iKey;
                case KeyCode.M: return k.mKey;
                case KeyCode.N: return k.nKey;
                case KeyCode.Q: return k.qKey;
                case KeyCode.R: return k.rKey;
                case KeyCode.S: return k.sKey;
                case KeyCode.T: return k.tKey;
                case KeyCode.U: return k.uKey;
                case KeyCode.W: return k.wKey;
                case KeyCode.X: return k.xKey;
                case KeyCode.Y: return k.yKey;

                case KeyCode.LeftShift: return k.leftShiftKey;
                case KeyCode.RightShift: return k.rightShiftKey;
                case KeyCode.LeftControl: return k.leftCtrlKey;
                case KeyCode.RightControl: return k.rightCtrlKey;
                case KeyCode.LeftAlt: return k.leftAltKey;
                case KeyCode.RightAlt: return k.rightAltKey;

                case KeyCode.Space: return k.spaceKey;
                case KeyCode.Tab: return k.tabKey;
                case KeyCode.Return: return k.enterKey;
                case KeyCode.Backspace: return k.backspaceKey;
                case KeyCode.Escape: return k.escapeKey;

                case KeyCode.F1: return k.f1Key;
                case KeyCode.F2: return k.f2Key;
                case KeyCode.F3: return k.f3Key;
                case KeyCode.F4: return k.f4Key;
                case KeyCode.F5: return k.f5Key;
                case KeyCode.F6: return k.f6Key;
                case KeyCode.F7: return k.f7Key;
                case KeyCode.F8: return k.f8Key;
                case KeyCode.F9: return k.f9Key;
                case KeyCode.F10: return k.f10Key;
                case KeyCode.F11: return k.f11Key;
                case KeyCode.F12: return k.f12Key;

                case KeyCode.Alpha1: return k.digit1Key;
                case KeyCode.Alpha2: return k.digit2Key;
                case KeyCode.Alpha3: return k.digit3Key;
                case KeyCode.Alpha4: return k.digit4Key;
                case KeyCode.Alpha5: return k.digit5Key;
                case KeyCode.Alpha6: return k.digit6Key;
                case KeyCode.LeftBracket: return k.leftBracketKey;
                case KeyCode.RightBracket: return k.rightBracketKey;
                case KeyCode.Minus: return k.minusKey;
                case KeyCode.Equals: return k.equalsKey;

                case KeyCode.UpArrow: return k.upArrowKey;
                case KeyCode.DownArrow: return k.downArrowKey;
                case KeyCode.LeftArrow: return k.leftArrowKey;
                case KeyCode.RightArrow: return k.rightArrowKey;
            }

            return null;
        }
#endif
    }
}
