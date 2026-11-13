using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

/// <summary>
/// KRAssetFinder: tìm asset THEO TÊN FILE ở bất kỳ thư mục nào dưới Assets/_Project/Sprites,
/// nên generator vẫn chạy được khi bạn lỡ kéo thả sprite vào sai thư mục.
/// Kèm công cụ "Fix Sprite Folder Structure": đưa file về đúng thư mục chuẩn (theo kr_manifest.json)
/// và xoá bản sao trùng byte (an toàn - chỉ xoá khi nội dung giống hệt).
/// </summary>
public static class KRAssetFinder
{
    public const string SpritesRoot = "Assets/_Project/Sprites";

    [Serializable] public class ManifestEntry { public string n; public string f; }
    [Serializable] public class Manifest { public List<ManifestEntry> entries = new List<ManifestEntry>(); }

    private static Dictionary<string, List<string>> index;
    private static Manifest manifest;
    private static Dictionary<string, string> manifestMap;

    public static void Invalidate()
    {
        index = null;
        manifest = null;
        manifestMap = null;
    }

    // ------------------------------------------------------------------ Index
    private static void BuildIndex()
    {
        index = new Dictionary<string, List<string>>();
        if (!AssetDatabase.IsValidFolder("Assets/_Project")) return;
        string[] guids = AssetDatabase.FindAssets("", new[] { "Assets/_Project" });
        for (int i = 0; i < guids.Length; i++)
        {
            string p = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (AssetDatabase.IsValidFolder(p)) continue;
            string ext = Path.GetExtension(p).ToLowerInvariant();
            if (ext != ".png" && ext != ".json" && ext != ".prefab" && ext != ".asset") continue;
            string name = Path.GetFileName(p);
            List<string> list;
            if (!index.TryGetValue(name, out list)) { list = new List<string>(); index[name] = list; }
            list.Add(p);
        }
    }

    private static int Score(string path, string preferContains)
    {
        int s = 10;
        if (!string.IsNullOrEmpty(preferContains) && path.Contains(preferContains)) s = 0;
        else if (path.Contains("/Extra/")) s = 30;
        else if (path.Contains("/Resources/")) s = 20;
        return s + path.Length / 100;
    }

    /// <summary>Tìm đường dẫn asset theo tên file (vd "level_01_map.json"). preferContains: ưu tiên đường dẫn chứa chuỗi này.</summary>
    public static string Find(string fileName, string preferContains = null)
    {
        if (index == null) BuildIndex();
        List<string> list;
        if (!index.TryGetValue(fileName, out list) || list.Count == 0) return null;
        string best = list[0];
        int bestScore = Score(best, preferContains);
        for (int i = 1; i < list.Count; i++)
        {
            int sc = Score(list[i], preferContains);
            if (sc < bestScore) { bestScore = sc; best = list[i]; }
        }
        return best;
    }

    public static Sprite LoadSprite(string fileName, string preferContains = null)
    {
        string p = Find(fileName, preferContains);
        return p == null ? null : AssetDatabase.LoadAssetAtPath<Sprite>(p);
    }

    public static string LoadText(string fileName)
    {
        string p = Find(fileName);
        if (p == null) return null;
        TextAsset ta = AssetDatabase.LoadAssetAtPath<TextAsset>(p);
        return ta != null ? ta.text : null;
    }

    public static List<string> FindAllUnder(string fileName)
    {
        if (index == null) BuildIndex();
        List<string> list;
        return index.TryGetValue(fileName, out list) ? new List<string>(list) : new List<string>();
    }

    // ------------------------------------------------------------------ Manifest
    public static Manifest GetManifest()
    {
        if (manifest != null) return manifest;
        manifest = new Manifest();
        string txt = LoadText("kr_manifest.json");
        if (!string.IsNullOrEmpty(txt))
        {
            Manifest m = JsonUtility.FromJson<Manifest>(txt);
            if (m != null && m.entries != null) manifest = m;
        }
        manifestMap = new Dictionary<string, string>();
        for (int i = 0; i < manifest.entries.Count; i++) manifestMap[manifest.entries[i].n] = manifest.entries[i].f;
        return manifest;
    }

    /// <summary>Thư mục chuẩn (tương đối so với Sprites) của file, hoặc null nếu không thuộc bộ sprite của patch.</summary>
    public static string CanonicalFolder(string fileName)
    {
        GetManifest();
        string f;
        return manifestMap.TryGetValue(fileName, out f) ? f : null;
    }

