// XL4Net.GameServer/Config/GameServerConfig.cs

using System;

namespace XL4Net.GameServer.Config
{
    /// <summary>
    /// Configurações do GameServer.
    /// Valores podem ser alterados via arquivo ou variáveis de ambiente.
    /// </summary>
    public class GameServerConfig
    {
        // ============================================
        // NETWORK
        // ============================================

        /// <summary>
        /// Porta principal do servidor (TCP/UDP).
        /// </summary>
        public ushort Port { get; set; } = 7777;

        /// <summary>
        /// Chave de conexão que o cliente deve enviar.
        /// Deve ser igual no cliente e servidor.
        /// </summary>
        public string ConnectionKey { get; set; } = "XL4Net_v1.0";

        /// <summary>
        /// Máximo de jogadores simultâneos.
        /// </summary>
        public int MaxPlayers { get; set; } = 100;

        // ============================================
        // GAME LOOP
        // ============================================

        /// <summary>
        /// Taxa de atualização do servidor em Hz.
        /// 30 = 30 ticks por segundo (~33ms por tick).
        /// </summary>
        public int TickRate { get; set; } = 30;

        /// <summary>
        /// Intervalo entre ticks em milissegundos.
        /// Calculado automaticamente a partir do TickRate.
        /// </summary>
        public double TickIntervalMs => 1000.0 / TickRate;

        // ============================================
        // AUTHENTICATION
        // ============================================

        /// <summary>
        /// URL do AuthServer para validação de tokens.
        /// </summary>
        public string AuthServerHost { get; set; } = "127.0.0.1";

        /// <summary>
        /// Porta do AuthServer.
        /// </summary>
        public ushort AuthServerPort { get; set; } = 2106;

        /// <summary>
        /// Timeout para validação de token (em segundos).
        /// </summary>
        public int AuthTimeoutSeconds { get; set; } = 5;

        /// <summary>
        /// Tempo máximo que um cliente pode ficar sem autenticar (em segundos).
        /// Após esse tempo, é desconectado.
        /// </summary>
        public int AuthGracePeriodSeconds { get; set; } = 10;

        /// <summary>
        /// Chave secreta para validar tokens JWT.
        /// DEVE ser a mesma usada pelo AuthServer.
        /// </summary>
        public string JwtSecret { get; set; } = "XL4Net_JWT_Secret_Key_Change_In_Production_2024";

        /// <summary>
        /// Issuer esperado dos tokens JWT.
        /// </summary>
        public string JwtIssuer { get; set; } = "XL4Net.AuthServer";

        // ============================================
        // TIMEOUTS
        // ============================================

        /// <summary>
        /// Intervalo entre pings para medir latência (em segundos).
        /// </summary>
        public int PingIntervalSeconds { get; set; } = 1;

        /// <summary>
        /// Tempo sem resposta antes de considerar cliente desconectado (em segundos).
        /// </summary>
        public int DisconnectTimeoutSeconds { get; set; } = 10;

        // ============================================
        // WORLD
        // ============================================

        /// <summary>
        /// Distância de visão dos jogadores (para AOI na Fase 6).
        /// </summary>
        public float ViewDistance { get; set; } = 50f;

        // ============================================
        // LOGGING
        // ============================================

        /// <summary>
        /// Nome do servidor para identificação nos logs.
        /// </summary>
        public string ServerName { get; set; } = "GameServer-01";

        /// <summary>
        /// Nível de log (Debug, Information, Warning, Error).
        /// </summary>
        public string LogLevel { get; set; } = "Information";

        // ============================================
        // MÉTODOS
        // ============================================

        /// <summary>
        /// Valida se as configurações estão corretas.
        /// </summary>
        public void Validate()
        {
            if (Port == 0)
                throw new ArgumentException("Port cannot be 0");

            if (MaxPlayers <= 0)
                throw new ArgumentException("MaxPlayers must be greater than 0");

            if (TickRate < 10 || TickRate > 128)
                throw new ArgumentException("TickRate must be between 10 and 128");

            if (string.IsNullOrWhiteSpace(ConnectionKey))
                throw new ArgumentException("ConnectionKey cannot be empty");

            if (string.IsNullOrWhiteSpace(JwtSecret))
                throw new ArgumentException("JwtSecret cannot be empty");

            if (JwtSecret.Length < 32)
                throw new ArgumentException("JwtSecret must be at least 32 characters");
        }

        /// <summary>
        /// Retorna representação em string para logging.
        /// </summary>
        public override string ToString()
        {
            return $"GameServerConfig {{ Port={Port}, MaxPlayers={MaxPlayers}, " +
                   $"TickRate={TickRate}Hz, AuthServer={AuthServerHost}:{AuthServerPort} }}";
        }
    }
}