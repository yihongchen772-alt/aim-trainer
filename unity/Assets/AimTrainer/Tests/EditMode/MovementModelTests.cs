using System;
using AimTrainer.Movement;
using NUnit.Framework;

namespace AimTrainer.Tests
{
    /// <summary>
    /// 移动核心算法的单元测试。Unity 里打开 Window → General → Test Runner → EditMode 运行。
    /// 每个手感指标都在多个帧率下检查，保证 60 帧和 500 帧的手感一致。
    /// </summary>
    public class MovementModelTests
    {
        static readonly float[] FrameRates = { 30f, 60f, 144f, 240f, 500f };

        static float SimulateStop(MovementTuning t, float fps, bool counterStrafe)
        {
            float dt = 1f / fps;
            var velocity = new PlanarVector(0f, t.runSpeed);
            PlanarVector wish = counterStrafe ? new PlanarVector(0f, -1f) : PlanarVector.Zero;
            float elapsed = 0f;
            while (velocity.Magnitude > t.accurateSpeedThreshold)
            {
                velocity = MovementModel.StepGround(velocity, wish, t.runSpeed, t, dt);
                elapsed += dt;
                if (elapsed > 5f) Assert.Fail("速度一直没有降到门槛以下");
            }
            return elapsed;
        }

        [Test]
        public void DefaultTuning_ReleaseStopTakesAbout68ms()
        {
            var t = new MovementTuning();
            Assert.That(t.TheoreticalReleaseStopTime, Is.EqualTo(0.0676f).Within(0.001f));
        }

        [Test]
        public void DefaultTuning_CounterStrafeTakesAbout33ms()
        {
            var t = new MovementTuning();
            Assert.That(t.TheoreticalCounterStrafeStopTime, Is.EqualTo(0.0334f).Within(0.001f));
        }

        [Test]
        public void CounterStrafe_IsMuchFasterThanRelease()
        {
            var t = new MovementTuning();
            Assert.That(t.TheoreticalCounterStrafeStopTime, Is.LessThan(t.TheoreticalReleaseStopTime * 0.6f));
        }

        [Test]
        public void ReleaseStop_MatchesTheory_AtEveryFrameRate([ValueSource(nameof(FrameRates))] float fps)
        {
            var t = new MovementTuning();
            float measured = SimulateStop(t, fps, counterStrafe: false);
            Assert.That(measured, Is.EqualTo(t.TheoreticalReleaseStopTime).Within(1f / fps + 1e-4f));
        }

        [Test]
        public void CounterStrafeStop_MatchesTheory_AtEveryFrameRate([ValueSource(nameof(FrameRates))] float fps)
        {
            var t = new MovementTuning();
            float measured = SimulateStop(t, fps, counterStrafe: true);
            Assert.That(measured, Is.EqualTo(t.TheoreticalCounterStrafeStopTime).Within(1f / fps + 1e-4f));
        }

        [Test]
        public void Acceleration_IsFrameRateIndependent()
        {
            var t = new MovementTuning();
            var wish = new PlanarVector(1f, 0f);
            float reference = -1f;
            foreach (float fps in FrameRates)
            {
                // 0.5 秒在所有测试帧率下都是整数帧。
                int frames = (int)MathF.Round(0.5f * fps);
                PlanarVector v = PlanarVector.Zero;
                for (int i = 0; i < frames; i++) v = MovementModel.StepGround(v, wish, t.runSpeed, t, 1f / fps);
                if (reference < 0f) reference = v.Magnitude;
                Assert.That(v.Magnitude, Is.EqualTo(reference).Within(1e-3f), $"{fps} 帧");
            }
            Assert.That(reference, Is.EqualTo(t.runSpeed * (1f - MathF.Exp(-t.groundAcceleration * 0.5f))).Within(1e-3f));
        }

        [Test]
        public void Release_EventuallyStopsCompletely()
        {
            var t = new MovementTuning();
            var v = new PlanarVector(t.runSpeed, 0f);
            for (int i = 0; i < 200; i++) v = MovementModel.StepGround(v, PlanarVector.Zero, t.runSpeed, t, 1f / 144f);
            Assert.That(v, Is.EqualTo(PlanarVector.Zero));
        }

