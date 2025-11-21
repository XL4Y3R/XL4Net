// XL4Net.GameServer/Players/PlayerSession.cs

using System;
using XL4Net.Shared.Math;

namespace XL4Net.GameServer.Players
{
    /// <summary>
    /// Estados possíveis de um jogador conectado.
    /// </summary>
    public enum PlayerConnectionState
    {
        /// <summary>
        /// Conectou mas ainda não enviou token.
        /// </summary>
        Connected,

        /// <summary>
        /// Enviou token, aguardando validação com AuthServer.
        /// </summary>
        Authenticating,

        /// <summary>
        /// Token validado, jogador autenticado.
        /// </summary>
        Authenticated,

        /// <summary>
        /// Entrou no mundo do jogo.
        /// </summary>
        InGame,

        /// <summary>
        /// Em processo de desconexão.
        /// </summary>
        Disconnecting
    }

    /// <summary>
    /// Representa um jogador conectado ao GameServer.
    /// Contém dados de conexão, autenticação e estado de jogo.
    /// </summary>
    public class PlayerSession
    {
        // ============================================
        // CAMPOS PRIVADOS
        // ============================================

        private PlayerConnectionState _state;

        // ============================================
        // PROPRIEDADES - CONEXÃO
        // ============================================

        /// <summary>
        /// ID único da conexão (atribuído pelo LiteNetLib).
        /// </summary>
        public int PeerId { get; }

        /// <summary>
        /// Endereço IP do cliente.
        /// </summary>
        public string IpAddress { get; }

        /// <summary>
        /// Momento em que o cliente conectou.
        /// </summary>
        public DateTime ConnectedAt { get; }

        /// <summary>
        /// Momento do último pacote recebido (para timeout).
        /// </summary>
        public DateTime LastActivityAt { get; private set; }

        /// <summary>
        /// Latência atual em milissegundos (RTT / 2).
        /// </summary>
        public int LatencyMs { get; private set; }

        // ============================================
        // PROPRIEDADES - AUTENTICAÇÃO
        // ============================================

        /// <summary>
        /// Estado atual da conexão.
        /// </summary>
        public PlayerConnectionState State
        {
            get => _state;
            private set
            {
                var oldState = _state;
                _state = value;
                StateChangedAt = DateTime.UtcNow;

                // Dispara evento se alguém estiver ouvindo
                OnStateChanged?.Invoke(this, oldState, value);
            }
        }

        /// <summary>
        /// Momento da última mudança de estado.
        /// </summary>
        public DateTime StateChangedAt { get; private set; }

        /// <summary>
        /// ID do usuário (vem do AuthServer após validação do token).
        /// Null até ser autenticado.
        /// </summary>
        public Guid? UserId { get; private set; }

        /// <summary>
        /// Nome do usuário (vem do AuthServer).
        /// Null até ser autenticado.
        /// </summary>
        public string Username { get; private set; }

        /// <summary>
        /// Token JWT enviado pelo cliente.
        /// Guardamos para possível revalidação.
        /// </summary>
        public string AuthToken { get; private set; }

        // ============================================
        // PROPRIEDADES - POSIÇÃO E MOVIMENTO
        // ============================================

        /// <summary>
        /// Posição do jogador no mundo.
        /// </summary>
        public Vec3 Position { get; set; }

        /// <summary>
        /// Velocidade atual do jogador.
        /// Importante para física (gravidade, inércia).
        /// </summary>
        public Vec3 Velocity { get; set; }

        /// <summary>
        /// Rotação (ângulo Y) em graus.
        /// </summary>
        public float Rotation { get; set; }

        // ============================================
        // PROPRIEDADES - FLAGS DE ESTADO
        // ============================================

        /// <summary>
        /// Jogador está no chão?
        /// </summary>
        public bool IsGrounded { get; set; }

        /// <summary>
        /// Jogador está correndo?
        /// </summary>
        public bool IsSprinting { get; set; }

        /// <summary>
        /// Jogador está agachado?
        /// </summary>
        public bool IsCrouching { get; set; }

        /// <summary>
        /// Jogador está pulando (subindo)?
        /// </summary>
        public bool IsJumping { get; set; }

        /// <summary>
        /// Jogador está caindo?
        /// </summary>
        public bool IsFalling { get; set; }

        // ============================================
        // PROPRIEDADES - PREDICTION/RECONCILIATION
        // ============================================

        /// <summary>
        /// Último tick em que o jogador enviou input.
        /// </summary>
        public int LastInputTick { get; set; }

        /// <summary>
        /// Último SequenceNumber de input processado pelo servidor.
        /// Cliente usa para saber quais inputs descartar do buffer.
        /// </summary>
        public uint LastProcessedInputSequence { get; set; }

        // ============================================
        // EVENTOS
        // ============================================

        /// <summary>
        /// Disparado quando o estado do jogador muda.
        /// </summary>
        public event Action<PlayerSession, PlayerConnectionState, PlayerConnectionState> OnStateChanged;

