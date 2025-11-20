// XL4Net.AuthServer/Database/IAccountRepository.cs

using System;
using System.Threading.Tasks;
using XL4Net.AuthServer.Models;

namespace XL4Net.AuthServer.Database
{
    /// <summary>
    /// Interface para acesso aos dados de contas no PostgreSQL.
    /// Abstrai a implementação do banco de dados (pode trocar depois se quiser).
    /// </summary>
    public interface IAccountRepository
    {
        // ============================================
        // CONTAS (CRUD)
        // ============================================

        /// <summary>
        /// Cria uma nova conta no banco de dados.
        /// </summary>
        /// <param name="username">Nome de usuário (único)</param>
        /// <param name="email">Email (único)</param>
        /// <param name="passwordHash">Senha hashada com BCrypt</param>
        /// <returns>Account criada com Id preenchido, ou null se falhar</returns>
        Task<Account?> CreateAccountAsync(string username, string email, string passwordHash);

        /// <summary>
        /// Busca uma conta por username.
        /// </summary>
        /// <param name="username">Nome de usuário</param>
        /// <returns>Account encontrada, ou null se não existir</returns>
        Task<Account?> GetAccountByUsernameAsync(string username);

        /// <summary>
        /// Busca uma conta por email.
        /// </summary>
        /// <param name="email">Email</param>
        /// <returns>Account encontrada, ou null se não existir</returns>
        Task<Account?> GetAccountByEmailAsync(string email);

        /// <summary>
        /// Busca uma conta por ID.
        /// </summary>
        /// <param name="id">UUID da conta</param>
        /// <returns>Account encontrada, ou null se não existir</returns>
        Task<Account?> GetAccountByIdAsync(Guid id);

        /// <summary>
        /// Atualiza o timestamp de último login.
        /// </summary>
        /// <param name="accountId">UUID da conta</param>
        /// <returns>True se atualizou, False se conta não existe</returns>
        Task<bool> UpdateLastLoginAsync(Guid accountId);

        /// <summary>
        /// Atualiza os metadados da conta (JSONB).
        /// </summary>
        /// <param name="accountId">UUID da conta</param>
        /// <param name="metadata">JSON string com metadados</param>
        /// <returns>True se atualizou, False se conta não existe</returns>
        Task<bool> UpdateMetadataAsync(Guid accountId, string metadata);

        /// <summary>
        /// Verifica se um username já existe.
        /// </summary>
        /// <param name="username">Nome de usuário</param>
        /// <returns>True se existe, False se disponível</returns>
        Task<bool> UsernameExistsAsync(string username);

        /// <summary>
        /// Verifica se um email já existe.
        /// </summary>
        /// <param name="email">Email</param>
        /// <returns>True se existe, False se disponível</returns>
        Task<bool> EmailExistsAsync(string email);

        // ============================================
        // LOGIN ATTEMPTS (Auditoria)
        // ============================================

        /// <summary>
        /// Registra uma tentativa de login (sucesso ou falha).
        /// </summary>
        /// <param name="accountId">UUID da conta (null se username não existe)</param>
        /// <param name="ipAddress">IP de origem</param>
        /// <param name="username">Username tentado</param>
        /// <param name="success">True = sucesso, False = falha</param>
        /// <returns>True se registrou, False se erro</returns>
        Task<bool> RecordLoginAttemptAsync(Guid? accountId, string ipAddress, string username, bool success);

        // ============================================
        // RATE LIMITING
        // ============================================

        /// <summary>
        /// Verifica rate limiting usando função do PostgreSQL.
        /// Chama: SELECT * FROM check_rate_limit('192.168.1.100', 60, 5)
        /// </summary>
        /// <param name="ipAddress">IP a verificar</param>
        /// <param name="timeWindowMinutes">Janela de tempo (padrão: 60 minutos)</param>
        /// <param name="maxAttempts">Máximo de tentativas (padrão: 5)</param>
        /// <returns>RateLimitResult com status</returns>
        Task<RateLimitResult> CheckRateLimitAsync(string ipAddress, int timeWindowMinutes = 60, int maxAttempts = 5);

        // ============================================
        // MANUTENÇÃO
        // ============================================

        /// <summary>
        /// Limpa tentativas de login antigas (>7 dias).
        /// Chama: SELECT cleanup_old_login_attempts()
        /// </summary>
        /// <returns>True se executou, False se erro</returns>
        Task<bool> CleanupOldLoginAttemptsAsync();

        /// <summary>
        /// Testa a conexão com o banco de dados.
        /// </summary>
        /// <returns>True se conectado, False se erro</returns>
        Task<bool> TestConnectionAsync();
    }
}