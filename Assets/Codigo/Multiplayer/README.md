# Sistema Multiplayer — ExoBeasts V3

> **Branch:** `Multiplayer`
> **Última atualização:** Março 2026
> **Plano detalhado:** `parallel-enchanting-harp.md`

---

## O que o sistema já faz

O multiplayer do ExoBeasts usa **P2P com Host** via Unity Netcode for GameObjects (NGO) e Epic Online Services (EOS) para matchmaking. Abaixo está tudo que está implementado e funcional.

---

## 1. Autenticação EOS — ✅ Funcional e testado

**Scripts:** `Auth/EOSAuthenticator.cs`, `Auth/SessionManager.cs`

O jogador faz login anônimo usando um **Device ID** — um identificador único gerado a partir da máquina, sem necessidade de conta Epic.

**Fluxo de login:**
```
SetDeviceIdName("NomeDoJogador")
LoginWithDeviceId()
  → CreateDeviceId (cria na primeira vez, reutiliza depois)
  → ConnectInterface.Login()
  → (se novo) CreateUser()
  → OnLoginSuccess(productUserId)
  → SessionManager.StartSession(userId, displayName)
```

**Como testar:** Abrir a cena `EOSAuthTest.unity` e clicar Login.

**Eventos disponíveis:**
- `EOSAuthenticator.OnLoginSuccess(string userId)`
- `EOSAuthenticator.OnLoginFailed(string error)`

---

## 2. Conexão P2P — ✅ Funcional e testado (LAN)

**Scripts:** `Core/HostManager.cs`, `Core/NetworkBootstrap.cs`
**Cena de teste:** `Network Test.unity`

Um jogador inicia como **Host** (servidor + cliente). Os outros entram como **Client**.

```
Host:   transport.SetConnectionData("0.0.0.0", 7777)
        NetworkManager.StartHost()

Client: transport.SetConnectionData("IP_DO_HOST", 7777)
        NetworkManager.StartClient()
```

**Como testar manualmente (sem Lobby):**
1. Abrir `Network Test.unity`
2. Instância A → clicar "Iniciar como HOST"
3. Instância B → digitar IP do Host → clicar "Entrar como CLIENT"
4. Host clica "Carregar cena de jogo" → ambos vão para `SceneMapTest.unity`

> **Nota:** Funciona em LAN. Para conexões via internet, a Fase 4 (EOS P2P Transport) ainda está pendente.

---

## 3. Prefab de Rede — ✅ Configurado (cubo de teste)

**Prefab:** `Models/PlayerTest.prefab`
**Scripts:** `Sync/NetworkedPlayerController.cs`, `Sync/PlayerNetworkSetup.cs`, `Testing/NetworkedCubeMovement.cs`

O prefab de teste é um **cubo** com todos os componentes de rede:

| Componente | Função |
|---|---|
| `NetworkObject` | Identifica o objeto como pertencente à rede |
| `ClientNetworkTransform` | Sincroniza posição — authoritative pelo dono (Interpolate=true) |
| `NetworkedPlayerController` | NetworkVariables de vida, munição, índice de personagem |
| `PlayerNetworkSetup` | Desabilita input e câmera nos jogadores remotos |
| `NetworkedCubeMovement` | Movimento WASD simples para testes |

**Regra crítica do `PlayerNetworkSetup`:**
- `IsOwner = true` → tudo habilitado normalmente
- `IsOwner = false` → desabilita PlayerMovement, CameraController, shooting, melee, combat, **CharacterController**

> O CharacterController deve ser desabilitado no jogador remoto — ele é "dono" da posição e impede o NetworkTransform de aplicar posições recebidas pela rede.

---

## 4. Sistema de Lobby EOS — ✅ Testado e funcional

**Scripts:** `Lobby/LobbyManager.cs`, `Lobby/LobbyData.cs`

O `LobbyManager` usa o Epic Online Services Lobby Service para criar salas de matchmaking onde os jogadores se encontram antes da partida.

