// XL4Net.Client/Prediction/ClientPrediction.cs

using System;
using System.Collections.Generic;
using XL4Net.Shared.Math;
using XL4Net.Shared.Prediction;

namespace XL4Net.Client.Prediction
{
    /// <summary>
    /// Gerenciador de Client-Side Prediction.
    /// Orquestra input, prediction, e reconciliation.
    /// </summary>
    /// <remarks>
    /// Esta é a classe principal do sistema de prediction.
    /// Ela coordena:
    /// - Captura e execução local de inputs (prediction)
    /// - Armazenamento de inputs e estados
    /// - Reconciliação com estado do servidor
    /// - Re-aplicação de inputs após correção
    /// 
    /// Uso típico (game loop):
    /// ```csharp
    /// // No Update/Tick do cliente
    /// void OnTick()
    /// {
    ///     // 1. Captura input
    ///     var moveDir = new Vec2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
    ///     bool jump = Input.GetButtonDown("Jump");
    ///     
    ///     // 2. Processa (prediction + envia)
    ///     var inputData = prediction.ProcessInput(moveDir, jump, false);
    ///     SendToServer(inputData);
    ///     
    ///     // 3. Renderiza
    ///     character.transform.position = ToUnityVector(prediction.CurrentState.Position);
    /// }
    /// 
    /// // Ao receber estado do servidor
    /// void OnServerState(StateSnapshot serverState)
    /// {
    ///     prediction.OnServerStateReceived(serverState);
    /// }
    /// ```
    /// </remarks>
    public class ClientPrediction
    {
        #region Eventos

        /// <summary>
        /// Disparado quando ocorre misprediction (cliente != servidor).
        /// Útil para debugging e métricas.
        /// </summary>
        /// <param name="predictedState">Estado que cliente tinha predito</param>
        /// <param name="serverState">Estado autoritativo do servidor</param>
        /// <param name="positionDelta">Diferença de posição</param>
        public event Action<StateSnapshot, StateSnapshot, float> OnMisprediction;

        /// <summary>
        /// Disparado quando um comando é processado localmente.
        /// </summary>
        public event Action<ICommand> OnCommandProcessed;

        /// <summary>
        /// Disparado após reconciliation completa.
        /// </summary>
        /// <param name="oldState">Estado antes da correção</param>
        /// <param name="newState">Estado após correção</param>
        /// <param name="inputsReplayed">Quantidade de inputs re-aplicados</param>
        public event Action<StateSnapshot, StateSnapshot, int> OnReconciliationComplete;

        #endregion

        #region Campos Privados

        private readonly InputBuffer _inputBuffer;
        private readonly StateBuffer _stateBuffer;
        private readonly MovementSettings _movementSettings;
        private readonly PredictionSettings _predictionSettings;

        private StateSnapshot _currentState;
        private uint _currentTick;
        private uint _sequenceNumber;
        private bool _isInitialized;

        // Métricas
        private int _totalMispredictions;
        private float _averageMispredictionDelta;

        #endregion

        #region Propriedades

        /// <summary>
        /// Estado atual do jogador (após última prediction).
        /// </summary>
        public StateSnapshot CurrentState => _currentState;

        /// <summary>
        /// Tick atual estimado do servidor.
        /// </summary>
        public uint CurrentTick => _currentTick;

        /// <summary>
        /// Último SequenceNumber usado.
        /// </summary>
        public uint LastSequenceNumber => _sequenceNumber;

        /// <summary>
        /// Quantidade de inputs pendentes (aguardando confirmação).
        /// </summary>
        public int PendingInputsCount => _inputBuffer.Count;

        /// <summary>
        /// Total de mispredictions desde o início.
        /// </summary>
        public int TotalMispredictions => _totalMispredictions;

        /// <summary>
        /// Média de delta de posição em mispredictions.
        /// </summary>
        public float AverageMispredictionDelta => _averageMispredictionDelta;

        /// <summary>
        /// Configurações de movimento.
        /// </summary>
        public MovementSettings MovementSettings => _movementSettings;

        /// <summary>
        /// Configurações de prediction.
        /// </summary>
        public PredictionSettings Settings => _predictionSettings;

        /// <summary>
        /// Verifica se o sistema foi inicializado.
        /// </summary>
        public bool IsInitialized => _isInitialized;

        #endregion

        #region Construtor

        /// <summary>
        /// Cria novo gerenciador de prediction.
        /// </summary>
        /// <param name="movementSettings">Configurações de movimento</param>
        /// <param name="predictionSettings">Configurações de prediction</param>
        public ClientPrediction(
            MovementSettings movementSettings = null,
            PredictionSettings predictionSettings = null)
        {
            _movementSettings = movementSettings ?? MovementSettings.Default;
            _predictionSettings = predictionSettings ?? PredictionSettings.Default;

            _inputBuffer = new InputBuffer(_predictionSettings.InputBufferSize);
            _stateBuffer = new StateBuffer(_predictionSettings.StateBufferSize);

            _currentState = new StateSnapshot();
            _currentTick = 0;
            _sequenceNumber = 0;
            _isInitialized = false;
        }

