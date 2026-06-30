# 05 — Glossário Técnico

> Termos, siglas e conceitos usados na documentação de refatoração. Consulte sempre que encontrar algo desconhecido.
> Ordem: alfabética por seção.

---

## Seção A — Bibliotecas e Frameworks

### NGO (Netcode for GameObjects)
Biblioteca oficial da Unity para multiplayer baseada em GameObjects e MonoBehaviours.
- Pacote: `com.unity.netcode.gameobjects` v1.12.0 neste projeto.
- Modelo: Server-authoritative por default; `NetworkVariable<T>` para sincronizar estado; `ServerRpc` e `ClientRpc` para mensagens direcionadas.
- Documentação: https://docs-multiplayer.unity3d.com/

### EOS (Epic Online Services)
Conjunto de serviços online da Epic (lobby, matchmaking, achievements, friends, etc.). Neste projeto usamos **Lobby** e **Connect** (autenticação anônima via Device ID).
- Plugin: `com.playeveryware.eos` (PlayEveryWare wrapper para Unity).
- Não confundir com **Epic Games Store** ou **Epic Online Subsystem (EOS) do Unreal**. Aqui é o serviço web da Epic, acessível via SDK.

### UGS (Unity Gaming Services)
Plataforma da Unity para serviços online. Neste projeto usamos somente o **Relay**.
- Bootstrap: `UGSBootstrap.cs`.

### Unity Relay
Serviço de relay/NAT traversal da UGS. Permite que clientes em redes diferentes (atrás de NAT, firewalls) conectem ao host sem port forwarding.
- API: `Unity.Services.Relay.RelayService.Instance.CreateAllocationAsync(maxConnections)`.
- O host obtém um `allocation` + `joinCode`; clientes resolvem o `joinCode` para `JoinAllocation` e conectam via Relay server.
- Tem fallback para IP direto se UGS não estiver disponível.

### MPPM (Multiplayer Play Mode)
Feature do Unity Editor 6 que permite rodar múltiplos clientes de teste num único Editor. Cada clone é um processo separado com seu próprio `Application.dataPath`.
- Pacote: `com.unity.multiplayer.playmode` v1.6.3.
- Acesso: `Window > Multiplayer > Play Mode > +Add`.
- **Atenção:** clones MPPM compartilham `SystemInfo` mas precisam de `DeviceId` distinto no EOS (resolvido em `EOSAuthenticator.DeleteDeviceIdThenCreate`).
- Helper customizado: `MppmHelper.cs` (detecção via command-line args, não pela env var oficial — substituição foi tentada e reverteu).

### UTP (Unity Transport)
Camada de transporte usada pelo NGO neste projeto. Pacote `com.unity.transport` v2.4.0.
- Componente Unity: `UnityTransport`, agregado ao GameObject do `NetworkManager`.
- Em MPPM/Editor: `SetConnectionData("0.0.0.0", port)` para o host; `SetConnectionData(serverIp, port)` para o cliente.
- Em build com Relay: `SetHostRelayData(...)` / `SetClientRelayData(...)`.

### PlayEveryWare EOS Plugin
Wrapper da PlayEveryWare sobre o SDK nativo EOS para Unity. Carrega credenciais de `StreamingAssets/EOS/` e expõe `EOSManager.Instance.GetEOSPlatformInterface()`.
- Componente principal: `EOSManager` (MonoBehaviour) — geralmente colocado num GameObject persistente.
- Wrapper customizado neste projeto: `EOSManagerWrapper.cs`.

---

## Seção B — Conceitos NGO

### NetworkManager.Singleton
Singleton global do NGO. Único por cena/processo. Expõe `IsServer`, `IsClient`, `IsHost`, `IsListening`, `LocalClientId`, `ConnectedClientsIds`, `SceneManager`, `ConnectionApprovalCallback`, etc.
- Crítico: `NetworkManager.Singleton.IsServer` é **diferente** do `IsServer` herdado de `NetworkBehaviour` antes de `OnNetworkSpawn` (ver "REGRA DE OURO NGO" abaixo).

### NetworkBehaviour
Subclasse de `MonoBehaviour` que adiciona suporte a NGO. Exige um `NetworkObject` no mesmo GameObject ou em parent.
- Métodos override: `OnNetworkSpawn()`, `OnNetworkDespawn()`.
- Properties úteis: `IsServer`, `IsClient`, `IsHost`, `IsOwner`, `IsSpawned`, `OwnerClientId`, `NetworkObjectId`.

