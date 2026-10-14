using System;

namespace AimTrainer.Weapons
{
    /// <summary>
    /// 射击节奏，与帧率无关：按住左键时第 k 发出现在 t₀ + k·间隔，
    /// 一帧里到期几发就打几发（低帧率时不会少打），间隔不会因为帧率高低而变化。
    /// 半自动时每次按下最多打一发，并且仍然受最短间隔限制。
    /// </summary>
    public sealed class FireClock
    {
        /// <summary>一帧最多补打的子弹数，防止卡顿后一次打出一大串。</summary>
        public const int MaxShotsPerTick = 4;

        double nextShotTime = double.NegativeInfinity;
        bool wasHeld;

        /// <summary>
        /// 每帧调用一次，返回本帧应该发射的子弹数。
        /// </summary>
        /// <param name="now">当前时间（秒）。</param>
        /// <param name="triggerHeld">左键是否按住。</param>
        /// <param name="interval">两发之间的间隔（秒）。</param>
        /// <param name="automatic">是否全自动。</param>
        public int Tick(double now, bool triggerHeld, float interval, bool automatic)
        {
            int shots = 0;
            bool pressedThisTick = triggerHeld && !wasHeld;
            wasHeld = triggerHeld;
            if (!triggerHeld) return 0;

            if (pressedThisTick && nextShotTime < now) nextShotTime = now;

            if (automatic)
            {
                while (nextShotTime <= now && shots < MaxShotsPerTick)
                {
                    shots++;
                    nextShotTime += interval;
                }
                // 卡顿太久时丢掉补不上的子弹，从现在重新计时。
                if (nextShotTime < now - interval) nextShotTime = now;
            }
            else if (pressedThisTick && nextShotTime <= now)
            {
                shots = 1;
                nextShotTime = now + interval;
            }
            return shots;
        }

        /// <summary>换弹、切换场景时调用：松开扳机，下一次按下立即可以开枪。</summary>
        public void Reset()
        {
            nextShotTime = double.NegativeInfinity;
            wasHeld = false;
        }
    }
}
