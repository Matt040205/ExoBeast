# 03 — Protocolo de Progresso, Logs e Correção

> Como cada agente registra seu trabalho e como o orquestrador interpreta os logs para decidir avançar, bloquear ou corrigir.
> Este protocolo é **obrigatório**. Sprints sem log no formato definido aqui são marcadas como "não-conformes" e refeitas.

---

## 1. Estrutura de pastas de log

Todos os logs vivem em:

```
docs/Refactoring/logs/
  ├── SPRINT_00_log.md         ← um arquivo por sprint
  ├── SPRINT_01_log.md
  ├── SPRINT_02_log.md
  ├── ...
  └── BLOCKERS.md              ← índice global de bloqueios ativos
```

**Convenções:**

- Um único arquivo de log por sprint. Múltiplos agentes (em rounds de correção) **anexam** ao mesmo arquivo — não criam novos.
- Logs ficam versionados em git, no mesmo branch da sprint. Cada commit relevante atualiza o log.
- `BLOCKERS.md` é o índice de "questões abertas" — entradas são adicionadas e removidas conforme bloqueios surgem e são resolvidos.

---

## 2. Formato do log de sprint

Cada arquivo `SPRINT_NN_log.md` segue este template:

```markdown
# Sprint NN — <Título da sprint>

**Agente:** <nome ou identificador do agente que iniciou>
**Branch:** <branch git em que a sprint está sendo executada>
**Início:** <ISO 8601 datetime, ex.: 2026-05-20T14:30:00-03:00>
**Status atual:** <PRÉ-LEITURA | EM-PROGRESSO | BLOQUEADA | PRONTA-PARA-REVISÃO | APROVADA | REVERTIDA>

---

## Checklist de Pré-Leitura

(Marcado antes de qualquer mudança de código.)

- [ ] Li `00_LEIA_PRIMEIRO.md`
- [ ] Li `05_GLOSSARIO.md`
- [ ] Li `01_QUALITY_GATE.md`
- [ ] Li `04_CONTRATOS_INTERFACE.md`
- [ ] Li a Sprint NN inteira em `02_SPRINTS.md`
- [ ] Li este `03_PROTOCOLO_PROGRESSO.md`
- [ ] `git pull origin main` e rebase do branch
- [ ] Working tree clean
- [ ] Build verde no Unity Editor antes de mudanças
- [ ] Smoke test em MPPM passa ANTES das mudanças (controle)

---

## Tarefa NN.1 — <título da tarefa>

**Início:** <datetime>
**Critério de aceitação:** <copiar da sprint>

### Ações executadas
- <ação 1>
- <ação 2>
- ...

### Arquivos tocados
| Arquivo | LOC antes | LOC depois | Tipo de mudança |
|---|---:|---:|---|
| `Assets/...` | 1626 | 1480 | Refator: extraído método X |

### Comandos rodados
```
<comando exato>
<resultado/output relevante>
```

### Build
- Status: ✅ verde / ❌ vermelho com erro: `<erro>`
- Warnings novos: 0 / +1: `<warning>`

### Smoke test
- Cenário testado: <descrição>
- Resultado: ✅ pass / ❌ fail (com detalhes)

### Critério de aceitação atingido
- [x] item 1 do critério
- [x] item 2
- ...

### Observações
- <qualquer coisa relevante>

---

## Tarefa NN.2 — ...
(repetir bloco acima)

---

## Bloqueios encontrados nesta sprint

(Se houver. Caso contrário, seção vazia ou "Nenhum".)

### Bloqueio #1 — <título curto>

**Tipo:** <CONTRATO | TÉCNICO | AMBIENTE | ESCOPO | OUTRO>
**Quando:** <timestamp>
**Descrição:**
- <o que esperava>
- <o que encontrou>

**Tentativas:**
1. <o que foi tentado>
2. <resultado>

**Aguardando resposta de:** orquestrador

**Resolução:** (preencher quando resolvido)
- <decisão>
- <ação tomada>

---

## Encerramento da sprint

**Datetime final:** <ISO>
**Status final:** <PRONTA-PARA-REVISÃO | REVERTIDA>

### Métricas finais
- LOC do `LobbyManager.cs`: <antes> → <depois> (Δ <delta>)
- Arquivos deletados: <lista>
- Arquivos criados: <lista>
- Total de tarefas executadas: <N>
- Total de bloqueios: <N>

### Quality Gate checklist
(Itens aplicáveis à sprint, marcados pelo agente para conferência do orquestrador.)

- [x] Nenhuma assinatura pública de `04_CONTRATOS_INTERFACE.md` foi modificada
- [x] LOC do `LobbyManager` **só diminuiu** (ratchet)
- [x] Nenhum arquivo novo > 500 LOC
- [x] Build verde sem warnings novos
- [x] Smoke test MPPM passa
- [x] Nenhuma diretiva `#if !EOS_DISABLE` removida
- [x] Comentários `audit`/`REGRA DE OURO`/`OPTIMIZATION`/`SYNC-FIX` preservados
- [x] Nenhum bug histórico (00_LEIA_PRIMEIRO.md §2.3) reintroduzido
- [x] Esta sprint não tocou em arquivos fora do seu escopo

