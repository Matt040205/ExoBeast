using Unity.Netcode;
using UnityEngine;
using ExoBeasts.Multiplayer.Sync;

[RequireComponent(typeof(NetworkObject))]
public class TemorSismicoLogic : NetworkBehaviour
{
    private float range;
    private float angle;
    private float damage;
    private float stunDuration;
    private float knockUpDuration;
    private float knockUpForce;
    private float vulnerabilityMultiplier;
    private float vulnerabilityDuration;
    private bool isConfigured;
    private bool hasAppliedEffects;
    private ulong attackerClientId;
    private PlayerHealthSystem attackerHealth;

    // --- Efeitos Visuais ---
    // BUG FIX (Bug 1 - Sessao 7 Maio 2026): estes campos eram private e setados via Setup() que so
    // roda no servidor. Em clientes remotos, OnNetworkSpawn().SpawnVisualSlashes() saia cedo porque
    // groundSlashPrefab == null. Agora sao [SerializeField] preenchidos no prefab TemorSismico.prefab,
    // entao chegam pelos clientes naturalmente quando o NetworkObject eh spawnado.
    // ATENCAO GAME DESIGNER: arrastar o GroundSlash prefab no campo "groundSlashPrefab" do componente
    // TemorSismicoLogic no prefab (mesmo prefab que ja esta atribuido no SO HabilidadeTemorSismico).
    [Header("Visual (preenchido no prefab — sincroniza para todos os clientes)")]
    [SerializeField] public GameObject groundSlashPrefab;
    [SerializeField] public int numberOfSlashes = 3;
    [SerializeField] public float travelSpeed = 14f;
    [SerializeField] public float travelTime = 1.5f;
    [SerializeField] public float slowDownRate = 0.5f;
    [SerializeField] public float fadeOutGracePeriod = 2.0f;
    private float totalLifeTime;

    private class VisualSlash
    {
        public GameObject obj;
        public UnityEngine.VFX.VisualEffect vfx;
        public float timer;
        public int state; // 0=traveling, 1=slowing, 2=fading
        public float currentSpeed;
    }
    private System.Collections.Generic.List<VisualSlash> visualSlashes = new System.Collections.Generic.List<VisualSlash>();

    public void Setup(
        GameObject owner,
        float newRange,
        float newAngle,
        float newDamage,
        float newStunDuration,
        float newKnockUpDuration,
        float newKnockUpForce,
        float newVulnerabilityMultiplier,
        float newVulnerabilityDuration,
        GameObject vfxPrefabOverride = null,
        int slashesCountOverride = -1,
        float vfxSpeedOverride = -1f,
        float vfxTravelTimeOverride = -1f,
        float vfxSlowRateOverride = -1f,
        float vfxFadeTimeOverride = -1f)
    {
        range = Mathf.Max(0f, newRange);
        angle = Mathf.Clamp(newAngle, 0f, 360f);
        damage = Mathf.Max(0f, newDamage);
        stunDuration = Mathf.Max(0f, newStunDuration);
        knockUpDuration = Mathf.Max(0f, newKnockUpDuration);
        knockUpForce = Mathf.Max(0f, newKnockUpForce);
        vulnerabilityMultiplier = Mathf.Max(1f, newVulnerabilityMultiplier);
        vulnerabilityDuration = Mathf.Max(0f, newVulnerabilityDuration);

        // Overrides opcionais — se preenchido pelo SO, sobrescreve o serialized do prefab.
        // ATENCAO: overrides server-only (Setup so roda no servidor) — para sincronizar nos clientes,
        // os valores TEM QUE estar nos serialized fields do prefab. Estes overrides so afetam o servidor.
        if (vfxPrefabOverride != null) groundSlashPrefab = vfxPrefabOverride;
        if (slashesCountOverride > 0) numberOfSlashes = slashesCountOverride;
        if (vfxSpeedOverride > 0f) travelSpeed = vfxSpeedOverride;
        if (vfxTravelTimeOverride > 0f) travelTime = vfxTravelTimeOverride;
        if (vfxSlowRateOverride >= 0f) slowDownRate = vfxSlowRateOverride;
        if (vfxFadeTimeOverride > 0f) fadeOutGracePeriod = vfxFadeTimeOverride;

        numberOfSlashes = Mathf.Max(1, numberOfSlashes);
        totalLifeTime = Mathf.Max(2f, travelTime + fadeOutGracePeriod + 0.5f);

        isConfigured = true;

        NetworkGameplayResolver.TryResolveAttackerFromPlayer(owner, out attackerClientId, out attackerHealth);
        transform.position = owner.transform.position;
        transform.rotation = AbilityAimUtility.ResolveAimRotation(owner);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Em clientes nao-servidor, Setup() nunca eh chamado — entao totalLifeTime nao foi calculado.
        // Inicializamos com base nos serialized fields (que vieram com o prefab).
        if (!IsServer)
        {
            totalLifeTime = Mathf.Max(2f, travelTime + fadeOutGracePeriod + 0.5f);
        }

        if (IsServer)
        {
            ApplyEffectsIfReady();
            Invoke(nameof(DespawnSelf), totalLifeTime);
        }
        SpawnVisualSlashes();
    }

