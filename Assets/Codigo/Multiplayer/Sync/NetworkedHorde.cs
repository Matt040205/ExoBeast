using UnityEngine;
using Unity.Netcode;
using System.Collections;

namespace ExoBeasts.Multiplayer.Sync
{
    /// <summary>
    /// ── NetworkedHorde ───────────────────────────────────
    /// Gerencia o sistema de waves de inimigos sincronizado em rede.
    ///
    ///  ▸ Apenas o servidor controla spawn, contagem e progressao
    ///  ▸ NetworkVariables: CurrentWave, EnemiesRemaining, EnemiesSpawned, IsWaveActive
    ///  ▸ OnEnemyKilledServerRpc: notificacao de morte (RequireOwnership = false)
    ///  ▸ ForceStartNextWaveServerRpc: disponivel para debug/testes
    ///  ▸ SpawnEnemy(): placeholder — aguarda pool de inimigos (Fase 5)
    ///  ▸ Singleton — acessivel via NetworkedHorde.Instance
    /// ─────────────────────────────────────────────────────
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
                StartCoroutine(StartFirstWaveDelayed());
            }

            CurrentWave.OnValueChanged += OnWaveChanged;
            EnemiesRemaining.OnValueChanged += OnEnemiesRemainingChanged;
        }

        private IEnumerator StartFirstWaveDelayed()
        {
            yield return new WaitForSeconds(5f);
            StartNextWave();
        }

        private void StartNextWave()
        {
            if (!IsServer) return;

            CurrentWave.Value++;
            IsWaveActive.Value = true;

            int enemyCount = CalculateEnemyCount(CurrentWave.Value);
            EnemiesRemaining.Value = enemyCount;
            EnemiesSpawned.Value = 0;

            Debug.Log($"[NetworkedHorde] Iniciando Wave {CurrentWave.Value} com {enemyCount} inimigos");
            OnWaveStartedClientRpc(CurrentWave.Value, enemyCount);
            StartCoroutine(SpawnWaveEnemies(enemyCount));
        }

        private int CalculateEnemyCount(int wave)
        {
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
            Debug.Log("[NetworkedHorde] Inimigo spawnado (placeholder)");
        }

        [ServerRpc(RequireOwnership = false)]
        public void OnEnemyKilledServerRpc()
        {
            if (!IsServer) return;

            EnemiesRemaining.Value = Mathf.Max(0, EnemiesRemaining.Value - 1);
            Debug.Log($"[NetworkedHorde] Inimigo morto. Restantes: {EnemiesRemaining.Value}");

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
            OnWaveCompletedClientRpc(CurrentWave.Value);
            StartCoroutine(WaitAndStartNextWave());
        }

        private IEnumerator WaitAndStartNextWave()
        {
            Debug.Log($"[NetworkedHorde] Proxima wave em {timeBetweenWaves} segundos");
            yield return new WaitForSeconds(timeBetweenWaves);
            StartNextWave();
        }

        [ClientRpc]
        private void OnWaveStartedClientRpc(int waveNumber, int enemyCount)
        {
            Debug.Log($"[NetworkedHorde] Wave {waveNumber} iniciada! {enemyCount} inimigos chegando");
        }

        [ClientRpc]
        private void OnWaveCompletedClientRpc(int waveNumber)
        {
            Debug.Log($"[NetworkedHorde] Wave {waveNumber} completa!");
        }

        private void OnWaveChanged(int oldValue, int newValue)
        {
            Debug.Log($"[NetworkedHorde] Wave mudou: {oldValue} -> {newValue}");
        }

        private void OnEnemiesRemainingChanged(int oldValue, int newValue)
        {
            if (newValue == 0 && oldValue > 0)
            {
                Debug.Log("[NetworkedHorde] Todos os inimigos foram eliminados!");
            }
        }

        /// <summary>Forcar inicio de proxima wave (para testes).</summary>
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
