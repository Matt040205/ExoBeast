# Gemini - Executor de Alteracoes Diretas
# Projeto: ExoBeasts V3 - Migracao Multiplayer NGO

## Seu Papel

Voce eh o **executor** da migracao multiplayer do ExoBeasts V3.
Suas responsabilidades:
1. **Alterar** scripts conforme as instrucoes detalhadas abaixo
2. **Seguir** os padroes de codigo obrigatorios SEM desvio
3. **Respeitar** a ordem de dependencias entre scripts
4. **Testar mentalmente** cada alteracao antes de entregar
5. **Reportar** ao Claude (orquestrador) para verificacao

**REGRA ABSOLUTA:** Nunca altere um script sem ler o codigo original completo primeiro.
**REGRA ABSOLUTA:** Nunca remova funcionalidade existente de singleplayer.
**REGRA ABSOLUTA:** Toda alteracao deve compilar sem erros.

---

## Contexto Tecnico

- **Engine:** Unity 6 com Netcode for GameObjects (NGO) 1.12.0
- **Transporte:** Unity Transport 2.4.0 (UDP P2P)
- **Modelo:** P2P com Host (1 jogador eh servidor+cliente, outros sao clientes)
- **Max jogadores:** 4
- **Arquitetura:** Start-As-Host (singleplayer = Host local sem clientes remotos)
- **Movimento:** Owner-authoritative via ClientNetworkTransform
- **Animacoes:** Owner-authoritative via ClientNetworkAnimator
- **Projeteis rapidos:** Visuais locais (NAO sao NetworkObjects)

---

## PADROES DE CODIGO OBRIGATORIOS

### Padrao 1: Migrar MonoBehaviour para NetworkBehaviour

```csharp
// ANTES (errado para multiplayer)
using UnityEngine;

public class MeuScript : MonoBehaviour
{
    private float vida = 100f;

    void Start()
    {
        vida = 100f;
        ConfigurarUI();
    }

    void Update()
    {
        ProcessarInput();
        AtualizarVisuais();
    }
}

// DEPOIS (correto para multiplayer)
using UnityEngine;
using Unity.Netcode;

public class MeuScript : NetworkBehaviour
{
    // NetworkVariable para dados que TODOS precisam ver
    // SEMPRE declarar no escopo da classe, NUNCA dentro de metodos
    public NetworkVariable<float> netVida = new NetworkVariable<float>(
        100f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Start() CONTINUA existindo para coisas que NAO dependem de rede
    // (cache de componentes locais, configuracao de UI)
    void Start()
    {
        // APENAS coisas locais que nao dependem de IsOwner/IsServer
        CachearComponentesLocais();
    }

    // OnNetworkSpawn() para TUDO que depende de rede
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            netVida.Value = 100f; // Server inicializa valores
        }

        if (IsOwner)
        {
            ConfigurarComoJogadorLocal();
        }
        else
        {
            ConfigurarComoJogadorRemoto();
        }

        // Registrar callbacks de NetworkVariable
        netVida.OnValueChanged += OnVidaChanged;
    }

    public override void OnNetworkDespawn()
    {
        netVida.OnValueChanged -= OnVidaChanged;
        base.OnNetworkDespawn();
    }

    void Update()
    {
        if (!IsOwner) return; // Input APENAS do dono
        ProcessarInput();
    }

    private void OnVidaChanged(float anterior, float nova)
    {
        // Roda em TODOS os clientes quando o servidor muda o valor
        AtualizarBarraDeVida(nova);
    }
}
```

### Padrao 2: ServerRpc + ClientRpc (Cadeia de Sincronizacao)

```csharp
// PADRAO UNIVERSAL para acoes do jogador que outros precisam ver:
// Owner detecta input → ServerRpc → Servidor valida → ClientRpc → Todos veem

// === NO SCRIPT DO JOGADOR ===

// Passo 1: Owner detecta input e chama ServerRpc
void Update()
{
    if (!IsOwner) return;

    if (Input.GetButtonDown("Fire1"))
    {
        // Feedback IMEDIATO para o owner (zero lag)
        ExecutarVisualLocalDeTiro(direcao);

        // Pedir ao servidor para processar
        AtirarServerRpc(direcao);
    }
}

// Passo 2: ServerRpc - servidor recebe e valida
[ServerRpc]
private void AtirarServerRpc(Vector3 direcao)
{
    // Servidor valida (cooldown, municao, etc)
    if (tempoUltimoTiro + cooldown > Time.time) return;
    tempoUltimoTiro = Time.time;

    // Servidor processa dano via raycast ou overlap
    if (Physics.Raycast(transform.position, direcao, out RaycastHit hit, alcance))
    {
        if (hit.collider.TryGetComponent<EnemyHealthSystem>(out var enemy))
        {
            enemy.TakeDamage(dano); // Server-to-server, sem RPC
        }
    }

    // Avisar TODOS os clientes para mostrar visual
    AtirarVisualClientRpc(direcao);
}

// Passo 3: ClientRpc - todos os clientes mostram o visual
[ClientRpc]
private void AtirarVisualClientRpc(Vector3 direcao)
{
    // Owner ja fez o visual no Passo 1, pular
    if (IsOwner) return;

    // Jogadores remotos veem o tiro
    ExecutarVisualLocalDeTiro(direcao);
}

// Metodo compartilhado de visual (SEM logica de rede)
private void ExecutarVisualLocalDeTiro(Vector3 direcao)
{
    animator.SetTrigger("Shoot");
    PlaySomDeTiro();
    SpawnProjetilVisual(direcao);
}
```

### Padrao 3: RequireOwnership = false (para ServerRpc em objetos compartilhados)

```csharp
// Quando um CLIENTE quer afetar um objeto que NAO eh dele
// Exemplo: jogador quer causar dano num inimigo (inimigo pertence ao servidor)

// NO SCRIPT DO JOGADOR (Owner = jogador):
[ServerRpc] // RequireOwnership = true (padrao) - OK, jogador eh owner
private void RequestDealDamageServerRpc(ulong enemyNetworkObjectId, float damage)
{
    // Servidor encontra o inimigo pelo NetworkObjectId
    if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(
        enemyNetworkObjectId, out NetworkObject enemyNO))
    {
        var enemyHealth = enemyNO.GetComponent<EnemyHealthSystem>();
        if (enemyHealth != null)
        {
            enemyHealth.TakeDamage(damage);
        }
    }
}

// ALTERNATIVA: ServerRpc no proprio objeto compartilhado
// Exemplo: inimigo recebe dano de qualquer jogador
[ServerRpc(RequireOwnership = false)] // QUALQUER cliente pode chamar
public void TakeDamageServerRpc(float damage, ServerRpcParams rpcParams = default)
{
    if (!IsServer) return;
    // rpcParams.Receive.SenderClientId = quem chamou (para anti-cheat)
    TakeDamage(damage);
}
```

### Padrao 4: NetworkVariable com OnValueChanged

```csharp
// Para dados que late-joiners precisam ver (vida, wave, dinheiro)

public NetworkVariable<float> netHealth = new NetworkVariable<float>(
    100f,                                    // valor inicial
    NetworkVariableReadPermission.Everyone,  // todos podem LER
    NetworkVariableWritePermission.Server    // so servidor pode ESCREVER
);

// Owner-writable (para posicao de mira, por exemplo):
public NetworkVariable<Vector3> netAimTarget = new NetworkVariable<Vector3>(
    Vector3.zero,
    NetworkVariableReadPermission.Everyone,
    NetworkVariableWritePermission.Owner  // dono pode escrever
);

public override void OnNetworkSpawn()
{
    base.OnNetworkSpawn();
    netHealth.OnValueChanged += OnHealthChanged;

    // Inicializar UI com valor ATUAL (para late-joiners)
    AtualizarBarraDeVida(netHealth.Value);
}

public override void OnNetworkDespawn()
{
    netHealth.OnValueChanged -= OnHealthChanged;
    base.OnNetworkDespawn();
}

private void OnHealthChanged(float oldVal, float newVal)
{
    AtualizarBarraDeVida(newVal);

    // Flash de dano quando vida diminui
    if (newVal < oldVal)
    {
        PlayHitFlash();
    }
}
```

### Padrao 5: Desativar Componentes em Jogadores Remotos

```csharp
public override void OnNetworkSpawn()
{
    base.OnNetworkSpawn();

    if (!IsOwner)
    {
        // Desativar TUDO que eh exclusivo do jogador local
        if (TryGetComponent<Camera>(out var cam)) cam.enabled = false;
        if (TryGetComponent<AudioListener>(out var listener)) listener.enabled = false;
        if (TryGetComponent<PlayerMovement>(out var mov)) mov.enabled = false;
        if (TryGetComponent<CharacterController>(out var cc)) cc.enabled = false;
        // O script ATUAL tambem pode se desativar
        this.enabled = false;
        return;
    }
}
```

### Padrao 6: Substituir FindObjectOfType / FindGameObjectWithTag

