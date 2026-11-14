using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

/// <summary>
/// TDSceneGenerator (v4) - sinh 10 màn chơi bằng TILEMAP ISOMETRIC CHUẨN UNITY:
///   Grid_MapLayout (Isometric) -> Tilemap_Ground (nền + đường + sông/dung nham + cầu) + Tilemap_Decor (cây, đá, tinh thể)
///   + Props (lâu đài, núi lửa, cổng triệu hồi...) + Overlay hiệu ứng (màn 9)
///   + Waypoints NHIỀU TUYẾN (Route_0..Route_N, LevelPaths) + Plot tháp + Manager + HUD icon.
/// Dữ liệu đọc từ Assets/_Project/Sprites/Maps/level_XX_map.json (không còn float[][] nên không bị rỗng waypoint).
/// </summary>
public class TDSceneGenerator : EditorWindow
{
    private int singleLevel = 1;

    // ------------------------------------------------------------------ Menu
    [MenuItem("Tools/King of Rush/Generate 10 Map + ALL (MainMenu, Shop, Hero, UI)", false, 1)]
    public static void GenerateEverythingMenu()
    {
        if (!EditorUtility.DisplayDialog("King of Rush",
            "Sẽ tạo lại: sprite UI/Tile, prefab, MainMenu và Level_1 .. Level_10 (ghi đè scene cũ).\nTiếp tục?",
            "Tạo tất cả", "Huỷ")) return;
        GenerateEverything(false);
    }

    [MenuItem("Tools/King of Rush/Generate 10 Map (chỉ các màn chơi)", false, 2)]
    public static void GenerateAllLevelsMenu()
    {
        UIAssetPipeline.EnsureAll();
        EnsurePrefabs();
        List<string> errors = GenerateAllLevels();
        SetupBuildSettingsTool.SyncAllScenesToBuildSettings(true);
        EditorUtility.DisplayDialog("King of Rush", errors.Count == 0 ? "Đã tạo xong Level_1 .. Level_10." : "Có lỗi:\n- " + string.Join("\n- ", errors.ToArray()), "OK");
    }

    [MenuItem("Tools/King of Rush/Generate MainMenu + UI", false, 3)]
    public static void GenerateMenuOnlyMenu()
    {
        UIAssetPipeline.EnsureAll();
        MainMenuGenerator.GenerateMainMenuScene();
        SetupBuildSettingsTool.SyncAllScenesToBuildSettings(true);
        EditorUtility.DisplayDialog("King of Rush", "Đã tạo xong scene MainMenu (kèm Shop + Chọn tướng).", "OK");
    }

    [MenuItem("Tools/King of Rush/Generator Window", false, 20)]
    public static void ShowWindow()
    {
        GetWindow<TDSceneGenerator>("King of Rush Generator");
    }

