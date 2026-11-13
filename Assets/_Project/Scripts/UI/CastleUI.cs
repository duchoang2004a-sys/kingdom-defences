using UnityEngine;
using TMPro;

public class CastleUI : MonoBehaviour
{
    public TextMeshProUGUI healthFloatingText;
    public ParticleSystem hitParticles;

    private void Start()
    {
        if (LevelManager.main != null)
        {
            LevelManager.main.OnLivesChanged += HandleCastleHit;
        }

        if (healthFloatingText != null)
            healthFloatingText.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (LevelManager.main != null)
        {
            LevelManager.main.OnLivesChanged -= HandleCastleHit;
        }
    }

    private void HandleCastleHit(int currentLives)
    {
        int baseLives = LevelManager.main != null ? LevelManager.main.startingLives : 20;
        if (currentLives >= baseLives) return;

        // Play particle effect
        if (hitParticles != null)
        {
            hitParticles.Play();
        }

        // Show floating text
        if (healthFloatingText != null)
        {
            healthFloatingText.text = "-1 HP";
            healthFloatingText.gameObject.SetActive(true);

            // Simple bounce animation and fade out
            StopAllCoroutines();
            StartCoroutine(FloatingTextAnim());
        }
    }

    private System.Collections.IEnumerator FloatingTextAnim()
    {
        float timer = 0f;
        Vector3 startPos = Vector3.up * 1f;

        while (timer < 1f)
        {
            timer += Time.deltaTime;
            healthFloatingText.transform.localPosition = startPos + Vector3.up * (timer * 2f);

            Color c = healthFloatingText.color;
            c.a = 1f - timer;
            healthFloatingText.color = c;

            yield return null;
        }

        healthFloatingText.gameObject.SetActive(false);
    }
}
