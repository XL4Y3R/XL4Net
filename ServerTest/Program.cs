using System;
using System.Text;
using System.Threading.Tasks;
using XL4Net.Server.Transport;
using XL4Net.Shared.Pooling;
using XL4Net.Shared.Protocol.Enums;
using XL4Net.Shared.Transport;

namespace ServerTest
{
    class Program
    {
        // Instância global do servidor (para acessar nos callbacks)
        private static LiteNetServerTransport _server;

        static async Task Main(string[] args)
        {
            Console.WriteLine("=== XL4Net Server Test ===");
            Console.WriteLine();

            // Cria servidor
            _server = new LiteNetServerTransport(port: 7777, maxClients: 10);

            // Registra eventos
            _server.OnClientConnected += OnClientConnected;
            _server.OnClientDisconnected += OnClientDisconnected;
            _server.OnPacketReceived += OnPacketReceived;
            _server.OnError += OnError;

            // Inicia servidor
            Console.WriteLine("Starting server on port 7777...");
            bool started = await _server.StartAsync();

            if (!started)
            {
                Console.WriteLine("Failed to start server!");
                return;
            }

            Console.WriteLine("Server started successfully!");
            Console.WriteLine("Waiting for clients... (Press 'Q' to quit)");
            Console.WriteLine();

            // Game loop (30 Hz)
            var lastUpdate = DateTime.UtcNow;
            var tickInterval = TimeSpan.FromMilliseconds(33);

            while (true)
            {
                // IMPORTANTE: Processa mensagens enfileiradas
                _server.ProcessIncoming();

                // Verifica input
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.Q)
                    {
                        Console.WriteLine("Shutting down server...");
                        break;
                    }
                }

                // Sleep
                var now = DateTime.UtcNow;
                var elapsed = now - lastUpdate;
                var remaining = tickInterval - elapsed;

                if (remaining > TimeSpan.Zero)
                {
                    await Task.Delay(remaining);
                }

                lastUpdate = DateTime.UtcNow;
            }

            // Para servidor
            await _server.StopAsync();
            _server.Dispose();

            Console.WriteLine("Server stopped. Press any key to exit.");
            Console.ReadKey();
        }

        // === CALLBACKS ===

        static void OnClientConnected(int clientId, string ipAddress)
        {
            Console.WriteLine($"[CONNECT] Client {clientId} connected! (IP: {ipAddress})");

            // Envia mensagem de boas-vindas
            var packet = PacketPool.Rent();
            packet.Type = (byte)PacketType.Data;
            packet.Channel = ChannelType.Reliable;
            packet.Sequence = 1;

            string welcomeMsg = $"Welcome, Client {clientId}!";
            packet.Payload = Encoding.UTF8.GetBytes(welcomeMsg);
            packet.PayloadSize = packet.Payload.Length;

            // USA A INSTÂNCIA GLOBAL! ✅
            _ = _server.SendToClientAsync(clientId, packet);

            Console.WriteLine($"[SEND] Sent welcome message to client {clientId}");
        }

        static void OnClientDisconnected(int clientId, string reason)
        {
            Console.WriteLine($"[DISCONNECT] Client {clientId} disconnected: {reason}");
        }

        static void OnPacketReceived(int clientId, Packet packet)
        {
            try
            {
                // Desserializa mensagem
                string message = Encoding.UTF8.GetString(packet.Payload, 0, packet.PayloadSize);

                Console.WriteLine($"[RECV] From client {clientId}: \"{message}\"");

                // Responde com echo
                var response = PacketPool.Rent();
                response.Type = (byte)PacketType.Data;
                response.Channel = ChannelType.Reliable;
                response.Sequence = (ushort)(packet.Sequence + 1);

                string echoMsg = $"Echo: {message}";
                response.Payload = Encoding.UTF8.GetBytes(echoMsg);
                response.PayloadSize = response.Payload.Length;

                // USA A INSTÂNCIA GLOBAL! ✅
                _ = _server.SendToClientAsync(clientId, response);

                Console.WriteLine($"[SEND] Echoed back to client {clientId}");

                // IMPORTANTE: Retorna packet ao pool
                PacketPool.Return(packet);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Processing packet from {clientId}: {ex.Message}");
                PacketPool.Return(packet);
            }
        }

        static void OnError(string error)
        {
            Console.WriteLine($"[ERROR] {error}");
        }
    }
}