using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Thư viện sprite UI (ScriptableObject) - nằm trong Resources nên hoạt động cả trong Editor lẫn bản Build.
/// Được tạo tự động bởi menu Tools > King of Rush > Generate 10 Map + ALL.
/// </summary>
[CreateAssetMenu(fileName = "UISpriteLibrary", menuName = "King of Rush/UI Sprite Library")]
public class UISpriteLibrary : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public string key;
        public Sprite sprite;
    }

    public List<Entry> entries = new List<Entry>();
    private Dictionary<string, Sprite> map;

    public Sprite Get(string key)
    {
        if (map == null)
        {
            map = new Dictionary<string, Sprite>();
            for (int i = 0; i < entries.Count; i++)
            {
                Entry e = entries[i];
                if (e != null && !string.IsNullOrEmpty(e.key) && e.sprite != null && !map.ContainsKey(e.key))
                    map.Add(e.key, e.sprite);
            }
        }
        Sprite s;
        map.TryGetValue(key, out s);
        return s;
    }
}

/// <summary>
/// UIAssets.Get("btn_home") -> Sprite. Chấp nhận tên file, đường dẫn đầy đủ hoặc tên cũ (alias).
/// </summary>
public static class UIAssets
{
    private static UISpriteLibrary lib;
    private static bool tried;

    private static readonly Dictionary<string, string> alias = new Dictionary<string, string>
    {
        { "icon_skill_q", "icon_skill_hero1_strike" },
        { "icon_skill_w", "icon_skill_hero2_aura" },
        { "icon_skill_e", "icon_reinforcements" },
        { "icon_meteor", "icon_spell_meteor" },
        { "icon_freeze", "icon_spell_freeze" },
        { "icon_skull", "icon_skull_kills" },
        { "coin", "icon_coin" },
        { "heart", "icon_heart" },
        { "gem", "icon_gem" },
        { "flag", "icon_wave_flag" },
    };

    public static void Reset()
    {
        lib = null;
        tried = false;
    }

    public static Sprite Get(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        key = Path.GetFileNameWithoutExtension(key);
        string mapped;
        if (alias.TryGetValue(key, out mapped)) key = mapped;

        if (lib == null && !tried)
        {
            tried = true;
            lib = Resources.Load<UISpriteLibrary>("UISpriteLibrary");
        }

        Sprite s = lib != null ? lib.Get(key) : null;

#if UNITY_EDITOR
        if (s == null) s = EditorFallback(key);
#endif
        return s;
    }

#if UNITY_EDITOR
    private static Sprite EditorFallback(string key)
    {
        string[] guids = UnityEditor.AssetDatabase.FindAssets(key + " t:Sprite", new[] { "Assets/_Project/Sprites" });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
            if (Path.GetFileNameWithoutExtension(path) == key)
                return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        return null;
    }
#endif
}
