using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Pool de inimigos integrado com NGO Spawn/Despawn via handler.
/// </summary>
public class EnemyPoolManager : MonoBehaviour
{
    public static EnemyPoolManager Instance { get; private set; }

    [Header("Configuracao Base")]
    public int initialPoolSize = 5;
    public int maxPoolSize = 100;

    private readonly Dictionary<string, Queue<GameObject>> pools = new Dictionary<string, Queue<GameObject>>();
    private readonly HashSet<GameObject> registeredPrefabs = new HashSet<GameObject>();

    private bool IsNGOServer => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

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

    private void OnDestroy()
    {
        UnregisterNetworkPrefabs();

        if (Instance == this)
            Instance = null;
    }

    private void RegisterNetworkPrefabs()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || networkManager.NetworkConfig?.Prefabs == null)
            return;

        UnregisterNetworkPrefabs();

        foreach (NetworkPrefab networkPrefab in networkManager.NetworkConfig.Prefabs.Prefabs)
        {
            GameObject prefab = networkPrefab.Prefab;
            if (prefab == null || prefab.GetComponent<EnemyController>() == null)
                continue;

            if (prefab.GetComponent<NetworkObject>() == null)
                continue;

            networkManager.PrefabHandler.RemoveHandler(prefab);
            networkManager.PrefabHandler.AddHandler(prefab, new EnemyPrefabHandler(prefab, this));
            registeredPrefabs.Add(prefab);
        }
    }

    private void UnregisterNetworkPrefabs()
    {
        if (registeredPrefabs.Count == 0)
            return;

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager != null)
        {
            foreach (GameObject prefab in registeredPrefabs)
            {
                if (prefab != null)
                    networkManager.PrefabHandler.RemoveHandler(prefab);
            }
        }

        registeredPrefabs.Clear();
    }

    public GameObject GetPooledEnemy(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
            return null;

        string prefabName = prefab.name;
        if (!pools.TryGetValue(prefabName, out Queue<GameObject> pool))
        {
            pool = new Queue<GameObject>();
            pools[prefabName] = pool;

            for (int i = 0; i < initialPoolSize; i++)
            {
                GameObject preWarmed = CreateNewInPool(prefab, prefabName);
                if (preWarmed != null)
                    pool.Enqueue(preWarmed);
            }
        }

        GameObject enemy = TryDequeueValidPooledEnemy(pool);
        if (enemy == null)
            enemy = CreateNewInPool(prefab, prefabName);

        if (enemy == null)
            return null;

        NavMeshAgent navAgent = enemy.GetComponent<NavMeshAgent>();
        if (navAgent != null)
            navAgent.enabled = false;

        enemy.transform.position = position;
        enemy.transform.rotation = rotation;
        enemy.SetActive(true);

        if (navAgent != null)
        {
            navAgent.enabled = true;
            navAgent.Warp(position);
        }

        if (IsNGOServer && enemy.TryGetComponent(out NetworkObject netObj) && !netObj.IsSpawned)
            netObj.Spawn(true);

        return enemy;
    }

    private GameObject CreateNewInPool(GameObject prefab, string prefabName)
    {
        if (prefab == null)
            return null;

        GameObject newObj = Instantiate(prefab, transform);
        newObj.name = prefabName;

        NavMeshAgent navAgent = newObj.GetComponent<NavMeshAgent>();
        if (navAgent != null)
            navAgent.enabled = false;

        newObj.SetActive(false);
        return newObj;
    }

    private GameObject TryDequeueValidPooledEnemy(Queue<GameObject> pool)
    {
        if (pool == null)
            return null;

        while (pool.Count > 0)
        {
            GameObject candidate = pool.Dequeue();
            if (candidate != null)
                return candidate;
        }

        return null;
    }

    public void ReturnToPool(GameObject enemy)
    {
        if (enemy == null)
            return;

        NavMeshAgent navAgent = enemy.GetComponent<NavMeshAgent>();
        if (navAgent != null)
            navAgent.enabled = false;

        string prefabName = enemy.name.Replace("(Clone)", string.Empty).Trim();
        if (!pools.TryGetValue(prefabName, out Queue<GameObject> pool))
        {
            pool = new Queue<GameObject>();
            pools[prefabName] = pool;
        }

        bool hasNetworkObject = enemy.TryGetComponent(out NetworkObject netObj);
        if (IsNGOServer && hasNetworkObject && netObj.IsSpawned)
            netObj.Despawn(false);

        enemy.SetActive(false);

        if (IsNGOServer || !hasNetworkObject)
            enemy.transform.SetParent(transform);

        if (!pool.Contains(enemy))
            pool.Enqueue(enemy);
    }

    public void ClearAllPools()
    {
        foreach (Queue<GameObject> pool in pools.Values)
        {
            while (pool.Count > 0)
            {
                GameObject pooledObject = pool.Dequeue();
                if (pooledObject != null)
                    Destroy(pooledObject);
            }
        }

        pools.Clear();
    }
}

/// <summary>
/// Handler de spawn/despawn para redirecionar inimigos ao EnemyPoolManager.
/// </summary>
public class EnemyPrefabHandler : INetworkPrefabInstanceHandler
{
    private readonly GameObject prefab;
    private readonly EnemyPoolManager poolManager;

    public EnemyPrefabHandler(GameObject prefab, EnemyPoolManager poolManager)
    {
        this.prefab = prefab;
        this.poolManager = poolManager;
    }

    public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
    {
        GameObject obj = poolManager != null
            ? poolManager.GetPooledEnemy(prefab, position, rotation)
            : null;

        if (obj == null && prefab != null)
        {
            obj = Object.Instantiate(prefab, position, rotation);
            obj.name = prefab.name;
            obj.SetActive(true);
        }

        return obj != null ? obj.GetComponent<NetworkObject>() : null;
    }

    public void Destroy(NetworkObject networkObject)
    {
        if (networkObject == null)
            return;

        if (poolManager != null)
        {
            poolManager.ReturnToPool(networkObject.gameObject);
            return;
        }

        Object.Destroy(networkObject.gameObject);
    }
}
