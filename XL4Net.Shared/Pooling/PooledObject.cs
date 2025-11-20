// XL4Net.Shared/Pooling/PooledObject.cs

using System;

namespace XL4Net.Shared.Pooling
{
    /// <summary>
    /// Wrapper para usar objetos poolados com 'using' statement.
    /// Garante que o objeto seja retornado ao pool automaticamente,
    /// mesmo se houver exception.
    /// </summary>
    /// <typeparam name="T">Tipo do objeto poolado</typeparam>
    /// <remarks>
    /// É um struct (não class) para evitar alocação extra.
    /// Implementa IDisposable para funcionar com 'using'.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Uso recomendado:
    /// using (var rental = pool.RentDisposable())
    /// {
    ///     var packet = rental.Value;
    ///     packet.Sequence = 100;
    ///     Send(packet);
    ///     
    ///     // Se Send() lançar exception, packet AINDA é retornado!
    /// } // ← Dispose() automático aqui, retorna ao pool
    /// 
    /// // Uso ERRADO (sem using):
    /// var packet = pool.Rent();
    /// packet.Sequence = 100;
    /// Send(packet); // Se exception aqui, NUNCA retorna! (LEAK)
    /// pool.Return(packet); // Pode não executar!
    /// </code>
    /// </example>
    public readonly struct PooledObject<T> : IDisposable where T : class, new()
    {
        private readonly ObjectPool<T> _pool;

        /// <summary>
        /// O objeto alugado do pool.
        /// </summary>
        public T Value { get; }

        /// <summary>
        /// Cria um PooledObject.
        /// Normalmente você não chama isso diretamente, usa pool.RentDisposable().
        /// </summary>
        /// <param name="pool">Pool de onde o objeto veio</param>
        /// <param name="value">Objeto alugado</param>
        public PooledObject(ObjectPool<T> pool, T value)
        {
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// Retorna o objeto ao pool automaticamente.
        /// Chamado pelo 'using' statement no final do bloco.
        /// </summary>
        public void Dispose()
        {
            // Retorna ao pool (se pool não for null)
            _pool?.Return(Value);
        }
    }

    /// <summary>
    /// Extension methods para facilitar o uso de PooledObject.
    /// </summary>
    public static class ObjectPoolExtensions
    {
        /// <summary>
        /// Aluga um objeto do pool retornando um PooledObject para usar com 'using'.
        /// Garante retorno automático mesmo com exceptions.
        /// </summary>
        /// <typeparam name="T">Tipo do objeto</typeparam>
        /// <param name="pool">Pool de onde alugar</param>
        /// <returns>PooledObject para usar com 'using'</returns>
        /// <example>
        /// <code>
        /// using (var rental = myPool.RentDisposable())
        /// {
        ///     var obj = rental.Value;
        ///     // Usa obj...
        /// } // Auto-return aqui
        /// </code>
        /// </example>
        public static PooledObject<T> RentDisposable<T>(this ObjectPool<T> pool) where T : class, new()
        {
            if (pool == null)
                throw new ArgumentNullException(nameof(pool));

            var obj = pool.Rent();
            return new PooledObject<T>(pool, obj);
        }
    }
}