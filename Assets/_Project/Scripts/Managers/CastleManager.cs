using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Quản lý Máu Lâu Đài (CastleManager) - Chuẩn Đề Bài Kingdom Rush (Hero Tactics):
/// - Máu lâu đài hỗ trợ dạng float (0.0f - 100.0f).
/// - Cập nhật Bậc Lâu Đài (Castle Tier) dựa trên Cấp Độ Trung Bình của toàn bộ tháp (Average Tower Level).
/// - Hồi phục máu tự động theo nhịp (Peace-time Auto-Regen 2% - 4% mỗi nhịp) khi không bị tấn công trong 6s.
/// - Cập nhật code xử lý trạng thái Thua (Game Over) khi Máu Lâu Đài <= 0.
/// </summary>
public class CastleManager : MonoBehaviour
{
    public static CastleManager main;

    [Header("Castle Health Data (Float 0 - 100)")]
    public float maxHealth = 100f;
    public float currentHealth = 100f;

    [Header("Tower Average Level & Castle Tier")]
    public float averageTowerLevel = 0f;
    public int currentCastleTier = 0; // 0: Chưa đủ bậc, 1: Bậc 1 (Avg 1.0-1.9), 2: Bậc 2 (Avg 2.0-2.9), 3: Bậc 3 (Avg 3.0)
    public int validTowersCount = 0;

    [Header("Regen Settings (2-4% Mỗi Nhịp)")]
    [Tooltip("Thời gian yên bình không nhận sát thương trước khi bắt đầu hồi máu (giây)")]
    public float peaceTimeRequired = 6f;
    [Tooltip("Chu kỳ mỗi nhịp hồi máu (giây)")]
    public float regenTickInterval = 5f;

    public float lastDamageTime = -10f;
    private float lastRegenTickTime = 0f;
    private float nextTierCheckTime = 0f;

    public event Action<float, float> OnCastleHealthChanged;
    public event Action<int, float> OnCastleTierChanged;
    public event Action OnCastleDestroyed;

    public float HealthPercentage => maxHealth > 0 ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
    public bool IsDestroyed => currentHealth <= 0f;

    private void Awake()
    {
        if (main != null && main != this)
        {
            Destroy(gameObject);
            return;
        }
        main = this;
    }

    private void Start()
    {
        currentHealth = maxHealth;
        CalculateAverageTowerLevelAndTier();
        NotifyHealthChanged();
    }

    private void Update()
    {
        // 1. Định kỳ 1.0s quét và tính Cấp Độ Trung Bình của Tháp & Bậc Lâu Đài
        if (Time.time >= nextTierCheckTime)
        {
            nextTierCheckTime = Time.time + 1.0f;
            CalculateAverageTowerLevelAndTier();
        }

        // 2. Tự động hồi phục Máu Lâu Đài theo Nhịp (2% - 4% mỗi nhịp 5s) khi ở trạng thái Hòa Bình
        if (Time.time - lastDamageTime >= peaceTimeRequired && currentHealth > 0f && currentHealth < maxHealth)
        {
            if (Time.time - lastRegenTickTime >= regenTickInterval)
            {
                lastRegenTickTime = Time.time;
                PerformRegenTick();
            }
        }
    }

