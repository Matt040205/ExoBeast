using UnityEngine;

public class TurretController : MonoBehaviour
{
    // A tag que identifica os inimigos no seu projeto
    [Header("Configurações da Torre")]
    [Tooltip("A tag dos objetos inimigos.")]
    [SerializeField] private string enemyTag = "Enemy";

    // O alcance (range) da torreta
    [Tooltip("O alcance de detecção da torre.")]
    [SerializeField] private float attackRange = 10f;

    // O dano que a torreta causa
    [Tooltip("O dano que a torre causa por ataque.")]
    [SerializeField] private float damagePerShot = 20f;

    // O tempo entre os ataques
    [Tooltip("O tempo de recarga entre os ataques, em segundos.")]
    [SerializeField] private float fireRate = 1f;

    // A vida que a torreta tem
    [Tooltip("A vida máxima da torreta.")]
    [SerializeField] private float maxHealth = 100f;
    private float currentHealth;

    private float nextFireTime;
    private Transform targetEnemy;

    void Start()
    {
        // Inicializa a vida da torre
        currentHealth = maxHealth;
        // Chama a função de busca de inimigos a cada 0.5 segundos para otimizar
        InvokeRepeating("FindTarget", 0f, 0.5f);
    }

    void Update()
    {
        if (targetEnemy != null)
        {
            // Aponta para o inimigo encontrado
            LookAtTarget();

            // Verifica se pode atacar com base no tempo de recarga
            if (Time.time >= nextFireTime)
            {
                ShootAtTarget();
                nextFireTime = Time.time + 1f / fireRate;
            }
        }
    }

    private void FindTarget()
    {
        // Encontra todos os objetos com a tag 'Enemy'
        GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);

        // Verifica se há inimigos no array
        if (enemies.Length > 0)
        {
            Transform closestEnemy = null;
            float closestDistance = Mathf.Infinity;

            // Percorre a lista de inimigos para encontrar o mais próximo e dentro do alcance
            foreach (GameObject enemy in enemies)
            {
                float distance = Vector3.Distance(transform.position, enemy.transform.position);

                // Se o inimigo estiver dentro do alcance e for o mais próximo até agora
                if (distance <= attackRange && distance < closestDistance)
                {
                    closestDistance = distance;
                    closestEnemy = enemy.transform;
                }
            }
            targetEnemy = closestEnemy;
        }
        else
        {
            // Se não houver inimigos, o alvo é nulo
            targetEnemy = null;
        }
    }

    private void LookAtTarget()
    {
        // Calcula a direção para o alvo
        Vector3 direction = targetEnemy.position - transform.position;
        // Cria uma rotação para olhar nessa direção
        Quaternion lookRotation = Quaternion.LookRotation(direction);
        // Aplica a rotação de forma suave
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 10f);
    }

    private void ShootAtTarget()
    {
        // Tenta obter o componente EnemyHealthSystem do alvo
        EnemyHealthSystem healthSystem = targetEnemy.GetComponent<EnemyHealthSystem>();

        if (healthSystem != null)
        {
            // Aplica o dano e verifica se o inimigo morreu
            bool isDead = healthSystem.TakeDamage(damagePerShot);

            if (isDead)
            {
                // Se o inimigo morreu, a torre perde o alvo
                targetEnemy = null;
            }
        }
    }

    // Desenha o alcance da torre na cena do Unity para facilitar a visualização
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}