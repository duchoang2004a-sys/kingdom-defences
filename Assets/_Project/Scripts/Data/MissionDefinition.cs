using System;
using System.Collections.Generic;
using UnityEngine;

public enum GoalType
{
    SurviveNWaves,          // Sống sót qua N wave
    HoldSeconds,            // Trụ vững trong T giây
    KillNEnemies,           // Diệt N quái (không tính quái lọt)
    KillNBosses,            // Diệt N boss
    KillBossBeforeDeadline, // Diệt boss trước mốc thời gian T
    KeepCastleHpPercent,    // Giữ máu lâu đài >= X% (Mục tiêu phụ)
    NoSpellOrItemUsed       // Chưa dùng phép hoặc item (Mục tiêu phụ)
}

public enum StageCombineType
{
    RequireAll, // Cần hoàn thành TẤT CẢ mục tiêu trong Stage (AND)
    RequireAny  // Chỉ cần hoàn thành MỘT TRONG CÁC mục tiêu trong Stage (OR)
}

[Serializable]
public class MissionGoalData
{
    public GoalType type = GoalType.SurviveNWaves;
    public string enemyId = ""; // Dùng nếu lọc loại boss/quái cụ thể
    public float targetValue = 3f; // N wave, T giây, N boss, hoặc X%
    public float deadlineTime = 0f; // Hạn chót cho KillBossBeforeDeadline

    [HideInInspector] public float currentProgress = 0f;
    [HideInInspector] public bool isCompleted = false;

    public MissionGoalData() { }

    public MissionGoalData(GoalType type, float targetVal, string enemyId = "", float deadline = 0f)
    {
        this.type = type;
        this.targetValue = targetVal;
        this.enemyId = enemyId;
        this.deadlineTime = deadline;
        this.currentProgress = 0f;
        this.isCompleted = false;
    }
}

[Serializable]
public class MissionStageData
{
    public string stageTitle = "Giai Đoạn 1";
    public StageCombineType combineType = StageCombineType.RequireAll;
    public List<MissionGoalData> goals = new List<MissionGoalData>();

    public bool IsStageCompleted()
    {
        if (goals == null || goals.Count == 0) return true;

        if (combineType == StageCombineType.RequireAll)
        {
            foreach (var g in goals)
            {
                if (g != null && !g.isCompleted) return false;
            }
            return true;
        }
        else
        {
            foreach (var g in goals)
            {
                if (g != null && g.isCompleted) return true;
            }
            return false;
        }
    }
}

[Serializable]
public class SecondaryGoalData
{
    public string description = "Mục tiêu phụ";
    public MissionGoalData goal = new MissionGoalData();
    public int starWeight = 1; // 1 sao cho mục tiêu phụ này
    public int gemReward = 25; // Phần thưởng Gem thưởng lần đầu
}

[Serializable]
public class BossScheduleRule
{
    public string bossEnemyId = "Shaman";
    public GameObject bossPrefab;
    public int minWaveInterval = 2; // Tối thiểu sau k wave
    public int maxWaveInterval = 3; // Tối đa sau k wave
    public int totalBossesToSpawn = 2;
    public int seed = 12345;
}

[Serializable]
public class MissionRulesData
{
    public List<string> allowedHeroIds = new List<string>();
    public List<string> bannedItemIds = new List<string>();
    public int overrideStartingGold = -1; // -1 là dùng giá trị mặc định của LevelManager
}

/// <summary>
/// Cấu trúc Dữ Liệu MissionDefinition (Part A2 - Chuẩn Thiết Kế Mới):
/// Tách biệt Asset cố định, Trạng thái chạy trong trận, và Kết quả lưu trong SaveManager.
/// </summary>
[CreateAssetMenu(fileName = "NewMissionDefinition", menuName = "Tower Defense/Mission Definition")]
public class MissionDefinition : ScriptableObject
{
    [Header("Identifiability")]
    public string missionId = "Mission_Level_1";
    public int levelNumber = 1;
    public string title = "Khởi Đầu Vương Quốc";
    [TextArea(2, 4)]
    public string description = "Mô tả nhiệm vụ hiển thị ở Màn chọn Level";

    [Header("Backward Compatibility")]
    [Tooltip("Nếu không bật, level dùng luật mặc định = sống sót hết wave")]
    public bool useCustomMission = true;

    [Header("Primary Mission Stages (Chuỗi Giai Đoạn Thắng)")]
    public List<MissionStageData> stages = new List<MissionStageData>();

    [Header("Secondary Objectives (Tối đa 3 mục tiêu phụ)")]
    public List<SecondaryGoalData> secondaryGoals = new List<SecondaryGoalData>();