### PR aberto
- URL: <link>
- Diff resumido: +N -M LOC, X arquivos
- Description: <resumo do que mudou>

### Solicitação ao orquestrador
- [ ] Revisar este log
- [ ] Aprovar PR
- [ ] Autorizar Sprint <N+1>
```

---

## 3. Anatomia de uma entrada por tarefa

Para cada tarefa atômica dentro da sprint, o agente preenche o bloco da `## Tarefa NN.M` no formato acima. Regras:

### 3.1 Granularidade

- **Uma tarefa por bloco.** Não agrupe.
- Se a sprint listou 8 tarefas, devem aparecer 8 blocos `## Tarefa N.X` no log.
- Se durante execução surgir sub-tarefa, registrar como `### Sub-tarefa N.X.a` dentro do bloco principal.

### 3.2 Antes-e-depois

- **LOC antes/depois sempre medido**, mesmo que não seja a métrica principal.
- Use o snippet PowerShell:
  ```powershell
  (Get-Content path/to/file.cs | Measure-Object -Line).Lines
  ```

### 3.3 Comandos rodados

- Copiar o **comando exato** e a saída relevante (truncar saída para até 30 linhas; se mais, referenciar log externo).
- Inclui `git status`, `git diff --stat`, builds, smoke tests.

### 3.4 Status visuais

- ✅ = passou / sucesso
- ❌ = falhou / erro
- ⚠️ = passou com ressalva (warning, atenção)
- 🚫 = bloqueado, aguardando orquestrador

### 3.5 Não inventar números

- Se uma métrica não pôde ser medida, escrever **"não medido"** com justificativa.
- Nunca aproximar.

---

## 4. Sinalizando bloqueios

Bloqueio = qualquer situação onde o agente **não pode prosseguir sem decisão externa**.

### 4.1 Tipos de bloqueio

| Tipo | Quando usar | Quem resolve |
|---|---|---|
| **CONTRATO** | Sprint exige mudar assinatura listada em `04_CONTRATOS_INTERFACE.md` | Orquestrador (pode aprovar ou negar) |
| **TÉCNICO** | Comportamento do código contradiz o esperado (ex.: smoke test falha por motivo desconhecido) | Orquestrador (pode pedir investigação adicional) |
| **AMBIENTE** | Setup local não está como esperado (Unity não compila, MPPM não funciona) | Orquestrador (pode pedir clean install ou pull) |
| **ESCOPO** | Tarefa parece exigir tocar em arquivo fora do escopo | Orquestrador (pode expandir escopo ou criar sub-sprint) |
| **OUTRO** | Qualquer coisa não enquadrável | Orquestrador |

### 4.2 Processo de bloqueio

Quando detectar um bloqueio:

1. **Pare imediatamente.** Não tente "consertar tentando coisas aleatórias".
2. **Reverta** mudanças não comitadas que dependiam do estado em bloqueio:
   ```powershell
   git stash
   ```
