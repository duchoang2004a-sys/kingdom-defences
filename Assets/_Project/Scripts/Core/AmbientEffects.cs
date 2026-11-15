using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hiệu ứng không gian nhẹ theo chủ đề từng màn (không cần asset, tự tạo sprite 1 điểm ảnh lúc chạy):
///   snow   - tuyết rơi nhẹ, thỉnh thoảng có mảnh băng rơi nhanh (màn băng)
///   leaves - lá bay theo gió, có vệt xám mờ dần ở đuôi (màn cây cối)
///   petals - cánh hoa bay theo gió, vệt mờ dần ở đuôi
///   dust   - bụi cát thổi ngang (màn đá / sa mạc)
///   rain   - mưa xiên + trời sầm lại theo nhịp
///   dark   - cảnh tối: phủ mờ, nhịp sáng-tối chậm, mây bóng trôi
///   embers - tàn lửa bay lên (màn núi lửa)
/// TDSceneGenerator tạo object "AmbientEffects" và gán kind theo trường "ambience" trong level_XX_map.json.
/// Tối đa ~150 object, vẽ trên sorting order 40 (trên quái/tháp, dưới UI).
/// </summary>
public class AmbientEffects : MonoBehaviour
{
    public string kind = "none";
    public float halfWidth = 9.6f;
    public float halfHeight = 5.4f;

    private class Particle
    {
        public Transform t;
        public SpriteRenderer sr;
        public TrailRenderer trail;
        public float vx, vy, sway, swayAmp, phase, rot, age, life;
        public bool shard;
    }

    private enum Mode { None, Fall, Wind, Rise }

    private readonly List<Particle> items = new List<Particle>();
    private Mode mode = Mode.None;
    private Sprite px;      // đĩa mềm (hạt tuyết, lá, bụi, vệt mưa khi kéo dài, mây bóng)
    private Sprite solid;   // ô đặc phủ cả khung (cảnh tối / mưa)
    private Material trailMat;
    private SpriteRenderer dim;
    private Color dimColor = new Color(0f, 0f, 0f, 0f);
    private float dimMin, dimMax, dimSpeed;
    private bool fades;

