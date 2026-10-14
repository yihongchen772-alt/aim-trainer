using System;
using UnityEngine;

namespace AimTrainer.Movement
{
    /// <summary>
    /// 移动手感的全部可调参数。默认值来自网页原型，是按手感调出来的，不是官方数据。
    /// 单位：长度为米，速度为米/秒，速率为 1/秒（越大变化越快）。
    /// </summary>
    [Serializable]
    public sealed class MovementTuning
    {
        [Header("水平速度")]
        [Tooltip("跑步速度（米/秒），对应手持步枪。")]
        [Min(0f)] public float runSpeed = 5.4f;

        [Tooltip("静步（按住 Shift）速度 = 跑步速度 × 该系数。")]
        [Range(0f, 1f)] public float walkSpeedMultiplier = 0.52f;

        [Tooltip("完全蹲下时的速度 = 跑步速度 × 该系数。与静步同时生效时取较慢的一个。")]
        [Range(0f, 1f)] public float crouchSpeedMultiplier = 0.42f;

        [Header("加速与急停（1/秒）")]
        [Tooltip("有方向输入时，速度向目标速度靠拢的速率。13 表示约 177 毫秒达到目标速度的 90%。")]
        [Min(0f)] public float groundAcceleration = 13f;

        [Tooltip("松开所有方向键后的减速速率。18 时从 5.4 降到 1.6 米/秒约需 68 毫秒。")]
        [Min(0f)] public float releaseDeceleration = 18f;

        [Tooltip("按下与当前移动方向相反的键（反向急停）时的速率。目标速度变成反方向的全速，所以比松键停得更快：13 时约 33 毫秒。")]
        [Min(0f)] public float counterStrafeDeceleration = 13f;

        [Tooltip("松键减速时，速度低于该值直接归零，避免指数衰减拖出很长的尾巴。")]
        [Min(0f)] public float stopSnapSpeed = 0.05f;

        [Tooltip("空中有方向输入时，速度向输入方向靠拢的速率。1.2 时一次完整跳跃（约 0.6 秒）大约完成一半的变化：侧向能转约 45°，按反方向几乎能在空中停下。想要更弱的空中控制可调到 0.3 左右。")]
        [Min(0f)] public float airAcceleration = 1.2f;

        [Header("跳跃与重力")]
        [Tooltip("重力加速度（米/秒²）。")]
        [Min(0f)] public float gravity = 20f;

        [Tooltip("起跳后脚底能达到的最大高度（米）。起跳速度由它和重力算出：√(2 × 重力 × 跳高)。")]
        [Min(0f)] public float jumpHeight = 0.9f;

        [Tooltip("落地前这么多秒内按下跳跃，落地时会自动起跳。0 表示只在按下的那一帧判断。")]
        [Min(0f)] public float jumpBufferTime = 0.08f;

        [Tooltip("站在地面时施加的向下速度（米/秒）。更陡的下坡和下台阶由控制器额外的贴地检测处理。")]
        [Min(0f)] public float groundStickSpeed = 2f;

        [Tooltip("在地面上能直接走上去的最大台阶高度（米）。空中时自动变为 0，跳不上的台子不会被“吸”上去。")]
        [Min(0f)] public float stepOffset = 0.4f;

        [Tooltip("完全蹲下时能直接走上的台阶高度（米）。必须小于“最矮通道净高 − 下蹲身高 − 皮肤厚度”，否则蹲着会卡在矮通道入口。")]
        [Min(0f)] public float crouchStepOffset = 0.1f;

        [Header("身体尺寸（米）")]
        [Min(0.1f)] public float standingHeight = 1.8f;
        [Min(0.1f)] public float crouchHeight = 1.3f;
        [Min(0f)] public float standingEyeHeight = 1.6f;
        [Min(0f)] public float crouchEyeHeight = 1.15f;

        [Tooltip("从站立到完全蹲下（或反过来）需要的秒数。")]
        [Min(0f)] public float crouchTransitionTime = 0.1f;

        [Header("精准判定")]
        [Tooltip("水平速度不超过这个值且站在地面上时，视为可以精准射击。第二阶段的散布计算会用到它。")]
        [Min(0f)] public float accurateSpeedThreshold = 1.6f;

        /// <summary>起跳瞬间的竖直速度。</summary>
        public float JumpVelocity => MovementModel.JumpVelocity(jumpHeight, gravity);

        /// <summary>静步速度（米/秒）。</summary>
        public float WalkSpeed => runSpeed * walkSpeedMultiplier;

        /// <summary>从跑步速度松开方向键，降到精准门槛以下的理论耗时（秒）。</summary>
        public float TheoreticalReleaseStopTime =>
            MovementModel.ReleaseStopTime(runSpeed, accurateSpeedThreshold, releaseDeceleration);

        /// <summary>从跑步速度按反方向键，降到精准门槛以下的理论耗时（秒）。</summary>
        public float TheoreticalCounterStrafeStopTime =>
            MovementModel.CounterStrafeStopTime(runSpeed, runSpeed, accurateSpeedThreshold, counterStrafeDeceleration);
    }
}
