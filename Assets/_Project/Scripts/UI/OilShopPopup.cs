using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Giao diện Popup Cửa Hàng Dầu & Gems (Gem Shop & Consumable Oils UI):
/// - Cho phép người chơi dùng Gems mua hoặc kích hoạt trực tiếp các loại Dầu tăng cường trong trận chiến.
/// </summary>
public class OilShopPopup : MonoBehaviour
{
    public static OilShopPopup instance;

    private GameObject popupPanel;
    private TextMeshProUGUI gemsText;

    private TextMeshProUGUI damageOilQtyText;
    private TextMeshProUGUI fireRateOilQtyText;
    private TextMeshProUGUI poisonOilQtyText;
    private TextMeshProUGUI bossReduceOilQtyText;

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        EnsureUI();
    }

    public static void ShowShop()
    {
        if (instance == null)
        {
            GameObject obj = new GameObject("OilShopPopup_Manager");
            instance = obj.AddComponent<OilShopPopup>();
        }

        instance.EnsureUI();
        if (instance.popupPanel != null)
        {
            instance.popupPanel.SetActive(true);
            instance.RefreshUI();
        }
    }

    public static void HideShop()
    {
        if (instance != null && instance.popupPanel != null)
        {
            instance.popupPanel.SetActive(false);
        }
    }

    public void EnsureUI()
    {
        if (popupPanel != null) return;

        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        // Modal Background Dark Overlay
        popupPanel = new GameObject("OilShopPopup_Panel");
        popupPanel.transform.SetParent(canvas.transform, false);

        RectTransform panelRt = popupPanel.AddComponent<RectTransform>();
        panelRt.anchorMin = Vector2.zero;
        panelRt.anchorMax = Vector2.one;
        panelRt.sizeDelta = Vector2.zero;

        Image panelBg = popupPanel.AddComponent<Image>();
        panelBg.color = new Color(0f, 0f, 0f, 0.75f);

        // Center Modal Container
        GameObject dialog = new GameObject("DialogContainer");
        dialog.transform.SetParent(popupPanel.transform, false);

        RectTransform diagRt = dialog.AddComponent<RectTransform>();
        diagRt.anchorMin = new Vector2(0.5f, 0.5f);
        diagRt.anchorMax = new Vector2(0.5f, 0.5f);
        diagRt.sizeDelta = new Vector2(580f, 380f);

        Image diagBg = dialog.AddComponent<Image>();
        diagBg.color = new Color(0.12f, 0.14f, 0.20f, 0.98f);

        // Header Title & Gems
        GameObject headerObj = new GameObject("HeaderTitle");
        headerObj.transform.SetParent(dialog.transform, false);
        TextMeshProUGUI titleTxt = headerObj.AddComponent<TextMeshProUGUI>();
        titleTxt.text = "🛢️ CỬA HÀNG DẦU TĂNG CƯỜNG (GEM SHOP)";
        titleTxt.fontSize = 16;
        titleTxt.fontStyle = FontStyles.Bold;
        titleTxt.alignment = TextAlignmentOptions.Center;
        titleTxt.color = new Color(1f, 0.85f, 0.3f);
        RectTransform titleRt = headerObj.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.05f, 0.88f);
        titleRt.anchorMax = new Vector2(0.70f, 0.98f);
        titleRt.sizeDelta = Vector2.zero;

        GameObject gemsObj = new GameObject("GemsDisplay");
        gemsObj.transform.SetParent(dialog.transform, false);
        gemsText = gemsObj.AddComponent<TextMeshProUGUI>();
        gemsText.fontSize = 15;
        gemsText.fontStyle = FontStyles.Bold;
        gemsText.alignment = TextAlignmentOptions.Right;
        gemsText.color = new Color(0.4f, 0.9f, 1f);
        RectTransform gemsRt = gemsObj.GetComponent<RectTransform>();
        gemsRt.anchorMin = new Vector2(0.68f, 0.88f);
        gemsRt.anchorMax = new Vector2(0.88f, 0.98f);
        gemsRt.sizeDelta = Vector2.zero;

        // Close Button (X)
        GameObject closeObj = new GameObject("Btn_Close");
        closeObj.transform.SetParent(dialog.transform, false);
        RectTransform closeRt = closeObj.AddComponent<RectTransform>();
        closeRt.anchorMin = new Vector2(0.92f, 0.88f);
        closeRt.anchorMax = new Vector2(0.98f, 0.98f);
        closeRt.sizeDelta = Vector2.zero;
        Image closeImg = closeObj.AddComponent<Image>();
        closeImg.color = new Color(0.8f, 0.2f, 0.2f);
        Button closeBtn = closeObj.AddComponent<Button>();
        closeBtn.onClick.AddListener(HideShop);

        GameObject closeTxtObj = new GameObject("Label");
        closeTxtObj.transform.SetParent(closeObj.transform, false);
        TextMeshProUGUI closeTxt = closeTxtObj.AddComponent<TextMeshProUGUI>();
        closeTxt.text = "✕";
        closeTxt.fontSize = 16;
        closeTxt.alignment = TextAlignmentOptions.Center;
        closeTxt.color = Color.white;
        RectTransform cTxtRt = closeTxtObj.GetComponent<RectTransform>();
        cTxtRt.anchorMin = Vector2.zero;
        cTxtRt.anchorMax = Vector2.one;
        cTxtRt.sizeDelta = Vector2.zero;

        // Grid of 4 Oils
        CreateOilRow(dialog.transform, 0, "🔥 Dầu Sát Thương", "+30% DMG Tháp (20s)", 50,
            () => BuyOil(1, 50), () => UseOilInGame(1), out damageOilQtyText);

        CreateOilRow(dialog.transform, 1, "⚡ Dầu Tốc Độ Bắn", "+40% Tốc Bắn (20s)", 50,
            () => BuyOil(2, 50), () => UseOilInGame(2), out fireRateOilQtyText);

        CreateOilRow(dialog.transform, 2, "🧪 Dầu Độc Tố", "Tẩm Độc DoT (20s)", 60,
            () => BuyOil(3, 60), () => UseOilInGame(3), out poisonOilQtyText);

        CreateOilRow(dialog.transform, 3, "👑 Dầu Xóa Cấp Boss", "-1 Cấp Boss & -25% HP", 100,
            () => BuyOil(4, 100), () => UseOilInGame(4), out bossReduceOilQtyText);

        popupPanel.SetActive(false);
    }

    private void CreateOilRow(Transform parent, int index, string title, string desc, int price, UnityEngine.Events.UnityAction onBuy, UnityEngine.Events.UnityAction onUse, out TextMeshProUGUI qtyTxt)
    {
        GameObject row = new GameObject($"OilRow_{index}");
        row.transform.SetParent(parent, false);

        RectTransform rt = row.AddComponent<RectTransform>();
        float yMax = 0.82f - (index * 0.18f);
        float yMin = yMax - 0.16f;
        rt.anchorMin = new Vector2(0.04f, yMin);
        rt.anchorMax = new Vector2(0.96f, yMax);
        rt.sizeDelta = Vector2.zero;

        Image bg = row.AddComponent<Image>();
        bg.color = new Color(0.18f, 0.22f, 0.32f, 0.9f);

        // Text Info
        GameObject infoObj = new GameObject("Info");
        infoObj.transform.SetParent(row.transform, false);
        TextMeshProUGUI infoTxt = infoObj.AddComponent<TextMeshProUGUI>();
        infoTxt.text = $"<b>{title}</b>\n<size=11><color=#cccccc>{desc}</color></size>";
        infoTxt.fontSize = 13;
        infoTxt.color = Color.white;
        RectTransform infoRt = infoObj.GetComponent<RectTransform>();
        infoRt.anchorMin = new Vector2(0.03f, 0.05f);
        infoRt.anchorMax = new Vector2(0.48f, 0.95f);
        infoRt.sizeDelta = Vector2.zero;

        // Qty Text
        GameObject qtyObj = new GameObject("Qty");
        qtyObj.transform.SetParent(row.transform, false);
        qtyTxt = qtyObj.AddComponent<TextMeshProUGUI>();
        qtyTxt.text = "x0";
        qtyTxt.fontSize = 13;
        qtyTxt.fontStyle = FontStyles.Bold;
        qtyTxt.alignment = TextAlignmentOptions.Center;
        qtyTxt.color = new Color(1f, 0.85f, 0.3f);
        RectTransform qtyRt = qtyObj.GetComponent<RectTransform>();
        qtyRt.anchorMin = new Vector2(0.49f, 0.05f);
        qtyRt.anchorMax = new Vector2(0.62f, 0.95f);
        qtyRt.sizeDelta = Vector2.zero;

        // Buy Button
        GameObject buyObj = new GameObject("Btn_Buy");
        buyObj.transform.SetParent(row.transform, false);
        Image buyImg = buyObj.AddComponent<Image>();
        buyImg.color = new Color(0.2f, 0.6f, 0.3f);
        Button buyBtn = buyObj.AddComponent<Button>();
        buyBtn.onClick.AddListener(onBuy);
        RectTransform buyRt = buyObj.GetComponent<RectTransform>();
        buyRt.anchorMin = new Vector2(0.64f, 0.15f);
        buyRt.anchorMax = new Vector2(0.80f, 0.85f);
        buyRt.sizeDelta = Vector2.zero;

        GameObject buyTxtObj = new GameObject("Label");
        buyTxtObj.transform.SetParent(buyObj.transform, false);
        TextMeshProUGUI buyTxt = buyTxtObj.AddComponent<TextMeshProUGUI>();
        buyTxt.text = $"💎 Mua ({price})";
        buyTxt.fontSize = 11;
        buyTxt.fontStyle = FontStyles.Bold;
        buyTxt.alignment = TextAlignmentOptions.Center;
        buyTxt.color = Color.white;
        RectTransform bRt = buyTxtObj.GetComponent<RectTransform>();
        bRt.anchorMin = Vector2.zero;
        bRt.anchorMax = Vector2.one;
        bRt.sizeDelta = Vector2.zero;

        // Use Button
        GameObject useObj = new GameObject("Btn_Use");
        useObj.transform.SetParent(row.transform, false);
        Image useImg = useObj.AddComponent<Image>();
        useImg.color = new Color(0.8f, 0.5f, 0.15f);
        Button useBtn = useObj.AddComponent<Button>();
        useBtn.onClick.AddListener(onUse);
        RectTransform useRt = useObj.GetComponent<RectTransform>();
        useRt.anchorMin = new Vector2(0.82f, 0.15f);
        useRt.anchorMax = new Vector2(0.97f, 0.85f);
        useRt.sizeDelta = Vector2.zero;

        GameObject useTxtObj = new GameObject("Label");
        useTxtObj.transform.SetParent(useObj.transform, false);
        TextMeshProUGUI useTxt = useTxtObj.AddComponent<TextMeshProUGUI>();
        useTxt.text = "⚡ Dùng";
        useTxt.fontSize = 11;
        useTxt.fontStyle = FontStyles.Bold;
        useTxt.alignment = TextAlignmentOptions.Center;
        useTxt.color = Color.white;
        RectTransform uRt = useTxtObj.GetComponent<RectTransform>();
        uRt.anchorMin = Vector2.zero;
        uRt.anchorMax = Vector2.one;
        uRt.sizeDelta = Vector2.zero;
    }

    private void BuyOil(int oilType, int cost)
    {
        if (SaveManager.instance == null) return;

        if (SaveManager.instance.Gems >= cost)
        {
            SaveManager.instance.Gems -= cost;
            switch (oilType)
            {
                case 1: SaveManager.instance.DamageOils++; break;
                case 2: SaveManager.instance.FireRateOils++; break;
                case 3: SaveManager.instance.PoisonOils++; break;
                case 4: SaveManager.instance.BossReduceOils++; break;
            }
            RefreshUI();
            Debug.Log($"<color=green>[Shop]</color> Đã mua Dầu loại {oilType}! Gems còn lại: {SaveManager.instance.Gems}");
        }
        else
        {
            Debug.LogWarning("[Shop] Không đủ Gems để mua dầu này!");
        }
    }

    private void UseOilInGame(int oilType)
    {
        if (oilType == 3) // Vùng Độc Trải Đường Anti-heal
        {
            if (SaveManager.instance != null && SaveManager.instance.PoisonOils <= 0)
            {
                Debug.LogWarning("[Shop] Không đủ Dầu/Vật phẩm Vùng Độc!");
                return;
            }

            if (PathPoisonZoneManager.main == null)
            {
                GameObject obj = new GameObject("PathPoisonZoneManager");
                obj.AddComponent<PathPoisonZoneManager>();
            }

            if (SaveManager.instance != null) SaveManager.instance.PoisonOils--;
            PathPoisonZoneManager.main.StartPlacementMode();
            RefreshUI();
            HideShop();
            return;
        }

        if (ConsumableOilManager.main == null)
        {
            GameObject managerObj = new GameObject("ConsumableOilManager");
            managerObj.AddComponent<ConsumableOilManager>();
        }

        bool success = false;
        switch (oilType)
        {
            case 1: success = ConsumableOilManager.main.UseDamageOil(); break;
            case 2: success = ConsumableOilManager.main.UseFireRateOil(); break;
            case 4: success = ConsumableOilManager.main.UseBossLevelReductionOil(); break;
        }

        if (success)
        {
            RefreshUI();
            HideShop();
        }
    }

    public void RefreshUI()
    {
        if (SaveManager.instance == null) return;

        if (gemsText != null) gemsText.text = $"💎 {SaveManager.instance.Gems}";
        if (damageOilQtyText != null) damageOilQtyText.text = $"x{SaveManager.instance.DamageOils}";
        if (fireRateOilQtyText != null) fireRateOilQtyText.text = $"x{SaveManager.instance.FireRateOils}";
        if (poisonOilQtyText != null) poisonOilQtyText.text = $"x{SaveManager.instance.PoisonOils}";
        if (bossReduceOilQtyText != null) bossReduceOilQtyText.text = $"x{SaveManager.instance.BossReduceOils}";
    }
}
