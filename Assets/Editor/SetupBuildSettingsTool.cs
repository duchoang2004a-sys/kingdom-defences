using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Công cụ tự động đồng bộ tất cả Scene vào Build Settings / Build Profiles của Unity.
/// Giải quyết triệt để lỗi:
/// "Scene 'Level_1' couldn't be loaded because it has not been added to the active build profile or shared scene list..."
/// </summary>
[InitializeOnLoad]
public class SetupBuildSettingsTool
{
    static SetupBuildSettingsTool()
    {
        // Tự động kiểm tra đồng bộ khi load project
        EditorApplication.delayCall += () =>
        {
            SyncAllScenesToBuildSettings(silent: true);
        };
    }

    [MenuItem("Tools/Kingdom Rush/Sync All Scenes to Build Settings (Fix Scene Load)", false, 10)]
    [MenuItem("Tools/Tower Defense/Sync All Scenes to Build Settings", false, 10)]
    public static void SyncAllScenesManual()
    {
        SyncAllScenesToBuildSettings(silent: false);
    }

    public static void SyncAllScenesToBuildSettings(bool silent = false)
    {
        // 1. Quét tất cả file .unity trong project
        string[] allSceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/_Project/Scenes", "Assets/Scenes" });
        if (allSceneGuids == null || allSceneGuids.Length == 0)
        {
            // Quét toàn bộ Assets nếu không tìm thấy trong thư mục chuẩn
            allSceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
        }

        List<string> scenePaths = new List<string>();
        foreach (string guid in allSceneGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(path) && path.EndsWith(".unity") && !scenePaths.Contains(path))
            {
                scenePaths.Add(path);
            }
        }

        if (scenePaths.Count == 0)
        {
            if (!silent)
            {
                EditorUtility.DisplayDialog("Thông Báo", "Không tìm thấy Scene nào trong thư mục Assets!", "OK");
            }
            return;
        }

        // 2. Sắp xếp thông minh:
        // - MainMenu luôn ở index 0
        // - Level_1, Level_2, ... xếp theo thứ tự số
        // - Các scene khác xếp phía sau
        scenePaths.Sort((a, b) =>
        {
            string nameA = Path.GetFileNameWithoutExtension(a);
            string nameB = Path.GetFileNameWithoutExtension(b);

            // MainMenu luôn lên đầu (Index 0)
            if (nameA.Equals("MainMenu", StringComparison.OrdinalIgnoreCase)) return -1;
            if (nameB.Equals("MainMenu", StringComparison.OrdinalIgnoreCase)) return 1;

            // Xử lý các Level_X
            bool aIsLevel = nameA.StartsWith("Level_", StringComparison.OrdinalIgnoreCase);
            bool bIsLevel = nameB.StartsWith("Level_", StringComparison.OrdinalIgnoreCase);

            if (aIsLevel && bIsLevel)
            {
                int numA = ExtractLevelNumber(nameA);
                int numB = ExtractLevelNumber(nameB);
                return numA.CompareTo(numB);
            }
            if (aIsLevel) return -1;
            if (bIsLevel) return 1;

            return string.Compare(nameA, nameB, StringComparison.OrdinalIgnoreCase);
        });

        // 3. Cập nhật EditorBuildSettings.scenes
        EditorBuildSettingsScene[] newBuildScenes = scenePaths
            .Select(path => new EditorBuildSettingsScene(path, true))
            .ToArray();

        // Kiểm tra xem có thay đổi không
        bool isChanged = false;
        EditorBuildSettingsScene[] currentScenes = EditorBuildSettings.scenes;

        if (currentScenes.Length != newBuildScenes.Length)
        {
            isChanged = true;
        }
        else
        {
            for (int i = 0; i < currentScenes.Length; i++)
            {
                if (currentScenes[i].path != newBuildScenes[i].path || !currentScenes[i].enabled)
                {
                    isChanged = true;
                    break;
                }
            }
        }

        if (isChanged)
        {
            EditorBuildSettings.scenes = newBuildScenes;
            AssetDatabase.SaveAssets();
        }

        if (!silent || isChanged)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("<color=green><b>[SetupBuildSettingsTool] Đã đồng bộ thành công các Scenes vào Build Settings:</b></color>");
            for (int i = 0; i < newBuildScenes.Length; i++)
            {
                sb.AppendLine($"  [{i}] {newBuildScenes[i].path} (Enabled: {newBuildScenes[i].enabled})");
            }
            Debug.Log(sb.ToString());

            if (!silent)
            {
                EditorUtility.DisplayDialog(
                    "Đồng Bộ Thành Công!",
                    $"Đã thêm {newBuildScenes.Length} scene vào Build Settings / Build Profiles:\n" +
                    string.Join("\n", newBuildScenes.Select((s, idx) => $"[{idx}] {Path.GetFileNameWithoutExtension(s.path)}")),
                    "Tuyệt vời"
                );
            }
        }
    }

    private static int ExtractLevelNumber(string name)
    {
        string numStr = name.Replace("Level_", "").Replace("level_", "").Trim();
        if (int.TryParse(numStr, out int num))
        {
            return num;
        }
        return 999;
    }
}