### NetworkObject
Componente que marca um GameObject como sincronizável pela rede.
- Cada instância recebe um `NetworkObjectId` único.
- Spawn explícito via `networkObject.Spawn()` (server-only). Antes de Spawn, o objeto existe localmente mas não é visível a outros clientes.
- Despawn via `networkObject.Despawn(destroy: true/false)`.
- Deve ser registrado no `NetworkPrefabsList` do `NetworkManager`.

### NetworkVariable<T>
Wrapper que sincroniza um valor entre clientes automaticamente.
- Permissões: `NetworkVariableReadPermission` (Everyone/Owner) e `NetworkVariableWritePermission` (Server/Owner).
- Hooks: `OnValueChanged += (oldVal, newVal) => { ... }`. Subscribe em `OnNetworkSpawn`, unsubscribe em `OnNetworkDespawn`.
- Custoso quando muda alto-frequência — usar acumulador local (ver pattern em `MatchManager.MatchTime`).

### NetworkTransform
Componente que sincroniza transform (position/rotation/scale) pela rede.
- Server-authoritative por padrão.
- Para owner-authoritative (jogador local controla seu próprio transform), usar `ClientNetworkTransform` (custom override em `Assets/Codigo/Multiplayer/Sync/ClientNetworkTransform.cs`).

### ServerRpc
Método chamável de cliente para executar no servidor. Atributo `[ServerRpc]`.
- `RequireOwnership = false` permite que qualquer cliente chame (necessário para inimigos atacados por qualquer cliente).
- Pode receber `ServerRpcParams rpcParams` como último parâmetro — `rpcParams.Receive.SenderClientId` identifica o chamador.

### ClientRpc
Método chamável de servidor para executar em todos os clientes (ou subset via `ClientRpcParams`). Atributo `[ClientRpc]`.
- Para enviar a um cliente específico: `ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { targetId } } }`.

### IsServer / IsClient / IsHost / IsOwner
Estado do nó NGO atual (acessível em `NetworkBehaviour` ou `NetworkManager.Singleton`):
- `IsServer` — sou o servidor (autoridade).
- `IsClient` — sou um cliente (incluindo host, que é client+server simultaneamente).
- `IsHost` — sou host (server E client).
- `IsOwner` — eu sou o owner deste NetworkObject específico (ex.: o jogador local sobre seu próprio personagem).
- **Importante:** `IsServer` herdado de `NetworkBehaviour` é `IsSpawned && NetworkManager.Singleton.IsServer`. Antes de `Spawn()` retorna `false` mesmo no servidor. Use `NetworkManager.Singleton.IsServer` para checagens **pré-Spawn**.

### Connection Approval
Mecanismo do NGO para o servidor validar/rejeitar conexões antes de aceitar o cliente.
- Habilitar: `NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true` ANTES de `StartHost()`.
- Callback: `NetworkManager.Singleton.ConnectionApprovalCallback = (req, res) => { ... }`.
- Cliente envia payload: `NetworkManager.Singleton.NetworkConfig.ConnectionData = bytes` antes de `StartClient()`.
- Servidor lê: `req.Payload` (byte[]) e responde via `res.Approved`, `res.CreatePlayerObject`, etc.
- **Neste projeto:** payload de 4 bytes carrega o `characterIndex` (`BitConverter.ToInt32`).

### Spawn vs Despawn vs Destroy
- `Spawn()` — registra um NetworkObject na rede. Server-only. Antes de Spawn, o objeto não é visível a outros clientes.
- `Despawn(destroy: bool)` — remove da rede. Se `destroy=true`, também faz `Destroy(gameObject)` em todos os clientes.
- `Destroy(go)` direto sem `Despawn` primeiro pode deixar referências quebradas em outros clientes. **Sempre `Despawn` antes.**

### Scene Management (NetworkSceneManager)
NGO tem seu próprio gerenciador de cenas, separado de `UnityEngine.SceneManagement.SceneManager`.
- Acesso: `NetworkManager.Singleton.SceneManager`.
- `LoadScene(sceneName, LoadSceneMode.Single)` carrega cena em todos os clientes conectados.
- Requer `EnableSceneManagement = true` no `NetworkManager`.
- Cenas devem estar no `Build Settings`.

---

## Seção C — Conceitos EOS

