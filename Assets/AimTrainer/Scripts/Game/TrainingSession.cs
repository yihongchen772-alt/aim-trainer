using System.Collections.Generic;
using AimTrainer.Audio;
using AimTrainer.Bots;
using AimTrainer.Combat;
using AimTrainer.Core;
using AimTrainer.Diagnostics;
using AimTrainer.Player;
using AimTrainer.Weapons;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace AimTrainer.Game
{
    /// <summary>
    /// 一局训练的流程：菜单 → 进行中（Esc 暂停）→ 结算。负责刷新假人、统计成绩、处理玩家死亡和回合重置。
    ///
    /// 假人来源：
    /// - BotSpawnArea（随机刷新区域）：定点甩枪、移动靶模式在这里随机找你看得见的位置。
    /// - BotSpawnPoint（固定点位）：对枪模式随机挑点位，预瞄模式同时用上所有标记了“预瞄”的点位
    ///   （选了 PrefireRoute 路线时只用路线上的点位，并从路线起点出发）。
    /// - HoldSpot（架枪位置）下面的入口点位：只在架枪模式使用，假人从入口冲出来。
    /// 某种来源不存在时会退而使用另一种。开始时自动收集场景里所有的点位、区域、路线和架枪位置。
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class TrainingSession : MonoBehaviour
    {
        public enum Phase
        {
            Menu,
            Playing,
            Paused,
            Results,
        }

        const float FlickRespawnDelay = 0.25f;
        const float StrafeRespawnDelay = 0.35f;
        const float DuelRespawnDelay = 1f;
        const float HoldRespawnMin = 0.5f;
        const float HoldRespawnMax = 1.6f;
        const float RoundRestartDelay = 1.5f;
        const float MinSpawnDistance = 6f;
        const float MaxSpawnDistance = 40f;

        [SerializeField] PlayerController player;
        [SerializeField] HitscanWeapon weapon;
        [SerializeField] Health playerHealth;
        [SerializeField] PlayerAudio playerAudio;
        [SerializeField] Transform playerSpawn;

        [Tooltip("可选：定点甩枪和移动靶模式的出生点。离随机刷新区域太远的地图（比如 A 点）用它把玩家直接放到包点附近。")]
        [SerializeField] Transform aimSpawn;
        [SerializeField] PerformanceSettings performance;
        [SerializeField] MovementDebugHud debugHud;

        // 开始时自动收集场景里所有的点位和刷新区域，新加的点位不需要手动登记。
        BotSpawnPoint[] spawnPoints = new BotSpawnPoint[0];
        BotSpawnArea[] spawnAreas = new BotSpawnArea[0];
        PrefireRoute[] routes = new PrefireRoute[0];
        HoldSpot[] holds = new HoldSpot[0];
        readonly RegionLookup regions = new RegionLookup();
        readonly AngleStats angleStats = new AngleStats();

        /// <summary>“继续上次训练”切换场景后，新场景加载完自动开始。</summary>
        static bool autoStartPending;

        readonly List<BotAgent> bots = new List<BotAgent>();
        readonly List<float> respawnQueue = new List<float>();
        readonly Dictionary<BotAgent, BotSpawnPoint> occupiedPoints = new Dictionary<BotAgent, BotSpawnPoint>();
        readonly RaycastHit[] rayBuffer = new RaycastHit[8];
        readonly SessionStats stats = new SessionStats();
        System.Random rng;
        int spawnSerial;

        float roundStartTime;
        float roundRestartAt = -1f;
        bool roundOver;
        string message = string.Empty;
        float messageUntil;

        // 本局开始时锁定的训练选项。暂停时在菜单里改选项，要“重新开始”才生效，成绩也按这一局实际的选项记录。
        int activeBotCount = 1;
        BotDifficulty activeDifficulty = BotDifficulty.Normal;
        string activeWeaponId = WeaponPresets.Vandal;

        public Phase CurrentPhase { get; private set; } = Phase.Menu;

        /// <summary>本局实际使用的模式（开始时从设置里锁定）。</summary>
        public TrainingMode ActiveMode { get; private set; } = TrainingMode.Flick;

        /// <summary>本局实际使用的时长（秒），0 表示不限时。</summary>
        public int ActiveDuration { get; private set; }
        public SessionStats Stats => stats;

        /// <summary>本局按点位（角度）的统计，预瞄、架枪和对枪模式有效。</summary>
        public AngleStats Angles => angleStats;

        /// <summary>场景里的预瞄路线（可能为空数组）。</summary>
        public IReadOnlyList<PrefireRoute> Routes => routes;

        /// <summary>场景里的架枪位置（可能为空数组）。</summary>
        public IReadOnlyList<HoldSpot> Holds => holds;

        /// <summary>当前在场的假人（包括正在倒下的）。</summary>
        public IReadOnlyList<BotAgent> Bots => bots;

        /// <summary>本局使用的预瞄路线，没有选或地图上没有路线时为 null。</summary>
        public PrefireRoute ActiveRoute { get; private set; }

        /// <summary>本局使用的架枪位置，不是架枪模式或地图上没有架枪位置时为 null。</summary>
        public HoldSpot ActiveHold { get; private set; }

        /// <summary>本局的路线或架枪位置名称，没有时为空。</summary>
        public string ActiveVariant => ActiveHold != null ? ActiveHold.Label : ActiveRoute != null ? ActiveRoute.Label : string.Empty;

        /// <summary>菜单正在等待按键（改键位）时为 true，这期间 Esc 不会暂停或继续。</summary>
        public static bool SuppressEscape { get; set; }
        public GameSettings Settings => GameSettings.Current;
        public PlayerController Player => player;
        public HitscanWeapon Weapon => weapon;
        public Health PlayerHealth => playerHealth;

        /// <summary>本局已进行的时间（秒，暂停时不走）。</summary>
        public float Elapsed { get; private set; }

        /// <summary>本局剩余时间（秒），不限时为 -1。</summary>
        public float Remaining => ActiveDuration > 0 ? Mathf.Max(0f, ActiveDuration - Elapsed) : -1f;

        /// <summary>当前回合已用时间（预瞄模式显示）。</summary>
        public float RoundTime => roundOver ? stats.LastRoundTime : Elapsed - roundStartTime;

        /// <summary>屏幕中央的提示文字（例如“你被击杀了”），没有时为空。</summary>
        public string CenterMessage => Time.unscaledTime < messageUntil ? message : string.Empty;

        /// <summary>上次受伤的时间（Time.unscaledTime），用于受伤红屏。</summary>
        public float LastDamageTime { get; private set; } = -10f;

        /// <summary>本局是否刷新了最好成绩（结算时有效）。</summary>
        public bool NewRecord { get; private set; }

        public BestRecords.Record PreviousBest { get; private set; }

        public string SceneTitle => SceneCatalog.TitleOf(SceneManager.GetActiveScene().name);

        public int AliveBots
        {
            get
            {
                int count = 0;
                foreach (BotAgent bot in bots)
                {
                    if (bot != null && !bot.IsDead) count++;
                }
                return count;
            }
        }

        /// <summary>位置所在的地图区域名称（例如“A 大道”），地图没有划分区域时为空。</summary>
        public string RegionAt(Vector3 position) => regions.Find(position.x, position.z);

        /// <summary>下一个加载的训练场景在加载完成后自动开始一局（“继续上次训练”用）。</summary>
        public static void RequestAutoStart() => autoStartPending = true;

        /// <summary>设置定点甩枪和移动靶模式专用的出生点（可选）。</summary>
        public void SetAimSpawn(Transform spawn) => aimSpawn = spawn;

        public void Configure(PlayerController controller, HitscanWeapon gun, Health health, PlayerAudio sounds,
            Transform spawn, PerformanceSettings perf, MovementDebugHud hud)
        {
            player = controller;
            weapon = gun;
            playerHealth = health;
            playerAudio = sounds;
            playerSpawn = spawn;
            performance = perf;
            debugHud = hud;
        }

        void Awake()
        {
            rng = new System.Random();
            if (player == null) player = FindInScene<PlayerController>();
            if (weapon == null && player != null) weapon = player.GetComponentInChildren<HitscanWeapon>();
            if (playerHealth == null && player != null) playerHealth = player.GetComponent<Health>();
            if (playerAudio == null && player != null) playerAudio = player.GetComponent<PlayerAudio>();
            holds = FindAllInScene<HoldSpot>();
            routes = FindAllInScene<PrefireRoute>();
            // 架枪位置下面的入口点位只在架枪模式使用，不参与对枪和预瞄。
            var freePoints = new List<BotSpawnPoint>();
            foreach (BotSpawnPoint point in FindAllInScene<BotSpawnPoint>())
            {
                if (point.GetComponentInParent<HoldSpot>() == null) freePoints.Add(point);
            }
            spawnPoints = freePoints.ToArray();
            spawnAreas = FindAllInScene<BotSpawnArea>();
            foreach (MapRegion region in FindAllInScene<MapRegion>())
            {
                regions.Add(region.Label, region.Min.x, region.Max.x, region.Min.y, region.Max.y);
            }

            if (player != null) player.ManageCursor = false;
            if (weapon != null) weapon.ShotFired += OnShotFired;
            if (playerHealth != null)
            {
                playerHealth.Damaged += OnPlayerDamaged;
                playerHealth.Died += OnPlayerDied;
            }
        }

        void Start()
        {
            LockActiveOptions();
            ApplySettings();
            Transform spawn = CurrentSpawn();
            if (player != null && spawn != null) player.Teleport(spawn.position, spawn.eulerAngles.y);
            EnterMenu();
            if (autoStartPending)
            {
                autoStartPending = false;
                StartSession();
            }
        }

        void OnApplicationQuit() => Settings.Save();

        void OnDestroy()
        {
            Settings.Save();
            Time.timeScale = 1f;
            if (weapon != null) weapon.ShotFired -= OnShotFired;
            if (playerHealth != null)
            {
                playerHealth.Damaged -= OnPlayerDamaged;
                playerHealth.Died -= OnPlayerDied;
            }
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused && CurrentPhase == Phase.Playing) Pause();
        }

        void Update()
        {
            // F1 切换的调试面板也记到设置里，菜单里的开关和下次启动都保持一致。
            if (debugHud != null && debugHud.Visible != Settings.showDebugPanel) Settings.showDebugPanel = debugHud.Visible;

            Keyboard keyboard = Keyboard.current;
            bool escape = keyboard != null && keyboard.escapeKey.wasPressedThisFrame && !SuppressEscape;
            if (CurrentPhase == Phase.Playing)
            {
                // 按 Esc，或者鼠标因为别的原因被释放（编辑器里按 Esc 会直接释放鼠标）时暂停。
                if (escape || Cursor.lockState != CursorLockMode.Locked)
                {
                    Pause();
                    return;
                }
                // 假人只改 Transform，物理世界要到下一次物理步才更新；每帧同步一次，刷新检查和射线都用最新位置。
                Physics.SyncTransforms();
                TickPlaying(Time.deltaTime);
            }
            else if (CurrentPhase == Phase.Paused && escape)
            {
#if !UNITY_EDITOR
                // 编辑器里按 Esc 总会释放鼠标，这里恢复也会立刻被打断，所以只在打包后的程序里用 Esc 继续。
                Resume();
#endif
            }
        }

        // ---------- 流程 ----------

        /// <summary>按当前设置开始新的一局。</summary>
        public void StartSession()
        {
            Settings.Save();
            LockActiveOptions();
            ApplySettings();
            stats.Reset();
            angleStats.Reset();
            Elapsed = 0f;
            NewRecord = false;
            PreviousBest = BestRecords.Get(RecordKey());
            FxPool.ClearHoles();
            StartRound();
            SetPhase(Phase.Playing);
        }

        public void Pause()
        {
            if (CurrentPhase != Phase.Playing) return;
            SetPhase(Phase.Paused);
        }

        public void Resume()
        {
            if (CurrentPhase != Phase.Paused) return;
            Settings.Save();
            ApplySettings();
            SetPhase(Phase.Playing);
        }

        /// <summary>提前结束本局并显示成绩。</summary>
        public void EndSession()
        {
            if (CurrentPhase != Phase.Playing && CurrentPhase != Phase.Paused) return;
            bool roundBased = ActiveMode == TrainingMode.Prefire;
            string key = RecordKey();
            PreviousBest = BestRecords.Get(key);
            NewRecord = BestRecords.Submit(key, stats, roundBased);
            if (stats.Shots > 0 || stats.Kills > 0 || stats.Deaths > 0) SessionHistory.Add(CreateHistoryEntry());
            SetPhase(Phase.Results);
        }

        HistoryEntry CreateHistoryEntry() => new HistoryEntry
        {
            time = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture),
            scene = SceneManager.GetActiveScene().name,
            mode = ActiveMode,
            difficulty = activeDifficulty,
            botCount = activeBotCount,
            duration = ActiveDuration,
            weapon = activeWeaponId,
            variant = ActiveVariant,
            score = stats.Score,
            kills = stats.Kills,
            deaths = stats.Deaths,
            accuracy = stats.Accuracy,
            headshotRate = stats.HeadshotRate,
            bestRoundTime = stats.BestRoundTime,
            averageKillTime = stats.AverageKillTime,
            seconds = Elapsed,
        };

        /// <summary>开始新的一局时，把菜单里的训练选项锁定为本局使用的选项。</summary>
        void LockActiveOptions()
        {
            ActiveMode = Settings.mode;
            activeDifficulty = Settings.difficulty;
            activeBotCount = Settings.botCount;
            ActiveDuration = Settings.durationSeconds;
            activeWeaponId = Settings.weaponId;
            if (weapon != null) weapon.SetStats(WeaponPresets.Create(activeWeaponId));
            ActiveRoute = ActiveMode == TrainingMode.Prefire ? FindRoute(Settings.prefireRoute) : null;
            ActiveHold = ActiveMode == TrainingMode.Hold ? FindHold(Settings.holdSpot) : null;
        }

        /// <summary>按名称找路线；名称为空或地图上没有这条路线时返回 null（使用所有预瞄点位）。</summary>
        public PrefireRoute FindRoute(string label)
        {
            if (string.IsNullOrEmpty(label)) return null;
            foreach (PrefireRoute route in routes)
            {
                if (route != null && route.Label == label) return route;
            }
            return null;
        }

        /// <summary>按名称找架枪位置；找不到时用地图上的第一个，地图上没有架枪位置时返回 null。</summary>
        public HoldSpot FindHold(string label)
        {
            foreach (HoldSpot hold in holds)
            {
                if (hold != null && hold.Label == label) return hold;
            }
            return holds.Length > 0 ? holds[0] : null;
        }

        /// <summary>
        /// 出生点：架枪模式在架枪位置，预瞄选了路线时在路线起点，定点甩枪和移动靶用专用出生点（如果有），其他用正常出生点。
        /// </summary>
        Transform CurrentSpawn()
        {
            if (ActiveHold != null) return ActiveHold.transform;
            if (ActiveRoute != null) return ActiveRoute.transform;
            bool aimMode = ActiveMode == TrainingMode.Flick || ActiveMode == TrainingMode.Strafe;
            return aimMode && aimSpawn != null ? aimSpawn : playerSpawn;
        }

        /// <summary>回到菜单（不保存成绩），场景里的假人全部清除。</summary>
        public void EnterMenu()
        {
            ClearBots();
            SetPhase(Phase.Menu);
        }

        public void LoadScene(string sceneName)
        {
            Settings.Save();
            Time.timeScale = 1f;
            SceneManager.LoadScene(sceneName);
        }

        public void QuitGame()
        {
            Settings.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>把灵敏度、音量、帧率上限等设置应用到场景里。</summary>
        public void ApplySettings()
        {
            GameSettings s = Settings;
            PlayerInputReader.Bindings = s.keys;
            if (player != null)
            {
                player.Sensitivity = s.sensitivity;
                player.InvertY = s.invertY;
            }
            AudioListener.volume = s.volume;
            if (performance != null) performance.SetFrameRateCap(s.frameRateCap);
            if (debugHud != null)
            {
                debugHud.Visible = s.showDebugPanel;
                debugHud.MouseDpi = s.mouseDpi;
            }
        }

        void SetPhase(Phase phase)
        {
            CurrentPhase = phase;
            bool playing = phase == Phase.Playing;
            Time.timeScale = playing ? 1f : 0f;
            if (player != null)
            {
                if (playing) player.LockCursor();
                else player.UnlockCursor();
            }
            RefreshInput();
            SetBotsFireEnabled(playing && !roundOver);
        }

        /// <summary>只有“进行中、回合没结束、玩家活着”时才能移动和开枪。</summary>
        void RefreshInput()
        {
            bool enabled = CurrentPhase == Phase.Playing && !roundOver && (playerHealth == null || !playerHealth.IsDead);
            if (player != null) player.InputEnabled = enabled;
            if (weapon != null) weapon.InputEnabled = enabled;
        }

        void TickPlaying(float dt)
        {
            Elapsed += dt;

            if (roundRestartAt >= 0f && Elapsed >= roundRestartAt)
            {
                roundRestartAt = -1f;
                StartRound();
            }

            for (int i = respawnQueue.Count - 1; i >= 0; i--)
            {
                if (respawnQueue[i] > Elapsed) continue;
                respawnQueue.RemoveAt(i);
                if (!roundOver) SpawnOne();
            }

            if (ActiveDuration > 0 && Elapsed >= ActiveDuration) EndSession();
        }

        void StartRound()
        {
            ClearBots();
            roundOver = false;
            roundRestartAt = -1f;
            roundStartTime = Elapsed;

            if (playerHealth != null) playerHealth.ResetHealth();
            Transform spawn = CurrentSpawn();
            if (player != null && spawn != null) player.Teleport(spawn.position, spawn.eulerAngles.y);
            if (weapon != null) weapon.ResetWeapon();
            RefreshInput();

            if (ActiveMode == TrainingMode.Prefire)
            {
                int spawned = 0;
                if (ActiveRoute != null)
                {
                    foreach (BotSpawnPoint point in ActiveRoute.Points)
                    {
                        if (point == null || occupiedPoints.ContainsValue(point)) continue;
                        SpawnAtPoint(point, point.CanShoot);
                        spawned++;
                    }
                }
                else
                {
                    foreach (BotSpawnPoint point in spawnPoints)
                    {
                        if (point == null || !point.UsedInPrefire) continue;
                        SpawnAtPoint(point, point.CanShoot);
                        spawned++;
                    }
                }
                if (spawned > 0) return;
                ShowMessage("这张地图没有预瞄点位，改为随机刷新。", 3f);
            }
            else if (ActiveMode == TrainingMode.Hold && ActiveHold == null)
            {
                ShowMessage("这张地图没有架枪位置，改为对枪。架枪训练请选“亚海悬城（整图）”。", 4f);
            }

            int count = activeBotCount;
            // 架枪时同时冲出来的假人不超过入口数量。
            if (ActiveHold != null) count = Mathf.Clamp(count, 1, Mathf.Max(1, ActiveHold.Entries.Length));
            for (int i = 0; i < count; i++) SpawnOne();
        }

        // ---------- 刷新假人 ----------

        void SpawnOne()
        {
            switch (ActiveMode)
            {
                case TrainingMode.Flick:
                    if (!SpawnInView(BotBehavior.Static, false)) RetrySpawnLater();
                    break;
                case TrainingMode.Strafe:
                    if (!SpawnInView(BotBehavior.Strafe, false)) RetrySpawnLater();
                    break;
                case TrainingMode.Hold when ActiveHold != null:
                    BotSpawnPoint entry = PickFreeHoldEntry();
                    if (entry != null) SpawnAtPoint(entry, true);
                    else RetrySpawnLater();
                    break;
                default:
                    BotSpawnPoint point = PickFreePoint();
                    if (point != null) SpawnAtPoint(point, point.CanShoot);
                    else if (!SpawnInView(BotBehavior.Strafe, true)) RetrySpawnLater();
                    break;
            }
        }

        /// <summary>这次没找到合适的位置（例如被其他假人占满）：0.5 秒后再试，不会少一个假人。</summary>
        void RetrySpawnLater()
        {
            if (!roundOver) respawnQueue.Add(Elapsed + 0.5f);
        }

        BotSpawnPoint PickFreePoint()
        {
            var candidates = new List<BotSpawnPoint>();
            foreach (BotSpawnPoint point in spawnPoints)
            {
                if (point == null || occupiedPoints.ContainsValue(point)) continue;
                if (player != null && Vector3.Distance(point.transform.position, player.transform.position) < MinSpawnDistance)
                    continue;
                candidates.Add(point);
            }
            return candidates.Count == 0 ? null : candidates[rng.Next(candidates.Count)];
        }

        /// <summary>架枪模式：随机挑一个没有假人的入口。</summary>
        BotSpawnPoint PickFreeHoldEntry()
        {
            var candidates = new List<BotSpawnPoint>();
            foreach (BotSpawnPoint entry in ActiveHold.Entries)
            {
                if (entry != null && !occupiedPoints.ContainsValue(entry)) candidates.Add(entry);
            }
            return candidates.Count == 0 ? null : candidates[rng.Next(candidates.Count)];
        }

        void SpawnAtPoint(BotSpawnPoint point, bool canShoot)
        {
            BotAgent bot = BotFactory.Create(point.transform.position, point.transform.eulerAngles.y, CurrentSkill(), player,
                point.Behavior, canShoot, point, NextSeed());
            occupiedPoints[bot] = point;
            Register(bot);
        }

        /// <summary>
        /// 在随机刷新区域里找一个你看得见的位置放假人。找不到时逐步放宽条件：
        /// 先不要求在视野里，再不要求没有遮挡、也不限最远距离。成功放下假人时返回 true。
        /// </summary>
        bool SpawnInView(BotBehavior behavior, bool canShoot)
        {
            if (player == null) return false;
            Vector3 feet = default;
            bool found = false;
            for (int pass = 0; pass < 3 && !found; pass++)
            {
                for (int attempt = 0; attempt < 30 && !found; attempt++)
                {
                    if (!TrySampleSpawn(out Vector3 candidate)) break;
                    if (!IsGoodSpawn(candidate, requireInView: pass == 0, requireLineOfSight: pass <= 1,
                            enforceMaxDistance: pass <= 1))
                    {
                        continue;
                    }
                    feet = candidate;
                    found = true;
                }
            }

            if (!found)
            {
                // 地图里既没有刷新区域也没有点位：放在玩家前方 12 米。
                if (spawnAreas.Length > 0 || spawnPoints.Length > 0) return false;
                feet = player.transform.position + player.transform.forward * 12f;
            }

            Vector3 toPlayer = player.transform.position - feet;
            float yaw = Mathf.Atan2(toPlayer.x, toPlayer.z) * Mathf.Rad2Deg;
            BotAgent bot = BotFactory.Create(feet, yaw, CurrentSkill(), player, behavior, canShoot, null, NextSeed());
            Register(bot);
            return true;
        }

        bool TrySampleSpawn(out Vector3 feet)
        {
            if (spawnAreas.Length > 0)
            {
                BotSpawnArea area = spawnAreas[rng.Next(spawnAreas.Length)];
                if (area != null && area.TrySample(rng, out feet)) return true;
            }
            if (spawnPoints.Length > 0)
            {
                BotSpawnPoint point = spawnPoints[rng.Next(spawnPoints.Length)];
                if (point != null && !occupiedPoints.ContainsValue(point))
                {
                    feet = point.transform.position;
                    return true;
                }
            }
            feet = default;
            return false;
        }

        bool IsGoodSpawn(Vector3 feet, bool requireInView, bool requireLineOfSight, bool enforceMaxDistance)
        {
            Vector3 eye = player.EyePosition;
            Vector3 head = feet + Vector3.up * BotFactory.HeadCenterHeight;
            Vector3 toHead = head - eye;
            float flatDistance = new Vector2(toHead.x, toHead.z).magnitude;
            if (flatDistance < MinSpawnDistance || (enforceMaxDistance && flatDistance > MaxSpawnDistance)) return false;

            foreach (BotAgent bot in bots)
            {
                if (bot != null && !bot.IsDead && (bot.transform.position - feet).sqrMagnitude < 2.5f * 2.5f) return false;
            }

            if (requireInView)
            {
                float yawToHead = Mathf.Atan2(toHead.x, toHead.z) * Mathf.Rad2Deg;
                float delta = Mathf.Abs(Mathf.DeltaAngle(player.Yaw, yawToHead));
                if (delta > player.HorizontalFov * 0.5f + 10f) return false;
            }

            if (!requireLineOfSight) return true;
            float distance = toHead.magnitude;
            int count = Physics.RaycastNonAlloc(eye, toHead / distance, rayBuffer, distance - 0.2f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (!rayBuffer[i].collider.transform.IsChildOf(player.transform)) return false;
            }
            return true;
        }

        void Register(BotAgent bot)
        {
            bots.Add(bot);
            bot.FireEnabled = CurrentPhase == Phase.Playing && !roundOver;
            bot.Health.Died += (h, info, result) => OnBotKilled(bot, result);
            bot.Removed += OnBotRemoved;
        }

        void ClearBots()
        {
            foreach (BotAgent bot in bots)
            {
                if (bot != null) Destroy(bot.gameObject);
            }
            bots.Clear();
            occupiedPoints.Clear();
            respawnQueue.Clear();
        }

        void SetBotsFireEnabled(bool enabled)
        {
            foreach (BotAgent bot in bots)
            {
                if (bot != null) bot.FireEnabled = enabled;
            }
        }

        BotSkill CurrentSkill() => BotSkill.For(activeDifficulty);

        int NextSeed() => unchecked(rng.Next() ^ ++spawnSerial * 7919);

        // ---------- 事件 ----------

        void OnShotFired(ShotReport report)
        {
            if (CurrentPhase != Phase.Playing) return;
            stats.RecordShot(report.HitTarget, report.HitTarget && report.Zone == HitZone.Head, report.Damage.Dealt);
        }

        void OnBotKilled(BotAgent bot, DamageResult result)
        {
            if (CurrentPhase != Phase.Playing) return;
            bool lineOfSightMode = TrainingModeInfo.BotsShoot(ActiveMode);
            // 对枪和预瞄从假人第一次看到你开始计时；假人没看到你就被击杀时不计入平均耗时（记为 -1）。
            float timeToKill = lineOfSightMode
                ? (bot.FirstSeenPlayerTime >= 0f ? Time.time - bot.FirstSeenPlayerTime : -1f)
                : Time.time - bot.SpawnTime;
            bool oneTap = result.Zone == HitZone.Head && result.Dealt >= bot.Health.Max - 0.01f;
            stats.RecordKill(timeToKill, oneTap);
            if (TrainingModeInfo.TracksAngles(ActiveMode)) angleStats.RecordKill(bot.Label, timeToKill);
            occupiedPoints.Remove(bot);

            switch (ActiveMode)
            {
                case TrainingMode.Flick:
                    respawnQueue.Add(Elapsed + FlickRespawnDelay);
                    break;
                case TrainingMode.Strafe:
                    respawnQueue.Add(Elapsed + StrafeRespawnDelay);
                    break;
                case TrainingMode.Duel:
                    respawnQueue.Add(Elapsed + DuelRespawnDelay);
                    break;
                case TrainingMode.Hold:
                    float delay = ActiveHold != null
                        ? HoldRespawnMin + (float)rng.NextDouble() * (HoldRespawnMax - HoldRespawnMin)
                        : DuelRespawnDelay;
                    respawnQueue.Add(Elapsed + delay);
                    break;
                case TrainingMode.Prefire:
                    if (AliveBots == 0 && !roundOver) FinishRound(cleared: true);
                    break;
            }
        }

        void OnBotRemoved(BotAgent bot)
        {
            bots.Remove(bot);
            occupiedPoints.Remove(bot);
        }

        void OnPlayerDamaged(Health health, DamageInfo info, DamageResult result)
        {
            LastDamageTime = Time.unscaledTime;
            if (playerAudio != null) playerAudio.PlayHurt();
        }

        void OnPlayerDied(Health health, DamageInfo info, DamageResult result)
        {
            if (CurrentPhase != Phase.Playing) return;
            stats.RecordDeath();
            if (TrainingModeInfo.TracksAngles(ActiveMode) && info.Source != null)
            {
                BotAgent killer = info.Source.GetComponent<BotAgent>();
                if (killer != null) angleStats.RecordDeath(killer.Label);
            }
            FinishRound(cleared: false);
        }

        void FinishRound(bool cleared)
        {
            roundOver = true;
            SetBotsFireEnabled(false);
            RefreshInput();
            respawnQueue.Clear();

            if (cleared)
            {
                float time = Elapsed - roundStartTime;
                stats.RecordRoundCleared(time);
                ShowMessage($"清点完成  {time:0.00} 秒", RoundRestartDelay);
            }
            else
            {
                ShowMessage("你被击杀了", RoundRestartDelay);
            }
            roundRestartAt = Elapsed + RoundRestartDelay;
        }

        void ShowMessage(string text, float seconds)
        {
            message = text;
            messageUntil = Time.unscaledTime + seconds;
        }

        string RecordKey()
        {
            string key = BestRecords.Key(SceneManager.GetActiveScene().name, ActiveMode, activeDifficulty, activeBotCount,
                ActiveDuration) + "." + activeWeaponId;
            // 不同路线 / 架枪位置分开记录（不选路线时和以前的记录保持一致）。
            return string.IsNullOrEmpty(ActiveVariant) ? key : key + "." + ActiveVariant;
        }

        T FindInScene<T>() where T : Component
        {
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
        }

        T[] FindAllInScene<T>() where T : Component
        {
            var list = new List<T>();
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                list.AddRange(root.GetComponentsInChildren<T>(true));
            }
            return list.ToArray();
        }
    }
}
