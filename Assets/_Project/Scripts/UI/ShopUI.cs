using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Ví tiền của người chơi (Vàng trong shop + Gem lưu ở SaveManager).</summary>
public static class ShopWallet
{
    public static int Gold
    {
        get { return PlayerPrefs.GetInt("PlayerGold", 1250); }
        set { PlayerPrefs.SetInt("PlayerGold", Mathf.Max(0, value)); PlayerPrefs.Save(); }
    }

    public static int Gems
    {
        get { return SaveManager.instance != null ? SaveManager.instance.Gems : PlayerPrefs.GetInt("PlayerGems", 250); }
        set
        {
            if (SaveManager.instance != null) SaveManager.instance.Gems = value;
            else { PlayerPrefs.SetInt("PlayerGems", Mathf.Max(0, value)); PlayerPrefs.Save(); }
        }
    }
}

/// <summary>
/// ShopUI = MÀN CỬA HÀNG (theo demo shop_scene): top bar, tab icon bên trái, lưới 8 thẻ vật phẩm,
/// panel chi tiết bên phải, nút MUA, nút Làm mới. Tất cả bằng sprite/icon.
/// Dùng ShopUI.ShowShopScreen() / ShopUI.HideShopScreen() từ MainMenu hoặc trong màn chơi.
/// </summary>
public class ShopUI : MonoBehaviour
{
    public static ShopUI instance;

    private class StatRow { public string icon; public string value; }

    private class Item
    {
        public string id, name, desc, rarity;
        public int price;
        public bool equipment;
        public int discount;      // 0 / 20 / 30
        public string badge;      // "" / "MỚI" / "HOT"
        public StatRow[] stats;
    }

    private readonly List<Item> items = new List<Item>();
    private int selected = 4;
    private int tab = 2;

    // UI refs
    private GameObject root;
    private RectTransform design;
    private TextMeshProUGUI goldText, gemText, timerText, refreshCostText, statusText;
    private readonly List<Image> cardBgs = new List<Image>();
    private readonly List<GameObject> cardOwnedFlags = new List<GameObject>();
    private readonly List<TextMeshProUGUI> cardPriceTexts = new List<TextMeshProUGUI>();
    private readonly List<TextMeshProUGUI> cardCountTexts = new List<TextMeshProUGUI>();
    private readonly List<Image> tabImages = new List<Image>();

    private Image detailIcon, detailRarity;
    private TextMeshProUGUI detailName, detailRarityText, detailDesc, buyPriceText;
    private Image[] detailStatIcons = new Image[3];
    private TextMeshProUGUI[] detailStatNames = new TextMeshProUGUI[3];
    private TextMeshProUGUI[] detailStatValues = new TextMeshProUGUI[3];
    private Image buyBg;
    private GameObject buyCoin;

    private float prevTimeScale = 1f;
    private bool built;
    private float statusTimer;

    private const int RefreshCost = 50;

    // ------------------------------------------------------------------ Static API
    public static void ShowShopScreen()
    {
        if (instance == null)
        {
            ShopUI found = UnityEngine.Object.FindAnyObjectByType<ShopUI>();
            if (found == null)
            {
                GameObject go = new GameObject("ShopUI");
                found = go.AddComponent<ShopUI>();
            }
            instance = found;
        }
        instance.Open();
    }

    public static void HideShopScreen()
    {
        if (instance != null) instance.Close();
    }

    private void Awake()
    {
        if (instance == null) instance = this;
    }

