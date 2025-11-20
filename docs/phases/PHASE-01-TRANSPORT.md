# FASE 1: Transport Layer

**Duração estimada:** 2-3 semanas  
**Objetivo:** Implementar camada de transporte TCP/UDP com pooling

---

## VISÃO GERAL

Nesta fase vamos criar a fundação do XL4Net: o sistema de transporte de rede que permite comunicação entre cliente e servidor via TCP e UDP.

**O que faremos:**
1. Setup completo do Visual Studio 2022
2. Criar projetos e configurar dependências
3. Estudar código do Fishnet Tugboat
4. Implementar TCP client/server
5. Implementar UDP client/server
6. Sistema de Reliable UDP (ack/resend)
7. Connection management (handshake, heartbeat)
8. Object pooling
9. Testar com 2 clients conectando

---

## CHECKLIST DETALHADO

### 1.1 Setup Visual Studio 2022 ⏳

- [ ] Instalar VS2022 (se necessário)
- [ ] Criar solution XL4Net.sln
- [ ] Criar projeto XL4Net.Shared (.NET Standard 2.1)
- [ ] Criar projeto XL4Net.Client (.NET Standard 2.1)
- [ ] Criar projeto XL4Net.Server (.NET 9)
- [ ] Criar projeto XL4Net.AuthServer (.NET 9)
- [ ] Configurar referências entre projetos
- [ ] Instalar pacotes NuGet
- [ ] Compilar tudo sem erros

### 1.2 Estrutura Base ⏳

- [ ] XL4Net.Shared/Constants/NetworkPorts.cs
- [ ] XL4Net.Shared/Protocol/Enums/MessageType.cs
- [ ] XL4Net.Shared/Protocol/Enums/ChannelType.cs
- [ ] XL4Net.Shared/Protocol/Enums/DisconnectReason.cs

### 1.3 Object Pooling ⏳

- [ ] XL4Net.Shared/Pooling/IPoolable.cs
- [ ] XL4Net.Shared/Pooling/ObjectPool.cs
- [ ] XL4Net.Shared/Pooling/PooledObject.cs
- [ ] XL4Net.Server/Pooling/PacketPool.cs
- [ ] XL4Net.Server/Pooling/BufferPool.cs
- [ ] XL4Net.Server/Pooling/PoolMetrics.cs

### 1.4 Packet Structure ⏳

- [ ] XL4Net.Shared/Transport/Packet.cs (struct + IPoolable)
- [ ] Sequence number
- [ ] Ack + AckBits
- [ ] Channel type
- [ ] Payload

### 1.5 TCP Implementation ⏳

- [ ] XL4Net.Client/Transport/TcpClient.cs
- [ ] XL4Net.Server/Transport/TcpServer.cs
- [ ] ConnectAsync() / AcceptClientsAsync()
- [ ] SendAsync() / ReceiveAsync()
- [ ] Handshake (SYN/ACK)
- [ ] Heartbeat (ping/pong)
- [ ] Graceful disconnect

### 1.6 UDP Implementation ⏳

- [ ] XL4Net.Client/Transport/UdpClient.cs
- [ ] XL4Net.Server/Transport/UdpServer.cs
- [ ] SendTo() / ReceiveFrom()
- [ ] Connection-less handshake
- [ ] Heartbeat

### 1.7 Reliable UDP ⏳

- [ ] Sequence numbering
- [ ] Ack/Nack system
- [ ] Resend logic (timeout 100ms, max 5 retries)
- [ ] Packet ordering
- [ ] Fragmentação (mensagens >MTU)

### 1.8 Channels ⏳

- [ ] Reliable channel (TCP-like)
- [ ] Unreliable channel (fire-and-forget)
- [ ] Sequenced channel (discard old)

### 1.9 Testes ⏳

- [ ] Teste: 1 client conecta via TCP
- [ ] Teste: 1 client conecta via UDP
- [ ] Teste: 2 clients conectam simultâneos
- [ ] Teste: Heartbeat timeout (desconecta após 5s)
- [ ] Teste: Reliable UDP (simular packet loss)
- [ ] Teste: Pool metrics (sem leaks)

---

## PARTE 1: SETUP VISUAL STUDIO 2022

### Passo 1: Criar Solution

