// XL4Net.Shared/Protocol/Enums/MessageType.cs

namespace XL4Net.Shared.Protocol.Enums
{
    /// <summary>
    /// Tipos de mensagens de rede.
    /// Usado para identificar o conteúdo do payload em PacketType.Data.
    /// </summary>
    public enum MessageType : ushort
    {
        // ============================================
        // SISTEMA (0-99)
        // ============================================
        Unknown = 0,
        Ping = 1,
        Pong = 2,
        Disconnect = 3,

        // ============================================
        // AUTENTICAÇÃO (100-199)
        // ============================================
        RegisterRequest = 100,
        RegisterResponse = 101,
        LoginRequest = 102,
        LoginResponse = 103,
        TokenValidationRequest = 104,
        TokenValidationResponse = 105,

        // ============================================
        // PREDICTION/RECONCILIATION (200-219)
        // ============================================

        /// <summary>
        /// Input do jogador (cliente → servidor).
        /// Contém InputData com movimento, pulo, etc.
        /// </summary>
        PlayerInput = 200,

        /// <summary>
        /// Batch de inputs (cliente → servidor).
        /// Múltiplos inputs para redundância.
        /// </summary>
        PlayerInputBatch = 201,

        /// <summary>
        /// Estado autoritativo do jogador (servidor → cliente).
        /// Usado para reconciliation.
        /// </summary>
        PlayerState = 202,

        /// <summary>
        /// Snapshot do mundo (servidor → cliente).
        /// Estados de outros jogadores para interpolação.
        /// </summary>
        WorldSnapshot = 203,

        // ============================================
        // GAMEPLAY LEGADO (220-299)
        // Mantido para compatibilidade
        // ============================================
        PlayerMove = 220,
        PlayerAttack = 221,
        EntitySpawn = 222,
        EntityDespawn = 223,
        EntityUpdate = 224,

        // ============================================
        // CHAT/SOCIAL (300-399)
        // ============================================
        ChatMessage = 300
    }
}