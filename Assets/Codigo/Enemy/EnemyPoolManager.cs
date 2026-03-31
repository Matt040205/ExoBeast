using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;

/// <summary>
/// ── EnemyPoolManager ───────────────────────────────────
/// Pool de inimigos integrado com NGO Spawn/Despawn.
///
///  ▸ Server: GetPooledEnemy() retorna inimigo do pool e faz NetworkObject.Spawn
///  ▸ Server: ReturnToPool() faz Despawn(false) e devolve ao pool
///  ▸ Pools organizados por nome do prefab em dicionario de filas
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
            Destroy(this); // Apenas destrói o script duplicado, protegendo o resto do objeto!
            return;
        }
        Instance = this;
    }


    /// <summary>
    /// Retorna um inimigo pronto para ser spawnado no servidor.
    /// </summary>
    public GameObject GetPooledEnemy(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (!NetworkManager.Singleton.IsServer) return null;

        string prefabName = prefab.name;
        if (!pools.ContainsKey(prefabName))
        {
            pools[prefabName] = new Queue<GameObject>();
            for (int i = 0; i < initialPoolSize; i++)
            {
                CreateNewInPool(prefab, prefabName);
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

        enemy.transform.position = position;
        enemy.transform.rotation = rotation;
        enemy.SetActive(true);

        if (enemy.TryGetComponent<NetworkObject>(out var netObj))
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
        newObj.SetActive(false);
        pools[prefabName].Enqueue(newObj);
        return newObj;
    }

    /// <summary>
    /// Devolve um inimigo ao pool e o despawna da rede sem destruí-lo.
    /// </summary>
    public void ReturnToPool(GameObject enemy)
    {
        if (enemy == null || !NetworkManager.Singleton.IsServer) return;

        string prefabName = enemy.name;

        if (!pools.ContainsKey(prefabName))
        {
            pools[prefabName] = new Queue<GameObject>();
        }

        if (enemy.TryGetComponent<NetworkObject>(out var netObj))
        {
            if (netObj.IsSpawned)
            {
                netObj.Despawn(false);
            }
        }

        enemy.SetActive(false);
        enemy.transform.SetParent(transform);
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
