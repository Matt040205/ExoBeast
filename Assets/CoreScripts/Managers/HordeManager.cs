using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
[System.Serializable]
public struct EnemySpawnConfig
{
    [Tooltip("Tipo de inimigo a ser instanciado.")]
    public EnemyDataSO enemyData;
    [Tooltip("Quantidade de inimigos deste tipo a serem gerados nesta entrada.")]
    public int spawnCount; //
    [Tooltip("Índice do caminho no Spawn Paths. Se menor que 0 ou inválido, escolhe um caminho aleatório.")]
    public int pathIndex;
    [Tooltip("Intervalo de tempo (segundos) para esperar após o nascimento deste inimigo antes de instanciar o próximo.")]
    public float spawnDelay;
}

[System.Serializable]
public struct WaveConfig
{
    [Tooltip("Tempo de preparação antes do início desta rodada (em segundos).")]
    public float prepTime;
    [Tooltip("Sequência ordenada de spawn dos inimigos nesta rodada.")]
    public List<EnemySpawnConfig> spawnSequence;
}

public class HordeManager : NetworkBehaviour
{
    public static HordeManager Instance { get; private set; }
    public bool IsLocalMode { get; private set; }

    [Header("Configuracoes da Horda")]
    public int victoryHorde = 5;
    public float timeBetweenWaves = 10f;
    public float spawnInterval = 1f;
    public int enemiesPerInterval = 1;

    [Header("Configurações de Ondas Customizadas (Opcional)")]
    [Tooltip("Lista de ondas personalizadas. Se vazia, o jogo gerará as ondas de forma randômica.")]
    public List<WaveConfig> customWaves;

    [Header("Inimigos e Dificuldade")]
    public EnemyDataSO[] enemyTypes;
    public int enemiesPerHordeMin = 5;
    public int enemiesPerHordeMax = 10;

    [Header("Caminhos de Spawn")]
    public List<SpawnPath> spawnPaths;
    private int lastPathIndex = -1;

    [Header("Interface (UI)")]
    public TextMeshProUGUI hordeText;
    public TextMeshProUGUI hordeTextBuild;

    [Header("Network Variables")]
    public NetworkVariable<int> currentHorde = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> enemiesRemaining = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> isWaveActive = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Timer da partida agora vive no MatchManager.MatchTime (canonico segundo Estado_Atual_Multiplayer.md).
    // currentMatchTime aqui foi removido para eliminar duplicacao de NetworkVariable + escrita por frame.

    // ── Fallback local (quando NGO não está ativo) ──
    private int localCurrentHorde = 0;
    private int localEnemiesRemaining = 0;
    private bool localIsWaveActive = false;

    private int enemiesToSpawnTotal;
    private int enemiesSpawnedCount = 0;
    private Coroutine spawnCoroutine;

    [Header("Fase de Preparacao")]
    public float prepTimeFirstWave = 60f;
    public float prepTimeBetweenWaves = 30f;
    private bool isPreparing = false;

    // Lista pre-sorteada de inimigos para a proxima onda (inimigo + indice do caminho + atraso)
    private List<(EnemyDataSO enemy, int pathIndex, float spawnDelay)> preGeneratedWaveList = new List<(EnemyDataSO, int, float)>();
    private readonly List<Transform> playerTargetCandidates = new List<Transform>(4);
    private static readonly List<EnemyController> _activeEnemiesRegistry = new List<EnemyController>(64);

    // OPTIMIZATION (Sprint 3 / Item E3p2 - 2026-05-08): registry de inimigos
    // ativos para targeting de torres sem Physics.OverlapSphereNonAlloc no servidor.
    public static IReadOnlyList<EnemyController> GetActiveEnemies() => _activeEnemiesRegistry;

    public static void RegisterEnemy(EnemyController enemy)
    {
        if (enemy == null || _activeEnemiesRegistry.Contains(enemy))
            return;

        _activeEnemiesRegistry.Add(enemy);
    }

    public static void UnregisterEnemy(EnemyController enemy)
    {
        if (enemy == null)
            return;

        _activeEnemiesRegistry.Remove(enemy);
    }

