using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class LevelPrepManager : MonoBehaviour
{
    [Header("Data")]
    public List<HeroDefinition> availableHeroes; // Kéo các SO Anh hùng vào đây

    [Header("UI Rendering")]
    public Transform heroListParent; // Kéo Content của ScrollView vào đây
    public GameObject heroButtonPrefab;

    [Header("Hero Details Display")]
    public Text heroNameText;
    public Text heroStatsText;
    public Image heroBigPortrait;

    private List<HeroButtonUI> spawnedButtons = new List<HeroButtonUI>();
    private HeroDefinition selectedHero;

    private void Start()
    {
        GenerateHeroUI();
    }

    // Tự động sinh giao diện
    public void GenerateHeroUI()
    {
        // Xóa các nút cũ
        foreach (Transform child in heroListParent) Destroy(child.gameObject);
        spawnedButtons.Clear();

        foreach (var hero in availableHeroes)
        {
            GameObject btnObj = Instantiate(heroButtonPrefab, heroListParent);
            HeroButtonUI script = btnObj.GetComponent<HeroButtonUI>();
            script.Setup(hero, OnHeroSelected);
            spawnedButtons.Add(script);
        }

        if (availableHeroes.Count > 0) OnHeroSelected(availableHeroes[0]);
    }

    private void OnHeroSelected(HeroDefinition hero)
    {
        selectedHero = hero;

        // Cập nhật Highlight
        foreach (var btn in spawnedButtons)
            btn.SetSelected(false); // Bạn cần logic để check nút nào ứng với hero nào

        // Cập nhật Panel chi tiết
        heroNameText.text = hero.heroName;
        heroBigPortrait.sprite = hero.portrait;
        heroStatsText.text = $"Máu: {hero.baseHP}\nSát thương: {hero.baseDamage}\n\n{hero.description}";

        // Lưu vào RunConfig
        // RunConfig.SelectedHeroId = hero.heroId;
    }

    public void StartBattle()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("BattleScene");
    }
}