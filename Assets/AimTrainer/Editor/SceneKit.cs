using System.IO;
using System.Linq;
using AimTrainer.Audio;
using AimTrainer.Bots;
using AimTrainer.Combat;
using AimTrainer.Core;
using AimTrainer.Diagnostics;
using AimTrainer.Game;
using AimTrainer.Movement;
using AimTrainer.Player;
using AimTrainer.UI;
using AimTrainer.Weapons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace AimTrainer.EditorTools
{
    /// <summary>
    /// 生成场景用的公共工具：文件夹、材质、白模方块、灯光、玩家和训练系统。
    /// 所有尺寸单位都是米（1 个 Unity 单位 = 1 米）。
    /// </summary>
    public static class SceneKit
    {
        public const string Root = "Assets/AimTrainer";
        public const string GeneratedFolder = Root + "/Generated";
        public const string ScenesFolder = Root + "/Scenes";
        public const string SettingsFolder = Root + "/Settings";
        const string MovementSettingsPath = SettingsFolder + "/DefaultMovement.asset";
        const string WeaponPath = SettingsFolder + "/DefaultRifle.asset";
        const string GridTexturePath = GeneratedFolder + "/GridChecker.asset";

        /// <summary>玩家的生命值：100 生命 + 50 护甲。</summary>
        public const float PlayerHealth = 150f;

        public struct Palette
        {
            public Material Floor;
            public Material Wall;
            public Material Block;
            public Material Cover;
            public Material Platform;
            public Material Marker;
            public Material Target;
        }

        public struct PlayerRig
        {
            public GameObject Player;
            public PlayerController Controller;
            public Transform Spawn;
            public TrainingSession Session;
        }

        public static string ScenePath(string sceneName) => ScenesFolder + "/" + sceneName + ".unity";

        public static void EnsureFolders()
        {
            EnsureFolder(GeneratedFolder);
            EnsureFolder(ScenesFolder);
            EnsureFolder(SettingsFolder);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // ---------- 资源 ----------

        public static MovementSettings LoadOrCreateMovementSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<MovementSettings>(MovementSettingsPath);
            if (settings != null) return settings;
            settings = ScriptableObject.CreateInstance<MovementSettings>();
            AssetDatabase.CreateAsset(settings, MovementSettingsPath);
            return settings;
        }

        public static WeaponDefinition LoadOrCreateWeapon()
        {
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(WeaponPath);
            if (weapon != null) return weapon;
            weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            AssetDatabase.CreateAsset(weapon, WeaponPath);
            return weapon;
        }

        /// <summary>创建（或复用）白模材质。floorSize 决定地面棋盘格的平铺次数，每格 1 米。</summary>
        public static Palette CreatePalette(string sceneName, Vector2 floorSize)
        {
            Shader lit = DefaultLitShader();
            Material floor = GetOrCreateMaterial("Floor_" + sceneName, lit, Color.white);
            floor.mainTexture = GetOrCreateGridTexture();
            floor.mainTextureScale = floorSize / 2f;
            EditorUtility.SetDirty(floor);

            return new Palette
            {
                Floor = floor,
                Wall = GetOrCreateMaterial("Wall", lit, new Color(0.72f, 0.72f, 0.74f)),
                Block = GetOrCreateMaterial("Block", lit, new Color(0.55f, 0.57f, 0.6f)),
                Cover = GetOrCreateMaterial("Cover", lit, new Color(0.66f, 0.58f, 0.46f)),
                Platform = GetOrCreateMaterial("Platform", lit, new Color(0.48f, 0.55f, 0.62f)),
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

        /// <summary>新建一个空场景，放好太阳光和环境光。</summary>
        public static Scene NewScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
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
            return scene;
        }

        public static void SaveScene(Scene scene, string sceneName)
        {
            string path = ScenePath(sceneName);
            EditorSceneManager.SaveScene(scene, path);
            UpdateBuildSettings();
        }

        /// <summary>打包列表固定为：靶场（启动场景）、各张地图、移动测试。</summary>
        public static void UpdateBuildSettings()
        {
            string[] order =
            {
                SceneCatalog.RangeScene, SceneCatalog.SiteScene, SceneCatalog.AscentScene, SceneCatalog.AscentAScene,
                SceneCatalog.BindBScene, SceneCatalog.HavenCScene, SceneCatalog.SplitAScene, SceneCatalog.MovementScene,
            };
            var ours = order.Select(ScenePath).ToArray();
            var scenes = order.Select(ScenePath).Where(File.Exists).Select(p => new EditorBuildSettingsScene(p, true)).ToList();
            scenes.AddRange(EditorBuildSettings.scenes.Where(s => !ours.Contains(s.path)));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        public static Transform Group(Transform parent, string name)
        {
            var group = new GameObject(name).transform;
            if (parent != null) group.SetParent(parent, false);
            return group;
        }

        /// <summary>
        /// 按“底面中心 + 尺寸”放一个立方体：x、z 是底面中心，y 是底面高度，w × d 是占地，h 是高度。
        /// </summary>
        public static GameObject Block(Transform parent, string name, float x, float z, float width, float depth,
            float height, Material material, float y = 0f, bool collider = true, bool local = false)
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

        /// <summary>按最小 / 最大坐标放立方体（世界坐标），画地图时比中心点更直观。</summary>
        public static GameObject Box(Transform parent, string name, float xMin, float xMax, float zMin, float zMax,
            float height, Material material, float y = 0f) =>
            Block(parent, name, (xMin + xMax) / 2f, (zMin + zMax) / 2f, xMax - xMin, zMax - zMin, height, material, y);

        /// <summary>地面 + 四周围墙。</summary>
        public static void Shell(Transform parent, float xMin, float xMax, float zMin, float zMax, float wallHeight, Palette p)
        {
            float w = xMax - xMin;
            float d = zMax - zMin;
            float cx = (xMin + xMax) / 2f;
            float cz = (zMin + zMax) / 2f;
            Block(parent, "Floor", cx, cz, w, d, 1f, p.Floor, y: -1f);
            Block(parent, "Wall_North", cx, zMax + 0.5f, w + 2f, 1f, wallHeight, p.Wall);
            Block(parent, "Wall_South", cx, zMin - 0.5f, w + 2f, 1f, wallHeight, p.Wall);
            Block(parent, "Wall_East", xMax + 0.5f, cz, 1f, d, wallHeight, p.Wall);
            Block(parent, "Wall_West", xMin - 0.5f, cz, 1f, d, wallHeight, p.Wall);
        }

        /// <summary>
        /// 斜坡：沿 x 方向从 (lowX, baseY) 升到 (highX, baseY + height)，宽度方向是 z ∈ [zMin, zMax]。
        /// 斜坡下面用台阶状的方块填实，防止从侧面钻到坡底下。坡度不超过 45° 都能直接走上去，蹲着也能走。
        /// </summary>
        public static void RampAlongX(Transform parent, string name, float lowX, float highX, float zMin, float zMax,
            float height, Material material, float baseY = 0f) =>
            Ramp(parent, name, true, lowX, highX, zMin, zMax, height, material, baseY);

        /// <summary>斜坡：沿 z 方向从 (lowZ, baseY) 升到 (highZ, baseY + height)，宽度方向是 x ∈ [xMin, xMax]。</summary>
        public static void RampAlongZ(Transform parent, string name, float lowZ, float highZ, float xMin, float xMax,
            float height, Material material, float baseY = 0f) =>
            Ramp(parent, name, false, lowZ, highZ, xMin, xMax, height, material, baseY);

        static void Ramp(Transform parent, string name, bool alongX, float low, float high, float wMin, float wMax,
            float height, Material material, float baseY)
        {
            float run = high - low;
            float length = Mathf.Sqrt(run * run + height * height);
            float angle = Mathf.Atan2(height, Mathf.Abs(run)) * Mathf.Rad2Deg;
            float rad = angle * Mathf.Deg2Rad;
            const float thickness = 0.4f;
            float dir = Mathf.Sign(run);
            float mid = (low + high) / 2f;
            float wMid = (wMin + wMax) / 2f;

            // 坡面中点在 (mid, baseY + height/2)，坡面法线指向上方并朝低处一侧。
            Vector3 surfaceMid;
            Vector3 normal;
            Quaternion rotation;
            Vector3 scale;
            if (alongX)
            {
                surfaceMid = new Vector3(mid, baseY + height / 2f, wMid);
                normal = new Vector3(-dir * Mathf.Sin(rad), Mathf.Cos(rad), 0f);
                rotation = Quaternion.Euler(0f, 0f, dir * angle);
                scale = new Vector3(length, thickness, wMax - wMin);
            }
            else
            {
                surfaceMid = new Vector3(wMid, baseY + height / 2f, mid);
                normal = new Vector3(0f, Mathf.Cos(rad), -dir * Mathf.Sin(rad));
                rotation = Quaternion.Euler(-dir * angle, 0f, 0f);
                scale = new Vector3(wMax - wMin, thickness, length);
            }

            GameObject ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = name;
            ramp.transform.SetParent(parent, false);
            ramp.transform.position = surfaceMid - normal * (thickness / 2f);
            ramp.transform.rotation = rotation;
            ramp.transform.localScale = scale;
            ramp.GetComponent<Renderer>().sharedMaterial = material;
            GameObjectUtility.SetStaticEditorFlags(ramp, StaticEditorFlags.BatchingStatic);

            const int fillers = 4;
            for (int i = 1; i < fillers; i++)
            {
                float a0 = low + run * i / fillers;
                float a1 = low + run * (i + 1) / fillers;
                float fillHeight = height * i / fillers - 0.05f;
                if (alongX) Box(parent, name + "_Fill" + i, Mathf.Min(a0, a1), Mathf.Max(a0, a1), wMin, wMax, fillHeight, material, baseY);
                else Box(parent, name + "_Fill" + i, wMin, wMax, Mathf.Min(a0, a1), Mathf.Max(a0, a1), fillHeight, material, baseY);
            }
        }

        /// <summary>说明区域：玩家走进去时调试面板显示标题和说明。</summary>
        public static Transform Zone(Transform level, string title, Vector3 center, Vector3 size, string description)
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

        /// <summary>
        /// 放一个假人点位。facingYaw 是朝向（0 = +Z 北，90 = +X 东，180 = 南，270 = 西）。
        /// worldPeekDelta 是探头位移（世界坐标），会自动换算成点位的本地坐标。
        /// </summary>
        public static BotSpawnPoint Point(Transform parent, string label, Vector3 feet, float facingYaw, BotBehavior behavior,
            Vector3 worldPeekDelta = default, float strafeWidth = 3f, bool prefire = true, bool canShoot = true)
        {
            var go = new GameObject("BotPoint_" + label);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(feet, Quaternion.Euler(0f, facingYaw, 0f));
            Vector3 localPeek = Quaternion.Inverse(go.transform.rotation) * worldPeekDelta;
            var point = go.AddComponent<BotSpawnPoint>();
            point.Configure(behavior, strafeWidth, localPeek, prefire, canShoot, label);
            return point;
        }

        /// <summary>地图区域（俯视图上的矩形），玩家走进去时小地图下方显示名称。</summary>
        public static MapRegion Region(Transform parent, string label, float xMin, float xMax, float zMin, float zMax)
        {
            var go = new GameObject("Region_" + label);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3((xMin + xMax) / 2f, 0f, (zMin + zMax) / 2f);
            var region = go.AddComponent<MapRegion>();
            region.Configure(label, new Vector2(xMin, zMin), new Vector2(xMax, zMax));
            return region;
        }

        /// <summary>预瞄路线：start / yaw 是玩家出发点，points 是沿途要清的点位（按顺序）。</summary>
        public static PrefireRoute Route(Transform parent, string label, Vector3 start, float yaw, BotSpawnPoint[] points)
        {
            var go = new GameObject("Route_" + label);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(start, Quaternion.Euler(0f, yaw, 0f));
            var route = go.AddComponent<PrefireRoute>();
            route.Configure(label, points);
            return route;
        }

        /// <summary>架枪位置：position / yaw 是玩家守的位置。入口点位用 Point 放在返回的 Transform 下面。</summary>
        public static Transform Hold(Transform parent, string label, Vector3 position, float yaw)
        {
            var go = new GameObject("Hold_" + label);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            go.AddComponent<HoldSpot>().Configure(label);
            return go.transform;
        }

        public static BotSpawnArea Area(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            var area = go.AddComponent<BotSpawnArea>();
            area.Configure(size);
            return area;
        }

        // ---------- 玩家 ----------

        /// <summary>
        /// 创建玩家（移动、视角、武器、生命值、音效、调试面板）。
        /// withTraining 为 true 时同时创建训练系统（菜单、HUD、刷新假人），否则只是能走能开枪的测试场景。
        /// </summary>
        public static PlayerRig CreatePlayer(Vector3 spawn, float yaw, bool withTraining)
        {
            MovementSettings movement = LoadOrCreateMovementSettings();
            WeaponDefinition weaponDefinition = LoadOrCreateWeapon();
            MovementTuning t = movement.tuning;

            var spawnMarker = new GameObject("PlayerSpawn").transform;
            spawnMarker.SetPositionAndRotation(spawn, Quaternion.Euler(0f, yaw, 0f));

            var player = new GameObject("Player");
            player.transform.SetPositionAndRotation(spawn, Quaternion.Euler(0f, yaw, 0f));

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

            var controller = player.AddComponent<PlayerController>();
            controller.Configure(movement, camera);
            controller.ManageCursor = !withTraining;

            player.AddComponent<Health>().Configure(PlayerHealth);
            player.AddComponent<AudioSource>().playOnAwake = false;
            var audio = player.AddComponent<PlayerAudio>();

            var viewmodelObject = new GameObject("Viewmodel");
            viewmodelObject.transform.SetParent(cameraObject.transform, false);
            var viewmodel = viewmodelObject.AddComponent<Viewmodel>();
            viewmodel.Bind(controller);

            var weapon = player.AddComponent<HitscanWeapon>();
            weapon.Configure(weaponDefinition, controller, viewmodel, audio);

            var debugHud = player.AddComponent<MovementDebugHud>();
            debugHud.Bind(controller);
            debugHud.Visible = !withTraining;

            var rig = new PlayerRig { Player = player, Controller = controller, Spawn = spawnMarker };

            var systems = new GameObject(withTraining ? "Training" : "Systems");
            var performance = systems.AddComponent<PerformanceSettings>();
            if (withTraining)
            {
                var session = systems.AddComponent<TrainingSession>();
                session.Configure(controller, weapon, player.GetComponent<Health>(), audio, spawnMarker, performance,
                    debugHud);
                systems.AddComponent<TrainingHud>().Bind(session);
                systems.AddComponent<TrainingMenu>().Bind(session);
                rig.Session = session;
            }
            return rig;
        }
    }
}
