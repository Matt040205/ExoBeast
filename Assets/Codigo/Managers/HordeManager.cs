using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using ExoBeasts.Multiplayer.GameServer;

/// <summary>
/// ── HordeManager ───────────────────────────────────────
/// Gerencia ondas de inimigos com autoridade no servidor.
///
///  ▸ NetworkVariables: currentHorde, enemiesRemaining, isWaveActive
///  ▸ Server: spawna inimigos via EnemyPoolManager em intervalos
///  ▸ Server: detecta fim de onda e carrega cena de vitoria via NGO SceneManager
///  ▸ OnEnemyKilledServerRpc: qualquer cliente pode notificar morte de inimigo
///  ▸ Distribui alvos entre jogadores via PlayerRegistry
/// ─────────────────────────────────────────────────────
/// </summary>
public class HordeManager : NetworkBehaviour
{
    public static HordeManager Instance { get; private set; }

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

    [Header("Interface (UI)")]
    public TextMeshProUGUI hordeText;
    public TextMeshProUGUI hordeTextBuild;

    [Header("Network Variables")]
    public NetworkVariable<int> currentHorde = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> enemiesRemaining = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> isWaveActive = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private int enemiesToSpawnTotal;
    private int enemiesSpawnedCount = 0;
    private Coroutine spawnCoroutine;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            StartCoroutine(WaitForPlayersAndBegin());
        }

        currentHorde.OnValueChanged += OnCurrentHordeChanged;
        UpdateHordeUI(currentHorde.Value);
    }

    private void OnCurrentHordeChanged(int oldVal, int newVal) => UpdateHordeUI(newVal);

    public override void OnNetworkDespawn()
    {
        currentHorde.OnValueChanged -= OnCurrentHordeChanged;
        base.OnNetworkDespawn();
    }

    private IEnumerator WaitForPlayersAndBegin()
    {
        while (PlayerRegistry.Instance == null || PlayerRegistry.Instance.GetPlayerCount() == 0)
        {
            yield return new WaitForSeconds(1f);
        }

        yield return new WaitForSeconds(3f); 
        StartNextHorde();
    }

    private void UpdateHordeUI(int current)
    {
        if (hordeTextBuild != null) hordeTextBuild.text = $"{current}/{victoryHorde}";
        if (hordeText != null) hordeText.text = $"{current}/{victoryHorde}";
    }

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

    private void OnWaveCompleted()
    {
        if (!IsServer) return;

        isWaveActive.Value = false;

        if (currentHorde.Value >= victoryHorde)
        {
            // Vitoria via Sincronizacao de Cena do NGO
            NetworkManager.Singleton.SceneManager.LoadScene("Win", LoadSceneMode.Single);
        }
        else
        {
            StartCoroutine(WaitAndStartNextWave());
        }
    }

    private IEnumerator WaitAndStartNextWave()
    {
        yield return new WaitForSeconds(timeBetweenWaves);
        if (IsServer) StartNextHorde();
    }

    private void StartNextHorde()
    {
        if (!IsServer) return;

        currentHorde.Value++;
        isWaveActive.Value = true;

        enemiesToSpawnTotal = Random.Range(enemiesPerHordeMin, enemiesPerHordeMax + 1);
        enemiesSpawnedCount = 0;
        enemiesRemaining.Value = enemiesToSpawnTotal;

        if (spawnCoroutine != null) StopCoroutine(spawnCoroutine);
        spawnCoroutine = StartCoroutine(SpawnEnemiesOverTime());
    }

    private IEnumerator SpawnEnemiesOverTime()
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

    private void SpawnSingleEnemy()
    {
        if (spawnPaths == null || spawnPaths.Count == 0 || enemyTypes.Length == 0) return;

        int pathIndex = GetRandomPathIndex();
        SpawnPath selectedPath = spawnPaths[pathIndex];

        if (selectedPath.spawnPoint == null) return;

        int enemyTypeIndex = Random.Range(0, enemyTypes.Length);
        EnemyDataSO enemyData = enemyTypes[enemyTypeIndex];

        GameObject newEnemy = EnemyPoolManager.Instance.GetPooledEnemy(enemyData.enemyPrefab, selectedPath.spawnPoint.position, selectedPath.spawnPoint.rotation);

        if (newEnemy != null)
        {
            EnemyController enemyController = newEnemy.GetComponent<EnemyController>();
            if (enemyController != null)
            {
                Transform target = GetRandomPlayerTarget();
                enemyController.InitializeEnemy(target, selectedPath.patrolPoints, enemyData, currentHorde.Value);
            }
        }
    }

    private Transform GetRandomPlayerTarget()
    {
        if (PlayerRegistry.Instance == null || PlayerRegistry.Instance.GetPlayerCount() == 0) return null;

        var players = PlayerRegistry.Instance.GetAllPlayers();
        var clientIds = new List<ulong>(players.Keys);
        ulong randomId = clientIds[Random.Range(0, clientIds.Count)];

        return players[randomId].transform;
    }

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
