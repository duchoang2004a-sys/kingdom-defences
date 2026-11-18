using UnityEngine;

/// <summary>Sprite dựng bằng code (vòng tròn, tam giác ▼ ▶) để không phụ thuộc font hay file ảnh ngoài.</summary>
public static class StoryGfx
{
    private delegate float Coverage(float u, float v);

    private static Sprite circle;
    private static Sprite triDown;
    private static Sprite triRight;

    public static Sprite Circle()
    {
        if (circle == null)
        {
            circle = Build(64, delegate (float u, float v)
            {
                float dx = u - 0.5f;
                float dy = v - 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                return Mathf.Clamp01((0.48f - d) / 0.03f);
            });
        }
        return circle;
    }

    /// <summary>Tam giác chỉ xuống (▼).</summary>
    public static Sprite TriDown()
    {
        if (triDown == null)
        {
            triDown = Build(64, delegate (float u, float v)
            {
                float edge = 0.4f * (v - 0.15f) / 0.7f - Mathf.Abs(u - 0.5f);
                return Mathf.Clamp01(edge / 0.03f) * Mathf.Clamp01((0.85f - v) / 0.03f) * Mathf.Clamp01((v - 0.15f) / 0.03f);
            });
        }
        return triDown;
    }

    /// <summary>Tam giác chỉ phải (▶).</summary>
    public static Sprite TriRight()
    {
        if (triRight == null)
        {
            triRight = Build(64, delegate (float u, float v)
            {
                float edge = 0.4f * (0.85f - u) / 0.7f - Mathf.Abs(v - 0.5f);
                return Mathf.Clamp01(edge / 0.03f) * Mathf.Clamp01((0.85f - u) / 0.03f) * Mathf.Clamp01((u - 0.15f) / 0.03f);
            });
        }
        return triRight;
    }

    private static Sprite Build(int size, Coverage cov)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.hideFlags = HideFlags.HideAndDontSave;

        Color32[] px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float a = cov((x + 0.5f) / size, (y + 0.5f) / size);
                byte b = (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f);
                px[y * size + x] = new Color32(255, 255, 255, b);
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);

        Sprite sp = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        sp.hideFlags = HideFlags.HideAndDontSave;
        return sp;
    }
}
