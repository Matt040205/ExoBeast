using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;

public class EnemyPoolManager : MonoBehaviour
{
    public static EnemyPoolManager Instance;

    public GameObject enemyPrefab;
    public int poolSize = 20;

    private List<GameObject> enemyPool;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        enemyPool = new List<GameObject>();
    }

    private void Start()
    {
        if (NetworkManager.Singleton != null)
        {
            if (NetworkManager.Singleton.IsServer)
            {
                InitializePool();
            }
            else
            {
                NetworkManager.Singleton.OnServerStarted += InitializePool;
            }
        }
    }

    private void InitializePool()
    {
        for (int i = 0; i < poolSize; i++)
        {
            GameObject newEnemy = Instantiate(enemyPrefab);
            newEnemy.SetActive(false);
            enemyPool.Add(newEnemy);
        }
    }

    public GameObject GetPooledEnemy()
    {
        foreach (GameObject enemy in enemyPool)
        {
            if (!enemy.activeInHierarchy)
            {
                enemy.SetActive(true);
                NetworkObject netObj = enemy.GetComponent<NetworkObject>();
                if (netObj != null && !netObj.IsSpawned)
                {
                    netObj.Spawn(true);
                }
                return enemy;
            }
        }

        GameObject newEnemy = Instantiate(enemyPrefab);
        newEnemy.SetActive(true);
        enemyPool.Add(newEnemy);

        NetworkObject newNetObj = newEnemy.GetComponent<NetworkObject>();
        if (newNetObj != null)
        {
            newNetObj.Spawn(true);
        }
        return newEnemy;
    }

    public void ReturnToPool(GameObject enemy)
    {
        NetworkObject netObj = enemy.GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
        {
            netObj.Despawn(false);
        }
        enemy.SetActive(false);
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnServerStarted -= InitializePool;
        }
    }
}