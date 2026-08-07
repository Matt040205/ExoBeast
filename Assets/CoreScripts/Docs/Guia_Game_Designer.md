# Guia do Game Designer — ExoBeasts V3 Multiplayer
# Como os scripts funcionam apos a migracao NGO

Ultima atualizacao: 2026-06-30
(Historico de mudancas: mar/2026 criacao inicial; jun/2026 atualizacao pos-Sprint 8)

---

## 1. Introducao: Por que Multiplayer?

O ExoBeasts V3 foi migrado para suportar **ate 4 jogadores simultaneos** usando **Netcode for GameObjects (NGO)** da Unity.

### Modelo de Rede: P2P com Host

```
┌──────────────────────────────────────────────────────────┐
│                    COMO FUNCIONA                         │
│                                                          │
│   Jogador 1 (HOST)          Jogador 2 (CLIENT)          │
│   ┌─────────────┐           ┌─────────────┐             │
│   │  Servidor    │◄────────►│  Cliente     │             │
│   │  + Cliente   │           │             │             │
│   └─────────────┘           └─────────────┘             │
│          ▲                         ▲                     │
│          │         EOS P2P         │                     │
│          ▼                         ▼                     │
│   ┌─────────────┐           ┌─────────────┐             │
│   │  Jogador 3   │           │  Jogador 4   │            │
│   │  (CLIENT)    │           │  (CLIENT)    │            │
│   └─────────────┘           └─────────────┘             │
└──────────────────────────────────────────────────────────┘
```

- **Host** = um jogador que roda o servidor E o cliente ao mesmo tempo
- **Clients** = jogadores que enviam comandos ao Host e recebem atualizacoes
- **Singleplayer** = roda como Host sem clientes remotos (mesmo codigo, sem mudancas especiais)
- **Matchmaking** = Epic Online Services (EOS) — lobby com busca e entrada por ID

### O que isso significa na pratica?

O Host e a "verdade" do jogo. Ele decide:
- Quanto dano um inimigo recebe
- Quando uma wave comeca
- Se o jogador tem dinheiro para construir uma torre
- Se um inimigo morreu ou nao

Os clientes pedem permissao ao Host para fazer coisas (atirar, construir, ativar habilidade) e o Host valida e executa.

---

## 2. Conceitos Essenciais

### MonoBehaviour vs NetworkBehaviour

| Conceito | MonoBehaviour | NetworkBehaviour |
|----------|---------------|------------------|
| O que e | Script normal do Unity | Script que funciona em rede |
| Heranca | `: MonoBehaviour` | `: NetworkBehaviour` |
| Ciclo de vida | `Start()`, `Update()` | `OnNetworkSpawn()`, `OnNetworkDespawn()` |
| Pode usar rede? | Nao | Sim (RPCs, NetworkVariables) |
| Precisa de NetworkObject? | Nao | Sim (no mesmo GameObject ou pai) |

**Regra simples:** Se o script precisa que outros jogadores vejam algo, ele precisa ser `NetworkBehaviour`.

### NetworkVariable — Dado sincronizado

```
Exemplo: vida do jogador

  Servidor muda vida para 75
       │
       ▼
  NetworkVariable<float> currentHealth = 75
       │
       ├──► Cliente 1 ve: 75 (barra de vida atualiza)
       ├──► Cliente 2 ve: 75
       └──► Cliente 3 ve: 75

  Se um jogador 4 entrar DEPOIS, ele tambem recebe 75 automaticamente.
```

NetworkVariables sao usadas para dados **persistentes** que todos precisam ver:
- Vida do jogador / inimigo / objetivo
- Wave atual
- Dinheiro do time
- Estado de habilidade (ativa/inativa)

### ServerRpc — Cliente pede ao servidor

```
  Jogador (Cliente)              Servidor (Host)
       │                              │
       │  "Quero atirar!"             │
       │ ──── ServerRpc ────────────► │
       │                              │ Valida: tem municao?
       │                              │ Calcula: acertou inimigo?
       │                              │ Aplica: dano no inimigo
       │                              │
```

Usado quando o jogador quer **fazer algo** que afeta o jogo:
- Causar dano
- Construir torre
- Ativar habilidade
- Gastar dinheiro

### ClientRpc — Servidor avisa todos

```
  Servidor (Host)               Todos os Clientes
       │                              │
       │  "Mostrem o efeito!"         │
       │ ──── ClientRpc ────────────► │
       │                              │ Todos tocam o som
       │                              │ Todos mostram o VFX
       │                              │ Todos atualizam a animacao
```

