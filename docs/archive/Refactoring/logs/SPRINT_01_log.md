# Sprint 01 — Hotfix MPPM Scene Resolution + Remoção do `LobbyUIManager`

**Agente:** Claude (Opus 4.7)
**Branch:** `claude/sprint-01-hotfix-and-remove-lobbyuimanager` (criada a partir de `multi-player-refactor`)
**Início:** 2026-05-21
**Status atual:** PRONTA-PARA-REVISÃO ⚠ — aguarda smoke test do usuário (MPPM + ideal: build standalone)

> **Histórico:** este log começou como investigação pura (sem código) após bug descoberto no smoke test pós-Sprint 0. Após autorização do usuário, evoluiu para execução combinada de duas frentes: (a) hotfix do bug investigado, (b) Sprint 1 formal (Remover `LobbyUIManager`).

---

## Resumo executivo

**Bug reportado pelo usuário (smoke test pós-Sprint 0):**
> No Multiplayer Play Mode, quando o host clica em "Iniciar Partida", o cliente NÃO é direcionado para a tela `EscolherPersonagem`.

**Conclusão da investigação:**

| Pergunta | Resposta |
|---|---|
| Ocorre em build? | **Improvável** — bug tem assinatura MPPM-specific (ver §3) |
| Causa raiz (hipótese principal) | NGO Scene Resolution falha no clone MPPM ao tentar resolver `"EscolherPersonagem"` em runtime |
| Causa raiz (hipótese alternativa) | Cliente não conecta via NGO antes do host carregar a cena |
| Próxima ação sugerida | Coletar logs do cliente MPPM com `Develop > Console > Show Multiplayer Logs` durante o bug |
| Correção aplicada nesta sprint? | **NÃO** — investigação apenas, conforme instrução explícita do usuário |

---

## Tarefa 1.1 — Mapeamento do fluxo StartMatch host → cliente

**Status:** ✅ COMPLETO

### Sequência verificada no código

**Host (Player 1):**
1. Clica em `BtnIniciarPartida` → `LobbySceneUI.cs:426 IniciarPartida()` → `_lobby.StartMatch()`
2. `LobbyManager.cs:803 StartMatch(mapOverride=null)` — valida `_currentLobby.hostProductUserId == myUid`
3. `LobbyManager.cs:833 StartMatchCoroutine`:
   - Linha 910–915 **`#if UNITY_EDITOR`**: MPPM usa IP direto `127.0.0.1` (sem Relay)
   - Linha 916–967 **else** (build): chama Relay via UGS
   - Linha 991: `nm.StartHost()` — host NGO sobe
   - Linha 1026–1027: publica `SERVER_ADDRESS` e `SERVER_PORT` no lobby EOS
   - Linha 1028: publica `LOBBY_STATE = InGame`
   - Linha 1042: dispara `WaitForAllClientsAndLoadScene(sceneName="EscolherPersonagem", expectedPlayerCount=N)`
4. `WaitForAllClientsAndLoadScene` (linha 1085) aguarda `nm.ConnectedClientsIds.Count >= expectedPlayerCount` (timeout 25s)
5. Quando todos conectam: `nm.SceneManager.LoadScene("EscolherPersonagem", LoadSceneMode.Single)` (linha 1157)

**Cliente (Player 2):**
1. Recebe `OnLobbyAttributeUpdated` (LobbyManager.cs:1335)
2. Filtros: `GameModeManager.CurrentMode == Multiplayer`, `_isInLobby`, `info.LobbyId == _currentLobby.lobbyId`
3. `ProcessLobbyAttributes` (linha 1386):
   - Valida que não é o host EOS
   - Valida que NGO já não está conectado
   - Lê `LOBBY_STATE` → deve ser `InGame` ou `Starting`
   - Lê `RELAY_CODE` → se usable, conecta via Relay
   - Senão, fallback `SERVER_ADDRESS`/`SERVER_PORT` → `ConnectClientCoroutine` (linha 1483) → `nmClient.StartClient()`
4. Após conectar, fica aguardando o `OnSceneEvent(Load)` do NGO disparado pelo host
5. NGO entrega Scene Load → cliente faz `SceneManager.LoadSceneAsync` internamente

---