    /// <summary>
    /// Tính Cấp Độ Trung Bình của toàn bộ Tháp đang hoạt động và cập nhật Bậc Lâu Đài
    /// </summary>
    public void CalculateAverageTowerLevelAndTier()
    {
        Plot[] plots = Object.FindObjectsByType<Plot>(FindObjectsSortMode.None);
        int count = 0;
        float totalLevelsSum = 0f;

        for (int i = 0; i < plots.Length; i++)
        {
            Plot p = plots[i];
            if (p == null) continue;

            IBaseTower t = p.GetTower();
            if (t != null)
            {
                // Ân hạn 25s cho tháp mới xây Cấp 1 không làm kéo sụt Bậc Lâu đài
                int towerLvlIndex = t.GetCurrentLevel();
                if (towerLvlIndex == 0 && Time.time - t.GetCreationTime() < 25f)
                {
                    continue;
                }

                count++;
                int towerLvl = towerLvlIndex + 1; // 1, 2, 3
                totalLevelsSum += towerLvl;
            }
        }

        validTowersCount = count;
        float newAvgLevel = 0f;
        int newTier = 0;

        // Yêu cầu tối thiểu 3 tháp đã xây để bắt đầu kích hoạt Bậc Lâu Đài
        if (validTowersCount >= 3)
        {
            newAvgLevel = totalLevelsSum / validTowersCount;
            if (newAvgLevel >= 3.0f) newTier = 3;
            else if (newAvgLevel >= 2.0f) newTier = 2;
            else if (newAvgLevel >= 1.0f) newTier = 1;
        }

        if (Mathf.Abs(newAvgLevel - averageTowerLevel) > 0.05f || newTier != currentCastleTier)
        {
            averageTowerLevel = newAvgLevel;
            currentCastleTier = newTier;

            OnCastleTierChanged?.Invoke(currentCastleTier, averageTowerLevel);
            if (LevelManager.main != null)
            {
                LevelManager.main.currentCastleTier = currentCastleTier;
            }
            Debug.Log($"<color=yellow>[CastleManager]</color> Bậc Lâu Đài: Bậc {currentCastleTier} (Cấp Tháp Trung Bình: {averageTowerLevel:F2}, Số Tháp: {validTowersCount})");
        }
    }

    /// <summary>
    /// Thực hiện 1 nhịp hồi máu Lâu Đài (2% - 4% theo Bậc Lâu Đài)
    /// </summary>
    private void PerformRegenTick()
    {
        float regenPercent = 0f;
        if (currentCastleTier == 3) regenPercent = 0.04f;      // 4% mỗi nhịp
        else if (currentCastleTier == 2) regenPercent = 0.03f; // 3% mỗi nhịp
        else if (currentCastleTier == 1) regenPercent = 0.02f; // 2% mỗi nhịp

        if (regenPercent > 0f)
        {
            float amount = maxHealth * regenPercent;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);

            NotifyHealthChanged();
            Debug.Log($"<color=green>[CastleRegen]</color> Hồi phục +{amount:F1} HP (+{regenPercent * 100f:F0}%). Máu Lâu Đài hiện tại: {currentHealth:F1}/{maxHealth}");
        }
    }

    /// <summary>
    /// Nhận sát thương vào Lâu Đài và kiểm tra điều kiện Thua (Máu <= 0)
    /// </summary>
    public void TakeDamage(float damageAmount)
    {
        if (currentHealth <= 0f) return;

        lastDamageTime = Time.time;
        currentHealth = Mathf.Max(0f, currentHealth - damageAmount);

        NotifyHealthChanged();

        Debug.Log($"<color=red>[CastleDamage]</color> Lâu Đài mất -{damageAmount:F1} HP. Máu còn lại: {currentHealth:F1}/{maxHealth}");

        if (currentHealth <= 0f)
        {
            HandleGameOver();
        }
    }

    public void Heal(float healAmount)
    {
        if (currentHealth <= 0f) return;

        currentHealth = Mathf.Min(maxHealth, currentHealth + healAmount);
        NotifyHealthChanged();
    }

    private void HandleGameOver()
    {
        Debug.Log("<color=red><b>[GAME OVER] Máu Lâu Đài đã về 0! Người chơi thất bại!</b></color>");
        OnCastleDestroyed?.Invoke();

        if (LevelManager.main != null)
        {
            LevelManager.main.EndGame();
        }
    }

    private void NotifyHealthChanged()
    {
        OnCastleHealthChanged?.Invoke(currentHealth, maxHealth);

        if (LevelManager.main != null)
        {
            LevelManager.main.currentCastleHealth = currentHealth;
            LevelManager.main.livesCount = Mathf.CeilToInt(currentHealth);
            LevelManager.main.NotifyHealthAndLivesChanged();
        }
    }
}
