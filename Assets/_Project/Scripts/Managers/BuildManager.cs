using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Quản lý việc chọn tháp, lưu trữ danh sách tháp cấp 1 và logic khởi tạo tháp cơ bản
public class BuildManager : MonoBehaviour
{
    public static BuildManager main;

    [Header("3 Tháp Cơ Bản Cấp 1 (Chuẩn Kingdom Rush: Cung, Pháo, Pháp Sư)")]
    [SerializeField] private Tower[] towers;
    private int selectedTower = 0;

    public Tower[] Towers
    {
        get => towers;
        set => towers = value;
    }

    private void Awake()
    {
        main = this;
    }

    private void Start()
    {
        // Luôn đảm bảo có tháp Cấp 1 để chơi
        if (towers == null || towers.Length == 0)
        {
            CreateDefaultTowerTemplates();
        }
    }

    private void CreateDefaultTowerTemplates()
    {
        // 1. Tạo Template Đạn (Bullet Templates)
        GameObject arrowBullet = CreateBulletTemplate("ArrowBulletTemplate", BulletType.Arrow, new Color(1f, 0.85f, 0.2f), 15f * GameScale.ProjectileSpeed, 12f, 0f);
        GameObject cannonBullet = CreateBulletTemplate("CannonBulletTemplate", BulletType.Cannonball, new Color(0.2f, 0.2f, 0.25f), 9f * GameScale.ProjectileSpeed, 30f, 2.2f * GameScale.RangeFactor);
        GameObject mageBullet = CreateBulletTemplate("MageBulletTemplate", BulletType.MageBolt, new Color(0.4f, 0.85f, 1f), 12f * GameScale.ProjectileSpeed, 22f, 0f);

        // 2. Tạo 5 Tháp Cấp 1 Tiêu Chuẩn (Cung, Pháo, Pháp Sư, Trại Lính, Băng)
        GameObject archerObj = CreateTurretTemplate("ArcherTower_Lv1", "Tháp Cung", 100, new Color(0.2f, 0.65f, 0.35f), arrowBullet, 12f, 3.6f * GameScale.RangeFactor, 1.6f, "Cung");
        GameObject mageObj = CreateTurretTemplate("MageTower_Lv1", "Pháp Sư", 100, new Color(0.55f, 0.25f, 0.85f), mageBullet, 22f, 3.4f * GameScale.RangeFactor, 0.95f, "Pháp Sư");
        GameObject cannonObj = CreateTurretTemplate("CannonTower_Lv1", "Tháp Pháo", 125, new Color(0.85f, 0.35f, 0.15f), cannonBullet, 30f, 3.2f * GameScale.RangeFactor, 0.65f, "Pháo");
        GameObject barracksObj = CreateBarracksTemplate("Barracks_Lv1", "Trại Lính", 70);
        GameObject iceObj = CreateTurretTemplate("IceTower_Lv1", "Tháp Băng", 110, new Color(0.2f, 0.75f, 1f), mageBullet, 8f, 3.2f * GameScale.RangeFactor, 0.8f, "Băng");

        Sprite sprArcher = LoadIconSprite("Assets/_Project/Sprites/UI/Skills/icon_tower_archer.png");
        Sprite sprMage = LoadIconSprite("Assets/_Project/Sprites/UI/Skills/icon_tower_mage.png");
        Sprite sprCannon = LoadIconSprite("Assets/_Project/Sprites/UI/Skills/icon_tower_cannon.png");
        Sprite sprBarracks = LoadIconSprite("Assets/_Project/Sprites/UI/Skills/icon_tower_barracks.png");
        Sprite sprIce = LoadIconSprite("Assets/_Project/Sprites/UI/Skills/icon_tower_ice.png");

        towers = new Tower[]
        {
            new Tower("Tháp Cung", 100, archerObj, sprArcher),
            new Tower("Pháp Sư", 100, mageObj, sprMage),
            new Tower("Tháp Pháo", 125, cannonObj, sprCannon),
            new Tower("Trại Lính", 70, barracksObj, sprBarracks),
            new Tower("Tháp Băng", 110, iceObj, sprIce)
        };

        selectedTower = 0;
        Debug.Log("[BuildManager] Đã khởi tạo 5 tháp cấp 1: Cung (100G), Pháp Sư (100G), Pháo (125G), Trại Lính (70G), Băng (110G).");
    }

    private Sprite LoadIconSprite(string path)
    {
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
#else
        return null;
#endif
    }

    private GameObject CreateBarracksTemplate(string objName, string tName, int cost)
    {
        GameObject barracksObj = new GameObject(objName);
        barracksObj.SetActive(false);
        barracksObj.transform.SetParent(transform);

        SpriteRenderer sr = barracksObj.AddComponent<SpriteRenderer>();
        sr.color = Color.white;
        sr.sortingOrder = 10;
        Sprite spr = LoadIconSprite("Assets/_Project/Sprites/Towers/tower_barracks_L1.png");
        if (spr != null) sr.sprite = spr;

        CircleCollider2D col = barracksObj.AddComponent<CircleCollider2D>();
        col.radius = 0.7f;
        col.isTrigger = true;

        Barracks barracks = barracksObj.AddComponent<Barracks>();
        barracks.towerName = tName;
        barracks.buildCost = cost;
        return barracksObj;
    }

