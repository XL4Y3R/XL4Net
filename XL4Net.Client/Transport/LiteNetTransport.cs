// XL4Net.Client/Transport/LiteNetTransport.cs

using LiteNetLib;
using LiteNetLib.Utils;
using MessagePack;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using XL4Net.Shared.Pooling;
using XL4Net.Shared.Protocol.Enums;
using XL4Net.Shared.Transport;

namespace XL4Net.Client.Transport
{
    /// <summary>
    /// Implementação de ITransport usando LiteNetLib.
    /// Wrapper que adapta LiteNetLib para nossa interface.
    /// </summary>
    public class LiteNetTransport : ITransport
    {
        #region Campos Privados

        private NetManager _netManager;
        private EventBasedNetListener _listener;
        private NetPeer? _serverPeer;

        // Fila thread-safe para mensagens recebidas
        private readonly ConcurrentQueue<IncomingMessage> _incomingMessages = new();

        // Configuração
        private string _connectionKey = "XL4Net_v1.0";
        private int _updateInterval = 15; // ms

        // Controle de estado
        private bool _isRunning;
        private CancellationTokenSource? _cts;

        // Estrutura para mensagens enfileiradas
        private struct IncomingMessage
        {
            public Packet Packet;
            public MessageType Type; // Connected, Disconnected, Data, Error
        }

        private enum MessageType
        {
            Connected,
            Disconnected,
            Data,
            Error
        }

        #endregion

        #region Propriedades (ITransport)

        public bool IsConnected => _serverPeer != null && _serverPeer.ConnectionState == ConnectionState.Connected;

        public int Latency => _serverPeer?.Ping ?? 0;

        #endregion

        #region Eventos (ITransport)

        public event Action? OnConnected;
        public event Action<string>? OnDisconnected;
        public event Action<Packet>? OnPacketReceived;
        public event Action<string>? OnError;

        #endregion

        #region Construtor

        public LiteNetTransport()
        {
            // Setup do listener do LiteNetLib
            _listener = new EventBasedNetListener();

            // Registra callbacks (executam em thread do LiteNetLib)
            _listener.PeerConnectedEvent += OnLiteNetConnected;
            _listener.PeerDisconnectedEvent += OnLiteNetDisconnected;
            _listener.NetworkReceiveEvent += OnLiteNetReceive;
            _listener.NetworkErrorEvent += OnLiteNetError;

            // Cria NetManager
            _netManager = new NetManager(_listener)
            {
                AutoRecycle = true, // LiteNetLib recicla seus próprios packets
                IPv6Enabled = false, // Simplifica por enquanto
                DisconnectTimeout = 5000, // 5 segundos
                ReconnectDelay = 500, // 0.5 segundo
                MaxConnectAttempts = 5
            };
        }

        #endregion

        #region Métodos Públicos (ITransport)

