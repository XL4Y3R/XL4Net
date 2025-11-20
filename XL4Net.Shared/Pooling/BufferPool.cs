// XL4Net.Shared/Pooling/BufferPool.cs

using System;
using System.Collections.Concurrent;

namespace XL4Net.Shared.Pooling
{
    /// <summary>
    /// Pool estático de byte arrays organizados por tamanho.
    /// Minimiza alocações e fragmentação de memória.
    /// </summary>
    /// <remarks>
    /// Buffers são organizados em "buckets" por tamanho:
    /// - Pequeno: 256 bytes  (headers, mensagens pequenas)
    /// - Médio:   1024 bytes (maioria das mensagens)
    /// - Grande:  4096 bytes (mensagens grandes, fragmentação)
    /// - Huge:    16384 bytes (casos especiais, snapshots)
    /// 
    /// Algoritmo de seleção:
    /// - Solicita X bytes → Retorna o menor bucket que cabe X
    /// - Exemplo: Rent(500) → Retorna buffer de 1024 bytes
    /// 
    /// IMPORTANTE: Ao retornar, informe o tamanho REAL do buffer,
    /// não o tamanho que você usou!
    /// 
    /// Performance:
    /// - Zero alocações após warmup
    /// - Zero fragmentação (tamanhos fixos)
    /// - Thread-safe (ConcurrentBag)
    /// </remarks>
    public static class BufferPool
    {
        // Tamanhos dos buckets (potências de 2 para alinhamento)
        private const int SIZE_SMALL = 256;      // ~256 bytes
        private const int SIZE_MEDIUM = 1024;    // ~1 KB
        private const int SIZE_LARGE = 4096;     // ~4 KB
        private const int SIZE_HUGE = 16384;     // ~16 KB

        // Pools separados por tamanho
        // ConcurrentBag é thread-safe sem locks
        private static readonly ConcurrentBag<byte[]> _poolSmall = new();
        private static readonly ConcurrentBag<byte[]> _poolMedium = new();
        private static readonly ConcurrentBag<byte[]> _poolLarge = new();
        private static readonly ConcurrentBag<byte[]> _poolHuge = new();

        // Limites de cada pool
        private const int MAX_POOL_SIZE_SMALL = 128;   // 128 × 256B = 32 KB
        private const int MAX_POOL_SIZE_MEDIUM = 64;   // 64 × 1KB = 64 KB
        private const int MAX_POOL_SIZE_LARGE = 32;    // 32 × 4KB = 128 KB
        private const int MAX_POOL_SIZE_HUGE = 16;     // 16 × 16KB = 256 KB
                                                       // Total máximo em pools: ~480 KB

        // Métricas (para debug/monitoring)
        private static int _totalSmallCreated = 0;
        private static int _totalMediumCreated = 0;
        private static int _totalLargeCreated = 0;
        private static int _totalHugeCreated = 0;

        private static int _totalSmallRented = 0;
        private static int _totalMediumRented = 0;
        private static int _totalLargeRented = 0;
        private static int _totalHugeRented = 0;

        // Static constructor para warmup
        static BufferPool()
        {
            Warmup();
        }

        /// <summary>
        /// Warmup: cria buffers antecipadamente para evitar alocações no primeiro frame.
        /// </summary>
        private static void Warmup()
        {
            // Small: usado frequentemente (headers, pequenas mensagens)
            for (int i = 0; i < 32; i++)
            {
                _poolSmall.Add(new byte[SIZE_SMALL]);
                _totalSmallCreated++;
            }

            // Medium: maioria das mensagens
            for (int i = 0; i < 16; i++)
            {
                _poolMedium.Add(new byte[SIZE_MEDIUM]);
                _totalMediumCreated++;
            }

            // Large: mensagens grandes, menos frequente
            for (int i = 0; i < 8; i++)
            {
                _poolLarge.Add(new byte[SIZE_LARGE]);
                _totalLargeCreated++;
            }

            // Huge: raramente usado
            for (int i = 0; i < 4; i++)
            {
                _poolHuge.Add(new byte[SIZE_HUGE]);
                _totalHugeCreated++;
            }
        }

