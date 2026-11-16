using UnityEngine;

/// <summary>
/// SESSION_20261115_G - Vòng đời của 1 hiệu ứng chiêu thức của tướng (do HeroSkillFX tạo ra):
/// chờ trễ -> (thu phóng) -> mờ dần -> tự huỷ. Chạy với LineRenderer và SpriteRenderer, không cần asset.
/// </summary>
public class HeroFxAnim : MonoBehaviour
{
    public float delay = 0f;
    public float duration = 0.4f;
    [Range(0f, 0.95f)] public float holdFraction = 0f;   // phần đầu vòng đời giữ nguyên độ sáng
    public bool animateScale = false;
    public float scaleFrom = 1f;
    public float scaleTo = 1f;
    public Transform follow;                              // nếu có: bám theo mục tiêu (khiên Paladin)

    private float elapsed;
    private bool prepared;
    private bool shown;
    private Vector3 baseScale = Vector3.one;
    private SpriteRenderer[] sprites;
    private LineRenderer[] lines;
    private Color[] spriteBase;
    private Color[] lineStart;
    private Color[] lineEnd;

    /// <summary>Gọi sau khi gán xong các trường: cache màu gốc, ẩn hiệu ứng nếu còn phải chờ trễ.</summary>
    public void Prepare()
    {
        if (prepared) return;
        prepared = true;
        baseScale = transform.localScale;
        sprites = GetComponentsInChildren<SpriteRenderer>(true);
        lines = GetComponentsInChildren<LineRenderer>(true);
        spriteBase = new Color[sprites.Length];
        lineStart = new Color[lines.Length];
        lineEnd = new Color[lines.Length];
        for (int i = 0; i < sprites.Length; i++) spriteBase[i] = sprites[i].color;
        for (int i = 0; i < lines.Length; i++)
        {
            lineStart[i] = lines[i].startColor;
            lineEnd[i] = lines[i].endColor;
        }
        shown = delay <= 0f;
        SetVisible(shown);
        Apply(0f);
    }

    private void SetVisible(bool v)
    {
        for (int i = 0; i < sprites.Length; i++) if (sprites[i] != null) sprites[i].enabled = v;
        for (int i = 0; i < lines.Length; i++) if (lines[i] != null) lines[i].enabled = v;
    }

    private void Apply(float t)
    {
        float a = 1f;
        if (t > holdFraction) a = 1f - (t - holdFraction) / Mathf.Max(0.0001f, 1f - holdFraction);
        a = Mathf.Clamp01(a);

        if (animateScale)
        {
            float e = 1f - (1f - t) * (1f - t);   // ease-out
            transform.localScale = baseScale * Mathf.Lerp(scaleFrom, scaleTo, e);
        }

        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] == null) continue;
            Color c = spriteBase[i];
            c.a *= a;
            sprites[i].color = c;
        }
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i] == null) continue;
            Color s = lineStart[i];
            Color e2 = lineEnd[i];
            s.a *= a;
            e2.a *= a;
            lines[i].startColor = s;
            lines[i].endColor = e2;
        }
    }

    private void Update()
    {
        if (!prepared) Prepare();

        elapsed += Time.deltaTime;
        if (follow != null)
        {
            transform.position = new Vector3(follow.position.x, follow.position.y, transform.position.z);
        }

        if (elapsed < delay) return;
        if (!shown)
        {
            shown = true;
            SetVisible(true);
        }

        float t = Mathf.Clamp01((elapsed - delay) / Mathf.Max(0.05f, duration));
        Apply(t);
        if (t >= 1f) Destroy(gameObject);
    }
}
