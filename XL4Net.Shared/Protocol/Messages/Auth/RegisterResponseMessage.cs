// XL4Net.Shared/Protocol/Messages/Auth/RegisterResponseMessage.cs

using System;
using MessagePack;
using XL4Net.Shared.Protocol.Enums;

namespace XL4Net.Shared.Protocol.Messages.Auth
{
    /// <summary>
    /// Mensagem de resposta de registro de conta.
    /// AuthServer → Cliente
    /// </summary>
    [MessagePackObject]
    public class RegisterResponseMessage
    {
        /// <summary>
        /// Tipo da mensagem (sempre RegisterResponse).
        /// </summary>
        [Key(0)]
        public MessageType Type { get; set; } = MessageType.RegisterResponse;

        /// <summary>
        /// TRUE = conta criada com sucesso
        /// FALSE = falha (veja ErrorMessage)
        /// </summary>
        [Key(1)]
        public bool Success { get; set; }

        /// <summary>
        /// ID da conta criada (se sucesso).
        /// </summary>
        [Key(2)]
        public string? AccountId { get; set; }

        /// <summary>
        /// Username da conta criada (se sucesso).
        /// </summary>
        [Key(3)]
        public string? Username { get; set; }

        /// <summary>
        /// Mensagem de erro (se falha).
        /// Exemplos: "Username already taken", "Invalid email format"
        /// </summary>
        [Key(4)]
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Construtor padrão (requerido pelo MessagePack).
        /// </summary>
        public RegisterResponseMessage() { }

        /// <summary>
        /// Cria resposta de sucesso.
        /// </summary>
        public static RegisterResponseMessage CreateSuccess(Guid accountId, string username)
        {
            return new RegisterResponseMessage
            {
                Success = true,
                AccountId = accountId.ToString(),
                Username = username,
                ErrorMessage = null
            };
        }

        /// <summary>
        /// Cria resposta de falha.
        /// </summary>
        public static RegisterResponseMessage CreateFailure(string errorMessage)
        {
            return new RegisterResponseMessage
            {
                Success = false,
                AccountId = null,
                Username = null,
                ErrorMessage = errorMessage
            };
        }

        /// <summary>
        /// Retorna string legível para logs.
        /// </summary>
        public override string ToString()
        {
            if (Success)
                return $"RegisterResponse[SUCCESS]: {Username} ({AccountId})";
            else
                return $"RegisterResponse[FAILURE]: {ErrorMessage}";
        }
    }
}