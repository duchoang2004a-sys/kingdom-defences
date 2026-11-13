using UnityEngine;

/// <summary>
/// TowerAimer: làm "ĐẦU THÁP" (con "Head") sống động:
///  - Mode Aim   : xoay nòng/cung về phía quái Turret đang nhắm, lật trái/phải theo hướng quái, quét nhẹ khi chưa có mục tiêu.
///  - Mode Crystal: tinh thể phép thuật/băng lơ lửng, xoay nhẹ, phồng lên và nghiêng về phía quái khi đang tấn công.
///  - Mode Wave  : lá cờ trại lính phấp phới.
/// Chạy ở LateUpdate nên ghi đè góc xoay thô của Turret (tránh bị lộn ngược khi quái ở bên trái).
/// </summary>
public class TowerAimer : MonoBehaviour
{
    public enum AimMode { Aim, Crystal, Wave }

    public AimMode mode = AimMode.Aim;
    public Transform head;
    public Transform muzzle;
    public Vector2 muzzleLocal = new Vector2(0.3f, 0.12f);
    public float maxAngle = 40f;

    private Turret turret;
    private SpriteRenderer headSr;
    private Vector3 headBase;
    private float angle;
    private float seed;
    private bool flip;
    private bool baseCaptured;

    private void Awake()
    {
        turret = GetComponent<Turret>();
        if (head == null) head = transform.Find("Head");
        if (head != null) headSr = head.GetComponent<SpriteRenderer>();
        seed = Random.Range(0f, 10f);
    }

    private void LateUpdate()
    {
        if (head == null) return;
        if (!baseCaptured) { headBase = head.localPosition; baseCaptured = true; }

        Transform tgt = turret != null ? turret.CurrentTargetTransform : null;
        bool hasTarget = tgt != null && tgt.gameObject.activeInHierarchy;

        if (mode == AimMode.Wave)
        {
            head.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 3f + seed) * 7f);
            return;
        }

        if (mode == AimMode.Crystal)
        {
            float bob = Mathf.Sin(Time.time * 2.2f + seed) * 0.035f;
            Vector3 lean = Vector3.zero;
            float pulse = 1f;
            if (hasTarget)
            {
                Vector2 d = tgt.position - head.position;
                lean = new Vector3(Mathf.Clamp(d.x, -1.5f, 1.5f) * 0.03f, 0f, 0f);
                pulse = 1f + 0.1f * (0.5f + 0.5f * Mathf.Sin(Time.time * 9f));
            }
            head.localPosition = headBase + new Vector3(0f, bob, 0f) + lean;
            head.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 1.3f + seed) * 12f);
            head.localScale = Vector3.one * Mathf.Lerp(head.localScale.x, pulse, 1f - Mathf.Exp(-10f * Time.deltaTime));
            return;
        }

        // ---- Aim
        float desired;
        if (hasTarget)
        {
            Vector2 d = tgt.position - head.position;
            if (Mathf.Abs(d.x) > 0.15f) flip = d.x < 0f;
            float ang = Mathf.Atan2(d.y, Mathf.Max(0.05f, Mathf.Abs(d.x))) * Mathf.Rad2Deg;
            ang = Mathf.Clamp(ang, -maxAngle, maxAngle);
            desired = flip ? -ang : ang;
        }
        else
        {
            desired = Mathf.Sin(Time.time * 0.9f + seed) * 6f;
        }

        angle = Mathf.LerpAngle(angle, desired, 1f - Mathf.Exp(-12f * Time.deltaTime));
        head.localRotation = Quaternion.Euler(0f, 0f, angle);
        if (headSr != null) headSr.flipX = flip;
        if (muzzle != null) muzzle.localPosition = new Vector3(flip ? -muzzleLocal.x : muzzleLocal.x, muzzleLocal.y, 0f);
    }
}
