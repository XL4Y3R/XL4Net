// XL4Net.Shared/Pooling/PacketPool.cs

using XL4Net.Shared.Transport;

namespace XL4Net.Shared.Pooling
{
    /// <summary>
    /// Pool estático global para reutilização de Packets.
    /// Reduz alocações e pressão no Garbage Collector.
    /// </summary>
    /// <remarks>
    /// Pool único compartilhado por todo o framework (Client, Server, AuthServer).
    /// 
    /// Configuração:
    /// - Initial Size: 128 packets (4 segundos @ 30Hz)
    /// - Max Size: 2048 packets (buffer para picos de tráfego)
    /// 
    /// Uso recomendado:
    /// <code>
    /// using (var rental = PacketPool.RentDisposable())
    /// {
    ///     var packet = rental.Value;
    ///     packet.Sequence = 123;
    ///     // Usa packet...
    /// } // Auto-return ao pool
    /// </code>
    /// 
    /// Performance esperada:
    /// - Zero alocações durante gameplay (após warmup)
    /// - Pressure no GC reduzida em ~95%
    /// - Reutilização de ~100 packets para 50 players @ 30Hz
    /// </remarks>
    public static class PacketPool
    {
        // Pool único global
        // Thread-safe (ConcurrentBag interno)
        private static readonly ObjectPool<Packet> _pool = new ObjectPool<Packet>(
            initialSize: 128,   // Warmup: ~4 segundos de tráfego @ 30Hz
            maxSize: 2048       // Limite: buffer para picos (ex: 100 players conectando)
        );

        /// <summary>
        /// Aluga um Packet do pool.
        /// Se o pool estiver vazio, cria um novo Packet.
        /// </summary>
        /// <returns>Packet pronto para uso (resetado se reciclado)</returns>
        /// <remarks>
        /// IMPORTANTE: Sempre retorne o packet com Return() ou use RentDisposable().
        /// Esquecer de retornar causa memory leak!
        /// </remarks>
        /// <example>
        /// <code>
        /// // Uso básico:
        /// var packet = PacketPool.Rent();
        /// try
        /// {
        ///     packet.Sequence = 100;
        ///     Send(packet);
        /// }
        /// finally
        /// {
        ///     PacketPool.Return(packet); // SEMPRE retorne!
        /// }
        /// </code>
        /// </example>
        public static Packet Rent()
        {
            return _pool.Rent();
        }

        /// <summary>
        /// Retorna um Packet ao pool para reutilização.
        /// O Packet é resetado automaticamente (Reset() é chamado).
        /// </summary>
        /// <param name="packet">Packet a ser retornado (pode ser null)</param>
        /// <remarks>
        /// Após retornar, NÃO use mais o packet! Ele pode ser reutilizado por outra thread.
        /// 
        /// ATENÇÃO: Se o packet tem Payload, você deve retornar o buffer ao BufferPool
        /// ANTES de retornar o packet ao PacketPool!
        /// 
        /// <code>
        /// // ✅ CORRETO:
        /// if (packet.Payload != null)
        /// {
        ///     BufferPool.Return(packet.Payload);
        ///     packet.Payload = null; // Limpa referência
        /// }
        /// PacketPool.Return(packet);
        /// 
        /// // ❌ ERRADO (perde referência do buffer):
        /// PacketPool.Return(packet); // Reset() limpa Payload!
        /// BufferPool.Return(packet.Payload); // packet.Payload já é null!
        /// </code>
        /// </remarks>
        public static void Return(Packet packet)
        {
            _pool.Return(packet);
        }

        /// <summary>
        /// Aluga um Packet do pool retornando um PooledObject para usar com 'using'.
        /// Garante retorno automático mesmo se houver exception.
        /// </summary>
        /// <returns>PooledObject que encapsula o Packet</returns>
        /// <remarks>
        /// Esta é a forma RECOMENDADA de usar o pool, pois garante que o packet
        /// será retornado mesmo se houver exception.
        /// </remarks>
        /// <example>
        /// <code>
        /// // ✅ RECOMENDADO:
        /// using (var rental = PacketPool.RentDisposable())
        /// {
        ///     var packet = rental.Value;
        ///     packet.Sequence = 100;
        ///     Send(packet);
        ///     
        ///     // Se Send() lançar exception, packet AINDA é retornado!
        /// } // Dispose() automático, retorna packet ao pool
        /// 
        /// // Com buffers:
        /// using (var rental = PacketPool.RentDisposable())
        /// {
        ///     var packet = rental.Value;
        ///     
        ///     var buffer = BufferPool.Rent(1024);
        ///     try
        ///     {
        ///         packet.Payload = buffer;
        ///         packet.PayloadSize = SerializeMessage(buffer);
        ///         Send(packet);
        ///     }
        ///     finally
        ///     {
        ///         BufferPool.Return(buffer);
        ///         packet.Payload = null; // Limpa referência
        ///     }
        /// } // Packet retornado automaticamente
        /// </code>
        /// </example>
        public static PooledObject<Packet> RentDisposable()
        {
            return _pool.RentDisposable();
        }

        /// <summary>
        /// Retorna estatísticas do pool para monitoring/debug.
        /// Útil para detectar memory leaks e ajustar configuração.
        /// </summary>
        /// <returns>Estatísticas atuais do pool</returns>
        /// <example>
        /// <code>
        /// var stats = PacketPool.GetStats();
        /// 
        /// Console.WriteLine($"Packets disponíveis: {stats.Available}");
        /// Console.WriteLine($"Total criados: {stats.TotalCreated}");
        /// Console.WriteLine($"Total alugados: {stats.TotalRented}");
        /// Console.WriteLine($"Total retornados: {stats.TotalReturned}");
        /// 
        /// // Alerta de leak:
        /// if (stats.PotentialLeaks > 100)
        /// {
        ///     Console.WriteLine($"⚠️ MEMORY LEAK: {stats.PotentialLeaks} packets não retornados!");
        /// }
        /// </code>
        /// </example>
        public static PoolStats GetStats()
        {
            return _pool.GetStats();
        }

        /// <summary>
        /// Limpa o pool completamente.
        /// Útil para testes ou shutdown do servidor.
        /// </summary>
        /// <remarks>
        /// CUIDADO: Após Clear(), todos os packets em uso ficam "órfãos".
        /// Só use isto durante shutdown ou em testes isolados.
        /// </remarks>
        public static void Clear()
        {
            _pool.Clear();
        }
    }
}