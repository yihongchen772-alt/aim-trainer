using System;
using AimTrainer.Bots;
using AimTrainer.Game;
using NUnit.Framework;

namespace AimTrainer.Tests
{
    public class BotMathTests
    {
        [Test]
        public void VisionCone_RespectsAngleAndRange()
        {
            // 面朝 +Z，半角 60°。
            Assert.That(BotMath.InVisionCone(0, 0, 1, 0, 0, 10, 60f, 50f), Is.True, "正前方");
            Assert.That(BotMath.InVisionCone(0, 0, 1, 10, 0, 10, 60f, 50f), Is.True, "右前 45°");
            Assert.That(BotMath.InVisionCone(0, 0, 1, 10, 0, 1, 60f, 50f), Is.False, "右侧约 84°");
            Assert.That(BotMath.InVisionCone(0, 0, 1, 0, 0, -10, 60f, 50f), Is.False, "背后");
            Assert.That(BotMath.InVisionCone(0, 0, 1, 0, 0, 60, 60f, 50f), Is.False, "超出距离");
            Assert.That(BotMath.InVisionCone(0, 0, 1, 0, 0, -10, 180f, 50f), Is.True, "半角 180° 时背后也能看到");
        }

        [Test]
        public void ReactionTimer_WaitsReactionTimeBeforeReady()
        {
            var timer = new ReactionTimer(new Random(3), 0.3f, 0f, 0.25f);
            float t = 0f;
            while (!timer.Tick(true, 0.05f)) t += 0.05f;
            Assert.That(t + 0.05f, Is.EqualTo(0.3f).Within(0.051f));
        }

        [Test]
        public void ReactionTimer_ShortLossIsRemembered_LongLossResets()
        {
            var timer = new ReactionTimer(new Random(3), 0.3f, 0f, 0.25f);
            for (int i = 0; i < 8; i++) timer.Tick(true, 0.05f);
            Assert.That(timer.Ready, Is.True);

            timer.Tick(false, 0.2f);
            Assert.That(timer.Tick(true, 0f), Is.True, "离开视线 0.2 秒（小于记忆时间）后回来，立刻可以开枪");

            timer.Tick(false, 0.3f);
            Assert.That(timer.Tick(true, 0f), Is.False, "离开 0.3 秒后需要重新反应");
        }

        [Test]
        public void ReactionTimer_JitterStaysInRange()
        {
            var timer = new ReactionTimer(new Random(5), 0.3f, 0.2f, 0f);
            for (int i = 0; i < 200; i++)
            {
                timer.Reset();
                Assert.That(timer.CurrentReaction, Is.InRange(0.24f, 0.36f));
            }
        }

        [Test]
        public void HitChance_DropsWhenTargetRunsOrJumpsOrIsFar()
        {
            float standing = BotMath.AdjustedHitChance(0.5f, 0f, 5.4f, true, 0f, 10f);
            Assert.That(standing, Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(BotMath.AdjustedHitChance(0.5f, 5.4f, 5.4f, true, 0f, 10f), Is.EqualTo(0.35f).Within(1e-5f));
            Assert.That(BotMath.AdjustedHitChance(0.5f, 0f, 5.4f, false, 0f, 10f), Is.LessThan(standing));
            Assert.That(BotMath.AdjustedHitChance(0.5f, 0f, 5.4f, true, 0f, 40f), Is.LessThan(standing));
        }

        [Test]
        public void RollShot_MatchesConfiguredRates()
        {
            var rng = new Random(11);
            int hits = 0, heads = 0;
            const int n = 20000;
            for (int i = 0; i < n; i++)
            {
                BotShotOutcome o = BotMath.RollShot(rng, 0.6f, 0.25f);
                if (o == BotShotOutcome.Miss) continue;
                hits++;
                if (o == BotShotOutcome.Head) heads++;
            }
            Assert.That(hits / (float)n, Is.EqualTo(0.6f).Within(0.02f));
            Assert.That(heads / (float)hits, Is.EqualTo(0.25f).Within(0.02f));
        }

        [Test]
        public void DifficultyPresets_GetHarderInOrder()
        {
            BotSkill easy = BotSkill.For(BotDifficulty.Easy);
            BotSkill normal = BotSkill.For(BotDifficulty.Normal);
            BotSkill hard = BotSkill.For(BotDifficulty.Hard);
            BotSkill pro = BotSkill.For(BotDifficulty.Pro);
            Assert.That(normal.reactionTime, Is.EqualTo(0.3f), "普通难度的反应时间是 0.3 秒");
            Assert.That(easy.reactionTime, Is.GreaterThan(normal.reactionTime));
            Assert.That(hard.reactionTime, Is.LessThan(normal.reactionTime));
            Assert.That(pro.reactionTime, Is.LessThan(hard.reactionTime));
            Assert.That(pro.hitChance, Is.GreaterThan(hard.hitChance));
            Assert.That(hard.hitChance, Is.GreaterThan(normal.hitChance));
            Assert.That(normal.hitChance, Is.GreaterThan(easy.hitChance));
        }
    }

    public class SessionStatsTests
    {
        [Test]
        public void Rates_AreComputedFromShots()
        {
            var s = new SessionStats();
            s.RecordShot(true, true, 150f);
            s.RecordShot(true, false, 40f);
            s.RecordShot(false, false, 0f);
            s.RecordShot(false, false, 0f);
            Assert.That(s.Accuracy, Is.EqualTo(0.5f));
            Assert.That(s.HeadshotRate, Is.EqualTo(0.5f));
            Assert.That(s.DamageDealt, Is.EqualTo(190f));
        }

        [Test]
        public void KillTimes_AverageIgnoresNegative()
        {
            var s = new SessionStats();
            s.RecordKill(0.4f, true);
            s.RecordKill(0.6f, false);
            s.RecordKill(-1f, false);
            Assert.That(s.Kills, Is.EqualTo(3));
            Assert.That(s.OneTapKills, Is.EqualTo(1));
            Assert.That(s.AverageKillTime, Is.EqualTo(0.5f).Within(1e-6f));
        }

        [Test]
        public void RoundTimes_TrackBestAndLast()
        {
            var s = new SessionStats();
            s.RecordRoundCleared(12f);
            s.RecordRoundCleared(9.5f);
            s.RecordRoundCleared(11f);
            Assert.That(s.RoundsCleared, Is.EqualTo(3));
            Assert.That(s.BestRoundTime, Is.EqualTo(9.5f));
            Assert.That(s.LastRoundTime, Is.EqualTo(11f));
        }

        [Test]
        public void Score_RewardsAccuracy()
        {
            var accurate = new SessionStats();
            var sloppy = new SessionStats();
            for (int i = 0; i < 10; i++)
            {
                accurate.RecordShot(true, false, 40f);
                accurate.RecordKill(0.5f, false);
                sloppy.RecordShot(true, false, 40f);
                sloppy.RecordShot(false, false, 0f);
                sloppy.RecordShot(false, false, 0f);
                sloppy.RecordKill(0.5f, false);
            }
            Assert.That(accurate.Score, Is.GreaterThan(sloppy.Score));
        }
    }
}
