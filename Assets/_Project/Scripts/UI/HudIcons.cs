using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Nạp icon HUD: ưu tiên thư viện UI có sẵn (UIAssets), sau đó Resources/UI/&lt;tên&gt; (icon_mission_book, icon_enemy_roster...).
/// Đọc cả khi PNG được import dạng Texture thường (tự tạo Sprite lúc chạy).
/// </summary>
public static class HudIcons
{
    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    public static Sprite Get(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        Sprite s;
        if (cache.TryGetValue(name, out s) && s != null) return s;

        s = UIAssets.Get(name);
        if (s == null)
        {
            s = Resources.Load<Sprite>("UI/" + name);
            if (s == null)
            {
                Texture2D tex = Resources.Load<Texture2D>("UI/" + name);
                if (tex != null)
                {
                    s = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                }
            }
        }
        cache[name] = s;
        return s;
    }
}
