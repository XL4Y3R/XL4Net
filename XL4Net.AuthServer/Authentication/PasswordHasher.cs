// XL4Net.AuthServer/Authentication/PasswordHasher.cs

using System;
using BCrypt.Net;
using Serilog;

namespace XL4Net.AuthServer.Authentication
{
    /// <summary>
    /// Gerencia hash e verificação de senhas usando BCrypt.
    /// BCrypt é LENTO de propósito (cost factor) para dificultar brute force.
    /// </summary>
    public static class PasswordHasher
    {
        /// <summary>
        /// Cost factor do BCrypt (quantas rodadas do algoritmo).
        /// 12 = ~300ms de hash (bom balanço segurança/performance).
        /// 
        /// Valores:
        /// - 10 = ~100ms (rápido, menos seguro)
        /// - 12 = ~300ms (recomendado)
        /// - 14 = ~1000ms (muito seguro, mas lento)
        /// </summary>
        private const int BCRYPT_COST_FACTOR = 12;

        /// <summary>
        /// Gera hash BCrypt de uma senha.
        /// Usado ao REGISTRAR nova conta.
        /// </summary>
        /// <param name="password">Senha em plain text (8+ caracteres)</param>
        /// <returns>
        /// Hash BCrypt (60 caracteres).
        /// Formato: "$2a$12$N0DF5K5wZ8XZELAVzSQ9V.qHHkv/BPQl1w4K3F2lW4K3F2lW4K3F2"
        /// </returns>
        /// <exception cref="ArgumentException">Se senha inválida</exception>
        public static string Hash(string password)
        {
            // Validação
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new ArgumentException("Password cannot be empty", nameof(password));
            }

            if (password.Length < 8)
            {
                throw new ArgumentException("Password must be at least 8 characters", nameof(password));
            }

            if (password.Length > 100)
            {
                throw new ArgumentException("Password too long (max 100 characters)", nameof(password));
            }

            try
            {
                // Gera hash (demora ~300ms no cost 12)
                var startTime = DateTime.UtcNow;
                var hash = BCrypt.Net.BCrypt.HashPassword(password, BCRYPT_COST_FACTOR);
                var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;

                Log.Debug("Password hashed in {Duration}ms (cost factor: {CostFactor})",
                    duration, BCRYPT_COST_FACTOR);

                return hash;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to hash password");
                throw new InvalidOperationException("Failed to hash password", ex);
            }
        }

        /// <summary>
        /// Verifica se uma senha corresponde ao hash armazenado.
        /// Usado ao fazer LOGIN.
        /// </summary>
        /// <param name="password">Senha em plain text (digitada pelo usuário)</param>
        /// <param name="hash">Hash BCrypt armazenado no banco</param>
        /// <returns>
        /// True = senha correta
        /// False = senha incorreta
        /// </returns>
        public static bool Verify(string password, string hash)
        {
            // Validação
            if (string.IsNullOrWhiteSpace(password))
            {
                Log.Warning("Password verification failed: empty password");
                return false;
            }

            if (string.IsNullOrWhiteSpace(hash))
            {
                Log.Warning("Password verification failed: empty hash");
                return false;
            }

            // Hash BCrypt válido começa com "$2a$" ou "$2b$" ou "$2y$"
            if (!hash.StartsWith("$2"))
            {
                Log.Warning("Password verification failed: invalid hash format");
                return false;
            }

            try
            {
                // Verifica (demora ~300ms)
                var startTime = DateTime.UtcNow;
                var isValid = BCrypt.Net.BCrypt.Verify(password, hash);
                var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;

                Log.Debug("Password verified in {Duration}ms - Result: {IsValid}",
                    duration, isValid);

                return isValid;
            }
            catch (SaltParseException ex)
            {
                // Hash corrompido ou formato inválido
                Log.Error(ex, "Invalid BCrypt hash format");
                return false;
            }
            catch (Exception ex)
            {
                // Outro erro (ex: hash muito curto)
                Log.Error(ex, "Failed to verify password");
                return false;
            }
        }

        /// <summary>
        /// Verifica se um hash BCrypt precisa de rehashing.
        /// Útil se você aumentar o cost factor no futuro.
        /// </summary>
        /// <param name="hash">Hash BCrypt</param>
        /// <returns>True se precisa rehash (cost factor desatualizado)</returns>
        public static bool NeedsRehash(string hash)
        {
            if (string.IsNullOrWhiteSpace(hash))
                return true;

            try
            {
                // Extrai cost factor do hash (ex: "$2a$12$..." → 12)
                var parts = hash.Split('$');
                if (parts.Length < 4)
                    return true;

                if (int.TryParse(parts[2], out var costFactor))
                {
                    // Se cost factor for menor que o atual, precisa rehash
                    return costFactor < BCRYPT_COST_FACTOR;
                }

                return true;
            }
            catch
            {
                return true;
            }
        }

        /// <summary>
        /// Gera senha aleatória forte para testes ou reset.
        /// </summary>
        /// <param name="length">Tamanho da senha (padrão: 16)</param>
        /// <returns>Senha aleatória</returns>
        public static string GenerateRandomPassword(int length = 16)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";
            var random = new Random();
            var password = new char[length];

            for (int i = 0; i < length; i++)
            {
                password[i] = chars[random.Next(chars.Length)];
            }

            return new string(password);
        }
    }
}