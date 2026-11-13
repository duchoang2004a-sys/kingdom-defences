using System;
using System.Collections.Generic;
using UnityEngine;

public enum ItemType
{
    Oil,        // Dầu tăng cường tiêu hao có số lượng (mua bằng Gem)
    Skill,      // Kỹ năng hồi chiêu (như Nham Thạch, Làm Chậm)
    PoisonZone  // Vùng Độc Trải Đường
}

public enum ItemTargetType
{
    Turret,     // Nhắm vào Tháp
    Hero,       // Nhắm vào Hero
    Enemy,      // Nhắm vào Quái/Boss
    PathArc,    // Nhắm trải dọc cung đường đi
    Global      // Tác động toàn cục
}

/// <summary>
/// Định Nghĩa Dữ Liệu Vật Phẩm (ItemDefinition - Part B1 ScriptableObject):
/// Chứa thông tin nhận diện, quy tắc nhắm, hệ số tác động và giá Gem cửa hàng.
/// </summary>
[CreateAssetMenu(fileName = "NewItemDefinition", menuName = "Tower Defense/Item Definition")]
public class ItemDefinition : ScriptableObject
{
    [Header("Identification")]
    public string itemId = "Oil_Damage";
    public string itemName = "Dầu Nộ Sát Thương";
    public string iconName = "🔥";
    public ItemType itemType = ItemType.Oil;
    public ItemTargetType targetType = ItemTargetType.Turret;

    [Header("Usage Rules")]
    public float cooldownDuration = 0f;
    public float activeDuration = 20f;
    public float damageMultiplier = 1.35f; // +35% DMG
    public float fireRateMultiplier = 1.30f; // +30% Speed
    public float bossEffectMultiplier = 0.5f; // Độc chỉ còn 50% hiệu lực với Boss

    [Header("Shop Economy")]
    public int gemCost = 50;
    public int requiredCupsToUnlock = 0;
    [TextArea(2, 3)]
    public string description = "+35% Sát thương cho Tháp hoặc Hero trong 20 giây.";
}

public static class ItemDatabase
{
    private static Dictionary<string, ItemDefinition> items;

    public static ItemDefinition GetItem(string id)
    {
        if (items == null)
        {
            items = new Dictionary<string, ItemDefinition>();

            // 1. Dầu Nộ Sát Thương (+35% DMG, 20s)
            ItemDefinition oilDmg = ScriptableObject.CreateInstance<ItemDefinition>();
            oilDmg.itemId = "Oil_Damage";
            oilDmg.itemName = "Dầu Nộ Sát Thương";
            oilDmg.iconName = "🔥";
            oilDmg.itemType = ItemType.Oil;
            oilDmg.targetType = ItemTargetType.Turret;
            oilDmg.damageMultiplier = 1.35f;
            oilDmg.activeDuration = 20f;
            oilDmg.gemCost = 50;
            oilDmg.description = "+35% Sát thương cho tháp hoặc hero trong 20s.";
            items[oilDmg.itemId] = oilDmg;

            // 2. Dầu Tốc Bắn (+30% Fire Rate, 20s)
            ItemDefinition oilSpd = ScriptableObject.CreateInstance<ItemDefinition>();
            oilSpd.itemId = "Oil_FireRate";
            oilSpd.itemName = "Dầu Tốc Độ Bắn";
            oilSpd.iconName = "⚡";
            oilSpd.itemType = ItemType.Oil;
            oilSpd.targetType = ItemTargetType.Turret;
            oilSpd.fireRateMultiplier = 1.30f;
            oilSpd.activeDuration = 20f;
            oilSpd.gemCost = 50;
            oilSpd.description = "+30% Tốc độ bắn cho tháp trong 20s.";
            items[oilSpd.itemId] = oilSpd;

            // 3. Vùng Độc Trải Đường (Anti-heal Wounded, 12s)
            ItemDefinition poisonZone = ScriptableObject.CreateInstance<ItemDefinition>();
            poisonZone.itemId = "PoisonZone";
            poisonZone.itemName = "Vùng Độc Anti-Heal";
            poisonZone.iconName = "🧪";
            poisonZone.itemType = ItemType.PoisonZone;
            poisonZone.targetType = ItemTargetType.PathArc;
            poisonZone.activeDuration = 12f;
            poisonZone.gemCost = 60;
            poisonZone.description = "Trải dải độc 5u làm chậm 35%, độc DoT & Vết Thương (Khóa hồi máu Boss).";
            items[poisonZone.itemId] = poisonZone;
        }

        if (items.ContainsKey(id)) return items[id];
        return null;
    }
}
