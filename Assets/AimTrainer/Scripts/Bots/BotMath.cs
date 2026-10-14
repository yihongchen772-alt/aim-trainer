using System;

namespace AimTrainer.Bots
{
    /// <summary>假人一枪的结果。</summary>
    public enum BotShotOutcome
    {
        Miss,
        Body,
        Legs,
        Head,
    }

    /// <summary>假人逻辑里的纯函数：视野判定和命中掷骰。不依赖 UnityEngine，可以单元测试。</summary>
    public static class BotMath
    {
        /// <summary>
        /// 目标是否在视野圆锥内：方向与正前方的夹角不超过 halfAngle，距离不超过 range。
        /// forward 不需要归一化；toTarget 是从眼睛指向目标的向量。
        /// </summary>
        public static bool InVisionCone(float forwardX, float forwardY, float forwardZ,
            float toTargetX, float toTargetY, float toTargetZ, float halfAngleDegrees, float range)
        {
            float distSq = toTargetX * toTargetX + toTargetY * toTargetY + toTargetZ * toTargetZ;
            if (distSq > range * range) return false;
            if (distSq < 1e-8f) return true;
            float fLen = MathF.Sqrt(forwardX * forwardX + forwardY * forwardY + forwardZ * forwardZ);
            if (fLen < 1e-6f) return false;
            float cos = (forwardX * toTargetX + forwardY * toTargetY + forwardZ * toTargetZ) / (fLen * MathF.Sqrt(distSq));
            return cos >= MathF.Cos(halfAngleDegrees * (MathF.PI / 180f));
        }

        /// <summary>
        /// 命中率修正：玩家移动越快越难打中（跑动 ×0.7），远距离（超过 30 米）×0.8，空中 ×0.8，下蹲 ×0.92。
        /// </summary>
        public static float AdjustedHitChance(float baseChance, float targetSpeed, float runSpeed, bool targetGrounded,
            float targetCrouch, float distance)
        {
            float speedFactor = runSpeed > 0f ? Math.Clamp(targetSpeed / runSpeed, 0f, 1f) : 0f;
            float chance = baseChance * (1f - 0.3f * speedFactor);
            if (distance > 30f) chance *= 0.8f;
            if (!targetGrounded) chance *= 0.8f;
            chance *= 1f - 0.08f * Math.Clamp(targetCrouch, 0f, 1f);
            return Math.Clamp(chance, 0f, 1f);
        }

        /// <summary>掷骰决定这一枪的结果。命中后按 headshotChance 判定爆头，否则 85% 打身体、15% 打腿。</summary>
        public static BotShotOutcome RollShot(Random rng, float hitChance, float headshotChance)
        {
            if (rng.NextDouble() >= hitChance) return BotShotOutcome.Miss;
            if (rng.NextDouble() < headshotChance) return BotShotOutcome.Head;
            return rng.NextDouble() < 0.85 ? BotShotOutcome.Body : BotShotOutcome.Legs;
        }
    }

    /// <summary>
    /// 反应计时：玩家持续可见满 reactionTime 秒后才允许开枪。
    /// 玩家离开视线不超过 memoryTime 秒又回来时，不需要重新反应；超过后计时清零，并重新随机一个反应时间。
    /// </summary>
    public sealed class ReactionTimer
    {
        readonly Random rng;
        float baseReaction;
        float jitter;
        float memory;
        float currentReaction;
        float seen;
        float lost;

        public ReactionTimer(Random random, float reactionTime, float reactionJitter, float memoryTime)
        {
            rng = random;
            Configure(reactionTime, reactionJitter, memoryTime);
        }

        /// <summary>本轮实际使用的反应时间（已加随机浮动）。</summary>
        public float CurrentReaction => currentReaction;

        /// <summary>玩家已经连续可见的时间。</summary>
        public float SeenTime => seen;

        public bool Ready => seen >= currentReaction;

        public void Configure(float reactionTime, float reactionJitter, float memoryTime)
        {
            baseReaction = Math.Max(0f, reactionTime);
            jitter = Math.Clamp(reactionJitter, 0f, 1f);
            memory = Math.Max(0f, memoryTime);
            Reset();
        }

        /// <summary>每次感知更新时调用，返回现在是否可以开枪。</summary>
        public bool Tick(bool canSee, float dt)
        {
            if (canSee)
            {
                lost = 0f;
                seen += dt;
            }
            else if (seen > 0f)
            {
                lost += dt;
                if (lost > memory) Reset();
            }
            return canSee && Ready;
        }

        public void Reset()
        {
            seen = 0f;
            lost = 0f;
            currentReaction = baseReaction * (1f + jitter * (float)(rng.NextDouble() * 2.0 - 1.0));
        }
    }
}
