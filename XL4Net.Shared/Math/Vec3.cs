using System;
using System.Runtime.CompilerServices;
using MessagePack;

namespace XL4Net.Shared.Math
{
    /// <summary>
    /// Vetor 3D próprio do XL4Net.
    /// Engine-agnostic - não depende de Unity, Godot, etc.
    /// Usado para comunicação entre cliente e servidor.
    /// </summary>
    /// <remarks>
    /// Para converter de/para Unity:
    /// - Unity → XL4Net: new Vec3(unityVector.x, unityVector.y, unityVector.z)
    /// - XL4Net → Unity: new Vector3(vec3.X, vec3.Y, vec3.Z)
    /// 
    /// Um projeto XL4Net.Unity pode adicionar extension methods para facilitar.
    /// </remarks>
    [MessagePackObject]
    public struct Vec3 : IEquatable<Vec3>
    {
        // Campos serializados pelo MessagePack
        [Key(0)]
        public float X;

        [Key(1)]
        public float Y;

        [Key(2)]
        public float Z;

        #region Construtores

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vec3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vec3(float value)
        {
            X = value;
            Y = value;
            Z = value;
        }

        #endregion

        #region Propriedades Estáticas

        /// <summary>Vetor zero (0, 0, 0)</summary>
        public static Vec3 Zero => new Vec3(0f, 0f, 0f);

        /// <summary>Vetor um (1, 1, 1)</summary>
        public static Vec3 One => new Vec3(1f, 1f, 1f);

        /// <summary>Direção para cima (0, 1, 0)</summary>
        public static Vec3 Up => new Vec3(0f, 1f, 0f);

        /// <summary>Direção para baixo (0, -1, 0)</summary>
        public static Vec3 Down => new Vec3(0f, -1f, 0f);

        /// <summary>Direção para frente (0, 0, 1)</summary>
        public static Vec3 Forward => new Vec3(0f, 0f, 1f);

        /// <summary>Direção para trás (0, 0, -1)</summary>
        public static Vec3 Back => new Vec3(0f, 0f, -1f);

        /// <summary>Direção para direita (1, 0, 0)</summary>
        public static Vec3 Right => new Vec3(1f, 0f, 0f);

        /// <summary>Direção para esquerda (-1, 0, 0)</summary>
        public static Vec3 Left => new Vec3(-1f, 0f, 0f);

        #endregion

        #region Propriedades Calculadas

        /// <summary>
        /// Magnitude (comprimento) do vetor.
        /// Usa raiz quadrada - mais lento que SqrMagnitude.
        /// </summary>
        [IgnoreMember]
        public float Magnitude
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => MathF.Sqrt(X * X + Y * Y + Z * Z);
        }