### ProductUserId (PUID)
Identificador único de usuário EOS dentro de um Product. **Persistente entre sessões** se o Device ID for o mesmo.
- Formato: string opaca tipo `00027a1d05ab40e7a3c4b3e5...`.
- Em código C# do EOS SDK: tipo `Epic.OnlineServices.ProductUserId`. Converter com `ProductUserId.FromString(str)`.
- **Crítico:** clones MPPM precisam ter PUIDs diferentes (resolvido via Device ID distinto por clone).

### Device ID
Credencial anônima da EOS Connect. Não exige conta Epic.
- Criar: `connectInterface.CreateDeviceId(...)` com um `DeviceModel` string única por clone.
- Login: `Credentials { Type = ExternalCredentialType.DeviceidAccessToken, Token = null }`.
- Resultado: `LocalUserId` (= PUID).
- Em MPPM: o `DeviceModel` inclui o `CloneId` para garantir PUIDs únicos.

### Lobby (EOS Lobby Service)
Serviço EOS para criar/listar salas de espera. Não é gameplay — apenas discovery + sincronização de atributos.
- `LobbyInterface` — API principal.
- `LobbyDetails` — handle de uma sala específica. **Deve ser `Release()`-ado** quando não for mais usado (handle leak vaza memória nativa).
- `CreateLobby`, `JoinLobby`, `SearchLobby`, `LeaveLobby` — operações.
- Atributos: chave-valor public/private associados ao lobby ou a membros.
- **Atributos publicados pelo projeto** (constants em `Lobby/LobbyData.cs`):
  - `LOBBY_NAME`, `MAP_NAME`, `MAX_PLAYERS`, `LOBBY_STATE`, `SERVER_ADDRESS`, `SERVER_PORT`, `RELAY_CODE` — globais ao lobby.
  - `DISPLAY_NAME`, `IS_READY`, `CHARACTER_INDEX` — por membro.

### Notification Handlers (EOS)
Callbacks que o servidor EOS dispara assincronamente. Devem ser registrados E **removidos** no destroy.
- `AddNotifyLobbyMemberStatusReceived` — membro entrou/saiu/foi kicked.
- `AddNotifyLobbyUpdateReceived` — atributos do lobby mudaram (ex.: `SERVER_ADDRESS` publicado pelo host).
- `AddNotifyLobbyMemberUpdateReceived` — atributos de membro mudaram (ex.: `IS_READY` togglado).

### EOS_DISABLE
Diretiva de pré-processador C#. Quando definida, todo o código EOS é compilado fora.
- Usado para builds que precisam excluir EOS (ex.: testes que não querem network).
- No projeto, aparece como `#if !EOS_DISABLE ... #endif` envolvendo blocos EOS.
- **Não remover sem entender o motivo.**

---

## Seção D — Conceitos Unity gerais

### MonoBehaviour
Classe base do Unity para scripts anexados a GameObjects. Lifecycle: `Awake → OnEnable → Start → Update → ... → OnDestroy`.

### Singleton (Pattern)
Padrão de design — única instância de uma classe acessível globalmente.
- **No projeto:** vários `Instance` getters com `auto-create` (criam GameObject novo se nulo). Isso mascara problemas de ordem de execução.
- Alternativa preferida: `GetExistingInstance()` que retorna null se não existir, forçando o chamador a tratar ausência.

### DDOL (DontDestroyOnLoad)
`UnityEngine.Object.DontDestroyOnLoad(go)` — marca GameObject para sobreviver troca de cena.
- **Requisito:** o GameObject deve estar em **root** (sem parent). Por isso vários singletons do projeto fazem `transform.SetParent(null)` antes de DDOL.

### ScriptableObject (SO)
Asset persistente em disco que armazena dados. Diferente de MonoBehaviour, não fica em GameObject.
- Usado em `EOSConfig.cs` para credenciais (com `[NonSerialized]` para não persistir secrets).
- Carregamento: `Resources.Load<MyConfig>("PathNoResources")` ou `[SerializeField] reference no Inspector`.

### Coroutine
Função especial Unity que pode pausar com `yield return`. Iniciada com `StartCoroutine(MyCoroutine())`.
- `yield return null` — espera 1 frame.
- `yield return new WaitForSeconds(x)` — espera tempo real.
- `yield return new WaitUntil(() => condition)` — espera condição.
- `StopCoroutine(coroutineHandle)` — para.
- **Importante:** se o GameObject que iniciou é destruído, a coroutine para silenciosamente. Coroutines em singletons DDOL sobrevivem trocas de cena.

