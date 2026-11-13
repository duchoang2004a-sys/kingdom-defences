using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Lớp MissionTracker:
/// - Lắng nghe các sự kiện gameplay từ LevelManager, EnemySpawner và CastleManager để cập nhật tiến độ nhiệm vụ theo thời gian thực.
/// - Theo dõi cả Nhiệm vụ chính và danh sách các Mục tiêu phụ (diệt N boss, trụ N phút, giữ máu lâu đài > X%...).
/// - Cung cấp sự kiện `OnMissionProgressUpdated` và `OnMissionCompleted` khi mục tiêu thay đổi hoặc hoàn thành.
/// </summary>
public class MissionTracker : MonoBehaviour
{
    public static MissionTracker main;

    [Header("Active Mission Data")]
    public LevelMissionData missionData;
    public int totalEnemiesKilled = 0;
    public int bossesKilled = 0;
    public float elapsedTime = 0f;
    public bool isMissionCompleted = false;

    [Header("Gameplay Events Broadcast")]
    public static Action<LevelMissionData> OnMissionProgressUpdated;
    public static Action<LevelMissionData> OnMissionCompleted;

    [Header("UI Banner References")]
    public TextMeshProUGUI missionTitleText;
    public TextMeshProUGUI missionProgressText;
    public TextMeshProUGUI secondaryMissionsText;
    public TextMeshProUGUI castleTierText;

    private float startTime;
    private bool isTracking = false;

    private void Awake()
    {
        if (main != null && main != this)
        {
            Destroy(gameObject);
            return;
        }
        main = this;
        EnsureMissionData();
    }

    public void EnsureMissionData()
    {
        if (missionData == null)
        {
            int levelNum = LevelManager.main != null ? LevelManager.main.levelNumber : 1;
            missionData = GameMissionDatabase.GetMissionForLevel(levelNum);
            if (missionData == null)
            {
                missionData = new LevelMissionData
                {
                    missionTitle = $"Màn {levelNum}: Phòng Thủ",
                    missionType = MissionType.SurviveWaves,
                    targetValue = 5f,
                    secondaryMissions = new List<SecondaryMissionData>()
                };
            }
        }
    }

    private void Start()
    {
        EnsureMissionData();

        startTime = Time.time;
        isTracking = true;

        RegisterGameplayListeners();
        EnsureMissionHUD();
    }

    private void OnEnable()
    {
        RegisterGameplayListeners();
    }

    private void OnDisable()
    {
        UnregisterGameplayListeners();
    }

    private void OnDestroy()
    {
        UnregisterGameplayListeners();
    }

    /// <summary>
    /// Đăng ký lắng nghe sự kiện gameplay từ LevelManager, EnemySpawner và CastleManager
    /// </summary>
    private void RegisterGameplayListeners()
    {
        UnregisterGameplayListeners(); // Đảm bảo không đăng ký trùng lặp

        // 1. Sự kiện diệt quái từ EnemySpawner
        EnemySpawner.onEnemyKilled.AddListener(OnEnemyKilledHandler);

        // 2. Các sự kiện trạng thái từ LevelManager
        if (LevelManager.main != null)
        {
            LevelManager.main.OnWaveChanged += OnWaveChangedHandler;
            LevelManager.main.OnCastleHealthChanged += OnCastleHealthChangedHandler;
            LevelManager.main.OnCastleTierChanged += OnCastleTierChangedHandler;
        }

        // 3. Sự kiện từ CastleManager nếu có
        if (CastleManager.main != null)
        {
            CastleManager.main.OnCastleHealthChanged += OnCastleHealthChangedHandler;
        }
    }

    /// <summary>
    /// Hủy đăng ký lắng nghe sự kiện khi bị hủy hoặc tắt component
    /// </summary>
    private void UnregisterGameplayListeners()
    {
        EnemySpawner.onEnemyKilled.RemoveListener(OnEnemyKilledHandler);

        if (LevelManager.main != null)
        {
            LevelManager.main.OnWaveChanged -= OnWaveChangedHandler;
            LevelManager.main.OnCastleHealthChanged -= OnCastleHealthChangedHandler;
            LevelManager.main.OnCastleTierChanged -= OnCastleTierChangedHandler;
        }

        if (CastleManager.main != null)
        {
            CastleManager.main.OnCastleHealthChanged -= OnCastleHealthChangedHandler;
        }
    }

