// XL4Net.Shared/Prediction/MovementSettings.cs

namespace XL4Net.Shared.Prediction
{
    /// <summary>
    /// Configurações de movimento do jogador.
    /// Compartilhado entre cliente e servidor.
    /// </summary>
    /// <remarks>
    /// CRÍTICO: Estas configurações DEVEM ser IDÊNTICAS no cliente e servidor!
    /// Qualquer diferença causa misprediction constante.
    /// 
    /// Recomendações:
    /// 1. Usar valores fixos (hardcoded) inicialmente
    /// 2. Futuramente: carregar de arquivo de configuração
    /// 3. Alternativa: servidor envia config no connect
    /// 
    /// Exemplo de uso:
    /// ```csharp
    /// // Cliente
    /// var settings = MovementSettings.Default;
    /// var cmd = new MoveCommand(tick, seq, moveDir, jump, sprint, rotation, settings);
    /// var newState = cmd.Execute(currentState, deltaTime);
    /// 
    /// // Servidor
    /// var settings = MovementSettings.Default; // MESMO!
    /// var newState = MovementPhysics.Execute(currentState, inputData, settings, deltaTime);
    /// ```
    /// </remarks>
    public class MovementSettings
    {
        // ============================================
        // VELOCIDADES
        // ============================================

        /// <summary>
        /// Velocidade de caminhada (unidades/segundo).
        /// </summary>
        public float WalkSpeed { get; set; } = 5f;

        /// <summary>
        /// Velocidade de corrida (unidades/segundo).
        /// </summary>
        public float SprintSpeed { get; set; } = 8f;

        /// <summary>
        /// Velocidade agachado (unidades/segundo).
        /// </summary>
        public float CrouchSpeed { get; set; } = 2.5f;

        // ============================================
        // PULO E GRAVIDADE
        // ============================================

        /// <summary>
        /// Força do pulo (velocidade Y inicial).
        /// </summary>
        public float JumpForce { get; set; } = 10f;

        /// <summary>
        /// Gravidade (negativo = para baixo).
        /// </summary>
        public float Gravity { get; set; } = -20f;

        /// <summary>
        /// Velocidade máxima de queda (negativo).
        /// Evita que o jogador acelere indefinidamente.
        /// </summary>
        public float MaxFallSpeed { get; set; } = -50f;

        // ============================================
        // COLISÃO (simplificado)
        // ============================================

        /// <summary>
        /// Altura do chão (Y = 0 por padrão).
        /// Em jogo real, isso viria de detecção de colisão.
        /// </summary>
        public float GroundLevel { get; set; } = 0f;

        // ============================================
        // INSTÂNCIA PADRÃO
        // ============================================

        /// <summary>
        /// Configurações padrão.
        /// USAR ESTA INSTÂNCIA em cliente e servidor para garantir igualdade!
        /// </summary>
        public static MovementSettings Default { get; } = new MovementSettings();

        // ============================================
        // MÉTODOS
        // ============================================

        /// <summary>
        /// Cria cópia das configurações.
        /// Útil para ter configurações diferentes por personagem/classe.
        /// </summary>
        public MovementSettings Clone()
        {
            return new MovementSettings
            {
                WalkSpeed = WalkSpeed,
                SprintSpeed = SprintSpeed,
                CrouchSpeed = CrouchSpeed,
                JumpForce = JumpForce,
                Gravity = Gravity,
                MaxFallSpeed = MaxFallSpeed,
                GroundLevel = GroundLevel
            };
        }

        public override string ToString()
        {
            return $"MovementSettings[Walk={WalkSpeed}, Sprint={SprintSpeed}, Jump={JumpForce}, Gravity={Gravity}]";
        }
    }
}