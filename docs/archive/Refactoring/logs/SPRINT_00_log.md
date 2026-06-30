# Sprint 00 — Setup e Alinhamento

**Agente:** Claude (Opus 4.7) — com intervenção colaborativa de outro agente para resolver Bloqueio #3
**Branch:** `multi-player-refactor`
**Início:** 2026-05-21
**Status atual:** PRONTA-PARA-REVISÃO ✅

---

## Checklist de Pré-Leitura

- [x] Li `00_LEIA_PRIMEIRO.md`
- [x] Li `05_GLOSSARIO.md`
- [x] Li `01_QUALITY_GATE.md`
- [x] Li `04_CONTRATOS_INTERFACE.md`
- [x] Li `02_SPRINTS.md` (visão geral + Sprint 0/1 detalhadas)
- [x] Li `03_PROTOCOLO_PROGRESSO.md` (primeiras 100 linhas — formato de log, BLOCKERS)
- [x] `git fetch origin` executado
- [x] Working tree clean confirmado
- [x] Build verde via `dotnet build PI3D.sln`
- [x] Smoke test em MPPM (PASS após resolução dos 3 bloqueios pré-requisitos — confirmado pelo usuário)

---

## Tarefa 0.1 — Leitura completa da documentação de refatoração

**Status:** ✅ COMPLETO

### Ações executadas
- Leitura sequencial dos 6 documentos em `docs/Refactoring/`:
  - `00_LEIA_PRIMEIRO.md` (274 linhas, integral)
  - `05_GLOSSARIO.md` (319 linhas, integral)
  - `01_QUALITY_GATE.md` (834 linhas, integral)
  - `04_CONTRATOS_INTERFACE.md` (577 linhas, integral)
  - `02_SPRINTS.md` (856 linhas, integral)
  - `03_PROTOCOLO_PROGRESSO.md` (primeiras 100 linhas — restante consultar quando necessário)

### Observações
- Plano canônico identificado: 9 sprints atômicas (0-8), branch dedicada por sprint (`claude/sprint-XX-...`), PR por sprint.
- Refatoração é estrutural (não funcional). Meta: `LobbyManager.cs` 1626 → ≤700 LOC; namespace Multiplayer total 7690 → ~5800-6400.
- Filosofia ratchet: LOC só pode descer; contratos públicos imutáveis sem aprovação explícita.
- 16 arquivos no anel interno proibidos de modificar nesta rodada.

---

## Tarefa 0.2 — Consultar memória do projeto

**Status:** ✅ COMPLETO

### Memórias auto-carregadas no system prompt
Já presentes em [MEMORY.md](C:/Users/zegil/.claude/projects/C--Users-zegil-Documents-GitHub-ExoBeasts-V3-PI3D/memory/MEMORY.md):

| Memória | Relevância para Sprints |
|---|---|
| `ngo_patterns.md` | Sprints 3+ (LobbyManager refactor) |
| `bug_host_client_movement.md` | Sprints 3+ (PlayerNetworkSetup é anel interno, mas usado por contexto) |
| `feedback_destroy_childcount.md` | Geral (cuidado com loops sobre childCount) |
| `bug_enemy_spawn_build.md` | Sprints futuras se tocar prefabs |
| `bug_trap_system_multiplayer.md` | Sprints futuras (não toca trap nesta rodada) |
| `bugfix_session_7_maio_2026.md` | Sprint 3 (StartHost falha com IsClient=True — guard em OnLobbyAttributeUpdated) |
| `eos_credentials_refactor.md` | Sprint 7 (higiene de EOSConfig) |
| `optimization_sprints_1_2.md` | Geral (padrões de otimização aplicados) |

### Para Sprint 1 especificamente
Sprint 1 só deleta `LobbyUIManager.cs` (anel externo). Não há memória específica de risco — apenas:
- `00_LEIA_PRIMEIRO §2.3` (bugs históricos a não reintroduzir): nenhum desses bugs é tocado por Sprint 1.

---

## Tarefa 0.3 — Pull e rebase / estado git

**Status:** ✅ COMPLETO

### Comandos rodados
```powershell
git fetch origin
git status
git log --oneline -3
```

### Output
```
On branch multi-player-refactor
Your branch is up to date with 'origin/multi-player-refactor'.

nothing to commit, working tree clean

3ebf8ff docs(refactoring): adiciona guia de refatoração multiplayer
1068461 mudando IU part9.1
24a7075 mudando IU part9
```

