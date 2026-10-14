using System.Collections.Generic;
using UnityEngine;

namespace AimTrainer.Bots
{
    /// <summary>
    /// 预瞄路线：物体的位置和朝向是玩家的出发点，points 是这条路线上要清的点位（按推进顺序）。
    /// 预瞄模式选了路线后，只在这些点位放假人，并从出发点开始。
    /// </summary>
    public sealed class PrefireRoute : MonoBehaviour
    {
        [Tooltip("路线名称，显示在菜单里。")]
        [SerializeField] string label = string.Empty;

        [Tooltip("路线上的点位，按推进顺序排列。")]
        [SerializeField] BotSpawnPoint[] points = new BotSpawnPoint[0];

        public string Label => string.IsNullOrEmpty(label) ? name : label;
        public IReadOnlyList<BotSpawnPoint> Points => points;

        public void Configure(string routeLabel, BotSpawnPoint[] routePoints)
        {
            label = routeLabel;
            points = routePoints ?? new BotSpawnPoint[0];
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.8f);
            Vector3 previous = transform.position + Vector3.up * 0.1f;
            Gizmos.DrawWireSphere(previous, 0.4f);
            foreach (BotSpawnPoint point in points)
            {
                if (point == null) continue;
                Vector3 next = point.transform.position + Vector3.up * 0.1f;
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
    }
}
