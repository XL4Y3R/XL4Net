// XL4Net.Server/Transport/LiteNetServerTransport.cs

using LiteNetLib;
using LiteNetLib.Utils;
using MessagePack;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using XL4Net.Shared.Pooling;
using XL4Net.Shared.Protocol.Enums;
using XL4Net.Shared.Transport;

namespace XL4Net.Server.Transport
{
    /// <summary>
    /// Implementação de transport para servidor usando LiteNetLib.
    /// Gerencia múltiplas conexões de clientes simultaneamente.
    /// </summary>
    public class LiteNetServerTransport : IDisposable
    {
        #region Campos Privados

        private NetManager _netManager;
        private EventBasedNetListener _listener;

        // Gerenciamento de clientes conectados
        private readonly ConcurrentDictionary<int, ClientConnection> _clients = new();
        private int _nextClientId = 1;

        // Fila thread-safe para mensagens recebidas
        private readonly ConcurrentQueue<IncomingMessage> _incomingMessages = new();

        // Configuração
        private string _connectionKey = "XL4Net_v1.0";
        private int _port;
        private int _maxClients;
        private int _updateInterval = 15; // ms

        // Controle de estado
        private bool _isRunning;
        private CancellationTokenSource _cts;

        // Estrutura para representar um cliente conectado
        private class ClientConnection
        {
            public int ClientId { get; set; }
            public NetPeer Peer { get; set; }
            public DateTime ConnectedAt { get; set; }
        }

        // Estrutura para mensagens enfileiradas
        private struct IncomingMessage
        {
            public int ClientId;
            public Packet Packet;
            public MessageType Type;
            public string IpAddress;
        }

        private enum MessageType
        {
            ClientConnected,
            ClientDisconnected,
            Data,
            Error
        }

        #endregion

        #region Propriedades

        /// <summary>
        /// Indica se o servidor está rodando.
        /// </summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// Número de clientes conectados atualmente.
        /// </summary>
        public int ConnectedClientsCount => _clients.Count;

        /// <summary>
        /// Lista de IDs dos clientes conectados.
        /// </summary>
        public IEnumerable<int> ConnectedClientIds => _clients.Keys;

        #endregion

        #region Eventos

        /// <summary>
        /// Disparado quando um cliente conecta.
        /// Parâmetros: clientId, ipAddress
        /// </summary>
        public event Action<int, string> OnClientConnected;

        /// <summary>
        /// Disparado quando um cliente desconecta.
        /// Parâmetros: clientId, razão
        /// </summary>
        public event Action<int, string> OnClientDisconnected;

        /// <summary>
        /// Disparado quando recebe um packet de um cliente.
        /// Parâmetros: clientId, packet
        /// IMPORTANTE: Packet deve ser retornado ao pool após uso!
        /// </summary>
        public event Action<int, Packet> OnPacketReceived;

        /// <summary>
        /// Disparado quando ocorre um erro.
        /// Parâmetro: mensagem de erro
        /// </summary>
        public event Action<string> OnError;

        #endregion

        #region Construtor

        /// <summary>
        /// Cria um novo LiteNetServerTransport.
        /// </summary>
        /// <param name="port">Porta para escutar</param>
        /// <param name="maxClients">Número máximo de clientes simultâneos</param>
        public LiteNetServerTransport(int port, int maxClients = 100)
        {
            _port = port;
            _maxClients = maxClients;

            // Setup do listener do LiteNetLib
            _listener = new EventBasedNetListener();

            // Registra callbacks (executam em thread do LiteNetLib)
            _listener.ConnectionRequestEvent += OnLiteNetConnectionRequest;
            _listener.PeerConnectedEvent += OnLiteNetPeerConnected;
            _listener.PeerDisconnectedEvent += OnLiteNetPeerDisconnected;
            _listener.NetworkReceiveEvent += OnLiteNetReceive;
            _listener.NetworkErrorEvent += OnLiteNetError;

            // Cria NetManager
            _netManager = new NetManager(_listener)
            {
                AutoRecycle = true,
                IPv6Enabled = false,
                DisconnectTimeout = 5000,
                MaxConnectAttempts = 5
            };
        }

        #endregion

        #region Métodos Públicos

