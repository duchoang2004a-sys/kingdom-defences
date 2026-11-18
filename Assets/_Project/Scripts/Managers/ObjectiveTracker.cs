using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>Trạng thái một sao trong trận (Bước 2-3).</summary>
public enum ObjectiveStatus
{
    Active,   // còn đạt được (sao sáng)
    Done,     // đã đạt
    Failed    // đã hỏng (sao xám)
}

public class ObjectiveState
{
    public int star;                       // 1 = nhiệm vụ chính, 2 và 3 = mục tiêu phụ
    public ObjectiveData data;
    public ObjectiveStatus status = ObjectiveStatus.Active;
    public string failReason = "";

    public bool IsMain { get { return star == 1; } }
    public string Type { get { return data != null && data.type != null ? data.type : ""; } }
}

/// <summary>
/// Bước 2-3 (KingOfRush_Nang_cap_chi_tiet.docx, mục 5 và 7): bộ theo dõi nhiệm vụ 3 sao của màn chơi.
/// - Đọc điều kiện từ phần "objectives" của Resources/story_intro.json (Bảng 22).
/// - Sao 1 = nhiệm vụ chính; sao 2 và 3 = mục tiêu phụ. Mục tiêu phụ hỏng thì không hồi lại.
/// - Nghe các sự kiện có sẵn của game (GameEvents, LevelManager) nên không phải sửa logic quái/tháp;
///   chỉ có 4 điểm gọi thêm một dòng: phép (SpellManager), anh hùng gục (Hero), bán tháp (TowerUI), khóa tháp (chưa có cơ chế).
/// - Không có dữ liệu objectives cho màn: không tạo gì, luồng nhiệm vụ cũ (MissionTracker) giữ nguyên.
/// - Trùm nhận diện theo tên prefab (TrollKing, Shaman, GolemIce, DemonLord, DragonBoss), không dựa vào Enemy.IsBoss (IsBoss cũng đúng với Troll/Golem thường).
/// </summary>
public class ObjectiveTracker : MonoBehaviour
{
    public static ObjectiveTracker Instance { get; private set; }
    public static bool IsActive { get { return Instance != null && Instance.hasData; } }

    /// <summary>Phát khi tiến độ/trạng thái đổi (khung HUD đọc lại).</summary>
    public static event Action Changed;
    /// <summary>Phát khi một mục tiêu phụ vừa hỏng (HUD hiện lý do 3 giây).</summary>
    public static event Action<ObjectiveState> ObjectiveFailed;

    public readonly ObjectiveState[] states = new ObjectiveState[3];
    public int Level { get; private set; }
    public string LevelName { get; private set; }
    public string Chapter { get; private set; }
    private float ownElapsed;
    /// <summary>Giây chơi thật (không tính lúc Thẻ nhiệm vụ mở hay tạm dừng). Dùng chung đồng hồ với MissionTracker để khớp điều kiện thắng.</summary>
    public float Elapsed { get { return MissionTracker.main != null ? MissionTracker.main.elapsedTime : ownElapsed; } }
    public bool ClockRunning { get; private set; }
    public int BossesKilled { get; private set; }
    public int LivesLost { get; private set; }
    public int TowerLocks { get; private set; }

    private bool hasData;
    private bool finished;
    private float nextPoll;
    private readonly Dictionary<int, float> bossSpawn = new Dictionary<int, float>();

    private static readonly string[] SpellIds = { "Meteor", "Freeze", "Reinforcements" };

    private bool Live { get { return hasData && !finished; } }

    private ObjectiveHud hud;
    private int hudTries;

    /// <summary>Nhiệm vụ chính là "diệt trùm": MissionTracker đọc số trùm đã hạ từ BossesKilled để đếm đúng loại trùm của Bảng 22.</summary>
    public bool MainIsKillBosses { get { return hasData && states[0] != null && states[0].Type == "KILL_BOSSES"; } }

