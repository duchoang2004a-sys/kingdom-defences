using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// UIAssetPipeline (v3): chuẩn hoá import sprite UI / Tile / Prop / Map, tạo UISpriteLibrary (Resources)
/// và Tile asset cho Tilemap isometric. KHÔNG phụ thuộc vị trí thư mục: mọi sprite được tìm theo tên file
/// (KRAssetFinder) và phân loại theo kr_manifest.json.
/// </summary>
public static class UIAssetPipeline
{
    public const string SpritesRoot = "Assets/_Project/Sprites";
    public const string ResourcesDir = "Assets/_Project/Resources";
    public const string LibraryPath = "Assets/_Project/Resources/UISpriteLibrary.asset";
    public const string TileAssetsDir = "Assets/_Project/Tiles/Generated";

    [System.Serializable] private class TileEntry { public string id; public int pivotTop; }
    [System.Serializable] private class TileManifest { public List<TileEntry> tiles = new List<TileEntry>(); }

    [MenuItem("Tools/King of Rush/Rebuild UI Sprite Library + Tiles", false, 30)]
    public static void EnsureAllMenu()
    {
        string rep = EnsureAll();
        EditorUtility.DisplayDialog("King of Rush", rep, "OK");
    }

    /// <summary>Chạy toàn bộ bước chuẩn bị asset. Trả về báo cáo ngắn.</summary>
    public static string EnsureAll()
    {
        KRAssetFinder.Invalidate();
        KRGameSprites.EnsureAll();
        ConfigureImports();
        KRAssetFinder.Invalidate();
        int ui = BuildLibrary();
        int tiles = BuildTileAssets();
        UIAssets.Reset();
        string rep = "UISpriteLibrary: " + ui + " sprite | Tile asset: " + tiles;
        Debug.Log("[King of Rush] " + rep);
        if (tiles == 0) Debug.LogError("[King of Rush] KHÔNG tạo được Tile nào! Kiểm tra tiles_manifest.json + PNG tile (hoặc chạy Tools > King of Rush > Fix Sprite Folder Structure).");
        return rep;
    }

    // ------------------------------------------------------------------ Import settings
    private static Dictionary<string, int> LoadTilePivots()
    {
        Dictionary<string, int> d = new Dictionary<string, int>();
        string txt = KRAssetFinder.LoadText("tiles_manifest.json");
        if (string.IsNullOrEmpty(txt)) return d;
        TileManifest m = JsonUtility.FromJson<TileManifest>(txt);
        if (m == null || m.tiles == null) return d;
        for (int i = 0; i < m.tiles.Count; i++) d[m.tiles[i].id] = m.tiles[i].pivotTop;
        return d;
    }

    private static bool IsOurs(string path, out string category)
    {
        category = KRAssetFinder.CanonicalFolder(Path.GetFileName(path));
        return category != null;
    }

    private static void ConfigureImports()
    {
        if (!AssetDatabase.IsValidFolder(SpritesRoot)) return;
        Dictionary<string, int> tilePivot = LoadTilePivots();
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { SpritesRoot });

