// XL4Net.Shared/Protocol/Enums/PacketType.cs

namespace XL4Net.Shared.Protocol.Enums
{
    /// <summary>
    /// Tipos de packet do protocolo XL4Net.
    /// Identifica o propósito do packet sem precisar desserializar o Payload.
    /// </summary>
    /// <remarks>
    /// Sistema de tipos:
    /// - 0-9: Packets de controle (handshake, heartbeat)
    /// - 10-99: Packets de gameplay
    /// - 100+: Reservado para extensões
    /// 
    /// Usado no campo Packet.Type (1 byte, valores 0-255).
    /// </remarks>
    public enum PacketType : byte
    {
        // ====================================================================
        // CONTROLE DE CONEXÃO (0-9)
        // ====================================================================

        /// <summary>
        /// Handshake inicial (SYN).
        /// Cliente → Servidor: Inicia conexão, envia magic number e versão.
        /// </summary>
        Handshake = 0,

        /// <summary>
        /// Resposta ao handshake (SYN-ACK).
        /// Servidor → Cliente: Aceita conexão, envia session ID.
        /// </summary>
        HandshakeAck = 1,

        /// <summary>
        /// Ping (heartbeat).
        /// Cliente → Servidor: Mantém conexão viva, inclui timestamp para calcular latência.
        /// </summary>
        Ping = 2,

        /// <summary>
        /// Pong (resposta ao heartbeat).
        /// Servidor → Cliente: Responde ping, echo do timestamp + server tick.
        /// </summary>
        Pong = 3,

        /// <summary>
        /// Desconexão graceful.
        /// Qualquer lado: Notifica que está fechando a conexão.
        /// </summary>
        Disconnect = 4,

        // ====================================================================
        // GAMEPLAY (10-99)
        // ====================================================================

        /// <summary>
        /// Dados genéricos de jogo.
        /// Payload contém mensagem serializada (MessagePack).
        /// </summary>
        Data = 10,

        /// <summary>
        /// Input de movimento do player.
        /// Cliente → Servidor: Direção, timestamp para client-side prediction.
        /// </summary>
        PlayerMove = 11,

        /// <summary>
        /// Input de ataque do player.
        /// Cliente → Servidor: Tipo de ataque, target, timestamp.
        /// </summary>
        PlayerAttack = 12,

        /// <summary>
        /// Estado do player (snapshot).
        /// Servidor → Cliente: Posição, rotação, velocidade para reconciliation.
        /// </summary>
        PlayerState = 13,

        /// <summary>
        /// Spawn de entidade.
        /// Servidor → Cliente: Nova entidade entrou no AOI.
        /// </summary>
        EntitySpawn = 20,

        /// <summary>
        /// Despawn de entidade.
        /// Servidor → Cliente: Entidade saiu do AOI ou foi destruída.
        /// </summary>
        EntityDespawn = 21,

        /// <summary>
        /// Update de entidade.
        /// Servidor → Cliente: Estado atualizado de entidade no AOI.
        /// </summary>
        EntityUpdate = 22,

        /// <summary>
        /// Mensagem de chat.
        /// Cliente ↔ Servidor: Texto de chat.
        /// </summary>
        Chat = 30,

        // ====================================================================
        // RESERVADO (100+)
        // ====================================================================

        /// <summary>
        /// Extensão customizada 1.
        /// Reservado para uso futuro ou mods.
        /// </summary>
        Custom1 = 100,

        /// <summary>
        /// Extensão customizada 2.
        /// Reservado para uso futuro ou mods.
        /// </summary>
        Custom2 = 101
    }
}