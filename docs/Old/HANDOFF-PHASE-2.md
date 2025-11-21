# XL4Net - Handoff para Próxima Conversa
## Transport Layer (Fase 1) → AuthServer (Fase 2)

**Data:** 2024-11-20  
**Sessão:** 2 → 3  
**Fase concluída:** 1 - Transport Layer (90%)  
**Próxima fase:** 2 - AuthServer  
**Tokens usados na sessão 2:** ~148.000 / 190.000

---

## ✅ O QUE FOI CONCLUÍDO

### **1. Transport Layer Funcional** 🎉

Implementamos wrappers completos sobre o LiteNetLib:

**Arquivos criados:**
- `XL4Net.Client/Transport/LiteNetTransport.cs` (~450 linhas)
  - Wrapper do NetManager do LiteNetLib
  - Threading correto (ConcurrentQueue + ProcessIncoming)
  - Força IPv4 (resolve problema localhost → ::1)
  - Connection key com NetDataWriter
  - Pooling automático no SendAsync

- `XL4Net.Server/Transport/LiteNetServerTransport.cs` (~600 linhas)
  - Gerencia múltiplos clientes (ConcurrentDictionary)
  - Broadcast otimizado (serializa 1x, envia N)
  - Connection request validation (key + max clients)
  - ClientId único via Interlocked.Increment
  - peer.Tag para fast lookup

**Packet.cs:**
- Serialização manual customizada (14 bytes header)
- Métodos IsAcked(), MarkAsAcked()
- Atributos MessagePack [MessagePackObject] e [Key(N)]
- IPoolable implementado

**Testes:**
- ServerTest/Program.cs (console .NET 9)
- ClientTest/Program.cs (console .NET 9)
- ✅ Teste funcional 100% sucesso (echo funcionando)

---

## 🔧 PROBLEMAS RESOLVIDOS

### **Problema 1: IPv6 vs IPv4**
**Sintoma:** Cliente não conectava, ficava "Outgoing" → "Disconnected"  
**Causa:** DNS resolvia localhost para ::1 (IPv6), servidor só aceitava IPv4  
**Solução:** Filtrar apenas AddressFamily.InterNetwork no ConnectAsync  
**Código:**
```csharp
var ipv4Addresses = addresses
    .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
    .ToArray();
```

### **Problema 2: Connection Key**
**Sintoma:** Servidor rejeitava conexão  
**Causa:** Enviando string direta em vez de NetDataWriter  
**Solução:**
```csharp
var writer = new NetDataWriter();
writer.Put(_connectionKey);
_serverPeer = _netManager.Connect(endpoint, writer);
```

### **Problema 3: Cliente não recebia mensagens**
**Sintoma:** Servidor enviava, cliente não recebia  
**Causa:** Bug no ServerTest.cs (criava segunda instância do servidor)  
**Solução:** Usar instância global `_server` em vez de `GetServerInstance()`

---

## 📋 CHECKLIST FASE 1

### Core (Essencial) ✅
- [x] LiteNetTransport.cs (cliente)
- [x] LiteNetServerTransport.cs (servidor)
- [x] Packet.cs com serialização customizada
- [x] PacketPool funcionando
- [x] Threading correto
- [x] Teste funcional (1 cliente)

### Opcional (Pode fazer depois) ⏳
- [ ] Unit tests automatizados
- [ ] Stress test (10+ clientes)
- [ ] Remover logs de debug
- [ ] Documentação de uso

---

## 🎯 PRÓXIMA FASE: 2 - AUTHSERVER

### **Objetivo:**
Implementar servidor de autenticação com PostgreSQL e JWT.

### **Tecnologias:**
- PostgreSQL 16 (via Docker)
- Npgsql (driver .NET)
- Dapper (micro ORM)
- BCrypt.Net-Next (password hashing)
- System.IdentityModel.Tokens.Jwt (JWT tokens)

### **Arquitetura AuthServer:**