```csharp
// PROIBIDO:
var player = FindObjectOfType<PlayerMovement>();           // ERRADO
var player = GameObject.FindGameObjectWithTag("Player");   // ERRADO
var horde = FindObjectOfType<HordeManager>();              // ERRADO

// CORRETO - Para achar o jogador LOCAL:
var localPlayer = NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject();
var movement = localPlayer.GetComponent<PlayerMovement>();

// CORRETO - Para achar TODOS os jogadores (server-side):
// Usar PlayerRegistry (ja existe em Assets/Codigo/Multiplayer/GameServer/PlayerRegistry.cs)
var allPlayers = PlayerRegistry.Instance.GetAllPlayers();
var closestPlayer = PlayerRegistry.Instance.GetClosestPlayer(transform.position);

// CORRETO - Para Singletons de gerenciadores:
// Manter padrao Singleton.Instance, mas setar Instance em OnNetworkSpawn() se for NetworkBehaviour
public static HordeManager Instance { get; private set; }
public override void OnNetworkSpawn()
{
    base.OnNetworkSpawn();
    Instance = this;
}
```

### Padrao 7: Substituir SceneManager.LoadScene

```csharp
// PROIBIDO em sessao multiplayer:
UnityEngine.SceneManagement.SceneManager.LoadScene("Win");

// CORRETO:
if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
{
    // Em sessao de rede, usar o SceneManager do NGO
    NetworkManager.Singleton.SceneManager.LoadScene("Win", UnityEngine.SceneManagement.LoadSceneMode.Single);
}
else
{
    // Fora de sessao (menu, bootstrap), usar o padrao
    UnityEngine.SceneManagement.SceneManager.LoadScene("Win");
}
```

### Padrao 8: Coroutines Seguras em NetworkBehaviour

```csharp
// PROBLEMA: Coroutine em objeto despawnado = crash
// SOLUCAO 1: Verificar IsSpawned
private IEnumerator MinhaCoroutine()
{
    yield return new WaitForSeconds(1f);
    if (!IsSpawned) yield break; // Parar se despawnado
    FazerAlgo();
}

// SOLUCAO 2 (Unity 6 - preferida): Usar Awaitable com cancellation
public override void OnNetworkSpawn()
{
    base.OnNetworkSpawn();
    _ = MinhaRotina(destroyCancellationToken);
}

private async Awaitable MinhaRotina(CancellationToken token)
{
    await Awaitable.WaitForSecondsAsync(1f, token);
    FazerAlgo(); // So executa se nao foi cancelado
}
```

### Padrao 9: NetworkAnimator para Triggers

```csharp
// PROIBIDO (nao sincroniza entre clientes):
animator.SetTrigger("Jump");

// CORRETO (sincroniza via rede):
GetComponent<NetworkAnimator>().SetTrigger("Jump");
// ou se ja tiver referencia cacheada:
networkAnimator.SetTrigger("Jump");

// NOTA: SetFloat, SetBool e SetInteger sao sincronizados AUTOMATICAMENTE
// pelo NetworkAnimator. Apenas TRIGGERS precisam do metodo especial.
// IMPORTANTE: Usar ClientNetworkAnimator (nao NetworkAnimator) para owner-auth
```

### Padrao 10: Pool de Objetos com NGO

```csharp
// Para inimigos (NetworkObjects com pool):
public class EnemyPoolHandler : INetworkPrefabInstanceHandler
{
    private Queue<GameObject> pool = new Queue<GameObject>();
    private GameObject prefab;

    public EnemyPoolHandler(GameObject prefab, int initialSize)
    {
        this.prefab = prefab;
        for (int i = 0; i < initialSize; i++)
        {
            var obj = Object.Instantiate(prefab);
            obj.SetActive(false);
            pool.Enqueue(obj);
        }
    }

    // NGO chama isso em vez de Instantiate
    public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
    {
        GameObject obj;
        if (pool.Count > 0)
        {
            obj = pool.Dequeue();
        }
        else
        {
            obj = Object.Instantiate(prefab);
        }
        obj.transform.SetPositionAndRotation(position, rotation);
        obj.SetActive(true);
        return obj.GetComponent<NetworkObject>();
    }

    // NGO chama isso em vez de Destroy
    public void Destroy(NetworkObject networkObject)
    {
        networkObject.gameObject.SetActive(false);
        pool.Enqueue(networkObject.gameObject);
    }
}

// Registrar no NetworkManager:
// NetworkManager.Singleton.PrefabHandler.AddHandler(prefab, handler);
```

---

## INVENTARIO COMPLETO DE SCRIPTS E ACOES

### Legenda de Acoes
- **NB** = Migrar para NetworkBehaviour
- **MANTER** = Manter como MonoBehaviour (sem heranca de rede)
- **SO** = ScriptableObject (dados estaticos, sem alteracao)
- **REFATORAR** = Alteracoes significativas na logica
- **VINCULAR** = Conectar com wrapper multiplayer existente
- **CRIAR** = Script novo a ser criado

---

## SPRINT 1: FUNDACAO

### CRIAR: `Assets/Codigo/Managers/GameModeManager.cs`
**Tipo:** NetworkBehaviour Singleton (DontDestroyOnLoad)

```csharp
using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

namespace ExoBeasts.Managers
{
    public enum GameMode { Singleplayer, Multiplayer }

    public class GameModeManager : MonoBehaviour
    {
        public static GameModeManager Instance { get; private set; }
        public static GameMode CurrentMode { get; private set; } = GameMode.Singleplayer;

        [SerializeField] private string escolherPersonagemScene = "EscolherPersonagem";
        [SerializeField] private string lobbyScene = "LobbyScene";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Chamado pelo botao "Jogar Solo" no MenuManager.
        /// Seta modo singleplayer e vai para selecao de personagem.
        /// O NetworkManager.StartHost() sera chamado no GameSetupManager
        /// ao carregar a cena de jogo.
        /// </summary>
        public void StartSingleplayer()
        {
            CurrentMode = GameMode.Singleplayer;
            SceneManager.LoadScene(escolherPersonagemScene);
        }

        /// <summary>
        /// Chamado pelo botao "Jogar Online" no MenuManager.
        /// Seta modo multiplayer e vai para lobby (auth + matchmaking).
        /// </summary>
        public void StartMultiplayer()
        {
            CurrentMode = GameMode.Multiplayer;
            SceneManager.LoadScene(lobbyScene);
        }

        /// <summary>
        /// Auxiliar: verifica se estamos em modo multiplayer com clientes remotos.
        /// Util para decidir se usa NetworkManager.SceneManager ou SceneManager padrao.
        /// </summary>
        public static bool IsNetworkSession
        {
            get
            {
                return NetworkManager.Singleton != null &&
                       NetworkManager.Singleton.IsListening;
            }
        }

        /// <summary>
        /// Carrega cena de forma segura (usa NGO se em sessao de rede).
        /// Apenas o servidor/host pode chamar em sessao de rede.
        /// </summary>
        public static void LoadSceneSafe(string sceneName)
        {
            if (IsNetworkSession && NetworkManager.Singleton.IsServer)
            {
                NetworkManager.Singleton.SceneManager.LoadScene(
                    sceneName, LoadSceneMode.Single);
            }
            else if (!IsNetworkSession)
            {
                SceneManager.LoadScene(sceneName);
            }
        }
    }
}
```

### MODIFICAR: `Assets/Codigo/Managers/MenuManager.cs`
**Acao:** MANTER MonoBehaviour, adicionar integracao com GameModeManager

**Alteracoes exatas:**
1. Adicionar dois botoes SerializeField:
```csharp
[Header("Multiplayer")]
[SerializeField] private Button botaoJogarSolo;
[SerializeField] private Button botaoJogarOnline;
```

2. No `Start()` ou `Awake()`, registrar listeners:
```csharp
if (botaoJogarSolo != null)
    botaoJogarSolo.onClick.AddListener(() => GameModeManager.Instance.StartSingleplayer());
if (botaoJogarOnline != null)
    botaoJogarOnline.onClick.AddListener(() => GameModeManager.Instance.StartMultiplayer());
```

3. Se existir botao antigo de "Jogar" que carrega cena diretamente, manter como fallback para solo:
```csharp
// Botao antigo agora redireciona para singleplayer
public void BotaoJogar()
{
    GameModeManager.Instance.StartSingleplayer();
}
```

### MODIFICAR: `Assets/Codigo/Managers/Saves/GameSetupManager.cs`
**Acao:** REFATORAR spawn de jogador

**Alteracoes exatas:**
1. No metodo de spawn de jogador (provavelmente em `Start()` ou similar):
   - Mover logica de spawn para callback `OnClientConnectedCallback`
   - Usar `spawnPoints[]` array para posicionar jogadores sem sobreposicao
   - Registrar jogador no PlayerRegistry apos spawn

