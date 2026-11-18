using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// StatsUI = THANH TOP HUD (theo demo gameplay_level_XX):
///   [Home][Pause/Continue][x2][Shop]   (Tên màn)   (Wave)   (Vàng) (Mạng) (Số quái hạ) [Cài đặt]
/// Toàn bộ dùng ICON/sprite (UISpriteLibrary), chỉ có số/tên màn là chữ.
/// Gắn vào Canvas_UI. Tự dựng giao diện khi Start().
/// </summary>
public class StatsUI : MonoBehaviour
{
    public static StatsUI main;

    private TextMeshProUGUI goldText, livesText, killsText, waveText, titleText;
    private Image pauseImg, ffImg, soundImg, musicImg;
    private Button pauseBtn;
    private GameObject pausePanel;
    private RectTransform topBar;

    private bool paused;
    private bool fast;
    private float nextRefresh;
    private bool built;

    private void Awake()
    {
        main = this;
    }

    private void Start()
    {
        Build();
        Subscribe();
        RefreshAll();
    }

    private void OnDestroy()
    {
        if (main == this) main = null;
        if (paused || fast) Time.timeScale = 1f;
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (LevelManager.main == null) return;
        LevelManager.main.OnCurrencyChanged += OnCurrency;
        LevelManager.main.OnLivesChanged += OnLives;
        LevelManager.main.OnWaveChanged += OnWave;
    }

    private void Unsubscribe()
    {
        if (LevelManager.main == null) return;
        LevelManager.main.OnCurrencyChanged -= OnCurrency;
        LevelManager.main.OnLivesChanged -= OnLives;
        LevelManager.main.OnWaveChanged -= OnWave;
    }

    private void OnCurrency(int v) { if (goldText != null) goldText.text = v.ToString("N0"); }
    private void OnLives(int v) { if (livesText != null) livesText.text = v.ToString(); }
    private void OnWave(int w) { UpdateWave(); }

    private void Update()
    {
        if (Time.unscaledTime >= nextRefresh)
        {
            nextRefresh = Time.unscaledTime + 0.3f;
            RefreshAll();
        }
    }

    // ------------------------------------------------------------------ Build
    private void Build()
    {
        if (built) return;
        built = true;

        // Dọn thanh top cũ (nếu scene còn sót object cũ)
        Transform old = transform.Find("TopBar");
        if (old != null) Destroy(old.gameObject);

        topBar = UIKit.NewRect("TopBar", transform);
        UIKit.Place(topBar, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(0f, 96f));
        topBar.anchorMin = new Vector2(0f, 1f);
        topBar.anchorMax = new Vector2(1f, 1f);
        topBar.offsetMin = new Vector2(0f, -96f);
        topBar.offsetMax = Vector2.zero;
        topBar.SetAsFirstSibling();

        Image bg = topBar.gameObject.AddComponent<Image>();
        bg.sprite = UIAssets.Get("hud_topbar_bg");
        bg.color = bg.sprite != null ? Color.white : new Color(0.1f, 0.11f, 0.2f, 0.95f);
        bg.raycastTarget = true; // chặn click xuyên xuống map

        Vector2 tl = new Vector2(0f, 1f);
        Vector2 tr = new Vector2(1f, 1f);
        Vector2 size = new Vector2(72f, 72f);

        UIKit.IconButton(topBar, "BtnHome", "btn_home", "btn_home_pressed", tl, tl, new Vector2(21f, -12f), size, GoHome);
        pauseBtn = UIKit.IconButton(topBar, "BtnPause", "btn_pause", "btn_pause_pressed", tl, tl, new Vector2(105f, -12f), size, TogglePause);
        pauseImg = pauseBtn.GetComponent<Image>();
        Button ff = UIKit.IconButton(topBar, "BtnFast", "btn_ff", "btn_ff_pressed", tl, tl, new Vector2(189f, -12f), size, ToggleFast);
        ffImg = ff.GetComponent<Image>();
        UIKit.IconButton(topBar, "BtnShop", "btn_shop", "btn_shop_pressed", tl, tl, new Vector2(273f, -12f), size, OpenShop);

        // Tên màn
        Image title = UIKit.Img(topBar, "TitleChip", "hud_title_pill", tl, tl, new Vector2(390f, -15f), new Vector2(480f, 66f), true);
        titleText = UIKit.Text(title.transform, "Title", "Màn 1", 26f, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460f, 60f));

