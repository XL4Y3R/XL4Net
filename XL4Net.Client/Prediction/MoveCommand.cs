// XL4Net.Client/Prediction/MoveCommand.cs

using XL4Net.Shared.Math;
using XL4Net.Shared.Prediction;

namespace XL4Net.Client.Prediction
{
    /// <summary>
    /// Comando de movimento do jogador.
    /// Encapsula um input de movimento para prediction e reconciliation.
    /// </summary>
    public class MoveCommand : ICommand
    {
        // ============================================
        // CAMPOS PRIVADOS
        // ============================================

        private readonly Vec2 _moveDirection;
        private readonly bool _jump;
        private readonly bool _sprint;
        private readonly float _rotation;
        private readonly MovementSettings _settings;

        // ============================================
        // PROPRIEDADES (ICommand)
        // ============================================

        /// <summary>
        /// Tick do servidor quando comando foi criado.
        /// </summary>
        public uint Tick { get; }

        /// <summary>
        /// Número sequencial único.
        /// </summary>
        public uint SequenceNumber { get; }

        // ============================================
        // PROPRIEDADES EXTRAS (para debug)
        // ============================================

        /// <summary>
        /// Direção de movimento.
        /// </summary>
        public Vec2 MoveDirection => _moveDirection;

        /// <summary>
        /// Está pulando?
        /// </summary>
        public bool IsJumping => _jump;

        /// <summary>
        /// Está correndo?
        /// </summary>
        public bool IsSprinting => _sprint;

        /// <summary>
        /// Rotação Y.
        /// </summary>
        public float Rotation => _rotation;

        // ============================================
        // CONSTRUTOR
        // ============================================

        /// <summary>
        /// Cria comando de movimento.
        /// </summary>
        public MoveCommand(
            uint tick,
            uint sequenceNumber,
            Vec2 moveDirection,
            bool jump,
            bool sprint,
            float rotation = 0f,
            MovementSettings settings = null)
        {
            Tick = tick;
            SequenceNumber = sequenceNumber;
            _moveDirection = moveDirection;
            _jump = jump;
            _sprint = sprint;
            _rotation = rotation;
            _settings = settings ?? MovementSettings.Default;
        }

        // ============================================
        // MÉTODOS (ICommand)
        // ============================================

        /// <summary>
        /// Executa o comando no estado atual.
        /// Usa MovementPhysics.Execute() para garantir determinismo.
        /// </summary>
        public StateSnapshot Execute(StateSnapshot currentState, float deltaTime)
        {
            // Converte para InputData
            var inputData = ToInputData();

            // Usa MovementPhysics compartilhado (mesmo código do servidor!)
            return MovementPhysics.Execute(currentState, inputData, _settings, deltaTime);
        }

        /// <summary>
        /// Converte para InputData para enviar ao servidor.
        /// </summary>
        public InputData ToInputData()
        {
            var input = new InputData
            {
                Tick = Tick,
                SequenceNumber = SequenceNumber,
                MoveDirection = _moveDirection,
                Rotation = _rotation,
                ActionFlags = 0
            };

            input.Jump = _jump;
            input.Sprint = _sprint;

            return input;
        }

        // ============================================
        // DEBUG
        // ============================================

        public override string ToString()
        {
            return $"MoveCmd[Tick={Tick}, Seq={SequenceNumber}, Dir={_moveDirection}, Jump={_jump}, Sprint={_sprint}]";
        }
    }
}