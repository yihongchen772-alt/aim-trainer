using UnityEngine;

namespace AimTrainer.Combat
{
    /// <summary>
    /// 挂在碰撞体上的命中判定区域，把射线命中转成对所属 Health 的伤害。
    /// 头部的判定框应该单独一个碰撞体，并且比看到的模型稍大一圈。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class Hitbox : MonoBehaviour
    {
        [SerializeField] HitZone zone = HitZone.Body;
        [Tooltip("所属的生命值组件。留空时自动在父物体上查找。")]
        [SerializeField] Health owner;

        public HitZone Zone => zone;

        public Health Owner
        {
            get
            {
                if (owner == null) owner = GetComponentInParent<Health>();
                return owner;
            }
        }

        public void Configure(HitZone hitZone, Health health)
        {
            zone = hitZone;
            owner = health;
        }
    }
}
