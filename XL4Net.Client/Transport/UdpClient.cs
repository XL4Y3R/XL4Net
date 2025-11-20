// XL4Net.Client/Transport/UdpClient.cs

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using XL4Net.Shared.Transport;
using XL4Net.Shared.Pooling;
using XL4Net.Shared.Protocol.Enums;

namespace XL4Net.Client.Transport
{
    /// <summary>
    /// Cliente UDP para XL4Net.
    /// Gerencia conexão com o servidor através de UDP (unreliable).
    /// </summary>
    /// <remarks>
    /// Diferenças em relação ao TcpClient:
    /// - UDP não tem conexão real (é stateless)
    /// - Handshake é manual (enviamos ConnectRequest, aguardamos ConnectAccept)
    /// - Um único socket UdpClient para tudo
    /// - Sem garantia de ordem ou entrega (por enquanto)
    /// 
    /// Baseado no LiteNetLib NetManager (client-side).
    /// </remarks>
    public class UdpClient : ITransport
    {
        #region Constantes

        private const int HEARTBEAT_INTERVAL_MS = 1000;  // 1 segundo
        private const int HEARTBEAT_TIMEOUT_MS = 5000;   // 5 segundos
        private const uint MAGIC_NUMBER = 0x584C344E;    // "XL4N"
        private const int CONNECT_TIMEOUT_MS = 3000;     // 3 segundos para handshake

        #endregion

        #region Campos Privados

        // Configuração
        private string _serverAddress;
        private int _serverPort;
        private IPEndPoint _serverEndPoint;

        // Network
        private System.Net.Sockets.UdpClient _udpSocket;
        private UdpPeerConnection _serverConnection;
        private CancellationTokenSource _cts;

        // Heartbeat
        private DateTime _lastHeartbeatReceived;
        private Timer _heartbeatTimer;

        // Filas thread-safe
        private readonly ConcurrentQueue<Packet> _incomingPackets = new ConcurrentQueue<Packet>();

        // Estado
        private bool _isConnected;
        private bool _isDisposed;
        private int _latencyMs;

        // Receive loop
        private Task _receiveTask;

        #endregion

        #region Propriedades ITransport

        public bool IsConnected => _isConnected;
        public int Latency => _latencyMs;

        #endregion

        #region Eventos ITransport

        public event Action OnConnected;
        public event Action<string> OnDisconnected;
        public event Action<Packet> OnPacketReceived;
        public event Action<string> OnError;

        #endregion

        #region Construtor

        /// <summary>
        /// Cria um novo cliente UDP.
        /// </summary>
        /// <param name="serverAddress">Endereço do servidor (ex: "localhost" ou "192.168.1.100")</param>
        /// <param name="serverPort">Porta do servidor (ex: 7778)</param>
        public UdpClient(string serverAddress, int serverPort)
        {
            _serverAddress = serverAddress ?? throw new ArgumentNullException(nameof(serverAddress));
            _serverPort = serverPort;
        }

        #endregion

        #region Métodos ITransport

