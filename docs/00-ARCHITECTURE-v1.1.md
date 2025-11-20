# XL4Net - Game Networking Framework
## Documento de Arquitetura

**Versão:** 1.1 (Atualizado com decisões da Fase 1)  
**Data:** 2024-11-20  
**Autor:** XL4Y3R  

---

## 📋 CHANGELOG v1.1

**Atualizado:** 2024-11-20 (Após conclusão da Fase 1)

**Mudanças principais:**
- ✅ **Transport:** Mudou de "Custom TCP/UDP" para **LiteNetLib wrapper**
- ✅ Adicionada seção 7.7 documentando a decisão do LiteNetLib
- ✅ Atualizada seção de Transport Layer com arquitetura implementada
- ✅ Removida seção 7.6 "Plano B" (LiteNetLib agora é Plano A)
- ✅ Adicionadas informações sobre wrappers (LiteNetTransport, LiteNetServerTransport)

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
- **Pragmatismo**: Usa bibliotecas battle-tested quando faz sentido

### 1.3 Inspiração

**Código base:** [Fishnet Networking](https://github.com/FirstGearGames/FishNet)
- Transport layer (Tugboat → **LiteNetLib**)
- Prediction/Reconciliation
- Interest Management

**Decisão Crítica (2024-11-20):** Assim como Fishnet, usamos **LiteNetLib** para transport layer em vez de implementação própria. Economiza 3-4 semanas e fornece solução battle-tested.

---

## 2. STACK TECNOLÓGICA

| Componente | Tecnologia | Versão | Justificativa |
|------------|-----------|--------|---------------|
| **Shared** | .NET Standard | 2.1 | Compatibilidade Unity + .NET 9 |
| **Client** | .NET Standard | 2.1 | Unity 6.2+ |
| **Server** | .NET | 9 | Performance moderna |
| **AuthServer** | .NET | 9 | Performance + async |
| **Serialização Packet** | Manual (14 bytes) | - | Controle total, header compacto |
| **Serialização Payload** | MessagePack | 2.5.140 | Performance + API moderna |
| **Database** | PostgreSQL | 16+ | Concorrência + JSONB |
| **Transport** | **LiteNetLib wrapper** | **1.3.1** | **Battle-tested, usado pelo Fishnet** |
| **Unity** | Unity | 6.2+ | LTS mais recente |

### 2.1 Decisões Técnicas

| Decisão | Escolha | Alternativas | Motivo |
|---------|---------|--------------|--------|
| **Serialização Packet** | Manual (14 bytes header) | MessagePack completo | Controle total, compacto |
| **Serialização Payload** | MessagePack | ProtoBuf, JSON | Performance + flexibilidade |
| **Database** | PostgreSQL | MySQL, SQLite | Melhor concorrência, JSONB |
| **Transport** | **LiteNetLib wrapper** | **Implementação própria, Mirror** | **Produtividade + qualidade** |
| **Patterns** | Observer+Command+Strategy+State | - | Escalabilidade e manutenibilidade |

---

## 3. ARQUITETURA DE PROJETOS

### 3.1 Estrutura da Solution

```
XL4Net/
│
├── src/
│   ├── XL4Net.Shared/              # .NET Standard 2.1
│   │   └── Transport/
│   │       ├── ITransport.cs       # Interface genérica
│   │       ├── Packet.cs           # Packet com serialização customizada
│   │       └── PacketPool.cs       # Object pooling
│   │
│   ├── XL4Net.Client/              # .NET Standard 2.1
│   │   └── Transport/
│   │       └── LiteNetTransport.cs # Wrapper do LiteNetLib (cliente)
│   │
│   ├── XL4Net.Server/              # .NET 9
│   │   └── Transport/
│   │       └── LiteNetServerTransport.cs # Wrapper do LiteNetLib (servidor)
│   │
│   └── XL4Net.AuthServer/          # .NET 9
│
├── tests/
│   ├── ServerTest/                 # Console app de teste
│   ├── ClientTest/                 # Console app de teste
│   ├── XL4Net.Tests/               # Unit tests
│   └── XL4Net.IntegrationTests/    # Integration tests
│
├── examples/
│   ├── SimpleGame.Client/          # Unity Project
│   └── SimpleGame.Server/          # .NET 9 Console
│
└── docs/
    ├── 00-ARCHITECTURE.md          # Este documento
    ├── 01-CODING-STANDARDS.md
    ├── 02-PROJECT-STATE.md
    └── 03-WORKFLOW.md
```

### 3.2 Dependências entre Projetos

```
AuthServer -----> Shared
GameServer -----> Shared
Client ---------> Shared (+ LiteNetLib)

Unity Project --> Client (como DLL)

Tests ---------> Shared + Client + Server
```

**IMPORTANTE:**
- Shared NÃO referencia ninguém
- Client só referencia Shared + LiteNetLib
- Server só referencia Shared + LiteNetLib
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
│       ├── ChannelType.cs
│       └── PacketType.cs
├── Models/
│   ├── PlayerState.cs
│   ├── EntityState.cs
│   ├── TransformState.cs
│   └── InputData.cs
├── Transport/
│   ├── ITransport.cs           # Interface genérica
│   ├── Packet.cs               # Com serialização customizada
│   └── PacketPool.cs           # Object pooling
├── Constants/
│   └── NetworkConstants.cs
└── Pooling/
    ├── ObjectPool.cs
    ├── IPoolable.cs
    └── PooledObject.cs
```

**Pacotes NuGet:**
- MessagePack (serialização de mensagens)

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
│   └── LiteNetTransport.cs      # ✅ IMPLEMENTADO (wrapper do LiteNetLib)
└── Events/
    └── NetworkEvents.cs
```

**Pacotes NuGet:**
- MessagePack
- LiteNetLib (1.3.1) ← **NOVO!**

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
│   └── LiteNetServerTransport.cs  # ✅ IMPLEMENTADO (wrapper do LiteNetLib)
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
- LiteNetLib (1.3.1) ← **NOVO!**
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
    public const ushort GAME_TCP = 7777;  // LiteNetLib usa UDP, mas pode ter TCP fallback
    public const ushort GAME_UDP = 7778;  // Porta principal
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
    public ushort Port { get; set; } = 7777;
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

**Implementação:** Wrappers sobre **LiteNetLib** (v1.3.1)

**Por que LiteNetLib?**
- ✅ Battle-tested (usado pelo Fishnet)
- ✅ 22.484 linhas de código maduro
- ✅ Reliable UDP já implementado (ACK/resend)
- ✅ Economiza 3-4 semanas de desenvolvimento
- ✅ Licença MIT (100% livre para uso comercial)

**Trade-offs:**
- ❌ Dependência externa
- ✅ Mas isolado via interface `ITransport` (pode trocar depois)

### 7.2 Arquitetura dos Wrappers

```
Seu código XL4Net
      ↓
ITransport (interface genérica)
      ↓
LiteNetTransport / LiteNetServerTransport (wrappers)
      ↓
LiteNetLib (NetManager, NetPeer)
      ↓
UDP Socket (Sistema Operacional)
```

**Vantagem:** Se precisar trocar LiteNetLib por outra biblioteca, só troca o wrapper. O resto do XL4Net não sabe a diferença!

### 7.3 ITransport Interface

```csharp
public interface ITransport
{
    // Propriedades
    bool IsConnected { get; }
    int Latency { get; }
    
    // Eventos
    event Action OnConnected;
    event Action<string> OnDisconnected;
    event Action<Packet> OnPacketReceived;
    event Action<string> OnError;
    
    // Métodos
    Task<bool> StartAsync();
    Task StopAsync();
    Task<bool> SendAsync(Packet packet);
    void ProcessIncoming(); // ← Game loop chama
}
```

**IMPORTANTE:** `ProcessIncoming()` é chamado no main thread (game loop). LiteNetLib callbacks rodam em background threads, mas mensagens são enfileiradas (ConcurrentQueue) e processadas no main thread.

### 7.4 LiteNetTransport (Cliente)

```csharp
// XL4Net.Client/Transport/LiteNetTransport.cs
public class LiteNetTransport : ITransport
{
    private NetManager _netManager;           // LiteNetLib
    private NetPeer _serverPeer;              // Conexão com servidor
    private ConcurrentQueue<Message> _queue;  // Fila thread-safe
    
    public async Task<bool> ConnectAsync(string host, int port)
    {
        // Resolve DNS (força IPv4)
        var ipAddress = ResolveIPv4(host);
        
        // Conecta usando LiteNetLib
        var writer = new NetDataWriter();
        writer.Put("XL4Net_v1.0"); // Connection key
        
        _serverPeer = _netManager.Connect(ipAddress, port, writer);
        
        // Aguarda conexão (timeout 5s)
        return await WaitForConnection();
    }
    
    public void ProcessIncoming()
    {
        // Processa até 100 mensagens por frame
        while (_queue.TryDequeue(out var message))
        {
            OnPacketReceived?.Invoke(message.Packet);
        }
    }
}
```

**Threading:**
```
LiteNetLib Thread (background)    Main Thread (game loop)
        |                                 |
    Receive UDP                           |
        |                                 |
    Enqueue(message)                      |
        |                                 |
        | ------------------------------> ProcessIncoming()
        |                                 |
        |                            Dispatch events
```

### 7.5 LiteNetServerTransport (Servidor)

```csharp
// XL4Net.Server/Transport/LiteNetServerTransport.cs
public class LiteNetServerTransport
{
    private NetManager _netManager;
    private ConcurrentDictionary<int, ClientConnection> _clients;
    private ConcurrentQueue<Message> _queue;
    
    public async Task<bool> StartAsync()
    {
        // Inicia NetManager (bind na porta)
        _netManager.Start(port: 7777);
        
        // Loop de update em background
        _ = Task.Run(() => UpdateLoop());
        
        return true;
    }
    
    public async Task BroadcastAsync(Packet packet)
    {
        // Serializa UMA vez (otimização)
        var data = packet.Serialize();
        
        // Envia para todos os clientes
        foreach (var client in _clients.Values)
        {
            client.Peer.Send(data, DeliveryMethod.ReliableOrdered);
        }
        
        // Retorna ao pool
        PacketPool.Return(packet);
    }
}
```

### 7.6 Channels do LiteNetLib

LiteNetLib fornece 3 tipos de canais:

```csharp
public enum ChannelType
{
    Reliable,      // TCP-like (ACK, resend, ordenado)
    Unreliable,    // UDP puro (fire and forget)
    Sequenced,     // Descarta pacotes velhos
}
```

**Mapeamento:**
```csharp
private DeliveryMethod GetDeliveryMethod(ChannelType channel)
{
    return channel switch
    {
        ChannelType.Reliable => DeliveryMethod.ReliableOrdered,
        ChannelType.Unreliable => DeliveryMethod.Unreliable,
        ChannelType.Sequenced => DeliveryMethod.Sequenced,
        _ => DeliveryMethod.ReliableOrdered
    };
}
```

**Uso:**
- **Reliable**: Chat, spawn/despawn, inventário
- **Unreliable**: Movimento (30Hz), animações
- **Sequenced**: Snapshot de estado

### 7.7 Decisão: Por que LiteNetLib?

**Data:** 2024-11-20  
**Contexto:** Fase 1 - Transport Layer  

**Problema:** Implementar Reliable UDP do zero é muito complexo:
- ACK/NACK system
- Resend automático
- Congestion control
- NAT traversal
- Packet reordering
- Fragmentação
- MTU discovery

**Estimativa:** 3-4 semanas para implementação básica + bugs escondidos

**Alternativas consideradas:**

| Opção | Prós | Contras | Tempo |
|-------|------|---------|-------|
| **Implementar próprio** | Controle total, aprendizado | Muitos bugs, complexo | 3-4 semanas |
| **Copiar do LiteNetLib** | Sem dependência externa | ~5000 linhas, manutenção sua | 2-3 semanas |
| **Usar LiteNetLib** | Battle-tested, mantido | Dependência externa | 3-5 dias |

**Decisão:** Usar LiteNetLib via wrappers

**Motivos:**
1. ✅ **Produtividade:** Economiza 3-4 semanas
2. ✅ **Qualidade:** 22.484 linhas testadas em produção
3. ✅ **Comprovado:** Fishnet usa e funciona
4. ✅ **Isolado:** Interface `ITransport` permite trocar depois
5. ✅ **Licença:** MIT = 100% livre

**Resultado:** Implementado em 2 dias, funcionando 100%! ✅

**Lições aprendidas:**
- Pragmatismo > Purismo
- Use bibliotecas battle-tested quando possível
- Isole dependências com interfaces
- Foque no que diferencia seu projeto (prediction, reconciliation, AOI)

---

## 8. PACKET STRUCTURE

### 8.1 Serialização Manual (Header)

```csharp
public class Packet : IPoolable
{
    public ushort Sequence { get; set; }    // 2 bytes
    public ushort Ack { get; set; }         // 2 bytes
    public uint AckBits { get; set; }       // 4 bytes
    public ChannelType Channel { get; set; } // 1 byte
    public byte Type { get; set; }          // 1 byte
    public byte[] Payload { get; set; }     // N bytes
    public int PayloadSize { get; set; }    // 4 bytes (não serializado, só controle)
    
    // Total header: 14 bytes
}
```

**Por que serialização manual do Packet?**
- ✅ Header compacto (14 bytes vs ~20+ do MessagePack)
- ✅ Controle total do formato
- ✅ Performance (sem overhead de MessagePack no header)

**Por que MessagePack no Payload?**
- ✅ Flexibilidade (qualquer mensagem de jogo)
- ✅ Performance (compressão automática)
- ✅ API moderna (atributos simples)

### 8.2 Formato Wire Protocol

```
Packet serializado (bytes na rede):

┌──────────────────────────────────────────────────┐
│ Type (1 byte)                                    │
├──────────────────────────────────────────────────┤
│ Sequence (2 bytes)                               │
├──────────────────────────────────────────────────┤
│ Ack (2 bytes)                                    │
├──────────────────────────────────────────────────┤
│ AckBits (4 bytes)                                │
├──────────────────────────────────────────────────┤
│ Channel (1 byte)                                 │
├──────────────────────────────────────────────────┤
│ PayloadSize (4 bytes)                            │
├──────────────────────────────────────────────────┤
│ Payload (N bytes - MessagePack serializado)     │
└──────────────────────────────────────────────────┘

Total: 14 + N bytes
```

**Exemplo:**
```csharp
// Criar e enviar packet
var packet = PacketPool.Rent();
packet.Type = (byte)PacketType.Data;
packet.Channel = ChannelType.Reliable;
packet.Sequence = 123;

// Serializa mensagem de jogo com MessagePack
var moveMessage = new PlayerMoveMessage { Direction = new Vec3(1, 0, 0) };
packet.Payload = MessagePackSerializer.Serialize(moveMessage);
packet.PayloadSize = packet.Payload.Length;

// Serializa packet inteiro (header manual + payload)
byte[] wireData = packet.Serialize(); // 14 bytes header + N bytes payload

// Envia via transport
await transport.SendAsync(packet); // Retorna ao pool automaticamente
```

### 8.3 Sistema de ACK/NACK

**NOTA:** Com LiteNetLib, o sistema de ACK é gerenciado internamente quando usa `DeliveryMethod.ReliableOrdered`. Os campos `Ack` e `AckBits` no Packet ficam disponíveis para implementação futura de sistema customizado (ex: lag compensation).

---

## 9. OBJECT POOLING

### 9.1 Importância

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

### 9.2 O que poolear

| Tipo | Frequência | Impacto |
|------|-----------|---------|
| **Packets** | Milhares/s | ALTO |
| **Messages** | Milhares/s | ALTO |
| **byte[] buffers** | Contínuo | ALTO |
| **Commands** | 30-60/s/player | MÉDIO |
| **State snapshots** | 30/s/player | MÉDIO |

### 9.3 Implementação Base

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

### 9.4 Pools Específicos

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

### 9.5 Pattern: Using Statement

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

### 9.6 Performance Esperada

**Sem Pooling:**
- GC Gen0: a cada 2-3 segundos
- GC pause: 10-50ms
- Alocações: ~100MB/segundo

**Com Pooling:**
- GC Gen0: a cada 20-30 segundos
- GC pause: <5ms
- Alocações: ~1MB/segundo (só startup)

---

## 10. CLIENT-SIDE PREDICTION

### 10.1 Fluxo

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

### 10.2 Implementação

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

## 11. TIME SYNCHRONIZATION

### 11.1 O Problema

Cliente e servidor rodam em máquinas diferentes com relógios diferentes. Para prediction/reconciliation funcionar, precisamos sincronizar o tempo.

**Desafios:**
- Latência variável (50-200ms)
- Clock drift (relógios desalinham naturalmente)
- Jitter (variação de latência)

### 11.2 TimeManager

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
}
```

### 11.3 Fluxo de Sincronização

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

### 11.4 Uso na Prediction

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

### 11.5 Interpolation Time

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

## 12. SERVER RECONCILIATION

### 12.1 Server Authoritative

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

### 12.2 State History

Servidor guarda últimos 60 ticks (2 segundos @ 30Hz) para:
- Lag compensation (rewind pra hit detection)
- Debugging
- Replay

---

## 13. INTEREST MANAGEMENT (AOI)

### 13.1 Problema

Com 500 players:
- Enviar updates de TODOS pra TODOS = 500 × 500 = 250.000 msgs/tick
- @ 30Hz = 7.500.000 mensagens/segundo
- **IMPOSSÍVEL**

### 13.2 Solução: Area of Interest

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

### 13.3 Spatial Hash Grid

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

## 14. OBSERVABILITY & METRICS

### 14.1 Importância

Sem métricas, você está voando cego. Precisa saber:
- Performance do servidor (CPU, memória, network)
- Comportamento dos jogadores
- Gargalos e problemas antes de virarem crises

### 14.2 ServerMetrics

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

### 14.3 Logging Estruturado (Serilog)

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

---

## 15. AUTHSERVER

### 15.1 Fluxo de Autenticação

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

### 15.2 Database Schema

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

### 15.3 Security

- **Passwords:** BCrypt (cost 12)
- **JWT:** HS256 ou RS256, secret key em env variable
- **Rate Limiting:** 5 tentativas/minuto por IP
- **HTTPS:** Obrigatório em produção
- **SQL Injection:** Queries parametrizadas (Dapper)

---

## 16. CONTAINERIZATION (DOCKER)

### 16.1 Por Que Docker?

**Benefícios:**
- ✅ Ambiente idêntico em dev/staging/prod
- ✅ PostgreSQL configurado automaticamente
- ✅ Fácil replicar setup
- ✅ CI/CD simplificado
- ✅ Isolamento de serviços

### 16.2 Estrutura de Containers

```
docker-compose.yml:
├── postgres          (Database)
├── authserver        (AuthServer .NET 9)
├── gameserver        (GameServer .NET 9)
└── adminer           (DB Admin UI - opcional)
```

### 16.3 docker-compose.yml

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
      - "7777:7777/udp"
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

---

## 17. THREADING MODEL

### 17.1 Filosofia

**Regra de Ouro:** Simulação de jogo = Single-threaded. I/O = Multi-threaded.

**Por quê?**
- Evita locks/races/deadlocks
- Código mais simples
- Performance previsível
- Fácil debugar

### 17.2 Arquitetura

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
│  ├─ Receive Messages (LiteNetLib)       │
│  ├─ Send Messages (LiteNetLib)          │
│  └─ Database Queries                    │
│                                         │
└─────────────────────────────────────────┘
```