### Fluxo completo Host → Client

```
HOST:
  LobbyManager.CreateLobby(settings)
    → EOS cria lobby com atributos: nome, mapa, max jogadores, estado
    → Publica DISPLAY_NAME do host como atributo de membro
    → OnLobbyCreated(lobby)

CLIENT:
  LobbyManager.SearchLobbies(filter)          ← busca por nome
    → EOS retorna lobbies públicos, caches LobbyDetails por ID
    → OnLobbiesFound(List<LobbyInfo>)

  -- OU --

  LobbyManager.JoinLobby(lobbyId)             ← entrar direto por ID
    → Se handle não está em cache: SearchByIdThenJoin(id)
    → Popula lista de membros existentes (EOS não emite Joined retroativamente)
    → Publica DISPLAY_NAME como atributo de membro
    → OnLobbyJoined(lobby)

AMBOS (quando prontos):
  LobbyManager.SetReady(true)
    → Publica IS_READY=True como atributo do membro no lobby

HOST (quando todos prontos):
  LobbyManager.StartMatch()
    → NetworkManager.StartHost()       (NGO inicia servidor)
    → Obtém IP local da máquina
    → Publica SERVER_ADDRESS + SERVER_PORT no lobby como atributos
    → Em caso de falha: NetworkManager.Shutdown() (rollback)

CLIENT (automático via notificação):
  OnLobbyAttributeUpdated detecta SERVER_ADDRESS
    → UnityTransport.SetConnectionData(ip, port)
    → NetworkManager.StartClient()     (conecta ao Host NGO)

NGO transporta todos para a cena de jogo automaticamente.
```

### Detalhes importantes de implementação

**Cache de LobbyDetails:**
O EOS SDK exige um handle `LobbyDetails` para chamar `JoinLobby` — não aceita apenas o ID como string. Por isso `SearchLobbies` armazena os handles em `_detailsCache`. Quando o cliente usa "Entrar por ID" diretamente (sem busca prévia), o método privado `SearchByIdThenJoin` usa `LobbySearchSetLobbyIdOptions` para buscar o handle por ID e então entrar.

**Inicialização assíncrona do EOS:**
O `EOSManagerWrapper` inicializa o EOS SDK via coroutine (`WaitForPlayEveryWareInit`). Para evitar registrar notificações antes do SDK estar pronto, o `LobbyManager.Start()` verifica `IsInitialized` e, se necessário, aguarda o evento `OnEOSInitialized` antes de chamar `RegisterNotifications()`.

**Membros existentes ao entrar:**
Quando um cliente entra em um lobby que já tem jogadores, o EOS não emite eventos `Joined` retroativamente para quem já estava na sala. O método `PopulateMembersFromDetails` itera `LobbyDetails.GetMemberByIndex` para montar a lista de membros atual no momento do join.

**Nome de exibição:**
- Host e cliente publicam `DISPLAY_NAME` como atributo de membro ao criar/entrar no lobby
- Quando um novo membro entra, o host lê o atributo via `CopyMemberAttributeByKey`
- Fallback: se o atributo ainda não propagou, exibe `Jogador_XXXXXXXX` (8 primeiros chars do ID)
- O fallback é corrigido automaticamente quando `SetMemberAttribute(DISPLAY_NAME)` propaga e dispara `OnMemberAttributeChanged`

**Detecção de Host:**
A detecção de quem é o host é feita comparando `lobby.hostProductUserId` com `SessionManager.GetUserId()` — não pelo nome de exibição (que pode colidir entre jogadores com o mesmo nome). Aplica-se tanto no `LobbyPlaceholderUI` quanto no `LobbyUI`.

### Eventos do LobbyManager

