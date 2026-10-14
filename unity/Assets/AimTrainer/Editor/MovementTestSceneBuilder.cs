using System.IO;
using System.Linq;
using AimTrainer.Core;
using AimTrainer.Diagnostics;
using AimTrainer.Movement;
using AimTrainer.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace AimTrainer.EditorTools
{
    /// <summary>
    /// 菜单 AimTrainer → 创建移动测试场景：一键生成第一阶段用的白模测试场景。
    /// 场景里有速度跑道、台阶、跳台、下蹲通道和急停靶场，玩家走进每个区域时调试面板会显示测试方法。
    /// 所有尺寸单位都是米，1 个 Unity 单位 = 1 米。
    /// </summary>
    public static class MovementTestSceneBuilder
    {
        const string Root = "Assets/AimTrainer";
        const string GeneratedFolder = Root + "/Generated";
        const string ScenesFolder = Root + "/Scenes";
        const string SettingsFolder = Root + "/Settings";
        const string ScenePath = ScenesFolder + "/MovementTest.unity";
        const string SettingsPath = SettingsFolder + "/DefaultMovement.asset";
        const string GridTexturePath = GeneratedFolder + "/GridChecker.asset";

        const float FloorSize = 80f;
        const float WallHeight = 6f;
        static readonly Vector3 Spawn = new Vector3(0f, 0f, -32f);

        struct Palette
        {
            public Material Floor;
            public Material Wall;
            public Material Block;
            public Material Marker;
            public Material Target;
        }

        const string MenuPath = "AimTrainer/创建移动测试场景（第一阶段）";

        [MenuItem(MenuPath, true)]
        static bool CanBuild() => !EditorApplication.isPlayingOrWillChangePlaymode;

        [MenuItem(MenuPath)]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("无法生成", "请先退出 Play 模式再生成测试场景。", "好");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("重新生成测试场景", ScenePath + " 已存在，重新生成会覆盖它（移动参数资源不会被覆盖）。",
                    "覆盖", "取消"))
            {
                return;
            }

            EnsureFolder(GeneratedFolder);
            EnsureFolder(ScenesFolder);
            EnsureFolder(SettingsFolder);

            MovementSettings settings = LoadOrCreateSettings();
            Palette palette = CreatePalette();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildLighting();
            var level = new GameObject("Level").transform;
            BuildShell(level, palette);
            BuildSpeedLane(level, palette);
            BuildStepTest(level, palette);
            BuildJumpTest(level, palette);
            BuildCrouchTunnel(level, palette);
            BuildStrafeRange(level, palette);
            GameObject player = BuildPlayer(settings);
            new GameObject("Systems").AddComponent<FrameRateSettings>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = player;
            Debug.Log("[AimTrainer] 已生成 " + ScenePath + "。点 Play，再点一下 Game 视图锁定鼠标即可开始。");
        }

        // ---------- 资源 ----------

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static MovementSettings LoadOrCreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<MovementSettings>(SettingsPath);
            if (settings != null) return settings;
            settings = ScriptableObject.CreateInstance<MovementSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            return settings;
        }

        static Palette CreatePalette()
        {
            Shader lit = DefaultLitShader();
            Material floor = GetOrCreateMaterial("Floor", lit, Color.white);
            floor.mainTexture = GetOrCreateGridTexture();
            floor.mainTextureScale = new Vector2(FloorSize / 2f, FloorSize / 2f);
            EditorUtility.SetDirty(floor);

            return new Palette
            {
                Floor = floor,
                Wall = GetOrCreateMaterial("Wall", lit, new Color(0.72f, 0.72f, 0.74f)),
                Block = GetOrCreateMaterial("Block", lit, new Color(0.55f, 0.57f, 0.6f)),
                Marker = GetOrCreateMaterial("Marker", lit, new Color(1f, 0.62f, 0.2f)),
                Target = GetOrCreateMaterial("Target", lit, new Color(0.9f, 0.22f, 0.2f)),
            };
        }

        /// <summary>新建立方体时 Unity 自动使用的材质对应当前渲染管线（内置管线是 Standard，URP 是 Lit），取它的着色器最省事。</summary>
        static Shader DefaultLitShader()
        {
            GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Shader shader = probe.GetComponent<Renderer>().sharedMaterial.shader;
            Object.DestroyImmediate(probe);
            return shader;
        }

        static Material GetOrCreateMaterial(string name, Shader shader, Color color)
        {
            string path = GeneratedFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            material.color = color;
            // 白模用低光泽度，避免高光干扰看边缘。内置管线叫 _Glossiness，URP 叫 _Smoothness。
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.1f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.1f);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>每格 1 米的浅灰棋盘格贴图，用来感知移动速度和距离。</summary>
        static Texture2D GetOrCreateGridTexture()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(GridTexturePath);
            if (existing != null) return existing;

            const int size = 256;
            const int half = size / 2;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "GridChecker",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 8,
            };
            var light = new Color32(150, 153, 158, 255);
            var dark = new Color32(136, 139, 144, 255);
            var line = new Color32(105, 108, 113, 255);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool edge = x % half < 2 || y % half < 2;
                    bool checker = (x < half) == (y < half);
                    pixels[y * size + x] = edge ? line : checker ? light : dark;
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(true);
            AssetDatabase.CreateAsset(texture, GridTexturePath);
            return texture;
        }

        // ---------- 场景 ----------

        static void BuildLighting()
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // 三色环境光不需要烘焙，打开场景就是最终效果。
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.66f, 0.72f);
            RenderSettings.ambientEquatorColor = new Color(0.48f, 0.48f, 0.48f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.29f, 0.27f);
            RenderSettings.fog = false;
        }

        static void BuildShell(Transform level, Palette p)
        {
            float half = FloorSize / 2f;
            Block(level, "Floor", 0f, 0f, FloorSize, FloorSize, 1f, p.Floor, y: -1f);
            Block(level, "Wall_North", 0f, half + 0.5f, FloorSize + 2f, 1f, WallHeight, p.Wall);
            Block(level, "Wall_South", 0f, -half - 0.5f, FloorSize + 2f, 1f, WallHeight, p.Wall);
            Block(level, "Wall_East", half + 0.5f, 0f, 1f, FloorSize, WallHeight, p.Wall);
            Block(level, "Wall_West", -half - 0.5f, 0f, 1f, FloorSize, WallHeight, p.Wall);
        }

        static void BuildSpeedLane(Transform level, Palette p)
        {
            Transform zone = Zone(level, "速度跑道", new Vector3(-26f, 0f, 0f), new Vector3(8f, 4f, 72f),
                "直线跑道，每 5 米一条橙色横线。全速跑完相邻两条线约需 0.93 秒（5.4 米/秒）。按住 Shift 静步约 1.8 秒。");
            for (int i = -6; i <= 6; i++)
            {
                Block(zone, "Line_" + (i * 5), 0f, i * 5f, 8f, 0.12f, 0.01f, p.Marker, collider: false, local: true);
            }
        }

        static void BuildStepTest(Transform level, Palette p)
        {
            Transform zone = Zone(level, "台阶测试", new Vector3(-12f, 0f, -10f), new Vector3(10f, 4f, 22f),
                "四个台子高 0.20 / 0.35 / 0.55 / 0.70 米。0.40 米以下可以直接走上去，更高的需要跳。从台子上走下来不会短暂进入“空中”状态。");
            float[] heights = { 0.2f, 0.35f, 0.55f, 0.7f };
            for (int i = 0; i < heights.Length; i++)
            {
                Block(zone, "Step_" + heights[i].ToString("0.00"), 0f, -7.5f + i * 5f, 3f, 2.5f, heights[i], p.Block, local: true);
            }
        }

        static void BuildJumpTest(Transform level, Palette p)
        {
            Transform zone = Zone(level, "跳跃测试", new Vector3(-12f, 0f, 16f), new Vector3(10f, 4f, 22f),
                "四个台子高 0.70 / 0.85 / 1.10 / 1.25 米。跳高 0.9 米：前两个能跳上去，后两个跳不上。");
            float[] heights = { 0.7f, 0.85f, 1.1f, 1.25f };
            for (int i = 0; i < heights.Length; i++)
            {
                Block(zone, "Ledge_" + heights[i].ToString("0.00"), 0f, -7.5f + i * 5f, 3f, 2.5f, heights[i], p.Block, local: true);
            }
        }

        static void BuildCrouchTunnel(Transform level, Palette p)
        {
            Transform zone = Zone(level, "下蹲通道", new Vector3(4f, 0f, 0f), new Vector3(6f, 4f, 14f),
                "顶高 1.45 米，宽 2.4 米。站着进不去，按住 Ctrl 或 C 蹲下才能通过；在里面松开下蹲不会站起来。");
            const float length = 10f;
            const float innerWidth = 2.4f;
            const float ceiling = 1.45f;
            Block(zone, "Tunnel_Left", -innerWidth / 2f - 0.25f, 0f, 0.5f, length, 2.2f, p.Wall, local: true);
            Block(zone, "Tunnel_Right", innerWidth / 2f + 0.25f, 0f, 0.5f, length, 2.2f, p.Wall, local: true);
            Block(zone, "Tunnel_Roof", 0f, 0f, innerWidth + 1f, length, 0.3f, p.Block, y: ceiling, local: true);
        }

        static void BuildStrafeRange(Transform level, Palette p)
        {
            Transform zone = Zone(level, "急停靶场", new Vector3(24f, 0f, 0f), new Vector3(28f, 4f, 72f),
                "对着前方的红色靶子按 A / D 左右横跳：松开一个键的同时按另一个（两个键同时按住等于松键）。速度条变绿时就是可以精准射击的时刻，看左上角“反向急停”的耗时。");
            float[] distances = { 8f, 15f, 25f };
            float[] offsets = { -6f, 0f, 6f };
            for (int i = 0; i < distances.Length; i++)
            {
                for (int j = 0; j < offsets.Length; j++)
                {
                    Target(zone, new Vector3(offsets[j], 0f, -10f + distances[i]), p.Target);
                }
            }
            Block(zone, "StartLine", 0f, -12f, 20f, 0.12f, 0.01f, p.Marker, collider: false, local: true);
        }

        static void Target(Transform parent, Vector3 localFeet, Material material)
        {
            var target = new GameObject("Target").transform;
            target.SetParent(parent, false);
            target.localPosition = localFeet;
            // 身体到 1.45 米，头部中心在 1.6 米、直径 0.3 米，和网页原型的假人一致。
            Block(target, "Body", 0f, 0f, 0.5f, 0.3f, 1.45f, material, local: true);
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(target, false);
            head.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            head.transform.localScale = Vector3.one * 0.3f;
            head.GetComponent<Renderer>().sharedMaterial = material;
        }

        static GameObject BuildPlayer(MovementSettings settings)
        {
            MovementTuning t = settings.tuning;
            var player = new GameObject("Player");
            player.transform.SetPositionAndRotation(Spawn, Quaternion.identity);

            var body = player.AddComponent<CharacterController>();
            body.radius = 0.3f;
            body.height = t.standingHeight;
            body.center = new Vector3(0f, t.standingHeight / 2f, 0f);
            body.stepOffset = t.stepOffset;
            body.skinWidth = 0.03f;
            body.minMoveDistance = 0f;
            body.slopeLimit = 45f;

            var cameraObject = new GameObject("PlayerCamera") { tag = "MainCamera" };
            cameraObject.transform.SetParent(player.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, t.standingEyeHeight, 0f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.03f;
            camera.farClipPlane = 500f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.72f, 0.82f);
            cameraObject.AddComponent<AudioListener>();

            player.AddComponent<PlayerController>().Configure(settings, camera);
            player.AddComponent<MovementDebugHud>().Bind(player.GetComponent<PlayerController>());
            return player;
        }

        // ---------- 工具 ----------

        static Transform Zone(Transform level, string title, Vector3 center, Vector3 size, string description)
        {
            var zone = new GameObject(title).transform;
            zone.SetParent(level, false);
            zone.position = center + Vector3.up * (size.y / 2f);
            zone.gameObject.AddComponent<TestZoneLabel>().Configure(title, description, size);

            var root = new GameObject("Geometry").transform;
            root.SetParent(zone, false);
            root.position = center;
            return root;
        }

        /// <summary>按“底面中心 + 尺寸”放一个立方体，比按几何中心放更直观。</summary>
        static GameObject Block(Transform parent, string name, float x, float z, float width, float depth, float height,
            Material material, float y = 0f, bool collider = true, bool local = false)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            var center = new Vector3(x, y + height / 2f, z);
            if (local) block.transform.localPosition = center;
            else block.transform.position = center;
            block.transform.localScale = new Vector3(width, height, depth);
            block.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) Object.DestroyImmediate(block.GetComponent<Collider>());
            GameObjectUtility.SetStaticEditorFlags(block, StaticEditorFlags.BatchingStatic);
            return block;
        }

        static void AddToBuildSettings(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == scenePath)) return;
            scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