### 17.3 Implementação

```csharp
public class GameServer
{
    private Channel<INetworkMessage> _incomingMessages;
    private CancellationTokenSource _cts;
    
    public async Task RunAsync()
    {
        _cts = new CancellationTokenSource();
        
        // 1. Inicia I/O threads (LiteNetLib faz isso internamente)
        await _transport.StartAsync();
        
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
            
            // 1. Processa mensagens recebidas (da fila thread-safe)
            _transport.ProcessIncoming();
            
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
}
```

### 17.4 Regras

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
- `System.Threading.Channels` ou `ConcurrentQueue` (lock-free queue)
- Enfileira mensagem, main thread processa via `ProcessIncoming()`

**❌ Não usar:**
- `lock { }` na game logic
- `ConcurrentDictionary` pra estado de jogo (só pra I/O data)
- Tasks paralelas pra simulação

---

## 18. ESCALABILIDADE

### 18.1 Por Escala

| Players | Arquitetura | Mudanças no Código |
|---------|-------------|-------------------|
| **10-50** | 1 Auth + 1 Game | Nenhuma |
| **50-500** | 1 Auth + 1 Game + AOI | Adiciona InterestManager |
| **500-2000** | LB Auth + N GameServers | Adiciona MasterServer |
| **2000-5000+** | Cluster Auth + Cluster Game | Database sharding |

