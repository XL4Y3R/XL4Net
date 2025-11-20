namespace XL4Net.Shared.Protocol.Enums
{
    /// <summary>
    /// Motivos de desconexão.
    /// </summary>
    public enum DisconnectReason : byte
    {
        /// <summary>
        /// Desconexão normal iniciada pelo cliente.
        /// </summary>
        ClientDisconnect = 0,

        /// <summary>
        /// Servidor foi desligado.
        /// </summary>
        ServerShutdown = 1,

        /// <summary>
        /// Timeout - sem heartbeat por muito tempo.
        /// </summary>
        Timeout = 2,

        /// <summary>
        /// Dados inválidos recebidos.
        /// </summary>
        InvalidData = 3,

        /// <summary>
        /// Autenticação falhou.
        /// </summary>
        AuthenticationFailed = 4,

        /// <summary>
        /// Servidor está cheio.
        /// </summary>
        ServerFull = 5,

        /// <summary>
        /// Versão do protocolo incompatível.
        /// </summary>
        VersionMismatch = 6
    }
}