        /// <summary>
        /// Aluga um buffer com tamanho mínimo especificado.
        /// Retorna o menor buffer que satisfaz minSize.
        /// </summary>
        /// <param name="minSize">Tamanho mínimo necessário em bytes</param>
        /// <returns>Buffer (pode ser maior que minSize)</returns>
        /// <exception cref="ArgumentOutOfRangeException">Se minSize for negativo ou muito grande</exception>
        /// <remarks>
        /// IMPORTANTE: O buffer retornado pode ser MAIOR que minSize.
        /// Exemplo: Rent(500) retorna buffer de 1024 bytes.
        /// 
        /// Você DEVE retornar informando o tamanho REAL do buffer:
        /// <code>
        /// var buffer = BufferPool.Rent(500);
        /// // buffer.Length = 1024 (não 500!)
        /// 
        /// // Usa buffer...
        /// 
        /// BufferPool.Return(buffer, buffer.Length); // ← Tamanho REAL!
        /// </code>
        /// </remarks>
        /// <example>
        /// <code>
        /// // Alugar buffer
        /// var buffer = BufferPool.Rent(100);
        /// Console.WriteLine($"Recebi buffer de {buffer.Length} bytes");
        /// 
        /// // Usar buffer
        /// int bytesWritten = SerializeMessage(buffer);
        /// Send(buffer, bytesWritten);
        /// 
        /// // Retornar buffer (tamanho REAL, não bytesWritten!)
        /// BufferPool.Return(buffer, buffer.Length);
        /// </code>
        /// </example>
        public static byte[] Rent(int minSize)
        {
            if (minSize < 0)
                throw new ArgumentOutOfRangeException(nameof(minSize), "Size cannot be negative");

            if (minSize > SIZE_HUGE)
            {
                // Muito grande para pool, aloca direto
                // Não será poolado (será coletado pelo GC)
                return new byte[minSize];
            }

            // Seleciona pool apropriado
            if (minSize <= SIZE_SMALL)
            {
                _totalSmallRented++;
                if (_poolSmall.TryTake(out var buffer))
                    return buffer;

                _totalSmallCreated++;
                return new byte[SIZE_SMALL];
            }

            if (minSize <= SIZE_MEDIUM)
            {
                _totalMediumRented++;
                if (_poolMedium.TryTake(out var buffer))
                    return buffer;

                _totalMediumCreated++;
                return new byte[SIZE_MEDIUM];
            }

            if (minSize <= SIZE_LARGE)
            {
                _totalLargeRented++;
                if (_poolLarge.TryTake(out var buffer))
                    return buffer;

                _totalLargeCreated++;
                return new byte[SIZE_LARGE];
            }

            // SIZE_HUGE
            _totalHugeRented++;
            if (_poolHuge.TryTake(out var hugeBuffer))
                return hugeBuffer;

            _totalHugeCreated++;
            return new byte[SIZE_HUGE];
        }

        /// <summary>
        /// Retorna um buffer ao pool apropriado.
        /// </summary>
        /// <param name="buffer">Buffer a ser retornado</param>
        /// <param name="bufferSize">Tamanho REAL do buffer (buffer.Length)</param>
        /// <remarks>
        /// CRÍTICO: Informe o tamanho REAL do buffer (buffer.Length), não o tamanho usado!
        /// 
        /// <code>
        /// // ✅ CORRETO:
        /// var buffer = BufferPool.Rent(500);
        /// // buffer.Length = 1024
        /// BufferPool.Return(buffer, buffer.Length); // ← 1024, não 500!
        /// 
        /// // ❌ ERRADO:
        /// var buffer = BufferPool.Rent(500);
        /// int used = 300;
        /// BufferPool.Return(buffer, used); // ← 300? ERRADO! Deve ser 1024!
        /// </code>
        /// 
        /// Se o pool estiver cheio (acima do limite), o buffer é descartado (GC limpa).
        /// Isso evita crescimento infinito se houver leaks.
        /// </remarks>
        public static void Return(byte[] buffer, int bufferSize)
        {
            if (buffer == null)
                return;

            // Limpa buffer (segurança - evita leak de dados)
            // Comentado por performance, descomente se necessário:
            // Array.Clear(buffer, 0, buffer.Length);

            // Retorna ao pool apropriado
            if (bufferSize == SIZE_SMALL)
            {
                if (_poolSmall.Count < MAX_POOL_SIZE_SMALL)
                    _poolSmall.Add(buffer);
                return;
            }

            if (bufferSize == SIZE_MEDIUM)
            {
                if (_poolMedium.Count < MAX_POOL_SIZE_MEDIUM)
                    _poolMedium.Add(buffer);
                return;
            }

            if (bufferSize == SIZE_LARGE)
            {
                if (_poolLarge.Count < MAX_POOL_SIZE_LARGE)
                    _poolLarge.Add(buffer);
                return;
            }

            if (bufferSize == SIZE_HUGE)
            {
                if (_poolHuge.Count < MAX_POOL_SIZE_HUGE)
                    _poolHuge.Add(buffer);
                return;
            }

            // Tamanho não reconhecido ou muito grande, descarta
            // GC vai coletar
        }