3. **Registre** no log da sprint conforme template §2 (seção "Bloqueio #N").
4. **Adicione entrada em `BLOCKERS.md`** (índice global):
   ```markdown
   ## [ABERTO] Sprint NN — <título curto>
   Aberto em: <datetime>
   Agente: <nome>
   Link: docs/Refactoring/logs/SPRINT_NN_log.md#bloqueio-1
   ```
5. **Atualize status da sprint** para `BLOQUEADA`.
6. **Aguarde resposta.** Não pule para próxima tarefa "enquanto isso".

### 4.3 Quando o bloqueio é resolvido

- Orquestrador edita o bloco "Bloqueio #N" no log da sprint, preenchendo "Resolução".
- Orquestrador atualiza `BLOCKERS.md` movendo a entrada de `[ABERTO]` para `[RESOLVIDO]` com data.
- Agente retoma a sprint, registra a retomada no log:
  ```
  ### Retomada após bloqueio #1
  Datetime: <ISO>
  Decisão: <resumo da decisão do orquestrador>
  Próxima tarefa: NN.M
  ```

---

## 5. Como o orquestrador interpreta o log

O orquestrador lê o log para decidir uma de cinco ações: **avançar**, **bloquear**, **pedir esclarecimento**, **corrigir**, **reverter**.

### 5.1 Sinais de "avançar"

Todos verdadeiros ⇒ aprovar PR e autorizar próxima sprint:
- Checklist de pré-leitura completo.
- Todas as tarefas da sprint registradas com `Critério de aceitação atingido`.
- Build verde, sem warnings novos.
- Smoke test passou.
- LOC do `LobbyManager` igual ou menor (se sprint era refactor).
- Quality Gate checklist todo ✅.
- Nenhum item de `04_CONTRATOS_INTERFACE.md` mudou sem aprovação.

### 5.2 Sinais de "pedir esclarecimento"

- Tarefa com critério "parcialmente atingido" sem justificativa.
- LOC mediu mas conta não fecha (ex.: alegou remover 547 LOC mas diff só mostra -200).
- Smoke test descrito como "✅" mas sem detalhes do cenário.
- Comentário `audit` removido sem justificativa.

**Ação:** comentar no log da sprint pedindo detalhe. Não marcar como aprovada até receber resposta.

### 5.3 Sinais de "corrigir" (sprint volta para o agente)

- Build com warning novo.
- Critério de aceitação fail.
- Smoke test falhou (parcialmente).
- Sprint tocou em arquivo fora do escopo (mesmo que com boa intenção).

**Ação:** orquestrador adiciona seção `## Correções Solicitadas` no log:
```markdown
## Correções Solicitadas — Round 1
Datetime: <ISO>
Solicitante: <orquestrador>

- [ ] Reverter mudanças em `Assets/Codigo/X.cs` (fora do escopo da Sprint NN)
- [ ] Restaurar comentário `audit` na linha Y de `Z.cs`
- [ ] Re-rodar smoke test e anexar resultado

Status: AGUARDANDO-AGENTE
```

Agente (mesmo ou novo) abre o log, atende cada item, anexa nova seção:
```markdown
## Atendimento Round 1
Datetime: <ISO>
Agente: <nome>

- [x] Mudanças em `X.cs` revertidas (commit <sha>)
- [x] Comentário restaurado (commit <sha>)
- [x] Smoke test re-rodado: ✅ pass (detalhes: ...)
```

### 5.4 Sinais de "reverter"

- Bug histórico (`00_LEIA_PRIMEIRO.md §2.3`) reintroduzido.
- Sistema multiplayer não conecta mais após mudanças.
- Build não compila e a sprint não tem capacidade de consertar (>1h tentando).
- Contrato de interface quebrado sem autorização.

**Ação:** orquestrador faz `git revert <commits da sprint>`, atualiza log com `Status: REVERTIDA`, e abre nova sprint corrigindo o approach.

---

## 6. Template de relatório de início de sprint

Quando um agente assume uma sprint, o **primeiro commit** no branch da sprint deve incluir `docs/Refactoring/logs/SPRINT_NN_log.md` com **apenas o cabeçalho preenchido**, sinalizando "estou começando":

