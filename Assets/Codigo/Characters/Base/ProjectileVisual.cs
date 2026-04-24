using UnityEngine;
using Unity.Netcode;

/// <summary>
/// ── ProjectileVisual ───────────────────────────────────
/// Projetil visual local (NAO eh NetworkObject — cada cliente tem o seu).
///
///  ▸ Initialize(): seta dano, direcao e velocidade via Rigidbody
///  ▸ OnTriggerEnter: owner pede dano ao servidor via PlayerShooting.RequestDamageOnEnemy
///  ▸ Remotos: apenas efeito visual de impacto, sem logica de dano
///  ▸ Retorna ao ProjectilePool apos hit ou timeout
/// ─────────────────────────────────────────────────────
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class ProjectileVisual : MonoBehaviour
{
    [Header("Configurações")]
    public float speed = 80f;
    public float maxLifetime = 2f;
    public GameObject impactEffectPrefab;

    private float damage;
    private bool isCritical;
    private float armorPenetration;
    private PlayerHealthSystem playerHealth;

    private ProjectilePool pool;
    private Rigidbody rb;
    private bool hasHit;

    private bool isEmpoweredSkill = false;
    private float explosionRadius = 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = false;
        GetComponent<Collider>().isTrigger = true;
    }

    public void Initialize(float damage, bool isCritical, float armorPenetration, PlayerHealthSystem playerHealth, Vector3 direction, bool isEmpoweredSkill = false, float explosionRadius = 0f)
    {
        this.damage = damage;
        this.isCritical = isCritical;
        this.armorPenetration = armorPenetration;
        this.playerHealth = playerHealth;
        this.hasHit = false;
        this.isEmpoweredSkill = isEmpoweredSkill;
        this.explosionRadius = explosionRadius;

        rb.linearVelocity = direction * speed;
        CancelInvoke(nameof(ReturnToPool));
        Invoke(nameof(ReturnToPool), maxLifetime);
    }

    public void InitializeVisual(Vector3 direction)
    {
        Initialize(0f, false, 0f, null, direction, false, 0f);
    }

    public void SetPoolReference(ProjectilePool poolReference)
    {
        pool = poolReference;
    }

    void OnTriggerEnter(Collider other)
    {
        if (hasHit) return;

        if (other.CompareTag("Player")) return;

        hasHit = true;
        rb.linearVelocity = Vector3.zero;

        if (impactEffectPrefab != null)
        {
            Instantiate(impactEffectPrefab, transform.position, Quaternion.LookRotation(other.transform.position - transform.position));
        }

        // Dano apenas para o owner (damage > 0 = projetil do jogador local)
        if (damage > 0)
        {
            if (isEmpoweredSkill && JuiceManager.Instance != null)
            {
                JuiceManager.Instance.HitStop(0.08f);
            }

            PlayerShooting shooting = null;
            if (playerHealth != null && playerHealth.IsOwner)
            {
                shooting = playerHealth.GetComponent<PlayerShooting>();
            }
            else
            {
                var localPlayer = NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject();
                if (localPlayer != null) shooting = localPlayer.GetComponent<PlayerShooting>();
            }

            if (shooting != null)
            {
                if (isEmpoweredSkill && explosionRadius > 0f)
                {
                    // Dano em Área da Explosão
                    Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius);
                    foreach (var hitCol in hits)
                    {
                        if (hitCol.TryGetComponent<NetworkObject>(out var netObj))
                        {
                            var networkedEnemy = netObj.GetComponent<ExoBeasts.Multiplayer.Sync.NetworkedEnemy>();
                            if (networkedEnemy != null)
                            {
                                shooting.RequestDamageOnEnemy(netObj.NetworkObjectId, damage, armorPenetration, isCritical);
                            }
                        }
                    }
                    shooting.RequestExplosionVfx(transform.position, explosionRadius);
                }
                else
                {
                    // Dano Único Normal
                    if (other.TryGetComponent<NetworkObject>(out var enemyNetObj))
                    {
                        shooting.RequestDamageOnEnemy(enemyNetObj.NetworkObjectId, damage, armorPenetration, isCritical);
                    }
                }

                // Só processa lifesteal e eventos se for do owner real (para não duplicar em localPlayers falsos)
                if (playerHealth != null && playerHealth.IsOwner)
                {
                    playerHealth.TriggerDamageDealt(damage);
                }
            }
        }

        ReturnToPool();
    }

    void ReturnToPool()
    {
        CancelInvoke();
        rb.linearVelocity = Vector3.zero;
        if (pool != null)
        {
            pool.ReturnProjectile(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
