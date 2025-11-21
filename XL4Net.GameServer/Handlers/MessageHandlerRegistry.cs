// XL4Net.GameServer/Handlers/MessageHandlerRegistry.cs

using System;
using System.Collections.Generic;
using Serilog;
using XL4Net.GameServer.Players;
using XL4Net.Shared.Protocol.Enums;
using XL4Net.Shared.Transport;

namespace XL4Net.GameServer.Handlers
{
    /// <summary>
    /// Registry central de handlers de mensagens.
    /// Implementa o padrão Strategy com dispatch O(1).
    /// </summary>
    /// <remarks>
    /// Uso:
    /// 1. Registrar handlers no startup: registry.Register(new GameAuthHandler());
    /// 2. No game loop: registry.Dispatch(peerId, session, packet);
    /// 3. Registry faz lookup O(1) e chama o handler correto
    /// </remarks>
    public class MessageHandlerRegistry
    {
        // ============================================
        // CAMPOS PRIVADOS
        // ============================================

        // Lookup O(1) por PacketType
        private readonly Dictionary<PacketType, IMessageHandler> _handlers;

        // Referência ao servidor (passada para os handlers via contexto)
        private readonly Core.GameServer _server;

        // Métricas
        private long _totalMessagesHandled;
        private long _unknownPacketTypes;

        // ============================================
        // PROPRIEDADES
        // ============================================

        /// <summary>
        /// Total de mensagens processadas.
        /// </summary>
        public long TotalMessagesHandled => _totalMessagesHandled;

        /// <summary>
        /// Total de pacotes com tipo desconhecido.
        /// </summary>
        public long UnknownPacketTypes => _unknownPacketTypes;

        /// <summary>
        /// Número de handlers registrados.
        /// </summary>
        public int HandlerCount => _handlers.Count;

        // ============================================
        // CONSTRUTOR
        // ============================================

        /// <summary>
        /// Cria novo registry de handlers.
        /// </summary>
        /// <param name="server">Referência ao GameServer</param>
        public MessageHandlerRegistry(Core.GameServer server)
        {
            _server = server ?? throw new ArgumentNullException(nameof(server));
            _handlers = new Dictionary<PacketType, IMessageHandler>();
        }

        // ============================================
        // REGISTRO DE HANDLERS
        // ============================================

        /// <summary>
        /// Registra um handler para seu PacketType.
        /// </summary>
        /// <param name="handler">Handler a registrar</param>
        /// <exception cref="InvalidOperationException">Se já existe handler para o tipo</exception>
        public void Register(IMessageHandler handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            if (_handlers.ContainsKey(handler.PacketType))
            {
                throw new InvalidOperationException(
                    $"Handler for PacketType.{handler.PacketType} already registered");
            }

            _handlers[handler.PacketType] = handler;

            Log.Information("Handler registered: {Type} → {Handler}",
                handler.PacketType, handler.GetType().Name);
        }

        /// <summary>
        /// Registra múltiplos handlers de uma vez.
        /// </summary>
        public void RegisterAll(params IMessageHandler[] handlers)
        {
            foreach (var handler in handlers)
            {
                Register(handler);
            }
        }

        /// <summary>
        /// Verifica se existe handler para um tipo.
        /// </summary>
        public bool HasHandler(PacketType type)
        {
            return _handlers.ContainsKey(type);
        }

        /// <summary>
        /// Remove handler de um tipo.
        /// </summary>
        public bool Unregister(PacketType type)
        {
            var removed = _handlers.Remove(type);
            if (removed)
            {
                Log.Information("Handler unregistered: {Type}", type);
            }
            return removed;
        }

        // ============================================
        // DISPATCH
        // ============================================

        /// <summary>
        /// Despacha um packet para o handler apropriado.
        /// </summary>
        /// <param name="peerId">ID da conexão</param>
        /// <param name="session">Sessão do jogador (pode ser null)</param>
        /// <param name="packet">Packet recebido</param>
        /// <returns>True se handler foi encontrado e executado</returns>
        /// <remarks>
        /// IMPORTANTE: Este método NÃO retorna o packet ao pool.
        /// O handler é responsável por retornar após uso, ou o chamador
        /// deve retornar se nenhum handler for encontrado.
        /// </remarks>
        public bool Dispatch(int peerId, PlayerSession? session, Packet packet)
        {
            var packetType = (PacketType)packet.Type;

            // Lookup O(1)
            if (!_handlers.TryGetValue(packetType, out var handler))
            {
                _unknownPacketTypes++;

                Log.Warning("No handler for PacketType.{Type} from PeerId={PeerId}",
                    packetType, peerId);

                return false;
            }

            try
            {
                // Cria contexto para o handler
                var context = new MessageContext(peerId, session, _server);

                // Executa handler
                handler.Handle(context, packet);

                _totalMessagesHandled++;

                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in handler {Handler} for PeerId={PeerId}",
                    handler.GetType().Name, peerId);

                return false;
            }
        }

        /// <summary>
        /// Despacha packet usando apenas PeerId (busca session internamente).
        /// Conveniência para uso no GameServer.
        /// </summary>
        public bool Dispatch(int peerId, Packet packet)
        {
            var session = _server.Players.GetByPeerId(peerId);
            return Dispatch(peerId, session, packet);
        }

        // ============================================
        // DEBUG / MÉTRICAS
        // ============================================

        /// <summary>
        /// Lista todos os handlers registrados.
        /// </summary>
        public void LogRegisteredHandlers()
        {
            Log.Information("=== Registered Message Handlers ===");
            foreach (var kvp in _handlers)
            {
                Log.Information("  {Type,-20} → {Handler}",
                    kvp.Key, kvp.Value.GetType().Name);
            }
            Log.Information("=== Total: {Count} handlers ===", _handlers.Count);
        }

        /// <summary>
        /// Reseta contadores de métricas.
        /// </summary>
        public void ResetMetrics()
        {
            _totalMessagesHandled = 0;
            _unknownPacketTypes = 0;
        }
    }
}