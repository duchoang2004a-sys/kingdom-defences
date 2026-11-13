using UnityEngine;
using System.Collections;
using System.Collections.Generic;

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

    [HideInInspector] public Plot plot;
    [HideInInspector] public float creationTime;

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
    }

    /// <summary>
    /// Được gọi bởi lính tiếp viện (ReinforcementSoldier) khi tử trận để bắt đầu đếm ngược hồi sinh
    /// </summary>
    public void ReportDeath(GameObject soldier)
    {
        StartCoroutine(RespawnSoldierRoutine());
    }

    public void ReportDeath(ReinforcementSoldier soldier)
    {
        if (soldier != null) ReportDeath(soldier.gameObject);
        else StartCoroutine(RespawnSoldierRoutine());
    }

    private IEnumerator RespawnSoldierRoutine()
    {
        yield return new WaitForSeconds(10f); // Chu kỳ hồi sinh lính chuẩn Kingdom Rush (10s)
        SpawnSoldier();
    }

    public void SpawnSoldier()
    {
        if (soldierPrefab == null) return;
        Vector3 spawnPos = rallyPoint != null ? rallyPoint.position : (transform.position + Vector3.down * 0.5f);
        GameObject sObj = Instantiate(soldierPrefab, spawnPos, Quaternion.identity);
        ReinforcementSoldier s = sObj.GetComponent<ReinforcementSoldier>();
        if (s != null)
        {
            s.isPermanent = true;
            s.SetBarracks(this);
            if (rallyPoint != null) s.SetRallyPosition(rallyPoint.position);
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
