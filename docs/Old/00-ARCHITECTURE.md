# XL4Net - Game Networking Framework
## Documento de Arquitetura

**Versão:** 1.0  
**Data:** 2024-11-20  
**Autor:** XL4Y3R  

---

## 1. VISÃO GERAL

### 1.1 Objetivo

XL4Net é um framework de networking escalável e reutilizável para jogos multiplayer em Unity. Projetado inicialmente para suportar um MMO, mas aplicável a qualquer gênero multiplayer (ARPG, survival, battle royale, co-op, etc).

A arquitetura suporta desde **10 até milhares de jogadores simultâneos** através de design modular e escalável.

### 1.2 Princípios

- **Escalabilidade**: Funciona para 10 players, funciona para 5000
- **Modularidade**: Cada componente é independente e reutilizável
- **Performance**: Object pooling, zero allocation durante gameplay
- **Aprendizado**: Baseado em Fishnet, mas compreensível e documentado
- **Production-ready**: Logging, métricas, error handling desde o início

### 1.3 Inspiração

**Código base:** [Fishnet Networking](https://github.com/FirstGearGames/FishNet)
- Transport layer (Tugboat)
- Prediction/Reconciliation
- Interest Management

Vamos estudar, adaptar e melhorar com nossa arquitetura.

---

## 2. STACK TECNOLÓGICA

| Componente | Tecnologia | Versão | Justificativa |
|------------|-----------|--------|---------------|
| **Shared** | .NET Standard | 2.1 | Compatibilidade Unity + .NET 9 |
| **Client** | .NET Standard | 2.1 | Unity 6.2+ |
| **Server** | .NET | 9 | Performance moderna |
| **AuthServer** | .NET | 9 | Performance + async |
| **Serialização** | MessagePack | Latest | Performance + API moderna |
| **Database** | PostgreSQL | 16+ | Concorrência + JSONB |
| **Transport** | Custom TCP/UDP | - | Controle total, baseado em Fishnet |
| **Unity** | Unity | 6.2+ | LTS mais recente |

### 2.1 Decisões Técnicas

| Decisão | Escolha | Alternativas | Motivo |
|---------|---------|--------------|--------|
| **Serialização** | MessagePack | ProtoBuf, JSON | API moderna, performance suficiente |
| **Database** | PostgreSQL | MySQL, SQLite | Melhor concorrência, JSONB |
| **Transport** | Custom TCP/UDP | LiteNetLib, Mirror | Controle total, aprendizado |
| **Patterns** | Observer+Command+Strategy+State | - | Escalabilidade e manutenibilidade |

---

## 3. ARQUITETURA DE PROJETOS

### 3.1 Estrutura da Solution

```
XL4Net/
│
├── src/
│   ├── XL4Net.Shared/              # .NET Standard 2.1
│   ├── XL4Net.Client/              # .NET Standard 2.1
│   ├── XL4Net.Server/              # .NET 9
│   └── XL4Net.AuthServer/          # .NET 9
│
├── tests/
│   ├── XL4Net.Tests/               # .NET 9
│   └── XL4Net.IntegrationTests/    # .NET 9
│
├── examples/
│   ├── SimpleGame.Client/          # Unity Project
│   └── SimpleGame.Server/          # .NET 9 Console
│
└── docs/
    ├── 00-ARCHITECTURE.md          # Este documento
    ├── 01-CODING-STANDARDS.md
    ├── 02-PROJECT-STATE.md
    ├── 03-WORKFLOW.md
    └── phases/
        ├── PHASE-01-TRANSPORT.md
        ├── PHASE-02-AUTH.md
        ├── PHASE-03-GAMESERVER.md
        ├── PHASE-04-PREDICTION.md
        ├── PHASE-05-RECONCILIATION.md
        ├── PHASE-06-AOI.md
        ├── PHASE-07-MULTISERVER.md
        ├── PHASE-08-OPTIMIZATION.md
        └── PHASE-09-DOCUMENTATION.md
```

### 3.2 Dependências entre Projetos

```
AuthServer -----> Shared
GameServer -----> Shared
Client ---------> Shared

Unity Project --> Client (como DLL)

Tests ---------> Shared + Client + Server
```

**IMPORTANTE:**
- Shared NÃO referencia ninguém
- Client só referencia Shared
- Server só referencia Shared
- Unity **não compila** Client, só usa a DLL compilada

---

## 4. DETALHAMENTO DOS PROJETOS

### 4.1 XL4Net.Shared (.NET Standard 2.1)

**Responsabilidade:** Código compartilhado entre Client, Server e AuthServer

**Estrutura:**
```
XL4Net.Shared/
├── Protocol/
│   ├── Messages/
│   │   ├── INetworkMessage.cs
│   │   ├── Auth/
│   │   │   ├── LoginRequest.cs
│   │   │   ├── LoginResponse.cs
│   │   │   └── TokenValidationRequest.cs
│   │   ├── Game/
│   │   │   ├── PlayerMoveMessage.cs
│   │   │   ├── PlayerAttackMessage.cs
│   │   │   ├── EntitySpawnMessage.cs
│   │   │   └── EntityUpdateMessage.cs
│   │   └── System/
│   │       ├── PingMessage.cs
│   │       ├── PongMessage.cs
│   │       └── DisconnectMessage.cs
│   ├── Serialization/
│   │   ├── ISerializer.cs
│   │   └── MessagePackSerializer.cs
│   └── Enums/
│       ├── MessageType.cs
│       ├── DisconnectReason.cs
│       └── ChannelType.cs
├── Models/
│   ├── PlayerState.cs
│   ├── EntityState.cs
│   ├── TransformState.cs
│   └── InputData.cs
├── Constants/
│   └── NetworkConstants.cs
└── Pooling/
    ├── ObjectPool.cs
    ├── IPoolable.cs
    └── PooledObject.cs
```

**Pacotes NuGet:**
- MessagePack (serialização)

**IMPORTANTE:** Este projeto NÃO usa tipos Unity (Vector3, etc). Usamos tipos próprios:
```csharp
// XL4Net.Shared/Math/Vec3.cs
public struct Vec3
{
    public float X, Y, Z;
    
    public Vec3(float x, float y, float z)
    {
        X = x; Y = y; Z = z;
    }
    
    public float Magnitude => MathF.Sqrt(X*X + Y*Y + Z*Z);
    
    public static Vec3 operator +(Vec3 a, Vec3 b) 
        => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
}
```

Um projeto Unity separado (`XL4Net.Unity`) fará conversão:
```csharp
public static Vec3 ToVec3(this UnityEngine.Vector3 v) 
    => new Vec3(v.x, v.y, v.z);
```

---

### 4.2 XL4Net.Client (.NET Standard 2.1)

**Responsabilidade:** Lógica de networking do cliente (SEM código Unity)

**Estrutura:**
```
XL4Net.Client/
├── Core/
│   ├── NetworkClient.cs
│   ├── ConnectionManager.cs
│   └── ClientTick.cs
├── Prediction/
│   ├── ClientPrediction.cs
│   ├── StateBuffer.cs
│   ├── InputBuffer.cs
│   ├── ICommand.cs
│   └── Commands/
│       ├── MoveCommand.cs
│       └── AttackCommand.cs
├── Reconciliation/
│   └── ClientReconciliation.cs
├── Interpolation/
│   ├── EntityInterpolator.cs
│   └── TransformInterpolator.cs
├── Transport/
│   ├── ITransport.cs
│   ├── TcpClient.cs
│   ├── UdpClient.cs
│   ├── Packet.cs
│   ├── Channel.cs
│   └── ReliableUdp.cs
└── Events/
    └── NetworkEvents.cs
```

**Pacotes NuGet:**
- MessagePack
- System.Buffers (pooling)

**Importante:** Este projeto compila para DLL que o Unity referencia.

---

### 4.3 XL4Net.Server (.NET 9)

**Responsabilidade:** Servidor de jogo authoritative

**Estrutura:**
```
XL4Net.Server/
├── Core/
│   ├── GameServer.cs
│   ├── ClientConnection.cs
│   ├── ServerTick.cs
│   └── ServerConfig.cs
├── Simulation/
│   ├── ServerSimulation.cs
│   ├── MovementValidator.cs
│   ├── CombatSystem.cs
│   └── StateManager.cs
├── Reconciliation/
│   ├── ServerReconciliation.cs
│   └── StateHistory.cs
├── Broadcasting/
│   ├── InterestManager.cs        # AOI
│   ├── SpatialGrid.cs
│   └── MessageBroadcaster.cs
├── MessageHandlers/
│   ├── IMessageHandler.cs
│   ├── MessageHandlerRegistry.cs
│   ├── Movement/
│   │   ├── PlayerMoveHandler.cs
│   │   └── PlayerStopHandler.cs
│   ├── Combat/
│   │   ├── PlayerAttackHandler.cs
│   │   └── PlayerTakeDamageHandler.cs
│   └── Social/
│       └── ChatMessageHandler.cs
├── Transport/
│   ├── ITransport.cs
│   ├── TcpServer.cs
│   ├── UdpServer.cs
│   ├── Packet.cs
│   └── Channel.cs
├── States/
│   ├── IGameState.cs
│   ├── StateMachine.cs
│   ├── LobbyState.cs
│   ├── PlayingState.cs
│   └── GameOverState.cs
├── Pooling/
│   ├── PacketPool.cs
│   ├── MessagePool.cs
│   ├── BufferPool.cs
│   └── PoolMetrics.cs
└── Events/
    └── ServerEvents.cs
```

**Pacotes NuGet:**
- MessagePack
- Serilog (logging)
- System.Threading.Channels

---

### 4.4 XL4Net.AuthServer (.NET 9)

**Responsabilidade:** Autenticação e gerenciamento de contas

**Estrutura:**
```
XL4Net.AuthServer/
├── Core/
│   ├── AuthServer.cs
│   ├── AuthConfig.cs
│   └── Program.cs
├── Authentication/
│   ├── TokenManager.cs          # JWT
│   ├── PasswordHasher.cs        # BCrypt
│   └── RateLimiter.cs
├── Database/
│   ├── IAccountRepository.cs
│   ├── PostgresAccountRepository.cs
│   └── DbContext.cs
├── Models/
│   ├── Account.cs
│   ├── AuthToken.cs
│   └── LoginAttempt.cs
└── Endpoints/
    ├── LoginEndpoint.cs
    ├── RegisterEndpoint.cs
    └── ValidateTokenEndpoint.cs
```

**Pacotes NuGet:**
- Npgsql (PostgreSQL driver)
- Dapper (micro ORM)
- BCrypt.Net-Next (password hashing)
- System.IdentityModel.Tokens.Jwt (JWT)
- Serilog

---

## 5. NETWORK CONSTANTS

### 5.1 Portas Padrão

```csharp
public static class NetworkPorts
{
    // AuthServer
    public const ushort AUTH_TCP = 2106;
    
    // GameServer
    public const ushort GAME_TCP = 7777;
    public const ushort GAME_UDP = 7778;
}
```

### 5.2 Configurações

```csharp
public class ServerConfig
{
    public int TickRate { get; set; } = 30;           // Hz
    public int ClientSendRate { get; set; } = 30;     // Hz
    public int SnapshotRate { get; set; } = 30;       // Hz
    public int MaxPlayers { get; set; } = 100;        // Por servidor
    public ushort TcpPort { get; set; } = 7777;
    public ushort UdpPort { get; set; } = 7778;
    public float ViewDistance { get; set; } = 50f;    // AOI radius
}
```

---

## 6. DESIGN PATTERNS

### 6.1 Observer Pattern (Events)

**Uso:** Desacoplamento de sistemas

```csharp
public class NetworkClient
{
    public event Action<int> OnConnected;
    public event Action<DisconnectReason> OnDisconnected;
    public event Action<INetworkMessage> OnMessageReceived;
}
```

**Onde usar:**
- Network events (connect, disconnect, message received)
- Game events (player death, damage, level up)
- Server events (player joined, player left, tick)

---

### 6.2 Command Pattern (Input Buffer)

**Uso:** Client-side prediction e reconciliation

```csharp
public interface ICommand
{
    long Timestamp { get; }
    void Execute();
    INetworkMessage ToMessage();
}

public class MoveCommand : ICommand
{
    private Vector3 _direction;
    
    public void Execute()
    {
        // Client-side prediction
        player.Move(_direction);
    }
    
    public INetworkMessage ToMessage()
    {
        return new InputMessage { Direction = _direction };
    }
}
```

**Onde usar:**
- Player inputs
- Rollback/replay em reconciliation
- Input buffering

---

### 6.3 Strategy Pattern (Message Handlers)

**Uso:** Escalabilidade de tipos de mensagens

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
        // Processa movimento
    }
}
```

**Onde usar:**
- Server message processing
- Client message processing

---

### 6.4 State Pattern (Game States)

**Uso:** Organização de estados do servidor

```csharp
public interface IGameState
{
    void Enter();
    void Update(float deltaTime);
    void Exit();
}

