using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cấu hình 8 Màn chơi Handcrafted Isometric chuẩn toán học và hình học
/// Khớp 100% với ảnh thiết kế:
/// Màn 1: Đường mòn (1 đường, 33 bước, 8 ô tháp)
/// Màn 2: Ngã rẽ (2 đường, 22+23 bước, 10 ô tháp)
/// Màn 3: Cánh đồng (2 đường, 27+26 bước, 9 ô tháp)
/// Màn 4: Hẻm đá (1 đường, 35 bước, 9 ô tháp)
/// Màn 5: Giao lộ (2 đường, 27+28 bước, 10 ô tháp)
/// Màn 6: Kẻ dẫn đường (2 đường, 24+23 bước, 10 ô tháp)
/// Màn 7: Thành lũy (1 đường, 36 bước, 9 ô tháp)
/// Màn 8: Băng giá (2 đường, 34+23 bước, 10 ô tháp - Boss Hỏa Long)
/// </summary>
public static class HandcraftedLevelDatabase
{
    public class LevelLayout
    {
        public int levelNumber;
        public string displayName;
        public string subtitle;
        public string theme; // "Grass" hoặc "Snow"
        public int startingGold;
        public int startingLives;
        public int maxWaves;
        public float hpScalePerWave;
        public MissionType missionType;
        public float missionTargetValue;
        public string bossType; // "None", "Troll", "Shaman", "Golem", "Dragon"
        public List<Vector3Int> path1 = new List<Vector3Int>();
        public List<Vector3Int> path2 = new List<Vector3Int>();
        public List<Vector3Int> plots = new List<Vector3Int>();
        public List<Vector3Int> decorativeTrees = new List<Vector3Int>();
        public List<Vector3Int> decorativeRocks = new List<Vector3Int>();
        public List<Vector3Int> decorativeCrystals = new List<Vector3Int>();
        public string spawnAName = "Spawn";
        public string spawnBName = "Spawn B";
    }

