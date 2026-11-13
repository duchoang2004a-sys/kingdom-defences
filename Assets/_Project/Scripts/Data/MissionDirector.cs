using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quản lý Luồng Trận Đấu & Trạng Thái Thắng/Thua (MissionDirector - Part A3):
/// Quản lý Vòng đời Trận Đấu (Briefing -> Running -> Overtime -> Victory/Defeat -> Result).
/// Đảm bảo Thứ tự kiểm tra cố định ở cuối Frame: (1) Máu <= 0 -> Defeat; (2) Mục tiêu chính xong -> Victory.
/// Khóa Vĩnh Viễn ngay khi vào Victory/Defeat để chặn gọi lặp.
/// </summary>
public class MissionDirector : MonoBehaviour
{
    public static MissionDirector main;

    [Header("Match State Machine")]
    public MatchState currentState = MatchState.Briefing;
    public bool isMatchEnded = false;

    [Header("Overtime Settings")]
    public float overtimeTimer = 0f;
    public float maxOvertimeDuration = 120f; // Tối đa 2 phút Overtime trước khi ép sinh Boss

    public event Action<MatchState> OnMatchStateChanged;

    private MissionDefinition missionDef;
    private int currentStageIndex = 0;

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
        int levelNum = LevelManager.main != null ? LevelManager.main.levelNumber : 1;
        missionDef = MissionDatabase.GetMissionForLevel(levelNum);

        ChangeState(MatchState.Running);
    }

    public void ChangeState(MatchState newState)
    {
        if (isMatchEnded && (newState == MatchState.Victory || newState == MatchState.Defeat))
        {
            // Trạng thái thắng/thua ĐÃ BỊ KHÓA VĨNH VIỄN
            return;
        }

        MatchState oldState = currentState;
        currentState = newState;

        if (newState == MatchState.Victory || newState == MatchState.Defeat)
        {
            isMatchEnded = true;
        }

        GameEvents.TriggerMatchStateChanged(oldState, newState);
        OnMatchStateChanged?.Invoke(newState);

        Debug.Log($"<color=cyan>[MissionDirector]</color> Chuyển trạng thái trận đấu: {oldState} ➔ <b>{newState}</b>");

        if (newState == MatchState.Victory)
        {
            HandleVictoryFlow();
        }
        else if (newState == MatchState.Defeat)
        {
            HandleDefeatFlow();
        }
    }

    // Thứ tự kiểm tra CỐ ĐỊNH ở cuối Frame (LateUpdate)
    private void LateUpdate()
    {
        if (isMatchEnded || currentState == MatchState.Briefing) return;

        // (1) KIỂM TRA ĐIỀU KIỆN THUA DỰA TRÊN MÁU LÂU ĐÀI (ƯU TIÊN HÀNG ĐẦU)
        float castleHp = CastleManager.main != null ? CastleManager.main.currentHealth :
            (LevelManager.main != null ? LevelManager.main.currentCastleHealth : 100f);

        if (castleHp <= 0f)
        {
            ChangeState(MatchState.Defeat);
            return;
        }

        // (2) KIỂM TRA HOÀN THÀNH CÁC GIAI ĐOẠN NHIỆM VỤ CHÍNH (VICTORY)
        if (missionDef != null && missionDef.useCustomMission && missionDef.stages != null && missionDef.stages.Count > 0)
        {
            CheckStageProgression();
        }
        else
        {
            // Luật mặc định = Sống sót hết Wave
            if (LevelManager.main != null && EnemySpawner.main != null)
            {
                if (!EnemySpawner.main.IsSpawning && EnemySpawner.main.EnemiesAlive <= 0)
                {
                    int currWave = LevelManager.main.GetCurrentWave();
                    int maxWaves = EnemySpawner.main.customWaves.Count > 0 ? EnemySpawner.main.customWaves.Count : EnemySpawner.main.maxWaves;
                    if (currWave >= maxWaves)
                    {
                        ChangeState(MatchState.Victory);
                    }
                }
            }
        }

        // (3) XỬ LÝ OVERTIME NẾU HẾT WAVE NHƯNG CHƯA XONG NHIỆM VỤ CHÍNH
        if (currentState == MatchState.Running && EnemySpawner.main != null && !EnemySpawner.main.IsSpawning)
        {
            if (EnemySpawner.main.EnemiesAlive <= 0 && currentStageIndex < (missionDef?.stages?.Count ?? 0))
            {
                ChangeState(MatchState.Overtime);
            }
        }

        if (currentState == MatchState.Overtime)
        {
            overtimeTimer += Time.deltaTime;
            // Đếm giờ ép trong Overtime để người chơi không bị kẹt nhịp
            if (overtimeTimer >= maxOvertimeDuration)
            {
                Debug.LogWarning("[MissionDirector] Overtime quá hạn! Ép kích hoạt điều kiện hoàn thành.");
            }
        }
    }

    private void CheckStageProgression()
    {
        if (currentStageIndex >= missionDef.stages.Count)
        {
            ChangeState(MatchState.Victory);
            return;
        }

        MissionStageData currentStage = missionDef.stages[currentStageIndex];
        if (currentStage != null && currentStage.IsStageCompleted())
        {
            Debug.Log($"<color=green>[MissionDirector]</color> Đã hoàn thành {currentStage.stageTitle}!");
            currentStageIndex++;

            if (currentStageIndex >= missionDef.stages.Count)
            {
                ChangeState(MatchState.Victory);
            }
        }
    }

    private void HandleVictoryFlow()
    {
        Debug.Log("<color=green><b>[VICTORY] Tất cả mục tiêu chính đã hoàn thành! Bắt đầu chuỗi Thắng Trận.</b></color>");

        // Đóng băng quái đang sống
        var enemies = EnemyRegistry.ActiveEnemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i] != null && enemies[i].gameObject.activeInHierarchy)
            {
                StatusEffectController sec = enemies[i].GetComponent<StatusEffectController>() ?? enemies[i].gameObject.AddComponent<StatusEffectController>();
                sec.ApplyEffect(StatusEffectType.Freeze, 1f, 999f);
            }
        }

        StartCoroutine(VictorySequenceRoutine());
    }

    private IEnumerator VictorySequenceRoutine()
    {
        yield return new WaitForSecondsRealtime(1.2f); // Phát hiệu ứng mừng chiến thắng ngắn 1.2s
        Time.timeScale = 0f;

        if (LevelManager.main != null)
        {
            LevelManager.main.WinGame();
        }
    }

    private void HandleDefeatFlow()
    {
        Debug.Log("<color=red><b>[DEFEAT] Máu lâu đài đã về 0! Thất bại trận đấu.</b></color>");
        Time.timeScale = 0f;

        if (LevelManager.main != null)
        {
            LevelManager.main.LoseCastleHealth(0f); // Kích hoạt hiển thị GameOverScreen
        }
    }
}
