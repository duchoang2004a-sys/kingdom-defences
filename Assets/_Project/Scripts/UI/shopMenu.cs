using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class Menu : MonoBehaviour
{
    [Header("References")]
    [SerializeField] TextMeshProUGUI currencyUI;
    [SerializeField] TextMeshProUGUI waveCountText;
    [SerializeField] TextMeshProUGUI livesCountText;
    [SerializeField] Animator animator;


    private bool isMenuOpen = true;

    // Update text boxes on start
    private void Start()
    {
        if (LevelManager.main != null)
        {
            LevelManager.main.OnLivesChanged += UpdateLivesText;
            LevelManager.main.OnWaveChanged += UpdateWaveText;
            LevelManager.main.OnCurrencyChanged += UpdateCurrencyText;

            UpdateLivesText(LevelManager.main.livesCount);
            UpdateWaveText(LevelManager.main.currWave);
            UpdateCurrencyText(LevelManager.main.currency);
        }
    }

    private void OnDestroy()
    {
        if (LevelManager.main != null)
        {
            LevelManager.main.OnLivesChanged -= UpdateLivesText;
            LevelManager.main.OnWaveChanged -= UpdateWaveText;
            LevelManager.main.OnCurrencyChanged -= UpdateCurrencyText;
        }
    }

    public void ToggleMenu()
    {
        isMenuOpen = !isMenuOpen;
        if (animator != null) animator.SetBool("MenuOpen", isMenuOpen);
    }

    public void UpdateWaveText(int currentWave)
    {
        if (waveCountText != null) waveCountText.text = "Wave: " + currentWave.ToString();
    }

    public void UpdateLivesText(int currentLives)
    {
        if (livesCountText != null) livesCountText.text = "Lives: " + currentLives.ToString();
    }

    public void UpdateCurrencyText(int currency)
    {
        if (currencyUI != null) currencyUI.text = currency.ToString();
    }

}