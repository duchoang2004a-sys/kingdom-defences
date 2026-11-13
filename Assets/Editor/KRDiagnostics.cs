using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// KRDiagnostics: kiểm tra "sức khoẻ" project + scene đang mở và nói rõ THIẾU / SAI CHỖ ở đâu.
/// Menu: Tools ▸ King of Rush ▸ Diagnose (kiểm tra project + scene).
/// </summary>
public static class KRDiagnostics
{
    private static readonly string[] RequiredUiKeys = {
        "btn_home", "btn_pause", "btn_play", "btn_ff", "btn_shop", "btn_settings", "btn_close", "btn_check", "btn_wide_green", "btn_wide_orange",
        "hud_topbar_bg", "hud_chip_bg", "hud_title_pill", "hud_wave_chip_bg", "icon_coin", "icon_heart", "icon_skull_kills", "icon_wave_flag", "icon_gem",
        "icon_skill_hero1_strike", "icon_skill_hero2_aura", "icon_reinforcements", "icon_spell_meteor", "icon_spell_freeze", "skill_cooldown_overlay",
        "icon_tower_archer", "icon_tower_mage", "icon_tower_cannon", "icon_tower_barracks", "icon_tower_ice", "panel_dark_9slice",
        "shop_card_bg", "item_potion_red", "stat_hp", "lvl_btn_open", "lvl_badge_gold", "card_hero_bg", "portrait_hero_knight", "build_pad",
        "main_menu_decor_background_1920x1080", "logo_title", "star_full", "star_empty" };

    private static readonly string[] Prefabs = {
        "Goblin", "GoblinArcher", "Orc", "DarkElf", "Demon", "Troll", "Golem", "TrollKing", "Shaman", "GolemIce", "DemonLord", "DragonBoss",
        "ArcherTower_Lv1", "MageTower_Lv1", "CannonTower_Lv1", "Barracks_Lv1", "IceTower_Lv1", "ArrowBullet", "CannonBullet", "MageBullet", "IceBullet",
        "ExplosionVFX", "HitSparkVFX", "LightningChainVFX", "ReinforcementSoldier", "Hero_Knight", "Hero_Ranger", "Hero_Mage", "Hero_Paladin" };

    [MenuItem("Tools/King of Rush/Diagnose (kiểm tra project + scene)", false, 50)]
    public static void RunMenu()
    {
        string r = Run();
        Debug.Log("[King of Rush Diagnose]\n" + r);
        EditorUtility.DisplayDialog("King of Rush - Diagnose", r.Length > 1400 ? r.Substring(0, 1400) + "\n... (xem Console)" : r, "OK");
    }