public class PlayingState : IGameState
{
    public void Update(float dt)
    {
        // Simula gameplay
        _simulation.Update(dt);
        
        if (gameOver)
            _stateMachine.ChangeState(new GameOverState());
    }
}
```

**Onde usar:**
- Server states (Lobby → Playing → GameOver)
- Client states (Connecting → Connected → InGame)

---

## 7. TRANSPORT LAYER

### 7.1 Visão Geral

**Baseado em:** Fishnet Tugboat

**Arquivos de referência:**
```
FishNet/Runtime/Transporting/Transports/Tugboat/
├── Client/ClientSocket.cs
├── Server/ServerSocket.cs
└── Core/
    ├── CommonSocket.cs
    ├── Packet.cs
    └── Channel.cs
```

### 7.2 Channels

```csharp
public enum ChannelType
{
    Reliable,      // TCP-like no UDP (ack, resend, ordenado)
    Unreliable,    // UDP puro (fire and forget)
    Sequenced,     // Descarta pacotes velhos
}
```

**Uso:**
- **Reliable**: Chat, spawn/despawn, inventário
- **Unreliable**: Movimento (30Hz), animações
- **Sequenced**: Snapshot de estado

### 7.3 Packet Structure

```csharp
public class Packet : IPoolable
{
    public ushort Sequence { get; set; }       // Número sequencial
    public ushort Ack { get; set; }            // Último pacote recebido
    public uint AckBits { get; set; }          // Últimos 32 pacotes (bitfield)
    public ChannelType Channel { get; set; }
    public byte[] Payload { get; set; }        // Mensagem serializada
    
