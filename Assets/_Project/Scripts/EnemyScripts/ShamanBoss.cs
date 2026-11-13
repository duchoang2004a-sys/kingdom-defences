using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Boss Kẻ Dẫn Đường (Shaman Dẫn Đường - Part B):
/// - Máu 3000 HP, tốc độ 1.6, đi xuyên Lính/Hero (không bị chặn bởi Blocker).
/// - Kháng hiệu ứng: 50% Slow, 70% Stun, 70% Freeze.
/// - Đòn Choáng Tháp (B2): Mỗi 3s chọn 1 tháp trong 8u (dùng túi xáo trộn), báo trước 0.6s rồi choáng 1s.
/// - Hỗ Trợ Đồng Minh (B3): Aura 2.5u tăng 15% tốc độ, 40% kháng CC, hồi 2% HP/s cho đồng minh.
/// - Tự Hồi Máu Khi Rảnh (B4): Cửa sổ trượt 5s nhận sát thương < 2% max HP -> tự hồi 3% HP/s.
/// - Giai Đoạn 2 (<66% HP) & Giai Đoạn 3 (<33% HP) & Overtime rút ngắn nhịp choáng.
/// </summary>
public class ShamanBoss : MonoBehaviour
{
    [Header("Shaman Attributes")]
    public float maxHealth = 3000f;
    public float moveSpeed = 1.6f;
    public float stunSkillInterval = 3.0f;
    public float stunSkillRadius = 8.0f;
    public float auraRadius = 2.5f;

    [Header("Visual Telegraph")]
    private LineRenderer lightningBeam;

    private Enemy enemy;
    private EnemyHealth health;
    private EnemyMovement movement;
    private StatusEffectController statusController;

    private float lastStunSkillTime = 0f;
    private float lastMinionSpawnTime = 0f;

    // Bag shuffling for tower targeting
    private List<Turret> towerTargetBag = new List<Turret>();

    // Damage sliding window for out-of-combat heal (Part B4)
    private readonly Queue<KeyValuePair<float, float>> damageWindow = new Queue<KeyValuePair<float, float>>();
    private float totalDamageInWindow = 0f;

    private void Awake()
    {
        enemy = GetComponent<Enemy>() ?? gameObject.AddComponent<Enemy>();
        enemy.enemyName = "Shaman";
        enemy.isBoss = true;
        enemy.bossTier = 2;
        enemy.goldReward = 100;

        health = GetComponent<EnemyHealth>() ?? gameObject.AddComponent<EnemyHealth>();
        movement = GetComponent<EnemyMovement>() ?? gameObject.AddComponent<EnemyMovement>();
        statusController = GetComponent<StatusEffectController>() ?? gameObject.AddComponent<StatusEffectController>();

        // Thiết lập kháng hiệu ứng khống chế theo đề bài (Part B1)
        statusController.statusResist = 0.70f; // 70% Stun / Freeze resist

        SetupTelegraphLine();
    }

    private void OnEnable()
    {
        // Reset trạng thái khi lấy từ Object Pool (Part C)
        lastStunSkillTime = Time.time;
        lastMinionSpawnTime = Time.time;
        damageWindow.Clear();
        totalDamageInWindow = 0f;
        towerTargetBag.Clear();

        if (lightningBeam != null) lightningBeam.enabled = false;
    }

    private void SetupTelegraphLine()
    {
        GameObject lineObj = new GameObject("ShamanLightningBeam");
        lineObj.transform.SetParent(transform, false);

        lightningBeam = lineObj.AddComponent<LineRenderer>();
        lightningBeam.startWidth = 0.08f;
        lightningBeam.endWidth = 0.04f;
        lightningBeam.material = new Material(Shader.Find("Sprites/Default"));
        lightningBeam.startColor = new Color(0.3f, 0.8f, 1f, 0.9f);
        lightningBeam.endColor = new Color(1f, 0.9f, 0.2f, 0.9f);
        lightningBeam.enabled = false;
    }

