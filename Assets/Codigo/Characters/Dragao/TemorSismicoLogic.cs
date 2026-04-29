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

    public void Setup(
        GameObject owner,
        float newRange,
        float newAngle,
        float newDamage,
        float newStunDuration,
        float newKnockUpDuration,
        float newKnockUpForce,
        float newVulnerabilityMultiplier,
        float newVulnerabilityDuration)
    {
        range = Mathf.Max(0f, newRange);
        angle = Mathf.Clamp(newAngle, 0f, 360f);
        damage = Mathf.Max(0f, newDamage);
        stunDuration = Mathf.Max(0f, newStunDuration);
        knockUpDuration = Mathf.Max(0f, newKnockUpDuration);
        knockUpForce = Mathf.Max(0f, newKnockUpForce);
        vulnerabilityMultiplier = Mathf.Max(1f, newVulnerabilityMultiplier);
        vulnerabilityDuration = Mathf.Max(0f, newVulnerabilityDuration);
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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, range > 0f ? range : 5f);
    }
}
