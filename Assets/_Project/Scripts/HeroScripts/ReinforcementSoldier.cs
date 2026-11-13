using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quản lý binh lính tiếp viện (Reinforcements / Barracks Soldier):
/// Xuất hiện chặn đường 1 quái vật, đấu kiếm tay đôi, tồn tại trong 30 giây hoặc đến khi tử trận.
/// Triển khai IBlocker chuẩn Kingdom Rush.
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

        // Đấu tay đôi với quái đang chặn
        if (engagedEnemy != null)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = engagedEnemy.transform.position.x < transform.position.x;
            }

            if (Time.time - lastAttackTime >= attackCooldown)
            {
                if (animator != null) animator.SetTrigger("Attack");

                EnemyHealth eh = engagedEnemy.GetComponent<EnemyHealth>();
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
