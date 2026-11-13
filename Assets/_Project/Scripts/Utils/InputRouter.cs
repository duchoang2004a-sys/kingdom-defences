using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Bộ định tuyến Input tập trung giải quyết triệt để xung đột click chuột:
/// Thứ tự ưu tiên: UI > Nhắm Phép (Spell) > Nhắm Skill Hero > Plot / Tháp > Chọn & Di chuyển Hero.
/// </summary>
public static class InputRouter
{
    private static int consumedFrame = -1;
    private static string consumedBy = "";

    /// <summary>
    /// Đánh dấu cú click chuột ở frame hiện tại đã được một hệ thống xử lý, các hệ thống ưu tiên thấp hơn phải bỏ qua.
    /// </summary>
    public static void Consume(string consumerName = "")
    {
        consumedFrame = Time.frameCount;
        consumedBy = consumerName;
    }

    /// <summary>
    /// Kiểm tra xem cú click chuột đã bị tiêu thụ bởi hệ thống khác chưa.
    /// </summary>
    public static bool IsConsumed()
    {
        return consumedFrame == Time.frameCount;
    }

    /// <summary>
    /// Kiểm tra an toàn: chuột không đè lên UI và chưa bị tiêu thụ bởi hành động khác.
    /// </summary>
    public static bool CanProcessGameplayClick()
    {
        if (IsConsumed()) return false;
        if (EventSystem.current != null)
        {
            try
            {
                if (EventSystem.current.IsPointerOverGameObject()) return false;
            }
            catch
            {
                // Fallback an toàn nếu InputModule đang khởi động
            }
        }
        return true;
    }
}
