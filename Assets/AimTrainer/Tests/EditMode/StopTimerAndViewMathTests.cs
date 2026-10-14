using AimTrainer.Movement;
using NUnit.Framework;

namespace AimTrainer.Tests
{
    public class StopTimerTests
    {
        /// <summary>全速跑 0.5 秒，然后松键或反向，一直模拟到停下来。</summary>
        static StopTimer Simulate(float fps, PlanarVector stopInput, float speedMultiplier = 1f)
        {
            var t = new MovementTuning();
            var timer = new StopTimer();
            float dt = 1f / fps;
            float target = t.runSpeed * speedMultiplier;
            var forward = new PlanarVector(0f, 1f);
            PlanarVector v = PlanarVector.Zero;

            for (float time = 0f; time < 0.5f; time += dt) Step(ref v, forward);
            for (float time = 0f; time < 0.5f; time += dt) Step(ref v, stopInput);
            return timer;

            void Step(ref PlanarVector velocity, PlanarVector wish)
            {
                PlanarVector before = velocity;
                velocity = MovementModel.StepGround(velocity, wish, target, t, dt);
                timer.Tick(before, velocity, wish, true, dt, t, target);
            }
        }

        [TestCase(60f)]
        [TestCase(144f)]
        [TestCase(360f)]
        public void Release_MeasurementMatchesTheory(float fps)
        {
            StopTimer timer = Simulate(fps, PlanarVector.Zero);
            Assert.That(timer.LastRelease.HasValue, Is.True);
            StopMeasurement m = timer.LastRelease.Value;
            Assert.That(m.Seconds, Is.EqualTo(m.PredictedSeconds).Within(0.003f));
            Assert.That(timer.LastCounterStrafe.HasValue, Is.False);
        }

        [TestCase(60f)]
        [TestCase(144f)]
        [TestCase(360f)]
        public void CounterStrafe_MeasurementMatchesTheory(float fps)
        {
            StopTimer timer = Simulate(fps, new PlanarVector(0f, -1f));
            Assert.That(timer.LastCounterStrafe.HasValue, Is.True);
            StopMeasurement m = timer.LastCounterStrafe.Value;
            Assert.That(m.Seconds, Is.EqualTo(m.PredictedSeconds).Within(0.003f));
        }

        /// <summary>松开 D 后隔几帧才按下 A（真实操作几乎总是这样），仍应记为反向急停。</summary>
        [TestCase(144f, 1)]
        [TestCase(144f, 3)]
        [TestCase(240f, 2)]
        [TestCase(240f, 5)]
        public void CounterStrafe_WithNoInputGap_IsStillRecorded(float fps, int gapFrames)
        {
            var t = new MovementTuning();
            var timer = new StopTimer();
            float dt = 1f / fps;
            PlanarVector v = PlanarVector.Zero;
            var forward = new PlanarVector(0f, 1f);
            for (float time = 0f; time < 0.5f; time += dt) Step(timer, t, ref v, forward, dt);
            for (int i = 0; i < gapFrames; i++) Step(timer, t, ref v, PlanarVector.Zero, dt);
            for (float time = 0f; time < 0.5f; time += dt) Step(timer, t, ref v, -forward, dt);

            Assert.That(timer.LastCounterStrafe.HasValue, Is.True);
            Assert.That(timer.LastRelease.HasValue, Is.False);
            StopMeasurement m = timer.LastCounterStrafe.Value;
            Assert.That(m.GapSeconds, Is.EqualTo(gapFrames * dt).Within(1e-4f));
            Assert.That(m.Seconds, Is.EqualTo(m.PredictedSeconds).Within(0.003f));
            Assert.That(m.Seconds, Is.LessThan(t.TheoreticalReleaseStopTime));
        }

        /// <summary>反向急停途中卡了一帧（50 毫秒），速度在这一帧内穿过 0，测量仍应接近理论值。</summary>
        [Test]
        public void CounterStrafe_WithLongFrame_StillMatchesTheory()
        {
            var t = new MovementTuning();
            var timer = new StopTimer();
            const float dt = 1f / 144f;
            PlanarVector v = PlanarVector.Zero;
            var forward = new PlanarVector(0f, 1f);
            for (float time = 0f; time < 0.5f; time += dt) Step(timer, t, ref v, forward, dt);
            Step(timer, t, ref v, -forward, dt);
            Step(timer, t, ref v, -forward, 0.05f);
            for (int i = 0; i < 20; i++) Step(timer, t, ref v, -forward, dt);

            Assert.That(timer.LastCounterStrafe.HasValue, Is.True);
            StopMeasurement m = timer.LastCounterStrafe.Value;
            Assert.That(m.Seconds, Is.EqualTo(m.PredictedSeconds).Within(0.008f));
        }

        [Test]
        public void PressingForwardAgain_CancelsTheMeasurement()
        {
            var t = new MovementTuning();
            var timer = new StopTimer();
            const float dt = 1f / 144f;
            PlanarVector v = PlanarVector.Zero;
            var forward = new PlanarVector(0f, 1f);
            for (float time = 0f; time < 0.5f; time += dt) Step(timer, t, ref v, forward, dt);
            for (int i = 0; i < 3; i++) Step(timer, t, ref v, PlanarVector.Zero, dt);
            for (float time = 0f; time < 0.5f; time += dt) Step(timer, t, ref v, forward, dt);

            Assert.That(timer.IsMeasuring, Is.False);
            Assert.That(timer.LastRelease.HasValue, Is.False);
            Assert.That(timer.LastCounterStrafe.HasValue, Is.False);
        }

        static void Step(StopTimer timer, MovementTuning t, ref PlanarVector velocity, PlanarVector wish, float dt)
        {
            PlanarVector before = velocity;
            velocity = MovementModel.StepGround(velocity, wish, t.runSpeed, t, dt);
            timer.Tick(before, velocity, wish, true, dt, t, t.runSpeed);
        }

        [Test]
        public void WalkingSpeedStops_AreNotMeasured()
        {
            StopTimer timer = Simulate(144f, PlanarVector.Zero, speedMultiplier: 0.52f);
            Assert.That(timer.LastRelease.HasValue, Is.False);
        }

        [Test]
        public void TurningSideways_IsNotACounterStrafe()
        {
            StopTimer timer = Simulate(144f, new PlanarVector(1f, 0f));
            Assert.That(timer.LastCounterStrafe.HasValue, Is.False);
            Assert.That(timer.LastRelease.HasValue, Is.False);
        }
    }

    public class ViewMathTests
    {
        [Test]
        public void Sensitivity_MatchesValorantYaw()
        {
            Assert.That(ViewMath.DegreesPerCount(0.4f), Is.EqualTo(0.028f).Within(1e-6f));
            Assert.That(ViewMath.CountsPer360(0.4f), Is.EqualTo(12857.14f).Within(0.1f));
            Assert.That(ViewMath.CentimetersPer360(0.4f, 800f), Is.EqualTo(40.82f).Within(0.01f));
        }

        [Test]
        public void HorizontalFov103_At16by9_IsAbout70Vertical()
        {
            Assert.That(ViewMath.HorizontalToVerticalFov(103f, 16f / 9f), Is.EqualTo(70.53f).Within(0.05f));
            Assert.That(ViewMath.HorizontalToVerticalFov(90f, 1f), Is.EqualTo(90f).Within(1e-3f));
        }
    }
}
