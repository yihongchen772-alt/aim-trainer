using AimTrainer.Combat;
using AimTrainer.Game;
using AimTrainer.Weapons;
using UnityEngine;

namespace AimTrainer.UI
{
    /// <summary>
    /// 训练中的界面：准星、命中标记、精准度指示、计时、成绩、生命值、弹药、受伤红屏和中央提示。
    /// 用 IMGUI 绘制，不需要 Canvas 或任何界面资源。
    /// </summary>
    public sealed class TrainingHud : MonoBehaviour
    {
        enum MarkerKind
        {
            Body,
            Head,
            Kill,
        }

        [SerializeField] TrainingSession session;

        float markerTime = -10f;
        MarkerKind markerKind;
        float lastStatsUpdate = -1f;
        string statsLine = string.Empty;
        string timerText = string.Empty;

        public void Bind(TrainingSession training) => session = training;

        void Start()
        {
            if (session == null) session = GetComponent<TrainingSession>();
            if (session != null && session.Weapon != null) session.Weapon.ShotFired += OnShot;
            // 小地图是后来加的：以前生成的场景里没有这个组件，运行时补上。
            if (session != null && GetComponent<Minimap>() == null) gameObject.AddComponent<Minimap>().Bind(session);
        }

        void OnDestroy()
        {
            if (session != null && session.Weapon != null) session.Weapon.ShotFired -= OnShot;
        }

        void OnShot(ShotReport report)
        {
            if (!report.HitTarget) return;
            markerTime = Time.unscaledTime;
            markerKind = report.Damage.Killed ? MarkerKind.Kill : report.Zone == HitZone.Head ? MarkerKind.Head : MarkerKind.Body;
        }

        void OnGUI()
        {
            if (session == null || Event.current.type != EventType.Repaint) return;
            TrainingSession.Phase phase = session.CurrentPhase;
            if (phase != TrainingSession.Phase.Playing && phase != TrainingSession.Phase.Paused) return;

            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            DrawDamageVignette();
            if (phase == TrainingSession.Phase.Playing)
            {
                CrosshairDrawer.Draw(center, session.Settings);
                DrawHitMarker(center);
                DrawAccuracyIndicator(center);
            }

            Matrix4x4 previous = GUI.matrix;
            float scale = UiTheme.Scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            DrawTopBar(width);
            DrawHealth(height);
            DrawAmmo(width, height);
            DrawCenterMessage(width, height);
            GUI.matrix = previous;
        }

        void DrawTopBar(float width)
        {
            if (Time.unscaledTime - lastStatsUpdate > 0.1f)
            {
                lastStatsUpdate = Time.unscaledTime;
                SessionStats s = session.Stats;
                bool prefire = session.ActiveMode == TrainingMode.Prefire;
                float remaining = session.Remaining;
                timerText = remaining >= 0f ? FormatClock(remaining) : FormatClock(session.Elapsed);

                string line = $"击杀 {s.Kills}   命中率 {s.Accuracy * 100f:0}%   爆头率 {s.HeadshotRate * 100f:0}%";
                if (s.AverageKillTime > 0f) line += $"   平均击杀 {s.AverageKillTime * 1000f:0} ms";
                if (TrainingModeInfo.BotsShoot(session.ActiveMode)) line += $"   死亡 {s.Deaths}";
                if (prefire)
                {
                    line += $"\n剩余假人 {session.AliveBots}   本轮 {session.RoundTime:0.00} s";
                    if (s.RoundsCleared > 0) line += $"   最快 {s.BestRoundTime:0.00} s";
                    if (session.ActiveRoute != null) line += $"   路线：{session.ActiveRoute.Label}";
                }
                else if (session.ActiveHold != null)
                {
                    line += $"\n架枪：{session.ActiveHold.Label}";
                }
                statsLine = line;
            }

            var timerStyle = UiTheme.HudBig;
            timerStyle.alignment = TextAnchor.UpperCenter;
            UiTheme.ShadowLabel(new Rect(0f, 14f, width, 44f), timerText, timerStyle);
            var lineStyle = UiTheme.HudText;
            lineStyle.alignment = TextAnchor.UpperCenter;
            UiTheme.ShadowLabel(new Rect(0f, 58f, width, 48f), statsLine, lineStyle);

            if (session.CurrentPhase == TrainingSession.Phase.Playing && session.Elapsed < 4f)
            {
                UiTheme.ShadowLabel(new Rect(0f, 104f, width, 24f), "Esc 暂停 / 设置", lineStyle);
            }
        }

