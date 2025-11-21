// XL4Net.Shared/Prediction/InputData.cs

using System;
using MessagePack;
using XL4Net.Shared.Math;

namespace XL4Net.Shared.Prediction
{
    /// <summary>
    /// Representa os inputs do jogador em um tick específico.
    /// Enviado do cliente para o servidor.
    /// </summary>
    /// <remarks>
    /// Esta estrutura é enviada toda vez que o cliente tem input para processar.
    /// O servidor recebe, valida, e aplica o movimento.
    /// 
    /// IMPORTANTE: Esta é uma estrutura genérica. Jogos específicos podem
    /// criar suas próprias estruturas de input herdando ou compondo com esta.
    /// 
    /// Fluxo:
    /// 1. Cliente captura input (WASD, mouse, etc)
    /// 2. Empacota em InputData com tick atual
    /// 3. Executa localmente (prediction)
    /// 4. Envia para servidor
    /// 5. Servidor valida e executa
    /// 6. Servidor envia StateSnapshot de volta
    /// </remarks>
    [MessagePackObject]
    public struct InputData : IEquatable<InputData>
    {
        /// <summary>
        /// Tick do servidor em que este input foi gerado.
        /// Usado para sincronização e reconciliation.
        /// </summary>
        /// <remarks>
        /// O cliente estima o tick do servidor baseado em ping.
        /// Este valor é crucial para o servidor saber "quando" o input aconteceu.
        /// </remarks>
        [Key(0)]
        public uint Tick;

        /// <summary>
        /// Número sequencial do input (incrementa a cada input).
        /// Usado para identificar quais inputs foram processados.
        /// </summary>
        /// <remarks>
        /// Diferente do Tick que pode ter gaps (se não houver input),
        /// o SequenceNumber é contínuo.
        /// </remarks>
        [Key(1)]
        public uint SequenceNumber;

        /// <summary>
        /// Direção de movimento normalizada (XZ plane).
        /// X = direita/esquerda, Y = frente/trás.
        /// </summary>
        /// <remarks>
        /// Valores típicos:
        /// - W = (0, 1)
        /// - S = (0, -1)
        /// - A = (-1, 0)
        /// - D = (1, 0)
        /// - W+D = (0.707, 0.707) normalizado
        /// </remarks>
        [Key(2)]
        public Vec2 MoveDirection;

        /// <summary>
        /// Direção de olhar/mira (onde o jogador está olhando).
        /// Usado para rotação do personagem.
        /// </summary>
        [Key(3)]
        public Vec2 LookDirection;

        /// <summary>
        /// Ângulo de rotação Y (yaw) em graus.
        /// Alternativa ao LookDirection para controles mais simples.
        /// </summary>
        [Key(4)]
        public float Rotation;

        /// <summary>
        /// Flags de ações (compactado em bits para eficiência).
        /// </summary>
        /// <remarks>
        /// Bit 0: Jump
        /// Bit 1: Sprint
        /// Bit 2: Crouch
        /// Bit 3: PrimaryAction (ataque/uso)
        /// Bit 4: SecondaryAction (mirar/bloquear)
        /// Bit 5: Interact
        /// Bit 6-7: Reservado
        /// </remarks>
        [Key(5)]
        public byte ActionFlags;

        #region Action Flags Helpers

        // Constantes para os bits
        private const byte FLAG_JUMP = 1 << 0;           // 0b00000001
        private const byte FLAG_SPRINT = 1 << 1;         // 0b00000010
        private const byte FLAG_CROUCH = 1 << 2;         // 0b00000100
        private const byte FLAG_PRIMARY_ACTION = 1 << 3; // 0b00001000
        private const byte FLAG_SECONDARY_ACTION = 1 << 4; // 0b00010000
        private const byte FLAG_INTERACT = 1 << 5;       // 0b00100000

        /// <summary>Jogador quer pular</summary>
        [IgnoreMember]
        public bool Jump
        {
            get => (ActionFlags & FLAG_JUMP) != 0;
            set => ActionFlags = value
                ? (byte)(ActionFlags | FLAG_JUMP)
                : (byte)(ActionFlags & ~FLAG_JUMP);
        }

