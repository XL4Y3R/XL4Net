// XL4Net.Shared/Protocol/Messages/Game/PlayerStateMessage.cs

using MessagePack;
using XL4Net.Shared.Prediction;
using XL4Net.Shared.Protocol.Enums;

namespace XL4Net.Shared.Protocol.Messages.Game
{
    /// <summary>
    /// Mensagem de estado do jogador.
    /// Enviada do servidor para o cliente.
    /// </summary>
    /// <remarks>
    /// IMPORTANTE: Key(0) DEVE ser MessageType para PeekMessageType funcionar!
    /// </remarks>
    [MessagePackObject]
    public class PlayerStateMessage
    {
        /// <summary>
        /// Tipo da mensagem.
        /// </summary>
        [Key(0)]
        public MessageType Type { get; set; } = MessageType.PlayerState;

        /// <summary>
        /// Estado completo do jogador.
        /// </summary>
        [Key(1)]
        public StateSnapshot State { get; set; }

        /// <summary>
        /// Construtor padrão (para deserialização).
        /// </summary>
        public PlayerStateMessage()
        {
            Type = MessageType.PlayerState;
            State = new StateSnapshot();
        }

        /// <summary>
        /// Cria mensagem com estado especificado.
        /// </summary>
        public PlayerStateMessage(StateSnapshot state)
        {
            Type = MessageType.PlayerState;
            State = state;
        }

        public override string ToString()
        {
            return $"PlayerStateMsg[Tick={State.Tick}, Pos={State.Position}, " +
                   $"LastInput={State.LastProcessedInput}]";
        }
    }

    /// <summary>
    /// Mensagem com estados de múltiplos jogadores (snapshot do mundo).
    /// </summary>
    [MessagePackObject]
    public class WorldSnapshotMessage
    {
        /// <summary>
        /// Tipo da mensagem.
        /// </summary>
        [Key(0)]
        public MessageType Type { get; set; } = MessageType.WorldSnapshot;

        /// <summary>
        /// Tick do servidor quando snapshot foi criado.
        /// </summary>
        [Key(1)]
        public uint ServerTick { get; set; }

        /// <summary>
        /// Estados dos jogadores visíveis.
        /// </summary>
        [Key(2)]
        public PlayerSnapshot[] Players { get; set; }

        /// <summary>
        /// Construtor padrão.
        /// </summary>
        public WorldSnapshotMessage()
        {
            Type = MessageType.WorldSnapshot;
            ServerTick = 0;
            Players = System.Array.Empty<PlayerSnapshot>();
        }

        /// <summary>
        /// Quantidade de jogadores no snapshot.
        /// </summary>
        [IgnoreMember]
        public int PlayerCount => Players?.Length ?? 0;

        public override string ToString()
        {
            return $"WorldSnapshot[Tick={ServerTick}, Players={PlayerCount}]";
        }
    }

    /// <summary>
    /// Estado resumido de um jogador para snapshot.
    /// </summary>
    [MessagePackObject]
    public struct PlayerSnapshot
    {
        /// <summary>
        /// ID do jogador.
        /// </summary>
        [Key(0)]
        public int PlayerId { get; set; }

        /// <summary>
        /// Posição no mundo.
        /// </summary>
        [Key(1)]
        public Shared.Math.Vec3 Position { get; set; }

        /// <summary>
        /// Rotação Y em graus.
        /// </summary>
        [Key(2)]
        public float Rotation { get; set; }

        /// <summary>
        /// Velocidade (para interpolação/extrapolação).
        /// </summary>
        [Key(3)]
        public Shared.Math.Vec3 Velocity { get; set; }

        /// <summary>
        /// Flags de estado compactadas.
        /// </summary>
        [Key(4)]
        public byte StateFlags { get; set; }

        /// <summary>
        /// ID da animação atual.
        /// </summary>
        [Key(5)]
        public byte AnimationId { get; set; }

        /// <summary>
        /// Cria PlayerSnapshot a partir de StateSnapshot.
        /// </summary>
        public static PlayerSnapshot FromState(int playerId, StateSnapshot state)
        {
            return new PlayerSnapshot
            {
                PlayerId = playerId,
                Position = state.Position,
                Rotation = state.Rotation,
                Velocity = state.Velocity,
                StateFlags = state.StateFlags,
                AnimationId = 0
            };
        }

        /// <summary>
        /// Converte para StateSnapshot.
        /// </summary>
        public StateSnapshot ToStateSnapshot(uint tick)
        {
            return new StateSnapshot
            {
                Tick = tick,
                LastProcessedInput = 0,
                Position = Position,
                Velocity = Velocity,
                Rotation = Rotation,
                StateFlags = StateFlags
            };
        }

        public override string ToString()
        {
            return $"PlayerSnapshot[Id={PlayerId}, Pos={Position}]";
        }
    }
}