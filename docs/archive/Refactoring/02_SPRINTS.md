# 02 — Sprints de Implementação

> Cada sprint é uma unidade atômica de trabalho com escopo fechado, critério de aceitação objetivo e protocolo de rollback definido.
>
> **Execute exatamente uma sprint por vez.** Não pule etapas. Não combine sprints.
>
> Antes de iniciar qualquer sprint, leia `00_LEIA_PRIMEIRO.md`, `01_QUALITY_GATE.md`, `04_CONTRATOS_INTERFACE.md`, `05_GLOSSARIO.md` e `03_PROTOCOLO_PROGRESSO.md`.

---

## Visão geral

| Sprint | Título | Esforço | Risco | Pré-req | LOC esperado (Δ) |
|:---:|---|---|---|---|---|
| 0 | Setup e alinhamento | PEQUENO | Baixo | — | 0 |
| 1 | Remover `LobbyUIManager.cs` | MÉDIO | Baixo | Sprint 0 | -547 |
| 2 | Consolidar bootstrap NGO (remover `HostManager`) | MÉDIO | Médio | Sprint 1 | -100 a -250 |
| 3 | Extrair `MatchSessionLauncher` do `LobbyManager` | GRANDE | Médio | Sprint 2 | LobbyManager -500 / novo +500 |
| 4 | Extrair `LobbyNotificationDispatcher` | MÉDIO | Médio | Sprint 3 | LobbyManager -250 / novo +250 |
| 5 | Extrair `LobbyMembershipService` | MÉDIO | Médio | Sprint 4 | LobbyManager -200 / novo +200 |
| 6 | Decidir UI canônica e remover duplicação restante | MÉDIO | Baixo | Sprint 5 | -500 a -1100 |
| 7 | Higiene (`#if !EOS_DISABLE`, `_charNames`, `GetLocalIpAddress`) | PEQUENO | Baixo | Sprint 6 | 0 (refactor interno) |
| 8 | (Opcional) Singletons sem auto-create | PEQUENO | Médio | Sprint 7 | 0 |

**Total esperado:** `LobbyManager.cs` 1626 → ~400 LOC; remoção de 3 UIs duplicadas + HostManager.

---

# SPRINT 0 — Setup e Alinhamento

## Objetivo
Garantir que o ambiente local está pronto, que o agente leu toda a documentação e que o sistema funciona **antes** de qualquer mudança.

## Pré-condições
- Acesso ao repositório `ExoBeasts_V3/PI3D`.
- Acesso ao repositório `mestre_darmas` (apenas leitura; **não modificar**).
- Unity 6 (6000.0.52f1) instalado.
- MPPM habilitado.

## Escopo de arquivos
**Nenhum.** Esta sprint é apenas leitura, pull e setup.

## Arquivos proibidos
**Todos.** Não modifique nada.

## Tarefas atômicas

### 0.1 Ler documentação de refatoração
**O que fazer:** Leia os 6 documentos da pasta `docs/Refactoring/` na ordem indicada em `00_LEIA_PRIMEIRO.md` §4.

**Critério de aceitação:**
- Checklist de pré-leitura em §6 do `00_LEIA_PRIMEIRO.md` marcada no log.

**Como validar:** Registre no log da sprint os checkboxes marcados.

### 0.2 Consultar memória do projeto
**O que fazer:** Abra `C:\Users\zegil\.claude\projects\C--Users-zegil-Documents-GitHub-ExoBeasts-V3-PI3D\memory\MEMORY.md` e leia o índice. Se sua sprint futura tocar em arquivos listados em §2.1 do `00_LEIA_PRIMEIRO.md`, leia o arquivo de memória correspondente.

**Critério:** registrar no log quais memórias específicas leu.

### 0.3 Pull e rebase
**O que fazer:**
```powershell
git fetch origin
git status
git log --oneline -5
git log --oneline origin/main -5
```

Se a branch local estiver atrás de `origin/main`:
```powershell
git pull --rebase origin main
```

**Critério:**
- `git status` reporta working tree clean.
- Branch local em paridade ou ahead de `origin/main`.

### 0.4 Build base
**O que fazer:** Abra o projeto Unity, aguarde recompilação, verifique console.

**Critério:**
- Console sem erros vermelhos.
- 0 warnings novos vs estado de origem (anote contagem).

**Como validar:** screenshot ou copy do Console no log.

### 0.5 Smoke test base
**O que fazer:**
1. Window → Multiplayer → Play Mode → +Add (cria 1 clone MPPM).
2. Entre em Play.
3. Na instância principal (Editor): cena `LobbyScene` deve aparecer; digite um nick e clique "Login".
4. Clique "Criar Sala".
5. No clone (MPPM): aguarde login automático; clique "Buscar Salas" ou cole o ID da sala criada e clique "Entrar".
6. Ambos confirmam Ready, selecionam personagem (Coruja/Samurai).
7. Host clica "Iniciar Partida".
8. Ambos chegam em `CenaMapaTeste` com seus personagens.

**Critério:**
- Cada passo executou sem erro.
- Ambos os personagens visíveis na cena final.
- Console sem erros novos durante o fluxo.

**Como validar:** sequência de prints/logs no log da sprint. Se algum passo falhar, abrir bloqueio (ver §4 de `03_PROTOCOLO_PROGRESSO.md`).

