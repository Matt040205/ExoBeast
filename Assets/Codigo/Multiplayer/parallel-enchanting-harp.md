# Plano de Implementacao Multiplayer - ExoBeasts V3

## Especificacoes do Projeto

| Item | Valor |
|------|-------|
| **Engine** | Unity 6 (6000.0.52f1) |
| **Max Jogadores** | 4 jogadores |
| **Modelo de Conexao** | **P2P (Peer-to-Peer) com Host** |
| **Prazo** | 1-2 meses |
| **Escopo** | Sincronizacao completa (lobby, movimento, combate, torres, inimigos, moedas, habilidades) |

---

## Visao Geral da Arquitetura - P2P

```
[Epic Online Services - Lobby Service]
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

**Modelo P2P (Peer-to-Peer):**
- Um jogador atua como **Host** (servidor + cliente simultaneamente)
- O Host processa toda a lógica autoritativa do jogo
- Outros jogadores conectam diretamente ao Host
- Epic Lobby Service gerencia matchmaking e conexões iniciais
- NAT Traversal facilitado pelo Epic P2P Service

**Vantagens do P2P:**
- ✅ Sem custo de servidor dedicado
- ✅ Setup mais simples
- ✅ Melhor para grupos pequenos (2-4 jogadores)
- ✅ Latência menor para jogadores próximos ao Host

**Considerações:**
- ⚠️ Host precisa de conexão estável
- ⚠️ Se Host desconectar, partida termina (Host Migration pode ser implementado depois)

---

## Pacotes Instalados (versoes reais)

| Pacote | Versao | Status |
|--------|--------|--------|
| `com.unity.netcode.gameobjects` | 1.12.0 | ✅ Instalado e funcional |
| `com.unity.transport` | 2.4.0 | ✅ Instalado e funcional |
| `com.unity.multiplayer.tools` | 2.2.1 | ✅ Instalado |
| `com.unity.multiplayer.center` | 1.0.0 | ✅ Instalado |
| `com.playeveryware.eos` | local | ✅ Instalado e funcional |

---

## FASE 1: Fundacao e Configuracao — ✅ CONCLUIDA

### 1.1 Configuracao no Epic Developer Portal — ✅

- [x] Conta de desenvolvedor Epic criada
- [x] Produto "ExoBeasts" configurado
- [x] Credenciais (ProductId, SandboxId, ClientId, DeploymentId) obtidas
- [x] Servico **Lobbies** ativado
- [x] Servico **Peer-to-peer** ativado (NAT traversal)
- [x] Arquivo `EOSCredentials.json` configurado na raiz do projeto
- [x] Arquivo no `.gitignore` (nao comitado)

### 1.2 Instalacao de Pacotes Unity — ✅

Todos os pacotes estao em `Packages/manifest.json`:

```json
"com.unity.netcode.gameobjects": "1.12.0",
"com.unity.transport": "2.4.0",
"com.unity.multiplayer.tools": "2.2.1",
"com.playeveryware.eos": "file:com.playeveryware.eos"
```

### 1.3 Estrutura de Pastas — ✅

Todos os scripts base criados em `Assets/Codigo/Multiplayer/`:

```
Multiplayer/
├── Core/
│   ├── NetworkBootstrap.cs        ✅ Implementado (StartHost/StartClient reais)
│   ├── EOSManager.cs              ✅ Implementado (wrapper PlayEveryWare)
│   ├── EOSConfig.cs               ✅ Implementado (carrega credenciais do arquivo)
│   ├── HostManager.cs             ✅ Implementado (StartAsHost + StartAsClient)
│   └── WindowsPlatformSpecifics.cs ✅ Implementado
├── Auth/
│   ├── EOSAuthenticator.cs        ✅ Implementado (Device ID - funcional)
│   └── SessionManager.cs          ✅ Implementado
├── Lobby/
│   ├── LobbyManager.cs            🚧 Estrutura criada (EOS calls = TODO)
│   ├── LobbyUI.cs                 🚧 Estrutura criada
│   ├── LobbyData.cs               ✅ Implementado (structs)
│   └── LobbyItemUI.cs             🚧 Estrutura criada
├── GameServer/
│   ├── GameServerManager.cs       🚧 Estrutura criada
│   ├── MatchManager.cs            🚧 Estrutura criada
│   └── PlayerRegistry.cs          🚧 Estrutura criada
├── Sync/
│   ├── NetworkedPlayerController.cs 🚧 Estrutura criada
│   ├── NetworkedCurrency.cs        🚧 Estrutura criada
│   ├── NetworkedBuilding.cs        🚧 Estrutura criada
│   └── NetworkedHorde.cs           🚧 Estrutura criada
└── Testing/
    ├── EOSAuthTest.cs              ✅ Funcional (testado)
    └── NetworkConnectionTest.cs    ✅ Implementado e testado (Host/Client LAN)
