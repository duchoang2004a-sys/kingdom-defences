using System;
using System.Collections.Generic;

// Mô hình dữ liệu map (file level_XX_map.json). Chỉ dùng kiểu đơn giản để JsonUtility đọc được
// (JsonUtility KHÔNG đọc được mảng lồng float[][] -> nguyên nhân cũ khiến waypoint bị rỗng).

[Serializable] public class KRPoint { public float x; public float y; }
[Serializable] public class KRSpawn { public string label; public float x; public float y; }
[Serializable] public class KRRoute { public int spawn; public string label; public List<KRPoint> points = new List<KRPoint>(); }
[Serializable] public class KRSlot { public int id; public float x; public float y; public float score; }
[Serializable] public class KRProp { public string sprite; public float x; public float y; public int order; }

[Serializable]
public class KRMap
{
    public int level;
    public string name;
    public string theme;
    public int imageW;
    public int imageH;
    public float cellW;
    public float cellH;
    public float gridOffsetX;
    public float gridOffsetY;

    public List<KRSpawn> spawns = new List<KRSpawn>();
    public KRPoint castle = new KRPoint();
    public List<KRRoute> routes = new List<KRRoute>();
    public List<KRSlot> slots = new List<KRSlot>();

    public List<string> tileIds = new List<string>();
    public List<int> gx = new List<int>();
    public List<int> gy = new List<int>();
    public List<int> gt = new List<int>();
    public List<int> dx = new List<int>();
    public List<int> dy = new List<int>();
    public List<int> dt = new List<int>();
    public List<KRProp> props = new List<KRProp>();
    public bool hasOverlay;

    public int waves;
    public int startGold;
    public int lives;
    public float hpMultiplier;
    public List<string> enemies = new List<string>();
    public string boss;
    public string tip;

    // Lịch boss: loại ("troll_king", "shaman", "golem_ice", "demon_lord", "dragon"), wave xuất hiện, HP
    public List<string> bossTypes = new List<string>();
    public List<int> bossWaves = new List<int>();
    public List<int> bossHps = new List<int>();
}
