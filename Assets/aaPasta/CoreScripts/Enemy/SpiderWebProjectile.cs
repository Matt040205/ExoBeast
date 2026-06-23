using UnityEngine;

public class SpiderWebProjectile : MonoBehaviour
{
    [Header("Configurações do Projétil")]
    public float speed = 15f;
    public float maxLifeTime = 5f;

    private Transform target;
    private float damage;
    private bool enableTrapMechanic;
    private GameObject shooter;
    private bool hasHit = false;

    public void Launch(Transform newTarget, float newDamage, bool enableTrap, GameObject newShooter)
    {
        target = newTarget;
        damage = newDamage;
        enableTrapMechanic = enableTrap;
        shooter = newShooter;
        hasHit = false;

        Destroy(gameObject, maxLifeTime);
    }

    private void Update()
    {
        if (target == null)
        {
            // Se o alvo morreu ou sumiu, apenas avança para frente
            transform.Translate(transform.forward * speed * Time.deltaTime, Space.World);
            return;
        }

        // Segue o alvo
        Vector3 targetCenter = target.position + Vector3.up * 0.5f; // mira no centro
        Vector3 dir = (targetCenter - transform.position).normalized;
        transform.position += dir * speed * Time.deltaTime;
        transform.forward = dir;

        // Se estiver muito próximo, aplica o impacto (para evitar falhas de colisão por velocidade)
        if (Vector3.Distance(transform.position, targetCenter) < 0.8f)
        {
            HandleHit(target.gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleHit(other.gameObject);
    }

    private void HandleHit(GameObject hitObj)
    {
        if (hasHit) return;

        // Verifica se atingiu o atirador ou outro inimigo
        if (hitObj == shooter || hitObj.CompareTag("Enemy")) return;

        // Tenta achar Player
        PlayerHealthSystem playerHealth = hitObj.GetComponentInParent<PlayerHealthSystem>()
            ?? hitObj.GetComponentInChildren<PlayerHealthSystem>()
            ?? hitObj.GetComponent<PlayerHealthSystem>();

        if (playerHealth != null)
        {
            hasHit = true;
            // Aplica dano
            playerHealth.TakeDamage(damage, shooter != null ? shooter.transform : null, false);

            // Aplica debuff no servidor
            if (Unity.Netcode.NetworkManager.Singleton == null || Unity.Netcode.NetworkManager.Singleton.IsServer)
            {
                SpiderWebDebuffPlayer debuff = playerHealth.GetComponent<SpiderWebDebuffPlayer>();
                if (debuff == null)
                    debuff = playerHealth.gameObject.AddComponent<SpiderWebDebuffPlayer>();
                
                debuff.OnHit(enableTrapMechanic);
            }

            Destroy(gameObject);
            return;
        }

        // Tenta achar Torre
        TowerController tower = hitObj.GetComponentInParent<TowerController>()
            ?? hitObj.GetComponentInChildren<TowerController>()
            ?? hitObj.GetComponent<TowerController>();

        if (tower != null)
        {
            hasHit = true;
            // Aplica dano
            tower.TakeDamage(damage);

            // Aplica debuff no servidor
            if (Unity.Netcode.NetworkManager.Singleton == null || Unity.Netcode.NetworkManager.Singleton.IsServer)
            {
                SpiderWebDebuffTower debuff = tower.GetComponent<SpiderWebDebuffTower>();
                if (debuff == null)
                    debuff = tower.gameObject.AddComponent<SpiderWebDebuffTower>();
                
                debuff.OnHit(enableTrapMechanic);
            }

            Destroy(gameObject);
            return;
        }
    }
}
