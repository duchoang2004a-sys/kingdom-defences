using UnityEngine;

/// <summary>
/// Camera chiến thuật (Tactical Camera Controller) - bản sửa lỗi "map quá to / góc đen khi thu nhỏ":
/// 1. Cỡ camera mặc định = cỡ do TDSceneGenerator đặt (GameScale.ViewOrthoSize). Zoom chỉ trong khoảng
///    [fit x minZoomFactor, fit x maxZoomFactor] và luôn bị chặn để KHÔNG BAO GIỜ nhìn ra ngoài vùng tile phủ kín
///    (coverHalfSize) => hết các khoảng đen ở 4 góc.
/// 2. Chọn hero không còn tự zoom ra xa: chỉ phím F / Space / nút camera mới focus. Bỏ chọn hero => camera nhả bám và về toàn cảnh.
/// 3. Rê bản đồ bằng chuột giữa; vị trí camera bị chặn theo kích thước khung nhìn hiện tại (đúng với mọi tỉ lệ màn hình).
/// </summary>
public class HeroCameraController : MonoBehaviour
{
    public static HeroCameraController main;

    [Header("Target & Following")]
    public Transform target;
    public bool isFollowing = false;
    public float smoothTime = 0.25f;

    [Header("Zoom (tính theo cỡ camera ban đầu)")]
    public float minZoomFactor = 0.65f;
    public float maxZoomFactor = 1.10f;
    public float focusZoomFactor = 0.80f;
    public float zoomSpeed = 2.5f;

    [Header("Vùng tile phủ kín (nửa kích thước, đơn vị world)")]
    public Vector2 coverHalfSize = new Vector2(GameScale.MapHalfWidth, GameScale.MapHalfHeight);   // ảnh map 1920x1080 = ±9,6 x ±5,4
    public float dragSpeed = 1.2f;

    private Camera cam;
    private Vector3 currentVelocity;
    private float fitSize;
    private float targetOrthoSize;
    private Vector3 dragOrigin;
    private bool isDragging = false;
    private Coroutine resetRoutine;

    private void Awake()
    {
        main = this;
        // Scene sinh bằng bản cũ đã lưu vùng phủ cũ (7,1 x 4,35) vào component: vùng đó làm camera bị ép zoom-in ~3,99 (map quá to,
        // rìa trên bị thanh HUD che). Luôn dùng khung thật của ảnh map 1920x1080.
        coverHalfSize = new Vector2(GameScale.MapHalfWidth, GameScale.MapHalfHeight);
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;

        if (cam != null)
        {
            // Cỡ toàn cảnh cố định theo GameScale (scene cũ có thể đã lưu cỡ camera khác)
            fitSize = GameScale.ViewOrthoSize;
            cam.orthographicSize = fitSize;
            targetOrthoSize = fitSize;
        }
    }

    private void Start()
    {
        if (target == null)
        {
            Hero h = Object.FindAnyObjectByType<Hero>();
            if (h != null) target = h.transform;
        }
    }

    private float MinOrtho() { return fitSize * minZoomFactor; }

    private float MaxOrtho()
    {
        // Toàn cảnh (fitSize) đã có sẵn lề quanh map + chỗ cho thanh HUD trên: không cho zoom-out xa hơn nữa
        return Mathf.Max(MinOrtho(), fitSize);
    }

    private void LateUpdate()
    {
        if (cam == null) return;

        HandleShortcuts();
        HandleZoom();
        HandlePanAndFollow();
        transform.position = ClampPosition(transform.position);
    }

