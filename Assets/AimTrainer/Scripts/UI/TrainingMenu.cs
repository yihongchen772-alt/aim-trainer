using System.Collections.Generic;
using System.Globalization;
using AimTrainer.Bots;
using AimTrainer.Game;
using AimTrainer.Movement;
using AimTrainer.Player;
using AimTrainer.Weapons;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;

namespace AimTrainer.UI
{
    /// <summary>
    /// 训练菜单：开始前、暂停时（Esc）和结算时显示。左边选地图和训练选项，
    /// 右边分“设置”（灵敏度、准星、画面）、“键位”和“记录”三页。设置会立即生效并保存在本机。
    /// </summary>
    public sealed class TrainingMenu : MonoBehaviour
    {
        const float PanelWidth = 1040f;
        const float PanelHeight = 720f;
        const float ColumnWidth = 486f;

        static readonly Color[] CrosshairColors =
        {
            new Color(0f, 1f, 0.6f), new Color(0f, 1f, 1f), new Color(1f, 1f, 0.2f),
            new Color(1f, 0.25f, 0.3f), new Color(1f, 1f, 1f), new Color(1f, 0.4f, 1f),
        };

        static readonly int[] BotCounts = { 1, 2, 3, 5 };
        static readonly int[] Durations = { 30, 60, 120, 0 };
        static readonly int[] FrameCaps = { 0, 60, 144, 240, 360 };
        static readonly string[] RightTabs = { "设置", "键位", "记录" };

        static readonly TrainingMode[] Modes =
            { TrainingMode.Flick, TrainingMode.Strafe, TrainingMode.Duel, TrainingMode.Prefire, TrainingMode.Hold };

        [SerializeField] TrainingSession session;

        string sensitivityText;
        string dpiText;
        bool optionsChangedWhilePaused;
        Vector2 scroll;
        Vector2 areaOrigin;
        Rect scrollRect;
        Vector2 previewCenter;
        int rightTab;

        /// <summary>正在等待新按键的动作序号，-1 表示没有在改键位。</summary>
        int capturingKey = -1;

        string keyMessage = string.Empty;

        public void Bind(TrainingSession training) => session = training;

        void Start()
        {
            if (session == null) session = GetComponent<TrainingSession>();
        }

        void OnDisable() => TrainingSession.SuppressEscape = false;

        /// <summary>改键位：点了某个动作的按钮后，等待键盘上按下的下一个键。</summary>
        void Update()
        {
            if (session == null || session.CurrentPhase == TrainingSession.Phase.Playing) capturingKey = -1;
            TrainingSession.SuppressEscape = capturingKey >= 0;
            if (capturingKey < 0) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                capturingKey = -1;
                keyMessage = "已取消。";
                return;
            }
            foreach (KeyControl control in keyboard.allKeys)
            {
                if (control == null || !control.wasPressedThisFrame) continue;
                Key key = control.keyCode;
                if (KeyBindings.IsReserved(key)) continue;
                KeyBindings keys = session.Settings.keys;
                int swapped = keys.Assign(capturingKey, key);
                keyMessage = swapped >= 0
                    ? $"{KeyBindings.NameOf(capturingKey)} 改为 {KeyBindings.DisplayName(key)}；{KeyBindings.NameOf(swapped)} 原来用这个键，已经互换为 {KeyBindings.DisplayName(keys.Get(swapped))}。"
                    : $"{KeyBindings.NameOf(capturingKey)} 改为 {KeyBindings.DisplayName(key)}。";
                capturingKey = -1;
                session.Settings.Save();
                session.ApplySettings();
                break;
            }
        }

