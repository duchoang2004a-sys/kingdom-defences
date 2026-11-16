using UnityEngine;

/// <summary>
/// SESSION_20261115_G - Hiệu ứng hình ảnh RIÊNG cho từng chiêu của tướng. Toàn bộ vẽ lúc chạy bằng LineRenderer / SpriteRenderer
/// (không cần asset): vệt chém + dư ảnh, sóng xung kích, mưa tên, bẫy gai, chuỗi sét, bão băng, khiên, hào quang.
/// Mỗi hiệu ứng là 1 GameObject có HeroFxAnim tự mờ dần rồi huỷ.
/// </summary>
public static class HeroSkillFX
{
    private const int LineOrder = 72;
    private const int DiscOrder = 66;

    private static Material lineMat;
    private static Sprite discSprite;

    private static Material GetLineMaterial()
    {
        if (lineMat == null)
        {
            Shader sh = Shader.Find("Sprites/Default");
            if (sh == null) return null;
            lineMat = new Material(sh);
            lineMat.name = "HeroSkillFX_Line";
        }
        return lineMat;
    }

    private static Sprite GetDiscSprite()
    {
        if (discSprite == null)
        {
            const int N = 64;
            Texture2D tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            Color[] px = new Color[N * N];
            float c = (N - 1) * 0.5f;
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;   // 0 = tâm, 1 = mép
                    float a = d >= 1f ? 0f : Mathf.Pow(1f - d, 0.8f);
                    px[y * N + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            discSprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);   // 1 đơn vị = đường kính 1
        }
        return discSprite;
    }

    private static void Launch(GameObject go, float duration, float delay, bool animScale, float scaleFrom, float scaleTo, Transform follow, float hold)
    {
        HeroFxAnim a = go.AddComponent<HeroFxAnim>();
        a.duration = Mathf.Max(0.05f, duration);
        a.delay = delay;
        a.animateScale = animScale;
        a.scaleFrom = scaleFrom;
        a.scaleTo = scaleTo;
        a.follow = follow;
        a.holdFraction = Mathf.Clamp(hold, 0f, 0.95f);
        a.Prepare();
    }

    private static LineRenderer MakeLine(string name, Vector2 pos, bool worldSpace, int points, float width, Color c0, Color c1)
    {
        Material m = GetLineMaterial();
        if (m == null) return null;
        GameObject go = new GameObject(name);
        go.transform.position = new Vector3(pos.x, pos.y, 0f);
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.sharedMaterial = m;
        lr.useWorldSpace = worldSpace;
        lr.positionCount = points;
        lr.startWidth = width;
        lr.endWidth = width;
        lr.startColor = c0;
        lr.endColor = c1;
        lr.numCapVertices = 3;
        lr.numCornerVertices = 2;
        lr.alignment = LineAlignment.View;
        lr.sortingOrder = LineOrder;
        return lr;
    }

    // ------------------------------------------------------------ khối cơ bản

    /// <summary>Vòng sóng lan ra từ tâm (vẽ trong local space, HeroFxAnim thu phóng).</summary>
    public static void Ring(Vector2 center, float radius, Color col, float width, float duration, float fromScale, float toScale, float delay, Transform follow = null, float hold = 0f)
    {
        const int N = 56;
        LineRenderer lr = MakeLine("HeroFX_Ring", center, false, N + 1, width, col, col);
        if (lr == null) return;
        for (int i = 0; i <= N; i++)
        {
            float a = (float)i / N * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
        }
        Launch(lr.gameObject, duration, delay, true, fromScale, toScale, follow, hold);
    }

    /// <summary>Đĩa sáng mềm (vùng ảnh hưởng) có thu phóng + mờ dần.</summary>
    public static void Disc(Vector2 center, float radius, Color col, float duration, float fromScale, float toScale, float delay, Transform follow = null, float hold = 0f)
    {
        GameObject go = new GameObject("HeroFX_Disc");
        go.transform.position = new Vector3(center.x, center.y, 0f);
        go.transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetDiscSprite();
        sr.color = col;
        sr.sortingOrder = DiscOrder;
        Launch(go, duration, delay, true, fromScale, toScale, follow, hold);
    }

    /// <summary>Vệt thẳng 2 điểm (world space), màu chuyển từ c0 (đầu) sang c1 (cuối).</summary>
    public static void Streak(Vector2 a, Vector2 b, float width, Color c0, Color c1, float duration, float delay)
    {
        LineRenderer lr = MakeLine("HeroFX_Streak", Vector2.zero, true, 2, width, c0, c1);
        if (lr == null) return;
        lr.SetPosition(0, new Vector3(a.x, a.y, 0f));
        lr.SetPosition(1, new Vector3(b.x, b.y, 0f));
        Launch(lr.gameObject, duration, delay, false, 1f, 1f, null, 0f);
    }

    /// <summary>Tia loé nhỏ tại điểm trúng đòn.</summary>
    public static void Spark(Vector2 pos, Color col, float size, float delay)
    {
        Disc(pos, size, col, 0.25f, 0.4f, 1.4f, delay);
    }

