using System;
using System.Collections.Generic;
using UnityEngine;

// Dữ liệu cốt truyện + điều kiện 3 sao (nguồn: KingOfRush_Nang_cap_chi_tiet.docx, Bảng 10-22).
// File dữ liệu: Assets/_Project/Resources/story_intro.json (sinh bằng gen_story_json.py từ tài liệu).

[Serializable]
public class StoryPage
{
    public string scene;      // "Cảnh 1: Biên giới phía đông"
    public int sceneIndex;    // 1..sceneCount
    public string speaker;    // "Dẫn truyện" = lời dẫn
    public bool narrator;
    public string text;
}

/// <summary>Một điều kiện sao (sao 1 = nhiệm vụ chính, sao 2/3 = mục tiêu phụ).</summary>
[Serializable]
public class ObjectiveData
{
    public string type;       // SURVIVE_WAVES, HOLD_TIME, KILL_BOSSES, NO_LEAK, ...
    public string text;       // câu hiển thị trên Thẻ nhiệm vụ (nguyên văn Bảng 22)
    public string enemy;      // id quái/boss liên quan (nếu có)
    public int count;         // số đợt / số boss / số tháp / số mạng...
    public float seconds;     // mốc thời gian (giây)
    public float percent;     // ngưỡng % máu lâu đài (0-100)
}

[Serializable]
public class LevelObjectivesData
{
    public ObjectiveData star1;
    public ObjectiveData star2;
    public ObjectiveData star3;

    public ObjectiveData Get(int star)
    {
        return star == 1 ? star1 : (star == 2 ? star2 : star3);
    }
}

[Serializable]
public class StoryLevelData
{
    public int level;         // 1..10, 0 = kết truyện
    public string name;
    public string chapter;
    public int sceneCount;
    public List<StoryPage> pages = new List<StoryPage>();
    public LevelObjectivesData objectives;
}

[Serializable]
public class StoryDatabaseData
{
    public int version;
    public List<StoryLevelData> levels = new List<StoryLevelData>();
    public StoryLevelData ending;
}

public static class StoryDatabase
{
    private static StoryDatabaseData cache;
    private static bool loaded;

    public static StoryDatabaseData Data
    {
        get { Load(); return cache; }
    }

    public static void Load()
    {
        if (loaded) return;
        loaded = true;
        TextAsset ta = Resources.Load<TextAsset>("story_intro");
        if (ta == null)
        {
            Debug.LogWarning("[Story] Thiếu Resources/story_intro.json: bỏ qua cốt truyện, Thẻ nhiệm vụ dùng dữ liệu dự phòng.");
            return;
        }
        try
        {
            cache = JsonUtility.FromJson<StoryDatabaseData>(ta.text);
        }
        catch (Exception e)
        {
            Debug.LogError("[Story] story_intro.json không đọc được: " + e.Message);
            cache = null;
        }
    }

    public static void Reload()
    {
        loaded = false;
        cache = null;
        Load();
    }

    public static StoryLevelData GetLevel(int level)
    {
        StoryDatabaseData d = Data;
        if (d == null || d.levels == null) return null;
        for (int i = 0; i < d.levels.Count; i++)
        {
            if (d.levels[i] != null && d.levels[i].level == level) return d.levels[i];
        }
        return null;
    }

    public static StoryLevelData GetEnding()
    {
        StoryDatabaseData d = Data;
        return d != null ? d.ending : null;
    }

    public static LevelObjectivesData GetObjectives(int level)
    {
        StoryLevelData l = GetLevel(level);
        return l != null ? l.objectives : null;
    }
}