| Evento | Quando dispara |
|---|---|
| `OnLobbyCreated(LobbyInfo)` | Lobby criado com sucesso |
| `OnLobbiesFound(List<LobbyInfo>)` | Busca retornou resultados |
| `OnLobbyJoined(LobbyInfo)` | Entrou em um lobby |
| `OnLobbyLeft()` | Saiu do lobby |
| `OnMemberJoined(LobbyMember)` | Outro jogador entrou na sala |
| `OnMemberLeft(LobbyMember)` | Outro jogador saiu da sala |
| `OnMemberUpdated(LobbyMember)` | Atributo de membro mudou (ex: IS_READY) |
| `OnError(string)` | Qualquer falha EOS |

### Atributos armazenados no lobby EOS

**Atributos do lobby** (visíveis para todos):

| Chave | Tipo | Descrição |
|---|---|---|
| `LOBBY_NAME` | string | Nome da sala |
| `MAP_NAME` | string | Cena destino (ex: SceneMapTest) |
| `MAX_PLAYERS` | int64 | Máximo de jogadores (2-4) |
| `LOBBY_STATE` | string | WaitingForPlayers / InGame |
| `SERVER_ADDRESS` | string | IP do Host (publicado ao iniciar) |
| `SERVER_PORT` | int64 | Porta NGO (padrão 7777) |

**Atributos de membro** (por jogador):

| Chave | Tipo | Descrição |
|---|---|---|
| `DISPLAY_NAME` | string | Nome de exibição (publicado ao criar/entrar) |
| `IS_READY` | string | "True" / "False" |
| `CHARACTER_INDEX` | string | Índice do personagem selecionado |

---

## 5. UI de Lobby — ✅ Placeholder funcional e testado

**Script:** `Testing/LobbyPlaceholderUI.cs`
**Cena:** `LobbyScene.unity`

Interface de lobby sem Canvas, usando `OnGUI` do Unity. Adicionar em um GameObject vazio na `LobbyScene.unity`. Requer o **EOSManager** (PlayEveryWare) presente na cena.

**Telas:**

```
[Autenticação]
  Nome de exibição: [_______]
  [Aguardando EOS SDK...]  ← desabilitado até EOS inicializar
  [Login via Device ID]    ← habilitado quando EOS pronto

[Lista de Lobbies]
  Buscar: [filtro por nome] [Buscar]
  → Sala do João [1/4] [Entrar]
  → Sala Cheia   [4/4] [Cheio] (desabilitado)
  ID: [colar ID do lobby]   [Entrar]   ← entrar direto por ID
  [+ Criar Novo Lobby]

[Criar Lobby — sub-painel]
  Nome: [_______]
  Max jogadores: [−] 4 [+]
  Público: [✓ Sim]
  [Criar] [Cancelar]

[Sala de Espera]
  Sala: Nome do Lobby
  ID: 5da2dca4...
  1. João [Host] ✓  ◄ VOCE  (amarelo)
  2. Maria              (branco)
  3. — Aguardando —     (cinza)
  4. — Aguardando —     (cinza)

  [ ] Estou Pronto
  [Iniciar Partida]  ← apenas para o Host (detectado por ProductUserId)
  [Sair da Sala]
```

**Detalhes técnicos do LobbyPlaceholderUI:**
- Todas as referências de managers são cacheadas no `Start()` para evitar lazy-create durante `OnDestroy`
- O botão de login fica desabilitado até o evento `OnEOSInitialized` confirmar que o SDK está pronto
- Host detectado por `lobby.hostProductUserId == SessionManager.GetUserId()` (não por nome)
- Campo "ID:" permite entrar em qualquer lobby cujo ID seja conhecido, sem busca prévia
- Slot do jogador local marcado com `◄ VOCE` em amarelo
- Detecção de clone MPPM via `MppmHelper` (command-line args do Unity 6 MPPM v1.6+)
- Clones MPPM recebem auto-nome `Clone_{vpId4chars}` e painel debug exibe `MPPM Clone: {vpId}` em ciano

**Canvas real (aguarda artes):** `Lobby/LobbyUI.cs` + `Lobby/LobbyItemUI.cs` — toda a lógica está implementada, só falta conectar os objetos no Inspector quando as artes estiverem prontas.

