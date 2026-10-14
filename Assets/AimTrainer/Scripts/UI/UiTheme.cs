using UnityEngine;

namespace AimTrainer.UI
{
    /// <summary>界面用到的颜色、字号和 IMGUI 样式。所有样式第一次在 OnGUI 里用到时创建。</summary>
    public static class UiTheme
    {
        public static readonly Color Panel = new Color(0.06f, 0.07f, 0.09f, 0.94f);
        public static readonly Color PanelLight = new Color(0.12f, 0.13f, 0.16f, 1f);
        public static readonly Color Line = new Color(0.22f, 0.24f, 0.29f, 1f);
        public static readonly Color Text = new Color(0.92f, 0.93f, 0.95f);
        public static readonly Color Muted = new Color(0.58f, 0.61f, 0.68f);
        public static readonly Color Accent = new Color(1f, 0.27f, 0.33f);
        public static readonly Color Good = new Color(0.24f, 0.86f, 0.59f);
        public static readonly Color Warn = new Color(1f, 0.8f, 0.25f);
        public static readonly Color Bad = new Color(1f, 0.42f, 0.25f);

        static GUIStyle title;
        static GUIStyle heading;
        static GUIStyle body;
        static GUIStyle muted;
        static GUIStyle button;
        static GUIStyle buttonOn;
        static GUIStyle primary;
        static GUIStyle field;
        static GUIStyle hudBig;
        static GUIStyle hudText;
        static GUIStyle hudCenter;
        static GUIStyle statValue;
        static Texture2D panelTex;
        static Texture2D lightTex;
        static Texture2D accentTex;
        static Texture2D hoverTex;
        static Texture2D accentHoverTex;

        /// <summary>按 1080p 设计的界面在当前分辨率下的缩放比例。</summary>
        public static float Scale => Mathf.Clamp(Screen.height / 1080f, 0.6f, 2.5f);

        public static GUIStyle Title => title ??= Label(30, FontStyle.Bold, Text);
        public static GUIStyle Heading => heading ??= Label(13, FontStyle.Bold, Muted);
        public static GUIStyle Body => body ??= Wrap(Label(15, FontStyle.Normal, Text));
        public static GUIStyle MutedText => muted ??= Wrap(Label(13, FontStyle.Normal, Muted));
        public static GUIStyle HudBig => hudBig ??= Shadowed(Label(34, FontStyle.Bold, Color.white));
        public static GUIStyle HudText => hudText ??= Shadowed(Label(16, FontStyle.Normal, Color.white));
        public static GUIStyle StatValue => statValue ??= Label(28, FontStyle.Bold, Text);

        public static GUIStyle HudCenter
        {
            get
            {
                if (hudCenter != null) return hudCenter;
                hudCenter = Shadowed(Label(32, FontStyle.Bold, Accent));
                hudCenter.alignment = TextAnchor.MiddleCenter;
                return hudCenter;
            }
        }

        public static GUIStyle Button => button ??= MakeButton(LightTex, HoverTex, Text);
        public static GUIStyle ButtonOn => buttonOn ??= MakeButton(AccentTex, AccentHoverTex, Color.white);

        public static GUIStyle Primary
        {
            get
            {
                if (primary != null) return primary;
                primary = MakeButton(AccentTex, AccentHoverTex, Color.white);
                primary.fontSize = 18;
                primary.fontStyle = FontStyle.Bold;
                primary.padding = new RectOffset(28, 28, 12, 12);
                return primary;
            }
        }

        public static GUIStyle Field
        {
            get
            {
                if (field != null) return field;
                field = new GUIStyle(GUI.skin.textField) { fontSize = 15, padding = new RectOffset(8, 8, 6, 6) };
                field.normal.background = LightTex;
                field.focused.background = HoverTex;
                field.normal.textColor = Text;
                field.focused.textColor = Text;
                return field;
            }
        }

        public static Texture2D PanelTex => panelTex != null ? panelTex : panelTex = Solid(Panel);
        static Texture2D LightTex => lightTex != null ? lightTex : lightTex = Solid(PanelLight);
        static Texture2D HoverTex => hoverTex != null ? hoverTex : hoverTex = Solid(new Color(0.19f, 0.2f, 0.25f, 1f));
        static Texture2D AccentTex => accentTex != null ? accentTex : accentTex = Solid(Accent);
        static Texture2D AccentHoverTex => accentHoverTex != null ? accentHoverTex : accentHoverTex = Solid(new Color(1f, 0.38f, 0.43f));

        /// <summary>画一个纯色矩形。</summary>
        public static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        static GUIStyle Label(int size, FontStyle style, Color color)
        {
            var s = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = style, richText = false };
            s.normal.textColor = color;
            return s;
        }

        static GUIStyle Wrap(GUIStyle s)
        {
            s.wordWrap = true;
            return s;
        }

        static GUIStyle Shadowed(GUIStyle s)
        {
            s.wordWrap = false;
            return s;
        }

        static GUIStyle MakeButton(Texture2D normal, Texture2D hover, Color textColor)
        {
            var s = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                padding = new RectOffset(12, 12, 8, 8),
                margin = new RectOffset(0, 6, 0, 6),
                alignment = TextAnchor.MiddleCenter,
            };
            s.normal.background = normal;
            s.hover.background = hover;
            s.active.background = hover;
            s.focused.background = normal;
            s.normal.textColor = textColor;
            s.hover.textColor = textColor;
            s.active.textColor = textColor;
            s.focused.textColor = textColor;
            return s;
        }

        static Texture2D Solid(Color color)
        {
            var tex = new Texture2D(1, 1) { hideFlags = HideFlags.DontSave };
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }

        /// <summary>带黑色描边的文字（HUD 在亮背景上也能看清）。</summary>
        public static void ShadowLabel(Rect rect, string text, GUIStyle style)
        {
            Color original = style.normal.textColor;
            style.normal.textColor = new Color(0f, 0f, 0f, 0.75f);
            GUI.Label(new Rect(rect.x + 1.5f, rect.y + 1.5f, rect.width, rect.height), text, style);
            style.normal.textColor = original;
            GUI.Label(rect, text, style);
        }
    }
}
