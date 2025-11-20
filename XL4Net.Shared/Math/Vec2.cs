using System;

namespace XL4Net.Shared.Math
{
    /// <summary>
    /// Vetor 2D (substitui UnityEngine.Vector2).
    /// </summary>
    public struct Vec2 : IEquatable<Vec2>
    {
        public float X;
        public float Y;

        public Vec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float Magnitude => MathF.Sqrt(X * X + Y * Y);
        public float SqrMagnitude => X * X + Y * Y;

        public Vec2 Normalized
        {
            get
            {
                var mag = Magnitude;
                return mag > 0.00001f ? this / mag : Zero;
            }
        }

        public static Vec2 Zero => new Vec2(0, 0);
        public static Vec2 One => new Vec2(1, 1);
        public static Vec2 Up => new Vec2(0, 1);
        public static Vec2 Down => new Vec2(0, -1);
        public static Vec2 Right => new Vec2(1, 0);
        public static Vec2 Left => new Vec2(-1, 0);

        public static Vec2 operator +(Vec2 a, Vec2 b)
            => new Vec2(a.X + b.X, a.Y + b.Y);

        public static Vec2 operator -(Vec2 a, Vec2 b)
            => new Vec2(a.X - b.X, a.Y - b.Y);

        public static Vec2 operator *(Vec2 a, float scalar)
            => new Vec2(a.X * scalar, a.Y * scalar);

        public static Vec2 operator /(Vec2 a, float scalar)
            => new Vec2(a.X / scalar, a.Y / scalar);

        public static float Distance(Vec2 a, Vec2 b)
            => (a - b).Magnitude;

        public static float Dot(Vec2 a, Vec2 b)
            => a.X * b.X + a.Y * b.Y;

        public static Vec2 Lerp(Vec2 a, Vec2 b, float t)
        {
            t = System.Math.Clamp(t, 0f, 1f);
            return new Vec2(
                a.X + (b.X - a.X) * t,
                a.Y + (b.Y - a.Y) * t
            );
        }

        public bool Equals(Vec2 other)
        {
            return MathF.Abs(X - other.X) < 0.00001f &&
                   MathF.Abs(Y - other.Y) < 0.00001f;
        }

        public override bool Equals(object obj)
            => obj is Vec2 other && Equals(other);

        public override int GetHashCode()
            => HashCode.Combine(X, Y);

        public static bool operator ==(Vec2 a, Vec2 b) => a.Equals(b);
        public static bool operator !=(Vec2 a, Vec2 b) => !a.Equals(b);

        public override string ToString()
            => $"({X:F2}, {Y:F2})";
    }
}