        /// <summary>
        /// Inicia conexão com o servidor.
        /// </summary>
        public async Task<bool> StartAsync()
        {
            if (_isConnected || _isDisposed)
                return false;

            try
            {
                _cts = new CancellationTokenSource();

                // 1. Resolve endereço do servidor
                var addresses = await Dns.GetHostAddressesAsync(_serverAddress).ConfigureAwait(false);
                if (addresses.Length == 0)
                {
                    OnError?.Invoke("Failed to resolve server address");
                    return false;
                }

                _serverEndPoint = new IPEndPoint(addresses[0], _serverPort);

                // 2. Cria socket UDP (bind em porta local aleatória)
                _udpSocket = new System.Net.Sockets.UdpClient();
                _udpSocket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

                // 3. Inicia receive loop (background thread)
                _receiveTask = Task.Run(() => ReceiveLoopAsync(_cts.Token), _cts.Token);

                // 4. Handshake com servidor
                var handshakeSuccess = await PerformHandshakeAsync().ConfigureAwait(false);
                if (!handshakeSuccess)
                {
                    await StopAsync().ConfigureAwait(false);
                    return false;
                }

                // 5. Cria conexão virtual com servidor
                _serverConnection = new UdpPeerConnection(_udpSocket, _serverEndPoint, 0);

                // 6. Marca como conectado
                _isConnected = true;
                _lastHeartbeatReceived = DateTime.UtcNow;

                // 7. Inicia heartbeat
                StartHeartbeat();

                // 8. Dispara evento
                OnConnected?.Invoke();

                return true;
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Connection failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Para a conexão e limpa recursos.
        /// </summary>
        public async Task StopAsync()
        {
            if (_isDisposed)
                return;

            _isConnected = false;

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

            // Fecha socket
            _udpSocket?.Close();
            _udpSocket?.Dispose();
            _udpSocket = null;

            // Limpa conexão
            _serverConnection?.Dispose();
            _serverConnection = null;

            // Limpa fila
            while (_incomingPackets.TryDequeue(out var packet))
            {
                PacketPool.Return(packet);
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Envia um packet para o servidor.
        /// IMPORTANTE: O packet é retornado ao pool automaticamente.
        /// </summary>
        public async Task<bool> SendAsync(Packet packet)
        {
            if (!_isConnected || _serverConnection == null)
            {
                PacketPool.Return(packet);
                return false;
            }

            try
            {
                // Serializa packet para bytes
                var data = packet.Serialize();

                // Envia pela conexão
                var success = await _serverConnection.SendAsync(data).ConfigureAwait(false);

                // Retorna packet ao pool
                PacketPool.Return(packet);

                return success;
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Send failed: {ex.Message}");
                PacketPool.Return(packet);
                return false;
            }
        }

        /// <summary>
        /// Processa packets recebidos (deve ser chamado no game loop).
        /// </summary>
        public void ProcessIncoming()
        {
            // Processa até 100 packets por frame (evita travar o jogo)
            int processed = 0;
            const int MAX_PER_FRAME = 100;

            while (processed < MAX_PER_FRAME && _incomingPackets.TryDequeue(out var packet))
            {
                try
                {
                    // Dispara evento
                    OnPacketReceived?.Invoke(packet);

                    // IMPORTANTE: Quem recebe o evento é responsável por retornar ao pool
                }
                catch (Exception ex)
                {
                    OnError?.Invoke($"Error processing packet: {ex.Message}");

                    // Se der erro, retorna ao pool aqui
                    PacketPool.Return(packet);
                }

                processed++;
            }
        }

        #endregion

        #region Receive Loop

        /// <summary>
        /// Loop de recebimento de datagramas (roda em background thread).
        /// </summary>
        /// <remarks>
        /// ATENÇÃO: Roda em I/O thread! Não processa mensagens aqui, apenas enfileira.
        /// </remarks>
        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _udpSocket != null)
            {
                try
                {
                    // Recebe datagrama (bloqueia até receber)
                    var result = await _udpSocket.ReceiveAsync().ConfigureAwait(false);

                    // Valida que veio do servidor esperado
                    if (!result.RemoteEndPoint.Equals(_serverEndPoint))
                    {
                        // Ignoramos datagramas de outros endereços (possível ataque)
                        continue;
                    }

                    // Processa dados
                    HandleDataReceived(result.Buffer);
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset)
                {
                    // ICMP Destination Unreachable (servidor offline)
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

        #endregion

        #region Handshake

        /// <summary>
        /// Realiza handshake com servidor (envia ConnectRequest, aguarda ConnectAccept).
        /// </summary>
        private async Task<bool> PerformHandshakeAsync()
        {
            try
            {
                // 1. Cria packet de handshake (ConnectRequest)
                var handshakePacket = PacketPool.Rent();
                handshakePacket.Type = (byte)PacketType.Handshake;

                // Payload: magic number + timestamp
                var magicBytes = BitConverter.GetBytes(MAGIC_NUMBER);
                var timestampBytes = BitConverter.GetBytes(DateTime.UtcNow.Ticks);
                var payload = new byte[magicBytes.Length + timestampBytes.Length];
                Buffer.BlockCopy(magicBytes, 0, payload, 0, magicBytes.Length);
                Buffer.BlockCopy(timestampBytes, 0, payload, magicBytes.Length, timestampBytes.Length);

                handshakePacket.Payload = payload;
                handshakePacket.PayloadSize = payload.Length;

                // 2. Serializa e envia
                var data = handshakePacket.Serialize();
                await _udpSocket.SendAsync(data, data.Length, _serverEndPoint).ConfigureAwait(false);

                // Retorna ao pool
                PacketPool.Return(handshakePacket);

                // 3. Aguarda ConnectAccept (timeout 3 segundos)
                var startTime = DateTime.UtcNow;
                var timeout = TimeSpan.FromMilliseconds(CONNECT_TIMEOUT_MS);

                while ((DateTime.UtcNow - startTime) < timeout)
                {
                    if (_incomingPackets.TryPeek(out var packet) && packet.Type == (byte)PacketType.HandshakeAck)
                    {
                        // Recebeu ACK!
                        _incomingPackets.TryDequeue(out var ackPacket);
                        PacketPool.Return(ackPacket);
                        return true;
                    }

                    await Task.Delay(10).ConfigureAwait(false);
                }

                // Timeout!
                OnError?.Invoke("Handshake timeout");
                return false;
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Handshake failed: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Heartbeat

        /// <summary>
        /// Inicia sistema de heartbeat (mantém conexão viva).
        /// </summary>
        private void StartHeartbeat()
        {
            _heartbeatTimer = new Timer(
                callback: HeartbeatTick,
                state: null,
                dueTime: HEARTBEAT_INTERVAL_MS,
                period: HEARTBEAT_INTERVAL_MS
            );
        }

        /// <summary>
        /// Tick do heartbeat (roda a cada 1 segundo).
        /// </summary>
        private async void HeartbeatTick(object state)
        {
            if (!_isConnected)
                return;

            // 1. Verifica timeout (sem resposta por 5 segundos)
            var timeSinceLastHeartbeat = DateTime.UtcNow - _lastHeartbeatReceived;
            if (timeSinceLastHeartbeat.TotalMilliseconds > HEARTBEAT_TIMEOUT_MS)
            {
                // Timeout! Desconecta
                OnError?.Invoke("Heartbeat timeout");
                await StopAsync().ConfigureAwait(false);
                OnDisconnected?.Invoke("Heartbeat timeout");
                return;
            }

            // 2. Envia ping
            var pingPacket = PacketPool.Rent();
            pingPacket.Type = (byte)PacketType.Ping;

            // Coloca timestamp no payload
            var timestampBytes = BitConverter.GetBytes(DateTime.UtcNow.Ticks);
            pingPacket.Payload = timestampBytes;
            pingPacket.PayloadSize = timestampBytes.Length;

            await SendAsync(pingPacket).ConfigureAwait(false);
            // Nota: SendAsync já retorna ao pool
        }

        /// <summary>
        /// Processa resposta de pong (calcula latência).
        /// </summary>
        private void HandlePong(Packet pongPacket)
        {
            _lastHeartbeatReceived = DateTime.UtcNow;

            // Calcula latência (RTT) - lê timestamp do Payload
            if (pongPacket.PayloadSize >= 8 && pongPacket.Payload != null)
            {
                var sentTicks = BitConverter.ToInt64(pongPacket.Payload, 0);
                var sentTime = new DateTime(sentTicks);
                var rtt = (DateTime.UtcNow - sentTime).TotalMilliseconds;
                _latencyMs = (int)rtt;
            }
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Chamado quando receive loop recebe dados.
        /// ATENÇÃO: Roda em background thread (I/O)!
        /// </summary>
        private void HandleDataReceived(byte[] data)
        {
            try
            {
                // 1. Deserializa bytes para Packet
                var packet = PacketPool.Rent();
                packet.Deserialize(data);

                // 2. Trata packets especiais (heartbeat)
                if (packet.Type == (byte)PacketType.Pong)
                {
                    HandlePong(packet);
                    PacketPool.Return(packet);
                    return;
                }

                // 3. Enfileira para main thread processar
                _incomingPackets.Enqueue(packet);
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Failed to handle data: {ex.Message}");
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
    }
}