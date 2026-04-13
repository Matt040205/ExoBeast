# Guia de Setup Multiplayer — Configuracao de Cenas e Prefabs
**ExoBeasts V3 — Projeto PI3D**
**Data:** Marco 2026

---

## Indice
1. [Visao Geral do Fluxo](#1-visao-geral-do-fluxo)
2. [Passo 1: Player 1.prefab](#2-passo-1-player-1prefab)
3. [Passo 2: SceneMapTest.unity — Hierarquia](#3-passo-2-scenemaptestunity--hierarquia)
4. [Passo 3: NetworkManager — Registrar Prefabs](#4-passo-3-networkmanager--registrar-prefabs)
5. [Passo 4: Build Settings](#5-passo-4-build-settings)
6. [Passo 5: Testar com MPPM](#6-passo-5-testar-com-mppm)
7. [Erros de Cache Unity (ILPP)](#7-erros-de-cache-unity-ilpp)
8. [Troubleshooting](#8-troubleshooting)
9. [Referencia Rapida — Componentes por GameObject](#9-referencia-rapida--componentes-por-gameobject)

---

## 1. Visao Geral do Fluxo

```
LobbyScene.unity                    SceneMapTest.unity
┌─────────────────────┐              ┌─────────────────────────────────┐
│  EOSManager         │──persiste──→│                                 │
│  EOSAuthenticator   │  (DontDe-   │  [GameManager]                  │
│  SessionManager     │  stroyOn-   │    NetworkObject                │
│  LobbyManager       │  Load)      │    GameSetupManager             │
│  NetworkManager     │             │    MatchManager                 │
│  HostManager        │             │    CurrencyManager              │
│  NetworkBootstrap   │             │                                 │
│                     │             │  [NetworkSystems]               │
│  LobbyPlaceholder   │             │    NetworkObject                │
│  UI (lobby local)   │             │    PlayerRegistry               │
│                     │             │    GameServerManager            │
│  Botao StartMatch ──┼──carrega──→│    PlayerIdentityBridge         │
│                     │             │                                 │
└─────────────────────┘             │  [HordeSystem]                  │
                                    │    NetworkObject                │
 Objetos que PERSISTEM              │    HordeManager                 │
 entre cenas (lobby→jogo):          │    EnemyPoolManager             │
  • NetworkManager                  │                                 │
  • EOSManager                      │  [Objective] (cristal/base)     │
  • EOSAuthenticator                │    NetworkObject                │
  • SessionManager                  │    ObjectiveHealthSystem        │
  • LobbyManager                    │    Collider + Mesh              │
  • HostManager                     │                                 │
  • NetworkBootstrap                │  SpawnPoint_1..4 (transforms)   │
                                    │  EnemySpawnPoints               │
                                    │  Terreno, Luzes, etc.           │
                                    └─────────────────────────────────┘
```

**IMPORTANTE:** O `NetworkManager` (da Unity) persiste entre cenas via DontDestroyOnLoad.
Ele NAO precisa existir na SceneMapTest — ele ja vem da LobbyScene.

---

## 2. Passo 1: Player 1.prefab

### Onde fica
`Assets/Prefabs/Player 1.prefab` (ou onde quer que esteja no projeto)

### Componentes que JA EXISTEM (nao mexer)
- `CharacterController`
- `Animator`
- `PlayerMovement` (NetworkBehaviour)
- `PlayerHealthSystem` (NetworkBehaviour)
- `PlayerShooting` (NetworkBehaviour)
- `MeleeCombatSystem` (NetworkBehaviour)
- `PlayerCombatManager` (NetworkBehaviour)

### Componentes que voce PRECISA ADICIONAR

| Componente | Configuracao |
|---|---|
| **NetworkObject** | Add Component → Netcode → NetworkObject. Deixar padrao. |
| **ClientNetworkTransform** | Add Component → Netcode → ClientNetworkTransform. Configurar: `Interpolate = true`, `Position Threshold = 0.001`, `Rotation Threshold = 0.01` |
| **NetworkAnimator** | Add Component → Netcode → NetworkAnimator. Arrastar o `Animator` do player para o campo "Animator". |
| **PlayerNetworkSetup** | Add Component → buscar "PlayerNetworkSetup". Arrastar as referencias no Inspector (ver abaixo). |

### Configurar PlayerNetworkSetup no Inspector

Abra o componente `PlayerNetworkSetup` e arraste:

| Campo | Arrastar... |
|---|---|
| Movement | O componente `PlayerMovement` do mesmo GameObject |
| Camera Controller | O componente `CameraController` (ou `ThirdPersonCamera`) |
| Character Controller | O componente `CharacterController` do mesmo GameObject |
| Player Shooting | O componente `PlayerShooting` do mesmo GameObject |
| Melee Combat | O componente `MeleeCombatSystem` do mesmo GameObject |
| Player Combat Manager | O componente `PlayerCombatManager` do mesmo GameObject |
| Local Only Objects | Arrastar GameObjects que so devem aparecer para o jogador local (ex: camera pessoal, HUD 3D, mira) |

### O que NAO colocar no prefab
- **NAO adicionar `NetworkedPlayerController`** — ele tem um sistema de vida duplicado que conflita com `PlayerHealthSystem`
- **NAO adicionar `NetworkTransform` normal** — o jogador usa `ClientNetworkTransform` (owner-authoritative)

---

## 3. Passo 2: SceneMapTest.unity — Hierarquia

Abra a cena `Assets/Codigo/Multiplayer/SceneMapTest.unity` e crie os seguintes GameObjects:

### 3.1 [GameManager]
1. Criar GameObject vazio → renomear para `GameManager`
2. Add Component: **NetworkObject**
3. Add Component: **GameSetupManager**
   - `Character Prefabs` (array): Arrastar `Player 1.prefab` no slot [0]
   - `Spawn Points` (array): Arrastar os 4 transforms de spawn (criar a seguir)
4. Add Component: **MatchManager** (sem configuracao extra)
5. Add Component: **CurrencyManager** (sem configuracao extra)

### 3.2 [NetworkSystems]
1. Criar GameObject vazio → renomear para `NetworkSystems`
2. Add Component: **NetworkObject**
3. Add Component: **PlayerRegistry** (sem configuracao extra)
4. Add Component: **GameServerManager** (sem configuracao extra)
5. Add Component: **PlayerIdentityBridge** (sem configuracao extra)

### 3.3 [HordeSystem]
1. Criar GameObject vazio → renomear para `HordeSystem`
2. Add Component: **NetworkObject**
3. Add Component: **HordeManager**
   - Configurar campos de horda (dados de ondas, referencia a EnemyPoolManager, spawn points de inimigos)
4. Add Component: **EnemyPoolManager**
   - Configurar `Initial Pool Size` (ex: 20)

> **NOTA:** EnemyPoolManager eh MonoBehaviour, NAO precisa de NetworkObject proprio.
> Ele mora no mesmo GameObject do HordeManager que JA tem NetworkObject.

### 3.4 [Objective]
1. Criar GameObject (cubo/esfera ou modelo do cristal) → renomear para `Objective`
2. Add Component: **NetworkObject**
3. Add Component: **ObjectiveHealthSystem**
   - `Max Health`: 1000 (ou o valor desejado)
4. Add Component: **Collider** (se ainda nao tiver) — para inimigos detectarem onde atacar
5. Posicionar onde a base/cristal deve ficar no mapa

### 3.5 SpawnPoints (4 transforms)
1. Criar 4 GameObjects vazios:
   - `SpawnPoint_1`, `SpawnPoint_2`, `SpawnPoint_3`, `SpawnPoint_4`
2. Posicionar onde os jogadores devem nascer (espaçados)
3. Arrastar todos os 4 para o array `Spawn Points` do `GameSetupManager`

> **DICA:** Adicione um icone colorido (clique no cubo de cor no Inspector)
> para visualizar os spawn points na Scene view.

### 3.6 EnemySpawnPoints
1. Criar quantos GameObjects vazios precisar para spawn de inimigos
2. Arrastar para o campo correspondente do `HordeManager`

### Hierarquia Final da Cena

```
SceneMapTest (cena)
├── GameManager            [NetworkObject, GameSetupManager, MatchManager, CurrencyManager]
├── NetworkSystems         [NetworkObject, PlayerRegistry, GameServerManager, PlayerIdentityBridge]
├── HordeSystem            [NetworkObject, HordeManager, EnemyPoolManager]
├── Objective              [NetworkObject, ObjectiveHealthSystem, Collider, Mesh]
├── SpawnPoint_1           [Transform vazio]
├── SpawnPoint_2           [Transform vazio]
├── SpawnPoint_3           [Transform vazio]
├── SpawnPoint_4           [Transform vazio]
├── EnemySpawn_1           [Transform vazio]
├── EnemySpawn_2           [Transform vazio]
├── Directional Light      [ja existe]
├── Terrain / Chao         [ja existe]
└── (outros objetos de cenario)
```

---

## 4. Passo 3: NetworkManager — Registrar Prefabs

O `NetworkManager` fica na **LobbyScene** (persiste entre cenas). Voce precisa registrar TODOS os prefabs que serao spawnados pela rede.

### Como registrar:
1. Abrir `LobbyScene.unity`
2. Selecionar o GameObject que tem o componente `NetworkManager`
3. No Inspector, expandir **NetworkManager → NetworkConfig → Prefab List**
4. Clicar "+" e arrastar cada prefab:

| Prefab | Obrigatorio? | Motivo |
|---|---|---|
| **Player 1.prefab** | SIM | Spawnado pelo GameSetupManager |
| **Cada prefab de inimigo** | SIM | Spawnados pelo EnemyPoolManager |
| **Cada prefab de torre** (se tiver NetworkObject) | SIM | Spawnados pelo BuildManager |
| **Cada prefab de armadilha** (se tiver NetworkObject) | SIM | Spawnados pelo BuildManager |

> **REGRA:** Todo GameObject que chama `NetworkObject.Spawn()` no codigo
> PRECISA estar nesta lista. Se nao estiver, o erro sera:
> `"NetworkPrefab not found for hash XXXX"` e o objeto nao aparece no cliente.

### Prefabs de inimigo — como encontrar
Os prefabs de inimigos sao referenciados nos `EnemyDataSO` (ScriptableObjects em `Assets/`).
Cada um tem um campo `enemyPrefab`. Arraste esses mesmos prefabs para a Prefab List.

---

## 5. Passo 4: Build Settings

1. **File → Build Settings**
2. Garantir que TODAS estas cenas estao na lista (arrastar do Project):
   - `LobbyScene` (index 0 ou qualquer)
   - `SceneMapTest` (OBRIGATORIO — sera carregada via NGO)
   - `Win` (cena de vitoria)
   - `Lose` (cena de derrota)
3. A cena que o NGO carrega via `NetworkManager.SceneManager.LoadScene("SceneMapTest")` PRECISA estar no Build Settings, senao o erro eh silencioso e a cena simplesmente nao carrega.

---

## 6. Passo 5: Testar com MPPM

### Pre-requisitos
- MPPM (Multiplayer Play Mode) v1.6.3 instalado
- Pelo menos 1 clone virtual configurado

### Passo a passo
1. **Window → Multiplayer Play Mode** → Ativar 1 clone
2. Abrir a `LobbyScene` como cena ativa
3. Clicar **Play** no Editor principal
4. No Editor: clicar "Login" → "Create Lobby"
5. Copiar o Lobby ID (botao "Copiar ID")
6. No clone MPPM: clicar "Login" → colar ID no campo "ID:" → "Join by ID"
7. Verificar que ambos aparecem na lista de membros
8. No Editor (host): clicar "Start Match"
9. Ambos devem carregar `SceneMapTest` e spawnar jogadores

### O que verificar
- [ ] Ambos os jogadores aparecem na cena?
- [ ] Cada jogador se move independentemente?
- [ ] Mover o jogador A — o jogador B ve o movimento?
- [ ] Atirar com jogador A — o jogador B ve o tiro?
- [ ] Inimigos spawnam (se HordeManager esta configurado)?
- [ ] Console sem erros vermelhos criticos?

---

## 7. Erros de Cache Unity (ILPP)

Se voce ver erros como:
```
NetworkBehaviourILPP: ... TriggerHitVisualClientRpc ... must be marked with 'ClientRpc' attribute!
```

Isto eh cache antigo do compilador Unity. O codigo-fonte esta correto mas o Unity ainda tem uma versao compilada antiga em cache.

### Solucao:
1. **Fechar Unity completamente**
2. Navegar ate a pasta do projeto: `ExoBeasts_V3/PI3D/`
3. **Deletar a pasta `Library/`** inteira
4. Reabrir o projeto no Unity — ele vai reimportar tudo (demora ~2-5 min)

> **NOTA:** Deletar Library/ eh seguro. Ela eh recriada automaticamente.
> Nenhum asset, codigo ou configuracao de cena eh perdido.
> So nao delete `Assets/`, `Packages/` ou `ProjectSettings/`.

---

## 8. Troubleshooting

### "Player nao aparece apos Start Match"
- Verificar que `Player 1.prefab` tem `NetworkObject`
- Verificar que `Player 1.prefab` esta na Prefab List do NetworkManager
- Verificar que `GameSetupManager` existe na SceneMapTest com `characterPrefabs[0]` preenchido
- Verificar que `SpawnPoints` estao arrastados no GameSetupManager

### "Jogador spawna mas nao se move"
- Verificar que `ClientNetworkTransform` esta no prefab
- Verificar que `PlayerNetworkSetup` esta no prefab e as refs estao arrastadas
- Verificar no Console se aparece "[PlayerNetworkSetup] Jogador LOCAL inicializado"

### "Inimigos nao spawnam"
- Verificar que `HordeManager` existe na cena com `NetworkObject`
- Verificar que `EnemyPoolManager` esta configurado
- Verificar que os prefabs de inimigo estao na Prefab List do NetworkManager
- Verificar que `PlayerRegistry` existe na cena (HordeManager espera jogadores)

### "Erro: NetworkPrefab not found"
- O prefab que esta sendo spawnado nao esta registrado no NetworkManager
- Abrir LobbyScene → NetworkManager → Prefab List → adicionar o prefab faltante

### "Erro: Multiple NetworkObjects on same GameObject"
- Um prefab tem mais de um `NetworkObject`. Remover o duplicado.
- Filhos do prefab NAO devem ter `NetworkObject` proprio (a menos que sejam nested prefabs intencionais)

### "Erro: Can't write to NetworkVariable (non-server)"
- Um cliente esta tentando alterar um valor que so o servidor pode alterar
- Verificar que o codigo tem `if (!IsServer) return;` antes de alterar NetworkVariables

### "Camera piscando / multiplos AudioListeners"
- Cameras e AudioListeners de jogadores remotos nao estao sendo desativados
- Verificar que `PlayerNetworkSetup` esta desabilitando `cameraController` para `!IsOwner`
- Adicionar a camera e AudioListener ao array `localOnlyObjects`

---

## 9. Referencia Rapida — Componentes por GameObject

### Player 1.prefab
```
✅ Ja existe          ➕ Adicionar
─────────────────────────────────
✅ CharacterController  ➕ NetworkObject
✅ Animator             ➕ ClientNetworkTransform
✅ PlayerMovement       ➕ NetworkAnimator
✅ PlayerHealthSystem   ➕ PlayerNetworkSetup
✅ PlayerShooting
✅ MeleeCombatSystem
✅ PlayerCombatManager

❌ NAO ADICIONAR: NetworkedPlayerController, NetworkTransform (normal)
```

### SceneMapTest — GameManager
```
➕ NetworkObject
➕ GameSetupManager     → characterPrefabs[], spawnPoints[]
➕ MatchManager
➕ CurrencyManager
```

### SceneMapTest — NetworkSystems
```
➕ NetworkObject
➕ PlayerRegistry
➕ GameServerManager
➕ PlayerIdentityBridge
```

### SceneMapTest — HordeSystem
```
➕ NetworkObject
➕ HordeManager         → dados de onda, spawn points de inimigos
➕ EnemyPoolManager     → initialPoolSize
```

### SceneMapTest — Objective
```
➕ NetworkObject
➕ ObjectiveHealthSystem → maxHealth
➕ Collider
```

### LobbyScene — NetworkManager
```
✅ Ja existe
➕ Prefab List: Player 1.prefab + todos os enemy prefabs + tower prefabs
```

---

**Apos completar todos os passos, salve TODAS as cenas (Ctrl+S em cada uma) e tente o fluxo com MPPM.**

Se aparecerem erros de ILPP (cache), delete a pasta `Library/` conforme Secao 7.
