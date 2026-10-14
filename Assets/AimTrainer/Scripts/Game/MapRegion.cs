using UnityEngine;

namespace AimTrainer.Game
{
    /// <summary>
    /// 地图区域（例如“A 大道”“中路庭院”）：俯视图上的一个矩形，玩家走进去时小地图下方显示名称。
    /// 同名的多个矩形算同一个区域。
    /// </summary>
    public sealed class MapRegion : MonoBehaviour
    {
        [SerializeField] string label = string.Empty;

        [Tooltip("矩形的最小角 (x, z)，世界坐标。")]
        [SerializeField] Vector2 min;

        [Tooltip("矩形的最大角 (x, z)，世界坐标。")]
        [SerializeField] Vector2 max;

        public string Label => string.IsNullOrEmpty(label) ? name : label;
        public Vector2 Min => min;
        public Vector2 Max => max;

        public void Configure(string regionLabel, Vector2 minXZ, Vector2 maxXZ)
        {
            label = regionLabel;
            min = Vector2.Min(minXZ, maxXZ);
            max = Vector2.Max(minXZ, maxXZ);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.8f);
            var center = new Vector3((min.x + max.x) * 0.5f, 0.05f, (min.y + max.y) * 0.5f);
            Gizmos.DrawWireCube(center, new Vector3(max.x - min.x, 0.1f, max.y - min.y));
        }
    }
}
