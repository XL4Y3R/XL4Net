using MessagePack;
using XL4Net.Shared.Protocol.Enums;

namespace XL4Net.Shared.Protocol.Messages.Game
{
    /// <summary>
    /// Mensagem de autenticação no GameServer.
    /// Cliente → GameServer: Envia token JWT obtido do AuthServer.
    /// </summary>
    /// <remarks>
    /// Fluxo:
    /// 1. Cliente faz login no AuthServer (username/password)
    /// 2. AuthServer retorna token JWT
    /// 3. Cliente conecta no GameServer
    /// 4. Cliente envia este GameAuthRequest com o token
    /// 5. GameServer valida token (localmente ou via AuthServer)
    /// 6. GameServer responde com GameAuthResponse
    /// </remarks>
    [MessagePackObject]
    public class GameAuthRequestMessage
    {
        /// <summary>
        /// Tipo da mensagem.
        /// </summary>
        [Key(0)]
        public MessageType Type { get; set; } = MessageType.TokenValidationRequest;

        /// <summary>
        /// Token JWT obtido do AuthServer.
        /// </summary>
        [Key(1)]
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// Versão do cliente (para validação de compatibilidade).
        /// </summary>
        [Key(2)]
        public string ClientVersion { get; set; } = "1.0.0";

        /// <summary>
        /// Construtor padrão (requerido pelo MessagePack).
        /// </summary>
        public GameAuthRequestMessage() { }

        /// <summary>
        /// Construtor com token.
        /// </summary>
        public GameAuthRequestMessage(string token, string clientVersion = "1.0.0")
        {
            Token = token;
            ClientVersion = clientVersion;
        }

        /// <summary>
        /// Retorna string legível para logs (token truncado por segurança).
        /// </summary>
        public override string ToString()
        {
            var truncatedToken = Token.Length > 20
                ? Token.Substring(0, 20) + "..."
                : Token;
            return $"GameAuthRequest: Token={truncatedToken}, Version={ClientVersion}";
        }
    }
}