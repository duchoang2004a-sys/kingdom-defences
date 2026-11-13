using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// T? ??ng s?a ch?a EventSystem trên Unity 6 khi dùng New Input System:
/// Thay th? StandaloneInputModule b?ng InputSystemUIInputModule ?? tránh 4000+ l?i InvalidOperationException
/// và ph?c h?i toàn b? kh? n?ng click nút b?m trên Canvas.
/// </summary>
public class EventSystemInputFixer : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void OnBeforeSceneLoad()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        FixEventSystem();
    }

    private void Awake()
    {
        FixEventSystem();
    }

    private void Start()
    {
        FixEventSystem();
    }

    public static void FixEventSystem()
    {
        EventSystem es = Object.FindAnyObjectByType<EventSystem>();
        if (es == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            es = esObj.AddComponent<EventSystem>();
        }

#if ENABLE_INPUT_SYSTEM
        // N?u ?ang có StandaloneInputModule (Legacy) gây l?i crash Input trên Unity 6
        StandaloneInputModule oldModule = es.GetComponent<StandaloneInputModule>();
        if (oldModule != null)
        {
            DestroyImmediate(oldModule);
        }

        // Thay th? b?ng InputSystemUIInputModule chu?n c?a New Input System
        if (es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() == null)
        {
            es.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            Debug.Log("[EventSystemFixer] ?ã t? ??ng kích ho?t InputSystemUIInputModule cho EventSystem!");
        }
#endif
    }
}
