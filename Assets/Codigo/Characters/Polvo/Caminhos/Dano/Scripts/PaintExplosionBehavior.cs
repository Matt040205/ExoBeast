using UnityEngine;
using Unity.Netcode;

/// <summary>
/// ── PaintExplosionBehavior ─────────────────────────────────
/// Faz os inimigos explodirem em tinta ao morrer.
/// ───────────────────────────────────────────────────────────
/// </summary>
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
        {
            towerController.OnEnemyKilled += HandleEnemyKilled;
        }
    }

    private void HandleEnemyKilled(EnemyHealthSystem target)
    {
        Vector3 position = target.transform.position;

        if (explosionVFX != null)
        {
            Instantiate(explosionVFX, position, Quaternion.identity);
        }

        Collider[] hits = Physics.OverlapSphere(position, explosionRadius);
        float damageToDeal = towerController.CurrentDamage * explosionDamagePct;

        foreach (var hit in hits)
        {
            if (hit.CompareTag("Enemy") && hit.gameObject != target.gameObject)
            {
                EnemyHealthSystem hp = hit.GetComponent<EnemyHealthSystem>();
                if (hp != null && !hp.isDead)
                {
                    hp.TakeDamage(damageToDeal);
                }
            }
        }
    }

    private void OnDestroy()
    {
        if (towerController != null)
        {
            towerController.OnEnemyKilled -= HandleEnemyKilled;
        }
    }
}