    private GameObject CreateBulletTemplate(string name, BulletType bType, Color color, float speed, float damage, float aoe)
    {
        GameObject bulletObj = new GameObject(name);
        bulletObj.SetActive(false);
        bulletObj.transform.SetParent(transform);

        SpriteRenderer sr = bulletObj.AddComponent<SpriteRenderer>();
        sr.color = color;
        sr.sortingOrder = 30;

        Texture2D tex = new Texture2D(12, 12);
        Color[] colors = new Color[144];
        for (int i = 0; i < 144; i++) colors[i] = Color.white;
        tex.SetPixels(colors);
        tex.Apply();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, 12, 12), new Vector2(0.5f, 0.5f), 16f);

        CircleCollider2D col = bulletObj.AddComponent<CircleCollider2D>();
        col.radius = 0.2f;
        col.isTrigger = true;

        Bullet bullet = bulletObj.AddComponent<Bullet>();
        bullet.bulletSpeed = speed;
        bullet.bulletDamage = damage;
        bullet.bulletType = bType;
        bullet.aoeRadius = aoe;

        return bulletObj;
    }

    private GameObject CreateTurretTemplate(string objName, string tName, int cost, Color color, GameObject bulletPrefab, float dmg, float rng, float rate, string type)
    {
        GameObject turretObj = new GameObject(objName);
        turretObj.SetActive(false);
        turretObj.transform.SetParent(transform);

        // Sprite Tháp
        SpriteRenderer sr = turretObj.AddComponent<SpriteRenderer>();
        sr.color = Color.white;
        sr.sortingOrder = 10;

        string tType = type.ToLower();
        if (tType.Contains("cung")) tType = "archer";
        else if (tType.Contains("pháp") || tType.Contains("mage")) tType = "mage";
        else if (tType.Contains("pháo") || tType.Contains("cannon")) tType = "cannon";
        else if (tType.Contains("băng") || tType.Contains("ice")) tType = "ice";

        Sprite spr = LoadIconSprite($"Assets/_Project/Sprites/Towers/tower_{tType}_L1.png");
        if (spr != null)
        {
            sr.sprite = spr;
        }
        else
        {
            Texture2D tex = new Texture2D(32, 32);
            Color[] colors = new Color[1024];
            for (int i = 0; i < 1024; i++) colors[i] = Color.white;
            tex.SetPixels(colors);
            tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 16f);
        }

        CircleCollider2D col = turretObj.AddComponent<CircleCollider2D>();
        col.radius = 0.7f;
        col.isTrigger = true;

        Turret turret = turretObj.AddComponent<Turret>();
        turret.towerName = tName;
        turret.buildCost = cost;
        turret.damage = dmg;
        turret.targetingRange = rng;
        turret.BulletsPerSecond = rate;
        turret.currentLevel = 0; // Luôn bắt đầu từ Cấp 1 (index 0)

        // Thiết lập Upgrade Tree 3 cấp chuẩn theo yêu cầu đề bài
        if (type == "Cung")
        {
            turret.upgrades = new TowerUpgrade[]
            {
                new TowerUpgrade { levelName = "Cấp 1: Cung Thủ", cost = 0,   damage = 12f, range = 3.6f * GameScale.RangeFactor, fireRate = 1.6f, bulletsPerShot = 1 },
                new TowerUpgrade { levelName = "Cấp 2: Bắn Chuẩn", cost = 75,  damage = 22f, range = 4.2f * GameScale.RangeFactor, fireRate = 2.0f, bulletsPerShot = 1 },
                new TowerUpgrade { levelName = "Cấp 3: Song Tiễn", cost = 140, damage = 35f, range = 4.8f * GameScale.RangeFactor, fireRate = 2.4f, bulletsPerShot = 2 } // Bắn 2 mũi tên
            };
        }
        else if (type == "Pháo")
        {
            turret.upgrades = new TowerUpgrade[]
            {
                new TowerUpgrade { levelName = "Cấp 1: Tháp Pháo", cost = 0,   damage = 30f, range = 3.2f * GameScale.RangeFactor, fireRate = 0.65f, aoeRadius = 2.0f * GameScale.RangeFactor },
                new TowerUpgrade { levelName = "Cấp 2: Đại Bác",   cost = 90,  damage = 55f, range = 3.8f * GameScale.RangeFactor, fireRate = 0.8f,  aoeRadius = 2.6f * GameScale.RangeFactor },
                new TowerUpgrade { levelName = "Cấp 3: Siêu Nổ",   cost = 160, damage = 95f, range = 4.4f * GameScale.RangeFactor, fireRate = 0.95f, aoeRadius = 3.8f * GameScale.RangeFactor, bulletScale = 1.5f } // Siêu nổ rộng
            };
        }
        else // Pháp Sư
        {
            turret.upgrades = new TowerUpgrade[]
            {
                new TowerUpgrade { levelName = "Cấp 1: Pháp Sư",   cost = 0,   damage = 22f, range = 3.4f * GameScale.RangeFactor, fireRate = 0.95f, slowDuration = 0f },
                new TowerUpgrade { levelName = "Cấp 2: Sấm Sét",   cost = 80,  damage = 40f, range = 4.0f * GameScale.RangeFactor, fireRate = 1.25f, slowDuration = 1.0f },
                new TowerUpgrade { levelName = "Cấp 3: Băng Hàn",  cost = 150, damage = 65f, range = 4.6f * GameScale.RangeFactor, fireRate = 1.5f,  slowDuration = 2.5f } // Làm chậm / đóng băng
            };
        }

        // Gán BulletPrefab cho tháp
        var prop = turret.GetType().GetField("BulletPrefab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (prop != null)
        {
            prop.SetValue(turret, bulletPrefab);
        }

        return turretObj;
    }

    public Tower GetSelectedTower()
    {
        if (towers != null && selectedTower >= 0 && selectedTower < towers.Length)
            return towers[selectedTower];
        return null;
    }

    public void SetSelectedTower(int _selectedTower)
    {
        selectedTower = _selectedTower;
    }
}
