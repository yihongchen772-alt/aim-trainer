using System;

namespace AimTrainer.Weapons
{
    /// <summary>
    /// 连射计数：决定当前是第几发（影响散布和弹道上跳）。
    /// 每开一枪计数 +1；停火超过 sprayRecoveryDelay 秒后，按 sprayRecoveryRate 逐渐恢复。
    /// 所以点射（间隔足够长）每一发都是“第一发”，长按连射时计数一路增加。
    /// </summary>
    public sealed class SprayTracker
    {
        double lastShotTime = double.NegativeInfinity;
        float heat;

        /// <summary>当前累积的连射计数（可以是小数，恢复是连续的）。</summary>
        public float Heat => heat;

        /// <summary>开一枪，返回这是第几发（从 1 开始）。</summary>
        public int RegisterShot(double now, WeaponStats w)
        {
            heat = HeatAt(now, w);
            heat += 1f;
            lastShotTime = now;
            return Math.Max(1, (int)MathF.Ceiling(heat - 1e-4f));
        }

        /// <summary>如果现在开枪，会是第几发。</summary>
        public int PeekShotIndex(double now, WeaponStats w) =>
            Math.Max(1, (int)MathF.Ceiling(HeatAt(now, w) + 1f - 1e-4f));

        public void Reset()
        {
            heat = 0f;
            lastShotTime = double.NegativeInfinity;
        }

        float HeatAt(double now, WeaponStats w)
        {
            if (double.IsNegativeInfinity(lastShotTime)) return 0f;
            double recovering = now - lastShotTime - w.sprayRecoveryDelay;
            if (recovering <= 0.0) return heat;
            return MathF.Max(0f, heat - (float)(recovering * w.sprayRecoveryRate));
        }
    }
}
