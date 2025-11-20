using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace XL4Net.Shared.Transport
{
    /// <summary>
    /// Representa uma conexão TCP individual.
    /// Usado tanto pelo cliente (conexão com servidor) quanto pelo servidor (uma conexão por cliente).
    /// </summary>
    public class TcpConnection : IDisposable
    {
        #region Campos Privados

        private readonly TcpClient _tcpClient;
        private readonly NetworkStream _stream;
        private readonly int _connectionId;
        private readonly CancellationTokenSource _cts;

        private bool _isDisposed;

        #endregion

        #region Propriedades

        /// <summary>
        /// ID único desta conexão.
        /// </summary>
        public int ConnectionId => _connectionId;

        /// <summary>
        /// Indica se a conexão está ativa.
        /// </summary>
        public bool IsConnected => _tcpClient?.Connected ?? false;

        /// <summary>
        /// Endereço remoto (IP:porta).
        /// </summary>
        public string RemoteEndPoint => _tcpClient?.Client?.RemoteEndPoint?.ToString() ?? "Unknown";

        #endregion

        #region Eventos

        /// <summary>
        /// Disparado quando recebe dados completos (um packet).
        /// </summary>
        public event Action<byte[]> OnDataReceived;

        /// <summary>
        /// Disparado quando a conexão é fechada.
        /// </summary>
        public event Action<int> OnDisconnected;

        #endregion

        #region Construtor

        /// <summary>
        /// Cria uma nova conexão TCP.
        /// </summary>
        /// <param name="tcpClient">TcpClient do .NET</param>
        /// <param name="connectionId">ID único da conexão</param>
        public TcpConnection(TcpClient tcpClient, int connectionId)
        {
            _tcpClient = tcpClient ?? throw new ArgumentNullException(nameof(tcpClient));
            _connectionId = connectionId;
            _cts = new CancellationTokenSource();

            // Configura stream para leitura/escrita
            _stream = tcpClient.GetStream();
        }

        #endregion

        #region Métodos Públicos

        /// <summary>
        /// Inicia a thread de recebimento de dados.
        /// </summary>
        public void StartReceiving()
        {
            // Inicia task em background para receber dados
            _ = Task.Run(() => ReceiveLoopAsync(_cts.Token));
        }

        /// <summary>
        /// Envia dados pela conexão.
        /// </summary>
        /// <param name="data">Dados a enviar</param>
        public async Task<bool> SendAsync(byte[] data)
        {
            if (!IsConnected || _isDisposed)
                return false;

            try
            {
                // Protocolo simples: [4 bytes length][data]
                // Permite saber onde um packet termina e outro começa

                // Escreve tamanho (4 bytes = int32)
                var lengthBytes = BitConverter.GetBytes(data.Length);
                await _stream.WriteAsync(lengthBytes, 0, 4).ConfigureAwait(false);

                // Escreve dados
                await _stream.WriteAsync(data, 0, data.Length).ConfigureAwait(false);

                // Flush garante envio imediato
                await _stream.FlushAsync().ConfigureAwait(false);

                return true;
            }
            catch (Exception)
            {
                // Qualquer erro = desconecta
                Disconnect();
                return false;
            }
        }

        /// <summary>
        /// Fecha a conexão.
        /// </summary>
        public void Disconnect()
        {
            if (_isDisposed)
                return;

            try
            {
                _cts?.Cancel();
                _stream?.Close();
                _tcpClient?.Close();
            }
            catch
            {
                // Ignora erros ao fechar
            }
            finally
            {
                OnDisconnected?.Invoke(_connectionId);
            }
        }

        #endregion

        #region Métodos Privados

        /// <summary>
        /// Loop de recebimento de dados (roda em background thread).
        /// </summary>
        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            var lengthBuffer = new byte[4]; // Buffer para ler tamanho

            try
            {
                while (!ct.IsCancellationRequested && IsConnected)
                {
                    // 1. Lê tamanho do packet (4 bytes)
                    var bytesRead = await ReadExactlyAsync(_stream, lengthBuffer, 4, ct).ConfigureAwait(false);
                    if (bytesRead != 4)
                    {
                        // Conexão fechada
                        break;
                    }

                    // 2. Converte bytes para int
                    var length = BitConverter.ToInt32(lengthBuffer, 0);

                    // 3. Valida tamanho (proteção contra packets maliciosos)
                    if (length <= 0 || length > 1024 * 1024) // Max 1MB
                    {
                        // Packet inválido, desconecta
                        break;
                    }

                    // 4. Lê dados do packet
                    var dataBuffer = new byte[length];
                    bytesRead = await ReadExactlyAsync(_stream, dataBuffer, length, ct).ConfigureAwait(false);
                    if (bytesRead != length)
                    {
                        // Conexão fechada
                        break;
                    }

                    // 5. Dispara evento com dados recebidos
                    OnDataReceived?.Invoke(dataBuffer);
                }
            }
            catch (OperationCanceledException)
            {
                // Cancellation é esperado, não faz nada
            }
            catch (Exception)
            {
                // Qualquer outro erro = desconecta
            }
            finally
            {
                Disconnect();
            }
        }

        /// <summary>
        /// Lê exatamente N bytes do stream (ou falha).
        /// </summary>
        /// <remarks>
        /// NetworkStream.ReadAsync() pode retornar menos bytes que pedido.
        /// Este método garante que lê EXATAMENTE a quantidade pedida.
        /// </remarks>
        private async Task<int> ReadExactlyAsync(NetworkStream stream, byte[] buffer, int count, CancellationToken ct)
        {
            int totalRead = 0;

            while (totalRead < count)
            {
                var bytesRead = await stream.ReadAsync(buffer, totalRead, count - totalRead, ct).ConfigureAwait(false);

                if (bytesRead == 0)
                {
                    // Stream fechado
                    return totalRead;
                }

                totalRead += bytesRead;
            }

            return totalRead;
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            Disconnect();
            _cts?.Dispose();
            _stream?.Dispose();
            _tcpClient?.Dispose();
        }

        #endregion
    }
}