```

### 1.4 Cenas do Projeto — ✅ Parcial

- [x] `EOSAuthTest.unity` — cena de teste de autenticacao EOS
- [x] `NetworkTest.unity` — cena de teste de conexao P2P (criada nesta sprint)
- [ ] `LobbyScene.unity` — aguardando Fase 3
- [ ] `NetworkBootstrap.unity` — aguardando integracao com menu

---

## FASE 2: Autenticacao — ✅ CONCLUIDA (Device ID)

### 2.1 EOSManagerWrapper — ✅

**Arquivo:** `Core/EOSManager.cs`

- [x] Wrapper para PlayEveryWare EOSManager
- [x] Expoe `ConnectInterface`, `AuthInterface`, `PlatformInterface`
- [x] Carrega credenciais via `EOSConfig`
- [x] Singleton com DontDestroyOnLoad

### 2.2 EOSAuthenticator — ✅

**Arquivo:** `Auth/EOSAuthenticator.cs`

**Metodo implementado:** Device ID (login anonimo)
- [x] `LoginWithDeviceId()` — cria Device ID unico por maquina
- [x] Fluxo: CreateDeviceId → Login → (se novo) CreateUser → sucesso
- [x] Armazena `ProductUserId` localmente
- [x] Dispara `OnLoginSuccess` / `OnLoginFailed`
- [x] Integrado com `SessionManager`

**Testado:** Login funcional confirmado em `EOSAuthTest.unity`

### 2.3 SessionManager — ✅

**Arquivo:** `Auth/SessionManager.cs`

- [x] Armazena userId, displayName, lobbyId, matchId
- [x] `StartSession()` / `EndSession()`
- [x] Singleton com DontDestroyOnLoad

### 2.4 UI de Login — 🚧 Pendente

- [ ] UI formal em `MenuScene.unity` (atualmente usando OnGUI de debug)
- Aguardando integracao com fluxo de menu principal

---

## SPRINT: Validacao de Rede Basica — ✅ CONCLUIDA

> Sprint da equipe: validar que NGO + UnityTransport funcionam entre instancias.

- [x] **Pacote de Netcode instalado** — NGO 1.12.0 confirmado
- [x] **NetworkManager configurado na cena** — NetworkTest.unity com NetworkManager + UnityTransport
- [x] **Sistema basico Host/Client** — `NetworkConnectionTest.cs` com Host e Client funcionais
- [x] **Teste de conexao entre instancias com Prefabs genericos** — 2 instancias conectadas via MPPM (Multiplayer Play Mode), capsulas spawnando corretamente

**Como testar:**
- `Window → Package Manager` → instalar `com.unity.multiplayer.playmode`
- Ou: Build + Editor (Host no .exe, Client no Editor)
- Cena: `NetworkTest.unity`
- Script: `Testing/NetworkConnectionTest.cs` (UI via OnGUI, sem Canvas necessario)
- Troca de cena sincronizada: campo `Game Scene Name` no Inspector

---

## FASE 3: Sistema de Lobby — 🚧 PROXIMA FASE

### 3.1 Estrutura de Dados — ✅ Definida

**Arquivo:** `Lobby/LobbyData.cs` — structs ja implementadas:
- `LobbyInfo` (id, nome, host, players, mapa, publico, estado)
- `LobbyMember` (userId, displayName, characterIndex, isReady)
- `LobbySettings`, `LobbySearchFilter`
- `LobbyState` enum, `MemberAttributes` constants

### 3.2 LobbyManager — 🚧 TODO (estrutura pronta)

**Arquivo:** `Lobby/LobbyManager.cs`

**Metodos com estrutura criada, chamadas EOS pendentes:**

| Metodo | Status | O que falta |
|--------|--------|-------------|
| `CreateLobby(settings)` | 🚧 Simulado | Chamar `LobbyInterface.CreateLobby()` real |
| `SearchLobbies(filter)` | 🚧 Simulado | Chamar `LobbyInterface.CreateLobbySearch()` |
| `JoinLobby(lobbyId)` | 🚧 TODO | Implementar `LobbyInterface.JoinLobby()` |
| `LeaveLobby()` | 🚧 Parcial | Chamar `LobbyInterface.LeaveLobby()` |
| `SetMemberAttribute(key, value)` | 🚧 TODO | Implementar `UpdateLobbyMember()` |
| `StartMatch()` | 🚧 TODO | Coordenar Host.StartAsHost() + enviar conexao para clients |

### 3.3 Fluxo de Matchmaking P2P — Planejado

```
1. Host cria lobby via Epic Lobby Service
2. Outros jogadores buscam e entram no lobby
3. Todos selecionam personagens (atributo do membro)
4. Todos marcam "Pronto"
5. Host clica "Iniciar Partida"
6. Host chama HostManager.StartAsHost()
   └─ transport.SetConnectionData("0.0.0.0", porta)
   └─ NetworkManager.StartHost()
