// XL4Net.Shared/Transport/Packet.cs

using XL4Net.Shared.Pooling;
using XL4Net.Shared.Protocol.Enums;

namespace XL4Net.Shared.Transport
{
    /// <summary>
    /// Pacote de rede que encapsula dados e metadados de transmissão.
    /// É o "envelope" que carrega mensagens pela rede.
    /// 
    /// IMPORTANTE: É uma CLASS (não struct) para funcionar com ObjectPool.
    /// </summary>
    /// <remarks>
    /// Estrutura baseada no Fishnet Tugboat transport.
    /// 
    /// Sistema de ACK/NACK:
    /// - Sequence: Número sequencial deste pacote
    /// - Ack: Último pacote recebido do outro lado
    /// - AckBits: Bitfield dos últimos 32 pacotes recebidos
    /// 
    /// Exemplo de uso:
    /// <code>
    /// using (var rental = PacketPool.RentDisposable())
    /// {
    ///     var packet = rental.Value;
    ///     packet.Sequence = 123;
    ///     packet.Channel = ChannelType.Reliable;
    ///     packet.Payload = serializedData;
    ///     
    ///     socket.Send(packet);
    /// } // Auto-return ao pool
    /// </code>
    /// </remarks>
    public class Packet : IPoolable
    {
        /// <summary>
        /// Número sequencial deste pacote (0-65535, depois volta a 0).
        /// Usado para ordenação, detecção de perda e sistema de ACK.
        /// </summary>
        /// <example>
        /// Pacote #1  → Sequence = 1
        /// Pacote #2  → Sequence = 2
        /// ...
        /// Pacote #65535 → Sequence = 65535
        /// Pacote #65536 → Sequence = 0 (wrap around)
        /// </example>
        public ushort Sequence { get; set; }

        /// <summary>
        /// Número do último pacote recebido do outro lado.
        /// Usado para confirmar recebimento (acknowledgment).
        /// </summary>
        /// <example>
        /// Se o servidor recebeu pacote #5 do cliente:
        /// - Próximo pacote do servidor terá Ack = 5
        /// - Cliente sabe: "Servidor recebeu meu #5!"
        /// </example>
        public ushort Ack { get; set; }

        /// <summary>
        /// Bitfield de 32 bits indicando quais dos últimos 32 pacotes foram recebidos.
        /// Bit 0 = pacote Ack, Bit 1 = pacote Ack-1, etc.
        /// </summary>
        /// <remarks>
        /// Sistema de ACK estendido que permite detectar múltiplos pacotes perdidos.
        /// 
        /// Exemplo:
        /// Se Ack = 100 e AckBits = 0b11111111111111111111111111101111:
        ///                                                   ↑
        ///                                            Bit 4 = 0 (perdido)
        /// 
        /// Significa:
        /// - Recebeu pacotes: #100, #99, #98, #97, #95, #94, ... #69
        /// - NÃO recebeu: #96 (bit 4 = 0)
        /// 
        /// O sender pode detectar que #96 foi perdido e reenviar.
        /// </remarks>
        public uint AckBits { get; set; }

        /// <summary>
        /// Canal de transmissão deste pacote.
        /// Define comportamento de confiabilidade e ordenação.
        /// </summary>
        /// <seealso cref="ChannelType"/>
        public ChannelType Channel { get; set; }

        /// <summary>
        /// Dados serializados da mensagem (pode ser null para pacotes vazios como ACK-only).
        /// Tamanho típico: 0-1400 bytes (MTU).
        /// </summary>
        /// <remarks>
        /// IMPORTANTE: Este array será POOLADO separadamente pelo BufferPool.
        /// Não aloque manualmente com 'new byte[]', use BufferPool.Rent().
        /// </remarks>
        public byte[]? Payload { get; set; }

        /// <summary>
        /// Tamanho real dos dados úteis no Payload.
        /// Payload pode ser maior que PayloadSize (buffer reaproveitado).
        /// </summary>
        /// <example>
        /// Payload = byte[1024] (buffer do pool)
        /// PayloadSize = 256    (só os primeiros 256 bytes são dados válidos)
        /// 
        /// Ao enviar, use: payload.AsSpan(0, PayloadSize)
        /// </example>
        public int PayloadSize { get; set; }

        /// <summary>
        /// Tipo do packet (Handshake, Ping, Data, etc).
        /// Usado para identificar o propósito do packet sem precisar desserializar o Payload.
        /// </summary>
        /// <seealso cref="PacketType"/>
        public byte Type { get; set; }