    // ------------------------------------------------------------------ Data
    private void BuildItems()
    {
        items.Clear();
        items.Add(new Item { id = "potion_red", name = "Bình Máu", desc = "Hồi 50% máu Lâu đài khi sử dụng trong trận.", rarity = "THƯỜNG", price = 80, stats = new[] { Row("stat_hp", "+50%"), Row("stat_speed", "Tức thì") } });
        items.Add(new Item { id = "potion_blue", name = "Bình Mana", desc = "Giảm 30% thời gian hồi kỹ năng của Hero.", rarity = "THƯỜNG", price = 80, stats = new[] { Row("stat_speed", "-30%"), Row("stat_range", "10s") } });
        items.Add(new Item { id = "potion_green", name = "Bình Độc", desc = "Rải vùng độc trên đường đi của quái.", rarity = "THƯỜNG", price = 120, stats = new[] { Row("stat_dmg", "+12/s"), Row("stat_range", "Vùng") } });
        items.Add(new Item { id = "chest_gold", name = "Rương Vàng", desc = "Mở ngẫu nhiên nhận 1-3 vật phẩm.", rarity = "HIẾM", price = 300, stats = new[] { Row("stat_dmg", "1-3 món") } });
        items.Add(new Item { id = "weapon_axe", name = "Rìu Đôi", desc = "Rìu chiến hạng nặng, hợp với Barbarian.", rarity = "HIẾM", price = 450, equipment = true, stats = new[] { Row("stat_dmg", "+18"), Row("stat_speed", "+10%"), Row("stat_range", "-1.5s") } });
        items.Add(new Item { id = "weapon_hammer", name = "Búa Chiến", desc = "Búa choáng, gây choáng kẻ địch gần.", rarity = "HIẾM", price = 520, equipment = true, stats = new[] { Row("stat_dmg", "+22"), Row("stat_speed", "-5%") } });
        items.Add(new Item { id = "weapon_staff", name = "Gậy Phép", desc = "Tăng sát thương phép của Pháp Sư.", rarity = "SỬ THI", price = 600, equipment = true, stats = new[] { Row("stat_dmg", "+25"), Row("stat_range", "+0.8") } });
        items.Add(new Item { id = "weapon_sword", name = "Kiếm Lửa", desc = "Kiếm rực lửa, thiêu đốt kẻ địch.", rarity = "SỬ THI", price = 900, equipment = true, stats = new[] { Row("stat_dmg", "+35"), Row("stat_speed", "+15%") } });
        ApplyPromotions(false);
    }

    private static StatRow Row(string icon, string value) { return new StatRow { icon = icon, value = value }; }

    private void ApplyPromotions(bool reshuffle)
    {
        int seed = PlayerPrefs.GetInt("Shop_Seed", 7);
        if (reshuffle) { seed = UnityEngine.Random.Range(1, 99999); PlayerPrefs.SetInt("Shop_Seed", seed); PlayerPrefs.Save(); }
        System.Random r = new System.Random(seed);
        for (int i = 0; i < items.Count; i++) { items[i].discount = 0; items[i].badge = ""; }
        int a = r.Next(0, items.Count); int b = r.Next(0, items.Count); int c = r.Next(0, items.Count);
        items[a].discount = 20; items[b].discount = 30; items[c].badge = "HOT";
        int d = r.Next(0, items.Count); if (items[d].discount == 0) items[d].badge = "MỚI";
    }

    private int Price(Item it) { return it.discount > 0 ? Mathf.RoundToInt(it.price * (100 - it.discount) / 100f) : it.price; }
    private bool Owned(Item it) { return it.equipment && PlayerPrefs.GetInt("Shop_Owned_" + it.id, 0) == 1; }
    private int Count(Item it) { return PlayerPrefs.GetInt("Shop_Count_" + it.id, 0); }

    // ------------------------------------------------------------------ Open / Close
    private void Open()
    {
        if (!built) Build();
        RefreshAll();
        root.SetActive(true);
        prevTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
    }

    private void Close()
    {
        if (root != null) root.SetActive(false);
        Time.timeScale = prevTimeScale;
    }

