// XL4Net.GameServer/Players/PlayerManager.cs

using System;
using System.Collections.Generic;
using System.Linq;
using Serilog;

namespace XL4Net.GameServer.Players
{
    /// <summary>
    /// Gerencia todas as sessões de jogadores conectados.
    /// Otimizado para buscas O(1) por PeerId e UserId.
    /// Suporta de 10 a 5000+ jogadores.
    /// </summary>
    public class PlayerManager
    {
        // ============================================
        // CAMPOS PRIVADOS
        // ============================================

        // Índice primário: PeerId → Session
        // Usado em: toda mensagem recebida da rede
        private readonly Dictionary<int, PlayerSession> _byPeerId;

        // Índice secundário: UserId → Session
        // Usado em: lógica de jogo (dano, party, trade, etc)
        // Só contém jogadores AUTENTICADOS
        private readonly Dictionary<Guid, PlayerSession> _byUserId;

        // Lock para thread-safety (I/O thread vs game loop)
        private readonly object _lock = new object();

        private readonly int _maxPlayers;

        // ============================================
        // PROPRIEDADES
        // ============================================

        /// <summary>
        /// Total de conexões (incluindo não autenticados).
        /// </summary>
        public int ConnectionCount
        {
            get
            {
                lock (_lock)
                {
                    return _byPeerId.Count;
                }
            }
        }

        /// <summary>
        /// Total de jogadores autenticados.
        /// </summary>
        public int AuthenticatedCount
        {
            get
            {
                lock (_lock)
                {
                    return _byUserId.Count;
                }
            }
        }

        /// <summary>
        /// Total de jogadores no jogo.
        /// </summary>
        public int InGameCount
        {
            get
            {
                lock (_lock)
                {
                    // Aqui usamos LINQ pois é chamado raramente (métricas)
                    return _byPeerId.Values.Count(p => p.IsInGame);
                }
            }
        }

        /// <summary>
        /// Servidor está cheio?
        /// </summary>
        public bool IsFull => ConnectionCount >= _maxPlayers;

        /// <summary>
        /// Máximo de jogadores permitido.
        /// </summary>
        public int MaxPlayers => _maxPlayers;

        // ============================================
        // EVENTOS
        // ============================================

        public event Action<PlayerSession> OnPlayerConnected;
        public event Action<PlayerSession> OnPlayerDisconnected;
        public event Action<PlayerSession> OnPlayerAuthenticated;
        public event Action<PlayerSession> OnPlayerEnteredGame;

        // ============================================
        // CONSTRUTOR
        // ============================================

        /// <summary>
        /// Cria gerenciador de jogadores.
        /// </summary>
        /// <param name="maxPlayers">Capacidade máxima</param>
        public PlayerManager(int maxPlayers)
        {
            if (maxPlayers <= 0)
                throw new ArgumentException("MaxPlayers must be > 0", nameof(maxPlayers));

            _maxPlayers = maxPlayers;

            // Pré-aloca capacidade para evitar redimensionamento
            _byPeerId = new Dictionary<int, PlayerSession>(maxPlayers);
            _byUserId = new Dictionary<Guid, PlayerSession>(maxPlayers);
        }

        // ============================================
        // ADICIONAR / REMOVER
        // ============================================

        /// <summary>
        /// Adiciona nova conexão (quando cliente conecta).
        /// </summary>
        /// <returns>Session criada, ou null se servidor cheio</returns>
        public PlayerSession AddConnection(int peerId, string ipAddress)
        {
            lock (_lock)
            {
                // Já existe?
                if (_byPeerId.TryGetValue(peerId, out var existing))
                {
                    Log.Warning("PeerId {PeerId} already exists, returning existing session", peerId);
                    return existing;
                }

                // Servidor cheio?
                if (_byPeerId.Count >= _maxPlayers)
                {
                    Log.Warning("Server full ({Max}), rejecting connection from {IP}",
                        _maxPlayers, ipAddress);
                    return null;
                }

                // Cria sessão
                var session = new PlayerSession(peerId, ipAddress);
                _byPeerId[peerId] = session;

                // Escuta mudanças de estado
                session.OnStateChanged += OnSessionStateChanged;

                Log.Information("Connection added: PeerId={PeerId}, IP={IP}, Total={Count}/{Max}",
                    peerId, ipAddress, _byPeerId.Count, _maxPlayers);

                OnPlayerConnected?.Invoke(session);

                return session;
            }
        }

        /// <summary>
        /// Remove conexão (quando cliente desconecta).
        /// </summary>
        public bool RemoveConnection(int peerId)
        {
            lock (_lock)
            {
                if (!_byPeerId.TryGetValue(peerId, out var session))
                {
                    Log.Warning("Tried to remove non-existent PeerId {PeerId}", peerId);
                    return false;
                }

                // Remove dos dois índices
                _byPeerId.Remove(peerId);

                if (session.UserId.HasValue)
                {
                    _byUserId.Remove(session.UserId.Value);
                }

                // Para de escutar eventos
                session.OnStateChanged -= OnSessionStateChanged;

                Log.Information("Connection removed: {Session}, Total={Count}/{Max}",
                    session, _byPeerId.Count, _maxPlayers);

                OnPlayerDisconnected?.Invoke(session);

                return true;
            }
        }