    // --- EVENT HANDLERS LẮNG NGHE SỰ KIỆN GAMEPLAY ---

    private void OnEnemyKilledHandler()
    {
        totalEnemiesKilled++;
        EvaluateSecondaryObjectives();
        UpdateHUDBanner();
        OnMissionProgressUpdated?.Invoke(missionData);
    }

    private void OnWaveChangedHandler(int newWave)
    {
        EvaluateSecondaryObjectives();
        UpdateHUDBanner();
        CheckWinCondition();
        OnMissionProgressUpdated?.Invoke(missionData);
    }

    private void OnCastleHealthChangedHandler(float currentHp, float maxHp)
    {
        EvaluateSecondaryObjectives();
        UpdateHUDBanner();
        CheckWinCondition();
        OnMissionProgressUpdated?.Invoke(missionData);
    }

    private void OnCastleTierChangedHandler(int newTier)
    {
        UpdateHUDBanner();
    }

    public void RegisterBossKill()
    {
        bossesKilled++;
        Debug.Log($"<color=yellow>[MissionTracker]</color> Đã hạ Boss! Tổng số Boss đã diệt: {bossesKilled}/{(int)missionData.targetValue}");
        EvaluateSecondaryObjectives();
        UpdateHUDBanner();
        CheckWinCondition();
        OnMissionProgressUpdated?.Invoke(missionData);
    }

    private void EnsureMissionHUD()
    {
        Canvas canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        Transform existing = canvas.transform.Find("MissionHUD_Banner");
        if (existing != null)
        {
            missionTitleText = existing.Find("Title")?.GetComponent<TextMeshProUGUI>();
            missionProgressText = existing.Find("Progress")?.GetComponent<TextMeshProUGUI>();
            secondaryMissionsText = existing.Find("Secondary")?.GetComponent<TextMeshProUGUI>();
            castleTierText = existing.Find("CastleTier")?.GetComponent<TextMeshProUGUI>();
            return;
        }

        // Tạo Banner Nhiệm vụ ở mép trên giữa màn hình
        GameObject bannerObj = new GameObject("MissionHUD_Banner");
        bannerObj.transform.SetParent(canvas.transform, false);

        RectTransform rt = bannerObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -8f);
        rt.sizeDelta = new Vector2(560f, 78f);

        Image bg = bannerObj.AddComponent<Image>();
        bg.color = new Color(0.10f, 0.13f, 0.20f, 0.92f);