    /// <summary>Vòng sáng nhỏ quanh tướng khi bắt đầu tung chiêu.</summary>
    public static void CastFlash(Vector2 pos, Color col)
    {
        Ring(pos, 0.5f, col, 0.04f, 0.3f, 0.3f, 1.3f, 0f);
    }

    private static void Arc(Vector2 origin, Vector2 dir, float radius, float halfAngleDeg, float width, Color cTail, Color cHead, float duration, float delay)
    {
        const int N = 18;
        LineRenderer lr = MakeLine("HeroFX_Arc", Vector2.zero, true, N + 1, width, cTail, cHead);
        if (lr == null) return;
        float baseAng = Mathf.Atan2(dir.y, dir.x);
        float half = halfAngleDeg * Mathf.Deg2Rad;
        for (int i = 0; i <= N; i++)
        {
            float t = (float)i / N;
            float ang = baseAng - half + t * half * 2f;
            float r = radius * (0.85f + 0.15f * Mathf.Sin(t * Mathf.PI));
            lr.SetPosition(i, new Vector3(origin.x + Mathf.Cos(ang) * r, origin.y + Mathf.Sin(ang) * r, 0f));
        }
        Launch(lr.gameObject, duration, delay, false, 1f, 1f, null, 0f);
    }

    // ------------------------------------------------------------ hiệu ứng theo chiêu

    /// <summary>Knight chiêu 1: vệt chém thẳng dài + dư ảnh (nhiều vệt mờ dần, trễ dần) + cung chém bao quanh.</summary>
    public static void SlashBeam(Vector2 origin, Vector2 dir, float length, float width, Color col)
    {
        Vector2 d = dir.normalized;
        Vector2 perp = new Vector2(-d.y, d.x);

        for (int i = 0; i < 4; i++)
        {
            float off = (i - 1.5f) * 0.12f;
            float alpha = Mathf.Lerp(1f, 0.25f, i / 3f);
            Color c0 = new Color(col.r, col.g, col.b, 0f);          // đuôi (gần tướng) trong suốt
            Color c1 = new Color(col.r, col.g, col.b, alpha);       // mũi kiếm sáng
            Vector2 s = origin + perp * off + d * 0.1f;
            Vector2 e = origin + perp * off * 1.6f + d * length;
            Streak(s, e, width * (1f - i * 0.15f), c0, c1, 0.28f + i * 0.07f, i * 0.035f);
        }
        Streak(origin + d * 0.1f, origin + d * length, width * 0.35f, new Color(1f, 1f, 1f, 0f), Color.white, 0.22f, 0f);
        Arc(origin, d, length * 0.8f, 38f, width * 0.5f, new Color(col.r, col.g, col.b, 0f), new Color(1f, 1f, 0.8f, 0.9f), 0.35f, 0.02f);
    }

    /// <summary>Knight chiêu 2: sóng xung kích lan rộng (tiếng gầm) 3 lớp.</summary>
    public static void Shockwave(Vector2 center, float radius, Color col)
    {
        Disc(center, radius, new Color(col.r, col.g, col.b, 0.22f), 0.7f, 0.15f, 1f, 0f, null, 0.2f);
        Ring(center, radius, col, 0.09f, 0.7f, 0.1f, 1f, 0f);
        Ring(center, radius, Color.white, 0.04f, 0.55f, 0.1f, 0.85f, 0.05f);
        Ring(center, radius, col, 0.06f, 0.75f, 0.1f, 1f, 0.15f);
    }

    /// <summary>Ranger chiêu 1: mưa tên trút xuống vùng ngắm.</summary>
    public static void ArrowRain(Vector2 center, float radius, int count)
    {
        Ring(center, radius, new Color(1f, 0.85f, 0.4f, 0.9f), 0.05f, 0.9f, 0.3f, 1f, 0f);
        Disc(center, radius, new Color(1f, 0.8f, 0.3f, 0.18f), 0.9f, 1f, 1f, 0f, null, 0.3f);
        for (int i = 0; i < count; i++)
        {
            Vector2 p = center + Random.insideUnitCircle * radius * 0.92f;
            float delay = Random.Range(0f, 0.55f);
            Vector2 from = p + new Vector2(0.5f, 2.6f);
            Streak(from, p, 0.05f, new Color(1f, 0.95f, 0.7f, 0f), new Color(1f, 0.9f, 0.5f, 1f), 0.16f, delay);
            Spark(p, new Color(1f, 0.85f, 0.35f, 0.9f), 0.16f, delay + 0.12f);
        }
    }

