using AimTrainer.Bots;
using AimTrainer.Game;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AimTrainer.EditorTools
{
    /// <summary>
    /// A 点（原创白模）：一个典型的“进攻方攻点”局部场景，结构参考常见战术射击地图，但几何体全部原创。
    ///
    /// 俯视图（x 向右为东，z 向上为北，单位米）：
    ///
    ///   z=42 ┌──────────────────────────────────────────┐
    ///        │  实心      │ 实心 │CT通道│ 实心 │  实心   │
    ///   z=30 │──高台（高 3.2，南沿有 1.35 栏杆）──┐    │      │
    ///        │                     斜坡↗         │      │  实心
    ///        │      A 点包点区（箱子、发电机、柱子）   │
    ///   z=0  │ 实心 │A 大│     实心建筑     │连接│  实心  │
    ///        │      │    │                  │    │        │
    ///   z=-32│──────┘    └──────────────────┘    └────────│
    ///        │            进攻方出生院子                    │
    ///   z=-48└──────────────────────────────────────────┘
    ///       x=-32                                          x=12
    ///
    /// 尺寸标准（和 docs/phase4-greybox.md 一致）：墙高 6 米；A 大宽 7 米、连接宽 5 米；
    /// 蹲掩体 1.4 米（蹲下完全躲住、站起来能露头）；跳箱 0.85 米；半身掩体 1.0 米；高掩体 2.6 米；高台 3.2 米。
    /// </summary>
    public static class SiteSceneBuilder
    {
        const float XMin = -32f;
        const float XMax = 12f;
        const float ZMin = -48f;
        const float ZMax = 42f;
        const float WallHeight = 6f;
        const float HeavenHeight = 3.2f;
        static readonly Vector3 Spawn = new Vector3(-10f, 0f, -44f);
        static readonly Vector3 AimSpawn = new Vector3(-12f, 0f, 2f);

        public static void Build()
        {
            SceneKit.EnsureFolders();
            SceneKit.Palette p = SceneKit.CreatePalette(SceneCatalog.SiteScene, new Vector2(XMax - XMin, ZMax - ZMin));
            Scene scene = SceneKit.NewScene();

            Transform level = SceneKit.Group(null, "Level");
            SceneKit.Shell(level, XMin, XMax, ZMin, ZMax, WallHeight, p);
            BuildBuildings(SceneKit.Group(level, "Buildings"), p);
            BuildSite(SceneKit.Group(level, "Site"), p);
            BuildApproaches(SceneKit.Group(level, "Approaches"), p);

            Transform bots = SceneKit.Group(null, "Bots");
            BuildBotPoints(bots);
            // 包点区（含高台和箱子顶）随机刷新，用于定点甩枪和移动靶。
            SceneKit.Area(bots, "SiteSpawnArea", new Vector3(-13f, 3.5f, 15f), new Vector3(36f, 7f, 28f));

            SceneKit.PlayerRig rig = SceneKit.CreatePlayer(Spawn, 0f, withTraining: true);

            // 定点甩枪和移动靶在包点区随机刷新，离进攻方出生点太远；这两个模式直接从包点入口出生。
            var aimSpawn = new GameObject("AimModeSpawn").transform;
            aimSpawn.SetPositionAndRotation(AimSpawn, Quaternion.identity);
            rig.Session.SetAimSpawn(aimSpawn);

            SceneKit.SaveScene(scene, SceneCatalog.SiteScene);
        }

        /// <summary>实心的建筑体块，围出 A 大、连接和 CT 通道。</summary>
        static void BuildBuildings(Transform parent, SceneKit.Palette p)
        {
            SceneKit.Box(parent, "WestOfMain", -32f, -28f, -32f, 0f, WallHeight, p.Wall);
            SceneKit.Box(parent, "BetweenMainAndLink", -21f, -8f, -32f, 0f, WallHeight, p.Wall);
            SceneKit.Box(parent, "EastOfLink", -3f, 12f, -32f, 0f, WallHeight, p.Wall);
            SceneKit.Box(parent, "EastOfSite", 6f, 12f, 0f, 42f, WallHeight, p.Wall);
            SceneKit.Box(parent, "BehindHeaven", -32f, -14f, 30f, 42f, WallHeight, p.Wall);
            SceneKit.Box(parent, "CT_Left", -14f, -6f, 30f, 42f, WallHeight, p.Wall);
            SceneKit.Box(parent, "CT_Right", 0f, 6f, 30f, 42f, WallHeight, p.Wall);
        }

        static void BuildSite(Transform parent, SceneKit.Palette p)
        {
            // 高台：3.2 米高，南沿 1.35 米栏杆（蹲下完全躲住，站起来能架枪），中间留 4 米宽的窗口。
            SceneKit.Box(parent, "Heaven", -32f, -14f, 24f, 30f, HeavenHeight, p.Platform);
            SceneKit.Box(parent, "Heaven_Rail_West", -32f, -24f, 24f, 24.3f, 1.35f, p.Cover, HeavenHeight);
            SceneKit.Box(parent, "Heaven_Rail_East", -20f, -14f, 24f, 24.3f, 1.35f, p.Cover, HeavenHeight);
            // 斜坡上高台（约 22°），蹲着也能走上去。
            SceneKit.RampAlongX(parent, "Heaven_Ramp", -6f, -14f, 24.3f, 27.3f, HeavenHeight, p.Platform);

            SceneKit.Box(parent, "Generator_2.6", -22f, -18f, 10f, 13f, 2.6f, p.Block);
            SceneKit.Box(parent, "Boxes_1.4", -10f, -7f, 14f, 17f, 1.4f, p.Cover);
            // 箱堆北半边再叠一层（总高 2.4 米），后面的探头假人才真正藏得住。
            SceneKit.Box(parent, "Boxes_Top_1.0", -10f, -7f, 15.5f, 17f, 1f, p.Cover, 1.4f);
            SceneKit.Box(parent, "JumpBox_0.85", -27f, -25.5f, 3f, 4.5f, 0.85f, p.Cover);
            SceneKit.Box(parent, "PlantCover_1.0", -2f, 2f, 19f, 23f, 1f, p.Cover);
            SceneKit.Box(parent, "Pillar_4.0", -29f, -27f, 16f, 18f, 4f, p.Block);
            SceneKit.Box(parent, "Elbow_4.0", -21f, -19.5f, 0f, 5f, 4f, p.Wall);

            // 包点标记（地面上的橙色方框，没有碰撞）。
            SceneKit.Block(parent, "PlantMark_N", -6f, 23.5f, 8f, 0.15f, 0.01f, p.Marker, collider: false);
            SceneKit.Block(parent, "PlantMark_S", -6f, 17.5f, 8f, 0.15f, 0.01f, p.Marker, collider: false);
            SceneKit.Block(parent, "PlantMark_W", -10f, 20.5f, 0.15f, 6f, 0.01f, p.Marker, collider: false);
            SceneKit.Block(parent, "PlantMark_E", -2f, 20.5f, 0.15f, 6f, 0.01f, p.Marker, collider: false);
        }

        static void BuildApproaches(Transform parent, SceneKit.Palette p)
        {
            SceneKit.Box(parent, "Main_Crouch_1.4", -27f, -25.5f, -14f, -12.5f, 1.4f, p.Cover);
            SceneKit.Box(parent, "Link_Half_1.0", -8f, -6.5f, -10f, -8.5f, 1f, p.Cover);
            SceneKit.Box(parent, "Yard_Crouch_1.4", -11.5f, -8.5f, -41.5f, -38.5f, 1.4f, p.Cover);
            SceneKit.Box(parent, "Yard_Tall_2.6", 4f, 6f, -44f, -40f, 2.6f, p.Block);
        }

        /// <summary>防守方常见站位。预瞄模式会同时放上所有标记为“预瞄”的点位。</summary>
        static void BuildBotPoints(Transform bots)
        {
            float h = HeavenHeight;
            // A 大：进点路上第一个要清的位置。
            SceneKit.Point(bots, "A大深处", new Vector3(-24.5f, 0f, -6f), 180f, BotBehavior.Static);
            SceneKit.Point(bots, "连接深处", new Vector3(-5.5f, 0f, -14f), 180f, BotBehavior.Static, prefire: false);

            // 包点区。
            SceneKit.Point(bots, "肘角", new Vector3(-18.8f, 0f, 3f), 20f, BotBehavior.Static);
            SceneKit.Point(bots, "发电机后探头", new Vector3(-20f, 0f, 14.6f), 200f, BotBehavior.Peek, new Vector3(-3.5f, 0f, 0f));
            SceneKit.Point(bots, "箱子后探头", new Vector3(-8.5f, 0f, 18.4f), 180f, BotBehavior.Peek, new Vector3(2.6f, 0f, 0f));
            SceneKit.Point(bots, "柱子后探头", new Vector3(-28f, 0f, 19.2f), 160f, BotBehavior.Peek, new Vector3(1.8f, 0f, 0f));
            SceneKit.Point(bots, "包点后角", new Vector3(-30.5f, 0f, 22.5f), 135f, BotBehavior.Static);
            SceneKit.Point(bots, "下包位", new Vector3(0f, 0f, 24.6f), 200f, BotBehavior.Static);
            SceneKit.Point(bots, "连接出口", new Vector3(3f, 0f, 4f), 225f, BotBehavior.Static);

            // 高台和 CT 通道。
            SceneKit.Point(bots, "高台窗口", new Vector3(-22f, h, 27f), 180f, BotBehavior.Static);
            SceneKit.Point(bots, "高台坡口", new Vector3(-15.5f, h, 28.5f), 150f, BotBehavior.Static);
            SceneKit.Point(bots, "CT通道", new Vector3(-3f, 0f, 34f), 180f, BotBehavior.Strafe, strafeWidth: 4f);
        }
    }
}
