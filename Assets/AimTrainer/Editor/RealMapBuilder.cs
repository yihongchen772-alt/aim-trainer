using System.IO;
using AimTrainer.Bots;
using AimTrainer.Game;
using AimTrainer.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AimTrainer.EditorTools
{
    /// <summary>
    /// 用真实地图模型（glb，由 tools/realmaps/fetch.py 准备）生成训练场景：
    /// 画面模型只负责显示，碰撞模型加上 MeshCollider 后隐藏，负责走路、子弹和视线判定；
    /// 再放上玩家、预瞄路线、架枪位置、官方地名和官方小地图。
    ///
    /// glb 由 glTFast 包导入（Packages/manifest.json 里的 com.unity.cloud.gltfast）。
    /// glTF 是右手坐标系，导入 Unity 时会镜像一个轴；这里根据碰撞模型导入后的位置自动判断镜像的是 X 还是 Z，
    /// 训练数据（RealMapData，glTF 原始坐标）按同样的方式换算。
    /// </summary>
    public static class RealMapBuilder
    {
        public readonly struct Spot
        {
            public readonly string Name;
            public readonly Vector3 Position;
            public readonly Vector3 Face;

            public Spot(string name, Vector3 position, Vector3 face)
            {
                Name = name;
                Position = position;
                Face = face;
            }
        }

        public readonly struct Area
        {
            public readonly Vector3 Center;
            public readonly float HalfSize;

            public Area(Vector3 center, float halfSize)
            {
                Center = center;
                HalfSize = halfSize;
            }
        }

        public readonly struct Callout
        {
            public readonly string Name;
            public readonly float X;
            public readonly float Z;

            public Callout(string name, float x, float z)
            {
                Name = name;
                X = x;
                Z = z;
            }
        }

        public readonly struct Route
        {
            public readonly string Name;
            public readonly Spot Start;
            public readonly Spot[] Points;

            public Route(string name, Spot start, Spot[] points)
            {
                Name = name;
                Start = start;
                Points = points;
            }
        }

        /// <summary>架枪入口：假人在 Position 等着，然后冲到 To。</summary>
        public readonly struct Entry
        {
            public readonly string Name;
            public readonly Vector3 Position;
            public readonly Vector3 To;

            public Entry(string name, Vector3 position, Vector3 to)
            {
                Name = name;
                Position = position;
                To = to;
            }
        }

        public readonly struct Hold
        {
            public readonly string Name;
            public readonly Spot Spot;
            public readonly Entry[] Entries;

            public Hold(string name, Spot spot, Entry[] entries)
            {
                Name = name;
                Spot = spot;
                Entries = entries;
            }
        }

        public sealed class MapDefinition
        {
            public string Scene;
            public string Folder;
            public string Visual;
            public string Collision;
            public string Minimap;

            /// <summary>碰撞模型在 glTF 坐标里的俯视中心 (x, z)，用来判断导入时镜像了哪个轴。</summary>
            public Vector2 CollisionCenterXZ;

            /// <summary>官方小地图换算：u = 游戏 Y × MinimapX.x + MinimapX.y，v = 游戏 X × MinimapY.x + MinimapY.y（游戏坐标是 glTF 坐标 × 100）。</summary>
            public Vector2 MinimapX;
            public Vector2 MinimapY;

            public Spot Spawn;
            public Spot AimSpawn;
            public Area AimArea;
            public Callout[] Callouts;
            public Route[] Routes;
            public Hold[] Holds;
        }

        /// <summary>地名的作用半径（米）：离最近的地名超过这个距离时不显示区域名称。</summary>
        const float CalloutRadius = 22f;

        /// <summary>模型文件都已经由 glTFast 导入完成（文件存在但还没导入时返回 false）。</summary>
        public static bool AssetsReady(MapDefinition map) =>
            AssetDatabase.LoadAssetAtPath<GameObject>(map.Folder + "/" + map.Visual) != null &&
            AssetDatabase.LoadAssetAtPath<GameObject>(map.Folder + "/" + map.Collision) != null;

        public static bool BuildAscentReal() => Build(RealMapData.Ascent);

        public static bool Build(MapDefinition map)
        {
            var visualAsset = AssetDatabase.LoadAssetAtPath<GameObject>(map.Folder + "/" + map.Visual);
            var collisionAsset = AssetDatabase.LoadAssetAtPath<GameObject>(map.Folder + "/" + map.Collision);
            var minimap = AssetDatabase.LoadAssetAtPath<Texture2D>(map.Folder + "/" + map.Minimap);
            if (visualAsset == null || collisionAsset == null)
            {
                bool filesExist = File.Exists(map.Folder + "/" + map.Visual) && File.Exists(map.Folder + "/" + map.Collision);
                Debug.LogWarning(filesExist
                    ? $"[AimTrainer] {map.Scene}：地图模型还没有导入成功。请确认 Package Manager 里装好了 glTFast（com.unity.cloud.gltfast），然后用 AimTrainer → 生成缺少的场景。"
                    : $"[AimTrainer] {map.Scene}：缺少地图模型文件（{map.Folder}），请先运行 python3 tools/realmaps/fetch.py。");
                return false;
            }

            SceneKit.EnsureFolders();
            Scene scene = SceneKit.NewScene();
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.white;
            Transform level = SceneKit.Group(null, "Level");

            // 画面模型：只显示，不参与碰撞。
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(visualAsset);
            visual.name = "MapVisual";
            visual.transform.SetParent(level, false);
            foreach (Collider c in visual.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (Renderer r in visual.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }

            // 碰撞模型：每个网格加 MeshCollider，然后去掉渲染（看不见，但走路、子弹、视线都用它）。
            var collision = (GameObject)PrefabUtility.InstantiatePrefab(collisionAsset);
            PrefabUtility.UnpackPrefabInstance(collision, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            collision.name = "MapCollision";
            collision.transform.SetParent(level, false);
            bool mirrorX = DetectMirrorX(collision, map.CollisionCenterXZ);
            foreach (MeshFilter filter in collision.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer != null) Object.DestroyImmediate(renderer);
            }
            foreach (Transform t in collision.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
            }

            Vector3 ToUnity(Vector3 p) => mirrorX ? new Vector3(-p.x, p.y, p.z) : new Vector3(p.x, p.y, -p.z);
            float YawTowards(Vector3 from, Vector3 to)
            {
                Vector3 d = ToUnity(to) - ToUnity(from);
                return d.x * d.x + d.z * d.z < 1e-6f ? 0f : Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            }

            // 玩家和训练系统。
            Spot spawn = map.Spawn;
            SceneKit.PlayerRig rig = SceneKit.CreatePlayer(ToUnity(spawn.Position), YawTowards(spawn.Position, spawn.Face), withTraining: true);
            var camera = rig.Player.GetComponentInChildren<Camera>();
            if (camera != null)
            {
                camera.farClipPlane = 1500f;
                camera.backgroundColor = new Color(0.62f, 0.7f, 0.8f);
            }
            var aimSpawn = new GameObject("AimModeSpawn").transform;
            aimSpawn.SetPositionAndRotation(ToUnity(map.AimSpawn.Position),
                Quaternion.Euler(0f, YawTowards(map.AimSpawn.Position, map.AimSpawn.Face), 0f));
            rig.Session.SetAimSpawn(aimSpawn);

            // 假人点位、预瞄路线、架枪位置。
            Transform bots = SceneKit.Group(null, "Bots");
            Transform routes = SceneKit.Group(null, "Routes");
            foreach (Route route in map.Routes)
            {
                var points = new BotSpawnPoint[route.Points.Length];
                for (int i = 0; i < points.Length; i++)
                {
                    Spot p = route.Points[i];
                    points[i] = SceneKit.Point(bots, p.Name, ToUnity(p.Position), YawTowards(p.Position, p.Face), BotBehavior.Static);
                }
                SceneKit.Route(routes, route.Name, ToUnity(route.Start.Position), YawTowards(route.Start.Position, route.Start.Face), points);
            }
            foreach (Hold hold in map.Holds)
            {
                Transform holdRoot = SceneKit.Hold(bots, hold.Name, ToUnity(hold.Spot.Position), YawTowards(hold.Spot.Position, hold.Spot.Face));
                foreach (Entry entry in hold.Entries)
                {
                    // 冲出的假人朝向架枪位置，冲出来时玩家正好在它的视野中间。
                    SceneKit.Point(holdRoot, entry.Name, ToUnity(entry.Position), YawTowards(entry.To, hold.Spot.Position),
                        BotBehavior.Swing, ToUnity(entry.To) - ToUnity(entry.Position), 3f, prefire: false);
                }
            }
            Area area = map.AimArea;
            SceneKit.Area(bots, "SiteSpawnArea", ToUnity(area.Center) + Vector3.up * 2f,
                new Vector3(area.HalfSize * 2f, 8f, area.HalfSize * 2f));

            // 官方地名（小地图下方显示所在区域）。
            Transform regions = SceneKit.Group(null, "Regions");
            foreach (Callout callout in map.Callouts)
            {
                var go = new GameObject("Region_" + callout.Name);
                go.transform.SetParent(regions, false);
                go.transform.position = ToUnity(new Vector3(callout.X, 0f, callout.Z));
                go.AddComponent<MapRegion>().ConfigurePoint(callout.Name, CalloutRadius);
            }

            // 官方小地图：u = 100·MinimapX.x·gz + MinimapX.y，v = 100·MinimapY.x·gx + MinimapY.y（gx、gz 是 glTF 坐标）。
            if (minimap != null)
            {
                float ku = 100f * map.MinimapX.x;
                float kv = 100f * map.MinimapY.x;
                // Unity 坐标 → glTF：镜像 X 时 gx = -x、gz = z；镜像 Z 时 gx = x、gz = -z。
                Vector3 u = mirrorX ? new Vector3(0f, ku, map.MinimapX.y) : new Vector3(0f, -ku, map.MinimapX.y);
                Vector3 v = mirrorX ? new Vector3(-kv, 0f, map.MinimapY.y) : new Vector3(kv, 0f, map.MinimapY.y);
                new GameObject("Minimap").AddComponent<MinimapImage>().Configure(minimap, u, v);
            }

            SceneKit.SaveScene(scene, map.Scene);
            Debug.Log($"[AimTrainer] 已生成真实地图场景 {map.Scene}（导入时镜像了 {(mirrorX ? "X" : "Z")} 轴）。");
            return true;
        }

        /// <summary>根据碰撞模型导入后的俯视中心，判断 glTF → Unity 时镜像的是 X 轴还是 Z 轴。</summary>
        static bool DetectMirrorX(GameObject collision, Vector2 gltfCenter)
        {
            Bounds bounds = default;
            bool any = false;
            foreach (Renderer r in collision.GetComponentsInChildren<Renderer>(true))
            {
                if (!any) bounds = r.bounds;
                else bounds.Encapsulate(r.bounds);
                any = true;
            }
            if (!any) return true;
            Vector3 c = bounds.center;
            float mirrorXError = Mathf.Abs(c.x + gltfCenter.x) + Mathf.Abs(c.z - gltfCenter.y);
            float mirrorZError = Mathf.Abs(c.x - gltfCenter.x) + Mathf.Abs(c.z + gltfCenter.y);
            if (Mathf.Min(mirrorXError, mirrorZError) > 5f)
            {
                Debug.LogWarning($"[AimTrainer] 碰撞模型导入后的位置和预期不符（中心 {c}），点位可能对不上。");
            }
            return mirrorXError <= mirrorZError;
        }
    }
}