    // ------------------------------------------------------------------ khởi tạo
    /// <summary>Gọi một lần ở cuối LevelManager.Start: dựng bộ theo dõi, khung HUD và hiện Thẻ nhiệm vụ.</summary>
    public static void BeginLevel(int level)
    {
        if (Instance != null) return;
        StoryLevelData sd = StoryDatabase.GetLevel(level);
        LevelObjectivesData obj = sd != null ? sd.objectives : null;
        if (obj == null || obj.star1 == null || obj.star2 == null || obj.star3 == null) return;

        GameObject go = new GameObject("ObjectiveTracker");
        ObjectiveTracker t = go.AddComponent<ObjectiveTracker>();
        t.Setup(level, sd);
        t.hud = ObjectiveHud.Create(t);
        MissionCardUI.ShowAtLevelStart(level, t.StartClock);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Setup(int level, StoryLevelData sd)
    {
        Level = level;
        LevelName = sd.name;
        Chapter = sd.chapter;
        for (int i = 0; i < 3; i++)
        {
            ObjectiveState s = new ObjectiveState();
            s.star = i + 1;
            s.data = sd.objectives.Get(i + 1);
            states[i] = s;
        }
        hasData = true;
        Subscribe();
    }

    /// <summary>Đóng Thẻ nhiệm vụ: đồng hồ trận (và đồng hồ đợt đầu tiên của game) bắt đầu chạy.</summary>
    public void StartClock()
    {
        ClockRunning = true;
        Raise();
    }

    private void Subscribe()
    {
        GameEvents.OnEnemyEscaped += HandleEscaped;
        GameEvents.OnEnemyKilled += HandleKilled;
        GameEvents.OnSpellOrItemUsed += HandleSpell;
        if (LevelManager.main != null)
        {
            LevelManager.main.OnCastleHealthChanged += HandleCastleHp;
            LevelManager.main.OnWaveChanged += HandleWave;
            LevelManager.main.OnGameOver += HandleGameOver;
        }
    }

    private void Unsubscribe()
    {
        GameEvents.OnEnemyEscaped -= HandleEscaped;
        GameEvents.OnEnemyKilled -= HandleKilled;
        GameEvents.OnSpellOrItemUsed -= HandleSpell;
        if (LevelManager.main != null)
        {
            LevelManager.main.OnCastleHealthChanged -= HandleCastleHp;
            LevelManager.main.OnWaveChanged -= HandleWave;
            LevelManager.main.OnGameOver -= HandleGameOver;
        }
    }

    private void OnDestroy()
    {
        Unsubscribe();
        if (Instance == this) Instance = null;
    }

    // ------------------------------------------------------------------ sự kiện từ game
    private void HandleEscaped(Enemy enemy, int livesLost)
    {
        if (!Live) return;
        LivesLost += Mathf.Max(0, livesLost);
        if (enemy != null) bossSpawn.Remove(enemy.GetInstanceID());

        for (int i = 1; i < 3; i++)
        {
            ObjectiveState s = states[i];
            if (s.status != ObjectiveStatus.Active) continue;
            switch (s.Type)
            {
                case "NO_LEAK":
                    Fail(s, "Đã để quái lọt vào lâu đài");
                    break;
                case "MAX_LIVES_LOST":
                    if (LivesLost > s.data.count) Fail(s, "Đã mất quá " + s.data.count + " mạng");
                    break;
                case "NO_LEAK_ENEMY":
                    if (Matches(enemy, s.data.enemy)) Fail(s, DisplayName(s.data.enemy) + " đã lọt vào lâu đài");
                    break;
                case "KILL_BOSS_BEFORE_LANDING":
                    if (Matches(enemy, s.data.enemy)) Fail(s, DisplayName(s.data.enemy) + " đã đáp xuống lâu đài");
                    break;
            }
        }
        Raise();
    }

    private void HandleKilled(Enemy enemy, DamageInfo info)
    {
        if (!Live || enemy == null) return;
        bossSpawn.Remove(enemy.GetInstanceID());
        if (enemy.depthLevel != 0) return;   // quái con của trùm không tính; trùm nhận diện theo tên prefab (Matches), không dựa vào IsBoss

        ObjectiveData main = states[0].data;
        if (main != null && main.type == "KILL_BOSSES" && Matches(enemy, main.enemy)) BossesKilled++;
        Raise();
    }

    private void HandleSpell(string id)
    {
        if (!Live || Array.IndexOf(SpellIds, id) < 0) return;   // "PoisonZone" (bình độc) không phải phép
        FailType("NO_SPELLS", "Đã dùng phép thuật");
    }

    private void HandleCastleHp(float current, float max)
    {
        if (!Live || max <= 0f) return;
        float pct = current / max * 100f;
        for (int i = 1; i < 3; i++)
        {
            ObjectiveState s = states[i];
            if (s.status == ObjectiveStatus.Active && s.Type == "CASTLE_HP_ABOVE" && pct + 0.001f < s.data.percent)
                Fail(s, "Lâu đài còn dưới " + Mathf.RoundToInt(s.data.percent) + "% máu");
        }
    }

    private void HandleWave(int wave)
    {
        if (Live) Raise();
    }

    private void HandleGameOver()
    {
        finished = true;
    }

    // ------------------------------------------------------------------ điểm gọi thêm từ code game (null-safe)
    public static void NotifyHeroDown()
    {
        if (Instance != null && Instance.Live) Instance.FailType("HERO_NOT_DOWN", "Anh hùng đã bị hạ gục");
    }

    public static void NotifyTowerSold()
    {
        if (Instance != null && Instance.Live) Instance.FailType("NO_SELL_TOWER", "Đã bán tháp");
    }

    /// <summary>Gọi mỗi khi một tháp bị khóa (cơ chế khóa tháp của Demon Chúa Lửa; game hiện chưa có cơ chế này).</summary>
    public static void NotifyTowerLocked()
    {
        if (Instance == null || !Instance.Live) return;
        Instance.TowerLocks++;
        for (int i = 1; i < 3; i++)
        {
            ObjectiveState s = Instance.states[i];
            if (s.status == ObjectiveStatus.Active && s.Type == "NO_TOWER_LOCK_OVER" && Instance.TowerLocks > s.data.count)
                Instance.Fail(s, "Có tháp bị khóa quá " + s.data.count + " lần");
        }
    }

    // ------------------------------------------------------------------ vòng lặp
    private void Update()
    {
        if (hud == null && hudTries < 180)   // Canvas_UI có thể chưa có lúc LevelManager.Start: thử lại trong vài giây đầu
        {
            hudTries++;
            hud = ObjectiveHud.Create(this);
        }
        if (!Live) return;
        if (ClockRunning) ownElapsed += Time.deltaTime;
        if (!ClockRunning || Time.unscaledTime < nextPoll) return;
        nextPoll = Time.unscaledTime + 0.25f;
        Poll();
    }

    private void Poll()
    {
        for (int i = 1; i < 3; i++)
        {
            ObjectiveState s = states[i];
            if (s.status != ObjectiveStatus.Active) continue;

            if (s.Type == "TOWER_LV3_BEFORE")
            {
                if (CountTowersAtLevel3() >= s.data.count)
                {
                    s.status = ObjectiveStatus.Done;
                    Raise();
                }
                else if (Elapsed > s.data.seconds)
                {
                    Fail(s, "Chưa đủ tháp cấp 3 trước phút thứ " + Mathf.CeilToInt(s.data.seconds / 60f));
                }
            }
            else if (s.Type == "BOSS_KILL_WITHIN")
            {
                WatchBosses(s);
            }
        }
    }

    private static int CountTowersAtLevel3()
    {
        int n = 0;
        Turret[] turrets = UnityEngine.Object.FindObjectsByType<Turret>(FindObjectsSortMode.None);
        for (int i = 0; i < turrets.Length; i++)
        {
            if (turrets[i] != null && turrets[i].currentLevel >= 2) n++;   // currentLevel là chỉ số từ 0: 2 = cấp 3
        }
        Barracks[] barracks = UnityEngine.Object.FindObjectsByType<Barracks>(FindObjectsSortMode.None);
        for (int i = 0; i < barracks.Length; i++)
        {
            if (barracks[i] != null && barracks[i].currentLevel >= 2) n++;
        }
        return n;
    }

    /// <summary>Mỗi trùm cùng loại phải bị hạ trong "seconds" giây kể từ lúc xuất hiện; quá hạn là hỏng sao.</summary>
    private void WatchBosses(ObjectiveState s)
    {
        IReadOnlyList<EnemyHealth> list = EnemyRegistry.ActiveEnemies;
        for (int i = 0; i < list.Count; i++)
        {
            EnemyHealth eh = list[i];
            if (eh == null) continue;
            Enemy e = eh.GetComponent<Enemy>();
            if (e == null || e.depthLevel != 0 || !Matches(e, s.data.enemy)) continue;

            int id = e.GetInstanceID();
            float born;
            if (!bossSpawn.TryGetValue(id, out born))
            {
                bossSpawn[id] = Time.time;
                continue;
            }
            if (Time.time - born > s.data.seconds)
            {
                Fail(s, "Quá " + Mathf.RoundToInt(s.data.seconds) + " giây mà chưa hạ " + DisplayName(s.data.enemy));
                return;
            }
        }
    }

    // ------------------------------------------------------------------ kết quả
    /// <summary>Tính số sao khi thắng (1-3). Gọi nhiều lần vẫn cho cùng kết quả.</summary>
    public int EvaluateStars()
    {
        if (!hasData) return 1;
        if (!finished)
        {
            finished = true;
            states[0].status = ObjectiveStatus.Done;
            for (int i = 1; i < 3; i++)
            {
                ObjectiveState s = states[i];
                if (s.status != ObjectiveStatus.Active) continue;
                if (s.Type == "TOWER_LV3_BEFORE") Fail(s, "Chưa đủ tháp cấp 3 trước thời hạn");
                else s.status = ObjectiveStatus.Done;   // mục tiêu dạng "không để / không dùng / còn trên": không hỏng là đạt
            }
            Raise();
        }

        int stars = 1;
        for (int i = 1; i < 3; i++)
        {
            if (states[i].status == ObjectiveStatus.Done) stars++;
        }
        return Mathf.Clamp(stars, 1, 3);
    }

    /// <summary>Màn Kết quả: liệt kê từng sao kèm lý do đạt hoặc không đạt (Bảng 22). Chốt kết quả trước khi liệt kê.</summary>
    public void AppendResultLines(StringBuilder sb)
    {
        if (!hasData || sb == null) return;
        EvaluateStars();
        for (int i = 0; i < 3; i++)
        {
            ObjectiveState s = states[i];
            if (s == null || s.data == null) continue;
            bool ok = s.status == ObjectiveStatus.Done;
            sb.Append(ok ? "<color=#77ff99>[v]</color> " : "<color=#8a8fa8>[ ]</color> ");
            sb.Append(s.data.text);
            if (ok) sb.Append(i == 0 ? "  (nhiệm vụ chính)" : "  (đạt)");
            else sb.Append("  <color=#ff8f8f>- ").Append(string.IsNullOrEmpty(s.failReason) ? "không đạt" : s.failReason).Append("</color>");
            sb.Append('\n');
        }
    }

    // ------------------------------------------------------------------ hiển thị
    /// <summary>Dòng tiến độ nhiệm vụ chính cho HUD: "Đợt 4/8", "Còn 02:35", "Trùm 1/2".</summary>
    public string MainProgressText()
    {
        ObjectiveState m = states[0];
        if (m == null || m.data == null) return "";
        switch (m.Type)
        {
            case "SURVIVE_WAVES":
            {
                int goal = (LevelManager.main != null && LevelManager.main.TotalWaves > 0) ? LevelManager.main.TotalWaves : Mathf.Max(1, m.data.count);   // cùng mẫu số với MissionTracker.GetWaveGoal
                int cur = LevelManager.main != null ? LevelManager.main.currWave : 1;
                return "Đợt " + Mathf.Clamp(cur, 1, goal) + "/" + goal;
            }
            case "HOLD_TIME":
            {
                float rem = Mathf.Max(0f, m.data.seconds - Elapsed);
                int mm = (int)(rem / 60f);
                int ss = Mathf.CeilToInt(rem - mm * 60f);
                if (ss >= 60) { mm++; ss = 0; }
                return "Còn " + mm.ToString("00") + ":" + ss.ToString("00");
            }
            case "KILL_BOSSES":
                return "Trùm " + Mathf.Min(BossesKilled, Mathf.Max(1, m.data.count)) + "/" + Mathf.Max(1, m.data.count);
        }
        return m.data.text;
    }

    // ------------------------------------------------------------------ nội bộ
    private void FailType(string type, string reason)
    {
        for (int i = 1; i < 3; i++)
        {
            ObjectiveState s = states[i];
            if (s.status == ObjectiveStatus.Active && s.Type == type) Fail(s, reason);
        }
    }

    private void Fail(ObjectiveState s, string reason)
    {
        if (s == null || s.status != ObjectiveStatus.Active) return;
        s.status = ObjectiveStatus.Failed;
        s.failReason = reason;
        try { if (ObjectiveFailed != null) ObjectiveFailed(s); }
        catch (Exception ex) { Debug.LogException(ex); }
        Raise();
    }

    private static void Raise()
    {
        try { if (Changed != null) Changed(); }
        catch (Exception ex) { Debug.LogException(ex); }
    }

    // ------------------------------------------------------------------ nhận diện quái theo mã trong tài liệu
    private static string Norm(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        string low = s.ToLowerInvariant().Replace("(clone)", "");
        StringBuilder sb = new StringBuilder(low.Length);
        for (int i = 0; i < low.Length; i++)
        {
            if (char.IsLetter(low[i])) sb.Append(low[i]);
        }
        return sb.ToString();
    }

    /// <summary>Mã quái trong tài liệu (GoblinArcher, Golem, TrollBoss, Shaman, IceGolem, FireDemon, Dragon) khớp với quái trong game không.</summary>
    public static bool Matches(Enemy e, string id)
    {
        if (e == null || string.IsNullOrEmpty(id)) return false;
        string n = Norm(e.EnemyName) + "|" + Norm(e.gameObject.name);
        switch (id)
        {
            case "GoblinArcher": return n.Contains("goblinarcher");
            case "Golem": return n.Contains("golem") && !n.Contains("ice");
            case "TrollBoss": return n.Contains("trollking") || n.Contains("trollboss");   // prefab TrollKing (Troll Chúa); Troll thường không tính
            case "Shaman": return n.Contains("shaman");
            case "IceGolem": return n.Contains("golemice") || n.Contains("icegolem");
            case "FireDemon": return n.Contains("demonlord") || n.Contains("firedemon");   // prefab DemonLord (Demon Chúa Lửa); Demon thường không tính
            case "Dragon": return n.Contains("dragon");
        }
        return n.Contains(Norm(id));
    }

    public static string DisplayName(string id)
    {
        switch (id)
        {
            case "GoblinArcher": return "Goblin Xạ thủ";
            case "Golem": return "Golem";
            case "TrollBoss": return "Troll Chúa";
            case "Shaman": return "Shaman";
            case "IceGolem": return "Golem Băng";
            case "FireDemon": return "Demon Chúa Lửa";
            case "Dragon": return "Rồng Hắc Ám";
        }
        return string.IsNullOrEmpty(id) ? "Quái" : id;
    }

    // ------------------------------------------------------------------ nối với hệ thống nhiệm vụ cũ (MissionTracker)
    /// <summary>
    /// Dựng nhiệm vụ chính của màn từ sao 1 trong dữ liệu (Bảng 22) để điều kiện thắng khớp tài liệu.
    /// Trả về null nếu màn không có dữ liệu: GameMissionDatabase dùng nhiệm vụ mặc định như cũ.
    /// </summary>
    public static LevelMissionData BuildMissionFromSpec(int level)
    {
        LevelObjectivesData o = StoryDatabase.GetObjectives(level);
        if (o == null || o.star1 == null) return null;

        LevelMissionData m = new LevelMissionData();
        m.missionTitle = o.star1.text;
        m.secondaryMissions = new List<SecondaryMissionData>();   // mục tiêu phụ do ObjectiveTracker theo dõi
        switch (o.star1.type)
        {
            case "SURVIVE_WAVES":
                m.missionType = MissionType.SurviveWaves;
                m.targetValue = Mathf.Max(1, o.star1.count);
                break;
            case "HOLD_TIME":
                m.missionType = MissionType.HoldTime;
                m.targetValue = Mathf.Max(1f, o.star1.seconds);
                break;
            case "KILL_BOSSES":
                m.missionType = MissionType.KillBosses;
                m.targetValue = Mathf.Max(1, o.star1.count);
                break;
            default:
                return null;
        }
        return m;
    }
}
