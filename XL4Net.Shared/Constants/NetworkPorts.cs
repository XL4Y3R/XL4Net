namespace XL4Net.Shared.Constants
{
    /// <summary>
    /// Portas padrão usadas pelo XL4Net.
    /// </summary>
    public static class NetworkPorts
    {
        /// <summary>
        /// Porta TCP do AuthServer (autenticação).
        /// </summary>
        public const ushort AUTH_TCP = 2106;

        /// <summary>
        /// Porta TCP do GameServer (conexão principal).
        /// </summary>
        public const ushort GAME_TCP = 7777;

        /// <summary>
        /// Porta UDP do GameServer (gameplay rápido).
        /// </summary>
        public const ushort GAME_UDP = 7778;
    }
}