```
AuthServer (porta 2106 TCP)
    ↓
┌─────────────────────────────────┐
│ Endpoints                       │
│  - POST /auth/register          │
│  - POST /auth/login             │
│  - POST /auth/validate-token    │
└─────────────────────────────────┘
    ↓
┌─────────────────────────────────┐
│ Authentication Layer            │
│  - TokenManager (JWT)           │
│  - PasswordHasher (BCrypt)      │
│  - RateLimiter                  │
└─────────────────────────────────┘
    ↓
┌─────────────────────────────────┐
│ Database Layer                  │
│  - PostgresAccountRepository    │
│  - Dapper queries               │
└─────────────────────────────────┘
    ↓
┌─────────────────────────────────┐
│ PostgreSQL                      │
│  - accounts table (UUID id)     │
│  - login_attempts table         │
└─────────────────────────────────┘
```

### **Database Schema:**

```sql
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

CREATE TABLE login_attempts (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id UUID REFERENCES accounts(id),
    ip_address INET NOT NULL,
    username VARCHAR(50),
    success BOOLEAN NOT NULL,
    attempted_at TIMESTAMP DEFAULT NOW()
);

CREATE INDEX idx_login_attempts_ip ON login_attempts(ip_address, attempted_at);
```

### **Arquivos a criar:**

```
XL4Net.AuthServer/ (novo projeto .NET 9)
├── Program.cs
├── AuthConfig.cs
├── Authentication/
│   ├── TokenManager.cs        (JWT generation/validation)
│   ├── PasswordHasher.cs      (BCrypt)
│   └── RateLimiter.cs         (5 tentativas/min por IP)
├── Database/
│   ├── IAccountRepository.cs
│   ├── PostgresAccountRepository.cs
│   └── DbContext.cs
├── Models/
│   ├── Account.cs
│   ├── AuthToken.cs
│   └── LoginAttempt.cs
└── Endpoints/
    ├── RegisterEndpoint.cs
    ├── LoginEndpoint.cs
    └── ValidateTokenEndpoint.cs

docker-compose.yml (raiz do projeto)
sql/
└── init.sql (schema)
```

### **Pacotes NuGet:**
```xml
<PackageReference Include="Npgsql" Version="8.0.1" />
<PackageReference Include="Dapper" Version="2.1.28" />
<PackageReference Include="BCrypt.Net-Next" Version="4.0.3" />
<PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="7.0.3" />
<PackageReference Include="Serilog.AspNetCore" Version="8.0.0" />
```

### **Fluxo de Autenticação:**

```
Cliente                AuthServer               PostgreSQL
  |                        |                        |
  |--- POST /register ---->|                        |
  |    {username, pwd}     |                        |
  |                        |--- Hash password ----->|
  |                        |--- INSERT account ---->|
  |                        |<-- account_id ---------|
  |<-- 201 Created --------|                        |
  |                        |                        |
  |--- POST /login ------->|                        |
  |    {username, pwd}     |                        |
  |                        |--- SELECT account ---->|
  |                        |<-- account data -------|
  |                        |--- Verify BCrypt       |
  |                        |--- Generate JWT        |
  |<-- {token, exp} -------|                        |
  |                        |                        |
  
Cliente connects to GameServer with token
  |                        |                        |
GameServer validates token:
  |--- POST /validate ---->|                        |
  |    {token}             |                        |
  |                        |--- Verify JWT sig      |
  |                        |--- Check expiration    |
  |<-- {valid, userId} ----|                        |
```

### **JWT Structure:**

```json
{
  "header": {
    "alg": "HS256",
    "typ": "JWT"
  },
  "payload": {
    "sub": "account-uuid-here",
    "username": "player123",
    "iat": 1700000000,
    "exp": 1700003600
  },
  "signature": "..."
}
```

### **Rate Limiting:**
- 5 login attempts per IP per minute
- 10 register attempts per IP per hour
- 429 Too Many Requests se exceder

### **Security Checklist:**
- [ ] BCrypt cost factor = 12
- [ ] JWT secret key em environment variable
- [ ] HTTPS obrigatório em produção (dev pode ser HTTP)
- [ ] SQL injection protection (Dapper parametrizado)
- [ ] Input validation (username 3-50 chars, email válido, senha 8+ chars)

---

## 📝 PASSOS PARA INICIAR PRÓXIMA SESSÃO

### **1. Template de Início (copie e cole):**

