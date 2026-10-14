using UnityEngine;

namespace AimTrainer.Movement
{
    /// <summary>
    /// 保存移动参数的资源文件。运行游戏时在 Inspector 里改它，退出播放后改动会保留，方便反复调手感。
    /// 创建方法：Project 窗口右键 → Create → AimTrainer → Movement Settings。
    /// </summary>
    [CreateAssetMenu(fileName = "MovementSettings", menuName = "AimTrainer/Movement Settings")]
    public sealed class MovementSettings : ScriptableObject
    {
        public MovementTuning tuning = new MovementTuning();
    }
}
