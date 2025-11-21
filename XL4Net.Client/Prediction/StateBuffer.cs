// XL4Net.Client/Prediction/StateBuffer.cs

using System.Collections.Generic;
using XL4Net.Shared.Prediction;

namespace XL4Net.Client.Prediction
{
    /// <summary>
    /// Buffer circular que guarda histórico de estados.
    /// Usado para comparar predições com estados do servidor.
    /// </summary>
    public class StateBuffer
    {
        // ============================================
        // CAMPOS PRIVADOS
        // ============================================

        private readonly StateSnapshot[] _buffer;
        private readonly int _capacity;
        private int _head;
        private int _count;

        // ============================================
        // PROPRIEDADES
        // ============================================

        /// <summary>
        /// Quantidade de estados no buffer.
        /// </summary>
        public int Count => _count;

        /// <summary>
        /// Capacidade máxima.
        /// </summary>
        public int Capacity => _capacity;

        /// <summary>
        /// Buffer vazio?
        /// </summary>
        public bool IsEmpty => _count == 0;

        // ============================================
        // CONSTRUTOR
        // ============================================

        /// <summary>
        /// Cria buffer com capacidade especificada.
        /// </summary>
        public StateBuffer(int capacity = 64)
        {
            _capacity = capacity > 0 ? capacity : 64;
            _buffer = new StateSnapshot[_capacity];
            _head = 0;
            _count = 0;
        }

        // ============================================
        // MÉTODOS PÚBLICOS
        // ============================================

        /// <summary>
        /// Adiciona estado ao buffer.
        /// </summary>
        public void Add(StateSnapshot state)
        {
            _buffer[_head] = state;
            _head = (_head + 1) % _capacity;

            if (_count < _capacity)
            {
                _count++;
            }
        }

        /// <summary>
        /// Tenta obter estado por Tick.
        /// </summary>
        /// <param name="tick">Tick desejado</param>
        /// <param name="state">Estado encontrado (se houver)</param>
        /// <returns>True se encontrou</returns>
        public bool TryGet(uint tick, out StateSnapshot state)
        {
            state = default;

            if (_count == 0)
                return false;

            int startIndex = (_head - _count + _capacity) % _capacity;

            for (int i = 0; i < _count; i++)
            {
                int index = (startIndex + i) % _capacity;
                if (_buffer[index].Tick == tick)
                {
                    state = _buffer[index];
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Obtém estado por Tick (ou default se não encontrar).
        /// </summary>
        public StateSnapshot Get(uint tick)
        {
            TryGet(tick, out var state);
            return state;
        }

        /// <summary>
        /// Obtém o estado mais recente.
        /// </summary>
        public StateSnapshot GetLatest()
        {
            if (_count == 0)
                return default;

            int latestIndex = (_head - 1 + _capacity) % _capacity;
            return _buffer[latestIndex];
        }

        /// <summary>
        /// Remove estados com Tick menor ou igual ao especificado.
        /// </summary>
        public int RemoveUpTo(uint tick)
        {
            if (_count == 0)
                return 0;

            int removed = 0;
            int startIndex = (_head - _count + _capacity) % _capacity;

            for (int i = 0; i < _count; i++)
            {
                int index = (startIndex + i) % _capacity;
                if (_buffer[index].Tick <= tick)
                {
                    removed++;
                }
                else
                {
                    break;
                }
            }

            _count -= removed;
            return removed;
        }

        /// <summary>
        /// Retorna todos os estados (para debug).
        /// </summary>
        public List<StateSnapshot> GetAll()
        {
            var result = new List<StateSnapshot>(_count);

            if (_count == 0)
                return result;

            int startIndex = (_head - _count + _capacity) % _capacity;

            for (int i = 0; i < _count; i++)
            {
                int index = (startIndex + i) % _capacity;
                result.Add(_buffer[index]);
            }

            return result;
        }

        /// <summary>
        /// Limpa buffer.
        /// </summary>
        public void Clear()
        {
            _count = 0;
            _head = 0;
        }

        public override string ToString()
        {
            if (_count == 0)
                return "StateBuffer[Empty]";

            var all = GetAll();
            return $"StateBuffer[Count={_count}/{_capacity}, Tick={all[0].Tick}-{all[all.Count - 1].Tick}]";
        }
    }
}