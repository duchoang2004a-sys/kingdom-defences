using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bước 2 (KingOfRush_Nang_cap_chi_tiet.docx, mục 5 và Bảng 29): Thẻ nhiệm vụ.
/// - Hiện đầu mỗi màn (sau Story Intro hoặc sau khi bấm Bỏ qua) và nêu rõ cả ba điều kiện sao.
/// - Chạm, click, Space hoặc Enter trên thẻ: đóng thẻ, đồng hồ đợt đầu tiên bắt đầu đếm (thẻ dừng thời gian lúc mở).
/// - Mở lại được từ menu Tạm dừng (mục "Nhiệm vụ") hoặc bằng cách chạm vào khung nhiệm vụ trên HUD: chỉ để xem, đóng thẻ thì giữ nguyên trạng thái trước đó.
/// - Bỏ qua cú chạm trong 150 mili giây đầu sau khi thẻ hiện (Bảng 9) để không lỡ đóng thẻ bằng cú chạm của màn trước.
/// </summary>
public class MissionCardUI : MonoBehaviour
{
    private const string CanvasName = "MissionCardCanvas";
    private const int CanvasOrder = 400;
    private const float IgnoreInputSeconds = 0.15f;

    private static MissionCardUI current;
    public static bool IsOpen { get { return current != null; } }

    private Action onClosed;
    private bool closing;
    private bool holdsTime;
    private float openedAt;
    private float prevTimeScale = 1f;

    // ------------------------------------------------------------------ mở thẻ
    /// <summary>Đầu màn chơi: hiện thẻ, dừng thời gian; khi đóng thẻ gọi onClosedCallback (bắt đầu đồng hồ trận).</summary>
    public static void ShowAtLevelStart(int level, Action onClosedCallback)
    {
        Open(level, true, onClosedCallback);
    }

    /// <summary>Xem lại thẻ giữa trận (menu Tạm dừng, hoặc chạm vào khung nhiệm vụ trên HUD).</summary>
    public static void ShowReview()
    {
        if (ObjectiveTracker.Instance == null) return;
        Open(ObjectiveTracker.Instance.Level, false, null);
    }