        void OnGUI()
        {
            if (session == null || session.CurrentPhase == TrainingSession.Phase.Playing) return;

            float scale = UiTheme.Scale;
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = Screen.height / scale;

            UiTheme.Fill(new Rect(0f, 0f, width, height), new Color(0f, 0f, 0f, 0.55f));
            float panelW = Mathf.Min(PanelWidth, width - 32f);
            float panelH = Mathf.Min(PanelHeight + (session.CurrentPhase == TrainingSession.Phase.Results ? 130f : 0f), height - 32f);
            var panel = new Rect((width - panelW) * 0.5f, (height - panelH) * 0.5f, panelW, panelH);
            GUI.DrawTexture(panel, UiTheme.PanelTex);
            UiTheme.Fill(new Rect(panel.x, panel.y, 56f, 3f), UiTheme.Accent);

            areaOrigin = new Vector2(panel.x + 24f, panel.y + 18f);
            GUILayout.BeginArea(new Rect(areaOrigin.x, areaOrigin.y, panel.width - 48f, panel.height - 36f));
            DrawHeader();
            // 窗口太小时中间部分可以滚动，底部的按钮始终可见。
            scroll = GUILayout.BeginScrollView(scroll, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.ExpandHeight(true));
            if (session.CurrentPhase == TrainingSession.Phase.Results) DrawResults();
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(ColumnWidth));
            DrawTrainingOptions();
            GUILayout.EndVertical();
            GUILayout.Space(20f);
            GUILayout.BeginVertical(GUILayout.Width(ColumnWidth));
            DrawRightColumn();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
            if (Event.current.type == EventType.Repaint) scrollRect = GUILayoutUtility.GetLastRect();
            DrawFooter();
            GUILayout.EndArea();

            // 准星预览按实际像素绘制（和游戏里看到的大小一样），所以放在缩放矩阵之外画。
            if (Event.current.type == EventType.Repaint && rightTab == 0)
            {
                GUI.matrix = Matrix4x4.identity;
                Vector2 center = (areaOrigin + scrollRect.position + previewCenter - scroll) * scale;
                var clip = new Rect((areaOrigin + scrollRect.position) * scale, scrollRect.size * scale);
                if (clip.Contains(center))
                {
                    // 只画在可见的滚动区域内，滚动时准星不会盖到标题或底部按钮上。
                    GUI.BeginClip(clip);
                    CrosshairDrawer.Draw(center - clip.position, session.Settings);
                    GUI.EndClip();
                }
            }

