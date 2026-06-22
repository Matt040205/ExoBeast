using UnityEngine;
using Unity.Netcode;
using ExoBeasts.Multiplayer.Sync;

public class PaintExplosionBehavior : TowerBehavior
{
    [Header("Configuraes da Exploso")]
    public GameObject explosionVFX;
    public float explosionRadius = 4f;
    [Tooltip("Dano da exploso em relao ao dano base da torre (Ex: 0.5 = 50%)")]
    public float explosionDamagePct = 0.5f;

    public override void Initialize(TowerController owner)
    {
        base.Initialize(owner);
        if (!IsServer) return;

        if (towerController != null)
            towerController.OnEnemyKilled += HandleEnemyKilled;
    }

    private void HandleEnemyKilled(EnemyHealthSystem target)
    {
        NetworkGameplayResolver.TryResolveAttackerFromBuilding(this, out ulong attackerClientId, out PlayerHealthSystem attackerHealth);
        Vector3 position = target.transform.position;

        if (explosionVFX != null)
            Instantiate(explosionVFX, position, Quaternion.identity);

        Collider[] hits = Physics.OverlapSphere(position, explosionRadius);
        float damageToDeal = towerController.CurrentDamage * explosionDamagePct;

        foreach (Collider hit in hits)
        {
            if (!hit.CompareTag("Enemy") || hit.gameObject == target.gameObject)
                continue;

            EnemyHealthSystem hp = hit.GetComponent<EnemyHealthSystem>();
            if (hp != null && !hp.isDead)
                hp.ApplyAuthoritativeDamage(damageToDeal, 0f, false, attackerClientId, attackerHealth);
        }
    }

    private void OnDestroy()
    {
        if (towerController != null)
            towerController.OnEnemyKilled -= HandleEnemyKilled;
    }
}
