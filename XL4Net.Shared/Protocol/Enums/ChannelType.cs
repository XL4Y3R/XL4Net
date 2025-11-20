namespace XL4Net.Shared.Protocol.Enums
{
    /// <summary>
    /// Tipo de canal de comunicação.
    /// </summary>
    public enum ChannelType : byte
    {
        /// <summary>
        /// Canal confiável (TCP-like) - garante entrega ordenada.
        /// Usado para: chat, spawn/despawn, inventário.
        /// </summary>
        Reliable = 0,

        /// <summary>
        /// Canal não confiável (UDP puro) - fire and forget.
        /// Usado para: movimento (30Hz), animações.
        /// </summary>
        Unreliable = 1,

        /// <summary>
        /// Canal sequenciado - descarta pacotes velhos.
        /// Usado para: snapshots de estado.
        /// </summary>
        Sequenced = 2
    }
}