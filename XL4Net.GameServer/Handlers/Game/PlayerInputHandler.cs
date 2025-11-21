// XL4Net.GameServer/Handlers/Game/PlayerInputHandler.cs

using LiteNetLib;
using MessagePack;
using Serilog;
using XL4Net.GameServer.Players;
using XL4Net.Shared.Pooling;
using XL4Net.Shared.Prediction;
using XL4Net.Shared.Protocol.Enums;
using XL4Net.Shared.Protocol.Messages.Game;
using XL4Net.Shared.Transport;

namespace XL4Net.GameServer.Handlers.Game
{
    /// <summary>
    /// Handler para processar inputs de movimento do jogador.
    /// Executa server-side da prediction/reconciliation.
    /// </summary>
    /// <remarks>
    /// Fluxo:
    /// 1. Recebe PlayerInputMessage do cliente
    /// 2. Valida input (anti-cheat básico)
    /// 3. Executa MovementPhysics.Execute() (lógica IDÊNTICA ao cliente)
    /// 4. Atualiza PlayerSession com novo estado
    /// 5. Envia PlayerStateMessage de volta (estado autoritativo)
    /// 
    /// IMPORTANTE: A lógica de movimento DEVE ser idêntica ao cliente!
    /// Usamos MovementPhysics.Execute() que é compartilhado.
    /// </remarks>
    public class PlayerInputHandler
    {
        // ============================================
        // CAMPOS PRIVADOS
        // ============================================

        // Configurações de movimento (compartilhadas com cliente)
        private readonly MovementSettings _movementSettings;

        // Delta time fixo (1/TickRate)
        private readonly float _tickDeltaTime;

        // Métricas
        private long _inputsProcessed;
        private long _inputsRejected;

        // ============================================
        // PROPRIEDADES
        // ============================================

        /// <summary>
        /// Total de inputs processados.
        /// </summary>
        public long InputsProcessed => _inputsProcessed;

        /// <summary>
        /// Total de inputs rejeitados (anti-cheat).
        /// </summary>
        public long InputsRejected => _inputsRejected;

        // ============================================
        // CONSTRUTOR
        // ============================================

        /// <summary>
        /// Cria handler de input.
        /// </summary>
        /// <param name="tickRate">Taxa de ticks do servidor (Hz)</param>
        /// <param name="movementSettings">Configurações de movimento (null = Default)</param>
        public PlayerInputHandler(int tickRate = 30, MovementSettings movementSettings = null)
        {
            _movementSettings = movementSettings ?? MovementSettings.Default;
            _tickDeltaTime = 1f / tickRate;

            Log.Information("PlayerInputHandler created: TickRate={TickRate}Hz, DeltaTime={DeltaTime:F4}s",
                tickRate, _tickDeltaTime);
        }

        // ============================================
        // PROCESSAMENTO DE INPUT
        // ============================================

        /// <summary>
        /// Processa um input de jogador.
        /// </summary>
        /// <param name="session">Sessão do jogador</param>
        /// <param name="input">Dados do input</param>
        /// <param name="server">Referência ao servidor</param>
        /// <returns>Novo estado do jogador</returns>
        public StateSnapshot ProcessInput(PlayerSession session, InputData input, Core.GameServer server)
        {
            // 1. Valida input
            if (!ValidateInput(session, input))
            {
                _inputsRejected++;

                // Retorna estado atual sem alteração
                return GetCurrentState(session, (uint)server.CurrentTick);
            }

            // 2. Obtém estado atual do jogador
            var currentState = GetCurrentState(session, input.Tick);

            // 3. Executa física de movimento (CÓDIGO COMPARTILHADO!)
            var newState = MovementPhysics.Execute(
                currentState,
                input,
                _movementSettings,
                _tickDeltaTime
            );

            // 4. Valida resultado (anti-cheat pós-movimento)
            if (!ValidateMovementResult(session, currentState, newState, input))
            {
                _inputsRejected++;

                Log.Warning("Movement validation failed for {Player}: " +
                    "Pos={OldPos} -> {NewPos}, Input={Input}",
                    session, currentState.Position, newState.Position, input);

                // Retorna estado atual sem alteração
                return currentState;
            }

            // 5. Atualiza sessão com novo estado
            ApplyStateToSession(session, newState);

            // 6. Atualiza métricas
            _inputsProcessed++;
            session.LastInputTick = (int)input.Tick;

            Log.Debug("Input processed for {Player}: Seq={Seq}, Pos={Pos}",
                session, input.SequenceNumber, newState.Position);

            return newState;
        }

        /// <summary>
        /// Processa PlayerInputMessage e envia resposta.
        /// Método conveniente para uso direto no dispatch.
        /// </summary>
        public void HandleMessage(
            PlayerSession session,
            PlayerInputMessage message,
            Core.GameServer server)
        {
            // Processa input
            var newState = ProcessInput(session, message.Input, server);

            // Envia estado autoritativo de volta
            SendStateToClient(session.PeerId, newState, server);
        }

