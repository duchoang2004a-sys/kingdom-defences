using System.Collections.Generic;
using UnityEngine;

// --- 1. DỮ LIỆU ANH HÙNG ---
[CreateAssetMenu(fileName = "NewHero", menuName = "TD/Hero Definition")]
public class HeroDefinition : ScriptableObject
{
    public string heroId;
    public string heroName;
    public Sprite portrait;
    public float baseHP = 100f;
    public float baseDamage = 15f;
    [TextArea] public string description;
}

// --- 2. DỮ LIỆU NHIỆM VỤ ---
public enum MissionType
{
    HoldTime,
    KillBosses,
    ProtectCastle,
    SurviveWaves
}

public enum SecondaryMissionType
{
    KillNBosses,
    SurviveNMinutes,
    KeepCastleHpPercent,
    KillNEnemies,
    BuildNTowers
}

[System.Serializable]
public class SecondaryMissionData
{
    public string description = "Mục tiêu phụ";
    public SecondaryMissionType type;
    public float targetValue = 1f;
    public float currentProgress = 0f;
    public bool isCompleted = false;
}

[System.Serializable]
public class LevelMissionData
{
    public string missionTitle = "Phòng Thủ";
    public MissionType missionType = MissionType.SurviveWaves;
    public float targetValue = 5f;
    public int targetKills = 20;
    public float survivalMinutes = 3f;
    public List<SecondaryMissionData> secondaryMissions = new List<SecondaryMissionData>();
}

public static class GameMissionDatabase
{
    public static LevelMissionData GetMissionForLevel(int levelNum)
    {
        LevelMissionData m = new LevelMissionData();
        m.missionTitle = $"Màn {levelNum}: Phòng Thủ";
        m.missionType = MissionType.SurviveWaves;
        m.targetValue = 5f;
        m.secondaryMissions = new List<SecondaryMissionData>
        {
            new SecondaryMissionData { description = "Giữ Máu Lâu Đài >= 80%", type = SecondaryMissionType.KeepCastleHpPercent, targetValue = 0.80f },
            new SecondaryMissionData { description = "Diệt 20 Kẻ Địch", type = SecondaryMissionType.KillNEnemies, targetValue = 20f }
        };
        return m;
    }
}

// --- 3. DỮ LIỆU NÂNG CẤP THÁP ---
[System.Serializable]
public class TowerUpgrade
{
    public string levelName;
    public int cost;
    public float damage;
    public float range;
    public float fireRate;
    public int bulletsPerShot = 1;
    public float bulletScale = 1f;
    public float aoeRadius = 0f;
    public float slowDuration = 0f;

    [Header("Chỉ số lính (Barracks)")]
    public int soldierCount = 2;
    public float soldierHP = 120f;
    public float soldierArmor = 0.1f;
    public float soldierRespawnTime = 10f;
}

// --- 4. DỮ LIỆU GỐC THÁP ---
[CreateAssetMenu(fileName = "NewTowerData", menuName = "TD/Tower Data")]
public class TowerData : ScriptableObject
{
    public string towerName;
    public TowerUpgrade[] upgrades;
}

// --- 5. ENUM HIỆU ỨNG TRẠNG THÁI (KHẮC PHỤC 41 LỖI STATUS EFFECT TYPE) ---
public enum StatusEffectType
{
    None,
    Slow,
    Freeze,
    Stun,
    Burn,
    ArmorBreak,
    Poison,
    Wounded
}

public enum StatusType
{
    None,
    Slow,
    Freeze,
    Stun,
    Burn,
    ArmorBreak,
    Poison,
    Wounded
}