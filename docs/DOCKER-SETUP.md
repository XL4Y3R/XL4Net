# XL4Net - Docker Setup Guide

**Versão:** 1.0  
**Data:** 2024-11-20  

---

## 1. VISÃO GERAL

Este guia explica como usar Docker para configurar toda a infraestrutura do XL4Net de forma rápida e reproduzível.

**O que Docker faz por você:**
- ✅ PostgreSQL instalado e configurado automaticamente
- ✅ AuthServer e GameServer rodando em containers isolados
- ✅ Ambiente idêntico em dev/staging/prod
- ✅ Fácil replicar setup em outras máquinas
- ✅ CI/CD simplificado

---

## 2. PRÉ-REQUISITOS

### 2.1 Instalar Docker

**Windows:**
1. Baixe Docker Desktop: https://www.docker.com/products/docker-desktop/
2. Instale e reinicie o PC
3. Verifique: `docker --version`

**Linux (Ubuntu/Debian):**
```bash
sudo apt update
sudo apt install docker.io docker-compose
sudo systemctl start docker
sudo systemctl enable docker
sudo usermod -aG docker $USER
```

**Mac:**
1. Baixe Docker Desktop: https://www.docker.com/products/docker-desktop/
2. Instale
3. Verifique: `docker --version`

---

## 3. ESTRUTURA DE ARQUIVOS

```
XL4Net/
├── docker-compose.yml          ← Configuração principal
├── docker-compose.dev.yml      ← Override para desenvolvimento
├── docker-compose.prod.yml     ← Override para produção
├── .env                        ← Variáveis de ambiente (NÃO commitar!)
├── .env.example                ← Template do .env
├── sql/
│   └── init.sql                ← Script de inicialização do DB
└── src/
    ├── XL4Net.AuthServer/
    │   └── Dockerfile
    └── XL4Net.Server/
        └── Dockerfile
```

---

## 4. ARQUIVO docker-compose.yml

Crie na raiz do projeto:

```yaml
version: '3.8'

services:
  # PostgreSQL Database
  postgres:
    image: postgres:16-alpine
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
    networks:
      - xl4net-network
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U xl4admin -d xl4net"]
      interval: 10s
      timeout: 5s
      retries: 5

  # AuthServer
  authserver:
    build:
      context: .
      dockerfile: src/XL4Net.AuthServer/Dockerfile
    container_name: xl4net-auth
    environment:
      - DATABASE_URL=Host=postgres;Port=5432;Database=xl4net;Username=xl4admin;Password=${DB_PASSWORD:-changeme}
      - JWT_SECRET=${JWT_SECRET:-your-secret-key-min-32-chars}
      - ASPNETCORE_ENVIRONMENT=${ENVIRONMENT:-Development}
    ports:
      - "2106:2106"
    networks:
      - xl4net-network
    depends_on:
      postgres:
        condition: service_healthy
    restart: unless-stopped

  # GameServer
  gameserver:
    build:
      context: .
      dockerfile: src/XL4Net.Server/Dockerfile
    container_name: xl4net-game
    environment:
      - AUTHSERVER_URL=http://authserver:2106
      - ASPNETCORE_ENVIRONMENT=${ENVIRONMENT:-Development}
    ports:
      - "7777:7777"      # TCP
      - "7778:7778/udp"  # UDP
    networks:
      - xl4net-network
    depends_on:
      - authserver
    restart: unless-stopped

  # Adminer (Database Admin UI) - Opcional
  adminer:
    image: adminer:latest
    container_name: xl4net-adminer
    ports:
      - "8080:8080"
    networks:
      - xl4net-network
    depends_on:
      - postgres
    restart: unless-stopped

volumes:
  postgres_data:
    driver: local

networks:
  xl4net-network:
    driver: bridge
```

---

## 5. DOCKERFILE - AuthServer

Crie em `src/XL4Net.AuthServer/Dockerfile`:

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copia .csproj e restaura dependências (layer cache)
COPY ["src/XL4Net.Shared/XL4Net.Shared.csproj", "XL4Net.Shared/"]
COPY ["src/XL4Net.AuthServer/XL4Net.AuthServer.csproj", "XL4Net.AuthServer/"]
RUN dotnet restore "XL4Net.AuthServer/XL4Net.AuthServer.csproj"

# Copia todo o código e compila
COPY src/ .
WORKDIR /src/XL4Net.AuthServer
RUN dotnet build -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=publish /app/publish .

# Cria usuário não-root (segurança)
RUN adduser --disabled-password --gecos '' appuser && chown -R appuser /app
USER appuser

EXPOSE 2106