        /// <summary>Jogador está correndo</summary>
        [IgnoreMember]
        public bool Sprint
        {
            get => (ActionFlags & FLAG_SPRINT) != 0;
            set => ActionFlags = value
                ? (byte)(ActionFlags | FLAG_SPRINT)
                : (byte)(ActionFlags & ~FLAG_SPRINT);
        }

        /// <summary>Jogador está agachado</summary>
        [IgnoreMember]
        public bool Crouch
        {
            get => (ActionFlags & FLAG_CROUCH) != 0;
            set => ActionFlags = value
                ? (byte)(ActionFlags | FLAG_CROUCH)
                : (byte)(ActionFlags & ~FLAG_CROUCH);
        }

        /// <summary>Ação primária (atacar, usar, etc)</summary>
        [IgnoreMember]
        public bool PrimaryAction
        {
            get => (ActionFlags & FLAG_PRIMARY_ACTION) != 0;
            set => ActionFlags = value
                ? (byte)(ActionFlags | FLAG_PRIMARY_ACTION)
                : (byte)(ActionFlags & ~FLAG_PRIMARY_ACTION);
        }

        /// <summary>Ação secundária (mirar, bloquear, etc)</summary>
        [IgnoreMember]
        public bool SecondaryAction
        {
            get => (ActionFlags & FLAG_SECONDARY_ACTION) != 0;
            set => ActionFlags = value
                ? (byte)(ActionFlags | FLAG_SECONDARY_ACTION)
                : (byte)(ActionFlags & ~FLAG_SECONDARY_ACTION);
        }

        /// <summary>Interagir com objeto/NPC</summary>
        [IgnoreMember]
        public bool Interact
        {
            get => (ActionFlags & FLAG_INTERACT) != 0;
            set => ActionFlags = value
                ? (byte)(ActionFlags | FLAG_INTERACT)
                : (byte)(ActionFlags & ~FLAG_INTERACT);
        }

        /// <summary>
        /// Verifica se existe algum input (movimento ou ação).
        /// Útil para otimização - não enviar inputs vazios.
        /// </summary>
        [IgnoreMember]
        public bool HasInput =>
            MoveDirection.SqrMagnitude > 0.001f ||
            ActionFlags != 0;

        #endregion

        #region Factory Methods

        /// <summary>
        /// Cria InputData vazio (sem input).
        /// </summary>
        public static InputData Empty(uint tick, uint sequenceNumber)
        {
            return new InputData
            {
                Tick = tick,
                SequenceNumber = sequenceNumber,
                MoveDirection = Vec2.Zero,
                LookDirection = Vec2.Zero,
                Rotation = 0f,
                ActionFlags = 0
            };
        }

        /// <summary>
        /// Cria InputData apenas com movimento.
        /// </summary>
        public static InputData Movement(uint tick, uint sequenceNumber, Vec2 direction)
        {
            return new InputData
            {
                Tick = tick,
                SequenceNumber = sequenceNumber,
                MoveDirection = direction,
                LookDirection = Vec2.Zero,
                Rotation = 0f,
                ActionFlags = 0
            };
        }

        #endregion

        #region IEquatable

        public bool Equals(InputData other)
        {
            return Tick == other.Tick &&
                   SequenceNumber == other.SequenceNumber &&
                   MoveDirection == other.MoveDirection &&
                   LookDirection == other.LookDirection &&
                   MathF.Abs(Rotation - other.Rotation) < 0.01f &&
                   ActionFlags == other.ActionFlags;
        }

        public override bool Equals(object obj)
            => obj is InputData other && Equals(other);

        public override int GetHashCode()
            => HashCode.Combine(Tick, SequenceNumber, MoveDirection, ActionFlags);

        public static bool operator ==(InputData left, InputData right)
            => left.Equals(right);

        public static bool operator !=(InputData left, InputData right)
            => !left.Equals(right);

        #endregion

        #region ToString

        public override string ToString()
        {
            return $"Input[Tick={Tick}, Seq={SequenceNumber}, Move={MoveDirection}, " +
                   $"Jump={Jump}, Sprint={Sprint}]";
        }

        #endregion
    }
}