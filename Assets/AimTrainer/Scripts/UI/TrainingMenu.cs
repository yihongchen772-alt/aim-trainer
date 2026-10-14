using System.Globalization;
using AimTrainer.Bots;
using AimTrainer.Game;
using AimTrainer.Movement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AimTrainer.UI
{
    /// <summary>
    /// 训练菜单：开始前、暂停时（Esc）和结算时显示。左边选地图和训练选项，右边是灵敏度、准星和画面设置。
    /// 设置会立即生效并保存在本机。
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

        [SerializeField] TrainingSession session;

        string sensitivityText;
        string dpiText;
        bool optionsChangedWhilePaused;
        Vector2 scroll;
        Vector2 areaOrigin;
        Rect scrollRect;
        Vector2 previewCenter;

        public void Bind(TrainingSession training) => session = training;

        void Start()
        {
            if (session == null) session = GetComponent<TrainingSession>();
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
            DrawPreferences();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
            if (Event.current.type == EventType.Repaint) scrollRect = GUILayoutUtility.GetLastRect();
            DrawFooter();
            GUILayout.EndArea();

            // 准星预览按实际像素绘制（和游戏里看到的大小一样），所以放在缩放矩阵之外画。
            if (Event.current.type == EventType.Repaint)
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

            string record;
            if (session.NewRecord) record = "新纪录！这是当前地图、模式和设置下的最好成绩。";
            else if (session.PreviousBest != null)
                record = prefire
                    ? $"当前设置下的最快清点：{session.PreviousBest.bestRoundTime:0.00} 秒"
                    : $"当前设置下的最高得分：{session.PreviousBest.score}";
            else record = "完成一局后会记录这个设置下的最好成绩。";
            GUILayout.Label(record, session.NewRecord ? Colored(UiTheme.Good) : UiTheme.MutedText);
            GUILayout.Space(10f);
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
            GUILayout.BeginHorizontal();
            foreach (SceneCatalog.Entry entry in SceneCatalog.Training)
            {
                if (Choice(entry.Title, entry.SceneName == activeScene, 150f) && entry.SceneName != activeScene)
                {
                    session.LoadScene(entry.SceneName);
                }
            }
            GUILayout.EndHorizontal();

            Section("模式");
            GUILayout.BeginHorizontal();
            foreach (TrainingMode mode in new[] { TrainingMode.Flick, TrainingMode.Strafe, TrainingMode.Duel, TrainingMode.Prefire })
            {
                if (Choice(TrainingModeInfo.Title(mode), s.mode == mode, 112f) && s.mode != mode)
                {
                    s.mode = mode;
                    OptionChanged();
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.Label(TrainingModeInfo.Description(s.mode), UiTheme.MutedText);

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

            Section(s.mode == TrainingMode.Prefire ? "同时在场的假人（预瞄模式使用地图上所有预瞄点位）" : "同时在场的假人");
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
            GUILayout.Label("WASD 移动 · Shift 静步 · Ctrl/C 下蹲 · 空格跳 · 左键射击 · R 换弹 · Esc 暂停 · F1 调试面板", UiTheme.MutedText);
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
                    if (GUILayout.Button("开始训练", UiTheme.Primary)) StartSession();
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

        void OptionChanged()
        {
            if (session.CurrentPhase == TrainingSession.Phase.Paused) optionsChangedWhilePaused = true;
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