    public void Reset()
    {
        Sequence = 0;
        Ack = 0;
        AckBits = 0;
        Payload = null;
    }
}
```

**IMPORTANTE:** `Packet` é uma **class** (não struct) para funcionar com `ObjectPool<T>` que tem constraint `where T : class`.

### 7.4 Reliable UDP

**Features a implementar:**
- Acknowledgment system (ack/nack)
- Resend automático de pacotes perdidos
- Ordering de pacotes
- Fragmentação de mensagens grandes (>MTU ~1400 bytes)

**Algoritmo:**
```
1. Envia packet com sequence number
2. Adiciona a lista de "aguardando ack"
3. Se não recebe ack em 100ms, reenvia
4. Repete até 5 tentativas
5. Se falhar, desconecta
```

### 7.5 Connection Management

**Handshake Completo:**
```csharp
// Cliente envia
public class HandshakeRequest
{
    public uint MagicNumber { get; set; }      // 0x584C344E ("XL4N")
    public ushort ProtocolVersion { get; set; } // Ex: 1
    public string ClientVersion { get; set; }   // Ex: "1.0.5"
    public string Platform { get; set; }        // "PC", "Android", "iOS", "WebGL"
}

// Servidor responde
public class HandshakeResponse
{
    public bool Accepted { get; set; }
    public uint SessionId { get; set; }
    public string ServerVersion { get; set; }
    public string RejectReason { get; set; }    // Se Accepted = false
    public int ServerTick { get; set; }         // Pra time sync inicial
}
```

**Fluxo:**
```
Client                          Server
  |                                |
  |--- HandshakeRequest ---------->|
  |    (magic, version, platform)  |
  |                                |--- Valida magic number
  |                                |--- Verifica protocol version
  |                                |--- Verifica se server não está cheio
  |                                |
  |<-- HandshakeResponse ----------|
  |    (accepted, sessionId)       |
  |                                |
  Connected!                       Adiciona à lista de clientes
```

**Validação do servidor:**
```csharp
private HandshakeResponse ValidateHandshake(HandshakeRequest request)
{
    // 1. Magic number correto?
    if (request.MagicNumber != 0x584C344E)
        return new HandshakeResponse 
        { 
            Accepted = false, 
            RejectReason = "Invalid magic number" 
        };
    
    // 2. Protocol version compatível?
    if (request.ProtocolVersion != CURRENT_PROTOCOL_VERSION)
        return new HandshakeResponse
        {
            Accepted = false,
            RejectReason = $"Protocol mismatch. Server: {CURRENT_PROTOCOL_VERSION}, Client: {request.ProtocolVersion}"
        };
    
    // 3. Servidor cheio?
    if (_clients.Count >= _config.MaxPlayers)
        return new HandshakeResponse
        {
            Accepted = false,
            RejectReason = "Server full"
        };
    
    // 4. OK!
    var sessionId = GenerateSessionId();
    return new HandshakeResponse
    {
        Accepted = true,
        SessionId = sessionId,
        ServerVersion = SERVER_VERSION,
        ServerTick = CurrentTick
    };
}
```

**Heartbeat:**
- Client envia Ping a cada 1 segundo
- Server responde com Pong (inclui ServerTick pra time sync)
- Se não recebe Pong por 5 segundos, desconecta

```csharp
public class PingMessage
{
    public long ClientSendTime { get; set; }  // Timestamp em ms
}

public class PongMessage
{
    public long ClientSendTime { get; set; }  // Echo do ping
    public int ServerTick { get; set; }       // Tick atual do servidor
    public long ServerTime { get; set; }      // Tempo do servidor em ms
}
```

### 7.6 Plano B: LiteNetLib

**IMPORTANTE:** Implementar Reliable UDP robusto é **muito difícil**. Principais desafios:
- Congestion control (evitar Death Spiral)
- NAT traversal (furar firewalls)
- Packet reordering eficiente
- Fragmentação de mensagens grandes

**Estratégia de Fallback:**

Se após **3 semanas** de Fase 1 o transport custom não estiver estável:

1. **Usar LiteNetLib** como transport base:
```csharp
// Install-Package LiteNetLib
public class LiteNetTransport : ITransport
{
    private NetManager _netManager;
    
    public void Connect(string host, int port)
    {
        _netManager = new NetManager(this);
        _netManager.Start();
        _netManager.Connect(host, port, "XL4Net");
    }
}
```

2. **Manter interface ITransport:**
```csharp
public interface ITransport
{
    void Connect(string host, int port);
    void Send(byte[] data, ChannelType channel);
    event Action<byte[]> OnReceived;
}
```

3. **Resto do framework não muda** (Client, Server, Prediction, etc)

**Vantagem:** Isola o problema. Se transport falhar, troca só ele.

**Referências para implementar custom:**
- [Quake 3 Networking](https://fabiensanglard.net/quake3/network.php)
- [Valve Source Engine](https://developer.valvesoftware.com/wiki/Source_Multiplayer_Networking)
- [Gaffer on Games - Reliable UDP](https://gafferongames.com/post/reliable_ordered_messages/)

---

## 8. OBJECT POOLING

### 8.1 Importância

**Problema sem pooling:**
```
30 Hz tick × 50 players = 1500 mensagens/segundo
Cada mensagem aloca: Packet + Message + byte[]
= 4500 alocações/segundo
= GC roda a cada 2-3 segundos
= Lag spike de 10-50ms
```

**Solução:**
Object pooling elimina alocações durante gameplay.

### 8.2 O que poolear

| Tipo | Frequência | Impacto |
|------|-----------|---------|
| **Packets** | Milhares/s | ALTO |
| **Messages** | Milhares/s | ALTO |
| **byte[] buffers** | Contínuo | ALTO |
| **Commands** | 30-60/s/player | MÉDIO |
| **State snapshots** | 30/s/player | MÉDIO |

### 8.3 Implementação Base

```csharp
public class ObjectPool<T> where T : class, new()
{
    private readonly ConcurrentBag<T> _objects = new();
    private readonly int _maxSize;
    
