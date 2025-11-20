// XL4Net.Shared/Protocol/Messages/Auth/LoginRequestMessage.cs

using MessagePack;
using XL4Net.Shared.Protocol.Enums;

namespace XL4Net.Shared.Protocol.Messages.Auth
{
    /// <summary>
    /// Mensagem de requisição de login.
    /// Cliente → AuthServer
    /// </summary>
    [MessagePackObject]
    public class LoginRequestMessage
    {
        /// <summary>
        /// Tipo da mensagem (sempre LoginRequest).
        /// </summary>
        [Key(0)]
        public MessageType Type { get; set; } = MessageType.LoginRequest;

        /// <summary>
        /// Nome de usuário ou email.
        /// Suporta login por username OU email.
        /// </summary>
        [Key(1)]
        public string UsernameOrEmail { get; set; } = string.Empty;

        /// <summary>
        /// Senha em plain text.
        /// IMPORTANTE: Usar TLS/HTTPS em produção!
        /// </summary>
        [Key(2)]
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Construtor padrão (requerido pelo MessagePack).
        /// </summary>
        public LoginRequestMessage() { }

        /// <summary>
        /// Construtor com parâmetros.
        /// </summary>
        public LoginRequestMessage(string usernameOrEmail, string password)
        {
            UsernameOrEmail = usernameOrEmail;
            Password = password;
        }

        /// <summary>
        /// Verifica se está tentando login por email (contém @).
        /// </summary>
        [IgnoreMember]
        public bool IsEmailLogin => UsernameOrEmail.Contains("@");

        /// <summary>
        /// Retorna string legível para logs (SEM SENHA!).
        /// </summary>
        public override string ToString()
        {
            var loginType = IsEmailLogin ? "email" : "username";
            return $"LoginRequest: {UsernameOrEmail} ({loginType})";
        }
    }
}