using System;
using MessagePack;
using XL4Net.Shared.Math;

namespace XL4Net.Shared.Prediction
{
    /// <summary>
    /// Representa o estado completo de uma entidade em um tick específico.
    /// Usado para reconciliation entre cliente e servidor.
    /// </summary>
    /// <remarks>
    /// Este snapshot é enviado do servidor para o cliente dizendo:
    /// "No tick X, após processar seu input Y, você estava em Z"
    /// 
    /// O cliente compara com seu estado predito no mesmo tick.
    /// Se diferente, aplica correção (reconciliation).
    /// 
    /// Fluxo:
    /// 1. Servidor processa InputData do cliente
    /// 2. Servidor cria StateSnapshot com posição resultante
    /// 3. Servidor envia StateSnapshot para cliente
    /// 4. Cliente compara com sua predição
    /// 5. Se diferente, cliente corrige (rollback + replay)
    /// </remarks>
    [MessagePackObject]
    public struct StateSnapshot : IEquatable<StateSnapshot>
    {
        /// <summary>
        /// Tick do servidor em que este estado foi calculado.
        /// </summary>
        [Key(0)]
        public uint Tick;

        /// <summary>
        /// Último SequenceNumber de input processado pelo servidor.
        /// Cliente usa para saber quais inputs descartar do buffer.
        /// </summary>
        /// <remarks>
        /// Exemplo: Se LastProcessedInput = 50, cliente remove
        /// todos os inputs com SequenceNumber ≤ 50 do buffer.
        /// </remarks>
        [Key(1)]
        public uint LastProcessedInput;

        /// <summary>
        /// Posição no mundo.
        /// </summary>
        [Key(2)]
        public Vec3 Position;

        /// <summary>
        /// Velocidade atual.
        /// Importante para física (gravidade, inércia).
        /// </summary>
        [Key(3)]
        public Vec3 Velocity;

        /// <summary>
        /// Rotação Y (yaw) em graus.
        /// </summary>
        /// <remarks>
        /// Para simplicidade, só guardamos rotação Y.
        /// Jogos que precisam de rotação completa podem usar Quaternion.
        /// </remarks>
        [Key(4)]
        public float Rotation;

        /// <summary>
        /// Flags de estado (compactado em bits).
        /// </summary>
        /// <remarks>
        /// Bit 0: IsGrounded (no chão)
        /// Bit 1: IsSprinting
        /// Bit 2: IsCrouching
        /// Bit 3: IsJumping
        /// Bit 4: IsFalling
        /// Bit 5-7: Reservado
        /// </remarks>
        [Key(5)]
        public byte StateFlags;

        #region State Flags Helpers

        private const byte FLAG_GROUNDED = 1 << 0;
        private const byte FLAG_SPRINTING = 1 << 1;
        private const byte FLAG_CROUCHING = 1 << 2;
        private const byte FLAG_JUMPING = 1 << 3;
        private const byte FLAG_FALLING = 1 << 4;

        /// <summary>Entidade está no chão</summary>
        [IgnoreMember]
        public bool IsGrounded
        {
            get => (StateFlags & FLAG_GROUNDED) != 0;
            set => StateFlags = value
                ? (byte)(StateFlags | FLAG_GROUNDED)
                : (byte)(StateFlags & ~FLAG_GROUNDED);
        }

        /// <summary>Entidade está correndo</summary>
        [IgnoreMember]
        public bool IsSprinting
        {
            get => (StateFlags & FLAG_SPRINTING) != 0;
            set => StateFlags = value
                ? (byte)(StateFlags | FLAG_SPRINTING)
                : (byte)(StateFlags & ~FLAG_SPRINTING);
        }

        /// <summary>Entidade está agachada</summary>
        [IgnoreMember]
        public bool IsCrouching
        {
            get => (StateFlags & FLAG_CROUCHING) != 0;
            set => StateFlags = value
                ? (byte)(StateFlags | FLAG_CROUCHING)
                : (byte)(StateFlags & ~FLAG_CROUCHING);
        }

        /// <summary>Entidade está pulando (subindo)</summary>
        [IgnoreMember]
        public bool IsJumping
        {
            get => (StateFlags & FLAG_JUMPING) != 0;
            set => StateFlags = value
                ? (byte)(StateFlags | FLAG_JUMPING)
                : (byte)(StateFlags & ~FLAG_JUMPING);
        }