## Tarefa 1.2 — Verificação OnLobbyAttributeUpdated

**Status:** ✅ COMPLETO

### Fluxo validado (LobbyManager.cs:1335–1475)

O cliente em MPPM lê:
- `LOBBY_STATE` = `"InGame"` ✓ (publicado pelo host na linha 1028)
- `RELAY_CODE` = `NO_RELAY_CODE` em MPPM (porque host está em `#if UNITY_EDITOR`)
- Cai no fallback `SERVER_ADDRESS = "127.0.0.1"` + `SERVER_PORT` (linhas 1453–1474)
- Chama `ConnectClientCoroutine` (linha 1471)

**Conclusão**: o caminho de detecção e conexão NGO está íntegro. Não há razão estrutural para o cliente falhar nesta fase.

### Guard MPPM já presente no código

O fluxo tem **defesas dedicadas a MPPM**:
- Linha 904–908: re-atribui `NetworkConfig.NetworkTransport` via `GetComponent` se for `null` (referência serializada quebrada no clone)
- Linha 1513–1517: mesma guarda no cliente

Isso sugere que o autor original ja sabia que MPPM tem peculiaridades.

---

## Tarefa 1.3 — Pontos de divergência MPPM vs Build

**Status:** ✅ COMPLETO

### Divergências mapeadas no fluxo de StartMatch

| Local | MPPM (Editor) | Build |
|---|---|---|
| `LobbyManager.cs:910–915` | IP direto `127.0.0.1`, sem Relay | Relay obrigatório via UGSBootstrap |
| `LobbyManager.cs:1023` | Publica `RELAY_CODE = NO_RELAY_CODE` | Publica `RELAY_CODE = relayJoinCode` real |
| `LobbyManager.cs:1431–1450` (cliente) | Pula Relay, usa fallback SERVER_ADDRESS | Conecta via Relay |
| `GameModeManager.cs:147–172` | Tem `TryLoadSceneInEditorPlayMode` (fallback Editor) | Esse método nem compila |
| `BuildSceneListGuard.cs` | **Roda em ExitingEditMode** | **Não existe** (é `Assets/Editor/`, descartado no build) |
| `EOSConfigGenerator.cs:127` | Lê `EOSCredentials.json` do projeto raiz (com fix MPPM) | Lê via Resources / env vars |

### Onde a transição pode quebrar (e em quais contextos)

| Ponto de falha | Sintoma | MPPM? | Build? |
|---|---|---|---|
| Cliente nunca recebe `OnLobbyAttributeUpdated` | Cliente fica no LobbyScene sem log | ⚠ Possível | ⚠ Possível |
| Cliente lê `LOBBY_STATE != InGame` | Filtrado, ignorado silenciosamente | ⚠ Possível | ⚠ Possível |
| `StartClient` falha (transporte) | Log: `StartClient retornou false` | ⚠ Possível | ⚠ Possível |
| Host carrega cena antes do cliente conectar | Cliente pega `OnSceneEvent` tarde, talvez perdido | ⚠ Possível | ⚠ Possível |
| **NGO SceneManager.LoadScene rejeita nome no cliente** | **Cliente fica preso, watchdog 15s volta ao Lobby** | ✅ **Muito provável** | ❌ Improvável (cenas no .exe) |
| `SceneManager.LoadSceneAsync` interno do NGO falha no clone | Cliente: erro "scene not in shared list" | ✅ **Muito provável** | ❌ Não aplicável |

---

## §3 — Hipótese principal: NGO Scene Resolution no clone MPPM

### Por que essa hipótese tem o maior peso

**Contexto do Blocker #3 (Sprint 0):**
- Já comprovamos que Unity 6 MPPM tem bug onde `SceneManager.LoadScene(name)` falha em clones porque o clone usa `EditorBuildSettings.globalScenes` (NEW Build Profiles) que pode dessincronizar de `.scenes` (CLASSIC).
- Fix do Blocker #3 (`BuildSceneListGuard.cs`) sincroniza ambas as listas no projeto **original** antes de `ExitingEditMode`.

