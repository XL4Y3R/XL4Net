// XL4Net.AuthServer/Models/AuthToken.cs

using System;

namespace XL4Net.AuthServer.Models
{
    /// <summary>
    /// DTO retornado pelo endpoint /auth/login após autenticação bem-sucedida.
    /// Contém o JWT token e informações de expiração.
    /// </summary>
    public class AuthToken
    {
        /// <summary>
        /// Token JWT (JSON Web Token) assinado.
        /// Formato: "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIi..."
        /// Cliente envia este token no header: Authorization: Bearer {token}
        /// </summary>
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// Data/hora de expiração do token (UTC).
        /// Padrão: 1 hora após emissão.
        /// </summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// ID do usuário autenticado (extraído do JWT payload).
        /// Útil para o cliente saber qual conta está logada.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Username do usuário autenticado.
        /// Útil para exibir "Bem-vindo, {username}!" no cliente.
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Tempo restante até expiração (em segundos).
        /// Útil para o cliente saber quando renovar o token.
        /// </summary>
        public int ExpiresInSeconds => (int)(ExpiresAt - DateTime.UtcNow).TotalSeconds;

        /// <summary>
        /// Verifica se o token ainda é válido (não expirou).
        /// </summary>
        public bool IsValid => DateTime.UtcNow < ExpiresAt;

        /// <summary>
        /// Retorna string legível para logs (sem expor token completo!).
        /// </summary>
        public override string ToString()
        {
            var tokenPreview = Token.Length > 20 ? Token.Substring(0, 20) + "..." : Token;
            return $"AuthToken[{UserId}]: {Username} - Expires in {ExpiresInSeconds}s - Token: {tokenPreview}";
        }
    }
}