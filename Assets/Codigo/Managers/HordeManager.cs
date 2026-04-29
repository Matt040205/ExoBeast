using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using ExoBeasts.Multiplayer.GameServer;

/// <summary>
/// ── HordeManager ───────────────────────────────────────
/// Gerencia ondas de inimigos. Suporta modo local (singleplayer offline)
/// e modo rede (NGO Host/Server).
///
///  ▸ Modo Local: usa Start() para iniciar, variáveis locais para estado
///  ▸ Modo Rede: usa OnNetworkSpawn(), NetworkVariables, ServerRpcs
///  ▸ Escala de dificuldade via nível da horda (EnemyDataSO.GetHealth/Damage/etc)
/// ─────────────────────────────────────────────────────
/// </summary>
public class HordeManager : NetworkBehaviour
{
    public static HordeManager Instance { get; private set; }
    public bool IsLocalMode { get; private set; }

    [Header("Configuracoes da Horda")]
    public int victoryHorde = 5;
    public float timeBetweenWaves = 10f;
    public float spawnInterval = 1f;
    public int enemiesPerInterval = 1;

    [Header("Inimigos e Dificuldade")]
    public EnemyDataSO[] enemyTypes;
    public int enemiesPerHordeMin = 5;
    public int enemiesPerHordeMax = 10;

    [Header("Caminhos de Spawn")]
    public List<SpawnPath> spawnPaths;
    private int lastPathIndex = -1;

    [Header("VFX de Spawn")]
    [Tooltip("Prefab do relâmpago que aparece ANTES do inimigo nascer.")]
    public GameObject lightningVfxPrefab;
    [Tooltip("Tempo (em segundos) entre o relâmpago e o inimigo aparecer.")]
    public float lightningDelay = 0.5f;
    [Tooltip("Altura acima do ponto de spawn onde o relâmpago aparece (o raio cai do céu).")]
    public float lightningHeightOffset = 15f;
    [Tooltip("Escala do efeito do relâmpago (1 = tamanho original do prefab).")]
    public float lightningScale = 3f;

    [Header("Interface (UI)")]
    public TextMeshProUGUI hordeText;
    public TextMeshProUGUI hordeTextBuild;

    [Header("Network Variables")]
    public NetworkVariable<int> currentHorde = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> enemiesRemaining = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> isWaveActive = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    
    // Variável para sincronizar o tempo global da partida (timer HUD)
    public NetworkVariable<float> currentMatchTime = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // ── Fallback local (quando NGO não está ativo) ──
    private int localCurrentHorde = 0;
    private int localEnemiesRemaining = 0;
    private bool localIsWaveActive = false;

    private int enemiesToSpawnTotal;
    private int enemiesSpawnedCount = 0;
    private Coroutine spawnCoroutine;

    // ── Propriedades de acesso transparente (local / rede) ──
    private int CurrentHorde
    {
        get => IsLocalMode ? localCurrentHorde : currentHorde.Value;
        set
        {
            if (IsLocalMode) { localCurrentHorde = value; UpdateHordeUI(value); }
            else currentHorde.Value = value;
        }
    }
    private int EnemiesRemaining
    {
        get => IsLocalMode ? localEnemiesRemaining : enemiesRemaining.Value;
        set { if (IsLocalMode) localEnemiesRemaining = value; else enemiesRemaining.Value = value; }
    }
    private bool WaveActive
    {
        get => IsLocalMode ? localIsWaveActive : isWaveActive.Value;
        set { if (IsLocalMode) localIsWaveActive = value; else isWaveActive.Value = value; }
    }

    /// <summary>Tem autoridade para controlar a horda (server ou local)</summary>
    private bool HasAuthority => IsLocalMode || IsServer;

    // ════════════════════════════════════════════════════
    //  CICLO DE VIDA
    // ════════════════════════════════════════════════════

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Update()
    {
        // Incrementa o tempo da partida apenas no servidor (sincroniza com clientes via netcode)
        if (IsServer)
        {
            currentMatchTime.Value += Time.deltaTime;
        }
    }

    private bool hordeStarted = false;

    void Start()
    {
        // Inicia detecção robusta com 1 frame de atraso (para NGO finalizar inicialização)
        StartCoroutine(InitializeHordeSystem());
    }

