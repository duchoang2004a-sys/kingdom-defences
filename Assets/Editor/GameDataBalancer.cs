using System.IO;
using UnityEditor;
using UnityEngine;

public class GameDataBalancerEditor : Editor
{
    [MenuItem("Tools/Kingdom Rush/Balance & Generate Enemy Assets (Tạo 8 loại Quái chuẩn 8 Level)", false, 30)]
    public static void GenerateBalancedEnemyAssets()
    {
        string dirPath = "Assets/_Project/Data/Enemies";
        if (!Directory.Exists(dirPath))
        {
            Directory.CreateDirectory(dirPath);
            AssetDatabase.Refresh();
        }

        CreateOrUpdateEnemyAsset(dirPath, "Enemy_Goblin", "Goblin", 30, 1, 15, 2.2f);
        CreateOrUpdateEnemyAsset(dirPath, "Enemy_Orc", "Orc Chiến Sĩ", 80, 1, 25, 1.5f);
        CreateOrUpdateEnemyAsset(dirPath, "Enemy_GoblinArcher", "Goblin Xạ Thủ", 40, 1, 18, 2.0f);
        CreateOrUpdateEnemyAsset(dirPath, "Enemy_Troll", "Troll Khổng Lồ", 180, 2, 40, 1.2f);
        CreateOrUpdateEnemyAsset(dirPath, "Enemy_DarkElf", "Sát Thủ Dark Elf", 90, 1, 30, 2.8f);
        CreateOrUpdateEnemyAsset(dirPath, "Enemy_Demon", "Quỷ Ác Demon", 140, 2, 35, 2.5f);
        CreateOrUpdateEnemyAsset(dirPath, "Enemy_Golem", "Golem Đá Cổ Đại", 350, 3, 60, 0.8f);
        CreateOrUpdateEnemyAsset(dirPath, "Enemy_DragonBoss", "Rồng Thần Hỏa Long (BOSS)", 1000, 5, 200, 1.0f);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("<color=green>[Kingdom Rush]</color> Đã tạo và cân bằng thành công 8 Dữ liệu Quái vật ScriptableObjects tại: " + dirPath);
        EditorUtility.DisplayDialog("Cân bằng Game Thành công!", "Đã tạo 8 tệp EnemyData ScriptableObjects tương ứng với 8 Level chuẩn đề bài!\n\nVị trí lưu: Assets/_Project/Data/Enemies/", "Tuyệt vời!");
    }

    private static void CreateOrUpdateEnemyAsset(string dir, string fileName, string name, int hp, int dmg, int reward, float speed)
    {
        string path = $"{dir}/{fileName}.asset";
        EnemyData data = AssetDatabase.LoadAssetAtPath<EnemyData>(path);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<EnemyData>();
            AssetDatabase.CreateAsset(data, path);
        }

        SerializedObject so = new SerializedObject(data);
        so.FindProperty("enemyName").stringValue = name;
        so.FindProperty("maxHealth").intValue = hp;
        so.FindProperty("damage").intValue = dmg;
        so.FindProperty("currencyReward").intValue = reward;
        so.FindProperty("speed").floatValue = speed;
        so.ApplyModifiedProperties();
    }
}