```csharp
// ANTES (provavelmente):
void Start()
{
    var prefab = GameDataManager.Instance.equipeSelecionada[0].prefab;
    Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
}

// DEPOIS:
private void Start()
{
    if (GameModeManager.CurrentMode == GameMode.Singleplayer)
    {
        // Singleplayer: iniciar como Host e spawnar
        NetworkManager.Singleton.StartHost();
    }
    // Em multiplayer, o NetworkManager ja foi iniciado pelo LobbyManager

    NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
}

private void OnClientConnected(ulong clientId)
{
    if (!NetworkManager.Singleton.IsServer) return;

    // Determinar personagem (solo: local | multi: payload)
    int charIndex = GetCharacterIndexForClient(clientId);
    var prefab = characterPrefabs[charIndex];

    // Spawn point baseado no numero de jogadores
    int playerIndex = NetworkManager.Singleton.ConnectedClientsList.Count - 1;
    Transform sp = spawnPoints[playerIndex % spawnPoints.Length];

    var player = Instantiate(prefab, sp.position, sp.rotation);
    player.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
}

private int GetCharacterIndexForClient(ulong clientId)
{
    if (GameModeManager.CurrentMode == GameMode.Singleplayer)
    {
        return GameDataManager.Instance.equipeSelecionada[0].characterIndex;
    }
    else
    {
        // Ler do Connection Approval Payload
        // (implementar em NetworkBootstrap.ConnectionApproval)
        return SessionManager.Instance.GetCharacterForClient(clientId);
    }
}

private void OnDestroy()
{
    if (NetworkManager.Singleton != null)
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
}
```

---

## SPRINT 2: PREFAB DO JOGADOR (10 scripts)

### 2.1 CameraController.cs
**Caminho:** `Assets/Codigo/Char scripts/Player/CameraController.cs`
**Acao:** NB (MonoBehaviour → NetworkBehaviour)

**Alteracoes exatas:**
1. Trocar heranca: `MonoBehaviour` → `NetworkBehaviour`
2. Adicionar: `using Unity.Netcode;`
3. Adicionar `OnNetworkSpawn()`:
```csharp
public override void OnNetworkSpawn()
{
    base.OnNetworkSpawn();

    if (!IsOwner)
    {
        // Desativar camera e audio de jogadores remotos
        var cam = GetComponentInChildren<Camera>();
        if (cam != null) cam.enabled = false;

        var listener = GetComponentInChildren<AudioListener>();
        if (listener != null) listener.enabled = false;

        // Desativar Cinemachine cameras se existirem
        var vcams = GetComponentsInChildren<CinemachineCamera>(true);
        foreach (var vcam in vcams)
            vcam.enabled = false;

        this.enabled = false; // Nao processar Update/LateUpdate
        return;
    }
}
```
4. Se `Start()` tem inicializacao, mover parte de rede para `OnNetworkSpawn()`
5. `Update()`/`LateUpdate()`: Ja protegido por `this.enabled = false` nos remotos

### 2.2 ThirdPersonCamera.cs
**Caminho:** `Assets/Codigo/Char scripts/Player/ThirdPersonCamera.cs`
**Acao:** NB (MonoBehaviour → NetworkBehaviour)

**Alteracoes IDENTICAS ao CameraController:**
1. Trocar heranca → `NetworkBehaviour`
2. Adicionar `using Unity.Netcode;`
3. Adicionar `OnNetworkSpawn()` com desativacao de Camera/AudioListener/Cinemachine para `!IsOwner`
4. Manter TODA a matematica de camera intacta (zoom, transicao 1a/3a pessoa)

### 2.3 PlayerMovement.cs
**Caminho:** `Assets/Codigo/Char scripts/Player/PlayerMovement.cs`
**Acao:** JA EH NetworkBehaviour - REFATORAR

**Alteracoes exatas:**
1. Mover inicializacoes de `Start()` para `OnNetworkSpawn()`:
```csharp
public override void OnNetworkSpawn()
{
    base.OnNetworkSpawn();

    if (!IsOwner)
    {
        // Desabilitar CharacterController em remotos (ClientNetworkTransform controla)
        if (TryGetComponent<CharacterController>(out var cc))
            cc.enabled = false;
        this.enabled = false;
        return;
    }

    // Inicializacoes que estavam no Start() para o owner
    // (cache de referencias, configuracao de camera, etc)
    InitializeLocalPlayer();
}
```

2. Refatorar `Update()`:
```csharp
void Update()
{
    if (!IsOwner) return; // DEVE ser a primeira linha

    // Toda a logica de input e movimento continua aqui
    // (o ClientNetworkTransform sincroniza a posicao automaticamente)
}
```

3. Substituir `FindObjectOfType<CameraController>()`:
```csharp
// ANTES:
cameraController = FindObjectOfType<CameraController>();

// DEPOIS (no OnNetworkSpawn, apos IsOwner check):
cameraController = GetComponentInChildren<CameraController>();
// OU se camera eh filho do prefab:
cameraController = Camera.main?.GetComponent<CameraController>();
```

4. Animacoes - trocar triggers:
```csharp
// ANTES:
animator.SetTrigger("Jump");

// DEPOIS:
GetComponent<NetworkAnimator>().SetTrigger("Jump");
// NOTA: SetFloat("Speed", ...) e SetBool("IsGrounded", ...) NAO precisam mudar
// O ClientNetworkAnimator sincroniza parametros float/bool/int automaticamente
```

5. Vincular com TopDownCameraManager:
```csharp
// No OnNetworkSpawn(), apos IsOwner check:
if (IsOwner && TopDownCameraManager.Instance != null)
{
    TopDownCameraManager.Instance.SetCameraTarget(transform);
}
```

### 2.4 PlayerHealthSystem.cs
**Caminho:** `Assets/Codigo/Char scripts/Player/PlayerHealthSystem.cs`
**Acao:** JA EH NetworkBehaviour - REFATORAR

**Alteracoes exatas:**
1. Verificar NetworkVariables existentes. Deve ter:
```csharp
public NetworkVariable<float> currentHealth = new NetworkVariable<float>(
    100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

public NetworkVariable<float> maxHealth = new NetworkVariable<float>(
    100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
```

2. Adicionar NetworkVariables para buffs (se nao existirem):
```csharp
public NetworkVariable<float> speedMultiplier = new NetworkVariable<float>(
    1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

public NetworkVariable<float> damageMultiplier = new NetworkVariable<float>(
    1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

public NetworkVariable<float> damageResistance = new NetworkVariable<float>(
    0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
```

3. Mover `Start()` → `OnNetworkSpawn()`:
```csharp
public override void OnNetworkSpawn()
{
    base.OnNetworkSpawn();

    if (IsServer)
    {
        currentHealth.Value = maxHealth.Value;
    }

    // Registrar no HUD local
    if (IsOwner && PlayerHUD.Instance != null)
    {
        PlayerHUD.Instance.RegistrarJogador(this);
    }

    // Callback para UI
    currentHealth.OnValueChanged += OnHealthChanged;
}
```

4. Proteger `TakeDamage()` com server-check:
```csharp
public void TakeDamage(float damage)
{
    if (!IsServer) return; // APENAS servidor aplica dano

    float finalDamage = damage * (1f - damageResistance.Value);
    currentHealth.Value = Mathf.Max(0, currentHealth.Value - finalDamage);

    // Visual de hit para todos
    TakeDamageVisualClientRpc();

    if (currentHealth.Value <= 0)
    {
        Die();
    }
}

[ClientRpc]
private void TakeDamageVisualClientRpc()
{
    // Flash vermelho, som de dano, etc
    PlayHitEffect();
}
```

5. Refatorar `Die()`:
```csharp
private void Die()
{
    if (!IsServer) return;

    // Resetar vida no servidor
    currentHealth.Value = maxHealth.Value;

    // Respawn em todos os clientes
    RespawnClientRpc(GetSpawnPosition());
}

[ClientRpc]
private void RespawnClientRpc(Vector3 spawnPosition)
{
    if (IsOwner)
    {
        // Apenas o owner precisa teleportar (CharacterController precisa desligar/ligar)
        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        transform.position = spawnPosition;
        if (cc != null) cc.enabled = true;
    }

    // Todos veem efeito de respawn
    PlayRespawnEffect();
}
```

### 2.5 PlayerShooting.cs
**Caminho:** `Assets/Codigo/Char scripts/Player/PlayerShooting.cs`
**Acao:** JA EH NetworkBehaviour - REFATORAR

**Alteracoes exatas:**
1. Mover `Start()` → `OnNetworkSpawn()`:
```csharp
public override void OnNetworkSpawn()
{
    base.OnNetworkSpawn();

    if (!IsOwner)
    {
        this.enabled = false; // Remotos nao processam input de tiro
        return;
    }

    // Inicializacoes do owner (cache de pool, configuracao de mira, etc)
    InitializeShootingLocal();
}
```

2. `Update()` ja deve ter `if (!IsOwner) return;` - verificar