**Por que isso pode persistir no fluxo NGO:**
1. O `BuildSceneListGuard.cs:23 [InitializeOnLoad]` roda no domínio Editor. Em MPPM, o clone também tem domínio Editor próprio, então o hook deveria disparar **no clone também**.
2. **MAS** o clone tem `Library/` separado — Unity pode estar lendo metadata de cenas de uma cache que não foi atualizada pelo guard.
3. **E** `AssetDatabase.SaveAssets()` (linha 82 de BuildSceneListGuard) pode falhar silenciosamente no clone se o asset estiver read-only via virtual project.
4. NGO 1.12 internamente, quando o cliente recebe Scene Load, chama `SceneManager.LoadSceneAsync(name_ou_index)`. Se a resolução falha no clone, o erro genérico do Unity 6 aparece: `Scene 'X' couldn't be loaded because it has not been added to the active build profile or shared scene list`.

### O sintoma que confirma essa hipótese

Se essa hipótese estiver correta, o **console do Player 2 (clone) durante o bug** deve mostrar:
- Mensagens normais de `[LobbyManager] Conectando via IP: 127.0.0.1:xxxx`
- `StartClient retornou: true`
- Mensagens do `SceneTransitionHandler` mostrando `LoadingScreenUI.Show()`
- **Erro `Scene 'EscolherPersonagem' couldn't be loaded`** após a transição começar
- 15s depois: `[SceneTransitionHandler] Watchdog detectou loading preso` → volta ao Lobby

### O sintoma que **refutaria** essa hipótese

Se o cliente NÃO entrar em loading e ficar visualmente no LobbyScene sem alteração, então a falha é antes do NGO Scene Load — provavelmente uma das outras hipóteses (cliente não conecta, lobby attribute não chega).

---

## §4 — Resposta direta às perguntas do usuário

### "Esse comportamento também ocorre na build ou apenas no Multiplayer Play Mode?"

**Análise estrutural sem evidência empírica:**

- **Se a causa raiz for NGO Scene Resolution (Hipótese §3):** ❌ **Build NÃO afetado.**
  - Em build, `EditorBuildSettings.globalScenes` e `.scenes` não existem. A lista de cenas é resolvida em build-time e incluída no `.exe`. Não há "shared scene list" para dessincronizar.
  - `BuildSceneListGuard.cs` está em `Assets/Editor/` — não compila em build.
  - Esse vetor de falha é **exclusivo de MPPM**.

- **Se a causa raiz for outra (cliente não conecta, watchdog timeout, etc.):** ⚠ **Build PODE estar afetado.**
  - Cenários como timeout EOS, falha Relay, ou desconexão durante a transição são neutros entre MPPM e build.

**Recomendação de teste seguro (sem mudar código):**
1. Build standalone do projeto na branch atual (já tem todos os fixes do Sprint 0)
2. Rodar 2 instâncias da build em máquinas diferentes (ou na mesma)
3. Host inicia partida; verificar se cliente acompanha
4. Se cliente acompanha em build mas não em MPPM → confirma hipótese §3 (MPPM-specific)
5. Se cliente NÃO acompanha em build → bug é mais profundo, requer fix em LobbyManager/NGO setup

### "Vamos tomar bastante cuidado para não quebrar nada na build"

**Garantias atuais (estado do código):**
- Os arquivos modificados no Sprint 0 são:
  - `BuildSceneListGuard.cs` → **Editor-only, não afeta build**
  - `EOSConfigGenerator.cs` → **Editor-only (gera arquivo de credenciais), não afeta build**
  - `GameModeManager.cs` → `LoadLocalSceneMppmSafe` usa `#if UNITY_EDITOR` para o `TryLoadSceneInEditorPlayMode`, mas o caminho principal (`SceneUtility.GetBuildIndexByScenePath` + `SceneManager.LoadScene(buildIndex)`) também funciona em build, com mesma semântica do código antigo
  - `LobbySceneUI.cs` → mudanças apenas na UI (raycast, AutoDetect typo) — neutras
  - `LobbyScene.unity` → mudança apenas de `raycastTarget` em TMP_Text — neutra
- **Conclusão**: nenhum dos fixes do Sprint 0 introduz regressão em build.

---

## §5 — Mapeamento de arquivos relevantes (referência para próximo passo)

### Arquivos do fluxo (somente leitura nesta sprint)

