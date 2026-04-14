# Plano de Implementação Multiplayer — ExoBeasts V3

## Especificações do Projeto

| Item | Valor |
|------|-------|
| **Engine** | Unity 6 (6000.0.52f1) |
| **Max Jogadores** | 4 jogadores |
| **Modelo de Conexão** | **P2P (Peer-to-Peer) com Host** |
| **Escopo** | Sincronização completa (lobby, movimento, combate, torres, inimigos, moedas, habilidades) |

---

## Visão Geral da Arquitetura — P2P

```
[Epic Online Services — Lobby Service]
       |
       v
[Client 1 - HOST] <----+
   (Servidor + Cliente) |
       ^                |
       |                |
       +---> [Client 2] (Apenas Cliente)
       +---> [Client 3] (Apenas Cliente)
       +---> [Client 4] (Apenas Cliente)
```

**Modelo P2P:**
- Um jogador atua como **Host** (servidor + cliente simultaneamente)
- O Host processa toda a lógica autoritativa do jogo
- Outros jogadores conectam diretamente ao Host
- Epic Lobby Service gerencia matchmaking e conexões iniciais
- NAT Traversal facilitado pelo Epic P2P Service

---

## Pacotes Instalados

| Pacote | Versão | Status |
|--------|--------|--------|
| `com.unity.netcode.gameobjects` | 1.12.0 | ✅ Instalado e funcional |
| `com.unity.transport` | 2.4.0 | ✅ Instalado e funcional |
| `com.unity.multiplayer.tools` | 2.2.1 | ✅ Instalado |
| `com.unity.multiplayer.center` | 1.0.0 | ✅ Instalado |
| `com.playeveryware.eos` | local | ✅ Instalado e funcional |

---

## FASE 1: Fundação e Configuração — ✅ CONCLUÍDA

### 1.1 Configuração no Epic Developer Portal — ✅

- [x] Conta de desenvolvedor Epic criada
- [x] Produto "ExoBeasts" configurado
- [x] Credenciais (ProductId, SandboxId, ClientId, DeploymentId) obtidas
- [x] Serviço **Lobbies** ativado
- [x] Serviço **Peer-to-peer** ativado (NAT traversal)
- [x] Arquivo `EOSCredentials.json` configurado na raiz do projeto
- [x] Arquivo no `.gitignore` (não comitado)

### 1.2 Instalação de Pacotes Unity — ✅

Todos os pacotes estão em `Packages/manifest.json`:

```json
"com.unity.netcode.gameobjects": "1.12.0",
"com.unity.transport": "2.4.0",
"com.unity.multiplayer.tools": "2.2.1",
"com.playeveryware.eos": "file:com.playeveryware.eos"
```

### 1.3 Estrutura de Pastas — ✅ Completa