```markdown
# Sprint NN — <Título>

**Agente:** <nome>
**Branch:** claude/<nome-sprint>
**Início:** <ISO>
**Status atual:** PRÉ-LEITURA

---

## Checklist de Pré-Leitura
- [ ] ...
```

Após terminar a pré-leitura, o agente atualiza:
```
**Status atual:** EM-PROGRESSO
```
E preenche a primeira tarefa.

---

## 7. Template de relatório de conclusão de sprint

Quando todas as tarefas da sprint estiverem com `[x] Critério de aceitação atingido`, agente atualiza o final do log:

```markdown
## Encerramento da sprint

**Datetime final:** <ISO>
**Status final:** PRONTA-PARA-REVISÃO

### Métricas finais
...

### Quality Gate checklist
...

### PR aberto
URL: <link gh pr view>

### Solicitação ao orquestrador
- [ ] Revisar este log
- [ ] Aprovar PR
- [ ] Autorizar Sprint <N+1>
```

E **para de trabalhar**. Não inicia próxima sprint sem autorização.

---

## 8. Protocolo de correção (sprint retorna para nova rodada)

Quando uma sprint volta para correção (§5.3), o ciclo é:

```
1. Orquestrador adiciona "## Correções Solicitadas — Round N"
2. Status da sprint → CORREÇÃO-N
3. Agente (mesmo ou novo) atende
4. Agente adiciona "## Atendimento Round N"
5. Status da sprint → PRONTA-PARA-REVISÃO
6. Orquestrador avalia novamente: aprova ou pede mais correções
```

**Limite:** após 3 rounds de correção sem fechar, o orquestrador considera a sprint **REVERTIDA** e redesenha o escopo.

---

## 9. Quando trocar de agente no meio da sprint

Eventualmente, um agente pode "cair" (timeout, perda de contexto, decisão do orquestrador de mover).

### 9.1 Pré-requisitos para passar a bola

O agente saindo deve garantir que o log da sprint:
- Tem cabeçalho completo.
- Lista todas as tarefas tocadas (mesmo as incompletas).
- Para cada tarefa incompleta, declara **explicitamente** o estado: "INTERROMPIDA: <onde parou>".
- Working tree está limpo OU stash declarado: "Stash criado: `git stash show stash@{0}`".

### 9.2 Como o agente novo entra

O agente novo:
1. Lê o log da sprint inteiro (todos os rounds + bloqueios + atendimentos).
2. Lê `MEMORY.md` e os 6 docs de refatoração (mesmo se já tiver lido em outra sprint — contexto muda).
3. Roda checklist de pré-leitura (mesmo se o anterior rodou).
4. Verifica working tree e git status.
5. Pop stash se houver: `git stash pop`.
6. Adiciona seção no log:
   ```markdown
   ## Passagem de agente
   Datetime: <ISO>
   Agente saindo: <nome anterior>
   Agente entrando: <nome novo>
   Estado herdado: <resumo de onde parou>
   ```
7. Continua a tarefa onde parou.

---

## 10. `BLOCKERS.md` — índice global

Único arquivo. Lista todos os bloqueios em qualquer sprint, abertos e resolvidos. Permite ao orquestrador ver "o que está travado em qualquer ponto do projeto" de uma vez.

Template:

```markdown
# BLOCKERS.md — Índice Global de Bloqueios

## Ativos

### [ABERTO] Sprint 03 — Confirmar remoção de `GameServerManager`
Aberto em: 2026-05-22T10:14:00-03:00
Agente: claude/sprint-03
Tipo: ESCOPO
Resumo: Varredura encontrou 1 referência a `GameServerManager.Instance.IsServerReady()` em `XYZ.cs` (fora do escopo declarado). Como proceder?
Link: docs/Refactoring/logs/SPRINT_03_log.md#bloqueio-2

## Resolvidos

### [RESOLVIDO] Sprint 01 — Referência a `LobbyUIManager` em prefab
Aberto em: 2026-05-21T15:00:00-03:00
Resolvido em: 2026-05-21T16:45:00-03:00
Tipo: TÉCNICO
Resolução: Prefab "PainelMultiplayer.prefab" tinha referência por Inspector. Removida manualmente no Editor antes do delete do .cs.
Link: docs/Refactoring/logs/SPRINT_01_log.md#bloqueio-1
```