| Arquivo | Linhas-chave | Responsabilidade |
|---|---|---|
| `Assets/Codigo/Multiplayer/Lobby/LobbyManager.cs` | 803 (`StartMatch`), 833 (coroutine), 1085 (`WaitForAllClients`), 1157 (`LoadScene`), 1335 (`OnLobbyAttributeUpdated`), 1386 (`ProcessLobbyAttributes`), 1483 (`ConnectClientCoroutine`) | Coração do fluxo |
| `Assets/Codigo/Multiplayer/Lobby/LobbySceneUI.cs` | 426 (`IniciarPartida`) | Trigger do host |
| `Assets/Codigo/Managers/Loading/SceneTransitionHandler.cs` | 116 (`OnSceneEvent`), 217 (`StartLoadingWatchdog`), 232 (`LoadingWatchdogCoroutine`) | Watchdog 15s |
| `Assets/Codigo/Managers/GameModeManager.cs` | 101 (`LoadSceneSafe`), 122 (`LoadLocalSceneMppmSafe`) | Resolução de cena em modo offline (NÃO usado neste fluxo, pois `IsNetworkSession=true`) |
| `Assets/Editor/BuildSceneListGuard.cs` | 23 (`[InitializeOnLoad]`), 47 (`TryEnsureCanonicalScenes`), 102 (`OnPlayModeStateChanged`) | Sincronização Editor-time das listas de cena |

### Cenas envolvidas

| Cena | Build Index | NetworkManager presente? | EnableSceneManagement |
|---|---|---|---|
| `MenuScene.unity` | 0 | ✅ Sim (instancia inicial) | 1 (true) |
| `EscolherPersonagem.unity` | 1 | ❌ Não (recebe via DDOL) | — |
| `LobbyScene.unity` | 2 | ❌ Não (recebe via DDOL) | — |
| `CenaMapaTeste.unity` | 4 | ✅ Sim | 1 (true) |

**Insight**: o NetworkManager nasce em `MenuScene` (ou `CenaMapaTeste`) e persiste via DontDestroyOnLoad. Em `LobbyScene`, é o NetworkManager herdado de MenuScene que está vivo. Isso é importante porque a config `EnableSceneManagement=1` vem do prefab/instância de `MenuScene`.

---

## §6 — Próxima ação recomendada (NÃO executada)

Conforme instrução do usuário ("não aplique essa correção ainda, apenas investigue"), **nenhuma correção foi aplicada nesta sprint**.

### Caminho de investigação adicional (sugestão para o usuário decidir)

**Passo 1 — Coletar evidência empírica (sem código):**
1. Abrir Player 2 (clone MPPM) — abrir Console
2. Habilitar `Develop > Show Multiplayer Logs` (NGO verbose)
3. Reproduzir o bug
4. Salvar logs completos
5. Compartilhar com a investigação para decidir entre Hipótese §3 e alternativas

**Passo 2 — Validar em build (opcional):**
1. Fazer build standalone
2. Rodar host + cliente da build
3. Verificar se mesmo bug ocorre
4. Resultado define escopo do fix

**Passo 3 — Quando autorizada a correção:**
- Se Hipótese §3 confirmada: estender `BuildSceneListGuard` para forçar reload do clone (talvez via `AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate)`) OU adicionar um VerifySceneBeforeLoading customizado no NGO SceneManager OU pré-validar cenas em runtime
- Se cliente não conecta: investigar timeout/retry no `ConnectClientCoroutine`
- Se watchdog dispara: investigar evento `OnSceneEvent` no clone

### Princípios a respeitar no fix (do `01_QUALITY_GATE.md`)

- **Anel interno** (não tocar sem aprovação): `LobbyManager.cs` e `GameModeManager.cs` estão na lista de arquivos protegidos. Mexer neles exige autorização explícita do usuário e justificativa documentada.
- **Anel externo** (8 arquivos por sprint): qualquer fix deve caber em até 8 arquivos.
- **Ratchet**: o fix não pode introduzir warnings novos nem deixar o estado pior.
- **Smoke test**: fix só vale quando MPPM + build passam o smoke test base.

---

## §7 — Sobre o conceito de "Sprint 1"

⚠ **Nomenclatura ambígua a resolver:**

