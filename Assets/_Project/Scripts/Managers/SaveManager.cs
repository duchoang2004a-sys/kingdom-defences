using System;
using System.Collections.Generic;
using UnityEngine;

public class SaveManager : MonoBehaviour
{
    public static SaveManager instance;

    [Header("Progression Data")]
    public int highestUnlockedLevel = 1;
    public int clearedMaps = 0;
    public int totalCups = 0;

    [Header("Currencies & Consumables (Gem Shop & Oils)")]
    public int Gems
    {
        get => PlayerPrefs.GetInt("PlayerGems", 250);
        set { PlayerPrefs.SetInt("PlayerGems", Mathf.Max(0, value)); PlayerPrefs.Save(); }
    }

    public int DamageOils
    {
        get => PlayerPrefs.GetInt("Oil_Damage", 2);
        set { PlayerPrefs.SetInt("Oil_Damage", Mathf.Max(0, value)); PlayerPrefs.Save(); }
    }

    public int FireRateOils
    {
        get => PlayerPrefs.GetInt("Oil_FireRate", 2);
        set { PlayerPrefs.SetInt("Oil_FireRate", Mathf.Max(0, value)); PlayerPrefs.Save(); }
    }

    public int BossReduceOils
    {
        get => PlayerPrefs.GetInt("Oil_BossReduce", 1);
        set { PlayerPrefs.SetInt("Oil_BossReduce", Mathf.Max(0, value)); PlayerPrefs.Save(); }
    }

    public int PoisonOils
    {
        get => PlayerPrefs.GetInt("Oil_Poison", 2);
        set { PlayerPrefs.SetInt("Oil_Poison", Mathf.Max(0, value)); PlayerPrefs.Save(); }
    }

    public int StunResistOils
    {
        get => PlayerPrefs.GetInt("Oil_StunResist", 1);
        set { PlayerPrefs.SetInt("Oil_StunResist", Mathf.Max(0, value)); PlayerPrefs.Save(); }
    }

    [Header("One-time Rewards (Bitmask)")]
    private int RewardBitmask
    {
        get => PlayerPrefs.GetInt("OneTimeRewardBitmask", 0);
        set { PlayerPrefs.SetInt("OneTimeRewardBitmask", value); PlayerPrefs.Save(); }
    }

    [Header("Hero Progression")]
    public string selectedHeroId = "Knight";

