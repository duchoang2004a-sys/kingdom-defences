using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

[System.Serializable]
public class StatusEffect
{
    public StatusType type;
    public float duration;
    public float value;
    public GameObject source;

    public StatusEffect(StatusType t, float d, float v, GameObject s = null)
    {
        type = t;
        duration = d;
        value = v;
        source = s;
    }
}

/// <summary>
/// Quản lý toàn bộ hiệu ứng trạng thái (Status Effects) cho Quái:
/// - Khống chế: Slow (Làm chậm), Freeze (Băng giá), Stun (Choáng váng)
/// - Sát thương theo thời gian: Burn (Thiêu đốt), Poison (Trúng độc)
/// - Trạng thái đặc biệt: ArmorBreak (Phá giáp), Wounded (Vết thương hở chống hồi máu)
/// - Tích hợp chỉ số statusResist (Kháng khống chế)
/// </summary>
public class StatusEffectController : MonoBehaviour
{
    [Header("Chỉ số kháng khống chế (0.0 = 0%, 0.7 = 70% giảm thời gian CC)")]
    public float statusResist = 0f;

    [SerializeField]
    private List<StatusEffect> activeEffects = new List<StatusEffect>();

    private Enemy enemy;
    private EnemyHealth health;
    private EnemyMovement movement;
    private float dotTickTimer = 0f;

    // Thuộc tính kiểm tra trạng thái cho các hệ thống khác
    public bool IsDisabled => activeEffects.Exists(e => (e.type == StatusType.Freeze || e.type == StatusType.Stun) && e.duration > 0);
    public bool IsWounded => activeEffects.Exists(e => e.type == StatusType.Wounded && e.duration > 0);
    public float ArmorReduction => GetEffectValue(StatusType.ArmorBreak);
    public float SlowFactor => GetEffectValue(StatusType.Slow);

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        health = GetComponent<EnemyHealth>();
        movement = GetComponent<EnemyMovement>();
    }

    private void Update()
    {
        if (enemy == null && health == null) return;

        // 1. Cập nhật đếm lùi thời gian các hiệu ứng
        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            var effect = activeEffects[i];
            if (effect == null)
            {
                activeEffects.RemoveAt(i);
                continue;
            }

            effect.duration -= Time.deltaTime;
            if (effect.duration <= 0)
            {
                activeEffects.RemoveAt(i);
            }
        }

        // 2. Cập nhật tốc độ di chuyển dựa trên Freeze, Stun và Slow
        if (movement != null)
        {
            if (IsDisabled)
            {
                movement.SetStatusSpeedMultiplier(0f);
            }
            else
            {
                float slow = SlowFactor;
                if (slow > 0)
                {
                    float factor = slow > 0.85f ? (1f - slow) : (1f - Mathf.Clamp01(slow));
                    movement.SetStatusSpeedMultiplier(Mathf.Clamp(factor, 0.15f, 1f));
                }
                else
                {
                    movement.SetStatusSpeedMultiplier(1f);
                }
            }
        }

        // 3. Xử lý sát thương DoT (Burn, Poison) mỗi 0.5 giây
        dotTickTimer += Time.deltaTime;
        if (dotTickTimer >= 0.5f)
        {
            dotTickTimer = 0f;
            ApplyDoTDamage(0.5f);
        }
    }

    private void ApplyDoTDamage(float interval)
    {
        if (health == null || health.CurrentHealth <= 0) return;

        float totalBurn = GetEffectValue(StatusType.Burn);
        float totalPoison = GetEffectValue(StatusType.Poison);

        if (totalBurn > 0)
        {
            health.TakeDamage(new DamageInfo(totalBurn * interval, DamageType.TrueDamage));
        }

        if (totalPoison > 0)
        {
            health.TakeDamage(new DamageInfo(totalPoison * interval, DamageType.Poison));
        }
    }

    public float GetEffectValue(StatusType type)
    {
        float maxVal = 0f;
        for (int i = 0; i < activeEffects.Count; i++)
        {
            if (activeEffects[i].type == type && activeEffects[i].duration > 0)
            {
                if (activeEffects[i].value > maxVal) maxVal = activeEffects[i].value;
            }
        }
        return maxVal;
    }

    // --- CÁC HÀM OVERLOAD APPLY EFFECT ĐẦY ĐỦ CHO TẤT CẢ SCRIPT GỌI ---

    /// <summary>
    /// Áp dụng hiệu ứng với 4 tham số: Type, Sức mạnh/Giá trị, Thời gian, Nguồn gây ra
    /// </summary>
    public void ApplyEffect(StatusEffectType type, float value, float duration, GameObject source = null)
    {
        StatusType convertedType = ConvertType(type);
        ApplyInternal(convertedType, duration, value, source);
    }

    /// <summary>
    /// Áp dụng hiệu ứng với 3 tham số: Type, Sức mạnh/Giá trị, Thời gian
    /// </summary>
    public void ApplyEffect(StatusEffectType type, float value, float duration)
    {
        ApplyEffect(type, value, duration, null);
    }

    /// <summary>
    /// Áp dụng hiệu ứng (StatusType) 4 tham số
    /// </summary>
    public void ApplyEffect(StatusType type, float value, float duration, GameObject source = null)
    {
        ApplyInternal(type, duration, value, source);
    }

    /// <summary>
    /// Áp dụng hiệu ứng kiểu cũ (duration, value)
    /// </summary>
    public void ApplyEffect(StatusType type, float duration, float value = 0)
    {
        ApplyInternal(type, duration, value, null);
    }

    private void ApplyInternal(StatusType type, float duration, float value, GameObject source)
    {
        if ((enemy != null && enemy.IsDead()) || (health != null && health.CurrentHealth <= 0)) return;

        // Áp dụng kháng khống chế statusResist cho các hiệu ứng làm choáng, đóng băng, làm chậm
        float finalDuration = duration;
        if (type == StatusType.Freeze || type == StatusType.Stun || type == StatusType.Slow)
        {
            float resist = Mathf.Clamp(statusResist, 0f, 0.90f);
            finalDuration *= (1f - resist);
            if (finalDuration <= 0.05f) return; // Kháng hoàn toàn
        }

        var existing = activeEffects.Find(e => e.type == type);
        if (existing != null)
        {
            existing.duration = Mathf.Max(existing.duration, finalDuration);
            if (value > existing.value) existing.value = value;
            if (source != null) existing.source = source;
            return;
        }

        activeEffects.Add(new StatusEffect(type, finalDuration, value, source));
    }

    private StatusType ConvertType(StatusEffectType t)
    {
        switch (t)
        {
            case StatusEffectType.Slow: return StatusType.Slow;
            case StatusEffectType.Freeze: return StatusType.Freeze;
            case StatusEffectType.Stun: return StatusType.Stun;
            case StatusEffectType.Burn: return StatusType.Burn;
            case StatusEffectType.ArmorBreak: return StatusType.ArmorBreak;
            case StatusEffectType.Poison: return StatusType.Poison;
            case StatusEffectType.Wounded: return StatusType.Wounded;
            default: return StatusType.None;
        }
    }
}