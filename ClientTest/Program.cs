// tests/ClientTest/Program.cs

using LiteNetLib;
using MessagePack;
using System;
using System.Threading;
using System.Threading.Tasks;
using XL4Net.Client.Prediction;
using XL4Net.Client.Transport;
using XL4Net.Shared.Math;
using XL4Net.Shared.Pooling;
using XL4Net.Shared.Prediction;
using XL4Net.Shared.Protocol.Enums;
using XL4Net.Shared.Protocol.Messages.Game;
using XL4Net.Shared.Transport;

namespace ClientTest
{
    class Program
    {
        // ============================================
        // CONFIGURAÇÃO
        // ============================================
        private const string SERVER_HOST = "127.0.0.1";
        private const int SERVER_PORT = 7777;
        private const int TICK_RATE = 30;
        private const float TICK_INTERVAL = 1f / TICK_RATE;
        private const float PING_INTERVAL = 2f;

        // ============================================
        // ESTADO
        // ============================================
        private static LiteNetTransport _transport;
        private static bool _authenticated = false;
        private static bool _inGame = false;

        // Sistema de Prediction (ClientPrediction.cs completo)
        private static ClientPrediction _prediction;

        // Ping/Pong
        private static DateTime _lastPingSent = DateTime.MinValue;
        private static int _latencyMs = 0;
        private static int _pingsSent = 0;

        // ============================================
        // MAIN
        // ============================================
        static async Task Main(string[] args)
        {
            Console.WriteLine("========================================");
            Console.WriteLine("  XL4Net ClientTest - Movement Test");
            Console.WriteLine("  Fase 5: Server Reconciliation");
            Console.WriteLine("========================================");
            Console.WriteLine();

            // Inicializa sistema de prediction
            _prediction = new ClientPrediction(
                movementSettings: MovementSettings.Default,
                predictionSettings: PredictionSettings.Default
            );

            // Registra eventos
            _prediction.OnMisprediction += OnMisprediction;
            _prediction.OnReconciliationComplete += OnReconciliationComplete;

            // Cria transport
            _transport = new LiteNetTransport();
            _transport.OnConnected += OnConnected;
            _transport.OnDisconnected += OnDisconnected;
            _transport.OnPacketReceived += OnPacketReceived;
            _transport.OnError += OnError;

            // Conecta
            Console.WriteLine($"Conectando em {SERVER_HOST}:{SERVER_PORT}...");
            var connected = await _transport.ConnectAsync(SERVER_HOST, SERVER_PORT);

            if (!connected)
            {
                Console.WriteLine("[ERRO] Falha ao conectar!");
                WaitForKey();
                return;
            }

            await Task.Delay(500);

            // Envia autenticação
            var token = JwtTestHelper.GenerateTestToken();
            SendAuthRequest(token);

            // Aguarda autenticação
            Console.WriteLine("Aguardando autenticação...");
            var authTimeout = DateTime.UtcNow.AddSeconds(5);
            while (!_authenticated && DateTime.UtcNow < authTimeout)
            {
                _transport.ProcessIncoming();
                await Task.Delay(16);
            }

            if (!_authenticated)
            {
                Console.WriteLine("[ERRO] Timeout na autenticação!");
                await Cleanup();
                return;
            }

            // Inicializa prediction com posição inicial
            _prediction.Initialize(Vec3.Zero, serverTick: 0);
            _inGame = true;
            _lastPingSent = DateTime.UtcNow;

            Console.WriteLine();
            Console.WriteLine("========================================");
            Console.WriteLine("  AUTENTICADO! Teste de Reconciliation");
            Console.WriteLine("========================================");
            Console.WriteLine();
            Console.WriteLine("Controles:");
            Console.WriteLine("  W/S     - Frente/Trás");
            Console.WriteLine("  A/D     - Esquerda/Direita");
            Console.WriteLine("  SPACE   - Pular");
            Console.WriteLine("  SHIFT   - Correr");
            Console.WriteLine("  P       - Posição atual");
            Console.WriteLine("  M       - Métricas");
            Console.WriteLine("  D       - Debug info");
            Console.WriteLine("  Q       - Sair");
            Console.WriteLine();
            Console.WriteLine($"Posição inicial: {_prediction.CurrentState.Position}");
            Console.WriteLine();

            // Loop principal
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };

            while (!cts.Token.IsCancellationRequested)
            {
                _transport.ProcessIncoming();
                SendPingIfNeeded();

                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(intercept: true);

                    switch (key.Key)
                    {
                        case ConsoleKey.Q:
                            Console.WriteLine("Saindo...");
                            cts.Cancel();
                            break;

                        case ConsoleKey.P:
                            ShowPosition();
                            break;

                        case ConsoleKey.M:
                            ShowMetrics();
                            break;

                        case ConsoleKey.I:
                            ShowDebug();
                            break;

                        case ConsoleKey.W:
                        case ConsoleKey.S:
                        case ConsoleKey.A:
                        case ConsoleKey.D:
                        case ConsoleKey.Spacebar:
                            ProcessKeyInput(key);
                            break;
                    }
                }