    private void Start()
    {
        string k = string.IsNullOrEmpty(kind) ? "none" : kind.ToLowerInvariant();
        if (k == "none") return;

        px = MakeDisc();
        solid = MakeSolid();
        Shader sh = Shader.Find("Sprites/Default");
        if (sh != null) trailMat = new Material(sh);

        switch (k)
        {
            case "snow":
                mode = Mode.Fall;
                Populate(80, new Color(1f, 1f, 1f, 0.75f), new Color(0.85f, 0.93f, 1f, 0.55f), 0.04f, 0.10f, -0.15f, 0.15f, -1.0f, -0.45f, 0f, 0f, 0f);
                Populate(6, new Color(0.7f, 0.9f, 1f, 0.9f), new Color(0.85f, 0.97f, 1f, 0.9f), 0.10f, 0.16f, -0.2f, 0.0f, -4.2f, -3.2f, 0.28f, 0.05f, 0.5f);   // mảnh băng rơi nhanh
                break;
            case "leaves":
                mode = Mode.Wind;
                Populate(26, new Color(0.45f, 0.75f, 0.30f, 0.9f), new Color(0.85f, 0.75f, 0.30f, 0.9f), 0.07f, 0.12f, 1.2f, 2.4f, -0.30f, -0.05f, 0.55f, 0.05f, 0.30f);
                break;
            case "petals":
                mode = Mode.Wind;
                Populate(24, new Color(1f, 0.80f, 0.88f, 0.95f), new Color(1f, 0.95f, 0.97f, 0.95f), 0.06f, 0.10f, 1.0f, 2.0f, -0.35f, -0.05f, 0.55f, 0.04f, 0.28f);
                break;
            case "dust":
                mode = Mode.Wind;
                Populate(40, new Color(0.85f, 0.75f, 0.55f, 0.55f), new Color(0.75f, 0.68f, 0.52f, 0.45f), 0.03f, 0.07f, 2.4f, 4.2f, -0.12f, 0.12f, 0.35f, 0.04f, 0.22f);
                break;
            case "rain":
                mode = Mode.Fall;
                Populate(110, new Color(0.75f, 0.80f, 0.88f, 0.40f), new Color(0.85f, 0.88f, 0.95f, 0.30f), 0.012f, 0.016f, -1.4f, -0.9f, -9.5f, -7.5f, 0f, 0f, 0f);
                for (int i = 0; i < items.Count; i++)   // vệt mưa: kéo dài sprite theo hướng rơi
                {
                    Particle p = items[i];
                    float len = Random.Range(0.18f, 0.30f);
                    p.t.localScale = new Vector3(p.t.localScale.x, len, 1f);
                    p.t.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(p.vy, p.vx) * Mathf.Rad2Deg - 90f);
                    p.rot = 0f;
                }
                MakeDim(new Color(0.05f, 0.07f, 0.12f, 1f), 0.08f, 0.24f, 0.10f);
                break;
            case "dark":
                mode = Mode.Wind;
                Populate(10, new Color(0.45f, 0.45f, 0.55f, 0.20f), new Color(0.35f, 0.35f, 0.45f, 0.16f), 0.5f, 1.1f, 0.15f, 0.40f, -0.02f, 0.02f, 0f, 0f, 0f);   // mây bóng trôi
                MakeDim(new Color(0.02f, 0.03f, 0.08f, 1f), 0.14f, 0.36f, 0.12f);
                break;
            case "embers":
                mode = Mode.Rise;
                fades = true;
                Populate(40, new Color(1f, 0.55f, 0.15f, 0.95f), new Color(1f, 0.80f, 0.25f, 0.95f), 0.025f, 0.055f, -0.25f, 0.35f, 0.35f, 0.95f, 0.30f, 0.03f, 0.35f);
                break;
            default:
                return;
        }
    }

    private static Sprite MakeDisc()
    {
        const int N = 32;
        Texture2D tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        Color[] cols = new Color[N * N];
        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                float dx = (x + 0.5f) / N * 2f - 1f;
                float dy = (y + 0.5f) / N * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01((1f - r) * 2.2f);     // viền mềm
                cols[y * N + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        return Sprite.Create(tex, new Rect(0f, 0f, N, N), new Vector2(0.5f, 0.5f), N);   // 1 đơn vị world
    }

    private static Sprite MakeSolid()
    {
        Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        Color[] cols = new Color[16];
        for (int i = 0; i < cols.Length; i++) cols[i] = Color.white;
        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);   // 1 đơn vị world
    }

    private void Populate(int count, Color c0, Color c1, float sMin, float sMax, float vxMin, float vxMax, float vyMin, float vyMax,
                          float trailTime, float trailWidth, float trailAlpha)
    {
        for (int i = 0; i < count; i++)
        {
            GameObject go = new GameObject("Ambient_" + kind + "_" + i);
            go.transform.SetParent(transform, false);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = px;
            sr.sortingOrder = 40;
            sr.color = Color.Lerp(c0, c1, Random.value);
            float s = Random.Range(sMin, sMax);
            go.transform.localScale = new Vector3(s, s, 1f);

            Particle p = new Particle();
            p.t = go.transform;
            p.sr = sr;
            p.vx = Random.Range(vxMin, vxMax);
            p.vy = Random.Range(vyMin, vyMax);
            p.sway = Random.Range(1.2f, 2.6f);
            p.swayAmp = Random.Range(0.4f, 1.0f);
            p.phase = Random.Range(0f, 6.28f);
            p.rot = Random.Range(-90f, 90f);
            p.life = Random.Range(2.5f, 5.5f);
            p.shard = trailTime > 0f && mode == Mode.Fall;

            if (trailTime > 0f && trailMat != null)
            {
                TrailRenderer tr = go.AddComponent<TrailRenderer>();
                tr.material = trailMat;
                tr.time = trailTime;
                tr.startWidth = Mathf.Max(0.01f, trailWidth);
                tr.endWidth = 0f;
                Color g = new Color(0.62f, 0.62f, 0.66f, trailAlpha);      // vệt xám nhẹ
                tr.startColor = g;
                tr.endColor = new Color(g.r, g.g, g.b, 0f);                // mờ dần ở cuối đuôi
                tr.minVertexDistance = 0.04f;
                tr.sortingOrder = 39;
                p.trail = tr;
            }

            Place(p, true);
            items.Add(p);
        }
    }

    private void MakeDim(Color c, float aMin, float aMax, float speed)
    {
        GameObject go = new GameObject("Ambient_Dim");
        go.transform.SetParent(transform, false);
        dim = go.AddComponent<SpriteRenderer>();
        dim.sprite = solid;
        dim.sortingOrder = 45;
        go.transform.position = Vector3.zero;
        go.transform.localScale = new Vector3(halfWidth * 2.2f, halfHeight * 2.2f, 1f);
        dimColor = c;
        dimMin = aMin; dimMax = aMax; dimSpeed = speed;
        dim.color = new Color(c.r, c.g, c.b, aMin);
    }

    private void Place(Particle p, bool anywhere)
    {
        float x, y;
        if (anywhere)
        {
            x = Random.Range(-halfWidth, halfWidth);
            y = Random.Range(-halfHeight, halfHeight);
        }
        else if (mode == Mode.Fall)
        {
            x = Random.Range(-halfWidth - 1.5f, halfWidth + 0.5f);
            y = halfHeight + Random.Range(0.1f, 0.6f);
        }
        else if (mode == Mode.Rise)
        {
            x = Random.Range(-halfWidth, halfWidth);
            y = -halfHeight - Random.Range(0.1f, 0.5f);
        }
        else
        {
            x = -halfWidth - Random.Range(0.2f, 1.0f);
            y = Random.Range(-halfHeight, halfHeight);
        }
        p.t.position = new Vector3(x, y, 0f);
        p.age = 0f;
        if (fades) p.sr.color = new Color(p.sr.color.r, p.sr.color.g, p.sr.color.b, 0.95f);
        if (p.trail != null) p.trail.Clear();
    }

    private void Update()
    {
        if (mode == Mode.None) return;
        float dt = Time.deltaTime;
        float now = Time.time;

        for (int i = 0; i < items.Count; i++)
        {
            Particle p = items[i];
            Vector3 pos = p.t.position;
            float gust = 1f + 0.6f * Mathf.Sin(now * 0.5f + p.phase);   // gió giật nhẹ
            float swayX = Mathf.Sin(now * p.sway + p.phase) * 0.18f * p.swayAmp;
            float swayY = (mode == Mode.Wind) ? Mathf.Cos(now * p.sway * 0.8f + p.phase) * 0.22f * p.swayAmp : 0f;

            pos.x += (p.vx * (mode == Mode.Wind ? gust : 1f) + swayX) * dt;
            pos.y += (p.vy + swayY) * dt;
            p.age += dt;

            if (fades)
            {
                Color c = p.sr.color;
                c.a = Mathf.Clamp01(1f - p.age / p.life) * 0.95f;
                p.sr.color = c;
            }

            p.t.position = pos;
            if (p.rot != 0f) p.t.Rotate(0f, 0f, p.rot * dt);

            bool out0 = pos.y < -halfHeight - 0.7f || pos.y > halfHeight + 0.9f || pos.x > halfWidth + 1.2f || pos.x < -halfWidth - 2.2f;
            if (out0 || (fades && p.age >= p.life)) Place(p, false);
        }

        if (dim != null)
        {
            float k = 0.5f + 0.5f * Mathf.Sin(now * dimSpeed);
            dim.color = new Color(dimColor.r, dimColor.g, dimColor.b, Mathf.Lerp(dimMin, dimMax, k));
        }
    }
}
