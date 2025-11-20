// XL4Net.AuthServer/Core/AuthConfig.cs

using System;

namespace XL4Net.AuthServer.Core
{
    /// <summary>
    /// Configurações do AuthServer.
    /// Carregadas de environment variables ou valores padrão.
    /// </summary>
    public class AuthConfig
    {
        // ============================================
        // DATABASE
        // ============================================

        /// <summary>
        /// Connection string do PostgreSQL.
        /// Formato: "Host=localhost;Port=5432;Database=xl4net;Username=xl4admin;Password=changeme"
        /// </summary>
        public string DatabaseConnectionString { get; set; } = string.Empty;

        // ============================================
        // JWT
        // ============================================

        /// <summary>
        /// Chave secreta para assinar tokens JWT.
        /// IMPORTANTE: NUNCA commitar no Git! Usar environment variable em produção.
        /// Mínimo: 32 caracteres.
        /// </summary>
        public string JwtSecretKey { get; set; } = string.Empty;

        /// <summary>
        /// Emissor do token JWT (issuer).
        /// </summary>
        public string JwtIssuer { get; set; } = "XL4Net.AuthServer";

        /// <summary>
        /// Audiência do token JWT (audience).
        /// </summary>
        public string JwtAudience { get; set; } = "XL4Net.GameServer";

        /// <summary>
        /// Tempo de validade do token JWT em minutos.
        /// Padrão: 60 minutos (1 hora).
        /// </summary>
        public int JwtExpirationMinutes { get; set; } = 60;

        // ============================================
        // RATE LIMITING
        // ============================================

        /// <summary>
        /// Janela de tempo para rate limiting (em minutos).
        /// Padrão: 60 minutos.
        /// </summary>
        public int RateLimitTimeWindowMinutes { get; set; } = 60;

        /// <summary>
        /// Máximo de tentativas de login na janela de tempo.
        /// Padrão: 5 tentativas.
        /// </summary>
        public int RateLimitMaxAttempts { get; set; } = 5;

        // ============================================
        // SERVER
        // ============================================

        /// <summary>
        /// Porta onde o AuthServer escuta (LiteNetLib).
        /// Padrão: 2106
        /// </summary>
        public int ServerPort { get; set; } = 2106;

        /// <summary>
        /// Connection key para validar clientes.
        /// Padrão: "XL4Net_AuthServer_v1.0"
        /// </summary>
        public string ConnectionKey { get; set; } = "XL4Net_AuthServer_v1.0";

        // ============================================
        // MÉTODOS
        // ============================================

        /// <summary>
        /// Carrega configurações de environment variables.
        /// Fallback para valores padrão se não encontrar.
        /// </summary>
        public static AuthConfig LoadFromEnvironment()
        {
            var config = new AuthConfig();

            // Database
            config.DatabaseConnectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING")
                ?? "Host=localhost;Port=5433;Database=xl4net;Username=xl4admin;Password=xl4netdev123";

            // JWT
            config.JwtSecretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
                ?? "xl4net-dev-secret-key-change-in-production-minimum-32-chars";

            config.JwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER")
                ?? "XL4Net.AuthServer";

            config.JwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE")
                ?? "XL4Net.GameServer";

            if (int.TryParse(Environment.GetEnvironmentVariable("JWT_EXPIRATION_MINUTES"), out var jwtExpiration))
                config.JwtExpirationMinutes = jwtExpiration;

            // Rate Limiting
            if (int.TryParse(Environment.GetEnvironmentVariable("RATE_LIMIT_WINDOW_MINUTES"), out var rateLimitWindow))
                config.RateLimitTimeWindowMinutes = rateLimitWindow;

            if (int.TryParse(Environment.GetEnvironmentVariable("RATE_LIMIT_MAX_ATTEMPTS"), out var rateLimitMax))
                config.RateLimitMaxAttempts = rateLimitMax;

            // Server
            if (int.TryParse(Environment.GetEnvironmentVariable("SERVER_PORT"), out var serverPort))
                config.ServerPort = serverPort;

            config.ConnectionKey = Environment.GetEnvironmentVariable("CONNECTION_KEY")
                ?? "XL4Net_AuthServer_v1.0";

            return config;
        }

        /// <summary>
        /// Valida se a configuração está válida.
        /// </summary>
        public bool Validate(out string error)
        {
            error = string.Empty;

            // Database
            if (string.IsNullOrWhiteSpace(DatabaseConnectionString))
            {
                error = "Database connection string is required";
                return false;
            }

            // JWT Secret
            if (string.IsNullOrWhiteSpace(JwtSecretKey))
            {
                error = "JWT secret key is required";
                return false;
            }

            if (JwtSecretKey.Length < 32)
            {
                error = "JWT secret key must be at least 32 characters";
                return false;
            }

            // JWT Expiration
            if (JwtExpirationMinutes <= 0)
            {
                error = "JWT expiration must be positive";
                return false;
            }

            // Rate Limiting
            if (RateLimitTimeWindowMinutes <= 0)
            {
                error = "Rate limit window must be positive";
                return false;
            }

            if (RateLimitMaxAttempts <= 0)
            {
                error = "Rate limit max attempts must be positive";
                return false;
            }

            // Server Port
            if (ServerPort <= 0 || ServerPort > 65535)
            {
                error = "Server port must be between 1 and 65535";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Retorna string legível para logs (SEM SECRET KEY!).
        /// </summary>
        public override string ToString()
        {
            var dbPreview = DatabaseConnectionString.Split(';')[0] + ";...";
            return $@"AuthConfig:
  Database: {dbPreview}
  JWT: Issuer={JwtIssuer}, Audience={JwtAudience}, Expiration={JwtExpirationMinutes}min
  Rate Limiting: {RateLimitMaxAttempts} attempts per {RateLimitTimeWindowMinutes}min
  Server: Port={ServerPort}";
        }
    }
}