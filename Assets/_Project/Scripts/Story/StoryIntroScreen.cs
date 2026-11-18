using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Màn hình Story Intro (Bước 1, Bảng 9 của KingOfRush_Nang_cap_chi_tiet.docx).
/// Dựng hoàn toàn bằng code lúc chạy nên không cần sửa scene hay generator.
/// - Chạm / click / Space / Enter: hiện đủ dòng đang gõ, hoặc sang trang kế; ở trang cuối thì vào màn chơi.
/// - Nút "Bỏ qua ▶▶", Esc hoặc Back: bỏ mọi trang còn lại, không hỏi xác nhận.
/// - Chạm trong 150 ms đầu sau khi sang trang bị bỏ qua (chống bấm lố).
/// - Tốc độ chữ ~40 ký tự/giây theo thời gian không co giãn; giữ Space để tua.
/// Kết thúc (đọc hết hoặc bỏ qua) gọi callback; luồng chuyển sang màn chơi + Thẻ nhiệm vụ do StoryFlow lo.
/// </summary>
public class StoryIntroScreen : MonoBehaviour
{
    public const float CharsPerSecond = 40f;
    public const float FastForwardMultiplier = 6f;
    public const float TapGuardSeconds = 0.15f;

    /// <summary>true khi đang mở (chặn mở hai lần khi bấm đúp).</summary>
    public static bool IsOpen { get; private set; }

    private static readonly Vector2 TopLeft = new Vector2(0f, 1f);
    private static readonly Vector2 TopRight = new Vector2(1f, 1f);
    private static readonly Vector2 Mid = new Vector2(0.5f, 0.5f);

    private StoryLevelData data;
    private Action<bool> onFinished;
    private int pageIndex = -1;
    private float shownAt;
    private float revealed;
    private int totalChars;
    private bool lineDone;
    private bool finished;
    private GameObject eventSystemObj;

    private Canvas canvas;
    private RectTransform root;
    private Image bg;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI chapterText;
    private TextMeshProUGUI sceneText;
    private TextMeshProUGUI speakerText;
    private TextMeshProUGUI bodyText;
    private TextMeshProUGUI initialText;
    private RectTransform portraitRt;
    private Image portraitImg;
    private RectTransform bandRt;
    private Image bandImg;
    private Image arrowImg;
    private RectTransform dotsRow;
    private readonly List<Image> dots = new List<Image>();

    /// <summary>Mở màn hình; finished(true) = người chơi bỏ qua, finished(false) = đọc hết.</summary>
    public static StoryIntroScreen Show(StoryLevelData levelData, Action<bool> finished)
    {
        GameObject go = new GameObject("StoryIntroScreen");
        StoryIntroScreen s = go.AddComponent<StoryIntroScreen>();
        s.Begin(levelData, finished);
        return s;
    }

    // ------------------------------------------------------------------ vòng đời
    private void Begin(StoryLevelData levelData, Action<bool> finishedCallback)
    {
        data = levelData;
        onFinished = finishedCallback;
        IsOpen = true;
        EnsureEventSystem();
        Build();
        BuildDots();
        ShowPage(0);
    }

    private void OnDestroy()
    {
        if (!finished) IsOpen = false;
    }

