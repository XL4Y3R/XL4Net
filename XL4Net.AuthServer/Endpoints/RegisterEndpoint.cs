// XL4Net.AuthServer/Endpoints/RegisterEndpoint.cs

using System;
using System.Threading.Tasks;
using Serilog;
using XL4Net.AuthServer.Authentication;
using XL4Net.AuthServer.Database;
using XL4Net.AuthServer.Models;

namespace XL4Net.AuthServer.Endpoints
{
    /// <summary>
    /// Endpoint: POST /auth/register
    /// Cria uma nova conta de usuário.
    /// </summary>
    public class RegisterEndpoint
    {
        private readonly IAccountRepository _repository;

        /// <summary>
        /// Construtor.
        /// </summary>
        /// <param name="repository">Repositório de contas</param>
        public RegisterEndpoint(IAccountRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        /// <summary>
        /// Processa requisição de registro.
        /// </summary>
        /// <param name="request">Dados de registro (username, email, password)</param>
        /// <returns>RegisterResponse com resultado</returns>
        public async Task<RegisterResponse> HandleAsync(RegisterRequest request)
        {
            // 1. Validação de input
            if (request == null)
            {
                Log.Warning("Register attempt failed: null request");
                return RegisterResponse.CreateFailure("Invalid request");
            }

            if (!request.IsValid(out var validationError))
            {
                Log.Warning("Register attempt failed: validation error - {ValidationError}", validationError);
                return RegisterResponse.CreateFailure(validationError);
            }

            try
            {
                // 2. Verifica se username já existe
                var usernameExists = await _repository.UsernameExistsAsync(request.Username);
                if (usernameExists)
                {
                    Log.Warning("Register attempt failed: username already exists - {Username}", request.Username);
                    return RegisterResponse.CreateFailure("Username already taken");
                }

                // 3. Verifica se email já existe
                var emailExists = await _repository.EmailExistsAsync(request.Email);
                if (emailExists)
                {
                    Log.Warning("Register attempt failed: email already exists - {Email}", request.Email);
                    return RegisterResponse.CreateFailure("Email already registered");
                }

                // 4. Hash da senha (BCrypt cost 12 = ~300ms)
                string passwordHash;
                try
                {
                    passwordHash = PasswordHasher.Hash(request.Password);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to hash password during registration: {Username}", request.Username);
                    return RegisterResponse.CreateFailure("Failed to process password");
                }

                // 5. Cria conta no banco
                var account = await _repository.CreateAccountAsync(
                    request.Username,
                    request.Email,
                    passwordHash
                );

                if (account == null)
                {
                    Log.Error("Failed to create account in database: {Username}", request.Username);
                    return RegisterResponse.CreateFailure("Failed to create account");
                }

                // 6. Sucesso!
                Log.Information("Account created successfully: {Username} ({AccountId}) - Email: {Email}",
                    account.Username, account.Id, account.Email);

                return RegisterResponse.CreateSuccess(account.Id, account.Username);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error during registration: {Username}", request.Username);
                return RegisterResponse.CreateFailure("Internal server error");
            }
        }
    }

    /// <summary>
    /// Response DTO para RegisterEndpoint.
    /// </summary>
    public class RegisterResponse
    {
        /// <summary>
        /// TRUE = conta criada com sucesso
        /// FALSE = falha (veja ErrorMessage)
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// ID da conta criada (se sucesso).
        /// </summary>
        public Guid? AccountId { get; set; }

        /// <summary>
        /// Username da conta criada (se sucesso).
        /// </summary>
        public string? Username { get; set; }

        /// <summary>
        /// Mensagem de erro (se falha).
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Cria resposta de sucesso.
        /// </summary>
        public static RegisterResponse CreateSuccess(Guid accountId, string username)
        {
            return new RegisterResponse
            {
                Success = true,
                AccountId = accountId,
                Username = username,
                ErrorMessage = null
            };
        }

        /// <summary>
        /// Cria resposta de falha.
        /// </summary>
        public static RegisterResponse CreateFailure(string errorMessage)
        {
            return new RegisterResponse
            {
                Success = false,
                AccountId = null,
                Username = null,
                ErrorMessage = errorMessage
            };
        }

        /// <summary>
        /// Retorna string legível para logs.
        /// </summary>
        public override string ToString()
        {
            if (Success)
                return $"RegisterResponse[SUCCESS]: {Username} ({AccountId})";
            else
                return $"RegisterResponse[FAILURE]: {ErrorMessage}";
        }
    }
}

