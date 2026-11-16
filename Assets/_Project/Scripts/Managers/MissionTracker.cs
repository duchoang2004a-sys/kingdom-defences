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
        CheckWinCondition();   // nhát diệt cuối cùng có thể là điều kiện thắng: kiểm ngay, không đợi khung hình sau
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

    /// <summary>HUD nhiệm vụ phải nằm cùng canvas với thanh trên (Canvas_UI); không có mới lấy canvas bất kỳ.</summary>
    private static Canvas FindHudCanvas()
    {
        Canvas[] all = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name == "Canvas_UI") return all[i];
        }
        return UnityEngine.Object.FindAnyObjectByType<Canvas>();
    }

    private void EnsureMissionHUD()
    {
        Canvas canvas = FindHudCanvas();
        if (canvas == null) return;

        Transform existing = canvas.transform.Find("MissionHUD_Banner");
        if (existing != null)
        {
            missionTitleText = existing.Find("Panel/Title")?.GetComponent<TextMeshProUGUI>();
            missionProgressText = existing.Find("Panel/Progress")?.GetComponent<TextMeshProUGUI>();
            secondaryMissionsText = existing.Find("Panel/Secondary")?.GetComponent<TextMeshProUGUI>();
            castleTierText = existing.Find("Panel/CastleTier")?.GetComponent<TextMeshProUGUI>();
            return;
        }

        // v2 (SESSION_20261113_B): thu gọn thành icon quyển sách nhiệm vụ ở góc trái; bấm để mở/đóng danh sách điều kiện thắng.
        GameObject bannerObj = new GameObject("MissionHUD_Banner");
        bannerObj.transform.SetParent(canvas.transform, false);

        RectTransform rt = bannerObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(10f, -104f);   // ngay dưới thanh trên (TopBar cao 96) - trước đây -70 đè lên nút Home
        rt.sizeDelta = new Vector2(52f, 52f);

        Image bookImg = bannerObj.AddComponent<Image>();
        Sprite book = HudIcons.Get("icon_mission_book");
        bookImg.sprite = book;
        bookImg.preserveAspect = true;
        bookImg.color = book != null ? Color.white : new Color(0.55f, 0.4f, 0.2f, 0.95f);
        Button bookBtn = bannerObj.AddComponent<Button>();
        bookBtn.targetGraphic = bookImg;

        // Panel danh sách (mở sang phải icon)
        GameObject panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(bannerObj.transform, false);
        RectTransform pr = panel.GetComponent<RectTransform>();
        pr.anchorMin = new Vector2(1f, 1f);
        pr.anchorMax = new Vector2(1f, 1f);
        pr.pivot = new Vector2(0f, 1f);
        pr.anchoredPosition = new Vector2(8f, 0f);
        pr.sizeDelta = new Vector2(360f, 176f);
        Image pbg = panel.AddComponent<Image>();
        pbg.color = new Color(0.10f, 0.13f, 0.20f, 0.94f);
        Outline pol = panel.AddComponent<Outline>();
        pol.effectColor = new Color(0.85f, 0.7f, 0.25f, 0.7f);
        pol.effectDistance = new Vector2(1.5f, -1.5f);

        // Title
        GameObject titleObj = new GameObject("Title", typeof(RectTransform));
        titleObj.transform.SetParent(panel.transform, false);
        missionTitleText = titleObj.AddComponent<TextMeshProUGUI>();
        missionTitleText.fontSize = 14;
        missionTitleText.fontStyle = FontStyles.Bold;
        missionTitleText.alignment = TextAlignmentOptions.MidlineLeft;
        missionTitleText.color = new Color(1f, 0.85f, 0.3f);
        missionTitleText.raycastTarget = false;
        RectTransform tRt = titleObj.GetComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0.03f, 0.82f);
        tRt.anchorMax = new Vector2(0.97f, 0.98f);
        tRt.sizeDelta = Vector2.zero;

        // Progress
        GameObject progObj = new GameObject("Progress", typeof(RectTransform));
        progObj.transform.SetParent(panel.transform, false);
        missionProgressText = progObj.AddComponent<TextMeshProUGUI>();
        missionProgressText.fontSize = 12;
        missionProgressText.alignment = TextAlignmentOptions.MidlineLeft;
        missionProgressText.color = Color.white;
        missionProgressText.raycastTarget = false;
        RectTransform pRt = progObj.GetComponent<RectTransform>();
        pRt.anchorMin = new Vector2(0.03f, 0.66f);
        pRt.anchorMax = new Vector2(0.97f, 0.82f);
        pRt.sizeDelta = Vector2.zero;

        // Secondary Missions: mỗi điều kiện 1 dòng, dấu tích ở CUỐI dòng
        GameObject secObj = new GameObject("Secondary", typeof(RectTransform));
        secObj.transform.SetParent(panel.transform, false);
        secondaryMissionsText = secObj.AddComponent<TextMeshProUGUI>();
        secondaryMissionsText.fontSize = 12;
        secondaryMissionsText.alignment = TextAlignmentOptions.TopLeft;
        secondaryMissionsText.color = new Color(0.9f, 0.9f, 0.9f);
        secondaryMissionsText.raycastTarget = false;
        RectTransform sRt = secObj.GetComponent<RectTransform>();
        sRt.anchorMin = new Vector2(0.03f, 0.20f);
        sRt.anchorMax = new Vector2(0.97f, 0.66f);
        sRt.sizeDelta = Vector2.zero;

        // Castle Tier Badge
        GameObject tierObj = new GameObject("CastleTier", typeof(RectTransform));
        tierObj.transform.SetParent(panel.transform, false);
        castleTierText = tierObj.AddComponent<TextMeshProUGUI>();
        castleTierText.fontSize = 11;
        castleTierText.fontStyle = FontStyles.Bold;
        castleTierText.alignment = TextAlignmentOptions.MidlineLeft;
        castleTierText.color = new Color(0.4f, 0.9f, 1f);
        castleTierText.raycastTarget = false;
        RectTransform tierRt = tierObj.GetComponent<RectTransform>();
        tierRt.anchorMin = new Vector2(0.03f, 0.02f);
        tierRt.anchorMax = new Vector2(0.97f, 0.20f);
        tierRt.sizeDelta = Vector2.zero;

        // Mở sẵn vài giây đầu để người chơi thấy điều kiện thắng, sau đó tự thu lại
        bookBtn.onClick.AddListener(() => panel.SetActive(!panel.activeSelf));
        StartCoroutine(AutoCloseMissionPanel(panel, 8f));
    }

    private System.Collections.IEnumerator AutoCloseMissionPanel(GameObject panel, float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        if (panel != null) panel.SetActive(false);
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
            missionTitleText.text = $"MỤC TIÊU CHÍNH: {missionData.missionTitle}";
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
                    missionProgressText.text = $"Tiến độ Wave: <color=#77ccff>{currW} / {GetWaveGoal()}</color>";
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
                string mark = sub.isCompleted ? "<color=#55ff55><b>[v]</b></color>" : "<color=#ffaa33>[ ]</color>";
                subStatusList.Add($"- {sub.description}  {mark}");
            }
            secondaryMissionsText.text = string.Join("\n", subStatusList);
        }

        if (castleTierText != null)
        {
            int tier = CastleManager.main != null ? CastleManager.main.currentCastleTier : (LevelManager.main != null ? LevelManager.main.currentCastleTier : 0);
            float avgLvl = CastleManager.main != null ? CastleManager.main.averageTowerLevel : 0f;

            if (tier > 0)
            {
                string regenStr = tier == 3 ? "+4% HP/5s" : (tier == 2 ? "+3% HP/5s" : "+2% HP/5s");
                castleTierText.text = $"Lâu Đài Bậc {tier}\n<size=9>(Cấp Avg: {avgLvl:F1} | {regenStr})</size>";
            }
            else
            {
                castleTierText.text = "Lâu Đài Bậc 0\n<size=9>(Xây 3 tháp cùng cấp)</size>";
            }
        }
    }

    /// <summary>
    /// Số wave cần đạt cho nhiệm vụ SurviveWaves: lấy theo tổng wave thật của màn (cùng nguồn với chữ "Wave x/y" trên HUD),
    /// tránh lệch kiểu HUD "Wave 5/6" nhưng nhiệm vụ ghi 5/5 rồi thắng sớm / không bao giờ thắng.
    /// </summary>
    private int GetWaveGoal()
    {
        if (LevelManager.main != null && LevelManager.main.TotalWaves > 0) return LevelManager.main.TotalWaves;
        return missionData != null ? Mathf.Max(1, (int)missionData.targetValue) : 1;
    }

    /// <summary>
    /// Hàm kiểm tra điều kiện hoàn thành màn chơi
    /// </summary>
    public bool CheckWinCondition()
    {
        if (isMissionCompleted)
        {
            // Tự chữa: nếu lần trước bị ngắt giữa chừng (ngoại lệ) khiến chưa thắng thì gọi lại WinGame (WinGame tự chặn gọi lặp)
            if (LevelManager.main != null && !LevelManager.main.hasWon) LevelManager.main.WinGame();
            return true;
        }

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
                int waveGoal = GetWaveGoal();
                EnemySpawner waveSpawner = EnemySpawner.main;
                // Thắng khi đã tới wave cuối VÀ quái đã dọn sạch (đúng lúc EnemySpawner.EndWave gọi WinGame).
                // Không có spawner thì chỉ cần đạt số wave.
                bool waveFieldClear = waveSpawner == null ||
                    (!waveSpawner.isPreparingWave && !waveSpawner.IsSpawning && waveSpawner.EnemiesAlive <= 0);
                if (currWave >= waveGoal && waveFieldClear) primaryCompleted = true;
                break;
        }

        if (primaryCompleted && !isMissionCompleted)
        {
            isMissionCompleted = true;
            Debug.Log($"<color=green>[MissionTracker]</color> Nhiệm vụ chính '{missionData.missionTitle}' đã hoàn thành!");

            // Mỗi bước bọc riêng: một subscriber/bộ lưu lỗi không được chặn dialog chiến thắng
            try { OnMissionCompleted?.Invoke(missionData); }
            catch (Exception ex) { Debug.LogException(ex); }
            try { EvaluateStarsAndSave(); }
            catch (Exception ex) { Debug.LogException(ex); }

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
