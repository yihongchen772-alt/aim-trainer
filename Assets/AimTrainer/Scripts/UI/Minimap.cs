using System.Collections.Generic;
using AimTrainer.Bots;
using AimTrainer.Game;
using AimTrainer.Player;
using UnityEngine;

namespace AimTrainer.UI
{
    /// <summary>
    /// 右上角的小地图：俯视图、玩家位置和朝向、正在看着你的假人（红点），下方显示当前区域名称。
    /// 场景里有 MinimapImage（例如真实地图的官方小地图）时用那张图；否则开始时根据场景里的碰撞体自动画出俯视图（北在上）：
    /// 墙最深，高台和斜坡偏蓝，箱子和掩体浅一些。
    /// </summary>
    public sealed class Minimap : MonoBehaviour
    {
        const float SizeAt1080 = 230f;
        const int MaxTextureSize = 512;

        static readonly Color32 FloorColor = new Color32(58, 64, 74, 200);
        static readonly Color32 WallColor = new Color32(14, 16, 20, 235);
        static readonly Color32 BlockColor = new Color32(105, 110, 120, 235);
        static readonly Color32 CoverColor = new Color32(150, 140, 120, 235);
        static readonly Color32 PlatformColor = new Color32(84, 110, 140, 235);

        [SerializeField] TrainingSession session;

        Texture2D mapTexture;
        bool ownsMapTexture;
        Texture2D arrowTexture;
        Texture2D dotTexture;

        // 世界坐标 (x, z) → 图片坐标 (u 向右, v 向下)：u = U.x·x + U.y·z + U.z。
        Vector3 uFromWorld;
        Vector3 vFromWorld;
        GUIStyle regionStyle;
        string regionText = string.Empty;
        float lastRegionUpdate = -1f;

        public void Bind(TrainingSession training) => session = training;

        void Start()
        {
            if (session == null) session = GetComponent<TrainingSession>();
            MinimapImage image = FindImage();
            if (image != null && image.Image != null)
            {
                mapTexture = image.Image;
                uFromWorld = image.UFromWorld;
                vFromWorld = image.VFromWorld;
            }
            else
            {
                BuildTexture();
                ownsMapTexture = true;
            }
            arrowTexture = CreateArrow(32);
            dotTexture = CreateDot(16);
        }

        void OnDestroy()
        {
            if (mapTexture != null && ownsMapTexture) Destroy(mapTexture);
            if (arrowTexture != null) Destroy(arrowTexture);
            if (dotTexture != null) Destroy(dotTexture);
        }

        void OnGUI()
        {
            if (session == null || mapTexture == null || Event.current.type != EventType.Repaint) return;
            if (!session.Settings.showMinimap) return;
            if (session.CurrentPhase != TrainingSession.Phase.Playing) return;
            PlayerController player = session.Player;
            if (player == null) return;

            float scale = UiTheme.Scale;
            float box = SizeAt1080 * scale;
            float margin = 16f * scale;
            var frame = new Rect(Screen.width - box - margin, margin, box, box);

            // 按比例缩放到正方形框里，较短的一边居中。
            float aspect = (float)mapTexture.width / mapTexture.height;
            Rect map = aspect >= 1f
                ? new Rect(frame.x, frame.y + (box - box / aspect) * 0.5f, box, box / aspect)
                : new Rect(frame.x + (box - box * aspect) * 0.5f, frame.y, box * aspect, box);
            UiTheme.Fill(new Rect(map.x - 2f, map.y - 2f, map.width + 4f, map.height + 4f), new Color(0f, 0f, 0f, 0.45f));
            GUI.DrawTexture(map, mapTexture, ScaleMode.StretchToFill, true);

            // 正在看着你的假人（相当于端游里被队友“报点”的敌人）。
            float dotSize = 9f * scale;
            foreach (BotAgent bot in session.Bots)
            {
                if (bot == null || !bot.CanSeePlayer) continue;
                Vector2 p = ToMap(map, bot.transform.position);
                GUI.color = UiTheme.Accent;
                GUI.DrawTexture(new Rect(p.x - dotSize * 0.5f, p.y - dotSize * 0.5f, dotSize, dotSize), dotTexture);
            }

            // 玩家：箭头指向视线方向（按换算关系把朝向转成图上的方向，官方小地图不一定是北在上）。
            Vector2 center = ToMap(map, player.transform.position);
            float arrow = 16f * scale;
            float yawRad = player.Yaw * Mathf.Deg2Rad;
            float fx = Mathf.Sin(yawRad);
            float fz = Mathf.Cos(yawRad);
            float du = uFromWorld.x * fx + uFromWorld.y * fz;
            float dv = vFromWorld.x * fx + vFromWorld.y * fz;
            float screenAngle = Mathf.Atan2(du, -dv) * Mathf.Rad2Deg;
            Matrix4x4 previous = GUI.matrix;
            GUIUtility.RotateAroundPivot(screenAngle, center);
            GUI.color = new Color(0.35f, 0.95f, 1f);
            GUI.DrawTexture(new Rect(center.x - arrow * 0.5f, center.y - arrow * 0.5f, arrow, arrow), arrowTexture);
            GUI.matrix = previous;
            GUI.color = Color.white;

            if (Time.unscaledTime - lastRegionUpdate > 0.1f)
            {
                lastRegionUpdate = Time.unscaledTime;
                regionText = session.RegionAt(player.transform.position);
            }
            if (!string.IsNullOrEmpty(regionText))
            {
                regionStyle ??= new GUIStyle(UiTheme.HudText) { alignment = TextAnchor.UpperCenter };
                Matrix4x4 before = GUI.matrix;
                GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
                var label = new Rect(frame.x / scale, (map.yMax + 6f * scale) / scale, box / scale, 26f);
                UiTheme.ShadowLabel(label, regionText, regionStyle);
                GUI.matrix = before;
            }
        }

