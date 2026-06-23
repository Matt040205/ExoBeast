# Sprint 4 — Limpeza e Polimento (Otimização Multiplayer)

> **Status**: 📋 Planejado — pronto para execução por agentes Opus 4.7+
> **Tempo total estimado**: 3-5 horas (paralelizável em 6 agentes)
> **Risco geral**: 🟢-🟡 Baixo a médio (auditorias e refactors localizados)
> **Pré-requisitos**: Sprints 1, 2 e 3 mergeadas e validadas em build

## Contexto

As Sprints 1-3 atacaram os **grandes ofensores** de banda e CPU em multiplayer:
- Sprint 1: Quick Wins — NetworkVariable churn, Debug.Log em hot paths, permissões de leitura
- Sprint 2: CPU Server-Side — `FindGameObjectsWithTag`, `Physics.OverlapSphere` allocs
- Sprint 3: Refactors — EOS double-tick, SearchLobbies cache, PlayerMovement compactado, spatial partitioning

Sprint 4 é diferente: **a maioria dos itens é auditoria, não refactor cego**. São limpezas pontuais que individualmente economizam pouco (1-5% por item), mas em conjunto:
- Eliminam **GC pressure** (alocações por frame em hot paths)
- Reduzem **latência percebida** em UI de lobby (debounce)
- Removem **RPCs com corpo vazio** (ruído puro na rede)
- Auditam **broadcast desnecessário** de VFX/SFX/CameraShake

Após Sprint 4, o sistema multiplayer deve estar pronto para **profilagem real** com Unity Network Profiler em playtest com 4 jogadores.

## Itens nesta sprint

| Ordem | ID | Arquivo principal | Tempo | Risco |
|---|---|---|---|---|
| 1 | **G6** | `PlayerHealthSystem.cs` | ~10 min | 🟢 |
| 2 | **G7** | `CommanderAbilityController.cs` | ~15 min | 🟢 |
| 3 | **E6** | `MatchManager.cs` | ~5 min | 🟢 |
| 4 | **A6** | `LobbyManager.cs` | ~30 min | 🟡 |
| 5 | **E7** | múltiplos (auditoria) | ~1-2h | 🟡 |
| 6 | **V1** | múltiplos (auditoria) | ~1-2h | 🟡 |

## Ordem de execução recomendada

### Onda 1 — Quick wins independentes (paralelo)
Itens com risco 🟢 que tocam arquivos diferentes. Podem rodar em **3 agentes simultâneos**:

- **G6** → `PlayerHealthSystem.cs` (Agent A)
- **G7** → `CommanderAbilityController.cs` (Agent B)
- **E6** → `MatchManager.cs` (Agent C)

⏱ Janela: 10-15 min em paralelo. Build limpo após cada um.

### Onda 2 — Debounce de lobby (sequencial após onda 1)
- **A6** → `LobbyManager.cs`
  - Risco 🟡 médio porque toca arquivo na lista de frágeis (ver `01_padroes.md`)
  - Mas modifica APENAS `SetMemberAttribute` — método independente, similar ao A3 da Sprint 3
  - Designar 1 agente dedicado

⏱ Janela: 30-45 min.

### Onda 3 — Auditorias paralelas (após onda 2)
Ambos são **auditorias com discovery**: agente investiga arquivos múltiplos antes de mudar.
Pode rodar 2 agentes em paralelo:

- **E7** → audit de `[ClientRpc]` em `Assets/Codigo/Characters/` e `Assets/Codigo/Abilities/`
- **V1** → audit de `JuiceManager`, `CameraShakeManager`, `UINotificationManager`

⏱ Janela: 1-2h por agente. Resultado primeiro = plano de mudanças, segundo = aplicar mudanças.

## Para todos os agentes

### Pré-leitura obrigatória (NÃO pular)
1. `01_padroes.md` (este diretório) — padrões NGO usados, arquivos frágeis, convenções
2. `~/.claude/projects/.../memory/MEMORY.md` — bug history, regras de ouro
3. O guide específico do seu item (`02_G6_*.md` etc.)

### Regras de ouro
- **NUNCA** tocar em arquivos fora dos listados no seu item sem reportar primeiro
- Edits de método isolado preferíveis a refactors estruturais
- Se algo parecer inconsistente com o guide, **abortar e reportar** — o código pode ter mudado desde a escrita do plano
- Validar build com `dotnet build PI3D.sln` antes de marcar item como completo
- Comentário `OPTIMIZATION (Sprint 4 / Item XX - 2026-MM-DD)` em todo bloco modificado
- Commit message format: `Sprint 4 / XX: <descricao curta>` (sem emojis, sem co-authors)

### Não fazer
- ❌ Tocar `PlayerNetworkSetup` ou `FinishLocalSetupNextFrame` (ver MEMORY.md "Bug Recorrente Movimento")
- ❌ Modificar ordem de habilitar componentes em ciclo de spawn
- ❌ Adicionar `using` de namespace experimental sem verificar dependências
- ❌ Renomear símbolos públicos (apenas privados/internos)
- ❌ Fazer commit sem build limpo

## Critério de aceite global da Sprint 4

- [ ] Build limpo (0 erros, 52 warnings pré-existentes OK)
- [ ] 6 itens marcados como completed
- [ ] Memory atualizado com `optimization_sprint_4.md` resumindo as mudanças
- [ ] Sem regressão funcional: criar lobby + entrar partida + combate ativo + voltar menu = OK
- [ ] Network Profiler (se Unity Editor disponível) mostra:
  - Sem deltas de NetworkVariable a cada frame em players parados
  - Sem ClientRpc vazios em logs do servidor
  - SetMemberAttribute calls reduzidas em UI hesitante (>250ms entre clicks)

## Format de report ao orquestrador (template)

Cada agente, ao completar seu item, retorna:

```
Item: <id>
Status: completed | aborted | partial
Arquivos modificados: <lista>
Build: PASS (0 erros, X warnings) | FAIL
Validação in-game: PASS | NOT_RUN | FAIL
Métrica medida (se aplicável): <antes/depois>
Riscos detectados: <lista ou "nenhum">
Próximo item liberado: true (<id>) | false (motivo)
Notas para próximo agente: <se relevante>
```

## Próximos passos após Sprint 4

1. **Profilagem real** com Unity Network Profiler em playtest 4 jogadores (cena `CenaMapaTeste`)
2. **Coletar baseline** de bytes/s outbound do host em wave de 30 inimigos
3. **Decidir Sprint 5** (se necessário): prováveis candidatos:
   - Compactação de NetworkVariables maiores (struct → bits)
   - Snapshot interpolation customizada para inimigos
   - Pool de RPCs ou batched updates
   - LOD de network (clientes longe recebem updates a 10Hz, perto a 30Hz)

## Referências cruzadas

- Plan original: `~/.claude/plans/vamos-fazer-um-plano-distributed-nebula.md`
- Sprint 1+2 doc: `optimization_sprints_1_2.md` (memory)
- Sprint 3 doc: removido após conclusão (~05/2026)
- Padrões NGO: `01_padroes.md` (este diretório)
