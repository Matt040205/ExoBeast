using UnityEngine;
using UnityEngine.AI;
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
    // Verifica se o NGO está rodando como servidor
    private bool IsNGOServer => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
    private bool IsNGOActive => NetworkManager.Singleton != null && 
                                (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsClient);

    public GameObject GetPooledEnemy(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        // Bloqueia apenas se NGO está ativo e NÃO somos o servidor (somos cliente puro)
        if (IsNGOActive && !IsNGOServer) return null;

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

        // Desabilita o NavMeshAgent ANTES de mover para evitar conflitos
        var navAgent = enemy.GetComponent<NavMeshAgent>();
        if (navAgent != null) navAgent.enabled = false;

        enemy.transform.position = position;
        enemy.transform.rotation = rotation;
        enemy.SetActive(true);

        // Reabilita o NavMeshAgent e faz Warp para a posição correta no NavMesh
        if (navAgent != null)
        {
            navAgent.enabled = true;
            navAgent.Warp(position);
        }

        // Só faz NetworkObject.Spawn se o NGO estiver ativo como servidor
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

        // Desabilita NavMeshAgent antes de desativar para evitar erro de NavMesh
        var navAgent = newObj.GetComponent<NavMeshAgent>();
        if (navAgent != null) navAgent.enabled = false;

        newObj.SetActive(false);
        pools[prefabName].Enqueue(newObj);
        return newObj;
    }

    /// <summary>
    /// Devolve um inimigo ao pool e o despawna da rede sem destruí-lo.
    /// </summary>
    public void ReturnToPool(GameObject enemy)
    {
        if (enemy == null) return;
        // Bloqueia apenas se NGO está ativo e NÃO somos servidor
        if (IsNGOActive && !IsNGOServer) return;

        // Desabilita NavMeshAgent para evitar erros ao desativar
        var navAgent = enemy.GetComponent<NavMeshAgent>();
        if (navAgent != null) navAgent.enabled = false;

        string prefabName = enemy.name;

        if (!pools.ContainsKey(prefabName))
        {
            pools[prefabName] = new Queue<GameObject>();
        }

        // Só faz Despawn se o NGO estiver ativo e o objeto estiver spawnado na rede
        if (IsNGOServer && enemy.TryGetComponent<NetworkObject>(out var netObj))
        {
            if (netObj.IsSpawned)
            {
                netObj.Despawn(false);
            }
        }

        enemy.SetActive(false);

        // Só faz reparenting se o NetworkObject NÃO estiver spawned (evita SpawnStateException)
        bool canReparent = true;
        if (enemy.TryGetComponent<NetworkObject>(out var no) && no.IsSpawned)
            canReparent = false;

        if (canReparent)
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
