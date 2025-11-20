using System;

namespace XL4Net.Shared.Math
{
    /// <summary>
    /// Vetor 2D inteiro (usado em spatial grid).
    /// </summary>
    public struct Vec2Int : IEquatable<Vec2Int>
    {
        public int X;
        public int Y;

        public Vec2Int(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static Vec2Int Zero => new Vec2Int(0, 0);
        public static Vec2Int One => new Vec2Int(1, 1);
        public static Vec2Int Up => new Vec2Int(0, 1);
        public static Vec2Int Down => new Vec2Int(0, -1);
        public static Vec2Int Right => new Vec2Int(1, 0);
        public static Vec2Int Left => new Vec2Int(-1, 0);

        public static Vec2Int operator +(Vec2Int a, Vec2Int b)
            => new Vec2Int(a.X + b.X, a.Y + b.Y);

        public static Vec2Int operator -(Vec2Int a, Vec2Int b)
            => new Vec2Int(a.X - b.X, a.Y - b.Y);

        public static Vec2Int operator *(Vec2Int a, int scalar)
            => new Vec2Int(a.X * scalar, a.Y * scalar);

        public bool Equals(Vec2Int other)
            => X == other.X && Y == other.Y;

        public override bool Equals(object obj)
            => obj is Vec2Int other && Equals(other);

        public override int GetHashCode()
            => HashCode.Combine(X, Y);

        public static bool operator ==(Vec2Int a, Vec2Int b) => a.Equals(b);
        public static bool operator !=(Vec2Int a, Vec2Int b) => !a.Equals(b);

        public override string ToString()
            => $"({X}, {Y})";
    }
}