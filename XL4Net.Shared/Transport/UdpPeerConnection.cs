// XL4Net.Shared/Transport/UdpPeerConnection.cs

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace XL4Net.Shared.Transport
{
    /// <summary>
    /// Representa uma "conexão virtual" UDP com um peer específico.
    /// 
    /// UDP não tem conexões reais, então usamos IPEndPoint para identificar o peer.
    /// Esta classe encapsula a lógica de envio/recebimento para um endpoint específico.
    /// </summary>
    /// <remarks>
    /// Diferenças em relação ao TcpConnection:
    /// - Não tem NetworkStream (UDP usa datagramas)
    /// - Não tem método StartReceiving() (servidor recebe de todos os peers num único socket)
    /// - Identificação é por IPEndPoint, não por socket dedicado
    /// 
    /// Baseado no conceito de NetPeer do LiteNetLib.
    /// </remarks>
    public class UdpPeerConnection
    {
        #region Campos Privados

        private readonly UdpClient _udpClient;
        private readonly IPEndPoint _remoteEndPoint;
        private readonly int _connectionId;
        private bool _isDisposed;

        #endregion

        #region Propriedades

        /// <summary>
        /// ID único desta conexão.
        /// </summary>
        public int ConnectionId => _connectionId;

        /// <summary>
        /// Endpoint remoto deste peer (IP:porta).
        /// </summary>
        public IPEndPoint RemoteEndPoint => _remoteEndPoint;

        /// <summary>
        /// Indica se a conexão está ativa.
        /// </summary>
        public bool IsConnected { get; private set; }

        #endregion

        #region Eventos

        /// <summary>
        /// Disparado quando a conexão é encerrada.
        /// </summary>
        public event Action<int>? OnDisconnected;

        #endregion

        #region Construtor

        /// <summary>
        /// Cria uma nova conexão UDP virtual.
        /// </summary>
        /// <param name="udpClient">Socket UDP compartilhado</param>
        /// <param name="remoteEndPoint">Endpoint do peer remoto</param>
        /// <param name="connectionId">ID único desta conexão</param>
        public UdpPeerConnection(UdpClient udpClient, IPEndPoint remoteEndPoint, int connectionId)
        {
            _udpClient = udpClient ?? throw new ArgumentNullException(nameof(udpClient));
            _remoteEndPoint = remoteEndPoint ?? throw new ArgumentNullException(nameof(remoteEndPoint));
            _connectionId = connectionId;
            IsConnected = true;
        }

        #endregion

        #region Métodos Públicos

        /// <summary>
        /// Envia dados para o peer remoto.
        /// </summary>
        /// <param name="data">Dados a enviar</param>
        /// <returns>True se enviou com sucesso</returns>
        /// <remarks>
        /// IMPORTANTE: UDP é unreliable! Não há garantia de entrega.
        /// O sistema de ACK/resend será implementado em camada superior (Fase seguinte).
        /// </remarks>
        public async Task<bool> SendAsync(byte[] data)
        {
            if (_isDisposed || !IsConnected)
                return false;

            if (data == null || data.Length == 0)
                return false;

            try
            {
                // Envia datagrama para o endpoint específico
                int bytesSent = await _udpClient.SendAsync(data, data.Length, _remoteEndPoint).ConfigureAwait(false);

                return bytesSent == data.Length;
            }
            catch (SocketException ex)
            {
                // Erros comuns UDP:
                // - ICMP Destination Unreachable (peer offline)
                // - Network unreachable
                Console.WriteLine($"[UdpPeerConnection] Send error to {_remoteEndPoint}: {ex.Message}");
                return false;
            }
            catch (ObjectDisposedException)
            {
                // Socket foi fechado
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UdpPeerConnection] Unexpected send error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Desconecta este peer (marca como desconectado).
        /// </summary>
        /// <remarks>
        /// Não fecha o socket UDP (é compartilhado), apenas marca como desconectado.
        /// </remarks>
        public void Disconnect()
        {
            if (!IsConnected)
                return;

            IsConnected = false;
            OnDisconnected?.Invoke(_connectionId);
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            Disconnect();
        }

        #endregion

        #region Override

        public override string ToString()
        {
            return $"UdpPeer[Id={_connectionId}, Endpoint={_remoteEndPoint}, Connected={IsConnected}]";
        }

        #endregion
    }
}