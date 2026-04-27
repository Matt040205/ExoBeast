using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using Unity.Netcode;

/// <summary>
/// ── EnemyPoolManager ───────────────────────────────────
/// Pool de inimigos integrado com NGO Spawn/Despawn via Handler.
///
///  ▸ Implementa INetworkPrefabInstanceHandler para que os clientes 
///    puxem objetos DESTE pool em vez de rodarem Instantiate() quando 
///    o Servidor fizer o Spawn.
/// ─────────────────────────────────────────────────────
/// </summary>
public class EnemyPoolManager : MonoBehaviour
{
    public static EnemyPoolManager Instance { get; private set; }

    [Header("Configuração Base")]
    public int initialPoolSize = 5;
    public int maxPoolSize = 100;

    private Dictionary<string, Queue<GameObject>> pools = new Dictionary<string, Queue<GameObject>>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        RegisterNetworkPrefabs();
    }

    /// <summary>
    /// Percorre os prefabs do NetworkManager e cadastra o manipulador de instâncias
    /// para cada um que seja um inimigo.
    /// </summary>
    private void RegisterNetworkPrefabs()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.NetworkConfig.Prefabs != null)
        {
            foreach (var networkPrefab in NetworkManager.Singleton.NetworkConfig.Prefabs.Prefabs)
            {
                var prefab = networkPrefab.Prefab;
                if (prefab != null && prefab.GetComponent<EnemyController>() != null)
                {
                    // Registra para interceptar a criação no cliente
                    NetworkManager.Singleton.PrefabHandler.AddHandler(prefab, new EnemyPrefabHandler(prefab, this));
                }
            }
        }
    }

    private bool IsNGOServer => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

    public GameObject GetPooledEnemy(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        string prefabName = prefab.name;
        if (!pools.ContainsKey(prefabName))
        {
            pools[prefabName] = new Queue<GameObject>();
            for (int i = 0; i < initialPoolSize; i++)
            {
                GameObject preWarmed = CreateNewInPool(prefab, prefabName);
                pools[prefabName].Enqueue(preWarmed); // Pré-popula a fila
            }
        }

        GameObject enemy;
        if (pools[prefabName].Count > 0)
        {
            enemy = pools[prefabName].Dequeue();
        }
        else
        {
            enemy = CreateNewInPool(prefab, prefabName);
        }

        if (enemy == null) return null;

        var navAgent = enemy.GetComponent<NavMeshAgent>();
        if (navAgent != null) navAgent.enabled = false;

        enemy.transform.position = position;
        enemy.transform.rotation = rotation;
        enemy.SetActive(true);

        if (navAgent != null)
        {
            navAgent.enabled = true;
            navAgent.Warp(position);
        }

        // IMPORTANTE: Só disparamos Spawn manual quando SOMOS O SERVIDOR.
        // O cliente passará por aqui quando o NGO chamar Instantiate() nele.
        if (IsNGOServer && enemy.TryGetComponent<NetworkObject>(out var netObj))
        {
            if (!netObj.IsSpawned)
            {
                netObj.Spawn(true);
            }
        }

        return enemy;
    }

    private GameObject CreateNewInPool(GameObject prefab, string prefabName)
    {
        GameObject newObj = Instantiate(prefab, transform);
        newObj.name = prefabName;

        var navAgent = newObj.GetComponent<NavMeshAgent>();
        if (navAgent != null) navAgent.enabled = false;

        newObj.SetActive(false);
        // NÃO enfileira aqui — o chamador (GetPooledEnemy) já usa o retorno diretamente.
        // Enfileirar causava o objeto aparecer na fila E ser usado ao mesmo tempo,
        // resultando em reuso duplicado na próxima chamada.
        return newObj;
    }

    /// <summary>
    /// Devolve um inimigo ao pool e o despawna da rede (se for server) sem destruí-lo.
    /// Clientes caem aqui pelo callback .Destroy() do PrefabHandler do NGO.
    /// </summary>
    public void ReturnToPool(GameObject enemy)
    {
        if (enemy == null) return;

        var navAgent = enemy.GetComponent<NavMeshAgent>();
        if (navAgent != null) navAgent.enabled = false;

        string prefabName = enemy.name;

        if (!pools.ContainsKey(prefabName))
        {
            pools[prefabName] = new Queue<GameObject>();
        }

        bool hasNetworkObject = enemy.TryGetComponent<NetworkObject>(out var netObj);

        if (IsNGOServer && hasNetworkObject)
        {
            if (netObj.IsSpawned)
            {
                netObj.Despawn(false);
            }
        }

        enemy.SetActive(false);

        if (IsNGOServer || !hasNetworkObject)
            enemy.transform.SetParent(transform);

        if (!pools[prefabName].Contains(enemy))
            pools[prefabName].Enqueue(enemy);
    }

    public void ClearAllPools()
    {
        foreach (var pool in pools.Values)
        {
            while (pool.Count > 0)
            {
                Destroy(pool.Dequeue());
            }
        }
        pools.Clear();
    }
}

/// <summary>
/// Classe injetada no NetworkManager para interceptar o Instantiate e Destroy automático do NGO
/// e redirecionar para o EnemyPoolManager.
/// </summary>
public class EnemyPrefabHandler : INetworkPrefabInstanceHandler
{
    private GameObject _prefab;
    private EnemyPoolManager _poolManager;

    public EnemyPrefabHandler(GameObject prefab, EnemyPoolManager poolManager)
    {
        _prefab = prefab;
        _poolManager = poolManager;
    }

    public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
    {
        // O servidor enviou o Spawn. Em vez de Instantiate puro, pegamos do pool.
        GameObject obj = _poolManager.GetPooledEnemy(_prefab, position, rotation);
        return obj.GetComponent<NetworkObject>();
    }

    public void Destroy(NetworkObject networkObject)
    {
        // O servidor despawnou o objeto. Em vez de Destroy puro, devolvemos ao pool.
        _poolManager.ReturnToPool(networkObject.gameObject);
    }
}
