# XL4Net - Padrões de Código

**Versão:** 1.0  
**Data:** 2024-11-20  

---

## 1. NAMING CONVENTIONS

### 1.1 C# (Seguir Microsoft Guidelines)

- **Classes/Interfaces:** PascalCase
  ```csharp
  public class PlayerMoveHandler { }
  public interface ICommand { }
  ```

- **Métodos:** PascalCase
  ```csharp
  public void ProcessInput() { }
  public async Task SendMessageAsync() { }
  ```

- **Variáveis locais:** camelCase
  ```csharp
  var playerPosition = GetPosition();
  float deltaTime = 0.016f;
  ```

- **Campos privados:** _camelCase (underscore prefix)
  ```csharp
  private InputBuffer _inputBuffer;
  private int _tickRate;
  ```

- **Constantes:** UPPER_CASE
  ```csharp
  public const int MAX_PLAYERS = 100;
  public const ushort DEFAULT_PORT = 7777;
  ```

- **Propriedades:** PascalCase
  ```csharp
  public int TickRate { get; set; }
  public bool IsConnected { get; private set; }
  ```

### 1.2 Arquivos

- **1 classe por arquivo**
- Nome do arquivo = nome da classe
- Pastas em PascalCase

Exemplo:
```
XL4Net.Client/
└── Prediction/
    ├── ICommand.cs               // Interface
    ├── ClientPrediction.cs       // Classe
    └── Commands/
        ├── MoveCommand.cs
        └── AttackCommand.cs
```

---

## 2. ESTRUTURA DE ARQUIVOS

### 2.1 Template Padrão

```csharp
// XL4Net.Client/Prediction/Commands/MoveCommand.cs

using System;
using XL4Net.Shared.Protocol;
using XL4Net.Shared.Models;

namespace XL4Net.Client.Prediction.Commands
{
    /// <summary>
    /// Comando de movimentação do player.
    /// Implementa client-side prediction.
    /// </summary>
    public class MoveCommand : ICommand
    {
        // Campos privados
        private readonly Vector3 _direction;
        private readonly Player _player;
        
        // Propriedades
        public long Timestamp { get; }
        
        // Construtor
        public MoveCommand(Vector3 direction, Player player, long timestamp)
        {
            _direction = direction;
            _player = player;
            Timestamp = timestamp;
        }
        
        // Métodos públicos
        public void Execute()
        {
            // Validação
            if (_player == null)
                throw new ArgumentNullException(nameof(_player));
            
            // Implementação
            _player.Move(_direction);
        }
        
        public INetworkMessage ToMessage()
        {
            return new InputMessage
            {
                Direction = _direction,
                Timestamp = Timestamp
            };
        }
        
        // Métodos privados (se necessário)
        private bool IsValidDirection(Vector3 dir)
        {
            return dir.magnitude <= 1f;
        }
    }
}
```

### 2.2 Ordem dos Membros

1. Campos privados
2. Propriedades públicas
3. Construtor(es)
4. Métodos públicos
5. Métodos privados

---

## 3. COMENTÁRIOS

### 3.1 Comentários em Português

```csharp
// ✅ BOM
// Valida se o movimento é permitido
if (!IsValidMove(position))
{
    // Movimento inválido, ignora comando
    return;
}

// ❌ RUIM
// Validates if movement is allowed
if (!IsValidMove(position))
{
    return;
}
```

### 3.2 XML Documentation em Português

```csharp
/// <summary>
/// Processa input do player e aplica client-side prediction.
/// </summary>
/// <param name="command">Comando a ser executado</param>
/// <returns>True se o comando foi processado com sucesso</returns>
/// <exception cref="ArgumentNullException">Se command for null</exception>
public bool ProcessInput(ICommand command)
{
    if (command == null)
        throw new ArgumentNullException(nameof(command));
    
    // ...
}
```

### 3.3 Comentários TODO/FIXME

