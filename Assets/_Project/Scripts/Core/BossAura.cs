using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BossAura: hiệu ứng riêng cho BOSS (gắn cùng SpriteRenderer thân boss, được KRPrefabFactory cấu hình):
///  1) Lớp "khe nứt + viền sáng" phủ lên thân: lấy sprite theo tên sprite thân + "_fx" (đồng bộ animation, lật hướng)
///     và NHẤP NHÁY như dung nham/băng/rune sắp trào ra; thỉnh thoảng "bùng sáng" (flare).
///  2) Vòng hào quang animation dưới chân (dung nham / băng / rune / thịnh nộ / hư không).
///  3) Đốm lửa (hoặc tuyết) bay quanh người.
///  4) (Tuỳ chọn) Giọt dung nham rơi từ khe nứt và để lại vệt dung nham trên đường rồi mờ dần.
/// </summary>
public class BossAura : MonoBehaviour
{
    [Header("Thân boss")]
    public SpriteRenderer body;

    [Header("Khe nứt + viền sáng (tên sprite = tên sprite thân + \"_fx\")")]
    public Sprite[] fxFrames;
    public Color fxColor = Color.white;
    public float pulseSpeed = 2.2f;
    [Range(0f, 1f)] public float pulseMin = 0.6f;
    public Vector2 flareInterval = new Vector2(3f, 6f);

    [Header("Vòng hào quang dưới chân")]
    public Sprite[] ringFrames;
    public float ringFps = 8f;
    public Vector3 ringOffset = Vector3.zero;
    public float ringScale = 1f;
    public Color ringColor = new Color(1f, 1f, 1f, 0.9f);

    [Header("Đốm lửa / tuyết")]
    public Sprite emberSprite;
    public int emberCount = 10;
    public Vector2 emberVelocity = new Vector2(0f, 0.7f);
    public Color emberColor = new Color(1f, 0.7f, 0.25f, 1f);
    public float emberWidth = 0.5f;
    public float emberHeight = 0.9f;

    [Header("Dung nham nhỏ giọt (ngẫu nhiên)")]
    public bool enableDrips = false;
    public Sprite[] dripFrames;
    public Sprite[] puddleFrames;
    public Vector2 dripInterval = new Vector2(2.5f, 5f);

    private class Ember
    {
        public Transform t;
        public SpriteRenderer sr;
        public float life;
        public float maxLife;
        public Vector2 vel;
    }

    private SpriteRenderer fxSr, ringSr;
    private Dictionary<string, Sprite> fxMap;
    private readonly List<Ember> embers = new List<Ember>();
    private readonly List<GameObject> drips = new List<GameObject>();
    private float ringT, flare, nextFlare, nextDrip;
    private bool built;

    private void Awake()
    {
        if (body == null) body = GetComponent<SpriteRenderer>();
        Build();
    }

    private void OnEnable()
    {
        flare = 0f;
        nextFlare = Time.time + Random.Range(flareInterval.x, flareInterval.y);
        nextDrip = Time.time + Random.Range(dripInterval.x, dripInterval.y);
        for (int i = 0; i < embers.Count; i++) Respawn(embers[i], true);
    }

    private void OnDisable()
    {
        for (int i = 0; i < drips.Count; i++) if (drips[i] != null) Destroy(drips[i]);
        drips.Clear();
        StopAllCoroutines();
    }

    private void Build()
    {
        if (built || body == null) return;
        built = true;

        fxMap = new Dictionary<string, Sprite>();
        if (fxFrames != null)
            for (int i = 0; i < fxFrames.Length; i++)
                if (fxFrames[i] != null && !fxMap.ContainsKey(fxFrames[i].name)) fxMap.Add(fxFrames[i].name, fxFrames[i]);

        GameObject fxGo = new GameObject("Aura_FX");
        fxGo.transform.SetParent(transform, false);
        fxSr = fxGo.AddComponent<SpriteRenderer>();
        fxSr.sortingLayerID = body.sortingLayerID;

        GameObject ringGo = new GameObject("Aura_Ring");
        ringGo.transform.SetParent(transform, false);
        ringGo.transform.localPosition = ringOffset;
        ringGo.transform.localScale = Vector3.one * ringScale;
        ringSr = ringGo.AddComponent<SpriteRenderer>();
        ringSr.sortingLayerID = body.sortingLayerID;
        ringSr.color = ringColor;

        if (emberSprite != null)
        {
            for (int i = 0; i < emberCount; i++)
            {
                GameObject g = new GameObject("Ember_" + i);
                g.transform.SetParent(transform, false);
                Ember e = new Ember();
                e.t = g.transform;
                e.sr = g.AddComponent<SpriteRenderer>();
                e.sr.sprite = emberSprite;
                e.sr.sortingLayerID = body.sortingLayerID;
                embers.Add(e);
                Respawn(e, true);
            }
        }
    }

    private void Respawn(Ember e, bool randomizeAge)
    {
        e.maxLife = Random.Range(0.8f, 1.6f);
        e.life = randomizeAge ? Random.Range(0f, e.maxLife) : e.maxLife;
        e.vel = emberVelocity + new Vector2(Random.Range(-0.15f, 0.15f), Random.Range(-0.1f, 0.2f));
        float x = Random.Range(-emberWidth, emberWidth);
        float y = emberVelocity.y < 0f ? emberHeight : Random.Range(0f, emberHeight * 0.5f);
        e.t.localPosition = new Vector3(x, y, 0f);
    }

