# 00 — LEIA PRIMEIRO

> **Este é o primeiro documento que qualquer agente envolvido na refatoração multiplayer DEVE ler.**
> **Antes de tocar em qualquer arquivo de código, leia este documento até o fim e marque "lido" no log da sua sprint.**

---

## ⚠️ Aviso Crítico

O sistema multiplayer do PI3D está **em produção e funcional**.

Isso significa:

- **NÃO é uma greenfield refactor.** Não há liberdade para reescrever do zero ou trocar arquiteturas inteiras.
- **A meta da refatoração é estrutural**, não funcional. Nenhum comportamento observável (UI, conexão, gameplay) pode regredir.
- **Cada arquivo que parece "estranho" tem história.** Comentários `audit`, `REGRA DE OURO`, `BUG FIX` e `OPTIMIZATION` no código documentam decisões que custaram horas de debug. Preserve-os.

Se você não tem certeza se uma mudança é segura, **não faça**. Sinalize bloqueio ao orquestrador (ver `03_PROTOCOLO_PROGRESSO.md`).

---

## 1. Estado atual do sistema (snapshot 2026-05-20)

### 1.1 O que FUNCIONA hoje

**Autenticação e Sessão**
- Login EOS via Device ID (anônimo, sem conta Epic) — `EOSAuthenticator.cs`.
- Sessão persistente entre cenas com `sessionToken` GUID único por processo (necessário para distinguir clones MPPM) — `SessionManager.cs`.
- Wrapper sobre PlayEveryWare EOS SDK com tick fallback otimizado — `EOSManagerWrapper.cs`.

**Lobby (EOS Lobby Service)**
- Criar, buscar, entrar e sair de lobbies — `LobbyManager.cs`.
- Cache de `LobbyDetails` handles (EOS exige handle, não string ID).
- Sincronização de membros: `IS_READY`, `CHARACTER_INDEX`, `DISPLAY_NAME`.
- Rate-limit de `SearchLobbies` (cooldown 2s + cache).
- Notificações EOS: `MemberStatus`, `LobbyUpdate`, `MemberUpdate`.
- Detecção e tratamento de host vs cliente via `ProductUserId`.

**Lançamento de Partida (NGO)**
- `StartMatch` configura `ConnectionApproval = true` antes de `StartHost`.
- Em Editor (MPPM): IP direto `127.0.0.1` (Relay tem overhead desnecessário).
- Em build: tenta Unity Relay (NAT traversal) com fallback para IP direto LAN.
- Publica `RELAY_CODE` + `SERVER_ADDRESS` + `SERVER_PORT` como atributos do lobby.
- `WaitForAllClientsAndLoadScene` aguarda todos os clientes conectarem antes de `LoadScene` (timeout 25s).
- Connection Approval encoda `characterIndex` (4 bytes) no payload.

**Cliente recebe SERVER_ADDRESS via `OnLobbyAttributeUpdated`**
- `ProcessLobbyAttributes` decide entre Relay (preferido) e IP direto.
- Coroutines `ConnectClientCoroutine` e `ConnectClientViaRelayCoroutine` aguardam Shutdown limpo da sessão anterior antes de `StartClient`.

**Gameplay em rede sincronizado**
- Jogadores: `ClientNetworkTransform` (owner-authoritative) + `NetworkedPlayerController` (HP/ammo/character) — `Sync/`.
- Inimigos: `NetworkTransform` server-authoritative + `NetworkedEnemy` (HP/escudo/aggro/feedback de dano direcionado) — `Sync/`.
- Torres: `NetworkedBuilding` (server-authoritative; upgrades server-validated com cobrança de moeda) — `Sync/`.
- Armadilhas: `NetworkedTrapVisual` (server-authoritative; refund 60% no sell) — `Sync/`.
- Projéteis: `ServerAuthoritativeProjectile` (server-only, dano via `NetworkedEnemy`) — `Sync/`.
- Identidade: `PlayerIdentityBridge` (ponte NGO clientId ↔ EOS productUserId via ServerRpc) — `Core/`.
- Player setup: `PlayerNetworkSetup` (resolve owner vs remote, fix de race condition do Input System) — `Sync/`.

