using System;
using UnityEngine;

public enum MatchState
{
    Briefing,   // Màn xem thông tin nhiệm vụ trước khi bắt đầu
    Running,    // Trận đấu đang diễn ra bình thường
    Overtime,   // Hết wave nhưng chưa xong nhiệm vụ chính -> Ép đếm giờ / sinh boss
    Victory,    // Thắng trận (ĐÃ KHÓA VĨNH VIỄN)
    Defeat,     // Thua trận (ĐÃ KHÓA VĨNH VIỄN)
    Result      // Màn hình kết quả
}

/// <summary>
/// Hệ thống Cánh Sóng Sự Kiện Tập Trung (GameEvents - Part A3):
/// Tách biệt hoàn toàn tín hiệu quái bị giết (EnemyKilled) với quái lọt về lâu đài (EnemyEscaped).
/// </summary>
public static class GameEvents
{
    // Tín hiệu khi quái bị tiêu diệt bởi người chơi / tháp / hero
    public static event Action<Enemy, DamageInfo> OnEnemyKilled;

    // Tín hiệu khi quái lọt về điểm cuối lâu đài
    public static event Action<Enemy, int> OnEnemyEscaped;

    // Tín hiệu khi tháp được xây hoặc nâng cấp
    public static event Action<Turret> OnTowerBuiltOrUpgraded;

    // Tín hiệu khi sử dụng phép thuật hoặc vật phẩm (Dầu)
    public static event Action<string> OnSpellOrItemUsed;

    // Tín hiệu thay đổi trạng thái trận đấu
    public static event Action<MatchState, MatchState> OnMatchStateChanged;

    public static void TriggerEnemyKilled(Enemy enemy, DamageInfo info)
    {
        OnEnemyKilled?.Invoke(enemy, info);
    }

    public static void TriggerEnemyEscaped(Enemy enemy, int livesLost)
    {
        OnEnemyEscaped?.Invoke(enemy, livesLost);
    }

    public static void TriggerTowerBuiltOrUpgraded(Turret turret)
    {
        OnTowerBuiltOrUpgraded?.Invoke(turret);
    }

    public static void TriggerSpellOrItemUsed(string itemId)
    {
        OnSpellOrItemUsed?.Invoke(itemId);
    }

    public static void TriggerMatchStateChanged(MatchState oldState, MatchState newState)
    {
        OnMatchStateChanged?.Invoke(oldState, newState);
    }
}