    public static string Run()
    {
        KRAssetFinder.Invalidate();
        StringBuilder sb = new StringBuilder();

        // 1) Manifest / thư mục
        KRAssetFinder.Manifest m = KRAssetFinder.GetManifest();
        int found = 0, missing = 0, misplaced = 0;
        List<string> missList = new List<string>(), misList = new List<string>();
        for (int i = 0; i < m.entries.Count; i++)
        {
            string p = KRAssetFinder.Find(m.entries[i].n, "/" + m.entries[i].f + "/");
            if (p == null) { missing++; if (missList.Count < 6) missList.Add(m.entries[i].n); continue; }
            found++;
            if (!p.Contains("/" + m.entries[i].f + "/")) { misplaced++; if (misList.Count < 6) misList.Add(p); }
        }
        sb.AppendLine("[1] Sprite theo manifest: " + m.entries.Count + " file | có " + found + " | THIẾU " + missing + " | SAI THƯ MỤC " + misplaced);
        if (m.entries.Count == 0) sb.AppendLine("    !! Không thấy kr_manifest.json - chưa copy đủ patch.");
        if (missList.Count > 0) sb.AppendLine("    thiếu: " + string.Join(", ", missList.ToArray()));
        if (misList.Count > 0) sb.AppendLine("    sai chỗ (ví dụ): " + string.Join(" ; ", misList.ToArray()) + "\n    -> chạy Tools > King of Rush > Fix Sprite Folder Structure");

        // 2) JSON map
        int jsons = 0;
        for (int n = 1; n <= 10; n++) if (KRAssetFinder.Find("level_" + n.ToString("D2") + "_map.json") != null) jsons++;
        sb.AppendLine("[2] level_XX_map.json: " + jsons + "/10 | tiles_manifest.json: " + (KRAssetFinder.Find("tiles_manifest.json") != null ? "có" : "THIẾU"));

        // 3) Tile
        int tileAssets = 0;
        string[] tg = AssetDatabase.FindAssets("t:Tile", new[] { "Assets/_Project/Tiles/Generated" });
        tileAssets = tg.Length;
        sb.AppendLine("[3] Tile asset đã tạo: " + tileAssets + (tileAssets == 0 ? "  !! (map sẽ đen) -> Tools > King of Rush > Rebuild UI Sprite Library + Tiles" : ""));

        // 4) UI library
        UISpriteLibrary lib = AssetDatabase.LoadAssetAtPath<UISpriteLibrary>(UIAssetPipeline.LibraryPath);
        List<string> badKeys = new List<string>();
        for (int i = 0; i < RequiredUiKeys.Length; i++)
            if (lib == null || lib.Get(RequiredUiKeys[i]) == null) badKeys.Add(RequiredUiKeys[i]);
        sb.AppendLine("[4] UISpriteLibrary: " + (lib != null ? lib.entries.Count + " sprite" : "CHƯA CÓ") + " | key thiếu: " + badKeys.Count + (badKeys.Count > 0 ? " (" + string.Join(", ", badKeys.GetRange(0, Mathf.Min(6, badKeys.Count)).ToArray()) + ")" : ""));

        // 5) Prefab
        List<string> noPrefab = new List<string>();
        for (int i = 0; i < Prefabs.Length; i++) if (KRAssetFinder.Find(Prefabs[i] + ".prefab") == null) noPrefab.Add(Prefabs[i]);
        sb.AppendLine("[5] Prefab: " + (Prefabs.Length - noPrefab.Count) + "/" + Prefabs.Length + (noPrefab.Count > 0 ? " | thiếu: " + string.Join(", ", noPrefab.GetRange(0, Mathf.Min(8, noPrefab.Count)).ToArray()) + "  -> Tools > King of Rush > Build ALL Prefabs" : ""));

        // 6) Sprite đạn + đầu tháp (kiểm tra đạn không bị vô hình)
        string[] bullets = { "ArrowBullet", "CannonBullet", "MageBullet", "IceBullet" };
        List<string> badBullets = new List<string>();
        for (int i = 0; i < bullets.Length; i++)
        {
            string bp = KRAssetFinder.Find(bullets[i] + ".prefab");
            GameObject bg = bp != null ? AssetDatabase.LoadAssetAtPath<GameObject>(bp) : null;
            SpriteRenderer bsr = bg != null ? bg.GetComponent<SpriteRenderer>() : null;
            if (bsr == null || bsr.sprite == null) badBullets.Add(bullets[i]);
        }
        int heads = 0;
        string[] hk = { "archer", "mage", "cannon", "barracks", "ice" };
        for (int i = 0; i < hk.Length; i++) if (KRAssetFinder.LoadSprite("tower_head_" + hk[i] + ".png") != null) heads++;
        sb.AppendLine("[6] Đạn: " + (badBullets.Count == 0 ? "tất cả có sprite" : "THIẾU sprite: " + string.Join(", ", badBullets.ToArray())) + " | Đầu tháp: " + heads + "/5 | towers_layout.json: " + (KRAssetFinder.Find("towers_layout.json") != null ? "có" : "THIẾU"));

        // 7) Scene đang mở
        sb.AppendLine(DiagnoseScene());
        return sb.ToString();
    }

    public static string DiagnoseScene()
    {
        Scene sc = SceneManager.GetActiveScene();
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("[7] Scene đang mở: " + sc.name);
        if (!sc.name.StartsWith("Level_")) { sb.AppendLine("    (mở Level_X.unity để kiểm tra scene màn chơi)"); return sb.ToString(); }

        LevelInfo info = Object.FindAnyObjectByType<LevelInfo>();
        sb.AppendLine("    LevelInfo: " + (info != null ? "có (" + info.displayName + ")" : "KHÔNG CÓ  => đây là scene CŨ, chưa được generator mới tạo lại!"));

        Tilemap[] maps = Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
        int tiles = 0;
        for (int i = 0; i < maps.Length; i++) tiles += maps[i].GetUsedTilesCount();
        sb.AppendLine("    Tilemap: " + maps.Length + " | tổng tile: " + tiles + (tiles == 0 ? "  !! không có tile" : ""));
        GameObject wp = GameObject.Find("Waypoints");
        LevelPaths lp = wp != null ? wp.GetComponent<LevelPaths>() : null;
        if (lp != null && lp.RouteCount == 0) lp.Rebuild();
        sb.AppendLine("    Waypoints/LevelPaths: " + (lp != null ? lp.RouteCount + " tuyến" : "KHÔNG CÓ LevelPaths"));
        GameObject plots = GameObject.Find("TowerPlots");
        sb.AppendLine("    Ô tháp: " + (plots != null ? plots.transform.childCount : 0));
        GameObject props = GameObject.Find("Props");
        sb.AppendLine("    Props: " + (props != null ? props.transform.childCount : 0));
        EnemySpawner sp = Object.FindAnyObjectByType<EnemySpawner>();
        if (sp != null) sb.AppendLine("    Spawner: " + (sp.enemyPrefabs != null ? sp.enemyPrefabs.Length : 0) + " loại quái | boss: " + (sp.bossSchedule != null ? sp.bossSchedule.Length : 0));
        else sb.AppendLine("    !! Không có EnemySpawner");
        return sb.ToString();
    }
}
