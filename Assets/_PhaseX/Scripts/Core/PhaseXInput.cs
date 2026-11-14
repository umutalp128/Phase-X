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
    public enum K { F1, F2, F3, F4, F5, F6, F7, F8, F9, D1, D2, D3, D4, D5, Space, Enter, Escape, C, Z, X, Tab, W, A, S, D, E, V, Shift, Left, Right, Up, Down, Backspace }

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
            case K.F5: return kb.f5Key.wasPressedThisFrame;
            case K.F6: return kb.f6Key.wasPressedThisFrame;
            case K.F7: return kb.f7Key.wasPressedThisFrame;
            case K.F8: return kb.f8Key.wasPressedThisFrame;
            case K.F9: return kb.f9Key.wasPressedThisFrame;
            case K.D1: return kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame;
            case K.D2: return kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame;
            case K.D3: return kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame;
            case K.D4: return kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame;
            case K.D5: return kb.digit5Key.wasPressedThisFrame || kb.numpad5Key.wasPressedThisFrame;
            case K.Space: return kb.spaceKey.wasPressedThisFrame;
            case K.Enter: return kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame;
            case K.Escape: return kb.escapeKey.wasPressedThisFrame;
            case K.C: return kb.cKey.wasPressedThisFrame;
            case K.Z: return kb.zKey.wasPressedThisFrame;
            case K.X: return kb.xKey.wasPressedThisFrame;
            case K.Tab: return kb.tabKey.wasPressedThisFrame;
            case K.W: return kb.wKey.wasPressedThisFrame;
            case K.A: return kb.aKey.wasPressedThisFrame;
            case K.S: return kb.sKey.wasPressedThisFrame;
            case K.D: return kb.dKey.wasPressedThisFrame;
            case K.E: return kb.eKey.wasPressedThisFrame;
            case K.V: return kb.vKey.wasPressedThisFrame;
            case K.Shift: return kb.leftShiftKey.wasPressedThisFrame || kb.rightShiftKey.wasPressedThisFrame;
            case K.Left: return kb.leftArrowKey.wasPressedThisFrame;
            case K.Right: return kb.rightArrowKey.wasPressedThisFrame;
            case K.Up: return kb.upArrowKey.wasPressedThisFrame;
            case K.Down: return kb.downArrowKey.wasPressedThisFrame;
            case K.Backspace: return kb.backspaceKey.wasPressedThisFrame;
        }
        return false;
#else
        switch (k)
        {
            case K.F1: return Input.GetKeyDown(KeyCode.F1);
            case K.F2: return Input.GetKeyDown(KeyCode.F2);
            case K.F3: return Input.GetKeyDown(KeyCode.F3);
            case K.F4: return Input.GetKeyDown(KeyCode.F4);
            case K.F5: return Input.GetKeyDown(KeyCode.F5);
            case K.F6: return Input.GetKeyDown(KeyCode.F6);
            case K.F7: return Input.GetKeyDown(KeyCode.F7);
            case K.F8: return Input.GetKeyDown(KeyCode.F8);
            case K.F9: return Input.GetKeyDown(KeyCode.F9);
            case K.D1: return Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1);
            case K.D2: return Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2);
            case K.D3: return Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3);
            case K.D4: return Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4);
            case K.D5: return Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5);
            case K.Space: return Input.GetKeyDown(KeyCode.Space);
            case K.Enter: return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
            case K.Escape: return Input.GetKeyDown(KeyCode.Escape);
            case K.C: return Input.GetKeyDown(KeyCode.C);
            case K.Z: return Input.GetKeyDown(KeyCode.Z);
            case K.X: return Input.GetKeyDown(KeyCode.X);
            case K.Tab: return Input.GetKeyDown(KeyCode.Tab);
            case K.W: return Input.GetKeyDown(KeyCode.W);
            case K.A: return Input.GetKeyDown(KeyCode.A);
            case K.S: return Input.GetKeyDown(KeyCode.S);
            case K.D: return Input.GetKeyDown(KeyCode.D);
            case K.E: return Input.GetKeyDown(KeyCode.E);
            case K.V: return Input.GetKeyDown(KeyCode.V);
            case K.Shift: return Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift);
            case K.Left: return Input.GetKeyDown(KeyCode.LeftArrow);
            case K.Right: return Input.GetKeyDown(KeyCode.RightArrow);
            case K.Up: return Input.GetKeyDown(KeyCode.UpArrow);
            case K.Down: return Input.GetKeyDown(KeyCode.DownArrow);
            case K.Backspace: return Input.GetKeyDown(KeyCode.Backspace);
        }
        return false;
#endif
    }

    static readonly K[] FunctionKeys = { K.F1, K.F2, K.F3, K.F4, K.F5, K.F6, K.F7, K.F8, K.F9 };

    /// <summary>Index (0-8) of the F1-F9 key pressed this frame, or -1.</summary>
    public static int FunctionKeyDown()
    {
        for (int i = 0; i < FunctionKeys.Length; i++) if (Down(FunctionKeys[i])) return i;
        return -1;
    }

    /// <summary>True while a movement key is held (used by the 3D character controller).</summary>
    public static bool Held(K k)
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return false;
        switch (k)
        {
            case K.W: return kb.wKey.isPressed;
            case K.A: return kb.aKey.isPressed;
            case K.S: return kb.sKey.isPressed;
            case K.D: return kb.dKey.isPressed;
            case K.Shift: return kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            case K.Left: return kb.leftArrowKey.isPressed;
            case K.Right: return kb.rightArrowKey.isPressed;
            case K.Up: return kb.upArrowKey.isPressed;
            case K.Down: return kb.downArrowKey.isPressed;
        }
        return false;
#else
        switch (k)
        {
            case K.W: return Input.GetKey(KeyCode.W);
            case K.A: return Input.GetKey(KeyCode.A);
            case K.S: return Input.GetKey(KeyCode.S);
            case K.D: return Input.GetKey(KeyCode.D);
            case K.Shift: return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            case K.Left: return Input.GetKey(KeyCode.LeftArrow);
            case K.Right: return Input.GetKey(KeyCode.RightArrow);
            case K.Up: return Input.GetKey(KeyCode.UpArrow);
            case K.Down: return Input.GetKey(KeyCode.DownArrow);
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
