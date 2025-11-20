# XL4Net - Workflow com Claude

**Versão:** 1.0  
**Data:** 2024-11-20  

---

## 1. VISÃO GERAL

Este documento define como trabalhar com Claude.ai no desenvolvimento do XL4Net através de múltiplas conversas, mantendo continuidade e qualidade.

---

## 2. PROTOCOLO DE INÍCIO DE CONVERSA

### 2.1 Template Obrigatório

Sempre inicie uma nova conversa com:

```
Olá! Vou continuar o desenvolvimento do XL4Net.

AÇÕES OBRIGATÓRIAS:
1. Leia docs/00-ARCHITECTURE.md (visão completa)
2. Leia docs/01-CODING-STANDARDS.md (padrões)
3. Leia docs/02-PROJECT-STATE.md (onde estamos)
4. Leia docs/phases/PHASE-XX-[NOME].md (fase atual)

CONTEXTO:
- Projeto: XL4Net (framework de networking)
- Fase atual: [X] - [Nome da Fase]
- Último progresso: [resumo breve do que foi feito]

OBJETIVO DESTA CONVERSA:
[Descrever objetivo específico - ex: "Implementar TCP client/server"]

Confirme que leu os documentos antes de começar.
```

### 2.2 Exemplo Prático

```
Olá! Vou continuar o desenvolvimento do XL4Net.

AÇÕES OBRIGATÓRIAS:
1. Leia docs/00-ARCHITECTURE.md
2. Leia docs/01-CODING-STANDARDS.md
3. Leia docs/02-PROJECT-STATE.md
4. Leia docs/phases/PHASE-01-TRANSPORT.md

CONTEXTO:
- Projeto: XL4Net (framework de networking)
- Fase atual: 1 - Transport Layer
- Último progresso: Projetos criados no VS2022, referências configuradas

OBJETIVO DESTA CONVERSA:
Implementar TcpClient e TcpServer baseado no código do Fishnet Tugboat.

Confirme que leu os documentos antes de começar.
```

---

## 3. PROTOCOLO DURANTE O DESENVOLVIMENTO

### 3.1 Foco em APRENDIZADO

**Claude DEVE:**
- ✅ Explicar conceitos antes de implementar
- ✅ Mostrar exemplos práticos
- ✅ Justificar decisões técnicas
- ✅ Usar analogias quando possível
- ✅ Perguntar se ficou claro
- ✅ Implementar com comentários explicativos

**Claude NÃO DEVE:**
- ❌ Criar resumos intermediários
- ❌ Atualizar documentos no meio do trabalho
- ❌ Fazer recapitulações constantes
- ❌ Implementar sem explicar

### 3.2 Estilo de Comunicação

**Exemplo de boa explicação:**

```
Vou implementar o PacketPool agora. Deixa eu explicar como funciona:

Um Object Pool é basicamente uma "caixa de objetos reutilizáveis".

Imagine uma piscina de bolinhas de criança:
- Você pega uma bolinha (Rent)
- Brinca com ela (Use)
- Devolve pra piscina (Return)
- Outra criança pega a MESMA bolinha depois

No servidor:
- Sem pool: new Packet() 1000x/segundo → GC limpando lixo toda hora
- Com pool: Reutiliza os mesmos 100 Packets → GC raramente roda

Faz sentido? Posso continuar com a implementação?
```

### 3.3 Estrutura de Explicação

Para cada implementação nova:

1. **O QUE** vou fazer
2. **POR QUÊ** vou fazer assim
3. **COMO** funciona (analogia se possível)
4. Mostro o código
5. Pergunto se ficou claro

**Exemplo:**

```
Vou criar o método Rent() do ObjectPool.

POR QUÊ usar ConcurrentBag em vez de List?
- Porque múltiplas threads podem chamar Rent() ao mesmo tempo
- List não é thread-safe (pode corromper dados)
- ConcurrentBag é otimizado para este caso

Código:

public T Rent()
{
    if (_objects.TryTake(out var obj))
        return obj;
    
    return new T(); // Pool vazio, cria novo
}

Viu como o TryTake() é atômico? Isso garante que duas threads 
não pegam o mesmo objeto.

Ficou claro ou quer que eu explique melhor a parte de concorrência?
```

### 3.4 Perguntas Incentivadas

Se Claude não explicar algo direito:

- **"Por que você fez X em vez de Y?"**
- **"Explica de novo com exemplo mais simples"**
- **"Qual diferença entre A e B?"**
- **"Isso não vai causar problema de performance?"**

---

## 4. PROTOCOLO DE FIM DE CONVERSA

### 4.1 Quando Finalizar

Finalize a conversa quando:
- ✅ Objetivo foi alcançado
- ✅ Tokens estão >150k
- ✅ Sessão ficou muito longa (>3 horas)
- ✅ Checkpoint natural da fase

### 4.2 Checklist de Finalização

**ANTES de encerrar, Claude DEVE:**

1. **Atualizar PROJECT-STATE.md:**
   - Marcar tarefas concluídas
   - Adicionar arquivos criados/modificados
   - Documentar decisões tomadas
   - Listar problemas encontrados
   - Indicar próximo passo EXATO

