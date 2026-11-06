using System;

namespace SevenDays.Procedural
{
    /// <summary>Vector 3D mínimo para geometría pura (sin UnityEngine), con la convención de Unity: Y arriba.</summary>
    public readonly struct V3
    {
        public readonly float X, Y, Z;
        public V3(float x, float y, float z) { X = x; Y = y; Z = z; }

        public static V3 operator +(V3 a, V3 b) => new V3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static V3 operator -(V3 a, V3 b) => new V3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static V3 operator -(V3 a) => new V3(-a.X, -a.Y, -a.Z);
        public static V3 operator *(V3 a, float k) => new V3(a.X * k, a.Y * k, a.Z * k);

        public float Length => (float)Math.Sqrt(X * X + Y * Y + Z * Z);
        public V3 Normalized { get { float l = Length; return l > 1e-8f ? this * (1f / l) : new V3(0, 0, 0); } }
        public static float Dot(V3 a, V3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        public static V3 Cross(V3 a, V3 b) => new V3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        public static V3 Axis(int i) => i == 0 ? new V3(1, 0, 0) : i == 1 ? new V3(0, 1, 0) : new V3(0, 0, 1);
        public float this[int i] => i == 0 ? X : i == 1 ? Y : Z;
        public static V3 FromAxes(int a, float va, int b, float vb, int c, float vc)
        {
            float[] r = new float[3];
            r[a] = va; r[b] = vb; r[c] = vc;
            return new V3(r[0], r[1], r[2]);
        }
    }

    public readonly struct V2
    {
        public readonly float X, Y;
        public V2(float x, float y) { X = x; Y = y; }
        public static V2 operator +(V2 a, V2 b) => new V2(a.X + b.X, a.Y + b.Y);
    }
}