O usuário chamou o trabalho atual de "Sprint 1", mas no plano formal (`02_SPRINTS.md`) a Sprint 1 é:
> **Sprint 1 — Remover `LobbyUIManager.cs`** (anel externo: 1 arquivo deletado, 0 arquivos modificados, ~150 linhas removidas)

A investigação atual NÃO se encaixa nessa definição. Ela é uma **investigação de bug** descoberta durante o smoke test do Sprint 0.

**Opções para o usuário:**
1. **Tratar como Sprint 1-investigação** (caminho atual): mantém este log, marca como sprint não-formal de bugfix. Sprint 1 formal (Remover LobbyUIManager) seria renumerada para Sprint 2.
2. **Tratar como continuação do Sprint 0**: este log se integra ao SPRINT_00 como apêndice de bugs descobertos pós-aceite.
3. **Tratar como Hotfix paralelo**: criar `HOTFIX_NN_log.md` separado, manter Sprint 1 formal intacta para depois.

**Recomendação minha**: Opção 3. Bugs descobertos pós-aceite são esperados — manter o plano formal limpo e ter logs de hotfix paralelos é mais escalável.

---

## §8 — Critérios de Saída desta investigação

- [x] Fluxo StartMatch host→cliente mapeado linha a linha
- [x] OnLobbyAttributeUpdated do cliente analisado
- [x] Divergências MPPM vs Build documentadas
- [x] Hipótese principal identificada com plano de validação
- [x] Resposta direta às perguntas do usuário
- [x] Mapa de arquivos relevantes para próxima ação
- [ ] Logs reais do clone MPPM durante o bug coletados (depende do usuário — agora opcional pós-fix)
- [ ] Teste em build executado (depende do usuário — recomendado pós-fix)
- [x] Correção aplicada (ver §9 abaixo — usuário autorizou em 2026-05-21)

---

## §9 — Execução combinada (Hotfix + Sprint 1 formal) — autorizada em 2026-05-21

Após o usuário aprovar 3 decisões via `AskUserQuestion`:
- ✅ Tocar `LobbyManager.cs` (anel interno) para o fix de scene resolution
- ✅ Editar cenas via Unity MCP
- ✅ Branch única combinada

Esta sprint passou a executar **dois trabalhos paralelos** no mesmo branch:

### 9.1 Hotfix A — `BuildSceneListGuard.cs` (Editor-only)

**Arquivo:** `Assets/Editor/BuildSceneListGuard.cs`

**Mudanças aplicadas:**
1. Adicionado `AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate)` após `SaveAssets()` em `TryEnsureCanonicalScenes`. Justificativa: o clone MPPM tem `Library/` separado e pode não re-ler `ProjectSettings/EditorBuildSettings.asset` após o `SaveAssets`. O `Refresh` força o clone a re-importar.
2. Adicionado hook `EnteredPlayMode` (em adição ao `ExitingEditMode` já existente) — apenas para **diagnóstico**: detecta e loga DRIFT quando o clone entra em Play Mode com listas de cena dessincronizadas. Não altera estado, só evidencia o problema.

**Risco em build:** ZERO — arquivo em `Assets/Editor/`, descartado pelo build pipeline.

### 9.2 Hotfix B — `LobbyManager.cs` (anel interno, autorizado)

**Arquivo:** `Assets/Codigo/Multiplayer/Lobby/LobbyManager.cs`

**Mudanças aplicadas em `WaitForAllClientsAndLoadScene`:**

1. **Pré-validação da cena no host** (linhas ~1157-1175 novas): antes de chamar `nm.SceneManager.LoadScene(sceneName, ...)`, valida que `SceneUtility.GetBuildIndexByScenePath` retorna ≥ 0 para a cena alvo. Se não, aborta com erro claro instruindo o usuário a executar `Tools > ExoBeasts > Repair Build Scene List`.

2. **`VerifySceneBeforeLoading` callback registrado no NGO** (linha ~1180-1184 novas): cada peer (host e clientes) chama este callback antes de carregar uma cena via mensagem NGO. O callback loga `sceneIndex`, `sceneName`, `resolvedPath`, `isResolvable`. Se a cena não resolve no peer atual, retorna `false` — aborta o load do lado do peer com erro claro em vez de deixar o Unity falhar silenciosamente.

