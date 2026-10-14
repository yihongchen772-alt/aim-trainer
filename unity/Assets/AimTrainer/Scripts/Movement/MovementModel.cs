using System;

namespace AimTrainer.Movement
{
    /// <summary>
    /// 移动手感的核心算法：纯函数，不依赖 UnityEngine，可以直接做单元测试。
    ///
    /// 水平速度不交给物理引擎的摩擦力处理，而是每帧按指数规律向“目标速度”靠拢：
    ///     v ← 目标 + (v − 目标) · e^(−速率 · dt)
    /// 这个公式与帧率无关：同样经过 0.5 秒，30 帧和 500 帧算出的速度完全一样。
    ///
    /// 三种情况用不同的速率：
    /// - 有方向输入：groundAcceleration
    /// - 松开所有方向键：目标为 0，用 releaseDeceleration
    /// - 按下与当前速度相反的方向（反向急停）：目标是反方向的全速，用 counterStrafeDeceleration。
    ///   目标离得更远，所以即使速率相同，越过精准门槛的时间也比单纯松键短得多。
    /// </summary>
    public static class MovementModel
    {
        const float Epsilon = 1e-6f;

        /// <summary>
        /// 把键盘输入换算成世界水平面上的单位方向。
        /// forward / right 取值 -1..1，yawDegrees 是角色朝向（绕 +Y 轴，顺时针为正）。
        /// 同时按下相反的两个键时输入抵消为 0。
        /// </summary>
        public static PlanarVector WishDirection(float forward, float right, float yawDegrees)
        {
            PlanarVector f = PlanarVector.FromYaw(yawDegrees);
            var r = new PlanarVector(f.Z, -f.X);
            return (f * forward + r * right).Normalized;
        }

        /// <summary>当前姿态下的目标移动速度。静步和下蹲同时生效时取较慢的一个。</summary>
        public static float TargetSpeed(MovementTuning t, bool walking, float crouchAmount)
        {
            float walk = walking ? t.walkSpeedMultiplier : 1f;
            float crouch = Lerp(1f, t.crouchSpeedMultiplier, Clamp01(crouchAmount));
            return t.runSpeed * MathF.Min(walk, crouch);
        }

        /// <summary>地面上本帧使用的速率：松键、反向急停或正常加速。</summary>
        public static float GroundRate(PlanarVector velocity, PlanarVector wishDirection, MovementTuning t)
        {
            if (wishDirection.SqrMagnitude < Epsilon) return t.releaseDeceleration;
            return PlanarVector.Dot(velocity, wishDirection) < 0f ? t.counterStrafeDeceleration : t.groundAcceleration;
        }

        /// <summary>地面上推进一帧水平速度。</summary>
        public static PlanarVector StepGround(PlanarVector velocity, PlanarVector wishDirection, float targetSpeed,
            MovementTuning t, float dt)
        {
            bool hasInput = wishDirection.SqrMagnitude >= Epsilon;
            PlanarVector target = hasInput ? wishDirection * targetSpeed : PlanarVector.Zero;
            PlanarVector next = Approach(velocity, target, GroundRate(velocity, wishDirection, t), dt);
            if (!hasInput && next.Magnitude <= t.stopSnapSpeed) next = PlanarVector.Zero;
            return next;
        }

        /// <summary>空中推进一帧水平速度。没有输入时保持惯性。</summary>
        public static PlanarVector StepAir(PlanarVector velocity, PlanarVector wishDirection, float targetSpeed,
            MovementTuning t, float dt)
        {
            if (wishDirection.SqrMagnitude < Epsilon) return velocity;
            return Approach(velocity, wishDirection * targetSpeed, t.airAcceleration, dt);
        }

        /// <summary>按指数规律从 current 靠向 target，与帧率无关。速率为 0 时保持不变。</summary>
        public static PlanarVector Approach(PlanarVector current, PlanarVector target, float rate, float dt)
        {
            if (rate <= 0f || dt <= 0f) return current;
            float keep = MathF.Exp(-rate * dt);
            return target + (current - target) * keep;
        }

        /// <summary>达到 height 米跳高所需的起跳速度。</summary>
        public static float JumpVelocity(float height, float gravity) =>
            MathF.Sqrt(2f * MathF.Max(0f, gravity) * MathF.Max(0f, height));

        /// <summary>
        /// 推进一帧竖直运动，返回本帧位移。恒定重力下用精确公式积分，
        /// 所以跳跃高度和滞空时间不随帧率变化。
        /// </summary>
        public static float IntegrateVertical(ref float verticalVelocity, float gravity, float dt)
        {
            float dy = verticalVelocity * dt - 0.5f * gravity * dt * dt;
            verticalVelocity -= gravity * dt;
            return dy;
        }

        /// <summary>
        /// 下蹲程度（0 = 站立，1 = 完全蹲下）向目标线性推进。
        /// 想站起来但头顶被挡住（canStand 为 false）时保持当前程度不变。
        /// </summary>
        public static float StepCrouch(float amount, bool wantCrouch, bool canStand, float transitionTime, float dt)
        {
            if (!wantCrouch && !canStand) return amount;
            float target = wantCrouch ? 1f : 0f;
            if (transitionTime <= 0f) return target;
            return MoveTowards(amount, target, dt / transitionTime);
        }

        /// <summary>从 fromSpeed 松开方向键，降到 threshold 以下的理论耗时（秒）。</summary>
        public static float ReleaseStopTime(float fromSpeed, float threshold, float releaseRate)
        {
            if (fromSpeed <= threshold) return 0f;
            if (releaseRate <= 0f || threshold <= 0f) return float.PositiveInfinity;
            return MathF.Log(fromSpeed / threshold) / releaseRate;
        }

        /// <summary>
        /// 以 fromSpeed 前进时按下反方向键（目标为反方向的 oppositeTargetSpeed），
        /// 速度降到 threshold 以下的理论耗时（秒）。
        /// 推导：v(t) = −S + (v₀ + S)·e^(−kt)，令 v(t) = 门槛求 t。
        /// </summary>
        public static float CounterStrafeStopTime(float fromSpeed, float oppositeTargetSpeed, float threshold, float rate)
        {
            if (fromSpeed <= threshold) return 0f;
            if (rate <= 0f || threshold + oppositeTargetSpeed <= 0f) return float.PositiveInfinity;
            return MathF.Log((fromSpeed + oppositeTargetSpeed) / (threshold + oppositeTargetSpeed)) / rate;
        }

        static float Lerp(float a, float b, float t) => a + (b - a) * t;
        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        static float MoveTowards(float current, float target, float maxDelta)
        {
            if (MathF.Abs(target - current) <= maxDelta) return target;
            return current + MathF.Sign(target - current) * maxDelta;
        }
    }
}
