// XL4Net.AuthServer/Models/Account.cs

using System;

namespace XL4Net.AuthServer.Models
{
    /// <summary>
    /// Representa uma conta de usuário no banco de dados.
    /// Mapeada para a tabela 'accounts' no PostgreSQL.
    /// </summary>
    public class Account
    {
        /// <summary>
        /// ID único da conta (UUID v4).
        /// Gerado automaticamente pelo PostgreSQL via gen_random_uuid().
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Nome de usuário único (3-50 caracteres).
        /// Usado para login.
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Email único do usuário.
        /// Formato validado pelo PostgreSQL (constraint).
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Senha hashada com BCrypt (cost factor 12).
        /// NUNCA armazena senha em plain text!
        /// Formato: "$2a$12$..."
        /// </summary>
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>
        /// Metadados flexíveis em JSON (JSONB no PostgreSQL).
        /// Exemplos: level, premium status, last character played, etc.
        /// </summary>
        public string Metadata { get; set; } = "{}";

        /// <summary>
        /// Data/hora de criação da conta.
        /// Preenchido automaticamente pelo PostgreSQL (NOW()).
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Data/hora do último login bem-sucedido.
        /// Atualizado pelo LoginEndpoint.
        /// </summary>
        public DateTime? LastLogin { get; set; }

        /// <summary>
        /// Valida se a conta está em formato correto.
        /// </summary>
        public bool IsValid()
        {
            // Username: 3-50 chars
            if (string.IsNullOrWhiteSpace(Username) ||
                Username.Length < 3 ||
                Username.Length > 50)
            {
                return false;
            }

            // Email: formato básico
            if (string.IsNullOrWhiteSpace(Email) ||
                !Email.Contains("@") ||
                !Email.Contains("."))
            {
                return false;
            }

            // PasswordHash: deve existir (BCrypt hash tem ~60 chars)
            if (string.IsNullOrWhiteSpace(PasswordHash) ||
                PasswordHash.Length < 50)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Retorna string legível para logs (sem senha!).
        /// </summary>
        public override string ToString()
        {
            return $"Account[{Id}]: {Username} ({Email}) - Created: {CreatedAt:yyyy-MM-dd}";
        }
    }
}