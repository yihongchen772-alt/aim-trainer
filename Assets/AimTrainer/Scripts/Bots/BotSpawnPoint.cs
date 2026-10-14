using UnityEngine;

namespace AimTrainer.Bots
{
    public enum BotBehavior
    {
        /// <summary>站在原地不动（架枪）。</summary>
        Static,

        /// <summary>在点位左右来回横跳（ADAD）。</summary>
        Strafe,

        /// <summary>从掩体后探头，停一会儿再缩回去。</summary>
        Peek,

        /// <summary>等一会儿后从掩体后全速冲出（Swing）到探头终点，然后停在那里对枪。架枪训练用。</summary>
        Swing,
    }

    /// <summary>
    /// 假人点位：拖到地图里任意位置即可。蓝色箭头是假人面朝的方向，
    /// 探头和冲出点位的橙线是探出去的终点，横跳点位的绿线是横跳范围。
    /// 物体的位置就是假人脚底，Y 轴旋转就是朝向。
    /// </summary>
    public sealed class BotSpawnPoint : MonoBehaviour
    {
        [Tooltip("在点位上的行为。")]
        [SerializeField] BotBehavior behavior = BotBehavior.Static;

        [Tooltip("横跳范围（米），以点位为中心左右各一半。")]
        [SerializeField, Min(0f)] float strafeWidth = 3f;

        [Tooltip("探头时相对点位的位移（本地坐标，米）。例如 (2, 0, 0) 表示向自己右边探出 2 米。")]
        [SerializeField] Vector3 peekOffset = new Vector3(1.5f, 0f, 0f);

        [Tooltip("预瞄模式中是否在这个点位放假人。")]
        [SerializeField] bool usedInPrefire = true;

        [Tooltip("对枪和预瞄模式中，这个点位的假人会不会开枪。")]
        [SerializeField] bool canShoot = true;

        [Tooltip("点位名称，只用于显示。")]
        [SerializeField] string label = string.Empty;

        public BotBehavior Behavior => behavior;
        public float StrafeWidth => strafeWidth;
        public bool UsedInPrefire => usedInPrefire;
        public bool CanShoot => canShoot;
        public string Label => string.IsNullOrEmpty(label) ? name : label;

        /// <summary>探头终点的世界坐标。</summary>
        public Vector3 PeekPosition => transform.TransformPoint(peekOffset);

        public void Configure(BotBehavior botBehavior, float width, Vector3 localPeekOffset, bool prefire, bool shoots,
            string pointLabel)
        {
            behavior = botBehavior;
            strafeWidth = width;
            peekOffset = localPeekOffset;
            usedInPrefire = prefire;
            canShoot = shoots;
            label = pointLabel;
        }

        void OnDrawGizmos()
        {
            Vector3 feet = transform.position;
            Gizmos.color = canShoot ? new Color(1f, 0.3f, 0.3f, 0.9f) : new Color(1f, 0.8f, 0.3f, 0.9f);
            Gizmos.DrawWireCube(feet + Vector3.up * 0.9f, new Vector3(0.5f, 1.8f, 0.5f));
            Gizmos.DrawWireSphere(feet + Vector3.up * 1.6f, 0.15f);
            Gizmos.color = new Color(0.3f, 0.6f, 1f);
            Gizmos.DrawLine(feet + Vector3.up * 1.6f, feet + Vector3.up * 1.6f + transform.forward * 1.5f);

            if (behavior == BotBehavior.Peek || behavior == BotBehavior.Swing)
            {
                Gizmos.color = new Color(1f, 0.6f, 0.1f);
                Gizmos.DrawLine(feet + Vector3.up * 0.05f, PeekPosition + Vector3.up * 0.05f);
                Gizmos.DrawWireSphere(PeekPosition + Vector3.up * 1.6f, 0.15f);
            }
            else if (behavior == BotBehavior.Strafe)
            {
                Gizmos.color = new Color(0.3f, 1f, 0.4f);
                Vector3 half = transform.right * (strafeWidth * 0.5f);
                Gizmos.DrawLine(feet - half + Vector3.up * 0.05f, feet + half + Vector3.up * 0.05f);
            }
        }
    }
}
