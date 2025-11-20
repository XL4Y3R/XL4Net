// XL4Net.AuthServer/Models/ValidateTokenRequest.cs

using System.ComponentModel.DataAnnotations;

namespace XL4Net.AuthServer.Models
{
    /// <summary>
    /// Request DTO para endpoint /auth/validate-token.
    /// Enviado pelo GameServer para verificar se um token JWT é válido.
    /// </summary>
    public class ValidateTokenRequest
    {
        /// <summary>
        /// Token JWT a ser validado.
        /// Formato: "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
        /// </summary>
        [Required(ErrorMessage = "Token is required")]
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// Valida se o request está em formato correto.
        /// </summary>
        public bool IsValid(out string error)
        {
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(Token))
            {
                error = "Token is required";
                return false;
            }

            // Token JWT tem 3 partes separadas por ponto
            var parts = Token.Split('.');
            if (parts.Length != 3)
            {
                error = "Invalid token format (must have 3 parts)";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Retorna string legível para logs (sem expor token completo!).
        /// </summary>
        public override string ToString()
        {
            var tokenPreview = Token.Length > 20 ? Token.Substring(0, 20) + "..." : Token;
            return $"ValidateTokenRequest: Token={tokenPreview}";
        }
    }
}