```csharp
// TODO: Implementar lag compensation
// FIXME: Bug quando latência > 500ms
// NOTE: Este código foi copiado do Fishnet
// HACK: Solução temporária, refatorar depois
```

---

## 4. ERROR HANDLING

### 4.1 Try-Catch em Operações de I/O

```csharp
// ✅ BOM - SEMPRE usar try-catch em I/O
public async Task<bool> ConnectAsync(string host, int port)
{
    try
    {
        await _socket.ConnectAsync(host, port);
        Log.Info($"Connected to {host}:{port}");
        return true;
    }
    catch (SocketException ex)
    {
        Log.Error($"Socket error: {ex.Message}");
        return false;
    }
    catch (Exception ex)
    {
        Log.Error($"Unexpected error: {ex.Message}");
        throw; // Re-throw se não souber lidar
    }
}

// ❌ RUIM - Sem tratamento
public async Task ConnectAsync(string host, int port)
{
    await _socket.ConnectAsync(host, port);
}
```

### 4.2 Validação de Argumentos

```csharp
public void ProcessMessage(INetworkMessage message)
{
    // Valida no início do método
    if (message == null)
        throw new ArgumentNullException(nameof(message));
    
    if (message.Type == MessageType.Unknown)
        throw new ArgumentException("Invalid message type", nameof(message));
    
    // Processa mensagem
    // ...
}
```

### 4.3 Logging de Erros

```csharp
try
{
    ProcessPacket(data);
}
catch (Exception ex)
{
    // Log com contexto
    Log.Error($"Failed to process packet from {clientId}: {ex.Message}");
    Log.Debug($"Stack trace: {ex.StackTrace}");
    
    // Opcional: Desconecta cliente problemático
    DisconnectClient(clientId, DisconnectReason.InvalidData);
}
```

---

## 5. LOGGING

### 5.1 Níveis de Log

```csharp
// DEBUG - Desenvolvimento/troubleshooting
Log.Debug($"Received packet: seq={packet.Sequence}, size={packet.Data.Length}");

// INFO - Eventos importantes
Log.Info($"Player {playerId} connected from {ipAddress}");

// WARNING - Situações anormais mas recuperáveis
Log.Warning($"High latency detected: {latency}ms for player {playerId}");

// ERROR - Erros que afetam funcionalidade
Log.Error($"Failed to serialize message: {ex.Message}");

// FATAL - Erros críticos que param o servidor
Log.Fatal($"Database connection lost: {ex.Message}");
```

### 5.2 Logs em Inglês

```csharp
// ✅ SEMPRE em inglês (padrão da indústria)
Log.Info("Player connected: id={0}", playerId);
Log.Error("Failed to deserialize packet");

// ❌ NUNCA em português
Log.Info("Jogador conectou: id={0}", playerId);
Log.Error("Falha ao desserializar pacote");
```

### 5.3 Performance Logging

```csharp
// Usa interpolação de string só quando necessário
// ❌ RUIM - Always allocates string
Log.Debug($"Position: {player.Position}");

// ✅ BOM - Only allocates if debug enabled
Log.Debug("Position: {0}", player.Position);

// ✅ MELHOR - Verifica nível antes
if (Log.IsDebugEnabled)
{
    Log.Debug($"Complex calculation: {ExpensiveOperation()}");
}
```

---

## 6. DESIGN PATTERNS

### 6.1 Observer Pattern (Events)

```csharp
public class NetworkClient
{
    // Declare eventos públicos
    public event Action<int> OnConnected;
    public event Action<DisconnectReason> OnDisconnected;
    public event Action<INetworkMessage> OnMessageReceived;
    
    // Dispara eventos com null-conditional
    private void HandleConnect(int connectionId)
    {
        OnConnected?.Invoke(connectionId);
    }
    
    // Limpa eventos no dispose
    public void Dispose()
    {
        OnConnected = null;
        OnDisconnected = null;
        OnMessageReceived = null;
    }
}

// Uso:
_client.OnConnected += (id) => 
{
    Console.WriteLine($"Connected with ID: {id}");
};
```