```
Multiplayer/
├── Core/
│   ├── NetworkBootstrap.cs          ✅ Implementado (StartHost/StartClient reais)
│   ├── EOSManager.cs                ✅ Implementado (wrapper PlayEveryWare, auto-init)
│   ├── EOSConfig.cs                 ✅ Implementado (carrega credenciais do arquivo)
│   ├── HostManager.cs               ✅ Implementado (StartAsHost + StartAsClient)
│   ├── MppmHelper.cs                ✅ Implementado (detecção clone MPPM v1.6+)
│   └── WindowsPlatformSpecifics.cs  ✅ Implementado (cache EOS isolado por clone)
├── Auth/
│   ├── EOSAuthenticator.cs          ✅ Implementado (Device ID — funcional e testado)
│   └── SessionManager.cs            ✅ Implementado
├── Lobby/
│   ├── LobbyManager.cs              ✅ Implementado e testado (Março 2026)
│   ├── LobbyUI.cs                   ✅ Implementado (Canvas-based, aguarda artes)
│   ├── LobbyItemUI.cs               ✅ Implementado
│   └── LobbyData.cs                 ✅ Implementado (structs e constantes)
├── GameServer/
│   ├── GameServerManager.cs         🚧 Estrutura criada
│   ├── MatchManager.cs              🚧 Estrutura criada
│   └── PlayerRegistry.cs            🚧 Estrutura criada
├── Sync/
│   ├── NetworkedPlayerController.cs ✅ Implementado (NetworkBehaviour base)
│   ├── PlayerNetworkSetup.cs        ✅ Implementado (desabilita input remoto)
│   ├── NetworkedEnemy.cs            ✅ Implementado (classe base inimigo em rede)
│   ├── NetworkedHorde.cs            ✅ Implementado (gerenciamento de waves)
│   ├── NetworkedCurrency.cs         🚧 Estrutura criada
│   └── NetworkedBuilding.cs         🚧 Estrutura criada
└── Testing/
    ├── EOSAuthTest.cs               ✅ Funcional (testado)
    ├── NetworkConnectionTest.cs     ✅ Implementado e testado (Host/Client LAN)
    ├── NetworkedCubeMovement.cs     ✅ Implementado (movimento WASD p/ cubo de teste)
    └── LobbyPlaceholderUI.cs        ✅ Implementado e testado (UI OnGUI p/ LobbyScene)
```

### 1.4 Cenas do Projeto — ✅ Atualizadas

- [x] `EOSAuthTest.unity` — teste de autenticação EOS
- [x] `Network Test.unity` — teste de conexão P2P (cubos com movimento)
- [x] `LobbyScene.unity` — cena de lobby (criada, usando LobbyPlaceholderUI)
- [x] `SceneMapTest.unity` — mapa destino após conexão
- [ ] `NetworkBootstrap.unity` — aguardando integração com menu principal

---

## FASE 2: Autenticação — ✅ CONCLUÍDA (Device ID)

### 2.1 EOSManagerWrapper — ✅

**Arquivo:** `Core/EOSManager.cs`

- [x] Wrapper para PlayEveryWare EOSManager
- [x] Expõe `ConnectInterface`, `AuthInterface`, `PlatformInterface`
- [x] Carrega credenciais via `EOSConfig`
- [x] Singleton com DontDestroyOnLoad
- [x] `Start()` chama `Initialize()` automaticamente
- [x] Eventos `OnEOSInitialized` e `OnInitializationFailed` para observadores assíncronos

### 2.2 EOSAuthenticator — ✅

**Arquivo:** `Auth/EOSAuthenticator.cs`

**Método implementado:** Device ID (login anônimo)
- [x] `LoginWithDeviceId()` — cria Device ID único por máquina
- [x] Fluxo: CreateDeviceId → Login → (se novo) CreateUser → sucesso
- [x] Armazena `ProductUserId` localmente
- [x] Dispara `OnLoginSuccess` / `OnLoginFailed`
- [x] Integrado com `SessionManager`
- [x] `SetDeviceIdName(name)` — define nome de exibição antes do login (valida com `IsNullOrWhiteSpace`)

**Testado:** Login funcional confirmado em `EOSAuthTest.unity` e `LobbyScene.unity`

### 2.3 SessionManager — ✅

**Arquivo:** `Auth/SessionManager.cs`

- [x] Armazena userId, displayName, lobbyId, matchId
- [x] `StartSession()` / `EndSession()`
- [x] Singleton com DontDestroyOnLoad

### 2.4 UI de Login — 🚧 Pendente (formal)

- [ ] UI formal em `MenuScene.unity` (atualmente usando OnGUI de debug)
- Aguardando integração com fluxo de menu principal

---

## SPRINT: Validação de Rede Básica — ✅ CONCLUÍDA