3. **Método estático novo `OnVerifySceneBeforeLoading`** (linhas ~1170-1195 novas): implementação do callback acima.

**Risco em build:** BAIXO — o callback é registrado dinamicamente em runtime e o caminho do código compilado em build é idêntico ao do Editor. O log adicional não afeta hot path.

### 9.3 Sprint 1 formal — Remover `LobbyUIManager.cs`

**Decisão estendida pelo usuário (escopo estendido em ~~3 → 4~~ → 5 arquivos):** incluir `MenuScene.unity` na limpeza, porque o GameObject `LobbyUIManager` também aparecia lá (descoberto no mapeamento de dependentes 1.1).

#### Mapeamento de dependentes (1.1)

| Local | Conteúdo |
|---|---|
| `Assets/Codigo/Multiplayer/Lobby/LobbyUIManager.cs` | Arquivo do componente (639 LOC) — a ser deletado |
| `Assets/Codigo/Multiplayer/Lobby/LobbyUIManager.cs.meta` | Meta (2 LOC) — a ser deletado |
| `Assets/Scenes/MenuScene.unity` | GameObject vestigial `LobbyUIManager` (51 linhas serializadas, sem listeners onClick apontando para ele) |
| `Assets/Scenes/EscolherPersonagem.unity` | GameObject `LobbyUIManager` + 3 listeners onClick em botões (AlterarMaxPlayers x2, FecharPainelMultiplayer x1) |
| `Assets/Codigo/Docs/Estado_Atual_Multiplayer.md` | Menções históricas (docs) — atualização opcional, não bloqueia Sprint |
| `Assets/Codigo/Docs/Guia_Setup_Multiplayer_Cenas.md` | Mesma natureza |
| `Assets/Tests/Editor/MenuSceneValidationTests.cs` | Assertion que valida que botões **NÃO** chamam `AbrirPainelMultiplayer` — continua válido pós-remoção |

#### Execução

| Tarefa | Como foi feito | Resultado |
|---|---|---|
| Deletar GameObject em MenuScene | Unity MCP `manage_gameobject action=delete` | ✅ Funcionou no primeiro try; diff git: -51 linhas |
| Deletar GameObject em EscolherPersonagem | Unity MCP `manage_gameobject action=delete` — **falhou** após save (Unity reverteu por motivo desconhecido). Re-tentativa também falhou. | ⚠ Fallback necessário |
| Fallback EscolherPersonagem | Edit direto no `.unity` YAML — removidos: bloco GameObject+Transform+MonoBehaviour, 3 listeners onClick (substituídos por `m_Calls: []`), 1 entrada em `SceneRoots` | ✅ |
| Deletar `LobbyUIManager.cs` + `.meta` | `rm -f` | ✅ |
| Validar build | `dotnet build PI3D.sln --no-incremental` | ✅ 0 erros, 67 warnings (1 a menos que baseline porque `_eosFlowRunning` foi removido junto) |

> **Por que execute_code do Unity MCP falhou:** mesmo bug do `mono.exe` Windows PATH já encontrado no Sprint 0 ("O nome do arquivo ou a extensão é muito grande"). Workaround usado: edit direto no YAML serializado. **Risco mitigado:** todas as edições foram validadas via `Grep` final que retornou zero referências residuais nas cenas.

### 9.4 Diff agregado da sprint

| Arquivo | Δ Linhas | Tipo |
|---|---|---|
| `Assets/Codigo/Multiplayer/Lobby/LobbyUIManager.cs` | **-639** | Deletado |
| `Assets/Codigo/Multiplayer/Lobby/LobbyUIManager.cs.meta` | **-2** | Deletado |
| `Assets/Codigo/Multiplayer/Lobby/LobbyManager.cs` | +59 | Hotfix B (VerifySceneBeforeLoading + pré-validação) |
| `Assets/Editor/BuildSceneListGuard.cs` | +~25 | Hotfix A (Refresh + diagnostic hook) — já contava do Sprint 0, mas Sprint 1 amplia |
| `Assets/Scenes/MenuScene.unity` | -51 | GameObject LobbyUIManager removido |
| `Assets/Scenes/EscolherPersonagem.unity` | -93 (líquido) | GameObject + 3 listeners + 1 SceneRoots entry removidos |

