using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quản lý logic Anh hùng (Hero):
/// Di chuyển tự do, tự động tấn công kẻ địch gần nhất, hồi phục HP khi rời giao tranh 3s,
/// kích hoạt 2 kỹ năng chủ động (Skill 1: Chém choáng, Skill 2: Gầm giảm giáp) và cơ chế Hồi sinh (Respawn).
/// Triển khai IBlocker: Có khả năng chặn đứng và giao chiến cận chiến (Melee Duel) với tối đa 2-3 quái cùng lúc.
/// Trí tuệ AI chiến thuật: Phân biệt Tầm nhìn (Vision Range) vs Tầm đánh (Attack Range) và Bán kính neo (Leash Radius).
/// </summary>
public class Hero : MonoBehaviour, IBlocker
{
    [Header("Identity")]
    public string heroId = "Knight";
    private float shieldEndTime = 0f;

    [Header("Visual & Animation")]
    public Animator animator;
    public SpriteRenderer spriteRenderer;

    [Header("Movement")]
    public float moveSpeed = 3.5f;
    private Vector2 targetPosition;
    private bool isMoving = false;

    [Header("Combat & Melee Duel")]
    [Tooltip("Tầm chém cận chiến phù hợp với kích thước Sprite (~1.4 unit)")]
    public float attackRange = 1.4f;
    public float attackDamage = 25f;
    public float attackCooldown = 0.8f;
    private float lastAttackTime;

    [Header("Tactical Vision & Leash")]
    [Tooltip("Tầm nhìn phát hiện quái vật")]
    public float visionRange = 3.8f;
    [Tooltip("Bán kính neo quanh chốt gác - Hero không bao giờ đuổi theo quái vượt quá bán kính này")]
    public float leashRadius = 3.5f;
    public Vector2 guardPostPosition;
    public bool isSelected = false;

    public enum HeroAIState { Guarding, Intercepting, Dueling, Returning, Dead }
    [Header("Current AI State")]
    public HeroAIState currentState = HeroAIState.Guarding;

    [Header("Blocking / Chặn Quái")]
    [Tooltip("Số lượng quái tối đa có thể giữ chân cùng lúc (Kingdom Rush Knight thường là 2-3 quái)")]
    public int maxBlockedEnemies = 2;
    private readonly List<EnemyMovement> engagedEnemies = new List<EnemyMovement>();

    public Vector2 Position => transform.position;
    public bool IsAlive => !isDead && currentHealth > 0;
    public int MaxBlockedCount => maxBlockedEnemies;
    public int CurrentlyBlockedCount => engagedEnemies.Count;

    [Header("Health & Respawn")]
    public float maxHealth = 200f;
    public float currentHealth;
    public float regenRate = 8f;
    public float respawnDuration = 12f;
    public bool isDead = false;
    private float lastDamageTime;
    private Vector2 spawnPoint;

    [Header("Skills")]
    public float skill1Cooldown = 10f;
    private float lastSkill1Time = -10f;
    public float skill2Cooldown = 20f;
    private float lastSkill2Time = -20f;

    public float Skill1CooldownRemaining => Mathf.Max(0f, skill1Cooldown - (Time.time - lastSkill1Time));
    public float Skill2CooldownRemaining => Mathf.Max(0f, skill2Cooldown - (Time.time - lastSkill2Time));
    /// <summary>Thời gian còn lại tới đòn đánh thường kế tiếp (HUD hiển thị hồi chiêu đánh thường).</summary>
    public float AttackCooldownRemaining => Mathf.Max(0f, attackCooldown - (Time.time - lastAttackTime));

    private HeroRangeVisualizer rangeVisualizer;

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (rangeVisualizer == null) rangeVisualizer = GetComponent<HeroRangeVisualizer>() ?? gameObject.AddComponent<HeroRangeVisualizer>();
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
        spawnPoint = transform.position;
        targetPosition = transform.position;
        guardPostPosition = transform.position;
        lastDamageTime = Time.time;
        lastSkill1Time = -skill1Cooldown;
        lastSkill2Time = -skill2Cooldown;

