using UnityEngine;

/// <summary>
/// Bảng Cân Bằng Toàn Cục (GameDataBalancer) - Part B3:
/// Lưu trữ các hệ số nhân cho 8 màn chơi chính thức.
/// </summary>
public static class GameDataBalancer
{
    public struct LevelBalance
    {
        public int maxWaves;
        public int baseEnemies;
        public float hpPerWave;
        public float goldMultiplier;
        public int startingGold;
        public int startingLives;
    }

    private static readonly LevelBalance[] balanceTable = new LevelBalance[]
    {
        new LevelBalance { maxWaves = 8,  baseEnemies = 11, hpPerWave = 0.16f, goldMultiplier = 1.0f, startingGold = 250, startingLives = 20 },
        new LevelBalance { maxWaves = 10, baseEnemies = 8,  hpPerWave = 0.16f, goldMultiplier = 0.9f, startingGold = 300, startingLives = 20 },
        new LevelBalance { maxWaves = 8,  baseEnemies = 10, hpPerWave = 0.20f, goldMultiplier = 0.8f, startingGold = 300, startingLives = 20 },
        new LevelBalance { maxWaves = 10, baseEnemies = 8,  hpPerWave = 0.14f, goldMultiplier = 0.8f, startingGold = 320, startingLives = 20 },
        new LevelBalance { maxWaves = 9,  baseEnemies = 6,  hpPerWave = 0.14f, goldMultiplier = 0.7f, startingGold = 350, startingLives = 20 },
        new LevelBalance { maxWaves = 10, baseEnemies = 6,  hpPerWave = 0.06f, goldMultiplier = 0.7f, startingGold = 380, startingLives = 20 },
        new LevelBalance { maxWaves = 12, baseEnemies = 5,  hpPerWave = 0.03f, goldMultiplier = 0.9f, startingGold = 700, startingLives = 25 },
        new LevelBalance { maxWaves = 12, baseEnemies = 4,  hpPerWave = 0.05f, goldMultiplier = 0.8f, startingGold = 800, startingLives = 25 }
    };

    public static LevelBalance GetBalance(int levelNumber)
    {
        int index = Mathf.Clamp(levelNumber - 1, 0, balanceTable.Length - 1);
        return balanceTable[index];
    }

    // Phương thức bổ sung phục vụ EnemyHealth.cs
    public static float GetDifficultyGold(int levelNumber)
    {
        return GetBalance(levelNumber).goldMultiplier;
    }

    public static (float armor, float magicResist, float statusResist) GetEnemyStats(string enemyName)
    {
        string name = enemyName.ToLower();
        if (name.Contains("orc")) return (0.25f, 0f, 0f);
        if (name.Contains("troll")) return (0.15f, 0f, 0f);
        if (name.Contains("dark elf") || name.Contains("darkelf")) return (0f, 0.40f, 0f);
        if (name.Contains("demon")) return (0.10f, 0.20f, 0f);
        if (name.Contains("golem")) return (0.50f, 0f, 0f);
        if (name.Contains("dragon")) return (0.30f, 0.30f, 0.60f);
        return (0f, 0f, 0f);
    }
}