    private void OnGUI()
    {
        GUILayout.Label("King of Rush - Generator", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        if (GUILayout.Button("Generate 10 Map + ALL (MainMenu, Shop, Hero, UI)", GUILayout.Height(34))) GenerateEverythingMenu();
        if (GUILayout.Button("Generate 10 Map (chỉ màn chơi)", GUILayout.Height(26))) GenerateAllLevelsMenu();
        if (GUILayout.Button("Generate MainMenu + UI", GUILayout.Height(26))) GenerateMenuOnlyMenu();
        EditorGUILayout.Space();
        singleLevel = EditorGUILayout.IntSlider("Màn", singleLevel, 1, 10);
        if (GUILayout.Button("Generate màn " + singleLevel, GUILayout.Height(26)))
        {
            UIAssetPipeline.EnsureAll();
            EnsurePrefabs();
            string err = GenerateLevel(singleLevel, false);
            SetupBuildSettingsTool.SyncAllScenesToBuildSettings(true);
            if (!string.IsNullOrEmpty(err)) EditorUtility.DisplayDialog("King of Rush", err, "OK");
        }
    }

    // ------------------------------------------------------------------ Batch
    public static void GenerateEverything(bool silent)
    {
        List<string> errors = new List<string>();
        try
        {
            EditorUtility.DisplayProgressBar("King of Rush", "Kiểm tra thư mục sprite...", 0.03f);
            KRAssetFinder.Invalidate();
            string plan = KRAssetFinder.FixFolders(false);
            if (!silent && !plan.StartsWith("Sẽ di chuyển 0 file") && EditorUtility.DisplayDialog("King of Rush - Dọn thư mục sprite", plan + "\n\nDọn trước khi tạo map? (khuyến nghị)", "Dọn", "Bỏ qua"))
            {
                KRAssetFinder.FixFolders(true);
            }

            EditorUtility.DisplayProgressBar("King of Rush", "Chuẩn hoá sprite UI + Tile...", 0.08f);
            UIAssetPipeline.EnsureAll();

            EditorUtility.DisplayProgressBar("King of Rush", "Dựng prefab + animation (quái, boss, hero, tháp, đạn)...", 0.15f);
            try { KRPrefabFactory.BuildAll(); }
            catch (Exception e) { errors.Add("Prefab: " + e.Message); Debug.LogException(e); }

            EditorUtility.DisplayProgressBar("King of Rush", "Tạo MainMenu (Shop + Chọn tướng)...", 0.25f);
            try { MainMenuGenerator.GenerateMainMenuScene(); }
            catch (Exception e) { errors.Add("MainMenu: " + e.Message); Debug.LogException(e); }

            errors.AddRange(GenerateAllLevels());

            SetupBuildSettingsTool.SyncAllScenesToBuildSettings(true);
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainMenu.unity");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        string msg = errors.Count == 0 ? "Hoàn tất! Đã tạo MainMenu + 10 màn chơi.\nBấm Play ở scene MainMenu để chơi." : "Hoàn tất nhưng có " + errors.Count + " lỗi:\n- " + string.Join("\n- ", errors.ToArray()) + "\n(Xem Console để biết chi tiết)";
        Debug.Log("[King of Rush] " + msg);
        if (!silent) EditorUtility.DisplayDialog("King of Rush", msg, "OK");
    }

    /// <summary>Tạo Level_1..10. Trả về danh sách lỗi (rỗng = tốt).</summary>
    public static List<string> GenerateAllLevels()
    {
        List<string> errors = new List<string>();
        try
        {
            for (int n = 1; n <= 10; n++)
            {
                EditorUtility.DisplayProgressBar("King of Rush", "Tạo Level_" + n + " ...", 0.3f + 0.65f * (n - 1) / 10f);
                string err = GenerateLevel(n, false);
                if (!string.IsNullOrEmpty(err)) errors.Add("Level_" + n + ": " + err);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
        return errors;
    }

    // ------------------------------------------------------------------ Một màn
    private static void EnsurePrefabs()
    {
        if (KRAssetFinder.Find("ArcherTower_Lv1.prefab") == null || KRAssetFinder.Find("TrollKing.prefab") == null || AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Resources/Heroes/Hero_Knight.prefab") == null)
            KRPrefabFactory.BuildAll();
    }

    public static KRMap LoadMap(int n)
    {
        string file = "level_" + n.ToString("D2") + "_map.json";
        string txt = KRAssetFinder.LoadText(file);
        if (string.IsNullOrEmpty(txt))
        {
            Debug.LogError("[King of Rush] Không tìm thấy " + file + " ở BẤT KỲ thư mục nào trong Assets/_Project (hãy copy đủ bản patch).");
            return null;
        }
        KRMap map = JsonUtility.FromJson<KRMap>(txt);
        if (map == null || map.routes == null || map.routes.Count == 0)
        {
            Debug.LogError("[King of Rush] JSON map lỗi: " + file);
            return null;
        }
        return map;
    }

    /// <summary>Tạo scene Level_n. Trả về null nếu OK, hoặc thông báo lỗi.</summary>
    public static string GenerateLevel(int n, bool setupAssets)
    {
        try
        {
            if (setupAssets) UIAssetPipeline.EnsureAll();

            KRMap map = LoadMap(n);
            if (map == null) return "không đọc được level_" + n.ToString("D2") + "_map.json";

            EnsureFolder("Assets/_Project/Scenes");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            SetupCamera();
            BuildBackgroundFallback(n);
            Tilemap ground = BuildTilemaps(map);
            BuildProps(map);
            BuildOverlay(n, map);

            LevelPaths paths = BuildWaypoints(map);
            BuildPlots(map);
            Vector3 castlePos = new Vector3(map.castle.x, map.castle.y, 0f);
            CreateCastleMarker(castlePos);
            SetupManagers(map, paths);
            SetupUI();

            // ---- Kiểm tra kết quả trước khi lưu
            List<string> problems = new List<string>();
            int tiles = ground != null ? ground.GetUsedTilesCount() : 0;
            if (tiles == 0) problems.Add("Tilemap_Ground RỖNG (0 tile) - thiếu Tile asset/sprite");
            if (paths == null || paths.RouteCount != map.routes.Count) problems.Add("số tuyến đường không khớp");
            if (map.slots.Count == 0) problems.Add("không có ô đặt tháp");
            if (KRAssetFinder.LoadSprite("build_pad.png") == null) problems.Add("thiếu sprite build_pad.png (ô đặt tháp sẽ vô hình)");

            string savePath = "Assets/_Project/Scenes/Level_" + n + ".unity";
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, savePath);
            RegisterLevelInDatabase("Level_" + n, map);

            string info = savePath + " - " + map.name + ": " + tiles + " tile, " + map.routes.Count + " tuyến, " + map.slots.Count + " ô tháp, " + map.props.Count + " prop";
            if (problems.Count > 0)
            {
                string err = string.Join("; ", problems.ToArray());
                Debug.LogError("[King of Rush] " + info + " | VẤN ĐỀ: " + err);
                return err;
            }
            Debug.Log("<color=green>[King of Rush]</color> Đã tạo " + info);
            return null;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            return "ngoại lệ: " + e.Message;
        }
    }

    // ------------------------------------------------------------------ Camera / nền
    private static Camera SetupCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            GameObject camObj = new GameObject("Main Camera");
            cam = camObj.AddComponent<Camera>();
            camObj.tag = "MainCamera";
            camObj.AddComponent<AudioListener>();
        }
        cam.orthographic = true;
        cam.orthographicSize = GameScale.ViewOrthoSize;   // 12.8 x 7.2 đơn vị = ảnh map 1280x720 @100 PPU
        cam.transform.position = new Vector3(0f, 0f, -10f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.07f, 0.07f, 0.11f, 1f);
        return cam;
    }

    /// <summary>Ảnh nền cả map đặt SAU tilemap: chỉ lộ ra nếu thiếu tile (lưới an toàn).</summary>
    private static void BuildBackgroundFallback(int n)
    {
        Sprite bg = KRAssetFinder.LoadSprite("level_" + n.ToString("D2") + "_background.png");
        if (bg == null) return;
        GameObject go = new GameObject("Map_Background_Fallback");
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = bg;
        sr.sortingOrder = -100;
    }

    // ------------------------------------------------------------------ Tilemap isometric
    private static Tilemap MakeTilemap(Transform parent, string name, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Tilemap tm = go.AddComponent<Tilemap>();
        TilemapRenderer tr = go.AddComponent<TilemapRenderer>();
        tr.mode = TilemapRenderer.Mode.Individual;
        tr.sortOrder = TilemapRenderer.SortOrder.TopRight;   // vẽ từ phía sau (trên) ra phía trước (dưới)
        tr.sortingOrder = order;
        return tm;
    }

    private static void FillTiles(Tilemap tm, List<int> xs, List<int> ys, List<int> ts, List<string> ids, Dictionary<string, TileBase> cache)
    {
        int count = xs.Count;
        if (count == 0) return;
        Vector3Int[] pos = new Vector3Int[count];
        TileBase[] tiles = new TileBase[count];
        for (int i = 0; i < count; i++)
        {
            pos[i] = new Vector3Int(xs[i], ys[i], 0);
            string id = ids[ts[i]];
            TileBase tb;
            if (!cache.TryGetValue(id, out tb))
            {
                tb = UIAssetPipeline.LoadTile(id);
                if (tb == null) Debug.LogWarning("[King of Rush] Thiếu Tile asset: " + id);
                cache[id] = tb;
            }
            tiles[i] = tb;
        }
        tm.SetTiles(pos, tiles);
    }

    private static Tilemap BuildTilemaps(KRMap map)
    {
        if (map.gx.Count == 0) return null;

        GameObject gridObj = new GameObject("Grid_MapLayout");
        Grid grid = gridObj.AddComponent<Grid>();
        grid.cellLayout = GridLayout.CellLayout.Isometric;
        grid.cellSize = new Vector3(map.cellW, map.cellH, 1f);

        Tilemap ground = MakeTilemap(gridObj.transform, "Tilemap_Ground", -30);
        Tilemap decor = MakeTilemap(gridObj.transform, "Tilemap_Decor", -20);

        Dictionary<string, TileBase> cache = new Dictionary<string, TileBase>();
        FillTiles(ground, map.gx, map.gy, map.gt, map.tileIds, cache);
        FillTiles(decor, map.dx, map.dy, map.dt, map.tileIds, cache);

        // Căn Grid: tâm ô tham chiếu phải trùng đúng vị trí world của ảnh thiết kế (độc lập với công thức nội bộ của Unity)
        gridObj.transform.position = Vector3.zero;
        Vector3Int refCell = new Vector3Int(map.gx[0], map.gy[0], 0);
        Vector3 c0 = ground.GetCellCenterWorld(refCell);
        Vector3 target = new Vector3((refCell.x - refCell.y) * map.cellW * 0.5f,
                                     (refCell.x + refCell.y) * map.cellH * 0.5f + map.gridOffsetY, 0f);
        gridObj.transform.position = target - c0;
        return ground;
    }

    // ------------------------------------------------------------------ Props / Overlay
    private static void BuildProps(KRMap map)
    {
        GameObject parent = new GameObject("Props");
        for (int i = 0; i < map.props.Count; i++)
        {
            KRProp p = map.props[i];
            Sprite sp = KRAssetFinder.LoadSprite(p.sprite + ".png");
            if (sp == null) continue;
            GameObject go = new GameObject("Prop_" + p.sprite);
            go.transform.SetParent(parent.transform, false);
            go.transform.position = new Vector3(p.x, p.y, 0f);       // sprite có pivot góc trên-trái
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sp;
            sr.sortingOrder = p.sprite.StartsWith("castle_") ? 15 : 3;
        }
    }

    private static void BuildOverlay(int n, KRMap map)
    {
        if (!map.hasOverlay) return;
        Sprite ov = KRAssetFinder.LoadSprite("level_" + n.ToString("D2") + "_overlay.png");
        if (ov == null) return;
        GameObject go = new GameObject("Map_Overlay_FX");
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = ov;
        sr.sortingOrder = 60;
    }

    // ------------------------------------------------------------------ Waypoints nhiều tuyến
    private static LevelPaths BuildWaypoints(KRMap map)
    {
        GameObject root = new GameObject("Waypoints");
        LevelPaths paths = root.AddComponent<LevelPaths>();

        for (int r = 0; r < map.routes.Count; r++)
        {
            KRRoute route = map.routes[r];
            GameObject group = new GameObject("Route_" + r);
            group.transform.SetParent(root.transform, false);

            Vector2 prev = new Vector2(float.MaxValue, float.MaxValue);
            int idx = 0;
            for (int j = 0; j < route.points.Count; j++)
            {
                Vector2 p = new Vector2(route.points[j].x, route.points[j].y);
                if (j > 0 && Vector2.Distance(prev, p) < 0.05f) continue;   // bỏ waypoint trùng vị trí
                GameObject wp = new GameObject("WP_" + idx);
                wp.transform.SetParent(group.transform, false);
                wp.transform.position = new Vector3(p.x, p.y, 0f);
                prev = p;
                idx++;
            }
        }

        GameObject spawns = new GameObject("SpawnPoints");
        for (int i = 0; i < map.spawns.Count; i++)
        {
            GameObject s = new GameObject("Spawn_" + (char)('A' + i));
            s.transform.SetParent(spawns.transform, false);
            s.transform.position = new Vector3(map.spawns[i].x, map.spawns[i].y, 0f);
        }

        paths.Rebuild();
        return paths;
    }

    // ------------------------------------------------------------------ Plot / Castle / Hero
    private static void BuildPlots(KRMap map)
    {
        Sprite padSprite = KRAssetFinder.LoadSprite("build_pad.png");
        GameObject plots = new GameObject("TowerPlots");
        for (int i = 0; i < map.slots.Count; i++)
        {
            KRSlot s = map.slots[i];
            GameObject plot = new GameObject("Plot_" + s.id);
            plot.transform.SetParent(plots.transform, false);
            plot.transform.position = new Vector3(s.x, s.y, 0f);

            CircleCollider2D col = plot.AddComponent<CircleCollider2D>();
            col.radius = 0.45f;
            col.isTrigger = false;
            plot.AddComponent<Plot>();

            GameObject visual = new GameObject("PlotVisual");
            visual.transform.SetParent(plot.transform, false);
            visual.transform.localScale = Vector3.one * GameScale.Plot;
            SpriteRenderer sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = padSprite;
            sr.sortingOrder = 5;
        }
    }

    private static void CreateCastleMarker(Vector3 pos)
    {
        GameObject castle = new GameObject("Castle_Destination_Marker");
        castle.transform.position = pos;
        BoxCollider2D col = castle.AddComponent<BoxCollider2D>();
        col.size = new Vector2(1.1f, 1.1f);
        col.isTrigger = true;
        CastleManager cm = castle.AddComponent<CastleManager>();
        cm.maxHealth = 20f;
    }

    // ------------------------------------------------------------------ Managers
    private static void SetupManagers(KRMap map, LevelPaths paths)
    {
        GameObject managers = new GameObject("GameManagers");
        LevelManager levelMgr = managers.AddComponent<LevelManager>();
        levelMgr.startingGold = map.startGold;
        levelMgr.startingLives = map.lives;
        levelMgr.levelNumber = map.level;
        levelMgr.paths = paths;
        levelMgr.totalWavesOverride = map.waves;

        if (paths != null && paths.RouteCount > 0)
        {
            levelMgr.Path = paths.GetRoute(0);
            levelMgr.StartPoint = levelMgr.Path[0];
            if (paths.RouteCount > 1)
            {
                levelMgr.Path2 = paths.GetRoute(1);
                levelMgr.StartPoint2 = levelMgr.Path2[0];
            }
        }

        LevelInfo info = managers.AddComponent<LevelInfo>();
        info.levelNumber = map.level;
        info.displayName = "Màn " + map.level + " · " + map.name;
        info.theme = map.theme;
        info.tip = map.tip;
        info.bossKey = map.boss;
        info.routeCount = map.routes.Count;
        info.slotCount = map.slots.Count;

        BuildManager buildMgr = managers.AddComponent<BuildManager>();
        managers.AddComponent<SpellManager>();
        managers.AddComponent<ObjectPool>();
        SetupBaseTowers(buildMgr);

        GameObject spawnerObj = new GameObject("EnemySpawner");
        EnemySpawner spawner = spawnerObj.AddComponent<EnemySpawner>();
        spawner.maxWaves = map.waves;
        spawner.levelHpMultiplier = map.hpMultiplier <= 0f ? 1f : map.hpMultiplier;

        List<GameObject> list = new List<GameObject>();
        for (int i = 0; i < map.enemies.Count; i++)
        {
            GameObject pf = LoadEnemyPrefab(EnemyPrefabName(map.enemies[i]));
            if (pf != null && !list.Contains(pf)) list.Add(pf);
        }
        if (list.Count == 0)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs/Enemies" });
            for (int i = 0; i < guids.Length; i++)
            {
                GameObject pf = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (pf != null && pf.name != "DragonBoss" && pf.name != "TrollKing" && pf.name != "Shaman" && pf.name != "GolemIce" && pf.name != "DemonLord") list.Add(pf);
            }
        }
        spawner.enemyPrefabs = list.ToArray();

        // Lịch Boss theo từng màn (loại, wave xuất hiện, HP chỉ định)
        List<EnemySpawner.BossSpawnEntry> schedule = new List<EnemySpawner.BossSpawnEntry>();
        for (int i = 0; i < map.bossTypes.Count; i++)
        {
            GameObject bp = LoadEnemyPrefab(BossPrefabName(map.bossTypes[i]));
            if (bp == null) { Debug.LogWarning("[King of Rush] Thiếu prefab boss: " + map.bossTypes[i]); continue; }
            EnemySpawner.BossSpawnEntry entry = new EnemySpawner.BossSpawnEntry();
            entry.prefab = bp;
            entry.wave = i < map.bossWaves.Count ? map.bossWaves[i] : map.waves;
            entry.hp = i < map.bossHps.Count ? map.bossHps[i] : 1000f;
            schedule.Add(entry);
        }
        spawner.bossSchedule = schedule.ToArray();
    }

    private static string EnemyPrefabName(string key)
    {
        switch (key)
        {
            case "goblin": return "Goblin";
            case "orc": return "Orc";
            case "goblin_archer": return "GoblinArcher";
            case "troll": return "Troll";
            case "darkelf": return "DarkElf";
            case "demon": return "Demon";
            case "golem": return "Golem";
        }
        return key;
    }

    private static string BossPrefabName(string boss)
    {
        switch (boss)
        {
            case "troll_king": return "TrollKing";
            case "shaman": return "Shaman";
            case "golem_ice": return "GolemIce";
            case "demon_lord": return "DemonLord";
            case "dragon": return "DragonBoss";
        }
        return "";
    }

    private static GameObject LoadPrefabByName(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        string p = KRAssetFinder.Find(name + ".prefab", "/Prefabs/");
        return p == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(p);
    }

    private static GameObject LoadEnemyPrefab(string name)
    {
        return LoadPrefabByName(name);
    }

    private static GameObject LoadTowerPrefab(string folder, string name)
    {
        return LoadPrefabByName(name);
    }

    private static void SetupBaseTowers(BuildManager buildMgr)
    {
        Sprite sprArcher = KRAssetFinder.LoadSprite("icon_tower_archer.png");
        Sprite sprMage = KRAssetFinder.LoadSprite("icon_tower_mage.png");
        Sprite sprCannon = KRAssetFinder.LoadSprite("icon_tower_cannon.png");
        Sprite sprBarracks = KRAssetFinder.LoadSprite("icon_tower_barracks.png");
        Sprite sprIce = KRAssetFinder.LoadSprite("icon_tower_ice.png");

        GameObject pArcher = LoadTowerPrefab("Archer", "ArcherTower_Lv1");
        GameObject pMage = LoadTowerPrefab("Mage", "MageTower_Lv1");
        GameObject pCannon = LoadTowerPrefab("Cannon", "CannonTower_Lv1");
        GameObject pBarracks = LoadTowerPrefab("Barracks", "Barracks_Lv1");
        GameObject pIce = LoadTowerPrefab("Ice", "IceTower_Lv1");

        List<Tower> list = new List<Tower>();
        if (pArcher != null) list.Add(new Tower("Tháp Cung", 70, pArcher, sprArcher));
        if (pMage != null) list.Add(new Tower("Pháp Sư", 100, pMage, sprMage));
        if (pCannon != null) list.Add(new Tower("Tháp Pháo", 125, pCannon, sprCannon));
        if (pBarracks != null) list.Add(new Tower("Trại Lính", 70, pBarracks, sprBarracks));
        if (pIce != null) list.Add(new Tower("Tháp Băng", 110, pIce, sprIce));
        if (list.Count > 0) buildMgr.Towers = list.ToArray();
    }

    // ------------------------------------------------------------------ UI
    private static void SetupUI()
    {
        GameObject canvasObj = new GameObject("Canvas_UI");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasObj.AddComponent<GraphicRaycaster>();

        if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            GameInput.EnsureCorrectInputModule(eventSystem);
        }

        // Thanh top HUD (icon) + Shop popup
        canvasObj.AddComponent<StatsUI>();
        if (UnityEngine.Object.FindAnyObjectByType<ShopUI>() == null)
            new GameObject("ShopUI").AddComponent<ShopUI>();

        // Popup xây / nâng cấp tháp
        GameObject towerUIObj = new GameObject("TowerUI_Controller", typeof(RectTransform));
        towerUIObj.transform.SetParent(canvasObj.transform, false);
        towerUIObj.AddComponent<TowerUI>();
    }

    // ------------------------------------------------------------------ Helpers
    private static Sprite TryLoadSprite(string path)
    {
        Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sp == null) Debug.LogWarning("[King of Rush] Thiếu sprite: " + path);
        return sp;
    }

    /// <summary>Giữ API cũ cho TDAutoPrefabTool và các tool khác.</summary>
    public static Sprite LoadOrCreateSprite(string assetPath, float ppu = 100f)
    {
        if (string.IsNullOrEmpty(assetPath)) return null;

        Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sp != null) return sp;

        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = ppu;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            sp = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sp != null) return sp;
        }
        return null;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace("\\", "/");
        string leaf = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    private static void RegisterLevelInDatabase(string sceneName, KRMap map)
    {
        LevelDatabase db = LevelDatabase.GetOrCreateDatabase();
        if (db == null) return;
        db.RegisterLevel(sceneName, "Màn " + map.level + ": " + map.name, map.level, map.startGold, map.lives);
        EditorUtility.SetDirty(db);
    }
}
