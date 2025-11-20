// XL4Net.Shared/Protocol/Messages/Auth/LoginResponseMessage.cs

using System;
using MessagePack;
using XL4Net.Shared.Protocol.Enums;

namespace XL4Net.Shared.Protocol.Messages.Auth
{
    /// <summary>
    /// Mensagem de resposta de login.
    /// AuthServer → Cliente
    /// </summary>
    [MessagePackObject]
    public class LoginResponseMessage
    {
        /// <summary>
        /// Tipo da mensagem (sempre LoginResponse).
        /// </summary>
        [Key(0)]
        public MessageType Type { get; set; } = MessageType.LoginResponse;

        /// <summary>
        /// TRUE = login bem-sucedido (token gerado)
        /// FALSE = falha (veja ErrorMessage)
        /// </summary>
        [Key(1)]
        public bool Success { get; set; }

        /// <summary>
        /// Token JWT (se sucesso).
        /// Cliente deve enviar este token no header ao conectar no GameServer.
        /// </summary>
        [Key(2)]
        public string? Token { get; set; }

        /// <summary>
        /// Data/hora de expiração do token (UTC ticks).
        /// Converter com: new DateTime(ExpiresAtTicks, DateTimeKind.Utc)
        /// </summary>
        [Key(3)]
        public long ExpiresAtTicks { get; set; }

        /// <summary>
        /// User ID (se sucesso).
        /// </summary>
        [Key(4)]
        public string? UserId { get; set; }

        /// <summary>
        /// Username (se sucesso).
        /// </summary>
        [Key(5)]
        public string? Username { get; set; }

        /// <summary>
        /// Mensagem de erro (se falha).
        /// Exemplos: "Invalid username or password", "Too many attempts"
        /// </summary>
        [Key(6)]
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// TRUE se bloqueado por rate limiting.
        /// </summary>
        [Key(7)]
        public bool IsRateLimited { get; set; }

        /// <summary>
        /// Segundos até poder tentar novamente (se rate limited).
        /// </summary>
        [Key(8)]
        public int RetryAfterSeconds { get; set; }

        /// <summary>
        /// Construtor padrão (requerido pelo MessagePack).
        /// </summary>
        public LoginResponseMessage() { }

        /// <summary>
        /// Data/hora de expiração (helper property, não serializado).
        /// </summary>
        [IgnoreMember]
        public DateTime ExpiresAt
        {
            get => new DateTime(ExpiresAtTicks, DateTimeKind.Utc);
            set => ExpiresAtTicks = value.Ticks;
        }

        /// <summary>
        /// Cria resposta de sucesso.
        /// </summary>
        public static LoginResponseMessage CreateSuccess(string token, DateTime expiresAt, Guid userId, string username)
        {
            return new LoginResponseMessage
            {
                Success = true,
                Token = token,
                ExpiresAtTicks = expiresAt.Ticks,
                UserId = userId.ToString(),
                Username = username,
                ErrorMessage = null,
                IsRateLimited = false,
                RetryAfterSeconds = 0
            };
        }

        /// <summary>
        /// Cria resposta de falha.
        /// </summary>
        public static LoginResponseMessage CreateFailure(string errorMessage)
        {
            return new LoginResponseMessage
            {
                Success = false,
                Token = null,
                ExpiresAtTicks = 0,
                UserId = null,
                Username = null,
                ErrorMessage = errorMessage,
                IsRateLimited = false,
                RetryAfterSeconds = 0
            };
        }

        /// <summary>
        /// Cria resposta de rate limiting.
        /// </summary>
        public static LoginResponseMessage CreateRateLimited(string message, int retryAfterSeconds)
        {
            return new LoginResponseMessage
            {
                Success = false,
                Token = null,
                ExpiresAtTicks = 0,
                UserId = null,
                Username = null,
                ErrorMessage = message,
                IsRateLimited = true,
                RetryAfterSeconds = retryAfterSeconds
            };
        }

        /// <summary>
        /// Retorna string legível para logs.
        /// </summary>
        public override string ToString()
        {
            if (Success)
                return $"LoginResponse[SUCCESS]: {Username} ({UserId})";
            else if (IsRateLimited)
                return $"LoginResponse[RATE_LIMITED]: {ErrorMessage} - Retry after {RetryAfterSeconds}s";
            else
                return $"LoginResponse[FAILURE]: {ErrorMessage}";
        }
    }
}