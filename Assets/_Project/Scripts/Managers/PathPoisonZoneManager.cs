using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ActivePoisonZone
{
    public int pathId;
    public float centerArc;
    public float length = 5.0f;
    public float durationRemaining = 12.0f;
    public float tickTimer = 0f;
    public GameObject visualObj;

    public ActivePoisonZone(int pathId, float centerArc, float length, float duration, GameObject visual)
    {
        this.pathId = pathId;
        this.centerArc = centerArc;
        this.length = length;
        this.durationRemaining = duration;
        this.tickTimer = 0f;
        this.visualObj = visual;
    }
}

/// <summary>
/// Quản lý Vùng Độc Theo Đường Đi (PathPoisonZoneManager - Part B3):
/// - Tận dụng độ dài tích lũy theo chiều dài cung (arc distance s) để kiểm tra va chạm 1D siêu nhanh (|s_quái - s_tâm| <= L/2).
/// - Áp dụng Làm chậm 35%, Độc DoT (duy trì 2s sau khi rời vùng) và Vết Thương (Anti-heal - Khóa hồi máu Boss trong 3s).
/// - Chế độ Nhắm Đặt Tương Tác: Tối đa 2 vùng độc cùng lúc, kéo thả trực quan theo dải đường.
/// </summary>
public class PathPoisonZoneManager : MonoBehaviour
{
    public static PathPoisonZoneManager main;

    [Header("Active Poison Zones (Max 2)")]
    public List<ActivePoisonZone> activeZones = new List<ActivePoisonZone>();

    [Header("Placement Mode State")]
    public bool isTargetingPlacement = false;

