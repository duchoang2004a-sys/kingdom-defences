using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum BulletType
{
    Arrow,        // Physical single-target, high velocity (Archer Tower)
    Cannonball,   // Explosive AoE splash damage (Cannon Tower)
    MageBolt      // Magic chain lightning + slow / freeze (Mage Tower)
}

public class Bullet : MonoBehaviour
{
    [Header("Bullet Type & References")]
    public BulletType bulletType = BulletType.Arrow;
    [SerializeField] private Rigidbody2D rb;

    [Header("Attributes")]
    public float bulletSpeed = 12f;
    public float bulletDamage = 10f;
    public float armorPenetration = 0f;

    [Header("AoE Splash (Cannon Tower)")]
    public float aoeRadius = 2.0f;

    [Header("Chain Lightning (Mage Tower)")]
    public int maxChainTargets = 3;
    public float chainRadius = 2.0f;
    public float slowDuration = 0f;
    public float slowFactor = 0.5f;

    [Header("Visual & Impact Effects")]
    public GameObject impactVfxPrefab;
    public GameObject lightningVfxPrefab;

    [Header("Targeting & Retargeting")]
    public LayerMask enemyMask;
    public float retargetSearchRadius = 7.0f;

    private Transform target;
    private bool hasHit = false;

    private void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
    }

    private void OnEnable()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
        target = null;
        hasHit = false;
        armorPenetration = 0f;
    }

    public void SetTarget(Transform _target)
    {
        target = _target;
    }

    public void SetTarget(Transform _target, float _damage)
    {
        target = _target;
        bulletDamage = _damage;
    }

    public void SetTarget(Transform _target, float _damage, float _scale, float _aoeRadius)
    {
        SetTarget(_target, _damage, _scale, _aoeRadius, default, 0f, 0f);
    }

    public void SetTarget(Transform _target, float _damage, float _scale, float _aoeRadius, LayerMask _mask, float _slowDuration, float _armorPen)
    {
        target = _target;
        bulletDamage = _damage;
        if (_scale > 0) transform.localScale = Vector3.one * _scale;
        if (_aoeRadius > 0) aoeRadius = _aoeRadius;
        if (_mask != default) enemyMask = _mask;
        slowDuration = _slowDuration;
        armorPenetration = _armorPen;
    }

    private void FixedUpdate()
    {
        if (hasHit) return;

        // If target was killed mid-air, retarget to the furthest enemy on track
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            target = FindNextPriorityEnemy();
            if (target == null)
            {
                ReturnBullet();
                return;
            }
        }

        if (target != null)
        {
            Vector2 direction = ((Vector2)target.position - (Vector2)transform.position).normalized;
            if (rb != null)
            {
                rb.linearVelocity = direction * bulletSpeed;
            }
            else
            {
                transform.position = Vector2.MoveTowards(transform.position, target.position, bulletSpeed * Time.fixedDeltaTime);
            }

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);

            if (Vector2.Distance(transform.position, target.position) < 0.25f)
            {
                HitTarget(target.gameObject);
            }
        }
    }

    private Transform FindNextPriorityEnemy()
    {
        var enemies = EnemyRegistry.ActiveEnemies;
        if (enemies.Count == 0) return null;

        Transform bestTarget = null;
        float highestDistanceTraveled = -1f;
        Vector2 myPos = transform.position;

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyHealth eh = enemies[i];
            if (eh == null || !eh.gameObject.activeInHierarchy || eh.CurrentHealth <= 0) continue;

            float dist = Vector2.Distance(myPos, eh.transform.position);
            if (dist <= retargetSearchRadius)
            {
                EnemyMovement em = eh.GetComponent<EnemyMovement>();
                float traveled = em != null ? em.DistanceTraveled : (1000f - dist);
                if (traveled > highestDistanceTraveled)
                {
                    highestDistanceTraveled = traveled;
                    bestTarget = eh.transform;
                }
            }
        }

        return bestTarget;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasHit) return;
        HitTarget(other.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D other)
    {
        if (hasHit) return;
        HitTarget(other.gameObject);
    }

    private void HitTarget(GameObject obj)
    {
        EnemyHealth primaryHealth = obj.GetComponent<EnemyHealth>() ?? obj.GetComponentInParent<EnemyHealth>();
        if (primaryHealth == null && enemyMask != default)
        {
            if ((enemyMask.value & (1 << obj.layer)) == 0) return;
        }

        hasHit = true;

        switch (bulletType)
        {
            case BulletType.Arrow:
                HandleArrowHit(primaryHealth);
                break;

            case BulletType.Cannonball:
                HandleCannonHit(primaryHealth);
                break;

            case BulletType.MageBolt:
                HandleMageHit(primaryHealth);
                break;
        }

        ReturnBullet();
    }

    private void ApplyPoisonIfActive(EnemyHealth targetHealth)
    {
        if (targetHealth == null || !ConsumableOilManager.IsPoisonActive) return;
        StatusEffectController sec = targetHealth.GetComponent<StatusEffectController>();
        if (sec == null) sec = targetHealth.gameObject.AddComponent<StatusEffectController>();
        sec.ApplyEffect(StatusEffectType.Poison, 12f, 4.0f, gameObject);
    }

    // 1. Tháp Cung: Sát thương vật lý (Physical)
    private void HandleArrowHit(EnemyHealth primary)
    {
        if (primary != null)
        {
            DamageInfo dmgInfo = new DamageInfo(bulletDamage, DamageType.Physical, gameObject, false, armorPenetration);
            primary.TakeDamage(dmgInfo);
            ApplyPoisonIfActive(primary);
        }
        SpawnImpactVFX(transform.position);
    }

    // 2. Tháp Pháo: Sát thương nổ (Explosive - giảm 50% hiệu lực của giáp)
    private void HandleCannonHit(EnemyHealth primary)
    {
        SpawnImpactVFX(transform.position);

        var enemies = EnemyRegistry.ActiveEnemies;
        Vector2 impactPos = transform.position;

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyHealth eh = enemies[i];
            if (eh == null || !eh.gameObject.activeInHierarchy || eh.CurrentHealth <= 0) continue;

            float dist = Vector2.Distance(impactPos, eh.transform.position);
            if (dist <= aoeRadius)
            {
                float falloff = Mathf.Clamp01(1f - (dist / aoeRadius) * 0.4f);
                DamageInfo dmg = new DamageInfo(bulletDamage * falloff, DamageType.Explosive, gameObject, false, armorPenetration);
                eh.TakeDamage(dmg);
                ApplyPoisonIfActive(eh);
            }
        }
    }

    // 3. Tháp Pháp Sư: Sát thương phép (Magic) + Giật nan + Slow/Freeze
    private void HandleMageHit(EnemyHealth primary)
    {
        List<Vector3> chainPositions = new List<Vector3> { transform.position };

        if (primary != null)
        {
            DamageInfo dmg = new DamageInfo(bulletDamage, DamageType.Magic, gameObject, false, armorPenetration);
            primary.TakeDamage(dmg);
            chainPositions.Add(primary.transform.position);

            ApplyMageStatusEffect(primary.gameObject);
        }

        // Tìm quái lân cận trong bán kính giật sét
        var enemies = EnemyRegistry.ActiveEnemies;
        Vector2 hitCenter = transform.position;
        int hitCount = (primary != null) ? 1 : 0;

        for (int i = 0; i < enemies.Count; i++)
        {
            if (hitCount >= maxChainTargets) break;
            EnemyHealth extraEh = enemies[i];
            if (extraEh == null || extraEh == primary || !extraEh.gameObject.activeInHierarchy || extraEh.CurrentHealth <= 0) continue;

            float dist = Vector2.Distance(hitCenter, extraEh.transform.position);
            if (dist <= chainRadius)
            {
                DamageInfo chainDmg = new DamageInfo(bulletDamage * 0.75f, DamageType.Magic, gameObject, false, armorPenetration);
                extraEh.TakeDamage(chainDmg);
                chainPositions.Add(extraEh.transform.position);

                ApplyMageStatusEffect(extraEh.gameObject);
                hitCount++;
            }
        }

        SpawnLightningChainVFX(chainPositions);
    }

    private void ApplyMageStatusEffect(GameObject targetObj)
    {
        if (slowDuration <= 0) return;

        StatusEffectController sec = targetObj.GetComponent<StatusEffectController>();
        if (sec == null) sec = targetObj.AddComponent<StatusEffectController>();

        // Cấp 3 Mage có thời gian làm chậm >= 2.0s -> đóng băng 1s + làm chậm phần còn lại
        if (slowDuration >= 2.0f)
        {
            sec.ApplyEffect(StatusEffectType.Freeze, 1f, 1.0f, gameObject);
            sec.ApplyEffect(StatusEffectType.Slow, 0.6f, slowDuration, gameObject);
        }
        else
        {
            sec.ApplyEffect(StatusEffectType.Slow, slowFactor, slowDuration, gameObject);
        }
    }

    private void SpawnImpactVFX(Vector3 pos)
    {
        if (impactVfxPrefab != null)
        {
            if (ObjectPool.instance != null)
                ObjectPool.instance.Spawn(impactVfxPrefab, pos, Quaternion.identity);
            else
                Instantiate(impactVfxPrefab, pos, Quaternion.identity);
        }
    }

    private void SpawnLightningChainVFX(List<Vector3> points)
    {
        if (points.Count < 2) return;

        GameObject vfxObj = null;
        if (lightningVfxPrefab != null)
        {
            if (ObjectPool.instance != null)
                vfxObj = ObjectPool.instance.Spawn(lightningVfxPrefab, points[0], Quaternion.identity);
            else
                vfxObj = Instantiate(lightningVfxPrefab, points[0], Quaternion.identity);
        }

        if (vfxObj != null)
        {
            LightningChainVFX chain = vfxObj.GetComponent<LightningChainVFX>();
            if (chain != null) chain.ShowChain(points);
        }
    }

    private void ReturnBullet()
    {
        if (ObjectPool.instance != null)
        {
            ObjectPool.instance.ReturnToPool(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
