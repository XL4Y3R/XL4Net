using System;
using System.Threading.Tasks;

namespace XL4Net.Shared.Transport
{
    /// <summary>
    /// Interface base para implementações de transport (TCP, UDP, etc).
    /// Define o contrato que qualquer transport deve seguir.
    /// </summary>
    public interface ITransport
    {
        #region Propriedades

        /// <summary>
        /// Indica se o transport está conectado.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Latência estimada em milissegundos (RTT).
        /// </summary>
        int Latency { get; }

        #endregion

        #region Eventos

        /// <summary>
        /// Disparado quando conecta com sucesso.
        /// </summary>
        event Action OnConnected;

        /// <summary>
        /// Disparado quando desconecta.
        /// Parâmetro: razão da desconexão
        /// </summary>
        event Action<string> OnDisconnected;

        /// <summary>
        /// Disparado quando recebe um packet.
        /// Parâmetro: packet recebido (deve ser retornado ao pool após uso)
        /// </summary>
        event Action<Packet> OnPacketReceived;

        /// <summary>
        /// Disparado quando ocorre um erro.
        /// Parâmetro: mensagem de erro
        /// </summary>
        event Action<string> OnError;

        #endregion

        #region Métodos

        /// <summary>
        /// Inicia o transport (cliente conecta, servidor começa a escutar).
        /// </summary>
        /// <returns>True se iniciou com sucesso</returns>
        Task<bool> StartAsync();

        /// <summary>
        /// Para o transport e fecha conexões.
        /// </summary>
        Task StopAsync();

        /// <summary>
        /// Envia um packet.
        /// IMPORTANTE: O packet será retornado ao pool automaticamente após envio.
        /// </summary>
        /// <param name="packet">Packet a enviar</param>
        /// <returns>True se enviou com sucesso</returns>
        Task<bool> SendAsync(Packet packet);

        /// <summary>
        /// Processa mensagens enfileiradas (deve ser chamado no game loop).
        /// </summary>
        void ProcessIncoming();

        #endregion
    }
}