using UnityEngine;

/// <summary>Đổi animation Dragon theo phase: dùng DragonBoss.currentPhase nếu có, nếu không thì theo % máu (66% / 33%).</summary>
[RequireComponent(typeof(Animator))]
public class DragonPhaseAnimator : MonoBehaviour
{
    private Animator anim;
    private EnemyHealth hp;
    private DragonBoss boss;
    private int lastPhase = 0;
    private static readonly int PhaseHash = Animator.StringToHash("Phase");

    private void Awake()
    {
        anim = GetComponent<Animator>();
        hp = GetComponent<EnemyHealth>();
        boss = GetComponent<DragonBoss>();
    }

    private void OnEnable()
    {
        lastPhase = 0;
    }

    private void Update()
    {
        if (anim == null) return;
        int phase = 1;
        if (boss != null) phase = Mathf.Clamp(boss.currentPhase, 1, 3);
        else if (hp != null)
        {
            float max = hp.MaxHealth > 0f ? hp.MaxHealth : 1f;
            float pct = hp.CurrentHealth / max;
            phase = pct <= 0.33f ? 3 : (pct <= 0.66f ? 2 : 1);
        }
        if (phase != lastPhase)
        {
            lastPhase = phase;
            anim.SetInteger(PhaseHash, phase);
        }
    }
}
