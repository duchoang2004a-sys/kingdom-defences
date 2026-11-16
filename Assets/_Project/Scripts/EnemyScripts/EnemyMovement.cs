using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Xử lý di chuyển của quái vật tuần tự qua các Waypoints theo chuẩn đề bài Kingdom Rush.
// v2 (SESSION_20261113_B): đường đi rộng có 3 làn; quái thường biết lách blocker (lính/hero) sang làn trống,
// chỉ bị chặn khi cùng làn; boss to bằng cả đường nên luôn bị chặn. Luôn đi tới waypoint cuối (= castle).
public class EnemyMovement : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody2D rb;

    private Enemy enemy;
    private float moveSpeed;
    private float baseSpeed;

    private Transform target;
    private int pathIndex = 0;
    private int pathId = 0;
    private int damage = 1;

    [Header("Melee Engagement / Blocking")]
    private SpriteRenderer sr;
    private IBlocker currentBlocker;
    public bool IsBlocked => currentBlocker != null && currentBlocker.IsAlive;
    private float lastCombatAttackTime = 0f;

    [Header("Speed Variance (Cân bằng game)")]
    [Tooltip("Hệ số biến thiên tốc độ ngẫu nhiên để quái tách đàn, không đi trùng khít lên nhau")]
    public float minSpeedMultiplier = 0.85f;
    public float maxSpeedMultiplier = 1.15f;

    [Header("Lanes (3 làn trên đường đất)")]
    [Tooltip("Cho phép quái thường đổi làn để lách blocker")]
    public bool smartLaneDodge = true;

    private int lane = 1;                 // 0 | 1 | 2
    private float lateral = 0f;           // độ lệch ngang hiện tại so với đường tâm
    private Vector2 appliedOffset = Vector2.zero; // phần lệch đã cộng vào transform.position ở frame trước
    private Vector2 smoothDir = Vector2.right;
    private float laneCheckTimer = 0f;
    private bool dodgeDecided = false;
    private bool willDodge = false;
    private readonly bool[] laneBusy = new bool[3];

    private float statusSpeedMultiplier = 1f;
    private StatusEffectController statusController;

    public int Lane => lane;

    public void ForceDisengage()
    {
        currentBlocker = null;
    }

    public int PathId => pathId;
    public int PathIndex => pathIndex;

    public void SetPath(int id)
    {
        pathId = id;
    }

    public void SetPathAndWaypoint(int id, int waypointIndex)
    {
        pathId = id;
        pathIndex = waypointIndex;
        Transform[] activePath = GetPath();
        if (activePath != null && activePath.Length > 0)
        {
            int safeIndex = Mathf.Clamp(waypointIndex, 0, activePath.Length - 1);
            target = activePath[safeIndex];
        }
    }

    private Transform[] GetPath()
    {
        if (LevelManager.main == null) return null;
        return LevelManager.main.GetRoute(pathId);
    }

    public float DistanceTraveled
    {
        get
        {
            if (target == null) return 0f;
            return (pathIndex * 1000f) - Vector2.Distance(transform.position, target.position);
        }
    }

    public void ApplySlow(float duration, float factor)
    {
        if (statusController == null) statusController = GetComponent<StatusEffectController>();
        if (statusController == null) statusController = gameObject.AddComponent<StatusEffectController>();

        // factor can be speed multiplier (e.g. 0.5f means 50% slow)
        float slowStrength = factor > 0.8f ? (1f - factor) : factor;
        statusController.ApplyEffect(StatusEffectType.Slow, slowStrength, duration);
    }

    public void SetStatusSpeedMultiplier(float mult)
    {
        statusSpeedMultiplier = Mathf.Clamp01(mult);
    }

    private bool IsBossEnemy => enemy != null && enemy.IsBoss;

    private void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
        }
        enemy = GetComponent<Enemy>();
        sr = GetComponentInChildren<SpriteRenderer>();
    }

    private void Start()
    {
        InitSpeed();
    }

    private void InitSpeed()
    {
        float rawSpeed = enemy != null ? enemy.MoveSpeed : 2f;
        float variance = Random.Range(minSpeedMultiplier, maxSpeedMultiplier);
        baseSpeed = rawSpeed * variance;
        moveSpeed = baseSpeed;
    }

    private void OnEnable()
    {
        pathIndex = 0;
        currentBlocker = null;
        appliedOffset = Vector2.zero;
        lateral = 0f;
        dodgeDecided = false;
        willDodge = false;
        // Boss chiếm cả đường -> làn giữa; quái thường chọn ngẫu nhiên 1 trong 3 làn (ưu tiên làn giữa nhẹ)
        lane = (enemy != null && enemy.IsBoss) ? 1 : PickSpawnLane();
        InitSpeed();
        SetTargetImmediate();
    }

    private static int PickSpawnLane()
    {
        float r = Random.value;
        if (r < 0.30f) return 0;
        if (r < 0.70f) return 1;
        return 2;
    }

    private void OnDisable()
    {
        if (currentBlocker != null)
        {
            currentBlocker.Disengage(this);
            currentBlocker = null;
        }
    }

    public void SetTargetImmediate()
    {
        if (LevelManager.main != null)
        {
            LevelManager.main.EnsurePathAndWaypoints();
        }

        Transform[] activePath = GetPath();
        if (activePath != null && activePath.Length > 0)
        {
            pathIndex = 0;
            // Bỏ qua các waypoint nằm ngay tại vị trí spawn (tránh nhảy cóc / biến mất)
            while (pathIndex < activePath.Length - 1 &&
                   Vector2.Distance(transform.position, activePath[pathIndex].position) < 0.25f)
            {
                pathIndex++;
            }
            target = activePath[pathIndex];
        }
    }

    private void Update()
    {
        // 1. Kiểm tra trạng thái Giao chiến / Bị chặn (Melee Duel with Hero & Soldiers)
        if (currentBlocker != null)
        {
            if (!currentBlocker.IsAlive || Vector2.Distance(transform.position, currentBlocker.Position) > 1.8f)
            {
                currentBlocker.Disengage(this);
                currentBlocker = null;
            }
            else
            {
                // Quay mặt về phía Blocker (Hero hoặc Lính tiếp viện)
                if (sr != null)
                {
                    sr.flipX = currentBlocker.Position.x < transform.position.x;
                }

                // Kiểm tra xem quái có đang bị Choáng (Stun) hoặc Đóng băng (Freeze) hay không
                bool isCCed = false;
                if (statusController == null) statusController = GetComponent<StatusEffectController>();
                if (statusController != null) isCCed = statusController.IsDisabled;

                if (!isCCed)
                {
                    // Thực hiện đòn tấn công theo chu kỳ khi không bị khống chế
                    float interval = enemy != null ? enemy.combatAttackInterval : 1.2f;
                    if (Time.time - lastCombatAttackTime >= interval)
                    {
                        if (enemy != null) enemy.PlayAttackAnimation();
                        float dmg = enemy != null ? enemy.GetCombatDamage() : 8f;
                        currentBlocker.TakeDamage(dmg);
                        lastCombatAttackTime = Time.time;
                    }
                }

                // Khi đang đấu tay đôi -> Tạm dừng di chuyển trên đường
                return;
            }
        }
        else
        {
            // Chỉ bị chặn khi CÙNG LÀN với blocker (khoảng cách nhỏ). Boss to nên bán kính chặn rộng hơn.
            float engage = IsBossEnemy ? GameScale.BossEngageRange : GameScale.EngageRange;
            var blockers = BlockerRegistry.ActiveBlockers;
            for (int i = 0; i < blockers.Count; i++)
            {
                IBlocker b = blockers[i];
                if (b != null && b.IsAlive && b.CanBlockMore())
                {
                    if (Vector2.Distance(transform.position, b.Position) <= engage)
                    {
                        currentBlocker = b;
                        b.Engage(this);
                        lastCombatAttackTime = Time.time + 0.3f;
                        return;
                    }
                }
            }
        }

        if (target == null)
        {
            Transform[] activePath = GetPath();
            if (activePath == null || activePath.Length == 0)
            {
                if (LevelManager.main != null)
                {
                    LevelManager.main.EnsurePathAndWaypoints();
                    activePath = GetPath();
                }
            }

            if (activePath != null && activePath.Length > 0 && pathIndex < activePath.Length)
            {
                target = activePath[pathIndex];
            }
            if (target == null) return;
        }

        // Vị trí trên ĐƯỜNG TÂM = vị trí hiện tại trừ phần lệch làn đã cộng ở frame trước
        // (nhờ vậy teleport/đẩy lùi từ script khác vẫn được tôn trọng).
        Vector2 center = (Vector2)transform.position - appliedOffset;
        Vector2 toTarget = (Vector2)target.position - center;
        if (toTarget.sqrMagnitude > 0.0001f)
        {
            Vector2 dir = toTarget.normalized;
            smoothDir = Vector2.Lerp(smoothDir, dir, 1f - Mathf.Exp(-10f * Time.deltaTime));
            if (smoothDir.sqrMagnitude < 0.0001f) smoothDir = dir;
            smoothDir.Normalize();
        }

        // Tự động lật mặt Sprite theo hướng di chuyển
        if (sr != null && Mathf.Abs(target.position.x - center.x) > 0.05f)
        {
            sr.flipX = target.position.x < center.x;
        }

        // Chọn làn (lách blocker) rồi trượt ngang mượt sang làn đó
        if (smartLaneDodge && !IsBossEnemy) UpdateLaneChoice(center, smoothDir);
        float wantLateral = IsBossEnemy ? 0f : GameScale.LaneOffset(lane);
        lateral = Mathf.MoveTowards(lateral, wantLateral, GameScale.LaneSwitchSpeed * Time.deltaTime);

        // Di chuyển mượt mà dọc đường tâm tới mục tiêu bằng Vector2.MoveTowards (chuẩn đề tài 05)
        center = Vector2.MoveTowards(center, target.position, moveSpeed * statusSpeedMultiplier * Time.deltaTime);
        Vector2 normal = new Vector2(-smoothDir.y, smoothDir.x);
        appliedOffset = normal * lateral;
        transform.position = new Vector3(center.x + appliedOffset.x, center.y + appliedOffset.y, transform.position.z);

        // Kiểm tra xem đã đến gần Waypoint mục tiêu chưa (tính trên ĐƯỜNG TÂM; xử lý được nhiều waypoint sát nhau)
        int guard = 0;
        while (target != null && guard < 8 && Vector2.Distance(center, target.position) < 0.2f)
        {
            guard++;
            pathIndex++;
            Transform[] activePath = GetPath();

            // Quái đã đi hết tất cả Waypoints và đột nhập thành công vào Lâu đài
            if (activePath == null || pathIndex >= activePath.Length)
            {
                EnemyReachedEnd();
                return;
            }

            target = activePath[pathIndex];
        }
    }

    /// <summary>
    /// Nhìn trước theo hướng đi: nếu làn hiện tại có blocker còn nhận thêm được quái, với xác suất DodgeChance
    /// (quyết 1 lần cho mỗi vật cản) đổi sang làn trống gần nhất. Cả 3 làn đều bị chiếm -> không lách được, đành bị chặn.
    /// </summary>
    private void UpdateLaneChoice(Vector2 center, Vector2 dir)
    {
        laneCheckTimer -= Time.deltaTime;
        if (laneCheckTimer > 0f) return;
        laneCheckTimer = 0.1f;

        laneBusy[0] = laneBusy[1] = laneBusy[2] = false;
        Vector2 normal = new Vector2(-dir.y, dir.x);
        float reach = GameScale.EngageRange * 1.15f;
        var blockers = BlockerRegistry.ActiveBlockers;
        for (int i = 0; i < blockers.Count; i++)
        {
            IBlocker b = blockers[i];
            if (b == null || !b.IsAlive || !b.CanBlockMore()) continue;
            Vector2 rel = b.Position - center;
            float along = Vector2.Dot(rel, dir);
            if (along < -0.15f || along > GameScale.DodgeLookAhead) continue;
            float lat = Vector2.Dot(rel, normal);
            for (int l = 0; l < 3; l++)
            {
                if (Mathf.Abs(lat - GameScale.LaneOffset(l)) <= reach) laneBusy[l] = true;
            }
        }

        if (!laneBusy[lane])
        {
            dodgeDecided = false;   // làn hiện tại thông thoáng -> lần sau gặp vật cản sẽ quyết định lại
            return;
        }

        if (!dodgeDecided)
        {
            dodgeDecided = true;
            willDodge = Random.value < GameScale.DodgeChance;
        }
        if (!willDodge) return;

        int best = -1;
        for (int step = 1; step <= 2 && best < 0; step++)
        {
            int a = lane - step, c = lane + step;
            bool okA = a >= 0 && !laneBusy[a];
            bool okC = c <= 2 && !laneBusy[c];
            if (okA && okC) best = Random.value < 0.5f ? a : c;
            else if (okA) best = a;
            else if (okC) best = c;
        }
        if (best >= 0) lane = best;
    }

    public void UpdateSpeed(float newSpeed)
    {
        moveSpeed = newSpeed;
    }

    public void ResetSpeed()
    {
        moveSpeed = baseSpeed;
    }

    private void EnemyReachedEnd()
    {
        int livesLost = damage;
        if (enemy != null && enemy.Data != null && enemy.Data.Damage > 0)
        {
            livesLost = enemy.Data.Damage;
        }

        // Phát tín hiệu sự kiện quái lọt về lâu đài chuẩn Part A3
        GameEvents.TriggerEnemyEscaped(enemy, livesLost);

        if (LevelManager.main != null)
        {
            LevelManager.main.LoseLives(livesLost);
        }

        EnemyHealth.onEnemyDestroy.Invoke();

        // Quái lọt về lâu đài KHÔNG phải quái bị diệt: không phát onEnemyKilled
        // (trước đây nhiệm vụ "Diệt N kẻ địch" bị tính cả quái lọt).

        if (ObjectPool.instance != null)
        {
            ObjectPool.instance.ReturnToPool(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