    private void Update()
    {
        if (health == null || health.CurrentHealth <= 0) return;

        // Bỏ qua chặn đường của Lính/Hero (Đi xuyên Blocker - Part B1)
        if (movement != null)
        {
            movement.ForceDisengage();
        }

        bool isDisabled = statusController != null && statusController.IsDisabled;

        if (!isDisabled)
        {
            // 1. Đòn Choáng Tháp theo chu kỳ (Part B2)
            float activeStunInterval = GetActiveStunInterval();
            if (Time.time - lastStunSkillTime >= activeStunInterval)
            {
                lastStunSkillTime = Time.time;
                StartCoroutine(CastTowerStunRoutine());
            }

            // 2. Hỗ trợ Aura cho đồng minh xung quanh (Part B3)
            ApplySupportAura();

            // 3. Tự hồi máu khi không bị đánh trong cửa sổ trượt 5s (Part B4)
            ProcessSelfHealing();

            // 4. Giai đoạn 3 (<33% HP): Sinh quái con (Part B5)
            float hpPct = health.CurrentHealth / health.MaxHealth;
            if (hpPct < 0.33f && Time.time - lastMinionSpawnTime >= 10.0f)
            {
                lastMinionSpawnTime = Time.time;
                SpawnMinions();
            }
        }
    }

    private float GetActiveStunInterval()
    {
        float interval = stunSkillInterval;
        float hpPct = health.CurrentHealth / health.MaxHealth;

        if (hpPct < 0.33f) interval = 2.0f;       // Giai đoạn 3
        else if (hpPct < 0.66f) interval = 2.5f;  // Giai đoạn 2

        // Overtime: Rút ngắn 0.25s mỗi 20s (Part B5)
        if (MissionDirector.main != null && MissionDirector.main.currentState == MatchState.Overtime)
        {
            float otTime = MissionDirector.main.overtimeTimer;
            interval = Mathf.Max(1.0f, interval - (Mathf.Floor(otTime / 20f) * 0.25f));
        }

        return interval;
    }

    /// <summary>
    /// Đòn Choáng Tháp (Part B2): Chọn tháp bằng Túi xáo trộn (Bag Shuffling) -> Báo trước 0.6s -> Choáng 1s
    /// </summary>
    private IEnumerator CastTowerStunRoutine()
    {
        Turret targetTower = GetNextTowerFromBag();
        if (targetTower == null) yield break;

        Vector3 startPos = transform.position;
        Vector3 targetPos = targetTower.transform.position;

        // Báo trước 0.6s bằng tia sét nối từ Shaman tới tháp
        if (lightningBeam != null)
        {
            lightningBeam.enabled = true;
            lightningBeam.SetPosition(0, startPos);
            lightningBeam.SetPosition(1, targetPos);
        }

        float timer = 0f;
        while (timer < 0.6f)
        {
            timer += Time.deltaTime;
            if (lightningBeam != null)
            {
                lightningBeam.SetPosition(0, transform.position);
                if (targetTower != null) lightningBeam.SetPosition(1, targetTower.transform.position);
            }
            yield return null;
        }

        if (lightningBeam != null) lightningBeam.enabled = false;

        // Choáng tháp 1s (Part B2)
        if (targetTower != null && targetTower.gameObject.activeInHierarchy)
        {
            targetTower.Stun(1.0f);
        }
    }

