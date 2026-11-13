using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

// Quản lý từng ô đất để xây dựng tháp hoặc mở menu Nâng cấp / Bán tháp
public class Plot : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SpriteRenderer sr;
    [SerializeField] private Color hoverColor = new Color(0.9f, 0.9f, 0.9f, 1f);

    private GameObject tower;
    private Color startColor;
    private int lastClickFrame = -1;

    public IBaseTower GetTower()
    {
        if (tower == null) return null;
        return tower.GetComponent<IBaseTower>();
    }

    public Turret GetTurret()
    {
        if (tower == null) return null;
        return tower.GetComponent<Turret>();
    }

    public GameObject GetTowerObject() => tower;

    public void ClearTower()
    {
        tower = null;
    }

    public void RemoveTower()
    {
        if (tower != null)
        {
            Destroy(tower);
            tower = null;
        }
    }

    private void Start()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (sr != null) startColor = sr.color;

        // Đảm bảo có Collider2D để nhận click
        Collider2D col = GetComponent<Collider2D>();
        if (col == null)
        {
            CircleCollider2D c = gameObject.AddComponent<CircleCollider2D>();
            c.radius = 0.8f;
            c.isTrigger = true;
        }
    }

    private void Update()
    {
        // Hỗ trợ kiểm tra click chuột an toàn qua GameInput
        if (GameInput.GetMouseButtonDown(0))
        {
            if (!InputRouter.CanProcessGameplayClick()) return;

            Vector2 mousePos = Camera.main != null ? (Vector2)Camera.main.ScreenToWorldPoint(GameInput.MousePosition) : Vector2.zero;

            // 1. Kiểm tra cự ly trực tiếp (bán kính 0.75m quanh tâm bệ tháp - tin cậy 100% không phụ thuộc layer/trigger physics)
            float dist = Vector2.Distance(mousePos, transform.position);
            bool isHit = dist <= 0.75f;

            // 2. Kiểm tra OverlapPoint nếu cự ly chưa khớp
            if (!isHit)
            {
                Collider2D col = GetComponent<Collider2D>();
                if (col != null && col.OverlapPoint(mousePos))
                {
                    isHit = true;
                }
            }

            // 3. Kiểm tra trúng tháp xây trên ô đất
            if (!isHit && tower != null)
            {
                Collider2D towerCol = tower.GetComponent<Collider2D>();
                if (towerCol != null && towerCol.OverlapPoint(mousePos))
                {
                    isHit = true;
                }
            }

            if (isHit)
            {
                InputRouter.Consume("Plot");
                OnPlotClicked();
            }
        }
    }

    private void OnMouseEnter()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        if (sr != null) sr.color = hoverColor;
    }

    private void OnMouseExit()
    {
        if (sr != null) sr.color = startColor;
    }

    private void OnMouseDown()
    {
        OnPlotClicked();
    }

    public void OnPlotClicked()
    {
        if (Time.frameCount == lastClickFrame) return; // Tránh xử lý đúp trong 1 frame
        lastClickFrame = Time.frameCount;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        // Đảm bảo TowerUI tồn tại
        if (TowerUI.main == null)
        {
            TowerUI found = Object.FindAnyObjectByType<TowerUI>();
            if (found == null)
            {
                Canvas canvas = Object.FindAnyObjectByType<Canvas>();
                if (canvas != null)
                {
                    GameObject uiObj = new GameObject("TowerUI");
                    uiObj.transform.SetParent(canvas.transform, false);
                    uiObj.AddComponent<TowerUI>();
                }
            }
        }

        // 1. Nếu ô đất ĐÃ CÓ THÁP: Mở bảng Nâng Cấp (Upgrade) & Bán tháp (Sell) kèm Vòng Tầm Bắn
        if (tower != null)
        {
            if (TowerUI.main != null)
            {
                TowerUI.main.SetTarget(this);
            }
            return;
        }

        // 2. Nếu ô đất ĐANG TRỐNG: Mở menu CHỌN THÁP CẤP 1 ĐỂ XÂY (Chỉ 4 lựa chọn: Cung, Pháo, Pháp Sư, Doanh Trại)
        if (TowerUI.main != null)
        {
            TowerUI.main.ShowBuildMenu(this);
        }
    }

    // Xây dựng tháp cấp 1 được chọn từ menu
    public bool BuildTower(Tower towerToBuild)
    {
        if (towerToBuild == null || towerToBuild.prefab == null)
        {
            Debug.LogWarning("[Build] Tháp được chọn không hợp lệ!");
            return false;
        }

        int actualCost = towerToBuild.cost;
        IBaseTower baseTowerComp = towerToBuild.prefab.GetComponent<IBaseTower>();

        // We try to get cost from Turret or Barracks if not set in Tower scriptable
        if (actualCost <= 0)
        {
            Turret turret = towerToBuild.prefab.GetComponent<Turret>();
            if (turret != null) actualCost = turret.buildCost;
            else
            {
                Barracks barracks = towerToBuild.prefab.GetComponent<Barracks>();
                if (barracks != null) actualCost = barracks.buildCost;
            }
        }

        if (LevelManager.main == null)
        {
            Debug.LogWarning("[Build] LevelManager.main is missing in the scene!");
            return false;
        }

        if (!LevelManager.main.SpendCurrency(actualCost))
        {
            Debug.Log($"[Build] Không đủ vàng! Cần: {actualCost} G, Hiện có: {LevelManager.main.currency} G.");
            return false;
        }

        tower = Instantiate(towerToBuild.prefab, transform.position, Quaternion.identity);
        tower.SetActive(true);

        IBaseTower newTower = tower.GetComponent<IBaseTower>();
        if (newTower != null)
        {
            newTower.SetPlot(this);
            newTower.ApplyUpgrade(0);
        }

        Debug.Log($"[Build] Đã xây {towerToBuild.name} Cấp 1 (Chi phí: {actualCost} G) tại ô đất {name}");

        // Sau khi xây xong, tự động mở bảng thông tin và hiện tầm bắn của tháp
        if (TowerUI.main != null)
        {
            TowerUI.main.SetTarget(this);
        }

        return true;
    }
}
