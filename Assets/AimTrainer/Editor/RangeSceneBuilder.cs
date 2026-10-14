using AimTrainer.Bots;
using AimTrainer.Game;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AimTrainer.EditorTools
{
    /// <summary>
    /// 靶场：70 × 90 米的开阔场地，散布着不同高度的掩体和两座高台。
    /// 定点甩枪 / 移动靶在北边的大片区域里随机刷新；对枪 / 预瞄用掩体旁边的固定点位。
    /// 从出生点往北每 10 米一条橙色距离线。
    /// </summary>
    public static class RangeSceneBuilder
    {
        const float XMin = -35f;
        const float XMax = 35f;
        const float ZMin = -30f;
        const float ZMax = 60f;
        static readonly Vector3 Spawn = new Vector3(0f, 0f, -24f);

        public static void Build()
        {
            SceneKit.EnsureFolders();
            SceneKit.Palette p = SceneKit.CreatePalette(SceneCatalog.RangeScene, new Vector2(XMax - XMin, ZMax - ZMin));
            Scene scene = SceneKit.NewScene();

            Transform level = SceneKit.Group(null, "Level");
            SceneKit.Shell(level, XMin, XMax, ZMin, ZMax, 6f, p);

            Transform markers = SceneKit.Group(level, "DistanceLines");
            for (int meters = 10; Spawn.z + meters < ZMax; meters += 10)
            {
                SceneKit.Block(markers, meters + "m", 0f, Spawn.z + meters, XMax - XMin, 0.12f, 0.01f, p.Marker, collider: false);
            }

            Transform cover = SceneKit.Group(level, "Cover");
            // 名字里的数字是掩体高度：1.4 米蹲下能完全躲住、站起来能露头开枪；0.85 米能跳上去；2.6 米以上完全挡住。
            SceneKit.Block(cover, "Crouch_1.4_A", -12f, 6f, 3f, 3f, 1.4f, p.Cover);
            SceneKit.Block(cover, "Tall_2.6_A", 10f, 10f, 4f, 1f, 2.6f, p.Block);
            SceneKit.Block(cover, "Half_1.0_A", -20f, 22f, 2f, 6f, 1f, p.Cover);
            SceneKit.Block(cover, "Crouch_1.4_B", 0f, 20f, 6f, 1f, 1.4f, p.Cover);
            SceneKit.Block(cover, "Jump_0.85_A", 18f, 28f, 3f, 3f, 0.85f, p.Cover);
            SceneKit.Block(cover, "Tall_2.6_B", -8f, 34f, 2f, 2f, 2.6f, p.Block);
            SceneKit.Block(cover, "Crouch_1.4_C", 8f, 40f, 5f, 1f, 1.4f, p.Cover);
            SceneKit.Block(cover, "Crouch_1.4_D", -24f, 44f, 3f, 3f, 1.4f, p.Cover);
            SceneKit.Block(cover, "Tall_2.6_C", 22f, 46f, 2f, 4f, 2.6f, p.Block);
            SceneKit.Block(cover, "Wall_3.0", 0f, 50f, 8f, 1f, 3f, p.Wall);
            SceneKit.Block(cover, "Platform_2.4", -26f, 54f, 12f, 6f, 2.4f, p.Platform);
            SceneKit.Block(cover, "Platform_3.2", 24f, 54f, 10f, 6f, 3.2f, p.Platform);

            Transform bots = SceneKit.Group(null, "Bots");
            // 点位和刷新区域只要放进场景，训练开始时会自动找到。南边的点位面朝出生点（180° = 朝南）。
            SceneKit.Point(bots, "蹲掩体后", new Vector3(0f, 0f, 21.2f), 180f, BotBehavior.Static);
            SceneKit.Point(bots, "高墙右侧探头", new Vector3(10f, 0f, 11.2f), 180f, BotBehavior.Peek, new Vector3(3.4f, 0f, 0f));
            SceneKit.Point(bots, "半身掩体", new Vector3(-20f, 0f, 25.6f), 165f, BotBehavior.Static);
            SceneKit.Point(bots, "柱子左侧探头", new Vector3(-8f, 0f, 35.6f), 180f, BotBehavior.Peek, new Vector3(-2.6f, 0f, 0f));
            SceneKit.Point(bots, "中场横跳", new Vector3(10f, 0f, 30f), 190f, BotBehavior.Strafe, strafeWidth: 5f);
            SceneKit.Point(bots, "远处掩体", new Vector3(8f, 0f, 41.2f), 180f, BotBehavior.Static);
            SceneKit.Point(bots, "左高台", new Vector3(-26f, 2.4f, 54f), 160f, BotBehavior.Static);
            SceneKit.Point(bots, "右高台", new Vector3(24f, 3.2f, 54f), 200f, BotBehavior.Static);
            SceneKit.Point(bots, "高墙后探头", new Vector3(22f, 0f, 48.8f), 190f, BotBehavior.Peek, new Vector3(-2f, 0f, 0f));
            SceneKit.Area(bots, "RandomSpawnArea", new Vector3(0f, 3.5f, 25f), new Vector3(66f, 7f, 56f));

            SceneKit.CreatePlayer(Spawn, 0f, withTraining: true);
            SceneKit.SaveScene(scene, SceneCatalog.RangeScene);
        }
    }
}
