namespace AimTrainer.Game
{
    public enum TrainingMode
    {
        /// <summary>定点甩枪：假人站着不动，随机出现在视野里，打死立刻刷新。</summary>
        Flick,

        /// <summary>移动靶：假人左右横跳，不开枪。</summary>
        Strafe,

        /// <summary>对枪：假人在点位上探头或横跳，看到你会开枪。被打死后整轮重置。</summary>
        Duel,

        /// <summary>预瞄清点：所有预瞄点位（或选定路线上的点位）同时放上会开枪的假人，计时清空。</summary>
        Prefire,

        /// <summary>架枪：玩家守一个位置，假人从入口全速冲出来对枪。</summary>
        Hold,
    }

    public static class TrainingModeInfo
    {
        public static string Title(TrainingMode mode)
        {
            switch (mode)
            {
                case TrainingMode.Flick: return "定点甩枪";
                case TrainingMode.Strafe: return "移动靶";
                case TrainingMode.Duel: return "对枪";
                case TrainingMode.Hold: return "架枪";
                default: return "预瞄清点";
            }
        }

        public static string Description(TrainingMode mode)
        {
            switch (mode)
            {
                case TrainingMode.Flick: return "假人静止，随机出现在视野内，打死立刻刷新。练首发甩枪和爆头线。";
                case TrainingMode.Strafe: return "假人左右急停横跳，不会开枪。练跟枪和预判急停时机。";
                case TrainingMode.Duel: return "假人在地图点位上探头、横跳，看到你后按反应时间开枪。被击杀后整轮重置。";
                case TrainingMode.Hold: return "守住一个位置，假人随机从入口全速冲出来，看到你后按反应时间开枪。练架枪的反应和爆头线。";
                default: return "点位上放好会开枪的假人，假人真正看到你后按反应时间开枪，计时清空。可以选路线，结束后显示每个角度的耗时。";
            }
        }

        /// <summary>这个模式下假人会不会开枪（玩家会不会受伤）。</summary>
        public static bool BotsShoot(TrainingMode mode) =>
            mode == TrainingMode.Duel || mode == TrainingMode.Prefire || mode == TrainingMode.Hold;

        /// <summary>这个模式是否按点位统计（结算时显示每个角度的成绩）。</summary>
        public static bool TracksAngles(TrainingMode mode) => BotsShoot(mode);
    }
}
