using UnityEngine;

public class Enemy : MonoBehaviour
{
    [Header("Customizable Enemy Settings (Inspector)")]
    [Tooltip("Tên loại quái vật hiển thị trong game")]
    public string enemyName = "Goblin";

    [Tooltip("Lượng vàng thưởng cho người chơi khi quái bị tiêu diệt")]
    public int goldReward = 15;

    [Tooltip("Lượng máu tối đa của quái")]
    public float maxHealth = 30f;

    [Tooltip("Tốc độ di chuyển của quái")]
    public float moveSpeed = 2f;

    [Header("Defenses & Resistances")]
    [Tooltip("Giáp vật lý (0.0 đến 0.8: giảm sát thương vật lý và nổ)")]
    [Range(0f, 0.8f)]
    public float armor = 0f;

    [Tooltip("Kháng phép (0.0 đến 0.8: giảm sát thương phép thuật)")]
    [Range(0f, 0.8f)]
    public float magicResist = 0f;

    [Tooltip("Kháng hiệu ứng khống chế (Giảm thời gian Slow/Freeze/Stun cho Boss)")]
    [Range(0f, 0.9f)]
    public float statusResist = 0f;

    [Header("Melee Combat vs Hero / Soldiers")]
    [Tooltip("Sát thương khi quái đấu tay đôi với Hero hoặc Lính")]
    public float combatDamage = 8f;
    [Tooltip("Khoảng cách giữa các đòn đánh khi đấu tay đôi")]
    public float combatAttackInterval = 1.2f;

    public float GetCombatDamage()
    {
        if (enemyData != null && enemyData.Damage > 1)
        {
            return enemyData.Damage * 6f;
        }
        return combatDamage;
    }

    [Header("Optional ScriptableObject Data")]
    [SerializeField] private EnemyData enemyData;
    public EnemyData Data => enemyData;

    [Header("Boss & Split Settings")]
    public bool isBoss = false;
    public int bossTier = 1;
    public GameObject minionPrefab;
    public int minionCount = 2;
    [HideInInspector] public int depthLevel = 0; // 0 = original boss, 1 = minion (won't split again)

    [Header("Targeting & Decoy Mechanics (Part A4)")]
    public bool isUntargetable = false;
    public float tauntPriority = 0f;
    public float shield = 0f;

    public void SetUntargetable(bool untargetable)
    {
        isUntargetable = untargetable;
        EnemyHealth eh = GetComponent<EnemyHealth>();
        if (eh != null)
        {
            if (untargetable)
            {
                EnemyRegistry.Unregister(eh);
            }
            else
            {
                EnemyRegistry.Register(eh);
            }
        }
    }

    public bool IsBoss => isBoss || (enemyData != null && enemyData.Damage >= 2) || (!string.IsNullOrEmpty(enemyName) && (enemyName.Contains("Boss") || enemyName.Contains("Dragon") || enemyName.Contains("Golem") || enemyName.Contains("Troll")));

    public string EnemyName => enemyData != null && !string.IsNullOrEmpty(enemyData.EnemyName) ? enemyData.EnemyName : (string.IsNullOrEmpty(enemyName) ? gameObject.name : enemyName);
    public int GoldReward => enemyData != null && enemyData.CurrencyReward > 0 ? enemyData.CurrencyReward : goldReward;
    public int CurrencyReward => GoldReward;
    public float MaxHealth => enemyData != null && enemyData.MaxHealth > 0 ? enemyData.MaxHealth : maxHealth;
    public float MoveSpeed => enemyData != null && enemyData.Speed > 0 ? enemyData.Speed : moveSpeed;
    public float Armor => enemyData != null ? enemyData.Armor : armor;
    public float MagicResist => enemyData != null ? enemyData.MagicResist : magicResist;
    public float StatusResist => enemyData != null ? enemyData.StatusResist : statusResist;

    private Animator animator;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (enemyData != null)
        {
            if (string.IsNullOrEmpty(enemyName)) enemyName = enemyData.EnemyName;
            if (goldReward <= 0) goldReward = enemyData.CurrencyReward;
            if (maxHealth <= 0) maxHealth = enemyData.MaxHealth;
            if (moveSpeed <= 0) moveSpeed = enemyData.Speed;
        }

        SyncStatusController();
        EnsureEnemyVisuals();
    }

    private void Start()
    {
        SyncStatusController();
        EnsureEnemyVisuals();
    }

    private void OnEnable()
    {
        EnsureEnemyVisuals();
    }

    public void EnsureEnemyVisuals()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>() ?? GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            sr.sortingOrder = 10;
            sr.color = Color.white;

            if (sr.sprite == null)
            {
                string eType = EnemyName.ToLower().Replace("boss", "").Replace("enemy", "").Trim();
                if (string.IsNullOrEmpty(eType)) eType = "goblin";

                Sprite sp = Resources.Load<Sprite>($"Enemies/enemy_{eType}");
#if UNITY_EDITOR
                if (sp == null)
                {
                    sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/_Project/Sprites/Enemies/enemy_{eType}.png")
                        ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/_Project/Sprites/Enemies/enemy_{eType}_walk.png");
                }
#endif
                if (sp != null)
                {
                    sr.sprite = sp;
                }
            }
        }
    }

    public void SyncStatusController()
    {
        StatusEffectController sec = GetComponent<StatusEffectController>();
        if (sec != null)
        {
            sec.statusResist = StatusResist;
        }

        // Nếu không có ScriptableObject EnemyData, thử lấy chỉ số từ Bảng Cân Bằng theo tên
        if (enemyData == null)
        {
            var stats = GameDataBalancer.GetEnemyStats(EnemyName);
            if (armor <= 0) armor = stats.armor;
            if (magicResist <= 0) magicResist = stats.magicResist;
            if (statusResist <= 0) statusResist = stats.statusResist;

            if (sec != null) sec.statusResist = statusResist;
        }
    }

    public void PlayAttackAnimation()
    {
        if (animator != null)
        {
            animator.SetTrigger("Attack");
        }
    }
    /// <summary>
    /// Kiểm tra quái đã chết hay chưa
    /// </summary>
    public bool IsDead()
    {
        EnemyHealth eh = GetComponent<EnemyHealth>();
        return (eh != null) ? eh.CurrentHealth <= 0 : !gameObject.activeInHierarchy;
    }
}