    public ObjectPool(int initialSize = 32, int maxSize = 1024)
    {
        _maxSize = maxSize;
        
        // Warmup
        for (int i = 0; i < initialSize; i++)
        {
            _objects.Add(new T());
        }
    }
    
    public T Rent()
    {
        return _objects.TryTake(out var obj) ? obj : new T();
    }
    
    public void Return(T obj)
    {
        if (_objects.Count < _maxSize)
        {
            if (obj is IPoolable poolable)
                poolable.Reset();
            
            _objects.Add(obj);
        }
    }
}

public interface IPoolable
{
    void Reset();
}
```

### 8.4 Pools Específicos

```csharp
// PacketPool
public static class PacketPool
{
    private static readonly ObjectPool<Packet> _pool = new(128, 2048);
    
    public static Packet Rent() => _pool.Rent();
    public static void Return(Packet p) => _pool.Return(p);
}

// MessagePool
public static class MessagePool
{
    private static Dictionary<Type, object> _pools = new();
    
    public static T Rent<T>() where T : INetworkMessage, new()
    {
        // ...
    }
    
    public static void Return<T>(T msg) where T : INetworkMessage
    {
        // ...
    }
}

// BufferPool (byte arrays por tamanho)
public static class BufferPool
{
    private static ObjectPool<byte[]> _pool256 = new(64);
    private static ObjectPool<byte[]> _pool1024 = new(32);
    private static ObjectPool<byte[]> _pool4096 = new(16);
    
    public static byte[] Rent(int minSize)
    {
        if (minSize <= 256) return _pool256.Rent();
        if (minSize <= 1024) return _pool1024.Rent();
        if (minSize <= 4096) return _pool4096.Rent();
        return new byte[minSize]; // Fallback
    }
}
```

### 8.5 Pattern: Using Statement

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
    // Usa packet
} // Automaticamente retorna
```

### 8.6 Performance Esperada

**Sem Pooling:**
- GC Gen0: a cada 2-3 segundos
- GC pause: 10-50ms
- Alocações: ~100MB/segundo

**Com Pooling:**
- GC Gen0: a cada 20-30 segundos
- GC pause: <5ms
- Alocações: ~1MB/segundo (só startup)

---

## 9. CLIENT-SIDE PREDICTION

### 9.1 Fluxo

```
Frame 1: Player aperta W
  ↓
1. Cria MoveCommand
2. Execute() - Move player localmente (prediction)
3. Adiciona ao InputBuffer
4. Envia pro servidor com timestamp
  ↓
Servidor processa (latência 50ms)
  ↓
Frame 4: Cliente recebe confirmação do servidor
  ↓
5. Reconciliation - compara estado predito vs servidor
6. Se diferente, aplica correção e re-aplica inputs
```

### 9.2 Implementação

```csharp
public class ClientPrediction
{
    private List<ICommand> _inputBuffer = new();
    private List<PlayerState> _stateHistory = new();
    
    public void ProcessInput(ICommand command)
    {
        // 1. Executa localmente
        command.Execute();
        
        // 2. Guarda no buffer
        _inputBuffer.Add(command);
        
        // 3. Guarda estado após execução
        _stateHistory.Add(GetCurrentState());
        
        // 4. Envia pro servidor
        _networkClient.Send(command.ToMessage());
    }
    
    public void OnServerStateReceived(PlayerState serverState)
    {
        // 5. Encontra estado correspondente
        var predictedState = _stateHistory.Find(s => s.Tick == serverState.Tick);
        
        // 6. Compara
        if (!predictedState.Equals(serverState))
        {
            // 7. Servidor disse que estamos errados
            ApplyServerState(serverState);
            
            // 8. Re-aplica inputs após correção
            ReplayInputs(serverState.Tick);
        }
        
        // 9. Limpa histórico antigo
        CleanupOldStates(serverState.Tick);
    }
}
```

---

## 10. TIME SYNCHRONIZATION

### 10.1 O Problema

Cliente e servidor rodam em máquinas diferentes com relógios diferentes. Para prediction/reconciliation funcionar, precisamos sincronizar o tempo.

**Desafios:**
- Latência variável (50-200ms)
- Clock drift (relógios desalinham naturalmente)
- Jitter (variação de latência)

### 10.2 TimeManager

```csharp
public class TimeManager
{
    // Configuração
    public int TickRate { get; } = 30;
    public float TickInterval => 1f / TickRate; // 0.033s @ 30Hz
    
    // Servidor (authoritative)
    public int ServerTick { get; private set; }
    public long ServerTimeMs => ServerTick * (long)(TickInterval * 1000);
    
    // Cliente (estimado)
    public int LocalTick { get; private set; }
    public int TickOffset { get; private set; } // Diferença estimada
    public int EstimatedServerTick => LocalTick + TickOffset;
    
    // Latência
    public int RTT { get; private set; }        // Round Trip Time (ms)
    public int OneWayLatency => RTT / 2;
    public int Jitter { get; private set; }     // Variação de latência
    
    // Histórico de RTT (pra calcular jitter)
    private CircularBuffer<int> _rttHistory = new(60);
    
    public void Update(float deltaTime)
    {
        LocalTick++;
    }
    
    public void OnPingPong(long clientSendTime, long serverRecvTime, long serverSendTime)
    {
        var now = GetCurrentTimeMs();
        
        // 1. Calcula RTT
        var rtt = (int)(now - clientSendTime);
        
        // 2. Atualiza histórico
        _rttHistory.Add(rtt);
        
        // 3. RTT suavizado (média móvel)
        RTT = CalculateAverage(_rttHistory);
        
        // 4. Calcula jitter (variação)
        Jitter = CalculateStdDev(_rttHistory);
        
        // 5. Estima tick do servidor AGORA
        var oneWayLatency = rtt / 2;
        var serverTickWhenReceived = serverSendTime / (TickInterval * 1000);
        var ticksPassedSinceServer = oneWayLatency / (TickInterval * 1000);
        var estimatedCurrentServerTick = serverTickWhenReceived + ticksPassedSinceServer;
        
        // 6. Calcula drift (diferença entre estimativa e realidade)
        var currentEstimate = EstimatedServerTick;
        var drift = estimatedCurrentServerTick - currentEstimate;
        
        // 7. Corrige offset SUAVEMENTE (não pula)
        if (Math.Abs(drift) > 5) // Drift grande, corrige mais rápido
        {
            TickOffset += drift / 2;
        }
        else // Drift pequeno, corrige devagar
        {
            TickOffset += drift / 10;
        }
    }
    
    private int CalculateAverage(CircularBuffer<int> values)
    {
        int sum = 0;
        foreach (var v in values)
            sum += v;
        return sum / values.Count;
    }
    
    private int CalculateStdDev(CircularBuffer<int> values)
    {
        var avg = CalculateAverage(values);
        int sumSquaredDiff = 0;
        foreach (var v in values)
        {
            var diff = v - avg;
            sumSquaredDiff += diff * diff;
        }
        return (int)Math.Sqrt(sumSquaredDiff / values.Count);
    }
}
```

