// XL4Net.GameServer/Core/GameServer.cs

using LiteNetLib;
using MessagePack;
using Serilog;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using XL4Net.GameServer.Config;
using XL4Net.GameServer.Handlers;
using XL4Net.GameServer.Handlers.Auth;
using XL4Net.GameServer.Players;
using XL4Net.Server.Transport;
using XL4Net.Shared.Pooling;
using XL4Net.Shared.Protocol.Enums;
using XL4Net.Shared.Protocol.Messages.Game;
using XL4Net.Shared.Transport;
using XL4Net.GameServer.Handlers.Game;
using XL4Net.Shared.Prediction;       


namespace XL4Net.GameServer.Core
{
    /// <summary>
    /// Servidor de jogo principal.
    /// Gerencia game loop, jogadores e comunicação.
    /// </summary>
    public class GameServer : IDisposable
    {
        // ============================================
        // CAMPOS PRIVADOS
        // ============================================

        // Configuração
        private readonly GameServerConfig _config;

        // Transport (comunicação de rede)
        private readonly LiteNetServerTransport _transport;

        // Gerenciador de jogadores
        private readonly PlayerManager _playerManager;

        // Controle do game loop
        private CancellationTokenSource _cts;
        private Task _gameLoopTask;
        private bool _isRunning;
        private bool _disposed;

        // Métricas
        private int _currentTick;
        private double _lastTickDurationMs;
        private double _averageTickDurationMs;
        private readonly Stopwatch _tickStopwatch;

        // Sistema de handlers
        private readonly MessageHandlerRegistry _handlerRegistry;
        private readonly GameAuthHandler _authHandler;
        private readonly PlayerInputHandler _inputHandler;

        // ============================================
        // PROPRIEDADES
        // ============================================

        /// <summary>
        /// Servidor está rodando?
        /// </summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// Tick atual do servidor.
        /// </summary>
        public int CurrentTick => _currentTick;

        /// <summary>
        /// Jogadores conectados.
        /// </summary>
        public int PlayerCount => _playerManager.ConnectionCount;

        /// <summary>
        /// Jogadores autenticados.
        /// </summary>
        public int AuthenticatedCount => _playerManager.AuthenticatedCount;

        /// <summary>
        /// Duração do último tick (ms).
        /// </summary>
        public double LastTickDurationMs => _lastTickDurationMs;

        /// <summary>
        /// Duração média dos ticks (ms).
        /// </summary>
        public double AverageTickDurationMs => _averageTickDurationMs;

        /// <summary>
        /// Configuração do servidor.
        /// </summary>
        public GameServerConfig Config => _config;

        /// <summary>
        /// Gerenciador de jogadores (para acesso externo).
        /// </summary>
        public PlayerManager Players => _playerManager;

        // ============================================
        // EVENTOS
        // ============================================

        /// <summary>
        /// Disparado quando o servidor inicia.
        /// </summary>
        public event Action OnServerStarted;

        /// <summary>
        /// Disparado quando o servidor para.
        /// </summary>
        public event Action OnServerStopped;

        /// <summary>
        /// Disparado a cada tick (para sistemas externos).
        /// </summary>
        public event Action<int> OnTick;

        // ============================================
        // CONSTRUTOR
        // ============================================

        /// <summary>
        /// Cria nova instância do GameServer.
        /// </summary>
        /// <param name="config">Configuração do servidor</param>
        public GameServer(GameServerConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            // Valida configuração
            _config.Validate();

            // Inicializa componentes
            _playerManager = new PlayerManager(_config.MaxPlayers);

            // Transport recebe port e maxClients no construtor
            _transport = new LiteNetServerTransport(_config.Port, _config.MaxPlayers);

            // Sistema de handlers
            _handlerRegistry = new MessageHandlerRegistry(this);
            _authHandler = new GameAuthHandler(
                jwtSecret: _config.JwtSecret,
                jwtIssuer: _config.JwtIssuer
            );

            _inputHandler = new PlayerInputHandler(
                tickRate: _config.TickRate,
                movementSettings: MovementSettings.Default
            );

            RegisterMessageHandlers();


            _tickStopwatch = new Stopwatch();

            // Registra handlers de eventos do transport
            RegisterTransportEvents();

            Log.Information("GameServer created: {Config}", _config);
        }

        /// <summary>
        /// Registra todos os handlers de mensagens.
        /// </summary>
        private void RegisterMessageHandlers()
        {
            // AuthHandler é tratado como caso especial (não vai no registry)
            // TODO: Fase 4+ - Registrar outros handlers aqui
            // _handlerRegistry.Register(new PlayerMoveHandler());

            _handlerRegistry.LogRegisteredHandlers();
        }

