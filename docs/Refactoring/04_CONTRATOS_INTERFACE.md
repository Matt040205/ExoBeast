# 04 — Contratos de Interface (o que NÃO pode mudar)

> Inventário das assinaturas públicas, eventos, RPCs e referências de Inspector que **devem ser preservados integralmente** durante a refatoração.
>
> Qualquer mudança em um item listado aqui exige aprovação explícita do orquestrador e atualização desta lista no mesmo PR.

---

## Como usar este documento

Antes de tocar em qualquer arquivo da §2 do `00_LEIA_PRIMEIRO.md`:

1. Busque o arquivo nas tabelas abaixo (Ctrl+F pelo nome).
2. Confirme se sua sprint listou esse arquivo como permitido.
3. Confirme se sua mudança preserva **cada item** da tabela.
4. Se alguma assinatura precisa mudar:
   - **Pare.**
   - Registre a necessidade no log da sprint.
   - Aguarde aprovação do orquestrador.
   - Atualize esta tabela no mesmo PR que faz a mudança.

---

## 1. `LobbyManager` (Assets/Codigo/Multiplayer/Lobby/LobbyManager.cs)

### 1.1 Singleton API

| Membro | Assinatura | Dependentes conhecidos |
|---|---|---|
| `Instance` | `public static LobbyManager Instance { get; }` (auto-create) | `LobbySceneUI:93,812`, `LobbyUIManager:103`, `LobbyPlaceholderUI:83`, `MenuLobbyPanel:61`, `EOSAuthTest`, qualquer UI |
| `HasInstance` | `public static bool HasInstance { get; }` | uso defensivo em UI |
| `TryGetExistingInstance` | `public static LobbyManager TryGetExistingInstance()` | uso defensivo em UI |

### 1.2 Eventos públicos (event Action<…>)

| Evento | Assinatura | Quem assina |
|---|---|---|
| `OnLobbyCreated` | `event Action<LobbyInfo>` | 4 UIs de lobby |
| `OnLobbiesFound` | `event Action<List<LobbyInfo>>` | 3 UIs (LobbySceneUI/LobbyUIManager/LobbyPlaceholderUI) |
| `OnLobbyJoined` | `event Action<LobbyInfo>` | 4 UIs |
| `OnLobbyLeft` | `event Action` | 4 UIs |
| `OnMemberJoined` | `event Action<LobbyMember>` | 4 UIs |
| `OnMemberLeft` | `event Action<LobbyMember>` | 4 UIs |
| `OnMemberUpdated` | `event Action<LobbyMember>` | 4 UIs |
| `OnError` | `event Action<string>` | 4 UIs |

**Regra:** nome do evento, assinatura do delegate, e semântica de quando dispara — todos preservados.

### 1.3 Métodos públicos chamados de fora

| Método | Assinatura | Chamadores |
|---|---|---|
| `CreateLobby` | `bool CreateLobby(LobbySettings settings)` | 4 UIs |
| `SearchLobbies` | `void SearchLobbies(LobbySearchFilter filter)` | 4 UIs |
| `JoinLobby` | `void JoinLobby(string lobbyId)` | 4 UIs |
| `LeaveLobby` | `void LeaveLobby()` | 4 UIs |
| `ForceLeaveImmediate` | `void ForceLeaveImmediate()` | `MultiplayerRuntimeReset.cs` |
| `CancelPendingClientConnect` | `void CancelPendingClientConnect()` | `MultiplayerRuntimeReset.cs` |
| `ForceResetRuntimeState` | `void ForceResetRuntimeState(bool notifyLobbyLeft = false)` | `MultiplayerRuntimeReset.cs` |
| `SetMemberAttribute` | `void SetMemberAttribute(string key, string value)` | `LobbyPlaceholderUI:590` (alteração de nick) |
| `SetReady` | `void SetReady(bool ready)` | 4 UIs |
| `SelectCharacter` | `void SelectCharacter(int characterIndex)` | 4 UIs + `SelecaoManager` |
| `StartMatch` | `void StartMatch(string mapOverride = null)` | `LobbySceneUI`, `LobbyUIManager`, `LobbyPlaceholderUI`, `MenuLobbyPanel`, `LobbyManager.OnNgoConnectionApproval` |
| `InvalidateLobbySearchCache` | `void InvalidateLobbySearchCache()` | uso interno + utilitário |
| `GetCurrentLobby` | `LobbyInfo GetCurrentLobby()` | 4 UIs |
| `GetMembers` | `List<LobbyMember> GetMembers()` | 4 UIs + `NetworkGameplayResolver` |
| `GetOrderedMembers` | `List<LobbyMember> GetOrderedMembers()` | `NetworkGameplayResolver:191`, `LobbyManager` interno |
| `GetCanonicalMemberIndex` | `int GetCanonicalMemberIndex(string userId)` | `NetworkGameplayResolver:190` |
| `IsInLobby` | `bool IsInLobby()` | `MenuLobbyPanel:107` |
| `AllMembersReady` (se for público) | confirmar visibilidade antes de mexer | `LobbyUIManager.AllMembersReady` chama? — verificar |