        [Test]
        public void Jump_ReachesConfiguredHeight_AtEveryFrameRate([ValueSource(nameof(FrameRates))] float fps)
        {
            var t = new MovementTuning();
            float dt = 1f / fps;
            float vy = t.JumpVelocity;
            float y = 0f;
            float peak = 0f;
            float airTime = 0f;
            do
            {
                y += MovementModel.IntegrateVertical(ref vy, t.gravity, dt);
                peak = MathF.Max(peak, y);
                airTime += dt;
            } while (y > 0f);

            Assert.That(peak, Is.EqualTo(t.jumpHeight).Within(0.01f));
            Assert.That(airTime, Is.EqualTo(2f * t.JumpVelocity / t.gravity).Within(dt + 1e-4f));
        }

        [Test]
        public void TargetSpeed_WalkAndCrouchUseTheSlowerMultiplier()
        {
            var t = new MovementTuning();
            Assert.That(MovementModel.TargetSpeed(t, false, 0f), Is.EqualTo(5.4f).Within(1e-4f));
            Assert.That(MovementModel.TargetSpeed(t, true, 0f), Is.EqualTo(5.4f * 0.52f).Within(1e-4f));
            Assert.That(MovementModel.TargetSpeed(t, false, 1f), Is.EqualTo(5.4f * 0.42f).Within(1e-4f));
            Assert.That(MovementModel.TargetSpeed(t, true, 1f), Is.EqualTo(5.4f * 0.42f).Within(1e-4f));
        }

        [Test]
        public void GroundRate_PicksCounterStrafeOnlyWhenPressingAgainstVelocity()
        {
            var t = new MovementTuning { groundAcceleration = 10f, releaseDeceleration = 20f, counterStrafeDeceleration = 30f };
            var movingRight = new PlanarVector(5f, 0f);
            Assert.That(MovementModel.GroundRate(movingRight, PlanarVector.Zero, t), Is.EqualTo(20f));
            Assert.That(MovementModel.GroundRate(movingRight, new PlanarVector(-1f, 0f), t), Is.EqualTo(30f));
            Assert.That(MovementModel.GroundRate(movingRight, new PlanarVector(1f, 0f), t), Is.EqualTo(10f));
            Assert.That(MovementModel.GroundRate(movingRight, new PlanarVector(0f, 1f), t), Is.EqualTo(10f));
        }

        [Test]
        public void Air_KeepsMomentumWithoutInput()
        {
            var t = new MovementTuning();
            var v = new PlanarVector(3f, 4f);
            Assert.That(MovementModel.StepAir(v, PlanarVector.Zero, t.runSpeed, t, 0.1f), Is.EqualTo(v));
        }

        [Test]
        public void WishDirection_FollowsYaw()
        {
            AssertClose(MovementModel.WishDirection(1f, 0f, 0f), new PlanarVector(0f, 1f));
            AssertClose(MovementModel.WishDirection(1f, 0f, 90f), new PlanarVector(1f, 0f));
            AssertClose(MovementModel.WishDirection(0f, 1f, 0f), new PlanarVector(1f, 0f));
            AssertClose(MovementModel.WishDirection(0f, -1f, 0f), new PlanarVector(-1f, 0f));
            Assert.That(MovementModel.WishDirection(1f, 1f, 37f).Magnitude, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(MovementModel.WishDirection(0f, 0f, 37f), Is.EqualTo(PlanarVector.Zero));
        }

        [Test]
        public void Crouch_StaysDownWhenHeadIsBlocked()
        {
            Assert.That(MovementModel.StepCrouch(1f, wantCrouch: false, canStand: false, 0.1f, 0.05f), Is.EqualTo(1f));
            Assert.That(MovementModel.StepCrouch(1f, wantCrouch: false, canStand: true, 0.1f, 0.05f), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(MovementModel.StepCrouch(0f, wantCrouch: true, canStand: false, 0.1f, 0.2f), Is.EqualTo(1f));
            Assert.That(MovementModel.StepCrouch(0.3f, wantCrouch: true, canStand: true, 0f, 0.01f), Is.EqualTo(1f));
        }

        static void AssertClose(PlanarVector actual, PlanarVector expected)
        {
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(1e-5f), "X");
            Assert.That(actual.Z, Is.EqualTo(expected.Z).Within(1e-5f), "Z");
        }
    }
}