7. LobbyManager guarda IP:Porta do Host como atributo do lobby
8. Clients leem IP:Porta do lobby e chamam HostManager.StartAsClient(ip)
   └─ transport.SetConnectionData(ip, porta)
   └─ NetworkManager.StartClient()
9. Host carrega cena via NetworkManager.SceneManager.LoadScene()
10. Clients sao transportados automaticamente (Network Scene Management)
```

> **Nota:** O fluxo de rede (passos 6-10) ja foi validado na Sprint de Rede Basica.
> O que falta e apenas a parte do Lobby EOS (passos 1-8).

### 3.4 UI de Lobby — 🚧 Pendente

**Nova Cena:** `LobbyScene.unity`

**Panel: CreateLobbyPanel**
- InputField: LobbyName
- Slider: MaxPlayers (2-4)
- Toggle: IsPublic
- Button: CreateLobby

**Panel: LobbyListPanel**
- ScrollView: LobbyList
  - Prefab: LobbyListItem (Nome, Host, 2/4, Button Join)
- Button: RefreshList
- Button: CreateNew

**Panel: LobbyRoomPanel**
- Text: LobbyName
- List: PlayerSlots (4 slots)
  - Cada slot: Avatar, Nome, Personagem, Ready Status
- Button: SelectCharacter
- Toggle: Ready
- Button: StartGame (apenas host, apenas quando todos prontos)
- Button: LeaveLobby

---

## FASE 4: Configuracao P2P — 🚧 Parcialmente Concluida

### 4.1 Host e Client — ✅ Implementado

**Arquivo:** `Core/HostManager.cs`

```csharp
// Ja implementado e testado:
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

**Objetivo:** Substituir UnityTransport padrao por EOS P2P Transport para NAT Traversal automatico.

**Configuracao no Portal:**
- Game Services > Peer-to-peer > Ativar

**No Codigo (a implementar na Fase 3/4):**
```csharp
// Configurar EOS P2P Transport ao inves de UDP simples
// Isso resolve conexoes entre jogadores em redes diferentes (NAT)
```