**Regra:** ao extrair `MatchSessionLauncher` (Sprint 3), o `LobbyManager.StartMatch` continua existindo como facade que delega para o novo serviço — **assinatura idêntica**.

### 1.4 Constants públicas

| Constant | Tipo |
|---|---|
| `LobbyAttributes.LOBBY_NAME`, `MAP_NAME`, `MAX_PLAYERS`, `LOBBY_STATE`, `SERVER_ADDRESS`, `SERVER_PORT`, `RELAY_CODE` | static readonly string (em `LobbyData.cs`) |
| `MemberAttributes.DISPLAY_NAME`, `IS_READY`, `CHARACTER_INDEX` | static readonly string (em `LobbyData.cs`) |

**Regra:** valores das strings são chaves no EOS Lobby Service — se mudar a string, **lobbies criados pela versão antiga param de comunicar** com a versão nova. Imutáveis.

### 1.5 DTOs

| Tipo | Propriedades chaves |
|---|---|
| `LobbyInfo` | `lobbyId`, `lobbyName`, `hostDisplayName`, `hostProductUserId`, `currentPlayers`, `maxPlayers`, `mapName`, `isPublic`, `state` |
| `LobbyMember` | `productUserId`, `displayName`, `isHost`, `isReady`, `selectedCharacterIndex` |
| `LobbySettings` | `lobbyName`, `maxPlayers`, `isPublic`, `mapName` |
| `LobbySearchFilter` | `lobbyName`, `onlyPublic`, `maxResults` |
| `LobbyState` (enum) | `WaitingForPlayers`, `Starting`, `InGame`, `Finished`, etc. |

**Regra:** mudar nome de field ou tipo quebra serialização EOS e UI. Imutáveis.

---

## 2. `EOSAuthenticator` (Assets/Codigo/Multiplayer/Auth/EOSAuthenticator.cs)

### 2.1 Singleton API

| Membro | Assinatura |
|---|---|
| `Instance` | `public static EOSAuthenticator Instance { get; }` (auto-create) |

### 2.2 Properties

| Property | Assinatura |
|---|---|
| `IsLoggedIn` | `bool` |
| `CurrentProductUserId` | `string` |

### 2.3 Eventos

| Evento | Assinatura | Quem assina |
|---|---|---|
| `OnLoginSuccess` | `event Action<string>` (userId) | 4 UIs + `LobbyUIManager.InitMultiplayerFlow` |
| `OnLoginFailed` | `event Action<string>` (error) | 4 UIs |
| `OnLogout` | `event Action` | atualmente sem assinantes — preservar mesmo assim |

### 2.4 Métodos públicos

| Método | Assinatura | Chamadores |
|---|---|---|
| `LoginWithDeviceId` | `void LoginWithDeviceId()` | 4 UIs |
| `Logout` | `void Logout()` | sem chamadores diretos hoje; preservar |
| `SetDeviceIdName` | `void SetDeviceIdName(string name)` | 4 UIs |
| `GetProductUserId` (EOS-enabled only) | `ProductUserId GetProductUserId()` | uso interno + futuro |

