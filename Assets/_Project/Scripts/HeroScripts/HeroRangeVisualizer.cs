using UnityEngine;

/// <summary>
/// Hiển thị trực quan 2 vòng tròn phạm vi của Hero khi được người chơi chọn (Selected):
/// 1. Vòng Tầm đánh (Attack Range): Vòng sáng màu ngọc bích/vàng kim hiển thị cự ly vung kiếm/bắn tên.
/// 2. Vòng Tầm nhìn (Vision Range / Intercept): Vòng mờ hơn hiển thị bán kính quan sát phát hiện quái vật.
/// </summary>
public class HeroRangeVisualizer : MonoBehaviour
{
    [Header("References")]
    public Hero hero;

    private LineRenderer attackCircle;
    private LineRenderer visionCircle;

    private const int Segments = 48;
    private bool isInitialized = false;

    private void Awake()
    {
        if (hero == null) hero = GetComponent<Hero>();
        SetupCircles();
    }

    private void SetupCircles()
    {
        if (isInitialized) return;

        // 1. Vòng Tầm Đánh (Attack Range)
        GameObject atkObj = new GameObject("Visual_AttackRange");
        atkObj.transform.SetParent(transform, false);
        atkObj.transform.localPosition = new Vector3(0f, 0.05f, 0f);

        attackCircle = atkObj.AddComponent<LineRenderer>();
        ConfigureLineRenderer(attackCircle, new Color(0.2f, 0.85f, 1f, 0.75f), 0.04f);

        // 2. Vòng Tầm Nhìn (Vision Range)
        GameObject visObj = new GameObject("Visual_VisionRange");
        visObj.transform.SetParent(transform, false);
        visObj.transform.localPosition = new Vector3(0f, 0.05f, 0f);

        visionCircle = visObj.AddComponent<LineRenderer>();
        ConfigureLineRenderer(visionCircle, new Color(0.3f, 0.85f, 0.55f, 0.35f), 0.03f);

        isInitialized = true;
        SetVisible(false);
    }

    private void ConfigureLineRenderer(LineRenderer lr, Color col, float width)
    {
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.positionCount = Segments;
        lr.startWidth = width;
        lr.endWidth = width;

        Material mat = new Material(Shader.Find("Sprites/Default"));
        lr.material = mat;
        lr.startColor = col;
        lr.endColor = col;
        lr.sortingOrder = 8;
    }

    public void RedrawCircles(float attackRadius, float visionRadius)
    {
        if (!isInitialized) SetupCircles();

        DrawCircle(attackCircle, attackRadius);
        DrawCircle(visionCircle, visionRadius);
    }

    private void DrawCircle(LineRenderer lr, float radius)
    {
        if (lr == null) return;

        float angleStep = 2f * Mathf.PI / Segments;
        for (int i = 0; i < Segments; i++)
        {
            float angle = i * angleStep;
            float x = Mathf.Cos(angle) * radius;
            float y = Mathf.Sin(angle) * radius; // Chuẩn 1:1 khớp với bán kính Physics 2D
            lr.SetPosition(i, new Vector3(x, y, 0f));
        }
    }

    public void SetVisible(bool visible)
    {
        if (attackCircle != null) attackCircle.enabled = visible;
        if (visionCircle != null) visionCircle.enabled = visible;

        if (visible && hero != null)
        {
            RedrawCircles(hero.attackRange, hero.visionRange);
        }
    }

    private void Update()
    {
        if (hero != null && hero.isSelected)
        {
            SetVisible(true);
        }
        else
        {
            SetVisible(false);
        }
    }
}