        if (rangeVisualizer != null)
        {
            rangeVisualizer.RedrawCircles(attackRange, visionRange);
            rangeVisualizer.SetVisible(false);
        }
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        if (rangeVisualizer != null)
        {
            rangeVisualizer.SetVisible(selected);
        }
    }

    private void Update()
    {
        if (isDead)
        {
            currentState = HeroAIState.Dead;
            return;
        }

        HandleMovement();
        HandleRegen();
        HandleAutoSkills();   // SESSION_20261115_G: chiêu hồi xong là dùng trước (chiêu hồi lâu nhất xét trước), rồi mới tới đánh thường

        if (!isMoving)
        {
            HandleTacticalAI();
        }
    }

    #region IBlocker Implementation
    public bool CanBlockMore()
    {
        return IsAlive && !isMoving && engagedEnemies.Count < maxBlockedEnemies;
    }

    public void Engage(EnemyMovement enemy)
    {
        if (enemy != null && !engagedEnemies.Contains(enemy))
        {
            engagedEnemies.Add(enemy);
        }
    }

    public void Disengage(EnemyMovement enemy)
    {
        if (enemy != null)
        {
            engagedEnemies.Remove(enemy);
        }
    }

    public void DisengageAll()
    {
        for (int i = 0; i < engagedEnemies.Count; i++)
        {
            if (engagedEnemies[i] != null)
            {
                engagedEnemies[i].ForceDisengage();
            }
        }
        engagedEnemies.Clear();
    }
    #endregion

    private void HandleMovement()
    {
        if (isMoving)
        {
            transform.position = Vector2.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);

            if (spriteRenderer != null && Mathf.Abs(targetPosition.x - transform.position.x) > 0.05f)
            {
                spriteRenderer.flipX = targetPosition.x < transform.position.x;
            }

            if (animator != null)
            {
                animator.SetFloat("Speed", 1f);
            }

            if (Vector2.Distance(transform.position, targetPosition) < 0.1f)
            {
                isMoving = false;
                guardPostPosition = targetPosition; // Vị trí mới trở thành Chốt gác (Guard Post)
                currentState = HeroAIState.Guarding;
                if (animator != null) animator.SetFloat("Speed", 0f);
            }
        }
        else
        {
            if (animator != null && currentState == HeroAIState.Guarding)
            {
                animator.SetFloat("Speed", 0f);
            }
        }
    }

    public void MoveTo(Vector2 position)
    {
        if (isDead) return;

        // Khi người chơi ra lệnh di chuyển -> Tách khỏi các quái đang chặn để chạy đến chốt gác mới
        DisengageAll();
        targetPosition = position;
        guardPostPosition = position;
        isMoving = true;
        currentState = HeroAIState.Guarding;
    }

    private void HandleTacticalAI()
    {
        // 1. Dọn dẹp quái đã chết
        engagedEnemies.RemoveAll(e => e == null || !e.gameObject.activeInHierarchy);

        // 2. Nếu đang có quái bị chặn đấu tay đôi -> Dueling!
        if (engagedEnemies.Count > 0)
        {
            currentState = HeroAIState.Dueling;
            HandleCombat();
            return;
        }

        // 3. Nếu chưa có quái bị chặn -> Quét Tầm nhìn (Vision Range) & Bán kính neo (Leash Radius)
        var enemies = EnemyRegistry.ActiveEnemies;
        EnemyHealth bestTarget = null;
        float closestDist = visionRange;

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyHealth eh = enemies[i];
            if (eh == null || !eh.gameObject.activeInHierarchy || eh.CurrentHealth <= 0) continue;

            // Kiểm tra xem quái có nằm trong bán kính neo quanh chốt gác không
            float distFromPost = Vector2.Distance(guardPostPosition, eh.transform.position);
            if (distFromPost > leashRadius) continue;

            float distFromHero = Vector2.Distance(transform.position, eh.transform.position);
            if (distFromHero <= closestDist)
            {
                closestDist = distFromHero;
                bestTarget = eh;
            }
        }

        // 4. Quyết định hành vi theo mục tiêu tìm được
        if (bestTarget != null)
        {
            if (closestDist <= attackRange)
            {
                // Đã trong tầm đánh -> Dừng lại vung đòn!
                currentState = HeroAIState.Dueling;
                HandleCombat();
            }
            else
            {
                // Quái nằm trong tầm nhìn -> Tự động bước chân ra đón đầu chặn đường (Intercepting)!
                currentState = HeroAIState.Intercepting;
                transform.position = Vector2.MoveTowards(transform.position, bestTarget.transform.position, moveSpeed * Time.deltaTime);

                if (spriteRenderer != null)
                {
                    spriteRenderer.flipX = bestTarget.transform.position.x < transform.position.x;
                }
                if (animator != null)
                {
                    animator.SetFloat("Speed", 1f);
                }
            }
        }
        else
        {
            // Không có quái trong tầm nhìn hoặc quái đã thoát khỏi bán kính neo
            // Nếu Hero đang đứng lệch khỏi Chốt gác -> Tự động đi bộ quay về Chốt gác (Returning)!
            float distToPost = Vector2.Distance(transform.position, guardPostPosition);
            if (distToPost > 0.2f)
            {
                currentState = HeroAIState.Returning;
                transform.position = Vector2.MoveTowards(transform.position, guardPostPosition, moveSpeed * Time.deltaTime);

                if (spriteRenderer != null)
                {
                    spriteRenderer.flipX = guardPostPosition.x < transform.position.x;
                }
                if (animator != null)
                {
                    animator.SetFloat("Speed", 1f);
                }
            }
            else
            {
                // Đã đứng yên tại chốt gác quan sát (Guarding)
                currentState = HeroAIState.Guarding;
                if (animator != null)
                {
                    animator.SetFloat("Speed", 0f);
                }
            }
        }
    }

    private void HandleCombat()
    {
        if (Time.time - lastAttackTime < attackCooldown) return;

        EnemyHealth targetEnemyHealth = null;

        // 1. Ưu tiên số 1: Tấn công quái vật đang chặn tay đôi trước
        if (engagedEnemies.Count > 0)
        {
            for (int i = 0; i < engagedEnemies.Count; i++)
            {
                EnemyMovement em = engagedEnemies[i];
                if (em != null && em.gameObject.activeInHierarchy)
                {
                    EnemyHealth eh = em.GetComponent<EnemyHealth>();
                    if (eh != null && eh.CurrentHealth > 0)
                    {
                        targetEnemyHealth = eh;
                        break;
                    }
                }
            }
        }

        // 2. Nếu không có quái đang chặn -> Tấn công quái gần nhất trong tầm đánh
        if (targetEnemyHealth == null)
        {
            var enemies = EnemyRegistry.ActiveEnemies;
            float closestDist = attackRange;
            Vector2 myPos = transform.position;

            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyHealth eh = enemies[i];
                if (eh == null || !eh.gameObject.activeInHierarchy || eh.CurrentHealth <= 0) continue;

                float dist = Vector2.Distance(myPos, eh.transform.position);
                if (dist <= closestDist)
                {
                    closestDist = dist;
                    targetEnemyHealth = eh;
                }
            }
        }

        // Thực hiện đòn đánh
        if (targetEnemyHealth != null)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = targetEnemyHealth.transform.position.x < transform.position.x;
            }

            if (animator != null && animator.hasRootMotion)
            {
                animator.SetTrigger("Attack");
            }

            // Kích hoạt hiệu ứng vung đòn (Lunge & Slash Visual Effect)
            PlayAttackVisualEffect(targetEnemyHealth.transform.position);

            DamageInfo dmg = new DamageInfo(attackDamage, DamageType.Physical, gameObject);
            targetEnemyHealth.TakeDamage(dmg);
            lastAttackTime = Time.time;
        }
    }

    private void PlayAttackVisualEffect(Vector3 targetPos)
    {
        StartCoroutine(AttackAnimationRoutine(targetPos));
    }

    private System.Collections.IEnumerator AttackAnimationRoutine(Vector3 targetPos)
    {
        Vector3 originalPos = transform.position;
        Vector3 lungePos = Vector3.Lerp(originalPos, targetPos, 0.35f);

        float dir = (targetPos.x < originalPos.x) ? 1f : -1f;
        Quaternion originalRot = transform.rotation;
        Quaternion slashRot = Quaternion.Euler(0, 0, dir * 25f);

        // 1. Lao lên vung kiếm (Lunge Forward)
        float elapsed = 0f;
        while (elapsed < 0.1f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / 0.1f;
            transform.position = Vector3.Lerp(originalPos, lungePos, t);
            transform.rotation = Quaternion.Lerp(originalRot, slashRot, t);
            yield return null;
        }

        // Tạo Vết Chém (Slash FX) tại vị trí quái
        CreateSlashVisualFX(targetPos);

        // 2. Lùi về vị trí cũ
        elapsed = 0f;
        while (elapsed < 0.12f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / 0.12f;
            transform.position = Vector3.Lerp(lungePos, originalPos, t);
            transform.rotation = Quaternion.Lerp(slashRot, originalRot, t);
            yield return null;
        }

        transform.position = originalPos;
        transform.rotation = originalRot;
    }

    private void CreateSlashVisualFX(Vector3 pos)
    {
        GameObject slashObj = new GameObject("HeroSlashFX");
        slashObj.transform.position = pos + new Vector3(0, 0.3f, 0);

        SpriteRenderer sr = slashObj.AddComponent<SpriteRenderer>();
        sr.color = new Color(1f, 0.9f, 0.3f, 0.9f);
        sr.sortingOrder = 60;

        Texture2D tex = new Texture2D(32, 32);
        Color[] cols = new Color[32 * 32];
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                bool isSlashLine = Mathf.Abs(x - y) <= 3;
                cols[y * 32 + x] = isSlashLine ? Color.white : Color.clear;
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);

        StartCoroutine(AnimateSlashFX(slashObj));
    }

    private System.Collections.IEnumerator AnimateSlashFX(GameObject slashObj)
    {
        float dur = 0.2f;
        float el = 0f;
        Vector3 startS = Vector3.one * 0.8f;
        Vector3 endS = Vector3.one * 1.6f;

        SpriteRenderer sr = slashObj.GetComponent<SpriteRenderer>();

        while (el < dur)
        {
            if (slashObj == null) yield break;
            el += Time.deltaTime;
            float t = el / dur;

            slashObj.transform.localScale = Vector3.Lerp(startS, endS, t);
            if (sr != null)
            {
                Color c = sr.color;
                c.a = 1f - t;
                sr.color = c;
            }
            yield return null;
        }

        if (slashObj != null) Destroy(slashObj);
    }

    private void HandleRegen()
    {
        // Chỉ hồi máu khi rời giao tranh (không chịu sát thương và không đang giữ chân quái)
        if (engagedEnemies.Count == 0 && Time.time - lastDamageTime >= 3f && currentHealth < maxHealth)
        {
            currentHealth += regenRate * Time.deltaTime;
            currentHealth = Mathf.Min(currentHealth, maxHealth);
        }
    }

    public void TakeDamage(float damage)
    {
        if (isDead) return;

        // Nếu Paladin đang bật Khiên Thánh -> Giảm 80% sát thương nhận vào
        if (Time.time < shieldEndTime)
        {
            damage *= 0.2f;
        }

        currentHealth -= damage;
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

    private float slowMultiplier = 1f;
    private float slowEndTime = 0f;

    /// <summary>
    /// Làm chậm tốc độ di chuyển của Hero
    /// </summary>
    public void ApplySlow(float factor, float duration)
    {
        slowMultiplier = Mathf.Clamp(factor, 0.2f, 1f);
        slowEndTime = Time.time + duration;
    }

    public float CurrentMoveSpeed => (Time.time < slowEndTime) ? (moveSpeed * slowMultiplier) : moveSpeed;

    private void Die()
    {
        isDead = true;
        isMoving = false;
        currentHealth = 0f;
        SetSelected(false);

        // Giải phóng toàn bộ quái đang chặn khi tử trận
        DisengageAll();

        if (animator != null)
        {
            animator.SetTrigger("Die");
            animator.SetBool("IsDead", true);
        }

        Debug.Log($"[Hero] Anh hùng tử trận! Quái vật được giải phóng. Sẽ hồi sinh sau {respawnDuration:F0} giây...");
        ObjectiveTracker.NotifyHeroDown();
        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(respawnDuration);

        transform.position = spawnPoint;
        targetPosition = spawnPoint;
        guardPostPosition = spawnPoint;
        currentHealth = maxHealth;
        isDead = false;

        if (animator != null)
        {
            animator.SetBool("IsDead", false);
            animator.Play("Idle");
        }

        Debug.Log("[Hero] Anh hùng đã hồi sinh và sẵn sàng tham chiến!");
    }

    // =====================================================================
    // SESSION_20261115_G - Chiêu thức v5
    //   1) Mỗi chiêu có hiệu ứng riêng (HeroSkillFX): vệt chém + dư ảnh, sóng xung kích, mưa tên, bẫy gai, chuỗi sét, bão băng, khiên, hào quang.
    //   2) Tướng TỰ tung chiêu khi hồi xong và có mục tiêu hợp lệ; chiêu hồi lâu nhất (mạnh nhất) xét trước, sau cùng mới tới đánh thường.
    //   3) Bấm icon chiêu = tung ngay (tự chọn quái / cụm quái), không còn phải click chọn điểm (HeroController.PrepareSkillN -> CastSkillNow).
    // =====================================================================

    [Header("Auto Skills (v5)")]
    [Tooltip("Tự tung chiêu khi hồi xong và có mục tiêu hợp lệ (chiêu hồi lâu nhất ưu tiên trước, rồi mới đánh thường)")]
    public bool autoCastSkills = true;
    private float nextAutoSkillCheck = 0f;
    private const float AutoSkillCheckInterval = 0.2f;
    private const float SlashHalfWidth = 0.55f;

    /// <summary>Độ dài vệt chém Knight: gấp đôi tầm đánh thường (dài hơn tầm đánh ~1-2 ô).</summary>
    private float SlashLength { get { return Mathf.Max(attackRange * 2f, attackRange + 1.2f); } }
    /// <summary>"Diện rộng": phủ cả vùng tầm nhìn của tướng.</summary>
    private float RadiusWide { get { return Mathf.Max(visionRange, attackRange * 2.5f); } }
    private float RadiusLarge { get { return Mathf.Max(3.0f, RadiusWide * 0.9f); } }
    private float RadiusMedium { get { return Mathf.Max(2.4f, RadiusWide * 0.7f); } }
    private float RadiusSmall { get { return Mathf.Max(2.0f, RadiusWide * 0.6f); } }
    private float ChainHop { get { return Mathf.Max(2.0f, visionRange * 0.6f); } }

    private bool IsTarget(EnemyHealth eh)
    {
        return eh != null && eh.gameObject.activeInHierarchy && eh.CurrentHealth > 0;
    }

    private int CountEnemiesNear(Vector2 center, float radius)
    {
        int n = 0;
        var enemies = EnemyRegistry.ActiveEnemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyHealth eh = enemies[i];
            if (IsTarget(eh) && Vector2.Distance(center, eh.transform.position) <= radius) n++;
        }
        return n;
    }

    private void CollectEnemiesNear(Vector2 center, float radius, List<EnemyHealth> into)
    {
        var enemies = EnemyRegistry.ActiveEnemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyHealth eh = enemies[i];
            if (IsTarget(eh) && Vector2.Distance(center, eh.transform.position) <= radius) into.Add(eh);
        }
    }

    private static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float len2 = ab.sqrMagnitude;
        if (len2 < 0.0001f) return Vector2.Distance(p, a);
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
        return Vector2.Distance(p, a + ab * t);
    }

    private void CollectEnemiesAlongSegment(Vector2 a, Vector2 b, float halfWidth, List<EnemyHealth> into)
    {
        var enemies = EnemyRegistry.ActiveEnemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyHealth eh = enemies[i];
            if (IsTarget(eh) && DistToSegment(eh.transform.position, a, b) <= halfWidth) into.Add(eh);
        }
    }

    /// <summary>Quái gần tướng nhất trong khoảng maxDist.</summary>
    private bool FindNearestEnemy(float maxDist, out Vector2 pos)
    {
        pos = transform.position;
        bool found = false;
        float best = maxDist;
        Vector2 me = transform.position;
        var enemies = EnemyRegistry.ActiveEnemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyHealth eh = enemies[i];
            if (!IsTarget(eh)) continue;
            float d = Vector2.Distance(me, eh.transform.position);
            if (d <= best)
            {
                best = d;
                pos = eh.transform.position;
                found = true;
            }
        }
        return found;
    }

    /// <summary>Tâm cụm quái dày nhất trong tầm nhìn (đếm số quái trong bán kính chiêu quanh từng quái).</summary>
    private bool FindDensestCluster(float searchRange, float radius, out Vector2 center, out int count)
    {
        center = transform.position;
        count = 0;
        Vector2 me = transform.position;
        float bestScore = float.MinValue;
        var enemies = EnemyRegistry.ActiveEnemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyHealth a = enemies[i];
            if (!IsTarget(a)) continue;
            Vector2 pa = a.transform.position;
            float da = Vector2.Distance(me, pa);
            if (da > searchRange) continue;
            int n = 0;
            for (int j = 0; j < enemies.Count; j++)
            {
                EnemyHealth b = enemies[j];
                if (IsTarget(b) && Vector2.Distance(pa, b.transform.position) <= radius) n++;
            }
            float score = n * 100f - da;   // nhiều quái hơn thì tốt hơn; bằng nhau thì ưu tiên quái gần tướng
            if (score > bestScore)
            {
                bestScore = score;
                center = pa;
                count = n;
            }
        }
        return count > 0;
    }

    /// <summary>Chuỗi sét: quái đầu gần điểm ngắm nhất, các quái sau là quái gần nhất so với quái trước.</summary>
    private List<EnemyHealth> BuildChain(Vector2 from, float firstRange, float hopRange, int maxTargets)
    {
        List<EnemyHealth> chain = new List<EnemyHealth>();
        var enemies = EnemyRegistry.ActiveEnemies;
        Vector2 cur = from;
        float range = firstRange;
        for (int k = 0; k < maxTargets; k++)
        {
            EnemyHealth best = null;
            float bestD = range;
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyHealth eh = enemies[i];
                if (!IsTarget(eh) || chain.Contains(eh)) continue;
                float d = Vector2.Distance(cur, eh.transform.position);
                if (d <= bestD)
                {
                    bestD = d;
                    best = eh;
                }
            }
            if (best == null) break;
            chain.Add(best);
            cur = best.transform.position;
            range = hopRange;
        }
        return chain;
    }

    private bool AlliesNeedHeal(float radius, float belowPct)
    {
        var blockers = BlockerRegistry.ActiveBlockers;
        for (int i = 0; i < blockers.Count; i++)
        {
            IBlocker b = blockers[i];
            if (b == null || !b.IsAlive) continue;
            if (Vector2.Distance(transform.position, b.Position) > radius) continue;
            ReinforcementSoldier soldier = b as ReinforcementSoldier;
            if (soldier != null && soldier.maxHealth > 0f && soldier.currentHealth / soldier.maxHealth <= belowPct) return true;
        }
        return false;
    }

    private void FaceTowards(Vector2 p)
    {
        if (spriteRenderer != null && Mathf.Abs(p.x - transform.position.x) > 0.01f)
        {
            spriteRenderer.flipX = p.x < transform.position.x;
        }
    }

    private void HitEnemy(EnemyHealth eh, float dmg, DamageType type)
    {
        if (eh == null) return;
        eh.TakeDamage(new DamageInfo(dmg, type, gameObject));
    }

    private void ApplyStatus(EnemyHealth eh, StatusEffectType type, float magnitude, float duration)
    {
        if (eh == null) return;
        StatusEffectController sec = eh.GetComponent<StatusEffectController>();
        if (sec != null) sec.ApplyEffect(type, magnitude, duration, gameObject);
    }

    // ------------------------------------------------------------------ tự động tung chiêu

    /// <summary>Có nên tự tung chiêu slot (1/2) lúc này không, và ngắm vào đâu (không lãng phí chiêu khi chưa có mục tiêu hợp lệ).</summary>
    private bool WantsAutoCast(int slot, out Vector2 aim)
    {
        Vector2 me = transform.position;
        aim = me;
        float hpPct = maxHealth > 0f ? currentHealth / maxHealth : 1f;
        int count;

        if (heroId == "Paladin")
        {
            if (slot == 1)
            {
                // Khiên Thánh: bật khi đang giáp lá cà và máu đã hao
                bool inFight = engagedEnemies.Count > 0 || CountEnemiesNear(me, attackRange * 1.5f) > 0;
                return inFight && hpPct <= 0.85f && Time.time >= shieldEndTime;
            }
            // Hào Quang: hồi máu + thưởng vàng khi tướng / lính quanh đó đã hao
            return hpPct <= 0.7f || AlliesNeedHeal(RadiusWide, 0.65f);
        }

        if (heroId == "Ranger")
        {
            if (slot == 1) return FindDensestCluster(visionRange, RadiusMedium, out aim, out count);
            return FindNearestEnemy(visionRange, out aim);
        }

        if (heroId == "Mage")
        {
            if (slot == 1) return FindNearestEnemy(visionRange, out aim);
            return FindDensestCluster(visionRange, RadiusLarge, out aim, out count);
        }

        // Knight (mặc định)
        if (slot == 1) return FindNearestEnemy(SlashLength, out aim);
        return CountEnemiesNear(me, RadiusWide) > 0;
    }

    private bool TryAutoCastSlot(int slot)
    {
        float remaining = slot == 1 ? Skill1CooldownRemaining : Skill2CooldownRemaining;
        if (remaining > 0f) return false;

        Vector2 aim;
        if (!WantsAutoCast(slot, out aim)) return false;

        bool cast = slot == 1 ? UseSkill1(aim) : UseSkill2(aim);
        if (cast) lastAttackTime = Time.time;   // chiêu thay cho đòn đánh thường ở lượt này
        return cast;
    }

    /// <summary>Gọi mỗi frame từ Update: chiêu hồi lâu nhất (mạnh nhất) xét trước, rồi chiêu còn lại; sau cùng mới tới đánh thường.</summary>
    private void HandleAutoSkills()
    {
        if (!autoCastSkills || isDead) return;
        if (Time.time < nextAutoSkillCheck) return;
        nextAutoSkillCheck = Time.time + AutoSkillCheckInterval;

        int first = skill2Cooldown >= skill1Cooldown ? 2 : 1;
        int second = first == 2 ? 1 : 2;
        if (TryAutoCastSlot(first)) return;
        TryAutoCastSlot(second);
    }

    /// <summary>Bấm icon chiêu: tung ngay. Tự chọn quái / cụm quái hợp lý; nếu không có quái thì vẫn tung theo hướng mặt để thấy hiệu ứng.</summary>
    public bool CastSkillNow(int slot)
    {
        if (isDead) return false;
        float remaining = slot == 1 ? Skill1CooldownRemaining : Skill2CooldownRemaining;
        if (remaining > 0f) return false;

        Vector2 aim;
        if (!WantsAutoCast(slot, out aim))
        {
            Vector2 facing = (spriteRenderer != null && spriteRenderer.flipX) ? Vector2.left : Vector2.right;
            aim = (Vector2)transform.position + facing * Mathf.Max(attackRange * 1.5f, 1.0f);
        }
        return slot == 1 ? UseSkill1(aim) : UseSkill2(aim);
    }

    // ------------------------------------------------------------------ chiêu 1 / chiêu 2

    public bool UseSkill1(Vector2 targetPos)
    {
        if (isDead) return false;
        if (Time.time - lastSkill1Time < skill1Cooldown) return false;

        lastSkill1Time = Time.time;
        FaceTowards(targetPos);

        if (animator != null)
        {
            animator.SetTrigger("Skill1");
        }

        Vector2 me = transform.position;
        List<EnemyHealth> hits = new List<EnemyHealth>();

        if (heroId == "Ranger")
        {
            // Ranger Skill 1: Mưa Tên Bão Tố - mưa tên trút xuống cả vùng quanh điểm ngắm (60 ST + làm chậm 50% trong 3,5s)
            float r = RadiusMedium;
            HeroSkillFX.CastFlash(me, new Color(1f, 0.85f, 0.4f, 0.9f));
            HeroSkillFX.ArrowRain(targetPos, r, 18);
            CollectEnemiesNear(targetPos, r, hits);
            for (int i = 0; i < hits.Count; i++)
            {
                HitEnemy(hits[i], 60f, DamageType.Physical);
                ApplyStatus(hits[i], StatusEffectType.Slow, 0.5f, 3.5f);
            }
            Debug.Log("[Hero:Ranger] Mưa Tên Bão Tố trút xuống " + hits.Count + " kẻ địch!");
        }
        else if (heroId == "Mage")
        {
            // Mage Skill 1: Chuỗi Sét Tê Liệt - sét giật lan qua tối đa 4 mục tiêu (80 Magic ST + giảm giáp 25% trong 4s)
            List<EnemyHealth> chain = BuildChain(targetPos, RadiusLarge, ChainHop, 4);
            Vector2[] pts = new Vector2[chain.Count + 1];
            pts[0] = me + new Vector2(0f, 0.25f);
            for (int i = 0; i < chain.Count; i++)
            {
                pts[i + 1] = (Vector2)chain[i].transform.position + new Vector2(0f, 0.1f);
            }
            if (chain.Count == 0)
            {
                Vector2 d = targetPos - me;
                if (d.sqrMagnitude < 0.0001f) d = Vector2.right;
                pts = new Vector2[] { pts[0], me + d.normalized * SlashLength };
            }
            HeroSkillFX.CastFlash(me, new Color(0.6f, 0.8f, 1f, 0.9f));
            HeroSkillFX.Lightning(pts, new Color(0.45f, 0.75f, 1f, 0.9f), 0.06f);
            for (int i = 0; i < chain.Count; i++)
            {
                Vector2 p = chain[i].transform.position;
                HitEnemy(chain[i], 80f, DamageType.Magic);
                ApplyStatus(chain[i], StatusEffectType.ArmorBreak, 0.25f, 4.0f);
                HeroSkillFX.Spark(p, new Color(0.7f, 0.9f, 1f, 0.95f), 0.28f, 0.05f * (i + 1));
            }
            Debug.Log("[Hero:Mage] Chuỗi Sét Tê Liệt giật lan qua " + chain.Count + " kẻ địch!");
        }
        else if (heroId == "Paladin")
        {
            // Paladin Skill 1: Khiên Thánh Bất Diệt (giảm 80% ST trong 4s + hồi 40 HP), khiên vàng bám theo tướng
            shieldEndTime = Time.time + 4.0f;
            currentHealth = Mathf.Min(maxHealth, currentHealth + 40f);
            HeroSkillFX.CastFlash(me, new Color(1f, 0.92f, 0.5f, 0.9f));
            HeroSkillFX.ShieldBubble(transform, Mathf.Max(0.8f, attackRange * 0.7f), 4.0f);
            Debug.Log("[Hero:Paladin] Khiên Thánh Bất Diệt bật lên! Kháng 80% sát thương!");
        }
        else
        {
            // Knight Skill 1: Cú Chém Dũng Mãnh - vệt chém thẳng có dư ảnh dài gấp đôi tầm đánh, chém trúng mọi quái nằm trên đường chém (45 ST + choáng 1s)
            Vector2 dir = targetPos - me;
            if (dir.sqrMagnitude < 0.0001f)
            {
                dir = (spriteRenderer != null && spriteRenderer.flipX) ? Vector2.left : Vector2.right;
            }
            dir.Normalize();
            float len = SlashLength;
            Vector2 tip = me + dir * len;
            HeroSkillFX.SlashBeam(me + new Vector2(0f, 0.15f), dir, len, 0.16f, new Color(1f, 0.9f, 0.4f, 1f));
            CollectEnemiesAlongSegment(me, tip, SlashHalfWidth, hits);
            for (int i = 0; i < hits.Count; i++)
            {
                Vector2 p = hits[i].transform.position;
                HitEnemy(hits[i], 45f, DamageType.Physical);
                ApplyStatus(hits[i], StatusEffectType.Stun, 1f, 1.0f);
                HeroSkillFX.Spark(p, new Color(1f, 0.95f, 0.6f, 0.95f), 0.25f, 0.04f);
            }
            Debug.Log("[Hero:Knight] Cú chém dũng mãnh trúng " + hits.Count + " kẻ địch!");
        }

        return true;
    }

    public bool UseSkill2(Vector2 targetPos)
    {
        if (isDead) return false;
        if (Time.time - lastSkill2Time < skill2Cooldown) return false;

        lastSkill2Time = Time.time;
        FaceTowards(targetPos);

        if (animator != null)
        {
            animator.SetTrigger("Skill2");
        }

        Vector2 me = transform.position;
        List<EnemyHealth> hits = new List<EnemyHealth>();

        if (heroId == "Ranger")
        {
            // Ranger Skill 2: Bẫy Gai Rừng (trói chân 3s + 40 True ST + thiêu đốt)
            float r = RadiusSmall;
            HeroSkillFX.CastFlash(me, new Color(0.5f, 0.9f, 0.4f, 0.9f));
            HeroSkillFX.ThornTrap(targetPos, r);
            CollectEnemiesNear(targetPos, r, hits);
            for (int i = 0; i < hits.Count; i++)
            {
                HitEnemy(hits[i], 40f, DamageType.True);
                ApplyStatus(hits[i], StatusEffectType.Stun, 1f, 3.0f);
                ApplyStatus(hits[i], StatusEffectType.Burn, 10f, 4.0f);
            }
            Debug.Log("[Hero:Ranger] Bẫy Gai Rừng trói " + hits.Count + " kẻ địch!");
        }
        else if (heroId == "Mage")
        {
            // Mage Skill 2: Bão Băng Vĩnh Cửu (50 Magic ST + đóng băng 4s toàn bộ quái trong vùng rộng)
            float r = RadiusLarge;
            HeroSkillFX.CastFlash(me, new Color(0.6f, 0.9f, 1f, 0.9f));
            HeroSkillFX.IceStorm(targetPos, r);
            CollectEnemiesNear(targetPos, r, hits);
            for (int i = 0; i < hits.Count; i++)
            {
                HitEnemy(hits[i], 50f, DamageType.Magic);
                ApplyStatus(hits[i], StatusEffectType.Freeze, 1f, 4.0f);
            }
            Debug.Log("[Hero:Mage] Bão Băng Vĩnh Cửu đóng băng " + hits.Count + " kẻ địch!");
        }
        else if (heroId == "Paladin")
        {
            // Paladin Skill 2: Hào Quang Quang Minh (hồi 60 HP cho Hero & Lính trong vùng rộng, thêm 20 Gold)
            float r = RadiusWide;
            currentHealth = Mathf.Min(maxHealth, currentHealth + 60f);

            var blockers = BlockerRegistry.ActiveBlockers;
            for (int i = 0; i < blockers.Count; i++)
            {
                IBlocker b = blockers[i];
                if (b != null && b.IsAlive && Vector2.Distance(transform.position, b.Position) <= r)
                {
                    ReinforcementSoldier soldier = b as ReinforcementSoldier;
                    if (soldier != null)
                    {
                        soldier.currentHealth = Mathf.Min(soldier.maxHealth, soldier.currentHealth + 60f);
                    }
                }
            }

            if (LevelManager.main != null)
            {
                LevelManager.main.IncreaseCurrency(20);
            }
            HeroSkillFX.HolyAura(me, r);
            Debug.Log("[Hero:Paladin] Hào Quang Quang Minh tỏa sáng! Hồi máu đồng minh & nhận 20 Gold!");
        }
        else
        {
            // Knight Skill 2: Tiếng Gầm Chiến Trận - sóng xung kích phủ cả vùng tầm nhìn (75 Magic ST + giảm 30% giáp trong 6s)
            float r = RadiusWide;
            HeroSkillFX.Shockwave(me, r, new Color(1f, 0.55f, 0.25f, 1f));
            CollectEnemiesNear(me, r, hits);
            for (int i = 0; i < hits.Count; i++)
            {
                HitEnemy(hits[i], 75f, DamageType.Magic);
                ApplyStatus(hits[i], StatusEffectType.ArmorBreak, 0.3f, 6.0f);
            }
            Debug.Log("[Hero:Knight] Tiếng gầm chiến trận làm rung chuyển " + hits.Count + " kẻ địch!");
        }

        return true;
    }
}
