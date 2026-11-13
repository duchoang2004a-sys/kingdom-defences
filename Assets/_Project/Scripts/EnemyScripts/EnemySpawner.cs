using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class WaveGroup
{
    public GameObject enemyPrefab;
    public int count;
    public float rate; // Spawn rate (enemies per second for this group)
}

[System.Serializable]
public class WaveData
{
    public List<WaveGroup> waveGroups = new List<WaveGroup>();
    public float nextWaveDelay = 5f;
    // Optional scaling multiplier for this wave (HP/Damage)
    public float difficultyMultiplier = 1f;
}

public class EnemySpawner : MonoBehaviour
{
    public static UnityEvent onEnemyKilled = new UnityEvent();

    [Header("Wave Setup")]
    public List<WaveData> customWaves = new List<WaveData>();
    [Tooltip("Lặp lại vô tận cho Endless Mode. Tắt để có thể thắng màn (Win Game).")]
    public bool loopWavesEndlessly = false;
    [Tooltip("Số Wave tối đa của màn chơi (khi customWaves rỗng). Đạt số wave này và diệt hết quái sẽ Thắng Màn!")]
    public int maxWaves = 10;

    [Header("Độ khó theo màn (gán bởi TDSceneGenerator)")]
    public float levelHpMultiplier = 1f;

    [System.Serializable]
    public class BossSpawnEntry
    {
        public GameObject prefab;
        public int wave = 1;
        public float hp = 1000f;
    }

    [Header("Lịch Boss (gán bởi TDSceneGenerator)")]
    public BossSpawnEntry[] bossSchedule;

    [Header("Boss cũ (xuất hiện ở wave cuối)")]
    public GameObject bossPrefab;
    public float bossHpMultiplier = 2.5f;

    [Header("Enemy Prefabs (Kéo Prefabs Quái tùy chỉnh vào đây)")]
    [Tooltip("Kéo danh sách các Prefab Quái (Goblin, Orc, Troll, Boss...) vào mảng này")]
    public GameObject[] enemyPrefabs;
    [SerializeField] private int baseEnemies = 8; // stating amount of enemies that spawn on the first wave
    [SerializeField] private float enemiesPerSecond = .5f; // the amount of enemies that will spawn per seccond
    [SerializeField] private float enemiesPerSecondCap = 10f; // Caped amount of enemies per second
    [SerializeField] private float timeBetweenWaves = 5f;  // the time between the last enemy killed and the start of the next wave
    [SerializeField] private float difficultyScalingFactor = 0.75f; // used for scaling up the amount of enemies and enemies per second that spawn

    public static EnemySpawner main;

    // Wave Management variables
    private bool isSpawning = false; // are enemies currently spawning
    public bool IsSpawning => isSpawning;
    private int enemiesAlive;
    public int EnemiesAlive { get => enemiesAlive; set => enemiesAlive = value; }
    private Coroutine currentWaveCoroutine;
    private float currentDifficultyMultiplier = 1f;

    [Header("Early Wave Calling (Gọi Đợt Sớm)")]
    public bool isPreparingWave = false;
    public float prepTimeRemaining = 0f;
    public float totalPrepTime = 0f;
    private GameObject earlyWaveFlagObj;

    public void CallEarlyWave()
    {
        if (!isPreparingWave) return;

        int currentWaveNum = LevelManager.main != null ? LevelManager.main.GetCurrentWave() : 1;
        int nextWaveGold = 100 + currentWaveNum * 25;
        float ratio = Mathf.Clamp01(prepTimeRemaining / Mathf.Max(0.1f, totalPrepTime));
        int bonusGold = Mathf.Max(15, (int)System.Math.Round(0.20f * nextWaveGold * ratio));

        if (LevelManager.main != null)
        {
            LevelManager.main.AddGold(bonusGold);
        }

        Debug.Log($"<color=yellow>[Early Wave Call]</color> Gọi đợt sớm thành công! Thưởng: +{bonusGold} Vàng.");

        isPreparingWave = false;
        prepTimeRemaining = 0f;

        if (earlyWaveFlagObj != null)
        {
            Destroy(earlyWaveFlagObj);
        }
    }