Usado para **efeitos visuais/sonoros** que todos devem ver:
- Som de tiro / cura / vitoria
- Flash de hit no inimigo
- Animacao de habilidade
- Popup de dano

### IsOwner / IsServer — Quem controla o que?

| Verificacao | Significado | Usado para |
|-------------|-------------|------------|
| `IsOwner` | "Este jogador e MEU?" | Input, camera, HUD |
| `IsServer` | "Eu sou o servidor?" | Dano, spawn, economia |
| `!IsOwner` | "Este jogador e de OUTRO?" | Desativar camera/input remoto |

### OnNetworkSpawn / OnNetworkDespawn

- `OnNetworkSpawn()` = "o objeto nasceu na rede" — substitui `Start()` para tudo que depende de rede
- `OnNetworkDespawn()` = "o objeto vai ser removido da rede" — substitui `OnDestroy()` para cleanup

**Importante:** `IsOwner` e `IsServer` so funcionam DEPOIS de `OnNetworkSpawn()`. Usar antes causa bugs.

---

## 3. Sistema por Sistema — O que Mudou

### 3a. Player (`Characters/Player/`)

Todos os scripts do jogador agora herdam de `NetworkBehaviour`.

#### PlayerMovement.cs
- **Quem controla:** Apenas o dono (`IsOwner`) processa input e move o personagem
- **Sincronizacao:** Usa `ClientNetworkTransform` (owner-authoritative) — o dono move e a posicao replica para todos
- **Animacoes:** Sincronizadas via `NetworkAnimator`
- **No Inspector:** Precisa de `NetworkObject` + `ClientNetworkTransform` no prefab

#### PlayerHealthSystem.cs
- **Vida:** `NetworkVariable<float> currentHealth` — servidor controla, todos veem
- **Buffs:** `speedMultiplier`, `damageMultiplier`, `damageResistance` — todos sincronizados
- **Morte/Respawn:** Servidor reseta vida → `RespawnClientRpc()` teleporta o dono
- **HUD:** Le `currentHealth.OnValueChanged` para atualizar barra de vida

#### PlayerShooting.cs
- **Input:** Apenas `IsOwner` detecta clique
- **Cadeia de tiro:**
  1. Owner atira localmente (feedback imediato, zero lag)
  2. `ShootServerRpc()` — servidor valida e calcula dano
  3. `ShootVisualClientRpc()` — todos os outros jogadores veem o tiro
- **Projeteis:** Sao visuais locais (nao sao NetworkObjects) — cada cliente tem seu pool

#### MeleeCombatSystem.cs
- **Input:** Apenas `IsOwner` processa o botao de ataque
- **Dano:** `RequestMeleeDamageServerRpc()` — servidor valida e aplica
- **Animacao:** Sincronizada via NetworkAnimator (todos veem o golpe)

#### PlayerCombatManager.cs
- **Tipo de combate:** `NetworkVariable<CombatType>` — Ranged ou Melee
- **Troca de arma:** Cliente pede via `ServerRpc`, servidor atualiza
- **Visual:** `OnValueChanged` atualiza modelos 3D em todos os clientes

#### ThirdPersonCamera.cs e CameraController.cs
- **Apenas o dono:** Camera, AudioListener e input de mouse so funcionam no `IsOwner`
- **Jogadores remotos:** Camera e inputs desativados automaticamente em `OnNetworkSpawn`
- **Cinemachine:** Continua funcionando normalmente para o dono

#### PlayerHUD.cs
- **Local:** Singleton que mostra dados apenas do jogador LOCAL
- **Registro:** Jogador local se registra no HUD via `PlayerHUD.Instance.RegistrarJogador(this)` ao nascer
- **Dados:** Le NetworkVariables de vida, municao, etc.

#### VerificadorQueda.cs
- **Apenas o dono:** `IsOwner` detecta queda abaixo do limite
- **Respawn:** Teleporte local via CharacterController, `ClientNetworkTransform` replica para todos

#### PreyMarkLogic.cs (Marca da Presa)
- **Servidor:** Itera `SpawnManager.SpawnedObjects` para encontrar inimigos
- **Efeito:** Aplica debuff de dano extra em todos os inimigos marcados

---

### 3b. Inimigos (`Enemy/`)

Inimigos sao **server-authoritative** — a IA roda apenas no Host.

