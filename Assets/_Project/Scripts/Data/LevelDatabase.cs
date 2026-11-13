using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "LevelDatabase", menuName = "Kingdom Rush/Level Database")]
public class LevelDatabase : ScriptableObject
{
    public const string DatabasePath = "Assets/_Project/Data/LevelDatabase.asset";

    [SerializeField]
    private List<LevelEntry> levels = new List<LevelEntry>();

    public List<LevelEntry> Levels => levels;

    [System.Serializable]
    public class LevelEntry
    {
        public string sceneName;
        public string displayName;
        public int levelNumber;
        public int startingGold;
        public int startingLives;
        public LevelDefinition definition;
    }

    public LevelEntry GetLevel(int index)
    {
        if (index >= 0 && index < levels.Count) return levels[index];
        return null;
    }

    public LevelEntry GetLevelBySceneName(string sceneName)
    {
        return levels.Find(l => l.sceneName == sceneName);
    }

    public void RegisterLevel(string sceneName, string displayName, int levelNum, int startGold, int startLives, LevelDefinition def = null)
    {
        int existingIndex = levels.FindIndex(l => l.sceneName == sceneName);
        LevelEntry entry = new LevelEntry
        {
            sceneName = sceneName,
            displayName = displayName,
            levelNumber = levelNum,
            startingGold = startGold,
            startingLives = startLives,
            definition = def
        };

        if (existingIndex >= 0)
        {
            levels[existingIndex] = entry;
        }
        else
        {
            levels.Add(entry);
        }

        // Sắp xếp theo levelNumber
        levels.Sort((a, b) => a.levelNumber.CompareTo(b.levelNumber));
    }

#if UNITY_EDITOR
    public static LevelDatabase GetOrCreateDatabase()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Project/Data"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project"))
                AssetDatabase.CreateFolder("Assets", "_Project");
            AssetDatabase.CreateFolder("Assets/_Project", "Data");
        }

        LevelDatabase db = AssetDatabase.LoadAssetAtPath<LevelDatabase>(DatabasePath);
        if (db == null)
        {
            db = ScriptableObject.CreateInstance<LevelDatabase>();
            AssetDatabase.CreateAsset(db, DatabasePath);
            AssetDatabase.SaveAssets();
        }
        return db;
    }
#endif
}