---

## 6. MppmHelper — ✅ Detecção de MPPM para testes

**Script:** `Core/MppmHelper.cs`

Utilitário estático que detecta se o Editor está rodando como um **clone MPPM** (Virtual Player). Usado por `WindowsPlatformSpecifics`, `EOSAuthenticator` e `LobbyPlaceholderUI` para garantir IDs únicos por instância.

**Como funciona:**
O MPPM v1.6.3 (Unity 6) lança clones como processos separados com os argumentos de linha de comando:
- `--virtual-project-clone` → marca o processo como clone
- `-vpId={id}` → identificador estável de 8 chars (gerado na criação do Virtual Project, persiste entre sessões)

```csharp
// Uso:
bool isClone = MppmHelper.IsClone;   // true se Virtual Player
string id    = MppmHelper.CloneId;   // ex: "a1b2c3d4" (8 chars estáveis)
```

**Integração com EOS:**
- `WindowsPlatformSpecifics.GetTempDir()` retorna `eos_clone_{CloneId}/` — cache EOS isolado por clone
- `EOSAuthenticator.CreateDeviceIdAndLogin()` appende `_clone{CloneId}` ao `DeviceModel` — Device ID único por clone

> **Nota sobre MPPM com a mesma máquina:** Mesmo com o `MppmHelper`, testes MPPM na mesma máquina podem sofrer colisão de `ProductUserId` dependendo do estado do cache EOS em disco. Para resultados garantidamente corretos, use **duas máquinas físicas diferentes** — cada uma tem seu próprio cache EOS e Device ID naturalmente únicos.

---

## 7. Base de IA para Inimigos — ✅ Estrutura implementada

**Scripts:** `Sync/NetworkedEnemy.cs`, `Sync/NetworkedHorde.cs`

### NetworkedEnemy (classe base para todos os inimigos)

```csharp
// Herdar desta classe em vez de MonoBehaviour:
public class MeuInimigo : NetworkedEnemy { ... }
```

| Membro | Descrição |
|---|---|
| `NetworkVariable<float> CurrentHealth` | Vida sincronizada (server-authoritative) |
| `NetworkVariable<int> State` | Estado: Idle / Chasing / Attacking / Dead |
| `TakeDamageServerRpc(float, ulong)` | Recebe dano de qualquer jogador |
| `RunAI()` virtual | Executa só no servidor — sobrescrever com lógica da IA |
| `Die()` | Notifica NetworkedHorde, toca animação em todos, despawna em 2s |

### NetworkedHorde (gerenciador de waves)

| Membro | Descrição |
|---|---|
| `NetworkVariable<int> CurrentWave` | Wave atual |
| `NetworkVariable<int> EnemiesRemaining` | Inimigos vivos (clampado em ≥ 0) |
| `OnEnemyKilledServerRpc()` | Chamado quando inimigo morre |
| `ForceStartNextWaveServerRpc()` | Força próxima wave (debug) |
| `SpawnEnemy()` | **Placeholder** — precisa de referência ao prefab |

---

## Como testar o sistema completo

### Entre duas máquinas físicas (recomendado)

1. **Máquina A (Host):**
   - Abrir `LobbyScene.unity` → Play
   - Digitar nome → Login
   - Criar lobby (nome, max 4, público)
   - **Copiar o ID exibido na sala de espera** (botão "Copiar ID")

2. **Máquina B (Client):**
   - Abrir `LobbyScene.unity` → Play
   - Digitar nome → Login
   - Colar o ID no campo "ID:" → clicar Entrar

3. **Ambos:** Marcar "Estou Pronto"

4. **Host:** Clicar "Iniciar Partida"
   - NGO sobe como Host, publica IP no lobby
   - Client detecta IP e conecta automaticamente
   - Ambos vão para `SceneMapTest.unity`

### Com MPPM (mesma máquina)

```
Window → Multiplayer → Multiplayer Play Mode
```