**UI de Lobby (4 implementações coexistentes)**
- `LobbySceneUI.cs` — Canvas/TMPro na `LobbyScene.unity`. **É o caminho de produção.**
- `LobbyUIManager.cs` — OnGUI/IMGUI na `EscolherPersonagem`. **Auto-marcado "legado/test-only" no próprio comentário.**
- `LobbyPlaceholderUI.cs` — OnGUI/IMGUI em pasta `Testing/`. Duplica fluxo completo.
- `MenuLobbyPanel.cs` — OnGUI/IMGUI na `MenuScene`. Tem character-select.

**Bootstrap NGO (4 caminhos)**
- `NetworkBootstrap.cs` — `StartHost`/`StartClient` simples, + return-to-menu em disconnect.
- `HostManager.cs` — `StartAsHost`/`StartAsClient` (duplica NetworkBootstrap).
- `GameServerManager.cs` — callbacks `OnClientConnected/Disconnected` + cap `maxConnectedPlayers`.
- `LobbyManager.StartMatchCoroutine` — implementa StartHost com Relay (único caminho com Relay).

**Recuperação**
- Quando o host derruba, clientes voltam automaticamente para `MenuScene` via `NetworkBootstrap.OnTransportFailure` / `OnClientStopped`.
- `MultiplayerRuntimeReset` limpa estado NGO ao retornar para single-player.

### 1.2 O que está em validação

- **Fase 4: EOS P2P Transport** — LAN OK; NAT pela internet pendente (precisa de testes entre 2 máquinas físicas).
- **Fase 5: Sincronização gameplay** — regressões resolvidas múltiplas vezes; ainda em hardening.
- **Rotação de credenciais EOS** pós-refactor de 13 Maio 2026 — pendente.

### 1.3 Stack técnico (referência rápida)

- **Unity:** 6 (6000.0.52f1)
- **NGO (Netcode for GameObjects):** 1.12.0
- **Unity Transport:** 2.4.0
- **EOS Plugin:** `com.playeveryware.eos` (instalado localmente como package)
- **MPPM (Multiplayer Play Mode):** `com.unity.multiplayer.playmode` v1.6.3
- **Branch dev:** `main`
- **Modelo:** P2P com host. Máximo de 4 jogadores.

---

## 2. Scripts CRÍTICOS que NÃO podem ser quebrados

Esta seção é o **anel de proteção**. Antes de modificar qualquer arquivo abaixo, **pare e consulte o orquestrador**.

### 2.1 Anel interno (NÃO TOCAR nesta rodada de refatoração)

