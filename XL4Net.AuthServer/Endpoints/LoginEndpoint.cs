// XL4Net.AuthServer/Endpoints/LoginEndpoint.cs

using System;
using System.Threading.Tasks;
using Serilog;
using XL4Net.AuthServer.Authentication;
using XL4Net.AuthServer.Database;
using XL4Net.AuthServer.Models;

namespace XL4Net.AuthServer.Endpoints
{
    /// <summary>
    /// Endpoint: POST /auth/login
    /// Autentica usuário e retorna JWT token.
    /// </summary>
    public class LoginEndpoint
    {
        private readonly IAccountRepository _repository;
        private readonly TokenManager _tokenManager;
        private readonly RateLimiter _rateLimiter;

        /// <summary>
        /// Construtor.
        /// </summary>
        /// <param name="repository">Repositório de contas</param>
        /// <param name="tokenManager">Gerenciador de JWT tokens</param>
        /// <param name="rateLimiter">Rate limiter</param>
        public LoginEndpoint(IAccountRepository repository, TokenManager tokenManager, RateLimiter rateLimiter)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _tokenManager = tokenManager ?? throw new ArgumentNullException(nameof(tokenManager));
            _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
        }

        /// <summary>
        /// Processa requisição de login.
        /// </summary>
        /// <param name="request">Dados de login (username/email + password)</param>
        /// <returns>LoginResponse com token JWT ou erro</returns>
        public async Task<LoginResponse> HandleAsync(LoginRequest request)
        {
            // 1. Validação de input
            if (request == null)
            {
                Log.Warning("Login attempt failed: null request");
                return LoginResponse.CreateFailure("Invalid request");
            }

            if (!request.IsValid(out var validationError))
            {
                Log.Warning("Login attempt failed: validation error - {ValidationError}", validationError);
                return LoginResponse.CreateFailure(validationError);
            }

            // IP address é necessário para rate limiting
            if (string.IsNullOrWhiteSpace(request.IpAddress))
            {
                Log.Warning("Login attempt failed: missing IP address - {UsernameOrEmail}", request.UsernameOrEmail);
                return LoginResponse.CreateFailure("IP address required");
            }

            try
            {
                // 2. Rate limiting check
                var rateLimit = await _rateLimiter.CheckAsync(request.IpAddress);
                if (rateLimit.IsLimited)
                {
                    var message = _rateLimiter.GetUserMessage(rateLimit);
                    Log.Warning("Login attempt blocked: rate limit exceeded - {UsernameOrEmail} from {IpAddress} - {Message}",
                        request.UsernameOrEmail, request.IpAddress, message);

                    return LoginResponse.CreateRateLimited(message, rateLimit.RetryAfterSeconds);
                }

                // 3. Busca conta (por username ou email)
                Account? account;
                if (request.IsEmailLogin)
                {
                    account = await _repository.GetAccountByEmailAsync(request.UsernameOrEmail);
                }
                else
                {
                    account = await _repository.GetAccountByUsernameAsync(request.UsernameOrEmail);
                }

                // Conta não existe
                if (account == null)
                {
                    Log.Warning("Login attempt failed: account not found - {UsernameOrEmail} from {IpAddress}",
                        request.UsernameOrEmail, request.IpAddress);

                    // Registra tentativa falha (accountId = null)
                    await _rateLimiter.RecordFailureAsync(request.IpAddress, request.UsernameOrEmail, accountId: null);

                    // Mensagem genérica (não revela se username existe)
                    return LoginResponse.CreateFailure("Invalid username or password");
                }

                // 4. Verifica senha (BCrypt)
                bool isPasswordValid;
                try
                {
                    isPasswordValid = PasswordHasher.Verify(request.Password, account.PasswordHash);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to verify password during login: {Username}", account.Username);

                    // Registra tentativa falha
                    await _rateLimiter.RecordFailureAsync(request.IpAddress, account.Username, account.Id);

                    return LoginResponse.CreateFailure("Authentication error");
                }

                // Senha incorreta
                if (!isPasswordValid)
                {
                    Log.Warning("Login attempt failed: incorrect password - {Username} from {IpAddress}",
                        account.Username, request.IpAddress);

                    // Registra tentativa falha
                    await _rateLimiter.RecordFailureAsync(request.IpAddress, account.Username, account.Id);

                    // Mensagem genérica (não revela que username existe)
                    return LoginResponse.CreateFailure("Invalid username or password");
                }

                // 5. Gera JWT token
                AuthToken authToken;
                try
                {
                    authToken = _tokenManager.GenerateToken(account.Id, account.Username);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to generate token during login: {Username}", account.Username);

                    // Registra tentativa falha
                    await _rateLimiter.RecordFailureAsync(request.IpAddress, account.Username, account.Id);

                    return LoginResponse.CreateFailure("Failed to generate authentication token");
                }

                // 6. Atualiza last_login no banco
                await _repository.UpdateLastLoginAsync(account.Id);

                // 7. Registra tentativa bem-sucedida
                await _rateLimiter.RecordSuccessAsync(request.IpAddress, account.Id, account.Username);

                // 8. Sucesso!
                Log.Information("Login successful: {Username} ({AccountId}) from {IpAddress} - Token expires: {ExpiresAt:yyyy-MM-dd HH:mm:ss} UTC",
                    account.Username, account.Id, request.IpAddress, authToken.ExpiresAt);

                return LoginResponse.CreateSuccess(authToken);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error during login: {UsernameOrEmail} from {IpAddress}",
                    request.UsernameOrEmail, request.IpAddress);

                return LoginResponse.CreateFailure("Internal server error");
            }
        }
    }

    /// <summary>
    /// Response DTO para LoginEndpoint.
    /// </summary>
    public class LoginResponse
    {
        /// <summary>
        /// TRUE = login bem-sucedido (token gerado)
        /// FALSE = falha (veja ErrorMessage)
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Token JWT (se sucesso).
        /// Cliente deve enviar este token no header: Authorization: Bearer {token}
        /// </summary>
        public string? Token { get; set; }

        /// <summary>
        /// Data/hora de expiração do token (UTC).
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// User ID (se sucesso).
        /// </summary>
        public Guid? UserId { get; set; }

        /// <summary>
        /// Username (se sucesso).
        /// </summary>
        public string? Username { get; set; }

        /// <summary>
        /// Mensagem de erro (se falha).
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// TRUE se bloqueado por rate limiting.
        /// </summary>
        public bool IsRateLimited { get; set; }

        /// <summary>
        /// Segundos até poder tentar novamente (se rate limited).
        /// </summary>
        public int? RetryAfterSeconds { get; set; }

        /// <summary>
        /// Cria resposta de sucesso.
        /// </summary>
        public static LoginResponse CreateSuccess(AuthToken authToken)
        {
            return new LoginResponse
            {
                Success = true,
                Token = authToken.Token,
                ExpiresAt = authToken.ExpiresAt,
                UserId = authToken.UserId,
                Username = authToken.Username,
                ErrorMessage = null,
                IsRateLimited = false,
                RetryAfterSeconds = null
            };
        }

        /// <summary>
        /// Cria resposta de falha.
        /// </summary>
        public static LoginResponse CreateFailure(string errorMessage)
        {
            return new LoginResponse
            {
                Success = false,
                Token = null,
                ExpiresAt = null,
                UserId = null,
                Username = null,
                ErrorMessage = errorMessage,
                IsRateLimited = false,
                RetryAfterSeconds = null
            };
        }

        /// <summary>
        /// Cria resposta de rate limiting.
        /// </summary>
        public static LoginResponse CreateRateLimited(string message, int retryAfterSeconds)
        {
            return new LoginResponse
            {
                Success = false,
                Token = null,
                ExpiresAt = null,
                UserId = null,
                Username = null,
                ErrorMessage = message,
                IsRateLimited = true,
                RetryAfterSeconds = retryAfterSeconds
            };
        }

        /// <summary>
        /// Retorna string legível para logs.
        /// </summary>
        public override string ToString()
        {
            if (Success)
                return $"LoginResponse[SUCCESS]: {Username} ({UserId})";
            else if (IsRateLimited)
                return $"LoginResponse[RATE_LIMITED]: {ErrorMessage} - Retry after {RetryAfterSeconds}s";
            else
                return $"LoginResponse[FAILURE]: {ErrorMessage}";
        }
    }
}