- [x] Pacote de Netcode instalado — NGO 1.12.0 confirmado
- [x] NetworkManager configurado na cena — `Network Test.unity`
- [x] Sistema básico Host/Client — `NetworkConnectionTest.cs`
- [x] Prefab de teste — `PlayerTest.prefab` (cubo) com NetworkObject + ClientNetworkTransform
- [x] Movimento sincronizado — `NetworkedCubeMovement.cs` (WASD, apenas no owner)
- [x] Troca de cena sincronizada — NGO SceneManager carrega `SceneMapTest.unity` para todos
- [x] `PlayerNetworkSetup` — desabilita input/câmera/CharacterController no jogador remoto

---

## SPRINT: Base de IA para Inimigos — ✅ CONCLUÍDA

- [x] **NetworkedEnemy.cs** — classe base `NetworkBehaviour` para todos os inimigos
  - `NetworkVariable<float> CurrentHealth` (server-authoritative)
  - `NetworkVariable<int> State` (enum EnemyState: Idle, Chasing, Attacking, Dead)
  - `TakeDamageServerRpc(float damage, ulong attackerClientId)` — RequireOwnership = false
  - `Die()` → notifica NetworkedHorde, dispara `OnDiedClientRpc`, despawna após 2s
  - `RunAI()` virtual — executa apenas no servidor
- [x] **NetworkedHorde.cs** — gerenciamento de waves
  - `NetworkVariable<int> CurrentWave`, `EnemiesRemaining` (clampado em ≥ 0)
  - `OnEnemyKilledServerRpc()` — decrementa contador
  - `ForceStartNextWaveServerRpc()` — forçar próxima wave (debug)
  - `SpawnEnemy()` — placeholder (precisa de referência ao prefab de inimigo)

---

## FASE 3: Sistema de Lobby — ✅ IMPLEMENTADO E TESTADO

### 3.1 Estrutura de Dados — ✅

**Arquivo:** `Lobby/LobbyData.cs`

- [x] `LobbyInfo` — id, nome, host, hostProductUserId, players, mapa, público, estado
- [x] `LobbyMember` — userId, displayName, characterIndex, isReady, isHost
- [x] `LobbySettings`, `LobbySearchFilter`
- [x] `LobbyState` enum (WaitingForPlayers, SelectingCharacters, StartingMatch, InGame)
- [x] `LobbyAttributes` constants (LOBBY_NAME, MAP_NAME, MAX_PLAYERS, SERVER_ADDRESS, SERVER_PORT...)
- [x] `MemberAttributes` constants (DISPLAY_NAME, CHARACTER_INDEX, IS_READY, IS_HOST)

### 3.2 LobbyManager — ✅ Testado com chamadas EOS reais

**Arquivo:** `Lobby/LobbyManager.cs`

| Método | Status | Descrição |
|--------|--------|-----------|
| `CreateLobby(settings)` | ✅ Testado | `LobbyInterface.CreateLobby()` + SetLobbyAttributes + publica DISPLAY_NAME |
| `SearchLobbies(filter)` | ✅ Testado | `CreateLobbySearch` + `Find` + cache de LobbyDetails |
| `JoinLobby(lobbyId)` | ✅ Testado | Cache de LobbyDetails; fallback `SearchByIdThenJoin`; popula membros existentes |
| `LeaveLobby()` | ✅ EOS real | `LobbyInterface.LeaveLobby()` com guard de autenticação |
| `SetMemberAttribute(key, value)` | ✅ EOS real | `UpdateLobbyModification` + `AddMemberAttribute` com guard |
| `SetReady(bool)` | ✅ | Wrapper de `SetMemberAttribute(IS_READY)` |
| `SelectCharacter(int)` | ✅ | Wrapper de `SetMemberAttribute(CHARACTER_INDEX)` |
| `StartMatch()` | ✅ | `NetworkManager.StartHost()` + publica IP + LoadScene + rollback em falha |