        void DrawHealth(float height)
        {
            if (!TrainingModeInfo.BotsShoot(session.ActiveMode) || session.PlayerHealth == null) return;
            Health health = session.PlayerHealth;
            var style = UiTheme.HudBig;
            style.alignment = TextAnchor.LowerLeft;
            UiTheme.ShadowLabel(new Rect(28f, height - 82f, 120f, 44f), Mathf.CeilToInt(health.Current).ToString(), style);
            var bar = new Rect(28f, height - 34f, 200f, 6f);
            UiTheme.Fill(bar, new Color(1f, 1f, 1f, 0.2f));
            float fraction = Mathf.Clamp01(health.Current / health.Max);
            UiTheme.Fill(new Rect(bar.x, bar.y, bar.width * fraction, bar.height),
                fraction > 0.35f ? Color.white : UiTheme.Accent);
        }

        void DrawAmmo(float width, float height)
        {
            HitscanWeapon weapon = session.Weapon;
            if (weapon == null) return;
            var style = UiTheme.HudBig;
            style.alignment = TextAnchor.LowerRight;
            UiTheme.ShadowLabel(new Rect(width - 228f, height - 82f, 200f, 44f), $"{weapon.Ammo} / ∞", style);
            var small = UiTheme.HudText;
            small.alignment = TextAnchor.LowerRight;
            string label = weapon.IsReloading ? "换弹中" : weapon.Stats.displayName;
            UiTheme.ShadowLabel(new Rect(width - 228f, height - 40f, 200f, 22f), label, small);
            if (weapon.IsReloading)
            {
                var bar = new Rect(width - 228f, height - 16f, 200f, 4f);
                UiTheme.Fill(bar, new Color(1f, 1f, 1f, 0.2f));
                UiTheme.Fill(new Rect(bar.x, bar.y, bar.width * weapon.ReloadProgress, bar.height), Color.white);
            }
        }

        void DrawCenterMessage(float width, float height)
        {
            string message = session.CenterMessage;
            if (string.IsNullOrEmpty(message)) return;
            UiTheme.ShadowLabel(new Rect(0f, height * 0.36f, width, 50f), message, UiTheme.HudCenter);
        }

        void DrawDamageVignette()
        {
            float since = Time.unscaledTime - session.LastDamageTime;
            if (since > 0.5f) return;
            float alpha = 0.3f * (1f - since / 0.5f);
            float edge = Screen.height * 0.12f;
            var color = new Color(0.85f, 0.05f, 0.1f, alpha);
            UiTheme.Fill(new Rect(0f, 0f, Screen.width, edge), color);
            UiTheme.Fill(new Rect(0f, Screen.height - edge, Screen.width, edge), color);
            UiTheme.Fill(new Rect(0f, edge, edge, Screen.height - 2f * edge), color);
            UiTheme.Fill(new Rect(Screen.width - edge, edge, edge, Screen.height - 2f * edge), color);
        }

        void DrawHitMarker(Vector2 center)
        {
            float since = Time.unscaledTime - markerTime;
            if (since > 0.12f) return;
            Color color = markerKind == MarkerKind.Kill ? UiTheme.Accent
                : markerKind == MarkerKind.Head ? UiTheme.Warn : Color.white;
            color.a = 1f - since / 0.12f;

            Matrix4x4 previous = GUI.matrix;
            for (int i = 0; i < 4; i++)
            {
                GUIUtility.RotateAroundPivot(45f + i * 90f, center);
                UiTheme.Fill(new Rect(center.x + 9f, center.y - 1f, 8f, 2f), color);
                GUI.matrix = previous;
            }
        }

        void DrawAccuracyIndicator(Vector2 center)
        {
            if (!session.Settings.showAccuracyIndicator || session.Weapon == null) return;
            Color color;
            switch (session.Weapon.CurrentTier)
            {
                case AccuracyTier.Standing:
                    color = UiTheme.Good;
                    break;
                case AccuracyTier.Walking:
                    color = UiTheme.Warn;
                    break;
                default:
                    color = UiTheme.Bad;
                    break;
            }
            float y = Mathf.Round(center.y + session.Settings.crosshairGap + session.Settings.crosshairLength + 12f);
            UiTheme.Fill(new Rect(Mathf.Round(center.x) - 3f, y - 1f, 6f, 6f), Color.black);
            UiTheme.Fill(new Rect(Mathf.Round(center.x) - 2f, y, 4f, 4f), color);
        }

        static string FormatClock(float seconds)
        {
            int total = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{total / 60}:{total % 60:00}";
        }
    }
}
