using System;
using AimTrainer.Bots;
using UnityEngine;

namespace AimTrainer.Game
{
    /// <summary>
    /// 玩家的偏好设置和上次的训练选项，保存在本机（PlayerPrefs），重新打开游戏会恢复。
    /// </summary>
    [Serializable]
    public sealed class GameSettings
    {
        const string PrefsKey = "AimTrainer.Settings.v1";

        static GameSettings current;

        [Header("鼠标")]
        public float sensitivity = 0.4f;
        public float mouseDpi = 800f;
        public bool invertY;

        [Header("准星")]
        public Color crosshairColor = new Color(0f, 1f, 0.6f, 1f);
        public int crosshairLength = 5;
        public int crosshairGap = 3;
        public int crosshairThickness = 2;
        public bool crosshairOutline = true;
        public bool crosshairDot;

        [Tooltip("在准星下方显示当前移动状态的精准度（绿 = 急停 / 黄 = 静步 / 红 = 跑动或空中）。")]
        public bool showAccuracyIndicator = true;

        [Header("画面与声音")]
        [Range(0f, 1f)] public float volume = 0.7f;

        [Tooltip("帧率上限，0 表示不限制。")]
        public int frameRateCap;

        public bool showDebugPanel;

        [Tooltip("训练时在右上角显示小地图。")]
        public bool showMinimap = true;

        [Header("键位")]
        public Player.KeyBindings keys = new Player.KeyBindings();

        [Header("训练选项")]
        public TrainingMode mode = TrainingMode.Flick;
        public BotDifficulty difficulty = BotDifficulty.Normal;
        public int botCount = 1;

        [Tooltip("一局的时长（秒），0 表示不限时。")]
        public int durationSeconds = 60;

        [Tooltip("使用的枪，见 WeaponPresets（vandal / phantom / spectre / sheriff / ghost）。")]
        public string weaponId = Weapons.WeaponPresets.Vandal;

        [Tooltip("预瞄模式使用的路线名称，空字符串表示使用地图上所有预瞄点位。地图上没有同名路线时也使用所有点位。")]
        public string prefireRoute = string.Empty;

        [Tooltip("架枪模式使用的位置名称，空字符串表示地图上的第一个位置。")]
        public string holdSpot = string.Empty;

        /// <summary>当前设置（第一次访问时从本机读取）。</summary>
        public static GameSettings Current => current ??= Load();

        public static GameSettings Load()
        {
            var settings = new GameSettings();
            try
            {
                if (PlayerPrefs.HasKey(PrefsKey)) JsonUtility.FromJsonOverwrite(PlayerPrefs.GetString(PrefsKey), settings);
            }
            catch (ArgumentException)
            {
                // 存档损坏时使用默认值。
            }
            settings.Sanitize();
            return settings;
        }

        public void Save()
        {
            Sanitize();
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }

        /// <summary>把超出范围的数值拉回合理范围（例如手动改坏了存档）。</summary>
        public void Sanitize()
        {
            sensitivity = Mathf.Clamp(float.IsNaN(sensitivity) ? 0.4f : sensitivity, 0.01f, 10f);
            mouseDpi = Mathf.Clamp(float.IsNaN(mouseDpi) ? 800f : mouseDpi, 100f, 32000f);
            crosshairLength = Mathf.Clamp(crosshairLength, 0, 20);
            crosshairGap = Mathf.Clamp(crosshairGap, 0, 20);
            crosshairThickness = Mathf.Clamp(crosshairThickness, 1, 6);
            volume = Mathf.Clamp01(volume);
            frameRateCap = Mathf.Clamp(frameRateCap, 0, 1000);
            botCount = Mathf.Clamp(botCount, 1, 8);
            durationSeconds = Mathf.Clamp(durationSeconds, 0, 3600);
            if (!Enum.IsDefined(typeof(TrainingMode), mode)) mode = TrainingMode.Flick;
            if (!Enum.IsDefined(typeof(BotDifficulty), difficulty)) difficulty = BotDifficulty.Normal;
            if (!Weapons.WeaponPresets.IsKnown(weaponId)) weaponId = Weapons.WeaponPresets.Vandal;
            prefireRoute ??= string.Empty;
            holdSpot ??= string.Empty;
            keys ??= new Player.KeyBindings();
            keys.Sanitize();
        }
    }
}
