using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>LevelValidator (v3): kiểm tra nhanh 10 scene Level_1..10 (mở lần lượt, chạy KRDiagnostics.DiagnoseScene).</summary>
public static class LevelValidator
{
    [MenuItem("Tools/King of Rush/Validate All Levels", false, 51)]
    public static void ValidateAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string original = EditorSceneManager.GetActiveScene().path;
        StringBuilder sb = new StringBuilder();
        int bad = 0;
        for (int n = 1; n <= 10; n++)
        {
            string path = "Assets/_Project/Scenes/Level_" + n + ".unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) { sb.AppendLine("Level_" + n + ": KHÔNG CÓ scene"); bad++; continue; }
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            string r = KRDiagnostics.DiagnoseScene();
            if (r.Contains("!!") || r.Contains("KHÔNG")) bad++;
            sb.AppendLine(r);
        }
        if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original, OpenSceneMode.Single);
        Debug.Log("[King of Rush] Validate: " + bad + " màn có vấn đề\n" + sb);
        EditorUtility.DisplayDialog("King of Rush - Validate", bad == 0 ? "Cả 10 màn đều ổn." : bad + " màn có vấn đề - xem Console.", "OK");
    }
}
