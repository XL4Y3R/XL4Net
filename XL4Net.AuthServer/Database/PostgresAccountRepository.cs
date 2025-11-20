// XL4Net.AuthServer/Database/PostgresAccountRepository.cs

using System;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using Npgsql;
using Serilog;
using XL4Net.AuthServer.Models;

namespace XL4Net.AuthServer.Database
{
    /// <summary>
    /// Implementação do repositório usando PostgreSQL + Dapper.
    /// Gerencia conexão com o banco e executa queries SQL.
    /// </summary>
    public class PostgresAccountRepository : IAccountRepository
    {
        private readonly string _connectionString;

        /// <summary>
        /// Construtor.
        /// </summary>
        /// <param name="connectionString">
        /// Connection string do PostgreSQL.
        /// Formato: "Host=localhost;Port=5432;Database=xl4net;Username=xl4admin;Password=changeme"
        /// </param>
        public PostgresAccountRepository(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        // ============================================
        // HELPER: Criar conexão
        // ============================================

        /// <summary>
        /// Cria uma nova conexão com o PostgreSQL.
        /// IMPORTANTE: Caller deve fazer Dispose() da conexão!
        /// </summary>
        private IDbConnection CreateConnection()
        {
            return new NpgsqlConnection(_connectionString);
        }

        // ============================================
        // CONTAS (CRUD)
        // ============================================

        public async Task<Account?> CreateAccountAsync(string username, string email, string passwordHash)
        {
            try
            {
                using var conn = CreateConnection();

                // SQL com RETURNING * (retorna o registro inserido)
                const string sql = @"
                    INSERT INTO accounts (username, email, password_hash, metadata, created_at)
                    VALUES (@Username, @Email, @PasswordHash, '{}'::JSONB, NOW())
                    RETURNING id, username, email, password_hash, metadata, created_at, last_login";

                var account = await conn.QueryFirstOrDefaultAsync<Account>(sql, new
                {
                    Username = username,
                    Email = email,
                    PasswordHash = passwordHash
                });

                if (account != null)
                {
                    Log.Information("Account created: {Username} (ID: {AccountId})", username, account.Id);
                }

                return account;
            }
            catch (PostgresException ex) when (ex.SqlState == "23505") // Unique violation
            {
                // Username ou email já existe
                Log.Warning("Account creation failed: duplicate username or email - {Username}", username);
                return null;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to create account: {Username}", username);
                return null;
            }
        }

        public async Task<Account?> GetAccountByUsernameAsync(string username)
        {
            try
            {
                using var conn = CreateConnection();

                const string sql = @"
                    SELECT id, username, email, password_hash, metadata, created_at, last_login
                    FROM accounts
                    WHERE username = @Username";

                var account = await conn.QueryFirstOrDefaultAsync<Account>(sql, new { Username = username });

                return account;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to get account by username: {Username}", username);
                return null;
            }
        }

        public async Task<Account?> GetAccountByEmailAsync(string email)
        {
            try
            {
                using var conn = CreateConnection();

                const string sql = @"
                    SELECT id, username, email, password_hash, metadata, created_at, last_login
                    FROM accounts
                    WHERE email = @Email";

                var account = await conn.QueryFirstOrDefaultAsync<Account>(sql, new { Email = email });

                return account;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to get account by email: {Email}", email);
                return null;
            }
        }

        public async Task<Account?> GetAccountByIdAsync(Guid id)
        {
            try
            {
                using var conn = CreateConnection();

                const string sql = @"
                    SELECT id, username, email, password_hash, metadata, created_at, last_login
                    FROM accounts
                    WHERE id = @Id";

                var account = await conn.QueryFirstOrDefaultAsync<Account>(sql, new { Id = id });

                return account;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to get account by ID: {AccountId}", id);
                return null;
            }
        }

        public async Task<bool> UpdateLastLoginAsync(Guid accountId)
        {
            try
            {
                using var conn = CreateConnection();

                const string sql = @"
                    UPDATE accounts
                    SET last_login = NOW()
                    WHERE id = @AccountId";

                var rowsAffected = await conn.ExecuteAsync(sql, new { AccountId = accountId });

                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to update last login: {AccountId}", accountId);
                return false;
            }
        }

        public async Task<bool> UpdateMetadataAsync(Guid accountId, string metadata)
        {
            try
            {
                using var conn = CreateConnection();

                const string sql = @"
                    UPDATE accounts
                    SET metadata = @Metadata::JSONB
                    WHERE id = @AccountId";

                var rowsAffected = await conn.ExecuteAsync(sql, new
                {
                    AccountId = accountId,
                    Metadata = metadata
                });

                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to update metadata: {AccountId}", accountId);
                return false;
            }
        }

        public async Task<bool> UsernameExistsAsync(string username)
        {
            try
            {
                using var conn = CreateConnection();

                const string sql = @"
                    SELECT EXISTS(SELECT 1 FROM accounts WHERE username = @Username)";

                var exists = await conn.QueryFirstAsync<bool>(sql, new { Username = username });

                return exists;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to check username existence: {Username}", username);
                return false;
            }
        }

        public async Task<bool> EmailExistsAsync(string email)
        {
            try
            {
                using var conn = CreateConnection();

                const string sql = @"
                    SELECT EXISTS(SELECT 1 FROM accounts WHERE email = @Email)";

                var exists = await conn.QueryFirstAsync<bool>(sql, new { Email = email });

                return exists;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to check email existence: {Email}", email);
                return false;
            }
        }

        // ============================================
        // LOGIN ATTEMPTS (Auditoria)
        // ============================================

        public async Task<bool> RecordLoginAttemptAsync(Guid? accountId, string ipAddress, string username, bool success)
        {
            try
            {
                using var conn = CreateConnection();

                const string sql = @"
                    INSERT INTO login_attempts (account_id, ip_address, username, success, attempted_at)
                    VALUES (@AccountId, @IpAddress::INET, @Username, @Success, NOW())";

                var rowsAffected = await conn.ExecuteAsync(sql, new
                {
                    AccountId = accountId,
                    IpAddress = ipAddress,
                    Username = username,
                    Success = success
                });

                if (rowsAffected > 0)
                {
                    var status = success ? "SUCCESS" : "FAILED";
                    Log.Information("Login attempt recorded: {Status} - {Username} from {IpAddress}",
                        status, username, ipAddress);
                }

                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to record login attempt: {Username} from {IpAddress}", username, ipAddress);
                return false;
            }
        }

        // ============================================
        // RATE LIMITING
        // ============================================

        public async Task<RateLimitResult> CheckRateLimitAsync(string ipAddress, int timeWindowMinutes = 60, int maxAttempts = 5)
        {
            try
            {
                using var conn = CreateConnection();

                // Chama função do PostgreSQL
                const string sql = @"
                    SELECT 
                        attempts_count AS AttemptsCount,
                        is_limited AS IsLimited,
                        retry_after_seconds AS RetryAfterSeconds
                    FROM check_rate_limit(@IpAddress::INET, @TimeWindowMinutes, @MaxAttempts)";

                var result = await conn.QueryFirstOrDefaultAsync<RateLimitResult>(sql, new
                {
                    IpAddress = ipAddress,
                    TimeWindowMinutes = timeWindowMinutes,
                    MaxAttempts = maxAttempts
                });

                if (result != null && result.IsLimited)
                {
                    Log.Warning("Rate limit exceeded for IP: {IpAddress} - {AttemptsCount} attempts",
                        ipAddress, result.AttemptsCount);
                }

                return result ?? new RateLimitResult { IsLimited = false, AttemptsCount = 0, RetryAfterSeconds = 0 };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to check rate limit for IP: {IpAddress}", ipAddress);

                // Em caso de erro, permite o login (fail open)
                return new RateLimitResult { IsLimited = false, AttemptsCount = 0, RetryAfterSeconds = 0 };
            }
        }

        // ============================================
        // MANUTENÇÃO
        // ============================================

        public async Task<bool> CleanupOldLoginAttemptsAsync()
        {
            try
            {
                using var conn = CreateConnection();

                // Chama função do PostgreSQL
                const string sql = "SELECT cleanup_old_login_attempts()";

                await conn.ExecuteAsync(sql);

                Log.Information("Old login attempts cleaned up successfully");

                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to cleanup old login attempts");
                return false;
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                using var conn = CreateConnection();

                // Query simples pra testar conexão
                const string sql = "SELECT 1";

                await conn.ExecuteScalarAsync<int>(sql);

                Log.Information("Database connection test successful");

                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Database connection test failed");
                return false;
            }
        }
    }
}