        /// <summary>Entidade está caindo</summary>
        [IgnoreMember]
        public bool IsFalling
        {
            get => (StateFlags & FLAG_FALLING) != 0;
            set => StateFlags = value
                ? (byte)(StateFlags | FLAG_FALLING)
                : (byte)(StateFlags & ~FLAG_FALLING);
        }

        #endregion

        #region Factory Methods

        /// <summary>
        /// Cria snapshot inicial (spawn).
        /// </summary>
        public static StateSnapshot Initial(uint tick, Vec3 position, float rotation = 0f)
        {
            return new StateSnapshot
            {
                Tick = tick,
                LastProcessedInput = 0,
                Position = position,
                Velocity = Vec3.Zero,
                Rotation = rotation,
                StateFlags = FLAG_GROUNDED // Começa no chão
            };
        }

        /// <summary>
        /// Cria snapshot a partir de outro, avançando o tick.
        /// Útil para clonar estado entre ticks.
        /// </summary>
        public static StateSnapshot FromPrevious(StateSnapshot previous, uint newTick)
        {
            var snapshot = previous;
            snapshot.Tick = newTick;
            return snapshot;
        }

        #endregion

        #region Comparison Methods

        /// <summary>
        /// Verifica se dois snapshots são aproximadamente iguais.
        /// Usado na reconciliation para decidir se precisa corrigir.
        /// </summary>
        /// <param name="other">Snapshot a comparar</param>
        /// <param name="positionTolerance">Tolerância de posição (padrão: 0.01)</param>
        /// <param name="velocityTolerance">Tolerância de velocidade (padrão: 0.1)</param>
        /// <returns>True se os snapshots são considerados iguais</returns>
        public bool ApproximatelyEquals(StateSnapshot other,
            float positionTolerance = 0.01f,
            float velocityTolerance = 0.1f)
        {
            // Posição é o mais importante
            if (!Position.Approximately(other.Position, positionTolerance))
                return false;

            // Velocidade afeta física futura
            if (!Velocity.Approximately(other.Velocity, velocityTolerance))
                return false;

            // Flags de estado afetam comportamento
            if (StateFlags != other.StateFlags)
                return false;

            return true;
        }

        /// <summary>
        /// Calcula a diferença de posição entre dois snapshots.
        /// Útil para métricas e debugging.
        /// </summary>
        public float PositionDelta(StateSnapshot other)
        {
            return Vec3.Distance(Position, other.Position);
        }

        /// <summary>
        /// Interpola entre dois snapshots.
        /// Usado para smoothing visual após correção.
        /// </summary>
        /// <param name="from">Snapshot inicial</param>
        /// <param name="to">Snapshot final</param>
        /// <param name="t">Fator de interpolação (0-1)</param>
        public static StateSnapshot Lerp(StateSnapshot from, StateSnapshot to, float t)
        {
            return new StateSnapshot
            {
                Tick = t < 0.5f ? from.Tick : to.Tick,
                LastProcessedInput = t < 0.5f ? from.LastProcessedInput : to.LastProcessedInput,
                Position = Vec3.Lerp(from.Position, to.Position, t),
                Velocity = Vec3.Lerp(from.Velocity, to.Velocity, t),
                Rotation = LerpAngle(from.Rotation, to.Rotation, t),
                StateFlags = t < 0.5f ? from.StateFlags : to.StateFlags
            };
        }

        /// <summary>
        /// Interpolação de ângulo (lida com wrap-around 360°).
        /// </summary>
        private static float LerpAngle(float from, float to, float t)
        {
            float delta = ((to - from + 540f) % 360f) - 180f;
            return from + delta * t;
        }

        #endregion

        #region IEquatable

        public bool Equals(StateSnapshot other)
        {
            return Tick == other.Tick &&
                   LastProcessedInput == other.LastProcessedInput &&
                   Position == other.Position &&
                   Velocity == other.Velocity &&
                   MathF.Abs(Rotation - other.Rotation) < 0.01f &&
                   StateFlags == other.StateFlags;
        }

        public override bool Equals(object obj)
            => obj is StateSnapshot other && Equals(other);

        public override int GetHashCode()
            => HashCode.Combine(Tick, Position, Velocity, StateFlags);

        public static bool operator ==(StateSnapshot left, StateSnapshot right)
            => left.Equals(right);

        public static bool operator !=(StateSnapshot left, StateSnapshot right)
            => !left.Equals(right);

        #endregion

        #region ToString

        public override string ToString()
        {
            return $"State[Tick={Tick}, Pos={Position}, Vel={Velocity}, " +
                   $"Grounded={IsGrounded}, LastInput={LastProcessedInput}]";
        }

        #endregion
    }
}