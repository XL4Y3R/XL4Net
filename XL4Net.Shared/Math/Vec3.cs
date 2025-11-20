using System;

namespace XL4Net.Shared.Math
{
    /// <summary>
    /// Vetor 3D (substitui UnityEngine.Vector3).
    /// Independente de engine para manter Shared portável.
    /// </summary>
    public struct Vec3 : IEquatable<Vec3>
    {
        public float X;
        public float Y;
        public float Z;

        public Vec3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        // Propriedades úteis
        public float Magnitude => MathF.Sqrt(X * X + Y * Y + Z * Z);
        public float SqrMagnitude => X * X + Y * Y + Z * Z;

        public Vec3 Normalized
        {
            get
            {
                float mag = Magnitude;
                return mag > 0.00001f ? this / mag : Zero;
            }
        }

        // Constantes
        public static Vec3 Zero => new Vec3(0, 0, 0);
        public static Vec3 One => new Vec3(1, 1, 1);
        public static Vec3 Up => new Vec3(0, 1, 0);
        public static Vec3 Down => new Vec3(0, -1, 0);
        public static Vec3 Forward => new Vec3(0, 0, 1);
        public static Vec3 Back => new Vec3(0, 0, -1);
        public static Vec3 Right => new Vec3(1, 0, 0);
        public static Vec3 Left => new Vec3(-1, 0, 0);

        // Operadores
        public static Vec3 operator +(Vec3 a, Vec3 b)
            => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        public static Vec3 operator -(Vec3 a, Vec3 b)
            => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        public static Vec3 operator *(Vec3 a, float scalar)
            => new Vec3(a.X * scalar, a.Y * scalar, a.Z * scalar);

        public static Vec3 operator /(Vec3 a, float scalar)
            => new Vec3(a.X / scalar, a.Y / scalar, a.Z / scalar);

        public static Vec3 operator -(Vec3 a)
            => new Vec3(-a.X, -a.Y, -a.Z);

        // Métodos úteis
        public static float Distance(Vec3 a, Vec3 b)
        {
            return (a - b).Magnitude;
        }

        public static float Dot(Vec3 a, Vec3 b)
        {
            return a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        }

        public static Vec3 Cross(Vec3 a, Vec3 b)
        {
            return new Vec3(
                a.Y * b.Z - a.Z * b.Y,
                a.Z * b.X - a.X * b.Z,
                a.X * b.Y - a.Y * b.X
            );
        }

        public static Vec3 Lerp(Vec3 a, Vec3 b, float t)
        {
            t = System.Math.Clamp(t, 0f, 1f);
            return new Vec3(
                a.X + (b.X - a.X) * t,
                a.Y + (b.Y - a.Y) * t,
                a.Z + (b.Z - a.Z) * t
            );
        }

        // Equality
        public bool Equals(Vec3 other)
        {
            return MathF.Abs(X - other.X) < 0.00001f &&
                   MathF.Abs(Y - other.Y) < 0.00001f &&
                   MathF.Abs(Z - other.Z) < 0.00001f;
        }

        public override bool Equals(object obj)
            => obj is Vec3 other && Equals(other);

        public override int GetHashCode()
            => HashCode.Combine(X, Y, Z);

        public static bool operator ==(Vec3 a, Vec3 b) => a.Equals(b);
        public static bool operator !=(Vec3 a, Vec3 b) => !a.Equals(b);

        public override string ToString()
            => $"({X:F2}, {Y:F2}, {Z:F2})";
    }
}