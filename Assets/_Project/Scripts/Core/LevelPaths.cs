using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Thông tin 1 tuyến đường (route) của quái: danh sách waypoint từ điểm Spawn tới Lâu đài.
/// </summary>
[System.Serializable]
public class RouteInfo
{
    public string label;
    public int spawnIndex;
    public Transform[] points;
}

/// <summary>
/// LevelPaths: quản lý TOÀN BỘ tuyến đường của màn chơi (1..N tuyến, không còn giới hạn 2 đường).
/// Gắn vào GameObject "Waypoints". Cấu trúc con:
///   Waypoints
///     Route_0 (con: WP_0, WP_1, ...)
///     Route_1 ...
///   (Tương thích cũ: Path1 / Path2)
/// - Tự động loại bỏ waypoint trùng vị trí liên tiếp (nguyên nhân quái spawn xong biến mất).
/// - PickRouteId() chia đều quái cho các tuyến (túi xáo trộn) nên Spawn A/B/C đều có quái.
/// </summary>
public class LevelPaths : MonoBehaviour
{
    public static LevelPaths main;

    public List<RouteInfo> routes = new List<RouteInfo>();

    [Tooltip("Khoảng cách tối thiểu giữa 2 waypoint liên tiếp. Nhỏ hơn sẽ bị coi là trùng và bỏ qua.")]
    public float minWaypointGap = 0.05f;

    private readonly List<int> bag = new List<int>();

    public int RouteCount { get { return routes.Count; } }

    private void Awake()
    {
        main = this;
        Rebuild();
    }

    /// <summary>Quét lại cây con và dựng danh sách tuyến đường.</summary>
    public void Rebuild()
    {
        routes.Clear();
        bag.Clear();

        List<Transform> groups = new List<Transform>();
        foreach (Transform child in transform)
        {
            if (child.name.StartsWith("Route_") || child.name == "Path1" || child.name == "Path2")
                groups.Add(child);
        }

        groups.Sort((a, b) => ExtractIndex(a.name).CompareTo(ExtractIndex(b.name)));

        for (int g = 0; g < groups.Count; g++)
        {
            List<Transform> pts = new List<Transform>();
            foreach (Transform wp in groups[g])
            {
                if (pts.Count > 0 && Vector2.Distance(pts[pts.Count - 1].position, wp.position) < minWaypointGap)
                    continue; // bỏ waypoint trùng vị trí
                pts.Add(wp);
            }
            if (pts.Count < 2) continue;

            RouteInfo info = new RouteInfo();
            info.label = groups[g].name;
            info.points = pts.ToArray();
            info.spawnIndex = routes.Count;
            routes.Add(info);
        }
    }

    private static int ExtractIndex(string n)
    {
        int digits = 0;
        int value = 0;
        for (int i = n.Length - 1; i >= 0; i--)
        {
            if (char.IsDigit(n[i])) { value += (n[i] - '0') * Pow10(digits); digits++; }
            else break;
        }
        return value;
    }

    private static int Pow10(int e)
    {
        int r = 1;
        for (int i = 0; i < e; i++) r *= 10;
        return r;
    }

    public Transform[] GetRoute(int id)
    {
        if (routes.Count == 0) return null;
        if (id < 0 || id >= routes.Count) id = 0;
        return routes[id].points;
    }

    public Vector3 GetSpawnPosition(int id)
    {
        Transform[] r = GetRoute(id);
        if (r != null && r.Length > 0) return r[0].position;
        return transform.position;
    }

    /// <summary>Chọn tuyến cho quái kế tiếp: chia đều, ngẫu nhiên theo từng "túi".</summary>
    public int PickRouteId()
    {
        if (routes.Count <= 1) return 0;
        if (bag.Count == 0)
        {
            for (int i = 0; i < routes.Count; i++) bag.Add(i);
            for (int i = bag.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int tmp = bag[i]; bag[i] = bag[j]; bag[j] = tmp;
            }
        }
        int id = bag[bag.Count - 1];
        bag.RemoveAt(bag.Count - 1);
        return id;
    }
}