### 6.2 Command Pattern

```csharp
public interface ICommand
{
    long Timestamp { get; }
    void Execute();
    INetworkMessage ToMessage();
}

// Implementação
public class MoveCommand : ICommand
{
    private readonly Vector3 _direction;
    
    public long Timestamp { get; }
    
    public MoveCommand(Vector3 direction, long timestamp)
    {
        _direction = direction;
        Timestamp = timestamp;
    }
    
    public void Execute()
    {
        // Client-side prediction
        player.Move(_direction);
    }
    
    public INetworkMessage ToMessage()
    {
        return new InputMessage 
        { 
            Direction = _direction, 
            Timestamp = Timestamp 
        };
    }
}
```

### 6.3 Strategy Pattern (Message Handlers)

```csharp
public interface IMessageHandler
{
    MessageType Type { get; }
    void Handle(NetworkConnection connection, INetworkMessage message);
}

public class PlayerMoveHandler : IMessageHandler
{
    public MessageType Type => MessageType.PlayerMove;
    
    public void Handle(NetworkConnection conn, INetworkMessage msg)
    {
        var moveMsg = (PlayerMoveMessage)msg;
        
        // Valida movimento
        if (!IsValidMove(conn.PlayerId, moveMsg))
        {
            Log.Warning($"Invalid move from {conn.PlayerId}");
            return;
        }
        
        // Aplica movimento
        ApplyMovement(conn.PlayerId, moveMsg);
        
        // Broadcast
        BroadcastMovement(conn.PlayerId, moveMsg);
    }
    
    private bool IsValidMove(int playerId, PlayerMoveMessage msg)
    {
        // Validação server-side
        return true;
    }
}

// Registry
public class MessageHandlerRegistry
{
    private Dictionary<MessageType, IMessageHandler> _handlers = new();
    
    public void Register(IMessageHandler handler)
    {
        _handlers[handler.Type] = handler;
    }
    
    public void Handle(NetworkConnection conn, INetworkMessage msg)
    {
        if (_handlers.TryGetValue(msg.Type, out var handler))
        {
            handler.Handle(conn, msg);
        }
        else
        {
            Log.Warning($"No handler for {msg.Type}");
        }
    }
}
```

### 6.4 State Pattern

```csharp
public interface IGameState
{
    void Enter();
    void Update(float deltaTime);
    void Exit();
}

public class PlayingState : IGameState
{
    private readonly GameServer _server;
    
    public PlayingState(GameServer server)
    {
        _server = server;
    }
    
    public void Enter()
    {
        Log.Info("Entering Playing state");
        _server.StartGameLoop();
    }
    
    public void Update(float deltaTime)
    {
        _server.Tick(deltaTime);
        
        if (_server.IsGameOver)
        {
            _server.ChangeState(new GameOverState(_server));
        }
    }
    
    public void Exit()
    {
        Log.Info("Exiting Playing state");
        _server.StopGameLoop();
    }
}
```

---

## 7. OBJECT POOLING

### 7.1 SEMPRE usar pools para

#### Packets

```csharp
// ❌ NUNCA
var packet = new Packet();

// ✅ SEMPRE
var packet = PacketPool.Rent();
try
{
    // Usa packet
    Send(packet);
}
finally
{
    PacketPool.Return(packet);
}
```

#### Messages

```csharp
// ❌ NUNCA
var msg = new PlayerMoveMessage();

// ✅ SEMPRE
var msg = MessagePool.Rent<PlayerMoveMessage>();
try
{
    msg.PlayerId = 123;
    msg.Position = position;
    Broadcast(msg);
}
finally
{
    MessagePool.Return(msg);
}
```

#### Buffers