> **Nota:** Na Sprint atual usamos UnityTransport UDP direto (funciona em LAN).
> Para conexoes via internet (jogadores em redes diferentes), precisaremos
> configurar o EOS P2P Transport para NAT traversal automatico.

---

## FASE 5: Sincronizacao de Gameplay — 🚧 PENDENTE

### 5.1 Player Prefab Networked

**Modificar:** `Assets/Modelos/PreFab/Entidades/Player 1.prefab`

**Componentes a adicionar:**
1. `NetworkObject`
2. `NetworkTransform`
   - Sync Position X/Y/Z: true
   - Sync Rotation Y: true
   - Interpolate: true
3. `NetworkAnimator`
4. `NetworkedPlayerController` (script em Sync/)

### 5.2 NetworkedPlayerController — 🚧 Estrutura criada

**Arquivo:** `Sync/NetworkedPlayerController.cs`

```csharp
public class NetworkedPlayerController : NetworkBehaviour
{
    public NetworkVariable<float> NetworkHealth = new(writePerm: NetworkVariableWritePermission.Server);
    public NetworkVariable<int> NetworkAmmo = new(writePerm: NetworkVariableWritePermission.Server);
    public NetworkVariable<int> CharacterIndex = new(writePerm: NetworkVariableWritePermission.Owner);

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
            movement.enabled = false; // Outros jogadores nao controlam este

        NetworkHealth.OnValueChanged += OnHealthChanged;
    }
}
```

### 5.3 Modificacoes em PlayerMovement.cs

**Arquivo:** `Assets/Codigo/Char scripts/Player/PlayerMovement.cs`

```csharp
// Mudar heranca:
public class PlayerMovement : NetworkBehaviour

// No Update():
private void Update()
{
    if (!IsOwner) return; // CRITICO: apenas dono processa input
    // ... resto do codigo
}
```

### 5.4 Modificacoes em PlayerHealthSystem.cs

```csharp
public class PlayerHealthSystem : NetworkBehaviour
{
    public NetworkVariable<float> networkHealth = new(
        writePerm: NetworkVariableWritePermission.Server
    );

    [ServerRpc(RequireOwnership = false)]
    public void TakeDamageServerRpc(float damage, ulong attackerId) { ... }

    [ClientRpc]
    private void RespawnClientRpc() { ... }
}
```

### 5.5 Sincronizacao de Combate

```csharp
// PlayerShooting.cs:
if (IsOwner && Input.GetButton("Fire1"))
    ShootServerRpc(targetPosition, damage);

[ServerRpc]
private void ShootServerRpc(Vector3 target, float damage) { ... }
```

### 5.6 Sincronizacao de Moedas (CurrencyManager)

```csharp
public class CurrencyManager : NetworkBehaviour
{
    public NetworkVariable<int> TeamGeodites = new(writePerm: NetworkVariableWritePermission.Server);
    public NetworkVariable<int> TeamDarkEther = new(writePerm: NetworkVariableWritePermission.Server);

    [ServerRpc(RequireOwnership = false)]
    public void AddGeoditeServerRpc(int amount) { ... }

    [ServerRpc(RequireOwnership = false)]
    public void SpendGeoditeServerRpc(int amount, ServerRpcParams rpcParams = default) { ... }
}
```

### 5.7 Sincronizacao de Torres (BuildManager)

```csharp
[ServerRpc(RequireOwnership = false)]
public void PlaceTowerServerRpc(int towerIndex, Vector3 position, Quaternion rotation)
{
    GameObject tower = Instantiate(towerPrefabs[towerIndex], position, rotation);
    tower.GetComponent<NetworkObject>().Spawn();
}
```

### 5.8 Sincronizacao de Waves (HordeManager)

```csharp
public class HordeManager : NetworkBehaviour
{
    public NetworkVariable<int> CurrentWave = new();
    public NetworkVariable<int> EnemiesRemaining = new();

    public override void OnNetworkSpawn()
    {
        if (IsServer) StartCoroutine(SpawnWaves()); // Apenas servidor spawna
    }
}
```

