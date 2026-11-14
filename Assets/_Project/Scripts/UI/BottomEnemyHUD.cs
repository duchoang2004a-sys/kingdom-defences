using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// v2 (SESSION_20261113_B): bảng quái thu gọn thành MỘT icon (icon_enemy_roster) ở góc dưới phải.
/// Bấm icon -> hàng ngang nhỏ (trái sang phải) gồm icon quái + tên bên dưới, chỉ gồm các loại quái đã được gọi spawn.
/// Bấm một icon (hoặc bấm trực tiếp quái trên map) -> thẻ chi tiết. Giữ API cũ: main, TrackEnemy, RefreshWaveInfo.
/// </summary>
public class BottomEnemyHUD : MonoBehaviour
{
    public static BottomEnemyHUD main;

    private class Entry
    {
        public string name;
        public Sprite icon;
        public Color tint = Color.white;
        public float hp, speed, armor, magicRes, statusRes, combat, reward;
        public int castleDamage = 1;
        public bool boss;
        public int spawned;
        public int alive;
    }

    private readonly List<Entry> entries = new List<Entry>();
    private readonly Dictionary<string, Entry> byName = new Dictionary<string, Entry>();

    private GameObject root;
    private RectTransform stripRt;
    private GameObject stripObj;
    private GameObject detailObj;
    private Image detailIcon;
    private TextMeshProUGUI detailTitle;
    private TextMeshProUGUI detailBody;
    private TextMeshProUGUI badgeText;
    private Entry shown;
    private float refreshTimer;

    private void Awake()
    {
        main = this;
        EnsureUIBuilt();
    }

    private void Start()
    {
        EnsureUIBuilt();
        RefreshWaveInfo();
    }

