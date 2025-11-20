namespace XL4Net.Shared.Protocol.Enums
{
    /// <summary>
    /// Tipos de mensagens de rede.
    /// </summary>
    public enum MessageType : ushort
    {
        // Sistema (0-99)
        Unknown = 0,
        Ping = 1,
        Pong = 2,
        Disconnect = 3,

        // Autenticação (100-199)
        RegisterRequest = 100,
        RegisterResponse = 101,
        LoginRequest = 102,
        LoginResponse = 103,
        TokenValidationRequest = 104,
        TokenValidationResponse = 105,

        // Gameplay (200-299)
        PlayerMove = 200,
        PlayerAttack = 201,
        EntitySpawn = 202,
        EntityDespawn = 203,
        EntityUpdate = 204,

        // Chat/Social (300-399)
        ChatMessage = 300
    }
}