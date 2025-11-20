// XL4Net.Shared/Pooling/ObjectPool.cs

using System;
using System.Collections.Concurrent;

namespace XL4Net.Shared.Pooling
{
    /// <summary>
    /// Pool genérico thread-safe para reutilização de objetos.
    /// Reduz alocações e pressão no Garbage Collector.
    /// </summary>
    /// <typeparam name="T">Tipo do objeto a ser poolado (deve ser class com construtor padrão)</typeparam>
    /// <remarks>
    /// Usa ConcurrentBag para operações thread-safe sem locks.
    /// Faz "warmup" inicial criando objetos antecipadamente.
    /// Limita tamanho máximo para evitar crescimento infinito.
    /// </remarks>
    public class ObjectPool<T> where T : class, new()
    {
        // ConcurrentBag: thread-safe, otimizado para o padrão rent/return
        // Cada thread tem uma "bolsinha" local, minimizando contenção
        private readonly ConcurrentBag<T> _objects;

        private readonly int _maxSize;

        // Métricas para debug/monitoring (opcional mas útil)
        private int _totalCreated;
        private int _totalRented;
        private int _totalReturned;

        /// <summary>
        /// Quantidade de objetos disponíveis no pool no momento.
        /// </summary>
        public int AvailableCount => _objects.Count;

        /// <summary>
        /// Total de objetos criados desde o início (inclui os que não retornaram).
        /// </summary>
        public int TotalCreated => _totalCreated;

        /// <summary>
        /// Total de vezes que Rent() foi chamado.
        /// </summary>
        public int TotalRented => _totalRented;

        /// <summary>
        /// Total de vezes que Return() foi chamado.
        /// </summary>
        public int TotalReturned => _totalReturned;

        /// <summary>
        /// Objetos que foram alugados mas nunca devolvidos (possível memory leak).
        /// </summary>
        public int PotentialLeaks => _totalCreated - _objects.Count;

        /// <summary>
        /// Cria um novo ObjectPool.
        /// </summary>
        /// <param name="initialSize">Quantidade inicial de objetos (warmup)</param>
        /// <param name="maxSize">Tamanho máximo do pool (objetos excedentes são descartados)</param>
        public ObjectPool(int initialSize = 32, int maxSize = 1024)
        {
            if (initialSize < 0)
                throw new ArgumentOutOfRangeException(nameof(initialSize), "Initial size cannot be negative");

            if (maxSize < initialSize)
                throw new ArgumentOutOfRangeException(nameof(maxSize), "Max size must be >= initial size");

            _maxSize = maxSize;
            _objects = new ConcurrentBag<T>();

            // Warmup: cria objetos antecipadamente
            // Isso evita alocações durante o gameplay
            for (int i = 0; i < initialSize; i++)
            {
                _objects.Add(new T());
                _totalCreated++;
            }
        }

        /// <summary>
        /// Aluga um objeto do pool.
        /// Se o pool estiver vazio, cria um novo objeto.
        /// </summary>
        /// <returns>Objeto pronto para uso (pode ser reciclado ou novo)</returns>
        public T Rent()
        {
            _totalRented++;

            // Tenta pegar do pool
            if (_objects.TryTake(out var obj))
            {
                return obj;
            }

            // Pool vazio, cria novo
            _totalCreated++;
            return new T();
        }

        /// <summary>
        /// Retorna um objeto ao pool para reutilização.
        /// Se o objeto implementa IPoolable, chama Reset() automaticamente.
        /// Se o pool estiver cheio (>= maxSize), descarta o objeto.
        /// </summary>
        /// <param name="obj">Objeto a ser retornado</param>
        public void Return(T obj)
        {
            if (obj == null)
                return; // Ignora null silenciosamente

            _totalReturned++;

            // Verifica limite de tamanho
            if (_objects.Count >= _maxSize)
            {
                // Pool cheio, descarta objeto
                // Ele será coletado pelo GC eventualmente
                return;
            }

            // Se implementa IPoolable, reseta estado
            if (obj is IPoolable poolable)
            {
                poolable.Reset();
            }

            // Retorna ao pool
            _objects.Add(obj);
        }

        /// <summary>
        /// Limpa o pool completamente.
        /// Útil para testes ou shutdown.
        /// </summary>
        public void Clear()
        {
            while (_objects.TryTake(out _))
            {
                // Esvazia o bag
            }
        }

        /// <summary>
        /// Retorna estatísticas do pool para debug/monitoring.
        /// </summary>
        public PoolStats GetStats()
        {
            return new PoolStats
            {
                Available = AvailableCount,
                TotalCreated = TotalCreated,
                TotalRented = TotalRented,
                TotalReturned = TotalReturned,
                PotentialLeaks = PotentialLeaks
            };
        }
    }

    /// <summary>
    /// Estatísticas de um ObjectPool para monitoring.
    /// </summary>
    public struct PoolStats
    {
        public int Available { get; set; }
        public int TotalCreated { get; set; }
        public int TotalRented { get; set; }
        public int TotalReturned { get; set; }
        public int PotentialLeaks { get; set; }

        public override string ToString()
        {
            return $"Pool Stats: Available={Available}, Created={TotalCreated}, " +
                   $"Rented={TotalRented}, Returned={TotalReturned}, Leaks={PotentialLeaks}";
        }
    }
}