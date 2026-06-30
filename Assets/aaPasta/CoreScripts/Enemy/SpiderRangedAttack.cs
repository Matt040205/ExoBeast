using UnityEngine;

public class SpiderRangedAttack : MonoBehaviour
{
    [Header("Configurações Ranged")]
    public GameObject webProjectilePrefab;
    public Transform firePoint;
    
    [Tooltip("Habilita a mecânica do alvo ser preso por teias ao receber múltiplos hits.")]
    public bool enableTrapMechanic = true;

    [Header("FMOD - Sons")]
    public string eventoAtaque = AudioEventIds.EnemySpiderAttack;

    public void FireProjectile(Transform target, float damage)
    {
        if (webProjectilePrefab == null)
        {
            Debug.LogWarning($"[SpiderRangedAttack] Prefab de teia não definido em {gameObject.name}");
            return;
        }

        Vector3 spawnPosition = firePoint != null ? firePoint.position : transform.position;
        GameObject projObj = Instantiate(webProjectilePrefab, spawnPosition, Quaternion.identity);

        if (!string.IsNullOrEmpty(eventoAtaque))
        {
            ExoAudioService.PlayOneShot3D(eventoAtaque, spawnPosition);
        }
        
        SpiderWebProjectile projectile = projObj.GetComponent<SpiderWebProjectile>();
        if (projectile != null)
        {
            projectile.Launch(target, damage, enableTrapMechanic, gameObject);
        }
        else
        {
            Debug.LogError("[SpiderRangedAttack] Prefab de teia precisa ter o script SpiderWebProjectile anexado.");
        }
    }
}
