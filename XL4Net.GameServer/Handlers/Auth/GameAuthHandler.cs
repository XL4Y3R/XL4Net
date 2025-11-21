// XL4Net.GameServer/Handlers/Auth/GameAuthHandler.cs

using LiteNetLib;
using MessagePack;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
using XL4Net.Shared.Pooling;
using XL4Net.Shared.Protocol.Enums;
using XL4Net.Shared.Protocol.Messages.Game;
using XL4Net.Shared.Transport;

namespace XL4Net.GameServer.Handlers.Auth
{
    /// <summary>
    /// Handler para autenticação no GameServer.
    /// Valida token JWT e autentica o jogador.
    /// </summary>
    public class GameAuthHandler : IMessageHandler
    {
        private readonly string _jwtSecret;
        private readonly string _jwtIssuer;
        private readonly Version _minClientVersion;

        public PacketType PacketType => PacketType.Data;

        public GameAuthHandler(
            string jwtSecret,
            string jwtIssuer = "XL4Net.AuthServer",
            string minClientVersion = "1.0.0")
        {
            if (string.IsNullOrWhiteSpace(jwtSecret))
                throw new ArgumentException("JWT secret cannot be empty", nameof(jwtSecret));

            _jwtSecret = jwtSecret;
            _jwtIssuer = jwtIssuer;
            _minClientVersion = Version.Parse(minClientVersion);
        }

        public void Handle(MessageContext context, Packet packet)
        {
            // Deserializa
            GameAuthRequestMessage request;
            try
            {
                request = MessagePackSerializer.Deserialize<GameAuthRequestMessage>(packet.Payload);
            }
            catch (Exception ex)
            {
                Log.Warning("Failed to deserialize GameAuthRequest from PeerId={PeerId}: {Error}",
                    context.PeerId, ex.Message);
                SendResponse(context, GameAuthResponseMessage.CreateFailure(
                    GameAuthResult.InvalidToken, "Invalid message format"));
                PacketPool.Return(packet);
                return;
            }

            PacketPool.Return(packet);

            Log.Debug("Auth request from PeerId={PeerId}: {Request}", context.PeerId, request);

            // Validações
            if (context.Session == null)
            {
                Log.Warning("Auth request from unknown PeerId={PeerId}", context.PeerId);
                SendResponse(context, GameAuthResponseMessage.CreateFailure(
                    GameAuthResult.InternalError, "Session not found"));
                return;
            }

            if (context.IsAuthenticated)
            {
                Log.Warning("PeerId={PeerId} already authenticated", context.PeerId);
                SendResponse(context, GameAuthResponseMessage.CreateFailure(
                    GameAuthResult.AlreadyConnected, "Already authenticated"));
                return;
            }

            // Valida versão
            if (!ValidateClientVersion(request.ClientVersion, out var versionError))
            {
                Log.Warning("Version mismatch from PeerId={PeerId}: {Version}", context.PeerId, request.ClientVersion);
                SendResponse(context, GameAuthResponseMessage.CreateFailure(
                    GameAuthResult.VersionMismatch, versionError));
                DisconnectPlayer(context, "Version mismatch");
                return;
            }

            // Valida token
            if (!ValidateToken(request.Token, out var userId, out var username, out var tokenError))
            {
                Log.Warning("Invalid token from PeerId={PeerId}: {Error}", context.PeerId, tokenError);
                SendResponse(context, GameAuthResponseMessage.CreateFailure(
                    GameAuthResult.InvalidToken, tokenError));
                DisconnectPlayer(context, tokenError);
                return;
            }

            // Verifica login duplo
            if (context.Server.Players.IsUserConnected(userId))
            {
                Log.Warning("Duplicate login: UserId={UserId} from PeerId={PeerId}", userId, context.PeerId);
                SendResponse(context, GameAuthResponseMessage.CreateFailure(
                    GameAuthResult.AlreadyConnected, "Account already logged in"));
                DisconnectPlayer(context, "Duplicate login");
                return;
            }

            // ============================================
            // SUCESSO! Autentica e entra no jogo
            // ============================================
            context.Session.BeginAuthentication(request.Token);
            context.Session.CompleteAuthentication(userId, username);

            // NOVO: Entra automaticamente no jogo após autenticação
            // Em produção, isso poderia ser feito após seleção de personagem
            context.Session.EnterGame();

            Log.Information("Player authenticated and entered game: PeerId={PeerId}, UserId={UserId}, Username={Username}",
                context.PeerId, userId, username);

            SendResponse(context, GameAuthResponseMessage.CreateSuccess(userId, username, context.CurrentTick));
        }

        private bool ValidateToken(string token, out Guid userId, out string username, out string error)
        {
            userId = Guid.Empty;
            username = string.Empty;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(token))
            {
                error = "Token is empty";
                return false;
            }

            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();

                // IMPORTANTE: Desabilita mapeamento automático de claims
                tokenHandler.InboundClaimTypeMap.Clear();

                var key = Encoding.UTF8.GetBytes(_jwtSecret);

                var validationParams = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _jwtIssuer,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1)
                };

                var principal = tokenHandler.ValidateToken(token, validationParams, out _);

                // Agora "sub" vai aparecer como "sub" mesmo
                var userIdClaim = principal.Claims.FirstOrDefault(c => c.Type == "sub");

                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out userId))
                {
                    // Debug: mostra claims disponíveis
                    var availableClaims = string.Join(", ", principal.Claims.Select(c => $"{c.Type}={c.Value}"));
                    Log.Debug("Available claims: {Claims}", availableClaims);

                    error = "Token missing user ID";
                    return false;
                }

                var usernameClaim = principal.Claims.FirstOrDefault(c => c.Type == "name");
                username = usernameClaim?.Value ?? "Unknown";

                return true;
            }
            catch (SecurityTokenExpiredException)
            {
                error = "Token expired";
                return false;
            }
            catch (SecurityTokenInvalidSignatureException)
            {
                error = "Invalid token signature";
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Token validation error");
                error = "Token validation error";
                return false;
            }
        }

        private bool ValidateClientVersion(string versionStr, out string error)
        {
            error = string.Empty;

            if (!Version.TryParse(versionStr, out var version))
            {
                error = $"Invalid version format: {versionStr}";
                return false;
            }

            if (version < _minClientVersion)
            {
                error = $"Client version {version} too old. Minimum: {_minClientVersion}";
                return false;
            }

            return true;
        }

        private void SendResponse(MessageContext context, GameAuthResponseMessage response)
        {
            var packet = PacketPool.Rent();
            packet.Type = (byte)PacketType.Data;
            packet.Channel = ChannelType.Reliable;
            packet.Payload = MessagePackSerializer.Serialize(response);
            packet.PayloadSize = packet.Payload.Length;

            context.Server.SendTo(context.PeerId, packet, DeliveryMethod.ReliableOrdered);

            Log.Debug("Sent auth response to PeerId={PeerId}: {Response}", context.PeerId, response);
        }

        private void DisconnectPlayer(MessageContext context, string reason)
        {
            context.Session?.FailAuthentication();
            context.Server.DisconnectPlayer(context.PeerId, reason);
        }
    }
}