    [Header("Boss Schedule Rules (Lịch sinh Boss quy tắc)")]
    public BossScheduleRule bossRule = new BossScheduleRule();

    [Header("Level Rules & Modifiers")]
    public MissionRulesData rules = new MissionRulesData();

    [Header("First-Time Gem Rewards (Chống Farm)")]
    public int firstTimeWinGems = 50;
    public int gemsPerStar = 25;
}

/// <summary>
/// Trình truy xuất dữ liệu Mission cơ sở cho cả 8 Level
/// </summary>
public static class MissionDatabase
{
    private static Dictionary<int, MissionDefinition> cache = new Dictionary<int, MissionDefinition>();

    public static MissionDefinition GetMissionForLevel(int levelNum)
    {
        if (cache.ContainsKey(levelNum)) return cache[levelNum];

        MissionDefinition m = ScriptableObject.CreateInstance<MissionDefinition>();
        m.levelNumber = levelNum;
        m.missionId = $"Mission_Level_{levelNum}";
        m.useCustomMission = true;

        switch (levelNum)
        {
            case 1:
                m.title = "Màn 1: Cổng Thành Goblin";
                m.description = "Sống sót qua 3 Wave Goblin để bảo vệ vương quốc.";
                MissionStageData st1_1 = new MissionStageData { stageTitle = "Giai Đoạn 1: Sống sót Wave" };
                st1_1.goals.Add(new MissionGoalData(GoalType.SurviveNWaves, 3f));
                m.stages.Add(st1_1);

                m.secondaryGoals.Add(new SecondaryGoalData { description = "Giữ Máu Lâu Đài >= 80%", goal = new MissionGoalData(GoalType.KeepCastleHpPercent, 0.80f) });
                m.secondaryGoals.Add(new SecondaryGoalData { description = "Diệt 15 Goblin", goal = new MissionGoalData(GoalType.KillNEnemies, 15f) });
                break;

            case 3:
                m.title = "Màn 3: Rừng Ngoằn Ngoèo";
                m.description = "Trụ vững trong 3 phút trước đợt tấn công!";
                MissionStageData st3_1 = new MissionStageData { stageTitle = "Giai Đoạn 1: Trụ vững 3 phút" };
                st3_1.goals.Add(new MissionGoalData(GoalType.HoldSeconds, 180f));
                m.stages.Add(st3_1);

                m.secondaryGoals.Add(new SecondaryGoalData { description = "Diệt 1 Boss Goblin", goal = new MissionGoalData(GoalType.KillNBosses, 1f) });
                m.secondaryGoals.Add(new SecondaryGoalData { description = "Giữ Máu Lâu Đài >= 75%", goal = new MissionGoalData(GoalType.KeepCastleHpPercent, 0.75f) });
                break;

            case 6:
                m.title = "Màn 6: Kẻ Dẫn Đường";
                m.description = "Giai đoạn 1: Sống qua Wave 5. Giai đoạn 2: Diệt Shaman Dẫn Đường!";
                MissionStageData st6_1 = new MissionStageData { stageTitle = "Giai Đoạn 1: Sống qua Wave 5" };
                st6_1.goals.Add(new MissionGoalData(GoalType.SurviveNWaves, 5f));
                m.stages.Add(st6_1);

                MissionStageData st6_2 = new MissionStageData { stageTitle = "Giai Đoạn 2: Hạ gục Shaman" };
                st6_2.goals.Add(new MissionGoalData(GoalType.KillNBosses, 1f, "Shaman"));
                m.stages.Add(st6_2);

                m.secondaryGoals.Add(new SecondaryGoalData { description = "Diệt Shaman trong 90s khi xuất hiện", goal = new MissionGoalData(GoalType.KillBossBeforeDeadline, 90f, "Shaman") });
                m.secondaryGoals.Add(new SecondaryGoalData { description = "Giữ Máu Lâu Đài >= 70%", goal = new MissionGoalData(GoalType.KeepCastleHpPercent, 0.70f) });
                break;

            default:
                m.title = $"Màn {levelNum}: Phòng Thủ Chiến Thuật";
                m.description = "Sống sót qua các đợt quái tấn công.";
                MissionStageData stDef = new MissionStageData { stageTitle = "Giai Đoạn 1: Sống sót hết Wave" };
                stDef.goals.Add(new MissionGoalData(GoalType.SurviveNWaves, 4f));
                m.stages.Add(stDef);

                m.secondaryGoals.Add(new SecondaryGoalData { description = "Giữ Máu Lâu Đài >= 80%", goal = new MissionGoalData(GoalType.KeepCastleHpPercent, 0.80f) });
                break;
        }

        cache[levelNum] = m;
        return m;
    }
}
