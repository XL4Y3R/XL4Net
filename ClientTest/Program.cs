// ClientTest/Program.cs

using System;
using System.Text;
using System.Threading.Tasks;
using XL4Net.Client.Transport;
using XL4Net.Shared.Pooling;
using XL4Net.Shared.Protocol.Enums;
using XL4Net.Shared.Transport;

namespace ClientTest
{
    class Program
    {
        private static LiteNetTransport _client;
        private static ushort _sequenceNumber = 0;

        static async Task Main(string[] args)
        {
            Console.WriteLine("=== XL4Net Client Test ===");
            Console.WriteLine();

            // Cria cliente
            _client = new LiteNetTransport();

            // Registra eventos
            _client.OnConnected += OnConnected;
            _client.OnDisconnected += OnDisconnected;
            _client.OnPacketReceived += OnPacketReceived;
            _client.OnError += OnError;

            // Conecta ao servidor
            Console.Write("Enter server address (default: 127.0.0.1): ");
            string host = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(host))
                host = "127.0.0.1"; 

            Console.WriteLine($"Connecting to {host}:7777...");
            bool connected = await _client.ConnectAsync(host, 7777);

            if (!connected)
            {
                Console.WriteLine("Failed to connect!");
                return;
            }

            Console.WriteLine("Connected!");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  Type a message and press ENTER to send");
            Console.WriteLine("  Type 'quit' to disconnect");
            Console.WriteLine();

            // Game loop (processa mensagens a 30 Hz)
            var lastUpdate = DateTime.UtcNow;
            var tickInterval = TimeSpan.FromMilliseconds(33); // ~30 Hz

            while (_client.IsConnected)
            {
                // Processa mensagens enfileiradas
                _client.ProcessIncoming();

                // Verifica input do usuário (não-bloqueante)
                if (Console.KeyAvailable)
                {
                    string input = Console.ReadLine();

                    if (string.IsNullOrWhiteSpace(input))
                        continue;

                    if (input.ToLower() == "quit")
                    {
                        Console.WriteLine("Disconnecting...");
                        break;
                    }

                    // Envia mensagem
                    SendMessage(input);
                }

                // Sleep até próximo tick
                var now = DateTime.UtcNow;
                var elapsed = now - lastUpdate;
                var remaining = tickInterval - elapsed;

                if (remaining > TimeSpan.Zero)
                {
                    await Task.Delay(remaining);
                }

                lastUpdate = DateTime.UtcNow;
            }

            // Desconecta
            await _client.StopAsync();
            _client.Dispose();

            Console.WriteLine("Disconnected. Press any key to exit.");
            Console.ReadKey();
        }

        // === HELPERS ===

        static void SendMessage(string text)
        {
            try
            {
                var packet = PacketPool.Rent();
                packet.Type = (byte)PacketType.Data;
                packet.Channel = ChannelType.Reliable;
                packet.Sequence = ++_sequenceNumber;

                packet.Payload = Encoding.UTF8.GetBytes(text);
                packet.PayloadSize = packet.Payload.Length;

                _ = _client.SendAsync(packet);

                Console.WriteLine($"[SEND] \"{text}\"");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to send: {ex.Message}");
            }
        }

        // === CALLBACKS ===

        static void OnConnected()
        {
            Console.WriteLine("[EVENT] Connected to server!");
        }

        static void OnDisconnected(string reason)
        {
            Console.WriteLine($"[EVENT] Disconnected: {reason}");
        }

        static void OnPacketReceived(Packet packet)
        {
            try
            {
                string message = Encoding.UTF8.GetString(packet.Payload, 0, packet.PayloadSize);
                Console.WriteLine($"[RECV] Server says: \"{message}\"");

                // IMPORTANTE: Retorna ao pool
                PacketPool.Return(packet);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Processing packet: {ex.Message}");
                PacketPool.Return(packet);
            }
        }

        static void OnError(string error)
        {
            Console.WriteLine($"[ERROR] {error}");
        }
    }
}