    private void HandleShortcuts()
    {
        try
        {
            if (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.Space))
            {
                FocusOnHero();
            }
        }
        catch { }
    }

    public void FocusOnHero()
    {
        if (target == null)
        {
            Hero h = Object.FindAnyObjectByType<Hero>();
            if (h != null) target = h.transform;
        }

        if (target != null)
        {
            isFollowing = true;
            targetOrthoSize = Mathf.Clamp(fitSize * focusZoomFactor, MinOrtho(), MaxOrtho());
        }
    }

    /// <summary>Nhả bám hero (không đổi vị trí camera ngay) - dùng khi bỏ chọn hero.</summary>
    public void ReleaseFocus()
    {
        isFollowing = false;
        currentVelocity = Vector3.zero;
        targetOrthoSize = Mathf.Clamp(fitSize, MinOrtho(), MaxOrtho());
    }

    public void ResetCameraToOverview()
    {
        isFollowing = false;
        targetOrthoSize = Mathf.Clamp(fitSize, MinOrtho(), MaxOrtho());
        if (resetRoutine != null) StopCoroutine(resetRoutine);
        resetRoutine = StartCoroutine(SmoothResetRoutine());
    }

    private System.Collections.IEnumerator SmoothResetRoutine()
    {
        Vector3 startPos = transform.position;
        Vector3 targetPos = ClampPosition(new Vector3(0, 0, transform.position.z), targetOrthoSize);
        float elapsed = 0f;
        float dur = 0.4f;

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dur);
            transform.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        transform.position = targetPos;
        resetRoutine = null;
    }

    private void HandleZoom()
    {
        float scroll = 0f;
        try
        {
            scroll = Input.GetAxis("Mouse ScrollWheel");
        }
        catch { }

        float minO = MinOrtho();
        float maxO = MaxOrtho();

        if (Mathf.Abs(scroll) > 0.01f)
        {
            targetOrthoSize = targetOrthoSize - scroll * zoomSpeed;
        }
        targetOrthoSize = Mathf.Clamp(targetOrthoSize, minO, maxO);

        if (Mathf.Abs(cam.orthographicSize - targetOrthoSize) > 0.005f)
        {
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetOrthoSize, Time.deltaTime * 8f);
        }
        // Luôn nằm trong khoảng cho phép (kể cả khi cửa sổ đổi tỉ lệ)
        cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, minO, maxO);
    }

    private void HandlePanAndFollow()
    {
        // 1. Kéo bản đồ bằng chuột giữa
        if (GameInput.GetMouseButtonDown(2))
        {
            isDragging = true;
            isFollowing = false;
            dragOrigin = cam.ScreenToWorldPoint(GameInput.MousePosition);
        }

        if (GameInput.GetMouseButton(2) && isDragging)
        {
            Vector3 diff = dragOrigin - cam.ScreenToWorldPoint(GameInput.MousePosition);
            transform.position = ClampPosition(transform.position + diff);
            return;
        }

        if (!GameInput.GetMouseButton(2) && isDragging)
        {
            isDragging = false;
        }

        // 2. Chế độ bám theo Hero mượt mà
        if (isFollowing && target != null)
        {
            Vector3 targetPos = new Vector3(target.position.x, target.position.y, transform.position.z);
            Vector3 clampedTarget = ClampPosition(targetPos);
            transform.position = Vector3.SmoothDamp(transform.position, clampedTarget, ref currentVelocity, smoothTime);

            // Đã tới đủ gần hero -> thôi bám để người chơi tự do kéo camera
            if (Vector2.Distance((Vector2)transform.position, (Vector2)clampedTarget) < 0.15f)
            {
                isFollowing = false;
            }
        }
    }

    private Vector3 ClampPosition(Vector3 pos)
    {
        return ClampPosition(pos, cam.orthographicSize);
    }

    private Vector3 ClampPosition(Vector3 pos, float ortho)
    {
        float aspect = cam.aspect > 0.1f ? cam.aspect : 16f / 9f;
        float halfH = ortho;
        float halfW = halfH * aspect;
        float f = GameScale.TopHudFraction;

        float limitX = coverHalfSize.x - halfW;
        float x = limitX >= 0f ? Mathf.Clamp(pos.x, -limitX, limitX) : 0f;

        // Vùng nhìn được DƯỚI thanh HUD trên là [c - halfH, c + halfH - 2*halfH*f] (c = tâm camera theo y)
        float yLo = -coverHalfSize.y + halfH;
        float yHi = coverHalfSize.y - halfH + 2f * halfH * f;
        // Khung nhìn lớn hơn map: đặt map vừa khít dưới thanh HUD; ngược lại cho kéo/bám trong phạm vi map
        float y = yLo <= yHi ? Mathf.Clamp(pos.y, yLo, yHi) : halfH * f;
        return new Vector3(x, y, pos.z);
    }
}