---

## 3. `SessionManager` (Assets/Codigo/Multiplayer/Auth/SessionManager.cs)

### 3.1 Singleton API

| Membro | Assinatura |
|---|---|
| `Instance` | `public static SessionManager Instance { get; }` (auto-create) |
| `HasInstance` | `public static bool HasInstance { get; }` |
| `TryGetExistingInstance` | `public static SessionManager TryGetExistingInstance()` |

### 3.2 Campos críticos

| Campo | Tipo | Por quê é contrato |
|---|---|---|
| `sessionToken` | `public readonly string` (GUID inicializado em field initializer) | Distingue clones MPPM com mesmo PUID. **Imutável.** Documentado em `MEMORY.md` § Identidade 3 Março 2026. |

**Regra:** mudar inicialização para lazy (no Awake) regride race condition resolvida. **Mantenha o field initializer.**

### 3.3 Métodos públicos

| Método | Assinatura | Chamadores |
|---|---|---|
| `StartSession` | `void StartSession(string userId, string displayName)` | `EOSAuthenticator.OnConnectLoginComplete`, `OnCreateUserComplete` |
| `EndSession` | `void EndSession()` | `EOSAuthenticator.Logout` |
| `SetCurrentLobby` | `void SetCurrentLobby(string lobbyId)` | `LobbyManager` |
| `SetCurrentMatch` | `void SetCurrentMatch(string matchId)` | preservar |
| `GetUserId` | `string GetUserId()` | múltiplos chamadores (UI + Bridge + LobbyManager) |
| `GetDisplayName` | `string GetDisplayName()` | múltiplos |
| `SetDisplayName` | `void SetDisplayName(string newName)` | UI |
| `IsInSession` | `bool IsInSession()` | uso externo |
| `IsInLobby` / `IsInMatch` | `bool` | uso externo |
| `GetCurrentLobbyId` / `GetCurrentMatchId` | `string` | uso externo |

---

## 4. `EOSManagerWrapper` (Assets/Codigo/Multiplayer/Core/EOSManagerWrapper.cs)

### 4.1 Singleton + Properties

| Membro | Assinatura |
|---|---|
| `Instance` | `public static EOSManagerWrapper Instance { get; }` (auto-create) |
| `IsInitialized` | `bool` |
| `IsConnected` | `bool` |

### 4.2 Eventos

| Evento | Assinatura | Quem assina |
|---|---|---|
| `OnEOSInitialized` | `event Action` | `LobbyManager.Start`, `LobbyPlaceholderUI.Start` |
| `OnEOSShutdown` | `event Action` | reservado |
| `OnInitializationFailed` | `event Action<string>` | `LobbyPlaceholderUI.OnEOSFailed` |

### 4.3 Métodos públicos

| Método | Assinatura |
|---|---|
| `Initialize` | `void Initialize()` |
| `Shutdown` | `void Shutdown()` |
| `SetConnected` | `void SetConnected(bool connected)` |
| `GetPlatformInterface` (EOS) | `PlatformInterface GetPlatformInterface()` |
| `GetConnectInterface` (EOS) | `ConnectInterface GetConnectInterface()` |
| `GetAuthInterface` (EOS) | `AuthInterface GetAuthInterface()` |

---

## 5. `PlayerIdentityBridge` (Assets/Codigo/Multiplayer/Core/PlayerIdentityBridge.cs)

### 5.1 Static API

| Membro | Assinatura |
|---|---|
| `Instance` | `public static PlayerIdentityBridge Instance { get; private set; }` (NetworkBehaviour, **não auto-create**) |

### 5.2 ServerRpc — IMUTÁVEL

| Método | Assinatura |
|---|---|
| `RegisterPlayerServerRpc` | `[ServerRpc(RequireOwnership = false)] void RegisterPlayerServerRpc(string productUserId, string sessionToken, ServerRpcParams rpcParams = default)` |

