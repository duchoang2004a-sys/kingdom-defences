using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hệ số tỉ lệ hiển thị tập trung. Dùng chung cho Editor (KRPrefabFactory / TDSceneGenerator / relayout_maps) và runtime.
/// Đường đi rộng PathWidth; quái chiếm ~1/3 bề rộng và đi được trên 3 làn cách nhau LaneSpacing.
/// </summary>
public static class GameScale
{
    // Hệ số nhân lên scale gốc của prefab (quái 0.5 = nhỏ một nửa so với trước)
    public const float Enemy = 0.5f;
    public const float Boss = 0.7f;
    public const float Hero = 0.65f;
    public const float Soldier = 0.6f;
    public const float Tower = 0.7f;
    public const float Plot = 0.75f;
    public const float Castle = 0.6f;

    // Hình học đường đi (đơn vị world) - khớp Tools_MapGen/relayout_maps.py (HALF_W = 0.72)
    public const float PathWidth = 1.44f;
    public const float LaneSpacing = 0.42f;      // 3 làn: -LaneSpacing, 0, +LaneSpacing

    // Giao chiến / lách
    public const float EngageRange = 0.34f;      // quái thường chỉ bị chặn khi cùng làn với blocker
    public const float BossEngageRange = 0.7f;   // boss to bằng cả đường: luôn bị chặn
    public const float DodgeLookAhead = 1.4f;    // nhìn trước bao xa để lách
    public const float DodgeChance = 0.75f;      // xác suất quái "thông minh" đổi làn khi gặp vật cản
    public const float LaneSwitchSpeed = 1.0f;   // tốc độ trượt ngang giữa các làn (đơn vị/giây)

    // Khung nhìn: ảnh map 1280x720 @100 PPU = 12.8 x 7.2 đơn vị -> orthographicSize 3.6 vừa khít, không lộ viền đen
    public const float ViewOrthoSize = 3.6f;

    private static readonly HashSet<int> applied = new HashSet<int>();

    /// <summary>Nhân scale của đối tượng với hệ số thu nhỏ đúng MỘT lần (an toàn khi gọi lặp lại).</summary>
    public static void Apply(GameObject go, float factor)
    {
        if (go == null) return;
        int id = go.GetInstanceID();
        if (!applied.Add(id)) return;
        go.transform.localScale *= factor;
    }

    public static float LaneOffset(int lane)
    {
        return (Mathf.Clamp(lane, 0, 2) - 1) * LaneSpacing;
    }
}