    public static LevelLayout GetLayout(int levelNum)
    {
        LevelLayout layout = new LevelLayout();
        layout.levelNumber = levelNum;

        switch (levelNum)
        {
            case 1:
                layout.displayName = "Màn 1: Đường Mòn";
                layout.subtitle = "Màn 1 · Đường mòn · cỏ · 1 đường (33 bước) · 8 ô tháp";
                layout.theme = "Grass";
                layout.startingGold = 350;
                layout.startingLives = 20;
                layout.maxWaves = 3;
                layout.hpScalePerWave = 0.15f;
                layout.missionType = MissionType.SurviveWaves;
                layout.missionTargetValue = 3f;
                layout.bossType = "None";
                layout.spawnAName = "Spawn";

                // Path 1 (33 bước): Uốn lượn từ góc dưới trái lên góc trên phải
                layout.path1 = new List<Vector3Int>
                {
                    new Vector3Int(-10, -3, 0),
                    new Vector3Int(-7, -3, 0),
                    new Vector3Int(-7, 1, 0),
                    new Vector3Int(-2, 1, 0),
                    new Vector3Int(-2, -2, 0),
                    new Vector3Int(4, -2, 0),
                    new Vector3Int(4, 3, 0),
                    new Vector3Int(10, 3, 0)
                };

                // 8 Ô Đặt Tháp (Plots) được đánh số thứ tự từ 1 đến 8 như trong ảnh mẫu
                layout.plots = new List<Vector3Int>
                {
                    new Vector3Int(-4, 2, 0),   // Ô 1
                    new Vector3Int(5, -1, 0),   // Ô 2
                    new Vector3Int(8, 4, 0),    // Ô 3
                    new Vector3Int(-8, -2, 0),  // Ô 4
                    new Vector3Int(1, -1, 0),   // Ô 5
                    new Vector3Int(5, 4, 0),    // Ô 6
                    new Vector3Int(2, 0, 0),    // Ô 7
                    new Vector3Int(-4, -1, 0)   // Ô 8
                };

                // Cây cối, đá và pha lê trang trí
                layout.decorativeTrees = new List<Vector3Int>
                {
                    new Vector3Int(-12, 1, 0), new Vector3Int(-10, 4, 0), new Vector3Int(-5, 5, 0),
                    new Vector3Int(1, 5, 0), new Vector3Int(7, 6, 0), new Vector3Int(12, 5, 0),
                    new Vector3Int(-11, -5, 0), new Vector3Int(-4, -5, 0), new Vector3Int(3, -5, 0),
                    new Vector3Int(9, -4, 0), new Vector3Int(12, -1, 0)
                };
                layout.decorativeRocks = new List<Vector3Int>
                {
                    new Vector3Int(-9, 2, 0), new Vector3Int(-6, -4, 0), new Vector3Int(0, 3, 0),
                    new Vector3Int(7, -3, 0), new Vector3Int(11, 1, 0)
                };
                layout.decorativeCrystals = new List<Vector3Int>
                {
                    new Vector3Int(-8, 3, 0), new Vector3Int(11, -3, 0)
                };
                break;

            case 2:
                layout.displayName = "Màn 2: Ngã Rẽ";
                layout.subtitle = "Màn 2 · Ngã rẽ · cỏ · 2 đường (22+23 bước) · 10 ô tháp";
                layout.theme = "Grass";
                layout.startingGold = 400;
                layout.startingLives = 20;
                layout.maxWaves = 3;
                layout.hpScalePerWave = 0.16f;
                layout.missionType = MissionType.SurviveWaves;
                layout.missionTargetValue = 3f;
                layout.bossType = "None";
                layout.spawnAName = "Spawn A";
                layout.spawnBName = "Spawn B";

                // 2 Paths: Nhánh Spawn A và Spawn B hội tụ tại ngã rẽ rồi tiến vào Lâu đài
                layout.path1 = new List<Vector3Int>
                {
                    new Vector3Int(-10, 2, 0),
                    new Vector3Int(-5, 2, 0),
                    new Vector3Int(-5, 0, 0),
                    new Vector3Int(0, 0, 0),
                    new Vector3Int(10, 4, 0)
                };
                layout.path2 = new List<Vector3Int>
                {
                    new Vector3Int(-6, -4, 0),
                    new Vector3Int(0, -4, 0),
                    new Vector3Int(0, 0, 0),
                    new Vector3Int(10, 4, 0)
                };

                // 10 Ô tháp bao quanh 2 nhánh đường
                layout.plots = new List<Vector3Int>
                {
                    new Vector3Int(1, 1, 0),    // Ô 1
                    new Vector3Int(8, 5, 0),    // Ô 2
                    new Vector3Int(-6, 1, 0),   // Ô 3
                    new Vector3Int(4, 2, 0),    // Ô 4
                    new Vector3Int(1, -2, 0),   // Ô 5
                    new Vector3Int(6, 4, 0),    // Ô 6
                    new Vector3Int(-2, -5, 0),  // Ô 7
                    new Vector3Int(-1, 1, 0),   // Ô 8
                    new Vector3Int(-8, 3, 0),   // Ô 9
                    new Vector3Int(6, 2, 0)     // Ô 10
                };

                layout.decorativeTrees = new List<Vector3Int>
                {
                    new Vector3Int(-11, 4, 0), new Vector3Int(-3, 4, 0), new Vector3Int(3, 5, 0),
                    new Vector3Int(11, 6, 0), new Vector3Int(-9, -2, 0), new Vector3Int(-3, -2, 0),
                    new Vector3Int(4, -5, 0), new Vector3Int(10, 0, 0)
                };
                layout.decorativeRocks = new List<Vector3Int>
                {
                    new Vector3Int(-4, 3, 0), new Vector3Int(3, 0, 0), new Vector3Int(-4, -5, 0)
                };
                layout.decorativeCrystals = new List<Vector3Int>
                {
                    new Vector3Int(-7, 4, 0), new Vector3Int(12, 2, 0)
                };
                break;

            case 3:
                layout.displayName = "Màn 3: Cánh Đồng";
                layout.subtitle = "Màn 3 · Cánh đồng · cỏ · 2 đường (27+26 bước) · 9 ô tháp";
                layout.theme = "Grass";
                layout.startingGold = 420;
                layout.startingLives = 20;
                layout.maxWaves = 3;
                layout.hpScalePerWave = 0.17f;
                layout.missionType = MissionType.SurviveWaves;
                layout.missionTargetValue = 3f;
                layout.bossType = "None";
                layout.spawnAName = "Spawn A";
                layout.spawnBName = "Spawn B";

                layout.path1 = new List<Vector3Int>
                {
                    new Vector3Int(-10, 2, 0),
                    new Vector3Int(-5, 2, 0),
                    new Vector3Int(-2, 4, 0),
                    new Vector3Int(2, 1, 0),
                    new Vector3Int(5, 4, 0),
                    new Vector3Int(8, 4, 0)
                };
                layout.path2 = new List<Vector3Int>
                {
                    new Vector3Int(-5, -4, 0),
                    new Vector3Int(0, -4, 0),
                    new Vector3Int(2, -1, 0),
                    new Vector3Int(6, 2, 0),
                    new Vector3Int(10, 3, 0)
                };

                layout.plots = new List<Vector3Int>
                {
                    new Vector3Int(0, 3, 0),    // Ô 1
                    new Vector3Int(0, -2, 0),   // Ô 2
                    new Vector3Int(6, 5, 0),    // Ô 3
                    new Vector3Int(-7, 1, 0),   // Ô 4
                    new Vector3Int(3, 2, 0),    // Ô 5
                    new Vector3Int(-2, -5, 0),  // Ô 6
                    new Vector3Int(4, 4, 0),    // Ô 7
                    new Vector3Int(1, 0, 0),    // Ô 8
                    new Vector3Int(-4, 3, 0)    // Ô 9
                };

                layout.decorativeTrees = new List<Vector3Int>
                {
                    new Vector3Int(-11, 4, 0), new Vector3Int(-7, 5, 0), new Vector3Int(0, 6, 0),
                    new Vector3Int(9, 6, 0), new Vector3Int(-8, -2, 0), new Vector3Int(4, -4, 0),
                    new Vector3Int(9, -1, 0)
                };
                layout.decorativeRocks = new List<Vector3Int>
                {
                    new Vector3Int(-3, 1, 0), new Vector3Int(4, 0, 0), new Vector3Int(-3, -3, 0)
                };
                layout.decorativeCrystals = new List<Vector3Int>
                {
                    new Vector3Int(-6, 3, 0), new Vector3Int(3, -3, 0)
                };
                break;

            case 4:
                layout.displayName = "Màn 4: Hẻm Đá";
                layout.subtitle = "Màn 4 · Hẻm đá · cỏ · 1 đường (35 bước) · 9 ô tháp";
                layout.theme = "Grass";
                layout.startingGold = 450;
                layout.startingLives = 20;
                layout.maxWaves = 3;
                layout.hpScalePerWave = 0.18f;
                layout.missionType = MissionType.KillBosses;
                layout.missionTargetValue = 3f;
                layout.bossType = "Troll";
                layout.spawnAName = "Spawn";

                layout.path1 = new List<Vector3Int>
                {
                    new Vector3Int(-9, -4, 0),
                    new Vector3Int(-7, -1, 0),
                    new Vector3Int(-3, 3, 0),
                    new Vector3Int(0, 0, 0),
                    new Vector3Int(3, 2, 0),
                    new Vector3Int(6, -1, 0),
                    new Vector3Int(9, 4, 0)
                };

                layout.plots = new List<Vector3Int>
                {
                    new Vector3Int(-4, 1, 0),   // Ô 1
                    new Vector3Int(4, 0, 0),    // Ô 2
                    new Vector3Int(7, 3, 0),    // Ô 3
                    new Vector3Int(-7, -3, 0),  // Ô 4
                    new Vector3Int(-1, 1, 0),   // Ô 5
                    new Vector3Int(5, 2, 0),    // Ô 6
                    new Vector3Int(-8, 0, 0),   // Ô 7
                    new Vector3Int(1, 1, 0),    // Ô 8
                    new Vector3Int(8, 2, 0)     // Ô 9
                };

                layout.decorativeRocks = new List<Vector3Int>
                {
                    new Vector3Int(-10, 0, 0), new Vector3Int(-6, 4, 0), new Vector3Int(0, -3, 0),
                    new Vector3Int(5, -4, 0), new Vector3Int(10, 1, 0), new Vector3Int(-3, -4, 0)
                };
                layout.decorativeTrees = new List<Vector3Int>
                {
                    new Vector3Int(-11, -3, 0), new Vector3Int(-2, 5, 0), new Vector3Int(7, 5, 0),
                    new Vector3Int(8, -3, 0)
                };
                layout.decorativeCrystals = new List<Vector3Int>
                {
                    new Vector3Int(9, 5, 0), new Vector3Int(-9, 3, 0)
                };
                break;

            case 5:
                layout.displayName = "Màn 5: Giao Lộ";
                layout.subtitle = "Màn 5 · Giao lộ · cỏ · 2 đường (27+28 bước) · 10 ô tháp";
                layout.theme = "Grass";
                layout.startingGold = 480;
                layout.startingLives = 20;
                layout.maxWaves = 3;
                layout.hpScalePerWave = 0.18f;
                layout.missionType = MissionType.SurviveWaves;
                layout.missionTargetValue = 3f;
                layout.bossType = "None";
                layout.spawnAName = "Spawn A";
                layout.spawnBName = "Spawn B";

                layout.path1 = new List<Vector3Int>
                {
                    new Vector3Int(-10, -2, 0),
                    new Vector3Int(-5, 0, 0),
                    new Vector3Int(-2, 3, 0),
                    new Vector3Int(3, 3, 0),
                    new Vector3Int(6, 1, 0),
                    new Vector3Int(10, 3, 0)
                };
                layout.path2 = new List<Vector3Int>
                {
                    new Vector3Int(-8, -4, 0),
                    new Vector3Int(-3, -2, 0),
                    new Vector3Int(1, -2, 0),
                    new Vector3Int(4, 1, 0),
                    new Vector3Int(10, 3, 0)
                };

                layout.plots = new List<Vector3Int>
                {
                    new Vector3Int(-3, 0, 0),   // Ô 1
                    new Vector3Int(4, 4, 0),    // Ô 2
                    new Vector3Int(-5, -3, 0),  // Ô 3
                    new Vector3Int(2, 0, 0),    // Ô 4
                    new Vector3Int(0, 4, 0),    // Ô 5
                    new Vector3Int(7, 2, 0),    // Ô 6
                    new Vector3Int(1, -1, 0),   // Ô 7
                    new Vector3Int(-4, -1, 0),  // Ô 8
                    new Vector3Int(5, 5, 0),    // Ô 9
                    new Vector3Int(-7, -2, 0)   // Ô 10
                };

                layout.decorativeTrees = new List<Vector3Int>
                {
                    new Vector3Int(-11, 2, 0), new Vector3Int(-4, 5, 0), new Vector3Int(2, 6, 0),
                    new Vector3Int(9, 5, 0), new Vector3Int(-10, -5, 0), new Vector3Int(4, -4, 0)
                };
                layout.decorativeRocks = new List<Vector3Int>
                {
                    new Vector3Int(-6, 2, 0), new Vector3Int(0, 1, 0), new Vector3Int(6, -2, 0)
                };
                layout.decorativeCrystals = new List<Vector3Int>
                {
                    new Vector3Int(2, 5, 0), new Vector3Int(11, -1, 0)
                };
                break;

            case 6:
                layout.displayName = "Màn 6: Kẻ Dẫn Đường";
                layout.subtitle = "Màn 6 · Kẻ dẫn đường · cỏ · 2 đường (24+23 bước) · 10 ô tháp";
                layout.theme = "Grass";
                layout.startingGold = 500;
                layout.startingLives = 20;
                layout.maxWaves = 3;
                layout.hpScalePerWave = 0.19f;
                layout.missionType = MissionType.KillBosses;
                layout.missionTargetValue = 3f;
                layout.bossType = "Shaman";
                layout.spawnAName = "Spawn A";
                layout.spawnBName = "Spawn B";

                layout.path1 = new List<Vector3Int>
                {
                    new Vector3Int(-10, 1, 0),
                    new Vector3Int(-4, 3, 0),
                    new Vector3Int(3, 4, 0),
                    new Vector3Int(7, 2, 0),
                    new Vector3Int(10, 3, 0)
                };
                layout.path2 = new List<Vector3Int>
                {
                    new Vector3Int(-5, -4, 0),
                    new Vector3Int(-1, -1, 0),
                    new Vector3Int(4, 0, 0),
                    new Vector3Int(7, 2, 0),
                    new Vector3Int(10, 3, 0)
                };

                layout.plots = new List<Vector3Int>
                {
                    new Vector3Int(5, 3, 0),    // Ô 1
                    new Vector3Int(1, 2, 0),    // Ô 2
                    new Vector3Int(1, -2, 0),   // Ô 3
                    new Vector3Int(-6, 2, 0),   // Ô 4
                    new Vector3Int(8, 4, 0),    // Ô 5
                    new Vector3Int(3, 2, 0),    // Ô 6
                    new Vector3Int(1, -4, 0),   // Ô 7
                    new Vector3Int(-8, 2, 0),   // Ô 8
                    new Vector3Int(8, 1, 0),    // Ô 9
                    new Vector3Int(1, 0, 0)     // Ô 10
                };

                layout.decorativeTrees = new List<Vector3Int>
                {
                    new Vector3Int(-11, 4, 0), new Vector3Int(-2, 5, 0), new Vector3Int(5, 6, 0),
                    new Vector3Int(11, 5, 0), new Vector3Int(-8, -2, 0), new Vector3Int(5, -3, 0)
                };
                layout.decorativeRocks = new List<Vector3Int>
                {
                    new Vector3Int(-3, 1, 0), new Vector3Int(4, -2, 0), new Vector3Int(7, -1, 0)
                };
                layout.decorativeCrystals = new List<Vector3Int>
                {
                    new Vector3Int(-7, -5, 0), new Vector3Int(10, 1, 0)
                };
                break;

            case 7:
                layout.displayName = "Màn 7: Thành Lũy";
                layout.subtitle = "Màn 7 · Thành lũy · cỏ · 1 đường (36 bước) · 9 ô tháp";
                layout.theme = "Grass";
                layout.startingGold = 550;
                layout.startingLives = 20;
                layout.maxWaves = 3;
                layout.hpScalePerWave = 0.20f;
                layout.missionType = MissionType.ProtectCastle;
                layout.missionTargetValue = 0.70f;
                layout.bossType = "Golem";
                layout.spawnAName = "Spawn";

                layout.path1 = new List<Vector3Int>
                {
                    new Vector3Int(-8, -4, 0),
                    new Vector3Int(-6, -1, 0),
                    new Vector3Int(-7, 2, 0),
                    new Vector3Int(-2, 3, 0),
                    new Vector3Int(4, 0, 0),
                    new Vector3Int(6, 3, 0),
                    new Vector3Int(9, 4, 0)
                };

                layout.plots = new List<Vector3Int>
                {
                    new Vector3Int(4, 1, 0),    // Ô 1
                    new Vector3Int(-3, 2, 0),   // Ô 2
                    new Vector3Int(-6, -3, 0),  // Ô 3
                    new Vector3Int(5, 3, 0),    // Ô 4
                    new Vector3Int(-6, 0, 0),   // Ô 5
                    new Vector3Int(7, 4, 0),    // Ô 6
                    new Vector3Int(6, 1, 0),    // Ô 7
                    new Vector3Int(-6, 1, 0),   // Ô 8
                    new Vector3Int(3, 2, 0)     // Ô 9
                };

                layout.decorativeTrees = new List<Vector3Int>
                {
                    new Vector3Int(-11, 0, 0), new Vector3Int(-5, 4, 0), new Vector3Int(1, 4, 0),
                    new Vector3Int(8, 6, 0), new Vector3Int(-9, -5, 0), new Vector3Int(8, -2, 0)
                };
                layout.decorativeRocks = new List<Vector3Int>
                {
                    new Vector3Int(-1, 1, 0), new Vector3Int(2, -2, 0), new Vector3Int(8, 0, 0)
                };
                layout.decorativeCrystals = new List<Vector3Int>
                {
                    new Vector3Int(-2, 5, 0), new Vector3Int(9, 2, 0)
                };
                break;

            case 8:
                layout.levelNumber = 8;
                layout.displayName = "Màn 8: Băng Giá";
                layout.subtitle = "Màn 8 · Băng giá · tuyết · 2 đường (34+23 bước) · 10 ô tháp";
                layout.theme = "Snow";
                layout.startingGold = 650;
                layout.startingLives = 20;
                layout.maxWaves = 3;
                layout.hpScalePerWave = 0.22f;
                layout.missionType = MissionType.KillBosses;
                layout.missionTargetValue = 3f;
                layout.bossType = "Dragon"; // BOSS HỎA LONG CỔ ĐẠI
                layout.spawnAName = "Spawn A";
                layout.spawnBName = "Spawn B";

                layout.path1 = new List<Vector3Int>
                {
                    new Vector3Int(-10, -2, 0),
                    new Vector3Int(-6, 0, 0),
                    new Vector3Int(-3, 3, 0),
                    new Vector3Int(2, 3, 0),
                    new Vector3Int(5, 1, 0),
                    new Vector3Int(10, 4, 0)
                };
                layout.path2 = new List<Vector3Int>
                {
                    new Vector3Int(-5, -4, 0),
                    new Vector3Int(-1, -2, 0),
                    new Vector3Int(1, 0, 0),
                    new Vector3Int(5, 1, 0),
                    new Vector3Int(10, 4, 0)
                };

                layout.plots = new List<Vector3Int>
                {
                    new Vector3Int(7, 3, 0),    // Ô 1
                    new Vector3Int(0, 0, 0),    // Ô 2
                    new Vector3Int(-1, 4, 0),   // Ô 3
                    new Vector3Int(6, 4, 0),    // Ô 4
                    new Vector3Int(-7, 0, 0),   // Ô 5
                    new Vector3Int(2, 1, 0),    // Ô 6
                    new Vector3Int(0, -2, 0),   // Ô 7
                    new Vector3Int(5, 2, 0),    // Ô 8
                    new Vector3Int(-1, 1, 0),   // Ô 9
                    new Vector3Int(8, 3, 0)     // Ô 10
                };

                layout.decorativeTrees = new List<Vector3Int>
                {
                    new Vector3Int(-11, 1, 0), new Vector3Int(-4, 5, 0), new Vector3Int(3, 5, 0),
                    new Vector3Int(11, 6, 0), new Vector3Int(-8, -4, 0), new Vector3Int(4, -3, 0),
                    new Vector3Int(10, 0, 0)
                };
                layout.decorativeRocks = new List<Vector3Int>
                {
                    new Vector3Int(-8, 2, 0), new Vector3Int(-2, 0, 0), new Vector3Int(3, -1, 0),
                    new Vector3Int(7, 0, 0)
                };
                layout.decorativeCrystals = new List<Vector3Int>
                {
                    new Vector3Int(-7, 3, 0), new Vector3Int(8, 1, 0), new Vector3Int(9, -2, 0)
                };
                break;

            case 9:
                layout.levelNumber = 9;
                layout.displayName = "Màn 9: Núi Lửa";
                layout.subtitle = "Màn 9 · Núi lửa · đá đỏ · 2 đường (28+26 bước) · 10 ô tháp";
                layout.theme = "Grass";
                layout.startingGold = 700;
                layout.startingLives = 20;
                layout.maxWaves = 3;
                layout.hpScalePerWave = 0.25f;
                layout.missionType = MissionType.SurviveWaves;
                layout.missionTargetValue = 3f;
                layout.bossType = "Golem";
                layout.spawnAName = "Spawn A";
                layout.spawnBName = "Spawn B";

                layout.path1 = new List<Vector3Int>
                {
                    new Vector3Int(-10, 3, 0),
                    new Vector3Int(-4, 3, 0),
                    new Vector3Int(-1, 0, 0),
                    new Vector3Int(3, 0, 0),
                    new Vector3Int(6, 2, 0),
                    new Vector3Int(10, 2, 0)
                };
                layout.path2 = new List<Vector3Int>
                {
                    new Vector3Int(-8, -4, 0),
                    new Vector3Int(-3, -2, 0),
                    new Vector3Int(1, -2, 0),
                    new Vector3Int(5, 0, 0),
                    new Vector3Int(10, 2, 0)
                };

                layout.plots = new List<Vector3Int>
                {
                    new Vector3Int(-6, 4, 0),
                    new Vector3Int(-2, 2, 0),
                    new Vector3Int(2, 2, 0),
                    new Vector3Int(7, 3, 0),
                    new Vector3Int(-5, -3, 0),
                    new Vector3Int(-1, -1, 0),
                    new Vector3Int(3, -1, 0),
                    new Vector3Int(7, 1, 0),
                    new Vector3Int(0, 1, 0),
                    new Vector3Int(4, 1, 0)
                };

                layout.decorativeTrees = new List<Vector3Int>
                {
                    new Vector3Int(-12, 4, 0), new Vector3Int(-9, 5, 0), new Vector3Int(-4, 5, 0),
                    new Vector3Int(8, 4, 0), new Vector3Int(-11, -3, 0), new Vector3Int(9, -2, 0)
                };
                layout.decorativeRocks = new List<Vector3Int>
                {
                    new Vector3Int(-7, 2, 0), new Vector3Int(0, 3, 0), new Vector3Int(5, -2, 0),
                    new Vector3Int(9, 1, 0)
                };
                layout.decorativeCrystals = new List<Vector3Int>
                {
                    new Vector3Int(-3, -4, 0), new Vector3Int(8, -1, 0)
                };
                break;

            case 10:
            default:
                layout.levelNumber = 10;
                layout.displayName = "Màn 10: Ngai Rồng (BOSS)";
                layout.subtitle = "Màn 10 · Đỉnh núi băng · tuyết · 2 đường (35+28 bước) · 12 ô tháp - Đại Trùm Rồng Lửa";
                layout.theme = "Snow";
                layout.startingGold = 850;
                layout.startingLives = 20;
                layout.maxWaves = 3;
                layout.hpScalePerWave = 0.30f;
                layout.missionType = MissionType.KillBosses;
                layout.missionTargetValue = 1f;
                layout.bossType = "Dragon";
                layout.spawnAName = "Spawn A";
                layout.spawnBName = "Spawn B";

                layout.path1 = new List<Vector3Int>
                {
                    new Vector3Int(-11, 2, 0),
                    new Vector3Int(-7, 2, 0),
                    new Vector3Int(-4, 4, 0),
                    new Vector3Int(2, 4, 0),
                    new Vector3Int(6, 2, 0),
                    new Vector3Int(10, 3, 0)
                };
                layout.path2 = new List<Vector3Int>
                {
                    new Vector3Int(-9, -4, 0),
                    new Vector3Int(-4, -2, 0),
                    new Vector3Int(0, 0, 0),
                    new Vector3Int(6, 2, 0),
                    new Vector3Int(10, 3, 0)
                };

                layout.plots = new List<Vector3Int>
                {
                    new Vector3Int(-8, 3, 0),
                    new Vector3Int(-3, 5, 0),
                    new Vector3Int(1, 5, 0),
                    new Vector3Int(7, 3, 0),
                    new Vector3Int(-6, -3, 0),
                    new Vector3Int(-2, -1, 0),
                    new Vector3Int(3, 1, 0),
                    new Vector3Int(8, 2, 0),
                    new Vector3Int(-1, 2, 0),
                    new Vector3Int(4, 3, 0),
                    new Vector3Int(0, -1, 0),
                    new Vector3Int(5, 0, 0)
                };

                layout.decorativeTrees = new List<Vector3Int>
                {
                    new Vector3Int(-12, 1, 0), new Vector3Int(-5, 6, 0), new Vector3Int(3, 6, 0),
                    new Vector3Int(11, 5, 0), new Vector3Int(-10, -5, 0), new Vector3Int(5, -4, 0)
                };
                layout.decorativeRocks = new List<Vector3Int>
                {
                    new Vector3Int(-9, 1, 0), new Vector3Int(-2, 1, 0), new Vector3Int(4, -1, 0),
                    new Vector3Int(8, 0, 0)
                };
                layout.decorativeCrystals = new List<Vector3Int>
                {
                    new Vector3Int(-8, 4, 0), new Vector3Int(9, 2, 0), new Vector3Int(10, -3, 0)
                };
                break;
        }

        return layout;
    }
}
