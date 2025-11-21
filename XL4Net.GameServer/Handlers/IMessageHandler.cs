// XL4Net.GameServer/Handlers/IMessageHandler.cs

using XL4Net.GameServer.Players;
using XL4Net.Shared.Protocol.Enums;
using XL4Net.Shared.Transport;

namespace XL4Net.GameServer.Handlers
{
    /// <summary>
    /// Interface base para handlers de mensagens.
    /// Implementa o Strategy Pattern para processar diferentes tipos de pacotes.
    /// </summary>
    /// <remarks>
    /// Cada tipo de mensagem (Auth, Move, Attack, Chat) tem seu próprio handler.
    /// O MessageHandlerRegistry faz o dispatch baseado no PacketType.
    /// 
    /// Ciclo de vida:
    /// 1. Registry recebe packet
    /// 2. Lookup O(1) pelo PacketType
    /// 3. Handler.Handle() é chamado
    /// 4. Handler processa e responde se necessário
    /// </remarks>
    public interface IMessageHandler
    {
        /// <summary>
        /// Tipo de packet que este handler processa.
        /// Usado pelo Registry para fazer o dispatch.
        /// </summary>
        PacketType PacketType { get; }

        /// <summary>
        /// Processa um packet recebido.
        /// </summary>
        /// <param name="context">Contexto com informações do servidor e jogador</param>
        /// <param name="packet">Packet recebido (já deserializado do wire)</param>
        /// <remarks>
        /// IMPORTANTE: 
        /// - Este método roda na main thread (game loop)
        /// - Não fazer operações blocking (I/O, database)
        /// - Se precisar de I/O, usar async e enfileirar resultado
        /// - O Packet deve ser retornado ao pool após uso!
        /// </remarks>
        void Handle(MessageContext context, Packet packet);
    }

    /// <summary>
    /// Contexto passado para os handlers.
    /// Contém referências necessárias para processar a mensagem.
    /// </summary>
    public class MessageContext
    {
        /// <summary>
        /// ID da conexão (PeerId do LiteNetLib).
        /// </summary>
        public int PeerId { get; }

        /// <summary>
        /// Sessão do jogador (pode ser null se ainda não autenticado).
        /// </summary>
        public PlayerSession? Session { get; }

        /// <summary>
        /// Referência ao servidor (para enviar respostas, broadcast, etc).
        /// </summary>
        public Core.GameServer Server { get; }

        /// <summary>
        /// Tick atual do servidor.
        /// </summary>
        public int CurrentTick => Server.CurrentTick;

        /// <summary>
        /// Construtor.
        /// </summary>
        public MessageContext(int peerId, PlayerSession? session, Core.GameServer server)
        {
            PeerId = peerId;
            Session = session;
            Server = server;
        }

        /// <summary>
        /// Jogador está autenticado?
        /// </summary>
        public bool IsAuthenticated => Session?.IsAuthenticated ?? false;

        /// <summary>
        /// Jogador está no jogo?
        /// </summary>
        public bool IsInGame => Session?.IsInGame ?? false;
    }
}