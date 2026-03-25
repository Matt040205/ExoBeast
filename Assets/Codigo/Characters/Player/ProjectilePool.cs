using System.Collections.Generic;
using UnityEngine;

public class ProjectilePool : MonoBehaviour
{
    public static ProjectilePool Instance;

    [Header("Configurações")]
    public GameObject projectilePrefab;
    public int initialPoolSize = 20;
    public int maxPoolSize = 50;

    private Queue<GameObject> projectilePool = new Queue<GameObject>();
    private List<GameObject> activeProjectiles = new List<GameObject>();

    void Awake()
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

        // A linha "InitializePool();" foi removida daqui.
    }

    public void InitializePool()
    {
        if (projectilePrefab == null)
        {
            Debug.LogError("ProjectilePool: Tentativa de inicializar o pool, mas 'projectilePrefab' está nulo. O PlayerShooting esqueceu de defini-lo?", this);
            return;
        }

        for (int i = 0; i < initialPoolSize; i++)
        {
            CreateNewProjectile();
        }
    }

    void CreateNewProjectile()
    {
        if (projectilePrefab == null) return;

        if (projectilePool.Count >= maxPoolSize) return;

        GameObject proj = Instantiate(projectilePrefab, transform);
        proj.SetActive(false);
        projectilePool.Enqueue(proj);
    }

    public GameObject GetProjectile(Vector3 position, Quaternion rotation)
    {
        if (projectilePool.Count == 0)
        {
            CreateNewProjectile();
            if (projectilePool.Count == 0)
            {
                Debug.LogError("ProjectilePool: O pool está vazio e não foi possível criar um novo projétil (prefab nulo ou max pool atingido).", this);
                return null;
            }
        }

        GameObject projectile = projectilePool.Dequeue();
        projectile.transform.position = position;
        projectile.transform.rotation = rotation;
        projectile.SetActive(true);

        ProjectileVisual visual = projectile.GetComponent<ProjectileVisual>();
        if (visual != null)
        {
            visual.SetPoolReference(this);
        }

        activeProjectiles.Add(projectile);
        return projectile;
    }

    public void ReturnProjectile(GameObject projectile)
    {
        if (projectile == null) return;

        projectile.SetActive(false);
        projectilePool.Enqueue(projectile);
        activeProjectiles.Remove(projectile);
    }

    public void ClearAllProjectiles()
    {
        foreach (GameObject proj in activeProjectiles.ToArray())
        {
            ReturnProjectile(proj);
        }
    }
}