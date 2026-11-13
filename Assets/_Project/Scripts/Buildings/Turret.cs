using UnityEngine;
using System.Collections.Generic;

public class Turret : MonoBehaviour, IBaseTower
{
    [Header("Thông tin tháp")]
    public string towerName = "Tháp Phòng Thủ";
    public int buildCost = 100;

    public TowerData towerData;
    public TowerUpgrade[] upgrades;
    public int currentLevel = 0;
    public Transform shootPoint;
    public GameObject bulletPrefab;

    [Header("Thuộc tính bổ trợ & Tương thích")]
    public float damage = 10f;
    public float targetingRange = 3.5f;
    public float BulletsPerSecond = 1f;
    public GameObject BulletPrefab;
    public Transform FiringPoint;
    public Transform turretRotationPoint;

    [Header("Trạng thái khống chế (Stun)")]
    public float stunTimer = 0f;
    public bool isStunned => stunTimer > 0f;

    [HideInInspector] public Plot plot;
    [HideInInspector] public float creationTime;

    private Enemy target;

    /// <summary>Transform của quái đang bị nhắm (cho TowerAimer xoay đầu tháp).</summary>
    public Transform CurrentTargetTransform { get { return target != null ? target.transform : null; } }
    private float fireCountdown = 0f;

    private void Awake()
    {
        creationTime = Time.time;
        if (shootPoint == null && FiringPoint != null) shootPoint = FiringPoint;
        if (bulletPrefab == null && BulletPrefab != null) bulletPrefab = BulletPrefab;

        TowerUpgrade[] list = GetUpgradeList();
        if (list != null && list.Length > 0)
        {
            buildCost = list[0].cost;
        }
        if (towerData != null && !string.IsNullOrEmpty(towerData.towerName))
        {
            towerName = towerData.towerName;
        }
    }

    private void Start()
    {
        ApplyUpgrade(currentLevel);
    }

    private void Update()
    {
        // Nếu đang bị làm choáng (bởi ShamanBoss hoặc skill quái), ngừng tìm mục tiêu và bắn
        if (stunTimer > 0f)
        {
            stunTimer -= Time.deltaTime;
            return;
        }

        SearchTarget();
        if (target == null) return;

        // Xoay đầu tháp về phía mục tiêu nếu có rotation point
        if (turretRotationPoint != null)
        {
            Vector3 dir = target.transform.position - turretRotationPoint.position;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            turretRotationPoint.rotation = Quaternion.Euler(0, 0, angle);
        }

        if (fireCountdown <= 0f)
        {
            Shoot();
            float rate = GetCurrentUpgrade() != null && GetCurrentUpgrade().fireRate > 0 ? GetCurrentUpgrade().fireRate : BulletsPerSecond;
            fireCountdown = 1f / Mathf.Max(0.1f, rate);
        }

        fireCountdown -= Time.deltaTime;
    }

    /// <summary>
    /// Làm choáng tháp trong một khoảng thời gian (gọi bởi ShamanBoss)
    /// </summary>
    public void Stun(float duration)
    {
        if (duration > stunTimer)
        {
            stunTimer = duration;
        }
    }

    private void SearchTarget()
    {
        float range = GetRange();
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, range);
        float shortestDistance = Mathf.Infinity;
        Enemy nearestEnemy = null;

        foreach (var hit in hitColliders)
        {
            Enemy e = hit.GetComponent<Enemy>();
            EnemyHealth eh = hit.GetComponent<EnemyHealth>() ?? hit.GetComponentInParent<EnemyHealth>();
            bool isDead = (eh != null && eh.CurrentHealth <= 0) || !hit.gameObject.activeInHierarchy;

            if (e != null && !isDead)
            {
                float distanceToEnemy = Vector2.Distance(transform.position, hit.transform.position);
                if (distanceToEnemy < shortestDistance)
                {
                    shortestDistance = distanceToEnemy;
                    nearestEnemy = e;
                }
            }
        }