        #endregion

        #region Inicialização

        /// <summary>
        /// Inicializa o sistema de prediction com estado inicial.
        /// Chamado após spawn ou respawn do jogador.
        /// </summary>
        /// <param name="initialState">Estado inicial do servidor</param>
        /// <param name="serverTick">Tick atual do servidor</param>
        public void Initialize(StateSnapshot initialState, uint serverTick)
        {
            _currentState = initialState;
            _currentTick = serverTick;
            _sequenceNumber = 0;

            _inputBuffer.Clear();
            _stateBuffer.Clear();
            _stateBuffer.Add(initialState);

            _totalMispredictions = 0;
            _averageMispredictionDelta = 0f;

            _isInitialized = true;
        }

        /// <summary>
        /// Inicializa com posição inicial simples.
        /// </summary>
        public void Initialize(Vec3 position, uint serverTick)
        {
            var initialState = StateSnapshot.Initial(serverTick, position);
            Initialize(initialState, serverTick);
        }

        #endregion

        #region Processamento de Input

        /// <summary>
        /// Processa input do jogador.
        /// Executa prediction local e retorna InputData para enviar ao servidor.
        /// </summary>
        /// <param name="moveDirection">Direção de movimento</param>
        /// <param name="jump">Se quer pular</param>
        /// <param name="sprint">Se está correndo</param>
        /// <param name="rotation">Rotação Y</param>
        /// <returns>InputData para enviar ao servidor</returns>
        public InputData ProcessInput(Vec2 moveDirection, bool jump, bool sprint, float rotation = 0f)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("ClientPrediction não inicializado. Chame Initialize() primeiro.");

            // 1. Avança tick
            _currentTick++;
            _sequenceNumber++;

            // 2. Cria comando
            var command = new MoveCommand(
                _currentTick,
                _sequenceNumber,
                moveDirection,
                jump,
                sprint,
                rotation,
                _movementSettings
            );

            // 3. Executa localmente (PREDICTION)
            _currentState = command.Execute(_currentState, _predictionSettings.TickDelta);

            // 4. Guarda nos buffers
            _inputBuffer.Add(command);
            _stateBuffer.Add(_currentState);

            // 5. Dispara evento
            OnCommandProcessed?.Invoke(command);

            // 6. Retorna dados para enviar ao servidor
            return command.ToInputData();
        }

        /// <summary>
        /// Processa tick vazio (sem input significativo).
        /// Ainda precisa processar física (gravidade, etc).
        /// </summary>
        public InputData ProcessEmptyTick()
        {
            return ProcessInput(Vec2.Zero, false, false, _currentState.Rotation);
        }

        #endregion

        #region Reconciliation

        /// <summary>
        /// Processa estado recebido do servidor.
        /// Compara com prediction e corrige se necessário.
        /// </summary>
        /// <param name="serverState">Estado autoritativo do servidor</param>
        public void OnServerStateReceived(StateSnapshot serverState)
        {
            if (!_isInitialized)
                return;

            // 1. Busca nosso estado predito para o mesmo tick
            if (!_stateBuffer.TryGet(serverState.Tick, out var predictedState))
            {
                // Tick muito antigo ou futuro, ignora
                return;
            }

            // 2. Compara estados
            bool needsReconciliation = !predictedState.ApproximatelyEquals(
                serverState,
                _predictionSettings.PositionTolerance,
                _predictionSettings.VelocityTolerance
            );

            if (needsReconciliation)
            {
                // 3. Registra misprediction
                float delta = predictedState.PositionDelta(serverState);
                RecordMisprediction(predictedState, serverState, delta);

                // 4. Executa reconciliation
                Reconcile(serverState);
            }

            // 5. Remove inputs confirmados do buffer
            _inputBuffer.RemoveUpTo(serverState.LastProcessedInput);
        }

        /// <summary>
        /// Executa reconciliation completa.
        /// </summary>
        /// <param name="serverState">Estado autoritativo para usar como base</param>
        private void Reconcile(StateSnapshot serverState)
        {
            var oldState = _currentState;

            // 1. Aplica estado do servidor como base
            var state = serverState;

            // 2. Re-aplica todos os inputs não confirmados
            var pendingInputs = _inputBuffer.GetAll();
            int replayed = 0;

            foreach (var cmd in pendingInputs)
            {
                // Só re-aplica inputs APÓS o tick do servidor
                if (cmd.Tick > serverState.Tick)
                {
                    state = cmd.Execute(state, _predictionSettings.TickDelta);
                    _stateBuffer.Add(state);
                    replayed++;
                }
            }

            // 3. Atualiza estado atual
            _currentState = state;

            // 4. Dispara evento
            OnReconciliationComplete?.Invoke(oldState, _currentState, replayed);
        }