1. Ativar 1 clone virtual no painel MPPM
2. Entrar em Play Mode
3. Verificar no Console: `[MppmHelper] Clone MPPM detectado. vpId: ...`
4. O clone deve receber auto-nome `Clone_{vpId}` na tela de Auth
5. Criar lobby na instância principal, entrar pelo ID no clone

> **Atenção:** Testes MPPM na mesma máquina podem resultar no mesmo `ProductUserId` entre instâncias se o cache EOS já existir do clone anterior. Se isso ocorrer, apague a pasta `eos_clone_*` em `Application.temporaryCachePath` e reinicie o Play Mode.

---

## Bugs conhecidos (não bloqueantes)

| Severidade | Arquivo | Descrição | Impacto |
|---|---|---|---|
| Cosmético | `LobbyManager.cs` | `hostDisplayName` recebe `productUserId` raw nos resultados de `SearchLobbies` | Não afeta: a lista de lobbies exibe `lobbyName`, não o nome do host |
| Cosmético | `LobbyData.cs` | Default de `LobbySettings.mapName` é `"CenaMapaTeste"` mas a cena real é `"SceneMapTest"` | Só afeta se novo código usar o default do struct sem sobrescrever |
| Ausente | `LobbyManager.cs` | Nenhum timeout em operações async EOS (`CreateLobby`, `JoinLobby`, etc.) | UI trava em "Criando lobby..." se EOS backend estiver inacessível |
| UX | `LobbyManager.cs` | `_isInLobby = true` é setado antes de `_currentLobby` ser atribuído | Janela de ~1 frame onde `isInLobby && currentLobby == null`; coberto por null check |
| UX | `LobbyManager.cs` | `SetMemberAttribute` não atualiza `_members` localmente — aguarda callback EOS | O toggle "Estou Pronto" pode ter lag de 1-2 frames antes do checkmark aparecer |
| UX | `LobbyManager.cs` | `_members` não tem ordem garantida — depende do EOS | Layout de slots pode ser diferente entre o host e o cliente |

---

## Bugs corrigidos (histórico)

### Sessão de auditoria — Março 2026 (1ª sessão)

| ID | Arquivo | Descrição |
|---|---|---|
| CRÍTICO | `LobbyManager.cs` | Parâmetro `isHost:` → `host:` no construtor de `LobbyMember` (CS1739) |
| CRÍTICO | `LobbyManager.cs` | Notificações EOS nunca registradas (race condition: EOS init assíncrono) |
| CRÍTICO | `LobbyManager.cs` | Membros existentes não apareciam ao entrar — `PopulateMembersFromDetails` adicionado |
| HIGH | `LobbyManager.cs` | `hostProductUserId` não populado em `PopulateLobbyInfoFromDetails` |
| HIGH | `LobbyManager.cs` | Auth guards ausentes em `SearchLobbies`, `JoinLobby`, `LeaveLobby`, `SetMemberAttribute` |
| HIGH | `LobbyManager.cs` | `StartMatch` sem rollback quando `UpdateLobby` falha após `StartHost` |
| MEDIUM | `LobbyPlaceholderUI.cs` | Detecção de host por `displayName` → corrigido para `ProductUserId` |
| MEDIUM | `LobbyPlaceholderUI.cs` | `UnsubscribeFromEvents` em `OnDestroy` criava novos GameObjects via `.Instance` |
| MEDIUM | `EOSManager.cs` | `Initialize()` nunca chamada automaticamente |
| LOW | `NetworkedHorde.cs` | `EnemiesRemaining` podia ficar negativo |
| LOW | `PlayerRegistry.cs` | Double-destroy: `Despawn()` + `Destroy()` redundante |
| LOW | `EOSAuthenticator.cs` | `SetDeviceIdName` aceitava strings de espaço como nome válido |

### Sessão de limpeza + MPPM — Março 2026 (2ª sessão)