## Quality gate checklist (Sprint 0)
- [x] Working tree clean.
- [x] Build verde.
- [x] Smoke test OK.
- Nada foi modificado.

## Protocolo de rollback
N/A (sem mudanças).

## Critério de conclusão
Status do log = `PRONTA-PARA-REVISÃO` com todos os 5 itens da checklist marcados. Orquestrador aprova → Sprint 1 autorizada.

---

# SPRINT 1 — Remover `LobbyUIManager.cs`

## Objetivo
Eliminar a UI duplicada de lobby auto-marcada como "legado/test-only" no próprio comentário do arquivo. Reduz duplicação sem tocar em `LobbyManager`.

## Pré-condições
- Sprint 0 aprovada.
- Branch nova criada: `git switch -c claude/sprint-01-remove-lobbyuimanager`.

## Escopo de arquivos (apenas estes)
- `Assets/Codigo/Multiplayer/Lobby/LobbyUIManager.cs` — **deletar**.
- `Assets/Codigo/Multiplayer/Lobby/LobbyUIManager.cs.meta` — **deletar junto**.
- `Assets/Scenes/EscolherPersonagem.unity` — remover GameObject `LobbyUIManager` e referências de onClick.
- Eventualmente prefabs que referenciem o componente (ver tarefa 1.1).

## Arquivos proibidos
- `Assets/Codigo/Multiplayer/Lobby/LobbyManager.cs` — não tocar.
- `Assets/Codigo/Multiplayer/Lobby/LobbySceneUI.cs` — não tocar.
- Todos em `Assets/Codigo/Multiplayer/Sync/`, `Auth/`, `Core/`, `GameServer/`, `Testing/`.
- Qualquer outra cena além de `EscolherPersonagem.unity`.

## Tarefas atômicas

### 1.1 Mapear dependentes
**O que fazer:**
```powershell
# Buscar referências por nome de classe e por nome de método público
Grep -r "LobbyUIManager" Assets/
Grep -r "AbrirPainelMultiplayer" Assets/
Grep -r "FecharPainelMultiplayer" Assets/
Grep -r "AlterarMaxPlayers" Assets/
```

**Critério:** lista completa de:
- Arquivos `.cs` que referenciam.
- Arquivos `.unity` que referenciam (geralmente via `m_Script: {fileID: ...}`).
- Arquivos `.prefab` que referenciam.

**Como validar:** registrar a lista exata no log. Se alguma referência for **fora de `EscolherPersonagem.unity`**, **abrir bloqueio** — escopo precisa ser ajustado.

### 1.2 Confirmar com orquestrador
**O que fazer:** registrar no log "Dependentes encontrados: <lista>. Solicito autorização para prosseguir com delete."

**Critério:** orquestrador deixa comentário "autorizado" no log antes de seguir.

### 1.3 Remover referências de Inspector
**O que fazer:**
1. Abra `EscolherPersonagem.unity` no Editor Unity.
2. Localize GameObject `LobbyUIManager` na Hierarchy.
3. Para cada botão que tinha `onClick → LobbyUIManager.<método>`, remova a entrada do onClick (não delete o botão; só remova a linha do listener).
4. Delete o GameObject `LobbyUIManager`.
5. Salve a cena (Ctrl+S).

**Critério:**
- Cena salva sem warnings sobre "Missing reference".
- Diff git mostra mudanças em `.unity` mas **não em texto humano-legível** além de remoção de blocos `m_Component` e `MonoBehaviour`.

**Como validar:** `git diff --stat Assets/Scenes/EscolherPersonagem.unity` mostra alteração; abrir cena no Editor sem console errors.

### 1.4 Deletar arquivos
**O que fazer:**
```powershell
Remove-Item Assets/Codigo/Multiplayer/Lobby/LobbyUIManager.cs
Remove-Item Assets/Codigo/Multiplayer/Lobby/LobbyUIManager.cs.meta
```

**Critério:** ambos arquivos removidos. Volta ao Editor: console sem warnings de "missing script" (se houver, voltar para tarefa 1.1 — alguma referência foi esquecida).

### 1.5 Compilar
**O que fazer:** focar no Editor; deixar compilar.

**Critério:**
- 0 errors vermelhos.
- 0 warnings novos.

**Como validar:** Console screenshot/copy no log.

### 1.6 Smoke test
**O que fazer:** repetir tarefa 0.5 (smoke test MPPM completo).

**Critério:** mesmo passo a passo passa. Foco: fluxo `MenuScene → LobbyScene → EscolherPersonagem → CenaMapaTeste` continua funcionando.

**Atenção:** o painel multiplayer na cena `EscolherPersonagem` que era controlado por `LobbyUIManager` **agora não existirá**. Confirme que isso é OK (o painel era "legado/test-only" conforme comentário do código).

### 1.7 Medir LOC e atualizar log
**O que fazer:**
```powershell
# LOC inventário antes e depois da pasta Multiplayer
Get-ChildItem -Path Assets/Codigo/Multiplayer -Recurse -Filter *.cs |
  Measure-Object -Line | Select-Object Lines
```

**Critério:** total de LOC do namespace Multiplayer caiu em ≥ 547.

## Quality gate checklist (Sprint 1)
- [x] Sprint não tocou em `04_CONTRATOS_INTERFACE.md` (LobbyUIManager não era listado lá como contrato).
- [x] Build verde sem warnings novos.
- [x] Smoke test passou.
- [x] Nenhum arquivo cresceu (na verdade, namespace todo encolheu).
- [x] Nenhum comentário `audit`/`REGRA DE OURO`/`OPTIMIZATION` foi removido.
- [x] Nenhum bug de §2.3 do `00_LEIA_PRIMEIRO.md` reapareceu.