        // ============================================
        // CONSTRUTOR
        // ============================================

        /// <summary>
        /// Cria uma nova sessão de jogador.
        /// </summary>
        /// <param name="peerId">ID da conexão LiteNetLib</param>
        /// <param name="ipAddress">Endereço IP do cliente</param>
        public PlayerSession(int peerId, string ipAddress)
        {
            PeerId = peerId;
            IpAddress = ipAddress ?? "unknown";
            ConnectedAt = DateTime.UtcNow;
            LastActivityAt = DateTime.UtcNow;
            StateChangedAt = DateTime.UtcNow;
            _state = PlayerConnectionState.Connected;

            // Estado inicial de movimento (spawn point padrão)
            Position = new Vec3(0f, 0f, 0f);
            Velocity = Vec3.Zero;
            Rotation = 0f;

            // Flags iniciais
            IsGrounded = true;  // Começa no chão
            IsSprinting = false;
            IsCrouching = false;
            IsJumping = false;
            IsFalling = false;

            // Prediction
            LastInputTick = 0;
            LastProcessedInputSequence = 0;
        }

        // ============================================
        // MÉTODOS PÚBLICOS
        // ============================================

        /// <summary>
        /// Registra atividade do jogador (atualiza LastActivityAt).
        /// Chamar sempre que receber qualquer pacote deste jogador.
        /// </summary>
        public void RecordActivity()
        {
            LastActivityAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Atualiza a latência do jogador.
        /// </summary>
        /// <param name="latencyMs">Latência em milissegundos</param>
        public void UpdateLatency(int latencyMs)
        {
            LatencyMs = latencyMs;
        }

        /// <summary>
        /// Inicia processo de autenticação.
        /// Chamar quando receber token do cliente.
        /// </summary>
        /// <param name="token">Token JWT enviado pelo cliente</param>
        public void BeginAuthentication(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentException("Token cannot be empty", nameof(token));

            AuthToken = token;
            State = PlayerConnectionState.Authenticating;
        }

        /// <summary>
        /// Finaliza autenticação com sucesso.
        /// Chamar quando AuthServer validar o token.
        /// </summary>
        /// <param name="userId">ID do usuário</param>
        /// <param name="username">Nome do usuário</param>
        public void CompleteAuthentication(Guid userId, string username)
        {
            if (userId == Guid.Empty)
                throw new ArgumentException("UserId cannot be empty", nameof(userId));

            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Username cannot be empty", nameof(username));

            UserId = userId;
            Username = username;
            State = PlayerConnectionState.Authenticated;
        }

        /// <summary>
        /// Falha na autenticação.
        /// Jogador será desconectado em seguida.
        /// </summary>
        public void FailAuthentication()
        {
            State = PlayerConnectionState.Disconnecting;
        }

        /// <summary>
        /// Jogador entrou no mundo.
        /// Chamar após spawn inicial.
        /// </summary>
        /// <param name="spawnPosition">Posição de spawn (opcional)</param>
        public void EnterGame(Vec3? spawnPosition = null)
        {
            if (State != PlayerConnectionState.Authenticated)
                throw new InvalidOperationException("Player must be authenticated before entering game");

            if (spawnPosition.HasValue)
            {
                Position = spawnPosition.Value;
            }

            // Reset estado de movimento
            Velocity = Vec3.Zero;
            IsGrounded = true;
            IsSprinting = false;
            IsCrouching = false;
            IsJumping = false;
            IsFalling = false;
            LastProcessedInputSequence = 0;

            State = PlayerConnectionState.InGame;
        }

        /// <summary>
        /// Inicia processo de desconexão.
        /// </summary>
        public void BeginDisconnect()
        {
            State = PlayerConnectionState.Disconnecting;
        }

        /// <summary>
        /// Verifica se o jogador está autenticado.
        /// </summary>
        public bool IsAuthenticated => State == PlayerConnectionState.Authenticated ||
                                       State == PlayerConnectionState.InGame;

        /// <summary>
        /// Verifica se o jogador está no jogo.
        /// </summary>
        public bool IsInGame => State == PlayerConnectionState.InGame;

        /// <summary>
        /// Tempo desde a última atividade (em segundos).
        /// </summary>
        public double SecondsSinceLastActivity =>
            (DateTime.UtcNow - LastActivityAt).TotalSeconds;

        /// <summary>
        /// Tempo desde a conexão (em segundos).
        /// </summary>
        public double SecondsSinceConnection =>
            (DateTime.UtcNow - ConnectedAt).TotalSeconds;

        /// <summary>
        /// Tempo no estado atual (em segundos).
        /// </summary>
        public double SecondsInCurrentState =>
            (DateTime.UtcNow - StateChangedAt).TotalSeconds;

        // ============================================
        // OVERRIDE
        // ============================================

        public override string ToString()
        {
            var name = Username ?? $"Peer#{PeerId}";
            return $"PlayerSession {{ {name}, State={State}, Pos={Position}, IP={IpAddress} }}";
        }
    }
}