| Arquivo | Por que não tocar |
|---|---|
| `Assets/Codigo/Multiplayer/Sync/ClientNetworkTransform.cs` | 15 LOC. Override mínimo de `OnIsServerAuthoritative` usado por todos os prefabs de player. Mexer obrigaria refatorar todos os prefabs. |
| `Assets/Codigo/Multiplayer/Sync/NetworkedBuilding.cs` | Server-authoritative correto. Contém a "REGRA DE OURO NGO" documentada na linha ~84 (uso de `NetworkManager.Singleton.IsServer` pré-Spawn). Quebrar isso reintroduz bug de torres com defaults zerados em cliente. |
| `Assets/Codigo/Multiplayer/Sync/NetworkedTrapVisual.cs` | Server-authoritative correto. Mesma "REGRA DE OURO" (linha ~73). Bug histórico: trap defaults em cliente. |
| `Assets/Codigo/Multiplayer/Sync/NetworkedEnemy.cs` | Sistema de feedback de dano (`DamageFeedbackMode.InstigatorOnly`), escudo, aggro visual via ClientRpc. Resultado de múltiplas sessões de bugfix (7 Maio 2026). |
| `Assets/Codigo/Multiplayer/Sync/NetworkedPlayerController.cs` | NetworkVariables com hooks bem dimensionados. `Debug.Log` sob `#if UNITY_EDITOR` é proposital (hot path). |
| `Assets/Codigo/Multiplayer/Sync/PlayerNetworkSetup.cs` | Contém coroutine `FinishLocalSetupNextFrame` que resolve race condition real do Input System em Unity 6. Documentado em `bug_host_client_movement.md` na memória do projeto. |
| `Assets/Codigo/Multiplayer/Sync/ServerAuthoritativeProjectile.cs` | Pattern correto. Grace period de spawn justifica complexidade. |
| `Assets/Codigo/Multiplayer/Sync/NetworkGameplayResolver.cs` | Static helper bem fatorado. Várias chamadas por frame — não acoplar. |
| `Assets/Codigo/Multiplayer/Core/EOSManagerWrapper.cs` | Sprint 3 Item A2 já otimizou. Tick fallback rate-limited. Mexer regride performance. |
| `Assets/Codigo/Multiplayer/Core/PlayerIdentityBridge.cs` | 83 LOC, responsabilidade única. Modelo do que os outros deveriam ser. |
| `Assets/Codigo/Multiplayer/Core/MppmHelper.cs` | Detecção custom de clone MPPM. **Tentar substituir pela env var oficial já foi tentado e reverteu** (ver `MEMORY.md`). |
| `Assets/Codigo/Multiplayer/Core/CharacterChoiceCache.cs` | Cache estático simples. Não toque sem entender o fluxo Connection Approval. |
| `Assets/Codigo/Multiplayer/Core/PartySlotLayout.cs` | Static helper de layout. 48 LOC, OK. |
| `Assets/Codigo/Multiplayer/Auth/SessionManager.cs` | Contém `sessionToken` GUID crítico para MPPM. Documentado em `MEMORY.md` linha "Identidade 3 Março 2026". |
| `Assets/Codigo/Multiplayer/Auth/EOSAuthenticator.cs` | Fluxo Device ID com tratamento de MPPM clone (delete-then-create). |
| `Assets/Codigo/Multiplayer/Core/EOSConfig.cs` | Refactor de 13 Maio 2026 removeu gambiarra de credenciais. `[NonSerialized]` é proposital — não regredir. |

### 2.2 Anel externo (TOCAR APENAS conforme a sprint específica autorizar)

| Arquivo | Sprint que autoriza | Restrição |
|---|---|---|
| `Assets/Codigo/Multiplayer/Lobby/LobbyManager.cs` | Sprints 3, 4, 5 | Apenas extração de classes; **assinaturas públicas preservadas integralmente**. |
| `Assets/Codigo/Multiplayer/Lobby/LobbySceneUI.cs` | Sprint 6 (apenas) | Refatoração interna do `WireBtn`; comportamento preservado. |
| `Assets/Codigo/Multiplayer/Lobby/LobbyUIManager.cs` | Sprint 1 | **Delete**. Sem outras edições antes. |
| `Assets/Codigo/Multiplayer/Testing/LobbyPlaceholderUI.cs` | Sprint 6 | Decisão: delete ou converte em debug overlay. |
| `Assets/Codigo/Multiplayer/Testing/MenuLobbyPanel.cs` | Sprint 6 | Decisão do orquestrador (ver tree no `02_SPRINTS.md`). |
| `Assets/Codigo/Multiplayer/Core/NetworkBootstrap.cs` | Sprint 2 | Extensão (adicionar método com Relay). Não remover métodos existentes. |
| `Assets/Codigo/Multiplayer/Core/HostManager.cs` | Sprint 2 | **Delete**. Migrar chamadores antes. |
| `Assets/Codigo/Multiplayer/GameServer/GameServerManager.cs` | Sprint 2 (avaliar) | Decidir: remover ou consolidar com `MatchManager`. |

### 2.3 Bugs históricos a NÃO reintroduzir

Estes bugs já foram resolvidos. Qualquer regressão é **rollback imediato** da sprint.