    private void GoHome()
    {
        Time.timeScale = 1f;
        if (SceneManager.GetActiveScene().name != "MainMenu") SceneManager.LoadScene("MainMenu");
        else Close();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // ------------------------------------------------------------------ Helpers (toạ độ thiết kế 1280x720, gốc trên-trái)
    private static readonly Vector2 TL = new Vector2(0f, 1f);

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
        BuildItems();

        GameObject canvasGo = new GameObject("ShopCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        CanvasScaler cs = canvasGo.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);
        cs.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        cs.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        root = canvasGo;

        // Nền
        Image bg = UIKit.ImgFill(canvasGo.transform, "Background", "main_menu_decor_background_1920x1080", false, true);
        bg.preserveAspect = false;
        UIKit.Solid(canvasGo.transform, "Dim", new Color(0.055f, 0.063f, 0.13f, 0.72f), false);

        // Vùng thiết kế 1280x720 (phóng 1.5 lên 1920x1080)
        Vector2 c = new Vector2(0.5f, 0.5f);
        design = UIKit.Box(canvasGo.transform, "Design", c, c, Vector2.zero, new Vector2(1280f, 720f));
        design.localScale = new Vector3(1.5f, 1.5f, 1f);

        BuildTopBar(canvasGo.transform);
        BuildTabs();
        BuildMainPanel();
        BuildDetailPanel();
        BuildBottom();

        statusText = DT(design, "", 20f, UIKit.Green, 148f, 600f, 796f, 30f);
        root.SetActive(false);
    }

    private void BuildTopBar(Transform canvasRoot)
    {
        RectTransform bar = UIKit.NewRect("TopBar", canvasRoot);
        bar.anchorMin = new Vector2(0f, 1f); bar.anchorMax = new Vector2(1f, 1f);
        bar.offsetMin = new Vector2(0f, -96f); bar.offsetMax = Vector2.zero;
        Image bi = bar.gameObject.AddComponent<Image>();
        bi.sprite = UIAssets.Get("hud_topbar_bg");
        bi.raycastTarget = true;

        Vector2 tl = new Vector2(0f, 1f);
        Vector2 tr = new Vector2(1f, 1f);
        UIKit.IconButton(bar, "BtnBack", "btn_prev", "btn_prev_pressed", tl, tl, new Vector2(21f, -12f), new Vector2(72f, 72f), Close);

        Image rib = UIKit.Img(bar, "Ribbon", "ribbon_title_orange", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 9f), new Vector2(510f, 117f));
        UIKit.Text(rib.transform, "Title", "CỬA HÀNG", 38f, Color.white, new Vector2(0.5f, 0.56f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460f, 70f));

        // Gem + Gold (phải)
        Image gemChip = UIKit.Img(bar, "GemChip", "hud_chip_bg", tr, tr, new Vector2(-21f, -15f), new Vector2(165f, 66f), true);
        UIKit.Img(gemChip.transform, "Icon", "icon_gem", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(42f, 42f));
        gemText = UIKit.Text(gemChip.transform, "Val", "0", 28f, Color.white, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(70f, 0f), new Vector2(86f, 50f), TextAlignmentOptions.Left);

        Image goldChip = UIKit.Img(bar, "GoldChip", "hud_chip_bg", tr, tr, new Vector2(-201f, -15f), new Vector2(225f, 66f), true);
        UIKit.Img(goldChip.transform, "Icon", "icon_coin", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(42f, 42f));
        goldText = UIKit.Text(goldChip.transform, "Val", "0", 28f, Color.white, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(70f, 0f), new Vector2(140f, 50f), TextAlignmentOptions.Left);
    }

    private void BuildTabs()
    {
        string[] icons = { "portrait_hero_knight", "icon_tower_archer", "item_potion_red", "item_chest_gold" };
        tabImages.Clear();
        for (int i = 0; i < 4; i++)
        {
            int idx = i;
            Image t = DI(design, "Tab_" + i, i == tab ? "shop_tab_active" : "shop_tab", 24f, 84f + i * 96f, 108f, 86f, true, true);
            UIKit.MakeButton(t, null, () => { tab = idx; SetStatus(idx == 2 ? "" : "Sắp ra mắt"); RefreshTabs(); });
            Image ic = UIKit.Img(t.transform, "Icon", icons[i], new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(62f, 62f));
            tabImages.Add(t);
        }
    }

    private void RefreshTabs()
    {
        for (int i = 0; i < tabImages.Count; i++)
            tabImages[i].sprite = UIAssets.Get(i == tab ? "shop_tab_active" : "shop_tab");
    }

