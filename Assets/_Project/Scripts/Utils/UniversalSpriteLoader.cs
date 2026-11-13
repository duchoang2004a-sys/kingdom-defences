using System;
using System.IO;
using UnityEngine;

/// <summary>
/// UniversalSpriteLoader: Bộ nạp Sprite toàn năng cho Unity Engine.
/// Tự động nạp Sprite thông qua:
/// 1. AssetDatabase (trong Unity Editor)
/// 2. Auto-fix TextureImporter sang Sprite (2D and UI) nếu ảnh đang ở dạng Texture 2D thường
/// 3. Read Direct Bytes từ đĩa cứng bằng File.ReadAllBytes & Texture2D.LoadImage (Runtime & Editor Standalone)
/// 4. In Log chi tiết trạng thái nạp vào Unity Console / Terminal
/// </summary>
public static class UniversalSpriteLoader
{
    public static Sprite LoadSprite(string relativeOrFullPath)
    {
        if (string.IsNullOrEmpty(relativeOrFullPath)) return null;

#if UNITY_EDITOR
        // 1. Nạp trực tiếp dạng Sprite từ AssetDatabase
        Sprite spr = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(relativeOrFullPath);
        if (spr != null)
        {
            Debug.Log($"<color=cyan>[SpriteLoader]</color> Loaded Sprite from AssetDatabase: {relativeOrFullPath}");
            return spr;
        }

        // 2. Nếu file tồn tại nhưng chưa chuyển TextureImporter thành Sprite (2D and UI)
        Texture2D loadedTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(relativeOrFullPath);
        if (loadedTex != null)
        {
            UnityEditor.TextureImporter importer = UnityEditor.AssetImporter.GetAtPath(relativeOrFullPath) as UnityEditor.TextureImporter;
            if (importer != null && importer.textureType != UnityEditor.TextureImporterType.Sprite)
            {
                importer.textureType = UnityEditor.TextureImporterType.Sprite;
                importer.SaveAndReimport();
                spr = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(relativeOrFullPath);
                if (spr != null)
                {
                    Debug.Log($"<color=green>[SpriteLoader AUTO-FIX]</color> Converted & loaded Sprite: {relativeOrFullPath}");
                    return spr;
                }
            }
            Sprite createdSpr = Sprite.Create(loadedTex, new Rect(0, 0, loadedTex.width, loadedTex.height), new Vector2(0.5f, 0.5f), 100f);
            Debug.Log($"<color=green>[SpriteLoader CONVERT]</color> Created Sprite from Texture2D: {relativeOrFullPath} ({loadedTex.width}x{loadedTex.height})");
            return createdSpr;
        }
#endif

        // 3. Đọc dữ liệu Raw File Bytes từ hệ thống tệp (Chạy được ở cả Build & Editor)
        string[] possiblePaths = new string[]
        {
            relativeOrFullPath,
            Path.Combine(Application.dataPath, "..", relativeOrFullPath),
            Path.Combine(Application.dataPath, relativeOrFullPath.Replace("Assets/", "")),
            Path.Combine(Directory.GetCurrentDirectory(), relativeOrFullPath),
            Path.Combine(Directory.GetCurrentDirectory(), "public/assets/aistudio", Path.GetFileName(relativeOrFullPath))
        };

        foreach (string p in possiblePaths)
        {
            if (File.Exists(p))
            {
                try
                {
                    byte[] bytes = File.ReadAllBytes(p);
                    Texture2D rawTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (rawTex.LoadImage(bytes))
                    {
                        Sprite sprFromFile = Sprite.Create(rawTex, new Rect(0, 0, rawTex.width, rawTex.height), new Vector2(0.5f, 0.5f), 100f);
                        Debug.Log($"<color=green>[SpriteLoader RAW BYTES SUCCESS]</color> Loaded raw image bytes from: {p} ({rawTex.width}x{rawTex.height})");
                        return sprFromFile;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SpriteLoader ERROR] Failed reading bytes at {p}: {ex.Message}");
                }
            }
        }

        Debug.LogWarning($"<color=red>[SpriteLoader MISSING]</color> Could not locate or parse sprite at path: {relativeOrFullPath}");
        return null;
    }
}
