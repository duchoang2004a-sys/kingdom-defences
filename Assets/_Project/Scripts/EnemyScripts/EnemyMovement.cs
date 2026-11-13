using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Xử lý di chuyển của quái vật tuần tự qua các Waypoints theo chuẩn đề bài Kingdom Rush
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

    private float statusSpeedMultiplier = 1f;
    private StatusEffectController statusController;

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
        InitSpeed();
        SetTargetImmediate();
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
            // Kiểm tra xem có Blocker nào gần đây có thể chặn mình không
            var blockers = BlockerRegistry.ActiveBlockers;
            for (int i = 0; i < blockers.Count; i++)
            {
                IBlocker b = blockers[i];
                if (b != null && b.IsAlive && b.CanBlockMore())
                {
                    if (Vector2.Distance(transform.position, b.Position) <= 0.9f)
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

        // Tự động lật mặt Sprite theo hướng di chuyển
        if (sr != null && target != null && Mathf.Abs(target.position.x - transform.position.x) > 0.05f)
        {
            sr.flipX = target.position.x < transform.position.x;
        }

        // Di chuyển mượt mà tới mục tiêu bằng Vector2.MoveTowards (chuẩn đề tài 05)
        transform.position = Vector2.MoveTowards(transform.position, target.position, moveSpeed * statusSpeedMultiplier * Time.deltaTime);

        // Kiểm tra xem đã đến gần Waypoint mục tiêu chưa (vòng lặp: xử lý được nhiều waypoint sát nhau)
        int guard = 0;
        while (target != null && guard < 8 && Vector2.Distance(transform.position, target.position) < 0.2f)
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

        if (EnemySpawner.onEnemyKilled != null)
        {
            EnemySpawner.onEnemyKilled.Invoke();
        }

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
