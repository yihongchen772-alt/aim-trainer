using System.Text;
using AimTrainer.Movement;
using AimTrainer.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AimTrainer.Diagnostics
{
    /// <summary>
    /// 左上角的调试面板：帧率、水平速度、是否可以精准射击、急停实测耗时、灵敏度换算，以及最近 2 秒的速度曲线。
    /// F1 显示 / 隐藏，F2 把累计转角归零（用来核对 360° 距离）。
    /// </summary>
    public sealed class MovementDebugHud : MonoBehaviour
    {
        const int HistoryLength = 240;
        const float SampleInterval = 1f / 120f;
        const float PanelWidth = 430f;

        [SerializeField] PlayerController player;
        [SerializeField] bool visible = true;

        [Tooltip("只用于显示 360° 需要多少厘米，不影响实际灵敏度。")]
        [SerializeField, Min(100f)] float mouseDpi = 800f;

        readonly float[] speedHistory = new float[HistoryLength];
        readonly bool[] accurateHistory = new bool[HistoryLength];
        readonly StringBuilder text = new StringBuilder(512);

        int historyHead;
        float sampleTimer;
        int fpsFrames;
        float fpsTimer;
        float worstFrame;
        float fps;
        float worstFrameMs;
        float textTimer;
        string cachedText = string.Empty;
        GUIStyle labelStyle;
        Texture2D white;

        public void Bind(PlayerController target) => player = target;

        /// <summary>只用于显示 360° 需要多少厘米。</summary>
        public float MouseDpi
        {
            get => mouseDpi;
            set => mouseDpi = Mathf.Max(100f, value);
        }

        /// <summary>是否显示面板（F1 切换）。</summary>
        public bool Visible
        {
            get => visible;
            set => visible = value;
        }

        void Awake()
        {
            if (player == null) player = GetComponent<PlayerController>();
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.f1Key.wasPressedThisFrame) visible = !visible;
                if (keyboard.f2Key.wasPressedThisFrame && player != null) player.ResetAccumulatedYaw();
            }

            float frame = Time.unscaledDeltaTime;
            fpsFrames++;
            fpsTimer += frame;
            if (frame > worstFrame) worstFrame = frame;
            if (fpsTimer >= 0.5f)
            {
                fps = fpsFrames / fpsTimer;
                worstFrameMs = worstFrame * 1000f;
                fpsFrames = 0;
                fpsTimer = 0f;
                worstFrame = 0f;
            }

            if (player == null) return;

            sampleTimer += Time.deltaTime;
            if (sampleTimer > 0.25f) sampleTimer = SampleInterval;
            while (sampleTimer >= SampleInterval)
            {
                sampleTimer -= SampleInterval;
                speedHistory[historyHead] = player.HorizontalSpeed;
                accurateHistory[historyHead] = player.IsAccurate;
                historyHead = (historyHead + 1) % HistoryLength;
            }

            textTimer -= frame;
            if (visible && textTimer <= 0f)
            {
                textTimer = 0.1f;
                cachedText = BuildText();
            }
        }

        string BuildText()
        {
            MovementTuning t = player.Tuning;
            float sens = player.Sensitivity;
            text.Clear();
            text.Append("FPS ").Append(fps.ToString("0"))
                .Append("   帧时间 ").Append(fps > 0f ? (1000f / fps).ToString("0.00") : "-")
                .Append(" ms（最慢 ").Append(worstFrameMs.ToString("0.0")).Append(" ms）\n");
            text.Append("水平速度 ").Append(player.HorizontalSpeed.ToString("0.00")).Append(" m/s   状态 ")
                .Append(StateName(player.State)).Append('\n');
            text.Append("精准射击 ").Append(player.IsAccurate ? "可以" : "不行")
                .Append("（地面且速度 ≤ ").Append(t.accurateSpeedThreshold.ToString("0.0")).Append(" m/s）\n");
            AppendStop("松键急停 ", player.StopTimer.LastRelease, t.TheoreticalReleaseStopTime);
            AppendStop("反向急停 ", player.StopTimer.LastCounterStrafe, t.TheoreticalCounterStrafeStopTime);
            text.Append("灵敏度 ").Append(sens.ToString("0.000"))
                .Append(" · 360° = ").Append(ViewMath.CountsPer360(sens).ToString("0")).Append(" 计数 = ")
                .Append(ViewMath.CentimetersPer360(sens, mouseDpi).ToString("0.0")).Append(" cm @ ")
                .Append(mouseDpi.ToString("0")).Append(" DPI\n");
            text.Append("累计转角 ").Append(player.AccumulatedYaw.ToString("0.0")).Append("°（F2 归零）   F1 隐藏面板");
            if (!player.IsCursorLocked) text.Append("\n点击画面锁定鼠标后才能移动");

            TestZoneLabel zone = TestZoneLabel.Find(player.transform.position);
            if (zone != null) text.Append("\n\n【").Append(zone.Title).Append("】").Append(zone.Description);
            return text.ToString();
        }

        void AppendStop(string label, StopMeasurement? last, float theoretical)
        {
            text.Append(label);
            if (last.HasValue)
            {
                StopMeasurement m = last.Value;
                text.Append(Ms(m.Seconds)).Append(" ms（理论 ").Append(Ms(m.PredictedSeconds)).Append(" ms");
                if (m.GapSeconds > 0f) text.Append("，含空档 ").Append(Ms(m.GapSeconds)).Append(" ms");
                text.Append("，起速 ").Append(m.StartSpeed.ToString("0.0")).Append("）\n");
            }
            else
            {
                text.Append("—（全速跑动后测量，理论 ").Append(Ms(theoretical)).Append(" ms）\n");
            }
        }

        static string Ms(float seconds) => float.IsInfinity(seconds) ? "∞" : (seconds * 1000f).ToString("0");

        static string StateName(MoveState state)
        {
            switch (state)
            {
                case MoveState.Idle: return "静止";
                case MoveState.Running: return "跑步";
                case MoveState.Walking: return "静步";
                case MoveState.Crouching: return "下蹲";
                default: return "空中";
            }
        }

        void OnGUI()
        {
            if (player == null || Event.current.type != EventType.Repaint) return;

            // 没有训练菜单的场景（移动测试）里由这里画准星；训练场景的准星由 TrainingHud 画。
            if (player.ManageCursor && player.IsCursorLocked)
            {
                UI.CrosshairDrawer.Draw(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), Game.GameSettings.Current);
            }

            if (!visible) return;
            EnsureStyles();

            Vector2 textSize = labelStyle.CalcSize(new GUIContent(cachedText));
            float textHeight = Mathf.Max(textSize.y, 120f);
            var panel = new Rect(12f, 12f, Mathf.Max(PanelWidth, textSize.x + 24f), textHeight + 100f);
            Fill(panel, new Color(0f, 0f, 0f, 0.62f));
            GUI.Label(new Rect(panel.x + 12f, panel.y + 8f, panel.width - 24f, textHeight), cachedText, labelStyle);

            MovementTuning t = player.Tuning;
            float maxSpeed = Mathf.Max(t.runSpeed * 1.1f, 0.1f);

            // 当前速度条：绿色表示可以精准射击。
            var bar = new Rect(panel.x + 12f, panel.y + textHeight + 16f, panel.width - 24f, 8f);
            Fill(bar, new Color(1f, 1f, 1f, 0.15f));
            Fill(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(player.HorizontalSpeed / maxSpeed), bar.height),
                player.IsAccurate ? new Color(0.24f, 0.86f, 0.59f) : new Color(1f, 0.55f, 0.2f));

            // 最近 2 秒的速度曲线，绿色表示当时可以精准射击，黄线是速度门槛。
            var graph = new Rect(bar.x, bar.yMax + 8f, bar.width, 60f);
            Fill(graph, new Color(1f, 1f, 1f, 0.06f));
            float columnWidth = graph.width / HistoryLength;
            for (int i = 0; i < HistoryLength; i++)
            {
                int index = (historyHead + i) % HistoryLength;
                float h = graph.height * Mathf.Clamp01(speedHistory[index] / maxSpeed);
                Color c = accurateHistory[index] ? new Color(0.24f, 0.86f, 0.59f, 0.9f) : new Color(1f, 0.55f, 0.2f, 0.9f);
                Fill(new Rect(graph.x + i * columnWidth, graph.yMax - h, Mathf.Max(1f, columnWidth), h), c);
            }
            float thresholdY = graph.yMax - graph.height * Mathf.Clamp01(t.accurateSpeedThreshold / maxSpeed);
            Fill(new Rect(graph.x, thresholdY, graph.width, 1f), new Color(1f, 0.85f, 0.2f, 0.9f));
        }

        void EnsureStyles()
        {
            if (labelStyle != null) return;
            white = Texture2D.whiteTexture;
            labelStyle = new GUIStyle(GUI.skin.label) { font = UI.UiTheme.Font, fontSize = 14, richText = false, wordWrap = false };
            labelStyle.normal.textColor = Color.white;
        }

        void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, white);
            GUI.color = previous;
        }
    }
}
