using System;
using System.Collections.Generic;
using UnityEngine;

public enum BuffAttribute
{
    Damage,
    FireRate,
    Range,
    MoveSpeed
}

[Serializable]
public class ActiveBuff
{
    public string buffId;
    public BuffAttribute attribute;
    public float multiplier = 1f; // Ví dụ 1.35f = +35%
    public float flatAmount = 0f;
    public float durationRemaining;

    public ActiveBuff(string id, BuffAttribute attr, float mult, float flat, float duration)
    {
        buffId = id;
        attribute = attr;
        multiplier = mult;
        flatAmount = flat;
        durationRemaining = duration;
    }
}

/// <summary>
/// Quản lý Buff Tháp & Hero (BuffController - Part B2):
/// - Tách biệt hoàn toàn chỉ số gốc (Base Stats) với chỉ số hiệu dụng (Effective Stats).
/// - An toàn khi nâng cấp tháp giữa chừng: chỉ sửa Base Stats, các Buff nhân/cộng giữ nguyên tỷ lệ trên giá trị mới.
/// - Áp dụng trần tối đa (Max Caps): DMG cap 2.0x, Fire Rate cap 1.8x.
/// </summary>
public class BuffController : MonoBehaviour
{
    private readonly List<ActiveBuff> activeBuffs = new List<ActiveBuff>();

    // Caps tối đa theo đề bài (Part B2)
    public const float MaxDamageMultiplierCap = 2.0f;
    public const float MaxFireRateMultiplierCap = 1.8f;

    public float DamageMultiplier { get; private set; } = 1.0f;
    public float FireRateMultiplier { get; private set; } = 1.0f;

    private void Update()
    {
        if (activeBuffs.Count == 0) return;

        bool changed = false;
        float dt = Time.deltaTime;

        for (int i = activeBuffs.Count - 1; i >= 0; i--)
        {
            ActiveBuff b = activeBuffs[i];
            b.durationRemaining -= dt;
            if (b.durationRemaining <= 0)
            {
                activeBuffs.RemoveAt(i);
                changed = true;
            }
        }

        if (changed)
        {
            RecalculateBuffs();
        }
    }

    public void ApplyBuff(string buffId, BuffAttribute attr, float multiplier, float flat, float duration)
    {
        ActiveBuff existing = activeBuffs.Find(b => b.buffId == buffId && b.attribute == attr);
        if (existing != null)
        {
            // Cùng nguồn thì làm mới thời gian (Refresh duration)
            existing.durationRemaining = Mathf.Max(existing.durationRemaining, duration);
            existing.multiplier = Mathf.Max(existing.multiplier, multiplier);
        }
        else
        {
            activeBuffs.Add(new ActiveBuff(buffId, attr, multiplier, flat, duration));
        }

        RecalculateBuffs();
    }

    private void RecalculateBuffs()
    {
        float dmgMult = 1.0f;
        float rateMult = 1.0f;

        foreach (var b in activeBuffs)
        {
            if (b.attribute == BuffAttribute.Damage)
            {
                dmgMult *= b.multiplier;
            }
            else if (b.attribute == BuffAttribute.FireRate)
            {
                rateMult *= b.multiplier;
            }
        }

        DamageMultiplier = Mathf.Min(dmgMult, MaxDamageMultiplierCap);
        FireRateMultiplier = Mathf.Min(rateMult, MaxFireRateMultiplierCap);
    }

    public void ClearAllBuffs()
    {
        activeBuffs.Clear();
        RecalculateBuffs();
    }
}