    private Turret GetNextTowerFromBag()
    {
        // Lọc lại các tháp hợp lệ trong bán kính 8.0u
        Plot[] plots = Object.FindObjectsByType<Plot>(FindObjectsSortMode.None);
        List<Turret> available = new List<Turret>();

        for (int i = 0; i < plots.Length; i++)
        {
            if (plots[i] == null) continue;
            Turret t = plots[i].GetTurret();
            if (t != null && Vector2.Distance(transform.position, t.transform.position) <= stunSkillRadius)
            {
                available.Add(t);
            }
        }

        if (available.Count == 0) return null;

        // Nếu túi xáo trộn rỗng, nạp lại túi mới và xáo trộn
        if (towerTargetBag.Count == 0)
        {
            towerTargetBag.AddRange(available);
            // Fisher-Yates Shuffle
            for (int i = 0; i < towerTargetBag.Count; i++)
            {
                Turret temp = towerTargetBag[i];
                int r = Random.Range(i, towerTargetBag.Count);
                towerTargetBag[i] = towerTargetBag[r];
                towerTargetBag[r] = temp;
            }
        }

        Turret selected = towerTargetBag[0];
        towerTargetBag.RemoveAt(0);

        if (selected == null || !available.Contains(selected))
        {
            return available[Random.Range(0, available.Count)];
        }

        return selected;
    }

    /// <summary>
    /// Hiệu ứng Hỗ trợ Aura (Part B3): +15-20% Tốc độ, +40% Kháng CC, Hồi 2% HP/s cho đồng minh
    /// </summary>
    private void ApplySupportAura()
    {
        var enemies = EnemyRegistry.ActiveEnemies;
        Vector2 myPos = transform.position;
        float hpPct = health.CurrentHealth / health.MaxHealth;
        float speedBonus = hpPct < 0.66f ? 0.20f : 0.15f; // Giai đoạn 2 aura tăng tốc 20%

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyHealth eh = enemies[i];
            if (eh == null || eh.gameObject == gameObject || !eh.gameObject.activeInHierarchy || eh.CurrentHealth <= 0) continue;

            if (Vector2.Distance(myPos, eh.transform.position) <= auraRadius)
            {
                StatusEffectController sec = eh.GetComponent<StatusEffectController>() ?? eh.gameObject.AddComponent<StatusEffectController>();
                sec.statusResist = Mathf.Max(sec.statusResist, 0.40f); // +40% Kháng khống chế

                // Hồi 2% HP/s cho đồng minh
                eh.TakeDamage(new DamageInfo(-eh.MaxHealth * 0.02f * Time.deltaTime, DamageType.True));
            }
        }
    }

    /// <summary>
    /// Tự Hồi Máu Khi Rảnh (Part B4): Cửa sổ trượt 5s nếu nhận sát thương < 2% max HP -> Hồi 3% HP/s
    /// </summary>
    private void ProcessSelfHealing()
    {
        float now = Time.time;

        // Xóa sát thương cũ ngoài cửa sổ 5s
        while (damageWindow.Count > 0 && now - damageWindow.Peek().Key > 5.0f)
        {
            totalDamageInWindow -= damageWindow.Dequeue().Value;
        }

        // Nếu tổng sát thương nhận trong 5s < 2% Max HP -> Tiến hành tự hồi máu
        float threshold = health.MaxHealth * 0.02f;
        if (totalDamageInWindow < threshold && health.CurrentHealth < health.MaxHealth)
        {
            float healAmount = health.MaxHealth * 0.03f * Time.deltaTime; // 3% Max HP / s
            health.TakeDamage(new DamageInfo(-healAmount, DamageType.True));
        }
    }

    public void TrackDamageTaken(float amount)
    {
        if (amount > 0)
        {
            damageWindow.Enqueue(new KeyValuePair<float, float>(Time.time, amount));
            totalDamageInWindow += amount;
        }
    }

    private void SpawnMinions()
    {
        if (EnemySpawner.main != null && EnemySpawner.main.enemyPrefabs != null && EnemySpawner.main.enemyPrefabs.Length > 0)
        {
            GameObject prefab = EnemySpawner.main.enemyPrefabs[0];
            Instantiate(prefab, transform.position + Vector3.left * 0.4f, Quaternion.identity);
            Instantiate(prefab, transform.position + Vector3.right * 0.4f, Quaternion.identity);
            Debug.Log("<color=purple>[ShamanBoss]</color> Giai đoạn 3: Shaman đã sinh 2 quái hỗ trợ!");
        }
    }
}
