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
    
    [System.Serializable]
    public struct CharacterImpact
    {
        [Tooltip("Parte do nome do personagem (ex: 'Coruja', 'Raposa', 'Polvo')")]
        public string characterName;
        public GameObject impactPrefab;
    }

    [Header("Efeitos de Impacto")]
    [Tooltip("Efeito padrão caso o personagem não tenha um específico na lista.")]
    public GameObject defaultImpactEffectPrefab;
    public CharacterImpact[] characterImpactEffects;

    private float damage;
    private bool isCritical;
    private float armorPenetration;
    private PlayerHealthSystem playerHealth;

    private ProjectilePool pool;
    private Rigidbody rb;
    private bool hasHit;

    private bool isEmpoweredSkill = false;
    private float explosionRadius = 0f;

    // Grace period: ignora colisões nos primeiros frames para não colidir
    // com o muzzle flash, arma, ou outros colliders do atirador que nascem
    // na mesma posição do projétil.
    private float spawnTime;
    private const float SPAWN_GRACE_PERIOD = 0.05f;
    private Transform ownerRoot;

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
        this.spawnTime = Time.time;

        // Guarda a raiz do atirador para ignorar colisões com filhos dele
        // (muzzle flash, arma, acessórios, VFX parenteados ao firePoint).
        if (playerHealth != null)
            ownerRoot = playerHealth.transform;

        rb.linearVelocity = direction * speed;
        CancelInvoke(nameof(ReturnToPool));
        Invoke(nameof(ReturnToPool), maxLifetime);
    }

    public void InitializeVisual(Vector3 direction, Transform shooterRoot = null)
    {
        Initialize(0f, false, 0f, null, direction, false, 0f);
        // Para projéteis visuais, o ownerRoot não é setado via playerHealth.
        // O chamador pode passar o Transform do atirador para evitar colisões
        // com o muzzle flash, arma e acessórios parenteados nele.
        if (shooterRoot != null)
            ownerRoot = shooterRoot;
    }

    public void SetPoolReference(ProjectilePool poolReference)
    {
        pool = poolReference;
    }

    void OnTriggerEnter(Collider other)
    {
        // OnTriggerEnter é chamado pelo Unity mesmo em MonoBehaviours desabilitados.
        // No projétil do servidor, o ProjectileVisual é desabilitado para deixar o
        // ServerAuthoritativeProjectile lidar com colisão e dano. Sem este check,
        // o ProjectileVisual destruía o GameObject antes do server processar o dano.
        if (!enabled) return;

        if (hasHit) return;

        if (other.CompareTag("Player")) return;

        // Grace period: ignora qualquer colisão nos primeiros frames após spawn.
        // Impede que o projétil colida com o muzzle flash, partículas de VFX,
        // ou outros colliders que existem no ponto de disparo.
        if (Time.time - spawnTime < SPAWN_GRACE_PERIOD) return;

        // Ignora colliders que são filhos do atirador (arma, acessórios, VFX).
        if (ownerRoot != null && other.transform.IsChildOf(ownerRoot)) return;

        hasHit = true;
        rb.linearVelocity = Vector3.zero;

        GameObject effectToSpawn = defaultImpactEffectPrefab;

        if (characterImpactEffects != null && characterImpactEffects.Length > 0 && playerHealth != null)
        {
            PlayerShooting shooting = playerHealth.GetComponent<PlayerShooting>();
            if (shooting != null && shooting.characterData != null)
            {
                string charName = shooting.characterData.name.ToLower();
                foreach (var impactDef in characterImpactEffects)
                {
                    if (!string.IsNullOrEmpty(impactDef.characterName) && charName.Contains(impactDef.characterName.ToLower()))
                    {
                        if (impactDef.impactPrefab != null)
                        {
                            effectToSpawn = impactDef.impactPrefab;
                        }
                        break;
                    }
                }
            }
        }

        if (effectToSpawn != null)
        {
            Instantiate(effectToSpawn, transform.position, Quaternion.LookRotation(other.transform.position - transform.position));
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