**Métodos internos relevantes:**
- `SearchByIdThenJoin(id)` — busca EOS por ID exato (`LobbySearchSetLobbyIdOptions`) quando handle não está em cache
- `PopulateMembersFromDetails(details, hostId)` — itera membros existentes via `GetMemberByIndex` ao entrar no lobby
- `ReadMemberDisplayName(lobbyId, userId)` — lê atributo `DISPLAY_NAME` de um membro via `CopyMemberAttributeByKey`
- `PopulateLobbyInfoFromDetails(lobbyId, details, cb)` — lê `LobbyInfo` completo incluindo `hostProductUserId`

**Notificações EOS registradas** (após confirmação de init do EOS):
- `AddNotifyLobbyMemberStatusReceived` — detecta entradas/saídas em tempo real
- `AddNotifyLobbyUpdateReceived` — clientes detectam `SERVER_ADDRESS` e conectam automaticamente ao NGO Host

### 3.3 MppmHelper — ✅ Implementado (Março 2026, 2ª sessão)

**Arquivo:** `Core/MppmHelper.cs`

Utilitário estático para detecção de clone MPPM no Unity 6 (MPPM v1.6.3).

**Descoberta crítica:** O MPPM v1.6.3 **não usa** a variável de ambiente `UNITY_MULTIPLAYER_PLAY_MODE_PLAYER_INDEX`. A detecção correta é via **command-line args** passados ao processo clone:
- `--virtual-project-clone` → marca o processo como clone
- `-vpId={id}` → identificador estável de 8 chars hex (persiste entre sessões)

| Propriedade | Descrição |
|---|---|
| `MppmHelper.IsClone` | `true` quando rodando como Virtual Player |
| `MppmHelper.CloneId` | ID estável do clone (ex: `"a1b2c3d4"`), vazio se não for clone |

**Integração:**
- `WindowsPlatformSpecifics.GetTempDir()` retorna `eos_clone_{CloneId}/` (cache EOS isolado)
- `EOSAuthenticator.CreateDeviceIdAndLogin()` usa `_clone{CloneId}` no DeviceModel
- `LobbyPlaceholderUI` exibe auto-nome `Clone_{vpId4chars}` e debug MPPM em ciano

### 3.4 Bugs críticos corrigidos no LobbyManager — Março 2026

| Bug | Causa | Fix |
|-----|-------|-----|
| CS1739: parâmetro `isHost` inexistente | `LobbyMember(isHost: true)` → nome errado | `host: true` |
| Notificações nunca registradas | Race condition: EOS init assíncrono; `RegisterNotifications` chamado antes do SDK estar pronto | `Start()` subscreve `OnEOSInitialized` se `!IsInitialized` |
| Membros existentes não aparecem para quem entra | EOS não emite `Joined` para membros pré-existentes | `PopulateMembersFromDetails` itera `GetMemberByIndex` |
| `hostProductUserId` vazio para cliente | `PopulateLobbyInfoFromDetails` não lia `LobbyOwnerUserId` | Adicionado `result.hostProductUserId = di.Value.LobbyOwnerUserId?.ToString()` |
| Crash potencial em `LeaveLobby` sem auth | `GetLocalUserId()` sem validação | Guard `if (!localUserId.IsValid())` com limpeza de estado local |
| Rollback ausente em `StartMatch` | Se `UpdateLobby` falha após `StartHost`, host rodando sem clientes | `NetworkManager.Singleton.Shutdown()` no callback de erro |
| `SearchLobbies`/`JoinLobby` sem auth | EOS recebia `ProductUserId` inválido silenciosamente | Guards `if (!localUserId.IsValid())` em todos os métodos públicos |

### 3.5 UI de Lobby — ✅ Placeholder funcional e testado

