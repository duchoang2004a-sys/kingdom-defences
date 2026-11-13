using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class GameWinScreen : MonoBehaviour
{
    [SerializeField] private GameObject GameWinPanel;
    [SerializeField] private TextMeshProUGUI cupsText;

    void Start()
    {
        if (LevelManager.main != null)
        {
            LevelManager.main.OnGameWin += ToggleGameWinPanel;
        }
        
        if (GameWinPanel != null)
        {
            GameWinPanel.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (LevelManager.main != null)
        {
            LevelManager.main.OnGameWin -= ToggleGameWinPanel;
        }
    }

    public void ToggleGameWinPanel()
    {
        if (GameWinPanel != null)
        {
            GameWinPanel.SetActive(true);
            
            if (cupsText != null)
            {
                // Calculate cups based on remaining lives
                int cupsEarned = 1;
                // Since base lives could be anything, let's just do a simple check. 
                // Or pull it from SaveManager if we wanted.
                // Assuming baseLives is 20 for this map setup
                int startingLives = LevelManager.main != null ? LevelManager.main.startingLives : 20;
                if (LevelManager.main != null && LevelManager.main.livesCount >= startingLives) cupsEarned = 3;
                else if (LevelManager.main != null && LevelManager.main.livesCount >= Mathf.Max(1, startingLives / 2)) cupsEarned = 2;

                string stars = "";
                for(int i=0; i<cupsEarned; i++) stars += "⭐";
                
                cupsText.text = $"VICTORY!\n\nYou earned:\n{stars}\n({cupsEarned} Cups)";
            }
        }
    }

    public void OnNextLevelButtonPressed()
    {
        Time.timeScale = 1f;
        string currentScene = SceneManager.GetActiveScene().name;
        if (currentScene.StartsWith("Level_"))
        {
            string numStr = currentScene.Substring(6);
            if (int.TryParse(numStr, out int currentNum))
            {
                string nextScene = $"Level_{currentNum + 1}";
                if (Application.CanStreamedLevelBeLoaded(nextScene))
                {
                    SceneManager.LoadScene(nextScene);
                    return;
                }
            }
        }
        SceneManager.LoadScene("MainMenu");
    }

    public void OnMainMenuButtonPressed()
    {
        Time.timeScale = 1f; 
        SceneManager.LoadScene("MainMenu");
    }
}
