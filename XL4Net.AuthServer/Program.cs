// XL4Net.AuthServer/Program.cs

using System;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;
using Serilog;
using XL4Net.AuthServer.Authentication;
using XL4Net.AuthServer.Core;
using XL4Net.AuthServer.Database;
using XL4Net.AuthServer.Endpoints;
using XL4Net.AuthServer.Messages;
using XL4Net.AuthServer.Models;
using XL4Net.Shared.Protocol.Enums;
using XL4Net.Shared.Protocol.Messages.Auth;
using XL4Net.Shared.Transport;
using XL4Net.Shared.Pooling;

namespace XL4Net.AuthServer
{
    /// <summary>
    /// Entry point do AuthServer.
    /// Servidor de autenticação usando LiteNetLib (porta 2106).
    /// </summary>
    class Program
    {
        // Componentes globais
        private static AuthConfig _config = null!;
        private static XL4Net.Server.Transport.LiteNetServerTransport _transport = null!;
        private static IAccountRepository _repository = null!;
        private static TokenManager _tokenManager = null!;
        private static RateLimiter _rateLimiter = null!;

        // Endpoints
        private static RegisterEndpoint _registerEndpoint = null!;
        private static LoginEndpoint _loginEndpoint = null!;
        private static ValidateTokenEndpoint _validateTokenEndpoint = null!;

        // Controle
        private static CancellationTokenSource _cts = new CancellationTokenSource();
        private static bool _isRunning = false;

        static async Task<int> Main(string[] args)
        {
            // 1. Setup Serilog (logging)
            SetupLogging();

            Log.Information("========================================");
            Log.Information("XL4Net AuthServer v1.0");
            Log.Information("========================================");

            try
            {
                // 2. Carrega configurações
                if (!LoadConfiguration())
                    return 1;

                // 3. Testa conexão com PostgreSQL
                if (!await TestDatabaseConnection())
                    return 1;

                // 4. Instancia componentes
                if (!InstantiateComponents())
                    return 1;

                // 5. Inicia transport (LiteNetLib na porta 2106)
                if (!await StartTransport())
                    return 1;

                // 6. Handlers de sinais (Ctrl+C)
                Console.CancelKeyPress += OnCancelKeyPress;

                Log.Information("AuthServer started successfully on port {Port}", _config.ServerPort);
                Log.Information("Press Ctrl+C to stop");

                // 7. Loop principal
                _isRunning = true;
                await MainLoop();

                Log.Information("AuthServer stopped gracefully");
                return 0;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "AuthServer crashed");
                return 1;
            }
            finally
            {
                // Cleanup
                await Shutdown();
                Log.CloseAndFlush();
            }
        }

