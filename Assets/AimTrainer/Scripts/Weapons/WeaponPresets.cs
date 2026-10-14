using System;

namespace AimTrainer.Weapons
{
    /// <summary>
    /// 参考端游常用枪械的预设：射速、弹匣、换弹、分段伤害、首发精度和弹道走势。
    /// 数值来自公开资料，不同版本可能有出入；后坐力图案是按“先往哪跳、多快、跳多高、后段怎么摆”近似的，
    /// 不是逐发复刻。所有预设都可以在 Settings/DefaultRifle.asset 之外通过代码或菜单切换。
    /// </summary>
    public static class WeaponPresets
    {
        public readonly struct Entry
        {
            public readonly string Id;
            public readonly string Title;
            public readonly string Note;

            public Entry(string id, string title, string note)
            {
                Id = id;
                Title = title;
                Note = note;
            }
        }

        public const string Vandal = "vandal";
        public const string Phantom = "phantom";
        public const string Spectre = "spectre";
        public const string Sheriff = "sheriff";
        public const string Ghost = "ghost";

        public static readonly Entry[] All =
        {
            new Entry(Vandal, "Vandal 狂徒", "全自动 9.75 发/秒，任何距离爆头 160 一枪死；前 3 发准，之后往上偏右跳"),
            new Entry(Phantom, "Phantom 幻影", "全自动 11 发/秒，爆头 156，15 米和 30 米后伤害衰减；上跳更平缓"),
            new Entry(Spectre, "Spectre 骇灵", "全自动 13.3 发/秒冲锋枪，爆头 78（两枪），跑打惩罚小，后坐力往上偏左"),
            new Entry(Sheriff, "Sheriff 警长", "半自动左轮 4 发/秒，爆头 159 一枪死，身体 55；每枪跳得很高"),
            new Entry(Ghost, "Ghost 鬼魅", "半自动手枪 6.75 发/秒，爆头 105（打护甲要两枪），点射很准"),
        };

        public static string TitleOf(string id)
        {
            foreach (Entry e in All)
            {
                if (e.Id == id) return e.Title;
            }
            return All[0].Title;
        }

        /// <summary>按 id 创建一份新的参数（每次调用返回新对象，可以随意修改）。未知 id 返回 Vandal。</summary>
        public static WeaponStats Create(string id)
        {
            switch (id)
            {
                case Phantom:
                    return new WeaponStats
                    {
                        displayName = "Phantom 幻影", roundsPerSecond = 11f, magazineSize = 30, reloadTime = 2.5f,
                        headDamage = 156f, bodyDamage = 39f, legDamage = 33f,
                        falloffDistance = 15f, falloffMultiplier = 0.9f, falloffDistance2 = 30f, falloffMultiplier2 = 0.8f,
                        firstShotSpread = 0.2f, sprayGrowthPerShot = 0.17f, maxSpraySpread = 1.8f,
                        walkSpread = 1.5f, runSpread = 5.5f, airSpread = 8f,
                        climbStartShot = 3, climbPerShot = 0.42f, climbYawPerShot = -0.04f, climbShots = 9,
                        swayStartShot = 12, swayAmplitude = 1.2f, swayFrequency = 0.75f, viewKick = 0.025f,
                    };
                case Spectre:
                    return new WeaponStats
                    {
                        displayName = "Spectre 骇灵", roundsPerSecond = 13.33f, magazineSize = 30, reloadTime = 2.25f,
                        headDamage = 78f, bodyDamage = 26f, legDamage = 22f,
                        falloffDistance = 20f, falloffMultiplier = 0.85f,
                        firstShotSpread = 0.4f, sprayGrowthPerShot = 0.28f, maxSpraySpread = 2.5f,
                        walkSpread = 0.8f, runSpread = 2.5f, airSpread = 6f,
                        climbStartShot = 2, climbPerShot = 0.35f, climbYawPerShot = -0.08f, climbShots = 10,
                        swayStartShot = 12, swayAmplitude = 1.8f, swayFrequency = 0.9f, viewKick = 0.02f,
                        sprayRecoveryDelay = 0.1f, sprayRecoveryRate = 40f,
                    };
                case Sheriff:
                    return new WeaponStats
                    {
                        displayName = "Sheriff 警长", roundsPerSecond = 4f, automatic = false, magazineSize = 6, reloadTime = 3f,
                        headDamage = 159f, bodyDamage = 55f, legDamage = 46f,
                        falloffDistance = 30f, falloffMultiplier = 0.91f,
                        firstShotSpread = 0.25f, sprayGrowthPerShot = 0.9f, maxSpraySpread = 3.5f,
                        walkSpread = 2.5f, runSpread = 6f, airSpread = 8f,
                        climbStartShot = 2, climbPerShot = 1.4f, climbYawPerShot = 0.1f, climbShots = 4,
                        swayStartShot = 99, swayAmplitude = 0f, viewKick = 0.05f,
                        sprayRecoveryDelay = 0.26f, sprayRecoveryRate = 6f,
                    };
                case Ghost:
                    return new WeaponStats
                    {
                        displayName = "Ghost 鬼魅", roundsPerSecond = 6.75f, automatic = false, magazineSize = 15, reloadTime = 1.5f,
                        headDamage = 105f, bodyDamage = 30f, legDamage = 26f,
                        falloffDistance = 30f, falloffMultiplier = 0.84f,
                        firstShotSpread = 0.3f, sprayGrowthPerShot = 0.35f, maxSpraySpread = 2f,
                        walkSpread = 1.2f, runSpread = 3f, airSpread = 6f,
                        climbStartShot = 2, climbPerShot = 0.6f, climbYawPerShot = 0.05f, climbShots = 4,
                        swayStartShot = 99, swayAmplitude = 0f, viewKick = 0.03f,
                        sprayRecoveryDelay = 0.16f, sprayRecoveryRate = 12f,
                    };
                default:
                    return new WeaponStats
                    {
                        displayName = "Vandal 狂徒", roundsPerSecond = 9.75f, magazineSize = 25, reloadTime = 2.5f,
                        headDamage = 160f, bodyDamage = 40f, legDamage = 34f,
                        firstShotSpread = 0.25f, sprayGrowthPerShot = 0.22f, maxSpraySpread = 2.2f,
                        walkSpread = 1.6f, runSpread = 6f, airSpread = 8f,
                        climbStartShot = 4, climbPerShot = 0.55f, climbYawPerShot = 0.06f, climbShots = 8,
                        swayStartShot = 12, swayAmplitude = 1.6f, swayFrequency = 0.8f, viewKick = 0.03f,
                    };
            }
        }

        public static bool IsKnown(string id) => Array.Exists(All, e => e.Id == id);
    }
}
