using System;
using System.Collections.Generic;

namespace AimTrainer.Game
{
    /// <summary>一局训练的成绩统计。纯数据，不依赖 UnityEngine。</summary>
    public sealed class SessionStats
    {
        readonly List<float> killTimes = new List<float>();
        readonly List<float> roundTimes = new List<float>();

        public int Shots { get; private set; }

        /// <summary>打中假人的子弹数（不含打墙）。</summary>
        public int Hits { get; private set; }

        public int Headshots { get; private set; }
        public int Kills { get; private set; }

        /// <summary>一枪爆头击杀的次数（假人满血时第一枪就爆头）。</summary>
        public int OneTapKills { get; private set; }

        public int Deaths { get; private set; }
        public float DamageDealt { get; private set; }
        public int RoundsCleared => roundTimes.Count;

        /// <summary>命中率：打中假人的子弹 / 开枪数。</summary>
        public float Accuracy => Shots > 0 ? (float)Hits / Shots : 0f;

        /// <summary>爆头率：爆头的子弹 / 打中假人的子弹。</summary>
        public float HeadshotRate => Hits > 0 ? (float)Headshots / Hits : 0f;

        /// <summary>平均击杀耗时（秒）：从假人出现（或第一次和你对上视线）到被打死。</summary>
        public float AverageKillTime => killTimes.Count > 0 ? Sum(killTimes) / killTimes.Count : 0f;

        public float BestRoundTime => roundTimes.Count > 0 ? Min(roundTimes) : 0f;
        public float LastRoundTime => roundTimes.Count > 0 ? roundTimes[roundTimes.Count - 1] : 0f;

        /// <summary>
        /// 综合得分：每次击杀 100 分，爆头的子弹每发 +20，按命中率加权（命中率越高越接近满分）。
        /// 只用来和自己的历史成绩比较。
        /// </summary>
        public int Score => (int)MathF.Round((Kills * 100f + Headshots * 20f) * (0.5f + 0.5f * Accuracy));

        public void RecordShot(bool hitTarget, bool headshot, float damage)
        {
            Shots++;
            if (!hitTarget) return;
            Hits++;
            if (headshot) Headshots++;
            DamageDealt += damage;
        }

        /// <summary>记录一次击杀。timeToKill 为负时不计入平均耗时。</summary>
        public void RecordKill(float timeToKill, bool oneTap)
        {
            Kills++;
            if (oneTap) OneTapKills++;
            if (timeToKill >= 0f) killTimes.Add(timeToKill);
        }

        public void RecordDeath() => Deaths++;

        public void RecordRoundCleared(float seconds) => roundTimes.Add(seconds);

        public void Reset()
        {
            Shots = Hits = Headshots = Kills = OneTapKills = Deaths = 0;
            DamageDealt = 0f;
            killTimes.Clear();
            roundTimes.Clear();
        }

        static float Sum(List<float> values)
        {
            float total = 0f;
            foreach (float v in values) total += v;
            return total;
        }

        static float Min(List<float> values)
        {
            float best = float.MaxValue;
            foreach (float v in values) best = MathF.Min(best, v);
            return best;
        }
    }
}
