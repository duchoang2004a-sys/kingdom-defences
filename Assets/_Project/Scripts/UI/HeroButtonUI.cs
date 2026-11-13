using UnityEngine;
using UnityEngine.UI;

public class HeroButtonUI : MonoBehaviour
{
    public Image portrait;
    public Image selectionHighlight;
    public Button button;
    private HeroDefinition data;
    private System.Action<HeroDefinition> onSelect;

    public void Setup(HeroDefinition hero, System.Action<HeroDefinition> callback)
    {
        data = hero;
        portrait.sprite = hero.portrait;
        onSelect = callback;
        button.onClick.AddListener(() => onSelect?.Invoke(data));
    }

    public void SetSelected(bool isSelected)
    {
        if (selectionHighlight != null)
            selectionHighlight.enabled = isSelected;
    }
}