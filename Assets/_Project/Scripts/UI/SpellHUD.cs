using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// v2 (SESSION_20261113_B). Tách làm hai phần:
///  1) THANH ITEM (dưới giữa, icon nhỏ): Thiên thạch, Đóng băng, Bình hồi máu Lâu đài, Dầu Sát thương, Dầu Tốc bắn,
///     Dầu Độc, Dầu Xóa cấp Boss (số lượng lấy từ SaveManager như cửa hàng). KHÔNG còn chiêu của tướng.
///  2) STICKER CHIÊU TƯỚNG (mép phải màn hình, icon nhỏ dọc): Đánh thường, Chiêu 1 (Q), Chiêu 2 (W), Kỹ năng đặc biệt
///     (E - Tiếp viện), mỗi ô có lớp phủ hồi chiêu + số giây. Chỉ hiện khi đã có tướng ra trận.
/// Giữ các trường public cũ (skillQButton...) để tương thích.
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
        public RectTransform cdOverlay;
        public TextMeshProUGUI cdText;
        public TextMeshProUGUI badge;
        public float maxCd = 1f;
        public float lastCd;
        public bool hasAction;
        public Color baseColor = Color.white;
    }

    private Slot meteor, freeze, heal, oilDmg, oilRate, oilPoison, oilBoss;
    private Slot basic, q, w, e;
    private RectTransform itemBar;
    private RectTransform heroSticker;
    private bool built;
    private static int healUses = 2;

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
        healUses = 2;

        Canvas canvas = FindUICanvas();
        if (canvas == null) return;

        Transform old = canvas.transform.Find("SpellBar");
        if (old != null) Destroy(old.gameObject);
        old = canvas.transform.Find("HeroSkillSticker");
        if (old != null) Destroy(old.gameObject);

        // ---------------- Thanh item (dưới giữa)
        GameObject bar = new GameObject("SpellBar", typeof(RectTransform));
        bar.transform.SetParent(canvas.transform, false);
        itemBar = bar.GetComponent<RectTransform>();
        itemBar.anchorMin = new Vector2(0.5f, 0f);
        itemBar.anchorMax = new Vector2(0.5f, 0f);
        itemBar.pivot = new Vector2(0.5f, 0f);
        itemBar.anchoredPosition = new Vector2(0f, 12f);
        itemBar.sizeDelta = new Vector2(7 * 66f, 66f);

        float size = 60f, step = 66f, x0 = -3f * step;
        meteor = MakeSlot(itemBar, "Meteor", "icon_spell_meteor", "THIÊN\nTHẠCH", new Color(0.9f, 0.4f, 0.15f), x0 + 0 * step, size, "x2",
            () => { if (SpellManager.main != null) SpellManager.main.PrepareMeteor(); });
        freeze = MakeSlot(itemBar, "Freeze", "icon_spell_freeze", "BĂNG", new Color(0.4f, 0.75f, 1f), x0 + 1 * step, size, "x1",
            () => { if (SpellManager.main != null) SpellManager.main.UseFreeze(); });
        heal = MakeSlot(itemBar, "HealCastle", "icon_heart", "HỒI\nMÁU", new Color(0.9f, 0.25f, 0.35f), x0 + 2 * step, size, "x2", UseHealCastle);
        oilDmg = MakeSlot(itemBar, "OilDamage", "item_potion_red", "DẦU\nST", new Color(0.85f, 0.2f, 0.2f), x0 + 3 * step, size, "x0",
            () => { EnsureOil(); if (ConsumableOilManager.main != null) ConsumableOilManager.main.UseDamageOil(); });
        oilRate = MakeSlot(itemBar, "OilFireRate", "item_potion_blue", "DẦU\nBẮN", new Color(0.25f, 0.5f, 0.95f), x0 + 4 * step, size, "x0",
            () => { EnsureOil(); if (ConsumableOilManager.main != null) ConsumableOilManager.main.UseFireRateOil(); });
        oilPoison = MakeSlot(itemBar, "OilPoison", "item_potion_green", "DẦU\nĐỘC", new Color(0.25f, 0.75f, 0.35f), x0 + 5 * step, size, "x0",
            () => { EnsureOil(); if (ConsumableOilManager.main != null) ConsumableOilManager.main.UsePoisonOil(); });
        oilBoss = MakeSlot(itemBar, "OilBoss", "icon_skull_kills", "DẦU\nBOSS", new Color(0.6f, 0.3f, 0.75f), x0 + 6 * step, size, "x0",
            () => { EnsureOil(); if (ConsumableOilManager.main != null) ConsumableOilManager.main.UseBossLevelReductionOil(); });

        meteorButton = meteor.button; meteorBadge = meteor.badge;
        freezeButton = freeze.button; freezeBadge = freeze.badge;

        // ---------------- Sticker chiêu tướng (mép phải, dọc)
        GameObject st = new GameObject("HeroSkillSticker", typeof(RectTransform));
        st.transform.SetParent(canvas.transform, false);
        heroSticker = st.GetComponent<RectTransform>();
        heroSticker.anchorMin = new Vector2(1f, 0.5f);
        heroSticker.anchorMax = new Vector2(1f, 0.5f);
        heroSticker.pivot = new Vector2(1f, 0.5f);
        heroSticker.anchoredPosition = new Vector2(-8f, 20f);
        heroSticker.sizeDelta = new Vector2(54f, 4 * 58f);

        float ss = 52f, sstep = 58f, y0 = 1.5f * sstep;
        basic = MakeStickerSlot(heroSticker, "Basic", "item_weapon_sword", "ĐÁNH", new Color(0.8f, 0.8f, 0.85f), y0, ss, "", null);
        q = MakeStickerSlot(heroSticker, "Q", "icon_skill_hero1_strike", "CHIÊU\n1", new Color(0.95f, 0.6f, 0.2f), y0 - sstep, ss, "",
            () => { if (HeroController.main != null) HeroController.main.PrepareSkill1(); });
        w = MakeStickerSlot(heroSticker, "W", "icon_skill_hero2_aura", "CHIÊU\n2", new Color(0.7f, 0.4f, 0.9f), y0 - 2 * sstep, ss, "",
            () => { if (HeroController.main != null) HeroController.main.PrepareSkill2(); });
        e = MakeStickerSlot(heroSticker, "E", "icon_reinforcements", "ĐẶC\nBIỆT", new Color(0.3f, 0.7f, 0.95f), y0 - 3 * sstep, ss, "",
            () => { if (SpellManager.main != null) SpellManager.main.PrepareReinforcements(); });

        skillQButton = q.button; skillQBadge = q.badge;
        skillWButton = w.button; skillWBadge = w.badge;
        skillEButton = e.button; skillEBadge = e.badge;
        heroSticker.gameObject.SetActive(false);
    }

    private void EnsureOil()
    {
        if (ConsumableOilManager.main == null)
        {
            new GameObject("ConsumableOilManager").AddComponent<ConsumableOilManager>();
        }
    }

    private void UseHealCastle()
    {
        if (healUses <= 0) return;
        CastleManager cm = CastleManager.main;
        if (cm == null || cm.IsDestroyed || cm.currentHealth >= cm.maxHealth) return;
        cm.Heal(cm.maxHealth * 0.25f);
        healUses--;
    }

    // ------------------------------------------------------------------ slot factory

    private Slot MakeSlot(Transform parent, string name, string iconKey, string fallbackLabel, Color fallbackColor,
                          float x, float size, string badgeText, UnityEngine.Events.UnityAction onClick)
    {
        return BuildSlot(parent, name, iconKey, fallbackLabel, fallbackColor,
            new Vector2(0.5f, 0.5f), new Vector2(x, 0f), size, badgeText, onClick);
    }

    private Slot MakeStickerSlot(Transform parent, string name, string iconKey, string fallbackLabel, Color fallbackColor,
                                 float y, float size, string badgeText, UnityEngine.Events.UnityAction onClick)
    {
        return BuildSlot(parent, name, iconKey, fallbackLabel, fallbackColor,
            new Vector2(1f, 0.5f), new Vector2(0f, y), size, badgeText, onClick);
    }

    private Slot BuildSlot(Transform parent, string name, string iconKey, string fallbackLabel, Color fallbackColor,
                           Vector2 anchor, Vector2 pos, float size, string badgeText, UnityEngine.Events.UnityAction onClick)
    {
        Slot s = new Slot();

        GameObject holder = new GameObject("Slot_" + name, typeof(RectTransform));
        holder.transform.SetParent(parent, false);
        RectTransform hr = holder.GetComponent<RectTransform>();
        hr.anchorMin = anchor;
        hr.anchorMax = anchor;
        hr.pivot = anchor.x > 0.9f ? new Vector2(1f, 0.5f) : new Vector2(0.5f, 0.5f);
        hr.anchoredPosition = pos;
        hr.sizeDelta = new Vector2(size, size);
        Image bg = holder.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.1f, 0.14f, 0.9f);
        Outline ol = holder.AddComponent<Outline>();
        ol.effectColor = new Color(0.85f, 0.7f, 0.25f, 0.65f);
        ol.effectDistance = new Vector2(1f, -1f);
        s.button = holder.AddComponent<Button>();
        s.button.targetGraphic = bg;
        s.hasAction = onClick != null;
        if (onClick != null) s.button.onClick.AddListener(onClick);
        else s.button.interactable = false;

        // Icon (có sprite thì dùng sprite; thiếu sprite thì ô màu + nhãn chữ để luôn đọc được)
        GameObject iconObj = new GameObject("Icon", typeof(RectTransform));
        iconObj.transform.SetParent(holder.transform, false);
        s.icon = iconObj.AddComponent<Image>();
        s.icon.raycastTarget = false;
        s.icon.preserveAspect = true;
        RectTransform ir = iconObj.GetComponent<RectTransform>();
        ir.anchorMin = Vector2.zero;
        ir.anchorMax = Vector2.one;
        ir.offsetMin = new Vector2(4f, 4f);
        ir.offsetMax = new Vector2(-4f, -4f);
        Sprite spr = HudIcons.Get(iconKey);
        if (spr != null)
        {
            s.icon.sprite = spr;
            s.baseColor = Color.white;
            s.icon.color = Color.white;
        }
        else
        {
            s.baseColor = fallbackColor;
            s.icon.color = fallbackColor;
            TextMeshProUGUI lab = NewText(iconObj.transform, "Label", fallbackLabel, size * 0.2f, Color.white, TextAlignmentOptions.Center);
            lab.fontStyle = FontStyles.Bold;
            Stretch(lab.rectTransform);
        }

        // Lớp phủ hồi chiêu: thanh tối cao dần theo thời gian còn lại (không cần sprite)
        GameObject cdObj = new GameObject("Cooldown", typeof(RectTransform));
        cdObj.transform.SetParent(holder.transform, false);
        Image cdImg = cdObj.AddComponent<Image>();
        cdImg.color = new Color(0f, 0f, 0f, 0.62f);
        cdImg.raycastTarget = false;
        s.cdOverlay = cdObj.GetComponent<RectTransform>();
        s.cdOverlay.anchorMin = new Vector2(0f, 0f);
        s.cdOverlay.anchorMax = new Vector2(1f, 0f);
        s.cdOverlay.offsetMin = Vector2.zero;
        s.cdOverlay.offsetMax = Vector2.zero;

        s.cdText = NewText(holder.transform, "CdText", "", size * 0.42f, Color.white, TextAlignmentOptions.Center);
        s.cdText.fontStyle = FontStyles.Bold;
        Stretch(s.cdText.rectTransform);

        s.badge = NewText(holder.transform, "Badge", badgeText, size * 0.24f, new Color(1f, 0.93f, 0.55f), TextAlignmentOptions.BottomRight);
        s.badge.fontStyle = FontStyles.Bold;
        Stretch(s.badge.rectTransform);
        s.badge.rectTransform.offsetMin = new Vector2(2f, 1f);
        s.badge.rectTransform.offsetMax = new Vector2(-3f, -1f);
        return s;
    }

    private static TextMeshProUGUI NewText(Transform parent, string name, string text, float fontSize, Color color, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        return t;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // ------------------------------------------------------------------ update

    private void Update()
    {
        if (!built) return;

        // Sticker chiêu tướng: chỉ hiện khi có tướng ra trận
        HeroController hc = HeroController.main;
        bool hasHero = hc != null && hc.hero != null;
        if (heroSticker != null && heroSticker.gameObject.activeSelf != hasHero) heroSticker.gameObject.SetActive(hasHero);
        if (hasHero)
        {
            Hero h = hc.hero;
            UpdateCooldown(basic, h.AttackCooldownRemaining, h.IsAlive);
            UpdateCooldown(q, h.Skill1CooldownRemaining, h.IsAlive);
            UpdateCooldown(w, h.Skill2CooldownRemaining, h.IsAlive);
        }
        if (SpellManager.main != null)
        {
            if (hasHero) UpdateCooldown(e, SpellManager.main.ReinforcementCooldownRemaining, true);
            UpdateUses(meteor, SpellManager.main.meteorUses);
            UpdateUses(freeze, SpellManager.main.freezeUses);
        }

        CastleManager cm = CastleManager.main;
        bool canHeal = healUses > 0 && cm != null && !cm.IsDestroyed && cm.currentHealth < cm.maxHealth;
        UpdateUses(heal, healUses);
        if (heal != null) heal.button.interactable = canHeal;

        SaveManager sm = SaveManager.instance;
        ConsumableOilManager om = ConsumableOilManager.main;
        UpdateOil(oilDmg, sm != null ? sm.DamageOils : 0, om != null ? om.damageOilRemaining : 0f);
        UpdateOil(oilRate, sm != null ? sm.FireRateOils : 0, om != null ? om.fireRateOilRemaining : 0f);
        UpdateOil(oilPoison, sm != null ? sm.PoisonOils : 0, om != null ? om.poisonOilRemaining : 0f);
        UpdateOil(oilBoss, sm != null ? sm.BossReduceOils : 0, 0f);
    }

    private static Color Tint(Slot s, float f)
    {
        Color c = s.baseColor * f;
        c.a = 1f;
        return c;
    }

    private void SetOverlay(Slot s, float ratio)
    {
        s.cdOverlay.anchorMax = new Vector2(1f, Mathf.Clamp01(ratio));
    }

    private void UpdateCooldown(Slot s, float cd, bool alive)
    {
        if (s == null) return;
        if (cd > s.lastCd + 0.5f) s.maxCd = cd;
        s.lastCd = cd;

        bool cooling = cd > 0.05f;
        SetOverlay(s, cooling ? cd / Mathf.Max(0.1f, s.maxCd) : 0f);
        s.cdText.text = cooling ? (cd >= 10f ? Mathf.CeilToInt(cd).ToString() : cd.ToString("F1")) : "";
        s.button.interactable = s.hasAction && !cooling && alive;
        s.icon.color = (cooling || !alive) ? Tint(s, 0.65f) : s.baseColor;
    }

    private void UpdateUses(Slot s, int uses)
    {
        if (s == null) return;
        s.badge.text = "x" + uses;
        s.button.interactable = uses > 0;
        s.icon.color = uses > 0 ? s.baseColor : Tint(s, 0.45f);
        SetOverlay(s, 0f);
        s.cdText.text = "";
    }

    private void UpdateOil(Slot s, int count, float activeRemaining)
    {
        if (s == null) return;
        s.badge.text = "x" + count;
        bool active = activeRemaining > 0.05f;
        s.cdText.text = active ? Mathf.CeilToInt(activeRemaining).ToString() : "";
        s.cdText.color = new Color(0.55f, 1f, 0.55f);
        SetOverlay(s, 0f);
        s.button.interactable = count > 0 && !active;
        s.icon.color = (count > 0) ? s.baseColor : Tint(s, 0.45f);
    }
}