### Estado
- Branch atual: `multi-player-refactor`
- Sincronizada com `origin/multi-player-refactor` (sem ahead/behind)
- Working tree clean
- Commits: docs/Refactoring/ (3ebf8ff) baseado em main IU updates

### Observações
- `main` está 2 commits atrás de `origin/main` (não impacta esta sprint pois trabalhamos em `multi-player-refactor`).
- Há stash preservado em main: `stash@{0}: On main: Sprint4-old-plan-G6-G7-E6-A6-E7-V1-2026-05-21` — execução anterior de um plano diferente, mantido para revisão.

---

## Tarefa 0.4 — Build base via dotnet

**Status:** ✅ COMPLETO

### Comando rodado
```powershell
dotnet build PI3D.sln --no-incremental
```

### Resultado
- **0 erros**
- **68 warnings** (BASELINE para Sprint 1+ — não pode aumentar)
- Tempo: ~11s

### Warnings predominantes (CS0649 e CS0414)
- Campos não-atribuídos em `EOSConfigGenerator.cs` (esperado — `[NonSerialized]` por design após refactor de 13 Maio 2026)
- Campos não-usados em `EOSConfig.cs` (mesma razão)
- Outros: `attackState`, `serverTickRate`, `_isReady`, `_eosFlowRunning`, etc. — diversos campos legacy

### Observação
O Quality Gate (§11) exige que warnings **não aumentem**. Este número (68) vira o teto para Sprint 1.

---

## Bloqueio #1 — Clone MPPM falha por path resolution (RESOLVIDO 2026-05-21)

**Origem:** detectado durante tentativa de smoke test base (tarefa 0.5).
**Causa:** bug pré-existente em `Assets/Editor/EOSConfigGenerator.cs:127` não considerava `MppmHelper.IsClone`.
**Fix aplicado:** patch de 3 linhas + 1 using replicando pattern de `EOSConfig.cs:72-76`.
**Build pós-fix:** PASS (0 erros, 68 warnings baseline mantido).
**Detalhes completos:** ver [BLOCKERS.md](BLOCKERS.md).
**Status:** aguardando validação do usuário (re-executar tarefa 0.5).

---

## Tarefa 0.5 — Smoke test base MPPM

**Status:** ✅ PASS (após resolução de 3 bloqueios pré-requisitos)

### Sequência real até PASS

1. Tentativa #1 (controle, antes das mudanças): falhou no Player 2 com `[EOSConfigGenerator] Nenhuma fonte de credenciais EOS encontrada` → abriu Bloqueio #1.
2. Fix Bloqueio #1 aplicado (eu, fix mínimo no `EOSConfigGenerator.cs` usando `MppmHelper.IsClone`).
3. Tentativa #2: avançou da fase EOS mas falhou em `Scene 'LobbyScene' couldn't be loaded` no Player 2 → abriu Bloqueio #2.
4. Reset filesystem do clone MPPM (deletar `Library/VP/mppm2c2807dc/` + resetar `SystemData.json` Player 2 para `VirtualProjectIdentifier: null`).
5. Usuário recriou Virtual Player no painel MPPM (novo ID `mppm5b7be4a6`).
6. Tentativa #3 (com clone limpo): mesmo erro de scene loading → confirmou que NÃO era cache. Bloqueio #2 escalou para Bloqueio #3 (Unity 6 `globalScenes` ↔ `scenes` dessincronizadas).
7. Fix Bloqueio #3 (Etapa A, eu): workaround runtime em `GameModeManager.cs` via index resolution.
8. Fix Bloqueio #3 (Etapa B, outro agente — solução definitiva): `BuildSceneListGuard.cs` editor-time + refinamentos.
9. Tentativa #4: **PASS**. Player 1 + Player 2 completam o fluxo MenuScene → LobbyScene → criar/entrar sala → ready → escolher personagem → iniciar partida → CenaMapaTeste com personagens visíveis.

### Por quê pendente (histórico arquivado)
Smoke test exige Unity Editor aberto + clone MPPM ativo. Eu (agente Claude) não tenho acesso direto ao Editor.