3. Criar cadeia de tiro sincronizada:
```csharp
// Owner atira (chamado pelo Update quando input detectado)
private void Shoot(Vector3 direction)
{
    // Visual IMEDIATO para owner (zero lag)
    SpawnProjectileVisual(direction);
    PlayShootSound();
    animator.SetTrigger("Shoot"); // Ou via NetworkAnimator

    // Pedir ao servidor para processar dano e avisar outros
    ShootServerRpc(direction);
}

[ServerRpc]
private void ShootServerRpc(Vector3 direction)
{
    // Servidor valida cooldown
    // Servidor faz raycast/overlap para dano
    // Servidor avisa outros clientes

    ShootVisualClientRpc(direction);
}

[ClientRpc]
private void ShootVisualClientRpc(Vector3 direction)
{
    if (IsOwner) return; // Owner ja fez visual

    SpawnProjectileVisual(direction);
    PlayShootSound();
}
```

4. Se ja tem `RequestDealDamageServerRpc`, MANTER e conectar com ProjectileVisual:
```csharp
// No PlayerShooting, metodo publico para ProjectileVisual chamar:
public void RequestDamageOnEnemy(ulong enemyNetworkObjectId, float damage)
{
    if (!IsOwner) return;
    RequestDealDamageServerRpc(enemyNetworkObjectId, damage);
}
```

5. Cadeia de recarga:
```csharp
[ServerRpc]
private void ReloadServerRpc()
{
    // Servidor valida
    ReloadVisualClientRpc();
}

[ClientRpc]
private void ReloadVisualClientRpc()
{
    if (IsOwner) return; // Owner ja tem feedback local
    PlayReloadAnimation();
    PlayReloadSound();
}
```

### 2.6 MeleeCombatSystem.cs
**Caminho:** `Assets/Codigo/Char scripts/Player/MeleeCombatSystem.cs`
**Acao:** NB (MonoBehaviour → NetworkBehaviour)

**Alteracoes exatas:**
1. Trocar heranca: `MonoBehaviour` → `NetworkBehaviour`
2. Adicionar: `using Unity.Netcode;`
3. Adicionar OnNetworkSpawn:
```csharp
public override void OnNetworkSpawn()
{
    base.OnNetworkSpawn();
    if (!IsOwner)
    {
        this.enabled = false;
        return;
    }
}
```

4. Proteger input:
```csharp
// Em OnFire() ou equivalente (chamado por input):
public void OnFire()
{
    if (!IsOwner) return;
    // logica de ataque melee
}
```

5. Refatorar `DetectHits()` (chamado por Animation Event):
```csharp
// DetectHits eh chamado por Animation Event (sincroniza via NetworkAnimator)
public void DetectHits()
{
    // Som toca para TODOS (animacao sincronizada pelo NetworkAnimator)
    PlayMeleeSwingSound();

    // Apenas Owner calcula hits
    if (!IsOwner) return;

    var hits = Physics.OverlapSphere(hitPoint.position, hitRadius, enemyLayer);
    foreach (var hit in hits)
    {
        if (hit.TryGetComponent<NetworkObject>(out var netObj))
        {
            // Pedir ao servidor para aplicar dano
            RequestMeleeDamageServerRpc(netObj.NetworkObjectId, meleeDamage);
        }
    }
}

[ServerRpc]
private void RequestMeleeDamageServerRpc(ulong targetNetworkObjectId, float damage)
{
    if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(
        targetNetworkObjectId, out NetworkObject target))
    {
        var enemyHealth = target.GetComponent<EnemyHealthSystem>();
        if (enemyHealth != null)
        {
            enemyHealth.TakeDamage(damage);
        }
    }
}
```

### 2.7 PlayerCombatManager.cs
**Caminho:** `Assets/Codigo/Char scripts/Player/PlayerCombatManager.cs`
**Acao:** NB (MonoBehaviour → NetworkBehaviour)

**Alteracoes exatas:**
1. Trocar heranca → `NetworkBehaviour`
2. Adicionar `using Unity.Netcode;`
3. Criar enum se nao existir:
```csharp
public enum CombatType : byte { Ranged = 0, Melee = 1 }
```

4. Adicionar NetworkVariable:
```csharp
public NetworkVariable<CombatType> netCombatType = new NetworkVariable<CombatType>(
    CombatType.Ranged,
    NetworkVariableReadPermission.Everyone,
    NetworkVariableWritePermission.Server
);
```

5. OnNetworkSpawn:
```csharp
public override void OnNetworkSpawn()
{
    base.OnNetworkSpawn();

    // Todos registram callback para atualizar visuais
    netCombatType.OnValueChanged += OnCombatTypeChanged;

    // Inicializar visual com valor atual
    UpdateWeaponVisuals(netCombatType.Value);

    if (!IsOwner) return;
    // Owner: ativar script de ataque correto
    ActivateAttackScript(netCombatType.Value);
}
```

6. Troca de arma via ServerRpc:
```csharp
void Update()
{
    if (!IsOwner) return;

    if (Input.GetKeyDown(KeyCode.Tab) || Input.GetButtonDown("SwitchWeapon"))
    {
        var newType = netCombatType.Value == CombatType.Ranged
            ? CombatType.Melee : CombatType.Ranged;
        RequestSwitchWeaponServerRpc(newType);
    }
}

[ServerRpc]
private void RequestSwitchWeaponServerRpc(CombatType newType)
{
    netCombatType.Value = newType;
}

private void OnCombatTypeChanged(CombatType oldType, CombatType newType)
{
    UpdateWeaponVisuals(newType);
    if (IsOwner) ActivateAttackScript(newType);
}

private void UpdateWeaponVisuals(CombatType type)
{
    // Ativar/desativar modelos 3D de armas para TODOS verem
    if (meleeWeaponModel != null) meleeWeaponModel.SetActive(type == CombatType.Melee);
    if (rangedWeaponModel != null) rangedWeaponModel.SetActive(type == CombatType.Ranged);
}

private void ActivateAttackScript(CombatType type)
{
    // Apenas owner ativa scripts de input
    if (playerShooting != null) playerShooting.enabled = (type == CombatType.Ranged);
    if (meleeCombat != null) meleeCombat.enabled = (type == CombatType.Melee);
}
```

### 2.8 CommanderAbilityController.cs
**Caminho:** `Assets/Codigo/Char scripts/JP/CommanderAbilityController.cs`
**Acao:** NB (MonoBehaviour → NetworkBehaviour)

**Alteracoes exatas:**
1. Trocar heranca → `NetworkBehaviour`
2. Adicionar `using Unity.Netcode;`
3. Proteger input:
```csharp
public override void OnNetworkSpawn()
{
    base.OnNetworkSpawn();
    if (!IsOwner)
    {
        this.enabled = false;
        return;
    }
}

void Update()
{
    if (!IsOwner) return;

    // Detectar input de habilidades (Q, E, X, etc)
    if (Input.GetKeyDown(KeyCode.Q)) ActivateAbility(0);
    if (Input.GetKeyDown(KeyCode.E)) ActivateAbility(1);
    if (Input.GetKeyDown(KeyCode.X)) ActivateUltimate();
}
```

4. Cada ativacao via ServerRpc:
```csharp
private void ActivateAbility(int index)
{
    // Feedback visual imediato para owner (preview, cursor change)
    ShowAbilityPreviewLocal(index);

    RequestActivateAbilityServerRpc(index);
}

[ServerRpc]
private void RequestActivateAbilityServerRpc(int abilityIndex)
{
    // Servidor valida cooldown e recursos
    if (!CanUseAbility(abilityIndex)) return;

    // Servidor executa logica de dano/buff
    ExecuteAbilityServer(abilityIndex);

    // Avisar todos para VFX
    AbilityVisualClientRpc(abilityIndex);
}

[ClientRpc]
private void AbilityVisualClientRpc(int abilityIndex)
{
    // Todos veem VFX, ouvem SFX
    PlayAbilityVFX(abilityIndex);
    PlayAbilitySFX(abilityIndex);
}
```

### 2.9 ProjectilePool.cs
**Caminho:** `Assets/Codigo/Char scripts/Player/ProjectilePool.cs`
**Acao:** MANTER MonoBehaviour Singleton

**Alteracoes:** NENHUMA. Pool local por cliente para projeteis visuais.
- Cada cliente tem seu proprio pool independente
- Projeteis rapidos NAO sao NetworkObjects (decisao arquitetural)
- Se futuramente precisar de projeteis lentos em rede, implementar INetworkPrefabInstanceHandler separado

### 2.10 ProjectileVisual.cs
**Caminho:** `Assets/Codigo/ProjectileVisual.cs`
**Acao:** MANTER MonoBehaviour - REFATORAR OnTriggerEnter