**IMPORTANTE:** O código de prediction/reconciliation/transport **NÃO MUDA**.

### 18.2 Arquitetura Multi-Server

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
(7777/UDP)    (7777/UDP)    (7777/UDP)
50 players    50 players    50 players
```

### 18.3 MasterServer (Fase 7)

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

## 19. ROADMAP DE IMPLEMENTAÇÃO

### Timeline Estimado: 6-7 meses (part-time)

| Fase | Duração | Descrição | Status |
|------|---------|-----------|--------|
| **1. Transport** | 2-3 sem | LiteNetLib wrappers, Pooling | ✅ 90% |
| **2. AuthServer** | 2 sem | Login, JWT, PostgreSQL | ⏳ Próximo |
| **3. GameServer** | 2-3 sem | Tick, Handlers, Broadcasting | ⏳ |
| **4. Prediction** | 3-4 sem | Command pattern, Input buffer | ⏳ |
| **5. Reconciliation** | 3-4 sem | Server validation, Rollback | ⏳ |
| **6. AOI** | 2-3 sem | Spatial grid, Interest management | ⏳ |
| **7. Multi-Server** | 2 sem | MasterServer, Load balancing | ⏳ |
| **8. Optimization** | 3-4 sem | Lag comp, Metrics, Polish | ⏳ |
| **9. Documentation** | 1-2 sem | Docs, Examples, Tutorials | ⏳ |

**Total:** ~19-27 semanas

### Milestones

✅ **M1 (Fase 1):** Transport funcionando com LiteNetLib  
⏳ **M2 (Fase 3):** Login + GameServer básico funcionando  
⏳ **M3 (Fase 5):** Prediction + Reconciliation = movimento suave  
⏳ **M4 (Fase 6):** 500+ players simultâneos  
⏳ **M5 (Fase 9):** Framework completo + documentado  

---

## 20. REFERÊNCIAS

### 20.1 Código de Estudo

**LiteNetLib:**
- GitHub: https://github.com/RevenantX/LiteNetLib
- Docs: https://revenantx.github.io/LiteNetLib/

**Fishnet:**
- GitHub: https://github.com/FirstGearGames/FishNet
- Prediction: `FishNet/Runtime/Object/Prediction/PredictedObject.cs`
- TimeManager: `FishNet/Runtime/Managing/Timing/TimeManager.cs`

**Mirror:**
- NetworkTransform: Mais didático que Fishnet

### 20.2 Artigos

- [Valve - Source Multiplayer Networking](https://developer.valvesoftware.com/wiki/Source_Multiplayer_Networking)
- [Gabriel Gambetta - Fast-Paced Multiplayer](https://www.gabrielgambetta.com/client-server-game-architecture.html)
- [Glenn Fiedler - Networking for Game Programmers](https://gafferongames.com/)

### 20.3 Papers

- [Quake III Network Model](https://fabiensanglard.net/quake3/network.php)
- [Unreal Engine Networking](https://docs.unrealengine.com/5.0/en-US/networking-overview-for-unreal-engine/)

---

## 21. GLOSSÁRIO

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
| **Wrapper** | Camada de abstração sobre biblioteca externa |

---

**FIM DO DOCUMENTO**

Versão 1.1 - 2024-11-20 (Atualizado com decisões da Fase 1)
