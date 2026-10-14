namespace AimTrainer.Game
{
    /// <summary>可以在菜单里切换的训练场景。场景文件由编辑器菜单 AimTrainer → 生成全部场景 创建。</summary>
    public static class SceneCatalog
    {
        public readonly struct Entry
        {
            public readonly string SceneName;
            public readonly string Title;
            public readonly string Description;

            public Entry(string sceneName, string title, string description)
            {
                SceneName = sceneName;
                Title = title;
                Description = description;
            }
        }

        public const string RangeScene = "Range";
        public const string SiteScene = "ASite";
        public const string MovementScene = "MovementTest";
        public const string AscentScene = "Ascent";
        public const string AscentRealScene = "AscentReal";
        public const string AscentAScene = "AscentA";
        public const string BindBScene = "BindB";
        public const string HavenCScene = "HavenC";
        public const string SplitAScene = "SplitA";

        public static readonly Entry[] Training =
        {
            new Entry(RangeScene, "靶场", "开阔场地，少量掩体，适合甩枪和跟枪。"),
            new Entry(AscentRealScene, "亚海悬城（真实地图）", "游戏里的真实地图模型和官方小地图（素材归 Riot Games 所有）。预瞄可选 A 大、中路、B 大三条路线；架枪可选 A 点、B 侧道、中路坡底三个位置。点位都按真实地形检查过。"),
            new Entry(SiteScene, "A 点（原创）", "原创白模：大道、连接、高台、包点箱体，适合对枪和预瞄清点。"),
            new Entry(AscentScene, "亚海悬城（整图）", "仿亚海悬城整图：A / 中路 / B 三路共 24 个区域，有小地图区域名称。预瞄可选 A 大、中路、B 大三条路线；架枪可选 A 点、B 点、中路顶三个位置。布局为近似复刻。"),
            new Entry(AscentAScene, "亚海悬城 A", "仿亚海悬城 A 点：A 大、连接、发电机、三箱、天堂 / 地狱、酒窖、花园。布局为近似复刻。"),
            new Entry(BindBScene, "源工重镇 B", "仿源工重镇 B 点：B 长廊、水烟房窗口、B 大箱、双箱、花园、肘角。布局为近似复刻。"),
            new Entry(HavenCScene, "隐世修所 C", "仿隐世修所 C 点：C 长廊、车库门、木头堆、大箱、角落。布局为近似复刻。"),
            new Entry(SplitAScene, "霓虹町 A", "仿霓虹町 A 点：A 大斜坡、高处包点、屏风、塔、天堂。布局为近似复刻。"),
        };

        public static readonly Entry Movement =
            new Entry(MovementScene, "移动测试", "第一阶段的移动手感测试场景：跑道、台阶、跳台、下蹲通道。");

        public static string TitleOf(string sceneName)
        {
            foreach (Entry e in Training)
            {
                if (e.SceneName == sceneName) return e.Title;
            }
            return sceneName == MovementScene ? Movement.Title : sceneName;
        }
    }
}