**Alteracoes exatas:**
1. No `OnTriggerEnter()`, envelopar dano:
```csharp
private void OnTriggerEnter(Collider other)
{
    // Visual (particula de impacto, etc) - todos fazem
    SpawnImpactVFX();

    // Dano - APENAS o owner do projetil
    // (o owner eh quem chamou SpawnProjectile no PlayerShooting)
    if (other.TryGetComponent<NetworkObject>(out var netObj))
    {
        // Buscar o PlayerShooting do jogador local para enviar ServerRpc
        var localPlayer = NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject();
        if (localPlayer != null)
        {
            var shooting = localPlayer.GetComponent<PlayerShooting>();
            if (shooting != null && shooting.IsOwner)
            {
                shooting.RequestDamageOnEnemy(netObj.NetworkObjectId, projectileDamage);
            }
        }
    }

    // Retornar ao pool
    ReturnToPool();
}
```

**ALTERNATIVA MAIS SIMPLES** (se o projetil ja tem referencia ao shooter):
```csharp
private void OnTriggerEnter(Collider other)
{
    SpawnImpactVFX();

    // Apenas owner do projetil pede dano
    if (isLocalPlayerProjectile && other.TryGetComponent<NetworkObject>(out var netObj))
    {
        ownerShooting.RequestDamageOnEnemy(netObj.NetworkObjectId, damage);
    }

    ReturnToPool();
}
```

---

## SPRINT 3: INIMIGOS E WAVES

### 3.1 EnemyHealthSystem.cs
**Caminho:** `Assets/Codigo/Enemy/EnemyHealthSystem.cs`
**Acao:** VINCULAR com NetworkedEnemy + REFATORAR

**Conceito:** Vida do inimigo eh controlada por `NetworkedEnemy.NetworkHealth` (ja existe).
EnemyHealthSystem continua como MonoBehaviour mas le/escreve via NetworkedEnemy.

**Alteracoes exatas:**
1. Adicionar referencia ao NetworkedEnemy:
```csharp
private NetworkedEnemy networkedEnemy;

void Awake()
{
    networkedEnemy = GetComponent<NetworkedEnemy>();
}
```

2. TakeDamage protegido:
```csharp
public void TakeDamage(float damage)
{
    // So o servidor pode causar dano
    if (networkedEnemy != null && !networkedEnemy.IsServer) return;

    float newHealth = currentHealth - damage;
    currentHealth = Mathf.Max(0, newHealth);

    // Atualizar NetworkVariable (servidor)
    if (networkedEnemy != null)
        networkedEnemy.NetworkHealth.Value = currentHealth;

    // Visual de hit para todos
    if (networkedEnemy != null)
        networkedEnemy.HitFlashClientRpc();

    if (currentHealth <= 0)
        Die();
}
```

3. Adicionar no NetworkedEnemy (se nao existir):
```csharp
[ClientRpc]
public void HitFlashClientRpc()
{
    // Flash vermelho no material do inimigo
    PlayHitFlashEffect();
    // Spawn damage popup local
    DamagePopup.Create(transform.position, lastDamageAmount);
}
```

### 3.2 EnemyCombatSystem.cs
**Caminho:** `Assets/Codigo/Enemy/EnemyCombatSystem.cs`
**Acao:** MANTER MonoBehaviour - REFATORAR

**Alteracoes exatas:**
1. Deteccao de colisao ja roda apenas no servidor (EnemyController server-only)
2. Dano no jogador - servidor chama diretamente:
```csharp
// ANTES:
player.GetComponent<PlayerHealthSystem>().TakeDamage(damage);

// DEPOIS (mesma coisa, pois TakeDamage verifica IsServer internamente):
player.GetComponent<PlayerHealthSystem>().TakeDamage(damage);
// FUNCIONA porque o servidor tem autoridade sobre AMBOS
// (inimigo e PlayerHealthSystem rodam no servidor)
```

3. Animacao de ataque sincronizada via NetworkAnimator no prefab do inimigo

### 3.3 EnemyPoolManager.cs
**Caminho:** `Assets/Codigo/Enemy/EnemyPoolManager.cs`
**Acao:** REFATORAR para INetworkPrefabInstanceHandler

**Alteracoes exatas:**
1. Implementar `INetworkPrefabInstanceHandler`:
```csharp
public class EnemyPoolManager : MonoBehaviour, INetworkPrefabInstanceHandler
{
    // ... campos existentes ...

    private void Start()
    {
        InitializePool();

        // Registrar handler para cada tipo de inimigo
        foreach (var prefab in enemyPrefabs)
        {
            NetworkManager.Singleton.PrefabHandler.AddHandler(
                prefab, this);
        }
    }

    // InitializePool DEVE rodar em TODOS (servidor E clientes)
    private void InitializePool()
    {
        foreach (var prefab in enemyPrefabs)
        {
            var queue = new Queue<GameObject>();
            for (int i = 0; i < poolSizePerType; i++)
            {
                var obj = Instantiate(prefab);
                obj.SetActive(false);
                queue.Enqueue(obj);
            }
            pools[prefab.name] = queue;
        }
    }

    // Chamado pelo NGO quando servidor faz Spawn
    public NetworkObject Instantiate(ulong ownerClientId, Vector3 pos, Quaternion rot)
    {
        // Pegar do pool
        var obj = GetFromPool();
        obj.transform.SetPositionAndRotation(pos, rot);
        obj.SetActive(true);
        return obj.GetComponent<NetworkObject>();
    }

    // Chamado pelo NGO quando servidor faz Despawn(false)
    public void Destroy(NetworkObject networkObject)
    {
        networkObject.gameObject.SetActive(false);
        ReturnToPool(networkObject.gameObject);
    }

    // Metodo publico para HordeManager pedir spawn
    public void SpawnEnemy(string enemyType, Vector3 position, Quaternion rotation)
    {
        if (!NetworkManager.Singleton.IsServer) return;

        var obj = GetFromPool(enemyType);
        if (obj == null) return;

        obj.transform.SetPositionAndRotation(position, rotation);
        obj.SetActive(true);
        obj.GetComponent<NetworkObject>().Spawn(true); // destroyWithScene = true
    }
}
```

### 3.4 UNIFICAR: HordeManager.cs + NetworkedHorde.cs
**Caminho HordeManager:** `Assets/Codigo/Managers/HordeManager.cs`
**Caminho NetworkedHorde:** `Assets/Codigo/Multiplayer/Sync/NetworkedHorde.cs`
**Acao:** Mover TODA logica de rede para HordeManager. DELETAR NetworkedHorde.cs

**Alteracoes no HordeManager:**
1. Ja eh NetworkBehaviour - adicionar NetworkVariables:
```csharp
public NetworkVariable<int> currentWave = new NetworkVariable<int>(
    0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

public NetworkVariable<int> enemiesAlive = new NetworkVariable<int>(
    0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

public NetworkVariable<bool> waveInProgress = new NetworkVariable<bool>(
    false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
```

2. Substituir `FindGameObjectWithTag("Player")`:
```csharp
// ANTES:
Transform playerTransform = GameObject.FindGameObjectWithTag("Player").transform;

// DEPOIS:
// Usar PlayerRegistry para achar todos os jogadores
var allPlayers = PlayerRegistry.Instance.GetAllPlayers();
// Para spawn de inimigos, usar posicao aleatoria ou media dos jogadores
Vector3 avgPlayerPos = PlayerRegistry.Instance.GetAveragePlayerPosition();
```

3. Substituir `SceneManager.LoadScene`:
```csharp
// ANTES:
SceneManager.LoadScene("Win");

// DEPOIS:
GameModeManager.LoadSceneSafe("Win");
```

4. Spawn de inimigos via EnemyPoolManager:
```csharp
private void SpawnWaveEnemies()
{
    if (!IsServer) return;

    foreach (var spawnData in currentWaveData.enemies)
    {
        Vector3 spawnPos = GetRandomSpawnPoint();
        EnemyPoolManager.Instance.SpawnEnemy(spawnData.type, spawnPos, Quaternion.identity);
        enemiesAlive.Value++;
    }

    waveInProgress.Value = true;
}
```

### 3.5 EnemyController.cs
**Caminho:** `Assets/Codigo/Enemy/EnemyControler.cs`
**Acao:** MANTER MonoBehaviour - REFATORAR targeting

**Conceito:** IA roda APENAS no servidor (NetworkedEnemy ja faz `enemyController.enabled = runAI`)

**Alteracoes exatas:**
1. Substituir `FindGameObjectWithTag("Player")` por multiplos jogadores:
```csharp
// ANTES:
private Transform target;
void Start()
{
    target = GameObject.FindGameObjectWithTag("Player").transform;
}

// DEPOIS:
private Transform target;

public void InitializeEnemy(/* params existentes */)
{
    // Buscar jogador mais proximo via PlayerRegistry
    UpdateTarget();
}

private void UpdateTarget()
{
    if (PlayerRegistry.Instance == null) return;

    Transform closest = PlayerRegistry.Instance.GetClosestPlayer(transform.position);
    if (closest != null)
        target = closest;
}

// Chamar periodicamente (a cada 1-2 segundos) para re-avaliar alvo
private float retargetTimer = 0f;
void Update()
{
    retargetTimer += Time.deltaTime;
    if (retargetTimer >= 1.5f)
    {
        retargetTimer = 0f;
        UpdateTarget();
    }

    // ... resto da IA existente ...
}
```