        /// <summary>
        /// Conecta ao servidor.
        /// </summary>
        public async Task<bool> StartAsync()
        {
            if (_isRunning)
            {
                OnError?.Invoke("Transport already running");
                return false;
            }

            try
            {
                // Inicia NetManager (abre socket local)
                _netManager.Start();
                _isRunning = true;

                // Inicia loop de update do LiteNetLib em background
                _cts = new CancellationTokenSource();
                _ = Task.Run(() => UpdateLoop(_cts.Token));

                return true;
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Failed to start transport: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Conecta a um servidor específico.
        /// </summary>       
        public async Task<bool> ConnectAsync(string host, int port)
        {
            if (!_isRunning)
            {
                if (!await StartAsync())
                    return false;
            }

            try
            {
                // Resolve DNS
                IPAddress ipAddress;
                if (!IPAddress.TryParse(host, out ipAddress))
                {
                    var addresses = await Dns.GetHostAddressesAsync(host);

                    // Filtra APENAS IPv4 (IPv6 está desabilitado)
                    var ipv4Addresses = addresses
                        .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
                        .ToArray();

                    if (ipv4Addresses.Length == 0)
                    {
                        OnError?.Invoke($"Failed to resolve host to IPv4: {host}");
                        return false;
                    }

                    ipAddress = ipv4Addresses[0];
                }
                else
                {
                    // Se parseou direto, valida se é IPv4
                    if (ipAddress.AddressFamily != AddressFamily.InterNetwork)
                    {
                        OnError?.Invoke("Only IPv4 addresses are supported");
                        return false;
                    }
                }

                // Conecta usando LiteNetLib (COM NetDataWriter!)
                var writer = new NetDataWriter();
                writer.Put(_connectionKey);

                _serverPeer = _netManager.Connect(new IPEndPoint(ipAddress, port), writer);

                if (_serverPeer == null)
                {
                    OnError?.Invoke("Failed to initiate connection");
                    return false;
                }

                // Aguarda conexão (timeout 5s)
                var timeout = DateTime.UtcNow.AddSeconds(5);
                while (!IsConnected && DateTime.UtcNow < timeout)
                {
                    await Task.Delay(50);
                }

                return IsConnected;
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Connection failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Para o transport e desconecta.
        /// </summary>
        public async Task StopAsync()
        {
            if (!_isRunning)
                return;

            try
            {
                // Cancela loop de update
                _cts?.Cancel();

                // Desconecta
                _serverPeer?.Disconnect();

                // Para NetManager
                _netManager.Stop();

                _isRunning = false;
                _serverPeer = null;

                // Aguarda um pouco para garantir limpeza
                await Task.Delay(100);
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Error stopping transport: {ex.Message}");
            }
        }

        /// <summary>
        /// Envia um packet.
        /// IMPORTANTE: Packet é copiado e depois retornado ao pool.
        /// </summary>
        public async Task<bool> SendAsync(Packet packet)
        {
            if (!IsConnected)
            {
                OnError?.Invoke("Cannot send: not connected");
                return false;
            }

            try
            {
                // Serializa nosso Packet para byte array
                var data = SerializePacket(packet);

                // Determina DeliveryMethod baseado no ChannelType
                var deliveryMethod = GetDeliveryMethod(packet.Channel);

                // Envia via LiteNetLib
                if (_serverPeer != null)
                {
                    _serverPeer.Send(data, deliveryMethod);
                }
                else
                {
                    OnError?.Invoke("Cannot send: server peer is null");
                    PacketPool.Return(packet);
                    return false;
                }

                // Retorna packet ao pool
                PacketPool.Return(packet);

                return true;
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Send failed: {ex.Message}");

                // Retorna ao pool mesmo em caso de erro
                PacketPool.Return(packet);

                return false;
            }
        }

        /// <summary>
        /// Processa mensagens enfileiradas.
        /// DEVE ser chamado no game loop (main thread).
        /// </summary>
        public void ProcessIncoming()
        {
            // Processa até 100 mensagens por frame (evita travar)
            int processed = 0;
            const int maxPerFrame = 100;

            while (processed < maxPerFrame && _incomingMessages.TryDequeue(out var message))
            {
                processed++;

                switch (message.Type)
                {
                    case MessageType.Connected:
                        OnConnected?.Invoke();
                        break;

                    case MessageType.Disconnected:
                        OnDisconnected?.Invoke("Connection lost");
                        break;

                    case MessageType.Data:
                        OnPacketReceived?.Invoke(message.Packet);
                        break;

                    case MessageType.Error:
                        // Mensagem de erro já foi passada no packet.Payload
                        OnError?.Invoke(System.Text.Encoding.UTF8.GetString(message.Packet.Payload));
                        PacketPool.Return(message.Packet);
                        break;
                }
            }
        }

        #endregion

        #region Callbacks do LiteNetLib (Thread do LiteNetLib!)

        /// <summary>
        /// Callback quando conecta (THREAD DO LITENETLIB).
        /// </summary>
        private void OnLiteNetConnected(NetPeer peer)
        {
            // Enfileira para processar no main thread
            _incomingMessages.Enqueue(new IncomingMessage
            {
                Type = MessageType.Connected
            });
        }

        /// <summary>
        /// Callback quando desconecta (THREAD DO LITENETLIB).
        /// </summary>
        private void OnLiteNetDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            // Enfileira para processar no main thread
            _incomingMessages.Enqueue(new IncomingMessage
            {
                Type = MessageType.Disconnected
            });
        }

        /// <summary>
        /// Callback quando recebe dados (THREAD DO LITENETLIB).
        /// </summary>
        private void OnLiteNetReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
        {
            try
            {
                // Desserializa para nosso Packet
                var packet = DeserializePacket(reader.GetRemainingBytes());

                // Enfileira para processar no main thread
                _incomingMessages.Enqueue(new IncomingMessage
                {
                    Type = MessageType.Data,
                    Packet = packet
                });
            }
            catch (Exception ex)
            {
                // Enfileira erro
                var errorPacket = PacketPool.Rent();
                errorPacket.Payload = System.Text.Encoding.UTF8.GetBytes($"Deserialization error: {ex.Message}");

                _incomingMessages.Enqueue(new IncomingMessage
                {
                    Type = MessageType.Error,
                    Packet = errorPacket
                });
            }
        }

        /// <summary>
        /// Callback quando ocorre erro de socket (THREAD DO LITENETLIB).
        /// </summary>
        private void OnLiteNetError(IPEndPoint endPoint, SocketError socketError)
        {
            var errorPacket = PacketPool.Rent();
            errorPacket.Payload = System.Text.Encoding.UTF8.GetBytes($"Socket error: {socketError}");

            _incomingMessages.Enqueue(new IncomingMessage
            {
                Type = MessageType.Error,
                Packet = errorPacket
            });
        }

        #endregion

        #region Métodos Privados

        /// <summary>
        /// Loop de update do LiteNetLib (roda em background thread).
        /// </summary>
        private async Task UpdateLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    // PollEvents processa callbacks do LiteNetLib
                    _netManager.PollEvents();

                    // Aguarda intervalo
                    await Task.Delay(_updateInterval, ct);
                }
                catch (OperationCanceledException)
                {
                    break; // Normal, cancellation requested
                }
                catch (Exception ex)
                {
                    var errorPacket = PacketPool.Rent();
                    errorPacket.Payload = System.Text.Encoding.UTF8.GetBytes($"Update loop error: {ex.Message}");

                    _incomingMessages.Enqueue(new IncomingMessage
                    {
                        Type = MessageType.Error,
                        Packet = errorPacket
                    });
                }
            }
        }

        /// <summary>
        /// Serializa nosso Packet para byte array usando MessagePack.
        /// </summary>
        private byte[] SerializePacket(Packet packet)
        {
            return packet.Serialize();
        }

        /// <summary>
        /// Desserializa byte array para nosso Packet usando MessagePack.
        /// </summary>
        private Packet DeserializePacket(byte[] data)
        {
            var packet = PacketPool.Rent();
            packet.Deserialize(data);
            return packet;
        }

        /// <summary>
        /// Converte nosso ChannelType para DeliveryMethod do LiteNetLib.
        /// </summary>
        private DeliveryMethod GetDeliveryMethod(ChannelType channel)
        {
            return channel switch
            {
                ChannelType.Reliable => DeliveryMethod.ReliableOrdered,
                ChannelType.Unreliable => DeliveryMethod.Unreliable,
                ChannelType.Sequenced => DeliveryMethod.Sequenced,
                _ => DeliveryMethod.ReliableOrdered
            };
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            StopAsync().Wait();

            _netManager?.Stop();
            _cts?.Dispose();

            // Limpa eventos
            OnConnected = null;
            OnDisconnected = null;
            OnPacketReceived = null;
            OnError = null;
        }

        #endregion
    }
}