---

## 11. Boas práticas para o agente

Para que o orquestrador consiga interpretar rápido:

1. **Seja específico.** "Compilou" vs "Compilou sem warnings novos. 0 errors, 0 warnings em Console" — o segundo é interpretável.
2. **Cite linhas e SHAs.** "Removi LobbyUIManager.cs em commit a3f2c19" > "removi o arquivo".
3. **Anexe diffs curtos.** Se tarefa mexeu em < 20 linhas, copie no log.
4. **Diferencie observação vs ação.** "Reparei que X parece redundante mas NÃO mexi (fora do escopo)" — útil. "Removi X" — exige escopo declarado.
5. **Não decida sozinho em zonas cinzentas.** Quando duvidar se uma mudança está dentro do escopo, bloqueie.

---

## 12. Boas práticas para o orquestrador

Para que o agente saiba o que fazer:

1. **Responda bloqueios em uma única passada.** Múltiplas respostas conflitantes confundem.
2. **Aprovação ou rejeição, não "talvez".** Se em dúvida, peça mais informação no log; depois decida.
3. **Quando rejeitar, dê motivo objetivo.** "Falta evidência de smoke test" > "não convenceu".
4. **Mantenha `BLOCKERS.md` em sync.** Toda decisão sobre bloqueio atualiza o índice global.
5. **Avalie ratchet primeiro.** LOC do `LobbyManager` é o sinal mais barato de progresso. Se subiu, qualquer outra coisa é secundária.

---

## 13. Exemplo curto e completo (sprint fictícia)

```markdown
# Sprint 99 — Renomear constante FOO para BAR

**Agente:** claude/sprint-99
**Branch:** claude/sprint-99
**Início:** 2026-05-22T09:00:00-03:00
**Status atual:** PRONTA-PARA-REVISÃO

## Checklist de Pré-Leitura
- [x] Li `00_LEIA_PRIMEIRO.md`
- [x] Li `05_GLOSSARIO.md`
- [x] Li `01_QUALITY_GATE.md`
- [x] Li `04_CONTRATOS_INTERFACE.md`
- [x] Li Sprint 99 em `02_SPRINTS.md`
- [x] git pull + rebase OK
- [x] Working tree clean
- [x] Build verde antes
- [x] Smoke test base OK

## Tarefa 99.1 — Grep por FOO
**Início:** 09:05
### Ações
- Grep "FOO" em Assets/Codigo

### Comandos
```
> Grep -r "FOO" Assets/Codigo
SomeFile.cs:42:    const string FOO = "foo";
SomeFile.cs:88:    Console.WriteLine(FOO);
```

### Build / Smoke test
- N/A (só leitura)

### Critério de aceitação
- [x] 2 ocorrências localizadas

## Tarefa 99.2 — Renomear
**Início:** 09:10

### Ações
- Edit SomeFile.cs:42 FOO → BAR
- Edit SomeFile.cs:88 FOO → BAR

### Arquivos tocados
| Arquivo | LOC antes | LOC depois |
|---|---:|---:|
| SomeFile.cs | 90 | 90 |

### Build
- ✅ verde, 0 warnings novos

### Smoke test
- ✅ MPPM 2 instâncias, host cria sala, cliente entra, ambos chegam em CenaMapaTeste

### Critério de aceitação
- [x] Compila sem warnings
- [x] Smoke test OK

## Encerramento

**Status final:** PRONTA-PARA-REVISÃO
**LOC LobbyManager:** 1626 → 1626 (sprint não tocou nele)

### Quality Gate checklist
- [x] Sem mudança em assinaturas públicas
- [x] LOC LobbyManager não subiu
- [x] Build verde
- [x] Smoke test ok

### PR aberto
URL: https://github.com/.../pull/123

### Solicitação ao orquestrador
- [ ] Revisar
- [ ] Aprovar
```

---

**Fim do `03_PROTOCOLO_PROGRESSO.md`.**
