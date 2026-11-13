using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Giao diện cho các đơn vị đồng minh có khả năng chặn đường quái vật (Hero, Hiệp sĩ tiếp viện, Lính Barracks).
/// Quái khi gặp Blocker sẽ dừng di chuyển trên đường và bước vào trạng thái Đấu tay đôi (Melee Duel).
/// </summary>
public interface IBlocker
{
    Transform transform { get; }
    Vector2 Position { get; }
    bool IsAlive { get; }
    int MaxBlockedCount { get; }
    int CurrentlyBlockedCount { get; }

    bool CanBlockMore();
    void Engage(EnemyMovement enemy);
    void Disengage(EnemyMovement enemy);
    void TakeDamage(float amount);
}

/// <summary>
/// Quản lý danh sách các Blocker đang hoạt động trong Scene (Zero-GC, truy vấn nhanh).
/// </summary>
public static class BlockerRegistry
{
    private static readonly List<IBlocker> activeBlockers = new List<IBlocker>();
    public static IReadOnlyList<IBlocker> ActiveBlockers => activeBlockers;

    public static void Register(IBlocker blocker)
    {
        if (blocker != null && !activeBlockers.Contains(blocker))
        {
            activeBlockers.Add(blocker);
        }
    }

    public static void Unregister(IBlocker blocker)
    {
        if (blocker != null)
        {
            activeBlockers.Remove(blocker);
        }
    }

    public static void Clear()
    {
        activeBlockers.Clear();
    }
}
