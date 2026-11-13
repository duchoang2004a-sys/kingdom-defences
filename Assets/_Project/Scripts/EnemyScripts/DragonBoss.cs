using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// DragonBoss (Trùm Cuối Màn 8 - Hỏa Long Thần):
/// - 3 Giai đoạn chiến đấu (Phase 1: 100-66%, Phase 2: 66-33%, Phase 3: <33% Cuồng bạo)
/// - Miễn nhiễm làm chậm thông thường, kháng khống chế 75%
/// - Phun lửa thiêu rụi Hero & Lính chặn đường
/// - Gọi Mưa Lửa (Fire Rain / Meteor) lên các vị trí Tháp phòng thủ làm tê liệt (Stun) tạm thời
/// - Khi HP < 33%: Bật Khiên Lửa (Flame Shield) giảm 40% sát thương nhận vào, tăng tốc độ
/// </summary>
public class DragonBoss : MonoBehaviour
{
    [Header("Dragon Boss Stats")]
    public float maxHealth = 5000f;
    public float baseMoveSpeed = 1.0f;
    public int bossGoldReward = 350;

    [Header("Skill Timers")]
    public float fireBreathCooldown = 4.0f;
    public float fireRainCooldown = 8.0f;
    public float roarCooldown = 12.0f;

    private float lastFireBreathTime = 0f;
    private float lastFireRainTime = 0f;
    private float lastRoarTime = 0f;

    [Header("Phases")]
    public int currentPhase = 1;
    public bool isEnraged = false;

    private Enemy enemy;
    private EnemyHealth health;
    private EnemyMovement movement;
    private StatusEffectController statusController;
    private SpriteRenderer spriteRenderer;

    [Header("Visual Effects")]
    private GameObject shieldAura;

    private void Awake()
    {
        enemy = GetComponent<Enemy>() ?? gameObject.AddComponent<Enemy>();
        enemy.enemyName = "DragonBoss";
        enemy.isBoss = true;
        enemy.bossTier = 3;
        enemy.goldReward = bossGoldReward;
        enemy.maxHealth = maxHealth;
        enemy.moveSpeed = baseMoveSpeed;
        enemy.statusResist = 0.75f;

        health = GetComponent<EnemyHealth>() ?? gameObject.AddComponent<EnemyHealth>();
        movement = GetComponent<EnemyMovement>() ?? gameObject.AddComponent<EnemyMovement>();
        statusController = GetComponent<StatusEffectController>() ?? gameObject.AddComponent<StatusEffectController>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        CreateShieldVisual();
    }

    private void Start()
    {
        lastFireBreathTime = Time.time;
        lastFireRainTime = Time.time + 2f;
        lastRoarTime = Time.time + 4f;
    }

    private void Update()
    {
        if (health == null || health.CurrentHealth <= 0) return;

        UpdateBossPhases();
        HandleSkills();
    }

    private void UpdateBossPhases()
    {
        float hpPercent = health.CurrentHealth / Mathf.Max(1f, health.MaxHealth);

        if (hpPercent <= 0.33f && currentPhase < 3)
        {
            EnterPhase3();
        }
        else if (hpPercent <= 0.66f && currentPhase < 2)
        {
            EnterPhase2();
        }
    }

    private void EnterPhase2()
    {
        currentPhase = 2;
        fireBreathCooldown = 3.0f;
        fireRainCooldown = 6.0f;

        // Tiếng gầm chuyển pha
        StartCoroutine(DragonRoarEffect());
    }

    private void EnterPhase3()
    {
        currentPhase = 3;
        isEnraged = true;
        fireBreathCooldown = 2.2f;
        fireRainCooldown = 4.5f;

        if (enemy != null)
        {
            enemy.moveSpeed = baseMoveSpeed * 1.4f;
        }

        if (shieldAura != null)
        {
            shieldAura.SetActive(true);
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.color = new Color(1f, 0.4f, 0.4f, 1f); // ửng đỏ cuồng nộ
        }

        StartCoroutine(DragonRoarEffect());
    }

    private void HandleSkills()
    {
        // 1. Phun Lửa
        if (Time.time - lastFireBreathTime >= fireBreathCooldown)
        {
            lastFireBreathTime = Time.time;
            CastFireBreath();
        }

        // 2. Mưa Lửa Thần (Phase 2 & 3)
        if (currentPhase >= 2 && Time.time - lastFireRainTime >= fireRainCooldown)
        {
            lastFireRainTime = Time.time;
            CastFireRain();
        }

        // 3. Tiếng Gầm Rồng
        if (Time.time - lastRoarTime >= roarCooldown)
        {
            lastRoarTime = Time.time;
            StartCoroutine(DragonRoarEffect());
        }
    }

    private void CastFireBreath()
    {
        // Gây sát thương AoE lên Hero và lính lân cận trong bán kính 2.8 unit
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, 2.8f);
        foreach (var col in hits)
        {
            // Gây sát thương lên Hero
            Hero hero = col.GetComponent<Hero>();
            if (hero != null)
            {
                hero.TakeDamage(currentPhase >= 3 ? 35f : 20f);
            }

            // Gây sát thương lên lính cản đường
            ReinforcementSoldier soldier = col.GetComponent<ReinforcementSoldier>();
            if (soldier != null)
            {
                soldier.TakeDamage(currentPhase >= 3 ? 40 : 25);
            }
        }
    }

    private void CastFireRain()
    {
        // Làm tê liệt 1 tháp ngẫu nhiên trong 2.0s bằng hàm Stun của Turret
        Turret[] allTurrets = Object.FindObjectsByType<Turret>(FindObjectsSortMode.None);
        if (allTurrets != null && allTurrets.Length > 0)
        {
            Turret targetTurret = allTurrets[Random.Range(0, allTurrets.Length)];
            if (targetTurret != null)
            {
                targetTurret.Stun(2.0f);
            }
        }
    }

    private IEnumerator DragonRoarEffect()
    {
        // Tiếng gầm rồng gây sát thương chấn động và làm chậm
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, 5.0f);
        foreach (var col in hits)
        {
            Hero hero = col.GetComponent<Hero>();
            if (hero != null)
            {
                hero.TakeDamage(15f);
                hero.ApplySlow(0.5f, 2.0f);
            }

            ReinforcementSoldier soldier = col.GetComponent<ReinforcementSoldier>();
            if (soldier != null)
            {
                soldier.TakeDamage(10);
            }
        }
        yield return null;
    }

    private void CreateShieldVisual()
    {
        shieldAura = new GameObject("DragonFlameShield");
        shieldAura.transform.SetParent(transform);
        shieldAura.transform.localPosition = Vector3.zero;

        SpriteRenderer sr = shieldAura.AddComponent<SpriteRenderer>();
        sr.color = new Color(1f, 0.5f, 0f, 0.45f);
        sr.sortingOrder = 7;
        shieldAura.transform.localScale = new Vector3(1.8f, 1.8f, 1f);
        shieldAura.SetActive(false);
    }
}
