using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SpellHUD = THANH KỸ NĂNG dưới cùng (theo demo): 5 icon
///   [Chém Q] [Gầm W] [Tiếp viện E] [Meteor] [Freeze]
/// - Mỗi ô là 1 icon, có lớp phủ cooldown dạng quạt + số giây, huy hiệu phím / số lượt.
/// - Không còn nút chữ "METEOR (2 lượt)" ... như trước.
/// </summary>
public class SpellHUD : MonoBehaviour
{
    public static SpellHUD instance;

    public Button skillQButton;
    public TextMeshProUGUI skillQBadge;
    public Button skillWButton;
    public TextMeshProUGUI skillWBadge;
    public Button skillEButton;
    public TextMeshProUGUI skillEBadge;
    public Button meteorButton;
    public TextMeshProUGUI meteorBadge;
    public Button freezeButton;
    public TextMeshProUGUI freezeBadge;

    private class Slot
    {
        public Button button;
        public Image icon;
        public Image cooldownFill;
        public TextMeshProUGUI cooldownText;
        public TextMeshProUGUI badge;
        public float maxCd = 1f;
        public float lastCd;
    }

    private Slot q, w, e, meteor, freeze;
    private RectTransform root;
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

        Transform old = canvas.transform.Find("SpellBar");
        if (old != null) Destroy(old.gameObject);

        root = UIKit.NewRect("SpellBar", canvas.transform);
        Vector2 bc = new Vector2(0.5f, 0f);
        UIKit.Place(root, bc, bc, new Vector2(0f, 18f), new Vector2(800f, 150f));

        q = MakeSlot("Q", "icon_skill_hero1_strike", -320f, "Q", () => { if (HeroController.main != null) HeroController.main.PrepareSkill1(); });
        w = MakeSlot("W", "icon_skill_hero2_aura", -160f, "W", () => { if (HeroController.main != null) HeroController.main.PrepareSkill2(); });
        e = MakeSlot("E", "icon_reinforcements", 0f, "E", () => { if (SpellManager.main != null) SpellManager.main.PrepareReinforcements(); });
        meteor = MakeSlot("Meteor", "icon_spell_meteor", 160f, "x2", () => { if (SpellManager.main != null) SpellManager.main.PrepareMeteor(); });
        freeze = MakeSlot("Freeze", "icon_spell_freeze", 320f, "x1", () => { if (SpellManager.main != null) SpellManager.main.UseFreeze(); });

        skillQButton = q.button; skillQBadge = q.badge;
        skillWButton = w.button; skillWBadge = w.badge;
        skillEButton = e.button; skillEBadge = e.badge;
        meteorButton = meteor.button; meteorBadge = meteor.badge;
        freezeButton = freeze.button; freezeBadge = freeze.badge;
    }

    private Slot MakeSlot(string name, string iconKey, float x, string badgeText, UnityEngine.Events.UnityAction onClick)
    {
        Slot s = new Slot();
        Vector2 bc = new Vector2(0.5f, 0.5f);
        RectTransform holder = UIKit.Box(root, "Slot_" + name, bc, bc, new Vector2(x, 0f), new Vector2(132f, 132f));

        s.icon = UIKit.Img(holder, "Icon", iconKey, bc, bc, Vector2.zero, new Vector2(132f, 132f), false, true);
        s.button = UIKit.MakeButton(s.icon, null, onClick);
        UIKit.AddHoverScale(holder.gameObject, 1.07f);

        s.cooldownFill = UIKit.Img(holder, "Cooldown", "skill_cooldown_overlay", bc, bc, Vector2.zero, new Vector2(132f, 132f), false, false);
        s.cooldownFill.type = Image.Type.Filled;
        s.cooldownFill.fillMethod = Image.FillMethod.Radial360;
        s.cooldownFill.fillOrigin = (int)Image.Origin360.Top;
        s.cooldownFill.fillClockwise = false;
        s.cooldownFill.fillAmount = 0f;
        s.cooldownFill.preserveAspect = false;

        s.cooldownText = UIKit.Text(holder, "CdText", "", 40f, Color.white, bc, bc, Vector2.zero, new Vector2(132f, 60f));

        Vector2 br = new Vector2(1f, 0f);
        Image badge = UIKit.Img(holder, "Badge", "badge_circle", br, br, new Vector2(8f, -8f), new Vector2(44f, 44f));
        s.badge = UIKit.Text(badge.transform, "Txt", badgeText, 22f, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(44f, 44f));
        return s;
    }

    private void Update()
    {
        if (!built) return;

        HeroController hc = HeroController.main;
        if (hc != null && hc.hero != null)
        {
            UpdateCooldown(q, hc.hero.Skill1CooldownRemaining, null);
            UpdateCooldown(w, hc.hero.Skill2CooldownRemaining, null);
        }
        if (SpellManager.main != null)
        {
            UpdateCooldown(e, SpellManager.main.ReinforcementCooldownRemaining, null);
            UpdateUses(meteor, SpellManager.main.meteorUses);
            UpdateUses(freeze, SpellManager.main.freezeUses);
        }
    }

    private void UpdateCooldown(Slot s, float cd, string unused)
    {
        if (s == null) return;
        if (cd > s.lastCd + 0.5f) s.maxCd = cd;
        s.lastCd = cd;

        bool cooling = cd > 0.05f;
        s.cooldownFill.fillAmount = cooling ? Mathf.Clamp01(cd / Mathf.Max(0.1f, s.maxCd)) : 0f;
        s.cooldownText.text = cooling ? Mathf.CeilToInt(cd).ToString() : "";
        s.button.interactable = !cooling;
        s.icon.color = cooling ? new Color(0.6f, 0.6f, 0.6f, 1f) : Color.white;
    }

    private void UpdateUses(Slot s, int uses)
    {
        if (s == null) return;
        s.badge.text = "x" + uses;
        s.button.interactable = uses > 0;
        s.icon.color = uses > 0 ? Color.white : new Color(0.45f, 0.45f, 0.45f, 1f);
        s.cooldownFill.fillAmount = 0f;
    }
}
