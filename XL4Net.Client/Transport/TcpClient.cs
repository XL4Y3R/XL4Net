using System;
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
    /// Cliente TCP para XL4Net.
    /// Gerencia conexão com o servidor de forma assíncrona.
    /// </summary>
    public class TcpClient : ITransport
    {
        #region Constantes

        private const int HEARTBEAT_INTERVAL_MS = 1000;  // 1 segundo
        private const int HEARTBEAT_TIMEOUT_MS = 5000;   // 5 segundos
        private const uint MAGIC_NUMBER = 0x584C344E;    // "XL4N"

        #endregion

        #region Campos Privados

        // Configuração
        private string _serverAddress;
        private int _serverPort;

        // Conexão
        private TcpConnection _connection;
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
        /// Cria um novo cliente TCP.
        /// </summary>
        /// <param name="serverAddress">Endereço do servidor (ex: "localhost" ou "192.168.1.100")</param>
        /// <param name="serverPort">Porta do servidor (ex: 7777)</param>
        public TcpClient(string serverAddress, int serverPort)
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

                // 1. Cria TcpClient do .NET
                var tcpClient = new System.Net.Sockets.TcpClient();

                // 2. Configura opções de socket
                tcpClient.NoDelay = true; // Desabilita Nagle (envia imediatamente)

                // 3. Conecta ao servidor (com timeout de 5 segundos)
                var connectTask = tcpClient.ConnectAsync(_serverAddress, _serverPort);
                var timeoutTask = Task.Delay(5000);

                var completedTask = await Task.WhenAny(connectTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    // Timeout!
                    tcpClient.Close();
                    OnError?.Invoke("Connection timeout");
                    return false;
                }

                // 4. Cria wrapper TcpConnection
                _connection = new TcpConnection(tcpClient, 0); // ConnectionId 0 = cliente

                // 5. Registra eventos
                _connection.OnDataReceived += HandleDataReceived;
                _connection.OnDisconnected += HandleDisconnected;

                // 6. Inicia recebimento
                _connection.StartReceiving();

                // 7. Handshake com servidor
                var handshakeSuccess = await PerformHandshakeAsync();
                if (!handshakeSuccess)
                {
                    _connection.Disconnect();
                    return false;
                }

                // 8. Marca como conectado
                _isConnected = true;
                _lastHeartbeatReceived = DateTime.UtcNow;

                // 9. Inicia heartbeat
                StartHeartbeat();

                // 10. Dispara evento
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
            if (!_isConnected)
                return;

            _isConnected = false;

            // Para heartbeat
            _heartbeatTimer?.Dispose();
            _heartbeatTimer = null;

            // Cancela tasks
            _cts?.Cancel();

            // Fecha conexão
            _connection?.Disconnect();
            _connection = null;

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
            if (!_isConnected || _connection == null)
            {
                PacketPool.Return(packet);
                return false;
            }

            try
            {
                // Serializa packet para bytes
                var data = packet.Serialize();

                // Envia pela conexão
                var success = await _connection.SendAsync(data);

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

        #region Handshake

        /// <summary>
        /// Realiza handshake com servidor (SYN/ACK).
        /// </summary>
        private async Task<bool> PerformHandshakeAsync()
        {
            try
            {
                // 1. Cria packet de handshake
                var handshakePacket = PacketPool.Rent();
                handshakePacket.Type = (byte)PacketType.Handshake;

                // Aloca payload do BufferPool
                var magicBytes = BitConverter.GetBytes(MAGIC_NUMBER);
                handshakePacket.Payload = magicBytes;
                handshakePacket.PayloadSize = magicBytes.Length;

                // 2. Envia SYN
                var data = handshakePacket.Serialize();
                await _connection.SendAsync(data);

                // Retorna ao pool
                PacketPool.Return(handshakePacket);

                // 3. Aguarda ACK (timeout 3 segundos)
                var ackReceived = false;
                var startTime = DateTime.UtcNow;

                while (!ackReceived && (DateTime.UtcNow - startTime).TotalSeconds < 3)
                {
                    if (_incomingPackets.TryPeek(out var packet) && packet.Type == (byte)PacketType.HandshakeAck)
                    {
                        _incomingPackets.TryDequeue(out var ackPacket);
                        PacketPool.Return(ackPacket);
                        ackReceived = true;
                    }

                    await Task.Delay(10);
                }

                if (!ackReceived)
                {
                    OnError?.Invoke("Handshake timeout");
                    return false;
                }

                return true;
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
                await StopAsync();
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

            await SendAsync(pingPacket);
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
        /// Chamado quando TcpConnection recebe dados.
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

        /// <summary>
        /// Chamado quando TcpConnection desconecta.
        /// </summary>
        private void HandleDisconnected(int connectionId)
        {
            if (!_isConnected)
                return;

            _isConnected = false;
            OnDisconnected?.Invoke("Connection lost");
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