**Placeholder (sem artes) — `Testing/LobbyPlaceholderUI.cs`:**
- [x] Tela Auth: input de nome + botão de login gateado por `_eosReady`
- [x] Tela Lista: busca por nome com filtro, lista scrollável, botão Entrar por item
- [x] Campo "ID:": entrar em qualquer lobby direto pelo ID (sem busca prévia)
- [x] Sub-painel Criar: nome, max jogadores (2-4), público/privado
- [x] Tela Sala: lista de membros populada de membros existentes + novos via evento
- [x] Detecção de host por `ProductUserId` (não por nome de exibição)
- [x] Referências cacheadas no `Start()` para evitar lazy-create em `OnDestroy`
- [x] Subscrição a `OnEOSInitialized` / `OnInitializationFailed`

**Canvas real (aguarda artes) — `Lobby/LobbyUI.cs` + `Lobby/LobbyItemUI.cs`:**
- [x] Estrutura de panels (CreateLobbyPanel, LobbyListPanel, LobbyRoomPanel)
- [x] `UpdateLobbyList()` — instancia lobbyItemPrefab por resultado
- [x] `RefreshPlayerSlots()` — reconstrói slots dinamicamente
- [x] Host-only Start button via SetActive
- [x] Subscrições completas: OnMemberJoined + OnMemberLeft em tempo real

**Teste end-to-end realizado (Março 2026):**
- [x] Host cria lobby → exibido na sala de espera com ID visível
- [x] Client entra por ID → membros existentes aparecem imediatamente
- [x] Host vê client entrar via notificação EOS
- [x] Nomes de exibição propagados como atributo de membro
- [x] Auditoria completa de código (Março 2026, 2ª sessão) — todos os fluxos EOS verificados, nenhum código alucinado encontrado

---

## FASE 4: Configuração P2P — 🚧 Parcialmente Concluída

### 4.1 Host e Client — ✅ Implementado (LAN)

**Arquivo:** `Core/HostManager.cs`

```csharp
public void StartAsHost()
{
    transport.SetConnectionData("0.0.0.0", hostPort);
    NetworkManager.Singleton.StartHost();
}

public void StartAsClient(string hostIp, ushort port = 0)
{
    transport.SetConnectionData(hostIp, port);
    NetworkManager.Singleton.StartClient();
}
```

### 4.2 GameServerManager — 🚧 Estrutura criada

**Arquivo:** `GameServer/GameServerManager.cs`

Responsabilidades (apenas no Host):
- Iniciar partida quando todos estiverem prontos
- Validar jogadores conectados
- Gerenciar estado da partida
- Processar lógica autoritativa

### 4.3 Epic P2P Service — 🚧 Pendente

**Objetivo:** Substituir UnityTransport padrão por EOS P2P Transport para NAT Traversal automático.

> **Status atual:** UnityTransport UDP direto (funciona em LAN).
> Para conexões via internet (jogadores em redes diferentes), é necessário configurar
> o EOS P2P Transport para NAT traversal automático.

---

## FASE 5: Sincronização de Gameplay — 🚧 PENDENTE

### 5.1 Player Prefab Networked — Tarefa no Editor (pendente)

`Player 1.prefab` (personagem real) precisa receber:
1. `NetworkObject`
2. `ClientNetworkTransform` (Interpolate=true, PositionThreshold=0.001)
3. `NetworkedPlayerController` (já existe em `Sync/`)
4. `PlayerNetworkSetup` (já existe em `Sync/`) — conectar refs no Inspector:
   - movement, cameraController, shooting, melee, combat, characterController
   - localOnlyObjects: CinemachineCamera, AudioListener, HUD Canvas

### 5.2 NetworkedPlayerController — ✅ Implementado (base)

**Arquivo:** `Sync/NetworkedPlayerController.cs`

```csharp
public class NetworkedPlayerController : NetworkBehaviour
{
    public NetworkVariable<float> NetworkHealth = new(writePerm: NetworkVariableWritePermission.Server);
    public NetworkVariable<int> NetworkAmmo = new(writePerm: NetworkVariableWritePermission.Server);
    public NetworkVariable<int> CharacterIndex = new(writePerm: NetworkVariableWritePermission.Owner);
}
```

### 5.3 PlayerNetworkSetup — ✅ Implementado

