using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// TowerBuildEffect: hiệu ứng DỰNG THÁP và NÂNG CẤP.
///  Dựng: tháp ẩn đi -> các mảnh (gạch đá, bánh xe, quả pháo có ngòi, sách phép, kiếm, khiên, bông tuyết...) bay vòng cung từ xung quanh
///        vào vị trí, lấp lánh sao -> bụi + chùm sao nổ ra -> tháp "bật" lên (co giãn đàn hồi) rồi mới bắt đầu hoạt động.
///  Nâng cấp: vòng bụi + chùm sao + nảy nhẹ (gọi từ Turret/Barracks.ApplyUpgrade).
/// Các mảnh/hiệu ứng là sprite thường (không dùng pool) nên tự huỷ an toàn.
/// </summary>
public class TowerBuildEffect : MonoBehaviour
{
    public Sprite[] partSprites;
    public Sprite starSprite;
    public Sprite[] puffFrames;
    public float flyTime = 0.55f;
    public int sortingOrder = 30;

    private SpriteRenderer baseSr;
    private readonly List<SpriteRenderer> kids = new List<SpriteRenderer>();
    private Turret turret;
    private Barracks barracks;
    private Vector3 baseScale = Vector3.one;
    private bool built;
    private bool scaleCaptured;

    private void Awake()
    {
        baseSr = GetComponent<SpriteRenderer>();
        turret = GetComponent<Turret>();
        barracks = GetComponent<Barracks>();
        // Tắt hoạt động của tháp trong lúc dựng (tránh bắn khi chưa thành hình)
        if (turret != null) turret.enabled = false;
        if (barracks != null) barracks.enabled = false;
    }

    private void Start()
    {
        baseScale = transform.localScale;
        scaleCaptured = true;
        StartCoroutine(PlayBuild());
    }

    // ------------------------------------------------------------------ helpers
    private void SetVisible(bool v)
    {
        if (baseSr != null) baseSr.enabled = v;
        for (int i = 0; i < kids.Count; i++) if (kids[i] != null) kids[i].enabled = v;
    }

    private static float EaseInOut(float u) { return u * u * (3f - 2f * u); }

    private GameObject MakeSprite(string name, Sprite sp, Vector3 worldPos, float scale, Color color)
    {
        GameObject g = new GameObject(name);
        g.transform.position = worldPos;
        g.transform.localScale = Vector3.one * scale;
        SpriteRenderer sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = sp;
        sr.color = color;
        sr.sortingOrder = sortingOrder;
        return g;
    }

    private void SpawnStar(Vector3 pos, Vector2 vel, float life, float size)
    {
        if (starSprite == null) return;
        GameObject g = MakeSprite("BuildStar", starSprite, pos, size, new Color(1f, 0.95f, 0.55f, 1f));
        FxFade f = g.AddComponent<FxFade>();
        f.life = life;
        f.velocity = vel;
        f.startScale = size;
        f.endScale = 0f;
        f.spin = Random.Range(-240f, 240f);
    }

    private IEnumerator Puff(Vector3 pos, float scale)
    {
        if (puffFrames == null || puffFrames.Length == 0) yield break;
        GameObject g = MakeSprite("BuildPuff", puffFrames[0], pos, scale, new Color(1f, 1f, 1f, 0.9f));
        SpriteRenderer sr = g.GetComponent<SpriteRenderer>();
        for (int i = 0; i < puffFrames.Length; i++)
        {
            if (g == null) yield break;
            sr.sprite = puffFrames[i];
            yield return new WaitForSeconds(0.06f);
        }
        if (g != null) Destroy(g);
    }

    // ------------------------------------------------------------------ dựng tháp
    private IEnumerator PlayBuild()
    {
        kids.Clear();
        SpriteRenderer[] all = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < all.Length; i++) if (all[i] != baseSr) kids.Add(all[i]);
        SetVisible(false);

        Vector3 center = transform.position + new Vector3(0f, 0.28f, 0f);
        int n = partSprites != null ? partSprites.Length : 0;
        GameObject[] parts = new GameObject[n];
        Vector3[] from = new Vector3[n];
        Vector3[] to = new Vector3[n];
        float[] delay = new float[n];
        float[] spin = new float[n];
        float[] nextStar = new float[n];

        for (int i = 0; i < n; i++)
        {
            float ang = (i / (float)Mathf.Max(1, n)) * Mathf.PI * 2f + Random.Range(-0.4f, 0.4f);
            from[i] = center + new Vector3(Mathf.Cos(ang) * 1.1f, Mathf.Sin(ang) * 0.55f + 0.7f, 0f);
            to[i] = center + new Vector3(Random.Range(-0.12f, 0.12f), Random.Range(-0.08f, 0.14f), 0f);
            delay[i] = i * 0.12f;
            spin[i] = Random.Range(-360f, 360f);
            parts[i] = MakeSprite("BuildPart_" + i, partSprites[i], from[i], 0.25f, new Color(1f, 1f, 1f, 0f));
        }

