using System.Collections.Generic;

namespace AimTrainer.Game
{
    /// <summary>
    /// 按位置查地图区域名称。两种区域：
    /// - 矩形（俯视图上的 x、z 范围）：多个矩形重叠时取面积最小的那个；
    /// - 地名点（例如官方小地图上的“A区 大道”）：不在任何矩形里时，取范围内最近的地名点。
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

        readonly struct NamedPoint
        {
            public readonly string Name;
            public readonly float X, Z, Radius;

            public NamedPoint(string name, float x, float z, float radius)
            {
                Name = name;
                X = x;
                Z = z;
                Radius = radius;
            }
        }

        readonly List<Region> regions = new List<Region>();
        readonly List<NamedPoint> points = new List<NamedPoint>();

        public int Count => regions.Count + points.Count;

        /// <summary>加一个地名点：离它 radius 米以内、并且它是最近的地名点时，返回这个名称。</summary>
        public void AddPoint(string name, float x, float z, float radius) => points.Add(new NamedPoint(name, x, z, radius));

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
            if (best.Length > 0) return best;

            float bestDistance = float.MaxValue;
            foreach (NamedPoint point in points)
            {
                float dx = x - point.X;
                float dz = z - point.Z;
                float distance = dx * dx + dz * dz;
                if (distance > point.Radius * point.Radius || distance >= bestDistance) continue;
                best = point.Name;
                bestDistance = distance;
            }
            return best;
        }
    }
}
