using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quản lý binh lính tiếp viện (Reinforcements / Barracks Soldier):
/// Xuất hiện chặn đường 1 quái vật, đấu kiếm tay đôi, tồn tại trong 30 giây hoặc đến khi tử trận.
/// Triển khai IBlocker chuẩn Kingdom Rush.
/// v3 (SESSION_20261115_E): lính Barracks có tầm nhìn - Barracks phân công quái (SetTarget), lính chủ động lao tới và tự đổi làn để chặn;
/// nhiều lính có thể cùng đánh một quái. Lính tiếp viện triệu hồi bằng vật phẩm (không có Barracks) giữ hành vi cũ.
/// </summary>
public class ReinforcementSoldier : MonoBehaviour, IBlocker
{
    [Header("Stats")]
    public float maxHealth = 90f;
    public float currentHealth;
    public float attackDamage = 12f;
    public float attackCooldown = 0.9f;
    public float lifetime = 30f;
    public bool isPermanent = false;
    public float armor = 0f; // 0.0 to 1.0 (0% to 100% reduction)
    public float regenRate = 0f; // HP per second

    [Header("Visual")]
    public Animator animator;
    public SpriteRenderer spriteRenderer;

    private EnemyMovement engagedEnemy;
    private float lastAttackTime;
    private float lastDamageTime;
    private bool isDead = false;
    private Barracks parentBarracks;
    private Vector2 rallyPosition;

    public Vector2 Position => transform.position;
    public bool IsAlive => !isDead && currentHealth > 0;
    public int MaxBlockedCount => 1;
    public int CurrentlyBlockedCount => engagedEnemy != null ? 1 : 0;

    public void SetBarracks(Barracks barracks) => parentBarracks = barracks;
    public void SetRallyPosition(Vector2 pos) => rallyPosition = pos;

    // ---- v3 (SESSION_20261115_E): tầm nhìn - quái do Barracks phân công trong vòng tròn LayerMask
    [Header("Tầm nhìn / lao tới mục tiêu (v3)")]
    [Tooltip("Tốc độ lao tới quái được phân công (đơn vị/giây); lính tự đổi sang làn của quái để chặn.")]
    public float chaseSpeed = 2.6f;
    private EnemyMovement assignedTarget;

    public EnemyMovement AssignedTarget => assignedTarget;
    public EnemyMovement EngagedEnemy => engagedEnemy;
    public bool IsEngaged => engagedEnemy != null;
    public void SetTarget(EnemyMovement enemy) => assignedTarget = enemy;

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void OnEnable()
    {
        BlockerRegistry.Register(this);
    }

    private void OnDisable()
    {
        DisengageAll();
        BlockerRegistry.Unregister(this);
    }

    private void Start()
    {
        currentHealth = maxHealth;
        lastAttackTime = Time.time;
        lastDamageTime = Time.time;
        if (!isPermanent)
        {
            StartCoroutine(LifetimeRoutine());
        }
    }

    private IEnumerator LifetimeRoutine()
    {
        yield return new WaitForSeconds(lifetime);
        if (!isDead && !isPermanent)
        {
            Die();
        }
    }

