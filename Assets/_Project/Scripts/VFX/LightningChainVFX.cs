using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LightningChainVFX : MonoBehaviour
{
    private LineRenderer lr;
    [SerializeField] private float duration = 0.2f;

    private void Awake()
    {
        lr = GetComponent<LineRenderer>();
        if (lr == null)
        {
            lr = gameObject.AddComponent<LineRenderer>();
            lr.startWidth = 0.12f;
            lr.endWidth = 0.05f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = new Color(0.3f, 0.8f, 1f, 0.9f);
            lr.endColor = new Color(0.8f, 0.4f, 1f, 0.9f);
            lr.sortingOrder = 30;
        }
    }

    public void ShowChain(List<Vector3> points)
    {
        if (lr == null) lr = GetComponent<LineRenderer>();
        if (points == null || points.Count < 2)
        {
            Finish();
            return;
        }

        // Tạo các điểm gấp khúc dạng tia sét (zigzag lightning)
        List<Vector3> lightningPoints = new List<Vector3>();
        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector3 start = points[i];
            Vector3 end = points[i + 1];
            lightningPoints.Add(start);

            // Thêm 2 điểm lệch ngẫu nhiên ở giữa tạo hiệu ứng sét giật
            int segments = 3;
            for (int s = 1; s < segments; s++)
            {
                float t = (float)s / segments;
                Vector3 mid = Vector3.Lerp(start, end, t);
                Vector3 perp = Vector2.Perpendicular(end - start).normalized;
                mid += perp * Random.Range(-0.25f, 0.25f);
                lightningPoints.Add(mid);
            }
        }
        lightningPoints.Add(points[points.Count - 1]);

        lr.positionCount = lightningPoints.Count;
        lr.SetPositions(lightningPoints.ToArray());

        StartCoroutine(FadeOutRoutine());
    }

    private IEnumerator FadeOutRoutine()
    {
        yield return new WaitForSeconds(duration);
        Finish();
    }

    private void Finish()
    {
        if (ObjectPool.instance != null)
            ObjectPool.instance.ReturnToPool(gameObject);
        else
            Destroy(gameObject);
    }
}