#### EnemyController.cs
- **IA:** NavMeshAgent controlado apenas pelo servidor
- **Targeting:** Usa `PlayerRegistry` para encontrar o jogador mais proximo entre todos os conectados
- **Rename:** Antigo `EnemyControler.cs` → corrigido para `EnemyController.cs`

#### EnemyHealthSystem.cs
- **Dano:** Apenas o servidor pode chamar `TakeDamage()`
- **Assinatura:** `TakeDamage(float damage, float armorPenetration = 0f, bool isCritical = false)`
- **Visual:** Hit flash e damage popup via `ClientRpc` — todos veem
- **Barra de vida:** UI le `NetworkHealth.OnValueChanged`

#### NetworkedEnemy.cs
- **Wrapper:** Conecta EnemyController com a rede
- **Spawn:** Servidor instancia e faz `NetworkObject.Spawn()`

#### EnemyPoolManager.cs
- **Pool:** Reutiliza inimigos mortos em vez de criar novos (`Despawn(false)` / `SetActive`)
- **Performance:** Evita Instantiate/Destroy em waves grandes

---

### 3c. Torres (`Towers/`)

#### TowerController.cs
- **IA de torre:** Targeting e tiro rodam no servidor
- **Construcao:** Cliente pede via `PlaceBuildingServerRpc` → servidor valida custo → spawna para todos
- **Animacao:** Tiro sincronizado via NetworkAnimator

#### TowerBehavior.cs (Base dos upgrades)
- **Classe abstrata:** Base para todos os comportamentos especiais de torre
- **NetworkBehaviour:** Permite `IsServer` guards nos subclasses
- **13 subclasses:** BleedingBehavior, OwlEyeBehavior, ArrowRainBehavior, FuryStackyBehavior, ReloadSpeedBehavior, FlyingEnemyTargetingBehavior, PreyMarkBehavior, DarkVisionBehavior, ProjectileSpeedBehavior, ArmorShredBehavior, BonusDamageToShreddedBehavior, DoubleAttackBehavior, AssaultBehavior, FuryStackBehavior, MultiShotBehavior

