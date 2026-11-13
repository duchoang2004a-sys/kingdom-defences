using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HeroHUD = KHUNG TƯỚNG góc dưới trái (theo demo): chân dung + tên + thanh máu bằng sprite.
/// Kỹ năng của hero nằm ở SpellHUD (icon Q / W). Các trường skill1Text/skill2Text giữ lại để tương thích code cũ.
/// </summary>
public class HeroHUD : MonoBehaviour
{
    public static HeroHUD instance;

    public Hero targetHero;
    public TextMeshProUGUI skill1Text;   // không dùng nữa (giữ để tương thích HeroDeployer)
    public TextMeshProUGUI skill2Text;   // không dùng nữa (giữ để tương thích HeroDeployer)

    private Image portrait;
    private Image hpFill;
    private TextMeshProUGUI nameText;
    private TextMeshProUGUI hpText;
    private GameObject deadIcon;
    private GameObject cameraButton;
    private string lastHeroId = "";
    private bool built;

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        Build();
    }

    private Canvas FindUICanvas()
    {
        Canvas[] all = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
            if (all[i].name == "Canvas_UI") return all[i];
        return all.Length > 0 ? all[0] : null;
    }

    private void Build()
    {
        if (built) return;
        built = true;

        Canvas canvas = FindUICanvas();
        if (canvas == null) return;

        Transform old = canvas.transform.Find("HeroPanel");
        if (old != null) Destroy(old.gameObject);

        Vector2 bl = new Vector2(0f, 0f);
        Image panel = UIKit.Img(canvas.transform, "HeroPanel", "hud_chip_bg", bl, bl, new Vector2(21f, 21f), new Vector2(520f, 126f), true, false);

        // Chân dung
        portrait = UIKit.Img(panel.transform, "Portrait", "portrait_hero_knight", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(104f, 104f));
        deadIcon = UIKit.Img(panel.transform, "DeadIcon", "icon_skull_kills", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(44f, 0f), new Vector2(44f, 44f)).gameObject;
        deadIcon.SetActive(false);

        // Tên hero + thanh máu
        nameText = UIKit.Text(panel.transform, "Name", "Hiệp Sĩ", 26f, Color.white, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(132f, -16f), new Vector2(360f, 36f), TextAlignmentOptions.Left);

        Image hpBg = UIKit.Img(panel.transform, "HpBg", "bar_hp_hero_bg", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(132f, 24f), new Vector2(360f, 44f), true);
        hpFill = UIKit.Img(hpBg.transform, "HpFill", "bar_hp_hero_fill", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(5f, 0f), new Vector2(350f, 34f));
        hpFill.preserveAspect = false;
        hpFill.type = Image.Type.Filled;
        hpFill.fillMethod = Image.FillMethod.Horizontal;
        hpFill.fillAmount = 1f;
        hpText = UIKit.Text(hpBg.transform, "HpText", "", 22f, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(360f, 40f));

        // Nút đưa camera về toàn cảnh (chỉ hiện khi hero đang được focus)
        Image cam = UIKit.Img(canvas.transform, "BtnCameraOverview", "btn_prev", bl, bl, new Vector2(560f, 40f), new Vector2(84f, 84f), false, true);
        UIKit.MakeButton(cam, "btn_prev_pressed", () =>
        {
            if (HeroCameraController.main != null) HeroCameraController.main.ResetCameraToOverview();
            if (targetHero != null) targetHero.isSelected = false;
        });
        cameraButton = cam.gameObject;
        cameraButton.SetActive(false);
    }

    private void Update()
    {
        if (!built) return;

        if (targetHero == null)
        {
            targetHero = Object.FindAnyObjectByType<Hero>();
            if (targetHero == null) return;
        }

        if (targetHero.heroId != lastHeroId)
        {
            lastHeroId = targetHero.heroId;
            string id = string.IsNullOrEmpty(lastHeroId) ? "knight" : lastHeroId.ToLower();
            Sprite sp = UIAssets.Get("portrait_hero_" + id);
            if (sp == null) sp = UIAssets.Get("portrait_hero_knight");
            if (portrait != null && sp != null) portrait.sprite = sp;
            if (nameText != null) nameText.text = lastHeroId;
        }

        float max = targetHero.maxHealth > 0f ? targetHero.maxHealth : 1f;
        if (hpFill != null) hpFill.fillAmount = Mathf.Clamp01(targetHero.currentHealth / max);
        if (hpText != null) hpText.text = ((int)Mathf.Max(0f, targetHero.currentHealth)) + " / " + ((int)targetHero.maxHealth);

        if (deadIcon != null) deadIcon.SetActive(targetHero.isDead);
        if (portrait != null) portrait.color = targetHero.isDead ? new Color(0.4f, 0.4f, 0.4f, 1f) : Color.white;
        if (cameraButton != null) cameraButton.SetActive(targetHero.isSelected);
    }
}
