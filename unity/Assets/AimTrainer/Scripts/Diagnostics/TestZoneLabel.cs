using System.Collections.Generic;
using UnityEngine;

namespace AimTrainer.Diagnostics
{
    /// <summary>
    /// 测试场景里的一块区域说明。玩家走进这块区域时，调试面板会显示它的标题和测试方法。
    /// 区域范围是以本物体为中心、大小为 size 的长方体，在 Scene 视图里画成线框。
    /// </summary>
    public sealed class TestZoneLabel : MonoBehaviour
    {
        static readonly List<TestZoneLabel> Active = new List<TestZoneLabel>();

        [SerializeField] string title = "测试区域";
        [SerializeField, TextArea] string description = string.Empty;
        [SerializeField] Vector3 size = new Vector3(10f, 4f, 10f);

        public string Title => title;
        public string Description => description;

        public void Configure(string zoneTitle, string zoneDescription, Vector3 zoneSize)
        {
            title = zoneTitle;
            description = zoneDescription;
            size = zoneSize;
        }

        public bool Contains(Vector3 point) => new Bounds(transform.position, size).Contains(point);

        public static TestZoneLabel Find(Vector3 point)
        {
            for (int i = 0; i < Active.Count; i++)
            {
                if (Active[i].Contains(point)) return Active[i];
            }
            return null;
        }

        void OnEnable() => Active.Add(this);
        void OnDisable() => Active.Remove(this);

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.7f, 0.2f, 0.8f);
            Gizmos.DrawWireCube(transform.position, size);
        }
    }
}