        Vector2 ToMap(Rect map, Vector3 world)
        {
            float u = Mathf.Clamp01(uFromWorld.x * world.x + uFromWorld.y * world.z + uFromWorld.z);
            float v = Mathf.Clamp01(vFromWorld.x * world.x + vFromWorld.y * world.z + vFromWorld.z);
            return new Vector2(map.x + u * map.width, map.y + v * map.height);
        }

        MinimapImage FindImage()
        {
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                var image = root.GetComponentInChildren<MinimapImage>(true);
                if (image != null) return image;
            }
            return null;
        }

        // ---------- 俯视图 ----------

        struct Footprint
        {
            public Bounds Bounds;
            public Color32 Color;
            public bool IsFloor;
        }

        void BuildTexture()
        {
            var footprints = new List<Footprint>();
            Transform playerRoot = session != null && session.Player != null ? session.Player.transform : null;
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                foreach (Collider collider in root.GetComponentsInChildren<Collider>())
                {
                    if (collider.isTrigger || !collider.enabled) continue;
                    if (playerRoot != null && collider.transform.IsChildOf(playerRoot)) continue;
                    if (collider.GetComponentInParent<BotAgent>() != null) continue;
                    bool floor = collider.name == "Floor";
                    footprints.Add(new Footprint { Bounds = collider.bounds, Color = floor ? FloorColor : ColorOf(collider), IsFloor = floor });
                }
            }
            if (footprints.Count == 0) return;

            // 地图范围：有地面时用地面，否则用所有物体的范围。
            Bounds extent = default;
            bool hasExtent = false;
            foreach (Footprint f in footprints)
            {
                if (!f.IsFloor) continue;
                if (!hasExtent) extent = f.Bounds;
                else extent.Encapsulate(f.Bounds);
                hasExtent = true;
            }
            if (!hasExtent)
            {
                extent = footprints[0].Bounds;
                foreach (Footprint f in footprints) extent.Encapsulate(f.Bounds);
            }

            float worldXMin = extent.min.x;
            float worldZMin = extent.min.z;
            float worldWidth = Mathf.Max(1f, extent.size.x);
            float worldDepth = Mathf.Max(1f, extent.size.z);
            // 北在上：u = (x - xMin) / 宽，v = 1 - (z - zMin) / 深。
            uFromWorld = new Vector3(1f / worldWidth, 0f, -worldXMin / worldWidth);
            vFromWorld = new Vector3(0f, -1f / worldDepth, 1f + worldZMin / worldDepth);
            float pixelsPerMeter = Mathf.Min(4f, MaxTextureSize / Mathf.Max(worldWidth, worldDepth));
            int width = Mathf.Max(8, Mathf.CeilToInt(worldWidth * pixelsPerMeter));
            int height = Mathf.Max(8, Mathf.CeilToInt(worldDepth * pixelsPerMeter));

            var pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(0, 0, 0, 0);
            // 先画地面，再从低到高画其他物体，高台上的箱子会盖在高台上面。
            footprints.Sort((a, b) =>
            {
                if (a.IsFloor != b.IsFloor) return a.IsFloor ? -1 : 1;
                return a.Bounds.max.y.CompareTo(b.Bounds.max.y);
            });
            foreach (Footprint f in footprints)
            {
                int x0 = Mathf.Clamp(Mathf.FloorToInt((f.Bounds.min.x - worldXMin) * pixelsPerMeter), 0, width);
                int x1 = Mathf.Clamp(Mathf.CeilToInt((f.Bounds.max.x - worldXMin) * pixelsPerMeter), 0, width);
                int z0 = Mathf.Clamp(Mathf.FloorToInt((f.Bounds.min.z - worldZMin) * pixelsPerMeter), 0, height);
                int z1 = Mathf.Clamp(Mathf.CeilToInt((f.Bounds.max.z - worldZMin) * pixelsPerMeter), 0, height);
                for (int z = z0; z < z1; z++)
                {
                    for (int x = x0; x < x1; x++) pixels[z * width + x] = f.Color;
                }
            }

            mapTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Minimap",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            mapTexture.SetPixels32(pixels);
            mapTexture.Apply(false, true);
        }

        /// <summary>按材质名称（生成场景时用的 Wall / Block / Cover / Platform）选颜色，其他物体按高度判断。</summary>
        static Color32 ColorOf(Collider collider)
        {
            var renderer = collider.GetComponent<Renderer>();
            string material = renderer != null && renderer.sharedMaterial != null ? renderer.sharedMaterial.name : string.Empty;
            if (material.StartsWith("Wall")) return WallColor;
            if (material.StartsWith("Platform")) return PlatformColor;
            if (material.StartsWith("Cover")) return CoverColor;
            if (material.StartsWith("Block")) return BlockColor;
            return collider.bounds.max.y > 2.5f ? WallColor : CoverColor;
        }

        static Texture2D CreateArrow(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 纹理第 0 行画在底部：三角形的尖在上面（y 大的一端）。
                    float t = (float)y / (size - 1);
                    float halfWidth = (1f - t) * half * 0.8f;
                    bool inside = t >= 0.05f && Mathf.Abs(x + 0.5f - half) <= halfWidth;
                    pixels[y * size + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        static Texture2D CreateDot(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r));
                    byte a = (byte)(Mathf.Clamp01(r - d) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
