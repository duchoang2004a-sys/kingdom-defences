using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quản lý Hệ thống Dầu Tăng Cường (Consumable Oils System - Step 4):
/// - Dầu Tăng Sát Thương (+30% DMG cho tất cả tháp trong 20s)
/// - Dầu Tốc Độ Bắn (+40% Fire Rate cho tất cả tháp trong 20s)
/// - Dầu Độc Tố (Mọi đòn bắn tháp gây hiệu ứng Độc DoT trong 20s)
/// - Dầu Xóa Cấp Boss (Giảm 1 bậc Boss & trừ 25% máu hiện tại/tối đa tương ứng chứ không giết ngay)
/// </summary>
public class ConsumableOilManager : MonoBehaviour
{
    public static ConsumableOilManager main;

    [Header("Active Timers (seconds remaining)")]
    public float damageOilRemaining = 0f;
    public float fireRateOilRemaining = 0f;
    public float poisonOilRemaining = 0f;

    public static float DamageMultiplier => (main != null && main.damageOilRemaining > 0f) ? 1.30f : 1.00f;
    public static float FireRateMultiplier => (main != null && main.fireRateOilRemaining > 0f) ? 1.40f : 1.00f;
    public static bool IsPoisonActive => main != null && main.poisonOilRemaining > 0f;

    private void Awake()
    {
        main = this;
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        if (damageOilRemaining > 0f) damageOilRemaining = Mathf.Max(0f, damageOilRemaining - dt);
        if (fireRateOilRemaining > 0f) fireRateOilRemaining = Mathf.Max(0f, fireRateOilRemaining - dt);
        if (poisonOilRemaining > 0f) poisonOilRemaining = Mathf.Max(0f, poisonOilRemaining - dt);
    }

    public bool UseDamageOil()
    {
        if (SaveManager.instance != null && SaveManager.instance.DamageOils <= 0)
        {
            Debug.LogWarning("[OilManager] Không đủ Dầu Tăng Sát Thương!");
            return false;
        }

        if (SaveManager.instance != null) SaveManager.instance.DamageOils--;
        damageOilRemaining = 20f;
        Debug.Log("<color=red>[Dầu Tăng Sát Thương]</color> Kích hoạt! +30% Sát thương cho tất cả tháp trong 20s.");
        return true;
    }

    public bool UseFireRateOil()
    {
        if (SaveManager.instance != null && SaveManager.instance.FireRateOils <= 0)
        {
            Debug.LogWarning("[OilManager] Không đủ Dầu Tốc Độ Bắn!");
            return false;
        }

        if (SaveManager.instance != null) SaveManager.instance.FireRateOils--;
        fireRateOilRemaining = 20f;
        Debug.Log("<color=yellow>[Dầu Tốc Độ Bắn]</color> Kích hoạt! +40% Tốc độ bắn cho tất cả tháp trong 20s.");
        return true;
    }

    public bool UsePoisonOil()
    {
        if (SaveManager.instance != null && SaveManager.instance.PoisonOils <= 0)
        {
            Debug.LogWarning("[OilManager] Không đủ Dầu Độc Tố!");
            return false;
        }

        if (SaveManager.instance != null) SaveManager.instance.PoisonOils--;
        poisonOilRemaining = 20f;
        Debug.Log("<color=green>[Dầu Độc Tố]</color> Kích hoạt! Đòn bắn tháp tẩm độc gây sát thương DoT trong 20s.");
        return true;
    }

    public bool UseBossLevelReductionOil()
    {
        if (SaveManager.instance != null && SaveManager.instance.BossReduceOils <= 0)
        {
            Debug.LogWarning("[OilManager] Không đủ Dầu Xóa Cấp Boss!");
            return false;
        }

        // Tìm Boss đang sống trong EnemyRegistry
        var enemies = EnemyRegistry.ActiveEnemies;
        EnemyHealth targetBossHealth = null;

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyHealth eh = enemies[i];
            if (eh == null || !eh.gameObject.activeInHierarchy || eh.CurrentHealth <= 0) continue;

            Enemy enemyComp = eh.GetComponent<Enemy>();
            if (enemyComp != null && enemyComp.IsBoss)
            {
                targetBossHealth = eh;
                break;
            }
        }

        if (targetBossHealth == null)
        {
            Debug.LogWarning("[OilManager] Không tìm thấy Boss nào trên bản đồ để dùng Dầu Xóa Cấp!");
            return false;
        }

        if (SaveManager.instance != null) SaveManager.instance.BossReduceOils--;

        Enemy bossEnemy = targetBossHealth.GetComponent<Enemy>();
        if (bossEnemy != null)
        {
            bossEnemy.bossTier = Mathf.Max(1, bossEnemy.bossTier - 1);
        }

        // Giảm 25% máu hiện tại và tối đa tương ứng chứ không diệt ngay
        targetBossHealth.ReduceHealthPercent(0.25f);

        Debug.Log($"<color=purple>[Dầu Xóa Cấp Boss]</color> Đã hạ 1 cấp Boss và giảm 25% HP của {bossEnemy?.EnemyName ?? "Boss"}!");
        return true;
    }
}