    // ------------------------------------------------------------------ Fix folders
    [MenuItem("Tools/King of Rush/Fix Sprite Folder Structure (dọn thư mục sprite)", false, 40)]
    public static void FixFoldersMenu()
    {
        Invalidate();
        string report = FixFolders(false);
        if (!EditorUtility.DisplayDialog("King of Rush - Dọn thư mục sprite", report + "\n\nThực hiện?", "Thực hiện", "Huỷ")) return;
        Invalidate();
        string done = FixFolders(true);
        Invalidate();
        Debug.Log("[King of Rush] " + done);
        EditorUtility.DisplayDialog("King of Rush", done, "OK");
    }

    private static string Md5(string assetPath)
    {
        try
        {
            using (MD5 md5 = MD5.Create())
            using (FileStream fs = File.OpenRead(assetPath))
                return BitConverter.ToString(md5.ComputeHash(fs));
        }
        catch { return ""; }
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace("\\", "/");
        string leaf = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    public static string FixFolders(bool apply)
    {
        Manifest m = GetManifest();
        if (m.entries.Count == 0) return "Không tìm thấy kr_manifest.json - hãy copy đủ bản patch trước.";

        int moves = 0, deletes = 0, conflicts = 0, ok = 0;
        BuildIndex();
        if (apply) AssetDatabase.StartAssetEditing();
        try
        {
            for (int i = 0; i < m.entries.Count; i++)
            {
                string name = m.entries[i].n;
                string target = SpritesRoot + "/" + m.entries[i].f + "/" + name;
                List<string> found;
                if (!index.TryGetValue(name, out found)) continue;

                List<string> cand = new List<string>();
                for (int k = 0; k < found.Count; k++)
                    if (found[k].StartsWith(SpritesRoot + "/") ) cand.Add(found[k]);   // bỏ qua Resources/...
                if (cand.Count == 0) continue;

                string keep = null;
                if (cand.Contains(target)) keep = target;
                else
                {
                    keep = cand[0];
                    int bs = Score(keep, null);
                    for (int k = 1; k < cand.Count; k++) { int sc = Score(cand[k], null); if (sc < bs) { bs = sc; keep = cand[k]; } }
                    moves++;
                    if (apply)
                    {
                        EnsureFolder(SpritesRoot + "/" + m.entries[i].f);
                        string err = AssetDatabase.MoveAsset(keep, target);
                        if (!string.IsNullOrEmpty(err)) { Debug.LogWarning("[King of Rush] Không move được " + keep + ": " + err); conflicts++; continue; }
                        keep = target;
                    }
                }
                ok++;

                string keepHash = null;
                for (int k = 0; k < cand.Count; k++)
                {
                    string p = cand[k];
                    if (p == keep || p == target && keep == target) continue;
                    if (!File.Exists(p)) continue;
                    if (keepHash == null) keepHash = Md5(apply && keep == target ? target : keep);
                    if (Md5(p) == keepHash) { deletes++; if (apply) AssetDatabase.DeleteAsset(p); }
                    else conflicts++;
                }
            }
        }
        finally
        {
            if (apply) AssetDatabase.StopAssetEditing();
        }

        if (!apply)
            return "Sẽ di chuyển " + moves + " file về thư mục chuẩn, xoá " + deletes + " bản sao trùng hệt, " + conflicts + " file khác nội dung (giữ nguyên).";

        // Xoá thư mục rỗng còn sót (Extra, Maps/Maps, ...)
        DeleteEmptyFolders(SpritesRoot);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return "Đã di chuyển " + moves + " file, xoá " + deletes + " bản sao trùng, " + conflicts + " file xung đột (giữ nguyên).";
    }

    private static bool DeleteEmptyFolders(string folder)
    {
        string[] subs = AssetDatabase.GetSubFolders(folder);
        bool empty = true;
        for (int i = 0; i < subs.Length; i++)
            if (!DeleteEmptyFolders(subs[i])) empty = false;

        string abs = Path.GetFullPath(folder);
        if (Directory.Exists(abs))
        {
            foreach (string f in Directory.GetFiles(abs))
                if (!f.EndsWith(".meta")) { empty = false; break; }
        }
        if (empty && folder != SpritesRoot)
        {
            AssetDatabase.DeleteAsset(folder);
            return true;
        }
        return empty && folder != SpritesRoot ? true : false;
    }
}
