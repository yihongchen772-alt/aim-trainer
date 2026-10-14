using System;
using AimTrainer.Combat;

namespace AimTrainer.Weapons
{
    /// <summary>移动状态对精度的影响分档，用于界面显示。</summary>
    public enum AccuracyTier
    {
        /// <summary>站定或急停后：没有移动惩罚。</summary>
        Standing,

        /// <summary>静步速度附近：较小的惩罚。</summary>
        Walking,

        /// <summary>跑动：很大的惩罚。</summary>
        Running,

        /// <summary>空中：最大的惩罚。</summary>
        Airborne,
    }

    /// <summary>
    /// 射击相关的纯函数：散布、弹道图案、伤害。不依赖 UnityEngine，可以直接做单元测试。
    /// </summary>
    public static class WeaponMath
    {
        /// <summary>
        /// 移动带来的额外散布（度）。
        /// 速度 ≤ 精准门槛：0（急停后开枪）。门槛 → 静步速度：0 → walkSpread 线性增加（静步开枪）。
        /// 静步速度 → 跑步速度：walkSpread → runSpread 线性增加（跑动开枪）。空中：airSpread。
        /// </summary>
        public static float MovementSpread(WeaponStats w, float speed, bool grounded, float accurateThreshold,
            float walkSpeed, float runSpeed)
        {
            if (!grounded) return w.airSpread;
            if (speed <= accurateThreshold) return 0f;
            if (speed <= walkSpeed)
            {
                return w.walkSpread * InverseLerp(accurateThreshold, walkSpeed, speed);
            }
            return Lerp(w.walkSpread, w.runSpread, InverseLerp(walkSpeed, runSpeed, speed));
        }

        /// <summary>移动状态对应的精度分档。</summary>
        public static AccuracyTier Tier(float speed, bool grounded, float accurateThreshold, float walkSpeed)
        {
            if (!grounded) return AccuracyTier.Airborne;
            if (speed <= accurateThreshold) return AccuracyTier.Standing;
            // 留 5% 余量，静步满速时不会因为浮点误差跳到“跑动”。
            if (speed <= walkSpeed * 1.05f) return AccuracyTier.Walking;
            return AccuracyTier.Running;
        }

        /// <summary>第 shotIndex 发（从 1 开始）的基础散布（不含移动惩罚），已乘下蹲系数。</summary>
        public static float BaseSpread(WeaponStats w, int shotIndex, float crouchAmount)
        {
            float spray = MathF.Min(w.firstShotSpread + w.sprayGrowthPerShot * MathF.Max(0, shotIndex - 1),
                MathF.Max(w.maxSpraySpread, w.firstShotSpread));
            return spray * Lerp(1f, w.crouchMultiplier, Clamp01(crouchAmount));
        }

        /// <summary>总散布（度）= 基础散布 + 移动惩罚。</summary>
        public static float TotalSpread(WeaponStats w, int shotIndex, float crouchAmount, float movementSpread) =>
            BaseSpread(w, shotIndex, crouchAmount) + movementSpread;

        /// <summary>
        /// 第 shotIndex 发（从 1 开始）的固定弹道偏移（度）：pitch 向上为正，yaw 向右为正。
        /// 前几发不动，之后逐发上跳，到上限后改为左右摆动。压枪就是往反方向拉鼠标抵消它。
        /// </summary>
        public static void RecoilPattern(WeaponStats w, int shotIndex, out float pitch, out float yaw)
        {
            int climbed = Math.Clamp(shotIndex - w.climbStartShot + 1, 0, w.climbShots);
            pitch = climbed * w.climbPerShot;
            yaw = shotIndex >= w.swayStartShot
                ? MathF.Sin((shotIndex - w.swayStartShot + 1) * w.swayFrequency) * w.swayAmplitude
                : 0f;
        }

        /// <summary>按部位和距离结算伤害。</summary>
        public static float Damage(WeaponStats w, HitZone zone, float distance)
        {
            float damage = zone switch
            {
                HitZone.Head => w.headDamage,
                HitZone.Legs => w.legDamage,
                _ => w.bodyDamage,
            };
            if (w.falloffDistance > 0f && distance > w.falloffDistance) damage *= w.falloffMultiplier;
            return damage;
        }

        /// <summary>在半径为 radiusDegrees 的圆内均匀取一个点（度），用于随机散布。</summary>
        public static void SampleDisc(Random rng, float radiusDegrees, out float x, out float y)
        {
            double r = radiusDegrees * Math.Sqrt(rng.NextDouble());
            double angle = rng.NextDouble() * Math.PI * 2.0;
            x = (float)(r * Math.Cos(angle));
            y = (float)(r * Math.Sin(angle));
        }

        static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        static float InverseLerp(float a, float b, float v) => MathF.Abs(b - a) < 1e-6f ? 1f : Clamp01((v - a) / (b - a));
    }
}