**Arquivo:** `Sync/PlayerNetworkSetup.cs`

- `IsOwner = true` → todos os scripts ficam habilitados normalmente
- `IsOwner = false` → desabilita: PlayerMovement, CameraController, PlayerShooting,
  MeleeCombatSystem, PlayerCombatManager, CharacterController
  e desativa: CinemachineCamera, AudioListener, HUD Canvas (via localOnlyObjects)

### 5.4 Modificações Pendentes nos Scripts do Jogador

```
PlayerMovement.cs       → mudar para NetworkBehaviour, if (!IsOwner) return no Update()
PlayerHealthSystem.cs   → NetworkVariable, TakeDamageServerRpc
PlayerShooting.cs       → ShootServerRpc
MeleeCombatSystem.cs    → MeleeAttackServerRpc
CurrencyManager.cs      → NetworkVariable (Geodites, DarkEther)
HordeManager.cs         → Servidor controla spawn de inimigos
BuildManager.cs         → PlaceTowerServerRpc, PlaceTrapServerRpc
```

---

## FASE 6: Polimento e Testes — 🚧 PENDENTE

### 6.1 Ferramentas de Teste

**Multiplayer Play Mode (MPPM)** — recomendado:
```
Window → Package Manager → Add by name: com.unity.multiplayer.playmode
Window → Multiplayer → Multiplayer Play Mode
```

### 6.2 Tratamento de Desconexão

```csharp
NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
NetworkManager.Singleton.OnServerStopped += OnServerStopped;

private void OnClientDisconnect(ulong clientId)
{
    if (clientId == NetworkManager.Singleton.LocalClientId)
    {
        ShowMessage("Desconectado do servidor");
        ReturnToMenu();
    }
}
```

### 6.3 Configuração de NetworkTransform

- Position Threshold: 0.001
- Rotation Threshold: 0.01
- Interpolate: true
- Use Quaternion Sync: true
- Use Unreliable Deltas: true (melhor para movimento)

---

## Checklist Geral de Verificação

### Fase 1 — Setup
- [x] Conta Epic Developer criada
- [x] Produto configurado no portal (Lobbies + P2P ativos)
- [x] NGO 1.12.0 instalado
- [x] EOS Plugin (PlayEveryWare) instalado
- [x] Projeto compila sem erros
- [x] Estrutura de pastas criada

### Fase 2 — Auth
- [x] EOS SDK inicializa corretamente (auto-init no `Start()`)
- [x] Login com Device ID funcional
- [x] ProductUserId exibido no console
- [x] SessionManager armazena dados da sessão
- [ ] UI de login integrada ao MenuScene (OnGUI temporário em uso)

### Sprint — Rede Básica
- [x] NetworkManager configurado na cena
- [x] UnityTransport configurado no NetworkManager
- [x] Player Prefab (cubo) com NetworkObject + ClientNetworkTransform
- [x] Host inicia com `StartHost()` funcional
- [x] Client conecta com `StartClient()` funcional
- [x] Cubo spawna para cada jogador conectado
- [x] Movimento WASD sincronizado (NetworkedCubeMovement)
- [x] PlayerNetworkSetup desabilita input/câmera no jogador remoto
- [x] Troca de cena sincronizada via NGO SceneManager