### Inspector (Unity Editor)
Painel onde se editam properties de MonoBehaviours/SOs.
- `[SerializeField]` em campo privado o expõe no Inspector.
- `[Header("…")]` agrupa campos.
- `[Tooltip("…")]` adiciona hint.
- **Referências no Inspector** (drag-and-drop de outros componentes) são salvas via `fileID` no `.unity` ou `.prefab`. Renomear o campo C# **quebra a referência** silenciosamente.

### Prefab
Asset que serializa um GameObject completo + componentes + configurações.
- Variants: prefab que herda de outro prefab. **Atenção:** prefab variants têm `fileID` virtual que quebra spawn em standalone (ver `bug_enemy_spawn_build.md` da memória).

### Meta Files (.cs.meta, .unity.meta, etc.)
Cada asset Unity tem um arquivo `.meta` com seu GUID (identificador único) + import settings.
- **Não apagar separadamente.** Apagar `Foo.cs` mas deixar `Foo.cs.meta` cria warning no Editor. Apagar só `.meta` reordena GUIDs e quebra referências.

### #if UNITY_EDITOR
Diretiva de pré-processador. Código entre `#if UNITY_EDITOR` e `#endif` só compila no Editor — não vai para build.
- Útil para `Debug.Log` em hot paths (evita poluir Player.log e alocar GC string em produção).

---

## Seção E — Termos específicos deste projeto

### "REGRA DE OURO NGO"
Convenção interna do projeto. Refere-se à regra: **não use `IsServer` herdado de `NetworkBehaviour` antes de `Spawn()`**.
- `IsServer` herdado é `IsSpawned && NetworkManager.Singleton.IsServer` — retorna `false` antes de Spawn mesmo no servidor.
- Use `NetworkManager.Singleton.IsServer` diretamente em métodos chamados PRÉ-Spawn (ex.: `InitializeServer`, `InitializeTowerServer`).
- Documentada em `NetworkedTrapVisual.cs:73-78` e `NetworkedBuilding.cs:84-87`.

### "audit comment" (A1, A4, A5, C5, etc.)
Comentário de auditoria iterativa em `LobbyManager.cs`. Cada tag refere-se a um item de checklist resolvido em sprints anteriores:
- `A1` — anti-leak guards em `LobbyModification` handles.
- `A4` — try/finally em `LobbySearchCopySearchResultByIndex`.
- `A5` — propagação de erros via `OnError` em vez de log silencioso.
- `C4` — anti-double-fire em `OnEOSInitialized`.
- `C5` — placeholder síncrono de `_currentLobby` antes de `_isInLobby = true`.

**Não remover esses comentários.** Cada um documenta um bug específico.

### "SYNC-FIX"
Tag de comentário em `LobbyManager.cs` indicando código adicionado para corrigir uma race condition de sincronização específica (verificar atributos imediatamente após join, em vez de esperar notificação).

### "OPTIMIZATION (Sprint X / Item Y)"
Tag de comentário indicando otimização de performance feita em sprint específica. Ex.: rate-limit em `SearchLobbies` (Sprint 3 / Item A3) ou tick fallback em `EOSManagerWrapper` (Sprint 3 / Item A2).

### `sessionToken`
GUID gerado em `field initializer` de `SessionManager` (`= System.Guid.NewGuid().ToString()`).
- Único por **processo** (não por usuário).
- Usado para distinguir clones MPPM que compartilham `productUserId` em cache.
- **Não pode ser nulo nem mudar durante a vida do processo.** Mudar a inicialização (ex.: lazy init no Awake) reintroduz race condition documentada em "Identidade 3 Março 2026".

### "Bridge" (PlayerIdentityBridge)
Padrão usado para conectar dois sistemas de identidade: NGO (`clientId` ulong) e EOS (`productUserId` string).
- O cliente, ao spawnar localmente, chama `RegisterPlayerServerRpc(userId, token)` enviando sua identidade EOS para o servidor.
- O servidor registra: `clientId → (userId, token)` e propaga para `PlayerRegistry`.

### "God Class"
Anti-pattern: classe que assume responsabilidades múltiplas e descorrelacionadas.
- **No projeto:** `LobbyManager.cs` (1626 LOC) mistura EOS + NGO + Relay + Scene + Connection Approval + IP discovery + Member ordering. Refatoração principal é quebrá-la em 4 classes.

