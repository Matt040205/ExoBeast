using UnityEngine;
using Unity.Netcode;
using System.Collections;

namespace ExoBeasts.Multiplayer.Sync
{
    /// <summary>
    /// Gerencia o sistema de hordas/waves sincronizado
    /// Apenas o servidor controla spawn de inimigos
    /// </summary>
    public class NetworkedHorde : NetworkBehaviour
    {
        private static NetworkedHorde _instance;
        public static NetworkedHorde Instance => _instance;

        [Header("Wave Settings")]
        [SerializeField] private float timeBetweenWaves = 30f;
        [SerializeField] private float spawnInterval = 2f;
        [SerializeField] private int baseEnemiesPerWave = 10;

        [Header("Wave State")]
        public NetworkVariable<int> CurrentWave = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public NetworkVariable<int> EnemiesRemaining = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public NetworkVariable<int> EnemiesSpawned = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public NetworkVariable<bool> IsWaveActive = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        // TODO: Adicionar referencia ao pool de inimigos quando existir
        // private EnemyPoolManager enemyPool;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                Debug.Log("[NetworkedHorde] Sistema de hordas inicializado no servidor");
                // Aguardar alguns segundos antes de iniciar primeira wave
                StartCoroutine(StartFirstWaveDelayed());
            }

            // Escutar mudancas
            CurrentWave.OnValueChanged += OnWaveChanged;
            EnemiesRemaining.OnValueChanged += OnEnemiesRemainingChanged;
        }

        private IEnumerator StartFirstWaveDelayed()
        {
            yield return new WaitForSeconds(5f);
            StartNextWave();
        }

        /// <summary>
        /// Iniciar proxima wave
        /// </summary>
        private void StartNextWave()
        {
            if (!IsServer) return;

            CurrentWave.Value++;
            IsWaveActive.Value = true;

            int enemyCount = CalculateEnemyCount(CurrentWave.Value);
            EnemiesRemaining.Value = enemyCount;
            EnemiesSpawned.Value = 0;

            Debug.Log($"[NetworkedHorde] Iniciando Wave {CurrentWave.Value} com {enemyCount} inimigos");

            // Notificar clientes
            OnWaveStartedClientRpc(CurrentWave.Value, enemyCount);

            // Iniciar spawn de inimigos
            StartCoroutine(SpawnWaveEnemies(enemyCount));
        }

        private int CalculateEnemyCount(int wave)
        {
            // Formula simples: aumentar 20% a cada wave
            return Mathf.RoundToInt(baseEnemiesPerWave * Mathf.Pow(1.2f, wave - 1));
        }

        private IEnumerator SpawnWaveEnemies(int totalEnemies)
        {
            if (!IsServer) yield break;

            for (int i = 0; i < totalEnemies; i++)
            {
                SpawnEnemy();
                EnemiesSpawned.Value++;

                yield return new WaitForSeconds(spawnInterval);
            }

            Debug.Log($"[NetworkedHorde] Todos os {totalEnemies} inimigos da wave {CurrentWave.Value} foram spawnados");
        }

        private void SpawnEnemy()
        {
            if (!IsServer) return;

            // TODO: Obter inimigo do pool
            // GameObject enemy = enemyPool.GetPooledEnemy();
            // if (enemy == null)
            // {
            //     Debug.LogWarning("[NetworkedHorde] Pool de inimigos vazio!");
            //     return;
            // }

            // TODO: Posicionar inimigo em spawn point
            // Vector3 spawnPos = GetRandomSpawnPoint();
            // enemy.transform.position = spawnPos;

            // Spawnar na rede
            // var networkObject = enemy.GetComponent<NetworkObject>();
            // if (networkObject != null)
            // {
            //     networkObject.Spawn();
            // }

            Debug.Log("[NetworkedHorde] Inimigo spawnado (placeholder)");
        }

        /// <summary>
        /// Chamar quando um inimigo for morto
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void OnEnemyKilledServerRpc()
        {
            if (!IsServer) return;

            EnemiesRemaining.Value--;
            Debug.Log($"[NetworkedHorde] Inimigo morto. Restantes: {EnemiesRemaining.Value}");

            // Verificar se wave acabou
            if (EnemiesRemaining.Value <= 0 && IsWaveActive.Value)
            {
                OnWaveCompleted();
            }
        }

        private void OnWaveCompleted()
        {
            if (!IsServer) return;

            IsWaveActive.Value = false;
            Debug.Log($"[NetworkedHorde] Wave {CurrentWave.Value} completa!");

            // Notificar clientes
            OnWaveCompletedClientRpc(CurrentWave.Value);

            // Iniciar proxima wave apos delay
            StartCoroutine(WaitAndStartNextWave());
        }

        private IEnumerator WaitAndStartNextWave()
        {
            Debug.Log($"[NetworkedHorde] Proxima wave em {timeBetweenWaves} segundos");
            yield return new WaitForSeconds(timeBetweenWaves);
            StartNextWave();
        }

        // Client RPCs
        [ClientRpc]
        private void OnWaveStartedClientRpc(int waveNumber, int enemyCount)
        {
            Debug.Log($"[NetworkedHorde] Wave {waveNumber} iniciada! {enemyCount} inimigos chegando");
            // TODO: Mostrar UI de nova wave
            // TODO: Reproduzir som de alerta
        }

        [ClientRpc]
        private void OnWaveCompletedClientRpc(int waveNumber)
        {
            Debug.Log($"[NetworkedHorde] Wave {waveNumber} completa!");
            // TODO: Mostrar UI de wave completa
            // TODO: Reproduzir som de vitoria
        }

        // Callbacks
        private void OnWaveChanged(int oldValue, int newValue)
        {
            Debug.Log($"[NetworkedHorde] Wave mudou: {oldValue} -> {newValue}");
        }

        private void OnEnemiesRemainingChanged(int oldValue, int newValue)
        {
            // Atualizar UI de progresso
            if (newValue == 0 && oldValue > 0)
            {
                Debug.Log("[NetworkedHorde] Todos os inimigos foram eliminados!");
            }
        }

        /// <summary>
        /// Forcar inicio de proxima wave (para testes)
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void ForceStartNextWaveServerRpc()
        {
            if (!IsServer) return;

            if (IsWaveActive.Value)
            {
                Debug.LogWarning("[NetworkedHorde] Wave ja esta ativa!");
                return;
            }

            StopAllCoroutines();
            StartNextWave();
        }

        public override void OnNetworkDespawn()
        {
            CurrentWave.OnValueChanged -= OnWaveChanged;
            EnemiesRemaining.OnValueChanged -= OnEnemiesRemainingChanged;
        }
    }
}
