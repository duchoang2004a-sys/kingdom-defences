using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthBar : MonoBehaviour
{
    public Image fillImage;
    private EnemyHealth enemyHealth;

    private void Start()
    {
        enemyHealth = GetComponentInParent<EnemyHealth>();
        
        // Cố gắng tự động tìm Image component nếu chưa gán
        if (fillImage == null)
        {
            Image[] images = GetComponentsInChildren<Image>();
            foreach (Image img in images)
            {
                if (img.type == Image.Type.Filled)
                {
                    fillImage = img;
                    break;
                }
            }
        }
    }

    private void LateUpdate()
    {
        if (enemyHealth != null && fillImage != null)
        {
            // Cập nhật thanh máu
            fillImage.fillAmount = enemyHealth.CurrentHealth / enemyHealth.MaxHealth;
            
            // Giữ thanh máu luôn hướng lên (không xoay theo Enemy nếu Enemy xoay)
            transform.rotation = Quaternion.identity;
        }
    }
}
