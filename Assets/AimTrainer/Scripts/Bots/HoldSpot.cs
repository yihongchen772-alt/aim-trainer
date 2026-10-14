using UnityEngine;

namespace AimTrainer.Bots
{
    /// <summary>
    /// 架枪位置：物体的位置和朝向是玩家守的位置；子物体里的 BotSpawnPoint（一般是“冲出”行为）是假人冲出来的入口。
    /// 入口点位只在架枪模式使用，对枪和预瞄模式会忽略它们。
    /// </summary>
    public sealed class HoldSpot : MonoBehaviour
    {
        [Tooltip("名称，显示在菜单里。")]
        [SerializeField] string label = string.Empty;

        public string Label => string.IsNullOrEmpty(label) ? name : label;

        /// <summary>这个位置的所有入口。</summary>
        public BotSpawnPoint[] Entries => GetComponentsInChildren<BotSpawnPoint>(true);

        public void Configure(string spotLabel) => label = spotLabel;

        void OnDrawGizmos()
        {
            Vector3 eye = transform.position + Vector3.up * 1.6f;
            Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.9f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.9f, new Vector3(0.6f, 1.8f, 0.6f));
            Gizmos.DrawLine(eye, eye + transform.forward * 2f);
            Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.35f);
            foreach (BotSpawnPoint entry in Entries)
            {
                if (entry != null) Gizmos.DrawLine(eye, entry.PeekPosition + Vector3.up * 1.6f);
            }
        }
    }
}
