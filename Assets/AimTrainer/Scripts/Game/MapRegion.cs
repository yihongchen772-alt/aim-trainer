using UnityEngine;

namespace AimTrainer.Game
{
    /// <summary>
    /// 地图区域（例如“A 大道”“中路庭院”），玩家走进去时小地图下方显示名称。两种写法：
    /// - 矩形：俯视图上 min ~ max 的范围（radius 为 0）。同名的多个矩形算同一个区域。
    /// - 地名点：物体所在位置是地名的中心，radius 米以内、离它最近时显示这个名称（真实地图用官方小地图的地名）。
    /// </summary>
    public sealed class MapRegion : MonoBehaviour
    {
        [SerializeField] string label = string.Empty;

        [Tooltip("矩形的最小角 (x, z)，世界坐标。")]
        [SerializeField] Vector2 min;

        [Tooltip("矩形的最大角 (x, z)，世界坐标。")]
        [SerializeField] Vector2 max;

        [Tooltip("大于 0 时是地名点：以物体位置为中心，这个半径（米）以内取最近的地名点。")]
        [SerializeField, Min(0f)] float radius;

        public string Label => string.IsNullOrEmpty(label) ? name : label;
        public Vector2 Min => min;
        public Vector2 Max => max;
        public float Radius => radius;
        public bool IsPoint => radius > 0f;

        public void Configure(string regionLabel, Vector2 minXZ, Vector2 maxXZ)
        {
            label = regionLabel;
            min = Vector2.Min(minXZ, maxXZ);
            max = Vector2.Max(minXZ, maxXZ);
            radius = 0f;
        }

        /// <summary>设成地名点：物体的位置是中心。</summary>
        public void ConfigurePoint(string regionLabel, float pointRadius)
        {
            label = regionLabel;
            radius = Mathf.Max(0.01f, pointRadius);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.8f);
            if (IsPoint)
            {
                Gizmos.DrawWireSphere(transform.position, 0.5f);
                return;
            }
            var center = new Vector3((min.x + max.x) * 0.5f, 0.05f, (min.y + max.y) * 0.5f);
            Gizmos.DrawWireCube(center, new Vector3(max.x - min.x, 0.1f, max.y - min.y));
        }
    }
}
