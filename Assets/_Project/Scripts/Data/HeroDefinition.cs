using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class HeroData
{
    public string heroId;
    public string heroName;
    public string heroTitle;
    public string role;
    public int requiredCups;
    public float maxHealth;
    public float attackDamage;
    public float moveSpeed;
    public float attackRange;
    public string skill1Name;
    public string skill1Desc;
    public string skill2Name;
    public string skill2Desc;
    public Color themeColor;

    public HeroData(string id, string name, string title, string role, int cups, float hp, float dmg, float spd, float range, string s1Name, string s1Desc, string s2Name, string s2Desc, Color col)
    {
        heroId = id;
        heroName = name;
        heroTitle = title;
        this.role = role;
        requiredCups = cups;
        maxHealth = hp;
        attackDamage = dmg;
        moveSpeed = spd;
        attackRange = range;
        skill1Name = s1Name;
        skill1Desc = s1Desc;
        skill2Name = s2Name;
        skill2Desc = s2Desc;
        themeColor = col;
    }
}

/// <summary>
/// Cơ sở dữ liệu danh sách 4 Anh hùng chuẩn thể loại Kingdom Rush Tactics
/// </summary>
public static class HeroDatabase
{
    private static List<HeroData> heroes;

    public static List<HeroData> GetAllHeroes()
    {
        if (heroes == null)
        {
            heroes = new List<HeroData>
            {
                new HeroData(
                    "Knight",
                    "Hiệp Sĩ Gerald",
                    "Lá Chắn Vương Quốc",
                    "Cận chiến / Đỡ đòn (Tank)",
                    0, // Mở khóa sẵn
                    200f, 25f, 3.5f, 1.4f,
                    "⚔️ Cú Chém Dũng Mãnh", "Chém kiếm gây 45 ST vật lý và choáng 1s (AoE nhỏ)",
                    "📢 Tiếng Gầm Chiến Trận", "Gầm xung trận gây 75 ST phép và giảm 30% giáp kẻ địch trong 6s",
                    new Color(0.2f, 0.5f, 0.9f)
                ),
                new HeroData(
                    "Ranger",
                    "Nữ Cung Thủ Alleria",
                    "Bóng Ma Rừng Xanh",
                    "Xạ thủ tầm xa (Ranged DPS)",
                    4, // Cần 4 cúp
                    120f, 35f, 4.0f, 4.5f,
                    "🏹 Mưa Tên Bão Tố", "Bắn chùm tên diện rộng làm chậm 50% và gây 60 ST",
                    "🕸️ Bẫy Gai Rừng", "Đặt bẫy giữ chân quái vật trong 3s và gây độc",
                    new Color(0.2f, 0.8f, 0.35f)
                ),
                new HeroData(
                    "Mage",
                    "Đại Pháp Sư Ignis",
                    "Bậc Thầy Nguyên Tố",
                    "Pháp sư khống chế (AoE Control)",
                    8, // Cần 8 cúp
                    100f, 45f, 3.2f, 3.8f,
                    "⚡ Chuỗi Sét Tê Liệt", "Phóng tia sét giật lan qua 4 kẻ địch gây 80 ST phép",
                    "❄️ Bão Băng Vĩnh Cửu", "Đóng băng toàn bộ kẻ địch trong phạm vi lớn trong 4s",
                    new Color(0.6f, 0.25f, 0.9f)
                ),
                new HeroData(
                    "Paladin",
                    "Thánh Kỵ Sĩ Malik",
                    "Hào Quang Thánh Điện",
                    "Đỡ đòn / Hỗ trợ (Support Tank)",
                    12, // Cần 12 cúp
                    260f, 20f, 3.0f, 1.5f,
                    "🛡️ Khiên Thánh Bất Diệt", "Kích hoạt khiên giảm 80% sát thương và miễn nhiễm khống chế trong 4s",
                    "✨ Hào Quang Quang Minh", "Hồi 50 HP cho Hero & Lính, tăng 25% tốc bắn cho tháp xung quanh",
                    new Color(0.95f, 0.75f, 0.15f)
                )
            };
        }
        return heroes;
    }

    public static HeroData GetHeroById(string id)
    {
        var list = GetAllHeroes();
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].heroId.Equals(id, StringComparison.OrdinalIgnoreCase))
                return list[i];
        }
        return list[0]; // Mặc định trả về Knight
    }
}