```csharp
// ❌ NUNCA
byte[] buffer = new byte[1024];

// ✅ SEMPRE
var buffer = BufferPool.Rent(1024);
try
{
    // Usa buffer
    Serialize(message, buffer);
}
finally
{
    BufferPool.Return(buffer);
}
```

### 7.2 Pattern: Using Statement (recomendado)

```csharp
public struct PooledObject<T> : IDisposable where T : class
{
    private readonly ObjectPool<T> _pool;
    public T Value { get; }
    
    public PooledObject(ObjectPool<T> pool, T value)
    {
        _pool = pool;
        Value = value;
    }
    
    public void Dispose() => _pool.Return(Value);
}

// Uso:
using (var rental = PacketPool.RentDisposable())
{
    var packet = rental.Value;
    packet.Sequence = 100;
    Send(packet);
    
} // Automaticamente retorna ao pool
```

### 7.3 Regras de Pooling

1. **NUNCA** esqueça de Return()
2. **SEMPRE** use try-finally ou using
3. **NÃO** guarde referência após Return()
4. **LIMPE** estado no Reset() (IPoolable)
5. **MONITORE** leaks com métricas

```csharp
// ✅ BOM - Try-finally garante return
var msg = MessagePool.Rent<ChatMessage>();
try
{
    msg.Text = "Hello";
    Send(msg);
}
finally
{
    MessagePool.Return(msg); // SEMPRE executa
}

// ❌ RUIM - Se Send() lançar exception, não retorna
var msg = MessagePool.Rent<ChatMessage>();
msg.Text = "Hello";
Send(msg);
MessagePool.Return(msg); // Pode não executar!
```

---

## 8. ASYNC/AWAIT

### 8.1 SEMPRE usar async para I/O

```csharp
// ✅ BOM
public async Task<bool> ConnectAsync(string host, int port)
{
    try
    {
        await _socket.ConnectAsync(host, port);
        return true;
    }
    catch (Exception ex)
    {
        Log.Error($"Connect failed: {ex.Message}");
        return false;
    }
}

// ❌ RUIM - Blocking
public bool Connect(string host, int port)
{
    _socket.Connect(host, port); // Bloqueia thread!
    return true;
}
```

### 8.2 NUNCA usar .Result ou .Wait()

```csharp
// ❌ NUNCA - Causa deadlock
var result = ConnectAsync().Result;
SendAsync().Wait();

// ✅ SEMPRE - Await
var result = await ConnectAsync();
await SendAsync();
```

### 8.3 ConfigureAwait em bibliotecas

```csharp
// Em código de biblioteca (XL4Net.Client/Server)
public async Task SendAsync(byte[] data)
{
    // ConfigureAwait(false) evita capturar contexto
    await _stream.WriteAsync(data).ConfigureAwait(false);
}

// Em código de aplicação (Unity)
public async Task LoadDataAsync()
{
    // Não usa ConfigureAwait - precisa voltar ao thread Unity
    var data = await _client.ReceiveAsync();
    UpdateUI(data); // Precisa estar no main thread
}
```

---

## 9. PERFORMANCE

### 9.1 Evitar Alocações Desnecessárias

```csharp
// ❌ RUIM - Aloca array a cada frame
public void Update()
{
    byte[] buffer = new byte[1024]; // Aloca!
    Serialize(message, buffer);
}

// ✅ BOM - Reutiliza buffer
private byte[] _buffer = new byte[1024];

public void Update()
{
    Serialize(message, _buffer); // Zero allocation
}
```

### 9.2 Usar Span<T> quando possível (.NET 9)

```csharp
// ✅ BOM - Zero allocation com Span
public void ProcessPacket(Span<byte> data)
{
    var header = data.Slice(0, 4);
    var payload = data.Slice(4);
    
    // Processa sem copiar dados
}

// Chamada:
Span<byte> buffer = stackalloc byte[256]; // Stack allocation!
ProcessPacket(buffer);
```

### 9.3 String Interpolation vs Format