ENTRYPOINT ["dotnet", "XL4Net.AuthServer.dll"]
```

---

## 6. DOCKERFILE - GameServer

Crie em `src/XL4Net.Server/Dockerfile`:

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copia .csproj e restaura
COPY ["src/XL4Net.Shared/XL4Net.Shared.csproj", "XL4Net.Shared/"]
COPY ["src/XL4Net.Server/XL4Net.Server.csproj", "XL4Net.Server/"]
RUN dotnet restore "XL4Net.Server/XL4Net.Server.csproj"

# Copia código e compila
COPY src/ .
WORKDIR /src/XL4Net.Server
RUN dotnet build -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=publish /app/publish .

RUN adduser --disabled-password --gecos '' appuser && chown -R appuser /app
USER appuser

EXPOSE 7777
EXPOSE 7778/udp

ENTRYPOINT ["dotnet", "XL4Net.Server.dll"]
```

---

## 7. ARQUIVO .env

Crie `.env` na raiz (NÃO commitar no Git!):

```bash
# Database
DB_PASSWORD=super_secret_password_123

# JWT
JWT_SECRET=your-jwt-secret-key-must-be-at-least-32-characters-long

# Environment
ENVIRONMENT=Development
```

---

## 8. ARQUIVO .env.example

Crie `.env.example` (pode commitar):

```bash
# Database
DB_PASSWORD=changeme

# JWT
JWT_SECRET=your-secret-key-here

# Environment (Development, Staging, Production)
ENVIRONMENT=Development
```

---

## 9. SCRIPT DE INICIALIZAÇÃO DO DB

Crie `sql/init.sql`:

```sql
-- Extensões
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- Tabela de contas
CREATE TABLE accounts (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    username VARCHAR(50) UNIQUE NOT NULL,
    email VARCHAR(255) UNIQUE NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    metadata JSONB DEFAULT '{}',
    created_at TIMESTAMP DEFAULT NOW(),
    last_login TIMESTAMP,
    is_banned BOOLEAN DEFAULT FALSE,
    ban_reason TEXT
);

-- Índices
CREATE INDEX idx_username ON accounts(username);
CREATE INDEX idx_email ON accounts(email);
CREATE INDEX idx_metadata ON accounts USING GIN(metadata);

-- Tabela de tentativas de login
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

-- Conta admin de teste (senha: admin123)
-- REMOVER EM PRODUÇÃO!
INSERT INTO accounts (username, email, password_hash) 
VALUES (
    'admin',
    'admin@xl4net.local',
    '$2a$12$LQv3c1yqBWVHxkd0LHAkCOYz6TtxMQJqhN8/LewY5GyYIpSRelgyG'
);
```

---

## 10. COMANDOS DOCKER

### 10.1 Primeira vez (Build e Start)

```bash
# 1. Copiar template do .env
cp .env.example .env

# 2. Editar .env com senhas reais
nano .env  # ou vim, code, etc

# 3. Build das imagens
docker-compose build

# 4. Iniciar tudo
docker-compose up -d

# 5. Ver logs
docker-compose logs -f
```

### 10.2 Comandos Diários

```bash
# Iniciar serviços
docker-compose up -d

# Parar serviços
docker-compose down

# Ver logs em tempo real
docker-compose logs -f

# Logs de um serviço específico
docker-compose logs -f gameserver

# Ver status
docker-compose ps

# Restart de um serviço
docker-compose restart authserver
```

### 10.3 Rebuild Após Mudanças no Código

```bash
# Rebuild e restart
docker-compose up -d --build

# Ou rebuild específico
docker-compose build authserver
docker-compose up -d authserver
```

### 10.4 Acessar Container

```bash
# Shell no container
docker exec -it xl4net-game bash

# Ver variáveis de ambiente
docker exec xl4net-game env

# PostgreSQL CLI
docker exec -it xl4net-db psql -U xl4admin -d xl4net
```

### 10.5 Limpar Tudo (CUIDADO!)

```bash
# Para containers e remove volumes (APAGA DATABASE!)
docker-compose down -v

# Remove imagens também
docker-compose down -v --rmi all

# Limpa sistema inteiro (todos os containers/imagens)
docker system prune -a
```

---

## 11. DESENVOLVIMENTO LOCAL

### 11.1 docker-compose.dev.yml

Para desenvolvimento com hot reload:

```yaml
version: '3.8'

services:
  authserver:
    volumes:
      # Monta código fonte (hot reload)
      - ./src:/src
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ASPNETCORE_URLS=http://+:2106

  gameserver:
    volumes:
      - ./src:/src
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
```

**Uso:**
```bash
docker-compose -f docker-compose.yml -f docker-compose.dev.yml up -d
```

### 11.2 Adminer (DB Admin)

Acesse: http://localhost:8080

**Login:**
- System: PostgreSQL
- Server: postgres
- Username: xl4admin
- Password: (do seu .env)
- Database: xl4net

---

## 12. PRODUÇÃO

### 12.1 docker-compose.prod.yml

