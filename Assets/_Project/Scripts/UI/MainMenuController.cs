using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// MainMenuController = MÀN HÌNH CHÍNH (theo demo main_menu_screen):
///  - Nền có hiệp sĩ + boss, logo game, chip tổng sao
///  - Nút CHỌN TƯỚNG, lưới 10 nút màn chơi (số, tên, sao, khóa, BOSS)
///  - Nút góc: Shop, Thành tích | Âm thanh, Cài đặt
/// Toàn bộ dựng bằng sprite từ UISpriteLibrary khi Start().
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("UI References (tự gán khi dựng)")]
    public TextMeshProUGUI totalCupsText;
    public TextMeshProUGUI selectedHeroBadge;
    public Button heroesButton;
    public Button[] levelButtons;

    private GameObject trophyModal;
    private GameObject settingsModal;
    private Image soundImg, musicImg;
    private RectTransform root;
    private Image heroPortrait;

    private static readonly string[] LevelNames =
    {
        "Đường Mòn", "Ngã Rẽ", "Cánh Đồng", "Hẻm Đá", "Giao Lộ",
        "Kẻ Dẫn Đường", "Thành Lũy", "Băng Giá", "Núi Lửa", "Ngai Rồng"
    };

    private void Start()
    {
        if (Camera.main != null)
        {
            Camera.main.orthographic = true;
            Camera.main.orthographicSize = 5.4f;
            Camera.main.transform.position = new Vector3(0, 0, -10f);
            Camera.main.backgroundColor = new Color(0.1f, 0.09f, 0.26f, 1f);
        }

        if (SaveManager.instance == null)
        {
            GameObject saveObj = new GameObject("SaveManager");
            saveObj.AddComponent<SaveManager>();
        }

        if (Object.FindAnyObjectByType<HeroSelectPopup>() == null)
            gameObject.AddComponent<HeroSelectPopup>();
        if (Object.FindAnyObjectByType<ShopUI>() == null)
            new GameObject("ShopUI").AddComponent<ShopUI>();

        Time.timeScale = 1f;
        Build();
        UpdateUI();
    }

    private void OnEnable()
    {
        if (root != null) UpdateUI();
    }

    // ------------------------------------------------------------------ Build
    private void Build()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        Transform parent = canvas != null ? canvas.transform : transform;

        Transform old = parent.Find("MenuRoot");
        if (old != null) Destroy(old.gameObject);

        root = UIKit.NewRect("MenuRoot", parent);
        UIKit.Stretch(root);
        root.SetAsFirstSibling();

        // Nền (bao phủ toàn màn hình, giữ tỉ lệ bằng AspectRatioFitter - envelope)
        Image bg = UIKit.Img(root, "Background", "main_menu_decor_background_1920x1080", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1920f, 1080f), false, false);
        bg.preserveAspect = false;
        AspectRatioFitter fit = bg.gameObject.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fit.aspectRatio = 1920f / 1080f;

        // Logo
        Vector2 tc = new Vector2(0.5f, 1f);
        UIKit.Img(root, "Logo", "logo_title", tc, tc, new Vector2(0f, -22f), new Vector2(675f, 276f));

        // Chip tổng sao
        Image chip = UIKit.Img(root, "StarsChip", "hud_title_pill", tc, tc, new Vector2(0f, -310f), new Vector2(330f, 62f), true);
        UIKit.Img(chip.transform, "Star", "star_full", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(40f, 40f));
        totalCupsText = UIKit.Text(chip.transform, "Val", "0 / 30", 30f, UIKit.Gold, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(84f, 0f), new Vector2(220f, 50f), TextAlignmentOptions.Left);

        // Nút Chọn tướng
        Vector2 bc = new Vector2(0.5f, 0f);
        Image hb = UIKit.Img(root, "Btn_Heroes", "btn_wide_orange", bc, bc, new Vector2(0f, 250f), new Vector2(430f, 90f), true, true);
        heroesButton = UIKit.MakeButton(hb, "btn_wide_orange_pressed", OpenHeroesPopup);
        UIKit.AddHoverScale(hb.gameObject, 1.04f);
        heroPortrait = UIKit.Img(hb.transform, "Portrait", "portrait_hero_knight", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 3f), new Vector2(66f, 66f));
        UIKit.Text(hb.transform, "Label", "CHỌN TƯỚNG", 30f, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(40f, 4f), new Vector2(300f, 50f));
        selectedHeroBadge = null;

        BuildLevelGrid();

        // Nút góc
        Vector2 tl = new Vector2(0f, 1f);
        Vector2 tr = new Vector2(1f, 1f);
        Vector2 sz = new Vector2(96f, 96f);
        UIKit.IconButton(root, "Btn_Shop", "btn_shop", "btn_shop_pressed", tl, tl, new Vector2(40f, -30f), sz, OpenShopUI);
        UIKit.IconButton(root, "Btn_Trophy", "btn_trophy", "btn_trophy_pressed", tl, tl, new Vector2(150f, -30f), sz, OpenTrophyModal);
        Button sb = UIKit.IconButton(root, "Btn_Sound", PlayerPrefs.GetInt("SoundOn", 1) == 1 ? "btn_sound" : "btn_sound_off", null, tr, tr, new Vector2(-150f, -30f), sz, null);
        soundImg = sb.GetComponent<Image>();
        sb.onClick.AddListener(ToggleSound);
        UIKit.IconButton(root, "Btn_Settings", "btn_settings", "btn_settings_pressed", tr, tr, new Vector2(-40f, -30f), sz, OpenSettingsModal);
    }

    private void BuildLevelGrid()
    {
        int count = LevelNames.Length;
        levelButtons = new Button[count];
        Vector2 bc = new Vector2(0.5f, 0f);
        float w = 330f, h = 92f, gx = 20f, gy = 16f;

        for (int i = 0; i < count; i++)
        {
            int level = i + 1;
            int col = i % 5;
            int row = i / 5;
            float x = (col - 2) * (w + gx);
            float y = 24f + (1 - row) * (h + gy);

            Image btnImg = UIKit.Img(root, "Level_" + level, "lvl_btn_open", bc, bc, new Vector2(x, y), new Vector2(w, h), true, true);
            Button b = UIKit.MakeButton(btnImg, null, () => LoadLevel(level));
            UIKit.AddHoverScale(btnImg.gameObject, 1.04f);
            levelButtons[i] = b;

            Transform t = btnImg.transform;
            Vector2 lc = new Vector2(0f, 0.5f);
            Image badge = UIKit.Img(t, "Badge", "lvl_badge_gold", lc, lc, new Vector2(14f, 2f), new Vector2(56f, 56f));
            UIKit.Text(badge.transform, "Num", level.ToString(), 26f, new Color(0.25f, 0.15f, 0.19f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56f, 56f));
            UIKit.Text(t, "Name", LevelNames[i], 24f, Color.white, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(84f, -12f), new Vector2(230f, 34f), TextAlignmentOptions.Left);

            // 3 sao
            for (int s = 0; s < 3; s++)
                UIKit.Img(t, "Star" + s, "star_empty", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(86f + s * 44f, 10f), new Vector2(36f, 36f));

            // Khóa + BOSS
            UIKit.Img(t, "Lock", "icon_lock_white", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 2f), new Vector2(48f, 48f));
            if (level == 8 || level == 10)
            {
                UIKit.Img(t, "Skull", "icon_skull_kills", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -10f), new Vector2(34f, 34f));
            }
        }
    }

    // ------------------------------------------------------------------ Refresh
    private void UpdateUI()
    {
        if (SaveManager.instance == null) return;

        int total = SaveManager.instance.totalCups;
        if (totalCupsText != null) totalCupsText.text = total + " / " + (LevelNames.Length * 3);

        if (heroPortrait != null)
        {
            Sprite sp = UIAssets.Get("portrait_hero_" + SaveManager.instance.selectedHeroId.ToLower());
            if (sp != null) heroPortrait.sprite = sp;
        }

        if (levelButtons == null) return;
        for (int i = 0; i < levelButtons.Length; i++)
        {
            Button b = levelButtons[i];
            if (b == null) continue;
            int level = i + 1;
            bool unlocked = SaveManager.instance.IsLevelUnlocked(level);
            int cups = SaveManager.instance.GetLevelCups(level);
            bool boss = (level == 8 || level == 10);

            Image img = b.GetComponent<Image>();
            string key = !unlocked ? "lvl_btn_locked" : (boss ? "lvl_btn_boss" : (cups > 0 ? "lvl_btn_done" : "lvl_btn_open"));
            Sprite s = UIAssets.Get(key);
            if (s != null) img.sprite = s;
            b.interactable = unlocked;
            b.targetGraphic = img;

            Transform t = b.transform;
            Transform lockT = t.Find("Lock");
            if (lockT != null) lockT.gameObject.SetActive(!unlocked);

            Transform badge = t.Find("Badge");
            if (badge != null)
            {
                Image bi = badge.GetComponent<Image>();
                Sprite bs = UIAssets.Get(unlocked ? "lvl_badge_gold" : "lvl_badge_gray");
                if (bs != null) bi.sprite = bs;
            }

            for (int st = 0; st < 3; st++)
            {
                Transform star = t.Find("Star" + st);
                if (star == null) continue;
                star.gameObject.SetActive(unlocked);
                Sprite ss = UIAssets.Get(st < cups ? "star_full" : "star_empty");
                if (ss != null) star.GetComponent<Image>().sprite = ss;
            }
        }
    }

    // ------------------------------------------------------------------ Actions
    public void LoadLevel(int level)
    {
        Time.timeScale = 1f;
        // Chọn màn -> Story Intro (lần đầu hoặc khi bật "Luôn xem cốt truyện") -> vào màn chơi -> Thẻ nhiệm vụ
        StoryFlow.PlayThenContinue(level, false, () => SceneManager.LoadScene("Level_" + level));
    }

    public void OpenHeroesPopup()
    {
        HeroSelectPopup pop = Object.FindAnyObjectByType<HeroSelectPopup>();
        if (pop == null) pop = gameObject.AddComponent<HeroSelectPopup>();
        pop.OpenPopup();
    }

    public void OpenShopUI()
    {
        ShopUI.ShowShopScreen();
    }

    public void ToggleSound()
    {
        bool on = PlayerPrefs.GetInt("SoundOn", 1) != 1;
        PlayerPrefs.SetInt("SoundOn", on ? 1 : 0);
        AudioListener.volume = on ? 1f : 0f;
        if (soundImg != null) soundImg.sprite = UIAssets.Get(on ? "btn_sound" : "btn_sound_off");
    }

    public void OpenTrophyModal()
    {
        if (trophyModal == null) trophyModal = BuildTrophyModal();
        trophyModal.SetActive(true);
        RefreshTrophy();
    }

    public void OpenSettingsModal()
    {
        if (settingsModal == null) settingsModal = BuildSettingsModal();
        settingsModal.SetActive(true);
    }

    // ------------------------------------------------------------------ Modals (icon-only)
    private GameObject BuildSettingsModal()
    {
        RectTransform m = UIKit.NewRect("SettingsModal", root);
        UIKit.Stretch(m);
        UIKit.Solid(m, "Dim", new Color(0.03f, 0.03f, 0.08f, 0.75f), true);
        Vector2 c = new Vector2(0.5f, 0.5f);
        UIKit.Img(m, "Panel", "panel_dark_9slice", c, c, Vector2.zero, new Vector2(700f, 420f), true, true);
        Image rib = UIKit.Img(m, "Ribbon", "ribbon_title_orange", c, c, new Vector2(0f, 210f), new Vector2(480f, 110f));
        UIKit.Text(rib.transform, "T", "CÀI ĐẶT", 34f, Color.white, new Vector2(0.5f, 0.56f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420f, 60f));

        bool soundOn = PlayerPrefs.GetInt("SoundOn", 1) == 1;
        bool musicOn = PlayerPrefs.GetInt("MusicOn", 1) == 1;
        Button sb = UIKit.IconButton(m, "Sound", soundOn ? "btn_sound" : "btn_sound_off", null, c, c, new Vector2(-110f, 0f), new Vector2(160f, 160f), null);
        sb.onClick.AddListener(() => { ToggleSound(); sb.GetComponent<Image>().sprite = UIAssets.Get(PlayerPrefs.GetInt("SoundOn", 1) == 1 ? "btn_sound" : "btn_sound_off"); });
        Button mb = UIKit.IconButton(m, "Music", musicOn ? "btn_music" : "btn_music_off", null, c, c, new Vector2(110f, 0f), new Vector2(160f, 160f), null);
        mb.onClick.AddListener(() =>
        {
            bool on = PlayerPrefs.GetInt("MusicOn", 1) != 1;
            PlayerPrefs.SetInt("MusicOn", on ? 1 : 0);
            mb.GetComponent<Image>().sprite = UIAssets.Get(on ? "btn_music" : "btn_music_off");
        });

        // Công tắc "Luôn xem cốt truyện" (mặc định: chỉ xem lần đầu của từng màn)
        RectTransform st = UIKit.Box(m, "StoryToggle", c, c, new Vector2(0f, -150f), new Vector2(540f, 70f));
        Image stImg = st.gameObject.AddComponent<Image>();
        stImg.color = new Color(0.16f, 0.20f, 0.34f, 1f);
        stImg.raycastTarget = true;
        TextMeshProUGUI stLabel = UIKit.Text(st, "Label", "", 30f, Color.white, c, c, Vector2.zero, new Vector2(520f, 60f));
        System.Action refreshStory = () => stLabel.text = "Luôn xem cốt truyện: " + (StoryFlow.AlwaysShow ? "BẬT" : "TẮT");
        refreshStory();
        UIKit.MakeButton(stImg, null, () => { StoryFlow.AlwaysShow = !StoryFlow.AlwaysShow; refreshStory(); });

        UIKit.IconButton(m, "Close", "btn_close", "btn_close_pressed", c, c, new Vector2(320f, 190f), new Vector2(84f, 84f), () => m.gameObject.SetActive(false));
        return m.gameObject;
    }

    private GameObject BuildTrophyModal()
    {
        RectTransform m = UIKit.NewRect("TrophyModal", root);
        UIKit.Stretch(m);
        UIKit.Solid(m, "Dim", new Color(0.03f, 0.03f, 0.08f, 0.75f), true);
        Vector2 c = new Vector2(0.5f, 0.5f);
        UIKit.Img(m, "Panel", "panel_dark_9slice", c, c, Vector2.zero, new Vector2(1000f, 560f), true, true);
        Image rib = UIKit.Img(m, "Ribbon", "ribbon_title_orange", c, c, new Vector2(0f, 290f), new Vector2(480f, 110f));
        UIKit.Text(rib.transform, "T", "THÀNH TÍCH", 34f, Color.white, new Vector2(0.5f, 0.56f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420f, 60f));
        UIKit.IconButton(m, "Close", "btn_close", "btn_close_pressed", c, c, new Vector2(460f, 250f), new Vector2(84f, 84f), () => m.gameObject.SetActive(false));

        // 10 hàng sao (2 cột x 5)
        for (int i = 0; i < 10; i++)
        {
            int col = i / 5;
            int row = i % 5;
            Vector2 pos = new Vector2(-240f + col * 480f, 140f - row * 82f);
            RectTransform cell = UIKit.Box(m, "Row_" + i, c, c, pos, new Vector2(440f, 70f));
            Image badge = UIKit.Img(cell, "Badge", "lvl_badge_gold", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(56f, 56f));
            UIKit.Text(badge.transform, "N", (i + 1).ToString(), 26f, new Color(0.25f, 0.15f, 0.19f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56f, 56f));
            for (int s = 0; s < 3; s++)
                UIKit.Img(cell, "Star" + s, "star_empty", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(90f + s * 56f, 0f), new Vector2(48f, 48f));
        }
        return m.gameObject;
    }

    private void RefreshTrophy()
    {
        if (trophyModal == null || SaveManager.instance == null) return;
        for (int i = 0; i < 10; i++)
        {
            Transform row = trophyModal.transform.Find("Row_" + i);
            if (row == null) continue;
            int cups = SaveManager.instance.GetLevelCups(i + 1);
            for (int s = 0; s < 3; s++)
            {
                Transform star = row.Find("Star" + s);
                if (star == null) continue;
                Sprite sp = UIAssets.Get(s < cups ? "star_full" : "star_empty");
                if (sp != null) star.GetComponent<Image>().sprite = sp;
            }
        }
    }
}