    private void Update()
    {
        if (finished) return;

        if (StoryInput.SkipPressed())
        {
            Finish(true);
            return;
        }
        if (StoryInput.ConfirmPressed()) OnAdvance();
        if (finished) return;

        if (!lineDone)
        {
            float speed = CharsPerSecond * (StoryInput.SpaceHeld() ? FastForwardMultiplier : 1f);
            revealed += speed * Time.unscaledDeltaTime;
            int n = Mathf.Min(Mathf.FloorToInt(revealed), totalChars);
            if (n != bodyText.maxVisibleCharacters) bodyText.maxVisibleCharacters = n;
            if (n >= totalChars) lineDone = true;
        }

        if (arrowImg != null)
        {
            arrowImg.enabled = lineDone;
            float pulse = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f));
            Color c = UIKit.Gold;
            c.a = pulse;
            arrowImg.color = c;
        }
    }

    // ------------------------------------------------------------------ thao tác
    private void OnAdvance()
    {
        if (finished) return;
        if (Time.unscaledTime - shownAt < TapGuardSeconds) return;   // chạm trong 150 ms đầu: bỏ qua

        if (!lineDone)
        {
            CompleteLine();
            return;
        }
        if (pageIndex >= data.pages.Count - 1)
        {
            Finish(false);   // trang cuối: vào thẳng màn chơi
            return;
        }
        ShowPage(pageIndex + 1);
    }

    private void CompleteLine()
    {
        revealed = totalChars;
        bodyText.maxVisibleCharacters = totalChars;
        lineDone = true;
    }

    private void Finish(bool skipped)
    {
        if (finished) return;
        finished = true;
        Action<bool> cb = onFinished;
        onFinished = null;
        Cleanup();
        if (cb != null) cb(skipped);
    }

    private void Cleanup()
    {
        IsOpen = false;
        if (canvas != null) Destroy(canvas.gameObject);
        if (eventSystemObj != null) Destroy(eventSystemObj);
        Destroy(gameObject);
    }

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        if (FindAnyObjectByType<EventSystem>() != null) return;
        eventSystemObj = new GameObject("EventSystem_Story", typeof(EventSystem));
        GameInput.EnsureCorrectInputModule(eventSystemObj);
    }

    // ------------------------------------------------------------------ dựng giao diện (toạ độ thiết kế 1920x1080)
    private void Build()
    {
        canvas = UIKit.EnsureCanvas("StoryCanvas", 6000);
        root = UIKit.NewRect("StoryIntroRoot", canvas.transform);
        UIKit.Stretch(root);

        // Nền + vùng chạm toàn màn hình: chạm/click ở đâu cũng là "tiếp"
        bg = UIKit.Solid(root, "Background", new Color(0.05f, 0.06f, 0.1f, 1f), true);
        bg.preserveAspect = false;
        Button tap = bg.gameObject.AddComponent<Button>();
        tap.transition = Selectable.Transition.None;
        tap.targetGraphic = bg;
        tap.onClick.AddListener(OnAdvance);

        // Dải tối trên đầu để dễ đọc tiêu đề
        RectTransform shade = UIKit.NewRect("TopShade", root);
        shade.anchorMin = new Vector2(0f, 1f);
        shade.anchorMax = new Vector2(1f, 1f);
        shade.pivot = new Vector2(0.5f, 1f);
        shade.anchoredPosition = Vector2.zero;
        shade.sizeDelta = new Vector2(0f, 260f);
        Image shadeImg = shade.gameObject.AddComponent<Image>();
        shadeImg.color = new Color(0f, 0f, 0f, 0.5f);
        shadeImg.raycastTarget = false;

        titleText = UIKit.Text(root, "Title", "", 56f, UIKit.Gold, TopLeft, TopLeft, new Vector2(64f, -36f), new Vector2(1200f, 80f), TextAlignmentOptions.TopLeft, FontStyles.Bold);
        chapterText = UIKit.Text(root, "Chapter", "", 34f, UIKit.Muted, TopLeft, TopLeft, new Vector2(68f, -116f), new Vector2(1200f, 50f), TextAlignmentOptions.TopLeft, FontStyles.Italic);
        sceneText = UIKit.Text(root, "Scene", "", 30f, UIKit.White, TopLeft, TopLeft, new Vector2(68f, -168f), new Vector2(1200f, 46f), TextAlignmentOptions.TopLeft, FontStyles.Normal);

        // Hộp thoại phía dưới (kéo giãn theo chiều ngang)
        RectTransform box = UIKit.NewRect("DialogueBox", root);
        box.anchorMin = new Vector2(0f, 0f);
        box.anchorMax = new Vector2(1f, 0f);
        box.pivot = new Vector2(0.5f, 0f);
        box.offsetMin = new Vector2(60f, 64f);
        box.offsetMax = new Vector2(-60f, 384f);
        Image boxImg = box.gameObject.AddComponent<Image>();
        boxImg.color = new Color(0.04f, 0.05f, 0.1f, 0.88f);
        boxImg.raycastTarget = false;   // để cú chạm xuyên xuống nền

        portraitRt = UIKit.Box(box, "Portrait", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(36f, 0f), new Vector2(240f, 240f));
        portraitImg = portraitRt.gameObject.AddComponent<Image>();
        portraitImg.sprite = StoryGfx.Circle();
        portraitImg.raycastTarget = false;
        portraitImg.preserveAspect = true;
        initialText = UIKit.Text(portraitRt, "Initial", "", 120f, UIKit.White, Mid, Mid, Vector2.zero, new Vector2(240f, 240f));

        bandRt = UIKit.Box(box, "NameBand", TopLeft, new Vector2(0f, 0.5f), new Vector2(330f, 0f), new Vector2(560f, 64f));
        bandImg = bandRt.gameObject.AddComponent<Image>();
        bandImg.raycastTarget = false;
        speakerText = UIKit.Text(bandRt, "Name", "", 36f, UIKit.White, Mid, Mid, Vector2.zero, new Vector2(540f, 60f));

        bodyText = UIKit.Text(box, "Body", "", 42f, UIKit.White, Mid, Mid, Vector2.zero, new Vector2(100f, 100f), TextAlignmentOptions.TopLeft, FontStyles.Normal);
        bodyText.textWrappingMode = TextWrappingModes.Normal;

        arrowImg = NewSprite(box, "Arrow", StoryGfx.TriDown(), UIKit.Gold, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-44f, 32f), new Vector2(44f, 44f));

        // Chấm chỉ trang (dưới hộp, bên trái) và gợi ý thao tác (dưới hộp, bên phải)
        dotsRow = UIKit.Box(root, "Dots", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(70f, 22f), new Vector2(10f, 18f));
        UIKit.Text(root, "Hint", "Chạm / Space / Enter: tiếp  ·  Giữ Space: tua  ·  Esc: bỏ qua", 24f, UIKit.Muted,
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-70f, 18f), new Vector2(1100f, 34f), TextAlignmentOptions.BottomRight, FontStyles.Normal);

        // Nút Bỏ qua ▶▶ (góc trên phải) - dựng cuối cùng để nằm trên cùng
        RectTransform skipRt = UIKit.Box(root, "SkipButton", TopRight, TopRight, new Vector2(-50f, -40f), new Vector2(290f, 84f));
        Image skipImg = skipRt.gameObject.AddComponent<Image>();
        skipImg.color = new Color(0f, 0f, 0f, 0.6f);
        skipImg.raycastTarget = true;
        UIKit.MakeButton(skipImg, null, delegate { Finish(true); });
        UIKit.Text(skipRt, "Label", "Bỏ qua", 36f, UIKit.White, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(170f, 60f), TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        NewSprite(skipRt, "Arrow1", StoryGfx.TriRight(), UIKit.Gold, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-18f, 0f), new Vector2(32f, 32f));
        NewSprite(skipRt, "Arrow2", StoryGfx.TriRight(), UIKit.Gold, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-54f, 0f), new Vector2(32f, 32f));
    }

    private void BuildDots()
    {
        int n = data.pages.Count;
        const float size = 18f;
        const float gap = 14f;
        dotsRow.sizeDelta = new Vector2(Mathf.Max(size, n * size + (n - 1) * gap), size);
        for (int i = 0; i < n; i++)
        {
            Image d = NewSprite(dotsRow, "Dot" + i, StoryGfx.Circle(), Color.white, TopLeft, new Vector2(0f, 0.5f),
                new Vector2(i * (size + gap), -size * 0.5f), new Vector2(size, size));
            dots.Add(d);
        }
    }

    private static Image NewSprite(Transform parent, string name, Sprite sprite, Color color, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        RectTransform rt = UIKit.Box(parent, name, anchor, pivot, pos, size);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        img.preserveAspect = true;
        return img;
    }

    // ------------------------------------------------------------------ hiển thị một trang
    private void ShowPage(int index)
    {
        pageIndex = index;
        StoryPage p = data.pages[index];
        bool last = index >= data.pages.Count - 1;
        bool narrator = p.narrator;

        shownAt = Time.unscaledTime;
        revealed = 0f;
        lineDone = false;

        string nm = string.IsNullOrEmpty(data.name) ? "" : data.name.ToUpperInvariant();
        titleText.text = data.level > 0 ? "MÀN " + data.level + " · " + nm : nm;
        chapterText.text = data.chapter ?? "";
        sceneText.text = "Cảnh " + p.sceneIndex + "/" + data.sceneCount + " · " + PlaceOf(p.scene);

        // Dải tên + chân dung (lời dẫn: dải "Dẫn truyện", không chân dung)
        speakerText.text = narrator ? "Dẫn truyện" : (p.speaker ?? "");
        bandImg.color = narrator ? new Color(0.55f, 0.42f, 0.16f, 0.96f) : new Color(0.16f, 0.30f, 0.52f, 0.96f);
        SetupPortrait(p.speaker, narrator);

        float left = narrator ? 60f : 330f;
        bandRt.anchoredPosition = new Vector2(left, 0f);
        RectTransform bodyRt = bodyText.rectTransform;
        bodyRt.anchorMin = Vector2.zero;
        bodyRt.anchorMax = Vector2.one;
        bodyRt.offsetMin = new Vector2(left, 28f);
        bodyRt.offsetMax = new Vector2(-130f, -48f);

        // Nền: ảnh trang (nếu có trong Resources) hoặc màu theo cảnh
        Sprite bgSprite = LoadBackground(index);
        if (bgSprite != null)
        {
            bg.sprite = bgSprite;
            bg.color = Color.white;
        }
        else
        {
            bg.sprite = null;
            bg.color = ScenePalette(p.sceneIndex);
        }

        // Chữ gõ dần
        bodyText.text = p.text ?? "";
        bodyText.maxVisibleCharacters = 0;
        bodyText.ForceMeshUpdate();
        totalChars = bodyText.textInfo != null ? bodyText.textInfo.characterCount : 0;
        if (totalChars <= 0) totalChars = (p.text ?? "").Length;
        if (totalChars <= 0) lineDone = true;

        // Mũi tên: ▼ ở trang chưa cuối, ▶ ở trang cuối (chỉ hiện khi đã gõ xong)
        arrowImg.sprite = last ? StoryGfx.TriRight() : StoryGfx.TriDown();
        arrowImg.enabled = false;

        for (int i = 0; i < dots.Count; i++)
        {
            Color c = i == index ? UIKit.Gold : (i < index ? new Color(1f, 1f, 1f, 0.7f) : new Color(1f, 1f, 1f, 0.25f));
            dots[i].color = c;
        }
    }

    private void SetupPortrait(string speaker, bool narrator)
    {
        portraitRt.gameObject.SetActive(!narrator);
        if (narrator) return;

        Sprite sp = FindPortrait(speaker);
        if (sp != null)
        {
            portraitImg.sprite = sp;
            portraitImg.color = Color.white;
            initialText.text = "";
        }
        else
        {
            portraitImg.sprite = StoryGfx.Circle();
            portraitImg.color = ColorFor(speaker);
            initialText.text = InitialOf(speaker);
        }
    }

    private Sprite LoadBackground(int index)
    {
        string key = data.level > 0
            ? "story/man" + data.level.ToString("00") + "/p" + (index + 1).ToString("00")
            : "story/ket_truyen_p" + (index + 1).ToString("00");
        return Resources.Load<Sprite>(key);
    }

    private Color ScenePalette(int sceneIndex)
    {
        float hue = Mathf.Repeat(data.level * 0.071f + sceneIndex * 0.05f, 1f);
        return Color.HSVToRGB(hue, 0.38f, 0.26f);
    }

    // ------------------------------------------------------------------ tiện ích chuỗi
    private static string PlaceOf(string scene)
    {
        if (string.IsNullOrEmpty(scene)) return "";
        int c = scene.IndexOf(':');
        return c >= 0 ? scene.Substring(c + 1).Trim() : scene;
    }

    private static Sprite FindPortrait(string speaker)
    {
        if (string.IsNullOrEmpty(speaker)) return null;
        Sprite sp = Resources.Load<Sprite>("story/portraits/" + Slug(speaker));
        if (sp != null) return sp;
        if (speaker.IndexOf("Kael", StringComparison.OrdinalIgnoreCase) >= 0) return UIAssets.Get("portrait_hero_knight");
        return null;
    }

    private static string Slug(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        string t = s.Trim().ToLowerInvariant().Replace('đ', 'd');
        try
        {
            string d = t.Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder(d.Length);
            for (int i = 0; i < d.Length; i++)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(d[i]) != UnicodeCategory.NonSpacingMark) sb.Append(d[i]);
            }
            t = sb.ToString();
        }
        catch (Exception)
        {
        }
        return t.Replace(' ', '_');
    }

    private static string InitialOf(string speaker)
    {
        if (string.IsNullOrEmpty(speaker)) return "?";
        string[] w = speaker.Trim().Split(' ');
        string last = w[w.Length - 1];
        return last.Length > 0 ? last.Substring(0, 1).ToUpperInvariant() : "?";
    }

    private static Color ColorFor(string speaker)
    {
        int h = 17;
        if (!string.IsNullOrEmpty(speaker))
        {
            for (int i = 0; i < speaker.Length; i++) h = unchecked(h * 31 + speaker[i]);
        }
        float hue = (Mathf.Abs(h % 360)) / 360f;
        return Color.HSVToRGB(hue, 0.45f, 0.65f);
    }
}
