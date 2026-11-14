using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Trại lính. v2 (SESSION_20261113_B): xây xong là triệu hồi lính canh chặn ngay trên đường (bản gốc không bao giờ
/// gọi SpawnSoldier lúc xây nên không thấy lính). Số lính / máu / giáp / sát thương theo cấp tháp; lên cấp thì lính
/// hiện tại được cường hóa + hồi đầy máu, thêm lính nếu cấp mới có nhiều lính hơn. Lính chết sẽ hồi sinh theo
/// soldierRespawnTime. Lính đứng ở các LÀN khác nhau của đường (cấp 1: 2 lính ở hai làn rìa, cấp 2+: 3 lính kín 3 làn).
/// </summary>
public class Barracks : MonoBehaviour, IBaseTower
{
    [Header("Thông tin trại lính")]
    public string towerName = "Trại Lính (Barracks)";
    public int buildCost = 100;

    public TowerData towerData;
    public TowerUpgrade[] upgrades;
    public int currentLevel = 0;
    public GameObject soldierPrefab;
    public Transform rallyPoint;

    [Header("Lính canh")]
    [Tooltip("Sát thương lính cấp 1; mỗi cấp tháp cộng thêm 'damagePerLevel'")]
    public float soldierDamage = 10f;
    public float damagePerLevel = 4f;
    public float soldierRegen = 1.5f;

    [HideInInspector] public Plot plot;
    [HideInInspector] public float creationTime;

    private ReinforcementSoldier[] slots = new ReinforcementSoldier[0];
    private bool[] slotPending = new bool[0];
    private Vector2 rallyBase;
    private Vector2 rallyDir = Vector2.right;

    private void Awake()
    {
        creationTime = Time.time;
        var list = GetUpgradeList();
        if (list != null && list.Length > 0)
        {
            buildCost = list[0].cost;
        }
        if (towerData != null && !string.IsNullOrEmpty(towerData.towerName))
        {
            towerName = towerData.towerName;
        }
    }

    private void Start()
    {
        ApplyUpgrade(currentLevel);
    }

    private void OnDestroy()
    {
        // Bán / phá tháp -> lính biến mất theo
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null) Destroy(slots[i].gameObject);
        }
    }

    public void ApplyUpgrade(int level)
    {
        currentLevel = level;
        SpriteRenderer sr = GetComponent<SpriteRenderer>() ?? GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = Color.white;
            sr.sortingOrder = 10;
            int lvlNum = currentLevel + 1;
            Sprite newSpr = Resources.Load<Sprite>($"Towers/tower_barracks_L{lvlNum}");
#if UNITY_EDITOR
            if (newSpr == null)
            {
                newSpr = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/_Project/Sprites/Towers/tower_barracks_L{lvlNum}.png");
            }
#endif
            if (newSpr != null)
            {
                sr.sprite = newSpr;
            }
        }

        TowerBuildEffect buildFx = GetComponent<TowerBuildEffect>();
        if (buildFx != null) buildFx.OnUpgraded(currentLevel);

        RefreshGarrison();
    }

    // ------------------------------------------------------------------ Garrison

    private int TargetCount()
    {
        TowerUpgrade u = GetCurrentUpgrade();
        return Mathf.Clamp(u != null ? u.soldierCount : 2, 0, 6);
    }

    /// <summary>Đảm bảo đủ lính theo cấp hiện tại: thêm lính thiếu, cường hóa lính đang có, cập nhật điểm đứng.</summary>
    public void RefreshGarrison()
    {
        if (!isActiveAndEnabled || soldierPrefab == null) return;

        int target = TargetCount();
        if (slots.Length != target)
        {
            var newSlots = new ReinforcementSoldier[target];
            var newPending = new bool[target];
            for (int i = 0; i < slots.Length; i++)
            {
                if (i < target) { newSlots[i] = slots[i]; newPending[i] = slotPending[i]; }
                else if (slots[i] != null) Destroy(slots[i].gameObject);
            }
            slots = newSlots;
            slotPending = newPending;
        }

        ResolveRally();
        TowerUpgrade u = GetCurrentUpgrade();
        for (int i = 0; i < slots.Length; i++)
        {
            ReinforcementSoldier s = slots[i];
            // Lính vừa Instantiate chưa chạy Start() nên currentHealth còn 0 (IsAlive=false). Plot gọi ApplyUpgrade(0) rồi Start() gọi lại
            // trong cùng frame -> không dùng IsAlive để tránh sinh lính trùng. Lính chết đã được ReportDeath() gỡ khỏi slot.
            if (s != null)
            {
                ApplyStats(s, u, true);
                s.SetRallyPosition(SlotPosition(i));
            }
            else
            {
                slots[i] = null;
                if (!slotPending[i]) SpawnInSlot(i);
            }
        }
    }

    private void SpawnInSlot(int i)
    {
        if (soldierPrefab == null || i < 0 || i >= slots.Length) return;
        Vector3 spawnPos = transform.position + new Vector3(0f, -0.15f, 0f);
        GameObject sObj = Instantiate(soldierPrefab, spawnPos, Quaternion.identity);
        ReinforcementSoldier s = sObj.GetComponent<ReinforcementSoldier>();
        if (s == null) { Destroy(sObj); return; }
        s.isPermanent = true;
        s.SetBarracks(this);
        ApplyStats(s, GetCurrentUpgrade(), false);
        s.SetRallyPosition(SlotPosition(i));
        slots[i] = s;
    }

    private void ApplyStats(ReinforcementSoldier s, TowerUpgrade u, bool healFull)
    {
        if (s == null) return;
        float hp = u != null && u.soldierHP > 0f ? u.soldierHP : 120f;
        s.maxHealth = hp;
        if (healFull) s.currentHealth = hp;
        s.armor = Mathf.Clamp(u != null ? u.soldierArmor : 0.1f, 0f, 0.8f);
        s.attackDamage = soldierDamage + damagePerLevel * currentLevel;
        s.regenRate = soldierRegen + currentLevel;
    }

    /// <summary>Tìm điểm gần tháp nhất trên đường đi (mọi tuyến) làm điểm tập kết, để lính chặn đúng đường.</summary>
    private void ResolveRally()
    {
        Vector2 me = transform.position;
        Vector2 point = me + Vector2.down * 0.6f;
        Vector2 dir = Vector2.right;
        bool found = false;

        if (LevelManager.main != null)
        {
            LevelManager.main.EnsurePathAndWaypoints();
            float best = float.MaxValue;
            int rc = LevelManager.main.RouteCount;
            for (int r = 0; r < rc; r++)
            {
                Transform[] route = LevelManager.main.GetRoute(r);
                if (route == null || route.Length < 2) continue;
                for (int i = 0; i + 1 < route.Length; i++)
                {
                    if (route[i] == null || route[i + 1] == null) continue;
                    Vector2 a = route[i].position, b = route[i + 1].position;
                    Vector2 ab = b - a;
                    float len2 = ab.sqrMagnitude;
                    if (len2 < 0.000001f) continue;
                    float t = Mathf.Clamp01(Vector2.Dot(me - a, ab) / len2);
                    Vector2 p = a + ab * t;
                    float d = (me - p).sqrMagnitude;
                    if (d < best) { best = d; point = p; dir = ab.normalized; found = true; }
                }
            }
        }

        if (!found && rallyPoint != null) point = rallyPoint.position;
        rallyBase = point;
        rallyDir = dir;
    }

    /// <summary>Điểm đứng của lính thứ i: 1 lính = làn giữa, 2 lính = hai làn rìa, 3+ lính = đủ 3 làn (hàng sau lùi dọc đường).</summary>
    private Vector2 SlotPosition(int i)
    {
        int n = slots.Length;
        int lane = n == 1 ? 1 : (n == 2 ? (i == 0 ? 0 : 2) : i % 3);
        int rank = n > 3 ? i / 3 : 0;
        Vector2 normal = new Vector2(-rallyDir.y, rallyDir.x);
        return rallyBase + normal * GameScale.LaneOffset(lane) - rallyDir * (0.45f * rank);
    }

    /// <summary>
    /// Được gọi bởi lính tiếp viện (ReinforcementSoldier) khi tử trận để bắt đầu đếm ngược hồi sinh
    /// </summary>
    public void ReportDeath(GameObject soldier)
    {
        int slot = -1;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null && slots[i].gameObject == soldier) { slot = i; break; }
        }
        if (slot < 0)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if ((slots[i] == null || !slots[i].IsAlive) && !slotPending[i]) { slot = i; break; }
            }
        }
        if (slot < 0) return;

        slots[slot] = null;
        slotPending[slot] = true;
        StartCoroutine(RespawnSlotRoutine(slot));
    }

    public void ReportDeath(ReinforcementSoldier soldier)
    {
        if (soldier != null) ReportDeath(soldier.gameObject);
        else ReportDeath((GameObject)null);
    }

    private IEnumerator RespawnSlotRoutine(int slot)
    {
        TowerUpgrade u = GetCurrentUpgrade();
        float wait = u != null && u.soldierRespawnTime > 0f ? u.soldierRespawnTime : 10f;
        yield return new WaitForSeconds(wait);
        if (slot < slotPending.Length)
        {
            slotPending[slot] = false;
            if (slot < slots.Length && slots[slot] == null)
            {
                ResolveRally();
                SpawnInSlot(slot);
            }
        }
    }

    /// <summary>Giữ lại cho tương thích: triệu hồi 1 lính vào ô trống đầu tiên.</summary>
    public void SpawnSoldier()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null && !slotPending[i]) { ResolveRally(); SpawnInSlot(i); return; }
        }
    }

    private TowerUpgrade[] GetUpgradeList()
    {
        if (upgrades != null && upgrades.Length > 0) return upgrades;
        if (towerData != null && towerData.upgrades != null && towerData.upgrades.Length > 0) return towerData.upgrades;
        return null;
    }

    public int GetSellRefund() => (int)(GetCurrentUpgrade() != null ? GetCurrentUpgrade().cost * 0.7f : buildCost * 0.7f);

    public int GetMaxLevel()
    {
        var list = GetUpgradeList();
        return list != null ? list.Length : 0;
    }

    public TowerUpgrade GetCurrentUpgrade()
    {
        var list = GetUpgradeList();
        if (list != null && currentLevel >= 0 && currentLevel < list.Length)
            return list[currentLevel];
        return new TowerUpgrade { cost = buildCost, range = 3.5f, soldierCount = 2, soldierHP = 120f };
    }

    public TowerUpgrade GetNextUpgrade()
    {
        var list = GetUpgradeList();
        return (list != null && currentLevel + 1 < list.Length) ? list[currentLevel + 1] : null;
    }

    public void SetPlot(Plot plot) => this.plot = plot;
    public float GetRange() => GetCurrentUpgrade() != null ? GetCurrentUpgrade().range : 3.5f;
    public int GetCurrentLevel() => currentLevel;
    public float GetCreationTime() => creationTime;
}
