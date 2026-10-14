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
    /// - BotSpawnPoint（固定点位）：对枪模式随机挑点位，预瞄模式同时用上所有标记了“预瞄”的点位。
    /// 某种来源不存在时会退而使用另一种。开始时自动收集场景里所有的点位和区域。
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
        const float RoundRestartDelay = 1.5f;
        const float MinSpawnDistance = 6f;
        const float MaxSpawnDistance = 40f;

        [SerializeField] PlayerController player;
        [SerializeField] HitscanWeapon weapon;
        [SerializeField] Health playerHealth;
        [SerializeField] PlayerAudio playerAudio;
        [SerializeField] Transform playerSpawn;
        [SerializeField] PerformanceSettings performance;
        [SerializeField] MovementDebugHud debugHud;

        // 开始时自动收集场景里所有的点位和刷新区域，新加的点位不需要手动登记。
        BotSpawnPoint[] spawnPoints = new BotSpawnPoint[0];
        BotSpawnArea[] spawnAreas = new BotSpawnArea[0];

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

        public Phase CurrentPhase { get; private set; } = Phase.Menu;
        public SessionStats Stats => stats;
        public GameSettings Settings => GameSettings.Current;
        public PlayerController Player => player;
        public HitscanWeapon Weapon => weapon;
        public Health PlayerHealth => playerHealth;

        /// <summary>本局已进行的时间（秒，暂停时不走）。</summary>
        public float Elapsed { get; private set; }

        /// <summary>本局剩余时间（秒），不限时为 -1。</summary>
        public float Remaining => Settings.durationSeconds > 0 ? Mathf.Max(0f, Settings.durationSeconds - Elapsed) : -1f;

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
            spawnPoints = FindAllInScene<BotSpawnPoint>();
            spawnAreas = FindAllInScene<BotSpawnArea>();

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
            ApplySettings();
            if (player != null && playerSpawn != null) player.Teleport(playerSpawn.position, playerSpawn.eulerAngles.y);
            EnterMenu();
        }

        void OnDestroy()
        {
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
            Keyboard keyboard = Keyboard.current;
            bool escape = keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
            if (CurrentPhase == Phase.Playing)
            {
                // 按 Esc，或者鼠标因为别的原因被释放（编辑器里按 Esc 会直接释放鼠标）时暂停。
                if (escape || Cursor.lockState != CursorLockMode.Locked)
                {
                    Pause();
                    return;
                }
                TickPlaying(Time.deltaTime);
            }
            else if (CurrentPhase == Phase.Paused && escape)
            {
                Resume();
            }
        }

        // ---------- 流程 ----------

        /// <summary>按当前设置开始新的一局。</summary>
        public void StartSession()
        {
            Settings.Save();
            ApplySettings();
            stats.Reset();
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
            ApplySettings();
            SetPhase(Phase.Playing);
        }

        /// <summary>提前结束本局并显示成绩。</summary>
        public void EndSession()
        {
            if (CurrentPhase != Phase.Playing && CurrentPhase != Phase.Paused) return;
            bool roundBased = Settings.mode == TrainingMode.Prefire;
            string key = RecordKey();
            PreviousBest = BestRecords.Get(key);
            NewRecord = BestRecords.Submit(key, stats, roundBased);
            SetPhase(Phase.Results);
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
            if (player != null)
            {
                player.Sensitivity = s.sensitivity;
                player.InvertY = s.invertY;
            }
            AudioListener.volume = s.volume;
            if (performance != null) performance.SetFrameRateCap(s.frameRateCap);
            if (debugHud != null) debugHud.Visible = s.showDebugPanel;
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

            if (Settings.durationSeconds > 0 && Elapsed >= Settings.durationSeconds) EndSession();
        }

        void StartRound()
        {
            ClearBots();
            roundOver = false;
            roundRestartAt = -1f;
            roundStartTime = Elapsed;

            if (playerHealth != null) playerHealth.ResetHealth();
            if (player != null && playerSpawn != null) player.Teleport(playerSpawn.position, playerSpawn.eulerAngles.y);
            if (weapon != null) weapon.ResetWeapon();
            RefreshInput();

            if (Settings.mode == TrainingMode.Prefire)
            {
                int spawned = 0;
                foreach (BotSpawnPoint point in spawnPoints)
                {
                    if (point == null || !point.UsedInPrefire) continue;
                    SpawnAtPoint(point, point.CanShoot);
                    spawned++;
                }
                if (spawned > 0) return;
                ShowMessage("这张地图没有预瞄点位，改为随机刷新。", 3f);
            }

            for (int i = 0; i < Settings.botCount; i++) SpawnOne();
        }

        // ---------- 刷新假人 ----------

        void SpawnOne()
        {
            switch (Settings.mode)
            {
                case TrainingMode.Flick:
                    SpawnInView(BotBehavior.Static, false);
                    break;
                case TrainingMode.Strafe:
                    SpawnInView(BotBehavior.Strafe, false);
                    break;
                default:
                    BotSpawnPoint point = PickFreePoint();
                    if (point != null) SpawnAtPoint(point, point.CanShoot);
                    else SpawnInView(BotBehavior.Strafe, true);
                    break;
            }
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

        void SpawnAtPoint(BotSpawnPoint point, bool canShoot)
        {
            BotAgent bot = BotFactory.Create(point.transform.position, point.transform.eulerAngles.y, CurrentSkill(), player,
                point.Behavior, canShoot, point, NextSeed());
            occupiedPoints[bot] = point;
            Register(bot);
        }

        /// <summary>在随机刷新区域里找一个你看得见的位置放假人；找不到合适位置时逐步放宽条件。</summary>
        void SpawnInView(BotBehavior behavior, bool canShoot)
        {
            if (player == null) return;
            Vector3 feet = default;
            bool found = false;
            for (int pass = 0; pass < 3 && !found; pass++)
            {
                for (int attempt = 0; attempt < 30 && !found; attempt++)
                {
                    if (!TrySampleSpawn(out Vector3 candidate)) break;
                    if (!IsGoodSpawn(candidate, requireInView: pass == 0, requireLineOfSight: pass <= 1)) continue;
                    feet = candidate;
                    found = true;
                }
            }

            if (!found)
            {
                // 地图里既没有刷新区域也没有点位：放在玩家前方 12 米。
                if (spawnAreas.Length > 0 || spawnPoints.Length > 0) return;
                feet = player.transform.position + player.transform.forward * 12f;
            }

            Vector3 toPlayer = player.transform.position - feet;
            float yaw = Mathf.Atan2(toPlayer.x, toPlayer.z) * Mathf.Rad2Deg;
            BotAgent bot = BotFactory.Create(feet, yaw, CurrentSkill(), player, behavior, canShoot, null, NextSeed());
            Register(bot);
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

        bool IsGoodSpawn(Vector3 feet, bool requireInView, bool requireLineOfSight)
        {
            Vector3 eye = player.EyePosition;
            Vector3 head = feet + Vector3.up * BotFactory.HeadCenterHeight;
            Vector3 toHead = head - eye;
            float flatDistance = new Vector2(toHead.x, toHead.z).magnitude;
            if (flatDistance < MinSpawnDistance || flatDistance > MaxSpawnDistance) return false;

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

        BotSkill CurrentSkill() => BotSkill.For(Settings.difficulty);

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
            bool lineOfSightMode = Settings.mode == TrainingMode.Duel || Settings.mode == TrainingMode.Prefire;
            float start = lineOfSightMode && bot.FirstSeenPlayerTime >= 0f ? bot.FirstSeenPlayerTime : bot.SpawnTime;
            bool oneTap = result.Zone == HitZone.Head && result.Dealt >= bot.Health.Max - 0.01f;
            stats.RecordKill(Time.time - start, oneTap);
            occupiedPoints.Remove(bot);

            switch (Settings.mode)
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

        string RecordKey() => BestRecords.Key(SceneManager.GetActiveScene().name, Settings.mode, Settings.difficulty,
            Settings.botCount, Settings.durationSeconds);

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
