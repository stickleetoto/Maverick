using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

/// <summary>
/// Runtime-safe input compatibility layer for Project MAVERICK.
///
/// Unity projects using only the newer Input System throw runtime exceptions when old
/// UnityEngine.Input APIs are called. Most MAVERICK prototype scripts were written
/// against the old input manager because it is faster for early Unity iteration.
/// This shim supports both modes:
/// - Old/Input Manager or Both: use UnityEngine.Input.
/// - Input System only: use Keyboard.current / Mouse.current.
///
/// It intentionally covers only the keys/axes used by MAVERICK v0.7.1.
/// </summary>
public static class MaverickInput
{
    public static bool GetKey(KeyCode key)
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return UnityEngine.Input.GetKey(key);
#elif ENABLE_INPUT_SYSTEM
        var control = ToKeyControl(key);
        return control != null && control.isPressed;
#else
        return false;
#endif
    }

    public static bool GetKeyDown(KeyCode key)
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return UnityEngine.Input.GetKeyDown(key);
#elif ENABLE_INPUT_SYSTEM
        var control = ToKeyControl(key);
        return control != null && control.wasPressedThisFrame;
#else
        return false;
#endif
    }

    public static bool GetMouseButton(int button)
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return UnityEngine.Input.GetMouseButton(button);
#elif ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse == null) return false;
        return button switch
        {
            0 => mouse.leftButton.isPressed,
            1 => mouse.rightButton.isPressed,
            2 => mouse.middleButton.isPressed,
            _ => false
        };
#else
        return false;
#endif
    }

    public static bool GetMouseButtonDown(int button)
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return UnityEngine.Input.GetMouseButtonDown(button);
#elif ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse == null) return false;
        return button switch
        {
            0 => mouse.leftButton.wasPressedThisFrame,
            1 => mouse.rightButton.wasPressedThisFrame,
            2 => mouse.middleButton.wasPressedThisFrame,
            _ => false
        };
#else
        return false;
#endif
    }

    public static Vector3 mousePosition
    {
        get
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.mousePosition;
#elif ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse == null) return Vector3.zero;
            Vector2 p = mouse.position.ReadValue();
            return new Vector3(p.x, p.y, 0f);
#else
            return Vector3.zero;
#endif
        }
    }

    public static float GetAxis(string axisName)
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return UnityEngine.Input.GetAxis(axisName);
#else
        return GetAxisRaw(axisName);
#endif
    }

    public static float GetAxisRaw(string axisName)
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return UnityEngine.Input.GetAxisRaw(axisName);
#elif ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        switch (axisName)
        {
            case "Horizontal":
                return AxisFromKeys(keyboard, Key.A, Key.D, Key.LeftArrow, Key.RightArrow);
            case "Vertical":
                return AxisFromKeys(keyboard, Key.S, Key.W, Key.DownArrow, Key.UpArrow);
            case "Mouse X":
                return mouse != null ? mouse.delta.ReadValue().x : 0f;
            case "Mouse Y":
                return mouse != null ? mouse.delta.ReadValue().y : 0f;
            default:
                return 0f;
        }