        // Wave
        Image waveChip = UIKit.Img(topBar, "WaveChip", "hud_wave_chip_bg", tr, tr, new Vector2(-720f, -15f), new Vector2(225f, 66f), true);
        UIKit.Img(waveChip.transform, "Flag", "icon_wave_flag", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(42f, 42f));
        waveText = UIKit.Text(waveChip.transform, "Wave", "Wave 1/1", 24f, Color.white, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(68f, 0f), new Vector2(150f, 50f), TextAlignmentOptions.Left);

        // Cài đặt (phải cùng)
        UIKit.IconButton(topBar, "BtnSettings", "btn_settings", "btn_settings_pressed", tr, tr, new Vector2(-21f, -12f), size, OpenPause);

        // Chip: số quái hạ, mạng, vàng (phải -> trái)
        killsText = MakeChip("KillsChip", "icon_skull_kills", new Vector2(-108f, -15f), 165f);
        livesText = MakeChip("LivesChip", "icon_heart", new Vector2(-288f, -15f), 165f);
        goldText = MakeChip("GoldChip", "icon_coin", new Vector2(-468f, -15f), 225f);
    }

    private TextMeshProUGUI MakeChip(string name, string iconKey, Vector2 pos, float width)
    {
        Vector2 tr = new Vector2(1f, 1f);
        Image chip = UIKit.Img(topBar, name, "hud_chip_bg", tr, tr, pos, new Vector2(width, 66f), true);
        UIKit.Img(chip.transform, "Icon", iconKey, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(42f, 42f));
        return UIKit.Text(chip.transform, "Value", "0", 28f, Color.white, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(72f, 0f), new Vector2(width - 84f, 50f), TextAlignmentOptions.Left);
    }

    // ------------------------------------------------------------------ Refresh
    public void RefreshAll()
    {
        LevelManager lm = LevelManager.main;
        if (lm == null) return;
        if (goldText != null) goldText.text = lm.currency.ToString("N0");
        if (livesText != null) livesText.text = lm.livesCount.ToString();
        if (killsText != null) killsText.text = lm.enemiesKilled.ToString();
        UpdateWave();

        if (titleText != null)
        {
            LevelInfo info = Object.FindAnyObjectByType<LevelInfo>();
            titleText.text = info != null ? info.displayName : ("Màn " + lm.levelNumber);
        }
    }

    private void UpdateWave()
    {
        if (waveText == null || LevelManager.main == null) return;
        waveText.text = "Wave " + LevelManager.main.GetCurrentWave() + "/" + LevelManager.main.TotalWaves;
    }

    // ------------------------------------------------------------------ Actions
    private void SetButtonSprites(Button b, string normal, string pressed)
    {
        Image img = b.GetComponent<Image>();
        Sprite n = UIAssets.Get(normal);
        if (n != null) img.sprite = n;
        SpriteState st = b.spriteState;
        st.pressedSprite = UIAssets.Get(pressed);
        st.highlightedSprite = img.sprite;
        st.selectedSprite = img.sprite;
        b.spriteState = st;
    }

    private void ApplyTimeScale()
    {
        Time.timeScale = paused ? 0f : (fast ? 2f : 1f);
    }

    public void TogglePause()
    {
        paused = !paused;
        ApplyTimeScale();
        if (pauseBtn != null)
        {
            if (paused) SetButtonSprites(pauseBtn, "btn_play", "btn_play_pressed");
            else SetButtonSprites(pauseBtn, "btn_pause", "btn_pause_pressed");
        }
    }

    public void ToggleFast()
    {
        fast = !fast;
        if (ffImg != null) ffImg.color = fast ? new Color(1f, 0.78f, 0.45f, 1f) : Color.white;
        ApplyTimeScale();
    }

    public void OpenShop()
    {
        ShopUI.ShowShopScreen();
    }

    public void GoHome()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    public void Retry()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // ------------------------------------------------------------------ Pause menu (icon only)
    public void OpenPause()
    {
        if (pausePanel == null) BuildPausePanel();
        pausePanel.SetActive(true);
        paused = true;
        ApplyTimeScale();
    }

    public void ClosePause()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        paused = false;
        ApplyTimeScale();
        if (pauseBtn != null) SetButtonSprites(pauseBtn, "btn_pause", "btn_pause_pressed");
    }

    private void BuildPausePanel()
    {
        RectTransform root = UIKit.NewRect("PausePanel", transform);
        UIKit.Stretch(root);
        pausePanel = root.gameObject;
        UIKit.Solid(root, "Dim", new Color(0.03f, 0.03f, 0.08f, 0.7f), true);

        Vector2 c = new Vector2(0.5f, 0.5f);
        UIKit.Img(root, "Panel", "panel_dark_9slice", c, c, Vector2.zero, new Vector2(760f, 520f), true, true);
        Image rib = UIKit.Img(root, "Ribbon", "ribbon_title_orange", c, c, new Vector2(0f, 250f), new Vector2(520f, 120f));
        UIKit.Text(rib.transform, "Title", "TẠM DỪNG", 38f, Color.white, new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460f, 70f));

        // Hàng 1: tiếp tục - chơi lại - về menu
        UIKit.IconButton(root, "Resume", "btn_play", "btn_play_pressed", c, c, new Vector2(-220f, 20f), new Vector2(180f, 180f), ClosePause);
        UIKit.IconButton(root, "Retry", "btn_retry", "btn_retry_pressed", c, c, new Vector2(0f, 20f), new Vector2(180f, 180f), Retry);
        UIKit.IconButton(root, "Home", "btn_home", "btn_home_pressed", c, c, new Vector2(220f, 20f), new Vector2(180f, 180f), GoHome);

        // Hàng 2: âm thanh / nhạc (+ nút "NHIỆM VỤ" xem lại Thẻ nhiệm vụ khi màn có nhiệm vụ 3 sao)
        bool soundOn = PlayerPrefs.GetInt("SoundOn", 1) == 1;
        bool musicOn = PlayerPrefs.GetInt("MusicOn", 1) == 1;
        bool hasMission = ObjectiveTracker.IsActive;
        float rowSoundX = hasMission ? -195f : -90f;
        float rowMusicX = hasMission ? -45f : 90f;
        if (hasMission)
        {
            Button missionBtn = UIKit.IconButton(root, "Mission", "btn_wide_green", "btn_wide_green_pressed", c, c, new Vector2(145f, -170f), new Vector2(230f, 84f), MissionCardUI.ShowReview);
            UIKit.Text(missionBtn.transform, "Label", "NHIỆM VỤ", 30f, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220f, 60f));
        }
        Button sb = UIKit.IconButton(root, "Sound", soundOn ? "btn_sound" : "btn_sound_off", null, c, c, new Vector2(rowSoundX, -170f), new Vector2(110f, 110f), null);
        soundImg = sb.GetComponent<Image>();
        sb.onClick.AddListener(() =>
        {
            bool on = PlayerPrefs.GetInt("SoundOn", 1) != 1;
            PlayerPrefs.SetInt("SoundOn", on ? 1 : 0);
            AudioListener.volume = on ? 1f : 0f;
            soundImg.sprite = UIAssets.Get(on ? "btn_sound" : "btn_sound_off");
        });
        Button mb = UIKit.IconButton(root, "Music", musicOn ? "btn_music" : "btn_music_off", null, c, c, new Vector2(rowMusicX, -170f), new Vector2(110f, 110f), null);
        musicImg = mb.GetComponent<Image>();
        mb.onClick.AddListener(() =>
        {
            bool on = PlayerPrefs.GetInt("MusicOn", 1) != 1;
            PlayerPrefs.SetInt("MusicOn", on ? 1 : 0);
            musicImg.sprite = UIAssets.Get(on ? "btn_music" : "btn_music_off");
        });
        UIKit.IconButton(root, "Close", "btn_close", "btn_close_pressed", c, c, new Vector2(340f, 230f), new Vector2(90f, 90f), ClosePause);

        pausePanel.SetActive(false);
    }
}
