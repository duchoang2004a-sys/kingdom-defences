using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HeroSelectPopup = MÀN CHỌN TƯỚNG: 4 thẻ hero (chân dung, thanh chỉ số có icon, 2 icon kỹ năng,
/// trạng thái: đang chọn / chọn / khóa kèm số sao yêu cầu). Không dùng emoji / chữ dài.
/// API giữ nguyên: HeroSelectPopup.instance.OpenPopup(sceneSauKhiChon), ClosePopup(), RefreshCards().
/// </summary>
public class HeroSelectPopup : MonoBehaviour
{
    public static HeroSelectPopup instance;

    public GameObject popupRoot;
    public Transform cardsContainer;
    public TextMeshProUGUI totalCupsBadgeText;

    private string pendingSceneToLoad = "";
    private bool built;
    private RectTransform design;

    private static readonly Vector2 TL = new Vector2(0f, 1f);

    private void Awake()
    {
        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    public void OpenPopup(string sceneToLoadAfter = "")
    {
        pendingSceneToLoad = sceneToLoadAfter;
        if (!built) Build();
        popupRoot.SetActive(true);
        RefreshCards();
    }

    public void ClosePopup()
    {
        if (popupRoot != null) popupRoot.SetActive(false);
        if (!string.IsNullOrEmpty(pendingSceneToLoad))
        {
            string sc = pendingSceneToLoad;
            pendingSceneToLoad = "";
            UnityEngine.SceneManagement.SceneManager.LoadScene(sc);
        }
    }

    private void CancelPopup()
    {
        pendingSceneToLoad = "";
        if (popupRoot != null) popupRoot.SetActive(false);
    }

    // ------------------------------------------------------------------ helpers (toạ độ thiết kế 1280x720, gốc trên-trái)
    private Image DI(Transform parent, string name, string key, float x, float y, float w, float h, bool sliced = false, bool raycast = false)
    {
        return UIKit.Img(parent, name, key, TL, TL, new Vector2(x, -y), new Vector2(w, h), sliced, raycast);
    }

    private TextMeshProUGUI DT(Transform parent, string text, float size, Color col, float x, float y, float w, float h,
        TextAlignmentOptions al = TextAlignmentOptions.Center, FontStyles st = FontStyles.Bold)
    {
        return UIKit.Text(parent, "Txt", text, size, col, TL, TL, new Vector2(x, -y), new Vector2(w, h), al, st);
    }

    // ------------------------------------------------------------------ Build
    private void Build()
    {
        built = true;

        GameObject canvasGo = new GameObject("HeroSelectCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 450;
        CanvasScaler cs = canvasGo.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);
        cs.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        cs.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        popupRoot = canvasGo;

        UIKit.Solid(canvasGo.transform, "Dim", new Color(0.03f, 0.035f, 0.09f, 0.82f), true);

        Vector2 c = new Vector2(0.5f, 0.5f);
        design = UIKit.Box(canvasGo.transform, "Design", c, c, Vector2.zero, new Vector2(1280f, 720f));
        design.localScale = new Vector3(1.5f, 1.5f, 1f);

        DI(design, "Panel", "panel_dark_9slice", 80f, 56f, 1120f, 608f, true, true);

        Image rib = DI(design, "Ribbon", "ribbon_title_orange", 400f, 22f, 480f, 110f);
        UIKit.Text(rib.transform, "Title", "CHỌN TƯỚNG", 34f, Color.white, new Vector2(0.5f, 0.56f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(440f, 60f));

        // Tổng số sao (cúp)
        Image chip = DI(design, "CupsChip", "hud_chip_bg", 1000f, 76f, 150f, 42f, true);
        DI(chip.transform, "Star", "star_full", 12f, 6f, 30f, 30f);
        totalCupsBadgeText = UIKit.Text(chip.transform, "Val", "0", 22f, UIKit.Gold, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(50f, 0f), new Vector2(90f, 36f), TextAlignmentOptions.Left);

        UIKit.IconButton(design, "BtnClose", "btn_close", "btn_close_pressed", TL, TL, new Vector2(1140f, -70f), new Vector2(54f, 54f), CancelPopup);

        RectTransform cont = UIKit.Box(design, "Cards", TL, TL, Vector2.zero, new Vector2(1280f, 720f));
        cardsContainer = cont;

        // Nút xác nhận
        Image ok = DI(design, "BtnConfirm", "btn_wide_green", 540f, 586f, 200f, 62f, true, true);
        UIKit.MakeButton(ok, "btn_wide_green_pressed", ClosePopup);
        UIKit.Img(ok.transform, "Icon", "btn_play", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 3f), new Vector2(50f, 50f));
    }

    // ------------------------------------------------------------------ Cards
    public void RefreshCards()
    {
        if (!built || cardsContainer == null) return;

        for (int i = cardsContainer.childCount - 1; i >= 0; i--)
            Destroy(cardsContainer.GetChild(i).gameObject);

        int cups = SaveManager.instance != null ? SaveManager.instance.totalCups : 0;
        string equipped = SaveManager.instance != null ? SaveManager.instance.selectedHeroId : "Knight";
        if (totalCupsBadgeText != null) totalCupsBadgeText.text = cups.ToString();

        List<HeroData> heroes = HeroDatabase.GetAllHeroes();
        for (int i = 0; i < heroes.Count; i++)
        {
            HeroData d = heroes[i];
            bool unlocked = SaveManager.instance == null || SaveManager.instance.IsHeroUnlocked(d.heroId, d.requiredCups);
            bool isEquipped = string.Equals(equipped, d.heroId, System.StringComparison.OrdinalIgnoreCase);
            BuildCard(i, d, unlocked, isEquipped, cups);
        }
    }

    private void BuildCard(int index, HeroData d, bool unlocked, bool equipped, int cups)
    {
        float x = 108f + index * 268f;
        float y = 132f;
        string bgKey = !unlocked ? "card_hero_locked" : (equipped ? "card_hero_selected" : "card_hero_bg");
        Image card = DI(cardsContainer, "Card_" + d.heroId, bgKey, x, y, 252f, 440f, true, true);

        // Chân dung
        Image pic = DI(card.transform, "Portrait", "portrait_hero_" + d.heroId.ToLower(), 41f, 16f, 170f, 170f);
        if (!unlocked) pic.color = new Color(0.28f, 0.28f, 0.32f, 1f);

        // Tên + danh hiệu
        TextMeshProUGUI name = DT(card.transform, d.heroName, 18f, unlocked ? Color.white : UIKit.Muted, 8f, 190f, 236f, 26f);
        TextMeshProUGUI title = DT(card.transform, d.heroTitle, 13f, d.themeColor, 8f, 214f, 236f, 22f, TextAlignmentOptions.Center, FontStyles.Italic);

        // 4 thanh chỉ số có icon
        string[] icons = { "stat_hp", "stat_dmg", "stat_range", "stat_speed" };
        float[] vals = { d.maxHealth / 300f, d.attackDamage / 50f, d.attackRange / 5f, d.moveSpeed / 4.5f };
        Color[] cols = { new Color(0.5f, 1f, 0.6f), new Color(1f, 0.55f, 0.5f), new Color(0.5f, 0.85f, 1f), new Color(1f, 0.9f, 0.45f) };
        for (int s = 0; s < 4; s++)
        {
            float ry = 246f + s * 32f;
            DI(card.transform, "I" + s, icons[s], 22f, ry, 26f, 26f);
            Image bg = DI(card.transform, "Bar" + s, "bar_hp_hero_bg", 56f, ry + 2f, 170f, 22f, true);
            Image fill = UIKit.Img(bg.transform, "Fill", "bar_hp_hero_fill", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(3f, 0f), new Vector2(164f, 16f));
            fill.preserveAspect = false;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = Mathf.Clamp01(vals[s]);
            fill.color = unlocked ? cols[s] : new Color(0.5f, 0.5f, 0.55f);
        }

        // 2 kỹ năng (icon)
        Image s1 = DI(card.transform, "Skill1", "icon_skill_hero1_strike", 70f, 378f, 52f, 52f);
        Image s2 = DI(card.transform, "Skill2", "icon_skill_hero2_aura", 130f, 378f, 52f, 52f);
        if (!unlocked) { s1.color = new Color(0.4f, 0.4f, 0.4f, 1f); s2.color = new Color(0.4f, 0.4f, 0.4f, 1f); }

        // Trạng thái
        if (!unlocked)
        {
            DI(card.transform, "Lock", "icon_lock_white", 80f, 154f, 40f, 40f);
            Image chip = DI(card.transform, "ReqChip", "hud_chip_bg", 76f, 326f, 100f, 36f, true);
            DI(chip.transform, "Star", "star_full", 10f, -3f, 28f, 28f);
            UIKit.Text(chip.transform, "Req", d.requiredCups.ToString(), 20f, UIKit.Gold, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(44f, 0f), new Vector2(50f, 30f), TextAlignmentOptions.Left);
        }
        else if (equipped)
        {
            Image badge = DI(card.transform, "Equipped", "btn_check", 196f, 12f, 48f, 48f);
        }
        else
        {
            string id = d.heroId;
            Image pick = DI(card.transform, "Pick", "btn_check", 188f, 12f, 56f, 56f, false, true);
            UIKit.MakeButton(pick, "btn_check_pressed", () =>
            {
                if (SaveManager.instance != null) SaveManager.instance.SelectHero(id);
                RefreshCards();
            });
            // Bấm vào cả thẻ cũng chọn
            UIKit.MakeButton(card, null, () =>
            {
                if (SaveManager.instance != null) SaveManager.instance.SelectHero(id);
                RefreshCards();
            });
        }
    }
}
