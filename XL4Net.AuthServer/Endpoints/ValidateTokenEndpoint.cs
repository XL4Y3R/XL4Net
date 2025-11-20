// XL4Net.AuthServer/Endpoints/ValidateTokenEndpoint.cs

using System;
using System.Threading.Tasks;
using Serilog;
using XL4Net.AuthServer.Authentication;
using XL4Net.AuthServer.Models;

namespace XL4Net.AuthServer.Endpoints
{
    /// <summary>
    /// Endpoint: POST /auth/validate-token
    /// Valida um JWT token (usado pelo GameServer).
    /// </summary>
    public class ValidateTokenEndpoint
    {
        private readonly TokenManager _tokenManager;

        /// <summary>
        /// Construtor.
        /// </summary>
        /// <param name="tokenManager">Gerenciador de JWT tokens</param>
        public ValidateTokenEndpoint(TokenManager tokenManager)
        {
            _tokenManager = tokenManager ?? throw new ArgumentNullException(nameof(tokenManager));
        }

        /// <summary>
        /// Processa requisição de validação de token.
        /// </summary>
        /// <param name="request">Token JWT a ser validado</param>
        /// <returns>ValidateTokenResponse com resultado</returns>
        public async Task<ValidateTokenResponse> HandleAsync(ValidateTokenRequest request)
        {
            // 1. Validação de input
            if (request == null)
            {
                Log.Warning("Token validation failed: null request");
                return ValidateTokenResponse.CreateFailure("Invalid request");
            }

            if (!request.IsValid(out var validationError))
            {
                Log.Warning("Token validation failed: validation error - {ValidationError}", validationError);
                return ValidateTokenResponse.CreateFailure(validationError);
            }

            try
            {
                // 2. Valida token (verifica assinatura, expiração, etc)
                var result = _tokenManager.ValidateToken(request.Token);

                // Log do resultado
                if (result.IsValid)
                {
                    Log.Information("Token validated successfully: {Username} ({UserId}) - Expires: {ExpiresAt:yyyy-MM-dd HH:mm:ss} UTC",
                        result.Username, result.UserId, result.ExpiresAt);
                }
                else
                {
                    Log.Warning("Token validation failed: {ErrorMessage}", result.ErrorMessage);
                }

                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error during token validation");
                return ValidateTokenResponse.CreateFailure("Internal server error");
            }

            // Nota: Este método é async para consistência com outros endpoints,
            // mas a validação de token é síncrona (não precisa acessar banco).
            // O await está aqui para evitar warning do compilador.
            await Task.CompletedTask;
        }

        /// <summary>
        /// Versão síncrona do HandleAsync (útil para testes).
        /// </summary>
        /// <param name="request">Token JWT a ser validado</param>
        /// <returns>ValidateTokenResponse com resultado</returns>
        public ValidateTokenResponse Handle(ValidateTokenRequest request)
        {
            // Chama versão async e aguarda (sync over async - não ideal, mas OK para este caso)
            return HandleAsync(request).GetAwaiter().GetResult();
        }
    }
}