```yaml
version: '3.8'

services:
  postgres:
    restart: always
    volumes:
      # Backup diário
      - ./backups:/backups
    deploy:
      resources:
        limits:
          cpus: '2.0'
          memory: 2G

  authserver:
    restart: always
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:2106
    deploy:
      resources:
        limits:
          cpus: '1.0'
          memory: 512M

  gameserver:
    restart: always
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
    deploy:
      resources:
        limits:
          cpus: '2.0'
          memory: 2G
    
  # Remove Adminer em produção
  adminer:
    profiles:
      - debug
```

**Uso:**
```bash
docker-compose -f docker-compose.yml -f docker-compose.prod.yml up -d
```

### 12.2 Backup do Database

```bash
# Backup manual
docker exec xl4net-db pg_dump -U xl4admin xl4net > backup_$(date +%Y%m%d).sql

# Restaurar backup
docker exec -i xl4net-db psql -U xl4admin xl4net < backup_20241120.sql
```

### 12.3 Script de Backup Automático

Crie `scripts/backup.sh`:

```bash
#!/bin/bash
DATE=$(date +%Y%m%d_%H%M%S)
BACKUP_DIR="/backups"
CONTAINER="xl4net-db"

# Backup
docker exec $CONTAINER pg_dump -U xl4admin xl4net > $BACKUP_DIR/xl4net_$DATE.sql

# Compactar
gzip $BACKUP_DIR/xl4net_$DATE.sql

# Limpar backups antigos (>30 dias)
find $BACKUP_DIR -name "xl4net_*.sql.gz" -mtime +30 -delete

echo "Backup completed: xl4net_$DATE.sql.gz"
```

**Cron (backup diário às 3am):**
```bash
crontab -e
# Adicionar:
0 3 * * * /path/to/scripts/backup.sh
```

---

## 13. TROUBLESHOOTING

### 13.1 Porta já em uso

```bash
# Erro: port 5432 already in use

# Descobrir o que está usando
sudo lsof -i :5432

# Parar PostgreSQL local (Linux)
sudo systemctl stop postgresql

# Ou mudar porta no docker-compose.yml
ports:
  - "5433:5432"  # Host:Container
```

### 13.2 Container não inicia

```bash
# Ver logs detalhados
docker logs xl4net-game

# Ver últimas 100 linhas
docker logs --tail 100 xl4net-game

# Modo interativo (debug)
docker-compose up  # Sem -d
```

### 13.3 Database connection failed

```bash
# Verificar se PostgreSQL está healthy
docker-compose ps

# Se não estiver, ver logs
docker logs xl4net-db

# Testar conexão manual
docker exec -it xl4net-db psql -U xl4admin -d xl4net
```

### 13.4 Mudanças no código não aparecem

```bash
# Rebuild forçado
docker-compose build --no-cache authserver
docker-compose up -d authserver
```

---

## 14. CI/CD (GitHub Actions)

Exemplo de `.github/workflows/docker.yml`:

```yaml
name: Docker Build and Push

on:
  push:
    branches: [ main ]

jobs:
  build:
    runs-on: ubuntu-latest
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Login to Docker Hub
      uses: docker/login-action@v2
      with:
        username: ${{ secrets.DOCKER_USERNAME }}
        password: ${{ secrets.DOCKER_PASSWORD }}
    
    - name: Build and push AuthServer
      run: |
        docker build -f src/XL4Net.AuthServer/Dockerfile -t xl4yer/xl4net-auth:latest .
        docker push xl4yer/xl4net-auth:latest
    
    - name: Build and push GameServer
      run: |
        docker build -f src/XL4Net.Server/Dockerfile -t xl4yer/xl4net-game:latest .
        docker push xl4yer/xl4net-game:latest
```

---

## 15. BOAS PRÁTICAS

### 15.1 Segurança

- ✅ **NUNCA** commite `.env` no Git
- ✅ Use senhas fortes (>32 caracteres)
- ✅ Adicione `.env` no `.gitignore`
- ✅ Rode containers como non-root user
- ✅ Em produção, use secrets do Docker Swarm/Kubernetes

### 15.2 Performance

- ✅ Use multi-stage builds (reduz tamanho da imagem)
- ✅ Aproveite layer caching (COPY .csproj antes do código)
- ✅ Use imagens alpine quando possível
- ✅ Limite recursos (CPU/memória) em produção

### 15.3 Monitoramento

```bash
# Ver uso de recursos
docker stats

# Apenas serviços XL4Net
docker stats xl4net-db xl4net-auth xl4net-game
```

---

## 16. PRÓXIMOS PASSOS

Depois que dominar Docker Compose, considere:

1. **Docker Swarm** - Orquestração simples para múltiplos servidores
2. **Kubernetes** - Orquestração avançada para produção
3. **Prometheus + Grafana** - Monitoramento de métricas
4. **ELK Stack** - Logs centralizados

---

**FIM DO DOCUMENTO**

Versão 1.0 - 2024-11-20