        // ============================================
        // BUSCA O(1)
        // ============================================

        /// <summary>
        /// Busca por PeerId. O(1)
        /// Usar para: processar mensagens da rede.
        /// </summary>
        public PlayerSession GetByPeerId(int peerId)
        {
            lock (_lock)
            {
                _byPeerId.TryGetValue(peerId, out var session);
                return session;
            }
        }

        /// <summary>
        /// Busca por UserId. O(1)
        /// Usar para: lógica de jogo (dano, party, trade).
        /// Só retorna jogadores autenticados.
        /// </summary>
        public PlayerSession GetByUserId(Guid userId)
        {
            lock (_lock)
            {
                _byUserId.TryGetValue(userId, out var session);
                return session;
            }
        }

        /// <summary>
        /// Verifica se usuário já está conectado. O(1)
        /// Usar para: evitar login duplo.
        /// </summary>
        public bool IsUserConnected(Guid userId)
        {
            lock (_lock)
            {
                return _byUserId.ContainsKey(userId);
            }
        }

        // ============================================
        // BUSCA O(n) - USAR COM MODERAÇÃO
        // ============================================

        /// <summary>
        /// Busca por Username. O(n)
        /// Usar para: comandos admin, chat whisper.
        /// NÃO usar em loops frequentes!
        /// </summary>
        public PlayerSession GetByUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return null;

            lock (_lock)
            {
                foreach (var session in _byUserId.Values)
                {
                    if (session.Username != null &&
                        session.Username.Equals(username, StringComparison.OrdinalIgnoreCase))
                    {
                        return session;
                    }
                }
                return null;
            }
        }

        // ============================================
        // LISTAGENS
        // ============================================

        /// <summary>
        /// Todas as conexões (incluindo não autenticadas).
        /// Retorna cópia para segurança.
        /// </summary>
        public List<PlayerSession> GetAllConnections()
        {
            lock (_lock)
            {
                return new List<PlayerSession>(_byPeerId.Values);
            }
        }

        /// <summary>
        /// Apenas jogadores autenticados.
        /// </summary>
        public List<PlayerSession> GetAuthenticatedPlayers()
        {
            lock (_lock)
            {
                return new List<PlayerSession>(_byUserId.Values);
            }
        }

        /// <summary>
        /// Apenas jogadores no jogo (InGame).
        /// </summary>
        public List<PlayerSession> GetPlayersInGame()
        {
            lock (_lock)
            {
                var result = new List<PlayerSession>(_byUserId.Count);
                foreach (var session in _byUserId.Values)
                {
                    if (session.IsInGame)
                        result.Add(session);
                }
                return result;
            }
        }

        /// <summary>
        /// PeerIds de jogadores no jogo (para broadcast).
        /// </summary>
        public List<int> GetInGamePeerIds()
        {
            lock (_lock)
            {
                var result = new List<int>(_byUserId.Count);
                foreach (var session in _byUserId.Values)
                {
                    if (session.IsInGame)
                        result.Add(session.PeerId);
                }
                return result;
            }
        }

        // ============================================
        // MANUTENÇÃO
        // ============================================

        /// <summary>
        /// Conexões inativas (para timeout).
        /// </summary>
        public List<PlayerSession> GetInactiveConnections(double timeoutSeconds)
        {
            lock (_lock)
            {
                var result = new List<PlayerSession>();
                foreach (var session in _byPeerId.Values)
                {
                    if (session.SecondsSinceLastActivity > timeoutSeconds)
                        result.Add(session);
                }
                return result;
            }
        }

        /// <summary>
        /// Conexões que não autenticaram a tempo.
        /// </summary>
        public List<PlayerSession> GetAuthenticationExpired(double gracePeriodSeconds)
        {
            lock (_lock)
            {
                var result = new List<PlayerSession>();
                foreach (var session in _byPeerId.Values)
                {
                    if (!session.IsAuthenticated &&
                        session.SecondsSinceConnection > gracePeriodSeconds)
                    {
                        result.Add(session);
                    }
                }
                return result;
            }
        }

        // ============================================
        // PRIVADO
        // ============================================

        /// <summary>
        /// Reage a mudanças de estado da sessão.
        /// Mantém índice secundário sincronizado.
        /// </summary>
        private void OnSessionStateChanged(
            PlayerSession session,
            PlayerConnectionState oldState,
            PlayerConnectionState newState)
        {
            // Quando autenticado, adiciona ao índice por UserId
            if (newState == PlayerConnectionState.Authenticated && session.UserId.HasValue)
            {
                lock (_lock)
                {
                    _byUserId[session.UserId.Value] = session;
                }

                Log.Debug("Player indexed by UserId: {Session}", session);
                OnPlayerAuthenticated?.Invoke(session);
            }

            // Quando entra no jogo
            if (newState == PlayerConnectionState.InGame)
            {
                OnPlayerEnteredGame?.Invoke(session);
            }
        }
    }
}