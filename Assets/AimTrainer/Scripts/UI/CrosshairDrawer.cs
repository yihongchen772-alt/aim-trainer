using AimTrainer.Game;
using UnityEngine;

namespace AimTrainer.UI
{
    /// <summary>按设置画准星：四条线 + 可选中心点 + 可选黑色外框。单位是屏幕像素，不随界面缩放。</summary>
    public static class CrosshairDrawer
    {
        public static void Draw(Vector2 center, GameSettings s)
        {
            int length = s.crosshairLength;
            int gap = s.crosshairGap;
            int thick = s.crosshairThickness;
            float half = Mathf.Floor(thick / 2f);
            float cx = Mathf.Round(center.x);
            float cy = Mathf.Round(center.y);

            if (s.crosshairOutline) DrawShape(cx, cy, length, gap, thick, half, s.crosshairDot, 1f, Color.black);
            DrawShape(cx, cy, length, gap, thick, half, s.crosshairDot, 0f, s.crosshairColor);
        }

        static void DrawShape(float cx, float cy, int length, int gap, int thick, float half, bool dot, float grow, Color color)
        {
            if (length > 0)
            {
                Rect(cx - half - grow, cy - gap - length - grow, thick + 2 * grow, length + 2 * grow, color);
                Rect(cx - half - grow, cy + gap + (thick % 2) - grow, thick + 2 * grow, length + 2 * grow, color);
                Rect(cx - gap - length - grow, cy - half - grow, length + 2 * grow, thick + 2 * grow, color);
                Rect(cx + gap + (thick % 2) - grow, cy - half - grow, length + 2 * grow, thick + 2 * grow, color);
            }
            if (dot) Rect(cx - half - grow, cy - half - grow, thick + 2 * grow, thick + 2 * grow, color);
        }

        static void Rect(float x, float y, float w, float h, Color color) => UiTheme.Fill(new Rect(x, y, w, h), color);
    }
}