**Regra:** este ServerRpc é chamado por todos os clientes via `PlayerNetworkSetup.RegisterIdentityWithBridgeWhenReady`. Mudar a assinatura quebra serialização NGO entre versões. **Nunca mudar.**

### 5.3 DTOs

| Tipo | Campos |
|---|---|
| `PlayerIdentity` (struct) | `productUserId`, `sessionToken` |

### 5.4 Métodos públicos

| Método | Assinatura |
|---|---|
| `GetIdentity` | `PlayerIdentity? GetIdentity(ulong clientId)` |
| `GetClientIdByUserId` | `ulong? GetClientIdByUserId(string productUserId)` |

---

## 6. `PlayerRegistry` (Assets/Codigo/Multiplayer/GameServer/PlayerRegistry.cs)

### 6.1 API

| Membro | Assinatura |
|---|---|
| `Instance` | `public static PlayerRegistry Instance { get; private set; }` (NetworkBehaviour) |
| `RegisterPlayer` | `void RegisterPlayer(ulong clientId, GameObject playerObj, int characterIndex = 0)` |
| `SetPlayerCharacterChoice` | `void SetPlayerCharacterChoice(ulong clientId, int index)` |
| `GetPlayerCharacterChoice` | `int GetPlayerCharacterChoice(ulong clientId)` |
| `LinkProductUserId` | `void LinkProductUserId(ulong clientId, string productUserId)` |
| `GetProductUserId` | `string GetProductUserId(ulong clientId)` |
| `GetClientIdByUserId` | `ulong? GetClientIdByUserId(string productUserId)` |
| `UnregisterPlayer` | `void UnregisterPlayer(ulong clientId)` |
| `GetPlayerObject` | `GameObject GetPlayerObject(ulong clientId)` |
| `GetAllPlayers` | `Dictionary<ulong, GameObject> GetAllPlayers()` |
| `GetPlayerCount` | `int GetPlayerCount()` |
| `GetClosestPlayer` | `Transform GetClosestPlayer(Vector3 position)` |
| `CollectValidPlayerTransforms` | `static int CollectValidPlayerTransforms(List<Transform> results)` |
| `IsValidPlayerObject` | `static bool IsValidPlayerObject(GameObject playerObject)` |

**Dependentes:** `EnemyController`, `HordeManager`, `GameSetupManager`, `NetworkGameplayResolver` (todos fora da pasta Multiplayer mas dependem desta API).

---

## 7. `NetworkBootstrap` (Assets/Codigo/Multiplayer/Core/NetworkBootstrap.cs)

### 7.1 Métodos públicos

| Método | Assinatura | Chamadores |
|---|---|---|
| `StartHost` | `void StartHost()` | inputs auto-start no Awake/Start |
| `StartClient` | `void StartClient(string hostIp = null)` | inputs auto-start |
| `Shutdown` | `void Shutdown()` | uso externo |
| `StartServer` | `void StartServer()` | **DEPRECATED**, mantido como warning. Preservar comportamento (log warning). |

### 7.2 SerializeField (Inspector)

| Campo | Tipo |
|---|---|
| `autoStartHost` | `bool` |
| `autoStartClient` | `bool` |
| `useP2PMode` | `bool` |
| `clientConnectIp` | `string` |
| `networkPort` | `ushort` |

**Regra:** estes campos têm valores configurados em cenas (`SceneMapTest.unity`, talvez `MenuScene.unity`). Renomear quebra a referência.

---

## 8. `HostManager` (Assets/Codigo/Multiplayer/Core/HostManager.cs)

⚠️ **Sprint 2 vai DELETAR este arquivo.** Antes de deletar:

1. Buscar todos os chamadores de `HostManager.Instance.*` no projeto.
2. Migrar cada um para `NetworkBootstrap.Instance.*` (ou equivalente).
3. Confirmar que nenhuma cena tem `[SerializeField] HostManager` ou referência por Inspector.

Métodos que vão sumir (deve haver substituto antes de deletar):