| Bug | Onde foi resolvido | Sintoma se reaparecer |
|---|---|---|
| Movimento host/cliente quebrado | `PlayerNetworkSetup.FinishLocalSetupNextFrame` | Jogador 2 (cliente) não anda; ou host trava ao spawnar |
| Inimigos sumindo em build | Prefab variants — fileID virtual | `[NetworkedEnemy]` não spawna em standalone (só em editor) |
| Armadilhas com defaults zerados em cliente | `IsServer` herdado vs `NetworkManager.Singleton.IsServer` pré-Spawn | Build limit ignorado, HUD zerado |
| ServerRpc auto-invocação infinita (Coruja Skill 2) | `ApplyMarkVisualClientRpc` separado | Marca da Coruja só aparece no host |
| Aggro visual só no host (Bug 4/5) | `SetAggroVisualClientRpc` | Ponto de exclamação não aparece em cliente quando inimigo detecta jogador |
| StartHost falha com IsClient=True | Guard em `OnLobbyAttributeUpdated` + shutdown emergencial | Host clica "Iniciar Partida" em build → não vira host |
| Loop infinito em `while(childCount > 0) Destroy(...)` | `for` reverso com bounds fixas | UI trava ao trocar de painel |
| Cooldown preso em não-host | Habilidades multiplayer (sprint Abril 2026) | Coruja Q/E/X / Raposa Q têm cooldown infinito em cliente |
| Credenciais EOS commitadas no SO | `[NonSerialized]` + env vars + gitignore (13 Maio 2026) | `EOSConfig.asset` carrega ClientSecret do disco |

---

## 3. Regras Absolutas

Estas regras valem em TODAS as sprints. Quebrar qualquer uma é rollback imediato e revisão de processo.

### 3.1 Regras de processo

1. **NUNCA modifique código sem ler o estado da sprint atual.** Antes de editar, abra `02_SPRINTS.md` e confirme: (a) qual sprint está ativa, (b) qual o escopo de arquivos, (c) que sua sprint atual permite tocar nesse arquivo.

2. **NUNCA instale plugins no projeto Unity.** O `Packages/manifest.json` é território do projeto. Quality Gate, ferramentas de análise e tooling auxiliar ficam **fora** do PI3D.

3. **NUNCA edite arquivos do projeto Quality Gate em `mestre_darmas/`** durante este trabalho. Esse repositório é referência; mudanças nele são fora de escopo.

4. **NUNCA atualize o baseline do Quality Gate em PR.** O baseline é sagrado (ver `01_QUALITY_GATE.md` §13).

5. **NUNCA commit code com warnings novos.** Se sua refatoração introduzir um warning de compilação que não existia antes, conserte antes do commit.

6. **NUNCA desabilite testes para fazer CI passar.** Se uma checagem reprovou, ela está te dizendo algo.

7. **SEMPRE rode `build` no Unity Editor após mudanças.** Compilação verde é critério mínimo.

8. **SEMPRE rode smoke test com MPPM (2 instâncias)** após mudanças que afetem fluxo de lobby ou launch.

9. **SEMPRE preserve comentários** `audit`, `REGRA DE OURO`, `BUG FIX`, `OPTIMIZATION`, `DEPRECATED`, `SYNC-FIX`. Eles são memória institucional.

10. **SEMPRE registre seu progresso** no formato definido em `03_PROTOCOLO_PROGRESSO.md`.

### 3.2 Regras de código (durante refatoração)

11. **Não introduza dependências novas.** Se sua refatoração precisaria de uma biblioteca, sinalize bloqueio.

12. **Não troque APIs do NGO ou EOS por equivalentes diferentes.** Manter `Unity.Netcode.NetworkBehaviour`, `Epic.OnlineServices.*`, etc.

13. **Não mude assinaturas públicas** de classes sem listar a mudança em `04_CONTRATOS_INTERFACE.md` e ter aprovação do orquestrador.

