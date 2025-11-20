// XL4Net.AuthServer/Models/RegisterRequest.cs

using System.ComponentModel.DataAnnotations;

namespace XL4Net.AuthServer.Models
{
    /// <summary>
    /// Request DTO para endpoint /auth/register.
    /// Dados enviados pelo cliente para criar uma nova conta.
    /// </summary>
    public class RegisterRequest
    {
        /// <summary>
        /// Nome de usuário desejado (único).
        /// Regras: 3-50 caracteres, alfanumérico + underscore.
        /// </summary>
        [Required(ErrorMessage = "Username is required")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be 3-50 characters")]
        [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Username can only contain letters, numbers and underscore")]
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Email do usuário (único).
        /// Validado via regex básico.
        /// </summary>
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [StringLength(255, ErrorMessage = "Email too long")]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Senha em plain text (será hashada com BCrypt).
        /// IMPORTANTE: HTTPS obrigatório em produção!
        /// Regras: mínimo 8 caracteres.
        /// </summary>
        [Required(ErrorMessage = "Password is required")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters")]
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Confirmação da senha (deve ser igual).
        /// Validado antes de enviar ao servidor.
        /// </summary>
        [Compare("Password", ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; } = string.Empty;

        /// <summary>
        /// Valida se o request está em formato correto.
        /// </summary>
        public bool IsValid(out string error)
        {
            error = string.Empty;

            // Username
            if (string.IsNullOrWhiteSpace(Username) || Username.Length < 3 || Username.Length > 50)
            {
                error = "Username must be 3-50 characters";
                return false;
            }

            // Email básico
            if (string.IsNullOrWhiteSpace(Email) || !Email.Contains("@"))
            {
                error = "Invalid email format";
                return false;
            }

            // Senha
            if (string.IsNullOrWhiteSpace(Password) || Password.Length < 8)
            {
                error = "Password must be at least 8 characters";
                return false;
            }

            // Confirmação
            if (Password != ConfirmPassword)
            {
                error = "Passwords do not match";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Retorna string legível para logs (SEM SENHA!).
        /// </summary>
        public override string ToString()
        {
            return $"RegisterRequest: {Username} ({Email})";
        }
    }
}