        // ============================================
        // MÉTODOS PÚBLICOS - LIFECYCLE
        // ============================================

        /// <summary>
        /// Inicia o servidor.
        /// </summary>
        public async Task<bool> StartAsync()
        {
            if (_isRunning)
            {
                Log.Warning("Server is already running");
                return false;
            }

            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(GameServer));
            }

            Log.Information("Starting GameServer on port {Port}...", _config.Port);

            try
            {
                // Transport.StartAsync() não recebe parâmetros (usa do construtor)
                var transportStarted = await _transport.StartAsync();

                if (!transportStarted)
                {
                    Log.Error("Failed to start transport on port {Port}", _config.Port);
                    return false;
                }

                // Inicia game loop
                _cts = new CancellationTokenSource();
                _isRunning = true;
                _gameLoopTask = Task.Run(() => GameLoopAsync(_cts.Token));

                Log.Information("GameServer started successfully on port {Port} @ {TickRate}Hz",
                    _config.Port, _config.TickRate);

                OnServerStarted?.Invoke();

                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to start GameServer");
                _isRunning = false;
                return false;
            }
        }

        /// <summary>
        /// Para o servidor gracefully.
        /// </summary>
        public async Task StopAsync()
        {
            if (!_isRunning)
            {
                Log.Warning("Server is not running");
                return;
            }

            Log.Information("Stopping GameServer...");

            try
            {
                // Sinaliza para o game loop parar
                _cts?.Cancel();

                // Aguarda game loop finalizar (com timeout)
                if (_gameLoopTask != null)
                {
                    var completedInTime = await Task.WhenAny(
                        _gameLoopTask,
                        Task.Delay(TimeSpan.FromSeconds(5))
                    ) == _gameLoopTask;

                    if (!completedInTime)
                    {
                        Log.Warning("Game loop did not stop in time, forcing shutdown");
                    }
                }

                // Para transport
                await _transport.StopAsync();

                _isRunning = false;

                Log.Information("GameServer stopped. Final tick: {Tick}, Players served: {Count}",
                    _currentTick, _playerManager.ConnectionCount);

                OnServerStopped?.Invoke();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error stopping GameServer");
                _isRunning = false;
            }
        }

        // ============================================
        // GAME LOOP
        // ============================================

        /// <summary>
        /// Loop principal do servidor (30Hz).
        /// Roda na main thread, processa tudo sequencialmente.
        /// </summary>
        private async Task GameLoopAsync(CancellationToken ct)
        {
            Log.Debug("Game loop started");

            var targetTickTime = TimeSpan.FromMilliseconds(_config.TickIntervalMs);

            while (!ct.IsCancellationRequested)
            {
                _tickStopwatch.Restart();

                try
                {
                    // ========== TICK START ==========

                    // 1. Processa mensagens da rede (da fila thread-safe)
                    ProcessIncomingMessages();

                    // 2. Atualiza jogadores (timeout, autenticação, etc)
                    UpdatePlayers();

                    // 3. Simula mundo (física, AI, etc) - PLACEHOLDER
                    SimulateWorld();

                    // 4. Envia estados para clientes - PLACEHOLDER
                    BroadcastStates();

                    // 5. Incrementa tick
                    _currentTick++;

                    // Dispara evento de tick
                    OnTick?.Invoke(_currentTick);

                    // ========== TICK END ==========
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error in game loop tick {Tick}", _currentTick);
                }

                // Mede duração do tick
                _tickStopwatch.Stop();
                _lastTickDurationMs = _tickStopwatch.Elapsed.TotalMilliseconds;

                // Média móvel exponencial (suaviza métricas)
                _averageTickDurationMs = _averageTickDurationMs * 0.9 + _lastTickDurationMs * 0.1;

                // Aviso se tick demorou demais
                if (_lastTickDurationMs > _config.TickIntervalMs)
                {
                    Log.Warning("Tick {Tick} took {Duration:F2}ms (target: {Target:F2}ms)",
                        _currentTick, _lastTickDurationMs, _config.TickIntervalMs);
                }

                // Calcula tempo restante até próximo tick
                var elapsed = _tickStopwatch.Elapsed;
                var remaining = targetTickTime - elapsed;

                if (remaining > TimeSpan.Zero)
                {
                    // Dorme até próximo tick
                    await Task.Delay(remaining, ct).ConfigureAwait(false);
                }
            }

            Log.Debug("Game loop ended at tick {Tick}", _currentTick);
        }

        // ============================================
        // MÉTODOS DO TICK
        // ============================================

        /// <summary>
        /// Processa mensagens recebidas da rede.
        /// Chamado pelo game loop a cada tick.
        /// </summary>
        private void ProcessIncomingMessages()
        {
            // Transport processa mensagens da fila interna
            // e dispara eventos (OnPeerConnected, OnPacketReceived, etc)
            _transport.ProcessIncoming();
        }

        /// <summary>
        /// Atualiza estado dos jogadores.
        /// Verifica timeouts, autenticação expirada, etc.
        /// </summary>
        private void UpdatePlayers()
        {
            // Verifica jogadores que não autenticaram a tempo
            var authExpired = _playerManager.GetAuthenticationExpired(_config.AuthGracePeriodSeconds);
            foreach (var session in authExpired)
            {
                Log.Warning("Player {PeerId} did not authenticate in time, disconnecting",
                    session.PeerId);

                DisconnectPlayer(session.PeerId, "Authentication timeout");
            }

            // Verifica jogadores inativos
            var inactive = _playerManager.GetInactiveConnections(_config.DisconnectTimeoutSeconds);
            foreach (var session in inactive)
            {
                Log.Warning("Player {Player} inactive for {Seconds:F1}s, disconnecting",
                    session, session.SecondsSinceLastActivity);

                DisconnectPlayer(session.PeerId, "Inactivity timeout");
            }
        }

        /// <summary>
        /// Simula o mundo do jogo.
        /// PLACEHOLDER - Será implementado nas próximas fases.
        /// </summary>
        private void SimulateWorld()
        {
            // TODO: Fase 4+ - Física, AI, etc
        }

        /// <summary>
        /// Envia estados para os clientes.
        /// PLACEHOLDER - Será implementado nas próximas fases.
        /// </summary>
        private void BroadcastStates()
        {
            // TODO: Implementar quando tiver mais de 1 jogador
            // 
            // O fluxo seria:
            // 1. Para cada jogador InGame
            // 2. Obter lista de jogadores próximos (AOI)
            // 3. Criar WorldSnapshotMessage com PlayerSnapshots
            // 4. Enviar para o jogador
            //
            // Exemplo:
            // var snapshot = new WorldSnapshotMessage
            // {
            //     ServerTick = (uint)_currentTick,
            //     Players = GetNearbyPlayerSnapshots(session)
            // };
            // SendTo(session.PeerId, packet, DeliveryMethod.Unreliable);
        }

        // ============================================
        // MÉTODOS - COMUNICAÇÃO
        // ============================================

        /// <summary>
        /// Desconecta um jogador.
        /// </summary>
        /// <param name="peerId">ID da conexão</param>
        /// <param name="reason">Motivo da desconexão</param>
        public void DisconnectPlayer(int peerId, string reason = "Disconnected by server")
        {
            var session = _playerManager.GetByPeerId(peerId);
            if (session != null)
            {
                session.BeginDisconnect();
            }

            _transport.DisconnectClient(peerId, reason);
        }

        /// <summary>
        /// Envia pacote para um jogador específico.
        /// </summary>
        public void SendTo(int peerId, Packet packet, DeliveryMethod delivery = DeliveryMethod.ReliableOrdered)
        {
            _transport.SendTo(peerId, packet, delivery);
        }


        /// <summary>
        /// Envia pacote para todos os jogadores no jogo.
        /// </summary>
        public void BroadcastToAll(Packet packet, DeliveryMethod delivery = DeliveryMethod.ReliableOrdered)
        {
            var peerIds = _playerManager.GetInGamePeerIds();
            foreach (var peerId in peerIds)
            {
                _transport.SendTo(peerId, packet, delivery);
            }
        }

        /// <summary>
        /// Envia pacote para todos exceto um jogador.
        /// </summary>
        public void BroadcastExcept(int excludePeerId, Packet packet, DeliveryMethod delivery = DeliveryMethod.ReliableOrdered)
        {
            var peerIds = _playerManager.GetInGamePeerIds();
            foreach (var peerId in peerIds)
            {
                if (peerId != excludePeerId)
                {
                    _transport.SendTo(peerId, packet, delivery);
                }
            }
        }

        // ============================================
        // HANDLERS DE EVENTOS DO TRANSPORT
        // ============================================

        /// <summary>
        /// Registra handlers para eventos do transport.
        /// </summary>
        private void RegisterTransportEvents()
        {
            _transport.OnClientConnected += HandlePeerConnected;
            _transport.OnClientDisconnected += HandlePeerDisconnected;
            _transport.OnPacketReceived += HandlePacketReceived;
            _transport.OnError += HandleTransportError;
        }

        /// <summary>
        /// Remove handlers de eventos do transport.
        /// </summary>
        private void UnregisterTransportEvents()
        {
            _transport.OnClientConnected -= HandlePeerConnected;
            _transport.OnClientDisconnected -= HandlePeerDisconnected;
            _transport.OnPacketReceived -= HandlePacketReceived;
            _transport.OnError -= HandleTransportError;
        }

        /// <summary>
        /// Chamado quando um cliente conecta.
        /// </summary>
        private void HandlePeerConnected(int peerId, string ipAddress)  // ← Agora recebe IP
        {
            Log.Information("Peer connected: PeerId={PeerId}, IP={IP}", peerId, ipAddress);

            var session = _playerManager.AddConnection(peerId, ipAddress);

            if (session == null)
            {
                // Servidor cheio, desconecta
                _transport.DisconnectClient(peerId, "Server is full");
                return;
            }

            // TODO: Enviar mensagem de boas-vindas / solicitar autenticação
        }

        /// <summary>
        /// Chamado quando um cliente desconecta.
        /// </summary>
        private void HandlePeerDisconnected(int peerId, string reason)
        {
            var session = _playerManager.GetByPeerId(peerId);

            Log.Information("Peer disconnected: {Session}, Reason={Reason}",
                session?.ToString() ?? $"PeerId={peerId}", reason);

            _playerManager.RemoveConnection(peerId);
        }

        /// <summary>
        /// Chamado quando um pacote é recebido.
        /// </summary>
        private void HandlePacketReceived(int peerId, Packet packet)
        {
            var session = _playerManager.GetByPeerId(peerId);

            if (session == null)
            {
                Log.Warning("Received packet from unknown peer {PeerId}, ignoring", peerId);
                PacketPool.Return(packet);
                return;
            }

            // Atualiza última atividade
            session.RecordActivity();

            var packetType = (PacketType)packet.Type;

            Log.Debug("Packet received: PeerId={PeerId}, Type={Type}, Size={Size}, State={State}",
                peerId, packetType, packet.PayloadSize, session.State);

            // Cliente não autenticado - só aceita AuthRequest
            if (!session.IsAuthenticated)
            {
                HandleUnauthenticatedPacket(session, packet);
                return;
            }

            // Cliente autenticado - usa sistema de handlers
            HandleAuthenticatedPacket(session, packet);
        }

        /// <summary>
        /// Processa pacote de cliente autenticado.
        /// </summary>
        private void HandleAuthenticatedPacket(PlayerSession session, Packet packet)
        {
            var packetType = (PacketType)packet.Type;

            // Tenta dispatch pelo registry
            bool handled = _handlerRegistry.Dispatch(session.PeerId, session, packet);

            if (!handled)
            {
                if (packetType == PacketType.Data)
                {
                    HandleDataPacket(session, packet);
                }
                else
                {
                    Log.Warning("Unhandled PacketType.{Type} from {Player}", packetType, session);
                    PacketPool.Return(packet);
                }
            }
        }

        /// <summary>
        /// Processa pacote de cliente não autenticado.
        /// </summary>
        private void HandleUnauthenticatedPacket(PlayerSession session, Packet packet)
        {
            var packetType = (PacketType)packet.Type;

            // Só aceita pacotes Data
            if (packetType != PacketType.Data)
            {
                Log.Warning("Unauthenticated client {PeerId} sent {Type}, ignoring", session.PeerId, packetType);
                PacketPool.Return(packet);
                return;
            }

            // Verifica se é AuthRequest
            try
            {
                var messageType = PeekMessageType(packet.Payload);

                if (messageType != MessageType.TokenValidationRequest)
                {
                    Log.Warning("Unauthenticated client {PeerId} sent {MessageType}, expected auth",
                        session.PeerId, messageType);

                    SendAuthError(session.PeerId, GameAuthResult.InvalidToken, "Please authenticate first");
                    DisconnectPlayer(session.PeerId, "Not authenticated");
                    PacketPool.Return(packet);
                    return;
                }

                // É AuthRequest - processa
                var context = new MessageContext(session.PeerId, session, this);
                _authHandler.Handle(context, packet);
            }
            catch (Exception ex)
            {
                Log.Warning("Failed to parse packet from {PeerId}: {Error}", session.PeerId, ex.Message);
                SendAuthError(session.PeerId, GameAuthResult.InvalidToken, "Invalid message format");
                DisconnectPlayer(session.PeerId, "Invalid packet");
                PacketPool.Return(packet);
            }
        }

        /// <summary>
        /// Processa pacote Data (contém MessageType no payload).
        /// </summary>
        private void HandleDataPacket(PlayerSession session, Packet packet)
        {
            try
            {
                var messageType = PeekMessageType(packet.Payload);

                Log.Debug("Data packet from {Player}: MessageType={Type}", session, messageType);

                // ========================================
                // DISPATCH POR MESSAGETYPE
                // ========================================
                switch (messageType)
                {
                    // ====== PREDICTION/RECONCILIATION ======
                    case MessageType.PlayerInput:
                        HandlePlayerInput(session, packet);
                        break;

                    case MessageType.PlayerInputBatch:
                        HandlePlayerInputBatch(session, packet);
                        break;

                    // ====== OUTROS (futuro) ======
                    // case MessageType.PlayerAttack:
                    //     HandlePlayerAttack(session, packet);
                    //     break;

                    // case MessageType.ChatMessage:
                    //     HandleChatMessage(session, packet);
                    //     break;

                    default:
                        Log.Warning("Unhandled MessageType.{Type} from {Player}", messageType, session);
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Warning("Failed to process Data packet from {Player}: {Error}", session, ex.Message);
            }
            finally
            {
                // SEMPRE retorna packet ao pool
                PacketPool.Return(packet);
            }
        }

        /// <summary>
        /// Extrai MessageType do payload sem deserializar tudo.
        /// </summary>
        private MessageType PeekMessageType(byte[] payload)
        {
            if (payload == null || payload.Length < 2)
                return MessageType.Unknown;

            try
            {
                var reader = new MessagePackReader(payload);

                // MessagePack com [Key(N)] usa ARRAY, não Map!
                int count = reader.ReadArrayHeader();

                if (count > 0)
                {
                    // Primeiro elemento (Key=0) é o MessageType (ushort)
                    var value = reader.ReadUInt16();
                    return (MessageType)value;
                }
            }
            catch (Exception ex)
            {
                Log.Debug("PeekMessageType failed: {Error}", ex.Message);
            }

            return MessageType.Unknown;
        }

        /// <summary>
        /// Envia erro de autenticação.
        /// </summary>
        private void SendAuthError(int peerId, GameAuthResult result, string message)
        {
            var response = GameAuthResponseMessage.CreateFailure(result, message);

            var packet = PacketPool.Rent();
            packet.Type = (byte)PacketType.Data;
            packet.Channel = ChannelType.Reliable;
            packet.Payload = MessagePackSerializer.Serialize(response);
            packet.PayloadSize = packet.Payload.Length;

            SendTo(peerId, packet, DeliveryMethod.ReliableOrdered);
        }



        /// <summary>
        /// Chamado quando ocorre erro no transport.
        /// </summary>
        private void HandleTransportError(string error)
        {
            Log.Error("Transport error: {Error}", error);
        }

        /// <summary>
        /// Processa input único de movimento.
        /// </summary>
        private void HandlePlayerInput(PlayerSession session, Packet packet)
        {
            try
            {
                // Deserializa mensagem
                var message = MessagePackSerializer.Deserialize<PlayerInputMessage>(packet.Payload);

                Log.Debug("PlayerInput from {Player}: {Input}", session, message.Input);

                // Processa via handler
                _inputHandler.HandleMessage(session, message, this);
            }
            catch (Exception ex)
            {
                Log.Warning("Failed to handle PlayerInput from {Player}: {Error}", session, ex.Message);
            }
        }

        /// <summary>
        /// Processa batch de inputs (redundância).
        /// </summary>
        private void HandlePlayerInputBatch(PlayerSession session, Packet packet)
        {
            try
            {
                // Deserializa mensagem
                var batch = MessagePackSerializer.Deserialize<PlayerInputBatchMessage>(packet.Payload);

                Log.Debug("PlayerInputBatch from {Player}: Count={Count}", session, batch.Count);

                // Processa via handler
                _inputHandler.HandleBatchMessage(session, batch, this);
            }
            catch (Exception ex)
            {
                Log.Warning("Failed to handle PlayerInputBatch from {Player}: {Error}", session, ex.Message);
            }
        }


        // ============================================
        // DISPOSE
        // ============================================

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            // Para o servidor se estiver rodando
            if (_isRunning)
            {
                StopAsync().GetAwaiter().GetResult();
            }

            // Remove handlers
            UnregisterTransportEvents();

            // Dispose do CancellationTokenSource
            _cts?.Dispose();

            Log.Debug("GameServer disposed");
        }
    }
}