```csharp
// Para logs frequentes:
// ❌ RUIM - Sempre aloca string
Log.Debug($"Player {playerId} moved to {position}");

// ✅ BOM - Só aloca se debug enabled
Log.Debug("Player {0} moved to {1}", playerId, position);

// Para strings que serão usadas:
// ✅ OK - Interpolation é mais legível
string message = $"Welcome, {playerName}!";
```

### 9.4 LINQ vs Loops

```csharp
// Para hot paths (chamado frequentemente):
// ❌ RUIM - LINQ aloca enumerator
var nearbyPlayers = _players.Where(p => IsNear(p)).ToList();

// ✅ BOM - Loop manual, zero allocation
var nearbyPlayers = new List<int>(_players.Count);
foreach (var player in _players)
{
    if (IsNear(player))
        nearbyPlayers.Add(player);
}

// Para código não crítico (setup, config):
// ✅ OK - LINQ é mais legível
var admins = accounts.Where(a => a.IsAdmin).ToList();
```

---

## 10. TESTES

### 10.1 Naming

```csharp
[TestFixture]
public class ClientPredictionTests
{
    [Test]
    public void ProcessInput_WithValidCommand_ShouldExecuteAndBuffer()
    {
        // Arrange
        var prediction = new ClientPrediction();
        var command = new MoveCommand(Vector3.forward, 12345);
        
        // Act
        var result = prediction.ProcessInput(command);
        
        // Assert
        Assert.IsTrue(result);
        Assert.AreEqual(1, prediction.InputBuffer.Count);
    }
    
    [Test]
    public void ProcessInput_WithNullCommand_ShouldThrowArgumentNullException()
    {
        // Arrange
        var prediction = new ClientPrediction();
        
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            prediction.ProcessInput(null)
        );
    }
}
```

### 10.2 Arrange-Act-Assert Pattern

```csharp
[Test]
public void Connect_WithValidAddress_ShouldReturnTrue()
{
    // Arrange - Setup
    var client = new NetworkClient();
    var host = "localhost";
    var port = 7777;
    
    // Act - Executa
    var result = client.Connect(host, port);
    
    // Assert - Verifica
    Assert.IsTrue(result);
    Assert.IsTrue(client.IsConnected);
}
```

---

## 11. GIT COMMITS

### 11.1 Formato

```
[Fase] Breve descrição (50 chars)

Descrição detalhada opcional.

- Item 1
- Item 2
- Item 3
```

### 11.2 Exemplos

```
[Phase1] Implement TCP client/server

Created basic TCP connection management with
handshake, heartbeat, and graceful disconnect.

- TcpClient.cs with ConnectAsync()
- TcpServer.cs with AcceptClientsAsync()
- Connection handshake (SYN/ACK)
- Heartbeat every 1 second
```

```
[Phase4] Add Command pattern for inputs

Implemented ICommand interface and MoveCommand.
Follows Fishnet structure for client-side prediction.

- ICommand.cs with Timestamp and Execute()
- MoveCommand with prediction logic
- Unit tests pending (Phase 8)
```

---

## 12. CODE REVIEW CHECKLIST

Antes de commitar:

- [ ] Código segue naming conventions
- [ ] Comentários em português
- [ ] Logs em inglês
- [ ] Error handling apropriado
- [ ] Pools sendo usados corretamente
- [ ] Sem alocações desnecessárias em hot paths
- [ ] Async/await usado corretamente
- [ ] Tests passando (quando aplicável)
- [ ] Sem warnings do compiler
- [ ] Documentação XML atualizada

---

## 13. REFERÊNCIAS

- [Microsoft C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- [Framework Design Guidelines](https://docs.microsoft.com/en-us/dotnet/standard/design-guidelines/)
- [Async Best Practices](https://docs.microsoft.com/en-us/archive/msdn-magazine/2013/march/async-await-best-practices-in-asynchronous-programming)

---

**FIM DO DOCUMENTO**

Versão 1.0 - 2024-11-20