2. Verificar que todos os status effects (slow, root, knockback) funcionam server-side (ja devem funcionar pois IA roda no servidor)

### 3.6 EnemyDataSO.cs
**Caminho:** `Assets/Codigo/Enemy/EnemyDataSO.cs`
**Acao:** SO - SEM ALTERACAO

### 3.7 WorldSpaceEnemyUI.cs
**Caminho:** `Assets/Codigo/Enemy/WorldSpaceEnemyUI.cs`
**Acao:** MANTER - VINCULAR

**Alteracoes exatas:**
```csharp
// Adicionar no Start ou Awake:
private NetworkedEnemy networkedEnemy;

void Start()
{
    networkedEnemy = GetComponentInParent<NetworkedEnemy>();
    if (networkedEnemy != null)
    {
        networkedEnemy.NetworkHealth.OnValueChanged += OnHealthChanged;
        // Inicializar com valor atual
        UpdateHealthBar(networkedEnemy.NetworkHealth.Value);
    }
}

private void OnHealthChanged(float oldVal, float newVal)
{
    UpdateHealthBar(newVal);
}

void OnDestroy()
{
    if (networkedEnemy != null)
        networkedEnemy.NetworkHealth.OnValueChanged -= OnHealthChanged;
}
```

---

## SPRINT 4: CONSTRUCAO E ECONOMIA

### 4.1 BuildManager.cs
**Caminho:** `Assets/Codigo/Tower scripts/BuildManager.cs`
**Acao:** NB (MonoBehaviour → NetworkBehaviour) + REFATORAR

**Alteracoes exatas:**
1. Trocar heranca → `NetworkBehaviour`
2. Ghost preview CONTINUA local (zero lag)
3. PlaceBuilding → ServerRpc:
```csharp
// Owner (jogador) coloca torre
public void PlaceBuilding(int buildableID, Vector3 position, Quaternion rotation)
{
    // Validacao local rapida (evita roundtrip desnecessario)
    if (!CanAfford(buildableID)) return;

    RequestBuildServerRpc(buildableID, position, rotation);
}

[ServerRpc(RequireOwnership = false)]
private void RequestBuildServerRpc(int buildableID, Vector3 position, Quaternion rotation,
    ServerRpcParams rpcParams = default)
{
    ulong clientId = rpcParams.Receive.SenderClientId;

    // Servidor valida: custo, limite de traps, grid valido
    if (!ValidateBuild(buildableID, position, clientId)) return;

    // Deduzir custo
    NetworkedCurrency.Instance.SpendGeodites(GetBuildCost(buildableID));

    // Instanciar e spawnar
    var prefab = buildablePrefabs[buildableID];
    var building = Instantiate(prefab, position, rotation);
    building.GetComponent<NetworkObject>().Spawn(true);

    // Atualizar contagem de traps
    activeTrapCounts[buildableID]++;
}
```

### 4.2 CurrencyManager.cs
**Caminho:** `Assets/Codigo/Managers/CurrencyManager.cs`
**Acao:** VINCULAR com NetworkedCurrency

**Alteracoes exatas:**
1. Remover alteracao LOCAL de valores
2. Ler de NetworkedCurrency:
```csharp
void Start()
{
    // Vincular UI ao NetworkedCurrency
    if (NetworkedCurrency.Instance != null)
    {
        NetworkedCurrency.Instance.TeamGeodites.OnValueChanged += OnGeoditesChanged;
        NetworkedCurrency.Instance.TeamDarkEther.OnValueChanged += OnDarkEtherChanged;

        // Inicializar UI
        UpdateGeoditesUI(NetworkedCurrency.Instance.TeamGeodites.Value);
        UpdateDarkEtherUI(NetworkedCurrency.Instance.TeamDarkEther.Value);
    }
}

// ANTES (direto):
// geodites += amount;
// DEPOIS (via servidor):
public void AddGeodites(int amount)
{
    if (NetworkedCurrency.Instance != null)
        NetworkedCurrency.Instance.AddGeoditesServerRpc(amount);
}
```

### 4.3 TowerController.cs
**Caminho:** `Assets/Codigo/Tower scripts/TowerController.cs`
**Acao:** MANTER - IA server-only

**Alteracoes:**
- IA de targeting ja roda no servidor (torre eh spawnada pelo servidor)
- Dano aplicado pelo servidor diretamente
- Animacoes sincronizadas via NetworkAnimator no prefab

### 4.4 ObjectiveHealthSystem.cs
**Caminho:** `Assets/Codigo/Managers/ObjectiveHealthSystem.cs`
**Acao:** NB (MonoBehaviour → NetworkBehaviour)

```csharp
public class ObjectiveHealthSystem : NetworkBehaviour
{
    public NetworkVariable<float> netHealth = new NetworkVariable<float>(
        1000f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsServer)
            netHealth.Value = maxHealth;

        netHealth.OnValueChanged += OnHealthChanged;
        UpdateUI(netHealth.Value);
    }

    public void TakeDamage(float damage)
    {
        if (!IsServer) return;
        netHealth.Value = Mathf.Max(0, netHealth.Value - damage);
        if (netHealth.Value <= 0)
            OnObjectiveDestroyed();
    }

    private void OnHealthChanged(float oldVal, float newVal)
    {
        UpdateUI(newVal);
    }

    private void OnObjectiveDestroyed()
    {
        if (!IsServer) return;
        GameModeManager.LoadSceneSafe("Lose");
    }
}
```

### 4.5 GridPlacement.cs
**Caminho:** `Assets/Codigo/Tower scripts/GridPlacement.cs`
**Acao:** MANTER - SEM ALTERACAO
Calculos matematicos locais. Preview/ghost local. Validacao final no BuildManager (servidor).

### 4.6 TrapLogicBase.cs / TrapDataSO.cs
**Caminho:** `Assets/Codigo/Tower scripts/Armadilhas/`
**Acao:** MANTER TrapDataSO (SO). TrapLogicBase: proteger trigger com server-check.

```csharp
// Na TrapLogicBase, trigger de dano:
protected virtual void OnTriggerEnter(Collider other)
{
    if (!NetworkManager.Singleton.IsServer) return;
    // Aplicar dano/efeito
}
```

---

## SPRINT 5: UI E GERENCIADORES

### 5.1 PlayerHUD.cs
**Caminho:** `Assets/Codigo/Char scripts/Player/PlayerHUD.cs`
**Acao:** MANTER - REFATORAR

**Alteracoes exatas:**
1. Tornar Singleton:
```csharp
public static PlayerHUD Instance { get; private set; }
void Awake() { Instance = this; }
```

2. Remover `FindGameObjectWithTag("Player")`:
```csharp
// ANTES (no Update):
var player = GameObject.FindGameObjectWithTag("Player");
vida = player.GetComponent<PlayerHealthSystem>().currentHealth;

// DEPOIS (injecao de dependencia):
private PlayerHealthSystem localPlayerHealth;

public void RegistrarJogador(PlayerHealthSystem health)
{
    localPlayerHealth = health;
    health.currentHealth.OnValueChanged += OnPlayerHealthChanged;
    UpdateHealthUI(health.currentHealth.Value);
}

private void OnPlayerHealthChanged(float oldVal, float newVal)
{
    UpdateHealthUI(newVal);
}
```

3. HUD mostra APENAS dados do jogador LOCAL

### 5.2 UIManager.cs
**Caminho:** `Assets/Codigo/Managers/UIManager.cs`
**Acao:** MANTER - REFATORAR

**Alteracoes exatas:**
1. **REMOVER** `Time.timeScale = 0` de QUALQUER lugar:
```csharp
// ANTES:
Time.timeScale = 0f; // PROIBIDO em multiplayer

// DEPOIS: Nao setar timeScale. Pause eh visual-only.
```

2. Timer de jogo: ler de NetworkVariable:
```csharp
// Se MatchManager tiver MatchTime:
// matchTimeText.text = FormatTime(MatchManager.Instance.MatchTime.Value);
```

3. Vida do objetivo: ler de NetworkVariable:
```csharp
// objectiveHealthBar.fillAmount = ObjectiveHealthSystem.Instance.netHealth.Value / maxHealth;
```

### 5.3 PauseControl.cs
**Caminho:** `Assets/Codigo/Managers/PauseControl.cs`
**Acao:** MANTER - REFATORAR

**Alteracoes exatas:**
```csharp
// Pause eh LOCAL por cliente
public static bool isPaused = false;

public void TogglePause()
{
    isPaused = !isPaused;

    // NUNCA mudar Time.timeScale
    // Apenas mostrar/esconder menu de pause
    pauseMenuCanvas.SetActive(isPaused);

    // Bloquear input do jogador quando pausado
    // (PlayerMovement e PlayerShooting verificam PauseControl.isPaused)
    Cursor.lockState = isPaused ? CursorLockMode.None : CursorLockMode.Locked;
    Cursor.visible = isPaused;
}
```