### 5.9 Sincronizacao de Habilidades

```csharp
public void UseAbility1()
{
    if (!IsOwner) return;
    if (IsOnCooldown(ability1)) return;
    UseAbilityServerRpc(1);
    StartCooldown(ability1);
}

[ServerRpc]
private void UseAbilityServerRpc(int abilityIndex) { ... }

[ClientRpc]
private void PlayAbilityEffectClientRpc(int abilityIndex) { ... }
```

---

## FASE 6: Polimento e Testes — 🚧 PENDENTE

### 6.1 Ferramentas de Teste

**Multiplayer Play Mode (MPPM)** — recomendado para desenvolvimento:
```
Window → Package Manager → Add by name: com.unity.multiplayer.playmode
Window → Multiplayer → Multiplayer Play Mode
```
> Ja utilizado com sucesso na Sprint de Rede Basica.

### 6.2 Tratamento de Desconexao

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

### 6.3 Configuracao de NetworkTransform

- Position Threshold: 0.001
- Rotation Threshold: 0.01
- Interpolate: true
- Use Quaternion Sync: true
- Use Unreliable Deltas: true (melhor para movimento)

### 6.4 Lag Compensation (Futuro)

```csharp
[ServerRpc]
void ShootServerRpc(Vector3 direction, float clientTimestamp)
{
    float latency = NetworkManager.Singleton.LocalTime.Time - clientTimestamp;
    // Rollback de posicoes para o momento do tiro
}
```

---

## Checklist Geral de Verificacao

### Fase 1 - Setup
- [x] Conta Epic Developer criada
- [x] Produto configurado no portal (Lobbies + P2P ativos)
- [x] NGO 1.12.0 instalado
- [x] EOS Plugin (PlayEveryWare) instalado
- [x] Projeto compila sem erros
- [x] Estrutura de pastas criada

### Fase 2 - Auth
- [x] EOS SDK inicializa corretamente
- [x] Login com Device ID funcional
- [x] ProductUserId exibido no console
- [x] SessionManager armazena dados da sessao
- [ ] UI de login integrada ao MenuScene (OnGUI temporario em uso)

### Sprint - Rede Basica
- [x] NetworkManager configurado na cena
- [x] UnityTransport configurado no NetworkManager
- [x] Player Prefab (capsula) com NetworkObject + NetworkTransform
- [x] Host inicia com StartHost() funcional
- [x] Client conecta com StartClient() funcional
- [x] Capsula spawna para cada jogador conectado
- [x] Troca de cena sincronizada via NGO SceneManager
- [x] Testado com 2 instancias (MPPM ou Build+Editor)

### Fase 3 - Lobby
- [ ] Criar lobby via EOS funciona
- [ ] Buscar lobbies funciona
- [ ] Entrar em lobby funciona
- [ ] Sair de lobby funciona
- [ ] Lista de membros atualiza em tempo real
- [ ] Status "pronto" funciona
- [ ] Host inicia partida quando todos prontos
- [ ] Clients recebem IP do Host via lobby
- [ ] UI de lobby completa (LobbyScene.unity)

### Fase 4 - P2P
- [x] HostManager.StartAsHost() funcional (LAN)
- [x] HostManager.StartAsClient() funcional (LAN)
- [ ] EOS P2P Transport configurado (para conexoes via internet / NAT)
- [ ] Teste entre maquinas em redes diferentes (internet)

### Fase 5 - Gameplay
- [ ] Spawn de jogadores funciona
- [ ] Movimento sincronizado
- [ ] Combate ranged sincronizado
- [ ] Combate melee sincronizado
- [ ] Vida sincronizada
- [ ] Moedas sincronizadas
- [ ] Torres sincronizadas
- [ ] Traps sincronizadas
- [ ] Inimigos sincronizados
- [ ] Waves sincronizadas
- [ ] Habilidades sincronizadas
- [ ] Ultimate sincronizado

