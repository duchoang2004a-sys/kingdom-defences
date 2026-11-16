using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

public class LevelManager : MonoBehaviour
{
    [Header("Level Economy & Settings (Tùy chỉnh trong Inspector mỗi Map)")]
    [Tooltip("Số tiền vốn ban đầu cho người chơi mua tháp tại map này")]
    public int startingGold = 250;

    [Tooltip("Số mạng ban đầu của lâu đài")]
    public int startingLives = 20;

    [Tooltip("Wave bắt đầu")]
    public int startWave = 1;

    [Tooltip("Thứ tự Level hiện tại (1, 2, 3...)")]
    public int levelNumber = 1;

    public static LevelManager main; // static reference

    /* The path is how the enemies navigate the map, the path consists of points.
        The enemies go from point to point until they reach the end of the map */
    public Transform[] Path;
    public Transform[] Path2; // Secondary path if any
    public Transform StartPoint;
    public Transform StartPoint2; // Secondary start if any

    [Header("Castle Health & Auto-Regen")]
    [Tooltip("Máu Lâu đài dạng số thực 100.0 HP")]
    public float maxCastleHealth = 100f;
    public float currentCastleHealth = 100f;
    public int currentCastleTier = 0; // 0: Chưa đủ bậc, 1: Bậc 1, 2: Bậc 2, 3: Bậc 3
    public float lastCastleDamageTime = -10f;

    public event Action<float, float> OnCastleHealthChanged;
#pragma warning disable CS0067
    public event Action<int> OnCastleTierChanged;
#pragma warning restore CS0067

    public event Action<int> OnLivesChanged;
    public event Action<int> OnWaveChanged;
    public event Action<int> OnCurrencyChanged;
    public event Action OnGameOver;
    public event Action OnGameWin;

    public int currWave;
    public int currency;
    public int livesCount;
    public int enemiesKilled = 0;

    [Header("Kết quả màn chơi")]
    [Tooltip("Đã thắng màn này (chặn WinGame bị gọi lặp, chặn Game Over đè lên Win)")]
    public bool hasWon = false;
    [Tooltip("Số sao (1-3) của lần thắng gần nhất - GameWinScreen đọc để hiện đánh giá")]
    public int lastCupsEarned = 0;

    private void Awake()
    {
        if (main != null && main != this)
        {
            Destroy(gameObject);
            return;
        }
        main = this;

        // Tự động nhận diện levelNumber từ tên Scene (VD: "Level_3" -> 3)
        string scName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (!string.IsNullOrEmpty(scName) && scName.StartsWith("Level_"))
        {
            string numPart = scName.Substring(6);
            if (int.TryParse(numPart, out int parsedNum))
            {
                levelNumber = parsedNum;
            }
        }

        EnsurePathAndWaypoints();

        if (Camera.main != null)
        {
            Camera.main.orthographic = true;
            Camera.main.orthographicSize = GameScale.ViewOrthoSize;
            // Map nằm dưới thanh HUD trên: dịch tâm camera lên một đoạn (xem GameScale.TopHudFraction)
            Camera.main.transform.position = new Vector3(0, GameScale.OverviewCameraY(GameScale.ViewOrthoSize), -10f);
            Camera.main.clearFlags = CameraClearFlags.SolidColor;
            Camera.main.backgroundColor = new Color(0.07f, 0.07f, 0.11f, 1f);
        }
    }

    [Header("Multi-Route (tự gán bởi TDSceneGenerator)")]
    public LevelPaths paths;
    [Tooltip("Số wave của màn (0 = dùng bảng GameDataBalancer)")]
    public int totalWavesOverride = 0;

    public int TotalWaves
    {
        get { return totalWavesOverride > 0 ? totalWavesOverride : GameDataBalancer.GetBalance(levelNumber).maxWaves; }
    }

    /// <summary>Số tuyến đường hiện có (1..N).</summary>
    public int RouteCount
    {
        get
        {
            if (paths != null && paths.RouteCount > 0) return paths.RouteCount;
            if (Path2 != null && Path2.Length > 0) return 2;
            if (Path != null && Path.Length > 0) return 1;
            return 0;
        }
    }

    /// <summary>Lấy danh sách waypoint của tuyến id (id sai sẽ dùng tuyến 0).</summary>
    public Transform[] GetRoute(int id)
    {
        if (paths != null && paths.RouteCount > 0) return paths.GetRoute(id);
        if (id == 1 && Path2 != null && Path2.Length > 0) return Path2;
        return Path;
    }

    /// <summary>Chọn tuyến cho quái sắp spawn (chia đều giữa các tuyến/spawn).</summary>
    public int PickRouteId()
    {
        if (paths != null && paths.RouteCount > 0) return paths.PickRouteId();
        if (Path2 != null && Path2.Length > 0) return UnityEngine.Random.value > 0.5f ? 1 : 0;
        return 0;
    }