1. Abra **Visual Studio 2022**
2. Clique em **"Create a new project"**
3. Na busca, digite **"Blank Solution"**
4. Selecione **"Blank Solution"**
5. Clique **"Next"**
6. **Solution name:** `XL4Net`
7. **Location:** Escolha onde salvar (ex: `C:\Dev\XL4Net`)
8. Clique **"Create"**

**Resultado esperado:** Solution vazia criada.

---

### Passo 2: Criar XL4Net.Shared (.NET Standard 2.1)

1. No **Solution Explorer**, clique com botão direito em **"Solution 'XL4Net'"**
2. **Add → New Project**
3. Na busca, digite **"Class Library"**
4. Selecione **"Class Library"** (ícone C#)
5. Clique **"Next"**
6. **Project name:** `XL4Net.Shared`
7. **Location:** Deixe o padrão (dentro da pasta XL4Net)
8. Clique **"Next"**
9. **Framework:** Selecione **".NET Standard 2.1"**
10. Clique **"Create"**

**IMPORTANTE:** Apague o arquivo `Class1.cs` que foi criado automaticamente.

**Resultado esperado:** Projeto XL4Net.Shared criado e visível no Solution Explorer.

---

### Passo 3: Criar XL4Net.Client (.NET Standard 2.1)

Repita o processo:

1. Botão direito em **Solution → Add → New Project**
2. **"Class Library"** (C#)
3. **Project name:** `XL4Net.Client`
4. **Framework:** **".NET Standard 2.1"**
5. Apague `Class1.cs`

---

### Passo 4: Criar XL4Net.Server (.NET 9)

1. Botão direito em **Solution → Add → New Project**
2. **"Class Library"** (C#)
3. **Project name:** `XL4Net.Server`
4. **Framework:** **".NET 9.0"** ⚠️ (não .NET Standard!)
5. Apague `Class1.cs`

---

### Passo 5: Criar XL4Net.AuthServer (.NET 9)

1. Botão direito em **Solution → Add → New Project**
2. Desta vez, selecione **"Console App"** (não Class Library)
3. **Project name:** `XL4Net.AuthServer`
4. **Framework:** **".NET 9.0"**
5. Deixe o `Program.cs` (não apague)

---

### Passo 6: Configurar Referências

Agora vamos fazer os projetos se enxergarem:

#### XL4Net.Client → XL4Net.Shared

1. No **Solution Explorer**, expanda **"XL4Net.Client"**
2. Botão direito em **"Dependencies"**
3. **Add Project Reference...**
4. Marque ✅ **XL4Net.Shared**
5. Clique **"OK"**

#### XL4Net.Server → XL4Net.Shared

1. Expanda **"XL4Net.Server"**
2. Botão direito em **"Dependencies"**
3. **Add Project Reference...**
4. Marque ✅ **XL4Net.Shared**
5. Clique **"OK"**

#### XL4Net.AuthServer → XL4Net.Shared

1. Expanda **"XL4Net.AuthServer"**
2. Botão direito em **"Dependencies"**
3. **Add Project Reference...**
4. Marque ✅ **XL4Net.Shared**
5. Clique **"OK"**

**Resultado esperado:** 

```
Solution Explorer:
├── XL4Net.Shared
├── XL4Net.Client
│   └── Dependencies
│       └── Projects
│           └── XL4Net.Shared
├── XL4Net.Server
│   └── Dependencies
│       └── Projects
│           └── XL4Net.Shared
└── XL4Net.AuthServer
    └── Dependencies
        └── Projects
            └── XL4Net.Shared
```

---

### Passo 7: Instalar Pacotes NuGet

#### XL4Net.Shared - MessagePack

1. Botão direito em **XL4Net.Shared → Manage NuGet Packages**
2. Clique na aba **"Browse"**
3. Busque: **"MessagePack"**
4. Selecione **"MessagePack"** (by neuecc)
5. Clique **"Install"**
6. Aceite a licença

#### XL4Net.Client - MessagePack

1. Botão direito em **XL4Net.Client → Manage NuGet Packages**
2. **Browse → "MessagePack"**
3. **Install**

#### XL4Net.Server - Múltiplos Pacotes

1. Botão direito em **XL4Net.Server → Manage NuGet Packages**
2. Instale um por vez:
   - **MessagePack**
   - **Serilog**
   - **Serilog.Sinks.Console**
   - **Serilog.Sinks.File**

#### XL4Net.AuthServer - Múltiplos Pacotes

1. Botão direito em **XL4Net.AuthServer → Manage NuGet Packages**
2. Instale:
   - **MessagePack**
   - **Npgsql** (PostgreSQL driver)
   - **Dapper** (micro ORM)
   - **BCrypt.Net-Next** (password hashing)
   - **System.IdentityModel.Tokens.Jwt** (JWT)
   - **Serilog**
   - **Serilog.Sinks.Console**

**Resultado esperado:** Todos os pacotes instalados sem erros.

---

### Passo 8: Verificar Compilação

1. No menu, clique **Build → Build Solution** (ou `Ctrl+Shift+B`)
2. Verifique a janela **Output** (embaixo)
3. Deve mostrar: **"Build succeeded"** para todos os 4 projetos

**Se der erro:**
- Verifique se frameworks estão corretos (.NET Standard 2.1 vs .NET 9)
- Verifique se referências foram adicionadas
- Verifique se pacotes foram instalados

---

### Passo 9: Criar Estrutura de Pastas

Agora vamos organizar os projetos:

#### XL4Net.Shared

Clique com botão direito no projeto → **Add → New Folder**

Crie as seguintes pastas:
```
XL4Net.Shared/
├── Constants/
├── Protocol/
│   ├── Messages/
│   ├── Serialization/
│   └── Enums/
├── Models/
├── Transport/
└── Pooling/
```

#### XL4Net.Client

```
XL4Net.Client/
├── Core/
├── Prediction/
├── Reconciliation/
├── Interpolation/
├── Transport/
└── Events/
```

#### XL4Net.Server

```
XL4Net.Server/
├── Core/
├── Simulation/
├── Reconciliation/
├── Broadcasting/
├── MessageHandlers/
├── Transport/
├── States/
├── Pooling/
└── Events/
```

#### XL4Net.AuthServer

```
XL4Net.AuthServer/
├── Core/
├── Authentication/
├── Database/
├── Models/
└── Endpoints/
```

**Dica:** Para criar pastas aninhadas (ex: `Protocol/Messages`), crie uma por vez.

---

## PARTE 2: PRIMEIROS ARQUIVOS

Agora vamos criar os arquivos básicos.

### 2.1 NetworkPorts.cs

**Local:** `XL4Net.Shared/Constants/NetworkPorts.cs`

```csharp
namespace XL4Net.Shared.Constants
{
    /// <summary>
    /// Define as portas padrão usadas pelo XL4Net.
    /// </summary>
    public static class NetworkPorts
    {
        /// <summary>
        /// Porta TCP do AuthServer (login/autenticação).
        /// </summary>
        public const ushort AUTH_TCP = 2106;
        
        /// <summary>
        /// Porta TCP do GameServer (mensagens confiáveis).
        /// </summary>
        public const ushort GAME_TCP = 7777;
        
        /// <summary>
        /// Porta UDP do GameServer (mensagens rápidas/unreliable).
        /// </summary>
        public const ushort GAME_UDP = 7778;
    }
}
```

**Como criar:**
1. Botão direito na pasta **Constants**
2. **Add → Class...**
3. Nome: `NetworkPorts.cs`
4. Cole o código acima

---

### 2.2 ChannelType.cs

**Local:** `XL4Net.Shared/Protocol/Enums/ChannelType.cs`

```csharp
namespace XL4Net.Shared.Protocol.Enums
{
    /// <summary>
    /// Tipos de canais de comunicação.
    /// </summary>
    public enum ChannelType : byte
    {
        /// <summary>
        /// Canal confiável (TCP-like no UDP).
        /// Garante entrega, ordem e sem duplicação.
        /// Usa ack/resend.
        /// </summary>
        Reliable = 0,
        
        /// <summary>
        /// Canal não-confiável (fire-and-forget).
        /// Sem garantia de entrega ou ordem.
        /// Menor latência.
        /// </summary>
        Unreliable = 1,
        
        /// <summary>
        /// Canal sequenciado.
        /// Descarta pacotes velhos (seq < último recebido).
        /// Sem garantia de entrega, mas mantém ordem.
        /// </summary>
        Sequenced = 2
    }
}
```

---

### 2.3 MessageType.cs

**Local:** `XL4Net.Shared/Protocol/Enums/MessageType.cs`

```csharp
namespace XL4Net.Shared.Protocol.Enums
{
    /// <summary>
    /// Tipos de mensagens do protocolo XL4Net.
    /// </summary>
    public enum MessageType : ushort
    {
        // Sistema (0-99)
        Unknown = 0,
        Ping = 1,
        Pong = 2,
        Disconnect = 3,
        
        // Autenticação (100-199)
        LoginRequest = 100,
        LoginResponse = 101,
        TokenValidation = 102,
        
        // Gameplay (200-299)
        PlayerMove = 200,
        PlayerAttack = 201,
        EntitySpawn = 202,
        EntityUpdate = 203,
        EntityDespawn = 204,
        
        // Chat/Social (300-399)
        ChatMessage = 300
    }
}
```

---

### 2.4 DisconnectReason.cs

**Local:** `XL4Net.Shared/Protocol/Enums/DisconnectReason.cs`

```csharp
namespace XL4Net.Shared.Protocol.Enums
{
    /// <summary>
    /// Motivos de desconexão.
    /// </summary>
    public enum DisconnectReason : byte
    {
        /// <summary>
        /// Desconexão normal iniciada pelo cliente.
        /// </summary>
        ClientRequested = 0,
        
        /// <summary>
        /// Servidor está desligando.
        /// </summary>
        ServerShutdown = 1,
        
        /// <summary>
        /// Timeout (sem heartbeat por >5 segundos).
        /// </summary>
        Timeout = 2,
        
        /// <summary>
        /// Dados inválidos ou corrompidos.
        /// </summary>
        InvalidData = 3,
        
        /// <summary>
        /// Servidor cheio.
        /// </summary>
        ServerFull = 4,
        
        /// <summary>
        /// Autenticação falhou.
        /// </summary>
        AuthenticationFailed = 5,
        
        /// <summary>
        /// Cliente foi banido.
        /// </summary>
        Banned = 6,
        
        /// <summary>
        /// Erro de rede.
        /// </summary>
        NetworkError = 7
    }
}
```

---

### 2.5 Vec3.cs (Tipos Matemáticos Próprios)

**IMPORTANTE:** XL4Net NÃO usa tipos Unity (Vector3, Vector2, etc). Usamos tipos próprios para manter o código engine-agnostic.

**Local:** `XL4Net.Shared/Math/Vec3.cs`

```csharp
using System;

namespace XL4Net.Shared.Math
{
    /// <summary>
    /// Vetor 3D (substitui UnityEngine.Vector3).
    /// </summary>
    public struct Vec3 : IEquatable<Vec3>
    {
        public float X;
        public float Y;
        public float Z;
        
        public Vec3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
        
        // Propriedades úteis
        public float Magnitude => MathF.Sqrt(X * X + Y * Y + Z * Z);
        public float SqrMagnitude => X * X + Y * Y + Z * Z;
        
        public Vec3 Normalized
        {
            get
            {
                var mag = Magnitude;
                return mag > 0.00001f ? this / mag : Zero;
            }
        }
        
        // Constantes
        public static Vec3 Zero => new Vec3(0, 0, 0);
        public static Vec3 One => new Vec3(1, 1, 1);
        public static Vec3 Forward => new Vec3(0, 0, 1);
        public static Vec3 Back => new Vec3(0, 0, -1);
        public static Vec3 Up => new Vec3(0, 1, 0);
        public static Vec3 Down => new Vec3(0, -1, 0);
        public static Vec3 Right => new Vec3(1, 0, 0);
        public static Vec3 Left => new Vec3(-1, 0, 0);
        
        // Operadores
        public static Vec3 operator +(Vec3 a, Vec3 b) 
            => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        
        public static Vec3 operator -(Vec3 a, Vec3 b) 
            => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        
        public static Vec3 operator *(Vec3 a, float scalar) 
            => new Vec3(a.X * scalar, a.Y * scalar, a.Z * scalar);
        
        public static Vec3 operator /(Vec3 a, float scalar) 
            => new Vec3(a.X / scalar, a.Y / scalar, a.Z / scalar);
        
        public static Vec3 operator -(Vec3 a) 
            => new Vec3(-a.X, -a.Y, -a.Z);
        
        // Métodos úteis
        public static float Distance(Vec3 a, Vec3 b)
        {
            return (a - b).Magnitude;
        }
        
        public static float Dot(Vec3 a, Vec3 b)
        {
            return a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        }
        
        public static Vec3 Cross(Vec3 a, Vec3 b)
        {
            return new Vec3(
                a.Y * b.Z - a.Z * b.Y,
                a.Z * b.X - a.X * b.Z,
                a.X * b.Y - a.Y * b.X
            );
        }
        
        public static Vec3 Lerp(Vec3 a, Vec3 b, float t)
        {
            t = System.Math.Clamp(t, 0f, 1f);
            return new Vec3(
                a.X + (b.X - a.X) * t,
                a.Y + (b.Y - a.Y) * t,
                a.Z + (b.Z - a.Z) * t
            );
        }
        
        // Equality
        public bool Equals(Vec3 other)
        {
            return MathF.Abs(X - other.X) < 0.00001f &&
                   MathF.Abs(Y - other.Y) < 0.00001f &&
                   MathF.Abs(Z - other.Z) < 0.00001f;
        }
        
        public override bool Equals(object obj) 
            => obj is Vec3 other && Equals(other);
        
        public override int GetHashCode() 
            => HashCode.Combine(X, Y, Z);
        
        public static bool operator ==(Vec3 a, Vec3 b) => a.Equals(b);
        public static bool operator !=(Vec3 a, Vec3 b) => !a.Equals(b);
        
        public override string ToString() 
            => $"({X:F2}, {Y:F2}, {Z:F2})";
    }
}
```

---

### 2.6 Vec2.cs

**Local:** `XL4Net.Shared/Math/Vec2.cs`

```csharp
using System;

namespace XL4Net.Shared.Math
{
    /// <summary>
    /// Vetor 2D (substitui UnityEngine.Vector2).
    /// </summary>
    public struct Vec2 : IEquatable<Vec2>
    {
        public float X;
        public float Y;
        
        public Vec2(float x, float y)
        {
            X = x;
            Y = y;
        }
        
        public float Magnitude => MathF.Sqrt(X * X + Y * Y);
        public float SqrMagnitude => X * X + Y * Y;
        
        public Vec2 Normalized
        {
            get
            {
                var mag = Magnitude;
                return mag > 0.00001f ? this / mag : Zero;
            }
        }
        
        public static Vec2 Zero => new Vec2(0, 0);
        public static Vec2 One => new Vec2(1, 1);
        public static Vec2 Up => new Vec2(0, 1);
        public static Vec2 Down => new Vec2(0, -1);
        public static Vec2 Right => new Vec2(1, 0);
        public static Vec2 Left => new Vec2(-1, 0);
        
        public static Vec2 operator +(Vec2 a, Vec2 b) 
            => new Vec2(a.X + b.X, a.Y + b.Y);
        
        public static Vec2 operator -(Vec2 a, Vec2 b) 
            => new Vec2(a.X - b.X, a.Y - b.Y);
        
        public static Vec2 operator *(Vec2 a, float scalar) 
            => new Vec2(a.X * scalar, a.Y * scalar);
        
        public static Vec2 operator /(Vec2 a, float scalar) 
            => new Vec2(a.X / scalar, a.Y / scalar);
        
        public static float Distance(Vec2 a, Vec2 b) 
            => (a - b).Magnitude;
        
        public static float Dot(Vec2 a, Vec2 b) 
            => a.X * b.X + a.Y * b.Y;
        
        public static Vec2 Lerp(Vec2 a, Vec2 b, float t)
        {
            t = System.Math.Clamp(t, 0f, 1f);
            return new Vec2(
                a.X + (b.X - a.X) * t,
                a.Y + (b.Y - a.Y) * t
            );
        }
        
        public bool Equals(Vec2 other)
        {
            return MathF.Abs(X - other.X) < 0.00001f &&
                   MathF.Abs(Y - other.Y) < 0.00001f;
        }
        
        public override bool Equals(object obj) 
            => obj is Vec2 other && Equals(other);
        
        public override int GetHashCode() 
            => HashCode.Combine(X, Y);
        
        public static bool operator ==(Vec2 a, Vec2 b) => a.Equals(b);
        public static bool operator !=(Vec2 a, Vec2 b) => !a.Equals(b);
        
        public override string ToString() 
            => $"({X:F2}, {Y:F2})";
    }
}
```

---

### 2.7 Vec2Int.cs (Para Spatial Grid)

**Local:** `XL4Net.Shared/Math/Vec2Int.cs`

```csharp
using System;

namespace XL4Net.Shared.Math
{
    /// <summary>
    /// Vetor 2D inteiro (usado em spatial grid).
    /// </summary>
    public struct Vec2Int : IEquatable<Vec2Int>
    {
        public int X;
        public int Y;
        
        public Vec2Int(int x, int y)
        {
            X = x;
            Y = y;
        }
        
        public static Vec2Int Zero => new Vec2Int(0, 0);
        public static Vec2Int One => new Vec2Int(1, 1);
        public static Vec2Int Up => new Vec2Int(0, 1);
        public static Vec2Int Down => new Vec2Int(0, -1);
        public static Vec2Int Right => new Vec2Int(1, 0);
        public static Vec2Int Left => new Vec2Int(-1, 0);
        
        public static Vec2Int operator +(Vec2Int a, Vec2Int b) 
            => new Vec2Int(a.X + b.X, a.Y + b.Y);
        
        public static Vec2Int operator -(Vec2Int a, Vec2Int b) 
            => new Vec2Int(a.X - b.X, a.Y - b.Y);
        
        public static Vec2Int operator *(Vec2Int a, int scalar) 
            => new Vec2Int(a.X * scalar, a.Y * scalar);
        
        public bool Equals(Vec2Int other) 
            => X == other.X && Y == other.Y;
        
        public override bool Equals(object obj) 
            => obj is Vec2Int other && Equals(other);
        
        public override int GetHashCode() 
            => HashCode.Combine(X, Y);
        
        public static bool operator ==(Vec2Int a, Vec2Int b) => a.Equals(b);
        public static bool operator !=(Vec2Int a, Vec2Int b) => !a.Equals(b);
        
        public override string ToString() 
            => $"({X}, {Y})";
    }
}
```

**NOTA:** Quando fizer integração com Unity, você criará extension methods num projeto separado:

```csharp
// XL4Net.Unity/Extensions/VectorExtensions.cs (futuro)
public static class VectorExtensions
{
    public static Vec3 ToVec3(this UnityEngine.Vector3 v) 
        => new Vec3(v.x, v.y, v.z);
    
    public static UnityEngine.Vector3 ToUnity(this Vec3 v)
        => new UnityEngine.Vector3(v.X, v.Y, v.Z);
}
```

---

### Passo 10: Compilar Novamente

1. **Build → Build Solution** (`Ctrl+Shift+B`)
2. Deve compilar sem erros

**Se der erro:**
- Verifique se criou os arquivos nas pastas corretas
- Verifique os namespaces

---

## PRÓXIMOS PASSOS

Nesta conversa, paramos aqui. Fizemos:

✅ Setup completo do Visual Studio 2022
✅ 4 projetos criados com frameworks corretos
✅ Referências configuradas
✅ Pacotes NuGet instalados
✅ Estrutura de pastas criada
✅ Arquivos básicos (enums, constants)

**Na PRÓXIMA conversa, faremos:**

1. Implementar Object Pooling (IPoolable, ObjectPool)
2. Implementar Packet structure
3. Começar TCP client/server

**Template para próxima conversa:**

```
Olá! Continuando o XL4Net.

AÇÕES OBRIGATÓRIAS:
1. Leia docs/00-ARCHITECTURE.md
2. Leia docs/01-CODING-STANDARDS.md
3. Leia docs/02-PROJECT-STATE.md
4. Leia docs/phases/PHASE-01-TRANSPORT.md

CONTEXTO:
- Fase 1 - Transport Layer
- Setup VS2022 completo ✅
- Enums e constants criados ✅

OBJETIVO:
Implementar Object Pooling system (IPoolable, ObjectPool, PooledObject)

Confirme que leu os documentos antes de começar.
```

---

## REFERÊNCIAS FISHNET

Para estudar antes de implementar:

### Transport Layer
```
FishNet/Runtime/Transporting/Transports/Tugboat/
├── Client/
│   └── ClientSocket.cs          ← Estudar ConnectAsync(), SendAsync()
├── Server/
│   └── ServerSocket.cs          ← Estudar AcceptAsync(), BroadcastAsync()
└── Core/
    ├── CommonSocket.cs          ← Lógica compartilhada
    ├── Packet.cs                ← Estrutura de pacote
    └── Channel.cs               ← Reliable/Unreliable logic
```

### Como estudar:
1. Clone o repositório Fishnet: `git clone https://github.com/FirstGearGames/FishNet.git`
2. Abra os arquivos no VS Code ou VS2022
3. Leia os comentários
4. Entenda a estrutura antes de copiar

---

**FIM DA FASE 1 - PARTE 1**

Continue na próxima conversa!