        /// <summary>
        /// Inicia o servidor na porta especificada.
        /// </summary>
        public async Task<bool> StartAsync()
        {
            if (_isRunning)
            {
                OnError?.Invoke("Server already running");
                return false;
            }

            try
            {
                // Inicia NetManager (bind na porta)
                bool started = _netManager.Start(_port);

                if (!started)
                {
                    OnError?.Invoke($"Failed to bind to port {_port}");
                    return false;
                }

                _isRunning = true;

                // Inicia loop de update do LiteNetLib em background
                _cts = new CancellationTokenSource();
                _ = Task.Run(() => UpdateLoop(_cts.Token));

                // Log sucesso
                Console.WriteLine($"Server started on port {_port} (max clients: {_maxClients})");

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

            try
            {
                Console.WriteLine("Stopping server...");

                // Cancela loop de update
                _cts?.Cancel();

                // Desconecta todos os clientes
                foreach (var client in _clients.Values.ToArray())
                {
                    client.Peer.Disconnect();
                }

                _clients.Clear();

                // Para NetManager
                _netManager.Stop();

                _isRunning = false;

                // Aguarda um pouco para garantir limpeza
                await Task.Delay(100);

                Console.WriteLine("Server stopped");
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Error stopping server: {ex.Message}");
            }
        }

        /// <summary>
        /// Envia um packet para um cliente específico.
        /// IMPORTANTE: Packet é copiado e depois retornado ao pool.
        /// </summary>
        public async Task<bool> SendToClientAsync(int clientId, Packet packet)
        {
            if (!_clients.TryGetValue(clientId, out var client))
            {
                OnError?.Invoke($"Client {clientId} not found");
                PacketPool.Return(packet);
                return false;
            }

            try
            {
                // Usa serialização customizada (NÃO MessagePack)
                var data = packet.Serialize(); // ← CORRIGIDO

                var deliveryMethod = GetDeliveryMethod(packet.Channel);
                client.Peer.Send(data, deliveryMethod);

                PacketPool.Return(packet);
                return true;
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Send to client {clientId} failed: {ex.Message}");
                PacketPool.Return(packet);
                return false;
            }
        }

        /// <summary>
        /// Envia packet para cliente com delivery method específico.
        /// </summary>
        public void SendTo(int clientId, Packet packet, DeliveryMethod delivery)
        {
            if (!_clients.TryGetValue(clientId, out var client))
            {
                return;
            }

            try
            {
                byte[] data = packet.Serialize();
                client.Peer.Send(data, delivery);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending to client {clientId}: {ex.Message}");
            }
        }


        /// <summary>
        /// Envia um packet para todos os clientes conectados.
        /// IMPORTANTE: Packet é copiado e depois retornado ao pool.
        /// </summary>
        public async Task BroadcastAsync(Packet packet, int? excludeClientId = null)
        {
            try
            {
                // Usa serialização customizada (NÃO MessagePack)
                var data = packet.Serialize(); // ← CORRIGIDO
                var deliveryMethod = GetDeliveryMethod(packet.Channel);

                foreach (var client in _clients.Values)
                {
                    if (excludeClientId.HasValue && client.ClientId == excludeClientId.Value)
                        continue;

                    client.Peer.Send(data, deliveryMethod);
                }

                PacketPool.Return(packet);
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Broadcast failed: {ex.Message}");
                PacketPool.Return(packet);
            }
        }

        /// <summary>
        /// Desconecta um cliente específico.
        /// </summary>
        /// <param name="clientId">ID do cliente</param>
        /// <param name="reason">Motivo da desconexão</param>
        public void DisconnectClient(int clientId, string reason = "Disconnected by server")
        {
            if (_clients.TryGetValue(clientId, out var client))
            {
                Console.WriteLine($"Disconnecting client {clientId}: {reason}");

                // NetDataWriter para enviar motivo
                var writer = new NetDataWriter();
                writer.Put(reason);

                client.Peer.Disconnect(writer);
            }
        }

        /// <summary>
        /// Retorna a latência (ping) de um cliente específico.
        /// </summary>
        public int GetClientLatency(int clientId)
        {
            if (_clients.TryGetValue(clientId, out var client))
            {
                return client.Peer.Ping;
            }
            return -1;
        }

        /// <summary>
        /// Alias para SendToClientAsync (compatibilidade com AuthServer).
        /// </summary>
        public async Task<bool> SendToAsync(int clientId, Packet packet)
        {
            return await SendToClientAsync(clientId, packet);
        }

        /// <summary>
        /// Retorna o endereço IP de um cliente específico.
        /// </summary>
        /// <param name="clientId">ID do cliente</param>
        /// <returns>IP address como string (ex: "192.168.1.100") ou null se cliente não encontrado</returns>
        public string? GetClientIP(int clientId)
        {
            if (_clients.TryGetValue(clientId, out var client))
            {
                // NetPeer não possui EndPoint, mas possui propriedade Address herdada de IPEndPoint
                return client.Peer.Address?.ToString(); // return client.Peer.EndPoint?.Address?.ToString();
            }
            return null;
        }

        /// <summary>
        /// Processa mensagens enfileiradas.
        /// DEVE ser chamado no game loop (main thread).
        /// </summary>
        public void ProcessIncoming()
        {
            // Processa até 100 mensagens por frame
            int processed = 0;
            const int maxPerFrame = 100;

            while (processed < maxPerFrame && _incomingMessages.TryDequeue(out var message))
            {
                processed++;

                switch (message.Type)
                {
                    case MessageType.ClientConnected:
                        OnClientConnected?.Invoke(message.ClientId, message.IpAddress);
                        break;

                    case MessageType.ClientDisconnected:
                        // Razão da desconexão está no payload do packet
                        var reason = System.Text.Encoding.UTF8.GetString(message.Packet.Payload);
                        OnClientDisconnected?.Invoke(message.ClientId, reason);
                        PacketPool.Return(message.Packet);
                        break;

                    case MessageType.Data:
                        OnPacketReceived?.Invoke(message.ClientId, message.Packet);
                        // IMPORTANTE: Quem recebe o evento deve retornar ao pool!
                        break;

                    case MessageType.Error:
                        var error = System.Text.Encoding.UTF8.GetString(message.Packet.Payload);
                        OnError?.Invoke(error);
                        PacketPool.Return(message.Packet);
                        break;
                }
            }
        }

        #endregion

        #region Callbacks do LiteNetLib (Thread do LiteNetLib!)

        /// <summary>
        /// Callback quando recebe requisição de conexão (THREAD DO LITENETLIB).
        /// </summary>
        private void OnLiteNetConnectionRequest(ConnectionRequest request)
        {
            try
            {
                Console.WriteLine($"[DEBUG] Connection request from {request.RemoteEndPoint}");

                // Verifica key de conexão
                string receivedKey = request.Data.GetString();
                Console.WriteLine($"[DEBUG] Received key: '{receivedKey}', Expected: '{_connectionKey}'");

                if (receivedKey != _connectionKey)
                {
                    Console.WriteLine($"[DEBUG] Connection rejected: invalid key");
                    request.Reject();
                    return;
                }

                // Verifica se servidor está cheio
                if (_clients.Count >= _maxClients)
                {
                    Console.WriteLine($"[DEBUG] Connection rejected: server full ({_clients.Count}/{_maxClients})");
                    request.Reject();
                    return;
                }

                // Aceita conexão
                Console.WriteLine("[DEBUG] Accepting connection");
                request.Accept();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Error in connection request: {ex}");
                request.Reject();
            }
        }

        /// <summary>
        /// Callback quando cliente conecta (THREAD DO LITENETLIB).
        /// </summary>
        private void OnLiteNetPeerConnected(NetPeer peer)
        {
            try
            {
                // Gera ID único para o cliente
                int clientId = Interlocked.Increment(ref _nextClientId);

                // Pega IP do cliente
                string ipAddress = peer.Address?.ToString() ?? "unknown";

                // Registra cliente
                var client = new ClientConnection
                {
                    ClientId = clientId,
                    Peer = peer,
                    ConnectedAt = DateTime.UtcNow
                };

                _clients.TryAdd(clientId, client);

                // Usa Tag do peer para guardar o clientId
                peer.Tag = clientId;

                Console.WriteLine($"Client {clientId} connected from {ipAddress} (total: {_clients.Count})");

                // Enfileira evento COM IP
                _incomingMessages.Enqueue(new IncomingMessage
                {
                    Type = MessageType.ClientConnected,
                    ClientId = clientId,
                    IpAddress = ipAddress
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in peer connected: {ex.Message}");
            }
        }

        /// <summary>
        /// Callback quando cliente desconecta (THREAD DO LITENETLIB).
        /// </summary>
        private void OnLiteNetPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            try
            {
                // Recupera clientId do Tag
                if (peer.Tag is int clientId)
                {
                    // Remove cliente
                    _clients.TryRemove(clientId, out _);

                    Console.WriteLine($"Client {clientId} disconnected: {disconnectInfo.Reason} (total: {_clients.Count})");

                    // Enfileira evento
                    var packet = PacketPool.Rent();
                    packet.Payload = System.Text.Encoding.UTF8.GetBytes(disconnectInfo.Reason.ToString());

                    _incomingMessages.Enqueue(new IncomingMessage
                    {
                        Type = MessageType.ClientDisconnected,
                        ClientId = clientId,
                        Packet = packet
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in peer disconnected: {ex.Message}");
            }
        }

        /// <summary>
        /// Callback quando recebe dados (THREAD DO LITENETLIB).
        /// </summary>
        private void OnLiteNetReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
        {
            try
            {
                if (peer.Tag is not int clientId)
                    return;

                // Usa deserialização customizada (NÃO MessagePack)
                var packet = PacketPool.Rent();
                packet.Deserialize(reader.GetRemainingBytes()); // ← CORRIGIDO

                _incomingMessages.Enqueue(new IncomingMessage
                {
                    Type = MessageType.Data,
                    ClientId = clientId,
                    Packet = packet
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deserializing packet: {ex.Message}");

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
            Console.WriteLine($"Socket error from {endPoint}: {socketError}");

            var errorPacket = PacketPool.Rent();
            errorPacket.Payload = System.Text.Encoding.UTF8.GetBytes($"Socket error from {endPoint}: {socketError}");

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
            Console.WriteLine("Server update loop started");

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
                    Console.WriteLine($"Error in update loop: {ex.Message}");
                }
            }

            Console.WriteLine("Server update loop stopped");
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
            OnClientConnected = null;
            OnClientDisconnected = null;
            OnPacketReceived = null;
            OnError = null;
        }

        #endregion
    }
}