using UnityEngine;

namespace AimTrainer.Combat
{
    /// <summary>一次伤害的输入。</summary>
    public readonly struct DamageInfo
    {
        public readonly float Amount;
        public readonly HitZone Zone;
        public readonly Vector3 Point;
        public readonly Vector3 Direction;
        public readonly GameObject Source;

        public DamageInfo(float amount, HitZone zone, Vector3 point, Vector3 direction, GameObject source)
        {
            Amount = amount;
            Zone = zone;
            Point = point;
            Direction = direction;
            Source = source;
        }
    }

    /// <summary>一次伤害的结果。</summary>
    public readonly struct DamageResult
    {
        public static readonly DamageResult None = new DamageResult(0f, HitZone.Body, false, 0f);

        /// <summary>实际扣掉的生命值（不超过剩余生命值）。</summary>
        public readonly float Dealt;
        public readonly HitZone Zone;

        /// <summary>这一下是否造成了击杀。</summary>
        public readonly bool Killed;

        public readonly float RemainingHealth;

        public DamageResult(float dealt, HitZone zone, bool killed, float remainingHealth)
        {
            Dealt = dealt;
            Zone = zone;
            Killed = killed;
            RemainingHealth = remainingHealth;
        }
    }
}
