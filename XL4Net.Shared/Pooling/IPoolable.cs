// XL4Net.Shared/Pooling/IPoolable.cs

namespace XL4Net.Shared.Pooling
{
    /// <summary>
    /// Interface para objetos que podem ser reutilizados em um ObjectPool.
    /// Implementar Reset() para limpar o estado do objeto antes de reutilizá-lo.
    /// </summary>
    public interface IPoolable
    {
        /// <summary>
        /// Reseta o estado do objeto para valores padrão.
        /// Chamado automaticamente quando o objeto é retornado ao pool.
        /// </summary>
        /// <example>
        /// <code>
        /// public void Reset()
        /// {
        ///     // Limpa campos
        ///     Sequence = 0;
        ///     Data = null;
        ///     
        ///     // Limpa coleções
        ///     _messages.Clear();
        /// }
        /// </code>
        /// </example>
        void Reset();
    }
}