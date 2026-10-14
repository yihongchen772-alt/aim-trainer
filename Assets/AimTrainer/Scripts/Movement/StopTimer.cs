using System;

namespace AimTrainer.Movement
{
    public enum StopKind
    {
        /// <summary>松开所有方向键。</summary>
        Release,

        /// <summary>按下与移动方向相反的键（中间允许有几帧没有输入的空档）。</summary>
        CounterStrafe,
    }

    /// <summary>一次急停的测量结果。</summary>
    public readonly struct StopMeasurement
    {
        public readonly StopKind Kind;

        /// <summary>从开始急停到速度降到精准门槛以下的实测耗时（秒）。</summary>
        public readonly float Seconds;

        /// <summary>按当前参数算出的理论耗时（秒），用来和实测值对照。</summary>
        public readonly float PredictedSeconds;

        /// <summary>开始急停时的水平速度。</summary>
        public readonly float StartSpeed;

        /// <summary>反向急停时，松开原方向键到按下反方向键之间没有输入的时间（秒）。松键急停为 0。</summary>
        public readonly float GapSeconds;

        public StopMeasurement(StopKind kind, float seconds, float predictedSeconds, float startSpeed, float gapSeconds)
        {
            Kind = kind;
            Seconds = seconds;
            PredictedSeconds = predictedSeconds;
            StartSpeed = startSpeed;
            GapSeconds = gapSeconds;
        }
    }

    /// <summary>
    /// 测量“从全速开始急停，到速度降到精准门槛以下”花了多久，用来验收急停手感。
    ///
    /// 一次急停从“不再朝原方向按键”开始，有两种结束方式：
    /// - 松键急停：一直没有方向输入。
    /// - 反向急停：按下了反方向键。手指不可能在同一帧松开 D、按下 A，中间常有几帧空档，
    ///   所以先按松键开始计时，一旦按下反方向键就改记为反向急停，空档时间也算在内并单独显示。
    /// 重新朝原方向或侧向按键会取消这次测量。
    ///
    /// 只在地面上、并且急停前速度接近跑步速度时才开始测量，保证每次结果可以互相比较。
    /// 越过门槛的那一帧里按线性插值估算精确时刻，所以结果基本不受帧率影响。
    /// </summary>
    public sealed class StopTimer
    {
        /// <summary>开始测量所需的最低速度，占跑步速度的比例。</summary>
        public const float MinStartSpeedFraction = 0.95f;

        /// <summary>输入方向与原移动方向夹角的余弦不超过这个值时，算作反方向（约 134° 以上）。</summary>
        const float OppositeDotLimit = -0.7f;

        bool measuring;
        StopKind kind;
        PlanarVector startDirection;
        float elapsed;
        float gap;
        float startSpeed;
        float predicted;

        public bool IsMeasuring => measuring;
        public StopMeasurement? LastRelease { get; private set; }
        public StopMeasurement? LastCounterStrafe { get; private set; }

        /// <summary>
        /// 每帧在速度更新之后调用一次。
        /// </summary>
        /// <param name="before">本帧更新前的水平速度。</param>
        /// <param name="after">本帧更新后（含碰撞修正）的水平速度。</param>
        /// <param name="wishDirection">本帧的输入方向（单位向量，没有输入时为零）。</param>
        /// <param name="grounded">本帧开始时是否站在地面上。</param>
        /// <param name="targetSpeed">本帧的目标移动速度，用来计算反向急停的理论耗时。</param>
        public void Tick(PlanarVector before, PlanarVector after, PlanarVector wishDirection, bool grounded,
            float dt, MovementTuning t, float targetSpeed)
        {
            if (!grounded || dt <= 0f)
            {
                measuring = false;
                return;
            }

            bool hasInput = !wishDirection.IsZero;
            float speedBefore = before.Magnitude;
            float threshold = t.accurateSpeedThreshold;

            if (measuring && hasInput)
            {
                bool opposite = PlanarVector.Dot(wishDirection, startDirection) <= OppositeDotLimit;
                if (!opposite)
                {
                    measuring = false;
                }
                else if (kind == StopKind.Release)
                {
                    // 松开原方向键几帧后才按下反方向键：改记为反向急停，空档时间计入总耗时。
                    kind = StopKind.CounterStrafe;
                    gap = elapsed;
                    predicted = elapsed + MovementModel.CounterStrafeStopTime(
                        PlanarVector.Dot(before, startDirection), targetSpeed, threshold, t.counterStrafeDeceleration);
                }
            }

            if (!measuring && speedBefore > threshold && speedBefore >= t.runSpeed * MinStartSpeedFraction)
            {
                PlanarVector direction = before.Normalized;
                if (!hasInput)
                {
                    Begin(StopKind.Release, direction, speedBefore,
                        MovementModel.ReleaseStopTime(speedBefore, threshold, t.releaseDeceleration));
                }
                else if (PlanarVector.Dot(wishDirection, direction) <= OppositeDotLimit)
                {
                    Begin(StopKind.CounterStrafe, direction, speedBefore,
                        MovementModel.CounterStrafeStopTime(speedBefore, targetSpeed, threshold, t.counterStrafeDeceleration));
                }
            }

            if (!measuring) return;

            // 反向急停时速度可能在一帧之内穿过 0 变成反方向，所以沿原方向取带符号的分量判断；
            // 松键时速度方向不变，带符号分量就等于速度大小。
            float alongBefore = PlanarVector.Dot(before, startDirection);
            float alongAfter = PlanarVector.Dot(after, startDirection);
            if (alongAfter > threshold && after.Magnitude > threshold)
            {
                elapsed += dt;
                return;
            }

            // 速度在这一帧内越过门槛：按线性插值估算越过的时刻。
            float fraction = alongBefore > alongAfter
                ? Math.Clamp((alongBefore - threshold) / (alongBefore - alongAfter), 0f, 1f)
                : 1f;
            elapsed += dt * fraction;

            var result = new StopMeasurement(kind, elapsed, predicted, startSpeed, gap);
            if (kind == StopKind.Release) LastRelease = result;
            else LastCounterStrafe = result;
            measuring = false;
        }

        /// <summary>放弃当前正在进行的测量（保留之前的结果）。</summary>
        public void Cancel() => measuring = false;

        public void Reset()
        {
            measuring = false;
            LastRelease = null;
            LastCounterStrafe = null;
        }

        void Begin(StopKind stopKind, PlanarVector direction, float speed, float predictedSeconds)
        {
            measuring = true;
            kind = stopKind;
            startDirection = direction;
            elapsed = 0f;
            gap = 0f;
            startSpeed = speed;
            predicted = predictedSeconds;
        }
    }
}