    private void ShowEarlyWaveFlagUI()
    {
        if (earlyWaveFlagObj != null) Destroy(earlyWaveFlagObj);

        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        earlyWaveFlagObj = new GameObject("EarlyWaveFlag_Button");
        earlyWaveFlagObj.transform.SetParent(canvas.transform, false);

        UnityEngine.UI.Image img = earlyWaveFlagObj.AddComponent<UnityEngine.UI.Image>();
        img.color = new Color(0.95f, 0.75f, 0.2f, 0.9f);

        UnityEngine.UI.Button btn = earlyWaveFlagObj.AddComponent<UnityEngine.UI.Button>();
        btn.onClick.AddListener(CallEarlyWave);

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(earlyWaveFlagObj.transform, false);
        TMPro.TextMeshProUGUI txt = textObj.AddComponent<TMPro.TextMeshProUGUI>();
        txt.text = "⚡ GỌI SỚM (+VÀNG)";
        txt.fontSize = 18;
        txt.alignment = TMPro.TextAlignmentOptions.Center;
        txt.color = Color.white;

        RectTransform rect = earlyWaveFlagObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.15f);
        rect.anchorMax = new Vector2(0.5f, 0.15f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0, 0);
        rect.sizeDelta = new Vector2(200, 45);

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
    }

    private void Awake()
    {
        main = this;
    }

    void Start()
    {
        EnsureValidEnemyPrefabs();
        PreloadWaves();

        // Tự động khởi tạo BottomEnemyHUD trên Canvas nếu chưa có
        if (BottomEnemyHUD.main == null)
        {
            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas != null)
            {
                GameObject hudObj = new GameObject("BottomEnemyHUD");
                hudObj.transform.SetParent(canvas.transform, false);
                hudObj.AddComponent<BottomEnemyHUD>();
            }
        }

        StartCoroutine(StartWaveCoroutine()); // start the first wave
    }

    private void EnsureValidEnemyPrefabs()
    {
        List<GameObject> validList = new List<GameObject>();
        if (enemyPrefabs != null)
        {
            foreach (var p in enemyPrefabs)
            {
                if (p != null) validList.Add(p);
            }
        }

        // Nếu mảng rỗng, quét thư mục Prefabs/Enemies
        if (validList.Count == 0 && (customWaves == null || customWaves.Count == 0))
        {
#if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs/Enemies" });
            if (guids != null && guids.Length > 0)
            {
                foreach (string g in guids)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
                    GameObject go = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (go != null) validList.Add(go);
                }
            }
#endif
            if (validList.Count == 0)
            {
                GameObject resGoblin = Resources.Load<GameObject>("Enemies/Goblin") ?? Resources.Load<GameObject>("Goblin");
                if (resGoblin != null) validList.Add(resGoblin);
            }
        }

        if (validList.Count == 0 && (customWaves == null || customWaves.Count == 0))
        {
            CreateDefaultEnemyTemplate();
        }
        else if (validList.Count > 0)
        {
            enemyPrefabs = validList.ToArray();
        }
    }

    private void CreateDefaultEnemyTemplate()
    {
        GameObject enemyObj = new GameObject("DefaultGoblinTemplate");
        enemyObj.SetActive(false);
        enemyObj.transform.SetParent(transform);
        enemyObj.transform.position = new Vector3(-9999f, -9999f, 0f); // Ẩn hoàn toàn khỏi màn hình

        SpriteRenderer sr = enemyObj.AddComponent<SpriteRenderer>();
        sr.color = new Color(0.9f, 0.25f, 0.2f); // Reddish Goblin
        sr.sortingOrder = 5;

        Texture2D tex = new Texture2D(16, 16);
        Color[] colors = new Color[256];
        for (int i = 0; i < 256; i++) colors[i] = Color.white;
        tex.SetPixels(colors);
        tex.Apply();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);

        CircleCollider2D col = enemyObj.AddComponent<CircleCollider2D>();
        col.radius = 0.35f;
        col.isTrigger = true;

        Rigidbody2D rb = enemyObj.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;

        Enemy enemy = enemyObj.AddComponent<Enemy>();
        enemy.enemyName = "Goblin";
        enemy.moveSpeed = 2f;
        enemy.maxHealth = 30f;
        enemy.goldReward = 15;

        enemyObj.AddComponent<EnemyHealth>();
        enemyObj.AddComponent<EnemyMovement>();

        enemyPrefabs = new GameObject[] { enemyObj };
        Debug.Log("[EnemySpawner] Đã khởi tạo quái mẫu Goblin.");
    }

    private void PreloadWaves()
    {
        if (ObjectPool.instance == null) return;

        if (customWaves.Count > 0)
        {
            foreach (WaveData wave in customWaves)
            {
                foreach (WaveGroup group in wave.waveGroups)
                {
                    if (group.enemyPrefab != null)
                    {
                        // Preload the requested amount
                        ObjectPool.instance.Preload(group.enemyPrefab, group.count);
                    }
                }
            }
        }
        else
        {
            // Preload a chunk for procedural
            if (enemyPrefabs != null)
            {
                foreach (GameObject prefab in enemyPrefabs)
                {
                    if (prefab != null)
                        ObjectPool.instance.Preload(prefab, 15);
                }
            }
        }
    }

    // Starts the wave, and start timeBetweenWaves timer
    private IEnumerator StartWaveCoroutine()
    {
        int waveIndex = LevelManager.main != null ? LevelManager.main.GetCurrentWave() - 1 : 0;
        if (waveIndex < 0) waveIndex = 0;

        float delay = (waveIndex == 0) ? 1.5f : timeBetweenWaves;

        if (customWaves.Count > 0 && waveIndex < customWaves.Count)
        {
            delay = waveIndex == 0 ? 1.5f : customWaves[waveIndex - 1].nextWaveDelay;
        }

        isPreparingWave = true;
        totalPrepTime = delay;
        prepTimeRemaining = delay;

        if (delay > 2f)
        {
            ShowEarlyWaveFlagUI();
        }

        while (prepTimeRemaining > 0f && isPreparingWave)
        {
            prepTimeRemaining -= Time.deltaTime;
            yield return null;
        }

        isPreparingWave = false;
        if (earlyWaveFlagObj != null)
        {
            Destroy(earlyWaveFlagObj);
        }

        isSpawning = true;
        currentWaveCoroutine = StartCoroutine(SpawnWaveRoutine(waveIndex));
    }

    private IEnumerator SpawnWaveRoutine(int waveIndex)
    {
        if (customWaves.Count > 0)
        {
            // CUSTOM WAVES LOGIC
            int safeIndex = loopWavesEndlessly ? waveIndex % customWaves.Count : Mathf.Min(waveIndex, customWaves.Count - 1);
            WaveData currentWave = customWaves[safeIndex];

            // Tăng dần độ khó nếu loop
            int loopCount = waveIndex / customWaves.Count;
            currentDifficultyMultiplier = currentWave.difficultyMultiplier + (loopCount * 0.5f);

            foreach (WaveGroup group in currentWave.waveGroups)
            {
                if (group.enemyPrefab == null) continue;
                for (int i = 0; i < group.count; i++)
                {
                    SpawnEnemy(group.enemyPrefab, currentDifficultyMultiplier);
                    yield return new WaitForSeconds(1f / group.rate);
                }
            }
        }
        else
        {
            // PROCEDURAL WAVES LOGIC
            if (enemyPrefabs == null || enemyPrefabs.Length == 0)
            {
                Debug.LogWarning("Chưa thiết lập Enemy Prefab trong EnemySpawner!");
                isSpawning = false;
                yield break;
            }

            int wave = waveIndex + 1;

            // Lấy dữ liệu cân bằng theo Level
            int levelNum = LevelManager.main != null ? LevelManager.main.levelNumber : 1;
            GameDataBalancer.LevelBalance balance = GameDataBalancer.GetBalance(levelNum);

            int enemiesToSpawn = Mathf.RoundToInt(balance.baseEnemies * Mathf.Pow(wave, difficultyScalingFactor));
            float rate = Mathf.Clamp(enemiesPerSecond * Mathf.Pow(wave, difficultyScalingFactor), 0f, enemiesPerSecondCap);

            // HP scaling: 1.0 + (wave * hpPerWave)
            currentDifficultyMultiplier = (1.0f + (wave * balance.hpPerWave)) * Mathf.Max(0.1f, levelHpMultiplier);

            for (int i = 0; i < enemiesToSpawn; i++)
            {
                int prefabIndex = Random.Range(0, enemyPrefabs.Length);
                if (enemyPrefabs[prefabIndex] != null)
                {
                    SpawnEnemy(enemyPrefabs[prefabIndex], currentDifficultyMultiplier);
                }
                yield return new WaitForSeconds(1f / rate);
            }

            // Boss theo lịch của màn (mỗi boss: prefab + wave xuất hiện + HP chỉ định)
            bool bossSpawned = false;
            if (bossSchedule != null)
            {
                for (int b = 0; b < bossSchedule.Length; b++)
                {
                    BossSpawnEntry entry = bossSchedule[b];
                    if (entry == null || entry.prefab == null || entry.wave != wave) continue;
                    yield return new WaitForSeconds(1.5f);
                    SpawnBoss(entry);
                    bossSpawned = true;
                }
            }

            // Tương thích cũ: 1 boss ở wave cuối
            int lastWave = LevelManager.main != null ? LevelManager.main.TotalWaves : balance.maxWaves;
            if (!bossSpawned && (bossSchedule == null || bossSchedule.Length == 0) && bossPrefab != null && wave >= lastWave)
            {
                yield return new WaitForSeconds(1.5f);
                SpawnEnemy(bossPrefab, currentDifficultyMultiplier * bossHpMultiplier);
            }
        }

        isSpawning = false;

        // Wait until all enemies are dead before starting next wave
        while (enemiesAlive > 0)
        {
            yield return null;
        }

        EndWave();
    }

    // Used to spawn enemies
    /// <summary>Spawn boss với HP chỉ định (quy đổi sang hệ số so với maxHealth của prefab).</summary>
    private void SpawnBoss(BossSpawnEntry entry)
    {
        Enemy proto = entry.prefab.GetComponent<Enemy>();
        float baseHp = proto != null ? proto.MaxHealth : 100f;
        float mult = baseHp > 0.01f ? entry.hp / baseHp : 1f;
        SpawnEnemy(entry.prefab, mult);
    }

    private void SpawnEnemy(GameObject prefabToSpawn, float difficultyMultiplier)
    {
        if (prefabToSpawn == null) return;

        if (LevelManager.main != null)
        {
            LevelManager.main.EnsurePathAndWaypoints();
        }

        // Chọn tuyến đường (chia đều giữa các tuyến) và spawn đúng tại waypoint đầu tiên của tuyến đó
        int routeId = LevelManager.main != null ? LevelManager.main.PickRouteId() : 0;
        Vector3 spawnPos = LevelManager.main != null ? LevelManager.main.GetSpawnPosition(routeId) : new Vector3(-12f, 0f, 0f);

        // Lệch nhẹ để quái không chồng lên nhau (nhỏ hơn ngưỡng chạm waypoint 0.2)
        spawnPos += new Vector3(Random.Range(-0.08f, 0.08f), Random.Range(-0.06f, 0.06f), 0f);

        GameObject spawned;
        if (ObjectPool.instance != null)
        {
            spawned = ObjectPool.instance.Spawn(prefabToSpawn, spawnPos, Quaternion.identity);
        }
        else
        {
            spawned = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);
        }

        enemiesAlive++;

        EnemyMovement em = spawned.GetComponent<EnemyMovement>();
        if (em != null)
        {
            em.SetPath(routeId);
            em.SetTargetImmediate();
        }

        // Apply difficulty multiplier to health
        EnemyHealth healthComponent = spawned.GetComponent<EnemyHealth>();
        if (healthComponent != null)
        {
            healthComponent.ApplyDifficulty(difficultyMultiplier);
        }

        if (BottomEnemyHUD.main != null && healthComponent != null)
        {
            BottomEnemyHUD.main.TrackEnemy(healthComponent);
        }
    }

    private void EndWave()
    {
        int newWave = LevelManager.main.GetCurrentWave() + 1;
        int levelNum = LevelManager.main != null ? LevelManager.main.levelNumber : 1;
        GameDataBalancer.LevelBalance balance = GameDataBalancer.GetBalance(levelNum);
        int totalWaves = customWaves.Count > 0 ? customWaves.Count : (LevelManager.main != null ? LevelManager.main.TotalWaves : balance.maxWaves);

        // Nếu màn chơi có nhiệm vụ HoldTime hoặc KillBosses, tiếp tục spawn quái cho đến khi hoàn thành mục tiêu
        bool isContinuousMission = MissionTracker.main != null && MissionTracker.main.missionData != null &&
            (MissionTracker.main.missionData.missionType == MissionType.HoldTime ||
             MissionTracker.main.missionData.missionType == MissionType.KillBosses);

        if (!loopWavesEndlessly && !isContinuousMission && newWave > totalWaves)
        {
            if (MissionTracker.main != null)
            {
                MissionTracker.main.EvaluateStarsAndSave();
            }
            LevelManager.main.WinGame();
            return;
        }
        LevelManager.main.SetWave(newWave); // Wave gets set in level manager
        StartCoroutine(StartWaveCoroutine());
    }

    // Scales the amount of enemies per wave with the difficuly scaling factior
    private int GetEnemiesPerWave(int wave)
    {
        return Mathf.RoundToInt(baseEnemies * Mathf.Pow(wave, difficultyScalingFactor));
    }

    // Scales the enemies per second with the difficuly scaling factior
    private float GetEnemiesPerSecond(int wave)
    {
        return Mathf.Clamp(enemiesPerSecond * Mathf.Pow(wave, difficultyScalingFactor), 0f, enemiesPerSecondCap);
    }

    // Called when an enemy gets destroyed
    private void EnemyDestroyed()
    {
        enemiesAlive--;
        if (BottomEnemyHUD.main != null)
        {
            BottomEnemyHUD.main.RefreshWaveInfo();
        }
    }

    private void OnEnable()
    {
        EnemyHealth.onEnemyDestroy.AddListener(EnemyDestroyed);
    }
    private void OnDisable()
    {
        EnemyHealth.onEnemyDestroy.RemoveListener(EnemyDestroyed);
    }
}
