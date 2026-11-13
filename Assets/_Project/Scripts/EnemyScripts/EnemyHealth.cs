using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Handles enemy health, damage mitigation (Armor/Resists), white flash on hit, and health bar
public class EnemyHealth : MonoBehaviour
{
    [Header("Events")]
    public static UnityEvent onEnemyDestroy = new UnityEvent();

    private float currentHealth;
    private float maxHealthRuntime = 30f;
    private bool isDestroyed = false;

    private Enemy enemy;
    private StatusEffectController statusController;
    private SpriteRenderer spriteRenderer;
    private Color defaultSpriteColor = Color.white;
    private Coroutine flashCoroutine;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealthRuntime;
    public bool IsDestroyed => isDestroyed;

    private Transform healthBarBg;
    private Transform healthBarFill;
    private SpriteRenderer fillRenderer;

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        statusController = GetComponent<StatusEffectController>();
        if (statusController == null)
        {
            statusController = gameObject.AddComponent<StatusEffectController>();
        }

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            defaultSpriteColor = spriteRenderer.color;
        }

        SetupHealthBar();
    }

    private void OnEnable()
    {
        if (enemy == null) enemy = GetComponent<Enemy>();
        maxHealthRuntime = enemy != null ? enemy.MaxHealth : 30f;
        currentHealth = maxHealthRuntime;
        isDestroyed = false;

        // Reset sprite color to prevent any stuck white flash from pooling
        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
            flashCoroutine = null;
        }
        if (spriteRenderer != null)
        {
            spriteRenderer.color = defaultSpriteColor;
        }

        // Register with static registry for zero-allocation targeting
        EnemyRegistry.Register(this);

        // Nếu quái spawn trong lúc phép Băng Giá đang diễn ra, quái cũng sẽ bị đóng băng theo thời gian còn lại
        if (SpellManager.GlobalFreezeRemaining > 0.1f && statusController != null)
        {
            statusController.ApplyEffect(StatusEffectType.Freeze, 1f, SpellManager.GlobalFreezeRemaining);
        }

        SetupHealthBar();
        UpdateHealthBar();

        if (healthBarBg != null)
        {
            healthBarBg.gameObject.SetActive(true);
        }
    }

    private void OnDisable()
    {
        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
            flashCoroutine = null;
        }
        if (spriteRenderer != null)
        {
            spriteRenderer.color = defaultSpriteColor;
        }

        EnemyRegistry.Unregister(this);
    }

    public void ApplyDifficulty(float multiplier)
    {
        if (enemy == null) enemy = GetComponent<Enemy>();
        float baseHp = enemy != null ? enemy.MaxHealth : 30f;
        maxHealthRuntime = Mathf.Max(10f, baseHp * multiplier);
        currentHealth = maxHealthRuntime;
        UpdateHealthBar();
    }

    private void SetupHealthBar()
    {
        if (healthBarBg != null) return;

        // 1. Nền đen của thanh máu (Viền ngoài)
        GameObject bgObj = new GameObject("HP_Bar_BG");
        bgObj.transform.SetParent(transform, false);
        bgObj.transform.localPosition = new Vector3(0, 0.55f, 0);
        bgObj.transform.localScale = new Vector3(0.55f, 0.5f, 1f);

        SpriteRenderer bgSr = bgObj.AddComponent<SpriteRenderer>();
        bgSr.color = new Color(0.08f, 0.08f, 0.08f, 0.95f);
        bgSr.sortingOrder = 50;

        Texture2D bgTex = new Texture2D(36, 8);
        Color[] bgColors = new Color[36 * 8];
        for (int i = 0; i < bgColors.Length; i++) bgColors[i] = Color.white;
        bgTex.SetPixels(bgColors);
        bgTex.Apply();
        bgSr.sprite = Sprite.Create(bgTex, new Rect(0, 0, 36, 8), new Vector2(0.5f, 0.5f), 32f);

        // 2. Ruột thanh máu (Màu xanh / vàng / đỏ theo % HP)
        GameObject fillObj = new GameObject("HP_Bar_Fill");
        fillObj.transform.SetParent(bgObj.transform, false);
        fillObj.transform.localPosition = new Vector3(-0.48f, 0, 0);

        fillRenderer = fillObj.AddComponent<SpriteRenderer>();
        fillRenderer.color = new Color(0.2f, 0.85f, 0.3f);
        fillRenderer.sortingOrder = 51;

        Texture2D fillTex = new Texture2D(32, 6);
        Color[] fillColors = new Color[32 * 6];
        for (int i = 0; i < fillColors.Length; i++) fillColors[i] = Color.white;
        fillTex.SetPixels(fillColors);
        fillTex.Apply();
        fillRenderer.sprite = Sprite.Create(fillTex, new Rect(0, 0, 32, 6), new Vector2(0f, 0.5f), 32f);

        healthBarBg = bgObj.transform;
        healthBarFill = fillObj.transform;
        bgObj.SetActive(true);
    }

    private void UpdateHealthBar()
    {
        if (healthBarFill != null)
        {
            float max = MaxHealth;
            float pct = max > 0 ? Mathf.Clamp01(currentHealth / max) : 0f;
            healthBarFill.localScale = new Vector3(pct, 1f, 1f);

            if (fillRenderer != null)
            {
                if (pct > 0.5f)
                    fillRenderer.color = Color.Lerp(Color.yellow, new Color(0.2f, 0.88f, 0.35f), (pct - 0.5f) * 2f);
                else
                    fillRenderer.color = Color.Lerp(new Color(0.95f, 0.2f, 0.2f), Color.yellow, pct * 2f);
            }
        }

        if (healthBarBg != null)
        {
            healthBarBg.gameObject.SetActive(currentHealth > 0);
        }
    }

    private float invulnerableUntil = 0f;

    public void SetInvulnerable(float duration)
    {
        invulnerableUntil = Time.time + duration;
    }

    public void ReduceHealthPercent(float percent)
    {
        percent = Mathf.Clamp01(percent);
        currentHealth *= (1f - percent);
        maxHealthRuntime *= (1f - percent);
        UpdateHealthBar();
    }

    private DamageInfo lastDamageInfo;

    // Overload for DamageInfo
    public void TakeDamage(DamageInfo info)
    {
        if (isDestroyed || Time.time < invulnerableUntil) return;

        lastDamageInfo = info;
        float rawDamage = info.amount;
        float finalDamage = CalculateDamageAfterMitigation(rawDamage, info.type, info.armorPenetration);

        currentHealth -= finalDamage;
        UpdateHealthBar();

        TriggerWhiteFlash();

        // Kích hoạt hiển thị Sát thương Số nhảy phân màu
        DamageTextManager.SpawnDamageText(transform.position, finalDamage, info.source, info.type, transform);

        if (currentHealth <= 0 && !isDestroyed)
        {
            Die();
        }
    }

    // New Heal method with anti-heal check
    public void Heal(float amount)
    {
        if (isDestroyed) return;

        // Anti-heal check: If wounded, no healing allowed
        if (statusController != null && statusController.IsWounded)
        {
            return;
        }

        currentHealth = Mathf.Min(currentHealth + amount, MaxHealth);
        UpdateHealthBar();
    }

    // Backwards-compatible overload for simple float damage
    public void TakeDamage(float dmg)
    {
        TakeDamage(new DamageInfo(dmg, DamageType.Physical));
    }

    private float CalculateDamageAfterMitigation(float rawDmg, DamageType type, float armorPen = 0f)
    {
        if (enemy == null) enemy = GetComponent<Enemy>();
        if (statusController == null) statusController = GetComponent<StatusEffectController>();

        float baseArmor = enemy != null ? enemy.Armor : 0f;
        float baseMagicResist = enemy != null ? enemy.MagicResist : 0f;
        float armorReduction = statusController != null ? statusController.ArmorReduction : 0f;

        // Apply armor penetration (Archer Tower)
        float ignoredArmor = baseArmor * armorPen;
        float effectiveArmor = Mathf.Clamp(baseArmor - armorReduction - ignoredArmor, -0.2f, 0.8f);
        float effectiveResist = Mathf.Clamp(baseMagicResist, -0.2f, 0.8f);

        switch (type)
        {
            case DamageType.Physical:
                return rawDmg * (1f - effectiveArmor);

            case DamageType.Explosive:
                // Explosive ignores half the armor (stacked with penetration if any)
                return rawDmg * (1f - (effectiveArmor * 0.5f));

            case DamageType.Magic:
                return rawDmg * (1f - effectiveResist);

            case DamageType.True:
            default:
                return rawDmg;
        }
    }

    private void TriggerWhiteFlash()
    {
        if (spriteRenderer == null) return;
        if (flashCoroutine != null) StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(FlashWhiteRoutine());
    }

    private IEnumerator FlashWhiteRoutine()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.white;
            yield return new WaitForSeconds(0.06f);
            if (spriteRenderer != null)
            {
                spriteRenderer.color = defaultSpriteColor;
            }
        }
        flashCoroutine = null;
    }

    private void Die()
    {
        if (isDestroyed) return;
        isDestroyed = true;

        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
            flashCoroutine = null;
        }
        if (spriteRenderer != null)
        {
            spriteRenderer.color = defaultSpriteColor;
        }

        EnemyRegistry.Unregister(this);

        if (enemy == null) enemy = GetComponent<Enemy>();

        // Phát tín hiệu sự kiện diệt quái chuẩn Part A3
        GameEvents.TriggerEnemyKilled(enemy, lastDamageInfo);

        // Xử lý ghi nhận diệt Boss & Sinh Quái Con (Minion Split)
        if (enemy != null && enemy.IsBoss)
        {
            if (MissionTracker.main != null)
            {
                MissionTracker.main.RegisterBossKill();
            }

            // Nếu là Boss gốc (depthLevel == 0) -> Sinh 2-3 Quái con tại vị trí chết
            if (enemy.depthLevel == 0)
            {
                SpawnBossMinions();
            }
        }

        int reward = enemy != null ? enemy.CurrencyReward : 15;

        // Áp dụng hệ số nhân vàng theo Level từ Bảng Cân Bằng
        float levelGoldMultiplier = GameDataBalancer.GetDifficultyGold(LevelManager.main != null ? LevelManager.main.levelNumber : 1);
        int finalReward = Mathf.RoundToInt(reward * levelGoldMultiplier);

        if (LevelManager.main != null)
        {
            LevelManager.main.IncreaseCurrency(finalReward);
        }

        onEnemyDestroy.Invoke();

        if (EnemySpawner.onEnemyKilled != null)
        {
            EnemySpawner.onEnemyKilled.Invoke();
        }

        if (ObjectPool.instance != null)
        {
            ObjectPool.instance.ReturnToPool(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void SpawnBossMinions()
    {
        if (enemy == null) return;

        EnemyMovement em = GetComponent<EnemyMovement>();
        int pId = em != null ? em.PathId : 0;
        int pIdx = em != null ? em.PathIndex : 0;
        Vector3 deathPos = transform.position;

        int count = Mathf.Clamp(enemy.minionCount, 2, 3);
        GameObject prefabToSpawn = enemy.minionPrefab;

        // Nếu chưa gán minionPrefab, sử dụng prefab từ EnemySpawner nếu có
        if (prefabToSpawn == null && EnemySpawner.main != null && EnemySpawner.main.enemyPrefabs != null && EnemySpawner.main.enemyPrefabs.Length > 0)
        {
            prefabToSpawn = EnemySpawner.main.enemyPrefabs[0];
        }

        if (prefabToSpawn == null) return;

        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPos = deathPos + (Vector3)(Random.insideUnitCircle * 0.35f);
            GameObject minionObj = null;

            if (ObjectPool.instance != null)
            {
                minionObj = ObjectPool.instance.Spawn(prefabToSpawn, spawnPos, Quaternion.identity);
            }
            else
            {
                minionObj = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);
            }

            if (minionObj != null)
            {
                // Tăng bộ đếm quái sống trong EnemySpawner
                if (EnemySpawner.main != null)
                {
                    EnemySpawner.main.EnemiesAlive++;
                }

                Enemy minionEnemy = minionObj.GetComponent<Enemy>();
                if (minionEnemy != null)
                {
                    minionEnemy.depthLevel = 1; // Quái con không tự tách thêm lần nữa
                    minionEnemy.goldReward = Mathf.Max(2, minionEnemy.goldReward / 2); // Thưởng vàng quái con giảm nửa
                }

                EnemyMovement minionEm = minionObj.GetComponent<EnemyMovement>();
                if (minionEm != null)
                {
                    minionEm.SetPathAndWaypoint(pId, pIdx);
                }

                EnemyHealth minionHealth = minionObj.GetComponent<EnemyHealth>();
                if (minionHealth != null)
                {
                    minionHealth.SetInvulnerable(0.3f); // Bất tử 0.3s để tránh bị diệt chùm instant
                }
            }
        }

        Debug.Log($"<color=orange>[Boss Split]</color> Boss {enemy.EnemyName} đã tách thành {count} quái con tại vị trí chết!");
    }
}