    /// <summary>Vị trí spawn của tuyến id = waypoint đầu tiên của chính tuyến đó (không bao giờ lệch).</summary>
    public Vector3 GetSpawnPosition(int id)
    {
        Transform[] r = GetRoute(id);
        if (r != null && r.Length > 0) return r[0].position;
        if (StartPoint != null) return StartPoint.position;
        return new Vector3(-12f, 0f, 0f);
    }

    public void EnsurePathAndWaypoints()
    {
        GameObject wpObj = GameObject.Find("Waypoints") ?? GameObject.Find("Path");
        if (wpObj != null)
        {
            if (paths == null) paths = wpObj.GetComponent<LevelPaths>();
            if (paths == null) paths = wpObj.AddComponent<LevelPaths>();
            if (paths.RouteCount == 0) paths.Rebuild();

            if (paths.RouteCount > 0)
            {
                // Giữ tương thích với code cũ: Path = tuyến 0, Path2 = tuyến 1
                Path = paths.GetRoute(0);
                Path2 = paths.RouteCount > 1 ? paths.GetRoute(1) : null;
            }

            if (Path != null && Path.Length > 0) StartPoint = Path[0];
            if (Path2 != null && Path2.Length > 0) StartPoint2 = Path2[0];
        }

        if (StartPoint == null && Path != null && Path.Length > 0) StartPoint = Path[0];
        if (StartPoint2 == null && Path2 != null && Path2.Length > 0) StartPoint2 = Path2[0];

        if (StartPoint == null)
        {
            GameObject fallback = new GameObject("DefaultStartPoint");
            fallback.transform.position = new Vector3(-12f, 0f, 0f);
            StartPoint = fallback.transform;
        }
    }


    private void Start()
    {
        EnsurePathAndWaypoints();

        int levelNum = levelNumber;
        GameDataBalancer.LevelBalance balance = GameDataBalancer.GetBalance(levelNum);

        currWave = startWave;
        currency = (startingGold != 250) ? startingGold : balance.startingGold;
        int initialLives = (startingLives != 20) ? startingLives : balance.startingLives;

        // Khởi tạo CastleManager nếu chưa có trong Scene
        if (CastleManager.main == null)
        {
            gameObject.AddComponent<CastleManager>();
        }

        if (CastleManager.main != null)
        {
            CastleManager.main.maxHealth = initialLives * 5f;
            CastleManager.main.currentHealth = initialLives * 5f;
        }

        currentCastleHealth = CastleManager.main != null ? CastleManager.main.currentHealth : initialLives * 5f;
        livesCount = initialLives;

        // 1. Tự động triển khai Hero đã chọn từ MainMenu vào trận
        HeroDeployer heroDeployer = Object.FindAnyObjectByType<HeroDeployer>();
        if (heroDeployer == null)
        {
            heroDeployer = gameObject.AddComponent<HeroDeployer>();
        }
        else
        {
            heroDeployer.DeploySelectedHero();
        }

        // 2. Tự động kích hoạt Bộ theo dõi Nhiệm vụ màn chơi
        MissionTracker mt = Object.FindAnyObjectByType<MissionTracker>();
        if (mt == null)
        {
            mt = gameObject.AddComponent<MissionTracker>();
        }
        mt.EnsureMissionData();

        // 3. Đảm bảo EventSystem dùng đúng InputModule (khắc phục lỗi không bấm được UI trên New Input System)
        EventSystem es = Object.FindAnyObjectByType<EventSystem>();
        if (es != null)
        {
            GameInput.EnsureCorrectInputModule(es.gameObject);
        }

        // 4. Phát sự kiện khởi đầu an toàn (không để exception của một listener làm ngắt chuỗi)
        try { OnCastleHealthChanged?.Invoke(currentCastleHealth, maxCastleHealth); } catch (Exception ex) { Debug.LogWarning($"[LevelManager] OnCastleHealthChanged exception: {ex.Message}"); }
        try { OnLivesChanged?.Invoke(livesCount); } catch (Exception ex) { Debug.LogWarning($"[LevelManager] OnLivesChanged exception: {ex.Message}"); }
        try { OnCurrencyChanged?.Invoke(currency); } catch (Exception ex) { Debug.LogWarning($"[LevelManager] OnCurrencyChanged exception: {ex.Message}"); }
        try { OnWaveChanged?.Invoke(currWave); } catch (Exception ex) { Debug.LogWarning($"[LevelManager] OnWaveChanged exception: {ex.Message}"); }
    }

    public void NotifyHealthAndLivesChanged()
    {
        if (CastleManager.main != null)
        {
            currentCastleHealth = CastleManager.main.currentHealth;
            maxCastleHealth = CastleManager.main.maxHealth;
            currentCastleTier = CastleManager.main.currentCastleTier;
            OnCastleTierChanged?.Invoke(currentCastleTier);
        }

        livesCount = Mathf.CeilToInt(currentCastleHealth);
        OnCastleHealthChanged?.Invoke(currentCastleHealth, maxCastleHealth);
        OnLivesChanged?.Invoke(livesCount);
    }