    private void Update()
    {
        // Bấm vào quái trên map -> mở thẻ chi tiết của loại quái đó
        if (GameInput.GetMouseButtonDown(0))
        {
            if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                return;

            Vector2 mousePos = Camera.main != null ? (Vector2)Camera.main.ScreenToWorldPoint(GameInput.MousePosition) : Vector2.zero;
            RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero);
            if (hit.collider != null)
            {
                // GetComponentInParent gồm cả chính object; không dùng '??' vì GetComponent trả "fake null" trong Editor
                EnemyHealth eh = hit.collider.GetComponentInParent<EnemyHealth>();
                if (eh != null)
                {
                    Entry e = Register(eh, false);
                    if (e != null) ShowDetail(e);
                }
            }
        }

        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer <= 0f)
        {
            refreshTimer = 0.5f;
            RefreshWaveInfo();
        }
    }

    /// <summary>EnemySpawner gọi mỗi lần gọi quái ra: ghi nhận loại quái (lần đầu tạo icon trong roster).</summary>
    public void TrackEnemy(EnemyHealth enemyHealth)
    {
        if (enemyHealth == null) return;
        EnsureUIBuilt();
        Register(enemyHealth, true);
        RefreshWaveInfo();
    }

    private Entry Register(EnemyHealth eh, bool countSpawn)
    {
        Enemy enemy = eh.GetComponent<Enemy>();
        string key = enemy != null && !string.IsNullOrEmpty(enemy.EnemyName) ? enemy.EnemyName : eh.gameObject.name.Replace("(Clone)", "").Trim();

        Entry e;
        if (!byName.TryGetValue(key, out e))
        {
            e = new Entry { name = key };
            e.hp = eh.MaxHealth;
            if (enemy != null)
            {
                e.speed = enemy.MoveSpeed;
                e.armor = enemy.Armor;
                e.magicRes = enemy.MagicResist;
                e.statusRes = enemy.StatusResist;
                e.combat = enemy.GetCombatDamage();
                e.reward = enemy.GoldReward;
                e.boss = enemy.IsBoss;
                e.castleDamage = enemy.Data != null && enemy.Data.Damage > 0 ? enemy.Data.Damage : 1;
            }
            SpriteRenderer sr = eh.GetComponentInChildren<SpriteRenderer>();   // gồm cả chính object
            if (sr != null) { e.icon = sr.sprite; e.tint = sr.color; }
            byName[key] = e;
            entries.Add(e);
            AddChip(e);
        }
        if (countSpawn) e.spawned++;
        return e;
    }

    public void RefreshWaveInfo()
    {
        if (badgeText == null) return;
        int alive = EnemyRegistry.ActiveCount;
        badgeText.text = alive.ToString();

        if (stripObj != null && stripObj.activeSelf || detailObj != null && detailObj.activeSelf)
        {
            for (int i = 0; i < entries.Count; i++) entries[i].alive = 0;
            var list = EnemyRegistry.ActiveEnemies;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null) continue;
                Enemy en = list[i].GetComponent<Enemy>();
                string key = en != null && !string.IsNullOrEmpty(en.EnemyName) ? en.EnemyName : list[i].gameObject.name.Replace("(Clone)", "").Trim();
                Entry e;
                if (byName.TryGetValue(key, out e)) e.alive++;
            }
            if (shown != null && detailObj != null && detailObj.activeSelf) FillDetail(shown);
        }
    }

    // ------------------------------------------------------------------ UI

    private void ToggleStrip()
    {
        if (stripObj == null) return;
        bool open = !stripObj.activeSelf;
        stripObj.SetActive(open);
        if (!open && detailObj != null) detailObj.SetActive(false);
        RefreshWaveInfo();
    }

    private void ShowDetail(Entry e)
    {
        if (detailObj == null || e == null) return;
        shown = e;
        FillDetail(e);
        detailObj.SetActive(true);
    }

    private void FillDetail(Entry e)
    {
        if (detailIcon != null)
        {
            detailIcon.sprite = e.icon;
            detailIcon.color = e.tint;
            detailIcon.enabled = e.icon != null;
        }
        if (detailTitle != null) detailTitle.text = (e.boss ? "[BOSS] " : "") + e.name.ToUpper();
        if (detailBody != null)
        {
            detailBody.text =
                "Máu: <color=#ff8a8a>" + Mathf.CeilToInt(e.hp) + "</color>\n" +
                "Tốc độ: <color=#9fe0ff>" + e.speed.ToString("F1") + "</color>\n" +
                "Giáp: " + Mathf.RoundToInt(e.armor * 100f) + "%   Kháng phép: " + Mathf.RoundToInt(e.magicRes * 100f) + "%\n" +
                "Kháng khống chế: " + Mathf.RoundToInt(e.statusRes * 100f) + "%\n" +
                "Sát thương đấu tay: " + Mathf.RoundToInt(e.combat) + "\n" +
                "Trừ máu Lâu đài: <color=#ffb36b>" + e.castleDamage + "</color>\n" +
                "Thưởng: <color=#ffe066>+" + Mathf.RoundToInt(e.reward) + "G</color>\n" +
                "Đang trên đường: " + e.alive + "   |   Đã gọi: " + e.spawned;
        }
    }

    private void AddChip(Entry e)
    {
        if (stripRt == null) return;

        GameObject chip = new GameObject("Chip_" + e.name, typeof(RectTransform));
        chip.transform.SetParent(stripRt, false);
        Image bg = chip.AddComponent<Image>();
        bg.color = new Color(1f, 1f, 1f, 0.07f);
        LayoutElement le = chip.AddComponent<LayoutElement>();
        le.preferredWidth = 62f;
        le.preferredHeight = 66f;
        Button btn = chip.AddComponent<Button>();
        btn.targetGraphic = bg;
        Entry captured = e;
        btn.onClick.AddListener(() => ShowDetail(captured));

        GameObject iconObj = new GameObject("Icon", typeof(RectTransform));
        iconObj.transform.SetParent(chip.transform, false);
        Image icon = iconObj.AddComponent<Image>();
        icon.sprite = e.icon;
        icon.color = e.tint;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        RectTransform ir = iconObj.GetComponent<RectTransform>();
        ir.anchorMin = new Vector2(0.5f, 1f);
        ir.anchorMax = new Vector2(0.5f, 1f);
        ir.pivot = new Vector2(0.5f, 1f);
        ir.anchoredPosition = new Vector2(0f, -3f);
        ir.sizeDelta = new Vector2(42f, 42f);

        GameObject nameObj = new GameObject("Name", typeof(RectTransform));
        nameObj.transform.SetParent(chip.transform, false);
        TextMeshProUGUI t = nameObj.AddComponent<TextMeshProUGUI>();
        t.text = e.name;
        t.fontSize = 10;
        t.alignment = TextAlignmentOptions.Center;
        t.color = e.boss ? new Color(1f, 0.6f, 0.5f) : new Color(0.95f, 0.95f, 0.95f);
        t.overflowMode = TextOverflowModes.Ellipsis;
        t.raycastTarget = false;
        RectTransform nr = nameObj.GetComponent<RectTransform>();
        nr.anchorMin = new Vector2(0f, 0f);
        nr.anchorMax = new Vector2(1f, 0f);
        nr.pivot = new Vector2(0.5f, 0f);
        nr.anchoredPosition = new Vector2(0f, 1f);
        nr.sizeDelta = new Vector2(0f, 18f);
    }

    /// <summary>Image không có handler sẽ để click "nổi" lên Button của nút roster (cha) -> bấm nền thẻ làm đóng/mở roster. Button rỗng này chặn lại.</summary>
    private static void AbsorbClicks(GameObject go)
    {
        Button b = go.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        Navigation nav = new Navigation();
        nav.mode = Navigation.Mode.None;
        b.navigation = nav;
    }

    private void EnsureUIBuilt()
    {
        if (root != null) return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null) return;
        }

        // Nút icon roster: góc dưới phải
        root = new GameObject("EnemyRoster_HUD", typeof(RectTransform));
        root.transform.SetParent(canvas.transform, false);
        RectTransform rr = root.GetComponent<RectTransform>();
        rr.anchorMin = new Vector2(1f, 0f);
        rr.anchorMax = new Vector2(1f, 0f);
        rr.pivot = new Vector2(1f, 0f);
        rr.anchoredPosition = new Vector2(-12f, 12f);
        rr.sizeDelta = new Vector2(54f, 54f);

        Image btnImg = root.AddComponent<Image>();
        Sprite iconSpr = HudIcons.Get("icon_enemy_roster");
        btnImg.sprite = iconSpr;
        btnImg.preserveAspect = true;
        btnImg.color = iconSpr != null ? Color.white : new Color(0.5f, 0.2f, 0.2f, 0.95f);
        Button btn = root.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        btn.onClick.AddListener(ToggleStrip);

        // Huy hiệu số quái đang trên đường
        GameObject badge = new GameObject("Badge", typeof(RectTransform));
        badge.transform.SetParent(root.transform, false);
        Image bdg = badge.AddComponent<Image>();
        bdg.color = new Color(0.8f, 0.15f, 0.15f, 0.95f);
        bdg.raycastTarget = false;
        RectTransform br = badge.GetComponent<RectTransform>();
        br.anchorMin = new Vector2(1f, 1f);
        br.anchorMax = new Vector2(1f, 1f);
        br.pivot = new Vector2(0.5f, 0.5f);
        br.anchoredPosition = new Vector2(-4f, -4f);
        br.sizeDelta = new Vector2(22f, 22f);
        GameObject bt = new GameObject("Text", typeof(RectTransform));
        bt.transform.SetParent(badge.transform, false);
        badgeText = bt.AddComponent<TextMeshProUGUI>();
        badgeText.text = "0";
        badgeText.fontSize = 12;
        badgeText.fontStyle = FontStyles.Bold;
        badgeText.alignment = TextAlignmentOptions.Center;
        badgeText.color = Color.white;
        badgeText.raycastTarget = false;
        RectTransform btr = bt.GetComponent<RectTransform>();
        btr.anchorMin = Vector2.zero;
        btr.anchorMax = Vector2.one;
        btr.sizeDelta = Vector2.zero;

        // Hàng ngang icon + tên (mở rộng sang trái của nút, xếp từ trái sang phải)
        stripObj = new GameObject("Strip", typeof(RectTransform));
        stripObj.transform.SetParent(root.transform, false);
        stripRt = stripObj.GetComponent<RectTransform>();
        stripRt.anchorMin = new Vector2(0f, 0f);
        stripRt.anchorMax = new Vector2(0f, 0f);
        stripRt.pivot = new Vector2(1f, 0f);
        stripRt.anchoredPosition = new Vector2(-8f, 0f);
        Image sbg = stripObj.AddComponent<Image>();
        sbg.color = new Color(0.08f, 0.1f, 0.14f, 0.9f);
        Outline so = stripObj.AddComponent<Outline>();
        so.effectColor = new Color(0.85f, 0.7f, 0.25f, 0.6f);
        so.effectDistance = new Vector2(1f, -1f);
        AbsorbClicks(stripObj);
        HorizontalLayoutGroup hl = stripObj.AddComponent<HorizontalLayoutGroup>();
        hl.padding = new RectOffset(6, 6, 6, 6);
        hl.spacing = 4f;
        hl.childAlignment = TextAnchor.UpperLeft;
        hl.childControlWidth = true;
        hl.childControlHeight = true;
        hl.childForceExpandWidth = false;
        hl.childForceExpandHeight = false;
        ContentSizeFitter fit = stripObj.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        stripObj.SetActive(false);

        // Thẻ chi tiết quái (phía trên nút)
        detailObj = new GameObject("Detail", typeof(RectTransform));
        detailObj.transform.SetParent(root.transform, false);
        RectTransform dr = detailObj.GetComponent<RectTransform>();
        dr.anchorMin = new Vector2(1f, 1f);
        dr.anchorMax = new Vector2(1f, 1f);
        dr.pivot = new Vector2(1f, 0f);
        dr.anchoredPosition = new Vector2(0f, 10f);
        dr.sizeDelta = new Vector2(300f, 210f);
        Image dbg = detailObj.AddComponent<Image>();
        dbg.color = new Color(0.08f, 0.1f, 0.14f, 0.96f);
        Outline dol = detailObj.AddComponent<Outline>();
        dol.effectColor = new Color(0.85f, 0.7f, 0.25f, 0.8f);
        dol.effectDistance = new Vector2(1.5f, -1.5f);
        AbsorbClicks(detailObj);

        GameObject di = new GameObject("Icon", typeof(RectTransform));
        di.transform.SetParent(detailObj.transform, false);
        detailIcon = di.AddComponent<Image>();
        detailIcon.preserveAspect = true;
        detailIcon.raycastTarget = false;
        RectTransform dir = di.GetComponent<RectTransform>();
        dir.anchorMin = new Vector2(0f, 1f);
        dir.anchorMax = new Vector2(0f, 1f);
        dir.pivot = new Vector2(0f, 1f);
        dir.anchoredPosition = new Vector2(12f, -12f);
        dir.sizeDelta = new Vector2(64f, 64f);

        GameObject dt = new GameObject("Title", typeof(RectTransform));
        dt.transform.SetParent(detailObj.transform, false);
        detailTitle = dt.AddComponent<TextMeshProUGUI>();
        detailTitle.fontSize = 17;
        detailTitle.fontStyle = FontStyles.Bold;
        detailTitle.color = new Color(1f, 0.9f, 0.4f);
        detailTitle.alignment = TextAlignmentOptions.MidlineLeft;
        detailTitle.raycastTarget = false;
        RectTransform dtr = dt.GetComponent<RectTransform>();
        dtr.anchorMin = new Vector2(0f, 1f);
        dtr.anchorMax = new Vector2(1f, 1f);
        dtr.pivot = new Vector2(0f, 1f);
        dtr.anchoredPosition = new Vector2(88f, -12f);
        dtr.sizeDelta = new Vector2(-130f, 64f);

        GameObject db = new GameObject("Body", typeof(RectTransform));
        db.transform.SetParent(detailObj.transform, false);
        detailBody = db.AddComponent<TextMeshProUGUI>();
        detailBody.fontSize = 13;
        detailBody.color = new Color(0.88f, 0.92f, 0.96f);
        detailBody.alignment = TextAlignmentOptions.TopLeft;
        detailBody.raycastTarget = false;
        RectTransform dbr = db.GetComponent<RectTransform>();
        dbr.anchorMin = new Vector2(0f, 0f);
        dbr.anchorMax = new Vector2(1f, 1f);
        dbr.offsetMin = new Vector2(14f, 10f);
        dbr.offsetMax = new Vector2(-14f, -82f);

        GameObject close = new GameObject("Close", typeof(RectTransform));
        close.transform.SetParent(detailObj.transform, false);
        Image cimg = close.AddComponent<Image>();
        cimg.color = new Color(0.6f, 0.15f, 0.15f, 0.95f);
        Button cb = close.AddComponent<Button>();
        cb.targetGraphic = cimg;
        cb.onClick.AddListener(() => detailObj.SetActive(false));
        RectTransform cr = close.GetComponent<RectTransform>();
        cr.anchorMin = new Vector2(1f, 1f);
        cr.anchorMax = new Vector2(1f, 1f);
        cr.pivot = new Vector2(1f, 1f);
        cr.anchoredPosition = new Vector2(-8f, -8f);
        cr.sizeDelta = new Vector2(26f, 26f);
        GameObject ct = new GameObject("X", typeof(RectTransform));
        ct.transform.SetParent(close.transform, false);
        TextMeshProUGUI cx = ct.AddComponent<TextMeshProUGUI>();
        cx.text = "X";
        cx.fontSize = 14;
        cx.fontStyle = FontStyles.Bold;
        cx.alignment = TextAlignmentOptions.Center;
        cx.color = Color.white;
        cx.raycastTarget = false;
        RectTransform cxr = ct.GetComponent<RectTransform>();
        cxr.anchorMin = Vector2.zero;
        cxr.anchorMax = Vector2.one;
        cxr.sizeDelta = Vector2.zero;
        detailObj.SetActive(false);
    }
}