    private static void Open(int level, bool atLevelStart, Action onClosedCallback)
    {
        if (current != null) return;

        StoryLevelData sd = StoryDatabase.GetLevel(level);
        if (sd == null || sd.objectives == null)
        {
            if (onClosedCallback != null) onClosedCallback();
            return;
        }

        Canvas canvas = UIKit.EnsureCanvas(CanvasName, CanvasOrder);
        GameObject go = new GameObject("MissionCard", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        MissionCardUI ui = go.AddComponent<MissionCardUI>();
        current = ui;
        ui.onClosed = onClosedCallback;
        ui.Build(sd, atLevelStart);
    }

    // ------------------------------------------------------------------ dựng giao diện
    private void Build(StoryLevelData sd, bool atLevelStart)
    {
        openedAt = Time.unscaledTime;
        prevTimeScale = atLevelStart ? 1f : Time.timeScale;
        Time.timeScale = 0f;
        holdsTime = true;

        RectTransform root = (RectTransform)transform;
        UIKit.Stretch(root);

        // Lớp tối phủ toàn màn hình: chặn bấm xuống game, chạm vào đâu cũng đóng thẻ
        Image dim = UIKit.Solid(root, "Dim", new Color(0.02f, 0.03f, 0.07f, 0.8f), true);
        AddTapToClose(dim);

        Vector2 mid = new Vector2(0.5f, 0.5f);
        Image card = UIKit.Img(root, "Card", "panel_dark_9slice", mid, mid, new Vector2(0f, 10f), new Vector2(960f, 640f), true, true);
        if (card.sprite == null) card.color = new Color(0.12f, 0.13f, 0.24f, 0.98f);
        AddTapToClose(card);
        Transform c = card.transform;

        Image ribbon = UIKit.Img(c, "Ribbon", "ribbon_title_orange", mid, mid, new Vector2(0f, 330f), new Vector2(640f, 130f));
        if (ribbon.sprite == null) ribbon.color = new Color(0.86f, 0.5f, 0.12f, 1f);
        UIKit.Text(ribbon.transform, "Title", "NHIỆM VỤ - MÀN " + sd.level, 40f, Color.white, new Vector2(0.5f, 0.55f), mid, Vector2.zero, new Vector2(580f, 80f));

        UIKit.Text(c, "LevelName", string.IsNullOrEmpty(sd.name) ? ("Màn " + sd.level) : sd.name, 38f, UIKit.Gold, mid, mid, new Vector2(0f, 232f), new Vector2(880f, 50f));
        if (!string.IsNullOrEmpty(sd.chapter))
            UIKit.Text(c, "Chapter", sd.chapter, 26f, UIKit.Muted, mid, mid, new Vector2(0f, 192f), new Vector2(880f, 40f), TextAlignmentOptions.Center, FontStyles.Normal);

        string[] labels = { "NHIỆM VỤ CHÍNH  (SAO 1)", "MỤC TIÊU PHỤ  (SAO 2)", "MỤC TIÊU PHỤ  (SAO 3)" };
        float[] ys = { 100f, -26f, -152f };
        for (int i = 0; i < 3; i++)
        {
            BuildRow(c, i + 1, sd.objectives.Get(i + 1), labels[i], ys[i]);
        }

        // Nút đóng + gợi ý thao tác (chạm hoặc Space)
        Vector2 bottom = new Vector2(0.5f, 0f);
        string btnLabel = atLevelStart ? "BẮT ĐẦU" : "ĐÓNG";
        Button btn = UIKit.IconButton(c, "BtnStart", "btn_wide_green", "btn_wide_green_pressed", mid, mid, new Vector2(0f, -258f), new Vector2(360f, 84f), TryClose);
        UIKit.Text(btn.transform, "Label", btnLabel, 34f, Color.white, mid, mid, Vector2.zero, new Vector2(340f, 60f));
        UIKit.Text(root, "Hint", atLevelStart ? "Chạm hoặc nhấn Space để bắt đầu" : "Chạm hoặc nhấn Space để đóng", 26f, UIKit.Muted,
            bottom, bottom, new Vector2(0f, 70f), new Vector2(900f, 40f), TextAlignmentOptions.Center, FontStyles.Normal);
    }

    private void BuildRow(Transform parent, int star, ObjectiveData data, string label, float y)
    {
        Vector2 mid = new Vector2(0.5f, 0.5f);
        Vector2 leftMid = new Vector2(0f, 0.5f);

        // Trạng thái hiện tại (khi mở lại giữa trận): sáng = còn đạt được / đã đạt, xám = đã hỏng
        ObjectiveState st = null;
        if (ObjectiveTracker.Instance != null && star >= 1 && star <= 3) st = ObjectiveTracker.Instance.states[star - 1];
        bool failed = st != null && st.status == ObjectiveStatus.Failed;
        bool done = st != null && st.status == ObjectiveStatus.Done;

        Image row = UIKit.Img(parent, "Row" + star, "hud_chip_bg", mid, mid, new Vector2(0f, y), new Vector2(880f, 108f), true);
        if (row.sprite == null) row.color = new Color(0.2f, 0.22f, 0.36f, 0.9f);

        Image starImg = UIKit.Img(row.transform, "Star", "star_full", leftMid, leftMid, new Vector2(22f, 0f), new Vector2(84f, 84f));
        starImg.preserveAspect = true;
        starImg.color = failed ? new Color(0.36f, 0.36f, 0.42f, 1f) : Color.white;

        Color labelColor = star == 1 ? UIKit.Gold : UIKit.Muted;
        UIKit.Text(row.transform, "Label", label, 22f, labelColor, leftMid, leftMid, new Vector2(128f, 30f), new Vector2(560f, 30f), TextAlignmentOptions.Left);

        string text = data != null && !string.IsNullOrEmpty(data.text) ? data.text : "";
        Color textColor = failed ? new Color(0.6f, 0.62f, 0.7f, 1f) : Color.white;
        UIKit.Text(row.transform, "Text", text, 28f, textColor, leftMid, leftMid, new Vector2(128f, -8f), new Vector2(720f, 70f), TextAlignmentOptions.Left, FontStyles.Normal);

        if (done)
        {
            UIKit.Text(row.transform, "State", "ĐÃ ĐẠT", 22f, UIKit.Green, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-18f, 30f), new Vector2(160f, 30f), TextAlignmentOptions.Right);
        }
        else if (failed)
        {
            string why = st != null && !string.IsNullOrEmpty(st.failReason) ? st.failReason : "Không còn đạt được";
            UIKit.Text(row.transform, "State", "ĐÃ HỎNG: " + why, 22f, new Color(1f, 0.55f, 0.55f, 1f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-18f, 30f), new Vector2(560f, 30f), TextAlignmentOptions.Right);
        }
    }

    private void AddTapToClose(Image target)
    {
        Button b = target.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.targetGraphic = target;
        b.onClick.AddListener(TryClose);
    }

    // ------------------------------------------------------------------ đóng thẻ
    private void Update()
    {
        if (closing) return;
        if (holdsTime && Time.timeScale > 0f) Time.timeScale = 0f;   // script khác bật lại thời gian lúc vào màn thì thẻ vẫn giữ game đứng
        if (Time.unscaledTime - openedAt < IgnoreInputSeconds) return;
        if (StoryInput.ConfirmPressed() || StoryInput.SkipPressed()) Close();
    }

    private void TryClose()
    {
        if (closing) return;
        if (Time.unscaledTime - openedAt < IgnoreInputSeconds) return;
        Close();
    }

    private void Close()
    {
        if (closing) return;
        closing = true;
        if (holdsTime)
        {
            Time.timeScale = prevTimeScale;
            holdsTime = false;
        }

        Action cb = onClosed;
        onClosed = null;
        if (current == this) current = null;
        Destroy(gameObject);

        try { if (cb != null) cb(); }
        catch (Exception ex) { Debug.LogException(ex); }
    }

    private void OnDestroy()
    {
        if (current == this) current = null;
    }
}