    /// <summary>Ranger chiêu 2: bẫy gai mọc lên trong vùng.</summary>
    public static void ThornTrap(Vector2 center, float radius)
    {
        Ring(center, radius, new Color(0.45f, 0.85f, 0.35f, 0.95f), 0.06f, 1.4f, 0.2f, 1f, 0f);
        Disc(center, radius, new Color(0.35f, 0.7f, 0.25f, 0.22f), 1.4f, 0.3f, 1f, 0f, null, 0.5f);
        int spikes = 14;
        for (int i = 0; i < spikes; i++)
        {
            float ang = (float)i / spikes * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f);
            float r = radius * Random.Range(0.25f, 0.95f);
            Vector2 b = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
            Vector2 tip = b + new Vector2(Random.Range(-0.08f, 0.08f), Random.Range(0.35f, 0.6f));
            Streak(b, tip, 0.07f, new Color(0.35f, 0.25f, 0.1f, 1f), new Color(0.75f, 1f, 0.5f, 1f), 1.1f, Random.Range(0f, 0.25f));
        }
    }

    /// <summary>Mage chiêu 1: chuỗi sét gấp khúc đi qua các điểm (tướng -> quái 1 -> quái 2 ...).</summary>
    public static void Lightning(Vector2[] pts, Color glow, float width)
    {
        if (pts == null || pts.Length < 2) return;
        System.Collections.Generic.List<Vector3> list = new System.Collections.Generic.List<Vector3>();
        for (int i = 0; i < pts.Length - 1; i++)
        {
            Vector2 a = pts[i];
            Vector2 b = pts[i + 1];
            Vector2 d = b - a;
            float len = d.magnitude;
            Vector2 perp = len > 0.0001f ? new Vector2(-d.y, d.x) / len : Vector2.up;
            int segs = Mathf.Clamp(Mathf.RoundToInt(len / 0.35f), 2, 10);
            for (int s = 0; s < segs; s++)
            {
                float t = (float)s / segs;
                Vector2 p = a + d * t;
                if (s > 0) p += perp * Random.Range(-0.14f, 0.14f);
                list.Add(new Vector3(p.x, p.y, 0f));
            }
        }
        Vector2 last = pts[pts.Length - 1];
        list.Add(new Vector3(last.x, last.y, 0f));

        for (int pass = 0; pass < 2; pass++)
        {
            bool glowPass = pass == 0;
            Color col = glowPass ? glow : Color.white;
            LineRenderer lr = MakeLine(glowPass ? "HeroFX_BoltGlow" : "HeroFX_BoltCore", Vector2.zero, true, list.Count, glowPass ? width * 2.4f : width, col, col);
            if (lr == null) return;
            for (int i = 0; i < list.Count; i++) lr.SetPosition(i, list[i]);
            Launch(lr.gameObject, glowPass ? 0.4f : 0.3f, 0f, false, 1f, 1f, null, 0f);
        }
    }

    /// <summary>Mage chiêu 2: bão băng - vùng băng + mảnh băng toả ra.</summary>
    public static void IceStorm(Vector2 center, float radius)
    {
        Color ice = new Color(0.55f, 0.88f, 1f, 1f);
        Disc(center, radius, new Color(0.6f, 0.9f, 1f, 0.30f), 1.2f, 0.3f, 1f, 0f, null, 0.4f);
        Ring(center, radius, ice, 0.07f, 1.0f, 0.2f, 1f, 0f);
        Ring(center, radius * 0.6f, Color.white, 0.04f, 0.8f, 0.2f, 1f, 0.12f);
        for (int i = 0; i < 12; i++)
        {
            float ang = (float)i / 12f * Mathf.PI * 2f + Random.Range(-0.15f, 0.15f);
            Vector2 d = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            Streak(center + d * radius * 0.15f, center + d * radius * Random.Range(0.7f, 1f), 0.06f, new Color(1f, 1f, 1f, 0f), ice, 0.8f, Random.Range(0f, 0.2f));
        }
    }

    /// <summary>Paladin chiêu 1: bong bóng khiên bám theo tướng suốt thời gian khiên.</summary>
    public static void ShieldBubble(Transform follow, float radius, float duration)
    {
        if (follow == null) return;
        Vector2 c = follow.position;
        Disc(c, radius, new Color(1f, 0.92f, 0.5f, 0.30f), duration, 1f, 1f, 0f, follow, 0.8f);
        Ring(c, radius, new Color(1f, 0.95f, 0.6f, 0.95f), 0.05f, duration, 1f, 1f, 0f, follow, 0.8f);
    }

    /// <summary>Paladin chiêu 2: hào quang - vòng sáng + các cột sáng toả ra quanh tướng.</summary>
    public static void HolyAura(Vector2 center, float radius)
    {
        Color gold = new Color(1f, 0.9f, 0.45f, 1f);
        Disc(center, radius, new Color(1f, 0.95f, 0.6f, 0.28f), 1.1f, 0.2f, 1f, 0f, null, 0.3f);
        Ring(center, radius, gold, 0.07f, 1.0f, 0.2f, 1f, 0f);
        for (int i = 0; i < 8; i++)
        {
            float ang = (float)i / 8f * Mathf.PI * 2f;
            Vector2 b = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * radius * 0.7f;
            Streak(b, b + new Vector2(0f, 1.4f), 0.12f, new Color(1f, 1f, 0.8f, 0.9f), new Color(1f, 0.95f, 0.5f, 0f), 0.9f, i * 0.04f);
        }
    }
}
