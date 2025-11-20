// XL4Net.AuthServer/Models/RateLimitResult.cs

namespace XL4Net.AuthServer.Models
{
    /// <summary>
    /// DTO retornado pela função check_rate_limit() do PostgreSQL.
    /// Indica se um IP está temporariamente bloqueado por excesso de tentativas.
    /// </summary>
    public class RateLimitResult
    {
        /// <summary>
        /// Quantidade de tentativas falhas no time window.
        /// Exemplo: 3 tentativas nos últimos 60 minutos.
        /// </summary>
        public long AttemptsCount { get; set; }

        /// <summary>
        /// TRUE = IP está bloqueado (excedeu limite)
        /// FALSE = IP pode tentar login
        /// </summary>
        public bool IsLimited { get; set; }

        /// <summary>
        /// Segundos restantes até poder tentar novamente.
        /// 0 = pode tentar agora
        /// > 0 = aguarde N segundos
        /// </summary>
        public int RetryAfterSeconds { get; set; }

        /// <summary>
        /// Indica se pode tentar login agora.
        /// </summary>
        public bool CanAttemptLogin => !IsLimited;

        /// <summary>
        /// Mensagem amigável para o usuário.
        /// </summary>
        public string GetUserMessage()
        {
            if (CanAttemptLogin)
                return "Login allowed";

            if (RetryAfterSeconds < 60)
                return $"Too many failed attempts. Try again in {RetryAfterSeconds} seconds.";

            var minutes = RetryAfterSeconds / 60;
            return $"Too many failed attempts. Try again in {minutes} minutes.";
        }

        /// <summary>
        /// Retorna string legível para logs.
        /// </summary>
        public override string ToString()
        {
            if (IsLimited)
                return $"RateLimitResult[BLOCKED]: {AttemptsCount} attempts - Retry after {RetryAfterSeconds}s";
            else
                return $"RateLimitResult[OK]: {AttemptsCount} attempts - Can proceed";
        }
    }
}