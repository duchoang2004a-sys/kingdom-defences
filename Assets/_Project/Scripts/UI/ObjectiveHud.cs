using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bước 3 (KingOfRush_Nang_cap_chi_tiet.docx, mục 5.1 và Bảng 29): khung "Nhiệm vụ" trong HUD, ngay dưới thanh trên ở góc trái.
/// - Dòng nhiệm vụ chính kèm tiến độ ("Đợt 4/8", "Còn 02:35", "Trùm 1/2") và hai sao cho mục tiêu phụ (sao 2, sao 3).
/// - Mục tiêu phụ còn đạt được: sao sáng. Đã hỏng: sao xám có dấu gạch, kèm dòng lý do trong 3 giây.
/// - Chạm vào khung để xem lại Thẻ nhiệm vụ (chỉ xem; xem đầy đủ cũng có ở menu Tạm dừng).
/// - Thay khung quyển sách cũ của MissionTracker (MissionHUD_Banner): ẩn nó đi, MissionTracker vẫn chạy bình thường.
/// </summary>
public class ObjectiveHud : MonoBehaviour
{
    private const string RootName = "ObjectiveHud";
    private const float ReasonSeconds = 3f;

    private static readonly Color StarOn = Color.white;
    private static readonly Color StarFailed = new Color(0.36f, 0.36f, 0.42f, 1f);
    private static readonly Color StrikeColor = new Color(0.95f, 0.3f, 0.3f, 1f);

    private ObjectiveTracker tracker;
    private TextMeshProUGUI mainText;
    private TextMeshProUGUI reasonText;
    private readonly Image[] stars = new Image[2];      // sao 2 và sao 3 (mục tiêu phụ)
    private readonly Image[] strikes = new Image[2];
    private float reasonUntil;
    private float nextRefresh;
    private int hideFrames;

    /// <summary>Dựng khung trên Canvas_UI. Trả về null nếu chưa có canvas (ObjectiveTracker thử lại sau).</summary>
    public static ObjectiveHud Create(ObjectiveTracker t)
    {
        if (t == null) return null;
        Canvas canvas = FindHudCanvas();
        if (canvas == null) return null;

        Transform old = canvas.transform.Find(RootName);
        if (old != null) Destroy(old.gameObject);

        Vector2 tl = new Vector2(0f, 1f);
        RectTransform root = UIKit.Box(canvas.transform, RootName, tl, tl, new Vector2(10f, -104f), new Vector2(300f, 84f));
        ObjectiveHud hud = root.gameObject.AddComponent<ObjectiveHud>();
        hud.Build(root, t);
        return hud;
    }

    /// <summary>Cùng canvas với thanh trên (Canvas_UI); không có thì lấy canvas bất kỳ.</summary>
    private static Canvas FindHudCanvas()
    {
        Canvas[] all = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name == "Canvas_UI") return all[i];
        }
        return Object.FindAnyObjectByType<Canvas>();
    }

    private void Build(RectTransform root, ObjectiveTracker t)
    {
        tracker = t;
        Vector2 tl = new Vector2(0f, 1f);
        Vector2 tr = new Vector2(1f, 1f);

        Image bg = UIKit.ImgFill(root, "Bg", "hud_chip_bg", true, true);
        if (bg.sprite == null) bg.color = new Color(0.10f, 0.13f, 0.20f, 0.9f);
        UIKit.MakeButton(bg, null, OnTap);

        TextMeshProUGUI title = UIKit.Text(root, "Title", "NHIỆM VỤ", 18f, UIKit.Gold, tl, tl, new Vector2(14f, -8f), new Vector2(160f, 24f), TextAlignmentOptions.Left);
        title.raycastTarget = false;
        mainText = UIKit.Text(root, "Main", "", 28f, Color.white, tl, tl, new Vector2(14f, -32f), new Vector2(190f, 44f), TextAlignmentOptions.Left);
        mainText.raycastTarget = false;

        for (int i = 0; i < 2; i++)
        {
            Image star = UIKit.Img(root, "Star" + (i + 2), "star_full", tr, tr, new Vector2(-(14f + (1 - i) * 52f), -20f), new Vector2(44f, 44f));
            star.preserveAspect = true;
            stars[i] = star;

            Image cut = UIKit.Solid(star.transform, "Strike", StrikeColor);
            UIKit.Place((RectTransform)cut.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(54f, 4f));
            cut.transform.localRotation = Quaternion.Euler(0f, 0f, -45f);
            cut.gameObject.SetActive(false);
            strikes[i] = cut;
        }

        reasonText = UIKit.Text(root, "Reason", "", 22f, new Color(1f, 0.6f, 0.6f, 1f), tl, tl, new Vector2(4f, -90f), new Vector2(520f, 32f), TextAlignmentOptions.Left, FontStyles.Normal);
        reasonText.raycastTarget = false;
        reasonText.gameObject.SetActive(false);

        ObjectiveTracker.Changed += Refresh;
        ObjectiveTracker.ObjectiveFailed += OnObjectiveFailed;
        Refresh();
    }

    private void OnDestroy()
    {
        ObjectiveTracker.Changed -= Refresh;
        ObjectiveTracker.ObjectiveFailed -= OnObjectiveFailed;
    }

    private void OnTap()
    {
        MissionCardUI.ShowReview();
    }

    private void Update()
    {
        if (reasonText != null && reasonText.gameObject.activeSelf && Time.unscaledTime >= reasonUntil)
            reasonText.gameObject.SetActive(false);

        if (Time.unscaledTime >= nextRefresh)
        {
            nextRefresh = Time.unscaledTime + 0.25f;
            Refresh();   // đồng hồ "Còn mm:ss" đổi theo thời gian, không có sự kiện riêng
        }

        if (hideFrames < 40)
        {
            hideFrames++;
            HideOldBanner();   // MissionTracker dựng khung cũ ở Start (sau LevelManager.Start): ẩn trong vài chục khung hình đầu
        }
    }

    private void HideOldBanner()
    {
        if (transform.parent == null) return;
        Transform oldBanner = transform.parent.Find("MissionHUD_Banner");
        if (oldBanner != null && oldBanner.gameObject.activeSelf) oldBanner.gameObject.SetActive(false);
    }

    private void Refresh()
    {
        if (tracker == null || mainText == null) return;
        mainText.text = tracker.MainProgressText();
        for (int i = 0; i < 2; i++)
        {
            ObjectiveState s = tracker.states[i + 1];
            bool failed = s != null && s.status == ObjectiveStatus.Failed;
            if (stars[i] != null) stars[i].color = failed ? StarFailed : StarOn;
            if (strikes[i] != null) strikes[i].gameObject.SetActive(failed);
        }
    }

    private void OnObjectiveFailed(ObjectiveState s)
    {
        if (s == null || reasonText == null) return;
        reasonText.text = "Mất sao " + s.star + ": " + s.failReason;
        reasonText.gameObject.SetActive(true);
        reasonUntil = Time.unscaledTime + ReasonSeconds;
        Refresh();
    }
}
