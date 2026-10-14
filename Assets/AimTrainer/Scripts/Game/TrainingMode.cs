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

        /// <summary>预瞄清点：所有预瞄点位同时放上会开枪的假人，计时清空整个点位。</summary>
        Prefire,
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
                default: return "所有预瞄点位同时放上会开枪的假人，计时清空整个区域。练路线、预瞄角度和清点速度。";
            }
        }

        /// <summary>这个模式下假人会不会开枪（玩家会不会受伤）。</summary>
        public static bool BotsShoot(TrainingMode mode) => mode == TrainingMode.Duel || mode == TrainingMode.Prefire;
    }
}
