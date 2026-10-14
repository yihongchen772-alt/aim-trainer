using System;
using UnityEngine;

namespace AimTrainer.Bots
{
    public enum BotDifficulty
    {
        Easy,
        Normal,
        Hard,
        Pro,
    }

    /// <summary>
    /// 假人的能力参数。反应时间是“玩家进入视野圆锥并且没有被挡住”到“第一次开枪”的时间。
    /// </summary>
    [Serializable]
    public sealed class BotSkill
    {
        [Tooltip("反应时间（秒）。")]
        [Min(0f)] public float reactionTime = 0.3f;

        [Tooltip("反应时间的随机浮动比例。0.2 表示在 80%~120% 之间随机。")]
        [Range(0f, 1f)] public float reactionJitter = 0.15f;

        [Tooltip("每一枪的基础命中率。")]
        [Range(0f, 1f)] public float hitChance = 0.45f;

        [Tooltip("命中时打中头部的概率。")]
        [Range(0f, 1f)] public float headshotChance = 0.2f;

        [Tooltip("两枪之间的间隔（秒）。")]
        [Min(0.05f)] public float fireInterval = 0.35f;

        [Tooltip("视野圆锥的半角（度）。玩家在假人正前方这个角度以内才会被发现。")]
        [Range(5f, 180f)] public float visionHalfAngle = 60f;

        [Tooltip("最远发现距离（米）。")]
        [Min(1f)] public float visionRange = 60f;

        [Tooltip("发现玩家后转身的速度（度/秒）。")]
        [Min(0f)] public float turnSpeed = 540f;

        [Tooltip("玩家短暂离开视线后，假人还“记得”多久（秒）。在这段时间内回到视线不需要重新反应。")]
        [Min(0f)] public float memoryTime = 0.25f;

        [Tooltip("左右移动和探头的速度（米/秒）。")]
        [Min(0f)] public float moveSpeed = 3.6f;

        public static BotSkill For(BotDifficulty difficulty)
        {
            switch (difficulty)
            {
                case BotDifficulty.Easy:
                    return new BotSkill { reactionTime = 0.5f, hitChance = 0.3f, headshotChance = 0.08f, fireInterval = 0.5f, visionHalfAngle = 50f, moveSpeed = 3f };
                case BotDifficulty.Hard:
                    return new BotSkill { reactionTime = 0.22f, hitChance = 0.6f, headshotChance = 0.3f, fireInterval = 0.28f, visionHalfAngle = 70f, moveSpeed = 4f };
                case BotDifficulty.Pro:
                    return new BotSkill { reactionTime = 0.16f, hitChance = 0.72f, headshotChance = 0.42f, fireInterval = 0.22f, visionHalfAngle = 80f, moveSpeed = 4.4f };
                default:
                    return new BotSkill();
            }
        }

        public BotSkill Clone() => (BotSkill)MemberwiseClone();
    }
}