## Protocolo de rollback
Se smoke test falhar:
```powershell
git checkout Assets/Codigo/Multiplayer/Lobby/LobbyUIManager.cs
git checkout Assets/Codigo/Multiplayer/Lobby/LobbyUIManager.cs.meta
git checkout Assets/Scenes/EscolherPersonagem.unity
```
Voltar para Editor, deixar recompilar, smoke test novamente.

Se o rollback não restaurar funcionalidade (ex.: cena foi salva em estado quebrado antes do rollback), reverter o commit:
```powershell
git revert HEAD
```

## Critério de conclusão
- Arquivos deletados.
- Smoke test verde.
- Log marcado `PRONTA-PARA-REVISÃO`.
- PR aberto contra `main`.

---

# SPRINT 2 — Consolidar bootstrap NGO

## Objetivo
Reduzir a tripla sobreposição de `NetworkBootstrap` / `HostManager` / `GameServerManager` para 1-2 caminhos canônicos.

## Pré-condições
- Sprint 1 aprovada e merged em `main`.
- Branch nova: `claude/sprint-02-consolidate-bootstrap`.
- `git pull origin main` na branch antes de começar (Sprint 1 mergeada).

## Escopo de arquivos
- `Assets/Codigo/Multiplayer/Core/HostManager.cs` — **deletar**.
- `Assets/Codigo/Multiplayer/Core/HostManager.cs.meta` — **deletar**.
- `Assets/Codigo/Multiplayer/Core/NetworkBootstrap.cs` — extensão (adicionar property pública para `networkPort` se necessária; **não remover métodos**).
- `Assets/Codigo/Multiplayer/GameServer/GameServerManager.cs` — **avaliar** (deletar se ninguém usa, ou marcar `[Obsolete]`).
- Cenas que referenciem `HostManager` (varredura na tarefa 2.1).

## Arquivos proibidos
- `LobbyManager.cs` — não tocar.
- Tudo em `Sync/`.
- Tudo em `Auth/`.
- `EOSManagerWrapper.cs`, `PlayerIdentityBridge.cs`, `MppmHelper.cs`, `SessionManager.cs`.

## Tarefas atômicas

### 2.1 Mapear chamadores
**O que fazer:**
```powershell
Grep -r "HostManager\." Assets/
Grep -r "GameServerManager\." Assets/
```

**Critério:** lista exaustiva de chamadores para cada classe.

### 2.2 Para cada chamador de `HostManager`, definir substituto
**O que fazer:** use a tabela na §8 de `04_CONTRATOS_INTERFACE.md` que mapeia método antigo → método NetworkBootstrap equivalente.

**Critério:** lista no log no formato:
| Arquivo | Linha | Chamada antiga | Substituto |
|---|---|---|---|
| X.cs | 42 | `HostManager.Instance.StartAsHost()` | `NetworkBootstrap.Instance.StartHost()` |
| ... | | | |

### 2.3 Decidir destino de `GameServerManager`
**O que fazer:**
- Se varredura encontrar 0 chamadores em código de produção: marcar para delete.
- Se houver chamadores: marcar como `[Obsolete]` no header da classe, criar issue/note para futura migração; **não deletar nesta sprint**.

**Critério:** decisão registrada no log com justificativa.

### 2.4 (Se decidiu deletar GameServerManager) — preparar
**O que fazer:** para cada chamador (geralmente nenhum), migrar.

### 2.5 Migrar chamadores de `HostManager`
**O que fazer:** Para cada item da tabela em 2.2:
1. Abrir o arquivo.
2. Substituir a chamada exatamente como mapeado.
3. Salvar.
4. Recompilar e verificar build.

**Atenção a casos especiais:**
- `HostManager.Instance.GetHostPort()` retorna `ushort`. `NetworkBootstrap` tem `networkPort` privado — **precisa adicionar property pública `public ushort NetworkPort => networkPort;`**. Isso é uma extensão, permitida.
- `HostManager.Instance.GetMaxPlayers()` retorna `int`. Se houver chamador, decidir: mover constante para `NetworkBootstrap` ou usar literal (ver caso).

**Critério:** projeto compila após cada migração individual (commitar a cada chamador migrado para facilitar rollback granular).

### 2.6 Remover referências de Inspector ao `HostManager`
**O que fazer:** repetir padrão da tarefa 1.3 — abrir cenas que tinham GameObject `HostManager`, remover o GameObject, salvar.

### 2.7 Deletar `HostManager.cs` e `.meta`
**O que fazer:**
```powershell
Remove-Item Assets/Codigo/Multiplayer/Core/HostManager.cs
Remove-Item Assets/Codigo/Multiplayer/Core/HostManager.cs.meta
```

**Critério:** Editor recompila sem warnings de missing script.

### 2.8 Compilar e smoke test
Idêntico a 1.5 e 1.6.

**Atenção especial no smoke test:**
- Foque em verificar que `LobbyManager.StartMatch` continua funcionando — ele usa `NetworkManager.Singleton.StartHost()` direto, **não** `HostManager`, então não deve quebrar.
- Verifique que `NetworkBootstrap` não está sendo usado em paralelo com `LobbyManager.StartMatchCoroutine` (não devem competir). Em particular: a flag `autoStartHost` no Inspector do `NetworkBootstrap` deve estar `false` em cenas de produção.

