using System;
using System.Collections.Generic;

namespace AimTrainer.Game
{
    /// <summary>
    /// 按点位（角度）统计的成绩：每个点位被击杀几次、平均击杀耗时、被它击杀几次。
    /// 预瞄和架枪训练结束后用它找出最慢、最容易被打死的角度。纯数据，不依赖 UnityEngine。
    /// </summary>
    public sealed class AngleStats
    {
        public sealed class Entry
        {
            public string Label;
            public int Kills;

            /// <summary>被这个点位的假人击杀的次数。</summary>
            public int Deaths;

            public float TotalTime;
            public int TimedKills;
            public float SlowestTime;

            /// <summary>平均击杀耗时（秒）：从和假人对上视线到击杀。没有有效样本时为 0。</summary>
            public float AverageTime => TimedKills > 0 ? TotalTime / TimedKills : 0f;
        }

        readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();

        public int Count => entries.Count;

        /// <summary>记录一次击杀。time 为负表示假人没看到你就被打死了，只计击杀数，不计耗时。</summary>
        public void RecordKill(string label, float time)
        {
            Entry entry = Get(label);
            entry.Kills++;
            if (time < 0f) return;
            entry.TotalTime += time;
            entry.TimedKills++;
            entry.SlowestTime = MathF.Max(entry.SlowestTime, time);
        }

        public void RecordDeath(string label) => Get(label).Deaths++;

        public void Reset() => entries.Clear();

        public Entry Find(string label) => entries.TryGetValue(Normalize(label), out Entry entry) ? entry : null;

        /// <summary>按“最需要练”的顺序排列：被击杀次数多的在前，其次是平均耗时长的。</summary>
        public List<Entry> Sorted()
        {
            var list = new List<Entry>(entries.Values);
            list.Sort((a, b) =>
            {
                int byDeaths = b.Deaths.CompareTo(a.Deaths);
                if (byDeaths != 0) return byDeaths;
                int byTime = b.AverageTime.CompareTo(a.AverageTime);
                return byTime != 0 ? byTime : string.CompareOrdinal(a.Label, b.Label);
            });
            return list;
        }

        Entry Get(string label)
        {
            string key = Normalize(label);
            if (!entries.TryGetValue(key, out Entry entry))
            {
                entry = new Entry { Label = key };
                entries.Add(key, entry);
            }
            return entry;
        }

        static string Normalize(string label) => string.IsNullOrEmpty(label) ? "未命名" : label;
    }
}