| Método antigo | Substituto NetworkBootstrap |
|---|---|
| `HostManager.Instance.StartAsHost()` | `NetworkBootstrap.Instance.StartHost()` |
| `HostManager.Instance.StartAsClient(ip, port)` | `NetworkBootstrap.Instance.StartClient(ip)` (porta vem do field) |
| `HostManager.Instance.StopHost()` | `NetworkBootstrap.Instance.Shutdown()` |
| `HostManager.Instance.IsHost()` | `NetworkManager.Singleton?.IsHost == true` |
| `HostManager.Instance.GetMaxPlayers()` | mover constante para `NetworkBootstrap` ou config |
| `HostManager.Instance.GetHostPort()` | `NetworkBootstrap.Instance.networkPort` (expor via property) |
| `HostManager.Instance.GetConnectedPlayersCount()` | `NetworkManager.Singleton?.ConnectedClients.Count ?? 0` |

---

## 9. `GameServerManager` (Assets/Codigo/Multiplayer/GameServer/GameServerManager.cs)

⚠️ **Sprint 2 vai AVALIAR remoção.**

| Membro | Assinatura | Status |
|---|---|---|
| `Instance` | `static` | usado por... — verificar |
| `ValidatePlayerAction` | `bool ValidatePlayerAction(ulong clientId, string action)` | sem chamadores claros — confirmar antes de remover |
| `GetPlayerData` | `PlayerData GetPlayerData(ulong clientId)` | sem chamadores claros |
| `GetAllPlayers` | `Dictionary<ulong, PlayerData> GetAllPlayers()` | sem chamadores claros |
| `IsServerReady` | `bool IsServerReady()` | sem chamadores claros |

**Decisão de remoção:** se varredura confirmar 0 chamadores em código de produção (excluindo Testing), remover na Sprint 2. Caso contrário, marcar como `[Obsolete]` e migrar gradualmente.

---

## 10. NetworkBehaviours de gameplay (Sync/)

⚠️ **NÃO PODEM mudar nesta refatoração.** Listados aqui só para referência.

### 10.1 `NetworkedPlayerController`

| Membro | Assinatura |
|---|---|
| `CharacterIndex` | `NetworkVariable<int>` |
| `NetworkHealth` | `NetworkVariable<float>` |
| `NetworkAmmo` | `NetworkVariable<int>` (Owner read) |
| `TakeDamageServerRpc` | `[ServerRpc(RequireOwnership = false)] void TakeDamageServerRpc(float damage, ulong attackerId)` |
| `UpdateAmmoServerRpc` | `[ServerRpc] void UpdateAmmoServerRpc(int newAmmo)` |
| `OnPlayerDiedClientRpc` | `[ClientRpc] void OnPlayerDiedClientRpc()` |
| `OnPlayerRespawnedClientRpc` | `[ClientRpc] void OnPlayerRespawnedClientRpc()` |

### 10.2 `NetworkedEnemy`

| Membro | Assinatura |
|---|---|
| `NetworkHealth`, `IsDead`, `NetworkShield`, `IsShielded` | `NetworkVariable<*>` |
| `TakeDamageServerRpc` | `[ServerRpc(RequireOwnership = false)] void TakeDamageServerRpc(float damage, float armorPen, bool isCrit, ServerRpcParams rpcParams = default)` |
| `ApplyDamageServer` (overloads) | `bool ApplyDamageServer(...)` |
| `TriggerHitVisual` | `void TriggerHitVisual(float finalDamage, DamageContext damageContext)` |
| `DieRoutine` | `IEnumerator DieRoutine()` |
| `PlayAttackVfxClientRpc` | `[ClientRpc] void PlayAttackVfxClientRpc(Vector3 position, Quaternion rotation)` |
| `ApplyMarkVisualClientRpc` | `[ClientRpc] void ApplyMarkVisualClientRpc(bool marked)` |
| `SetAggroVisualClientRpc` | `[ClientRpc] void SetAggroVisualClientRpc(bool isActive)` |
| `TriggerImmunePopup` | `void TriggerImmunePopup(DamageContext damageContext)` |
| `OnShieldBrokenClientRpc` | `[ClientRpc] void OnShieldBrokenClientRpc()` |

### 10.3 `NetworkedBuilding`