    private void Start()
    {
        bool isNetworkSession = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (!isNetworkSession)
        {
            ApplyEffectsIfReady();
            SpawnVisualSlashes();
            Destroy(gameObject, totalLifeTime);
        }
    }

    private void ApplyEffectsIfReady()
    {
        if (!isConfigured || hasAppliedEffects)
            return;

        hasAppliedEffects = true;

        Collider[] hits = Physics.OverlapSphere(transform.position, range);
        System.Collections.Generic.HashSet<EnemyController> affectedEnemies = new System.Collections.Generic.HashSet<EnemyController>();

        foreach (Collider hit in hits)
        {
            EnemyController enemyController = hit.GetComponentInParent<EnemyController>();
            if (enemyController == null || enemyController.IsDead || !affectedEnemies.Add(enemyController))
                continue;

            Vector3 directionToEnemy = enemyController.transform.position - transform.position;
            directionToEnemy.y = 0f;

            if (angle < 359.9f &&
                directionToEnemy.sqrMagnitude > 0.0001f &&
                Vector3.Angle(transform.forward, directionToEnemy.normalized) > angle * 0.5f)
                continue;

            EnemyHealthSystem enemyHealth = enemyController.GetComponent<EnemyHealthSystem>();
            if (enemyHealth == null)
                enemyHealth = hit.GetComponentInParent<EnemyHealthSystem>();

            if (enemyHealth != null)
            {
                DamageContext damageContext = new DamageContext(attackerClientId, false, DamageFeedbackMode.AllObservers);
                enemyHealth.ApplyAuthoritativeDamage(damage, 0f, damageContext, attackerHealth);

                if (vulnerabilityMultiplier > 1f && vulnerabilityDuration > 0f)
                    enemyHealth.AplicarVulnerabilidadeTemporaria(vulnerabilityMultiplier, vulnerabilityDuration);
            }

            if (stunDuration > 0f)
                enemyController.ApplyStun(stunDuration);

            enemyController.ApplyKnockUp(knockUpDuration, knockUpForce);
        }
    }

    private void DespawnSelf()
    {
        if (IsServer && NetworkObject.IsSpawned)
            NetworkObject.Despawn();
    }

    private void SpawnVisualSlashes()
    {
        if (groundSlashPrefab == null) return;
        
        float startingAngle = numberOfSlashes > 1 ? -angle / 2f : 0f;
        float angleStep = numberOfSlashes > 1 ? angle / (numberOfSlashes - 1) : 0f;

        for (int i = 0; i < numberOfSlashes; i++)
        {
            float currentAngle = startingAngle + (angleStep * i);
            Quaternion rotationOffset = Quaternion.Euler(0f, currentAngle, 0f);
            Quaternion finalRotation = transform.rotation * rotationOffset;

            GameObject slashObj = Instantiate(groundSlashPrefab, transform.position, finalRotation);
            
            // Remove scripts antigos se existirem no prefab para evitar conflitos
            var oldScript = slashObj.GetComponent("GroundSlash");
            if (oldScript != null) Destroy(oldScript);
            var oldShooter = slashObj.GetComponent("GroundSlashShooter");
            if (oldShooter != null) Destroy(oldShooter);
            var rb = slashObj.GetComponent<Rigidbody>();
            if (rb != null) Destroy(rb); // Movimentação será feita via Transform

            VisualSlash vs = new VisualSlash
            {
                obj = slashObj,
                vfx = slashObj.GetComponent<UnityEngine.VFX.VisualEffect>(),
                timer = 0f,
                state = 0,
                currentSpeed = travelSpeed
            };
            visualSlashes.Add(vs);
        }
    }

    private void Update()
    {
        for (int i = visualSlashes.Count - 1; i >= 0; i--)
        {
            var vs = visualSlashes[i];
            if (vs.obj == null)
            {
                visualSlashes.RemoveAt(i);
                continue;
            }

            vs.timer += Time.deltaTime;

            if (vs.state == 0) // Traveling
            {
                MoveSlash(vs);
                if (vs.timer >= travelTime)
                {
                    vs.state = 1; // Slowing
                    vs.timer = 0f;
                }
            }
            else if (vs.state == 1) // Slowing down
            {
                vs.currentSpeed -= slowDownRate * travelSpeed * Time.deltaTime;
                if (vs.currentSpeed <= 0)
                {
                    vs.currentSpeed = 0;
                    vs.state = 2; // Fading
                    vs.timer = 0f;
                    if (vs.vfx != null) vs.vfx.Stop();
                }
                else
                {
                    MoveSlash(vs);
                }
            }
            else if (vs.state == 2) // Fading
            {
                if (vs.timer >= fadeOutGracePeriod)
                {
                    Destroy(vs.obj);
                    visualSlashes.RemoveAt(i);
                }
            }
        }
    }

    private void MoveSlash(VisualSlash vs)
    {
        Transform t = vs.obj.transform;
        t.position += t.forward * vs.currentSpeed * Time.deltaTime;

        Vector3 rayStart = t.position + Vector3.up * 1f;
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 3f))
        {
            t.position = new Vector3(t.position.x, hit.point.y, t.position.z);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, range > 0f ? range : 5f);
    }
}
