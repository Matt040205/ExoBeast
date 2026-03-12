using UnityEngine;
using System.Collections.Generic;

public class EnemyPoolManager : MonoBehaviour
{
    public static EnemyPoolManager Instance;

    [Header("Configura��es do Pool")]
    public GameObject enemyPrefab;
    public int poolSize = 20;

    private List<GameObject> enemyPool;

    private void Awake()
    {
        // Cria uma inst�ncia singleton
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        // Inicializa o pool
        enemyPool = new List<GameObject>();
        for (int i = 0; i < poolSize; i++)
        {
            GameObject newEnemy = Instantiate(enemyPrefab);
            newEnemy.SetActive(false);
            enemyPool.Add(newEnemy);
        }
    }

    // Pega um inimigo inativo do pool
    public GameObject GetPooledEnemy()
    {
        foreach (GameObject enemy in enemyPool)
        {
            if (!enemy.activeInHierarchy)
            {
                enemy.SetActive(true);
                return enemy;
            }
        }

        // Se o pool estiver vazio, cria mais um (opcional)
        GameObject newEnemy = Instantiate(enemyPrefab);
        enemyPool.Add(newEnemy);
        return newEnemy;
    }

    // Retorna o inimigo ao pool
    public void ReturnToPool(GameObject enemy)
    {
        enemy.SetActive(false);
    }
}