### 10.3 Fluxo de Sincronização

**No Connect:**
```
Client                          Server
  |                                |
  |--- Ping (t0) ----------------->|
  |                                |
  |<-- Pong (serverTick, t0) ------|
  |                                |
  RTT = now - t0
  Offset inicial = serverTick - localTick + RTT/2
```

**Durante o jogo (a cada 1 segundo):**
```
Client envia Ping com timestamp
Server responde Pong com seu tick atual
Client recalcula Offset suavemente
```

### 10.4 Uso na Prediction

```csharp
public class ClientPrediction
{
    private TimeManager _time;
    
    public void ProcessInput(ICommand command)
    {
        // 1. Timestamp do comando = tick do servidor estimado
        command.Timestamp = _time.EstimatedServerTick;
        
        // 2. Executa localmente
        command.Execute();
        
        // 3. Envia pro servidor
        Send(command);
    }
    
    public void OnServerState(PlayerState serverState)
    {
        // Servidor diz "no tick X, você estava em Y"
        // Encontra predição correspondente
        var predictedState = _stateHistory.Find(s => s.Tick == serverState.Tick);
        
        if (!predictedState.Equals(serverState))
        {
            // Corrige!
            Reconcile(serverState);
        }
    }
}
```

### 10.5 Interpolation Time

Para interpolar outras entidades (não o próprio player), renderizamos no **passado**:

```csharp
public float InterpolationTime => _time.EstimatedServerTick - InterpolationDelay;
public int InterpolationDelay => (_time.RTT / _time.TickInterval) + 2; // RTT + buffer

public void InterpolateOtherPlayers()
{
    var targetTick = InterpolationTime;
    
    foreach (var entity in _entities)
    {
        var from = entity.GetStateAtTick(targetTick);
        var to = entity.GetStateAtTick(targetTick + 1);
        var alpha = (targetTick % 1); // Fração entre ticks
        
        entity.VisualPosition = Vec3.Lerp(from.Position, to.Position, alpha);
    }
}
```

**Por quê no passado?**
- Garantimos que temos 2 snapshots pra interpolar
- Compensa jitter
- Movimento fica suave

---

## 11. SERVER RECONCILIATION

### 10.1 Server Authoritative

Servidor é **sempre** a fonte da verdade.

```csharp
public class ServerReconciliation
{
    private CircularBuffer<WorldState> _stateHistory = new(60);
    
    public void ProcessClientInput(ClientConnection client, InputMessage input)
    {
        // 1. Valida input
        if (!IsValidInput(client, input))
        {
            Log.Warning($"Invalid input from {client.Id}");
            return;
        }
        
        // 2. Aplica input no servidor
        ApplyInput(client, input);
        
        // 3. Guarda estado após aplicação
        _stateHistory.Add(GetWorldState());
        
        // 4. Envia confirmação pro cliente
        SendStateSnapshot(client);
    }
}
```

### 10.2 State History

Servidor guarda últimos 60 ticks (2 segundos @ 30Hz) para:
- Lag compensation (rewind pra hit detection)
- Debugging
- Replay

---

## 11. INTEREST MANAGEMENT (AOI)

### 11.1 Problema

Com 500 players:
- Enviar updates de TODOS pra TODOS = 500 × 500 = 250.000 msgs/tick
- @ 30Hz = 7.500.000 mensagens/segundo
- **IMPOSSÍVEL**

### 11.2 Solução: Area of Interest

Só envia o que o player **pode ver**.

```csharp
public class InterestManager
{
    private SpatialGrid _grid;
    private float _viewDistance = 50f;
    
    public List<int> GetPlayersInRange(int playerId)
    {
        var position = GetPlayerPosition(playerId);
        return _grid.Query(position, _viewDistance);
    }
    
    public void BroadcastMovement(int playerId, PlayerMoveMessage msg)
    {
        var nearbyPlayers = GetPlayersInRange(playerId);
        
        foreach (var targetId in nearbyPlayers)
        {
            SendTo(targetId, msg);
        }
    }
}
```

### 11.3 Spatial Hash Grid

Divide mundo em células para busca O(1):

```csharp
public class SpatialGrid
{
    private Dictionary<Vector2Int, List<int>> _cells = new();
    private float _cellSize = 50f;
    
    public void Insert(int playerId, Vector3 position)
    {
        var cell = WorldToCell(position);
        if (!_cells.ContainsKey(cell))
            _cells[cell] = new List<int>();
        
        _cells[cell].Add(playerId);
    }
    
    public List<int> Query(Vector3 center, float radius)
    {
        var result = new List<int>();
        var cellRadius = Mathf.CeilToInt(radius / _cellSize);
        var centerCell = WorldToCell(center);
        
        // Checa células vizinhas
        for (int x = -cellRadius; x <= cellRadius; x++)
        {
            for (int y = -cellRadius; y <= cellRadius; y++)
            {
                var cell = centerCell + new Vector2Int(x, y);
                if (_cells.TryGetValue(cell, out var players))
                {
                    result.AddRange(players);
                }
            }
        }
        
        return result;
    }
}
```

**Performance:**
- Sem AOI: O(N²) = 250.000 checks
- Com AOI: O(k) onde k = players próximos (~10-20)

---

## 12. OBSERVABILITY & METRICS

### 12.1 Importância

Sem métricas, você está voando cego. Precisa saber:
- Performance do servidor (CPU, memória, network)
- Comportamento dos jogadores
- Gargalos e problemas antes de virarem crises

### 12.2 ServerMetrics

