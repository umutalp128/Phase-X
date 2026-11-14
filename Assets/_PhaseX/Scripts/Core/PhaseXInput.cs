using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Thin keyboard/mouse wrapper so the game works with the new Input System (this project's
/// setting) and with the legacy Input Manager on any teammate's machine.
/// Mouse position is in screen pixels, origin bottom-left.
/// </summary>
public static class PhaseXInput
{
    public enum K { F1, F2, F3, F4, D1, D2, D3, D4, D5, Space, Enter, Escape, C }

    public static bool Down(K k)
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return false;
        switch (k)
        {
            case K.F1: return kb.f1Key.wasPressedThisFrame;
            case K.F2: return kb.f2Key.wasPressedThisFrame;
            case K.F3: return kb.f3Key.wasPressedThisFrame;
            case K.F4: return kb.f4Key.wasPressedThisFrame;
            case K.D1: return kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame;
            case K.D2: return kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame;
            case K.D3: return kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame;
            case K.D4: return kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame;
            case K.D5: return kb.digit5Key.wasPressedThisFrame || kb.numpad5Key.wasPressedThisFrame;
            case K.Space: return kb.spaceKey.wasPressedThisFrame;
            case K.Enter: return kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame;
            case K.Escape: return kb.escapeKey.wasPressedThisFrame;
            case K.C: return kb.cKey.wasPressedThisFrame;
        }
        return false;
#else
        switch (k)
        {
            case K.F1: return Input.GetKeyDown(KeyCode.F1);
            case K.F2: return Input.GetKeyDown(KeyCode.F2);
            case K.F3: return Input.GetKeyDown(KeyCode.F3);
            case K.F4: return Input.GetKeyDown(KeyCode.F4);
            case K.D1: return Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1);
            case K.D2: return Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2);
            case K.D3: return Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3);
            case K.D4: return Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4);
            case K.D5: return Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5);
            case K.Space: return Input.GetKeyDown(KeyCode.Space);
            case K.Enter: return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
            case K.Escape: return Input.GetKeyDown(KeyCode.Escape);
            case K.C: return Input.GetKeyDown(KeyCode.C);
        }
        return false;
#endif
    }

    public static Vector2 MousePosition
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
            return Input.mousePosition;
#endif
        }
    }

    /// <summary>Pixels moved this frame.</summary>
    public static Vector2 MouseDelta
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
#else
            return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 12f;
#endif
        }
    }

    public static bool AnyButtonHeld
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            var m = Mouse.current;
            return m != null && (m.leftButton.isPressed || m.rightButton.isPressed || m.middleButton.isPressed);
#else
            return Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2);
#endif
        }
    }

    public static bool AnyButtonPressedThisFrame
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            var m = Mouse.current;
            return m != null && (m.leftButton.wasPressedThisFrame || m.rightButton.wasPressedThisFrame || m.middleButton.wasPressedThisFrame);
#else
            return Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2);
#endif
        }
    }

    /// <summary>Scroll in wheel "notches" (positive = away from the user), normalized across platforms.</summary>
    public static float ScrollNotches
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            float y = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
            if (Mathf.Abs(y) > 10f) y /= 120f; // some platforms report 120 per notch
            return Mathf.Clamp(y, -3f, 3f);
#else
            return Mathf.Clamp(Input.mouseScrollDelta.y, -3f, 3f);
#endif
        }
    }
}