        float total = flyTime + (n > 0 ? delay[n - 1] : 0f);
        float t = 0f;
        float puffAt = total;
        while (t < total)
        {
            t += Time.deltaTime;
            for (int i = 0; i < n; i++)
            {
                if (parts[i] == null) continue;
                float u = Mathf.Clamp01((t - delay[i]) / flyTime);
                float e = EaseInOut(u);
                Vector3 p = Vector3.Lerp(from[i], to[i], e);
                p.y += Mathf.Sin(e * Mathf.PI) * 0.4f;     // bay theo vòng cung
                parts[i].transform.position = p;
                parts[i].transform.rotation = Quaternion.Euler(0f, 0f, spin[i] * (1f - e));
                float sc = Mathf.Lerp(0.25f, 1.0f, Mathf.Min(1f, u * 2f)) * Mathf.Lerp(1f, 0.55f, Mathf.Max(0f, (u - 0.7f) / 0.3f));
                parts[i].transform.localScale = Vector3.one * sc;
                SpriteRenderer sr = parts[i].GetComponent<SpriteRenderer>();
                sr.color = new Color(1f, 1f, 1f, Mathf.Clamp01(u * 5f));

                // sao lấp lánh dọc đường bay
                if (u > 0f && u < 1f && Time.time >= nextStar[i])
                {
                    nextStar[i] = Time.time + 0.07f;
                    SpawnStar(p + (Vector3)(Random.insideUnitCircle * 0.12f), new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(0.1f, 0.5f)), 0.4f, Random.Range(0.35f, 0.7f));
                }
            }
            yield return null;
        }

        // Các mảnh khớp lại: bụi + chùm sao
        for (int i = 0; i < n; i++) if (parts[i] != null) Destroy(parts[i]);
        StartCoroutine(Puff(center + new Vector3(0f, -0.1f, 0f), 1.1f));
        for (int i = 0; i < 10; i++)
        {
            float a = i / 10f * Mathf.PI * 2f;
            SpawnStar(center, new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.7f) * Random.Range(0.8f, 1.6f), 0.55f, Random.Range(0.5f, 0.9f));
        }

        // Tháp "bật" lên: co giãn đàn hồi
        SetVisible(true);
        float pop = 0f;
        const float popTime = 0.38f;
        while (pop < popTime)
        {
            pop += Time.deltaTime;
            float u = Mathf.Clamp01(pop / popTime);
            float s = u < 0.55f ? Mathf.Lerp(0.55f, 1.16f, u / 0.55f) : Mathf.Lerp(1.16f, 1.0f, (u - 0.55f) / 0.45f);
            float sy = u < 0.55f ? Mathf.Lerp(0.45f, 1.0f, u / 0.55f) : Mathf.Lerp(1.0f, 1.0f, 1f);
            transform.localScale = new Vector3(baseScale.x * s, baseScale.y * Mathf.Lerp(sy, s, 0.6f), baseScale.z);
            yield return null;
        }
        transform.localScale = baseScale;

        built = true;
        if (turret != null) turret.enabled = true;
        if (barracks != null) barracks.enabled = true;
    }

    // ------------------------------------------------------------------ nâng cấp (gọi từ ApplyUpgrade)
    public void OnUpgraded(int level)
    {
        if (!built) return;
        StartCoroutine(UpgradePop());
    }

    private IEnumerator UpgradePop()
    {
        Vector3 center = transform.position + new Vector3(0f, 0.3f, 0f);
        StartCoroutine(Puff(center, 1.2f));
        for (int i = 0; i < 12; i++)
        {
            float a = i / 12f * Mathf.PI * 2f;
            SpawnStar(center, new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.8f) * Random.Range(0.7f, 1.4f), 0.6f, Random.Range(0.45f, 0.85f));
        }
        Vector3 target = transform.localScale;
        float t = 0f;
        while (t < 0.3f)
        {
            t += Time.deltaTime;
            float u = t / 0.3f;
            float s = 1f + 0.16f * Mathf.Sin(u * Mathf.PI);
            transform.localScale = target * s;
            yield return null;
        }
        transform.localScale = target;
    }
}

/// <summary>Sprite bay/phai mờ/thu nhỏ rồi tự huỷ (dùng cho sao lấp lánh).</summary>
public class FxFade : MonoBehaviour
{
    public float life = 0.5f;
    public Vector2 velocity;
    public float startScale = 1f;
    public float endScale = 0f;
    public float spin = 0f;
    private float t;
    private SpriteRenderer sr;

    private void Start()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        t += Time.deltaTime;
        float u = Mathf.Clamp01(t / Mathf.Max(0.01f, life));
        transform.position += (Vector3)(velocity * Time.deltaTime);
        transform.Rotate(0f, 0f, spin * Time.deltaTime);
        transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, u);
        if (sr != null) { Color c = sr.color; c.a = 1f - u * u; sr.color = c; }
        if (t >= life) Destroy(gameObject);
    }
}