2. **Criar Handoff Summary:**
   - O que foi feito
   - O que funcionou
   - O que NÃO funcionou (se houver)
   - Próximo passo específico
   - Referências importantes

3. **Mostrar Tokens Usados:**
   ```
   📊 Tokens usados nesta conversa: X / 190.000
   ```

### 4.3 Template de Handoff

```markdown
## HANDOFF PARA PRÓXIMA CONVERSA

**Data:** 2024-11-20
**Fase:** 1 - Transport Layer

### O que foi feito:
- [x] Implementado TcpClient com ConnectAsync()
- [x] Implementado TcpServer com AcceptClientsAsync()
- [x] Connection handshake (SYN/ACK)
- [x] Heartbeat system (ping/pong)

### Arquivos criados:
- XL4Net.Shared/Transport/Packet.cs
- XL4Net.Client/Transport/TcpClient.cs
- XL4Net.Server/Transport/TcpServer.cs

### Decisões tomadas:
- Handshake usa magic number 0x584C344E ("XL4N")
- Heartbeat interval: 1 segundo
- Timeout: 5 segundos sem heartbeat

### Problemas encontrados:
- Nenhum

### Próximo passo EXATO:
Implementar UdpClient e UdpServer seguindo mesma estrutura do TCP.

Arquivo: XL4Net.Client/Transport/UdpClient.cs

Método inicial:
```csharp
public class UdpClient
{
    private UdpSocket _socket;
    
    public async Task<bool> ConnectAsync(string host, int port)
    {
        // TODO: Implementar
    }
}
```

Referência Fishnet: 
FishNet/Runtime/Transporting/Transports/Tugboat/Client/ClientSocket.cs
Linhas 45-120 (UDP connection logic)

### Atenção especial:
- UDP não tem "connection", então handshake é diferente
- Cliente envia SYN, aguarda SYN-ACK
- Se não recebe em 3 segundos, retry (máx 3 tentativas)
```

---

## 5. SISTEMA DE TOKENS

### 5.1 Monitoramento

**TODA mensagem de Claude DEVE terminar com:**

```
---
📊 **Tokens usados nesta conversa:** ~X / 190.000
```

### 5.2 Alertas

- **0-100k tokens:** ✅ Continue normalmente
- **100k-150k tokens:** ⚠️ Planeje finalização em breve
- **150k-170k tokens:** ⚠️ Prepare handoff
- **>170k tokens:** 🚨 Finalize AGORA e crie nova conversa

### 5.3 Exemplo

```
---
📊 **Tokens usados nesta conversa:** ~85.000 / 190.000
```

ou

```
---
📊 **Tokens usados nesta conversa:** ~155.000 / 190.000
⚠️ **Aviso:** Considere iniciar nova conversa em breve
```

---

## 6. GESTÃO DE CONHECIMENTO

### 6.1 Documentos Mestres (SEMPRE presentes)

Esses documentos são a "bíblia" do projeto:

```
docs/
├── 00-ARCHITECTURE.md          ← Visão completa
├── 01-CODING-STANDARDS.md      ← Padrões de código
├── 02-PROJECT-STATE.md          ← Estado atual (VIVO)
└── 03-WORKFLOW.md               ← Este documento
```

**Claude SEMPRE lê todos antes de começar.**

### 6.2 Documentos de Fase

Cada fase tem seu documento detalhado:

```
docs/phases/
├── PHASE-01-TRANSPORT.md        ← Setup + TCP/UDP
├── PHASE-02-AUTH.md             ← AuthServer
├── PHASE-03-GAMESERVER.md       ← GameServer básico
├── PHASE-04-PREDICTION.md       ← Client prediction
├── PHASE-05-RECONCILIATION.md   ← Server reconciliation
├── PHASE-06-AOI.md              ← Interest management
├── PHASE-07-MULTISERVER.md      ← Multi-server
├── PHASE-08-OPTIMIZATION.md     ← Performance
└── PHASE-09-DOCUMENTATION.md    ← Docs finais
```

**Claude lê o documento da fase atual antes de trabalhar.**

### 6.3 PROJECT-STATE.md (Documento Vivo)

Este documento é **ATUALIZADO CONSTANTEMENTE**:

- Marca tarefas concluídas ✅
- Lista arquivos criados
- Documenta decisões
- Indica próximo passo

**Formato:**

```markdown
## FASE ATUAL

**FASE 1: Transport Layer** (em andamento)

### Progresso:
- [x] Setup VS2022
- [x] TCP client/server
- [ ] UDP client/server ← PRÓXIMO
- [ ] Reliable UDP
- [ ] Pooling

### Arquivos criados:
- XL4Net.Client/Transport/TcpClient.cs
- XL4Net.Server/Transport/TcpServer.cs

### Próximo passo:
Implementar UdpClient.cs
```

---

## 7. CONFIGURAÇÃO DO VISUAL STUDIO 2022

### 7.1 Guia Passo-a-Passo Completo

**IMPORTANTE:** Claude deve guiar TODO o processo de configuração do VS2022, incluindo:

1. Criar Solution
2. Adicionar projetos
3. Configurar target frameworks
4. Adicionar referências entre projetos
5. Instalar pacotes NuGet
6. Configurar build order
7. Verificar que tudo compila

**Exemplo de instruções:**

```
PASSO 1: Criar Solution

1. Abra Visual Studio 2022
2. Clique em "Create a new project"
3. Procure por "Blank Solution"
4. Nome: XL4Net
5. Location: [escolha onde salvar]
6. Clique "Create"

PASSO 2: Adicionar projeto Shared

1. Clique com botão direito na Solution
2. Add → New Project
3. Procure "Class Library"
4. Nome: XL4Net.Shared
5. Framework: .NET Standard 2.1
6. Clique "Create"

[Continue com instruções detalhadas...]
```

**Claude NÃO deve assumir que você sabe fazer algo. TUDO deve ser explicado.**

---

## 8. BOAS PRÁTICAS

### 8.1 Antes de Implementar

**Claude SEMPRE:**
1. Explica o conceito
2. Mostra analogia (se aplicável)
3. Mostra código de referência (Fishnet)
4. Pergunta se ficou claro
5. Implementa

### 8.2 Durante Implementação

**Claude SEMPRE:**
- Comenta o código em português
- Explica decisões técnicas
- Valida se está seguindo os padrões
- Testa mentalmente edge cases

### 8.3 Após Implementação

**Claude SEMPRE:**
- Resume o que foi feito
- Indica como testar
- Aponta possíveis melhorias futuras
- Pergunta se há dúvidas

---

## 9. PERGUNTAS FREQUENTES

### 9.1 "Claude esqueceu o contexto?"

**Solução:** Inicie nova conversa com o template de início, Claude vai ler todos os docs e recuperar contexto.

### 9.2 "Claude não explicou direito"

**Solução:** Pergunte diretamente:
- "Explica de novo com exemplo mais simples"
- "Por que você fez assim?"

### 9.3 "Preciso mudar algo que já foi decidido"

**Solução:** 
1. Discuta com Claude
2. Atualize ARCHITECTURE.md
3. Documente decisão em PROJECT-STATE.md
4. Continue

### 9.4 "Claude está fazendo algo diferente do planejado"

**Solução:**
- Lembre Claude de ler ARCHITECTURE.md
- Aponte a seção relevante
- Realinhe o trabalho

---

## 10. CHECKLIST DE QUALIDADE

Antes de commitar código, verifique:

- [ ] Segue naming conventions (CODING-STANDARDS.md)
- [ ] Comentários em português
- [ ] Logs em inglês
- [ ] Error handling apropriado
- [ ] Pools sendo usados (se aplicável)
- [ ] Sem alocações desnecessárias
- [ ] Async/await correto
- [ ] Sem warnings
- [ ] PROJECT-STATE.md atualizado

---

## 11. EXEMPLOS DE CONVERSAS

### 11.1 Conversa Inicial (Fase 1)

```
Você: Olá! Vou iniciar o XL4Net. Leia os docs e vamos começar
      a Fase 1.

Claude: [lê documentos]
        Entendi! Vamos começar pela Fase 1 - Transport Layer.
        
        Primeiro, vou explicar o que faremos:
        
        Vamos implementar TCP e UDP client/server baseado no 
        Fishnet Tugboat. [explicação detalhada]
        
        Pronto para começar o setup do Visual Studio?

Você: Sim!

Claude: [guia passo-a-passo do VS2022]
```

### 11.2 Conversa Continuação (Fase 1)

```
Você: Olá! Continuando o XL4Net.

      AÇÕES OBRIGATÓRIAS:
      1. Leia docs/00-ARCHITECTURE.md
      2. Leia docs/01-CODING-STANDARDS.md
      3. Leia docs/02-PROJECT-STATE.md
      4. Leia docs/phases/PHASE-01-TRANSPORT.md
      
      CONTEXTO:
      - Fase 1 - Transport
      - TCP já implementado
      
      OBJETIVO:
      Implementar UDP client/server

Claude: [lê documentos]
        Perfeito! Vi que você já tem TCP funcionando.
        
        Agora vamos para UDP. Deixa eu explicar as diferenças...
        [explicação]
        
        Pronto para começar?
```

---

## 12. TROUBLESHOOTING

### 12.1 Claude não está seguindo os padrões

**Problema:** Claude escreveu logs em português

**Solução:**
```
"Claude, lembre-se: logs devem ser em INGLÊS de acordo com 
CODING-STANDARDS.md seção 5.2"
```

### 12.2 Claude pulou explicação

**Problema:** Claude implementou sem explicar

**Solução:**
```
"Espera, não entendi. Pode explicar primeiro o que é Reliable UDP
antes de implementar?"
```

### 12.3 Código não compila

**Problema:** Erro de compilação

**Solução:**
1. Mostre o erro para Claude
2. Claude analisa e corrige
3. Explica o que causou o erro

---

**FIM DO DOCUMENTO**

Versão 1.0 - 2024-11-20

Este workflow garante continuidade e qualidade no desenvolvimento do XL4Net através de múltiplas conversas com Claude.
