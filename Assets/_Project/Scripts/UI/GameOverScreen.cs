using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class GameOverScreen : MonoBehaviour
{
    public static GameOverScreen main;

    [Header("References")]
    [SerializeField] private GameObject GameOverPanel;

    private void Awake()
    {
        main = this;
    }

    private void Start()
    {
        if (LevelManager.main != null)
        {
            LevelManager.main.OnGameOver += ShowGameOver;
        }

        if (GameOverPanel != null)
        {
            GameOverPanel.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (LevelManager.main != null)
        {
            LevelManager.main.OnGameOver -= ShowGameOver;
        }
    }

    // Hiển thị Game Over
    public void ShowGameOver()
    {
        if (GameOverPanel != null)
        {
            GameOverPanel.SetActive(true);
            return;
        }

        // Tự động tạo giao diện Game Over nếu chưa có sẵn trong Scene
        BuildFallbackGameOverUI();
    }

    public void ToggleGameOverPanel()
    {
        ShowGameOver();
    }

    private void BuildFallbackGameOverUI()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null) return;
        }

        // 1. Fullscreen dark overlay
        GameObject overlayObj = new GameObject("GameOverOverlay");
        overlayObj.transform.SetParent(canvas.transform, false);
        GameOverPanel = overlayObj;

        RectTransform overlayRect = overlayObj.AddComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.sizeDelta = Vector2.zero;

        Image overlayImg = overlayObj.AddComponent<Image>();
        overlayImg.color = new Color(0.04f, 0.05f, 0.08f, 0.88f);

        // 2. Modal Box
        GameObject modalObj = new GameObject("GameOverModal");
        modalObj.transform.SetParent(overlayObj.transform, false);

        RectTransform modalRect = modalObj.AddComponent<RectTransform>();
        modalRect.sizeDelta = new Vector2(480f, 290f);
        modalRect.anchoredPosition = Vector2.zero;

        Image modalBg = modalObj.AddComponent<Image>();
        modalBg.color = new Color(0.12f, 0.14f, 0.18f, 0.98f);

        Outline modalOutline = modalObj.AddComponent<Outline>();
        modalOutline.effectColor = new Color(0.9f, 0.25f, 0.25f, 0.9f);
        modalOutline.effectDistance = new Vector2(3f, -3f);

        // 3. Title: "THẤT BẠI - GAME OVER"
        GameObject titleObj = new GameObject("GameOverTitle");
        titleObj.transform.SetParent(modalObj.transform, false);
        TextMeshProUGUI titleTxt = titleObj.AddComponent<TextMeshProUGUI>();
        titleTxt.text = "-- THẤT BẠI --";
        titleTxt.fontSize = 28;
        titleTxt.fontStyle = FontStyles.Bold;
        titleTxt.alignment = TextAlignmentOptions.Center;
        titleTxt.color = new Color(1f, 0.3f, 0.3f);
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchoredPosition = new Vector2(0f, 95f);
        titleRect.sizeDelta = new Vector2(440f, 40f);

        // 4. Description
        GameObject descObj = new GameObject("GameOverDesc");
        descObj.transform.SetParent(modalObj.transform, false);
        TextMeshProUGUI descTxt = descObj.AddComponent<TextMeshProUGUI>();
        descTxt.text = "Lâu đài của bạn đã bị quái vật xâm chiếm!\nHãy điều chỉnh chiến thuật và xây dựng lại tuyến phòng thủ.";
        descTxt.fontSize = 15;
        descTxt.alignment = TextAlignmentOptions.Center;
        descTxt.color = new Color(0.85f, 0.85f, 0.9f);
        RectTransform descRect = descObj.GetComponent<RectTransform>();
        descRect.anchoredPosition = new Vector2(0f, 35f);
        descRect.sizeDelta = new Vector2(420f, 50f);

        // 5. Button Replay (Chơi lại)
        CreateButton(modalObj.transform, "ReplayBtn", "🔄 CHƠI LẠI", new Vector2(-110f, -65f), new Color(0.18f, 0.55f, 0.32f), OnRePlayButtonPressed);

        // 6. Button Main Menu (Menu chính)
        CreateButton(modalObj.transform, "MainMenuBtn", "🏠 MENU CHÍNH", new Vector2(110f, -65f), new Color(0.25f, 0.35f, 0.5f), OnMainMenuButtonPressed);

        GameOverPanel.SetActive(true);
    }

    private void CreateButton(Transform parent, string name, string label, Vector2 pos, Color btnColor, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);

        RectTransform rect = btnObj.AddComponent<RectTransform>();
        rect.anchoredPosition = pos;
        rect.sizeDelta = new Vector2(190f, 50f);

        Image img = btnObj.AddComponent<Image>();
        img.color = btnColor;

        Outline outline = btnObj.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.4f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        GameObject textObj = new GameObject("Label");
        textObj.transform.SetParent(btnObj.transform, false);
        TextMeshProUGUI txt = textObj.AddComponent<TextMeshProUGUI>();
        txt.raycastTarget = false; // Không chặn raycast chuột vào button
        txt.text = label;
        txt.fontSize = 15;
        txt.fontStyle = FontStyles.Bold;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = Color.white;

        RectTransform txtRect = textObj.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.sizeDelta = Vector2.zero;
    }

    // Nút Replay: Chơi lại ngay màn chơi hiện tại
    public void OnRePlayButtonPressed()
    {
        Time.timeScale = 1f;
        string currentScene = SceneManager.GetActiveScene().name;
        if (!string.IsNullOrEmpty(currentScene))
        {
            SceneManager.LoadScene(currentScene);
        }
        else
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    // Nút Main Menu: Quay về giao diện chính
    public void OnMainMenuButtonPressed()
    {
        Time.timeScale = 1f;
        if (Application.CanStreamedLevelBeLoaded("MainMenu"))
        {
            SceneManager.LoadScene("MainMenu");
        }
        else if (Application.CanStreamedLevelBeLoaded("MainMenuScene"))
        {
            SceneManager.LoadScene("MainMenuScene");
        }
        else
        {
            SceneManager.LoadScene(0);
        }
    }

    public void OnQuitButtonPressed()
    {
        Time.timeScale = 1f;
        Application.Quit();
    }
}
