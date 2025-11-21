using System;
using System.Runtime.CompilerServices;
using MessagePack;

namespace XL4Net.Shared.Math
{
    /// <summary>
    /// Vetor 2D próprio do XL4Net.
    /// Engine-agnostic - não depende de Unity, Godot, etc.
    /// Útil para inputs de movimento, UI, coordenadas 2D.
    /// </summary>
    [MessagePackObject]
    public struct Vec2 : IEquatable<Vec2>
    {
        [Key(0)]
        public float X;

        [Key(1)]
        public float Y;

        #region Construtores

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vec2(float value)
        {
            X = value;
            Y = value;
        }

        #endregion

        #region Propriedades Estáticas

        public static Vec2 Zero => new Vec2(0f, 0f);
        public static Vec2 One => new Vec2(1f, 1f);
        public static Vec2 Up => new Vec2(0f, 1f);
        public static Vec2 Down => new Vec2(0f, -1f);
        public static Vec2 Right => new Vec2(1f, 0f);
        public static Vec2 Left => new Vec2(-1f, 0f);

        #endregion

        #region Propriedades Calculadas

        [IgnoreMember]
        public float Magnitude
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => MathF.Sqrt(X * X + Y * Y);
        }

        [IgnoreMember]
        public float SqrMagnitude
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => X * X + Y * Y;
        }

        [IgnoreMember]
        public Vec2 Normalized
        {
            get
            {
                float mag = Magnitude;
                if (mag > 1E-05f)
                    return new Vec2(X / mag, Y / mag);
                return Zero;
            }
        }

        #endregion

        #region Operadores

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec2 operator +(Vec2 a, Vec2 b)
            => new Vec2(a.X + b.X, a.Y + b.Y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec2 operator -(Vec2 a, Vec2 b)
            => new Vec2(a.X - b.X, a.Y - b.Y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec2 operator *(Vec2 a, float d)
            => new Vec2(a.X * d, a.Y * d);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec2 operator *(float d, Vec2 a)
            => new Vec2(a.X * d, a.Y * d);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec2 operator /(Vec2 a, float d)
            => new Vec2(a.X / d, a.Y / d);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec2 operator -(Vec2 a)
            => new Vec2(-a.X, -a.Y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Vec2 a, Vec2 b)
            => a.X == b.X && a.Y == b.Y;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Vec2 a, Vec2 b)
            => !(a == b);

        #endregion

        #region Métodos Estáticos

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Dot(Vec2 a, Vec2 b)
            => a.X * b.X + a.Y * b.Y;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Distance(Vec2 a, Vec2 b)
            => (a - b).Magnitude;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SqrDistance(Vec2 a, Vec2 b)
            => (a - b).SqrMagnitude;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec2 Lerp(Vec2 a, Vec2 b, float t)
        {
            t = t < 0f ? 0f : (t > 1f ? 1f : t);
            return new Vec2(
                a.X + (b.X - a.X) * t,
                a.Y + (b.Y - a.Y) * t
            );
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec2 LerpUnclamped(Vec2 a, Vec2 b, float t)
            => new Vec2(
                a.X + (b.X - a.X) * t,
                a.Y + (b.Y - a.Y) * t
            );

        public static Vec2 MoveTowards(Vec2 current, Vec2 target, float maxDistanceDelta)
        {
            Vec2 diff = target - current;
            float sqrMag = diff.SqrMagnitude;

            if (sqrMag == 0f || (maxDistanceDelta >= 0f && sqrMag <= maxDistanceDelta * maxDistanceDelta))
                return target;

            float mag = MathF.Sqrt(sqrMag);
            return current + diff / mag * maxDistanceDelta;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec2 ClampMagnitude(Vec2 vector, float maxLength)
        {
            float sqrMag = vector.SqrMagnitude;
            if (sqrMag > maxLength * maxLength)
            {
                float mag = MathF.Sqrt(sqrMag);
                return vector / mag * maxLength;
            }
            return vector;
        }

        #endregion

        #region Conversão Vec2 <-> Vec3

        /// <summary>
        /// Converte para Vec3 com Y como altura (plano XZ).
        /// Útil para jogos 3D onde movimento é no plano horizontal.
        /// </summary>
        /// <param name="y">Valor do Y (altura)</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vec3 ToVec3XZ(float y = 0f)
            => new Vec3(X, y, Y);

        /// <summary>
        /// Converte para Vec3 com Z=0 (plano XY).
        /// Útil para jogos 2D.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vec3 ToVec3XY(float z = 0f)
            => new Vec3(X, Y, z);

        /// <summary>
        /// Cria Vec2 a partir de Vec3 (ignora Y).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec2 FromVec3XZ(Vec3 v)
            => new Vec2(v.X, v.Z);

        /// <summary>
        /// Cria Vec2 a partir de Vec3 (ignora Z).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec2 FromVec3XY(Vec3 v)
            => new Vec2(v.X, v.Y);

        #endregion

        #region IEquatable

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Vec2 other)
            => X == other.X && Y == other.Y;

        public override bool Equals(object obj)
            => obj is Vec2 other && Equals(other);

        public override int GetHashCode()
            => HashCode.Combine(X, Y);

        #endregion

        #region ToString

        public override string ToString()
            => $"({X:F2}, {Y:F2})";

        public string ToString(string format)
            => $"({X.ToString(format)}, {Y.ToString(format)})";

        #endregion

        #region Aproximação

        public bool Approximately(Vec2 other, float tolerance = 0.0001f)
        {
            return MathF.Abs(X - other.X) < tolerance &&
                   MathF.Abs(Y - other.Y) < tolerance;
        }

        #endregion
    }
}