        /// <summary>
        /// Processa batch de inputs.
        /// Útil para redundância e recuperação de pacotes perdidos.
        /// </summary>
        public void HandleBatchMessage(
            PlayerSession session,
            PlayerInputBatchMessage batch,
            Core.GameServer server)
        {
            if (batch.Inputs == null || batch.Inputs.Length == 0)
            {
                Log.Warning("Empty input batch from {Player}", session);
                return;
            }

            StateSnapshot lastState = default;
            var lastProcessedSeq = session.LastProcessedInputSequence;

            // Processa cada input em ordem
            foreach (var input in batch.Inputs)
            {
                // Pula inputs já processados
                if (input.SequenceNumber <= lastProcessedSeq)
                {
                    continue;
                }

                lastState = ProcessInput(session, input, server);
                lastProcessedSeq = input.SequenceNumber;
            }

            // Atualiza último sequence processado
            session.LastProcessedInputSequence = lastProcessedSeq;

            // Envia estado final
            if (lastState.Tick > 0)
            {
                SendStateToClient(session.PeerId, lastState, server);
            }
        }

        // ============================================
        // VALIDAÇÃO (ANTI-CHEAT BÁSICO)
        // ============================================

        /// <summary>
        /// Valida input antes de processar.
        /// </summary>
        private bool ValidateInput(PlayerSession session, InputData input)
        {
            // 1. Verifica se jogador está no jogo
            if (!session.IsInGame)
            {
                Log.Warning("Input from non-InGame player {Player}", session);
                return false;
            }

            // 2. Verifica se sequence é válido (não pode voltar no tempo)
            if (input.SequenceNumber <= session.LastProcessedInputSequence)
            {
                // Input antigo, ignora (pode acontecer por reordenação de pacotes)
                Log.Debug("Old input from {Player}: Seq={Seq}, LastProcessed={Last}",
                    session, input.SequenceNumber, session.LastProcessedInputSequence);
                return false;
            }

            // 3. Verifica direção de movimento normalizada
            if (input.MoveDirection.SqrMagnitude > 1.1f) // Permite pequena margem
            {
                Log.Warning("Invalid move direction from {Player}: {Dir}",
                    session, input.MoveDirection);
                return false;
            }

            // 4. Verifica tick (não pode estar muito no futuro ou passado)
            // TODO: Implementar validação de tick baseada em RTT

            return true;
        }

        /// <summary>
        /// Valida resultado do movimento (anti-speedhack básico).
        /// </summary>
        private bool ValidateMovementResult(
            PlayerSession session,
            StateSnapshot oldState,
            StateSnapshot newState,
            InputData input)
        {
            // Calcula distância percorrida
            var delta = newState.Position - oldState.Position;
            var distance = delta.Magnitude;

            // Velocidade máxima possível (sprint + margem de 20%)
            var maxSpeed = _movementSettings.SprintSpeed * 1.2f;
            var maxDistance = maxSpeed * _tickDeltaTime;

            // Verifica se moveu mais do que deveria (horizontal)
            var horizontalDelta = new Shared.Math.Vec3(delta.X, 0, delta.Z);
            var horizontalDistance = horizontalDelta.Magnitude;

            if (horizontalDistance > maxDistance)
            {
                Log.Warning("Speedhack detected? {Player}: Distance={Dist:F3}, Max={Max:F3}",
                    session, horizontalDistance, maxDistance);

                // Por enquanto, só loga. Em produção, poderia:
                // - Desconectar jogador
                // - Aplicar penalidade
                // - Registrar para análise posterior
                // return false;
            }

            return true;
        }

        // ============================================
        // HELPERS
        // ============================================

        /// <summary>
        /// Obtém estado atual do jogador como StateSnapshot.
        /// </summary>
        private StateSnapshot GetCurrentState(PlayerSession session, uint tick)
        {
            return new StateSnapshot
            {
                Tick = tick,
                LastProcessedInput = session.LastProcessedInputSequence,
                Position = session.Position,
                Velocity = session.Velocity,
                Rotation = session.Rotation,
                IsGrounded = session.IsGrounded,
                IsSprinting = session.IsSprinting,
                IsCrouching = session.IsCrouching,
                IsJumping = session.IsJumping,
                IsFalling = session.IsFalling
            };
        }

        /// <summary>
        /// Aplica StateSnapshot na sessão do jogador.
        /// </summary>
        private void ApplyStateToSession(PlayerSession session, StateSnapshot state)
        {
            session.Position = state.Position;
            session.Velocity = state.Velocity;
            session.Rotation = state.Rotation;
            session.IsGrounded = state.IsGrounded;
            session.IsSprinting = state.IsSprinting;
            session.IsCrouching = state.IsCrouching;
            session.IsJumping = state.IsJumping;
            session.IsFalling = state.IsFalling;
            session.LastProcessedInputSequence = state.LastProcessedInput;
        }

        /// <summary>
        /// Envia estado autoritativo para o cliente.
        /// </summary>
        private void SendStateToClient(int peerId, StateSnapshot state, Core.GameServer server)
        {
            // Cria mensagem
            var response = new PlayerStateMessage(state);

            // Serializa
            var payload = MessagePackSerializer.Serialize(response);

            // Empacota e envia
            var packet = PacketPool.Rent();
            packet.Type = (byte)PacketType.Data;
            packet.Channel = ChannelType.Reliable;
            packet.Payload = payload;
            packet.PayloadSize = payload.Length;

            server.SendTo(peerId, packet, DeliveryMethod.ReliableOrdered);

            // Nota: SendTo já retorna o packet ao pool
        }

        // ============================================
        // MÉTRICAS
        // ============================================

        /// <summary>
        /// Reseta contadores de métricas.
        /// </summary>
        public void ResetMetrics()
        {
            _inputsProcessed = 0;
            _inputsRejected = 0;
        }
    }
}