        /// <summary>
        /// Magnitude ao quadrado.
        /// Mais rápido que Magnitude - use para comparações.
        /// </summary>
        /// <example>
        /// // Ao invés de: if (vec.Magnitude &lt; 5f)
        /// // Use: if (vec.SqrMagnitude &lt; 25f) // 5² = 25
        /// </example>
        [IgnoreMember]
        public float SqrMagnitude
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => X * X + Y * Y + Z * Z;
        }

        /// <summary>
        /// Retorna vetor normalizado (magnitude = 1).
        /// Não modifica o vetor original.
        /// </summary>
        [IgnoreMember]
        public Vec3 Normalized
        {
            get
            {
                float mag = Magnitude;
                if (mag > 1E-05f)
                    return new Vec3(X / mag, Y / mag, Z / mag);
                return Zero;
            }
        }

        #endregion

        #region Operadores Aritméticos

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec3 operator +(Vec3 a, Vec3 b)
            => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec3 operator -(Vec3 a, Vec3 b)
            => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec3 operator *(Vec3 a, float d)
            => new Vec3(a.X * d, a.Y * d, a.Z * d);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec3 operator *(float d, Vec3 a)
            => new Vec3(a.X * d, a.Y * d, a.Z * d);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec3 operator /(Vec3 a, float d)
            => new Vec3(a.X / d, a.Y / d, a.Z / d);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec3 operator -(Vec3 a)
            => new Vec3(-a.X, -a.Y, -a.Z);

        #endregion

        #region Operadores de Comparação

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Vec3 a, Vec3 b)
            => a.X == b.X && a.Y == b.Y && a.Z == b.Z;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Vec3 a, Vec3 b)
            => !(a == b);

        #endregion

        #region Métodos Estáticos

        /// <summary>
        /// Produto escalar (dot product) entre dois vetores.
        /// Útil para calcular ângulos e projeções.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Dot(Vec3 a, Vec3 b)
            => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        /// <summary>
        /// Produto vetorial (cross product) entre dois vetores.
        /// Retorna vetor perpendicular a ambos.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec3 Cross(Vec3 a, Vec3 b)
            => new Vec3(
                a.Y * b.Z - a.Z * b.Y,
                a.Z * b.X - a.X * b.Z,
                a.X * b.Y - a.Y * b.X
            );

        /// <summary>
        /// Distância entre dois pontos.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Distance(Vec3 a, Vec3 b)
            => (a - b).Magnitude;

        /// <summary>
        /// Distância ao quadrado entre dois pontos.
        /// Mais rápido - use para comparações.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SqrDistance(Vec3 a, Vec3 b)
            => (a - b).SqrMagnitude;

        /// <summary>
        /// Interpolação linear entre dois vetores.
        /// t=0 retorna 'a', t=1 retorna 'b'.
        /// </summary>
        /// <param name="a">Vetor inicial</param>
        /// <param name="b">Vetor final</param>
        /// <param name="t">Fator de interpolação (0 a 1)</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec3 Lerp(Vec3 a, Vec3 b, float t)
        {
            // Clamp t entre 0 e 1
            t = t < 0f ? 0f : (t > 1f ? 1f : t);
            return new Vec3(
                a.X + (b.X - a.X) * t,
                a.Y + (b.Y - a.Y) * t,
                a.Z + (b.Z - a.Z) * t
            );
        }

        /// <summary>
        /// Interpolação linear sem clamp.
        /// t pode ser menor que 0 ou maior que 1.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec3 LerpUnclamped(Vec3 a, Vec3 b, float t)
            => new Vec3(
                a.X + (b.X - a.X) * t,
                a.Y + (b.Y - a.Y) * t,
                a.Z + (b.Z - a.Z) * t
            );

        /// <summary>
        /// Move vetor em direção a um alvo com velocidade máxima.
        /// </summary>
        /// <param name="current">Posição atual</param>
        /// <param name="target">Posição alvo</param>
        /// <param name="maxDistanceDelta">Distância máxima a mover</param>
        public static Vec3 MoveTowards(Vec3 current, Vec3 target, float maxDistanceDelta)
        {
            Vec3 diff = target - current;
            float sqrMag = diff.SqrMagnitude;

            // Já chegou ou muito perto
            if (sqrMag == 0f || (maxDistanceDelta >= 0f && sqrMag <= maxDistanceDelta * maxDistanceDelta))
                return target;

            float mag = MathF.Sqrt(sqrMag);
            return current + diff / mag * maxDistanceDelta;
        }

        /// <summary>
        /// Retorna vetor com valores absolutos.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec3 Abs(Vec3 v)
            => new Vec3(MathF.Abs(v.X), MathF.Abs(v.Y), MathF.Abs(v.Z));

        /// <summary>
        /// Retorna vetor com menores valores de cada componente.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec3 Min(Vec3 a, Vec3 b)
            => new Vec3(
                MathF.Min(a.X, b.X),
                MathF.Min(a.Y, b.Y),
                MathF.Min(a.Z, b.Z)
            );

        /// <summary>
        /// Retorna vetor com maiores valores de cada componente.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec3 Max(Vec3 a, Vec3 b)
            => new Vec3(
                MathF.Max(a.X, b.X),
                MathF.Max(a.Y, b.Y),
                MathF.Max(a.Z, b.Z)
            );

        /// <summary>
        /// Limita cada componente entre min e max.
        /// </summary>
        public static Vec3 Clamp(Vec3 value, Vec3 min, Vec3 max)
            => new Vec3(
                MathF.Max(min.X, MathF.Min(max.X, value.X)),
                MathF.Max(min.Y, MathF.Min(max.Y, value.Y)),
                MathF.Max(min.Z, MathF.Min(max.Z, value.Z))
            );

        /// <summary>
        /// Limita a magnitude do vetor.
        /// </summary>
        public static Vec3 ClampMagnitude(Vec3 vector, float maxLength)
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

        #region IEquatable

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Vec3 other)
            => X == other.X && Y == other.Y && Z == other.Z;

        public override bool Equals(object obj)
            => obj is Vec3 other && Equals(other);

        public override int GetHashCode()
            => HashCode.Combine(X, Y, Z);

        #endregion

        #region ToString

        public override string ToString()
            => $"({X:F2}, {Y:F2}, {Z:F2})";

        public string ToString(string format)
            => $"({X.ToString(format)}, {Y.ToString(format)}, {Z.ToString(format)})";

        #endregion

        #region Aproximação (para comparações com tolerância)

        /// <summary>
        /// Verifica se dois vetores são aproximadamente iguais.
        /// Útil para comparar posições após cálculos de float.
        /// </summary>
        /// <param name="other">Vetor a comparar</param>
        /// <param name="tolerance">Tolerância (padrão: 0.0001)</param>
        public bool Approximately(Vec3 other, float tolerance = 0.0001f)
        {
            return MathF.Abs(X - other.X) < tolerance &&
                   MathF.Abs(Y - other.Y) < tolerance &&
                   MathF.Abs(Z - other.Z) < tolerance;
        }

        #endregion
    }
}