using System;
using AimTrainer.Combat;
using AimTrainer.Movement;
using AimTrainer.Weapons;
using NUnit.Framework;

namespace AimTrainer.Tests
{
    public class WeaponMathTests
    {
        static readonly MovementTuning Move = new MovementTuning();

        static float MoveSpread(WeaponStats w, float speed, bool grounded = true) =>
            WeaponMath.MovementSpread(w, speed, grounded, Move.accurateSpeedThreshold, Move.WalkSpeed, Move.runSpeed);

        [Test]
        public void ThreeAccuracyTiers_StandingWalkingRunning()
        {
            var w = new WeaponStats();
            // 急停后（速度 ≤ 1.6）：没有移动惩罚。
            Assert.That(MoveSpread(w, 0f), Is.EqualTo(0f));
            Assert.That(MoveSpread(w, Move.accurateSpeedThreshold), Is.EqualTo(0f));
            // 静步满速：静步惩罚。
            Assert.That(MoveSpread(w, Move.WalkSpeed), Is.EqualTo(w.walkSpread).Within(1e-4f));
            // 跑步满速：跑动惩罚。
            Assert.That(MoveSpread(w, Move.runSpeed), Is.EqualTo(w.runSpread).Within(1e-4f));
            // 空中：空中惩罚。
            Assert.That(MoveSpread(w, 0f, grounded: false), Is.EqualTo(w.airSpread));
            Assert.That(w.walkSpread, Is.LessThan(w.runSpread));
        }

        [Test]
        public void MovementSpread_GrowsMonotonicallyWithSpeed()
        {
            var w = new WeaponStats();
            float previous = -1f;
            for (float speed = 0f; speed <= Move.runSpeed + 0.5f; speed += 0.05f)
            {
                float spread = MoveSpread(w, speed);
                Assert.That(spread, Is.GreaterThanOrEqualTo(previous - 1e-5f), $"速度 {speed}");
                previous = spread;
            }
        }

        [Test]
        public void Tier_MatchesSpeedBands()
        {
            Assert.That(WeaponMath.Tier(1f, true, 1.6f, 2.8f), Is.EqualTo(AccuracyTier.Standing));
            Assert.That(WeaponMath.Tier(2.8f, true, 1.6f, 2.8f), Is.EqualTo(AccuracyTier.Walking));
            Assert.That(WeaponMath.Tier(5.4f, true, 1.6f, 2.8f), Is.EqualTo(AccuracyTier.Running));
            Assert.That(WeaponMath.Tier(0f, false, 1.6f, 2.8f), Is.EqualTo(AccuracyTier.Airborne));
        }

        [Test]
        public void BaseSpread_FirstShotIsTight_SprayGrowsAndCaps()
        {
            var w = new WeaponStats();
            Assert.That(WeaponMath.BaseSpread(w, 1, 0f), Is.EqualTo(w.firstShotSpread).Within(1e-6f));
            Assert.That(WeaponMath.BaseSpread(w, 2, 0f), Is.GreaterThan(w.firstShotSpread));
            Assert.That(WeaponMath.BaseSpread(w, 100, 0f), Is.EqualTo(w.maxSpraySpread).Within(1e-5f));
            Assert.That(WeaponMath.BaseSpread(w, 1, 1f), Is.EqualTo(w.firstShotSpread * w.crouchMultiplier).Within(1e-6f));
        }

        [Test]
        public void RecoilPattern_FirstShotsAreStraight_ThenClimbThenSway()
        {
            var w = new WeaponStats();
            WeaponMath.RecoilPattern(w, 1, out float p1, out float y1);
            WeaponMath.RecoilPattern(w, 2, out float p2, out _);
            Assert.That(p1, Is.EqualTo(0f));
            Assert.That(y1, Is.EqualTo(0f));
            Assert.That(p2, Is.EqualTo(0f));

            WeaponMath.RecoilPattern(w, w.climbStartShot, out float p3, out _);
            Assert.That(p3, Is.EqualTo(w.climbPerShot).Within(1e-6f));

            WeaponMath.RecoilPattern(w, 40, out float pMax, out _);
            Assert.That(pMax, Is.EqualTo(w.climbPerShot * w.climbShots).Within(1e-5f));

            bool swayed = false;
            for (int i = w.swayStartShot; i < w.swayStartShot + 6; i++)
            {
                WeaponMath.RecoilPattern(w, i, out _, out float yaw);
                Assert.That(Math.Abs(yaw), Is.LessThanOrEqualTo(w.swayAmplitude + 1e-5f));
                if (Math.Abs(yaw) > 0.1f) swayed = true;
            }
            Assert.That(swayed, Is.True);
        }

        [Test]
        public void Damage_HeadshotKillsA150HpTarget_BodyNeedsFourShots()
        {
            var w = new WeaponStats();
            Assert.That(WeaponMath.Damage(w, HitZone.Head, 10f), Is.GreaterThanOrEqualTo(150f));
            float body = WeaponMath.Damage(w, HitZone.Body, 10f);
            Assert.That(Math.Ceiling(150f / body), Is.EqualTo(4));
            Assert.That(WeaponMath.Damage(w, HitZone.Legs, 10f), Is.LessThan(body));
        }