**Alerta ativo (#26):** Todas as 13 subclasses usam `OnDestroy()` em vez de `OnNetworkDespawn()` para remover listeners. Funciona hoje, mas pode causar problemas se pooling for implementado.

---

### 3d. Habilidades — Coruja (`Characters/Coruja/`)

Padrao geral: **Ability ScriptableObject** define os dados → **Logic component** executa (`enabled = true` para ativar, `false` para desativar).

#### CacadoraNoturnaLogic.cs (Ultimate)
- **O que faz:** Raio de energia que causa dano em linha reta
- **Rede:** Servidor spawna o beam como NetworkObject
- **NetworkVariables:** `netDamage`, `netRange`, `netWidth`, `netCaster`
- **Animacao:** Trigger `CacadoraUltimate` via NetworkAnimator
- **Servidor:** Calcula e aplica dano (`ApplyBeamDamage`)
- **Clientes:** Recebem o spawn e mostram o visual

#### VooGraciosoLogic.cs
- **O que faz:** Melhora pulo + flutuacao + bonus de dano no proximo tiro
- **Owner:** Aplica `isFloating` e `jumpHeightModifier` localmente (responsivo)
- **Servidor:** Aplica `SetNextShotBonus` no PlayerShooting (dano autorizado)
- **Fim:** Detecta quando jogador toca o chao → pede `Despawn` ao servidor
- **Alerta (#17):** No Host, `DestroyLogic` pode ser chamado duas vezes (guard de `IsSpawned` previne erro)

#### HabilidadeCacadoraNoturna / HabilidadePerseguindoPresas / HabilidadeVooGracioso
- **ScriptableObjects** que definem parametros (dano, duracao, cooldown)
- **Ativacao:** `CommanderAbilityController` verifica cooldown e chama `ServerRpc`

#### PassivaComandanteCoruja.cs
- **Passiva:** Ativada automaticamente em `OnNetworkSpawn`

#### Tower Behaviors da Coruja (10 scripts)
- Todos protegidos por `if (!IsServer) return;` nos hooks de evento
- Afetam torres proximas ao jogador Coruja

---

### 3e. Habilidades — Raposa (`Characters/Raposa/`)

#### CuttingBladeLogic.cs (Dash com Dano)
- **Owner:** Detecta input de dash
- **ServerRpc:** `PerformDashDamageServerRpc(start, end, damage, resetCooldown)`
- **Servidor:** Valida percurso, aplica dano aos inimigos na trajetoria
- **ClientRpc:** `NotifyResetCooldownClientRpc()` reseta cooldown se matou inimigo

#### NineTailsDanceLogic.cs (Ultimate)
- **NetworkVariable:** `netIsUltimateActive` (bool) — sincroniza estado para todos
- **ServerRpc:** `SetUltimateStateServerRpc(bool)` — ativa/desativa
- **Efeito:** Muda modo de combate para Melee com bonus de dano e range
- **Alerta (#18):** `originalAttackRange` pode nao ser inicializado se um cliente entrar durante a ultimate (race condition rara)

#### PeaceOfMindLogic.cs (Cura)
- **ServerRpc:** `RequestPeaceOfMindServerRpc(totalHeal, duration)` — inicia cura no servidor
- **Servidor:** Roda coroutine de cura gradual, modifica `currentHealth`
- **ClientRpc:** `PlayHealSFXClientRpc()` e `StopHealSFXClientRpc()` — som FMOD sincronizado
- **Cleanup:** `OnNetworkDespawn` libera instancia FMOD

#### Aura Behaviors (Armor, Legacy, Healing)
- **Servidor:** Calcula buffs em torres proximas
- **Cleanup:** `OnNetworkDespawn` remove buffs se aura for destruida

---

### 3f. Dragao + Polvo — Status de migracao (atualizado jun/2026)

#### Dragao — MIGRADO (Sprint 4–Maio 2026)

As habilidades do Dragao foram migradas para NGO. O alerta #19 original foi resolvido.

| Script | Status | Observacao |
|--------|--------|------------|
| AquiNaoLogic.cs | Migrado | Owner-proxy pattern para VFX; dano server-authoritative |
| PosturaBaluarteLogic.cs | Migrado | Owner-proxy pattern; escudo server-authoritative |
| TemorSismicoLogic.cs | Migrado | `NetworkObject` real; knock-up 2s via `EnemyStatusController` |
| PassiveEscamasAdamantium.cs | Migrado | Guard `IsServer` + loop limitado (sem FindObjectsOfType) |
| DragonPatrolBehavior.cs | Migrado | IA server-authoritative com estados Idle/Chasing/Attacking/Returning e leash no ponto de spawn |
| TorretaDragao.prefab | Migrado | NetworkTransform server-authoritative; NavMeshObstacle movel desabilitado em runtime; IA/NavMesh desligados no modo preview |

**Observacao Torre Dragao**: o ataque animado esta desligado ate existir animacao propria. O modelo fica em pose base em vez de tombar durante o ataque.

#### Polvo — PARCIALMENTE MIGRADO

| Script | Status | Observacao |
|--------|--------|------------|
| MergulhoTintaLogic.cs | Migrado | Entrada/saida decidida no servidor; `LoseTarget()` no servidor; posicao de superficie enviada ao owner via ClientRpc |
| BombaSprayProjectile.cs | Migrado | Spawn da nuvem via servidor; prefab registrado em `DefaultNetworkPrefabs.asset` |
| NuvemDeTintaLogic.cs | Migrado | Spawn server-side; sem SendMessage |
| TracoUrbanoLogic.cs | Pendente | Ainda MonoBehaviour |
| ProjetilColorido.cs | Pendente | Ainda MonoBehaviour |
| PaintAbilitySystem.cs | Pendente | Ainda MonoBehaviour |
| ObraPrimaLogic.cs | Pendente | Ultimate — ainda MonoBehaviour |

Para os scripts ainda pendentes, quando migrar:
- `MonoBehaviour` → `NetworkBehaviour`
- `Destroy()` → `Despawn()`
- `TakeDamage(damage)` → incluir `armorPenetration` e `isCritical`
- Adicionar guards `IsServer` / `IsOwner`

---

### 3g. Audio (`Audio/`)

#### WinSound.cs e LoseSound.cs
- **NetworkBehaviour:** Servidor seleciona indice aleatorio da lista de audios
- **ClientRpc:** `PlayVictorySoundClientRpc(index)` / `PlayLoseSoundClientRpc(index)`
- **Resultado:** Todos os jogadores ouvem a MESMA musica

#### GerenciadorDeSomGlobal.cs, MusicManager.cs, VolumeManager.cs, WindSound.cs
- **Local:** 100% no cliente, sem rede
- **VolumeManager:** Le PlayerPrefs locais
- **MusicManager:** Singleton local, outros scripts chamam via ClientRpc quando necessario

---

### 3h. UI (`UI/`) e Managers (`Managers/`)

#### UIManager.cs
- **Local:** NAO e NetworkBehaviour
- **Pause:** Removido `Time.timeScale = 0` — pause e visual/input-only
- **Timer:** Le `MatchManager.Instance.MatchTime.Value` (NetworkVariable)

#### BuildManager.cs (em `Managers/`)
- **Ghost/Preview:** Local (zero lag visual)
- **Construcao:** `PlaceBuildingServerRpc` → servidor valida custo → spawna torre

#### CurrencyManager.cs (em `Managers/`)
- **Dinheiro do time:** NetworkVariable — se um jogador coleta, sobe para todos
- **Compras:** Cliente pede via ServerRpc, servidor valida

#### HordeManager.cs (em `Managers/`)
- **Waves:** NetworkVariables para wave atual, nivel de inimigos, contagem
- **Spawn:** Apenas o servidor spawna inimigos
- **Vitoria:** `NetworkManager.Singleton.SceneManager.LoadScene("Win")` — todos migram juntos

#### ObjectiveHealthSystem.cs (em `Managers/`)
- **Vida do Core:** `NetworkVariable<float>` — servidor controla
- **Derrota:** Quando Core morre, todos vao para cena "Lose" sincronizadamente

#### GameModeManager.cs (em `Managers/`)
- **Gerencia:** Solo vs Online
- **Solo:** Inicia como Host local silenciosamente
- **Online:** Redireciona para fluxo de Lobby

---

## 4. Sistema de Lobby (`Multiplayer/Lobby/`)

### Fluxo Completo (Sprint 8 — estado atual)

```
1. Login EOS (automatico, Device ID via EOSAuthenticator)
       │
       ▼
2. Criar Lobby  ──OU──  Buscar Lobbies
       │                      │
       ▼                      ▼
3. Sala com ate 4 slots     Lista de lobbies disponiveis
       │                      │
       │◄─────── Entrar ──────┘
       ▼
4. Todos clicam "Pronto" (LobbySceneUI)
       │
       ▼ AllMembersReady() == true
5. Host clica "Iniciar Partida" (habilitado somente quando todos prontos)
       │
       ├──► MatchSessionLauncher.LaunchHostCoroutine()
       │         publica SERVER_ADDRESS / RELAY_CODE / LOBBY_STATE=InGame
       │
       ├──► Clientes detectam via OnLobbyAttributeUpdated
       │         chamam ConnectAsClient*
       │
       └──► WaitForAllClientsAndLoadScene("CenaSeleçao")
               NGO carrega cena para todos → selecao de personagem
               → LobbyManager.StartMatch(mapName="CenaMapaNOVO")
               → CenaMapaNOVO carregada
```

### Arquivos principais (estado atual):
- **LobbyManager.cs** — chamadas EOS (criar, buscar, entrar, sair, iniciar)
- **LobbyData.cs** — estruturas de dados (`LobbyMember`, `LobbyInfo`, `LobbySettings`, `LobbyState`)
- **LobbySceneUI.cs** — interface canonica Canvas (Sprint 6+); botos por nome via `LobbyButtonBinder`
- **MatchSessionLauncher.cs** — orquestra StartHost/StartClient e publicacao de atributos de conexao
- **LobbyMembershipService.cs** — gestao de membros extraida do LobbyManager (Sprint 5)
- **LobbyNotificationDispatcher.cs** — notificacoes EOS extraidas do LobbyManager (Sprint 4)
- **EosLobbyModHelper.cs** — helpers `AddStringAttr`, `AddInt64Attr`, `AddStringMemberAttr` (Sprint 7)

**Nao usar** (deletados no Sprint 6): `LobbyPlaceholderUI.cs`, `MenuLobbyPanel.cs`.
**Nao confundir** com `LobbyUIManager.cs` — e tombstone `#if UNITY_EDITOR [Obsolete]`, nao e UI real.

---

## 5. Alertas Ativos

Problemas identificados que ainda nao foram corrigidos (atualizado jun/2026):

| # | Script | Problema | Severidade |
|---|--------|----------|------------|
| 1 | PlayerHealthSystem | Memory leak: OnValueChanged callback nunca removido | Media |
| 2 | PlayerHealthSystem | GetComponent via string no Respawn | Baixa |
| 3 | EnemyCombatSystem | SphereCollider adicionado em runtime (nao sincroniza) | Media |
| 4 | EnemyPoolManager | Nao usa INetworkPrefabInstanceHandler | Baixa |
| 5 | EnemyController | FindFirstObjectByType chamado a cada inimigo | Baixa |
| 6 | PlayerShooting | Invoke com string para reload | Baixa |
| 7 | BuildManager | Camera.main chamado no Update | Baixa |
| 8 | PlayerMovement | GetComponent<MergulhoTintaLogic> no input de pulo | Baixa |
| 9 | CommanderAbilityController | Cooldowns nao sincronizados (local por cliente) | Baixa |
| 10 | TrapLogicBase | Venda sem feedback visual | Baixa |
| 14 | BleedingBehavior | ApplyBleed nao implementado no EnemyHealthSystem | Media |
| 15 | OwlEyeBehavior | ApplyReveal nao implementado | Baixa |
| 16 | ArmorAuraBehavior | OverlapSphere todo frame (deveria ter timer) | Baixa |
| 17 | VooGraciosoLogic | DestroyLogic chamado duas vezes no Host | Baixa |
| 18 | NineTailsDanceLogic | originalAttackRange pode nao inicializar (race condition rara) | Baixa |
| 20 | MultiShotBehavior | FireProjectileAt nao existe no TowerController | Media |
| 21 | BotaoHabilidade | FindObjectOfType<Rastros> no menu | Baixa |
| 24 | MatchManager | EndMatchVictory/Defeat nunca chamados pelo fluxo | Baixa |
| 25 | MatchManager | Invoke sem cancelamento para delays | Baixa |
| 26 | 13 TowerBehavior subclasses | OnDestroy em vez de OnNetworkDespawn (problema se pooling implementado) | Media |
| 30 | Polvo (TracoUrbano, ProjetilColorido, PaintAbilitySystem, ObraPrima) | Scripts ainda MonoBehaviour — nao funcionam em multiplayer | Alta |
| 31 | TorretaDragao | Ataque animado desligado (sem animacao propria) — modelo em pose base durante ataque | Baixa |
| 32 | Loading 2o match | Estado residual de SceneTransitionHandler prende cliente na transicao | Media |
| 33 | Limite de armadilhas | BuildLimit == 0 nos TrapDataSO (Espinhos, TP, Broca, Fogueira, Piche) — configurar no Inspector | Alta |

Alertas corrigidos (removidos desta lista):
- #12: encoding TutorialPopupUI
- #13: Debug.Log UIManager
- #19: Dragon/Polvo nao migrados — Dragon concluido, Polvo parcial (ver secao 3f)
- #22: PassiveEscamasAdamantium sem IsServer guard — corrigido
- #23: ObjectiveHealthSystem lambda — corrigido
- #27: NineTailsDanceAbility enabled=true — corrigido
- #28: Dragon/Polvo TakeDamage 1 param — corrigido nos migrados
- #29: NuvemDeTinta/BombaSpray Destroy()/SendMessage() — corrigido

---

## 6. Checklist para o Game Designer

### Como testar multiplayer localmente (MPPM)

**Pre-requisito**: ter `EOSCredentials.json` na raiz do projeto (ver `Assets/Multiplayer/CREDENTIALS_SETUP.md`).

1. **Abrir Unity** com MPPM — menu `Window > Multiplayer Play Mode`
2. **Configurar 1 Virtual Player** adicional (total: Editor principal + 1 clone)
3. **Abrir** `LobbyScene.unity` (nao SceneMapTest diretamente)
4. **Clicar Play** no Editor (Player 1 = futuro host)
5. **No Editor**: criar lobby, escolher personagem, clicar "Pronto"
6. **No clone MPPM**: entrar no mesmo lobby, escolher personagem, clicar "Pronto"
7. **No Editor** (host): clicar "Iniciar Partida" (botao habilita quando todos prontos)
8. Ambos devem carregar `CenaSeleçao` → confirmar personagem → carregar `CenaMapaNOVO`

**O que verificar apos entrar na partida:**
- Ambos veem o personagem do outro?
- Animacoes sincronizam (ataque, pulo)?
- Tiros/golpes causam dano correto?
- Inimigos perseguem o jogador mais proximo?
- Torres construidas aparecem para todos?
- Armadilhas respeitam o limite configurado?
- Dinheiro atualiza para todos ao coletar?
- Player remoto nao rouboa camera nem input?

### O que observar no Inspector (checklist de prefab)

Para cada prefab de jogador, verificar que tem:
- `NetworkObject` (na raiz)
- `ClientNetworkTransform` (Interpolate = true, SmoothedTime = 0.05)
- `NetworkAnimator` ou `ClientNetworkAnimator`
- Todos os scripts de `Player/` atribuidos

Para torres e armadilhas:
- `NetworkObject` na raiz
- Prefab registrado em `Assets/Multiplayer/Setup/DefaultNetworkPrefabs.asset`
- `TrapDataSO`: `buildLimit` > 0 (hoje todos em 0 = ilimitado — Alerta #33)

### Como diferenciar bug de rede vs bug de gameplay

| Sintoma | Provavel causa |
|---------|---------------|
| So acontece quando tem 2+ jogadores | Bug de rede (verificar IsOwner/IsServer) |
| Acontece em singleplayer tambem | Bug de gameplay (nao relacionado a rede) |
| Um jogador ve algo diferente do outro | NetworkVariable nao atualizada ou ClientRpc faltando |
| Acao funciona para Host mas nao para Client | Falta ServerRpc ou guard errado |
| Objeto desaparece para um jogador | Destroy() em vez de Despawn() |
| Console mostra "non-server writes" | Codigo tentando mudar NetworkVariable sem ser servidor |
| Player nao se move (host) | FinishLocalSetupNextFrame interrompido — ver PADROES_NGO.md P4 |
| Player nao se move (cliente) | Dois PlayerInput ativos — ver bug_host_client_movement.md |
| Armadilha/heal nao detecta player remoto | Rigidbody Kinematic ausente — ver PADROES_NGO.md P2 |
| Limite de armadilha ignorado | IsServer pré-Spawn — ver PADROES_NGO.md P1 |
| Inimigo sumiu em build (ok no Editor) | FileID de Prefab Variant — ver PADROES_NGO.md P6 |

---

## Glossario Rapido

| Termo | Significado |
|-------|-------------|
| NGO | Netcode for GameObjects — framework de rede da Unity |
| Host | Jogador que e servidor + cliente ao mesmo tempo |
| Client | Jogador que conecta ao Host |
| Owner | Dono de um objeto de rede (quem tem permissao de controlar) |
| Spawn | Criar objeto na rede (todos recebem) |
| Despawn | Remover objeto da rede (substitui Destroy) |
| RPC | Remote Procedure Call — chamada de funcao pela rede |
| ServerRpc | RPC do cliente para o servidor |
| ClientRpc | RPC do servidor para todos os clientes |
| NetworkVariable | Variavel que sincroniza automaticamente entre servidor e clientes |
| MPPM | Multiplayer Play Mode — ferramenta Unity para testar com 2+ instancias |
| EOS | Epic Online Services — autenticacao e lobby |
| P2P | Peer-to-peer — jogadores conectam diretamente, sem servidor dedicado |
| Late-join | Jogador que entra no meio da partida |
| Owner-authoritative | O dono do objeto controla o movimento (usado para jogadores) |
| Server-authoritative | O servidor controla (usado para inimigos, economia, dano) |
| LobbySceneUI | Interface canonica de lobby em Canvas (substitui LobbyPlaceholderUI deletado no Sprint 6) |
| MatchSessionLauncher | Orquestra StartHost/StartClient e publicacao de dados de conexao no lobby EOS |
| CharacterChoiceCache | Cache server-side da escolha de personagem de cada jogador ate o spawn |
| EosLobbyModHelper | Helpers internos para modificar atributos de lobby e membro via EOS (Sprint 7) |
| CenaMapaNOVO | Nome canônico atual da cena de jogo (o nome CenaMapaTeste e legado) |
| CenaSeleçao | Cena canonica de selecao de personagem carregada pelo NGO antes da partida |

## Docs relacionados

- `PADROES_NGO.md` — padroes e armadilhas especificos deste projeto que causaram bugs reais
- `Guia_Setup_Multiplayer_Cenas.md` — setup de cenas, prefabs e NetworkManager
- `ONBOARDING.md` — guia de primeiro acesso para devs novos
- `Estado_Atual_Multiplayer.md` — estado canonico do multiplayer (changelog detalhado)
- `CREDENTIALS_SETUP.md` — como configurar credenciais EOS