    private void BuildMainPanel()
    {
        DI(design, "MainPanel", "panel_dark_9slice", 148f, 84f, 796f, 540f, true, true);

        DT(design, "Vật phẩm hôm nay", 22f, Color.white, 176f, 100f, 400f, 34f, TextAlignmentOptions.Left);
        timerText = DT(design, "", 14f, UIKit.Muted, 176f, 134f, 400f, 24f, TextAlignmentOptions.Left, FontStyles.Normal);

        // Nút làm mới (chip xanh: icon retry + coin + giá)
        Image rf = DI(design, "RefreshBtn", "hud_chip_bg", 692f, 102f, 232f, 44f, true, true);
        rf.color = new Color(0.62f, 1f, 0.72f, 1f);
        UIKit.MakeButton(rf, null, OnRefresh);
        UIKit.Img(rf.transform, "Icon", "btn_retry", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(4f, 0f), new Vector2(40f, 40f));
        UIKit.Img(rf.transform, "Coin", "icon_coin", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(120f, 0f), new Vector2(24f, 24f));
        refreshCostText = UIKit.Text(rf.transform, "Cost", RefreshCost.ToString(), 18f, Color.white, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(150f, 0f), new Vector2(60f, 30f), TextAlignmentOptions.Left);
        UIKit.Text(rf.transform, "Lbl", "Làm mới", 16f, Color.white, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(46f, 0f), new Vector2(76f, 30f), TextAlignmentOptions.Left);

        cardBgs.Clear(); cardOwnedFlags.Clear(); cardPriceTexts.Clear(); cardCountTexts.Clear();
        for (int i = 0; i < items.Count; i++)
        {
            int idx = i;
            Item it = items[i];
            float cx = 170f + (i % 4) * 194f;
            float cy = 174f + (i / 4) * 216f;

            Image card = DI(design, "Card_" + i, "shop_card_bg", cx, cy, 184f, 204f, true, true);
            UIKit.MakeButton(card, null, () => { selected = idx; RefreshAll(); });
            cardBgs.Add(card);

            Image glow = UIKit.Img(card.transform, "Glow", "badge_circle", TL, TL, new Vector2(32f, -22f), new Vector2(120f, 120f));
            glow.color = new Color(1f, 1f, 1f, 0.1f);
            UIKit.Img(card.transform, "Icon", "item_" + it.id, TL, TL, new Vector2(28f, -18f), new Vector2(128f, 128f));

            UIKit.Text(card.transform, "Name", it.name, 18f, Color.white, TL, TL, new Vector2(4f, -138f), new Vector2(176f, 28f));

            Image chip = UIKit.Img(card.transform, "PriceChip", "hud_chip_bg", TL, TL, new Vector2(37f, -166f), new Vector2(110f, 32f), true);
            UIKit.Img(chip.transform, "Coin", "icon_coin", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(9f, 0f), new Vector2(20f, 20f));
            TextMeshProUGUI pt = UIKit.Text(chip.transform, "Price", "0", 17f, UIKit.Gold, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(34f, 0f), new Vector2(72f, 28f), TextAlignmentOptions.Left);
            cardPriceTexts.Add(pt);

            // Số lượng đang có (vật phẩm tiêu hao)
            Image cnt = UIKit.Img(card.transform, "CountBadge", "badge_circle", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-6f, -6f), new Vector2(38f, 38f));
            TextMeshProUGUI ct = UIKit.Text(cnt.transform, "N", "0", 16f, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(38f, 38f));
            cardCountTexts.Add(ct);

            // Cờ "đã sở hữu"
            Image own = UIKit.Img(card.transform, "OwnedFlag", "btn_check", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-8f, 8f), new Vector2(50f, 50f));
            cardOwnedFlags.Add(own.gameObject);

