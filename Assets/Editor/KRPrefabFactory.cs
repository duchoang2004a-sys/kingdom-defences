using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// KRPrefabFactory: tự động dựng PREFAB + ANIMATION cho toàn bộ game (thay TDAutoPrefabTool / HeroPrefabBuilder / BulletPrefabGenerator):
///   - 7 quái thường + 5 BOSS (Troll Chúa, Shaman Dẫn Đường, Golem Băng, Demon Chúa Lửa, Rồng Hắc Ám 3 pha)
///     Boss: to hơn, chậm hơn, có BossAura (khe nứt phát sáng, vòng hào quang, đốm lửa/tuyết, giọt dung nham).
///   - 4 Hero (Resources/Heroes/Hero_{Id}.prefab) + Lính tiếp viện (Resources/Units)
///   - 15 tháp (5 loại x 3 cấp) có upgrade + đạn + animation hiệu ứng bắn
///   - Đạn (Arrow, Cannon, Mage, Ice) + VFX (nổ, tia lửa, tia sét)
/// Sprite được tìm theo TÊN FILE (KRAssetFinder) nên không phụ thuộc thư mục.
/// </summary>
public static class KRPrefabFactory
{
    private const string P = "Assets/_Project/";
    private static List<string> warnings = new List<string>();

    [MenuItem("Tools/King of Rush/Build ALL Prefabs (Enemy, Boss, Hero, Tower, Bullet, VFX)", false, 10)]
    public static void BuildAllMenu()
    {
        string r = BuildAll();
        EditorUtility.DisplayDialog("King of Rush - Prefab", r, "OK");
    }

    public static string BuildAll()
    {
        warnings = new List<string>();
        UIAssetPipeline.EnsureAll();
        KRAssetFinder.Invalidate();

        string[] folders = { "Prefabs", "Prefabs/Enemies", "Prefabs/Bosses", "Prefabs/Heroes", "Prefabs/Towers", "Prefabs/Towers/Archer", "Prefabs/Towers/Mage",
            "Prefabs/Towers/Cannon", "Prefabs/Towers/Barracks", "Prefabs/Towers/Ice", "Prefabs/Bullets", "Prefabs/VFX", "Prefabs/Units",
            "Animations", "Animations/Enemies", "Animations/Heroes", "Animations/Towers", "Animations/Bullets", "Animations/VFX", "Materials",
            "Resources", "Resources/Heroes", "Resources/Units" };
        for (int i = 0; i < folders.Length; i++) EnsureFolder(P + folders[i]);

        int enemies = 0, towers = 0;
        GameObject hit = BuildAnimVfx("HitSparkVFX", "fx_hit_spark", 18f, 0.3f, 0.8f, false);
        GameObject boom = BuildAnimVfx("ExplosionVFX", "fx_explosion", 14f, 0.55f, 1.7f, true);
        GameObject lightning = BuildLightning();
        GameObject arrow = BuildBullet("ArrowBullet", BulletType.Arrow, "proj_arrow", null, 15f, 12f, 0f, 1, 0f, hit, null, 1f);
        GameObject cball = BuildBullet("CannonBullet", BulletType.Cannonball, "proj_cannonball", null, 9f, 25f, 2.2f, 1, 0f, boom, null, 1f);
        GameObject magic = BuildBullet("MageBullet", BulletType.MageBolt, null, "proj_magic", 11f, 18f, 0f, 3, 1.5f, hit, lightning, 1f);
        GameObject ice = BuildBullet("IceBullet", BulletType.MageBolt, null, "proj_ice_shard", 10f, 8f, 0f, 1, 2f, hit, null, 1f);

        GameObject soldier = BuildSoldier();
        enemies += BuildEnemies();
        int heroes = BuildHeroes();
        towers += BuildTowers(arrow, cball, magic, ice, soldier);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string rep = "Quái + Boss: " + enemies + " | Hero: " + heroes + " | Tháp: " + towers + " prefab | Đạn: 4 | VFX: 3 | Lính: " + (soldier != null ? 1 : 0);
        if (warnings.Count > 0)
        {
            rep += "\nCảnh báo (" + warnings.Count + "):\n- " + string.Join("\n- ", warnings.GetRange(0, Mathf.Min(8, warnings.Count)).ToArray());
            for (int i = 0; i < warnings.Count; i++) Debug.LogWarning("[King of Rush] " + warnings[i]);
        }
        Debug.Log("<color=green>[King of Rush]</color> " + rep);
        return rep;
    }

    // ======================================================================= helpers
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace("\\", "/");
        string leaf = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    private static Sprite Spr(string fileName)
    {
        Sprite s = KRAssetFinder.LoadSprite(fileName);
        if (s == null) warnings.Add("Thiếu sprite: " + fileName);
        return s;
    }

    /// <summary>Đọc khung hình prefix_action_00.png, _01, ... (theo tên file, không phụ thuộc thư mục).</summary>
    private static List<Sprite> Frames(string prefix, string action)
    {
        List<Sprite> list = new List<Sprite>();
        for (int i = 0; i < 99; i++)
        {
            Sprite s = KRAssetFinder.LoadSprite(prefix + "_" + action + "_" + i.ToString("00") + ".png");
            if (s == null) break;
            list.Add(s);
        }
        if (list.Count == 0) warnings.Add("Thiếu khung hình: " + prefix + "_" + action + "_xx");
        return list;
    }

    private static List<Sprite> Numbered(string prefix)
    {
        List<Sprite> list = new List<Sprite>();
        for (int i = 0; i < 99; i++)
        {
            Sprite s = KRAssetFinder.LoadSprite(prefix + "_" + i.ToString("00") + ".png");
            if (s == null) break;
            list.Add(s);
        }
        return list;
    }