    private void LateUpdate()
    {
        if (!built || body == null) return;
        int order = body.sortingOrder;
        fxSr.sortingOrder = order + 1;
        ringSr.sortingOrder = order - 3;

        // 1) Lớp khe nứt / viền sáng: đồng bộ với sprite thân
        Sprite cur = body.sprite;
        Sprite fx;
        if (cur != null && fxMap.TryGetValue(cur.name + "_fx", out fx)) fxSr.sprite = fx; else fxSr.sprite = null;
        fxSr.flipX = body.flipX;
        fxSr.flipY = body.flipY;

        float pulse = Mathf.Lerp(pulseMin, 1f, 0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed));
        float a = Mathf.Clamp01(pulse + flare);
        Color c = Color.Lerp(fxColor, Color.white, Mathf.Clamp01(flare) * 0.6f);
        c.a = a * fxColor.a;
        fxSr.color = c;

        if (Time.time >= nextFlare)
        {
            flare = 1f;
            nextFlare = Time.time + Random.Range(flareInterval.x, flareInterval.y);
        }
        flare = Mathf.MoveTowards(flare, 0f, Time.deltaTime * 1.6f);

        // 2) Vòng dưới chân
        if (ringFrames != null && ringFrames.Length > 0)
        {
            ringT += Time.deltaTime * ringFps;
            ringSr.sprite = ringFrames[(int)ringT % ringFrames.Length];
        }

        // 3) Đốm lửa
        for (int i = 0; i < embers.Count; i++)
        {
            Ember e = embers[i];
            e.life -= Time.deltaTime;
            if (e.life <= 0f) { Respawn(e, false); continue; }
            e.t.localPosition += (Vector3)(e.vel * Time.deltaTime);
            Color ec = emberColor;
            ec.a = Mathf.Clamp01(e.life / e.maxLife) * emberColor.a;
            e.sr.color = ec;
            e.sr.sortingOrder = order + 2;
        }

        // 4) Giọt dung nham
        if (enableDrips && Time.time >= nextDrip)
        {
            nextDrip = Time.time + Random.Range(dripInterval.x, dripInterval.y);
            flare = 1f;
            StartCoroutine(DripRoutine());
        }
    }

    private IEnumerator DripRoutine()
    {
        if (dripFrames == null || dripFrames.Length == 0) yield break;

        GameObject g = new GameObject("Drip");
        g.transform.SetParent(transform, false);
        g.transform.localPosition = new Vector3(Random.Range(-emberWidth * 0.6f, emberWidth * 0.6f), emberHeight * 0.55f, 0f);
        SpriteRenderer sr = g.AddComponent<SpriteRenderer>();
        sr.sortingLayerID = body.sortingLayerID;
        sr.sortingOrder = body.sortingOrder + 2;
        drips.Add(g);

        int fall = Mathf.Max(1, dripFrames.Length - 1);
        for (int i = 0; i < fall; i++)
        {
            if (g == null) yield break;
            sr.sprite = dripFrames[i];
            g.transform.localPosition += new Vector3(0f, -emberHeight * 0.3f, 0f);
            yield return new WaitForSeconds(0.1f);
        }
        if (g == null) yield break;
        sr.sprite = dripFrames[dripFrames.Length - 1];
        Vector3 groundPos = g.transform.position;

        // vệt dung nham trên mặt đất (không gắn vào boss, tự mờ dần)
        if (puddleFrames != null && puddleFrames.Length > 0)
        {
            GameObject p = new GameObject("LavaPuddle");
            p.transform.position = new Vector3(groundPos.x, transform.position.y - 0.05f, 0f);
            p.transform.localScale = Vector3.one * Mathf.Max(0.8f, transform.lossyScale.x * 0.6f);
            AuraPuddle ap = p.AddComponent<AuraPuddle>();
            ap.frames = puddleFrames;
            ap.sortingOrder = body.sortingOrder - 4;
            ap.sortingLayerId = body.sortingLayerID;
        }
        yield return new WaitForSeconds(0.1f);
        drips.Remove(g);
        if (g != null) Destroy(g);
    }
}

/// <summary>Vệt dung nham: phát animation nở ra rồi mờ dần và tự huỷ.</summary>
public class AuraPuddle : MonoBehaviour
{
    public Sprite[] frames;
    public int sortingOrder = 0;
    public int sortingLayerId = 0;
    public float duration = 3.2f;
    private SpriteRenderer sr;
    private float t;

    private void Start()
    {
        sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sortingLayerID = sortingLayerId;
        sr.sortingOrder = sortingOrder;
    }

    private void Update()
    {
        if (sr == null || frames == null || frames.Length == 0) { Destroy(gameObject); return; }
        t += Time.deltaTime;
        int idx = Mathf.Min(frames.Length - 1, (int)(t * 6f));
        sr.sprite = frames[idx];
        Color c = Color.white;
        c.a = Mathf.Clamp01(1f - Mathf.Max(0f, t - duration * 0.55f) / (duration * 0.45f));
        sr.color = c;
        if (t >= duration) Destroy(gameObject);
    }
}
