// XL4Net.AuthServer/Authentication/RateLimiter.cs

using System;
using System.Threading.Tasks;
using Serilog;
using XL4Net.AuthServer.Database;
using XL4Net.AuthServer.Models;

namespace XL4Net.AuthServer.Authentication
{
    /// <summary>
    /// Gerencia rate limiting de tentativas de login.
    /// Wrapper amigável sobre IAccountRepository.CheckRateLimitAsync().
    /// 
    /// Regras padrão:
    /// - 5 tentativas por IP a cada 60 minutos
    /// - Após exceder: bloqueio temporário
    /// </summary>
    public class RateLimiter
    {
        private readonly IAccountRepository _repository;
        private readonly int _timeWindowMinutes;
        private readonly int _maxAttempts;

        /// <summary>
        /// Construtor.
        /// </summary>
        /// <param name="repository">Repositório de contas (para acessar banco)</param>
        /// <param name="timeWindowMinutes">Janela de tempo em minutos (padrão: 60)</param>
        /// <param name="maxAttempts">Máximo de tentativas na janela (padrão: 5)</param>
        public RateLimiter(IAccountRepository repository, int timeWindowMinutes = 60, int maxAttempts = 5)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _timeWindowMinutes = timeWindowMinutes;
            _maxAttempts = maxAttempts;

            Log.Information("RateLimiter initialized - Window: {TimeWindowMinutes}min, Max attempts: {MaxAttempts}",
                _timeWindowMinutes, _maxAttempts);
        }

        /// <summary>
        /// Verifica se um IP está autorizado a fazer login (não excedeu rate limit).
        /// </summary>
        /// <param name="ipAddress">Endereço IP a verificar</param>
        /// <returns>
        /// RateLimitResult com:
        /// - CanAttemptLogin = true se pode tentar
        /// - CanAttemptLogin = false se bloqueado (com tempo de espera)
        /// </returns>
        public async Task<RateLimitResult> CheckAsync(string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                Log.Warning("Rate limit check failed: empty IP address");

                // Em caso de erro, permite (fail open)
                return new RateLimitResult
                {
                    AttemptsCount = 0,
                    IsLimited = false,
                    RetryAfterSeconds = 0
                };
            }

            try
            {
                var result = await _repository.CheckRateLimitAsync(ipAddress, _timeWindowMinutes, _maxAttempts);

                if (result.IsLimited)
                {
                    Log.Warning("Rate limit exceeded for IP: {IpAddress} - {AttemptsCount}/{MaxAttempts} attempts in {TimeWindowMinutes}min - Retry after {RetryAfterSeconds}s",
                        ipAddress, result.AttemptsCount, _maxAttempts, _timeWindowMinutes, result.RetryAfterSeconds);
                }
                else
                {
                    Log.Debug("Rate limit check passed for IP: {IpAddress} - {AttemptsCount}/{MaxAttempts} attempts",
                        ipAddress, result.AttemptsCount, _maxAttempts);
                }

                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Rate limit check error for IP: {IpAddress} - Allowing login (fail open)", ipAddress);

                // Em caso de erro, permite (fail open = mais seguro que negar todos)
                return new RateLimitResult
                {
                    AttemptsCount = 0,
                    IsLimited = false,
                    RetryAfterSeconds = 0
                };
            }
        }

        /// <summary>
        /// Registra uma tentativa de login bem-sucedida.
        /// </summary>
        /// <param name="ipAddress">IP do cliente</param>
        /// <param name="accountId">ID da conta</param>
        /// <param name="username">Username</param>
        /// <returns>True se registrou, False se erro</returns>
        public async Task<bool> RecordSuccessAsync(string ipAddress, Guid accountId, string username)
        {
            try
            {
                var success = await _repository.RecordLoginAttemptAsync(accountId, ipAddress, username, success: true);

                if (success)
                {
                    Log.Information("Login attempt recorded: SUCCESS - {Username} from {IpAddress}", username, ipAddress);
                }

                return success;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to record successful login attempt: {Username} from {IpAddress}", username, ipAddress);
                return false;
            }
        }

        /// <summary>
        /// Registra uma tentativa de login falha.
        /// </summary>
        /// <param name="ipAddress">IP do cliente</param>
        /// <param name="username">Username tentado</param>
        /// <param name="accountId">ID da conta (null se username não existe)</param>
        /// <returns>True se registrou, False se erro</returns>
        public async Task<bool> RecordFailureAsync(string ipAddress, string username, Guid? accountId = null)
        {
            try
            {
                var success = await _repository.RecordLoginAttemptAsync(accountId, ipAddress, username, success: false);

                if (success)
                {
                    Log.Warning("Login attempt recorded: FAILED - {Username} from {IpAddress}", username, ipAddress);
                }

                return success;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to record failed login attempt: {Username} from {IpAddress}", username, ipAddress);
                return false;
            }
        }

        /// <summary>
        /// Retorna mensagem amigável para o usuário sobre rate limiting.
        /// </summary>
        /// <param name="result">Resultado do rate limit check</param>
        /// <returns>Mensagem em português para exibir ao jogador</returns>
        public string GetUserMessage(RateLimitResult result)
        {
            if (result.CanAttemptLogin)
            {
                return "Login permitido";
            }

            if (result.RetryAfterSeconds < 60)
            {
                return $"Muitas tentativas de login. Tente novamente em {result.RetryAfterSeconds} segundos.";
            }

            var minutes = result.RetryAfterSeconds / 60;
            if (minutes == 1)
            {
                return "Muitas tentativas de login. Tente novamente em 1 minuto.";
            }

            return $"Muitas tentativas de login. Tente novamente em {minutes} minutos.";
        }

        /// <summary>
        /// Calcula quantas tentativas ainda estão disponíveis.
        /// </summary>
        /// <param name="result">Resultado do rate limit check</param>
        /// <returns>Número de tentativas restantes (0 se bloqueado)</returns>
        public int GetRemainingAttempts(RateLimitResult result)
        {
            if (result.IsLimited)
                return 0;

            var remaining = _maxAttempts - (int)result.AttemptsCount;
            return Math.Max(0, remaining);
        }

        /// <summary>
        /// Limpa tentativas de login antigas (>7 dias) do banco.
        /// Deve ser chamado periodicamente (ex: 1x por dia via cron job).
        /// </summary>
        /// <returns>True se limpou, False se erro</returns>
        public async Task<bool> CleanupOldAttemptsAsync()
        {
            try
            {
                var success = await _repository.CleanupOldLoginAttemptsAsync();

                if (success)
                {
                    Log.Information("Old login attempts cleaned up successfully");
                }
                else
                {
                    Log.Warning("Failed to cleanup old login attempts");
                }

                return success;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during cleanup of old login attempts");
                return false;
            }
        }
    }
}