    private IEnumerator InitializeHordeSystem()
    {
        // Espera 1 frame para o NGO terminar sua inicialização
        yield return null;

        // Se OnNetworkSpawn já disparou e iniciou a horda, não duplicar
        if (hordeStarted)
        {
            Debug.Log("[HordeManager] InitializeHordeSystem: OnNetworkSpawn já iniciou. Abortando.");
            yield break;
        }

        bool ngoExiste = NetworkManager.Singleton != null;
        bool ngoServer = ngoExiste && NetworkManager.Singleton.IsServer;
        bool ngoClient = ngoExiste && NetworkManager.Singleton.IsClient;

        Debug.Log($"[HordeManager] InitializeHordeSystem - NGO existe={ngoExiste}, IsServer={ngoServer}, IsClient={ngoClient}");

        if (ngoServer)
        {
            // Modo rede como Host/Server — OnNetworkSpawn cuida disso
            // Mas se por algum motivo OnNetworkSpawn não tiver disparado, cobrimos aqui
            IsLocalMode = false;
            Debug.Log("[HordeManager] Modo REDE (Host/Server) detectado.");

            // Espera jogadores com TIMEOUT de 10 segundos
            float timeout = 10f;
            float timer = 0f;
            while ((PlayerRegistry.Instance == null || PlayerRegistry.Instance.GetPlayerCount() == 0) && timer < timeout)
            {
                // Checa a cada segundo se OnNetworkSpawn já assumiu
                if (hordeStarted) { Debug.Log("[HordeManager] OnNetworkSpawn assumiu durante a espera."); yield break; }
                timer += 1f;
                yield return new WaitForSeconds(1f);
            }

            // Última checagem antes de iniciar
            if (hordeStarted) yield break;

            if (timer >= timeout)
                Debug.LogWarning("[HordeManager] Timeout esperando PlayerRegistry! Iniciando mesmo assim.");

            yield return new WaitForSeconds(3f);
            if (hordeStarted) yield break;

            hordeStarted = true;
            StartNextHorde();
        }
        else if (!ngoClient)
        {
            // Modo local puro (sem NGO ativo)
            IsLocalMode = true;
            Debug.Log("[HordeManager] Modo LOCAL detectado. Iniciando em 3s...");
            UpdateHordeUI(0);

            yield return new WaitForSeconds(3f);
            if (hordeStarted) yield break;

            hordeStarted = true;
            StartNextHorde();
        }
        else
        {
            // Somos cliente puro - servidor controla as hordas
            IsLocalMode = false;
            Debug.Log("[HordeManager] Modo REDE (Cliente) - servidor controla hordas.");
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        currentHorde.OnValueChanged += OnCurrentHordeChanged;
        UpdateHordeUI(currentHorde.Value);

        // Se a InitializeHordeSystem ainda não detectou o servidor, e somos servidor,
        // garantir que a horda vai começar
        if (IsServer && !hordeStarted)
        {
            Debug.Log("[HordeManager] OnNetworkSpawn: IsServer=true, forçando início via rede.");
            IsLocalMode = false;
            hordeStarted = true;
            StartCoroutine(WaitForPlayersAndBeginWithTimeout());
        }
    }

    private void OnCurrentHordeChanged(int oldVal, int newVal) => UpdateHordeUI(newVal);

    public override void OnNetworkDespawn()
    {
        currentHorde.OnValueChanged -= OnCurrentHordeChanged;
        base.OnNetworkDespawn();
    }

    // ════════════════════════════════════════════════════
    //  INICIALIZAÇÃO
    // ════════════════════════════════════════════════════

    private IEnumerator WaitForPlayersAndBeginWithTimeout()
    {
        // Aguarda jogadores com timeout de 10 segundos
        float timeout = 10f;
        float timer = 0f;
        while ((PlayerRegistry.Instance == null || PlayerRegistry.Instance.GetPlayerCount() == 0) && timer < timeout)
        {
            if (!IsServer) yield break;
            timer += 1f;
            yield return new WaitForSeconds(1f);
        }

        if (timer >= timeout)
            Debug.LogWarning("[HordeManager] OnNetworkSpawn: Timeout esperando jogadores!");

        yield return new WaitForSeconds(3f);
        StartNextHorde();
    }

    // ════════════════════════════════════════════════════
    //  UI
    // ════════════════════════════════════════════════════

    private void UpdateHordeUI(int current)
    {
        if (hordeTextBuild != null) hordeTextBuild.text = $"{current}/{victoryHorde}";
        if (hordeText != null) hordeText.text = $"{current}/{victoryHorde}";
    }

    // ════════════════════════════════════════════════════
    //  MORTE DE INIMIGO
    // ════════════════════════════════════════════════════

    /// <summary>
    /// Chamado em modo LOCAL (sem RPC). Decrementa inimigos e verifica fim de onda.
    /// </summary>
    public void OnEnemyKilled()
    {
        localEnemiesRemaining = Mathf.Max(0, localEnemiesRemaining - 1);
        Debug.Log($"[HordeManager] Inimigo morto (local). Restam: {localEnemiesRemaining}");

        if (localEnemiesRemaining <= 0 && localIsWaveActive && enemiesSpawnedCount >= enemiesToSpawnTotal)
        {
            OnWaveCompleted();
        }
    }

    /// <summary>
    /// Chamado em modo REDE via ServerRpc.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void OnEnemyKilledServerRpc()
    {
        if (!IsServer) return;

        enemiesRemaining.Value = Mathf.Max(0, enemiesRemaining.Value - 1);

        if (enemiesRemaining.Value <= 0 && isWaveActive.Value && enemiesSpawnedCount >= enemiesToSpawnTotal)
        {
            OnWaveCompleted();
        }
    }

    // ════════════════════════════════════════════════════
    //  FLUXO DE ONDA
    // ════════════════════════════════════════════════════

    private void OnWaveCompleted()
    {
        if (!HasAuthority) return;

        WaveActive = false;
        Debug.Log($"[HordeManager] Horda {CurrentHorde} completada!");

        if (CurrentHorde >= victoryHorde)
        {
            // Vitória!
            if (IsLocalMode)
                SceneManager.LoadScene("Win");
            else
                NetworkManager.Singleton.SceneManager.LoadScene("Win", LoadSceneMode.Single);
        }
        else
        {
            StartCoroutine(WaitAndStartNextWave());
        }
    }

    private IEnumerator WaitAndStartNextWave()
    {
        Debug.Log($"[HordeManager] Próxima horda em {timeBetweenWaves}s...");
        yield return new WaitForSeconds(timeBetweenWaves);
        if (HasAuthority) StartNextHorde();
    }

    private void StartNextHorde()
    {
        if (!HasAuthority) return;

        CurrentHorde++;
        WaveActive = true;

        enemiesToSpawnTotal = Random.Range(enemiesPerHordeMin, enemiesPerHordeMax + 1);
        enemiesSpawnedCount = 0;
        EnemiesRemaining = enemiesToSpawnTotal;

        Debug.Log($"[HordeManager] Iniciando Horda {CurrentHorde} com {enemiesToSpawnTotal} inimigos! " +
                  $"Tipos disponíveis: {(enemyTypes != null ? enemyTypes.Length : 0)}" +
                  (enemyTypes != null ? $" [{string.Join(", ", System.Array.ConvertAll(enemyTypes, e => e != null ? e.name : "NULL"))}]" : ""));

        if (spawnCoroutine != null) StopCoroutine(spawnCoroutine);
        spawnCoroutine = StartCoroutine(SpawnEnemiesOverTime());
    }

    // ════════════════════════════════════════════════════
    //  SPAWN DE INIMIGOS
    // ════════════════════════════════════════════════════

    private IEnumerator SpawnEnemiesOverTime()
    {
        while (enemiesSpawnedCount < enemiesToSpawnTotal)
        {
            int batchSize = Mathf.Min(enemiesPerInterval, enemiesToSpawnTotal - enemiesSpawnedCount);

            for (int i = 0; i < batchSize; i++)
            {
                // Calcula posição de spawn para o relâmpago antes de spawnar o inimigo
                if (lightningVfxPrefab != null && spawnPaths != null && spawnPaths.Count > 0)
                {
                    int previewPath = GetRandomPathIndex();
                    SpawnPath path = spawnPaths[previewPath];
                    if (path.spawnPoint != null)
                    {
                        Vector3 spawnPos = path.spawnPoint.position;

                        // Mostra o relâmpago em todos os clientes
                        if (!IsLocalMode)
                            SpawnLightningClientRpc(spawnPos);
                        else
                            SpawnLightningLocal(spawnPos);

                        yield return new WaitForSeconds(lightningDelay);

                        // Spawna o inimigo exatamente onde o relâmpago caiu
                        SpawnSingleEnemyAt(path);
                    }
                }
                else
                {
                    SpawnSingleEnemy();
                }
            }

            enemiesSpawnedCount += batchSize;
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    [ClientRpc]
    private void SpawnLightningClientRpc(Vector3 position)
    {
        SpawnLightningLocal(position);
    }

    private void SpawnLightningLocal(Vector3 position)
    {
        if (lightningVfxPrefab != null)
        {
            // Posiciona o raio acima do ponto de spawn para ele "cair do céu"
            Vector3 spawnPos = position + Vector3.up * lightningHeightOffset;
            GameObject vfx = GlobalVFXPool.GetVFX(lightningVfxPrefab, spawnPos, Quaternion.identity, 3f);
            if (vfx != null)
            {
                // Estica o Y para o raio alcançar o chão, mantendo X e Z com a escala normal
                float yStretch = lightningScale * (1f + lightningHeightOffset * 0.3f) + 4f;
                vfx.transform.localScale = new Vector3(lightningScale, yStretch, lightningScale);
            }
        }
    }

    /// <summary>
    /// Spawn sem relâmpago (fallback quando o VFX não está configurado).
    /// </summary>
    private void SpawnSingleEnemy()
    {
        if (spawnPaths == null || spawnPaths.Count == 0 || enemyTypes == null || enemyTypes.Length == 0) return;

        int pathIndex = GetRandomPathIndex();
        SpawnPath selectedPath = spawnPaths[pathIndex];
        SpawnSingleEnemyAt(selectedPath);
    }

    /// <summary>
    /// Spawn de um inimigo em um caminho específico (usado pelo sistema de relâmpago).
    /// </summary>
    private void SpawnSingleEnemyAt(SpawnPath selectedPath)
    {
        if (enemyTypes == null || enemyTypes.Length == 0) return;
        if (selectedPath.spawnPoint == null) return;

        int pathIndex = spawnPaths.IndexOf(selectedPath);

        int enemyTypeIndex = Random.Range(0, enemyTypes.Length);
        EnemyDataSO enemyData = enemyTypes[enemyTypeIndex];

        if (enemyData == null || enemyData.enemyPrefab == null)
        {
            // Prefab null em build significa referência quebrada de prefab variant no ScriptableObject.
            // Decrementar para não travar a onda (esse slot nunca terá inimigo para matar).
            Debug.LogError($"[HordeManager] enemyTypes[{enemyTypeIndex}] é null ou sem prefab em runtime! " +
                           "Re-arraste o prefab no Inspector do ScriptableObject e no DefaultNetworkPrefabs.");
            ResolveFailedSpawnSlot();
            return;
        }

        GameObject newEnemy = null;

        if (EnemyPoolManager.Instance != null)
        {
            newEnemy = EnemyPoolManager.Instance.GetPooledEnemy(enemyData.enemyPrefab, selectedPath.spawnPoint.position, selectedPath.spawnPoint.rotation);
        }
        else if (IsLocalMode)
        {
            // Fallback local sem pool
            newEnemy = Instantiate(enemyData.enemyPrefab, selectedPath.spawnPoint.position, selectedPath.spawnPoint.rotation);
        }
        else if (IsServer)
        {
            // Fallback rede sem pool — instancia e spawna via NGO diretamente.
            newEnemy = Instantiate(enemyData.enemyPrefab, selectedPath.spawnPoint.position, selectedPath.spawnPoint.rotation);
            if (newEnemy.TryGetComponent<NetworkObject>(out var netObj))
                netObj.Spawn(true);
        }

        if (newEnemy == null)
        {
            Debug.LogError($"[HordeManager] Spawn retornou null para tipo '{enemyData.name}' (índice {enemyTypeIndex}). " +
                           "Verifique o DefaultNetworkPrefabs e o registro do EnemyPoolManager.");
            ResolveFailedSpawnSlot();
            return;
        }

        EnemyController enemyController = newEnemy.GetComponent<EnemyController>();
        if (enemyController != null)
        {
            Transform target = GetRandomPlayerTarget();
            enemyController.InitializeEnemy(target, selectedPath.patrolPoints, enemyData, CurrentHorde, pathIndex);
        }
    }

    private void ResolveFailedSpawnSlot()
    {
        EnemiesRemaining = Mathf.Max(0, EnemiesRemaining - 1);
        if (EnemiesRemaining <= 0 && WaveActive && enemiesSpawnedCount >= enemiesToSpawnTotal)
            OnWaveCompleted();
    }

    // ════════════════════════════════════════════════════
    //  ALVO DO JOGADOR
    // ════════════════════════════════════════════════════

    private Transform GetRandomPlayerTarget()
    {
        // Tenta via PlayerRegistry (modo rede)
        if (PlayerRegistry.Instance != null && PlayerRegistry.Instance.GetPlayerCount() > 0)
        {
            var players = PlayerRegistry.Instance.GetAllPlayers();
            var clientIds = new List<ulong>(players.Keys);
            ulong randomId = clientIds[Random.Range(0, clientIds.Count)];
            return players[randomId].transform;
        }

        // Fallback local: busca por tag "Player"
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null) return player.transform;

        return null;
    }

    // ════════════════════════════════════════════════════
    //  UTILIDADES
    // ════════════════════════════════════════════════════

    private int GetRandomPathIndex()
    {
        if (spawnPaths.Count <= 1) return 0;
        int newIndex;
        do
        {
            newIndex = Random.Range(0, spawnPaths.Count);
        } while (newIndex == lastPathIndex);
        lastPathIndex = newIndex;
        return newIndex;
    }
}
