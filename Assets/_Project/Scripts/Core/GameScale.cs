using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hệ số tỉ lệ hiển thị tập trung (v3: khung map 1920x1080 @100 PPU = 19.2 x 10.8 đơn vị, camera ortho 5.4).
/// Dùng chung cho Editor (KRPrefabFactory / TDSceneGenerator / Tools_MapGen/gen_maps_v4.py) và runtime.
/// Đường đi rộng PathWidth; quái chiếm ~1/3 bề rộng và đi được trên 3 làn cách nhau LaneSpacing.
/// Hằng số PATH_W / CASTLE_SCALE / PROP_SCALE trong gen_maps_v4.py PHẢI khớp các giá trị ở đây.
/// </summary>
public static class GameScale
{
    // Hệ số nhân lên scale gốc của prefab (mọi thứ nhỏ ~0.83 so với v2 vì đường hẹp 1.44 -> 1.2)
    public const float Enemy = 0.42f;
    public const float Boss = 0.6f;
    public const float Hero = 0.55f;
    public const float Soldier = 0.5f;
    public const float Tower = 0.55f;
    public const float Plot = 0.65f;
    public const float Castle = 0.42f;
    public const float Prop = 0.5f;              // cổng quái, sọ, hàng rào... (prop trang trí, không phải lâu đài)

    // Hình học đường đi (đơn vị world) - khớp Tools_MapGen/gen_maps_v4.py (PATH_W = 1.2)
    public const float PathWidth = 1.2f;
    public const float LaneSpacing = 0.4f;       // 3 làn: -LaneSpacing, 0, +LaneSpacing (mỗi làn = 1/3 đường)

    // Giao chiến / lách
    public const float EngageRange = 0.3f;       // quái thường chỉ bị chặn khi cùng làn với blocker
    public const float BossEngageRange = 0.6f;   // boss to bằng cả đường: luôn bị chặn
    public const float DodgeLookAhead = 1.0f;    // nhìn trước bao xa để lách
    public const float DodgeChance = 0.5f;       // xác suất quái "thông minh" đổi làn khi gặp vật cản
    public const float LaneSwitchSpeed = 0.8f;   // tốc độ trượt ngang giữa các làn (đơn vị/giây)

    // Tầm bắn / vùng nổ / chuỗi sét nhân với RangeFactor so với số liệu gốc (3.2-4.8 trên map 12.8x7.2).
    // 0.55 => tháp cấp 1 phủ ~1.8-2.0 đơn vị: chỉ bao 1-2 đoạn đường gần nó, không còn phủ cả map.
    public const float RangeFactor = 0.55f;
    public const float Projectile = 0.7f;        // kích thước đạn so với sprite gốc
    public const float ProjectileSpeed = 0.5f;   // tốc độ đạn (đơn vị/giây) so với số liệu gốc: bay chậm đủ để thấy

    // Khung nhìn: ảnh map 1920x1080 @100 PPU = 19.2 x 10.8 đơn vị -> orthographicSize 5.4 vừa khít, không lộ viền đen
    public const float ViewOrthoSize = 5.4f;
    public const float ViewHalfWidth = 9.6f;     // = ViewOrthoSize * 16/9
    public const float ViewHalfHeight = 5.4f;

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