        target = nearestEnemy;
    }

    private void Shoot()
    {
        TowerAnimator towerAnim = GetComponent<TowerAnimator>();
        if (towerAnim != null) towerAnim.PlayShoot();

        GameObject prefab = bulletPrefab != null ? bulletPrefab : BulletPrefab;
        Transform point = shootPoint != null ? shootPoint : (FiringPoint != null ? FiringPoint : transform);

        if (prefab != null && target != null)
        {
            GameObject bObj = Instantiate(prefab, point.position, Quaternion.identity);
            Bullet bullet = bObj.GetComponent<Bullet>();
            if (bullet != null)
            {
                TowerUpgrade up = GetCurrentUpgrade();
                float dmg = (up != null && up.damage > 0) ? up.damage : damage;
                float scale = (up != null && up.bulletScale > 0) ? up.bulletScale : 1f;
                float aoe = (up != null) ? up.aoeRadius : 0f;
                float slow = (up != null) ? up.slowDuration : 0f;

                bullet.SetTarget(target.transform, dmg, scale, aoe, default, slow, 0f);
            }

            // Mechanical recoil animation
            StartCoroutine(RecoilRoutine());
        }
    }

    private System.Collections.IEnumerator RecoilRoutine()
    {
        Transform recoilTarget = turretRotationPoint != null ? turretRotationPoint : transform;
        Vector3 startLocalPos = recoilTarget.localPosition;
        Vector3 recoilDir = Vector3.down * 0.12f;
        if (target != null)
        {
            Vector3 fireDir = (target.transform.position - transform.position).normalized;
            recoilDir = -fireDir * 0.12f;
        }

        float dur = 0.08f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            recoilTarget.localPosition = startLocalPos + Vector3.Lerp(recoilDir, Vector3.zero, t);
            yield return null;
        }
        recoilTarget.localPosition = startLocalPos;
    }

    public void ApplyUpgrade(int level)
    {
        currentLevel = level;
        TowerUpgrade up = GetCurrentUpgrade();
        if (up != null)
        {
            damage = up.damage;
            targetingRange = up.range;
            BulletsPerSecond = up.fireRate;
        }

        SpriteRenderer sr = GetComponent<SpriteRenderer>() ?? GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            string tType = "archer";
            if (towerName.Contains("Mage") || towerName.Contains("Pháp Sư")) tType = "mage";
            else if (towerName.Contains("Cannon") || towerName.Contains("Pháo")) tType = "cannon";
            else if (towerName.Contains("Ice") || towerName.Contains("Băng")) tType = "ice";
            else if (towerName.Contains("Barracks") || towerName.Contains("Lính")) tType = "barracks";

            int lvlNum = currentLevel + 1;
            Sprite newSpr = Resources.Load<Sprite>($"Towers/tower_{tType}_L{lvlNum}");
#if UNITY_EDITOR
            if (newSpr == null)
            {
                newSpr = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/_Project/Sprites/Towers/tower_{tType}_L{lvlNum}.png");
            }
#endif
            if (newSpr != null)
            {
                sr.sprite = newSpr;
            }
            transform.localScale = Vector3.one * (1.0f + currentLevel * 0.12f);
        }

        TowerBuildEffect buildFx = GetComponent<TowerBuildEffect>();
        if (buildFx != null) buildFx.OnUpgraded(currentLevel);
    }

    private TowerUpgrade[] GetUpgradeList()
    {
        if (upgrades != null && upgrades.Length > 0) return upgrades;
        if (towerData != null && towerData.upgrades != null && towerData.upgrades.Length > 0) return towerData.upgrades;
        return null;
    }

    public int GetSellRefund() => (int)(GetCurrentUpgrade() != null ? GetCurrentUpgrade().cost * 0.7f : buildCost * 0.7f);

    public int GetMaxLevel()
    {
        var list = GetUpgradeList();
        return list != null ? list.Length : 0;
    }

    public TowerUpgrade GetCurrentUpgrade()
    {
        var list = GetUpgradeList();
        if (list != null && currentLevel >= 0 && currentLevel < list.Length)
            return list[currentLevel];
        return new TowerUpgrade { cost = buildCost, damage = damage, range = targetingRange, fireRate = BulletsPerSecond };
    }

    public TowerUpgrade GetNextUpgrade()
    {
        var list = GetUpgradeList();
        return (list != null && currentLevel + 1 < list.Length) ? list[currentLevel + 1] : null;
    }

    public void SetPlot(Plot plot) => this.plot = plot;
    public float GetRange() => GetCurrentUpgrade() != null ? GetCurrentUpgrade().range : targetingRange;
    public int GetCurrentLevel() => currentLevel;
    public float GetCreationTime() => creationTime;
}
