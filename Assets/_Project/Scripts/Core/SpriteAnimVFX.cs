using UnityEngine;

/// <summary>
/// Hiệu ứng sprite-animation một lần. An toàn với ObjectPool: hết thời gian thì TRẢ VỀ POOL (nếu được spawn từ pool),
/// nếu không thì mới Destroy. (Bản cũ gọi Destroy trực tiếp làm hỏng hàng đợi pool -> MissingReferenceException.)
/// </summary>
public class SpriteAnimVFX : MonoBehaviour
{
    public float lifetime = 0.5f;

    private void OnEnable()
    {
        CancelInvoke();
        Invoke("Finish", lifetime);
    }

    private void OnDisable()
    {
        CancelInvoke();
    }

    private void Finish()
    {
        if (ObjectPool.instance != null) ObjectPool.instance.ReturnToPool(gameObject);
        else Destroy(gameObject);
    }
}