| Membro | Assinatura |
|---|---|
| `BuilderClientId`, `CharacterIndex`, `TotalCostSpent`, `DpsLevel`, `ControlLevel`, `SupportLevel`, `IsActive` | `NetworkVariable<*>` |
| `UpgradeStateApplied` | `event System.Action` |
| `RefreshVisualState` | `void RefreshVisualState()` |
| `InitializeTowerServer` | `void InitializeTowerServer(ulong builderClientId, int characterIndex, int initialCostSpent)` |
| `CanInteractLocally` | `bool CanInteractLocally()` |
| `RequestUpgradeServerRpc` | `[ServerRpc(RequireOwnership = false)] void RequestUpgradeServerRpc(int pathIndex, ServerRpcParams rpcParams = default)` |
| `RequestSellServerRpc` | `[ServerRpc(RequireOwnership = false)] void RequestSellServerRpc(float refundPercentage, ServerRpcParams rpcParams = default)` |
| `BroadcastShieldVisualStateClientRpc` | `[ClientRpc] void BroadcastShieldVisualStateClientRpc(ulong targetNetId, bool isActive)` |

### 10.4 `NetworkedTrapVisual`

| Membro | Assinatura |
|---|---|
| `BuilderClientId`, `TrapIndex`, `LogicObjectId`, `IsActivated` | `NetworkVariable<*>` |
| `TrapData` | `TrapDataSO` (property) |
| `SellRefundPercentage` | `float` (property) |
| `InitializeServer` | `void InitializeServer(ulong builderClientId, int trapIndex, ulong logicObjectId)` |
| `EnsureRegisteredServer` | `void EnsureRegisteredServer()` |
| `SetActivationStateServer` | `void SetActivationStateServer(bool isActivated)` |
| `MarkBeingRemovedServer` | `void MarkBeingRemovedServer()` |
| `CanInteractLocally` | `bool CanInteractLocally()` |
| `SellTrap` | `void SellTrap()` |

### 10.5 `ClientNetworkTransform`

Override de `NetworkTransform`:

```csharp
protected override bool OnIsServerAuthoritative() => false;
```

**Imutável.** Todos os prefabs de player herdam.

### 10.6 `ServerAuthoritativeProjectile`

| Membro | Assinatura |
|---|---|
| `Initialize` | `void Initialize(PlayerShooting shooting, ulong attackerId, float projectileDamage, bool projectileCrit, float projectileArmorPenetration, Vector3 direction, float speed, float maxLifetime, bool empoweredSkill, float empoweredExplosionRadius)` |

### 10.7 `PlayerNetworkSetup`

| Membro | Assinatura |
|---|---|
| `EnableMovement` | `void EnableMovement()` |
| `DisableMovement` | `void DisableMovement()` |

### 10.8 `NetworkGameplayResolver` (static helpers)

| Método | Assinatura |
|---|---|
| `TryResolveCharacterData` | `static bool TryResolveCharacterData(Component context, out CharacterBase characterData, int preferredIndex = -1, bool allowOwnerLocalFallback = true)` |
| `ResolveCharacterData` | `static CharacterBase ResolveCharacterData(Component context, int preferredIndex = -1, bool allowOwnerLocalFallback = true)` |
| `TryResolveCharacterIndex` | `static bool TryResolveCharacterIndex(Component context, out int characterIndex, int preferredIndex = -1, bool allowOwnerLocalFallback = true)` |
| `ResolveCharacterDataByIndex` | `static CharacterBase ResolveCharacterDataByIndex(int characterIndex)` |
| `TryResolveAttackerFromPlayer` | `static bool TryResolveAttackerFromPlayer(GameObject owner, out ulong attackerClientId, out PlayerHealthSystem attackerHealth)` |
| `TryResolveAttackerFromBuilding` | `static bool TryResolveAttackerFromBuilding(Component context, out ulong attackerClientId, out PlayerHealthSystem attackerHealth)` |
| `ResolvePlayerHealth` | `static PlayerHealthSystem ResolvePlayerHealth(ulong clientId)` |

---