```csharp
public class ServerMetrics
{
    // Conexões
    public int PlayersConnected { get; set; }
    public int PeakPlayers { get; set; }
    public int TotalConnectionsToday { get; set; }
    
    // Network
    public long MessagesInPerSecond { get; set; }
    public long MessagesOutPerSecond { get; set; }
    public long BytesInPerSecond { get; set; }
    public long BytesOutPerSecond { get; set; }
    public int AverageLatency { get; set; }  // ms
    
    // Performance
    public float TickDurationMs { get; set; }
    public float TickDurationAvg { get; set; }
    public float TickDurationMax { get; set; }
    public int TicksPerSecond { get; set; }
    
    // Memory
    public long ManagedMemoryMB { get; set; }
    public long TotalMemoryMB { get; set; }
    public int GCCollections { get; set; }
    
    // Pooling
    public int PacketPoolSize { get; set; }
    public int PacketPoolLeaks { get; set; }
    public int MessagePoolSize { get; set; }
    public int MessagePoolLeaks { get; set; }
    
    // Game-specific
    public int EntitiesSpawned { get; set; }
    public int AOICalculationsPerTick { get; set; }
}
```

### 12.3 Logging Estruturado (Serilog)

```csharp
// Setup
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
    )
    .WriteTo.File(
        "logs/server-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj}{NewLine}{Exception}"
    )
    .Enrich.WithProperty("ServerName", "GameServer-01")
    .CreateLogger();

// Uso
Log.Information("Server started on port {Port}", config.Port);
Log.Warning("High latency detected for player {PlayerId}: {Latency}ms", playerId, latency);
Log.Error(ex, "Failed to process message from {PlayerId}", playerId);

// Métricas estruturadas
Log.Information("Metrics: {@Metrics}", serverMetrics);
```

### 12.4 Painel de Métricas (Console)

```csharp
public class MetricsDisplay
{
    private ServerMetrics _metrics;
    
    public void Display()
    {
        Console.Clear();
        Console.WriteLine("╔════════════════════════════════════════════╗");
        Console.WriteLine("║        XL4Net Server Metrics               ║");
        Console.WriteLine("╠════════════════════════════════════════════╣");
        Console.WriteLine($"║ Players:     {_metrics.PlayersConnected,4} / {_config.MaxPlayers,-4} (Peak: {_metrics.PeakPlayers})");
        Console.WriteLine($"║ Tick:        {_metrics.TicksPerSecond,4} Hz  Avg: {_metrics.TickDurationAvg,5:F2}ms");
        Console.WriteLine($"║ Network In:  {FormatBytes(_metrics.BytesInPerSecond)}/s  {_metrics.MessagesInPerSecond} msg/s");
        Console.WriteLine($"║ Network Out: {FormatBytes(_metrics.BytesOutPerSecond)}/s  {_metrics.MessagesOutPerSecond} msg/s");
        Console.WriteLine($"║ Memory:      {_metrics.ManagedMemoryMB} MB managed, {_metrics.TotalMemoryMB} MB total");
        Console.WriteLine($"║ Pool Leaks:  Packets: {_metrics.PacketPoolLeaks}, Messages: {_metrics.MessagePoolLeaks}");
        Console.WriteLine("╚════════════════════════════════════════════╝");
    }
    
    private string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
}
```

### 12.5 Admin Commands

```csharp
public class AdminConsole
{
    public void ProcessCommand(string command)
    {
        var parts = command.Split(' ');
        
        switch (parts[0].ToLower())
        {
            case "stats":
                DisplayMetrics();
                break;
            
            case "players":
                ListPlayers();
                break;
            
            case "kick":
                if (parts.Length > 1 && int.TryParse(parts[1], out var id))
                    KickPlayer(id);
                break;
            
            case "broadcast":
                var message = string.Join(" ", parts.Skip(1));
                BroadcastMessage(message);
                break;
            
            case "shutdown":
                GracefulShutdown();
                break;
            
            default:
                Console.WriteLine("Unknown command. Type 'help' for commands.");
                break;
        }
    }
}
```

### 12.6 Alertas Automáticos

```csharp
public class AlertSystem
{
    public void CheckThresholds(ServerMetrics metrics)
    {
        // Tick duration muito alta
        if (metrics.TickDurationAvg > 40) // Target: 33ms @ 30Hz
        {
            Log.Warning("⚠️ High tick duration: {Duration}ms (target: 33ms)", 
                metrics.TickDurationAvg);
        }
        
        // Pool leaks
        if (metrics.PacketPoolLeaks > 100)
        {
            Log.Error("🚨 MEMORY LEAK: {Leaks} packets not returned to pool!", 
                metrics.PacketPoolLeaks);
        }
        
        // Latência alta
        if (metrics.AverageLatency > 200)
        {
            Log.Warning("⚠️ High average latency: {Latency}ms", 
                metrics.AverageLatency);
        }
        
        // Memória alta
        if (metrics.TotalMemoryMB > 2048) // 2GB
        {
            Log.Warning("⚠️ High memory usage: {Memory}MB", 
                metrics.TotalMemoryMB);
        }
    }
}
```

### 12.7 Exportação de Métricas (Opcional - Fase 8)

Para sistemas profissionais, considere:

**Prometheus + Grafana:**
```csharp
// Expor métricas em /metrics
app.MapGet("/metrics", () => 
{
    var metrics = _server.GetMetrics();
    return Results.Text(
        $"xl4net_players_connected {metrics.PlayersConnected}\n" +
        $"xl4net_tick_duration_ms {metrics.TickDurationAvg}\n" +
        $"xl4net_bytes_in_per_sec {metrics.BytesInPerSecond}\n" +
        // ...
    );
});
```

---

## 13. AUTHSERVER

### 12.1 Fluxo de Autenticação

```
Player
  ↓
1. POST /auth/login { username, password }
  ↓
AuthServer:
  ↓
2. Valida credenciais (BCrypt hash)
  ↓
3. Consulta PostgreSQL
  ↓
4. Gera JWT token (expira em 1h)
  ↓
5. Retorna { token, gameServerIP, gameServerPort }
  ↓
Player conecta no GameServer
  ↓
6. Envia token no handshake
  ↓
GameServer valida token (JWT signature)
  ↓
7. Aceita conexão
```

### 12.2 Database Schema

```sql
-- Extensão para UUIDs
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

CREATE TABLE accounts (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    username VARCHAR(50) UNIQUE NOT NULL,
    email VARCHAR(255) UNIQUE NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    metadata JSONB DEFAULT '{}',
    created_at TIMESTAMP DEFAULT NOW(),
    last_login TIMESTAMP
);

CREATE INDEX idx_username ON accounts(username);
CREATE INDEX idx_email ON accounts(email);
CREATE INDEX idx_metadata ON accounts USING GIN(metadata);

CREATE TABLE login_attempts (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id UUID REFERENCES accounts(id),
    ip_address INET NOT NULL,
    username VARCHAR(50),
    success BOOLEAN NOT NULL,
    attempted_at TIMESTAMP DEFAULT NOW()
);

CREATE INDEX idx_login_attempts_ip ON login_attempts(ip_address, attempted_at);
CREATE INDEX idx_login_attempts_account ON login_attempts(account_id, attempted_at);
```

