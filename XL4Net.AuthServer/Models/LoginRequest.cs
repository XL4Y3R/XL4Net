// XL4Net.AuthServer/Models/LoginRequest.cs

using System.ComponentModel.DataAnnotations;

namespace XL4Net.AuthServer.Models
{
    /// <summary>
    /// Request DTO para endpoint /auth/login.
    /// Dados enviados pelo cliente para autenticação.
    /// </summary>
    public class LoginRequest
    {
        /// <summary>
        /// Nome de usuário ou email.
        /// Suporta login por username OU email.
        /// </summary>
        [Required(ErrorMessage = "Username or email is required")]
        public string UsernameOrEmail { get; set; } = string.Empty;

        /// <summary>
        /// Senha em plain text.
        /// Comparada com BCrypt hash do banco.
        /// IMPORTANTE: HTTPS obrigatório em produção!
        /// </summary>
        [Required(ErrorMessage = "Password is required")]
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// IP do cliente (preenchido pelo servidor, não pelo cliente).
        /// Usado para rate limiting e auditoria.
        /// </summary>
        public string? IpAddress { get; set; }

        /// <summary>
        /// Valida se o request está em formato correto.
        /// </summary>
        public bool IsValid(out string error)
        {
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(UsernameOrEmail))
            {
                error = "Username or email is required";
                return false;
            }

            if (string.IsNullOrWhiteSpace(Password))
            {
                error = "Password is required";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Verifica se está tentando login por email (contém @).
        /// </summary>
        public bool IsEmailLogin => UsernameOrEmail.Contains("@");

        /// <summary>
        /// Retorna string legível para logs (SEM SENHA!).
        /// </summary>
        public override string ToString()
        {
            var loginType = IsEmailLogin ? "email" : "username";
            return $"LoginRequest: {UsernameOrEmail} ({loginType}) from {IpAddress ?? "unknown"}";
        }
    }
}