using System.Collections;
using UnityEngine;

public class ExplosionEffect : MonoBehaviour
{
    [SerializeField] private float duration = 0.4f;
    [SerializeField] private float maxScale = 2.5f;
    private SpriteRenderer sr;
    private Color initialColor;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr != null) initialColor = sr.color;
    }

    private void OnEnable()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        transform.localScale = Vector3.zero;
        if (sr != null) sr.color = initialColor;
        StartCoroutine(AnimateExplosion());
    }

    private IEnumerator AnimateExplosion()
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            // Expand quickly then slow down
            float currentScale = Mathf.Lerp(0f, maxScale, Mathf.Sin(t * Mathf.PI * 0.5f));
            transform.localScale = new Vector3(currentScale, currentScale, 1f);

            // Fade out
            if (sr != null)
            {
                Color c = initialColor;
                c.a = Mathf.Lerp(initialColor.a, 0f, t);
                sr.color = c;
            }

            yield return null;
        }

        if (ObjectPool.instance != null)
            ObjectPool.instance.ReturnToPool(gameObject);
        else
            Destroy(gameObject);
    }
}
