// XL4Net.AuthServer/Models/ValidateTokenResponse.cs

using System;

namespace XL4Net.AuthServer.Models
{
    /// <summary>
    /// Response DTO para endpoint /auth/validate-token.
    /// Retornado ao GameServer indicando se o token é válido.
    /// </summary>
    public class ValidateTokenResponse
    {
        /// <summary>
        /// TRUE = token válido e não expirado
        /// FALSE = token inválido, expirado ou assinatura incorreta
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// ID do usuário (se token válido).
        /// NULL se token inválido.
        /// </summary>
        public Guid? UserId { get; set; }

        /// <summary>
        /// Username do usuário (se token válido).
        /// NULL se token inválido.
        /// </summary>
        public string? Username { get; set; }

        /// <summary>
        /// Mensagem de erro (se token inválido).
        /// Exemplos: "Token expired", "Invalid signature", "Token malformed"
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Data/hora de expiração do token (UTC).
        /// NULL se token inválido.
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Cria uma resposta de sucesso (token válido).
        /// </summary>
        public static ValidateTokenResponse CreateSuccess(Guid userId, string username, DateTime expiresAt)
        {
            return new ValidateTokenResponse
            {
                IsValid = true,
                UserId = userId,
                Username = username,
                ExpiresAt = expiresAt,
                ErrorMessage = null
            };
        }

        /// <summary>
        /// Cria uma resposta de falha (token inválido).
        /// </summary>
        public static ValidateTokenResponse CreateFailure(string errorMessage)
        {
            return new ValidateTokenResponse
            {
                IsValid = false,
                UserId = null,
                Username = null,
                ExpiresAt = null,
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