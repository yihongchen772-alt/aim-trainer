using System;
using System.Collections.Generic;
using UnityEngine;

namespace AimTrainer.Game
{
    /// <summary>一局训练的记录。</summary>
    [Serializable]
    public sealed class HistoryEntry
    {
        /// <summary>结束时间（本地时间，yyyy-MM-dd HH:mm）。</summary>
        public string time;

        public string scene;
        public TrainingMode mode;
        public Bots.BotDifficulty difficulty;
        public int botCount;
        public int duration;
        public string weapon;

        /// <summary>预瞄路线或架枪位置的名称，没有时为空。</summary>
        public string variant;

        public int score;
        public int kills;
        public int deaths;
        public float accuracy;
        public float headshotRate;
        public float bestRoundTime;
        public float averageKillTime;

        /// <summary>这一局实际玩了多久（秒，不含暂停）。</summary>
        public float seconds;
    }

    /// <summary>最近的训练记录（最多 30 局），保存在本机。菜单里的“记录”和“继续上次训练”用它。</summary>
    public static class SessionHistory
    {
        const string PrefsKey = "AimTrainer.History.v1";
        public const int Capacity = 30;

        [Serializable]
        sealed class Store
        {
            public List<HistoryEntry> entries = new List<HistoryEntry>();
        }

        static Store store;

        /// <summary>按时间从新到旧排列。</summary>
        public static IReadOnlyList<HistoryEntry> Entries => Load().entries;

        /// <summary>最近一局，没有记录时为 null。</summary>
        public static HistoryEntry Last => Load().entries.Count > 0 ? Load().entries[0] : null;

        public static void Add(HistoryEntry entry)
        {
            if (entry == null) return;
            List<HistoryEntry> list = Load().entries;
            list.Insert(0, entry);
            if (list.Count > Capacity) list.RemoveRange(Capacity, list.Count - Capacity);
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(store));
            PlayerPrefs.Save();
        }

        public static void Clear()
        {
            Load().entries.Clear();
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();
        }

        static Store Load()
        {
            if (store != null) return store;
            store = new Store();
            try
            {
                if (PlayerPrefs.HasKey(PrefsKey)) JsonUtility.FromJsonOverwrite(PlayerPrefs.GetString(PrefsKey), store);
            }
            catch (ArgumentException)
            {
                // 存档损坏时从空记录开始。
            }
            store.entries ??= new List<HistoryEntry>();
            store.entries.RemoveAll(e => e == null || string.IsNullOrEmpty(e.scene));
            return store;
        }
    }
}
