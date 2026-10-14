using UnityEngine;

namespace AimTrainer.Weapons
{
    /// <summary>
    /// 保存枪械参数的资源文件。运行中在 Inspector 里改，退出播放后改动会保留。
    /// 创建方法：Project 窗口右键 → Create → AimTrainer → Weapon。
    /// </summary>
    [CreateAssetMenu(fileName = "Weapon", menuName = "AimTrainer/Weapon")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        public WeaponStats stats = new WeaponStats();
    }
}
