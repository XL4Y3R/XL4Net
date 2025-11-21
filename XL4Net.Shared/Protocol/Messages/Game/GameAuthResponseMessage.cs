// XL4Net.Shared/Protocol/Messages/Game/GameAuthResponseMessage.cs

using System;
using MessagePack;
using XL4Net.Shared.Protocol.Enums;

namespace XL4Net.Shared.Protocol.Messages.Game
{
    /// <summary>
    /// Códigos de resultado da autenticação no GameServer.
    /// </summary>
    public enum GameAuthResult : byte
    {
        /// <summary>
        /// Autenticação bem-sucedida.
        /// </summary>
        Success = 0,

        /// <summary>
        /// Token inválido ou malformado.
        /// </summary>
        InvalidToken = 1,

        /// <summary>
        /// Token expirado.
        /// </summary>
        TokenExpired = 2,

        /// <summary>
        /// Usuário já está conectado (login duplo).
        /// </summary>
        AlreadyConnected = 3,

        /// <summary>
        /// Servidor cheio.
        /// </summary>
        ServerFull = 4,

        /// <summary>
        /// Versão do cliente incompatível.
        /// </summary>
        VersionMismatch = 5,

        /// <summary>
        /// Usuário banido.
        /// </summary>
        Banned = 6,

        /// <summary>
        /// Erro interno do servidor.
        /// </summary>
        InternalError = 99
    }

    /// <summary>
    /// Mensagem de resposta de autenticação no GameServer.
    /// GameServer → Cliente
    /// </summary>
    [MessagePackObject]
    public class GameAuthResponseMessage
    {
        /// <summary>
        /// Tipo da mensagem.
        /// </summary>
        [Key(0)]
        public MessageType Type { get; set; } = MessageType.TokenValidationResponse;

        /// <summary>
        /// Resultado da autenticação.
        /// </summary>
        [Key(1)]
        public GameAuthResult Result { get; set; }

        /// <summary>
        /// Mensagem descritiva (para debug/logs).
        /// </summary>
        [Key(2)]
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// User ID (se sucesso).
        /// </summary>
        [Key(3)]
        public string? UserId { get; set; }

        /// <summary>
        /// Username (se sucesso).
        /// </summary>
        [Key(4)]
        public string? Username { get; set; }

        /// <summary>
        /// Tick atual do servidor (para sincronização inicial).
        /// </summary>
        [Key(5)]
        public int ServerTick { get; set; }

        /// <summary>
        /// Timestamp do servidor em UTC ticks.
        /// </summary>
        [Key(6)]
        public long ServerTimeTicks { get; set; }

        /// <summary>
        /// Construtor padrão (requerido pelo MessagePack).
        /// </summary>
        public GameAuthResponseMessage() { }

        /// <summary>
        /// Autenticação foi bem-sucedida?
        /// </summary>
        [IgnoreMember]
        public bool Success => Result == GameAuthResult.Success;

        /// <summary>
        /// Timestamp do servidor como DateTime.
        /// </summary>
        [IgnoreMember]
        public DateTime ServerTime
        {
            get => new DateTime(ServerTimeTicks, DateTimeKind.Utc);
            set => ServerTimeTicks = value.Ticks;
        }

        /// <summary>
        /// Cria resposta de sucesso.
        /// </summary>
        public static GameAuthResponseMessage CreateSuccess(
            Guid userId,
            string username,
            int serverTick)
        {
            return new GameAuthResponseMessage
            {
                Result = GameAuthResult.Success,
                Message = "Authentication successful",
                UserId = userId.ToString(),
                Username = username,
                ServerTick = serverTick,
                ServerTimeTicks = DateTime.UtcNow.Ticks
            };
        }

        /// <summary>
        /// Cria resposta de falha.
        /// </summary>
        public static GameAuthResponseMessage CreateFailure(
            GameAuthResult result,
            string message)
        {
            return new GameAuthResponseMessage
            {
                Result = result,
                Message = message,
                UserId = null,
                Username = null,
                ServerTick = 0,
                ServerTimeTicks = DateTime.UtcNow.Ticks
            };
        }

        /// <summary>
        /// Retorna string legível para logs.
        /// </summary>
        public override string ToString()
        {
            if (Success)
                return $"GameAuthResponse[SUCCESS]: {Username} ({UserId}), Tick={ServerTick}";
            else
                return $"GameAuthResponse[{Result}]: {Message}";
        }
    }
}