**Por que UUID em vez de SERIAL?**
- **Segurança:** Não expõe quantos usuários existem
- **Distribuição:** Funciona em sharding/multi-database
- **Merge-friendly:** Fácil juntar dados de diferentes sources
- **Performance:** UUID v4 com pgcrypto é rápido

### 12.3 Security

- **Passwords:** BCrypt (cost 12)
- **JWT:** HS256 ou RS256, secret key em env variable
- **Rate Limiting:** 5 tentativas/minuto por IP
- **HTTPS:** Obrigatório em produção
- **SQL Injection:** Queries parametrizadas (Dapper)

---

## 14. CONTAINERIZATION (DOCKER)

### 14.1 Por Que Docker?

**Benefícios:**
- ✅ Ambiente idêntico em dev/staging/prod
- ✅ PostgreSQL configurado automaticamente
- ✅ Fácil replicar setup
- ✅ CI/CD simplificado
- ✅ Isolamento de serviços

### 14.2 Estrutura de Containers

```
docker-compose.yml:
├── postgres          (Database)
├── authserver        (AuthServer .NET 9)
├── gameserver        (GameServer .NET 9)
└── adminer           (DB Admin UI - opcional)
```

### 14.3 docker-compose.yml

```yaml
version: '3.8'

services:
  postgres:
    image: postgres:16
    container_name: xl4net-db
    environment:
      POSTGRES_DB: xl4net
      POSTGRES_USER: xl4admin
      POSTGRES_PASSWORD: ${DB_PASSWORD:-changeme}
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data
      - ./sql/init.sql:/docker-entrypoint-initdb.d/init.sql
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U xl4admin"]
      interval: 10s
      timeout: 5s
      retries: 5

  authserver:
    build:
      context: .
      dockerfile: src/XL4Net.AuthServer/Dockerfile
    container_name: xl4net-auth
    environment:
      - DATABASE_URL=Host=postgres;Database=xl4net;Username=xl4admin;Password=${DB_PASSWORD:-changeme}
      - JWT_SECRET=${JWT_SECRET:-your-secret-key-here}
    ports:
      - "2106:2106"
    depends_on:
      postgres:
        condition: service_healthy
    restart: unless-stopped

  gameserver:
    build:
      context: .
      dockerfile: src/XL4Net.Server/Dockerfile
    container_name: xl4net-game
    environment:
      - AUTHSERVER_URL=http://authserver:2106
    ports:
      - "7777:7777"  # TCP
      - "7778:7778/udp"  # UDP
    depends_on:
      - authserver
    restart: unless-stopped

  # Opcional: Admin UI pro PostgreSQL
  adminer:
    image: adminer:latest
    container_name: xl4net-adminer
    ports:
      - "8080:8080"
    depends_on:
      - postgres

volumes:
  postgres_data:
```

### 14.4 Dockerfile (AuthServer)

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copia csproj e restaura dependências
COPY ["src/XL4Net.Shared/XL4Net.Shared.csproj", "XL4Net.Shared/"]
COPY ["src/XL4Net.AuthServer/XL4Net.AuthServer.csproj", "XL4Net.AuthServer/"]
RUN dotnet restore "XL4Net.AuthServer/XL4Net.AuthServer.csproj"

# Copia código e compila
COPY src/ .
RUN dotnet build "XL4Net.AuthServer/XL4Net.AuthServer.csproj" -c Release -o /app/build
RUN dotnet publish "XL4Net.AuthServer/XL4Net.AuthServer.csproj" -c Release -o /app/publish

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 2106
ENTRYPOINT ["dotnet", "XL4Net.AuthServer.dll"]
```

### 14.5 Comandos Úteis

```bash
# Iniciar todos os serviços
docker-compose up -d

# Ver logs
docker-compose logs -f gameserver

# Parar tudo
docker-compose down

# Rebuild após mudanças
docker-compose up -d --build

# Limpar volumes (CUIDADO: apaga database!)
docker-compose down -v
```

### 14.6 .env File

```bash
# .env (NÃO commitar no Git!)
DB_PASSWORD=super_secret_password_123
JWT_SECRET=your-jwt-secret-key-min-32-chars
```

### 14.7 Development vs Production

**Development:**
```yaml
# docker-compose.dev.yml
services:
  gameserver:
    volumes:
      - ./src:/src  # Hot reload
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
```

**Production:**
```yaml
# docker-compose.prod.yml
services:
  gameserver:
    restart: always
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
    deploy:
      resources:
        limits:
          cpus: '2.0'
          memory: 2G
```

---

## 15. THREADING MODEL

### 15.1 Filosofia

**Regra de Ouro:** Simulação de jogo = Single-threaded. I/O = Multi-threaded.

**Por quê?**
- Evita locks/races/deadlocks
- Código mais simples
- Performance previsível
- Fácil debugar

### 15.2 Arquitetura

```
┌─────────────────────────────────────────┐
│          GameServer Process             │
├─────────────────────────────────────────┤
│                                         │
│  Main Thread (Game Loop)                │
│  ├─ Tick (30 Hz)                        │
│  ├─ Process Inputs                      │
│  ├─ Simulate Physics/Gameplay           │
│  └─ Broadcast States                    │
│                                         │
│  I/O Thread Pool (Async)                │
│  ├─ Accept Connections                  │
│  ├─ Receive Messages                    │
│  ├─ Send Messages                       │
│  └─ Database Queries                    │
│                                         │
└─────────────────────────────────────────┘
```

### 15.3 Implementação

```csharp
public class GameServer
{
    private Channel<INetworkMessage> _incomingMessages;
    private CancellationTokenSource _cts;
    
    public async Task RunAsync()
    {
        _cts = new CancellationTokenSource();
        
        // 1. Inicia I/O threads
        _ = Task.Run(() => AcceptConnectionsAsync(_cts.Token));
        _ = Task.Run(() => ReceiveMessagesAsync(_cts.Token));
        
        // 2. Game loop (main thread)
        await GameLoopAsync(_cts.Token);
    }
    
    // Main Thread - Single-threaded game logic
    private async Task GameLoopAsync(CancellationToken ct)
    {
        var tickInterval = TimeSpan.FromSeconds(1.0 / 30); // 30 Hz
        
        while (!ct.IsCancellationRequested)
        {
            var tickStart = DateTime.UtcNow;
            
            // 1. Processa mensagens recebidas
            while (_incomingMessages.Reader.TryRead(out var message))
            {
                ProcessMessage(message);
            }
            
            // 2. Simula gameplay
            Simulate();
            
            // 3. Broadcast states
            BroadcastStates();
            
            // 4. Sleep até próximo tick
            var elapsed = DateTime.UtcNow - tickStart;
            var remaining = tickInterval - elapsed;
            if (remaining > TimeSpan.Zero)
            {
                await Task.Delay(remaining, ct);
            }
        }
    }
    
