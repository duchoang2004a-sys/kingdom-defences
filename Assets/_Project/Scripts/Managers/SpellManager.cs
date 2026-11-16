using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class SpellManager : MonoBehaviour
{
    public static SpellManager main;

    [Header("Meteor Strike")]
    public int meteorUses = 2;
    public float meteorDamage = 120f;
    public float meteorDelay = 1f;
    public float meteorRadius = 5f;

    [Header("Freeze")]
    public int freezeUses = 1;
    public float freezeDuration = 5f;
    public static float GlobalFreezeRemaining = 0f;

    [Header("Reinforcements")]
    public GameObject reinforcementPrefab;
    public float reinforcementCooldown = 60f;
    private float lastReinforcementTime;

    public float ReinforcementCooldownRemaining => Mathf.Max(0f, reinforcementCooldown - (Time.time - lastReinforcementTime));
    public bool CanCastReinforcements => Time.time - lastReinforcementTime >= reinforcementCooldown;
    public bool IsTargeting => currentState != SpellState.None;

    // v3 (SESSION_20261115_E): bán kính ảnh hưởng nhân GameScale.RangeFactor như mọi tầm/vùng nổ khác (số gốc 5 áp cho map 12.8x7.2).
    // Vòng hiện theo chuột và lúc thả dùng CÙNG một bán kính này nên vòng nhìn thấy = vùng sát thương thật.
    public float MeteorRadius => meteorRadius * GameScale.RangeFactor;
    public float ReinforcementRadius => 0.8f;   // 2 hiệp sĩ xuất hiện ở ±0.4 quanh điểm click

    private GameObject aimRing;
    private LineRenderer aimLine;
    private RangeIndicator aimIndicator;
    private float aimRadius = -1f;
    private SpellState aimState = SpellState.None;

    public enum SpellState { None, CastingMeteor, CastingReinforcements }
    public SpellState currentState = SpellState.None;

    private void Awake()
    {
        main = this;
        lastReinforcementTime = -reinforcementCooldown;
        GlobalFreezeRemaining = 0f;
    }

    private void Start()
    {
        if (Object.FindAnyObjectByType<SpellHUD>() == null)
        {
            gameObject.AddComponent<SpellHUD>();
        }
    }

    private void Update()
    {
        if (GlobalFreezeRemaining > 0)
        {
            GlobalFreezeRemaining -= Time.deltaTime;
        }

        UpdateAimRing();

        if (GameInput.GetMouseButtonDown(0))
        {
            if (!InputRouter.CanProcessGameplayClick()) return;

            Vector2 mousePos = Camera.main != null ? (Vector2)Camera.main.ScreenToWorldPoint(GameInput.MousePosition) : Vector2.zero;

            if (currentState == SpellState.CastingMeteor)
            {
                if (meteorUses > 0)
                {
                    StartCoroutine(CastMeteor(mousePos));
                    meteorUses--;
                    InputRouter.Consume("Spell_Meteor");
                }
                currentState = SpellState.None;
            }
            else if (currentState == SpellState.CastingReinforcements)
            {
                if (Time.time - lastReinforcementTime >= reinforcementCooldown)
                {
                    CastReinforcements(mousePos);
                    lastReinforcementTime = Time.time;
                    InputRouter.Consume("Spell_Reinforcements");
                }
                currentState = SpellState.None;
            }
        }

        if (GameInput.GetMouseButtonDown(1))
        {
            currentState = SpellState.None;
        }
    }

    // ------------------------------------------------------------------ vòng ảnh hưởng khi chọn vị trí (v3)

    /// <summary>Đang chọn vị trí (Thiên thạch / Tiếp viện): vòng tròn bán kính ảnh hưởng đi theo chuột trên bản đồ.</summary>
    private void UpdateAimRing()
    {
        if (currentState == SpellState.None)
        {
            if (aimRing != null && aimRing.activeSelf) aimRing.SetActive(false);
            aimState = SpellState.None;
            return;
        }
        if (Camera.main == null) return;

        float r = currentState == SpellState.CastingMeteor ? MeteorRadius : ReinforcementRadius;
        if (aimRing == null)
        {
            aimRing = MakeRing(Vector2.zero, r, new Color(1f, 0.6f, 0.2f, 0.95f), 0f);
            aimLine = aimRing.GetComponent<LineRenderer>();
            aimIndicator = aimRing.GetComponent<RangeIndicator>();
            aimRadius = r;
            aimState = SpellState.None;
        }

        if (aimState != currentState || !Mathf.Approximately(aimRadius, r))
        {
            Color c = currentState == SpellState.CastingMeteor ? new Color(1f, 0.6f, 0.2f, 0.95f) : new Color(0.3f, 0.8f, 1f, 0.95f);
            if (aimLine != null) { aimLine.startColor = c; aimLine.endColor = c; }
            if (aimIndicator != null) aimIndicator.DrawCircle(r);
            aimRadius = r;
            aimState = currentState;
        }

        Vector2 mp = (Vector2)Camera.main.ScreenToWorldPoint(GameInput.MousePosition);
        aimRing.transform.position = new Vector3(mp.x, mp.y, 0f);
        if (!aimRing.activeSelf) aimRing.SetActive(true);
    }

    /// <summary>Tạo vòng tròn (LineRenderer + RangeIndicator có sẵn). life &gt; 0: tự hủy sau life giây.</summary>
    private GameObject MakeRing(Vector2 pos, float radius, Color color, float life)
    {
        GameObject go = new GameObject("SpellRing");
        go.transform.position = new Vector3(pos.x, pos.y, 0f);
        LineRenderer lr = go.AddComponent<LineRenderer>();
        RangeIndicator ri = go.AddComponent<RangeIndicator>();
        lr.sortingOrder = 200;
        lr.startColor = color;
        lr.endColor = color;
        ri.DrawCircle(radius);
        if (life > 0f) Destroy(go, life);
        return go;
    }

    public void PrepareMeteor()
    {
        if (meteorUses > 0)
        {
            currentState = SpellState.CastingMeteor;
            Debug.Log("[Spell] Click trên bản đồ để thả Meteor Strike (AoE lớn toàn màn)");
        }
    }

    private IEnumerator CastMeteor(Vector2 targetPos)
    {
        Debug.Log("[Spell] Thiên thạch Meteor đang rơi... 1s delay!");
        // v3: vòng cảnh báo đúng vùng sát thương tại điểm vừa click, tồn tại tới khi thiên thạch giáng xuống
        MakeRing(targetPos, MeteorRadius, new Color(1f, 0.35f, 0.1f, 0.95f), meteorDelay + 0.4f);
        yield return new WaitForSeconds(meteorDelay);

        var enemies = EnemyRegistry.ActiveEnemies;
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            if (i >= enemies.Count) continue;
            EnemyHealth eh = enemies[i];
            if (eh == null || !eh.gameObject.activeInHierarchy || eh.CurrentHealth <= 0) continue;

            if (Vector2.Distance(targetPos, eh.transform.position) <= MeteorRadius)
            {
                DamageInfo dmg = new DamageInfo(meteorDamage, DamageType.Explosive, gameObject);
                eh.TakeDamage(dmg);

                // Thêm hiệu ứng thiêu đốt
                StatusEffectController sec = eh.GetComponent<StatusEffectController>();
                if (sec != null)
                {
                    sec.ApplyEffect(StatusEffectType.Burn, 15f, 3.0f, gameObject);
                }
            }
        }
        Debug.Log("[Spell] Thiên thạch giáng lâm! Gây sát thương nổ cực mạnh.");
    }

    public void UseFreeze()
    {
        if (freezeUses > 0)
        {
            freezeUses--;
            GlobalFreezeRemaining = freezeDuration;

            var enemies = EnemyRegistry.ActiveEnemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyHealth eh = enemies[i];
                if (eh == null || !eh.gameObject.activeInHierarchy || eh.CurrentHealth <= 0) continue;

                StatusEffectController sec = eh.GetComponent<StatusEffectController>() ?? eh.gameObject.AddComponent<StatusEffectController>();
                sec.ApplyEffect(StatusEffectType.Freeze, 1f, freezeDuration, gameObject);
            }
            Debug.Log($"[Spell] Đã đóng băng toàn bộ quái vật trên bản đồ trong {freezeDuration} giây!");
        }
    }

    public void PrepareReinforcements()
    {
        if (Time.time - lastReinforcementTime >= reinforcementCooldown)
        {
            currentState = SpellState.CastingReinforcements;
            Debug.Log("[Spell] Click trên đường để gọi 2 hiệp sĩ tiếp viện!");
        }
    }

    private void CastReinforcements(Vector2 targetPos)
    {
        if (reinforcementPrefab != null)
        {
            Instantiate(reinforcementPrefab, targetPos + Vector2.left * 0.4f, Quaternion.identity);
            Instantiate(reinforcementPrefab, targetPos + Vector2.right * 0.4f, Quaternion.identity);
        }
        else
        {
            SpawnFallbackSoldier(targetPos + Vector2.left * 0.4f, "Reinforcement_1");
            SpawnFallbackSoldier(targetPos + Vector2.right * 0.4f, "Reinforcement_2");
        }
        Debug.Log("[Spell] 2 Hiệp sĩ tiếp viện đã xuất trận và chặn đường quái vật!");
    }

    private void SpawnFallbackSoldier(Vector2 pos, string name)
    {
        // Ưu tiên prefab lính tiếp viện do KRPrefabFactory dựng (có animation)
        GameObject soldierPrefab = Resources.Load<GameObject>("Units/ReinforcementSoldier");
        if (soldierPrefab != null)
        {
            GameObject spawned = Instantiate(soldierPrefab, pos, Quaternion.identity);
            spawned.name = name;
            return;
        }

        GameObject soldierObj = new GameObject(name);
        soldierObj.transform.position = pos;

        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(soldierObj.transform, false);
        visual.transform.localScale = new Vector3(0.75f, 0.75f, 1f);

        SpriteRenderer sr = visual.AddComponent<SpriteRenderer>();
        sr.color = new Color(0.75f, 0.88f, 1f);
        sr.sortingOrder = 9;

        var allSprites = Resources.FindObjectsOfTypeAll<Sprite>();
        foreach (var s in allSprites)
        {
            if (s.name.ToLower().Contains("idle_01") || s.name.ToLower().Contains("idle"))
            {
                sr.sprite = s;
                break;
            }
        }

        CircleCollider2D col = soldierObj.AddComponent<CircleCollider2D>();
        col.radius = 0.35f;
        col.isTrigger = true;

        Rigidbody2D rb = soldierObj.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;

        ReinforcementSoldier soldier = soldierObj.AddComponent<ReinforcementSoldier>();
        soldier.spriteRenderer = sr;
    }
}