### 2.9 Medir LOC e fechar log
Idêntico a 1.7.

## Quality gate checklist (Sprint 2)
- [x] `NetworkBootstrap` não removeu métodos públicos existentes (apenas adicionou).
- [x] `HostManager` removido (ou justificativa registrada se houve bloqueio).
- [x] Build verde.
- [x] Smoke test verde, com atenção a `LobbyManager.StartMatch`.
- [x] LOC do namespace Multiplayer caiu em ≥ 100.

## Protocolo de rollback
Se smoke test falhar:
- Reverter commit que migrou chamadores (cada um foi committed isoladamente em 2.5).
- `git restore` arquivos deletados.
- Re-build, smoke test.

## Critério de conclusão
- `HostManager.cs` deletado (ou `[Obsolete]` justificado).
- `GameServerManager.cs` decidido.
- Smoke test verde.
- PR aberto.

---

# SPRINT 3 — Extrair `MatchSessionLauncher` do `LobbyManager`

## Objetivo
Extrair ~500 LOC de orquestração NGO + Relay + Connection Approval do `LobbyManager` para uma classe nova `MatchSessionLauncher`, **sem alterar comportamento**.

## Pré-condições
- Sprint 2 aprovada e merged.
- Branch nova: `claude/sprint-03-extract-launcher`.
- **Backup branch criada:** `git branch backup/pre-sprint-03` antes de qualquer mudança — sprints grandes precisam de fallback fácil.

## Escopo de arquivos
- **Criar:** `Assets/Codigo/Multiplayer/GameServer/MatchSessionLauncher.cs` (nova classe, ≤ 500 LOC).
- **Modificar:** `Assets/Codigo/Multiplayer/Lobby/LobbyManager.cs` (extrações, sem mudanças funcionais).
- **Possivelmente modificar:** `Assets/Codigo/Multiplayer/Core/NetworkBootstrap.cs` se decidir consolidar StartHost lá (opcional; pode manter no MatchSessionLauncher).
- Adicionar GameObject `MatchSessionLauncher` em cenas que tinham `LobbyManager` (CenaMapaTeste, LobbyScene, EscolherPersonagem, MenuScene — confirmar).

## Arquivos proibidos
- Todos em `Sync/`.
- Todos em `Auth/`.
- `EOSManagerWrapper.cs`, `PlayerIdentityBridge.cs`, `MppmHelper.cs`, `SessionManager.cs`, `CharacterChoiceCache.cs`, `PartySlotLayout.cs`.
- Todas as UIs (`LobbySceneUI`, `LobbyPlaceholderUI`, `MenuLobbyPanel`).
- `MatchManager.cs`, `PlayerRegistry.cs`, `GameServerManager.cs`.

## Tarefas atômicas

### 3.1 Mapear o código a extrair
**O que fazer:** identifique no `LobbyManager.cs` os blocos a mover:
- Método `OnNgoConnectionApproval` (≈ 14 linhas).
- Método `StartMatch` (entry point — fica como facade no LobbyManager; corpo migra).
- Coroutine `StartMatchCoroutine` (≈ 200 linhas, incluindo lógica Relay + IP + WaitForAllClients).
- Coroutine `DelayedSceneLoad` (deprecated — pode migrar ou remover).
- Coroutine `WaitForAllClientsAndLoadScene` (≈ 80 linhas).
- Coroutine `ConnectClientCoroutine` (≈ 65 linhas).
- Coroutine `ConnectClientViaRelayCoroutine` (≈ 110 linhas).
- Método auxiliar `IsUsableRelayCode` (3 linhas; static).
- Método `GetLocalIpAddress` (se ele estiver dentro de StartMatchCoroutine; senão deixa para Sprint 7).

**Critério:** linhas inicial e final de cada bloco identificadas no log.

### 3.2 Criar esqueleto de `MatchSessionLauncher.cs`
**O que fazer:** criar arquivo vazio com:

```
namespace ExoBeasts.Multiplayer.GameServer
{
    public class MatchSessionLauncher : MonoBehaviour
    {
        // Singleton (mesmo padrão de outros singletons do projeto)
        // public static MatchSessionLauncher Instance { get; }
        // public static bool HasInstance => _instance != null;

        // API pública:
        // public void LaunchAsHost(string mapOverride, LobbyInfo lobbyContext, int hostCharIndex)
        // public void ConnectAsClient(LobbyDetails details, string relayCodeOrIp, ushort port, int myCharIndex)
        // public void CancelPendingConnect()

        // Coroutines internas:
        // StartMatchCoroutine, WaitForAllClientsAndLoadScene, ConnectClientCoroutine,
        // ConnectClientViaRelayCoroutine, IsUsableRelayCode

        // Connection Approval:
        // OnNgoConnectionApproval (callback)
    }
}
```

**Critério:** arquivo compila vazio (sem implementação ainda).

### 3.3 Mover `OnNgoConnectionApproval` e `IsUsableRelayCode`
**O que fazer:**
1. Copiar exatamente o corpo de `OnNgoConnectionApproval` do `LobbyManager` para o `MatchSessionLauncher`. Não mudar nada além de membros referenciados que precisam ser passados como parâmetros.
2. Em `LobbyManager.StartMatchCoroutine`, mudar `nm.ConnectionApprovalCallback = OnNgoConnectionApproval;` para `nm.ConnectionApprovalCallback = MatchSessionLauncher.Instance.OnNgoConnectionApproval;`.
3. Mover `IsUsableRelayCode` (static).

