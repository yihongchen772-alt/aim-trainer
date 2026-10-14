using System;
using UnityEngine;

namespace AimTrainer.Weapons
{
    /// <summary>
    /// 一把枪的全部参数。默认值是按手感调出来的全自动步枪（参考端游最常用的步枪），不是官方数据。
    /// 角度单位都是度。散布是“子弹偏离准星的最大角度”（圆锥半角），在这个圆内均匀随机。
    /// </summary>
    [Serializable]
    public sealed class WeaponStats
    {
        [Header("基础")]
        public string displayName = "步枪";

        [Tooltip("每秒发射的子弹数。")]
        [Min(0.1f)] public float roundsPerSecond = 9.75f;

        [Tooltip("按住左键连发；关闭则每次点击只打一发。")]
        public bool automatic = true;

        [Min(1)] public int magazineSize = 25;

        [Tooltip("换弹耗时（秒）。")]
        [Min(0f)] public float reloadTime = 2.5f;

        [Tooltip("射线最远距离（米）。")]
        [Min(1f)] public float maxRange = 200f;

        [Header("伤害（无距离衰减时）")]
        [Min(0f)] public float headDamage = 160f;
        [Min(0f)] public float bodyDamage = 40f;
        [Min(0f)] public float legDamage = 34f;

        [Tooltip("超过这个距离（米）后伤害乘以衰减系数。0 表示没有距离衰减。")]
        [Min(0f)] public float falloffDistance;

        [Range(0f, 1f)] public float falloffMultiplier = 0.85f;

        [Header("精准度：基础散布（度）")]
        [Tooltip("静止时第一发的散布。")]
        [Min(0f)] public float firstShotSpread = 0.12f;

        [Tooltip("连射时每多一发增加的散布。")]
        [Min(0f)] public float sprayGrowthPerShot = 0.22f;

        [Tooltip("连射散布的上限（不含移动惩罚）。")]
        [Min(0f)] public float maxSpraySpread = 2.2f;

        [Tooltip("完全蹲下时，基础散布乘以这个系数。")]
        [Range(0f, 1f)] public float crouchMultiplier = 0.85f;

        [Header("精准度：移动惩罚（度，叠加在基础散布上）")]
        [Tooltip("以静步速度移动时额外增加的散布。速度不超过精准门槛（急停后）时为 0，介于两者之间时按比例。")]
        [Min(0f)] public float walkSpread = 1.6f;

        [Tooltip("以跑步速度移动时额外增加的散布。")]
        [Min(0f)] public float runSpread = 6f;

        [Tooltip("在空中时额外增加的散布。")]
        [Min(0f)] public float airSpread = 8f;

        [Header("后坐力：弹道图案（度）")]
        [Tooltip("从第几发开始上跳。")]
        [Min(1)] public int climbStartShot = 3;

        [Tooltip("每发上跳的角度。")]
        [Min(0f)] public float climbPerShot = 0.5f;

        [Tooltip("最多上跳多少发，之后不再继续升高。")]
        [Min(0)] public int climbShots = 8;

        [Tooltip("从第几发开始左右摆动。")]
        [Min(1)] public int swayStartShot = 11;

        [Tooltip("左右摆动的幅度。")]
        [Min(0f)] public float swayAmplitude = 1.4f;

        [Tooltip("左右摆动的快慢（每发相位增加的弧度）。")]
        [Min(0f)] public float swayFrequency = 0.8f;

        [Header("后坐力：恢复")]
        [Tooltip("停火超过这么多秒后，连射计数才开始恢复。要大于两发之间的间隔，否则连射时会提前恢复。")]
        [Min(0f)] public float sprayRecoveryDelay = 0.12f;

        [Tooltip("开始恢复后，每秒恢复多少发的连射计数。30 表示约 0.3 秒恢复 10 发。")]
        [Min(0f)] public float sprayRecoveryRate = 30f;

        [Header("枪模动画")]
        [Tooltip("每发子弹让枪模后坐的距离（米），只影响画面。")]
        [Min(0f)] public float viewKick = 0.03f;

        /// <summary>两发子弹之间的最短间隔（秒）。</summary>
        public float ShotInterval => 1f / MathF.Max(0.1f, roundsPerSecond);
    }
}
