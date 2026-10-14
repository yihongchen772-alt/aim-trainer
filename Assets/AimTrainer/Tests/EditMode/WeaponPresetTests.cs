using AimTrainer.Combat;
using AimTrainer.Weapons;
using NUnit.Framework;

namespace AimTrainer.Tests
{
    public class WeaponPresetTests
    {
        const float BotHealth = 150f;

        [Test]
        public void Vandal_OneTapsAtAnyRange()
        {
            WeaponStats w = WeaponPresets.Create(WeaponPresets.Vandal);
            Assert.That(WeaponMath.Damage(w, HitZone.Head, 5f), Is.GreaterThanOrEqualTo(BotHealth));
            Assert.That(WeaponMath.Damage(w, HitZone.Head, 60f), Is.GreaterThanOrEqualTo(BotHealth));
        }

        [Test]
        public void Phantom_FallsOffInTwoSteps()
        {
            WeaponStats w = WeaponPresets.Create(WeaponPresets.Phantom);
            float near = WeaponMath.Damage(w, HitZone.Head, 10f);
            float mid = WeaponMath.Damage(w, HitZone.Head, 20f);
            float far = WeaponMath.Damage(w, HitZone.Head, 40f);
            Assert.That(near, Is.EqualTo(156f));
            Assert.That(mid, Is.LessThan(near));
            Assert.That(far, Is.LessThan(mid));
            Assert.That(near, Is.GreaterThanOrEqualTo(BotHealth), "近距离一枪爆头");
            Assert.That(far, Is.LessThan(BotHealth), "远距离爆头打不死满护甲");
        }

        [Test]
        public void Spectre_NeedsTwoHeadshots_Sheriff_OneTaps()
        {
            WeaponStats spectre = WeaponPresets.Create(WeaponPresets.Spectre);
            Assert.That(WeaponMath.Damage(spectre, HitZone.Head, 10f), Is.LessThan(BotHealth));
            Assert.That(2 * WeaponMath.Damage(spectre, HitZone.Head, 10f), Is.GreaterThanOrEqualTo(BotHealth));

            WeaponStats sheriff = WeaponPresets.Create(WeaponPresets.Sheriff);
            Assert.That(sheriff.automatic, Is.False);
            Assert.That(WeaponMath.Damage(sheriff, HitZone.Head, 20f), Is.GreaterThanOrEqualTo(BotHealth));
        }

        [Test]
        public void EveryPreset_IsSane()
        {
            foreach (WeaponPresets.Entry entry in WeaponPresets.All)
            {
                WeaponStats w = WeaponPresets.Create(entry.Id);
                Assert.That(w.roundsPerSecond, Is.GreaterThan(0f), entry.Id);
                Assert.That(w.magazineSize, Is.GreaterThan(0), entry.Id);
                Assert.That(w.headDamage, Is.GreaterThan(w.bodyDamage), entry.Id);
                Assert.That(w.bodyDamage, Is.GreaterThanOrEqualTo(w.legDamage), entry.Id);
                Assert.That(w.walkSpread, Is.LessThan(w.runSpread), entry.Id);
                // 第一发不受后坐力影响，扫射时一定往上跳。
                WeaponMath.RecoilPattern(w, 1, out float p1, out float y1);
                Assert.That(p1, Is.EqualTo(0f), entry.Id);
                Assert.That(y1, Is.EqualTo(0f), entry.Id);
                WeaponMath.RecoilPattern(w, 8, out float p8, out _);
                Assert.That(p8, Is.GreaterThan(0f), entry.Id);
                Assert.That(WeaponPresets.TitleOf(entry.Id), Is.EqualTo(entry.Title));
            }
        }

        [Test]
        public void SprayTracker_RecoversBetweenSemiAutoShots()
        {
            // 手枪按射速连点时，连射计数会累积；停顿 0.5 秒后回到第一发。
            WeaponStats w = WeaponPresets.Create(WeaponPresets.Sheriff);
            var spray = new SprayTracker();
            Assert.That(spray.RegisterShot(0.0, w), Is.EqualTo(1));
            Assert.That(spray.RegisterShot(w.ShotInterval, w), Is.EqualTo(2));
            Assert.That(spray.RegisterShot(w.ShotInterval + 0.8, w), Is.EqualTo(1));
        }

        [Test]
        public void UnknownId_FallsBackToVandal()
        {
            Assert.That(WeaponPresets.Create("nope").displayName, Is.EqualTo(WeaponPresets.Create(WeaponPresets.Vandal).displayName));
            Assert.That(WeaponPresets.IsKnown("nope"), Is.False);
        }
    }
}
