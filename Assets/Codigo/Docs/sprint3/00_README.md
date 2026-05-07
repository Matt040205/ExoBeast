# Sprint 3 — Refactors Estruturais (Otimização Multiplayer)

> **Status**: Pronto para execução por agentes Opus 4.7. Orquestração e validação por agente principal (zegil@anthropic).

## Contexto

Este sprint dá continuidade ao trabalho de Sprints 1 e 2 (já mergeados em `main`,
documentados em `memory/optimization_sprints_1_2.md`). Os Sprints anteriores
foram **quick wins** (acumuladores locais, throttling, gates client-side).
Sprint 3 é mais arriscado: toca em zonas com histórico de bugs (`PlayerMovement`,
`EnemyPoolManager`) e requer validação in-game cuidadosa.

**Premissa**: cada item é executado por um agente diferente (ou pelo mesmo agente
em PRs separados). Cada agente lê o guide específico do seu item + o guide de
padrões compartilhados (`01_padroes.md`) ANTES de começar. Cada item gera seu
próprio commit (preferencialmente sua própria PR pra rollback isolado).

**Plano original**: `C:\Users\zegil\.claude\plans\vamos-fazer-um-plano-distributed-nebula.md`
(reproduzido em sumário neste README — leitura completa do plano original NÃO é necessária).

## Itens do Sprint (ordem obrigatória de execução)

A ordem foi escolhida pra **escalar do risco baixo ao alto**, e pra que o estado
do servidor/cliente esteja estável antes dos refactors mais agressivos.

| # | Item | Guide | Risco | Tempo estimado | Pré-requisito |
|---|---|---|---|---|---|
| 1 | **A2** — EOSManagerWrapper double-tick | `02_A2_eos_double_tick.md` | 🟢 Baixo | ~30 min | Nenhum |
| 2 | **A3** — LobbyManager.SearchLobbies rate-limit | `03_A3_lobby_search_rate_limit.md` | 🟢 Baixo | ~1h | Nenhum |
| 3 | **E5** — EnemyPoolManager validar reuso de NetworkObject | `04_E5_enemy_pool_reuse.md` | 🟡 Médio | ~2-3h | A2+A3 mergeados |
| 4 | **G3** — PlayerMovement NetworkVariables redundantes | `05_G3_player_movement_networkvars.md` | 🔴 Alto | ~2-3h + bastante teste | E5 mergeado |
| 5 | **E3p2** — Spatial partitioning para targeting | `06_E3p2_spatial_partitioning.md` | 🟡 Médio | ~1 dia | G3 mergeado |

**NÃO PULAR ORDEM** sem confirmação do orquestrador. A ordem reflete dependências
de estabilidade (G3 mexe em movement; se quebrar e E5 também tiver mudado, fica
difícil isolar a regressão).

## Como cada agente reporta progresso ao orquestrador

Após terminar um item:

1. **Build limpo obrigatório**: `dotnet build PI3D.sln` retorna `0 Erro(s)`. 52 warnings
   pré-existentes são OK; não introduzir novos warnings.
2. **Sumário em formato fixo** (postar no chat de orquestração):
   ```
   Item: <ID, ex: A2>
   Status: completed | blocked | aborted
   Arquivos modificados: <lista relativa, ex: Assets/Codigo/Multiplayer/Core/EOSManagerWrapper.cs>
   Build: PASS (0 erros, 52 warnings) | FAIL (...)
   Validação in-game: NOT_RUN | PASS | FAIL (...)
   Métrica medida: <ex: "EOS Tick: 2x/frame → 1x/frame confirmado via UnityProfiler">
   Riscos detectados: <lista>
   Próximo item liberado para execução: <true|false>
   ```
3. **Se `blocked`**: descrever exatamente o que está bloqueando. Não improvisar.
4. **Se `aborted`** (regressão funcional ou risco descoberto): rollback imediato
   (`git stash` ou `git revert`), reportar ao orquestrador.

## Critérios globais de aceitação

Após o sprint inteiro:
- **Banda outbound do host**: redução ≥ 50% em combate ativo (medido via Network Profiler
  do Unity em wave de 30 inimigos + 8+ torres + 3 jogadores MPPM por 60s).
- **CPU servidor em wave grande**: redução ≥ 30% em "Server Tick" (Profiler CPU).
- **Sem regressão funcional**: TODOS os bugs já corrigidos em sessões anteriores
  (movimento host/cliente, traps multiplayer, abilities, regeneração de HP, ultimate
  charge, aggro indicator, etc.) continuam funcionando.

## Critérios globais de stop/rollback

**Pare imediatamente e reverta** se durante a execução de qualquer item:

- Movimento do host quebrar (não anda, anda errado, fica preso).
- Cliente não-host não consegue conectar OU conecta mas é desconectado em < 30s.
- Spawn de inimigos parar (HordeManager não inicia wave).
- ServerRpc/ClientRpc começar a logar warnings de ownership ou serialization.
- Build deixar de compilar e a correção não for trivial (< 5 min).

Em qualquer caso acima: `git stash` (ou `git checkout <arquivo>`) + reportar ao orquestrador.

## Arquivos compartilhados (LEITURA OBRIGATÓRIA antes de qualquer item)

1. `01_padroes.md` — padrões NGO + ferramentas + convenções de código + lista de
   arquivos frágeis (intocáveis sem aprovação).
2. `Assets/Codigo/Docs/Estado_Atual_Multiplayer.md` — fonte canônica do multiplayer.
3. `Assets/Diretrizes_Multiagente.md` — contrato entre agentes (preservar trabalho
   alheio, não usar docs históricas como fonte).
4. `memory/optimization_sprints_1_2.md` (em `~/.claude/projects/.../memory/`) —
   contexto dos Sprints 1+2 já feitos (padrões reutilizáveis estabelecidos).

## Orquestrador — papel após mergeado

Após cada item mergeado pelo executor:
1. Validar localmente com Unity Editor + MPPM.
2. Atualizar `memory/MEMORY.md` (linha-índice na Status das Fases).
3. Liberar próximo item ao próximo agente.

## Referências cruzadas

- Plano original (motivação, achados detalhados): `~/.claude/plans/vamos-fazer-um-plano-distributed-nebula.md`
- Sprints 1+2 detalhe: `memory/optimization_sprints_1_2.md`
- Bug history em zonas frágeis:
  - `memory/bug_host_client_movement.md` — relevante para G3
  - `memory/bug_trap_system_multiplayer.md` — relevante para E5
  - `memory/bug_enemy_spawn_build.md` — relevante para E5
- Padrões NGO consolidados: seção "Padrão NGO" do `memory/MEMORY.md`
