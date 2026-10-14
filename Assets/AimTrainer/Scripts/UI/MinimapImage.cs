using UnityEngine;

namespace AimTrainer.UI
{
    /// <summary>
    /// 场景自带的小地图图片（例如官方小地图）和世界坐标到图片坐标的换算。场景里有这个组件时，小地图用这张图，
    /// 不再根据碰撞体自动生成俯视图。
    /// 换算：u = U.x·x + U.y·z + U.z，v = V.x·x + V.y·z + V.z（u 向右、v 向下，0~1 覆盖整张图）。
    /// </summary>
    public sealed class MinimapImage : MonoBehaviour
    {
        [SerializeField] Texture2D image;
        [SerializeField] Vector3 uFromWorld = new Vector3(1f, 0f, 0f);
        [SerializeField] Vector3 vFromWorld = new Vector3(0f, -1f, 0f);

        public Texture2D Image => image;
        public Vector3 UFromWorld => uFromWorld;
        public Vector3 VFromWorld => vFromWorld;

        public void Configure(Texture2D texture, Vector3 u, Vector3 v)
        {
            image = texture;
            uFromWorld = u;
            vFromWorld = v;
        }
    }
}