                await Task.Delay(16);
            }

            ShowFinalMetrics();
            await Cleanup();
        }

        // ============================================
        // INPUT PROCESSING
        // ============================================

        private static void ProcessKeyInput(ConsoleKeyInfo key)
        {
            if (!_inGame || !_prediction.IsInitialized) return;

            Vec2 moveDir = Vec2.Zero;
            bool jump = false;
            bool sprint = (key.Modifiers & ConsoleModifiers.Shift) != 0;

            switch (key.Key)
            {
                case ConsoleKey.W: moveDir = new Vec2(0, 1); break;
                case ConsoleKey.S: moveDir = new Vec2(0, -1); break;
                case ConsoleKey.A: moveDir = new Vec2(-1, 0); break;
                case ConsoleKey.D: moveDir = new Vec2(1, 0); break;
                case ConsoleKey.Spacebar: jump = true; break;
                default: return;
            }

            // Processa input via ClientPrediction
            var inputData = _prediction.ProcessInput(moveDir, jump, sprint);

            Console.WriteLine($"[INPUT] Tick={_prediction.CurrentTick}, Seq={inputData.SequenceNumber}, Dir={moveDir}");
            Console.WriteLine($"[PREDICT] Pos={_prediction.CurrentState.Position}, Pending={_prediction.PendingInputsCount}");

            // Envia para servidor
            SendPlayerInput(inputData);
        }

        // ============================================
        // EVENTOS DE PREDICTION
        // ============================================

        private static void OnMisprediction(StateSnapshot predicted, StateSnapshot server, float delta)
        {
            Console.WriteLine();
            Console.WriteLine($"[!] MISPREDICTION detectada!");
            Console.WriteLine($"    Predicted: {predicted.Position}");
            Console.WriteLine($"    Server:    {server.Position}");
            Console.WriteLine($"    Delta:     {delta:F4}");
        }

        private static void OnReconciliationComplete(StateSnapshot oldState, StateSnapshot newState, int replayed)
        {
            Console.WriteLine($"[FIX] Reconciliation completa: {replayed} inputs re-aplicados");
            Console.WriteLine($"      Nova posição: {newState.Position}");
        }

        // ============================================
        // DISPLAY
        // ============================================

        private static void ShowPosition()
        {
            var state = _prediction.CurrentState;
            Console.WriteLine($"[POS] Pos={state.Position}, Vel={state.Velocity}, Grounded={state.IsGrounded}");
        }

        private static void ShowMetrics()
        {
            Console.WriteLine();
            Console.WriteLine("=== MÉTRICAS ===");
            Console.WriteLine($"  Tick atual: {_prediction.CurrentTick}");
            Console.WriteLine($"  Inputs pendentes: {_prediction.PendingInputsCount}");
            Console.WriteLine($"  Mispredictions: {_prediction.TotalMispredictions}");
            Console.WriteLine($"  Avg Delta: {_prediction.AverageMispredictionDelta:F4}");
            Console.WriteLine($"  Latência: {_latencyMs}ms");
            Console.WriteLine($"  Pings: {_pingsSent}");
            Console.WriteLine("================");
        }

        private static void ShowDebug()
        {
            Console.WriteLine();
            Console.WriteLine(_prediction.GetDebugInfo());
        }

        private static void ShowFinalMetrics()
        {
            Console.WriteLine();
            Console.WriteLine("=== MÉTRICAS FINAIS ===");
            Console.WriteLine(_prediction.GetDebugInfo());
            Console.WriteLine("=======================");
        }

        // ============================================
        // PING/PONG
        // ============================================

        private static void SendPingIfNeeded()
        {
            var now = DateTime.UtcNow;
            if ((now - _lastPingSent).TotalSeconds >= PING_INTERVAL)
            {
                SendPing();
                _lastPingSent = now;
            }
        }

        private static void SendPing()
        {
            var packet = PacketPool.Rent();
            packet.Type = (byte)PacketType.Ping;
            packet.Channel = ChannelType.Unreliable;
            packet.Sequence = (ushort)(_pingsSent + 1);
            packet.Payload = BitConverter.GetBytes(DateTime.UtcNow.Ticks);
            packet.PayloadSize = 8;
            _ = _transport.SendAsync(packet);
            _pingsSent++;

            if (_pingsSent % 10 == 0)
                Console.WriteLine($"[PING] #{_pingsSent} (keepalive)");
        }

        private static void HandlePong(Packet packet)
        {
            if (packet.Payload != null && packet.Payload.Length >= 8)
            {
                var sent = BitConverter.ToInt64(packet.Payload, 0);
                _latencyMs = (int)((DateTime.UtcNow.Ticks - sent) / TimeSpan.TicksPerMillisecond);

                // Sincroniza tick com servidor
                _prediction.SyncTick(_prediction.CurrentTick, _latencyMs / 2);
            }
        }

        // ============================================
        // NETWORK - SEND
        // ============================================

        private static void SendAuthRequest(string token)
        {
            Console.WriteLine("[SEND] GameAuthRequest...");
            var authRequest = new GameAuthRequestMessage(token, "1.0.0");
            var packet = PacketPool.Rent();
            packet.Type = (byte)PacketType.Data;
            packet.Channel = ChannelType.Reliable;
            packet.Payload = MessagePackSerializer.Serialize(authRequest);
            packet.PayloadSize = packet.Payload.Length;
            _ = _transport.SendAsync(packet);
        }

        private static void SendPlayerInput(InputData input)
        {
            var message = new PlayerInputMessage(input);
            var packet = PacketPool.Rent();
            packet.Type = (byte)PacketType.Data;
            packet.Channel = ChannelType.Reliable;
            packet.Payload = MessagePackSerializer.Serialize(message);
            packet.PayloadSize = packet.Payload.Length;
            _ = _transport.SendAsync(packet);
        }

        // ============================================
        // NETWORK - RECEIVE
        // ============================================

        private static void OnConnected() => Console.WriteLine("[EVENT] Conectado ao servidor!");
        private static void OnDisconnected(string reason) { Console.WriteLine($"[EVENT] Desconectado: {reason}"); _inGame = false; }
        private static void OnError(string error) => Console.WriteLine($"[ERRO] {error}");

        private static void OnPacketReceived(Packet packet)
        {
            try
            {
                var packetType = (PacketType)packet.Type;

                if (packetType == PacketType.Pong)
                {
                    HandlePong(packet);
                }
                else if (packetType == PacketType.Data)
                {
                    var messageType = PeekMessageType(packet.Payload);

                    switch (messageType)
                    {
                        case MessageType.TokenValidationResponse:
                            HandleAuthResponse(packet);
                            break;

                        case MessageType.PlayerState:
                            HandlePlayerState(packet);
                            break;

                        default:
                            TryHandleAsAuthResponse(packet);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERRO] {ex.Message}");
            }
            finally
            {
                PacketPool.Return(packet);
            }
        }

        private static void TryHandleAsAuthResponse(Packet packet)
        {
            try
            {
                var response = MessagePackSerializer.Deserialize<GameAuthResponseMessage>(packet.Payload);
                if (response != null && !string.IsNullOrEmpty(response.Message))
                    HandleAuthResponseInternal(response);
            }
            catch { }
        }

        private static void HandleAuthResponse(Packet packet)
        {
            var response = MessagePackSerializer.Deserialize<GameAuthResponseMessage>(packet.Payload);
            HandleAuthResponseInternal(response);
        }

        private static void HandleAuthResponseInternal(GameAuthResponseMessage response)
        {
            Console.WriteLine();
            Console.WriteLine("=== AUTH RESPONSE ===");
            Console.WriteLine($"  Success: {response.Success}");
            Console.WriteLine($"  Message: {response.Message}");
            if (response.Success)
            {
                Console.WriteLine($"  UserId: {response.UserId}");
                Console.WriteLine($"  Username: {response.Username}");
                _authenticated = true;
            }
            Console.WriteLine("=====================");
        }

        private static void HandlePlayerState(Packet packet)
        {
            var message = MessagePackSerializer.Deserialize<PlayerStateMessage>(packet.Payload);
            var serverState = message.State;

            // Usa ClientPrediction para processar estado do servidor
            _prediction.OnServerStateReceived(serverState);

            Console.WriteLine();
            Console.WriteLine("=== SERVER STATE ===");
            Console.WriteLine($"  Tick: {serverState.Tick}, LastInput: {serverState.LastProcessedInput}");
            Console.WriteLine($"  Position: {serverState.Position}");
            Console.WriteLine($"  Pending: {_prediction.PendingInputsCount}");
            Console.WriteLine($"  [OK] State processado");
            Console.WriteLine("====================");
        }

        private static MessageType PeekMessageType(byte[] payload)
        {
            if (payload == null || payload.Length < 2) return MessageType.Unknown;
            try
            {
                var reader = new MessagePackReader(payload);
                int count = reader.ReadArrayHeader();
                if (count > 0) return (MessageType)reader.ReadUInt16();
            }
            catch { }
            return MessageType.Unknown;
        }

        private static async Task Cleanup()
        {
            Console.WriteLine("Limpando recursos...");
            await _transport.StopAsync();
            _transport.Dispose();
            Console.WriteLine("Fim do teste.");
            WaitForKey();
        }

        private static void WaitForKey()
        {
            Console.WriteLine("\nPressione qualquer tecla para sair...");
            Console.ReadKey();
        }
    }
}