        AssetDatabase.StartAssetEditing();
        try
        {
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!path.EndsWith(".png")) continue;
                string cat;
                if (!IsOurs(path, out cat)) continue;
                // sprite gameplay (Enemies/Heroes/Effects/Towers/Projectiles) do KRGameSprites xử lý
                if (cat.StartsWith("Enemies") || cat.StartsWith("Heroes") || cat.StartsWith("Effects") || cat.StartsWith("Towers") || cat.StartsWith("Projectiles")) continue;
                ConfigureOne(path, cat, tilePivot);
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }
        AssetDatabase.Refresh();
    }

    private static void ConfigureOne(string path, string category, Dictionary<string, int> tilePivot)
    {
        TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) return;

        string name = Path.GetFileNameWithoutExtension(path);
        Vector2 pivot = new Vector2(0.5f, 0.5f);
        SpriteAlignment align = SpriteAlignment.Center;
        int pt;
        if (category.StartsWith("Tiles") && tilePivot.TryGetValue(name, out pt))
        {
            pivot = new Vector2(0.5f, (64f - pt) / 64f);
            align = SpriteAlignment.Custom;
        }
        else if (category.StartsWith("Props"))
        {
            pivot = new Vector2(0f, 1f);
            align = SpriteAlignment.Custom;
        }
        else if (name == "build_pad")
        {
            pivot = new Vector2(0.5f, (64f - 27f) / 64f);
            align = SpriteAlignment.Custom;
        }
        Vector4 border = BorderFor(name);

        TextureImporterSettings s = new TextureImporterSettings();
        ti.ReadTextureSettings(s);

        bool changed = false;
        if (ti.textureType != TextureImporterType.Sprite) changed = true;
        if (s.spriteMode != (int)SpriteImportMode.Single) changed = true;
        if (!Mathf.Approximately(s.spritePixelsPerUnit, 100f)) changed = true;
        if (s.mipmapEnabled) changed = true;
        if (!s.alphaIsTransparency) changed = true;
        if (s.filterMode != FilterMode.Bilinear) changed = true;
        if (s.spriteAlignment != (int)align) changed = true;
        if (align == SpriteAlignment.Custom && (s.spritePivot - pivot).sqrMagnitude > 0.00001f) changed = true;
        if (ti.textureCompression != TextureImporterCompression.Uncompressed) changed = true;
        if (ti.maxTextureSize < 2048) changed = true;
        if ((ti.spriteBorder - border).sqrMagnitude > 0.01f) changed = true;
        if (!changed) return;

        ti.textureType = TextureImporterType.Sprite;
        ti.ReadTextureSettings(s);
        s.spriteMode = (int)SpriteImportMode.Single;
        s.spritePixelsPerUnit = 100f;
        s.mipmapEnabled = false;
        s.alphaIsTransparency = true;
        s.filterMode = FilterMode.Bilinear;
        s.wrapMode = TextureWrapMode.Clamp;
        s.spriteAlignment = (int)align;
        s.spritePivot = pivot;
        s.spriteMeshType = SpriteMeshType.FullRect;
        ti.SetTextureSettings(s);
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.maxTextureSize = 2048;
        ti.spriteBorder = border;
        ti.SaveAndReimport();
    }

    /// <summary>Viền 9-slice (trái, dưới, phải, trên) theo tên sprite.</summary>
    private static Vector4 BorderFor(string n)
    {
        if (n.EndsWith("_9slice")) return new Vector4(44, 44, 44, 44);
        if (n.StartsWith("btn_wide_")) return new Vector4(40, 40, 40, 40);
        if (n.StartsWith("lvl_btn_")) return new Vector4(34, 34, 34, 34);
        if (n == "hud_chip_bg") return new Vector4(21, 16, 21, 16);
        if (n == "hud_title_pill" || n == "hud_wave_chip_bg") return new Vector4(22, 16, 22, 16);
        if (n.StartsWith("shop_card")) return new Vector4(26, 26, 26, 26);
        if (n.StartsWith("shop_tab")) return new Vector4(22, 22, 22, 22);
        if (n.StartsWith("tag_")) return new Vector4(10, 8, 10, 8);
        if (n.StartsWith("card_hero_")) return new Vector4(44, 44, 44, 44);
        if (n.EndsWith("_bg") && n.StartsWith("bar_")) return new Vector4(11, 8, 11, 8);
        return Vector4.zero;
    }

    // ------------------------------------------------------------------ Library
    private static int PriorityOf(string path)
    {
        string f = KRAssetFinder.CanonicalFolder(Path.GetFileName(path));
        if (f != null && path.Contains("/" + f + "/")) return 0;   // đang ở đúng thư mục chuẩn
        if (path.Contains("/Extra/")) return 3;
        return 1;
    }

    public static int BuildLibrary()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Project")) AssetDatabase.CreateFolder("Assets", "_Project");
        if (!AssetDatabase.IsValidFolder(ResourcesDir)) AssetDatabase.CreateFolder("Assets/_Project", "Resources");

        UISpriteLibrary lib = AssetDatabase.LoadAssetAtPath<UISpriteLibrary>(LibraryPath);
        if (lib == null)
        {
            lib = ScriptableObject.CreateInstance<UISpriteLibrary>();
            AssetDatabase.CreateAsset(lib, LibraryPath);
        }

        lib.entries.Clear();
        HashSet<string> seen = new HashSet<string>();
        if (AssetDatabase.IsValidFolder(SpritesRoot))
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { SpritesRoot });
            List<string> paths = new List<string>();
            for (int i = 0; i < guids.Length; i++)
            {
                string p = AssetDatabase.GUIDToAssetPath(guids[i]);
                string cat;
                if (p.EndsWith(".png") && IsOurs(p, out cat) && cat.StartsWith("UI")) paths.Add(p);
            }
            paths.Sort((a, b) => PriorityOf(a).CompareTo(PriorityOf(b)));

            for (int i = 0; i < paths.Count; i++)
            {
                string key = Path.GetFileNameWithoutExtension(paths[i]);
                if (!seen.Add(key)) continue;
                Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(paths[i]);
                if (sp == null) continue;
                UISpriteLibrary.Entry e = new UISpriteLibrary.Entry();
                e.key = key;
                e.sprite = sp;
                lib.entries.Add(e);
            }
        }

        EditorUtility.SetDirty(lib);
        AssetDatabase.SaveAssets();
        return lib.entries.Count;
    }

    // ------------------------------------------------------------------ Tiles
    public static int BuildTileAssets()
    {
        Dictionary<string, int> pivots = LoadTilePivots();
        if (pivots.Count == 0) { Debug.LogError("[King of Rush] Không đọc được tiles_manifest.json"); return 0; }

        if (!AssetDatabase.IsValidFolder("Assets/_Project/Tiles")) AssetDatabase.CreateFolder("Assets/_Project", "Tiles");
        if (!AssetDatabase.IsValidFolder(TileAssetsDir)) AssetDatabase.CreateFolder("Assets/_Project/Tiles", "Generated");

        int made = 0;
        foreach (KeyValuePair<string, int> kv in pivots)
        {
            Sprite sp = KRAssetFinder.LoadSprite(kv.Key + ".png", "/Tiles/");
            if (sp == null) { Debug.LogWarning("[King of Rush] Thiếu sprite tile: " + kv.Key + ".png"); continue; }

            string assetPath = TileAssetsDir + "/" + kv.Key + ".asset";
            Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(assetPath);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = sp;
                tile.color = Color.white;
                tile.colliderType = Tile.ColliderType.None;
                AssetDatabase.CreateAsset(tile, assetPath);
            }
            else if (tile.sprite != sp)
            {
                tile.sprite = sp;
                EditorUtility.SetDirty(tile);
            }
            made++;
        }
        AssetDatabase.SaveAssets();
        return made;
    }

    public static TileBase LoadTile(string id)
    {
        return AssetDatabase.LoadAssetAtPath<Tile>(TileAssetsDir + "/" + id + ".asset");
    }
}
