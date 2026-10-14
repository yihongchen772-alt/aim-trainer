using System;
using UnityEngine;

namespace AimTrainer.Game
{
    /// <summary>每种“场景 + 模式 + 难度 + 假人数 + 时长”组合的最好成绩，保存在本机。</summary>
    public static class BestRecords
    {
        [Serializable]
        public sealed class Record
        {
            public int score;
            public int kills;
            public float accuracy;
            public float headshotRate;
            public float bestRoundTime;
        }

        public static string Key(string scene, TrainingMode mode, Bots.BotDifficulty difficulty, int bots, int duration) =>
            $"AimTrainer.Best.{scene}.{mode}.{difficulty}.{bots}.{duration}";

        public static Record Get(string key)
        {
            if (!PlayerPrefs.HasKey(key)) return null;
            try
            {
                return JsonUtility.FromJson<Record>(PlayerPrefs.GetString(key));
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        /// <summary>保存本局成绩，返回是否刷新了最高分（预瞄模式按最快清点时间比较）。</summary>
        public static bool Submit(string key, SessionStats stats, bool compareRoundTime)
        {
            Record previous = Get(key);
            bool better;
            if (previous == null) better = compareRoundTime ? stats.RoundsCleared > 0 : stats.Score > 0;
            else if (compareRoundTime)
                better = stats.RoundsCleared > 0 && (previous.bestRoundTime <= 0f || stats.BestRoundTime < previous.bestRoundTime);
            else better = stats.Score > previous.score;

            if (!better) return false;
            var record = new Record
            {
                score = stats.Score,
                kills = stats.Kills,
                accuracy = stats.Accuracy,
                headshotRate = stats.HeadshotRate,
                bestRoundTime = stats.BestRoundTime,
            };
            PlayerPrefs.SetString(key, JsonUtility.ToJson(record));
            PlayerPrefs.Save();
            return true;
        }
    }
}
