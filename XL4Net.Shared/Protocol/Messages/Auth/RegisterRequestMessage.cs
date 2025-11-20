// XL4Net.Shared/Protocol/Messages/Auth/RegisterRequestMessage.cs

using MessagePack;
using XL4Net.Shared.Protocol.Enums;

namespace XL4Net.Shared.Protocol.Messages.Auth
{
    /// <summary>
    /// Mensagem de requisição de registro de nova conta.
    /// Cliente → AuthServer
    /// </summary>
    [MessagePackObject]
    public class RegisterRequestMessage
    {
        /// <summary>
        /// Tipo da mensagem (sempre RegisterRequest).
        /// </summary>
        [Key(0)]
        public MessageType Type { get; set; } = MessageType.RegisterRequest;

        /// <summary>
        /// Nome de usuário desejado (3-50 caracteres).
        /// </summary>
        [Key(1)]
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Email do usuário.
        /// </summary>
        [Key(2)]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Senha em plain text.
        /// IMPORTANTE: Usar TLS/HTTPS em produção!
        /// </summary>
        [Key(3)]
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Confirmação da senha (deve ser igual a Password).
        /// </summary>
        [Key(4)]
        public string ConfirmPassword { get; set; } = string.Empty;

        /// <summary>
        /// Construtor padrão (requerido pelo MessagePack).
        /// </summary>
        public RegisterRequestMessage() { }

        /// <summary>
        /// Construtor com parâmetros.
        /// </summary>
        public RegisterRequestMessage(string username, string email, string password, string confirmPassword)
        {
            Username = username;
            Email = email;
            Password = password;
            ConfirmPassword = confirmPassword;
        }

        /// <summary>
        /// Retorna string legível para logs (SEM SENHA!).
        /// </summary>
        public override string ToString()
        {
            return $"RegisterRequest: {Username} ({Email})";
        }
    }
}