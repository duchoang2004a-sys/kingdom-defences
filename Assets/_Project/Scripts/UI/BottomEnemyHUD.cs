using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BottomEnemyHUD : MonoBehaviour
{
    public static BottomEnemyHUD main;

    [Header("UI References (Tự động khởi tạo nếu để trống)")]
    public GameObject hudContainer;
    public Image enemyIcon;
    public TextMeshProUGUI enemyNameText;
    public TextMeshProUGUI statsInfoText;
    public TextMeshProUGUI waveInfoText;
    public Slider hpSlider;
    public TextMeshProUGUI hpValueText;
    public Image hpFillImage;

    private EnemyHealth currentTrackedEnemy;

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
        // Nhấp chuột vào bất kỳ quái nào trên map để xem thông tin chi tiết ở thanh bên dưới
        if (GameInput.GetMouseButtonDown(0))
        {
            if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                return;

            Vector2 mousePos = Camera.main != null ? (Vector2)Camera.main.ScreenToWorldPoint(GameInput.MousePosition) : Vector2.zero;
            RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero);
            if (hit.collider != null)
            {
                EnemyHealth eh = hit.collider.GetComponent<EnemyHealth>() ?? hit.collider.GetComponentInParent<EnemyHealth>();
                if (eh != null)
                {
                    TrackEnemy(eh);
                }
            }
        }

        // Cập nhật máu của quái đang theo dõi
        if (currentTrackedEnemy != null)
        {
            if (!currentTrackedEnemy.gameObject.activeInHierarchy || currentTrackedEnemy.CurrentHealth <= 0)
            {
                currentTrackedEnemy = null;
                FindNextLeadingEnemy();
            }
            else
            {
                UpdateHealthDisplay(currentTrackedEnemy.CurrentHealth, currentTrackedEnemy.MaxHealth);
            }
        }
        else
        {
            // Tự động ghim quái đi đầu gần đích nhất
            if (Time.frameCount % 20 == 0)
            {
                FindNextLeadingEnemy();
            }
        }
    }

    public void TrackEnemy(EnemyHealth enemyHealth)
    {
        if (enemyHealth == null) return;
        currentTrackedEnemy = enemyHealth;
        EnsureUIBuilt();

        Enemy enemy = enemyHealth.GetComponent<Enemy>();
        string eName = enemy != null && !string.IsNullOrEmpty(enemy.EnemyName) ? enemy.EnemyName : "Goblin";
        float maxHp = enemyHealth.MaxHealth;
        float curHp = enemyHealth.CurrentHealth;
        float speed = enemy != null ? enemy.MoveSpeed : 2f;
        int reward = enemy != null ? enemy.GoldReward : 15;

        if (enemyNameText != null) enemyNameText.text = $"{eName.ToUpper()}";
        if (statsInfoText != null) statsInfoText.text = $"Tốc độ: {speed:F1} | Thưởng: +{reward}G";

        // Cập nhật màu avatar tương ứng quái
        SpriteRenderer sr = enemyHealth.GetComponent<SpriteRenderer>();
        if (sr != null && enemyIcon != null)
        {
            enemyIcon.sprite = sr.sprite;
            enemyIcon.color = sr.color;
        }

        UpdateHealthDisplay(curHp, maxHp);
        RefreshWaveInfo();
    }

    private void UpdateHealthDisplay(float current, float max)
    {
        if (hpSlider != null)
        {
            float pct = Mathf.Clamp01(current / Mathf.Max(1f, max));
            hpSlider.value = pct;
            if (hpFillImage != null)
            {
                if (pct > 0.5f) hpFillImage.color = Color.Lerp(Color.yellow, new Color(0.2f, 0.85f, 0.3f), (pct - 0.5f) * 2f);
                else hpFillImage.color = Color.Lerp(Color.red, Color.yellow, pct * 2f);
            }
        }

        if (hpValueText != null)
        {
            hpValueText.text = $"{Mathf.Max(0, Mathf.CeilToInt(current))} / {Mathf.CeilToInt(max)} HP";
        }
    }

    public void RefreshWaveInfo()
    {
        if (waveInfoText == null) return;

        int wave = LevelManager.main != null ? LevelManager.main.GetCurrentWave() : 1;
        EnemyMovement[] allEnemies = Object.FindObjectsByType<EnemyMovement>(FindObjectsSortMode.None);
        int count = allEnemies != null ? allEnemies.Length : 0;

        waveInfoText.text = $"Đợt {wave}  •  Đang tiến quân: {count} quái";
    }

    private void FindNextLeadingEnemy()
    {
        EnemyMovement[] enemies = Object.FindObjectsByType<EnemyMovement>(FindObjectsSortMode.None);
        if (enemies == null || enemies.Length == 0)
        {
            if (enemyNameText != null) enemyNameText.text = "ĐANG CHỜ QUÁI...";
            if (statsInfoText != null) statsInfoText.text = "Chuẩn bị đợt xuất hiện tiếp theo";
            if (hpSlider != null) hpSlider.value = 0f;
            if (hpValueText != null) hpValueText.text = "- / -";
            RefreshWaveInfo();
            return;
        }

        EnemyMovement best = null;
        float bestDist = -1f;
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] != null && enemies[i].gameObject.activeInHierarchy)
            {
                if (enemies[i].DistanceTraveled > bestDist)
                {
                    bestDist = enemies[i].DistanceTraveled;
                    best = enemies[i];
                }
            }
        }

        if (best != null)
        {
            EnemyHealth eh = best.GetComponent<EnemyHealth>();
            if (eh != null) TrackEnemy(eh);
        }
    }

    private void EnsureUIBuilt()
    {
        if (hudContainer != null) return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null) return;
        }

        // Tạo HUD Container neo chặt ở mép dưới cùng màn hình (Bottom Center)
        hudContainer = new GameObject("BottomEnemyHUD_Banner");
        hudContainer.transform.SetParent(canvas.transform, false);

        RectTransform rect = hudContainer.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.70f, 0f);
        rect.anchorMax = new Vector2(0.70f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 15f); // Đặt chính xác ở mép dưới bên phải
        rect.sizeDelta = new Vector2(460f, 65f);

        Image bg = hudContainer.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.1f, 0.14f, 0.92f); // Nền tối bo viền hiện đại

        // Viền trang trí vàng cổ điển
        Outline outline = hudContainer.AddComponent<Outline>();
        outline.effectColor = new Color(0.85f, 0.7f, 0.25f, 0.7f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        // Icon đại diện quái
        GameObject iconObj = new GameObject("EnemyIcon");
        iconObj.transform.SetParent(hudContainer.transform, false);
        enemyIcon = iconObj.AddComponent<Image>();
        enemyIcon.color = Color.white;
        RectTransform iconRect = iconObj.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(35f, 0f);
        iconRect.sizeDelta = new Vector2(46f, 46f);

        // Tên quái
        GameObject nameObj = new GameObject("EnemyName");
        nameObj.transform.SetParent(hudContainer.transform, false);
        enemyNameText = nameObj.AddComponent<TextMeshProUGUI>();
        enemyNameText.text = "QUÁI VẬT";
        enemyNameText.fontSize = 15;
        enemyNameText.fontStyle = FontStyles.Bold;
        enemyNameText.color = new Color(1f, 0.9f, 0.4f);
        RectTransform nameRect = nameObj.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 0.5f);
        nameRect.anchorMax = new Vector2(0f, 0.5f);
        nameRect.pivot = new Vector2(0f, 0.5f);
        nameRect.anchoredPosition = new Vector2(70f, 15f);
        nameRect.sizeDelta = new Vector2(200f, 22f);

        // Chỉ số: Tốc độ & Thưởng vàng
        GameObject statsObj = new GameObject("StatsInfo");
        statsObj.transform.SetParent(hudContainer.transform, false);
        statsInfoText = statsObj.AddComponent<TextMeshProUGUI>();
        statsInfoText.text = "Tốc độ: 2.0 | Thưởng: +15G";
        statsInfoText.fontSize = 12;
        statsInfoText.color = new Color(0.8f, 0.85f, 0.9f);
        RectTransform statsRect = statsObj.GetComponent<RectTransform>();
        statsRect.anchorMin = new Vector2(0f, 0.5f);
        statsRect.anchorMax = new Vector2(0f, 0.5f);
        statsRect.pivot = new Vector2(0f, 0.5f);
        statsRect.anchoredPosition = new Vector2(70f, -12f);
        statsRect.sizeDelta = new Vector2(200f, 20f);

        // Slider thanh máu
        GameObject sliderObj = new GameObject("HPSlider");
        sliderObj.transform.SetParent(hudContainer.transform, false);
        hpSlider = sliderObj.AddComponent<Slider>();
        hpSlider.interactable = false;
        RectTransform sliderRect = sliderObj.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(1f, 0.5f);
        sliderRect.anchorMax = new Vector2(1f, 0.5f);
        sliderRect.pivot = new Vector2(1f, 0.5f);
        sliderRect.anchoredPosition = new Vector2(-15f, 12f);
        sliderRect.sizeDelta = new Vector2(200f, 18f);

        // Nền thanh máu
        GameObject sliderBg = new GameObject("Background");
        sliderBg.transform.SetParent(sliderObj.transform, false);
        Image sBg = sliderBg.AddComponent<Image>();
        sBg.color = new Color(0.2f, 0.2f, 0.25f, 0.9f);
        RectTransform sBgRect = sliderBg.GetComponent<RectTransform>();
        sBgRect.anchorMin = Vector2.zero;
        sBgRect.anchorMax = Vector2.one;
        sBgRect.sizeDelta = Vector2.zero;

        // Fill thanh máu
        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderObj.transform, false);
        RectTransform faRect = fillArea.AddComponent<RectTransform>();
        faRect.anchorMin = Vector2.zero;
        faRect.anchorMax = Vector2.one;
        faRect.sizeDelta = Vector2.zero;

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        hpFillImage = fill.AddComponent<Image>();
        hpFillImage.color = new Color(0.2f, 0.85f, 0.3f);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;

        hpSlider.fillRect = fillRect;
        hpSlider.value = 1f;

        // Số HP Text
        GameObject hpTextObj = new GameObject("HPValueText");
        hpTextObj.transform.SetParent(sliderObj.transform, false);
        hpValueText = hpTextObj.AddComponent<TextMeshProUGUI>();
        hpValueText.text = "30 / 30 HP";
        hpValueText.fontSize = 11;
        hpValueText.fontStyle = FontStyles.Bold;
        hpValueText.alignment = TextAlignmentOptions.Center;
        hpValueText.color = Color.white;
        RectTransform hpTxtRect = hpTextObj.GetComponent<RectTransform>();
        hpTxtRect.anchorMin = Vector2.zero;
        hpTxtRect.anchorMax = Vector2.one;
        hpTxtRect.sizeDelta = Vector2.zero;

        // Tiến độ Wave (Bên dưới thanh máu)
        GameObject waveObj = new GameObject("WaveInfo");
        waveObj.transform.SetParent(hudContainer.transform, false);
        waveInfoText = waveObj.AddComponent<TextMeshProUGUI>();
        waveInfoText.text = "Đợt 1 • Đang tiến quân: 0 quái";
        waveInfoText.fontSize = 12;
        waveInfoText.alignment = TextAlignmentOptions.Right;
        waveInfoText.color = new Color(0.7f, 0.9f, 1f);
        RectTransform waveRect = waveObj.GetComponent<RectTransform>();
        waveRect.anchorMin = new Vector2(1f, 0.5f);
        waveRect.anchorMax = new Vector2(1f, 0.5f);
        waveRect.pivot = new Vector2(1f, 0.5f);
        waveRect.anchoredPosition = new Vector2(-15f, -12f);
        waveRect.sizeDelta = new Vector2(200f, 20f);
    }
}