| ID | Arquivo | Descrição |
|---|---|---|
| INFRA | Todos os 24 scripts | Limpeza de comentários redundantes + cabeçalhos profissionais padronizados |
| MEDIUM | `LobbyUI.cs` | Detecção de host por `hostDisplayName` → corrigido para `hostProductUserId` |
| INVESTIGAÇÃO | `EOSAuthenticator.cs` + `WindowsPlatformSpecifics.cs` | Root cause do UserId colidente no MPPM: env var `UNITY_MULTIPLAYER_PLAY_MODE_PLAYER_INDEX` não existe no MPPM v1.6.3 — criado `MppmHelper.cs` com detecção correta via command-line args |
| NOVO | `Core/MppmHelper.cs` | Novo utilitário para detecção de clone MPPM via `--virtual-project-clone` e `-vpId=` |

---

## Estrutura de pastas completa

```
Assets/Codigo/Multiplayer/
├── Core/
│   ├── NetworkBootstrap.cs        — inicia Host/Client ao entrar na cena de jogo
│   ├── EOSManager.cs              — wrapper do PlayEveryWare EOSManager (auto-init)
│   ├── EOSConfig.cs               — carrega credenciais do EOSCredentials.json
│   ├── HostManager.cs             — StartAsHost() / StartAsClient()
│   ├── MppmHelper.cs              — detecção de clone MPPM via command-line args
│   └── WindowsPlatformSpecifics.cs — cache EOS isolado por clone MPPM
│
├── Auth/
│   ├── EOSAuthenticator.cs        — login anônimo via Device ID (MppmHelper-aware)
│   └── SessionManager.cs          — armazena userId, displayName, lobbyId
│
├── Lobby/
│   ├── LobbyManager.cs            — CRUD de lobby com chamadas EOS reais
│   ├── LobbyData.cs               — structs: LobbyInfo, LobbyMember, constantes
│   ├── LobbyUI.cs                 — Canvas UI (aguarda artes)
│   └── LobbyItemUI.cs             — item de lista de lobby
│
├── GameServer/
│   ├── GameServerManager.cs       — gerencia estado da partida (estrutura)
│   ├── MatchManager.cs            — controla fluxo da partida (estrutura)
│   └── PlayerRegistry.cs          — mapeia clientId → GameObject (estrutura)
│
├── Sync/
│   ├── NetworkedPlayerController.cs — NetworkVariables de vida, munição, personagem
│   ├── PlayerNetworkSetup.cs      — desabilita input/câmera em jogadores remotos
│   ├── NetworkedEnemy.cs          — classe base NetworkBehaviour para inimigos
│   ├── NetworkedHorde.cs          — gerencia waves de inimigos
│   ├── NetworkedCurrency.cs       — moedas compartilhadas (estrutura)
│   └── NetworkedBuilding.cs       — torres/traps em rede (estrutura)
│
└── Testing/
    ├── EOSAuthTest.cs             — UI de teste de autenticação EOS
    ├── NetworkConnectionTest.cs   — UI de teste Host/Client manual (LAN)
    ├── NetworkedCubeMovement.cs   — movimento WASD para o cubo de teste
    └── LobbyPlaceholderUI.cs      — UI completa de lobby sem Canvas (testada)
```

---

## Próximos passos

| Prioridade | Tarefa |
|---|---|
| 1 | **Testar lobby entre duas máquinas físicas** — validar fluxo completo host→client em redes diferentes |
| 2 | Configurar EOS P2P Transport (NAT para conexões via internet entre redes diferentes) |
| 3 | Configurar `Player 1.prefab` com componentes de rede no Editor |
| 4 | Mudar `PlayerMovement` e `PlayerHealthSystem` para `NetworkBehaviour` |
| 5 | Sincronizar combate (ShootServerRpc, MeleeAttackServerRpc) |
| 6 | Adicionar timeout nas operações async EOS (atualmente a UI trava se o backend falha) |
| 7 | UI de Login formal em MenuScene.unity (substituir OnGUI de debug) |