    private static AnimationClip MakeClip(string folder, string name, List<Sprite> sprites, float fps, bool loop)
    {
        string path = folder + "/" + name + ".anim";
        AssetDatabase.DeleteAsset(path);
        AnimationClip clip = new AnimationClip();
        clip.name = name;
        clip.frameRate = fps;
        if (sprites != null && sprites.Count > 0)
        {
            EditorCurveBinding binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            ObjectReferenceKeyframe[] kf = new ObjectReferenceKeyframe[sprites.Count];
            for (int i = 0; i < sprites.Count; i++)
            {
                kf[i] = new ObjectReferenceKeyframe();
                kf[i].time = i / fps;
                kf[i].value = sprites[i];
            }
            AnimationUtility.SetObjectReferenceCurve(clip, binding, kf);
        }
        AnimationClipSettings st = AnimationUtility.GetAnimationClipSettings(clip);
        st.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, st);
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    private static AnimationClip EmptyClip(string folder, string name)
    {
        string path = folder + "/" + name + ".anim";
        AssetDatabase.DeleteAsset(path);
        AnimationClip clip = new AnimationClip();
        clip.name = name;
        clip.frameRate = 12f;
        EditorCurveBinding binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        ObjectReferenceKeyframe[] kf = new ObjectReferenceKeyframe[1];
        kf[0] = new ObjectReferenceKeyframe();
        kf[0].time = 0f;
        kf[0].value = null;
        AnimationUtility.SetObjectReferenceCurve(clip, binding, kf);
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    private static AnimatorController NewController(string path)
    {
        AssetDatabase.DeleteAsset(path);
        return AnimatorController.CreateAnimatorControllerAtPath(path);
    }

    private static AnimatorState AddState(AnimatorStateMachine sm, string name, Motion clip)
    {
        AnimatorState s = sm.AddState(name);
        s.motion = clip;
        return s;
    }

    private static void AnyTo(AnimatorStateMachine sm, AnimatorState to, AnimatorConditionMode mode, string param, float threshold = 0f)
    {
        AnimatorStateTransition t = sm.AddAnyStateTransition(to);
        t.hasExitTime = false;
        t.duration = 0f;
        t.canTransitionToSelf = false;
        t.AddCondition(mode, threshold, param);
    }

    private static void Back(AnimatorState from, AnimatorState to)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.hasExitTime = true;
        t.exitTime = 0.95f;
        t.duration = 0f;
    }

    private static GameObject Save(GameObject go, string path)
    {
        GameObject asset = PrefabUtility.SaveAsPrefabAsset(go, path);
        UnityEngine.Object.DestroyImmediate(go);
        return asset;
    }

    /// <summary>Chỉ gán tag nếu tag đã tồn tại trong project (tránh lỗi "Tag: X is not defined").</summary>
    private static void TrySetTag(GameObject go, string tag)
    {
        string[] tags = UnityEditorInternal.InternalEditorUtility.tags;
        if (Array.IndexOf(tags, tag) < 0) return;
        go.tag = tag;
    }

    private static Sprite shadowSprite;
    private static void AddShadow(GameObject root, float localWidth, float yOffset, int order)
    {
        if (shadowSprite == null) shadowSprite = KRAssetFinder.LoadSprite("HeroShadow.png");
        if (shadowSprite == null) return;
        GameObject g = new GameObject("Shadow");
        g.transform.SetParent(root.transform, false);
        SpriteRenderer sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = shadowSprite;
        sr.color = new Color(0f, 0f, 0f, 0.35f);
        sr.sortingOrder = order;
        float w = shadowSprite.bounds.size.x;
        g.transform.localScale = Vector3.one * (w > 0.001f ? localWidth / w : 1f);
        g.transform.localPosition = new Vector3(0f, yOffset, 0f);
    }

    // ======================================================================= VFX / bullets
    private static GameObject BuildAnimVfx(string name, string framePrefix, float fps, float life, float scale, bool explosion)
    {
        List<Sprite> frames = Numbered(framePrefix);
        string animDir = P + "Animations/VFX";
        AnimationClip clip = MakeClip(animDir, name, frames, fps, false);
        AnimatorController ctrl = NewController(animDir + "/" + name + ".controller");
        AnimatorState st = AddState(ctrl.layers[0].stateMachine, "Play", clip);
        ctrl.layers[0].stateMachine.defaultState = st;

        GameObject go = new GameObject(name);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = frames.Count > 0 ? frames[0] : null;
        sr.sortingOrder = 20;
        go.transform.localScale = Vector3.one * scale;
        Animator an = go.AddComponent<Animator>();
        an.runtimeAnimatorController = ctrl;
        // ExplosionEffect tự quản vòng đời và tự TRẢ VỀ POOL; không được thêm SpriteAnimVFX (sẽ tranh nhau trả về pool)
        if (explosion) go.AddComponent<ExplosionEffect>();
        else { SpriteAnimVFX v = go.AddComponent<SpriteAnimVFX>(); v.lifetime = life; }
        return Save(go, P + "Prefabs/VFX/" + name + ".prefab");
    }