### "Ratchet"
Princípio do Quality Gate: o código atual pode ser ruim, mas **não pode piorar**.
- Detalhado em `01_QUALITY_GATE.md` §1.

### "Hot path"
Trecho de código que executa muitas vezes por segundo. Em Unity: `Update`, `FixedUpdate`, `LateUpdate`, hooks de `NetworkVariable.OnValueChanged` em combate, callbacks de física.
- Regra geral: nada de alocação GC, `FindObjectOfType`, ou `string concatenation` em hot path.

---

## Seção F — Termos de arquitetura do código

### Owner-authoritative vs Server-authoritative
- **Server-authoritative:** servidor decide. Cliente apenas pede via RPC e recebe estado via NetworkVariable. Padrão para inimigos, projéteis, mundo. **Mais seguro.**
- **Owner-authoritative:** dono do objeto decide. Cliente local controla seu transform; servidor confia. Padrão usado em `ClientNetworkTransform` para movimentação fluida do jogador local.

### Connection Approval Payload
Bytes que o cliente envia ao se conectar. No projeto, são **4 bytes** = `BitConverter.GetBytes(characterIndex)`.
- Codificação: `nm.NetworkConfig.ConnectionData = System.BitConverter.GetBytes(myCharIndex)`.
- Decodificação no servidor: `int charIndex = BitConverter.ToInt32(req.Payload, 0)`.

### `CharacterChoiceCache`
Dicionário estático em `Core/CharacterChoiceCache.cs` que armazena `ClientId → CharacterIndex`.
- Populado em duas frentes: (a) `LobbyManager.OnNgoConnectionApproval` (servidor lê do payload), (b) `LobbyManager.StartMatchCoroutine` (host registra o próprio antes do StartHost).
- Consumido por: `GameSetupManager` (script fora do escopo Multiplayer) para spawnar o prefab correto.

### `PartySlotLayout`
Helper estático em `Core/PartySlotLayout.cs`. Calcula qual slot da `equipeSelecionada` é o Comandante de cada jogador baseado em (a) quantos jogadores no lobby, (b) índice canônico do jogador.

### Coroutine "fire-and-forget"
Coroutine iniciada sem armazenar handle e sem ser cancelável depois. Usada quando o resultado não importa após disparada (ex.: `ForceLeaveImmediate` envia `LeaveLobby` ao EOS em fire-and-forget pra não bloquear a transição de cena).

---

## Seção G — Termos de processo (refatoração)

### Smoke test
Teste manual mínimo: rodar o sistema em condição típica e verificar que não quebrou. Para multiplayer:
1. Editor + 1 clone MPPM.
2. Editor (host) clica "Criar Sala".
3. Clone (cliente) entra na sala via ID ou busca.
4. Ambos confirmam Ready e selecionam personagem.
5. Host clica "Iniciar Partida".
6. Verificar que ambos chegam em `CenaMapaTeste` com seus personagens visíveis.

### Rollback
Reverter mudanças. Em git: `git restore <arquivo>` ou `git revert <commit>`.
- **Não usar `git reset --hard` em branch compartilhada** — destrói commits de outros.

### Aceitação (critério de)
Condição objetiva, verificável, que indica que uma tarefa está completa.
- **Bom:** "Compila sem warnings novos; LOC do LobbyManager é menor que 1626; smoke test passa em MPPM."
- **Ruim:** "Código ficou mais limpo."

### LOC (Lines of Code)
Contagem de linhas de um arquivo. Inclui linhas em branco e comentários (Quality Gate usa essa contagem).
- Comando útil: `(Get-Content arquivo.cs | Measure-Object -Line).Lines` no PowerShell.

### Baseline (Quality Gate)
Snapshot em `quality/baseline.json` do estado "aceito" da branch `main`.
- PRs são comparados contra o baseline.
- Regressões bloqueiam; melhorias passam.
- **Não atualizar baseline em PR feature.** Atualização é commit separado em `main`.

### PR (Pull Request)
Solicitação de merge de uma branch em outra (geralmente em `main`).
- Cada sprint resulta em 1 PR.
- PR pequeno (uma sprint = um PR) facilita revisão e rollback.

---

**Fim do `05_GLOSSARIO.md`.**

Se um termo que você precisou não está aqui, registre no log da sprint e o orquestrador adicionará.
