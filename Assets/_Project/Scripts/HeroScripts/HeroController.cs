using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Bộ điều khiển Anh hùng (Hero Controller) - bản sửa lỗi "chọn tướng rồi không thoát ra được":
/// 1. Click vào Hero -> chọn (bấm lại lần nữa -> bỏ chọn). Phím Space/F -> chọn + focus camera.
/// 2. Khi đang chọn: click mặt đất -> Hero đi tới chốt mới VÀ TỰ BỎ CHỌN (như Kingdom Rush).
/// 3. Click vào bệ tháp / tháp / bất kỳ vật thể khác đã "ăn" click, chuột phải, hoặc phím Esc -> bỏ chọn.
/// 4. Kích hoạt Skill 1 & Skill 2 -> click điểm thi triển, thi triển xong tự bỏ chọn.
/// 5. Chọn Hero KHÔNG còn ép camera zoom: camera chỉ focus khi bấm F / Space / nút camera.
/// </summary>
public class HeroController : MonoBehaviour
{
    public static HeroController main;

    public Hero hero;

    /// <summary>Bán kính (đơn vị world) quanh Hero để tính là click trúng Hero.</summary>
    public float pickRadius = 0.9f;

    private enum HeroCommandMode { Move, Skill1, Skill2 }
    private HeroCommandMode currentMode = HeroCommandMode.Move;

    private void Awake()
    {
        main = this;
        EnsureHero();
    }

    private void EnsureHero()
    {
        if (hero == null)
        {
            hero = Object.FindAnyObjectByType<Hero>();
        }
        if (hero != null)
        {
            GameScale.Apply(hero.gameObject, GameScale.Hero);   // thu nhỏ đúng 1 lần
        }
    }

    private void Start()
    {
        if (Object.FindAnyObjectByType<HeroHUD>() == null)
        {
            gameObject.AddComponent<HeroHUD>();
        }

        // Đảm bảo có CameraController trên Camera chính
        if (Camera.main != null && Camera.main.GetComponent<HeroCameraController>() == null)
        {
            Camera.main.gameObject.AddComponent<HeroCameraController>();
        }
    }

    /// <summary>Chọn hero. focusCamera = true (phím F / Space / nút HUD) mới kéo camera tới hero.</summary>
    public void SelectHero(bool focusCamera)
    {
        if (hero == null) return;
        hero.SetSelected(true);
        if (focusCamera && HeroCameraController.main != null)
        {
            HeroCameraController.main.FocusOnHero();
        }
    }

    /// <summary>Giữ tương thích code cũ (HeroDeployer / HUD): chọn + focus camera.</summary>
    public void SelectHero()
    {
        SelectHero(true);
    }

    public void DeselectHero()
    {
        if (hero != null)
        {
            hero.SetSelected(false);
        }
        currentMode = HeroCommandMode.Move;
        if (HeroCameraController.main != null)
        {
            HeroCameraController.main.ReleaseFocus();
        }
    }

    private void LateUpdate()
    {
        if (hero == null)
        {
            EnsureHero();
            if (hero == null) return;
        }

        try
        {
            // Phím tắt F hoặc Space: chọn + focus hero
            if (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.Space))
            {
                SelectHero(true);
            }
            // Esc: bỏ chọn / hủy ngắm
            if (Input.GetKeyDown(KeyCode.Escape) && (hero.isSelected || currentMode != HeroCommandMode.Move))
            {
                DeselectHero();
            }
        }
        catch { }

        // Chuột phải -> Hủy chọn Hero hoặc hủy ngắm Skill
        if (GameInput.GetMouseButtonDown(1))
        {
            DeselectHero();
            return;
        }

        if (!GameInput.GetMouseButtonDown(0)) return;

        // Click đã bị hệ thống khác xử lý (bệ tháp, tháp, phép...) -> thoát khỏi trạng thái chọn hero
        if (InputRouter.IsConsumed())
        {
            if (hero.isSelected || currentMode != HeroCommandMode.Move) DeselectHero();
            return;
        }

        // Click lên UI: để UI tự xử lý, không đổi trạng thái hero
        if (!InputRouter.CanProcessGameplayClick()) return;

        Vector2 mousePos = Camera.main != null ? (Vector2)Camera.main.ScreenToWorldPoint(GameInput.MousePosition) : Vector2.zero;

        // 1. Đang nhắm Skill 1
        if (currentMode == HeroCommandMode.Skill1)
        {
            hero.UseSkill1(mousePos);
            InputRouter.Consume("Hero_Skill1");
            DeselectHero();
            return;
        }

        // 2. Đang nhắm Skill 2
        if (currentMode == HeroCommandMode.Skill2)
        {
            hero.UseSkill2(mousePos);
            InputRouter.Consume("Hero_Skill2");
            DeselectHero();
            return;
        }

        // 3. Click trúng Hero -> bật / tắt chọn
        float distToHero = Vector2.Distance(mousePos, hero.transform.position);
        if (distToHero <= pickRadius)
        {
            InputRouter.Consume("Hero_Select");
            if (hero.isSelected) DeselectHero();
            else SelectHero(false);
            return;
        }

        // 4. Đang chọn + click mặt đất -> đi tới chốt mới rồi bỏ chọn
        if (hero.isSelected)
        {
            hero.MoveTo(mousePos);
            InputRouter.Consume("Hero_Move");
            DeselectHero();
        }
    }

    public void PrepareSkill1()
    {
        currentMode = HeroCommandMode.Skill1;
        if (hero != null) hero.SetSelected(true);
    }

    public void PrepareSkill2()
    {
        currentMode = HeroCommandMode.Skill2;
        if (hero != null) hero.SetSelected(true);
    }
}