            GUI.matrix = previous;
        }

        void DrawHeader()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("战术练枪场", UiTheme.Title);
            GUILayout.FlexibleSpace();
            string phase = session.CurrentPhase == TrainingSession.Phase.Paused ? "已暂停"
                : session.CurrentPhase == TrainingSession.Phase.Results ? "本局结束" : "准备开始";
            GUILayout.Label($"{session.SceneTitle} · {phase}", UiTheme.Heading);
            GUILayout.EndHorizontal();
            GUILayout.Space(6f);
        }

        void DrawResults()
        {
            SessionStats s = session.Stats;
            bool prefire = session.ActiveMode == TrainingMode.Prefire;
            bool shooting = TrainingModeInfo.BotsShoot(session.ActiveMode);

            GUILayout.Label("本局成绩", UiTheme.Heading);
            GUILayout.BeginHorizontal();
            Stat(prefire ? "最快清点" : "得分", prefire ? (s.RoundsCleared > 0 ? $"{s.BestRoundTime:0.00}s" : "—") : s.Score.ToString());
            Stat("击杀", s.Kills.ToString());
            Stat("命中率", $"{s.Accuracy * 100f:0}%");
            Stat("爆头率", $"{s.HeadshotRate * 100f:0}%");
            Stat("平均击杀", s.AverageKillTime > 0f ? $"{s.AverageKillTime * 1000f:0}ms" : "—");
            Stat("一枪爆头", s.OneTapKills.ToString());
            if (shooting) Stat("死亡", s.Deaths.ToString());
            GUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(session.ActiveVariant))
            {
                string kind = session.ActiveMode == TrainingMode.Hold ? "架枪位置" : "路线";
                GUILayout.Label($"{kind}：{session.ActiveVariant}", UiTheme.MutedText);
            }

            string record;
            if (session.NewRecord) record = "新纪录！这是当前地图、模式和设置下的最好成绩。";
            else if (session.PreviousBest != null)
                record = prefire
                    ? $"当前设置下的最快清点：{session.PreviousBest.bestRoundTime:0.00} 秒"
                    : $"当前设置下的最高得分：{session.PreviousBest.score}";
            else record = "完成一局后会记录这个设置下的最好成绩。";
            GUILayout.Label(record, session.NewRecord ? Colored(UiTheme.Good) : UiTheme.MutedText);
            DrawAngleTable();
            GUILayout.Space(10f);
        }

        /// <summary>各点位的成绩：最常把你打死、平均耗时最长的点位排在前面。</summary>
        void DrawAngleTable()
        {
            if (!TrainingModeInfo.TracksAngles(session.ActiveMode) || session.Angles.Count == 0) return;
            GUILayout.Space(8f);
            GUILayout.Label("各点位成绩（最需要练的在前；耗时 = 和假人对上视线到击杀）", UiTheme.Heading);
            List<AngleStats.Entry> rows = session.Angles.Sorted();
            AngleRow("点位", "击杀", "平均耗时", "最慢", "被它击杀", UiTheme.MutedText);
            for (int i = 0; i < rows.Count && i < 10; i++)
            {
                AngleStats.Entry e = rows[i];
                GUIStyle style = e.Deaths > 0 ? Colored(UiTheme.Bad) : UiTheme.Body;
                AngleRow(e.Label, e.Kills.ToString(), e.TimedKills > 0 ? $"{e.AverageTime * 1000f:0} ms" : "—",
                    e.TimedKills > 0 ? $"{e.SlowestTime * 1000f:0} ms" : "—", e.Deaths.ToString(), style);
            }
            if (rows.Count > 10) GUILayout.Label($"另有 {rows.Count - 10} 个点位未显示。", UiTheme.MutedText);
        }

        static void AngleRow(string label, string kills, string average, string slowest, string deaths, GUIStyle style)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, style, GUILayout.Width(220f));
            GUILayout.Label(kills, style, GUILayout.Width(70f));
            GUILayout.Label(average, style, GUILayout.Width(110f));
            GUILayout.Label(slowest, style, GUILayout.Width(110f));
            GUILayout.Label(deaths, style, GUILayout.Width(90f));
            GUILayout.EndHorizontal();
        }

        void Stat(string label, string value)
        {
            GUILayout.BeginVertical(GUILayout.Width(132f));
            GUILayout.Label(value, UiTheme.StatValue);
            GUILayout.Label(label, UiTheme.MutedText);
            GUILayout.EndVertical();
        }

        void DrawTrainingOptions()
        {
            GameSettings s = session.Settings;
            string activeScene = SceneManager.GetActiveScene().name;

            Section("地图");
            // 每行 3 个地图按钮。
            for (int row = 0; row < SceneCatalog.Training.Length; row += 3)
            {
                GUILayout.BeginHorizontal();
                for (int i = row; i < row + 3 && i < SceneCatalog.Training.Length; i++)
                {
                    SceneCatalog.Entry entry = SceneCatalog.Training[i];
                    // 场景还没生成（例如真实地图的模型还没导入）时按钮变灰，点了也不会切换。
                    bool available = entry.SceneName == activeScene || Application.CanStreamedLevelBeLoaded(entry.SceneName);
                    GUI.enabled = available;
                    if (Choice(entry.Title, entry.SceneName == activeScene, 154f) && entry.SceneName != activeScene && available)
                    {
                        session.LoadScene(entry.SceneName);
                    }
                    GUI.enabled = true;
                }
                GUILayout.EndHorizontal();
            }
            foreach (SceneCatalog.Entry entry in SceneCatalog.Training)
            {
                if (entry.SceneName == activeScene) GUILayout.Label(entry.Description, UiTheme.MutedText);
            }
            // 每帧都画这一行（内容可能为空），布局在不同 IMGUI 事件之间保持一致。
            GUILayout.Label(Application.CanStreamedLevelBeLoaded(SceneCatalog.AscentRealScene) ? string.Empty
                : "灰色的地图还没生成：真实地图要等 glTFast 和地图模型导入完，然后点 AimTrainer → 生成缺少的场景。", UiTheme.MutedText);

            Section("武器");
            GUILayout.BeginHorizontal();
            for (int i = 0; i < WeaponPresets.All.Length; i++)
            {
                WeaponPresets.Entry entry = WeaponPresets.All[i];
                string label = entry.Title.Split(' ')[0];
                if (Choice(label, s.weaponId == entry.Id, 88f) && s.weaponId != entry.Id)
                {
                    s.weaponId = entry.Id;
                    OptionChanged();
                }
            }
            GUILayout.EndHorizontal();
            foreach (WeaponPresets.Entry entry in WeaponPresets.All)
            {
                if (entry.Id == s.weaponId) GUILayout.Label(entry.Title + "：" + entry.Note, UiTheme.MutedText);
            }

            Section("模式");
            GUILayout.BeginHorizontal();
            foreach (TrainingMode mode in Modes)
            {
                if (Choice(TrainingModeInfo.Title(mode), s.mode == mode, 92f) && s.mode != mode)
                {
                    s.mode = mode;
                    OptionChanged();
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.Label(TrainingModeInfo.Description(s.mode), UiTheme.MutedText);
            if (s.mode == TrainingMode.Prefire) DrawRouteChoice(s);
            else if (s.mode == TrainingMode.Hold) DrawHoldChoice(s);

            Section("假人难度（对枪 / 预瞄的反应和枪法，移动靶的横跳速度）");
            GUILayout.BeginHorizontal();
            string[] names = { "简单", "普通", "困难", "职业" };
            for (int i = 0; i < names.Length; i++)
            {
                var level = (BotDifficulty)i;
                if (Choice(names[i], s.difficulty == level, 112f) && s.difficulty != level)
                {
                    s.difficulty = level;
                    OptionChanged();
                }
            }
            GUILayout.EndHorizontal();
            BotSkill skill = BotSkill.For(s.difficulty);
            GUILayout.Label($"反应 {skill.reactionTime * 1000f:0} ms · 命中率 {skill.hitChance * 100f:0}% · 爆头率 {skill.headshotChance * 100f:0}% · 移动 {skill.moveSpeed:0.0} m/s",
                UiTheme.MutedText);

            Section(s.mode == TrainingMode.Prefire ? "同时在场的假人（预瞄模式使用路线上的所有点位，不看这个数量）"
                : s.mode == TrainingMode.Hold ? "同时冲出来的假人（不超过入口数量）" : "同时在场的假人");
            GUILayout.BeginHorizontal();
            foreach (int count in BotCounts)
            {
                if (Choice(count.ToString(), s.botCount == count, 60f) && s.botCount != count)
                {
                    s.botCount = count;
                    OptionChanged();
                }
            }
            GUILayout.EndHorizontal();

            Section("时长");
            GUILayout.BeginHorizontal();
            foreach (int seconds in Durations)
            {
                string label = seconds == 0 ? "不限时" : seconds >= 60 ? $"{seconds / 60} 分钟" : $"{seconds} 秒";
                if (Choice(label, s.durationSeconds == seconds, 90f) && s.durationSeconds != seconds)
                {
                    s.durationSeconds = seconds;
                    OptionChanged();
                }
            }
            GUILayout.EndHorizontal();

            if (optionsChangedWhilePaused && session.CurrentPhase == TrainingSession.Phase.Paused)
            {
                GUILayout.Label("训练选项已修改，点“重新开始”后生效。", Colored(UiTheme.Warn));
            }
        }

        void DrawRouteChoice(GameSettings s)
        {
            IReadOnlyList<PrefireRoute> routes = session.Routes;
            if (routes.Count == 0) return;
            Section("预瞄路线");
            PrefireRoute selected = session.FindRoute(s.prefireRoute);
            var labels = new List<string> { "全部点位" };
            foreach (PrefireRoute route in routes) labels.Add(route.Label);
            for (int row = 0; row < labels.Count; row += 3)
            {
                GUILayout.BeginHorizontal();
                for (int i = row; i < row + 3 && i < labels.Count; i++)
                {
                    bool on = i == 0 ? selected == null : selected == routes[i - 1];
                    if (Choice(labels[i], on, 154f) && !on)
                    {
                        s.prefireRoute = i == 0 ? string.Empty : labels[i];
                        OptionChanged();
                    }
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.Label(selected == null ? "地图上所有预瞄点位同时放假人，从出生点出发。"
                : $"从路线起点出发，沿途 {selected.Points.Count} 个点位。", UiTheme.MutedText);
        }

        void DrawHoldChoice(GameSettings s)
        {
            Section("架枪位置");
            IReadOnlyList<HoldSpot> holds = session.Holds;
            if (holds.Count == 0)
            {
                GUILayout.Label("这张地图没有架枪位置（会改为对枪）。架枪训练请选“亚海悬城（整图）”。", Colored(UiTheme.Warn));
                return;
            }
            HoldSpot selected = session.FindHold(s.holdSpot);
            for (int row = 0; row < holds.Count; row += 3)
            {
                GUILayout.BeginHorizontal();
                for (int i = row; i < row + 3 && i < holds.Count; i++)
                {
                    bool on = holds[i] == selected;
                    if (Choice(holds[i].Label, on, 154f) && !on)
                    {
                        s.holdSpot = holds[i].Label;
                        OptionChanged();
                    }
                }
                GUILayout.EndHorizontal();
            }
            if (selected != null)
            {
                var names = new List<string>();
                foreach (BotSpawnPoint entry in selected.Entries) names.Add(entry.Label);
                GUILayout.Label("入口：" + string.Join("、", names), UiTheme.MutedText);
            }
        }

        void DrawRightColumn()
        {
            GUILayout.BeginHorizontal();
            for (int i = 0; i < RightTabs.Length; i++)
            {
                if (Choice(RightTabs[i], rightTab == i, 100f) && rightTab != i)
                {
                    rightTab = i;
                    capturingKey = -1;
                    // 换页后控件数量不同：结束这次 IMGUI 事件，下一帧按新页面重新布局。
                    GUIUtility.ExitGUI();
                }
            }
            GUILayout.EndHorizontal();
            switch (rightTab)
            {
                case 1:
                    DrawKeyBindings();
                    break;
                case 2:
                    DrawHistory();
                    break;
                default:
                    DrawPreferences();
                    break;
            }
        }

        void DrawKeyBindings()
        {
            KeyBindings keys = session.Settings.keys;
            Section("键位（点按钮，再按键盘上的新按键；Esc 取消）");
            for (int i = 0; i < KeyBindings.Count; i++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(KeyBindings.NameOf(i), UiTheme.Body, GUILayout.Width(170f));
                bool waiting = capturingKey == i;
                string text = waiting ? "请按新按键…" : KeyBindings.DisplayName(keys.Get(i));
                if (GUILayout.Button(text, waiting ? UiTheme.ButtonOn : UiTheme.Button, GUILayout.Width(170f), GUILayout.Height(32f)))
                {
                    capturingKey = waiting ? -1 : i;
                    keyMessage = string.Empty;
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(6f);
            GUILayout.Label(keyMessage, UiTheme.MutedText);
            GUILayout.Label("一个键只能对应一个动作，改成已被占用的键时两个动作会互换。Esc（暂停）、F1 / F2（调试面板）、鼠标左键（射击）和滚轮（跳跃）不能改。",
                UiTheme.MutedText);
            if (GUILayout.Button("恢复默认键位", UiTheme.Button, GUILayout.Width(170f), GUILayout.Height(34f)))
            {
                keys.ResetToDefaults();
                capturingKey = -1;
                keyMessage = "已恢复默认键位。";
                session.Settings.Save();
                session.ApplySettings();
            }
        }

        void DrawHistory()
        {
            IReadOnlyList<HistoryEntry> entries = SessionHistory.Entries;
            Section($"最近的训练（最多保存 {SessionHistory.Capacity} 局）");
            if (entries.Count == 0)
            {
                GUILayout.Label("还没有训练记录。一局结束（或在暂停菜单点“结束本局”）后会记录在这里。", UiTheme.MutedText);
                return;
            }
            for (int i = 0; i < entries.Count && i < 12; i++)
            {
                HistoryEntry e = entries[i];
                string variant = string.IsNullOrEmpty(e.variant) ? string.Empty : $"（{e.variant}）";
                GUILayout.Label($"{e.time}  {SceneCatalog.TitleOf(e.scene)} · {TrainingModeInfo.Title(e.mode)}{variant}", UiTheme.Body);
                string result = e.mode == TrainingMode.Prefire
                    ? (e.bestRoundTime > 0f ? $"最快清点 {e.bestRoundTime:0.00}s" : "未清点完成")
                    : $"得分 {e.score}";
                string kill = e.averageKillTime > 0f ? $" · 平均击杀 {e.averageKillTime * 1000f:0}ms" : string.Empty;
                GUILayout.Label($"{WeaponPresets.TitleOf(e.weapon)} · {result} · 击杀 {e.kills} · 命中率 {e.accuracy * 100f:0}% · 爆头率 {e.headshotRate * 100f:0}%{kill}",
                    UiTheme.MutedText);
                GUILayout.Space(4f);
            }
            if (GUILayout.Button("清空记录", UiTheme.Button, GUILayout.Width(120f), GUILayout.Height(32f)))
            {
                SessionHistory.Clear();
                GUIUtility.ExitGUI();
            }
        }

        /// <summary>按上一局的设置再开一局；地图不同时先切换地图，加载完自动开始。</summary>
        void ContinueLast(HistoryEntry last)
        {
            GameSettings s = session.Settings;
            s.mode = last.mode;
            s.difficulty = last.difficulty;
            s.botCount = last.botCount;
            s.durationSeconds = last.duration;
            s.weaponId = last.weapon;
            if (last.mode == TrainingMode.Prefire) s.prefireRoute = last.variant ?? string.Empty;
            else if (last.mode == TrainingMode.Hold) s.holdSpot = last.variant ?? string.Empty;
            s.Sanitize();
            s.Save();
            if (last.scene == SceneManager.GetActiveScene().name)
            {
                StartSession();
                return;
            }
            TrainingSession.RequestAutoStart();
            session.LoadScene(last.scene);
        }

        static string DescribeLast(HistoryEntry last)
        {
            string variant = string.IsNullOrEmpty(last.variant) ? string.Empty : $"（{last.variant}）";
            return $"上次：{SceneCatalog.TitleOf(last.scene)} · {TrainingModeInfo.Title(last.mode)}{variant} · {WeaponPresets.TitleOf(last.weapon)}";
        }

        void DrawPreferences()
        {
            GameSettings s = session.Settings;

            Section("灵敏度（填游戏内数值）");
            GUILayout.BeginHorizontal();
            sensitivityText ??= s.sensitivity.ToString("0.###", CultureInfo.InvariantCulture);
            string newText = GUILayout.TextField(sensitivityText, UiTheme.Field, GUILayout.Width(90f));
            if (newText != sensitivityText)
            {
                sensitivityText = newText;
                if (float.TryParse(newText, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) &&
                    parsed >= 0.01f && parsed <= 10f)
                {
                    s.sensitivity = parsed;
                    session.ApplySettings();
                }
            }
            // 只在拖动了滑条时才采用滑条的值；手动输入超过滑条范围（例如 3.5）时不会被滑条改掉。
            GUI.changed = false;
            float slider = GUILayout.HorizontalSlider(Mathf.Clamp(s.sensitivity, 0.05f, 2f), 0.05f, 2f, GUILayout.Width(220f));
            if (GUI.changed)
            {
                s.sensitivity = Mathf.Round(slider * 1000f) / 1000f;
                sensitivityText = s.sensitivity.ToString("0.###", CultureInfo.InvariantCulture);
                session.ApplySettings();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("鼠标 DPI", UiTheme.MutedText, GUILayout.Width(70f));
            dpiText ??= s.mouseDpi.ToString("0", CultureInfo.InvariantCulture);
            string newDpi = GUILayout.TextField(dpiText, UiTheme.Field, GUILayout.Width(80f));
            if (newDpi != dpiText)
            {
                dpiText = newDpi;
                if (float.TryParse(newDpi, NumberStyles.Float, CultureInfo.InvariantCulture, out float dpi) && dpi >= 100f)
                {
                    s.mouseDpi = dpi;
                    session.ApplySettings();
                }
            }
            GUILayout.Space(8f);
            if (Choice(s.invertY ? "反转 Y：开" : "反转 Y：关", s.invertY, 110f))
            {
                s.invertY = !s.invertY;
                session.ApplySettings();
            }
            GUILayout.EndHorizontal();
            GUILayout.Label($"转一圈 360° 需要 {ViewMath.CentimetersPer360(s.sensitivity, s.mouseDpi):0.0} cm（eDPI {s.sensitivity * s.mouseDpi:0}，每个计数 {ViewMath.DegreesPerCount(s.sensitivity):0.0000}°）",
                UiTheme.MutedText);

            Section("准星");
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(330f));
            GUILayout.BeginHorizontal();
            foreach (Color color in CrosshairColors)
            {
                Rect swatch = GUILayoutUtility.GetRect(34f, 26f, GUILayout.Width(34f), GUILayout.Height(26f));
                bool selected = ColorsClose(color, s.crosshairColor);
                UiTheme.Fill(swatch, selected ? Color.white : UiTheme.Line);
                UiTheme.Fill(new Rect(swatch.x + 3f, swatch.y + 3f, swatch.width - 6f, swatch.height - 6f), color);
                if (GUI.Button(swatch, GUIContent.none, GUIStyle.none)) s.crosshairColor = color;
                GUILayout.Space(4f);
            }
            GUILayout.EndHorizontal();
            s.crosshairLength = IntSlider("长度", s.crosshairLength, 0, 12);
            s.crosshairGap = IntSlider("间隙", s.crosshairGap, 0, 10);
            s.crosshairThickness = IntSlider("粗细", s.crosshairThickness, 1, 4);
            GUILayout.BeginHorizontal();
            if (Choice(s.crosshairOutline ? "外框：开" : "外框：关", s.crosshairOutline, 96f)) s.crosshairOutline = !s.crosshairOutline;
            if (Choice(s.crosshairDot ? "中心点：开" : "中心点：关", s.crosshairDot, 106f)) s.crosshairDot = !s.crosshairDot;
            GUILayout.EndHorizontal();
            if (Choice(s.showAccuracyIndicator ? "精准度指示：开" : "精准度指示：关", s.showAccuracyIndicator, 150f))
            {
                s.showAccuracyIndicator = !s.showAccuracyIndicator;
            }
            GUILayout.EndVertical();

            Rect preview = GUILayoutUtility.GetRect(120f, 120f, GUILayout.Width(120f), GUILayout.Height(120f));
            UiTheme.Fill(preview, new Color(0.45f, 0.5f, 0.56f));
            UiTheme.Fill(new Rect(preview.x, preview.y + preview.height * 0.6f, preview.width, preview.height * 0.4f),
                new Color(0.62f, 0.6f, 0.56f));
            if (Event.current.type == EventType.Repaint) previewCenter = preview.center;
            GUILayout.EndHorizontal();

            Section("画面与声音");
            GUILayout.BeginHorizontal();
            GUILayout.Label("音量", UiTheme.MutedText, GUILayout.Width(40f));
            GUI.changed = false;
            float volume = GUILayout.HorizontalSlider(s.volume, 0f, 1f, GUILayout.Width(160f));
            if (GUI.changed)
            {
                s.volume = volume;
                session.ApplySettings();
            }
            GUILayout.Space(16f);
            if (Choice(s.showDebugPanel ? "调试面板：开" : "调试面板：关", s.showDebugPanel, 128f))
            {
                s.showDebugPanel = !s.showDebugPanel;
                session.ApplySettings();
            }
            if (Choice(s.showMinimap ? "小地图：开" : "小地图：关", s.showMinimap, 112f)) s.showMinimap = !s.showMinimap;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("帧率上限", UiTheme.MutedText, GUILayout.Width(70f));
            foreach (int cap in FrameCaps)
            {
                if (Choice(cap == 0 ? "不限" : cap.ToString(), s.frameRateCap == cap, 70f) && s.frameRateCap != cap)
                {
                    s.frameRateCap = cap;
                    session.ApplySettings();
                }
            }
            GUILayout.EndHorizontal();
        }

        void DrawFooter()
        {
            KeyBindings k = session.Settings.keys;
            string Name(Key key) => KeyBindings.DisplayName(key);
            GUILayout.Label($"{Name(k.forward)}{Name(k.left)}{Name(k.back)}{Name(k.right)} 移动 · {Name(k.walk)} 静步 · {Name(k.crouch)}/{Name(k.crouchAlt)} 下蹲 · {Name(k.jump)}跳 · 左键射击 · {Name(k.reload)} 换弹 · Esc 暂停 · F1 调试面板",
                UiTheme.MutedText);
            HistoryEntry last = SessionHistory.Last;
            bool canContinue = last != null && Application.CanStreamedLevelBeLoaded(last.scene);
            // 这一行每个阶段都画（内容可能为空），避免布局在不同 IMGUI 事件之间不一致。
            GUILayout.Label(canContinue && session.CurrentPhase == TrainingSession.Phase.Menu ? DescribeLast(last) : string.Empty,
                UiTheme.MutedText);
#if UNITY_EDITOR
            // 这一行在每个 IMGUI 事件里都画（不随阶段变化），避免布局在不同事件之间不一致。
            GUILayout.Label("在编辑器里按 Esc 会释放鼠标，暂停后请点“继续”回到游戏（打包后的程序可以再按 Esc 继续）。", UiTheme.MutedText);
#endif
            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            switch (session.CurrentPhase)
            {
                case TrainingSession.Phase.Paused:
                    if (GUILayout.Button("继续", UiTheme.Primary)) session.Resume();
                    if (GUILayout.Button("重新开始", UiTheme.Button, GUILayout.Height(44f))) StartSession();
                    if (GUILayout.Button("结束本局", UiTheme.Button, GUILayout.Height(44f))) session.EndSession();
                    break;
                case TrainingSession.Phase.Results:
                    if (GUILayout.Button("再来一局", UiTheme.Primary)) StartSession();
                    break;
                default:
                    bool start = GUILayout.Button("开始训练", UiTheme.Primary);
                    bool resume = canContinue && GUILayout.Button("继续上次训练", UiTheme.Button, GUILayout.Height(44f));
                    if (start) StartSession();
                    else if (resume) ContinueLast(last);
                    break;
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("退出游戏", UiTheme.Button, GUILayout.Height(44f))) session.QuitGame();
            GUILayout.EndHorizontal();
        }

        void StartSession()
        {
            optionsChangedWhilePaused = false;
            session.StartSession();
        }

        /// <summary>
        /// 训练选项改了以后，菜单里显示的控件可能变多或变少（路线 / 架枪位置选项、“重新开始后生效”提示），
        /// 所以立刻结束这次 IMGUI 事件，下一帧重新布局，避免“Getting control N's position”错误。
        /// </summary>
        void OptionChanged()
        {
            if (session.CurrentPhase == TrainingSession.Phase.Paused) optionsChangedWhilePaused = true;
            GUIUtility.ExitGUI();
        }

        static void Section(string title)
        {
            GUILayout.Space(10f);
            GUILayout.Label(title, UiTheme.Heading);
        }

        static bool Choice(string label, bool selected, float width) =>
            GUILayout.Button(label, selected ? UiTheme.ButtonOn : UiTheme.Button, GUILayout.Width(width), GUILayout.Height(34f));

        static int IntSlider(string label, int value, int min, int max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{label} {value}", UiTheme.MutedText, GUILayout.Width(70f));
            int result = Mathf.RoundToInt(GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(220f)));
            GUILayout.EndHorizontal();
            return result;
        }

        static bool ColorsClose(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.05f;

        static GUIStyle Colored(Color color)
        {
            var style = new GUIStyle(UiTheme.Body);
            style.normal.textColor = color;
            return style;
        }
    }
}
