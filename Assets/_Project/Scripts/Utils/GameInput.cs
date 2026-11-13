using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public static class GameInput
{
    public static Vector2 MousePosition
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.position.ReadValue();
            }
#endif
            try
            {
                return Input.mousePosition;
            }
            catch
            {
                return Vector2.zero;
            }
        }
    }

    public static bool GetMouseButtonDown(int button)
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            if (button == 0) return Mouse.current.leftButton.wasPressedThisFrame;
            if (button == 1) return Mouse.current.rightButton.wasPressedThisFrame;
            if (button == 2) return Mouse.current.middleButton.wasPressedThisFrame;
        }
#endif
        try
        {
            return Input.GetMouseButtonDown(button);
        }
        catch
        {
            return false;
        }
    }

    public static bool GetMouseButton(int button)
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            if (button == 0) return Mouse.current.leftButton.isPressed;
            if (button == 1) return Mouse.current.rightButton.isPressed;
            if (button == 2) return Mouse.current.middleButton.isPressed;
        }
#endif
        try
        {
            return Input.GetMouseButton(button);
        }
        catch
        {
            return false;
        }
    }

    public static bool GetMouseButtonUp(int button)
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            if (button == 0) return Mouse.current.leftButton.wasReleasedThisFrame;
            if (button == 1) return Mouse.current.rightButton.wasReleasedThisFrame;
            if (button == 2) return Mouse.current.middleButton.wasReleasedThisFrame;
        }
#endif
        try
        {
            return Input.GetMouseButtonUp(button);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Tự động kiểm tra và chuyển đổi InputModule trên EventSystem cho tương thích 100% với New Input System hoặc Legacy Input System.
    /// Giúp khắc phục triệt để lỗi không click được các Button UI, kỹ năng Hero hoặc Item.
    /// </summary>
    public static void EnsureCorrectInputModule(GameObject eventSystemObj)
    {
        if (eventSystemObj == null) return;

        UnityEngine.EventSystems.BaseInputModule currentModule = eventSystemObj.GetComponent<UnityEngine.EventSystems.BaseInputModule>();

        // Thử tìm Type InputSystemUIInputModule qua Reflection (nếu project đang kích hoạt New Input System)
        System.Type inputSystemUIModuleType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputSystemUIModuleType != null)
        {
            // Project dùng New Input System: Nếu đang có StandaloneInputModule cũ, thay thế bằng InputSystemUIInputModule
            if (currentModule != null && currentModule.GetType() != inputSystemUIModuleType)
            {
                Object.DestroyImmediate(currentModule);
                currentModule = null;
            }

            if (currentModule == null)
            {
                eventSystemObj.AddComponent(inputSystemUIModuleType);
            }
        }
        else
        {
            // Project dùng Legacy Input System
            if (currentModule == null)
            {
                eventSystemObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
        }
    }
}
