using UnityEngine;

/// <summary>
/// Tự động đọc Anh hùng đã chọn từ SaveManager và triển khai (Deploy) vào trận chiến tại vị trí Lâu đài.
/// Tự động cập nhật chỉ số, màu sắc và kỹ năng tương ứng với Hero được chọn.
/// </summary>
public class HeroDeployer : MonoBehaviour
{
    public static HeroDeployer instance;

    [Header("Prefab Reference (Tùy chọn)")]
    public GameObject heroPrefab;

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        DeploySelectedHero();
    }

    public void DeploySelectedHero()
    {
        string selectedId = "Knight";
        if (SaveManager.instance != null)
        {
            selectedId = SaveManager.instance.selectedHeroId;
        }

        HeroData heroData = HeroDatabase.GetHeroById(selectedId);

        // 1. Kiểm tra xem trong Scene đã có Hero sẵn chưa
        Hero existingHero = Object.FindAnyObjectByType<Hero>();
        if (existingHero != null)
        {
            ApplyHeroData(existingHero, heroData);
            return;
        }

        // 2. Tìm vị trí spawn hợp lý (gần Lâu đài / cuối đường)
        Vector3 spawnPos = Vector3.zero;
        if (LevelManager.main != null && LevelManager.main.Path != null && LevelManager.main.Path.Length > 0)
        {
            spawnPos = LevelManager.main.Path[LevelManager.main.Path.Length - 1].position + new Vector3(-1.5f, 0f, 0f);
        }
        else
        {
            GameObject castle = GameObject.Find("Castle");
            if (castle != null) spawnPos = castle.transform.position + new Vector3(-1.5f, 0f, 0f);
        }

        // 3. Nạp Prefab Hero
        GameObject heroObj = null;
        if (heroPrefab != null)
        {
            heroObj = Instantiate(heroPrefab, spawnPos, Quaternion.identity);
        }
        else
        {
            // Thử load từ Resources hoặc tìm trong project
            heroPrefab = Resources.Load<GameObject>($"Heroes/Hero_{selectedId}") ?? Resources.Load<GameObject>("Hero_Knight");
            if (heroPrefab != null)
            {
                heroObj = Instantiate(heroPrefab, spawnPos, Quaternion.identity);
            }
        }

        // 4. Nếu chưa có Prefab -> Khởi tạo Hero thực tế bằng code (Fallback an toàn tuyệt đối)
        if (heroObj == null)
        {
            heroObj = CreateFallbackHero(spawnPos, heroData);
        }

        Hero heroComp = heroObj.GetComponent<Hero>();
        if (heroComp != null)
        {
            ApplyHeroData(heroComp, heroData);
        }

        Debug.Log($"<color=green>[HeroDeployer]</color> Đã triển khai thành công Anh hùng '{heroData.heroName}' ({heroData.role}) vào trận chiến!");
    }

    private void ApplyHeroData(Hero hero, HeroData data)
    {
        hero.name = $"Hero_{data.heroId}";
        hero.heroId = data.heroId;
        hero.maxHealth = data.maxHealth;
        hero.currentHealth = data.maxHealth;
        hero.attackDamage = data.attackDamage;
        hero.moveSpeed = data.moveSpeed;
        hero.attackRange = data.attackRange;

        // Đổi màu sắc trang phục / tint theo nguyên tố của Hero
        // Sprite hero đã có màu sẵn -> không nhuộm màu (trừ Paladin: ánh vàng nhẹ)
        if (hero.spriteRenderer != null)
        {
            bool paladin = string.Equals(data.heroId, "Paladin", System.StringComparison.OrdinalIgnoreCase);
            hero.spriteRenderer.color = paladin ? new Color(1f, 0.86f, 0.5f, 1f) : Color.white;
        }

        // Cập nhật HeroHUD nếu có
        if (HeroHUD.instance != null)
        {
            HeroHUD.instance.targetHero = hero;
            if (HeroHUD.instance.skill1Text != null) HeroHUD.instance.skill1Text.text = $"{data.skill1Name}\n<size=10>(Sẵn sàng)</size>";
            if (HeroHUD.instance.skill2Text != null) HeroHUD.instance.skill2Text.text = $"{data.skill2Name}\n<size=10>(Sẵn sàng)</size>";
        }
    }

    private GameObject CreateFallbackHero(Vector3 spawnPos, HeroData data)
    {
        GameObject root = new GameObject($"Hero_{data.heroId}");
        root.transform.position = spawnPos;
        root.tag = "Player";

        // Logic
        Hero hero = root.AddComponent<Hero>();
        HeroController heroCtrl = root.AddComponent<HeroController>();
        heroCtrl.hero = hero;

        CircleCollider2D col = root.AddComponent<CircleCollider2D>();
        col.radius = 0.45f;
        col.isTrigger = true;

        Rigidbody2D rb = root.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;

        // Visual
        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);

        SpriteRenderer sr = visual.AddComponent<SpriteRenderer>();
        sr.color = data.themeColor;
        sr.sortingOrder = 10;

        var allSprites = Resources.FindObjectsOfTypeAll<Sprite>();
        foreach (var s in allSprites)
        {
            if (s.name.ToLower().Contains("idle_01") || s.name.ToLower().Contains("idle"))
            {
                sr.sprite = s;
                break;
            }
        }

        if (sr.sprite == null)
        {
            Texture2D tex = new Texture2D(32, 32);
            Color[] colors = new Color[32 * 32];
            for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
            tex.SetPixels(colors);
            tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
        }

        hero.spriteRenderer = sr;
        return root;
    }
}
