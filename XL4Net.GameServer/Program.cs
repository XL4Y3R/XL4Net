// XL4Net.GameServer/Program.cs

using System;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using Serilog.Events;
using XL4Net.GameServer.Config;
using XL4Net.GameServer.Core;

namespace XL4Net.GameServer
{
    /// <summary>
    /// Entry point do GameServer.
    /// </summary>
    public class Program
    {
        // Controle de shutdown
        private static readonly ManualResetEventSlim _shutdownEvent = new ManualResetEventSlim(false);
        private static GameServer.Core.GameServer _server;

        public static async Task<int> Main(string[] args)
        {
            // ============================================
            // 1. CONFIGURAR SERILOG
            // ============================================

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .Enrich.WithProperty("Application", "XL4Net.GameServer")
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
                )
                .CreateLogger();

            try
            {
                Log.Information("========================================");
                Log.Information("  XL4Net GameServer");
                Log.Information("  Version: 1.0.0");
                Log.Information("========================================");

                // ============================================
                // 2. CARREGAR CONFIGURAÇÕES
                // ============================================

                var config = LoadConfiguration(args);

                Log.Information("Configuration loaded: {Config}", config);

                // ============================================
                // 3. REGISTRAR SHUTDOWN HANDLER (Ctrl+C)
                // ============================================

                Console.CancelKeyPress += OnCancelKeyPress;
                AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

                // ============================================
                // 4. CRIAR E INICIAR SERVIDOR
                // ============================================

                _server = new GameServer.Core.GameServer(config);

                // Registra eventos para logging
                _server.OnServerStarted += () => Log.Information("Server is ready to accept connections!");
                _server.OnServerStopped += () => Log.Information("Server has stopped.");
                _server.Players.OnPlayerConnected += (p) => Log.Information(">> Player connected: {Player}", p);
                _server.Players.OnPlayerDisconnected += (p) => Log.Information("<< Player disconnected: {Player}", p);
                _server.Players.OnPlayerAuthenticated += (p) => Log.Information("** Player authenticated: {Player}", p);

                // Inicia servidor
                var started = await _server.StartAsync();

                if (!started)
                {
                    Log.Fatal("Failed to start server!");
                    return 1;
                }

                // ============================================
                // 5. LOOP PRINCIPAL (MÉTRICAS)
                // ============================================

                Log.Information("Press Ctrl+C to stop the server...");
                Log.Information("");

                // Exibe métricas a cada 10 segundos
                var metricsTask = Task.Run(() => MetricsLoopAsync());

                // Aguarda sinal de shutdown
                _shutdownEvent.Wait();

                // ============================================
                // 6. SHUTDOWN GRACEFUL
                // ============================================

                Log.Information("Shutting down...");

                await _server.StopAsync();
                _server.Dispose();

                Log.Information("Goodbye!");

                return 0;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Unhandled exception");
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        // ============================================
        // CONFIGURAÇÃO
        // ============================================

        /// <summary>
        /// Carrega configurações (pode vir de args, env vars, ou arquivo).
        /// </summary>
        private static GameServerConfig LoadConfiguration(string[] args)
        {
            var config = new GameServerConfig();

            // Sobrescreve com argumentos de linha de comando (se houver)
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLower())
                {
                    case "--port" when i + 1 < args.Length:
                        if (ushort.TryParse(args[++i], out var port))
                            config.Port = port;
                        break;

                    case "--max-players" when i + 1 < args.Length:
                        if (int.TryParse(args[++i], out var maxPlayers))
                            config.MaxPlayers = maxPlayers;
                        break;

                    case "--tick-rate" when i + 1 < args.Length:
                        if (int.TryParse(args[++i], out var tickRate))
                            config.TickRate = tickRate;
                        break;

                    case "--name" when i + 1 < args.Length:
                        config.ServerName = args[++i];
                        break;

                    case "--help":
                        PrintHelp();
                        Environment.Exit(0);
                        break;
                }
            }

            // Sobrescreve com variáveis de ambiente (se houver)
            var envPort = Environment.GetEnvironmentVariable("GAMESERVER_PORT");
            if (!string.IsNullOrEmpty(envPort) && ushort.TryParse(envPort, out var envPortValue))
                config.Port = envPortValue;

            var envMaxPlayers = Environment.GetEnvironmentVariable("GAMESERVER_MAX_PLAYERS");
            if (!string.IsNullOrEmpty(envMaxPlayers) && int.TryParse(envMaxPlayers, out var envMaxPlayersValue))
                config.MaxPlayers = envMaxPlayersValue;

            return config;
        }

        /// <summary>
        /// Exibe ajuda de linha de comando.
        /// </summary>
        private static void PrintHelp()
        {
            Console.WriteLine("XL4Net GameServer");
            Console.WriteLine("");
            Console.WriteLine("Usage: XL4Net.GameServer [options]");
            Console.WriteLine("");
            Console.WriteLine("Options:");
            Console.WriteLine("  --port <port>          Server port (default: 7777)");
            Console.WriteLine("  --max-players <n>      Maximum players (default: 100)");
            Console.WriteLine("  --tick-rate <hz>       Tick rate in Hz (default: 30)");
            Console.WriteLine("  --name <name>          Server name (default: GameServer-01)");
            Console.WriteLine("  --help                 Show this help");
            Console.WriteLine("");
            Console.WriteLine("Environment variables:");
            Console.WriteLine("  GAMESERVER_PORT        Server port");
            Console.WriteLine("  GAMESERVER_MAX_PLAYERS Maximum players");
        }

        // ============================================
        // MÉTRICAS
        // ============================================

        /// <summary>
        /// Loop que exibe métricas periodicamente.
        /// </summary>
        private static async Task MetricsLoopAsync()
        {
            while (!_shutdownEvent.IsSet)
            {
                await Task.Delay(TimeSpan.FromSeconds(10));

                if (_server != null && _server.IsRunning)
                {
                    Log.Information(
                        "[METRICS] Tick={Tick}, Players={Players}/{Max}, " +
                        "Authenticated={Auth}, TickTime={TickMs:F2}ms (avg: {AvgMs:F2}ms)",
                        _server.CurrentTick,
                        _server.PlayerCount,
                        _server.Config.MaxPlayers,
                        _server.AuthenticatedCount,
                        _server.LastTickDurationMs,
                        _server.AverageTickDurationMs
                    );
                }
            }
        }

        // ============================================
        // SHUTDOWN HANDLERS
        // ============================================

        /// <summary>
        /// Handler para Ctrl+C.
        /// </summary>
        private static void OnCancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            Log.Information("Ctrl+C detected, initiating shutdown...");
            e.Cancel = true; // Não termina imediatamente
            _shutdownEvent.Set();
        }

        /// <summary>
        /// Handler para quando o processo está encerrando.
        /// </summary>
        private static void OnProcessExit(object sender, EventArgs e)
        {
            if (!_shutdownEvent.IsSet)
            {
                Log.Information("Process exit detected, initiating shutdown...");
                _shutdownEvent.Set();

                // Aguarda um pouco para cleanup
                Thread.Sleep(1000);
            }
        }
    }
}