Em PlayerMovement e PlayerShooting, adicionar:
```csharp
void Update()
{
    if (!IsOwner) return;
    if (PauseControl.isPaused) return; // Pular input quando pausado
    // ... resto do input ...
}
```

### 5.4 TopDownCameraManager.cs
**Caminho:** `Assets/Codigo/Tower scripts/TopDownCameraManager.cs`
**Acao:** MANTER - ADICIONAR metodo

```csharp
// Adicionar metodo para vincular ao jogador local:
public void SetCameraTarget(Transform localPlayerTransform)
{
    // Setar target de camera para seguir o jogador local
    cameraTarget = localPlayerTransform;
}
// Chamado pelo PlayerMovement.OnNetworkSpawn() quando IsOwner
```

---

## SPRINT 6: HABILIDADES (60+ scripts)

### PADRAO UNIVERSAL PARA HABILIDADES

Cada habilidade segue EXATAMENTE este padrao. Nao desviar.

#### Para ScriptableObjects de habilidade (Ability.cs, passivaAbility.cs, HabilidadeXxx.cs):
**Acao:** SO - SEM ALTERACAO (dados estaticos)

#### Para Logic scripts (XxxLogic.cs) que ficam no prefab do jogador:
**Acao:** NB (MonoBehaviour → NetworkBehaviour)

```csharp
// TEMPLATE PARA TODA HABILIDADE:
using UnityEngine;
using Unity.Netcode;

public class [NomeDaHabilidade]Logic : NetworkBehaviour
{
    // Dados da habilidade (cooldown, dano, etc) vem do ScriptableObject
    // via CommanderAbilityController ou referencia direta

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (!IsOwner)
        {
            this.enabled = false;
            return;
        }
    }

    // Chamado pelo CommanderAbilityController quando owner usa a habilidade
    public void Activate()
    {
        if (!IsOwner) return;

        // Feedback visual imediato para owner
        PlayActivationVFXLocal();

        // Pedir ao servidor
        ActivateAbilityServerRpc();
    }

    [ServerRpc]
    private void ActivateAbilityServerRpc()
    {
        // Servidor valida cooldown
        if (Time.time < lastUseTime + cooldown) return;
        lastUseTime = Time.time;

        // Servidor executa logica de dano/buff
        ExecuteAbilityServer();

        // Avisar todos para VFX
        ActivateAbilityVisualClientRpc();
    }

    private void ExecuteAbilityServer()
    {
        // Dano em area: Physics.OverlapSphere no servidor
        var hits = Physics.OverlapSphere(transform.position, radius, enemyLayer);
        foreach (var hit in hits)
        {
            if (hit.TryGetComponent<EnemyHealthSystem>(out var enemy))
            {
                enemy.TakeDamage(damage);
            }
        }

        // OU buff no jogador:
        // GetComponent<PlayerHealthSystem>().speedMultiplier.Value *= 1.5f;
    }

    [ClientRpc]
    private void ActivateAbilityVisualClientRpc()
    {
        if (IsOwner) return; // Owner ja fez no Activate()
        PlayActivationVFXLocal();
    }

    private void PlayActivationVFXLocal()
    {
        // Particulas, sons, animacoes
        // Tudo local - cada cliente renderiza
    }
}
```

### Habilidades por Personagem - Raposa (Samurai/Fox)

| Script | Acao | Notas |
|--------|------|-------|
| CuttingBladeAbility.cs | SO | Dados estaticos |
| CuttingBladeLogic.cs | NB | Dano em area → ServerRpc + OverlapSphere servidor |
| NineTailsDanceAbility.cs | SO | Dados estaticos |
| NineTailsDanceLogic.cs | NB | Multiplos golpes → Loop server-side com ClientRpc por golpe |
| PeaceOfMindAbility.cs | SO | Dados estaticos |
| PeaceOfMindLogic.cs | NB | Cura/buff → Server altera NetworkVariable de vida |
| NineTailsLegacyPassive.cs | NB ou MANTER | Se altera stats: server-only no OnNetworkSpawn |
| ArmorAuraBehavior.cs | NB | Aura → Server aplica buff em jogadores proximos |
| HealingAuraBehavior.cs | NB | Cura em area → Server-only com VFX ClientRpc |
| LegacyAuraBehavior.cs | NB | Similar a ArmorAura |
| RebirthBehavior.cs | NB | Revive → Server reseta vida + RespawnClientRpc |
| SpiritualBarrierBehavior.cs | NB | Escudo → NetworkVariable de escudo extra |
| ArmorShredBehavior.cs | NB | Debuff → Server aplica em inimigo |
| BonusDamageToShreddedBehavior.cs | NB | Multiplicador → Server verifica debuff |
| DoubleAttackBehavior.cs | NB | Modifica ataque → Owner check |
| MultiShotBehavior.cs | NB | Multiplos projeteis → Owner spawna locais |
| AssaultBehavior.cs | NB | Dash de ataque → Owner-auth via ClientNetworkTransform |
| FuryStackBehavior.cs | NB | Stack de buff → NetworkVariable<int> stacks |

### Habilidades por Personagem - Coruja (Owl/Archer)

| Script | Acao | Notas |
|--------|------|-------|
| HabilidadeCacadoraNoturna.cs | SO | Dados estaticos |
| CacadoraNoturnaLogic.cs | NB | Ultimate → Server controla duracao, ClientRpc VFX |
| HabilidadePerseguindoPresas.cs | SO | Dados estaticos |
| HabilidadeVooGracioso.cs | SO | Dados estaticos |
| VooGraciosoLogic.cs | NB | Voo → Owner-auth movement, ServerRpc para dano ao pousar |
| PassivaComandanteCoruja.cs | NB | Passiva → Server aplica buff no OnNetworkSpawn |
| TrilhaFocoImplacavel.cs | NB | Trail buff → Server-only |
| TrilhaSobrevivenciaCacadora.cs | NB | Trail buff → Server-only |
| TrilhaVooSilencioso.cs | NB | Trail buff → Server-only |
| BleedingBehavior.cs | NB | DoT → Server aplica tick de dano |
| OwlEyeBehavior.cs | NB | Visao → Owner-only (local) |
| PiercingBehavior.cs | NB | Penetracao → Owner detecta, ServerRpc por hit |
| DarkVisionBehavior.cs | NB | Visao → Owner-only (local) |
| FlyingEnemyTargetingBehavior.cs | NB | Targeting → Owner detecta, server valida |
| PreyMarkBehavior.cs | NB | Marca → Server aplica debuff |
| ProjectileSpeedBehavior.cs | NB | Buff local → Owner-only |
| ArrowRainBehavior.cs | NB | Area → ServerRpc posicao, Server dano, ClientRpc chuva visual |
| FuryStackyBehavior.cs | NB | Stacks → NetworkVariable |
| ReloadSpeedBehavior.cs | NB | Buff local → Owner-only |

### Habilidades por Personagem - Dragao (Dragon)

| Script | Acao | Notas |
|--------|------|-------|
| HabilidadeAquiNao.cs | SO | Dados estaticos |
| AquiNaoLogic.cs | NB | Knockback em area → Server OverlapSphere + Knockback |
| HabilidadePosturaBaluarte.cs | SO | Dados estaticos |
| PosturaBaluarteLogic.cs | NB | Stance → NetworkVariable<bool> estaEmPostura |
| HabilidadeTemorSismico.cs | SO | Dados estaticos |
| TemorSismicoLogic.cs | NB | Stun em area → Server aplica stun |
| PassiveEscamasAdamantium.cs | NB | Passiva defesa → Server aplica damageResistance |

### Habilidades por Personagem - Polvo (Octopus)

| Script | Acao | Notas |
|--------|------|-------|
| HabilidadeBombaSpray.cs | SO | Dados estaticos |
| BombaSprayProjectile.cs | NB | Projetil especial → Se lento: NetworkObject. Se rapido: visual local |
| HabilidadeMergulhoTinta.cs | SO | Dados estaticos |
| MergulhoTintaLogic.cs | NB | Dash + AoE → Owner-auth move, ServerRpc dano ao chegar |
| HabilidadeObraPrima.cs | SO | Dados estaticos |
| ObraPrimaLogic.cs | NB | Ultimate → Server controla, ClientRpc VFX massivo |
| NuvemDeTintaLogic.cs | NB | Area deny → Server spawna zona, dano tick server-only |
| PassivaTracoUrbano.cs | NB | Passiva → Server aplica buff |
| TracoUrbanoLogic.cs | NB | Logica da passiva → Server-only |
| ProjetilColorido.cs | MANTER | Visual local como ProjectileVisual |
| PaintAbilitySystem.cs | NB | Sistema de pintura → Server controla tipo, ClientRpc visuais |

---

## SPRINT 7: UTILITARIOS