**Atenção:**
- `CharacterChoiceCache.SetClientCharacterIndex(...)` é static — pode ser chamado direto sem mudança.
- O callback tem assinatura específica do NGO; preserve-a.

**Critério:** compila; smoke test ainda verde.

### 3.4 Mover `StartMatchCoroutine` + dependências
**O que fazer:**
1. Mover o corpo inteiro de `StartMatchCoroutine` para `MatchSessionLauncher`.
2. Renomear como necessário: ex.: `private IEnumerator LaunchHostCoroutine(string mapOverride, LobbyInfo lobby, int hostCharIndex)`.
3. Os campos privados que essa coroutine usa (`DEFAULT_PORT`, `NO_RELAY_CODE`) — duplicar como constantes em `MatchSessionLauncher` ou expor como `public const` em `LobbyManager`.
4. Em `LobbyManager.StartMatch(string mapOverride = null)`:
   - **Manter assinatura idêntica** (contrato em `04_CONTRATOS_INTERFACE.md` §1.3).
   - O corpo fica simples:
     ```
     // Validações de pré-condição (não está em lobby, é host do lobby EOS, etc.)
     // Cálculo de hostCharIndex (via GetMyCharacterIndex)
     // CharacterChoiceCache.SetHostCharacterIndex(...) se necessário
     // MatchSessionLauncher.Instance.LaunchAsHost(mapOverride, _currentLobby, hostCharIndex);
     ```

**Atenção crítica:**
- Os comentários `audit (A1)`, `OPTIMIZATION (Sprint 3 / Item A3)`, etc., **viajam junto** com o código. Preserve byte-a-byte.
- `WaitForAllClientsAndLoadScene` provavelmente também migra (chamado de dentro de `StartMatchCoroutine`).

**Critério:** compila; smoke test verde, com atenção especial a `StartMatch` funcionar em MPPM (host + cliente conectam).

### 3.5 Mover `ConnectClientCoroutine` e `ConnectClientViaRelayCoroutine`
**O que fazer:**
1. Mover as duas coroutines.
2. Em `LobbyManager.ProcessLobbyAttributes`, no ponto onde inicia `ConnectClientCoroutine` ou `ConnectClientViaRelayCoroutine`, substituir por:
   ```
   MatchSessionLauncher.Instance.ConnectAsClient(...)
   ```
3. A flag `_pendingClientConnect` (Coroutine handle) precisa de uma decisão:
   - **Opção A:** ficar no `LobbyManager` mas armazenar a coroutine iniciada via `StartCoroutine(MatchSessionLauncher.Instance.ConnectAsClient(...))`. Risco: o handle não corresponde mais ao GameObject correto se `StopCoroutine` for chamado em LobbyManager — Coroutines pertencem ao MonoBehaviour que as iniciou.
   - **Opção B (recomendada):** mover `_pendingClientConnect` para `MatchSessionLauncher`. `LobbyManager.CancelPendingClientConnect()` (já é público — contrato!) passa a delegar para `MatchSessionLauncher.Instance.CancelPendingConnect()`.

**Critério:** compila; smoke test verde.

### 3.6 Atualizar cenas com novo GameObject `MatchSessionLauncher`
**O que fazer:** se o singleton de `MatchSessionLauncher` for auto-create-on-access (mesmo padrão de `LobbyManager`), pode ser pulado. Senão, adicione um GameObject `MatchSessionLauncher` em pelo menos uma cena persistente.

**Recomendação:** seguir o padrão do projeto — auto-create no getter para consistência com `LobbyManager`. Documente no log.

### 3.7 Build e smoke test profundo
**O que fazer:**
- Build verde.
- Smoke test completo (1.6 / 0.5).
- **Build standalone:** File → Build Settings → Build (target Windows). Confirmar que build produz `.exe` sem erros. **Não é necessário rodar o exe** — só validar que compila.
- Smoke test específico Sprint 3:
  - Host clica "Iniciar Partida": verifica que cena carrega para ambos.
  - Cliente entra **depois** do StartMatch ter sido publicado: testa o caminho `ProcessLobbyAttributes → ConnectAsClient`.
  - Host volta para MenuScene durante uma partida: verifica que cliente é redirecionado (NetworkBootstrap.OnClientStopped).

### 3.8 Medir LOC
**O que fazer:**
```powershell
(Get-Content Assets/Codigo/Multiplayer/Lobby/LobbyManager.cs | Measure-Object -Line).Lines
(Get-Content Assets/Codigo/Multiplayer/GameServer/MatchSessionLauncher.cs | Measure-Object -Line).Lines
```

**Critério:**
- `LobbyManager` < 1200 LOC (de 1626).
- `MatchSessionLauncher` ≤ 500 LOC.
- Soma dos dois ≤ soma original + 30 LOC (overhead aceitável: declaração de classe + namespace + imports).

