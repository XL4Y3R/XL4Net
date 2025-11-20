// XL4Net.AuthServer/Models/LoginAttempt.cs

using System;
using System.Net;

namespace XL4Net.AuthServer.Models
{
    /// <summary>
    /// Representa uma tentativa de login (sucesso ou falha).
    /// Usado para rate limiting e auditoria de segurança.
    /// Mapeada para a tabela 'login_attempts' no PostgreSQL.
    /// </summary>
    public class LoginAttempt
    {
        /// <summary>
        /// ID único da tentativa (UUID v4).
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// ID da conta (se existir).
        /// NULL = username não existe no banco.
        /// </summary>
        public Guid? AccountId { get; set; }

        /// <summary>
        /// Endereço IP de onde veio a tentativa.
        /// Tipo INET no PostgreSQL (suporta IPv4 e IPv6).
        /// </summary>
        public string IpAddress { get; set; } = string.Empty;

        /// <summary>
        /// Username que foi tentado.
        /// Armazenado mesmo que não exista (para auditoria).
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// TRUE = login bem-sucedido
        /// FALSE = login falhou (senha errada, username não existe, etc)
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Timestamp da tentativa.
        /// Preenchido automaticamente pelo PostgreSQL (NOW()).
        /// </summary>
        public DateTime AttemptedAt { get; set; }

        /// <summary>
        /// Valida se o IP é válido.
        /// </summary>
        public bool HasValidIp()
        {
            if (string.IsNullOrWhiteSpace(IpAddress))
                return false;

            return IPAddress.TryParse(IpAddress, out _);
        }

        /// <summary>
        /// Retorna string legível para logs.
        /// </summary>
        public override string ToString()
        {
            var status = Success ? "SUCCESS" : "FAILED";
            return $"LoginAttempt[{status}]: {Username} from {IpAddress} at {AttemptedAt:yyyy-MM-dd HH:mm:ss}";
        }
    }
}