using System;

namespace AimTrainer.Movement
{
    /// <summary>
    /// 水平面（世界坐标 XZ 平面）上的二维向量。
    /// 移动核心算法只处理水平分量，用这个不依赖 UnityEngine 的结构体，
    /// 算法就能脱离 Unity 编辑器做单元测试。
    /// </summary>
    public readonly struct PlanarVector : IEquatable<PlanarVector>
    {
        public static readonly PlanarVector Zero = new PlanarVector(0f, 0f);

        public readonly float X;
        public readonly float Z;

        public PlanarVector(float x, float z)
        {
            X = x;
            Z = z;
        }

        public float SqrMagnitude => X * X + Z * Z;
        public float Magnitude => MathF.Sqrt(SqrMagnitude);
        public bool IsZero => SqrMagnitude < 1e-12f;

        public PlanarVector Normalized
        {
            get
            {
                float m = Magnitude;
                return m > 1e-6f ? new PlanarVector(X / m, Z / m) : Zero;
            }
        }

        /// <summary>绕 +Y 轴旋转 yaw 度后的前方向，与 Unity 的 Quaternion.Euler(0, yaw, 0) * Vector3.forward 一致。</summary>
        public static PlanarVector FromYaw(float yawDegrees)
        {
            float r = yawDegrees * (MathF.PI / 180f);
            return new PlanarVector(MathF.Sin(r), MathF.Cos(r));
        }

        public static float Dot(PlanarVector a, PlanarVector b) => a.X * b.X + a.Z * b.Z;

        public static PlanarVector operator +(PlanarVector a, PlanarVector b) => new PlanarVector(a.X + b.X, a.Z + b.Z);
        public static PlanarVector operator -(PlanarVector a, PlanarVector b) => new PlanarVector(a.X - b.X, a.Z - b.Z);
        public static PlanarVector operator -(PlanarVector a) => new PlanarVector(-a.X, -a.Z);
        public static PlanarVector operator *(PlanarVector a, float s) => new PlanarVector(a.X * s, a.Z * s);
        public static PlanarVector operator *(float s, PlanarVector a) => new PlanarVector(a.X * s, a.Z * s);

        public bool Equals(PlanarVector other) => X.Equals(other.X) && Z.Equals(other.Z);
        public override bool Equals(object obj) => obj is PlanarVector other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Z);
        public override string ToString() => $"({X:0.###}, {Z:0.###})";
    }
}