    // I/O Thread - Recebe mensagens e adiciona na fila
    private async Task ReceiveMessagesAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var message = await ReceiveFromNetworkAsync(ct);
            
            // Enfileira pra main thread processar
            await _incomingMessages.Writer.WriteAsync(message, ct);
        }
    }
    
    // Main Thread - Processa mensagem (sem locks!)
    private void ProcessMessage(INetworkMessage message)
    {
        var handler = _handlerRegistry.GetHandler(message.Type);
        handler.Handle(connection, message);
    }
}
```

### 15.4 Regras

**✅ PODE fazer em I/O thread:**
- Accept connections
- Read/write sockets
- Database queries (async)
- Serialização/desserialização

**❌ NUNCA fazer em I/O thread:**
- Modificar estado do jogo
- Acessar posição de players
- Spawn/despawn entidades
- Calcular colisões

**✅ Comunicação I/O → Main:**
- `System.Threading.Channels` (lock-free queue)
- Enfileira mensagem, main thread processa

**❌ Não usar:**
- `lock { }` na game logic
- `ConcurrentDictionary` pra estado de jogo
- Tasks paralelas pra simulação

### 15.5 Exceção: Broadcast Paralelo (Opcional)

Se tiver MUITOS players (500+), broadcasting pode ser paralelizado:

```csharp
private void BroadcastStates()
{
    var messages = PrepareMessages(); // Main thread
    
    // Envia em paralelo (I/O bound)
    Parallel.ForEach(messages, msg => 
    {
        SendAsync(msg); // Thread-safe socket write
    });
}
```

Mas a **preparação** das mensagens fica na main thread.

---

## 16. ESCALABILIDADE

### 13.1 Por Escala

| Players | Arquitetura | Mudanças no Código |
|---------|-------------|-------------------|
| **10-50** | 1 Auth + 1 Game | Nenhuma |
| **50-500** | 1 Auth + 1 Game + AOI | Adiciona InterestManager |
| **500-2000** | LB Auth + N GameServers | Adiciona MasterServer |
| **2000-5000+** | Cluster Auth + Cluster Game | Database sharding |

**IMPORTANTE:** O código de prediction/reconciliation/transport **NÃO MUDA**.

### 13.2 Arquitetura Multi-Server

```
                Internet
                   |
              Load Balancer
                   |
    ┌──────────────┼──────────────┐
    |              |              |
AuthServer    AuthServer    AuthServer
  (2106)        (2106)        (2106)
    |              |              |
    └──────────────┴──────────────┘
                   |
             PostgreSQL
           (Primary + Replicas)
                   |
            MasterServer
          (Server Registry)
                   |
    ┌──────────────┼──────────────┐
    |              |              |
GameServer1   GameServer2   GameServer3
(7777/7778)   (7777/7778)   (7777/7778)
50 players    50 players    50 players
```

### 13.3 MasterServer (Fase 7)

```csharp
public class MasterServer
{
    private List<GameServerInfo> _servers = new();
    
    public GameServerInfo GetAvailableServer()
    {
        // Load balancing - retorna server com menos players
        return _servers
            .Where(s => s.IsHealthy && s.PlayerCount < s.MaxPlayers)
            .OrderBy(s => s.PlayerCount)
            .FirstOrDefault();
    }
    
    public void RegisterServer(GameServerInfo info)
    {
        _servers.Add(info);
    }
    
    public void HealthCheck()
    {
        foreach (var server in _servers)
        {
            if (!Ping(server))
            {
                server.IsHealthy = false;
                Log.Warning($"Server {server.Address} is down");
            }
        }
    }
}
```

---

## 14. ROADMAP DE IMPLEMENTAÇÃO

### Timeline Estimado: 6-7 meses (part-time)

| Fase | Duração | Descrição |
|------|---------|-----------|
| **1. Transport** | 2-3 sem | TCP/UDP, Reliable UDP, Pooling |
| **2. AuthServer** | 2 sem | Login, JWT, PostgreSQL |
| **3. GameServer** | 2-3 sem | Tick, Handlers, Broadcasting |
| **4. Prediction** | 3-4 sem | Command pattern, Input buffer |
| **5. Reconciliation** | 3-4 sem | Server validation, Rollback |
| **6. AOI** | 2-3 sem | Spatial grid, Interest management |
| **7. Multi-Server** | 2 sem | MasterServer, Load balancing |
| **8. Optimization** | 3-4 sem | Lag comp, Metrics, Polish |
| **9. Documentation** | 1-2 sem | Docs, Examples, Tutorials |

**Total:** ~19-27 semanas

### Milestones

✅ **M1 (Fase 3):** Login + GameServer básico funcionando  
✅ **M2 (Fase 5):** Prediction + Reconciliation = movimento suave  
✅ **M3 (Fase 6):** 500+ players simultâneos  
✅ **M4 (Fase 9):** Framework completo + documentado  

---

## 15. REFERÊNCIAS

### 15.1 Código de Estudo

**Fishnet:**
- Transport: `FishNet/Runtime/Transporting/Transports/Tugboat/`
- Prediction: `FishNet/Runtime/Object/Prediction/PredictedObject.cs`
- TimeManager: `FishNet/Runtime/Managing/Timing/TimeManager.cs`

**Mirror:**
- NetworkTransform: Mais didático que Fishnet

### 15.2 Artigos

- [Valve - Source Multiplayer Networking](https://developer.valvesoftware.com/wiki/Source_Multiplayer_Networking)
- [Gabriel Gambetta - Fast-Paced Multiplayer](https://www.gabrielgambetta.com/client-server-game-architecture.html)
- [Glenn Fiedler - Networking for Game Programmers](https://gafferongames.com/)

### 15.3 Papers

- [Quake III Network Model](https://fabiensanglard.net/quake3/network.php)
- [Unreal Engine Networking](https://docs.unrealengine.com/5.0/en-US/networking-overview-for-unreal-engine/)

---

## 16. GLOSSÁRIO

| Termo | Definição |
|-------|-----------|
| **AOI** | Area of Interest - região visível do player |
| **Client-side Prediction** | Cliente executa ação antes de confirmação do servidor |
| **Reconciliation** | Correção quando predição do cliente está errada |
| **Server Authoritative** | Servidor é a fonte da verdade |
| **Tick** | Iteração de simulação do servidor (ex: 30 Hz = 30 ticks/seg) |
| **Snapshot** | Estado do mundo em um momento específico |
| **Lag Compensation** | Rewind do servidor pra compensar latência |
| **Pooling** | Reutilização de objetos pra evitar GC |
| **MTU** | Maximum Transmission Unit (~1400 bytes UDP) |
| **RTT** | Round-Trip Time (latência ida e volta) |

---

**FIM DO DOCUMENTO**

Versão 1.0 - 2024-11-20
