using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using XL4Net.Shared.Transport;
using XL4Net.Shared.Pooling;
using XL4Net.Shared.Protocol.Enums;

namespace XL4Net.Server.Transport
{
    /// <summary>
    /// Servidor TCP para XL4Net.
    /// Gerencia múltiplos clientes conectados simultaneamente.
    /// </summary>
    public class TcpServer : ITransport
    {
        #region Constantes

        private const int HEARTBEAT_CHECK_INTERVAL_MS = 1000;  // 1 segundo
        private const int HEARTBEAT_TIMEOUT_MS = 5000;         // 5 segundos
        private const uint MAGIC_NUMBER = 0x584C344E;          // "XL4N"

        #endregion

        #region Campos Privados

        // Configuração
        private readonly int _port;
        private readonly int _maxClients;

        // Network
        private TcpListener _listener;
        private CancellationTokenSource _cts;

        // Clientes conectados (thread-safe)
        private readonly ConcurrentDictionary<int, ClientConnection> _clients = new();
        private int _nextConnectionId = 1000; // Começa em 1000

        // Filas thread-safe
        private readonly ConcurrentQueue<(int clientId, Packet packet)> _incomingPackets = new();

        // Heartbeat
        private Timer _heartbeatTimer;

        // Estado
        private bool _isRunning;
        private bool _isDisposed;

        #endregion

        #region Propriedades ITransport

        public bool IsConnected => _isRunning;
        public int Latency => 0; // Server não tem latência própria

        #endregion

        #region Eventos ITransport

        public event Action OnConnected;
        public event Action<string> OnDisconnected;
        public event Action<Packet> OnPacketReceived;
        public event Action<string> OnError;

        #endregion

        #region Eventos Específicos do Servidor

        /// <summary>
        /// Disparado quando um cliente conecta.
        /// Parâmetro: connectionId do novo cliente
        /// </summary>
        public event Action<int> OnClientConnected;

        /// <summary>
        /// Disparado quando um cliente desconecta.
        /// Parâmetros: connectionId, razão
        /// </summary>
        public event Action<int, string> OnClientDisconnected;

        #endregion

        #region Construtor

        /// <summary>
        /// Cria um novo servidor TCP.
        /// </summary>
        /// <param name="port">Porta para escutar (ex: 7777)</param>
        /// <param name="maxClients">Máximo de clientes simultâneos</param>
        public TcpServer(int port, int maxClients = 100)
        {
            _port = port;
            _maxClients = maxClients;
        }

        #endregion

        #region Métodos ITransport