### Fase 6 - Polimento
- [ ] Desconexao tratada (volta ao menu)
- [ ] Movimento suave com interpolacao
- [ ] Sem erros no console em sessao completa
- [ ] Funciona entre maquinas em redes diferentes
- [ ] Performance aceitavel (4 jogadores, 1 wave)

---

## Cronograma Atualizado

### ✅ Concluido
- **Semana 1-2:** Fundacao (Epic Portal, Pacotes, Estrutura)
- **Semana 3:** Autenticacao (Device ID funcional)
- **Sprint extra:** Rede basica validada (NGO + Transport + Host/Client)

### 🚧 Em andamento / Proximo
- **Semana 4:** Lobby System (implementar chamadas EOS reais no LobbyManager)
- **Semana 5:** P2P Transport + integracao Lobby → Rede
- **Semana 6:** Gameplay basico (Player Networked + movimento + vida)
- **Semana 7:** Gameplay completo (combate, moedas, torres, inimigos)
- **Semana 8:** Polimento, testes entre redes, preparacao para apresentacao

---

## Proximos Passos Imediatos

1. **Implementar LobbyManager com chamadas EOS reais**
   - `LobbyInterface.CreateLobby()` em `CreateLobby()`
   - `LobbyInterface.CreateLobbySearch()` em `SearchLobbies()`
   - `LobbyInterface.JoinLobby()` em `JoinLobby()`
   - Referencia: https://dev.epicgames.com/docs/game-services/lobbies

2. **Criar LobbyScene.unity** com UI de lista e sala

3. **Configurar EOS P2P Transport** para conexoes via internet
   - Necessario para testar entre redes diferentes (fora da LAN)
   - Referencia: https://dev.epicgames.com/docs/game-services/p-2-p

4. **Integrar fluxo completo:** Login → Lobby → Host/Client → Cena de jogo

5. **Criar UI de Login formal** em MenuScene.unity (substituir OnGUI de debug)

---

## Arquivos a Serem Modificados (Fase 5)

```
Assets/Codigo/Char scripts/Player/
  PlayerMovement.cs          # NetworkBehaviour, IsOwner check
  PlayerHealthSystem.cs      # NetworkVariable, TakeDamageServerRpc
  PlayerShooting.cs          # ShootServerRpc
  MeleeCombatSystem.cs       # MeleeAttackServerRpc

Assets/Codigo/Char scripts/JP/
  CommanderAbilityController.cs  # UseAbilityServerRpc

Assets/Codigo/Managers/
  CurrencyManager.cs         # NetworkVariable (Geodites, DarkEther)
  HordeManager.cs            # Servidor controla spawn de inimigos
  PauseControl.cs            # Pausar nao funciona em rede (adaptar)

Assets/Codigo/Tower scripts/
  BuildManager.cs            # PlaceTowerServerRpc, PlaceTrapServerRpc

Assets/Modelos/PreFab/Entidades/
  Player 1.prefab            # Adicionar NetworkObject, NetworkTransform
  [prefabs de inimigos]      # Adicionar NetworkObject
  [prefabs de torres]        # Adicionar NetworkObject
```

---

## Riscos e Mitigacoes

| Risco | Probabilidade | Impacto | Mitigacao |
|-------|---------------|---------|-----------|
| NAT Traversal falha em algumas redes | Media | Alto | EOS P2P Relay (fallback automatico) |
| Bugs de sincronizacao | Alta | Medio | Testar incrementalmente com MPPM |
| Latencia alta | Media | Medio | Interpolacao no NetworkTransform |
| EOS Lobby SDK complexo | Media | Alto | Testar cada operacao isoladamente |
| Host desconecta durante partida | Baixa | Alto | Tratar OnServerStopped (voltar ao menu) |
| Prazo apertado | Media | Alto | Priorizar: Lobby → P2P basico → Gameplay |
