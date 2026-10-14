using UnityEngine;

namespace AimTrainer.Bots
{
    /// <summary>
    /// 随机刷新区域：定点甩枪和移动靶模式会在这个长方体范围内随机找地面放假人。
    /// 从长方体顶部向下找地面，并检查那里站得下一个人（不和墙、箱子重叠）。
    /// </summary>
    public sealed class BotSpawnArea : MonoBehaviour
    {
        const float BodyRadius = 0.35f;
        const float BodyHeight = 1.8f;

        [SerializeField] Vector3 size = new Vector3(20f, 6f, 20f);

        readonly Collider[] overlapBuffer = new Collider[4];

        public Vector3 Size => size;

        public void Configure(Vector3 areaSize) => size = areaSize;

        /// <summary>随机找一个能站人的位置（脚底坐标）。找不到时返回 false。</summary>
        public bool TrySample(System.Random rng, out Vector3 feet)
        {
            Vector3 center = transform.position;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                float x = center.x + (float)(rng.NextDouble() - 0.5) * size.x;
                float z = center.z + (float)(rng.NextDouble() - 0.5) * size.z;
                var top = new Vector3(x, center.y + size.y * 0.5f, z);
                if (!Physics.Raycast(top, Vector3.down, out RaycastHit hit, size.y + 1f, Physics.DefaultRaycastLayers,
                        QueryTriggerInteraction.Ignore))
                {
                    continue;
                }
                if (hit.normal.y < 0.8f) continue;

                feet = hit.point;
                Vector3 bottom = feet + Vector3.up * (BodyRadius + 0.05f);
                Vector3 head = feet + Vector3.up * (BodyHeight - BodyRadius);
                int count = Physics.OverlapCapsuleNonAlloc(bottom, head, BodyRadius, overlapBuffer,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                if (count == 0) return true;
            }
            feet = default;
            return false;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.6f);
            Gizmos.DrawWireCube(transform.position, size);
        }
    }
}
