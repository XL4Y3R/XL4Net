// XL4Net.Shared/Prediction/MovementPhysics.cs

using XL4Net.Shared.Math;

namespace XL4Net.Shared.Prediction
{
    /// <summary>
    /// Lógica de física de movimento.
    /// COMPARTILHADO entre cliente e servidor.
    /// </summary>
    /// <remarks>
    /// CRÍTICO: Este código é usado TANTO pelo cliente (MoveCommand.Execute)
    /// quanto pelo servidor (PlayerInputHandler).
    /// 
    /// Qualquer alteração aqui DEVE ser feita com cuidado extremo!
    /// Diferenças causam misprediction constante.
    /// 
    /// O código é determinístico:
    /// - Mesmos inputs = Mesmos outputs
    /// - Não depende de estado externo
    /// - Não usa Random, DateTime, etc
    /// 
    /// Fluxo:
    /// 1. Aplica gravidade (se não grounded)
    /// 2. Processa pulo (se grounded + jump input)
    /// 3. Aplica movimento horizontal
    /// 4. Detecta "chão" (simplificado)
    /// 5. Atualiza flags de estado
    /// </remarks>
    public static class MovementPhysics
    {
        /// <summary>
        /// Executa física de movimento e retorna novo estado.
        /// </summary>
        /// <param name="currentState">Estado atual do jogador</param>
        /// <param name="input">Input do jogador</param>
        /// <param name="settings">Configurações de movimento</param>
        /// <param name="deltaTime">Tempo desde último tick (em segundos)</param>
        /// <returns>Novo estado após aplicar física</returns>
        /// <remarks>
        /// IMPORTANTE: Este método é PURO (sem side effects).
        /// Não modifica currentState, retorna novo estado.
        /// </remarks>
        public static StateSnapshot Execute(
            StateSnapshot currentState,
            InputData input,
            MovementSettings settings,
            float deltaTime)
        {
            // Cria cópia do estado para modificar
            var newState = currentState;
            newState.Tick = input.Tick;
            newState.LastProcessedInput = input.SequenceNumber;

            // 1. Calcula velocidade horizontal baseado em sprint/crouch
            float speed = CalculateSpeed(currentState, input, settings);

            // Direção de movimento no espaço do mundo
            // MoveDirection.X = direita/esquerda
            // MoveDirection.Y = frente/trás (mapeado para Z)
            Vec3 horizontalVelocity = input.MoveDirection.ToVec3XZ() * speed;

            // 2. Processa componente vertical (pulo/gravidade)
            float verticalVelocity = ProcessVerticalMovement(
                currentState,
                input,
                settings,
                deltaTime,
                out bool isJumping,
                out bool isFalling,
                out bool isGrounded);

            // 3. Monta velocidade final
            newState.Velocity = new Vec3(
                horizontalVelocity.X,
                verticalVelocity,
                horizontalVelocity.Z
            );

            // 4. Aplica movimento (posição += velocidade * deltaTime)
            newState.Position += newState.Velocity * deltaTime;

            // 5. Detecta colisão com chão (simplificado)
            ProcessGroundCollision(ref newState, settings, ref isGrounded, ref isJumping, ref isFalling);

            // 6. Atualiza flags de estado
            newState.Rotation = input.Rotation;
            newState.IsGrounded = isGrounded;
            newState.IsJumping = isJumping;
            newState.IsFalling = isFalling;
            newState.IsSprinting = input.Sprint && isGrounded;
            newState.IsCrouching = input.Crouch && isGrounded;

            return newState;
        }

        /// <summary>
        /// Calcula velocidade horizontal baseado no estado e input.
        /// </summary>
        private static float CalculateSpeed(
            StateSnapshot state,
            InputData input,
            MovementSettings settings)
        {
            // Só pode correr/agachar se estiver no chão
            if (!state.IsGrounded)
            {
                // No ar, mantém velocidade de caminhada
                return settings.WalkSpeed;
            }

            if (input.Crouch)
            {
                return settings.CrouchSpeed;
            }

            if (input.Sprint)
            {
                return settings.SprintSpeed;
            }

            return settings.WalkSpeed;
        }

        /// <summary>
        /// Processa componente vertical do movimento (pulo + gravidade).
        /// </summary>
        private static float ProcessVerticalMovement(
            StateSnapshot state,
            InputData input,
            MovementSettings settings,
            float deltaTime,
            out bool isJumping,
            out bool isFalling,
            out bool isGrounded)
        {
            float verticalVelocity = state.Velocity.Y;
            isGrounded = state.IsGrounded;
            isJumping = state.IsJumping;
            isFalling = state.IsFalling;

            // Pulo: só pode pular se estiver no chão
            if (input.Jump && state.IsGrounded)
            {
                verticalVelocity = settings.JumpForce;
                isGrounded = false;
                isJumping = true;
                isFalling = false;
            }
            else if (!state.IsGrounded)
            {
                // No ar: aplica gravidade
                verticalVelocity += settings.Gravity * deltaTime;

                // Limita velocidade de queda
                if (verticalVelocity < settings.MaxFallSpeed)
                {
                    verticalVelocity = settings.MaxFallSpeed;
                }

                // Detecta se está subindo ou caindo
                isJumping = verticalVelocity > 0;
                isFalling = verticalVelocity < 0;
            }

            return verticalVelocity;
        }

        /// <summary>
        /// Processa colisão com o chão (simplificado).
        /// Em jogo real, usaria raycast ou physics engine.
        /// </summary>
        private static void ProcessGroundCollision(
            ref StateSnapshot state,
            MovementSettings settings,
            ref bool isGrounded,
            ref bool isJumping,
            ref bool isFalling)
        {
            // Colisão simplificada: chão é Y = GroundLevel
            if (state.Position.Y <= settings.GroundLevel)
            {
                // Snap para o chão
                state.Position = new Vec3(
                    state.Position.X,
                    settings.GroundLevel,
                    state.Position.Z
                );

                // Zera velocidade vertical
                state.Velocity = new Vec3(
                    state.Velocity.X,
                    0f,
                    state.Velocity.Z
                );

                // Atualiza flags
                isGrounded = true;
                isJumping = false;
                isFalling = false;
            }
        }

        /// <summary>
        /// Versão simplificada: executa com MovementSettings.Default.
        /// Útil para testes.
        /// </summary>
        public static StateSnapshot Execute(
            StateSnapshot currentState,
            InputData input,
            float deltaTime)
        {
            return Execute(currentState, input, MovementSettings.Default, deltaTime);
        }
    }
}