        /// <summary>
        /// Configura Serilog.
        /// </summary>
        private static void SetupLogging()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
                )
                .Enrich.WithProperty("ServerType", "AuthServer")
                .CreateLogger();
        }

        /// <summary>
        /// Carrega configurações (environment variables ou padrão).
        /// </summary>
        private static bool LoadConfiguration()
        {
            try
            {
                Log.Information("Loading configuration...");

                _config = AuthConfig.LoadFromEnvironment();

                if (!_config.Validate(out var error))
                {
                    Log.Error("Configuration validation failed: {Error}", error);
                    return false;
                }

                Log.Information("Configuration loaded:");
                Log.Information(_config.ToString());

                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load configuration");
                return false;
            }
        }

        /// <summary>
        /// Testa conexão com PostgreSQL.
        /// </summary>
        private static async Task<bool> TestDatabaseConnection()
        {
            try
            {
                Log.Information("Testing database connection...");

                // Cria repositório temporário para testar conexão
                var testRepo = new PostgresAccountRepository(_config.DatabaseConnectionString);

                // Tenta fazer query simples
                var usernameExists = await testRepo.UsernameExistsAsync("_test_connection_");

                Log.Information("Database connection successful!");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Database connection failed - Make sure PostgreSQL is running");
                Log.Error("Connection string: {ConnectionString}",
                    _config.DatabaseConnectionString.Split(';')[0] + ";...");
                return false;
            }
        }

        /// <summary>
        /// Instancia componentes (Repository, TokenManager, Endpoints, etc).
        /// </summary>
        private static bool InstantiateComponents()
        {
            try
            {
                Log.Information("Instantiating components...");

                // Repository
                _repository = new PostgresAccountRepository(_config.DatabaseConnectionString);
                Log.Debug("PostgresAccountRepository");

                // Token Manager
                _tokenManager = new TokenManager(
                    _config.JwtSecretKey,
                    _config.JwtIssuer,
                    _config.JwtAudience,
                    _config.JwtExpirationMinutes
                );
                Log.Debug("TokenManager");

                // Rate Limiter
                _rateLimiter = new RateLimiter(
                    _repository,
                    _config.RateLimitTimeWindowMinutes,
                    _config.RateLimitMaxAttempts
                );
                Log.Debug("RateLimiter");

                // Endpoints
                _registerEndpoint = new RegisterEndpoint(_repository);
                _loginEndpoint = new LoginEndpoint(_repository, _tokenManager, _rateLimiter);
                _validateTokenEndpoint = new ValidateTokenEndpoint(_tokenManager);
                Log.Debug("Endpoints (Register, Login, ValidateToken)");

                Log.Information("All components instantiated successfully");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to instantiate components");
                return false;
            }
        }

        private static async Task<bool> StartTransport()
        {
            try
            {
                Log.Information("Starting transport (LiteNetLib) on port {Port}...", _config.ServerPort);

                // Cria LiteNetServerTransport
                _transport = new XL4Net.Server.Transport.LiteNetServerTransport(
                    _config.ServerPort,
                    maxClients: 1000 // AuthServer pode ter muitos clients simultâneos
                );

                // Registra eventos
                _transport.OnClientConnected += OnClientConnected;
                _transport.OnClientDisconnected += OnClientDisconnected;
                _transport.OnPacketReceived += OnPacketReceived;
                _transport.OnError += OnTransportError;

                // Inicia
                var success = await _transport.StartAsync();

                if (success)
                {
                    Log.Information("Transport started successfully");
                    return true;
                }
                else
                {
                    Log.Error("Failed to start transport");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to start transport");
                return false;
            }
        }

        /// <summary>
        /// Loop principal do servidor.
        /// </summary>
        private static async Task MainLoop()
        {
            Log.Information("Entering main loop...");

            // Tick a cada 100ms (10 Hz - suficiente para AuthServer)
            var tickInterval = TimeSpan.FromMilliseconds(100);

            while (_isRunning && !_cts.Token.IsCancellationRequested)
            {
                var tickStart = DateTime.UtcNow;

                try
                {
                    // Processa mensagens recebidas (da fila thread-safe)
                    _transport.ProcessIncoming();

                    // Sleep até próximo tick
                    var elapsed = DateTime.UtcNow - tickStart;
                    var remaining = tickInterval - elapsed;

                    if (remaining > TimeSpan.Zero)
                    {
                        await Task.Delay(remaining, _cts.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Shutdown solicitado
                    break;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error in main loop");
                }
            }

            Log.Information("Main loop exited");
        }

        /// <summary>
        /// Handler: Cliente conectou.
        /// </summary>
        private static void OnClientConnected(int clientId, string ipAddress)
        {
            Log.Information("[CONNECT] Client {ClientId} connected from {IpAddress}", clientId, ipAddress);
        }

        /// <summary>
        /// Handler: Cliente desconectou.
        /// </summary>
        private static void OnClientDisconnected(int clientId, string reason)
        {
            Log.Information("[DISCONNECT] Client {ClientId} disconnected: {Reason}", clientId, reason);
        }

        /// <summary>
        /// Handler: Packet recebido de cliente.
        /// ESTE É O CORAÇÃO DO AUTHSERVER!
        /// </summary>
        private static async void OnPacketReceived(int clientId, Packet packet)
        {
            try
            {
                // Log de debug
                Log.Debug("[RECV] Client {ClientId} - {Packet}", clientId, packet);

                // Valida packet
                if (packet.Payload == null || packet.PayloadSize == 0)
                {
                    Log.Warning("Received empty packet from client {ClientId}", clientId);
                    PacketPool.Return(packet);
                    return;
                }

                // Deserializa MessagePack
                // Primeiro, identifica o tipo da mensagem olhando o campo Type (Key 0)
                var messageTypeRaw = MessagePackSerializer.Deserialize<MessageTypeWrapper>(packet.Payload);
                var messageType = messageTypeRaw.Type;

                Log.Debug("Message type: {MessageType}", messageType);

                // Processa conforme o tipo
                Packet? responsePacket = null;

                switch (messageType)
                {
                    case MessageType.RegisterRequest:
                        responsePacket = await HandleRegisterRequest(clientId, packet.Payload);
                        break;

                    case MessageType.LoginRequest:
                        responsePacket = await HandleLoginRequest(clientId, packet.Payload);
                        break;

                    case MessageType.TokenValidationRequest:
                        responsePacket = await HandleValidateTokenRequest(clientId, packet.Payload);
                        break;

                    default:
                        Log.Warning("Unknown message type {MessageType} from client {ClientId}", messageType, clientId);
                        break;
                }

                // Envia resposta (se houver)
                if (responsePacket != null)
                {
                    await _transport.SendToAsync(clientId, responsePacket);
                    // Packet é retornado ao pool automaticamente após SendToAsync
                }

                // Retorna packet recebido ao pool
                PacketPool.Return(packet);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing packet from client {ClientId}", clientId);
                PacketPool.Return(packet);
            }
        }

        /// <summary>
        /// Processa RegisterRequest.
        /// </summary>
        private static async Task<Packet?> HandleRegisterRequest(int clientId, byte[] payload)
        {
            try
            {
                // Deserializa mensagem
                var request = MessagePackSerializer.Deserialize<RegisterRequestMessage>(payload);

                Log.Information("[REGISTER] Client {ClientId} - Username: {Username}, Email: {Email}",
                    clientId, request.Username, request.Email);

                // Converte para RegisterRequest do endpoint
                var endpointRequest = new RegisterRequest
                {
                    Username = request.Username,
                    Email = request.Email,
                    Password = request.Password,
                    ConfirmPassword = request.ConfirmPassword
                };

                // Chama endpoint
                var result = await _registerEndpoint.HandleAsync(endpointRequest);

                // Converte resposta para mensagem
                RegisterResponseMessage responseMsg;
                if (result.Success)
                {
                    responseMsg = RegisterResponseMessage.CreateSuccess(result.AccountId!.Value, result.Username!);
                }
                else
                {
                    responseMsg = RegisterResponseMessage.CreateFailure(result.ErrorMessage!);
                }

                // Serializa resposta
                var responsePayload = MessagePackSerializer.Serialize(responseMsg);

                // Cria packet de resposta
                var responsePacket = PacketPool.Rent();
                responsePacket.Type = (byte)PacketType.Data;
                responsePacket.Channel = ChannelType.Reliable;
                responsePacket.Payload = responsePayload;
                responsePacket.PayloadSize = responsePayload.Length;

                return responsePacket;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error handling RegisterRequest from client {ClientId}", clientId);
                return null;
            }
        }

        /// <summary>
        /// Processa LoginRequest.
        /// </summary>
        private static async Task<Packet?> HandleLoginRequest(int clientId, byte[] payload)
        {
            try
            {
                // Deserializa mensagem
                var request = MessagePackSerializer.Deserialize<LoginRequestMessage>(payload);

                Log.Information("[LOGIN] Client {ClientId} - UsernameOrEmail: {UsernameOrEmail}",
                    clientId, request.UsernameOrEmail);

                // Converte para LoginRequest do endpoint
                var endpointRequest = new LoginRequest
                {
                    UsernameOrEmail = request.UsernameOrEmail,
                    Password = request.Password,
                    IpAddress = _transport.GetClientIP(clientId) ?? "127.0.0.1" // Fallback se não conseguir pegar IP
                };

                // Chama endpoint
                var result = await _loginEndpoint.HandleAsync(endpointRequest);

                // Converte resposta para mensagem
                LoginResponseMessage responseMsg;
                if (result.Success)
                {
                    responseMsg = LoginResponseMessage.CreateSuccess(
                        result.Token!,
                        result.ExpiresAt!.Value,
                        result.UserId!.Value,
                        result.Username!
                    );
                }
                else if (result.IsRateLimited)
                {
                    responseMsg = LoginResponseMessage.CreateRateLimited(
                        result.ErrorMessage!,
                        result.RetryAfterSeconds!.Value
                    );
                }
                else
                {
                    responseMsg = LoginResponseMessage.CreateFailure(result.ErrorMessage!);
                }

                // Serializa resposta
                var responsePayload = MessagePackSerializer.Serialize(responseMsg);

                // Cria packet de resposta
                var responsePacket = PacketPool.Rent();
                responsePacket.Type = (byte)PacketType.Data;
                responsePacket.Channel = ChannelType.Reliable;
                responsePacket.Payload = responsePayload;
                responsePacket.PayloadSize = responsePayload.Length;

                return responsePacket;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error handling LoginRequest from client {ClientId}", clientId);
                return null;
            }
        }

        /// <summary>
        /// Processa ValidateTokenRequest (de GameServer).
        /// </summary>
        private static async Task<Packet?> HandleValidateTokenRequest(int clientId, byte[] payload)
        {
            try
            {
                // Deserializa mensagem
                var request = MessagePackSerializer.Deserialize<ValidateTokenRequestMessage>(payload);

                Log.Information("[VALIDATE] Client {ClientId} - Token validation request", clientId);

                // Converte para ValidateTokenRequest do endpoint
                var endpointRequest = new ValidateTokenRequest
                {
                    Token = request.Token
                };

                // Chama endpoint
                var result = await _validateTokenEndpoint.HandleAsync(endpointRequest);

                // Converte resposta para mensagem
                ValidateTokenResponseMessage responseMsg;
                if (result.IsValid)
                {
                    responseMsg = ValidateTokenResponseMessage.CreateSuccess(
                        result.UserId!.Value,
                        result.Username!,
                        result.ExpiresAt!.Value
                    );
                }
                else
                {
                    responseMsg = ValidateTokenResponseMessage.CreateFailure(result.ErrorMessage!);
                }

                // Serializa resposta
                var responsePayload = MessagePackSerializer.Serialize(responseMsg);

                // Cria packet de resposta
                var responsePacket = PacketPool.Rent();
                responsePacket.Type = (byte)PacketType.Data;
                responsePacket.Channel = ChannelType.Reliable;
                responsePacket.Payload = responsePayload;
                responsePacket.PayloadSize = responsePayload.Length;

                return responsePacket;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error handling ValidateTokenRequest from client {ClientId}", clientId);
                return null;
            }
        }

        /// <summary>
        /// Handler: Erro no transport.
        /// </summary>
        private static void OnTransportError(string error)
        {
            Log.Error("[TRANSPORT ERROR] {Error}", error);
        }

        /// <summary>
        /// Handler: Ctrl+C pressionado.
        /// </summary>
        private static void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true; // Previne finalização abrupta
            Log.Information("Shutdown requested (Ctrl+C)...");
            _isRunning = false;
            _cts.Cancel();
        }

        /// <summary>
        /// Shutdown graceful.
        /// </summary>
        private static async Task Shutdown()
        {
            Log.Information("Shutting down...");

            try
            {
                // Para transport
                if (_transport != null)
                {
                    await _transport.StopAsync();
                    Log.Information("Transport stopped");
                }

                // Cleanup rate limiter (opcional - limpa tentativas antigas)
                if (_rateLimiter != null)
                {
                    await _rateLimiter.CleanupOldAttemptsAsync();
                }

                _cts.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during shutdown");
            }
        }

        /// <summary>
        /// Helper class para deserializar apenas o campo Type da mensagem.
        /// </summary>
        [MessagePackObject(AllowPrivate = true)]
        internal class MessageTypeWrapper
        {
            [Key(0)]
            public MessageType Type { get; set; }
        }
    }
}