        /// <summary>
        /// Reseta o pacote para estado padrão.
        /// Chamado automaticamente quando o pacote é retornado ao pool.
        /// </summary>
        /// <remarks>
        /// IMPORTANTE: Não reseta o Payload para null (evita perder buffer poolado).
        /// Só limpa PayloadSize. O BufferPool gerencia o ciclo de vida dos buffers.
        /// </remarks>
        public void Reset()
        {
            Sequence = 0;
            Ack = 0;
            AckBits = 0;
            Channel = ChannelType.Unreliable;
            Type = 0;
            // NÃO reseta Payload (buffer é gerenciado pelo BufferPool)
            PayloadSize = 0;
        }

        /// <summary>
        /// Retorna representação em string para debug.
        /// </summary>
        public override string ToString()
        {
            return $"Packet[Seq={Sequence}, Ack={Ack}, Channel={Channel}, Size={PayloadSize}]";
        }

        // ====================================================================
        // MÉTODOS AUXILIARES PARA SISTEMA DE ACK
        // ====================================================================

        /// <summary>
        /// Verifica se um pacote específico foi confirmado (ACK).
        /// </summary>
        /// <param name="sequence">Número do pacote a verificar</param>
        /// <returns>True se foi confirmado, False caso contrário</returns>
        /// <example>
        /// packet.Ack = 100
        /// packet.AckBits = 0b...1110... (bit 4 = 0)
        /// 
        /// packet.IsAcked(100) → true  (é o Ack atual)
        /// packet.IsAcked(99)  → true  (bit 0 = 1)
        /// packet.IsAcked(98)  → true  (bit 1 = 1)
        /// packet.IsAcked(97)  → true  (bit 2 = 1)
        /// packet.IsAcked(96)  → false (bit 4 = 0)
        /// packet.IsAcked(68)  → false (fora da janela de 32 bits)
        /// </example>
        public bool IsAcked(ushort sequence)
        {
            // Se é o Ack atual
            if (sequence == Ack)
                return true;

            // Calcula diferença (com wrap around)
            int diff = Ack - sequence;

            // Se está fora da janela de 32 bits
            if (diff <= 0 || diff > 32)
                return false;

            // Verifica o bit correspondente
            // diff=1 → bit 0, diff=2 → bit 1, etc
            uint bitIndex = (uint)(diff - 1);
            uint mask = 1u << (int)bitIndex;

            return (AckBits & mask) != 0;
        }

        /// <summary>
        /// Define que um pacote foi confirmado (marca o bit correspondente).
        /// Usado ao processar recebimento de pacotes.
        /// </summary>
        /// <param name="sequence">Número do pacote recebido</param>
        /// <example>
        /// // Recebeu pacote #105
        /// packet.Ack = 100
        /// packet.MarkAsAcked(105)
        /// 
        /// // Resultado:
        /// packet.Ack = 105 (atualizado)
        /// packet.AckBits = ... (shiftado e atualizado)
        /// </example>
        public void MarkAsAcked(ushort sequence)
        {
            // Se é um pacote mais novo que o Ack atual
            if (IsSequenceNewer(sequence, Ack))
            {
                // Calcula quantos pacotes avançamos
                int diff = sequence - Ack;

                // Shifta AckBits (move bits pra direita)
                if (diff < 32)
                {
                    AckBits <<= diff;
                    // Marca o Ack anterior como recebido (bit 0)
                    AckBits |= 1u;
                }
                else
                {
                    // Salto muito grande, limpa tudo
                    AckBits = 0;
                }

                // Atualiza Ack
                Ack = sequence;
            }
            else
            {
                // Pacote antigo, marca apenas o bit
                int diff = Ack - sequence;

                if (diff > 0 && diff <= 32)
                {
                    uint bitIndex = (uint)(diff - 1);
                    uint mask = 1u << (int)bitIndex;
                    AckBits |= mask;
                }
            }
        }

        /// <summary>
        /// Verifica se um sequence number é mais novo que outro.
        /// Lida corretamente com wrap around (65535 → 0).
        /// </summary>
        /// <param name="s1">Primeiro sequence</param>
        /// <param name="s2">Segundo sequence</param>
        /// <returns>True se s1 é mais novo que s2</returns>
        /// <example>
        /// IsSequenceNewer(5, 3) → true  (5 > 3)
        /// IsSequenceNewer(3, 5) → false (3 < 5)
        /// IsSequenceNewer(1, 65535) → true  (wrap around: 65535 → 0 → 1)
        /// IsSequenceNewer(65535, 1) → false (1 é mais novo)
        /// </example>
        private static bool IsSequenceNewer(ushort s1, ushort s2)
        {
            // Diferença com wrap around
            // Se diferença > 32768, houve wrap around
            return ((s1 > s2) && (s1 - s2 <= 32768)) ||
                   ((s1 < s2) && (s2 - s1 > 32768));
        }