        /// <summary>
        /// Retorna um buffer ao pool (versão simplificada que detecta tamanho).
        /// Menos eficiente que Return(buffer, size), use quando possível.
        /// </summary>
        /// <param name="buffer">Buffer a ser retornado</param>
        public static void Return(byte[] buffer)
        {
            if (buffer == null)
                return;

            Return(buffer, buffer.Length);
        }

        /// <summary>
        /// Retorna estatísticas dos pools para monitoring/debug.
        /// </summary>
        public static BufferPoolStats GetStats()
        {
            return new BufferPoolStats
            {
                SmallAvailable = _poolSmall.Count,
                SmallCreated = _totalSmallCreated,
                SmallRented = _totalSmallRented,

                MediumAvailable = _poolMedium.Count,
                MediumCreated = _totalMediumCreated,
                MediumRented = _totalMediumRented,

                LargeAvailable = _poolLarge.Count,
                LargeCreated = _totalLargeCreated,
                LargeRented = _totalLargeRented,

                HugeAvailable = _poolHuge.Count,
                HugeCreated = _totalHugeCreated,
                HugeRented = _totalHugeRented
            };
        }

        /// <summary>
        /// Limpa todos os pools.
        /// CUIDADO: Só use durante shutdown ou testes!
        /// </summary>
        public static void Clear()
        {
            while (_poolSmall.TryTake(out _)) { }
            while (_poolMedium.TryTake(out _)) { }
            while (_poolLarge.TryTake(out _)) { }
            while (_poolHuge.TryTake(out _)) { }
        }
    }

    /// <summary>
    /// Estatísticas dos pools de buffers.
    /// </summary>
    public struct BufferPoolStats
    {
        // Small (256 bytes)
        public int SmallAvailable { get; set; }
        public int SmallCreated { get; set; }
        public int SmallRented { get; set; }
        public int SmallLeaks => SmallCreated - SmallAvailable;

        // Medium (1024 bytes)
        public int MediumAvailable { get; set; }
        public int MediumCreated { get; set; }
        public int MediumRented { get; set; }
        public int MediumLeaks => MediumCreated - MediumAvailable;

        // Large (4096 bytes)
        public int LargeAvailable { get; set; }
        public int LargeCreated { get; set; }
        public int LargeRented { get; set; }
        public int LargeLeaks => LargeCreated - LargeAvailable;

        // Huge (16384 bytes)
        public int HugeAvailable { get; set; }
        public int HugeCreated { get; set; }
        public int HugeRented { get; set; }
        public int HugeLeaks => HugeCreated - HugeAvailable;

        // Totais
        public int TotalAvailable => SmallAvailable + MediumAvailable + LargeAvailable + HugeAvailable;
        public int TotalCreated => SmallCreated + MediumCreated + LargeCreated + HugeCreated;
        public int TotalRented => SmallRented + MediumRented + LargeRented + HugeRented;
        public int TotalLeaks => SmallLeaks + MediumLeaks + LargeLeaks + HugeLeaks;

        public override string ToString()
        {
            return $"BufferPool Stats:\n" +
                   $"  Small (256B):  Available={SmallAvailable}, Created={SmallCreated}, Leaks={SmallLeaks}\n" +
                   $"  Medium (1KB):  Available={MediumAvailable}, Created={MediumCreated}, Leaks={MediumLeaks}\n" +
                   $"  Large (4KB):   Available={LargeAvailable}, Created={LargeCreated}, Leaks={LargeLeaks}\n" +
                   $"  Huge (16KB):   Available={HugeAvailable}, Created={HugeCreated}, Leaks={HugeLeaks}\n" +
                   $"  TOTAL:         Available={TotalAvailable}, Created={TotalCreated}, Leaks={TotalLeaks}";
        }
    }
}