**Total líquido removido na sprint: ~-700 LOC** (vs meta do plano: -547 LOC só do `LobbyUIManager.cs`).

### 9.5 Quality Gate checklist

- [x] Build verde (`dotnet build PI3D.sln --no-incremental` → 0 erros, 67 warnings — **menos 1 que baseline**)
- [x] Nenhum arquivo do anel interno foi tocado sem autorização explícita do usuário
- [x] Sprint não tocou em `04_CONTRATOS_INTERFACE.md` (LobbyUIManager não era contrato listado)
- [x] Nenhum comentário `audit`/`REGRA DE OURO`/`OPTIMIZATION` foi removido
- [x] Nenhum bug de §2.3 do `00_LEIA_PRIMEIRO.md` reapareceu (sintaticamente; runtime smoke test pendente)
- [x] Cenas limpas: `Grep` em ambas as cenas retorna zero referências a LobbyUIManager
- [ ] Smoke test MPPM completo executado (depende do usuário — fluxo MenuScene → LobbyScene → EscolherPersonagem → CenaMapaTeste)
- [ ] **Smoke test build standalone executado (recomendado pelo usuário para garantir não-regressão)**

### 9.6 Protocolo de rollback

Caso o smoke test falhe:

```powershell
# Reverter tudo na branch atual
git checkout multi-player-refactor -- Assets/Codigo/Managers/GameModeManager.cs
git checkout multi-player-refactor -- Assets/Codigo/Multiplayer/Lobby/LobbyManager.cs
git checkout multi-player-refactor -- Assets/Codigo/Multiplayer/Lobby/LobbySceneUI.cs
git checkout multi-player-refactor -- Assets/Editor/EOSConfigGenerator.cs
git checkout multi-player-refactor -- Assets/Scenes/EscolherPersonagem.unity
git checkout multi-player-refactor -- Assets/Scenes/LobbyScene.unity
git checkout multi-player-refactor -- Assets/Scenes/MenuScene.unity
git checkout multi-player-refactor -- Assets/Tests/Editor/MenuSceneValidationTests.cs

# Restaurar arquivos deletados
git checkout multi-player-refactor -- Assets/Codigo/Multiplayer/Lobby/LobbyUIManager.cs
git checkout multi-player-refactor -- Assets/Codigo/Multiplayer/Lobby/LobbyUIManager.cs.meta

# Remover arquivos novos
rm -f Assets/Editor/BuildSceneListGuard.cs Assets/Editor/BuildSceneListGuard.cs.meta
```

### 9.7 Próxima ação esperada do usuário

1. **Smoke test MPPM** (obrigatório): rodar Player 1 + Player 2 clone. Host inicia partida, validar que cliente acompanha até EscolherPersonagem e CenaMapaTeste.
2. **Smoke test build** (recomendado): build standalone, rodar 2 instâncias, validar mesmo fluxo. Garante que o fix em `LobbyManager.cs` não regrediu o caminho de produção (Relay).
3. Se ambos passarem: aprovar PR/merge para `multi-player-refactor`.
4. Se algum falhar: reverter via §9.6, coletar logs do clone (sintoma deveria estar diferente agora com VerifySceneBeforeLoading logando), nova rodada.

### 9.8 Notas para o próximo agente

- A `MEMORY.md` do projeto ainda menciona o `LobbyUIManager` em alguns locais (Estado_Atual_Multiplayer.md, Guia_Setup_Multiplayer_Cenas.md). Atualização desses arquivos é cosmética e pode ser feita em Sprint posterior (não bloqueia).
- O fix `OnVerifySceneBeforeLoading` em `LobbyManager.cs` é um callback **estático** registrado via instance method da classe. Se em sprints futuras o `LobbyManager` for fragmentado (Sprint 3 — extrair `MatchSessionLauncher`), esse callback deve migrar junto.
- O comentário `// BUG FIX (2026-05-21)` está em 3 arquivos (`GameModeManager`, `LobbyManager`, `BuildSceneListGuard`, `EOSConfigGenerator`). Esses são âncoras documentais — não remover sem ler o contexto histórico.
