using UnityEngine;

/// <summary>Cầu nối giữa Turret và hiệu ứng animation của tháp (muzzle flash / phép thuật).
/// Turret.Shoot() gọi PlayShoot(); animator con (FX) phát clip "Shoot".</summary>
public class TowerAnimator : MonoBehaviour
{
    public Animator fxAnimator;
    private static readonly int ShootHash = Animator.StringToHash("Shoot");

    private void Awake()
    {
        if (fxAnimator == null) fxAnimator = GetComponentInChildren<Animator>();
    }

    public void PlayShoot()
    {
        if (fxAnimator == null) return;
        fxAnimator.ResetTrigger(ShootHash);
        fxAnimator.SetTrigger(ShootHash);
    }
}