        #region Serialização

        /// <summary>
        /// Serializa o packet para bytes (formato wire protocol).
        /// Formato: [Type:1][Sequence:2][Ack:2][AckBits:4][Channel:1][PayloadSize:4][Payload:N]
        /// Total header: 14 bytes + payload
        /// </summary>
        /// <returns>Array de bytes representando o packet</returns>
        /// <remarks>
        /// Este formato é usado para transmissão pela rede (TCP/UDP).
        /// É diferente de MessagePack (usado para serializar mensagens dentro do Payload).
        /// 
        /// Estrutura do packet serializado:
        /// - Type (1 byte): Tipo do packet (0-255)
        /// - Sequence (2 bytes): Número sequencial
        /// - Ack (2 bytes): Último packet recebido
        /// - AckBits (4 bytes): Bitfield de confirmações
        /// - Channel (1 byte): Canal de transmissão
        /// - PayloadSize (4 bytes): Tamanho do payload
        /// - Payload (N bytes): Dados (pode ser vazio)
        /// </remarks>
        public byte[] Serialize()
        {
            // Calcula tamanho total
            int headerSize = 14; // Type(1) + Seq(2) + Ack(2) + AckBits(4) + Channel(1) + Size(4)
            int totalSize = headerSize + PayloadSize;

            var buffer = new byte[totalSize];
            int offset = 0;

            // 1. Type (1 byte)
            buffer[offset++] = Type;

            // 2. Sequence (2 bytes)
            System.Buffer.BlockCopy(System.BitConverter.GetBytes(Sequence), 0, buffer, offset, 2);
            offset += 2;

            // 3. Ack (2 bytes)
            System.Buffer.BlockCopy(System.BitConverter.GetBytes(Ack), 0, buffer, offset, 2);
            offset += 2;

            // 4. AckBits (4 bytes)
            System.Buffer.BlockCopy(System.BitConverter.GetBytes(AckBits), 0, buffer, offset, 4);
            offset += 4;

            // 5. Channel (1 byte)
            buffer[offset++] = (byte)Channel;

            // 6. PayloadSize (4 bytes)
            System.Buffer.BlockCopy(System.BitConverter.GetBytes(PayloadSize), 0, buffer, offset, 4);
            offset += 4;

            // 7. Payload (N bytes)
            if (PayloadSize > 0 && Payload != null)
            {
                System.Buffer.BlockCopy(Payload, 0, buffer, offset, PayloadSize);
            }

            return buffer;
        }

        /// <summary>
        /// Deserializa bytes para este packet.
        /// Reconstrói o packet a partir do formato wire protocol.
        /// </summary>
        /// <param name="buffer">Bytes recebidos da rede</param>
        /// <exception cref="System.ArgumentException">Se buffer for inválido</exception>
        /// <remarks>
        /// IMPORTANTE: Este método NÃO aloca novo array para Payload se o Payload atual
        /// já for grande o suficiente (reutiliza buffer existente do pool).
        /// </remarks>
        public void Deserialize(byte[] buffer)
        {
            if (buffer == null || buffer.Length < 14)
                throw new System.ArgumentException("Buffer too small for packet header");

            int offset = 0;

            // 1. Type (1 byte)
            Type = buffer[offset++];

            // 2. Sequence (2 bytes)
            Sequence = System.BitConverter.ToUInt16(buffer, offset);
            offset += 2;

            // 3. Ack (2 bytes)
            Ack = System.BitConverter.ToUInt16(buffer, offset);
            offset += 2;

            // 4. AckBits (4 bytes)
            AckBits = System.BitConverter.ToUInt32(buffer, offset);
            offset += 4;

            // 5. Channel (1 byte)
            Channel = (ChannelType)buffer[offset++];

            // 6. PayloadSize (4 bytes)
            PayloadSize = System.BitConverter.ToInt32(buffer, offset);
            offset += 4;

            // Valida tamanho
            if (PayloadSize < 0 || offset + PayloadSize > buffer.Length)
                throw new System.ArgumentException("Invalid payload size in packet");

            // 7. Payload (N bytes)
            if (PayloadSize > 0)
            {
                // Reutiliza buffer existente se possível (evita alocação)
                if (Payload == null || Payload.Length < PayloadSize)
                {
                    Payload = new byte[PayloadSize];
                }

                System.Buffer.BlockCopy(buffer, offset, Payload, 0, PayloadSize);
            }
            else
            {
                // Payload vazio, mas mantém buffer alocado (pode ser reutilizado depois)
                PayloadSize = 0;
            }
        }

        #endregion
    }
}