## Quality gate checklist (Sprint 3)
- [x] `LobbyManager.StartMatch(string)`, `LobbyManager.CancelPendingClientConnect()`, `LobbyManager.ForceResetRuntimeState(bool)` continuam com **assinatura idêntica** (ver `04_CONTRATOS_INTERFACE.md` §1.3).
- [x] Comentários `audit (A1/A4/A5/C5)`, `OPTIMIZATION (Sprint 3)`, `SYNC-FIX` preservados.
- [x] Build verde sem warnings novos.
- [x] Build standalone gera .exe sem erros.
- [x] Smoke test MPPM verde (3 cenários da 3.7).
- [x] LOC do `LobbyManager` **diminuiu** (ratchet primary).
- [x] Nenhuma diretiva `#if !EOS_DISABLE` removida.
- [x] Connection Approval ainda funciona (payload de 4 bytes lido corretamente).

## Protocolo de rollback
**Importante:** sprint grande. Fazer **commits pequenos** após cada tarefa (3.3, 3.4, 3.5).

Se uma tarefa específica quebrar:
```powershell
git reset --hard HEAD~1  # ou HEAD~N se múltiplos commits
```

Se a sprint inteira ficar inviável:
```powershell
git switch main
git branch -D claude/sprint-03-extract-launcher
git switch -c claude/sprint-03-extract-launcher backup/pre-sprint-03  # restart from backup
```

## Critério de conclusão
- `MatchSessionLauncher.cs` existe com ≤ 500 LOC.
- `LobbyManager.cs` < 1200 LOC.
- 3 smoke tests verdes (host inicia, cliente entra depois, host derruba).
- PR aberto.

---

# SPRINT 4 — Extrair `LobbyNotificationDispatcher`

## Objetivo
Extrair handlers EOS (`OnMemberStatusChanged`, `OnLobbyAttributeUpdated`, `OnMemberAttributeChanged`, `RegisterNotifications`, `UnregisterNotifications`) para uma classe nova.

## Pré-condições
- Sprint 3 aprovada e merged.
- Branch nova: `claude/sprint-04-extract-dispatcher`.
- Backup: `git branch backup/pre-sprint-04`.

## Escopo de arquivos
- **Criar:** `Assets/Codigo/Multiplayer/Lobby/LobbyNotificationDispatcher.cs` (≤ 300 LOC).
- **Modificar:** `Assets/Codigo/Multiplayer/Lobby/LobbyManager.cs` (extração).

## Arquivos proibidos
Mesma lista da Sprint 3.

## Tarefas atômicas

### 4.1 Mapear blocos a extrair
- `RegisterNotifications()` (≈ 20 linhas).
- `UnregisterNotifications()` (≈ 13 linhas).
- `OnMemberStatusChanged(ref ...)` (≈ 45 linhas).
- `OnLobbyAttributeUpdated(ref ...)` (≈ 47 linhas).
- `OnMemberAttributeChanged(ref ...)` (≈ 83 linhas).
- `ProcessLobbyAttributes(LobbyDetails)` (≈ 90 linhas).
- Handles `_memberStatusHandle`, `_lobbyUpdateHandle`, `_memberUpdateHandle` (campos).
- `ReadMemberDisplayName` (helper, ≈ 30 linhas).

### 4.2 Criar `LobbyNotificationDispatcher.cs`
Estrutura:
```
public class LobbyNotificationDispatcher
{
    // Constructor recebe referências para:
    // - LobbyManager (callbacks: OnError, OnMemberJoined, OnMemberLeft, OnMemberUpdated, OnLobbyLeft)
    // - MembersList (do LobbyMembershipService — se Sprint 5 já feita; senão, do LobbyManager)
    //
    // Métodos públicos:
    // Register(string lobbyId)
    // Unregister()
    //
    // Métodos privados:
    // OnMemberStatusChanged, OnLobbyAttributeUpdated, OnMemberAttributeChanged, ProcessLobbyAttributes
}
```

**Decisão arquitetural:** dispatcher provavelmente NÃO é MonoBehaviour (não precisa de lifecycle Unity). É um `class` C# normal, instanciado pelo `LobbyManager` no construtor/Start.

### 4.3 Mover handlers
**O que fazer:** copiar corpo de cada handler para o dispatcher. Substituir referências a campos do `LobbyManager` por callbacks ou referências passadas no construtor.

**Cuidado especial:**
- `ProcessLobbyAttributes` chama `MatchSessionLauncher.Instance.ConnectAsClient(...)` (após Sprint 3). Esse acesso é via singleton estático — OK manter.
- `OnLobbyAttributeUpdated` faz `ExoBeasts.Managers.GameModeManager.CurrentMode != GameMode.Multiplayer` — manter inalterado.

### 4.4 LobbyManager passa a delegar
**O que fazer:**
```
private LobbyNotificationDispatcher _dispatcher;

private void Start() {
    _eosCache = EOSManagerWrapper.Instance;
    _dispatcher = new LobbyNotificationDispatcher(this);
    if (_eosCache.IsInitialized) _dispatcher.Register(/* lobbyId */);
    else _eosCache.OnEOSInitialized += () => _dispatcher.Register(...);
}

private void OnDestroy() {
    _dispatcher?.Unregister();
}
```

### 4.5 Build + smoke test
Smoke test específico:
- Ready toggle por um cliente → host vê atualização (testa `OnMemberAttributeChanged`).
- Cliente entra/sai do lobby → outros vêem (testa `OnMemberStatusChanged`).
- Host clica StartMatch → cliente é teleportado para CenaMapaTeste (testa `OnLobbyAttributeUpdated → ProcessLobbyAttributes`).

