// XL4Net.Client/Prediction/InputBuffer.cs

using System.Collections.Generic;

namespace XL4Net.Client.Prediction
{
    /// <summary>
    /// Buffer circular que guarda comandos enviados ao servidor.
    /// Usado para reconciliation quando servidor confirma inputs.
    /// </summary>
    /// <remarks>
    /// Guarda ICommand (MoveCommand, etc) para poder re-executar
    /// durante reconciliation.
    /// </remarks>
    public class InputBuffer
    {
        // ============================================
        // CAMPOS PRIVADOS
        // ============================================

        private readonly ICommand[] _buffer;
        private readonly int _capacity;
        private int _head;  // Próxima posição para escrever
        private int _count; // Quantidade de elementos

        // ============================================
        // PROPRIEDADES
        // ============================================

        /// <summary>
        /// Quantidade de comandos no buffer.
        /// </summary>
        public int Count => _count;

        /// <summary>
        /// Capacidade máxima do buffer.
        /// </summary>
        public int Capacity => _capacity;

        /// <summary>
        /// Buffer está vazio?
        /// </summary>
        public bool IsEmpty => _count == 0;

        /// <summary>
        /// Buffer está cheio?
        /// </summary>
        public bool IsFull => _count >= _capacity;

        // ============================================
        // CONSTRUTOR
        // ============================================

        /// <summary>
        /// Cria buffer com capacidade especificada.
        /// </summary>
        /// <param name="capacity">Capacidade máxima (default: 64 = ~2 segundos @ 30Hz)</param>
        public InputBuffer(int capacity = 64)
        {
            _capacity = capacity > 0 ? capacity : 64;
            _buffer = new ICommand[_capacity];
            _head = 0;
            _count = 0;
        }

        // ============================================
        // MÉTODOS PÚBLICOS
        // ============================================

        /// <summary>
        /// Adiciona comando ao buffer.
        /// Se buffer estiver cheio, remove o mais antigo.
        /// </summary>
        public void Add(ICommand command)
        {
            if (command == null)
                return;

            // Adiciona no buffer circular
            _buffer[_head] = command;
            _head = (_head + 1) % _capacity;

            if (_count < _capacity)
            {
                _count++;
            }
            // Se cheio, o mais antigo foi sobrescrito automaticamente
        }

        /// <summary>
        /// Retorna todos os comandos no buffer (ordenados do mais antigo ao mais novo).
        /// </summary>
        public List<ICommand> GetAll()
        {
            var result = new List<ICommand>(_count);

            if (_count == 0)
                return result;

            // Calcula índice inicial (mais antigo)
            int startIndex = (_head - _count + _capacity) % _capacity;

            for (int i = 0; i < _count; i++)
            {
                int index = (startIndex + i) % _capacity;
                if (_buffer[index] != null)
                {
                    result.Add(_buffer[index]);
                }
            }

            return result;
        }

        /// <summary>
        /// Retorna comandos com SequenceNumber maior que o especificado.
        /// Usado para obter inputs pendentes após reconciliation.
        /// </summary>
        /// <param name="afterSequence">Último sequence confirmado pelo servidor</param>
        /// <returns>Lista de comandos pendentes (ordenados)</returns>
        public List<ICommand> GetAfter(uint afterSequence)
        {
            var result = new List<ICommand>();

            if (_count == 0)
                return result;

            int startIndex = (_head - _count + _capacity) % _capacity;

            for (int i = 0; i < _count; i++)
            {
                int index = (startIndex + i) % _capacity;
                var cmd = _buffer[index];

                if (cmd != null && cmd.SequenceNumber > afterSequence)
                {
                    result.Add(cmd);
                }
            }

            return result;
        }

        /// <summary>
        /// Remove comandos com SequenceNumber menor ou igual ao especificado.
        /// Chamado após servidor confirmar inputs.
        /// </summary>
        /// <param name="upToSequence">Último sequence confirmado</param>
        /// <returns>Quantidade de comandos removidos</returns>
        public int RemoveUpTo(uint upToSequence)
        {
            if (_count == 0)
                return 0;

            int removed = 0;
            int startIndex = (_head - _count + _capacity) % _capacity;

            // Conta quantos remover
            for (int i = 0; i < _count; i++)
            {
                int index = (startIndex + i) % _capacity;
                var cmd = _buffer[index];

                if (cmd != null && cmd.SequenceNumber <= upToSequence)
                {
                    _buffer[index] = null; // Limpa referência
                    removed++;
                }
                else
                {
                    break; // Buffer está ordenado, pode parar
                }
            }

            // Ajusta count
            _count -= removed;

            return removed;
        }

        /// <summary>
        /// Busca comando por SequenceNumber.
        /// </summary>
        /// <returns>Comando encontrado ou null</returns>
        public ICommand GetBySequence(uint sequence)
        {
            if (_count == 0)
                return null;

            int startIndex = (_head - _count + _capacity) % _capacity;

            for (int i = 0; i < _count; i++)
            {
                int index = (startIndex + i) % _capacity;
                var cmd = _buffer[index];

                if (cmd != null && cmd.SequenceNumber == sequence)
                {
                    return cmd;
                }
            }

            return null;
        }

        /// <summary>
        /// Busca comando por Tick.
        /// </summary>
        public ICommand GetByTick(uint tick)
        {
            if (_count == 0)
                return null;

            int startIndex = (_head - _count + _capacity) % _capacity;

            for (int i = 0; i < _count; i++)
            {
                int index = (startIndex + i) % _capacity;
                var cmd = _buffer[index];

                if (cmd != null && cmd.Tick == tick)
                {
                    return cmd;
                }
            }

            return null;
        }

        /// <summary>
        /// Limpa todo o buffer.
        /// </summary>
        public void Clear()
        {
            for (int i = 0; i < _capacity; i++)
            {
                _buffer[i] = null;
            }
            _count = 0;
            _head = 0;
        }

        // ============================================
        // DEBUG
        // ============================================

        public override string ToString()
        {
            if (_count == 0)
                return "InputBuffer[Empty]";

            var all = GetAll();
            var first = all[0];
            var last = all[all.Count - 1];

            return $"InputBuffer[Count={_count}/{_capacity}, Seq={first.SequenceNumber}-{last.SequenceNumber}]";
        }
    }
}