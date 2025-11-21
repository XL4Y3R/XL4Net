// XL4Net.Shared/Protocol/Messages/Game/PlayerInputMessage.cs

using MessagePack;
using XL4Net.Shared.Prediction;
using XL4Net.Shared.Protocol.Enums;

namespace XL4Net.Shared.Protocol.Messages.Game
{
    /// <summary>
    /// Mensagem de input do jogador.
    /// Enviada do cliente para o servidor.
    /// </summary>
    /// <remarks>
    /// IMPORTANTE: Key(0) DEVE ser MessageType para PeekMessageType funcionar!
    /// </remarks>
    [MessagePackObject]
    public class PlayerInputMessage
    {
        /// <summary>
        /// Tipo da mensagem (para identificação no dispatch).
        /// DEVE ser o primeiro campo (Key 0)!
        /// </summary>
        [Key(0)]
        public MessageType Type { get; set; } = MessageType.PlayerInput;

        /// <summary>
        /// Dados do input.
        /// </summary>
        [Key(1)]
        public InputData Input { get; set; }

        /// <summary>
        /// Construtor padrão (para deserialização).
        /// </summary>
        public PlayerInputMessage()
        {
            Type = MessageType.PlayerInput;
            Input = new InputData();
        }

        /// <summary>
        /// Cria mensagem com input especificado.
        /// </summary>
        public PlayerInputMessage(InputData input)
        {
            Type = MessageType.PlayerInput;
            Input = input;
        }

        public override string ToString()
        {
            return $"PlayerInputMsg[Tick={Input.Tick}, Seq={Input.SequenceNumber}, " +
                   $"Move={Input.MoveDirection}]";
        }
    }

    /// <summary>
    /// Mensagem com múltiplos inputs.
    /// Usado para redundância e recuperação de pacotes perdidos.
    /// </summary>
    [MessagePackObject]
    public class PlayerInputBatchMessage
    {
        /// <summary>
        /// Tipo da mensagem.
        /// </summary>
        [Key(0)]
        public MessageType Type { get; set; } = MessageType.PlayerInputBatch;

        /// <summary>
        /// Lista de inputs (ordenados por SequenceNumber).
        /// </summary>
        [Key(1)]
        public InputData[] Inputs { get; set; }

        /// <summary>
        /// Tick local do cliente quando enviou.
        /// </summary>
        [Key(2)]
        public uint ClientTick { get; set; }

        /// <summary>
        /// Construtor padrão.
        /// </summary>
        public PlayerInputBatchMessage()
        {
            Type = MessageType.PlayerInputBatch;
            Inputs = System.Array.Empty<InputData>();
            ClientTick = 0;
        }

        /// <summary>
        /// Cria mensagem com inputs especificados.
        /// </summary>
        public PlayerInputBatchMessage(InputData[] inputs, uint clientTick)
        {
            Type = MessageType.PlayerInputBatch;
            Inputs = inputs ?? System.Array.Empty<InputData>();
            ClientTick = clientTick;
        }

        /// <summary>
        /// Quantidade de inputs na mensagem.
        /// </summary>
        [IgnoreMember]
        public int Count => Inputs?.Length ?? 0;

        /// <summary>
        /// Input mais recente (último no array).
        /// </summary>
        [IgnoreMember]
        public InputData Latest => Count > 0 ? Inputs[Count - 1] : default;

        public override string ToString()
        {
            if (Count == 0)
                return "PlayerInputBatchMsg[Empty]";

            return $"PlayerInputBatchMsg[Count={Count}, " +
                   $"Seq={Inputs[0].SequenceNumber}-{Latest.SequenceNumber}]";
        }
    }
}