    private static GameObject BuildLightning()
    {
        string matPath = P + "Materials/KR_Lightning.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            Shader sh = Shader.Find("Sprites/Default");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            mat = new Material(sh);
            AssetDatabase.CreateAsset(mat, matPath);
        }
        GameObject go = new GameObject("LightningChainVFX");
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.material = mat;
        lr.startWidth = 0.09f;
        lr.endWidth = 0.05f;
        lr.startColor = new Color(0.85f, 0.65f, 1f, 1f);
        lr.endColor = new Color(0.5f, 0.8f, 1f, 1f);
        lr.positionCount = 2;
        lr.sortingOrder = 22;
        go.AddComponent<LightningChainVFX>();
        return Save(go, P + "Prefabs/VFX/LightningChainVFX.prefab");
    }

    private static GameObject BuildBullet(string name, BulletType type, string singleSprite, string framePrefix, float speed, float dmg,
        float aoe, int chain, float slow, GameObject impact, GameObject lightning, float scale)
    {
        GameObject go = new GameObject(name);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 15;
        List<Sprite> frames = framePrefix != null ? Numbered(framePrefix) : null;
        if (singleSprite != null) sr.sprite = Spr(singleSprite + ".png");
        else if (frames != null && frames.Count > 0) sr.sprite = frames[0];
        if (sr.sprite == null) sr.sprite = FallbackSprite("proj_fallback_" + name + ".png", name == "ArrowBullet" ? 40 : 28, name == "ArrowBullet" ? 10 : 28, name);
        go.transform.localScale = Vector3.one * scale;

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        CircleCollider2D col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.12f;

        Bullet b = go.AddComponent<Bullet>();
        b.bulletType = type;
        b.bulletSpeed = speed;
        b.bulletDamage = dmg;
        b.aoeRadius = aoe > 0f ? aoe : 2.0f;
        b.maxChainTargets = chain;
        b.chainRadius = 2.0f;
        b.slowDuration = slow;
        b.impactVfxPrefab = impact;
        b.lightningVfxPrefab = lightning;
        b.enemyMask = ~0;

        // Vệt sáng đuôi đạn: bay ra là THẤY NGAY, dù sprite nhỏ
        Color tc; float tt, tw;
        if (name == "ArrowBullet") { tc = new Color(1f, 0.95f, 0.75f, 0.9f); tt = 0.16f; tw = 0.07f; }
        else if (name == "CannonBullet") { tc = new Color(0.75f, 0.75f, 0.8f, 0.8f); tt = 0.28f; tw = 0.15f; }
        else if (name == "MageBullet") { tc = new Color(0.85f, 0.55f, 1f, 0.9f); tt = 0.24f; tw = 0.13f; }
        else { tc = new Color(0.6f, 0.9f, 1f, 0.9f); tt = 0.22f; tw = 0.11f; }
        TrailRenderer tr = go.AddComponent<TrailRenderer>();
        tr.material = TrailMaterial();
        tr.time = tt;
        tr.startWidth = tw;
        tr.endWidth = 0f;
        tr.startColor = tc;
        tr.endColor = new Color(tc.r, tc.g, tc.b, 0f);
        tr.minVertexDistance = 0.03f;
        tr.sortingOrder = 14;

        if (frames != null && frames.Count > 1)
        {
            AnimationClip clip = MakeClip(P + "Animations/Bullets", name, frames, 12f, true);
            AnimatorController ctrl = NewController(P + "Animations/Bullets/" + name + ".controller");
            AnimatorState st = AddState(ctrl.layers[0].stateMachine, "Fly", clip);
            ctrl.layers[0].stateMachine.defaultState = st;
            go.AddComponent<Animator>().runtimeAnimatorController = ctrl;
        }
        return Save(go, P + "Prefabs/Bullets/" + name + ".prefab");
    }

    private static Material TrailMaterial()
    {
        string matPath = P + "Materials/KR_Trail.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            Shader sh = Shader.Find("Sprites/Default");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            mat = new Material(sh);
            AssetDatabase.CreateAsset(mat, matPath);
        }
        return mat;
    }

    /// <summary>Sprite dự phòng (hình bầu dục sáng) nếu thiếu sprite đạn - để đạn không bao giờ vô hình.</summary>
    private static Sprite FallbackSprite(string file, int w, int h, string name)
    {
        string rel = P + "Sprites/Projectiles/" + file;
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(rel);
        if (existing != null) return existing;
        EnsureFolder(P + "Sprites/Projectiles");
        Color c = name == "ArrowBullet" ? new Color(0.9f, 0.75f, 0.5f) : (name == "CannonBullet" ? new Color(0.35f, 0.36f, 0.45f) : (name == "MageBullet" ? new Color(0.8f, 0.5f, 1f) : new Color(0.6f, 0.9f, 1f)));
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float nx = (x + 0.5f) / w * 2f - 1f, ny = (y + 0.5f) / h * 2f - 1f;
                tex.SetPixel(x, y, nx * nx + ny * ny <= 1f ? c : new Color(0, 0, 0, 0));
            }
        File.WriteAllBytes(Path.GetFullPath(rel), tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(rel);
        TextureImporter ti = AssetImporter.GetAtPath(rel) as TextureImporter;
        if (ti != null)
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.spritePixelsPerUnit = 100f;
            ti.filterMode = FilterMode.Point;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.SaveAndReimport();
        }
        warnings.Add("Thiếu sprite đạn -> dùng sprite dự phòng: " + file);
        return AssetDatabase.LoadAssetAtPath<Sprite>(rel);
    }

    // ======================================================================= soldier
    private static AnimatorController CharController(string animDir, string name, string prefix, bool withIdleStateName)
    {
        List<Sprite> idle = Frames(prefix, "idle");
        List<Sprite> walk = Frames(prefix, "walk");
        List<Sprite> attack = Frames(prefix, "attack");
        List<Sprite> skill = Frames(prefix, "skill");
        List<Sprite> hurt = Frames(prefix, "hurt");
        List<Sprite> die = Frames(prefix, "die");

        AnimatorController c = NewController(animDir + "/" + name + ".controller");
        c.AddParameter("Speed", AnimatorControllerParameterType.Float);
        c.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        c.AddParameter("Skill1", AnimatorControllerParameterType.Trigger);
        c.AddParameter("Skill2", AnimatorControllerParameterType.Trigger);
        c.AddParameter("Hurt", AnimatorControllerParameterType.Trigger);
        c.AddParameter("Die", AnimatorControllerParameterType.Trigger);
        c.AddParameter("IsDead", AnimatorControllerParameterType.Bool);

        AnimatorStateMachine sm = c.layers[0].stateMachine;
        AnimatorState sIdle = AddState(sm, "Idle", MakeClip(animDir, name + "_Idle", idle, 6f, true));
        AnimatorState sWalk = AddState(sm, "Walk", MakeClip(animDir, name + "_Walk", walk, 10f, true));
        AnimatorState sAtk = AddState(sm, "Attack", MakeClip(animDir, name + "_Attack", attack, 14f, false));
        AnimatorState sS1 = AddState(sm, "Skill1", MakeClip(animDir, name + "_Skill1", attack, 12f, false));
        AnimatorState sS2 = AddState(sm, "Skill2", MakeClip(animDir, name + "_Skill2", skill.Count > 0 ? skill : attack, 10f, false));
        AnimatorState sHurt = AddState(sm, "Hurt", MakeClip(animDir, name + "_Hurt", hurt, 10f, false));
        AnimatorState sDie = AddState(sm, "Die", MakeClip(animDir, name + "_Die", die, 8f, false));
        sm.defaultState = sIdle;

        AnimatorStateTransition t1 = sIdle.AddTransition(sWalk); t1.hasExitTime = false; t1.duration = 0f; t1.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        AnimatorStateTransition t2 = sWalk.AddTransition(sIdle); t2.hasExitTime = false; t2.duration = 0f; t2.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
        AnyTo(sm, sAtk, AnimatorConditionMode.If, "Attack");
        AnyTo(sm, sS1, AnimatorConditionMode.If, "Skill1");
        AnyTo(sm, sS2, AnimatorConditionMode.If, "Skill2");
        AnyTo(sm, sHurt, AnimatorConditionMode.If, "Hurt");
        AnyTo(sm, sDie, AnimatorConditionMode.If, "Die");
        AnyTo(sm, sDie, AnimatorConditionMode.If, "IsDead");
        Back(sAtk, sIdle); Back(sS1, sIdle); Back(sS2, sIdle); Back(sHurt, sIdle);
        return c;
    }

    private static GameObject BuildSoldier()
    {
        string prefix = "hero_knight";
        AnimatorController ctrl = CharController(P + "Animations/Heroes", "Soldier", prefix, true);
        GameObject root = new GameObject("ReinforcementSoldier");
        TrySetTag(root, "Player");
        Rigidbody2D rb = root.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        CircleCollider2D col = root.AddComponent<CircleCollider2D>();
        col.radius = 0.3f;
        col.isTrigger = true;
        ReinforcementSoldier soldier = root.AddComponent<ReinforcementSoldier>();

        GameObject vis = new GameObject("Visual");
        vis.transform.SetParent(root.transform, false);
        vis.transform.localScale = Vector3.one * 1.0f * GameScale.Soldier;
        SpriteRenderer sr = vis.AddComponent<SpriteRenderer>();
        List<Sprite> idle = Frames(prefix, "idle");
        sr.sprite = idle.Count > 0 ? idle[0] : null;
        sr.sortingOrder = 9;
        sr.color = new Color(0.82f, 0.92f, 1f, 1f);
        Animator an = vis.AddComponent<Animator>();
        an.runtimeAnimatorController = ctrl;
        AddShadow(root, 0.45f, 0f, 8);

        soldier.animator = an;
        soldier.spriteRenderer = sr;
        GameObject saved = Save(root, P + "Resources/Units/ReinforcementSoldier.prefab");
        return saved;
    }

    // ======================================================================= heroes
    private static int BuildHeroes()
    {
        string[,] heroes = { { "Knight", "hero_knight" }, { "Ranger", "hero_ranger" }, { "Mage", "hero_mage" }, { "Paladin", "hero_knight" } };
        int n = 0;
        for (int i = 0; i < 4; i++)
        {
            string id = heroes[i, 0];
            string prefix = heroes[i, 1];
            AnimatorController ctrl = CharController(P + "Animations/Heroes", "Hero_" + id, prefix, true);

            GameObject root = new GameObject("Hero_" + id);
            TrySetTag(root, "Player");
            Rigidbody2D rb = root.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            CircleCollider2D col = root.AddComponent<CircleCollider2D>();
            col.radius = 0.4f;
            col.isTrigger = true;
            Hero hero = root.AddComponent<Hero>();
            HeroController hc = root.AddComponent<HeroController>();
            hc.hero = hero;

            GameObject vis = new GameObject("Visual");
            vis.transform.SetParent(root.transform, false);
            // GameScale.Hero được áp ĐÚNG MỘT LẦN lúc chạy lên root (HeroController.EnsureHero) -> không nhân ở đây nữa (trước đây thu nhỏ 2 lần).
            vis.transform.localScale = Vector3.one * 1.25f;
            SpriteRenderer sr = vis.AddComponent<SpriteRenderer>();
            List<Sprite> idle = Frames(prefix, "idle");
            sr.sprite = idle.Count > 0 ? idle[0] : null;
            sr.sortingOrder = 10;
            if (id == "Paladin") sr.color = new Color(1f, 0.86f, 0.5f, 1f);
            Animator an = vis.AddComponent<Animator>();
            an.runtimeAnimatorController = ctrl;
            AddShadow(root, 0.6f, 0f, 9);

            hero.heroId = id;
            hero.animator = an;
            hero.spriteRenderer = sr;
            Save(root, P + "Resources/Heroes/Hero_" + id + ".prefab");
            n++;
        }
        return n;
    }

    // ======================================================================= enemies & bosses
    private class AuraSpec
    {
        public string ring = "lava";
        public Color fxColor = Color.white;
        public Color ringColor = new Color(1f, 1f, 1f, 0.9f);
        public Color emberColor = new Color(1f, 0.7f, 0.25f, 1f);
        public float ringScale = 1.2f;
        public Vector2 emberVel = new Vector2(0f, 0.7f);
        public float emberW = 0.5f, emberH = 0.9f;
        public int embers = 10;
        public bool drips = false;
        public float pulse = 2.2f;
        public Vector2 flare = new Vector2(3f, 6f);
    }

    private class EnemySpec
    {
        public string prefab, prefix, display;
        public float hp = 40f, speed = 1.5f, scale = 1.3f, armor = 0f, magicRes = 0f, statusRes = 0f, damage = 8f;
        public int reward = 15;
        public bool boss;
        public int tier = 1;
        public string minion;
        public int minionCount = 0;
        public AuraSpec aura;
    }

    private static List<EnemySpec> Specs()
    {
        List<EnemySpec> l = new List<EnemySpec>();
        l.Add(new EnemySpec { prefab = "Goblin", prefix = "enemy_goblin", display = "Goblin", hp = 40, speed = 1.9f, scale = 1.3f, reward = 15, damage = 6 });
        l.Add(new EnemySpec { prefab = "GoblinArcher", prefix = "enemy_goblin_archer", display = "Goblin Archer", hp = 35, speed = 1.6f, scale = 1.3f, reward = 18, damage = 7 });
        l.Add(new EnemySpec { prefab = "Orc", prefix = "enemy_orc", display = "Orc", hp = 90, speed = 1.4f, scale = 1.45f, reward = 25, armor = 0.15f, damage = 10 });
        l.Add(new EnemySpec { prefab = "DarkElf", prefix = "enemy_darkelf", display = "Dark Elf", hp = 70, speed = 1.9f, scale = 1.35f, reward = 30, magicRes = 0.3f, damage = 9 });
        l.Add(new EnemySpec { prefab = "Demon", prefix = "enemy_demon", display = "Demon", hp = 120, speed = 2.2f, scale = 1.45f, reward = 35, magicRes = 0.2f, damage = 12 });
        l.Add(new EnemySpec { prefab = "Troll", prefix = "enemy_troll", display = "Troll", hp = 260, speed = 1.1f, scale = 1.1f, reward = 60, armor = 0.25f, damage = 16 });
        l.Add(new EnemySpec { prefab = "Golem", prefix = "enemy_golem", display = "Golem", hp = 450, speed = 0.75f, scale = 1.0f, reward = 90, armor = 0.5f, statusRes = 0.3f, damage = 22 });

        // ---- BOSS: to hơn quái thường (scale lớn), di chuyển chậm hơn, có BossAura
        l.Add(new EnemySpec
        {
            prefab = "TrollKing", prefix = "enemy_troll_king", display = "Troll Chúa", hp = 1200, speed = 0.7f, scale = 1.5f, reward = 250, armor = 0.2f, damage = 25,
            boss = true, tier = 2, minion = "Orc", minionCount = 3,
            aura = new AuraSpec { ring = "rage", fxColor = new Color(1f, 0.85f, 0.8f, 1f), ringColor = new Color(1f, 1f, 1f, 0.95f), emberColor = new Color(1f, 0.4f, 0.25f, 1f), ringScale = 1.1f, embers = 12, pulse = 3.0f }
        });
        l.Add(new EnemySpec
        {
            prefab = "Shaman", prefix = "enemy_shaman", display = "Shaman Dẫn Đường", hp = 3000, speed = 0.75f, scale = 1.4f, reward = 300, magicRes = 0.2f, damage = 0,
            boss = true, tier = 2,
            aura = new AuraSpec { ring = "rune", fxColor = new Color(0.85f, 1f, 0.9f, 1f), ringScale = 1.0f, emberColor = new Color(0.6f, 1f, 0.75f, 1f), emberVel = new Vector2(0f, 0.5f), embers = 14, pulse = 1.8f, flare = new Vector2(2.5f, 4.5f) }
        });
        l.Add(new EnemySpec
        {
            prefab = "GolemIce", prefix = "enemy_golem_ice", display = "Golem Băng", hp = 3300, speed = 0.45f, scale = 1.35f, reward = 350, armor = 0.6f, statusRes = 0.5f, damage = 30,
            boss = true, tier = 2,
            aura = new AuraSpec { ring = "frost", fxColor = new Color(0.85f, 0.97f, 1f, 1f), ringScale = 1.15f, emberColor = new Color(0.85f, 0.95f, 1f, 1f), emberVel = new Vector2(0f, -0.35f), emberH = 1.2f, embers = 14, pulse = 1.5f }
        });
        l.Add(new EnemySpec
        {
            prefab = "DemonLord", prefix = "enemy_demon_lord", display = "Demon Chúa Lửa", hp = 2500, speed = 1.0f, scale = 1.5f, reward = 400, magicRes = 0.15f, damage = 35,
            boss = true, tier = 2,
            aura = new AuraSpec { ring = "lava", fxColor = Color.white, ringScale = 1.2f, drips = true, embers = 14, pulse = 2.6f, flare = new Vector2(2f, 4f) }
        });
        return l;
    }

    private static int BuildEnemies()
    {
        int n = 0;
        List<EnemySpec> specs = Specs();
        for (int i = 0; i < specs.Count; i++) { BuildEnemy(specs[i], specs); n++; }
        BuildDragon();
        return n + 1;
    }

    private static void SetupAura(GameObject root, SpriteRenderer body, AuraSpec a, List<string> fxPrefixes, float ringYOffset)
    {
        BossAura aura = root.AddComponent<BossAura>();
        aura.body = body;

        List<Sprite> fx = new List<Sprite>();
        for (int p = 0; p < fxPrefixes.Count; p++)
        {
            string prefix = fxPrefixes[p];
            string[] actions = { "idle", "walk", "attack", "skill", "hurt", "die", "fly", "breath" };
            for (int k = 0; k < actions.Length; k++)
                for (int i = 0; i < 99; i++)
                {
                    Sprite s = KRAssetFinder.LoadSprite(prefix + "_" + actions[k] + "_" + i.ToString("00") + "_fx.png");
                    if (s == null) break;
                    fx.Add(s);
                }
        }
        if (fx.Count == 0) warnings.Add("Chưa có sprite khe nứt (_fx) cho " + root.name);
        aura.fxFrames = fx.ToArray();
        aura.fxColor = a.fxColor;
        aura.pulseSpeed = a.pulse;
        aura.flareInterval = a.flare;

        List<Sprite> ring = Numbered("aura_" + a.ring + "_ring");
        aura.ringFrames = ring.ToArray();
        aura.ringColor = a.ringColor;
        aura.ringScale = a.ringScale;
        aura.ringOffset = new Vector3(0f, ringYOffset, 0f);

        aura.emberSprite = KRAssetFinder.LoadSprite("aura_ember.png");
        aura.emberCount = a.embers;
        aura.emberColor = a.emberColor;
        aura.emberVelocity = a.emberVel;
        aura.emberWidth = a.emberW;
        aura.emberHeight = a.emberH;

        aura.enableDrips = a.drips;
        if (a.drips)
        {
            aura.dripFrames = Numbered("aura_drip").ToArray();
            aura.puddleFrames = Numbered("aura_puddle").ToArray();
        }
    }

    private static GameObject BuildEnemy(EnemySpec s, List<EnemySpec> all)
    {
        string animDir = P + "Animations/Enemies";
        List<Sprite> idle = Frames(s.prefix, "idle");
        List<Sprite> walk = Frames(s.prefix, "walk");
        List<Sprite> attack = Frames(s.prefix, "attack");
        List<Sprite> hurt = Frames(s.prefix, "hurt");
        List<Sprite> die = Frames(s.prefix, "die");

        AnimatorController c = NewController(animDir + "/" + s.prefab + ".controller");
        c.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
        c.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        c.AddParameter("Hurt", AnimatorControllerParameterType.Trigger);
        c.AddParameter("IsDead", AnimatorControllerParameterType.Bool);
        AnimatorStateMachine sm = c.layers[0].stateMachine;
        AnimatorState sWalk = AddState(sm, "Walk", MakeClip(animDir, s.prefab + "_Walk", walk, 10f, true));
        AnimatorState sIdle = AddState(sm, "Idle", MakeClip(animDir, s.prefab + "_Idle", idle, 6f, true));
        AnimatorState sAtk = AddState(sm, "Attack", MakeClip(animDir, s.prefab + "_Attack", attack, 14f, false));
        AnimatorState sHurt = AddState(sm, "Hurt", MakeClip(animDir, s.prefab + "_Hurt", hurt, 10f, false));
        AnimatorState sDie = AddState(sm, "Die", MakeClip(animDir, s.prefab + "_Die", die, 8f, false));
        sm.defaultState = sWalk;
        AnyTo(sm, sAtk, AnimatorConditionMode.If, "Attack");
        AnyTo(sm, sHurt, AnimatorConditionMode.If, "Hurt");
        AnyTo(sm, sDie, AnimatorConditionMode.If, "IsDead");
        Back(sAtk, sWalk); Back(sHurt, sWalk);

        GameObject go = new GameObject(s.prefab);
        go.transform.localScale = Vector3.one * s.scale * (s.boss ? GameScale.Boss : GameScale.Enemy);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = walk.Count > 0 ? walk[0] : null;
        sr.sortingOrder = 5;
        go.AddComponent<Animator>().runtimeAnimatorController = c;

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        CircleCollider2D col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = s.boss ? 0.45f : 0.3f;
        col.offset = new Vector2(0f, 0.25f);

        Enemy e = go.AddComponent<Enemy>();
        go.AddComponent<EnemyHealth>();
        go.AddComponent<EnemyMovement>();
        go.AddComponent<StatusEffectController>();

        e.enemyName = s.boss ? s.display + " Boss" : s.display;
        e.goldReward = s.reward;
        e.maxHealth = s.hp;
        e.moveSpeed = s.speed;
        e.armor = s.armor;
        e.magicResist = s.magicRes;
        e.statusResist = s.statusRes;
        e.combatDamage = s.damage;
        e.isBoss = s.boss;
        e.bossTier = s.tier;
        if (!string.IsNullOrEmpty(s.minion))
        {
            string mp = P + "Prefabs/Enemies/" + s.minion + ".prefab";
            e.minionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(mp);
            e.minionCount = s.minionCount;
        }

        AddShadow(go, s.boss ? 0.55f : 0.4f, 0f, 4);
        if (s.boss && s.aura != null)
        {
            List<string> fxp = new List<string>();
            fxp.Add(s.prefix);
            SetupAura(go, sr, s.aura, fxp, 0.02f);
        }

        string folder = s.boss ? "Prefabs/Bosses/" : "Prefabs/Enemies/";
        GameObject saved = Save(go, P + folder + s.prefab + ".prefab");
        // Boss cũng đặt thêm một bản trong Prefabs/Enemies để các tool cũ tìm thấy
        if (s.boss) AssetDatabase.CopyAsset(P + folder + s.prefab + ".prefab", P + "Prefabs/Enemies/" + s.prefab + ".prefab");
        return saved;
    }

    private static void BuildDragon()
    {
        string animDir = P + "Animations/Enemies";
        string name = "DragonBoss";
        AnimatorController c = NewController(animDir + "/" + name + ".controller");
        c.AddParameter("Phase", AnimatorControllerParameterType.Int);
        c.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        c.AddParameter("Hurt", AnimatorControllerParameterType.Trigger);
        c.AddParameter("IsDead", AnimatorControllerParameterType.Bool);
        AnimatorStateMachine sm = c.layers[0].stateMachine;

        string p1 = "enemy_dragon_phase1", p2 = "enemy_dragon_phase2", p3 = "enemy_dragon_phase3";
        AnimatorState sF1 = AddState(sm, "Fly1", MakeClip(animDir, name + "_Fly1", Frames(p1, "fly"), 8f, true));
        AnimatorState sF2 = AddState(sm, "Fly2", MakeClip(animDir, name + "_Fly2", Frames(p2, "fly"), 9f, true));
        AnimatorState sF3 = AddState(sm, "Fly3", MakeClip(animDir, name + "_Fly3", Frames(p3, "fly"), 10f, true));
        AnimatorState sBr = AddState(sm, "Breath", MakeClip(animDir, name + "_Breath", Frames(p1, "breath"), 10f, false));
        AnimatorState sHurt = AddState(sm, "Hurt", MakeClip(animDir, name + "_Hurt", Frames(p1, "hurt"), 10f, false));
        AnimatorState sDie = AddState(sm, "Die", MakeClip(animDir, name + "_Die", Frames(p1, "die"), 8f, false));
        sm.defaultState = sF1;

        AnimatorStateTransition t12 = sF1.AddTransition(sF2); t12.hasExitTime = false; t12.duration = 0f; t12.AddCondition(AnimatorConditionMode.Greater, 1f, "Phase");
        AnimatorStateTransition t23 = sF2.AddTransition(sF3); t23.hasExitTime = false; t23.duration = 0f; t23.AddCondition(AnimatorConditionMode.Greater, 2f, "Phase");
        AnimatorStateTransition t21 = sF2.AddTransition(sF1); t21.hasExitTime = false; t21.duration = 0f; t21.AddCondition(AnimatorConditionMode.Less, 2f, "Phase");
        AnimatorStateTransition t32 = sF3.AddTransition(sF2); t32.hasExitTime = false; t32.duration = 0f; t32.AddCondition(AnimatorConditionMode.Less, 3f, "Phase");
        AnyTo(sm, sBr, AnimatorConditionMode.If, "Attack");
        AnyTo(sm, sHurt, AnimatorConditionMode.If, "Hurt");
        AnyTo(sm, sDie, AnimatorConditionMode.If, "IsDead");
        Back(sBr, sF1); Back(sHurt, sF1);

        GameObject go = new GameObject(name);
        go.transform.localScale = Vector3.one * 1.35f * GameScale.Boss;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        List<Sprite> f1 = Frames(p1, "fly");
        sr.sprite = f1.Count > 0 ? f1[0] : null;
        sr.sortingOrder = 8;
        go.AddComponent<Animator>().runtimeAnimatorController = c;

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        CircleCollider2D col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.7f;

        Enemy e = go.AddComponent<Enemy>();
        go.AddComponent<EnemyHealth>();
        go.AddComponent<EnemyMovement>();
        go.AddComponent<StatusEffectController>();
        DragonBoss dragon = go.AddComponent<DragonBoss>();
        go.AddComponent<DragonPhaseAnimator>();

        e.enemyName = "Dragon Boss";
        e.isBoss = true;
        e.bossTier = 3;
        e.goldReward = 500;
        e.maxHealth = 3000f;
        e.moveSpeed = 0.9f;
        e.magicResist = 0.1f;
        e.combatDamage = 40f;
        dragon.maxHealth = 3000f;
        dragon.baseMoveSpeed = 0.9f;

        AddShadow(go, 0.9f, -0.55f, 4);

        AuraSpec a = new AuraSpec { ring = "void", fxColor = Color.white, ringScale = 1.5f, drips = true, embers = 18, emberW = 0.9f, emberH = 1.0f, pulse = 2.4f, flare = new Vector2(2f, 3.5f) };
        List<string> fxp = new List<string> { p1, p2, p3 };
        SetupAura(go, sr, a, fxp, -0.5f);
        Save(go, P + "Prefabs/Bosses/" + name + ".prefab");
        AssetDatabase.DeleteAsset(P + "Prefabs/Enemies/" + name + ".prefab");
        AssetDatabase.CopyAsset(P + "Prefabs/Bosses/" + name + ".prefab", P + "Prefabs/Enemies/" + name + ".prefab");
    }

    // ======================================================================= towers
    private static TowerUpgrade U(string n, int cost, float dmg, float range, float rate, int bullets = 1, float aoe = 0f, float slow = 0f, float bscale = 1f)
    {
        TowerUpgrade u = new TowerUpgrade();
        u.levelName = n; u.cost = cost; u.damage = dmg; u.range = range; u.fireRate = rate;
        u.bulletsPerShot = bullets; u.aoeRadius = aoe; u.slowDuration = slow; u.bulletScale = bscale;
        return u;
    }

    private static TowerUpgrade UB(string n, int cost, int count, float hp, float armor, float respawn)
    {
        TowerUpgrade u = new TowerUpgrade();
        u.levelName = n; u.cost = cost; u.damage = 0f; u.range = 3.2f; u.fireRate = 1f;
        u.soldierCount = count; u.soldierHP = hp; u.soldierArmor = armor; u.soldierRespawnTime = respawn;
        return u;
    }

    [Serializable] private class TLEntry { public string key; public int level; public float headY; }
    [Serializable] private class TLayout { public List<TLEntry> towers = new List<TLEntry>(); }
    private static TLayout layoutCache;

    private static float HeadY(string key, int lvl)
    {
        if (layoutCache == null)
        {
            string txt = KRAssetFinder.LoadText("towers_layout.json");
            layoutCache = !string.IsNullOrEmpty(txt) ? JsonUtility.FromJson<TLayout>(txt) : new TLayout();
        }
        for (int i = 0; i < layoutCache.towers.Count; i++)
            if (layoutCache.towers[i].key == key && layoutCache.towers[i].level == lvl) return layoutCache.towers[i].headY;
        warnings.Add("Thiếu towers_layout.json cho " + key + " cấp " + lvl);
        return 0.9f;
    }

    private static Sprite[] BuildParts(string key)
    {
        string[] names;
        switch (key)
        {
            case "archer": names = new[] { "build_stone", "build_plank", "tower_head_archer", "proj_arrow" }; break;
            case "cannon": names = new[] { "build_stone", "build_wheel", "tower_head_cannon", "build_bomb" }; break;
            case "mage": names = new[] { "build_stone", "build_book", "tower_head_mage", "proj_magic_00" }; break;
            case "barracks": names = new[] { "build_stone", "build_sword", "build_shield", "tower_head_barracks" }; break;
            default: names = new[] { "build_stone", "build_snowflake", "tower_head_ice", "proj_ice_shard_00" }; break;
        }
        List<Sprite> list = new List<Sprite>();
        for (int i = 0; i < names.Length; i++)
        {
            Sprite s = KRAssetFinder.LoadSprite(names[i] + ".png");
            if (s != null) list.Add(s);
        }
        return list.ToArray();
    }

    private static int BuildTowers(GameObject arrow, GameObject cball, GameObject magic, GameObject ice, GameObject soldier)
    {
        int n = 0;
        string[,] types = {
            { "archer", "ArcherTower", "Archer", "Tháp Cung", "100" },
            { "mage", "MageTower", "Mage", "Pháp Sư", "100" },
            { "cannon", "CannonTower", "Cannon", "Tháp Pháo", "125" },
            { "barracks", "Barracks", "Barracks", "Trại Lính", "70" },
            { "ice", "IceTower", "Ice", "Tháp Băng", "110" } };

        for (int t = 0; t < 5; t++)
        {
            string key = types[t, 0], pname = types[t, 1], folder = types[t, 2], disp = types[t, 3];
            int cost = int.Parse(types[t, 4]);

            TowerUpgrade[] ups;
            GameObject bullet = null;
            string fxPrefix = "fx_hit_spark";
            if (key == "archer") { ups = new[] { U("Cấp 1", cost, 12, 3.6f, 1.6f), U("Cấp 2", 75, 22, 4.2f, 2.0f), U("Cấp 3", 140, 35, 4.8f, 2.4f, 2) }; bullet = arrow; }
            else if (key == "mage") { ups = new[] { U("Cấp 1", cost, 22, 3.4f, 0.95f), U("Cấp 2", 80, 40, 4.0f, 1.25f, 1, 0f, 1.0f), U("Cấp 3", 150, 65, 4.6f, 1.5f, 1, 0f, 2.5f) }; bullet = magic; fxPrefix = "proj_magic"; }
            else if (key == "cannon") { ups = new[] { U("Cấp 1", cost, 30, 3.2f, 0.65f, 1, 2.0f), U("Cấp 2", 90, 55, 3.8f, 0.8f, 1, 2.6f), U("Cấp 3", 160, 95, 4.4f, 0.95f, 1, 3.8f, 0f, 1.5f) }; bullet = cball; }
            else if (key == "ice") { ups = new[] { U("Cấp 1", cost, 8, 3.2f, 0.8f, 1, 0f, 1.5f), U("Cấp 2", 85, 14, 3.8f, 1.0f, 1, 0f, 2.2f), U("Cấp 3", 150, 24, 4.4f, 1.2f, 1, 0f, 3.0f) }; bullet = ice; fxPrefix = "proj_ice_shard"; }
            else { ups = new[] { UB("Cấp 1", cost, 2, 120f, 0.1f, 10f), UB("Cấp 2", 110, 3, 180f, 0.15f, 8f), UB("Cấp 3", 180, 3, 260f, 0.2f, 6f) }; }

            // Animation hiệu ứng bắn (FX con): Idle (ẩn) -> Shoot (phát khung hình) -> Idle
            string animDir = P + "Animations/Towers";
            List<Sprite> shootFrames = Numbered(fxPrefix);
            AnimatorController fxCtrl = null;
            if (key != "barracks")
            {
                fxCtrl = NewController(animDir + "/" + pname + "_FX.controller");
                fxCtrl.AddParameter("Shoot", AnimatorControllerParameterType.Trigger);
                AnimatorStateMachine sm = fxCtrl.layers[0].stateMachine;
                AnimatorState sIdle = AddState(sm, "Idle", EmptyClip(animDir, pname + "_FX_Idle"));
                AnimatorState sShoot = AddState(sm, "Shoot", MakeClip(animDir, pname + "_FX_Shoot", shootFrames, 18f, false));
                sm.defaultState = sIdle;
                AnimatorStateTransition ts = sIdle.AddTransition(sShoot); ts.hasExitTime = false; ts.duration = 0f; ts.AddCondition(AnimatorConditionMode.If, 0f, "Shoot");
                Back(sShoot, sIdle);
            }

            for (int lvl = 1; lvl <= 3; lvl++)
            {
                GameObject go = new GameObject(pname + "_Lv" + lvl);
                go.transform.localScale = Vector3.one * GameScale.Tower;
                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = Spr("tower_" + key + "_L" + lvl + ".png");     // THÂN tháp (không có vũ khí)
                sr.sortingOrder = 10;
                CircleCollider2D col = go.AddComponent<CircleCollider2D>();
                col.radius = 0.5f;
                col.offset = new Vector2(0f, 0.15f);
                col.isTrigger = true;

                // ---- ĐẦU tháp: nòng pháo / nỏ / tinh thể / cờ - xoay theo quái bởi TowerAimer
                GameObject head = new GameObject("Head");
                head.transform.SetParent(go.transform, false);
                head.transform.localPosition = new Vector3(0f, HeadY(key, lvl), 0f);
                SpriteRenderer hsr = head.AddComponent<SpriteRenderer>();
                hsr.sprite = Spr("tower_head_" + key + ".png");
                hsr.sortingOrder = 11;

                GameObject muzzle = new GameObject("Muzzle");
                muzzle.transform.SetParent(head.transform, false);
                bool crystal = key == "mage" || key == "ice";
                muzzle.transform.localPosition = crystal ? new Vector3(0f, 0.12f, 0f) : new Vector3(0.3f, 0.12f, 0f);

                TowerAimer aimer = go.AddComponent<TowerAimer>();
                aimer.head = head.transform;
                aimer.muzzle = muzzle.transform;
                aimer.muzzleLocal = new Vector2(0.3f, 0.12f);
                aimer.mode = key == "barracks" ? TowerAimer.AimMode.Wave : (crystal ? TowerAimer.AimMode.Crystal : TowerAimer.AimMode.Aim);

                if (key == "barracks")
                {
                    Barracks b = go.AddComponent<Barracks>();
                    b.towerName = disp;
                    b.buildCost = cost;
                    b.upgrades = ups;
                    b.currentLevel = lvl - 1;
                    b.soldierPrefab = soldier;
                }
                else
                {
                    Turret tu = go.AddComponent<Turret>();
                    tu.towerName = disp;
                    tu.buildCost = cost;
                    tu.upgrades = ups;
                    tu.currentLevel = lvl - 1;
                    tu.shootPoint = muzzle.transform;
                    tu.FiringPoint = muzzle.transform;
                    tu.turretRotationPoint = head.transform;
                    tu.bulletPrefab = bullet;
                    tu.BulletPrefab = bullet;
                    TowerUpgrade u0 = ups[lvl - 1];
                    tu.damage = u0.damage;
                    tu.targetingRange = u0.range;
                    tu.BulletsPerSecond = u0.fireRate;

                    // hiệu ứng nổ súng / phép thuật tại đầu nòng
                    GameObject fx = new GameObject("FX");
                    fx.transform.SetParent(muzzle.transform, false);
                    SpriteRenderer fsr = fx.AddComponent<SpriteRenderer>();
                    fsr.sortingOrder = 12;
                    fx.AddComponent<Animator>().runtimeAnimatorController = fxCtrl;
                    TowerAnimator ta = go.AddComponent<TowerAnimator>();
                    ta.fxAnimator = fx.GetComponent<Animator>();
                }

                // ---- Hiệu ứng DỰNG THÁP: các mảnh bay vào ghép lại + sao lấp lánh
                TowerBuildEffect be = go.AddComponent<TowerBuildEffect>();
                be.partSprites = BuildParts(key);
                be.starSprite = KRAssetFinder.LoadSprite("build_star.png");
                be.puffFrames = Numbered("fx_death_poof").ToArray();
                Save(go, P + "Prefabs/Towers/" + folder + "/" + pname + "_Lv" + lvl + ".prefab");
                n++;
            }
        }
        return n;
    }
}
