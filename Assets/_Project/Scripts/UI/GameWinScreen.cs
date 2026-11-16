using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Màn hình CHIẾN THẮNG (YOU WIN) + đánh giá sao (1-3).
/// - Tự dựng giao diện lúc chạy bằng UIKit + sprite UI có sẵn, nên KHÔNG cần gán GameWinPanel/cupsText trong scene.
///   (Scene do TDSceneGenerator sinh không có các tham chiếu này - đó là lý do trước đây dialog không hiện.)
/// - Dùng Canvas riêng (sortingOrder cao) nên không bị HUD/panel khác che.
/// - Chạy được khi Time.timeScale = 0 (hiệu ứng dùng unscaledDeltaTime).
/// </summary>
public class GameWinScreen : MonoBehaviour
{
    public static GameWinScreen main;

    [SerializeField] private GameObject GameWinPanel;       // tuỳ chọn: panel dựng tay (scene cũ)
    [SerializeField] private TextMeshProUGUI cupsText;      // tuỳ chọn: text dựng tay (scene cũ)

    private const int CanvasOrder = 900;
    private const string CanvasName = "Canvas_GameWin";

    private bool shown;
    private int earnedStars;
    private RectTransform card;
    private readonly Image[] starFull = new Image[3];
    private static Sprite fallbackStar;

    private void Awake()
    {
        main = this;
    }

