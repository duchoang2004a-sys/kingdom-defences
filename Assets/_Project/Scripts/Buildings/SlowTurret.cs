using System.Collections.Generic;
using UnityEngine;

// Ice / Frost Tower: Applies area slow/freeze through StatusEffectController
public class SlowTurret : MonoBehaviour
{
    [Header("Attributes")] 
    [SerializeField] private float targetingRange = 3.0f;
    [SerializeField] private float attacksPerSecond = 0.5f;
    [Range(0.1f, 0.9f)]
    [SerializeField] private float slowFactor = 0.5f; // 50% slow
    [SerializeField] private float slowDuration = 2.0f;
    
    private float timeUntilFire;

    private void Update()
    {
        timeUntilFire += Time.deltaTime;
        if (timeUntilFire >= 1f / attacksPerSecond)
        {
            ApplyAreaSlow();
            timeUntilFire = 0f;
        }
    }

    private void ApplyAreaSlow()
    {
        var enemies = EnemyRegistry.ActiveEnemies;
        Vector2 pos = transform.position;

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyHealth eh = enemies[i];
            if (eh == null || !eh.gameObject.activeInHierarchy || eh.CurrentHealth <= 0) continue;

            if (Vector2.Distance(pos, eh.transform.position) <= targetingRange)
            {
                StatusEffectController sec = eh.GetComponent<StatusEffectController>();
                if (sec == null) sec = eh.gameObject.AddComponent<StatusEffectController>();

                sec.ApplyEffect(StatusEffectType.Slow, slowFactor, slowDuration, gameObject);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, targetingRange);
    }
}