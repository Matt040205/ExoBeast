using Unity.Netcode;
using UnityEngine;
using ExoBeasts.Multiplayer.Sync;

[RequireComponent(typeof(NetworkObject))]
public class TemorSismicoLogic : NetworkBehaviour
{
    private float range;
    private float angle;
    private float damage;
    private float knockUpDuration;
    private float knockUpForce;
    private float vulnerabilityMultiplier;
    private float vulnerabilityDuration;
    private bool isConfigured;
    private bool hasAppliedEffects;
    private ulong attackerClientId;
    private PlayerHealthSystem attackerHealth;

    public void Setup(
        GameObject owner,
        float newRange,
        float newAngle,
        float newDamage,
        float newKnockUpDuration,
        float newKnockUpForce,
        float newVulnerabilityMultiplier,
        float newVulnerabilityDuration)
    {
        range = newRange;
        angle = newAngle;
        damage = newDamage;
        knockUpDuration = newKnockUpDuration;
        knockUpForce = newKnockUpForce;
        vulnerabilityMultiplier = newVulnerabilityMultiplier;
        vulnerabilityDuration = newVulnerabilityDuration;
        isConfigured = true;

        NetworkGameplayResolver.TryResolveAttackerFromPlayer(owner, out attackerClientId, out attackerHealth);
        transform.position = owner.transform.position;
        transform.rotation = AbilityAimUtility.ResolveAimRotation(owner);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            ApplyEffectsIfReady();
            Invoke(nameof(DespawnSelf), 2f);
        }
    }

    private void Start()
    {
        bool isNetworkSession = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (!isNetworkSession)
        {
            ApplyEffectsIfReady();
            Destroy(gameObject, 2f);
        }
    }

    private void ApplyEffectsIfReady()
    {
        if (!isConfigured || hasAppliedEffects)
            return;

        hasAppliedEffects = true;

        Collider[] hits = Physics.OverlapSphere(transform.position, range);
        foreach (Collider hit in hits)
        {
            if (!hit.CompareTag("Enemy"))
                continue;

            Vector3 directionToEnemy = (hit.transform.position - transform.position).normalized;
            if (Vector3.Angle(transform.forward, directionToEnemy) >= angle * 0.5f)
                continue;

            EnemyHealthSystem enemyHealth = hit.GetComponent<EnemyHealthSystem>();
            if (enemyHealth != null)
            {
                DamageContext damageContext = new DamageContext(attackerClientId, false, DamageFeedbackMode.AllObservers);
                enemyHealth.ApplyAuthoritativeDamage(damage, 0f, damageContext, attackerHealth);

                if (vulnerabilityMultiplier > 1f)
                    enemyHealth.AplicarVulnerabilidadeTemporaria(vulnerabilityMultiplier, vulnerabilityDuration);
            }

            EnemyController enemyController = hit.GetComponent<EnemyController>();
            if (enemyController != null)
                enemyController.ApplyKnockUp(knockUpDuration, knockUpForce);
        }
    }

    private void DespawnSelf()
    {
        if (IsServer && NetworkObject.IsSpawned)
            NetworkObject.Despawn();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, 5f);
    }
}
