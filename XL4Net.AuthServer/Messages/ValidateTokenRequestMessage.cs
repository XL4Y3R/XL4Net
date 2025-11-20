// XL4Net.AuthServer/Messages/ValidateTokenRequestMessage.cs

using MessagePack;
using XL4Net.Shared.Protocol.Enums;

namespace XL4Net.AuthServer.Messages
{
    /// <summary>
    /// Mensagem de requisição de validação de token JWT.
    /// GameServer → AuthServer
    /// 
    /// NOTA: Esta mensagem NÃO está em Shared porque o cliente Unity não precisa dela.
    /// </summary>
    [MessagePackObject]
    public class ValidateTokenRequestMessage
    {
        /// <summary>
        /// Tipo da mensagem (sempre TokenValidationRequest).
        /// </summary>
        [Key(0)]
        public MessageType Type { get; set; } = MessageType.TokenValidationRequest;

        /// <summary>
        /// Token JWT a ser validado.
        /// </summary>
        [Key(1)]
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// Construtor padrão (requerido pelo MessagePack).
        /// </summary>
        public ValidateTokenRequestMessage() { }

        /// <summary>
        /// Construtor com parâmetros.
        /// </summary>
        public ValidateTokenRequestMessage(string token)
        {
            Token = token;
        }

        /// <summary>
        /// Retorna string legível para logs (sem expor token completo!).
        /// </summary>
        public override string ToString()
        {
            var tokenPreview = Token.Length > 20 ? Token.Substring(0, 20) + "..." : Token;
            return $"ValidateTokenRequest: Token={tokenPreview}";
        }
    }
}