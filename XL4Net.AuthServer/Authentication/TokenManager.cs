// XL4Net.AuthServer/Authentication/TokenManager.cs

using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using XL4Net.AuthServer.Models;

namespace XL4Net.AuthServer.Authentication
{
    /// <summary>
    /// Gerencia criação e validação de JWT tokens.
    /// Tokens são usados para autenticar jogadores no GameServer.
    /// </summary>
    public class TokenManager
    {
        private readonly string _secretKey;
        private readonly string _issuer;
        private readonly string _audience;
        private readonly int _expirationMinutes;

        /// <summary>
        /// Construtor.
        /// </summary>
        /// <param name="secretKey">Chave secreta para assinar tokens (mínimo 32 caracteres)</param>
        /// <param name="issuer">Emissor do token (ex: "XL4Net.AuthServer")</param>
        /// <param name="audience">Audiência do token (ex: "XL4Net.GameServer")</param>
        /// <param name="expirationMinutes">Validade do token em minutos (padrão: 60)</param>
        public TokenManager(string secretKey, string issuer = "XL4Net.AuthServer",
            string audience = "XL4Net.GameServer", int expirationMinutes = 60)
        {
            // Validação
            if (string.IsNullOrWhiteSpace(secretKey))
                throw new ArgumentException("Secret key cannot be empty", nameof(secretKey));

            if (secretKey.Length < 32)
                throw new ArgumentException("Secret key must be at least 32 characters", nameof(secretKey));

            _secretKey = secretKey;
            _issuer = issuer;
            _audience = audience;
            _expirationMinutes = expirationMinutes;

            Log.Information("TokenManager initialized - Issuer: {Issuer}, Audience: {Audience}, Expiration: {ExpirationMinutes}min",
                _issuer, _audience, _expirationMinutes);
        }

        /// <summary>
        /// Gera um JWT token para um usuário autenticado.
        /// </summary>
        /// <param name="userId">UUID da conta</param>
        /// <param name="username">Nome de usuário</param>
        /// <returns>AuthToken contendo JWT e informações de expiração</returns>
        public AuthToken GenerateToken(Guid userId, string username)
        {
            try
            {
                // Claims (dados do token)
                var claims = new[]
                {
                    new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),  // Subject = User ID
                    new Claim(JwtRegisteredClaimNames.UniqueName, username),     // Username
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()), // JWT ID (único)
                    new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()) // Issued At
                };

                // Chave de assinatura
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
                var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                // Tempo de expiração
                var expiresAt = DateTime.UtcNow.AddMinutes(_expirationMinutes);

                // Cria o token
                var token = new JwtSecurityToken(
                    issuer: _issuer,
                    audience: _audience,
                    claims: claims,
                    expires: expiresAt,
                    signingCredentials: credentials
                );

                // Serializa para string
                var tokenHandler = new JwtSecurityTokenHandler();
                var tokenString = tokenHandler.WriteToken(token);

                Log.Information("JWT token generated for user: {Username} ({UserId}) - Expires: {ExpiresAt:yyyy-MM-dd HH:mm:ss} UTC",
                    username, userId, expiresAt);

                return new AuthToken
                {
                    Token = tokenString,
                    ExpiresAt = expiresAt,
                    UserId = userId,
                    Username = username
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to generate JWT token for user: {Username} ({UserId})", username, userId);
                throw new InvalidOperationException("Failed to generate authentication token", ex);
            }
        }

        /// <summary>
        /// Valida um JWT token e extrai as claims.
        /// </summary>
        /// <param name="token">Token JWT a ser validado</param>
        /// <returns>ValidateTokenResponse com resultado da validação</returns>
        public ValidateTokenResponse ValidateToken(string token)
        {
            // Validação básica
            if (string.IsNullOrWhiteSpace(token))
            {
                Log.Warning("Token validation failed: empty token");
                return ValidateTokenResponse.CreateFailure("Token is empty");
            }

            // Token JWT tem 3 partes separadas por ponto
            var parts = token.Split('.');
            if (parts.Length != 3)
            {
                Log.Warning("Token validation failed: invalid format (expected 3 parts, got {PartCount})", parts.Length);
                return ValidateTokenResponse.CreateFailure("Invalid token format");
            }

            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(_secretKey);

                // Parâmetros de validação
                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _issuer,
                    ValidateAudience = true,
                    ValidAudience = _audience,
                    ValidateLifetime = true, // Verifica expiração
                    ClockSkew = TimeSpan.FromMinutes(5) // Tolera 5min de diferença de relógio
                };

                // Valida o token
                var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);

                // Extrai claims
                var userIdClaim = principal.FindFirst(JwtRegisteredClaimNames.Sub);
                var usernameClaim = principal.FindFirst(JwtRegisteredClaimNames.UniqueName);

                if (userIdClaim == null || usernameClaim == null)
                {
                    Log.Warning("Token validation failed: missing required claims");
                    return ValidateTokenResponse.CreateFailure("Token missing required claims");
                }

                if (!Guid.TryParse(userIdClaim.Value, out var userId))
                {
                    Log.Warning("Token validation failed: invalid user ID format");
                    return ValidateTokenResponse.CreateFailure("Invalid user ID in token");
                }

                // Token válido!
                var jwtToken = (JwtSecurityToken)validatedToken;
                var expiresAt = jwtToken.ValidTo;

                Log.Information("Token validated successfully: {Username} ({UserId}) - Expires: {ExpiresAt:yyyy-MM-dd HH:mm:ss} UTC",
                    usernameClaim.Value, userId, expiresAt);

                return ValidateTokenResponse.CreateSuccess(userId, usernameClaim.Value, expiresAt);
            }
            catch (SecurityTokenExpiredException ex)
            {
                Log.Warning(ex, "Token validation failed: token expired");
                return ValidateTokenResponse.CreateFailure("Token expired");
            }
            catch (SecurityTokenInvalidSignatureException ex)
            {
                Log.Warning(ex, "Token validation failed: invalid signature");
                return ValidateTokenResponse.CreateFailure("Invalid token signature");
            }
            catch (SecurityTokenException ex)
            {
                Log.Warning(ex, "Token validation failed: security token exception");
                return ValidateTokenResponse.CreateFailure($"Invalid token: {ex.Message}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error during token validation");
                return ValidateTokenResponse.CreateFailure("Token validation error");
            }
        }

        /// <summary>
        /// Extrai o User ID de um token SEM validar (útil para logs).
        /// ATENÇÃO: Não use isso para autenticação! Use ValidateToken().
        /// </summary>
        /// <param name="token">Token JWT</param>
        /// <returns>User ID se conseguir extrair, null caso contrário</returns>
        public Guid? ExtractUserIdUnsafe(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var jwtToken = tokenHandler.ReadJwtToken(token);

                var userIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub);
                if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
                {
                    return userId;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Verifica se um token está expirado (sem validar assinatura).
        /// </summary>
        /// <param name="token">Token JWT</param>
        /// <returns>True se expirado, False se válido, null se erro</returns>
        public bool? IsTokenExpired(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var jwtToken = tokenHandler.ReadJwtToken(token);

                return jwtToken.ValidTo < DateTime.UtcNow;
            }
            catch
            {
                return null;
            }
        }
    }
}