    private const int MaxTrackedLevels = 20;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        LoadProgress();
    }

    public void SaveLevelProgress(int levelNumber, int newCups)
    {
        if (levelNumber < 1) levelNumber = 1;

        int prevCups = PlayerPrefs.GetInt($"Level_{levelNumber}_Cups", 0);
        if (newCups > prevCups)
        {
            PlayerPrefs.SetInt($"Level_{levelNumber}_Cups", newCups);
        }

        // Đánh dấu level này đã hoàn thành ít nhất 1 lần
        PlayerPrefs.SetInt($"Level_{levelNumber}_Cleared", 1);

        // Mở khóa level tiếp theo
        if (levelNumber >= highestUnlockedLevel)
        {
            highestUnlockedLevel = levelNumber + 1;
            PlayerPrefs.SetInt("HighestUnlockedLevel", highestUnlockedLevel);
        }

        RecalculateTotals();
        
        // Award Gems for First-time win and Star benchmarks
        AwardGemsForLevel(levelNumber, newCups);

        PlayerPrefs.Save();
        Debug.Log($"[SaveManager] Level {levelNumber} saved. Cups: {newCups} (Best: {Math.Max(prevCups, newCups)}), Total Cups: {totalCups}, Unlocked Level: {highestUnlockedLevel}");
    }

    private void AwardGemsForLevel(int level, int cups)
    {
        int mask = RewardBitmask;
        int levelBit = 1 << (level - 1);
        int star2Bit = 1 << (level + 10);
        int star3Bit = 1 << (level + 20);

        // 1. First time clearing the map
        if ((mask & levelBit) == 0)
        {
            Gems += 50;
            mask |= levelBit;
            Debug.Log($"<color=cyan>[Gems]</color> First win reward (+50) for Level {level}");
        }

        // 2. Reaching 2 stars (only once per map)
        if (cups >= 2 && (mask & star2Bit) == 0)
        {
            Gems += 30;
            mask |= star2Bit;
            Debug.Log($"<color=cyan>[Gems]</color> 2-Star milestone reward (+30) for Level {level}");
        }

        // 3. Reaching 3 stars (only once per map)
        if (cups >= 3 && (mask & star3Bit) == 0)
        {
            Gems += 70;
            mask |= star3Bit;
            Debug.Log($"<color=cyan>[Gems]</color> 3-Star perfect reward (+70) for Level {level}");
        }

        RewardBitmask = mask;
    }

    public void SaveProgress(int newCups)
    {
        int currentLevel = GetCurrentLevelNumber();
        SaveLevelProgress(currentLevel, newCups);
    }

    public int GetCurrentLevelNumber()
    {
        // 1. Thử lấy từ LevelManager an toàn (tránh lỗi compile CS1061 nếu LevelManager chưa reload)
        if (LevelManager.main != null)
        {
            try
            {
                var lmType = LevelManager.main.GetType();
                var field = lmType.GetField("levelNumber");
                if (field != null)
                {
                    int val = Convert.ToInt32(field.GetValue(LevelManager.main));
                    if (val > 0) return val;
                }
                var prop = lmType.GetProperty("levelNumber");
                if (prop != null)
                {
                    int val = Convert.ToInt32(prop.GetValue(LevelManager.main, null));
                    if (val > 0) return val;
                }
            }
            catch {}
        }

        // 2. Nhận diện từ tên Scene hiện tại (VD: "Level_1", "Level2", "Map3")
        try
        {
            string scName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (!string.IsNullOrEmpty(scName))
            {
                System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(scName, @"\d+");
                if (match.Success && int.TryParse(match.Value, out int parsed))
                {
                    return parsed;
                }
            }
        }
        catch {}

        return 1;
    }

    public bool IsLevelUnlocked(int levelNumber)
    {
        if (levelNumber <= 1) return true;
        return levelNumber <= highestUnlockedLevel;
    }

    public int GetLevelCups(int levelNumber)
    {
        return PlayerPrefs.GetInt($"Level_{levelNumber}_Cups", 0);
    }

    public void RecalculateTotals()
    {
        int sumCups = 0;
        int uniqueCleared = 0;

        for (int i = 1; i <= MaxTrackedLevels; i++)
        {
            int c = PlayerPrefs.GetInt($"Level_{i}_Cups", 0);
            sumCups += c;
            if (PlayerPrefs.GetInt($"Level_{i}_Cleared", 0) == 1)
            {
                uniqueCleared++;
            }
        }

        totalCups = sumCups;
        clearedMaps = uniqueCleared;

        PlayerPrefs.SetInt("ClearedMaps", clearedMaps);
        PlayerPrefs.SetInt("TotalCups", totalCups);
    }

    public void SelectHero(string heroId)
    {
        if (string.IsNullOrEmpty(heroId)) heroId = "Knight";
        selectedHeroId = heroId;
        PlayerPrefs.SetString("SelectedHeroId", selectedHeroId);
        PlayerPrefs.Save();
        Debug.Log($"[SaveManager] Đã chọn Hero: {selectedHeroId}");
    }

    public bool IsHeroUnlocked(string heroId, int requiredCups = 0)
    {
        if (string.IsNullOrEmpty(heroId) || heroId == "Knight") return true;
        if (PlayerPrefs.GetInt($"Hero_{heroId}_Unlocked", 0) == 1) return true;
        return totalCups >= requiredCups;
    }

    public void UnlockHero(string heroId)
    {
        PlayerPrefs.SetInt($"Hero_{heroId}_Unlocked", 1);
        PlayerPrefs.Save();
    }

    public void LoadProgress()
    {
        highestUnlockedLevel = PlayerPrefs.GetInt("HighestUnlockedLevel", 1);
        selectedHeroId = PlayerPrefs.GetString("SelectedHeroId", "Knight");
        RecalculateTotals();
    }
}

