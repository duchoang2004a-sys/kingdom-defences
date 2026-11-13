using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// KRGameSprites: chuẩn hoá import sprite GAMEPLAY (Enemies / Heroes / Effects / Towers / Projectiles).
///  - Frame nhân vật & quái: pivot ở CHÂN (0.45, 0.2) => đứng đúng trên đường / tile, không bị "bay".
///  - Tháp: pivot tại TÂM mặt đáy (0.5, 0.185) => khớp tâm ô đặt tháp (Plot).
///  - Point filter cho pixel-art, Bilinear cho tháp. PPU = 100.
///  - Sao chép sprite tháp vào Resources/Towers để Turret.ApplyUpgrade() đổi sprite được cả trong bản Build.
/// </summary>
public static class KRGameSprites
{
    public const string Root = "Assets/_Project/Sprites";
    private static readonly string[] SubFolders = { "Enemies", "Heroes", "Effects", "Towers", "Projectiles" };

    public static void EnsureAll()
    {
        CopyTowerSpritesToResources();

        List<string> folders = new List<string>();
        for (int i = 0; i < SubFolders.Length; i++)
            if (AssetDatabase.IsValidFolder(Root + "/" + SubFolders[i])) folders.Add(Root + "/" + SubFolders[i]);
        if (AssetDatabase.IsValidFolder("Assets/_Project/Resources/Towers")) folders.Add("Assets/_Project/Resources/Towers");
        if (folders.Count == 0) return;

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", folders.ToArray());
        AssetDatabase.StartAssetEditing();
        try
        {
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!path.EndsWith(".png") || path.EndsWith("_sheet.png")) continue;
                string fn = Path.GetFileName(path);
                if (!(fn.StartsWith("tower_") || fn.StartsWith("enemy_") || fn.StartsWith("hero_") || fn.StartsWith("fx_") || fn.StartsWith("proj_") || fn.StartsWith("aura_") || fn.StartsWith("build_"))) continue;
                ConfigureOne(path);
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }
        AssetDatabase.Refresh();
    }

    private static void CopyTowerSpritesToResources()
    {
        string src = Root + "/Towers";
        if (!AssetDatabase.IsValidFolder(src)) return;
        if (!AssetDatabase.IsValidFolder("Assets/_Project/Resources")) AssetDatabase.CreateFolder("Assets/_Project", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/_Project/Resources/Towers")) AssetDatabase.CreateFolder("Assets/_Project/Resources", "Towers");

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { src });
        for (int i = 0; i < guids.Length; i++)
        {
            string p = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (!p.EndsWith(".png")) continue;
            string dst = "Assets/_Project/Resources/Towers/" + Path.GetFileName(p);
            if (!File.Exists(dst)) AssetDatabase.CopyAsset(p, dst);
            else
            {
                // cập nhật nếu file nguồn mới hơn
                if (File.GetLastWriteTimeUtc(p) > File.GetLastWriteTimeUtc(dst))
                {
                    AssetDatabase.DeleteAsset(dst);
                    AssetDatabase.CopyAsset(p, dst);
                }
            }
        }
    }

    private static void ConfigureOne(string path)
    {
        TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) return;

        string fname = Path.GetFileName(path);
        bool towerHead = fname.StartsWith("tower_head_");
        bool tower = path.Contains("/Towers/") && fname.StartsWith("tower_") && !towerHead;
        bool charFrame = (path.Contains("/Enemies/") || path.Contains("/Heroes/")) && (path.Contains("_frames/") || path.Contains("_fx/"));
        bool dragon = path.Contains("enemy_dragon");

        Vector2 pivot = new Vector2(0.5f, 0.5f);
        SpriteAlignment align = SpriteAlignment.Center;
        if (tower) { pivot = new Vector2(0.5f, 37f / 200f); align = SpriteAlignment.Custom; }
        else if (towerHead) { pivot = new Vector2(0.5f, 37f / 64f); align = SpriteAlignment.Custom; }
        else if (charFrame && !dragon) { pivot = new Vector2(0.45f, 0.2f); align = SpriteAlignment.Custom; }

        FilterMode filter = (tower || towerHead) ? FilterMode.Bilinear : FilterMode.Point;

        TextureImporterSettings s = new TextureImporterSettings();
        ti.ReadTextureSettings(s);

        bool changed = false;
        if (ti.textureType != TextureImporterType.Sprite) changed = true;
        if (s.spriteMode != (int)SpriteImportMode.Single) changed = true;
        if (!Mathf.Approximately(s.spritePixelsPerUnit, 100f)) changed = true;
        if (s.mipmapEnabled) changed = true;
        if (!s.alphaIsTransparency) changed = true;
        if (s.filterMode != filter) changed = true;
        if (s.spriteAlignment != (int)align) changed = true;
        if (align == SpriteAlignment.Custom && (s.spritePivot - pivot).sqrMagnitude > 0.00001f) changed = true;
        if (ti.textureCompression != TextureImporterCompression.Uncompressed) changed = true;
        if (!changed) return;

        ti.textureType = TextureImporterType.Sprite;
        ti.ReadTextureSettings(s);
        s.spriteMode = (int)SpriteImportMode.Single;
        s.spritePixelsPerUnit = 100f;
        s.mipmapEnabled = false;
        s.alphaIsTransparency = true;
        s.filterMode = filter;
        s.wrapMode = TextureWrapMode.Clamp;
        s.spriteAlignment = (int)align;
        s.spritePivot = pivot;
        s.spriteMeshType = SpriteMeshType.FullRect;
        ti.SetTextureSettings(s);
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.SaveAndReimport();
    }
}