    // Cache cumulative distances for waypoints
    private readonly Dictionary<int, float[]> pathCumulativeDistances = new Dictionary<int, float[]>();

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
        CalculatePathDistances();
    }

    private void CalculatePathDistances()
    {
        pathCumulativeDistances.Clear();

        if (LevelManager.main == null) return;
        LevelManager.main.EnsurePathAndWaypoints();

        for (int r = 0; r < LevelManager.main.RouteCount; r++)
        {
            CachePathArray(r, LevelManager.main.GetRoute(r));
        }
    }

    private void CachePathArray(int pathId, Transform[] path)
    {
        if (path == null || path.Length == 0) return;

        float[] cumDist = new float[path.Length];
        cumDist[0] = 0f;
        for (int i = 1; i < path.Length; i++)
        {
            cumDist[i] = cumDist[i - 1] + Vector2.Distance(path[i - 1].position, path[i].position);
        }
        pathCumulativeDistances[pathId] = cumDist;
    }

    public float GetEnemyArcDistance(EnemyMovement em)
    {
        if (em == null) return 0f;
        int pId = em.PathId;
        int pIdx = em.PathIndex;

        if (!pathCumulativeDistances.ContainsKey(pId))
        {
            CalculatePathDistances();
        }

        if (!pathCumulativeDistances.ContainsKey(pId)) return 0f;
        float[] cumDist = pathCumulativeDistances[pId];

        Transform[] pathArray = LevelManager.main.GetRoute(pId);
        if (pathArray == null || pathArray.Length == 0) return 0f;

        int safeIdx = Mathf.Clamp(pIdx, 0, pathArray.Length - 1);
        float baseArc = cumDist[safeIdx];

        if (safeIdx > 0)
        {
            Vector2 prevPos = pathArray[safeIdx - 1].position;
            Vector2 currPos = em.transform.position;
            float extraDist = Vector2.Distance(prevPos, currPos);
            return cumDist[safeIdx - 1] + extraDist;
        }

        return baseArc;
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        // 1. Quản lý thời gian sống của các Vùng Độc
        for (int i = activeZones.Count - 1; i >= 0; i--)
        {
            ActivePoisonZone zone = activeZones[i];
            zone.durationRemaining -= dt;
            zone.tickTimer += dt;

            // Xử lý nhịp Tick mỗi 0.25s
            if (zone.tickTimer >= 0.25f)
            {
                zone.tickTimer = 0f;
                ProcessPoisonZoneTick(zone);
            }

            if (zone.durationRemaining <= 0)
            {
                if (zone.visualObj != null) Destroy(zone.visualObj);
                activeZones.RemoveAt(i);
            }
        }

        // 2. Chế độ Nhắm Đặt Vùng Độc Tương Tác
        if (isTargetingPlacement)
        {
            if (GameInput.GetMouseButtonDown(0))
            {
                if (InputRouter.CanProcessGameplayClick())
                {
                    Vector2 mousePos = Camera.main != null ? (Vector2)Camera.main.ScreenToWorldPoint(GameInput.MousePosition) : Vector2.zero;
                    if (TryPlacePoisonZoneAt(mousePos))
                    {
                        InputRouter.Consume("PoisonZone_Place");
                        isTargetingPlacement = false;
                    }
                }
            }
            if (GameInput.GetMouseButtonDown(1))
            {
                isTargetingPlacement = false;
            }
        }
    }

    private void ProcessPoisonZoneTick(ActivePoisonZone zone)
    {
        var enemies = EnemyRegistry.ActiveEnemies;

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyHealth eh = enemies[i];
            if (eh == null || !eh.gameObject.activeInHierarchy || eh.CurrentHealth <= 0) continue;

            EnemyMovement em = eh.GetComponent<EnemyMovement>();
            if (em == null || em.PathId != zone.pathId) continue;

            float enemyArc = GetEnemyArcDistance(em);

            // Kiểm tra khoảng cách 1D trên cung đường (|s_quái - s_tâm| <= L/2)
            if (Mathf.Abs(enemyArc - zone.centerArc) <= (zone.length / 2f))
            {
                StatusEffectController sec = eh.GetComponent<StatusEffectController>() ?? eh.gameObject.AddComponent<StatusEffectController>();

                // (1) Làm chậm 35% duy trì 0.6s
                sec.ApplyEffect(StatusEffectType.Slow, 0.35f, 0.6f);

                // (2) Độc DoT duy trì 2.0s
                sec.ApplyEffect(StatusEffectType.Poison, eh.MaxHealth * 0.015f + 8f, 2.0f);

                // (3) Vết Thương (Anti-heal): Khóa toàn bộ tự hồi máu & aura hồi máu trong 3.0s (Khắc tinh của Shaman!)
                sec.ApplyEffect(StatusEffectType.Wounded, 1.0f, 3.0f);

                // Ghi nhận nhận sát thương cho Shaman Boss để ngắt cửa sổ tự hồi máu 5s
                ShamanBoss shaman = eh.GetComponent<ShamanBoss>();
                if (shaman != null)
                {
                    shaman.TrackDamageTaken(eh.MaxHealth * 0.015f + 8f);
                }
            }
        }
    }

    public void StartPlacementMode()
    {
        if (activeZones.Count >= 2)
        {
            Debug.LogWarning("[PoisonZone] Đã đạt giới hạn tối đa 2 Vùng Độc cùng lúc!");
            return;
        }

        isTargetingPlacement = true;
        Debug.Log("[PoisonZone] Click chọn đoạn đường để trải Vùng Độc!");
    }

    public bool TryPlacePoisonZoneAt(Vector2 worldPos)
    {
        if (activeZones.Count >= 2) return false;

        // Tìm đoạn đường gần nhất với vị trí click chuột
        if (LevelManager.main == null || LevelManager.main.RouteCount == 0) return false;

        int bestPathId = 0;
        float bestDist = float.MaxValue;
        float bestArc = 0f;

        for (int r = 0; r < LevelManager.main.RouteCount; r++)
        {
            Transform[] routePts = LevelManager.main.GetRoute(r);
            if (routePts == null || routePts.Length < 2) continue;

            float arcR = FindClosestArcOnPath(r, routePts, worldPos, out float dR);
            if (dR < bestDist)
            {
                bestDist = dR;
                bestArc = arcR;
                bestPathId = r;
            }
        }

        if (bestDist > 3.0f)
        {
            Debug.LogWarning("[PoisonZone] Điểm chọn cách quá xa đường đi!");
            return false;
        }

        // Tạo hiệu ứng thị giác Vùng Độc
        GameObject visualObj = CreatePoisonZoneVisual(worldPos);

        ActivePoisonZone newZone = new ActivePoisonZone(bestPathId, bestArc, 5.0f, 12.0f, visualObj);
        activeZones.Add(newZone);

        GameEvents.TriggerSpellOrItemUsed("PoisonZone");

        Debug.Log($"<color=green>[PoisonZone]</color> Đã trải Vùng Độc tại cung đường {bestPathId} (Tâm Arc: {bestArc:F1}) trong 12s!");
        return true;
    }

    private float FindClosestArcOnPath(int pId, Transform[] path, Vector2 point, out float minDistance)
    {
        minDistance = float.MaxValue;
        float closestArc = 0f;
        if (path == null || path.Length < 2) return 0f;

        if (!pathCumulativeDistances.ContainsKey(pId)) CalculatePathDistances();
        float[] cumDist = pathCumulativeDistances[pId];

        for (int i = 0; i < path.Length - 1; i++)
        {
            Vector2 a = path[i].position;
            Vector2 b = path[i + 1].position;

            Vector2 proj = Vector2.ClampMagnitude(Vector2.Dot(point - a, (b - a).normalized) * (b - a).normalized, (b - a).magnitude);
            Vector2 closestPoint = a + proj;

            float dist = Vector2.Distance(point, closestPoint);
            if (dist < minDistance)
            {
                minDistance = dist;
                closestArc = cumDist[i] + proj.magnitude;
            }
        }

        return closestArc;
    }

    private GameObject CreatePoisonZoneVisual(Vector2 pos)
    {
        GameObject obj = new GameObject("PoisonZoneVisual");
        obj.transform.position = pos;

        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.color = new Color(0.2f, 0.85f, 0.3f, 0.45f);
        sr.sortingOrder = 4;

        Texture2D tex = new Texture2D(32, 32);
        Color[] colors = new Color[1024];
        for (int i = 0; i < 1024; i++) colors[i] = Color.white;
        tex.SetPixels(colors);
        tex.Apply();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 16f);
        obj.transform.localScale = new Vector3(3.0f, 1.2f, 1f);

        return obj;
    }
}