```
Olá! Vou continuar o desenvolvimento do XL4Net.

AÇÕES OBRIGATÓRIAS:
1. Leia docs/00-ARCHITECTURE.md
2. Leia docs/01-CODING-STANDARDS.md
3. Leia docs/02-PROJECT-STATE.md (ATUALIZADO!)
4. Leia este handoff document

CONTEXTO:
- Projeto: XL4Net (framework de networking)
- Fase atual: 2 - AuthServer
- Último progresso: Fase 1 (Transport) concluída com sucesso! ✅

OBJETIVO DESTA CONVERSA:
Implementar AuthServer com PostgreSQL + JWT seguindo a arquitetura
definida no handoff document.

Confirme que leu os documentos antes de começar.
```

### **2. Claude vai ler:**
- 00-ARCHITECTURE.md (58 KB)
- 01-CODING-STANDARDS.md (18 KB)
- 02-PROJECT-STATE.md (atualizado, ~10 KB)
- Este handoff document (~8 KB)

### **3. Primeira tarefa:**
Claude vai começar pelo Docker setup do PostgreSQL.

---

## 💡 LIÇÕES APRENDIDAS (FASE 1)

### **O que funcionou bem:**
1. ✅ Usar LiteNetLib economizou 3-4 semanas
2. ✅ Logs de debug ajudaram MUITO no troubleshooting
3. ✅ Teste incremental (console antes de Unity)
4. ✅ Interface ITransport isola dependência (pode trocar depois)

### **O que melhorar:**
1. ⚠️ Adicionar unit tests desde o início (não deixar pra depois)
2. ⚠️ Fazer stress test antes de considerar "completo"
3. ⚠️ Documentar "gotchas" (IPv6, Connection key) conforme encontra

### **Para Fase 2:**
1. 🎯 Setup Docker PRIMEIRO (evita problemas depois)
2. 🎯 Testar cada endpoint isoladamente
3. 🎯 Usar Postman/curl para testar antes do cliente
4. 🎯 Implementar rate limiting desde o início (não como "nice to have")

---

## 🔗 LINKS ÚTEIS

### **Documentação LiteNetLib:**
- https://github.com/RevenantX/LiteNetLib
- https://revenantx.github.io/LiteNetLib/

### **PostgreSQL Docker:**
- https://hub.docker.com/_/postgres

### **JWT .NET:**
- https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/wiki

### **BCrypt:**
- https://github.com/BcryptNet/bcrypt.net

### **Dapper:**
- https://github.com/DapperLib/Dapper

---

## 📊 ESTATÍSTICAS DA SESSÃO 2

**Duração:** ~3 horas  
**Tokens usados:** ~148.000 / 190.000 (78%)  
**Linhas de código escritas:** ~1.750  
**Bugs encontrados e resolvidos:** 3  
**Testes realizados:** 1 (funcional console)  
**Status:** ✅ Fase 1 concluída com sucesso!

---

## ⚠️ AVISOS IMPORTANTES PARA PRÓXIMA SESSÃO

### **1. SEMPRE ler os 4 documentos no início**
Não pule esta etapa! Cada documento tem informações críticas.

### **2. Docker pode dar problema no Windows**
Se der erro, verificar:
- Docker Desktop está rodando?
- WSL2 instalado?
- Hyper-V habilitado?

### **3. PostgreSQL connection string**
Formato: `Host=localhost;Port=5432;Database=xl4net;Username=xl4admin;Password=changeme`

### **4. JWT Secret Key**
NUNCA commitar no Git! Usar environment variable ou user secrets.

### **5. Testes com Postman**
Testar endpoints antes de integrar com cliente. Economiza tempo de debug.

---

## 🎯 OBJETIVO DA FASE 2

**Sucesso = Conseguir fazer este fluxo:**

```
1. Rodar docker-compose up
2. PostgreSQL sobe na porta 5432
3. AuthServer sobe na porta 2106
4. POST /register → cria conta
5. POST /login → retorna JWT
6. POST /validate → valida JWT
7. Cliente console consegue se autenticar
```

**Estimativa:** 2 semanas (part-time), ou ~8-12 horas de trabalho.

---

## 📁 ARQUIVOS ANEXOS

Os seguintes arquivos devem ser lidos na próxima sessão:

1. **02-PROJECT-STATE.md** (este foi atualizado)
2. **Este handoff document** (guia específico da Fase 2)
3. **00-ARCHITECTURE.md** (referência geral)
4. **01-CODING-STANDARDS.md** (padrões)

---

**FIM DO HANDOFF**

Boa sorte na Fase 2! 🚀  
Data: 2024-11-20  
Próxima sessão: Fase 2 - AuthServer
