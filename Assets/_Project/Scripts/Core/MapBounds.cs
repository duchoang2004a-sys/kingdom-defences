using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Khung (world rect) của bản đồ đang chơi. Dùng để camera luôn FIT/COVER map (không bao giờ lộ viền đen)
/// và để các HUD/Spawner biết phạm vi thật của map. Không cần gán tay: tự tìm từ nền map / tilemap trong scene.
/// </summary>
public static class MapBounds
{
    private static bool cached;
    private static Rect rect;
    private static int cachedFrame = -1;

    // Mặc định = ảnh map 1280x720 @100 PPU
    private static readonly Rect Default = new Rect(-6.4f, -3.6f, 12.8f, 7.2f);

    public static void Invalidate() { cached = false; }

    public static Rect Get()
    {
        if (cached && cachedFrame >= 0) return rect;
        rect = Compute();
        cached = true;
        cachedFrame = Time.frameCount;
        return rect;
    }

    private static Rect Compute()
    {
        // 1) Ảnh nền map (Map_Background_Fallback) = khung chuẩn của ảnh thiết kế
        GameObject bg = GameObject.Find("Map_Background_Fallback");
        if (bg != null)
        {
            SpriteRenderer sr = bg.GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
            {
                Bounds b = sr.bounds;
                if (b.size.x > 1f && b.size.y > 1f) return new Rect(b.min.x, b.min.y, b.size.x, b.size.y);
            }
        }

        // 2) Hợp các tilemap nền (nếu thiếu ảnh nền)
        Tilemap[] maps = Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
        bool any = false;
        Bounds all = new Bounds();
        for (int i = 0; i < maps.Length; i++)
        {
            TilemapRenderer tr = maps[i].GetComponent<TilemapRenderer>();
            if (tr == null) continue;
            Bounds b = tr.bounds;
            if (b.size.x < 1f || b.size.y < 1f) continue;
            if (!any) { all = b; any = true; } else all.Encapsulate(b);
        }
        if (any) return new Rect(all.min.x, all.min.y, all.size.x, all.size.y);

        return Default;
    }
}
