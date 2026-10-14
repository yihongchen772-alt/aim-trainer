using AimTrainer.Game;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AimTrainer.EditorTools
{
    /// <summary>
    /// 第一阶段的移动测试场景：速度跑道、台阶、跳台、下蹲通道和急停靶场。
    /// 玩家走进每个区域时调试面板会显示测试方法。这个场景没有训练菜单，鼠标由玩家控制器自己管理。
    /// </summary>
    public static class MovementTestSceneBuilder
    {
        const float FloorSize = 80f;
        const float WallHeight = 6f;
        static readonly Vector3 Spawn = new Vector3(0f, 0f, -32f);

        public static void Build()
        {
            SceneKit.EnsureFolders();
            SceneKit.Palette palette = SceneKit.CreatePalette(SceneCatalog.MovementScene, new Vector2(FloorSize, FloorSize));
            Scene scene = SceneKit.NewScene();

            Transform level = SceneKit.Group(null, "Level");
            float half = FloorSize / 2f;
            SceneKit.Shell(level, -half, half, -half, half, WallHeight, palette);
            BuildSpeedLane(level, palette);
            BuildStepTest(level, palette);
            BuildJumpTest(level, palette);
            BuildCrouchTunnel(level, palette);
            BuildStrafeRange(level, palette);

            SceneKit.CreatePlayer(Spawn, 0f, withTraining: false);
            SceneKit.SaveScene(scene, SceneCatalog.MovementScene);
        }

        static void BuildSpeedLane(Transform level, SceneKit.Palette p)
        {
            Transform zone = SceneKit.Zone(level, "速度跑道", new Vector3(-26f, 0f, 0f), new Vector3(8f, 4f, 72f),
                "直线跑道，每 5 米一条橙色横线。全速跑完相邻两条线约需 0.93 秒（5.4 米/秒）。按住 Shift 静步约 1.8 秒。");
            for (int i = -6; i <= 6; i++)
            {
                SceneKit.Block(zone, "Line_" + (i * 5), 0f, i * 5f, 8f, 0.12f, 0.01f, p.Marker, collider: false, local: true);
            }
        }

        static void BuildStepTest(Transform level, SceneKit.Palette p)
        {
            Transform zone = SceneKit.Zone(level, "台阶测试", new Vector3(-12f, 0f, -10f), new Vector3(10f, 4f, 22f),
                "四个台子高 0.20 / 0.35 / 0.55 / 0.70 米。0.40 米以下可以直接走上去，更高的需要跳。从台子上走下来不会短暂进入“空中”状态。");
            float[] heights = { 0.2f, 0.35f, 0.55f, 0.7f };
            for (int i = 0; i < heights.Length; i++)
            {
                SceneKit.Block(zone, "Step_" + heights[i].ToString("0.00"), 0f, -7.5f + i * 5f, 3f, 2.5f, heights[i], p.Block,
                    local: true);
            }
        }

        static void BuildJumpTest(Transform level, SceneKit.Palette p)
        {
            Transform zone = SceneKit.Zone(level, "跳跃测试", new Vector3(-12f, 0f, 16f), new Vector3(10f, 4f, 22f),
                "四个台子高 0.70 / 0.85 / 1.10 / 1.25 米。跳高 0.9 米：前两个能跳上去，后两个跳不上。");
            float[] heights = { 0.7f, 0.85f, 1.1f, 1.25f };
            for (int i = 0; i < heights.Length; i++)
            {
                SceneKit.Block(zone, "Ledge_" + heights[i].ToString("0.00"), 0f, -7.5f + i * 5f, 3f, 2.5f, heights[i], p.Block,
                    local: true);
            }
        }

        static void BuildCrouchTunnel(Transform level, SceneKit.Palette p)
        {
            Transform zone = SceneKit.Zone(level, "下蹲通道", new Vector3(4f, 0f, 0f), new Vector3(6f, 4f, 14f),
                "顶高 1.45 米，宽 2.4 米。站着进不去，按住 Ctrl 或 C 蹲下才能通过；在里面松开下蹲不会站起来。");
            const float length = 10f;
            const float innerWidth = 2.4f;
            const float ceiling = 1.45f;
            SceneKit.Block(zone, "Tunnel_Left", -innerWidth / 2f - 0.25f, 0f, 0.5f, length, 2.2f, p.Wall, local: true);
            SceneKit.Block(zone, "Tunnel_Right", innerWidth / 2f + 0.25f, 0f, 0.5f, length, 2.2f, p.Wall, local: true);
            SceneKit.Block(zone, "Tunnel_Roof", 0f, 0f, innerWidth + 1f, length, 0.3f, p.Block, y: ceiling, local: true);
        }

        static void BuildStrafeRange(Transform level, SceneKit.Palette p)
        {
            Transform zone = SceneKit.Zone(level, "急停靶场", new Vector3(24f, 0f, 0f), new Vector3(28f, 4f, 72f),
                "对着前方的红色靶子按 A / D 左右横跳：松开一个键的同时按另一个（两个键同时按住等于松键）。速度条变绿时就是可以精准射击的时刻，看左上角“反向急停”的耗时。");
            float[] distances = { 8f, 15f, 25f };
            float[] offsets = { -6f, 0f, 6f };
            foreach (float distance in distances)
            {
                foreach (float offset in offsets)
                {
                    Target(zone, new Vector3(offset, 0f, -10f + distance), p.Target);
                }
            }
            SceneKit.Block(zone, "StartLine", 0f, -12f, 20f, 0.12f, 0.01f, p.Marker, collider: false, local: true);
        }

        static void Target(Transform parent, Vector3 localFeet, Material material)
        {
            var target = new GameObject("Target").transform;
            target.SetParent(parent, false);
            target.localPosition = localFeet;
            // 身体到 1.45 米，头部中心在 1.6 米、直径 0.3 米，和假人一致。
            SceneKit.Block(target, "Body", 0f, 0f, 0.5f, 0.3f, 1.45f, material, local: true);
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(target, false);
            head.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            head.transform.localScale = Vector3.one * 0.3f;
            head.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
