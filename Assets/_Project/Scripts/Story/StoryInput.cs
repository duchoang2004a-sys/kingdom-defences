using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>Phím cho màn hình Story Intro; chạy được cả New Input System lẫn Legacy (cùng cách làm với GameInput).</summary>
public static class StoryInput
{
    private static bool legacyBroken;

    /// <summary>Space hoặc Enter (kể cả Enter bàn phím số) vừa được nhấn trong frame này.</summary>
    public static bool ConfirmPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        if (kb != null)
            return kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame;
#endif
        return LegacyDown(KeyCode.Space) || LegacyDown(KeyCode.Return) || LegacyDown(KeyCode.KeypadEnter);
    }

    /// <summary>Esc (nút Back của Android được hệ thống báo là Esc) vừa được nhấn.</summary>
    public static bool SkipPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        if (kb != null) return kb.escapeKey.wasPressedThisFrame;
#endif
        return LegacyDown(KeyCode.Escape);
    }

    /// <summary>Space đang được giữ (tua chữ).</summary>
    public static bool SpaceHeld()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        if (kb != null) return kb.spaceKey.isPressed;
#endif
        if (legacyBroken) return false;
        try { return Input.GetKey(KeyCode.Space); }
        catch { legacyBroken = true; return false; }
    }

    private static bool LegacyDown(KeyCode key)
    {
        if (legacyBroken) return false;
        try { return Input.GetKeyDown(key); }
        catch { legacyBroken = true; return false; }
    }
}
