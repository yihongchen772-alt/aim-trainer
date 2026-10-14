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

        public static readonly Entry[] Training =
        {
            new Entry(RangeScene, "靶场", "开阔场地，少量掩体，适合甩枪和跟枪。"),
            new Entry(SiteScene, "A 点（原创白模）", "大道、连接、高台、包点箱体，适合对枪和预瞄清点。"),
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