### 4.6 Medir LOC
- `LobbyManager` < 950 LOC.
- `LobbyNotificationDispatcher` ≤ 300 LOC.

## Quality gate checklist (Sprint 4)
- Idêntica à Sprint 3, com foco em:
- [x] Eventos públicos do `LobbyManager` (OnMemberJoined/Left/Updated, OnLobbyLeft) continuam disparando corretamente quando dispatcher recebe notificação.
- [x] LOC continua a cair.

## Rollback
Igual à Sprint 3.

## Critério de conclusão
- `LobbyNotificationDispatcher.cs` existe.
- `LobbyManager.cs` < 950 LOC.
- Smoke test verde.
- PR aberto.

---

# SPRINT 5 — Extrair `LobbyMembershipService`

## Objetivo
Extrair lógica de gerenciamento de membros (lista, ordenação canônica, character index, display name) para classe separada.

## Pré-condições
- Sprint 4 aprovada e merged.
- Branch nova: `claude/sprint-05-extract-membership`.
- Backup: `git branch backup/pre-sprint-05`.

## Escopo
- **Criar:** `Assets/Codigo/Multiplayer/Lobby/LobbyMembershipService.cs` (≤ 250 LOC).
- **Modificar:** `LobbyManager.cs` (delegação) e `LobbyNotificationDispatcher.cs` (passa a referência do service).

## Tarefas

### 5.1 Mapear blocos
- Campo `_members` (List<LobbyMember>).
- Métodos públicos `GetMembers`, `GetOrderedMembers`, `GetCanonicalMemberIndex` (todos contratos!).
- Métodos internos para ordenação, `PopulateMembersFromDetails`, `ReadMemberDisplayName`.
- Lógica de `SelectCharacter` que atualiza membro local optimistic.

### 5.2 Criar service
Estrutura similar à do dispatcher.

### 5.3 LobbyManager delega
**Cuidado:** os métodos públicos contratuais `GetMembers`, `GetOrderedMembers`, `GetCanonicalMemberIndex` **permanecem** em `LobbyManager` — internamente delegam ao service. Garante contrato preservado.

### 5.4 Smoke test
- Foco em ordenação canônica (host vs clients vendo a mesma ordem de personagens — bug histórico do `MEMORY.md`).
- Character index visível corretamente no slot do jogador.

### 5.5 LOC
- `LobbyManager` < 700 LOC.
- `LobbyMembershipService` ≤ 250 LOC.

## Critério de conclusão
- `LobbyMembershipService.cs` existe.
- `LobbyManager.cs` < 700 LOC.
- Smoke test verde.

---

# SPRINT 6 — Decidir UI canônica e remover duplicação

## Objetivo
Reduzir as 3 UIs restantes (`LobbySceneUI`, `LobbyPlaceholderUI`, `MenuLobbyPanel`) para no máximo 2 (uma de produção + opcionalmente um debug overlay).

## Pré-condições
- Sprint 5 aprovada e merged.
- **Decisão do orquestrador antes de começar:** qual UI é canônica? Ver decision tree abaixo.

### Decision tree (orquestrador decide ANTES da sprint)
```
Se entrypoint do produto é MenuScene:
  - LobbySceneUI vira único; MenuLobbyPanel é removido (mas MenuScene precisa apontar pra LobbyScene)
  - OU MenuLobbyPanel vira único; LobbySceneUI é removido (e LobbyScene é deprecated)
Se MenuScene NÃO tem lobby (só botão "Multiplayer" que carrega LobbyScene):
  - LobbySceneUI é canônico. MenuLobbyPanel removido.
```

## Escopo
Depende da decisão. Sempre inclui:
- `Assets/Codigo/Multiplayer/Testing/LobbyPlaceholderUI.cs` — **remover** (é OnGUI completo em Testing/; sem propósito após canonização).
- Possivelmente `Assets/Codigo/Multiplayer/Lobby/LobbySceneUI.cs` — refator interno (`WireBtn`).
- Possivelmente `Assets/Codigo/Multiplayer/Testing/MenuLobbyPanel.cs`.

## Tarefas (assumindo "LobbySceneUI canônico")

### 6.1 Remover `LobbyPlaceholderUI.cs`
- Mapear referências (varredura).
- Remover GameObject de qualquer cena que o use.
- Deletar `.cs` e `.cs.meta`.
- Build + smoke test.

### 6.2 Decisão sobre `MenuLobbyPanel.cs`
- Se MenuScene não terá mais lobby: remover.
- Se MenuScene mantém char-select pré-jogo (sem rede): refatorar para versão **sem dependências EOS/Lobby** (saneamento de escopo).

### 6.3 (Opcional) Extrair `LobbyButtonBinder`
- Mover `WireBtn`, `WireBtnByPath`, `WireBtnInParent` para classe helper.
- LobbySceneUI passa a usar `LobbyButtonBinder.Bind("BtnLogin", Login)`.
- Reduz LobbySceneUI em ~80 LOC.

### 6.4 Smoke test
- Cenário 1: MenuScene → LobbyScene → criar sala → cliente entra → confirmar Ready → start match → CenaMapaTeste.
- Cenário 2: voltar para MenuScene de dentro da partida.

## Critério de conclusão
- `LobbyPlaceholderUI.cs` deletado.
- Decisão sobre `MenuLobbyPanel` executada.
- LOC do namespace Multiplayer encolheu ≥ 500 nessa sprint.

