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
    public Vector2 coverHalfSize = new Vector2(7.1f, 4.35f);   // tile thật phủ ±7,2 x ±4,4 (trừ lề an toàn)
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
        // Scene sinh bằng bản cũ đã lưu (13, 7.2) vào component -> luôn kẹp về vùng tile thật
        coverHalfSize = new Vector2(Mathf.Min(coverHalfSize.x, 7.1f), Mathf.Min(coverHalfSize.y, 4.35f));
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;

        if (cam != null)
        {
            fitSize = cam.orthographicSize > 0.1f ? cam.orthographicSize : GameScale.ViewOrthoSize;
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
        float aspect = cam != null && cam.aspect > 0.1f ? cam.aspect : 16f / 9f;
        float byCoverW = coverHalfSize.x / aspect;   // không để nửa chiều rộng khung nhìn vượt vùng phủ
        float byCoverH = coverHalfSize.y;
        return Mathf.Max(MinOrtho(), Mathf.Min(fitSize * maxZoomFactor, Mathf.Min(byCoverW, byCoverH)));
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
        Vector3 targetPos = new Vector3(0, 0, transform.position.z);
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
        float aspect = cam.aspect > 0.1f ? cam.aspect : 16f / 9f;
        float halfH = cam.orthographicSize;
        float halfW = halfH * aspect;
        float limitX = Mathf.Max(0f, coverHalfSize.x - halfW);
        float limitY = Mathf.Max(0f, coverHalfSize.y - halfH);
        return new Vector3(Mathf.Clamp(pos.x, -limitX, limitX), Mathf.Clamp(pos.y, -limitY, limitY), pos.z);
    }
}
