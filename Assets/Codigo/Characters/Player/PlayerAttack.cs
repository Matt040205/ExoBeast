using UnityEngine;

public class PlayerAttack : MonoBehaviour
{
    [Header("Configura��es de Ataque")]
    public float damage = 25f;
    public float fireRate = 0.5f;
    public float range = 100f;

    private float nextTimeToFire = 0f;

    [Header("C�mera e UI")]
    // Refer�ncia � c�mera principal, de onde o tiro sair�
    public Camera mainCamera;

    // GameObject que representa o ponto na mira (opcional)
    public GameObject crosshairDot;

    void Start()
    {
        // Se a c�mera n�o for atribu�da no Inspector, pega a principal
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        // Garante que o ponto da mira esteja ativo se for atribu�do
        if (crosshairDot != null)
        {
            crosshairDot.SetActive(true);
        }
    }

    void Update()
    {
        // Verifica se o jogador clicou para atirar e se o tempo de recarga j� passou
        if (Input.GetMouseButton(0) && Time.time >= nextTimeToFire)
        {
            nextTimeToFire = Time.time + 1f / fireRate;
            Shoot();
        }
    }

    void Shoot()
    {
        RaycastHit hit;

        // Cria um raio a partir do centro da tela, na dire��o da c�mera
        // Este � o m�todo HitScan
        if (Physics.Raycast(mainCamera.transform.position, mainCamera.transform.forward, out hit, range))
        {
            Debug.Log("Acertou: " + hit.transform.name);

            EnemyHealthSystem enemyHealth = hit.transform.GetComponent<EnemyHealthSystem>();

            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(damage);
            }
        }
    }
}