using System;
using UnityEngine;

namespace AimTrainer.Combat
{
    /// <summary>
    /// 生命值。玩家和假人都用它。死亡后不再受伤，直到 ResetHealth。
    /// 端游里角色是 100 生命 + 最多 50 护甲，这里把护甲直接算进生命值：假人默认 150。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Health : MonoBehaviour
    {
        [Tooltip("满血生命值。")]
        [SerializeField, Min(1f)] float maxHealth = 150f;

        float current;
        bool initialized;

        public float Max => maxHealth;
        public float Current => initialized ? current : maxHealth;
        public bool IsDead => initialized && current <= 0f;

        /// <summary>受到伤害后触发（包括致死的那一下）。</summary>
        public event Action<Health, DamageInfo, DamageResult> Damaged;

        /// <summary>生命值降到 0 时触发一次。</summary>
        public event Action<Health, DamageInfo, DamageResult> Died;

        void Awake() => EnsureInitialized();

        public void Configure(float max)
        {
            maxHealth = Mathf.Max(1f, max);
            current = maxHealth;
            initialized = true;
        }

        public void ResetHealth()
        {
            current = maxHealth;
            initialized = true;
        }

        public DamageResult ApplyDamage(in DamageInfo info)
        {
            EnsureInitialized();
            if (current <= 0f || info.Amount <= 0f) return DamageResult.None;

            float dealt = Mathf.Min(info.Amount, current);
            current -= dealt;
            bool killed = current <= 0f;
            if (killed) current = 0f;

            var result = new DamageResult(dealt, info.Zone, killed, current);
            Damaged?.Invoke(this, info, result);
            if (killed) Died?.Invoke(this, info, result);
            return result;
        }

        void EnsureInitialized()
        {
            if (initialized) return;
            current = maxHealth;
            initialized = true;
        }
    }
}