        [Test]
        public void Damage_FalloffAppliesOnlyBeyondDistance()
        {
            var w = new WeaponStats { falloffDistance = 30f, falloffMultiplier = 0.8f };
            Assert.That(WeaponMath.Damage(w, HitZone.Body, 29f), Is.EqualTo(w.bodyDamage));
            Assert.That(WeaponMath.Damage(w, HitZone.Body, 31f), Is.EqualTo(w.bodyDamage * 0.8f).Within(1e-4f));
        }

        [Test]
        public void SampleDisc_StaysInsideRadius_AndCoversTheDisc()
        {
            var rng = new Random(1);
            float maxR = 0f;
            int outer = 0;
            for (int i = 0; i < 20000; i++)
            {
                WeaponMath.SampleDisc(rng, 2f, out float x, out float y);
                float r = MathF.Sqrt(x * x + y * y);
                Assert.That(r, Is.LessThanOrEqualTo(2f + 1e-4f));
                maxR = MathF.Max(maxR, r);
                if (r > 2f * MathF.Sqrt(0.5f)) outer++;
            }
            Assert.That(maxR, Is.GreaterThan(1.95f));
            // 均匀分布：半径大于 R/√2 的环面积占一半。
            Assert.That(outer / 20000f, Is.EqualTo(0.5f).Within(0.02f));
        }
    }

    public class SprayTrackerTests
    {
        [Test]
        public void HoldingFire_CountsUp()
        {
            var w = new WeaponStats();
            var spray = new SprayTracker();
            double t = 0;
            for (int i = 1; i <= 10; i++)
            {
                Assert.That(spray.RegisterShot(t, w), Is.EqualTo(i));
                t += w.ShotInterval;
            }
        }

        [Test]
        public void SlowTaps_AreAlwaysFirstShots()
        {
            var w = new WeaponStats();
            var spray = new SprayTracker();
            for (int i = 0; i < 5; i++) Assert.That(spray.RegisterShot(i * 0.4, w), Is.EqualTo(1));
        }

        [Test]
        public void ShortPause_RecoversPartially()
        {
            var w = new WeaponStats();
            var spray = new SprayTracker();
            double t = 0;
            for (int i = 0; i < 10; i++)
            {
                spray.RegisterShot(t, w);
                t += w.ShotInterval;
            }
            // 停 0.2 秒：(0.2 − 0.12) × 30 = 2.4 发恢复，10 − 2.4 = 7.6，下一发是第 9 发。
            int next = spray.RegisterShot(t - w.ShotInterval + 0.2, w);
            Assert.That(next, Is.EqualTo(9));
        }
    }

    public class FireClockTests
    {
        static int TotalShots(float fps, float seconds, float interval, bool automatic)
        {
            var clock = new FireClock();
            int total = 0;
            int frames = (int)Math.Round(seconds * fps);
            for (int i = 0; i <= frames; i++) total += clock.Tick(i / (double)fps, true, interval, automatic);
            return total;
        }

        [TestCase(30f)]
        [TestCase(60f)]
        [TestCase(144f)]
        [TestCase(500f)]
        public void FireRate_IsFrameRateIndependent(float fps)
        {
            var w = new WeaponStats();
            // 按住 1 秒：第 0 秒打第一发，之后每 1/9.75 秒一发，共 10 发（0, 0.103, …, 0.923）。
            Assert.That(TotalShots(fps, 1f, w.ShotInterval, true), Is.EqualTo(10));
        }

        [Test]
        public void SemiAuto_OneShotPerClick_AndRespectsInterval()
        {
            var clock = new FireClock();
            Assert.That(clock.Tick(0.0, true, 0.25f, false), Is.EqualTo(1));
            Assert.That(clock.Tick(0.01, true, 0.25f, false), Is.EqualTo(0));
            Assert.That(clock.Tick(0.02, false, 0.25f, false), Is.EqualTo(0));
            Assert.That(clock.Tick(0.1, true, 0.25f, false), Is.EqualTo(0), "间隔没到，点了也不开枪");
            Assert.That(clock.Tick(0.11, false, 0.25f, false), Is.EqualTo(0));
            Assert.That(clock.Tick(0.3, true, 0.25f, false), Is.EqualTo(1));
        }

        [Test]
        public void ReleasingTrigger_DoesNotBankShots()
        {
            var clock = new FireClock();
            Assert.That(clock.Tick(0.0, true, 0.1f, true), Is.EqualTo(1));
            Assert.That(clock.Tick(0.05, false, 0.1f, true), Is.EqualTo(0));
            // 松开 2 秒后再按：只打一发，不会把松开期间“攒”的子弹一次打出来。
            Assert.That(clock.Tick(2.0, true, 0.1f, true), Is.EqualTo(1));
        }

        [Test]
        public void LongHitch_IsCapped()
        {
            var clock = new FireClock();
            clock.Tick(0.0, true, 0.1f, true);
            Assert.That(clock.Tick(1.0, true, 0.1f, true), Is.EqualTo(FireClock.MaxShotsPerTick));
            Assert.That(clock.Tick(1.01, true, 0.1f, true), Is.LessThanOrEqualTo(1));
        }
    }
}