### Fase 3 — Lobby
- [x] LobbyManager com chamadas EOS reais
- [x] `CreateLobby` implementado e testado
- [x] `SearchLobbies` implementado e testado
- [x] `JoinLobby` implementado e testado (cache + fallback por ID)
- [x] `LeaveLobby` implementado com guards de auth
- [x] `SetMemberAttribute` implementado (IS_READY, CHARACTER_INDEX, DISPLAY_NAME)
- [x] `StartMatch`: StartHost + publica IP + LoadScene + rollback em falha
- [x] Notificações EOS (MemberStatus + LobbyUpdate) — registro adiado até EOS pronto
- [x] Clientes auto-conectam ao detectar SERVER_ADDRESS no lobby
- [x] `LobbyScene.unity` criada com `LobbyPlaceholderUI`
- [x] `LobbyPlaceholderUI.cs` (OnGUI) — testado com duas instâncias reais
- [x] `LobbyUI.cs` + `LobbyItemUI.cs` (Canvas, aguarda artes)
- [x] Membros existentes aparecem ao entrar (PopulateMembersFromDetails)
- [x] Nomes de exibição propagados como atributo de membro
- [x] Host detectado por ProductUserId (não nome)
- [x] Entrar por ID direto (campo ID: na UI)
- [x] Teste end-to-end: criar lobby → entrar por ID → membros visíveis ✅

### Fase 4 — P2P
- [x] `HostManager.StartAsHost()` funcional (LAN)
- [x] `HostManager.StartAsClient()` funcional (LAN)
- [ ] EOS P2P Transport configurado (para conexões via internet / NAT)
- [ ] Teste entre máquinas em redes diferentes (internet)

### Fase 5 — Gameplay
- [ ] Player 1.prefab configurado com componentes de rede
- [ ] Spawn de jogadores reais (não cubo) funciona
- [ ] Movimento sincronizado
- [ ] Combate ranged sincronizado
- [ ] Combate melee sincronizado
- [ ] Vida sincronizada
- [ ] Moedas sincronizadas
- [ ] Torres sincronizadas
- [ ] Inimigos sincronizados
- [ ] Waves sincronizadas
- [ ] Habilidades sincronizadas

### Fase 6 — Polimento
- [ ] Desconexão tratada (volta ao menu)
- [ ] Movimento suave com interpolação confirmado em partida real
- [ ] Sem erros no console em sessão completa
- [ ] Funciona entre máquinas em redes diferentes
- [ ] Performance aceitável (4 jogadores, 1 wave)

---

---

## Sessão Março 2026 — 2ª sessão: Limpeza + MPPM + Auditoria

### O que foi feito

**TAREFA 1 — Limpeza de código (todos os 24 scripts):**
- Removidos comentários que apenas repetiam o código (`// Eventos`, `// Getters`, `// Cleanup`, separadores `// ---`)
- Removidos blocos TODO e stubs de código comentado
- Adicionados cabeçalhos profissionais padronizados (`/// <summary>` com `── NomeDaClasse ──`, bullets `▸`, linha de fechamento)

**TAREFA 2 — Fix detecção MPPM:**
- Descoberto que `UNITY_MULTIPLAYER_PLAY_MODE_PLAYER_INDEX` não existe no MPPM v1.6.3
- Criado `Core/MppmHelper.cs` com detecção via command-line args (`--virtual-project-clone`, `-vpId=`)
- Atualizado `WindowsPlatformSpecifics`, `EOSAuthenticator`, `LobbyPlaceholderUI`
- Fix D aplicado em `LobbyUI.cs`: detecção de host por `hostProductUserId` (não `hostDisplayName`)

**Auditoria end-to-end do sistema de lobby:**
- Todos os 3 fluxos rastreados (criar, buscar+entrar, entrar por ID)
- Todas as chamadas EOS SDK verificadas como corretas
- Nenhum código alucinado encontrado

### Bugs encontrados na auditoria (não bloqueantes)

| Severidade | Arquivo | Descrição | Workaround |
|---|---|---|---|
| Cosmético | `LobbyManager.cs` | `hostDisplayName` recebe `productUserId` raw em `SearchLobbies` | Não afeta: UI usa `lobbyName`, não `hostDisplayName` |
| Cosmético | `LobbyData.cs` | Default `LobbySettings.mapName = "CenaMapaTeste"` mas cena real é `"SceneMapTest"` | LobbyPlaceholderUI sempre sobrescreve o valor |
| Ausente | `LobbyManager.cs` | Sem timeout em operações async EOS | Nenhum; UI trava se backend inacessível |
| UX | `LobbyManager.cs` | `_isInLobby = true` antes de `_currentLobby` ser atribuído | Coberto por null check em `SetMemberAttribute` |
| UX | `LobbyManager.cs` | `SetMemberAttribute` não atualiza `_members` localmente | OnGUI lê diretamente da lista; se fosse Canvas, seria bug |
| UX | `LobbyManager.cs` | Ordem de `_members` não determinística | Layout pode diferir entre host e cliente |

