using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Số sát thương nhảy (v2). Fix lỗi "số sát thương không biến mất, chồng chất trên màn hình":
/// - Nguyên nhân cũ: popup chỉ được animate/hủy bởi coroutine của DamageTextManager.instance, nhưng KHÔNG scene nào có
///   object này => instance == null => popup sinh ra rồi đứng im vĩnh viễn.
/// - Cách sửa: mỗi popup tự chạy vòng đời bằng component DamagePopup (Update + unscaled time, tự Destroy), không phụ thuộc scene.
///   Thêm giới hạn số popup cùng lúc, gộp chồng theo mục tiêu, reset static khi đổi scene.
/// Màu: Cung trắng · Pháo xám bạc · Pháp sư tím · Hero vàng viền đỏ · Phép/độc xanh lá.
/// </summary>
public class DamageTextManager : MonoBehaviour
{
    public static DamageTextManager instance;

    private const int MaxAlive = 28;               // số popup tối đa cùng lúc (cũ nhất bị xóa trước)
    private const float PopupLifetime = 0.7f;
    private const float StackResetTime = 0.45f;    // sau ngần này giây không trúng thêm -> stack về 0

    private struct StackInfo { public int stack; public float time; }
    private static readonly Dictionary<int, StackInfo> activeStacks = new Dictionary<int, StackInfo>();
    private static readonly List<DamagePopup> alive = new List<DamagePopup>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        activeStacks.Clear();
        alive.Clear();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    public static void SpawnDamageText(Vector3 worldPos, float damage, GameObject source, DamageType dmgType, Transform targetTransform = null)
    {
        if (damage <= 0.1f) return;

        // Dọn popup đã bị hủy + giới hạn số lượng
        for (int i = alive.Count - 1; i >= 0; i--) if (alive[i] == null) alive.RemoveAt(i);
        while (alive.Count >= MaxAlive)
        {
            if (alive[0] != null) Destroy(alive[0].gameObject);
            alive.RemoveAt(0);
        }

        Vector3 spawnPos = worldPos + new Vector3(0f, 0.32f, 0f);

        // Xếp chồng theo mục tiêu (tối đa 3 tầng rồi quay vòng), tự reset khi ngừng bị đánh
        if (targetTransform != null)
        {
            int key = targetTransform.GetInstanceID();
            StackInfo si;
            if (!activeStacks.TryGetValue(key, out si) || Time.unscaledTime - si.time > StackResetTime) si.stack = 0;
            spawnPos.y += si.stack * 0.18f;
            si.stack = (si.stack + 1) % 3;
            si.time = Time.unscaledTime;
            activeStacks[key] = si;
            if (activeStacks.Count > 80) PruneStacks();
        }

        GameObject popupObj = new GameObject("DamagePopup");
        popupObj.transform.position = spawnPos;

        TextMeshPro tm = popupObj.AddComponent<TextMeshPro>();
        tm.text = Mathf.RoundToInt(damage).ToString();
        tm.fontSize = 3.4f;                     // nhỏ hơn bản cũ (5.5) cho hợp map thu nhỏ
        tm.alignment = TextAlignmentOptions.Center;
        tm.sortingOrder = 100;
        tm.fontStyle = FontStyles.Bold;
        tm.color = GetDamageColor(source, dmgType);
        tm.outlineWidth = 0.22f;
        tm.outlineColor = IsHeroSource(source) ? new Color32(120, 20, 0, 255) : new Color32(10, 10, 10, 255);

        DamagePopup popup = popupObj.AddComponent<DamagePopup>();
        popup.Init(tm, PopupLifetime);
        alive.Add(popup);
    }

    private static void PruneStacks()
    {
        List<int> old = new List<int>();
        foreach (KeyValuePair<int, StackInfo> kv in activeStacks)
            if (Time.unscaledTime - kv.Value.time > 2f) old.Add(kv.Key);
        for (int i = 0; i < old.Count; i++) activeStacks.Remove(old[i]);
    }

    private static bool IsHeroSource(GameObject source)
    {
        return source != null && (source.GetComponent<Hero>() != null || source.GetComponent<HeroController>() != null || source.name.Contains("Hero"));
    }

    private static Color GetDamageColor(GameObject source, DamageType dmgType)
    {
        if (source != null)
        {
            string srcName = source.name.ToLower();

            if (source.GetComponent<Hero>() != null || source.GetComponent<HeroController>() != null || srcName.Contains("hero") || srcName.Contains("knight"))
                return new Color(1.0f, 0.82f, 0.18f);                       // Hero - vàng

            if (srcName.Contains("archer") || srcName.Contains("cung") || srcName.Contains("arrow"))
                return Color.white;                                         // Cung - trắng
            if (srcName.Contains("cannon") || srcName.Contains("phao") || srcName.Contains("bomb"))
                return new Color(0.85f, 0.88f, 0.92f);                      // Pháo - xám bạc
            if (srcName.Contains("mage") || srcName.Contains("phap") || srcName.Contains("ps"))
                return new Color(0.82f, 0.42f, 1.0f);                       // Pháp sư - tím
        }

        switch (dmgType)
        {
            case DamageType.Magic: return new Color(0.82f, 0.42f, 1.0f);
            case DamageType.Explosive: return new Color(0.95f, 0.55f, 0.2f);
            case DamageType.True: return new Color(0.25f, 1.0f, 0.45f);
            default: return Color.white;
        }
    }
}

/// <summary>Vòng đời 1 popup sát thương: nảy nhẹ, bay lên, mờ dần rồi TỰ HỦY (không phụ thuộc object nào khác).</summary>
public class DamagePopup : MonoBehaviour
{
    private TextMeshPro tm;
    private float duration = 0.7f;
    private float elapsed;
    private Vector3 startPos;
    private Color baseColor;

    public void Init(TextMeshPro text, float life)
    {
        tm = text;
        duration = Mathf.Max(0.2f, life);
        startPos = transform.position;
        baseColor = tm != null ? tm.color : Color.white;
        transform.localScale = Vector3.one * 0.8f;
    }

    private void Update()
    {
        elapsed += Time.unscaledDeltaTime;     // unscaled: vẫn tự hủy khi game pause / timeScale = 0
        float t = elapsed / duration;
        if (t >= 1f) { Destroy(gameObject); return; }

        transform.localScale = t < 0.2f
            ? Vector3.Lerp(Vector3.one * 0.8f, Vector3.one * 1.2f, t / 0.2f)
            : Vector3.Lerp(Vector3.one * 1.2f, Vector3.one, (t - 0.2f) / 0.8f);

        transform.position = startPos + new Vector3(0f, t * 0.5f, 0f);

        if (t > 0.55f && tm != null)
        {
            Color c = baseColor;
            c.a = Mathf.Lerp(1f, 0f, (t - 0.55f) / 0.45f);
            tm.color = c;
        }
    }
}