        /// <summary>
        /// Registra uma misprediction para métricas.
        /// </summary>
        private void RecordMisprediction(StateSnapshot predicted, StateSnapshot server, float delta)
        {
            _totalMispredictions++;

            // Média móvel exponencial
            const float alpha = 0.1f;
            _averageMispredictionDelta = alpha * delta + (1 - alpha) * _averageMispredictionDelta;

            OnMisprediction?.Invoke(predicted, server, delta);
        }

        #endregion

        #region Tick Management

        /// <summary>
        /// Avança o tick sem processar input.
        /// Usado se tick aconteceu mas não houve input do jogador.
        /// </summary>
        public void AdvanceTick()
        {
            _currentTick++;
        }

        /// <summary>
        /// Sincroniza tick com o servidor.
        /// Chamado periodicamente baseado em ping/pong.
        /// </summary>
        /// <param name="serverTick">Tick atual do servidor</param>
        /// <param name="oneWayLatency">Latência em ms</param>
        public void SyncTick(uint serverTick, int oneWayLatency)
        {
            // Estima tick atual do servidor considerando latência
            float ticksInLatency = oneWayLatency / 1000f / _predictionSettings.TickDelta;
            uint estimatedServerTick = serverTick + (uint)ticksInLatency;

            // Ajusta suavemente se diferença grande
            int drift = (int)estimatedServerTick - (int)_currentTick;

            if (System.Math.Abs(drift) > _predictionSettings.MaxTickDrift)
            {
                // Drift muito grande, snap para o valor correto
                _currentTick = estimatedServerTick;
            }
            else if (drift != 0)
            {
                // Drift pequeno, ajusta gradualmente
                _currentTick = (uint)((int)_currentTick + drift / 4);
            }
        }

        #endregion

        #region Reset

        /// <summary>
        /// Reseta o sistema de prediction.
        /// Chamado em disconnect ou morte.
        /// </summary>
        public void Reset()
        {
            _inputBuffer.Clear();
            _stateBuffer.Clear();
            _currentState = default;
            _currentTick = 0;
            _sequenceNumber = 0;
            _isInitialized = false;
        }

        #endregion

        #region Debug

        /// <summary>
        /// Retorna informações de debug.
        /// </summary>
        public string GetDebugInfo()
        {
            return $"ClientPrediction:\n" +
                   $"  Tick: {_currentTick}\n" +
                   $"  Position: {_currentState.Position}\n" +
                   $"  Velocity: {_currentState.Velocity}\n" +
                   $"  Pending Inputs: {_inputBuffer.Count}\n" +
                   $"  States Buffered: {_stateBuffer.Count}\n" +
                   $"  Total Mispredictions: {_totalMispredictions}\n" +
                   $"  Avg Misprediction Delta: {_averageMispredictionDelta:F3}";
        }

        public override string ToString()
        {
            return $"ClientPrediction[Tick={_currentTick}, Pos={_currentState.Position}, " +
                   $"Pending={_inputBuffer.Count}]";
        }

        #endregion
    }

    /// <summary>
    /// Configurações do sistema de prediction.
    /// </summary>
    public class PredictionSettings
    {
        /// <summary>
        /// Taxa de ticks por segundo.
        /// Deve ser igual ao servidor!
        /// </summary>
        public int TickRate { get; set; } = 30;

        /// <summary>
        /// Delta time de cada tick (calculado de TickRate).
        /// </summary>
        public float TickDelta => 1f / TickRate;

        /// <summary>
        /// Tamanho do buffer de inputs (em ticks).
        /// </summary>
        public int InputBufferSize { get; set; } = 64;

        /// <summary>
        /// Tamanho do buffer de estados (em ticks).
        /// </summary>
        public int StateBufferSize { get; set; } = 64;

        /// <summary>
        /// Tolerância de posição para considerar estados iguais.
        /// Abaixo disso não faz reconciliation.
        /// </summary>
        public float PositionTolerance { get; set; } = 0.01f;

        /// <summary>
        /// Tolerância de velocidade para considerar estados iguais.
        /// </summary>
        public float VelocityTolerance { get; set; } = 0.1f;

        /// <summary>
        /// Drift máximo de tick antes de snap forçado.
        /// </summary>
        public int MaxTickDrift { get; set; } = 10;

        /// <summary>
        /// Configurações padrão.
        /// </summary>
        public static PredictionSettings Default { get; } = new PredictionSettings();
    }
}