using AimTrainer.Game;
using AimTrainer.Player;
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace AimTrainer.Tests
{
    public class RegionLookupTests
    {
        [Test]
        public void Find_ReturnsRegionContainingPoint()
        {
            var lookup = new RegionLookup();
            lookup.Add("A 大道", -52f, -45f, -30f, 2f);
            lookup.Add("A 包点", -60f, -30f, 2f, 30f);
            Assert.That(lookup.Find(-48f, -10f), Is.EqualTo("A 大道"));
            Assert.That(lookup.Find(-40f, 20f), Is.EqualTo("A 包点"));
            Assert.That(lookup.Find(0f, 0f), Is.Empty, "不在任何区域里");
        }

        [Test]
        public void Find_PrefersSmallestOverlappingRegion()
        {
            var lookup = new RegionLookup();
            lookup.Add("包点", 0f, 30f, 0f, 30f);
            lookup.Add("下包位", 10f, 14f, 10f, 14f);
            Assert.That(lookup.Find(12f, 12f), Is.EqualTo("下包位"));
            Assert.That(lookup.Find(20f, 20f), Is.EqualTo("包点"));
        }

        [Test]
        public void Points_ReturnNearestWithinRadius()
        {
            var lookup = new RegionLookup();
            lookup.AddPoint("A区 大道", 0f, 0f, 20f);
            lookup.AddPoint("A区 部署区", 10f, 0f, 20f);
            Assert.That(lookup.Find(2f, 1f), Is.EqualTo("A区 大道"));
            Assert.That(lookup.Find(8f, -1f), Is.EqualTo("A区 部署区"));
            Assert.That(lookup.Find(100f, 0f), Is.Empty, "超出半径");
        }

        [Test]
        public void Rectangles_TakePriorityOverPoints()
        {
            var lookup = new RegionLookup();
            lookup.AddPoint("地名", 0f, 0f, 50f);
            lookup.Add("矩形", -1f, 1f, -1f, 1f);
            Assert.That(lookup.Find(0f, 0f), Is.EqualTo("矩形"));
            Assert.That(lookup.Find(5f, 5f), Is.EqualTo("地名"));
        }

        [Test]
        public void Add_AcceptsSwappedCorners()
        {
            var lookup = new RegionLookup();
            lookup.Add("中路", 6f, -6f, 16f, -8f);
            Assert.That(lookup.Find(0f, 0f), Is.EqualTo("中路"));
        }
    }

    public class AngleStatsTests
    {
        [Test]
        public void RecordKill_AveragesOnlyTimedKills()
        {
            var stats = new AngleStats();
            stats.RecordKill("天堂", 0.4f);
            stats.RecordKill("天堂", 0.6f);
            stats.RecordKill("天堂", -1f);
            AngleStats.Entry e = stats.Find("天堂");
            Assert.That(e.Kills, Is.EqualTo(3));
            Assert.That(e.TimedKills, Is.EqualTo(2));
            Assert.That(e.AverageTime, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(e.SlowestTime, Is.EqualTo(0.6f).Within(1e-5f));
        }

        [Test]
        public void Sorted_PutsDeathsFirstThenSlowest()
        {
            var stats = new AngleStats();
            stats.RecordKill("快", 0.2f);
            stats.RecordKill("慢", 0.9f);
            stats.RecordKill("中", 0.5f);
            stats.RecordDeath("中");
            var sorted = stats.Sorted();
            Assert.That(sorted[0].Label, Is.EqualTo("中"), "被击杀过的点位排最前");
            Assert.That(sorted[1].Label, Is.EqualTo("慢"));
            Assert.That(sorted[2].Label, Is.EqualTo("快"));
        }

        [Test]
        public void EmptyLabel_IsGroupedAsUnnamed()
        {
            var stats = new AngleStats();
            stats.RecordKill(null, 0.3f);
            stats.RecordDeath(string.Empty);
            Assert.That(stats.Count, Is.EqualTo(1));
            Assert.That(stats.Find("未命名").Deaths, Is.EqualTo(1));
        }
    }

    public class KeyBindingsTests
    {
        [Test]
        public void Defaults_MatchGameDefaults()
        {
            var keys = new KeyBindings();
            Assert.That(keys.forward, Is.EqualTo(Key.W));
            Assert.That(keys.walk, Is.EqualTo(Key.LeftShift));
            Assert.That(keys.crouch, Is.EqualTo(Key.LeftCtrl));
            Assert.That(keys.crouchAlt, Is.EqualTo(Key.C));
            Assert.That(keys.reload, Is.EqualTo(Key.R));
        }

        [Test]
        public void Assign_SwapsWhenKeyAlreadyUsed()
        {
            var keys = new KeyBindings();
            int swapped = keys.Assign(8, Key.W); // 换弹改成 W，前进原来就是 W
            Assert.That(swapped, Is.EqualTo(0));
            Assert.That(keys.reload, Is.EqualTo(Key.W));
            Assert.That(keys.forward, Is.EqualTo(Key.R), "前进拿到换弹原来的键");
        }

        [Test]
        public void Assign_RejectsReservedKeys()
        {
            var keys = new KeyBindings();
            Assert.That(keys.Assign(7, Key.Escape), Is.EqualTo(-1));
            Assert.That(keys.Assign(7, Key.F1), Is.EqualTo(-1));
            Assert.That(keys.jump, Is.EqualTo(Key.Space));
        }

        [Test]
        public void Sanitize_RestoresReservedOrInvalidKeys()
        {
            var keys = new KeyBindings { jump = Key.Escape, reload = (Key)99999 };
            keys.Sanitize();
            Assert.That(keys.jump, Is.EqualTo(Key.Space));
            Assert.That(keys.reload, Is.EqualTo(Key.R));
        }

        [Test]
        public void GetSet_CoverAllActions()
        {
            var keys = new KeyBindings();
            for (int i = 0; i < KeyBindings.Count; i++)
            {
                keys.Set(i, Key.F2 + 0);
                Assert.That(keys.Get(i), Is.EqualTo(Key.F2), $"动作 {i}");
                Assert.That(KeyBindings.NameOf(i), Is.Not.Empty);
            }
        }
    }
}
