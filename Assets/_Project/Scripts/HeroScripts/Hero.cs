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

    public bool UseSkill1(Vector2 targetPos)
    {
        if (isDead) return false;
        if (Time.time - lastSkill1Time < skill1Cooldown) return false;

        lastSkill1Time = Time.time;

        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = targetPos.x < transform.position.x;
        }

        if (animator != null)
        {
            animator.SetTrigger("Skill1");
        }

        var enemies = EnemyRegistry.ActiveEnemies;

        if (heroId == "Ranger")
        {
            // Ranger Skill 1: Mưa Tên Bão Tố (AoE 60 ST + 50% Slow 3.5s)
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyHealth eh = enemies[i];
                if (eh == null || !eh.gameObject.activeInHierarchy || eh.CurrentHealth <= 0) continue;

                if (Vector2.Distance(targetPos, eh.transform.position) <= 3.0f)
                {
                    DamageInfo dmg = new DamageInfo(60f, DamageType.Physical, gameObject);
                    eh.TakeDamage(dmg);

                    StatusEffectController sec = eh.GetComponent<StatusEffectController>();
                    if (sec != null) sec.ApplyEffect(StatusEffectType.Slow, 0.5f, 3.5f, gameObject);
                }
            }
            Debug.Log("[Hero:Ranger] Mưa Tên Bão Tố xối xả!");
        }
        else if (heroId == "Mage")
        {
            // Mage Skill 1: Chuỗi Sét Tê Liệt (Sét lan 4 mục tiêu 80 Magic ST)
            int hitCount = 0;
            for (int i = 0; i < enemies.Count && hitCount < 4; i++)
            {
                EnemyHealth eh = enemies[i];
                if (eh == null || !eh.gameObject.activeInHierarchy || eh.CurrentHealth <= 0) continue;

                if (Vector2.Distance(targetPos, eh.transform.position) <= 4.0f)
                {
                    DamageInfo dmg = new DamageInfo(80f, DamageType.Magic, gameObject);
                    eh.TakeDamage(dmg);

                    StatusEffectController sec = eh.GetComponent<StatusEffectController>();
                    if (sec != null) sec.ApplyEffect(StatusEffectType.ArmorBreak, 0.25f, 4.0f, gameObject);
                    hitCount++;
                }
            }
            Debug.Log($"[Hero:Mage] Chuỗi Sét Tê Liệt giật lan qua {hitCount} kẻ địch!");
        }
        else if (heroId == "Paladin")
        {
            // Paladin Skill 1: Khiên Thánh Bất Diệt (Giảm 80% ST trong 4s + Hồi 40 HP)
            shieldEndTime = Time.time + 4.0f;
            currentHealth = Mathf.Min(maxHealth, currentHealth + 40f);
            Debug.Log("[Hero:Paladin] Khiên Thánh Bất Diệt bật lên! Kháng 80% sát thương!");
        }
        else
        {
            // Knight Skill 1: Cú Chém Dũng Mãnh (45 ST + Choáng 1s)
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyHealth eh = enemies[i];
                if (eh == null || !eh.gameObject.activeInHierarchy || eh.CurrentHealth <= 0) continue;

                if (Vector2.Distance(targetPos, eh.transform.position) <= 2.2f)
                {
                    DamageInfo dmg = new DamageInfo(45f, DamageType.Physical, gameObject);
                    eh.TakeDamage(dmg);

                    StatusEffectController sec = eh.GetComponent<StatusEffectController>();
                    if (sec != null) sec.ApplyEffect(StatusEffectType.Stun, 1f, 1.0f, gameObject);
                }
            }
            Debug.Log("[Hero:Knight] Cú chém dũng mãnh!");
        }

        return true;
    }

    public bool UseSkill2(Vector2 targetPos)
    {
        if (isDead) return false;
        if (Time.time - lastSkill2Time < skill2Cooldown) return false;

        lastSkill2Time = Time.time;

        if (animator != null)
        {
            animator.SetTrigger("Skill2");
        }

        var enemies = EnemyRegistry.ActiveEnemies;

        if (heroId == "Ranger")
        {
            // Ranger Skill 2: Bẫy Gai Rừng (Trói chân 3s + 40 True Dmg + Thiêu đốt)
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyHealth eh = enemies[i];
                if (eh == null || !eh.gameObject.activeInHierarchy || eh.CurrentHealth <= 0) continue;

                if (Vector2.Distance(targetPos, eh.transform.position) <= 2.8f)
                {
                    DamageInfo dmg = new DamageInfo(40f, DamageType.True, gameObject);
                    eh.TakeDamage(dmg);

                    StatusEffectController sec = eh.GetComponent<StatusEffectController>();
                    if (sec != null)
                    {
                        sec.ApplyEffect(StatusEffectType.Stun, 1f, 3.0f, gameObject);
                        sec.ApplyEffect(StatusEffectType.Burn, 10f, 4.0f, gameObject);
                    }
                }
            }
            Debug.Log("[Hero:Ranger] Bẫy Gai Rừng đã được kích hoạt!");
        }
        else if (heroId == "Mage")
        {
            // Mage Skill 2: Bão Băng Vĩnh Cửu (Đóng băng toàn bộ quái trong vùng 4.0s)
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyHealth eh = enemies[i];
                if (eh == null || !eh.gameObject.activeInHierarchy || eh.CurrentHealth <= 0) continue;

                if (Vector2.Distance(targetPos, eh.transform.position) <= 4.0f)
                {
                    DamageInfo dmg = new DamageInfo(50f, DamageType.Magic, gameObject);
                    eh.TakeDamage(dmg);

                    StatusEffectController sec = eh.GetComponent<StatusEffectController>();
                    if (sec != null) sec.ApplyEffect(StatusEffectType.Freeze, 1f, 4.0f, gameObject);
                }
            }
            Debug.Log("[Hero:Mage] Bão Băng Vĩnh Cửu đóng băng diện rộng!");
        }
        else if (heroId == "Paladin")
        {
            // Paladin Skill 2: Hào Quang Quang Minh (Hồi 60 HP cho Hero & Lính, Thêm 20 Gold)
            currentHealth = Mathf.Min(maxHealth, currentHealth + 60f);

            var blockers = BlockerRegistry.ActiveBlockers;
            for (int i = 0; i < blockers.Count; i++)
            {
                IBlocker b = blockers[i];
                if (b != null && b.IsAlive && Vector2.Distance(transform.position, b.Position) <= 5.0f)
                {
                    if (b is ReinforcementSoldier soldier)
                    {
                        soldier.currentHealth = Mathf.Min(soldier.maxHealth, soldier.currentHealth + 60f);
                    }
                }
            }

            if (LevelManager.main != null)
            {
                LevelManager.main.IncreaseCurrency(20);
            }
            Debug.Log("[Hero:Paladin] Hào Quang Quang Minh tỏa sáng! Hồi máu đồng minh & nhận 20 Gold!");
        }
        else
        {
            // Knight Skill 2: Tiếng Gầm Chiến Trận (75 ST Phép + Giảm 30% giáp 6s)
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyHealth eh = enemies[i];
                if (eh == null || !eh.gameObject.activeInHierarchy || eh.CurrentHealth <= 0) continue;

                if (Vector2.Distance(targetPos, eh.transform.position) <= 4.0f)
                {
                    DamageInfo dmg = new DamageInfo(75f, DamageType.Magic, gameObject);
                    eh.TakeDamage(dmg);

                    StatusEffectController sec = eh.GetComponent<StatusEffectController>();
                    if (sec != null) sec.ApplyEffect(StatusEffectType.ArmorBreak, 0.3f, 6.0f, gameObject);
                }
            }
            Debug.Log("[Hero:Knight] Tiếng gầm chiến trận!");
        }

        return true;
    }
}
