using UnityEngine;

public enum DamageType
{
    Physical,   // Mitigated by full Armor
    Explosive,  // Mitigated by half Armor (AoE Cannons)
    Magic,      // Mitigated by Magic Resist (Mage Bolts)
    True,        // Ignores all defenses
    TrueDamage,
    Poison
}

[System.Serializable]
public struct DamageInfo
{
    public float amount;
    public DamageType type;
    public GameObject source;
    public bool isCrit;
    public float armorPenetration; // 0.0 to 1.0

    public DamageInfo(float amount, DamageType type = DamageType.Physical, GameObject source = null, bool isCrit = false, float armorPenetration = 0f)
    {
        this.amount = amount;
        this.type = type;
        this.source = source;
        this.isCrit = isCrit;
        this.armorPenetration = armorPenetration;
    }
}