---

# SPRINT 7 — Higiene (R4, R7, R8)

## Objetivo
Cleanups pequenos sem mexer em comportamento.

## Pré-condições
- Sprint 6 aprovada.

## Tarefas

### 7.1 Estratificar `#if !EOS_DISABLE` em LobbyManager
**O que fazer:**
- Identifique métodos onde `#if !EOS_DISABLE` envolve o corpo todo.
- Refator: método "alto nível" sem `#if`; método "low-level" com `#if` e fallback no `#else`.
  ```
  public void CreateLobby(LobbySettings s) {
      if (!LowLevelCreateLobby(s)) {
          // EOS_DISABLE path: log e dispara OnError
      }
  }

  private bool LowLevelCreateLobby(LobbySettings s) {
  #if !EOS_DISABLE
      // implementação atual
      return true;
  #else
      return false;
  #endif
  }
  ```
- Foco em legibilidade. Não tente fazer todos os métodos; pegue os 5 mais "poluídos".

### 7.2 Centralizar `_charNames`
**O que fazer:**
- Verifique se `GameDataManager.bibliotecaOriginalPersonagens` já expõe nomes.
- Se sim, substituir `_charNames` array nos arquivos onde aparece (verificar quais ainda existem após Sprint 6).
- Se não, criar property `public static IReadOnlyList<string> CharacterDisplayNames` em `PartySlotLayout`.

### 7.3 Extrair `GetLocalIpAddress`
**O que fazer:**
- Criar `Assets/Codigo/Multiplayer/Core/NetworkAddressHelper.cs` (~30 LOC).
- Mover o método (estava no `LobbyManager`).
- Atualizar chamadores.

### 7.4 Smoke test
- Cenário em LAN (IP direto): verificar que `NetworkAddressHelper.GetLocalIpAddress` retorna o IP correto.

## Critério de conclusão
- 3 tarefas atômicas fechadas.
- Build + smoke test verdes.

---

# SPRINT 8 — (Opcional) Singletons sem auto-create

## Objetivo
Remover o pattern `Instance { get { if null new GameObject; ... } }` dos 4 singletons principais.

## Pré-condições
- Sprint 7 aprovada.
- Game designer disponível para configurar GameObject `MultiplayerCore` na cena `MenuScene`.

## Por que opcional
Risco médio: pode regredir order-of-execution. Avaliar custo-benefício com o orquestrador.

## Tarefas

### 8.1 Criar GameObject persistente
- No projeto: criar prefab `MultiplayerCore.prefab` com:
  - `LobbyManager` (componente)
  - `EOSAuthenticator` (componente)
  - `EOSManagerWrapper` (componente)
  - `SessionManager` (componente)
  - `MatchSessionLauncher` (componente, após Sprint 3)
- Cada componente com `[DefaultExecutionOrder(-100)]`.
- Adicionar prefab à cena MenuScene; ele faz `DontDestroyOnLoad`.

### 8.2 Mudar getters
- Cada singleton: remover o `auto-create`. `Instance` retorna `_instance` (pode ser null).
- Chamadores: revisar e adicionar guards `if (LobbyManager.Instance == null) return;`.

### 8.3 Smoke test
- Verificar que tudo continua funcionando.
- Verificar que abrir uma cena sem `MultiplayerCore` não cria singleton fantasma.

## Critério de conclusão
- 4 singletons sem auto-create.
- MultiplayerCore.prefab criado.
- Smoke test verde.

---

## Apêndice — Mapa de dependências entre sprints

```
0 (setup) → 1 (remove LobbyUIManager) ─┐
                                       ↓
                                    2 (consolidate bootstrap) ─┐
                                                               ↓
                                                            3 (extract MatchSessionLauncher) ─┐
                                                                                              ↓
                                                                                           4 (extract Dispatcher) ─┐
                                                                                                                   ↓
                                                                                                                5 (extract Membership) ─┐
                                                                                                                                        ↓
                                                                                                                                     6 (UI canonization) ─┐
                                                                                                                                                          ↓
                                                                                                                                                       7 (hygiene) ─┐
                                                                                                                                                                    ↓
                                                                                                                                                                 8 (singleton refactor — opcional)
```

**Não pular etapas.** Cada sprint pressupõe a anterior aplicada em `main`.

---

## Apêndice — Estimativa de LOC final

| Marco | LobbyManager | Namespace total |
|---|---:|---:|
| Hoje | 1626 | 7690 |
| Após Sprint 1 (-LobbyUIManager) | 1626 | 7143 |
| Após Sprint 2 (-HostManager) | 1626 | 6900-7040 |
| Após Sprint 3 (+MatchSessionLauncher) | ~1120 | 6900-7040 (extração interna) |
| Após Sprint 4 (+Dispatcher) | ~880 | 6900-7040 |
| Após Sprint 5 (+Membership) | ~680 | 6900-7040 |
| Após Sprint 6 (-LobbyPlaceholderUI -MenuLobbyPanel?) | ~680 | 5800-6400 |
| Após Sprint 7 (hygiene) | ~660 | 5800-6400 |
| Após Sprint 8 (opcional) | ~660 | 5800-6400 |

**Meta final:** `LobbyManager.cs` ≤ 700 LOC (saiu do território "blocking" da rubrica Unity).

---

**Fim do `02_SPRINTS.md`.**