    private static void ClearActiveEnemiesRegistry()
    {
        _activeEnemiesRegistry.Clear();
    }

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
        if (Instance == null)
        {
            Instance = this;
            ClearActiveEnemiesRegistry();
        }
        else Destroy(gameObject);
    }

    private bool hordeStarted = false;

    void Start()
    {
        // Inicia detecção robusta com 1 frame de atraso (para NGO finalizar inicialização)
        StartCoroutine(InitializeHordeSystem());
    }

    private void Update()
    {
        if (isPreparing && Input.GetKeyDown(KeyCode.P))
        {
            if (HasAuthority)
                isPreparing = false;
            else if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
                SkipPreparationServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SkipPreparationServerRpc()
    {
        if (isPreparing)
            isPreparing = false;
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
            while (GetValidPlayerCount() == 0 && timer < timeout)
            {
                // Checa a cada segundo se OnNetworkSpawn já assumiu
                if (hordeStarted) { Debug.Log("[HordeManager] OnNetworkSpawn assumiu durante a espera."); yield break; }
                timer += 1f;
                yield return new WaitForSeconds(1f);
            }

            // Última checagem antes de iniciar
            if (hordeStarted) yield break;

            if (timer >= timeout)
                Debug.LogWarning("[HordeManager] Timeout esperando jogadores validos! Iniciando mesmo assim.");

            yield return new WaitForSeconds(3f);
            if (hordeStarted) yield break;

            hordeStarted = true;
            yield return StartCoroutine(PreparationPhaseFlow(true));
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
            yield return StartCoroutine(PreparationPhaseFlow(true));
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
        ClearActiveEnemiesRegistry();
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
        while (GetValidPlayerCount() == 0 && timer < timeout)
        {
            if (!IsServer) yield break;
            timer += 1f;
            yield return new WaitForSeconds(1f);
        }

        if (timer >= timeout)
            Debug.LogWarning("[HordeManager] OnNetworkSpawn: Timeout esperando jogadores validos!");

        yield return new WaitForSeconds(3f);
        yield return StartCoroutine(PreparationPhaseFlow(true));
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
        if (ObjectiveHealthSystem.Instance != null &&
            ModificacaoRunState.IsActive(ModificacaoGameplayEffect.SobrecargaDeNucleo))
        {
            ObjectiveHealthSystem.Instance.HealPercent(
                ModificacaoRunState.GetValue(ModificacaoGameplayEffect.SobrecargaDeNucleo, 0.05f));
        }

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
            StartCoroutine(PreparationPhaseFlow(false));
        }
    }

    // ════════════════════════════════════════════════════
    //  FASE DE PREPARACAO
    // ════════════════════════════════════════════════════

    private IEnumerator PreparationPhaseFlow(bool isFirstWave)
    {
        if (!HasAuthority) yield break;

        // 1) Pre-sorteia os inimigos da proxima onda
        PreGenerateNextWave();

        // 2) Monta o texto de anuncio
        string titulo = isFirstWave ? "Fase de Preparacao" : "Proxima Onda";
        string listaInimigos = BuildAnnouncementText(preGeneratedWaveList);
        float prepTime = isFirstWave ? prepTimeFirstWave : prepTimeBetweenWaves;

        int waveArrayIndex = CurrentHorde;
        if (customWaves != null && waveArrayIndex >= 0 && waveArrayIndex < customWaves.Count)
        {
            prepTime = customWaves[waveArrayIndex].prepTime;
        }

        if (ModificacaoRunState.IsActive(ModificacaoGameplayEffect.AvancoImplacavel))
            prepTime = 0f;

        Debug.Log($"[HordeManager] {titulo}: {listaInimigos} | Preparacao: {prepTime}s");

        // 3) Manda o anuncio para todos os clientes (e local)
        if (IsLocalMode)
        {
            ShowAnnouncerLocally(titulo, listaInimigos, prepTime);
        }
        else
        {
            ShowPreparationPhaseClientRpc(titulo, listaInimigos, prepTime);
        }

        // 4) Espera o tempo de preparacao ou ate pularem com P
        isPreparing = true;
        float elapsed = 0f;
        while (elapsed < prepTime && isPreparing)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        isPreparing = false;

        // 5) Esconde o anuncio
        if (IsLocalMode)
        {
            HideAnnouncerLocally();
        }
        else
        {
            HidePreparationPhaseClientRpc();
        }

        // 6) Inicia a onda usando a lista pre-gerada
        if (HasAuthority) StartNextHorde();
    }

    private void PreGenerateNextWave()
    {
        preGeneratedWaveList.Clear();

        int waveArrayIndex = CurrentHorde; // 0 before wave 1 starts
        if (customWaves != null && waveArrayIndex >= 0 && waveArrayIndex < customWaves.Count)
        {
            WaveConfig waveConfig = customWaves[waveArrayIndex];
            if (waveConfig.spawnSequence != null)
            {
                foreach (EnemySpawnConfig config in waveConfig.spawnSequence)
                {
                    if (config.enemyData != null)
                    {
                        int pathIndex = config.pathIndex;
                        if (pathIndex < 0 || spawnPaths == null || pathIndex >= spawnPaths.Count)
                        {
                            pathIndex = GetRandomPathIndex();
                        }

                        // Loop baseado na quantidade definida no novo campo spawnCount
                        int count = config.spawnCount > 0 ? config.spawnCount : 1;
                        for (int i = 0; i < count; i++)
                        {
                            preGeneratedWaveList.Add((config.enemyData, pathIndex, config.spawnDelay));
                        }
                    }
                }
            }
            else
            {
                if (enemyTypes == null || enemyTypes.Length == 0) return;
                if (spawnPaths == null || spawnPaths.Count == 0) return;

                int total = Random.Range(enemiesPerHordeMin, enemiesPerHordeMax + 1);

                for (int i = 0; i < total; i++)
                {
                    int typeIndex = Random.Range(0, enemyTypes.Length);
                    int pathIndex = Random.Range(0, spawnPaths.Count);
                    EnemyDataSO enemyData = enemyTypes[typeIndex];
                    if (enemyData != null)
                        preGeneratedWaveList.Add((enemyData, pathIndex, spawnInterval));
                }
            }
        }

        ApplyRunModifiersToGeneratedWave();
    }

    private void ApplyRunModifiersToGeneratedWave()
    {
        if (!ModificacaoRunState.IsActive(ModificacaoGameplayEffect.EnxameMassivo) ||
            preGeneratedWaveList == null ||
            preGeneratedWaveList.Count == 0)
        {
            return;
        }

        EnemyDataSO basicEnemy = FindBasicEnemyForSwarm();
        if (basicEnemy == null)
            return;

        int multiplier = Mathf.Max(1, Mathf.RoundToInt(ModificacaoRunState.GetValue(ModificacaoGameplayEffect.EnxameMassivo, 5f)));
        List<(EnemyDataSO enemy, int pathIndex, float spawnDelay)> adjusted = new List<(EnemyDataSO, int, float)>(preGeneratedWaveList.Count * multiplier);

        foreach (var entry in preGeneratedWaveList)
        {
            EnemyDataSO enemy = IsHeavyEnemy(entry.enemy) ? basicEnemy : entry.enemy;
            for (int i = 0; i < multiplier; i++)
                adjusted.Add((enemy, entry.pathIndex, entry.spawnDelay));
        }

        preGeneratedWaveList = adjusted;
    }

    private EnemyDataSO FindBasicEnemyForSwarm()
    {
        if (enemyTypes != null)
        {
            foreach (EnemyDataSO enemy in enemyTypes)
            {
                if (enemy != null && !IsHeavyEnemy(enemy))
                    return enemy;
            }
        }

        foreach (var entry in preGeneratedWaveList)
        {
            if (entry.enemy != null && !IsHeavyEnemy(entry.enemy))
                return entry.enemy;
        }

        return null;
    }

    private static bool IsHeavyEnemy(EnemyDataSO enemy)
    {
        if (enemy == null)
            return false;

        string name = enemy.name;
        return !string.IsNullOrEmpty(name) && name.ToLowerInvariant().Contains("monstro");
    }

    private string BuildAnnouncementText(List<(EnemyDataSO enemy, int pathIndex, float spawnDelay)> lista)
    {
        if (lista == null || lista.Count == 0) return "Nenhum inimigo";

        // Agrupa por caminho, depois conta cada tipo de inimigo dentro do caminho
        var porCaminho = new Dictionary<string, Dictionary<string, int>>();

        foreach (var entry in lista)
        {
            string nomeCaminho = "Caminho";
            if (entry.pathIndex >= 0 && entry.pathIndex < spawnPaths.Count)
            {
                string pn = spawnPaths[entry.pathIndex].pathName;
                if (!string.IsNullOrEmpty(pn)) nomeCaminho = pn;
            }

            if (!porCaminho.ContainsKey(nomeCaminho))
                porCaminho[nomeCaminho] = new Dictionary<string, int>();

            string nomeInimigo = entry.enemy != null ? entry.enemy.name : "Desconhecido";
            if (!porCaminho[nomeCaminho].ContainsKey(nomeInimigo))
                porCaminho[nomeCaminho][nomeInimigo] = 0;
            porCaminho[nomeCaminho][nomeInimigo]++;
        }

        // Monta o texto: "Ponte: 2x Aranha, 1x Aguia | Esgoto: 3x Escorpiao"
        var partes = new List<string>();
        foreach (var caminho in porCaminho)
        {
            string inimigos = string.Join(", ", caminho.Value.Select(kv => $"{kv.Value}x {kv.Key}"));
            partes.Add($"{caminho.Key}: {inimigos}");
        }

        return string.Join("\n", partes);
    }

    private void ShowAnnouncerLocally(string titulo, string listaInimigos, float duracao)
    {
        if (WaveAnnouncerUI.Instance != null)
            WaveAnnouncerUI.Instance.ShowAnnouncement(titulo, listaInimigos, duracao);
    }

    private void HideAnnouncerLocally()
    {
        if (WaveAnnouncerUI.Instance != null)
            WaveAnnouncerUI.Instance.Hide();
    }

    [ClientRpc]
    private void ShowPreparationPhaseClientRpc(string titulo, string listaInimigos, float duracao)
    {
        ShowAnnouncerLocally(titulo, listaInimigos, duracao);
    }

    [ClientRpc]
    private void HidePreparationPhaseClientRpc()
    {
        HideAnnouncerLocally();
    }

    // ════════════════════════════════════════════════════
    //  INICIO DA ONDA
    // ════════════════════════════════════════════════════

    private void StartNextHorde()
    {
        if (!HasAuthority) return;

        CurrentHorde++;
        WaveActive = true;

        enemiesToSpawnTotal = preGeneratedWaveList.Count;
        enemiesSpawnedCount = 0;
        EnemiesRemaining = enemiesToSpawnTotal;

        Debug.Log($"[HordeManager] Iniciando Horda {CurrentHorde} com {enemiesToSpawnTotal} inimigos!");

        if (spawnCoroutine != null) StopCoroutine(spawnCoroutine);
        spawnCoroutine = StartCoroutine(SpawnEnemiesOverTime());
    }

    // ════════════════════════════════════════════════════
    //  SPAWN DE INIMIGOS
    // ════════════════════════════════════════════════════

    private IEnumerator SpawnEnemiesOverTime()
    {
        int waveArrayIndex = CurrentHorde - 1; // Since CurrentHorde was incremented in StartNextHorde()
        bool isCustomWave = customWaves != null && waveArrayIndex >= 0 && waveArrayIndex < customWaves.Count;

        if (isCustomWave)
        {
            while (enemiesSpawnedCount < enemiesToSpawnTotal)
            {
                float delay = spawnInterval;
                if (preGeneratedWaveList.Count > 0)
                {
                    delay = preGeneratedWaveList[0].spawnDelay;
                }

                SpawnSingleEnemy();
                enemiesSpawnedCount++;

                yield return new WaitForSeconds(delay);
            }
        }
        else
        {
            while (enemiesSpawnedCount < enemiesToSpawnTotal)
            {
                int batchSize = Mathf.Min(enemiesPerInterval, enemiesToSpawnTotal - enemiesSpawnedCount);

                for (int i = 0; i < batchSize; i++)
                {
                    SpawnSingleEnemy();
                }

                enemiesSpawnedCount += batchSize;
                yield return new WaitForSeconds(spawnInterval);
            }
        }
    }

    private void SpawnSingleEnemy()
    {
        if (spawnPaths == null || spawnPaths.Count == 0) return;

        // Consome da lista pre-gerada se disponivel, senao sorteia
        EnemyDataSO enemyData;
        int pathIndex;
        if (preGeneratedWaveList.Count > 0)
        {
            var entry = preGeneratedWaveList[0];
            preGeneratedWaveList.RemoveAt(0);
            enemyData = entry.enemy;
            pathIndex = entry.pathIndex;
        }
        else if (enemyTypes != null && enemyTypes.Length > 0)
        {
            enemyData = enemyTypes[Random.Range(0, enemyTypes.Length)];
            pathIndex = GetRandomPathIndex();
        }
        else
        {
            return;
        }

        SpawnPath selectedPath = spawnPaths[pathIndex];
        if (selectedPath.spawnPoint == null) return;

        if (enemyData == null || enemyData.enemyPrefab == null)
        {
            // Prefab null em build significa referência quebrada de prefab variant no ScriptableObject.
            // Decrementar para não travar a onda (esse slot nunca terá inimigo para matar).
            Debug.LogError($"[HordeManager] enemyData '{enemyData?.name}' é null ou sem prefab em runtime! " +
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
            // Sem isso, em builds onde o EnemyPoolManager não inicializou a tempo,
            // nenhum inimigo era spawnado (o else if IsLocalMode retornava false).
            newEnemy = Instantiate(enemyData.enemyPrefab, selectedPath.spawnPoint.position, selectedPath.spawnPoint.rotation);
            if (newEnemy.TryGetComponent<NetworkObject>(out var netObj))
                netObj.Spawn(true);
        }

        if (newEnemy == null)
        {
            Debug.LogError($"[HordeManager] Spawn retornou null para tipo '{enemyData.name}'. " +
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
        PlayerRegistry.CollectValidPlayerTransforms(playerTargetCandidates);
        if (playerTargetCandidates.Count > 0)
            return playerTargetCandidates[Random.Range(0, playerTargetCandidates.Count)];

        return null;
    }

    private int GetValidPlayerCount()
    {
        PlayerRegistry.CollectValidPlayerTransforms(playerTargetCandidates);
        return playerTargetCandidates.Count;
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
