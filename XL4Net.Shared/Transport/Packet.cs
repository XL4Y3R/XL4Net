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
        public byte[] Payload { get; set; }

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
    }
}