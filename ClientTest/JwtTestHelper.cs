// tests/ClientTest/JwtTestHelper.cs

using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace ClientTest
{
    /// <summary>
    /// Helper para gerar tokens JWT para testes.
    /// APENAS PARA TESTES - Em produção o AuthServer gera os tokens.
    /// </summary>
    public static class JwtTestHelper
    {
        /// <summary>
        /// Gera um token JWT válido para teste.
        /// </summary>
        /// <param name="userId">ID do usuário</param>
        /// <param name="username">Nome do usuário</param>
        /// <param name="secret">Chave secreta (DEVE ser igual à do GameServer)</param>
        /// <param name="issuer">Issuer (DEVE ser igual à do GameServer)</param>
        /// <param name="expiresInMinutes">Tempo de expiração</param>
        /// <returns>Token JWT assinado</returns>
        public static string GenerateToken(
            Guid userId,
            string username,
            string secret,
            string issuer = "XL4Net.AuthServer",
            int expiresInMinutes = 60)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim("sub", userId.ToString()),      // Subject (UserId)
                new Claim("name", username),              // Username
                new Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()) // Issued At
            };

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: null,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiresInMinutes),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <summary>
        /// Gera token com valores padrão para teste rápido.
        /// </summary>
        public static string GenerateTestToken()
        {
            return GenerateToken(
                userId: Guid.NewGuid(),
                username: "TestPlayer",
                secret: "XL4Net_JWT_Secret_Key_Change_In_Production_2024"
            );
        }

        /// <summary>
        /// Gera token expirado (para testar rejeição).
        /// </summary>
        public static string GenerateExpiredToken()
        {
            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes("XL4Net_JWT_Secret_Key_Change_In_Production_2024"));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: "XL4Net.AuthServer",
                claims: new[]
                {
                    new Claim("sub", Guid.NewGuid().ToString()),
                    new Claim("name", "ExpiredUser")
                },
                expires: DateTime.UtcNow.AddMinutes(-10), // Expirou 10 minutos atrás
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <summary>
        /// Gera token com assinatura inválida (para testar rejeição).
        /// </summary>
        public static string GenerateInvalidSignatureToken()
        {
            return GenerateToken(
                userId: Guid.NewGuid(),
                username: "HackerUser",
                secret: "WRONG_SECRET_KEY_12345678901234567890" // Chave diferente!
            );
        }
    }
}