## 11. `CharacterChoiceCache` (Assets/Codigo/Multiplayer/Core/CharacterChoiceCache.cs)

| Membro | Assinatura |
|---|---|
| `ByClientId` | `static Dictionary<ulong, int>` (read mostly) |
| `SetClientCharacterIndex` | `static void SetClientCharacterIndex(ulong clientId, int charIndex, string source)` |
| `SetHostCharacterIndex` | `static void SetHostCharacterIndex(int charIndex, string source)` |
| `TryGet` | `static bool TryGet(ulong clientId, out int charIndex)` |
| `Clear` | `static void Clear()` |

**Chamadores:**
- `LobbyManager.OnNgoConnectionApproval` — set por client
- `LobbyManager.StartMatchCoroutine` — set por host
- `LobbyManager.ClearLobbyState` — clear
- `NetworkGameplayResolver` — read

---

## 12. `MatchManager` (Assets/Codigo/Multiplayer/GameServer/MatchManager.cs)

| Membro | Assinatura |
|---|---|
| `Instance` | `static MatchManager` |
| `CurrentMatchState` | `NetworkVariable<MatchState>` |
| `CurrentWave` | `NetworkVariable<int>` |
| `MatchTime` | `NetworkVariable<float>` |
| `StartMatchServerRpc` | `[ServerRpc(RequireOwnership = false)] void StartMatchServerRpc()` |
| `PauseMatch` | `void PauseMatch()` |
| `ResumeMatch` | `void ResumeMatch()` |
| `EndMatchVictory` | `void EndMatchVictory()` |
| `EndMatchDefeat` | `void EndMatchDefeat()` |
| `MatchState` (enum) | `WaitingForPlayers, Starting, Playing, Paused, Victory, Defeat, Ended` |

**Dependentes:** `UIManager.ForceTimerSync(MatchTime.Value)` (chamado em `OnNetworkSpawn`).

---

## 13. `MppmHelper` (Assets/Codigo/Multiplayer/Core/MppmHelper.cs)

| Membro | Assinatura |
|---|---|
| `IsClone` | `static bool` |
| `CloneId` | `static string` |

**Imutável.** Substituir pela env var oficial **já foi tentado e reverteu**. Veja `MEMORY.md`.

---

## 14. `MultiplayerRuntimeReset` (Assets/Codigo/Multiplayer/Core/MultiplayerRuntimeReset.cs)

| Membro | Assinatura |
|---|---|
| `ResetToOfflineLocal` | `static IEnumerator ResetToOfflineLocal()` |
| `ApplyOfflineLocalState` | `static void ApplyOfflineLocalState()` |

**Chamadores:** `LobbySceneUI.ReturnToMenuPrincipalRoutine`, `NetworkBootstrap.ReturnClientToMenuAfterHostDisconnect`.

---

## 15. `UGSBootstrap` (Assets/Codigo/Multiplayer/Core/UGSBootstrap.cs)

| Membro | Assinatura |
|---|---|
| `Instance` | `static` |
| `IsReady` | `bool` |

**Chamadores:** `LobbyManager.StartMatchCoroutine` (await até `IsReady`).

---

## 16. `EOSConfig` (Assets/Codigo/Multiplayer/Core/EOSConfig.cs)

ScriptableObject — interface preserva:

| Property | Assinatura |
|---|---|
| `ClientId`, `ClientSecret`, `ProductId`, `SandboxId`, `DeploymentId` | `string` getters (com `[NonSerialized]` campo backing) |
| `LoadCredentials` | `void LoadCredentials()` (lê env vars) |
| `ValidateCredentials` | `bool ValidateCredentials()` |
| `ClearCredentials` | `void ClearCredentials()` |

**Regra:** **NÃO remover `[NonSerialized]`.** O field initializer foi resultado do refactor 13 Maio 2026 para evitar credenciais comitadas.

---

## 17. `PartySlotLayout` (Assets/Codigo/Multiplayer/Core/PartySlotLayout.cs)

| Membro | Assinatura |
|---|---|
| `GetCommanderSlot` | `static int GetCommanderSlot(int totalPlayers, int playerIndex)` |
| `SlotsPerPlayer` (se existir) | static helper |

