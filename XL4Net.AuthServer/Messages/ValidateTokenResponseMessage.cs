// XL4Net.AuthServer/Messages/ValidateTokenResponseMessage.cs

using System;
using MessagePack;
using XL4Net.Shared.Protocol.Enums;

namespace XL4Net.AuthServer.Messages
{
    /// <summary>
    /// Mensagem de resposta de validação de token JWT.
    /// AuthServer → GameServer
    /// 
    /// NOTA: Esta mensagem NÃO está em Shared porque o cliente Unity não precisa dela.
    /// </summary>
    [MessagePackObject]
    public class ValidateTokenResponseMessage
    {
        /// <summary>
        /// Tipo da mensagem (sempre TokenValidationResponse).
        /// </summary>
        [Key(0)]
        public MessageType Type { get; set; } = MessageType.TokenValidationResponse;

        /// <summary>
        /// TRUE = token válido e não expirado
        /// FALSE = token inválido, expirado ou assinatura incorreta
        /// </summary>
        [Key(1)]
        public bool IsValid { get; set; }

        /// <summary>
        /// ID do usuário (se token válido).
        /// </summary>
        [Key(2)]
        public string? UserId { get; set; }

        /// <summary>
        /// Username do usuário (se token válido).
        /// </summary>
        [Key(3)]
        public string? Username { get; set; }

        /// <summary>
        /// Mensagem de erro (se token inválido).
        /// Exemplos: "Token expired", "Invalid signature"
        /// </summary>
        [Key(4)]
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Data/hora de expiração do token (UTC ticks).
        /// </summary>
        [Key(5)]
        public long ExpiresAtTicks { get; set; }

        /// <summary>
        /// Construtor padrão (requerido pelo MessagePack).
        /// </summary>
        public ValidateTokenResponseMessage() { }

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
        /// Cria resposta de sucesso (token válido).
        /// </summary>
        public static ValidateTokenResponseMessage CreateSuccess(Guid userId, string username, DateTime expiresAt)
        {
            return new ValidateTokenResponseMessage
            {
                IsValid = true,
                UserId = userId.ToString(),
                Username = username,
                ExpiresAtTicks = expiresAt.Ticks,
                ErrorMessage = null
            };
        }

        /// <summary>
        /// Cria resposta de falha (token inválido).
        /// </summary>
        public static ValidateTokenResponseMessage CreateFailure(string errorMessage)
        {
            return new ValidateTokenResponseMessage
            {
                IsValid = false,
                UserId = null,
                Username = null,
                ExpiresAtTicks = 0,
                ErrorMessage = errorMessage
            };
        }

        /// <summary>
        /// Retorna string legível para logs.
        /// </summary>
        public override string ToString()
        {
            if (IsValid)
                return $"ValidateTokenResponse[VALID]: User={Username} ({UserId}) - Expires: {ExpiresAt:yyyy-MM-dd HH:mm:ss}";
            else
                return $"ValidateTokenResponse[INVALID]: {ErrorMessage}";
        }
    }
}