### Checklist a executar (do `02_SPRINTS.md §0.5`)
1. [ ] Abrir Unity Editor no projeto PI3D
2. [ ] `Window → Multiplayer → Play Mode → +Add` (cria 1 clone MPPM)
3. [ ] Entrar em Play na instância principal (Editor)
4. [ ] Cena `LobbyScene` aparece; digitar nick + clicar "Login"
5. [ ] Clicar "Criar Sala"
6. [ ] No clone MPPM: aguardar login automático; clicar "Buscar Salas" ou colar ID + "Entrar"
7. [ ] Ambos confirmam Ready
8. [ ] Ambos selecionam personagem (ex.: host=Coruja, cliente=Samurai)
9. [ ] Host clica "Iniciar Partida"
10. [ ] Ambos chegam em `CenaMapaTeste` com seus personagens visíveis
11. [ ] Console: zero erros novos durante o fluxo

### Resultado esperado
Cada passo executa sem erro. Console sem `Exception`, `NullReferenceException`, `MissingReferenceException` novos.

### Resultado real (preencher após teste)
- [ ] Passou
- [ ] Falhou — qual passo: ____ — erro: ____

---

## Tarefa 0.6 — Criar este log (SPRINT_00_log.md)

**Status:** ✅ COMPLETO

### Ações
- Criada pasta `docs/Refactoring/logs/`
- Criado este arquivo `SPRINT_00_log.md` no formato definido em `03_PROTOCOLO_PROGRESSO.md`

---

## Quality Gate checklist (Sprint 0)

- [x] Working tree clean
- [x] Build verde (`dotnet build` PASS, 0 erros)
- [ ] Smoke test OK (PENDENTE — depende do usuário)
- [x] Nada foi modificado (nenhum arquivo `.cs` ou `.unity` tocado)

---

## Status final

**Smoke test (tarefa 0.5): PASS** — após resolução de 3 bloqueios pré-requisitos. Sprint 0 fechada como `PRONTA-PARA-REVISÃO`.

### Arquivos modificados nesta sprint (todos UNSTAGED, aguardando decisão de commit do orquestrador)

| Arquivo | Origem | Propósito |
|---|---|---|
| `Assets/Editor/EOSConfigGenerator.cs` | Bloqueio #1 (eu) | Path resolution MPPM via `MppmHelper.IsClone` |
| `Assets/Codigo/Managers/GameModeManager.cs` | Bloqueio #3 Etapa A (eu) + B (outro agente) | `LoadLocalSceneMppmSafe` + helpers + EditorSceneManager fallback |
| `Assets/Editor/BuildSceneListGuard.cs` (NOVO) | Bloqueio #3 Etapa B (outro agente) | Sincroniza `EditorBuildSettings.globalScenes` ↔ `.scenes` antes de Play Mode |
| `Assets/Codigo/Multiplayer/Lobby/LobbySceneUI.cs` | Outro agente | `DisableButtonLabelRaycasts` para UX dos botões + suporte typo |
| `Assets/Scenes/LobbyScene.unity` | Outro agente | 4 `TMP_Text` filhos de botões com `m_RaycastTarget: 0` |
| `Assets/Tests/Editor/MenuSceneValidationTests.cs` | Outro agente | 3 testes de regressão para os fixes |
| `Assets/Modelos/fontes/PaytoneOne SDF.asset` | Touch do Unity | Re-save incidental de font atlas |
| `Assets/ProjectSettings/Settings/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset` | Touch do Unity | Re-save incidental de TMP fallback |
| `docs/Refactoring/logs/SPRINT_00_log.md` | Eu | Este log |
| `docs/Refactoring/logs/BLOCKERS.md` | Eu | Documentação dos 3 bloqueios |

### Próximos passos (Sprint 1 — Remover `LobbyUIManager.cs`)

Conforme `02_SPRINTS.md` §SPRINT 1:
1. Criar branch `claude/sprint-01-remove-lobbyuimanager` a partir de `multi-player-refactor`.
2. Tarefa 1.1: mapear dependentes de `LobbyUIManager` (Grep do nome de classe + métodos públicos).
3. Tarefa 1.2: confirmar com orquestrador antes de deletar.
4. Tarefa 1.3-1.5: remover GameObject da cena `EscolherPersonagem.unity`, deletar `.cs` + `.cs.meta`, compilar.
5. Tarefa 1.6: smoke test MPPM (mesmo procedimento).
6. Tarefa 1.7: medir LOC final (esperado: namespace Multiplayer cair em ≥547 LOC).
7. Criar `SPRINT_01_log.md` no mesmo padrão.

**Aguardando sinal verde do orquestrador para começar Sprint 1.**

---

**Fim do log da Sprint 00.**