            // Tag giảm giá / MỚI / HOT (gán nội dung ở RefreshAll)
            Image tag = UIKit.Img(card.transform, "Tag", "tag_red", TL, TL, new Vector2(6f, -8f), new Vector2(62f, 26f), true);
            TextMeshProUGUI tt = UIKit.Text(tag.transform, "T", "", 13f, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(62f, 26f));
            tag.gameObject.SetActive(false);
        }
    }

    private void BuildDetailPanel()
    {
        DI(design, "DetailPanel", "panel_purple_9slice", 964f, 84f, 292f, 540f, true, true);
        detailIcon = DI(design, "DetailIcon", "item_weapon_axe", 1010f, 110f, 200f, 200f);
        detailName = DT(design, "", 26f, Color.white, 974f, 316f, 272f, 40f);
        detailRarity = DI(design, "Rarity", "tag_purple", 1050f, 358f, 120f, 30f, true);
        detailRarityText = UIKit.Text(detailRarity.transform, "T", "HIẾM", 14f, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 30f));

        for (int i = 0; i < 3; i++)
        {
            float y = 402f + i * 34f;
            detailStatIcons[i] = DI(design, "StatIcon" + i, "stat_dmg", 990f, y, 28f, 28f);
            detailStatNames[i] = DT(design, "", 16f, UIKit.Muted, 1026f, y, 120f, 28f, TextAlignmentOptions.Left, FontStyles.Normal);
            detailStatValues[i] = DT(design, "", 18f, UIKit.Green, 1130f, y, 100f, 28f, TextAlignmentOptions.Right);
        }
        detailDesc = DT(design, "", 14f, UIKit.Muted, 984f, 506f, 252f, 44f, TextAlignmentOptions.TopLeft, FontStyles.Normal);

        buyBg = DI(design, "BuyBtn", "btn_wide_green", 985f, 556f, 250f, 60f, true, true);
        UIKit.MakeButton(buyBg, "btn_wide_green_pressed", OnBuy);
        UIKit.Img(buyBg.transform, "Check", "btn_check", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 3f), new Vector2(40f, 40f));
        Image coin = UIKit.Img(buyBg.transform, "Coin", "icon_coin", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-14f, 3f), new Vector2(32f, 32f));
        buyCoin = coin.gameObject;
        buyPriceText = UIKit.Text(buyBg.transform, "Price", "0", 26f, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(50f, 3f), new Vector2(110f, 40f), TextAlignmentOptions.Left);
    }

    private void BuildBottom()
    {
        Image home = DI(design, "BtnHome", "btn_wide_lavender", 148f, 640f, 230f, 60f, true, true);
        UIKit.MakeButton(home, "btn_wide_lavender_pressed", GoHome);
        UIKit.Img(home.transform, "Icon", "btn_home", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 3f), new Vector2(48f, 48f));

        Image play = DI(design, "BtnContinue", "btn_wide_green", 684f, 640f, 260f, 60f, true, true);
        UIKit.MakeButton(play, "btn_wide_green_pressed", Close);
        UIKit.Img(play.transform, "Icon", "btn_play", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 3f), new Vector2(48f, 48f));
    }

    // ------------------------------------------------------------------ Refresh
    private void RefreshAll()
    {
        if (!built) return;
        if (goldText != null) goldText.text = ShopWallet.Gold.ToString("N0");
        if (gemText != null) gemText.text = ShopWallet.Gems.ToString("N0");
        RefreshTabs();

        for (int i = 0; i < items.Count; i++)
        {
            Item it = items[i];
            bool owned = Owned(it);
            cardBgs[i].sprite = UIAssets.Get(i == selected ? "shop_card_selected" : (owned ? "shop_card_owned" : "shop_card_bg"));
            cardPriceTexts[i].text = Price(it).ToString();
            cardPriceTexts[i].transform.parent.gameObject.SetActive(!owned);
            cardOwnedFlags[i].SetActive(owned);

            int n = it.equipment ? 0 : Count(it);
            cardCountTexts[i].transform.parent.gameObject.SetActive(n > 0);
            cardCountTexts[i].text = n.ToString();

            Transform tag = cardBgs[i].transform.Find("Tag");
            if (tag != null)
            {
                string label = it.discount > 0 ? "-" + it.discount + "%" : it.badge;
                tag.gameObject.SetActive(!string.IsNullOrEmpty(label));
                if (!string.IsNullOrEmpty(label))
                {
                    TextMeshProUGUI tt = tag.GetComponentInChildren<TextMeshProUGUI>();
                    if (tt != null) tt.text = label;
                    Image ti = tag.GetComponent<Image>();
                    ti.sprite = UIAssets.Get(it.discount > 0 || label == "HOT" ? "tag_red" : "tag_green");
                }
            }
        }

        // Chi tiết
        Item s = items[Mathf.Clamp(selected, 0, items.Count - 1)];
        Sprite ic = UIAssets.Get("item_" + s.id);
        if (detailIcon != null && ic != null) detailIcon.sprite = ic;
        detailName.text = s.name;
        detailRarityText.text = s.rarity;
        detailRarity.sprite = UIAssets.Get(s.rarity == "THƯỜNG" ? "tag_green" : (s.rarity == "HIẾM" ? "tag_purple" : "tag_gold"));
        detailDesc.text = s.desc;

        string[] names = { "Sát thương", "Tốc độ", "Hồi chiêu" };
        for (int i = 0; i < 3; i++)
        {
            bool has = s.stats != null && i < s.stats.Length;
            detailStatIcons[i].gameObject.SetActive(has);
            detailStatNames[i].gameObject.SetActive(has);
            detailStatValues[i].gameObject.SetActive(has);
            if (!has) continue;
            Sprite si = UIAssets.Get(s.stats[i].icon);
            if (si != null) detailStatIcons[i].sprite = si;
            detailStatNames[i].text = "";
            detailStatValues[i].text = s.stats[i].value;
        }

        bool owned2 = Owned(s);
        buyBg.sprite = UIAssets.Get(owned2 ? "btn_wide_lavender" : "btn_wide_green");
        buyCoin.SetActive(!owned2);
        buyPriceText.gameObject.SetActive(!owned2);
        buyPriceText.text = Price(s).ToString();
        Transform chk = buyBg.transform.Find("Check");
        if (chk != null) chk.gameObject.SetActive(owned2);
    }

    private void Update()
    {
        if (!built || root == null || !root.activeSelf) return;

        // Đồng hồ làm mới (giờ thật, chạy cả khi Time.timeScale = 0)
        long next = long.Parse(PlayerPrefs.GetString("Shop_NextRefresh", "0"));
        long now = DateTime.UtcNow.Ticks;
        if (next <= now)
        {
            next = now + TimeSpan.FromHours(3).Ticks;
            PlayerPrefs.SetString("Shop_NextRefresh", next.ToString());
            ApplyPromotions(true);
            RefreshAll();
        }
        TimeSpan left = new TimeSpan(next - now);
        if (timerText != null) timerText.text = "Tự làm mới sau " + string.Format("{0:00}:{1:00}:{2:00}", (int)left.TotalHours, left.Minutes, left.Seconds);

        if (statusTimer > 0f)
        {
            statusTimer -= Time.unscaledDeltaTime;
            if (statusTimer <= 0f && statusText != null) statusText.text = "";
        }
    }

    private void SetStatus(string msg)
    {
        if (statusText == null) return;
        statusText.text = msg;
        statusTimer = 1.8f;
    }

    // ------------------------------------------------------------------ Actions
    private void OnRefresh()
    {
        if (ShopWallet.Gold < RefreshCost) { SetStatus("Không đủ vàng!"); return; }
        ShopWallet.Gold -= RefreshCost;
        ApplyPromotions(true);
        RefreshAll();
        SetStatus("Đã làm mới!");
    }

    private void OnBuy()
    {
        Item it = items[Mathf.Clamp(selected, 0, items.Count - 1)];
        if (Owned(it)) { SetStatus("Đã trang bị!"); return; }

        int price = Price(it);
        if (ShopWallet.Gold < price) { SetStatus("Không đủ vàng!"); return; }

        ShopWallet.Gold -= price;
        if (it.equipment) PlayerPrefs.SetInt("Shop_Owned_" + it.id, 1);
        else PlayerPrefs.SetInt("Shop_Count_" + it.id, Count(it) + 1);
        PlayerPrefs.Save();

        RefreshAll();
        SetStatus("Mua thành công!");
    }
}
