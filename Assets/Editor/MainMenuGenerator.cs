using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// MainMenuGenerator (v3): tạo scene MainMenu gọn nhẹ. Toàn bộ giao diện (logo, 10 nút màn, shop, chọn tướng)
/// được MainMenuController / ShopUI / HeroSelectPopup dựng bằng sprite từ UISpriteLibrary khi chạy,
/// nên không còn text/emoji và không phụ thuộc object lưu sẵn trong scene.
/// Dùng qua: Tools > King of Rush > Generate MainMenu + UI  (hoặc Generate 10 Map + ALL).
/// </summary>
public static class MainMenuGenerator
{
    public static void GenerateMainMenuScene()
    {
        UIAssetPipeline.EnsureAll();

        if (!AssetDatabase.IsValidFolder("Assets/_Project/Scenes"))
            AssetDatabase.CreateFolder("Assets/_Project", "Scenes");

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.orthographic = true;
            cam.orthographicSize = 5.4f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.09f, 0.26f, 1f);
        }

        GameObject canvasObj = new GameObject("Canvas_MainMenu");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasObj.AddComponent<GraphicRaycaster>();

        if (Object.FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            GameInput.EnsureCorrectInputModule(es);
        }

        canvasObj.AddComponent<MainMenuController>();

        GameObject save = new GameObject("SaveManager");
        save.AddComponent<SaveManager>();

        new GameObject("HeroSelectPopup").AddComponent<HeroSelectPopup>();
        new GameObject("ShopUI").AddComponent<ShopUI>();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, "Assets/_Project/Scenes/MainMenu.unity");
        Debug.Log("<color=green>[King of Rush]</color> Đã tạo scene MainMenu (Assets/_Project/Scenes/MainMenu.unity).");
    }
}