    private void Update()
    {
        if (isDead) return;

        HandleRegen();

        // Dọn dẹp nếu quái đang chặn đã bị tiêu diệt
        if (engagedEnemy != null && (!engagedEnemy.gameObject.activeInHierarchy || engagedEnemy.GetComponent<EnemyHealth>()?.CurrentHealth <= 0))
        {
            engagedEnemy = null;
        }

        // v3: quái được Barracks phân công đã chết / biến mất thì bỏ (lần quét kế tiếp sẽ phân công lại)
        if (assignedTarget != null && !IsEnemyValid(assignedTarget))
        {
            assignedTarget = null;
        }

        // Đối thủ = quái đang đấu tay đôi với mình; chưa có thì quái được Barracks phân công trong tầm nhìn
        EnemyMovement fightTarget = engagedEnemy != null ? engagedEnemy : assignedTarget;
        if (fightTarget != null)
        {
            Vector2 myPos = transform.position;
            Vector2 tp = fightTarget.transform.position;
            float dist = Vector2.Distance(myPos, tp);

            if (spriteRenderer != null && Mathf.Abs(tp.x - myPos.x) > 0.02f)
            {
                spriteRenderer.flipX = tp.x < myPos.x;
            }

            // Chưa bị quái bắt vào đấu tay đôi -> chủ động lao tới quái (tự đổi sang làn của quái) rồi dừng sát quái.
            // Dừng ở khoảng cách < EngageRange để quái nhận lính làm blocker và dừng lại đấu với lính.
            if (engagedEnemy == null && dist > GameScale.EngageRange * 0.8f)
            {
                transform.position = Vector2.MoveTowards(myPos, tp, chaseSpeed * Time.deltaTime);
                if (animator != null) animator.SetFloat("Speed", 1f);
            }
            else if (animator != null)
            {
                animator.SetFloat("Speed", 0f);
            }

            // Đánh khi đang đấu tay đôi (như cũ) hoặc khi đã áp sát quái được phân công (nhiều lính cùng đánh 1 quái)
            bool canHit = engagedEnemy != null || dist <= GameScale.EngageRange * 1.6f;
            if (canHit && Time.time - lastAttackTime >= attackCooldown)
            {
                if (animator != null) animator.SetTrigger("Attack");

                EnemyHealth eh = fightTarget.GetComponent<EnemyHealth>();
                if (eh != null && eh.CurrentHealth > 0)
                {
                    DamageInfo dmg = new DamageInfo(attackDamage, DamageType.Physical, gameObject);
                    eh.TakeDamage(dmg);
                }

                lastAttackTime = Time.time;
            }
        }
        else if (isPermanent)
        {
            // Quay về điểm tập kết nếu không giao chiến
            float distToRally = Vector2.Distance(transform.position, rallyPosition);
            if (distToRally > 0.1f)
            {
                transform.position = Vector2.MoveTowards(transform.position, rallyPosition, 2.5f * Time.deltaTime);
                if (animator != null) animator.SetFloat("Speed", 1f);
                if (spriteRenderer != null && Mathf.Abs(rallyPosition.x - transform.position.x) > 0.05f)
                {
                    spriteRenderer.flipX = rallyPosition.x < transform.position.x;
                }
            }
            else
            {
                if (animator != null) animator.SetFloat("Speed", 0f);
            }
        }
    }

    private static bool IsEnemyValid(EnemyMovement enemy)
    {
        if (enemy == null || !enemy.gameObject.activeInHierarchy) return false;
        EnemyHealth eh = enemy.GetComponent<EnemyHealth>();
        return eh != null && eh.CurrentHealth > 0f && !eh.IsDestroyed;
    }

    private void HandleRegen()
    {
        if (regenRate > 0 && engagedEnemy == null && Time.time - lastDamageTime >= 3f && currentHealth < maxHealth)
        {
            currentHealth = Mathf.Min(maxHealth, currentHealth + regenRate * Time.deltaTime);
        }
    }

    #region IBlocker Implementation
    public bool CanBlockMore()
    {
        return IsAlive && engagedEnemy == null;
    }

    public void Engage(EnemyMovement enemy)
    {
        if (enemy != null && engagedEnemy == null)
        {
            engagedEnemy = enemy;
            if (animator != null) animator.SetFloat("Speed", 0f);
        }
    }

    public void Disengage(EnemyMovement enemy)
    {
        if (engagedEnemy == enemy)
        {
            engagedEnemy = null;
        }
    }

    public void DisengageAll()
    {
        if (engagedEnemy != null)
        {
            engagedEnemy.ForceDisengage();
            engagedEnemy = null;
        }
    }
    #endregion

    public void TakeDamage(float amount)
    {
        if (isDead) return;

        // Áp dụng giáp
        float reducedAmount = amount * (1f - Mathf.Clamp(armor, 0f, 0.8f));
        currentHealth -= reducedAmount;
        lastDamageTime = Time.time;

        if (animator != null && currentHealth > 0)
        {
            animator.SetTrigger("Hurt");
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        currentHealth = 0f;

        DisengageAll();

        if (animator != null)
        {
            animator.SetTrigger("Die");
        }

        if (isPermanent && parentBarracks != null)
        {
            parentBarracks.ReportDeath(gameObject);
        }

        Destroy(gameObject, 0.6f);
    }
}
