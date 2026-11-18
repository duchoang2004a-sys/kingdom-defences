using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TowerUI : MonoBehaviour
{
    public static TowerUI main;

    [Header("UI Panels")]
    public GameObject uiPanel;          // Panel Nâng cấp / Bán tháp (Khi đã có tháp)
    public GameObject buildPanel;       // Panel Chọn tháp cấp 1 (Khi ô đất trống)

    [Header("Upgrade / Sell Controls")]
    public Button upgradeButton;
    public Button sellButton;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI upgradeCostText;
    public TextMeshProUGUI damageText;

    private Plot currentPlot;
    private Plot currentBuildPlot;
    private RangeIndicator rangeIndicator;
    private List<Button> buildButtons = new List<Button>();

    private void Awake()
    {
        main = this;

        // Tự động tạo RangeIndicator nếu chưa có
        GameObject go = new GameObject("RangeIndicator");
        go.transform.SetParent(transform);
        rangeIndicator = go.AddComponent<RangeIndicator>();
        go.SetActive(false);

        EnsureUIBuilt();
    }

    private void EnsureUIBuilt()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas != null) transform.SetParent(canvas.transform, false);
        }

        if (uiPanel == null) BuildUpgradePanel();
        if (buildPanel == null) BuildSelectionPanel();
    }

    // 1. Giao diện Nâng cấp / Bán tháp (icon + số, không còn chữ dài)
    private TextMeshProUGUI sellCostText;
    private TextMeshProUGUI statLine2;
    private Image statIcon1, statIcon2, towerIconImg;

    private static string TowerIconKey(string towerName)
    {
        string n = string.IsNullOrEmpty(towerName) ? "" : towerName;
        if (n.Contains("Cung") || n.Contains("Archer")) return "icon_tower_archer";
        if (n.Contains("Pháp Sư") || n.Contains("Mage")) return "icon_tower_mage";
        if (n.Contains("Pháo") || n.Contains("Cannon")) return "icon_tower_cannon";
        if (n.Contains("Lính") || n.Contains("Barracks")) return "icon_tower_barracks";
        return "icon_tower_ice";
    }

    private void BuildUpgradePanel()
    {
        Vector2 c = new Vector2(0.5f, 0.5f);
        Image panelImg = UIKit.Img(transform, "TowerUpgradePanel", "panel_dark_9slice", c, c, Vector2.zero, new Vector2(560f, 340f), true, true);
        uiPanel = panelImg.gameObject;
        Transform p = uiPanel.transform;

        towerIconImg = UIKit.Img(p, "TowerIcon", "icon_tower_archer", c, c, new Vector2(-190f, 78f), new Vector2(120f, 120f));
        titleText = UIKit.Text(p, "Title", "THÁP", 30f, UIKit.Gold, c, c, new Vector2(50f, 108f), new Vector2(360f, 44f), TextAlignmentOptions.Left);

        // 2 dòng chỉ số: icon + số
        statIcon1 = UIKit.Img(p, "StatIcon1", "stat_dmg", c, c, new Vector2(-60f, 58f), new Vector2(44f, 44f));
        damageText = UIKit.Text(p, "Stat1", "0", 28f, Color.white, c, c, new Vector2(120f, 58f), new Vector2(300f, 44f), TextAlignmentOptions.Left);
        statIcon2 = UIKit.Img(p, "StatIcon2", "stat_range", c, c, new Vector2(-60f, 6f), new Vector2(44f, 44f));
        statLine2 = UIKit.Text(p, "Stat2", "0", 28f, Color.white, c, c, new Vector2(120f, 6f), new Vector2(300f, 44f), TextAlignmentOptions.Left);

        UIKit.IconButton(p, "CloseBtn", "btn_close", "btn_close_pressed", c, c, new Vector2(248f, 138f), new Vector2(64f, 64f), Hide);

        // Nâng cấp: [+] coin giá
        Image upImg = UIKit.Img(p, "UpgradeButton", "btn_wide_green", c, c, new Vector2(-135f, -105f), new Vector2(250f, 96f), true, true);
        upgradeButton = UIKit.MakeButton(upImg, "btn_wide_green_pressed", OnUpgradeButton);
        UIKit.Img(upImg.transform, "PlusIcon", "btn_plus", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 3f), new Vector2(56f, 56f));
        UIKit.Img(upImg.transform, "CoinIcon", "icon_coin", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(82f, 3f), new Vector2(38f, 38f));
        upgradeCostText = UIKit.Text(upImg.transform, "Cost", "0", 30f, Color.white, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(124f, 3f), new Vector2(120f, 50f), TextAlignmentOptions.Left);

        // Bán: coin hoàn lại
        Image sellImg = UIKit.Img(p, "SellButton", "btn_wide_orange", c, c, new Vector2(135f, -105f), new Vector2(250f, 96f), true, true);
        sellButton = UIKit.MakeButton(sellImg, "btn_wide_orange_pressed", OnSellButton);
        UIKit.Img(sellImg.transform, "MinusIcon", "btn_minus", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 3f), new Vector2(56f, 56f));
        UIKit.Img(sellImg.transform, "CoinIcon", "icon_coin", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(82f, 3f), new Vector2(38f, 38f));
        sellCostText = UIKit.Text(sellImg.transform, "Refund", "+0", 30f, Color.white, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(124f, 3f), new Vector2(120f, 50f), TextAlignmentOptions.Left);

        uiPanel.SetActive(false);
    }

    private void SetStats(string line1, string line2)
    {
        if (damageText != null) damageText.text = line1;
        if (statLine2 != null) statLine2.text = line2;
    }

    // 2. Giao diện Chọn Tháp Cấp 1 (5 icon: Cung, Pháp Sư, Pháo, Trại Lính, Băng)
    private void BuildSelectionPanel()
    {
        Vector2 c = new Vector2(0.5f, 0.5f);
        Image panelImg = UIKit.Img(transform, "TowerBuildSelectionPanel", "panel_dark_9slice", c, c, Vector2.zero, new Vector2(840f, 270f), true, true);
        buildPanel = panelImg.gameObject;
        Transform p = buildPanel.transform;

        UIKit.Text(p, "Title", "CHỌN THÁP CẤP 1 ĐỂ XÂY", 26f, Color.white, c, c, new Vector2(0f, 100f), new Vector2(640f, 40f));
        UIKit.IconButton(p, "CloseBtn", "btn_close", "btn_close_pressed", c, c, new Vector2(388f, 102f), new Vector2(60f, 60f), HideBuildMenu);

        RectTransform grid = UIKit.Box(p, "TowerGrid", c, c, new Vector2(0f, -18f), new Vector2(800f, 190f));

        buildPanel.SetActive(false);
    }

    public void ShowBuildMenu(Plot plot)
    {
        currentBuildPlot = plot;
        EnsureUIBuilt();

        if (uiPanel != null) uiPanel.SetActive(false);
        if (rangeIndicator != null) rangeIndicator.gameObject.SetActive(false);

        PlaceNearPlot(buildPanel, plot, 40f);

        RefreshBuildButtons();
        if (buildPanel != null) buildPanel.SetActive(true);
    }

    public void HideBuildMenu()
    {
        if (buildPanel != null) buildPanel.SetActive(false);
        currentBuildPlot = null;
    }

    private void RefreshBuildButtons()
    {
        if (buildPanel == null) return;
        Transform grid = buildPanel.transform.Find("TowerGrid");
        if (grid == null) return;

        foreach (Transform child in grid)
        {
            Destroy(child.gameObject);
        }
        buildButtons.Clear();

        if (BuildManager.main == null) return;
        Tower[] towers = BuildManager.main.Towers;
        if (towers == null || towers.Length == 0) return;

        int playerGold = LevelManager.main != null ? LevelManager.main.currency : 999;

        int count = Mathf.Min(towers.Length, 5);
        float spacing = 156f;
        float startX = -((count - 1) * spacing) / 2f;

        for (int i = 0; i < count; i++)
        {
            int index = i;
            Tower t = towers[i];
            int cost = t.cost;

            if (cost <= 0 && t.prefab != null)
            {
                Turret tc = t.prefab.GetComponent<Turret>();
                if (tc != null) cost = tc.buildCost;
                else
                {
                    Barracks bc = t.prefab.GetComponent<Barracks>();
                    if (bc != null) cost = bc.buildCost;
                }
            }

            bool canAfford = playerGold >= cost;

            GameObject btnObj = new GameObject("TowerBtn_" + i, typeof(RectTransform));
            btnObj.transform.SetParent(grid, false);
            RectTransform btnRect = btnObj.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0.5f);
            btnRect.anchorMax = new Vector2(0.5f, 0.5f);
            btnRect.anchoredPosition = new Vector2(startX + i * spacing, 0f);
            btnRect.sizeDelta = new Vector2(140f, 180f);

            Image btnImg = btnObj.AddComponent<Image>();
            btnImg.color = new Color(1f, 1f, 1f, 0.001f);   // vùng bấm trong suốt

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.interactable = canAfford;

            // Icon tháp (đã có sẵn nền màu trong sprite)
            Sprite icon = UIAssets.Get(TowerIconKey(t.name));
            if (icon == null) icon = t.iconSprite;
            Image iconImg = UIKit.Img(btnObj.transform, "Icon", "", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(124f, 124f));
            if (icon != null) { iconImg.sprite = icon; iconImg.color = canAfford ? Color.white : new Color(0.45f, 0.45f, 0.45f, 1f); }

            // Giá: coin + số
            Image chip = UIKit.Img(btnObj.transform, "CostChip", "hud_chip_bg", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(128f, 44f), true);
            UIKit.Img(chip.transform, "Coin", "icon_coin", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(28f, 28f));
            UIKit.Text(chip.transform, "Cost", cost.ToString(), 24f, canAfford ? UIKit.Gold : new Color(0.95f, 0.45f, 0.45f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(42f, 0f), new Vector2(80f, 40f), TextAlignmentOptions.Left);

            UIKit.AddHoverScale(btnObj, 1.07f);
            btn.onClick.AddListener(() => OnSelectTowerToBuild(index));
            buildButtons.Add(btn);
        }
    }

    private void OnSelectTowerToBuild(int index)
    {
        if (currentBuildPlot == null) return;
        if (BuildManager.main == null) return;
        Tower[] towers = BuildManager.main.Towers;
        if (index < 0 || index >= towers.Length) return;

        Tower selected = towers[index];
        Debug.Log($"[TowerUI] Người chơi chọn xây {selected.name} Cấp 1");
        bool success = currentBuildPlot.BuildTower(selected);
        if (success)
        {
            HideBuildMenu();
        }
    }

    public void ShowRangeIndicator(Plot plot)
    {
        if (plot == null) return;
        IBaseTower tower = plot.GetTower();
        if (tower != null)
        {
            if (rangeIndicator == null)
            {
                GameObject go = new GameObject("RangeIndicator");
                rangeIndicator = go.AddComponent<RangeIndicator>();
            }
            rangeIndicator.transform.position = plot.transform.position;
            rangeIndicator.DrawCircle(tower.GetRange());
            rangeIndicator.gameObject.SetActive(true);
        }
    }

    public void HideRangeIndicator()
    {
        if (rangeIndicator != null)
        {
            rangeIndicator.gameObject.SetActive(false);
        }
    }

    public void SetTarget(Plot plot)
    {
        currentPlot = plot;
        EnsureUIBuilt();
        HideBuildMenu();

        PlaceNearPlot(uiPanel, plot, 40f);

        if (uiPanel != null) uiPanel.SetActive(true);

        ShowRangeIndicator(plot);
        UpdateUI();
    }

    public void Hide()
    {
        if (uiPanel != null) uiPanel.SetActive(false);
        HideBuildMenu();
        currentPlot = null;
        HideRangeIndicator();
    }

    private void UpdateUI()
    {
        if (currentPlot == null || currentPlot.GetTower() == null)
        {
            Hide();
            return;
        }

        IBaseTower tower = currentPlot.GetTower();
        if (tower == null) { Hide(); return; }

        EnsureUIBuilt();

        TowerUpgrade currentStats = tower.GetCurrentUpgrade();
        TowerUpgrade nextStats = tower.GetNextUpgrade();

        string title = "THÁP";
        if (tower is Turret turret) title = turret.towerName;
        else if (tower is Barracks barracks) title = barracks.towerName;

        if (titleText != null) titleText.text = title.ToUpper();
        if (towerIconImg != null)
        {
            Sprite ic = UIAssets.Get(TowerIconKey(title));
            if (ic != null) towerIconImg.sprite = ic;
        }

        bool isTurret = tower is Turret;
        if (statIcon1 != null) { Sprite s1 = UIAssets.Get(isTurret ? "stat_dmg" : "icon_reinforcements"); if (s1 != null) statIcon1.sprite = s1; }
        if (statIcon2 != null) { Sprite s2 = UIAssets.Get(isTurret ? "stat_range" : "stat_hp"); if (s2 != null) statIcon2.sprite = s2; }

        const string up = "<color=#7CF29B>";
        if (nextStats != null)
        {
            if (upgradeCostText != null) upgradeCostText.text = nextStats.cost.ToString();

            if (isTurret)
                SetStats(currentStats.damage.ToString("F0") + "  >  " + up + nextStats.damage.ToString("F0") + "</color>",
                         currentStats.range.ToString("F1") + "  >  " + up + nextStats.range.ToString("F1") + "</color>");
            else
                SetStats(currentStats.soldierCount + "  >  " + up + nextStats.soldierCount + "</color>",
                         currentStats.soldierHP.ToString("F0") + "  >  " + up + nextStats.soldierHP.ToString("F0") + "</color>");

            if (upgradeButton != null) upgradeButton.interactable = LevelManager.main == null || LevelManager.main.currency >= nextStats.cost;
        }
        else
        {
            if (upgradeCostText != null) upgradeCostText.text = "MAX";
            if (isTurret)
                SetStats(currentStats.damage.ToString("F0"), currentStats.range.ToString("F1"));
            else
                SetStats(currentStats.soldierCount.ToString(), currentStats.soldierHP.ToString("F0"));
            if (upgradeButton != null) upgradeButton.interactable = false;
        }

        if (sellCostText != null) sellCostText.text = "+" + tower.GetSellRefund();
    }

    private void PlaceNearPlot(GameObject panel, Plot plot, float yOffsetPx)
    {
        if (Camera.main == null || panel == null || plot == null) return;
        Vector3 screenPos = Camera.main.WorldToScreenPoint(plot.transform.position);
        Canvas cv = GetComponentInParent<Canvas>();
        float scale = cv != null ? cv.scaleFactor : 1f;
        RectTransform rt = panel.GetComponent<RectTransform>();
        float halfW = rt.rect.width * 0.5f * scale;
        float halfH = rt.rect.height * 0.5f * scale;
        screenPos.y += yOffsetPx * scale + halfH;
        screenPos.x = Mathf.Clamp(screenPos.x, halfW + 10f, Screen.width - halfW - 10f);
        screenPos.y = Mathf.Clamp(screenPos.y, halfH + 170f * scale, Screen.height - halfH - 110f * scale);
        screenPos.z = 0f;
        panel.transform.position = screenPos;
    }

    public void OnUpgradeButton()
    {
        if (currentPlot == null) return;
        IBaseTower tower = currentPlot.GetTower();
        if (tower == null) return;

        TowerUpgrade nextStats = tower.GetNextUpgrade();
        if (nextStats != null)
        {
            int nextCost = nextStats.cost;
            if (LevelManager.main != null && LevelManager.main.SpendCurrency(nextCost))
            {
                if (tower is Turret tur) tur.ApplyUpgrade(tur.currentLevel + 1);
                else if (tower is Barracks bar) bar.ApplyUpgrade(bar.currentLevel + 1);

                ShowRangeIndicator(currentPlot);
                UpdateUI();
            }
        }
    }

    public void OnSellButton()
    {
        if (currentPlot == null) return;
        IBaseTower tower = currentPlot.GetTower();
        if (tower == null) return;

        int refund = tower.GetSellRefund();
        if (LevelManager.main != null)
        {
            LevelManager.main.IncreaseCurrency(refund);
        }

        currentPlot.RemoveTower();
        ObjectiveTracker.NotifyTowerSold();
        Hide();
    }
}
