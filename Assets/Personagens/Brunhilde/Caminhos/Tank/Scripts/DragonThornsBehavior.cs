using UnityEngine;
using Unity.Netcode;
using ExoBeasts.Multiplayer.Sync;

public class DragonThornsBehavior : TowerBehavior
{
    [Header("ConfiguraÃ§Ãµes do Espinho")]
    public float damageReflectionPercent = 0.20f;

    public override void Initialize(TowerController owner)
    {
        base.Initialize(owner);
        if (!IsServer) return;

        if (towerController != null)
            towerController.OnDamageTaken += HandleDamageTaken;
    }

    private void HandleDamageTaken(float damage, Transform attacker)
    {
        NetworkGameplayResolver.TryResolveAttackerFromBuilding(this, out ulong attackerClientId, out PlayerHealthSystem attackerHealth);

        if (attacker == null)
            return;

        EnemyHealthSystem enemyHealth = attacker.GetComponent<EnemyHealthSystem>();
        if (enemyHealth != null && !enemyHealth.isDead)
        {
            float damageToReflect = damage * damageReflectionPercent;
            enemyHealth.ApplyAuthoritativeDamage(damageToReflect, 0f, false, attackerClientId, attackerHealth);
        }
    }

    private void OnDestroy()
    {
        if (towerController != null)
            towerController.OnDamageTaken -= HandleDamageTaken;
    }
}
