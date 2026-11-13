using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject mainMenu;
    [SerializeField] private GameObject optionsMenu;
    
    public void OnPlayButtonPressed()
    {
        int targetLvl = 1;
        if (SaveManager.instance != null)
        {
            targetLvl = Mathf.Max(1, SaveManager.instance.highestUnlockedLevel);
        }

        string targetScene = $"Level_{targetLvl}";
        if (Application.CanStreamedLevelBeLoaded(targetScene))
        {
            SceneManager.LoadScene(targetScene);
        }
        else if (Application.CanStreamedLevelBeLoaded("Level_1"))
        {
            SceneManager.LoadScene("Level_1");
        }
        else if (SceneManager.sceneCountInBuildSettings > 1)
        {
            SceneManager.LoadScene(1);
        }
        else
        {
            Debug.LogWarning("[MainMenuManager] Hãy dùng công cụ 'Tools/Kingdom Rush/TD Scene Generator' để tạo các Level trước khi chơi!");
        }
    }

    public void OnOptionsButtonPressed()
    {
        mainMenu.SetActive(false);
        optionsMenu.SetActive(true);
    }

    public void OnBackButtonPressed()
    {
        optionsMenu.SetActive(false);
        mainMenu.SetActive(true);
    }

    public void OnQuitButtonPressed()
    {
        Application.Quit();
    }
    
}