#else
        return 0f;
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private static float AxisFromKeys(Keyboard keyboard, Key negative, Key positive, Key altNegative, Key altPositive)
    {
        if (keyboard == null) return 0f;
        float v = 0f;
        if (keyboard[negative].isPressed || keyboard[altNegative].isPressed) v -= 1f;
        if (keyboard[positive].isPressed || keyboard[altPositive].isPressed) v += 1f;
        return Mathf.Clamp(v, -1f, 1f);
    }

    private static ButtonControl ToKeyControl(KeyCode key)
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return null;

        return key switch
        {
            KeyCode.A => keyboard.aKey,
            KeyCode.B => keyboard.bKey,
            KeyCode.C => keyboard.cKey,
            KeyCode.D => keyboard.dKey,
            KeyCode.E => keyboard.eKey,
            KeyCode.F => keyboard.fKey,
            KeyCode.G => keyboard.gKey,
            KeyCode.H => keyboard.hKey,
            KeyCode.I => keyboard.iKey,
            KeyCode.J => keyboard.jKey,
            KeyCode.K => keyboard.kKey,
            KeyCode.L => keyboard.lKey,
            KeyCode.M => keyboard.mKey,
            KeyCode.N => keyboard.nKey,
            KeyCode.O => keyboard.oKey,
            KeyCode.P => keyboard.pKey,
            KeyCode.Q => keyboard.qKey,
            KeyCode.R => keyboard.rKey,
            KeyCode.S => keyboard.sKey,
            KeyCode.T => keyboard.tKey,
            KeyCode.U => keyboard.uKey,
            KeyCode.V => keyboard.vKey,
            KeyCode.W => keyboard.wKey,
            KeyCode.X => keyboard.xKey,
            KeyCode.Y => keyboard.yKey,
            KeyCode.Z => keyboard.zKey,
            KeyCode.Alpha1 => keyboard.digit1Key,
            KeyCode.Alpha2 => keyboard.digit2Key,
            KeyCode.Alpha3 => keyboard.digit3Key,
            KeyCode.Alpha4 => keyboard.digit4Key,
            KeyCode.Alpha5 => keyboard.digit5Key,
            KeyCode.Space => keyboard.spaceKey,
            KeyCode.Return => keyboard.enterKey,
            KeyCode.KeypadEnter => keyboard.numpadEnterKey,
            KeyCode.Backspace => keyboard.backspaceKey,
            KeyCode.Escape => keyboard.escapeKey,
            KeyCode.Insert => keyboard.insertKey,
            KeyCode.Home => keyboard.homeKey,
            KeyCode.End => keyboard.endKey,
            KeyCode.LeftShift => keyboard.leftShiftKey,
            KeyCode.RightShift => keyboard.rightShiftKey,
            KeyCode.LeftControl => keyboard.leftCtrlKey,
            KeyCode.RightControl => keyboard.rightCtrlKey,
            KeyCode.LeftAlt => keyboard.leftAltKey,
            KeyCode.RightAlt => keyboard.rightAltKey,
            KeyCode.Equals => keyboard.equalsKey,
            KeyCode.Minus => keyboard.minusKey,
            KeyCode.KeypadPlus => keyboard.numpadPlusKey,
            KeyCode.KeypadMinus => keyboard.numpadMinusKey,
            KeyCode.Mouse0 => Mouse.current != null ? Mouse.current.leftButton : null,
            KeyCode.Mouse1 => Mouse.current != null ? Mouse.current.rightButton : null,
            KeyCode.Mouse2 => Mouse.current != null ? Mouse.current.middleButton : null,
            KeyCode.F1 => keyboard.f1Key,
            KeyCode.F2 => keyboard.f2Key,
            KeyCode.F3 => keyboard.f3Key,
            KeyCode.F4 => keyboard.f4Key,
            KeyCode.F5 => keyboard.f5Key,
            KeyCode.F6 => keyboard.f6Key,
            KeyCode.F7 => keyboard.f7Key,
            KeyCode.F8 => keyboard.f8Key,
            KeyCode.F9 => keyboard.f9Key,
            KeyCode.F10 => keyboard.f10Key,
            KeyCode.F11 => keyboard.f11Key,
            KeyCode.F12 => keyboard.f12Key,
            KeyCode.LeftArrow => keyboard.leftArrowKey,
            KeyCode.RightArrow => keyboard.rightArrowKey,
            KeyCode.UpArrow => keyboard.upArrowKey,
            KeyCode.DownArrow => keyboard.downArrowKey,
            _ => null
        };
    }
#endif

    public static bool GetKeyUp(KeyCode key)
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return UnityEngine.Input.GetKeyUp(key);
#elif ENABLE_INPUT_SYSTEM
        var control = ResolveKeyControl(key);
        return control != null && control.wasReleasedThisFrame;
#else
        return false;
#endif
    }

}
