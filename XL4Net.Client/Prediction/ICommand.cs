// XL4Net.Client/Prediction/ICommand.cs

using XL4Net.Shared.Prediction;

namespace XL4Net.Client.Prediction
{
    /// <summary>
    /// Interface base para comandos de input.
    /// Representa uma ação do jogador que pode ser executada localmente (prediction)
    /// e enviada ao servidor.
    /// </summary>
    /// <remarks>
    /// Implementado por:
    /// - MoveCommand (movimento + pulo + sprint)
    /// - Futuros: AttackCommand, UseItemCommand, etc.
    /// 
    /// O padrão Command permite:
    /// 1. Executar localmente (client-side prediction)
    /// 2. Serializar e enviar ao servidor
    /// 3. Guardar no buffer para reconciliation
    /// 4. Re-executar durante reconciliation
    /// </remarks>
    public interface ICommand
    {
        /// <summary>
        /// Tick do servidor quando comando foi criado.
        /// </summary>
        uint Tick { get; }

        /// <summary>
        /// Número sequencial único do comando.
        /// Usado para identificar inputs confirmados pelo servidor.
        /// </summary>
        uint SequenceNumber { get; }

        /// <summary>
        /// Executa o comando no estado atual.
        /// </summary>
        /// <param name="currentState">Estado atual antes do comando</param>
        /// <param name="deltaTime">Delta time do tick</param>
        /// <returns>Novo estado após executar comando</returns>
        StateSnapshot Execute(StateSnapshot currentState, float deltaTime);

        /// <summary>
        /// Converte para InputData para enviar ao servidor.
        /// </summary>
        /// <returns>InputData serializado</returns>
        InputData ToInputData();
    }
}