        /// <summary>
        /// Inicia o servidor (começa a escutar conexões).
        /// </summary>
        public async Task<bool> StartAsync()
        {
            if (_isRunning || _isDisposed)
                return false;

            try
            {
                _cts = new CancellationTokenSource();

                // 1. Cria TcpListener
                _listener = new TcpListener(IPAddress.Any, _port);

                // 2. Inicia escuta
                _listener.Start(_maxClients);

                // 3. Marca como rodando
                _isRunning = true;

                // 4. Inicia thread de accept
                _ = Task.Run(() => AcceptLoopAsync(_cts.Token));

                // 5. Inicia heartbeat checker
                StartHeartbeatChecker();

                // 6. Dispara evento
                OnConnected?.Invoke();

                Console.WriteLine($"[TcpServer] Listening on port {_port}");

                return true;
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Failed to start server: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Para o servidor e desconecta todos os clientes.
        /// </summary>
        public async Task StopAsync()
        {
            if (!_isRunning)
                return;

            _isRunning = false;

            // Para heartbeat
            _heartbeatTimer?.Dispose();
            _heartbeatTimer = null;

            // Cancela accept loop
            _cts?.Cancel();

            // Desconecta todos os clientes
            foreach (var client in _clients.Values)
            {
                client.Connection.Disconnect();
            }
            _clients.Clear();

            // Para listener
            _listener?.Stop();
            _listener = null;

            // Limpa fila
            while (_incomingPackets.TryDequeue(out var item))
            {
                PacketPool.Return(item.packet);
            }

            Console.WriteLine("[TcpServer] Stopped");

            await Task.CompletedTask;
        }

        /// <summary>
        /// Envia um packet para um cliente específico.
        /// </summary>
        public async Task<bool> SendAsync(Packet packet)
        {
            // ITransport não especifica cliente, então não usamos este método
            // Use SendToClient() ou Broadcast()
            PacketPool.Return(packet);
            return false;
        }

        /// <summary>
        /// Processa packets recebidos (deve ser chamado no game loop).
        /// </summary>
        public void ProcessIncoming()
        {
            // Processa até 100 packets por frame
            int processed = 0;
            const int MAX_PER_FRAME = 100;

            while (processed < MAX_PER_FRAME && _incomingPackets.TryDequeue(out var item))
            {
                try
                {
                    // Dispara evento genérico
                    OnPacketReceived?.Invoke(item.packet);

                    // IMPORTANTE: Quem recebe o evento é responsável por retornar ao pool
                }
                catch (Exception ex)
                {
                    OnError?.Invoke($"Error processing packet: {ex.Message}");
                    PacketPool.Return(item.packet);
                }

                processed++;
            }
        }

        #endregion

        #region Métodos Específicos do Servidor

        /// <summary>
        /// Envia um packet para um cliente específico.
        /// </summary>
        public async Task<bool> SendToClientAsync(int clientId, Packet packet)
        {
            if (!_clients.TryGetValue(clientId, out var client))
            {
                PacketPool.Return(packet);
                return false;
            }

            try
            {
                var data = packet.Serialize();
                var success = await client.Connection.SendAsync(data);

                PacketPool.Return(packet);
                return success;
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Send to client {clientId} failed: {ex.Message}");
                PacketPool.Return(packet);
                return false;
            }
        }

        /// <summary>
        /// Envia um packet para todos os clientes conectados.
        /// </summary>
        public async Task BroadcastAsync(Packet packet)
        {
            if (_clients.IsEmpty)
            {
                PacketPool.Return(packet);
                return;
            }

            // Serializa uma vez só
            var data = packet.Serialize();

            // Envia para todos
            var tasks = new List<Task>();
            foreach (var client in _clients.Values)
            {
                tasks.Add(client.Connection.SendAsync(data));
            }

            await Task.WhenAll(tasks);

            PacketPool.Return(packet);
        }

        /// <summary>
        /// Envia um packet para todos EXCETO um cliente específico.
        /// </summary>
        public async Task BroadcastExceptAsync(int exceptClientId, Packet packet)
        {
            var data = packet.Serialize();

            var tasks = new List<Task>();
            foreach (var kvp in _clients)
            {
                if (kvp.Key != exceptClientId)
                {
                    tasks.Add(kvp.Value.Connection.SendAsync(data));
                }
            }

            await Task.WhenAll(tasks);

            PacketPool.Return(packet);
        }

        /// <summary>
        /// Desconecta um cliente específico.
        /// </summary>
        public void DisconnectClient(int clientId, string reason)
        {
            if (_clients.TryRemove(clientId, out var client))
            {
                client.Connection.Disconnect();
                OnClientDisconnected?.Invoke(clientId, reason);
                Console.WriteLine($"[TcpServer] Client {clientId} disconnected: {reason}");
            }
        }

        /// <summary>
        /// Retorna número de clientes conectados.
        /// </summary>
        public int GetClientCount() => _clients.Count;

        #endregion

        #region Accept Loop

        /// <summary>
        /// Loop de accept (roda em background thread).
        /// Aceita novas conexões continuamente.
        /// </summary>
        private async Task AcceptLoopAsync(CancellationToken ct)
        {
            Console.WriteLine("[TcpServer] Accept loop started");

            while (!ct.IsCancellationRequested && _isRunning)
            {
                try
                {
                    // 1. Aguarda nova conexão
                    var tcpClient = await _listener.AcceptTcpClientAsync();

                    // 2. Verifica limite de clientes
                    if (_clients.Count >= _maxClients)
                    {
                        Console.WriteLine($"[TcpServer] Server full, rejecting connection");
                        tcpClient.Close();
                        continue;
                    }

                    // 3. Gera ID único
                    var connectionId = Interlocked.Increment(ref _nextConnectionId);

                    // 4. Cria wrapper
                    var connection = new TcpConnection(tcpClient, connectionId);

                    // 5. Registra eventos
                    connection.OnDataReceived += data => HandleClientData(connectionId, data);
                    connection.OnDisconnected += _ => HandleClientDisconnected(connectionId);

                    // 6. Cria ClientConnection
                    var clientConn = new ClientConnection
                    {
                        ConnectionId = connectionId,
                        Connection = connection,
                        LastHeartbeat = DateTime.UtcNow
                    };

                    // 7. Adiciona ao dicionário
                    _clients.TryAdd(connectionId, clientConn);

                    // 8. Inicia recebimento
                    connection.StartReceiving();

                    // 9. Aguarda handshake (em background)
                    _ = Task.Run(() => WaitForHandshakeAsync(connectionId));

                    Console.WriteLine($"[TcpServer] Client {connectionId} connected from {connection.RemoteEndPoint}");
                }
                catch (OperationCanceledException)
                {
                    // Cancellation é esperado
                    break;
                }
                catch (Exception ex)
                {
                    OnError?.Invoke($"Accept error: {ex.Message}");
                }
            }

            Console.WriteLine("[TcpServer] Accept loop stopped");
        }

        #endregion

        #region Handshake

        /// <summary>
        /// Aguarda handshake do cliente (timeout 5 segundos).
        /// </summary>
        private async Task WaitForHandshakeAsync(int clientId)
        {
            var startTime = DateTime.UtcNow;
            var timeout = TimeSpan.FromSeconds(5);

            while ((DateTime.UtcNow - startTime) < timeout)
            {
                if (_clients.TryGetValue(clientId, out var client))
                {
                    if (client.HandshakeCompleted)
                    {
                        // Handshake OK!
                        OnClientConnected?.Invoke(clientId);
                        return;
                    }
                }
                else
                {
                    // Cliente já desconectou
                    return;
                }

                await Task.Delay(100);
            }

            // Timeout! Desconecta
            Console.WriteLine($"[TcpServer] Client {clientId} handshake timeout");
            DisconnectClient(clientId, "Handshake timeout");
        }

        /// <summary>
        /// Processa handshake recebido do cliente.
        /// </summary>
        private async Task HandleHandshake(int clientId, Packet packet)
        {
            try
            {
                // 1. Valida magic number
                if (packet.PayloadSize >= 4 && packet.Payload != null)
                {
                    var magic = BitConverter.ToUInt32(packet.Payload, 0);

                    if (magic != MAGIC_NUMBER)
                    {
                        Console.WriteLine($"[TcpServer] Invalid magic number from client {clientId}");
                        DisconnectClient(clientId, "Invalid protocol");
                        return;
                    }
                }
                else
                {
                    DisconnectClient(clientId, "Invalid handshake");
                    return;
                }

                // 2. Marca handshake como completo
                if (_clients.TryGetValue(clientId, out var client))
                {
                    client.HandshakeCompleted = true;
                }

                // 3. Envia ACK
                var ackPacket = PacketPool.Rent();
                ackPacket.Type = (byte)PacketType.HandshakeAck;

                await SendToClientAsync(clientId, ackPacket);

                Console.WriteLine($"[TcpServer] Handshake completed for client {clientId}");
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Handshake error for client {clientId}: {ex.Message}");
                DisconnectClient(clientId, "Handshake error");
            }
        }

        #endregion

        #region Heartbeat

        /// <summary>
        /// Inicia checker de heartbeat (valida a cada 1 segundo).
        /// </summary>
        private void StartHeartbeatChecker()
        {
            _heartbeatTimer = new Timer(
                callback: HeartbeatCheckTick,
                state: null,
                dueTime: HEARTBEAT_CHECK_INTERVAL_MS,
                period: HEARTBEAT_CHECK_INTERVAL_MS
            );
        }

        /// <summary>
        /// Tick do heartbeat checker.
        /// </summary>
        private void HeartbeatCheckTick(object state)
        {
            if (!_isRunning)
                return;

            var now = DateTime.UtcNow;
            var disconnectList = new List<int>();

            // Verifica timeout de cada cliente
            foreach (var kvp in _clients)
            {
                var timeSinceLastHeartbeat = now - kvp.Value.LastHeartbeat;

                if (timeSinceLastHeartbeat.TotalMilliseconds > HEARTBEAT_TIMEOUT_MS)
                {
                    disconnectList.Add(kvp.Key);
                }
            }

            // Desconecta clientes com timeout
            foreach (var clientId in disconnectList)
            {
                Console.WriteLine($"[TcpServer] Client {clientId} heartbeat timeout");
                DisconnectClient(clientId, "Heartbeat timeout");
            }
        }

        /// <summary>
        /// Processa ping recebido do cliente.
        /// </summary>
        private async Task HandlePing(int clientId, Packet pingPacket)
        {
            // Atualiza timestamp
            if (_clients.TryGetValue(clientId, out var client))
            {
                client.LastHeartbeat = DateTime.UtcNow;
            }

            // Responde com pong (echo do timestamp)
            var pongPacket = PacketPool.Rent();
            pongPacket.Type = (byte)PacketType.Pong;
            pongPacket.Payload = pingPacket.Payload;
            pongPacket.PayloadSize = pingPacket.PayloadSize;

            await SendToClientAsync(clientId, pongPacket);
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Chamado quando TcpConnection recebe dados.
        /// ATENÇÃO: Roda em background thread!
        /// </summary>
        private void HandleClientData(int clientId, byte[] data)
        {
            try
            {
                // 1. Deserializa
                var packet = PacketPool.Rent();
                packet.Deserialize(data);

                // 2. Trata packets de controle
                if (packet.Type == (byte)PacketType.Handshake)
                {
                    _ = HandleHandshake(clientId, packet);
                    PacketPool.Return(packet);
                    return;
                }

                if (packet.Type == (byte)PacketType.Ping)
                {
                    _ = HandlePing(clientId, packet);
                    PacketPool.Return(packet);
                    return;
                }

                // 3. Enfileira para main thread processar
                _incomingPackets.Enqueue((clientId, packet));
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Failed to handle data from client {clientId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Chamado quando um cliente desconecta.
        /// </summary>
        private void HandleClientDisconnected(int clientId)
        {
            if (_clients.TryRemove(clientId, out _))
            {
                OnClientDisconnected?.Invoke(clientId, "Connection lost");
                Console.WriteLine($"[TcpServer] Client {clientId} disconnected");
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            StopAsync().Wait();
            _cts?.Dispose();
        }

        #endregion

        #region Classes Internas

        /// <summary>
        /// Representa um cliente conectado.
        /// </summary>
        private class ClientConnection
        {
            public int ConnectionId { get; set; }
            public TcpConnection Connection { get; set; }
            public DateTime LastHeartbeat { get; set; }
            public bool HandshakeCompleted { get; set; }
        }

        #endregion
    }
}