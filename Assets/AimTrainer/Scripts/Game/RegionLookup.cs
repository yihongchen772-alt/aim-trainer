using System.Collections.Generic;

namespace AimTrainer.Game
{
    /// <summary>
    /// 按位置查地图区域名称（俯视图上的矩形，x 向东、z 向北）。多个区域重叠时取面积最小的那个。
    /// 纯数据，不依赖 UnityEngine。
    /// </summary>
    public sealed class RegionLookup
    {
        readonly struct Region
        {
            public readonly string Name;
            public readonly float XMin, XMax, ZMin, ZMax;

            public Region(string name, float xMin, float xMax, float zMin, float zMax)
            {
                Name = name;
                XMin = xMin;
                XMax = xMax;
                ZMin = zMin;
                ZMax = zMax;
            }

            public float Area => (XMax - XMin) * (ZMax - ZMin);
            public bool Contains(float x, float z) => x >= XMin && x <= XMax && z >= ZMin && z <= ZMax;
        }

        readonly List<Region> regions = new List<Region>();

        public int Count => regions.Count;

        public void Add(string name, float xMin, float xMax, float zMin, float zMax)
        {
            if (xMax < xMin) (xMin, xMax) = (xMax, xMin);
            if (zMax < zMin) (zMin, zMax) = (zMax, zMin);
            regions.Add(new Region(name, xMin, xMax, zMin, zMax));
        }

        /// <summary>(x, z) 所在区域的名称，不在任何区域里时返回空字符串。</summary>
        public string Find(float x, float z)
        {
            string best = string.Empty;
            float bestArea = float.MaxValue;
            foreach (Region region in regions)
            {
                if (!region.Contains(x, z) || region.Area >= bestArea) continue;
                best = region.Name;
                bestArea = region.Area;
            }
            return best;
        }
    }
}