| Script | Caminho | Acao | Alteracao |
|--------|---------|------|-----------|
| VerificadorQueda.cs | Char scripts/Player/ | NB | `if (!IsOwner) return;` no Update. Teleporte local, NGO sincroniza via ClientNetworkTransform |
| DamagePopup.cs | Codigo/ | MANTER | Spawn local por cliente quando receber ClientRpc de hit |
| CursorOn.cs | Codigo/ | MANTER | Se no Canvas: sem mudanca. Se no prefab jogador: `if (!IsOwner) return;` |
| WinSound.cs | Codigo/ | NB | Server sorteia indice → `PlayVictoryMusicClientRpc(index)` |
| LoseSound.cs | Codigo/ | NB | Server sorteia indice → `PlayDefeatMusicClientRpc(index)` |
| VolumeManager.cs | Codigo/ | MANTER | 100% local (PlayerPrefs) |
| MusicManager.cs | Codigo/ | MANTER | Singleton local. Outros chamam via ClientRpc |
| GerenciadorDeSomGlobal.cs | Managers/ | MANTER | 100% local |
| PreyMarkLogic.cs | Char scripts/Player/ | NB | `if (!IsServer) return;` no StartEffect. Debuffs server-only |
| SpawnPath.cs | Managers/ | MANTER | Dados estaticos de rota |
| MagicStar.cs | Codigo/ | MANTER | Visual local |
| WindSound.cs | Codigo/ | MANTER | Ambiente local |
| CommanderController.cs | Char scripts/Player/ | NB | Input `if (!IsOwner) return;` |
| PlayerAttack.cs | Char scripts/Player/ | NB | Input `if (!IsOwner) return;` + Dano via ServerRpc |
| DebugDamage.cs | Char scripts/Player/ | MANTER | Debug apenas |
| BotaoHabilidade.cs | Managers/ | MANTER | UI local |
| BuildButtonUI.cs | Managers/ | MANTER | UI local |
| BuildTooltipTrigger.cs | Managers/ | MANTER | UI local |
| Fals.cs | Managers/ | MANTER | Verificar funcao |
| RastroUpgrade.cs | Managers/ | NB se altera stats | Server-only para upgrades |
| Rastros.cs | Managers/ | NB se altera stats | Server valida unlock |
| StatIconDatabase.cs | Managers/ | SO/MANTER | Dados estaticos |
| UpgradeTooltip.cs | Managers/ | MANTER | UI local |
| AtributoFrasco.cs | FrascosPoder/ | SO | Dados estaticos |
| InventarioFrascos.cs | FrascosPoder/ | MANTER ou NB | Se altera stats: NetworkVariable |
| GameDataManager.cs | Saves/ | MANTER | Dados locais de save |
| SelecaoManager.cs | Saves/ | MANTER | Selecao local |
| SlotEquipeUI.cs | Saves/ | MANTER | UI local |
| TutorialData.cs | Tutorial/ | SO | Dados estaticos |
| TutorialManager.cs | Tutorial/ | MANTER | Local por cliente |
| TutorialPopupUI.cs | Tutorial/ | MANTER | UI local |
| TutorialReviewUI.cs | Tutorial/ | MANTER | UI local |
| TowerAbilitySystem.cs | Tower scripts/ | NB | Habilidade de torre → Server-only |
| TowerBehavior.cs | Tower scripts/ | MANTER | Config da torre |
| TowerSelectionCircle.cs | Tower scripts/ | MANTER | Visual local |
| TowerSelectionManager.cs | Tower scripts/ | MANTER | Input local |
| TurretController.cs | Tower scripts/ | MANTER | Server-only (parte do prefab torre) |
| Upgrade.cs | Tower scripts/ | SO | Dados estaticos |
| UpgradePanelUI.cs | Tower scripts/ | MANTER | UI local, upgrades via ServerRpc |
| UpgradePath.cs | Tower scripts/ | SO | Dados estaticos |

---

## SPRINT 8: INTEGRACAO FINAL

### Checklist de Integracao

1. **Fluxo Singleplayer:**
   - Menu → EscolherPersonagem → CenaMapaTeste (como Host local)
   - Verificar: tudo funciona identico ao original

2. **Fluxo Multiplayer:**
   - Menu → LobbyScene (EOS Auth + Lobby) → CenaMapaTeste (Host/Client)
   - Verificar: 2-4 jogadores jogam juntos

3. **Conexao com infraestrutura existente:**
   - `NetworkBootstrap.cs` → garante NetworkManager persiste entre cenas
   - `SessionManager.cs` → gerencia sessoes e reconexao
   - `PlayerRegistry.cs` → lista todos os jogadores conectados
   - `NetworkedCurrency.cs` → economia compartilhada
   - `NetworkedBuilding.cs` → torres sincronizadas
   - `NetworkedEnemy.cs` → wrapper de inimigos
   - `MatchManager.cs` → estado da partida
   - `GameServerManager.cs` → gerenciamento do servidor de jogo

4. **Prefabs - Componentes necessarios:**
   - Prefab Jogador: `NetworkObject` (raiz) + `ClientNetworkTransform` + `ClientNetworkAnimator`
   - Prefab Inimigo: `NetworkObject` (raiz) + `NetworkTransform` + `NetworkAnimator`
   - Prefab Torre: `NetworkObject` (raiz) + `NetworkedBuilding`
   - Prefab Objetivo: `NetworkObject` (raiz)

5. **Cenas - NetworkObject em cena:**
   - Todo GameObject com NetworkBehaviour PRECISA ter NetworkObject na raiz
   - HordeManager, BuildManager, ObjectiveHealthSystem devem ser NetworkObjects in-scene

---

## SCRIPTS MULTIPLAYER EXISTENTES (NAO ALTERAR)

Estes scripts ja estao prontos na pasta `Assets/Codigo/Multiplayer/`:

| Script | Funcao | Status |
|--------|--------|--------|
| EOSAuthenticator.cs | Auth via EOS Device ID | PRONTO |
| EOSConfig.cs | Configuracao EOS | PRONTO |
| EOSManager.cs | Wrapper EOS SDK | PRONTO |
| SessionManager.cs | Gerenciamento de sessao | PRONTO |
| NetworkBootstrap.cs | Bootstrap do NetworkManager | PRONTO |
| HostManager.cs | Gerenciamento de host | PRONTO |
| MppmHelper.cs | Helper para MPPM testing | PRONTO |
| WindowsPlatformSpecifics.cs | Plataforma Windows | PRONTO |
| GameServerManager.cs | Servidor de jogo | PRONTO |
| MatchManager.cs | Estado da partida | PRONTO |
| PlayerRegistry.cs | Registro de jogadores | PRONTO |
| LobbyData.cs | Dados do lobby | PRONTO |
| LobbyItemUI.cs | UI de item do lobby | PRONTO |
| LobbyManager.cs | Gerenciamento de lobby | PRONTO |
| LobbyUI.cs | UI do lobby | PRONTO |
| NetworkedBuilding.cs | Sync de torres | PRONTO |
| NetworkedCurrency.cs | Sync de economia | PRONTO |
| NetworkedEnemy.cs | Sync de inimigos | PRONTO |
| NetworkedPlayerController.cs | Sync de jogador | REVISAR (duplicata de vida) |
| PlayerNetworkSetup.cs | Setup de rede do jogador | PRONTO |
| NetworkedHorde.cs | Sync de waves | DELETAR (unificar com HordeManager) |

### NetworkedPlayerController.cs - REVISAO NECESSARIA
**Acao:** Remover NetworkVariable de vida duplicada. Vida fica APENAS no PlayerHealthSystem.
Se NetworkedPlayerController tem outros dados uteis (nome, team, etc), manter apenas esses.

---

## REGRAS FINAIS

### Antes de entregar QUALQUER script migrado:

1. **Compila?** Verificar mentalmente se todos os tipos, namespaces e referencias estao corretos
2. **IsOwner no input?** Todo ProcessInput/Update com input deve ter `if (!IsOwner) return;`
3. **IsServer no dano/estado?** Todo TakeDamage/alteracao de estado deve ter `if (!IsServer) return;`
4. **OnNetworkSpawn em vez de Start?** Toda inicializacao que usa IsOwner/IsServer esta em OnNetworkSpawn?
5. **NetworkVariables antes de Spawn?** NENHUMA NetworkVariable eh alterada antes de OnNetworkSpawn
6. **FindObjectOfType removido?** NENHUM FindObjectOfType/FindGameObjectWithTag restante?
7. **SceneManager.LoadScene removido?** Usar GameModeManager.LoadSceneSafe() em vez disso?
8. **Time.timeScale = 0 removido?** NENHUM timeScale = 0 em codigo multiplayer?
9. **Destroy() em NetworkObject?** Usar Despawn() em vez disso?
10. **Singleplayer funciona?** O script funciona como Host local sem clientes remotos?

### Formato de entrega:
Para cada script migrado, entregar:
1. O codigo completo do script alterado
2. Lista de NetworkVariables adicionadas (nome, tipo, permissao)
3. Lista de RPCs adicionados (nome, direcao, parametros)
4. Lista de componentes necessarios no prefab (NetworkObject, ClientNetworkTransform, etc)
5. Dependencias (quais outros scripts precisam estar migrados antes)
