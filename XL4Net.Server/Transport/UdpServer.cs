// XL4Net.Server/Transport/UdpServer.cs

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
    /// Servidor UDP para XL4Net.
    /// Gerencia múltiplos clientes através de "conexões virtuais" UDP.
    /// </summary>
    /// <remarks>
    /// Diferenças em relação ao TcpServer:
    /// - UDP não tem accept() - recebemos datagramas de qualquer lugar
    /// - Identificamos clientes por IPEndPoint (IP:porta)
    /// - Um único socket UdpClient para TODOS os clientes
    /// - Handshake manual (detectamos novos clientes via ConnectRequest)
    /// 
    /// Baseado no LiteNetLib NetManager (server-side).
    /// </remarks>
    public class UdpServer : ITransport
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
        private System.Net.Sockets.UdpClient _udpSocket;
        private CancellationTokenSource _cts;

        // Clientes conectados (mapeamento: IPEndPoint -> ClientData)
        private readonly ConcurrentDictionary<string, ClientData> _clients = new();
        private int _nextConnectionId = 1000; // Começa em 1000

        // Filas thread-safe
        private readonly ConcurrentQueue<(int clientId, Packet packet)> _incomingPackets = new();

        // Heartbeat
        private Timer _heartbeatTimer;

        // Estado
        private bool _isRunning;
        private bool _isDisposed;

        // Receive loop
        private Task _receiveTask;

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
        /// Disparado quando um cliente conecta (após handshake).
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
        /// Cria um novo servidor UDP.
        /// </summary>
        /// <param name="port">Porta para escutar (ex: 7778)</param>
        /// <param name="maxClients">Máximo de clientes simultâneos</param>
        public UdpServer(int port, int maxClients = 100)
        {
            _port = port;
            _maxClients = maxClients;
        }

        #endregion

        #region Métodos ITransport

        /// <summary>
        /// Inicia o servidor (começa a escutar datagramas).
        /// </summary>
        public async Task<bool> StartAsync()
        {
            if (_isRunning || _isDisposed)
                return false;

            try
            {
                _cts = new CancellationTokenSource();

                // 1. Cria socket UDP (bind na porta especificada)
                _udpSocket = new System.Net.Sockets.UdpClient(_port);
                _udpSocket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

                // 2. Marca como rodando
                _isRunning = true;

                // 3. Inicia receive loop (background thread)
                _receiveTask = Task.Run(() => ReceiveLoopAsync(_cts.Token), _cts.Token);

                // 4. Inicia heartbeat checker
                StartHeartbeatChecker();

                // 5. Dispara evento
                OnConnected?.Invoke();

                Console.WriteLine($"[UdpServer] Listening on UDP port {_port}");

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

            // Cancela receive loop
            _cts?.Cancel();

            // Aguarda receive loop terminar
            if (_receiveTask != null)
            {
                try
                {
                    await _receiveTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Esperado
                }
            }

            // Desconecta todos os clientes
            foreach (var client in _clients.Values)
            {
                client.Connection.Disconnect();
            }
            _clients.Clear();

            // Fecha socket
            _udpSocket?.Close();
            _udpSocket?.Dispose();
            _udpSocket = null;

            // Limpa fila
            while (_incomingPackets.TryDequeue(out var item))
            {
                PacketPool.Return(item.packet);
            }

            Console.WriteLine("[UdpServer] Stopped");

            await Task.CompletedTask;
        }

        /// <summary>
        /// Envia um packet para um cliente específico.
        /// </summary>
        public async Task<bool> SendAsync(Packet packet)
        {
            // ITransport não especifica cliente, então não usamos este método
            // Use SendToClientAsync() ou BroadcastAsync()
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
            // Procura cliente
            var clientData = FindClientById(clientId);
            if (clientData == null)
            {
                PacketPool.Return(packet);
                return false;
            }

            try
            {
                var data = packet.Serialize();
                var success = await clientData.Connection.SendAsync(data).ConfigureAwait(false);

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

            try
            {
                // Serializa uma vez (reutiliza para todos)
                var data = packet.Serialize();

                // Envia para cada cliente
                foreach (var client in _clients.Values)
                {
                    if (client.HandshakeCompleted)
                    {
                        await client.Connection.SendAsync(data).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Broadcast failed: {ex.Message}");
            }
            finally
            {
                PacketPool.Return(packet);
            }
        }

        /// <summary>
        /// Desconecta um cliente específico.
        /// </summary>
        public void DisconnectClient(int clientId, string reason)
        {
            var clientData = FindClientById(clientId);
            if (clientData == null)
                return;

            // Remove do dicionário
            var key = clientData.EndPointKey;
            if (_clients.TryRemove(key, out _))
            {
                clientData.Connection.Disconnect();
                OnClientDisconnected?.Invoke(clientId, reason);
                Console.WriteLine($"[UdpServer] Client {clientId} ({clientData.Connection.RemoteEndPoint}) disconnected: {reason}");
            }
        }

        /// <summary>
        /// Obtém número de clientes conectados.
        /// </summary>
        public int GetClientCount()
        {
            return _clients.Count;
        }

        #endregion

        #region Receive Loop

        /// <summary>
        /// Loop de recebimento de datagramas (roda em background thread).
        /// </summary>
        /// <remarks>
        /// ATENÇÃO: Roda em I/O thread! Não processa mensagens aqui, apenas enfileira.
        /// 
        /// Este é o "coração" do servidor UDP:
        /// - Recebe datagramas de QUALQUER endereço
        /// - Identifica cliente por IPEndPoint
        /// - Se é novo cliente, cria conexão virtual
        /// - Se é cliente existente, roteia para ele
        /// </remarks>
        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _udpSocket != null)
            {
                try
                {
                    // Recebe datagrama (bloqueia até receber)
                    var result = await _udpSocket.ReceiveAsync().ConfigureAwait(false);

                    // Processa dados de acordo com o endpoint
                    HandleDatagramReceived(result.RemoteEndPoint, result.Buffer);
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset)
                {
                    // ICMP Destination Unreachable (cliente offline)
                    // Ignoramos, heartbeat vai detectar timeout
                }
                catch (ObjectDisposedException)
                {
                    // Socket foi fechado (esperado ao parar)
                    break;
                }
                catch (OperationCanceledException)
                {
                    // Cancellation (esperado ao parar)
                    break;
                }
                catch (Exception ex)
                {
                    OnError?.Invoke($"Receive error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Processa datagrama recebido de um endpoint específico.
        /// </summary>
        private void HandleDatagramReceived(IPEndPoint remoteEndPoint, byte[] data)
        {
            try
            {
                // Gera chave única para este endpoint
                var endPointKey = GetEndPointKey(remoteEndPoint);

                // Deserializa packet
                var packet = PacketPool.Rent();
                packet.Deserialize(data);

                // Verifica se é handshake de novo cliente
                if (packet.Type == (byte)PacketType.Handshake)
                {
                    HandleNewClientHandshake(remoteEndPoint, endPointKey, packet);
                    PacketPool.Return(packet);
                    return;
                }

                // Procura cliente existente
                if (!_clients.TryGetValue(endPointKey, out var clientData))
                {
                    // Cliente desconhecido e não é handshake - ignora
                    PacketPool.Return(packet);
                    return;
                }

                // Cliente conhecido - processa packet
                HandleClientData(clientData.ConnectionId, packet);
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Failed to handle datagram from {remoteEndPoint}: {ex.Message}");
            }
        }

        #endregion

        #region Handshake

        /// <summary>
        /// Processa handshake de novo cliente.
        /// </summary>
        private async void HandleNewClientHandshake(IPEndPoint remoteEndPoint, string endPointKey, Packet packet)
        {
            try
            {
                // 1. Verifica se servidor está cheio
                if (_clients.Count >= _maxClients)
                {
                    Console.WriteLine($"[UdpServer] Rejecting client {remoteEndPoint}: server full");
                    // TODO: Enviar reject packet (fase futura)
                    return;
                }

                // 2. Valida magic number
                if (packet.PayloadSize < 4 || packet.Payload == null)
                {
                    Console.WriteLine($"[UdpServer] Invalid handshake from {remoteEndPoint}: no payload");
                    return;
                }

                var magic = BitConverter.ToUInt32(packet.Payload, 0);
                if (magic != MAGIC_NUMBER)
                {
                    Console.WriteLine($"[UdpServer] Invalid magic number from {remoteEndPoint}");
                    return;
                }

                // 3. Gera connectionId único
                var connectionId = Interlocked.Increment(ref _nextConnectionId);

                // 4. Cria conexão virtual
                var connection = new UdpPeerConnection(_udpSocket, remoteEndPoint, connectionId);

                // 5. Registra cliente
                var clientData = new ClientData
                {
                    ConnectionId = connectionId,
                    Connection = connection,
                    EndPointKey = endPointKey,
                    LastHeartbeat = DateTime.UtcNow,
                    HandshakeCompleted = true
                };

                if (!_clients.TryAdd(endPointKey, clientData))
                {
                    // Já existe (race condition?) - usa o existente
                    connection.Dispose();
                    return;
                }

                // 6. Registra evento de disconnect
                connection.OnDisconnected += HandleClientDisconnected;

                // 7. Envia HandshakeAck
                var ackPacket = PacketPool.Rent();
                ackPacket.Type = (byte)PacketType.HandshakeAck;
                await SendToClientAsync(connectionId, ackPacket).ConfigureAwait(false);

                // 8. Dispara evento
                OnClientConnected?.Invoke(connectionId);

                Console.WriteLine($"[UdpServer] Client {connectionId} connected from {remoteEndPoint}");
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Handshake error for {remoteEndPoint}: {ex.Message}");
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
                    disconnectList.Add(kvp.Value.ConnectionId);
                }
            }

            // Desconecta clientes com timeout
            foreach (var clientId in disconnectList)
            {
                Console.WriteLine($"[UdpServer] Client {clientId} heartbeat timeout");
                DisconnectClient(clientId, "Heartbeat timeout");
            }
        }

        /// <summary>
        /// Processa ping recebido do cliente.
        /// </summary>
        private async Task HandlePing(int clientId, Packet pingPacket)
        {
            // Atualiza timestamp
            var clientData = FindClientById(clientId);
            if (clientData != null)
            {
                clientData.LastHeartbeat = DateTime.UtcNow;
            }

            // Responde com pong (echo do timestamp)
            var pongPacket = PacketPool.Rent();
            pongPacket.Type = (byte)PacketType.Pong;
            pongPacket.Payload = pingPacket.Payload;
            pongPacket.PayloadSize = pingPacket.PayloadSize;

            await SendToClientAsync(clientId, pongPacket).ConfigureAwait(false);
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Processa dados de cliente conhecido.
        /// ATENÇÃO: Roda em background thread!
        /// </summary>
        private void HandleClientData(int clientId, Packet packet)
        {
            try
            {
                // Trata packets de controle
                if (packet.Type == (byte)PacketType.Ping)
                {
                    _ = HandlePing(clientId, packet);
                    PacketPool.Return(packet);
                    return;
                }

                // Enfileira para main thread processar
                _incomingPackets.Enqueue((clientId, packet));
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Failed to handle data from client {clientId}: {ex.Message}");
                PacketPool.Return(packet);
            }
        }

        /// <summary>
        /// Chamado quando um cliente desconecta.
        /// </summary>
        private void HandleClientDisconnected(int connectionId)
        {
            DisconnectClient(connectionId, "Connection lost");
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Gera chave única para IPEndPoint (usado como dicionário key).
        /// </summary>
        private string GetEndPointKey(IPEndPoint endPoint)
        {
            return $"{endPoint.Address}:{endPoint.Port}";
        }

        /// <summary>
        /// Procura cliente por connectionId (busca linear - OK para <1000 clientes).
        /// </summary>
        private ClientData FindClientById(int connectionId)
        {
            foreach (var client in _clients.Values)
            {
                if (client.ConnectionId == connectionId)
                    return client;
            }
            return null;
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
        /// Dados de um cliente conectado.
        /// </summary>
        private class ClientData
        {
            public int ConnectionId { get; set; }
            public UdpPeerConnection Connection { get; set; }
            public string EndPointKey { get; set; }
            public DateTime LastHeartbeat { get; set; }
            public bool HandshakeCompleted { get; set; }
        }

        #endregion
    }
}