    private void Start()
    {
        if (LevelManager.main != null)
        {
            LevelManager.main.OnGameWin += ToggleGameWinPanel;
        }

        // Chỉ ẩn panel dựng tay khi chưa thắng (WinGame có thể gọi Toggle trước khi Start chạy)
        if (!shown && GameWinPanel != null)
        {
            GameWinPanel.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (LevelManager.main != null)
        {
            LevelManager.main.OnGameWin -= ToggleGameWinPanel;
        }
        if (main == this) main = null;
    }

    /// <summary>Hiện dialog chiến thắng + sao. Gọi nhiều lần vẫn chỉ hiện một lần.</summary>
    public void ToggleGameWinPanel()
    {
        if (shown) return;
        shown = true;

        earnedStars = ResolveStars();

        if (GameWinPanel != null)
        {
            // Scene cũ có panel dựng tay: dùng lại
            GameWinPanel.SetActive(true);
            if (cupsText != null) cupsText.text = "YOU WIN!\n" + earnedStars + "/3";
            return;
        }

        BuildUI();
        StartCoroutine(RevealRoutine());
    }

    private int ResolveStars()
    {
        int stars = LevelManager.main != null ? LevelManager.main.lastCupsEarned : 0;
        if (stars <= 0 && MissionTracker.main != null)
        {
            stars = MissionTracker.main.EvaluateStarsAndSave();
        }
        if (stars <= 0)
        {
            // Dự phòng: tính theo số mạng còn lại
            int startingLives = LevelManager.main != null ? Mathf.Max(1, LevelManager.main.startingLives) : 20;
            int lives = LevelManager.main != null ? LevelManager.main.livesCount : startingLives;
            stars = lives >= startingLives ? 3 : (lives >= Mathf.Max(1, startingLives / 2) ? 2 : 1);
        }
        return Mathf.Clamp(stars, 1, 3);
    }

    // ------------------------------------------------------------------ Dựng giao diện lúc chạy
    private void BuildUI()
    {
        Canvas canvas = UIKit.EnsureCanvas(CanvasName, CanvasOrder);

        RectTransform root = UIKit.NewRect("GameWinPanel", canvas.transform);
        UIKit.Stretch(root);
        GameWinPanel = root.gameObject;

        // Lớp tối phủ toàn màn hình (chặn bấm xuống game phía sau)
        UIKit.Solid(root, "Dim", new Color(0.02f, 0.03f, 0.07f, 0.78f), true);

        // Thẻ chính
        Vector2 mid = new Vector2(0.5f, 0.5f);
        Image cardImg = UIKit.Img(root, "Card", "panel_purple_9slice", mid, mid, new Vector2(0f, -20f), new Vector2(900f, 640f), true, true);
        if (cardImg.sprite == null) cardImg.color = new Color(0.18f, 0.14f, 0.34f, 0.98f);
        card = cardImg.rectTransform;
        Transform c = card;

        // Ruy-băng tiêu đề
        Image ribbon = UIKit.Img(c, "Ribbon", "ribbon_title_green", mid, mid, new Vector2(0f, 338f), new Vector2(700f, 170f));
        if (ribbon.sprite == null) ribbon.color = new Color(0.2f, 0.62f, 0.34f, 1f);
        UIKit.Text(ribbon.transform, "Title", "YOU WIN!", 72f, Color.white, mid, mid, new Vector2(0f, 8f), new Vector2(680f, 120f));

        // Tên màn
        UIKit.Text(c, "LevelName", LevelTitle(), 34f, UIKit.Gold, mid, mid, new Vector2(0f, 215f), new Vector2(800f, 50f));

        // 3 ngôi sao: nền (rỗng) + sao sáng cho số sao đạt được
        float[] xs = { -190f, 0f, 190f };
        float[] ys = { 65f, 88f, 65f };
        float[] sz = { 150f, 190f, 150f };
        for (int i = 0; i < 3; i++)
        {
            MakeStar(c, "StarEmpty" + i, false, new Vector2(xs[i], ys[i]), sz[i]);
            Image full = MakeStar(c, "StarFull" + i, true, new Vector2(xs[i], ys[i]), sz[i]);
            full.gameObject.SetActive(i < earnedStars);
            starFull[i] = full;
        }

        UIKit.Text(c, "Result", earnedStars + "/3 SAO", 44f, UIKit.Gold, mid, mid, new Vector2(0f, -45f), new Vector2(600f, 60f));
        UIKit.Text(c, "Summary", BuildSummary(), 26f, Color.white, mid, mid, new Vector2(0f, -140f), new Vector2(820f, 120f),
            TextAlignmentOptions.Center, FontStyles.Normal);

        // Nút
        string next = NextSceneName();
        int count = next != null ? 3 : 2;
        float w = 250f;
        float gap = 20f;
        float x0 = -((count - 1) * (w + gap)) * 0.5f;
        int k = 0;
        Vector2 btnSize = new Vector2(w, 88f);
        if (next != null)
        {
            MakeButton(c, "NextBtn", "MÀN TIẾP", "btn_wide_green", "btn_wide_green_pressed", new Vector2(x0 + k * (w + gap), -255f), btnSize, OnNextLevelButtonPressed);
            k++;
        }
        MakeButton(c, "ReplayBtn", "CHƠI LẠI", "btn_wide_orange", "btn_wide_orange_pressed", new Vector2(x0 + k * (w + gap), -255f), btnSize, OnReplayButtonPressed);
        k++;
        MakeButton(c, "MenuBtn", "MENU", "btn_wide_lavender", "btn_wide_lavender_pressed", new Vector2(x0 + k * (w + gap), -255f), btnSize, OnMainMenuButtonPressed);
    }

    private static Image MakeStar(Transform parent, string name, bool full, Vector2 pos, float size)
    {
        Vector2 mid = new Vector2(0.5f, 0.5f);
        Image img = UIKit.Img(parent, name, full ? "star_full" : "star_empty", mid, mid, pos, new Vector2(size, size));
        if (img.sprite == null)
        {
            img.sprite = FallbackStar();
            img.preserveAspect = true;
            img.color = full ? new Color(1f, 0.82f, 0.18f, 1f) : new Color(0.22f, 0.22f, 0.3f, 0.9f);
        }
        return img;
    }

    private static void MakeButton(Transform parent, string name, string label, string spriteKey, string pressedKey,
        Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        Vector2 mid = new Vector2(0.5f, 0.5f);
        Image img = UIKit.Img(parent, name, spriteKey, mid, mid, pos, size, true, true);
        if (img.sprite == null) img.color = new Color(0.3f, 0.35f, 0.55f, 1f);
        UIKit.MakeButton(img, pressedKey, onClick);
        UIKit.AddHoverScale(img.gameObject);
        UIKit.Text(img.transform, "Label", label, 30f, Color.white, mid, mid, Vector2.zero, size);
    }

    private string LevelTitle()
    {
        LevelInfo info = FindAnyObjectByType<LevelInfo>();
        if (info != null && !string.IsNullOrEmpty(info.displayName)) return info.displayName;
        int n = LevelManager.main != null ? LevelManager.main.levelNumber : 1;
        return "Màn " + n;
    }

    private string BuildSummary()
    {
        StringBuilder sb = new StringBuilder();
        MissionTracker mt = MissionTracker.main;
        if (mt != null && mt.missionData != null)
        {
            sb.Append("<color=#77ff99>[v]</color> ").Append(mt.missionData.missionTitle).Append("  (nhiệm vụ chính)");
            List<SecondaryMissionData> subs = mt.missionData.secondaryMissions;
            if (subs != null)
            {
                for (int i = 0; i < subs.Count; i++)
                {
                    SecondaryMissionData s = subs[i];
                    if (s == null) continue;
                    sb.Append('\n');
                    sb.Append(s.isCompleted ? "<color=#77ff99>[v]</color> " : "<color=#8a8fa8>[ ]</color> ");
                    sb.Append(s.description);
                }
            }
            sb.Append('\n');
        }

        int kills = mt != null ? mt.totalEnemiesKilled : 0;
        sb.Append("Diệt ").Append(kills).Append(" quái");
        if (CastleManager.main != null)
        {
            sb.Append("  ·  Lâu đài còn ").Append(Mathf.RoundToInt(CastleManager.main.HealthPercentage * 100f)).Append("%");
        }
        return sb.ToString();
    }

    private static string NextSceneName()
    {
        string cur = SceneManager.GetActiveScene().name;
        if (!string.IsNullOrEmpty(cur) && cur.StartsWith("Level_"))
        {
            int num;
            if (int.TryParse(cur.Substring(6), out num))
            {
                string next = "Level_" + (num + 1);
                if (Application.CanStreamedLevelBeLoaded(next)) return next;
            }
        }
        return null;
    }

    // ------------------------------------------------------------------ Hiệu ứng (không phụ thuộc timeScale)
    private IEnumerator RevealRoutine()
    {
        for (int i = 0; i < starFull.Length; i++)
        {
            if (starFull[i] != null) starFull[i].rectTransform.localScale = Vector3.zero;
        }
        if (card != null)
        {
            yield return StartCoroutine(PopIn(card, 0.25f, 0.85f));
        }
        for (int i = 0; i < earnedStars && i < starFull.Length; i++)
        {
            yield return new WaitForSecondsRealtime(0.18f);
            if (starFull[i] != null) yield return StartCoroutine(PopIn(starFull[i].rectTransform, 0.42f, 0f));
        }
    }

    private static IEnumerator PopIn(RectTransform t, float duration, float from)
    {
        float e = 0f;
        while (e < duration)
        {
            if (t == null) yield break;
            e += Time.unscaledDeltaTime;
            float x = Mathf.Clamp01(e / duration) - 1f;
            float s = 1f + 2.70158f * x * x * x + 1.70158f * x * x;   // easeOutBack
            t.localScale = Vector3.one * Mathf.LerpUnclamped(from, 1f, s);
            yield return null;
        }
        if (t != null) t.localScale = Vector3.one;
    }

    // ------------------------------------------------------------------ Nút bấm
    public void OnNextLevelButtonPressed()
    {
        Time.timeScale = 1f;
        string next = NextSceneName();
        SceneManager.LoadScene(next != null ? next : "MainMenu");
    }

    public void OnReplayButtonPressed()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void OnMainMenuButtonPressed()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    // ------------------------------------------------------------------ Sao dự phòng (khi thư viện UI chưa có star_full/star_empty)
    private static Sprite FallbackStar()
    {
        if (fallbackStar != null) return fallbackStar;
        const int S = 128;
        Texture2D tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Vector2[] poly = new Vector2[10];
        float cx = S * 0.5f;
        float cy = S * 0.52f;
        float ro = S * 0.47f;
        float ri = ro * 0.40f;
        for (int i = 0; i < 10; i++)
        {
            float a = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
            float r = (i % 2 == 0) ? ro : ri;
            poly[i] = new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r);
        }

        Color32[] px = new Color32[S * S];
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                int hit = 0;
                for (int sy = 0; sy < 2; sy++)
                {
                    for (int sx = 0; sx < 2; sx++)
                    {
                        if (InPoly(poly, x + 0.25f + 0.5f * sx, y + 0.25f + 0.5f * sy)) hit++;
                    }
                }
                px[y * S + x] = new Color32(255, 255, 255, (byte)(hit * 255 / 4));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        fallbackStar = Sprite.Create(tex, new Rect(0f, 0f, S, S), new Vector2(0.5f, 0.5f), 100f);
        return fallbackStar;
    }

    private static bool InPoly(Vector2[] p, float x, float y)
    {
        bool inside = false;
        for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
        {
            if (((p[i].y > y) != (p[j].y > y)) &&
                (x < (p[j].x - p[i].x) * (y - p[i].y) / (p[j].y - p[i].y) + p[i].x))
            {
                inside = !inside;
            }
        }
        return inside;
    }
}