**Chamadores:** `NetworkGameplayResolver:194`, `LobbyManager:751`.

---

## 18. Referências de Inspector (cenas e prefabs)

Mudanças que afetam o Editor (cenas/prefabs) — **especialmente perigosas porque a referência é por GUID + fileID, não por nome de classe**.

### 18.1 LobbyScene.unity

| GameObject (na cena) | Componente | Field bindings |
|---|---|---|
| `LobbySceneUI` | `LobbySceneUI.cs` | `painelLobby`, `painelCriarLobby`, `painelJogadores`, `btnCriarHost`, ..., `playerSlotsContent`, etc. (~25 fields) |
| `Canvas/Painéis/...` | Buttons | `onClick` events bind to LobbySceneUI methods por nome (`Login`, `CriarSala`, etc.) **— LobbySceneUI usa `WireBtn` que sobrescreve onClick em runtime, então binding de Inspector é cosmético.** Mas **se mudar nome do método público, esse binding orfã.** |

### 18.2 EscolherPersonagem.unity

| GameObject | Componente | Field bindings |
|---|---|---|
| `LobbyUIManager` (a remover na Sprint 1) | `LobbyUIManager.cs` | `painelSelecao`, `painelLobby` (RectTransform) |
| Botões | `onClick` | métodos públicos do `LobbyUIManager` |

### 18.3 MenuScene.unity

| GameObject | Componente | Field bindings |
|---|---|---|
| `MenuLobbyPanel` | `MenuLobbyPanel.cs` | sem SerializeField, OnGUI puro |
| Algum botão | `onClick` | `MenuLobbyPanel.Mostrar()` |

### 18.4 CenaMapaTeste.unity / CenaMapaNOVO.unity

| GameObject | Componente | Field bindings |
|---|---|---|
| `NetworkManager` | NetworkManager + UnityTransport | NetworkPrefabsList contém todos os prefabs networked |
| `MatchManager` | MatchManager.cs | nenhum SerializeField crítico |
| `PlayerRegistry` | PlayerRegistry.cs | nenhum |
| `PlayerIdentityBridge` | PlayerIdentityBridge.cs | **NetworkObject obrigatório** |
| `BuildManager`, `GameSetupManager`, etc. | outros scripts (fora deste escopo) | |

### 18.5 Prefabs networked

| Prefab | Components críticos |
|---|---|
| `Player 1.prefab` | NetworkObject + ClientNetworkTransform + NetworkedPlayerController + PlayerNetworkSetup |
| Demais prefabs de personagem | mesmo conjunto |
| Inimigos | NetworkObject + NetworkTransform + NetworkedEnemy + EnemyController |
| Torres | NetworkObject + NetworkedBuilding + TowerController |
| Armadilhas (visual) | NetworkObject + NetworkedTrapVisual + (Animator) |
| Armadilhas (logic) | NetworkObject + TrapLogicBase |

**Regra:** se renomear um script C#, o Editor mostra "Missing Script". Use `Move-Item` no PowerShell (Unity rastreia GUID via meta), nunca `Rename-Item` direto. Para renomear classe: usar Refactor > Rename do IDE (Rider/VS), que atualiza .meta automaticamente.

---

## 19. Procedimento se um contrato precisar mudar

Se sua sprint absolutamente exigir uma mudança em algum item acima:

1. **Pare a sprint.** Não faça a mudança ainda.
2. Registre no log:
   ```
   [BLOQUEIO-CONTRATO] Sprint N tarefa N.X — necessário alterar <item>
   Motivo: <por quê>
   Impacto: <quais arquivos quebram>
   Alternativa considerada: <o que mais foi pensado>
   ```
3. Aguarde decisão do orquestrador.
4. Se aprovado:
   - Atualize este documento **no mesmo PR** que faz a mudança.
   - Inclua nota explicando a mudança no PR description.
   - Atualize chamadores ANTES de fazer commit final.

---

**Fim do `04_CONTRATOS_INTERFACE.md`.**