        // Title
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(bannerObj.transform, false);
        missionTitleText = titleObj.AddComponent<TextMeshProUGUI>();
        missionTitleText.fontSize = 13;
        missionTitleText.fontStyle = FontStyles.Bold;
        missionTitleText.alignment = TextAlignmentOptions.Center;
        missionTitleText.color = new Color(1f, 0.85f, 0.3f);
        RectTransform tRt = titleObj.GetComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0.02f, 0.65f);
        tRt.anchorMax = new Vector2(0.68f, 0.98f);
        tRt.sizeDelta = Vector2.zero;

        // Progress
        GameObject progObj = new GameObject("Progress");
        progObj.transform.SetParent(bannerObj.transform, false);
        missionProgressText = progObj.AddComponent<TextMeshProUGUI>();
        missionProgressText.fontSize = 11;
        missionProgressText.alignment = TextAlignmentOptions.Center;
        missionProgressText.color = Color.white;
        RectTransform pRt = progObj.GetComponent<RectTransform>();
        pRt.anchorMin = new Vector2(0.02f, 0.38f);
        pRt.anchorMax = new Vector2(0.68f, 0.64f);
        pRt.sizeDelta = Vector2.zero;

        // Secondary Missions
        GameObject secObj = new GameObject("Secondary");
        secObj.transform.SetParent(bannerObj.transform, false);
        secondaryMissionsText = secObj.AddComponent<TextMeshProUGUI>();
        secondaryMissionsText.fontSize = 10;
        secondaryMissionsText.alignment = TextAlignmentOptions.Center;
        secondaryMissionsText.color = new Color(0.85f, 0.85f, 0.85f);
        RectTransform sRt = secObj.GetComponent<RectTransform>();
        sRt.anchorMin = new Vector2(0.02f, 0.04f);
        sRt.anchorMax = new Vector2(0.68f, 0.36f);
        sRt.sizeDelta = Vector2.zero;

        // Castle Tier Badge
        GameObject tierObj = new GameObject("CastleTier");
        tierObj.transform.SetParent(bannerObj.transform, false);
        castleTierText = tierObj.AddComponent<TextMeshProUGUI>();
        castleTierText.fontSize = 11;
        castleTierText.fontStyle = FontStyles.Bold;
        castleTierText.alignment = TextAlignmentOptions.Center;
        castleTierText.color = new Color(0.4f, 0.9f, 1f);
        RectTransform tierRt = tierObj.GetComponent<RectTransform>();
        tierRt.anchorMin = new Vector2(0.70f, 0.08f);
        tierRt.anchorMax = new Vector2(0.98f, 0.92f);
        tierRt.sizeDelta = Vector2.zero;
    }

    private void Update()
    {
        if (!isTracking || missionData == null) return;

        elapsedTime = Time.time - startTime;

        EvaluateSecondaryObjectives();
        UpdateHUDBanner();
        CheckWinCondition();
    }

    /// <summary>
    /// Đánh giá và cập nhật tiến độ cho toàn bộ Mục tiêu phụ theo thời gian thực
    /// </summary>
    public void EvaluateSecondaryObjectives()
    {
        if (missionData == null || missionData.secondaryMissions == null) return;

        float castleHpPercent = CastleManager.main != null ? CastleManager.main.HealthPercentage :
            (LevelManager.main != null ? LevelManager.main.currentCastleHealth / LevelManager.main.maxCastleHealth : 1f);

        int activeTowersCount = CastleManager.main != null ? CastleManager.main.validTowersCount : 0;

        foreach (var sub in missionData.secondaryMissions)
        {
            if (sub == null) continue;

            switch (sub.type)
            {
                case SecondaryMissionType.KillNBosses:
                    sub.currentProgress = bossesKilled;
                    sub.isCompleted = bossesKilled >= (int)sub.targetValue;
                    break;

                case SecondaryMissionType.SurviveNMinutes:
                    sub.currentProgress = elapsedTime;
                    sub.isCompleted = elapsedTime >= sub.targetValue;
                    break;

                case SecondaryMissionType.KeepCastleHpPercent:
                    sub.currentProgress = castleHpPercent;
                    sub.isCompleted = castleHpPercent >= sub.targetValue;
                    break;

                case SecondaryMissionType.KillNEnemies:
                    sub.currentProgress = totalEnemiesKilled;
                    sub.isCompleted = totalEnemiesKilled >= (int)sub.targetValue;
                    break;

                case SecondaryMissionType.BuildNTowers:
                    sub.currentProgress = activeTowersCount;
                    sub.isCompleted = activeTowersCount >= (int)sub.targetValue;
                    break;
            }
        }
    }

    private void UpdateHUDBanner()
    {
        EnsureMissionData();
        if (missionData == null) return;

        if (missionTitleText != null)
        {
            missionTitleText.text = $"🎯 MỤC TIÊU CHÍNH: {missionData.missionTitle}";
        }

        if (missionProgressText != null)
        {
            switch (missionData.missionType)
            {
                case MissionType.HoldTime:
                    float remaining = Mathf.Max(0f, missionData.targetValue - elapsedTime);
                    int mins = (int)(remaining / 60);
                    int secs = (int)(remaining % 60);
                    missionProgressText.text = $"Trụ vững còn lại: <color=#55ff55>{mins:D2}:{secs:D2}</color>";
                    break;

                case MissionType.KillBosses:
                    missionProgressText.text = $"Tiêu diệt Boss: <color=#ff7777>{bossesKilled} / {(int)missionData.targetValue}</color>";
                    break;

                case MissionType.ProtectCastle:
                    float hpPercent = CastleManager.main != null ? CastleManager.main.HealthPercentage :
                        (LevelManager.main != null ? (LevelManager.main.currentCastleHealth / LevelManager.main.maxCastleHealth) : 1f);
                    missionProgressText.text = $"Máu Lâu đài: <color=#ffdd55>{hpPercent * 100:F0}%</color> (Yêu cầu >{(int)(missionData.targetValue * 100)}%)";
                    break;

                default: // SurviveWaves
                    int currW = LevelManager.main != null ? LevelManager.main.currWave : 1;
                    missionProgressText.text = $"Tiến độ Wave: <color=#77ccff>{currW} / {(int)missionData.targetValue}</color>";
                    break;
            }
        }

        // Hiển thị danh sách Mục tiêu phụ
        if (secondaryMissionsText != null && missionData != null && missionData.secondaryMissions != null)
        {
            List<string> subStatusList = new List<string>();
            foreach (var sub in missionData.secondaryMissions)
            {
                if (sub == null) continue;
                string mark = sub.isCompleted ? "<color=#55ff55>[✓]</color>" : "<color=#ffaa33>[ ]</color>";
                subStatusList.Add($"{mark} {sub.description}");
            }
            secondaryMissionsText.text = string.Join("  |  ", subStatusList);
        }

        if (castleTierText != null)
        {
            int tier = CastleManager.main != null ? CastleManager.main.currentCastleTier : (LevelManager.main != null ? LevelManager.main.currentCastleTier : 0);
            float avgLvl = CastleManager.main != null ? CastleManager.main.averageTowerLevel : 0f;

            if (tier > 0)
            {
                string regenStr = tier == 3 ? "+4% HP/5s" : (tier == 2 ? "+3% HP/5s" : "+2% HP/5s");
                castleTierText.text = $"🏰 Lâu Đài Bậc {tier}\n<size=9>(Cấp Avg: {avgLvl:F1} | {regenStr})</size>";
            }
            else
            {
                castleTierText.text = "🏰 Lâu Đài Bậc 0\n<size=9>(Xây 3 tháp cùng cấp)</size>";
            }
        }
    }

    /// <summary>
    /// Hàm kiểm tra điều kiện hoàn thành màn chơi
    /// </summary>
    public bool CheckWinCondition()
    {
        if (isMissionCompleted) return true;

        EnsureMissionData();
        if (missionData == null) return false;

        bool primaryCompleted = false;

        switch (missionData.missionType)
        {
            case MissionType.HoldTime:
                if (elapsedTime >= missionData.targetValue) primaryCompleted = true;
                break;

            case MissionType.KillBosses:
                if (bossesKilled >= (int)missionData.targetValue) primaryCompleted = true;
                break;

            case MissionType.ProtectCastle:
                float hpPercent = CastleManager.main != null ? CastleManager.main.HealthPercentage :
                    (LevelManager.main != null ? (LevelManager.main.currentCastleHealth / LevelManager.main.maxCastleHealth) : 1f);
                if (hpPercent >= missionData.targetValue) primaryCompleted = true;
                break;

            case MissionType.SurviveWaves:
                int currWave = LevelManager.main != null ? LevelManager.main.currWave : 1;
                if (currWave >= (int)missionData.targetValue) primaryCompleted = true;
                break;
        }

        if (primaryCompleted && !isMissionCompleted)
        {
            isMissionCompleted = true;
            Debug.Log($"<color=green>[MissionTracker]</color> Nhiệm vụ chính '{missionData.missionTitle}' đã hoàn thành!");

            OnMissionCompleted?.Invoke(missionData);
            EvaluateStarsAndSave();

            if (LevelManager.main != null)
            {
                LevelManager.main.WinGame();
            }
        }

        return primaryCompleted;
    }

    public int EvaluateStarsAndSave()
    {
        if (LevelManager.main == null) return 1;

        int cups = 1; // 1 Sao cho việc hoàn thành nhiệm vụ chính

        // Cập nhật tiến độ lần cuối
        EvaluateSecondaryObjectives();

        // Cộng thêm 1 Sao cho mỗi Mục tiêu phụ hoàn thành (tối đa 3 Sao)
        if (missionData != null && missionData.secondaryMissions != null)
        {
            foreach (var sub in missionData.secondaryMissions)
            {
                if (sub != null && sub.isCompleted)
                {
                    cups++;
                }
            }
        }

        cups = Mathf.Clamp(cups, 1, 3);

        int levelNum = LevelManager.main.levelNumber;
        if (SaveManager.instance != null)
        {
            SaveManager.instance.SaveLevelProgress(levelNum, cups);
        }

        Debug.Log($"<color=yellow>[MissionTracker]</color> Kết quả Level {levelNum}: Đạt {cups} ★★★!");
        return cups;
    }
}
