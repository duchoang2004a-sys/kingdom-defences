using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// UIKit: các hàm tiện ích dựng giao diện uGUI bằng SPRITE (icon, nút, panel) thay cho text/emoji.
/// Tất cả sprite lấy từ UIAssets (UISpriteLibrary).
/// </summary>
public static class UIKit
{
    public static readonly Color White = Color.white;
    public static readonly Color Ink = new Color(0.12f, 0.13f, 0.22f, 1f);
    public static readonly Color Gold = new Color(1f, 0.89f, 0.55f, 1f);
    public static readonly Color Muted = new Color(0.72f, 0.77f, 0.88f, 1f);
    public static readonly Color Green = new Color(0.47f, 0.94f, 0.63f, 1f);

    // ---------------------------------------------------------------- RectTransform
    public static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    public static RectTransform Stretch(RectTransform rt, float l = 0, float b = 0, float r = 0, float t = 0)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(l, b);
        rt.offsetMax = new Vector2(-r, -t);
        return rt;
    }

    public static RectTransform Box(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        return Place(NewRect(name, parent), anchor, pivot, pos, size);
    }

    // ---------------------------------------------------------------- Image
    public static Image Img(Transform parent, string name, string spriteKey, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size,
        bool sliced = false, bool raycast = false)
    {
        RectTransform rt = Box(parent, name, anchor, pivot, pos, size);
        return SetupImage(rt, UIAssets.Get(spriteKey), sliced, raycast);
    }

    public static Image ImgFill(Transform parent, string name, string spriteKey, bool sliced = false, bool raycast = false)
    {
        RectTransform rt = NewRect(name, parent);
        Stretch(rt);
        return SetupImage(rt, UIAssets.Get(spriteKey), sliced, raycast);
    }

    private static Image SetupImage(RectTransform rt, Sprite sprite, bool sliced, bool raycast)
    {
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = sprite != null ? Color.white : new Color(1f, 0f, 1f, 0.25f);
        img.raycastTarget = raycast;
        img.preserveAspect = !sliced;
        if (sliced && sprite != null && sprite.border.sqrMagnitude > 0.01f) img.type = Image.Type.Sliced;
        return img;
    }

    public static Image Solid(Transform parent, string name, Color color, bool raycast = false)
    {
        RectTransform rt = NewRect(name, parent);
        Stretch(rt);
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = raycast;
        return img;
    }

    // ---------------------------------------------------------------- Text
    public static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color,
        Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 box,
        TextAlignmentOptions align = TextAlignmentOptions.Center, FontStyles style = FontStyles.Bold)
    {
        RectTransform rt = Box(parent, name, anchor, pivot, pos, box);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.fontStyle = style;
        t.raycastTarget = false;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    // ---------------------------------------------------------------- Button
    /// <summary>Nút bằng icon: sprite thường + sprite khi nhấn (có thể null).</summary>
    public static Button IconButton(Transform parent, string name, string spriteKey, string pressedKey,
        Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, UnityAction onClick)
    {
        Image img = Img(parent, name, spriteKey, anchor, pivot, pos, size, false, true);
        return MakeButton(img, pressedKey, onClick);
    }

    public static Button MakeButton(Image img, string pressedKey, UnityAction onClick)
    {
        Button b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        Sprite pressed = string.IsNullOrEmpty(pressedKey) ? null : UIAssets.Get(pressedKey);
        if (pressed != null)
        {
            b.transition = Selectable.Transition.SpriteSwap;
            SpriteState st = new SpriteState();
            st.pressedSprite = pressed;
            st.highlightedSprite = img.sprite;
            st.selectedSprite = img.sprite;
            st.disabledSprite = img.sprite;
            b.spriteState = st;
        }
        else
        {
            b.transition = Selectable.Transition.ColorTint;
            ColorBlock cb = b.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            cb.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            cb.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.8f);
            b.colors = cb;
        }
        if (onClick != null) b.onClick.AddListener(onClick);
        return b;
    }

    /// <summary>Thêm hiệu ứng phóng nhẹ khi rê chuột (không bắt buộc).</summary>
    public static void AddHoverScale(GameObject go, float scale = 1.06f)
    {
        HoverScale hs = go.GetComponent<HoverScale>();
        if (hs == null) hs = go.AddComponent<HoverScale>();
        hs.scale = scale;
    }

    // ---------------------------------------------------------------- Canvas
    public static Canvas EnsureCanvas(string name, int sortingOrder)
    {
        Canvas existing = null;
        Canvas[] all = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name == name) { existing = all[i]; break; }
        }
        if (existing != null) return existing;

        GameObject go = new GameObject(name, typeof(RectTransform));
        Canvas c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = sortingOrder;
        CanvasScaler cs = go.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);
        cs.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        cs.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return c;
    }
}

/// <summary>Phóng nhẹ nút khi rê chuột / chạm.</summary>
public class HoverScale : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
{
    public float scale = 1.06f;
    private Vector3 baseScale = Vector3.one;
    private bool hover;

    private void Awake() { baseScale = transform.localScale; }
    private void OnDisable() { transform.localScale = baseScale; hover = false; }

    public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e) { hover = true; }
    public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) { hover = false; }

    private void Update()
    {
        Vector3 target = hover ? baseScale * scale : baseScale;
        transform.localScale = Vector3.Lerp(transform.localScale, target, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
    }
}