14. **Não remova `using` statements aparentemente não usados** sem confirmar via compilação que nenhum método os requer (extensões C# escondem dependências).

15. **Não troque coroutines por `async/await`** nesta rodada. NGO e EOS callbacks são coroutine-friendly; conversão pra async exige análise caso a caso.

16. **Preserve diretivas de pré-processador** (`#if !EOS_DISABLE`, `#if UNITY_EDITOR`). Elas têm motivo de existir.

17. **Não renomeie public fields/properties** de `MonoBehaviour` sem checar serialização nos `.unity` e `.prefab`. Unity perde referências silenciosamente.

18. **Mantenha os meta files (`.cs.meta`).** Apagar um arquivo sem apagar seu `.cs.meta` cria warning de meta órfão; apagar `.cs.meta` sem apagar o `.cs` pode reordenar GUID e quebrar referências.

### 3.3 Regras de comunicação

19. **Em caso de dúvida sobre se uma mudança é segura, NÃO faça.** Pare, registre a dúvida no log da sprint, e aguarde resposta do orquestrador.

20. **Ao terminar uma sprint, deixe o log da sprint em estado `pronto-para-revisao`.** Não passe para a próxima sprint sem aprovação.

---

## 4. Ordem de leitura recomendada

Para um agente novo entrando no projeto:

1. **Este arquivo (`00_LEIA_PRIMEIRO.md`)** — entender o estado e regras.
2. **`05_GLOSSARIO.md`** — vocabulário NGO, EOS, MPPM antes de mergulhar em código.
3. **`01_QUALITY_GATE.md`** — entender critérios; quais bloqueiam, quais avisam.
4. **`04_CONTRATOS_INTERFACE.md`** — entender o que NÃO pode mudar de assinatura.
5. **`02_SPRINTS.md`** — encontrar sua sprint específica e ler **só ela**.
6. **`03_PROTOCOLO_PROGRESSO.md`** — saber como reportar.
7. Memória do projeto: `C:\Users\zegil\.claude\projects\C--Users-zegil-Documents-GitHub-ExoBeasts-V3-PI3D\memory\MEMORY.md` — ler se sua sprint tocar em arquivos listados em §2.1 acima.

**Tempo estimado de leitura completa:** 45-60 minutos. Não pule.

---

## 5. O que está FORA de escopo desta refatoração

Para evitar scope creep e proteger o sistema funcional:

- ❌ Migrar de NGO para outra biblioteca de rede.
- ❌ Migrar de EOS para outro provedor de lobby.
- ❌ Adicionar features novas (matchmaking automático, ranking, anti-cheat).
- ❌ Substituir IMGUI/OnGUI por UI Toolkit.
- ❌ Migrar `LobbyManager` de Singleton para Dependency Injection container.
- ❌ Adicionar testes automatizados de PlayMode (separar como esforço próprio).
- ❌ Refatorar `MppmHelper` para a env var oficial (já tentado, reverteu).
- ❌ Implementar coverage / lint / jscpd em `.cs` (Quality Gate ainda não cobre C# de forma automatizada).
- ❌ Migrar de Coroutines para async/await.
- ❌ Trocar `ClientNetworkTransform` por algo "mais moderno".

Se você acha que algo da lista acima deve mudar, abra uma **discussão separada** com o orquestrador. **Não inclua na sprint atual.**

---

## 6. Critério de "pronto para começar a sprint"

Antes de iniciar QUALQUER sprint, marque no log:

```
[X] Li 00_LEIA_PRIMEIRO.md
[X] Li 05_GLOSSARIO.md
[X] Li 01_QUALITY_GATE.md (todos os critérios)
[X] Li 04_CONTRATOS_INTERFACE.md
[X] Li a sprint específica em 02_SPRINTS.md
[X] Li 03_PROTOCOLO_PROGRESSO.md
[X] Fiz git pull + rebase (se houver commits em origin/main)
[X] Confirmei branch atual: claude/<nome>
[X] Confirmei working tree clean
[X] Build verde no Unity Editor (compila sem warnings novos)
[X] Smoke test base: MPPM com 2 instâncias funciona ANTES das minhas mudanças
```

**Sem todos esses checks, NÃO comece a sprint.**

---

## 7. Como pedir ajuda

Se algo neste documento não está claro, ou se o sistema não está no estado descrito:

1. Pare imediatamente.
2. Abra `03_PROTOCOLO_PROGRESSO.md` e siga o **Protocolo de Bloqueio**.
3. Registre o que esperava encontrar e o que encontrou.
4. Aguarde resposta do orquestrador antes de continuar.

**Não invente.** O sistema atual foi construído com bugfix iterativo; suposições otimistas geram regressões.

---

**Fim do `00_LEIA_PRIMEIRO.md`.**

Próximo passo: `05_GLOSSARIO.md`.
