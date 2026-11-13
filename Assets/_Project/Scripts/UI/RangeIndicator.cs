using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class RangeIndicator : MonoBehaviour
{
    private LineRenderer line;
    public int segments = 64;
    public float lineWidth = 0.08f;

    private void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.positionCount = segments + 1;
        line.useWorldSpace = false;
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;
        line.loop = true;

        // Tìm Shader phù hợp cho cả URP lẫn Built-in
        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");

        if (shader != null)
        {
            line.material = new Material(shader);
        }

        // Màu xanh Cyan rực rỡ nổi bật
        Color c = new Color(0.15f, 0.9f, 1f, 0.9f);
        line.startColor = c;
        line.endColor = c;
        line.sortingOrder = 100; // Đặt lên trên mặt đất và tháp để người chơi nhìn thấy rõ ràng
    }

    public void DrawCircle(float radius)
    {
        if (line == null) line = GetComponent<LineRenderer>();
        if (line == null) return;

        line.positionCount = segments + 1;
        float angle = 0f;
        for (int i = 0; i < (segments + 1); i++)
        {
            float x = Mathf.Sin(Mathf.Deg2Rad * angle) * radius;
            float y = Mathf.Cos(Mathf.Deg2Rad * angle) * radius;
            line.SetPosition(i, new Vector3(x, y, 0));
            angle += (360f / segments);
        }
    }
}