    public void IncreaseCurrency(int amount)
    {
        currency += amount;
        OnCurrencyChanged?.Invoke(currency);
    }

    public void RegisterEnemyKilled()
    {
        enemiesKilled++;
    }

    // Bộ đếm kill trên HUD (StatsUI) trước đây không bao giờ tăng vì không ai gọi RegisterEnemyKilled:
    // nghe sự kiện quái chết để đếm đúng.
    private void OnEnable()
    {
        EnemySpawner.onEnemyKilled.AddListener(RegisterEnemyKilled);
    }

    private void OnDisable()
    {
        EnemySpawner.onEnemyKilled.RemoveListener(RegisterEnemyKilled);
    }

    public void AddGold(int amount)
    {
        IncreaseCurrency(amount);
    }

    public void IncreaseGold(int amount)
    {
        IncreaseCurrency(amount);
    }

    // Used when purchasing items
    public bool SpendCurrency(int amount)
    {
        if (amount <= currency)
        {
            currency -= amount;
            OnCurrencyChanged?.Invoke(currency);
            return true;
        }
        else
        {
            Debug.Log("Not enough currency");
            return false;
        }
    }

    public void IncreaseLives(int amount)
    {
        if (CastleManager.main != null)
        {
            CastleManager.main.Heal(amount * 5f);
        }
        else
        {
            currentCastleHealth = Mathf.Min(maxCastleHealth, currentCastleHealth + amount * 5f);
            NotifyHealthAndLivesChanged();
        }
    }

    public void LoseCastleHealth(float amount)
    {
        lastCastleDamageTime = Time.time;
        if (CastleManager.main != null)
        {
            CastleManager.main.TakeDamage(amount);
        }
        else
        {
            currentCastleHealth = Mathf.Max(0f, currentCastleHealth - amount);
            NotifyHealthAndLivesChanged();

            if (currentCastleHealth <= 0f)
            {
                EndGame();
            }
        }
    }

    // decrease amount of lives and check if player loses 
    public void LoseLives(int amount)
    {
        LoseCastleHealth(amount * 5f);
    }

    public void EndGame()
    {
        if (hasWon) return;   // đã thắng thì không cho Game Over đè lên dialog chiến thắng
        Debug.Log("Game Over");
        Time.timeScale = 0f;
        OnGameOver?.Invoke();

        // Đảm bảo hiển thị màn hình Game Over
        GameOverScreen gos = UnityEngine.Object.FindAnyObjectByType<GameOverScreen>();
        if (gos == null)
        {
            Canvas canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
            if (canvas != null)
            {
                GameObject goObj = new GameObject("GameOverScreen");
                goObj.transform.SetParent(canvas.transform, false);
                gos = goObj.AddComponent<GameOverScreen>();
            }
        }

        if (gos != null)
        {
            gos.ShowGameOver();
        }
    }

    public bool CheckLevelCompletion()
    {
        if (MissionTracker.main != null)
        {
            return MissionTracker.main.CheckWinCondition();
        }
        return false;
    }

    public void WinGame()
    {
        if (hasWon) return;   // WinGame có thể bị gọi từ MissionTracker, EnemySpawner, MissionDirector: chỉ xử lý 1 lần
        hasWon = true;

        Debug.Log("You Win!");
        Time.timeScale = 0f;

        // Tính sao TRƯỚC khi phát sự kiện để GameWinScreen có đủ dữ liệu
        int cupsEarned = 1;
        try
        {
            if (MissionTracker.main != null)
            {
                cupsEarned = MissionTracker.main.EvaluateStarsAndSave();
            }
            else if (SaveManager.instance != null)
            {
                if (livesCount == startingLives) cupsEarned = 3;
                else if (livesCount >= Mathf.Max(1, startingLives / 2)) cupsEarned = 2;
                SaveManager.instance.SaveLevelProgress(levelNumber, cupsEarned);
            }
        }
        catch (Exception ex) { Debug.LogException(ex); }
        lastCupsEarned = Mathf.Clamp(cupsEarned, 1, 3);

        try { OnGameWin?.Invoke(); }
        catch (Exception ex) { Debug.LogException(ex); }

        // Đảm bảo luôn có màn hình Game Win (tự dựng giao diện + sao lúc chạy); lỗi ở bước trước không được chặn bước này
        try
        {
            GameWinScreen gws = UnityEngine.Object.FindAnyObjectByType<GameWinScreen>();
            if (gws == null)
            {
                GameObject winObj = new GameObject("GameWinScreen");
                gws = winObj.AddComponent<GameWinScreen>();
            }
            gws.ToggleGameWinPanel();
        }
        catch (Exception ex) { Debug.LogException(ex); }
    }

    public void SetWave(int wave)
    {
        currWave = wave;
        OnWaveChanged?.Invoke(currWave);
    }

    public int GetCurrentWave()
    {
        return currWave;
    }
}