### Checklist adicional — Fase 3 (após 2ª sessão)

- [x] Todos os 24 scripts com cabeçalho profissional padronizado
- [x] `LobbyUI.cs`: host detection corrigida para `hostProductUserId`
- [x] `MppmHelper.cs` criado (detecção MPPM v1.6+ via command-line args)
- [x] `WindowsPlatformSpecifics.GetTempDir()` isola cache EOS por clone MPPM
- [x] `EOSAuthenticator` usa `MppmHelper.CloneId` no DeviceModel
- [x] `LobbyPlaceholderUI` usa `MppmHelper` para auto-nome e debug
- [x] Auditoria completa — fluxos EOS verificados, sem código alucinado
- [ ] Teste entre duas máquinas físicas (pendente pelo usuário)

---

## Próximos Passos Imediatos

1. **Testar lobby entre duas máquinas físicas** (prioridade máxima — valida todo o sistema)

2. **Configurar EOS P2P Transport** (Fase 4 — para internet / NAT entre redes diferentes)

3. **Configurar Player 1.prefab** (Fase 5 — tarefa no Editor)
   - Adicionar NetworkObject, ClientNetworkTransform, PlayerNetworkSetup
   - Conectar referências no Inspector

4. **Sincronizar PlayerMovement e PlayerHealthSystem** (Fase 5)
   - Mudar herança para NetworkBehaviour
   - Adicionar ServerRpcs para dano

5. **Adicionar timeout em operações async EOS** (bug identificado na auditoria de Março 2026)
   - `CreateLobby`, `JoinLobby`, `SearchLobbies` podem travar a UI se o backend EOS estiver inacessível

6. **UI de Login formal** em MenuScene.unity (substituir OnGUI de debug)

---

## Arquivos a Modificar — Fase 5

```
Assets/Codigo/Char scripts/Player/
  PlayerMovement.cs          → NetworkBehaviour, IsOwner check
  PlayerHealthSystem.cs      → NetworkVariable, TakeDamageServerRpc
  PlayerShooting.cs          → ShootServerRpc
  MeleeCombatSystem.cs       → MeleeAttackServerRpc

Assets/Codigo/Char scripts/JP/
  CommanderAbilityController.cs  → UseAbilityServerRpc

Assets/Codigo/Managers/
  CurrencyManager.cs         → NetworkVariable (Geodites, DarkEther)
  HordeManager.cs            → Servidor controla spawn de inimigos
  PauseControl.cs            → Pausar não funciona em rede (adaptar)

Assets/Codigo/Tower scripts/
  BuildManager.cs            → PlaceTowerServerRpc, PlaceTrapServerRpc

Assets/Modelos/PreFab/Entidades/
  Player 1.prefab            → NetworkObject + ClientNetworkTransform
  [prefabs de inimigos]      → NetworkObject
  [prefabs de torres]        → NetworkObject
```

---

## Riscos e Mitigações

| Risco | Probabilidade | Impacto | Mitigação |
|-------|---------------|---------|-----------|
| NAT Traversal falha em algumas redes | Média | Alto | EOS P2P Relay (fallback automático) |
| Bugs de sincronização | Alta | Médio | Testar incrementalmente com MPPM |
| Latência alta | Média | Médio | Interpolação no ClientNetworkTransform |
| Host desconecta durante partida | Baixa | Alto | Tratar `OnServerStopped` (voltar ao menu) |
